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
}
