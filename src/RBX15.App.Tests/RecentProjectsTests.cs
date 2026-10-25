using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The recent projects list (M5 item 5; decision H1; ledger upgrade-recent-projects): the last 10, newest first, one entry per file
/// whatever its case, missing files pruned, kept in settings.json beside the window and folder keys. Fakes only: no test reads the
/// user's settings.json.
/// </summary>
public sealed class RecentProjectsTests
{
    static string P(int n) => $@"C:\Projects\shirts\shirt-{n:00}.r15proj";

    static RecentProjects Make(FakeSettingsStore store, Func<string, bool>? exists = null) => new(store, exists ?? (_ => true));

    [Fact] // Add 12 distinct paths gives the newest 10, in order
    public void Twelve_adds_keep_the_newest_ten_newest_first()
    {
        var store = new FakeSettingsStore();
        var recents = Make(store);
        for (int i = 1; i <= 12; i++) recents.Add(P(i));

        Assert.Equal(Enumerable.Range(3, 10).Reverse().Select(P), recents.Items);
        Assert.Equal(recents.Items, store.Recents);
        Assert.Equal(RecentProjects.Max, recents.Items.Count);
    }

    [Fact] // re-adding the 3rd moves it to the top with no duplicate
    public void Re_adding_the_third_moves_it_to_the_top_with_no_duplicate()
    {
        var store = new FakeSettingsStore();
        var recents = Make(store);
        for (int i = 1; i <= 5; i++) recents.Add(P(i));      // 5 4 3 2 1
        recents.Add(P(3));

        Assert.Equal([P(3), P(5), P(4), P(2), P(1)], recents.Items);
        Assert.Equal(recents.Items, store.Recents);
    }

    [Fact] // a path differing only in case is the same file
    public void A_path_that_differs_only_in_case_is_one_entry()
    {
        var store = new FakeSettingsStore();
        var recents = Make(store);
        recents.Add(@"C:\Projects\Shirt.r15proj");
        recents.Add(@"c:\projects\SHIRT.R15PROJ");

        Assert.Equal([@"c:\projects\SHIRT.R15PROJ"], recents.Items);   // the newest spelling wins
    }

    [Theory] // only full paths are kept: a relative path or nothing is ignored
    [InlineData("")]
    [InlineData("shirt.r15proj")]
    [InlineData(@"..\shirt.r15proj")]
    [InlineData("C:shirt.r15proj")]
    public void A_path_that_is_not_fully_qualified_is_ignored(string path)
    {
        var store = new FakeSettingsStore();
        var recents = Make(store);
        recents.Add(path);

        Assert.Empty(recents.Items);
        Assert.Empty(store.RecentSaves);
    }

    [Fact] // each add re-reads the store, so a second window's additions survive
    public void An_add_keeps_what_another_window_stored()
    {
        var store = new FakeSettingsStore { Recents = [P(7), P(6)] };
        var recents = Make(store);
        recents.Add(P(8));

        Assert.Equal([P(8), P(7), P(6)], store.Recents);
    }

    [Fact] // a deleted file is pruned from Items and from the store
    public async Task A_deleted_file_is_pruned_from_the_items_and_the_next_save()
    {
        var store = new FakeSettingsStore { Recents = [P(3), P(2), P(1)] };
        var gone = new HashSet<string> { P(2) };
        var recents = Make(store, p => !gone.Contains(p));

        var items = await recents.RefreshAsync();

        Assert.Equal([P(3), P(1)], items);
        Assert.Equal([P(3), P(1)], recents.Items);
        Assert.Equal([P(3), P(1)], store.Recents);
        recents.Add(P(4));
        Assert.Equal([P(4), P(3), P(1)], store.Recents);
    }

    [Fact] // a refresh that finds nothing missing writes nothing
    public async Task A_refresh_with_every_file_present_does_not_write()
    {
        var store = new FakeSettingsStore { Recents = [P(2), P(1)] };
        var recents = Make(store);

        await recents.RefreshAsync();

        Assert.Equal([P(2), P(1)], recents.Items);
        Assert.Empty(store.RecentSaves);
    }

    [Fact] // the existence checks run off the calling thread, so a dead network share cannot block the UI
    public async Task The_existence_checks_run_on_a_pool_thread()
    {
        var store = new FakeSettingsStore { Recents = [P(1)] };
        int caller = Environment.CurrentManagedThreadId;
        int? checkedOn = null;
        var recents = Make(store, _ =>
        {
            checkedOn = Environment.CurrentManagedThreadId;
            return true;
        });

        await recents.RefreshAsync();

        Assert.NotNull(checkedOn);
        Assert.NotEqual(caller, checkedOn);
    }

