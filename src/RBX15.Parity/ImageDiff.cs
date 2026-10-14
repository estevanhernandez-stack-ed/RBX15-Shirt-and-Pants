using SkiaSharp;

namespace RBX15.Parity;

public sealed record DiffResult(bool SameSize, int MaxDelta, int DifferingPixels, int InteriorDifferences,
                                int EdgeOverTolerance, bool Pass);

/// <summary>
/// The v5 golden rule (spec section 6): zero differences on interior pixels, where a pixel is interior
/// when its reference 3x3 neighborhood is one flat color, and edge deltas of at most
/// <c>edgeTolerance</c>. Delta is the largest absolute RGBA channel difference on un-premultiplied
/// values. Two fully transparent pixels are equal whatever RGB they store.
/// </summary>
public static class ImageDiff
{
    /// <summary>Reads any SKImage (for example a premultiplied engine render) as an un-premultiplied
    /// RGBA bitmap, the same conversion canvas.toDataURL performs before encoding.</summary>
    public static SKBitmap FromImage(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
        {
            bmp.Dispose();
            throw new InvalidOperationException("cannot read image pixels");
        }
        return bmp;
    }

    /// <summary>
    /// Decodes straight into un-premultiplied RGBA. <c>SKBitmap.Decode</c> forces premultiplied
    /// storage, which quantizes low-alpha pixels (the antialiased edges goldens care about most).
    /// </summary>
    public static SKBitmap Load(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidOperationException($"cannot decode {path}");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw new InvalidOperationException($"cannot decode {path}: {result}");
        }
        return bitmap;
    }

    static SKBitmap Normalize(SKBitmap b)
    {
        var info = new SKImageInfo(b.Width, b.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if (b.Info.ColorType == info.ColorType && b.Info.AlphaType == info.AlphaType) return b.Copy();
        var dst = new SKBitmap(info);
        if (!b.CopyTo(dst, SKColorType.Rgba8888)) throw new InvalidOperationException("pixel conversion failed");
        return dst;
    }

    static int Delta(SKColor a, SKColor b)
    {
        if (a.Alpha == 0 && b.Alpha == 0) return 0;
        return Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)),
                        Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));
    }

    static bool IsInterior(SKBitmap reference, int x, int y)
    {
        if (x == 0 || y == 0 || x == reference.Width - 1 || y == reference.Height - 1) return false;
        var center = reference.GetPixel(x, y);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (Delta(reference.GetPixel(x + dx, y + dy), center) != 0) return false;
        return true;
    }

    public static DiffResult Compare(SKBitmap reference, SKBitmap candidate, int edgeTolerance = 3)
    {
        if (reference.Width != candidate.Width || reference.Height != candidate.Height)
            return new DiffResult(false, 255, -1, -1, -1, false);
        using var a = Normalize(reference);
        using var b = Normalize(candidate);
        int max = 0, differing = 0, interior = 0, edgeOver = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                int d = Delta(a.GetPixel(x, y), b.GetPixel(x, y));
                if (d == 0) continue;
                differing++;
                max = Math.Max(max, d);
                if (IsInterior(a, x, y)) interior++;
                else if (d > edgeTolerance) edgeOver++;
            }
        return new DiffResult(true, max, differing, interior, edgeOver, interior == 0 && edgeOver == 0);
    }

    public static SKBitmap Heatmap(SKBitmap reference, SKBitmap candidate)
    {
        using var a = Normalize(reference);
        using var b = Normalize(candidate);
        var heat = new SKBitmap(new SKImageInfo(a.Width, a.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                int d = Delta(a.GetPixel(x, y), b.GetPixel(x, y));
                byte v = (byte)Math.Min(255, d * 16);
                heat.SetPixel(x, y, d == 0 ? SKColors.White : new SKColor(255, (byte)(255 - v), (byte)(255 - v)));
            }
        return heat;
    }
}
