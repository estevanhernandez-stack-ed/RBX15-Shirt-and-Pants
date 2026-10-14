using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderWordArtTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static SceneLoader.Scene Load(string json) => SceneLoader.Parse(JsonDocument.Parse(json).RootElement, Root);

    [Fact]
    public void An_op_with_no_keys_uses_the_4_1_control_defaults()
    {
        // editor.html:229-262 and waStyle 'solid' (editor.js:2108): RIZZ, Anton, 64, stroke 3.
        var s = Load("""{ "name": "x", "actions": [ { "op": "wordart" } ] }""");
        var layer = Assert.Single(s.Document.Layers);
        Assert.Equal(1, layer.Id);
        Assert.Equal("✨ RIZZ [Anton]", layer.Name);
        Assert.Equal((118, 94), (layer.Image.Width, layer.Image.Height));
    }

    [Fact]
    public void Word_art_lands_above_the_scene_layers_with_the_next_id()
    {
        var s = Load("""
            { "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ],
              "actions": [ { "op": "wordart", "text": "NO\nCAP", "font": "Orbitron", "size": 72 } ] }
            """);
        Assert.Equal(new[] { 1, 2 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal("✨ NO\nCAP [Orbitron]", s.Document.Layers[1].Name);
        Assert.Equal((197, 186), (s.Document.Layers[1].Image.Width, s.Document.Layers[1].Image.Height));
    }

    [Fact]
    public void Unknown_style_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => Load("""{ "name": "x", "actions": [ { "op": "wordart", "style": "glitter" } ] }"""));

    [Fact]
    public void Every_frozen_wordart_scene_parses()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "goldens", "app", "scenes.json")));
        var scenes = doc.RootElement.EnumerateArray()
            .Where(e => e.GetProperty("name").GetString()!.StartsWith("wordart-", StringComparison.Ordinal))
            .Select(e => SceneLoader.Parse(e, Root))
            .ToList();
        Assert.Equal(22, scenes.Count);
        Assert.All(scenes, s => Assert.Single(s.Document.Layers));
    }
}
