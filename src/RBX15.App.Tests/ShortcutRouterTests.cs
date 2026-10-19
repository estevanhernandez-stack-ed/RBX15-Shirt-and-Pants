using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The keyboard router, tool keys and the Keys toggle (M4b item 10; ledger kbd-shortcuts, keys-text-input-guard,
/// kbd-input-focus-trap, keys-toggle-button, home-keys-toggle, keys-tool-shortcuts, kbd-ctrl-combo-tool-toggles,
/// kbd-focused-button-enter-space, keyboard-tool-toggle-interplay, tools-toggle-off, home-tool-buttons,
/// draw-tool-switch-midstroke). <see cref="ShortcutRouter.Route"/> and <see cref="ShortcutRouter.RouteText"/> are pure,
/// so the tables run them over every focus, modifier and Keys state; the window facts prove the wiring.
/// </summary>
[Collection("MainWindow")] // the window facts build a MainWindow, as ShellLookTests does
public class ShortcutRouterTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    static readonly ModifierKeys[] Modifiers = [ModifierKeys.None, ModifierKeys.Shift, ModifierKeys.Control, ModifierKeys.Alt];

    static readonly (string Text, ShortcutAction Action)[] ToolKeys =
    [
        ("v", ShortcutAction.ToggleSelect), ("b", ShortcutAction.TogglePen), ("l", ShortcutAction.ToggleLine),
        ("u", ShortcutAction.ToggleRect), ("o", ShortcutAction.ToggleEllipse), ("e", ShortcutAction.ToggleEraser),
    ];

    /// <summary>One focused element per focus state the router tells apart, built on the test's STA thread.</summary>
    static (string Name, ShortcutFocus Expected, IInputElement? Element)[] Foci()
    {
        var layers = new LayersPanel { DataContext = TestShell.Quiet(TestShell.Main(SessionWith(LayerOf(1)))).Layers };
        StaThread.Pump();
        Arrange(layers, 300, 300);
        var list = (ListBox)layers.FindName("LayerList");
        var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);

        var regions = new RegionsGrid { DataContext = Home(new EditorSession()).Regions };
        Arrange(regions, 900, 300);
        var tile = Descendants<Button>(regions).First(b => b.DataContext is RegionTileViewModel);

        var picker = new ColorPickerPopup(new ColorPickerViewModel("#ff0000"));
        var square = (IInputElement)picker.FindName("SvSquare");

        return
        [
            ("none", ShortcutFocus.None, null),
            ("TextBox", ShortcutFocus.TextField, new TextBox()),
            ("RichTextBox", ShortcutFocus.TextField, new RichTextBox()),
            ("Slider", ShortcutFocus.Other, new Slider()),
            ("CheckBox", ShortcutFocus.Other, new CheckBox()),
            ("Button", ShortcutFocus.Other, new Button()),
            ("ToggleButton", ShortcutFocus.Other, new ToggleButton()),
            ("RadioButton (a ribbon tab)", ShortcutFocus.Other, new RadioButton()),
            ("ComboBox", ShortcutFocus.Other, new ComboBox()),
            ("layer list", ShortcutFocus.LayerList, list),
            ("layer row", ShortcutFocus.LayerList, row),
            ("region tile", ShortcutFocus.RegionTile, tile),
            ("3D viewport", ShortcutFocus.Viewport, new Viewport3D()),
            ("color picker popup", ShortcutFocus.ColorPicker, square),
        ];
    }

    static void Arrange(FrameworkElement view, double width, double height)
    {
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();
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

    // ---- the pure router: Route (key down) and RouteText (characters)

    [Fact] // K1: each focus state is told apart; a text field is any TextBoxBase
    public void Focus_is_classified_by_the_k1_exemption_list() => StaThread.Run(() =>
    {
        foreach (var (name, expected, element) in Foci())
            Assert.True(expected == ShortcutRouter.Classify(element), $"{name}: expected {expected}, got {ShortcutRouter.Classify(element)}");
    });

    [Fact] // K2: tool keys are characters, so Route (key down) fires no action for any key in this item, whatever the focus, modifiers or
           // Keys state; Ctrl chords are KeyBindings (behind TextFieldShortcutCommand), and item 11's layer keys are added to Route
    public void Route_fires_no_action_for_any_key_yet() => StaThread.Run(() =>
    {
        var keys = new[] { Key.V, Key.B, Key.L, Key.U, Key.O, Key.E, Key.Z, Key.D, Key.Space, Key.Enter, Key.Tab, Key.Escape };
        foreach (var (name, _, element) in Foci())
            foreach (var mods in Modifiers)
                foreach (bool on in new[] { true, false })
                    foreach (var key in keys)
                        Assert.True(ShortcutRouter.Route(key, mods, element, on) is null, $"{key} with {mods} on {name}, Keys {(on ? "ON" : "OFF")}");
    });

    [Fact] // the table: focus x modifiers x Keys x character. A text field and the color picker keep every key (K1); the layer list, a region
           // tile and the 3D viewport keep only their own keys, which are item 11's, not the tool letters; Ctrl or Alt held never toggles
           // a tool (K3); Shift does not matter; Keys OFF blocks every bare key
    public void RouteText_table_over_focus_modifiers_and_keys() => StaThread.Run(() =>
    {
        foreach (var (name, expectedFocus, element) in Foci())
            foreach (var mods in Modifiers)
                foreach (bool on in new[] { true, false })
                    foreach (var (text, action) in ToolKeys)
                        foreach (var typed in new[] { text, text.ToUpperInvariant() })
                        {
                            bool trapped = expectedFocus is ShortcutFocus.TextField or ShortcutFocus.ColorPicker;
                            bool bare = (mods & (ModifierKeys.Control | ModifierKeys.Alt)) == 0;
                            ShortcutAction? expected = on && !trapped && bare ? action : null;
                            Assert.True(expected == ShortcutRouter.RouteText(typed, mods, element, on),
                                $"'{typed}' with {mods} on {name}, Keys {(on ? "ON" : "OFF")}: expected {expected}");
                        }
    });

    [Fact] // text that is not exactly one tool letter is nothing: other letters, the control characters Ctrl+V and Ctrl+E type, empty, longer
    public void RouteText_ignores_everything_but_the_six_tool_letters()
    {
        foreach (var text in new[] { "x", "d", "r", "[", "+", " ", "", "vv", "ve", "\u0016", "\u0005", "\u0002", "1" })
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.None, null, true));
        Assert.Null(ShortcutRouter.RouteText(null!, ModifierKeys.None, null, true));
    }

    [Fact] // keys-tool-shortcuts: v b l u o e, either case (Shift+B == b, Caps Lock too), match the characters the layout types (K2)
    public void The_six_letters_map_to_the_six_tools_in_either_case()
    {
        foreach (var (text, action) in ToolKeys)
        {
            Assert.Equal(action, ShortcutRouter.RouteText(text, ModifierKeys.None, null, true));
            Assert.Equal(action, ShortcutRouter.RouteText(text.ToUpperInvariant(), ModifierKeys.Shift, null, true));
        }
        Assert.Equal(new[] { Tool.Select, Tool.Pen, Tool.Line, Tool.Rect, Tool.Ellipse, Tool.Eraser },
            ToolKeys.Select(k => ShortcutRouter.ToolOf(k.Action)));
    }

    // ---- executing: toggleTool, one code path (tools-toggle-off, keyboard-tool-toggle-interplay)

    [Fact] // B twice gives Pen and then Select; V twice stays Select; Shift+B equals b; Ctrl+E and Ctrl+V do nothing to the tool
    public void Tool_keys_run_toggleTool()
    {
        var tools = new ToolsViewModel();
        var router = new ShortcutRouter(tools);
        void Press(string text, ModifierKeys mods = ModifierKeys.None)
        {
            if (ShortcutRouter.RouteText(text, mods, null, true) is { } action) router.Execute(action);
        }

        Press("b");
        Assert.Equal(Tool.Pen, tools.Current);
        Press("b");
        Assert.Equal(Tool.Select, tools.Current);
        Press("v");
        Press("v");
        Assert.Equal(Tool.Select, tools.Current);
        Press("B", ModifierKeys.Shift);
        Assert.Equal(Tool.Pen, tools.Current);
        Press("\u0005", ModifierKeys.Control);   // Ctrl+E types U+0005
        Press("e", ModifierKeys.Control);
        Press("v", ModifierKeys.Control);
        Press("\u0016", ModifierKeys.Control);   // Ctrl+V types U+0016
        Press("o", ModifierKeys.Alt);
        Assert.Equal(Tool.Pen, tools.Current);
        Press("e");
        Press("l");                              // another tool switches, it does not toggle off
        Assert.Equal(Tool.Line, tools.Current);
    }

    // ---- the window: PreviewTextInput and PreviewKeyDown go to the router

    sealed class Rig : IDisposable
    {
        public Rig(EditorSession? session = null)
        {
            Session = session ?? new EditorSession();
            Main = TestShell.Quiet(TestShell.Main(Session));
            Window = new MainWindow(Main, new FakeSettingsStore(), [Primary]);
            StaThread.Pump();
            Arrange(Root, 1920, 1040);
            Window.Modifiers = () => ModifierKeys.None;   // never the machine's real modifier state
            Source = new HwndSource(new HwndSourceParameters("shortcut-router-test"));
        }

        public EditorSession Session { get; }
        public MainViewModel Main { get; }
        public MainWindow Window { get; }
        public FrameworkElement Root => (FrameworkElement)Window.Content;
        public HwndSource Source { get; }
        public Tool Tool => Main.Home.Tools.Current;

        public void Dispose() => Source.Dispose();

        /// <summary>Types one character into the window with <paramref name="focused"/> focused, as the keyboard would; returns whether the router handled it.</summary>
        public bool Type(string text, IInputElement? focused = null, ModifierKeys mods = ModifierKeys.None)
        {
            Window.FocusedElement = () => focused;
            Window.Modifiers = () => mods;
            var raiseOn = focused as UIElement is { } ui && ReferenceEquals(Window, System.Windows.Window.GetWindow(ui)) ? ui : Window;
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, raiseOn, text))
                { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
            raiseOn.RaiseEvent(args);
            return args.Handled;
        }

        public bool Down(Key key, IInputElement? focused = null, ModifierKeys mods = ModifierKeys.None)
        {
            Window.FocusedElement = () => focused;
            Window.Modifiers = () => mods;
            var raiseOn = focused as UIElement is { } ui && ReferenceEquals(Window, System.Windows.Window.GetWindow(ui)) ? ui : Window;
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, Source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            raiseOn.RaiseEvent(args);
            return args.Handled;
        }

        public T Find<T>(string name) where T : FrameworkElement => Descendants<T>(Root).Single(e => e.Name == name);
    }

    [Fact] // the done-when: after a Slider has focus, V still switches the tool (kbd-input-focus-trap); so do a checkbox, a button and a ribbon tab
    public void After_a_slider_has_focus_V_still_switches_the_tool() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        foreach (IInputElement control in new UIElement[] { new Slider(), new CheckBox(), new Button(), new RadioButton(), new ToggleButton() })
        {
            Assert.True(rig.Type("b", control));
            Assert.Equal(Tool.Pen, rig.Tool);
            Assert.True(rig.Type("v", control));
            Assert.Equal(Tool.Select, rig.Tool);
        }
    });

    [Fact] // a real slider in the window: the Selected layer panel's Scale slider, after a drag leaves it focused
    public void A_focused_panel_slider_does_not_trap_the_tool_letters() => StaThread.Run(() =>
    {
        using var rig = new Rig(PlacementGroupTests.Selected());
        var slider = Descendants<Slider>(rig.Root).First();
        Assert.True(rig.Type("b", slider));
        Assert.Equal(Tool.Pen, rig.Tool);
        Assert.True(rig.Type("b", slider));
        Assert.Equal(Tool.Select, rig.Tool);
    });

    [Fact] // the done-when: in a TextBox nothing fires (keys-text-input-guard): neither the characters nor the key downs, and nothing is handled
    public void In_a_text_box_nothing_fires() => StaThread.Run(() =>
    {
        using var rig = new Rig(SessionWith(LayerOf(1), LayerOf(2)));
        rig.Session.Select(2);
        rig.Main.Home.Tools.Set(Tool.Pen);
        var box = new TextBox();
        foreach (var text in new[] { "v", "V", "b", "l", "u", "o", "e", "E" })
            Assert.False(rig.Type(text, box));
        foreach (var key in new[] { Key.V, Key.B, Key.Delete, Key.Back, Key.Left, Key.OemOpenBrackets })
            Assert.False(rig.Down(key, box));
        Assert.Equal(Tool.Pen, rig.Tool);
        Assert.Equal((2, 2, 0), (rig.Session.Document.Layers.Count, rig.Session.SelectedLayerId, rig.Session.UndoCount));
    });

    [Fact] // the Text tab's own boxes (item 9) are TextBoxBase too: the same guard, through the real view
    public void The_text_tab_boxes_keep_the_tool_letters() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.Main.ActiveTab = RibbonTab.Text;
        StaThread.Pump();
        rig.Main.Home.Tools.Set(Tool.Rect);
        foreach (var name in new[] { "WordArtText", "SizeBox" })
        {
            var box = rig.Find<TextBox>(name);
            Assert.False(rig.Type("v", box));
            Assert.False(rig.Type("e", box));
        }
        Assert.Equal(Tool.Rect, rig.Tool);
    });

    [Fact] // K1: the layer list, a region tile and the 3D viewport keep only their own keys (item 11's); the tool letters still switch the tool,
           // as in 4.1 (a layer row is not an INPUT), and it matters: clicking a layer leaves focus on its row
    public void The_layer_list_a_region_tile_and_the_viewport_do_not_trap_the_tool_letters() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        foreach (var (name, _, element) in Foci().Where(f => f.Expected is ShortcutFocus.LayerList or ShortcutFocus.RegionTile or ShortcutFocus.Viewport))
        {
            Assert.True(rig.Type("u", element), name);
            Assert.Equal(Tool.Rect, rig.Tool);
            Assert.True(rig.Type("u", element), name);
            Assert.Equal(Tool.Select, rig.Tool);
        }
    });

    [Fact] // K1: an open color picker traps every key; its letters never reach the tool
    public void An_open_color_picker_traps_the_tool_letters() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        var (_, _, square) = Foci().Single(f => f.Expected == ShortcutFocus.ColorPicker);
        Assert.False(rig.Type("b", square));
        Assert.Equal(Tool.Select, rig.Tool);
    });

    [Fact] // keys-tool-shortcuts, kbd-ctrl-combo-tool-toggles: Ctrl+V/B/L/U/O/E (and Alt) type or send nothing the router answers to
    public void Ctrl_and_alt_letters_leave_the_tool_alone() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.Main.Home.Tools.Set(Tool.Pen);
        foreach (var (letter, control) in new[] { ("v", "\u0016"), ("b", "\u0002"), ("l", "\u000c"), ("u", "\u0015"), ("o", "\u000f"), ("e", "\u0005") })
        {
            Assert.False(rig.Type(control, null, ModifierKeys.Control));
            Assert.False(rig.Type(letter, null, ModifierKeys.Control));
            Assert.False(rig.Type(letter, null, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.False(rig.Type(letter, null, ModifierKeys.Alt));
            Assert.False(rig.Down(Enum.Parse<Key>(letter.ToUpperInvariant()), null, ModifierKeys.Control));
        }
        Assert.Equal(Tool.Pen, rig.Tool);
    });

    [Fact] // keyboard-tool-toggle-interplay: a handled letter is Handled (4.1's preventDefault), so a list's type-ahead never sees it
    public void A_handled_letter_is_handled_and_an_unhandled_one_is_not() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        Assert.True(rig.Type("l"));
        Assert.Equal(Tool.Line, rig.Tool);
        Assert.False(rig.Type("x"));
        Assert.False(rig.Type("5"));
        Assert.Equal(Tool.Line, rig.Tool);
    });

    // ---- the Keys toggle (keys-toggle-button, home-keys-toggle)

    [Fact] // ON at launch; a click flips it; session only (a new view-model is ON again)
    public void The_keys_view_model_starts_on_and_flips_with_its_label()
    {
        var keys = new KeysViewModel();
        var names = Track(keys);
        Assert.True(keys.Enabled);
        Assert.Equal("Keys: ON", keys.Label);
        Assert.Equal("Toggle keyboard shortcuts", keys.ToolTip);
        keys.ToggleCommand.Execute(null);
        Assert.False(keys.Enabled);
        Assert.Equal("Keys: OFF", keys.Label);
        Assert.Contains(nameof(KeysViewModel.Label), names);
        keys.ToggleCommand.Execute(null);
        Assert.True(keys.Enabled);
        Assert.Equal("Keys: ON", keys.Label);
        Assert.True(new KeysViewModel().Enabled);   // nothing is saved
    }

    [Fact] // the toggle touches no document: not dirty, no undo step
    public void Flipping_the_toggle_is_not_a_document_change() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var main = TestShell.Quiet(TestShell.Main(session));
        main.Home.Keys.ToggleCommand.Execute(null);
        Assert.False(main.Home.Keys.Enabled);
        Assert.Equal((0, false), (session.UndoCount, session.IsDirty));
    });

    [Fact] // the button: the label, #2d4a2d on #7fff7f when ON and #4a2d2d on #ff7f7f when OFF, and the tooltip (editor.html:49, editor.js:1767-1773)
    public void The_keys_button_flips_its_label_and_colors() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        var button = rig.Find<Button>("KeysButton");
        Assert.Equal("Keys: ON", button.Content);
        Assert.Equal("Toggle keyboard shortcuts", button.ToolTip);
        Assert.Equal(Color.FromRgb(0x2d, 0x4a, 0x2d), ((SolidColorBrush)button.Background).Color);
        Assert.Equal(Color.FromRgb(0x7f, 0xff, 0x7f), ((SolidColorBrush)button.Foreground).Color);

        Invoke(button);
        Assert.False(rig.Main.Home.Keys.Enabled);
        Assert.Equal("Keys: OFF", button.Content);
        Assert.Equal(Color.FromRgb(0x4a, 0x2d, 0x2d), ((SolidColorBrush)button.Background).Color);
        Assert.Equal(Color.FromRgb(0xff, 0x7f, 0x7f), ((SolidColorBrush)button.Foreground).Color);

        Invoke(button);
        Assert.Equal("Keys: ON", button.Content);
        Assert.Equal(Color.FromRgb(0x2d, 0x4a, 0x2d), ((SolidColorBrush)button.Background).Color);
    });

    [Fact] // it sits in the Tools group, after the six tool buttons, a plain Button a screen reader names by its label
    public void The_keys_button_is_in_the_tools_group_after_the_tools() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        var group = rig.Find<ToolsGroup>("Tools");
        var buttons = Descendants<ButtonBase>(group).ToArray();
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser", "Keys: ON" }, buttons.Select(b => (string)b.Content));
        Assert.IsType<Button>(buttons[^1]);
        Assert.True(buttons[^1].Focusable && buttons[^1].IsTabStop);
    });

    [Fact] // Keys OFF blocks V (and every bare key) but not Ctrl+Z: the chord is a KeyBinding the router's gate never sees (the fix on both rows)
    public void Keys_off_blocks_v_but_not_ctrl_z() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1));
        using var rig = new Rig(session);
        session.Apply(d => d with { ShowGrid = true });
        rig.Main.Home.Keys.ToggleCommand.Execute(null);
        Assert.False(rig.Main.Home.Keys.Enabled);

        Assert.False(rig.Type("b"));
        Assert.False(rig.Type("v"));
        Assert.Equal(Tool.Select, rig.Tool);

        var undo = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.Z && b.Modifiers == ModifierKeys.Control);
        rig.Window.FocusedElement = () => null;
        Assert.True(undo.Command.CanExecute(null));
        undo.Command.Execute(null);
        Assert.False(session.Document.ShowGrid);   // undone with Keys OFF

        rig.Main.Home.Keys.ToggleCommand.Execute(null);
        Assert.True(rig.Type("b"));                // ON again: the same key works
        Assert.Equal(Tool.Pen, rig.Tool);
    });

    // ---- the tool buttons get their letters back (home-tool-buttons)

    [Fact]
    public void The_tool_tooltips_carry_their_letters() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        var group = rig.Find<ToolsGroup>("Tools");
        Assert.Equal(new[] { "Select/Move (V)", "Freehand draw (B)", "Line tool (L)", "Rectangle (U)", "Ellipse (O)", "Eraser (E)" },
            Descendants<ToolToggleButton>(group).Select(b => (string)b.ToolTip));
    });

    // ---- a tool switch mid stroke (draw-tool-switch-midstroke)

    [Fact] // pressing V mid-stroke leaves no live preview and no layer: the stroke is cancelled, nothing is committed, the release does nothing
    public void Pressing_V_mid_stroke_leaves_no_preview_and_no_layer() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.Main.Home.Tools.Set(Tool.Pen);
        var i = rig.Main.Canvas.Interaction;
        i.PointerDown(new Point(100, 100), 1);
        i.PointerMove(new Point(150, 150), 1);
        Assert.NotNull(i.Preview);

        Assert.True(rig.Type("v"));
        Assert.Equal(Tool.Select, rig.Tool);
        Assert.Null(i.Preview);
        Assert.Equal(CanvasGesture.None, i.Gesture);
        i.PointerUp(new Point(150, 150), 1);
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal(0, rig.Session.UndoCount);
    });

    [Fact] // the same for the active shape tool's own key (Rect again goes back to Select)
    public void Pressing_the_active_tools_key_mid_stroke_also_cancels_it() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.Main.Home.Tools.Set(Tool.Rect);
        var i = rig.Main.Canvas.Interaction;
        i.PointerDown(new Point(100, 100), 1);
        i.PointerMove(new Point(180, 160), 1);
        Assert.NotNull(i.Preview);
        Assert.True(rig.Type("u"));
        Assert.Null(i.Preview);
        i.PointerUp(new Point(180, 160), 1);
        Assert.Empty(rig.Session.Document.Layers);
    });

    // ---- Space and Enter on a focused button stay WPF's own (kbd-focused-button-enter-space, keep)

    [Fact] // the router answers none of them, with every focus kind, so a button's Space and Enter are never swallowed
    public void The_router_never_takes_space_or_enter() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        foreach (var (name, _, element) in Foci())
            foreach (var key in new[] { Key.Space, Key.Enter })
                Assert.False(rig.Down(key, element), $"{key} on {name}");
        Assert.False(rig.Type(" "));
    });

    [Fact] // Space on a focused Scatter button (the Placement group's on Home, the Presets group's on Layout) repeats the click:
           // WPF's own ButtonBase handling, through the window's preview handlers
    public void Space_on_a_focused_scatter_button_repeats_the_click() => StaThread.Run(() =>
    {
        foreach (var tab in new[] { RibbonTab.Home, RibbonTab.Layout })
        {
            var session = PlacementGroupTests.Selected(200, 100);
            using var rig = new Rig(session);
            rig.Main.ActiveTab = tab;
            StaThread.Pump();
            Arrange(rig.Root, 1920, 1040);
            var pane = rig.Find<UserControl>(tab == RibbonTab.Home ? "HomePane" : "LayoutPane");
            var scatter = Descendants<Button>(pane).Single(b => b.Name == "ScatterButton");
            rig.Window.FocusedElement = () => scatter;
            int clicks = 0;
            scatter.Click += (_, _) => clicks++;

            Invoke(scatter);                                         // the mouse click, as UI Automation sends it
            Assert.Equal(1, clicks);
            int layers = session.Document.Layers.Count;
            Assert.True(layers > 1, tab.ToString());                 // Scatter added layers
            int steps = session.UndoCount;

            void Space(RoutedEvent routed) =>
                scatter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, rig.Source, 0, Key.Space) { RoutedEvent = routed });
            Space(Keyboard.PreviewKeyDownEvent);
            Space(Keyboard.KeyDownEvent);
            Space(Keyboard.PreviewKeyUpEvent);
            Space(Keyboard.KeyUpEvent);
            StaThread.Pump();
            Assert.Equal(2, clicks);                                 // Space repeated it
            Assert.True(session.Document.Layers.Count > layers);     // adding more layers, one more undo step
            Assert.Equal(steps + 1, session.UndoCount);
        }
    });

    /// <summary>A click as UI Automation sends it; the peer queues the click on the dispatcher, so the queue runs before this returns.</summary>
    static void Invoke(Button b)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
    }
}
