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

    /// <summary>
    /// True when <paramref name="other"/> holds the same content: every scalar field equal,
    /// <see cref="BgImage"/> by reference, <see cref="RegionFills"/> sequence-equal (region key and
    /// <see cref="Fill"/> value equality) and <see cref="Layers"/> sequence-equal (<see cref="Layer"/>
    /// record equality, so each layer's image by reference). The compiler's record equality compares
    /// the two immutable lists by reference, so it calls two equal edits different. The editor session
    /// records an undo step only when this returns false (spec §5, ledger undo-empty-steps: 4.1 pushed
    /// a snapshot before no-op actions, editor.js:1117, :1601-1606, :1734-1749).
    /// </summary>
    public bool ContentEquals(Document other) =>
        other is not null &&
        (ReferenceEquals(this, other) ||
         Mode == other.Mode &&
         string.Equals(BgColor, other.BgColor, StringComparison.Ordinal) &&
         BgOpacity.Equals(other.BgOpacity) &&
         ReferenceEquals(BgImage, other.BgImage) &&
         ShowMask == other.ShowMask &&
         ShowGrid == other.ShowGrid &&
         RegionFills.SequenceEqual(other.RegionFills) &&
         Layers.SequenceEqual(other.Layers));
}
