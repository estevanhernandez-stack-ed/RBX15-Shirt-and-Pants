using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2a Task 6 for erase, M2b for detail, wordart and project).
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    const string EraseSkip = "op 'erase' is not run by the engine yet (Engine Eraser, M2a)";
    const string DetailSkip = "op 'detail' is not run by the engine yet (Engine Details, M2b)";
    const string WordArtSkip = "op 'wordart' is not run by the engine yet (Engine WordArt, M2b)";
    const string ProjectSkip = "op 'project' is not run by the engine yet (Engine ProjectIO, M2b)";
    static readonly string[] PendingOps = { "erase", "detail", "wordart", "project" };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
    public static TheoryData<string> ScenesBlockedBy(string op)
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            var unsupported = SceneLoader.UnsupportedOps(e);
            if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    [MemberData(nameof(ScenesBlockedBy), "erase", Skip = EraseSkip)]
    [MemberData(nameof(ScenesBlockedBy), "detail", Skip = DetailSkip)]
    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]
    [MemberData(nameof(ScenesBlockedBy), "project", Skip = ProjectSkip)]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Raw.Value.Single(e => NameOf(e) == name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Fact]
    public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
    {
        var unclassified = Raw.Value
            .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
            .ToList();
        Assert.Empty(unclassified);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
