using System.Windows;
using Microsoft.Win32;

namespace RBX15.App.Services;

/// <summary>
/// The WPF wrappers over the Windows common file dialogs (no WinForms). Replaces 4.1's hidden
/// <c>&lt;input type=file&gt;</c> elements (editor.html:108-109, :314) and the anchor-download Save As
/// (editor.js:1535-1539; ledger io-download-save-dialog). A native dialog returns on every pick, so
/// re-picking the same file works (file-input-same-file-reselect).
/// </summary>
public sealed class Win32FileDialogs(Func<Window?> owner) : IFileDialogs
{
    public IReadOnlyList<string> OpenImages()
    {
        var dialog = new OpenFileDialog { Filter = ImageImporter.OpenFilter, Multiselect = true };
        return Show(dialog) ? dialog.FileNames : [];
    }

    public string? SavePng(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "PNG image|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    bool Show(CommonDialog dialog) => (owner() is { } o ? dialog.ShowDialog(o) : dialog.ShowDialog()) == true;
}
