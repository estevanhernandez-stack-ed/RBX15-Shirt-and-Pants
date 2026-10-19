using System.Collections.Immutable;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Placement group on Home (ledger home-placement-group, placement-selection-guard): five buttons in two rows that act on
/// the selected layer, each as one undo step, and a group that is collapsed until a layer is selected. The geometry itself is
/// the engine's (PlacementTests); these facts pin the wiring, the undo step, the guard and the view.
/// </summary>
[Collection("MainWindow")] // the ribbon-height fact builds a MainWindow, as ShellLookTests does
public class PlacementGroupTests
{
    /// <summary>A session whose document holds one w x h layer (id 1, named "Image 1"), selected, with no history.</summary>
    internal static EditorSession Selected(int w = 200, int h = 100, TemplateMode mode = TemplateMode.Shirt)
    {
        var session = new EditorSession(new Document { Mode = mode, Layers = ImmutableList.Create(LayerOf(1, w, h)) });
        session.Select(1);
        return session;
    }

    static Layer Of(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    static (double Scale, double X, double Y, double Rotation) Pose(Layer l) => (l.Scale, l.X, l.Y, l.Rotation);

    static void Near(double expected, double actual, double tol = 1e-9) => Assert.InRange(actual, expected - tol, expected + tol);

    static PlacementViewModel Placement(EditorSession session, Func<Random>? rng = null) => new(session, rng ?? (() => new Random(7)));

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

    /// <summary>A click as UI Automation sends it; the peer queues the click on the dispatcher, so the queue runs before this returns.</summary>
    static void Invoke(Button b)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
    }

    static (string Name, RelayCommand Command)[] Commands(PlacementViewModel p) =>
    [
        ("FitFront", p.FitFrontCommand), ("FrontBack", p.FrontBackCommand), ("Stretch", p.StretchCommand),
        ("Allover", p.AlloverCommand), ("Scatter", p.ScatterCommand),
    ];

