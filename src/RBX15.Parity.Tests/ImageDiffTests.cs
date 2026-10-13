using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Parity.Tests;

public class ImageDiffTests
{
    static SKBitmap Flat(int w, int h, SKColor c)
    {
        var b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                b.SetPixel(x, y, c);
        return b;
    }

    [Fact]
    public void Identical_images_pass()
    {
        var r = ImageDiff.Compare(Flat(8, 8, SKColors.Red), Flat(8, 8, SKColors.Red));
        Assert.True(r.Pass);
        Assert.Equal(0, r.MaxDelta);
    }

    [Fact]
    public void One_changed_pixel_inside_a_flat_area_fails_as_interior()
    {
        var cand = Flat(8, 8, SKColors.Red);
        cand.SetPixel(4, 4, new SKColor(254, 0, 0));
        var r = ImageDiff.Compare(Flat(8, 8, SKColors.Red), cand);
        Assert.False(r.Pass);
        Assert.Equal(1, r.InteriorDifferences);
        Assert.Equal(1, r.MaxDelta);
    }

    [Fact]
    public void Small_delta_on_an_edge_passes_and_large_delta_fails()
    {
        var reference = Flat(8, 8, SKColors.Red);
        for (int y = 0; y < 8; y++) reference.SetPixel(4, y, SKColors.Blue);   // a vertical edge at x=4

        var small = reference.Copy();
        small.SetPixel(4, 4, new SKColor(0, 0, 252));                          // delta 3 on the edge
        Assert.True(ImageDiff.Compare(reference, small).Pass);

        var large = reference.Copy();
        large.SetPixel(4, 4, new SKColor(0, 0, 200));                          // delta 55 on the edge
        var r = ImageDiff.Compare(reference, large);
        Assert.False(r.Pass);
        Assert.Equal(0, r.InteriorDifferences);
        Assert.Equal(1, r.EdgeOverTolerance);
    }

    [Fact]
    public void Transparent_pixels_with_different_rgb_are_equal()
    {
        var r = ImageDiff.Compare(Flat(6, 6, new SKColor(255, 0, 0, 0)), Flat(6, 6, new SKColor(0, 255, 0, 0)));
        Assert.True(r.Pass);
        Assert.Equal(0, r.DifferingPixels);
    }

    [Fact]
    public void Size_mismatch_fails()
    {
        var r = ImageDiff.Compare(Flat(8, 8, SKColors.Red), Flat(8, 9, SKColors.Red));
        Assert.False(r.SameSize);
        Assert.False(r.Pass);
    }

    [Fact]
    public void Border_pixels_are_never_interior()
    {
        var cand = Flat(8, 8, SKColors.Red);
        cand.SetPixel(0, 0, new SKColor(250, 0, 0));
        var r = ImageDiff.Compare(Flat(8, 8, SKColors.Red), cand);
        Assert.Equal(0, r.InteriorDifferences);
        Assert.Equal(1, r.EdgeOverTolerance);
    }

    [Fact]
    public void Heatmap_is_white_where_equal_and_red_where_different()
    {
        var cand = Flat(8, 8, SKColors.Red);
        cand.SetPixel(4, 4, SKColors.Blue);
        using var heat = ImageDiff.Heatmap(Flat(8, 8, SKColors.Red), cand);
        Assert.Equal(SKColors.White, heat.GetPixel(0, 0));
        var hot = heat.GetPixel(4, 4);
        Assert.Equal(255, hot.Red);
        Assert.True(hot.Green < 255);
    }
}
