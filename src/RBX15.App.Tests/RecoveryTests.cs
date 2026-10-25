using System.Globalization;
using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The pieces the recovery tests share: a killed window's autosave, and the pixel comparison a read-back needs.</summary>
public abstract class RecoveryTestBase : IDisposable
{
    protected sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    protected static readonly DateTimeOffset Noon = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    protected static readonly DateTime DeadStart = new(2026, 10, 25, 9, 30, 0, DateTimeKind.Utc);
    protected static readonly DateTime MyStart = new(2026, 10, 25, 11, 0, 0, DateTimeKind.Utc);
    protected const int MyPid = 4242;

    protected readonly AppStorage Storage = TestShell.Storage();
    protected readonly FakeProcessProbe Probe = new();

    public void Dispose()
    {
        if (Directory.Exists(Storage.Root)) Directory.Delete(Storage.Root, recursive: true);
    }

    protected static SKImage Img(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    /// <summary>
    /// A window that was killed: its own session and autosave service under <paramref name="id"/>, a pid nobody runs, one autosave
    /// written. <paramref name="original"/> makes it a titled document (saved there, then edited).
    /// </summary>
    protected Crashed Crash(string id, DateTimeOffset at, int pid = 1111, string? original = null, SKColor? color = null)
    {
        var session = new EditorSession();
        var autosave = new AutosaveService(session, Storage, new ManualTickSource(), new FixedClock(at), id, () => pid, () => DeadStart);
        void Edit() => session.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = session.TakeId(), Image = Img(color ?? SKColors.Red), Name = "Crashed " + id }) });
        if (original is not null)
        {
            Edit();
            session.MarkSaved(original, session.Document);
        }
        Edit();
        autosave.WriteNow();
        Assert.True(File.Exists(autosave.ProjectPath));
        return new Crashed(session.Document, autosave.ProjectPath, autosave.SidecarPath);
    }

    protected sealed record Crashed(Document Document, string Project, string Sidecar);

    protected string[] AutosaveFiles() =>
        Directory.Exists(Storage.AutosaveDirectory) ? Directory.GetFiles(Storage.AutosaveDirectory).Select(f => Path.GetFileName(f)).Order().ToArray() : [];

    protected static void AssertSameContent(Document expected, Document actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.RegionFills, actual.RegionFills);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int i = 0; i < expected.Layers.Count; i++)
        {
            Assert.Equal(expected.Layers[i] with { Image = actual.Layers[i].Image }, actual.Layers[i]);
            using var x = expected.Layers[i].Image.Encode(SKEncodedImageFormat.Png, 100);
            using var y = actual.Layers[i].Image.Encode(SKEncodedImageFormat.Png, 100);
            Assert.True(x.AsSpan().SequenceEqual(y.AsSpan()), "layer pixels");
        }
    }
}

/// <summary>
/// Finding, claiming, releasing and deleting orphaned autosaves (M5 item 3; decisions R1, R2; ledger upgrade-autosave-recovery, the
/// recovery half). A temp <see cref="AppStorage"/> and a fake process probe: no real process is looked at and no
/// %LOCALAPPDATA%\RBX15 is touched.
/// </summary>
public sealed class RecoveryServiceTests : RecoveryTestBase
{
    readonly List<(Exception, string)> _logged = [];

    RecoveryService Service() => new(Storage, Probe, () => MyPid, () => MyStart, (e, source) => _logged.Add((e, source)));

    [Fact] // A killed owner's pair is an orphan; a live owner's (same pid and start time) never is
    public void A_dead_owners_pair_is_an_orphan_and_a_live_owners_is_not()
    {
        Crash("dead", Noon, pid: 1111);
        Crash("alive", Noon, pid: 2222);
        Probe.Running.Add((2222, DeadStart));

        var orphan = Assert.Single(Service().FindOrphans());

        Assert.Equal("dead", orphan.SessionId);
        Assert.Equal(1111, orphan.Sidecar.Pid);
        Assert.Equal("Untitled", orphan.DocumentName);
        Assert.Empty(_logged);
    }

