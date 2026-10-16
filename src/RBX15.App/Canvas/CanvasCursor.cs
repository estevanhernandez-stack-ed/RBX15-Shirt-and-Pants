namespace RBX15.App.Canvas;

/// <summary>
/// The canvas cursor shapes (ledger canvas-cursor-states, tools-cursor, select-hover-idle,
/// cursor-default, cursor-eraser). 4.1 uses crosshair, move, grab, grabbing, nwse-resize and the
/// generated eraser ring (editor.js:443-459, :1074, :1094, :1110, :1161, :1165).
/// </summary>
public enum CanvasCursorKind { Crosshair, Move, Grab, Grabbing, ResizeNwse, EraserRing }

/// <summary>RingDiameter: the brush size in template px when Kind == EraserRing, else 0.</summary>
public readonly record struct CanvasCursor(CanvasCursorKind Kind, double RingDiameter)
{
    public static readonly CanvasCursor Crosshair = new(CanvasCursorKind.Crosshair, 0);

    /// <summary>The eraser ring for a brush of <paramref name="size"/> template px (eraserCursorURI, editor.js:443-454).</summary>
    public static CanvasCursor Ring(double size) => new(CanvasCursorKind.EraserRing, size);

    /// <summary>A cursor of <paramref name="kind"/> with no ring.</summary>
    public static CanvasCursor Of(CanvasCursorKind kind) => new(kind, 0);
}
