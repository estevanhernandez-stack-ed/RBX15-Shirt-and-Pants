namespace RBX15.App.Services;

/// <summary>
/// The dialog and status strings 4.1 showed, verbatim (ledger dialogs-inventory, io-alert-inventory), and the
/// new unsaved-changes prompt. M4a Task 1 owns every entry; Tasks 2 and 3 consume them. The two strings 4.1 already
/// had a home for stay where they are: <c>ExportGuard.EmptyTemplateMessage</c> (editor.js:1506) and
/// <c>CanvasInteraction.NoLayerToErase</c> (editor.js:1270, shown on the status strip).
/// </summary>
public static class DialogText
{
    /// <summary>editor.js:2344, loadBuiltinPack with no pack folder (Task 2).</summary>
    public static string PackNotFound(string packName) => $"Built-in pack \"{packName}\" not found. Reinstall the app to restore it.";

    /// <summary>editor.js:2364, loadBuiltinPack with an empty pack folder (Task 2).</summary>
    public static string PackEmpty(string packName) => $"Built-in pack \"{packName}\" is empty.";

    /// <summary>editor.js:2385, Open folder with no images (Task 2; R14 keeps the text though BMP is accepted).</summary>
    public const string NoImagesInFolder = "No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files.";

    /// <summary>
    /// editor.js:2676, <c>alert('Failed to load project. ' + err.message)</c>. v5's message is the
    /// <c>ProjectFormatException</c>'s or the read error's (ledger project-load-flow portTarget: "message text will differ").
    /// </summary>
    public static string LoadFailed(string message) => "Failed to load project. " + message;

    /// <summary>The unsaved-changes prompt (R13): the Windows wording, since 4.1 had no prompt.</summary>
    public static string SaveChanges(string documentName) => $"Do you want to save changes to {documentName}?";

    /// <summary>The document name before the first save, in the prompt and the window title (R3).</summary>
    public const string Untitled = "Untitled";

    /// <summary>The crash dialog (M5 decision E2): the work was autosaved, the log names the failure.</summary>
    public static string FatalError(string logPath) =>
        $"Something went wrong and RBX15 has to close. Your work was autosaved and will be offered next time you open the app. Details: {logPath}";
}
