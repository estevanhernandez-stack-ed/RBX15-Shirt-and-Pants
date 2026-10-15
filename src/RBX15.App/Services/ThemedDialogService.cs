using System.Windows;
using RBX15.App.Views;

namespace RBX15.App.Services;

/// <summary><see cref="IDialogService"/> over <see cref="ThemedDialog"/>, owned by the main window when it is showing.</summary>
public sealed class ThemedDialogService(Func<Window?> owner) : IDialogService
{
    public bool Confirm(string message) => Show(message, confirm: true);

    public void Alert(string message) => Show(message, confirm: false);

    bool Show(string message, bool confirm)
    {
        var dialog = new ThemedDialog(message, confirm);
        if (owner() is { IsVisible: true } o) dialog.Owner = o;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }
}
