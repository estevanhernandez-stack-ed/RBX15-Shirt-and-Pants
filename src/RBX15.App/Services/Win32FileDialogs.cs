using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace RBX15.App.Services;

/// <summary>
/// The WPF wrappers over the Windows common file dialogs (no WinForms). Replaces 4.1's hidden
/// <c>&lt;input type=file&gt;</c> elements (editor.html:108-109, :314) and the anchor-download Save As
/// (editor.js:1535-1539, :2584-2590; ledger io-download-save-dialog). A native dialog returns on every pick, so
/// re-picking the same file works (file-input-same-file-reselect).
/// </summary>
public sealed class Win32FileDialogs(Func<Window?> owner) : IFileDialogs
{
    /// <summary>
    /// The Open filter. 4.1's input accepted <c>.json,.r15proj</c> (editor.html:108); the contract names .r15proj, so it
    /// comes first and JSON keeps 4.1's reach as a second entry (interfaces §2.2).
    /// </summary>
    public const string ProjectOpenFilter = "RBX15 project (*.r15proj)|*.r15proj|JSON project (*.json)|*.json";

    /// <summary>The Save filter (ledger project-save-filename portTarget).</summary>
    public const string ProjectSaveFilter = "RBX15 project (*.r15proj)|*.r15proj";

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

    public string? OpenProject(string? initialDirectory)
    {
        var dialog = new OpenFileDialog { Filter = ProjectOpenFilter, Multiselect = false };
        StartIn(dialog, initialDirectory);
        return Show(dialog) ? dialog.FileName : null;
    }

    public string? SaveProject(string suggestedFileName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = ProjectSaveFilter,
            DefaultExt = ".r15proj",
            AddExtension = true,
            OverwritePrompt = true,
        };
        StartIn(dialog, initialDirectory);
        return Show(dialog) ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog { Multiselect = false };
        StartIn(dialog, initialDirectory);
        return Show(dialog) ? dialog.FolderName : null;
    }

    /// <summary>A remembered folder that has since gone is ignored, so the dialog opens where Windows chooses.</summary>
    static void StartIn(CommonItemDialog dialog, string? directory)
    {
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) dialog.InitialDirectory = directory;
    }

    bool Show(CommonDialog dialog) => (owner() is { } o ? dialog.ShowDialog(o) : dialog.ShowDialog()) == true;
}
