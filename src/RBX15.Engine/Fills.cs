using System.Collections.Immutable;
using System.Globalization;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// A region fill (lib/fills.js descriptor). Legacy v4.0 string fills are Fill.Solid.
/// <para>
/// <see cref="Stops"/> (v5 only, ledger <c>fill-gradient-stops</c>) are optional middle colors for the
/// "linear" and "radial" types: C1 sits at 0, the stops evenly between, C2 at 1. At most
/// <see cref="MaxStops"/>; a longer list throws <see cref="ArgumentException"/> wherever it is set. A fill
/// without stops is value-identical to a 4.1 fill: same <see cref="Key"/>, same pixels. Equality is
/// structural, stops compared in order (a record's own equality would compare the list by reference).
/// </para>
/// </summary>
public sealed record Fill(string Type, string? C1 = null, string? C2 = null, double? Angle = null, double? Scale = null,
                          ImmutableArray<string> Stops = default)
{
    /// <summary>Most middle colors a fill carries: five colors in all.</summary>
    public const int MaxStops = 3;

    readonly ImmutableArray<string> _stops = CheckStops(Stops);

    /// <summary>The middle colors, never default; empty when there are none. Setting more than <see cref="MaxStops"/> throws.</summary>
    public ImmutableArray<string> Stops
    {
        get => _stops.IsDefault ? ImmutableArray<string>.Empty : _stops;
        init => _stops = CheckStops(value);
    }

    static ImmutableArray<string> CheckStops(ImmutableArray<string> stops)
    {
        if (stops.IsDefault) return ImmutableArray<string>.Empty;
        if (stops.Length > MaxStops) throw new ArgumentException($"A fill holds at most {MaxStops} color stops, not {stops.Length}.", nameof(stops));
        foreach (var s in stops) ArgumentNullException.ThrowIfNull(s, nameof(stops));
        return stops;
    }

    public static Fill Solid(string color) => new("solid", color);

    /// <summary>
    /// fillKey: [type, c1||'', c2||'', angle||0, scale||0].join('|'). A fill with stops appends
    /// <c>|</c> and its stops joined by commas, so it never collides with a 4.1 key; without stops the key is 4.1's.
    /// </summary>
    public string Key
    {
        get
        {
            string key = string.Join('|', Type, C1 ?? "", C2 ?? "", JsNumber(Angle ?? 0), JsNumber(Scale ?? 0));
            return Stops.IsEmpty ? key : key + "|" + string.Join(',', Stops);
        }
    }

    public bool Equals(Fill? other) =>
        other is not null &&
        Type == other.Type && C1 == other.C1 && C2 == other.C2 && Angle == other.Angle && Scale == other.Scale &&
        Stops.AsSpan().SequenceEqual(other.Stops.AsSpan());

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Type); h.Add(C1); h.Add(C2); h.Add(Angle); h.Add(Scale);
        foreach (var s in Stops) h.Add(s);
        return h.ToHashCode();
    }

    /// <summary>JS Number to string for the values fills carry (integers and plain decimals).</summary>
    public static string JsNumber(double v) =>
        double.IsNaN(v) ? "0" :
        v == Math.Floor(v) && Math.Abs(v) < 1e15 ? ((long)v).ToString(CultureInfo.InvariantCulture)
                                                 : v.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>Port of lib/fills.js paintRegionFill and editor.js paintRegions (proved pixel-exact by the 2026-10-11 spike).</summary>
public static class Fills
{
    public static double EffectiveScale(Fill f) => Math.Max(2, f.Scale is { } s && s != 0 && !double.IsNaN(s) ? s : 16);

