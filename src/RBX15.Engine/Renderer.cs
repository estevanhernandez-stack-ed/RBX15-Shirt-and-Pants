using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The export composition (editor.js exportPNG, lines 1512-1534). The order is a contract (spec
/// section 2): clear, background (stretched image, or color, times opacity), region fills, visible
/// layers bottom to top, then the mask with destination-in. Export always masks.
/// </summary>
public static class Renderer
{
    /// <summary>Canvas2D default smoothing ('low'): bilinear, no mipmaps.</summary>
    public static readonly SKSamplingOptions LayerSampling = new(SKFilterMode.Linear, SKMipmapMode.None);
    static readonly SKSamplingOptions MaskSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    public static SKImage Export(Document doc)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create export surface");
        RenderExport(surface.Canvas, doc);
        return surface.Snapshot();
    }

    public static byte[] ExportPng(Document doc)
    {
        using var image = Export(doc);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static void RenderExport(SKCanvas canvas, Document doc)
    {
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, doc);
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

    static void DrawBackground(SKCanvas canvas, Document doc)
    {
        var full = SKRect.Create(0, 0, TemplateSize.Width, TemplateSize.Height);
        float alpha = (float)doc.BgOpacity;
        if (doc.BgImage is { } bg)
        {
            using var p = new SKPaint { ColorF = new SKColorF(0, 0, 0, alpha) };
            canvas.DrawImage(Images.ForDrawing(bg), full, LayerSampling, p);
            return;
        }
        if (doc.BgColor is "transparent" or "") return;
        // An unparsable fillStyle is ignored by canvas, which keeps its default #000000.
        if (!CssColor.TryParseCanvasFill(doc.BgColor, out var c)) c = SKColors.Black;
        using var fill = new SKPaint { ColorF = new SKColorF(c.Red / 255f, c.Green / 255f, c.Blue / 255f, alpha) };
        canvas.DrawRect(full, fill);
    }

    /// <summary>drawLayerWithAdjustments: translate to center, rotate, then flip, opacity and filter.</summary>
    public static void DrawLayer(SKCanvas canvas, Layer layer)
    {
        if (!layer.Visible) return;
        var source = layer.RShift != 0 || layer.GShift != 0 || layer.BShift != 0
            ? Adjust.ShiftRgb(layer.Image, layer.RShift, layer.GShift, layer.BShift)
            : layer.Image;
        float w = (float)(layer.Image.Width * layer.Scale), h = (float)(layer.Image.Height * layer.Scale);
        canvas.Save();
        canvas.Translate((float)(layer.X + layer.Image.Width * layer.Scale / 2), (float)(layer.Y + layer.Image.Height * layer.Scale / 2));
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));
        if (layer.FlipH) canvas.Scale(-1, 1);
        if (layer.FlipV) canvas.Scale(1, -1);
        using var paint = new SKPaint { ColorF = new SKColorF(0, 0, 0, (float)layer.Opacity) };
        if (Adjust.HasAdjustments(layer))
            paint.ColorFilter = Adjust.FilterChain(layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate);
        canvas.DrawImage(Images.ForDrawing(source), SKRect.Create(-w / 2, -h / 2, w, h), LayerSampling, paint);
        canvas.Restore();
    }
}
