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

    [Fact] // a non-finite or non-positive placement is never written
    public void An_unusable_placement_is_not_saved()
    {
        var store = new JsonSettingsStore(_dir);
        store.SaveWindowPlacement(new WindowPlacement(double.NaN, 0, 800, 600, false));
        store.SaveWindowPlacement(new WindowPlacement(0, 0, 0, 600, false));
        Assert.False(File.Exists(FilePath));
    }
}
