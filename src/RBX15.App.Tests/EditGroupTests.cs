using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The Edit group (editor.html:77-84, editor.js:1394, :1470-1487; ledger home-edit-group, btn-clear, undo-buttons-shortcuts).</summary>
public class EditGroupTests
{
    [Fact] // undo-buttons-shortcuts parityCheck: launch both off, an edit turns undo on, undo turns redo on, a new edit turns redo off
    public void Undo_and_redo_enable_exactly_when_their_stack_has_a_step()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        int undoChanged = 0;
        edit.UndoCommand.CanExecuteChanged += (_, _) => undoChanged++;

        Assert.False(edit.UndoCommand.CanExecute(null));
        Assert.False(edit.RedoCommand.CanExecute(null));

        session.Apply(d => d with { ShowGrid = true });
        Assert.True(edit.UndoCommand.CanExecute(null));
        Assert.False(edit.RedoCommand.CanExecute(null));

        edit.UndoCommand.Execute(null);
        Assert.False(edit.UndoCommand.CanExecute(null));
        Assert.True(edit.RedoCommand.CanExecute(null));
        Assert.False(session.Document.ShowGrid);

        edit.RedoCommand.Execute(null);
        Assert.True(session.Document.ShowGrid);

        session.Undo();
        session.Apply(d => d with { ShowMask = false });
        Assert.False(edit.RedoCommand.CanExecute(null));
        Assert.True(undoChanged >= 4);
    }

    [Fact] // home-edit-group parityCheck: after 31 edits only 30 undos are available
    public void Undo_depth_is_30()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        for (int i = 0; i < 31; i++) session.Apply(d => d with { BgOpacity = i / 100.0 });

        int undos = 0;
        while (edit.UndoCommand.CanExecute(null)) { edit.UndoCommand.Execute(null); undos++; }
        Assert.Equal(30, undos);
    }

    [Fact] // btn-clear parityCheck: layers gone in one step, fills intact, undo restores the layers
    public void Clear_removes_all_layers_in_one_step_and_keeps_fills()
    {
        var session = SessionWith(LayerOf(1), LayerOf(2));
        session.Select(2);
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#17d4fa")));
        var edit = new EditGroupViewModel(session);

        Assert.True(edit.ClearCommand.CanExecute(null));
        edit.ClearCommand.Execute(null);

        Assert.Empty(session.Document.Layers);
        Assert.Null(session.SelectedLayerId);
        Assert.Single(session.Document.RegionFills);
        Assert.Equal(2, session.UndoCount);

        session.Undo();
        Assert.Equal(new[] { 1, 2 }, session.Document.Layers.Select(l => l.Id));
    }

    [Fact] // undo-empty-steps: Clear with no layers changes nothing; controller ruling 1: it is disabled then
    public void Clear_with_no_layers_records_nothing_and_is_disabled()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        Assert.False(edit.ClearCommand.CanExecute(null));
        edit.ClearCommand.Execute(null);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact] // controller ruling 1: CanExecute is Layers.Count > 0, refreshed on every document change
    public void Clear_enables_and_disables_as_layers_come_and_go()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        int changed = 0;
        edit.ClearCommand.CanExecuteChanged += (_, _) => changed++;

        session.Apply(d => d with { Layers = d.Layers.Add(LayerOf(1)) });
        Assert.True(edit.ClearCommand.CanExecute(null));

        edit.ClearCommand.Execute(null);
        Assert.False(edit.ClearCommand.CanExecute(null));

        session.Undo();
        Assert.True(edit.ClearCommand.CanExecute(null));
        Assert.True(changed >= 3);
    }

    [Fact] // critic L1 (controller ruling 2): inside a gesture only Document is raised, yet Undo must light up
    public void Undo_and_redo_refresh_during_an_open_gesture()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        int undoChanged = 0;
        edit.UndoCommand.CanExecuteChanged += (_, _) => undoChanged++;
        Assert.False(edit.UndoCommand.CanExecute(null));

        using (session.BeginGesture())
        {
            session.Apply(d => d with { ShowGrid = true });

            Assert.True(session.InGesture);
            Assert.True(edit.UndoCommand.CanExecute(null));
            Assert.True(undoChanged >= 1);
        }

        Assert.True(edit.UndoCommand.CanExecute(null));
        Assert.Equal(1, session.UndoCount);
    }
}
