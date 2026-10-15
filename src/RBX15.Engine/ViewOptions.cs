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

    static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
}
