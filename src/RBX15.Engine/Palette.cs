using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The colors of the user's art (ledger <c>art-palette</c>, new). Each image is sampled down to at most <see cref="MaxSide"/> px
/// on its long side and its opaque pixels are binned at 4 bits per channel; the busiest bins, by their average color,
/// become the palette, skipping a color that sits within <see cref="MergeDistance"/> of one already taken and a bin under
/// <see cref="MinShare"/> of the counted pixels. Deterministic: the same images always give the same list. WPF-free.
/// </summary>
public static class Palette
{
    /// <summary>The longest side an image is sampled at, in px. Enough to find the dominant colors, cheap on a 585x559 layer.</summary>
    public const int MaxSide = 64;

    /// <summary>A pixel under this alpha is ignored: it is mostly the checkerboard showing through.</summary>
    public const int MinAlpha = 128;

    /// <summary>Two colors this close (Euclidean RGB, 0 to 441) read as one, so a gradient does not fill the palette with its steps.</summary>
    public const double MergeDistance = 48;

    /// <summary>A bin holding less than this share of the counted pixels is noise (an anti-aliased edge), not a color of the art.</summary>
    public const double MinShare = 0.005;

    /// <summary>The colors of the visible layers' images, bottom to top (the order of <see cref="Document.Layers"/>).</summary>
    public static IReadOnlyList<string> FromDocument(Document doc, int max = 12)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Extract(doc.Layers.Where(l => l.Visible).Select(l => l.Image), max);
    }

    /// <summary>
    /// Up to <paramref name="max"/> lowercase <c>#rrggbb</c> colors, the most used first. Empty for no images, no opaque
    /// pixel, or a <paramref name="max"/> of 0 or less.
    /// </summary>
    public static IReadOnlyList<string> Extract(IEnumerable<SKImage> images, int max = 12)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (max <= 0) return Array.Empty<string>();

        var count = new long[4096];
        var sum = new long[4096][];
        long counted = 0;
        foreach (var image in images)
            counted += Tally(image, count, sum);
        if (counted == 0) return Array.Empty<string>();

        var order = Enumerable.Range(0, count.Length).Where(i => count[i] > 0 && count[i] >= counted * MinShare)
            .OrderByDescending(i => count[i]).ThenBy(i => i);
        var taken = new List<(int R, int G, int B)>();
        foreach (int bin in order)
        {
            var s = sum[bin];
            var color = ((int)((s[0] + count[bin] / 2) / count[bin]), (int)((s[1] + count[bin] / 2) / count[bin]), (int)((s[2] + count[bin] / 2) / count[bin]));
            if (taken.Any(t => Distance(t, color) <= MergeDistance)) continue;
            taken.Add(color);
            if (taken.Count == max) break;
        }
        return taken.Select(c => $"#{c.R:x2}{c.G:x2}{c.B:x2}").ToArray();
    }

    static double Distance((int R, int G, int B) a, (int R, int G, int B) b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>Adds one image's opaque pixels to the bins; returns how many it counted.</summary>
    static long Tally(SKImage image, long[] count, long[][] sum)
    {
        double scale = Math.Min(1, (double)MaxSide / Math.Max(image.Width, image.Height));
        int w = Math.Max(1, (int)Math.Round(image.Width * scale)), h = Math.Max(1, (int)Math.Round(image.Height * scale));
        var unpremul = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(unpremul);
        if (scale == 1)
        {
            // Small enough as it is: read the pixels as stored, no resampling.
            if (!image.ReadPixels(unpremul, bitmap.GetPixels(), bitmap.RowBytes, 0, 0)) return 0;
        }
        else
        {
            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            // Nearest, not smoothed: averaging across a hard edge would invent a color the art does not contain.
            surface.Canvas.DrawImage(image, new SKRect(0, 0, w, h), new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), null);
            using var small = surface.Snapshot();
            if (!small.ReadPixels(unpremul, bitmap.GetPixels(), bitmap.RowBytes, 0, 0)) return 0;
        }

        long counted = 0;
        var pixels = bitmap.GetPixelSpan();
        for (int i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i + 3] < MinAlpha) continue;
            int r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
            int bin = (r >> 4) << 8 | (g >> 4) << 4 | b >> 4;
            count[bin]++;
            var s = sum[bin] ??= new long[3];
            s[0] += r;
            s[1] += g;
            s[2] += b;
            counted++;
        }
        return counted;
    }
}
