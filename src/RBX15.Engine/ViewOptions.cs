namespace RBX15.Engine;

/// <summary>
/// How the editor view shows a document (render(), editor.js:339-359).
/// <list type="bullet">
/// <item><c>Zoom</c>: 4.1's zoom (editor.js:81; the app steps it 0.25 to 3, zoom-controls). Any finite positive value here.</item>
/// <item><c>DeviceScale</c>: the monitor's device pixels per DIP (1 at 96 DPI, 1.5 at 144 DPI). The view renders at
///   device resolution, so it is crisp at per-monitor DPI v2 (cross-window-dpi-rendering). 4.1 drew in CSS px and let
///   Chromium stretch the bitmap.</item>
/// <item><c>ShowGrid</c>: the region outlines (editor.js:352-359). The app passes <c>Document.ShowGrid</c>.</item>
/// </list>
/// At <c>DeviceScale == 1</c> the frame is exactly 4.1's visible canvas; the view goldens pin that.
/// Both numbers are checked on construction and on <c>with</c>.
/// </summary>
public sealed record ViewOptions(double Zoom, double DeviceScale = 1, bool ShowGrid = false)
{
    readonly double _zoom = Positive(Zoom, nameof(Zoom));
    readonly double _deviceScale = Positive(DeviceScale, nameof(DeviceScale));

    public double Zoom { get => _zoom; init => _zoom = Positive(value, nameof(Zoom)); }
    public double DeviceScale { get => _deviceScale; init => _deviceScale = Positive(value, nameof(DeviceScale)); }

    /// <summary>Device pixels per template pixel: <c>Zoom * DeviceScale</c>.</summary>
    public double Scale => Zoom * DeviceScale;

    /// <summary>
    /// Draw 4.1's selection overlay (editor.js:361-425) for this layer: box, corner handles, rotation stem, knob, glyph
    /// and the label when Rotation != 0. Null, a missing id or a hidden layer (M3b ruling R2) draws nothing. The app
    /// passes it only while the tool is Select (editor.js:362).
    /// </summary>
    public int? SelectedLayerId { get; init; }

    /// <summary>
    /// Draw the region guide under the template (new in v5; ledger view-region-guide): each region of the document's mode
    /// tinted white at 7% over the checkerboard and outlined in brand cyan at 45%, one device pixel inside its edge. A
    /// working aid only: it is never in the export or the 3D preview. With a transparent background (a new template,
    /// doc-new-transparent-background) the regions would otherwise read as the same checkerboard as the gaps between
    /// them. Default off, so the frame stays 4.1's for the view goldens; the app turns it on.
    /// </summary>
    public bool ShowRegionGuide { get; init; }

    /// <summary>The live drawing preview (editor.js:324-330), composited after the layers and before the mask. Null: none.</summary>
    public LivePreview? Preview { get; init; }

    static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
}
