using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// SelectionGeometry: the Select tool's press, rotate, resize and wheel math (editor.js:1018-1192, :1305-1313) and the
/// overlay label's text and bounds (editor.js:415-420). Ledger rows: select-hit-test, select-rotate-handle-hit,
/// select-resize-handle-hit, select-resize-handles, select-resize-drag, select-rotate-drag, select-rotate-drag-2,
/// select-move-hit-and-drag, select-hidden-layer-still-handled, props-scale-clamp-inconsistency, wheel-scale-layer,
/// render-rotation-label.
/// </summary>
public class SelectionGeometryTests
{
    static Layer L(int id = 1, int w = 100, int h = 50, double x = 200, double y = 200, double scale = 1, double rotation = 0, bool visible = true) =>
        new() { Id = id, Image = Px.Solid(w, h, SKColors.Red), X = x, Y = y, Scale = scale, Rotation = rotation, Visible = visible };

    static Document Doc(params Layer[] layers) => new() { Layers = ImmutableList.Create(layers) };

    static void Near((double X, double Y) expected, (double X, double Y) actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    [Fact] // getLayerCenter (editor.js:1023-1027)
    public void Center_is_the_scaled_box_center() =>
        Near((110, 70), SelectionGeometry.Center(L(w: 100, h: 50, x: 10, y: 20, scale: 2)));

    [Fact] // editor.js:1080-1088: center + R(rotation) * (±w/2, ±h/2), TL TR BR BL
    public void Corners_rotate_about_the_center_in_4_1_order()
    {
        var unrotated = L();
        Near((200, 200), SelectionGeometry.Corner(unrotated, ResizeCorner.TopLeft));
        Near((300, 200), SelectionGeometry.Corner(unrotated, ResizeCorner.TopRight));
        Near((300, 250), SelectionGeometry.Corner(unrotated, ResizeCorner.BottomRight));
        Near((200, 250), SelectionGeometry.Corner(unrotated, ResizeCorner.BottomLeft));
        var quarter = L(rotation: 90); // center (250, 225): local (x, y) maps to (250 - y, 225 + x)
        Near((275, 175), SelectionGeometry.Corner(quarter, ResizeCorner.TopLeft));
        Near((275, 275), SelectionGeometry.Corner(quarter, ResizeCorner.TopRight));
        Near((225, 275), SelectionGeometry.Corner(quarter, ResizeCorner.BottomRight));
        Near((225, 175), SelectionGeometry.Corner(quarter, ResizeCorner.BottomLeft));
    }

    [Theory] // editor.js:1035: h/2 + 30/zoom, so the knob stays 30 screen px above the top edge
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void The_rotate_handle_is_30_screen_px_above_the_top_edge(double zoom) =>
        Near((250, 200 - 30 / zoom), SelectionGeometry.RotateHandle(L(), zoom));

    [Fact] // ruling T1-1: rotate(r) maps (0, -d) to (d sin r, -d cos r); 4.1's getRotHandlePos uses c.x - sin r * d (mirrored)
    public void The_rotate_handle_turns_clockwise_with_the_layer()
    {
        var layer = L(rotation: 90); // center (250, 225), d = 25 + 30
        Near((305, 225), SelectionGeometry.RotateHandle(layer, 1));
        Near((250 + 55 * Math.Sin(Math.PI / 6), 225 - 55 * Math.Cos(Math.PI / 6)), SelectionGeometry.RotateHandle(layer with { Rotation = 30 }, 1));
        Near((250, 280), SelectionGeometry.RotateHandle(layer with { Rotation = 180 }, 1)); // 4.1 agrees only at 0 and 180
        var doc = Doc(layer);
        Assert.Equal(new SelectHit(SelectHitKind.Rotate, 1, default), SelectionGeometry.HitTest(doc, 1, 305, 225, 1));
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(doc, 1, 195, 225, 1).Kind); // 4.1's mirrored spot
    }

