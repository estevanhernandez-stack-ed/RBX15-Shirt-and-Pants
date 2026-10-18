using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>
/// Save work, Save As and Open over the session (spec §3 "Saving": a current file, Ctrl+S, Ctrl+Shift+S, native
/// dialogs, last folder remembered), and the unsaved-changes guard (Este's ruling 2026-10-16; interfaces R13).
/// <list type="bullet">
/// <item><b>Save</b> (saveProject, editor.js:2550-2591): ProjectIO.Write with no title (R1), written atomically
///   (a temp file in the same folder, then a replace or move over the target), then the session is marked saved and the
///   status strip names the file. 4.1 wrote through an anchor download with no feedback (io-download-save-dialog).</item>
/// <item><b>Open</b> (loadProject, editor.js:2593-2680): the read, parse and decode run off the UI thread (R4),
///   all-or-nothing; a failure is 4.1's alert text with v5's message (editor.js:2676) and changes nothing; the guard
///   asks only after a good read; dropped layers are named on the status strip (project-load-error-path-bugs).</item>
/// </list>
/// UI thread, except the read.
/// </summary>
public sealed class ProjectFileService : ObservableObject
{
    readonly EditorSession _session;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ISettingsStore _settings;
    readonly TimeProvider _clock;
    readonly Func<string, CancellationToken, Task<ProjectLoad>> _read;
    readonly Action<IReadOnlyCollection<SKImage>> _release;
    CancellationTokenSource? _pending;
    bool _isBusy;

    /// <param name="clock">savedAt's clock; TimeProvider.System by default.</param>
    /// <param name="read">The off-thread read; by default <see cref="ReadFile"/> on the thread pool.</param>
    /// <param name="release">Frees images nothing references any more; by default <see cref="ReleaseOnIdle"/> (R11).</param>
    public ProjectFileService(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status, ISettingsStore settings,
        TimeProvider? clock = null,
        Func<string, CancellationToken, Task<ProjectLoad>>? read = null,
        Action<IReadOnlyCollection<SKImage>>? release = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(settings);
        (_session, _files, _dialogs, _status, _settings) = (session, files, dialogs, status, settings);
        _clock = clock ?? TimeProvider.System;
        _read = read ?? ((path, cancel) => Task.Run(() => ReadFile(path), cancel));
        var dispatcher = Dispatcher.CurrentDispatcher;
        _release = release ?? (images => ReleaseOnIdle(dispatcher, images));
    }

    /// <summary>True while <see cref="OpenAsync(string)"/> awaits its read (R4). The window blocks input and every file command waits.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>The name the prompt and the window title use: the file name, or <see cref="DialogText.Untitled"/>.</summary>
    public static string DocumentName(string? currentFile) =>
        string.IsNullOrEmpty(currentFile) ? DialogText.Untitled : Path.GetFileName(currentFile);

    /// <summary>
    /// The largest project file Open reads, in bytes (512 MiB). ProjectIO caps each image (100 MiB encoded, 50 MP) but
    /// not the file, so a hostile or corrupt file could otherwise be read whole into memory and crash the app with the
    /// user's unsaved work on screen (T1-R14). New v5 surface; provisional.
    /// </summary>
    internal const long MaxProjectBytes = 512L * 1024 * 1024;