    [Fact] // A pid that a different process now holds (another start time) is a dead owner
    public void A_reused_pid_is_a_dead_owner()
    {
        Crash("reused", Noon, pid: 3333);
        Probe.Running.Add((3333, DeadStart.AddHours(5)));   // the pid is running, but it started later: not the writer

        Assert.Equal("reused", Assert.Single(Service().FindOrphans()).SessionId);
    }

    [Fact] // Newest first (R2)
    public void Orphans_come_newest_first()
    {
        Crash("old", Noon.AddHours(-3), pid: 1);
        Crash("new", Noon, pid: 2);
        Crash("mid", Noon.AddHours(-1), pid: 3);

        Assert.Equal(["new", "mid", "old"], Service().FindOrphans().Select(o => o.SessionId));
    }

    [Fact] // An unreadable sidecar, and a sidecar with no project file, are skipped and logged and never deleted
    public void An_unreadable_sidecar_or_a_pair_with_no_project_is_skipped_logged_and_kept()
    {
        Directory.CreateDirectory(Storage.AutosaveDirectory);
        File.WriteAllText(Path.Combine(Storage.AutosaveDirectory, "garbled.json"), "{ not json");
        File.WriteAllText(Path.Combine(Storage.AutosaveDirectory, "garbled.r15proj"), "{}");
        File.WriteAllText(Path.Combine(Storage.AutosaveDirectory, "empty.json"), "{}");   // parses, names no owner
        Crash("headless", Noon, pid: 5);
        File.Delete(Path.Combine(Storage.AutosaveDirectory, "headless.r15proj"));          // a sidecar with no project

        Assert.Empty(Service().FindOrphans());

        Assert.Equal(3, _logged.Count);
        Assert.All(_logged, l => Assert.Equal("recovery", l.Item2));
        Assert.Equal(new[] { "empty.json", "garbled.json", "garbled.r15proj", "headless.json" }, AutosaveFiles());
    }

    [Fact] // A claim in progress (.json.claim) and stray temp files are not sidecars
    public void Only_json_sidecars_are_listed()
    {
        var crashed = Crash("dead", Noon);
        File.Copy(crashed.Sidecar, Path.Combine(Storage.AutosaveDirectory, "other.json.claim"));
        File.Copy(crashed.Sidecar, Path.Combine(Storage.AutosaveDirectory, "other.json.1a2b.tmp"));

        Assert.Equal(["dead"], Service().FindOrphans().Select(o => o.SessionId));
    }

    [Fact] // No folder is no orphans, not an error
    public void A_missing_autosave_folder_has_no_orphans()
    {
        Assert.Empty(Service().FindOrphans());
        Assert.Empty(_logged);
    }

    [Fact] // R2: of two claims on the same orphan, exactly one wins
    public void Two_claims_on_the_same_orphan_have_exactly_one_winner()
    {
        Crash("dead", Noon);
        var service = Service();
        var orphan = Assert.Single(service.FindOrphans());

        Assert.True(service.TryClaim(orphan, "first", out var claimed));
        Assert.NotNull(claimed);
        Assert.False(service.TryClaim(orphan, "second", out var lost));
        Assert.Null(lost);
        Assert.Equal(new[] { "first.json", "first.r15proj" }, AutosaveFiles());
    }

