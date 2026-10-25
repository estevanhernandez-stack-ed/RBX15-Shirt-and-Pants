using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// Gradient color stops in the Fill Style group (ledger fill-gradient-stops, new). Stops are part of the
/// session's current fill, so these also pin that they follow the existing state rules: one undo step per edit that
/// changes a region, a pick as one gesture, and undo never moving the controls.
/// </summary>
public class FillStopsTests
{
    static (EditorSession Session, FillStyleViewModel Fill, RegionsGroupViewModel Regions) Make(FakeColorPicker? picker = null)
    {
        var session = new EditorSession();
        var fill = new FillStyleViewModel(session, picker ?? new FakeColorPicker());
        return (session, fill, new RegionsGroupViewModel(session, fill));
    }

    static string[] StopColors(FillStyleViewModel fill) => fill.Current.Stops.ToArray();

    // ------------------------------------------------------------ view-model

    [Fact] // "+" adds a stop halfway between the last stop (or Color 1) and Color 2
    public void Add_puts_a_halfway_color_before_color_2()
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("linear");
        fill.Color1 = "#000000";
        fill.Color2 = "#fefefe";

        fill.AddStopCommand.Execute(null);
        Assert.Equal(new[] { "#7f7f7f" }, StopColors(fill));            // (0 + 254) / 2 = 127

        fill.AddStopCommand.Execute(null);
        Assert.Equal(new[] { "#7f7f7f", "#bfbfbf" }, StopColors(fill)); // halfway from the last stop to Color 2

