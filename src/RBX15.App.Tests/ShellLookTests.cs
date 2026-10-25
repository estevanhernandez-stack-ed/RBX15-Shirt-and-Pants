using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;

namespace RBX15.App.Tests;

/// <summary>
/// The look notes from the M3a smoke runs (.superpowers/m3a-smoke/RESULTS.md "Look notes", RESULTS-themed.md),
/// each checked against 4.1 laid out at the same size. The 4.1 numbers were measured once while planning M3b: a
/// scratch Electron script loaded the shipped editor.html in a hidden BrowserWindow (show: false, CPU raster, device
/// scale 1, the vendored fonts, no input of any kind) at a content size of 1184x761, the client area of the
/// 1200x800 window (the M3a smoke's UIA tree: 47 header + 28 tabs + 122 ribbon pane + 536 canvas area + 28 footer),
/// and at 624x441, the client area of the 640x480 minimum.
/// </summary>
[Collection("MainWindow")] // MainWindow-building tests run one at a time; the tabs once shared a process-wide RadioButton group (fixed, ShellTests.Ribbon_tabs_are_grouped_per_window), kept as a guard
public class ShellLookTests
{
    const double ClientWidth = 1184, ClientHeight = 761;

    /// <summary>4.1's Home pane (.ribbon-pane.active) scrollWidth in a 1184 client: Home overflows in 4.1 too.</summary>
    const double Home41ScrollWidth = 1432;

    /// <summary>4.1's .canvas-area height at 1184x761 on the Layout tab, the tab that holds Fill Style and Template Regions.</summary>
    const double Layout41CanvasArea = 531;

    /// <summary>4.1's .ribbon height on that tab.</summary>
    const double Layout41Ribbon = 155;

    /// <summary>
    /// R1 (M4b item 12): the ribbon keeps one height, the tallest pane's, where 4.1's changed per tab. The Text pane (138 DIP) is the tallest,
    /// so the ribbon is 12 DIP above 4.1's Layout ribbon (155) and the canvas area 12 DIP below its 531. RibbonMeasureTests pins the same height.
    /// </summary>
    const double RibbonGrowth = RibbonMeasureTests.RibbonHeight - Layout41Ribbon;

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>The main window's content laid out off-screen at a client size, the panel column set as the window would.</summary>
    static MainWindow Shell(double width, double height)
    {
        var w = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        ((ColumnDefinition)w.FindName("PanelColumn")).Width = new GridLength(ShellLayout.PanelWidthFor(width));
        var root = (FrameworkElement)w.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        return w;
    }

    [Fact] // look note 1 (ribbon-overflow-hscroll, keep): 4.1's Home is 1432 wide in a 1184 client, so it scrolls too
    public void At_the_default_size_the_ribbon_scrolls_sideways_and_no_group_shrinks() => StaThread.Run(() =>
    {
        // A pinned ruling, not a measurement in this test: it records 4.1's measured 1432 against the 1184 client (T4-R8).
        Assert.True(Home41ScrollWidth > ClientWidth);
        var w = Shell(ClientWidth, ClientHeight);
        var home = Descendants<HomeRibbon>((DependencyObject)w.Content).Single();
        var scroller = Descendants<ScrollViewer>(home).First();
        Assert.Equal(ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);
        Assert.True(scroller.ExtentWidth > scroller.ViewportWidth);
        Assert.Equal(Visibility.Visible, scroller.ComputedHorizontalScrollBarVisibility);
        var groups = (StackPanel)home.FindName("Groups");
        Assert.All(groups.Children.OfType<FrameworkElement>(), g => Assert.Equal(g.DesiredSize.Width - g.Margin.Left - g.Margin.Right, g.ActualWidth, 1));
    });

