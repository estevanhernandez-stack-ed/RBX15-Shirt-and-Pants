using System.Windows;
using System.Windows.Media;

namespace RBX15.App.Tests;

/// <summary>
/// Expected positions that hold at every display scale. WPF lays out in DIPs and places in whole device pixels, so a
/// test that adds a DIP offset to a screen origin is right only at 100%: at 125% an offset of 12.5 device pixels
/// comes back from PointToScreen as 13, and the bridge injects the mouse at 62 where 62.5 was meant. These helpers take
/// the real transform (the element's own PresentationSource, or the root DPI a test set with
/// <see cref="VisualTreeHelper.SetRootDpi"/>) and do the same rounding the product path does, so an assertion compares
/// the exact value to the snapped one and needs no fudge beyond <see cref="HalfDevicePixel"/>.
/// </summary>
internal static class DeviceRounding
{
    /// <summary>
    /// The only tolerance a position assertion may carry, in device pixels: a value snapped to the device grid is at
    /// most half a pixel from the exact one (WPF rounds the screen point, and layout rounding snaps an offset).
    /// A float epsilon is added so that an exact half (12.5 against 13) passes.
    /// </summary>
    public const double HalfDevicePixel = 0.5 + 1e-9;

    static bool _primed;

    /// <summary>
    /// Gives an element that is tested outside a window a display scale of <paramref name="dpi"/> (96 is 100%, 120 is 125%,
    /// 144 is 150%) with <see cref="VisualTreeHelper.SetRootDpi"/>. WPF's behavior, found by measuring it, shapes three rules here:
    /// <list type="bullet">
    /// <item>The first SetRootDpi in a process becomes the default scale of every element made afterwards. So the first call
    /// here sets the system scale on a throwaway element, and a theory row at 144 cannot leave later tests at 150%.</item>
    /// <item>It reaches only the elements that exist when it is called. A UserControl's template is built by its first layout, and
    /// items an ItemsControl makes later start at the default. So lay the element out once, call this, and call <see cref="Propagate"/> after
    /// any layout that realizes more of the tree.</item>
    /// <item>A shown window takes its monitor's scale and ignores this, so an off-screen MainWindow test computes through the real
    /// transform (<see cref="ToDevice(Visual, Point)"/>, <see cref="Injected"/>) and carries no DPI rows.</item>
    /// </list>
    /// </summary>
    public static void SetRootDpi(Visual root, double dpi)
    {
        if (!_primed)
        {
            _primed = true;
            VisualTreeHelper.SetRootDpi(new System.Windows.Controls.Border(), VisualTreeHelper.GetDpi(new System.Windows.Controls.Border()));
        }
        VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96, dpi / 96));
    }

    /// <summary>Re-applies the root's own scale to every element realized since <see cref="SetRootDpi"/> (a no-op at the default scale).</summary>
    public static void Propagate(Visual root) => VisualTreeHelper.SetRootDpi(root, VisualTreeHelper.GetDpi(root));

    /// <summary>Device pixels per DIP on the visual's real DPI: its window's when shown, else its root's (the system's unless a test set it).</summary>
    public static double ScaleOf(Visual v) => VisualTreeHelper.GetDpi(v).DpiScaleX;

    /// <summary>
    /// A point in the visual's DIP space as an exact (unrounded) screen position in device pixels, through the
    /// real transform: the visual to its window root, the composition target's TransformToDevice, then the client origin.
    /// </summary>
    public static Point ToDevice(Visual v, Point dip)
    {
        var source = PresentationSource.FromVisual(v) ?? throw new InvalidOperationException("the visual is not shown, so it has no screen position");
        var root = (Visual)source.RootVisual;
        var inRoot = ReferenceEquals(v, root) ? dip : v.TransformToAncestor(root).Transform(dip);
        var device = source.CompositionTarget.TransformToDevice.Transform(inRoot);
        var client = root.PointToScreen(new Point(0, 0));   // the client origin: a whole pixel, and the same call the product ends in
        return new Point(client.X + device.X, client.Y + device.Y);
    }

    /// <summary>A length in DIPs as device pixels at <paramref name="scale"/> (not rounded).</summary>
    public static double ToDevice(double dip, double scale) => dip * scale;

    /// <summary>A device-pixel value on the device grid. Math.Round, to even, is what RawMouse does when the bridge injects a point.</summary>
    public static double Snap(double devicePixels) => Math.Round(devicePixels);

    /// <summary>A DIP length on the device grid: the nearest whole number of device pixels, back in DIPs.</summary>
    public static double Snap(double dip, double scale) => Snap(dip * scale) / scale;

    /// <summary>
    /// Where a point in <paramref name="v"/>'s DIP space lands, in the same space, once the bridge has injected it
    /// as whole device pixels in the window's client area (RawMouse rounds), which is what the canvas then reads back.
    /// </summary>
    public static Point Injected(Visual v, Point dip)
    {
        var source = PresentationSource.FromVisual(v) ?? throw new InvalidOperationException("the visual is not shown");
        var root = (Visual)source.RootVisual;
        var toDevice = source.CompositionTarget.TransformToDevice;
        var fromDevice = toDevice;
        fromDevice.Invert();
        var inRoot = ReferenceEquals(v, root) ? dip : v.TransformToAncestor(root).Transform(dip);
        var device = toDevice.Transform(inRoot);
        var snapped = fromDevice.Transform(new Point(Snap(device.X), Snap(device.Y)));
        return ReferenceEquals(v, root) ? snapped : root.TransformToDescendant(v).Transform(snapped);
    }

    /// <summary>
    /// The farthest point from <paramref name="from"/> whose double-arithmetic distance does not exceed
    /// <paramref name="distance"/>. <c>from + distance - from</c> can come back an ulp over (40.4 + 3.2 - 40.4 is
    /// 3.200000000000003), which turns a "still within the drag distance" probe into a drag at 125%, where the system
    /// distance is 3.2 DIPs and not a whole number. This is not a tolerance: the probe sits on the boundary.
    /// </summary>
    public static double AtMost(double from, double distance)
    {
        double to = from + distance;
        while (to - from > distance) to = Math.BitDecrement(to);
        return to;
    }
}
