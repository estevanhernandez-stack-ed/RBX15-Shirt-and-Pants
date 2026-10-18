using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// EditorSession: one undo step per document change (spec §5; ledger undo-empty-steps,
/// file-undo-snapshot-scope, undo-coverage-gaps, fillstyle-undo-snapshot-timing), selection that survives
/// undo (undo-restore-ui-side-effects), and an id counter that only grows (editor.js:499, :1465).
/// </summary>
public class EditorSessionTests
{
    static readonly SKImage Pixel = MakePixel();

    static SKImage MakePixel()
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    static Layer L(int id) => new() { Id = id, Image = Pixel, Name = "Image " + id };

    static Document WithLayers(Document d, params int[] ids) => d with { Layers = d.Layers.AddRange(ids.Select(L)) };

    /// <summary>Every Changed event's flags, in order.</summary>
    static List<SessionChange> Events(EditorSession s)
    {
        var seen = new List<SessionChange>();
        s.Changed += (_, e) => seen.Add(e.What);
        return seen;
    }

    // ---- defaults ----

    [Fact] // editor.js:76-107 (startup state), :86 (depth 30), :499 (nextId = 1); the background starts transparent (doc-new-transparent-background)
    public void A_new_session_is_4_1s_startup_state()
    {
        var s = new EditorSession();
        Assert.True(s.Document.ContentEquals(new Document() with { BgOpacity = 0 }));
        Assert.Equal(("#192e44", 0.0), (s.Document.BgColor, s.Document.BgOpacity));   // 4.1's color kept, at opacity 0
        Assert.Null(s.SelectedLayerId);
        Assert.Null(s.SelectedLayer);
        Assert.Equal(1, s.NextId);
        Assert.Null(s.CurrentFile);
        Assert.False(s.IsDirty);
        Assert.Equal("", s.TemplateTitle);
        Assert.False(s.CanUndo);
        Assert.False(s.CanRedo);
        Assert.False(s.InGesture);
        Assert.Equal((0, 0), (s.UndoCount, s.RedoCount));
    }

    [Fact] // the id counter starts above every id the starting document holds (editor.js:1465)
    public void A_starting_document_sets_the_id_counter_above_its_ids()
    {
        var s = new EditorSession(WithLayers(new Document(), 4, 9, 2));
        Assert.Equal(10, s.NextId);
        Assert.Equal(10, s.TakeId());
        Assert.Equal(11, s.NextId);
    }

    // ---- Apply ----

