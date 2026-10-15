using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RBX15.App.Services;

/// <summary>
/// <c>settings.json</c> in one directory (the app passes <c>%LOCALAPPDATA%\RBX15</c>, which MSIX
/// redirects into the package's local data, removed on uninstall). The file holds one object:
/// <c>{ "window": { "left", "top", "width", "height", "maximized" } }</c>. Later milestones add
/// sibling keys, so a save rewrites only <c>window</c> and keeps every other key it read.
/// <para>
/// Writes are atomic: the whole file goes to <c>settings.json.tmp</c>, then
/// <see cref="File.Move(string, string, bool)"/> replaces <c>settings.json</c>, so a crash mid-write
/// leaves the previous file intact. Reads never throw: a missing, unreadable, malformed or
/// out-of-range file gives null and the window opens at its default.
/// </para>
/// </summary>
public sealed class JsonSettingsStore(string directory) : ISettingsStore
{
    public const string FileName = "settings.json";
    const string WindowKey = "window";

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
        var root = ReadRoot() ?? new JsonObject();
        root[WindowKey] = new JsonObject
        {
            ["left"] = placement.Left,
            ["top"] = placement.Top,
            ["width"] = placement.Width,
            ["height"] = placement.Height,
            ["maximized"] = placement.Maximized,
        };
        var temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: losing the window position must never block closing the app.
            try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Finite coordinates and a positive finite size; anything else is a corrupt file.</summary>
    static bool IsUsable(WindowPlacement p) =>
        double.IsFinite(p.Left) && double.IsFinite(p.Top) &&
        double.IsFinite(p.Width) && double.IsFinite(p.Height) && p.Width > 0 && p.Height > 0;

    JsonObject? ReadRoot()
    {
        try
        {
            return File.Exists(FilePath) ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