    [Fact] // a check that throws (a bad share) counts as missing instead of failing the refresh
    public async Task A_check_that_throws_counts_as_missing()
    {
        var store = new FakeSettingsStore { Recents = [P(2), P(1)] };
        var recents = Make(store, p => p == P(1) ? throw new IOException("share is down") : true);

        Assert.Equal([P(2)], await recents.RefreshAsync());
    }

    [Fact] // what another add stores while the checks run is kept; only the paths found missing leave
    public async Task An_add_that_lands_during_a_refresh_is_kept()
    {
        var store = new FakeSettingsStore { Recents = [P(2), P(1)] };
        var recents = Make(store, p =>
        {
            if (p == P(1)) store.Recents = [P(9), .. store.Recents];   // another window saved while this one checked
            return p != P(1);
        });

        var items = await recents.RefreshAsync();

        Assert.Equal([P(9), P(2)], items);
        Assert.Equal([P(9), P(2)], store.Recents);
    }

    [Fact] // Remove drops one entry; Clear drops them all
    public void Remove_and_Clear_empty_the_list()
    {
        var store = new FakeSettingsStore();
        var recents = Make(store);
        for (int i = 1; i <= 3; i++) recents.Add(P(i));

        recents.Remove(P(2).ToUpperInvariant());
        Assert.Equal([P(3), P(1)], recents.Items);

        recents.Clear();
        Assert.Empty(recents.Items);
        Assert.Empty(store.Recents);
    }

    [Fact] // Changed follows every change, and the first refresh, so the jump list is published once at launch
    public async Task Changed_follows_each_change_and_the_first_refresh()
    {
        var store = new FakeSettingsStore();
        var recents = Make(store);
        int raised = 0;
        recents.Changed += (_, _) => raised++;

        await recents.RefreshAsync();
        Assert.Equal(1, raised);                  // the first refresh, with nothing stored
        await recents.RefreshAsync();
        Assert.Equal(1, raised);                  // nothing changed
        recents.Add(P(1));
        recents.Add(P(2));
        recents.Remove(P(1));
        recents.Clear();
        Assert.Equal(5, raised);
        recents.Clear();                          // already empty
        Assert.Equal(5, raised);
    }
}

/// <summary>Which saves, opens and restores reach the recents (M5 item 5; decision H1): a success adds, a cancel or failure does not, a restore never.</summary>
public sealed class RecentProjectsFeedTests : IDisposable
{
    readonly TestFiles _files = new();

    sealed class Rig
    {
        public Rig(EditorSession? session = null)
        {
            Session = session ?? new EditorSession();
            Service = new ProjectFileService(Session, Files, Dialogs, Status, Settings, release: _ => { });
        }

        public EditorSession Session { get; }
        public FakeFileDialogs Files { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public FakeStatus Status { get; } = new();
        public FakeSettingsStore Settings { get; } = new();
        public ProjectFileService Service { get; }
    }

    static EditorSession Dirty()
    {
        var s = new EditorSession();
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, HomeTestKit.Solid(4, 4, SKColors.Red), s.TakeId(), "art")));
        return s;
    }

    string ProjectFile(string name)
    {
        string path = Path.Combine(_files.Dir, name);
        File.WriteAllText(path, ProjectIO.Write(new Document(), DateTimeOffset.UnixEpoch, title: null));
        return path;
    }

    [Fact] // Save (no file yet, so Save As) adds the file
    public void Save_adds_the_saved_file()
    {
        var rig = new Rig(Dirty());
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, "a.r15proj");

        Assert.True(rig.Service.Save());

        Assert.Equal([rig.Files.SaveProjectResult], rig.Settings.Recents);
        Assert.Equal([rig.Files.SaveProjectResult], rig.Service.Recents.Items);
    }

    [Fact] // Save As adds the new file, on top of the earlier one
    public void Save_As_adds_the_new_file_on_top()
    {
        var rig = new Rig(Dirty());
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, "a.r15proj");
        Assert.True(rig.Service.Save());
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, "b.r15proj");

        Assert.True(rig.Service.SaveAs());

        Assert.Equal([Path.Combine(_files.Dir, "b.r15proj"), Path.Combine(_files.Dir, "a.r15proj")], rig.Settings.Recents);
    }

    [Fact] // Open adds the opened file
    public async Task Open_adds_the_opened_file()
    {
        var rig = new Rig();
        string path = ProjectFile("o.r15proj");

        Assert.True(await rig.Service.OpenAsync(path));

        Assert.Equal([path], rig.Settings.Recents);
    }

    [Fact] // a cancelled Save As, a failed write, a failed Open and a declined guard add nothing
    public async Task A_cancelled_or_failed_save_or_open_adds_nothing()
    {
        var rig = new Rig(Dirty());
        rig.Files.SaveProjectResult = null;                                              // Save As cancelled
        Assert.False(rig.Service.Save());

        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, "no-such-folder", "x.r15proj");   // the write fails
        Assert.False(rig.Service.Save());

        string broken = Path.Combine(_files.Dir, "broken.r15proj");
        File.WriteAllText(broken, "{ not a project");
        Assert.False(await rig.Service.OpenAsync(broken));                              // the read fails

        string good = ProjectFile("good.r15proj");
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        Assert.False(await rig.Service.OpenAsync(good));                                // the guard is cancelled (the session is dirty)

        Assert.Empty(rig.Settings.Recents);
        Assert.Empty(rig.Settings.RecentSaves);
    }

    public void Dispose() => _files.Dispose();
}