    [Fact] // placement-selection-guard parityCheck: click each with no selection, no history entry, no layer change
    public void With_nothing_selected_no_command_records_a_step_or_changes_the_document() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(LayerOf(1)) });   // a layer exists, none is selected
        var doc = session.Document;
        int nextId = session.NextId, changes = 0;
        session.Changed += (_, _) => changes++;
        session.Changing += (_, _) => changes++;
        var placement = Placement(session);

        Assert.False(placement.HasSelection);
        foreach (var (name, command) in Commands(placement))
        {
            Assert.False(command.CanExecute(null), name);
            command.Execute(null);   // a stray invocation (UI Automation, a bridge) must still do nothing
        }

        Assert.Same(doc, session.Document);
        Assert.Equal((0, false, nextId, 0), (session.UndoCount, session.CanUndo, session.NextId, changes));
    });

    [Fact] // btn-fit-front: the ledger's own numbers, one step, undo puts the layer back
    public void Fit_Front_is_one_undo_step_with_the_ledgers_numbers() => StaThread.Run(() =>
    {
        var session = Selected(200, 100);
        var before = Of(session, 1);
        Placement(session).FitFrontCommand.Execute(null);

        var l = Of(session, 1);
        Near(0.576, l.Scale);
        Near(237.4, l.X);
        Near(109.2, l.Y);
        Assert.Equal((1, 1), (session.UndoCount, session.Document.Layers.Count));
        Assert.Equal(1, session.SelectedLayerId);
        Assert.True(session.Undo());
        Assert.Same(before, Of(session, 1));
    });

    [Fact] // undo-empty-steps: a click that changes nothing records nothing
    public void Clicking_Fit_Front_on_a_fitted_layer_records_no_second_step() => StaThread.Run(() =>
    {
        var session = Selected();
        var placement = Placement(session);
        placement.FitFrontCommand.Execute(null);
        var doc = session.Document;
        placement.FitFrontCommand.Execute(null);

        Assert.Equal(1, session.UndoCount);
        Assert.Same(doc, session.Document);
    });

    [Fact] // btn-fit-fb: the front fit plus a "(back)" clone on top, the original stays selected, one step
    public void F_plus_B_adds_the_back_clone_in_one_step_and_keeps_the_selection() => StaThread.Run(() =>
    {
        var session = Selected(200, 100);
        Placement(session).FrontBackCommand.Execute(null);

        Assert.Equal(2, session.Document.Layers.Count);
        var (front, back) = (Of(session, 1), session.Document.Layers[^1]);
        Assert.Equal("Image 1 (back)", back.Name);
        Assert.NotEqual(1, back.Id);
        Near(0.576, back.Scale);
        Near(front.Scale, back.Scale);
        Assert.Same(front.Image, back.Image);
        Assert.Equal(1, session.SelectedLayerId);
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.Undo());
        Assert.Single(session.Document.Layers);
    });

    [Fact] // btn-stretch: the ledger's 300x100 numbers, plus the back clone, one step
    public void Stretch_fits_the_three_faces_in_one_step() => StaThread.Run(() =>
    {
        var session = Selected(300, 100);
        Placement(session).StretchCommand.Execute(null);

        var l = Of(session, 1);
        Near(0.8666666666666667, l.Scale);
        Near(165, l.X);
        Near(94.66666666666667, l.Y);
        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Equal("Image 1 (back)", session.Document.Layers[^1].Name);
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // btn-allover: 18 tiles, in region order, one step, ids from the session
    public void Allover_adds_eighteen_tiles_in_one_step_with_session_ids() => StaThread.Run(() =>
    {
        var session = Selected();
        int first = session.NextId;
        Placement(session).AlloverCommand.Execute(null);

        var tiles = session.Document.Layers.Skip(1).ToList();
        Assert.Equal(18, tiles.Count);
        Assert.All(tiles, t => Assert.Equal("Image 1 tile", t.Name));
        Assert.Equal(Enumerable.Range(first, 18), tiles.Select(t => t.Id));
        Assert.Equal(first + 18, session.NextId);
        Assert.Equal((1, 1), (session.UndoCount, session.SelectedLayerId));
        Assert.True(session.Undo());
        Assert.Single(session.Document.Layers);
    });

    [Fact] // btn-scatter: 26 copies in Shirt, every value in 4.1's ranges, one step
    public void Scatter_adds_twenty_six_copies_inside_the_ranges_in_one_step() => StaThread.Run(() =>
    {
        var session = Selected(200, 100);
        Placement(session).ScatterCommand.Execute(null);

        var copies = session.Document.Layers.Skip(1).ToList();
        Assert.Equal(26, copies.Count);
        Assert.All(copies, c => Assert.Equal("Image 1 scat", c.Name));
        Assert.All(copies, c => Assert.True(c.Scale >= 0.02 && c.Rotation >= -30 && c.Rotation < 30));
        var regions = Template.Regions(TemplateMode.Shirt).SelectMany(r => Enumerable.Repeat(r, r.W > 100 ? 3 : 1)).ToList();
        for (int i = 0; i < copies.Count; i++)
        {
            Assert.InRange(copies[i].X, regions[i].X, regions[i].X + regions[i].W * 0.7);
            Assert.InRange(copies[i].Y, regions[i].Y, regions[i].Y + regions[i].H * 0.7);
        }
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // spec §5: the random source is injected; the same seed gives the same scatter, a different one does not
    public void Scatter_follows_the_injected_random() => StaThread.Run(() =>
    {
        List<(double, double, double, double)> Run(int seed)
        {
            var session = Selected();
            Placement(session, () => new Random(seed)).ScatterCommand.Execute(null);
            return session.Document.Layers.Skip(1).Select(l => (l.Scale, l.X, l.Y, l.Rotation)).ToList();
        }
        Assert.Equal(Run(5), Run(5));
        Assert.NotEqual(Run(5), Run(6));
    });

    [Fact] // the composition root takes the random source too: a shell built with a seeded one scatters the same way as the view-model
    public void The_composed_shell_scatters_with_the_random_it_was_given() => StaThread.Run(() =>
    {
        var a = Selected();
        var main = TestShell.Quiet(TestShell.Main(a, random: () => new Random(5)));
        main.Home.Placement.ScatterCommand.Execute(null);
        var b = Selected();
        Placement(b, () => new Random(5)).ScatterCommand.Execute(null);

        Assert.Equal(b.Document.Layers.Select(l => (l.Scale, l.X, l.Y, l.Rotation)), a.Document.Layers.Select(l => (l.Scale, l.X, l.Y, l.Rotation)));
    });

    [Theory] // bug-pants-mode-region-keys: every Placement command works in both modes, with the same counts
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Every_command_works_in_both_modes(TemplateMode mode) => StaThread.Run(() =>
    {
        var expected = new Dictionary<string, int> { ["FitFront"] = 1, ["FrontBack"] = 2, ["Stretch"] = 2, ["Allover"] = 19, ["Scatter"] = 27 };
        for (int i = 0; i < expected.Count; i++)
        {
            var session = Selected(mode: mode);
            var (name, command) = Commands(Placement(session))[i];
            command.Execute(null);
            Assert.Equal((expected[name], 1), (session.Document.Layers.Count, session.UndoCount));
            Assert.Equal(1, session.SelectedLayerId);
        }
    });

    [Fact] // spec §4: HasSelection follows the selection, including a layer being removed
    public void HasSelection_follows_the_selection_and_raises_when_it_changes() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(LayerOf(1)) });
        var placement = Placement(session);
        var names = Track(placement);
        Assert.False(placement.HasSelection);
        session.Select(1);
        Assert.True(placement.HasSelection);
        Assert.True(placement.FitFrontCommand.CanExecute(null));
        session.Select(null);
        Assert.False(placement.HasSelection);
        Assert.False(placement.FitFrontCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(PlacementViewModel.HasSelection), nameof(PlacementViewModel.HasSelection) }, names);
        session.Select(1);
        session.Apply(DocumentOps.ClearLayers);   // the layer goes, the selection goes with it
        Assert.False(placement.HasSelection);
    });

    // ---- the view

    [Fact] // home-placement-group: the group sits after Brush, collapsed with nothing selected, shown with a selection
    public void The_group_follows_Brush_and_is_collapsed_until_a_layer_is_selected() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(LayerOf(1)) });
        var view = Load(Home(session));
        var group = (PlacementGroup)view.FindName("Placement");
        var groups = ((StackPanel)view.FindName("Groups")).Children.OfType<FrameworkElement>().ToList();

        Assert.Equal(groups.IndexOf((FrameworkElement)view.FindName("Brush")) + 1, groups.IndexOf(group));
        Assert.Equal(Visibility.Collapsed, group.Visibility);
        Assert.Equal(0, group.ActualHeight);

        session.Select(1);
        StaThread.Pump();
        view.UpdateLayout();
        Assert.Equal(Visibility.Visible, group.Visibility);
        Assert.True(group.ActualHeight > 0);
        Assert.Contains("PLACEMENT", Descendants<TextBlock>(group).Select(t => t.Text));

        session.Select(null);
        StaThread.Pump();
        Assert.Equal(Visibility.Collapsed, group.Visibility);
    });

    [Fact] // editor.html:63-76: five buttons, 4.1's labels and titles, three on the first row and two on the second
    public void The_buttons_carry_4_1s_labels_and_tooltips_in_two_rows() => StaThread.Run(() =>
    {
        var session = Selected();
        var view = Load(Home(session));
        var group = (PlacementGroup)view.FindName("Placement");
        var rows = Descendants<StackPanel>(group).Where(p => p.Orientation == Orientation.Horizontal && p.Children.OfType<Button>().Any()).ToList();

        Assert.Equal(new[] { 3, 2 }, rows.Select(r => r.Children.Count));
        Assert.Equal(new[] { "Fit Front", "F+B", "Stretch", "Allover", "Scatter" }, rows.SelectMany(r => r.Children.OfType<Button>()).Select(b => (string)b.Content));
        Assert.Equal(new[] { "Fit to front torso", "Front + Back", "Stretch across torso", "Tile allover", "Scatter randomly" },
            rows.SelectMany(r => r.Children.OfType<Button>()).Select(b => (string)b.ToolTip));
        Assert.True(rows[0].Children[0].TransformToAncestor(group).Transform(new Point(0, 0)).Y
                    < rows[1].Children[0].TransformToAncestor(group).Transform(new Point(0, 0)).Y);
    });

    [Fact] // a11y: every button has an automation name and the shared focus ring
    public void Every_button_is_named_and_uses_the_focus_ring() => StaThread.Run(() =>
    {
        var view = Load(Home(Selected()));
        var group = (PlacementGroup)view.FindName("Placement");
        var buttons = Descendants<Button>(group).ToList();

        Assert.Equal(5, buttons.Count);
        Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))));
        Assert.Equal(5, buttons.Select(AutomationProperties.GetName).Distinct().Count());
        Assert.All(buttons, b => Assert.True(b.Focusable && b.IsTabStop));
        Assert.All(buttons, b => Assert.Same(Application.Current.FindResource("FocusRing"), b.FocusVisualStyle));
    });

    [Fact] // the click a user (or UI Automation) makes: Fit Front through the button is one step
    public void Invoking_the_Fit_Front_button_is_one_undo_step() => StaThread.Run(() =>
    {
        var session = Selected();
        var view = Load(Home(session));
        var group = (PlacementGroup)view.FindName("Placement");
        Invoke((Button)group.FindName("FitFrontButton"));

        Assert.Equal(1, session.UndoCount);
        Near(0.576, Of(session, 1).Scale);
        Invoke((Button)group.FindName("AlloverButton"));
        Assert.Equal((2, 19), (session.UndoCount, session.Document.Layers.Count));
    });

    [Fact] // item 12 owns the ribbon re-measure; this pins that item 5 added nothing: the groups are not the tallest on Home, so selecting a layer moves nothing
    public void Showing_Placement_and_Presets_does_not_make_the_ribbon_taller() => StaThread.Run(() =>
    {
        (double Ribbon, double Area, double Home, double Placement) Measure(bool selected)
        {
            var session = new EditorSession(new Document { Layers = ImmutableList.Create(LayerOf(1)) });
            if (selected) session.Select(1);
            var w = new MainWindow(TestShell.Main(session), new RBX15.App.Tests.Fakes.FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
            StaThread.Pump();
            ((ColumnDefinition)w.FindName("PanelColumn")).Width = new GridLength(ShellLayout.PanelWidthFor(1184));
            var root = (FrameworkElement)w.Content;
            root.Measure(new Size(1184, 761));
            root.Arrange(new Rect(0, 0, 1184, 761));
            root.UpdateLayout();
            var home = (HomeRibbon)w.FindName("HomePane");
            var groups = ((StackPanel)home.FindName("Groups")).Children.OfType<FrameworkElement>().Where(g => g.Visibility == Visibility.Visible);
            return (((FrameworkElement)w.FindName("Ribbon")).ActualHeight, ((FrameworkElement)w.FindName("CanvasArea")).ActualHeight,
                    groups.Max(g => g.DesiredSize.Height), ((FrameworkElement)home.FindName("Placement")).DesiredSize.Height);
        }

        var (none, one) = (Measure(false), Measure(true));
        Assert.Equal((none.Ribbon, none.Area), (one.Ribbon, one.Area));
        Assert.True(one.Placement > 0 && one.Placement < one.Home, $"Placement {one.Placement} against the tallest Home group {one.Home}");
    });
}
