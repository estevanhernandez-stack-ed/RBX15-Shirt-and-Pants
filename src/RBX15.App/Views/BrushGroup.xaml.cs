using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RBX15.App.Views;

/// <summary>The Home tab's Brush group. DataContext is a BrushViewModel.</summary>
public partial class BrushGroup : UserControl
{
    /// <summary>A click that closed the popup (StaysOpen false closes it on the mouse down) must not reopen it on the mouse up.</summary>
    static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(250);

    DateTime _closedAt = DateTime.MinValue;

    public BrushGroup() => InitializeComponent();

    void OnPalette(object sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow - _closedAt < ReopenGuard) return;
        PalettePopup.IsOpen = true;
    }

    /// <summary>Puts the keyboard on the first swatch, so Enter or Space picks it and arrows or Tab reach the rest.</summary>
    void OnPaletteOpened(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => FirstButton(PaletteFrame)?.Focus());

    static Button? FirstButton(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button { IsVisible: true } button) return button;
            if (FirstButton(child) is { } found) return found;
        }
        return null;
    }

    void OnPaletteClosed(object? sender, EventArgs e) => _closedAt = DateTime.UtcNow;

    /// <summary>The swatch's command has set the color; the popup has done its job.</summary>
    void OnPaletteSwatch(object sender, RoutedEventArgs e) => PalettePopup.IsOpen = false;
}