    [Fact] // The same race with real contenders: eight claims at once, one winner, no leftovers
    public void Concurrent_claims_have_exactly_one_winner()
    {
        Crash("dead", Noon);
        var orphan = Assert.Single(Service().FindOrphans());
        int wins = 0;

        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            if (Service().TryClaim(orphan, "claimer-" + i, out _)) Interlocked.Increment(ref wins);
        });

        Assert.Equal(1, wins);
        Assert.Equal(2, AutosaveFiles().Length);                  // one pair, under the winner's id
        Assert.DoesNotContain(AutosaveFiles(), f => f.EndsWith(".claim") || f.EndsWith(".claim-lock") || f.EndsWith(".tmp"));
    }

    [Fact] // A claimed pair is renamed to the claimer's id and owned by the claimer, so another launch does not offer it
    public void A_claimed_pair_is_owned_by_the_claimer_and_no_other_launch_is_offered_it()
    {
        var crashed = Crash("dead", Noon, original: Path.Combine(Storage.Root, "user", "shirt.r15proj"));
        var service = Service();
        Assert.True(service.TryClaim(Assert.Single(service.FindOrphans()), "mine", out var claimed));

        Assert.Equal(new[] { "mine.json", "mine.r15proj" }, AutosaveFiles());
        Assert.False(File.Exists(crashed.Project));
        Assert.Equal((MyPid, MyStart, Path.Combine(Storage.Root, "user", "shirt.r15proj"), Noon),
            (claimed!.Sidecar.Pid, claimed.Sidecar.ProcessStartUtc, claimed.Sidecar.OriginalPath, claimed.Sidecar.AutosavedAt));
        Assert.Equal(claimed.Sidecar, AutosaveService.ReadSidecar(File.ReadAllText(claimed.SidecarPath)));

        Probe.Running.Add((MyPid, MyStart));                       // the claimer is alive
        Assert.Empty(Service().FindOrphans());
    }

    [Fact] // Risk (item 3): a crash during startup leaves the claimed file to be offered again
    public void A_claimer_that_dies_leaves_the_pair_to_be_offered_again()
    {
        Crash("dead", Noon);
        var service = Service();
        Assert.True(service.TryClaim(Assert.Single(service.FindOrphans()), "mine", out _));

        // The claimer never runs again: the probe does not list it, so its pair is an orphan under its own id.
        var again = Assert.Single(Service().FindOrphans());
        Assert.Equal("mine", again.SessionId);
    }

    [Fact] // Release puts the project and the dead owner's sidecar back
    public void Release_gives_the_pair_back_to_the_dead_owner()
    {
        var crashed = Crash("dead", Noon, pid: 1111);
        string sidecarBefore = File.ReadAllText(crashed.Sidecar);
        string projectBefore = File.ReadAllText(crashed.Project);
        var service = Service();
        Assert.True(service.TryClaim(Assert.Single(service.FindOrphans()), "mine", out var claimed));

        service.Release(claimed!);

        Assert.Equal(new[] { "dead.json", "dead.r15proj" }, AutosaveFiles());
        Assert.Equal(sidecarBefore, File.ReadAllText(crashed.Sidecar));
        Assert.Equal(projectBefore, File.ReadAllText(crashed.Project));
        Assert.Equal("dead", Assert.Single(Service().FindOrphans()).SessionId);
    }

    [Fact] // Discard
    public void Delete_removes_the_pair()
    {
        Crash("dead", Noon);
        var service = Service();
        Assert.True(service.TryClaim(Assert.Single(service.FindOrphans()), "mine", out var claimed));

        service.Delete(claimed!);

        Assert.Empty(AutosaveFiles());
    }

    [Fact] // Load reads the autosave back through ProjectIO.Read
    public void Load_reads_the_claimed_project()
    {
        var crashed = Crash("dead", Noon, color: SKColors.Blue);
        var service = Service();
        Assert.True(service.TryClaim(Assert.Single(service.FindOrphans()), "mine", out var claimed));

        var load = service.Load(claimed!);

        AssertSameContent(crashed.Document, load.Document);
    }

    [Fact] // A failed read throws and touches nothing
    public void A_corrupt_project_fails_to_load_and_stays()
    {
        var crashed = Crash("dead", Noon);
        File.WriteAllText(crashed.Project, "{ truncated");
        var service = Service();
        Assert.True(service.TryClaim(Assert.Single(service.FindOrphans()), "mine", out var claimed));

        Assert.Throws<ProjectFormatException>(() => service.Load(claimed!));

        Assert.Equal(new[] { "mine.json", "mine.r15proj" }, AutosaveFiles());
    }
}

/// <summary>
/// The launch's recovery step through the real composition (M5 item 3; decision R1; ledger upgrade-autosave-recovery, the recovery
/// half, and shell-no-unsaved-changes-guard): the prompt, Restore, Discard, Later, and a restored session that keeps autosaving.
/// </summary>
public sealed class StartupFlowTests : RecoveryTestBase
{
    (EditorSession Session, MainViewModel Main, FakeDialogService Dialogs, FakeFileDialogs Files) Shell()
    {
        var session = new EditorSession();
        var dialogs = new FakeDialogService();
        var files = new FakeFileDialogs();
        var main = TestShell.Quiet(Composition.Build(session, files, dialogs, new FakeColorPicker(), new FakeSettingsStore(), Storage, new ManualTickSource(), Probe));
        return (session, main, dialogs, files);
    }

