using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The File group: Add image (editor.js:2692-2708) and Download template (exportPNG,
/// editor.js:1500-1541; export-png-guard, io-empty-export-confirm, canvas-export-pipeline,
/// io-download-save-dialog, io-export-filename).
/// </summary>
public class FileGroupTests
{
    sealed record Rig(EditorSession Session, FakeFileDialogs Files, FakeDialogService Dialogs, FakeStatus Status, FileGroupViewModel Vm);

    static Rig Make(Document? doc = null)
    {
        var session = new EditorSession(doc);
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var status = new FakeStatus();
        return new Rig(session, files, dialogs, status, HomeTestKit.FileGroup(session, files, dialogs, status));
    }

    static SKImage Img()
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    [Fact] // io-empty-export-confirm: the 4.1 text; Cancel aborts before any Save dialog
    public void An_empty_template_asks_first_and_cancel_opens_no_save_dialog()
    {
        var r = Make();
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal(new[] { "The template is empty — download a blank PNG anyway?" }, r.Dialogs.Confirms);
        Assert.Empty(r.Files.SaveSuggestions);
        Assert.Empty(r.Status.Shown);
    }

    [Fact] // Review Focus: hidden layers are not art, so the confirm still appears (editor.js:1504)
    public void Hidden_layers_only_still_ask()
    {
        var r = Make(new Document { Layers = [new Layer { Id = 1, Image = Img(), Visible = false }] });
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Single(r.Dialogs.Confirms);
    }

    [Fact] // OK on the confirm, then the Save dialog named by ExportNaming, then exactly Renderer.ExportPng's bytes
    public void Confirm_ok_saves_the_export_named_by_the_title()
    {
        using var files = new TestFiles();
        var r = Make();
        r.Dialogs.ConfirmAnswer = true;
        r.Session.TemplateTitle = "My Shirt!";
        r.Files.SaveResult = Path.Combine(files.Dir, "out.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal(new[] { "My_Shirt__roblox.png" }, r.Files.SaveSuggestions);
        var bytes = File.ReadAllBytes(r.Files.SaveResult);
        Assert.Equal(Renderer.ExportPng(r.Session.Document), bytes);
        using var png = SKImage.FromEncodedData(bytes);
        Assert.Equal((585, 559), (png.Width, png.Height));
        Assert.Equal("Saved out.png.", r.Status.Current);
    }

    [Fact] // export-png-guard: a region fill is art, so no confirm; an empty title uses roblox_shirt
    public void With_art_there_is_no_confirm()
    {
        using var files = new TestFiles();
        var r = Make(new Document { RegionFills = [("torso_front", new Fill("solid", "#17d4fa"))] });
        r.Files.SaveResult = Path.Combine(files.Dir, "a.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Empty(r.Dialogs.Confirms);
        Assert.Equal(new[] { "roblox_shirt_roblox.png" }, r.Files.SaveSuggestions);
        Assert.True(File.Exists(r.Files.SaveResult));
    }

    [Fact] // io-download-save-dialog: cancel writes nothing and says nothing
    public void Cancelling_the_save_dialog_writes_nothing()
    {
        var r = Make(new Document { BgImage = Img() });
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Single(r.Files.SaveSuggestions);
        Assert.Empty(r.Status.Shown);
    }

    [Fact] // Review Focus: a write that fails (missing folder, locked file) is a status message, never a crash
    public void A_write_failure_shows_a_status_message()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        r.Files.SaveResult = Path.Combine(files.Dir, "no-such-folder", "x.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.StartsWith("Couldn't save x.png: ", r.Status.Current);
        Assert.False(File.Exists(r.Files.SaveResult));
    }

    [Fact] // atomic write: overwriting replaces the file and leaves no temp file behind
    public void Overwriting_replaces_the_file_and_leaves_no_temp_file()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        var target = Path.Combine(files.Dir, "out.png");
        File.WriteAllBytes(target, [1, 2, 3]);
        r.Files.SaveResult = target;
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal(Renderer.ExportPng(r.Session.Document), File.ReadAllBytes(target));
        Assert.Equal(new[] { target }, Directory.GetFiles(files.Dir));
    }

    [Fact] // atomic write: a failure leaves an existing target intact and no temp file
    public void A_failed_save_keeps_the_existing_file_and_leaves_no_temp_file()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        var target = Path.Combine(files.Dir, "locked.png");
        File.WriteAllBytes(target, [9, 9, 9]);
        r.Files.SaveResult = target;
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.StartsWith("Couldn't save locked.png: ", r.Status.Current);
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(target));
        Assert.Equal(new[] { target }, Directory.GetFiles(files.Dir));
    }

    [Fact] // export is not a document change
    public void Export_is_not_an_undo_step()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        r.Files.SaveResult = Path.Combine(files.Dir, "a.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal((0, false), (r.Session.UndoCount, r.Session.IsDirty));
    }

    // ---- M4a Task 1: Save work, Save As and Open ----

    [Fact] // home-file-group: Save work runs Save As when untitled, then saves to the file; Save As always asks
    public void Save_work_and_save_as_go_through_the_project_service()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        r.Files.SaveProjectResult = Path.Combine(files.Dir, "w.r15proj");
        r.Vm.SaveCommand.Execute(null);
        Assert.Equal(r.Files.SaveProjectResult, r.Session.CurrentFile);
        r.Vm.SaveCommand.Execute(null);
        Assert.Single(r.Files.SaveProjectCalls);                     // the second Save work wrote to the current file
        r.Vm.SaveAsCommand.Execute(null);
        Assert.Equal(2, r.Files.SaveProjectCalls.Count);
        Assert.Equal("Saved w.r15proj.", r.Status.Current);
    }

    [Fact] // home-file-group: Open runs the Open dialog, then opens the pick
    public async Task Open_opens_the_picked_project()
    {
        using var files = new TestFiles();
        var r = Make();
        string path = files.Write("p.r15proj", System.Text.Encoding.UTF8.GetBytes("""{ "mode": "Pants" }"""));
        r.Files.OpenProjectResult = path;
        await r.Vm.OpenCommand.ExecuteAsync(null);
        Assert.Equal(TemplateMode.Pants, r.Session.Document.Mode);
        Assert.Equal(path, r.Session.CurrentFile);
    }

    [Fact] // R4: while an Open reads, Save work, Save As and Open cannot run, and each says so through CanExecuteChanged
    public async Task The_file_commands_wait_while_an_open_reads()
    {
        var session = new EditorSession();
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var status = new FakeStatus();
        var pending = new TaskCompletionSource<ProjectLoad>();
        var service = new ProjectFileService(session, files, dialogs, status, new FakeSettingsStore(), read: (_, _) => pending.Task, release: _ => { });
        var vm = new FileGroupViewModel(session, files, dialogs, status, new ImportService(session, status), service);
        int changes = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => changes++;
        vm.SaveAsCommand.CanExecuteChanged += (_, _) => changes++;
        vm.OpenCommand.CanExecuteChanged += (_, _) => changes++;
        Assert.True(vm.SaveCommand.CanExecute(null) && vm.SaveAsCommand.CanExecute(null) && vm.OpenCommand.CanExecute(null));

        var open = service.OpenAsync("C:\\p\\slow.r15proj");
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.SaveAsCommand.CanExecute(null));
        Assert.False(vm.OpenCommand.CanExecute(null));
        Assert.Equal(3, changes);

        pending.SetResult(new ProjectLoad(new Document(), 1, []));
        Assert.True(await open);
        Assert.True(vm.SaveCommand.CanExecute(null) && vm.SaveAsCommand.CanExecute(null) && vm.OpenCommand.CanExecute(null));
        Assert.Equal(6, changes);
    }

    [Fact] // project-load-image-layer: Add image imports what the picker returns, one step
    public void Add_image_imports_the_picked_files()
    {
        using var files = new TestFiles();
        var r = Make();
        r.Files.OpenResult = [files.Image("p.png", 4, 4, SKColors.Red), files.Image("q.jpg", 4, 4, SKColors.Red, SKEncodedImageFormat.Jpeg)];
        r.Vm.AddImageCommand.Execute(null);
        Assert.Equal(new[] { "p", "q" }, r.Session.Document.Layers.Select(l => l.Name));
        Assert.Equal(1, r.Session.UndoCount);
    }
}
