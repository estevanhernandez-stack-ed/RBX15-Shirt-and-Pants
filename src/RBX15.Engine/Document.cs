using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>One layer (makeLayer, editor.js:134-142). Images are shared, immutable decoded bitmaps.</summary>
public sealed record Layer
{
    public required int Id { get; init; }
    public required SKImage Image { get; init; }
    public string Name { get; init; } = "Layer";
    public bool Visible { get; init; } = true;
    public double Opacity { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double Scale { get; init; } = 1;
    public double Rotation { get; init; }
    public bool FlipH { get; init; }
    public bool FlipV { get; init; }
    public double Brightness { get; init; } = 100;
    public double Contrast { get; init; } = 100;
    public double Saturate { get; init; } = 100;
    public double HueRotate { get; init; }
    public int RShift { get; init; }
    public int GShift { get; init; }
    public int BShift { get; init; }
}

/// <summary>The whole editable document (editor.js:76-107 defaults). Immutable; every edit is a new record.</summary>
public sealed record Document
{
    public TemplateMode Mode { get; init; } = TemplateMode.Shirt;
    public string BgColor { get; init; } = "#192e44";
    public double BgOpacity { get; init; } = 1;
    public SKImage? BgImage { get; init; }
    public ImmutableList<(string Region, Fill Fill)> RegionFills { get; init; } = ImmutableList<(string, Fill)>.Empty;
    public ImmutableList<Layer> Layers { get; init; } = ImmutableList<Layer>.Empty;
    public bool ShowMask { get; init; } = true;
    public bool ShowGrid { get; init; }
}