/// <summary>A restore opens work the user has not saved; it is not a recent project (M5 item 5; decision H1).</summary>
public sealed class RecentProjectsRestoreTests : RecoveryTestBase
{
    [Fact] // a restore opens work the user has not saved: it is not a recent project
    public void A_restore_adds_nothing() => StaThread.Run(() =>
    {
        string original = Path.Combine(Storage.Root, "user", "shirt.r15proj");
        Crash("dead", Noon, original: original);
        var store = new FakeSettingsStore();
        var dialogs = new FakeDialogService { RestoreChoiceAnswer = RestoreChoice.Restore };
        var main = TestShell.Quiet(Composition.Build(new EditorSession(), new FakeFileDialogs(), dialogs, new FakeColorPicker(), store, Storage,
            new ManualTickSource(), Probe, jumpList: new FakeJumpListSink()));

        var restore = main.Startup.RecoverAsync();
        while (!restore.IsCompleted)
        {
            StaThread.Pump();
            restore.Wait(TimeSpan.FromMilliseconds(5));
        }

        Assert.Equal(RecoveryOutcome.Restored, restore.GetAwaiter().GetResult());
        Assert.Empty(store.Recents);
        Assert.Empty(store.RecentSaves);
    });
}

/// <summary>The jump list follows the recents (M5 item 5; decision J1; ledger upgrade-jump-list): the exact tasks, the category, the clear, the user's removals. A fake sink: no taskbar.</summary>
public sealed class JumpListPublisherTests
{
    const string Exe = @"C:\Program Files\RBX15\RBX15.App.exe";

    static string P(int n) => $@"C:\My Projects\Shirt {n:00}.r15proj";

    static (RecentProjects Recents, FakeJumpListSink Sink, FakeSettingsStore Store) Make()
    {
        var store = new FakeSettingsStore();
        var recents = new RecentProjects(store, _ => true);
        var sink = new FakeJumpListSink();
        _ = new JumpListPublisher(recents, sink, Exe);
        return (recents, sink, store);
    }

    [Fact] // ten recents give ten tasks with the exact arguments, under the Recent category, and no tasks beyond them
    public void Ten_recents_publish_ten_tasks_with_the_exact_arguments()
    {
        var (recents, sink, _) = Make();
        for (int i = 1; i <= 10; i++) recents.Add(P(i));

        var last = sink.Last!;

        Assert.Equal(10, last.Count);
        Assert.Equal("Recent", IJumpListSink.Category);
        for (int i = 0; i < 10; i++)
        {
            var entry = last[i];
            string path = P(10 - i);
            Assert.Equal(new JumpEntry(Path.GetFileName(path), path, "\"" + path + "\"", Exe, Exe), entry);
        }
    }

    [Fact] // Clear hands the sink an empty list
    public void Clear_publishes_an_empty_list()
    {
        var (recents, sink, _) = Make();
        recents.Add(P(1));
        recents.Clear();

        Assert.Empty(sink.Last!);
    }

    [Fact] // an item the user removes from the jump list leaves the recents too, and the list is republished without it
    public void An_item_the_user_removes_leaves_the_recents()
    {
        var (recents, sink, store) = Make();
        for (int i = 1; i <= 3; i++) recents.Add(P(i));

        sink.RemoveByUser(P(2));

        Assert.Equal([P(3), P(1)], recents.Items);
        Assert.Equal([P(3), P(1)], store.Recents);
        Assert.Equal([P(3), P(1)], sink.Last!.Select(e => e.Description));
    }

    [Fact] // the first refresh at launch publishes once, even with nothing recent (a stale list is replaced)
    public async Task The_first_refresh_publishes_once()
    {
        var store = new FakeSettingsStore { Recents = [P(1)] };
        var recents = new RecentProjects(store, _ => true);
        var sink = new FakeJumpListSink();
        _ = new JumpListPublisher(recents, sink, Exe);

        await recents.RefreshAsync();
        await recents.RefreshAsync();

        Assert.Single(sink.Published);
        Assert.Equal([P(1)], sink.Last!.Select(e => e.Description));
    }
}

