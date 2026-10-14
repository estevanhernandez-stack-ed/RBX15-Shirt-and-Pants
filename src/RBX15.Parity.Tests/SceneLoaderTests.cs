using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static SceneLoader.Scene Load(string json) => SceneLoader.Parse(JsonDocument.Parse(json).RootElement, Root);

    [Fact]
    public void Defaults_match_the_4_1_editor()
    {
        var s = Load("""{ "name": "x" }""");
        Assert.Equal("x", s.Name);
        Assert.Equal(new Document().BgColor, s.Document.BgColor);
        Assert.Equal(TemplateMode.Shirt, s.Document.Mode);
        Assert.Empty(s.Document.Layers);
    }

    [Fact]
    public void Region_fills_keep_their_order_and_accept_legacy_strings()
    {
        var s = Load("""{ "name": "x", "mode": "Pants", "regionColors": { "waist_back": "#123", "waist_front": { "type": "dots", "c1": "#000", "c2": "#fff", "scale": 10 } } }""");
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Equal(new[] { "waist_back", "waist_front" }, s.Document.RegionFills.Select(f => f.Region));
        Assert.Equal(Fill.Solid("#123"), s.Document.RegionFills[0].Fill);
        Assert.Equal(new Fill("dots", "#000", "#fff", null, 10), s.Document.RegionFills[1].Fill);
    }

    [Fact]
    public void Layers_get_makeLayer_defaults_and_sequential_ids()
    {
        var s = Load("""{ "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png", "x": 5, "rShift": 7 }, { "image": "packs/drips/drip_cyan_front_v1.png", "visible": false } ] }""");
        var (a, b) = (s.Document.Layers[0], s.Document.Layers[1]);
        Assert.Equal((1, 2), (a.Id, b.Id));
        Assert.Equal((5.0, 0.0, 1.0, 1.0, 7), (a.X, a.Y, a.Scale, a.Opacity, a.RShift));
        Assert.False(b.Visible);
        Assert.Equal((128, 150), (a.Image.Width, a.Image.Height));
    }

    [Fact]
    public void All_frozen_scenes_load()
    {
        var scenes = SceneLoader.LoadAll(Path.Combine(Root, "tests", "goldens", "render", "scenes.json"), Root);
        Assert.Equal(34, scenes.Count); // 30 M0/M1 scenes + 4 M1-gap scenes (M2a Task 3)
    }
}
