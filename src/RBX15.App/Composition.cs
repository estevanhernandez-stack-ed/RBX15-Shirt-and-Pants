using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// Builds the view-models over one <see cref="EditorSession"/>. Each task that adds a required member
/// to MainViewModel adds its construction line and its member here, at the two anchors, and the tests'
/// TestShell reuses them through the injectable overload. <paramref name="owner"/> returns the window dialogs and
/// the color picker are owned by.
/// </summary>
internal static class Composition
{
    public static MainViewModel Build(EditorSession session, Func<Window?> owner, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Build(session, new Win32FileDialogs(owner), new ThemedDialogService(owner), new ThemedColorPicker(owner), settings);
    }

    /// <summary>
    /// The wiring, once: the native services come in, so the app passes the real ones and the tests'
    /// TestShell passes fakes. Every line that connects view-models lives here and nowhere else.
    /// </summary>
    public static MainViewModel Build(EditorSession session, IFileDialogs fileDialogs, IDialogService dialogs, IColorPicker colorPicker, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fileDialogs);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(colorPicker);
        ArgumentNullException.ThrowIfNull(settings);
        // M3b: the status strip, the tool and the brush come first; the canvas, the Home tab and the eraser share them.
        var status = new StatusViewModel();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(colorPicker);
        var canvas = new CanvasViewModel(session, tools, brush, status);
        // Task 4: import, export, layers and status (one ImportService serves Add image, the drop zone and the canvas).
        var import = new ImportService(session, status);
        // addLayer and selectLayer set the Select tool; a drawing commit keeps the drawing tool (editor.js:523, :577; R8).
        import.LayersAdded += (_, _) => tools.Set(Tool.Select);
        // M4a Task 1: Save, Save As, Open and the unsaved-changes guard over the session (spec §3).
        var projectFiles = new ProjectFileService(session, fileDialogs, dialogs, status, settings);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, dialogs, status, import, projectFiles);
        var layers = new LayersViewModel(session, import, fileDialogs);
        layers.LayerPicked += (_, _) => tools.Set(Tool.Select);
        // A canvas drop lands every file on the drop point (editor.js:961-986, drop-canvas).
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        // M3b Task 3: the "Selected layer" panel (editor.html:320-323).
        var layerProperties = new LayerPropertiesViewModel(session);
        var home = new HomeViewModel(session, colorPicker)
        {
            Tools = tools,
            Brush = brush,
            File = fileGroup,
        };
        // M4a Task 2: the asset library (editor.html:291-309); its adds go through the one ImportService, so LayersAdded sets Select.
        var assets = new AssetLibraryViewModel(import, fileDialogs, dialogs, status, settings, new PackSource(PackSource.DefaultDirectory), new AssetThumbnailLoader());
        // M4a Task 3: one status line per asset set whose previews fail (diag-console-only-errors; editor.js:2473-2475).
        ThumbnailFailureReport.Attach(assets, status);
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            Canvas = canvas,
            Home = home,
            Layers = layers,
            Status = status,
            LayerProperties = layerProperties,
            Files = projectFiles,
            Assets = assets,
            // M3a: required members above this line.
        };
    }
}
