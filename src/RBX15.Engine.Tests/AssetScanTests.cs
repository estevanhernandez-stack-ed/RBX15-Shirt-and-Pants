using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The asset library's pure logic (editor.js:2339-2450, :2452-2484, :2544). Fixture paths stand for a folder picked
/// as "root", so RelativePath segment 0 is "root", as Chromium's webkitRelativePath. Expected orders that depend on
/// JavaScript were printed by node from 4.1's own expressions.
/// </summary>
public class AssetScanTests
{
    static (string FullPath, string RelativePath) F(string relative) => (@"C:\pick\" + relative.Replace('/', '\\'), relative);

    static string[] Paths(AssetSet set) => set.Entries.Select(e => e.RelativePath).ToArray();

    [Fact] // R14: import's formats, so BMP is listed too (4.1: png|jpg|jpeg|gif|webp, editor.js:2382)
    public void The_listed_extensions_are_imports()
    {
        Assert.Equal([".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"], AssetScan.Extensions);
    }

    [Theory] // /\.(png|jpg|jpeg|gif|webp)$/i plus .bmp: case-insensitive, the last extension only
    [InlineData("a.png", true)]
    [InlineData("A.PNG", true)]
    [InlineData("b.Jpeg", true)]
    [InlineData("c.jpg", true)]
    [InlineData("d.webp", true)]
    [InlineData("e.gif", true)]
    [InlineData("f.bmp", true)]
    [InlineData(".png", true)]
    [InlineData("g.svg", false)]
    [InlineData("h.txt", false)]
    [InlineData("png", false)]
    [InlineData("i.png.txt", false)]
    [InlineData("README.md", false)]
    public void Image_names_are_filtered_by_extension(string name, bool expected) => Assert.Equal(expected, AssetScan.IsImageName(name));

    [Fact] // asset-dedupe-by-name parityCheck: four x.png at depths 2-5, only the deepest remains, and the label says so
    public void The_deepest_copy_of_a_name_wins()
    {
        var set = AssetScan.FromFolder([F("root/a/x.png"), F("root/a/sub/x.png"), F("root/b/x.png"), F("root/a/sub/deep/x.png")]);
        Assert.Equal(["root/a/sub/deep/x.png"], Paths(set));
        Assert.Equal(3, set.DuplicatesRemoved);
        Assert.Equal("1 unique images (3 duplicates removed)", set.Summary);     // editor.js:2448 (asset-count-label, fix)
        Assert.Equal(@"C:\pick\root\a\sub\deep\x.png", set.Entries[0].FullPath);
    }

    [Fact] // a tie keeps the first in sorted order; a deeper copy takes the first-seen name's position (a JS Map's set)
    public void A_tie_keeps_the_first_and_a_deeper_copy_keeps_the_names_position()
    {
        var set = AssetScan.FromFolder([F("root/z/a.png"), F("root/b.png"), F("root/a.png"), F("root/y/q.png"), F("root/c/q.png")]);
        // Sorted: root/a.png, root/b.png, root/c/q.png, root/y/q.png, root/z/a.png. a.png is first seen at depth 2 and
        // replaced by root/z/a.png (depth 3) in its slot; c/q and y/q tie at depth 3, so c/q stays.
        Assert.Equal(["root/z/a.png", "root/b.png", "root/c/q.png"], Paths(set));
        Assert.Equal(2, set.DuplicatesRemoved);
    }

    [Fact] // asset-dedupe-by-name quirk: the Map key is case-sensitive, so X.png and x.png are two images
    public void Names_differing_only_in_case_are_both_kept()
    {
        var set = AssetScan.FromFolder([F("root/A/X.png"), F("root/B/x.png")]);
        Assert.Equal(["root/A/X.png", "root/B/x.png"], Paths(set));
        Assert.Equal(0, set.DuplicatesRemoved);
    }

    [Fact] // io-folder-picker (fix): the input order never matters; non-images never count as duplicates
    public void The_result_is_the_same_for_any_input_order_and_non_images_are_dropped()
    {
        (string, string)[] files = [F("root/b/2.png"), F("root/notes.txt"), F("root/a/1.PNG"), F("root/b/logo.svg"), F("root/a/2.png")];
        var forward = AssetScan.FromFolder(files);
        var backward = AssetScan.FromFolder(files.Reverse());
        Assert.Equal(["root/a/1.PNG", "root/a/2.png"], Paths(forward));
        Assert.Equal(Paths(forward), Paths(backward));
        Assert.Equal(1, forward.DuplicatesRemoved);  // b/2.png ties a/2.png at depth 3; a/ sorts first
        Assert.Equal("2 unique images (1 duplicates removed)", forward.Summary);
    }

    [Fact] // a folder with no images: no entries (the caller shows the no-images alert, editor.js:2384-2386)
    public void A_folder_without_images_has_no_entries()
    {
        var set = AssetScan.FromFolder([F("root/readme.txt"), F("root/art.svg")]);
        Assert.Empty(set.Entries);
        Assert.Equal(0, set.DuplicatesRemoved);
    }

    [Fact] // asset-folder-grouping parityCheck: root files, three subfolders, two same-named leaves merged; order, counts, labels
    public void Groups_are_immediate_parents_in_4_1s_order_with_labels()
    {
        var set = AssetScan.FromFolder([
            F("root/top.png"),
            F("root/b/1.png"),
            F("root/x/Cool/2.png"),
            F("root/y/Cool/3.png"),
            F("root/A/4.png"),
            F("root/_x/5.png"),
        ]);
        Assert.Equal(["(all)", "(root)", "_x", "A", "b", "Cool"], set.Groups.Select(g => g.Key));
        Assert.Equal(["All (6)", "Root (1)", "_x (1)", "A (1)", "b (1)", "Cool (2)"], set.Groups.Select(g => g.Label));
        Assert.True(set.ShowGroupButtons);
        Assert.Equal(["root/x/Cool/2.png", "root/y/Cool/3.png"], set.Groups[5].Entries.Select(e => e.RelativePath));
        Assert.Equal(set.Entries, set.Groups[0].Entries);
        Assert.Equal("(root)", set.Entries.Single(e => e.FileName == "top.png").Group);
    }

    [Fact] // a.localeCompare(b) in node 22 (ICU): ["_x","10","9","a","A","b","cool","Cool","e","é","f","zeta","Zeta"]
    public void Folder_groups_sort_as_javascripts_localeCompare()
    {
        string[] names = ["b", "A", "a", "_x", "10", "9", "Zeta", "zeta", "é", "e", "f", "Cool", "cool"];
        var set = AssetScan.FromFolder(names.Select((n, i) => F($"root/{n}/{i}.png")));
        Assert.Equal(["(all)", "_x", "10", "9", "a", "A", "b", "cool", "Cool", "e", "é", "f", "zeta", "Zeta"], set.Groups.Select(g => g.Key));
    }

    [Theory] // buttons only with more than two groups (editor.js:2432): one subfolder alone, or root files alone, shows none
    [InlineData(new[] { "root/sub/1.png", "root/sub/2.png" }, false)]
    [InlineData(new[] { "root/1.png", "root/2.png" }, false)]
    [InlineData(new[] { "root/1.png", "root/sub/2.png" }, true)]
    [InlineData(new[] { "root/s/1.png", "root/t/2.png" }, true)]
    public void Group_buttons_show_only_past_two_groups(string[] paths, bool expected) =>
        Assert.Equal(expected, AssetScan.FromFolder(paths.Select(F)).ShowGroupButtons);

    [Fact] // a subfolder named "(all)" is overwritten by the all-files entry in 4.1's map (editor.js:2419)
    public void A_subfolder_named_all_does_not_make_a_second_all_group()
    {
        var set = AssetScan.FromFolder([F("root/(all)/1.png"), F("root/s/2.png")]);
        Assert.Equal(["(all)", "s"], set.Groups.Select(g => g.Key));
        Assert.Equal(2, set.Groups[0].Entries.Length);
    }

    [Fact] // packs-load-builtin (fix): the pack's own summary, sorted ordinal-ignore-case, no dedupe, no buttons
    public void A_pack_is_sorted_by_name_with_its_own_summary_and_no_buttons()
    {
        var set = AssetScan.FromPack("drips", [@"D:\p\drips\b.png", @"D:\p\drips\README.md", @"D:\p\drips\A.png", @"D:\p\drips\_c.png"]);
        Assert.Equal(["A.png", "b.png", "_c.png"], set.Entries.Select(e => e.FileName));   // OrdinalIgnoreCase: 'B' (0x42) < '_' (0x5F)
        Assert.Equal(["packs/drips/A.png", "packs/drips/b.png", "packs/drips/_c.png"], Paths(set));
        Assert.Equal(["(all)", "drips"], set.Groups.Select(g => g.Key));
        Assert.False(set.ShowGroupButtons);
        Assert.Equal(0, set.DuplicatesRemoved);
        Assert.Equal("3 images in built-in drips pack", set.Summary);   // editor.js:2373
        Assert.Equal("drips", set.Entries[0].Group);
    }

    [Fact] // pack-builtin-load: the shipped packs list 156 and 25 in readdir (NTFS) order, first and last as node printed
    public void The_shipped_packs_sort_as_4_1_listed_them()
    {
        string packs = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "packs");
        var drips = AssetScan.FromPack("drips", Directory.GetFiles(Path.Combine(packs, "drips")));
        var words = AssetScan.FromPack("word_art", Directory.GetFiles(Path.Combine(packs, "word_art")));
        Assert.Equal(156, drips.Entries.Length);
        Assert.Equal(25, words.Entries.Length);
        Assert.Equal(("drip_blood_front_flipped.png", "drip_blood_front_v1.png", "drip_white_wide_v2.png"),
            (drips.Entries[0].FileName, drips.Entries[1].FileName, drips.Entries[^1].FileName));
        Assert.Equal(("word_626_anton.png", "word_w_anton.png"), (words.Entries[0].FileName, words.Entries[^1].FileName));
        Assert.Equal("25 images in built-in word_art pack", words.Summary);   // the folder token, not "word art"
    }

