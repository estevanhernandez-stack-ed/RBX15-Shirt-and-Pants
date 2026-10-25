using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Placement is 4.1's toolbar placement buttons (editor.js:1325-1392), the presets (editor.js:1730-1760) and
/// "Center on front torso" (editor.js:854-859). Literals are 4.1's own doubles: node evaluated the same
/// arithmetic and printed them. Ledger rows btn-fit-front, btn-fit-fb, btn-stretch, btn-allover, btn-scatter,
/// preset-meme, preset-allover, preset-sleeve-pop, preset-mirror, preset-scattered, preset-fullbleed, props-center-front.
/// </summary>
public class PlacementTests
{
    const double Tol = 1e-9;

    static SKImage Img(int w, int h) => Px.Solid(w, h, SKColors.Red);

    static Layer L(int id, int w, int h, string name = "Art") => new() { Id = id, Image = Img(w, h), Name = name };

    static EditorSnapshot Snap(Layer layer, TemplateMode mode = TemplateMode.Shirt, params Layer[] others) =>
        new(new Document { Mode = mode, Layers = [.. others, layer] }, layer.Id);

    static Func<int> Ids(int first)
    {
        int next = first;
        return () => next++;
    }

    static void Near(double expected, double actual) => Assert.InRange(actual, expected - Tol, expected + Tol);

    static Layer Of(Document d, int id) => d.Layers.Single(l => l.Id == id);

    // ---- btn-fit-front, preset-meme

    [Theory] // 200x100: min(128/200, 128/100) * 0.9 = 0.576, x 231 + (128 - 115.2) / 2, y 74 + (128 - 57.6) / 2
    [InlineData(200, 100, TemplateMode.Shirt, 0.5760000000000001, 237.39999999999998, 109.19999999999999)]
    [InlineData(300, 200, TemplateMode.Shirt, 0.384, 237.4, 99.6)] // preset-meme: 0.384
    [InlineData(512, 512, TemplateMode.Shirt, 0.225, 237.4, 80.4)]
    [InlineData(33, 77, TemplateMode.Shirt, 1.4961038961038962, 270.3142857142857, 80.4)]
    [InlineData(200, 100, TemplateMode.Pants, 0.5760000000000001, 237.39999999999998, 109.19999999999999)] // waist_front, same geometry
    [InlineData(300, 200, TemplateMode.Pants, 0.384, 237.4, 99.6)]
    public void Fit_front_scales_to_90_percent_of_the_front_slot_centered(int w, int h, TemplateMode mode, double scale, double x, double y)
    {
        var s = Snap(L(1, w, h) with { Rotation = 33 }, mode);
        var l = Of(Placement.FitFront(s.Document, 1), 1);
        Near(scale, l.Scale);
        Near(x, l.X);
        Near(y, l.Y);
        Assert.Equal(0, l.Rotation);
    }

    [Fact] // flips, opacity, adjustments and name are unchanged; nothing else moves
    public void Fit_front_changes_only_scale_position_and_rotation()
    {
        var before = L(2, 200, 100) with { FlipH = true, FlipV = true, Opacity = 0.4, Brightness = 150, RShift = 20, Visible = false, Name = "Keep" };
        var other = L(1, 10, 10);
        var doc = new Document { Layers = [other, before] };
        var after = Placement.FitFront(doc, 2);
        Assert.Same(other, after.Layers[0]);
        var l = after.Layers[1];
        Assert.Equal(before with { Scale = l.Scale, X = l.X, Y = l.Y, Rotation = 0 }, l);
    }

    [Fact] // a layer already fitted gives the same document, so the session records no step
    public void Fit_front_twice_changes_nothing_the_second_time()
    {
        var once = Placement.FitFront(Snap(L(1, 200, 100)).Document, 1);
        Assert.Same(once, Placement.FitFront(once, 1));
    }

