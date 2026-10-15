using System.Security.Cryptography;
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The view gate: Renderer.RenderView at device scale 1 meets 4.1's visible #mainCanvas (render(),
/// editor.js:300-429) under the unchanged golden rule, for every frozen view scene. tests/goldens/view
/// was captured once by tools/parity-ref/view-ref.js (M3a Task 2) and is never edited. Each scene
/// is an app scene plus a "view" object (zoom, mask, grid); the base document comes from SceneLoader,
/// exactly as the export gates build it. A failing scene writes diff-heatmaps/view/&lt;name&gt;.png.
/// </summary>
public class GoldenViewTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "view");

    sealed record ViewScene(string Name, JsonElement Raw, double Zoom, bool Mask, bool Grid);

    static readonly Lazy<IReadOnlyList<ViewScene>> Scenes = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e =>
        {
            var v = e.GetProperty("view");
            bool Flag(string k, bool fallback) => v.TryGetProperty(k, out var b) ? b.GetBoolean() : fallback;
            return new ViewScene(e.GetProperty("name").GetString()!, e.Clone(), v.GetProperty("zoom").GetDouble(), Flag("mask", true), Flag("grid", false));
        }).ToList();
    });

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    static Document DocumentFor(ViewScene s) =>
        SceneLoader.Parse(s.Raw, Root).Document with { ShowMask = s.Mask, ShowGrid = s.Grid };

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_view_matches_the_frozen_4_1_view(string name)
    {
        var scene = Scenes.Value.Single(s => s.Name == name);
        var doc = DocumentFor(scene);
        using var rendered = Renderer.RenderViewImage(doc, new ViewOptions(scene.Zoom, 1, doc.ShowGrid));
        GoldenRule.AssertImageMatches(name, Path.Combine(Dir, name + ".png"), rendered, Path.Combine(Root, "diff-heatmaps", "view", name + ".png"));
    }

    [Fact] // contract: zoom 0.25/0.5/1/1.5/3, mask on and off, grid in Shirt and in Pants, no selection
    public void The_frozen_set_covers_the_contracted_views_and_matches_its_index()
    {
        var scenes = Scenes.Value;
        Assert.Equal(14, scenes.Count);
        Assert.Equal(new[] { 0.25, 0.5, 1, 1.5, 3 }, scenes.Select(s => s.Zoom).Distinct().Order());
        Assert.Contains(scenes, s => !s.Mask);
        Assert.Contains(scenes, s => s.Grid && SceneLoader.Parse(s.Raw, Root).Document.Mode == TemplateMode.Shirt);
        Assert.Contains(scenes, s => s.Grid && SceneLoader.Parse(s.Raw, Root).Document.Mode == TemplateMode.Pants);
        // The unchanged rule needs scenes without RGB shift (rgb-shift-compute has its own rule).
        Assert.All(scenes, s => Assert.DoesNotContain(SceneLoader.Parse(s.Raw, Root).Document.Layers, l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "index.json")));
        var entries = index.RootElement.GetProperty("scenes").EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!);
        Assert.Equal(scenes.Select(s => s.Name).Order(StringComparer.Ordinal), entries.Keys.Order(StringComparer.Ordinal));
        foreach (var s in scenes)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Dir, s.Name + ".png"));
            Assert.Equal(entries[s.Name].GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var size = Renderer.ViewSize(new ViewOptions(s.Zoom));
            Assert.Equal((size.Width, size.Height), (entries[s.Name].GetProperty("width").GetInt32(), entries[s.Name].GetProperty("height").GetInt32()));
        }
        Assert.Equal(scenes.Count + 2, Directory.EnumerateFiles(Dir).Count()); // the PNGs, scenes.json, index.json
    }
}
