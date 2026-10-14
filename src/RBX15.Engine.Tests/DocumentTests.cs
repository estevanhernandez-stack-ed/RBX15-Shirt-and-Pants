using SkiaSharp;

namespace RBX15.Engine.Tests;

public class DocumentTests
{
    static byte[] Png(SKColor c, int w = 1, int h = 1)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) bmp.SetPixel(x, y, c);
        using var img = SKImage.FromPixelCopy(bmp.PeekPixels());
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void Document_defaults_match_the_4_1_editor_state()
    {
        var d = new Document();
        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#192e44", d.BgColor);
        Assert.Equal(1, d.BgOpacity);
        Assert.Null(d.BgImage);
        Assert.Empty(d.RegionFills);
        Assert.Empty(d.Layers);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
    }

    [Fact]
    public void Layer_defaults_match_makeLayer()
    {
        var l = new Layer { Id = 1, Image = Images.Decode(Png(SKColors.Red)) };
        Assert.Equal("Layer", l.Name);
        Assert.True(l.Visible);
        Assert.Equal((1.0, 0.0, 0.0, 1.0, 0.0), (l.Opacity, l.X, l.Y, l.Scale, l.Rotation));
        Assert.False(l.FlipH || l.FlipV);
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate));
        Assert.Equal((0, 0, 0), (l.RShift, l.GShift, l.BShift));
    }

    [Fact]
    public void Decode_keeps_low_alpha_pixels_exact()
    {
        var img = Images.Decode(Png(new SKColor(200, 100, 50, 3)));
        Assert.Equal(SKAlphaType.Unpremul, img.AlphaType);
        Assert.Equal(new SKColor(200, 100, 50, 3), Px.At(img, 0, 0));
    }

    [Fact]
    public void Decode_rejects_non_images() =>
        Assert.Throws<InvalidDataException>(() => Images.Decode("not an image"u8.ToArray()));

    [Fact]
    public void ForDrawing_is_a_cached_premultiplied_copy()
    {
        var img = Images.Decode(Png(new SKColor(10, 20, 30, 128), 3, 2));
        var a = Images.ForDrawing(img);
        Assert.Equal(SKAlphaType.Premul, a.AlphaType);
        Assert.Equal((3, 2), (a.Width, a.Height));
        Assert.Same(a, Images.ForDrawing(img));
    }

    [Fact]
    public void With_expressions_share_the_bitmap()
    {
        var l = new Layer { Id = 1, Image = Images.Decode(Png(SKColors.Red)) };
        Assert.Same(l.Image, (l with { Id = 2, Name = "copy" }).Image);
    }
}