    static T Await<T>(Task<T> task)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (clock.ElapsedMilliseconds > 30_000) throw new TimeoutException("the dispatcher work did not finish in 30 s");
            StaThread.Pump();
            task.Wait(TimeSpan.FromMilliseconds(5));
        }
        return task.GetAwaiter().GetResult();
    }

    static void Await(Task task) => Await(task.ContinueWith(_ => 0, TaskScheduler.Default));

    [Fact] // No orphan: nothing is asked
    public void With_nothing_to_recover_nothing_is_asked() => StaThread.Run(() =>
    {
        var (session, main, dialogs, _) = Shell();

        Assert.Equal(RecoveryOutcome.None, Await(main.Startup.RecoverAsync()));

        Assert.Empty(dialogs.Restores);
        Assert.False(session.IsDirty);
    });

    [Fact] // R1: one prompt, naming the document ("Untitled" when it has no file) and the autosave time
    public void The_prompt_shows_once_with_the_name_and_the_time() => StaThread.Run(() =>
    {
        string original = Path.Combine(Storage.Root, "user", "shirt.r15proj");
        Crash("titled", Noon.AddHours(-1), pid: 1, original: original);
        var (_, main, dialogs, _) = Shell();

        Await(main.Startup.RecoverAsync());

        Assert.Equal([("shirt.r15proj", Noon.AddHours(-1))], dialogs.Restores);
    });

    [Fact] // R1: Restore opens the untitled autosave dirty (the title's *), with no history and no file
    public void Restore_opens_an_untitled_autosave_dirty_with_empty_history() => StaThread.Run(() =>
    {
        var crashed = Crash("dead", Noon, color: SKColors.Lime);
        var (session, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;

        Assert.Equal(RecoveryOutcome.Restored, Await(main.Startup.RecoverAsync()));

        Assert.Equal([("Untitled", Noon)], dialogs.Restores);
        Assert.True(session.IsDirty);
        Assert.StartsWith("*Untitled", main.Title);
        Assert.Null(session.CurrentFile);
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.CanUndo);
        AssertSameContent(crashed.Document, session.Document);
        Assert.Equal(session.Document.Layers[^1].Id, session.SelectedLayerId);
        Assert.Empty(dialogs.Alerts);
    });

    [Fact] // R1: a titled autosave points at the original when that file still exists; Ctrl+S then writes it, and the pair goes
    public void A_titled_restore_saves_over_the_original_and_the_pair_is_gone() => StaThread.Run(() =>
    {
        string original = Path.Combine(Storage.Root, "user", "shirt.r15proj");
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        File.WriteAllText(original, "the user's older save");
        var crashed = Crash("dead", Noon, original: original);
        var (session, main, dialogs, files) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;

        Assert.Equal(RecoveryOutcome.Restored, Await(main.Startup.RecoverAsync()));

        Assert.Equal(original, session.CurrentFile);
        Assert.True(session.IsDirty);
        Assert.StartsWith("*shirt.r15proj", main.Title);
        Assert.Equal("the user's older save", File.ReadAllText(original));   // restoring never touches the user's file

        Assert.True(main.Files.Save());                                       // Ctrl+S

        Assert.Empty(files.SaveProjectCalls);                                 // no Save As: it went to the original
        AssertSameContent(crashed.Document, ProjectIO.Read(File.ReadAllText(original)).Document);
        Assert.False(session.IsDirty);
        Assert.Empty(AutosaveFiles());
    });

    [Fact] // R1: when the original file is gone the restore stays untitled
    public void A_titled_restore_whose_file_is_gone_stays_untitled() => StaThread.Run(() =>
    {
        Crash("dead", Noon, original: Path.Combine(Storage.Root, "user", "gone.r15proj"));
        var (session, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;

        Assert.Equal(RecoveryOutcome.Restored, Await(main.Startup.RecoverAsync()));

        Assert.Equal([("gone.r15proj", Noon)], dialogs.Restores);             // the prompt still names what it was
        Assert.Null(session.CurrentFile);
        Assert.True(session.IsDirty);
        Assert.StartsWith("*Untitled", main.Title);
    });

    [Fact] // Item 2's "clean session deletes the pair" rule cannot delete the restored pair; the restored session autosaves under its own id
    public void A_restored_session_keeps_autosaving_under_its_own_session_id() => StaThread.Run(() =>
    {
        var crashed = Crash("dead", Noon, color: SKColors.Blue);
        var (session, main, dialogs, files) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;
        Await(main.Startup.RecoverAsync());

        // Right after the restore the pair sits under this window's id (the claim), and the dead owner's name is gone.
        Assert.Equal(Path.GetFileName(main.Autosave.ProjectPath), Path.GetFileName(Directory.GetFiles(Storage.AutosaveDirectory, "*.r15proj").Single()));
        Assert.False(File.Exists(crashed.Project));
        Assert.Equal(2, AutosaveFiles().Length);

        // Ticks with nothing edited: the session is dirty, so the clean-session rule leaves the pair alone, and the tick rewrites it as this owner's.
        Await(main.Autosave.Tick());
        Await(main.Autosave.Tick());
        Assert.True(session.IsDirty);
        Assert.Equal(2, AutosaveFiles().Length);
        var sidecar = AutosaveService.ReadSidecar(File.ReadAllText(main.Autosave.SidecarPath))!;
        Assert.Equal(Environment.ProcessId, sidecar.Pid);
        AssertSameContent(crashed.Document, ProjectIO.Read(File.ReadAllText(main.Autosave.ProjectPath)).Document);

        // An edit autosaves under the same id.
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Await(main.Autosave.Tick());
        Assert.True(ProjectIO.Read(File.ReadAllText(main.Autosave.ProjectPath)).Document.ShowGrid);
        Assert.Equal(2, AutosaveFiles().Length);

        // A real save finally clears it.
        files.SaveProjectResult = Path.Combine(Storage.Root, "user", "saved.r15proj");
        Directory.CreateDirectory(Path.GetDirectoryName(files.SaveProjectResult)!);
        Assert.True(main.Files.Save());
        Assert.Empty(AutosaveFiles());
    });

    [Fact] // A crash before the first save of a restore: the restored pair is offered again (the claimer's pid is dead)
    public void A_restored_session_that_crashes_before_a_save_is_offered_again() => StaThread.Run(() =>
    {
        var crashed = Crash("dead", Noon, color: SKColors.Blue);
        var (_, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;
        Await(main.Startup.RecoverAsync());

        // The restoring process dies here; the next launch's probe knows no live owner.
        var next = Assert.Single(main.Recovery.FindOrphans());

        AssertSameContent(crashed.Document, ProjectIO.Read(File.ReadAllText(next.ProjectPath)).Document);
    });

    [Fact] // Discard deletes the pair
    public void Discard_deletes_the_pair() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var (session, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Discard;

        Assert.Equal(RecoveryOutcome.Discarded, Await(main.Startup.RecoverAsync()));

        Assert.Empty(AutosaveFiles());
        Assert.False(session.IsDirty);
        Assert.Empty(session.Document.Layers);
    });

    [Fact] // Later (Esc or X) keeps the pair, and the next launch lists it again
    public void Later_keeps_the_pair_and_the_next_launch_lists_it_again() => StaThread.Run(() =>
    {
        var crashed = Crash("dead", Noon);
        string before = File.ReadAllText(crashed.Project);
        var (session, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Later;

        Assert.Equal(RecoveryOutcome.Later, Await(main.Startup.RecoverAsync()));

        Assert.False(session.IsDirty);
        Assert.Equal(new[] { "dead.json", "dead.r15proj" }, AutosaveFiles());
        Assert.Equal(before, File.ReadAllText(crashed.Project));
        Assert.Equal("dead", Assert.Single(main.Recovery.FindOrphans()).SessionId);
        // This window's own close must not delete what it did not take.
        main.Autosave.Close();
        Assert.Equal(new[] { "dead.json", "dead.r15proj" }, AutosaveFiles());

        var (_, next, nextDialogs, _) = Shell();                                // the next launch
        nextDialogs.RestoreChoiceAnswer = RestoreChoice.Later;
        Await(next.Startup.RecoverAsync());
        Assert.Single(nextDialogs.Restores);
    });

    [Fact] // A pair owned by a live process (same pid and start time) is never offered
    public void A_pair_owned_by_a_live_process_is_never_offered() => StaThread.Run(() =>
    {
        Crash("other-window", Noon, pid: 7777);
        Probe.Running.Add((7777, DeadStart));
        var (_, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Discard;

        Assert.Equal(RecoveryOutcome.None, Await(main.Startup.RecoverAsync()));

        Assert.Empty(dialogs.Restores);
        Assert.Equal(new[] { "other-window.json", "other-window.r15proj" }, AutosaveFiles());
    });

    [Fact] // A corrupt autosave: LoadFailed, nothing changes, the pair stays for the user
    public void A_corrupt_autosave_gives_LoadFailed_and_keeps_the_pair() => StaThread.Run(() =>
    {
        var crashed = Crash("dead", Noon);
        File.WriteAllText(crashed.Project, "{ \"layers\": [");
        var (session, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;

        Assert.Equal(RecoveryOutcome.Later, Await(main.Startup.RecoverAsync()));

        string alert = Assert.Single(dialogs.Alerts);
        Assert.StartsWith("Failed to load project. ", alert);
        Assert.False(session.IsDirty);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(new[] { "dead.json", "dead.r15proj" }, AutosaveFiles());
        Assert.Equal("{ \"layers\": [", File.ReadAllText(crashed.Project));
        main.Autosave.Close();                                                   // closing the window still leaves it
        Assert.Equal(2, AutosaveFiles().Length);
    });

    [Fact] // R2: one prompt per launch, newest first; the rest wait for later launches
    public void Only_the_newest_orphan_is_offered_per_launch() => StaThread.Run(() =>
    {
        Crash("older", Noon.AddHours(-2), pid: 1);
        Crash("newer", Noon, pid: 2);
        var (_, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Discard;

        Await(main.Startup.RecoverAsync());

        Assert.Equal([("Untitled", Noon)], dialogs.Restores);
        Assert.Equal(new[] { "older.json", "older.r15proj" }, AutosaveFiles());
    });

    [Fact] // Edits made after the window showed but before the restore are guarded like any replacement of dirty work
    public void A_cancelled_unsaved_changes_prompt_keeps_the_pair_and_the_edits() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var (session, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;
        dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        var edited = session.Document;

        Assert.Equal(RecoveryOutcome.Later, Await(main.Startup.RecoverAsync()));

        Assert.Equal(["Untitled"], dialogs.SaveChanges);
        Assert.Same(edited, session.Document);
        Assert.Equal(new[] { "dead.json", "dead.r15proj" }, AutosaveFiles());
    });

    [Fact] // E1/A1: nothing the flow does reaches %LOCALAPPDATA%\RBX15; the claim and the restore stay in the temp storage root
    public void The_flow_writes_only_under_its_storage_root() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var (_, main, dialogs, _) = Shell();
        dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;
        Await(main.Startup.RecoverAsync());

        Assert.All(Directory.GetFiles(Storage.Root, "*", SearchOption.AllDirectories),
            f => Assert.StartsWith(Storage.Root, f, StringComparison.OrdinalIgnoreCase));
    });
}

/// <summary>
/// A restored window closing (M5 item 3; ledger shell-lifecycle-quit, shell-no-unsaved-changes-guard): the restored work is dirty, so
/// the close asks; Don't save ends the process's claim on the pair and deletes it, Cancel keeps both the window and the pair.
/// </summary>
[Collection("MainWindow")]
public sealed class RestoredWindowCloseTests : RecoveryTestBase
{
    static readonly System.Windows.Rect Primary = new(0, 0, 1920, 1040);

    [Fact]
    public void A_restored_window_asks_on_close_and_Dont_save_deletes_the_pair() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var session = new EditorSession();
        var dialogs = new FakeDialogService { RestoreChoiceAnswer = RestoreChoice.Restore };
        var store = new FakeSettingsStore();
        var main = TestShell.Quiet(Composition.Build(session, new FakeFileDialogs(), dialogs, new FakeColorPicker(), store, Storage, new ManualTickSource(), Probe));
        var window = new MainWindow(main, store, [Primary]);
        var restore = main.Startup.RecoverAsync();
        while (!restore.IsCompleted)
        {
            StaThread.Pump();
            restore.Wait(TimeSpan.FromMilliseconds(5));
        }
        Assert.Equal(RecoveryOutcome.Restored, restore.GetAwaiter().GetResult());
        Assert.Equal(2, AutosaveFiles().Length);

        dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        window.Close();
        Assert.Equal(["Untitled"], dialogs.SaveChanges);
        Assert.Equal(2, AutosaveFiles().Length);                  // cancelled: the restored work is still unsaved

        dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        window.Close();
        Assert.Empty(AutosaveFiles());
    });
}

