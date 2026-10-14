using System.Security.Cryptography;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Fonts resolves the vendored faces as Chromium resolved 4.1's @font-face rules
/// (tools/parity-ref/fonts.js FACES): CSS weight matching, the variable wght axis at the declared
/// weight, and synthetic bold for a 900 request on a face declared below 600.
/// </summary>
public class FontsTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void The_manifest_declares_the_28_faces_fonts_js_declares()
    {
        // 17 Word Art faces (Poppins and Orbitron declare 700 and 900) and 11 UI faces.
        var faces = Fonts.ParseManifest(File.ReadAllText(Path.Combine(Root, "assets", "fonts", "README.md")));
        Assert.Equal(28, faces.Count);
        Assert.Contains(new Fonts.Face("Orbitron", 900, "orbitron/Orbitron[wght].ttf"), faces);
        Assert.Contains(new Fonts.Face("Dancing Script", 700, "dancingscript/DancingScript[wght].ttf"), faces);
    }

    [Fact]
    public void Every_word_art_family_resolves_at_900()
    {
        Assert.Equal(15, Fonts.WordArtFamilies.Count);
        foreach (var family in Fonts.WordArtFamilies)
            Assert.Equal(family, Fonts.Get(family, 900).FamilyName);
    }

    [Theory]
    [InlineData("Poppins", 900, 900)]
    [InlineData("Poppins", 700, 700)]
    [InlineData("Poppins", 800, 900)]
    [InlineData("Poppins", 400, 700)]
    [InlineData("Dancing Script", 900, 700)]
    [InlineData("Orbitron", 900, 900)]
    [InlineData("Anton", 900, 400)]
    [InlineData("Inter", 450, 500)]
    [InlineData("Inter", 300, 400)]
    public void Weight_matching_follows_css(string family, int requested, int declared) =>
        Assert.Equal(declared, Fonts.Match(family, requested).Weight);

    [Theory]
    [InlineData("Anton", true)]
    [InlineData("Bebas Neue", true)]
    [InlineData("Great Vibes", true)]
    [InlineData("Pacifico", true)]
    [InlineData("Creepster", true)]
    [InlineData("Bangers", true)]
    [InlineData("Permanent Marker", true)]
    [InlineData("Bungee Shade", true)]
    [InlineData("Rubik Mono One", true)]
    [InlineData("Press Start 2P", true)]
    [InlineData("Righteous", true)]
    [InlineData("Dancing Script", false)]
    [InlineData("Poppins", false)]
    [InlineData("Lato", false)]
    [InlineData("Orbitron", false)]
    public void A_900_request_is_synthetic_bold_only_on_faces_below_600(string family, bool synthetic)
    {
        Assert.Equal(synthetic, Fonts.IsSyntheticBold(family, 900));
        using var font = Fonts.CreateFont(family, 900, 64);
        Assert.Equal(synthetic, font.Embolden);
    }

    [Fact]
    public void A_request_below_the_bold_threshold_is_never_synthetic() => Assert.False(Fonts.IsSyntheticBold("Anton", 400));

    [Fact]
    public void Variable_files_draw_at_the_declared_weight()
    {
        Assert.Equal(700f, Fonts.AxisWeight("Dancing Script", 900));
        Assert.Equal(900f, Fonts.AxisWeight("Orbitron", 900));
        Assert.Null(Fonts.AxisWeight("Anton", 900));
        // hmtx advances at those instances (1000 upem): Dancing Script R is 616 units at wght 700,
        // Orbitron N is 832 units at wght 900. Chromium's measureText reports the same widths.
        using var dancing = Fonts.CreateFont("Dancing Script", 900, 64);
        Assert.Equal(616 * 64 / 1000f, dancing.GetGlyphWidths(dancing.GetGlyphs("R"))[0], 3);
        using var orbitron = Fonts.CreateFont("Orbitron", 900, 72);
        Assert.Equal(832 * 72 / 1000f, orbitron.GetGlyphWidths(orbitron.GetGlyphs("N"))[0], 3);
    }

    [Fact]
    public void Synthetic_bold_keeps_the_advance()
    {
        // Anton R is 976 units of 2048; Chromium measures 30.5 at 64px with synthetic bold on.
        using var anton = Fonts.CreateFont("Anton", 900, 64);
        Assert.Equal(30.5f, anton.GetGlyphWidths(anton.GetGlyphs("R"))[0], 3);
    }

    [Fact]
    public void Canvas_text_settings_match_chromium()
    {
        using var font = Fonts.CreateFont("Poppins", 900, 64);
        Assert.Equal((SKFontEdging.Antialias, true, SKFontHinting.None, true), (font.Edging, font.Subpixel, font.Hinting, font.LinearMetrics));
    }

    [Fact]
    public void An_unknown_family_throws() => Assert.Throws<ArgumentException>(() => Fonts.Get("Comic Sans MS", 900));

    [Fact]
    public void The_build_output_carries_a_byte_identical_copy_of_assets_fonts()
    {
        var repo = Path.Combine(Root, "assets", "fonts");
        static List<string> Listing(string dir) =>
            Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f)).Order(StringComparer.Ordinal).ToList();
        var expected = Listing(repo);
        Assert.Equal(expected, Listing(Fonts.Root));
        foreach (var rel in expected)
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, rel))), SHA256.HashData(File.ReadAllBytes(Path.Combine(Fonts.Root, rel))));
    }
}
