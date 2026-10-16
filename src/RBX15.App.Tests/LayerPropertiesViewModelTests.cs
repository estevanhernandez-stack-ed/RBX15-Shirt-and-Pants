using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The "Selected layer" panel's view-model (editor.js:746-912). Ledger rows: layers-selected-panel,
/// layers-empty-state, props-name-xy, props-scale, props-rotation, props-opacity, props-flip, props-adjust-panel,
/// props-adjust-sliders, props-adjust-reset, props-live-refresh, props-falsy-zero-adjust-display,
/// props-rotation-label-unrounded, props-panel-rebuild-resets, props-scale-clamp-inconsistency, layers-move-updown.
/// </summary>
public class LayerPropertiesViewModelTests
{
    static (EditorSession Session, LayerPropertiesViewModel Props) Make(params Layer[] layers)
    {
        var session = SessionWith(layers.Length == 0 ? [LayerOf(1)] : layers);
        session.Select(session.Document.Layers[^1].Id);
        return (session, new LayerPropertiesViewModel(session));
    }

    static Layer Shown(EditorSession s) => s.SelectedLayer!;

    [Fact] // layers-empty-state: editor.js:749-751
    public void With_nothing_selected_the_panel_is_empty_and_inert()
    {
        var session = SessionWith(LayerOf(1));
        var props = new LayerPropertiesViewModel(session);

        Assert.False(props.HasSelection);
        Assert.Equal("Select a layer to edit.", props.EmptyText);
        Assert.Equal(("", "", "", "", "", ""), (props.Name, props.XText, props.YText, props.PositionLabel, props.ScaleLabel, props.RotationLabel));
        Assert.False(props.FlipHCommand.CanExecute(null));
        Assert.False(props.ResetAdjustmentsCommand.CanExecute(null));
        Assert.False(props.MoveUpCommand.CanExecute(null));

        props.Name = "x";
        props.XText = "40";
        props.ScalePercent = 50;
        props.Brightness = 0;
        Assert.Equal(0, session.UndoCount);
        Assert.Equal(LayerOf(1).Name, session.Document.Layers[0].Name);
    }

    [Fact] // editor.js:756-765, :811-823: toFixed(0) for X, Y, scale and opacity; the stored values keep their fractions
    public void Shows_the_selected_layer()
    {
        var (_, props) = Make(LayerOf(1) with { Name = "Logo", X = 12.7, Y = -3.2, Scale = 0.575, Rotation = 37.28194618, Opacity = 0.5 });

        Assert.True(props.HasSelection);
        Assert.Equal("Logo", props.Name);
        Assert.Equal(("13", "-3"), (props.XText, props.YText));
        Assert.Equal("X: 13 \u00A0 Y: -3", props.PositionLabel);
        Assert.Equal(0.575 * 100, props.ScalePercent);
        Assert.Equal("57%", props.ScaleLabel);                     // 0.575 * 100 = 57.49999999999999 -> "57", as node's toFixed(0)
        Assert.Equal(37.28194618, props.Rotation);
        Assert.Equal("37\u00B0", props.RotationLabel);
        Assert.Equal((50.0, "50%"), (props.OpacityPercent, props.OpacityLabel));
        Assert.Equal(("100%", "100%", "100%", "0\u00B0", "0", "0", "0"),
            (props.BrightnessLabel, props.ContrastLabel, props.SaturateLabel, props.HueLabel, props.RLabel, props.GLabel, props.BLabel));
        Assert.False(props.AdjustmentsExpanded);                  // display:none at build (editor.js:784)
    }

    [Theory] // ECMA-262 Number.prototype.toFixed(0): nearest, ties away from zero on the exact double, "-" for any negative
    [InlineData(12.5, "13")]
    [InlineData(-12.5, "-13")]
    [InlineData(2.5, "3")]
    [InlineData(0.49999999999999994, "0")]
    [InlineData(85.49999999999999, "85")]
    [InlineData(-0.4, "-0")]
    [InlineData(-0.0, "0")]
    [InlineData(100000000000000000000.0, "100000000000000000000")]
    public void Position_and_percent_text_is_JS_toFixed_0(double value, string text) =>
        Assert.Equal(text, LayerPropertiesViewModel.ToFixed0(value));

