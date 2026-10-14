using SkiaSharp;

namespace RBX15.Engine;

public enum TemplateMode { Shirt, Pants }
public enum BodyPart { Body, RightLimb, LeftLimb }
public enum Face { Up, Right, Front, Left, Back, Down }

public readonly record struct Region(string Name, int X, int Y, int W, int H)
{
    public SKRect Rect => SKRect.Create(X, Y, W, H);
}

/// <summary>
/// The R15 classic template (editor.js:20-60). Shirt and Pants share geometry and differ only in
/// key names, which saved projects store verbatim. Semantic slots resolve a body part and face to
/// the right key per mode. 4.1 hardcoded torso_*/rarm_* keys, so Pants mode threw
/// (ledger mode-pants-crash-hardcoded-keys).
/// </summary>
public static class Template
{
    static readonly (BodyPart Part, Face Face, int X, int Y, int W, int H)[] Layout =
    {
        (BodyPart.Body, Face.Up, 231, 8, 128, 64),
        (BodyPart.Body, Face.Right, 165, 74, 64, 128),
        (BodyPart.Body, Face.Front, 231, 74, 128, 128),
        (BodyPart.Body, Face.Left, 361, 74, 64, 128),
        (BodyPart.Body, Face.Back, 427, 74, 128, 128),
        (BodyPart.Body, Face.Down, 231, 204, 128, 64),
        (BodyPart.RightLimb, Face.Up, 217, 289, 64, 64),
        (BodyPart.RightLimb, Face.Left, 19, 355, 64, 128),
        (BodyPart.RightLimb, Face.Back, 85, 355, 64, 128),
        (BodyPart.RightLimb, Face.Right, 151, 355, 64, 128),
        (BodyPart.RightLimb, Face.Front, 217, 355, 64, 128),
        (BodyPart.RightLimb, Face.Down, 217, 485, 64, 64),
        (BodyPart.LeftLimb, Face.Up, 308, 289, 64, 64),
        (BodyPart.LeftLimb, Face.Front, 308, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Left, 374, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Back, 440, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Right, 506, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Down, 308, 485, 64, 64),
    };

    static string Prefix(TemplateMode mode, BodyPart part) => (mode, part) switch
    {
        (TemplateMode.Shirt, BodyPart.Body) => "torso",
        (TemplateMode.Shirt, BodyPart.RightLimb) => "rarm",
        (TemplateMode.Shirt, BodyPart.LeftLimb) => "larm",
        (TemplateMode.Pants, BodyPart.Body) => "waist",
        (TemplateMode.Pants, BodyPart.RightLimb) => "rleg",
        _ => "lleg",
    };

    static Region[] Build(TemplateMode mode) =>
        Layout.Select(l => new Region($"{Prefix(mode, l.Part)}_{l.Face.ToString().ToLowerInvariant()}", l.X, l.Y, l.W, l.H))
              .ToArray();

    static readonly Region[] ShirtRegions = Build(TemplateMode.Shirt);
    static readonly Region[] PantsRegions = Build(TemplateMode.Pants);
    static readonly Lazy<SKImage> ShirtMask = new(() => BuildMask(ShirtRegions));
    static readonly Lazy<SKImage> PantsMask = new(() => BuildMask(PantsRegions));

    public static IReadOnlyList<Region> Regions(TemplateMode mode) => mode == TemplateMode.Shirt ? ShirtRegions : PantsRegions;

    public static bool TryFind(TemplateMode mode, string name, out Region region)
    {
        foreach (var r in Regions(mode))
            if (r.Name == name) { region = r; return true; }
        region = default;
        return false;
    }

    public static Region Slot(TemplateMode mode, BodyPart part, Face face)
    {
        int i = Array.FindIndex(Layout, l => l.Part == part && l.Face == face);
        return Regions(mode)[i];
    }

    /// <summary>buildMask (editor.js:116-126): white integer rects on transparent; export clips with destination-in.</summary>
    public static SKImage Mask(TemplateMode mode) => mode == TemplateMode.Shirt ? ShirtMask.Value : PantsMask.Value;

    static SKImage BuildMask(IEnumerable<Region> regions)
    {
        using var surface = SKSurface.Create(new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var white = new SKPaint { Color = SKColors.White };
        foreach (var r in regions) surface.Canvas.DrawRect(r.Rect, white);
        return surface.Snapshot();
    }
}
