using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The accessibility pass (spec §4 "Accessibility"; M5/M6 decision X1; ledger a11y-focus-rings). A UI Automation sweep over a shown
/// MainWindow (each of the four tabs, with a layer selected so Placement shows) and over the themed dialogs, the recovery prompt, the
/// color picker and the Recent drop-down: every element a client can focus or act on (focusable, or supporting Invoke, Toggle, Value,
/// RangeValue or SelectionItem) has a non-empty Name, and every focusable control resolves the FocusRing focus visual. Tab-order facts
/// pin that the keyboard order reaches the region tiles, a Background swatch, a layer row and the drop zone. Drives the peers through
/// <see cref="UiaKit"/> only: no mouse, no keyboard device, no synthetic desktop input, no app launch.
///
/// Known limitations (decision X1; written down for the release notes and docs-known-limitations, not covered by this sweep):
/// - High-contrast themes are not supported: the app draws its own dark palette whatever the Windows contrast theme is.
/// - Canvas edits are not announced to screen readers: a layer is exposed to UI Automation as an item that can be selected and read, but
///   moving, painting or filling on the canvas raises no spoken notification.
/// </summary>
[Collection("MainWindow")] // shows windows on the shared STA thread
public class AccessibilitySweepTests
{
    /// <summary>A work area no monitor covers, so no window appears on the developer's desktop.</summary>
    static readonly Rect OffScreen = new(-12000, -12000, 1920, 1040);

    sealed class Rig : IDisposable
    {
        public TestFiles Files { get; } = new();
        public AppStorage Storage { get; } = TestShell.Storage();
        public FakeFileDialogs FileDialogs { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public EditorSession Session { get; } = new();
        public MainWindow Window { get; }
        public AutomationPeer Root { get; }

        public Rig()
        {
            var settings = new FakeSettingsStore();
            var main = TestShell.Quiet(Composition.Build(Session, FileDialogs, Dialogs, new FakeColorPicker(), settings, Storage,
                new ManualTickSource(), new FakeProcessProbe(), random: () => new Random(1), jumpList: new FakeJumpListSink(),
                clipboard: new FakeClipboardSource(), clipboardDelay: _ => { }));
            Window = new MainWindow(main, settings, [OffScreen]) { ShowActivated = false, ShowInTaskbar = false };
            Window.FocusedElement = () => null;
            Window.Show();
            StaThread.Pump();
            Root = UiaKit.PeerOf(Window);
            FileDialogs.OpenResult = [Files.Image("a.png", 300, 120, SKColors.Red)];
            UiaKit.Invoke(UiaKit.Find(Root, name: "Add image"));   // one selected layer, so Placement and the Selected layer panel show
            StaThread.Pump();
            Assert.NotNull(Session.SelectedLayerId);
        }

        public void Tab(string name)
        {
            UiaKit.Select(UiaKit.Find(Root, name: name));
            StaThread.Pump();
        }

        public void Dispose()
        {
            Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
            Window.Close();
            StaThread.Pump();
            Files.Dispose();
            if (Directory.Exists(Storage.Root)) Directory.Delete(Storage.Root, recursive: true);
        }
    }

    static readonly System.Windows.Automation.Peers.PatternInterface[] ActionPatterns =
    [
        PatternInterface.Invoke, PatternInterface.Toggle, PatternInterface.Value, PatternInterface.RangeValue, PatternInterface.SelectionItem,
    ];

    static bool Actionable(AutomationPeer p) => p.IsKeyboardFocusable() || ActionPatterns.Any(w => UiaKit.Supports(p, w));

    static string Describe(AutomationPeer p) =>
        $"{p.GetClassName()} id='{p.GetAutomationId()}' name='{p.GetName()}' type={p.GetAutomationControlType()}";

    /// <summary>Whether a text field or combo box draws its own focus border (a template trigger on keyboard focus): the 4.1 focus look, which a ring on top would double.</summary>
    static bool DrawsOwnFocus(FrameworkElement owner) =>
        owner is TextBox or ComboBox && owner is Control { Template: { } template }
        && template.Triggers.OfType<Trigger>().Any(t => t.Property == UIElement.IsKeyboardFocusedProperty || t.Property == UIElement.IsKeyboardFocusWithinProperty);

    /// <summary>
    /// The problems among <paramref name="roots"/>' shown peers: an unnamed actionable element; a focusable control without the FocusRing
    /// (list boxes are checked through their items, which take the focus; text fields and combo boxes that draw their own cyan focus border are exempt).
    /// </summary>
    static List<string> Sweep(string where, IEnumerable<AutomationPeer> roots)
    {
        var focusRing = Application.Current.FindResource("FocusRing");
        var problems = new List<string>();
        var seen = new HashSet<AutomationPeer>();
        foreach (var peer in roots.SelectMany(UiaKit.Descendants))
        {
            if (!seen.Add(peer)) continue;
            if (peer is UIElementAutomationPeer { Owner: ListBoxItem { IsVisible: true, Focusable: true, IsEnabled: true } item }
                && !ReferenceEquals(item.FocusVisualStyle, focusRing))
                problems.Add($"{where}: no FocusRing: ListBoxItem '{peer.GetName()}'");
            if (!Actionable(peer)) continue;
            if (string.IsNullOrWhiteSpace(peer.GetName())) problems.Add($"{where}: no Name: {Describe(peer)}");
            if (peer is UIElementAutomationPeer { Owner: FrameworkElement owner and not Window and not ItemsControl } && owner.Focusable && owner.IsEnabled
                && !ReferenceEquals(owner.FocusVisualStyle, focusRing) && !DrawsOwnFocus(owner))
                problems.Add($"{where}: no FocusRing: {Describe(peer)}");
        }
        return problems;
    }

