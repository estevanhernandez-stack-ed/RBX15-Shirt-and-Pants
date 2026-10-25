using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// What an autosave leaves beside its project file (M5 decision A2): who owns it and what it is a copy of. Holds paths, times and a
/// version only, never document content (the project file holds that). Recovery (item 3) reads it to tell a crashed owner from a live one.
/// </summary>
/// <param name="Pid">The owning process's id.</param>
/// <param name="ProcessStartUtc">The owning process's start time: with the pid it tells a crashed owner from a reused pid.</param>
/// <param name="OriginalPath">The user's file the document was opened from or saved to, or null for an untitled document.</param>
/// <param name="AutosavedAt">When the project file was written.</param>
/// <param name="AppVersion">The app version that wrote it.</param>
public sealed record AutosaveSidecar(int Pid, DateTime ProcessStartUtc, string? OriginalPath, DateTimeOffset AutosavedAt, string AppVersion);

/// <summary>
/// Autosave (spec §3 "Upgrades"; M5 decisions A1, A2): while the document has unsaved changes, every tick writes it to
/// <c>autosave\&lt;session-id&gt;.r15proj</c> with a <c>&lt;session-id&gt;.json</c> sidecar, never next to or over the user's own file.
/// <list type="bullet">
/// <item>A tick writes only when the session is dirty, no gesture is open, and the document (or its file) is not the one last written.
///   The snapshot (the immutable <see cref="Document"/>, <see cref="EditorSession.CurrentFile"/>, the time) is taken on the UI thread; the
///   encode and the writes run on the thread pool, one at a time (a tick during a write is skipped).</item>
/// <item>Each file is written to a temp file in the same folder and moved over the target, the project file first, so a kill before the
///   moves leaves the previous autosave whole.</item>
/// <item>A Save or an Open (the session reads clean again) and a real close delete the pair; <see cref="Close"/> also stops further writes.</item>
/// <item>A failure raises <see cref="Failed"/> once per failure streak; the shell shows it on the status strip, never a dialog.</item>
/// </list>
/// All file work holds one lock, so <see cref="Delete"/> waits for a write in flight: the encode cannot be reading an image the Open that
/// triggered the delete is about to release, and a finished write cannot resurrect a pair the Save just removed.
/// </summary>
public sealed class AutosaveService
{
    static readonly JsonSerializerOptions SidecarOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly EditorSession _session;
    readonly AppStorage _storage;
    readonly TimeProvider _clock;
    readonly string _sessionId;
    readonly Func<int> _pid;
    readonly Func<DateTime> _processStart;
    readonly object _gate = new();
    Document? _lastDocument;   // what the pair on disk holds; null when there is no pair
    string? _lastFile;
    bool _writing;
    bool _failing;
    bool _closed;
    int _generation;           // bumped by Delete: a write snapshotted before it is dropped

