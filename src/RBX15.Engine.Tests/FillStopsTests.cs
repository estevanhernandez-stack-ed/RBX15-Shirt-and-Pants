using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>Gradient color stops (ledger fill-gradient-stops): the ramp, the key, structural equality, the cap.</summary>
public class FillStopsTests
{
    static SKImage Paint(Fill f) => Px.Render(100, 40, c => Fills.PaintRegionFill(c, SKRect.Create(0, 0, 100, 40), f));
    static ImmutableArray<string> S(params string[] s) => [.. s];

    [Fact] // red, green, blue at angle 0: green in the middle, red and blue at the ends
    public void A_three_color_linear_gradient_peaks_green_at_the_middle()
    {
        var img = Paint(new Fill("linear", "#ff0000", "#0000ff", Angle: 0, Stops: S("#00ff00")));
        var left = Px.At(img, 1, 20);
        var mid = Px.At(img, 50, 20);
        var right = Px.At(img, 98, 20);
        Assert.True(left.Red > 230 && left.Green < 30 && left.Blue < 30, $"left {left}");
        Assert.True(mid.Green > 230 && mid.Red < 40 && mid.Blue < 40, $"mid {mid}");
        Assert.True(right.Blue > 230 && right.Red < 30 && right.Green < 30, $"right {right}");
    }

    [Fact] // stops sit at k/(n+1): two stops split the run in thirds
    public void Two_stops_sit_at_thirds()
    {
        var img = Paint(new Fill("linear", "#ff0000", "#000000", Angle: 0, Stops: S("#00ff00", "#0000ff")));
        Px.Near(new SKColor(0, 255, 0), Px.At(img, 33, 20), 12);
        Px.Near(new SKColor(0, 0, 255), Px.At(img, 66, 20), 12);
    }

    [Fact] // radial: c1 at the center, c2 at max(w,h)/2, the stop at half that radius
    public void A_radial_with_one_stop_has_the_stop_color_at_mid_radius()
    {
        var f = new Fill("radial", "#ff0000", "#0000ff", Stops: S("#00ff00"));
        var img = Px.Render(80, 80, c => Fills.PaintRegionFill(c, SKRect.Create(0, 0, 80, 80), f));
        Px.Near(new SKColor(0, 255, 0), Px.At(img, 60, 40), 14);   // 20 px from the center of an r=40 gradient
        var center = Px.At(img, 40, 40);
        Assert.True(center.Red > 230 && center.Green < 30, $"center {center}");
    }

