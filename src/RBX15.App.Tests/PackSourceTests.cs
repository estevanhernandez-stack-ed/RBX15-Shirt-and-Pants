using System.Security.Cryptography;
using RBX15.App.Services;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The built-in packs ship in the build output byte for byte (ledger pack-contents, packs-inventory-drips,
/// packs-inventory-wordart) and resolve from one place (packs-resolve-dir; resolvePackDir, editor.js:2325-2333).
/// </summary>
public class PackSourceTests
{
    static readonly string RepoPacks = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "packs");

    static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [Theory] // pack-contents parityCheck: 156 and 25 files, each SHA-256 equal to the repo's; only the PNGs (package.json extraResources **/*.png)
    [InlineData(PackSource.Drips, 156)]
    [InlineData(PackSource.WordArt, 25)]
    public void The_output_carries_each_pack_byte_for_byte(string pack, int count)
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "packs"), PackSource.DefaultDirectory);
        var shipped = new PackSource(PackSource.DefaultDirectory).Files(pack)!;
        var repo = Directory.GetFiles(Path.Combine(RepoPacks, pack), "*.png");
        Assert.Equal(count, shipped.Count);
        Assert.Equal(count, repo.Length);
        Assert.Equal(
            repo.Select(p => (Path.GetFileName(p), Sha(p))).OrderBy(x => x.Item1, StringComparer.Ordinal),
            shipped.Select(p => (Path.GetFileName(p), Sha(p))).OrderBy(x => x.Item1, StringComparer.Ordinal));
        Assert.False(File.Exists(Path.Combine(PackSource.DefaultDirectory, "README.md")));
    }

    [Fact] // 4.1's folder names, which the count label shows (editor.js:2373, :2377-2378)
    public void The_pack_names_are_4_1s_folder_names()
    {
        Assert.Equal("drips", PackSource.Drips);
        Assert.Equal("word_art", PackSource.WordArt);
    }

    [Fact] // a missing pack is null (the not-found alert); a pack lists only its own top-level files, unfiltered
    public void Files_lists_the_top_level_of_a_pack_or_null_when_it_is_missing()
    {
        using var files = new TestFiles();
        Directory.CreateDirectory(Path.Combine(files.Dir, "p", "nested"));
        File.WriteAllBytes(Path.Combine(files.Dir, "p", "a.png"), [1]);
        File.WriteAllBytes(Path.Combine(files.Dir, "p", "notes.txt"), [1]);
        File.WriteAllBytes(Path.Combine(files.Dir, "p", "nested", "deep.png"), [1]);
        var source = new PackSource(files.Dir);
        Assert.Null(source.Files("missing"));
        Assert.Equal(["a.png", "notes.txt"], source.Files("p")!.Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }
}
