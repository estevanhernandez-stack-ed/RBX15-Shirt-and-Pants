namespace RBX15.App.Services;

/// <summary>
/// 4.1's blocking alert and confirm (ledger dialogs-inventory), as themed modal dialogs titled
/// <see cref="AppInfo.ProductName"/> (io-empty-export-confirm quirk: Electron showed the page title).
/// </summary>
public interface IDialogService
{
    /// <summary>OK and Cancel; true on OK. Enter is OK, Esc is Cancel, as Chromium's confirm.</summary>
    bool Confirm(string message);

    /// <summary>One OK button.</summary>
    void Alert(string message);
}
