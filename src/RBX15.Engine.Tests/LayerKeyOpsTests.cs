using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The keyboard steps (M4b item 11; ledger keys-arrow-nudge, keys-rotate-brackets, kbd-bracket-rotate-negative-drift,
/// keys-scale-plus-minus; editor.js:1801-1825). Pure, and each returns its input when it changes nothing.
/// </summary>
public class LayerKeyOpsTests
{
    static Layer L(int id, double x = 200, double y = 100, double scale = 1, double rotation = 0) =>
        new() { Id = id, Name = "L" + id, Image = Px.Solid(100, 50, SKColors.Red), X = x, Y = y, Scale = scale, Rotation = rotation };

    static Document Doc(params Layer[] layers) => new() { Layers = ImmutableList.Create(layers) };

    static Layer Only(Document d) => Assert.Single(d.Layers);

    [Fact] // keys-arrow-nudge: x and y move by the step in template pixels, nothing else changes, no clamp to the template
    public void Nudge_moves_x_and_y_by_the_step()
    {
        var moved = Only(LayerOps.Nudge(Doc(L(1)), 1, 5, -10));
        Assert.Equal((205.0, 90.0, 1.0, 0.0), (moved.X, moved.Y, moved.Scale, moved.Rotation));
        Assert.Equal(-800.0, Only(LayerOps.Nudge(Doc(L(1, x: 0)), 1, -800, 0)).X);
    }

    [Fact] // a zero step, a step that is not finite and a missing id change nothing
    public void Nudge_that_moves_nothing_returns_the_input()
    {
        var doc = Doc(L(1));
        Assert.Same(doc, LayerOps.Nudge(doc, 1, 0, 0));
        Assert.Same(doc, LayerOps.Nudge(doc, 1, double.NaN, 1));
        Assert.Same(doc, LayerOps.Nudge(doc, 1, 1, double.PositiveInfinity));
        Assert.Same(doc, LayerOps.Nudge(doc, 9, 1, 1));
    }

    [Theory] // done-when: 3 + ] = 8; 178 + ] = -177 (not 183); -180 + [ = 175 (not -185, kbd-bracket-rotate-negative-drift)
    [InlineData(3, 5, 8)]
    [InlineData(178, 5, -177)]
    [InlineData(-180, -5, 175)]
    [InlineData(175, 5, -180)]       // the range is [-180, 180): 180 wraps to -180, as 4.1's formula
    [InlineData(-175, -5, -180)]
    [InlineData(0, -15, -15)]
    [InlineData(170, 15, -175)]
    [InlineData(-170, -15, 175)]
    [InlineData(180, 5, -175)]       // a stored 180 (the slider's end) steps on from 180
    [InlineData(-190, 5, 175)]       // a loaded out-of-range value comes into range
    public void RotateStep_wraps_with_a_true_modulo(double from, double step, double expected)
    {
        var rotated = Only(LayerOps.RotateStep(Doc(L(1, rotation: from)), 1, step));
        Assert.Equal(expected, rotated.Rotation);
    }

    [Fact] // a step inside the range is plain addition: no float noise from adding and taking away 180
    public void RotateStep_inside_the_range_is_plain_addition()
    {
        Assert.Equal(0.1 + 5, Only(LayerOps.RotateStep(Doc(L(1, rotation: 0.1)), 1, 5)).Rotation);
        Assert.Equal(17.25 - 5, Only(LayerOps.RotateStep(Doc(L(1, rotation: 17.25)), 1, -5)).Rotation);
    }

    [Fact]
    public void RotateStep_that_changes_nothing_returns_the_input()
    {
        var doc = Doc(L(1));
        Assert.Same(doc, LayerOps.RotateStep(doc, 1, 0));
        Assert.Same(doc, LayerOps.RotateStep(doc, 1, double.NaN));
        Assert.Same(doc, LayerOps.RotateStep(doc, 9, 5));
        Assert.Same(doc, LayerOps.RotateStep(doc, 1, 360));   // a full turn lands on the same angle
    }

    [Fact] // keys-scale-plus-minus: 1 -> 1.1 -> 1.21, anchored at the top-left (x and y kept)
    public void ScaleStep_multiplies_about_the_top_left()
    {
        var doc = Doc(L(1));
        doc = LayerOps.ScaleStep(doc, 1, 1.1);
        Assert.Equal(1.1, Only(doc).Scale, 12);
        doc = LayerOps.ScaleStep(doc, 1, 1.1);
        var l = Only(doc);
        Assert.Equal(1.21, l.Scale, 12);
        Assert.Equal((200.0, 100.0), (l.X, l.Y));
        Assert.Equal(1.089, Only(LayerOps.ScaleStep(Doc(L(1, scale: 1.21)), 1, 0.9)).Scale, 12);
    }

    [Theory] // the cap is 5 and the floor 0.01
    [InlineData(4.8, 1.1, 5)]
    [InlineData(0.0105, 0.9, 0.01)]
    public void ScaleStep_clamps(double from, double factor, double expected) =>
        Assert.Equal(expected, Only(LayerOps.ScaleStep(Doc(L(1, scale: from)), 1, factor)).Scale);

    [Fact] // at the cap or the floor a press changes nothing, so it records no step
    public void ScaleStep_at_a_limit_returns_the_input()
    {
        var capped = Doc(L(1, scale: 5));
        Assert.Same(capped, LayerOps.ScaleStep(capped, 1, 1.1));
        var floor = Doc(L(1, scale: 0.01));
        Assert.Same(floor, LayerOps.ScaleStep(floor, 1, 0.9));
        Assert.Same(capped, LayerOps.ScaleStep(capped, 9, 0.9));
        Assert.Same(capped, LayerOps.ScaleStep(capped, 1, double.NaN));
    }
}
