using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>The Home tab's File group. DataContext is a FileGroupViewModel; the code-behind only runs the Recent drop-down's popup.</summary>
public partial class FileGroup : UserControl
{
    /// <summary>A click that closed the popup (StaysOpen false closes it on the mouse down) must not reopen it on the mouse up.</summary>
    static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(250);

    DateTime _closedAt = DateTime.MinValue;

    public FileGroup() => InitializeComponent();

    void OnRecent(object sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow - _closedAt < ReopenGuard) return;
        // Re-check the files as the drop-down opens, so one deleted since is not offered; the list rebuilds when the check ends.
        if (DataContext is FileGroupViewModel vm) vm.Recent.RefreshCommand.Execute(null);
        RecentPopup.IsOpen = true;
    }

    /// <summary>Puts the keyboard on the first entry, so Enter or Space opens it and arrows or Tab reach the rest.</summary>
    void OnRecentOpened(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => FirstButton(RecentFrame)?.Focus());

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

    void OnRecentClosed(object? sender, EventArgs e) => _closedAt = DateTime.UtcNow;

    /// <summary>The entry's command has run (or started); the popup has done its job.</summary>
    void OnRecentItem(object sender, RoutedEventArgs e) => RecentPopup.IsOpen = false;
}
