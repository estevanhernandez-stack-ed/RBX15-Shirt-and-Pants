using System.Windows;
using RBX15.App.Views;

namespace RBX15.App.Services;

/// <summary><see cref="IDialogService"/> over <see cref="ThemedDialog"/>, owned by the main window when it is showing.</summary>
public sealed class ThemedDialogService(Func<Window?> owner) : IDialogService
{
    public bool Confirm(string message) => Show(message, ThemedDialogKind.Confirm).Choice == SaveChoice.Save;

    public void Alert(string message) => Show(message, ThemedDialogKind.Alert);

    public SaveChoice AskSaveChanges(string documentName) =>
        Show(DialogText.SaveChanges(documentName), ThemedDialogKind.SaveChanges).Choice;

    public RestoreChoice AskRestore(string documentName, DateTimeOffset autosavedAt) =>
        Show(DialogText.RestorePrompt(documentName, DialogText.AutosavedWhen(autosavedAt, TimeProvider.System.GetUtcNow())), ThemedDialogKind.Restore).Choice switch
        {
            SaveChoice.Save => RestoreChoice.Restore,
            SaveChoice.DontSave => RestoreChoice.Discard,
            _ => RestoreChoice.Later,
        };

    ThemedDialog Show(string message, ThemedDialogKind kind)
    {
        var dialog = new ThemedDialog(message, kind);
        if (owner() is { IsVisible: true } o) dialog.Owner = o;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        return dialog;
    }
}
