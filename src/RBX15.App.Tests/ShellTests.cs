using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using RBX15.App.Controls;
using RBX15.App.Converters;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>The window, header, footer and layout skeleton (ledger shell-* rows).</summary>
[Collection("MainWindow")] // see ShellLookTests
public class ShellTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    [Fact] // shell-header-version-hardcoded (fix): the version comes from the assembly, without "+sha"
    public void The_app_version_is_5_0_0()
    {
        Assert.Equal("5.0.0", AppInfo.Version);
        Assert.Equal(new Version(5, 0, 0, 0), typeof(AppInfo).Assembly.GetName().Version);
    }

    [Fact] // shell-window-title (keep): main.js:8 and editor.html:6
    public void The_product_name_is_4_1s_window_title()
    {
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", AppInfo.ProductName);
        Assert.Contains("<title>RBX15 Classic Shirt and Pants Maker</title>", File.ReadAllText(Path.Combine(Root, "editor.html")));
    }

    [Fact] // shell-header (fix): editor.html:16-17, with the 626Labs mark in place of "ItsjustEste" (Este, 2026-10-17) and
           // the byline the version alone, major.minor; shell-footer (fix): 4.1's
           // editor.html:343 is unchanged, and v5 reads "Developed at 626Labs" (Este, 2026-10-14)
    public void Header_and_footer_text_match_4_1_and_the_footer_credit()
    {
        var vm = TestShell.Main(new EditorSession());
        var html = File.ReadAllText(Path.Combine(Root, "editor.html"));
        Assert.Contains("<h1>RBX15 Classic Shirt &amp; Pants Maker</h1>", html);
        Assert.Contains("<span>ItsjustEste | v4.1</span>", html);
        Assert.Contains("<footer>Developed at 626 Labs LLC</footer>", html);

        Assert.Equal(AppInfo.ProductName, vm.Title);
        Assert.Equal("RBX15 Classic Shirt & Pants Maker", vm.HeaderTitle);
        Assert.Equal("v5.0", vm.HeaderByline);
        Assert.Equal("Developed at 626Labs", vm.FooterText);
    }

    [Fact] // text-transform: uppercase (styles.css:153, :686)
    public void Upper_case_converter_matches_css_text_transform()
    {
        var c = new UpperCaseConverter();
        Assert.Equal("V5.0", c.Convert("v5.0", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("DEVELOPED AT 626LABS", c.Convert("Developed at 626Labs", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Null(c.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => c.ConvertBack("X", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Theory] // shell-responsive-breakpoint (keep): @media (max-width: 900px) { .panel { width: 200px } } (styles.css:694-697)
    [InlineData(1200, 280)]
    [InlineData(901, 280)]
    [InlineData(900.5, 280)]
    [InlineData(900, 200)]
    [InlineData(640, 200)]
    public void The_left_panel_narrows_at_900(double viewport, double panel)
    {
        Assert.Contains("@media (max-width: 900px) {\n  .panel { width: 200px; }", File.ReadAllText(Path.Combine(Root, "styles.css")).Replace("\r\n", "\n"));
        Assert.Equal(panel, ShellLayout.PanelWidthFor(viewport));
    }

    [Fact] // the window: title, theme, 4.1's size default, the skeleton's regions, four tabs
    public void The_main_window_builds_the_skeleton()
    {
        var store = new FakeSettingsStore();
        var facts = StaThread.Run(() =>
        {
            var w = new MainWindow(TestShell.Main(new EditorSession()), store, [Primary]);
            StaThread.Pump();
            string Text(string name) => ((TextBlock)w.FindName(name)).Text;
            var tabs = (StackPanel)w.FindName("RibbonTabs");
            return new
            {
                w.Title,
                Size = (w.Left, w.Top, w.Width, w.Height, w.WindowState),
                Min = (w.MinWidth, w.MinHeight),
                Header = Text("HeaderTitle"),
                Byline = Text("HeaderByline"),
                Mark = ((Image)w.FindName("HeaderMark")) is var m
                    ? (m.Source == w.FindResource("LabsMarkDuo"), m.Height, System.Windows.Automation.AutomationProperties.GetName(m))
                    : default,
                Footer = Text("FooterText"),
                FooterLook = ((TextBlock)w.FindName("FooterText")) is var f
                    ? (f.FontSize, ((System.Windows.Media.SolidColorBrush)f.Foreground).Color.ToString(), f.HorizontalAlignment)
                    : default,
                Tabs = tabs.Children.OfType<RadioButton>().Select(r => ((string)r.Content, r.IsChecked)).ToArray(),
                TabVisibility = tabs.Children.OfType<RadioButton>().Select(r => r.Visibility).ToArray(),
                Panel = ((ColumnDefinition)w.FindName("PanelColumn")).Width.Value,
                Regions = new[] { "RibbonPane", "LeftPanelContent", "CanvasArea" }.All(n => w.FindName(n) is not null),
                RightPanel = w.FindName("RightPanel"),
                Background = ((System.Windows.Media.SolidColorBrush)w.Background).Color.ToString(),
            };
        });
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", facts.Title);
        Assert.Equal((360.0, 120.0, 1200.0, 800.0, WindowState.Normal), facts.Size);
        Assert.Equal((640.0, 480.0), facts.Min);
        Assert.Equal("RBX15 Classic Shirt & Pants Maker", facts.Header);
        Assert.Equal("V5.0", facts.Byline);
        Assert.Equal((true, 18.0, "626Labs"), facts.Mark);
        Assert.Equal("DEVELOPED AT 626LABS", facts.Footer);
        Assert.Equal((10.0, "#FF5E6B7F", HorizontalAlignment.Center), facts.FooterLook);   // styles.css:678-689: 10px fg-muted, centered
        Assert.Equal([("Home", (bool?)true), ("Background", (bool?)false), ("Details", (bool?)false), ("Text", (bool?)false)], facts.Tabs);   // four tabs; a tab is Collapsed until its pane has a group, so Layout (Presets, M4b item 5), Details (M4b item 7) and Text (M4b item 9) all show (RibbonTabsTests)
        Assert.Equal([Visibility.Visible, Visibility.Visible, Visibility.Visible, Visibility.Visible], facts.TabVisibility);
        Assert.Equal(280, facts.Panel);
        Assert.True(facts.Regions);
        Assert.Null(facts.RightPanel);
        Assert.Equal("#FF0F1F31", facts.Background);
        Assert.Empty(store.Saves);
    }

    [Fact] // shell-window-size (fix): a saved placement is restored before the window shows, maximized included
    public void The_main_window_restores_a_saved_placement()
    {
        var store = new FakeSettingsStore(new WindowPlacement(100, 50, 900, 700, true));
        var (size, saved) = StaThread.Run(() =>
        {
            var w = new MainWindow(TestShell.Main(new EditorSession()), store, [Primary]);
            StaThread.Pump();
            return ((w.Left, w.Top, w.Width, w.Height, w.WindowState), w.CapturePlacement());
        });
        Assert.Equal((100.0, 50.0, 900.0, 700.0, WindowState.Maximized), size);
        Assert.True(saved.Maximized);
    }

    [Fact] // a normal window captures its own bounds
    public void A_normal_window_captures_its_bounds()
    {
        var saved = StaThread.Run(() =>
        {
            var w = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
            StaThread.Pump();
            w.Left = 10; w.Top = 20; w.Width = 1000; w.Height = 600;
            return w.CapturePlacement();
        });
        Assert.Equal(new WindowPlacement(10, 20, 1000, 600, false), saved);
    }

    [Fact] // StaThread loads the same dictionary App.xaml merges, so theme tests see the app's theme
    public void App_xaml_merges_the_theme_the_tests_load()
    {
        var app = XDocument.Load(Path.Combine(Root, "src", "RBX15.App", "App.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var merged = app.Descendants(p + "ResourceDictionary").Select(d => (string?)d.Attribute("Source")).OfType<string>().ToArray();
        Assert.Equal(["Themes/Controls.xaml"], merged);
        Assert.Equal(StaThread.Theme, "/RBX15.App;component/" + merged[0]);
        Assert.Null(app.Root!.Attribute("StartupUri"));
    }

    [Fact] // spec §4 "Display scaling: per-monitor DPI v2" (cross-window-dpi-rendering)
    public void The_app_manifest_declares_per_monitor_v2()
    {
        var manifest = XDocument.Load(Path.Combine(Root, "src", "RBX15.App", "app.manifest"));
        XNamespace ws = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        Assert.Equal("PerMonitorV2", manifest.Descendants(ws + "dpiAwareness").Single().Value);
    }

    [Fact] // M3b: Ctrl+Z is the one shortcut before M4 (planning contract); it runs the Edit group's Undo, no input sent
    public void Ctrl_Z_on_the_window_runs_the_Edit_groups_undo() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var binding = Assert.Single(window.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.Z && b.Modifiers == ModifierKeys.Control);
        // final fix wave 5: the binding's command is the Edit group's Undo behind the text-field guard
        Assert.Same(main.Home.Edit.UndoCommand, Assert.IsType<TextFieldShortcutCommand>(binding.Command).Inner);
    });

    /// <summary>What WPF's input binding does on its gesture: run the command iff it can execute (and only then handle the key).</summary>
    static bool Press(KeyBinding binding)
    {
        if (!binding.Command.CanExecute(binding.CommandParameter)) return false;
        binding.Command.Execute(binding.CommandParameter);
        return true;
    }

    [Fact] // final fix wave 5 (spec §4: text fields trap shortcuts): Ctrl+Z in a text box with nothing to undo never undoes the document
    public void Ctrl_Z_in_a_focused_text_box_never_falls_through_to_the_document() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var window = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var binding = window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.Z && b.Modifiers == ModifierKeys.Control);
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        var box = new TextBox();                                    // its own undo stack is empty
        Assert.False(box.CanUndo);

        window.FocusedElement = () => box;
        Assert.False(Press(binding));                               // not handled: the key stays the text box's
        Assert.Equal(1, session.UndoCount);
        window.FocusedElement = () => new RichTextBox();            // any TextBoxBase
        Assert.False(Press(binding));
        Assert.Equal(1, session.UndoCount);

        window.FocusedElement = () => null;                         // focus elsewhere: the window's Undo
        Assert.True(Press(binding));
        Assert.Equal(0, session.UndoCount);
        Assert.True(TextFieldShortcutCommand.TextFieldHasFocus(box));
        Assert.False(TextFieldShortcutCommand.TextFieldHasFocus(new Button()));
    });

    // ---- M4a Task 1: the three file chords and the close guard ----

    public static TheoryData<Key, ModifierKeys, string> FileChords => new()
    {
        { Key.S, ModifierKeys.Control, nameof(FileGroupViewModel.SaveCommand) },
        { Key.S, ModifierKeys.Control | ModifierKeys.Shift, nameof(FileGroupViewModel.SaveAsCommand) },
        { Key.O, ModifierKeys.Control, nameof(FileGroupViewModel.OpenCommand) },
    };

    [Theory, MemberData(nameof(FileChords))] // spec §4 "New: Ctrl+O, Ctrl+S, Ctrl+Shift+S"; each binding: its gesture, its inner command, the text-field guard
    public void A_file_chord_runs_its_file_group_command_unless_a_text_field_has_focus(Key key, ModifierKeys modifiers, string command) => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var binding = Assert.Single(window.InputBindings.OfType<KeyBinding>(), b => b.Key == key && b.Modifiers == modifiers);
        var guard = Assert.IsType<TextFieldShortcutCommand>(binding.Command);
        var expected = typeof(FileGroupViewModel).GetProperty(command)!.GetValue(main.Home.File);
        Assert.Same(expected, guard.Inner);

        window.FocusedElement = () => new TextBox();
        Assert.False(guard.CanExecute(null));                       // the key stays the text field's
        window.FocusedElement = () => null;
        Assert.True(guard.CanExecute(null));
    });

    [Fact] // the window binds exactly seven chords, in this order, each behind TextFieldShortcutCommand: Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z (M4b item 11),
           // Ctrl+D (Duplicate), and the three file chords (M4a). Ctrl+V stays unbound: paste is M5's, and Ctrl+V must not toggle a tool (K3)
    public void The_window_binds_exactly_the_seven_chords_and_not_ctrl_v() => StaThread.Run(() =>
    {
        var window = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var gestures = window.InputBindings.OfType<KeyBinding>().Select(b => (b.Key, b.Modifiers)).ToArray();
        Assert.Equal(new[]
        {
            (Key.Z, ModifierKeys.Control),
            (Key.Y, ModifierKeys.Control),
            (Key.Z, ModifierKeys.Control | ModifierKeys.Shift),
            (Key.D, ModifierKeys.Control),
            (Key.S, ModifierKeys.Control),
            (Key.S, ModifierKeys.Control | ModifierKeys.Shift),
            (Key.O, ModifierKeys.Control),
        }, gestures);
        Assert.Equal(7, window.InputBindings.Count);
        Assert.DoesNotContain(gestures, g => g.Key == Key.V);
        Assert.All(window.InputBindings.OfType<KeyBinding>(), b => Assert.IsType<TextFieldShortcutCommand>(b.Command));
    });

    /// <summary>A main window over fakes the test can reach, closed without ever being shown.</summary>
    static (MainWindow Window, EditorSession Session, FakeDialogService Dialogs, FakeFileDialogs Files, FakeSettingsStore Store) ClosableShell()
    {
        var session = new EditorSession();
        var dialogs = new FakeDialogService();
        var files = new FakeFileDialogs();
        var store = new FakeSettingsStore();
        var main = TestShell.Quiet(Composition.Build(session, files, dialogs, new FakeColorPicker(), store, TestShell.Storage(), new ManualTickSource()));
        var window = new MainWindow(main, store, [Primary]);
        StaThread.Pump();
        return (window, session, dialogs, files, store);
    }

    [Fact] // shell-no-unsaved-changes-guard parityCheck: closing with no edits shows no prompt and closes
    public void Closing_a_clean_document_shows_no_prompt() => StaThread.Run(() =>
    {
        var (window, _, dialogs, _, store) = ClosableShell();
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();
        Assert.True(closed);
        Assert.Empty(dialogs.SaveChanges);
        Assert.Single(store.Saves);                                  // the placement is saved on a real close
    });

    [Theory] // R13: closing dirty asks; Cancel, and Save with a cancelled Save As, keep the window open; Don't save closes
    [InlineData(SaveChoice.Cancel, false)]
    [InlineData(SaveChoice.DontSave, true)]
    [InlineData(SaveChoice.Save, false)]
    public void Closing_dirty_work_asks_save_dont_save_or_cancel(SaveChoice answer, bool closes) => StaThread.Run(() =>
    {
        var (window, session, dialogs, files, store) = ClosableShell();
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        dialogs.SaveChoiceAnswer = answer;
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();
        Assert.Equal(new[] { "Untitled" }, dialogs.SaveChanges);
        Assert.Equal(closes, closed);
        Assert.Equal(closes ? 1 : 0, store.Saves.Count);             // no placement save when the close was cancelled
        Assert.Equal(answer == SaveChoice.Save ? 1 : 0, files.SaveProjectCalls.Count);
        if (!closes) window.Close();                                 // the guard asks again on the next attempt
        if (!closes) Assert.Equal(2, dialogs.SaveChanges.Count);
    });

    [Fact] // final fix wave 6: each window's ribbon tabs are their own group; one window's tab never unchecks another's
    public void Ribbon_tabs_are_grouped_per_window() => StaThread.Run(() =>
    {
        var first = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
        var second = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var a = (RadioButton)first.FindName("HomeTab");
        var b = (RadioButton)second.FindName("HomeTab");
        Assert.Equal((true, true), (a.IsChecked, b.IsChecked));
        var select = (System.Windows.Automation.Provider.ISelectionItemProvider)System.Windows.Automation.Peers.UIElementAutomationPeer
            .CreatePeerForElement(a).GetPattern(System.Windows.Automation.Peers.PatternInterface.SelectionItem);
        select.Select();                                            // a click on the first window's tab
        Assert.Equal((true, true), (a.IsChecked, b.IsChecked));
    });
}