    [Theory] // props-rotation-label-unrounded (fix): whole degrees, half away from zero, never "-0"
    [InlineData(37.28194618, "37")]
    [InlineData(37.5, "38")]
    [InlineData(-37.5, "-38")]
    [InlineData(-0.4, "0")]
    [InlineData(179.6, "180")]
    [InlineData(-179.5, "-180")]
    [InlineData(0.0, "0")]
    public void The_rotation_label_is_whole_degrees(double rotation, string text)
    {
        var (_, props) = Make(LayerOf(1) with { Rotation = rotation });
        Assert.Equal(text + "\u00B0", props.RotationLabel);
        Assert.Equal(text, LayerPropertiesViewModel.WholeDegrees(rotation));
    }

    [Fact] // props-falsy-zero-adjust-display (fix): 4.1's `|| 100` showed a stored 0 as 100 after any rebuild
    public void Zero_adjustments_show_as_zero_after_reselect()
    {
        var (session, props) = Make(LayerOf(1), LayerOf(2));
        props.Brightness = 0;
        props.Contrast = 0;
        props.Saturate = 0;
        session.Select(1);
        session.Select(2);

        Assert.Equal((0.0, 0.0, 0.0), (props.Brightness, props.Contrast, props.Saturate));
        Assert.Equal(("0%", "0%", "0%"), (props.BrightnessLabel, props.ContrastLabel, props.SaturateLabel));
        Assert.Equal((0.0, 0.0, 0.0), (Shown(session).Brightness, Shown(session).Contrast, Shown(session).Saturate));
    }

    [Fact] // props-scale (fix): one step; the slider anchors at the top-left (editor.js:828-832); the same value records nothing
    public void A_scale_edit_is_one_step_anchored_at_the_top_left()
    {
        var (session, props) = Make(LayerOf(1) with { X = 10, Y = 20 });
        props.ScalePercent = 50;
        Assert.Equal((0.5, 10.0, 20.0), (Shown(session).Scale, Shown(session).X, Shown(session).Y));
        Assert.Equal(1, session.UndoCount);
        Assert.Equal("50%", props.ScaleLabel);

        props.ScalePercent = 50;
        Assert.Equal(1, session.UndoCount);
    }

    [Theory] // props-scale-clamp-inconsistency (fix): one clamp, 1..500 %; whole numbers, as a step-1 range
    [InlineData(0, 0.01)]
    [InlineData(-20, 0.01)]
    [InlineData(1000, 5)]
    [InlineData(49.6, 0.5)]
    [InlineData(500, 5)]
    public void The_scale_slider_is_whole_percent_in_1_to_500(double percent, double scale)
    {
        var (session, props) = Make(LayerOf(1) with { Scale = 2 });
        props.ScalePercent = percent;
        Assert.Equal(scale, Shown(session).Scale);
        Assert.Equal((1.0, 500.0), (LayerPropertiesViewModel.MinScalePercent, LayerPropertiesViewModel.MaxScalePercent));
        Assert.Equal((SelectionGeometry.MinScale * 100, SelectionGeometry.MaxScale * 100),
            (LayerPropertiesViewModel.MinScalePercent, LayerPropertiesViewModel.MaxScalePercent));
    }

    [Theory] // props-rotation (fix): -180..180 whole degrees (editor.js:764, :833-837), one step
    [InlineData(45.4, 45)]
    [InlineData(-44.5, -45)]
    [InlineData(200, 180)]
    [InlineData(-200, -180)]
    public void The_rotation_slider_is_whole_degrees_in_range(double value, double stored)
    {
        var (session, props) = Make();
        props.Rotation = value;
        Assert.Equal(stored, Shown(session).Rotation);
        Assert.Equal(1, session.UndoCount);
    }

