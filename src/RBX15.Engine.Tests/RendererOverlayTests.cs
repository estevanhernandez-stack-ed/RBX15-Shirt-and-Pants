using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The view's selection overlay (editor.js:361-425) and live preview (editor.js:324-330) as unit facts: what the
/// view-select goldens cannot show (the device scale, hidden layers, the label's face and bounds, the preview's place in
/// the composite, the preview being the commit). Ledger rows: render-selection-box, render-resize-handles,
/// render-rotation-handle, render-rotation-label, select-handle-render, select-hidden-layer-still-handled,
/// render-live-preview, draw-preview-composite.
/// </summary>
public class RendererOverlayTests
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa);
    static readonly SKColor Magenta = new(0xf2, 0x2f, 0x89);

    /// <summary>A 100x50 layer at (200, 200): center (250, 225), knob at (250, 170) at zoom 1.</summary>
    static Document OneLayer(bool visible = true, double rotation = 0) => new()
    {
        BgColor = "#000000",
        ShowMask = false,
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(100, 50, SKColors.Red), X = 200, Y = 200, Visible = visible, Rotation = rotation }),
    };

    static SKBitmap Frame(Document doc, ViewOptions options)
    {
        using var image = Renderer.RenderViewImage(doc, options);
        return ImageDiff.FromImage(image);
    }

    [Fact] // ViewOptions additions are init-only, null by default, and keep M3a's guards on with
    public void View_options_default_to_no_overlay_and_no_preview()
    {
        var o = new ViewOptions(1);
        Assert.Null(o.SelectedLayerId);
        Assert.Null(o.Preview);
        var p = LivePreview.Erase(new[] { new SKPoint(1, 1), new SKPoint(9, 9) }, 4);
        var s = o with { SelectedLayerId = 3, Preview = p };
        Assert.Equal((3, p), (s.SelectedLayerId, s.Preview));
        Assert.Throws<ArgumentOutOfRangeException>(() => s with { Zoom = 0 });
    }

    [Fact] // editor.js:362-363; ruling R2: no selection, a missing id or a hidden layer draws no overlay
    public void No_overlay_without_a_visible_selected_layer()
    {
        using var plain = Frame(OneLayer(), new ViewOptions(1));
        using var missing = Frame(OneLayer(), new ViewOptions(1) { SelectedLayerId = 9 });
        Assert.Equal(plain.Bytes, missing.Bytes);
        using var hiddenPlain = Frame(OneLayer(visible: false), new ViewOptions(1));
        using var hiddenSelected = Frame(OneLayer(visible: false, rotation: 30), new ViewOptions(1) { SelectedLayerId = 1 });
        Assert.Equal(hiddenPlain.Bytes, hiddenSelected.Bytes);
        using var shown = Frame(OneLayer(), new ViewOptions(1) { SelectedLayerId = 1 });
        Assert.NotEqual(plain.Bytes, shown.Bytes);
    }

    [Theory] // render-resize-handles parityCheck: 8 px squares at every zoom; the device scale multiplies them
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(0.5, 1)]
    [InlineData(1, 2)]
    public void Handles_are_8_css_px_at_every_zoom(double zoom, double deviceScale)
    {
        using var f = Frame(OneLayer(), new ViewOptions(zoom, deviceScale) { SelectedLayerId = 1 });
        double s = zoom * deviceScale;
        int x = (int)Math.Round(200 * s), y = (int)Math.Round(200 * s); // the top-left corner, device px
        int inside = (int)(3 * deviceScale), outside = (int)(5 * deviceScale);
        Assert.Equal(Cyan, f.GetPixel(x - inside, y - inside));
        Assert.Equal(Cyan, f.GetPixel(x + inside - 1, y + inside - 1));
        Assert.NotEqual(Cyan, f.GetPixel(x - outside, y - outside));
    }

    [Fact] // render-selection-box parityCheck: 2 px #17d4fa, dash [5, 5] from the top-left corner, clockwise
    public void The_box_is_a_2_px_cyan_dash_from_the_top_left()
    {
        using var f = Frame(OneLayer(), new ViewOptions(1) { SelectedLayerId = 1 });
        foreach (int row in new[] { 199, 200 }) // the 2 px stroke centered on y = 200
        {
            Assert.Equal(Cyan, f.GetPixel(204, row));  // dash 0..5 (past the 8 px handle)
            Assert.NotEqual(Cyan, f.GetPixel(207, row)); // gap 5..10
            Assert.Equal(Cyan, f.GetPixel(212, row));  // dash 10..15
        }
        Assert.NotEqual(Cyan, f.GetPixel(207, 198));
        Assert.NotEqual(Cyan, f.GetPixel(212, 201));
    }

    [Fact] // render-rotation-handle: the stem is 2 px #f22f89 up to the knob, the knob #f22f89 30 screen px above the edge
    public void The_stem_and_knob_sit_30_screen_px_above_the_top_edge()
    {
        foreach (double zoom in new[] { 1.0, 2.0 })
        {
            using var f = Frame(OneLayer(), new ViewOptions(zoom) { SelectedLayerId = 1 });
            int cx = (int)(250 * zoom), top = (int)(200 * zoom);
            Assert.Equal(Magenta, f.GetPixel(cx, top - 15));     // the stem, between the edge and the knob
            Assert.Equal(Magenta, f.GetPixel(cx - 1, top - 15));
            Assert.NotEqual(Magenta, f.GetPixel(cx + 2, top - 15));
            Assert.Equal(Magenta, f.GetPixel(cx, top - 30));     // the knob's center
            Assert.NotEqual(Magenta, f.GetPixel(cx, top - 40));  // above the knob (radius 8 plus its white rim)
        }
    }

    [Fact] // ruling T1-1: the knob is drawn where SelectionGeometry hit-tests it, not at 4.1's mirrored getRotHandlePos
    public void The_rotate_handle_is_hit_where_the_overlay_draws_it()
    {
        var doc = OneLayer(rotation: 90); // center (250, 225); the knob 25 + 30 to the right
        var (kx, ky) = SelectionGeometry.RotateHandle(doc.Layers[0], 1);
        using var f = Frame(doc, new ViewOptions(1) { SelectedLayerId = 1 });
        Assert.Equal(Magenta, f.GetPixel((int)Math.Round(kx), (int)Math.Round(ky)));
        Assert.NotEqual(Magenta, f.GetPixel(195, 225)); // 4.1's mirrored hit point: nothing is drawn there
        Assert.Equal(SelectHitKind.Rotate, SelectionGeometry.HitTest(doc, 1, kx, ky, 1).Kind);
    }

    [Fact] // ruling R4: every label pixel lies inside LabelBounds; rotation 0 has no label
    public void The_label_draws_only_inside_its_bounds()
    {
        foreach (var (rotation, zoom, scale) in new[] { (30.0, 1.0, 1.0), (-135.0, 1.5, 1.0), (17.25, 2.0, 1.0), (90.0, 1.0, 2.0), (-0.04, 0.5, 1.0) })
        {
            var layer = OneLayer(rotation: rotation).Layers[0];
            var options = new ViewOptions(zoom, scale);
            var size = Renderer.ViewSize(options);
            using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            Renderer.DrawRotationLabel(surface.Canvas, layer, options);
            using var image = surface.Snapshot();
            using var px = ImageDiff.FromImage(image);
            var box = SelectionGeometry.LabelBounds(layer, options);
            int drawn = 0;
            for (int y = 0; y < px.Height; y++)
                for (int x = 0; x < px.Width; x++)
                    if (px.GetPixel(x, y).Alpha > 0)
                    {
                        drawn++;
                        Assert.True(box.Contains(x, y), $"rotation {rotation}: label pixel ({x}, {y}) outside {box}");
                    }
            Assert.True(drawn > 20, $"rotation {rotation}: the label drew {drawn} pixels");
        }
        using var none = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        none.Canvas.Clear(SKColors.Transparent);
        Renderer.DrawRotationLabel(none.Canvas, OneLayer().Layers[0], new ViewOptions(1));
        using var blank = none.Snapshot();
        Assert.False(Eraser.HasContent(blank));
    }

    [Fact] // render-rotation-label: "30.0°" in 11 px JetBrains Mono 400, #f22f89, centered on (0, -h/2 - 46) in the rotated frame
    public void The_label_is_11_px_jetbrains_mono_magenta_centered_46_px_above_the_top_edge()
    {
        var layer = OneLayer(rotation: 30).Layers[0];
        using var got = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        got.Canvas.Clear(SKColors.Transparent);
        Renderer.DrawRotationLabel(got.Canvas, layer, new ViewOptions(1));
        using var want = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var c = want.Canvas;
        c.Clear(SKColors.Transparent);
        c.Translate(250, 225);
        c.RotateRadians((float)(30 * Math.PI / 180));
        using (var font = Fonts.CreateFont("JetBrains Mono", 400, 11))
        using (var paint = new SKPaint { Color = Magenta, IsAntialias = true })
            c.DrawText("30.0°", 0, -25 - 30 - 16, SKTextAlign.Center, font, paint);
        using var a = got.Snapshot();
        using var b = want.Snapshot();
        using var ga = ImageDiff.FromImage(a);
        using var gb = ImageDiff.FromImage(b);
        Assert.Equal(gb.Bytes, ga.Bytes);
        Assert.True(Eraser.HasContent(a));
    }

    static Document Template(bool mask) => new() { BgColor = "#000000", ShowMask = mask };

    [Fact] // render-live-preview, draw-preview-composite: after the layers, before the mask
    public void A_draw_preview_sits_between_the_layers_and_the_mask()
    {
        // A red rect from torso_front (231..359) across the gutter at x = 360 into torso_left (361..425).
        var preview = LivePreview.Draw(new Stroke(ShapeTool.Rect, new[] { new SKPoint(300, 100), new SKPoint(400, 150) }, SKColors.Red, 4, true));
        using var masked = Frame(Template(mask: true), new ViewOptions(1) { Preview = preview });
        using var open = Frame(Template(mask: false), new ViewOptions(1) { Preview = preview });
        Assert.Equal(SKColors.Red, masked.GetPixel(320, 120));      // inside a region
        Assert.NotEqual(SKColors.Red, masked.GetPixel(360, 120));   // the gutter is masked
        Assert.Equal(SKColors.Red, open.GetPixel(360, 120));        // mask off shows it
        var layered = Template(mask: false) with
        {
            Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(50, 50, SKColors.Blue), X = 290, Y = 90 }),
        };
        using var over = Frame(layered, new ViewOptions(1) { Preview = preview });
        Assert.Equal(SKColors.Red, over.GetPixel(320, 120));         // the preview is above the layers
    }

    [Fact] // editor.js:327: the eraser previews #f22f89 at half alpha; ruling R3: one point shows nothing
    public void An_eraser_preview_is_magenta_at_half_alpha_and_a_click_shows_nothing()
    {
        var doc = Template(mask: false);
        using var plain = Frame(doc, new ViewOptions(1));
        using var drag = Frame(doc, new ViewOptions(1) { Preview = LivePreview.Erase(new[] { new SKPoint(100, 100.5f), new SKPoint(200, 100.5f) }, 9) });
        Px.Near(new SKColor(121, 24, 68), drag.GetPixel(150, 100)); // 0.5 x #f22f89 over black
        using var click = Frame(doc, new ViewOptions(1) { Preview = LivePreview.Erase(new[] { new SKPoint(150, 100) }, 9) });
        Assert.Equal(plain.Bytes, click.Bytes);
    }

    [Fact] // ruling R12, draw-pen-single-click: a pen press previews the brush-size dot the commit will make
    public void A_pen_press_previews_its_dot()
    {
        var doc = Template(mask: false);
        using var dot = Frame(doc, new ViewOptions(1) { Preview = LivePreview.Draw(new Stroke(ShapeTool.Pen, new[] { new SKPoint(150.5f, 100.5f) }, SKColors.White, 10, false)) });
        Assert.Equal(SKColors.White, dot.GetPixel(150, 100));
        Assert.Equal(SKColors.White, dot.GetPixel(153, 100));
        Assert.Equal(SKColors.Black, dot.GetPixel(157, 100));
    }

    [Theory] // ruling R12: the preview composite equals the composite with the committed drawing layer, within 1 level
    [InlineData(ShapeTool.Pen)]
    [InlineData(ShapeTool.Line)]
    [InlineData(ShapeTool.Rect)]
    [InlineData(ShapeTool.Ellipse)]
    public void The_preview_is_the_commit(ShapeTool tool)
    {
        var stroke = new Stroke(tool, new[] { new SKPoint(120, 90), new SKPoint(260, 140), new SKPoint(330, 260) }, new SKColor(0x17, 0xd4, 0xfa), 7, false);
        var doc = Template(mask: false);
        using var previewSurface = SKSurface.Create(Renderer.TemplateInfo);
        Renderer.RenderComposite(previewSurface.Canvas, doc, false, LivePreview.Draw(stroke), null);
        var layer = Tools.CommitDrawing(stroke, 1, Tools.DrawingName(tool))!;
        using var commitSurface = SKSurface.Create(Renderer.TemplateInfo);
        Renderer.RenderComposite(commitSurface.Canvas, doc with { Layers = ImmutableList.Create(layer) }, false);
        using var a = previewSurface.Snapshot();
        using var b = commitSurface.Snapshot();
        using var pa = ImageDiff.FromImage(a);
        using var pb = ImageDiff.FromImage(b);
        Assert.True(ImageDiff.Compare(pb, pa, edgeTolerance: 1).MaxDelta <= 1, $"{tool}: {ImageDiff.Compare(pb, pa)}");
    }
}
