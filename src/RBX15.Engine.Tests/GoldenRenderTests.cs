using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M1 gate: the engine's export of every frozen 4.1 scene passes the golden rule (spec section 6).
/// Goldens are never edited here. A failing scene writes a heatmap to diff-heatmaps/ (a CI artifact).
/// </summary>
public class GoldenRenderTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "render");
    static readonly Lazy<IReadOnlyList<SceneLoader.Scene>> Scenes = new(() => SceneLoader.LoadAll(Path.Combine(Dir, "scenes.json"), Root));

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    static SKBitmap OpaqueOnly(SKBitmap source, SKBitmap golden)
    {
        var copy = source.Copy();
        for (int y = 0; y < golden.Height; y++)
            for (int x = 0; x < golden.Width; x++)
                if (golden.GetPixel(x, y).Alpha < 255) copy.SetPixel(x, y, SKColors.Transparent);
        return copy;
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_export_matches_the_frozen_4_1_golden(string name)
    {
        var scene = Scenes.Value.Single(s => s.Name == name);
        using var golden = ImageDiff.Load(Path.Combine(Dir, name + ".png"));
        using var rendered = Renderer.Export(scene.Document);
        using var candidate = ImageDiff.FromImage(rendered);

        bool rgbShift = scene.Document.Layers.Any(l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0);
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
            var heatDir = Path.Combine(Root, "diff-heatmaps");
            Directory.CreateDirectory(heatDir);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(heatDir, name + ".png"), data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }
}
