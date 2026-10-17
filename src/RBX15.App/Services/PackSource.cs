using System.IO;

namespace RBX15.App.Services;

/// <summary>
/// The two built-in packs on disk (resolvePackDir, editor.js:2325-2333; ledger packs-resolve-dir, keep). 4.1 looked
/// next to editor.js, then in resources/packs; v5 ships them in its own output at <c>packs/&lt;name&gt;</c> (the App
/// project's Content items, copied from the repo's packs/ folder), so there is one place to look, in development and
/// installed alike. The pack names are 4.1's folder names, which the count label shows (editor.js:2373).
/// </summary>
public sealed class PackSource(string packsDirectory)
{
    /// <summary>btnPackDrips (editor.js:2377).</summary>
    public const string Drips = "drips";

    /// <summary>btnPackWordArt (editor.js:2378).</summary>
    public const string WordArt = "word_art";

    /// <summary>The build output's packs folder.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "packs");

    readonly string _directory = packsDirectory ?? throw new ArgumentNullException(nameof(packsDirectory));

    /// <summary>
    /// Every file directly in <c>&lt;packsDirectory&gt;/&lt;packName&gt;</c>, unsorted and unfiltered (AssetScan.FromPack sorts
    /// and filters, as readdirSync then filter did, editor.js:2347-2348). Null when the folder is missing or cannot be
    /// listed: 4.1's not-found case, and a broken install either way.
    /// </summary>
    public IReadOnlyList<string>? Files(string packName)
    {
        ArgumentException.ThrowIfNullOrEmpty(packName);
        string dir = Path.Combine(_directory, packName);
        if (!Directory.Exists(dir)) return null;
        try
        {
            return Directory.GetFiles(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
