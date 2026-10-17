namespace RBX15.App.Controls;

/// <summary>
/// What the canvas's layer automation peers act through (M4a contract "Canvas UI Automation"). The canvas view
/// model implements it over the session, so a layer selected, moved, resized or rotated through UI Automation goes
/// through the same session entry points as the mouse: one undo step per change, none for a no-op (spec §5;
/// ledger undo-empty-steps). Sizes and positions are template pixels (585x559), never screen pixels.
/// </summary>
public interface ICanvasLayerAutomation
{
    /// <summary>The selected layer's id, or null.</summary>
    int? SelectedLayerId { get; }

    /// <summary>Selects a layer (null deselects). A non-null id also sets the Select tool, as a pick in the layers list does.</summary>
    void SelectLayer(int? id);

    /// <summary>Sets the layer's top-left to (x, y) in template pixels. True iff the document changed (one undo step).</summary>
    bool MoveLayer(int id, double x, double y);

    /// <summary>
    /// Scales the layer about its center so its displayed size fits width x height template pixels:
    /// scale = min(width / image width, height / image height), under the one 1-500% clamp. True iff it changed.
    /// </summary>
    bool ResizeLayer(int id, double width, double height);

    /// <summary>Turns the layer by <paramref name="degrees"/> (relative, clockwise), normalized to -180..180. True iff it changed.</summary>
    bool RotateLayer(int id, double degrees);
}
