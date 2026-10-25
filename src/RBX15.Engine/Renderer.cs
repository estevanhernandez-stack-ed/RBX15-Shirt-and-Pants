using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The export composition (editor.js exportPNG, lines 1512-1534). The order is a contract (spec
/// section 2): clear, background (stretched image, or color, times opacity), region fills, visible
/// layers bottom to top, then the mask with destination-in. Export always masks.
/// <para>
/// Chromium compatibility index. Each row is a place where SkiaSharp does not match Chromium's Skia
/// by default, and what keeps the renders at 4.1 parity. If a SkiaSharp upgrade breaks goldens, start here.
/// </para>
/// <list type="table">
/// <listheader><term>Point</term><description>Implemented in; exercised by golden scenes</description></listheader>
/// <item><term>kN32 surface so Skia takes its legacy 8888 blitters</term>
///   <description><see cref="Export"/>, <c>Images.ForDrawing</c>; every scene, most sensitively bg-color-half-opacity, layer-basic, layers-order-hidden</description></item>
/// <item><term>Blink ShouldDrawImageAntialiased</term>
///   <description><c>Renderer.DrawImageAntialiased</c>; layer-rotate-scale-fliph-opacity, layer-rotate-neg-flipv-upscale, rgb-shift-with-adjust-rotated, gap-layer-rotate-90 (exactly 90: rect stays rect, no antialiasing)</description></item>
/// <item><term>FloatPipeline: no-op color filter keeps non scale-translate draws out of lowp</term>
///   <description><c>Renderer.FloatPipeline</c>; layer-rotate-scale-fliph-opacity, layer-rotate-neg-flipv-upscale, rgb-shift-with-adjust-rotated, full-scene, gap-layer-rotate-90 (unit test Rotated_layer_composites_in_float_not_lowp)</description></item>
/// <item><term>AutoLayerForImageFilter: a filtered draw runs in a draw-sized SaveLayer</term>
///   <description><c>Renderer.DrawLayer</c> (SaveLayer(dst, filter)); adjust-brightness-150, adjust-contrast-40, adjust-saturate-0, adjust-hue-90, adjust-combo, adjust-extremes, rgb-shift-with-adjust-rotated, full-scene, gap-opacity-with-adjust (opacity under a filter)</description></item>
/// <item><term>cc decode cache: at cc level 1 or more (target at or below 0.5) pre-scale to the source ceil-divided by 2^level, at medium quality through Skia's mip chain at the geometric-mean Skia mip level. cc levels and Skia mip levels are counted separately. The chain is built with SKImage.ScalePixels at Linear + MipmapNearest, which relies on SkiaSharp resolving a floor-halving to Skia mip level 1 and copying it unscaled</term>
///   <description><c>Renderer.DecodeCacheSize</c>, <c>Images.DecodeCacheScaled</c>, <c>Images.SkiaMipLevel</c>; full-scene (word art 198x125 at 0.5: cc level 1 to 99x63, Skia mip level 0), gap-layer-mip2 (word art 198x125 at 0.2: cc level 2 to 50x32, Skia mip level 1; the name counts cc levels), gap-bg-image-prescale (2160x2160 to 585x559: cc level 1 to 1080x1080, Skia mip level 1) (unit tests Decode_cache_prescales_only_at_mip_level_one_or_more, Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n)</description></item>
/// <item><term>Eraser mask draw (lib/erase.js:63): same antialias and float-pipeline rules as a layer draw</term>
///   <description><c>Eraser.EraseStroke</c> (reuses <c>DrawImageAntialiased</c>, <c>FloatPipeline</c>); the 8 erase scenes in tests/goldens/app via GoldenAppTests</description></item>
/// <item><term>FEComponentTransfer: float32, truncating brightness and contrast tables</term>
///   <description><c>Adjust</c> (brightness and contrast tables, <c>FilterChain</c>); adjust-brightness-150, adjust-contrast-40, adjust-combo, adjust-extremes</description></item>
/// </list>
/// <para>
/// Precondition: 4.1 parity holds only on a kN32 premultiplied canvas whose base matrix is template
/// space (585x559, identity). A zoomed or high-DPI view must render the 585x559 composite at identity
/// and scale the resulting bitmap, as 4.1 does (nearest at zoom 1 or above).
/// </para>
/// </summary>
public static class Renderer
{
    /// <summary>Canvas2D default smoothing ('low'): bilinear, no mipmaps.</summary>
    public static readonly SKSamplingOptions LayerSampling = new(SKFilterMode.Linear, SKMipmapMode.None);
    static readonly SKSamplingOptions MaskSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
    /// Renders on a kN32 (platform-native, BGRA on Windows) surface, as Chromium's software canvas
    /// does. Skia picks its legacy 8888 blitters only for N32 devices, and those round differently
    /// from the raster pipeline: src-over is dst*(256-sa)>>8, not the pipeline's (v+255)>>8 divide.
    /// </summary>
    public static SKImage Export(Document doc)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
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

    /// <summary>
    /// Draws the full export composition onto <paramref name="canvas"/>.
    /// <para>
    /// Precondition: the canvas is kN32 premultiplied with an identity base matrix in template space
    /// (585x559). A zoomed or DPI view must render the composite at identity and scale the bitmap; 4.1
    /// scales the composite (nearest at zoom 1 or above), not each draw.
    /// </para>
    /// </summary>
    public static void RenderExport(SKCanvas canvas, Document doc) => RenderComposite(canvas, doc, applyMask: true);

    /// <summary>
    /// The offscreen half of render() (editor.js:300-337) without a live drawing preview:
    /// clear, background, region fills, visible layers bottom to top, then the mask with
    /// destination-in only when <paramref name="applyMask"/>. Export always masks
    /// (<see cref="RenderExport"/>); the view masks when <c>Document.ShowMask</c> (editor.js:332-337).
    /// Same precondition as <see cref="RenderExport"/>: a kN32 premultiplied canvas in template space.
    /// </summary>
    public static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask) =>
        RenderComposite(canvas, doc, applyMask, null, null);

    /// <summary>
    /// <see cref="RenderComposite(SKCanvas, Document, bool)"/> with the live drawing preview (editor.js:324-330) between
    /// the layers and the mask, so a masked view also masks the preview (ledger render-live-preview,
    /// draw-preview-composite). The preview is drawn into <paramref name="previewSurface"/> (a template-size kN32 surface
    /// the caller keeps, <see cref="ViewRenderer"/>) or, when null, into a surface made and disposed here; then that image
    /// is drawn at (0, 0) with <see cref="LivePreview.Alpha"/>, as 4.1's <c>drawImage(drawCanvas, 0, 0)</c> under
    /// globalAlpha. The export never passes a preview.
    /// </summary>
    internal static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask, LivePreview? preview, SKSurface? previewSurface)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        RenderStatic(canvas, doc);
        FinishComposite(canvas, doc, applyMask, preview, previewSurface);
    }

    /// <summary>
    /// Everything of the composite that sits below the live preview: clear, background, region fills, visible layers bottom
    /// to top. <see cref="ViewRenderer"/> caches this half while a stroke is live (ledger render-pipeline-canvas-reset);
    /// <see cref="FinishComposite"/> is the rest.
    /// </summary>
    internal static void RenderStatic(SKCanvas canvas, Document doc)
    {
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, doc);
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
    }

    /// <summary>The half above <see cref="RenderStatic"/>: the live preview at its alpha, then the mask iff <paramref name="applyMask"/>.</summary>
    internal static void FinishComposite(SKCanvas canvas, Document doc, bool applyMask, LivePreview? preview, SKSurface? previewSurface)
    {
        if (preview is not null) DrawPreview(canvas, preview, previewSurface);
        if (!applyMask) return;
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

    static void DrawPreview(SKCanvas canvas, LivePreview preview, SKSurface? reuse)
    {
        SKSurface? owned = null;
        var surface = reuse ?? (owned = SKSurface.Create(TemplateInfo) ?? throw new InvalidOperationException("cannot create the preview surface"));
        try
        {
            var pc = surface.Canvas;
            pc.Clear(SKColors.Transparent);
            if (preview.IsEraser) Eraser.DrawStroke(pc, preview.Stroke.Points, preview.Stroke.Width, EraseStroking.Once);
            else Tools.DrawStroke(pc, preview.Stroke);
            using var image = surface.Snapshot();
            using var paint = new SKPaint { ColorF = new SKColorF(0, 0, 0, preview.Alpha) };
            canvas.DrawImage(image, 0, 0, LayerSampling, paint);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>The template-size kN32 premultiplied surface info every composite, stroke and preview surface uses.</summary>
    internal static readonly SKImageInfo TemplateInfo =
        new(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

    /// <summary>
    /// The view frame's size in device pixels. 4.1 assigns <c>canvas.width = TW * zoom</c>
    /// (editor.js:340-341), and the browser truncates a fractional width, so zoom 1.5 gives
    /// 877x838 and zoom 0.25 gives 146x139 (render-zoom-canvas). The frame keeps that truncation,
    /// with the device scale folded in, because the view goldens freeze 4.1's sizes and the
    /// composite is stretched to exactly this frame (editor.js:350).
    /// </summary>
    public static SKSizeI ViewSize(ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        double scale = options.Scale;
        int w = (int)Math.Truncate(TemplateSize.Width * scale), h = (int)Math.Truncate(TemplateSize.Height * scale);
        if (w < 1 || h < 1) throw new ArgumentOutOfRangeException(nameof(options), scale, "the view frame would be empty");
        return new SKSizeI(w, h);
    }

    static readonly SKColor CheckerBase = new(0x0f, 0x1f, 0x31);  // editor.js:435
    static readonly SKColor CheckerSquare = new(0x05, 0x0c, 0x18); // editor.js:436
    internal static readonly SKSamplingOptions NearestSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
    /// Half the checkerboard tile in device pixels: 8 CSS px (editor.js:432-437) times the device
    /// scale, rounded to a whole pixel and at least 1, so the squares stay crisp at any DPI.
    /// </summary>
    internal static int CheckerHalf(double deviceScale) => Math.Max(1, (int)Math.Round(8 * deviceScale, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The view (render(), editor.js:300-429), through a <see cref="ViewRenderer"/> made for this call. The composite
    /// (with <see cref="ViewOptions.Preview"/>) renders at 585x559 identity (the Renderer's precondition), masked
    /// iff <c>doc.ShowMask</c>. The frame then gets:
    /// <list type="number">
    /// <item>the checkerboard (editor.js:345-348, :431-438): a tile of 2 x <see cref="CheckerHalf"/> device pixels anchored
    ///   at the frame origin, base #0f1f31 with #050c18 in the top-left and bottom-right quarters. It does not scale with zoom
    ///   (render-checkerboard);</item>
    /// <item>the composite stretched to the whole frame (editor.js:350), bilinear only when <c>Zoom * DeviceScale &lt; 1</c>
    ///   (editor.js:342; spec §4, nearest at 100% and above);</item>
    /// <item>when <c>options.ShowGrid</c>, every region of the document's mode stroked with rgba(255,255,0,0.3) at
    ///   (x, y, w, h) times the scale, unsnapped (editor.js:352-359; render-grid-overlay, overlay-grid-checkbox). The line
    ///   width is <c>DeviceScale</c>, 4.1's 1 CSS px.</item>
    /// <item>when <see cref="ViewOptions.SelectedLayerId"/> names a visible layer, the selection overlay
    ///   (<see cref="DrawSelectionOverlay"/>, editor.js:361-425).</item>
    /// </list>
    /// Precondition: <paramref name="canvas"/> is <see cref="ViewSize"/> pixels, kN32 premultiplied, identity matrix.
    /// The app keeps one <see cref="ViewRenderer"/> instead, so the offscreen surface and checker tile persist.
    /// </summary>
    public static void RenderView(SKCanvas canvas, Document doc, ViewOptions options)
    {
        using var renderer = new ViewRenderer();
        renderer.Render(canvas, doc, options);
    }

    internal static SKImage CheckerTile(int half)
    {
        var info = new SKImageInfo(half * 2, half * 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the checker tile");
        surface.Canvas.Clear(CheckerBase);
        using var square = new SKPaint { Color = CheckerSquare };
        surface.Canvas.DrawRect(SKRect.Create(0, 0, half, half), square);
        surface.Canvas.DrawRect(SKRect.Create(half, half, half, half), square);
        return surface.Snapshot();
    }

    static readonly SKColor SelectCyan = new(0x17, 0xd4, 0xfa);    // editor.js:375, :382
    static readonly SKColor SelectMagenta = new(0xf2, 0x2f, 0x89); // editor.js:391, :397, :418

    /// <summary>
    /// The selection overlay (editor.js:361-425; ledger render-selection-box, render-resize-handles,
    /// render-rotation-handle, render-rotation-label, select-handle-render), drawn on the view frame after the grid.
    /// Every size is 4.1's CSS px; the canvas is scaled by <c>DeviceScale</c> first, so at device scale 1 this is 4.1's
    /// drawing call for call. In the frame of the zoomed layer box (w = img.width*scale*zoom, centered at
    /// x*zoom + w/2, y*zoom + h/2, rotated by the layer's rotation; flips are not applied, editor.js:365-373):
    /// <list type="bullet">
    /// <item>the box: strokeRect(-w/2, -h/2, w, h), #17d4fa, width 2, dash [5, 5] from the top-left corner (:375-379);</item>
    /// <item>the corner handles: fillRect(hx-4, hy-4, 8, 8) at TL, TR, BR, BL, #17d4fa (:382-384);</item>
    /// <item>the stem: (0, -h/2) to (0, -h/2 - 30), #f22f89, width 2 (:387-393);</item>
    /// <item>the knob: a circle of radius 8 at (0, -h/2 - 30), filled #f22f89, stroked white 1.5 (:395-401);</item>
    /// <item>the glyph: an arc of radius 4 from -0.7π to 0.4π and its arrowhead, stroked white 1.5 (:403-414);</item>
    /// <item>the label when rotation != 0: <see cref="SelectionGeometry.RotationLabel"/> in 11 px JetBrains Mono 400,
    ///   #f22f89, centered, alphabetic baseline at (0, -h/2 - 46) (:416-420). JetBrains Mono is the vendored face
    ///   (render-rotation-label fix: bundled, so offline users get it too).</item>
    /// </list>
    /// Canvas defaults apply where 4.1 leaves them: antialiased, butt caps, miter joins with limit 10.
    /// A hidden layer draws nothing (M3b ruling R2).
    /// </summary>
    internal static void DrawSelectionOverlay(SKCanvas canvas, Layer layer, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(options);
        if (!layer.Visible) return;
        double zoom = options.Zoom;
        double w = layer.Image.Width * layer.Scale * zoom, h = layer.Image.Height * layer.Scale * zoom; // editor.js:365-366
        double cx = layer.X * zoom + w / 2, cy = layer.Y * zoom + h / 2;                               // editor.js:367-368
        int restoreTo = canvas.Save();
        canvas.Scale((float)options.DeviceScale);
        canvas.Translate((float)cx, (float)cy);
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));

        using (var dash = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0))
        using (var box = OverlayStroke(SelectCyan, 2))
        {
            box.PathEffect = dash;
            canvas.DrawRect(SKRect.Create((float)(-w / 2), (float)(-h / 2), (float)w, (float)h), box);
        }

        using (var handle = new SKPaint { Color = SelectCyan, IsAntialias = true, Style = SKPaintStyle.Fill })
            foreach (var (hx, hy) in new[] { (-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2) })
                canvas.DrawRect(SKRect.Create((float)(hx - 4), (float)(hy - 4), 8, 8), handle);

        double knobY = -h / 2 - SelectionGeometry.RotateHandleDistance;
        using (var stem = OverlayStroke(SelectMagenta, 2))
        using (var pb = new SKPathBuilder())
        {
            pb.MoveTo(0, (float)(-h / 2));
            pb.LineTo(0, (float)knobY);
            using var path = pb.Detach();
            canvas.DrawPath(path, stem);
        }

        using (var white = OverlayStroke(SKColors.White, 1.5f))
        {
            using (var knob = CanvasArc(0, (float)knobY, 8, 0, Math.PI * 2))
            {
                using (var fill = new SKPaint { Color = SelectMagenta, IsAntialias = true, Style = SKPaintStyle.Fill })
                    canvas.DrawPath(knob, fill);
                canvas.DrawPath(knob, white);
            }
            using (var glyph = CanvasArc(0, (float)knobY, 4, -Math.PI * 0.7, Math.PI * 0.4))
                canvas.DrawPath(glyph, white);
            double ax = Math.Cos(Math.PI * 0.4) * 4; // editor.js:408-409
            double ay = -h / 2 - SelectionGeometry.RotateHandleDistance + Math.Sin(Math.PI * 0.4) * 4;
            using var pb = new SKPathBuilder();
            pb.MoveTo((float)(ax - 3), (float)(ay - 2));
            pb.LineTo((float)ax, (float)ay);
            pb.LineTo((float)(ax + 2), (float)(ay - 3));
            using var arrow = pb.Detach();
            canvas.DrawPath(arrow, white);
        }

        canvas.RestoreToCount(restoreTo);
        DrawRotationLabel(canvas, layer, options);
    }

    /// <summary>
    /// The overlay's rotation label (editor.js:416-420; render-rotation-label): nothing when
    /// <see cref="SelectionGeometry.RotationLabel"/> is null, else that text in 11 px JetBrains Mono 400 (the vendored
    /// face), #f22f89, centered, alphabetic baseline at (0, -h/2 - 46) in the zoomed, rotated layer frame, the canvas scaled
    /// by <c>DeviceScale</c>. Every pixel it draws lies inside <see cref="SelectionGeometry.LabelBounds"/> (ruling R4).
    /// </summary>
    internal static void DrawRotationLabel(SKCanvas canvas, Layer layer, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(options);
        if (SelectionGeometry.RotationLabel(layer.Rotation) is not { } label) return;
        double zoom = options.Zoom;
        double w = layer.Image.Width * layer.Scale * zoom, h = layer.Image.Height * layer.Scale * zoom;
        int restoreTo = canvas.Save();
        canvas.Scale((float)options.DeviceScale);
        canvas.Translate((float)(layer.X * zoom + w / 2), (float)(layer.Y * zoom + h / 2));
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));
        using var font = Fonts.CreateFont("JetBrains Mono", 400, 11);
        using var text = new SKPaint { Color = SelectMagenta, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawText(label, 0, (float)(-h / 2 - SelectionGeometry.RotateHandleDistance - 16), SKTextAlign.Center, font, text);
        canvas.RestoreToCount(restoreTo);
    }

    static SKPaint OverlayStroke(SKColor color, float width) => new()
    {
        Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
        StrokeCap = SKStrokeCap.Butt, StrokeJoin = SKStrokeJoin.Miter, StrokeMiter = 10,
    };

    /// <summary>
    /// <c>beginPath(); arc(x, y, r, start, end)</c>, clockwise, as Chromium 146 draws it (measured, ruling T1-2): one Skia
    /// arcTo of the sweep from the start angle as given, in degrees converted from 4.1's doubles, or two arcTo of 180
    /// degrees for a full turn. The start is not canonicalized into [0, 2π): -0.7π stays -126 degrees. Canonicalizing it to
    /// 234 degrees moves the 1.5 px glyph's antialiasing by up to 46 levels at every one of 100 sub-pixel positions;
    /// as given, all 100 match 4.1 exactly. The builder is empty, so the first arcTo starts with Skia's own moveTo to
    /// the arc's start point, as on canvas after beginPath().
    /// </summary>
    static SKPath CanvasArc(float x, float y, float r, double start, double end)
    {
        double sweep = end - start;
        float startDeg = (float)(start * 180 / Math.PI), sweepDeg = (float)(sweep * 180 / Math.PI);
        var oval = new SKRect(x - r, y - r, x + r, y + r);
        using var pb = new SKPathBuilder();
        if (sweep >= Math.PI * 2)
        {
            pb.ArcTo(oval, startDeg, 180, false);
            pb.ArcTo(oval, startDeg + 180, 180, false);
        }
        else
        {
            pb.ArcTo(oval, startDeg, sweepDeg, false);
        }
        return pb.Detach();
    }

    /// <summary><see cref="RenderView"/> into a new kN32 premultiplied image of <see cref="ViewSize"/>. For tests and goldens; the app renders into its own bitmap.</summary>
    public static SKImage RenderViewImage(Document doc, ViewOptions options)
    {
        var size = ViewSize(options);
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the view surface");
        RenderView(surface.Canvas, doc, options);
        return surface.Snapshot();
    }

    static void DrawBackground(SKCanvas canvas, Document doc)
    {
        var full = SKRect.Create(0, 0, TemplateSize.Width, TemplateSize.Height);
        float alpha = (float)doc.BgOpacity;
        if (doc.BgImage is { } bg)
        {
            using var p = new SKPaint { ColorF = new SKColorF(0, 0, 0, alpha) };
            var image = Images.ForDrawing(bg);
            canvas.DrawImage(DecodeCache(image, full.Width / image.Width, full.Height / image.Height), full, LayerSampling, p);
            return;
        }
        if (doc.BgColor is "transparent" or "") return;
        // An unparsable fillStyle is ignored by canvas, which keeps its default #000000.
        if (!CssColor.TryParseCanvasFill(doc.BgColor, out var c)) c = SKColors.Black;
        using var fill = new SKPaint { ColorF = new SKColorF(c.Red / 255f, c.Green / 255f, c.Blue / 255f, alpha) };
        canvas.DrawRect(full, fill);
    }

    /// <summary>
    /// A no-op color filter. Chromium's software canvas composites a draw that its legacy blitters
    /// decline (any non scale-translate matrix) in Skia's float pipeline; SkiaSharp's build picks the
    /// 8-bit lowp pipeline for the same draw, one level high on blended pixels. A matrix color filter
    /// has no lowp stage, so attaching this one keeps those draws in float, as in 4.1.
    /// </summary>
    internal static readonly SKColorFilter FloatPipeline = SKColorFilter.CreateColorMatrix(new float[]
    {
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
    });

    /// <summary>
    /// drawLayerWithAdjustments: translate to center, rotate, then flip, opacity and filter. When the
    /// layer has adjustments, 4.1 sets ctx.filter, and a draw whose paint carries an image filter is
    /// run by Skia as a layer the size of the draw's device bounds: the image is drawn unfiltered
    /// into it, and the filter applies as the layer composites back. Drawing unfiltered keeps the
    /// inner draw on the same blitter 4.1 used, and the layer's origin sets the sampler's start point,
    /// which decides the 4-bit bilinear weights of the legacy scale-translate sampler.
    /// <para>
    /// Precondition: the canvas is kN32 premultiplied with an identity base matrix in template space
    /// (585x559). A zoomed or DPI view must render the composite at identity and scale the bitmap; 4.1
    /// scales the composite (nearest at zoom 1 or above), not each draw.
    /// </para>
    /// </summary>
    public static void DrawLayer(SKCanvas canvas, Layer layer)
    {
        if (!layer.Visible) return;
        bool shifted = layer.RShift != 0 || layer.GShift != 0 || layer.BShift != 0;
        var source = shifted ? Adjust.ShiftRgb(layer.Image, layer.RShift, layer.GShift, layer.BShift) : layer.Image;
        float w = (float)(layer.Image.Width * layer.Scale), h = (float)(layer.Image.Height * layer.Scale);
        var dst = SKRect.Create(-w / 2, -h / 2, w, h);
        int restoreTo = canvas.Save();
        canvas.Translate((float)(layer.X + layer.Image.Width * layer.Scale / 2), (float)(layer.Y + layer.Image.Height * layer.Scale / 2));
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));
        if (layer.FlipH) canvas.Scale(-1, 1);
        if (layer.FlipV) canvas.Scale(1, -1);
        var ctm = canvas.TotalMatrix;

        using var filter = Adjust.HasAdjustments(layer)
            ? new SKPaint { ColorFilter = Adjust.FilterChain(layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate) }
            : null;
        if (filter is not null) canvas.SaveLayer(dst, filter);

        using var paint = new SKPaint
        {
            ColorF = new SKColorF(0, 0, 0, (float)layer.Opacity),
            IsAntialias = DrawImageAntialiased(ctm, w, h),
            ColorFilter = IsScaleTranslate(ctm) ? null : FloatPipeline,
        };
        var image = Images.ForDrawing(source);
        // applyRGBShift hands drawImage a canvas, which never goes through the decode cache.
        if (!shifted)
            image = DecodeCache(image, ColumnLength(ctm.ScaleX, ctm.SkewY) * w / image.Width, ColumnLength(ctm.SkewX, ctm.ScaleY) * h / image.Height);
        canvas.DrawImage(image, dst, LayerSampling, paint);
        canvas.RestoreToCount(restoreTo);
    }

    internal static bool IsScaleTranslate(SKMatrix m) => m.SkewX == 0 && m.SkewY == 0 && m.Persp0 == 0 && m.Persp1 == 0;

    static float ColumnLength(float a, float b) => MathF.Sqrt(a * a + b * b);

    /// <summary>
    /// Blink's BaseRenderingContext2D::ShouldDrawImageAntialiased: antialias when the matrix does not
    /// keep rects rects (any rotation off a multiple of 90 degrees), or when the destination is
    /// thinner than one device pixel.
    /// </summary>
    internal static bool DrawImageAntialiased(SKMatrix m, float w, float h)
    {
        bool rectStaysRect = (m.SkewX == 0 && m.SkewY == 0 && m.ScaleX != 0 && m.ScaleY != 0) ||
                             (m.ScaleX == 0 && m.ScaleY == 0 && m.SkewX != 0 && m.SkewY != 0);
        if (!rectStaysRect || m.Persp0 != 0 || m.Persp1 != 0) return true;
        bool affine = m.SkewX != 0 || m.SkewY != 0;
        float widthExpansion = affine ? m.SkewY : m.ScaleX, heightExpansion = affine ? m.SkewX : m.ScaleY;
        return w * Math.Abs(widthExpansion) < 1 || h * Math.Abs(heightExpansion) < 1;
    }

    /// <summary>The image the decode cache serves for a draw at this scale (see <see cref="DecodeCacheSize"/>).</summary>
    static SKImage DecodeCache(SKImage premul, double scaleX, double scaleY)
    {
        var size = DecodeCacheSize(premul.Width, premul.Height, scaleX, scaleY);
        return size is { } s ? Images.DecodeCacheScaled(premul, s.Width, s.Height) : premul;
    }

    /// <summary>
    /// cc's software decode cache (CacheKey::FromDrawImage with MipMapUtil): the target is the source
    /// scaled and rounded; the cc level is how many ceil-halvings of the source stay at or above
    /// the target on both axes. cc level 0 draws the original (null); cc level n pre-scales to the
    /// source ceil-divided by 2^n. The Skia mip level that pre-scale samples is a separate count
    /// (<see cref="Images.SkiaMipLevel"/>).
    /// </summary>
    internal static (int Width, int Height)? DecodeCacheSize(int width, int height, double scaleX, double scaleY)
    {
        int targetW = (int)Math.Floor(Math.Abs(width * scaleX) + 0.5), targetH = (int)Math.Floor(Math.Abs(height * scaleY) + 0.5);
        if (targetW <= 0 || targetH <= 0) return null;
        int level = 0, mipW = width, mipH = height;
        while (true)
        {
            mipW = Math.Max(1, (mipW + 1) / 2);
            mipH = Math.Max(1, (mipH + 1) / 2);
            if (mipW < targetW || mipH < targetH) break;
            level++;
            if (mipW == 1 && mipH == 1) break;
        }
        if (level == 0) return null;
        return (Math.Max(1, (width + (1 << level) - 1) >> level), Math.Max(1, (height + (1 << level) - 1) >> level));
    }
}
