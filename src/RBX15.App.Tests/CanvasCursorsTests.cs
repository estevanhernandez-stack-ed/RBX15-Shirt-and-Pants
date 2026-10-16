using System.Windows.Input;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas cursors (canvas-cursor-states, tools-cursor, cursor-eraser, eraser-cursor): stock shapes, and
/// eraserCursorURI's ring (editor.js:443-454) at device resolution with 4.1's 120 px cap dropped (R15).
/// </summary>
public class CanvasCursorsTests
{
    static readonly SKColor Magenta = new(0xF2, 0x2F, 0x89);

    [Fact] // canvas-cursor-states: crosshair, move, grab, grabbing, nwse-resize (Grab and Grabbing are Hand: provisional)
    public void Stock_kinds_map_to_windows_cursors() => StaThread.Run(() =>
    {
        Assert.Same(Cursors.Cross, CanvasCursors.ToWpf(CanvasCursor.Crosshair, 1, 1));
        Assert.Same(Cursors.SizeAll, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.Move), 1, 1));
        Assert.Same(Cursors.Hand, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.Grab), 1, 1));
        Assert.Same(Cursors.Hand, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.Grabbing), 1, 1));
        Assert.Same(Cursors.SizeNWSE, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.ResizeNwse), 1, 1));
    });

    [Theory] // cursor-eraser: r = max(4, size*zoom/2) CSS px, times the device scale; no 120 cap; the 256 px cursor limit
    [InlineData(4, 1, 1, 4)]       // min 4 (8 px diameter)
    [InlineData(1, 0.25, 1, 4)]
    [InlineData(20, 1, 1, 10)]
    [InlineData(20, 2, 1, 20)]
    [InlineData(20, 1, 1.5, 15)]
    [InlineData(50, 3, 1, 75)]
    [InlineData(300, 1, 1, 126)]   // (256 - 4) / 2 at 100%
    [InlineData(50, 3, 2, 124)]    // 150 device px wanted; (256 - 8) / 2 at 200%
    public void The_ring_radius_is_half_the_brush_times_zoom_at_least_4_and_fits_a_cursor(double size, double zoom, double scale, double radius)
    {
        double r = CanvasCursors.RingRadius(size, zoom, scale);
        Assert.Equal(radius, r, 9);
        Assert.True(CanvasCursors.RingSide(r, scale) <= CanvasCursors.MaxCursorSide);
    }

    [Fact] // eraserCursorURI: d = ceil(r)*2 + 4; black 3 px at .5 under the #f22f89 ring; a 1.5 px center dot; hotspot d/2
    public void The_ring_bitmap_is_4_1s_svg()
    {
        using var ring = CanvasCursors.RenderEraserRing(20, 1, 1);
        Assert.Equal((24, 24), (ring.Width, ring.Height));
        var center = ring.GetPixel(11, 11);
        Assert.True(center.Alpha > 200 && center.Red > center.Green, $"center dot {center}");
        Assert.Equal(0, ring.GetPixel(12, 6).Alpha);                // inside the ring, off the dot
        Assert.Equal(0, ring.GetPixel(0, 0).Alpha);                 // the corner
        Assert.True(ring.GetPixel(22, 12).Alpha >= 127, $"ring {ring.GetPixel(22, 12)}"); // r = 10 right of the center
        var ringPixels = Enumerable.Range(0, 24).SelectMany(y => Enumerable.Range(0, 24).Select(x => ring.GetPixel(x, y)))
            .Where(p => p.Alpha == 255).ToList();
        Assert.Contains(ringPixels, p => p.Red == Magenta.Red && p.Green == Magenta.Green && p.Blue == Magenta.Blue);
    }

    [Fact] // R15: the largest ring fills the 256 px cursor exactly at 100%
    public void The_largest_ring_is_256_px()
    {
        using var ring = CanvasCursors.RenderEraserRing(300, 1, 1);
        Assert.Equal(256, ring.Width);
    }

    [Fact] // tools-cursor fix: the ring follows zoom and the device scale; one cursor per size, cached
    public void Ring_cursors_are_cached_per_size_and_follow_zoom() => StaThread.Run(() =>
    {
        var a = CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1);
        Assert.Same(a, CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1));
        Assert.Same(a, CanvasCursors.ToWpf(CanvasCursor.Ring(10), 2, 1));   // the same screen size
        Assert.NotSame(a, CanvasCursors.ToWpf(CanvasCursor.Ring(20), 2, 1));
        Assert.NotSame(a, CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1.5));
    });

    [Fact]
    public void Bad_ring_inputs_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCursors.RingRadius(-1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCursors.RingRadius(4, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCursors.RingRadius(4, 1, double.NaN));
    }
}
