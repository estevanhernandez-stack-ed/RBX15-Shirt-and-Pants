using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// DocumentOps ports the Home tab's region and mode edits (editor.js:1394, :1581-1598, :1655-1725).
/// Every op returns its input instance when it changes nothing (interfaces sheet §3.2), which the
/// tests pin with Assert.Same.
/// </summary>
public class DocumentOpsTests
{
    static readonly Fill Cyan = Fill.Solid("#17d4fa");
    static readonly Fill Linear45 = new("linear", "#17d4fa", "#f22f89", 45, 16);
    static readonly Fill Linear90 = new("linear", "#17d4fa", "#f22f89", 90, 16);

    static Document WithFills(params (string Region, Fill Fill)[] fills) =>
        new() { RegionFills = fills.ToImmutableList() };

    static Layer LayerOf(int id, int w, int h) => new() { Id = id, Image = Px.Solid(w, h, SKColors.Red), Name = "L" + id };

    // ---- mode (mode-switch-effects, mode-switch-undo-and-callers, tpl-active-regions-swap) ----

    [Fact] // editor.js:1696-1716: mode set, fills emptied, layers/background/mask/grid kept
    public void ToggleMode_switches_and_empties_fills_and_keeps_everything_else()
    {
        var layer = LayerOf(1, 10, 10);
        var doc = WithFills(("torso_front", Cyan), ("rarm_left", Linear45)) with
        {
            Layers = ImmutableList.Create(layer),
            BgColor = "#ff0000",
            BgOpacity = 0.5,
            ShowMask = false,
            ShowGrid = true,
        };

        var pants = DocumentOps.ToggleMode(doc);

        Assert.Equal(TemplateMode.Pants, pants.Mode);
        Assert.Empty(pants.RegionFills);
        Assert.Same(layer, Assert.Single(pants.Layers));
        Assert.Equal(("#ff0000", 0.5, false, true), (pants.BgColor, pants.BgOpacity, pants.ShowMask, pants.ShowGrid));

        var back = DocumentOps.ToggleMode(pants);
        Assert.Equal(TemplateMode.Shirt, back.Mode);
        Assert.Empty(back.RegionFills); // claim-mode-per-mode-region-data: fills do not come back
    }

    [Fact] // switchMode to the current mode wipes fills like 4.1; with none it is a no-op
    public void SwitchMode_to_the_same_mode_wipes_fills_and_is_a_no_op_without_them()
    {
        var painted = WithFills(("torso_front", Cyan));
        Assert.Empty(DocumentOps.SwitchMode(painted, TemplateMode.Shirt).RegionFills);

        var blank = new Document();
        Assert.Same(blank, DocumentOps.SwitchMode(blank, TemplateMode.Shirt));
    }

    // ---- paint and clear (regions-click-paint, regions-right-click-clear, regions-state-model) ----

    [Fact] // editor.js:1671: a new key is appended
    public void PaintRegion_appends_a_new_region()
    {
        var doc = DocumentOps.PaintRegion(DocumentOps.PaintRegion(new Document(), "torso_front", Cyan), "torso_back", Linear45);
        Assert.Equal(new[] { ("torso_front", Cyan), ("torso_back", Linear45) }, doc.RegionFills);
        Assert.Same(Linear45, DocumentOps.RegionFill(doc, "torso_back"));
        Assert.Null(DocumentOps.RegionFill(doc, "torso_up"));
    }

    [Fact] // a JS object keeps a key's position on reassignment
    public void PaintRegion_replaces_an_existing_region_in_place()
    {
        var doc = WithFills(("torso_front", Cyan), ("torso_back", Cyan));
        var repainted = DocumentOps.PaintRegion(doc, "torso_front", Linear45);
        Assert.Equal(new[] { ("torso_front", Linear45), ("torso_back", Cyan) }, repainted.RegionFills);
    }

    [Fact] // regions-click-paint fix: an identical repaint is no change, so no undo step
    public void PaintRegion_with_an_equal_fill_returns_the_same_document()
    {
        var doc = WithFills(("torso_front", new Fill("solid", "#17d4fa")));
        Assert.Same(doc, DocumentOps.PaintRegion(doc, "torso_front", new Fill("solid", "#17d4fa")));
    }

    [Theory] // only the mode's 18 keys can be painted (Pants keys in Shirt mode and the reverse)
    [InlineData(TemplateMode.Shirt, "waist_front")]
    [InlineData(TemplateMode.Pants, "torso_front")]
    [InlineData(TemplateMode.Shirt, "")]
    public void PaintRegion_refuses_a_key_the_mode_lacks(TemplateMode mode, string region)
    {
        var doc = new Document { Mode = mode };
        Assert.Throws<ArgumentException>(() => DocumentOps.PaintRegion(doc, region, Cyan));
    }

    [Fact] // Pants keys paint in Pants mode (mode-pants-crash-hardcoded-keys)
    public void PaintRegion_paints_pants_keys_in_pants_mode()
    {
        var doc = DocumentOps.PaintRegion(new Document { Mode = TemplateMode.Pants }, "lleg_back", Cyan);
        Assert.Equal(("lleg_back", Cyan), Assert.Single(doc.RegionFills));
    }

    [Fact] // editor.js:1681: delete the key; the rest keep their order
    public void ClearRegion_removes_only_that_region()
    {
        var doc = WithFills(("torso_up", Cyan), ("torso_front", Linear45), ("torso_down", Cyan));
        var cleared = DocumentOps.ClearRegion(doc, "torso_front");
        Assert.Equal(new[] { ("torso_up", Cyan), ("torso_down", Cyan) }, cleared.RegionFills);
    }

