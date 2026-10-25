using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>A project file that cannot be opened. Nothing was applied; the message names the problem.</summary>
public sealed class ProjectFormatException : Exception
{
    public ProjectFormatException(string message) : base(message) { }

    public ProjectFormatException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// The result of <see cref="ProjectIO.Read"/>: the document, the next free layer id
/// (editor.js:2663), and the names of the layers that could not be loaded, in file order.
/// 4.1 selects the top layer after a load (editor.js:2664); the caller does the same with
/// <c>Document.Layers[^1]</c> when there is one.
/// </summary>
public sealed record ProjectLoad(Document Document, int NextId, ImmutableList<string> DroppedLayers);

/// <summary>
/// <c>.r15proj</c> read and write: the port of saveProject (editor.js:2550-2591) and loadProject
/// (editor.js:2593-2680), with the spec §3 divergences (ledger <c>file-project-format</c>,
/// <c>project-format-r15proj</c>, <c>project-load-untrusted-src</c>, <c>project-load-error-path-bugs</c>,
/// <c>project-load-layers</c>, <c>project-save-format</c>, <c>project-version-compat</c>):
/// <list type="bullet">
/// <item>Read is all-or-nothing. The whole file is parsed and validated before a Document exists, so a
/// structural problem throws <see cref="ProjectFormatException"/> and the caller's state is untouched.
/// 4.1 mutated mode, background and fills before the layers were even parsed (editor.js:2599-2619).</item>
/// <item>Image fields must be <c>data:</c> URLs with a base64 payload. A layer whose <c>imgSrc</c> is missing,
/// is not a data URL, or does not decode is dropped and named in <see cref="ProjectLoad.DroppedLayers"/>.
/// A <c>bgImageSrc</c> that is not a data URL refuses the whole file (every image 4.1 saves is a data
/// URL, so only a crafted file carries one); a data-URL background that does not decode is dropped and
/// reported as <see cref="BackgroundImageName"/>.</item>
/// <item>Write emits 4.1's field set and order with <c>version: "5.0"</c>; 4.1 never reads <c>version</c>.</item>
/// </list>
/// </summary>
public static class ProjectIO
{
    public const string AppName = "RBX15 Classic Shirt and Pants Maker";
    public const string Version = "5.0";

    /// <summary>The <see cref="ProjectLoad.DroppedLayers"/> entry for a background image that did not decode.</summary>
    internal const string BackgroundImageName = "Background image";

    // ---------------------------------------------------------------- read