    static void NoProblems(List<string> problems) =>
        Assert.True(problems.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, problems));

    static T First<T>(DependencyObject root) where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>([root]);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t) return t;
            for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        throw new InvalidOperationException("no " + typeof(T).Name);
    }

    [Fact] // every tab, the left panel and the canvas
    public void Every_actionable_element_in_every_tab_has_a_name_and_the_focus_ring() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        var problems = new List<string>();
        foreach (var tab in new[] { "Home tab", "Background tab", "Details tab", "Text tab" })
        {
            rig.Tab(tab);
            Assert.True(UiaKit.IsSelected(UiaKit.Find(rig.Root, name: tab)));
            problems.AddRange(Sweep(tab, [rig.Root]));
        }
        NoProblems(problems.Distinct().ToList());
    });

    [Fact] // the Recent drop-down's popup content, which the window's peer tree does not reach
    public void The_recent_dropdown_entries_are_named_and_focus_ringed() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.FileDialogs.SaveProjectResult = Path.Combine(rig.Files.Dir, "keep.r15proj");
        UiaKit.Invoke(UiaKit.Find(rig.Root, name: "Save work"));
        StaThread.Pump();
        var popup = (Popup)First<FileGroup>(rig.Window).FindName("RecentPopup");
        popup.IsOpen = true;
        StaThread.Pump();
        var roots = UiaKit.RootsWithin(popup.Child);
        Assert.NotEmpty(roots);
        var peers = roots.SelectMany(UiaKit.Descendants).ToList();
        Assert.Contains(peers, p => p.GetName() == "keep.r15proj");
        Assert.Contains(peers, p => p.GetName() == "Clear recent list");
        NoProblems(Sweep("Recent", roots));
        popup.IsOpen = false;
    });

    [Fact] // alert, confirm, save-changes and the recovery prompt
    public void The_themed_dialogs_and_the_recovery_prompt_are_named_and_focus_ringed() => StaThread.Run(() =>
    {
        var problems = new List<string>();
        foreach (var kind in Enum.GetValues<ThemedDialogKind>())
        {
            var d = new ThemedDialog("Message.", kind)
            {
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000, ShowActivated = false, ShowInTaskbar = false,
            };
            d.Show();
            StaThread.Pump();
            problems.AddRange(Sweep("Dialog " + kind, [UiaKit.PeerOf(d)]));
            d.Close();
        }
        NoProblems(problems);
    });

    [Fact] // the color picker
    public void The_color_picker_is_named_and_focus_ringed() => StaThread.Run(() =>
    {
        var p = new ColorPickerPopup(new ColorPickerViewModel("#17d4fa"))
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000, ShowActivated = false, ShowInTaskbar = false,
        };
        p.Show();
        StaThread.Pump();
        NoProblems(Sweep("Color picker", [UiaKit.PeerOf(p)]));
        p.Close();
    });

    /// <summary>The names of the shown tab stops in tab order (document order, as no TabIndex reorders the window), one per focus target.</summary>
    static List<string> TabStops(Rig rig) =>
        UiaKit.Descendants(rig.Root)
            .OfType<UIElementAutomationPeer>()
            .Where(p => p.Owner is { IsEnabled: true, Focusable: true } and not Window && KeyboardNavigation.GetIsTabStop(p.Owner))
            .Select(p => p.GetName() + "|" + p.GetClassName())
            .ToList();

    static List<T> AllOf<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>();
        var stack = new Stack<DependencyObject>([root]);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t) found.Add(t);
            for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        return found;
    }

    static void AssertInOrder(List<string> stops, params string[] names)
    {
        int at = -1;
        foreach (var name in names)
        {
            int next = stops.FindIndex(at + 1, s => s.StartsWith(name, StringComparison.Ordinal));
            Assert.True(next > at, $"'{name}' is not a tab stop after position {at}. Stops: {string.Join(" > ", stops)}");
            at = next;
        }
    }

    [Fact] // Tab walks the first ribbon tab, the active pane, the asset browser, the drop zone, the layer rows, the Selected layer panel and the canvas controls
    public void Tab_order_reaches_the_ribbon_the_left_panel_the_layers_and_the_canvas() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        // Document order: the window sets no TabIndex outside the color picker, so the tab order is the order the controls sit in the tree.
        AssertInOrder(TabStops(rig), "Home tab", "Add image", "Open folder", "Drop art here", "Dupe a", "Layer name", "Scale percent", "Zoom in");
        // The layer row itself takes the focus (its container is focusable and ringed); its buttons are stops of their own.
        var list = Assert.Single(AllOf<ListBox>(rig.Window), l => l.Name == "LayerList");
        var item = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.True(item.Focusable && item.IsEnabled && item.IsVisible);
    });

    [Fact] // the Background tab: a Background swatch and every region tile are tab stops
    public void Tab_order_reaches_the_background_swatches_and_every_region_tile() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.Tab("Background tab");
        var stops = TabStops(rig);
        AssertInOrder(stops, "Background tab", "Background Navy", "Fill all regions", "Torso Top", "Drop art here", "Layer name");
        Assert.All(new[] { "Torso Front", "Torso Back", "Right Arm Top", "Left Arm Bot (Bottom)" }, name => Assert.Contains(stops, s => s.StartsWith(name + "|", StringComparison.Ordinal)));
    });
}
