// usage: OutfitBuilder <recipe.json> <out-dir>
// A recipe names a base project and a list of engine ops (wordart, detail) with optional placement,
// and writes <name>.r15proj (ProjectIO.Write) and <name>.png (Renderer.ExportPng).
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

var recipe = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var baseDir = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
Directory.CreateDirectory(args[1]);

foreach (var outfit in recipe.EnumerateArray())
{
    string name = outfit.GetProperty("name").GetString()!;
    var load = ProjectIO.Read(File.ReadAllText(Path.Combine(baseDir, outfit.GetProperty("base").GetString()!)));
    if (!load.DroppedLayers.IsEmpty) throw new InvalidDataException($"{name}: dropped layers {string.Join(", ", load.DroppedLayers)}");
    var doc = load.Document;

    foreach (var op in outfit.GetProperty("ops").EnumerateArray())
    {
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        string Str(string k, string d) => op.TryGetProperty(k, out var v) ? v.GetString()! : d;
        float Num(string k, float d) => op.TryGetProperty(k, out var v) ? v.GetSingle() : d;
        Layer? layer = Str("op", "") switch
        {
            "wordart" => WordArt.CreateLayer(new WordArtSettings(Str("text", "TIDE"), Str("font", "Anton"),
                Enum.Parse<WordArtStyle>(Str("style", "Solid"), true), Num("size", 64), CssColor.ToSk(Str("fill", "#17d4fa")),
                CssColor.ToSk(Str("stroke", "#000000")), CssColor.ToSk(Str("grad", "#f22f89")), Num("strokeWidth", 3),
                Num("angle", 0)), doc.Mode, id),
            "detail" => Details.Create(Details.FromId(Str("type", "hem")), doc.Mode,
                new DetailStyle(CssColor.ToSk(Str("color", "#17d4fa")), Num("width", 3), op.TryGetProperty("stitch", out var s) && s.GetBoolean()), id),
            var other => throw new InvalidDataException($"unknown op {other}"),
        };
        if (layer is null) continue;
        // Placement: "w" fits the layer to a template width (scale = w / image width); "cx","cy" centre it; "x","y" set top-left.
        if (op.TryGetProperty("w", out var w)) layer = layer with { Scale = w.GetDouble() / layer.Image.Width };
        double lw = layer.Image.Width * layer.Scale, lh = layer.Image.Height * layer.Scale;
        if (op.TryGetProperty("cx", out var cx)) layer = layer with { X = cx.GetDouble() - lw / 2 };
        if (op.TryGetProperty("cy", out var cy)) layer = layer with { Y = cy.GetDouble() - lh / 2 };
        if (op.TryGetProperty("x", out var x)) layer = layer with { X = x.GetDouble() };
        if (op.TryGetProperty("y", out var y)) layer = layer with { Y = y.GetDouble() };
        if (op.TryGetProperty("rotation", out var r)) layer = layer with { Rotation = r.GetDouble() };
        if (op.TryGetProperty("layerName", out var n)) layer = layer with { Name = n.GetString()! };
        doc = doc with { Layers = doc.Layers.Add(layer) };
    }

    File.WriteAllText(Path.Combine(args[1], name + ".r15proj"), ProjectIO.Write(doc, new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)));
    File.WriteAllBytes(Path.Combine(args[1], name + ".png"), Renderer.ExportPng(doc));
    Console.WriteLine($"{name}: {doc.Layers.Count} layers");
}
