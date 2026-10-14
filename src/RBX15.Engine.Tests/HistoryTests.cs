using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// History is a port of lib/history.js. The first seven facts are tests/history.test.js case for case;
/// the 4.1 strings become snapshots labeled through BgColor, which History never inspects.
/// </summary>
public class HistoryTests
{
    static EditorSnapshot Snap(string label, int? selected = null) => new(new Document { BgColor = label }, selected);

    static string? Label(EditorSnapshot? s) => s?.Document.BgColor;

    static Layer L(int id) => new() { Id = id, Image = Px.Solid(2, 2, SKColors.Red), Name = $"Layer {id}" };

    // ---- tests/history.test.js, case for case ----

    [Fact] // tests/history.test.js:10-14
    public void Undo_returns_the_recorded_snapshot()
    {
        var h = new History();
        h.Record(Snap("A"));
        Assert.Equal("A", Label(h.Undo(Snap("B"))));
    }

    [Fact] // tests/history.test.js:16-19
    public void Undo_on_an_empty_stack_returns_null()
    {
        var h = new History();
        Assert.Null(h.Undo(Snap("current")));
    }

    [Fact] // tests/history.test.js:21-27
    public void Redo_brings_back_the_state_undo_parked()
    {
        var h = new History();
        h.Record(Snap("A"));
        Assert.Equal("A", Label(h.Undo(Snap("B"))));
        Assert.Equal("B", Label(h.Redo(Snap("A"))));
    }

    [Fact] // tests/history.test.js:29-37
    public void A_new_record_clears_the_redo_stack()
    {
        var h = new History();
        h.Record(Snap("A"));
        h.Undo(Snap("B"));
        Assert.True(h.CanRedo);
        h.Record(Snap("A2"));
        Assert.False(h.CanRedo);
        Assert.Null(h.Redo(Snap("X")));
    }

    [Fact] // tests/history.test.js:39-47
    public void Multi_step_undo_and_redo_preserve_order()
    {
        var h = new History();
        h.Record(Snap("s0"));
        h.Record(Snap("s1"));
        h.Record(Snap("s2"));
        Assert.Equal("s2", Label(h.Undo(Snap("s3"))));
        Assert.Equal("s1", Label(h.Undo(Snap("s2"))));
        Assert.Equal("s2", Label(h.Redo(Snap("s1"))));
        Assert.Equal("s3", Label(h.Redo(Snap("s2"))));
        Assert.False(h.CanRedo);
    }

    [Fact] // tests/history.test.js:49-57
    public void Respects_the_cap_evicting_the_oldest()
    {
        var h = new History(3);
        foreach (var s in new[] { "a", "b", "c", "d", "e" }) h.Record(Snap(s));
        Assert.Equal((3, 0), (h.UndoCount, h.RedoCount));
        Assert.Equal("e", Label(h.Undo(Snap("live"))));
        Assert.Equal("d", Label(h.Undo(Snap("e"))));
        Assert.Equal("c", Label(h.Undo(Snap("d"))));
        Assert.Null(h.Undo(Snap("c")));
    }

    [Fact] // tests/history.test.js:59-68
    public void CanUndo_and_CanRedo_reflect_stack_state()
    {
        var h = new History();
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
        h.Record(Snap("A"));
        Assert.True(h.CanUndo);
        h.Undo(Snap("B"));
        Assert.False(h.CanUndo);
        Assert.True(h.CanRedo);
    }

    // ---- ledger checks and approved divergences ----

    [Fact] // undo-model, history-snapshot-memory: editor.js:86 passes 30; lib/history.js:35 evicts the oldest
    public void Default_depth_is_30_and_the_31st_record_evicts_the_first()
    {
        var h = new History();
        Assert.Equal(30, h.Capacity);
        for (int i = 0; i < 35; i++) h.Record(Snap($"s{i}"));
        Assert.Equal(30, h.UndoCount);
        string? last = null;
        for (int i = 0; i < 30; i++) last = Label(h.Undo(Snap("live")));
        Assert.Equal("s5", last);
        Assert.Null(h.Undo(Snap("live")));
    }

