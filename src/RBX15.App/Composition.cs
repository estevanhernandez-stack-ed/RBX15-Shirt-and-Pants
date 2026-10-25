using System.Diagnostics;
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
    /// <summary>How often a dirty document autosaves (spec §3 "Upgrades").</summary>
    public static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(60);

    public static MainViewModel Build(EditorSession session, Func<Window?> owner, ISettingsStore settings, AppStorage storage, ITickSource? autosaveTicks = null, IProcessProbe? processes = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var picker = new ThemedColorPicker(owner);
        var main = Build(session, new Win32FileDialogs(owner), new ThemedDialogService(owner), picker, settings, storage, autosaveTicks ?? new DispatcherTimerTickSource(AutosaveInterval), processes ?? new ProcessProbe(), launcher: new ProcessAppLauncher());
        // Every themed pick (brush, Fill Style colors, region swatches) offers the colors of the user's art.
        picker.Palette = () => main.Palette.Colors;
        return main;
    }

    /// <summary>
    /// The wiring, once: the native services come in, so the app passes the real ones and the tests'
    /// TestShell passes fakes. Every line that connects view-models lives here and nowhere else.
    /// </summary>
    public static MainViewModel Build(EditorSession session, IFileDialogs fileDialogs, IDialogService dialogs, IColorPicker colorPicker, ISettingsStore settings, AppStorage storage, ITickSource autosaveTicks, IProcessProbe processes, Func<Random>? random = null, IAppLauncher? launcher = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fileDialogs);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(colorPicker);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(autosaveTicks);
        ArgumentNullException.ThrowIfNull(processes);
        // M3b: the status strip, the tool and the brush come first; the canvas, the Home tab and the eraser share them.
        var status = new StatusViewModel();
        var tools = new ToolsViewModel();
        // M4b item 10: the Keys toggle; the window's ShortcutRouter reads it, the Tools group shows it (Home carries it).
        var keys = new KeysViewModel();
        // Art palette: the colors of the visible layers, for the Brush group's Palette button and (through the app's picker) every pick.
        var palette = new ArtPalette(session);
        var brush = new BrushViewModel(colorPicker) { Palette = palette };
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
        // v5.1: the 3D avatar preview over the session; the Settings group's "3D preview" box toggles it.
        var avatarPreview = new AvatarPreviewViewModel(session);
        // M4b item 5: Placement (Home) and Presets (Layout) share the app's random source; tests pass a seeded one through <paramref name="random"/>.
        Func<Random> placementRng = random ?? (() => Random.Shared);
        var placement = new PlacementViewModel(session, placementRng);
        // M4b item 6: the Background and BG Settings groups edit the document's background through the shared picker, dialogs and status strip.
        // Fill Style and Template Regions follow them, as on 4.1's Layout tab (Este, 2026-10-25, reversing F1).
        var fillStyle = new FillStyleViewModel(session, colorPicker);
        var layout = new LayoutViewModel
        {
            Presets = new PresetsViewModel(session, placementRng),
            Background = new BackgroundViewModel(session, colorPicker, fileDialogs, status),
            FillStyle = fillStyle,
            Regions = new RegionsGroupViewModel(session, fillStyle),
        };
        var home = new HomeViewModel(session, colorPicker)
        {
            Placement = placement,
            AvatarPreview = avatarPreview,
            Tools = tools,
            Keys = keys,
            Brush = brush,
            File = fileGroup,
        };
        // M4a Task 2: the asset library (editor.html:291-309); its adds go through the one ImportService, so LayersAdded sets Select.
        var assets = new AssetLibraryViewModel(import, fileDialogs, dialogs, status, settings, new PackSource(PackSource.DefaultDirectory), new AssetThumbnailLoader());
        // M4a Task 3: one status line per asset set whose previews fail (diag-console-only-errors; editor.js:2473-2475).
        ThumbnailFailureReport.Attach(assets, status);
        // M4b item 7: the Details tab; a created detail layer sets the Select tool (editor.js:1860-1861), as an import does.
        var details = new DetailsViewModel(session, colorPicker);
        details.LayersAdded += (_, _) => tools.Set(Tool.Select);
        // M4b item 9: the Text tab; an added Word Art layer sets the Select tool (editor.js:2287), as an import does.
        var text = new TextViewModel(session, colorPicker, status);
        text.LayersAdded += (_, _) => tools.Set(Tool.Select);
        // M5 item 2: autosave over the session; a failure shows on the status strip, never a dialog. The session id names this window's pair.
        string sessionId = Guid.NewGuid().ToString("N");
        var autosave = new AutosaveService(session, storage, autosaveTicks, TimeProvider.System, sessionId,
            () => Environment.ProcessId, () => Process.GetCurrentProcess().StartTime);
        autosave.Failed += reason => status.Show($"Couldn't autosave: {reason}");
        // M5 item 3: recovery finds the autosaves of dead windows (a skipped file goes to the error log); the startup flow offers the newest
        // one and claims it into this window's session id, so the restored work keeps autosaving under the id the autosave service writes.
        var recovery = new RecoveryService(storage, processes, () => Environment.ProcessId, () => Process.GetCurrentProcess().StartTime,
            new ErrorLog(storage.LogDirectory, TimeProvider.System).Write);
        var startup = new StartupFlow(session, recovery, dialogs, status, projectFiles, launcher ?? new InertAppLauncher(), sessionId);
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            Canvas = canvas,
            Home = home,
            Layout = layout,
            Details = details,
            Text = text,
            Layers = layers,
            Status = status,
            LayerProperties = layerProperties,
            Files = projectFiles,
            Assets = assets,
            AvatarPreview = avatarPreview,
            Palette = palette,
            Autosave = autosave,
            Recovery = recovery,
            Startup = startup,
            // M3a: required members above this line.
        };
    }

    /// <summary>The injectable overload's default: a build that names no launcher never starts a process (the app overload passes the real one).</summary>
    sealed class InertAppLauncher : IAppLauncher
    {
        public void Open(string path)
        {
        }
    }
}
