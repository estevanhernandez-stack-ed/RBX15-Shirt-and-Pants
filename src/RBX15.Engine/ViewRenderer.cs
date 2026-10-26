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
/// While a stroke is live (<see cref="ViewOptions.Preview"/> set) the static half of the composite, which is the background,
/// the region fills and every layer (<see cref="Renderer.RenderStatic"/>), is drawn once into a cache keyed on the
/// <see cref="Document"/> instance and copied onto the composite each frame; the preview, the mask and the view overlays
/// are still drawn per frame in 4.1's order, so the output is byte-identical (ledger render-pipeline-canvas-reset, C1).
/// Any new Document instance (an edit, an undo, a mode change) rebuilds it; the first frame without a preview releases it.
/// Every render clears what it reuses, so the output is byte-identical to a fresh <see cref="Renderer.RenderView"/>
/// (pinned by <c>ViewRendererTests</c>, and by the 14 frozen view goldens, which run through this class). Per-call
/// surfaces and images (the composite snapshot, the preview snapshot) are disposed in the call that made them, and
/// <see cref="Dispose"/> releases the persistent ones (M2a carry-forward: native memory lifetime). UI thread only.
/// </summary>
public sealed class ViewRenderer : IDisposable
{
    SKSurface? _composite;
    SKSurface? _preview;
    SKSurface? _staticSurface;
    SKImage? _static;
    Document? _staticDoc;
    SKImage? _tile;
    SKShader? _checker;
    int _tileHalf;
    bool _disposed;

    /// <summary>How many times the static composite has been drawn into the stroke cache. Read by tests: a stroke builds it once.</summary>
    public int CompositeCacheBuilds { get; private set; }

    /// <summary>False draws every frame whole, as before the cache existed. The reference the byte-equality tests render against.</summary>
    internal bool CacheStatic { get; init; } = true;

    /// <summary>True while a stroke's static composite is cached.</summary>
    public bool HasCompositeCache => _static is not null;

    /// <summary>
    /// The view frame: the composite (clear, background, fills, layers, <see cref="ViewOptions.Preview"/> at its alpha,
    /// the mask iff <c>doc.ShowMask</c>) at identity on the persistent surface; then on <paramref name="canvas"/> the
    /// checker, the region guide when <see cref="ViewOptions.ShowRegionGuide"/>, the composite stretched to
    /// <see cref="Renderer.ViewSize"/>, the grid when <see cref="ViewOptions.ShowGrid"/>,
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
        if (options.Preview is null || !CacheStatic)
        {
            ReleaseStatic();
            Renderer.RenderComposite(_composite.Canvas, doc, doc.ShowMask, options.Preview, _preview);
        }
        else
        {
            var cached = StaticComposite(doc);
            var offscreen = _composite.Canvas;
            using (var copy = new SKPaint { BlendMode = SKBlendMode.Src })
                offscreen.DrawImage(cached, 0, 0, Renderer.NearestSampling, copy);   // a straight copy: every pixel is replaced
            Renderer.FinishComposite(offscreen, doc, doc.ShowMask, options.Preview, _preview);
        }

        var frame = SKRect.Create(0, 0, size.Width, size.Height);
        canvas.Clear(SKColors.Transparent);
        using (var checker = new SKPaint { Shader = Checker(Renderer.CheckerHalf(options.DeviceScale)) })
            canvas.DrawRect(frame, checker);

        double scale = options.Scale;
        if (options.ShowRegionGuide) DrawRegionGuide(canvas, doc.Mode, scale, options.DeviceScale);
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

    /// <summary>The cached static half of the composite for <paramref name="doc"/>, drawn now if the cache is empty or holds another document.</summary>
    SKImage StaticComposite(Document doc)
    {
        if (_static is not null && ReferenceEquals(_staticDoc, doc)) return _static;
        _static?.Dispose();   // before the surface is drawn again, so the snapshot does not force a pixel copy
        _static = null;
        _staticSurface ??= SKSurface.Create(Renderer.TemplateInfo) ?? throw new InvalidOperationException("cannot create the static composite surface");
        Renderer.RenderStatic(_staticSurface.Canvas, doc);
        _static = _staticSurface.Snapshot();
        _staticDoc = doc;
        CompositeCacheBuilds++;
        return _static;
    }

    void ReleaseStatic()
    {
        _static?.Dispose();
        _static = null;
        _staticDoc = null;
    }

    /// <summary><see cref="ViewOptions.ShowRegionGuide"/>: under the composite, so art covers it and transparency shows it.</summary>
    static void DrawRegionGuide(SKCanvas canvas, TemplateMode mode, double scale, double deviceScale)
    {
        using var fill = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 0x12) };
        float line = (float)Math.Max(1, Math.Round(deviceScale));
        using var edge = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = line, Color = new SKColor(0x17, 0xD4, 0xFA, 0x73) };
        foreach (var r in Template.Regions(mode))
        {
            var rect = SKRect.Create((float)Math.Round(r.X * scale), (float)Math.Round(r.Y * scale),
                                     (float)Math.Round(r.W * scale), (float)Math.Round(r.H * scale));
            canvas.DrawRect(rect, fill);
            canvas.DrawRect(SKRect.Inflate(rect, -line / 2, -line / 2), edge);
        }
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
        ReleaseStatic();
        _staticSurface?.Dispose();
        _staticSurface = null;
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
