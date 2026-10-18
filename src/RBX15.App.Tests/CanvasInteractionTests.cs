using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas gesture state machine (editor.js:1056-1320) driven with template points, no desktop input:
/// select move, resize, rotate, hover, deselect and wheel; the draw tools with live preview and commit; the
/// eraser on the selection; one undo step per gesture (interfaces R2, R3, R5 to R12).
/// </summary>
public class CanvasInteractionTests
{
    /// <summary>A session over <paramref name="layers"/>, the shared tool and brush, a fake status strip and the interaction.</summary>
    sealed class Rig
    {
        public Rig(params Layer[] layers)
        {
            Session = SessionWith(layers);
            I = new CanvasInteraction(Session, Tools, Brush, Status);
        }

        public EditorSession Session { get; }
        public ToolsViewModel Tools { get; } = new();
        public BrushViewModel Brush { get; } = new(new FakeColorPicker());
        public FakeStatus Status { get; } = new();
        public CanvasInteraction I { get; }

        public Layer L(int id) => Session.Document.Layers.Find(l => l.Id == id)!;

        /// <summary>Press at the first point, move through the rest, release at the last.</summary>
        public void Drag(double zoom, params Point[] points)
        {
            I.PointerDown(points[0], zoom);
            foreach (var p in points.Skip(1)) I.PointerMove(p, zoom);
            I.PointerUp(points[^1], zoom);
        }
    }

    /// <summary>A 100x50 layer at (100, 100): center (150, 125), rotate handle (150, 70) at zoom 1.</summary>
    static Layer Box(int id = 1, bool visible = true) => LayerOf(id, 100, 50) with { X = 100, Y = 100, Visible = visible };

    /// <summary>The point at <paramref name="degrees"/> (0 = up, clockwise) and <paramref name="radius"/> from (cx, cy).</summary>
    static Point At(double cx, double cy, double degrees, double radius) =>
        new(cx + radius * Math.Sin(degrees * Math.PI / 180), cy - radius * Math.Cos(degrees * Math.PI / 180));

    static byte[] Pixels(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.Bytes;
    }

    static void AssertSameLayerImage(Layer expected, Layer actual)
    {
        Assert.Equal((expected.X, expected.Y, expected.Image.Width, expected.Image.Height), (actual.X, actual.Y, actual.Image.Width, actual.Image.Height));
        Assert.Equal(Pixels(expected.Image), Pixels(actual.Image));
    }

    // ---- Select: move, click, deselect ----

