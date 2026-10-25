using System.IO;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Autosave every 60 s while there are unsaved changes (M5 item 2; decisions A1, A2; ledger upgrade-autosave-recovery, the autosave
/// half, and shell-no-persistence, the autosave clause). A manual tick source and a temp <see cref="AppStorage"/>: no timer, no
/// %LOCALAPPDATA%\RBX15.
/// </summary>
public sealed class AutosaveServiceTests : IDisposable
{
    sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    static readonly DateTimeOffset Noon = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTime Started = new(2026, 10, 25, 9, 30, 0, DateTimeKind.Utc);

    readonly AppStorage _storage = TestShell.Storage();
    readonly ManualTickSource _ticks = new();
    readonly ManualClock _clock = new(Noon);
    readonly EditorSession _session = new();
    readonly AutosaveService _autosave;
    int _writes;

    public AutosaveServiceTests()
    {
        _autosave = new AutosaveService(_session, _storage, _ticks, _clock, "session-a", () => 4242, () => Started)
        {
            BeforeMove = _ => _writes++,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_storage.Root)) Directory.Delete(_storage.Root, recursive: true);
    }

    static SKImage Img(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    /// <summary>One edit: a new layer, so the session is dirty and holds a new document instance.</summary>
    void Edit(SKColor? color = null) =>
        _session.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = _session.TakeId(), Image = Img(color ?? SKColors.Red), Name = "Layer " + d.Layers.Count }) });

    string[] Files() => Directory.Exists(_storage.AutosaveDirectory) ? Directory.GetFiles(_storage.AutosaveDirectory).Select(f => Path.GetFileName(f)).Order().ToArray() : [];

    static void AssertSameContent(Document expected, Document actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.RegionFills, actual.RegionFills);
        Assert.Equal(expected.ShowGrid, actual.ShowGrid);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int i = 0; i < expected.Layers.Count; i++)
        {
            Assert.Equal(expected.Layers[i] with { Image = actual.Layers[i].Image }, actual.Layers[i]);
            using var x = expected.Layers[i].Image.Encode(SKEncodedImageFormat.Png, 100);
            using var y = actual.Layers[i].Image.Encode(SKEncodedImageFormat.Png, 100);
            Assert.True(x.AsSpan().SequenceEqual(y.AsSpan()), "layer pixels");
        }
    }

    [Fact] // A dirty document autosaves on the first tick; a second tick with no change writes nothing; a clean document never writes
    public async Task A_dirty_document_writes_on_the_first_tick_and_not_again_until_it_changes()
    {
        await _autosave.Tick();
        Assert.Equal(0, _writes);                                   // clean: nothing, and not even the folder
        Assert.False(Directory.Exists(_storage.AutosaveDirectory));

        Edit();
        await _autosave.Tick();
        Assert.Equal(1, _writes);
        Assert.Equal(new[] { "session-a.json", "session-a.r15proj" }, Files());

        await _autosave.Tick();
        await _autosave.Tick();
        Assert.Equal(1, _writes);                                   // the same document instance

        Edit(SKColors.Blue);
        await _autosave.Tick();
        Assert.Equal(2, _writes);
    }

    [Fact] // The tick source drives it: raising its event reaches the write
    public async Task The_tick_source_event_runs_a_tick()
    {
        var reached = new TaskCompletionSource();
        _autosave.BeforeMove = _ => reached.TrySetResult();
        Edit();
        _ticks.Raise();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        _autosave.Close();                                          // waits for the write in flight, then clears it
        Assert.Empty(Files());
    }

    [Fact] // A tick inside an open gesture writes nothing, and the next tick after it ends does
    public async Task A_tick_inside_a_gesture_writes_nothing()
    {
        var gesture = _session.BeginGesture();
        Edit();
        await _autosave.Tick();
        Assert.Equal(0, _writes);
        Assert.Empty(Files());

        gesture.Dispose();
        await _autosave.Tick();
        Assert.Equal(1, _writes);
    }

    [Fact] // The file reads back through ProjectIO.Read to the session's document; the sidecar names the pid, start time and original path
    public async Task The_written_file_reads_back_and_the_sidecar_names_its_owner()
    {
        Edit(SKColors.Red);
        Edit(SKColors.Blue);
        _session.Apply(d => d with { ShowGrid = true });
        await _autosave.Tick();

        var back = ProjectIO.Read(File.ReadAllText(_autosave.ProjectPath));
        AssertSameContent(_session.Document, back.Document);
        Assert.Empty(back.DroppedLayers);
        var meta = AutosaveService.ReadSidecar(File.ReadAllText(_autosave.SidecarPath))!;
        Assert.Equal((4242, Started, (string?)null, Noon, AppInfo.Version), (meta.Pid, meta.ProcessStartUtc, meta.OriginalPath, meta.AutosavedAt, meta.AppVersion));
        Assert.DoesNotContain("imgSrc", File.ReadAllText(_autosave.SidecarPath));   // metadata only, no document content
    }

    [Fact] // A titled document records its path in the sidecar, and the autosave never lands beside or over the user's file
    public async Task A_titled_document_autosaves_into_the_autosave_folder_and_names_the_original()
    {
        string original = Path.Combine(_storage.Root, "user", "shirt.r15proj");
        Edit();
        _session.MarkSaved(original, _session.Document);
        Edit(SKColors.Green);
        await _autosave.Tick();

        Assert.Equal(original, AutosaveService.ReadSidecar(File.ReadAllText(_autosave.SidecarPath))!.OriginalPath);
        Assert.Equal(_storage.AutosaveDirectory, Path.GetDirectoryName(_autosave.ProjectPath));
        Assert.False(File.Exists(original));
    }

    [Fact] // A write killed between the temp file and the move leaves the previous autosave intact, no partial target and no temp file
    public async Task A_write_killed_before_the_move_keeps_the_previous_autosave()
    {
        Edit(SKColors.Red);
        await _autosave.Tick();
        string before = File.ReadAllText(_autosave.ProjectPath);
        string sidecarBefore = File.ReadAllText(_autosave.SidecarPath);
        var failures = new List<string>();
        _autosave.Failed += failures.Add;

        _autosave.BeforeMove = temp =>
        {
            Assert.True(File.Exists(temp));                         // the temp file is complete when the kill lands
            throw new IOException("killed");
        };
        Edit(SKColors.Blue);
        await _autosave.Tick();

        Assert.Equal(before, File.ReadAllText(_autosave.ProjectPath));
        Assert.Equal(sidecarBefore, File.ReadAllText(_autosave.SidecarPath));
        Assert.Equal(new[] { "session-a.json", "session-a.r15proj" }, Files());   // no temp file left
        Assert.Equal(new[] { "killed" }, failures);
    }

    [Fact] // A failure is reported once per streak; a success ends the streak; the next failure is reported again
    public async Task A_failure_is_reported_once_per_streak()
    {
        var failures = new List<string>();
        _autosave.Failed += failures.Add;
        _autosave.BeforeMove = _ => throw new IOException("disk full");

        Edit();
        await _autosave.Tick();
        Edit();
        await _autosave.Tick();
        Assert.Equal(new[] { "disk full" }, failures);

        _autosave.BeforeMove = _ => { };
        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);

        _autosave.BeforeMove = _ => throw new IOException("disk full again");
        Edit();
        await _autosave.Tick();
        Assert.Equal(new[] { "disk full", "disk full again" }, failures);
    }

    [Fact] // An unwritable autosave folder is a failure, not a crash
    public async Task An_unwritable_folder_fails_without_throwing()
    {
        Directory.CreateDirectory(_storage.Root);
        File.WriteAllText(_storage.AutosaveDirectory, "a file where the folder should be");
        var failures = new List<string>();
        _autosave.Failed += failures.Add;

        Edit();
        await _autosave.Tick();
        _autosave.WriteNow();

        Assert.Single(failures);
    }

    [Fact] // Save and Open delete the pair: the session reads clean and File is raised
    public async Task A_save_or_an_open_deletes_the_pair()
    {
        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);

        _session.MarkSaved(Path.Combine(_storage.Root, "saved.r15proj"), _session.Document);   // what Save does
        Assert.Empty(Files());

        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);
        var load = new ProjectLoad(new Document(), 1, System.Collections.Immutable.ImmutableList<string>.Empty);
        _session.Open(load, Path.Combine(_storage.Root, "opened.r15proj"));                      // what Open does
        Assert.Empty(Files());
    }

    [Fact] // The real Save and Open (ProjectFileService) clear the pair, through the same File event
    public async Task The_project_file_services_Save_and_Open_delete_the_pair()
    {
        var files = new FakeFileDialogs { SaveProjectResult = Path.Combine(_storage.Root, "user", "shirt.r15proj") };
        Directory.CreateDirectory(Path.GetDirectoryName(files.SaveProjectResult)!);
        var service = new ProjectFileService(_session, files, new FakeDialogService { SaveChoiceAnswer = SaveChoice.DontSave }, new FakeStatus(), new FakeSettingsStore(), release: _ => { });

        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);
        Assert.True(service.Save());
        Assert.Empty(Files());

        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);
        Assert.True(await service.OpenAsync(files.SaveProjectResult));                           // the file Save just wrote; the guard asks, Don't save
        Assert.Empty(Files());
    }

    [Fact] // A File event that leaves the session dirty (the saved state is not the live document) keeps the pair
    public async Task A_dirty_file_event_keeps_the_pair()
    {
        Edit();
        await _autosave.Tick();
        _session.MarkSaved(Path.Combine(_storage.Root, "x.r15proj"), new Document());           // saved a different state: the session is still dirty
        Assert.Equal(2, Files().Length);
    }

    [Fact] // Undoing back to the saved content reads clean with no File event: the next tick removes the stale copy
    public async Task Undoing_back_to_clean_removes_the_stale_pair_on_the_next_tick()
    {
        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);

        Assert.True(_session.Undo());
        Assert.False(_session.IsDirty);
        await _autosave.Tick();
        Assert.Empty(Files());
        Assert.Equal(1, _writes);
    }

    [Fact] // Close deletes the pair and stops further writes; Delete alone does not stop them
    public async Task Close_deletes_the_pair_and_a_later_tick_writes_nothing()
    {
        Edit();
        await _autosave.Tick();
        _autosave.Delete();
        Assert.Empty(Files());

        Edit();
        await _autosave.Tick();
        Assert.Equal(2, Files().Length);

        _autosave.Close();
        Assert.Empty(Files());
        Edit();
        await _autosave.Tick();
        _autosave.WriteNow();
        Assert.Empty(Files());
    }

    [Fact] // WriteNow with a dirty document writes synchronously; clean writes nothing
    public void WriteNow_writes_a_dirty_document_synchronously()
    {
        _autosave.WriteNow();
        Assert.Empty(Files());

        Edit();
        _autosave.WriteNow();
        Assert.Equal(new[] { "session-a.json", "session-a.r15proj" }, Files());
        AssertSameContent(_session.Document, ProjectIO.Read(File.ReadAllText(_autosave.ProjectPath)).Document);
    }

    [Fact] // WriteNow even inside a gesture: a crash writes what is on screen
    public void WriteNow_inside_a_gesture_still_writes()
    {
        using var gesture = _session.BeginGesture();
        Edit();
        _autosave.WriteNow();
        Assert.Equal(2, Files().Length);
    }

    [Fact] // The crash handler's hook is WriteNow: the emergency autosave of a dirty document lands in the pair
    public void The_crash_handlers_emergency_autosave_can_be_this_services_WriteNow()
    {
        var crash = new CrashHandler((_, _) => { }, "log", _ => { }, () => { });
        crash.EmergencyAutosave = _autosave.WriteNow;
        Edit();
        crash.HandleDomain(new InvalidOperationException("boom"));
        Assert.Equal(2, Files().Length);
    }
}

