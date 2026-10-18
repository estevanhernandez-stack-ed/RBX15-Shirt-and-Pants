using System.Collections.Immutable;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>The palette pulled from the user's art (Palette; ledger art-palette, new).</summary>
public class PaletteTests
{
    /// <summary>An image of <paramref name="w"/> x <paramref name="h"/> whose pixel colors come from <paramref name="at"/>.</summary>
    static SKImage Image(int w, int h, Func<int, int, SKColor> at)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bitmap.SetPixel(x, y, at(x, y));
        return SKImage.FromBitmap(bitmap);
    }

    static SKImage Solid(SKColor color, int w = 10, int h = 10) => Image(w, h, (_, _) => color);

    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255);

    [Fact]
    public void A_solid_image_gives_its_one_color()
    {
        using var image = Solid(Red);
        Assert.Equal(new[] { "#ff0000" }, Palette.Extract(new[] { image }));
    }

    [Fact] // the busiest color leads
    public void Two_colors_come_most_used_first()
    {
        using var image = Image(10, 10, (x, _) => x < 3 ? Blue : Red);   // 30% blue, 70% red
        Assert.Equal(new[] { "#ff0000", "#0000ff" }, Palette.Extract(new[] { image }));
        using var flipped = Image(10, 10, (x, _) => x < 7 ? Blue : Red);
        Assert.Equal(new[] { "#0000ff", "#ff0000" }, Palette.Extract(new[] { flipped }));
    }

    [Fact]
    public void A_75_25_split_gives_both_with_the_75_first()
    {
        using var image = Image(8, 8, (_, y) => y < 6 ? Blue : Red);
        Assert.Equal(new[] { "#0000ff", "#ff0000" }, Palette.Extract(new[] { image }));
    }

    [Fact] // 16 apart in one channel: different bins, one color to the eye
    public void Near_identical_colors_merge_into_the_busier()
    {
        using var image = Image(10, 10, (x, _) => x < 6 ? new SKColor(0xff, 0, 0) : new SKColor(0xef, 0, 0));
        Assert.Equal(new[] { "#ff0000" }, Palette.Extract(new[] { image }));
    }

    [Fact] // a bin averages its pixels, so a shade within a bin reports its middle, not its first pixel
    public void A_bin_reports_the_average_of_its_pixels()
    {
        using var image = Image(10, 10, (x, _) => x % 2 == 0 ? new SKColor(0x20, 0x40, 0x60) : new SKColor(0x2e, 0x4e, 0x6e));
        Assert.Equal(new[] { "#274767" }, Palette.Extract(new[] { image }));
    }

    [Fact]
    public void Transparent_pixels_are_ignored()
    {
        using var image = Image(10, 10, (x, _) => x < 1 ? Red : SKColors.Transparent);
        Assert.Equal(new[] { "#ff0000" }, Palette.Extract(new[] { image }));
        using var faint = Image(10, 10, (_, _) => new SKColor(255, 0, 0, 100));   // under half opaque
        Assert.Empty(Palette.Extract(new[] { faint }));
    }

    [Fact] // a speck under half a percent is edge noise
    public void A_color_under_half_a_percent_is_skipped()
    {
        using var image = Image(30, 30, (x, y) => x == 0 && y < 3 ? Blue : Red);   // 3 of 900 pixels
        Assert.Equal(new[] { "#ff0000" }, Palette.Extract(new[] { image }));
    }

    [Fact]
    public void Max_caps_the_count_and_keeps_the_busiest()
    {
        using var image = Image(12, 1, (x, _) => x switch { < 6 => Red, < 9 => Blue, < 11 => new SKColor(0, 255, 0), _ => new SKColor(255, 255, 0) });
        Assert.Equal(new[] { "#ff0000", "#0000ff", "#00ff00", "#ffff00" }, Palette.Extract(new[] { image }));
        Assert.Equal(new[] { "#ff0000", "#0000ff" }, Palette.Extract(new[] { image }, max: 2));
        Assert.Empty(Palette.Extract(new[] { image }, max: 0));
    }

    [Fact]
    public void No_images_or_only_transparent_ones_give_an_empty_list()
    {
        Assert.Empty(Palette.Extract(Array.Empty<SKImage>()));
        using var clear = Solid(SKColors.Transparent);
        Assert.Empty(Palette.Extract(new[] { clear }));
        Assert.Empty(Palette.FromDocument(new Document()));
    }

    [Fact]
    public void The_same_input_twice_gives_the_same_list()
    {
        using var image = Image(100, 80, (x, y) => new SKColor((byte)(x * 2), (byte)(y * 3), (byte)(x + y)));   // scaled down, many bins
        var first = Palette.Extract(new[] { image });
        Assert.NotEmpty(first);
        Assert.Equal(first, Palette.Extract(new[] { image }));
    }

    [Fact] // a big layer is sampled small and still finds its dominant colors
    public void A_large_image_is_sampled_down()
    {
        using var image = Image(585, 559, (x, _) => x < 400 ? Red : Blue);
        Assert.Equal(new[] { "#ff0000", "#0000ff" }, Palette.Extract(new[] { image }));
    }

    [Fact] // the images' pixels pool: the color most used across all of them leads
    public void Several_images_pool_their_pixels()
    {
        using var a = Solid(Red, 4, 4);
        using var b = Solid(Blue, 10, 10);
        Assert.Equal(new[] { "#0000ff", "#ff0000" }, Palette.Extract(new[] { a, b }));
    }

    [Fact]
    public void FromDocument_skips_hidden_layers()
    {
        using var red = Solid(Red);
        using var blue = Solid(Blue);
        var doc = new Document
        {
            Layers = ImmutableList.Create(
                new Layer { Id = 1, Image = red },
                new Layer { Id = 2, Image = blue, Visible = false }),
        };
        Assert.Equal(new[] { "#ff0000" }, Palette.FromDocument(doc));
        Assert.Equal(new[] { "#0000ff" }, Palette.FromDocument(doc with { Layers = doc.Layers.SetItem(1, doc.Layers[1] with { Visible = true }).SetItem(0, doc.Layers[0] with { Visible = false }) }));
    }
}
