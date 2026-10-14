using System.Text.Json;
using System.Text.Json.Nodes;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// ProjectIO is the port of saveProject (editor.js:2550-2591) and loadProject (editor.js:2593-2680)
/// with the spec §3 divergences. The three fixtures are the frozen files 4.1 saved
/// (tests/goldens/projects, captured by tools/parity-ref/app-render.js --save-projects).
/// </summary>
public partial class ProjectIOTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static string Fixture(string name) => File.ReadAllText(Path.Combine(Root, "tests", "goldens", "projects", name + ".r15proj"));

    static JsonObject FixtureNode(string name) => JsonNode.Parse(Fixture(name))!.AsObject();

    static SKImage Pack(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(Root, rel)));

    /// <summary>Un-premultiplied RGBA bytes, like canvas getImageData.</summary>
    static byte[] Pixels(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.Bytes;
    }

    static void SamePixels(SKImage expected, SKImage actual)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        Assert.True(Pixels(expected).AsSpan().SequenceEqual(Pixels(actual)), "pixels differ");
    }

    static string PngDataUrl(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    static readonly string RedDot = PngDataUrl(Px.Solid(2, 2, SKColors.Red));

    /// <summary>A minimal valid file with one layer per imgSrc given, ids 1..n, names N1..Nn.</summary>
    static JsonObject Minimal(params string?[] imgSrcs)
    {
        var layers = new JsonArray();
        for (int i = 0; i < imgSrcs.Length; i++)
            layers.Add(new JsonObject { ["id"] = i + 1, ["name"] = $"N{i + 1}", ["imgSrc"] = imgSrcs[i] });
        return new JsonObject { ["version"] = "4.0", ["mode"] = "Shirt", ["layers"] = layers };
    }

    static ProjectLoad Read(JsonNode node) => ProjectIO.Read(node.ToJsonString());

    // ---- the three frozen 4.1 files ----

    [Fact] // proj-shirt-rich: gradient, legacy-string, dots and camo fills; transforms, adjustments, a hidden layer
    public void Reads_the_rich_shirt_fixture_field_for_field()
    {
        var load = ProjectIO.Read(Fixture("proj-shirt-rich"));
        var d = load.Document;

        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#0f1f31", d.BgColor);
        Assert.Equal(0.85, d.BgOpacity);
        Assert.Null(d.BgImage);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
        Assert.Equal(new (string, Fill)[]
        {
            ("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45)),
            ("torso_back", new Fill("linear", "#17d4fa", "#f22f89", 45)),
            ("torso_left", Fill.Solid("#ff8800")),
            ("rarm_front", Fill.Solid("#333")),
            ("larm_front", new Fill("dots", "#050c18", "#17d4fa", null, 12)),
            ("torso_up", new Fill("camo", "#556b2f", "#c2b280", null, 16)),
        }, d.RegionFills);

        Assert.Equal(new[] { 1, 2, 3, 4 }, d.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, d.Layers.Select(l => l.Name));
        Assert.Equal((0.8, 217.0, 355.0), (d.Layers[0].Opacity, d.Layers[0].X, d.Layers[0].Y));
        var l2 = d.Layers[1];
        Assert.Equal((245.0, 110.0, 0.5, -8.0, true, false, 140.0), (l2.X, l2.Y, l2.Scale, l2.Rotation, l2.FlipH, l2.FlipV, l2.Saturate));
        var l3 = d.Layers[2];
        Assert.Equal((100.0, 380.0, 0.6, 200.0, 30, 0, 0), (l3.X, l3.Y, l3.Scale, l3.HueRotate, l3.RShift, l3.GShift, l3.BShift));
        Assert.False(d.Layers[3].Visible);
        Assert.Equal(5, load.NextId);
        Assert.Empty(load.DroppedLayers);

        // tools/parity-ref/project-scenes.json names the pack image behind each layer.
        SamePixels(Pack("packs/drips/drip_white_sleeve_v1.png"), d.Layers[0].Image);
        SamePixels(Pack("packs/word_art/word_626_anton.png"), d.Layers[1].Image);
        SamePixels(Pack("packs/drips/drip_red_wide_v1.png"), d.Layers[2].Image);
        SamePixels(Pack("packs/drips/drip_cyan_front_v1.png"), d.Layers[3].Image);
    }

    [Fact] // proj-pants-legacy: Pants mode, a background image, legacy color-string fills
    public void Reads_the_legacy_pants_fixture_with_its_background_image()
    {
        var load = ProjectIO.Read(Fixture("proj-pants-legacy"));
        var d = load.Document;

        Assert.Equal(TemplateMode.Pants, d.Mode);
        Assert.Equal("#192e44", d.BgColor);
        Assert.Equal(0.6, d.BgOpacity);
        SamePixels(Pack("packs/drips/drip_red_wide_v1.png"), d.BgImage!);
        Assert.Equal(new (string, Fill)[]
        {
            ("waist_front", Fill.Solid("#ff8800")),
            ("waist_back", Fill.Solid("#0088ff")),
            ("rleg_front", Fill.Solid("#333")),
            ("lleg_front", Fill.Solid("#f22f89")),
        }, d.RegionFills);
        var only = Assert.Single(d.Layers);
        Assert.Equal((1, "L1", 217.0, 355.0), (only.Id, only.Name, only.X, only.Y));
        SamePixels(Pack("packs/drips/drip_cyan_pants_leg_v1.png"), only.Image);
        Assert.Equal(2, load.NextId);
        Assert.Empty(load.DroppedLayers);
    }

    [Fact] // proj-shirt-drawn: drawn, detail and Word Art layers 4.1 rasterized, with fractional placement
    public void Reads_the_drawn_shirt_fixture_with_4_1_layer_names_and_fractions()
    {
        var load = ProjectIO.Read(Fixture("proj-shirt-drawn"));
        var d = load.Document;

        Assert.Equal(new (string, Fill)[] { ("torso_front", new Fill("solid", "#2d2d2d")) }, d.RegionFills);
        Assert.Equal(new[] { "L1", "Rect drawing", "Crew Neck", "✨ W [Anton]" }, d.Layers.Select(l => l.Name));
        Assert.Equal(new[] { 1, 2, 3, 4 }, d.Layers.Select(l => l.Id));
        Assert.Equal((240.0, 80.0), (d.Layers[1].X, d.Layers[1].Y));
        Assert.Equal((251.0, 62.0), (d.Layers[2].X, d.Layers[2].Y));
        Assert.Equal((251.9671641791045, 83.6, 0.8119402985074626), (d.Layers[3].X, d.Layers[3].Y, d.Layers[3].Scale));
        Assert.Equal(5, load.NextId);
        Assert.Empty(load.DroppedLayers);
    }

    // ---- 4.1 file compatibility ----

    [Theory] // 4.1 never reads version (ledger project-version-compat)
    [InlineData("\"4.0\"")]
    [InlineData("\"5.0\"")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData(null)]
    public void Version_is_never_read(string? version)
    {
        var node = Minimal(RedDot);
        if (version is not null) node["version"] = JsonNode.Parse(version);
        else node.Remove("version");
        Assert.Single(Read(node).Document.Layers);
    }

    [Fact] // editor.js:2599-2627 on an empty object: every default applies
    public void An_empty_object_is_a_fresh_document()
    {
        var load = ProjectIO.Read("{}");
        var d = load.Document;
        Assert.Equal((TemplateMode.Shirt, "#192e44", 1.0, true, false), (d.Mode, d.BgColor, d.BgOpacity, d.ShowMask, d.ShowGrid));
        Assert.Null(d.BgImage);
        Assert.Empty(d.RegionFills);
        Assert.Empty(d.Layers);
        Assert.Equal(1, load.NextId);
        Assert.Empty(load.DroppedLayers);
    }

    [Fact] // editor.js:2599, 2601, 2602, 2606, 2615, 2621-2622, 2627: falsy and null fields
    public void Top_level_falsy_and_null_fields_take_4_1s_defaults()
    {
        var load = ProjectIO.Read("""
            { "mode": "", "bgColor": "", "bgOpacity": 0, "bgImageSrc": "", "regionColors": null,
              "showMask": null, "showGrid": null, "layers": null }
            """);
        var d = load.Document;
        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#192e44", d.BgColor);       // `||`
        Assert.Equal(0, d.BgOpacity);             // `!= null` keeps 0
        Assert.Null(d.BgImage);                   // falsy src, no image
        Assert.Empty(d.RegionFills);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
        Assert.Empty(d.Layers);
    }

    [Fact] // editor.js:2636-2652, field for field
    public void Layer_fields_coerce_like_loadProject()
    {
        var node = Minimal(RedDot, RedDot);
        var a = node["layers"]![0]!.AsObject();
        a["name"] = ""; a["visible"] = null; a["opacity"] = 0; a["x"] = null; a["y"] = 0; a["scale"] = 0; a["rotation"] = null;
        a["flipH"] = null; a["flipV"] = false; a["brightness"] = 0; a["contrast"] = null; a["saturate"] = 0; a["hueRotate"] = null;
        a["rShift"] = null; a["gShift"] = 0; a["bShift"] = -12;
        var b = node["layers"]![1]!.AsObject();
        foreach (var key in new[] { "name", "visible", "opacity", "x", "y", "scale", "rotation", "flipH", "flipV", "brightness", "contrast", "saturate", "hueRotate", "rShift", "gShift", "bShift" })
            b.Remove(key);

        var layers = Read(node).Document.Layers;

        var first = layers[0];
        Assert.Equal("Layer", first.Name);                                                          // `|| 'Layer'`
        Assert.True(first.Visible);                                                                // `!== false`
        Assert.Equal(0, first.Opacity);                                                            // `!= null` keeps 0
        Assert.Equal((0.0, 0.0, 1.0, 0.0), (first.X, first.Y, first.Scale, first.Rotation));      // scale `|| 1`: 0 becomes 1
        Assert.False(first.FlipH || first.FlipV);
        Assert.Equal((0.0, 100.0, 0.0, 0.0), (first.Brightness, first.Contrast, first.Saturate, first.HueRotate));
        Assert.Equal((0, 0, -12), (first.RShift, first.GShift, first.BShift));

        var second = layers[1];
        Assert.Equal(new Layer { Id = 2, Image = second.Image }, second);                          // every default is makeLayer's
    }

    [Fact] // editor.js:2632 skips a record without imgSrc; v5 also names it
    public void A_layer_without_an_image_is_dropped_and_named()
    {
        var node = Minimal(RedDot, null, "", RedDot);
        node["layers"]![2]!.AsObject().Remove("imgSrc");

        var load = Read(node);

        Assert.Equal(new[] { 1, 4 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "N2", "N3" }, load.DroppedLayers);
        Assert.Equal(5, load.NextId);
    }

    [Fact] // paintRegions skips a falsy fill (editor.js:282); a descriptor without a type paints as a solid
    public void Falsy_fills_are_skipped_and_descriptors_keep_their_fields()
    {
        var load = ProjectIO.Read("""
            { "regionColors": { "torso_front": null, "torso_back": false, "torso_left": 0, "torso_right": "",
                                "rarm_front": "#abc", "larm_front": { "c1": "#123456" },
                                "torso_up": { "type": "stripes", "c1": "#000000", "c2": "#ffffff", "angle": 90, "scale": 8, "extra": [1] },
                                "waist_front": "#ff0000" } }
            """);
        Assert.Equal(new (string, Fill)[]
        {
            ("rarm_front", Fill.Solid("#abc")),
            ("larm_front", new Fill("solid", "#123456")),
            ("torso_up", new Fill("stripes", "#000000", "#ffffff", 90, 8)),
            ("waist_front", Fill.Solid("#ff0000")), // a Pants key in a Shirt file: kept, as 4.1 kept and re-saved it
        }, load.Document.RegionFills);
    }

    [Fact] // JSON.parse keeps a repeated key's first position and its last value
    public void A_repeated_region_key_keeps_its_first_position_and_last_value()
    {
        var load = ProjectIO.Read("""{ "regionColors": { "torso_front": "#111111", "torso_back": "#222222", "torso_front": "#333333" } }""");
        Assert.Equal(new (string, Fill)[] { ("torso_front", Fill.Solid("#333333")), ("torso_back", Fill.Solid("#222222")) }, load.Document.RegionFills);
    }

    [Fact] // ledger project-load-layers quirk 3: 4.1 kept duplicate ids and sorted them in an unspecified order
    public void Duplicate_ids_keep_file_order_and_later_holders_get_fresh_ids()
    {
        var node = Minimal(RedDot, RedDot, RedDot, RedDot);
        node["layers"]![0]!["id"] = 7;
        node["layers"]![1]!["id"] = 3;
        node["layers"]![2]!["id"] = 7;
        node["layers"]![3]!["id"] = 3;

        var load = Read(node);

        Assert.Equal(new[] { "N1", "N2", "N3", "N4" }, load.Document.Layers.Select(l => l.Name));
        Assert.Equal(new[] { 7, 3, 8, 9 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(10, load.NextId);
    }

    [Fact] // editor.js:2663 counts the layers that loaded
    public void NextId_counts_only_layers_that_loaded()
    {
        var node = Minimal(RedDot, "data:image/png;base64,AAAA");
        node["layers"]![1]!["id"] = 40;
        var load = Read(node);
        Assert.Equal(2, load.NextId);
        Assert.Equal(new[] { "N2" }, load.DroppedLayers);
    }

    // ---- image sources (spec §3: data: URLs only) ----

    [Theory] // project-load-untrusted-src: never fetched, never read from disk; the layer is dropped and named
    [InlineData("http://example.com/a.png")]
    [InlineData("https://example.com/a.png")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("C:\\art\\a.png")]
    [InlineData("blob:https://x/1")]
    public void A_layer_image_that_is_not_a_data_URL_is_dropped_and_named(string src)
    {
        var load = Read(Minimal(RedDot, src));
        Assert.Equal(new[] { 1 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "N2" }, load.DroppedLayers);
    }

    [Theory] // undecodable data URLs: bad base64, not base64, not an image, SVG (spec §3 drops SVG)
    [InlineData("data:image/png;base64,@@@@")]
    [InlineData("data:image/png,%89PNG")]
    [InlineData("data:image/png;base64,AAAAAAAA")]
    [InlineData("data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=")]
    [InlineData("data:")]
    public void A_layer_image_that_does_not_decode_is_dropped_and_named(string src)
    {
        var load = Read(Minimal(src, RedDot));
        Assert.Equal(new[] { 2 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "N1" }, load.DroppedLayers);
    }

    [Fact] // the media type is not trusted; the bytes are sniffed, as an img element does
    public void A_data_URL_decodes_by_its_bytes_not_its_media_type()
    {
        var src = RedDot.Replace("data:image/png;base64,", "DATA:application/octet-stream;BASE64,");
        var only = Assert.Single(Read(Minimal(src)).Document.Layers);
        Assert.Equal((2, 2), (only.Image.Width, only.Image.Height));
    }

    [Theory] // a non-data background refuses the whole file: nothing 4.1 saves carries one
    [InlineData("http://example.com/bg.png")]
    [InlineData("file:///C:/bg.png")]
    public void A_background_that_is_not_a_data_URL_refuses_the_file(string src)
    {
        var node = Minimal(RedDot);
        node["bgImageSrc"] = src;
        var ex = Assert.Throws<ProjectFormatException>(() => Read(node));
        Assert.Contains("bgImageSrc must be a data: URL", ex.Message);
    }

    [Fact] // a data-URL background that does not decode is dropped and reported
    public void A_background_that_does_not_decode_is_dropped_and_reported()
    {
        var node = Minimal(RedDot);
        node["bgImageSrc"] = "data:image/png;base64,AAAA";
        var load = Read(node);
        Assert.Null(load.Document.BgImage);
        Assert.Equal(new[] { "Background image" }, load.DroppedLayers);
        Assert.Single(load.Document.Layers);
    }

    // ---- structural problems: all-or-nothing ----

    [Theory]
    [InlineData("", "not JSON")]
    [InlineData("{ \"mode\": \"Shirt\", ", "not JSON")]
    [InlineData("{ 'mode': 'Shirt' }", "not JSON")]
    [InlineData("[]", "a JSON array, not a project object")]
    [InlineData("null", "a JSON null, not a project object")]
    [InlineData("\"Shirt\"", "a JSON string, not a project object")]
    [InlineData("{ \"mode\": \"pants\" }", "unknown mode \"pants\"")]
    [InlineData("{ \"mode\": \"Robot\" }", "unknown mode \"Robot\"")]
    [InlineData("{ \"mode\": 1 }", "mode must be a string")]
    [InlineData("{ \"bgColor\": 25 }", "bgColor must be a string")]
    [InlineData("{ \"bgOpacity\": \"0.5\" }", "bgOpacity must be a number")]
    [InlineData("{ \"bgOpacity\": 1e400 }", "bgOpacity is out of range")]
    [InlineData("{ \"bgImageSrc\": {} }", "bgImageSrc must be a string")]
    [InlineData("{ \"showMask\": 1 }", "showMask must be true or false")]
    [InlineData("{ \"showGrid\": \"no\" }", "showGrid must be true or false")]
    [InlineData("{ \"regionColors\": [] }", "regionColors must be an object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": 5 } }", "regionColors.torso_front must be a color string or a fill object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": true } }", "regionColors.torso_front must be a color string or a fill object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": [\"#fff\"] } }", "regionColors.torso_front must be a color string or a fill object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": { \"c1\": 255 } } }", "regionColors.torso_front.c1 must be a string")]
    [InlineData("{ \"regionColors\": { \"torso_front\": { \"angle\": \"45\" } } }", "regionColors.torso_front.angle must be a number")]
    [InlineData("{ \"layers\": {} }", "layers must be an array")]
    [InlineData("{ \"layers\": [5] }", "layers[0] must be an object")]
    [InlineData("{ \"layers\": [null] }", "layers[0] must be an object")]
    [InlineData("{ \"layers\": [{ \"name\": \"A\" }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": \"1\" }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 1.5 }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 0 }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"x\": \"10\" }] }", "layers[0].x must be a number")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"visible\": 0 }] }", "layers[0].visible must be true or false")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"flipH\": \"true\" }] }", "layers[0].flipH must be true or false")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"name\": 7 }] }", "layers[0].name must be a string")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"rShift\": 1.5 }] }", "layers[0].rShift must be a whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"imgSrc\": 12 }] }", "layers[0].imgSrc must be a string")]
    public void A_structural_problem_throws_naming_it(string json, string expected)
    {
        var ex = Assert.Throws<ProjectFormatException>(() => ProjectIO.Read(json));
        Assert.Contains(expected, ex.Message);
    }

    [Fact] // validate first: a bad field after good images still refuses the whole file
    public void A_problem_in_the_last_layer_refuses_the_file_even_after_valid_layers()
    {
        var node = Minimal(RedDot, RedDot);
        node["layers"]![1]!["opacity"] = "half";
        var ex = Assert.Throws<ProjectFormatException>(() => Read(node));
        Assert.Contains("layers[1].opacity must be a number", ex.Message);
    }

    [Fact] // an id past int.MaxValue after a valid image layer: refused, nothing leaks or crashes
    public void A_layer_id_past_int_max_after_a_valid_image_layer_refuses_the_file()
    {
        var node = Minimal(RedDot, RedDot);
        node["layers"]![1]!["id"] = 2147483648L;
        var ex = Assert.Throws<ProjectFormatException>(() => Read(node));
        Assert.Contains("layers[1].id", ex.Message);
    }

    [Fact] // int.MaxValue itself would wrap nextId (max + 1), so ids stop at int.MaxValue - 1
    public void A_layer_id_of_int_max_is_refused_naming_the_layer()
    {
        var node = Minimal(RedDot, RedDot);
        node["layers"]![1]!["id"] = int.MaxValue;
        var ex = Assert.Throws<ProjectFormatException>(() => Read(node));
        Assert.Contains("layers[1].id", ex.Message);

        node["layers"]![1]!["id"] = int.MaxValue - 1;
        var load = Read(node);
        Assert.Equal(int.MaxValue, load.NextId);
        foreach (var l in load.Document.Layers) l.Image.Dispose();
    }

    [Fact]
    public void Read_refuses_null() => Assert.Throws<ArgumentNullException>(() => ProjectIO.Read(null!));
}
