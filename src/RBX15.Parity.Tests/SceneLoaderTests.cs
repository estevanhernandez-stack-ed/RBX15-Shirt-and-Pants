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

    [Fact]
    public void Shape_actions_append_drawing_layers_after_the_scene_layers()
    {
        var s = Load("""
            { "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ],
              "actions": [
                { "op": "shape", "tool": "rect", "from": [100, 100], "to": [150, 120], "color": "#17d4fa", "width": 4, "fill": true },
                { "op": "shape", "tool": "ellipse", "from": [200, 200], "to": [260, 240], "color": "#f22f89", "width": 6 } ] }
            """);
        Assert.Equal(new[] { 1, 2, 3 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "L1", "Rect drawing", "Circle drawing" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal((90.0, 90.0), (s.Document.Layers[1].X, s.Document.Layers[1].Y));
    }

    [Fact]
    public void A_shape_that_draws_nothing_adds_no_layer()
    {
        var s = Load("""
            { "name": "x", "actions": [ { "op": "shape", "tool": "rect", "from": [100, 100], "to": [100, 100], "color": "#17d4fa", "width": 4, "fill": true } ] }
            """);
        Assert.Empty(s.Document.Layers);
    }

    [Fact]
    public void Unsupported_ops_are_listed_once_in_order_and_refuse_to_parse()
    {
        // detail stays unsupported through M2a (Engine Details is M2b), so Task 6 adding erase leaves this intact.
        var e = JsonDocument.Parse("""
            { "name": "x", "actions": [
                { "op": "shape", "tool": "line", "from": [0, 0], "to": [10, 10], "color": "#000000", "width": 2 },
                { "op": "wordart", "text": "HI" }, { "op": "detail", "type": "hem" }, { "op": "wordart", "text": "YO" } ] }
            """).RootElement;
        Assert.Equal(new[] { "wordart", "detail" }, SceneLoader.UnsupportedOps(e));
        var ex = Assert.Throws<NotSupportedException>(() => SceneLoader.Parse(e, Root));
        Assert.Contains("wordart, detail", ex.Message);
    }

    [Fact]
    public void An_action_without_an_op_refuses_to_parse()
    {
        // Review Focus: a hand-written or truncated action is reported as op "" and never half-applied.
        var e = JsonDocument.Parse("""{ "name": "x", "actions": [ { "tool": "line" } ] }""").RootElement;
        Assert.Equal(new[] { "" }, SceneLoader.UnsupportedOps(e));
        Assert.Throws<NotSupportedException>(() => SceneLoader.Parse(e, Root));
    }

    [Fact]
    public void Scenes_without_actions_have_no_unsupported_ops()
    {
        Assert.Empty(SceneLoader.UnsupportedOps(JsonDocument.Parse("""{ "name": "x" }""").RootElement));
    }

    [Fact]
    public void Unknown_shape_tool_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Load("""
            { "name": "x", "actions": [ { "op": "shape", "tool": "star", "from": [0, 0], "to": [10, 10], "color": "#000000", "width": 2 } ] }
            """));
    }
}
