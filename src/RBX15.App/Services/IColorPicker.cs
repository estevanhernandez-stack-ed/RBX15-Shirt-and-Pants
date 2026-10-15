namespace RBX15.App.Services;

/// <summary>
/// The Fill Style group's Color 1 and Color 2 pickers (editor.html:157-158, ledger
/// <c>fillstyle-color-pickers</c>). Chromium on Windows opens the Win32 ChooseColor dialog for an
/// <c>&lt;input type=color&gt;</c>; v5 opens the same dialog (contract ruling). The view-models see only
/// this interface, so they test headless with a fake.
/// </summary>
public interface IColorPicker
{
    /// <summary>
    /// Shows the picker seeded with <paramref name="initialHex"/> (<c>#rrggbb</c>). Returns the picked
    /// color as lowercase <c>#rrggbb</c>, as an input of type color reports it (ledger
    /// <c>regions-state-model</c>), or null when the user cancels.
    /// </summary>
    string? Pick(string initialHex);
}