    [Theory] // controller ruling: non-finite Scale and Rotation are refused like X/Y: no step, the shown value stands
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_scale_and_rotation_are_refused_and_record_nothing(double value)
    {
        var (session, props) = Make(LayerOf(1) with { Scale = 2, Rotation = 30 });
        var raised = Track(props);
        props.ScalePercent = value;
        props.Rotation = value;
        Assert.Equal((2.0, 30.0, 0), (Shown(session).Scale, Shown(session).Rotation, session.UndoCount));
        Assert.Contains(nameof(LayerPropertiesViewModel.ScalePercent), raised);
        Assert.Contains(nameof(LayerPropertiesViewModel.Rotation), raised);
    }

    [Fact] // props-opacity (fix): 0..100 %, one step
    public void Opacity_is_whole_percent_and_one_step()
    {
        var (session, props) = Make();
        props.OpacityPercent = 49.5;
        Assert.Equal(0.5, Shown(session).Opacity);
        props.OpacityPercent = 150;
        Assert.Equal(1, Shown(session).Opacity);
        Assert.Equal(2, session.UndoCount);
        Assert.Equal("100%", props.OpacityLabel);
    }

    [Fact] // props-adjust-sliders (fix): each is one step, clamped to its range, and leaves the other six alone
    public void Each_adjustment_is_one_clamped_step_that_keeps_the_others()
    {
        var (session, props) = Make(LayerOf(1) with { Contrast = 40, GShift = -7 });
        props.Brightness = 250;
        props.Saturate = -5;
        props.HueRotate = 90.4;
        props.RShift = 150;
        props.BShift = -150;

        var l = Shown(session);
        Assert.Equal((200.0, 40.0, 0.0, 90.0, 100, -7, -100), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate, l.RShift, l.GShift, l.BShift));
        Assert.Equal(5, session.UndoCount);
        Assert.Equal(("200%", "0%", "90\u00B0", "100", "-7", "-100"),
            (props.BrightnessLabel, props.SaturateLabel, props.HueLabel, props.RLabel, props.GLabel, props.BLabel));
    }

    [Fact] // final fix wave 8: touching one adjustment clamps only that one; an out-of-range Brightness stays as stored
    public void An_adjustment_edit_clamps_only_its_own_field()
    {
        var (session, props) = Make(LayerOf(1) with { Brightness = 300, Contrast = -20, HueRotate = 400, GShift = 150 });
        props.HueRotate = 90;
        var l = Shown(session);
        Assert.Equal((300.0, -20.0, 90.0, 150), (l.Brightness, l.Contrast, l.HueRotate, l.GShift));
        Assert.Equal(1, session.UndoCount);
        props.RShift = 120;
        Assert.Equal((300.0, -20.0, 100, 150), (Shown(session).Brightness, Shown(session).Contrast, Shown(session).RShift, Shown(session).GShift));
    }

    [Theory] // final fix wave 15: every number the panel takes refuses NaN and both infinities: no throw, no step, the view reverts
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_input_is_refused_everywhere(double value)
    {
        var (session, props) = Make(LayerOf(1) with { Opacity = 0.4, Brightness = 120, Contrast = 80, Saturate = 60, HueRotate = 30 });
        var before = session.Document;
        var raised = Track(props);
        props.ScalePercent = value;
        props.Rotation = value;
        props.OpacityPercent = value;
        props.Brightness = value;
        props.Contrast = value;
        props.Saturate = value;
        props.HueRotate = value;
        Assert.Same(before, session.Document);
        Assert.Equal(0, session.UndoCount);
        Assert.Equal(
            new[] { "ScalePercent", "Rotation", "OpacityPercent", "Brightness", "Contrast", "Saturate", "HueRotate" },
            raised);
    }

    [Fact] // props-adjust-reset (fix): one step to (100,100,100,0,0,0,0) (editor.js:908-912), the panel stays open; at defaults, nothing
    public void Reset_is_one_step_and_keeps_the_adjustments_open()
    {
        var (session, props) = Make(LayerOf(1) with { Brightness = 0, HueRotate = 180, BShift = 30 });
        props.AdjustmentsExpanded = true;

        props.ResetAdjustmentsCommand.Execute(null);

        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(Shown(session)));
        Assert.Equal(1, session.UndoCount);
        Assert.True(props.AdjustmentsExpanded);
        props.ResetAdjustmentsCommand.Execute(null);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // props-flip (fix): each flip is one step and undo reverts it (editor.js:843-844)
    public void Flips_are_one_step_each_and_undo()
    {
        var (session, props) = Make();
        props.FlipHCommand.Execute(null);
        props.FlipVCommand.Execute(null);
        Assert.Equal((true, true, 2), (Shown(session).FlipH, Shown(session).FlipV, session.UndoCount));
        session.Undo();
        Assert.Equal((true, false), (Shown(session).FlipH, Shown(session).FlipV));
    }

    [Fact] // props-name-xy (fix): a committed name is one step; the same name none; empty is allowed, as 4.1
    public void A_name_commit_is_one_step_and_may_be_empty()
    {
        var (session, props) = Make();
        props.Name = "Logo";
        props.Name = "Logo";
        Assert.Equal(("Logo", 1), (Shown(session).Name, session.UndoCount));
        props.Name = "";
        Assert.Equal(("", 2), (Shown(session).Name, session.UndoCount));
    }

    [Theory] // props-name-xy: +value (editor.js:826-827); "" is 0; the stored value keeps its fraction, shown toFixed(0)
    [InlineData("40", 40.0, "40")]
    [InlineData("12.7", 12.7, "13")]
    [InlineData(" -5 ", -5.0, "-5")]
    [InlineData("", 0.0, "0")]
    [InlineData("1e3", 1000.0, "1000")]
    [InlineData("-0", 0.0, "0")]
    public void A_typed_X_commits_one_step_and_keeps_Y(string typed, double x, string shown)
    {
        var (session, props) = Make(LayerOf(1) with { X = 3, Y = 9 });
        props.XText = typed;
        Assert.Equal((x, 9.0), (Shown(session).X, Shown(session).Y));
        Assert.Equal(shown, props.XText);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // a field committed unchanged (focus loss with nothing typed) records nothing and keeps the fraction
    public void Committing_the_shown_text_changes_nothing()
    {
        var (session, props) = Make(LayerOf(1) with { X = 12.7, Y = 4.2 });
        props.XText = "13";
        props.YText = "4";
        Assert.Equal((12.7, 4.2, 0), (Shown(session).X, Shown(session).Y, session.UndoCount));
    }

    [Theory] // Review Focus: text that is not a finite number below 1e21 is refused, the field re-reads, nothing is recorded
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    [InlineData("1e21")]
    [InlineData("12,5")]
    [InlineData("0x10")]
    public void Unusable_position_text_is_refused_and_reverts(string typed)
    {
        var (session, props) = Make(LayerOf(1) with { X = 3, Y = 9 });
        var raised = Track(props);
        props.YText = typed;
        Assert.Equal((3.0, 9.0, 0), (Shown(session).X, Shown(session).Y, session.UndoCount));
        Assert.Equal("9", props.YText);
        Assert.Contains(nameof(LayerPropertiesViewModel.YText), raised);
    }

    [Fact] // a far position below 1e21 is accepted, and the document still exports (the layer is simply off the canvas)
    public void A_far_position_is_accepted_and_still_exports()
    {
        var (session, props) = Make();
        props.XText = "9e20";
        Assert.Equal(9e20, Shown(session).X);
        using var image = Renderer.Export(session.Document);
        Assert.Equal((585, 559), (image.Width, image.Height));
    }

    [Fact] // props-live-refresh (fix): a canvas move drag (Task 2: one Apply per move inside a gesture) refreshes X/Y live
    public void A_canvas_move_drag_refreshes_X_and_Y_on_every_move_and_is_one_step()
    {
        var (session, props) = Make(LayerOf(1) with { X = 10, Y = 10 });
        var raised = Track(props);
        using (session.BeginGesture())
        {
            session.Apply(d => LayerOps.MoveTo(d, 1, 25, 10));
            Assert.Equal(("25", "10", "X: 25 \u00A0 Y: 10"), (props.XText, props.YText, props.PositionLabel));
            session.Apply(d => LayerOps.MoveTo(d, 1, 40, 31));
            Assert.Equal(("40", "31"), (props.XText, props.YText));
        }
        Assert.Equal(1, session.UndoCount);
        // only what changed is raised: no rebuild, so focus and the open adjustments survive (props-panel-rebuild-resets)
        Assert.Equal(new[] { "XText", "PositionLabel", "XText", "YText", "PositionLabel" }, raised);
    }

    [Fact] // props-live-refresh: during a rotate drag the slider tracks and the label stays rounded
    public void A_rotate_drag_moves_the_rotation_slider_and_rounds_the_label()
    {
        var (session, props) = Make();
        using (session.BeginGesture())
            session.Apply(d => LayerOps.SetRotation(d, 1, 37.28194618));
        Assert.Equal((37.28194618, "37\u00B0"), (props.Rotation, props.RotationLabel));
    }

    [Fact] // a flip shows nowhere in the panel, so it raises nothing; an opacity edit raises only its own two
    public void Only_changed_properties_are_raised()
    {
        var (_, props) = Make();
        var raised = Track(props);
        props.FlipHCommand.Execute(null);
        Assert.Empty(raised);
        props.OpacityPercent = 40;
        Assert.Equal(new[] { "OpacityPercent", "OpacityLabel" }, raised);
    }

    [Fact] // props-adjust-panel and props-panel-rebuild-resets (fix): the open state survives selection, deselect, edits and undo
    public void The_adjustments_stay_open_across_selection_edits_and_undo()
    {
        var (session, props) = Make(LayerOf(1), LayerOf(2));
        props.AdjustmentsExpanded = true;
        props.HueRotate = 45;
        session.Select(1);
        session.Select(null);
        Assert.False(props.HasSelection);
        session.Select(2);
        session.Undo();
        Assert.True(props.AdjustmentsExpanded);
        Assert.Equal((0.0, "0\u00B0"), (props.HueRotate, props.HueLabel));
    }

    [Fact] // a slider drag is one gesture: any number of moves is one step; a drag back to the start records nothing
    public void A_slider_drag_is_one_step_and_a_round_trip_records_nothing()
    {
        var (session, props) = Make();
        using (props.BeginSliderDrag())
        {
            for (int v = 99; v >= 60; v--) props.ScalePercent = v;
        }
        Assert.Equal((0.6, 1), (Shown(session).Scale, session.UndoCount));

        using (props.BeginSliderDrag())
        {
            props.OpacityPercent = 30;
            props.OpacityPercent = 100;
        }
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // the layer's removal returns the panel to its empty state
    public void Removing_the_shown_layer_empties_the_panel()
    {
        var (session, props) = Make();
        session.ApplyWithSelection(s => LayerOps.Remove(s, 1));
        Assert.False(props.HasSelection);
        Assert.Equal("", props.Name);
    }

    [Fact] // btnDupe (editor.js:845): one step, the copy is selected and shown
    public void Duplicate_is_one_step_and_shows_the_copy()
    {
        var (session, props) = Make(LayerOf(1) with { Name = "Logo", X = 5 });
        props.DuplicateCommand.Execute(null);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(("Logo copy", "15"), (props.Name, props.XText));
    }

    [Fact] // layers-move-updown (fix): one step each; disabled at the ends (editor.js:846-853)
    public void Up_and_Down_are_one_step_and_disabled_at_the_ends()
    {
        var (session, props) = Make(LayerOf(1), LayerOf(2));     // 2 is selected and on top
        Assert.False(props.MoveUpCommand.CanExecute(null));
        Assert.True(props.MoveDownCommand.CanExecute(null));
        props.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { 2, 1 }, session.Document.Layers.Select(l => l.Id));
        Assert.Equal(1, session.UndoCount);
        Assert.True(props.MoveUpCommand.CanExecute(null));
        Assert.False(props.MoveDownCommand.CanExecute(null));
    }

    [Fact] // interface R2: a hidden selected layer is still edited from the panel, as in 4.1
    public void A_hidden_selected_layer_is_still_edited()
    {
        var (session, props) = Make(LayerOf(1) with { Visible = false });
        props.OpacityPercent = 20;
        Assert.Equal(0.2, Shown(session).Opacity);
    }
}