    [Theory] // select-rotate-handle-hit parityCheck: 13.9 hits, 14.1 misses (screen px, strict <)
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void The_rotate_handle_hits_strictly_inside_14_screen_px(double zoom)
    {
        var doc = Doc(L());
        var (hx, hy) = SelectionGeometry.RotateHandle(doc.Layers[0], zoom);
        Assert.Equal(SelectHitKind.Rotate, SelectionGeometry.HitTest(doc, 1, hx, hy - 13.9 / zoom, zoom).Kind);
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(doc, 1, hx, hy - 14.1 / zoom, zoom).Kind);
    }

    [Theory] // select-resize-handle-hit parityCheck: 9.9 hits, 10.1 misses, each corner in 4.1's order
    [InlineData(ResizeCorner.TopLeft, -1, -1)]
    [InlineData(ResizeCorner.TopRight, 1, -1)]
    [InlineData(ResizeCorner.BottomRight, 1, 1)]
    [InlineData(ResizeCorner.BottomLeft, -1, 1)]
    public void A_corner_hits_strictly_inside_10_screen_px(ResizeCorner corner, int dx, int dy)
    {
        var doc = Doc(L());
        var (cx, cy) = SelectionGeometry.Corner(doc.Layers[0], corner);
        double u = Math.Sqrt(0.5);
        foreach (double zoom in new[] { 0.5, 1, 2 })
        {
            Assert.Equal(new SelectHit(SelectHitKind.Resize, 1, corner),
                SelectionGeometry.HitTest(doc, 1, cx + dx * u * 9.9 / zoom, cy + dy * u * 9.9 / zoom, zoom));
            Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(doc, 1, cx + dx * u * 10.1 / zoom, cy + dy * u * 10.1 / zoom, zoom).Kind);
        }
    }

    [Fact] // editor.js:1064-1117 order: knob, corners, then the topmost visible box
    public void The_knob_beats_other_layers_and_a_corner_beats_the_body()
    {
        var selected = L(id: 1);
        var above = L(id: 2, w: 40, h: 40, x: 230, y: 150); // on top, covering the knob at (250, 170)
        var doc = Doc(selected, above);
        Assert.Equal(new SelectHit(SelectHitKind.Rotate, 1, default), SelectionGeometry.HitTest(doc, 1, 250, 170, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Resize, 1, ResizeCorner.TopLeft), SelectionGeometry.HitTest(doc, 1, 203.5, 203.5, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 2, default), SelectionGeometry.HitTest(doc, null, 250, 170, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, 1, 250, 225, 1));
    }

    [Fact] // ruling R2, select-hidden-layer-still-handled: no handles on a hidden layer; the press falls through to the pick
    public void A_hidden_selected_layer_has_no_handles()
    {
        var under = L(id: 1, w: 585, h: 559, x: 0, y: 0);
        var hidden = L(id: 2, visible: false);
        var doc = Doc(under, hidden);
        var (kx, ky) = SelectionGeometry.RotateHandle(hidden, 1);
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, 2, kx, ky, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, 2, 200, 200, 1));
        var alone = Doc(hidden);
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(alone, 2, kx, ky, 1).Kind);
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(alone, 2, 250, 225, 1).Kind);
    }

    [Fact] // select-hit-test: no selection, or a selection that is gone, is just the topmost visible box or none
    public void Without_a_live_selection_the_press_is_the_pick()
    {
        var doc = Doc(L(id: 1), L(id: 2, x: 250));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 2, default), SelectionGeometry.HitTest(doc, null, 275, 225, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 2, default), SelectionGeometry.HitTest(doc, 99, 275, 225, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, null, 210, 225, 1));
        Assert.Equal(new SelectHit(SelectHitKind.None, null, default), SelectionGeometry.HitTest(doc, null, 10, 10, 1));
        Assert.Throws<ArgumentNullException>(() => SelectionGeometry.HitTest(null!, null, 0, 0, 1));
    }

    [Theory] // atan2(dx, -dy): 0 is up, clockwise positive (editor.js:1076, :1176)
    [InlineData(0, -10, 0)]
    [InlineData(10, 0, 90)]
    [InlineData(0, 10, 180)]
    [InlineData(-10, 0, -90)]
    [InlineData(10, -10, 45)]
    public void The_pointer_angle_is_0_up_and_clockwise_positive(double dx, double dy, double degrees) =>
        Assert.Equal(degrees, SelectionGeometry.PointerAngle((100, 100), 100 + dx, 100 + dy), 9);

    [Fact] // editor.js:1076: the offset keeps the layer's rotation under the press point
    public void A_press_without_movement_keeps_the_rotation()
    {
        var layer = L(rotation: 20);
        var c = SelectionGeometry.Center(layer);
        double offset = SelectionGeometry.RotateStartOffset(layer, 320, 140);
        Assert.Equal(20, SelectionGeometry.RotateAngle(c, offset, 320, 140), 9);
    }

    [Theory] // select-rotate-drag parityCheck: the first snap angle strictly within 3 degrees, else unchanged
    [InlineData(44.2, 45)]
    [InlineData(47.9, 45)]
    [InlineData(48.1, 48.1)]
    [InlineData(42, 42)]
    [InlineData(-177.5, -180)]
    [InlineData(179, 180)]
    [InlineData(0.5, 0)]
    [InlineData(-91, -90)]
    public void The_snap_takes_the_first_angle_strictly_within_3_degrees(double degrees, double snapped) =>
        Assert.Equal(snapped, SelectionGeometry.SnapRotation(degrees));

    [Theory] // spec §5: rotation normalized to -180..180; the identity inside, so a slider value of 180 stays
    [InlineData(180, 180)]
    [InlineData(-180, -180)]
    [InlineData(37.5, 37.5)]
    [InlineData(181, -179)]
    [InlineData(-181, 179)]
    [InlineData(540, -180)]
    [InlineData(359.5, -0.5)]
    [InlineData(-450, -90)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void Rotation_is_normalized_to_minus_180_to_180(double degrees, double normalized) =>
        Assert.Equal(normalized, SelectionGeometry.NormalizeRotation(degrees), 9);

    [Theory] // select-rotate-drag: 4.1 stores 190 here (angle 20 + 170); v5 stays in range and keeps the snap
    [InlineData(0, 170)]
    [InlineData(10, 180)]
    [InlineData(12, -180)]
    [InlineData(20, -170)]
    [InlineData(100, -90)]
    public void A_drag_across_the_atan2_seam_stays_in_range(double pointerDegrees, double rotation)
    {
        var c = (X: 300.0, Y: 300.0);
        double rad = pointerDegrees * Math.PI / 180;
        double offset = 0 - 170; // pressed straight up on a layer rotated 170
        double got = SelectionGeometry.RotateAngle(c, offset, c.X + 50 * Math.Sin(rad), c.Y - 50 * Math.Cos(rad));
        Assert.Equal(rotation, got, 9);
        Assert.InRange(got, -180, 180);
    }

    [Theory] // select-resize-drag parityCheck: distance 100 to 150 gives 1.5x; one clamp; a zero start distance is no change
    [InlineData(1, 100, 150, 1.5)]
    [InlineData(0.5, 100, 200, 1)]
    [InlineData(0.02, 100, 10, 0.01)]
    [InlineData(4, 100, 200, 5)]
    [InlineData(1, 0, 50, 1)]
    [InlineData(1, 1e-10, 5, 1)]
    public void The_resize_scale_is_the_distance_ratio_under_the_clamp(double start, double startDistance, double distance, double scale) =>
        Assert.Equal(scale, SelectionGeometry.ResizeScale(start, startDistance, distance), 12);

    [Theory] // props-scale-clamp-inconsistency: one range, 1 to 500%
    [InlineData(0, 0.01)]
    [InlineData(-1, 0.01)]
    [InlineData(double.NaN, 0.01)]
    [InlineData(0.5, 0.5)]
    [InlineData(5, 5)]
    [InlineData(7, 5)]
    [InlineData(double.PositiveInfinity, 5)]
    public void The_scale_clamp_is_1_to_500_percent(double scale, double clamped) =>
        Assert.Equal(clamped, SelectionGeometry.ClampScale(scale));

    [Fact] // wheel-scale-layer parityCheck: 10 down then 10 up; 4.1 ends at 0.9794, v5 returns to the start
    public void Wheel_steps_are_symmetric_and_clamped()
    {
        double s = 1;
        for (int i = 0; i < 10; i++) s = SelectionGeometry.WheelScale(s, -1);
        for (int i = 0; i < 10; i++) s = SelectionGeometry.WheelScale(s, +1);
        Assert.Equal(1, s, 12);
        Assert.Equal(1.05, SelectionGeometry.WheelScale(1, 0), 12); // WPF Delta 0 counts as up, as 4.1's deltaY <= 0 grows
        Assert.Equal(5, SelectionGeometry.WheelScale(5, 1));
        Assert.Equal(0.01, SelectionGeometry.WheelScale(0.01, -1));
        Assert.Equal(5, SelectionGeometry.WheelScale(4.9, 1));
    }

    [Theory] // render-rotation-label parityCheck: toFixed(1) + U+00B0; no label at 0 (and -0)
    [InlineData(30, "30.0°")]
    [InlineData(17.25, "17.3°")]
    [InlineData(-17.25, "-17.3°")]
    [InlineData(-0.04, "-0.0°")]
    [InlineData(0.05, "0.1°")]
    [InlineData(1.45, "1.4°")]
    [InlineData(0.25, "0.3°")]
    [InlineData(-179.95, "-179.9°")]
    [InlineData(180, "180.0°")]
    [InlineData(-135, "-135.0°")]
    [InlineData(2.675, "2.7°")]
    [InlineData(-0.25, "-0.3°")]
    public void The_rotation_label_is_toFixed_1_with_a_degree_sign(double rotation, string label) =>
        Assert.Equal(label, SelectionGeometry.RotationLabel(rotation));

    [Fact] // the ledger's "toFixed vs F1 rounding on halves: verify": .NET's F1 rounds an exact tie to even, toFixed does not
    public void ToFixed1_is_javascript_not_dotnet_F1_on_exact_ties()
    {
        Assert.Equal("17.2", 17.25.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("17.3", SelectionGeometry.ToFixed1(17.25));
        Assert.Equal("0.0", SelectionGeometry.ToFixed1(1e-25));
        Assert.Equal("123456789.1", SelectionGeometry.ToFixed1(123456789.125)); // binary 123456789.125 exactly: a tie at 2 places, not at 1
        Assert.Equal(("NaN", "Infinity", "-Infinity"), (SelectionGeometry.ToFixed1(double.NaN), SelectionGeometry.ToFixed1(double.PositiveInfinity), SelectionGeometry.ToFixed1(double.NegativeInfinity)));
    }

    [Fact]
    public void There_is_no_label_at_zero()
    {
        Assert.Null(SelectionGeometry.RotationLabel(0));
        Assert.Null(SelectionGeometry.RotationLabel(-0.0));
        Assert.Equal(SKRectI.Empty, SelectionGeometry.LabelBounds(L(), new ViewOptions(1)));
    }

    [Fact] // ruling R4: the label box holds the anchor, rotates with the layer and scales with the device scale
    public void The_label_bounds_follow_the_anchor_rotation_and_device_scale()
    {
        var layer = L(rotation: 30); // center (250, 225); anchor (0, -25 - 46) in the rotated frame
        var box = SelectionGeometry.LabelBounds(layer, new ViewOptions(1));
        double rad = 30 * Math.PI / 180;
        double ax = 250 + 71 * Math.Sin(rad), ay = 225 - 71 * Math.Cos(rad);
        Assert.True(box.Left < ax && ax < box.Right && box.Top < ay && ay < box.Bottom, $"{box} must hold the anchor ({ax}, {ay})");
        var upsideDown = SelectionGeometry.LabelBounds(layer with { Rotation = 180 }, new ViewOptions(1));
        Assert.True(upsideDown.Top > 225, "at 180 degrees the label sits below the layer");
        var doubled = SelectionGeometry.LabelBounds(layer, new ViewOptions(1, 2));
        Assert.InRange(doubled.Left, 2 * box.Left - 1, 2 * box.Left + 1);
        Assert.InRange(doubled.Bottom, 2 * box.Bottom - 1, 2 * box.Bottom + 1);
        Assert.InRange(doubled.Width, 2 * box.Width - 2, 2 * box.Width + 2);
        var zoomed = SelectionGeometry.LabelBounds(layer, new ViewOptions(2)); // zoom moves the anchor, not the label's screen size
        Assert.InRange(zoomed.Width, box.Width - 1, box.Width + 1);
        Assert.InRange(zoomed.Height, box.Height - 1, box.Height + 1);
    }
}
