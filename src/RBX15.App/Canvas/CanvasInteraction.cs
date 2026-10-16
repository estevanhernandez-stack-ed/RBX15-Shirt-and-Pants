using System.ComponentModel;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Canvas;

/// <summary>What the canvas is doing between a press and its release.</summary>
public enum CanvasGesture { None, Move, Resize, Rotate, Draw, Erase }

/// <summary>
/// The canvas's input state machine: 4.1's unified mouse handler (editor.js:1056-1320) over the
/// session, with v5's fixes. Points are template px (CanvasCoordinates.ToTemplate), so it is tested
/// with plain Points and no desktop input; TemplateCanvasHost only forwards to it.
/// <list type="bullet">
/// <item><b>Select</b> (editor.js:1064-1118, :1154-1194, :1239-1246): the press hit-tests through
///   SelectionGeometry.HitTest (rotate handle, corners TL TR BR BL, then the topmost visible box, else
///   deselect). A drag is one EditorSession gesture, opened at the press, closed at the release, the
///   capture loss or a cancel: one undo step iff the content changed (R7; select-move, select-resize-drag,
///   select-rotate-drag fixes). Each move applies from the press state, as 4.1 does.</item>
/// <item><b>Draw</b> (Pen, Line, Rect, Ellipse; editor.js:1120-1146, :1196-1236, :1249-1293): no undo at the
///   press (draw-tool-start fix). The preview is the stroke the commit draws (R12). Pen color and width are
///   fixed at the press; shapes re-read color, size and fill on every move and wheel step (R11). The release
///   commits Tools.CommitDrawing as a new selected top layer in one step, or nothing when nothing was drawn
///   (draw-empty-no-layer). The drawing tool stays (R8, editor.js:528-540).</item>
/// <item><b>Erase</b> (editor.js:1265-1283): only with a selection, else the press is refused with a status
///   message (eraser-selection-semantics fix). The release erases the layer selected at the press through
///   Eraser.ApplyTo, one step, none when nothing was erased (R3).</item>
/// <item><b>Wheel</b> (editor.js:1305-1320): sign only. Select scales the selection about its center, and a
///   burst of wheel events is one step (R6, R7). Other tools step the brush size (wheel-brush-size).</item>
/// <item><b>Cursors</b>: hover shows rotate (Grab), resize (ResizeNwse) and move (Move) over handles and
///   layers (select-hover-idle fix); a drag holds its cursor (canvas-cursor-states fix); the eraser shows
///   its ring at the brush size (cursor-eraser).</item>
/// </list>
/// UI thread only.
/// </summary>
public sealed class CanvasInteraction
{
    /// <summary>editor.js:1270's alert text, shown on the status strip at the press instead (spec §5).</summary>
    public const string NoLayerToErase = "Select a layer to erase from, then try again.";

    readonly EditorSession _session;
    readonly ToolsViewModel _tools;
    readonly BrushViewModel _brush;
    readonly IStatus _status;
    readonly List<SKPoint> _points = [];
    IDisposable? _drag;
    IDisposable? _wheel;
    bool _ownChange;   // true while Wheel itself changes the session, so its own Changing does not close its burst
    int _layerId;
    Point _press;
    Point _last;
    double _zoom = 1;
    (double X, double Y) _layerStart;
    (double X, double Y) _center;
    double _startOffset;
    double _startScale;
    double _startDistance;
    ShapeTool _shape;
    SKColor _penColor;
    float _penWidth;
    float _eraseSize;

