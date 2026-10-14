using SkiaSharp;

namespace RBX15.Engine;

/// <summary>The 16 clothing details of 4.1's Details tab, in DETAIL_GENERATORS order (editor.js:1867-2075).</summary>
public enum DetailType
{
    CollarV, CollarCrew, CollarScoop, CollarHenley,
    CuffLeft, CuffRight, CuffsBoth, Hem,
    SeamCenter, SeamSides, Pocket, Placket,
    ShoulderStripe, ArmBands, Yoke, FullDetail,
}

/// <summary>
/// The Details tab settings read at click time (editor.js:1840-1842): color (#detailColor),
/// width (#detailWidth, an integer range 1..8, editor.html:181) and Stitch (#detailDash).
/// </summary>
public sealed record DetailStyle(SKColor Color, float Width, bool Stitch);

/// <summary>
/// The port of createDetailLayer and DETAIL_GENERATORS (editor.js:1834-2103). Each generator draws
/// on a fresh 585x559 canvas in template coordinates, the canvas is autocropped with pad 10, and
/// the crop becomes a scale-1 layer at the crop origin. Every <c>stroke()</c> and <c>fill()</c> call
/// in 4.1 is one draw here, in the same order and with the same path, so antialiased overlaps
/// composite as they do in Chromium. Coordinates are computed in doubles, as JS does, and handed
/// to Skia as floats, as Blink does.
/// <para>
/// Regions resolve through <see cref="Template.Slot"/>, so every detail also draws in Pants mode on
/// the equivalent waist and leg faces (approved divergence details-pants-mode-behavior: 4.1 throws).
/// </para>
/// </summary>
public static class Details
{
    static readonly (string Id, DetailType Type, string Name)[] Table =
    {
        // ids: editor.html:187-222 data-detail; names: editor.js:2082-2099
        ("collar-v", DetailType.CollarV, "V-Neck Collar"),
        ("collar-crew", DetailType.CollarCrew, "Crew Neck"),
        ("collar-scoop", DetailType.CollarScoop, "Scoop Neck"),
        ("collar-henley", DetailType.CollarHenley, "Henley Collar"),
        ("cuff-left", DetailType.CuffLeft, "Left Cuff"),
        ("cuff-right", DetailType.CuffRight, "Right Cuff"),
        ("cuffs-both", DetailType.CuffsBoth, "Both Cuffs"),
        ("hem", DetailType.Hem, "Hem Line"),
        ("seam-center", DetailType.SeamCenter, "Center Seam"),
        ("seam-sides", DetailType.SeamSides, "Side Seams"),
        ("pocket", DetailType.Pocket, "Chest Pocket"),
        ("placket", DetailType.Placket, "Button Placket"),
        ("shoulder-stripe", DetailType.ShoulderStripe, "Shoulder Stripe"),
        ("arm-bands", DetailType.ArmBands, "Arm Bands"),
        ("yoke", DetailType.Yoke, "Back Yoke"),
        ("full-detail", DetailType.FullDetail, "Full Detail Set"),
    };

    /// <summary>The 4.1 data-detail id (editor.html:187-222) to its type. Unknown ids throw.</summary>
    public static DetailType FromId(string id)
    {
        foreach (var row in Table)
            if (row.Id == id) return row.Type;
        throw new ArgumentException($"unknown detail id '{id}'", nameof(id));
    }

    /// <summary>The 4.1 data-detail id of a type.</summary>
    public static string Id(DetailType type) => Row(type).Id;

    /// <summary>The layer name 4.1 gives the detail (editor.js:2082-2099).</summary>
    public static string LayerName(DetailType type) => Row(type).Name;

    static (string Id, DetailType Type, string Name) Row(DetailType type)
    {
        foreach (var row in Table)
            if (row.Type == type) return row;
        throw new ArgumentOutOfRangeException(nameof(type));
    }

