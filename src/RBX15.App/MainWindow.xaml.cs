using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// The main window: the shell skeleton, window placement memory (ledger shell-window-size, fix), the window's
/// shortcuts, and the unsaved-changes guard on close (M4a R13). The placement is restored before the window is shown
/// and saved when it closes.
/// </summary>
public partial class MainWindow : Window
{
    readonly ISettingsStore _settings;
    bool _maximizedBeforeMinimize;

    /// <param name="workAreas">The monitors' work areas, primary first; null reads them from Windows.</param>
    public MainWindow(MainViewModel viewModel, ISettingsStore settings, IReadOnlyList<Rect>? workAreas = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        DataContext = viewModel;
        InitializeComponent();
        CaptionTheme.Apply(this);
        ApplyPlacement(PlacementGuard.Resolve(settings.LoadWindowPlacement(), workAreas ?? MonitorWorkAreas.Get(),
                                              ShellLayout.DefaultWidth, ShellLayout.DefaultHeight));
        Viewport.SizeChanged += (_, e) => PanelColumn.Width = new GridLength(ShellLayout.PanelWidthFor(e.NewSize.Width));
        // The window's shortcuts (spec §4 "Keyboard"; M4a: the three file chords, the rest is M4b). A focused text field
        // keeps each key even when it has nothing to do with it (final fix wave 5); anywhere else it is the command.
        AddShortcut(viewModel.Home.Edit.UndoCommand, Key.Z, ModifierKeys.Control);
        AddShortcut(viewModel.Home.File.SaveCommand, Key.S, ModifierKeys.Control);
        AddShortcut(viewModel.Home.File.SaveAsCommand, Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(viewModel.Home.File.OpenCommand, Key.O, ModifierKeys.Control);
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) _maximizedBeforeMinimize = WindowState == WindowState.Maximized; };
        // R4: a wait cursor while an Open reads; the Viewport's IsEnabled binding blocks the input.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBusy)) Mouse.OverrideCursor = viewModel.IsBusy ? Cursors.Wait : null;
        };
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>The element with keyboard focus, as the window's shortcuts see it. Tests replace it; nothing else does.</summary>
    internal Func<IInputElement?> FocusedElement { get; set; } = () => Keyboard.FocusedElement;

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
