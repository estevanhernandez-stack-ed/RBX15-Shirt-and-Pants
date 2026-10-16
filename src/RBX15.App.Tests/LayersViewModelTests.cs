using System.Windows.Media.Imaging;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The basic layers panel (editor.js:671-744, :846-853; ledger layers-list, layers-list-render,
/// layers-select, layers-visibility, layers-duplicate, layers-remove, layers-move-updown). Thumbnails
/// are WPF bitmaps, so every test runs on an STA thread.
/// </summary>
public class LayersViewModelTests
{
    static SKImage Img(int w = 10, int h = 10)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    /// <summary>A session holding layers named A, B, C... (ids 1..n, A at the bottom) with the top selected.</summary>
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

    static LayersViewModel Vm(EditorSession session, FakeFileDialogs? dialogs = null) =>
        new(session, new ImportService(session, new FakeStatus()), dialogs ?? new FakeFileDialogs());

    static string[] Names(LayersViewModel vm) => vm.Rows.Select(r => r.Name).ToArray();

    [Fact] // tools-auto-select-switch (R8): a user pick raises LayerPicked; a sync, the same row again or a deselect does not
    public void Picking_a_row_raises_LayerPicked_and_syncs_do_not() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        var vm = Vm(session);
        int picked = 0;
        vm.LayerPicked += (_, _) => picked++;
        session.Select(1);                    // the canvas or an undo moved the selection
        Assert.Equal(0, picked);
        vm.Selected = vm.Rows[0];             // the user picks B
        Assert.Equal((1, (int?)2), (picked, session.SelectedLayerId));
        vm.Selected = vm.Rows[0];
        vm.Selected = null;
        Assert.Equal(1, picked);
    });

    [Fact] // top = highest z (editor.js:672-675); .selected marks the selection
    public void Rows_list_the_top_layer_first_and_mark_the_selection() => StaThread.Run(() =>
    {
        var vm = Vm(Seeded(3));
        Assert.Equal(new[] { "C", "B", "A" }, Names(vm));
        Assert.Equal(new[] { true, false, false }, vm.Rows.Select(r => r.IsSelected));
        Assert.Same(vm.Rows[0], vm.Selected);
    });

    [Fact] // layers-list: add 3, newest on top and selected
    public void Imported_files_appear_newest_on_top_and_selected() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var dialogs = new FakeFileDialogs
        {
            OpenResult = [files.Image("one.png", 4, 4, SKColors.Red), files.Image("two.png", 4, 4, SKColors.Red), files.Image("three.png", 4, 4, SKColors.Red)],
        };
        var vm = Vm(session, dialogs);
        vm.BrowseCommand.Execute(null);
        Assert.Equal(1, dialogs.OpenCalls);
        Assert.Equal(new[] { "three", "two", "one" }, Names(vm));
        Assert.Equal("three", vm.Selected?.Name);
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // a cancelled pick adds nothing
    public void A_cancelled_browse_adds_nothing() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session, new FakeFileDialogs());
        vm.BrowseCommand.Execute(null);
        Assert.Empty(vm.Rows);
        Assert.Equal(0, session.UndoCount);
    });

    [Fact] // drop-panel: a zone drop uses the default placement
    public void A_zone_drop_uses_the_default_placement() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        Vm(session).DropFiles([files.Image("z.png", 512, 512, SKColors.Red)]);
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal((240.6, 83.6), (l.X, l.Y));
    });

    [Fact] // layers-select: selecting is never an undo step
    public void Selecting_a_row_selects_the_layer_without_an_undo_step() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.Selected = vm.Rows[2];
        Assert.Equal(1, session.SelectedLayerId);
        Assert.Equal(new[] { false, false, true }, vm.Rows.Select(r => r.IsSelected));
        vm.Selected = vm.Rows[1];
        Assert.Equal(2, session.SelectedLayerId);
        Assert.Same(vm.Rows[1], vm.Selected);
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // layers-visibility fix: one undo step; label, title and dimming follow
    public void Toggling_visibility_is_one_undo_step_and_relabels_the_row() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        var vm = Vm(session);
        var row = vm.Rows[1];
        Assert.Equal(("Hide", "Hide layer", "A", "Hide A"), (row.VisibilityLabel, row.VisibilityToolTip, row.AutomationName, row.VisibilityAutomationName));
        int steps = session.UndoCount;
        row.ToggleVisibilityCommand.Execute(null);
        Assert.False(session.Document.Layers[0].Visible);
        Assert.Equal(("Show", "Show layer", "A (hidden)", "Show A"), (row.VisibilityLabel, row.VisibilityToolTip, row.AutomationName, row.VisibilityAutomationName));
        Assert.Equal(steps + 1, session.UndoCount);
        session.Undo();
        Assert.True(row.Visible);
    });

    [Fact] // layers-duplicate: above the original, selected, one step
    public void Duplicate_inserts_the_copy_above_and_selects_it() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.Rows[1].DuplicateCommand.Execute(null); // A
        Assert.Equal(new[] { "B", "A copy", "A" }, Names(vm));
        Assert.Equal("A copy", vm.Selected?.Name);
        Assert.Equal(3, vm.Selected?.Id);
        Assert.Equal(steps + 1, session.UndoCount);
    });

    [Fact] // layers-remove: deleting the selected row selects the new top layer, one step
    public void Delete_of_the_selected_row_selects_the_new_top_layer() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        vm.Selected = vm.Rows[1]; // B
        int steps = session.UndoCount;
        vm.Rows[1].DeleteCommand.Execute(null);
        Assert.Equal(new[] { "C", "A" }, Names(vm));
        Assert.Equal("C", vm.Selected?.Name);
        Assert.Equal(steps + 1, session.UndoCount);
    });

    [Fact] // layers-move-updown fix: one step each; disabled at the ends and with no selection
    public void Up_and_down_reorder_one_step_each_and_disable_at_the_ends() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        Assert.False(vm.MoveUpCommand.CanExecute(null)); // C is on top
        Assert.True(vm.MoveDownCommand.CanExecute(null));
        int steps = session.UndoCount;
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "B", "C", "A" }, Names(vm));
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "B", "A", "C" }, Names(vm));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
        Assert.True(vm.MoveUpCommand.CanExecute(null));
        Assert.Equal(steps + 2, session.UndoCount);
        Assert.Equal("C", vm.Selected?.Name);

        session.Select(null);
        Assert.False(vm.MoveUpCommand.CanExecute(null));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
    });

    [Fact] // Review Focus (keyboard): rows are reused, so focus is not torn down by an edit
    public void Rows_are_updated_in_place_across_edits_and_undo() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        var before = vm.Rows.ToDictionary(r => r.Id);
        vm.Rows[2].ToggleVisibilityCommand.Execute(null);
        vm.MoveDownCommand.Execute(null);
        session.Undo();
        session.Undo();
        foreach (var row in vm.Rows) Assert.Same(before[row.Id], row);
        Assert.Equal(new[] { "C", "B", "A" }, Names(vm));
        Assert.True(vm.Rows.All(r => r.Visible));
    });

    [Fact] // 48x48 contain (editor.js:685): the bitmap keeps the aspect inside 96 device pixels
    public void Thumbnails_keep_the_aspect_inside_96_pixels() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(400, 100), session.TakeId(), "Wide"));
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(10, 30), session.TakeId(), "Small"));
        var vm = Vm(session);
        var wide = Assert.IsAssignableFrom<BitmapSource>(vm.Rows[1].Thumbnail);
        var small = Assert.IsAssignableFrom<BitmapSource>(vm.Rows[0].Thumbnail);
        Assert.Equal((96, 24), (wide.PixelWidth, wide.PixelHeight));
        Assert.Equal((10, 30), (small.PixelWidth, small.PixelHeight));
        Assert.True(wide.IsFrozen);
    });

    [Fact] // the row's automation name is the layer name; renamed layers follow
    public void The_automation_name_follows_the_layer_name() => StaThread.Run(() =>
    {
        var session = Seeded(1);
        var vm = Vm(session);
        session.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Name = "Logo" }) });
        Assert.Equal(("Logo", "Logo"), (vm.Rows[0].Name, vm.Rows[0].AutomationName));
    });
}
