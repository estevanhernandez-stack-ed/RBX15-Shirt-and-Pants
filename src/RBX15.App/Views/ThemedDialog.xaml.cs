using System.Windows;
using System.Windows.Input;

namespace RBX15.App.Views;

/// <summary>
/// The minimal themed dialog: a message with OK, plus Cancel for a confirm. Shown modally by
/// <see cref="Services.ThemedDialogService"/>; <see cref="Window.DialogResult"/> is true only on OK.
/// </summary>
public partial class ThemedDialog : Window
{
    public ThemedDialog(string message, bool confirm)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        InitializeComponent();
        Title = AppInfo.ProductName;
        MessageText.Text = message;
        IsConfirm = confirm;
        if (!confirm)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            CancelButton.IsCancel = false;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close(); // an alert closes on Esc too
            };
        }
    }

    public bool IsConfirm { get; }

    void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