    [Fact] // history-redo-stack-uncapped: the ledger's check sequence (30 records, 5 undos, 5 redos, 1 record)
    public void Redo_is_capped_and_undo_depth_stays_30()
    {
        var h = new History();
        for (int i = 0; i < 30; i++) h.Record(Snap($"s{i}"));
        for (int i = 0; i < 5; i++) h.Undo(Snap("live"));
        Assert.Equal((25, 5), (h.UndoCount, h.RedoCount));
        for (int i = 0; i < 5; i++) h.Redo(Snap("live"));
        Assert.Equal((30, 0), (h.UndoCount, h.RedoCount));
        h.Record(Snap("s30"));
        Assert.Equal((30, 0), (h.UndoCount, h.RedoCount));
    }

    [Fact] // the invariant behind the redo cap: one entry moves per step, Record leaves redo empty
    public void Neither_stack_ever_exceeds_capacity()
    {
        var h = new History(3);
        int n = 0;
        foreach (char op in "rrrruuuuuyyyyyruyryuuuyyyy")
        {
            var live = Snap($"x{n++}");
            if (op == 'r') h.Record(live);
            else if (op == 'u') h.Undo(live);
            else h.Redo(live);
            Assert.InRange(h.UndoCount + h.RedoCount, 0, 3);
        }
    }

    [Fact] // contract: Record pushes undo and clears redo (lib/history.js:33-36); empty redo parks nothing (:50)
    public void Record_after_undo_clears_redo_and_an_empty_redo_parks_nothing()
    {
        var h = new History();
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        h.Undo(Snap("C"));
        h.Undo(Snap("B"));
        Assert.Equal((0, 2), (h.UndoCount, h.RedoCount));
        h.Record(Snap("A"));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
        Assert.Null(h.Redo(Snap("A2")));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
    }

    [Fact] // lib/history.js:43: an empty undo returns before parking current
    public void Undo_on_empty_parks_nothing()
    {
        var h = new History();
        Assert.Null(h.Undo(Snap("live")));
        Assert.Equal((0, 0), (h.UndoCount, h.RedoCount));
        Assert.False(h.CanRedo);
    }

    [Fact] // undo-restore-ui-side-effects: 4.1 deselects on every restore (editor.js:1451); v5 keeps the selection
    public void Selection_survives_when_the_layer_exists_in_the_restored_document()
    {
        var doc = new Document { Layers = ImmutableList.Create(L(1), L(2)) };
        var h = new History();
        h.Record(new EditorSnapshot(doc, 2));
        var edited = doc with { BgColor = "#ff0000" };
        var undone = h.Undo(new EditorSnapshot(edited, 2))!;
        Assert.Same(doc, undone.Document);
        Assert.Equal(2, undone.SelectedLayerId);
        var redone = h.Redo(undone)!;
        Assert.Same(edited, redone.Document);
        Assert.Equal(2, redone.SelectedLayerId);
    }

    [Fact] // spec §5: the selection survives only "when the layer still exists"
    public void Selection_becomes_null_when_the_layer_is_gone()
    {
        var before = new Document { Layers = ImmutableList.Create(L(1)) };
        var after = before with { Layers = before.Layers.Add(L(2)) };
        var h = new History();
        h.Record(new EditorSnapshot(before, 1));
        var undone = h.Undo(new EditorSnapshot(after, 2))!;
        Assert.Same(before, undone.Document);
        Assert.Null(undone.SelectedLayerId);
        var redone = h.Redo(undone)!;
        Assert.Same(after, redone.Document);
        Assert.Null(redone.SelectedLayerId);
    }

    [Fact] // resolved contract ambiguity: the live selection survives; the recorded one does not override it
    public void The_live_selection_is_what_survives_not_the_recorded_one()
    {
        var doc = new Document { Layers = ImmutableList.Create(L(1), L(2)) };
        var h = new History();
        h.Record(new EditorSnapshot(doc, 1));
        h.Record(new EditorSnapshot(doc with { BgColor = "#000001" }, 1));
        Assert.Equal(2, h.Undo(new EditorSnapshot(doc with { BgColor = "#000002" }, 2))!.SelectedLayerId);
        Assert.Null(h.Undo(new EditorSnapshot(doc with { BgColor = "#000001" }, null))!.SelectedLayerId);
    }

