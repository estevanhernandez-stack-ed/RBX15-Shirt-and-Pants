namespace RBX15.App.Services;

/// <summary>The native file pickers that replace 4.1's hidden file inputs and anchor downloads.</summary>
public interface IFileDialogs
{
    /// <summary>
    /// Multi-select image picker (Add image, drop-zone click). Empty on cancel. The order is the
    /// order the dialog returns its file names in, which v5 defines as "the order picked" (spec §3).
    /// </summary>
    IReadOnlyList<string> OpenImages();

    /// <summary>Save dialog for the exported PNG, starting at <paramref name="suggestedFileName"/>. Null on cancel.</summary>
    string? SavePng(string suggestedFileName);

    /// <summary>
    /// The Open dialog (btnLoadProject and #projectFileInput, editor.js:2683, editor.html:108; ledger project-load-flow):
    /// single select, the filters of <see cref="Win32FileDialogs.ProjectOpenFilter"/> (interfaces §2.2), starting in
    /// <paramref name="initialDirectory"/> when it exists. Null on cancel.
    /// </summary>
    string? OpenProject(string? initialDirectory);

    /// <summary>
    /// The project Save dialog (saveProject's anchor download, editor.js:2584-2590; ledger project-save-filename, keep):
    /// FileName = <paramref name="suggestedFileName"/>, filter <see cref="Win32FileDialogs.ProjectSaveFilter"/>,
    /// DefaultExt .r15proj, AddExtension and OverwritePrompt, starting in <paramref name="initialDirectory"/> when it
    /// exists. Null on cancel.
    /// </summary>
    string? SaveProject(string suggestedFileName, string? initialDirectory);

    /// <summary>
    /// The native folder picker (Open folder, 4.1's webkitdirectory input, editor.js:2378-2381; ledger io-folder-picker):
    /// one folder, starting in <paramref name="initialDirectory"/> when it exists. Null on cancel. Task 2 calls it.
    /// </summary>
    string? PickFolder(string? initialDirectory);
}
