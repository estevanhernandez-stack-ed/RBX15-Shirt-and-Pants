using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using RBX15.App.Controls;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>CanvasView wires the host and the zoom pill to CanvasViewModel; its theme keys exist; the app is per-monitor DPI v2.</summary>
public class CanvasViewTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static string AppFile(params string[] parts) => Path.Combine([Root, "src", "RBX15.App", .. parts]);

    /// <summary>
    /// Bindings in a parsed view attach at DataBind priority, so the test runs the queue once
    /// (StaThread.Pump, to Background, below DataBind) before reading bound values.
    /// </summary>
    static CanvasView Load(CanvasViewModel vm)
    {
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        return view;
    }

    [Fact]
    public void The_host_and_the_pill_follow_the_view_model() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = new CanvasViewModel(session);
        var view = Load(vm);
        var host = (TemplateCanvasHost)view.FindName("Host");
        var label = (TextBlock)view.FindName("ZoomLabel");
        var zoomIn = (Button)view.FindName("ZoomInButton");
        var zoomOut = (Button)view.FindName("ZoomOutButton");

        Assert.Same(session.Document, host.Document);
        Assert.Equal(1, host.Zoom);
        Assert.Same(vm.DropCommand, host.DropCommand);
        Assert.Same(vm.ClickCommand, host.ClickCommand);
        Assert.Equal("100%", label.Text);
        Assert.Equal(("-", "+"), ((string)zoomOut.Content, (string)zoomIn.Content)); // editor.html:333-335
        Assert.Equal(("Zoom out", "Zoom in"), (AutomationProperties.GetName(zoomOut), AutomationProperties.GetName(zoomIn)));
        Assert.Equal("Template canvas", AutomationProperties.GetName(host));

        zoomIn.Command.Execute(null);
        Assert.Equal(1.25, host.Zoom);
        Assert.Equal("125%", label.Text);

        session.Apply(d => d with { ShowGrid = true });
        Assert.Same(session.Document, host.Document);

        while (zoomOut.Command.CanExecute(null)) zoomOut.Command.Execute(null);
        Assert.False(zoomOut.IsEnabled);
        Assert.Equal(0.25, host.Zoom);
    });

    [Fact] // canvas-zoom-clipped-no-pan: scrollbars appear only when the frame outgrows the area
    public void The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits() => StaThread.Run(() =>
    {
        var vm = new CanvasViewModel(new EditorSession());
        var view = Load(vm);
        var host = (TemplateCanvasHost)view.FindName("Host");
        host.ApplyDpi(1);
        var scroller = (ScrollViewer)view.FindName("Scroller");

        view.Measure(new Size(901, 701));
        view.Arrange(new Rect(0, 0, 901, 701));
        Assert.Equal(0, scroller.ScrollableWidth);
        Assert.Equal(0, scroller.ScrollableHeight);
        // The 587x561 wrap (585x559 and its 1px border) is centered, so the frame starts 1px inside it.
        Assert.Equal(new Point((901 - 587) / 2 + 1, (701 - 561) / 2 + 1), host.TranslatePoint(new Point(0, 0), view));

        for (int i = 0; i < 8; i++) vm.ZoomInCommand.Execute(null); // 300%: 1755x1677
        view.UpdateLayout(); // the host invalidated its measure; run the queued layout pass
        Assert.True(scroller.ScrollableWidth > 0);
        Assert.True(scroller.ScrollableHeight > 0);
    });

    [Fact] // every theme key the view names exists in T1's dictionaries (a missing DynamicResource fails silently)
    public void Every_theme_key_the_view_uses_is_defined_in_the_theme()
    {
        var view = File.ReadAllText(AppFile("Views", "CanvasView.xaml"));
        var theme = string.Concat(Directory.EnumerateFiles(AppFile("Themes"), "*.xaml").Select(File.ReadAllText));
        var keys = Regex.Matches(view, @"\{DynamicResource (\w+)\}").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.Contains($"x:Key=\"{k}\"", theme));
    }

    [Fact] // spec §4 display scaling; cross-window-dpi-rendering. Without it WPF never raises OnDpiChanged.
    public void The_app_is_per_monitor_dpi_v2()
    {
        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", File.ReadAllText(AppFile("RBX15.App.csproj")));
        var manifest = File.ReadAllText(AppFile("app.manifest"));
        Assert.Contains(">PerMonitorV2</dpiAwareness>", manifest);
        Assert.Contains(">true/pm</dpiAware>", manifest);
    }

    [Fact] // the CanvasArea grid shows the canvas (Task 1's skeleton, filled here)
    public void The_main_window_hosts_the_canvas_view_on_MainViewModel_Canvas()
    {
        var xaml = File.ReadAllText(AppFile("MainWindow.xaml"));
        Assert.Contains("xmlns:views=\"clr-namespace:RBX15.App.Views\"", xaml);
        Assert.Matches(@"<views:CanvasView\s+DataContext=""\{Binding Canvas\}""\s*/>", xaml);
    }
}
