using SkiaSharp;

namespace RBX15.Engine.Tests;

public class AdjustTests
{
    static SKColor Through(SKColorFilter f, SKColor c) =>
        Px.At(Px.Render(1, 1, canvas => { using var p = new SKPaint { Color = c, ColorFilter = f }; canvas.DrawRect(0, 0, 1, 1, p); }), 0, 0);

    static SKImage Raster(params SKColor[] pixels)
    {
        var info = new SKImageInfo(pixels.Length, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int i = 0; i < pixels.Length; i++) bmp.SetPixel(i, 0, pixels[i]);
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    [Fact]
    public void Brightness_scales_rgb() =>
        Px.Near(new SKColor(150, 150, 150), Through(Adjust.FilterChain(150, 100, 100, 0), new SKColor(100, 100, 100)));

    [Fact]
    public void Contrast_zero_is_mid_gray() =>
        Px.Near(new SKColor(128, 128, 128), Through(Adjust.FilterChain(100, 0, 100, 0), new SKColor(10, 200, 90)));

    [Fact]
    public void Saturate_zero_is_spec_luma() =>
        Px.Near(new SKColor(54, 54, 54), Through(Adjust.FilterChain(100, 100, 0, 0), new SKColor(255, 0, 0)));

    [Fact]
    public void Hue_rotate_180_uses_the_spec_matrix() =>
        Px.Near(new SKColor(0, 109, 109), Through(Adjust.FilterChain(100, 100, 100, 180), new SKColor(255, 0, 0)));

    [Fact]
    public void Each_stage_clamps_before_the_next()
    {
        // brightness(200%) on 200 -> 400 clamps to 255; contrast(50%) -> 0.5*1 + 0.25 = 0.75 -> 191.
        // Without the clamp it would come out 255.
        Px.Near(new SKColor(191, 191, 191), Through(Adjust.FilterChain(200, 50, 100, 0), new SKColor(200, 200, 200)));
    }

    [Fact]
    public void Filters_leave_alpha_alone() =>
        Assert.InRange(Through(Adjust.FilterChain(200, 100, 100, 0), new SKColor(100, 100, 100, 128)).Alpha, (byte)127, (byte)129);

    [Fact]
    public void Rgb_only_layers_count_as_adjusted()
    {
        var img = Raster(SKColors.Red);
        Assert.False(Adjust.HasAdjustments(new Layer { Id = 1, Image = img }));
        Assert.True(Adjust.HasAdjustments(new Layer { Id = 1, Image = img, GShift = -1 }));
        Assert.True(Adjust.HasAdjustments(new Layer { Id = 1, Image = img, HueRotate = 10 }));
    }

    [Fact]
    public void Rgb_shift_is_exact_clamped_and_skips_transparent_pixels()
    {
        var src = Raster(new SKColor(200, 100, 50, 3), new SKColor(10, 10, 10, 0));
        var shifted = Adjust.ShiftRgb(src, 10, -20, 250);
        Assert.Equal(new SKColor(210, 80, 255, 3), Px.At(shifted, 0, 0));
        Assert.Equal(0, Px.At(shifted, 1, 0).Alpha);
    }

    [Fact]
    public void Rgb_shift_is_cached_per_image_and_values()
    {
        var src = Raster(new SKColor(1, 2, 3));
        var a = Adjust.ShiftRgb(src, 5, 5, 5);
        Assert.Same(a, Adjust.ShiftRgb(src, 5, 5, 5));
        Assert.NotSame(a, Adjust.ShiftRgb(src, 6, 5, 5));
    }
}
