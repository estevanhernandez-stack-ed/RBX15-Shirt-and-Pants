using RBX15.App.ViewModels;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The Settings group (editor.html:98-107; ledger home-settings-group, overlay-mask-checkbox, overlay-grid-checkbox, file-undo-snapshot-scope).</summary>
public class SettingsGroupTests
{
    [Fact] // editor.html:100: the name is never an undo step and never dirties the document
    public void Template_name_is_session_state_not_an_undo_step()
    {
        var session = new EditorSession();
        var settings = new SettingsGroupViewModel(session);
        var raised = Track(settings);

        settings.TemplateName = "My Shirt";

        Assert.Equal("My Shirt", session.TemplateTitle);
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
        Assert.Contains(nameof(SettingsGroupViewModel.TemplateName), raised);

        settings.TemplateName = null!;
        Assert.Equal("", session.TemplateTitle);
    }

    [Fact] // defaults: Mask on, Grid off (editor.html:102-103)
    public void Mask_starts_on_and_grid_off()
    {
        var settings = new SettingsGroupViewModel(new EditorSession());
        Assert.Equal((true, false), (settings.ShowMask, settings.ShowGrid));
    }

    [Fact] // file-undo-snapshot-scope fix: each toggle is one step; undo puts the checkbox back (editor.js:1433-1436)
    public void Mask_and_grid_toggles_are_one_step_each_and_undo_restores_them()
    {
        var session = new EditorSession();
        var settings = new SettingsGroupViewModel(session);
        var raised = Track(settings);

        settings.ShowMask = false;
        settings.ShowGrid = true;
        Assert.Equal(2, session.UndoCount);
        Assert.Equal((false, true), (session.Document.ShowMask, session.Document.ShowGrid));

        session.Undo();
        Assert.False(settings.ShowGrid);
        session.Undo();
        Assert.True(settings.ShowMask);
        Assert.Equal(2, raised.Count(n => n == nameof(SettingsGroupViewModel.ShowMask)));
        Assert.Equal(2, raised.Count(n => n == nameof(SettingsGroupViewModel.ShowGrid)));
    }

    [Fact] // setting the value it already has changes nothing
    public void Setting_the_current_value_records_nothing()
    {
        var session = new EditorSession();
        var settings = new SettingsGroupViewModel(session);
        settings.ShowMask = true;
        settings.ShowGrid = false;
        Assert.Equal(0, session.UndoCount);
    }
}