    /// <summary>mulberry32 in uint arithmetic (JS Math.imul, >>> and ^ reduce to the low 32 bits).</summary>
    public static Func<double> Seeded(uint seed)
    {
        uint s = seed;
        return () =>
        {
            unchecked
            {
                s += 0x6D2B79F5;
                uint t = (s ^ (s >> 15)) * (1 | s);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        };
    }

    /// <summary>FNV-1a over UTF-16 code units, as JS charCodeAt sees them.</summary>
    public static uint SeedFrom(string str)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in str) { h ^= ch; h *= 16777619; }
            return h;
        }
    }

    static SKPaint FillPaint(SKColor c) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Fill };

    // Canvas arc(cx, cy, r, 0, 2PI) appended to the current subpath: a line to the arc start, then the
    // circle as two 180-degree arcs. Chromium-family Skia canvases emit a full arc this way.
    static void FullArc(SKPathBuilder b, float cx, float cy, float r)
    {
        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
        b.LineTo(cx + r, cy);
        b.ArcTo(oval, 0, 180, false);
        b.ArcTo(oval, 180, 180, false);
    }

    /// <summary>
    /// The color ramp of a linear or radial gradient: C1 at 0, each stop at k/(n+1), C2 at 1. With no stops
    /// this is exactly 4.1's two-color ramp (offsets 0 and 1), so those pixels do not move.
    /// </summary>
    internal static (SKColor[] Colors, float[] Positions) GradientRamp(Fill f, string c1, string c2)
    {
        var stops = f.Stops;
        if (stops.IsEmpty) return (new[] { CssColor.ToSk(c1), CssColor.ToSk(c2) }, new float[] { 0, 1 });
        var colors = new SKColor[stops.Length + 2];
        var positions = new float[stops.Length + 2];
        colors[0] = CssColor.ToSk(c1);
        for (int k = 0; k < stops.Length; k++)
        {
            colors[k + 1] = CssColor.ToSk(stops[k]);
            positions[k + 1] = (float)(k + 1) / (stops.Length + 1);
        }
        colors[^1] = CssColor.ToSk(c2);
        positions[^1] = 1;
        return (colors, positions);
    }

    public static void PaintRegionFill(SKCanvas ctx, SKRect region, Fill f, SKRect? extent = null)
    {
        var ext = extent ?? region;
        string c1 = string.IsNullOrEmpty(f.C1) ? "#000000" : f.C1;
        string c2 = string.IsNullOrEmpty(f.C2) ? "#ffffff" : f.C2;
        double scaleD = EffectiveScale(f);
        float scale = (float)scaleD;
        double angle = (f.Angle is { } a && !double.IsNaN(a) ? a : 0) * Math.PI / 180;
        float x = region.Left, y = region.Top, w = region.Width, h = region.Height;

        ctx.Save();
        ctx.ClipRect(region, SKClipOperation.Intersect, true);
        switch (f.Type)
        {
            case "linear":
            {
                var (colors, positions) = GradientRamp(f, c1, c2);
                float cx = ext.Left + ext.Width / 2, cy = ext.Top + ext.Height / 2;
                float dx = (float)(Math.Cos(angle) * ext.Width / 2), dy = (float)(Math.Sin(angle) * ext.Height / 2);
                using var sh = SKShader.CreateLinearGradient(new SKPoint(cx - dx, cy - dy), new SKPoint(cx + dx, cy + dy),
                    colors, positions, SKShaderTileMode.Clamp);
                using var p = new SKPaint { Shader = sh, IsAntialias = true };
                ctx.DrawRect(region, p);
                break;
            }
            case "radial":
            {
                var (colors, positions) = GradientRamp(f, c1, c2);
                float cx = ext.Left + ext.Width / 2, cy = ext.Top + ext.Height / 2;
                using var sh = SKShader.CreateRadialGradient(new SKPoint(cx, cy), Math.Max(ext.Width, ext.Height) / 2,
                    colors, positions, SKShaderTileMode.Clamp);
                using var p = new SKPaint { Shader = sh, IsAntialias = true };
                ctx.DrawRect(region, p);
                break;
            }
            case "stripes":
            {
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                using var p2 = FillPaint(CssColor.ToSk(c2));
                ctx.RotateRadians((float)angle);
                double period = scaleD * 2;
                double big = Math.Ceiling(1000 / period) * period;
                for (double i = -big; i < big; i += period)
                    ctx.DrawRect(SKRect.Create((float)i, (float)-big, scale, (float)(big * 2)), p2);
                break;
            }
            case "checker":
            {
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                using var p2 = FillPaint(CssColor.ToSk(c2));
                int gx0 = (int)Math.Floor(x / scaleD), gx1 = (int)Math.Ceiling((x + w) / scaleD);
                int gy0 = (int)Math.Floor(y / scaleD), gy1 = (int)Math.Ceiling((y + h) / scaleD);
                for (int gy = gy0; gy < gy1; gy++)
                    for (int gx = gx0; gx < gx1; gx++)
                        if (((gx + gy) & 1) == 0) ctx.DrawRect(SKRect.Create(gx * scale, gy * scale, scale, scale), p2);
                break;
            }
            case "dots":
            {
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                using var p2 = FillPaint(CssColor.ToSk(c2));
                float r = scale * 0.3f;
                int gx0 = (int)Math.Floor(x / scaleD), gx1 = (int)Math.Ceiling((x + w) / scaleD);
                int gy0 = (int)Math.Floor(y / scaleD), gy1 = (int)Math.Ceiling((y + h) / scaleD);
                for (int gy = gy0; gy <= gy1; gy++)
                    for (int gx = gx0; gx <= gx1; gx++)
                    {
                        float cx = gx * scale + scale / 2, cy = gy * scale + scale / 2;
                        using var pb = new SKPathBuilder();
                        pb.MoveTo(cx + r, cy);
                        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
                        pb.ArcTo(oval, 0, 180, false);
                        pb.ArcTo(oval, 180, 180, false);
                        using var path = pb.Detach();
                        ctx.DrawPath(path, p2);
                    }
                break;
            }
            case "camo":
            {
                string[] tones = { c1, c2, CssColor.Mix(c1, c2, 0.5), CssColor.Mix(c1, "#000000", 0.35) };
                string current = tones[0];
                using (var b = FillPaint(CssColor.ToSk(current))) ctx.DrawRect(region, b);
                double cell = scaleD * 2;
                int gx0 = (int)Math.Floor(x / cell) - 1, gx1 = (int)Math.Ceiling((x + w) / cell) + 1;
                int gy0 = (int)Math.Floor(y / cell) - 1, gy1 = (int)Math.Ceiling((y + h) / cell) + 1;
                for (int gy = gy0; gy < gy1; gy++)
                    for (int gx = gx0; gx < gx1; gx++)
                    {
                        var rand = Seeded(SeedFrom(c1 + c2 + Fill.JsNumber(scaleD) + "_" + gx + "_" + gy));
                        for (int bi = 0; bi < 2; bi++)
                        {
                            // JS: tones[1 + ((gx+gy+b) % 3)]. JS % keeps the sign; a negative index is
                            // undefined, and assigning undefined to fillStyle keeps the previous color.
                            // Carried verbatim; it cannot change pixels in the UI's 4..48 scale range.
                            int idx = 1 + ((gx + gy + bi) % 3);
                            if (idx >= 0 && idx < tones.Length) current = tones[idx];
                            double bx = (gx + rand()) * cell, by = (gy + rand()) * cell;
                            double br = scaleD * (0.6 + rand() * 1.1);
                            using var pb = new SKPathBuilder();
                            for (int k = 0; k < 4; k++)
                            {
                                double ox = (rand() - 0.5) * br, oy = (rand() - 0.5) * br;
                                pb.MoveTo((float)(bx + ox + br), (float)(by + oy));
                                FullArc(pb, (float)(bx + ox), (float)(by + oy), (float)(br * (0.6 + rand() * 0.4)));
                            }
                            using var path = pb.Detach();
                            using var p = FillPaint(CssColor.ToSk(current));
                            ctx.DrawPath(path, p);
                        }
                    }
                break;
            }
            default:
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                break;
        }
        ctx.Restore();
    }

    static SKRect Union(IEnumerable<SKRect> rects)
    {
        float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
        foreach (var r in rects)
        {
            x0 = Math.Min(x0, r.Left); y0 = Math.Min(y0, r.Top);
            x1 = Math.Max(x1, r.Right); y1 = Math.Max(y1, r.Bottom);
        }
        return new SKRect(x0, y0, x1, y1);
    }

    /// <summary>connectedRegionGroups (editor.js): rects whose boxes, inflated by tolerance, touch.</summary>
    public static List<List<SKRect>> ConnectedRegionGroups(IReadOnlyList<SKRect> rects, float tolerance = 4)
    {
        bool Touch(SKRect a, SKRect b) =>
            a.Left - tolerance < b.Right && b.Left - tolerance < a.Right && a.Top - tolerance < b.Bottom && b.Top - tolerance < a.Bottom;
        var groups = new List<List<SKRect>>();
        var seen = new HashSet<int>();
        for (int i = 0; i < rects.Count; i++)
        {
            if (!seen.Add(i)) continue;
            var comp = new List<SKRect>();
            var stack = new Stack<int>();
            stack.Push(i);
            while (stack.Count > 0)
            {
                int j = stack.Pop();
                comp.Add(rects[j]);
                for (int k = 0; k < rects.Count; k++)
                    if (!seen.Contains(k) && Touch(rects[j], rects[k])) { seen.Add(k); stack.Push(k); }
            }
            groups.Add(comp);
        }
        return groups;
    }

    /// <summary>paintRegions (editor.js): group same-key fills, split into connected groups, gradients span each group.</summary>
    public static void PaintRegions(SKCanvas canvas, TemplateMode mode, IReadOnlyList<(string Region, Fill Fill)> fills)
    {
        var byKey = new List<(string Key, Fill Fill, List<SKRect> Rects)>();
        foreach (var (name, fill) in fills)
        {
            if (!Template.TryFind(mode, name, out var region)) continue;
            int at = byKey.FindIndex(e => e.Key == fill.Key);
            if (at < 0) byKey.Add((fill.Key, fill, new List<SKRect> { region.Rect }));
            else byKey[at].Rects.Add(region.Rect);
        }
        foreach (var (_, fill, rects) in byKey)
            foreach (var group in ConnectedRegionGroups(rects))
            {
                var extent = Union(group);
                foreach (var r in group) PaintRegionFill(canvas, r, fill, extent);
            }
    }
}