    [Fact] // history-snapshot-memory: no re-encoding (4.1 stores a data URL per layer per snapshot, editor.js:1413)
    public void Snapshots_share_documents_and_bitmaps_by_reference()
    {
        var l1 = L(1);
        var l2 = L(2);
        var before = new Document { Layers = ImmutableList.Create(l1) };
        var after = before with { Layers = before.Layers.Add(l2) };
        var h = new History();
        h.Record(new EditorSnapshot(before, 1));
        var undone = h.Undo(new EditorSnapshot(after, 2))!;
        Assert.Same(before, undone.Document);
        Assert.Same(l1.Image, undone.Document.Layers[0].Image);
        var redone = h.Redo(undone)!;
        Assert.Same(after, redone.Document);
        Assert.Same(l1.Image, redone.Document.Layers[0].Image);
        Assert.Same(l2.Image, redone.Document.Layers[1].Image);
    }

    [Fact] // undo-rapid-restore-race: restore is synchronous, so back-to-back undos cannot interleave
    public void Rapid_undo_then_redo_ends_with_every_layer()
    {
        var h = new History();
        var doc = new Document();
        var states = new List<Document> { doc };
        for (int id = 1; id <= 5; id++)
        {
            h.Record(new EditorSnapshot(doc, null));
            doc = doc with { Layers = doc.Layers.Add(L(id)) };
            states.Add(doc);
        }
        var live = new EditorSnapshot(doc, 5);
        for (int i = 0; i < 5; i++) live = h.Undo(live)!;
        Assert.Same(states[0], live.Document);
        for (int i = 0; i < 5; i++) live = h.Redo(live)!;
        Assert.Same(states[5], live.Document);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, live.Document.Layers.Select(l => l.Id));
        Assert.False(h.CanRedo);
    }

    [Fact] // tests-lib-history integration check; the mode switch clears fills like switchMode (editor.js:1711)
    public void Four_kinds_of_edit_undo_to_the_start_and_redo_to_the_end()
    {
        var s0 = new Document();
        var s1 = s0 with { Layers = s0.Layers.Add(L(1)) };
        var s2 = s1 with { BgColor = "#ff0000" };
        var s3 = s2 with { RegionFills = s2.RegionFills.Add(("torso_front", Fill.Solid("#00ff00"))) };
        var s4 = s3 with { Mode = TemplateMode.Pants, RegionFills = ImmutableList<(string, Fill)>.Empty };
        var docs = new[] { s0, s1, s2, s3, s4 };
        var h = new History();
        for (int i = 0; i < 4; i++) h.Record(new EditorSnapshot(docs[i], null));
        var live = new EditorSnapshot(s4, null);
        for (int i = 3; i >= 0; i--)
        {
            live = h.Undo(live)!;
            Assert.Same(docs[i], live.Document);
        }
        Assert.Equal(TemplateMode.Shirt, live.Document.Mode);
        Assert.Empty(live.Document.Layers);
        Assert.Equal("#192e44", live.Document.BgColor);
        for (int i = 1; i <= 4; i++)
        {
            live = h.Redo(live)!;
            Assert.Same(docs[i], live.Document);
        }
        Assert.Equal(TemplateMode.Pants, live.Document.Mode);
        Assert.Empty(live.Document.RegionFills);
    }

    [Fact] // Review Focus: the smallest legal history, one step back and one step forward
    public void Capacity_one_keeps_only_the_latest_step()
    {
        var h = new History(1);
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
        Assert.Equal("B", Label(h.Undo(Snap("C"))));
        Assert.Null(h.Undo(Snap("B")));
        Assert.Equal((0, 1), (h.UndoCount, h.RedoCount));
        Assert.Equal("C", Label(h.Redo(Snap("B"))));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
    }

    [Fact]
    public void Capacity_must_be_positive_and_snapshots_non_null()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new History(0));
        var h = new History();
        Assert.Throws<ArgumentNullException>(() => h.Record(null!));
        Assert.Throws<ArgumentNullException>(() => h.Undo(null!));
        Assert.Throws<ArgumentNullException>(() => h.Redo(null!));
    }

    [Fact] // spec §3, project-load-history-interplay: opening a project empties both stacks
    public void Clear_empties_both_stacks_and_history_keeps_working()
    {
        var h = new History();
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        h.Undo(Snap("C"));
        Assert.Equal((1, 1), (h.UndoCount, h.RedoCount));

        h.Clear();

        Assert.Equal((0, 0), (h.UndoCount, h.RedoCount));
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
        Assert.Null(h.Undo(Snap("opened")));
        Assert.Null(h.Redo(Snap("opened")));
        Assert.Equal((0, 0), (h.UndoCount, h.RedoCount));

        h.Record(Snap("opened"));
        Assert.Equal("opened", Label(h.Undo(Snap("edited"))));
    }
}
