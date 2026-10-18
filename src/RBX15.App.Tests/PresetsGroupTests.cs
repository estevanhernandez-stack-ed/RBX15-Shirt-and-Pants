using System.Collections.Immutable;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Presets group, first on the Layout tab (ledger presets-six-buttons, layout-presets): six buttons in a 3-column grid
/// that act on the selected layer, each as one undo step (4.1 pushed two for four of them), collapsed until a layer is
/// selected. The Layout tab shows because the group is its first content (item 2's HasGroups rule).
/// </summary>
[Collection("MainWindow")] // the window facts build a MainWindow, as ShellLookTests does
public class PresetsGroupTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    static EditorSession Selected(int w = 200, int h = 100, TemplateMode mode = TemplateMode.Shirt) =>
        PlacementGroupTests.Selected(w, h, mode);

    static PresetsViewModel Presets(EditorSession session, Func<Random>? rng = null) => new(session, rng ?? (() => new Random(7)));

    static (string Name, RelayCommand Command)[] Commands(PresetsViewModel p) =>
    [
        ("Meme", p.MemeCommand), ("Allover", p.AlloverCommand), ("SleevePop", p.SleevePopCommand),
        ("Mirror", p.MirrorCommand), ("Scatter", p.ScatterCommand), ("Fullbleed", p.FullbleedCommand),
    ];

    static PresetsGroup Load(PresetsViewModel presets)
    {
        var view = new PresetsGroup { DataContext = presets };
        StaThread.Pump();
        view.Measure(new Size(600, 200));
        view.Arrange(new Rect(0, 0, 600, 200));
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

    static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1e-9, expected + 1e-9);

    [Fact] // presets-six-buttons parityCheck: no selection, all six are no-ops and the history is unchanged
    public void With_nothing_selected_no_preset_records_a_step_or_changes_the_document() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(LayerOf(1)) });
        var doc = session.Document;
        int nextId = session.NextId;
        var presets = Presets(session);

        Assert.False(presets.HasSelection);
        foreach (var (name, command) in Commands(presets))
        {
            Assert.False(command.CanExecute(null), name);
            command.Execute(null);
        }

        Assert.Same(doc, session.Document);
        Assert.Equal((0, nextId), (session.UndoCount, session.NextId));
    });

    [Fact] // preset-meme: the same transform as Fit Front (300x200 gives 0.384), one step
    public void Meme_Tee_is_Fit_Front_in_one_step() => StaThread.Run(() =>
    {
        var session = Selected(300, 200);
        Presets(session).MemeCommand.Execute(null);

        var l = session.Document.Layers.Single();
        Near(0.384, l.Scale);
        Assert.Equal(1, session.UndoCount);

        var fit = Selected(300, 200);
        new PlacementViewModel(fit, () => new Random(1)).FitFrontCommand.Execute(null);
        Assert.Equal((fit.Document.Layers.Single().Scale, fit.Document.Layers.Single().X, fit.Document.Layers.Single().Y), (l.Scale, l.X, l.Y));
    });

    [Fact] // preset-allover: 18 tiles, one step (4.1 pushed two identical entries)
    public void Allover_adds_eighteen_tiles_in_one_step() => StaThread.Run(() =>
    {
        var session = Selected();
        Presets(session).AlloverCommand.Execute(null);

        Assert.Equal(19, session.Document.Layers.Count);
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.Undo());
        Assert.False(session.CanUndo);
        Assert.Single(session.Document.Layers);
    });

    [Fact] // preset-sleeve-pop: 8 sleeve layers, one step
    public void Sleeve_Pop_adds_eight_sleeve_layers_in_one_step() => StaThread.Run(() =>
    {
        var session = Selected();
        Presets(session).SleevePopCommand.Execute(null);

        var sleeves = session.Document.Layers.Skip(1).ToList();
        Assert.Equal(8, sleeves.Count);
        Assert.All(sleeves, s => Assert.Equal("Image 1 sleeve", s.Name));
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(1, session.SelectedLayerId);
    });

    [Fact] // preset-mirror: the front fit and its back clone, ONE undo step where 4.1 pushed two
    public void Mirror_F_plus_B_is_one_undo_step_not_two() => StaThread.Run(() =>
    {
        var session = Selected();
        Presets(session).MirrorCommand.Execute(null);

        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Equal("Image 1 (back)", session.Document.Layers[^1].Name);
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.Undo());
        Assert.False(session.CanUndo);                     // one Ctrl+Z undoes the whole preset
        Assert.Single(session.Document.Layers);
        Assert.Equal(1.0, session.Document.Layers[0].Scale);
    });

    [Fact] // preset-scattered: 26 copies from the injected random, one step
    public void Scatter_adds_twenty_six_copies_in_one_step_from_the_injected_random() => StaThread.Run(() =>
    {
        var a = Selected();
        Presets(a, () => new Random(3)).ScatterCommand.Execute(null);
        var b = Selected();
        new PlacementViewModel(b, () => new Random(3)).ScatterCommand.Execute(null);

        Assert.Equal(27, a.Document.Layers.Count);
        Assert.Equal(1, a.UndoCount);
        Assert.Equal(b.Document.Layers.Select(l => (l.Scale, l.X, l.Y, l.Rotation)), a.Document.Layers.Select(l => (l.Scale, l.X, l.Y, l.Rotation)));
    });

    [Fact] // preset-fullbleed parityCheck: 100x100 gives scale 5.85, x 0, y -13; no clone, one step
    public void Fullbleed_covers_the_template_in_one_step() => StaThread.Run(() =>
    {
        var session = Selected(100, 100);
        Presets(session).FullbleedCommand.Execute(null);

        var l = session.Document.Layers.Single();
        Near(5.85, l.Scale);
        Near(0, l.X);
        Near(-13, l.Y);
        Assert.Equal(1, session.UndoCount);
    });

    [Theory] // layout-presets: every preset works in Pants (Sleeve Pop threw there in 4.1) with the same counts, one step each
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Every_preset_works_in_both_modes_in_one_step(TemplateMode mode) => StaThread.Run(() =>
    {
        int[] counts = [1, 19, 9, 2, 27, 1];
        for (int i = 0; i < counts.Length; i++)
        {
            var session = Selected(mode: mode);
            var (name, command) = Commands(Presets(session))[i];
            command.Execute(null);
            Assert.Equal((counts[i], 1, 1), (session.Document.Layers.Count, session.UndoCount, session.SelectedLayerId));
        }
    });

    [Fact] // a preset that changes nothing records nothing: Meme Tee on a layer already fitted
    public void A_preset_that_changes_nothing_records_no_step() => StaThread.Run(() =>
    {
        var session = Selected();
        var presets = Presets(session);
        presets.MemeCommand.Execute(null);
        var doc = session.Document;
        presets.MemeCommand.Execute(null);

        Assert.Equal(1, session.UndoCount);
        Assert.Same(doc, session.Document);
    });

    // ---- the view

    [Fact] // editor.html:113-124: six buttons in 4.1's order, in a 3-column grid, collapsed until a layer is selected
    public void The_group_is_a_three_column_grid_of_six_named_buttons_collapsed_without_a_selection() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(LayerOf(1)) });
        var view = Load(Presets(session));

        Assert.Equal(Visibility.Collapsed, view.Visibility);
        session.Select(1);
        StaThread.Pump();
        view.Measure(new Size(600, 200));      // collapsed, the group built no template; shown, it needs a layout pass
        view.Arrange(new Rect(0, 0, 600, 200));
        view.UpdateLayout();
        Assert.Equal(Visibility.Visible, view.Visibility);

        var grid = Descendants<UniformGrid>(view).Single();
        var buttons = grid.Children.OfType<Button>().ToList();
        Assert.Equal(3, grid.Columns);
        Assert.Equal(new[] { "Meme Tee", "Allover", "Sleeve Pop", "Mirror F+B", "Scatter", "Fullbleed" }, buttons.Select(b => (string)b.Content));
        Assert.Contains("PRESETS", Descendants<TextBlock>(view).Select(t => t.Text));
        Assert.All(buttons, b => Assert.Equal(10.0, b.FontSize));
        Assert.All(buttons, b => Assert.Equal(new Thickness(8, 4, 8, 4), b.Padding));
        Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, buttons.Select(b => (int)Math.Round(b.TranslatePoint(new Point(0, 0), grid).X / (grid.ActualWidth / 3))));
        Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, buttons.Select(b => (int)Math.Round(b.TranslatePoint(new Point(0, 0), grid).Y / (grid.ActualHeight / 2))));
    });

    [Fact] // a11y: every button has a distinct automation name and the shared focus ring
    public void Every_button_is_named_and_uses_the_focus_ring() => StaThread.Run(() =>
    {
        var view = Load(Presets(Selected()));
        var buttons = Descendants<Button>(view).ToList();

        Assert.Equal(6, buttons.Count);
        Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))));
        Assert.Equal(6, buttons.Select(AutomationProperties.GetName).Distinct().Count());
        Assert.All(buttons, b => Assert.True(b.Focusable && b.IsTabStop));
        Assert.All(buttons, b => Assert.Same(Application.Current.FindResource("FocusRing"), b.FocusVisualStyle));
    });

    [Fact] // the click a user makes: Mirror F+B through the button is one step
    public void Invoking_a_preset_button_is_one_undo_step() => StaThread.Run(() =>
    {
        var session = Selected();
        var view = Load(Presets(session));
        Invoke((Button)view.FindName("MirrorButton"));
        Assert.Equal((1, 2), (session.UndoCount, session.Document.Layers.Count));
        Invoke((Button)view.FindName("FullbleedButton"));
        Assert.Equal(2, session.UndoCount);
    });

    // ---- the window: the Layout tab shows because its pane has a group

    [Fact] // item 2's rule: a tab shows when its pane has a group. Layout does now, Details does since item 7; Text does not yet.
    public void The_Layout_tab_shows_with_the_Presets_group_and_Text_stays_collapsed() => StaThread.Run(() =>
    {
        var w = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
        StaThread.Pump();

        Assert.Equal(Visibility.Visible, ((RadioButton)w.FindName("LayoutTab")).Visibility);
        Assert.Equal(Visibility.Visible, ((RadioButton)w.FindName("DetailsTab")).Visibility);   // item 7 gave it Settings and four button groups
        Assert.Equal(Visibility.Collapsed, ((RadioButton)w.FindName("TextTab")).Visibility);
        Assert.True(((IRibbonPane)w.FindName("LayoutPane")).HasGroups);
        var groups = (StackPanel)((LayoutRibbon)w.FindName("LayoutPane")).FindName("Groups");
        Assert.IsType<PresetsGroup>(groups.Children[0]);
    });

    [Fact] // the shell end to end: select a layer, open Layout, click Mirror F+B through the shell's own view-models
    public void The_composed_shell_runs_a_preset_from_the_Layout_pane() => StaThread.Run(() =>
    {
        var session = Selected();
        var main = TestShell.Quiet(TestShell.Main(session));
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        main.ActiveTab = RibbonTab.Layout;
        StaThread.Pump();
        var pane = (LayoutRibbon)w.FindName("LayoutPane");
        pane.Measure(new Size(1200, 200));
        pane.Arrange(new Rect(0, 0, 1200, 200));
        pane.UpdateLayout();
        var group = (PresetsGroup)pane.FindName("Presets");

        Assert.Equal(Visibility.Visible, group.Visibility);
        Invoke((Button)group.FindName("MirrorButton"));
        Assert.Equal((1, 2), (session.UndoCount, session.Document.Layers.Count));

        session.Select(null);
        StaThread.Pump();
        Assert.Equal(Visibility.Collapsed, group.Visibility);
    });
}
