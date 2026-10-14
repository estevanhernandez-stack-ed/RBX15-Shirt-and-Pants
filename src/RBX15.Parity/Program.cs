using RBX15.Parity;
using SkiaSharp;

if (args.Length >= 3 && args[0] == "render")
{
    var root = RBX15.Parity.RepoRoot.Find(Directory.GetCurrentDirectory());
    var scenes = RBX15.Parity.SceneLoader.LoadAll(Path.GetFullPath(args[1]), root);
    int only = Array.IndexOf(args, "--only");
    var names = only > 0 && only + 1 < args.Length ? args[only + 1].Split(',').ToHashSet() : null;
    Directory.CreateDirectory(args[2]);
    foreach (var scene in scenes.Where(s => names is null || names.Contains(s.Name)))
    {
        File.WriteAllBytes(Path.Combine(args[2], scene.Name + ".png"), RBX15.Engine.Renderer.ExportPng(scene.Document));
        Console.WriteLine($"rendered {scene.Name}");
    }
    return 0;
}

if (args.Length < 3 || args[0] != "diff")
{
    Console.Error.WriteLine("usage: RBX15.Parity diff <reference.png> <candidate.png> [--heatmap <out.png>] | render <scenes.json> <outDir> [--only a,b]");
    return 2;
}

using var reference = ImageDiff.Load(args[1]);
using var candidate = ImageDiff.Load(args[2]);
var r = ImageDiff.Compare(reference, candidate);
Console.WriteLine($"same-size={r.SameSize} max-delta={r.MaxDelta} differing={r.DifferingPixels} " +
                  $"interior={r.InteriorDifferences} edge-over-tolerance={r.EdgeOverTolerance} pass={r.Pass}");

int i = Array.IndexOf(args, "--heatmap");
if (i > 0 && i + 1 < args.Length && r.SameSize)
{
    using var heat = ImageDiff.Heatmap(reference, candidate);
    using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
    using var fs = File.Create(args[i + 1]);
    data.SaveTo(fs);
}
return r.Pass ? 0 : 1;
