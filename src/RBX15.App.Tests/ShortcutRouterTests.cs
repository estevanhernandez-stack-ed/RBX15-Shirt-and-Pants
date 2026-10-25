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
using RBX15.Engine;
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

    static readonly ModifierKeys[] Modifiers = [ModifierKeys.None, ModifierKeys.Shift, ModifierKeys.Control, ModifierKeys.Alt, ModifierKeys.Control | ModifierKeys.Alt];

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
            ("Slider", ShortcutFocus.Slider, new Slider()),
            ("CheckBox", ShortcutFocus.Other, new CheckBox()),
            ("Button", ShortcutFocus.Other, new Button()),
            ("ToggleButton", ShortcutFocus.Other, new ToggleButton()),
            ("RadioButton (a ribbon tab)", ShortcutFocus.Other, new RadioButton()),
            ("ComboBox", ShortcutFocus.ComboBox, new ComboBox()),
            ("ComboBoxItem (an open font list)", ShortcutFocus.ComboBox, new ComboBoxItem()),
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

    [Fact] // K2: the tool keys are characters, so Route (key down) answers none of them, nor Space, Enter, Tab or a chord's letter, whatever the focus,
           // modifiers or Keys state: Ctrl chords are KeyBindings (behind TextFieldShortcutCommand)
    public void Route_answers_only_the_arrows_delete_and_backspace() => StaThread.Run(() =>
    {
        var keys = new[] { Key.V, Key.B, Key.L, Key.U, Key.O, Key.E, Key.Z, Key.D, Key.Y, Key.R, Key.OemOpenBrackets, Key.Space, Key.Enter, Key.Tab, Key.Escape, Key.Home, Key.F10, Key.Apps };
        foreach (var (name, _, element) in Foci())
            foreach (var mods in Modifiers)
                foreach (bool on in new[] { true, false })
                    foreach (var key in keys)
                        Assert.True(ShortcutRouter.Route(key, mods, element, on, true) is null, $"{key} with {mods} on {name}, Keys {(on ? "ON" : "OFF")}");
    });

    [Fact] // the table: focus x modifiers x Keys x character. A text field, the color picker and a font list keep every key (K1, look call (e)); the layer list, a region
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
                            bool trapped = expectedFocus is ShortcutFocus.TextField or ShortcutFocus.ColorPicker or ShortcutFocus.ComboBox;
                            bool bare = ((mods & ModifierKeys.Control) != 0) == ((mods & ModifierKeys.Alt) != 0);   // none, or Ctrl+Alt together (AltGr, D9)
                            ShortcutAction? expected = on && !trapped && bare ? action : null;
                            Assert.True(expected == ShortcutRouter.RouteText(typed, mods, element, on, false),
                                $"'{typed}' with {mods} on {name}, Keys {(on ? "ON" : "OFF")}: expected {expected}");
                        }
    });

    [Fact] // text that is not exactly one shortcut character is nothing: other letters, the control characters Ctrl+V, Ctrl+E and Ctrl+R type,
           // Escape, empty, longer; and with nothing selected the layer keys are nothing too (the tool letters do not need a selection)
    public void RouteText_ignores_everything_but_the_shortcut_characters()
    {
        foreach (var text in new[] { "x", "d", " ", "", "vv", "ve", "[]", "\u0016", "\u0005", "\u0002", "\u0012", "\u001b", "1" })
            foreach (bool selected in new[] { true, false })
                Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.None, null, true, selected));
        foreach (var text in new[] { "r", "R", "[", "]", "{", "}", "+", "=", "-", "_" })
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.None, null, true, hasSelection: false));
        Assert.Null(ShortcutRouter.RouteText(null!, ModifierKeys.None, null, true, true));
    }

    [Fact] // keys-tool-shortcuts: v b l u o e, either case (Shift+B == b, Caps Lock too), match the characters the layout types (K2)
    public void The_six_letters_map_to_the_six_tools_in_either_case()
    {
        foreach (var (text, action) in ToolKeys)
        {
            Assert.Equal(action, ShortcutRouter.RouteText(text, ModifierKeys.None, null, true, false));
            Assert.Equal(action, ShortcutRouter.RouteText(text.ToUpperInvariant(), ModifierKeys.Shift, null, true, false));
        }
        Assert.Equal(new[] { Tool.Select, Tool.Pen, Tool.Line, Tool.Rect, Tool.Ellipse, Tool.Eraser },
            ToolKeys.Select(k => ShortcutRouter.ToolOf(k.Action)));
    }

    // ---- executing: toggleTool, one code path (tools-toggle-off, keyboard-tool-toggle-interplay)

    [Fact] // B twice gives Pen and then Select; V twice stays Select; Shift+B equals b; Ctrl+E and Ctrl+V do nothing to the tool
    public void Tool_keys_run_toggleTool()
    {
        var tools = new ToolsViewModel();
        var router = new ShortcutRouter(tools, new LayerKeys(new EditorSession()));
        void Press(string text, ModifierKeys mods = ModifierKeys.None)
        {
            if (ShortcutRouter.RouteText(text, mods, null, true, false) is { } action) router.Execute(action);
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

    // ---- item 11: the layer keys (keys-arrow-nudge, keys-rotate-brackets, kbd-bracket-rotate-negative-drift, keys-scale-plus-minus,
    //      keys-delete, keys-selection-gate-and-modifiers), K1's per-key trap table, and D9 (AltGr)

    static Layer Pose(int id = 1, double x = 100, double y = 100, double scale = 1, double rotation = 0) =>
        LayerOf(id) with { X = x, Y = y, Scale = scale, Rotation = rotation };

    static EditorSession Selected(params Layer[] layers)
    {
        var session = SessionWith(layers);
        session.Select(layers[^1].Id);
        return session;
    }

    static Layer Top(EditorSession s) => s.Document.Layers[^1];

    [Fact] // K1: the layer list keeps Up, Down, Alt+Up and Alt+Down; every other key, Delete and Backspace included, still reaches the layer
    public void The_layer_list_keeps_its_own_keys() => StaThread.Run(() =>
    {
        var list = Foci().Single(f => f.Expected == ShortcutFocus.LayerList && f.Name == "layer list").Element;
        var row = Foci().Single(f => f.Name == "layer row").Element;
        foreach (var element in new[] { list, row })
        {
            foreach (var (key, mods) in new[] { (Key.Up, ModifierKeys.None), (Key.Down, ModifierKeys.None), (Key.Up, ModifierKeys.Alt),
                                                 (Key.Down, ModifierKeys.Alt) })
                Assert.Null(ShortcutRouter.Route(key, mods, element, true, true));
            Assert.Equal(ShortcutAction.NudgeLeft, ShortcutRouter.Route(Key.Left, ModifierKeys.None, element, true, true));
            Assert.Equal(ShortcutAction.NudgeRight, ShortcutRouter.Route(Key.Right, ModifierKeys.None, element, true, true));
            Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Back, ModifierKeys.None, element, true, true));
            Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Delete, ModifierKeys.None, element, true, true));
        }
    });

    [Fact] // keys-delete, 4.1: click a layer, press Delete. The real row, focused, in the window: the layer goes in one undo step, the selection to the new top
    public void Delete_on_a_focused_layer_row_removes_the_layer() => StaThread.Run(() =>
    {
        foreach (var key in new[] { Key.Delete, Key.Back })
        {
            using var rig = new Rig(Selected(Pose(1), Pose(2), Pose(3)));
            var panel = new LayersPanel { DataContext = rig.Main.Layers };
            StaThread.Pump();
            Arrange(panel, 300, 300);
            var list = (ListBox)panel.FindName("LayerList");
            rig.Session.Select(3);
            StaThread.Pump();
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);   // rows are top first: layer 3
            Assert.Equal(ShortcutFocus.LayerList, ShortcutRouter.Classify(row));

            Assert.True(rig.Down(key, row));
            Assert.Equal(new[] { 1, 2 }, rig.Session.Document.Layers.Select(l => l.Id));
            Assert.Equal(2, rig.Session.SelectedLayerId);
            Assert.Equal(1, rig.Session.UndoCount);
            rig.Session.Undo();
            Assert.Equal(new[] { 1, 2, 3 }, rig.Session.Document.Layers.Select(l => l.Id));
        }
    });

    [Fact] // K1: a focused region tile keeps Delete, Shift+F10 and Menu (its clear gestures); the arrows and Backspace still reach the layer
    public void A_region_tile_keeps_delete_shift_f10_and_menu() => StaThread.Run(() =>
    {
        var tile = Foci().Single(f => f.Expected == ShortcutFocus.RegionTile).Element;
        Assert.Null(ShortcutRouter.Route(Key.Delete, ModifierKeys.None, tile, true, true));
        Assert.Null(ShortcutRouter.Route(Key.F10, ModifierKeys.Shift, tile, true, true));
        Assert.Null(ShortcutRouter.Route(Key.Apps, ModifierKeys.None, tile, true, true));
        Assert.Equal(ShortcutAction.NudgeUp, ShortcutRouter.Route(Key.Up, ModifierKeys.None, tile, true, true));
        Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Back, ModifierKeys.None, tile, true, true));
        Assert.True(ShortcutRouter.Keeps(ShortcutFocus.RegionTile, Key.F10, ModifierKeys.Shift));
        Assert.False(ShortcutRouter.Keeps(ShortcutFocus.RegionTile, Key.F10, ModifierKeys.None));
    });

    [Fact] // K1: the 3D viewport keeps the arrows (any modifier) and Home; Delete and Backspace still reach the layer
    public void The_viewport_keeps_the_arrows_and_home() => StaThread.Run(() =>
    {
        var viewport = Foci().Single(f => f.Expected == ShortcutFocus.Viewport).Element;
        foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Home })
            foreach (var mods in new[] { ModifierKeys.None, ModifierKeys.Shift, ModifierKeys.Alt })
                Assert.Null(ShortcutRouter.Route(key, mods, viewport, true, true));
        Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Delete, ModifierKeys.None, viewport, true, true));
        Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Back, ModifierKeys.None, viewport, true, true));
    });

    [Fact] // K1: a text field and an open color picker keep every key; a font list keeps every character and the arrows; a slider keeps
           // the arrows; a checkbox, button or tab traps none; the three exemption foci have their own rows above
    public void Text_fields_and_the_picker_keep_every_key_and_the_rest_keep_none() => StaThread.Run(() =>
    {
        var keys = new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Delete, Key.Back };
        foreach (var (name, expected, element) in Foci())
        {
            if (expected is ShortcutFocus.Slider or ShortcutFocus.ComboBox)
            {
                foreach (var key in keys)
                    Assert.True((ShortcutRouter.Route(key, ModifierKeys.None, element, true, true) is null) == key is not (Key.Delete or Key.Back), $"{key} on {name}");
                foreach (var text in new[] { "[", "]", "{", "}", "r", "+", "-", "v" })
                    Assert.True((ShortcutRouter.RouteText(text, ModifierKeys.None, element, true, true) is null) == (expected == ShortcutFocus.ComboBox), $"'{text}' on {name}");
            }
            else if (expected is ShortcutFocus.TextField or ShortcutFocus.ColorPicker)
            {
                foreach (var key in keys) Assert.True(ShortcutRouter.Route(key, ModifierKeys.None, element, true, true) is null, $"{key} on {name}");
                foreach (var text in new[] { "[", "]", "{", "}", "r", "+", "-", "v" })
                    Assert.True(ShortcutRouter.RouteText(text, ModifierKeys.None, element, true, true) is null, $"'{text}' on {name}");
            }
            else
            {
                if (expected is ShortcutFocus.None or ShortcutFocus.Other)
                    foreach (var key in keys) Assert.True(ShortcutRouter.Route(key, ModifierKeys.None, element, true, true) is not null, $"{key} on {name}");
                foreach (var text in new[] { "[", "]", "{", "}", "r", "+", "-", "v" })
                    Assert.True(ShortcutRouter.RouteText(text, ModifierKeys.None, element, true, true) is not null, $"'{text}' on {name}");
            }
        }
    });

    [Fact] // the arrows: 1 template pixel, 10 with Shift, Alt does not stop them (4.1 did not exclude it); Delete and Backspace remove
    public void Route_maps_the_arrows_and_delete()
    {
        foreach (var (key, small, big) in new[]
        {
            (Key.Left, ShortcutAction.NudgeLeft, ShortcutAction.NudgeLeftBig), (Key.Right, ShortcutAction.NudgeRight, ShortcutAction.NudgeRightBig),
            (Key.Up, ShortcutAction.NudgeUp, ShortcutAction.NudgeUpBig), (Key.Down, ShortcutAction.NudgeDown, ShortcutAction.NudgeDownBig),
        })
        {
            Assert.Equal(small, ShortcutRouter.Route(key, ModifierKeys.None, null, true, true));
            Assert.Equal(big, ShortcutRouter.Route(key, ModifierKeys.Shift, null, true, true));
            Assert.Equal(small, ShortcutRouter.Route(key, ModifierKeys.Alt, null, true, true));
            Assert.Equal(big, ShortcutRouter.Route(key, ModifierKeys.Alt | ModifierKeys.Shift, null, true, true));
        }
        Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Delete, ModifierKeys.None, null, true, true));
        Assert.Equal(ShortcutAction.DeleteLayer, ShortcutRouter.Route(Key.Back, ModifierKeys.None, null, true, true));
    }

    [Fact] // keys-selection-gate-and-modifiers: no selection, or Keys OFF, or Ctrl or the Windows key held: no layer key routes
    public void Layer_keys_need_a_selection_keys_on_and_no_ctrl_or_windows_key()
    {
        foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Delete, Key.Back })
        {
            Assert.Null(ShortcutRouter.Route(key, ModifierKeys.None, null, true, hasSelection: false));
            Assert.Null(ShortcutRouter.Route(key, ModifierKeys.None, null, false, true));
            foreach (var mods in new[] { ModifierKeys.Control, ModifierKeys.Control | ModifierKeys.Shift, ModifierKeys.Windows, ModifierKeys.Control | ModifierKeys.Alt })
                Assert.Null(ShortcutRouter.Route(key, mods, null, true, true));
        }
        foreach (var text in new[] { "[", "]", "{", "}", "r", "R", "+", "=", "-", "_" })
        {
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.None, null, true, hasSelection: false));
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.None, null, false, true));
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.Control, null, true, true));
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.Alt, null, true, true));
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.Windows, null, true, true));
        }
    }

    [Fact] // K2: the characters map as 4.1's switch: [ ] 5 degrees, 15 with Shift; { } 15; r R reset; + = grow; - _ shrink
    public void RouteText_maps_the_layer_characters()
    {
        ShortcutAction? Text(string t, ModifierKeys m = ModifierKeys.None) => ShortcutRouter.RouteText(t, m, null, true, true);
        Assert.Equal(ShortcutAction.RotateBack, Text("["));
        Assert.Equal(ShortcutAction.RotateForward, Text("]"));
        Assert.Equal(ShortcutAction.RotateBackBig, Text("{", ModifierKeys.Shift));
        Assert.Equal(ShortcutAction.RotateForwardBig, Text("}", ModifierKeys.Shift));
        Assert.Equal(ShortcutAction.RotateBackBig, Text("["  , ModifierKeys.Shift));
        Assert.Equal(ShortcutAction.RotateForwardBig, Text("]", ModifierKeys.Shift));
        Assert.Equal(ShortcutAction.RotateBackBig, Text("{"));         // typed with AltGr elsewhere: no Shift held, still the big step
        Assert.Equal(ShortcutAction.RotateForwardBig, Text("}"));
        Assert.Equal(ShortcutAction.ResetRotation, Text("r"));
        Assert.Equal(ShortcutAction.ResetRotation, Text("R", ModifierKeys.Shift));
        Assert.Equal(ShortcutAction.ScaleUp, Text("+", ModifierKeys.Shift));
        Assert.Equal(ShortcutAction.ScaleUp, Text("="));
        Assert.Equal(ShortcutAction.ScaleDown, Text("-"));
        Assert.Equal(ShortcutAction.ScaleDown, Text("_", ModifierKeys.Shift));
    }

    [Fact] // D9: AltGr arrives as Ctrl+Alt; a character typed with both held routes like its bare form (K2), so [ ] { } work on a German layout
    public void AltGr_characters_route_like_their_bare_form() => StaThread.Run(() =>
    {
        const ModifierKeys altGr = ModifierKeys.Control | ModifierKeys.Alt;
        foreach (var text in new[] { "[", "]", "{", "}", "+", "-", "=", "_", "r", "v", "b", "e" })
        {
            var bare = ShortcutRouter.RouteText(text, ModifierKeys.None, null, true, true);
            Assert.NotNull(bare);
            Assert.Equal(bare, ShortcutRouter.RouteText(text, altGr, null, true, true));
            Assert.Equal(ShortcutRouter.RouteText(text, ModifierKeys.Shift, null, true, true), ShortcutRouter.RouteText(text, altGr | ModifierKeys.Shift, null, true, true));
            // a real Ctrl chord (Ctrl without Alt) and a lone Alt still block
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.Control, null, true, true));
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.Control | ModifierKeys.Shift, null, true, true));
            Assert.Null(ShortcutRouter.RouteText(text, ModifierKeys.Alt, null, true, true));
        }
        // AltGr does not get past a text field, Keys OFF or an empty selection either
        Assert.Null(ShortcutRouter.RouteText("[", altGr, new TextBox(), true, true));
        Assert.Null(ShortcutRouter.RouteText("[", altGr, null, false, true));
        Assert.Null(ShortcutRouter.RouteText("[", altGr, null, true, false));
    });

    [Fact] // D9 through the window: the German [ (AltGr+8) rotates the layer; Ctrl+[ does not
    public void An_altgr_bracket_rotates_the_selected_layer_in_the_window() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(rotation: 0)));
        Assert.True(rig.Type("]", null, ModifierKeys.Control | ModifierKeys.Alt));
        Assert.Equal(5, Top(rig.Session).Rotation);
        Assert.True(rig.Type("}", null, ModifierKeys.Control | ModifierKeys.Alt));
        Assert.Equal(20, Top(rig.Session).Rotation);
        Assert.False(rig.Type("]", null, ModifierKeys.Control));
        Assert.False(rig.Type("\u001d", null, ModifierKeys.Control));   // Ctrl+] types U+001D
        Assert.Equal(20, Top(rig.Session).Rotation);
        Assert.Equal(2, rig.Session.UndoCount);
    });

    // ---- the window: each key does its work as one undo step

    [Theory] // done-when 1: 3 + ] = 8; 178 + ] = -177; -180 + [ = 175 (not -185); Shift variants { } step 15
    [InlineData(3, "]", ModifierKeys.None, 8)]
    [InlineData(178, "]", ModifierKeys.None, -177)]
    [InlineData(-180, "[", ModifierKeys.None, 175)]
    [InlineData(0, "{", ModifierKeys.Shift, -15)]
    [InlineData(0, "}", ModifierKeys.Shift, 15)]
    [InlineData(170, "}", ModifierKeys.Shift, -175)]
    [InlineData(-180, "{", ModifierKeys.Shift, 165)]
    [InlineData(10, "[", ModifierKeys.None, 5)]
    public void The_brackets_rotate_with_a_true_modulo(double from, string typed, ModifierKeys mods, double expected) => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(rotation: from)));
        Assert.True(rig.Type(typed, null, mods));
        Assert.Equal(expected, Top(rig.Session).Rotation);
        Assert.Equal(1, rig.Session.UndoCount);
        rig.Session.Undo();
        Assert.Equal(from, Top(rig.Session).Rotation);
    });

    [Fact] // done-when 1 (drift): from -180, [ three times is 175, 170, 165, never -185 and on
    public void Repeated_left_brackets_never_drift_below_minus_180() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(rotation: -180)));
        var seen = new List<double>();
        for (int i = 0; i < 80; i++)
        {
            rig.Type("[");
            seen.Add(Top(rig.Session).Rotation);
        }
        Assert.Equal(new double[] { 175, 170, 165 }, seen.Take(3));
        Assert.All(seen, r => Assert.InRange(r, -180, 179.999));
    });

    [Theory] // done-when 2: R and r reset rotation; already 0 records nothing
    [InlineData("r")]
    [InlineData("R")]
    public void R_resets_the_rotation(string typed) => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(rotation: 37)));
        Assert.True(rig.Type(typed, null, typed == "R" ? ModifierKeys.Shift : ModifierKeys.None));
        Assert.Equal(0, Top(rig.Session).Rotation);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.True(rig.Type(typed));                    // handled (4.1's preventDefault), a no-op
        Assert.Equal(1, rig.Session.UndoCount);
    });

    [Fact] // done-when 3: scale 1, 1.1, 1.21 about the top-left (x and y kept); + = grow, - _ shrink
    public void Plus_and_minus_scale_about_the_top_left() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(x: 40, y: 60, scale: 1)));
        rig.Type("+", null, ModifierKeys.Shift);
        Assert.Equal(1.1, Top(rig.Session).Scale, 12);
        rig.Type("=");
        var l = Top(rig.Session);
        Assert.Equal(1.21, l.Scale, 12);
        Assert.Equal((40.0, 60.0), (l.X, l.Y));
        rig.Type("-");
        Assert.Equal(1.089, Top(rig.Session).Scale, 12);
        rig.Type("_", null, ModifierKeys.Shift);
        Assert.Equal(0.9801, Top(rig.Session).Scale, 12);
        Assert.Equal(4, rig.Session.UndoCount);
    });

    [Theory] // done-when 3: 4.8 gives 5 (the cap); 0.0105 gives 0.01 (the floor)
    [InlineData(4.8, "+", 5.0)]
    [InlineData(0.0105, "-", 0.01)]
    public void Scale_keys_clamp(double from, string typed, double expected) => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(scale: from)));
        Assert.True(rig.Type(typed, null, typed == "+" ? ModifierKeys.Shift : ModifierKeys.None));
        Assert.Equal(expected, Top(rig.Session).Scale);
        Assert.True(rig.Type(typed, null, typed == "+" ? ModifierKeys.Shift : ModifierKeys.None));   // at the limit: handled, records nothing
        Assert.Equal(1, rig.Session.UndoCount);
    });

    [Fact] // done-when 4 and 5: ArrowRight x5 adds 5 to x, one undo step per press; Shift+Down adds 10 to y
    public void Arrow_keys_nudge_one_step_per_press() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(x: 100, y: 100)));
        for (int i = 1; i <= 5; i++)
        {
            Assert.True(rig.Down(Key.Right));
            Assert.Equal(100 + i, Top(rig.Session).X);
            Assert.Equal(i, rig.Session.UndoCount);
        }
        Assert.True(rig.Down(Key.Down, null, ModifierKeys.Shift));
        Assert.Equal((105.0, 110.0), (Top(rig.Session).X, Top(rig.Session).Y));
        Assert.True(rig.Down(Key.Left));
        Assert.True(rig.Down(Key.Up));
        Assert.Equal((104.0, 109.0), (Top(rig.Session).X, Top(rig.Session).Y));
        Assert.Equal(8, rig.Session.UndoCount);
        rig.Session.Undo();
        rig.Session.Undo();
        Assert.Equal((105.0, 110.0), (Top(rig.Session).X, Top(rig.Session).Y));
    });

    [Fact] // done-when 6: with nothing selected the arrows do nothing and are not handled; Ctrl+ArrowLeft does nothing either way
    public void Arrows_do_nothing_without_a_selection_or_with_ctrl() => StaThread.Run(() =>
    {
        using var none = new Rig(SessionWith(Pose()));
        var before = none.Session.Document;
        foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Delete, Key.Back })
            Assert.False(none.Down(key));
        foreach (var text in new[] { "[", "]", "r", "+", "-" })
            Assert.False(none.Type(text));
        Assert.Same(before, none.Session.Document);
        Assert.Equal(0, none.Session.UndoCount);

        using var selected = new Rig(Selected(Pose()));
        Assert.False(selected.Down(Key.Left, null, ModifierKeys.Control));
        Assert.False(selected.Down(Key.Delete, null, ModifierKeys.Control));
        Assert.Equal((100.0, 0), (Top(selected.Session).X, selected.Session.UndoCount));
    });

    [Fact] // Keys OFF blocks the bare layer keys, and the text field keeps its own
    public void Keys_off_and_text_fields_block_the_layer_keys() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose()));
        Assert.False(rig.Down(Key.Right, new TextBox()));
        Assert.False(rig.Type("]", new TextBox()));
        Assert.False(rig.Down(Key.Delete, new TextBox()));
        rig.Main.Home.Keys.ToggleCommand.Execute(null);
        Assert.False(rig.Down(Key.Right));
        Assert.False(rig.Type("]"));
        Assert.False(rig.Down(Key.Delete));
        Assert.Equal((100.0, 0.0, 0), (Top(rig.Session).X, Top(rig.Session).Rotation, rig.Session.UndoCount));
        Assert.Single(rig.Session.Document.Layers);
    });

    [Fact] // the focus rules through the window: the layer list keeps Up and Down, a viewport its arrows, a region tile Delete; a slider traps nothing
    public void The_window_leaves_the_keys_a_focused_control_keeps() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(), Pose(2)));
        var foci = Foci().ToDictionary(f => f.Name, f => f.Element);
        Assert.False(rig.Down(Key.Up, foci["layer list"]));
        Assert.False(rig.Down(Key.Down, foci["layer row"], ModifierKeys.None));
        Assert.False(rig.Down(Key.Left, foci["3D viewport"]));
        Assert.False(rig.Down(Key.Delete, foci["region tile"]));
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.False(rig.Down(Key.Left, foci["Slider"]));    // a slider steps with its arrows (look call (d))
        Assert.False(rig.Down(Key.Right, foci["ComboBox"])); // so does the font list (look call (e))
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.True(rig.Down(Key.Left, foci["CheckBox"]));   // a checkbox never traps (kbd-input-focus-trap)
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(99, Top(rig.Session).X);
    });

    [Fact] // look call (d): a focused slider keeps the four arrows, with or without Shift; Delete and the bracket keys still reach the layer
    public void A_focused_slider_keeps_its_arrows_and_nothing_else() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(), Pose(2)));
        var slider = new Slider();
        foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down })
        {
            Assert.False(rig.Down(key, slider));
            Assert.False(rig.Down(key, slider, ModifierKeys.Shift));
        }
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.True(rig.Type("]", slider));
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.True(rig.Down(Key.Delete, slider));
        Assert.Equal(2, rig.Session.UndoCount);
    });

    [Fact] // look call (e): a focused font list keeps typed characters (type-ahead) and the arrows; Delete still removes the selected layer
    public void A_focused_font_list_keeps_letters_and_arrows() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(), Pose(2)));
        rig.Main.Home.Tools.Set(Tool.Select);
        var list = new ComboBox();
        foreach (var text in new[] { "b", "B", "v", "l", "u", "o", "e", "[", "]", "+", "-", "r" })
            Assert.False(rig.Type(text, list));
        foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down })
            Assert.False(rig.Down(key, list));
        Assert.Equal(Tool.Select, rig.Tool);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.True(rig.Down(Key.Delete, list));
        Assert.Equal(1, rig.Session.UndoCount);
    });

    [Fact] // done-when 12: the inspector shows the nudged position, rotation and scale
    public void The_inspector_refreshes_after_a_key() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(x: 100, y: 100)));
        var panel = rig.Main.LayerProperties;
        Assert.Equal("100", panel.XText);
        rig.Down(Key.Right, null, ModifierKeys.Shift);
        rig.Down(Key.Down);
        Assert.Equal(("110", "101"), (panel.XText, panel.YText));
        rig.Type("]");
        Assert.Equal(5, panel.Rotation);
        rig.Type("+", null, ModifierKeys.Shift);
        Assert.Equal(110, panel.ScalePercent, 9);
    });

    [Fact] // done-when 7: Ctrl+D adds one layer at (+10, +10) above the original, selects it, in one step; with nothing selected it is handled and does nothing
    public void Ctrl_d_duplicates_the_selection() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(1, x: 30, y: 40), Pose(2, x: 100, y: 120)));
        rig.Session.Select(1);
        var duplicate = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.D && b.Modifiers == ModifierKeys.Control);
        rig.Window.FocusedElement = () => null;
        Assert.True(duplicate.Command.CanExecute(null));
        duplicate.Command.Execute(null);

        Assert.Equal(new[] { 1, 3, 2 }, rig.Session.Document.Layers.Select(l => l.Id));
        var copy = rig.Session.Document.Layers[1];
        Assert.Equal((40.0, 50.0, "Image 1 copy"), (copy.X, copy.Y, copy.Name));
        Assert.Equal(3, rig.Session.SelectedLayerId);
        Assert.Equal(1, rig.Session.UndoCount);
        rig.Session.Undo();
        Assert.Equal(2, rig.Session.Document.Layers.Count);
    });

    [Fact]
    public void Ctrl_d_with_nothing_selected_is_handled_and_does_nothing() => StaThread.Run(() =>
    {
        using var rig = new Rig(SessionWith(Pose()));
        var before = rig.Session.Document;
        int nextId = rig.Session.NextId;
        var duplicate = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.D && b.Modifiers == ModifierKeys.Control);
        rig.Window.FocusedElement = () => null;
        Assert.True(duplicate.Command.CanExecute(null));   // enabled, so the key is handled (4.1 blocked the browser's bookmark)
        duplicate.Command.Execute(null);
        Assert.Same(before, rig.Session.Document);
        Assert.Equal((0, nextId), (rig.Session.UndoCount, rig.Session.NextId));
        rig.Window.FocusedElement = () => new TextBox();    // a focused text field keeps Ctrl+D
        Assert.False(duplicate.Command.CanExecute(null));
    });

    [Fact] // done-when 8: Delete on the top layer moves the selection to the new top, and one undo restores it; Backspace does the same
    public void Delete_and_backspace_remove_the_selection_and_select_the_new_top() => StaThread.Run(() =>
    {
        foreach (var key in new[] { Key.Delete, Key.Back })
        {
            using var rig = new Rig(Selected(Pose(1), Pose(2), Pose(3)));
            Assert.True(rig.Down(key));
            Assert.Equal(new[] { 1, 2 }, rig.Session.Document.Layers.Select(l => l.Id));
            Assert.Equal(2, rig.Session.SelectedLayerId);
            Assert.Equal(1, rig.Session.UndoCount);
            rig.Session.Undo();
            Assert.Equal(new[] { 1, 2, 3 }, rig.Session.Document.Layers.Select(l => l.Id));
            Assert.Equal(2, rig.Session.SelectedLayerId);   // undo keeps a selection whose layer still exists (undo-restore-ui-side-effects)

            rig.Session.Select(1);                       // deleting a lower layer keeps the layers above
            Assert.True(rig.Down(key));
            Assert.Equal(new[] { 2, 3 }, rig.Session.Document.Layers.Select(l => l.Id));
            Assert.Equal(3, rig.Session.SelectedLayerId);
        }
    });

    [Fact] // deleting the only layer leaves nothing selected; the keys then do nothing
    public void Deleting_the_last_layer_selects_nothing() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose()));
        Assert.True(rig.Down(Key.Delete));
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Null(rig.Session.SelectedLayerId);
        Assert.False(rig.Down(Key.Delete));
        Assert.Equal(1, rig.Session.UndoCount);
    });

    // ---- Undo and Redo: the buttons, the matrix and the chords (keys-undo-redo, undo-buttons-shortcuts)

    static KeyBinding Chord(MainWindow window, Key key, ModifierKeys mods) =>
        window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == key && b.Modifiers == mods);

    static void Press(Rig rig, Key key, ModifierKeys mods)
    {
        var binding = Chord(rig.Window, key, mods);
        rig.Window.FocusedElement = () => null;
        if (binding.Command.CanExecute(null)) binding.Command.Execute(null);
    }

    [Fact] // done-when 9: launch both off; an edit turns Undo on; an undo turns Redo on; a new edit turns Redo off
    public void The_undo_redo_disabled_state_matrix_holds() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose()));
        var edit = rig.Main.Home.Edit;
        Assert.Equal((false, false), (edit.UndoCommand.CanExecute(null), edit.RedoCommand.CanExecute(null)));
        rig.Down(Key.Right);
        Assert.Equal((true, false), (edit.UndoCommand.CanExecute(null), edit.RedoCommand.CanExecute(null)));
        Press(rig, Key.Z, ModifierKeys.Control);
        Assert.Equal((false, true), (edit.UndoCommand.CanExecute(null), edit.RedoCommand.CanExecute(null)));
        rig.Down(Key.Left);
        Assert.Equal((true, false), (edit.UndoCommand.CanExecute(null), edit.RedoCommand.CanExecute(null)));
    });

    [Fact] // done-when 9: 31 edits followed by 31 undos apply 30 (the history holds 30); done-when 10 follows
    public void Thirty_one_edits_then_thirty_one_undos_apply_thirty() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(x: 100)));
        for (int i = 0; i < 31; i++) rig.Down(Key.Right);
        Assert.Equal(131, Top(rig.Session).X);
        int applied = 0;
        for (int i = 0; i < 31; i++)
        {
            int before = rig.Session.UndoCount;
            Press(rig, Key.Z, ModifierKeys.Control);
            if (rig.Session.UndoCount < before) applied++;
        }
        Assert.Equal(30, applied);
        Assert.Equal(101, Top(rig.Session).X);   // the first edit's result is as far back as the history goes
    });

    [Fact] // done-when 10: Ctrl+Y and Ctrl+Shift+Z redo, with the redo stack as the gate; Keys OFF does not reach them
    public void Ctrl_y_and_ctrl_shift_z_redo() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(x: 100)));
        foreach (var (key, mods) in new[] { (Key.Y, ModifierKeys.Control), (Key.Z, ModifierKeys.Control | ModifierKeys.Shift) })
        {
            rig.Down(Key.Right);
            rig.Down(Key.Right);
            double x = Top(rig.Session).X;
            Press(rig, Key.Z, ModifierKeys.Control);
            Press(rig, Key.Z, ModifierKeys.Control);
            Assert.Equal(x - 2, Top(rig.Session).X);
            Press(rig, key, mods);
            Assert.Equal(x - 1, Top(rig.Session).X);
            rig.Main.Home.Keys.ToggleCommand.Execute(null);   // the chords ignore Keys OFF (the fix on keys-undo-redo)
            Press(rig, key, mods);
            rig.Main.Home.Keys.ToggleCommand.Execute(null);
            Assert.Equal(x, Top(rig.Session).X);
            Press(rig, key, mods);                            // nothing left to redo: no-op
            Assert.Equal(x, Top(rig.Session).X);
        }
    });

    [Fact] // a focused text field keeps Ctrl+Y and Ctrl+Shift+Z, as it does Ctrl+Z
    public void A_text_field_keeps_the_redo_chords() => StaThread.Run(() =>
    {
        using var rig = new Rig(Selected(Pose(x: 100)));
        rig.Down(Key.Right);
        rig.Session.Undo();
        rig.Window.FocusedElement = () => new TextBox();
        foreach (var (key, mods) in new[] { (Key.Y, ModifierKeys.Control), (Key.Z, ModifierKeys.Control | ModifierKeys.Shift), (Key.D, ModifierKeys.Control) })
            Assert.False(Chord(rig.Window, key, mods).Command.CanExecute(null));
        Assert.Equal(100, Top(rig.Session).X);
    });
}
