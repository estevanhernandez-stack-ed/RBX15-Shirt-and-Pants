using System.Globalization;
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

    /// <summary>Largest encoded file or payload <see cref="Decode"/> accepts: 100 MiB. Checked before any copy or parse.</summary>
    public const long MaxEncodedBytes = 100L * 1024 * 1024;

    /// <summary>Largest width or height <see cref="Decode"/> accepts: 16384, the common GPU texture limit.</summary>
    public const int MaxDimension = 16384;

    /// <summary>
    /// Largest pixel count <see cref="Decode"/> accepts: 50 million. A 48 MP phone photo (8000x6000)
    /// fits; at 4 bytes a pixel (200 MB) a plain decode holds the bitmap and its image copy (about 400 MB), an
    /// EXIF-rotated decode adds the oriented bitmap (about 600 MB peak), and the premultiplied drawing copy comes later.
    /// </summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>
    /// Throws <see cref="ImageTooLargeException"/> when an encoded payload is over
    /// <see cref="MaxEncodedBytes"/>. Import calls it with the file length before reading the file.
    /// </summary>
    public static void CheckEncodedSize(long bytes)
    {
        if (bytes > MaxEncodedBytes)
            throw new ImageTooLargeException(string.Create(CultureInfo.InvariantCulture,
                $"The file is {bytes / 1048576.0:0.#} MB; the limit is {MaxEncodedBytes / 1048576} MB."));
    }

    /// <summary>
    /// Throws <see cref="ImageTooLargeException"/> when either side is over <see cref="MaxDimension"/>
    /// or the pixel count is over <see cref="MaxPixels"/>.
    /// </summary>
    public static void CheckDimensions(int width, int height)
    {
        if (width > MaxDimension || height > MaxDimension)
            throw new ImageTooLargeException(string.Create(CultureInfo.InvariantCulture,
                $"The image is {width} x {height} pixels; the limit is {MaxDimension} pixels on a side."));
        if ((long)width * height > MaxPixels)
            throw new ImageTooLargeException(string.Create(CultureInfo.InvariantCulture,
                $"The image is {width} x {height} pixels ({(long)width * height / 1e6:0.#} megapixels); the limit is {MaxPixels / 1_000_000} megapixels."));
    }

    /// <summary>
    /// Decodes an encoded image (PNG, JPEG, WebP, BMP, GIF; the first frame of an animation) to
    /// un-premultiplied RGBA. Untrusted input: the payload size is checked first, then the
    /// dimensions from the codec header, both before any pixel memory is allocated (divergence
    /// <c>image-decode-caps</c>; 4.1 had no caps). An EXIF orientation is applied, as Chromium
    /// draws an img element (image-orientation: from-image), so the result has the displayed size.
    /// Throws <see cref="ImageTooLargeException"/> over a cap and <see cref="InvalidDataException"/>
    /// for bytes it cannot decode.
    /// </summary>
    public static SKImage Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        CheckEncodedSize(encoded.LongLength);
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("not a supported image");
        CheckDimensions(codec.Info.Width, codec.Info.Height);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException($"image decode failed: {result}");
        return codec.EncodedOrigin == SKEncodedOrigin.TopLeft
            ? SKImage.FromPixelCopy(bmp.PeekPixels())
            : Orient(bmp, codec.EncodedOrigin);
    }

    /// <summary>
    /// Applies an EXIF orientation as a pure pixel permutation, so the un-premultiplied values stay
    /// exact. Each case maps a displayed pixel (x, y) back to its stored pixel; 5 to 8 swap the sides.
    /// </summary>
    static SKImage Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        int w = source.Width, h = source.Height;
        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int dw = swap ? h : w, dh = swap ? w : h;
        using var dest = new SKBitmap(new SKImageInfo(dw, dh, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var srcMap = source.PeekPixels();
        using var dstMap = dest.PeekPixels();
        var src = srcMap.GetPixelSpan<uint>();
        var dst = dstMap.GetPixelSpan<uint>();
        int srcStride = srcMap.RowBytes / 4, dstStride = dstMap.RowBytes / 4;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                (int sx, int sy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (w - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (w - 1 - x, h - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, h - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (y, h - 1 - x),
                    SKEncodedOrigin.RightBottom => (w - 1 - y, h - 1 - x),
                    SKEncodedOrigin.LeftBottom => (w - 1 - y, x),
                    _ => (x, y),
                };
                dst[y * dstStride + x] = src[sy * srcStride + sx];
            }
        return SKImage.FromPixelCopy(dest.PeekPixels());
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

    /// <summary>
    /// Frees a layer or background image the editor no longer references (M4a interfaces R11: the outgoing
    /// document after Open). Disposes the image's RGB-shifted copies (<see cref="Adjust.ShiftRgb"/>) with their
    /// own derived entries, its premultiplied drawing copy (<see cref="ForDrawing"/>) unless that copy is the
    /// image itself, the decode-cache pre-scale keyed by either, and then the image. Idempotent; the caller
    /// guarantees nothing draws the image afterwards. UI thread.
    /// </summary>
    public static void Release(SKImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        foreach (var shifted in Adjust.TakeShifted(image)) Release(shifted);
        if (Premultiplied.TryGetValue(image, out var premul))
        {
            Premultiplied.Remove(image);
            ReleaseScaled(premul);
            if (!ReferenceEquals(premul, image)) premul.Dispose();
        }
        ReleaseScaled(image);
        image.Dispose();
    }

    static void ReleaseScaled(SKImage key)
    {
        if (!Scaled.TryGetValue(key, out var entry)) return;
        Scaled.Remove(key);
        if (!ReferenceEquals(entry.Result, key)) entry.Result.Dispose();
    }

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

/// <summary>
/// An image over one of the <see cref="Images"/> decode caps. <see cref="InvalidDataException"/> is
/// sealed, so this derives from <see cref="Exception"/>; every caller that drops undecodable images
/// (ProjectIO.TryDecodeDataUrl, the App's ImageImporter) catches both.
/// </summary>
public sealed class ImageTooLargeException(string message) : Exception(message);
