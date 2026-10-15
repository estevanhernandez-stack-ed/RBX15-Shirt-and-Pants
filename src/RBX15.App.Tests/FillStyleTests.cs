using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Fill Style group (editor.js:1550-1613). Ledger rows: regions-state-model, fillstyle-type-buttons,
/// fillstyle-live-edit-rule, fillstyle-undo-snapshot-timing, fillstyle-color-pickers,
/// fillstyle-angle-scale, fillstyle-ui-not-synced-from-region.
/// </summary>
public class FillStyleTests
{
    static (EditorSession Session, FillStyleViewModel Fill, RegionsGroupViewModel Regions) Make(FakeColorPicker? picker = null)
    {
        var session = new EditorSession();
        var fill = new FillStyleViewModel(session, picker ?? new FakeColorPicker());
        return (session, fill, new RegionsGroupViewModel(session, fill));
    }

    static void Paint(RegionsGroupViewModel regions, params string[] keys)
    {
        foreach (var k in keys) Tile(regions, k).PaintCommand.Execute(null);
    }

    [Fact] // editor.js:1552 and editor.html:148-162
    public void Defaults_and_type_buttons_match_4_1()
    {
        var (_, fill, _) = Make();
        Assert.Equal(new Fill("solid", "#17d4fa", "#f22f89", 45, 16), fill.Current);
        Assert.Equal(("solid", "#17d4fa", "#f22f89", 45.0, 16.0), (fill.Type, fill.Color1, fill.Color2, fill.Angle, fill.Scale));
        Assert.Equal(
            new[] { ("solid", "Solid"), ("linear", "Gradient"), ("radial", "Radial"), ("stripes", "Stripes"), ("checker", "Checker"), ("dots", "Dots"), ("camo", "Camo") },
            fill.Types.Select(t => (t.Type, t.Label)));
        Assert.Equal((0.0, 360.0, 4.0, 48.0), (FillStyleViewModel.MinAngle, FillStyleViewModel.MaxAngle, FillStyleViewModel.MinScale, FillStyleViewModel.MaxScale));
    }

