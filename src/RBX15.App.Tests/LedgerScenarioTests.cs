using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using Xunit;

namespace RBX15.App.Tests;

/// <summary>
/// M3a close-out scenarios. Each fact replays one parity-ledger row's parityCheck (named in its
/// comment) across the pieces Tasks 1-4 built, through their public surface only. A failure is a
/// defect in the owning task's code, never a reason to edit the scenario.
/// </summary>
public sealed class LedgerScenarioTests : IDisposable
{
    static readonly SKColor Red = new(255, 0, 0);
    static readonly SKColor Blue = new(0, 0, 255);
    static readonly SKColor Slate = new(100, 150, 200);
    readonly string _dir = Directory.CreateTempSubdirectory("rbx15-m3a-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Mode_switch_wipes_fills_keeps_layers_and_undo_brings_the_fills_back()
    {
        // mode-switch-effects, mode-switch-undo-and-callers, restore-mode-switch-side-effects,
        // tpl-active-regions-swap, claim-mode-per-mode-region-data (editor.js:1695-1725)
        using var art = Solid(40, 40, Slate);
        var s = new EditorSession();
        var fill = new Fill("linear", "#17d4fa", "#f22f89", 45, 16);
        foreach (string region in new[] { "torso_front", "torso_back", "rarm_front" })
            Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, region, fill)));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, art, s.TakeId(), "art")));
        int id = s.SelectedLayerId!.Value;
        var shirt = s.Document;

        Assert.True(s.Apply(DocumentOps.ToggleMode));
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
        Assert.Equal(shirt.Layers.ToArray(), s.Document.Layers.ToArray());
        Assert.Equal((shirt.BgColor, shirt.BgOpacity, shirt.ShowMask, shirt.ShowGrid),
                     (s.Document.BgColor, s.Document.BgOpacity, s.Document.ShowMask, s.Document.ShowGrid));

        Assert.True(s.Apply(DocumentOps.ToggleMode));       // back to Shirt: the fills do not come back
        Assert.Equal(TemplateMode.Shirt, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
        Assert.Equal(6, s.UndoCount);

        Assert.True(s.Undo());
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
        Assert.True(s.Undo());
        Assert.True(s.Document.ContentEquals(shirt));
        Assert.Equal(3, s.Document.RegionFills.Count);
        Assert.Equal(id, s.SelectedLayerId);
        Assert.True(s.Redo());
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
    }

    [Fact]
    public void Repainting_with_the_same_fill_is_no_step_and_clearing_an_unpainted_tile_is_none() => StaThread.Run(() =>
    {
        // regions-click-paint, regions-right-click-clear (editor.js:1668-1684)
        var s = new EditorSession();
        var fillStyle = new FillStyleViewModel(s, new Picker());
        var regions = new RegionsGroupViewModel(s, fillStyle);
        RegionTileViewModel Front() => regions.Body.Single(t => t.Region == "torso_front");
        byte[] blank = Renderer.ExportPng(s.Document);

        Front().ClearCommand.Execute(null);
        Assert.Equal(0, s.UndoCount);

        Front().PaintCommand.Execute(null);
        Front().PaintCommand.Execute(null);                 // same fill again: nothing changes, no step
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(fillStyle.Current, DocumentOps.RegionFill(s.Document, "torso_front"));
        Assert.Equal(fillStyle.Current, Front().Fill);

        Front().ClearCommand.Execute(null);
        Assert.Equal(2, s.UndoCount);
        Assert.Null(Front().Fill);
        Assert.Null(Front().Swatch);
        Assert.Equal(blank, Renderer.ExportPng(s.Document));

        Assert.True(s.Undo());
        Assert.Equal(fillStyle.Current, DocumentOps.RegionFill(s.Document, "torso_front"));
        Assert.True(s.Undo());
        Assert.Empty(s.Document.RegionFills);
        Assert.False(s.CanUndo);
    });

    [Fact]
    public void Clear_removes_only_layers_and_undo_restores_them()
    {
        // btn-clear (editor.js:1394)
        using var a = Solid(30, 30, Slate);
        using var b = Solid(20, 40, Blue);
        var s = new EditorSession();
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#ff0000"))));
        Assert.True(s.Apply(d => d with { BgColor = "#336699" }));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, a, s.TakeId(), "a")));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, b, s.TakeId(), "b")));
        var full = s.Document;
        var edit = new EditGroupViewModel(s);

        edit.ClearCommand.Execute(null);
        Assert.Empty(s.Document.Layers);
        Assert.Null(s.SelectedLayerId);
        Assert.Equal(full.RegionFills.ToArray(), s.Document.RegionFills.ToArray());
        Assert.Equal((full.Mode, full.BgColor, full.BgOpacity), (s.Document.Mode, s.Document.BgColor, s.Document.BgOpacity));
        using (var cleared = Renderer.Export(s.Document))
        {
            Assert.Equal(Red, PixelOf(cleared, 295, 138));                       // torso_front center: its fill
            Assert.Equal(new SKColor(0x33, 0x66, 0x99), PixelOf(cleared, 200, 100)); // torso_right: the background
        }

        Assert.True(edit.UndoCommand.CanExecute(null));
        edit.UndoCommand.Execute(null);
        Assert.True(s.Document.ContentEquals(full));
        Assert.Same(a, s.Document.Layers[0].Image);
        Assert.Same(b, s.Document.Layers[1].Image);
    }

    [Fact]
    public void Thirty_one_edits_leave_thirty_undos_and_a_new_edit_empties_redo()
    {
        // home-edit-group (editor.js:86, :1470-1487; lib/history.js:23-57)
        var s = new EditorSession();
        var edit = new EditGroupViewModel(s);
        Assert.False(edit.UndoCommand.CanExecute(null));
        Assert.False(edit.RedoCommand.CanExecute(null));
        for (int i = 1; i <= 31; i++)
        {
            double opacity = i / 100.0;
            Assert.True(s.Apply(d => d with { BgOpacity = opacity }));
        }

        int undos = 0;
        while (edit.UndoCommand.CanExecute(null) && undos < 100)
        {
            edit.UndoCommand.Execute(null);
            undos++;
        }
        Assert.Equal(30, undos);
        Assert.Equal(0.01, s.Document.BgOpacity);           // the oldest of the 31 snapshots was evicted
        Assert.True(edit.RedoCommand.CanExecute(null));

        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        Assert.False(edit.RedoCommand.CanExecute(null));
        Assert.Equal(0, s.RedoCount);
    }

    [Fact]
    public void Undo_and_redo_restore_the_exact_export_through_every_kind_of_edit()
    {
        // undo-restore, history-snapshot-coupling (editor.js:1404-1468)
        using var art = Solid(64, 64, Slate);
        var s = new EditorSession();
        var exports = new List<byte[]> { Renderer.ExportPng(s.Document) };
        void Step(bool recorded)
        {
            Assert.True(recorded);
            exports.Add(Renderer.ExportPng(s.Document));
        }

        Step(s.ApplyWithSelection(x => LayerOps.AddImage(x, art, s.TakeId(), "art")));
        int id = s.SelectedLayerId!.Value;
        Step(s.Apply(d => d with { BgColor = "#336699" }));
        Step(s.Apply(d => DocumentOps.PaintRegion(d, "torso_back", new Fill("stripes", "#ffffff", "#000000", 30, 8))));
        Step(s.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Brightness = 150 }) }));
        Step(s.Apply(DocumentOps.ToggleMode));
        Step(s.ApplyWithSelection(x => LayerOps.Remove(x, id)));
        Assert.Equal(7, exports.Count);

        for (int i = exports.Count - 2; i >= 0; i--)
        {
            Assert.True(s.Undo());
            Assert.Equal(exports[i], Renderer.ExportPng(s.Document));
        }
        Assert.False(s.Undo());
        for (int i = 1; i < exports.Count; i++)
        {
            Assert.True(s.Redo());
            Assert.Equal(exports[i], Renderer.ExportPng(s.Document));
        }
        Assert.False(s.Redo());
    }

    [Fact]
    public void Selection_survives_undoing_a_region_paint()
    {
        // undo-restore-ui-side-effects (4.1 deselects on every restore, editor.js:1451)
        using var art = Solid(32, 32, Slate);
        var s = new EditorSession();
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, art, s.TakeId(), "art")));
        int id = s.SelectedLayerId!.Value;
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_back", Fill.Solid("#00ff00"))));

        Assert.True(s.Undo());
        Assert.Empty(s.Document.RegionFills);
        Assert.Equal(id, s.SelectedLayerId);
        Assert.True(s.Redo());
        Assert.Equal(id, s.SelectedLayerId);
    }

    [Fact]
    public void Mask_and_grid_toggles_are_one_step_each_and_never_reach_the_export()
    {
        // overlay-mask-checkbox, overlay-grid-checkbox, home-settings-group
        // (editor.js:332-337, :352-359, :1494-1495; the export always masks, :1530-1532)
        using var red = Solid(20, 20, Red);
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = red, Name = "outside" }) }));
        byte[] export = Renderer.ExportPng(s.Document);
        var settings = new SettingsGroupViewModel(s);
        Assert.True(settings.ShowMask);
        Assert.False(settings.ShowGrid);

        using (var masked = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
            Assert.Equal(new SKColor(0x05, 0x0c, 0x18), PixelOf(masked, 5, 5));   // checkerboard (editor.js:436)

        settings.ShowMask = false;
        Assert.Equal(2, s.UndoCount);
        Assert.False(s.Document.ShowMask);
        using (var unmasked = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
            Assert.Equal(Red, PixelOf(unmasked, 5, 5));
        Assert.Equal(export, Renderer.ExportPng(s.Document));

        settings.ShowGrid = true;
        Assert.Equal(3, s.UndoCount);
        Assert.True(s.Document.ShowGrid);
        Assert.Equal(export, Renderer.ExportPng(s.Document));

        Assert.True(s.Undo());
        Assert.False(s.Document.ShowGrid);
        Assert.True(s.Undo());
        Assert.True(s.Document.ShowMask);
        Assert.True(settings.ShowMask);
    }

    [Fact]
    public void Download_asks_first_with_only_hidden_layers_or_a_background_color_and_cancel_writes_nothing()
    {
        // Review Focus; export-png-guard, io-empty-export-confirm (editor.js:1500-1506)
        using var red = Solid(20, 20, Red);
        var s = new EditorSession();
        var dialogs = new Dialogs();
        string path = Path.Combine(_dir, "blank.png");
        var files = new Files(path);
        var status = new Status();
        var file = new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status));

        file.DownloadTemplateCommand.Execute(null);         // a fresh document
        Assert.Equal(ExportGuard.EmptyTemplateMessage, Assert.Single(dialogs.Confirms));
        Assert.Empty(files.Suggested);

        Assert.True(s.Apply(d => d with
        {
            BgColor = "#ff8800",
            Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = red, Visible = false, X = 280, Y = 120 }),
        }));
        file.DownloadTemplateCommand.Execute(null);         // a background color and a hidden layer are not art
        Assert.Equal(2, dialogs.Confirms.Count);
        Assert.Empty(files.Suggested);
        Assert.False(File.Exists(path));

        dialogs.Answer = true;
        file.DownloadTemplateCommand.Execute(null);
        Assert.Equal(3, dialogs.Confirms.Count);
        Assert.Equal("roblox_shirt_roblox.png", Assert.Single(files.Suggested));
        Assert.Equal(Renderer.ExportPng(s.Document), File.ReadAllBytes(path));
    }

    [Fact]
    public void Download_with_the_mask_off_still_writes_the_masked_template()
    {
        // Review Focus; canvas-export-pipeline, overlay-mask-checkbox (editor.js:1508-1539)
        using var red = Solid(20, 20, Red);
        var s = new EditorSession { TemplateTitle = "My Shirt!" };
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#00ff00"))));
        Assert.True(s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = red, Name = "outside" }) }));
        Assert.True(s.Apply(d => d with { ShowMask = false }));
        var dialogs = new Dialogs();
        string path = Path.Combine(_dir, "shirt.png");
        var files = new Files(path);
        var status = new Status();

        new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status)).DownloadTemplateCommand.Execute(null);

        Assert.Empty(dialogs.Confirms);
        Assert.Equal("My_Shirt__roblox.png", Assert.Single(files.Suggested));
        using var written = SKImage.FromEncodedData(File.ReadAllBytes(path));
        Assert.Equal((585, 559), (written.Width, written.Height));
        Assert.Equal(0, PixelOf(written, 5, 5).Alpha);                        // the layer outside every region is masked
        Assert.Equal(new SKColor(0, 255, 0), PixelOf(written, 295, 138));     // the torso_front fill
    }

    [Fact]
    public void A_mixed_batch_adds_the_good_files_in_pick_order_as_one_step_and_names_each_failure()
    {
        // Review Focus; layers-multi-file-async-order, image-decode-caps, drop-filetypes (editor.js:998-1011)
        string[] paths =
        [
            WritePng("big.png", 300, 300, Slate),
            WriteText("broken.png", "not an image"),
            WriteText("logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"),
            WritePng("strip.png", Images.MaxDimension + 1, 1, Blue),
            WritePng("small.png", 10, 10, Red),
        ];
        var s = new EditorSession();
        var status = new Status();

        Assert.Equal(2, new ImportService(s, status).Import(paths));

        Assert.Equal(new[] { "big", "small" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal(s.Document.Layers[^1].Id, s.SelectedLayerId);
        Assert.Equal(1, s.UndoCount);
        var message = Assert.Single(status.Shown);          // one combined message per import (Task 4 ruling 5)
        foreach (string name in new[] { "broken.png", "logo.svg", "strip.png" })
            Assert.Contains(name, message, StringComparison.Ordinal);

        Assert.True(s.Undo());
        Assert.Empty(s.Document.Layers);
    }

    [Fact]
    public void Importing_after_undoing_an_import_takes_a_fresh_id()
    {
        // Review Focus (nextId only grows, editor.js:499, :1465)
        string path = WritePng("art.png", 24, 24, Slate);
        var s = new EditorSession();
        var import = new ImportService(s, new Status());

        Assert.Equal(1, import.Import([path]));
        int first = Assert.Single(s.Document.Layers).Id;
        Assert.True(s.Undo());
        Assert.True(s.CanRedo);

        Assert.Equal(1, import.Import([path]));
        var layer = Assert.Single(s.Document.Layers);
        Assert.Equal(first + 1, layer.Id);
        Assert.Equal(layer.Id, s.SelectedLayerId);
        Assert.False(s.CanRedo);
        Assert.Equal(first + 2, s.NextId);
    }

    [Fact]
    public void Three_imports_list_newest_first_and_hide_and_dupe_follow_the_z_order() => StaThread.Run(() =>
    {
        // layers-list, layers-visibility, layers-duplicate (editor.js:542-560, :570-573, :671-744)
        string[] paths = [WritePng("a.png", 16, 16, Red), WritePng("b.png", 16, 16, Blue), WritePng("c.png", 16, 16, Slate)];
        var s = new EditorSession();
        var import = new ImportService(s, new Status());
        var layers = new LayersViewModel(s, import, new Files(null));
        LayerRowViewModel Row(string name) => layers.Rows.Single(r => r.Name == name);

        Assert.Equal(3, import.Import(paths));
        Assert.Equal(new[] { "c", "b", "a" }, layers.Rows.Select(r => r.Name));
        Assert.True(Row("c").IsSelected);

        Row("b").ToggleVisibilityCommand.Execute(null);
        Assert.False(Row("b").Visible);
        Assert.Equal("Show", Row("b").VisibilityLabel);
        Assert.Equal("Show layer", Row("b").VisibilityToolTip);

        Row("b").DuplicateCommand.Execute(null);
        Assert.Equal(new[] { "c", "b copy", "b", "a" }, layers.Rows.Select(r => r.Name));
        Assert.True(Row("b copy").IsSelected);
        Assert.False(Row("b copy").Visible);
        Assert.Equal(s.Document.Layers.Reverse().Select(l => l.Id), layers.Rows.Select(r => r.Id));
        Assert.Equal(3, s.UndoCount);

        Assert.True(s.Undo());                              // the duplicate
        Assert.True(s.Undo());                              // the hide
        Assert.True(Row("b").Visible);
        Assert.Equal("Hide", Row("b").VisibilityLabel);
    });

    [Fact]
    public void A_color_pick_retargets_matching_regions_in_one_step_and_a_cancel_changes_nothing() => StaThread.Run(() =>
    {
        // fillstyle-color-pickers, fillstyle-live-edit-rule (editor.html:157-158; editor.js:1566-1598, :1608-1612)
        var s = new EditorSession();
        var picker = new Picker("#123456", null);
        var fillStyle = new FillStyleViewModel(s, picker);
        var regions = new RegionsGroupViewModel(s, fillStyle);
        regions.Body.Single(t => t.Region == "torso_front").PaintCommand.Execute(null);
        regions.Body.Single(t => t.Region == "torso_back").PaintCommand.Execute(null);
        Assert.Equal(2, s.UndoCount);

        fillStyle.PickColor1Command.Execute(null);
        Assert.Equal("#123456", fillStyle.Color1);
        Assert.Equal("#123456", fillStyle.Current.C1);
        foreach (string region in new[] { "torso_front", "torso_back" })
            Assert.Equal(fillStyle.Current, DocumentOps.RegionFill(s.Document, region));
        Assert.Equal(3, s.UndoCount);

        fillStyle.PickColor1Command.Execute(null);          // cancelled
        Assert.Equal("#123456", fillStyle.Current.C1);
        Assert.Equal(3, s.UndoCount);
        Assert.Equal(new[] { "#17d4fa", "#123456" }, picker.Opened);

        Assert.True(s.Undo());
        foreach (string region in new[] { "torso_front", "torso_back" })
            Assert.Equal("#17d4fa", DocumentOps.RegionFill(s.Document, region)!.C1);
    });

    [Fact]
    public void The_product_name_and_version_come_from_one_place()
    {
        // shell-window-title (main.js:8, editor.html:6); shell-header-version-hardcoded (editor.html:17)
        string name = AppInfo.ProductName;
        string version = AppInfo.Version;
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", name);
        Assert.Equal(ProjectIO.AppName, name);
        Assert.Equal("5.0.0", version);
    }

    static SKImage Solid(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(color);
        return SKImage.FromPixelCopy(bitmap.PeekPixels());
    }

    static SKColor PixelOf(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.GetPixel(x, y);
    }

    string WritePng(string fileName, int width, int height, SKColor color)
    {
        using var image = Solid(width, height, color);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    string WriteText(string fileName, string text)
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, text);
        return path;
    }

    sealed class Picker(params string?[] answers) : IColorPicker
    {
        readonly Queue<string?> _answers = new(answers);
        public List<string> Opened { get; } = [];

        public string? Pick(string initialHex)
        {
            Opened.Add(initialHex);
            return _answers.Count > 0 ? _answers.Dequeue() : null;
        }
    }

    sealed class Dialogs : IDialogService
    {
        public bool Answer { get; set; }
        public List<string> Confirms { get; } = [];
        public List<string> Alerts { get; } = [];

        public bool Confirm(string message)
        {
            Confirms.Add(message);
            return Answer;
        }

        public void Alert(string message) => Alerts.Add(message);
    }

    sealed class Files(string? savePath) : IFileDialogs
    {
        public List<string> Suggested { get; } = [];

        public IReadOnlyList<string> OpenImages() => [];

        public string? SavePng(string suggestedFileName)
        {
            Suggested.Add(suggestedFileName);
            return savePath;
        }
    }

    sealed class Status : IStatus
    {
        public List<string> Shown { get; } = [];
        public void Show(string message) => Shown.Add(message);
        public void Clear() { }
    }
}
