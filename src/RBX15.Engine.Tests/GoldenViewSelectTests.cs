using System.Security.Cryptography;
using System.Text.Json;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The view-select gate: Renderer.RenderView with a selection overlay or a live preview meets 4.1's visible #mainCanvas
/// (render(), editor.js:300-429) for every frozen scene in tests/goldens/view-select, captured once by
/// tools/parity-ref/view-select-ref.js (M3b Task 1) and never edited. Each scene is a view scene plus either
/// <c>view.select</c> (the 0-based base layer the overlay is drawn for; 4.1's selectLayer sets the Select tool) or a
/// <c>preview</c> (a shape or a two-point eraser drag left mid-stroke). The frame is held to the unchanged rule, with
/// only the rotation label's box blanked in both images (ruling R4, <see cref="SelectionGeometry.LabelBounds"/>).
/// A failing scene writes diff-heatmaps/view-select/&lt;name&gt;.png.
/// </summary>
public class GoldenViewSelectTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "view-select");

    sealed record SelectScene(string Name, JsonElement Raw, double Zoom, bool Mask, bool Grid, int? Select, JsonElement? Preview);

    static readonly Lazy<IReadOnlyList<SelectScene>> Scenes = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e =>
        {
            var v = e.GetProperty("view");
            bool Flag(string k, bool fallback) => v.TryGetProperty(k, out var b) ? b.GetBoolean() : fallback;
            int? select = v.TryGetProperty("select", out var sel) ? sel.GetInt32() : null;
            JsonElement? preview = e.TryGetProperty("preview", out var p) ? p.Clone() : null;
            return new SelectScene(e.GetProperty("name").GetString()!, e.Clone(), v.GetProperty("zoom").GetDouble(),
                Flag("mask", true), Flag("grid", false), select, preview);
        }).ToList();
    });

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    static SKPoint Point(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle());

    /// <summary>The engine's preview for a scene's <c>preview</c> object, built from the same fields 4.1 was driven with.</summary>
    static LivePreview PreviewFor(JsonElement p) => p.GetProperty("op").GetString() switch
    {
        "shape" => LivePreview.Draw(new Stroke(
            p.GetProperty("tool").GetString() switch
            {
                "line" => ShapeTool.Line,
                "rect" => ShapeTool.Rect,
                "ellipse" => ShapeTool.Ellipse,
                var t => throw new InvalidDataException($"unknown shape tool {t}"),
            },
            new[] { Point(p.GetProperty("from")), Point(p.GetProperty("to")) },
            SKColor.Parse(p.GetProperty("color").GetString()!),
            p.GetProperty("width").GetSingle(),
            p.GetProperty("fill").GetBoolean())),
        "erase" => LivePreview.Erase(p.GetProperty("points").EnumerateArray().Select(Point).ToArray(), p.GetProperty("size").GetSingle()),
        var op => throw new InvalidDataException($"unknown preview op {op}"),
    };

    static (Document Doc, ViewOptions Options) Frame(SelectScene s)
    {
        var doc = SceneLoader.Parse(s.Raw, Root).Document with { ShowMask = s.Mask, ShowGrid = s.Grid };
        var options = new ViewOptions(s.Zoom, 1, s.Grid)
        {
            SelectedLayerId = s.Select is int i ? doc.Layers[i].Id : null,
            Preview = s.Preview is { } p ? PreviewFor(p) : null,
        };
        return (doc, options);
    }

    static IReadOnlyList<SKRectI> Excluded(Document doc, ViewOptions options)
    {
        if (options.SelectedLayerId is not int id) return [];
        var label = SelectionGeometry.LabelBounds(doc.Layers.Single(l => l.Id == id), options);
        return label.IsEmpty ? [] : [label];
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_view_with_overlay_or_preview_matches_the_frozen_4_1_view(string name)
    {
        var (doc, options) = Frame(Scenes.Value.Single(s => s.Name == name));
        using var rendered = Renderer.RenderViewImage(doc, options);
        GoldenRule.AssertImageMatchesExcept(name, Path.Combine(Dir, name + ".png"), rendered,
            Path.Combine(Root, "diff-heatmaps", "view-select", name + ".png"), Excluded(doc, options));
    }

    [Fact] // the gate is not vacuous: without the overlay or the preview every frozen frame fails the same rule
    public void Without_the_overlay_or_the_preview_every_scene_fails()
    {
        foreach (var s in Scenes.Value)
        {
            var (doc, options) = Frame(s);
            using var plain = Renderer.RenderViewImage(doc, options with { SelectedLayerId = null, Preview = null });
            using var golden = ImageDiff.Load(Path.Combine(Dir, s.Name + ".png"));
            using var candidate = ImageDiff.FromImage(plain);
            foreach (var bitmap in new[] { golden, candidate })
                foreach (var r in Excluded(doc, options))
                    for (int y = r.Top; y < r.Bottom; y++)
                        for (int x = r.Left; x < r.Right; x++)
                            bitmap.SetPixel(x, y, SKColors.Transparent);
            Assert.False(ImageDiff.Compare(golden, candidate).Pass, $"{s.Name}: the frame without its overlay or preview passed");
        }
    }

    [Fact] // ruling R4's cross-check: inside the label box every flat pixel of 4.1's frame still matches within 1 level
    public void The_label_box_meets_the_word_art_rule()
    {
        int labelled = 0;
        foreach (var s in Scenes.Value)
        {
            var (doc, options) = Frame(s);
            var excluded = Excluded(doc, options);
            if (excluded.Count == 0) continue;
            labelled++;
            using var rendered = Renderer.RenderViewImage(doc, options);
            using var golden = ImageDiff.Load(Path.Combine(Dir, s.Name + ".png"));
            using var candidate = ImageDiff.FromImage(rendered);
            var box = SKRectI.Intersect(excluded[0], new SKRectI(0, 0, golden.Width, golden.Height));
            using var g = new SKBitmap(new SKImageInfo(box.Width, box.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using var c = new SKBitmap(new SKImageInfo(box.Width, box.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (int y = 0; y < box.Height; y++)
                for (int x = 0; x < box.Width; x++)
                {
                    g.SetPixel(x, y, golden.GetPixel(box.Left + x, box.Top + y));
                    c.SetPixel(x, y, candidate.GetPixel(box.Left + x, box.Top + y));
                }
            GoldenRule.AssertWordArtBitmaps(s.Name + " label", g, c, Path.Combine(Root, "diff-heatmaps", "view-select", s.Name + "-label.png"));
        }
        Assert.Equal(2, labelled);
    }

    [Fact] // the blanked rects are the only pixels the rule skips
    public void The_exclusion_blanks_only_its_rects()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rbx15-view-select-rule-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string goldenPath = Path.Combine(dir, "golden.png");
            using (var golden = Px.Solid(40, 40, SKColors.Navy))
            using (var data = golden.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(goldenPath, data.ToArray());
            using var patched = Px.Render(40, 40, c =>
            {
                c.Clear(SKColors.Navy);
                using var p = new SKPaint { Color = SKColors.Yellow };
                c.DrawRect(SKRect.Create(10, 10, 8, 8), p);
            });
            string heat = Path.Combine(dir, "heat.png");
            GoldenRule.AssertImageMatchesExcept("inside", goldenPath, patched, heat, [new SKRectI(10, 10, 18, 18)]);
            GoldenRule.AssertImageMatchesExcept("clipped", goldenPath, patched, heat, [new SKRectI(-5, -5, 30, 30)]);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                GoldenRule.AssertImageMatchesExcept("short", goldenPath, patched, heat, [new SKRectI(10, 10, 17, 18)]));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                GoldenRule.AssertImageMatchesExcept("none", goldenPath, patched, heat, []));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact] // contract: box, handles, rotation handle, the label at a nonzero angle, zoom, flip, grid, and each preview kind
    public void The_frozen_set_covers_the_contracted_frames_and_matches_its_index()
    {
        var scenes = Scenes.Value;
        Assert.Equal(new[]
        {
            "vs-select-z100", "vs-select-z200", "vs-select-rot30-z100", "vs-select-rotneg135-z150", "vs-select-fliph-z50",
            "vs-select-pants-grid-z100", "vs-preview-rect-z100", "vs-preview-ellipse-fill-z200", "vs-preview-line-z50",
            "vs-preview-erase-z100",
        }, scenes.Select(s => s.Name));
        Assert.Equal(new[] { 0.5, 1, 1.5, 2 }, scenes.Select(s => s.Zoom).Distinct().Order());
        Assert.All(scenes, s => Assert.True(s.Select.HasValue != s.Preview.HasValue, $"{s.Name}: exactly one of select and preview"));

        var frames = scenes.ToDictionary(s => s.Name, Frame);
        var rotations = scenes.Where(s => s.Select.HasValue)
            .Select(s => frames[s.Name].Doc.Layers.Single(l => l.Id == frames[s.Name].Options.SelectedLayerId).Rotation).ToList();
        Assert.Contains(0.0, rotations);                       // no label
        Assert.Contains(30.0, rotations);                      // a label at a nonzero angle
        Assert.Contains(-135.0, rotations);                    // past 90, the knob below the center
        Assert.Contains(scenes, s => s.Select.HasValue && frames[s.Name].Doc.Layers[s.Select.Value].FlipH);
        Assert.Contains(scenes, s => s.Select.HasValue && s.Grid && frames[s.Name].Doc.Mode == TemplateMode.Pants);
        var kinds = scenes.Where(s => s.Preview.HasValue).Select(s => frames[s.Name].Options.Preview!)
            .Select(p => p.IsEraser ? "erase" : p.Stroke.Tool.ToString()).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { "Ellipse", "Line", "Rect", "erase" }, kinds);
        Assert.All(scenes.Where(s => s.Preview.HasValue), s => Assert.True(frames[s.Name].Options.Preview!.Stroke.Points.Count == 2));
        Assert.All(scenes, s => Assert.DoesNotContain(frames[s.Name].Doc.Layers, l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "index.json")));
        Assert.Equal("tools/parity-ref/view-select-ref.js", index.RootElement.GetProperty("tool").GetString());
        var entries = index.RootElement.GetProperty("scenes").EnumerateArray().ToList();
        Assert.Equal(scenes.Select(s => s.Name), entries.Select(e => e.GetProperty("name").GetString()));
        foreach (var (s, e) in scenes.Zip(entries))
        {
            var bytes = File.ReadAllBytes(Path.Combine(Dir, s.Name + ".png"));
            Assert.Equal(e.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var size = Renderer.ViewSize(new ViewOptions(s.Zoom));
            Assert.Equal((size.Width, size.Height), (e.GetProperty("width").GetInt32(), e.GetProperty("height").GetInt32()));
            Assert.Equal(s.Select, e.GetProperty("select").ValueKind == JsonValueKind.Null ? null : e.GetProperty("select").GetInt32());
            Assert.Equal(s.Preview?.GetProperty("op").GetString(), e.GetProperty("preview").GetString());
        }
        Assert.Equal(scenes.Count + 2, Directory.EnumerateFiles(Dir).Count()); // the PNGs, scenes.json, index.json
    }
}
