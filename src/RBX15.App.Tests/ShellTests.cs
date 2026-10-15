using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using RBX15.App.Converters;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>The window, header, footer and layout skeleton (ledger shell-* rows).</summary>
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

    [Fact] // shell-header, shell-footer: editor.html:16-17, :343 verbatim; the byline's version is major.minor
    public void Header_and_footer_text_match_4_1()
    {
        var vm = TestShell.Main(new EditorSession());
        var html = File.ReadAllText(Path.Combine(Root, "editor.html"));
        Assert.Contains("<h1>RBX15 Classic Shirt &amp; Pants Maker</h1>", html);
        Assert.Contains("<span>ItsjustEste | v4.1</span>", html);
        Assert.Contains("<footer>Developed at 626 Labs LLC</footer>", html);

        Assert.Equal(AppInfo.ProductName, vm.Title);
        Assert.Equal("RBX15 Classic Shirt & Pants Maker", vm.HeaderTitle);
        Assert.Equal("ItsjustEste | v5.0", vm.HeaderByline);
        Assert.Equal("Developed at 626 Labs LLC", vm.FooterText);
    }

    [Fact] // text-transform: uppercase (styles.css:153, :686)
    public void Upper_case_converter_matches_css_text_transform()
    {
        var c = new UpperCaseConverter();
        Assert.Equal("ITSJUSTESTE | V5.0", c.Convert("ItsjustEste | v5.0", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("DEVELOPED AT 626 LABS LLC", c.Convert("Developed at 626 Labs LLC", typeof(string), null, CultureInfo.InvariantCulture));
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

    [Fact] // the window: title, theme, 4.1's size default, the skeleton's regions, one Home tab
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
                Footer = Text("FooterText"),
                Tabs = tabs.Children.OfType<RadioButton>().Select(r => ((string)r.Content, r.IsChecked)).ToArray(),
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
        Assert.Equal("ITSJUSTESTE | V5.0", facts.Byline);
        Assert.Equal("DEVELOPED AT 626 LABS LLC", facts.Footer);
        Assert.Equal([("Home", (bool?)true)], facts.Tabs);
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
}