    // ---- btn-fit-fb, preset-mirror

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Front_and_back_fits_the_front_and_clones_to_the_back_at_the_same_scale(TemplateMode mode)
    {
        var src = L(1, 200, 100) with { Rotation = 45, FlipH = true, Opacity = 0.5, Contrast = 120 };
        var s = Snap(src, mode, L(9, 5, 5, "Under"));
        var next = Placement.FrontAndBack(s, 1, 20);

        Assert.Equal(new[] { 9, 1, 20 }, next.Document.Layers.Select(l => l.Id));
        Assert.Equal(1, next.SelectedLayerId); // the original stays selected
        var front = Of(next.Document, 1);
        var back = Of(next.Document, 20);
        Near(0.5760000000000001, front.Scale);
        Near(237.4, front.X);
        Near(109.2, front.Y);
        Assert.Equal(0, front.Rotation);
        Assert.Equal("Art (back)", back.Name);
        Near(front.Scale, back.Scale);
        Near(427 + (128 - 200 * front.Scale) / 2, back.X); // 4.1's parity check
        Near(74 + (128 - 100 * front.Scale) / 2, back.Y);
        Assert.Equal(0, back.Rotation);
        Assert.True(back.FlipH); // not mirrored: the flip the original carried is all there is
        Assert.Equal((0.5, 120.0), (back.Opacity, back.Contrast)); // spread copy keeps every property
        Assert.Same(src.Image, back.Image); // shared image
        Assert.Same(src.Image, front.Image);
    }

    // ---- btn-stretch

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Stretch_box_is_the_union_of_the_right_front_and_left_slots(TemplateMode mode)
    {
        // 4.1 hard-coded (165, 74, 260, 128). Shirt and Pants share geometry, so the slot union is that box in both.
        var box = Placement.StretchBox(mode);
        Assert.Equal((165, 74, 260, 128), (box.X, box.Y, box.W, box.H));
    }

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Stretch_fits_300x100_into_the_combined_box_without_a_margin(TemplateMode mode)
    {
        var s = Snap(L(1, 300, 100) with { Rotation = 12 }, mode);
        var next = Placement.Stretch(s, 1, 2);
        var l = Of(next.Document, 1);
        Near(0.8666666666666667, l.Scale); // ledger: 0.8667
        Near(165, l.X);
        Near(94.66666666666666, l.Y); // ledger: 94.67
        Assert.Equal(0, l.Rotation);
        // the back clone is a full fit to Body Back, no 0.9: min(128/300, 128/100) = 0.42667
        var back = Of(next.Document, 2);
        Assert.Equal("Art (back)", back.Name);
        Near(0.4266666666666667, back.Scale);
        Near(427, back.X);
        Near(116.66666666666666, back.Y);
        Assert.Equal(0, back.Rotation);
        Assert.Equal(new[] { 1, 2 }, next.Document.Layers.Select(x => x.Id));
        Assert.Equal(1, next.SelectedLayerId);
    }

    // ---- btn-allover, preset-allover

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Allover_adds_one_tile_per_region_in_table_order(TemplateMode mode)
    {
        var src = L(1, 40, 30) with { Rotation = 20, FlipV = true, Opacity = 0.7, Saturate = 50 };
        var s = Snap(src, mode);
        var next = Placement.Allover(s, 1, Ids(100));
        var regions = Template.Regions(mode);

        Assert.Equal(18, regions.Count); // 4.1's table holds 18 entries too (rarm_up/down, larm_up/down included)
        Assert.Equal(19, next.Document.Layers.Count);
        Assert.Equal(1, next.SelectedLayerId);
        Assert.Same(src, next.Document.Layers[0]); // the original is untouched, below the tiles
        for (int i = 0; i < 18; i++)
        {
            var r = regions[i];
            var t = next.Document.Layers[i + 1];
            double sc = Math.Min((double)r.W / 40, (double)r.H / 30) * 0.8;
            Assert.Equal(100 + i, t.Id);
            Assert.Equal("Art tile", t.Name);
            Near(sc, t.Scale);
            Near(r.X + (r.W - 40 * sc) / 2, t.X);
            Near(r.Y + (r.H - 30 * sc) / 2, t.Y);
            Assert.Equal(0, t.Rotation);
            Assert.True(t.FlipV);
            Assert.Equal((0.7, 50.0), (t.Opacity, t.Saturate));
            Assert.Same(src.Image, t.Image);
        }
    }

