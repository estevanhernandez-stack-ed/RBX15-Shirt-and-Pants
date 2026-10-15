using System.Windows;

namespace RBX15.App.Controls;

/// <summary>
/// Mouse position to template space and back (canvas-coord-map). 4.1 maps every canvas event with
/// <c>(clientX - rect.left) / zoom</c> (editor.js:1058-1061, :1149-1152; the drop point at :966-967):
/// CSS px relative to the canvas, divided by zoom, unclamped. v5 takes the position relative to the
/// <see cref="TemplateCanvasHost"/> in DIPs, which are CSS px. The host is <c>ViewSize / DeviceScale</c>
/// DIPs, so the device scale cancels and only zoom remains. A point outside the frame maps outside
/// the template (negative, or past 585x559); callers clamp if they need to. M3b's tools consume this.
/// </summary>
public static class CanvasCoordinates
{
    public static Point ToTemplate(Point inFrame, double zoom)
    {
        CheckZoom(zoom);
        return new Point(inFrame.X / zoom, inFrame.Y / zoom);
    }

    public static Point ToFrame(Point template, double zoom)
    {
        CheckZoom(zoom);
        return new Point(template.X * zoom, template.Y * zoom);
    }

    static void CheckZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "zoom must be finite and positive");
    }
}
