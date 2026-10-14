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

    /// <summary>
    /// What the Word Art rule measured: whether the sizes match, the largest delta on interior pixels,
    /// how many interior pixels differ by more than 1, and the largest delta on edge pixels (reported,
    /// never bounded).
    /// </summary>
    internal sealed record WordArtResult(bool SameSize, int InteriorMaxDelta, int InteriorOverOne, int EdgeMaxDelta)
    {
        public bool Pass => SameSize && InteriorOverOne == 0;
    }

    /// <summary>
    /// The Word Art rule (ruling R2, 2026-10-14; divergence wordart-glyph-rasterization). Both images
    /// have the same size, and every interior pixel (its golden 3x3 neighborhood one flat color, as
    /// ImageDiff defines interior) differs by at most 1 level on every channel. Edge pixels are not
    /// bounded: Chromium rasterizes Word Art glyphs through DirectWrite and SkiaSharp does not, so the
    /// glyph edges differ. Layout and placement are held exactly by WordArtSwapTests, which swaps 4.1's
    /// own Word Art bitmap into the engine's document under the unchanged <see cref="AssertMatches"/>.
    /// Both bitmaps must be un-premultiplied RGBA, as ImageDiff.Load and ImageDiff.FromImage return them.
    /// </summary>
    internal static WordArtResult CompareWordArt(SKBitmap golden, SKBitmap candidate)
    {
        foreach (var b in new[] { golden, candidate })
            if (b.ColorType != SKColorType.Rgba8888 || b.AlphaType != SKAlphaType.Unpremul)
                throw new ArgumentException("the Word Art rule compares un-premultiplied RGBA bitmaps");
        if (golden.Width != candidate.Width || golden.Height != candidate.Height)
            return new WordArtResult(false, 255, -1, 255);

        // ImageDiff's delta and interior test, restated here because ImageDiff never changes.
        static int Delta(SKColor a, SKColor b) =>
            a.Alpha == 0 && b.Alpha == 0 ? 0
            : Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)),
                       Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));
        bool IsInterior(int x, int y)
        {
            if (x == 0 || y == 0 || x == golden.Width - 1 || y == golden.Height - 1) return false;
            var center = golden.GetPixel(x, y);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (Delta(golden.GetPixel(x + dx, y + dy), center) != 0) return false;
            return true;
        }

        int interiorMax = 0, interiorOverOne = 0, edgeMax = 0;
        for (int y = 0; y < golden.Height; y++)
            for (int x = 0; x < golden.Width; x++)
            {
                int d = Delta(golden.GetPixel(x, y), candidate.GetPixel(x, y));
                if (d == 0) continue;
                if (IsInterior(x, y))
                {
                    interiorMax = Math.Max(interiorMax, d);
                    if (d > 1) interiorOverOne++;
                }
                else edgeMax = Math.Max(edgeMax, d);
            }
        return new WordArtResult(true, interiorMax, interiorOverOne, edgeMax);
    }

    /// <summary>
    /// Asserts the Word Art rule on two bitmaps. A same-size failure writes a heatmap first, as
    /// <see cref="AssertMatches"/> does; a size mismatch has no per-pixel heatmap and fails on the size.
    /// </summary>
    internal static void AssertWordArtBitmaps(string name, SKBitmap golden, SKBitmap candidate, string heatmapPath)
    {
        var result = CompareWordArt(golden, candidate);
        if (!result.Pass && result.SameSize)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(heatmapPath, data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }

    /// <summary>The Word Art golden gate: the engine export of a scene with a wordart action, under <see cref="CompareWordArt"/>.</summary>
    public static void AssertWordArtMatches(string name, string goldenPath, Document document, string heatmapPath)
    {
        using var golden = ImageDiff.Load(goldenPath);
        using var rendered = Renderer.Export(document);
        using var candidate = ImageDiff.FromImage(rendered);
        AssertWordArtBitmaps(name, golden, candidate, heatmapPath);
    }
}