    [Fact] // fillstyle-live-edit-rule scenario: A,B follow the controls, C (another style) does not; one step per edit
    public void An_edit_retargets_regions_with_the_current_key_only()
    {
        var (session, fill, regions) = Make();
        fill.SetTypeCommand.Execute("linear");           // nothing painted yet: no step
        Paint(regions, "torso_front", "torso_back");     // A, B: linear 45
        fill.SetTypeCommand.Execute("solid");            // A, B follow to solid (one step)
        fill.SetTypeCommand.Execute("linear");           // and back
        Assert.Equal(4, session.UndoCount);
        var other = Fill.Solid("#123456");               // C: a style the controls do not hold
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_up", other));

        fill.Angle = 90;

        Assert.Equal(6, session.UndoCount);
        Assert.Equal(fill.Current, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal(fill.Current, DocumentOps.RegionFill(session.Document, "torso_back"));
        Assert.Equal(90, fill.Current.Angle);
        Assert.Equal("linear", fill.Current.Type);
        Assert.Same(other, DocumentOps.RegionFill(session.Document, "torso_up"));
    }

    [Fact] // fillstyle-live-edit-rule parityCheck, the whole sequence, compared as the exact RegionFills list
    public void The_row_scenario_runs_to_the_end_with_exact_region_fills()
    {
        var (session, fill, regions) = Make();
        fill.SetTypeCommand.Execute("linear");
        Paint(regions, "torso_front", "torso_back");                    // A, B: linear 45
        // C is another style. Painting it through the controls would move A and B with them
        // (identity is by value), so it is applied to the document directly, as in the first test.
        var c = Fill.Solid("#123456");
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_up", c));
        var linear45 = new Fill("linear", "#17d4fa", "#f22f89", 45, 16);

        fill.Angle = 90;                                                // A, B -> 90, C unchanged
        var linear90 = linear45 with { Angle = 90 };
        Assert.Equal([("torso_front", linear90), ("torso_back", linear90), ("torso_up", c)], session.Document.RegionFills);

        Paint(regions, "rarm_front");                                   // D at angle 90
        fill.Angle = 10;                                                // A, B, D -> 10, C unchanged
        var linear10 = linear45 with { Angle = 10 };
        Assert.Equal([("torso_front", linear10), ("torso_back", linear10), ("torso_up", c), ("rarm_front", linear10)],
                     session.Document.RegionFills);

        fill.SetTypeCommand.Execute("radial");                          // change type: all three follow, C still not
        var radial10 = linear10 with { Type = "radial" };
        Assert.Equal([("torso_front", radial10), ("torso_back", radial10), ("torso_up", c), ("rarm_front", radial10)],
                     session.Document.RegionFills);

        Paint(regions, "larm_front");                                   // E joins the group, then the type changes after
        fill.SetTypeCommand.Execute("stripes");
        var stripes10 = linear10 with { Type = "stripes" };
        Assert.Equal(
            [("torso_front", stripes10), ("torso_back", stripes10), ("torso_up", c), ("rarm_front", stripes10), ("larm_front", stripes10)],
            session.Document.RegionFills);

        fill.SetTypeCommand.Execute("solid");                           // C's own style is a different key: not merged
        var solid10 = linear10 with { Type = "solid" };
        Assert.Equal(
            [("torso_front", solid10), ("torso_back", solid10), ("torso_up", c), ("rarm_front", solid10), ("larm_front", solid10)],
            session.Document.RegionFills);
        Assert.NotEqual(c.Key, solid10.Key);
    }

    [Fact] // undo-empty-steps: re-clicking the active type changes nothing, so no step
    public void Reclicking_the_active_type_records_nothing()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        fill.SetTypeCommand.Execute("solid");
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // editor.js:1570-1579: an edit that touches no painted region records nothing
    public void An_edit_with_no_matching_region_changes_current_and_records_nothing()
    {
        var (session, fill, _) = Make();
        fill.Angle = 10;
        fill.Scale = 30;
        Assert.Equal((10.0, 30.0), (fill.Current.Angle, fill.Current.Scale));
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
    }

    [Fact] // fillstyle-undo-snapshot-timing parityCheck: a drag with a matching region is exactly one step
    public void A_slider_drag_is_one_step_and_undo_restores_the_starting_fills()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front", "torso_left");
        var start = fill.Current;

        using (fill.BeginSliderDrag())
        {
            for (int a = 46; a <= 120; a++) fill.Angle = a;
            Assert.Equal(2, session.UndoCount);          // the two paints; nothing yet for the drag
        }

        Assert.Equal(3, session.UndoCount);
        Assert.Equal(120, DocumentOps.RegionFill(session.Document, "torso_left")!.Angle);
        session.Undo();
        Assert.Equal(start, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal(start, DocumentOps.RegionFill(session.Document, "torso_left"));
    }

    [Fact] // Review Focus: a drag that ends where it started leaves the document unchanged, so no step
    public void A_drag_that_returns_to_its_start_records_nothing()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");

        using (fill.BeginSliderDrag())
        {
            fill.Scale = 30;
            fill.Scale = 16;
        }

        Assert.Equal(1, session.UndoCount);
        Assert.Equal(FillStyleViewModel.DefaultFill, DocumentOps.RegionFill(session.Document, "torso_front"));
    }