    [Fact] // select-move, select-move-hit-and-drag: x = start + (mx - dragStart.x) (editor.js:1169-1171); one step (R7)
    public void A_press_on_a_layer_selects_it_and_a_drag_moves_it_by_the_template_delta_as_one_step()
    {
        var rig = new Rig(Box());
        Assert.True(rig.I.PointerDown(new Point(120, 110), 2));
        Assert.Equal((CanvasGesture.Move, 1), (rig.I.Gesture, rig.Session.SelectedLayerId));
        rig.I.PointerMove(new Point(150, 95), 2);
        rig.I.PointerMove(new Point(170, 80), 2);
        Assert.Equal(0, rig.Session.UndoCount);   // adopted live, recorded at the release
        rig.I.PointerUp(new Point(170, 80), 2);

        Assert.Equal((150.0, 70.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.True(rig.Session.Undo());
        Assert.Equal((100.0, 100.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(1, rig.Session.SelectedLayerId);  // selection survives undo (spec §5)
    }

    [Fact] // select-hit-test: the press picks (editor.js:1100-1111); undo-empty-steps: a click records nothing
    public void A_click_without_movement_selects_and_records_nothing()
    {
        var rig = new Rig(Box());
        rig.Drag(1, new Point(120, 110));
        Assert.Equal(1, rig.Session.SelectedLayerId);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.False(rig.Session.InGesture);
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
    }

    [Fact] // select-deselect-click (editor.js:1114-1117): no gesture, no capture, never an undo step
    public void Empty_canvas_deselects_and_starts_nothing()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.False(rig.I.PointerDown(new Point(10, 500), 1));
        Assert.Null(rig.Session.SelectedLayerId);
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    // ---- Select: resize ----

    [Fact] // select-resize-handles, select-resize-drag: scale = start * d/d0, center kept (editor.js:1181-1190)
    public void A_corner_drag_scales_uniformly_about_the_center_as_one_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.True(rig.I.PointerDown(new Point(200, 100), 1));   // the top-right corner
        Assert.Equal(CanvasGesture.Resize, rig.I.Gesture);
        Assert.Equal(CanvasCursorKind.ResizeNwse, rig.I.Cursor.Kind);
        rig.I.PointerMove(new Point(225, 87.5), 1);              // 1.5 times the distance from (150, 125)
        var l = rig.L(1);
        Assert.Equal(1.5, l.Scale, 12);
        Assert.Equal((75.0, 87.5), (Math.Round(l.X, 9), Math.Round(l.Y, 9)));
        Assert.Equal(CanvasCursorKind.ResizeNwse, rig.I.Cursor.Kind); // held for the whole drag (canvas-cursor-states fix)
        rig.I.PointerUp(new Point(225, 87.5), 1);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // props-scale-clamp-inconsistency, select-resize-drag fix: one 1%-500% clamp (spec §5)
    public void Resize_is_clamped_to_1_and_500_percent()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.I.PointerDown(new Point(200, 100), 1);
        rig.I.PointerMove(new Point(650, -125), 1);              // 10 times the distance
        Assert.Equal(5, rig.L(1).Scale);
        rig.I.PointerMove(new Point(150.001, 125), 1);           // almost on the center
        Assert.Equal(0.01, rig.L(1).Scale);
        rig.I.PointerUp(new Point(150.001, 125), 1);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    // ---- Select: rotate ----

    [Fact] // select-rotate-handle-hit, select-rotate-drag, -2: snap to the first of -180..180 within < 3 (editor.js:1172-1180)
    public void The_rotate_handle_drag_snaps_within_3_degrees_and_is_one_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.True(rig.I.PointerDown(new Point(150, 70), 1));
        Assert.Equal((CanvasGesture.Rotate, CanvasCursorKind.Grabbing), (rig.I.Gesture, rig.I.Cursor.Kind));
        rig.I.PointerMove(At(150, 125, 44.2, 55), 1);
        Assert.Equal(45, rig.L(1).Rotation);
        rig.I.PointerMove(At(150, 125, 48.1, 55), 1);
        Assert.Equal(48.1, rig.L(1).Rotation, 9);
        rig.I.PointerMove(At(150, 125, 47.9, 55), 1);
        Assert.Equal(45, rig.L(1).Rotation);
        Assert.Equal(CanvasCursorKind.Grabbing, rig.I.Cursor.Kind);
        rig.I.PointerUp(At(150, 125, 47.9, 55), 1);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // select-rotate-drag fix: normalized to -180..180 (R5), so 4.1's -185.3 is 174.7 and -181.5 snaps to 180
    public void A_rotate_drag_across_the_bottom_stays_within_180_and_snaps_at_the_ends()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.True(rig.I.PointerDown(new Point(160, 70), 1));   // 10 px right of the handle, inside its 14 px
        double offset = Math.Atan2(10, 55) * 180 / Math.PI;      // editor.js:1073
        rig.I.PointerMove(At(150, 125, -175, 55), 1);
        Assert.Equal(360 + (-175 - offset), rig.L(1).Rotation, 9);
        rig.I.PointerMove(At(150, 125, -181.5 + offset, 55), 1);  // raw -181.5, normalized 178.5
        Assert.Equal(180, rig.L(1).Rotation);
        rig.I.PointerUp(At(150, 125, -181.5 + offset, 55), 1);
        Assert.InRange(rig.L(1).Rotation, -180, 180);
    }

    // ---- Select: cursors, hidden layers, capture, cancel ----

    [Fact] // select-hover-idle, canvas-cursor-states, cursor-default fixes: hover cursors; the handle is 30 screen px out
    public void Hover_shows_grab_resize_move_or_crosshair_and_follows_zoom()
    {
        var rig = new Rig(Box(), LayerOf(2, 40, 40) with { X = 400, Y = 400 });
        rig.Session.Select(1);
        CanvasCursorKind Hover(double x, double y, double zoom = 1)
        {
            rig.I.PointerMove(new Point(x, y), zoom);
            return rig.I.Cursor.Kind;
        }
        Assert.Equal(CanvasCursorKind.Grab, Hover(150, 70));
        Assert.Equal(CanvasCursorKind.ResizeNwse, Hover(200, 100));
        Assert.Equal(CanvasCursorKind.Move, Hover(150, 125));
        Assert.Equal(CanvasCursorKind.Move, Hover(420, 420));     // any visible layer box, not only the selection
        Assert.Equal(CanvasCursorKind.Crosshair, Hover(10, 10));
        Assert.Equal(CanvasCursorKind.Grab, Hover(150, 85, 2));   // 125 - 25 - 30/2
        Assert.Equal(CanvasCursorKind.Crosshair, Hover(150, 70, 2));

        rig.I.PointerMove(new Point(150, 125), 1);
        rig.I.PointerLeave();
        Assert.Equal(CanvasCursor.Crosshair, rig.I.Cursor);
        rig.Tools.Set(Tool.Line);
        Assert.Equal(CanvasCursorKind.Crosshair, Hover(150, 125));
    }

    [Fact] // select-hidden-layer-still-handled fix (R2): no handles on the canvas; the wheel still acts on it
    public void A_hidden_selected_layer_has_no_canvas_handles_but_the_wheel_still_scales_it()
    {
        var rig = new Rig(Box(visible: false));
        rig.Session.Select(1);
        Assert.False(rig.I.PointerDown(new Point(150, 70), 1));   // where its rotate handle would be
        Assert.Null(rig.Session.SelectedLayerId);                 // nothing visible there: deselect
        rig.Session.Select(1);
        Assert.False(rig.I.PointerDown(new Point(200, 100), 1));  // its corner
        Assert.Equal(0, rig.Session.UndoCount);

        rig.Session.Select(1);
        rig.I.Wheel(+1);
        Assert.Equal(1.05, rig.L(1).Scale, 12);
    }

    [Fact] // R10, select-hover-and-end fix: a lost capture ends the drag where it was, as one step
    public void Capture_loss_mid_drag_keeps_the_move_as_one_step()
    {
        var rig = new Rig(Box());
        rig.I.PointerDown(new Point(120, 110), 1);
        rig.I.PointerMove(new Point(150, 130), 1);
        rig.I.CaptureLost();
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Equal((130.0, 120.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(1, rig.Session.UndoCount);
        rig.I.PointerMove(new Point(300, 300), 1);              // hover only now
        Assert.Equal((130.0, 120.0), (rig.L(1).X, rig.L(1).Y));
        rig.I.CaptureLost();                                    // idle: nothing
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // R9: a tool change mid select drag ends it as a release, keeping its change as one step
    public void A_tool_change_mid_select_drag_keeps_the_change_as_one_step()
    {
        var rig = new Rig(Box());
        rig.I.PointerDown(new Point(120, 110), 1);
        rig.I.PointerMove(new Point(130, 110), 1);
        rig.Tools.Set(Tool.Pen);
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.False(rig.Session.InGesture);
        Assert.Equal((110.0, 100.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // final fix wave 7: a keyboard edit mid drag is its own step; drag part 1, the edit and drag part 2 are three
    public void A_panel_slider_keyboard_change_mid_move_splits_the_drag_into_three_steps()
    {
        var rig = new Rig(Box());
        var props = new LayerPropertiesViewModel(rig.Session);
        rig.I.PointerDown(new Point(120, 110), 1);
        rig.I.PointerMove(new Point(130, 110), 1);
        props.Rotation = 30;                                    // a keyboard step on the Rotation slider, outside any slider press
        Assert.Equal(CanvasGesture.Move, rig.I.Gesture);        // the drag goes on
        rig.I.PointerMove(new Point(150, 130), 1);              // still applied from the press state
        rig.I.PointerUp(new Point(150, 130), 1);

        Assert.Equal((130.0, 120.0, 30.0), (rig.L(1).X, rig.L(1).Y, rig.L(1).Rotation));
        Assert.Equal((3, false), (rig.Session.UndoCount, rig.Session.InGesture));
        Assert.True(rig.Session.Undo());
        Assert.Equal((110.0, 100.0, 30.0), (rig.L(1).X, rig.L(1).Y, rig.L(1).Rotation));
        Assert.True(rig.Session.Undo());
        Assert.Equal((110.0, 100.0, 0.0), (rig.L(1).X, rig.L(1).Y, rig.L(1).Rotation));
        Assert.True(rig.Session.Undo());
        Assert.Equal((100.0, 100.0, 0.0), (rig.L(1).X, rig.L(1).Y, rig.L(1).Rotation));
    }

    [Fact] // final fix wave 10: Ctrl+Z after the drag moved records part 1 and undoes it; the rest of the drag is one step
    public void Undo_mid_move_after_movement_leaves_one_step()
    {
        var rig = new Rig(Box());
        rig.I.PointerDown(new Point(120, 110), 1);
        rig.I.PointerMove(new Point(130, 110), 1);
        Assert.True(rig.Session.Undo());
        Assert.Equal((100.0, 100.0), (rig.L(1).X, rig.L(1).Y));
        rig.I.PointerMove(new Point(150, 130), 1);
        rig.I.PointerUp(new Point(150, 130), 1);

        Assert.Equal((130.0, 120.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal((1, 0, false), (rig.Session.UndoCount, rig.Session.RedoCount, rig.Session.InGesture));
        Assert.True(rig.Session.Undo());
        Assert.Equal((100.0, 100.0), (rig.L(1).X, rig.L(1).Y));
    }

    [Fact] // final fix wave 10: Ctrl+Z before the drag moved undoes the prior step (here the layer's add); the drag then does nothing
    public void Undo_mid_move_before_movement_undoes_the_prior_step_and_the_drag_records_nothing()
    {
        var rig = new Rig();
        rig.Session.ApplyWithSelection(s => LayerOps.AddImage(s, LayerOf(1, 100, 50).Image, rig.Session.TakeId(), "art"));
        var (cx, cy) = SelectionGeometry.Center(rig.Session.SelectedLayer!);
        Assert.True(rig.I.PointerDown(new Point(cx, cy), 1));
        Assert.True(rig.Session.Undo());
        Assert.Empty(rig.Session.Document.Layers);
        rig.I.PointerMove(new Point(cx + 30, cy + 20), 1);
        rig.I.PointerUp(new Point(cx + 30, cy + 20), 1);

        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal((0, 1, false), (rig.Session.UndoCount, rig.Session.RedoCount, rig.Session.InGesture));
    }

    [Fact] // final fix wave 10: Ctrl+Z mid erase that removes the pressed layer; the release records nothing
    public void Undo_mid_erase_that_removes_the_pressed_layer_makes_the_release_record_nothing()
    {
        var rig = new Rig();
        rig.Session.ApplyWithSelection(s => LayerOps.AddImage(s, LayerOf(1, 100, 50).Image, rig.Session.TakeId(), "art"));
        var (cx, cy) = SelectionGeometry.Center(rig.Session.SelectedLayer!);
        rig.Tools.Set(Tool.Eraser);
        Assert.True(rig.I.PointerDown(new Point(cx - 20, cy), 1));
        rig.I.PointerMove(new Point(cx + 20, cy), 1);
        Assert.True(rig.Session.Undo());
        Assert.Empty(rig.Session.Document.Layers);
        rig.I.PointerUp(new Point(cx + 20, cy), 1);

        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal((0, 1), (rig.Session.UndoCount, rig.Session.RedoCount));
    }

    // ---- Wheel ----

    [Fact] // wheel-scale-layer, wheel-sign-only-and-midstroke fixes: centered, x1.05 / /1.05, a burst is one step (R6, R7)
    public void Wheel_scales_the_selection_about_its_center_symmetrically_and_a_burst_is_one_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        for (int i = 0; i < 10; i++) rig.I.Wheel(-1);
        Assert.Equal(1 / Math.Pow(1.05, 10), rig.L(1).Scale, 12);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.True(rig.Session.InGesture);
        rig.I.EndWheelBurst();
        Assert.Equal(1, rig.Session.UndoCount);

        for (int i = 0; i < 10; i++) rig.I.Wheel(+1);
        rig.I.EndWheelBurst();
        Assert.Equal(2, rig.Session.UndoCount);
        var l = rig.L(1);
        Assert.Equal(1, l.Scale, 12);                            // 4.1: 0.95^10 * 1.05^10 = 0.9794
        var (cx, cy) = SelectionGeometry.Center(l);
        Assert.Equal((150.0, 125.0), (Math.Round(cx, 9), Math.Round(cy, 9)));
    }

    [Fact] // undo-empty-steps: no selection does nothing; a wheel against the 500% clamp records nothing
    public void Wheel_with_no_selection_or_at_the_clamp_records_nothing()
    {
        var rig = new Rig(Box() with { Scale = 5 });
        rig.I.Wheel(+1);
        Assert.False(rig.Session.InGesture);
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        rig.I.EndWheelBurst();
        Assert.Equal(5, rig.L(1).Scale);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // this task's ruling 2 (controller ruling 3): in Select the wheel does nothing while a select drag runs
    public void The_wheel_does_nothing_during_a_select_drag()
    {
        var rig = new Rig(Box());
        rig.I.PointerDown(new Point(120, 110), 1);               // a move drag
        rig.I.PointerMove(new Point(130, 110), 1);
        rig.I.Wheel(+1);
        Assert.Equal(1.0, rig.L(1).Scale);
        rig.I.PointerUp(new Point(130, 110), 1);
        Assert.Equal((110.0, 100.0, 1.0), (rig.L(1).X, rig.L(1).Y, rig.L(1).Scale));
        Assert.Equal((1, false), (rig.Session.UndoCount, rig.Session.InGesture));   // the move's step, no wheel burst

        Assert.True(rig.I.PointerDown(new Point(210, 100), 1));  // the top-right corner: a resize drag
        Assert.Equal(CanvasGesture.Resize, rig.I.Gesture);
        rig.I.Wheel(-1);
        Assert.Equal(1.0, rig.L(1).Scale);
        rig.I.PointerUp(new Point(210, 100), 1);
        Assert.Equal((1, false), (rig.Session.UndoCount, rig.Session.InGesture));
    }

    [Fact] // R7: a press, a selection change or a tool change closes the wheel burst
    public void A_press_a_selection_change_or_a_tool_change_closes_the_wheel_burst()
    {
        var rig = new Rig(Box(), LayerOf(2, 40, 40) with { X = 400, Y = 400 });
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        rig.I.PointerDown(new Point(10, 500), 1);               // deselects, and closes the burst first
        Assert.Equal((1, false), (rig.Session.UndoCount, rig.Session.InGesture));

        rig.Session.Select(1);
        rig.I.Wheel(+1);
        rig.Session.Select(2);
        Assert.Equal((2, false), (rig.Session.UndoCount, rig.Session.InGesture));

        rig.I.Wheel(+1);
        rig.Tools.Set(Tool.Pen);
        Assert.Equal((3, false), (rig.Session.UndoCount, rig.Session.InGesture));
    }

    [Fact] // R7: an edit elsewhere during a burst's quiet period closes the burst first: two steps, undone one at a time
    public void An_edit_during_a_wheel_burst_is_its_own_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        Assert.True(rig.Session.Apply(d => LayerOps.SetOpacity(d, 1, 0.5)));
        rig.I.EndWheelBurst();
        Assert.Equal(2, rig.Session.UndoCount);
        Assert.True(rig.Session.Undo());
        Assert.Equal((1.05, 1.0), (Math.Round(rig.L(1).Scale, 12), rig.L(1).Opacity));
    }

    [Fact] // R7: a slider gesture opened during a burst records separately (no nesting into the wheel's scope)
    public void A_gesture_opened_during_a_wheel_burst_records_separately()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        using (rig.Session.BeginGesture()) rig.Session.Apply(d => LayerOps.SetOpacity(d, 1, 0.3));
        Assert.Equal((2, false), (rig.Session.UndoCount, rig.Session.InGesture));
    }

    // ---- Draw ----

    [Fact] // draw-commit-layer, layers-add-drawing, R12: the preview is the commit; one step; selected; the tool stays (R8)
    public void A_pen_drag_previews_the_whole_path_and_commits_one_selected_layer_named_Pen_drawing()
    {
        var rig = new Rig();
        rig.Brush.Color = "#ff0000";
        rig.Brush.Size = 6;
        rig.Tools.Set(Tool.Pen);
        var points = new[] { new Point(100, 100), new Point(140, 100), new Point(140, 140) };
        rig.I.PointerDown(points[0], 1);
        rig.I.PointerMove(points[1], 1);
        rig.I.PointerMove(points[2], 1);
        var preview = Assert.IsType<LivePreview>(rig.I.Preview);
        Assert.False(preview.IsEraser);
        Assert.Equal(1f, preview.Alpha);
        Assert.Equal(3, preview.Stroke.Points.Count);
        Assert.Equal(0, rig.Session.UndoCount);                  // nothing at the press (draw-tool-start fix)
        rig.I.PointerUp(points[2], 1);

        var stroke = new Stroke(ShapeTool.Pen, points.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToArray(), new SKColor(255, 0, 0), 6, false);
        var expected = Tools.CommitDrawing(stroke, 1, "Pen drawing")!;
        var layer = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal((1, "Pen drawing", 1.0), (layer.Id, layer.Name, layer.Scale));
        AssertSameLayerImage(expected, layer);
        Assert.Equal(1, rig.Session.SelectedLayerId);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(Tool.Pen, rig.Tools.Current);               // editor.js:528-540: no setTool
        Assert.Null(rig.I.Preview);
    }

    [Fact] // draw-pen-single-click fix (approved): a press with no movement draws a dot the size of the brush
    public void A_pen_press_without_movement_commits_a_brush_size_dot()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Pen);
        rig.Drag(1, new Point(200, 200));
        var stroke = new Stroke(ShapeTool.Pen, [new SKPoint(200, 200)], SKColor.Parse(BrushViewModel.DefaultColor), Tools.DefaultBrushSize, false);
        AssertSameLayerImage(Tools.CommitDrawing(stroke, 1, "Pen drawing")!, Assert.Single(rig.Session.Document.Layers));
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Theory] // draw-empty-no-layer, draw-tool-start fix: shapes need two points (editor.js:1205-1234); no empty step
    [InlineData(Tool.Line)]
    [InlineData(Tool.Rect)]
    [InlineData(Tool.Ellipse)]
    public void A_shape_click_draws_nothing_and_records_nothing(Tool tool)
    {
        var rig = new Rig();
        rig.Tools.Set(tool);
        var before = rig.Session.Document;
        rig.Drag(1, new Point(200, 200));
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.Equal(1, rig.Session.NextId);
    }

    [Fact] // R11 (draw-brush-controls quirk): shapes re-read color, size and fill live; the pen keeps its press style
    public void Shapes_use_the_press_and_the_latest_point_and_reread_the_brush()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Rect);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        var s = rig.I.Preview!.Stroke;
        Assert.Equal(new[] { new SKPoint(100, 100), new SKPoint(150, 150) }, s.Points);
        Assert.Equal((SKColor.Parse("#17d4fa"), 4f, false), (s.Color, s.Width, s.Fill));

        rig.Brush.Fill = true;                                   // the preview follows at once
        Assert.True(rig.I.Preview!.Stroke.Fill);
        rig.Brush.Color = "#00ff00";
        rig.I.PointerMove(new Point(200, 180), 1);
        s = rig.I.Preview!.Stroke;
        Assert.Equal(new[] { new SKPoint(100, 100), new SKPoint(200, 180) }, s.Points);
        Assert.Equal((SKColor.Parse("#00ff00"), true), (s.Color, s.Fill));
        rig.I.Cancel();

        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(10, 10), 1);
        rig.Brush.Color = "#0000ff";
        rig.I.PointerMove(new Point(20, 20), 1);
        Assert.Equal(SKColor.Parse("#00ff00"), rig.I.Preview!.Stroke.Color);   // fixed at the press (editor.js:1138)
    }

    [Fact] // wheel-sign-only-and-midstroke: the wheel mid-shape changes the live width and the committed shape
    public void The_wheel_mid_shape_changes_the_live_width_and_the_committed_shape_but_not_a_pen_stroke()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Rect);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(200, 180), 1);
        rig.I.Wheel(+1);
        rig.I.Wheel(+1);
        Assert.Equal((6, 6f), (rig.Brush.Size, rig.I.Preview!.Stroke.Width));
        rig.I.PointerUp(new Point(200, 180), 1);
        var stroke = new Stroke(ShapeTool.Rect, [new SKPoint(100, 100), new SKPoint(200, 180)], SKColor.Parse("#17d4fa"), 6, false);
        AssertSameLayerImage(Tools.CommitDrawing(stroke, 1, "Rect drawing")!, rig.L(1));
        Assert.Equal("Rect drawing", rig.L(1).Name);

        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(300, 300), 1);
        rig.I.Wheel(-1);
        rig.I.PointerMove(new Point(320, 300), 1);
        Assert.Equal((5, 6f), (rig.Brush.Size, rig.I.Preview!.Stroke.Width)); // pen width fixed at the press (editor.js:1139)
    }

