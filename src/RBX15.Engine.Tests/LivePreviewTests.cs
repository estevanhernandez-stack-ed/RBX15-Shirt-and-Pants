using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>LivePreview: the stroke in progress (editor.js:1120-1234) and its alpha (editor.js:327). Ruling R12.</summary>
public class LivePreviewTests
{
    [Fact] // a draw preview keeps the stroke as given, at alpha 1, and owns a copy of the points
    public void Draw_keeps_the_stroke_and_copies_the_points()
    {
        var points = new List<SKPoint> { new(10, 10), new(20, 30) };
        var stroke = new Stroke(ShapeTool.Rect, points, SKColors.Lime, 6, true);
        var p = LivePreview.Draw(stroke);
        points.Add(new SKPoint(99, 99));
        Assert.Equal(new[] { new SKPoint(10, 10), new SKPoint(20, 30) }, p.Stroke.Points);
        Assert.Equal((ShapeTool.Rect, SKColors.Lime, 6f, true), (p.Stroke.Tool, p.Stroke.Color, p.Stroke.Width, p.Stroke.Fill));
        Assert.False(p.IsEraser);
        Assert.Equal(1f, p.Alpha);
    }

    [Theory] // editor.js:1138-1140: an opaque #f22f89 pen at the brush size, round caps; Tools.SanitizeWidth applies
    [InlineData(12f, 12f)]
    [InlineData(0f, 1f)]
    [InlineData(-4f, 1f)]
    [InlineData(float.NaN, 1f)]
    public void Erase_is_a_magenta_pen_at_half_alpha(float size, float width)
    {
        var points = new List<SKPoint> { new(1, 2), new(3, 4) };
        var p = LivePreview.Erase(points, size);
        points.Clear();
        Assert.True(p.IsEraser);
        Assert.Equal(0.5f, p.Alpha);
        Assert.Equal((ShapeTool.Pen, Eraser.StrokeColor, width, false), (p.Stroke.Tool, p.Stroke.Color, p.Stroke.Width, p.Stroke.Fill));
        Assert.Equal(2, p.Stroke.Points.Count);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => LivePreview.Draw(null!));
        Assert.Throws<ArgumentNullException>(() => LivePreview.Draw(new Stroke(ShapeTool.Pen, null!, SKColors.Red, 1, false)));
        Assert.Throws<ArgumentNullException>(() => LivePreview.Erase(null!, 4));
    }
}
