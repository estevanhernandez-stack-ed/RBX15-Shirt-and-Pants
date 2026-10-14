using System.Runtime.CompilerServices;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// Layer images are held exactly as the file says (un-premultiplied), so RGB shift is exact
/// (ledger rgb-shift-compute). Drawing goes through a premultiplied copy, because Chromium decodes
/// img elements to premultiplied pixels and canvas sampling filters those. Same input, same edges.
/// </summary>
public static class Images
{
    static readonly ConditionalWeakTable<SKImage, SKImage> Premultiplied = new();

    public static SKImage Decode(byte[] encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("not a supported image");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException($"image decode failed: {result}");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    public static SKImage ForDrawing(SKImage image) =>
        image.AlphaType == SKAlphaType.Premul ? image : Premultiplied.GetValue(image, ToPremul);

    static SKImage ToPremul(SKImage source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!source.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("premultiply failed");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }
}
