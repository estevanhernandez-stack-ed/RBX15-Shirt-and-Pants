using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Renderer.RenderView, ViewSize and RenderComposite (render(), editor.js:300-359). The view goldens
/// (GoldenViewTests) hold device scale 1 to 4.1's pixels; these facts pin the rules the goldens cannot
/// show: the device scale, exact nearest magnification of edge content, and the guards.
/// </summary>
public class RendererViewTests
{
    static readonly SKColor CheckerBase = new(0x0f, 0x1f, 0x31);
    static readonly SKColor CheckerSquare = new(0x05, 0x0c, 0x18);

    static SKBitmap View(Document doc, ViewOptions options)
    {
        using var image = Renderer.RenderViewImage(doc, options);
        return ImageDiff.FromImage(image);
    }

    /// <summary>A composite that is opaque everywhere (mask off, opaque background) with gradient and rotated edges.</summary>
    static Document OpaqueEdgy() => new()
    {
        ShowMask = false,
        RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, null))),
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(60, 24, new SKColor(242, 47, 137)), X = 250, Y = 100, Rotation = 30 }),
    };

    [Theory] // ViewOptions guards Zoom and DeviceScale on construction and on with
    [InlineData(0, 1)]
    [InlineData(-0.25, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, 0)]
    [InlineData(1, double.NaN)]
    public void View_options_refuse_a_zoom_or_scale_that_is_not_finite_and_positive(double zoom, double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewOptions(zoom, scale));
        var ok = new ViewOptions(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ok with { Zoom = zoom, DeviceScale = scale });
    }

    [Fact]
    public void View_options_default_to_device_scale_1_without_grid()
    {
        var o = new ViewOptions(1.5);
        Assert.Equal((1.5, 1.0, false, 1.5), (o.Zoom, o.DeviceScale, o.ShowGrid, o.Scale));
        Assert.Equal(new ViewOptions(1.5, 1, false), o);
    }

    [Theory] // canvas.width = TW * zoom truncates (editor.js:340-341; render-zoom-canvas: 1.5 -> 877x838)
    [InlineData(0.25, 1, 146, 139)]
    [InlineData(0.5, 1, 292, 279)]
    [InlineData(0.75, 1, 438, 419)]
    [InlineData(1, 1, 585, 559)]
    [InlineData(1.5, 1, 877, 838)]
    [InlineData(3, 1, 1755, 1677)]
    [InlineData(1, 1.25, 731, 698)]
    [InlineData(1, 1.5, 877, 838)]
    [InlineData(0.25, 1.75, 255, 244)]
    [InlineData(3, 2, 3510, 3354)]
    public void View_size_is_the_template_times_zoom_and_device_scale_truncated(double zoom, double scale, int w, int h) =>
        Assert.Equal(new SKSizeI(w, h), Renderer.ViewSize(new ViewOptions(zoom, scale)));

    [Fact]
    public void A_view_too_small_to_hold_a_pixel_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer.ViewSize(new ViewOptions(0.001)));

    [Theory] // the 8 px checker half (editor.js:432-437) at device resolution, whole pixels
    [InlineData(1, 8)]
    [InlineData(1.25, 10)]
    [InlineData(1.5, 12)]
    [InlineData(1.75, 14)]
    [InlineData(2, 16)]
    [InlineData(0.5, 4)]
    [InlineData(1.0625, 9)]
    [InlineData(0.01, 1)]
    public void Checker_half_is_8_px_times_the_device_scale_rounded(double scale, int half) =>
        Assert.Equal(half, Renderer.CheckerHalf(scale));

    [Theory] // render-checkerboard parityCheck: corners (0,0),(8,0),(8,8),(0,8); 16 px tile at every zoom
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(3)]
    public void Checker_is_anchored_at_the_origin_and_never_scales_with_zoom(double zoom)
    {
        using var v = View(new Document(), new ViewOptions(zoom)); // (0,0)..(16,16) is off every region at every zoom; mask on
        Assert.Equal(CheckerSquare, v.GetPixel(0, 0));
        Assert.Equal(CheckerBase, v.GetPixel(8, 0));
        Assert.Equal(CheckerSquare, v.GetPixel(8, 8));
        Assert.Equal(CheckerBase, v.GetPixel(0, 8));
        Assert.Equal(CheckerSquare, v.GetPixel(15, 15));
        Assert.Equal(CheckerSquare, v.GetPixel(16, 0));
    }

    [Fact] // DeviceScale 1.5: 12 px squares, so a 150% monitor shows 4.1's 8 CSS px squares crisply
    public void Checker_squares_follow_the_device_scale()
    {
        using var v = View(new Document(), new ViewOptions(1, 1.5));
        Assert.Equal(CheckerSquare, v.GetPixel(11, 11));
        Assert.Equal(CheckerBase, v.GetPixel(12, 0));
        Assert.Equal(CheckerSquare, v.GetPixel(12, 12));
        Assert.Equal(CheckerSquare, v.GetPixel(24, 0));
    }

    [Theory] // 4.1's visible canvas is opaque: the checker fills it before the composite (editor.js:344-350)
    [InlineData(0.25, true)]
    [InlineData(1, false)]
    public void Every_frame_pixel_is_opaque(double zoom, bool mask)
    {
        using var v = View(new Document { BgOpacity = 0.3, ShowMask = mask }, new ViewOptions(zoom));
        for (int y = 0; y < v.Height; y++)
            for (int x = 0; x < v.Width; x++)
                Assert.Equal(255, v.GetPixel(x, y).Alpha);
    }

    [Fact] // overlay-mask-checkbox: the view masks iff Document.ShowMask; export always masks
    public void The_view_masks_by_the_document_flag_and_export_always_masks()
    {
        var red = new Document { BgColor = "#ff0000" };
        using (var on = View(red, new ViewOptions(1))) Assert.Equal(CheckerSquare, on.GetPixel(2, 2));
        using (var off = View(red with { ShowMask = false }, new ViewOptions(1))) Assert.Equal(SKColors.Red, off.GetPixel(2, 2));
        Assert.Equal(0, Px.At(Renderer.Export(red with { ShowMask = false }), 2, 2).Alpha);
    }

    [Fact] // RenderExport is RenderComposite with the mask, byte for byte
    public void Export_is_the_masked_composite()
    {
        var doc = OpaqueEdgy() with { ShowMask = false };
        using var export = ImageDiff.FromImage(Renderer.Export(doc));
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Renderer.RenderComposite(surface.Canvas, doc, applyMask: true);
        using var masked = ImageDiff.FromImage(surface.Snapshot());
        Assert.Equal(export.Bytes, masked.Bytes);

        Renderer.RenderComposite(surface.Canvas, doc, applyMask: false);
        using var unmasked = ImageDiff.FromImage(surface.Snapshot());
        Assert.Equal(255, unmasked.GetPixel(2, 2).Alpha);
        Assert.Equal(0, masked.GetPixel(2, 2).Alpha);
    }

    [Fact] // spec §4 nearest at 100% and above: 300% is an exact 3x copy of 100% (the goldens at 300% use flat content)
    public void At_300_percent_every_pixel_is_an_exact_3x3_copy_of_100_percent()
    {
        var doc = OpaqueEdgy();
        using var one = View(doc, new ViewOptions(1));
        using var three = View(doc, new ViewOptions(3));
        for (int y = 0; y < three.Height; y++)
            for (int x = 0; x < three.Width; x++)
                Assert.Equal(one.GetPixel(x / 3, y / 3), three.GetPixel(x, y));
    }

    [Fact] // the device scale and the zoom multiply: zoom 1.5 at 200% DPI is the 300% frame (checker hidden)
    public void Device_scale_and_zoom_multiply()
    {
        var doc = OpaqueEdgy();
        using var a = View(doc, new ViewOptions(1.5, 2));
        using var b = View(doc, new ViewOptions(3));
        Assert.Equal(b.Bytes, a.Bytes);
    }

    [Fact] // editor.js:342 imageSmoothingEnabled = zoom < 1: below 100% a hard edge blends
    public void Below_100_percent_the_composite_is_smoothed()
    {
        var doc = new Document
        {
            BgColor = "#000000",
            ShowMask = false,
            Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(101, 559, SKColors.White) }),
        };
        using var half = View(doc, new ViewOptions(0.5));
        var edge = half.GetPixel(50, 100);
        Assert.InRange(edge.Red, 1, 254);
        Assert.Equal((edge.Red, edge.Red), (edge.Green, edge.Blue));
        Assert.Equal(SKColors.White, half.GetPixel(40, 100));
        Assert.Equal(SKColors.Black, half.GetPixel(60, 100));
    }

    [Fact] // overlay-grid-checkbox: Pants strokes its own regions, which share Shirt's geometry
    public void The_grid_is_the_same_in_shirt_and_pants_and_absent_without_ShowGrid()
    {
        using var shirt = View(new Document(), new ViewOptions(1, 1, true));
        using var pants = View(new Document { Mode = TemplateMode.Pants }, new ViewOptions(1, 1, true));
        using var none = View(new Document(), new ViewOptions(1));
        Assert.Equal(shirt.Bytes, pants.Bytes);
        Assert.NotEqual(none.Bytes, shirt.Bytes);
        Assert.Equal(none.GetPixel(300, 40), shirt.GetPixel(300, 40)); // inside torso_up, off every outline
        Assert.NotEqual(none.GetPixel(231, 40), shirt.GetPixel(231, 40)); // torso_up's left edge, x = 231
    }

    [Fact] // the grid line is 1 CSS px: DeviceScale device pixels wide, centered on the scaled edge
    public void Grid_lines_are_device_scale_pixels_wide()
    {
        var doc = new Document { ShowMask = false, BgColor = "#000000" };
        using var plain = View(doc, new ViewOptions(1, 2));
        using var grid = View(doc, new ViewOptions(1, 2, true));
        // torso_up's left edge is x = 231, so 462 at scale 2; a 2 px line covers 461 and 462 fully.
        Assert.Equal(plain.GetPixel(460, 80), grid.GetPixel(460, 80));
        Assert.NotEqual(plain.GetPixel(461, 80), grid.GetPixel(461, 80));
        Assert.Equal(grid.GetPixel(461, 80), grid.GetPixel(462, 80));
        Assert.Equal(plain.GetPixel(463, 80), grid.GetPixel(463, 80));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Assert.Throws<ArgumentNullException>(() => Renderer.RenderView(surface.Canvas, null!, new ViewOptions(1)));
        Assert.Throws<ArgumentNullException>(() => Renderer.RenderView(surface.Canvas, new Document(), null!));
        Assert.Throws<ArgumentNullException>(() => Renderer.RenderComposite(null!, new Document(), true));
    }
}
