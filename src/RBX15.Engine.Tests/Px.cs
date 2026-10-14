using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>Pixel helpers. Reads are un-premultiplied, like canvas getImageData.</summary>
static class Px
{
    public static SKColor At(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("ReadPixels failed");
        return bmp.GetPixel(x, y);
    }

    public static SKImage Render(int w, int h, Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        draw(surface.Canvas);
        return surface.Snapshot();
    }

    public static SKImage Solid(int w, int h, SKColor c) => Render(w, h, canvas => canvas.Clear(c));

    public static void Near(SKColor expected, SKColor actual, int tolerance = 1)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance &&
                    Math.Abs(expected.Blue - actual.Blue) <= tolerance && Math.Abs(expected.Alpha - actual.Alpha) <= tolerance,
                    $"expected {expected} ±{tolerance}, got {actual}");
    }
}