    [Fact] // node: torso_up 128x64, 40x30 layer
    public void Allover_first_tile_matches_4_1s_doubles()
    {
        var next = Placement.Allover(Snap(L(1, 40, 30)), 1, Ids(2));
        var t = Of(next.Document, 2);
        Near(1.7066666666666668, t.Scale);
        Near(260.8666666666667, t.X);
        Near(14.399999999999999, t.Y);
    }

    // ---- btn-scatter, preset-scattered

    /// <summary>Returns 0.001, 0.002, ... so each draw's position in the sequence shows in the result.</summary>
    sealed class CountingRandom : Random
    {
        public int Draws;
        public override double NextDouble() => (++Draws) / 1000.0;
    }

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Scatter_adds_26_copies_within_4_1s_bounds(TemplateMode mode)
    {
        var src = L(1, 80, 60);
        var next = Placement.Scatter(Snap(src, mode), 1, Ids(50), new Random(1234));
        Assert.Equal(27, next.Document.Layers.Count);
        Assert.Equal(1, next.SelectedLayerId);
        Assert.Same(src, next.Document.Layers[0]);

        // regions in table order, 3 copies when w > 100 else 1
        var expected = Template.Regions(mode).SelectMany(r => Enumerable.Repeat(r, r.W > 100 ? 3 : 1)).ToList();
        Assert.Equal(26, expected.Count);
        for (int i = 0; i < 26; i++)
        {
            var r = expected[i];
            var c = next.Document.Layers[i + 1];
            Assert.Equal(50 + i, c.Id);
            Assert.Equal("Art scat", c.Name);
            Assert.True(c.Scale >= 0.02);
            double lo = 0.15 * Math.Min(r.W, r.H) / 80, hi = 0.45 * Math.Min(r.W, r.H) / 80;
            Assert.InRange(c.Scale, Math.Max(0.02, lo), Math.Max(0.02, hi));
            Assert.InRange(c.Rotation, -30, 30);
            Assert.True(c.Rotation < 30);
            Assert.True(c.X >= r.X && c.X < r.X + 0.7 * r.W);
            Assert.True(c.Y >= r.Y && c.Y < r.Y + 0.7 * r.H);
            Assert.Same(src.Image, c.Image);
        }
    }

    [Fact] // 4.1 draws scale, x, y, rotation per copy (editor.js:1369-1373); a counting rng pins each draw's slot
    public void Scatter_draws_scale_then_x_then_y_then_rotation_per_copy()
    {
        var rng = new CountingRandom();
        var next = Placement.Scatter(Snap(L(1, 80, 60)), 1, Ids(2), rng);
        Assert.Equal(104, rng.Draws); // 26 copies * 4

        // the first region is torso_up (128x64): 3 copies; copy k uses draws 4k+1 .. 4k+4
        var r = Template.Regions(TemplateMode.Shirt)[0];
        for (int k = 0; k < 3; k++)
        {
            double d1 = (4 * k + 1) / 1000.0, d2 = (4 * k + 2) / 1000.0, d3 = (4 * k + 3) / 1000.0, d4 = (4 * k + 4) / 1000.0;
            var c = next.Document.Layers[k + 1];
            Near(Math.Max(0.02, (d1 * 0.3 + 0.15) * Math.Min(r.W, r.H) / 80), c.Scale);
            Near(r.X + d2 * (r.W * 0.7), c.X);
            Near(r.Y + d3 * (r.H * 0.7), c.Y);
            Near(d4 * 60 - 30, c.Rotation);
        }
        // the 4th layer is the next region (torso_right, 64 wide), whose single copy uses draws 13..16
        var r2 = Template.Regions(TemplateMode.Shirt)[1];
        var c2 = next.Document.Layers[4];
        Near(Math.Max(0.02, (0.013 * 0.3 + 0.15) * Math.Min(r2.W, r2.H) / 80), c2.Scale);
        Near(r2.X + 0.014 * (r2.W * 0.7), c2.X);
        Near(r2.Y + 0.015 * (r2.H * 0.7), c2.Y);
        Near(0.016 * 60 - 30, c2.Rotation);
    }

    [Fact]
    public void Scatter_with_the_same_seed_gives_the_same_layers()
    {
        var a = Placement.Scatter(Snap(L(1, 80, 60)), 1, Ids(2), new Random(7));
        var b = Placement.Scatter(Snap(L(1, 80, 60)), 1, Ids(2), new Random(7));
        Assert.Equal(a.Document.Layers.Select(l => (l.X, l.Y, l.Scale, l.Rotation)), b.Document.Layers.Select(l => (l.X, l.Y, l.Scale, l.Rotation)));
    }

