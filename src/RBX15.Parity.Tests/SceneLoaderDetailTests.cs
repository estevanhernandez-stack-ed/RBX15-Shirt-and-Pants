using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderDetailTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static SceneLoader.Scene Load(string json) => SceneLoader.Parse(JsonDocument.Parse(json).RootElement, Root);

    [Fact]
    public void A_detail_appends_a_named_layer_after_the_scene_layers()
    {
        // app-harness.js detail(): set #detailColor, #detailWidth, #detailDash, click the button;
        // createDetailLayer pushes the layer on top with the next id (editor.js:1858-1859).
        var s = Load("""
            { "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ],
              "actions": [ { "op": "detail", "type": "hem", "color": "#333333", "width": 2, "stitch": false } ] }
            """);
        Assert.Equal(new[] { 1, 2 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal("Hem Line", s.Document.Layers[1].Name);
        Assert.Equal((154.0, 187.0, 1.0), (s.Document.Layers[1].X, s.Document.Layers[1].Y, s.Document.Layers[1].Scale));
    }

    [Fact]
    public void Stitch_defaults_to_off()
    {
        // app-harness.js: setCheck('detailDash', !!op.stitch).
        var plain = Load("""{ "name": "x", "actions": [ { "op": "detail", "type": "yoke", "color": "#333333", "width": 2 } ] }""");
        var off = Details.Create(DetailType.Yoke, TemplateMode.Shirt, new DetailStyle(CssColor.ToSk("#333333"), 2, false), 1)!;
        var layer = plain.Document.Layers.Single();
        Assert.Equal((off.X, off.Y, off.Image.Width, off.Image.Height), (layer.X, layer.Y, layer.Image.Width, layer.Image.Height));
        using var a = ImageDiff.FromImage(off.Image);
        using var b = ImageDiff.FromImage(layer.Image);
        Assert.Equal(0, ImageDiff.Compare(a, b).DifferingPixels);
    }

    [Fact]
    public void Two_clicks_stack_two_identical_layers()
    {
        // Ledger details-mode-independence-and-selection: duplicates are not deduplicated.
        var s = Load("""
            { "name": "x", "actions": [
                { "op": "detail", "type": "pocket", "color": "#ffffff", "width": 2 },
                { "op": "detail", "type": "pocket", "color": "#ffffff", "width": 2 } ] }
            """);
        Assert.Equal(new[] { 1, 2 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal((s.Document.Layers[0].X, s.Document.Layers[0].Y), (s.Document.Layers[1].X, s.Document.Layers[1].Y));
        var (a, b) = (s.Document.Layers[0].Image, s.Document.Layers[1].Image);
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        using var ba = ImageDiff.FromImage(a);
        using var bb = ImageDiff.FromImage(b);
        Assert.Equal(0, ImageDiff.Compare(ba, bb).DifferingPixels);
    }

    [Fact]
    public void An_unknown_detail_type_is_a_scene_error()
    {
        Assert.Throws<InvalidDataException>(() => Load("""
            { "name": "x", "actions": [ { "op": "detail", "type": "belt-loops", "color": "#333333", "width": 2 } ] }
            """));
    }

    [Fact]
    public void A_pants_scene_runs_details_on_the_pants_faces()
    {
        // Approved divergence details-pants-mode-behavior: 4.1 throws (app-harness.js refuses Pants).
        var s = Load("""{ "name": "x", "mode": "Pants", "actions": [ { "op": "detail", "type": "full-detail", "color": "#333333", "width": 2 } ] }""");
        Assert.Equal("Full Detail Set", s.Document.Layers.Single().Name);
        Assert.Equal((8.0, 63.0), (s.Document.Layers[0].X, s.Document.Layers[0].Y));
    }
}
