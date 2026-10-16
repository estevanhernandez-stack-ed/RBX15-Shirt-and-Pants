using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Drag reorder through the layers view-model (handleLayerDragStart/Over/Drop and cleanupLayerDrag,
/// editor.js:584-666; ledger layers-reorder-drag, layers-reorder-drag-2, undo-empty-steps). Row thumbnails
/// are WPF bitmaps, so every test runs on the STA thread.
/// </summary>
public class LayersReorderTests
{
    static SKImage Img()
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    /// <summary>Layers 1..n named A, B, C... (1 at the bottom), the top one selected, in one undo step.</summary>
    static EditorSession Seeded(int count)
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            for (int i = 0; i < count; i++) s = LayerOps.AddImage(s, Img(), session.TakeId(), ((char)('A' + i)).ToString());
            return s;
        });
        return session;
    }

    static LayersViewModel Vm(EditorSession session) => new(session, new ImportService(session, new FakeStatus()), new FakeFileDialogs());

    /// <summary>Document.Layers ids, bottom first, as one string ("1234").</summary>
    static string Order(EditorSession session) => string.Concat(session.Document.Layers.Select(l => l.Id));

    static LayerRowViewModel Row(LayersViewModel vm, int id) => vm.Rows.Single(r => r.Id == id);

    static void AssertNoDragState(LayersViewModel vm)
    {
        Assert.All(vm.Rows, r => Assert.False(r.IsDragSource));
        Assert.All(vm.Rows, r => Assert.Equal(DropIndicator.None, r.DropIndicator));
    }

    // The expected orders are 4.1's own: node ran handleLayerDrop's array code (editor.js:634-652, the
    // findIndex/splice lines verbatim) on ids [1,2,3,4] for every (drag, target, half) and printed the ids.
    // above = layerDragPos 'top' (inserted after the target, higher z); false = 'bottom'.
    [Theory]
    [InlineData(1, 2, true, "2134")]
    [InlineData(1, 2, false, "1234")]
    [InlineData(1, 3, true, "2314")]
    [InlineData(1, 3, false, "2134")]
    [InlineData(1, 4, true, "2341")]
    [InlineData(1, 4, false, "2314")]
    [InlineData(2, 1, true, "1234")]
    [InlineData(2, 1, false, "2134")]
    [InlineData(2, 3, true, "1324")]
    [InlineData(2, 3, false, "1234")]
    [InlineData(2, 4, true, "1342")]
    [InlineData(2, 4, false, "1324")]
    [InlineData(3, 1, true, "1324")]
    [InlineData(3, 1, false, "3124")]
    [InlineData(3, 2, true, "1234")]
    [InlineData(3, 2, false, "1324")]
    [InlineData(3, 4, true, "1243")]
    [InlineData(3, 4, false, "1234")]
    [InlineData(4, 1, true, "1423")]
    [InlineData(4, 1, false, "4123")]
    [InlineData(4, 2, true, "1243")]
    [InlineData(4, 2, false, "1423")]
    [InlineData(4, 3, true, "1234")]
    [InlineData(4, 3, false, "1243")]
    public void A_drop_reorders_as_4_1s_splice_in_one_step_or_none(int drag, int target, bool above, string expected) => StaThread.Run(() =>
    {
        var session = Seeded(4);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.BeginRowDrag(drag);
        vm.DragOverRow(target, above);
        bool changed = vm.CompleteRowDrop();
        Assert.Equal(expected, Order(session));
        Assert.Equal(expected != "1234", changed);
        Assert.Equal(steps + (changed ? 1 : 0), session.UndoCount);   // 4.1 pushed a step even for "1234" (undo-empty-steps fix)
        Assert.Equal(4, session.SelectedLayerId);                      // selection unchanged by reorder
        Assert.Equal(expected.Reverse().Select(c => c - '0'), vm.Rows.Select(r => r.Id));   // rows top first
        AssertNoDragState(vm);
    });

    [Fact] // layers-reorder-drag-2 parityCheck: the bottom layer onto the top half of the top row is the new top; one Ctrl+Z restores
    public void Bottom_onto_the_top_rows_upper_half_becomes_the_top_and_one_undo_restores() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: true);
        Assert.True(vm.CompleteRowDrop());
        Assert.Equal(new[] { "A", "C", "B" }, vm.Rows.Select(r => r.Name));
        Assert.True(session.Undo());
        Assert.Equal(new[] { "C", "B", "A" }, vm.Rows.Select(r => r.Name));
    });

    [Fact] // .dragging-layer on the source; one live .drag-over-top/-bottom at a time (editor.js:595-617)
    public void The_dragged_row_is_marked_and_only_the_live_target_shows_its_edge() => StaThread.Run(() =>
    {
        var vm = Vm(Seeded(3));
        vm.BeginRowDrag(1);
        Assert.True(Row(vm, 1).IsDragSource);
        vm.DragOverRow(3, above: true);
        Assert.Equal(DropIndicator.Above, Row(vm, 3).DropIndicator);
        vm.DragOverRow(3, above: false);
        Assert.Equal(DropIndicator.Below, Row(vm, 3).DropIndicator);
        vm.DragOverRow(2, above: true);
        Assert.Equal(DropIndicator.None, Row(vm, 3).DropIndicator);
        Assert.Equal(DropIndicator.Above, Row(vm, 2).DropIndicator);
        vm.EndRowDrag();
        AssertNoDragState(vm);
    });

    [Fact] // layers-reorder-drag fix: 4.1 kept layerDragOverId while over the dragged row (editor.js:605), so a drop there moved it
    public void Hovering_the_dragged_row_after_another_clears_the_stale_target() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: true);
        vm.DragOverRow(1, above: true);
        Assert.Equal(DropIndicator.None, Row(vm, 3).DropIndicator);
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal("123", Order(session));
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // layers-reorder-drag fix: off every row there is no target, so the release drops nothing
    public void A_drop_after_the_pointer_left_every_row_does_nothing() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.BeginRowDrag(1);
        vm.DragOverRow(2, above: true);
        vm.ClearDropTarget();
        Assert.Equal(DropIndicator.None, Row(vm, 2).DropIndicator);
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // handleLayerDrop's guard (editor.js:625-628): no drag or a missing id does nothing
    public void Without_a_drag_or_with_a_missing_id_nothing_happens() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.DragOverRow(2, above: true);
        Assert.Equal(DropIndicator.None, Row(vm, 2).DropIndicator);
        Assert.False(vm.CompleteRowDrop());
        vm.BeginRowDrag(99);
        vm.DragOverRow(2, above: true);
        Assert.False(vm.CompleteRowDrop());
        vm.BeginRowDrag(1);
        vm.DragOverRow(99, above: true);
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // an undo mid-drag that removes the dragged layer ends the drag; the release drops nothing and throws nothing
    public void An_undo_that_removes_the_dragged_layer_ends_the_drag() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(), session.TakeId(), "New"));   // id 3, its own step
        var vm = Vm(session);
        vm.BeginRowDrag(3);
        vm.DragOverRow(1, above: false);
        Assert.True(session.Undo());
        int steps = session.UndoCount;
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal("12", Order(session));
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // the same for the target: an undo that removes it leaves no target
    public void An_undo_that_removes_the_target_leaves_no_target() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(), session.TakeId(), "New"));
        var vm = Vm(session);
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: true);
        Assert.True(session.Undo());
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal("12", Order(session));
        AssertNoDragState(vm);
    });

    [Fact] // editor.html:317, verbatim
    public void The_reorder_hint_is_4_1s_text()
    {
        var html = File.ReadAllText(Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "editor.html"));
        Assert.Contains("<h3>Layers <span style=\"font-size:10px;color:#555;text-transform:none\">(drag to reorder)</span></h3>", html);
        Assert.Equal("(drag to reorder)", StaThread.Run(() => Vm(new EditorSession()).ReorderHint));
    }
}
