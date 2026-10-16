using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// The main window: the shell skeleton plus window placement memory (ledger shell-window-size, fix).
/// The placement is restored before the window is shown and saved when it closes.
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
        // Ctrl+Z is M3b's one shortcut (planning contract: shortcuts are M4 except Ctrl+Z). A focused text field keeps
        // the key even when its own undo stack is empty (final fix wave 5); anywhere else it is the Edit group's Undo.
        InputBindings.Add(new KeyBinding(new TextFieldShortcutCommand(viewModel.Home.Edit.UndoCommand, () => FocusedElement()), Key.Z, ModifierKeys.Control));
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) _maximizedBeforeMinimize = WindowState == WindowState.Maximized; };
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>The element with keyboard focus, as the window's shortcuts see it. Tests replace it; nothing else does.</summary>
    internal Func<IInputElement?> FocusedElement { get; set; } = () => Keyboard.FocusedElement;

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

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel) _settings.SaveWindowPlacement(CapturePlacement());
    }
}
