using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RBX15.Engine;

/// <summary>One image the asset browser lists (4.1: a File with its webkitRelativePath, editor.js:2354-2360, :2381).</summary>
/// <param name="FullPath">The absolute path on disk.</param>
/// <param name="RelativePath">'/'-separated; segment 0 is the picked root's name, as webkitRelativePath; packs: "packs/&lt;name&gt;/&lt;file&gt;" (editor.js:2357).</param>
/// <param name="FileName">The file name with its extension (f.name).</param>
/// <param name="LayerName">The file name minus its last extension: the layer name an add gives (editor.js:2490).</param>
/// <param name="Caption">The thumbnail caption (editor.js:2478-2484, ledger asset-thumb-uuid-label).</param>
/// <param name="Group">The immediate parent folder's name, or <see cref="AssetScan.RootKey"/> (editor.js:2403-2413).</param>
public sealed record AssetEntry(string FullPath, string RelativePath, string FileName, string LayerName, string Caption, string Group);

/// <summary>One group button: its key (the button's title, editor.js:2442), its label and its entries in browser order.</summary>
public sealed record AssetGroup(string Key, string Label, ImmutableArray<AssetEntry> Entries);

/// <summary>A loaded pack or folder: what the browser shows right after the load.</summary>
/// <param name="Entries">The unique entries in 4.1's Map order: first-seen name position, deepest copy (editor.js:2390-2398).</param>
/// <param name="Groups"><see cref="AssetScan.AllKey"/> first, <see cref="AssetScan.RootKey"/> second, the rest by culture order (editor.js:2424-2430).</param>
/// <param name="ShowGroupButtons">More than two groups, for a folder (editor.js:2432); never for a pack (editor.js:2372).</param>
/// <param name="DuplicatesRemoved">Image files dropped by the name dedupe (editor.js:2448).</param>
/// <param name="Summary">The count label right after the load: the text 4.1 set and then overwrote in the same tick (ledger asset-count-label-overwritten, fix).</param>
public sealed record AssetSet(ImmutableArray<AssetEntry> Entries, ImmutableArray<AssetGroup> Groups, bool ShowGroupButtons, int DuplicatesRemoved, string Summary);

/// <summary>
/// The asset library's folder and pack logic (editor.js:2339-2450 and :2452-2484) as pure functions over paths:
/// no file IO, so every rule is tested on fixture lists. The App enumerates the disk and hands the paths here.
/// </summary>
public static class AssetScan
{
    /// <summary>The "(all)" group key (editor.js:2419).</summary>
    public const string AllKey = "(all)";

    /// <summary>The group of files directly inside the picked folder (editor.js:2408-2409).</summary>
    public const string RootKey = "(root)";

