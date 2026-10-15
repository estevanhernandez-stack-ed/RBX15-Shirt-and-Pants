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
}
