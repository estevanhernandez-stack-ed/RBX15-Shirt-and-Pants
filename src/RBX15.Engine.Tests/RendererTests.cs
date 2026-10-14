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
}
