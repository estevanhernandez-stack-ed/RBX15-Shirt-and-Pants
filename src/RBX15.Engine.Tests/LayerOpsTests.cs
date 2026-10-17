using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// LayerOps is 4.1's LAYERS section (editor.js:499-582) and Up/Down (editor.js:846-853). Placement
/// literals are 4.1's own doubles: node evaluated addLayer's arithmetic (editor.js:502-512) for each
/// size and printed String(x), String(y), String(scale).
/// </summary>
public class LayerOpsTests
{
    static SKImage Img(int w, int h) => Px.Solid(w, h, SKColors.Red);

    static Layer L(int id, string name = "L", int w = 10, int h = 10) => new() { Id = id, Image = Img(w, h), Name = name };

    static Document Doc(params Layer[] layers) => new() { Layers = [.. layers] };

    static int[] Ids(Document d) => d.Layers.Select(l => l.Id).ToArray();

    [Theory] // layers-default-name-offbyone: v5 names match the id (4.1 used id + 1, editor.js:513-514)
    [InlineData(1, "Image 1")]
    [InlineData(2, "Image 2")]
    [InlineData(42, "Image 42")]
    public void Default_name_matches_the_id(int id, string expected) => Assert.Equal(expected, LayerOps.DefaultName(id));

    [Theory] // layers-add-placement: 85% fit centered in the front slot, 4.1's doubles exactly
    [InlineData(512, 512, TemplateMode.Shirt, 240.6, 83.6, 0.2125)]
    [InlineData(100, 400, TemplateMode.Shirt, 281.4, 83.6, 0.272)]
    [InlineData(400, 100, TemplateMode.Shirt, 240.6, 124.4, 0.272)]
    [InlineData(33, 77, TemplateMode.Shirt, 271.6857142857143, 83.6, 1.412987012987013)]
    [InlineData(512, 512, TemplateMode.Pants, 240.6, 83.6, 0.2125)] // bug-pants-mode-region-keys: waist_front, same geometry
    public void Place_fits_85_percent_centered_in_the_front_slot(int w, int h, TemplateMode mode, double x, double y, double scale)
    {
        var l = LayerOps.Place(Img(w, h), 7, "Art", mode);
        Assert.Equal((x, y, scale), (l.X, l.Y, l.Scale));
        Assert.Equal((7, "Art"), (l.Id, l.Name));
        Assert.Equal((true, 1.0, 0.0, false, false), (l.Visible, l.Opacity, l.Rotation, l.FlipH, l.FlipV));
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate));
        Assert.Equal((0, 0, 0), (l.RShift, l.GShift, l.BShift));
    }

    [Theory] // drop-canvas: the image is centered on the drop point, at the same fit scale
    [InlineData(512, 512, 300, 200, 245.6, 145.6, 0.2125)]
    [InlineData(100, 400, 0, 0, -13.600000000000001, -54.400000000000006, 0.272)]
    [InlineData(33, 77, 584.5, 558.5, 561.1857142857143, 504.1, 1.412987012987013)]
    public void Place_centers_on_the_drop_point(int w, int h, double dx, double dy, double x, double y, double scale)
    {
        var l = LayerOps.Place(Img(w, h), 1, "Art", TemplateMode.Shirt, (dx, dy));
        Assert.Equal((x, y, scale), (l.X, l.Y, l.Scale));
        Assert.Equal(dx, l.X + w * l.Scale / 2, 9);
        Assert.Equal(dy, l.Y + h * l.Scale / 2, 9);
    }

    [Theory] // 4.1: name || 'Image ' + nextId (editor.js:514)
    [InlineData(null)]
    [InlineData("")]
    public void Place_without_a_name_uses_the_default(string? name) => Assert.Equal("Image 3", LayerOps.Place(Img(4, 4), 3, name, TemplateMode.Shirt).Name);

    [Fact] // addLayer pushes on top and selects (editor.js:521-522)
    public void Add_image_pushes_on_top_and_selects_it()
    {
        var s = new EditorSnapshot(Doc(L(1), L(2)), 1);
        var next = LayerOps.AddImage(s, Img(64, 32), 5, "New");
        Assert.Equal(new[] { 1, 2, 5 }, Ids(next.Document));
        Assert.Equal(5, next.SelectedLayerId);
        Assert.Equal("New", next.Document.Layers[^1].Name);
        Assert.Equal(new[] { 1, 2 }, Ids(s.Document));
    }

    [Fact] // ids are unique; EditorSession's NextId only grows (editor.js:1465)
    public void Add_image_refuses_an_id_already_in_the_document() =>
        Assert.Throws<ArgumentException>(() => LayerOps.AddImage(new EditorSnapshot(Doc(L(1)), null), Img(4, 4), 1, "Dup"));

    [Fact] // layers-remove: removing the selected layer selects the new topmost (editor.js:565)
    public void Removing_the_selected_layer_selects_the_new_top_layer()
    {
        var next = LayerOps.Remove(new EditorSnapshot(Doc(L(1), L(2), L(3)), 2), 2);
        Assert.Equal(new[] { 1, 3 }, Ids(next.Document));
        Assert.Equal(3, next.SelectedLayerId);
    }

    [Fact] // layers-remove: removing another layer keeps the selection
    public void Removing_another_layer_keeps_the_selection()
    {
        var next = LayerOps.Remove(new EditorSnapshot(Doc(L(1), L(2), L(3)), 1), 3);
        Assert.Equal(new[] { 1, 2 }, Ids(next.Document));
        Assert.Equal(1, next.SelectedLayerId);
    }

    [Fact]
    public void Removing_the_last_layer_selects_none()
    {
        var next = LayerOps.Remove(new EditorSnapshot(Doc(L(1)), 1), 1);
        Assert.Empty(next.Document.Layers);
        Assert.Null(next.SelectedLayerId);
    }

    [Fact] // undo-empty-steps: 4.1 pushed a snapshot even for a missing id (editor.js:563)
    public void Removing_a_missing_id_returns_the_same_snapshot()
    {
        var s = new EditorSnapshot(Doc(L(1)), 1);
        Assert.Same(s, LayerOps.Remove(s, 9));
    }

    [Fact] // layers-duplicate: index + 1, " copy", +10/+10, same image, selected (editor.js:542-560)
    public void Duplicate_inserts_above_the_original_offset_and_selected()
    {
        var src = L(2, "Logo") with { X = 5, Y = -3, Scale = 0.5, Rotation = 30, Opacity = 0.4, FlipH = true, Brightness = 0, RShift = -20 };
        var s = new EditorSnapshot(Doc(L(1), src, L(3)), 1);
        var next = LayerOps.Duplicate(s, 2, 9);
        Assert.Equal(new[] { 1, 2, 9, 3 }, Ids(next.Document));
        Assert.Equal(9, next.SelectedLayerId);
        var clone = next.Document.Layers[2];
        Assert.Equal(src with { Id = 9, Name = "Logo copy", X = 15, Y = 7 }, clone);
        Assert.Same(src.Image, clone.Image);
    }

    [Fact] // the suffix stacks: 'A copy copy'
    public void Duplicating_a_copy_stacks_the_suffix()
    {
        var once = LayerOps.Duplicate(new EditorSnapshot(Doc(L(1, "A")), 1), 1, 2);
        var twice = LayerOps.Duplicate(once, 2, 3);
        Assert.Equal(new[] { "A", "A copy", "A copy copy" }, twice.Document.Layers.Select(l => l.Name));
    }

    [Fact] // editor.js:544: if (!src) return
    public void Duplicating_a_missing_id_returns_the_same_snapshot()
    {
        var s = new EditorSnapshot(Doc(L(1)), null);
        Assert.Same(s, LayerOps.Duplicate(s, 9, 10));
    }

    [Fact]
    public void Duplicate_refuses_a_new_id_already_in_the_document() =>
        Assert.Throws<ArgumentException>(() => LayerOps.Duplicate(new EditorSnapshot(Doc(L(1), L(2)), null), 1, 2));

    [Fact] // layers-visibility: flips Visible; a missing id is the same document
    public void Toggle_visibility_flips_one_layer()
    {
        var d = Doc(L(1), L(2));
        var hidden = LayerOps.ToggleVisibility(d, 2);
        Assert.Equal((true, false), (hidden.Layers[0].Visible, hidden.Layers[1].Visible));
        Assert.True(LayerOps.ToggleVisibility(hidden, 2).Layers[1].Visible);
        Assert.Same(d, LayerOps.ToggleVisibility(d, 9));
    }

    [Fact] // layers-move-updown: Up swaps with index + 1, no-op at the top (editor.js:846-849)
    public void Move_up_swaps_with_the_layer_above_and_stops_at_the_top()
    {
        var d = Doc(L(1), L(2), L(3));
        Assert.Equal(new[] { 2, 1, 3 }, Ids(LayerOps.MoveUp(d, 1)));
        Assert.Same(d, LayerOps.MoveUp(d, 3));
    }

    [Fact] // layers-move-updown: Down swaps with index - 1, no-op at the bottom (editor.js:850-853)
    public void Move_down_swaps_with_the_layer_below_and_stops_at_the_bottom()
    {
        var d = Doc(L(1), L(2), L(3));
        Assert.Equal(new[] { 1, 3, 2 }, Ids(LayerOps.MoveDown(d, 3)));
        Assert.Same(d, LayerOps.MoveDown(d, 1));
    }

    [Fact]
    public void Moving_a_missing_id_returns_the_same_document()
    {
        var d = Doc(L(1), L(2));
        Assert.Same(d, LayerOps.MoveUp(d, 9));
        Assert.Same(d, LayerOps.MoveDown(d, 9));
    }

    [Fact] // select-hit-test: topmost visible layer under the point (editor.js:1100-1111)
    public void Hit_test_picks_the_topmost_layer_under_the_point()
    {
        var bottom = L(1, w: 100, h: 100);
        var top = L(2, w: 20, h: 20) with { X = 40, Y = 40 };
        var d = Doc(bottom, top);
        Assert.Equal(2, LayerOps.HitTest(d, 50, 50));
        Assert.Equal(1, LayerOps.HitTest(d, 5, 5));
        Assert.Equal(1, LayerOps.HitTest(d, 100, 100)); // the box edge is inclusive (lx <= hw)
        Assert.Null(LayerOps.HitTest(d, 100.5, 50));
    }

    [Fact] // select-hit-test: hidden layers are skipped (editor.js:1103)
    public void Hit_test_skips_hidden_layers()
    {
        var d = Doc(L(1, w: 100, h: 100), L(2, w: 100, h: 100) with { Visible = false });
        Assert.Equal(1, LayerOps.HitTest(d, 50, 50));
        Assert.Null(LayerOps.HitTest(Doc(L(1) with { Visible = false }), 5, 5));
    }

    [Fact] // pointInRotatedRect: the rotated box, not the axis-aligned one (editor.js:1043-1053)
    public void Hit_test_uses_the_rotated_box()
    {
        var d = Doc(L(1, w: 100, h: 20) with { Rotation = 90 }); // center (50, 10); rotated it spans x 40..60, y -40..60
        Assert.Equal(1, LayerOps.HitTest(d, 50, 55));
        Assert.Null(LayerOps.HitTest(d, 5, 10));
    }

    [Theory] // asset-rightclick-full (keep): max(585/w, 559/h), 4.1's doubles as node printed them (editor.js:2525); unclamped (R15)
    [InlineData(390, 200, 2.795)]
    [InlineData(128, 150, 4.5703125)]
    [InlineData(64, 130, 9.140625)]
    [InlineData(684, 133, 4.203007518796992)]
    [InlineData(1, 1, 585)]
    public void Add_full_covers_the_template_from_the_top_left(int w, int h, double scale)
    {
        var start = new EditorSnapshot(Doc(L(1)), 1);
        var s = LayerOps.AddFull(start, Img(w, h), 2, "drip");
        var l = s.Document.Layers[^1];
        Assert.Equal((2, "drip (full)", 0.0, 0.0, scale), (l.Id, l.Name, l.X, l.Y, l.Scale));
        Assert.Equal((true, 1.0, 0.0, false, false), (l.Visible, l.Opacity, l.Rotation, l.FlipH, l.FlipV));
        Assert.Equal((100.0, 100.0, 100.0, 0.0, 0, 0, 0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate, l.RShift, l.GShift, l.BShift));
        Assert.Equal(2, s.SelectedLayerId);                  // pushed on top and selected (editor.js:2530-2531)
        Assert.Equal([1, 2], Ids(s.Document));
        Assert.Throws<ArgumentException>(() => LayerOps.AddFull(s, Img(w, h), 2, "again"));   // ids are never reused
    }

    [Fact] // displayName + ' (full)' with an empty name is " (full)", as 4.1's concatenation
    public void Add_full_keeps_an_empty_base_name() =>
        Assert.Equal(" (full)", LayerOps.AddFull(new EditorSnapshot(Doc(), null), Img(4, 4), 1, "").Document.Layers[0].Name);
}
