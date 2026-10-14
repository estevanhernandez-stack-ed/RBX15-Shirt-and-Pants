using SkiaSharp;

namespace RBX15.Engine.Tests;

public class FillsTests
{
    static readonly SKRect R40 = SKRect.Create(0, 0, 40, 40);
    static SKImage Paint(Fill f, SKRect? region = null) => Px.Render(40, 40, c => Fills.PaintRegionFill(c, region ?? R40, f));
    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255), Green = new(0, 255, 0);

    [Fact]
    public void Legacy_string_fill_paints_solid() => Assert.Equal(Red, Px.At(Paint(Fill.Solid("#ff0000")), 20, 20));

    [Fact]
    public void Solid_fills_the_whole_region()
    {
        var img = Paint(new Fill("solid", "#00ff00"));
        Assert.Equal(Green, Px.At(img, 5, 5));
        Assert.Equal(Green, Px.At(img, 35, 35));
    }

    [Fact]
    public void Linear_gradient_runs_c1_to_c2()
    {
        var img = Paint(new Fill("linear", "#ff0000", "#0000ff", Angle: 0));
        var left = Px.At(img, 1, 20);
        var right = Px.At(img, 39, 20);
        Assert.True(left.Red > 200 && left.Blue < 60, $"left {left}");
        Assert.True(right.Blue > 200 && right.Red < 60, $"right {right}");
    }

    [Fact]
    public void Stripes_alternate_on_the_global_lattice()
    {
        var img = Paint(new Fill("stripes", "#ff0000", "#0000ff", Angle: 0, Scale: 8));
        Assert.Equal(Blue, Px.At(img, 2, 20));
        Assert.Equal(Red, Px.At(img, 11, 20));
    }

    [Fact]
    public void Patterns_are_continuous_across_adjacent_regions()
    {
        var f = new Fill("dots", "#ff0000", "#0000ff", Scale: 16);
        var img = Px.Render(80, 40, c =>
        {
            Fills.PaintRegionFill(c, SKRect.Create(0, 0, 40, 40), f);
            Fills.PaintRegionFill(c, SKRect.Create(40, 0, 40, 40), f);
        });
        Assert.Equal(Blue, Px.At(img, 56, 8));
        Assert.Equal(Red, Px.At(img, 48, 8));
    }

    [Fact]
    public void Checker_alternates() =>
        Assert.NotEqual(Px.At(Paint(new Fill("checker", "#ff0000", "#0000ff", Scale: 10)), 5, 5),
                        Px.At(Paint(new Fill("checker", "#ff0000", "#0000ff", Scale: 10)), 15, 5));

    [Fact]
    public void Dots_put_c2_at_centers_and_c1_in_gaps()
    {
        var img = Paint(new Fill("dots", "#ff0000", "#0000ff", Scale: 16));
        Assert.Equal(Blue, Px.At(img, 8, 8));
        Assert.Equal(Red, Px.At(img, 0, 0));
    }

    [Fact]
    public void Camo_is_deterministic()
    {
        var f = new Fill("camo", "#33421a", "#7a8a4a", Scale: 14);
        var a = Paint(f);
        var b = Paint(f);
        foreach (var (x, y) in new[] { (5, 5), (20, 20), (33, 12), (10, 30) })
            Assert.Equal(Px.At(a, x, y), Px.At(b, x, y));
    }

    [Fact]
    public void Fill_is_clipped_to_the_region()
    {
        var img = Paint(new Fill("solid", "#00ff00"), SKRect.Create(10, 10, 10, 10));
        Assert.Equal(Green, Px.At(img, 15, 15));
        Assert.Equal(0, Px.At(img, 2, 2).Alpha);
    }

    [Fact]
    public void Linear_gradient_spans_a_shared_extent()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", Angle: 0);
        var extent = SKRect.Create(0, 0, 80, 20);
        var img = Px.Render(80, 20, c =>
        {
            Fills.PaintRegionFill(c, SKRect.Create(0, 0, 40, 20), f, extent);
            Fills.PaintRegionFill(c, SKRect.Create(40, 0, 40, 20), f, extent);
        });
        var a = Px.At(img, 38, 10);
        var b = Px.At(img, 42, 10);
        Assert.True(Math.Abs(a.Red - b.Red) < 25 && Math.Abs(a.Blue - b.Blue) < 25, $"{a} vs {b}");
    }

    [Fact]
    public void Without_an_extent_each_region_restarts()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", Angle: 0);
        var img = Px.Render(80, 20, c =>
        {
            Fills.PaintRegionFill(c, SKRect.Create(0, 0, 40, 20), f);
            Fills.PaintRegionFill(c, SKRect.Create(40, 0, 40, 20), f);
        });
        Assert.True(Math.Abs(Px.At(img, 38, 10).Blue - Px.At(img, 42, 10).Blue) > 150);
    }

    [Fact]
    public void Fill_key_matches_fills_js()
    {
        Assert.Equal("solid|#aabbcc||0|0", Fill.Solid("#aabbcc").Key);
        Assert.Equal("dots|#ff0000|#0000ff|0|16", new Fill("dots", "#ff0000", "#0000ff", Scale: 16).Key);
        Assert.Equal(new Fill("linear", "#000000", "#ffffff", 45).Key, new Fill("linear", "#000000", "#ffffff", 45).Key);
        Assert.NotEqual(new Fill("linear", "#000000", "#ffffff", 45).Key, new Fill("linear", "#000000", "#ffffff", 90).Key);
    }

    [Fact]
    public void Prng_matches_fills_js()
    {
        Assert.Equal(1865758740u, Fills.SeedFrom("#556b2f#c2b28016_-1_3"));
        var r = Fills.Seeded(12345);
        Assert.Equal(0.97972826776094735, r());
        Assert.Equal(0.30675226449966431, r());
        Assert.Equal(0.48420542152598500, r());
    }

    [Fact]
    public void Mix_matches_fills_js()
    {
        Assert.Equal("#800080", CssColor.Mix("#ff0000", "#0000ff", 0.5));
        Assert.Equal("#37461f", CssColor.Mix("#556b2f", "#000000", 0.35));
        Assert.Equal((0xaa, 0xbb, 0xcc), CssColor.HexToRgb("#abc"));
    }

    [Fact]
    public void Effective_scale_follows_fills_js()
    {
        Assert.Equal(16, Fills.EffectiveScale(new Fill("dots")));
        Assert.Equal(16, Fills.EffectiveScale(new Fill("dots", Scale: 0)));
        Assert.Equal(2, Fills.EffectiveScale(new Fill("dots", Scale: 1)));
        Assert.Equal(2, Fills.EffectiveScale(new Fill("dots", Scale: -5)));
        Assert.Equal(24, Fills.EffectiveScale(new Fill("dots", Scale: 24)));
    }

    [Fact]
    public void Shirt_regions_group_into_torso_and_two_arms()
    {
        var groups = Fills.ConnectedRegionGroups(Template.Regions(TemplateMode.Shirt).Select(r => r.Rect).ToList());
        Assert.Equal(new[] { 6, 6, 6 }, groups.Select(g => g.Count).OrderBy(n => n));
    }

    [Fact]
    public void Fills_for_regions_the_mode_lacks_are_skipped()
    {
        var img = Px.Render(585, 559, c =>
            Fills.PaintRegions(c, TemplateMode.Pants, new List<(string, Fill)> { ("torso_front", Fill.Solid("#ff0000")) }));
        Assert.Equal(0, Px.At(img, 295, 138).Alpha);
    }
}
