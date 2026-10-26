using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The file a launch names (M5 item 4; decisions S1, R3; ledger upgrade-file-association-launch): the argument parser, and where
/// the file opens once the recovery step has had its say.
/// </summary>
public sealed class LaunchArgumentsTests : IDisposable
{
    readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    [Fact] // no arguments, and a flag only
    public void Nothing_to_open_gives_null()
    {
        Assert.Null(LaunchArguments.Parse([]));
        Assert.Null(LaunchArguments.Parse(["--test-input-bridge"]));
    }

    [Fact] // a path that does not exist, and a relative path to a file that does
    public void A_missing_file_and_a_relative_path_are_not_launch_files()
    {
        string real = _files.Write("shirt.r15proj", [1]);
        Assert.Null(LaunchArguments.Parse([Path.Combine(_files.Dir, "gone.r15proj")]));
        Assert.Null(LaunchArguments.Parse(["shirt.r15proj"]));
        Assert.Equal(real, LaunchArguments.Parse([real]));
    }

    [Fact] // a path with spaces, as the command line parser hands it over (and with the quotes still on, trimmed)
    public void A_path_with_spaces_survives()
    {
        string path = _files.Write("my red shirt.r15proj", [1]);
        Assert.Equal(path, LaunchArguments.Parse([path]));
        Assert.Equal(path, LaunchArguments.Parse(["\"" + path + "\""]));
    }

    [Fact] // a Unicode path
    public void A_unicode_path_survives()
    {
        string path = _files.Write("camisa roja ñá ✨ シャツ.r15proj", [1]);
        Assert.Equal(path, LaunchArguments.Parse([path]));
    }

    [Fact] // the bridge flag is ignored wherever it sits; any extension qualifies (ProjectIO refuses what is not a project)
    public void A_path_beside_the_bridge_flag_is_found_and_any_extension_qualifies()
    {
        string path = _files.Write("notes.txt", [1]);
        Assert.Equal(path, LaunchArguments.Parse(["--test-input-bridge", path]));
        Assert.Equal(path, LaunchArguments.Parse([path, "--test-input-bridge"]));
        Assert.Equal(path, LaunchArguments.Parse(["--other-flag", Path.Combine(_files.Dir, "gone.r15proj"), path]));
    }
}

public sealed class StartupLaunchFileTests : RecoveryTestBase
{
    // Under the storage root, which the base class deletes.
    readonly string _dir;

    public StartupLaunchFileTests() => Directory.CreateDirectory(_dir = Path.Combine(Storage.Root, "launch"));

    sealed record Rig(EditorSession Session, MainViewModel Main, FakeDialogService Dialogs, FakeSettingsStore Settings, FakeAppLauncher Launcher);

