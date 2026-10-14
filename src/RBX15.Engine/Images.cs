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

    /// <summary>
    /// A premultiplied render as a stored layer image: un-premultiplied RGBA, the form 4.1's toDataURL +
    /// img decode leaves it in. The caller keeps ownership of <paramref name="premul"/>.
    /// </summary>
    internal static SKImage ToUnpremulLayerImage(SKImage premul)
    {
        var info = new SKImageInfo(premul.Width, premul.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!premul.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read layer pixels");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

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
    /// drawn downscaled to a cc level of 1 or more (cc SoftwareImageDecodeCacheUtils::CacheKey::FromDrawImage):
    /// the image pre-scaled to that level's size with SkPixmap::scalePixels at medium quality,
    /// bilinear with nearest mipmaps. Skia then samples from its own mip chain, picked from the
    /// pre-scale's geometric-mean scale and rounded down (<see cref="SkiaMipLevel"/>). Cached per
    /// image. Cache-owned; callers must not dispose.
    /// </summary>
    internal static SKImage DecodeCacheScaled(SKImage premul, int width, int height)
    {
        if (Scaled.TryGetValue(premul, out var hit) && hit.Width == width && hit.Height == height) return hit.Result;
        int level = SkiaMipLevel(premul.Width, premul.Height, width, height);
        var source = premul;
        var chain = new List<SKImage>();
        for (int i = 0; i < level && (source.Width > 1 || source.Height > 1); i++)
            chain.Add(source = ScalePixels(source, Math.Max(1, source.Width / 2), Math.Max(1, source.Height / 2), MipBuild));
        var result = ScalePixels(source, width, height, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        foreach (var mip in chain) mip.Dispose();
        Scaled.AddOrUpdate(premul, new ScaledEntry(width, height, result));
        return result;
    }

    /// <summary>
    /// The Skia mip level (not a cc decode-cache level) Chromium's Skia samples for a medium-quality
    /// draw at this size: log2 of the inverse geometric-mean scale, rounded down (Chromium builds Skia
    /// with SK_SUPPORT_LEGACY_ANISOTROPIC_MIPMAP_SCALE; SkiaSharp's Skia takes the smaller axis scale,
    /// which mips the full-scene word art at exactly 0.5 where 4.1 does not). 0 means no mipmap.
    /// Fitted: gap-layer-mip2 (198x125 to 50x32: cc level 2, Skia mip level 1) and full-scene
    /// (198x125 to 99x63: cc level 1, Skia mip level 0).
    /// <para>
    /// Bounded uncertainty: the largest-axis rule fits both goldens too. The two rules agree on every
    /// cc level-n target whose source is at least 2^n on both axes, because both axis scales then lie in
    /// [2^-n, 2^(1-n)). Measured on 2026-10-14, they also agree on every background pre-scale (sources up
    /// to 4096x4096 stretched to 585x559) and on every uniform-scale layer target whose shorter side is 2
    /// or more (sources up to 300x300). They differ only where a short axis is clamped to 1 pixel (3x6 to
    /// 1x1: geometric mean 2, largest axis 1), which no golden exercises. Pinned by
    /// Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n.
    /// </para>
    /// </summary>
    internal static int SkiaMipLevel(int srcWidth, int srcHeight, int width, int height)
    {
        double scale = Math.Sqrt((double)width / srcWidth * ((double)height / srcHeight));
        return scale >= 1 || scale <= 0 ? 0 : (int)Math.Floor(Math.Log2(1 / scale));
    }

    /// <summary>
    /// Builds one level of Skia's mip chain (floor-halved, Skia's own downsample filter): scalePixels
    /// to the halved size resolves to mip level 1 and copies it unscaled. Skia builds level n+1 from
    /// level n, so successive halvings reproduce its chain.
    /// </summary>
    static readonly SKSamplingOptions MipBuild = new(SKFilterMode.Linear, SKMipmapMode.Nearest);

    static SKImage ScalePixels(SKImage source, int width, int height, SKSamplingOptions sampling)
    {
        var info = new SKImageInfo(width, height, source.ColorType, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        using (var dst = bmp.PeekPixels())
            if (!source.ScalePixels(dst, sampling))
                throw new InvalidOperationException("decode-cache scale failed");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
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