    [Fact] // editor.js:1679: no fill, no change (and so no undo step)
    public void ClearRegion_of_an_unpainted_region_returns_the_same_document()
    {
        var doc = WithFills(("torso_up", Cyan));
        Assert.Same(doc, DocumentOps.ClearRegion(doc, "torso_front"));
    }

    // ---- live edit (fillstyle-live-edit-rule, keep) ----

    [Fact] // the row's scenario: A,B linear 45, C solid; angle 90 moves A and B only
    public void RetargetFill_moves_every_region_with_the_before_key_and_no_other()
    {
        var doc = WithFills(("torso_front", Linear45), ("torso_back", Linear45), ("torso_up", Cyan));
        var next = DocumentOps.RetargetFill(doc, Linear45.Key, Linear90);
        Assert.Equal(new[] { ("torso_front", Linear90), ("torso_back", Linear90), ("torso_up", Cyan) }, next.RegionFills);
    }

    [Fact] // matching is by key, so an equal-valued descriptor painted separately follows too
    public void RetargetFill_matches_by_key_not_by_reference()
    {
        var doc = WithFills(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, 16)));
        var next = DocumentOps.RetargetFill(doc, Linear45.Key, Linear90);
        Assert.Equal(Linear90, DocumentOps.RegionFill(next, "torso_front"));
    }

    [Fact] // editor.js:1591: nothing matched, nothing changes (idle edits make no step)
    public void RetargetFill_with_no_match_returns_the_same_document()
    {
        var doc = WithFills(("torso_front", Cyan));
        Assert.Same(doc, DocumentOps.RetargetFill(doc, Linear45.Key, Linear90));
    }

    [Fact] // a key the mode lacks (from a 4.1 file) is retargeted too, like 4.1's for-in loop
    public void RetargetFill_includes_keys_the_mode_lacks()
    {
        var doc = WithFills(("waist_front", Linear45));
        Assert.Equal(Linear90, DocumentOps.RegionFill(DocumentOps.RetargetFill(doc, Linear45.Key, Linear90), "waist_front"));
    }

    // ---- fit (regions-click-fit-layer) ----

    [Fact] // the row's own numbers: 200x100 into torso_front 128x128
    public void FitLayerToRegion_matches_the_ledger_numbers()
    {
        var layer = LayerOf(7, 200, 100) with { Rotation = 30, FlipH = true, Opacity = 0.5 };
        var doc = new Document { Layers = ImmutableList.Create(LayerOf(1, 5, 5), layer) };

        var fitted = Assert.Single(DocumentOps.FitLayerToRegion(doc, 7, "torso_front").Layers, l => l.Id == 7);

        Assert.Equal(0.544, fitted.Scale, 12);
        Assert.Equal(240.6, fitted.X, 12);
        Assert.Equal(110.8, fitted.Y, 12);
        Assert.Equal(0, fitted.Rotation);
        Assert.True(fitted.FlipH);
        Assert.Equal(0.5, fitted.Opacity);
    }

    [Fact] // Pants mode fits into waist_* with the same geometry (mode-pants-crash-hardcoded-keys)
    public void FitLayerToRegion_works_in_pants_mode()
    {
        var doc = new Document { Mode = TemplateMode.Pants, Layers = ImmutableList.Create(LayerOf(1, 64, 128)) };
        var fitted = DocumentOps.FitLayerToRegion(doc, 1, "rleg_front").Layers[0];
        // rleg_front is 217,355 64x128: scale min(1,1)*0.85, centered
        Assert.Equal((0.85, 217 + (64 - 54.4) / 2, 355 + (128 - 108.8) / 2), (fitted.Scale, fitted.X, fitted.Y));
    }

    [Fact] // a missing layer, or a second identical fit, changes nothing
    public void FitLayerToRegion_no_ops_return_the_same_document()
    {
        var doc = new Document { Layers = ImmutableList.Create(LayerOf(1, 200, 100)) };
        Assert.Same(doc, DocumentOps.FitLayerToRegion(doc, 99, "torso_front"));
        var once = DocumentOps.FitLayerToRegion(doc, 1, "torso_front");
        Assert.Same(once, DocumentOps.FitLayerToRegion(once, 1, "torso_front"));
    }

    [Fact]
    public void FitLayerToRegion_refuses_a_key_the_mode_lacks()
    {
        var doc = new Document { Layers = ImmutableList.Create(LayerOf(1, 10, 10)) };
        Assert.Throws<ArgumentException>(() => DocumentOps.FitLayerToRegion(doc, 1, "waist_front"));
    }

    // ---- clear (btn-clear, keep) ----

    [Fact] // editor.js:1394: layers go; fills, background, mode, mask and grid stay
    public void ClearLayers_removes_layers_only()
    {
        var doc = WithFills(("torso_front", Cyan)) with
        {
            Mode = TemplateMode.Shirt,
            BgColor = "#00ff00",
            ShowGrid = true,
            Layers = ImmutableList.Create(LayerOf(1, 4, 4), LayerOf(2, 4, 4)),
        };
        var cleared = DocumentOps.ClearLayers(doc);
        Assert.Empty(cleared.Layers);
        Assert.Equal(doc.RegionFills, cleared.RegionFills);
        Assert.Equal(("#00ff00", true), (cleared.BgColor, cleared.ShowGrid));

        Assert.Same(cleared, DocumentOps.ClearLayers(cleared));
    }

    [Fact]
    public void RegionFitFraction_is_4_1s_constant() => Assert.Equal(0.85, DocumentOps.RegionFitFraction);
}
