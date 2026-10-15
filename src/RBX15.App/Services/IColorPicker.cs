namespace RBX15.App.Services;

/// <summary>
/// The Fill Style group's Color 1 and Color 2 pickers (editor.html:157-158, ledger
/// <c>fillstyle-color-pickers</c>, fix). 4.1's <c>&lt;input type=color&gt;</c> opens Chromium's Win32
/// ChooseColor dialog, which cannot be themed; v5 opens its own themed popup with live preview
/// (Este, 2026-10-14). The view-models see only this interface, so they test headless with a fake.
/// </summary>
public interface IColorPicker
{
    /// <summary>
    /// Shows the picker seeded with <paramref name="initialHex"/> (<c>#rrggbb</c>) and blocks until it
    /// closes. While it is open, every color change is passed to <paramref name="preview"/> as lowercase
    /// <c>#rrggbb</c>. Returns the picked color as lowercase <c>#rrggbb</c>, as an input of type color
    /// reports it (ledger <c>regions-state-model</c>), or null when the user cancels. The caller owns
    /// reverting a cancelled preview. <paramref name="anchor"/> is the element the picker opens under
    /// (the swatch button); null places it by the owner.
    /// </summary>
    string? Pick(string initialHex, Action<string>? preview = null, object? anchor = null);
}