    [Fact] // look note 2 + R1: the ribbon is the tallest pane's (167), so the canvas area is 4.1's Layout-tab area less exactly that growth
    public void At_the_default_size_the_canvas_area_is_4_1s_less_the_fixed_ribbons_growth() => StaThread.Run(() =>
    {
        var w = Shell(ClientWidth, ClientHeight);
        var ribbonElement = (FrameworkElement)w.FindName("Ribbon");
        double ribbon = ribbonElement.ActualHeight;
        double area = ((FrameworkElement)w.FindName("CanvasArea")).ActualHeight;
        // 4.1's numbers were measured at 100%. Above it the shell's text measures in device-pixel steps (at 125% the ribbon is 165.6
        // and the rest of the chrome 73.8, not 75), so there is nothing to compare against (MAN-8, 2026-10-25).
        double display = VisualTreeHelper.GetDpi(ribbonElement).PixelsPerInchX;
        if (display != 96)
        {
            Console.WriteLine($"SKIP: 4.1's canvas area was measured at 100%; this display is at {display} DPI.");
            return;
        }
        Assert.Equal(Layout41Ribbon + RibbonGrowth, ribbon);
        Assert.Equal(Layout41CanvasArea - RibbonGrowth, area);
    });

    [Theory] // look note 4: 4.1's div wraps its text; measured 251x84 in the 280 panel and 161x100 (three lines) in the 200 panel
    [InlineData(251, 84)]
    [InlineData(161, 100)]
    public void The_drop_zone_label_wraps_like_4_1s(double width, double height41) => StaThread.Run(() =>
    {
        var zone = new DropZone { DataContext = new LayersViewModel(new EditorSession(), new ImportService(new EditorSession(), new FakeStatus()), new FakeFileDialogs()) };
        StaThread.Pump();
        zone.Measure(new Size(width, 2000));
        zone.Arrange(new Rect(0, 0, width, 2000));
        zone.UpdateLayout();
        var button = (Button)zone.FindName("Zone");
        var label = (TextBlock)button.Template.FindName("Label", button);
        Assert.Equal(TextWrapping.Wrap, label.TextWrapping);
        Assert.InRange(label.DesiredSize.Width - label.Margin.Left - label.Margin.Right, 0, width - 28 + 0.01);   // never wider than the box: no clipped words
        Assert.InRange(button.ActualHeight, height41 - 2, height41 + 2);
    });

    [Fact] // look note 3: the untitled 52x26 and 162x26 windows are the keyboard-focus tooltips of Color 1 and Add image
    public void The_lingering_windows_are_the_persistent_keyboard_focus_tooltips() => StaThread.Run(() =>
    {
        // The smoke saw them after Esc returned focus to Color 1 and after the Open dialog returned focus to Add image.
        // WPF shows a tooltip on keyboard focus (ShowsToolTipOnKeyboardFocus unset) and keeps it until focus moves,
        // Esc or Ctrl (ShowDuration int.MaxValue): WCAG 1.4.13's persistent, dismissible content. This fact mostly pins
        // WPF's defaults (the ToolTip's size, ShowDuration) as the identification of the smoke's windows: a pinned ruling (T4-R10).
        foreach (var (text, size) in new[] { ("Color 1", (52.0, 26.0)), ("Add an image file as a layer.", (162.0, 26.0)) })
        {
            var tip = new ToolTip { Content = text };
            tip.ApplyTemplate();
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(size, (Math.Round(tip.DesiredSize.Width), Math.Round(tip.DesiredSize.Height)));
        }
        var button = new Button();
        Assert.Null(ToolTipService.GetShowsToolTipOnKeyboardFocus(button));
        Assert.Equal(int.MaxValue, ToolTipService.GetShowDuration(button));
        var session = new EditorSession();
        var status = new FakeStatus();
        var file = new FileGroup { DataContext = HomeTestKit.FileGroup(session, new FakeFileDialogs(), new FakeDialogService(), status) };
        StaThread.Pump();
        file.Measure(new Size(400, 200));
        file.Arrange(new Rect(0, 0, 400, 200));
        Assert.Contains(Descendants<Button>(file), b => Equals(b.ToolTip, "Add an image file as a layer."));
    });
}
