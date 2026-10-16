using System.Globalization;
using System.Numerics;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>The four corner handles in 4.1's order (editor.js:1083: TL, TR, BR, BL).</summary>
public enum ResizeCorner { TopLeft, TopRight, BottomRight, BottomLeft }

/// <summary>What a Select-tool press hit (editor.js:1064-1117).</summary>
public enum SelectHitKind { None, Rotate, Resize, Move }

/// <summary>A press result: the kind, the layer it acts on (null for None) and, for Resize, the corner.</summary>
public readonly record struct SelectHit(SelectHitKind Kind, int? LayerId, ResizeCorner Corner);

/// <summary>
/// The Select tool's geometry (editor.js:1018-1192) as pure functions over doubles in template px, operation for operation
/// where 4.1 is kept. "Screen px" constants are divided by the zoom, as 4.1 does (editor.js:1034, :1069, :1088), so the
/// handles keep their on-screen size at every zoom. The overlay that draws them is <c>Renderer.DrawSelectionOverlay</c>.
/// <para>
/// Fixes, each a ledger divergence: hidden layers have no handles (M3b ruling R2, select-hidden-layer-still-handled,
/// select-rotate-handle-hit); rotation is normalized to -180..180 before the snap (R5, select-rotate-drag); one scale
/// clamp of 1 to 500% (R6, props-scale-clamp-inconsistency); the wheel steps symmetrically about the center (R6,
/// wheel-scale-layer); and the rotate handle is hit where it is drawn (ruling T1-1 below <see cref="RotateHandle"/>).
/// </para>
/// </summary>
public static class SelectionGeometry
{
    /// <summary>The rotate knob's distance beyond the top edge, in screen px (editor.js:387 draws it, :1034 hit-tests it).</summary>
    public const double RotateHandleDistance = 30;

    /// <summary>The rotate knob's hit radius in screen px, strict &lt; (editor.js:1069).</summary>
    public const double RotateHitRadius = 14;

    /// <summary>A corner handle's hit radius in screen px, strict &lt; (editor.js:1088).</summary>
    public const double ResizeHitRadius = 10;

    /// <summary>The one scale clamp, 1 to 500% (spec §5; ledger props-scale-clamp-inconsistency).</summary>
    public const double MinScale = 0.01, MaxScale = 5;

    /// <summary>The wheel's step: x1.05 up, /1.05 down (spec §5 "symmetric steps"; 4.1's up step, editor.js:1310).</summary>
    public const double WheelStep = 1.05;

    /// <summary>The rotate snap tolerance in degrees, strict &lt; (editor.js:1178).</summary>
    public const double SnapTolerance = 3;

    /// <summary>The snap angles in 4.1's order; the first within <see cref="SnapTolerance"/> wins (editor.js:1176-1179).</summary>
    public static readonly IReadOnlyList<double> SnapAngles = new double[] { -180, -135, -90, -45, 0, 45, 90, 135, 180 };

    /// <summary>getLayerCenter (editor.js:1023-1027): x + img.width*scale/2, y + img.height*scale/2.</summary>
    public static (double X, double Y) Center(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        double w = layer.Image.Width * layer.Scale, h = layer.Image.Height * layer.Scale;
        return (layer.X + w / 2, layer.Y + h / 2);
    }

    /// <summary>
    /// The rotate knob's center in template px: <see cref="RotateHandleDistance"/> screen px above the top edge in the
    /// layer's rotated frame, exactly where <c>Renderer.DrawSelectionOverlay</c> draws it (editor.js:387-401:
    /// <c>rotate(r)</c> maps the local point (0, -d) to (d sin r, -d cos r)).
    /// <para>
    /// Ruling T1-1 (fix): 4.1's getRotHandlePos (editor.js:1029-1039) returns <c>c.x - sin(r)*d</c>, the knob mirrored
    /// across the layer's vertical axis. The two agree only at 0 and 180 degrees, so in 4.1 a rotated layer's visible knob
    /// does not rotate (the press falls through to a corner or a move) and an empty spot on the other side does. v5
    /// hit-tests the drawn knob: <c>c.x + sin(r)*d</c>. Pinned by
    /// <c>SelectionGeometryTests.The_rotate_handle_turns_clockwise_with_the_layer</c> and
    /// <c>RendererOverlayTests.The_rotate_handle_is_hit_where_the_overlay_draws_it</c>; Task 5 records the divergence.
    /// </para>
    /// </summary>
    public static (double X, double Y) RotateHandle(Layer layer, double zoom)
    {
        ArgumentNullException.ThrowIfNull(layer);
        double h = layer.Image.Height * layer.Scale;
        var c = Center(layer);
        double rad = layer.Rotation * Math.PI / 180;
        double dist = h / 2 + RotateHandleDistance / zoom;
        return (c.X + Math.Sin(rad) * dist, c.Y - Math.Cos(rad) * dist);
    }

