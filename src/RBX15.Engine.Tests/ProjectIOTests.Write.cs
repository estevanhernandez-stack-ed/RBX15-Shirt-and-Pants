using System.Collections.Immutable;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>ProjectIO.Write: saveProject's fields and order (editor.js:2551-2582), version "5.0", round trips.</summary>
public partial class ProjectIOTests
{
    static readonly DateTimeOffset Pinned = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);

    static void AssertSameDocument(Document expected, Document actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.BgColor, actual.BgColor);
        Assert.Equal(expected.BgOpacity, actual.BgOpacity);
        Assert.Equal(expected.BgImage is null, actual.BgImage is null);
        if (expected.BgImage is { } bg) SamePixels(bg, actual.BgImage!);
        Assert.Equal(expected.RegionFills, actual.RegionFills);
        Assert.Equal(expected.ShowMask, actual.ShowMask);
        Assert.Equal(expected.ShowGrid, actual.ShowGrid);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int i = 0; i < expected.Layers.Count; i++)
        {
            var e = expected.Layers[i];
            var a = actual.Layers[i];
            Assert.Equal(e with { Image = a.Image }, a);
            SamePixels(e.Image, a.Image);
        }
    }

    [Theory]
    [InlineData("proj-shirt-rich")]
    [InlineData("proj-pants-legacy")]
    [InlineData("proj-shirt-drawn")]
    public void Round_trip_reproduces_an_equal_document(string name)
    {
        var first = ProjectIO.Read(Fixture(name));
        var second = ProjectIO.Read(ProjectIO.Write(first.Document, Pinned));
        AssertSameDocument(first.Document, second.Document);
        Assert.Equal(first.NextId, second.NextId);
        Assert.Empty(second.DroppedLayers);
    }

    [Theory]
    [InlineData("proj-shirt-rich")]
    [InlineData("proj-pants-legacy")]
    [InlineData("proj-shirt-drawn")]
    public void Write_keeps_saveProjects_field_set_and_order(string name)
    {
        var fixture = FixtureNode(name);
        var written = JsonNode.Parse(ProjectIO.Write(ProjectIO.Read(Fixture(name)).Document, Pinned))!.AsObject();

        Assert.Equal(fixture.Select(p => p.Key), written.Select(p => p.Key));
        var fixtureLayers = fixture["layers"]!.AsArray();
        var writtenLayers = written["layers"]!.AsArray();
        Assert.Equal(fixtureLayers.Count, writtenLayers.Count);
        for (int i = 0; i < fixtureLayers.Count; i++)
            Assert.Equal(fixtureLayers[i]!.AsObject().Select(p => p.Key), writtenLayers[i]!.AsObject().Select(p => p.Key));
        Assert.Equal(fixture["regionColors"]!.AsObject().Select(p => p.Key), written["regionColors"]!.AsObject().Select(p => p.Key));

        Assert.Equal("5.0", (string?)written["version"]);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", (string?)written["app"]);
        Assert.Equal("2026-10-11T00:00:00.000Z", (string?)written["savedAt"]);
        Assert.StartsWith("data:image/png;base64,", (string?)writtenLayers[0]!["imgSrc"]);
    }

    [Fact] // editor.js:2583 JSON.stringify(project, null, 2)
    public void Write_is_deterministic_two_space_LF_JSON()
    {
        var doc = ProjectIO.Read(Fixture("proj-shirt-drawn")).Document;
        var a = ProjectIO.Write(doc, Pinned);
        var b = ProjectIO.Write(doc, Pinned);

        Assert.Equal(a, b);
        Assert.DoesNotContain('\r', a);
        Assert.StartsWith("{\n  \"version\": \"5.0\",\n  \"app\": \"RBX15 Classic Shirt and Pants Maker\",\n  \"savedAt\": \"2026-10-11T00:00:00.000Z\",\n  \"mode\": \"Shirt\",\n", a);
        Assert.Contains("\n  \"layers\": [\n    {\n      \"id\": 1,\n      \"name\": \"L1\",\n      \"visible\": true,\n      \"opacity\": 1,\n", a);
        Assert.Contains("\"name\": \"✨ W [Anton]\"", a);
        Assert.Contains("\"x\": 251.9671641791045,", a);
        Assert.EndsWith("\n  ]\n}", a);
    }

    [Fact] // toISOString is UTC with milliseconds
    public void SavedAt_is_written_as_UTC_with_milliseconds()
    {
        var json = ProjectIO.Write(new Document(), new DateTimeOffset(2026, 10, 14, 9, 5, 7, 42, TimeSpan.FromHours(-5)));
        Assert.Equal("2026-10-14T14:05:07.042Z", (string?)JsonNode.Parse(json)!["savedAt"]);
    }

    [Fact] // empty document: no image, no fills, no layers
    public void Writes_an_empty_document_with_null_background_and_empty_collections()
    {
        var node = JsonNode.Parse(ProjectIO.Write(new Document(), Pinned))!.AsObject();
        Assert.Null(node["bgImageSrc"]);
        Assert.True(node.ContainsKey("bgImageSrc"));
        Assert.Empty(node["regionColors"]!.AsObject());
        Assert.Empty(node["layers"]!.AsArray());
        Assert.False(node.ContainsKey("title"));
    }

    [Fact] // lib/fills.js normalizeFill reads both forms identically, so a legacy string may come back as a descriptor
    public void A_legacy_string_fill_is_written_as_a_solid_descriptor()
    {
        var json = ProjectIO.Write(ProjectIO.Read(Fixture("proj-pants-legacy")).Document, Pinned);
        var fill = JsonNode.Parse(json)!["regionColors"]!["rleg_front"]!.AsObject();
        Assert.Equal(new[] { "type", "c1" }, fill.Select(p => p.Key));
        Assert.Equal(("solid", "#333"), ((string?)fill["type"], (string?)fill["c1"]));
    }

    [Fact] // fill-gradient-stops: stops are written after scale, only when there are some
    public void Stops_are_written_only_when_present_and_round_trip()
    {
        var withStops = new Fill("linear", "#ff0000", "#0000ff", 90, 16, ["#00ff00", "#ffffff"]);
        var plain = new Fill("radial", "#ff0000", "#0000ff", 45, 16);
        var doc = new Document { RegionFills = [("torso_front", withStops), ("torso_back", plain)] };

        var json = ProjectIO.Write(doc, Pinned);
        var node = JsonNode.Parse(json)!["regionColors"]!.AsObject();
        Assert.Equal(new[] { "type", "c1", "c2", "angle", "scale", "stops" }, node["torso_front"]!.AsObject().Select(p => p.Key));
        Assert.Equal(new[] { "#00ff00", "#ffffff" }, node["torso_front"]!["stops"]!.AsArray().Select(n => (string?)n));
        Assert.Equal(new[] { "type", "c1", "c2", "angle", "scale" }, node["torso_back"]!.AsObject().Select(p => p.Key));
        Assert.DoesNotContain("\"stops\": []", json);

        var back = ProjectIO.Read(json).Document;
        Assert.Equal(doc.RegionFills, back.RegionFills);
        Assert.Equal(new[] { "#00ff00", "#ffffff" }, back.RegionFills[0].Fill.Stops.ToArray());
        Assert.Empty(back.RegionFills[1].Fill.Stops);
    }

    [Fact] // a fill saved with an empty stops list reads as none and is written back without the key
    public void An_empty_or_null_stops_array_reads_as_none_and_is_not_written()
    {
        foreach (var stops in new[] { "[]", "null" })
        {
            var json = "{ \"regionColors\": { \"torso_front\": { \"type\": \"linear\", \"c1\": \"#ff0000\", \"c2\": \"#0000ff\", \"stops\": " + stops + " } } }";
            var doc = ProjectIO.Read(json).Document;
            Assert.Empty(doc.RegionFills[0].Fill.Stops);
            Assert.DoesNotContain("stops", ProjectIO.Write(doc, Pinned));
        }
    }

    [Fact] // ledger project-template-name-not-saved: an optional field 4.1 ignores, after 4.1's fields
    public void A_title_is_written_last_so_4_1_field_order_stays_a_prefix()
    {
        var node = JsonNode.Parse(ProjectIO.Write(new Document(), Pinned, "My Shirt"))!.AsObject();
        Assert.Equal("title", node.Last().Key);
        Assert.Equal("My Shirt", (string?)node["title"]);
        Assert.Equal("layers", node.Reverse().Skip(1).First().Key);
    }

    [Fact]
    public void Write_refuses_null() => Assert.Throws<ArgumentNullException>(() => ProjectIO.Write(null!, Pinned));

    [Fact] // a hand-built document round-trips too: fractional fields, adjustments, a background image, a premultiplied drawing
    public void A_built_document_round_trips_including_a_semi_transparent_image()
    {
        var soft = Px.Render(8, 8, c =>
        {
            using var p = new SKPaint { Color = new SKColor(200, 40, 90, 37), IsAntialias = true };
            c.DrawCircle(4, 4, 3.3f, p);
        });
        var doc = new Document
        {
            Mode = TemplateMode.Pants,
            BgColor = "#0a0b0c",
            BgOpacity = 0.25,
            BgImage = Px.Solid(3, 2, new SKColor(1, 2, 3)),
            RegionFills = ImmutableList.Create(("waist_front", new Fill("radial", "#ffffff", "#000000")), ("rleg_back", Fill.Solid("#123"))),
            ShowMask = false,
            ShowGrid = true,
            Layers = ImmutableList.Create(
                new Layer { Id = 9, Image = soft, Name = "Soft \"quoted\" <b>", Opacity = 0.333, X = -12.5, Y = 1e-7, Scale = 2.75, Rotation = -179.5, FlipV = true, Brightness = 0, Contrast = 200, Saturate = 0, HueRotate = 359, RShift = -255, GShift = 255, BShift = 1 }),
        };

        var back = ProjectIO.Read(ProjectIO.Write(doc, Pinned, "t"));

        AssertSameDocument(doc, back.Document);
        Assert.Equal(10, back.NextId);
    }

    /// <summary>A noisy RGBA image (a PNG of it does not compress to nothing), so an encode takes real time.</summary>
    static SKImage Noise(int size, int seed)
    {
        var rng = new Random(seed);
        var pixels = new byte[size * size * 4];
        rng.NextBytes(pixels);
        for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        return SKImage.FromPixelCopy(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul), pixels);
    }

    [Fact] // M5 item 2: a 60 s autosave of an unchanged document re-encodes nothing; each image encodes once
    public void Encoding_the_same_image_twice_through_Write_encodes_once()
    {
        var layerA = Px.Solid(6, 6, SKColors.Red);
        var layerB = Px.Solid(6, 6, SKColors.Blue);
        var doc = new Document
        {
            BgImage = Px.Solid(3, 3, SKColors.Green),
            Layers = ImmutableList.Create(new Layer { Id = 1, Image = layerA }, new Layer { Id = 2, Image = layerB }),
        };

        int before = ProjectIO.PngEncodesOnThisThread;
        var first = ProjectIO.Write(doc, Pinned);
        Assert.Equal(3, ProjectIO.PngEncodesOnThisThread - before);

        var second = ProjectIO.Write(doc, Pinned);
        var edited = doc with { ShowGrid = true, Layers = doc.Layers.SetItem(0, doc.Layers[0] with { X = 4 }) };   // a new Document over the same images
        var third = ProjectIO.Write(edited, Pinned);
        Assert.Equal(3, ProjectIO.PngEncodesOnThisThread - before);
        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
        Assert.Equal(JsonNode.Parse(first)!["layers"]!.AsArray().Select(l => (string?)l!["imgSrc"]),
                     JsonNode.Parse(third)!["layers"]!.AsArray().Select(l => (string?)l!["imgSrc"]));

        var added = edited with { Layers = edited.Layers.Add(new Layer { Id = 3, Image = Px.Solid(6, 6, SKColors.Yellow) }) };
        ProjectIO.Write(added, Pinned);
        Assert.Equal(4, ProjectIO.PngEncodesOnThisThread - before);   // only the new image encodes
    }

    [Fact] // M5 item 2 risk: autosave encodes on a pool thread while the UI thread renders the same images; both only read
    public async Task Writing_on_a_pool_thread_while_the_UI_thread_renders_the_same_images_agrees_with_both()
    {
        var doc = new Document
        {
            Layers = ImmutableList.Create(
                new Layer { Id = 1, Image = Noise(512, 1), X = 20, Y = 30 },
                new Layer { Id = 2, Image = Noise(512, 2), X = 100, Y = 90, Rotation = 17 },
                new Layer { Id = 3, Image = Noise(512, 3), X = 200, Y = 150, Opacity = 0.6 }),
        };
        var expectedExport = Renderer.ExportPng(doc);

        using var started = new ManualResetEventSlim();
        var write = Task.Run(() =>
        {
            started.Set();
            return ProjectIO.Write(doc, Pinned);
        });
        started.Wait();
        int frames = 0;
        do
        {
            Assert.Equal(expectedExport, Renderer.ExportPng(doc));
            frames++;
        } while (!write.IsCompleted || frames < 3);

        var back = ProjectIO.Read(await write);
        Assert.True(SameContent(doc, back.Document));
        foreach (var layer in back.Document.Layers) layer.Image.Dispose();
    }

    /// <summary>Layer data and pixels equal (ContentEquals compares images by reference, which a decode never keeps).</summary>
    static bool SameContent(Document a, Document b)
    {
        if (a.Layers.Count != b.Layers.Count) return false;
        for (int i = 0; i < a.Layers.Count; i++)
        {
            if (a.Layers[i] != (b.Layers[i] with { Image = a.Layers[i].Image })) return false;
            using var x = a.Layers[i].Image.Encode(SKEncodedImageFormat.Png, 100);
            using var y = b.Layers[i].Image.Encode(SKEncodedImageFormat.Png, 100);
            if (!x.AsSpan().SequenceEqual(y.AsSpan())) return false;
        }
        return true;
    }
}
