using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// M4b item 12 (ledger shell-layout-skeleton, shell-responsive-breakpoint, ribbon-group-structure): the ribbon at the default
/// 1200x800 window (1184x761 client) with a layer selected and the preview column shown. R1: one fixed height, the tallest pane's,
/// the same on every tab. The numbers are written to .superpowers/m4b-look/ribbon-measure.json (gitignored) for the look page.
/// </summary>
[Collection("MainWindow")] // see ShellLookTests
public class RibbonMeasureTests
{
    const double ClientWidth = 1184, ClientHeight = 761;
    /// <summary>R1's pinned ribbon: the Text pane (138 DIP, the 80 DIP preview box over Add to canvas) is the tallest, so the pane is 138, the tab strip 28 and the border 1.</summary>
    internal const double FixedPaneHeight = 138, RibbonHeight = 167;

    static readonly RibbonTab[] Order = [RibbonTab.Home, RibbonTab.Layout, RibbonTab.Details, RibbonTab.Text];

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>The window laid out off-screen at a client width, with layer 1 selected and the preview column shown.</summary>
    static MainWindow Shell(double width = ClientWidth, double height = ClientHeight, bool selected = true)
    {
        var session = HomeTestKit.SessionWith(HomeTestKit.LayerOf(1));
        if (selected) session.Select(1);
        var w = new MainWindow(TestShell.Main(session), new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]) { ShowAllTabsForTest = true };
        StaThread.Pump();
        w.ViewModel.AvatarPreview.IsVisible = true;
        w.ApplyColumnWidths(width);
        var root = (FrameworkElement)w.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        return w;
    }

    static UserControl Pane(MainWindow w, RibbonTab tab) => (UserControl)w.FindName(tab + "Pane");

    /// <summary>A pane's content size: its Groups row, whose DesiredSize already carries the pane's 12,8 margin (what 4.1's scrollWidth and padding box measure).</summary>
    static (double Width, double Height) Content(UserControl pane)
    {
        var groups = (StackPanel)pane.FindName("Groups");
        return (groups.DesiredSize.Width, groups.DesiredSize.Height);
    }

    /// <summary>The group labels of a pane, left to right: the RibbonGroupLabel TextBlocks, which are the all-caps ones docked in a group.</summary>
    static string[] Labels(UserControl pane) => Descendants<TextBlock>(pane)
        .Where(t => t.Style is not null && t.Style == pane.TryFindResource("RibbonGroupLabel") && t.Visibility == Visibility.Visible)
        .OrderBy(t => t.TranslatePoint(new Point(0, 0), pane).X)
        .Select(t => t.Text)
        .ToArray();

    [Fact] // R1 + shell-layout-skeleton: one ribbon height on every tab; Details and Text fit 1200; Home and Layout scroll, as both did in 4.1
    public void The_ribbon_has_one_height_and_Home_and_Layout_scroll_at_1200_as_in_4_1() => StaThread.Run(() =>
    {
        var w = Shell();
        var ribbon = (FrameworkElement)w.FindName("Ribbon");
        var paneHost = (FrameworkElement)w.FindName("RibbonPane");
        double fixedHeight = paneHost.ActualHeight;
        var panes = Order.ToDictionary(t => t, t => Pane(w, t));
        var measured = Order.ToDictionary(t => t, t => Content(panes[t]));
        var paneHeights = Order.ToDictionary(t => t, t => panes[t].DesiredSize.Height);   // Home's includes its horizontal scrollbar

        var heights = new Dictionary<string, double>();
        foreach (var tab in Order)
        {
            w.ViewModel.ActiveTab = tab;
            StaThread.Pump();
            ((FrameworkElement)w.Content).UpdateLayout();
            heights[tab.ToString()] = ((FrameworkElement)w.FindName("RibbonPane")).ActualHeight;
            Assert.Equal(fixedHeight, ((FrameworkElement)w.FindName("RibbonPane")).ActualHeight);
            Assert.Equal(ribbon.ActualHeight, ((FrameworkElement)w.FindName("Ribbon")).ActualHeight);
        }

        double tallest = paneHeights.Values.Max();
        double homeGrowth = fixedHeight - paneHeights[RibbonTab.Home];
        var json = new
        {
            client = new { width = ClientWidth, height = ClientHeight },
            ribbonPaneWidth = paneHost.ActualWidth,
            fixedPaneHeight = fixedHeight,
            ribbonHeight = ribbon.ActualHeight,
            heightByTab = heights,
            homeGrowthOverItsRenderedPane = homeGrowth,
            homeGrowthOverItsContent = fixedHeight - measured[RibbonTab.Home].Height,
            tabs = Order.ToDictionary(t => t.ToString(), t => new
            {
                contentWidth = measured[t].Width,
                contentHeight = measured[t].Height,
                paneDesiredHeight = paneHeights[t],
                fits = measured[t].Width <= paneHost.ActualWidth,
            }),
        };
        string dir = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), ".superpowers", "m4b-look");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ribbon-measure.json"), JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(1184, paneHost.ActualWidth);
        Assert.Equal(tallest, fixedHeight, 1);                 // the fixed height is the tallest pane's, nothing taller
        Assert.Equal(RibbonTab.Text, paneHeights.MaxBy(kv => kv.Value).Key);
        // The pinned numbers are 100%'s. Above it text measures in device-pixel steps (at 125%: 137.6 and 165.6), so the pin holds
        // only at 96 DPI; one height on every tab, the tallest pane's, is checked at every scale (MAN-8, 2026-10-25).
        if (VisualTreeHelper.GetDpi(ribbon).PixelsPerInchX == 96)
            Assert.Equal((FixedPaneHeight, RibbonHeight), (fixedHeight, ribbon.ActualHeight));
        foreach (var tab in new[] { RibbonTab.Details, RibbonTab.Text })
            Assert.True(measured[tab].Width <= paneHost.ActualWidth, $"{tab} needs {measured[tab].Width} DIP; the ribbon is {paneHost.ActualWidth}");

        // Home (about 1455 DIP; 4.1's needed 1432) and Layout, with Fill Style and the region grid back on it (Este, 2026-10-25, reversing F1;
        // about 1460), are wider than a 1184 client, and both scrolled there in 4.1 too: the look page's 4.1 captures at 1184 show a 10 px
        // scrollbar on each (ribbon-overflow-hscroll, keep; ShellLookTests look note 1).
        foreach (var tab in new[] { RibbonTab.Home, RibbonTab.Layout })
        {
            Assert.True(measured[tab].Width > paneHost.ActualWidth, $"{tab} needs {measured[tab].Width} DIP");
            var scroller = Descendants<ScrollViewer>(panes[tab]).First();
            Assert.Equal(Visibility.Visible, scroller.ComputedHorizontalScrollBarVisibility);
        }
    });

    [Fact] // ribbon-group-structure: v5's group labels per tab, left to right; Fill Style and the regions end the Layout (Background) tab, as in 4.1
    public void Each_tab_carries_its_groups_labels() => StaThread.Run(() =>
    {
        var w = Shell();
        Assert.Equal(["MODE", "TOOLS", "BRUSH", "PLACEMENT", "EDIT", "FILE", "SETTINGS"], Labels(Pane(w, RibbonTab.Home)));
        Assert.Equal(["PRESETS", "BACKGROUND", "BG SETTINGS", "FILL STYLE", "CLICK REGIONS TO FILL"], Labels(Pane(w, RibbonTab.Layout)));
        Assert.Equal(["SETTINGS", "NECKLINE", "EDGES", "SEAMS", "ACCENTS"], Labels(Pane(w, RibbonTab.Details)));
        Assert.Equal(["TEXT", "COLORS & SIZE", "STYLE", "PREVIEW"], Labels(Pane(w, RibbonTab.Text)));
    });

    [Theory] // shell-responsive-breakpoint: 901 keeps 280, 900 narrows to 200; the asset grid stays three to a row either side
    [InlineData(901, 280)]
    [InlineData(900, 200)]
    public void The_900_901_breakpoint_keeps_the_panel_width_and_three_asset_columns(double width, double panel) => StaThread.Run(() =>
    {
        var w = Shell(width, ClientHeight);
        Assert.Equal(panel, ((ColumnDefinition)w.FindName("PanelColumn")).ActualWidth);
        w.ViewModel.Assets.LoadDripsCommand.Execute(null);
        StaThread.Pump();
        for (int pass = 0; pass < 2; pass++)   // tiles made by the first pass are laid out by the second
        {
            ((FrameworkElement)w.Content).Measure(new Size(width, ClientHeight));
            ((FrameworkElement)w.Content).Arrange(new Rect(0, 0, width, ClientHeight));
            ((FrameworkElement)w.Content).UpdateLayout();
        }
        var root = (UIElement)w.Content;
        var tiles = Descendants<Button>(root).Where(b => b.Name == "AssetTile").OrderBy(b => b.TranslatePoint(new Point(0, 0), root).Y).ThenBy(b => b.TranslatePoint(new Point(0, 0), root).X).ToList();
        Assert.True(tiles.Count >= 4, $"{tiles.Count} tiles, {w.ViewModel.Assets.Rows.Count} rows");
        var ys = tiles.Select(t => Math.Round(t.TranslatePoint(new Point(0, 0), root).Y, 2)).ToList();
        Assert.Equal(3, ys.Count(y => y == ys[0]));
        Assert.True(ys[3] > ys[0]);
    });
}
