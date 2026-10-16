using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M3b layer edits: the canvas drags (editor.js:1169-1190), the properties panel (editor.js:825-911), the layer list
/// drop (editor.js:621-657) and the drawing commit (editor.js:528-540). Every op returns its input instance when it
/// changes nothing, so the session records no step (undo-empty-steps). LayerOpsTests.cs (M3a) is untouched.
/// </summary>
public class LayerOpsEditTests
{
    static Layer L(int id, double x = 200, double y = 200, double scale = 1, int w = 100, int h = 50) =>
        new() { Id = id, Name = "L" + id, Image = Px.Solid(w, h, SKColors.Red), X = x, Y = y, Scale = scale };

    static Document Doc(params Layer[] layers) => new() { Layers = ImmutableList.Create(layers) };

    static Layer Only(Document d) => Assert.Single(d.Layers);

    [Fact] // select-move-hit-and-drag, props-name-xy: the layer lands at (x, y); nothing else moves
    public void MoveTo_places_the_layer()
    {
        var doc = Doc(L(1));
        var moved = Only(LayerOps.MoveTo(doc, 1, 215.5, -40));
        Assert.Equal((215.5, -40.0, 1.0), (moved.X, moved.Y, moved.Scale));
        Assert.Same(doc, LayerOps.MoveTo(doc, 1, 200, 200));
        Assert.Same(doc, LayerOps.MoveTo(doc, 1, double.NaN, 0));
        Assert.Same(doc, LayerOps.MoveTo(doc, 1, 0, double.PositiveInfinity));
    }

    [Fact] // props-scale (editor.js:828-832): the slider keeps the top-left; props-scale-clamp-inconsistency: 1 to 500%
    public void SetScale_keeps_the_top_left_and_clamps()
    {
        var doc = Doc(L(1));
        var s = Only(LayerOps.SetScale(doc, 1, 2.5));
        Assert.Equal((200.0, 200.0, 2.5), (s.X, s.Y, s.Scale));
        Assert.Equal(5, Only(LayerOps.SetScale(doc, 1, 7)).Scale);
        Assert.Equal(0.01, Only(LayerOps.SetScale(doc, 1, 0)).Scale);
        Assert.Equal(0.01, Only(LayerOps.SetScale(doc, 1, double.NaN)).Scale);
        Assert.Same(doc, LayerOps.SetScale(doc, 1, 1));
    }

    [Theory] // select-resize-handles parityCheck: center invariant to 1e-9; wheel-scale-layer fix: centered
    [InlineData(0.5)]
    [InlineData(2)]
    [InlineData(3.3)]
    [InlineData(9)]
    public void ScaleAboutCenter_keeps_the_center(double scale)
    {
        var doc = Doc(L(1, x: 13.7, y: -4.25, scale: 0.8));
        var before = SelectionGeometry.Center(doc.Layers[0]);
        var after = Only(LayerOps.ScaleAboutCenter(doc, 1, scale));
        Assert.Equal(SelectionGeometry.ClampScale(scale), after.Scale);
        var c = SelectionGeometry.Center(after);
        Assert.Equal(before.X, c.X, 9);
        Assert.Equal(before.Y, c.Y, 9);
    }

    [Fact] // a clamped value equal to the current scale changes nothing, so no rounding-only step is recorded
    public void ScaleAboutCenter_at_the_limit_returns_the_same_document()
    {
        var doc = Doc(L(1, scale: 5));
        Assert.Same(doc, LayerOps.ScaleAboutCenter(doc, 1, 7));
        Assert.Same(doc, LayerOps.ScaleAboutCenter(doc, 1, 5));
        var tiny = Doc(L(1, scale: 0.01));
        Assert.Same(tiny, LayerOps.ScaleAboutCenter(tiny, 1, SelectionGeometry.WheelScale(0.01, -1)));
    }

    [Fact] // props-rotation, select-rotate-drag: normalized to -180..180
    public void SetRotation_normalizes()
    {
        var doc = Doc(L(1));
        Assert.Equal(-160, Only(LayerOps.SetRotation(doc, 1, 200)).Rotation, 9);
        Assert.Equal(180, Only(LayerOps.SetRotation(doc, 1, 180)).Rotation);
        Assert.Same(doc, LayerOps.SetRotation(doc, 1, 0));
        Assert.Same(doc, LayerOps.SetRotation(doc, 1, 360));
    }

