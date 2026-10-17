using System.Windows;
using System.Windows.Input;
using RBX15.App.Services;

namespace RBX15.App.Views;

/// <summary>What a <see cref="ThemedDialog"/> asks.</summary>
public enum ThemedDialogKind
{
    /// <summary>A message and OK (4.1's alert).</summary>
    Alert,

    /// <summary>OK and Cancel (4.1's confirm).</summary>
    Confirm,

    /// <summary>Save, Don't save and Cancel: the unsaved-changes prompt (M4a interfaces R13).</summary>
    SaveChanges,
}

/// <summary>
/// The themed dialog: a message with OK, plus Cancel for a confirm, or Save, Don't save and Cancel for the
/// unsaved-changes prompt. Shown modally by <see cref="ThemedDialogService"/>. <see cref="Choice"/> is Save on
/// OK or Save, DontSave on Don't save, and Cancel on Cancel, Esc or the caption's close button.
/// </summary>
public partial class ThemedDialog : Window
{
    SaveChoice _choice = SaveChoice.Cancel;

    public ThemedDialog(string message, bool confirm)
        : this(message, confirm ? ThemedDialogKind.Confirm : ThemedDialogKind.Alert)
    {
    }

    public ThemedDialog(string message, ThemedDialogKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        InitializeComponent();
        CaptionTheme.Apply(this);
        Title = AppInfo.ProductName;
        MessageText.Text = message;
        IsConfirm = kind == ThemedDialogKind.Confirm;
        switch (kind)
        {
            case ThemedDialogKind.Alert:
                CancelButton.Visibility = Visibility.Collapsed;
                CancelButton.IsCancel = false;
                PreviewKeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) Close(); // an alert closes on Esc too
                };
                break;
            case ThemedDialogKind.SaveChanges:
                OkButton.Content = "Save";
                DontSaveButton.Visibility = Visibility.Visible;
                break;
        }
    }

    public bool IsConfirm { get; }

    /// <summary>The button that closed the dialog; Cancel until one did.</summary>
    public SaveChoice Choice => _choice;

    void OnOk(object sender, RoutedEventArgs e)
    {
        _choice = SaveChoice.Save;
        DialogResult = true;
    }

    void OnDontSave(object sender, RoutedEventArgs e)
    {
        _choice = SaveChoice.DontSave;
        DialogResult = false;
    }
}
