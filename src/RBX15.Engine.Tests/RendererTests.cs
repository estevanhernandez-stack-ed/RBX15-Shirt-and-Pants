using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class RendererTests
{
    static readonly SKColor Navy = new(0x19, 0x2e, 0x44);
    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255);

    static SKImage Solid(int w, int h, SKColor c)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) bmp.SetPixel(x, y, c);
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    static SKImage HalfRedHalfBlue()
    {
        var info = new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 20; x++) bmp.SetPixel(x, y, x < 10 ? Red : Blue);
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    static Document With(params Layer[] layers) => new() { Layers = layers.ToImmutableList() };

    [Fact]
    public void Empty_document_is_navy_inside_regions_and_clear_outside()
    {
        var img = Renderer.Export(new Document());
        Assert.Equal((585, 559), (img.Width, img.Height));
        Assert.Equal(Navy, Px.At(img, 295, 138));
        Assert.Equal(0, Px.At(img, 230, 138).Alpha);
        Assert.Equal(0, Px.At(img, 0, 0).Alpha);
    }

    [Fact]
    public void Export_masks_even_when_show_mask_is_off() =>
        Assert.Equal(0, Px.At(Renderer.Export(new Document { ShowMask = false }), 230, 138).Alpha);

    [Fact]
    public void Background_color_edge_cases_match_canvas()
    {
        Assert.Equal(0, Px.At(Renderer.Export(new Document { BgColor = "transparent" }), 295, 138).Alpha);
        Assert.Equal(0, Px.At(Renderer.Export(new Document { BgColor = "" }), 295, 138).Alpha);
        Assert.Equal(new SKColor(0, 0, 0), Px.At(Renderer.Export(new Document { BgColor = "not-a-color" }), 295, 138));
        Assert.InRange(Px.At(Renderer.Export(new Document { BgOpacity = 0.5 }), 295, 138).Alpha, 127, 128);
    }

    [Fact]
    public void Region_fills_paint_over_the_background()
    {
        var doc = new Document { RegionFills = ImmutableList.Create(("torso_front", Fill.Solid("#ff0000"))) };
        Assert.Equal(Red, Px.At(Renderer.Export(doc), 295, 138));
        Assert.Equal(Navy, Px.At(Renderer.Export(doc), 200, 138));
    }

    [Fact]
    public void Layers_draw_at_their_position()
    {
        var img = Renderer.Export(With(new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74 }));
        Assert.Equal(Red, Px.At(img, 235, 78));
        Assert.Equal(Navy, Px.At(img, 245, 78));
    }

    [Fact]
    public void Hidden_and_zero_opacity_layers_draw_nothing()
    {
        Assert.Equal(Navy, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74, Visible = false })), 235, 78));
        Assert.Equal(Navy, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74, Opacity = 0 })), 235, 78));
    }

    [Fact]
    public void Later_layers_draw_on_top()
    {
        var img = Renderer.Export(With(
            new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74 },
            new Layer { Id = 2, Image = Solid(10, 10, Blue), X = 231, Y = 74 }));
        Assert.Equal(Blue, Px.At(img, 235, 78));
    }

    [Fact]
    public void FlipH_mirrors_the_layer()
    {
        Assert.Equal(Red, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 231, Y = 74 })), 233, 79));
        Assert.Equal(Blue, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 231, Y = 74, FlipH = true })), 233, 79));
    }

    [Fact]
    public void Rotation_wraps_naturally()
    {
        var at5 = ImageDiff.FromImage(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 260, Y = 120, Scale = 2, Rotation = 5 })));
        var at725 = ImageDiff.FromImage(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 260, Y = 120, Scale = 2, Rotation = 725 })));
        Assert.True(ImageDiff.Compare(at5, at725).Pass);
    }

    [Fact]
    public void Export_png_decodes_to_the_template_size()
    {
        using var decoded = SKBitmap.Decode(Renderer.ExportPng(new Document()));
        Assert.Equal((585, 559), (decoded.Width, decoded.Height));
    }

    [Theory]
    [InlineData(null, "roblox_shirt_roblox.png")]
    [InlineData("", "roblox_shirt_roblox.png")]
    [InlineData("My Shirt!", "My_Shirt__roblox.png")]
    [InlineData("  ", "___roblox.png")]
    [InlineData("a-b_c9", "a-b_c9_roblox.png")]
    [InlineData("\U0001F600", "___roblox.png")]
    public void Png_file_name_matches_4_1(string? title, string expected) => Assert.Equal(expected, ExportNaming.PngFileName(title));

    [Fact]
    public void Project_file_name_matches_4_1()
    {
        Assert.Equal("untitled.r15proj", ExportNaming.ProjectFileName(""));
        Assert.Equal("My_Shirt_.r15proj", ExportNaming.ProjectFileName("My Shirt!"));
    }

    [Fact]
    public void Decode_cache_prescales_only_at_mip_level_one_or_more()
    {
        Assert.Equal((99, 63), Renderer.DecodeCacheSize(198, 125, 0.5, 0.5));           // full-scene word art: level 1, ceil sizes
        Assert.Null(Renderer.DecodeCacheSize(390, 200, 0.6, 0.6));                       // level 0 draws the original
        Assert.Null(Renderer.DecodeCacheSize(390, 200, 1.5, 2.795));                     // upscale
        Assert.Equal((1000, 750), Renderer.DecodeCacheSize(4000, 3000, 585 / 4000.0, 559 / 3000.0)); // big background photo: level 2
        Assert.Null(Renderer.DecodeCacheSize(1920, 1080, 585 / 1920.0, 559 / 1080.0));   // one axis under 2x keeps the original
    }

    /// <summary>
    /// Pins the bounded uncertainty in Images.SkiaMipLevel. Two goldens fit the geometric-mean rule, and
    /// the largest-axis rule fits them too. On a cc level-n target whose source is at least 2^n on both
    /// axes, both axis scales lie in [2^-n, 2^(1-n)), so the two rules pick the same level. They part only
    /// where a short axis has been clamped to 1 pixel, which no golden exercises.
    /// </summary>
    [Fact]
    public void Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n()
    {
        static int LargestAxis(int sw, int sh, int w, int h)
        {
            double s = Math.Max((double)w / sw, (double)h / sh);
            return s >= 1 ? 0 : (int)Math.Floor(Math.Log2(1 / s));
        }
        int checkedTargets = 0;
        for (int sw = 1; sw <= 128; sw++)
            for (int sh = 1; sh <= 128; sh++)
                for (int n = 1; (1 << n) <= Math.Min(sw, sh); n++)
                {
                    int w = (sw + (1 << n) - 1) >> n, h = (sh + (1 << n) - 1) >> n;   // cc's ceil-halved target
                    Assert.True(Images.SkiaMipLevel(sw, sh, w, h) == LargestAxis(sw, sh, w, h), $"{sw}x{sh} at cc level {n}");
                    checkedTargets++;
                }
        Assert.Equal(72799, checkedTargets);
        Assert.Equal(1, Images.SkiaMipLevel(198, 125, 50, 32));   // gap-layer-mip2: cc level 2, Skia mip level 1
        Assert.Equal(0, Images.SkiaMipLevel(198, 125, 99, 63));    // full-scene: cc level 1, Skia mip level 0
        Assert.Equal(1, Images.SkiaMipLevel(2160, 2160, 1080, 1080)); // gap-bg-image-prescale: cc level 1, Skia mip level 1
        Assert.Equal(2, Images.SkiaMipLevel(3, 6, 1, 1));          // outside the bound: largest-axis would pick 1
        Assert.Equal(1, LargestAxis(3, 6, 1, 1));
    }

    /// <summary>
    /// Pins Renderer.FloatPipeline. A rotated draw is declined by Skia's legacy blitters, so it
    /// composites in the float pipeline in Chromium (G = 224*0.4 + 46*0.6 = 117.2, 117) but in 8-bit
    /// lowp in SkiaSharp without the no-op color filter (118). If a Skia upgrade drops the trick, this fails.
    /// </summary>
    [Fact]
    public void Rotated_layer_composites_in_float_not_lowp()
    {
        var info = new SKImageInfo(200, 200, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(0, 224, 0));
        var layer = new Layer { Id = 1, Image = Solid(100, 100, new SKColor(0, 46, 0)), X = 50, Y = 50, Rotation = 45, Opacity = 0.6 };
        Renderer.DrawLayer(surface.Canvas, layer);
        using var result = surface.Snapshot();
        Assert.Equal(new SKColor(0, 117, 0), Px.At(result, 100, 100));
    }
}
