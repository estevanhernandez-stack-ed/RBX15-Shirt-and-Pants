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

    /// <summary>
    /// The premultiplied, platform-native (kN32) copy that drawing uses. kN32 matters: Skia only takes
    /// its legacy 8888 blitters (Chromium's software canvas path) for N32 sources onto N32 devices.
    /// Cache-owned; callers must not dispose.
    /// </summary>
    public static SKImage ForDrawing(SKImage image) =>
        image.AlphaType == SKAlphaType.Premul && image.ColorType == SKImageInfo.PlatformColorType
            ? image : Premultiplied.GetValue(image, ToPremul);

    sealed record ScaledEntry(int Width, int Height, SKImage Result);
    static readonly ConditionalWeakTable<SKImage, ScaledEntry> Scaled = new();

    /// <summary>
    /// The decode Chromium's software image decode cache hands the canvas when an img element is
    /// drawn downscaled to a mip level of 1 or more (cc SoftwareImageDecodeCacheUtils::CacheKey::FromDrawImage):
    /// the image pre-scaled to that level's size with SkPixmap::scalePixels at the draw's own
    /// quality, which for canvas 'low' smoothing is bilinear without mipmaps. Cached per image.
    /// Cache-owned; callers must not dispose.
    /// </summary>
    internal static SKImage DecodeCacheScaled(SKImage premul, int width, int height)
    {
        if (Scaled.TryGetValue(premul, out var hit) && hit.Width == width && hit.Height == height) return hit.Result;
        var info = new SKImageInfo(width, height, premul.ColorType, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        using (var dst = bmp.PeekPixels())
            if (!premul.ScalePixels(dst, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)))
                throw new InvalidOperationException("decode-cache scale failed");
        var result = SKImage.FromPixelCopy(bmp.PeekPixels());
        Scaled.AddOrUpdate(premul, new ScaledEntry(width, height, result));
        return result;
    }

    static SKImage ToPremul(SKImage source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!source.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("premultiply failed");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }
}