    [Fact] // props-flip (editor.js:843-844): each button toggles its own axis
    public void Flip_toggles_one_axis()
    {
        var doc = Doc(L(1));
        var h = LayerOps.Flip(doc, 1, horizontal: true);
        Assert.Equal((true, false), (Only(h).FlipH, Only(h).FlipV));
        var hv = LayerOps.Flip(h, 1, horizontal: false);
        Assert.Equal((true, true), (Only(hv).FlipH, Only(hv).FlipV));
        Assert.True(doc.ContentEquals(LayerOps.Flip(LayerOps.Flip(doc, 1, true), 1, true)));
    }

    [Fact] // props-opacity (editor.js:838-842): 0..1
    public void SetOpacity_clamps()
    {
        var doc = Doc(L(1));
        Assert.Equal(0.4, Only(LayerOps.SetOpacity(doc, 1, 0.4)).Opacity);
        Assert.Equal(1, Only(LayerOps.SetOpacity(LayerOps.SetOpacity(doc, 1, 0.4), 1, 1.5)).Opacity);
        Assert.Equal(0, Only(LayerOps.SetOpacity(doc, 1, -0.2)).Opacity);
        Assert.Same(doc, LayerOps.SetOpacity(doc, 1, 1));
        Assert.Same(doc, LayerOps.SetOpacity(doc, 1, double.NaN));
    }

    [Fact] // props-name-xy (editor.js:825): any text, empty allowed
    public void SetName_takes_any_text()
    {
        var doc = Doc(L(1));
        Assert.Equal("drip <b>", Only(LayerOps.SetName(doc, 1, "drip <b>")).Name);
        Assert.Equal("", Only(LayerOps.SetName(doc, 1, "")).Name);
        Assert.Equal("", Only(LayerOps.SetName(doc, 1, null)).Name);
        Assert.Same(doc, LayerOps.SetName(doc, 1, "L1"));
    }

    [Fact] // props-adjust-sliders: B/C/S 0..200, hue 0..360, RGB -100..100; a non-finite field keeps its value
    public void SetAdjustments_clamps_to_the_slider_ranges()
    {
        var doc = Doc(L(1));
        var a = Only(LayerOps.SetAdjustments(doc, 1, new LayerAdjustments(250, -5, 0, 400, 150, -150, 7)));
        Assert.Equal(new LayerAdjustments(200, 0, 0, 360, 100, -100, 7), LayerAdjustments.From(a));
        var keep = Only(LayerOps.SetAdjustments(doc, 1, new LayerAdjustments(double.NaN, 50, double.PositiveInfinity, 90, 0, 0, 0)));
        Assert.Equal(new LayerAdjustments(100, 50, 100, 90, 0, 0, 0), LayerAdjustments.From(keep));
        Assert.Same(doc, LayerOps.SetAdjustments(doc, 1, LayerAdjustments.Default));
        Assert.Throws<ArgumentNullException>(() => LayerOps.SetAdjustments(doc, 1, null!));
    }

