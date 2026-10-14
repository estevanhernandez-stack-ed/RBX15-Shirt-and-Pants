using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// tests/erase.test.js ported case for case (same layers, strokes, pixels and expected alphas),
/// plus Apply's commit rules (editor.js:1257-1283) and the stroke-once versus 4.1-replay question.
/// </summary>
public class EraserTests
{
    const int TW = TemplateSize.Width, TH = TemplateSize.Height;

    static readonly SKColor Red = new(255, 0, 0);

    /// <summary>erase.test.js:12-18 solidLayer.</summary>
    static SKImage SolidLayer(int w, int h) => Px.Solid(w, h, Red);

    /// <summary>erase.test.js:20-26 strokeRect: a template-sized canvas with one #000 rect.</summary>
    static SKImage StrokeRect(int x, int y, int w, int h, byte alpha = 255) =>
        Px.Render(TW, TH, c =>
        {
            using var p = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
            c.DrawRect(SKRect.Create(x, y, w, h), p);
        });

    /// <summary>erase.test.js:32 IDENTITY, as a Layer.</summary>
    static Layer At(SKImage image, double x = 0, double y = 0, double scale = 1, double rotation = 0,
                    bool flipH = false, bool flipV = false, int id = 1) =>
        new() { Id = id, Image = image, X = x, Y = y, Scale = scale, Rotation = rotation, FlipH = flipH, FlipV = flipV };

    static int AlphaAt(SKImage image, int x, int y) => Px.At(image, x, y).Alpha;

    static byte[] Alphas(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        var px = bmp.GetPixelSpan();
        var a = new byte[image.Width * image.Height];
        for (int i = 0; i < a.Length; i++) a[i] = px[i * 4 + 3];
        return a;
    }

    // ---- erase.test.js, case for case ----

    [Fact]
    public void Erases_stroked_pixels_at_identity_and_leaves_the_rest()
    {
        // erase.test.js:34-39
        using var stroke = StrokeRect(5, 5, 6, 6);
        using var erased = Eraser.EraseStroke(At(SolidLayer(20, 20)), stroke);
        Assert.Equal(0, AlphaAt(erased, 7, 7));
        Assert.Equal(255, AlphaAt(erased, 15, 15));
    }

