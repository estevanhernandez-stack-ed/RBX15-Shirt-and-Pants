using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The permanent guard under the Word Art rule (ruling R2). tests/goldens/app/wordart-ref holds, for
/// every wordart scene, the bitmap 4.1's own renderWordArt drew (tools/parity-ref/wordart-ref.js,
/// captured once in M2b and frozen). Swapping it in for the engine's Word Art bitmap must meet the
/// golden exactly under the unchanged GoldenRule, with no pixel differing: canvas size, the 85% fit,
/// centering, the un-premultiplied round trip and the downscaled draw are all the engine's own. The
/// only freedom the Word Art rule grants is inside the glyph bitmap.
/// </summary>
public class WordArtSwapTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");
    static readonly string RefDir = Path.Combine(Dir, "wordart-ref");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray()
            .Where(e => e.TryGetProperty("actions", out var a) && a.EnumerateArray().Any(x => x.GetProperty("op").GetString() == "wordart"))
            .Select(e => e.Clone())
            .ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    public static TheoryData<string> WordArtScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value) data.Add(NameOf(e));
        return data;
    }

    [Fact]
    public void Every_wordart_scene_has_exactly_one_frozen_4_1_bitmap()
    {
        Assert.Equal(22, Raw.Value.Count);
        var expected = Raw.Value.Select(e => NameOf(e) + ".png").Order(StringComparer.Ordinal).ToList();
        var actual = Directory.EnumerateFiles(RefDir, "*.png").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WordArtScenes))]
    public void The_4_1_word_art_bitmap_in_the_engine_document_meets_the_golden_exactly(string name)
    {
        var scene = SceneLoader.Parse(Raw.Value.Single(e => NameOf(e) == name), Root);
        var layers = scene.Document.Layers;
        var engine = layers[^1];
        var reference = Images.Decode(File.ReadAllBytes(Path.Combine(RefDir, name + ".png")));
        Assert.Equal((reference.Width, reference.Height), (engine.Image.Width, engine.Image.Height));

        var swapped = scene.Document with { Layers = layers.SetItem(layers.Count - 1, engine with { Image = reference }) };
        var goldenPath = Path.Combine(Dir, name + ".png");
        GoldenRule.AssertMatches(name, goldenPath, swapped, Path.Combine(Root, "diff-heatmaps", "app", name + ".swap.png"));

        using var golden = ImageDiff.Load(goldenPath);
        using var rendered = Renderer.Export(swapped);
        using var candidate = ImageDiff.FromImage(rendered);
        var exact = ImageDiff.Compare(golden, candidate);
        Assert.True(exact.DifferingPixels == 0, $"{name}: {exact}");
    }
}
