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
