using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Parity.Tests;

public class SceneLoaderEraseTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static SKImage Opaque(int w, int h)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(new SKColor(255, 0, 0));
        return surface.Snapshot();
    }

    static int AlphaAt(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixel(x, y).Alpha;
    }

    [Fact]
    public void Erase_replaces_only_the_indexed_layer()
    {
        var bottom = new Layer { Id = 1, Image = Opaque(100, 100) };
        var top = new Layer { Id = 2, Image = Opaque(100, 100) };
        var doc = new Document { Layers = ImmutableList.Create(bottom, top) };
        var action = Json("""{ "op": "erase", "layer": 1, "points": [[10, 50], [90, 50]], "size": 10 }""");

        var result = SceneLoader.ApplyErase(doc, action);

        Assert.Same(bottom, result.Layers[0]);
        Assert.Equal(2, result.Layers[1].Id);
        Assert.Equal(0, AlphaAt(result.Layers[1].Image, 50, 50));
        Assert.Equal(255, AlphaAt(result.Layers[1].Image, 50, 10));
    }

    [Fact]
    public void Erase_naming_a_missing_layer_is_a_scene_error()
    {
        var doc = new Document { Layers = ImmutableList.Create(new Layer { Id = 1, Image = Opaque(10, 10) }) };
        var action = Json("""{ "op": "erase", "layer": 1, "points": [[0, 0], [5, 5]], "size": 4 }""");
        Assert.Throws<InvalidDataException>(() => SceneLoader.ApplyErase(doc, action));
    }

    [Fact]
    public void Parse_runs_an_erase_after_a_shape_against_the_scene_layer()
    {
        // app-render.js indexes erase layers into the scene's own layers; a drawing appends on top
        // (editor.js:534), so index 0 is still the scene layer after the shape.
        var scene = Json("""{ "name": "e", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ], "actions": [ { "op": "shape", "tool": "line", "from": [10, 10], "to": [60, 10], "color": "#ffffff", "width": 4 }, { "op": "erase", "layer": 0, "points": [[0, 0], [64, 75], [127, 0]], "size": 30 } ] }""");
        Assert.Empty(SceneLoader.UnsupportedOps(scene));
        var s = SceneLoader.Parse(scene, Root);
        Assert.Equal(new[] { "L1", "Line drawing" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal((128, 150), (s.Document.Layers[0].Image.Width, s.Document.Layers[0].Image.Height));
    }

    [Fact]
    public void A_single_point_erase_leaves_the_layer_as_it_was()
    {
        // editor.js:1143-1144: a press without movement only calls moveTo, so nothing is erased.
        var layer = new Layer { Id = 1, Image = Opaque(20, 20) };
        var doc = new Document { Layers = ImmutableList.Create(layer) };
        var result = SceneLoader.ApplyErase(doc, Json("""{ "op": "erase", "layer": 0, "points": [[5, 5]], "size": 8 }"""));
        Assert.Same(layer, result.Layers[0]);
    }
}
