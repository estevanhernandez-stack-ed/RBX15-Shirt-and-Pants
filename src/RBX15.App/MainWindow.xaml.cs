using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.App.Views;

namespace RBX15.App;

/// <summary>
/// The main window: the shell skeleton, window placement memory (ledger shell-window-size, fix), the window's
/// shortcuts, and the unsaved-changes guard on close (M4a R13). The placement is restored before the window is shown
/// and saved when it closes.
/// </summary>
public partial class MainWindow : Window
{
    readonly ISettingsStore _settings;
    readonly ShortcutRouter _shortcuts;
    readonly LayerKeys _layerKeys;
    bool _maximizedBeforeMinimize;

    /// <param name="workAreas">The monitors' work areas, primary first; null reads them from Windows.</param>
    public MainWindow(MainViewModel viewModel, ISettingsStore settings, IReadOnlyList<Rect>? workAreas = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        DataContext = viewModel;
        _layerKeys = new LayerKeys(viewModel.Session);
        _shortcuts = new ShortcutRouter(viewModel.Home.Tools, _layerKeys);
        InitializeComponent();
        CaptionTheme.Apply(this);
        RefreshTabs();
        RibbonTabs.KeyDown += OnTabKeyDown;
        ApplyPlacement(PlacementGuard.Resolve(settings.LoadWindowPlacement(), workAreas ?? MonitorWorkAreas.Get(),
                                              ShellLayout.DefaultWidth, ShellLayout.DefaultHeight));
        Viewport.SizeChanged += (_, e) => ApplyColumnWidths(e.NewSize.Width);
        // The 3D preview column follows the toggle: width 0 when hidden, the breakpoint width when shown.
        viewModel.AvatarPreview.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AvatarPreviewViewModel.IsVisible)) ApplyColumnWidths(Viewport.ActualWidth);
        };
        ApplyColumnWidths(ShellLayout.DefaultWidth);
        // The window's seven chords (spec §4 "Keyboard"; M4b item 11): Undo, Redo (two chords), Duplicate and the three file chords. A focused
        // text field keeps each key even when it has nothing to do with it (final fix wave 5); anywhere else it is the command. Keys OFF never
        // reaches them (they are KeyBindings, not routed). Ctrl+V is left unbound for M5's paste.
        AddShortcut(viewModel.Home.Edit.UndoCommand, Key.Z, ModifierKeys.Control);
        AddShortcut(viewModel.Home.Edit.RedoCommand, Key.Y, ModifierKeys.Control);
        AddShortcut(viewModel.Home.Edit.RedoCommand, Key.Z, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(_layerKeys.DuplicateCommand, Key.D, ModifierKeys.Control);
        AddShortcut(viewModel.Home.File.SaveCommand, Key.S, ModifierKeys.Control);
        AddShortcut(viewModel.Home.File.SaveAsCommand, Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(viewModel.Home.File.OpenCommand, Key.O, ModifierKeys.Control);
        // The bare-key shortcuts (M4b items 10 and 11; K1, K2): characters from text input, keys from key downs, both through the router.
        PreviewTextInput += OnPreviewTextInput;
        PreviewKeyDown += OnPreviewKeyDown;
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) _maximizedBeforeMinimize = WindowState == WindowState.Maximized; };
        // R4: a wait cursor while an Open reads; the Viewport's IsEnabled binding blocks the input.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBusy)) Mouse.OverrideCursor = viewModel.IsBusy ? Cursors.Wait : null;
        };
    }

    /// <summary>Sets the left panel and the 3D preview column for a viewport (window client) width in DIPs.</summary>
    internal void ApplyColumnWidths(double viewportWidth)
    {
        PanelColumn.Width = new GridLength(ShellLayout.PanelWidthFor(viewportWidth));
        PreviewColumn.Width = new GridLength(ViewModel.AvatarPreview.IsVisible ? ShellLayout.PreviewWidthFor(viewportWidth) : 0);
    }

    bool _showAllTabsForTest;

    /// <summary>
    /// Shows the Layout, Details and Text tabs although their panes have no group yet. A test hook (M4b item 2): the shipped
    /// rule is that a tab shows only when its pane has a group (<see cref="IRibbonPane.HasGroups"/>), so the tests that
    /// exercise all four tabs turn this on until items 3, 7 and 9 give the panes content.
    /// </summary>
    internal bool ShowAllTabsForTest
    {
        get => _showAllTabsForTest;
        set
        {
            _showAllTabsForTest = value;
            RefreshTabs();
        }
    }

    /// <summary>Whether a tab is on show: Home always, the others once their pane has a group (or the test hook is on).</summary>
    internal bool IsTabShown(RibbonTab tab) => tab == RibbonTab.Home || _showAllTabsForTest || PaneOf(tab).HasGroups;

    IRibbonPane PaneOf(RibbonTab tab) => tab switch
    {
        RibbonTab.Home => HomePane,
        RibbonTab.Layout => LayoutPane,
        RibbonTab.Details => DetailsPane,
        RibbonTab.Text => TextPane,
        _ => throw new ArgumentOutOfRangeException(nameof(tab)),
    };

    void RefreshTabs()
    {
        foreach (var (tab, button) in new[] { (RibbonTab.Layout, LayoutTab), (RibbonTab.Details, DetailsTab), (RibbonTab.Text, TextTab) })
            button.Visibility = IsTabShown(tab) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Enter checks a focused tab, as Enter clicked 4.1's tab buttons (Space already does: RadioButton handles it).
    /// A focused text field keeps its keys; the tabs are never inside one.
    /// </summary>
    void OnTabKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not RadioButton { IsChecked: false } tab) return;
        tab.IsChecked = true;
        e.Handled = true;
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>The element with keyboard focus, as the window's shortcuts see it. Tests replace it; nothing else does.</summary>
    internal Func<IInputElement?> FocusedElement { get; set; } = () => Keyboard.FocusedElement;

    /// <summary>The modifier keys held, as the window's shortcuts see them. Tests replace it; nothing else does.</summary>
    internal Func<ModifierKeys> Modifiers { get; set; } = () => Keyboard.Modifiers;

    /// <summary>Whether a layer is selected, which the layer keys need.</summary>
    bool HasSelection => ViewModel.Session.SelectedLayerId is not null;

    /// <summary>A typed character is a shortcut (a tool letter, or a layer key with a selection) unless a text field or the color picker has focus or Keys is OFF; a handled one is 4.1's preventDefault.</summary>
    void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (ShortcutRouter.RouteText(e.Text, Modifiers(), FocusedElement(), ViewModel.Home.Keys.Enabled, HasSelection) is not { } action) return;
        _shortcuts.Execute(action);
        e.Handled = true;
    }

    /// <summary>A key down is a shortcut only for the keys that are not characters: the arrows, Delete and Backspace with a selection. Ctrl chords are KeyBindings and Space and Enter stay a focused button's.</summary>
    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key switch { Key.System => e.SystemKey, Key.ImeProcessed => e.ImeProcessedKey, Key.DeadCharProcessed => e.DeadCharProcessedKey, _ => e.Key };
        if (ShortcutRouter.Route(key, Modifiers(), FocusedElement(), ViewModel.Home.Keys.Enabled, HasSelection) is not { } action) return;
        _shortcuts.Execute(action);
        e.Handled = true;
    }

    /// <summary>
    /// Binds <paramref name="key"/> with <paramref name="modifiers"/> on the window to <paramref name="command"/> behind
    /// <see cref="TextFieldShortcutCommand"/>, so a focused text field keeps the key (spec §4: only text fields trap
    /// shortcuts). Returns the binding.
    /// </summary>
    internal KeyBinding AddShortcut(ICommand command, Key key, ModifierKeys modifiers)
    {
        ArgumentNullException.ThrowIfNull(command);
        var binding = new KeyBinding(new TextFieldShortcutCommand(command, () => FocusedElement()), key, modifiers);
        InputBindings.Add(binding);
        return binding;
    }

    internal void ApplyPlacement(WindowPlacement placement)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        WindowState = placement.Maximized ? WindowState.Maximized : WindowState.Normal;
        _maximizedBeforeMinimize = placement.Maximized;
    }

    /// <summary>
    /// The placement to save: the restore bounds whenever the window is maximized or minimized, and
    /// maximized when it is maximized or was maximized before it was minimized.
    /// </summary>
    internal WindowPlacement CapturePlacement()
    {
        var bounds = WindowState == WindowState.Normal || RestoreBounds.IsEmpty
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        bool maximized = WindowState == WindowState.Maximized || (WindowState == WindowState.Minimized && _maximizedBeforeMinimize);
        return new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized);
    }

    /// <summary>
    /// Closing (R13): a pending Open's read is cancelled and released; dirty work asks Save / Don't save / Cancel, and
    /// Cancel, a cancelled Save As or a failed save keeps the window open. A clean document closes with no prompt
    /// (ledger shell-no-unsaved-changes-guard parityCheck). The placement is saved only when the window really closes.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            ViewModel.Files.CancelPendingOpen();
            if (!ViewModel.Files.ConfirmDiscardChanges()) e.Cancel = true;
        }
        if (!e.Cancel) _settings.SaveWindowPlacement(CapturePlacement());
    }
}
