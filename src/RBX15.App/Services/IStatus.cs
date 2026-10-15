namespace RBX15.App.Services;

/// <summary>
/// The non-blocking status strip (spec §4): failures 4.1 only wrote to the developer console
/// (ledger diag-console-only-errors) and the result of an export (io-download-save-dialog).
/// </summary>
public interface IStatus
{
    /// <summary>Shows <paramref name="message"/>, replacing any message already shown.</summary>
    void Show(string message);

    /// <summary>Hides the strip.</summary>
    void Clear();
}