/// <summary>The autosave service as the shell composes it: the status line on a failure, and the window's close (M5 item 2).</summary>
[Collection("MainWindow")]
public sealed class AutosaveWiringTests : IDisposable
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    readonly AppStorage _storage = TestShell.Storage();

    public void Dispose()
    {
        if (Directory.Exists(_storage.Root)) Directory.Delete(_storage.Root, recursive: true);
        else if (File.Exists(_storage.Root)) File.Delete(_storage.Root);
    }

    (MainWindow Window, EditorSession Session, FakeDialogService Dialogs, ManualTickSource Ticks, MainViewModel Main) Shell()
    {
        var session = new EditorSession();
        var dialogs = new FakeDialogService();
        var ticks = new ManualTickSource();
        var store = new FakeSettingsStore();
        var main = TestShell.Quiet(Composition.Build(session, new FakeFileDialogs(), dialogs, new FakeColorPicker(), store, _storage, ticks, new FakeProcessProbe()));
        var window = new MainWindow(main, store, [Primary]);
        StaThread.Pump();
        return (window, session, dialogs, ticks, main);
    }

    string[] Files() => Directory.Exists(_storage.AutosaveDirectory) ? Directory.GetFiles(_storage.AutosaveDirectory) : [];

    [Fact] // A real close deletes the pair (Don't save); a cancelled close keeps it
    public void A_real_close_deletes_the_pair_and_a_cancelled_close_keeps_it() => StaThread.Run(() =>
    {
        var (window, session, dialogs, _, main) = Shell();
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        main.Autosave.WriteNow();
        Assert.Equal(2, Files().Length);

        dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        window.Close();
        Assert.Equal(2, Files().Length);                            // cancelled: the work is still unsaved

        dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        window.Close();
        Assert.Empty(Files());
    });

    [Fact] // A failed autosave shows once on the status strip ("Couldn't autosave: ..."), never a dialog
    public void A_failed_autosave_shows_on_the_status_strip_not_a_dialog() => StaThread.Run(() =>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storage.Root)!);
        File.WriteAllText(_storage.Root, "a file where the root should be");   // CreateDirectory under it fails
        var (_, session, dialogs, _, main) = Shell();
        Assert.True(session.Apply(d => d with { ShowGrid = true }));

        var tick = main.Autosave.Tick();
        while (!tick.IsCompleted) StaThread.Pump();                 // the write runs on the pool; its failure comes back through the dispatcher

        Assert.StartsWith("Couldn't autosave: ", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
    });
}