    [Fact] // a large image drives the raw scale under 0.02; 4.1's max(0.02, sc) holds it there
    public void Scatter_floors_the_scale_at_0_02()
    {
        var next = Placement.Scatter(Snap(L(1, 2000, 2000)), 1, Ids(2), new CountingRandom());
        Assert.All(next.Document.Layers.Skip(1), l => Assert.Equal(0.02, l.Scale));
    }

    // ---- preset-sleeve-pop

    [Theory]
    [InlineData(TemplateMode.Shirt, new[] { "rarm_front", "rarm_back", "rarm_left", "rarm_right", "larm_front", "larm_back", "larm_left", "larm_right" })]
    [InlineData(TemplateMode.Pants, new[] { "rleg_front", "rleg_back", "rleg_left", "rleg_right", "lleg_front", "lleg_back", "lleg_left", "lleg_right" })]
    public void Sleeve_pop_adds_8_copies_in_the_stated_order(TemplateMode mode, string[] order)
    {
        var src = L(1, 40, 90) with { Rotation = 15, FlipH = true, Opacity = 0.9 };
        var next = Placement.SleevePop(Snap(src, mode), 1, Ids(10));
        Assert.Equal(9, next.Document.Layers.Count);
        Assert.Equal(1, next.SelectedLayerId);
        Assert.Same(src, next.Document.Layers[0]);
        for (int i = 0; i < 8; i++)
        {
            Assert.True(Template.TryFind(mode, order[i], out var r));
            var c = next.Document.Layers[i + 1];
            double sc = Math.Min((double)r.W / 40, (double)r.H / 90) * 0.8;
            Assert.Equal(10 + i, c.Id);
            Assert.Equal("Art sleeve", c.Name);
            Near(sc, c.Scale);
            Near(r.X + (r.W - 40 * sc) / 2, c.X);
            Near(r.Y + (r.H - 90 * sc) / 2, c.Y);
            Assert.Equal(0, c.Rotation);
            Assert.True(c.FlipH);
            Assert.Equal(0.9, c.Opacity);
        }
    }

    // ---- preset-fullbleed