/// <summary>The File group's Recent drop-down (M5 item 5; decision H1; ledger upgrade-recent-projects): entries, opening through the save prompt, a missing file, Clear.</summary>
public sealed class RecentMenuTests : IDisposable
{
    readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    sealed record Rig(EditorSession Session, FakeDialogService Dialogs, FakeStatus Status, FakeSettingsStore Settings, FileGroupViewModel Group, ProjectFileService Files);

    Rig Make(EditorSession? session = null)
    {
        session ??= new EditorSession();
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var status = new FakeStatus();
        var settings = new FakeSettingsStore();
        var service = new ProjectFileService(session, files, dialogs, status, settings, release: _ => { });
        var group = new FileGroupViewModel(session, files, dialogs, status, new ImportService(session, status), service);
        return new Rig(session, dialogs, status, settings, group, service);
    }

    string ProjectFile(string name)
    {
        string path = Path.Combine(_files.Dir, name);
        File.WriteAllText(path, ProjectIO.Write(new Document(), DateTimeOffset.UnixEpoch, title: null));
        return path;
    }

    [Fact] // the entries are the recents, newest first, each the file name with the full path to show as its tooltip
    public void The_entries_follow_the_recents_newest_first()
    {
        var r = Make();
        Assert.False(r.Group.Recent.HasItems);
        string a = ProjectFile("a.r15proj"), b = ProjectFile("b.r15proj");
        r.Files.Recents.Add(a);
        r.Files.Recents.Add(b);

        Assert.True(r.Group.Recent.HasItems);
        Assert.Equal([("b.r15proj", b), ("a.r15proj", a)], r.Group.Recent.Items.Select(i => (i.Name, i.Path)));
    }

    [Fact] // opening a recent goes through the save prompt: a cancelled prompt keeps the document and opens nothing
    public async Task Opening_a_recent_asks_the_save_question_first()
    {
        var session = new EditorSession();
        Assert.True(session.ApplyWithSelection(x => LayerOps.AddImage(x, HomeTestKit.Solid(4, 4, SKColors.Red), session.TakeId(), "art")));
        var r = Make(session);
        string path = ProjectFile("a.r15proj");
        r.Files.Recents.Add(path);

        r.Dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        await r.Group.Recent.OpenRecentCommand.ExecuteAsync(path);
        Assert.Equal(["Untitled"], r.Dialogs.SaveChanges);
        Assert.Null(session.CurrentFile);
        Assert.True(session.IsDirty);

        r.Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        await r.Group.Recent.OpenRecentCommand.ExecuteAsync(path);
        Assert.Equal(path, session.CurrentFile);
        Assert.False(session.IsDirty);
        Assert.Equal([path], r.Settings.Recents);
    }

    [Fact] // a recent whose file is gone says so on the status strip and leaves the list
    public async Task A_missing_file_gives_the_status_line_and_leaves_the_list()
    {
        var r = Make();
        string path = ProjectFile("gone.r15proj");
        string keep = ProjectFile("keep.r15proj");
        r.Files.Recents.Add(keep);
        r.Files.Recents.Add(path);
        File.Delete(path);

        await r.Group.Recent.OpenRecentCommand.ExecuteAsync(path);

        Assert.Equal("gone.r15proj was moved or deleted.", r.Status.Current);
        Assert.Equal([keep], r.Group.Recent.Items.Select(i => i.Path));
        Assert.Equal([keep], r.Settings.Recents);
        Assert.Null(r.Session.CurrentFile);
    }

    [Fact] // the last entry clears the list
    public void Clear_recent_list_empties_the_menu_the_store_and_the_command()
    {
        var r = Make();
        r.Files.Recents.Add(ProjectFile("a.r15proj"));
        Assert.True(r.Group.Recent.ClearCommand.CanExecute(null));

        r.Group.Recent.ClearCommand.Execute(null);

        Assert.False(r.Group.Recent.HasItems);
        Assert.Empty(r.Group.Recent.Items);
        Assert.Empty(r.Settings.Recents);
        Assert.False(r.Group.Recent.ClearCommand.CanExecute(null));
    }

    [Fact] // opening the drop-down re-checks the files: a deleted one is not offered
    public async Task Refreshing_drops_a_file_deleted_since()
    {
        var r = Make();
        string a = ProjectFile("a.r15proj"), b = ProjectFile("b.r15proj");
        r.Files.Recents.Add(a);
        r.Files.Recents.Add(b);
        File.Delete(a);

        await r.Group.Recent.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([b], r.Group.Recent.Items.Select(i => i.Path));
    }
}