    /// <summary>
    /// The 585x559 canvas createDetailLayer draws (editor.js:1836-1851), before autocrop. Caller owns
    /// the image. Width must be finite and positive: 4.1's range input only yields 1..8, and Canvas2D
    /// would silently ignore anything else, so v5 refuses it instead of guessing.
    /// </summary>
    public static SKImage Render(DetailType type, TemplateMode mode, DetailStyle style)
    {
        if (!float.IsFinite(style.Width) || style.Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(style), style.Width, "detail width must be finite and positive");
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create detail surface");
        surface.Canvas.Clear(SKColors.Transparent);
        using (var pen = new Pen(surface.Canvas, mode, style))
            Draw(type, pen);
        return surface.Snapshot();
    }

    /// <summary>
    /// createDetailLayer (editor.js:1834-1865): render, autocrop (pad 10, editor.js:144-170), and a
    /// makeLayer(img, name, crop.x, crop.y, 1) layer (editor.js:1858) holding the crop un-premultiplied,
    /// the form 4.1's toDataURL + Image decode leaves it in. Null when nothing has alpha (no layer;
    /// the caller records no undo step, ledger details-buttons). Selection and the tool switch
    /// (editor.js:1860-1861) are the App's.
    /// </summary>
    public static Layer? Create(DetailType type, TemplateMode mode, DetailStyle style, int id)
    {
        using var drawn = Render(type, mode, style);
        if (Tools.Autocrop(drawn) is not { } crop) return null;
        using var premul = crop.Image;
        var info = new SKImageInfo(premul.Width, premul.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!premul.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read cropped detail");
        return new Layer { Id = id, Name = LayerName(type), Image = SKImage.FromPixelCopy(bmp.PeekPixels()), X = crop.X, Y = crop.Y, Scale = 1 };
    }

    static void Draw(DetailType type, Pen p)
    {
        switch (type)
        {
            case DetailType.CollarV: CollarV(p); break;
            case DetailType.CollarCrew: CollarCrew(p); break;
            case DetailType.CollarScoop: CollarScoop(p); break;
            case DetailType.CollarHenley: CollarHenley(p); break;
            case DetailType.CuffLeft: Cuffs(p, BodyPart.LeftLimb); break;
            case DetailType.CuffRight: Cuffs(p, BodyPart.RightLimb); break;
            case DetailType.CuffsBoth: CuffsBoth(p); break;
            case DetailType.Hem: Hem(p); break;
            case DetailType.SeamCenter: SeamCenter(p); break;
            case DetailType.SeamSides: SeamSides(p); break;
            case DetailType.Pocket: Pocket(p); break;
            case DetailType.Placket: Placket(p); break;
            case DetailType.ShoulderStripe: ShoulderStripe(p); break;
            case DetailType.ArmBands: ArmBands(p); break;
            case DetailType.Yoke: Yoke(p); break;
            case DetailType.FullDetail: FullDetail(p); break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    // ── Generators (editor.js:1867-2075) ─────────────────────────

    // editor.js:1868-1877
    static void CollarV(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y;
        p.Stroke(b => { b.MoveTo(P(cx - 30, top)); b.LineTo(P(cx, top + 28)); b.LineTo(P(cx + 30, top)); });
    }

    // editor.js:1878-1894: front U of two quads, then a separate back-collar stroke.
    static void CollarCrew(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y;
        p.Stroke(b =>
        {
            b.MoveTo(P(cx - 32, top));
            b.QuadTo(P(cx - 32, top + 16), P(cx, top + 18));
            b.QuadTo(P(cx + 32, top + 16), P(cx + 32, top));
        });
        var bk = p.Region(BodyPart.Body, Face.Back);
        double bcx = bk.X + bk.W / 2.0;
        p.Stroke(b => { b.MoveTo(P(bcx - 28, bk.Y)); b.QuadTo(P(bcx, bk.Y + 10), P(bcx + 28, bk.Y)); });
    }

    // editor.js:1895-1903
    static void CollarScoop(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y;
        p.Stroke(b => { b.MoveTo(P(cx - 38, top)); b.QuadTo(P(cx, top + 32), P(cx + 38, top)); });
    }

    // editor.js:1904-1922: crew, a 30 px placket line, three buttons of radius lineWidth, filled.
    static void CollarHenley(Pen p)
    {
        CollarCrew(p);
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y + 18;
        p.Stroke(b => { b.MoveTo(P(cx, top)); b.LineTo(P(cx, top + 30)); });
        double r = p.LineWidth; // editor.js:1916, the full width
        for (int i = 0; i < 3; i++) p.FillCircle(cx, top + 6 + i * 10, r);
    }

    // editor.js:1923-1943: one line 8 px above the bottom of each of the limb's four side faces,
    // in 4.1's order front, left, back, right.
    static void Cuffs(Pen p, BodyPart limb)
    {
        foreach (var face in new[] { Face.Front, Face.Left, Face.Back, Face.Right })
        {
            var reg = p.Region(limb, face);
            HLine(p, reg, reg.Y + reg.H - 8);
        }
    }

    // editor.js:1944-1947
    static void CuffsBoth(Pen p)
    {
        Cuffs(p, BodyPart.LeftLimb);
        Cuffs(p, BodyPart.RightLimb);
    }

    // editor.js:1948-1965: 4 px above the bottom of the right, front and left faces, then the back.
    static void Hem(Pen p)
    {
        foreach (var face in new[] { Face.Right, Face.Front, Face.Left, Face.Back })
        {
            var r = p.Region(BodyPart.Body, face);
            HLine(p, r, r.Y + r.H - 4);
        }
    }

    // editor.js:1966-1973
    static void SeamCenter(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        VLine(p, f.X + f.W / 2.0, f.Y, f.Y + f.H);
    }

    // editor.js:1974-1995: both edges of the front, then both edges of the back.
    static void SeamSides(Pen p)
    {
        foreach (var face in new[] { Face.Front, Face.Back })
        {
            var r = p.Region(BodyPart.Body, face);
            VLine(p, r.X, r.Y, r.Y + r.H);
            VLine(p, r.X + r.W, r.Y, r.Y + r.H);
        }
    }

    // editor.js:1996-2008: an open-top 22x24 box at 62 % across, 25 % down the front.
    static void Pocket(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double px = f.X + f.W * 0.62, py = f.Y + f.H * 0.25;
        const double pw = 22, ph = 24;
        p.Stroke(b => { b.MoveTo(P(px, py)); b.LineTo(P(px, py + ph)); b.LineTo(P(px + pw, py + ph)); b.LineTo(P(px + pw, py)); });
    }

    // editor.js:2009-2030: two lines 5 px apart, then 7 buttons of radius max(1.5, 0.8 lineWidth).
    static void Placket(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0;
        VLine(p, cx, f.Y + 4, f.Y + f.H - 4);
        VLine(p, cx + 5, f.Y + 4, f.Y + f.H - 4);
        double r = Math.Max(1.5, p.LineWidth * 0.8);
        double spacing = (f.H - 16) / 6.0;
        for (int i = 0; i < 7; i++) p.FillCircle(cx + 2.5, f.Y + 8 + i * spacing, r);
    }

    // editor.js:2031-2048: through the middle of the body's up face, then the right and left limb up faces.
    static void ShoulderStripe(Pen p)
    {
        var tu = p.Region(BodyPart.Body, Face.Up);
        HLine(p, tu, tu.Y + tu.H / 2.0);
        foreach (var limb in new[] { BodyPart.RightLimb, BodyPart.LeftLimb })
        {
            var r = p.Region(limb, Face.Up);
            HLine(p, r, r.Y + r.H / 2.0);
        }
    }

    // editor.js:2049-2059: 35 % down each limb side face, right limb front/back/left/right then left.
    static void ArmBands(Pen p)
    {
        foreach (var limb in new[] { BodyPart.RightLimb, BodyPart.LeftLimb })
            foreach (var face in new[] { Face.Front, Face.Back, Face.Left, Face.Right })
            {
                var r = p.Region(limb, face);
                HLine(p, r, r.Y + r.H * 0.35);
            }
    }

    // editor.js:2060-2068
    static void Yoke(Pen p)
    {
        var b = p.Region(BodyPart.Body, Face.Back);
        HLine(p, b, b.Y + b.H * 0.22);
    }

    // editor.js:2069-2074
    static void FullDetail(Pen p)
    {
        CollarCrew(p);
        Hem(p);
        CuffsBoth(p);
        SeamSides(p);
    }

    // ── Shared shapes ────────────────────────────────────────────

    /// <summary>beginPath; moveTo(r.x, y); lineTo(r.x + r.w, y); stroke(): a line across the face.</summary>
    static void HLine(Pen p, Region r, double y) =>
        p.Stroke(b => { b.MoveTo(P(r.X, y)); b.LineTo(P(r.X + r.W, y)); });

    /// <summary>beginPath; moveTo(x, y0); lineTo(x, y1); stroke().</summary>
    static void VLine(Pen p, double x, double y0, double y1) =>
        p.Stroke(b => { b.MoveTo(P(x, y0)); b.LineTo(P(x, y1)); });

    static SKPoint P(double x, double y) => new((float)x, (float)y);

    /// <summary>
    /// The canvas state createDetailLayer sets (editor.js:1844-1849) and the two draw calls the
    /// generators use. stroke(): lineWidth, round cap and join, and when Stitch is on the dash
    /// [2w, 2w] at lineDashOffset 0, which Blink hands Skia as an SKPathEffect dash; every stroke()
    /// is its own draw, so each starts the pattern afresh. fill(): the same color, never dashed.
    /// StrokeMiter stays at the canvas default 10; round joins never read it.
    /// </summary>
    sealed class Pen : IDisposable
    {
        readonly SKCanvas _canvas;
        readonly TemplateMode _mode;
        readonly SKPaint _stroke;
        readonly SKPaint _fill;
        readonly SKPathEffect? _dash;

        public Pen(SKCanvas canvas, TemplateMode mode, DetailStyle style)
        {
            _canvas = canvas;
            _mode = mode;
            LineWidth = style.Width;
            if (style.Stitch) _dash = SKPathEffect.CreateDash(new[] { style.Width * 2, style.Width * 2 }, 0);
            _stroke = new SKPaint
            {
                Color = style.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = style.Width,
                StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, StrokeMiter = 10, PathEffect = _dash,
            };
            _fill = new SKPaint { Color = style.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
        }

        /// <summary>c.lineWidth as the generators read it back (editor.js:1916, :2023).</summary>
        public double LineWidth { get; }

        public Region Region(BodyPart part, Face face) => Template.Slot(_mode, part, face);

        public void Stroke(Action<SKPathBuilder> build)
        {
            using var pb = new SKPathBuilder();
            build(pb);
            using var path = pb.Detach();
            _canvas.DrawPath(path, _stroke);
        }

        /// <summary>
        /// beginPath; arc(cx, cy, r, 0, 2pi); fill(). Blink's Path::AddEllipse splits the full turn into
        /// two 180-degree arcTo calls from angle 0, as Tools builds the ellipse.
        /// </summary>
        public void FillCircle(double cx, double cy, double r)
        {
            float x = (float)cx, y = (float)cy, rad = (float)r;
            var oval = new SKRect(x - rad, y - rad, x + rad, y + rad);
            using var pb = new SKPathBuilder();
            pb.MoveTo(x + rad, y);
            pb.ArcTo(oval, 0, 180, false);
            pb.ArcTo(oval, 180, 180, false);
            using var path = pb.Detach();
            _canvas.DrawPath(path, _fill);
        }

        public void Dispose()
        {
            _stroke.Dispose();
            _fill.Dispose();
            _dash?.Dispose();
        }
    }
}