/// <summary>The session half of a restore (M5 item 3): dirty until saved, and the other things an Open does.</summary>
public sealed class EditorSessionRecoverTests : RecoveryTestBase
{
    static ProjectLoad LoadOf(Document document) => new(document, 5, System.Collections.Immutable.ImmutableList<string>.Empty);

    static Document OneLayer(SKColor color, int id = 3) =>
        new Document() with { Layers = new Document().Layers.Add(new Layer { Id = id, Image = Img2(color), Name = "L" + id }) };

    static SKImage Img2(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    [Fact] // Dirty until the next MarkSaved, whatever the document holds
    public void Recover_reads_dirty_until_the_next_save()
    {
        var session = new EditorSession();
        var changes = new List<SessionChange>();
        session.Changed += (_, e) => changes.Add(e.What);

        session.Recover(LoadOf(new Document()), null);   // content equal to a new document: still dirty

        Assert.True(session.IsDirty);
        Assert.Equal(SessionChange.Document | SessionChange.Selection | SessionChange.History | SessionChange.File | SessionChange.Replaced, Assert.Single(changes));

        var doc = session.Document;
        session.MarkSaved(Path.Combine(Storage.Root, "x.r15proj"), doc);
        Assert.False(session.IsDirty);
    }

    [Fact] // Open after a recover reads clean again
    public void An_Open_after_a_recover_reads_clean()
    {
        var session = new EditorSession();
        session.Recover(LoadOf(OneLayer(SKColors.Red)), null);

        session.Open(LoadOf(OneLayer(SKColors.Blue)), Path.Combine(Storage.Root, "o.r15proj"));

        Assert.False(session.IsDirty);
    }

    [Fact] // Like Open: Changing first, history cleared, the top layer selected, ids carried, outgoing images handed back
    public void Recover_does_what_Open_does_for_history_selection_and_images()
    {
        var session = new EditorSession();
        var changing = 0;
        session.Changing += (_, _) => changing++;
        Assert.True(session.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = session.TakeId(), Image = Img2(SKColors.Red), Name = "Old" }) }));
        var oldImage = session.Document.Layers[0].Image;
        int before = changing;

        var outgoing = session.Recover(LoadOf(OneLayer(SKColors.Lime, id: 9)), null);

        Assert.Equal(before + 1, changing);
        Assert.Equal(0, session.UndoCount);
        Assert.Equal(0, session.RedoCount);
        Assert.Equal(9, session.SelectedLayerId);
        Assert.True(session.NextId > 9);
        Assert.Contains(oldImage, outgoing);
        Assert.DoesNotContain(session.Document.Layers[0].Image, outgoing);
    }

    [Fact] // A gesture open at the swap is abandoned, as for Open
    public void Recover_abandons_an_open_gesture()
    {
        var session = new EditorSession();
        var gesture = session.BeginGesture();

        session.Recover(LoadOf(OneLayer(SKColors.Red)), null);
        gesture.Dispose();

        Assert.False(session.InGesture);
    }

    [Fact] // CurrentFile is the original only when it exists
    public void Recover_sets_the_current_file_only_when_the_original_exists()
    {
        string there = Path.Combine(Storage.Root, "there.r15proj");
        Directory.CreateDirectory(Storage.Root);
        File.WriteAllText(there, "x");
        var session = new EditorSession();

        session.Recover(LoadOf(new Document()), there);
        Assert.Equal(there, session.CurrentFile);

        session.Recover(LoadOf(new Document()), Path.Combine(Storage.Root, "gone.r15proj"));
        Assert.Null(session.CurrentFile);

        session.Recover(LoadOf(new Document()), null);
        Assert.Null(session.CurrentFile);
    }
}

