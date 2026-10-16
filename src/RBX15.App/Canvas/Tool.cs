using RBX15.Engine;

namespace RBX15.App.Canvas;

/// <summary>
/// The six canvas tools (editor.js:464-471; ledger tools-set-tool). 4.1's ids are select, pen, line,
/// rect, circle and eraser; Ellipse is 4.1's 'circle', whose layers are named "Circle drawing"
/// (Tools.DrawingName, editor.js:1285).
/// </summary>
public enum Tool { Select, Pen, Line, Rect, Ellipse, Eraser }

/// <summary>Tool to engine shape mapping.</summary>
public static class ToolShapes
{
    /// <summary>The engine shape a drawing tool strokes; null for Select and Eraser, which make no layer.</summary>
    public static ShapeTool? ShapeOf(Tool tool) => tool switch
    {
        Tool.Pen => ShapeTool.Pen,
        Tool.Line => ShapeTool.Line,
        Tool.Rect => ShapeTool.Rect,
        Tool.Ellipse => ShapeTool.Ellipse,
        Tool.Select or Tool.Eraser => null,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "not a canvas tool"),
    };
}
