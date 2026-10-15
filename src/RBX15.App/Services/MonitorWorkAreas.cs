using System.Runtime.InteropServices;
using System.Windows;

namespace RBX15.App.Services;

/// <summary>
/// Every monitor's work area (the desktop minus the taskbar) for <see cref="PlacementGuard"/>, primary
/// first, without a WinForms reference. Each rectangle is the monitor's physical work area divided by
/// that monitor's own scale (GetDpiForMonitor / 96), the per-monitor DPI v2 mapping WPF applies to
/// <see cref="Window.Left"/> and <see cref="Window.Top"/> for a window on that monitor.
/// </summary>
internal static class MonitorWorkAreas
{
    public static IReadOnlyList<Rect> Get()
    {
        var areas = new List<(bool Primary, Rect Area)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfoW(monitor, ref info)) return true;
            double scale = GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 && dpiX > 0 ? dpiX / 96.0 : 1;
            var r = info.Work;
            areas.Add(((info.Flags & 1) != 0, new Rect(r.Left / scale, r.Top / scale, (r.Right - r.Left) / scale, (r.Bottom - r.Top) / scale)));
            return true;
        }, IntPtr.Zero);
        return Usable(areas, SystemParameters.WorkArea);
    }

    /// <summary>
    /// The usable work areas, primary first: a monitor reporting a zero or negative width or height
    /// (a disconnecting display) is dropped, since <see cref="PlacementGuard.Resolve"/> refuses it. When
    /// none survive, <paramref name="fallback"/> if it is usable, else a 1024x768 desktop, so startup always
    /// has a rectangle to place the window in.
    /// </summary>
    internal static IReadOnlyList<Rect> Usable(IEnumerable<(bool Primary, Rect Area)> areas, Rect fallback)
    {
        var kept = areas.Where(a => IsUsable(a.Area)).OrderByDescending(a => a.Primary).Select(a => a.Area).ToList();
        if (kept.Count > 0) return kept;
        return [IsUsable(fallback) ? fallback : new Rect(0, 0, 1024, 768)];
    }

    static bool IsUsable(Rect r) => !r.IsEmpty && r.Width > 0 && r.Height > 0;

    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