    /// <summary>Parses and validates a whole project file. Throws <see cref="ProjectFormatException"/> on any structural problem.</summary>
    public static ProjectLoad Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ProjectFormatException($"the file is not JSON: {e.Message}", e);
        }
        using (parsed)
        {
            var p = parsed.RootElement;
            if (p.ValueKind != JsonValueKind.Object)
                throw new ProjectFormatException($"the file is a JSON {Kind(p)}, not a project object");

            // editor.js:2599 `if (project.mode) switchMode(project.mode)`: an absent or empty mode keeps the
            // editor's mode; a fresh v5 document is Shirt. 4.1 passed any other value through unvalidated
            // (ledger project-load-untrusted-src); v5 accepts only the two modes 4.1 ever writes.
            var mode = OptString(p, "mode", "mode") switch
            {
                null or "" => TemplateMode.Shirt,
                "Shirt" => TemplateMode.Shirt,
                "Pants" => TemplateMode.Pants,
                var m => throw new ProjectFormatException($"unknown mode \"{m}\" (expected \"Shirt\" or \"Pants\")"),
            };

            var bgColor = OptString(p, "bgColor", "bgColor");                      // editor.js:2601 `|| '#192e44'`
            double bgOpacity = OptNumber(p, "bgOpacity", "bgOpacity") ?? 1;        // editor.js:2602 `!= null ? : 1`
            var bgSrc = OptString(p, "bgImageSrc", "bgImageSrc");                  // editor.js:2606 truthy
            if (!string.IsNullOrEmpty(bgSrc) && !IsDataUrl(bgSrc))
                throw new ProjectFormatException($"bgImageSrc must be a data: URL, not \"{Clip(bgSrc)}\"");

            var fills = ReadRegionColors(p);                                       // editor.js:2615
            bool showMask = OptBool(p, "showMask", "showMask") ?? true;            // editor.js:2621 `!= null`
            bool showGrid = OptBool(p, "showGrid", "showGrid") ?? false;           // editor.js:2622 `!= null`
            var records = ReadLayerRecords(p);                                     // editor.js:2625-2673

            // Every structural check has passed. Only images remain, and a bad image drops, never throws.
            var dropped = ImmutableList.CreateBuilder<string>();
            SKImage? bgImage = null;
            if (!string.IsNullOrEmpty(bgSrc))
            {
                bgImage = TryDecodeDataUrl(bgSrc);
                if (bgImage is null) dropped.Add(BackgroundImageName);
            }

            // Layer ids come from the file. 4.1 kept duplicates and then sorted by findIndex, so their order
            // was unspecified (ledger project-load-layers, quirk 3). v5 keeps file order, the first holder
            // keeps the id, and each later duplicate takes the next id above every id in the file.
            long fresh = records.Count == 0 ? 0 : records.Max(r => (long)r.Id);
            var used = new HashSet<int>();
            var layers = ImmutableList.CreateBuilder<Layer>();
            try
            {
                foreach (var r in records)
                {
                    // editor.js:2632 skips a record without imgSrc; v5 names it, like any layer that cannot load.
                    var image = string.IsNullOrEmpty(r.Src) || !IsDataUrl(r.Src) ? null : TryDecodeDataUrl(r.Src);
                    if (image is null) { dropped.Add(r.Layer.Name); continue; }
                    int id = r.Id;
                    if (!used.Add(id))
                    {
                        if (++fresh >= int.MaxValue) { image.Dispose(); throw new ProjectFormatException("layer ids run past the largest id"); }
                        id = (int)fresh;
                        used.Add(id);
                    }
                    layers.Add(r.Layer with { Id = id, Image = image });
                }
            }
            catch
            {
                // Decoded images are native memory: free them before the refusal leaves.
                foreach (var l in layers) l.Image.Dispose();
                bgImage?.Dispose();
                throw;
            }

            // editor.js:2663: nextId = max(layer ids) + 1 over the layers that loaded; a fresh editor's is 1.
            int nextId = layers.Count == 0 ? 1 : layers.Max(l => l.Id) + 1;

            var document = new Document
            {
                Mode = mode,
                BgColor = string.IsNullOrEmpty(bgColor) ? "#192e44" : bgColor,
                BgOpacity = bgOpacity,
                BgImage = bgImage,
                RegionFills = fills,
                Layers = layers.ToImmutable(),
                ShowMask = showMask,
                ShowGrid = showGrid,
            };
            return new ProjectLoad(document, nextId, dropped.ToImmutable());
        }
    }

    sealed record LayerRecord(int Id, Layer Layer, string? Src);

    /// <summary>A 1x1 transparent placeholder; every record's image is replaced or the record dropped.</summary>
    static readonly SKImage Placeholder = SKImage.Create(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul));

    static List<LayerRecord> ReadLayerRecords(JsonElement p)
    {
        var list = new List<LayerRecord>();
        if (!p.TryGetProperty("layers", out var ls) || ls.ValueKind == JsonValueKind.Null) return list; // editor.js:2627 `|| []`
        if (ls.ValueKind != JsonValueKind.Array) throw new ProjectFormatException($"layers must be an array, not a {Kind(ls)}");
        int i = 0;
        foreach (var d in ls.EnumerateArray())
        {
            string at = $"layers[{i++}]";
            if (d.ValueKind != JsonValueKind.Object) throw new ProjectFormatException($"{at} must be an object, not a {Kind(d)}");
            if (!d.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number || !idEl.TryGetInt32(out int id) || id < 1)
                throw new ProjectFormatException($"{at}.id must be a positive whole number");
            if (id == int.MaxValue)   // nextId = max + 1 would wrap
                throw new ProjectFormatException($"{at}.id must be at most {int.MaxValue - 1}");

            // The coercions of editor.js:2636-2652, field for field. `||` maps 0 and "" to the default;
            // `!= null` keeps 0; `!== false` is true unless the value is false; `!!` is false unless true.
            string? name = OptString(d, "name", at + ".name");
            double? scale = OptNumber(d, "scale", at + ".scale");
            var layer = new Layer
            {
                Id = id,
                Image = Placeholder,
                Name = string.IsNullOrEmpty(name) ? "Layer" : name,                       // :2637 `|| 'Layer'`
                Visible = OptBool(d, "visible", at + ".visible") != false,                // :2638 `!== false`
                Opacity = OptNumber(d, "opacity", at + ".opacity") ?? 1,                  // :2639 `!= null`
                X = OptNumber(d, "x", at + ".x") ?? 0,                                    // :2640 `|| 0`
                Y = OptNumber(d, "y", at + ".y") ?? 0,                                    // :2641 `|| 0`
                Scale = scale is null or 0 ? 1 : scale.Value,                             // :2642 `|| 1`: 0 becomes 1
                Rotation = OptNumber(d, "rotation", at + ".rotation") ?? 0,               // :2643 `|| 0`
                FlipH = OptBool(d, "flipH", at + ".flipH") == true,                       // :2644 `!!`
                FlipV = OptBool(d, "flipV", at + ".flipV") == true,                       // :2645 `!!`
                Brightness = OptNumber(d, "brightness", at + ".brightness") ?? 100,       // :2646 `!= null`
                Contrast = OptNumber(d, "contrast", at + ".contrast") ?? 100,             // :2647 `!= null`
                Saturate = OptNumber(d, "saturate", at + ".saturate") ?? 100,             // :2648 `!= null`
                HueRotate = OptNumber(d, "hueRotate", at + ".hueRotate") ?? 0,            // :2649 `!= null`
                RShift = OptInt(d, "rShift", at + ".rShift") ?? 0,                        // :2650 `|| 0`
                GShift = OptInt(d, "gShift", at + ".gShift") ?? 0,                        // :2651 `|| 0`
                BShift = OptInt(d, "bShift", at + ".bShift") ?? 0,                        // :2652 `|| 0`
            };
            list.Add(new LayerRecord(id, layer, OptString(d, "imgSrc", at + ".imgSrc")));
        }
        return list;
    }

    /// <summary>
    /// regionColors (editor.js:2615 `project.regionColors || {}`): a legacy color string reads as a
    /// solid (lib/fills.js normalizeFill); a descriptor keeps its fields. A falsy value (null, false,
    /// 0, "") is skipped, as paintRegions skips it (editor.js:282). Keys stay verbatim, including keys
    /// the mode does not have: 4.1 kept and re-saved them, and the renderer skips them. A key that
    /// appears twice keeps its first position and its last value, as JSON.parse does.
    /// </summary>
    static ImmutableList<(string Region, Fill Fill)> ReadRegionColors(JsonElement p)
    {
        var result = new List<(string Region, Fill Fill)>();
        if (!p.TryGetProperty("regionColors", out var rc) || rc.ValueKind == JsonValueKind.Null) return [.. result];
        if (rc.ValueKind != JsonValueKind.Object) throw new ProjectFormatException($"regionColors must be an object, not a {Kind(rc)}");
        foreach (var e in rc.EnumerateObject())
        {
            string at = $"regionColors.{e.Name}";
            var v = e.Value;
            Fill? fill = v.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.False => null,
                JsonValueKind.Number when v.GetDouble() == 0 => null,
                JsonValueKind.String => v.GetString() is { Length: > 0 } s ? Fill.Solid(s) : null,
                JsonValueKind.Object => new Fill(
                    OptString(v, "type", at + ".type") ?? "solid",
                    OptString(v, "c1", at + ".c1"), OptString(v, "c2", at + ".c2"),
                    OptNumber(v, "angle", at + ".angle"), OptNumber(v, "scale", at + ".scale"),
                    OptStops(v, at + ".stops")),
                _ => throw new ProjectFormatException($"{at} must be a color string or a fill object, not a {Kind(v)}"),
            };
            int existing = result.FindIndex(r => r.Region == e.Name);
            if (existing >= 0) result.RemoveAt(existing);
            if (fill is null) continue;
            if (existing >= 0) result.Insert(Math.Min(existing, result.Count), (e.Name, fill));
            else result.Add((e.Name, fill));
        }
        return [.. result];
    }

    /// <summary>
    /// A fill's optional <c>stops</c> (v5 only; 4.1 never wrote the key): an array of at most
    /// <see cref="Fill.MaxStops"/> color strings, <c>#rgb</c> or <c>#rrggbb</c>, kept as written.
    /// Absent or null reads as none; a bad entry names its index, as <c>regionColors.name.stops[1]</c>.
    /// </summary>
    static ImmutableArray<string> OptStops(JsonElement o, string what)
    {
        if (!o.TryGetProperty("stops", out var v) || v.ValueKind == JsonValueKind.Null) return ImmutableArray<string>.Empty;
        if (v.ValueKind != JsonValueKind.Array) throw new ProjectFormatException($"{what} must be an array, not a {Kind(v)}");
        if (v.GetArrayLength() > Fill.MaxStops)
            throw new ProjectFormatException($"{what} holds {v.GetArrayLength()} colors; a fill carries at most {Fill.MaxStops}");
        var stops = ImmutableArray.CreateBuilder<string>(v.GetArrayLength());
        int i = 0;
        foreach (var e in v.EnumerateArray())
        {
            string at = $"{what}[{i++}]";
            if (e.ValueKind != JsonValueKind.String) throw new ProjectFormatException($"{at} must be a color string, not a {Kind(e)}");
            string s = e.GetString()!;
            if (!IsHexColor(s)) throw new ProjectFormatException($"{at} must be a #rgb or #rrggbb color, not \"{Clip(s)}\"");
            stops.Add(s);
        }
        return stops.MoveToImmutable();
    }

    static bool IsHexColor(string s) =>
        s.Length is 4 or 7 && s[0] == '#' && s.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    static string? OptString(JsonElement o, string key, string what) =>
        !o.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null ? null
        : v.ValueKind == JsonValueKind.String ? v.GetString()
        : throw new ProjectFormatException($"{what} must be a string, not a {Kind(v)}");

    static double? OptNumber(JsonElement o, string key, string what)
    {
        if (!o.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.Number) throw new ProjectFormatException($"{what} must be a number, not a {Kind(v)}");
        if (!v.TryGetDouble(out double d) || !double.IsFinite(d)) throw new ProjectFormatException($"{what} is out of range");
        return d;
    }

    static int? OptInt(JsonElement o, string key, string what)
    {
        if (OptNumber(o, key, what) is not { } d) return null;
        if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw new ProjectFormatException($"{what} must be a whole number");
        return (int)d;
    }

    static bool? OptBool(JsonElement o, string key, string what) =>
        !o.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null ? null
        : v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean()
        : throw new ProjectFormatException($"{what} must be true or false, not a {Kind(v)}");

    static string Kind(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => "null",
    };

    static string Clip(string s) => s.Length <= 40 ? s : s[..40] + "...";

    static bool IsDataUrl(string s) => s.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decodes <c>data:[&lt;type&gt;][;params];base64,&lt;payload&gt;</c>. The media type is not trusted:
    /// the bytes are sniffed by the codec, as an img element does. Null when the URL is not base64,
    /// the payload is not valid base64, or the bytes are not an image the engine decodes (an SVG data
    /// URL, which 4.1 could hold, is one; spec §3 drops SVG).
    /// </summary>
    static SKImage? TryDecodeDataUrl(string url)
    {
        int comma = url.IndexOf(',');
        if (comma < 0) return null;
        var header = url.AsSpan(5, comma - 5);
        if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(url[(comma + 1)..]); }
        catch (FormatException) { return null; }
        try { return Images.Decode(bytes); }
        catch (Exception e) when (e is InvalidDataException or ImageTooLargeException) { return null; }
    }

    // ---------------------------------------------------------------- write

    /// <summary>
    /// Serializes <paramref name="doc"/> with saveProject's fields in saveProject's order
    /// (editor.js:2551-2582), <c>version</c> "5.0", <c>savedAt</c> as JS toISOString of
    /// <paramref name="savedAt"/> in UTC, every image as a PNG data URL, JSON.stringify's 2-space
    /// indent and LF line breaks (editor.js:2583). Deterministic for a given savedAt.
    /// <para>
    /// A non-null <paramref name="title"/> adds a <c>title</c> field after <c>layers</c>, so 4.1's
    /// field order stays an exact prefix; 4.1 ignores unknown fields (ledger project-template-name-not-saved).
    /// Region fills are written as descriptors with only the fields they carry, in lib/fills.js order
    /// (type, c1, c2, angle, scale), then <c>stops</c> only when the fill has any (v5; 4.1 ignores the key); a legacy color string read from a 4.1 file comes back as
    /// <c>{ "type": "solid", "c1": ... }</c>, which 4.1 reads identically (lib/fills.js normalizeFill).
    /// </para>
    /// </summary>
    public static string Write(Document doc, DateTimeOffset savedAt, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var buffer = new MemoryStream();
        var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using (var w = new Utf8JsonWriter(buffer, options))
        {
            w.WriteStartObject();
            w.WriteString("version", Version);
            w.WriteString("app", AppName);
            w.WriteString("savedAt", savedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            w.WriteString("mode", doc.Mode == TemplateMode.Pants ? "Pants" : "Shirt");
            w.WriteString("bgColor", doc.BgColor);
            Number(w, "bgOpacity", doc.BgOpacity);
            if (doc.BgImage is { } bg) w.WriteString("bgImageSrc", PngDataUrl(bg));
            else w.WriteNull("bgImageSrc");

            w.WriteStartObject("regionColors");
            foreach (var (region, fill) in MergeLastWins(doc.RegionFills))
            {
                w.WriteStartObject(region);
                w.WriteString("type", fill.Type);
                if (fill.C1 is { } c1) w.WriteString("c1", c1);
                if (fill.C2 is { } c2) w.WriteString("c2", c2);
                if (fill.Angle is { } angle) Number(w, "angle", angle);
                if (fill.Scale is { } scale) Number(w, "scale", scale);
                if (!fill.Stops.IsEmpty)
                {
                    w.WriteStartArray("stops");
                    foreach (var stop in fill.Stops) w.WriteStringValue(stop);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndObject();

            w.WriteBoolean("showMask", doc.ShowMask);
            w.WriteBoolean("showGrid", doc.ShowGrid);

            w.WriteStartArray("layers");
            foreach (var l in doc.Layers)
            {
                w.WriteStartObject();
                w.WriteNumber("id", l.Id);
                w.WriteString("name", l.Name);
                w.WriteBoolean("visible", l.Visible);
                Number(w, "opacity", l.Opacity);
                Number(w, "x", l.X);
                Number(w, "y", l.Y);
                Number(w, "scale", l.Scale);
                Number(w, "rotation", l.Rotation);
                w.WriteBoolean("flipH", l.FlipH);
                w.WriteBoolean("flipV", l.FlipV);
                Number(w, "brightness", l.Brightness);
                Number(w, "contrast", l.Contrast);
                Number(w, "saturate", l.Saturate);
                Number(w, "hueRotate", l.HueRotate);
                w.WriteNumber("rShift", l.RShift);
                w.WriteNumber("gShift", l.GShift);
                w.WriteNumber("bShift", l.BShift);
                w.WriteString("imgSrc", PngDataUrl(l.Image));
                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (title is not null) w.WriteString("title", title);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>JSON.stringify writes -0 as 0 and has no NaN or Infinity (it writes null); neither does a project.</summary>
    static void Number(Utf8JsonWriter w, string key, double v)
    {
        if (!double.IsFinite(v)) { w.WriteNull(key); return; }
        w.WriteNumber(key, v == 0 ? 0 : v);
    }

    /// <summary>A JS object keeps a key's first position and its last value.</summary>
    static IEnumerable<(string Region, Fill Fill)> MergeLastWins(IEnumerable<(string Region, Fill Fill)> fills)
    {
        var order = new List<string>();
        var last = new Dictionary<string, Fill>(StringComparer.Ordinal);
        foreach (var (region, fill) in fills)
        {
            if (!last.ContainsKey(region)) order.Add(region);
            last[region] = fill;
        }
        return order.Select(r => (r, last[r]));
    }

    /// <summary>
    /// Each image's data URL, cached per <see cref="SKImage"/> (M5 item 2; M2b carry-forward): a 60 s autosave of an unchanged
    /// 20-layer document re-encodes nothing, and a Save after an autosave reuses it. Images are immutable, so the cache cannot
    /// go stale; an entry lives exactly as long as its image object.
    /// </summary>
    static readonly ConditionalWeakTable<SKImage, string> PngUrls = new();

    /// <summary>PNG encodes done by the calling thread, for the tests' counter seam; thread-static so parallel tests do not count each other's.</summary>
    [ThreadStatic]
    internal static int PngEncodesOnThisThread;

    static string PngDataUrl(SKImage image) => PngUrls.GetValue(image, EncodePngDataUrl);

    static string EncodePngDataUrl(SKImage image)
    {
        PngEncodesOnThisThread++;
        using var data = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("PNG encode failed");
        return "data:image/png;base64," + Convert.ToBase64String(data.AsSpan());
    }
}