        fill.AddStopCommand.Execute(null);
        Assert.Equal(new[] { "#7f7f7f", "#bfbfbf", "#dfdfdf" }, StopColors(fill));
        Assert.Equal(new[] { "Color stop 1", "Color stop 2", "Color stop 3" }, fill.StopSwatches.Select(s => s.Name));
    }

    [Fact] // three stops is five colors; the command then cannot run, and a forced call adds nothing
    public void Add_is_disabled_at_three_stops_and_comes_back_on_removal()
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("radial");
        var changes = 0;
        fill.AddStopCommand.CanExecuteChanged += (_, _) => changes++;

        for (int i = 0; i < 3; i++)
        {
            Assert.True(fill.AddStopCommand.CanExecute(null));
            fill.AddStopCommand.Execute(null);
        }
        Assert.False(fill.AddStopCommand.CanExecute(null));
        Assert.True(changes > 0);
        fill.AddStopCommand.Execute(null);
        Assert.Equal(3, fill.Current.Stops.Length);
        Assert.Equal(3, fill.StopSwatches.Count);

        fill.StopSwatches[1].RemoveCommand.Execute(null);
        Assert.True(fill.AddStopCommand.CanExecute(null));
        Assert.Equal(2, fill.StopSwatches.Count);
    }

    [Fact] // right-click removes that stop; the others keep their order and the swatches keep their places
    public void Remove_drops_the_chosen_stop()
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("linear");
        for (int i = 0; i < 3; i++) fill.AddStopCommand.Execute(null);
        var first = fill.StopSwatches[0];
        var before = StopColors(fill);

        fill.StopSwatches[1].RemoveCommand.Execute(null);

        Assert.Equal(new[] { before[0], before[2] }, StopColors(fill));
        Assert.Equal(new[] { before[0], before[2] }, fill.StopSwatches.Select(s => s.Color));
        Assert.Same(first, fill.StopSwatches[0]);
        fill.StopSwatches[0].RemoveCommand.Execute(null);
        fill.StopSwatches[0].RemoveCommand.Execute(null);
        Assert.Empty(fill.Current.Stops);
        Assert.Empty(fill.StopSwatches);
        Assert.Equal(new Fill("linear", "#17d4fa", "#f22f89", 45, 16), fill.Current);   // back to the 4.1 value
    }

    [Fact] // a swatch left in the UI after its stop went does nothing
    public void A_stale_swatch_does_nothing()
    {
        var picker = new FakeColorPicker().Answer("#123456");
        var (_, fill, _) = Make(picker);
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        fill.AddStopCommand.Execute(null);
        var stale = fill.StopSwatches[1];
        fill.StopSwatches[0].RemoveCommand.Execute(null);
        fill.StopSwatches[0].RemoveCommand.Execute(null);

        stale.RemoveCommand.Execute(null);
        stale.PickCommand.Execute(null);

        Assert.Empty(fill.Current.Stops);
        Assert.Empty(picker.Seeds);
    }

    [Fact] // a stop pick is the Color 1 / Color 2 gesture: live preview, one step, seeded with the stop's color
    public void Picking_a_stop_previews_live_and_commits_as_one_step()
    {
        var picker = new FakeColorPicker().Answer("#00ff00", "#ff0000", "#ffff00", "#00ff00");
        var (session, fill, regions) = Make(picker);
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        Tile(regions, "torso_front").PaintCommand.Execute(null);
        Tile(regions, "torso_back").PaintCommand.Execute(null);
        var seed = fill.Current.Stops[0];
        var mid = default((string, string, int, bool));
        picker.DuringPick = () => mid = (fill.StopSwatches[0].Color, DocumentOps.RegionFill(session.Document, "torso_back")!.Stops[0], session.UndoCount, session.InGesture);

        fill.StopSwatches[0].PickCommand.Execute(null);

        Assert.Equal(new[] { seed }, picker.Seeds);
        Assert.Equal(("#00ff00", "#00ff00", 2, true), mid);            // live on the swatch and the canvas, nothing recorded yet
        Assert.Equal(new[] { "#00ff00" }, StopColors(fill));
        Assert.Equal(3, session.UndoCount);
        Assert.False(session.InGesture);
        session.Undo();
        Assert.Equal(new[] { seed }, DocumentOps.RegionFill(session.Document, "torso_front")!.Stops.ToArray());
        Assert.Equal(new[] { seed }, DocumentOps.RegionFill(session.Document, "torso_back")!.Stops.ToArray());
    }

    [Fact] // a cancel puts the seed back, on the swatch and the regions, and records nothing
    public void Cancelling_a_stop_pick_reverts_the_preview()
    {
        var picker = new FakeColorPicker().Answer(null, "#123456", "#654321");
        var (session, fill, regions) = Make(picker);
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        Tile(regions, "torso_front").PaintCommand.Execute(null);
        var painted = fill.Current;
        int steps = session.UndoCount;

        fill.StopSwatches[0].PickCommand.Execute(null);

        Assert.Equal(painted, fill.Current);
        Assert.Equal(painted.Stops[0], fill.StopSwatches[0].Color);
        Assert.Equal(painted, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal((steps, 0), (session.UndoCount, session.RedoCount));
    }

    [Fact] // the stop picker accepts any case and stores lowercase, like the colors
    public void A_stop_pick_stores_lowercase()
    {
        var picker = new FakeColorPicker().Answer("#ABCDEF");
        var (_, fill, _) = Make(picker);
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        fill.StopSwatches[0].PickCommand.Execute(null);
        Assert.Equal(new[] { "#abcdef" }, StopColors(fill));
    }

    [Fact] // other types hide the stops but keep them; Gradient and Radial show them again
    public void Stops_show_for_gradient_and_radial_only_and_survive_the_other_types()
    {
        var (_, fill, _) = Make();
        Assert.False(fill.ShowStops);                                  // Solid, the default
        fill.SetTypeCommand.Execute("linear");
        Assert.True(fill.ShowStops);
        fill.AddStopCommand.Execute(null);
        fill.AddStopCommand.Execute(null);
        var stops = StopColors(fill);
        var raised = Track(fill);

        foreach (var type in new[] { "solid", "stripes", "checker", "dots", "camo" })
        {
            fill.SetTypeCommand.Execute(type);
            Assert.False(fill.ShowStops, type);
            Assert.Equal(stops, StopColors(fill));                     // kept
            Assert.Equal(2, fill.StopSwatches.Count);
        }
        Assert.Contains(nameof(FillStyleViewModel.ShowStops), raised);

        fill.SetTypeCommand.Execute("radial");
        Assert.True(fill.ShowStops);
        Assert.Equal(stops, StopColors(fill));
        fill.SetTypeCommand.Execute("linear");
        Assert.Equal(stops, fill.StopSwatches.Select(s => s.Color));
    }

    [Fact] // painting with a three-stop gradient puts that Fill, stops included, in the document
    public void Painting_a_region_stores_the_fill_with_its_stops()
    {
        var (session, fill, regions) = Make();
        fill.SetTypeCommand.Execute("linear");
        fill.Color1 = "#ff0000";
        fill.Color2 = "#0000ff";
        fill.Angle = 0;
        for (int i = 0; i < 3; i++) fill.AddStopCommand.Execute(null);

        Tile(regions, "torso_front").PaintCommand.Execute(null);

        var stored = DocumentOps.RegionFill(session.Document, "torso_front")!;
        Assert.Equal(fill.Current, stored);
        Assert.Equal(3, stored.Stops.Length);
        Assert.Equal("linear", stored.Type);
        Assert.Equal(stored, Tile(regions, "torso_front").Fill);
    }

    [Fact] // applyFillEdit's rule holds for stops: an add and a remove retarget painted regions, one step each; undo leaves the controls
    public void Stop_edits_retarget_painted_regions_one_step_each_and_undo_leaves_the_controls()
    {
        var (session, fill, regions) = Make();
        fill.SetTypeCommand.Execute("linear");
        Tile(regions, "torso_front").PaintCommand.Execute(null);
        Tile(regions, "torso_back").PaintCommand.Execute(null);
        var plain = fill.Current;
        Assert.Equal(2, session.UndoCount);

        fill.AddStopCommand.Execute(null);
        var withStop = fill.Current;
        Assert.Equal(3, session.UndoCount);
        Assert.Equal(withStop, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal(withStop, DocumentOps.RegionFill(session.Document, "torso_back"));

        fill.StopSwatches[0].RemoveCommand.Execute(null);
        Assert.Equal(4, session.UndoCount);
        Assert.Equal(plain, DocumentOps.RegionFill(session.Document, "torso_front"));

        session.Undo();                                                 // the remove: regions get the stop back
        Assert.Equal(withStop, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal(plain, fill.Current);                              // fillstyle-ui-not-synced-from-region, keep
        Assert.Empty(fill.StopSwatches);
    }

    [Fact] // an add with nothing painted changes Current only: no document change, no undo step
    public void Adding_a_stop_with_no_matching_region_records_nothing()
    {
        var (session, fill, _) = Make();
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
    }

    [Fact] // the region button's swatch is painted through Fills, so it shows the stops
    public void A_region_tile_swatch_shows_the_middle_color() => StaThread.Run(() =>
    {
        var picker = new FakeColorPicker().Answer("#00ff00");
        var (_, fill, regions) = Make(picker);
        fill.SetTypeCommand.Execute("linear");
        fill.Color1 = "#ff0000";
        fill.Color2 = "#0000ff";
        fill.Angle = 0;
        fill.AddStopCommand.Execute(null);
        fill.StopSwatches[0].PickCommand.Execute(null);
        var tile = Tile(regions, "torso_front");
        tile.PaintCommand.Execute(null);

        var bmp = (BitmapSource)tile.Swatch!;
        var px = new byte[4];
        bmp.CopyPixels(new Int32Rect(bmp.PixelWidth / 2, bmp.PixelHeight / 2, 1, 1), px, 4, 0);
        Assert.True(px[1] > 230 && px[2] < 40 && px[0] < 40, $"mid pixel B{px[0]} G{px[1]} R{px[2]}");
    });

    // ------------------------------------------------------------ view

    static FillStyleGroup Load(FillStyleViewModel fill)
    {
        var group = new FillStyleGroup(() => false) { DataContext = fill };
        StaThread.Pump();
        group.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        group.Arrange(new Rect(group.DesiredSize));
        group.UpdateLayout();
        return group;
    }

    static Size Remeasure(FillStyleGroup group)
    {
        group.InvalidateMeasure();
        group.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        group.Arrange(new Rect(group.DesiredSize));
        group.UpdateLayout();
        return group.DesiredSize;
    }

    static IEnumerable<Button> Buttons(FillStyleGroup group) => Descendants<Button>(group);

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>Visible in the effective sense: itself and every ancestor Visible (IsVisible needs a window; these groups have none).</summary>
    static bool Shown(UIElement e)
    {
        for (DependencyObject? d = e; d is not null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    static Button Named(FillStyleGroup group, string name) =>
        Buttons(group).Single(b => AutomationProperties.GetName(b) == name);

    [Fact] // the "+" button: named, with its tooltip, between Color 2 and Ang; it adds and goes disabled at three
    public void The_plus_button_adds_a_stop_and_disables_at_three() => StaThread.Run(() =>
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("linear");
        var group = Load(fill);
        var add = Named(group, "Add color stop");

        Assert.Equal("Add a color stop", add.ToolTip);
        Assert.True(Shown(add) && add.IsEnabled);
        var color2 = Named(group, "Color 2");
        Assert.True(add.TranslatePoint(new Point(), group).X > color2.TranslatePoint(new Point(), group).X);   // after Color 2

        for (int i = 1; i <= 3; i++)
        {
            add.Command.Execute(add.CommandParameter);
            group.UpdateLayout();
            Assert.Equal(i, Buttons(group).Count(b => (AutomationProperties.GetName(b) ?? "").StartsWith("Color stop")));
        }
        Assert.False(add.IsEnabled);
    });

    [Fact] // stops sit between Color 1 and Color 2, left to right
    public void Stop_swatches_sit_between_the_two_colors() => StaThread.Run(() =>
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("radial");
        fill.AddStopCommand.Execute(null);
        fill.AddStopCommand.Execute(null);
        var group = Load(fill);

        double X(string name) => Named(group, name).TranslatePoint(new Point(), group).X;
        Assert.True(X("Color 1") < X("Color stop 1") && X("Color stop 1") < X("Color stop 2") && X("Color stop 2") < X("Color 2"));
        var stop = Named(group, "Color stop 1");
        Assert.Equal(fill.StopSwatches[0].Color, ((System.Windows.Media.SolidColorBrush)stop.Background).Color.ToString().Replace("#FF", "#").ToLowerInvariant());
        Assert.Equal("Color stop 1. Right-click removes it.", stop.ToolTip);
    });

    [Fact] // clicking a stop opens the picker under that swatch; a right-click removes it
    public void Clicking_a_stop_picks_and_right_clicking_removes() => StaThread.Run(() =>
    {
        var picker = new FakeColorPicker().Answer("#336699");
        var (_, fill, _) = Make(picker);
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        fill.AddStopCommand.Execute(null);
        var group = Load(fill);
        var first = Named(group, "Color stop 1");

        first.Command.Execute(first.CommandParameter);
        Assert.Same(first, Assert.Single(picker.Anchors));
        Assert.Equal("#336699", fill.Current.Stops[0]);

        var gestures = first.InputBindings.Cast<InputBinding>().Select(b => b.Gesture switch
        {
            MouseGesture m => m.MouseAction.ToString(),
            KeyGesture k => (k.Modifiers == ModifierKeys.None ? "" : k.Modifiers + "+") + k.Key,
            _ => "?",
        }).ToArray();
        Assert.Equal(new[] { "RightClick", "Delete", "Shift+F10", "Apps" }, gestures);

        var second = fill.Current.Stops[1];
        first.InputBindings.OfType<MouseBinding>().Single().Command.Execute(null);
        group.UpdateLayout();
        Assert.Equal(new[] { second }, StopColors(fill));
        Assert.Single(Buttons(group), b => (AutomationProperties.GetName(b) ?? "").StartsWith("Color stop"));
    });

    [Fact] // Solid, Stripes, Checker, Dots and Camo hide the stops and the "+"; Gradient and Radial bring them back
    public void Stops_and_plus_are_hidden_for_the_other_styles_and_restored() => StaThread.Run(() =>
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("linear");
        fill.AddStopCommand.Execute(null);
        fill.AddStopCommand.Execute(null);
        var group = Load(fill);
        var add = Named(group, "Add color stop");

        int StopsShown() => Buttons(group).Count(b => (AutomationProperties.GetName(b) ?? "").StartsWith("Color stop") && Shown(b));

        Assert.Equal((2, true), (StopsShown(), Shown(add)));
        foreach (var type in new[] { "solid", "stripes", "checker", "dots", "camo" })
        {
            fill.SetTypeCommand.Execute(type);
            Remeasure(group);
            Assert.Equal((0, false), (StopsShown(), Shown(add)));
            Assert.True(Shown(Named(group, "Color 1")) && Shown(Named(group, "Color 2")));
        }
        fill.SetTypeCommand.Execute("radial");
        Remeasure(group);
        Assert.Equal((2, true), (StopsShown(), Shown(add)));
    });

    [Fact] // the group's height is the same with no stops, three stops, and a style that hides them; its width may grow
    public void The_group_does_not_grow_taller_for_stops() => StaThread.Run(() =>
    {
        var (_, fill, _) = Make();
        fill.SetTypeCommand.Execute("linear");
        var group = Load(fill);
        var bare = group.DesiredSize;

        for (int i = 0; i < 3; i++) fill.AddStopCommand.Execute(null);
        var full = Remeasure(group);
        fill.SetTypeCommand.Execute("solid");
        var hidden = Remeasure(group);

        Assert.Equal(bare.Height, full.Height, 3);
        Assert.Equal(bare.Height, hidden.Height, 3);
        Assert.True(full.Width > bare.Width, $"{full.Width} vs {bare.Width}");
        Assert.True(hidden.Width < full.Width);
    });

    [Fact] // the ribbon itself: the Background tab's tallest group (Fill Style is there since 2026-10-25) is unchanged by stops
    public void The_background_ribbon_height_is_unchanged_by_stops() => StaThread.Run(() =>
    {
        var layout = Layout(new EditorSession());
        var view = new LayoutRibbon { DataContext = layout };
        StaThread.Pump();
        layout.FillStyle.SetTypeCommand.Execute("linear");
        view.Measure(new Size(4000, 400));
        double before = view.DesiredSize.Height;
        for (int i = 0; i < 3; i++) layout.FillStyle.AddStopCommand.Execute(null);
        view.InvalidateMeasure();
        view.Measure(new Size(4000, 400));
        Assert.Equal(before, view.DesiredSize.Height, 3);
    });
}