    [Theory]
    [InlineData(400, 400, 1.4625, 0.0, -13.0)] // ledger: scale 1.4625, x 0
    [InlineData(100, 100, 5.85, 0.0, -13.0)] // ledger: scale 5.85, x 0, y -13; above the 5.0 keyboard cap, unclamped
    [InlineData(300, 100, 5.59, -546.0, 0.0)]
    public void Fullbleed_covers_the_template_centered(int w, int h, double scale, double x, double y)
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
        {
            var s = Snap(L(1, w, h) with { Rotation = 80 }, mode);
            var l = Of(Placement.Fullbleed(s.Document, 1), 1);
            Near(scale, l.Scale);
            Near(x, l.X);
            Near(y, l.Y);
            Assert.Equal(0, l.Rotation);
        }
    }

    // ---- props-center-front

    [Theory]
    [InlineData(0.5, TemplateMode.Shirt)]
    [InlineData(0.5, TemplateMode.Pants)]
    [InlineData(1.0, TemplateMode.Shirt)]
    [InlineData(2.0, TemplateMode.Shirt)]
    public void Center_on_front_centers_the_unrotated_box_and_keeps_scale_and_rotation(double scale, TemplateMode mode)
    {
        var s = Snap(L(1, 200, 100) with { Scale = scale, Rotation = 40, X = -50, Y = 400 }, mode);
        var l = Of(Placement.CenterOnFront(s.Document, 1), 1);
        Near(231 + (128 - 200 * scale) / 2, l.X);
        Near(74 + (128 - 100 * scale) / 2, l.Y);
        Assert.Equal((scale, 40.0), (l.Scale, l.Rotation));
    }

    [Fact] // node: 200x100 at 0.5 -> (245, 113)
    public void Center_on_front_matches_4_1s_doubles()
    {
        var l = Of(Placement.CenterOnFront(Snap(L(1, 200, 100) with { Scale = 0.5 }).Document, 1), 1);
        Assert.Equal((245.0, 113.0), (l.X, l.Y));
    }

    [Fact]
    public void Center_on_front_when_already_centered_changes_nothing()
    {
        var once = Placement.CenterOnFront(Snap(L(1, 200, 100) with { Scale = 0.5 }).Document, 1);
        Assert.Same(once, Placement.CenterOnFront(once, 1));
    }

    // ---- every op: shared contracts

    [Fact] // the op returns its input instance when there is no such layer
    public void A_missing_id_returns_the_same_instance_from_every_op()
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
        {
            var s = Snap(L(1, 50, 50), mode);
            Assert.Same(s.Document, Placement.FitFront(s.Document, 99));
            Assert.Same(s.Document, Placement.Fullbleed(s.Document, 99));
            Assert.Same(s.Document, Placement.CenterOnFront(s.Document, 99));
            Assert.Same(s, Placement.FrontAndBack(s, 99, 5));
            Assert.Same(s, Placement.Stretch(s, 99, 5));
            Assert.Same(s, Placement.Allover(s, 99, Ids(5)));
            Assert.Same(s, Placement.Scatter(s, 99, Ids(5), new Random(1)));
            Assert.Same(s, Placement.SleevePop(s, 99, Ids(5)));
        }
    }

    [Fact] // an empty document has no such layer either
    public void An_empty_document_is_returned_as_is()
    {
        var empty = new Document();
        Assert.Same(empty, Placement.FitFront(empty, 1));
        var s = new EditorSnapshot(empty, null);
        Assert.Same(s, Placement.Allover(s, 1, Ids(2)));
    }

    [Fact] // no exception in Pants, same counts as Shirt
    public void Every_op_runs_in_pants_with_shirts_counts()
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
        {
            var s = Snap(L(1, 60, 80), mode);
            Assert.Single(Placement.FitFront(s.Document, 1).Layers);
            Assert.Equal(2, Placement.FrontAndBack(s, 1, 2).Document.Layers.Count);
            Assert.Equal(2, Placement.Stretch(s, 1, 2).Document.Layers.Count);
            Assert.Equal(19, Placement.Allover(s, 1, Ids(2)).Document.Layers.Count);
            Assert.Equal(27, Placement.Scatter(s, 1, Ids(2), new Random(3)).Document.Layers.Count);
            Assert.Equal(9, Placement.SleevePop(s, 1, Ids(2)).Document.Layers.Count);
            Assert.Single(Placement.Fullbleed(s.Document, 1).Layers);
            Assert.Single(Placement.CenterOnFront(s.Document, 1).Layers);
        }
    }

    [Fact] // the input is immutable: nothing mutates it
    public void Ops_leave_their_input_untouched()
    {
        var s = Snap(L(1, 60, 80));
        var before = s.Document;
        Placement.FrontAndBack(s, 1, 2);
        Placement.Allover(s, 1, Ids(2));
        Placement.Fullbleed(s.Document, 1);
        Assert.Same(before, s.Document);
        Assert.Single(s.Document.Layers);
        Assert.Equal(1.0, s.Document.Layers[0].Scale);
    }

    [Fact] // a clone id that is already in the document is a programming error, as LayerOps.Duplicate
    public void A_clone_id_already_in_use_throws()
    {
        var s = Snap(L(2, 20, 20), TemplateMode.Shirt, L(1, 5, 5));
        Assert.Throws<ArgumentException>(() => Placement.FrontAndBack(s, 2, 1));
        Assert.Throws<ArgumentException>(() => Placement.Stretch(s, 2, 1));
        Assert.Throws<ArgumentException>(() => Placement.Allover(s, 2, Ids(1)));
        Assert.Throws<ArgumentException>(() => Placement.SleevePop(s, 2, Ids(1)));
        Assert.Throws<ArgumentException>(() => Placement.Scatter(s, 2, Ids(1), new Random(1)));
    }

    [Fact]
    public void Scatter_requires_an_rng()
    {
        var s = Snap(L(1, 20, 20));
        Assert.Throws<ArgumentNullException>(() => Placement.Scatter(s, 1, Ids(2), null!));
    }
}