    /// <summary>
    /// The listed extensions, lowercase, matched case-insensitively. 4.1's folder and pack filter is
    /// /\.(png|jpg|jpeg|gif|webp)$/i (editor.js:2348, :2382); v5 lists exactly what import accepts, so BMP is
    /// added (interfaces R14; ledger io-image-formats). Equal to RBX15.App.Services.ImageImporter.Extensions (pinned by an App test).
    /// </summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    static readonly Regex CaptionExtension = new(@"\.(png|jpg|jpeg|gif|webp)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex UuidPrefix = new("^[0-9a-f]{8}-[0-9a-f]{4}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>True when the name ends in one of <see cref="Extensions"/>, any case (editor.js:2382).</summary>
    public static bool IsImageName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return Extensions.Any(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A picked folder (folderInput.onchange, editor.js:2380-2450). The files are first sorted by RelativePath,
    /// ordinal ignoring case: 4.1 took Chromium's enumeration order, v5 makes it deterministic (io-folder-picker, fix).
    /// Then the image filter; then the dedupe by file name, ordinal (a JS Map key), where a strictly deeper copy
    /// replaces the kept one in its first-seen position and a tie keeps the first (editor.js:2390-2398,
    /// asset-dedupe-by-name, keep); then the groups by immediate parent (asset-folder-grouping, keep).
    /// </summary>
    public static AssetSet FromFolder(IEnumerable<(string FullPath, string RelativePath)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var images = files
            .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(f => (f.FullPath, f.RelativePath, FileName: LastSegment(f.RelativePath)))
            .Where(f => IsImageName(f.FileName))
            .ToList();

        var order = new List<string>();
        var byName = new Dictionary<string, (string FullPath, string RelativePath, int Depth)>(StringComparer.Ordinal);
        foreach (var f in images)
        {
            int depth = f.RelativePath.Split('/').Length;
            if (!byName.TryGetValue(f.FileName, out var kept)) order.Add(f.FileName);
            else if (depth <= kept.Depth) continue;
            byName[f.FileName] = (f.FullPath, f.RelativePath, depth);
        }
        var unique = order.Select(n => Entry(byName[n].FullPath, byName[n].RelativePath)).ToImmutableArray();

        var groups = new List<AssetGroup>();
        // A subfolder named "(all)" is overwritten by the (all) entry in 4.1's map (editor.js:2419), so it has no group.
        foreach (var key in unique.Select(e => e.Group).Where(k => k != AllKey).Distinct(StringComparer.Ordinal)
                     .OrderBy(k => k == RootKey ? 0 : 1)
                     .ThenBy(k => k, StringComparer.InvariantCulture))
            groups.Add(Group(key, unique.Where(e => e.Group == key).ToImmutableArray()));
        groups.Insert(0, Group(AllKey, unique));

        int removed = images.Count - unique.Length;
        return new AssetSet(unique, [.. groups], groups.Count > 2, removed,
            Invariant($"{unique.Length} unique images ({removed} duplicates removed)"));
    }

    /// <summary>
    /// A built-in pack (loadBuiltInPack, editor.js:2339-2375): the image files sorted by name, ordinal ignoring
    /// case (spec §3; 4.1 took readdir order, which NTFS returns sorted the same way). No dedupe. The groups are
    /// "(all)" and the pack (editor.js:2370), with no buttons (editor.js:2372). RelativePath is 4.1's fake
    /// webkitRelativePath, "packs/&lt;pack&gt;/&lt;file&gt;" (editor.js:2357).
    /// </summary>
    public static AssetSet FromPack(string packName, IEnumerable<string> fullPaths)
    {
        ArgumentException.ThrowIfNullOrEmpty(packName);
        ArgumentNullException.ThrowIfNull(fullPaths);
        var entries = fullPaths
            .Select(p => (FullPath: p, FileName: Path.GetFileName(p)))
            .Where(f => IsImageName(f.FileName))
            .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(f => Entry(f.FullPath, "packs/" + packName + "/" + f.FileName))
            .ToImmutableArray();
        return new AssetSet(entries, [Group(AllKey, entries), Group(packName, entries)], false, 0,
            Invariant($"{entries.Length} images in built-in {packName} pack"));
    }

    /// <summary>
    /// The thumbnail caption (editor.js:2480-2484): the name minus /\.(png|jpg|jpeg|gif|webp)$/i, verbatim (so a
    /// .bmp keeps its extension), or the immediate parent folder's name when that starts like a UUID
    /// (/^[0-9a-f]{8}-[0-9a-f]{4}/i) and the path has 3 or more segments.
    /// </summary>
    public static string Caption(string fileName, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(relativePath);
        string raw = CaptionExtension.Replace(fileName, "", 1);
        var parts = relativePath.Split('/');
        return parts.Length >= 3 && UuidPrefix.IsMatch(raw) ? parts[^2] : raw;
    }

    /// <summary>The count label after a group switch (showAssetFolder, editor.js:2544).</summary>
    public static string CountText(AssetGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return Invariant($"{group.Entries.Length} images in {(group.Key == AllKey ? "all folders" : group.Key)}");
    }

    static AssetEntry Entry(string fullPath, string relativePath)
    {
        string fileName = LastSegment(relativePath);
        var parts = relativePath.Split('/');
        return new AssetEntry(fullPath, relativePath, fileName, LayerName(fileName), Caption(fileName, relativePath),
            parts.Length <= 2 ? RootKey : parts[^2]);
    }

    /// <summary>
    /// <c>file.name.replace(/\.[^.]+$/, '')</c> (editor.js:2490): "a.b.png" gives "a.b", ".png" gives "", "file." stays.
    /// The same rule as RBX15.App.Services.ImageImporter.LayerName, which the add itself uses.
    /// </summary>
    static string LayerName(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        return dot >= 0 && dot < fileName.Length - 1 ? fileName[..dot] : fileName;
    }

    static string LastSegment(string relativePath) => relativePath[(relativePath.LastIndexOf('/') + 1)..];

    /// <summary>The button label (editor.js:2437-2440).</summary>
    static AssetGroup Group(string key, ImmutableArray<AssetEntry> entries) =>
        new(key, Invariant($"{(key == AllKey ? "All" : key == RootKey ? "Root" : key)} ({entries.Length})"), entries);

    static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
