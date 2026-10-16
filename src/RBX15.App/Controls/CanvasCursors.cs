using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;
using RBX15.App.Canvas;
using SkiaSharp;

namespace RBX15.App.Controls;

/// <summary>
/// WPF cursors for <see cref="CanvasCursor"/> (ledger canvas-cursor-states, tools-cursor, cursor-eraser).
/// <list type="bullet">
/// <item>Crosshair is Cross (editor.js:458), Move is SizeAll (CSS move, :1110), ResizeNwse is SizeNWSE
///   (:1094). Grab and Grabbing (:1161, :1074) have no stock Windows cursor; both are Hand. Provisional; M3b look
///   sign-off.</item>
/// <item>EraserRing is eraserCursorURI (editor.js:443-454) drawn at device resolution: a black 3 px ring at 0.5
///   opacity under a #f22f89 1.5 px dashed (4 3) ring, and a 1.5 px #f22f89 center dot, every length in CSS px
///   times the device scale; radius max(4, size*zoom/2) CSS px, hotspot at the center. 4.1's 120 px cap is
///   dropped (cursor-eraser fix): the only cap is the Win32 cursor size limit of 256 device px a side.</item>
/// </list>
/// Ring cursors are cached by side length and radius in hundredths of a device pixel. UI thread only.
/// </summary>
public static class CanvasCursors
{
    /// <summary>The largest cursor bitmap Windows shows, in device px a side.</summary>
    public const int MaxCursorSide = 256;

    static readonly Dictionary<(int Side, long Radius), Cursor> RingCache = [];

    public static Cursor ToWpf(CanvasCursor cursor, double zoom, double deviceScale) => cursor.Kind switch
    {
        CanvasCursorKind.Crosshair => Cursors.Cross,
        CanvasCursorKind.Move => Cursors.SizeAll,
        CanvasCursorKind.Grab or CanvasCursorKind.Grabbing => Cursors.Hand,
        CanvasCursorKind.ResizeNwse => Cursors.SizeNWSE,
        CanvasCursorKind.EraserRing => EraserRing(cursor.RingDiameter, zoom, deviceScale),
        _ => throw new ArgumentOutOfRangeException(nameof(cursor), cursor.Kind, "unknown cursor kind"),
    };

    /// <summary>The device-px margin around the ring (4.1: d = ceil(r)*2 + 4 at device scale 1, editor.js:445).</summary>
    internal static int Margin(double deviceScale) => (int)Math.Ceiling(2 * deviceScale);

    /// <summary>The ring radius in device px: max(4, size*zoom/2) CSS px times the scale, capped so the cursor fits 256 px.</summary>
    internal static double RingRadius(double diameterTemplatePx, double zoom, double deviceScale)
    {
        Check(diameterTemplatePx, zoom, deviceScale);
        double r = Math.Max(4, diameterTemplatePx * zoom / 2) * deviceScale;
        return Math.Min(r, MaxCursorSide / 2 - Margin(deviceScale));
    }

    /// <summary>The cursor bitmap's side in device px: 2*ceil(r) plus the margin on both sides.</summary>
    internal static int RingSide(double radius, double deviceScale) => 2 * (int)Math.Ceiling(radius) + 2 * Margin(deviceScale);

    static Cursor EraserRing(double diameterTemplatePx, double zoom, double deviceScale)
    {
        double r = RingRadius(diameterTemplatePx, zoom, deviceScale);
        var key = (RingSide(r, deviceScale), (long)Math.Round(r * 100));
        if (RingCache.TryGetValue(key, out var cached)) return cached;
        using var ring = RenderEraserRing(diameterTemplatePx, zoom, deviceScale);
        var cursor = CreateCursor(ring);
        RingCache[key] = cursor;
        return cursor;
    }

    /// <summary>The ring bitmap, premultiplied BGRA, square, the ring centered at side/2 (the hotspot).</summary>
    internal static SKBitmap RenderEraserRing(double diameterTemplatePx, double zoom, double deviceScale)
    {
        double r = RingRadius(diameterTemplatePx, zoom, deviceScale);
        int side = RingSide(r, deviceScale);
        float c = side / 2f, ds = (float)deviceScale, radius = (float)r;
        var bitmap = new SKBitmap(new SKImageInfo(side, side, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var magenta = new SKColor(0xF2, 0x2F, 0x89);
        using (var under = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ds, Color = SKColors.Black.WithAlpha(128) })
            canvas.DrawCircle(c, c, radius, under);
        using (var dash = SKPathEffect.CreateDash([4 * ds, 3 * ds], 0))
        using (var ringPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ds, Color = magenta, PathEffect = dash })
            canvas.DrawCircle(c, c, radius, ringPaint);
        using (var dot = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = magenta })
            canvas.DrawCircle(c, c, 1.5f * ds, dot);
        return bitmap;
    }

    /// <summary>A Win32 cursor from the ring: straight-alpha 32 bpp color, an empty mask, the hotspot at the center.</summary>
    static Cursor CreateCursor(SKBitmap ring)
    {
        int side = ring.Width;
        var straight = new byte[side * side * 4];
        var info = new SKImageInfo(side, side, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pin = GCHandle.Alloc(straight, GCHandleType.Pinned);
        IntPtr color = IntPtr.Zero, mask = IntPtr.Zero;
        try
        {
            using (var pixels = ring.PeekPixels())
                if (!pixels.ReadPixels(info, pin.AddrOfPinnedObject(), side * 4, 0, 0)) throw new InvalidOperationException("cannot read the ring pixels");
            color = CreateBitmap(side, side, 1, 32, pin.AddrOfPinnedObject());
            var maskBits = new byte[(side + 15) / 16 * 2 * side];
            var maskPin = GCHandle.Alloc(maskBits, GCHandleType.Pinned);
            try { mask = CreateBitmap(side, side, 1, 1, maskPin.AddrOfPinnedObject()); }
            finally { maskPin.Free(); }
            if (color == IntPtr.Zero || mask == IntPtr.Zero) throw new Win32Exception();
            var iconInfo = new IconInfo { fIcon = false, xHotspot = side / 2, yHotspot = side / 2, hbmMask = mask, hbmColor = color };
            IntPtr icon = CreateIconIndirect(ref iconInfo);
            if (icon == IntPtr.Zero) throw new Win32Exception();
            return CursorInteropHelper.Create(new IconHandle(icon));
        }
        finally
        {
            pin.Free();
            if (color != IntPtr.Zero) DeleteObject(color);
            if (mask != IntPtr.Zero) DeleteObject(mask);
        }
    }

    static void Check(double diameter, double zoom, double deviceScale)
    {
        if (!double.IsFinite(diameter) || diameter < 0) throw new ArgumentOutOfRangeException(nameof(diameter), diameter, "ring diameter must be finite and not negative");
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "zoom must be finite and positive");
        if (!double.IsFinite(deviceScale) || deviceScale <= 0) throw new ArgumentOutOfRangeException(nameof(deviceScale), deviceScale, "device scale must be finite and positive");
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    sealed class IconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public IconHandle(IntPtr handle) : base(ownsHandle: true) => SetHandle(handle);

        protected override bool ReleaseHandle() => DestroyIcon(handle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll", SetLastError = true)]
    static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, IntPtr bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteObject(IntPtr gdiObject);
}
