using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class ToolsTests
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa);

    static Stroke Shape(ShapeTool tool, float x0, float y0, float x1, float y1, float width = 4, bool fill = false) =>
        new(tool, new[] { new SKPoint(x0, y0), new SKPoint(x1, y1) }, Cyan, width, fill);

    static int Differing(SKImage a, SKImage b)
    {
        using var x = ImageDiff.FromImage(a);
        using var y = ImageDiff.FromImage(b);
        return ImageDiff.Compare(x, y).DifferingPixels;
    }

    static SKImage Block(int x, int y, int w, int h) => Px.Render(TemplateSize.Width, TemplateSize.Height, c =>
    {
        using var p = new SKPaint { Color = SKColors.Red };
        c.DrawRect(SKRect.Create(x, y, w, h), p);
    });

    // ── RenderStroke ─────────────────────────────────────────────

    [Fact]
    public void Stroke_canvas_is_template_sized_kN32_and_transparent()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Line, 100, 100, 200, 100));
        Assert.Equal((TemplateSize.Width, TemplateSize.Height), (img.Width, img.Height));
        Assert.Equal(SKImageInfo.PlatformColorType, img.ColorType);
        Assert.Equal(0, Px.At(img, 10, 10).Alpha);
    }

    [Fact]
    public void Pen_strokes_the_whole_path_once()
    {
        // Half-alpha color: a path stroked once covers its own crossing a single time, so the
        // crossing has the same alpha as a plain segment. 4.1's per-move re-stroke would composite
        // the crossing over itself (editor.js:1199-1203, ledger draw-pen-incremental-stroke).
        var half = new SKColor(255, 0, 0, 128);
        var pen = new Stroke(ShapeTool.Pen,
            new[] { new SKPoint(100, 100), new SKPoint(200, 200), new SKPoint(200, 100), new SKPoint(100, 200) }, half, 10, false);
        using var img = Tools.RenderStroke(pen);
        Assert.Equal(128, Px.At(img, 150, 150).Alpha); // the self-crossing
        Assert.Equal(128, Px.At(img, 200, 150).Alpha); // middle of the vertical segment
    }

    [Fact]
    public void Pen_press_without_movement_draws_a_dot_of_the_brush_size()
    {
        // Approved divergence draw-pen-single-click: 4.1 paints nothing for a lone moveTo.
        using var img = Tools.RenderStroke(new Stroke(ShapeTool.Pen, new[] { new SKPoint(100.5f, 100.5f) }, Cyan, 10, false));
        Assert.Equal(255, Px.At(img, 100, 100).Alpha);
        Assert.Equal(255, Px.At(img, 103, 100).Alpha); // farthest corner 3.54 from center, radius 5
        Assert.Equal(0, Px.At(img, 106, 100).Alpha);   // nearest point 5.5 from center
        Assert.Equal(0, Px.At(img, 94, 100).Alpha);

        // Ledger autocrop parity check: layer bounds = dot bbox (pixels 95..105) padded by 10.
        var crop = Tools.Autocrop(img);
        Assert.NotNull(crop);
        using var cropped = crop.Value.Image;
        Assert.Equal((85, 85, 31, 31), (crop.Value.X, crop.Value.Y, cropped.Width, cropped.Height));
    }

    [Fact]
    public void Line_has_round_caps_and_ignores_fill()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Line, 100, 100, 200, 100, width: 10));
        Assert.Equal(255, Px.At(img, 150, 100).Alpha);
        Assert.Equal(255, Px.At(img, 97, 100).Alpha);  // inside the round cap; a butt cap stops at x=100
        Assert.Equal(0, Px.At(img, 206, 100).Alpha);   // past the cap radius
        using var filled = Tools.RenderStroke(Shape(ShapeTool.Line, 100, 100, 200, 100, width: 10, fill: true));
        Assert.Equal(0, Differing(img, filled));       // editor.js:1212-1216 never reads drawFill
    }

    [Fact]
    public void Rect_fill_covers_the_drag_rect_in_any_direction()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Rect, 100, 100, 150, 120, fill: true));
        Assert.Equal(255, Px.At(img, 100, 100).Alpha);
        Assert.Equal(255, Px.At(img, 149, 119).Alpha);
        Assert.Equal(0, Px.At(img, 99, 110).Alpha);
        Assert.Equal(0, Px.At(img, 150, 110).Alpha);
        Assert.Equal(0, Px.At(img, 125, 99).Alpha);
        Assert.Equal(0, Px.At(img, 125, 120).Alpha);
        using var reversed = Tools.RenderStroke(Shape(ShapeTool.Rect, 150, 120, 100, 100, fill: true));
        Assert.Equal(0, Differing(img, reversed));     // min/abs normalization, editor.js:1218-1221
    }

    [Fact]
    public void Rect_stroke_is_centered_on_the_edge_with_miter_corners()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Rect, 100, 100, 150, 120, width: 4));
        Assert.Equal(255, Px.At(img, 98, 110).Alpha);  // band x 98..102 on the left edge
        Assert.Equal(255, Px.At(img, 101, 110).Alpha);
        Assert.Equal(0, Px.At(img, 97, 110).Alpha);
        Assert.Equal(0, Px.At(img, 102, 110).Alpha);
        Assert.Equal(0, Px.At(img, 125, 110).Alpha);   // hollow
        Assert.Equal(255, Px.At(img, 98, 98).Alpha);   // square outer corner: a round join would cut it
    }

    [Fact]
    public void Rect_stroke_with_zero_height_draws_a_line()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Rect, 100, 100, 150, 100, width: 4));
        Assert.Equal(255, Px.At(img, 125, 100).Alpha);
        Assert.Equal(255, Px.At(img, 125, 98).Alpha);
        Assert.Equal(0, Px.At(img, 125, 97).Alpha);
    }

    [Fact]
    public void Ellipse_fill_uses_the_drag_box_center_and_radii()
    {
        // (100,100)-(160,140): center (130,120), rx 30, ry 20 (editor.js:1225-1228).
        using var img = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 100, 100, 160, 140, fill: true));
        Assert.Equal(255, Px.At(img, 130, 120).Alpha);
        Assert.Equal(0, Px.At(img, 99, 120).Alpha);
        Assert.Equal(0, Px.At(img, 160, 120).Alpha);
        Assert.Equal(0, Px.At(img, 130, 99).Alpha);
        Assert.Equal(0, Px.At(img, 130, 140).Alpha);
        Assert.Equal(0, Px.At(img, 101, 101).Alpha);   // the box corner is outside the ellipse
        using var reversed = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 160, 140, 100, 100, fill: true));
        Assert.Equal(0, Differing(img, reversed));
    }

    [Fact]
    public void Ellipse_stroke_is_hollow()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 100, 100, 160, 140, width: 4));
        Assert.Equal(0, Px.At(img, 130, 120).Alpha);
        Assert.True(Px.At(img, 130, 100).Alpha > 0);
        Assert.True(Px.At(img, 100, 120).Alpha > 0);
    }

    [Fact]
    public void Ellipse_with_one_zero_radius_strokes_a_line()
    {
        // Blink DegenerateEllipse: segments through the points at 0, pi/2, pi, 3pi/2, 2pi.
        using var img = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 100, 100, 100, 140, width: 4));
        Assert.Equal(255, Px.At(img, 100, 120).Alpha);
        Assert.Equal(255, Px.At(img, 98, 120).Alpha);
        Assert.Equal(0, Px.At(img, 97, 120).Alpha);
    }

    // ── Autocrop ─────────────────────────────────────────────────

    [Fact]
    public void Autocrop_pads_the_alpha_bounds_by_10_and_copies_pixels()
    {
        using var img = Block(50, 60, 4, 3);
        var crop = Tools.Autocrop(img);
        Assert.NotNull(crop);
        using var cropped = crop.Value.Image;
        Assert.Equal((40, 50, 24, 23), (crop.Value.X, crop.Value.Y, cropped.Width, cropped.Height));
        Assert.Equal(SKColors.Red, Px.At(cropped, 10, 10));
        Assert.Equal(SKColors.Red, Px.At(cropped, 13, 12));
        Assert.Equal(0, Px.At(cropped, 9, 10).Alpha);
        Assert.Equal(0, Px.At(cropped, 14, 12).Alpha);
    }

    [Fact]
    public void Autocrop_clips_the_padding_to_the_canvas()
    {
        using var topLeft = Block(0, 0, 2, 2);
        var a = Tools.Autocrop(topLeft)!.Value;
        using (a.Image) Assert.Equal((0, 0, 12, 12), (a.X, a.Y, a.Image.Width, a.Image.Height));

        using var bottomRight = Block(583, 557, 2, 2);
        var b = Tools.Autocrop(bottomRight)!.Value;
        using (b.Image) Assert.Equal((573, 547, 12, 12), (b.X, b.Y, b.Image.Width, b.Image.Height));
    }

    [Fact]
    public void Autocrop_of_an_empty_canvas_is_null()
    {
        using var empty = Px.Render(TemplateSize.Width, TemplateSize.Height, _ => { });
        Assert.Null(Tools.Autocrop(empty));
    }

    [Fact]
    public void Autocrop_counts_alpha_1_and_pad_0_means_10()
    {
        using var bmp = new SKBitmap(new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bmp.Erase(SKColors.Transparent);
        bmp.SetPixel(300, 200, new SKColor(255, 0, 0, 1));
        using var img = SKImage.FromBitmap(bmp);

        var crop = Tools.Autocrop(img)!.Value;
        using (crop.Image) Assert.Equal((290, 190, 21, 21), (crop.X, crop.Y, crop.Image.Width, crop.Image.Height));

        var zero = Tools.Autocrop(img, 0)!.Value; // pad || 10, editor.js:145
        using (zero.Image) Assert.Equal((290, 190, 21, 21), (zero.X, zero.Y, zero.Image.Width, zero.Image.Height));
    }

    // ── CommitDrawing ────────────────────────────────────────────

    [Fact]
    public void Commit_places_the_cropped_layer_at_the_crop_origin()
    {
        var layer = Tools.CommitDrawing(Shape(ShapeTool.Rect, 100, 100, 150, 120, fill: true), 7, "Rect drawing");
        Assert.NotNull(layer);
        Assert.Equal((7, "Rect drawing"), (layer.Id, layer.Name));
        Assert.Equal((90.0, 90.0, 1.0, 0.0, 1.0, true), (layer.X, layer.Y, layer.Scale, layer.Rotation, layer.Opacity, layer.Visible));
        Assert.Equal((false, false, 100.0, 100.0, 100.0, 0.0), (layer.FlipH, layer.FlipV, layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate));
        Assert.Equal((70, 40), (layer.Image.Width, layer.Image.Height));
        // The toDataURL round trip: stored un-premultiplied, like Images.Decode.
        Assert.Equal((SKColorType.Rgba8888, SKAlphaType.Unpremul), (layer.Image.ColorType, layer.Image.AlphaType));
        Assert.Equal(new SKColor(0x17, 0xd4, 0xfa, 255), Px.At(layer.Image, 10, 10));
        Assert.Equal(0, Px.At(layer.Image, 9, 10).Alpha);
    }

    [Fact]
    public void Nothing_drawn_commits_no_layer()
    {
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Rect, 100, 100, 100, 100, fill: true), 1, "Rect drawing"));
        Assert.Null(Tools.CommitDrawing(new Stroke(ShapeTool.Line, new[] { new SKPoint(100, 100) }, Cyan, 4, false), 1, "Line drawing"));
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Ellipse, 100, 100, 100, 100), 1, "Circle drawing"));
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Ellipse, 100, 100, 100, 140, fill: true), 1, "Circle drawing"));
        Assert.Null(Tools.CommitDrawing(new Stroke(ShapeTool.Pen, Array.Empty<SKPoint>(), Cyan, 4, false), 1, "Pen drawing"));
    }

    [Fact]
    public void A_shape_entirely_off_the_template_commits_no_layer()
    {
        // Review Focus: the stroke canvas is 585x559 (editor.js:1128-1131), so a drag wholly outside
        // it leaves no pixel and 4.1's alpha scan commits nothing (editor.js:1257-1262).
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Line, -60, -60, -10, -10), 1, "Line drawing")); // round cap reaches x = -8
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Rect, 600, 100, 700, 200, fill: true), 1, "Rect drawing"));
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Ellipse, 100, 570, 200, 640), 1, "Circle drawing")); // top edge y = 568
    }

    [Fact]
    public void Commit_keeps_only_the_part_inside_the_template()
    {
        var layer = Tools.CommitDrawing(Shape(ShapeTool.Line, -50, 100, 50, 100), 1, "Line drawing");
        Assert.NotNull(layer);
        Assert.Equal((0.0, 88.0), (layer.X, layer.Y)); // left end clipped at x=0; band y 98..102, minus pad
    }

    [Theory]
    [InlineData(ShapeTool.Pen, "Pen drawing")]
    [InlineData(ShapeTool.Line, "Line drawing")]
    [InlineData(ShapeTool.Rect, "Rect drawing")]
    [InlineData(ShapeTool.Ellipse, "Circle drawing")]
    public void Drawing_layers_are_named_after_the_4_1_tool_id(ShapeTool tool, string name) =>
        Assert.Equal(name, Tools.DrawingName(tool));
}
