using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2b enabled detail, wordart and project one task at a time; PendingOps lists what is still skipped).
/// <para>
/// Erase actions run through the internal EraseStroking.Replay41, which re-strokes the eraser path
/// after every mousemove exactly as app-render.js drove 4.1 (mousedown at points[0], one mousemove
/// per later point, mouseup with no stroke), so every erase scene meets the unchanged rule.
/// Production strokes once (approved divergence draw-pen-incremental-stroke); it is held to the
/// goldens directly on the two-point drags, where 4.1 strokes once too.
/// </para>
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    const string WordArtSkip = "op 'wordart' is not run by the engine yet (Engine WordArt, M2b)";
    static readonly string[] PendingOps = { "wordart" };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    static JsonElement Scene(string name) => Raw.Value.Single(e => NameOf(e) == name);

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

    /// <summary>Runnable scenes with at least one erase, every erase a two-point drag (one stroke in 4.1).</summary>
    public static TheoryData<string> TwoPointEraseScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            if (SceneLoader.UnsupportedOps(e).Count != 0 || !e.TryGetProperty("actions", out var actions)) continue;
            var erases = actions.EnumerateArray().Where(a => a.GetProperty("op").GetString() == "erase").ToList();
            if (erases.Count > 0 && erases.All(a => a.GetProperty("points").GetArrayLength() == 2)) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root, EraseStroking.Replay41);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Theory]
    [MemberData(nameof(TwoPointEraseScenes))]
    public void Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".once.png"));
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
    public void Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps()
    {
        var rows = typeof(GoldenAppTests).GetMethod(nameof(Engine_export_matches_the_frozen_4_1_app_golden))!
            .GetCustomAttributes(typeof(MemberDataAttribute), false).Cast<MemberDataAttribute>()
            .Select(a => (Op: (string)a.Parameters[0], a.Skip))
            .ToList();

        var skipOps = rows.Where(r => r.Skip != null).Select(r => r.Op).ToList();
        Assert.Equal(PendingOps.OrderBy(o => o), skipOps.OrderBy(o => o));
        Assert.Single(rows, r => r.Skip == null && r.Op == "");

        var accounted = rows.Sum(r => ((System.Collections.IEnumerable)ScenesBlockedBy(r.Op)).Cast<object>().Count());
        Assert.Equal(Raw.Value.Count, accounted);
        Assert.Equal(82, Raw.Value.Count);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
