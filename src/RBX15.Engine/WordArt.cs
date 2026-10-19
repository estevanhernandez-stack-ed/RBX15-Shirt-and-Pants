using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
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

    /// <summary>The waStrokeW slider's min and max (editor.html:263), enforced like the size.</summary>
    public const float MinStrokeWidth = 0, MaxStrokeWidth = 20;

    /// <summary>
    /// The waFont options in order: the CSS family and the label the select shows (editor.html:230-246).
    /// The families are <see cref="Fonts.WordArtFamilies"/>. The select does not draw an option in its own face.
    /// </summary>
    public static readonly IReadOnlyList<(string Family, string Label)> FontOptions = new (string, string)[]
    {
        ("Anton", "Anton (Block)"), ("Bebas Neue", "Bebas Neue (Tall)"), ("Dancing Script", "Dancing Script"),
        ("Great Vibes", "Great Vibes"), ("Pacifico", "Pacifico (Retro)"), ("Creepster", "Creepster"),
        ("Poppins", "Poppins (Bold)"), ("Lato", "Lato (Modern)"), ("Bangers", "Bangers (Comic)"),
        ("Permanent Marker", "Permanent Marker"), ("Bungee Shade", "Bungee Shade (3D)"), ("Rubik Mono One", "Rubik Mono"),
        ("Press Start 2P", "Press Start 2P"), ("Righteous", "Righteous"), ("Orbitron", "Orbitron (Cyber)"),
    };

    /// <summary>
    /// One run of a shaped line, all in one face: the vendored face (<c>Primary</c>) or the system face
    /// a character the family lacks falls back to. Glyph ids, and each glyph's x from the line's left edge.
    /// </summary>
    internal sealed record ShapedRun(SKTypeface Typeface, bool Primary, ushort[] Glyphs, float[] Xs);

    /// <summary>One shaped line: its runs in text order, and the advance width.</summary>
    internal sealed record ShapedLine(IReadOnlyList<ShapedRun> Runs, float Width);

    /// <summary>A system face a missing character falls back to, with the bytes HarfBuzz shapes from.</summary>
    internal sealed record FallbackFace(SKTypeface Typeface, byte[] Bytes, int Index);

    static readonly ConcurrentDictionary<(string Family, int Codepoint), FallbackFace?> FallbackByCharacter = new();
    static readonly ConcurrentDictionary<string, FallbackFace> FallbackFaces = new(StringComparer.Ordinal);

    /// <summary>The style 4.1 asks for (editor.js:2131): weight 900, normal width, upright.</summary>
    static readonly SKFontStyle RequestStyle = new(SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    /// <summary>waText || 'TEXT' (editor.js:2121, :2270).</summary>
    internal static string EffectiveText(string text) => text.Length == 0 ? "TEXT" : text;

    /// <summary>+waSize || 64 (editor.js:2125), then clamped to 8..300.</summary>
    internal static float EffectiveSize(float size) => float.IsNaN(size) || size == 0 ? 64 : Math.Clamp(size, MinSize, MaxSize);

    /// <summary>+waStrokeW || 0 (editor.js:2125, NaN becomes 0), then clamped to the slider's 0..20.</summary>
    internal static float EffectiveStrokeWidth(float strokeWidth) =>
        float.IsNaN(strokeWidth) ? 0 : Math.Clamp(strokeWidth, MinStrokeWidth, MaxStrokeWidth);

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
    /// Shapes one line as Blink's HarfBuzzShaper does. The line splits into runs by font coverage, one
    /// grapheme cluster at a time: a cluster whose first character the vendored face covers stays in it,
    /// and any other falls back to Arial when Arial has it (Chromium's generic sans-serif on Windows),
    /// else to the system face SKFontManager.MatchCharacter picks for the family at weight 900, as
    /// Chromium falls back (4.1's font string ends in ", sans-serif", editor.js:2131).
    /// Each run is shaped with HarfBuzz at the font size in 16.16 fixed point, the vendored run at the
    /// request's wght; advances and offsets come back as floats summed left to right. Text the vendored
    /// face covers is one run, shaped exactly as one HarfBuzz call over the whole line.
    /// </summary>
    internal static ShapedLine Shape(string family, float size, string line)
    {
        var primary = Fonts.Get(family, Weight);
        var runs = new List<ShapedRun>();
        float pen = 0;
        foreach (var (start, length, fallback) in Segment(primary, family, line))
        {
            var text = line.Substring(start, length);
            var (glyphs, xs, advance) = fallback is null
                ? ShapeRun(Fonts.FileBytes(family, Weight), 0, Fonts.AxisWeight(family, Weight), size, text, pen)
                : ShapeRun(fallback.Bytes, fallback.Index, null, size, text, pen);
            runs.Add(new ShapedRun(fallback?.Typeface ?? primary, fallback is null, glyphs, xs));
            pen += advance;
        }
        return new ShapedLine(runs, pen);
    }

    /// <summary>Consecutive grapheme clusters that draw in the same face: start, length, and the fallback face (null for the vendored face).</summary>
    static IEnumerable<(int Start, int Length, FallbackFace? Fallback)> Segment(SKTypeface primary, string family, string line)
    {
        int runStart = 0;
        FallbackFace? runFace = null;
        for (int i = 0; i < line.Length;)
        {
            int length = StringInfo.GetNextTextElementLength(line, i);
            Rune.DecodeFromUtf16(line.AsSpan(i), out var rune, out _);
            var face = Covers(primary, rune.Value) ? null : Fallback(family, rune.Value);
            if (i > runStart && !ReferenceEquals(face, runFace))
            {
                yield return (runStart, i - runStart, runFace);
                runStart = i;
            }
            runFace = face;
            i += length;
        }
        if (line.Length > runStart) yield return (runStart, line.Length - runStart, runFace);
    }

    /// <summary>
    /// The system face for a character the vendored family lacks, or null when no installed face has it
    /// (the cluster then stays in the vendored face). Cached per family and character, with one
    /// <see cref="FallbackFace"/> per distinct face, so neighbouring fallback characters share a run.
    /// </summary>
    static FallbackFace? Fallback(string family, int codepoint) => FallbackByCharacter.GetOrAdd((family, codepoint), key =>
    {
        // The generic sans-serif first (Arial on Windows, where Chromium resolves it), as the font
        // list '900 <size>px "<family>", sans-serif' (editor.js:2131) asks; then the system fallback.
        if (SansSerif.Value is { } sans && Covers(sans, key.Codepoint)) return Register(sans, owned: false);
        var typeface = SKFontManager.Default.MatchCharacter(key.Family, RequestStyle, null, key.Codepoint);
        if (typeface is null) return null;
        // Not disposed: the wrapper may be a cached face's (one managed wrapper per native face), so it leaks like SansSerif.
        if (!CoversOnce(typeface, key.Codepoint)) return null;
        return Register(typeface, owned: true);
    });

    /// <summary>
    /// The one cached <see cref="FallbackFace"/> per distinct face. An owned typeface that lost the race
    /// to an equal cached face is released; the shared sans-serif typeface is never disposed.
    /// </summary>
    static FallbackFace Register(SKTypeface typeface, bool owned)
    {
        var id = $"{typeface.FamilyName}|{typeface.FontWeight}|{typeface.FontWidth}|{typeface.FontSlant}";
        var face = FallbackFaces.GetOrAdd(id, _ =>
        {
            using var stream = typeface.OpenStream(out int index) ?? throw new InvalidDataException($"cannot read {typeface.FamilyName}");
            using var data = SKData.Create(stream);
            return new FallbackFace(typeface, data.ToArray(), index);
        });
        if (owned && !ReferenceEquals(face.Typeface, typeface)) typeface.Dispose();
        return face;
    }

    /// <summary>
    /// The face Chromium's generic sans-serif resolves to on Windows: Arial at the request's style
    /// (weight 900 resolves to Arial Black, ariblk.ttf, which DirectWrite groups into the Arial
    /// family). Null when the machine has no Arial (the font manager's substitute is not taken), so the
    /// system fallback decides. Lives for the process.
    /// </summary>
    static readonly Lazy<SKTypeface?> SansSerif = new(() =>
        SKFontManager.Default.MatchFamily("Arial", RequestStyle) is { FamilyName: "Arial" } arial ? arial : null);

    /// <summary>
    /// One SkFont per long-lived face for coverage checks: <see cref="Segment"/> asks once per grapheme
    /// cluster, and a font per question made a 2,000-character line allocate 2,000 native fonts. Glyph
    /// coverage does not depend on size. Only the vendored faces and the shared sans-serif come here.
    /// </summary>
    static readonly ConcurrentDictionary<SKTypeface, Lazy<SKFont>> CoverageFonts = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether a long-lived face has a glyph for the character (SKTypeface.ContainsGlyph is obsolete in 4.153.1).</summary>
    static bool Covers(SKTypeface typeface, int codepoint) =>
        CoverageFonts.GetOrAdd(typeface, t => new Lazy<SKFont>(() => new SKFont(t))).Value.ContainsGlyph(codepoint);

    /// <summary>Whether a face the font manager just returned has the character. Not cached: the face may be dropped.</summary>
    static bool CoversOnce(SKTypeface typeface, int codepoint)
    {
        using var font = new SKFont(typeface);
        return font.ContainsGlyph(codepoint);
    }

    /// <summary>
    /// Every font file HarfBuzz shapes from, pinned for the life of the process. The arrays are the
    /// cache-owned ones (<see cref="Fonts.FileBytes(string, int)"/>, <see cref="FallbackFace.Bytes"/>), so
    /// there is one handle per file. HarfBuzzSharp's Blob.FromStream is not used: under concurrent
    /// shaping (xUnit's parallel test classes) it hands HarfBuzz another font's bytes or freed memory,
    /// measured as wrong glyph ids and as 0xC0000005 in hb_shape_full. A read-only blob over pinned
    /// bytes is HarfBuzz's own zero-copy form and has no release callback.
    /// </summary>
    static readonly ConcurrentDictionary<byte[], Lazy<GCHandle>> Pinned = new(ReferenceEqualityComparer.Instance);

    static HB.Blob BlobOf(byte[] fontBytes)
    {
        var handle = Pinned.GetOrAdd(fontBytes, b => new Lazy<GCHandle>(() => GCHandle.Alloc(b, GCHandleType.Pinned))).Value;
        return new HB.Blob(handle.AddrOfPinnedObject(), fontBytes.Length, HB.MemoryMode.ReadOnly);
    }

    /// <summary>One run through HarfBuzz; its glyph x positions start at <paramref name="pen"/>.</summary>
    static (ushort[] Glyphs, float[] Xs, float Advance) ShapeRun(byte[] fontBytes, int index, float? wght, float size, string text, float pen)
    {
        using var blob = BlobOf(fontBytes);
        using var face = new HB.Face(blob, index);
        using var font = new HB.Font(face);
        int scale = (int)(size * 65536);
        font.SetScale(scale, scale);
        if (wght is { } w)
            font.SetVariations(new[] { new HB.Variation { Tag = Fonts.WghtTag, Value = w } });
        using var buffer = new HB.Buffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        font.Shape(buffer);
        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var glyphs = new ushort[infos.Length];
        var xs = new float[infos.Length];
        float x = pen;
        for (int i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            xs[i] = x + positions[i].XOffset / 65536f;
            x += positions[i].XAdvance / 65536f;
        }
        return (glyphs, xs, x - pen);
    }

    /// <summary>
    /// What <see cref="Render"/> returns. <see cref="Image"/> is the bitmap (the caller owns it).
    /// <see cref="ContentWidth"/> and <see cref="ContentHeight"/> are 4.1's cw and ch, the requested size, which is
    /// the image size only when nothing clamps it. <see cref="ContentScale"/> is the fraction of that requested size
    /// the content was drawn at: 1 when the render fits, less when a maximum or an image cap shrank it. A layer made
    /// from the image multiplies its scale by 1 / ContentScale to land at the requested size.
    /// </summary>
    public sealed record WordArtRender(SKImage Image, int ContentWidth, int ContentHeight, double ContentScale)
    {
        /// <summary>The three values 4.1's renderWordArt returns.</summary>
        public void Deconstruct(out SKImage image, out int contentWidth, out int contentHeight)
        {
            image = Image;
            contentWidth = ContentWidth;
            contentHeight = ContentHeight;
        }
    }

    /// <summary>
    /// The size to draw a width x height render at so it stays inside the image caps
    /// (<see cref="Images.MaxDimension"/> a side, <see cref="Images.MaxPixels"/> in all), and the factor the content
    /// shrinks by. A render that fits comes back unchanged with factor 1. Otherwise the pixels are the largest inside
    /// the caps to within a row and column, rounded up so the scaled content is never clipped.
    /// </summary>
    internal static (int Width, int Height, double Factor) FitToCaps(int width, int height)
    {
        static bool Fits(long w, long h) => w <= Images.MaxDimension && h <= Images.MaxDimension && w * h <= Images.MaxPixels;
        if (Fits(width, height)) return (width, height, 1.0);
        double factor = Math.Min(Math.Min((double)Images.MaxDimension / width, (double)Images.MaxDimension / height),
                                 Math.Sqrt((double)Images.MaxPixels / ((double)width * height)));
        while (true)
        {
            int w = Math.Clamp((int)Math.Ceiling(width * factor), 1, Images.MaxDimension);
            int h = Math.Clamp((int)Math.Ceiling(height * factor), 1, Images.MaxDimension);
            if (Fits(w, h)) return (w, h, factor);
            factor *= 0.9999; // ceil can overshoot the pixel cap by a row and a column
        }
    }

    /// <summary>
    /// renderWordArt (editor.js:2120-2240) on a new surface. With no maxima (null or 0, as 4.1's falsy
    /// check) the surface is the content size cw x ch, as Add to canvas renders it; with maxima it is
    /// min(cw, max) x min(ch, max) and the content is scaled by the smaller ratio, as the preview
    /// renders it. Text too big for the image caps (16384 a side, 50 megapixels; 4.1 failed or froze on it)
    /// never throws: it renders at the largest size inside the caps, and <see cref="WordArtRender.ContentScale"/>
    /// says how far it shrank. Returns the image (caller owns it), the content size cw, ch (the image size only
    /// when no maximum or cap clamps it) and that scale.
    /// <para>
    /// <paramref name="deviceScale"/> (ruling T2) is for the preview: the pixels per logical pixel of the display. The maxima
    /// are then device pixels, the content is drawn <paramref name="deviceScale"/> times larger (so it is sharp, and a shadow,
    /// glow or retro offset keeps its place relative to the glyphs), and <see cref="WordArtRender.ContentScale"/> is relative to
    /// the 1x content cw x ch, so it can exceed 1. The default 1 is 4.1's render, byte for byte.
    /// </para>
    /// </summary>
    public static WordArtRender Render(WordArtSettings s, int? maxWidth = null, int? maxHeight = null, double deviceScale = 1)
    {
        if (maxWidth < 0 || maxHeight < 0) throw new ArgumentOutOfRangeException(nameof(maxWidth), "maxima are positive, or null or 0 for none");
        if (!(deviceScale > 0) || double.IsInfinity(deviceScale)) throw new ArgumentOutOfRangeException(nameof(deviceScale), "the device scale is a positive number");
        s = s with { StrokeWidth = EffectiveStrokeWidth(s.StrokeWidth) };
        string text = EffectiveText(s.Text);
        float size = EffectiveSize(s.Size);
        using var font = Fonts.CreateFont(s.Font, Weight, size);

        // editor.js:2135-2154, in JS doubles.
        var lines = text.Split('\n').Select(l => Shape(s.Font, size, l)).ToList();
        double lineHeight = size * 1.15;
        double totalW = lines.Max(l => (double)l.Width);
        double totalH = lines.Count * lineHeight;
        double pad = Pad(s.Style, s.StrokeWidth);
        int cw = CeilingInt(totalW + pad * 2);
        int ch = CeilingInt(totalH + pad * 2);
        int deviceW = deviceScale == 1 ? cw : (int)Math.Min(Math.Ceiling(cw * deviceScale), int.MaxValue);
        int deviceH = deviceScale == 1 ? ch : (int)Math.Min(Math.Ceiling(ch * deviceScale), int.MaxValue);
        int width = maxWidth is > 0 ? Math.Min(deviceW, maxWidth.Value) : deviceW;
        int height = maxHeight is > 0 ? Math.Min(deviceH, maxHeight.Value) : deviceH;
        double sc = Math.Min((double)width / cw, (double)height / ch);
        var (fitWidth, fitHeight, capFactor) = FitToCaps(width, height);
        (width, height, sc) = (fitWidth, fitHeight, sc * capFactor);

        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create word art surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)sc, (float)sc);
        float middle = MiddleOffset(font.Typeface, size);
        double cx = cw / 2.0;
        double startY = pad + lineHeight / 2;
        // Blink's shadow blur ignores the canvas transform (see DrawWithShadow), so a render the caps shrank
        // shrinks the glow's sigma with it; the preview's own shrink keeps 4.1's untransformed blur.
        for (int li = 0; li < lines.Count; li++)
            DrawLine(canvas, font, lines[li], s, cx, startY + li * lineHeight, middle, cw, ch, (float)capFactor);
        return new WordArtRender(surface.Snapshot(), cw, ch, sc);
    }

    /// <summary>Math.Ceiling to an int, saturating instead of wrapping for absurd text.</summary>
    static int CeilingInt(double v) => (int)Math.Min(Math.Ceiling(v), int.MaxValue);

    /// <summary>One line's passes (editor.js:2166-2236).</summary>
    static void DrawLine(SKCanvas canvas, SKFont font, ShapedLine line, WordArtSettings s, double cx, double ty, float middle, int cw, int ch, float blurScale)
    {
        if (line.Runs.Count == 0) return;
        using var builder = new SKTextBlobBuilder();
        foreach (var run in line.Runs)
        {
            if (run.Primary)
            {
                builder.AddHorizontalRun(run.Glyphs, font, run.Xs, 0);
                continue;
            }
            using var fallbackFont = FallbackFont(run.Typeface, font.Size);
            builder.AddHorizontalRun(run.Glyphs, fallbackFont, run.Xs, 0);
        }
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
            DrawWithShadow(s.Fill, 20 * blurScale, glow, p => Text(cx, ty, p));
            DrawWithShadow(s.Fill, 40 * blurScale, glow, p => Text(cx, ty, p));
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
        var (from, to) = GradientEndpoints(s.Angle, cx, ty, cw, ch);
        return SKShader.CreateLinearGradient(from, to, new[] { s.Fill, s.Grad }, null, SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// The gradient line (editor.js:2165-2169): the vector (cos a * cw/2, sin a * ch/2) around the line's
    /// center (cx, ty), so a non-square canvas stretches it; 4.1 scales each axis by its own half-extent.
    /// </summary>
    internal static (SKPoint From, SKPoint To) GradientEndpoints(double angleDeg, double cx, double ty, double cw, double ch)
    {
        double angle = angleDeg * Math.PI / 180;
        double gx = Math.Cos(angle) * cw / 2, gy = Math.Sin(angle) * ch / 2;
        return (new SKPoint((float)(cx - gx), (float)(ty - gy)), new SKPoint((float)(cx + gx), (float)(ty + gy)));
    }

    /// <summary>
    /// The SkFont for a fallback run: the canvas text settings of <see cref="Fonts.CreateFont"/>, plus
    /// synthetic bold when the system face is lighter than <see cref="Fonts.BoldThreshold"/>, because the
    /// request is 900 (Blink applies the same rule to fallback faces).
    /// </summary>
    static SKFont FallbackFont(SKTypeface typeface, float size) => new(typeface, size)
    {
        Embolden = typeface.FontWeight < Fonts.BoldThreshold,
        Edging = SKFontEdging.Antialias,
        Subpixel = true,
        Hinting = SKFontHinting.None,
        LinearMetrics = true,
    };

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
    /// makeLayer default. Text too big for the image caps (see <see cref="Render"/>) still makes one layer: its
    /// bitmap is the largest inside the caps and its Scale makes up the difference, so it lands at the size
    /// the full render would have had.
    /// </summary>
    public static Layer CreateLayer(WordArtSettings s, TemplateMode mode, int id)
    {
        var (rendered, contentWidth, contentHeight, contentScale) = Render(s);
        using (rendered)
        {
            var front = Template.Slot(mode, BodyPart.Body, Face.Front);
            // The fit is for the requested size cw x ch (4.1's img.width x img.height). A render the caps shrank
            // (contentScale below 1) has its layer scale raised by the same factor, so it lands at the requested size.
            double scale = Math.Min((double)front.W / contentWidth, (double)front.H / contentHeight) * 0.85 / contentScale;
            return new Layer
            {
                Id = id,
                Name = LayerName(s.Text, s.Font),
                Image = Images.ToUnpremulLayerImage(rendered),
                X = front.X + (front.W - rendered.Width * scale) / 2,
                Y = front.Y + (front.H - rendered.Height * scale) / 2,
                Scale = scale,
            };
        }
    }
}
