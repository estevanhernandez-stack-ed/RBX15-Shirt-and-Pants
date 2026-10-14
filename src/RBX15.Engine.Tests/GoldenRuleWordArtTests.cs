using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The Word Art rule (GoldenRule.CompareWordArt, ruling R2): interior pixels within 1 level on every
/// channel, edge pixels unbounded, sizes equal. Synthetic 5x5 bitmaps: every border pixel is an edge,
/// and the center (2, 2) of a flat bitmap is interior.
/// </summary>
public class GoldenRuleWordArtTests
{
    static readonly SKColor Flat = new(23, 212, 250, 255);

    static SKBitmap Bitmap(int width = 5, int height = 5)
    {
        var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bmp.SetPixel(x, y, Flat);
        return bmp;
    }

    [Fact]
    public void An_interior_pixel_one_level_off_passes()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap();
        candidate.SetPixel(2, 2, new SKColor(22, 212, 250, 255));
        var result = GoldenRule.CompareWordArt(golden, candidate);
        Assert.Equal(new GoldenRule.WordArtResult(true, 1, 0, 0), result);
        Assert.True(result.Pass);
    }

    [Fact]
    public void An_interior_pixel_two_levels_off_fails_and_writes_a_heatmap()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap();
        candidate.SetPixel(2, 2, new SKColor(23, 212, 250, 253));
        Assert.Equal(new GoldenRule.WordArtResult(true, 2, 1, 0), GoldenRule.CompareWordArt(golden, candidate));
        var heatmap = Path.Combine(Path.GetTempPath(), "rbx15-wordart-rule-" + Guid.NewGuid().ToString("N"), "heat.png");
        try
        {
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => GoldenRule.AssertWordArtBitmaps("synthetic", golden, candidate, heatmap));
            Assert.True(File.Exists(heatmap));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(heatmap)!, true);
        }
    }

    [Fact]
    public void Edge_pixels_are_not_bounded()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap();
        candidate.SetPixel(0, 0, new SKColor(255, 0, 0, 255));
        var result = GoldenRule.CompareWordArt(golden, candidate);
        Assert.Equal(new GoldenRule.WordArtResult(true, 0, 0, 250), result);
        Assert.True(result.Pass);
    }

    [Fact]
    public void A_size_mismatch_fails()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap(5, 4);
        Assert.False(GoldenRule.CompareWordArt(golden, candidate).Pass);
    }
}
