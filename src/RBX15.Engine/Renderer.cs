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
    /// The offscreen half of render() (editor.js:300-337) without the live drawing preview (M3b):
    /// clear, background, region fills, visible layers bottom to top, then the mask with
    /// destination-in only when <paramref name="applyMask"/>. Export always masks
    /// (<see cref="RenderExport"/>); the view masks when <c>Document.ShowMask</c> (editor.js:332-337).
    /// Same precondition as <see cref="RenderExport"/>: a kN32 premultiplied canvas in template space.
    /// </summary>
    public static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, doc);
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        if (!applyMask) return;
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

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
    static readonly SKSamplingOptions NearestSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
    /// Half the checkerboard tile in device pixels: 8 CSS px (editor.js:432-437) times the device
    /// scale, rounded to a whole pixel and at least 1, so the squares stay crisp at any DPI.
    /// </summary>
    internal static int CheckerHalf(double deviceScale) => Math.Max(1, (int)Math.Round(8 * deviceScale, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The view (render(), editor.js:300-359) minus the live drawing preview and the selection overlay
    /// (both M3b). The composite renders at 585x559 identity (the Renderer's precondition), masked
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
    /// </list>
    /// Precondition: <paramref name="canvas"/> is <see cref="ViewSize"/> pixels, kN32 premultiplied, identity matrix.
    /// </summary>
    public static void RenderView(SKCanvas canvas, Document doc, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        var size = ViewSize(options);

        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var offscreen = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the composite surface");
        RenderComposite(offscreen.Canvas, doc, doc.ShowMask);
        using var composite = offscreen.Snapshot();

        var frame = SKRect.Create(0, 0, size.Width, size.Height);
        canvas.Clear(SKColors.Transparent);
        using (var tile = CheckerTile(CheckerHalf(options.DeviceScale)))
        using (var shader = tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, NearestSampling))
        using (var checker = new SKPaint { Shader = shader })
            canvas.DrawRect(frame, checker);

        double scale = options.Scale;
        canvas.DrawImage(composite, frame, scale < 1 ? LayerSampling : NearestSampling);

        if (!options.ShowGrid) return;
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

    static SKImage CheckerTile(int half)
    {
        var info = new SKImageInfo(half * 2, half * 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the checker tile");
        surface.Canvas.Clear(CheckerBase);
        using var square = new SKPaint { Color = CheckerSquare };
        surface.Canvas.DrawRect(SKRect.Create(0, 0, half, half), square);
        surface.Canvas.DrawRect(SKRect.Create(half, half, half, half), square);
        return surface.Snapshot();
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
