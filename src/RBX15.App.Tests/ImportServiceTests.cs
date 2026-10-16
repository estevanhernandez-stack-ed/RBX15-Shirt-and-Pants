using System.IO;
using System.Windows;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// ImportService: every file of one pick or drop is one undo step, in the given order (spec §3;
/// layers-multi-file-async-order), and every failure is named on the status strip (diag-console-only-errors).
/// </summary>
public class ImportServiceTests
{
    [Fact] // 4.1: N files = N undo steps in decode order; v5: one step, given order, last on top and selected
    public void A_multi_file_import_is_one_undo_step_in_the_given_order()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        int added = new ImportService(session, status).Import([
            files.Image("first.png", 512, 512, SKColors.Red),
            files.Image("second.png", 100, 400, SKColors.Green),
            files.Image("third.png", 400, 100, SKColors.Blue),
        ]);
        Assert.Equal(3, added);
        Assert.Equal(new[] { "first", "second", "third" }, session.Document.Layers.Select(l => l.Name));
        Assert.Equal(new[] { 1, 2, 3 }, session.Document.Layers.Select(l => l.Id));
        Assert.Equal(3, session.SelectedLayerId);
        Assert.Equal(1, session.UndoCount);
        var l = session.Document.Layers[1];
        Assert.Equal((281.4, 83.6, 0.272), (l.X, l.Y, l.Scale)); // layers-add-placement, 4.1's doubles

        Assert.True(session.Undo());
        Assert.Empty(session.Document.Layers);
        Assert.Null(session.SelectedLayerId);
        Assert.Equal(1, status.Clears);
    }

    [Fact] // Review Focus: a mixed drop keeps the good files and names every bad one
    public void A_mixed_drop_adds_the_good_files_as_one_step_and_names_every_failure()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        int added = new ImportService(session, status).Import([
            files.Image("one.png", 8, 8, SKColors.Red),
            files.Write("logo.svg", [0x3C, 0x73, 0x76, 0x67, 0x3E]),
            files.Write("readme.txt", [0x68, 0x69]),
            files.Write("bomb.png", TestFiles.PngHeader(30000, 30000)),
            files.Image("two.webp", 8, 8, SKColors.Blue, SKEncodedImageFormat.Webp),
        ]);
        Assert.Equal(2, added);
        Assert.Equal(new[] { "one", "two" }, session.Document.Layers.Select(l => l.Name));
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(
            "Couldn't add 3 files. logo.svg: SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF. " +
            "readme.txt: Not a supported image type. Use PNG, JPEG, WebP, BMP or GIF. " +
            "bomb.png: The image is 30000 x 30000 pixels; the limit is 16384 pixels on a side.",
            Assert.Single(status.Shown));
    }

    [Fact]
    public void One_failure_reads_as_one_sentence() =>
        Assert.Equal("Couldn't add logo.svg: " + ImageImporter.SvgReason,
            ImportService.FailureMessage([new ImportFailure("logo.svg", ImageImporter.SvgReason)]));

    [Fact] // no file decoded: no layer, no undo step, nothing dirty
    public void A_pick_where_everything_fails_changes_nothing()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        Assert.Equal(0, new ImportService(session, status).Import([files.Write("a.svg", [1])]));
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
        Assert.Equal("Couldn't add a.svg: " + ImageImporter.SvgReason, status.Current);
    }

    [Fact] // a clean import clears a stale failure from the strip
    public void A_clean_import_after_a_failure_clears_the_status()
    {
        using var files = new TestFiles();
        var status = new StatusViewModel();
        var import = new ImportService(new EditorSession(), status);
        import.Import([files.Write("a.svg", [1])]);
        Assert.True(status.IsVisible);
        import.Import([files.Image("ok.png", 4, 4, SKColors.Red)]);
        Assert.False(status.IsVisible);
    }

    [Fact] // tools-auto-select-switch (R8): addLayer sets Select (editor.js:523); one event per import that added a layer
    public void LayersAdded_is_raised_once_per_import_that_added_layers()
    {
        using var files = new TestFiles();
        var import = new ImportService(new EditorSession(), new FakeStatus());
        int raised = 0;
        import.LayersAdded += (_, _) => raised++;
        import.Import([files.Image("a.png", 4, 4, SKColors.Red), files.Image("b.png", 4, 4, SKColors.Blue)]);
        Assert.Equal(1, raised);
        import.Import([files.Write("bad.svg", [1])]);           // refused: nothing added
        import.Import([]);
        Assert.Equal(1, raised);
    }

    [Fact] // drop-canvas: the canvas drop handler centers every file on the drop point
    public void A_canvas_drop_centers_every_layer_on_the_drop_point()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var import = new ImportService(session, new FakeStatus());
        var canvas = new CanvasViewModel(session, new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus()) { DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y)) };
        canvas.DropCommand.Execute(new CanvasDrop([files.Image("a.png", 512, 512, SKColors.Red), files.Image("b.png", 33, 77, SKColors.Blue)], new Point(300, 200)));
        Assert.Equal(1, session.UndoCount);
        foreach (var l in session.Document.Layers)
        {
            Assert.Equal(300, l.X + l.Image.Width * l.Scale / 2, 9);
            Assert.Equal(200, l.Y + l.Image.Height * l.Scale / 2, 9);
        }
        Assert.Equal((245.6, 145.6), (session.Document.Layers[0].X, session.Document.Layers[0].Y));
    }

    [Fact] // bug-pants-mode-region-keys: 4.1 threw here; v5 lands on waist_front
    public void Pants_mode_import_lands_on_the_front_slot()
    {
        using var files = new TestFiles();
        var session = new EditorSession(new Document { Mode = TemplateMode.Pants });
        new ImportService(session, new FakeStatus()).Import([files.Image("p.png", 512, 512, SKColors.Red)]);
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal((240.6, 83.6, 0.2125), (l.X, l.Y, l.Scale));
    }

    [Fact]
    public void An_empty_pick_does_nothing()
    {
        var session = new EditorSession();
        var status = new FakeStatus();
        Assert.Equal(0, new ImportService(session, status).Import([]));
        Assert.Equal((0, 0, 0), (session.UndoCount, status.Shown.Count, status.Clears));
    }

    [Fact] // ids only grow, so an undone layer's id is never reused (editor.js:1465)
    public void Ids_keep_growing_across_undo()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var import = new ImportService(session, new FakeStatus());
        import.Import([files.Image("a.png", 4, 4, SKColors.Red)]);
        session.Undo();
        import.Import([files.Image("b.png", 4, 4, SKColors.Red)]);
        Assert.Equal(2, Assert.Single(session.Document.Layers).Id);
    }
}
