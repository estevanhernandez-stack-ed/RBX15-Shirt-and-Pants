using System.Runtime.InteropServices;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>The drawing tools that make layers. 4.1's tool id for Ellipse is 'circle'.</summary>
public enum ShapeTool { Pen, Line, Rect, Ellipse }

/// <summary>
/// One finished gesture in template coordinates. Pen uses every point; Line, Rect and Ellipse use
/// the first and the last (4.1 draws shapes from the press point to the last move point).
/// </summary>
public sealed record Stroke(ShapeTool Tool, IReadOnlyList<SKPoint> Points, SKColor Color, float Width, bool Fill);

/// <summary>
/// Pen, line, rect and ellipse geometry, autocrop and the drawing commit (editor.js:1120-1293,
/// :528-540, :144-170). Strokes render on the same kN32 premultiplied basis as <see cref="Renderer"/>,
/// which is the surface Chromium's software canvas draws into.
/// </summary>
public static class Tools
{
    /// <summary>The name 4.1 gives a drawing layer: the tool id capitalized plus ' drawing' (editor.js:1285).</summary>
    public static string DrawingName(ShapeTool tool) => tool switch
    {
        ShapeTool.Pen => "Pen drawing",
        ShapeTool.Line => "Line drawing",
        ShapeTool.Rect => "Rect drawing",
        ShapeTool.Ellipse => "Circle drawing",
        _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };

    /// <summary>
    /// A 585x559 transparent canvas with the stroke drawn as 4.1 draws it. Pen: one path stroked
    /// once with round caps and joins (approved divergence: 4.1 re-strokes per move); a single point
    /// is a filled dot of diameter Width (approved divergence: 4.1 paints nothing). Shapes need two
    /// points, as 4.1 only draws them on mousemove (editor.js:1205-1234).
    /// </summary>
    public static SKImage RenderStroke(Stroke stroke)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var pts = stroke.Points;
        if (stroke.Tool == ShapeTool.Pen)
        {
            if (pts.Count > 0) DrawPen(canvas, stroke);
        }
        else if (pts.Count >= 2)
        {
            DrawShape(canvas, stroke, pts[0], pts[pts.Count - 1]);
        }
        return surface.Snapshot();
    }

    // Canvas state for a stroke: lineWidth = drawSize, lineCap round on every tool (editor.js:1140,
    // :1210); lineJoin round for the pen (:1141) and canvas-default miter, miterLimit 10, for shapes.
    static SKPaint StrokePaint(Stroke s, SKStrokeJoin join) => new()
    {
        Color = s.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = s.Width,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = join, StrokeMiter = 10,
    };

    static SKPaint FillPaint(Stroke s) => new() { Color = s.Color, IsAntialias = true, Style = SKPaintStyle.Fill };

    static void DrawPen(SKCanvas canvas, Stroke s)
    {
        var pts = s.Points;
        if (pts.Count == 1)
        {
            using var dot = FillPaint(s);
            canvas.DrawCircle(pts[0], s.Width / 2, dot);
            return;
        }
        using var pb = new SKPathBuilder();
        pb.MoveTo(pts[0].X, pts[0].Y);
        for (int i = 1; i < pts.Count; i++) pb.LineTo(pts[i].X, pts[i].Y);
        using var path = pb.Detach();
        using var paint = StrokePaint(s, SKStrokeJoin.Round);
        canvas.DrawPath(path, paint);
    }

    static void DrawShape(SKCanvas canvas, Stroke s, SKPoint a, SKPoint b)
    {
        switch (s.Tool)
        {
            case ShapeTool.Line:
            {
                // editor.js:1212-1216. drawFill is never read for lines.
                using var pb = new SKPathBuilder();
                pb.MoveTo(a.X, a.Y);
                pb.LineTo(b.X, b.Y);
                using var path = pb.Detach();
                using var paint = StrokePaint(s, SKStrokeJoin.Miter);
                canvas.DrawPath(path, paint);
                break;
            }
            case ShapeTool.Rect:
            {
                // editor.js:1218-1223: normalized box, fillRect or strokeRect.
                var rect = SKRect.Create(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
                if (s.Fill)
                {
                    using var fill = FillPaint(s);
                    canvas.DrawRect(rect, fill);
                }
                else
                {
                    StrokeRect(canvas, rect, s);
                }
                break;
            }
            case ShapeTool.Ellipse:
            {
                // editor.js:1225-1233, in JS doubles, handed to canvas as floats.
                float cx = (float)(((double)a.X + b.X) / 2), cy = (float)(((double)a.Y + b.Y) / 2);
                float rx = (float)(Math.Abs((double)b.X - a.X) / 2), ry = (float)(Math.Abs((double)b.Y - a.Y) / 2);
                using var path = EllipsePath(cx, cy, rx, ry);
                if (path is null) break;
                using var paint = s.Fill ? FillPaint(s) : StrokePaint(s, SKStrokeJoin.Miter);
                canvas.DrawPath(path, paint);
                break;
            }
        }
    }

    /// <summary>
    /// Blink StrokeRectOnCanvas: a rect with exactly one zero side is stroked as the closed path
    /// (x, y) to (right, bottom), so the zero-length sides add nothing; otherwise drawRect.
    /// </summary>
    static void StrokeRect(SKCanvas canvas, SKRect r, Stroke s)
    {
        using var paint = StrokePaint(s, SKStrokeJoin.Miter);
        if ((r.Width > 0) != (r.Height > 0))
        {
            using var pb = new SKPathBuilder();
            pb.MoveTo(r.Left, r.Top);
            pb.LineTo(r.Right, r.Bottom);
            pb.Close();
            using var line = pb.Detach();
            canvas.DrawPath(line, paint);
            return;
        }
        canvas.DrawRect(r, paint);
    }

    /// <summary>
    /// ctx.ellipse(cx, cy, rx, ry, 0, 0, 2pi) after beginPath, as Blink builds it. Full ellipse:
    /// two 180-degree arcs from angle 0 (Path::AddEllipse). A zero radius: DegenerateEllipse, line
    /// segments through the points at 0, pi/2, pi, 3pi/2 and 2pi; both zero leaves a lone moveTo,
    /// which paints nothing, so null.
    /// </summary>
    static SKPath? EllipsePath(float cx, float cy, float rx, float ry)
    {
        if (rx == 0 && ry == 0) return null;
        using var pb = new SKPathBuilder();
        if (rx == 0 || ry == 0)
        {
            const float halfPi = MathF.PI / 2, twoPi = MathF.PI * 2;
            pb.MoveTo(cx + rx, cy);
            for (float angle = halfPi; angle < twoPi; angle += halfPi)
                pb.LineTo(cx + rx * MathF.Cos(angle), cy + ry * MathF.Sin(angle));
            pb.LineTo(cx + rx * MathF.Cos(twoPi), cy + ry * MathF.Sin(twoPi));
            return pb.Detach();
        }
        var oval = new SKRect(cx - rx, cy - ry, cx + rx, cy + ry);
        pb.MoveTo(cx + rx, cy);
        pb.ArcTo(oval, 0, 180, false);
        pb.ArcTo(oval, 180, 180, false);
        return pb.Detach();
    }

    /// <summary>
    /// autocrop (editor.js:144-170): the alpha &gt; 0 bounding box, padded (pad || 10, so 0 means 10),
    /// clamped to the canvas, copied at identity into a kN32 premultiplied image. Null when nothing
    /// has alpha. Caller owns the returned image.
    /// </summary>
    public static (SKImage Image, int X, int Y)? Autocrop(SKImage canvas, int pad = 10)
    {
        if (pad == 0) pad = 10;
        if (pad < 0) throw new ArgumentOutOfRangeException(nameof(pad), "4.1 callers never pass a negative pad");
        int sw = canvas.Width, sh = canvas.Height;
        var alpha = ReadAlpha(canvas);
        int minX = sw, minY = sh, maxX = 0, maxY = 0;
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (alpha[y * sw + x] > 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < minX) return null;
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(sw - 1, maxX + pad);
        maxY = Math.Min(sh - 1, maxY + pad);
        int cropW = maxX - minX + 1, cropH = maxY - minY + 1;

        var info = new SKImageInfo(cropW, cropH, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create crop surface");
        surface.Canvas.Clear(SKColors.Transparent);
        // drawImage(source, minX, minY, w, h, 0, 0, w, h): an integer translate at identity scale,
        // so every pixel copies exactly.
        using var copy = new SKPaint();
        surface.Canvas.DrawImage(canvas, -minX, -minY, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), copy);
        return (surface.Snapshot(), minX, minY);
    }

    static byte[] ReadAlpha(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Alpha8, SKAlphaType.Premul);
        var alpha = new byte[image.Width * image.Height];
        var handle = GCHandle.Alloc(alpha, GCHandleType.Pinned);
        try
        {
            if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), image.Width, 0, 0))
                throw new InvalidOperationException("cannot read stroke alpha");
        }
        finally
        {
            handle.Free();
        }
        return alpha;
    }

    /// <summary>
    /// The layer 4.1's mouseup + addDrawingLayer create (editor.js:1257-1285, :528-540): the stroke
    /// autocropped, at the crop origin, scale 1, every other field a makeLayer default (:134-142).
    /// The image is stored un-premultiplied, the form 4.1's toDataURL + img decode round trip
    /// leaves it in and the form Images.Decode gives; drawing re-premultiplies it. Null when the
    /// stroke left no pixel with alpha &gt; 0 (no layer, ledger draw-empty-no-layer).
    /// </summary>
    public static Layer? CommitDrawing(Stroke stroke, int id, string name)
    {
        using var drawn = RenderStroke(stroke);
        if (Autocrop(drawn) is not { } crop) return null;
        using var premul = crop.Image;
        var info = new SKImageInfo(premul.Width, premul.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!premul.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read cropped drawing");
        return new Layer { Id = id, Name = name, Image = SKImage.FromPixelCopy(bmp.PeekPixels()), X = crop.X, Y = crop.Y, Scale = 1 };
    }
}