    /// <summary>
    /// The default read: refuses a file over <paramref name="maxBytes"/> before reading it, then the whole file as
    /// UTF-8 and <see cref="ProjectIO.Read"/> (parse, validate and capped decode).
    /// </summary>
    internal static ProjectLoad ReadFile(string path, long maxBytes = MaxProjectBytes)
    {
        long length = new FileInfo(path).Length;
        if (length > maxBytes)
            throw new ProjectFormatException(string.Create(CultureInfo.InvariantCulture, $"the file is {length / 1048576.0:0.#} MB; the limit is {maxBytes / 1048576.0:0.#} MB"));
        return ProjectIO.Read(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>Save work (btnSaveProject, editor.js:2682): to the current file, or Save As when there is none. True iff written.</summary>
    public bool Save() => _session.CurrentFile is { } path ? WriteTo(path) : SaveAs();

    /// <summary>
    /// Save As (Ctrl+Shift+S; no button, interfaces §2.1). Suggests the current file's name, else
    /// <see cref="ExportNaming.ProjectFileName"/> of the template name (editor.js:2586-2587; project-save-filename, keep),
    /// starting in the current file's folder, else the remembered one. False on cancel or a failed write.
    /// </summary>
    public bool SaveAs()
    {
        string suggested = _session.CurrentFile is { } current ? Path.GetFileName(current) : ExportNaming.ProjectFileName(_session.TemplateTitle);
        var path = _files.SaveProject(suggested, InitialDirectory());
        return path is not null && WriteTo(path);
    }

    /// <summary>Open (btnLoadProject, editor.js:2683-2687): the Open dialog, then <see cref="OpenAsync(string)"/>. False on cancel, a failure or a declined guard.</summary>
    public async Task<bool> OpenAsync()
    {
        if (IsBusy) return false;
        var path = _files.OpenProject(InitialDirectory());
        return path is not null && await OpenAsync(path);
    }

    /// <summary>
    /// Opens <paramref name="path"/> (R4 order): the read off the UI thread; a failure alerts
    /// <see cref="DialogText.LoadFailed"/> and changes nothing; then the unsaved-changes guard, so nobody is asked to
    /// save for a file that cannot open; a declined guard releases the loaded images. On success the session swaps
    /// (<see cref="EditorSession.Open"/>), the outgoing images are released, the folder is remembered, and the status
    /// strip names the dropped layers or clears.
    /// </summary>
    public async Task<bool> OpenAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (IsBusy) return false;
        using var cancel = new CancellationTokenSource();
        _pending = cancel;
        IsBusy = true;
        ProjectLoad? load = null;
        Exception? failure = null;
        try
        {
            load = await _read(path, cancel.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
        }
        catch (Exception e) when (e is ProjectFormatException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            failure = e;
        }
        finally
        {
            _pending = null;
            IsBusy = false;
        }

        if (cancel.IsCancellationRequested)
        {
            if (load is not null) _release(ImagesOf(load.Document));
            return false;
        }
        if (failure is not null)
        {
            _dialogs.Alert(DialogText.LoadFailed(failure.Message));
            return false;
        }
        if (!ConfirmDiscardChanges())
        {
            _release(ImagesOf(load!.Document));
            return false;
        }
        _release(_session.Open(load!, path));
        RememberFolder(path);
        string name = Path.GetFileName(path);
        if (load!.DroppedLayers.IsEmpty) _status.Clear();
        else _status.Show($"Opened {name}. Couldn't load: {string.Join(", ", load.DroppedLayers)}.");
        return true;
    }

    /// <summary>
    /// The unsaved-changes guard (R13): clean goes on with no prompt; Save runs <see cref="Save"/> and goes on only if it
    /// wrote (a cancelled Save As or a failed write cancels the whole action); Don't save goes on; Cancel stops.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_session.IsDirty) return true;
        return _dialogs.AskSaveChanges(DocumentName(_session.CurrentFile)) switch
        {
            SaveChoice.Save => Save(),
            SaveChoice.DontSave => true,
            _ => false,
        };
    }

    /// <summary>Cancels a pending read; its result is released, not applied. The window calls it when it closes.</summary>
    public void CancelPendingOpen() => _pending?.Cancel();

    string? InitialDirectory() =>
        _session.CurrentFile is { } current ? Path.GetDirectoryName(current) : _settings.LoadFolder(SettingsFolder.Project);

    void RememberFolder(string path)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir) _settings.SaveFolder(SettingsFolder.Project, dir);
    }

    bool WriteTo(string path)
    {
        _session.CommitPendingEdits();
        var doc = _session.Document;
        string name = Path.GetFileName(path);
        string temp = path + "." + RandomNumberGenerator.GetHexString(8, lowercase: true) + ".tmp";
        try
        {
            string json = ProjectIO.Write(doc, _clock.GetUtcNow(), title: null);
            File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            // Replace keeps an existing target's attributes, ACL and alternate streams; Move only when there is none (T1-R15).
            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
            else File.Move(temp, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
            _status.Show($"Couldn't save {name}: {e.Message}");
            return false;
        }
        _session.MarkSaved(path, doc);
        RememberFolder(path);
        _status.Show($"Saved {name}.");
        return true;
    }

    static IReadOnlyCollection<SKImage> ImagesOf(Document doc)
    {
        var images = new List<SKImage>(doc.Layers.Count + 1);
        if (doc.BgImage is { } bg) images.Add(bg);
        images.AddRange(doc.Layers.Select(l => l.Image));
        return images;
    }

    /// <summary>
    /// R11: frees the images at ApplicationIdle, after the host has rendered the new document and every view-model has
    /// rebuilt, so nothing draws a disposed image.
    /// </summary>
    static void ReleaseOnIdle(Dispatcher dispatcher, IReadOnlyCollection<SKImage> images)
    {
        if (images.Count == 0) return;
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            foreach (var image in images) Images.Release(image);
        });
    }
}