    /// <summary>A corner of the rotated box in template px (editor.js:1083-1088): center + R(rotation) * (±w/2, ±h/2).</summary>
    public static (double X, double Y) Corner(Layer layer, ResizeCorner corner)
    {
        ArgumentNullException.ThrowIfNull(layer);
        double w = layer.Image.Width * layer.Scale, h = layer.Image.Height * layer.Scale;
        var c = Center(layer);
        double rad = layer.Rotation * Math.PI / 180;
        var (lx, ly) = corner switch
        {
            ResizeCorner.TopLeft => (-w / 2, -h / 2),
            ResizeCorner.TopRight => (w / 2, -h / 2),
            ResizeCorner.BottomRight => (w / 2, h / 2),
            ResizeCorner.BottomLeft => (-w / 2, h / 2),
            _ => throw new ArgumentOutOfRangeException(nameof(corner)),
        };
        return (c.X + lx * Math.Cos(rad) - ly * Math.Sin(rad), c.Y + lx * Math.Sin(rad) + ly * Math.Cos(rad));
    }

    /// <summary>
    /// A Select-tool press at template point (x, y), in 4.1's order (editor.js:1064-1117):
    /// <list type="number">
    /// <item>the selected layer's rotate knob, distance &lt; 14/zoom (Rotate);</item>
    /// <item>its corners TL, TR, BR, BL, distance &lt; 10/zoom (Resize);</item>
    /// <item><see cref="LayerOps.HitTest"/>: the topmost visible layer whose rotated box holds the point (Move);</item>
    /// <item>else None, which deselects.</item>
    /// </list>
    /// A hidden selected layer has no handles (ruling R2), so a press on its knob or corners falls through to the pick.
    /// </summary>
    public static SelectHit HitTest(Document doc, int? selectedId, double x, double y, double zoom)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (selectedId is int id && doc.Layers.Find(l => l.Id == id) is { Visible: true } sel)
        {
            if (Distance(RotateHandle(sel, zoom), x, y) < RotateHitRadius / zoom)
                return new SelectHit(SelectHitKind.Rotate, sel.Id, default);
            foreach (var corner in new[] { ResizeCorner.TopLeft, ResizeCorner.TopRight, ResizeCorner.BottomRight, ResizeCorner.BottomLeft })
                if (Distance(Corner(sel, corner), x, y) < ResizeHitRadius / zoom)
                    return new SelectHit(SelectHitKind.Resize, sel.Id, corner);
        }
        return LayerOps.HitTest(doc, x, y) is int hit
            ? new SelectHit(SelectHitKind.Move, hit, default)
            : new SelectHit(SelectHitKind.None, null, default);
    }

    /// <summary>ptDist (editor.js:1041).</summary>
    static double Distance((double X, double Y) p, double x, double y) => Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));

    /// <summary>The pointer's angle about <paramref name="center"/>: atan2(dx, -dy) in degrees, 0 up, clockwise positive (editor.js:1073, :1174).</summary>
    public static double PointerAngle((double X, double Y) center, double x, double y) =>
        Math.Atan2(x - center.X, -(y - center.Y)) * 180 / Math.PI;

    /// <summary>rotStartAngle at the press (editor.js:1073): the pointer angle minus the layer's rotation.</summary>
    public static double RotateStartOffset(Layer layer, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return PointerAngle(Center(layer), x, y) - layer.Rotation;
    }

    /// <summary>
    /// The rotation for a rotate-drag move (ruling R5): <c>raw = PointerAngle - startOffset</c> (editor.js:1174-1175),
    /// normalized into [-180, 180], then 4.1's snap (editor.js:1176-1179). The result is in [-180, 180].
    /// </summary>
    public static double RotateAngle((double X, double Y) center, double startOffset, double x, double y) =>
        SnapRotation(NormalizeRotation(PointerAngle(center, x, y) - startOffset));

    /// <summary>
    /// Rotation normalized to -180..180 (spec §5; ledger select-rotate-drag). The identity inside [-180, 180], so a slider
    /// value of 180 stays 180; anything else wraps into [-180, 180). A value that is not finite has no angle and gives 0.
    /// </summary>
    public static double NormalizeRotation(double degrees)
    {
        if (!double.IsFinite(degrees)) return 0;
        if (degrees >= -180 && degrees <= 180) return degrees;
        double r = (degrees + 180) % 360;
        if (r < 0) r += 360;
        return r - 180;
    }

    /// <summary>4.1's snap (editor.js:1176-1179): the first of <see cref="SnapAngles"/> strictly within 3 degrees, else unchanged.</summary>
    public static double SnapRotation(double degrees)
    {
        foreach (var s in SnapAngles)
            if (Math.Abs(degrees - s) < SnapTolerance) return s;
        return degrees;
    }

    /// <summary>
    /// The corner-drag scale (editor.js:1183-1185): <c>startScale * (distance / startDistance)</c> under
    /// <see cref="ClampScale"/>. A start distance under 1e-9 (a press at the center of a vanishing layer) keeps
    /// <paramref name="startScale"/>, where 4.1 divided by zero (ledger select-resize-handles).
    /// </summary>
    public static double ResizeScale(double startScale, double startDistance, double distance)
    {
        if (!(startDistance >= 1e-9)) return startScale;
        return ClampScale(startScale * (distance / startDistance));
    }

    /// <summary>The one scale clamp: [0.01, 5] (1 to 500%). NaN clamps to 0.01 (ruling R6).</summary>
    public static double ClampScale(double scale) => double.IsNaN(scale) ? MinScale : Math.Clamp(scale, MinScale, MaxScale);

    /// <summary>The wheel's scale (ruling R6, wheel-scale-layer fix): x1.05 when <paramref name="sign"/> &gt;= 0, /1.05 otherwise, clamped.</summary>
    public static double WheelScale(double scale, int sign) => ClampScale(sign >= 0 ? scale * WheelStep : scale / WheelStep);

    /// <summary>
    /// The canvas label (editor.js:416-420): null when <paramref name="rotation"/> == 0 (as <c>rotation !== 0</c>; -0 is 0),
    /// else <c>rotation.toFixed(1)</c> plus U+00B0: 17.25 gives "17.3°", -17.25 gives "-17.3°", 1.45 (binary
    /// 1.4499...) gives "1.4°", and -0.04 gives "-0.0°" (<see cref="ToFixed1"/>).
    /// </summary>
    public static string? RotationLabel(double rotation) =>
        rotation == 0 ? null : ToFixed1(rotation) + "°";

    /// <summary>
    /// JavaScript's <c>Number.prototype.toFixed(1)</c> (ECMA-262): the exact binary value rounded to one decimal, an
    /// exact tie taking the larger magnitude, and a negative value keeping its sign when it rounds to 0 ("-0.0").
    /// .NET's "F1" rounds an exact tie to even (17.25 gives "17.2", measured), so the digits come from the exact
    /// "F20" expansion: a double of magnitude 1e-21 or more is never within 5e-21 of a tie it is not equal to, so the
    /// 20th place cannot fake one. NaN and the infinities print as JavaScript prints them; at 1e21 and above toFixed
    /// falls back to ToString, which "R" matches for the label's purpose.
    /// </summary>
    internal static string ToFixed1(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        double a = Math.Abs(value);
        if (a >= 1e21) return value.ToString("R", CultureInfo.InvariantCulture);
        string exact = a.ToString("F20", CultureInfo.InvariantCulture);
        int dot = exact.IndexOf('.');
        var tenths = BigInteger.Parse(exact.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture) * 10 + (exact[dot + 1] - '0');
        if (exact[dot + 2] >= '5') tenths += 1;
        string text = (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (char)('0' + (int)(tenths % 10));
        return value < 0 ? "-" + text : text;
    }

    /// <summary>Half the label exclusion box's width, in CSS px (ruling R4).</summary>
    const double LabelHalfWidth = 30;

    /// <summary>The label exclusion box's extent above and below the alphabetic baseline, in CSS px (ruling R4).</summary>
    const double LabelAbove = 12, LabelBelow = 4;

    /// <summary>
    /// Ruling R4: the device-px axis-aligned box that holds the rotated label, so the view-select golden gate can leave
    /// the label out of the pixel comparison. The label box is local (-30, b - 12) to (30, b + 4) with the baseline
    /// b = -h/2 - 46 (editor.js:420), in CSS px of the zoomed canvas, rotated with the layer about its center and scaled
    /// by <c>DeviceScale</c>, then rounded outward. <see cref="SKRectI.Empty"/> when <see cref="RotationLabel"/> is null.
    /// </summary>
    public static SKRectI LabelBounds(Layer layer, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(options);
        if (RotationLabel(layer.Rotation) is null) return SKRectI.Empty;
        double zoom = options.Zoom, ds = options.DeviceScale;
        double w = layer.Image.Width * layer.Scale * zoom, h = layer.Image.Height * layer.Scale * zoom;
        double cx = layer.X * zoom + w / 2, cy = layer.Y * zoom + h / 2;
        double rad = layer.Rotation * Math.PI / 180, cos = Math.Cos(rad), sin = Math.Sin(rad);
        double baseline = -h / 2 - RotateHandleDistance - 16;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (lx, ly) in new[] { (-LabelHalfWidth, baseline - LabelAbove), (LabelHalfWidth, baseline - LabelAbove), (LabelHalfWidth, baseline + LabelBelow), (-LabelHalfWidth, baseline + LabelBelow) })
        {
            double x = (cx + lx * cos - ly * sin) * ds, y = (cy + lx * sin + ly * cos) * ds;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        return new SKRectI((int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY));
    }
}
