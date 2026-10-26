using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
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

    // Fill Style and Template Regions are on the Layout tab, shown as "Background" (Este, 2026-10-25, reversing F1).
    static LayoutRibbon Load(LayoutViewModel layout)
    {
        var view = new LayoutRibbon { DataContext = layout };
        StaThread.Pump();
        view.Measure(new Size(3000, 200));
        view.Arrange(new Rect(0, 0, 3000, 200));
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

    static Button[] Tiles(FrameworkElement view) =>
        Descendants<Button>(view).Where(b => b.DataContext is RegionTileViewModel).ToArray();

    [Fact] // regions-grid-layout-shirt parityCheck (4.1's 14 tiles) plus the limbs' Top and Bot (regions-limb-caps): 18, every region
    public void The_grid_shows_all_eighteen_named_focusable_tiles() => StaThread.Run(() =>
    {
        var view = Load(Layout(new EditorSession()));
        var tiles = Tiles(view);

        Assert.Equal(18, tiles.Length);
        Assert.All(tiles, t => Assert.True(t.Focusable && t.IsTabStop));
        Assert.Equal(18, tiles.Select(AutomationProperties.GetName).Distinct().Count());
        Assert.Contains("Torso Front", tiles.Select(AutomationProperties.GetName));
        Assert.Contains("Right Arm Top", tiles.Select(AutomationProperties.GetName));
        Assert.Contains("Left Arm Bot (Bottom)", tiles.Select(AutomationProperties.GetName));
    });

    [Fact] // the clear gestures reach ClearCommand: right-click (editor.js:1677) and Delete, Shift+F10, Menu
    public void A_tile_clears_on_right_click_and_from_the_keyboard() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        var view = Load(layout);
        var front = Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).Region == "torso_front");

        var gestures = front.InputBindings.Cast<InputBinding>().Select(b => b.Gesture switch
        {
            MouseGesture m => m.MouseAction.ToString(),
            KeyGesture k => (k.Modifiers == ModifierKeys.None ? "" : k.Modifiers + "+") + k.Key,
            _ => "?",
        }).ToArray();
        Assert.Equal(new[] { "RightClick", "Delete", "Shift+F10", "Apps" }, gestures);

        front.Command.Execute(null);
        Assert.NotNull(Tile(layout.Regions, "torso_front").Fill);
        front.InputBindings.OfType<MouseBinding>().Single().Command.Execute(null);
        Assert.Null(Tile(layout.Regions, "torso_front").Fill);
    });

    [Fact] // region-front-highlight-lost fix: after paint then clear, Front rests on #2a1040 again
    public void The_front_tile_keeps_its_tint_after_paint_and_clear() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        var view = Load(layout);
        Border Bd() => (Border)Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).IsFront).Template
            .FindName("Bd", Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).IsFront));
        var tint = (Color)ColorConverter.ConvertFromString("#2A1040");

        Assert.Equal(tint, ((SolidColorBrush)Bd().Background).Color);
        Tile(layout.Regions, "torso_front").PaintCommand.Execute(null);
        view.UpdateLayout();
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)Bd().Background).Color);
        Tile(layout.Regions, "torso_front").ClearCommand.Execute(null);
        view.UpdateLayout();
        Assert.Equal(tint, ((SolidColorBrush)Bd().Background).Color);
    });

    [Fact] // the type list's selection is FillStyle.Type, both ways
    public void The_fill_type_list_tracks_the_current_type() => StaThread.Run(() =>
    {
        var layout = Layout(new EditorSession());
        var view = Load(layout);
        var list = Descendants<ListBox>(view).Single(l => AutomationProperties.GetName(l) == "Fill type");

        Assert.Equal("solid", list.SelectedValue);
        list.SelectedIndex = 6;
        Assert.Equal("camo", layout.FillStyle.Type);
        layout.FillStyle.SetTypeCommand.Execute("dots");
        Assert.Equal(5, list.SelectedIndex);
    });

    [Fact] // fillstyle-type-buttons parityCheck: click each of the seven types; the type follows, exactly one is active, painted regions of the old key adopt it
    public void Each_fill_type_button_sets_the_type_and_is_the_only_active_one() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        var view = Load(layout);
        var list = Descendants<ListBox>(view).Single(l => AutomationProperties.GetName(l) == "Fill type");
        Tile(layout.Regions, "torso_front").PaintCommand.Execute(null);
        string[] types = ["solid", "linear", "radial", "stripes", "checker", "dots", "camo"];
        Assert.Equal(types, layout.FillStyle.Types.Select(t => t.Type));
        Assert.Equal(7, list.Items.Count);

        foreach (int i in new[] { 1, 2, 3, 4, 5, 6, 0 })
        {
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i);
            item.IsSelected = true;                                           // the click's effect on a single-selection list
            StaThread.Pump();

            Assert.Equal(types[i], layout.FillStyle.Type);
            Assert.Equal(types[i], layout.FillStyle.Current.Type);
            Assert.Single(list.SelectedItems);                                // exclusive: one active button
            Assert.Equal(new[] { i }, Enumerable.Range(0, 7).Where(k => ((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(k)).IsSelected));
            Assert.Equal(types[i], RBX15.Engine.DocumentOps.RegionFill(session.Document, "torso_front")!.Type);   // the painted region adopts the new type
        }
    });

    [Fact] // fillstyle-angle-scale parityCheck: bounds and defaults through the sliders' automation names
    public void The_angle_and_scale_sliders_have_4_1s_bounds_defaults_and_whole_steps() => StaThread.Run(() =>
    {
        var layout = Layout(new EditorSession());
        var view = Load(layout);
        var angle = Descendants<Slider>(view).Single(l => AutomationProperties.GetName(l) == "Angle");
        var scale = Descendants<Slider>(view).Single(l => AutomationProperties.GetName(l) == "Pattern scale");

        Assert.Equal((0.0, 360.0, 45.0, 44.0), (angle.Minimum, angle.Maximum, angle.Value, angle.Width));
        Assert.Equal((4.0, 48.0, 16.0, 44.0), (scale.Minimum, scale.Maximum, scale.Value, scale.Width));
        Assert.Equal(("Angle", "Pattern scale"), (angle.ToolTip, scale.ToolTip));
        Assert.All(new[] { angle, scale }, sl => Assert.True(sl.IsSnapToTickEnabled && sl.TickFrequency == 1));

        angle.Value = 360;
        scale.Value = 4;
        Assert.Equal((360.0, 4.0), (layout.FillStyle.Angle, layout.FillStyle.Scale));
        angle.Value = 400;                                                    // past the range: the slider clamps
        scale.Value = 99;
        Assert.Equal((360.0, 48.0), (layout.FillStyle.Angle, layout.FillStyle.Scale));
    });

    [Fact] // home-mode-toggle parityCheck: the click flips the label SHIRT <-> PANTS and the gradient direction cyan->magenta <-> magenta->cyan
    public void The_mode_button_flips_its_label_and_gradient_direction() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var view = Load(Home(session));
        var button = (Button)view.FindName("ModeToggle");
        (string, System.Windows.Media.Color, System.Windows.Media.Color) Look()
        {
            var b = (LinearGradientBrush)button.Background;
            return (Convert.ToString(button.Content)!, b.GradientStops[0].Color, b.GradientStops[^1].Color);
        }
        var cyan = System.Windows.Media.Color.FromRgb(0x17, 0xD4, 0xFA);
        var magenta = System.Windows.Media.Color.FromRgb(0xF2, 0x2F, 0x89);
        Assert.Equal(("SHIRT", cyan, magenta), Look());

        ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(button)
            .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
        Assert.Equal(TemplateMode.Pants, session.Document.Mode);
        Assert.Equal(("PANTS", magenta, cyan), Look());

        Assert.True(session.Undo());                                          // Undo after switching returns to the prior mode
        StaThread.Pump();
        Assert.Equal(("SHIRT", cyan, magenta), Look());
    });

    [Fact] // layout-mode-toggle's region-label half, on the ribbon: the visible limb headings follow the mode (the grid is on the Background tab)
    public void The_limb_headings_on_the_ribbon_follow_the_mode() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        var view = Load(layout);
        string[] Headings() => Descendants<TextBlock>(view)
            .Where(t => t.Text is "Right Arm" or "Left Arm" or "Right Leg" or "Left Leg")
            .Select(t => t.Text).Order().ToArray();

        Assert.Equal(new[] { "Left Arm", "Right Arm" }, Headings());
        new ModeGroupViewModel(session).ToggleCommand.Execute(null);
        StaThread.Pump();
        Assert.Equal(new[] { "Left Leg", "Right Leg" }, Headings());
        Assert.Equal("Waist Front", Tiles(view).Select(AutomationProperties.GetName).Single(n => n.StartsWith("Waist F")));
        Assert.DoesNotContain(Tiles(view).Select(AutomationProperties.GetName), n => n.StartsWith("Torso") || n.Contains("Arm"));

        Assert.True(session.Undo());
        StaThread.Pump();
        Assert.Equal(new[] { "Left Arm", "Right Arm" }, Headings());
    });

    [Fact] // the mode button, settings and edit bindings; M3b shows Tools and Brush; Placement is collapsed until a layer is selected (M4b item 5) and Keys: ON shows in the Tools group (M4b item 10); Save work and Open show (M4a)
    public void Mode_settings_and_edit_bind_and_hidden_groups_are_absent() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var view = Load(Home(session));
        var buttons = Descendants<ButtonBase>(view).ToArray();   // the tool buttons are ToggleButtons (final fix wave 14)
        var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToArray();

        var mode = buttons.Single(b => AutomationProperties.GetName(b).StartsWith("Mode:", StringComparison.Ordinal));
        Assert.Equal("SHIRT", mode.Content);
        Assert.Equal("Mode: SHIRT", AutomationProperties.GetName(mode)); // the name states the current mode
        mode.Command.Execute(null);
        Assert.Equal("PANTS", mode.Content);
        Assert.Equal("Mode: PANTS", AutomationProperties.GetName(mode));

        var undo = buttons.Single(b => (string)b.Content == "Undo");
        Assert.True(undo.IsEnabled);                       // the toggle was one step
        Assert.False(buttons.Single(b => (string)b.Content == "Redo").IsEnabled);

        Descendants<CheckBox>(view).Single(c => (string)c.Content == "Grid").IsChecked = true;
        Assert.True(session.Document.ShowGrid);
        Descendants<TextBox>(view).Single().Text = "Hoodie";
        Assert.Equal("Hoodie", session.TemplateTitle);

        Assert.Equal(new[] { "MODE", "TOOLS", "BRUSH", "EDIT", "SETTINGS" },
            texts.Where(t => t is "MODE" or "TOOLS" or "BRUSH" or "PLACEMENT" or "EDIT" or "SETTINGS" or "FILL STYLE" or "CLICK REGIONS TO FILL"));
        Assert.Contains(buttons, b => b.Content is "Draw");
        Assert.Equal(Visibility.Collapsed, ((UserControl)view.FindName("Placement")).Visibility);   // nothing selected
        Assert.DoesNotContain(buttons, b => b.Content is "Fit Front");
        Assert.Contains(buttons, b => b.Content is "Keys: ON");
        Assert.Contains(buttons, b => b.Content is "New");
        Assert.Contains(buttons, b => b.Content is "Save work");
        Assert.Contains(buttons, b => b.Content is "Open");
        Assert.Equal(["New", "Save work", "Open", "Recent", "Add image", "Download template"],
                     buttons.Select(b => b.Content).OfType<string>().Where(c => c is "New" or "Save work" or "Open" or "Recent" or "Add image" or "Download template").ToArray());   // M5 item 5: Recent sits beside Open; item 18: New comes first
        Assert.DoesNotContain("Set a style, then click a region to paint it. Right-click clears.", texts);   // the fill groups are on Background
    });

    [Fact] // M5 item 5 (H1): the Recent drop-down matches its File group neighbors exactly: SecondaryButton, padding 10,5, 11px, the FocusRing, a name, and the popup menu's entries are named and focus-ringed
    public void The_recent_button_matches_the_file_group_buttons_and_its_menu_is_named() => StaThread.Run(() =>
    {
        var view = Load(Home(new EditorSession()));
        var fileGroup = Descendants<FrameworkElement>(view).OfType<Views.FileGroup>().Single();
        var open = (Button)fileGroup.FindName("OpenButton");
        var recent = (Button)fileGroup.FindName("RecentButton");

        Assert.Equal("Recent", recent.Content);
        Assert.Equal("Recent projects", AutomationProperties.GetName(recent));
        Assert.Same(open.Style, recent.Style);
        Assert.Equal((open.Padding, open.FontSize, open.Margin), (recent.Padding, recent.FontSize, recent.Margin));
        Assert.NotNull(recent.FocusVisualStyle);
        Assert.Equal(open.FocusVisualStyle, recent.FocusVisualStyle);
        Assert.True(recent.Focusable && recent.IsTabStop);
        Assert.Equal(open.ActualHeight, recent.ActualHeight);

        // M5 item 18: New is one more of the same buttons, first in the group.
        var newButton = (Button)fileGroup.FindName("NewButton");
        Assert.Same(open.Style, newButton.Style);
        Assert.Equal((open.Padding, open.FontSize, open.Margin), (newButton.Padding, newButton.FontSize, newButton.Margin));
        Assert.NotNull(newButton.FocusVisualStyle);
        Assert.Equal(open.FocusVisualStyle, newButton.FocusVisualStyle);
        Assert.True(newButton.Focusable && newButton.IsTabStop);
        Assert.Equal(open.ActualHeight, newButton.ActualHeight);

        // The popup's contents are not in the button's visual tree until it opens, so the menu is checked through its named parts.
        var frame = (FrameworkElement)fileGroup.FindName("RecentFrame");
        var clear = (Button)fileGroup.FindName("RecentClear");
        Assert.Equal("Clear recent list", AutomationProperties.GetName(clear));
        Assert.NotNull(clear.FocusVisualStyle);
        Assert.NotNull(frame);
    });

    [Fact] // Este, 2026-10-25 (reversing F1): the Background tab holds 4.1's Layout groups in editor.html order, Fill Style and the regions last
    public void The_background_tab_ends_with_fill_style_and_the_regions() => StaThread.Run(() =>
    {
        var view = Load(Layout(new EditorSession()));
        var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToArray();
        Assert.Equal(new[] { "BACKGROUND", "FILL STYLE", "CLICK REGIONS TO FILL" },
            texts.Where(t => t is "BACKGROUND" or "FILL STYLE" or "CLICK REGIONS TO FILL"));
        Assert.Contains("Set a style, then click a region to paint it. Right-click clears.", texts);
        Assert.Equal(18, Tiles(view).Length);
    });

    [Fact] // controller ruling 3: a thumb that loses mouse capture with no DragCompleted still ends the gesture
    public void A_slider_gesture_ends_on_lost_mouse_capture_as_well_as_drag_completed() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        Tile(layout.Regions, "torso_front").PaintCommand.Execute(null);
        // The group with the button held, as during a real press: Mouse.LeftButton always reads up in a test, and
        // SliderGestureTracker ignores a DragStarted that arrives with the button up (M3b Task 3).
        var group = new FillStyleGroup(() => true) { DataContext = layout.FillStyle };
        var angle = (Slider)group.FindName("AngleSlider");

        void Start() => angle.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        void Complete() => angle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        void LoseCapture() => angle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });

        // capture lost, no DragCompleted: the drag is one step, and the next edit is its own step
        Start();
        layout.FillStyle.Angle = 60;
        layout.FillStyle.Angle = 70;
        Assert.Equal(1, session.UndoCount);
        LoseCapture();
        Assert.Equal(2, session.UndoCount);
        layout.FillStyle.Angle = 80;
        Assert.Equal(3, session.UndoCount);

        // the normal path still works, and the capture loss that follows it is a no-op
        Start();
        layout.FillStyle.Angle = 90;
        Complete();
        LoseCapture();
        Assert.Equal(4, session.UndoCount);
    });

    [Fact] // final fix wave 15: the Fill Style sliders go through SliderGestureTracker.Attach, so a press a template marks handled still opens the one gesture
    public void A_press_already_marked_handled_still_opens_the_fill_style_slider_gesture() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        Tile(layout.Regions, "torso_front").PaintCommand.Execute(null);
        var group = new FillStyleGroup(() => true) { DataContext = layout.FillStyle };
        var scale = (Slider)group.FindName("ScaleSlider");

        scale.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Handled = true });
        layout.FillStyle.Scale = 20;
        layout.FillStyle.Scale = 24;
        Assert.Equal(1, session.UndoCount);                         // the paint; the drag is open
        scale.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Handled = true });
        Assert.Equal((2, false), (session.UndoCount, session.InGesture));
    });

    [Fact] // fix round 1: press-and-hold on the track repeats LargeChange; the press is one gesture, one step
    public void A_pressed_slider_is_one_step_however_many_changes_it_repeats() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var layout = Layout(session);
        Tile(layout.Regions, "torso_front").PaintCommand.Execute(null);
        // The group with the button held, as during a real press: Mouse.LeftButton always reads up in a test, and
        // SliderGestureTracker ignores a DragStarted that arrives with the button up (M3b Task 3).
        var group = new FillStyleGroup(() => true) { DataContext = layout.FillStyle };
        var angle = (Slider)group.FindName("AngleSlider");

        angle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        for (int a = 50; a < 80; a++) layout.FillStyle.Angle = a;   // the repeat button's LargeChange ticks
        Assert.Equal(1, session.UndoCount);
        angle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        Assert.Equal(2, session.UndoCount);

        // a thumb press also raises DragStarted: no second gesture, and the release closes it exactly once
        angle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        angle.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        layout.FillStyle.Angle = 100;
        layout.FillStyle.Angle = 110;
        angle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        angle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        Assert.Equal(3, session.UndoCount);
        Assert.False(session.InGesture);

        // keyboard arrows: outside any press, one step each
        layout.FillStyle.Angle = 111;
        layout.FillStyle.Angle = 112;
        Assert.Equal(5, session.UndoCount);
    });

    [Fact] // fix round 1: Ctrl+click on the selected type deselects; the list snaps back to the active type
    public void Deselecting_the_fill_type_keeps_the_type_and_the_selection() => StaThread.Run(() =>
    {
        var layout = Layout(new EditorSession());
        var view = Load(layout);
        var list = Descendants<ListBox>(view).Single(l => AutomationProperties.GetName(l) == "Fill type");

        list.SelectedItem = null;
        StaThread.Pump();

        Assert.Equal("solid", layout.FillStyle.Type);
        Assert.Equal("solid", list.SelectedValue);
    });

    [Fact] // layout-tool-buttons, tools-set-tool: six buttons in editor.html:43-48 order; the active one is .btn-active, and
           // final fix wave 14: it is a toggle that is On for assistive tech (Narrator does not reliably announce ItemStatus)
    public void The_tool_buttons_show_4_1_labels_and_the_active_tool() => StaThread.Run(() =>
    {
        var home = Home(new EditorSession());
        var view = Load(home);
        var tools = Descendants<ToolToggleButton>(view).ToArray();
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser" }, tools.Select(b => (string)b.Content));
        Assert.Equal(new[] { "Select/Move (V)", "Freehand draw (B)", "Line tool (L)", "Rectangle (U)", "Ellipse (O)", "Eraser (E)" }, tools.Select(b => (string)b.ToolTip)); // home-tool-buttons: the letters are back with the keyboard (M4b item 10)
        Assert.All(tools, b => Assert.True(b.Focusable && b.IsTabStop));
        ToggleState[] States() => tools.Select(b => Toggle(b).ToggleState).ToArray();
        Assert.Equal(new[] { ToggleState.On, ToggleState.Off, ToggleState.Off, ToggleState.Off, ToggleState.Off, ToggleState.Off }, States());
        var cyan = (Color)ColorConverter.ConvertFromString("#17D4FA");
        Assert.Equal(cyan, ((SolidColorBrush)tools[0].Foreground).Color);   // .btn-active text (styles.css:411)

        var draw = tools[1];
        Toggle(draw).Toggle();                                               // assistive tech's toggle runs the button's command
        view.UpdateLayout();
        Assert.Equal(Tool.Pen, home.Tools.Current);
        Assert.Equal(new[] { ToggleState.Off, ToggleState.On, ToggleState.Off, ToggleState.Off, ToggleState.Off, ToggleState.Off }, States());
        Assert.Equal(cyan, ((SolidColorBrush)draw.Foreground).Color);
        Assert.NotEqual(cyan, ((SolidColorBrush)tools[0].Foreground).Color);
        Toggle(draw).Toggle();
        Assert.Equal(Tool.Select, home.Tools.Current);                       // tools-toggle-off
        Assert.Equal(ToggleState.Off, Toggle(draw).ToggleState);

        Toggle(tools[0]).Toggle();                                           // Select never toggles off: the state stays the view-model's
        Assert.Equal((Tool.Select, ToggleState.On), (home.Tools.Current, Toggle(tools[0]).ToggleState));
        Invoke(tools[5]).Invoke();                                           // Invoke stays too, for UIA drivers
        Assert.Equal((Tool.Eraser, ToggleState.On), (home.Tools.Current, Toggle(tools[5]).ToggleState));
        Assert.All(tools, b => Assert.Equal("", AutomationProperties.GetItemStatus(b)));
    });

    static System.Windows.Automation.Provider.IToggleProvider Toggle(UIElement e) =>
        (System.Windows.Automation.Provider.IToggleProvider)System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(e)
            .GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle);

    static System.Windows.Automation.Provider.IInvokeProvider Invoke(UIElement e) =>
        (System.Windows.Automation.Provider.IInvokeProvider)System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(e)
            .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);

    [Fact] // final fix wave 14: the brush swatch tells assistive tech its color as hex
    public void The_brush_swatch_exposes_its_hex_color() => StaThread.Run(() =>
    {
        var home = Home(new EditorSession());
        var view = Load(home);
        var color = Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Draw color");
        Assert.Equal("#17d4fa", AutomationProperties.GetHelpText(color));
        home.Brush.Color = "#FF0000";
        Assert.Equal("#ff0000", AutomationProperties.GetHelpText(color));
    });

    [Fact] // home-brush-group: #drawColor 26x24, #drawSize 1..50 value 4 width 60, Fill (editor.html:56-59); the picker opens under the swatch
    public void The_brush_group_binds_color_size_and_fill() => StaThread.Run(() =>
    {
        var picker = new FakeColorPicker().Answer("#ff0000");
        var home = Home(new EditorSession(), picker);
        var view = Load(home);
        var color = Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Draw color");
        var size = Descendants<Slider>(view).Single(s => AutomationProperties.GetName(s) == "Brush size");
        var fill = Descendants<CheckBox>(view).Single(c => (string)c.Content == "Fill");

        Assert.Equal((26.0, 24.0), (color.Width, color.Height));
        Assert.Equal((1.0, 50.0, 4.0, 60.0), (size.Minimum, size.Maximum, size.Value, size.Width));
        Assert.False(fill.IsChecked);

        color.Command.Execute(color.CommandParameter);
        Assert.Same(color, Assert.Single(picker.Anchors));
        Assert.Equal("#ff0000", home.Brush.Color);
        size.Value = 12;
        Assert.Equal(12, home.Brush.Size);
        home.Brush.StepSize(+1);
        Assert.Equal(13, size.Value);
        fill.IsChecked = true;
        Assert.True(home.Brush.Fill);
    });

    [Fact] // undo-buttons-shortcuts: the tooltips name the chords now that the keyboard works (editor.html:79-80)
    public void Undo_and_redo_tooltips_name_their_chords() => StaThread.Run(() =>
    {
        var view = Load(Home(new EditorSession()));
        var buttons = Descendants<Button>(view);
        Assert.Equal("Undo (Ctrl+Z)", buttons.Single(b => (string)b.Content == "Undo").ToolTip);
        Assert.Equal("Redo (Ctrl+Y / Ctrl+Shift+Z)", buttons.Single(b => (string)b.Content == "Redo").ToolTip);
    });
    [Fact] // regions-fill-all: the All tile sits in the cross, named, focusable, and fills every region; right-click clears
    public void The_all_tile_fills_and_right_click_clears_every_region() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var view = Load(Layout(session));
        var all = Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Fill all regions");
        Assert.True(all.Focusable && all.IsTabStop);

        ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(all)
            .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
        Assert.Equal(18, session.Document.RegionFills.Count);

        var clear = all.InputBindings.OfType<System.Windows.Input.MouseBinding>().Single();
        clear.Command.Execute(null);
        Assert.Empty(session.Document.RegionFills);
    });
}
