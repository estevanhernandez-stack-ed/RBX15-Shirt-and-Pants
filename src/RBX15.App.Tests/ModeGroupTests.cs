using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The Mode button (editor.js:1692-1725; ledger home-mode-toggle, mode-switch-undo-and-callers).</summary>
public class ModeGroupTests
{
    [Fact] // editor.html:33, editor.js:1702-1706
    public void Label_reads_SHIRT_then_PANTS()
    {
        var session = new EditorSession();
        var mode = new ModeGroupViewModel(session);
        Assert.Equal((TemplateMode.Shirt, "SHIRT"), (mode.Mode, mode.Label));

        var raised = Track(mode);
        mode.ToggleCommand.Execute(null);

        Assert.Equal((TemplateMode.Pants, "PANTS"), (mode.Mode, mode.Label));
        Assert.Equal(new[] { nameof(ModeGroupViewModel.Mode), nameof(ModeGroupViewModel.Label) }, raised);
    }

    [Fact] // one click = one step: the mode and the fill wipe together (editor.js:1723)
    public void Toggle_is_one_step_that_wipes_fills_and_keeps_layers()
    {
        var session = SessionWith(LayerOf(1));
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#17d4fa")));
        var mode = new ModeGroupViewModel(session);

        mode.ToggleCommand.Execute(null);

        Assert.Equal(2, session.UndoCount);
        Assert.Equal(TemplateMode.Pants, session.Document.Mode);
        Assert.Empty(session.Document.RegionFills);
        Assert.Single(session.Document.Layers);
    }

    [Fact] // the ledger's script: paint, toggle to Pants, undo -> Shirt with fills; redo -> Pants, empty
    public void Undo_and_redo_of_a_toggle_restore_mode_label_and_fills()
    {
        var session = new EditorSession();
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#17d4fa")));
        var mode = new ModeGroupViewModel(session);
        mode.ToggleCommand.Execute(null);

        Assert.True(session.Undo());
        Assert.Equal("SHIRT", mode.Label);
        Assert.Equal(("torso_front", Fill.Solid("#17d4fa")), Assert.Single(session.Document.RegionFills));

        Assert.True(session.Redo());
        Assert.Equal("PANTS", mode.Label);
        Assert.Empty(session.Document.RegionFills);
    }
}