    [Fact]
    public void Maps_the_template_stroke_through_offset_and_scale()
    {
        // erase.test.js:41-49: 10x10 layer at (20,20) scaled 2x covers template 20..40.
        using var stroke = StrokeRect(20, 20, 10, 10);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2), stroke);
        Assert.Equal(0, AlphaAt(erased, 2, 2));
        Assert.Equal(255, AlphaAt(erased, 8, 8));
    }

    [Fact]
    public void Rotation_180_maps_a_top_left_stroke_to_bottom_right_pixels()
    {
        // erase.test.js:51-57
        using var stroke = StrokeRect(20, 20, 10, 10);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2, rotation: 180), stroke);
        Assert.Equal(0, AlphaAt(erased, 8, 8));
        Assert.Equal(255, AlphaAt(erased, 2, 2));
    }

    [Fact]
    public void FlipH_maps_a_left_side_stroke_to_right_side_pixels()
    {
        // erase.test.js:59-65
        using var stroke = StrokeRect(20, 20, 10, 20);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2, flipH: true), stroke);
        Assert.Equal(0, AlphaAt(erased, 8, 5));
        Assert.Equal(255, AlphaAt(erased, 2, 5));
    }

    [Fact]
    public void Does_not_mutate_the_source_layer_image()
    {
        // erase.test.js:67-71, plus the 4.0 regression guard (ledger tests-lib-erase): the result
        // really is erased, the source really is not.
        using var source = SolidLayer(20, 20);
        using var stroke = StrokeRect(0, 0, 20, 20);
        using var erased = Eraser.EraseStroke(At(source), stroke);
        Assert.Equal(255, AlphaAt(source, 10, 10));
        Assert.Equal(0, AlphaAt(erased, 10, 10));
    }

    // ---- ledger parity checks beyond erase.test.js ----

    [Fact]
    public void FlipV_maps_a_top_stroke_to_bottom_pixels()
    {
        // erase.js:60 flips both axes; the flipH case (erase.test.js:59-65) transposed.
        using var stroke = StrokeRect(20, 20, 20, 10);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2, flipV: true), stroke);
        Assert.Equal(0, AlphaAt(erased, 5, 8));
        Assert.Equal(255, AlphaAt(erased, 5, 2));
    }

    [Fact]
    public void Rotated_30_degrees_the_hole_lands_under_the_cursor()
    {
        // Ledger tests-lib-erase (SMOKE_TEST A2). A 40x40 layer at (100,100), center (120,120),
        // rotated 30 degrees. The stroke centered at template (129,125) is offset (9,5) from the
        // center; un-rotating by 30 degrees gives image offset (10.29,-0.17), image pixel (30,19).
        // With the rotation sign wrong it would land at image (25.29,28.83), pixel (25,28).
        using var stroke = StrokeRect(127, 123, 4, 4);
        using var erased = Eraser.EraseStroke(At(SolidLayer(40, 40), x: 100, y: 100, rotation: 30), stroke);
        Assert.Equal(0, AlphaAt(erased, 30, 19));
        Assert.Equal(255, AlphaAt(erased, 25, 28));
        Assert.Equal(255, AlphaAt(erased, 10, 20));
    }

    [Fact]
    public void Erase_amount_is_proportional_to_stroke_alpha()
    {
        // erase.js:34-35 and :69-70: destination-out keeps dest alpha * (1 - maskA).
        // 255 * (1 - 128/255) = 127.
        using var stroke = StrokeRect(0, 0, 20, 20, alpha: 128);
        using var erased = Eraser.EraseStroke(At(SolidLayer(20, 20)), stroke);
        Assert.Equal(127, AlphaAt(erased, 10, 10));
        Assert.Equal(255, Px.At(erased, 10, 10).Red);
    }

    [Fact]
    public void Scale_zero_skips_the_non_finite_inverse_scale_like_canvas()
    {
        // Review Focus. A hand-edited project can carry scale 0 (the UI clamps at 0.01). erase.js:58
        // then calls scale(1/0, 1/0), which canvas ignores, and w = h = 0 puts the center at (x, y)
        // (erase.js:47-50). The mask CTM is translate(imgW/2, imgH/2) then translate(-x, -y), so
        // template pixels 0..4 land on image pixels 10..14.
        using var stroke = StrokeRect(0, 0, 5, 5);
        using var erased = Eraser.EraseStroke(At(SolidLayer(20, 20), scale: 0), stroke);
        Assert.Equal(0, AlphaAt(erased, 12, 12));
        Assert.Equal(255, AlphaAt(erased, 2, 2));
        Assert.Equal(255, AlphaAt(erased, 17, 17));
    }

    [Fact]
    public void Result_is_unpremultiplied_rgba_at_the_layer_image_size()
    {
        // editor.js:1276-1282: the result goes through toDataURL and a new Image, the same
        // representation Images.Decode gives every layer image.
        using var stroke = StrokeRect(0, 0, 5, 5);
        using var erased = Eraser.EraseStroke(At(SolidLayer(30, 12), x: 3, y: 4, scale: 0.5), stroke);
        Assert.Equal((30, 12), (erased.Width, erased.Height));
        Assert.Equal((SKColorType.Rgba8888, SKAlphaType.Unpremul), (erased.ColorType, erased.AlphaType));
    }

    // ---- Apply: the commit rules ----

    [Fact]
    public void A_click_without_movement_erases_nothing()
    {
        // editor.js:1143-1144 only moveTo on mousedown; with no mousemove nothing is stroked, and the
        // alpha scan (1257-1264) skips the erase.
        var layer = At(SolidLayer(20, 20));
        Assert.Same(layer, Eraser.Apply(layer, [new SKPoint(10, 10)], 8));
        Assert.Same(layer, Eraser.Apply(layer, [], 8));
    }

    [Fact]
    public void A_drag_entirely_off_the_template_erases_nothing()
    {
        // The stroke canvas is 585x559 (editor.js:1129-1131); a stroke outside it leaves no pixel.
        var layer = At(SolidLayer(20, 20));
        Assert.Same(layer, Eraser.Apply(layer, [new SKPoint(-100, -100), new SKPoint(-50, -50)], 4));
    }

    [Fact]
    public void Apply_erases_along_the_drag_and_keeps_the_layer_identity_and_transform()
    {
        var source = SolidLayer(100, 100);
        var layer = At(source, x: 0, y: 0) with { Name = "Kept", Opacity = 0.5 };
        var erased = Eraser.Apply(layer, [new SKPoint(10, 50), new SKPoint(90, 50)], 10);
        Assert.NotSame(source, erased.Image);
        Assert.Equal(0, AlphaAt(erased.Image, 50, 50));
        Assert.Equal(255, AlphaAt(erased.Image, 50, 10));
        Assert.Equal(layer with { Image = erased.Image }, erased);
        Assert.Equal(255, AlphaAt(source, 50, 50));
    }

    [Fact]
    public void Layers_sharing_an_image_keep_their_pixels()
    {
        // Ledger layer-sharing-img: clones share one image; only the erased layer gets a new one.
        var shared = SolidLayer(100, 100);
        var a = At(shared, id: 1);
        var b = At(shared, id: 2);
        var erased = Eraser.Apply(a, [new SKPoint(10, 50), new SKPoint(90, 50)], 10);
        Assert.Equal(0, AlphaAt(erased.Image, 50, 50));
        Assert.Same(shared, b.Image);
        Assert.Equal(255, AlphaAt(b.Image, 50, 50));
    }

    [Fact]
    public void An_rgb_shifted_layer_renders_the_erased_pixels()
    {
        // editor.js:1279 invalidates 4.1's single-slot shift cache after an erase. v5's shift cache
        // is keyed by image (Adjust.ShiftRgb), so the new image cannot hit the old entry.
        var layer = At(SolidLayer(20, 20), x: 100, y: 100) with { RShift = -40 };
        using var before = Px.Render(TW, TH, c => Renderer.DrawLayer(c, layer));
        var erased = Eraser.Apply(layer, [new SKPoint(103, 110), new SKPoint(117, 110)], 6);
        using var after = Px.Render(TW, TH, c => Renderer.DrawLayer(c, erased));
        Assert.Equal(255, AlphaAt(before, 110, 110));
        Assert.Equal(0, AlphaAt(after, 110, 110));
    }

    // ---- stroke once (v5) versus 4.1's per-move replay ----

    static readonly SKPoint[] Zigzag = [new(100, 100), new(160, 130), new(220, 100), new(280, 140)];

    [Fact]
    public void A_two_point_drag_strokes_identically_once_and_replayed()
    {
        // One mousemove = one stroke() in 4.1 (editor.js:1200-1203), and no stroke at mouseup for the
        // eraser (1253-1255). So a two-point erase is pixel-identical in v5 and 4.1.
        SKPoint[] two = [new(100, 100), new(260, 140)];
        using var once = Eraser.StrokeCanvas(two, 20, EraseStroking.Once);
        using var replay = Eraser.StrokeCanvas(two, 20, EraseStroking.Replay41);
        Assert.Equal(Alphas(replay), Alphas(once));
    }

    [Fact]
    public void Invalid_sizes_keep_the_canvas_default_line_width()
    {
        // Review Focus. Canvas2D ignores a lineWidth that is 0, negative, NaN or infinite, and the
        // stroke canvas is fresh per gesture (editor.js:1129-1131), so its default width 1 stays.
        // A width-1 line on y = 100.5 covers row 100 fully.
        SKPoint[] drag = [new(100, 100.5f), new(200, 100.5f)];
        using var one = Eraser.StrokeCanvas(drag, 1);
        Assert.Equal(255, AlphaAt(one, 150, 100));
        foreach (var size in new[] { 0f, -3f, float.NaN, float.PositiveInfinity })
        {
            using var got = Eraser.StrokeCanvas(drag, size);
            Assert.Equal(Alphas(one), Alphas(got));
        }
    }

    [Fact]
    public void Replay_only_raises_partially_covered_pixels()
    {
        // 4.1 composites segment j of n (n - j + 1) times. Fully covered pixels are 255 either way,
        // and src-over never lowers alpha, so the replay can differ only where the single stroke is
        // below 255: the antialiased fringe. On a zigzag of three segments some fringe pixel rises.
        using var once = Eraser.StrokeCanvas(Zigzag, 20, EraseStroking.Once);
        using var replay = Eraser.StrokeCanvas(Zigzag, 20, EraseStroking.Replay41);
        var (o, r) = (Alphas(once), Alphas(replay));
        int raised = 0;
        for (int i = 0; i < o.Length; i++)
        {
            if (o[i] == 255) Assert.Equal(255, r[i]);
            Assert.True(r[i] >= o[i], $"pixel {i % TW},{i / TW}: replay {r[i]} below once {o[i]}");
            if (r[i] > o[i]) raised++;
        }
        Assert.True(raised > 0, "a three-segment drag must raise some fringe pixel when replayed");
    }

    [Fact]
    public void Opaque_erasure_is_identical_and_only_the_fringe_keeps_more_alpha_once()
    {
        // At identity the mask is the stroke canvas, so the erased layer mirrors the test above:
        // wherever the single stroke erases fully, the replay does too; elsewhere stroke-once leaves
        // at least as much alpha as 4.1 did. This is the whole stroke-once divergence.
        var layer = At(SolidLayer(400, 200));
        var once = Eraser.Apply(layer, Zigzag, 20, EraseStroking.Once);
        var replay = Eraser.Apply(layer, Zigzag, 20, EraseStroking.Replay41);
        var (o, r) = (Alphas(once.Image), Alphas(replay.Image));
        int fullyErased = 0;
        for (int i = 0; i < o.Length; i++)
        {
            if (o[i] == 0) { Assert.Equal(0, r[i]); fullyErased++; }
            Assert.True(o[i] >= r[i], $"pixel {i % 400},{i / 400}: once {o[i]} below replay {r[i]}");
        }
        Assert.True(fullyErased > 0);
    }
}
