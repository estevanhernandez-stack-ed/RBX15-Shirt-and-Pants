using RBX15.Parity;
using SkiaSharp;

if (args.Length < 3 || args[0] != "diff")
{
    Console.Error.WriteLine("usage: RBX15.Parity diff <reference.png> <candidate.png> [--heatmap <out.png>]");
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