/// <summary>The restore prompt's text and shape (M5 decision R1; ledger dialogs-inventory: every new modal string joins the inventory).</summary>
public sealed class RestorePromptTests
{
    static T Find<T>(System.Windows.Window w, string name) where T : class => Assert.IsType<T>(w.FindName(name), exactMatch: false);

    [Fact]
    public void The_prompt_text_names_the_document_and_the_time()
    {
        Assert.Equal("RBX15 didn't close properly. Restore your unsaved work on shirt.r15proj, autosaved today at 3:41 PM?",
            DialogText.RestorePrompt("shirt.r15proj", "today at 3:41 PM"));
    }

    [Fact] // Same local day: "today at"; an older one: the date
    public void The_time_reads_today_or_the_date()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var now = new DateTimeOffset(2026, 10, 25, 20, 0, 0, TimeSpan.Zero).ToLocalTime();
        var sameDay = new DateTimeOffset(now.Year, now.Month, now.Day, 3, 41, 0, now.Offset);
        var earlier = sameDay.AddDays(-2);

        Assert.Equal("today at 3:41 AM", DialogText.AutosavedWhen(sameDay, now, culture));
        Assert.Equal($"{earlier.ToString("d", culture)} at 3:41 AM", DialogText.AutosavedWhen(earlier, now, culture));
    }

    [Fact] // The fake records the call and answers Later unless told otherwise
    public void The_fake_answers_later_by_default()
    {
        var dialogs = new FakeDialogService();

        Assert.Equal(RestoreChoice.Later, dialogs.AskRestore("Untitled", DateTimeOffset.UnixEpoch));
        Assert.Equal([("Untitled", DateTimeOffset.UnixEpoch)], dialogs.Restores);
    }

    [Fact] // Restore (Enter) and Discard; no Cancel button; Esc and the caption's close button leave it unanswered (Cancel)
    public void The_dialog_has_restore_and_discard_and_closes_unanswered() => StaThread.Run(() =>
    {
        var d = new RBX15.App.Views.ThemedDialog("RBX15 didn't close properly.", RBX15.App.Views.ThemedDialogKind.Restore);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", d.Title);
        var restore = Find<System.Windows.Controls.Button>(d, "OkButton");
        var discard = Find<System.Windows.Controls.Button>(d, "DontSaveButton");
        var cancel = Find<System.Windows.Controls.Button>(d, "CancelButton");
        Assert.Equal(("Restore", true, false), (restore.Content, restore.IsDefault, restore.IsCancel));
        Assert.Equal(("Discard", false, false, System.Windows.Visibility.Visible), (discard.Content, discard.IsDefault, discard.IsCancel, discard.Visibility));
        Assert.Equal((System.Windows.Visibility.Collapsed, false), (cancel.Visibility, cancel.IsCancel));
        Assert.Equal(SaveChoice.Cancel, d.Choice);
    });

    [Theory] // Each button's answer, set before DialogResult closes the dialog (the dialog is never shown, as in ThemedDialogTests)
    [InlineData("OkButton", SaveChoice.Save)]
    [InlineData("DontSaveButton", SaveChoice.DontSave)]
    public void Each_button_reports_its_choice(string button, SaveChoice expected) => StaThread.Run(() =>
    {
        var d = new RBX15.App.Views.ThemedDialog("x", RBX15.App.Views.ThemedDialogKind.Restore);
        var b = Find<System.Windows.Controls.Button>(d, button);
        Assert.Throws<InvalidOperationException>(() => b.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, b)));
        Assert.Equal(expected, d.Choice);
    });
}

/// <summary>The app's process probe, against this test process only (M5 item 3).</summary>
public sealed class ProcessProbeTests
{
    [Fact]
    public void The_probe_knows_this_process_by_pid_and_start_time()
    {
        var self = System.Diagnostics.Process.GetCurrentProcess();
        var probe = new ProcessProbe();

        Assert.True(probe.IsRunning(self.Id, self.StartTime.ToUniversalTime()));
        Assert.False(probe.IsRunning(self.Id, self.StartTime.ToUniversalTime().AddHours(-3)));   // a reused pid
        Assert.False(probe.IsRunning(int.MaxValue, DateTime.UtcNow));                           // no such process
    }
}
