using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The themed color picker popup (ledger <c>fillstyle-color-pickers</c>, fix): a borderless window owned
/// by the main window and shown with ShowDialog by <see cref="Services.ThemedColorPicker"/>, so a pick is
/// synchronous. Esc cancels, Enter commits (Enter on Cancel cancels), and losing activation commits.
/// Arrow keys move the square's point or the hue, Shift for bigger steps. All state lives in the
/// <see cref="ColorPickerViewModel"/>; this class only maps input to it and places the markers.
/// </summary>
public partial class ColorPickerPopup : Window
{
    /// <summary>The square and strip sizes in DIPs (the XAML's Width/Height), for the markers and the pointer math.</summary>
    public const double SquareWidth = 200, SquareHeight = 140, StripWidth = 200;

    /// <summary>The gap between the anchor button and the popup, in DIPs.</summary>
    public const double AnchorGap = 4;

    public ColorPickerPopup(ColorPickerViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
        Title = "Color picker";
        viewModel.PropertyChanged += OnViewModelChanged;
        PlaceMarkers();
        PreviewKeyDown += (_, e) => e.Handled = HandleKey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers, Keyboard.FocusedElement);
        Loaded += (_, _) => SvSquare.Focus();
        Deactivated += (_, _) =>
        {
            // Clicking anywhere outside commits. Close after the activation change has finished.
            if (!ViewModel.IsFinished) Dispatcher.BeginInvoke(() => Finish(commit: true));
        };
    }

    public ColorPickerViewModel ViewModel { get; }

    /// <summary>
    /// Maps one key press to the picker; returns whether it was used. Esc cancels; Enter commits unless
    /// Cancel has focus; arrows move the focused square or strip (Shift: the big step).
    /// </summary>
    internal bool HandleKey(Key key, ModifierKeys modifiers, IInputElement? focused)
    {
        switch (key)
        {
            case Key.Escape:
                Finish(commit: false);
                return true;
            case Key.Enter:
                Finish(commit: !ReferenceEquals(focused, CancelButton));
                return true;
        }
        bool big = (modifiers & ModifierKeys.Shift) != 0;
        if (ReferenceEquals(focused, SvSquare))
        {
            double step = big ? ColorPickerViewModel.SvBigStep : ColorPickerViewModel.SvStep;
            switch (key)
            {
                case Key.Left: ViewModel.MoveSv(-step, 0); return true;
                case Key.Right: ViewModel.MoveSv(step, 0); return true;
                case Key.Up: ViewModel.MoveSv(0, step); return true;
                case Key.Down: ViewModel.MoveSv(0, -step); return true;
            }
        }
        else if (ReferenceEquals(focused, HueStrip))
        {
            double step = big ? ColorPickerViewModel.HueBigStep : ColorPickerViewModel.HueStep;
            switch (key)
            {
                case Key.Left or Key.Down: ViewModel.MoveHue(-step); return true;
                case Key.Right or Key.Up: ViewModel.MoveHue(step); return true;
            }
        }
        return false;
    }

    /// <summary>Ends the pick once (commit or cancel) and closes. Later calls do nothing.</summary>
    internal void Finish(bool commit)
    {
        if (ViewModel.IsFinished) return;
        if (commit) ViewModel.Commit();
        else ViewModel.Cancel();
        if (IsLoaded || IsVisible) Close();
    }

    /// <summary>
    /// Where the popup goes: under the anchor, left edges aligned, or above it when there is no room
    /// below, and always inside the work area (DIPs throughout).
    /// </summary>
    internal static Point Place(Rect anchor, Size popup, Rect workArea)
    {
        double y = anchor.Bottom + AnchorGap;
        if (y + popup.Height > workArea.Bottom) y = anchor.Top - AnchorGap - popup.Height;
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - popup.Height));
        double x = Math.Max(workArea.Left, Math.Min(anchor.Left, workArea.Right - popup.Width));
        return new Point(x, y);
    }

    /// <summary>
    /// Sizes the window to its content and places it by <paramref name="anchor"/> on the work area holding
    /// it. Without an anchor on screen, it centers on its owner (or the screen).
    /// </summary>
    internal void PlaceBy(FrameworkElement? anchor, IReadOnlyList<Rect> workAreas)
    {
        var root = (FrameworkElement)Content;
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Width = Math.Ceiling(root.DesiredSize.Width);
        Height = Math.Ceiling(root.DesiredSize.Height);
        if (anchor is null || PresentationSource.FromVisual(anchor) is null || workAreas.Count == 0)
        {
            WindowStartupLocation = Owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            return;
        }
        var scale = VisualTreeHelper.GetDpi(anchor);
        var topLeft = anchor.PointToScreen(new Point(0, 0));
        var bottomRight = anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight));
        var rect = new Rect(topLeft.X / scale.DpiScaleX, topLeft.Y / scale.DpiScaleY,
                            (bottomRight.X - topLeft.X) / scale.DpiScaleX, (bottomRight.Y - topLeft.Y) / scale.DpiScaleY);
        var center = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
        var area = workAreas.FirstOrDefault(a => a.Contains(center), workAreas[0]);
        var at = Place(rect, new Size(Width, Height), area);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = at.X;
        Top = at.Y;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel && !ViewModel.IsFinished) ViewModel.Cancel(); // Alt+F4 is a cancel
    }

    void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColorPickerViewModel.Hue) or nameof(ColorPickerViewModel.Saturation) or nameof(ColorPickerViewModel.Value))
            PlaceMarkers();
    }

    void PlaceMarkers()
    {
        Canvas.SetLeft(SvMarker, ViewModel.Saturation * SquareWidth - SvMarker.Width / 2);
        Canvas.SetTop(SvMarker, (1 - ViewModel.Value) * SquareHeight - SvMarker.Height / 2);
        Canvas.SetLeft(HueMarker, ViewModel.Hue / 360 * StripWidth - HueMarker.Width / 2);
    }

    void OnSvDown(object sender, MouseButtonEventArgs e)
    {
        SvSquare.Focus();
        SvSquare.CaptureMouse();
        SetSv(e.GetPosition(SvSquare));
        e.Handled = true;
    }

    void OnSvMove(object sender, MouseEventArgs e)
    {
        if (SvSquare.IsMouseCaptured) SetSv(e.GetPosition(SvSquare));
    }

    void OnHueDown(object sender, MouseButtonEventArgs e)
    {
        HueStrip.Focus();
        HueStrip.CaptureMouse();
        ViewModel.SetHueFromPoint(e.GetPosition(HueStrip).X / StripWidth);
        e.Handled = true;
    }

    void OnHueMove(object sender, MouseEventArgs e)
    {
        if (HueStrip.IsMouseCaptured) ViewModel.SetHueFromPoint(e.GetPosition(HueStrip).X / StripWidth);
    }

    void OnPointerUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();

    void SetSv(Point p) => ViewModel.SetSvFromPoint(p.X / SquareWidth, p.Y / SquareHeight);

    void OnHexLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!ViewModel.TrySetHex(ViewModel.HexText)) ViewModel.RevertHexText();
    }

    void OnOk(object sender, RoutedEventArgs e) => Finish(commit: true);

    void OnCancel(object sender, RoutedEventArgs e) => Finish(commit: false);
}
