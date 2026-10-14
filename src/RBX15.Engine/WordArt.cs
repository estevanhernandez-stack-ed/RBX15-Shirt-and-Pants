using SkiaSharp;
using HB = HarfBuzzSharp;

namespace RBX15.Engine;

/// <summary>The six style buttons (editor.html:269-274, data-style solid..retro).</summary>
public enum WordArtStyle { Solid, Gradient, Neon, Shadow, Outline, Retro }

/// <summary>
/// The Word Art controls (editor.html:229-262): text, font family, style, size, fill (waColor),
/// stroke (waStroke), gradient end (waColor2), stroke width (waStrokeW) and gradient angle in
/// degrees (waAngle).
/// </summary>
public sealed record WordArtSettings(string Text, string Font, WordArtStyle Style, float Size, SKColor Fill, SKColor Stroke,
                                     SKColor Grad, float StrokeWidth, float Angle);

/// <summary>
/// The Word Art generator: renderWordArt (editor.js:2120-2240) and Add to canvas (editor.js:2257-2291).
/// Lines are shaped with HarfBuzz as Blink shapes them (kerning included) and drawn as text blobs
/// with the SkFont Chromium's canvas uses (<see cref="Fonts.CreateFont"/>), on a kN32 premultiplied
/// surface like every other engine draw.
/// </summary>
public static class WordArt
{
    /// <summary>4.1 always asks for weight 900 (editor.js:2131).</summary>
    public const int Weight = 900;

    /// <summary>The waSize input's min and max (editor.html:256), enforced (approved divergence; 4.1 accepts any typed value).</summary>
    public const float MinSize = 8, MaxSize = 300;

    /// <summary>One shaped line: glyph ids, each glyph's x from the line's left edge, and the advance width.</summary>
    internal sealed record ShapedLine(ushort[] Glyphs, float[] Xs, float Width);

    /// <summary>waText || 'TEXT' (editor.js:2121, :2270).</summary>
    internal static string EffectiveText(string text) => text.Length == 0 ? "TEXT" : text;

    /// <summary>+waSize || 64 (editor.js:2125), then clamped to 8..300.</summary>
    internal static float EffectiveSize(float size) => float.IsNaN(size) || size == 0 ? 64 : Math.Clamp(size, MinSize, MaxSize);

    /// <summary>pad (editor.js:2145): max(strokeW*2, neon 30 else 10, shadow 15, retro 12).</summary>
    internal static double Pad(WordArtStyle style, float strokeWidth) =>
        Math.Max(Math.Max(strokeWidth * 2.0, style == WordArtStyle.Neon ? 30 : 10),
                 Math.Max(style == WordArtStyle.Shadow ? 15 : 0, style == WordArtStyle.Retro ? 12 : 0));

    /// <summary>The Outline style's line width: strokeW || 3 (editor.js:2217).</summary>
    internal static float OutlineWidth(float strokeWidth) => strokeWidth == 0 || float.IsNaN(strokeWidth) ? 3 : strokeWidth;

    /// <summary>
    /// What textBaseline 'middle' adds to y (Blink TextMetrics::GetFontBaseline): half of ascent minus
    /// descent, where the OS/2 typo ascent and descent are scaled to sum to the font size keeping their
    /// ratio, each rounded to a LayoutUnit (1/64 px) (SimpleFontData::NormalizeEmHeightMetrics).
    /// </summary>
    internal static float MiddleOffset(SKTypeface typeface, float size)
    {
        var os2 = typeface.GetTableData(Os2Tag);
        if (os2 is null || os2.Length < 72) throw new InvalidDataException($"{typeface.FamilyName} has no OS/2 typo metrics");
        float ascent = (short)(os2[68] << 8 | os2[69]), descent = -(short)(os2[70] << 8 | os2[71]);
        float emAscent = ascent * size / (ascent + descent);
        return (LayoutUnit(emAscent) - LayoutUnit(size - emAscent)) / 2;
    }

    static float LayoutUnit(float v) => MathF.Round(v * 64, MidpointRounding.AwayFromZero) / 64;

    static readonly uint Os2Tag = 'O' << 24 | 'S' << 16 | '/' << 8 | '2';

