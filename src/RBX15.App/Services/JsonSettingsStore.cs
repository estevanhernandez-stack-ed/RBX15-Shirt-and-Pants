using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RBX15.App.Services;

/// <summary>
/// <c>settings.json</c> in one directory (the app passes <c>%LOCALAPPDATA%\RBX15</c>, which MSIX
/// redirects into the package's local data, removed on uninstall). The file holds one object:
/// <c>{ "window": { "left", "top", "width", "height", "maximized" }, "lastProjectFolder": "...", "lastAssetFolder": "...", "recentProjects": ["..."] }</c>.
/// Each save rewrites only its own key and keeps every other key it read.
/// <para>
/// Writes are atomic: the whole file goes to <c>settings.json.tmp</c>, then
/// <see cref="File.Move(string, string, bool)"/> replaces <c>settings.json</c>, so a crash mid-write
/// leaves the previous file intact. Reads never throw: a missing, unreadable, malformed or
/// out-of-range file gives null and the window opens at its default.
/// </para>
/// <para>
/// A save after a read that failed on an existing file (an <see cref="IOException"/> such as a sharing violation, or
/// <see cref="UnauthorizedAccessException"/>) is skipped, so a transient lock never makes a save drop the sibling keys
/// it could not read (M4a interfaces §2.5, the M3b carry-forward). A malformed file is still replaced.
/// </para>
/// </summary>
public sealed class JsonSettingsStore(string directory) : ISettingsStore
{
    public const string FileName = "settings.json";
    const string WindowKey = "window";
    const string RecentsKey = "recentProjects";

    /// <summary>The root key of each remembered folder.</summary>
    internal static string FolderKey(SettingsFolder which) => which switch
    {
        SettingsFolder.Project => "lastProjectFolder",
        SettingsFolder.Asset => "lastAssetFolder",
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
    };

    public string FilePath { get; } = Path.Combine(directory, FileName);

    public WindowPlacement? LoadWindowPlacement()
    {
        if (ReadRoot()?[WindowKey] is not JsonObject w) return null;
        if (Num(w["left"]) is not double left || Num(w["top"]) is not double top ||
            Num(w["width"]) is not double width || Num(w["height"]) is not double height ||
            w["maximized"] is not JsonValue m || !m.TryGetValue(out bool maximized)) return null;
        var p = new WindowPlacement(left, top, width, height, maximized);
        return IsUsable(p) ? p : null;
    }

    static double? Num(JsonNode? node) => node is JsonValue v && v.TryGetValue(out double d) ? d : null;

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!IsUsable(placement)) return;
        Update(WindowKey, new JsonObject
        {
            ["left"] = placement.Left,
            ["top"] = placement.Top,
            ["width"] = placement.Width,
            ["height"] = placement.Height,
            ["maximized"] = placement.Maximized,
        });
    }

    public string? LoadFolder(SettingsFolder which)
    {
        string key = FolderKey(which);
        if (ReadRoot()?[key] is not JsonValue v || !v.TryGetValue(out string? folder)) return null;
        try
        {
            return Path.IsPathFullyQualified(folder) && Directory.Exists(folder) ? folder : null;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SaveFolder(SettingsFolder which, string folder)
    {
        string key = FolderKey(which);
        if (string.IsNullOrEmpty(folder) || !Path.IsPathFullyQualified(folder)) return;
        Update(key, JsonValue.Create(folder));
    }

    public IReadOnlyList<string> LoadRecents()
    {
        if (ReadRoot()?[RecentsKey] is not JsonArray array) return [];
        var paths = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is JsonValue v && v.TryGetValue(out string? path) && !string.IsNullOrEmpty(path) && IsFullyQualified(path)) paths.Add(path);
        }
        return paths;
    }

    public void SaveRecents(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var array = new JsonArray();
        foreach (var path in paths)
        {
            if (!string.IsNullOrEmpty(path) && IsFullyQualified(path)) array.Add(JsonValue.Create(path));
        }
        Update(RecentsKey, array);
    }

    static bool IsFullyQualified(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Finite coordinates and a positive finite size; anything else is a corrupt file.</summary>
    static bool IsUsable(WindowPlacement p) =>
        double.IsFinite(p.Left) && double.IsFinite(p.Top) &&
        double.IsFinite(p.Width) && double.IsFinite(p.Height) && p.Width > 0 && p.Height > 0;

    /// <summary>Rewrites <paramref name="key"/> and keeps every other key; skipped when the existing file could not be read.</summary>
    void Update(string key, JsonNode value)
    {
        if (!TryReadRoot(out var root)) return;
        root ??= new JsonObject();
        root[key] = value;
        var temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: losing a remembered setting must never block closing the app or a save.
            try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
        }
    }

    JsonObject? ReadRoot() => TryReadRoot(out var root) ? root : null;

    /// <summary>
    /// False only when an existing file could not be read (IO or access). A missing file reads as true and null; a
    /// malformed or non-object file as true and null, so the next save replaces it.
    /// </summary>
    bool TryReadRoot(out JsonObject? root)
    {
        root = null;
        try
        {
            if (!File.Exists(FilePath)) return true;
            root = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject;
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
