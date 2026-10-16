using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// A reusable view renderer: render() (editor.js:300-429) with 4.1's persistent offscreen. 4.1 allocates its 585x559
/// composite canvas once and reuses it on every render (editor.js:296-298, <c>_offCanvas</c>) and builds its checker tile
/// once (editor.js:431-438). This class keeps the same three things for the app's canvas host:
/// <list type="bullet">
/// <item>one 585x559 kN32 premultiplied composite surface, cleared at the start of every render;</item>
/// <item>one 585x559 preview surface, made on the first render with a <see cref="ViewOptions.Preview"/>;</item>
/// <item>the checker tile and its shader, rebuilt only when <see cref="Renderer.CheckerHalf"/> changes (a DPI change).</item>
/// </list>
/// Every render clears what it reuses, so the output is byte-identical to a fresh <see cref="Renderer.RenderView"/>
/// (pinned by <c>ViewRendererTests</c>, and by the 14 frozen view goldens, which run through this class). Per-call
/// surfaces and images (the composite snapshot, the preview snapshot) are disposed in the call that made them, and
/// <see cref="Dispose"/> releases the persistent ones (M2a carry-forward: native memory lifetime). UI thread only.
/// </summary>
public sealed class ViewRenderer : IDisposable
{
    SKSurface? _composite;
    SKSurface? _preview;
    SKImage? _tile;
    SKShader? _checker;
    int _tileHalf;
    bool _disposed;

    /// <summary>
    /// The view frame: the composite (clear, background, fills, layers, <see cref="ViewOptions.Preview"/> at its alpha,
    /// the mask iff <c>doc.ShowMask</c>) at identity on the persistent surface; then on <paramref name="canvas"/> the
    /// checker, the composite stretched to <see cref="Renderer.ViewSize"/>, the grid when <see cref="ViewOptions.ShowGrid"/>,
    /// and the selection overlay when <see cref="ViewOptions.SelectedLayerId"/> names a visible layer.
    /// Precondition as <see cref="Renderer.RenderView"/>: <paramref name="canvas"/> is <see cref="Renderer.ViewSize"/>
    /// pixels, kN32 premultiplied, identity matrix. Throws <see cref="ObjectDisposedException"/> after <see cref="Dispose"/>.
    /// </summary>
    public void Render(SKCanvas canvas, Document doc, ViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        var size = Renderer.ViewSize(options);

        _composite ??= SKSurface.Create(Renderer.TemplateInfo) ?? throw new InvalidOperationException("cannot create the composite surface");
        if (options.Preview is not null)
            _preview ??= SKSurface.Create(Renderer.TemplateInfo) ?? throw new InvalidOperationException("cannot create the preview surface");
        Renderer.RenderComposite(_composite.Canvas, doc, doc.ShowMask, options.Preview, _preview);

        var frame = SKRect.Create(0, 0, size.Width, size.Height);
        canvas.Clear(SKColors.Transparent);
        using (var checker = new SKPaint { Shader = Checker(Renderer.CheckerHalf(options.DeviceScale)) })
            canvas.DrawRect(frame, checker);

        double scale = options.Scale;
        using (var composite = _composite.Snapshot())
            canvas.DrawImage(composite, frame, scale < 1 ? Renderer.LayerSampling : Renderer.NearestSampling);

        if (options.ShowGrid)
        {
            using var grid = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)options.DeviceScale,
                IsAntialias = true,
                ColorF = new SKColorF(1, 1, 0, 0.3f),
            };
            foreach (var r in Template.Regions(doc.Mode))
                canvas.DrawRect(SKRect.Create((float)(r.X * scale), (float)(r.Y * scale), (float)(r.W * scale), (float)(r.H * scale)), grid);
        }

        if (options.SelectedLayerId is int id && doc.Layers.Find(l => l.Id == id) is { Visible: true } selected)
            Renderer.DrawSelectionOverlay(canvas, selected, options);
    }

    SKShader Checker(int half)
    {
        if (_checker is not null && _tileHalf == half) return _checker;
        _checker?.Dispose();
        _tile?.Dispose();
        _tile = Renderer.CheckerTile(half);
        _checker = _tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, Renderer.NearestSampling);
        _tileHalf = half;
        return _checker;
    }

    /// <summary>Releases the persistent surfaces, tile and shader. Idempotent; <see cref="Render"/> refuses afterwards.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _checker?.Dispose();
        _tile?.Dispose();
        _preview?.Dispose();
        _composite?.Dispose();
        _checker = null;
        _tile = null;
        _preview = null;
        _composite = null;
    }
}