    public AutosaveService(EditorSession session, AppStorage storage, ITickSource ticks, TimeProvider clock, string sessionId, Func<int> pid, Func<DateTime> processStart)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(ticks);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(pid);
        ArgumentNullException.ThrowIfNull(processStart);
        (_session, _storage, _clock, _sessionId, _pid, _processStart) = (session, storage, clock, sessionId, pid, processStart);
        ticks.Tick += (_, _) => _ = Tick();
        // A Save or an Open leaves the session clean with File raised: the autosave is a copy of work that no longer needs one.
        session.Changed += (_, e) =>
        {
            if ((e.What & SessionChange.File) != 0 && !session.IsDirty) Delete();
        };
    }

    /// <summary>This session's autosaved project file.</summary>
    public string ProjectPath => Path.Combine(_storage.AutosaveDirectory, _sessionId + ".r15proj");

    /// <summary>This session's sidecar.</summary>
    public string SidecarPath => Path.Combine(_storage.AutosaveDirectory, _sessionId + ".json");

    /// <summary>Raised on the thread that called <see cref="Tick"/> (the UI thread) with the reason, on the first failure of a streak.</summary>
    public event Action<string>? Failed;

    /// <summary>Test seam: runs with the temp project file's path just before the moves that publish the pair. A throw there is a write killed mid-way.</summary>
    internal Action<string> BeforeMove { get; set; } = _ => { };

    /// <summary>
    /// One tick: writes if there is something new to write. Returns the task of the write (completed at once when the tick writes
    /// nothing). Never throws.
    /// </summary>
    public Task Tick()
    {
        if (_closed || _writing) return Task.CompletedTask;
        if (!_session.IsDirty)
        {
            // Undo back to the saved content reads clean without a File change: the stale copy goes too.
            if (_lastDocument is not null) Delete();
            return Task.CompletedTask;
        }
        if (_session.InGesture) return Task.CompletedTask;
        var snapshot = Snapshot();
        if (ReferenceEquals(snapshot.Document, _lastDocument) && snapshot.File == _lastFile) return Task.CompletedTask;
        _writing = true;
        return WriteInBackground(snapshot);
    }

    /// <summary>
    /// The emergency autosave of a crash (item 1's <c>CrashHandler.EmergencyAutosave</c>): writes a dirty document at once, on the
    /// calling thread. Best effort; never throws.
    /// </summary>
    public void WriteNow()
    {
        try
        {
            if (_closed || !_session.IsDirty) return;
            Write(Snapshot());
        }
        catch (Exception)
        {
            // The process is already failing; a second failure must not hide the first.
        }
    }

    /// <summary>Removes the pair. Safe to call when there is none; waits for a write in flight.</summary>
    public void Delete()
    {
        lock (_gate)
        {
            _generation++;
            _lastDocument = null;
            _lastFile = null;
            Remove(SidecarPath);   // the sidecar goes first: it is what recovery lists
            Remove(ProjectPath);
        }
    }

    /// <summary>The window's real close: stops further writes (a tick after the close must not recreate what the close just removed), then deletes the pair.</summary>
    public void Close()
    {
        _closed = true;
        Delete();
    }

    /// <summary>Reads a sidecar written by this service; null when the text is not one.</summary>
    internal static AutosaveSidecar? ReadSidecar(string json)
    {
        try { return JsonSerializer.Deserialize<AutosaveSidecar>(json, SidecarOptions); }
        catch (JsonException) { return null; }
    }

    /// <summary>The sidecar's text, as <see cref="ReadSidecar"/> reads it back; recovery rewrites a claimed sidecar with it.</summary>
    internal static string WriteSidecar(AutosaveSidecar sidecar) => JsonSerializer.Serialize(sidecar, SidecarOptions);

    sealed record Snap(Document Document, string? File, DateTimeOffset At, int Generation);

    Snap Snapshot() => new(_session.Document, _session.CurrentFile, _clock.GetUtcNow(), Volatile.Read(ref _generation));

    async Task WriteInBackground(Snap snapshot)
    {
        string? failure;
        try
        {
            failure = await Task.Run(() => Write(snapshot));
        }
        catch (Exception e)
        {
            failure = e.Message;
        }
        _writing = false;
        if (failure is null) _failing = false;
        else if (!_failing)
        {
            _failing = true;
            Failed?.Invoke(failure);
        }
    }

    /// <summary>Writes the pair for <paramref name="s"/>. Returns null on success or when a delete superseded it, else the failure reason.</summary>
    string? Write(Snap s)
    {
        string project = ProjectPath, sidecar = SidecarPath;
        string suffix = "." + RandomNumberGenerator.GetHexString(8, lowercase: true) + ".tmp";
        string projectTemp = project + suffix, sidecarTemp = sidecar + suffix;
        lock (_gate)
        {
            if (s.Generation != _generation || _closed) return null;
            try
            {
                Directory.CreateDirectory(_storage.AutosaveDirectory);
                // The bytes a Save writes (no title, R1); the encode is cached per image, so an unchanged layer costs nothing.
                string json = ProjectIO.Write(s.Document, s.At, title: null);
                File.WriteAllText(projectTemp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                var meta = new AutosaveSidecar(_pid(), _processStart().ToUniversalTime(), s.File, s.At, AppInfo.Version);
                File.WriteAllText(sidecarTemp, WriteSidecar(meta), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                BeforeMove(projectTemp);
                File.Move(projectTemp, project, overwrite: true);
                File.Move(sidecarTemp, sidecar, overwrite: true);
                _lastDocument = s.Document;
                _lastFile = s.File;
                return null;
            }
            catch (Exception e)
            {
                Remove(projectTemp);
                Remove(sidecarTemp);
                return e.Message;
            }
        }
    }

    static void Remove(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