    [Fact] // the fix: a change outside a drag (arrow key, track click) is its own step; 4.1 recorded none
    public void Keyboard_slider_changes_are_one_step_each()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        fill.Angle = 46;
        fill.Angle = 47;
        Assert.Equal(3, session.UndoCount);
        session.Undo();
        Assert.Equal(46, DocumentOps.RegionFill(session.Document, "torso_front")!.Angle);
    }

    [Theory] // fillstyle-angle-scale: whole numbers inside the range's bounds
    [InlineData(45.4, 45)]
    [InlineData(45.5, 46)]
    [InlineData(-3, 0)]
    [InlineData(400, 360)]
    public void Angle_is_a_whole_number_in_0_to_360(double set, double expected)
    {
        var (_, fill, _) = Make();
        fill.Angle = set;
        Assert.Equal(expected, fill.Angle);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(48.6, 48)]
    [InlineData(12.5, 13)]
    public void Scale_is_a_whole_number_in_4_to_48(double set, double expected)
    {
        var (_, fill, _) = Make();
        fill.Scale = set;
        Assert.Equal(expected, fill.Scale);
    }

    [Fact] // Review Focus: cancel, the same color, and an uppercase answer
    public void The_color_picker_is_seeded_stores_lowercase_and_cancel_or_same_color_records_nothing()
    {
        var picker = new FakeColorPicker().Answer(null).Answer("#17D4FA").Answer("#ABCDEF").Answer("#00ff00");
        var (session, fill, regions) = Make(picker);
        Paint(regions, "torso_front");

        fill.PickColor1Command.Execute(null);            // cancel
        fill.PickColor1Command.Execute(null);            // same color, uppercase
        Assert.Equal(1, session.UndoCount);
        Assert.Equal("#17d4fa", fill.Color1);

        fill.PickColor1Command.Execute(null);            // a new color: one step, lowercase
        Assert.Equal("#abcdef", fill.Color1);
        Assert.Equal("#abcdef", DocumentOps.RegionFill(session.Document, "torso_front")!.C1);
        Assert.Equal(2, session.UndoCount);

        fill.PickColor2Command.Execute(null);
        Assert.Equal("#00ff00", fill.Color2);
        Assert.Equal(new[] { "#17d4fa", "#17d4fa", "#17d4fa", "#f22f89" }, picker.Seeds);
    }

    [Fact] // Review Focus and open question 5: undo restores regions, never the controls
    public void Undo_of_a_live_edit_restores_regions_and_leaves_the_controls()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        fill.Angle = 90;

        session.Undo();

        Assert.Equal(45, DocumentOps.RegionFill(session.Document, "torso_front")!.Angle);
        Assert.Equal(90, fill.Angle);                    // fillstyle-ui-not-synced-from-region, keep
        Assert.Equal(90, fill.Current.Angle);

        fill.Angle = 100;                                // the restored region no longer has the current key
        Assert.Equal(45, DocumentOps.RegionFill(session.Document, "torso_front")!.Angle);
        Assert.Equal((1, 1), (session.UndoCount, session.RedoCount)); // no step, so redo survives
    }

    [Fact]
    public void Unknown_types_and_malformed_colors_are_refused()
    {
        var (_, fill, _) = Make();
        Assert.Throws<ArgumentException>(() => fill.SetTypeCommand.Execute("gradient"));
        Assert.Throws<ArgumentException>(() => fill.Color1 = "17d4fa");
        Assert.Throws<ArgumentException>(() => fill.Color2 = "#17d4fz");
        Assert.Equal(FillStyleViewModel.DefaultFill, fill.Current);
    }

    [Fact] // the controls bind to these names
    public void Edits_raise_the_changed_control_and_Current()
    {
        var (_, fill, _) = Make();
        var raised = Track(fill);
        fill.SetTypeCommand.Execute("dots");
        fill.Angle = 45;                                  // unchanged: nothing raised
        Assert.Equal(new[] { nameof(FillStyleViewModel.Current), nameof(FillStyleViewModel.Type) }, raised);
    }

    [Theory] // ChooseColorW speaks COLORREF 0x00BBGGRR; the picker answers lowercase #rrggbb
    [InlineData("#17d4fa", 0x00FAD417u)]
    [InlineData("#f22f89", 0x00892FF2u)]
    [InlineData("#000000", 0x00000000u)]
    [InlineData("#ffffff", 0x00FFFFFFu)]
    public void Hex_and_COLORREF_round_trip(string hex, uint colorRef)
    {
        Assert.Equal(colorRef, Win32ColorPicker.ToColorRef(hex));
        Assert.Equal(hex, Win32ColorPicker.FromColorRef(colorRef));
        Assert.Equal(hex, Win32ColorPicker.FromColorRef(colorRef | 0xFF000000u));
    }

    [Fact] // a press whose release never arrived leaves a stale gesture; the next press closes it first
    public void A_stale_slider_gesture_is_closed_when_the_next_press_begins() => StaThread.Run(() =>
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        bool down = true;
        var group = new Views.FillStyleGroup(() => down) { DataContext = fill };

        group.Begin(freshPress: true);
        fill.Angle = 90;
        Assert.Equal(1, session.UndoCount);              // the paint; the drag is still open
        down = false;                                    // released without any closing event

        group.Begin(freshPress: false);                  // DragStarted-style begin with the button up: stale, closed
        Assert.Equal(2, session.UndoCount);
        fill.Angle = 120;
        Assert.Equal(2, session.UndoCount);              // the new gesture is open
        down = true;
        group.Begin(freshPress: true);                   // a fresh press closes whatever is open
        Assert.Equal(3, session.UndoCount);
        group.Begin(freshPress: false);                  // same press, button down: left alone
        fill.Angle = 150;
        Assert.Equal(3, session.UndoCount);
    });
}
