using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// <para>
/// Scene format v2 adds an ordered <c>actions</c> array, run after the scene's own layers exist.
/// The loader runs only the ops the engine implements (SupportedOps). A scene holding any other op
/// refuses to parse, so no test ever compares a half-built document against a golden.
/// </para>
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    /// <summary>Ops the engine runs. Each engine task that implements an op adds it here.</summary>
    static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape" };

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    /// <summary>The scene's action ops the engine does not run yet, each once, in first-use order.</summary>
    public static IReadOnlyList<string> UnsupportedOps(JsonElement scene)
    {
        var ops = new List<string>();
        if (scene.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
            {
                var op = action.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                if (!SupportedOps.Contains(op) && !ops.Contains(op)) ops.Add(op);
            }
        return ops;
    }

    public static Scene Parse(JsonElement e, string repoRoot)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

        var name = Str(e, "name") ?? throw new InvalidDataException("scene without a name");
        var unsupported = UnsupportedOps(e);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"scene {name}: ops the engine does not run yet: {string.Join(", ", unsupported)}");

        var fills = ImmutableList.CreateBuilder<(string, Fill)>();
        if (e.TryGetProperty("regionColors", out var rc))
            foreach (var p in rc.EnumerateObject())
                fills.Add((p.Name, p.Value.ValueKind == JsonValueKind.String
                    ? Fill.Solid(p.Value.GetString()!)
                    : new Fill(Str(p.Value, "type") ?? "solid", Str(p.Value, "c1"), Str(p.Value, "c2"), Num(p.Value, "angle"), Num(p.Value, "scale"))));

        var layers = ImmutableList.CreateBuilder<Layer>();
        if (e.TryGetProperty("layers", out var ls))
        {
            int i = 0;
            foreach (var l in ls.EnumerateArray())
            {
                i++;
                layers.Add(new Layer
                {
                    Id = i, Name = "L" + i, Image = Image(Str(l, "image")!),
                    Visible = Bool(l, "visible") ?? true, Opacity = Num(l, "opacity") ?? 1,
                    X = Num(l, "x") ?? 0, Y = Num(l, "y") ?? 0, Scale = Num(l, "scale") ?? 1, Rotation = Num(l, "rotation") ?? 0,
                    FlipH = Bool(l, "flipH") ?? false, FlipV = Bool(l, "flipV") ?? false,
                    Brightness = Num(l, "brightness") ?? 100, Contrast = Num(l, "contrast") ?? 100,
                    Saturate = Num(l, "saturate") ?? 100, HueRotate = Num(l, "hueRotate") ?? 0,
                    RShift = (int)(Num(l, "rShift") ?? 0), GShift = (int)(Num(l, "gShift") ?? 0), BShift = (int)(Num(l, "bShift") ?? 0),
                });
            }
        }

        var bgImage = Str(e, "bgImage");
        var document = new Document
        {
            Mode = Str(e, "mode") == "Pants" ? TemplateMode.Pants : TemplateMode.Shirt,
            BgColor = Str(e, "bgColor") ?? "#192e44",
            BgOpacity = Num(e, "bgOpacity") ?? 1,
            BgImage = bgImage is null ? null : Image(bgImage),
            RegionFills = fills.ToImmutable(),
            Layers = layers.ToImmutable(),
        };

        if (e.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
                document = action.GetProperty("op").GetString() switch
                {
                    "shape" => ApplyShape(document, action),
                    var op => throw new InvalidDataException($"scene {name}: op '{op}' has no handler"),
                };

        return new Scene(name, document);
    }

    /// <summary>
    /// A shape gesture from <c>from</c> to <c>to</c>, committed as 4.1 commits it: a new drawing layer
    /// on top, with the next id after every layer already there (editor.js:1285, :528-540).
    /// tool, from, to, color and width are required; fill defaults to false (drawFill, editor.js:92).
    /// </summary>
    static Document ApplyShape(Document doc, JsonElement a)
    {
        var tool = a.GetProperty("tool").GetString() switch
        {
            "line" => ShapeTool.Line,
            "rect" => ShapeTool.Rect,
            "ellipse" => ShapeTool.Ellipse,
            var t => throw new InvalidDataException($"unknown shape tool '{t}'"),
        };
        SKPoint Pt(string key)
        {
            var p = a.GetProperty(key);
            return new SKPoint(p[0].GetSingle(), p[1].GetSingle());
        }
        bool fill = a.TryGetProperty("fill", out var f) && f.GetBoolean();
        var stroke = new Stroke(tool, new[] { Pt("from"), Pt("to") },
            CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), fill);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Tools.CommitDrawing(stroke, id, Tools.DrawingName(tool));
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }
}