    /// <summary>
    /// Shapes one line with HarfBuzz at the font size in 16.16 fixed point and the request's wght, as
    /// Blink's HarfBuzzShaper does; advances and offsets come back as floats summed left to right.
    /// </summary>
    internal static ShapedLine Shape(string family, float size, string line)
    {
        using var blob = HB.Blob.FromStream(new MemoryStream(Fonts.FileBytes(family, Weight), writable: false));
        using var face = new HB.Face(blob, 0);
        using var font = new HB.Font(face);
        int scale = (int)(size * 65536);
        font.SetScale(scale, scale);
        if (Fonts.AxisWeight(family, Weight) is { } wght)
            font.SetVariations(new[] { new HB.Variation { Tag = Fonts.WghtTag, Value = wght } });
        using var buffer = new HB.Buffer();
        buffer.AddUtf16(line);
        buffer.GuessSegmentProperties();
        font.Shape(buffer);
        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var glyphs = new ushort[infos.Length];
        var xs = new float[infos.Length];
        float pen = 0;
        for (int i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            xs[i] = pen + positions[i].XOffset / 65536f;
            pen += positions[i].XAdvance / 65536f;
        }
        return new ShapedLine(glyphs, xs, pen);
    }

    /// <summary>
    /// renderWordArt (editor.js:2120-2240) on a new surface. With no maxima (null or 0, as 4.1's falsy
    /// check) the surface is the content size cw x ch, as Add to canvas renders it; with maxima it is
    /// min(cw, max) x min(ch, max) and the content is scaled by the smaller ratio, as the preview
    /// renders it. Returns the image (caller owns it) and cw, ch.
    /// </summary>
    public static (SKImage Image, int Width, int Height) Render(WordArtSettings s, int? maxWidth = null, int? maxHeight = null)
    {
        if (maxWidth < 0 || maxHeight < 0) throw new ArgumentOutOfRangeException(nameof(maxWidth), "maxima are positive, or null or 0 for none");
        string text = EffectiveText(s.Text);
        float size = EffectiveSize(s.Size);
        using var font = Fonts.CreateFont(s.Font, Weight, size);

        // editor.js:2135-2154, in JS doubles.
        var lines = text.Split('\n').Select(l => Shape(s.Font, size, l)).ToList();
        double lineHeight = size * 1.15;
        double totalW = lines.Max(l => (double)l.Width);
        double totalH = lines.Count * lineHeight;
        double pad = Pad(s.Style, s.StrokeWidth);
        int cw = (int)Math.Ceiling(totalW + pad * 2);
        int ch = (int)Math.Ceiling(totalH + pad * 2);
        int width = maxWidth is > 0 ? Math.Min(cw, maxWidth.Value) : cw;
        int height = maxHeight is > 0 ? Math.Min(ch, maxHeight.Value) : ch;
        double sc = Math.Min((double)width / cw, (double)height / ch);

        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create word art surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)sc, (float)sc);
        float middle = MiddleOffset(font.Typeface, size);
        double cx = cw / 2.0;
        double startY = pad + lineHeight / 2;
        for (int li = 0; li < lines.Count; li++)
            DrawLine(canvas, font, lines[li], s, cx, startY + li * lineHeight, middle, cw, ch);
        return (surface.Snapshot(), cw, ch);
    }

    /// <summary>One line's passes (editor.js:2166-2236).</summary>
    static void DrawLine(SKCanvas canvas, SKFont font, ShapedLine line, WordArtSettings s, double cx, double ty, float middle, int cw, int ch)
    {
        if (line.Glyphs.Length == 0) return;
        using var builder = new SKTextBlobBuilder();
        builder.AddHorizontalRun(line.Glyphs, font, line.Xs, 0);
        using var blob = builder.Build();

        // fillText/strokeText(line, x, y) with textAlign center and textBaseline middle: Blink takes x
        // and y as floats, moves left by half the width and down by the middle offset.
        void Text(double x, double y, SKPaint paint) => canvas.DrawText(blob, (float)x - line.Width / 2, (float)y + middle, paint);

        // editor.js:2171-2179: the gradient spans the whole canvas around each line's center.
        using var gradient = s.Style == WordArtStyle.Gradient ? Gradient(s, cx, ty, cw, ch) : null;

        if (s.Style == WordArtStyle.Shadow)
        {
            // editor.js:2182-2188: rgba(0,0,0,0.6) at +4,+4, no blur.
            using var shadow = FillPaint(new SKColor(0, 0, 0, 153));
            Text(cx + 4, ty + 4, shadow);
        }

        if (s.Style == WordArtStyle.Retro)
        {
            // editor.js:2190-2196: 8 copies, deepest first, even offsets in the stroke color.
            for (int i = 8; i > 0; i--)
            {
                using var copy = FillPaint(i % 2 == 0 ? s.Stroke : s.Grad);
                Text(cx + i, ty + i, copy);
            }
        }

        if (s.Style == WordArtStyle.Neon)
        {
            // editor.js:2198-2213: the fill twice with a glow of the fill color (blur 20, then 40),
            // then a white core at globalAlpha 0.8. Stroke settings are ignored.
            using var glow = FillPaint(s.Fill);
            DrawWithShadow(s.Fill, 20, glow, p => Text(cx, ty, p));
            DrawWithShadow(s.Fill, 40, glow, p => Text(cx, ty, p));
            using var core = new SKPaint { ColorF = new SKColorF(1, 1, 1, 0.8f), IsAntialias = true };
            Text(cx, ty, core);
        }
        else if (s.Style == WordArtStyle.Outline)
        {
            // editor.js:2214-2219: stroked in the fill color, strokeW || 3, round joins.
            using var outline = StrokePaint(s.Fill, OutlineWidth(s.StrokeWidth));
            Text(cx, ty, outline);
        }
        else
        {
            // editor.js:2220-2235: fill, then stroke and fill again when strokeW > 0.
            using var fill = FillPaint(s.Fill);
            if (gradient is not null)
            {
                fill.Shader = gradient;
                fill.IsDither = true; // Chromium dithers canvas gradients; measured against the goldens.
            }
            Text(cx, ty, fill);
            if (s.StrokeWidth > 0)
            {
                using var stroke = StrokePaint(s.Stroke, s.StrokeWidth);
                Text(cx, ty, stroke);
                Text(cx, ty, fill);
            }
        }

        // A draw with shadowColor and shadowBlur set (Blink's shadow draw looper): the shadow first,
        // the same draw recolored src-in to the shadow color under a normal blur of sigma blur/2 that
        // ignores the canvas transform, then the draw itself.
        static void DrawWithShadow(SKColor color, float blur, SKPaint paint, Action<SKPaint> draw)
        {
            using var shadow = paint.Clone();
            using var recolor = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
            using var blurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur / 2, false);
            shadow.ColorFilter = recolor;
            shadow.MaskFilter = blurFilter;
            draw(shadow);
            draw(paint);
        }
    }

    static SKShader Gradient(WordArtSettings s, double cx, double ty, int cw, int ch)
    {
        double angle = s.Angle * Math.PI / 180;
        double gx = Math.Cos(angle) * cw / 2, gy = Math.Sin(angle) * ch / 2;
        return SKShader.CreateLinearGradient(new SKPoint((float)(cx - gx), (float)(ty - gy)), new SKPoint((float)(cx + gx), (float)(ty + gy)),
            new[] { s.Fill, s.Grad }, null, SKShaderTileMode.Clamp);
    }

    static SKPaint FillPaint(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>strokeText state: lineJoin round (editor.js:2218, :2230), canvas-default butt cap and miter limit 10.</summary>
    static SKPaint StrokePaint(SKColor color, float width) => new()
    {
        Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
        StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Butt, StrokeMiter = 10,
    };

    /// <summary>The layer name (editor.js:2276): a sparkle, the first 20 UTF-16 units of the text, the font family.</summary>
    public static string LayerName(string text, string font)
    {
        var t = EffectiveText(text);
        return "✨ " + (t.Length > 20 ? t[..20] : t) + " [" + font + "]";
    }

    /// <summary>
    /// Add to canvas (editor.js:2257-2291): the full-size render as a new layer, scaled to 85% of its
    /// fit in the front slot and centered there (editor.js:2272-2279). Template.Slot makes this work in
    /// Pants mode, where 4.1 throws (ledger mode-pants-crash-hardcoded-keys). The image is stored
    /// un-premultiplied, the form 4.1's toDataURL and img decode leave it in; every other field is a
    /// makeLayer default.
    /// </summary>
    public static Layer CreateLayer(WordArtSettings s, TemplateMode mode, int id)
    {
        var (rendered, _, _) = Render(s);
        using (rendered)
        {
            var info = new SKImageInfo(rendered.Width, rendered.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bmp = new SKBitmap(info);
            if (!rendered.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
                throw new InvalidOperationException("cannot read word art pixels");
            var front = Template.Slot(mode, BodyPart.Body, Face.Front);
            double scale = Math.Min((double)front.W / rendered.Width, (double)front.H / rendered.Height) * 0.85;
            return new Layer
            {
                Id = id,
                Name = LayerName(s.Text, s.Font),
                Image = SKImage.FromPixelCopy(bmp.PeekPixels()),
                X = front.X + (front.W - rendered.Width * scale) / 2,
                Y = front.Y + (front.H - rendered.Height * scale) / 2,
                Scale = scale,
            };
        }
    }
}
