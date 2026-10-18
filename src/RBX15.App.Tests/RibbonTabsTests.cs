using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The four ribbon tabs and tab switching (ledger ribbon-tab-switch, ribbon-tabs-switching). The Layout, Details and Text tabs
/// have no group yet, so the tests that exercise all four turn <see cref="MainWindow.ShowAllTabsForTest"/> on.
/// </summary>
[Collection("MainWindow")] // see ShellLookTests
public class RibbonTabsTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);
    static readonly RibbonTab[] Order = [RibbonTab.Home, RibbonTab.Layout, RibbonTab.Details, RibbonTab.Text];

    /// <summary>A main window over the fake shell, laid out but not shown; all four tabs on show unless asked otherwise.</summary>
    static MainWindow Built(EditorSession? session = null, bool showAll = true)
    {
        var w = new MainWindow(TestShell.Main(session ?? new EditorSession()), new FakeSettingsStore(), [Primary]) { ShowAllTabsForTest = showAll };
        StaThread.Pump();
        return w;
    }

    /// <summary>The same window shown far off-screen, so UI Automation and keyboard focus see a real window. The caller closes it.</summary>
    static MainWindow Shown()
    {
        var w = Built();
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
        w.ApplyPlacement(new WindowPlacement(-12000, -12000, 1200, 800, false));
        w.Show();
        StaThread.Pump();
        return w;
    }

    static RadioButton Tab(MainWindow w, RibbonTab tab) => (RadioButton)w.FindName(tab + "Tab");
    static UserControl Pane(MainWindow w, RibbonTab tab) => (UserControl)w.FindName(tab + "Pane");
    static RibbonTab[] Checked(MainWindow w) => Order.Where(t => Tab(w, t).IsChecked == true).ToArray();
    static RibbonTab[] Visible(MainWindow w) => Order.Where(t => Pane(w, t).Visibility == Visibility.Visible).ToArray();

    /// <summary>A click on the tab, as UI Automation (and so a screen reader or the bridge) sends it.</summary>
    static void Select(RadioButton tab) =>
        ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(tab).GetPattern(PatternInterface.SelectionItem)).Select();

    [Fact] // the tabs are Home, Layout, Details, Text in 4.1's order; the window starts on Home
    public void The_window_starts_on_Home_with_four_tabs_in_4_1s_order() => StaThread.Run(() =>
    {
        var w = Built();
        var tabs = ((StackPanel)w.FindName("RibbonTabs")).Children.OfType<RadioButton>().ToArray();
        Assert.Equal(["Home", "Layout", "Details", "Text"], tabs.Select(t => (string)t.Content));
        Assert.Equal(["Home tab", "Layout tab", "Details tab", "Text tab"], tabs.Select(AutomationProperties.GetName));
        Assert.Equal(RibbonTab.Home, w.ViewModel.ActiveTab);
        Assert.Equal([RibbonTab.Home], Checked(w));
        Assert.Equal([RibbonTab.Home], Visible(w));
    });

    [Fact] // clicking each tab makes exactly one tab checked and exactly one pane visible
    public void Clicking_each_tab_checks_that_tab_alone_and_shows_that_pane_alone() => StaThread.Run(() =>
    {
        var w = Built();
        foreach (var tab in new[] { RibbonTab.Layout, RibbonTab.Details, RibbonTab.Text, RibbonTab.Home, RibbonTab.Text, RibbonTab.Layout })
        {
            Select(Tab(w, tab));
            StaThread.Pump();
            Assert.Equal(tab, w.ViewModel.ActiveTab);
            Assert.Equal([tab], Checked(w));
            Assert.Equal([tab], Visible(w));
        }
    });

    [Fact] // the view-model drives the tabs too: setting ActiveTab checks the tab and shows the pane
    public void Setting_ActiveTab_in_the_view_model_moves_the_tab_and_the_pane() => StaThread.Run(() =>
    {
        var w = Built();
        w.ViewModel.ActiveTab = RibbonTab.Details;
        StaThread.Pump();
        Assert.Equal([RibbonTab.Details], Checked(w));
        Assert.Equal([RibbonTab.Details], Visible(w));
    });

    [Fact] // the active tab: cyan text, a navy-deep fill and the gradient underline; the others have none of the three
    public void The_active_tab_has_cyan_text_a_navy_deep_fill_and_the_underline() => StaThread.Run(() =>
    {
        var w = Built();
        var cyan = ((SolidColorBrush)w.FindResource("BrandCyanBrush")).Color;
        var deep = ((SolidColorBrush)w.FindResource("BrandNavyDeepBrush")).Color;
        var muted = ((SolidColorBrush)w.FindResource("Fg3Brush")).Color;
        foreach (var active in Order)
        {
            Select(Tab(w, active));
            StaThread.Pump();
            foreach (var t in Order)
            {
                var tab = Tab(w, t);
                tab.ApplyTemplate();
                var underline = (System.Windows.Shapes.Rectangle)tab.Template.FindName("Underline", tab);
                if (t == active)
                {
                    Assert.Equal(cyan, ((SolidColorBrush)tab.Foreground).Color);
                    Assert.Equal(deep, ((SolidColorBrush)tab.Background).Color);
                    Assert.Equal(Visibility.Visible, underline.Visibility);
                    Assert.Same(w.FindResource("BrandGradientBrush"), underline.Fill);
                }
                else
                {
                    Assert.Equal(muted, ((SolidColorBrush)tab.Foreground).Color);
                    Assert.Equal(Colors.Transparent, ((SolidColorBrush)tab.Background).Color);
                    Assert.Equal(Visibility.Collapsed, underline.Visibility);
                }
            }
        }
    });

    [Fact] // switching tabs is window state: the document, the undo history and the dirty flag do not move
    public void Switching_tabs_leaves_the_document_the_undo_count_and_the_dirty_flag_alone() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        session.Apply(d => d with { Layers = d.Layers.Add(HomeTestKit.LayerOf(session.TakeId())) });
        var w = Built(session);
        var (doc, undo, dirty, changes) = (session.Document, session.UndoCount, session.IsDirty, 0);
        session.Changed += (_, _) => changes++;
        foreach (var tab in new[] { RibbonTab.Layout, RibbonTab.Details, RibbonTab.Text, RibbonTab.Home })
        {
            Select(Tab(w, tab));
            StaThread.Pump();
        }
        Assert.Same(doc, session.Document);
        Assert.Equal((undo, dirty, 0), (session.UndoCount, session.IsDirty, changes));
        Assert.True(dirty);   // a dirty document stays dirty; a switch does not mark a clean one either (below)
        var clean = new EditorSession();
        var w2 = Built(clean);
        Select(Tab(w2, RibbonTab.Text));
        StaThread.Pump();
        Assert.Equal((0, false), (clean.UndoCount, clean.IsDirty));
    });

    [Fact] // two windows do not share tab state, whichever tab is clicked
    public void Two_windows_keep_their_own_active_tab() => StaThread.Run(() =>
    {
        var first = Built();
        var second = Built();
        Select(Tab(first, RibbonTab.Details));
        StaThread.Pump();
        Assert.Equal((RibbonTab.Details, RibbonTab.Home), (first.ViewModel.ActiveTab, second.ViewModel.ActiveTab));
        Assert.Equal([RibbonTab.Details], Checked(first));
        Assert.Equal([RibbonTab.Home], Checked(second));
        Select(Tab(second, RibbonTab.Text));
        StaThread.Pump();
        Assert.Equal([RibbonTab.Details], Checked(first));
        Assert.Equal([RibbonTab.Text], Checked(second));
        Assert.Equal([RibbonTab.Details], Visible(first));
        Assert.Equal([RibbonTab.Text], Visible(second));
    });

    [Fact] // the hide rule: a tab shows once its pane has a group. Layout has one since item 5 (Presets); Details and Text do not yet
    public void A_tab_is_collapsed_until_its_pane_has_a_group() => StaThread.Run(() =>
    {
        var w = Built(showAll: false);
        Assert.Equal([Visibility.Visible, Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed],
                     Order.Select(t => Tab(w, t).Visibility));
        Assert.Equal([true, true, false, false], Order.Select(w.IsTabShown));
        Assert.True(((IRibbonPane)Pane(w, RibbonTab.Layout)).HasGroups);
        Assert.False(((IRibbonPane)Pane(w, RibbonTab.Details)).HasGroups);
        w.ShowAllTabsForTest = true;
        Assert.All(Order, t => Assert.Equal(Visibility.Visible, Tab(w, t).Visibility));
        w.ShowAllTabsForTest = false;
        Assert.Equal(Visibility.Collapsed, Tab(w, RibbonTab.Text).Visibility);
    });

    [Fact] // the rule later items flip: adding one group to a pane's Groups panel is all it takes to make HasGroups true
    public void A_pane_reports_content_as_soon_as_it_has_a_group() => StaThread.Run(() =>
    {
        foreach (var pane in new IRibbonPane[] { new DetailsRibbon(), new TextRibbon() })   // Layout has its first group (Presets), so it is not empty any more
        {
            Assert.False(pane.HasGroups);
            ((StackPanel)((UserControl)pane).FindName("Groups")).Children.Add(new Border());
            Assert.True(pane.HasGroups);
        }
        Assert.True(((IRibbonPane)new HomeRibbon()).HasGroups);
        Assert.True(((IRibbonPane)new LayoutRibbon()).HasGroups);
    });

    [Fact] // each pane brings its own horizontal ScrollViewer and the 12,8 margin, as HomeRibbon has
    public void Each_pane_scrolls_horizontally_inside_a_12_8_margin() => StaThread.Run(() =>
    {
        foreach (var pane in new UserControl[] { new HomeRibbon(), new LayoutRibbon(), new DetailsRibbon(), new TextRibbon() })
        {
            var scroll = Assert.IsType<ScrollViewer>(pane.Content);
            Assert.Equal((ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled), (scroll.HorizontalScrollBarVisibility, scroll.VerticalScrollBarVisibility));
            Assert.Equal(new Thickness(12, 8, 12, 8), ((FrameworkElement)pane.FindName("Groups")).Margin);
        }
    });

    [Fact] // the four panes share one cell (R1) and the inactive ones are Hidden, not Collapsed, which keeps their size
    public void The_panes_share_one_cell_and_the_inactive_ones_are_Hidden() => StaThread.Run(() =>
    {
        var w = Built();
        var grid = (Grid)w.FindName("RibbonPane");
        Assert.Equal(Order.Select(t => Pane(w, t)), grid.Children.Cast<UIElement>());
        Assert.All(grid.Children.Cast<UserControl>(), p => Assert.Equal((0, 0), (Grid.GetRow(p), Grid.GetColumn(p))));
        Select(Tab(w, RibbonTab.Text));
        StaThread.Pump();
        Assert.Equal([Visibility.Hidden, Visibility.Hidden, Visibility.Hidden, Visibility.Visible], Order.Select(t => Pane(w, t).Visibility));
    });

    [Fact] // Risk: Hidden panes stay in the UIA tree; each tab is reachable by name and a hidden pane reports IsOffscreen
    public void Through_UI_Automation_the_tabs_are_found_by_name_and_hidden_panes_are_offscreen()
    {
        var w = StaThread.Run(Shown);
        try
        {
            var hwnd = StaThread.Run(() => new WindowInteropHelper(w).Handle);
            var root = AutomationElement.FromHandle(hwnd);
            foreach (var name in new[] { "Home tab", "Layout tab", "Details tab", "Text tab" })
            {
                var tab = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
                Assert.NotNull(tab);
                Assert.False(tab.Current.IsOffscreen);
            }
            var layoutTab = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Layout tab"));
            ((SelectionItemPattern)layoutTab.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            var offscreen = StaThread.Run(() =>
            {
                StaThread.Pump();
                return Order.ToDictionary(t => t, t => UIElementAutomationPeer.CreatePeerForElement(Pane(w, t)).IsOffscreen());
            });
            Assert.Equal(new Dictionary<RibbonTab, bool>
            {
                [RibbonTab.Home] = true, [RibbonTab.Layout] = false, [RibbonTab.Details] = true, [RibbonTab.Text] = true,
            }, offscreen);
        }
        finally
        {
            StaThread.Run(w.Close);
        }
    }

    [Fact] // Enter on a focused tab checks it, as Enter clicked 4.1's tab buttons; other keys do nothing
    public void Enter_on_a_focused_tab_switches_to_it() => StaThread.Run(() =>
    {
        var w = Shown();
        try
        {
            using var source = new HwndSource(new HwndSourceParameters("ribbon-tab-test"));
            void Press(RadioButton tab, Key key) =>
                tab.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
            Press(Tab(w, RibbonTab.Details), Key.Enter);
            StaThread.Pump();
            Assert.Equal([RibbonTab.Details], Checked(w));
            Assert.Equal([RibbonTab.Details], Visible(w));
            Press(Tab(w, RibbonTab.Text), Key.Enter);
            Assert.Equal(RibbonTab.Text, w.ViewModel.ActiveTab);
            Press(Tab(w, RibbonTab.Home), Key.A);
            Assert.Equal(RibbonTab.Text, w.ViewModel.ActiveTab);
        }
        finally
        {
            w.Close();
        }
    });

    [Fact] // every tab is its own Tab stop (not only the checked one) and the arrows do not walk between tabs (K1)
    public void Each_tab_is_a_tab_stop_and_the_arrows_are_not_tab_navigation() => StaThread.Run(() =>
    {
        var w = Shown();
        try
        {
            var strip = (StackPanel)w.FindName("RibbonTabs");
            Assert.Equal(KeyboardNavigationMode.Continue, KeyboardNavigation.GetTabNavigation(strip));
            Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetDirectionalNavigation(strip));
            foreach (var t in Order)
            {
                Assert.True(Tab(w, t).Focusable);
                Assert.True(Tab(w, t).IsTabStop);
            }
            Assert.True(Tab(w, RibbonTab.Home).Focus());
            Assert.True(Tab(w, RibbonTab.Home).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.Same(Tab(w, RibbonTab.Layout), Keyboard.FocusedElement);   // Home is checked, and Tab still reaches Layout
        }
        finally
        {
            w.Close();
        }
    });
}
