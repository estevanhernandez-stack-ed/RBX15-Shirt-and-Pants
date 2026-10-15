using System.Windows;
using RBX15.App.Controls;

namespace RBX15.App.Tests;

/// <summary>canvas-coord-map: 4.1's (client - rect) / zoom (editor.js:1058-1061, :1149-1152, :966-967), in DIPs.</summary>
public class CanvasCoordinatesTests
{
    [Theory]
    [InlineData(100, 50, 1, 100, 50)]
    [InlineData(292.5, 139.5, 0.5, 585, 279)]
    [InlineData(0.5, 0.5, 0.25, 2, 2)]
    [InlineData(146, 70, 0.25, 584, 280)]   // a drop near the right edge at 25% (editor.js:966-967)
    [InlineData(300, 150, 3, 100, 50)]
    [InlineData(346.5, 111, 1.5, 231, 74)]  // torso_front's corner at 150%
    public void Frame_points_divide_by_zoom(double fx, double fy, double zoom, double tx, double ty)
    {
        var p = CanvasCoordinates.ToTemplate(new Point(fx, fy), zoom);
        Assert.Equal(tx, p.X, 12);
        Assert.Equal(ty, p.Y, 12);
    }

    [Fact] // 4.1 never clamps: a drag past the edge keeps going (M3b's mouse capture relies on it)
    public void Points_outside_the_frame_map_outside_the_template_unclamped()
    {
        Assert.Equal(new Point(-10, 600), CanvasCoordinates.ToTemplate(new Point(-30, 1800), 3));
        Assert.Equal(new Point(-30, 1800), CanvasCoordinates.ToFrame(new Point(-10, 600), 3));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.75)]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(2.75)]
    [InlineData(3)]
    public void ToFrame_inverts_ToTemplate(double zoom)
    {
        var frame = new Point(123.25, 456.5);
        var back = CanvasCoordinates.ToFrame(CanvasCoordinates.ToTemplate(frame, zoom), zoom);
        Assert.Equal(frame.X, back.X, 9);
        Assert.Equal(frame.Y, back.Y, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.25)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_zoom_that_is_not_finite_and_positive_is_refused(double zoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCoordinates.ToTemplate(new Point(1, 1), zoom));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCoordinates.ToFrame(new Point(1, 1), zoom));
    }
}