    Rig Shell()
    {
        var session = new EditorSession();
        var dialogs = new FakeDialogService();
        var settings = new FakeSettingsStore();
        var launcher = new FakeAppLauncher();
        var main = TestShell.Quiet(Composition.Build(session, new FakeFileDialogs(), dialogs, new FakeColorPicker(), settings, Storage, new ManualTickSource(), Probe, launcher: launcher));
        return new Rig(session, main, dialogs, settings, launcher);
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

    /// <summary>A saved project in the temp folder: one blue layer.</summary>
    (string Path, Document Document) ProjectFile(string name = "launched.r15proj")
    {
        var s = new EditorSession();
        s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = Img(SKColors.Blue), Name = "Launched" }) });
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, ProjectIO.Write(s.Document, Noon));
        return (path, s.Document);
    }

    [Fact] // a startup with a valid project path ends with that document open, clean, CurrentFile set, the folder remembered
    public void A_valid_launch_path_opens_clean_with_the_folder_remembered() => StaThread.Run(() =>
    {
        var (path, document) = ProjectFile();
        var rig = Shell();

        Assert.Equal(RecoveryOutcome.None, Await(rig.Main.Startup.RunAsync(path)));

        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        AssertSameContent(document, rig.Session.Document);
        Assert.Equal(_dir, rig.Settings.Folders[SettingsFolder.Project]);
        Assert.Empty(rig.Launcher.Opened);
        Assert.Empty(rig.Dialogs.Alerts);
    });

    [Fact] // a launch with no file leaves the new document alone
    public void No_launch_path_opens_nothing() => StaThread.Run(() =>
    {
        var rig = Shell();

        Assert.Equal(RecoveryOutcome.None, Await(rig.Main.Startup.RunAsync(null)));

        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Launcher.Opened);
    });

    [Fact] // a corrupt file shows LoadFailed and leaves a new document
    public void A_corrupt_launch_file_shows_load_failed_over_a_new_document() => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, "broken.r15proj");
        File.WriteAllText(path, "{ \"layers\": [");
        var rig = Shell();

        Await(rig.Main.Startup.RunAsync(path));

        var alert = Assert.Single(rig.Dialogs.Alerts);
        Assert.StartsWith("Failed to load project.", alert);
        Assert.Null(rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        Assert.Empty(rig.Session.Document.Layers);
    });

    [Fact] // R3: Restore keeps the recovered work here and hands the launched path to the launcher; nothing opens here
    public void Restore_keeps_the_recovered_work_and_launches_the_path_in_a_new_window() => StaThread.Run(() =>
    {
        var crashed = Crash("dead", Noon, color: SKColors.Lime);
        var (path, _) = ProjectFile();
        var rig = Shell();
        rig.Dialogs.RestoreChoiceAnswer = RestoreChoice.Restore;

        Assert.Equal(RecoveryOutcome.Restored, Await(rig.Main.Startup.RunAsync(path)));

        Assert.Equal([path], rig.Launcher.Opened);
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        AssertSameContent(crashed.Document, rig.Session.Document);
        Assert.Empty(rig.Dialogs.SaveChanges);   // no unsaved-changes prompt: the file never opens over the restored work
    });

    [Fact] // R3: Discard opens the launched path here, and the autosave is gone
    public void Discard_opens_the_launch_path_here() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var (path, document) = ProjectFile();
        var rig = Shell();
        rig.Dialogs.RestoreChoiceAnswer = RestoreChoice.Discard;

        Assert.Equal(RecoveryOutcome.Discarded, Await(rig.Main.Startup.RunAsync(path)));

        Assert.Equal(path, rig.Session.CurrentFile);
        AssertSameContent(document, rig.Session.Document);
        Assert.Empty(rig.Launcher.Opened);
        Assert.Empty(AutosaveFiles());
    });

    [Fact] // R3: Esc / Later opens the launched path here too, and the autosave waits for the next launch
    public void Later_opens_the_launch_path_here_and_keeps_the_autosave() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var (path, document) = ProjectFile();
        var rig = Shell();
        rig.Dialogs.RestoreChoiceAnswer = RestoreChoice.Later;

        Assert.Equal(RecoveryOutcome.Later, Await(rig.Main.Startup.RunAsync(path)));

        Assert.Equal(path, rig.Session.CurrentFile);
        AssertSameContent(document, rig.Session.Document);
        Assert.Empty(rig.Launcher.Opened);
        Assert.NotEmpty(AutosaveFiles());
    });

    [Fact] // a launcher that cannot start the app is reported over the restored work, never thrown
    public void A_launcher_failure_is_an_alert() => StaThread.Run(() =>
    {
        Crash("dead", Noon);
        var (path, _) = ProjectFile();
        var session = new EditorSession();
        var dialogs = new FakeDialogService { RestoreChoiceAnswer = RestoreChoice.Restore };
        var main = TestShell.Quiet(Composition.Build(session, new FakeFileDialogs(), dialogs, new FakeColorPicker(), new FakeSettingsStore(), Storage, new ManualTickSource(), Probe, launcher: new ThrowingLauncher()));

        Await(main.Startup.RunAsync(path));

        Assert.StartsWith("Failed to load project.", Assert.Single(dialogs.Alerts));
        Assert.True(session.IsDirty);
    });

    sealed class ThrowingLauncher : IAppLauncher
    {
        public void Open(string path) => throw new InvalidOperationException("no such exe");

        public void OpenNew() => throw new InvalidOperationException("no such exe");
    }

    [Fact] // the bridged launch runs no startup flow and reads no arguments but its own flag
    public void The_bridged_launch_does_not_run_the_startup_flow()
    {
        string app = File.ReadAllText(FindSource("RBX15.App", "App.xaml.cs"));
        int bridge = app.IndexOf("void StartWithBridge", StringComparison.Ordinal);
        Assert.True(bridge > 0);
        string body = app[bridge..];
        Assert.DoesNotContain("Startup", body);
        Assert.DoesNotContain("LaunchArguments", body);
    }

    static string FindSource(string project, string file)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "src", project, file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(file);
    }
}
