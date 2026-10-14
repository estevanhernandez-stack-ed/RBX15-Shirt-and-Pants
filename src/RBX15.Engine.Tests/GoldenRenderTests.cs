using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M1 gate: the engine's export of every frozen 4.1 scene passes the golden rule (spec section 6).
/// Goldens are never edited here. A failing scene writes a heatmap to diff-heatmaps/ (a CI artifact).
/// </summary>
public class GoldenRenderTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "render");
    static readonly Lazy<IReadOnlyList<SceneLoader.Scene>> Scenes = new(() => SceneLoader.LoadAll(Path.Combine(Dir, "scenes.json"), Root));

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_export_matches_the_frozen_4_1_golden(string name)
    {
        var scene = Scenes.Value.Single(s => s.Name == name);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", name + ".png"));
    }
}