    [Fact] // draw-tool-switch-midstroke fix (R9): the stroke is discarded, no layer, no step, the preview cleared
    public void A_tool_switch_mid_stroke_discards_it()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        rig.Tools.Set(Tool.Line);
        Assert.Equal((CanvasGesture.None, (LivePreview?)null), (rig.I.Gesture, rig.I.Preview));
        rig.I.PointerUp(new Point(150, 150), 1);                 // the release that follows does nothing
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // M4a R11: an Open mid stroke discards it, as a tool switch does; the release that follows adds nothing to the opened file
    public void An_open_mid_stroke_discards_it()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        rig.Session.Open(new ProjectLoad(new Document(), 1, []), "x.r15proj");
        Assert.Equal((CanvasGesture.None, (LivePreview?)null), (rig.I.Gesture, rig.I.Preview));
        rig.I.PointerUp(new Point(150, 150), 1);
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal((0, false), (rig.Session.UndoCount, rig.Session.IsDirty));
    }

    [Fact] // draw-release-outside-canvas fix (R10): a lost capture commits the stroke at the last point
    public void Capture_loss_mid_stroke_commits_it_at_the_last_point()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Line);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        rig.I.CaptureLost();
        var stroke = new Stroke(ShapeTool.Line, [new SKPoint(100, 100), new SKPoint(150, 150)], SKColor.Parse("#17d4fa"), 4, false);
        AssertSameLayerImage(Tools.CommitDrawing(stroke, 1, "Line drawing")!, rig.L(1));
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // canvas-coord-map: points are unclamped; a stroke released past the edge keeps its on-template part
    public void A_stroke_released_outside_the_template_keeps_the_part_inside()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Line);
        rig.I.PointerDown(new Point(500, 100), 1);
        rig.I.PointerMove(new Point(700, 100), 1);
        Assert.Equal(new SKPoint(700, 100), rig.I.Preview!.Stroke.Points[^1]);
        rig.I.PointerUp(new Point(700, 100), 1);
        var l = rig.L(1);
        Assert.True(l.X >= 0 && l.X + l.Image.Width <= TemplateSize.Width);
    }

    // ---- Erase ----

    [Fact] // eraser-selection-semantics fix (spec §5): refuse at the press with 4.1's text (editor.js:1270)
    public void The_eraser_with_no_selection_refuses_at_the_press_with_a_status_message()
    {
        var rig = new Rig(Box());
        rig.Tools.Set(Tool.Eraser);
        Assert.False(rig.I.PointerDown(new Point(120, 110), 1));
        Assert.Equal("Select a layer to erase from, then try again.", rig.Status.Current);
        Assert.Equal((CanvasGesture.None, (LivePreview?)null), (rig.I.Gesture, rig.I.Preview));
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // eraser-apply (Eraser.ApplyTo, never Replay41), render-live-preview (half alpha): the selection, not the top layer
    public void An_eraser_drag_erases_the_selected_layer_under_another_as_one_step()
    {
        var top = LayerOf(2, 100, 50) with { X = 100, Y = 100 };
        var rig = new Rig(Box(), top);
        rig.Session.Select(1);
        rig.Brush.Size = 20;
        rig.Tools.Set(Tool.Eraser);
        var before = rig.Session.Document;
        rig.I.PointerDown(new Point(120, 125), 1);
        rig.I.PointerMove(new Point(180, 125), 1);
        var preview = rig.I.Preview!;
        Assert.Equal((true, 0.5f, 20f, Eraser.StrokeColor), (preview.IsEraser, preview.Alpha, preview.Stroke.Width, preview.Stroke.Color));
        rig.I.PointerUp(new Point(180, 125), 1);

        var expected = Eraser.ApplyTo(before, 1, [new SKPoint(120, 125), new SKPoint(180, 125)], 20);
        AssertSameLayerImage(expected.Layers[0], rig.L(1));
        Assert.NotSame(before.Layers[0].Image, rig.L(1).Image);
        Assert.Same(top.Image, rig.L(2).Image);
        Assert.Equal((1, 1), (rig.Session.UndoCount, rig.Session.SelectedLayerId));
        Assert.Equal(Tool.Eraser, rig.Tools.Current);
        rig.Session.Undo();
        Assert.Same(before.Layers[0].Image, rig.L(1).Image);
    }

    [Fact] // eraser-apply: "Erase applies to the selected layer even if hidden" (R2 keeps it)
    public void The_eraser_erases_a_hidden_selected_layer()
    {
        var rig = new Rig(Box(visible: false));
        rig.Session.Select(1);
        rig.Brush.Size = 20;
        rig.Tools.Set(Tool.Eraser);
        var before = rig.L(1).Image;
        rig.Drag(1, new Point(120, 125), new Point(180, 125));
        Assert.NotSame(before, rig.L(1).Image);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // R3 (M2a carry-forward, 4.1 kept): an eraser click erases nothing and records nothing
    public void Eraser_click_without_move_changes_nothing_and_records_no_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.Tools.Set(Tool.Eraser);
        var before = rig.Session.Document;
        Assert.True(rig.I.PointerDown(new Point(120, 125), 1));
        rig.I.PointerUp(new Point(120, 125), 1);
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // cursor-eraser, eraser-cursor, wheel-brush-size: the ring is the brush size; the wheel steps it by 1 in 1..50
    public void The_eraser_ring_follows_the_brush_and_the_wheel_but_a_stroke_keeps_its_press_size()
    {
        var rig = new Rig(Box());
        rig.Tools.Set(Tool.Eraser);
        Assert.Equal(CanvasCursor.Ring(4), rig.I.Cursor);
        for (int i = 0; i < 20; i++) rig.I.Wheel(+1);
        Assert.Equal((24, CanvasCursor.Ring(24)), (rig.Brush.Size, rig.I.Cursor));   // wheel-brush-size parityCheck
        for (int i = 0; i < 40; i++) rig.I.Wheel(+1);
        Assert.Equal(50, rig.Brush.Size);
        for (int i = 0; i < 60; i++) rig.I.Wheel(-1);
        Assert.Equal((1, CanvasCursor.Ring(1)), (rig.Brush.Size, rig.I.Cursor));

        rig.Session.Select(1);
        rig.I.PointerDown(new Point(120, 125), 1);
        rig.I.Wheel(+1);
        rig.I.PointerMove(new Point(130, 125), 1);
        Assert.Equal((2, 1f), (rig.Brush.Size, rig.I.Preview!.Stroke.Width));        // editor.js:1139: fixed at the press
        Assert.Equal(CanvasCursor.Ring(2), rig.I.Cursor);
        Assert.Equal(0, rig.Session.UndoCount);                                      // brush changes are never steps
    }

    // ---- Guards ----

    [Fact] // tools-set-tool: setTool refreshes the cursor (editor.js:477)
    public void A_tool_change_sets_its_default_cursor()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Eraser);
        Assert.Equal(CanvasCursorKind.EraserRing, rig.I.Cursor.Kind);
        rig.Tools.Set(Tool.Ellipse);
        Assert.Equal(CanvasCursor.Crosshair, rig.I.Cursor);
    }

    [Fact]
    public void Bad_arguments_are_refused()
    {
        var session = new EditorSession();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(new FakeColorPicker());
        var status = new FakeStatus();
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(null!, tools, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(session, null!, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(session, tools, null!, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(session, tools, brush, null!));
        var i = new CanvasInteraction(session, tools, brush, status);
        Assert.Throws<ArgumentOutOfRangeException>(() => i.PointerDown(new Point(1, 1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => i.PointerMove(new Point(1, 1), double.NaN));
    }
}
