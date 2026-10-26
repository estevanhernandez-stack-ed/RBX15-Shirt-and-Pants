using System.Buffers.Binary;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The Store art in docs/store-assets has the pixel sizes its README table states (ledger docs-store-assets;
/// make-store-assets.py writes them). Partner Center rejects art of the wrong size, and the files are uploaded by hand.
/// </summary>
public class StoreAssetsTests
{
    static readonly string Dir = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "docs", "store-assets");

    static readonly Regex Row = new(@"^\|\s*`(?<file>[^`]+\.png)`\s*\|\s*(?<w>\d+)\s*x\s*(?<h>\d+)\s*\|", RegexOptions.Multiline);

    static List<(string File, int W, int H)> Listed() =>
        Row.Matches(File.ReadAllText(Path.Combine(Dir, "README.md")))
            .Select(m => (m.Groups["file"].Value, int.Parse(m.Groups["w"].Value), int.Parse(m.Groups["h"].Value)))
            .ToList();

    /// <summary>Width and height from the PNG IHDR chunk, with no image library: the check needs the header only.</summary>
    static (int W, int H) Size(string path)
    {
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(path)) fs.ReadExactly(head);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, head[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(head, 12, 4));
        return (BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(20)));
    }

    [Fact]
    public void The_readme_lists_the_nine_store_images()
    {
        Assert.Equal(9, Listed().Count);
    }

    [Fact]
    public void Every_listed_png_exists_at_its_stated_pixel_size()
    {
        foreach (var (file, w, h) in Listed())
        {
            string path = Path.Combine(Dir, file);
            Assert.True(File.Exists(path), $"{file} is listed in docs/store-assets/README.md but missing");
            Assert.Equal((w, h), Size(path));
        }
    }

    [Fact]
    public void Every_png_in_the_folder_is_listed()
    {
        var listed = Listed().Select(r => r.File).ToHashSet(StringComparer.Ordinal);
        var onDisk = Directory.GetFiles(Dir, "*.png").Select(Path.GetFileName).ToList();
        foreach (var f in onDisk) Assert.True(listed.Contains(f!), $"{f} is in docs/store-assets but not in the README table");
    }
}
