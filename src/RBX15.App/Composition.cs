using System.Windows;
using System.Windows.Interop;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App;

/// <summary>
/// Builds the view-models over one <see cref="EditorSession"/>. Each task that adds a required member
/// to MainViewModel adds its construction line and its member here, at the two anchors, and the same
/// lines with fakes in the tests' TestShell. <paramref name="owner"/> returns the window native
/// dialogs are owned by.
/// </summary>
internal static class Composition
{
    public static MainViewModel Build(EditorSession session, Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(owner);
        var canvas = new CanvasViewModel(session);
        // Task 4: import, export, layers and status (one ImportService serves Add image, the drop zone and the canvas).
        var status = new StatusViewModel();
        var fileDialogs = new Win32FileDialogs(owner);
        var import = new ImportService(session, status);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, new ThemedDialogService(owner), status, import);
        var layers = new LayersViewModel(session, import, fileDialogs);
        // A canvas drop lands every file on the drop point (editor.js:961-986, drop-canvas).
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        // A canvas click selects the topmost visible layer under it, or none (editor.js:1100-1116; Task 2 ruling R5).
        canvas.ClickHandler = p => session.Select(LayerOps.HitTest(session.Document, p.X, p.Y));
        var home = new HomeViewModel(session, new Win32ColorPicker(
            () => owner() is { } w ? new WindowInteropHelper(w).Handle : IntPtr.Zero))
        {
            File = fileGroup,
        };
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            Canvas = canvas,
            Home = home,
            Layers = layers,
            Status = status,
            // M3a: required members above this line.
        };
    }
}
