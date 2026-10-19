using System.Security.Cryptography;
using System.Text.Json;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The 15-font Word Art gate (M4b decision W1; ledger wordart-font-list and fonts-google-runtime).
/// tests/goldens/app/wordart-fonts holds, for each of the 15 waFont options, the bitmap 4.1's own
/// renderWordArt drew for "RIZZ" in Solid at size 64 (tools/parity-ref/wordart-ref.js over
/// tools/parity-ref/wordart-fonts-scenes.json, frozen by tools/parity-ref/freeze-wordart-fonts.py).
/// The engine's Add to canvas bitmap for the same settings is held to the Word Art rule
/// (GoldenRule.CompareWordArt, ruling R2): same size, every interior pixel within 1 level.
/// <para>
/// This file is compiled only once the capture is committed: RBX15.Engine.Tests.csproj removes it from
/// the build while tests/goldens/app/wordart-fonts/index.json is absent, so the suite stays green until the
/// controller's capture lands and then picks the gate up with no further edit.
/// </para>
/// </summary>
public class GoldenWordArtFontTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app", "wordart-fonts");

    static string SceneName(string family) => "wordart-font-" + family.ToLowerInvariant().Replace(' ', '-');

    static WordArtSettings Settings(string family) =>
        new("RIZZ", family, WordArtStyle.Solid, 64, new SKColor(0x17, 0xd4, 0xfa), SKColors.Black, new SKColor(0xf2, 0x2f, 0x89), 3, 0);

    public static TheoryData<string> Families()
    {
        var data = new TheoryData<string>();
        foreach (var (family, _) in WordArt.FontOptions) data.Add(family);
        return data;
    }

    [Fact]
    public void The_capture_covers_exactly_the_15_fonts_and_every_file_matches_its_recorded_hash()
    {
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "index.json")));
        var recorded = index.RootElement.GetProperty("scenes").EnumerateArray()
            .ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("sha256").GetString()!);
        Assert.Equal(WordArt.FontOptions.Select(o => SceneName(o.Family)).Order(StringComparer.Ordinal), recorded.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(recorded.Keys.Select(n => n + ".png").Order(StringComparer.Ordinal),
                     Directory.EnumerateFiles(Dir, "*.png").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal));
        foreach (var (name, sha) in recorded)
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Dir, name + ".png")))).ToLowerInvariant());
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void The_engine_bitmap_for_RIZZ_in_each_font_meets_4_1s_under_the_word_art_rule(string family)
    {
        var name = SceneName(family);
        var layer = WordArt.CreateLayer(Settings(family), TemplateMode.Shirt, 1);
        using var candidate = ImageDiff.FromImage(layer.Image);
        using var golden = ImageDiff.Load(Path.Combine(Dir, name + ".png"));
        GoldenRule.AssertWordArtBitmaps(name, golden, candidate, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }
}
