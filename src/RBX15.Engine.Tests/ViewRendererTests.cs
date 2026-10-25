using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// ViewRenderer: 4.1's persistent offscreen (editor.js:296-298) and checker tile (editor.js:431-438) kept across frames
/// for the canvas host (M3a final-review carry-forward: render cost). Reuse must never change a pixel, and the native
/// surfaces must be released (M2a carry-forward: native memory lifetime). Ledger render-pipeline-canvas-reset (keep:
/// "cache ... but output must remain identical").
/// </summary>
public class ViewRendererTests
{
    static Document Scene() => new()
    {
        BgOpacity = 0.6,
        RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, null)), ("rarm_front", new Fill("dots", "#050c18", "#17d4fa", null, 12))),
        Layers = ImmutableList.Create(
            new Layer { Id = 1, Image = Px.Solid(60, 24, new SKColor(242, 47, 137)), X = 250, Y = 100, Rotation = 30 },
            new Layer { Id = 2, Image = Px.Solid(80, 40, new SKColor(23, 212, 250)), X = 100, Y = 380, Opacity = 0.7 }),
    };

    /// <summary>A sequence that changes everything the renderer reuses: size, device scale, mask, grid, overlay, preview kind, and back.</summary>
    static IEnumerable<(Document Doc, ViewOptions Options)> Sequence()
    {
        var doc = Scene();
        var pen = LivePreview.Draw(new Stroke(ShapeTool.Pen, new[] { new SKPoint(240, 90), new SKPoint(300, 150), new SKPoint(340, 120) }, SKColors.Yellow, 9, false));
        var erase = LivePreview.Erase(new[] { new SKPoint(250, 105), new SKPoint(300, 130) }, 14);
        yield return (doc, new ViewOptions(1));
        yield return (doc, new ViewOptions(1) { SelectedLayerId = 1 });
        yield return (doc, new ViewOptions(1) { Preview = pen });
        yield return (doc with { ShowMask = false }, new ViewOptions(0.5) { Preview = erase });
        yield return (doc, new ViewOptions(1.5, 1, true) { SelectedLayerId = 2 });
        yield return (doc, new ViewOptions(1, 1.25) { SelectedLayerId = 1, Preview = pen });
        yield return (doc with { Layers = doc.Layers.RemoveAt(0) }, new ViewOptions(1));
        yield return (new Document(), new ViewOptions(0.25));
        yield return (doc, new ViewOptions(1));
    }

    static byte[] Draw(Action<SKCanvas> render, ViewOptions options)
    {
        var size = Renderer.ViewSize(options);
        using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        render(surface.Canvas);
        using var image = surface.Snapshot();
        using var bitmap = ImageDiff.FromImage(image);
        return bitmap.Bytes;
    }

    [Fact] // render-pipeline-canvas-reset: one renderer reused across frames draws what a fresh one draws, byte for byte
    public void Reuse_is_byte_equal_to_fresh_across_a_document_sequence()
    {
        using var reused = new ViewRenderer();
        int frame = 0;
        foreach (var (doc, options) in Sequence())
        {
            var fresh = Draw(c => Renderer.RenderView(c, doc, options), options);
            var again = Draw(c => reused.Render(c, doc, options), options);
            Assert.True(fresh.AsSpan().SequenceEqual(again), $"frame {frame} differs on the reused renderer");
            frame++;
        }
    }

    /// <summary>The heaviest document the frame budget names: 20 rotated, translucent, color-filtered layers.</summary>
    static Document TwentyLayers()
    {
        var layers = ImmutableList.CreateBuilder<Layer>();
        for (int i = 0; i < 20; i++)
            layers.Add(new Layer
            {
                Id = i + 1,
                Image = Px.Solid(160, 120, new SKColor((byte)(40 + i * 10), (byte)(220 - i * 8), (byte)(90 + i * 6))),
                X = 20 + (i % 5) * 90, Y = 20 + (i / 5) * 110,
                Rotation = i * 7, Opacity = 0.9, Scale = 1 + (i % 3) * 0.1,
                Brightness = 110, Saturate = 120,
            });
        return new Document
        {
            BgOpacity = 0.7,
            RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, null))),
            Layers = layers.ToImmutable(),
        };
    }

    static IEnumerable<LivePreview> Growing(string tool)
    {
        for (int n = 2; n <= 12; n++)
        {
            var points = Enumerable.Range(0, n).Select(i => new SKPoint(30 + i * 38, 260 + (float)(120 * Math.Sin(i / 2.0)))).ToArray();
            yield return tool == "eraser"
                ? LivePreview.Erase(points, 18)
                : LivePreview.Draw(new Stroke(tool switch { "line" => ShapeTool.Line, "rect" => ShapeTool.Rect, _ => ShapeTool.Pen },
                    points, new SKColor(255, 200, 0, 200), 11, tool == "rect"));
        }
    }

    [Theory] // render-pipeline-canvas-reset (C1): the cached static composite draws what the whole composite draws, byte for byte
    [InlineData("pen", true)]
    [InlineData("line", true)]
    [InlineData("rect", true)]
    [InlineData("eraser", true)]
    [InlineData("pen", false)]
    [InlineData("eraser", false)]
    public void A_cached_stroke_frame_is_byte_equal_to_the_uncached_frame(string tool, bool showMask)
    {
        var doc = TwentyLayers() with { ShowMask = showMask, ShowGrid = true };
        using var cached = new ViewRenderer();
        using var whole = new ViewRenderer { CacheStatic = false };
        int frame = 0;
        foreach (var preview in Growing(tool))
        foreach (var options in new[] { new ViewOptions(1) { Preview = preview, SelectedLayerId = 3 },
                                        new ViewOptions(1.5, 1.25, true) { Preview = preview, ShowRegionGuide = true } })
        {
            var expected = Draw(c => whole.Render(c, doc, options), options);
            var actual = Draw(c => cached.Render(c, doc, options), options);
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"{tool} frame {frame} differs under the cache");
            frame++;
        }
        Assert.Equal(1, cached.CompositeCacheBuilds);   // one document, one stroke: drawn into the cache once
        Assert.Equal(0, whole.CompositeCacheBuilds);
    }

    [Fact] // a new Document instance (an edit, an undo, a mode change) rebuilds the cache; the first frame without a preview releases it
    public void The_cache_follows_the_document_instance_and_a_stroke_end_releases_it()
    {
        var doc = TwentyLayers();
        var preview = Growing("pen").First();
        var options = new ViewOptions(1) { Preview = preview };
        using var r = new ViewRenderer();
        Draw(c => r.Render(c, doc, options), options);
        Draw(c => r.Render(c, doc, options), options);
        Assert.Equal(1, r.CompositeCacheBuilds);
        Assert.True(r.HasCompositeCache);

        var undone = doc with { Layers = doc.Layers.RemoveAt(19) };   // an undo mid-stroke
        var cachedFrame = Draw(c => r.Render(c, undone, options), options);
        Assert.Equal(2, r.CompositeCacheBuilds);
        using var whole = new ViewRenderer { CacheStatic = false };
        Assert.True(Draw(c => whole.Render(c, undone, options), options).AsSpan().SequenceEqual(cachedFrame));

        var mode = undone with { Mode = TemplateMode.Pants };         // a mode change mid-stroke
        Draw(c => r.Render(c, mode, options), options);
        Assert.Equal(3, r.CompositeCacheBuilds);

        var plain = new ViewOptions(1);
        Draw(c => r.Render(c, mode, plain), plain);                    // the stroke ended
        Assert.False(r.HasCompositeCache);
        Draw(c => r.Render(c, mode, options), options);                // the next stroke builds its own
        Assert.Equal(4, r.CompositeCacheBuilds);
    }

    [Fact] // a DPI change on the same renderer rebuilds the checker tile (CheckerHalf 8 -> 12 -> 8)
    public void A_device_scale_change_rebuilds_the_checker()
    {
        using var r = new ViewRenderer();
        foreach (double scale in new[] { 1, 1.5, 1 })
        {
            var options = new ViewOptions(1, scale);
            var fresh = Draw(c => Renderer.RenderView(c, new Document(), options), options);
            Assert.True(fresh.AsSpan().SequenceEqual(Draw(c => r.Render(c, new Document(), options), options)), $"device scale {scale}");
        }
    }

    [Fact] // M2a carry-forward: the persistent surfaces are released, and a disposed renderer refuses to draw
    public void Dispose_releases_and_refuses_reuse()
    {
        var r = new ViewRenderer();
        var options = new ViewOptions(1) { Preview = LivePreview.Erase(new[] { new SKPoint(1, 1), new SKPoint(50, 50) }, 4) };
        Draw(c => r.Render(c, Scene(), options), options);
        r.Dispose();
        r.Dispose(); // idempotent
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Assert.Throws<ObjectDisposedException>(() => r.Render(surface.Canvas, Scene(), new ViewOptions(1)));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        using var r = new ViewRenderer();
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Assert.Throws<ArgumentNullException>(() => r.Render(null!, new Document(), new ViewOptions(1)));
        Assert.Throws<ArgumentNullException>(() => r.Render(surface.Canvas, null!, new ViewOptions(1)));
        Assert.Throws<ArgumentNullException>(() => r.Render(surface.Canvas, new Document(), null!));
    }
}
