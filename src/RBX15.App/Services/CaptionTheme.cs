using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RBX15.App.Services;

/// <summary>
/// Themes the native Windows caption through DWM, keeping the system frame, the caption buttons and snap
/// layouts (no custom WindowChrome; ledger <c>shell-window-frame</c>, fix; Este 2026-10-14). Once the
/// window has its HWND it sets immersive dark mode, the caption color (the header's middle stop,
/// brand-navy-deep, so the caption and the header read as one band), light caption text (ink-100) and
/// the border (brand-navy-soft). Windows that lack an attribute (dark mode before Windows 10 20H1,
/// colors before Windows 11) refuse it with an HRESULT, which is ignored.
/// </summary>
public static class CaptionTheme
{
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_BORDER_COLOR = 34;
    internal const int DWMWA_CAPTION_COLOR = 35;
    internal const int DWMWA_TEXT_COLOR = 36;

    /// <summary>The theme keys (Tokens.xaml) the caption takes its colors from.</summary>
    public const string CaptionColorKey = "BrandNavyDeepColor", TextColorKey = "Ink100Color", BorderColorKey = "BrandNavySoftColor";

    /// <summary>Themes <paramref name="window"/>'s caption now if it has an HWND, otherwise when it gets one (SourceInitialized).</summary>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero) Set(window, handle);
        else window.SourceInitialized += (_, _) => Set(window, new WindowInteropHelper(window).Handle);
    }

    /// <summary>
    /// The DWM attributes and values for these colors, in the order they are set: dark mode on, then
    /// caption, text and border as COLORREFs. A color that is null (its theme key is missing) is left out.
    /// </summary>
    internal static IReadOnlyList<(int Attribute, uint Value)> Attributes(Color? caption, Color? text, Color? border)
    {
        var list = new List<(int, uint)> { (DWMWA_USE_IMMERSIVE_DARK_MODE, 1) };
        if (caption is { } c) list.Add((DWMWA_CAPTION_COLOR, ToColorRef(c)));
        if (text is { } t) list.Add((DWMWA_TEXT_COLOR, ToColorRef(t)));
        if (border is { } b) list.Add((DWMWA_BORDER_COLOR, ToColorRef(b)));
        return list;
    }

    /// <summary>A WPF color to a Win32 COLORREF, <c>0x00BBGGRR</c>. Alpha is dropped: DWM colors are opaque.</summary>
    internal static uint ToColorRef(Color color) => (uint)(color.R | (color.G << 8) | (color.B << 16));

    /// <summary>Sets every attribute on <paramref name="hwnd"/>; returns each HRESULT (0 is S_OK). Never throws.</summary>
    internal static IReadOnlyList<int> Set(FrameworkElement window, IntPtr hwnd)
    {
        var results = new List<int>();
        if (hwnd == IntPtr.Zero) return results;
        foreach (var (attribute, value) in Attributes(Find(window, CaptionColorKey), Find(window, TextColorKey), Find(window, BorderColorKey)))
        {
            uint v = value;
            try { results.Add(DwmSetWindowAttribute(hwnd, attribute, ref v, sizeof(uint))); }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { results.Add(unchecked((int)0x80004001)); } // E_NOTIMPL
        }
        return results;
    }

    static Color? Find(FrameworkElement element, string key) => element.TryFindResource(key) is Color c ? c : null;

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
}
