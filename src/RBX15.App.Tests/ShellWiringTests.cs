using System.Collections.Immutable;
using System.Windows;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The wiring Composition.Build adds between the canvas and the session (Task 4 Step 21).</summary>
public class ShellWiringTests
{
    [Fact] // Task 2 ruling R5 (critic C13): editor.js:1100-1116, topmost visible layer under the point, else deselect
    public void A_canvas_click_selects_the_topmost_layer_under_it_and_empty_canvas_deselects() => StaThread.Run(() =>
    {
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        var image = surface.Snapshot();
        var session = new EditorSession(new Document
        {
            Layers = ImmutableList.Create(
                new Layer { Id = 1, Image = image, X = 100, Y = 100 },
                new Layer { Id = 2, Image = image, X = 120, Y = 120 },
                new Layer { Id = 3, Image = image, X = 300, Y = 300, Visible = false }),
        });
        var main = Composition.Build(session, () => null);

        main.Canvas.ClickCommand.Execute(new Point(130, 130)); // inside both: the top one wins
        Assert.Equal(2, session.SelectedLayerId);
        main.Canvas.ClickCommand.Execute(new Point(105, 105)); // only inside layer 1
        Assert.Equal(1, session.SelectedLayerId);
        main.Canvas.ClickCommand.Execute(new Point(310, 310)); // a hidden layer is never hit
        Assert.Null(session.SelectedLayerId);
        main.Canvas.ClickCommand.Execute(new Point(105, 105));
        main.Canvas.ClickCommand.Execute(new Point(10, 500));  // empty canvas deselects
        Assert.Null(session.SelectedLayerId);
        Assert.Equal(0, session.UndoCount);                     // selection is never an undo step
    });
}