    public CanvasInteraction(EditorSession session, ToolsViewModel tools, BrushViewModel brush, IStatus status)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _brush = brush ?? throw new ArgumentNullException(nameof(brush));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        Cursor = DefaultCursor();
        _tools.CurrentChanged += (_, _) => OnToolChanged();
        _brush.PropertyChanged += OnBrushChanged;
        _session.Changed += (_, e) =>
        {
            if (e.What.HasFlag(SessionChange.Selection)) EndWheelBurst();
        };
        // R7 (critic MJ1): any other change entry point (a panel field or slider, Flip, Reset, a region paint, an
        // import, Undo, Redo) closes the burst before it changes anything, so it is never folded into the wheel's step.
        _session.Changing += (_, _) =>
        {
            if (!_ownChange) EndWheelBurst();
        };
    }

    public CanvasGesture Gesture { get; private set; }

    public bool IsActive => Gesture != CanvasGesture.None;

    /// <summary>The live preview: non-null during Draw and Erase (editor.js:324-330).</summary>
    public LivePreview? Preview { get; private set; }

    public CanvasCursor Cursor { get; private set; }

    /// <summary>Raised when Gesture, Preview or Cursor changed.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// A left press at <paramref name="template"/>. True = a gesture started (the host captures the mouse).
    /// Closes any wheel burst. Select: a handle or a layer starts a drag (a layer is picked and selected
    /// first); empty canvas deselects and returns false. Draw tools start a stroke. The eraser with no
    /// selection shows <see cref="NoLayerToErase"/> and returns false.
    /// </summary>
    public bool PointerDown(Point template, double zoom)
    {
        CheckZoom(zoom);
        EndWheelBurst();
        if (IsActive) return false;   // a second press while a gesture runs cannot happen under capture
        _zoom = zoom;
        _press = _last = template;
        switch (_tools.Current)
        {
            case Tool.Select:
                return PressSelect(template, zoom);
            case Tool.Eraser:
                if (_session.SelectedLayerId is not int id)
                {
                    _status.Show(NoLayerToErase);
                    return false;
                }
                _layerId = id;
                _eraseSize = _brush.Size;   // fixed at the press (editor.js:1139)
                _points.Clear();
                _points.Add(ToSk(template));
                Update(CanvasGesture.Erase, LivePreview.Erase(_points.ToArray(), _eraseSize), CanvasCursor.Ring(_brush.Size));
                return true;
            default:
                _shape = ToolShapes.ShapeOf(_tools.Current)!.Value;
                _penColor = _brush.SkColor;   // the pen's style is fixed at the press (editor.js:1138-1141)
                _penWidth = _brush.Size;
                _points.Clear();
                _points.Add(ToSk(template));
                Update(CanvasGesture.Draw, LivePreview.Draw(CurrentStroke()), CanvasCursor.Crosshair);
                return true;
        }
    }

    bool PressSelect(Point t, double zoom)
    {
        var hit = SelectionGeometry.HitTest(_session.Document, _session.SelectedLayerId, t.X, t.Y, zoom);
        if (hit.Kind == SelectHitKind.None || hit.LayerId is not int id)
        {
            _session.Select(null);   // editor.js:1114-1117
            Update(CanvasGesture.None, null, HoverCursor(t, zoom));
            return false;
        }
        if (hit.Kind == SelectHitKind.Move)
        {
            _session.Select(id);     // selectLayer (editor.js:1105, :575-580) ...
            _tools.Set(Tool.Select); // ... which sets Select (already current here)
        }
        var layer = _session.Document.Layers.Find(l => l.Id == id)!;
        _layerId = id;
        _center = SelectionGeometry.Center(layer);
        CanvasGesture gesture;
        CanvasCursorKind cursor;
        switch (hit.Kind)
        {
            case SelectHitKind.Rotate:
                _startOffset = SelectionGeometry.RotateStartOffset(layer, t.X, t.Y);   // editor.js:1073
                gesture = CanvasGesture.Rotate;
                cursor = CanvasCursorKind.Grabbing;
                break;
            case SelectHitKind.Resize:
                _startScale = layer.Scale;                                        // editor.js:1092-1093
                _startDistance = Distance(t, _center);
                gesture = CanvasGesture.Resize;
                cursor = CanvasCursorKind.ResizeNwse;
                break;
            default:
                _layerStart = (layer.X, layer.Y);                                 // editor.js:1108-1109
                gesture = CanvasGesture.Move;
                cursor = CanvasCursorKind.Move;
                break;
        }
        _drag = _session.BeginGesture();
        Update(gesture, null, CanvasCursor.Of(cursor));
        return true;
    }

    /// <summary>
    /// A move. During a drag it applies from the press state (move, resize, rotate through session.Apply inside the
    /// gesture) or extends the stroke; a move to the last point does nothing. Idle, it only sets the hover cursor.
    /// </summary>
    public void PointerMove(Point template, double zoom)
    {
        CheckZoom(zoom);
        _zoom = zoom;
        if (!IsActive)
        {
            Update(CanvasGesture.None, null, HoverCursor(template, zoom));
            return;
        }
        if (template == _last) return;
        _last = template;
        switch (Gesture)
        {
            case CanvasGesture.Move:   // editor.js:1169-1171
                double x = _layerStart.X + (template.X - _press.X), y = _layerStart.Y + (template.Y - _press.Y);
                _session.Apply(d => LayerOps.MoveTo(d, _layerId, x, y));
                break;
            case CanvasGesture.Rotate: // editor.js:1172-1180, normalized (R5)
                double angle = SelectionGeometry.RotateAngle(_center, _startOffset, template.X, template.Y);
                _session.Apply(d => LayerOps.SetRotation(d, _layerId, angle));
                break;
            case CanvasGesture.Resize: // editor.js:1181-1190, clamped 1..500% (R6)
                double scale = SelectionGeometry.ResizeScale(_startScale, _startDistance, Distance(template, _center));
                _session.Apply(d => LayerOps.ScaleAboutCenter(d, _layerId, scale));
                break;
            case CanvasGesture.Draw:   // pen: every point; shapes: the press and the latest point (editor.js:1200-1234)
                if (_shape != ShapeTool.Pen && _points.Count > 1) _points[^1] = ToSk(template);
                else _points.Add(ToSk(template));
                Update(Gesture, LivePreview.Draw(CurrentStroke()), Cursor);
                break;
            case CanvasGesture.Erase:  // editor.js:1200-1203
                _points.Add(ToSk(template));
                Update(Gesture, LivePreview.Erase(_points.ToArray(), _eraseSize), Cursor);
                break;
        }
    }

    /// <summary>
    /// The release. The point is applied as a move first when it differs from the last one; then select drags close
    /// their gesture (one step iff changed), Draw commits its layer in one ApplyWithSelection, and Erase applies
    /// Eraser.ApplyTo in one Apply. Idle: nothing.
    /// </summary>
    public void PointerUp(Point template, double zoom)
    {
        CheckZoom(zoom);
        if (!IsActive) return;
        if (template != _last) PointerMove(template, zoom);
        Finish();
    }

    /// <summary>The pointer left the canvas. Idle only: the cursor goes back to the tool's default; a drag continues under capture.</summary>
    public void PointerLeave()
    {
        if (!IsActive) Update(CanvasGesture.None, null, DefaultCursor());
    }

    /// <summary>Mouse capture was lost mid-gesture: end it as a release at the last point (R10; draw-release-outside-canvas).</summary>
    public void CaptureLost()
    {
        if (IsActive) Finish();
    }

    /// <summary>
    /// R9 (draw-tool-switch-midstroke): a draw or erase stroke is discarded (no layer, no step, the preview cleared);
    /// a select drag ends as if released, keeping its change as one step.
    /// </summary>
    public void Cancel()
    {
        switch (Gesture)
        {
            case CanvasGesture.None:
                return;
            case CanvasGesture.Draw or CanvasGesture.Erase:
                _points.Clear();
                break;
            default:
                EndDrag();
                break;
        }
        Update(CanvasGesture.None, null, DefaultCursor());
    }

    /// <summary>
    /// One wheel event, sign only (wheel-sign-only-and-midstroke). Select with a selection: ScaleAboutCenter by
    /// WheelScale inside the wheel-burst gesture (R6, R7); nothing during a select drag. Other tools:
    /// BrushViewModel.StepSize, and a live shape re-renders at the new width (R11).
    /// </summary>
    public void Wheel(int sign)
    {
        if (_tools.Current != Tool.Select)
        {
            _brush.StepSize(sign);   // editor.js:1316-1318; OnBrushChanged refreshes a live shape and the ring
            return;
        }
        if (IsActive || _session.SelectedLayer is not { } layer) return;
        double scale = SelectionGeometry.WheelScale(layer.Scale, sign);
        _ownChange = true;
        try
        {
            _wheel ??= _session.BeginGesture();
            _session.Apply(d => LayerOps.ScaleAboutCenter(d, layer.Id, scale));
        }
        finally { _ownChange = false; }
    }

    /// <summary>Closes the open wheel-burst gesture, if any: the burst is recorded as one step iff it changed the scale.</summary>
    public void EndWheelBurst()
    {
        var wheel = _wheel;
        _wheel = null;
        wheel?.Dispose();
    }

    void Finish()
    {
        var gesture = Gesture;
        var points = _points.ToArray();
        var stroke = gesture == CanvasGesture.Draw ? CurrentStroke() : null;
        _points.Clear();
        if (gesture is CanvasGesture.Move or CanvasGesture.Rotate or CanvasGesture.Resize) EndDrag();
        Update(CanvasGesture.None, null, HoverCursor(_last, _zoom));
        if (stroke is not null)
        {
            // editor.js:1257-1285, :528-540: autocrop into a new top layer, selected; nothing drawn adds nothing.
            if (Tools.CommitDrawing(stroke, _session.NextId, Tools.DrawingName(_shape)) is { } layer)
                _session.ApplyWithSelection(s => LayerOps.AddDrawing(s, layer));
        }
        else if (gesture == CanvasGesture.Erase)
        {
            int id = _layerId;
            float size = _eraseSize;
            _session.Apply(d => Eraser.ApplyTo(d, id, points, size));
        }
    }

    void EndDrag()
    {
        var drag = _drag;
        _drag = null;
        drag?.Dispose();
    }

    /// <summary>The stroke drawn so far. Pen: its press style; shapes: the brush as it is now (R11).</summary>
    Stroke CurrentStroke() => _shape == ShapeTool.Pen
        ? new Stroke(ShapeTool.Pen, _points.ToArray(), _penColor, Tools.SanitizeWidth(_penWidth), false)
        : new Stroke(_shape, _points.ToArray(), _brush.SkColor, Tools.SanitizeWidth(_brush.Size), _brush.Fill);

    void OnToolChanged()
    {
        Cancel();
        EndWheelBurst();
        if (!IsActive) Update(CanvasGesture.None, null, DefaultCursor());
    }

    void OnBrushChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Gesture == CanvasGesture.Draw && _shape != ShapeTool.Pen)
            Update(Gesture, LivePreview.Draw(CurrentStroke()), Cursor);    // wheel-sign-only-and-midstroke
        if (Cursor.Kind == CanvasCursorKind.EraserRing) Update(Gesture, Preview, CanvasCursor.Ring(_brush.Size));
    }

    /// <summary>The idle cursor at a point (select-hover-idle fix; cursor-default; cursor-eraser).</summary>
    CanvasCursor HoverCursor(Point t, double zoom)
    {
        if (_tools.Current != Tool.Select) return DefaultCursor();
        return SelectionGeometry.HitTest(_session.Document, _session.SelectedLayerId, t.X, t.Y, zoom).Kind switch
        {
            SelectHitKind.Rotate => CanvasCursor.Of(CanvasCursorKind.Grab),
            SelectHitKind.Resize => CanvasCursor.Of(CanvasCursorKind.ResizeNwse),
            SelectHitKind.Move => CanvasCursor.Of(CanvasCursorKind.Move),
            _ => CanvasCursor.Crosshair,
        };
    }

    /// <summary>updateCursor (editor.js:456-459): the eraser's ring at the brush size, else crosshair.</summary>
    CanvasCursor DefaultCursor() => _tools.Current == Tool.Eraser ? CanvasCursor.Ring(_brush.Size) : CanvasCursor.Crosshair;

    void Update(CanvasGesture gesture, LivePreview? preview, CanvasCursor cursor)
    {
        if (gesture == Gesture && ReferenceEquals(preview, Preview) && cursor == Cursor) return;
        Gesture = gesture;
        Preview = preview;
        Cursor = cursor;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    static SKPoint ToSk(Point p) => new((float)p.X, (float)p.Y);

    static double Distance(Point p, (double X, double Y) c) => Math.Sqrt((p.X - c.X) * (p.X - c.X) + (p.Y - c.Y) * (p.Y - c.Y));

    static void CheckZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "zoom must be finite and positive");
    }
}
