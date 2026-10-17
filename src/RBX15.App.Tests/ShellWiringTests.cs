using System.Collections.Immutable;
using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The wiring Composition.Build adds between the canvas, the tools and the session.</summary>
public class ShellWiringTests
{
    [Fact] // select-hit-test, select-deselect-click: editor.js:1100-1116, topmost visible layer under the press, else deselect
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
        var main = Composition.Build(session, () => null, new Fakes.FakeSettingsStore());
        void Click(double x, double y)
        {
            main.Canvas.Interaction.PointerDown(new Point(x, y), 1);
            main.Canvas.Interaction.PointerUp(new Point(x, y), 1);
        }

        Click(130, 130); // inside both: the top one wins
        Assert.Equal(2, session.SelectedLayerId);
        Click(105, 105); // only inside layer 1
        Assert.Equal(1, session.SelectedLayerId);
        Click(310, 310); // a hidden layer is never hit
        Assert.Null(session.SelectedLayerId);
        Click(105, 105);
        Click(10, 500);  // empty canvas deselects
        Assert.Null(session.SelectedLayerId);
        Assert.Equal(0, session.UndoCount); // selection is never an undo step
    });

    [Fact] // tools-auto-select-switch (R8): import and a list pick set Select; a drawing commit and undo keep the tool
    public void The_tool_reverts_to_select_on_import_and_layer_pick_but_not_on_a_drawing_or_undo() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var tools = main.Home.Tools;
        tools.Set(Tool.Eraser);                                  // the Home tab and the canvas share one tool
        Assert.Equal(CanvasCursorKind.EraserRing, main.Canvas.Cursor.Kind);

        tools.Set(Tool.Pen);
        main.Canvas.DropCommand.Execute(new CanvasDrop([files.Image("a.png", 40, 40, SKColors.Red)], new Point(300, 200)));
        Assert.Equal(Tool.Select, tools.Current);                // addLayer (editor.js:523)

        tools.Set(Tool.Pen);
        main.Canvas.Interaction.PointerDown(new Point(50, 50), 1);
        main.Canvas.Interaction.PointerMove(new Point(80, 60), 1);
        main.Canvas.Interaction.PointerUp(new Point(80, 60), 1);
        Assert.Equal("Pen drawing", session.SelectedLayer!.Name);
        Assert.Equal(Tool.Pen, tools.Current);                   // addDrawingLayer has no setTool (editor.js:528-540)

        main.Layers.Selected = main.Layers.Rows.Single(r => r.Name == "a");
        Assert.Equal(Tool.Select, tools.Current);                // selectLayer (editor.js:577)

        tools.Set(Tool.Rect);
        Assert.True(session.Undo());
        Assert.Equal(Tool.Rect, tools.Current);                  // undo leaves the tool alone
    });
}
