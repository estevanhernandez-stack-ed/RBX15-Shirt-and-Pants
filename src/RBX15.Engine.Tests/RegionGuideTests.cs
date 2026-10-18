using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>The view-only region guide (ViewOptions.ShowRegionGuide; ledger view-region-guide, new).</summary>
public class RegionGuideTests
{
    static SKColor At(SKImage image, int x, int y)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    static Document Transparent(TemplateMode mode = TemplateMode.Shirt) => new() { Mode = mode, BgOpacity = 0 };

    [Fact] // a transparent region reads differently from the gap beside it once the guide is on, and identically without it
    public void The_guide_marks_transparent_regions_and_leaves_the_gaps_alone()
    {
        var doc = Transparent();
        var off = new ViewOptions(1);
        using var plain = Renderer.RenderViewImage(doc, off);
        using var guided = Renderer.RenderViewImage(doc, off with { ShowRegionGuide = true });

        // torso_front's middle (231,74 128x128) and a pixel in the 2 px gap left of it (229-230)
        Assert.NotEqual(At(plain, 295, 138), At(guided, 295, 138));
        Assert.Equal(At(plain, 230, 138), At(guided, 230, 138));
        Assert.Equal(At(plain, 10, 10), At(guided, 10, 10));   // outside every region
    }

    [Fact] // opaque art covers the guide: it sits under the template
    public void Opaque_art_covers_the_guide()
    {
        var doc = DocumentOps.PaintRegion(Transparent(), "torso_front", Fill.Solid("#ff0000"));
        using var guided = Renderer.RenderViewImage(doc, new ViewOptions(1) { ShowRegionGuide = true });
        Assert.Equal(new SKColor(0xff, 0, 0), At(guided, 295, 138));
        Assert.Equal(new SKColor(0xff, 0, 0), At(guided, 231, 74));   // the outline's own pixel, under the fill
    }

    [Theory] // every region of either mode is outlined at its edge
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Every_region_gets_the_outline(TemplateMode mode)
    {
        var doc = Transparent(mode);
        using var plain = Renderer.RenderViewImage(doc, new ViewOptions(1));
        using var guided = Renderer.RenderViewImage(doc, new ViewOptions(1) { ShowRegionGuide = true });
        foreach (var r in Template.Regions(mode))
            Assert.NotEqual(At(plain, r.X, r.Y + r.H / 2), At(guided, r.X, r.Y + r.H / 2));
    }

    [Fact] // the guide is view-only: the export never has it
    public void The_export_is_unchanged()
    {
        using var export = Renderer.Export(Transparent());
        Assert.Equal(0, At(export, 295, 138).Alpha);
    }
}
