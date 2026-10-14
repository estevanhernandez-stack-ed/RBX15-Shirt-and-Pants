using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// How the eraser's template-space stroke is rasterized before it is punched out. Internal: the
/// editor always strokes once; the replay exists only so parity tests can meet the frozen goldens.
/// </summary>
internal enum EraseStroking
{
    /// <summary>
    /// v5: the whole polyline stroked once. The approved divergence draw-pen-incremental-stroke
    /// ("Pen and eraser re-stroke the whole path"), recorded for the eraser on ledger row eraser-apply.
    /// Fully covered pixels erase exactly as in 4.1; only the antialiased fringe of a drag with three
    /// or more points keeps slightly more alpha. A two-point drag is identical (measured in M2a Task 6).
    /// </summary>
    Once,

    /// <summary>
    /// 4.1 (editor.js:1133-1144, 1200-1203): one open path, re-stroked after every mousemove, and
    /// not stroked again at mouseup for the eraser (editor.js:1253-1255 is pen only). This mirrors the
    /// event sequence tools/parity-ref/app-render.js sends: mousedown at points[0] (moveTo only), one
    /// mousemove per later point (lineTo, then stroke the whole path), mouseup with no stroke.
    /// Segment j of n is therefore composited n - j + 1 times, which only raises partially covered
    /// (antialiased) pixels. Parity only: it reproduces the frozen eraser goldens.
    /// </summary>
    Replay41,
}

/// <summary>
/// The eraser (lib/erase.js, editor.js:1265-1283). The stroke is drawn in template space, mapped
/// into the selected layer's own pixels through the inverse of its render transform, and punched
/// out with destination-out at identity, in two steps exactly as lib/erase.js does it.
/// </summary>
public static class Eraser
{
    /// <summary>editor.js:1138: eraser strokes are opaque magenta. Only their alpha matters.</summary>
    public static readonly SKColor StrokeColor = new(0xF2, 0x2F, 0x89);

    /// <summary>
    /// The layer with <paramref name="templatePoints"/> (a drag, in template coordinates) erased
    /// from its image, stroked once with round caps and joins at <paramref name="size"/>.
    /// Returns <paramref name="layer"/> itself when the stroke leaves no pixel on the 585x559
    /// stroke canvas: fewer than two points (a click without movement only calls moveTo,
    /// editor.js:1143-1144) or a drag entirely off the template (editor.js:1257-1264).
    /// Otherwise a new Layer with a new image; the old image, and any layer sharing it, is untouched.
    /// </summary>
    public static Layer Apply(Layer layer, IReadOnlyList<SKPoint> templatePoints, float size) =>
        Apply(layer, templatePoints, size, EraseStroking.Once);

    /// <summary><see cref="Apply(Layer, IReadOnlyList{SKPoint}, float)"/> with an explicit stroking mode (parity only).</summary>
    internal static Layer Apply(Layer layer, IReadOnlyList<SKPoint> templatePoints, float size, EraseStroking stroking)
    {
        using var stroke = StrokeCanvas(templatePoints, size, stroking);
        if (!HasContent(stroke)) return layer;
        return layer with { Image = EraseStroke(layer, stroke) };
    }