    [Theory] // asset-thumb-uuid-label: the caption regex strips 5 extensions only; a UUID-looking name shows its parent
    [InlineData("x.png", "root/x.png", "x")]
    [InlineData("name.webp.PNG", "root/a/name.webp.PNG", "name.webp")]
    [InlineData("photo.bmp", "root/photo.bmp", "photo.bmp")]
    [InlineData("1a2b3c4d-1111-2222-3333-444455556666.png", "root/x/Cool/1a2b3c4d-1111-2222-3333-444455556666.png", "Cool")]
    [InlineData("1A2B3C4D-AAAA-rest.jpg", "root/Shots/1A2B3C4D-AAAA-rest.jpg", "Shots")]
    [InlineData("1a2b3c4d-1111-2222.png", "root/1a2b3c4d-1111-2222.png", "1a2b3c4d-1111-2222")]
    [InlineData("1a2b3c4d-111.png", "root/x/1a2b3c4d-111.png", "1a2b3c4d-111")]
    [InlineData("g1a2b3c4-1111.png", "root/x/g1a2b3c4-1111.png", "g1a2b3c4-1111")]
    public void Captions_use_the_parent_for_uuid_names(string name, string relative, string expected) =>
        Assert.Equal(expected, AssetScan.Caption(name, relative));

    [Fact] // the entry keeps both names: the caption may be the folder, the layer is always the file (asset-thumb-uuid-label quirk)
    public void An_entry_carries_the_file_name_layer_name_and_caption()
    {
        var e = AssetScan.FromFolder([F("root/x/Cool/1a2b3c4d-1111-z.v2.png")]).Entries.Single();
        Assert.Equal(("1a2b3c4d-1111-z.v2.png", "1a2b3c4d-1111-z.v2", "Cool", "Cool"), (e.FileName, e.LayerName, e.Caption, e.Group));
    }

    [Fact] // showAssetFolder's label (editor.js:2544)
    public void The_count_text_names_all_folders_or_the_group_key()
    {
        var set = AssetScan.FromFolder([F("root/1.png"), F("root/s/2.png"), F("root/s/3.png")]);
        Assert.Equal("3 images in all folders", AssetScan.CountText(set.Groups[0]));
        Assert.Equal("1 images in (root)", AssetScan.CountText(set.Groups[1]));
        Assert.Equal("2 images in s", AssetScan.CountText(set.Groups[2]));
    }
}