    [Fact] // no stops: the same ramp 4.1 always built, offsets 0 and 1
    public void Without_stops_the_ramp_is_the_two_color_one()
    {
        var (colors, positions) = Fills.GradientRamp(new Fill("linear", "#ff0000", "#0000ff"), "#ff0000", "#0000ff");
        Assert.Equal(new[] { new SKColor(255, 0, 0), new SKColor(0, 0, 255) }, colors);
        Assert.Equal(new float[] { 0, 1 }, positions);
        var (_, three) = Fills.GradientRamp(new Fill("linear", Stops: S("#00ff00", "#ffffff", "#000000")), "#ff0000", "#0000ff");
        Assert.Equal(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }, three);
    }

    [Fact] // a fill with an empty or default stop list paints the same pixels as one built the 4.1 way
    public void Empty_stops_paint_exactly_as_no_stops()
    {
        var plain = new Fill("linear", "#ff0000", "#0000ff", Angle: 30, Scale: 16);
        var empty = plain with { Stops = ImmutableArray<string>.Empty };
        var a = Paint(plain);
        var b = Paint(empty);
        for (int y = 0; y < 40; y += 3)
            for (int x = 0; x < 100; x += 3)
                Assert.Equal(Px.At(a, x, y), Px.At(b, x, y));
    }

    [Fact] // other types ignore stops when painting
    public void Other_types_ignore_stops()
    {
        foreach (var type in new[] { "solid", "stripes", "checker", "dots", "camo" })
        {
            var plain = new Fill(type, "#ff0000", "#0000ff", Angle: 0, Scale: 8);
            var withStops = plain with { Stops = S("#00ff00") };
            var a = Paint(plain);
            var b = Paint(withStops);
            for (int y = 0; y < 40; y += 5)
                for (int x = 0; x < 100; x += 5)
                    Assert.Equal(Px.At(a, x, y), Px.At(b, x, y));
        }
    }

    [Fact] // 4.1 keys are untouched; stops append, and different stops are different keys
    public void The_key_is_4_1s_without_stops_and_appends_stops_with_them()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", 45, 16);
        Assert.Equal("linear|#ff0000|#0000ff|45|16", f.Key);
        Assert.Equal("linear|#ff0000|#0000ff|45|16", (f with { Stops = [] }).Key);
        Assert.Equal("linear|#ff0000|#0000ff|45|16|#00ff00,#ffffff", (f with { Stops = S("#00ff00", "#ffffff") }).Key);
        Assert.NotEqual((f with { Stops = S("#00ff00") }).Key, (f with { Stops = S("#00fe00") }).Key);
        Assert.NotEqual((f with { Stops = S("#00ff00", "#ffffff") }).Key, (f with { Stops = S("#ffffff", "#00ff00") }).Key);
    }

    [Fact] // a record compares a list member by reference; this one compares the colors, in order
    public void Equality_is_structural_for_stops()
    {
        var a = new Fill("linear", "#ff0000", "#0000ff", 0, 16, S("#00ff00", "#ffffff"));
        var b = new Fill("linear", "#ff0000", "#0000ff", 0, 16, S("#00ff00", "#ffffff"));   // another array, same colors
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a.Key, b.Key);
        Assert.NotEqual(a, a with { Stops = S("#00ff00") });
        Assert.NotEqual(a, a with { Stops = S("#ffffff", "#00ff00") });
        Assert.NotEqual(a, a with { Stops = [] });
        // none is none, whether default, empty or unset
        Assert.Equal(new Fill("linear", "#ff0000"), new Fill("linear", "#ff0000", Stops: default));
        Assert.Equal(new Fill("linear", "#ff0000"), new Fill("linear", "#ff0000") with { Stops = ImmutableArray<string>.Empty });
        Assert.Equal(new Fill("linear", "#ff0000").GetHashCode(), new Fill("linear", "#ff0000", Stops: []).GetHashCode());
        Assert.True(new Fill("linear", "#ff0000").Stops.IsEmpty);
    }

    [Fact] // the cap: three stops, five colors; a longer list is refused wherever it is set
    public void More_than_three_stops_are_refused()
    {
        Assert.Equal(3, Fill.MaxStops);
        var three = new Fill("linear", Stops: S("#111111", "#222222", "#333333"));
        Assert.Equal(3, three.Stops.Length);
        Assert.Throws<ArgumentException>(() => new Fill("linear", Stops: S("#111111", "#222222", "#333333", "#444444")));
        Assert.Throws<ArgumentException>(() => three with { Stops = S("#111111", "#222222", "#333333", "#444444") });
    }

    [Fact] // painting an equal fill (stops compared by value, not by array) changes nothing; a different stop does
    public void Painting_an_equal_stop_fill_is_a_no_op_and_a_changed_stop_replaces_it()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", Stops: S("#00ff00"));
        var doc = DocumentOps.PaintRegion(new Document(), "torso_front", f);
        Assert.Same(doc, DocumentOps.PaintRegion(doc, "torso_front", f with { Stops = S("#00ff00") }));
        var changed = DocumentOps.PaintRegion(doc, "torso_front", f with { Stops = S("#00fe00") });
        Assert.NotSame(doc, changed);
        Assert.False(doc.ContentEquals(changed));
        Assert.True(doc.ContentEquals(DocumentOps.PaintRegion(new Document(), "torso_front", f with { Stops = S("#00ff00") })));
    }

    [Fact] // applyFillEdit's rule matches by key: regions carrying the old stops follow an edit of them
    public void Retargeting_by_key_follows_the_stops()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", Stops: S("#00ff00"));
        var doc = DocumentOps.PaintRegion(DocumentOps.PaintRegion(new Document(), "torso_front", f), "torso_back", f with { Stops = [] });
        var next = f with { Stops = S("#ffffff", "#000000") };
        var after = DocumentOps.RetargetFill(doc, f.Key, next);
        Assert.Equal(next, DocumentOps.RegionFill(after, "torso_front"));
        Assert.Equal(f with { Stops = [] }, DocumentOps.RegionFill(after, "torso_back"));   // a different key stays
    }
}
