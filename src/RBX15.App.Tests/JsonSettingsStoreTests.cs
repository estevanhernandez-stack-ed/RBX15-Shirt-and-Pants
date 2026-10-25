using System.Text.Json.Nodes;
using RBX15.App.Services;

namespace RBX15.App.Tests;

/// <summary>
/// JsonSettingsStore (spec §3 "Upgrades": the saved window position; ledger shell-no-persistence, fix):
/// atomic writes, and reads that never throw.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "rbx15-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    string FilePath => Path.Combine(_dir, "settings.json");

    [Fact] // the directory is created on first save; the file round-trips
    public void A_saved_placement_loads_back()
    {
        var store = new JsonSettingsStore(_dir);
        Assert.Null(store.LoadWindowPlacement());

        var placement = new WindowPlacement(-1700.5, 50, 1000, 700.25, true);
        store.SaveWindowPlacement(placement);

        Assert.Equal(FilePath, store.FilePath);
        Assert.Equal(placement, new JsonSettingsStore(_dir).LoadWindowPlacement());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact] // the file's shape is fixed: { "window": { left, top, width, height, maximized } }
    public void The_file_holds_a_window_object()
    {
        new JsonSettingsStore(_dir).SaveWindowPlacement(new WindowPlacement(10, 20, 1200, 800, false));
        var w = JsonNode.Parse(File.ReadAllText(FilePath))!["window"]!;
        Assert.Equal((10.0, 20.0, 1200.0, 800.0, false),
                     (w["left"]!.GetValue<double>(), w["top"]!.GetValue<double>(), w["width"]!.GetValue<double>(),
                      w["height"]!.GetValue<double>(), w["maximized"]!.GetValue<bool>()));
    }

    [Fact] // a save rewrites only "window" and keeps keys later milestones add
    public void A_save_keeps_other_keys()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "recent": ["a.r15proj"], "window": { "left": 1 } }""");
        new JsonSettingsStore(_dir).SaveWindowPlacement(new WindowPlacement(5, 6, 700, 500, false));
        var root = JsonNode.Parse(File.ReadAllText(FilePath))!;
        Assert.Equal("a.r15proj", root["recent"]![0]!.GetValue<string>());
        Assert.Equal(700, root["window"]!["width"]!.GetValue<double>());
    }

    public static TheoryData<string> CorruptFiles => new()
    {
        "",
        "{",
        "null",
        "[1, 2]",
        """{ "window": null }""",
        """{ "window": { "left": 1, "top": 2, "width": 3 } }""",
        """{ "window": { "left": "1", "top": 2, "width": 3, "height": 4, "maximized": false } }""",
        """{ "window": { "left": 1, "top": 2, "width": 3, "height": 4, "maximized": "yes" } }""",
        """{ "window": { "left": 1, "top": 2, "width": -3, "height": 4, "maximized": false } }""",
        """{ "window": { "left": 1, "top": 2, "width": 0, "height": 4, "maximized": false } }""",
        """{ "window": { "left": 1e400, "top": 2, "width": 3, "height": 4, "maximized": false } }""",
    };

    [Theory, MemberData(nameof(CorruptFiles))] // Review Focus: a truncated or hand-edited settings file
    public void A_corrupt_file_loads_as_no_placement(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);
        Assert.Null(new JsonSettingsStore(_dir).LoadWindowPlacement());
    }

    [Fact] // a corrupt file is replaced by the next save
    public void A_save_over_a_corrupt_file_replaces_it()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ truncated");
        var store = new JsonSettingsStore(_dir);
        store.SaveWindowPlacement(new WindowPlacement(1, 2, 300, 400, false));
        Assert.Equal(new WindowPlacement(1, 2, 300, 400, false), store.LoadWindowPlacement());
    }

    [Fact] // writing is best effort: an unwritable location never throws (closing must not fail)
    public void A_save_that_cannot_write_does_not_throw()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "not-a-directory");
        File.WriteAllText(blocker, "a file where the settings directory should be");
        var store = new JsonSettingsStore(blocker);
        store.SaveWindowPlacement(new WindowPlacement(1, 2, 300, 400, false));
        Assert.Null(store.LoadWindowPlacement());
    }

    // ---- M4a: the last folders (spec §3 "Saving": last folder remembered) ----

    [Fact] // each folder has its own root key, round-trips, and leaves the window and the other folder alone
    public void Folders_round_trip_under_their_own_keys_beside_the_window()
    {
        var project = Directory.CreateDirectory(Path.Combine(_dir, "projects")).FullName;
        var assets = Directory.CreateDirectory(Path.Combine(_dir, "assets")).FullName;
        var store = new JsonSettingsStore(_dir);
        Assert.Null(store.LoadFolder(SettingsFolder.Project));
        store.SaveWindowPlacement(new WindowPlacement(5, 6, 700, 500, false));
        store.SaveFolder(SettingsFolder.Project, project);
        store.SaveFolder(SettingsFolder.Asset, assets);

        var reread = new JsonSettingsStore(_dir);
        Assert.Equal(project, reread.LoadFolder(SettingsFolder.Project));
        Assert.Equal(assets, reread.LoadFolder(SettingsFolder.Asset));
        Assert.Equal(new WindowPlacement(5, 6, 700, 500, false), reread.LoadWindowPlacement());
        var root = JsonNode.Parse(File.ReadAllText(FilePath))!;
        Assert.Equal(project, root["lastProjectFolder"]!.GetValue<string>());
        Assert.Equal(assets, root["lastAssetFolder"]!.GetValue<string>());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Theory] // a folder that is gone, relative, or not a string reads as unset, so the dialog opens where Windows chooses
    [InlineData("""{ "lastProjectFolder": "C:\\no\\such\\rbx15\\folder" }""")]
    [InlineData("""{ "lastProjectFolder": "relative\\folder" }""")]
    [InlineData("""{ "lastProjectFolder": 42 }""")]
    [InlineData("""{ "lastProjectFolder": null }""")]
    [InlineData("""{ "lastProjectFolder": "" }""")]
    [InlineData("{ truncated")]
    public void A_missing_or_bad_folder_loads_as_unset(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);
        Assert.Null(new JsonSettingsStore(_dir).LoadFolder(SettingsFolder.Project));
    }

    [Fact] // a relative or empty folder is never written
    public void A_relative_or_empty_folder_is_not_saved()
    {
        var store = new JsonSettingsStore(_dir);
        store.SaveFolder(SettingsFolder.Project, "");
        store.SaveFolder(SettingsFolder.Project, "relative");
        Assert.False(File.Exists(FilePath));
    }

    [Fact] // interfaces §2.5 (M3b carry-forward): a save after a read that failed on a locked file is skipped, never written over
    public void A_save_after_a_failed_read_keeps_the_sibling_keys()
    {
        Directory.CreateDirectory(_dir);
        string original = """{ "recent": ["a.r15proj"], "window": { "left": 1, "top": 2, "width": 300, "height": 400, "maximized": false } }""";
        File.WriteAllText(FilePath, original);
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "p")).FullName;
        var store = new JsonSettingsStore(_dir);
        // Deny reading the file to the current user while leaving delete and replace allowed: the read fails with
        // UnauthorizedAccessException, and the atomic move over the file would succeed if the store tried it.
        var file = new FileInfo(FilePath);
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var deny = new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.ReadData,
            System.Security.AccessControl.AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(deny);
        file.SetAccessControl(acl);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllText(FilePath));
            Assert.Null(store.LoadFolder(SettingsFolder.Project));
            store.SaveFolder(SettingsFolder.Project, folder);
            store.SaveWindowPlacement(new WindowPlacement(9, 9, 900, 900, true));
        }
        finally
        {
            acl = file.GetAccessControl();
            acl.RemoveAccessRule(deny);
            file.SetAccessControl(acl);
        }
        Assert.Equal(original, File.ReadAllText(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact] // a non-finite or non-positive placement is never written
    public void An_unusable_placement_is_not_saved()
    {
        var store = new JsonSettingsStore(_dir);
        store.SaveWindowPlacement(new WindowPlacement(double.NaN, 0, 800, 600, false));
        store.SaveWindowPlacement(new WindowPlacement(0, 0, 0, 600, false));
        Assert.False(File.Exists(FilePath));
    }

    // ---- recentProjects (M5 item 5; ledger upgrade-recent-projects, shell-no-persistence) ----

    [Fact] // the recents are a JSON array of full paths under "recentProjects", and they round-trip in order
    public void Recents_round_trip_as_an_array_of_full_paths()
    {
        var store = new JsonSettingsStore(_dir);
        Assert.Empty(store.LoadRecents());

        store.SaveRecents([@"C:\p\b.r15proj", @"C:\p\a.r15proj"]);

        Assert.Equal([@"C:\p\b.r15proj", @"C:\p\a.r15proj"], new JsonSettingsStore(_dir).LoadRecents());
        var array = JsonNode.Parse(File.ReadAllText(FilePath))!["recentProjects"]!.AsArray();
        Assert.Equal([@"C:\p\b.r15proj", @"C:\p\a.r15proj"], array.Select(n => n!.GetValue<string>()));
    }

    [Fact] // a recents write keeps window, lastProjectFolder, lastAssetFolder and any other key
    public void A_recents_write_keeps_window_and_the_folders()
    {
        Directory.CreateDirectory(_dir);
        string project = Directory.CreateDirectory(Path.Combine(_dir, "projects")).FullName;
        string asset = Directory.CreateDirectory(Path.Combine(_dir, "assets")).FullName;
        var store = new JsonSettingsStore(_dir);
        store.SaveWindowPlacement(new WindowPlacement(5, 6, 700, 500, true));
        store.SaveFolder(SettingsFolder.Project, project);
        store.SaveFolder(SettingsFolder.Asset, asset);
        var root = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
        root["other"] = "kept";
        File.WriteAllText(FilePath, root.ToJsonString());

        store.SaveRecents([@"C:\p\a.r15proj"]);

        var after = new JsonSettingsStore(_dir);
        Assert.Equal(new WindowPlacement(5, 6, 700, 500, true), after.LoadWindowPlacement());
        Assert.Equal(project, after.LoadFolder(SettingsFolder.Project));
        Assert.Equal(asset, after.LoadFolder(SettingsFolder.Asset));
        Assert.Equal([@"C:\p\a.r15proj"], after.LoadRecents());
        Assert.Equal("kept", JsonNode.Parse(File.ReadAllText(FilePath))!["other"]!.GetValue<string>());
        // and a window or folder write keeps the recents
        after.SaveFolder(SettingsFolder.Project, asset);
        after.SaveWindowPlacement(new WindowPlacement(1, 2, 800, 600, false));
        Assert.Equal([@"C:\p\a.r15proj"], new JsonSettingsStore(_dir).LoadRecents());
    }

    public static TheoryData<string> CorruptRecents => new()
    {
        "",
        "{",
        "null",
        """{ "recentProjects": null }""",
        """{ "recentProjects": "C:\\p\\a.r15proj" }""",
        """{ "recentProjects": { "0": "C:\\p\\a.r15proj" } }""",
    };

    [Theory, MemberData(nameof(CorruptRecents))] // a hand-edited or truncated file reads as no recents
    public void Corrupt_recents_load_as_empty(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);
        Assert.Empty(new JsonSettingsStore(_dir).LoadRecents());
    }

    [Fact] // entries that are not full path strings are skipped, the good ones kept in order
    public void Entries_that_are_not_full_paths_are_skipped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "recentProjects": [ "C:\\p\\a.r15proj", 7, null, "", "relative.r15proj", "C:\\p\\b.r15proj" ] }""");
        Assert.Equal([@"C:\p\a.r15proj", @"C:\p\b.r15proj"], new JsonSettingsStore(_dir).LoadRecents());
    }

    [Fact] // the M4a rule: a recents write after a read that failed on a locked file is skipped, never written over the siblings
    public void A_recents_write_after_a_failed_read_is_skipped()
    {
        Directory.CreateDirectory(_dir);
        string original = """{ "window": { "left": 1, "top": 2, "width": 300, "height": 400, "maximized": false }, "recentProjects": ["C:\\p\\old.r15proj"] }""";
        File.WriteAllText(FilePath, original);
        var store = new JsonSettingsStore(_dir);
        var file = new FileInfo(FilePath);
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var deny = new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.ReadData,
            System.Security.AccessControl.AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(deny);
        file.SetAccessControl(acl);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllText(FilePath));
            Assert.Empty(store.LoadRecents());
            store.SaveRecents([@"C:\p\new.r15proj"]);
        }
        finally
        {
            acl = file.GetAccessControl();
            acl.RemoveAccessRule(deny);
            file.SetAccessControl(acl);
        }
        Assert.Equal(original, File.ReadAllText(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }
}
