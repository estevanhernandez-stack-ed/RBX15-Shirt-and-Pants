using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Home tab XAML, loaded headless on StaThread's one shared STA thread, whose Application holds
/// Task 1's theme, so the shared styles (RibbonGroup, RibbonGroupLabel, UpperCase) and the theme keys
/// resolve as in the app. Pins what the view-model tests cannot: the bindings, the 14
/// keyboard-reachable tiles and their clear gestures, the persistent front tint, and that hidden
/// groups are absent.
/// </summary>
public class HomeRibbonViewTests
{
    static HomeRibbon Load(HomeViewModel home)
    {
        var view = new HomeRibbon { DataContext = home };
        StaThread.Pump();
        view.Measure(new Size(2000, 200));
        view.Arrange(new Rect(0, 0, 2000, 200));
        view.UpdateLayout();
        return view;
    }

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    static Button[] Tiles(HomeRibbon view) =>
        Descendants<Button>(view).Where(b => b.DataContext is RegionTileViewModel).ToArray();

    [Fact] // regions-grid-layout-shirt parityCheck: exactly 14 paintable tiles, each a focusable button with an automation name
    public void The_grid_shows_fourteen_named_focusable_tiles() => StaThread.Run(() =>
    {
        var view = Load(Home(new EditorSession()));
        var tiles = Tiles(view);

        Assert.Equal(14, tiles.Length);
        Assert.All(tiles, t => Assert.True(t.Focusable && t.IsTabStop));
        Assert.Equal(14, tiles.Select(AutomationProperties.GetName).Distinct().Count());
        Assert.Contains("Torso Front", tiles.Select(AutomationProperties.GetName));
    });

    [Fact] // the clear gestures reach ClearCommand: right-click (editor.js:1677) and Delete, Shift+F10, Menu
    public void A_tile_clears_on_right_click_and_from_the_keyboard() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var home = Home(session);
        var view = Load(home);
        var front = Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).Region == "torso_front");

        var gestures = front.InputBindings.Cast<InputBinding>().Select(b => b.Gesture switch
        {
            MouseGesture m => m.MouseAction.ToString(),
            KeyGesture k => (k.Modifiers == ModifierKeys.None ? "" : k.Modifiers + "+") + k.Key,
            _ => "?",
        }).ToArray();
        Assert.Equal(new[] { "RightClick", "Delete", "Shift+F10", "Apps" }, gestures);

        front.Command.Execute(null);
        Assert.NotNull(Tile(home.Regions, "torso_front").Fill);
        front.InputBindings.OfType<MouseBinding>().Single().Command.Execute(null);
        Assert.Null(Tile(home.Regions, "torso_front").Fill);
    });

    [Fact] // region-front-highlight-lost fix: after paint then clear, Front rests on #2a1040 again
    public void The_front_tile_keeps_its_tint_after_paint_and_clear() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var home = Home(session);
        var view = Load(home);
        Border Bd() => (Border)Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).IsFront).Template
            .FindName("Bd", Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).IsFront));
        var tint = (Color)ColorConverter.ConvertFromString("#2A1040");

        Assert.Equal(tint, ((SolidColorBrush)Bd().Background).Color);
        Tile(home.Regions, "torso_front").PaintCommand.Execute(null);
        view.UpdateLayout();
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)Bd().Background).Color);
        Tile(home.Regions, "torso_front").ClearCommand.Execute(null);
        view.UpdateLayout();
        Assert.Equal(tint, ((SolidColorBrush)Bd().Background).Color);
    });

    [Fact] // the type list's selection is FillStyle.Type, both ways
    public void The_fill_type_list_tracks_the_current_type() => StaThread.Run(() =>
    {
        var home = Home(new EditorSession());
        var view = Load(home);
        var list = Descendants<ListBox>(view).Single(l => AutomationProperties.GetName(l) == "Fill type");

        Assert.Equal("solid", list.SelectedValue);
        list.SelectedIndex = 6;
        Assert.Equal("camo", home.FillStyle.Type);
        home.FillStyle.SetTypeCommand.Execute("dots");
        Assert.Equal(5, list.SelectedIndex);
    });

    [Fact] // the mode button, settings and edit bindings; contract: no Tools, Brush or Placement group in M3a
    public void Mode_settings_and_edit_bind_and_hidden_groups_are_absent() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var view = Load(Home(session));
        var buttons = Descendants<Button>(view).ToArray();
        var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToArray();

        var mode = buttons.Single(b => AutomationProperties.GetName(b) == "Shirt or Pants mode");
        Assert.Equal("SHIRT", mode.Content);
        mode.Command.Execute(null);
        Assert.Equal("PANTS", mode.Content);

        var undo = buttons.Single(b => (string)b.Content == "Undo");
        Assert.True(undo.IsEnabled);                       // the toggle was one step
        Assert.False(buttons.Single(b => (string)b.Content == "Redo").IsEnabled);

        Descendants<CheckBox>(view).Single(c => (string)c.Content == "Grid").IsChecked = true;
        Assert.True(session.Document.ShowGrid);
        Descendants<TextBox>(view).Single().Text = "Hoodie";
        Assert.Equal("Hoodie", session.TemplateTitle);

        Assert.Equal(new[] { "MODE", "EDIT", "SETTINGS", "FILL STYLE", "TEMPLATE REGIONS" },
            texts.Where(t => t is "MODE" or "TOOLS" or "BRUSH" or "PLACEMENT" or "EDIT" or "SETTINGS" or "FILL STYLE" or "TEMPLATE REGIONS"));
        Assert.DoesNotContain(buttons, b => b.Content is "Draw" or "Fit Front" or "Save work" or "Open");
        Assert.Contains("Set a style, then click a region to paint it. Right-click clears.", texts);
    });

    [Fact] // controller ruling 3: a thumb that loses mouse capture with no DragCompleted still ends the gesture
    public void A_slider_gesture_ends_on_lost_mouse_capture_as_well_as_drag_completed() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var home = Home(session);
        var view = Load(home);
        Tile(home.Regions, "torso_front").PaintCommand.Execute(null);
        var angle = Descendants<Slider>(view).Single(s => AutomationProperties.GetName(s) == "Angle");

        void Start() => angle.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        void Complete() => angle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        void LoseCapture() => angle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });

        // capture lost, no DragCompleted: the drag is one step, and the next edit is its own step
        Start();
        home.FillStyle.Angle = 60;
        home.FillStyle.Angle = 70;
        Assert.Equal(1, session.UndoCount);
        LoseCapture();
        Assert.Equal(2, session.UndoCount);
        home.FillStyle.Angle = 80;
        Assert.Equal(3, session.UndoCount);

        // the normal path still works, and the capture loss that follows it is a no-op
        Start();
        home.FillStyle.Angle = 90;
        Complete();
        LoseCapture();
        Assert.Equal(4, session.UndoCount);
    });
}