    [Fact] // final fix wave 8: only a field that changes is clamped; an out-of-range field passed back unchanged stays as stored
    public void SetAdjustments_clamps_only_the_fields_that_change()
    {
        var doc = Doc(L(1) with { Brightness = 300, Contrast = -20, HueRotate = 400, GShift = 150 });
        var stored = LayerAdjustments.From(doc.Layers[0]);
        var a = Only(LayerOps.SetAdjustments(doc, 1, stored with { HueRotate = 90 }));
        Assert.Equal(stored with { HueRotate = 90 }, LayerAdjustments.From(a));
        var b = Only(LayerOps.SetAdjustments(doc, 1, stored with { Brightness = 250 }));
        Assert.Equal(stored with { Brightness = 200 }, LayerAdjustments.From(b));
        Assert.Same(doc, LayerOps.SetAdjustments(doc, 1, stored));
        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(Only(LayerOps.ResetAdjustments(doc, 1))));
    }

    [Fact] // props-adjust-reset (editor.js:908-911); layers-model defaults (editor.js:134-142)
    public void ResetAdjustments_restores_the_defaults()
    {
        Assert.Equal(new LayerAdjustments(100, 100, 100, 0, 0, 0, 0), LayerAdjustments.Default);
        var doc = Doc(L(1) with { Brightness = 0, Contrast = 0, Saturate = 0, HueRotate = 90, RShift = 5, GShift = -5, BShift = 1 });
        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(Only(LayerOps.ResetAdjustments(doc, 1))));
        var clean = Doc(L(1));
        Assert.Same(clean, LayerOps.ResetAdjustments(clean, 1));
    }

    [Fact] // Reorder: Layers order, 0 = bottom; no-op and out-of-range indices change nothing
    public void Reorder_moves_one_layer()
    {
        var doc = Doc(L(1), L(2), L(3));
        Assert.Equal(new[] { 2, 3, 1 }, LayerOps.Reorder(doc, 0, 2).Layers.Select(l => l.Id));
        Assert.Equal(new[] { 3, 1, 2 }, LayerOps.Reorder(doc, 2, 0).Layers.Select(l => l.Id));
        Assert.Same(doc, LayerOps.Reorder(doc, 1, 1));
        Assert.Same(doc, LayerOps.Reorder(doc, -1, 1));
        Assert.Same(doc, LayerOps.Reorder(doc, 0, 3));
    }

    /// <summary>handleLayerDrop (editor.js:621-657) as 4.1 runs it: splice the dragged layer out, find the target again, splice it back.</summary>
    static int[] Splice41(int[] ids, int dragId, int targetId, bool above)
    {
        var layers = ids.ToList();
        int dragIdx = layers.IndexOf(dragId);
        layers.RemoveAt(dragIdx);
        int insertIdx = layers.IndexOf(targetId);
        layers.Insert(above ? insertIdx + 1 : insertIdx, dragId);
        return layers.ToArray();
    }

    [Fact] // layers-reorder-drag: every drag, target and half of a 4-layer list lands where 4.1's splice puts it
    public void DropIndices_plus_Reorder_is_4_1_s_splice()
    {
        int[] ids = { 1, 2, 3, 4 };
        var doc = Doc(ids.Select(i => L(i)).ToArray());
        foreach (int drag in ids)
            foreach (int target in ids.Where(t => t != drag))
                foreach (bool above in new[] { true, false })
                {
                    var (from, to) = LayerOps.DropIndices(doc, drag, target, above)!.Value;
                    var got = LayerOps.Reorder(doc, from, to).Layers.Select(l => l.Id).ToArray();
                    Assert.Equal(Splice41(ids, drag, target, above), got);
                    if (got.SequenceEqual(ids)) Assert.Equal(from, to); // an order-keeping drop records nothing (undo-empty-steps)
                }
    }

    [Fact] // editor.js:625: a drop on itself, or a missing id, does nothing
    public void DropIndices_refuses_a_self_drop_or_a_missing_id()
    {
        var doc = Doc(L(1), L(2));
        Assert.Null(LayerOps.DropIndices(doc, 1, 1, true));
        Assert.Null(LayerOps.DropIndices(doc, 9, 1, true));
        Assert.Null(LayerOps.DropIndices(doc, 1, 9, false));
    }

    [Fact] // draw-commit-layer, layers-add-drawing (editor.js:534-537): pushed on top and selected
    public void AddDrawing_pushes_on_top_and_selects()
    {
        var s = new EditorSnapshot(Doc(L(1), L(2)), 1);
        var drawing = L(7) with { Name = "Rect drawing" };
        var next = LayerOps.AddDrawing(s, drawing);
        Assert.Equal(new[] { 1, 2, 7 }, next.Document.Layers.Select(l => l.Id));
        Assert.Same(drawing, next.Document.Layers[^1]);
        Assert.Equal(7, next.SelectedLayerId);
        Assert.Throws<ArgumentException>(() => LayerOps.AddDrawing(s, L(2)));
        Assert.Throws<ArgumentNullException>(() => LayerOps.AddDrawing(s, null!));
    }

    [Fact] // a missing id changes nothing, for every edit
    public void Every_edit_of_a_missing_layer_returns_the_same_document()
    {
        var doc = Doc(L(1));
        Assert.Same(doc, LayerOps.MoveTo(doc, 9, 1, 1));
        Assert.Same(doc, LayerOps.SetScale(doc, 9, 2));
        Assert.Same(doc, LayerOps.ScaleAboutCenter(doc, 9, 2));
        Assert.Same(doc, LayerOps.SetRotation(doc, 9, 10));
        Assert.Same(doc, LayerOps.Flip(doc, 9, true));
        Assert.Same(doc, LayerOps.SetOpacity(doc, 9, 0.5));
        Assert.Same(doc, LayerOps.SetName(doc, 9, "x"));
        Assert.Same(doc, LayerOps.SetAdjustments(doc, 9, new LayerAdjustments(1, 1, 1, 1, 1, 1, 1)));
        Assert.Same(doc, LayerOps.ResetAdjustments(doc, 9));
    }

    [Fact] // carry-forward: Document.ContentEquals compares Layer by record equality, so M3b adds no Layer state
    public void Layer_gains_no_field()
    {
        var names = typeof(Layer).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(new[]
        {
            "BShift", "Brightness", "Contrast", "FlipH", "FlipV", "GShift", "HueRotate", "Id", "Image", "Name", "Opacity",
            "RShift", "Rotation", "Saturate", "Scale", "Visible", "X", "Y",
        }, names);
    }
}
