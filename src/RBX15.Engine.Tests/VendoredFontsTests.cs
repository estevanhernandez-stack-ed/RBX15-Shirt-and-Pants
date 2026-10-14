using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// assets/fonts holds the only copy of the Word Art and UI fonts. Goldens v2 are captured with these
/// exact files injected as @font-face, and the v5 bundle ships the same files, so the manifest in
/// assets/fonts/README.md pins every byte (SHA-256) and the google/fonts commit they came from.
/// Google Fonts' served files drift over time; these may not.
/// </summary>
public partial class VendoredFontsTests
{
    const string Commit = "66928797f271fed5ee428544e7229685e2d90127";
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "assets", "fonts");

    [GeneratedRegex(@"^\| `(?<file>[^`]+)` \|.*\| (?<sha>[0-9a-f]{64}) \|$", RegexOptions.Multiline)]
    private static partial Regex ManifestRow();

    static Dictionary<string, string> Manifest()
    {
        var text = File.ReadAllText(Path.Combine(Dir, "README.md")).Replace("\r\n", "\n");
        return ManifestRow().Matches(text).ToDictionary(m => m.Groups["file"].Value, m => m.Groups["sha"].Value);
    }

    static IEnumerable<string> FilesOnDisk() =>
        Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Dir, f).Replace('\\', '/'))
            .Where(f => f != "README.md");

    [Fact]
    public void Manifest_names_the_pinned_google_fonts_commit() =>
        Assert.Contains($"google/fonts@{Commit}", File.ReadAllText(Path.Combine(Dir, "README.md")));

    [Fact]
    public void Manifest_lists_19_font_files_and_18_licenses()
    {
        var files = Manifest().Keys.ToList();
        Assert.Equal(19, files.Count(f => f.EndsWith(".ttf", StringComparison.Ordinal)));
        Assert.Equal(18, files.Count(f => f.EndsWith(".txt", StringComparison.Ordinal)));
    }

    [Fact]
    public void Every_vendored_file_matches_its_manifest_hash()
    {
        var mismatches = new List<string>();
        foreach (var (file, sha) in Manifest())
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Dir, file))));
            if (actual != sha) mismatches.Add($"{file}: manifest {sha}, file {actual}");
        }
        Assert.Empty(mismatches);
    }

    [Fact]
    public void Nothing_unlisted_sits_in_assets_fonts() =>
        Assert.Equal(Manifest().Keys.Order(StringComparer.Ordinal), FilesOnDisk().Order(StringComparer.Ordinal));

    [Fact]
    public void Every_family_directory_carries_its_license()
    {
        var dirs = Directory.GetDirectories(Dir);
        Assert.Equal(18, dirs.Length);
        foreach (var dir in dirs)
            Assert.True(File.Exists(Path.Combine(dir, "OFL.txt")) || File.Exists(Path.Combine(dir, "LICENSE.txt")), dir);
    }
}
