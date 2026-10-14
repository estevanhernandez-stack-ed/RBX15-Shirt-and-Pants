using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The golden rule (spec section 6) as both golden gates apply it: zero interior differences and
/// edge deltas of 3 or less. Scenes with an RGB shift (approved divergence rgb-shift-compute) are
/// held to the full rule on pixels whose golden alpha is 255, plus an alpha delta of at most 1
/// everywhere. A failing scene writes a heatmap (a CI artifact) before the assert.
/// </summary>
static class GoldenRule
{
    static SKBitmap OpaqueOnly(SKBitmap source, SKBitmap golden)
    {
        var copy = source.Copy();
        for (int y = 0; y < golden.Height; y++)
            for (int x = 0; x < golden.Width; x++)
                if (golden.GetPixel(x, y).Alpha < 255) copy.SetPixel(x, y, SKColors.Transparent);
        return copy;
    }

    public static void AssertMatches(string name, string goldenPath, Document document, string heatmapPath)
    {
        using var golden = ImageDiff.Load(goldenPath);
        using var rendered = Renderer.Export(document);
        using var candidate = ImageDiff.FromImage(rendered);

        bool rgbShift = document.Layers.Any(l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0);
        DiffResult result;
        if (rgbShift)
        {
            using var g = OpaqueOnly(golden, golden);
            using var c = OpaqueOnly(candidate, golden);
            result = ImageDiff.Compare(g, c);
            int worstAlpha = 0;
            for (int y = 0; y < golden.Height; y++)
                for (int x = 0; x < golden.Width; x++)
                    worstAlpha = Math.Max(worstAlpha, Math.Abs(golden.GetPixel(x, y).Alpha - candidate.GetPixel(x, y).Alpha));
            Assert.True(worstAlpha <= 1, $"{name}: RGB shift must never change alpha (worst alpha delta {worstAlpha})");
        }
        else
        {
            result = ImageDiff.Compare(golden, candidate);
        }

        if (!result.Pass)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(heatmapPath, data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }
}
