namespace RBX15.App.Services;

/// <summary>The unsaved-changes prompt's answer (M4a interfaces R13).</summary>
public enum SaveChoice
{
    Save,
    DontSave,
    Cancel,
}

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

    /// <summary>
    /// The unsaved-changes prompt (R13; new in v5: 4.1 had none, ledger shell-no-unsaved-changes-guard and
    /// shell-unsaved-work-loss): <see cref="DialogText.SaveChanges"/> with Save (Enter), Don't save, and Cancel (Esc and
    /// the caption's close button).
    /// </summary>
    SaveChoice AskSaveChanges(string documentName);
}