    [Fact] // one change, one step (spec §5)
    public void A_change_is_one_step_and_raises_document_and_history()
    {
        var s = new EditorSession();
        var seen = Events(s);

        Assert.True(s.Apply(d => d with { BgColor = "#000000" }));

        Assert.Equal("#000000", s.Document.BgColor);
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.IsDirty);
        Assert.Equal([SessionChange.Document | SessionChange.History], seen);
    }

    [Fact] // undo-empty-steps: a no-op pushes nothing (4.1 pushed, editor.js:1601-1606)
    public void A_change_that_keeps_the_content_records_nothing_and_raises_nothing()
    {
        var s = new EditorSession();
        var seen = Events(s);

        Assert.False(s.Apply(d => d with { BgColor = "#192e44" }));
        Assert.False(s.Apply(d => d));
        Assert.False(s.Apply(d => d with { Layers = d.Layers.Clear() }));

        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Empty(seen);
    }

    [Fact] // undo-empty-steps: 4.1's presets pushed two snapshots (editor.js:1734-1749); one lambda is one step
    public void Several_edits_in_one_change_are_one_step()
    {
        var s = new EditorSession();
        s.Apply(d => WithLayers(d, 1, 2, 3) with { ShowGrid = true, BgOpacity = 0.5 });
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.Undo());
        Assert.True(s.Document.ContentEquals(EditorSession.NewDocument()));
    }

    [Theory] // file-undo-snapshot-scope: every Document field is a step, including Mask and Grid (4.1 never pushed for them, editor.js:1494-1495)
    [InlineData("mask")]
    [InlineData("grid")]
    [InlineData("mode")]
    [InlineData("fill")]
    [InlineData("visibility")]
    public void Every_document_edit_is_undoable(string edit)
    {
        var s = new EditorSession(WithLayers(new Document(), 1));
        var start = s.Document;
        s.Apply(edit switch
        {
            "mask" => d => d with { ShowMask = false },
            "grid" => d => d with { ShowGrid = true },
            "mode" => d => d with { Mode = TemplateMode.Pants },
            "fill" => d => d with { RegionFills = d.RegionFills.Add(("torso_front", Fill.Solid("#ff8800"))) },
            "visibility" => d => d with { Layers = [d.Layers[0] with { Visible = false }] },
            _ => throw new ArgumentOutOfRangeException(nameof(edit)),
        });
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.Undo());
        Assert.True(s.Document.ContentEquals(start));
    }

    [Fact] // the change runs before anything is touched
    public void A_change_that_throws_changes_nothing()
    {
        var s = new EditorSession();
        var before = s.Document;
        var seen = Events(s);

        Assert.Throws<ArgumentException>(() => s.Apply(_ => throw new ArgumentException("bad region")));
        Assert.Throws<InvalidOperationException>(() => s.Apply(_ => null!));

        Assert.Same(before, s.Document);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Empty(seen);
    }

    [Fact] // a document change that removes the selected layer drops the selection
    public void Removing_the_selected_layer_drops_the_selection()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        s.Select(2);
        var seen = Events(s);

        s.Apply(d => d with { Layers = d.Layers.RemoveAll(l => l.Id == 2) });

        Assert.Null(s.SelectedLayerId);
        Assert.Equal([SessionChange.Document | SessionChange.History | SessionChange.Selection], seen);
    }

    [Fact] // observers see the finished state when Changed fires
    public void Changed_fires_after_the_state_is_updated()
    {
        var s = new EditorSession();
        (string Bg, int Undo, bool Dirty)? observed = null;
        s.Changed += (_, _) => observed = (s.Document.BgColor, s.UndoCount, s.IsDirty);
        s.Apply(d => d with { BgColor = "#123456" });
        Assert.Equal(("#123456", 1, true), observed);
    }

    // ---- ApplyWithSelection ----

    [Fact] // addLayer pushes, adds on top and selects (editor.js:500-526)
    public void Adding_and_selecting_is_one_step_with_a_selection_change()
    {
        var s = new EditorSession();
        var seen = Events(s);

        bool step = s.ApplyWithSelection(snap =>
        {
            int id = s.TakeId();
            return new EditorSnapshot(WithLayers(snap.Document, id), id);
        });

        Assert.True(step);
        Assert.Equal(1, s.SelectedLayerId);
        Assert.Equal("Image 1", s.SelectedLayer!.Name);
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(2, s.NextId);
        Assert.Equal([SessionChange.Document | SessionChange.History | SessionChange.Selection], seen);
    }

    [Fact] // the selection is adopted even when the content is unchanged; no step
    public void A_selection_only_result_raises_selection_and_records_nothing()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        var seen = Events(s);

        Assert.False(s.ApplyWithSelection(snap => snap with { SelectedLayerId = 2 }));

        Assert.Equal(2, s.SelectedLayerId);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.Selection], seen);
    }

    [Fact] // an id the returned document lacks selects nothing
    public void A_selection_the_document_lacks_becomes_null()
    {
        var s = new EditorSession(WithLayers(new Document(), 1));
        s.Select(1);
        s.ApplyWithSelection(snap => new EditorSnapshot(snap.Document with { ShowGrid = true }, 99));
        Assert.Null(s.SelectedLayerId);
    }

    // ---- Select ----

    [Fact] // selectLayer (editor.js:575) is never an undo step
    public void Select_changes_only_the_selection()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        var seen = Events(s);

        s.Select(2);
        s.Select(2);
        s.Select(7);
        s.Select(null);

        Assert.Null(s.SelectedLayerId);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.Selection, SessionChange.Selection], seen);
    }

    // ---- Undo and Redo ----

    [Fact] // btnUndo/btnRedo (editor.js:1477-1486); nothing to do returns false and raises nothing
    public void Undo_and_redo_walk_the_steps()
    {
        var s = new EditorSession();
        s.Apply(d => d with { BgColor = "#000001" });
        s.Apply(d => d with { BgColor = "#000002" });
        var seen = Events(s);

        Assert.True(s.Undo());
        Assert.Equal("#000001", s.Document.BgColor);
        Assert.True(s.Undo());
        Assert.Equal("#192e44", s.Document.BgColor);
        Assert.False(s.Undo());
        Assert.Equal((0, 2), (s.UndoCount, s.RedoCount));
        Assert.True(s.Redo());
        Assert.True(s.Redo());
        Assert.False(s.Redo());
        Assert.Equal("#000002", s.Document.BgColor);
        Assert.Equal(4, seen.Count);
        Assert.All(seen, w => Assert.Equal(SessionChange.Document | SessionChange.History, w));
    }

    [Fact] // a new edit after undo abandons the redo branch (lib/history.js:33-36)
    public void A_new_edit_drops_redo()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        s.Undo();
        Assert.True(s.CanRedo);
        s.Apply(d => d with { ShowMask = false });
        Assert.False(s.CanRedo);
    }

    [Fact] // undo-restore-ui-side-effects (fix): the selection survives when its layer still exists
    public void Selection_survives_undo_when_the_layer_exists()
    {
        var s = new EditorSession(WithLayers(new Document(), 1));
        s.Select(1);
        s.Apply(d => d with { ShowGrid = true });

        Assert.True(s.Undo());
        Assert.Equal(1, s.SelectedLayerId);
        Assert.True(s.Redo());
        Assert.Equal(1, s.SelectedLayerId);
    }

    [Fact] // undoing an add removes the layer, so its selection goes too
    public void Undoing_an_add_drops_the_selection_of_the_removed_layer()
    {
        var s = new EditorSession();
        s.ApplyWithSelection(snap => { int id = s.TakeId(); return new EditorSnapshot(WithLayers(snap.Document, id), id); });
        var seen = Events(s);

        Assert.True(s.Undo());

        Assert.Empty(s.Document.Layers);
        Assert.Null(s.SelectedLayerId);
        Assert.Equal([SessionChange.Document | SessionChange.History | SessionChange.Selection], seen);
    }

    [Fact] // nextId only grows (editor.js:1465): an undone add never gives its id back
    public void Undo_never_lowers_the_id_counter()
    {
        var s = new EditorSession();
        s.ApplyWithSelection(snap => { int id = s.TakeId(); return new EditorSnapshot(WithLayers(snap.Document, id), id); });
        s.Undo();
        Assert.Equal(2, s.NextId);
        s.Redo();
        Assert.Equal(2, s.NextId);
        s.Apply(d => WithLayers(d, 40));
        Assert.Equal(41, s.NextId);
    }

    [Fact] // depth 30 (editor.js:86)
    public void Undo_depth_is_thirty()
    {
        var s = new EditorSession();
        for (int i = 1; i <= 31; i++) { int n = i; s.Apply(d => d with { BgOpacity = n / 100.0 }); }
        Assert.Equal(30, s.UndoCount);
        while (s.Undo()) { }
        Assert.Equal(0.01, s.Document.BgOpacity);
    }

    // ---- TemplateTitle ----

    [Fact] // #exportTitle (editor.html:100) is not document state: no step, not dirty (file-undo-snapshot-scope)
    public void The_template_title_raises_title_and_is_never_a_step()
    {
        var s = new EditorSession();
        var seen = Events(s);

        s.TemplateTitle = "My Shirt";
        s.TemplateTitle = "My Shirt";
        s.TemplateTitle = null!;

        Assert.Equal("", s.TemplateTitle);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.Title, SessionChange.Title], seen);
    }

    // ---- gestures (fillstyle-undo-snapshot-timing) ----

    [Fact] // one slider drag is one step, recorded when the drag ends
    public void A_gesture_is_one_step_recorded_when_it_closes()
    {
        var s = new EditorSession();
        var seen = Events(s);

        using (s.BeginGesture())
        {
            Assert.True(s.InGesture);
            for (int i = 1; i <= 5; i++) { int n = i; Assert.True(s.Apply(d => d with { BgOpacity = n / 10.0 })); }
            Assert.Equal(0, s.UndoCount);
            Assert.True(s.IsDirty);                 // M4a R2: IsDirty is the live content against the saved marker
            Assert.True(s.CanUndo);
        }

        Assert.False(s.InGesture);
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.IsDirty);
        Assert.Equal(Enumerable.Repeat(SessionChange.Document, 5).Append(SessionChange.History), seen);
        Assert.True(s.Undo());
        Assert.Equal(0, s.Document.BgOpacity);
    }

    [Fact] // a drag that ends where it started records nothing (editor.js:1570-1579 recorded nothing for an idle drag)
    public void A_gesture_that_returns_to_its_start_records_nothing()
    {
        var s = new EditorSession();
        using (s.BeginGesture())
        {
            s.Apply(d => d with { BgOpacity = 0.3 });
            s.Apply(d => d with { BgOpacity = 0 });
        }
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
    }

    [Fact] // scopes nest by count; disposing one twice does nothing
    public void Nested_gestures_record_once_at_the_outermost_close()
    {
        var s = new EditorSession();
        var outer = s.BeginGesture();
        var inner = s.BeginGesture();
        s.Apply(d => d with { ShowGrid = true });
        inner.Dispose();
        inner.Dispose();
        Assert.True(s.InGesture);
        Assert.Equal(0, s.UndoCount);
        s.Apply(d => d with { ShowMask = false });
        outer.Dispose();
        outer.Dispose();
        Assert.False(s.InGesture);
        Assert.Equal(1, s.UndoCount);
        s.Undo();
        Assert.True(s.Document.ContentEquals(EditorSession.NewDocument()));
    }

    [Fact] // Review Focus: Ctrl+Z in the middle of a slider drag
    public void Undo_during_a_gesture_commits_it_undoes_it_and_the_drag_goes_on_as_a_new_step()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        var drag = s.BeginGesture();
        s.Apply(d => d with { BgOpacity = 0.2 });
        s.Apply(d => d with { BgOpacity = 0.4 });

        Assert.True(s.Undo());                       // commits the drag so far, then undoes it
        Assert.Equal(0, s.Document.BgOpacity);
        Assert.True(s.Document.ShowGrid);
        Assert.Equal((1, 1), (s.UndoCount, s.RedoCount));
        Assert.True(s.InGesture);

        s.Apply(d => d with { BgOpacity = 0.6 });     // the thumb is still held
        s.Apply(d => d with { BgOpacity = 0.8 });
        Assert.Equal(1, s.UndoCount);
        drag.Dispose();

        Assert.Equal((2, 0), (s.UndoCount, s.RedoCount));
        Assert.True(s.Undo());
        Assert.Equal(0, s.Document.BgOpacity);
        Assert.True(s.Undo());
        Assert.False(s.Document.ShowGrid);
    }

    [Fact] // a gesture that changed content is a new edit, so redo after it has nothing to redo
    public void Redo_during_a_changed_gesture_commits_it_and_finds_no_redo()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        s.Undo();
        using (s.BeginGesture())
        {
            s.Apply(d => d with { BgOpacity = 0.5 });
            Assert.False(s.Redo());
            Assert.Equal((1, 0), (s.UndoCount, s.RedoCount));
        }
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(0.5, s.Document.BgOpacity);
    }

    [Fact] // M3b R7 (critic MJ1): Changing comes first from Apply, ApplyWithSelection, BeginGesture, Undo and Redo, never from Select or the title
    public void Changing_is_raised_first_by_every_entry_point()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        var order = new List<string>();
        Document? atChanging = null;
        s.Changing += (_, _) => { order.Add("Changing"); atChanging = s.Document; };
        s.Changed += (_, _) => order.Add("Changed");

        var before = s.Document;
        Assert.True(s.Apply(d => d with { Layers = d.Layers.RemoveAt(1) }));
        Assert.Same(before, atChanging);   // raised before the change landed
        Assert.Equal(new[] { "Changing", "Changed" }, order);

        order.Clear();
        s.ApplyWithSelection(snap => snap with { SelectedLayerId = 1 });
        using (s.BeginGesture()) { }
        s.Undo();
        s.Redo();
        Assert.Equal(4, order.Count(o => o == "Changing"));

        s.Select(null);
        s.TemplateTitle = "shirt";
        Assert.Equal(4, order.Count(o => o == "Changing"));   // selection and title are not document changes
    }

    // ---- M4a: the saved-state marker, MarkSaved, CommitPendingEdits and Open (interfaces R2, R11) ----

    static ProjectLoad Load(Document doc, int nextId) => new(doc, nextId, []);

    static SKImage Img(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    [Fact] // R2 (Este, 2026-10-16): undo back to the saved state reads clean; redo away from it reads dirty
    public void Undo_back_to_the_saved_state_reads_clean()
    {
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { BgColor = "#000001" }));
        Assert.True(s.IsDirty);
        Assert.True(s.Undo());
        Assert.False(s.IsDirty);                              // back at the starting document
        Assert.True(s.Redo());
        Assert.True(s.IsDirty);

        s.MarkSaved(@"C:\p\a.r15proj", s.Document);
        Assert.False(s.IsDirty);
        Assert.True(s.Apply(d => d with { BgColor = "#000002" }));
        Assert.True(s.IsDirty);
        Assert.True(s.Undo());
        Assert.False(s.IsDirty);                              // back at the save, though history still holds a step
        Assert.True(s.Undo());
        Assert.True(s.IsDirty);                               // before the save
    }

    [Fact] // R2: "differs from the last save": an edit made by hand back to the saved values reads clean
    public void An_edit_back_to_the_saved_values_reads_clean()
    {
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        Assert.True(s.Apply(d => d with { ShowGrid = false }));
        Assert.Equal(2, s.UndoCount);
        Assert.False(s.IsDirty);
    }

    [Fact] // MarkSaved sets the file and the marker and raises File alone; IsDirty flips with no Document change
    public void MarkSaved_sets_the_file_and_raises_only_File()
    {
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        var seen = Events(s);
        s.MarkSaved(@"C:\p\shirt.r15proj", s.Document);
        Assert.Equal(@"C:\p\shirt.r15proj", s.CurrentFile);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.File], seen);
        Assert.Equal(1, s.UndoCount);                         // saving is not an undo step and keeps history
        Assert.Throws<ArgumentException>(() => s.MarkSaved("", s.Document));
        Assert.Throws<ArgumentNullException>(() => s.MarkSaved(null!, s.Document));
        Assert.Throws<ArgumentNullException>(() => s.MarkSaved("x.r15proj", null!));
    }

    [Fact] // M3b carry-forward: Save raises Changing first, so the canvas closes a wheel burst or a select drag; nothing else happens
    public void CommitPendingEdits_raises_Changing_and_nothing_else()
    {
        var s = new EditorSession();
        int changing = 0;
        s.Changing += (_, _) => changing++;
        var seen = Events(s);
        s.CommitPendingEdits();
        Assert.Equal(1, changing);
        Assert.Empty(seen);
    }

    [Fact] // project-load-flow, project-load-history-interplay (fix): Open swaps everything at once, clears history, reads clean
    public void Open_swaps_the_document_clears_history_and_selection_and_reads_clean()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        s.Select(2);
        s.TemplateTitle = "Hoodie";
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        Assert.True(s.Undo());
        Assert.Equal((0, 1), (s.UndoCount, s.RedoCount));
        var order = new List<string>();
        s.Changing += (_, _) => order.Add("Changing");
        s.Changed += (_, e) => order.Add(e.What.ToString());
        var loaded = WithLayers(new Document { Mode = TemplateMode.Pants }, 7);

        s.Open(Load(loaded, 8), @"C:\p\pants.r15proj");

        Assert.Same(loaded, s.Document);
        Assert.Equal(loaded.Layers[^1].Id, s.SelectedLayerId); // T1-R2: top layer selected, editor.js:2664
        Assert.Equal(8, s.NextId);
        Assert.Equal((0, 0), (s.UndoCount, s.RedoCount));
        Assert.False(s.CanUndo);
        Assert.Equal(@"C:\p\pants.r15proj", s.CurrentFile);
        Assert.False(s.IsDirty);
        Assert.Equal("Hoodie", s.TemplateTitle);              // R1: the template name is not part of the file
        Assert.Equal(new[] { "Changing", "Document, Selection, History, File, Replaced" }, order);
        Assert.Throws<ArgumentNullException>(() => s.Open(null!, "x"));
        Assert.Throws<ArgumentException>(() => s.Open(Load(loaded, 8), ""));
    }

    [Fact] // the id counter comes from the load, never below the loaded ids
    public void Open_takes_the_loads_next_id_but_never_below_its_layers()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2, 3, 4, 5));
        s.Open(Load(WithLayers(new Document(), 2), 3), "a.r15proj");
        Assert.Equal(3, s.NextId);                            // lower than before: history is gone, so no id can come back
        s.Open(Load(WithLayers(new Document(), 9), 4), "b.r15proj");
        Assert.Equal(10, s.NextId);
    }

    [Fact] // R11: the outgoing images are the old document's, the saved marker's, history's and a gesture start's, minus the new document's
    public void Open_returns_every_outgoing_image_except_those_the_new_document_holds()
    {
        SKImage a = Img(SKColors.Red), b = Img(SKColors.Green), c = Img(SKColors.Blue), bg = Img(SKColors.White), kept = Img(SKColors.Black), fresh = Img(SKColors.Gray);
        var start = new Document { BgImage = bg, Layers = [new Layer { Id = 1, Image = a }, new Layer { Id = 2, Image = kept }] };
        var s = new EditorSession(start);                     // the saved marker holds a, kept and bg
        Assert.True(s.Apply(d => d with { BgImage = null, Layers = [new Layer { Id = 3, Image = b }, new Layer { Id = 2, Image = kept }] }));
        var gesture = s.BeginGesture();
        Assert.True(s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = 4, Image = c }) }));   // c lives only in the live document

        var released = s.Open(Load(new Document { Layers = [new Layer { Id = 9, Image = kept }, new Layer { Id = 10, Image = fresh }] }, 11), "x.r15proj");

        Assert.Equal(new HashSet<SKImage>([a, b, c, bg]), new HashSet<SKImage>(released));
        Assert.Equal(4, released.Count);                      // each once, though a and kept sit in several places
        gesture.Dispose();
    }

    [Fact] // Open abandons an open gesture: its stale Dispose records nothing, and a new gesture works as usual
    public void A_gesture_open_across_an_Open_is_abandoned_and_its_stale_dispose_does_nothing()
    {
        var s = new EditorSession();
        var stale = s.BeginGesture();
        Assert.True(s.Apply(d => d with { BgOpacity = 0.5 }));
        s.Open(Load(new Document(), 1), "x.r15proj");
        Assert.False(s.InGesture);
        Assert.False(s.CanUndo);
        var seen = Events(s);
        stale.Dispose();
        Assert.Equal((0, false), (s.UndoCount, s.InGesture));
        Assert.Empty(seen);

        using (s.BeginGesture())
        {
            Assert.True(s.Apply(d => d with { BgOpacity = 0.25 }));
            stale.Dispose();                                  // still a no-op, even with a new gesture open
            Assert.True(s.InGesture);
        }
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.IsDirty);
    }

    [Fact] // an unchanged gesture leaves redo alone
    public void Redo_during_an_unchanged_gesture_still_redoes()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        s.Undo();
        using (s.BeginGesture())
            Assert.True(s.Redo());
        Assert.True(s.Document.ShowGrid);
        Assert.Equal((1, 0), (s.UndoCount, s.RedoCount));
    }
}
