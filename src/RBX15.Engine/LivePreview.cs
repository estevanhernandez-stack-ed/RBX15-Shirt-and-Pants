using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The live drawing preview of render() (editor.js:324-330): 4.1 composites its template-size drawCanvas over the layers
/// and before the mask, at globalAlpha 0.5 for the eraser and 1 for every other tool (ledger render-live-preview,
/// draw-preview-composite). v5 keeps the stroke, not a canvas, and draws it the way its commit draws it (M3b ruling R12,
/// "the preview is the commit"):
/// <list type="bullet">
/// <item><see cref="Draw"/>: <see cref="Tools.DrawStroke"/>, the path <see cref="Tools.CommitDrawing"/> autocrops, so a pen
///   preview is the whole path stroked once and a pen press shows its dot (approved divergences draw-pen-incremental-stroke,
///   draw-pen-single-click).</item>
/// <item><see cref="Erase"/>: <see cref="Eraser.DrawStroke"/> stroked once at half alpha. Fewer than 2 points show nothing,
///   which is exactly what a press without movement erases (ruling R3).</item>
/// </list>
/// Both factories copy the points, so the interaction can keep extending its own list.
/// </summary>
public sealed record LivePreview
{
    LivePreview(Stroke stroke, bool isEraser)
    {
        Stroke = stroke;
        IsEraser = isEraser;
    }

    /// <summary>A pen, line, rect or ellipse stroke in progress (editor.js:1196-1234). Alpha 1.</summary>
    public static LivePreview Draw(Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(stroke.Points);
        return new LivePreview(stroke with { Points = stroke.Points.ToArray() }, isEraser: false);
    }

    /// <summary>
    /// An eraser drag in progress (editor.js:1133-1144, :1200-1203): opaque <see cref="Eraser.StrokeColor"/>, round caps
    /// and joins, width <see cref="Tools.SanitizeWidth"/>(<paramref name="size"/>). Alpha 0.5 (editor.js:327).
    /// </summary>
    public static LivePreview Erase(IReadOnlyList<SKPoint> points, float size)
    {
        ArgumentNullException.ThrowIfNull(points);
        return new LivePreview(new Stroke(ShapeTool.Pen, points.ToArray(), Eraser.StrokeColor, Tools.SanitizeWidth(size), false), isEraser: true);
    }

    /// <summary>The stroke drawn. For an eraser preview: <see cref="ShapeTool.Pen"/>, <see cref="Eraser.StrokeColor"/>, the sanitized size, no fill.</summary>
    public Stroke Stroke { get; }

    /// <summary>True for an eraser preview.</summary>
    public bool IsEraser { get; }

    /// <summary>The preview's globalAlpha: 0.5 for the eraser, else 1 (editor.js:327).</summary>
    public float Alpha => IsEraser ? 0.5f : 1f;
}