    /// <summary>
    /// The 585x559 transparent stroke canvas 4.1 builds on mousedown (editor.js:1129-1144):
    /// kN32 premultiplied, StrokeColor, lineWidth = size, round caps and joins, antialiased.
    /// Fewer than two points draw nothing. A width that canvas would reject (not finite, or 0 or
    /// less) keeps canvas's default lineWidth of 1.
    /// </summary>
    internal static SKImage StrokeCanvas(IReadOnlyList<SKPoint> points, float size, EraseStroking stroking = EraseStroking.Once)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (points.Count >= 2)
        {
            using var paint = new SKPaint
            {
                Color = StrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = float.IsFinite(size) && size > 0 ? size : 1,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
            };
            // Once: only the full path. Replay41: the path through points[0..end] after every move.
            int firstEnd = stroking == EraseStroking.Replay41 ? 1 : points.Count - 1;
            for (int end = firstEnd; end < points.Count; end++)
            {
                using var builder = new SKPathBuilder();
                builder.MoveTo(points[0].X, points[0].Y);
                for (int k = 1; k <= end; k++) builder.LineTo(points[k].X, points[k].Y);
                using var path = builder.Detach();
                canvas.DrawPath(path, paint);
            }
        }
        return surface.Snapshot();
    }

    /// <summary>
    /// lib/erase.js applyEraseStroke (lines 43-73). <paramref name="strokeCanvas"/> is template
    /// sized; any pixel with alpha above 0 erases, proportionally to its alpha. Returns a new
    /// image the size of <paramref name="layer"/>'s image, un-premultiplied RGBA like
    /// <see cref="Images.Decode"/>, because 4.1 round-trips the result through toDataURL and a new
    /// Image (editor.js:1276-1282). The source image is never modified.
    /// </summary>
    public static SKImage EraseStroke(Layer layer, SKImage strokeCanvas)
    {
        int imgW = layer.Image.Width, imgH = layer.Image.Height;               // erase.js:44-45
        double w = imgW * layer.Scale, h = imgH * layer.Scale;                 // erase.js:47-48
        double cx = layer.X + w / 2, cy = layer.Y + h / 2;                     // erase.js:49-50
        double rotation = double.IsNaN(layer.Rotation) ? 0 : layer.Rotation;   // erase.js:51, (t.rotation || 0)
        double rad = rotation * Math.PI / 180;
        var info = new SKImageInfo(imgW, imgH, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        // Step 1 (erase.js:56-63): the stroke into layer-image space through the inverse transform.
        // Canvas ignores a transform call with a non-finite argument; so does this (scale 0 gives 1/0).
        using var maskSurface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create mask surface");
        var m = maskSurface.Canvas;
        m.Clear(SKColors.Transparent);
        m.Translate((float)(imgW / 2.0), (float)(imgH / 2.0));
        double inv = 1 / layer.Scale;
        if (double.IsFinite(inv)) m.Scale((float)inv, (float)inv);
        m.Scale(layer.FlipH ? -1 : 1, layer.FlipV ? -1 : 1);
        if (double.IsFinite(rad)) m.RotateRadians((float)-rad);
        if (double.IsFinite(cx) && double.IsFinite(cy)) m.Translate((float)-cx, (float)-cy);
        var stroke = Images.ForDrawing(strokeCanvas);
        var ctm = m.TotalMatrix;
        using (var paint = new SKPaint
        {
            IsAntialias = Renderer.DrawImageAntialiased(ctm, stroke.Width, stroke.Height),
            ColorFilter = Renderer.IsScaleTranslate(ctm) ? null : Renderer.FloatPipeline,
        })
            m.DrawImage(stroke, SKRect.Create(0, 0, stroke.Width, stroke.Height), Renderer.LayerSampling, paint);
        using var mask = maskSurface.Snapshot();

        // Step 2 (erase.js:66-71): copy the layer image, then destination-out the mask at identity.
        using var outSurface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create erase surface");
        var o = outSurface.Canvas;
        o.Clear(SKColors.Transparent);
        using (var copy = new SKPaint())
            o.DrawImage(Images.ForDrawing(layer.Image), 0, 0, Renderer.LayerSampling, copy);
        using (var dstOut = new SKPaint { BlendMode = SKBlendMode.DstOut })
            o.DrawImage(mask, 0, 0, Renderer.LayerSampling, dstOut);
        using var erased = outSurface.Snapshot();

        var unpremul = new SKImageInfo(imgW, imgH, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(unpremul);
        if (!erased.ReadPixels(unpremul, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read erased pixels");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    /// <summary>editor.js:1257-1262: any pixel with alpha above 0.</summary>
    internal static bool HasContent(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read stroke pixels");
        var px = bmp.GetPixelSpan();
        for (int i = 3; i < px.Length; i += 4)
            if (px[i] != 0) return true;
        return false;
    }
}
