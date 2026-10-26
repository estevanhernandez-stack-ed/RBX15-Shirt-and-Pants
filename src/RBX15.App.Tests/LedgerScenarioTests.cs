using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Threading;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;
using Xunit;
using Point = System.Windows.Point;

namespace RBX15.App.Tests;

/// <summary>
/// M3a close-out scenarios. Each fact replays one parity-ledger row's parityCheck (named in its
/// comment) across the pieces Tasks 1-4 built, through their public surface only. A failure is a
/// defect in the owning task's code, never a reason to edit the scenario.
/// </summary>
public sealed partial class LedgerScenarioTests : IDisposable
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
        Assert.True(s.Apply(d => d with { BgColor = "#336699", BgOpacity = 1 }));   // a new document's background starts transparent
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
        var file = new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status), new ProjectFileService(s, files, dialogs, status, new Fakes.FakeSettingsStore()), new Fakes.FakeAppLauncher());

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

        new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status), new ProjectFileService(s, files, dialogs, status, new Fakes.FakeSettingsStore()), new Fakes.FakeAppLauncher()).DownloadTemplateCommand.Execute(null);

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

    // ─── M3b close-out scenarios (Task 5). Each drives the real composition (TestShell.Main) on the
    // StaThread, the way the canvas host, the panels and the ribbon do, through their public surface.

    [Fact]
    public void Select_resize_rotate_and_move_are_one_step_each_and_a_click_records_nothing() => StaThread.Run(() =>
    {
        // select-resize-drag, select-resize-handles, select-rotate-drag, select-rotate-drag-2, select-move,
        // select-move-hit-and-drag, select-deselect-click, canvas-select-gestures, canvas-coord-map
        // (editor.js:1023-1039, :1058-1117, :1169-1192)
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");          // center (250, 130)
        var canvas = main.Canvas.Interaction;
        Assert.Equal(Tool.Select, main.Home.Tools.Current);

        Assert.True(canvas.PointerDown(new Point(300, 160), 1));          // the bottom-right corner
        Assert.Equal(CanvasGesture.Resize, canvas.Gesture);
        canvas.PointerMove(new Point(312.5, 167.5), 1);
        canvas.PointerUp(new Point(325, 175), 1);                          // 1.5 times the start distance
        Assert.Equal(1.5, LayerOf(s, id).Scale, 12);
        Assert.Equal(175, LayerOf(s, id).X, 9);                            // the center stays put
        Assert.Equal(85, LayerOf(s, id).Y, 9);
        Assert.Equal(2, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(250, 55), 1));           // the knob: h/2 + 30 px above the center
        Assert.Equal(CanvasGesture.Rotate, canvas.Gesture);
        canvas.PointerMove(At(250, 130, 44, 80), 1);
        Assert.Equal(45, LayerOf(s, id).Rotation, 9);                      // within 3 degrees of 45: snapped
        canvas.PointerMove(At(250, 130, 50, 80), 1);
        canvas.PointerUp(At(250, 130, 50, 80), 1);
        Assert.Equal(50, LayerOf(s, id).Rotation, 9);
        Assert.Equal(3, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(250, 130), 1));          // the body
        Assert.Equal(CanvasGesture.Move, canvas.Gesture);
        canvas.PointerMove(new Point(265, 120), 1);
        canvas.PointerUp(new Point(280, 110), 1);
        Assert.Equal((205.0, 65.0), (LayerOf(s, id).X, LayerOf(s, id).Y));
        Assert.Equal(4, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(280, 110), 1));          // a click: press and release in place
        canvas.PointerUp(new Point(280, 110), 1);
        Assert.Equal(4, s.UndoCount);

        var from = CanvasCoordinates.ToTemplate(new Point(560, 220), 2);  // a 50x30 px drag in the frame at 200%
        var to = CanvasCoordinates.ToTemplate(new Point(610, 250), 2);
        Assert.True(canvas.PointerDown(from, 2));
        canvas.PointerUp(to, 2);
        Assert.Equal((230.0, 80.0), (LayerOf(s, id).X, LayerOf(s, id).Y)); // is 25x15 template px
        Assert.Equal(5, s.UndoCount);

        Assert.False(canvas.PointerDown(new Point(10, 540), 1));          // empty canvas deselects
        Assert.Null(s.SelectedLayerId);
        Assert.False(main.LayerProperties.HasSelection);
        Assert.Equal(5, s.UndoCount);

        s.Select(id);   // spec §5: the live selection persists across undo (History.Step), undo does not restore a cleared one
        for (int i = 0; i < 4; i++) Assert.True(s.Undo());
        var back = LayerOf(s, id);
        Assert.Equal((200.0, 100.0, 1.0, 0.0), (back.X, back.Y, back.Scale, back.Rotation));
        Assert.Equal(id, s.SelectedLayerId);                               // selection survives undo
    });

    [Fact]
    public void A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step() => StaThread.Run(() =>
    {
        // wheel-scale-layer, wheel-scale-undo-burst, props-scale-clamp-inconsistency, props-scale
        // (editor.js:1305-1313; spec §5; interfaces R6, R7)
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");
        var canvas = main.Canvas.Interaction;

        for (int i = 0; i < 3; i++) canvas.Wheel(+1);
        canvas.EndWheelBurst();
        var l = LayerOf(s, id);
        Assert.Equal(1.05 * 1.05 * 1.05, l.Scale, 12);
        Assert.Equal(250, l.X + 50 * l.Scale, 9);                          // centered, not top-left (4.1)
        Assert.Equal(130, l.Y + 30 * l.Scale, 9);
        Assert.Equal(2, s.UndoCount);

        for (int i = 0; i < 200; i++) canvas.Wheel(+1);
        canvas.EndWheelBurst();
        Assert.Equal(5, LayerOf(s, id).Scale, 12);                         // one clamp for every path: 1-500%
        Assert.Equal(500, main.LayerProperties.ScalePercent, 9);
        Assert.Equal("500%", main.LayerProperties.ScaleLabel);
        Assert.Equal(3, s.UndoCount);

        Assert.True(s.Undo());
        Assert.Equal(1.05 * 1.05 * 1.05, LayerOf(s, id).Scale, 12);
        Assert.True(s.Undo());
        Assert.Equal(1, LayerOf(s, id).Scale, 12);

        for (int i = 0; i < 10; i++) canvas.Wheel(-1);
        for (int i = 0; i < 10; i++) canvas.Wheel(+1);
        canvas.EndWheelBurst();
        Assert.Equal(1, LayerOf(s, id).Scale, 12);                         // symmetric steps; 4.1 drifts to 0.9794
        // The add plus this burst, exactly: 10 steps down and 10 up end at 0.9999999999999999, one ulp off the exact 1 the
        // layer started at, so the content changed and the burst is its one step. Pure double math with no timer (the
        // test closes the burst itself), so the count is the same on every run (final fix wave 12; 2 on three runs).
        Assert.Equal(2, s.UndoCount);

        s.Select(null);
        canvas.Wheel(+1);
        canvas.EndWheelBurst();
        Assert.Equal(1, LayerOf(s, id).Scale, 12);                         // nothing selected: nothing scales
    });

    [Fact]
    public void The_pick_is_the_box_not_the_pixels_and_a_hidden_selected_layer_has_no_handles() => StaThread.Run(() =>
    {
        // select-hit-test, select-hit-test-rect-not-alpha, select-hidden-layer-still-handled,
        // select-rotate-handle-hit, select-resize-handle-hit, render-selection-box
        // (editor.js:1043-1053, :1064-1117, :361-363; spec §5; interfaces R2)
        using var small = Solid(20, 20, Red);
        using var clear = Solid(200, 200, SKColors.Transparent);
        var (s, main) = Shell();
        AddLayer(s, small, 240, 120, "small");
        int over = AddLayer(s, clear, 150, 30, "clear");                  // a transparent box over the small layer
        var canvas = main.Canvas.Interaction;

        s.Select(null);
        Assert.True(canvas.PointerDown(new Point(250, 130), 1));          // on the small layer's red pixels
        canvas.PointerUp(new Point(250, 130), 1);
        Assert.Equal(over, s.SelectedLayerId);                              // the topmost box wins (kept)

        Assert.True(canvas.PointerDown(new Point(150, 30), 1));           // its top-left corner
        Assert.Equal(CanvasGesture.Resize, canvas.Gesture);
        canvas.PointerUp(new Point(150, 30), 1);
        using (var plain = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
        using (var selected = Renderer.RenderViewImage(s.Document, new ViewOptions(1) { SelectedLayerId = over }))
            Assert.NotEqual(Png(plain), Png(selected));                     // a visible selection draws its overlay

        Assert.True(s.Apply(d => LayerOps.ToggleVisibility(d, over)));
        Assert.Equal(over, s.SelectedLayerId);
        using (var plain = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
        using (var selected = Renderer.RenderViewImage(s.Document, new ViewOptions(1) { SelectedLayerId = over }))
            Assert.Equal(Png(plain), Png(selected));                        // hidden: no box, handles, knob or label

        Assert.False(canvas.PointerDown(new Point(250, 0), 1));           // where its knob would be: falls through
        Assert.Null(s.SelectedLayerId);                                     // to the pick, which skips hidden layers
        s.Select(over);
        Assert.False(canvas.PointerDown(new Point(150, 30), 1));          // its corner is dead too
        Assert.Null(s.SelectedLayerId);
        Assert.Equal(3, s.UndoCount);                                       // two adds and the hide; no gesture step
    });

    [Fact]
    public void Draw_tools_commit_one_named_layer_and_keep_the_tool_and_a_click_draws_only_the_pen_dot() => StaThread.Run(() =>
    {
        // draw-commit-layer, draw-empty-no-layer, draw-pen-single-click, draw-begin, draw-tool-start,
        // layers-add-drawing, canvas-draw-tools, home-brush-group, layers-default-name-offbyone
        // (editor.js:528-540, :1120-1146, :1204-1293)
        var (s, main) = Shell();
        var tools = main.Home.Tools;
        var canvas = main.Canvas.Interaction;
        var cyan = new SKColor(0x17, 0xd4, 0xfa);                           // the brush default (editor.html:56)

        tools.ToggleCommand.Execute(Tool.Rect);
        main.Home.Brush.Fill = true;
        Assert.True(canvas.PointerDown(new Point(100, 100), 1));
        Assert.Equal(0, s.UndoCount);                                       // no undo step at the press (4.1 pushes one)
        canvas.PointerMove(new Point(150, 140), 1);
        Assert.NotNull(canvas.Preview);
        canvas.PointerUp(new Point(150, 140), 1);
        var rect = Assert.Single(s.Document.Layers);
        Assert.Equal(Tools.DrawingName(ShapeTool.Rect), rect.Name);        // "Rect drawing" (editor.js:1285)
        Assert.Equal(rect.Id, s.SelectedLayerId);
        Assert.Equal(Tool.Rect, tools.Current);                             // a drawing keeps its tool
        Assert.Null(canvas.Preview);
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(cyan, PixelOf(rect.Image, (int)(125 - rect.X), (int)(120 - rect.Y)));   // filled in the brush color

        foreach (var tool in new[] { Tool.Line, Tool.Rect, Tool.Ellipse })
        {
            tools.Set(tool);
            canvas.PointerDown(new Point(300, 300), 1);
            canvas.PointerUp(new Point(300, 300), 1);
        }
        Assert.Single(s.Document.Layers);                                   // a shape click draws nothing
        Assert.Equal(1, s.UndoCount);

        tools.Set(Tool.Pen);
        Assert.True(canvas.PointerDown(new Point(400, 300), 1));
        canvas.PointerUp(new Point(400, 300), 1);
        Assert.Equal(2, s.Document.Layers.Count);                           // a pen click is a brush-size dot (approved)
        var dot = s.Document.Layers[^1];
        Assert.Equal(Tools.DrawingName(ShapeTool.Pen), dot.Name);
        Assert.Equal(cyan, PixelOf(dot.Image, (int)(400 - dot.X), (int)(300 - dot.Y)));
        Assert.Equal(2, s.UndoCount);
    });

    [Fact]
    public void The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden() => StaThread.Run(() =>
    {
        // eraser-selection-semantics, eraser-apply, eraser-click-without-move, canvas-draw-tools
        // (editor.js:1133-1144, :1265-1283; spec §5; interfaces R2, R3)
        using var a = Solid(100, 60, Red);
        using var b = Solid(100, 60, Blue);
        var (s, main) = Shell();
        int ida = AddLayer(s, a, 200, 100, "a");
        int idb = AddLayer(s, b, 200, 300, "b");
        var canvas = main.Canvas.Interaction;
        main.Home.Brush.Size = 10;
        s.Select(null);
        main.Home.Tools.Set(Tool.Eraser);
        var before = s.Document;

        Assert.False(canvas.PointerDown(new Point(210, 130), 1));
        Assert.Equal("Select a layer to erase from, then try again.", main.Status.Message);   // editor.js:1270, at the press
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Same(before, s.Document);

        s.Select(ida);
        main.Home.Tools.Set(Tool.Eraser);
        Assert.True(canvas.PointerDown(new Point(250, 130), 1));          // a click without movement erases nothing
        canvas.PointerUp(new Point(250, 130), 1);
        Assert.Same(before, s.Document);
        Assert.Equal(2, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(210, 130), 1));
        canvas.PointerMove(new Point(250, 130), 1);
        Assert.True(canvas.Preview!.IsEraser);
        Assert.Equal(0.5f, canvas.Preview!.Alpha);                           // the preview at half alpha (editor.js:327)
        canvas.PointerUp(new Point(290, 130), 1);
        Assert.Equal(3, s.UndoCount);
        Assert.Equal(0, PixelOf(LayerOf(s, ida).Image, 50, 30).Alpha);      // through the middle of layer a
        Assert.Equal(Red, PixelOf(LayerOf(s, ida).Image, 50, 5));           // outside the 10 px stroke
        Assert.Same(b, LayerOf(s, idb).Image);                              // layer b is untouched

        Assert.True(s.Apply(d => LayerOps.ToggleVisibility(d, ida)));
        var hidden = LayerOf(s, ida).Image;
        Assert.True(canvas.PointerDown(new Point(210, 110), 1));          // hidden but selected: it still erases (4.1)
        canvas.PointerUp(new Point(290, 110), 1);
        Assert.NotSame(hidden, LayerOf(s, ida).Image);
        Assert.Equal(5, s.UndoCount);
    });

    [Fact]
    public void A_tool_switch_mid_stroke_discards_it_and_a_lost_capture_commits_it() => StaThread.Run(() =>
    {
        // draw-tool-switch-midstroke (R9), draw-release-outside-canvas, select-hover-and-end (R10)
        // (editor.js:1239-1246, :1296-1302)
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");
        var tools = main.Home.Tools;
        var canvas = main.Canvas.Interaction;

        tools.Set(Tool.Pen);
        Assert.True(canvas.PointerDown(new Point(50, 400), 1));
        canvas.PointerMove(new Point(120, 450), 1);
        Assert.NotNull(canvas.Preview);
        tools.Set(Tool.Line);                                                // 4.1 orphans the preview; v5 cancels
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Null(canvas.Preview);
        Assert.Single(s.Document.Layers);
        Assert.Equal(1, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(50, 400), 1));
        canvas.PointerMove(new Point(120, 450), 1);
        canvas.CaptureLost();                                                // released outside the window, or Alt+Tab
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Equal(Tools.DrawingName(ShapeTool.Line), s.Document.Layers[^1].Name);
        Assert.Equal(2, s.UndoCount);

        tools.Set(Tool.Select);
        s.Select(id);
        Assert.True(canvas.PointerDown(new Point(250, 130), 1));
        canvas.PointerMove(new Point(270, 140), 1);
        tools.Set(Tool.Pen);                                                 // a select drag ends as if released
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Equal((220.0, 110.0), (LayerOf(s, id).X, LayerOf(s, id).Y));
        Assert.Equal(3, s.UndoCount);
        canvas.PointerUp(new Point(300, 160), 1);                            // the stray release changes nothing
        Assert.Equal((220.0, 110.0), (LayerOf(s, id).X, LayerOf(s, id).Y));
        Assert.Equal(3, s.UndoCount);
    });

    [Fact]
    public void The_wheel_steps_the_brush_in_draw_tools_and_widens_a_live_shape() => StaThread.Run(() =>
    {
        // wheel-brush-size, wheel-sign-only-and-midstroke (R11), tools-draw-props, draw-brush-controls, home-brush-group
        // (editor.html:56-59; editor.js:90-92, :1207-1210, :1314-1319)
        var (s, main) = Shell();
        var brush = main.Home.Brush;
        var canvas = main.Canvas.Interaction;
        Assert.Equal(("#17d4fa", 4, false), (brush.Color, brush.Size, brush.Fill));

        main.Home.Tools.Set(Tool.Pen);
        for (int i = 0; i < 20; i++) canvas.Wheel(+1);
        Assert.Equal(24, brush.Size);
        for (int i = 0; i < 60; i++) canvas.Wheel(+1);
        Assert.Equal(50, brush.Size);
        for (int i = 0; i < 100; i++) canvas.Wheel(-1);
        Assert.Equal(1, brush.Size);
        Assert.Equal(0, s.UndoCount);                                       // the brush is never document state

        brush.Size = 10;
        main.Home.Tools.Set(Tool.Rect);
        Assert.True(canvas.PointerDown(new Point(100, 100), 1));
        canvas.PointerMove(new Point(200, 180), 1);
        Assert.Equal(10f, canvas.Preview!.Stroke.Width);
        canvas.Wheel(+1);
        Assert.Equal(11, brush.Size);
        Assert.Equal(11f, canvas.Preview!.Stroke.Width);                    // the live shape takes the new width
        canvas.PointerUp(new Point(200, 180), 1);
        var expected = Tools.CommitDrawing(new Stroke(ShapeTool.Rect, [new SKPoint(100, 100), new SKPoint(200, 180)], brush.SkColor, 11, false), 99, "expected")!;
        var got = Assert.Single(s.Document.Layers);
        Assert.Equal((expected.X, expected.Y, expected.Image.Width, expected.Image.Height), (got.X, got.Y, got.Image.Width, got.Image.Height));
        Assert.Equal(1, s.UndoCount);
        expected.Image.Dispose();
    });

    [Fact]
    public void Every_property_edit_is_one_step_zero_shows_as_zero_and_adjustments_stay_open() => StaThread.Run(() =>
    {
        // layers-empty-state, layers-select, props-name-xy, props-scale, props-rotation, props-opacity, props-flip,
        // props-adjust-panel, props-adjust-reset, props-falsy-zero-adjust-display, props-rotation-label-unrounded,
        // props-panel-rebuild-resets, layers-model (editor.js:747-913)
        using var art = Solid(100, 60, Slate);
        using var other = Solid(10, 10, Blue);
        var (s, main) = Shell();
        var props = main.LayerProperties;
        Assert.False(props.HasSelection);
        Assert.Equal("Select a layer to edit.", props.EmptyText);          // editor.js:750
        int idOther = AddLayer(s, other, 20, 20, "other");
        int id = AddLayer(s, art, 200, 100, "art");
        Assert.True(props.HasSelection);
        Assert.Equal(2, s.UndoCount);

        props.Name = "logo";
        Assert.Equal("logo", LayerOf(s, id).Name);
        Assert.Contains(main.Layers.Rows, r => r.Name == "logo");
        props.XText = "12.7";
        Assert.Equal(12.7, LayerOf(s, id).X);
        Assert.Equal("13", props.XText);                                    // stored 12.7, shown toFixed(0) (editor.js:756)
        props.YText = "abc";
        Assert.Equal("100", props.YText);                                   // unparsable: reverts, no step
        Assert.Equal(4, s.UndoCount);

        props.ScalePercent = 250;
        Assert.Equal((2.5, 12.7, 100.0), (LayerOf(s, id).Scale, LayerOf(s, id).X, LayerOf(s, id).Y));   // top-left anchor
        Assert.Equal("250%", props.ScaleLabel);
        props.ScalePercent = 900;
        Assert.Equal(5, LayerOf(s, id).Scale, 12);                          // one clamp, 1-500%
        props.Rotation = 30.4;
        Assert.Equal("30°", props.RotationLabel);
        props.Rotation = -0.4;
        Assert.Equal("0°", props.RotationLabel);                            // rounded, never "-0"
        props.OpacityPercent = 50;
        Assert.Equal(0.5, LayerOf(s, id).Opacity);
        Assert.Equal("50%", props.OpacityLabel);
        props.FlipHCommand.Execute(null);
        Assert.True(LayerOf(s, id).FlipH);
        Assert.Equal(10, s.UndoCount);

        props.AdjustmentsExpanded = true;
        props.Brightness = 0;
        Assert.Equal(0, LayerOf(s, id).Brightness);
        Assert.Equal("0%", props.BrightnessLabel);
        s.Select(idOther);
        s.Select(id);
        Assert.Equal(0, props.Brightness);                                  // 4.1 shows 100 here (editor.js:785)
        Assert.Equal("0%", props.BrightnessLabel);
        Assert.True(props.AdjustmentsExpanded);                             // 4.1 collapses it on every rebuild
        props.ResetAdjustmentsCommand.Execute(null);
        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(LayerOf(s, id)));
        Assert.True(props.AdjustmentsExpanded);
        Assert.Equal(12, s.UndoCount);
        props.ResetAdjustmentsCommand.Execute(null);                        // already at the defaults: no step
        Assert.Equal(12, s.UndoCount);

        using (props.BeginSliderDrag())
        {
            foreach (double r in new[] { 10.0, 20.0, 25.0 }) props.Rotation = r;
        }
        Assert.Equal(25, LayerOf(s, id).Rotation, 9);
        Assert.Equal(13, s.UndoCount);                                      // one drag, one step

        s.Select(null);
        Assert.False(props.HasSelection);
    });

    [Fact]
    public void The_panel_follows_a_canvas_drag_live_and_refreshes_after_a_region_fit() => StaThread.Run(() =>
    {
        // props-live-refresh, props-name-xy, regions-click-fit-layer (editor.js:1169-1192, :1658-1667)
        using var art = Solid(200, 100, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 100, 100, "art");                        // center (200, 150)
        var props = main.LayerProperties;
        var canvas = main.Canvas.Interaction;

        Assert.True(canvas.PointerDown(new Point(200, 70), 1));           // the knob
        canvas.PointerMove(At(200, 150, 60, 90), 1);
        Assert.Equal(60, props.Rotation, 9);                                // live, mid-drag
        Assert.Equal("60°", props.RotationLabel);
        canvas.PointerMove(At(200, 150, 77.4, 90), 1);
        Assert.Equal("77°", props.RotationLabel);
        canvas.PointerUp(At(200, 150, 77.4, 90), 1);

        Assert.True(canvas.PointerDown(new Point(200, 150), 1));
        canvas.PointerMove(new Point(230, 170), 1);
        Assert.Equal(("130", "120"), (props.XText, props.YText));          // 4.1 leaves X/Y stale during a move
        canvas.PointerUp(new Point(230, 170), 1);
        Assert.Equal(3, s.UndoCount);

        main.Layout.Regions.Body.Single(t => t.Region == "torso_front").PaintCommand.Execute(null);
        // 200x100 into torso_front (231, 74, 128x128): scale 0.544, x 240.6, y 110.8, rotation 0 (editor.js:1658-1667)
        Assert.Equal(54.4, props.ScalePercent, 9);
        Assert.Equal("54%", props.ScaleLabel);
        Assert.Equal(("241", "111"), (props.XText, props.YText));
        Assert.Equal(0, props.Rotation, 9);
        Assert.Equal(4, s.UndoCount);
    });

    [Fact]
    public void Dragging_a_row_reorders_in_one_step_a_stale_target_does_nothing_and_move_up_is_one_step() => StaThread.Run(() =>
    {
        // layers-reorder-drag, layers-reorder-drag-2, layer-reorder-dragging-class-timer (editor.js:584-666)
        using var img = Solid(16, 16, Slate);
        var (s, main) = Shell();
        int a = AddLayer(s, img, 0, 0, "a");
        int b = AddLayer(s, img, 0, 0, "b");
        int c = AddLayer(s, img, 0, 0, "c");
        int d = AddLayer(s, img, 0, 0, "d");
        var layers = main.Layers;
        string[] Names() => layers.Rows.Select(r => r.Name).ToArray();
        Assert.Equal(new[] { "d", "c", "b", "a" }, Names());
        Assert.Equal("(drag to reorder)", layers.ReorderHint);              // editor.html:317, now that reorder works

        layers.BeginRowDrag(a);
        Assert.True(layers.Rows.Single(r => r.Id == a).IsDragSource);
        layers.DragOverRow(d, above: true);
        Assert.Equal(DropIndicator.Above, layers.Rows.Single(r => r.Id == d).DropIndicator);
        Assert.True(layers.CompleteRowDrop());
        layers.EndRowDrag();
        Assert.Equal(new[] { "a", "d", "c", "b" }, Names());
        Assert.All(layers.Rows, r => Assert.False(r.IsDragSource));
        Assert.All(layers.Rows, r => Assert.Equal(DropIndicator.None, r.DropIndicator));
        Assert.Equal(5, s.UndoCount);

        layers.BeginRowDrag(b);
        layers.DragOverRow(c, above: false);
        layers.ClearDropTarget();                                           // off every row: 4.1 reuses the old target
        Assert.False(layers.CompleteRowDrop());
        layers.EndRowDrag();
        Assert.Equal(new[] { "a", "d", "c", "b" }, Names());
        Assert.Equal(5, s.UndoCount);

        s.Select(c);
        layers.MoveUpCommand.Execute(null);                                 // Alt+Up on a focused row runs it
        Assert.Equal(new[] { "a", "c", "d", "b" }, Names());
        Assert.Equal(6, s.UndoCount);
        Assert.True(s.Undo());
        Assert.True(s.Undo());
        Assert.Equal(new[] { "d", "c", "b", "a" }, Names());
    });

    [Fact]
    public void Tool_buttons_toggle_off_to_select_and_an_import_or_a_list_pick_returns_to_select() => StaThread.Run(() =>
    {
        // tools-set-tool, tools-auto-select-switch (interfaces R8; editor.js:464-490, :523, :528-540, :577)
        var (s, main) = Shell();
        var tools = main.Home.Tools;
        ToolButtonViewModel Button(Tool t) => tools.Buttons.Single(x => x.Tool == t);
        Tool[] Active() => tools.Buttons.Where(x => x.IsActive).Select(x => x.Tool).ToArray();
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser" }, tools.Buttons.Select(x => x.Label));
        Assert.Equal(new[] { Tool.Select }, Active());                      // Select at launch (editor.js:479)

        Button(Tool.Rect).Command.Execute(null);
        Assert.Equal(new[] { Tool.Rect }, Active());
        Button(Tool.Rect).Command.Execute(null);                            // the active tool again: back to Select
        Assert.Equal(new[] { Tool.Select }, Active());
        Button(Tool.Select).Command.Execute(null);                          // Select has no "off"
        Assert.Equal(Tool.Select, tools.Current);

        tools.Set(Tool.Pen);
        main.Layers.DropFiles([WritePng("art.png", 16, 16, Blue)]);
        Assert.Equal(Tool.Select, tools.Current);                           // an import returns to Select (editor.js:523)

        tools.Set(Tool.Pen);
        Assert.True(main.Canvas.Interaction.PointerDown(new Point(450, 450), 1));
        main.Canvas.Interaction.PointerUp(new Point(450, 450), 1);
        Assert.Equal(Tool.Pen, tools.Current);                              // a drawing keeps its tool (editor.js:528-540)
        Assert.Equal(2, s.Document.Layers.Count);

        main.Layers.Selected = main.Layers.Rows.Single(r => r.Name == "art");
        Assert.Equal(Tool.Select, tools.Current);                           // a pick in the list returns to Select (:577)

        tools.Set(Tool.Eraser);
        Assert.True(s.Undo());
        Assert.Equal(Tool.Eraser, tools.Current);                           // undo leaves the tool alone
    });

    [Fact]
    public void Brush_tool_and_zoom_are_never_undo_steps_and_undo_leaves_them_alone() => StaThread.Run(() =>
    {
        // undo-snapshot-contents (editor.js:1404-1415): a snapshot holds the document and the selection only
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");
        main.Home.Brush.Size = 30;
        main.Home.Brush.Fill = true;
        main.Home.Tools.Set(Tool.Rect);
        main.Canvas.ZoomInCommand.Execute(null);
        Assert.Equal(1, s.UndoCount);

        Assert.True(s.Apply(d => LayerOps.SetOpacity(d, id, 0.5)));
        Assert.True(s.Undo());
        Assert.Equal(1, LayerOf(s, id).Opacity);
        Assert.Equal((30, true, Tool.Rect, 1.25), (main.Home.Brush.Size, main.Home.Brush.Fill, main.Home.Tools.Current, main.Canvas.Zoom));
    });

    // ─── M4a close-out scenarios (Task 5). Each builds the real composition (Composition.Build) with recording fakes
    // for the native file dialogs, the themed dialogs and the settings file, and drives the File group, the project
    // service and the asset library through their public surface, the way the window and the panels do.

    [Fact]
    public void Save_as_writes_the_project_marks_it_clean_and_reopening_restores_it_with_history_cleared() => StaThread.Run(() =>
    {
        // project-save-format, project-save-filename, file-project-format, project-load-history-interplay,
        // project-template-name-not-saved, home-file-group, window-title-dirty-marker
        // (editor.js:2550-2591 saveProject, :2594-2690 loadProject; interfaces R1, R2, R3)
        using var art = Solid(40, 30, Slate);
        var (s, main, files, dialogs, settings) = Shell4a();
        string path = Path.Combine(_dir, "My_Shirt_v2_.r15proj");
        s.TemplateTitle = "My Shirt v2!";
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#ff0000"))));
        int id = AddLayer(s, art, 200, 100, "art");
        Assert.True(s.IsDirty);
        Assert.Equal("*Untitled - RBX15 Classic Shirt and Pants Maker", main.Title);

        files.Saves.Enqueue(path);
        main.Home.File.SaveCommand.Execute(null);                          // Save work with no current file is Save As
        Assert.Equal(new[] { "My_Shirt_v2_.r15proj" }, files.SuggestedProjects);   // ExportNaming.ProjectFileName
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));                   // temp file plus move: nothing left over
        using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("5.0", json.RootElement.GetProperty("version").GetString());
            Assert.False(json.RootElement.TryGetProperty("title", out _));   // R1: the template name is not saved
        }
        Assert.False(s.IsDirty);
        Assert.Equal(path, s.CurrentFile);
        Assert.Equal("My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        Assert.Equal("Saved My_Shirt_v2_.r15proj.", main.Status.Message);
        Assert.Equal(_dir, settings.Folders[SettingsFolder.Project]);     // the last folder is remembered
        byte[] saved = Renderer.ExportPng(s.Document);

        Assert.True(s.Apply(d => LayerOps.MoveTo(d, id, 10, 10)));
        Assert.True(s.IsDirty);
        Assert.Equal("*My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        Assert.True(s.Undo());                                             // R2: undoing back to the save reads clean
        Assert.False(s.IsDirty);
        Assert.Equal("My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);

        Assert.True(s.Apply(d => LayerOps.MoveTo(d, id, 10, 10)));
        dialogs.Choices.Enqueue(SaveChoice.DontSave);
        Assert.True(Await(main.Files.OpenAsync(path)));
        Assert.Equal(new[] { "My_Shirt_v2_.r15proj" }, dialogs.SaveChanges);   // the guard named the file
        Assert.Equal(saved, Renderer.ExportPng(s.Document));               // the reopened file exports as it was saved
        Assert.Equal(0, s.UndoCount);                                      // spec §3: history cleared on open
        Assert.False(s.CanUndo);
        Assert.Equal(s.Document.Layers[^1].Id, s.SelectedLayerId);         // T1-R2: top layer, editor.js:2664
        Assert.Equal(s.Document.Layers.Max(l => l.Id) + 1, s.NextId);
        Assert.Equal("My Shirt v2!", s.TemplateTitle);                     // R1: Open leaves the name box alone
        Assert.False(s.IsDirty);
        Assert.Equal(path, s.CurrentFile);
        Assert.Equal("My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);

        var (_, untitled, blank, _, _) = Shell4a();
        untitled.Home.File.SaveCommand.Execute(null);                      // the Save dialog is cancelled: nothing written
        Assert.Equal(new[] { "untitled.r15proj" }, blank.SuggestedProjects);
        Assert.Null(untitled.Session.CurrentFile);
    });

    [Fact]
    public void A_file_that_cannot_open_alerts_and_changes_nothing_and_layers_that_cannot_load_are_named() => StaThread.Run(() =>
    {
        // io-alert-inventory, project-load-flow, project-load-error-path-bugs, project-load-untrusted-src,
        // project-load-no-imgsrc-hang (editor.js:2632, :2663-2680; interfaces R4)
        using var art = Solid(30, 30, Slate);
        var (s, main, _, dialogs, _) = Shell4a();
        AddLayer(s, art, 50, 60, "kept");
        var before = s.Document;
        foreach (var text in new[] { "not json", "{\"version\": \"4.0\", \"layers\": [{\"id\": 1," })
        {
            string bad = WriteText($"bad{dialogs.Alerts.Count}.r15proj", text);
            Assert.False(Await(main.Files.OpenAsync(bad)));
            Assert.StartsWith("Failed to load project. ", dialogs.Alerts[^1]);   // editor.js:2676
            Assert.Same(before, s.Document);
            Assert.Equal(1, s.UndoCount);
            Assert.Null(s.CurrentFile);
        }
        Assert.Empty(dialogs.SaveChanges);                                  // R4: a file that cannot open never asks to save first

        using var a = Solid(20, 10, Red);
        using var b = Solid(20, 10, Blue);
        using var c = Solid(20, 10, Slate);
        var three = new Document
        {
            Layers = ImmutableList.Create(
                new Layer { Id = 1, Image = a, Name = "A", X = 10, Y = 10 },
                new Layer { Id = 2, Image = b, Name = "B", X = 20, Y = 20 },
                new Layer { Id = 3, Image = c, Name = "C", X = 30, Y = 30 }),
        };
        var node = JsonNode.Parse(ProjectIO.Write(three, DateTimeOffset.UnixEpoch))!;
        node["layers"]![1]!["imgSrc"] = "http://example.com/b.png";        // refused: not a data: URL
        node["layers"]![2]!.AsObject().Remove("imgSrc");                     // 4.1 skipped it silently (editor.js:2632)
        string mixed = WriteText("mixed.r15proj", node.ToJsonString());
        dialogs.Choices.Enqueue(SaveChoice.DontSave);
        Assert.True(Await(main.Files.OpenAsync(mixed)));
        Assert.Equal(new[] { "A" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal("Opened mixed.r15proj. Couldn't load: B, C.", main.Status.Message);
        Assert.Equal(2, s.NextId);
        Assert.Equal(0, s.UndoCount);

        var none = new JsonObject { ["layers"] = new JsonArray(new JsonObject { ["id"] = 1, ["name"] = "P" }, new JsonObject { ["id"] = 2, ["name"] = "Q" }) };
        string empty = WriteText("none.r15proj", none.ToJsonString());
        Assert.True(Await(main.Files.OpenAsync(empty)));                    // clean since the last open: no prompt
        Assert.Empty(s.Document.Layers);
        Assert.Same(s.Document, main.Canvas.Document);                      // the canvas took the opened document
        Assert.Equal("Opened none.r15proj. Couldn't load: P, Q.", main.Status.Message);
        Assert.False(s.Undo());                                              // Undo straight after Open does nothing
        Assert.Single(dialogs.SaveChanges);
    });

    [Fact]
    public void The_unsaved_changes_guard_asks_only_when_dirty_and_cancel_or_a_cancelled_save_keeps_everything() => StaThread.Run(() =>
    {
        // shell-unsaved-work-loss, shell-no-unsaved-changes-guard (interfaces R2, R13; Este 2026-10-16)
        using var art = Solid(30, 30, Slate);
        var (s, main, files, dialogs, _) = Shell4a();
        Assert.True(main.Files.ConfirmDiscardChanges());                    // nothing to lose: no prompt
        Assert.Empty(dialogs.SaveChanges);

        AddLayer(s, art, 50, 60, "art");
        dialogs.Choices.Enqueue(SaveChoice.Cancel);
        Assert.False(main.Files.ConfirmDiscardChanges());
        dialogs.Choices.Enqueue(SaveChoice.Save);                           // Save on an untitled document is Save As...
        Assert.False(main.Files.ConfirmDiscardChanges());                   // ...and a cancelled Save As cancels the action
        Assert.Equal(new[] { "untitled.r15proj" }, files.SuggestedProjects);
        dialogs.Choices.Enqueue(SaveChoice.DontSave);
        Assert.True(main.Files.ConfirmDiscardChanges());
        Assert.Equal(new[] { "Untitled", "Untitled", "Untitled" }, dialogs.SaveChanges);
        Assert.True(s.IsDirty);                                             // Don't save leaves the document to the caller
        Assert.Null(s.CurrentFile);

        string path = Path.Combine(_dir, "guard.r15proj");
        files.Saves.Enqueue(path);
        dialogs.Choices.Enqueue(SaveChoice.Save);
        Assert.True(main.Files.ConfirmDiscardChanges());
        Assert.True(File.Exists(path));
        Assert.False(s.IsDirty);
        Assert.True(s.Undo());                                               // R2: past the save reads dirty...
        Assert.True(s.IsDirty);
        Assert.True(s.Redo());                                               // ...and back to it reads clean
        Assert.False(s.IsDirty);
        Assert.True(main.Files.ConfirmDiscardChanges());
        Assert.Equal(4, dialogs.SaveChanges.Count);
    });

    [Fact]
    public void The_built_in_packs_ship_byte_identical_list_sorted_and_add_by_click_and_right_click_in_one_step_each() => StaThread.Run(() =>
    {
        // pack-contents, packs-inventory-drips, packs-inventory-wordart, pack-builtin-load, packs-load-builtin,
        // pack-load-sync-and-mime, asset-click-add, asset-rightclick-full (editor.js:2340-2378, :2491-2539)
        string repo = RepoRoot.Find(AppContext.BaseDirectory);
        var packs = new PackSource(PackSource.DefaultDirectory);
        foreach (var (name, count) in new[] { (PackSource.Drips, 156), (PackSource.WordArt, 25) })
        {
            var shipped = packs.Files(name)!.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
            var source = Directory.GetFiles(Path.Combine(repo, "packs", name), "*.png").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.Equal(count, shipped.Count);
            Assert.Equal(source.Select(Path.GetFileName), shipped.Select(Path.GetFileName));
            for (int i = 0; i < count; i++)
                Assert.Equal(SHA256.HashData(File.ReadAllBytes(source[i])), SHA256.HashData(File.ReadAllBytes(shipped[i])));
        }

        var (s, main, _, _, _) = Shell4a();
        main.Home.Tools.Set(Tool.Pen);
        main.Assets.LoadDripsCommand.Execute(null);
        Assert.Equal("156 images in built-in drips pack", main.Assets.CountText);   // editor.js:2373, no longer overwritten
        var tiles = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(156, tiles.Count);
        Assert.All(main.Assets.Rows.Take(main.Assets.Rows.Count - 1), r => Assert.Equal(3, r.Tiles.Count));   // editor.html:305
        var names = tiles.Select(t => t.Entry.FileName).ToList();
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
        Assert.Equal("drip_blood_front_flipped.png", names[0]);

        var front = tiles.Single(t => t.Entry.FileName == "drip_blue_front_v1.png");   // 128x150
        Assert.Equal(("drip_blue_front_v1", "drip_blue_front_v1.png"), (front.Caption, front.ToolTip));
        main.Assets.AddCommand.Execute(front);
        var l = s.Document.Layers[^1];
        double fit = Math.Min(128.0 / 128, 128.0 / 150) * 0.85;                       // 0.7253..., the front slot 231,74,128,128
        Assert.Equal("drip_blue_front_v1", l.Name);
        Assert.Equal(fit, l.Scale, 12);
        Assert.Equal(231 + (128 - 128 * fit) / 2, l.X, 9);
        Assert.Equal(74 + (128 - 150 * fit) / 2, l.Y, 9);
        Assert.Equal((l.Id, 1, Tool.Select), (s.SelectedLayerId!.Value, s.UndoCount, main.Home.Tools.Current));   // editor.js:523

        var wide = tiles.Single(t => t.Entry.FileName == "drip_blue_wide_v1.png");     // 390x200
        main.Assets.AddFullCommand.Execute(wide);
        var full = s.Document.Layers[^1];
        Assert.Equal("drip_blue_wide_v1 (full)", full.Name);
        Assert.Equal((0.0, 0.0), (full.X, full.Y));
        Assert.Equal(Math.Max(585.0 / 390, 559.0 / 200), full.Scale, 12);              // 2.795, unclamped (R15)
        Assert.Equal(2, s.UndoCount);
        Assert.True(s.Undo());
        Assert.DoesNotContain(s.Document.Layers, x => x.Id == full.Id);

        main.Assets.LoadWordArtCommand.Execute(null);
        Assert.Equal("25 images in built-in word_art pack", main.Assets.CountText);
        var words = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(25, words.Count);
        main.Assets.AddCommand.Execute(words[0]);
        Assert.Equal(Path.GetFileNameWithoutExtension(words[0].Entry.FileName), s.Document.Layers[^1].Name);
    });

    [Fact]
    public void A_folder_lists_the_import_formats_dedupes_by_name_keeping_the_deepest_and_shows_the_duplicates_count() => StaThread.Run(() =>
    {
        // asset-open-folder, asset-dedupe-by-name, asset-folder-grouping, asset-count-label, asset-count-label-overwritten,
        // asset-thumb-uuid-label, io-folder-picker (editor.js:2380-2470, :2478-2486, :2544; interfaces R14)
        const string uuid = "1a2b3c4d-1111-2222-3333-444444444444";
        string root = Path.Combine(_dir, "art");
        string Put(string rel, int w, int h, SKColor color)
        {
            string p = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            using var image = Solid(w, h, color);
            File.WriteAllBytes(p, Png(image));
            return p;
        }
        Put(@"a\x.png", 10, 10, Red);
        Put(@"a\sub\x.png", 10, 10, Red);
        Put(@"b\x.png", 10, 10, Red);
        string deepest = Put(@"a\sub\deep\x.png", 12, 7, Blue);
        Put("y.png", 10, 10, Slate);
        Put($@"Cool\{uuid}.png", 9, 9, Blue);
        File.WriteAllBytes(Path.Combine(root, "z.bmp"), TestFiles.Bmp2x1());
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not an image");
        File.WriteAllText(Path.Combine(root, "logo.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        var (s, main, files, dialogs, settings) = Shell4a();
        files.Folders.Enqueue(root);
        main.Assets.OpenFolderCommand.Execute(null);
        Assert.Equal("4 unique images (3 duplicates removed)", main.Assets.CountText);   // editor.js:2448, shown (4.1 overwrote it)
        Assert.Equal(new[] { "All (4)", "Root (2)", "Cool (1)", "deep (1)" }, main.Assets.GroupButtons.Select(g => g.Label));
        var tiles = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(deepest, tiles.Single(t => t.Entry.FileName == "x.png").Entry.FullPath);
        Assert.Contains(tiles, t => t.Entry.FileName == "z.bmp");                        // R14: the import formats, .bmp included
        Assert.DoesNotContain(tiles, t => t.Entry.FileName is "notes.txt" or "logo.svg");
        Assert.Equal(root, settings.Folders[SettingsFolder.Asset]);

        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons.Single(g => g.Key == "Cool"));
        Assert.Equal("1 images in Cool", main.Assets.CountText);                          // editor.js:2544
        var tile = Assert.Single(main.Assets.Rows.SelectMany(r => r.Tiles));
        Assert.Equal("Cool", tile.Caption);                                                // a UUID name shows its folder
        main.Assets.AddCommand.Execute(tile);
        Assert.Equal(uuid, s.Document.Layers[^1].Name);
        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons[0]);
        Assert.Equal("4 images in all folders", main.Assets.CountText);

        string empty = Directory.CreateDirectory(Path.Combine(_dir, "empty")).FullName;
        files.Folders.Enqueue(empty);
        main.Assets.OpenFolderCommand.Execute(null);
        Assert.Equal("No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files.", dialogs.Alerts[^1]);   // editor.js:2385
    });

    [Fact]
    public void A_thumbnail_or_a_file_dropped_on_the_canvas_lands_centered_on_the_drop_point_at_every_zoom() => StaThread.Run(() =>
    {
        // asset-drag-to-canvas, drop-canvas (editor.js:961-986 the canvas drop; :500-526 addLayer's drop placement; interfaces R6)
        var (s, main, _, _, _) = Shell4a();
        main.Assets.LoadDripsCommand.Execute(null);
        var front = main.Assets.Rows.SelectMany(r => r.Tiles).Single(t => t.Entry.FileName == "drip_blue_front_v1.png");
        foreach (double zoom in new[] { 1.0, 2.0 })
        {
            main.Assets.DropOnCanvas(front, CanvasCoordinates.ToTemplate(new Point(300 * zoom, 200 * zoom), zoom));
            var l = s.Document.Layers[^1];
            Assert.Equal(300, l.X + 128 * l.Scale / 2, 9);
            Assert.Equal(200, l.Y + 150 * l.Scale / 2, 9);
        }
        Assert.Equal(2, s.UndoCount);

        string png = WritePng("dropped.png", 40, 20, Blue);
        foreach (double zoom in new[] { 0.5, 1.0, 2.0 })
            foreach (var (tx, ty) in new[] { (10.0, 10.0), (150.0, 300.0), (292.5, 279.5), (575.0, 549.0) })
            {
                main.Canvas.DropHandler!(new CanvasDrop([png], CanvasCoordinates.ToTemplate(new Point(tx * zoom, ty * zoom), zoom)));
                var l = s.Document.Layers[^1];
                Assert.Equal("dropped", l.Name);
                Assert.Equal(tx, l.X + 40 * l.Scale / 2, 9);
                Assert.Equal(ty, l.Y + 20 * l.Scale / 2, 9);
            }
        Assert.Equal(14, s.UndoCount);
    });

    [Fact]
    public void Stored_values_out_of_range_open_raw_as_4_1_opens_them() => StaThread.Run(() =>
    {
        // project-load-layers note (interfaces R12; editor.js:2632-2660 copies each stored field as it is)
        using var art = Solid(20, 10, Red);
        var one = new Document { Layers = ImmutableList.Create(new Layer { Id = 4, Image = art, Name = "raw" }) };
        var node = JsonNode.Parse(ProjectIO.Write(one, DateTimeOffset.UnixEpoch))!;
        var layer = node["layers"]![0]!;
        layer["rotation"] = 720;
        layer["scale"] = 9;
        layer["opacity"] = 2.5;
        layer["brightness"] = 500;
        layer["hueRotate"] = -90;
        layer["x"] = 5000;
        string path = WriteText("raw.r15proj", node.ToJsonString());
        var (s, main, _, _, _) = Shell4a();
        Assert.True(Await(main.Files.OpenAsync(path)));
        var l = Assert.Single(s.Document.Layers);
        Assert.Equal((720.0, 9.0, 2.5, 500.0, -90.0, 5000.0), (l.Rotation, l.Scale, l.Opacity, l.Brightness, l.HueRotate, l.X));
        Assert.Equal(5, s.NextId);
    });

    [Fact]
    public void Opening_a_project_with_a_camo_region_leaves_fill_style_at_its_defaults() => StaThread.Run(() =>
    {
        // fillstyle-ui-not-synced-from-region (editor.js:1599-1613, :2594-2690): the Fill Style controls never read back
        // from a region, a project load or an undo. Since 2026-10-25 the group is on the Layout (Background) tab, so it is main.Layout.FillStyle.
        var camo = new Fill("camo", "#556b2f", "#c2b280", 33, 24);
        string path = WriteText("camo.r15proj", ProjectIO.Write(DocumentOps.PaintRegion(new Document(), "torso_front", camo), DateTimeOffset.UnixEpoch));
        var (s, main, _, _, _) = Shell4a();
        var style = main.Layout.FillStyle;
        Assert.Equal(("solid", "#17d4fa", "#f22f89", 45.0, 16.0), (style.Type, style.Color1, style.Color2, style.Angle, style.Scale));

        Assert.True(Await(main.Files.OpenAsync(path)));

        var loaded = DocumentOps.RegionFill(s.Document, "torso_front")!;     // the camo region really opened ...
        Assert.Equal(("camo", "#556b2f", "#c2b280", 33.0, 24.0), (loaded.Type, loaded.C1, loaded.C2, loaded.Angle, loaded.Scale));
        Assert.Equal("camo", main.Layout.Regions.Body.Single(t => t.Region == "torso_front").Fill!.Type);
        Assert.Equal(FillStyleViewModel.DefaultFill, style.Current);        // ... and the controls did not move
        Assert.Equal(("solid", "#17d4fa", "#f22f89", 45.0, 16.0), (style.Type, style.Color1, style.Color2, style.Angle, style.Scale));

        // and a style the user chose first survives the next open too (the controls are the user's, not the file's)
        style.SetTypeCommand.Execute("stripes");
        style.Color1 = "#123456";
        style.Angle = 10;
        Assert.Equal(0, s.UndoCount);                                        // no painted region matches, so these recorded nothing
        Assert.True(Await(main.Files.OpenAsync(path)));
        Assert.Equal(("stripes", "#123456", "#f22f89", 10.0, 16.0), (style.Type, style.Color1, style.Color2, style.Angle, style.Scale));
        Assert.Equal("camo", DocumentOps.RegionFill(s.Document, "torso_front")!.Type);
    });

    static (EditorSession Session, MainViewModel Main) Shell()
    {
        var s = new EditorSession();
        return (s, TestShell.Main(s));
    }

    /// <summary>Adds a scale-1 layer at (x, y) and selects it, as one undo step (the way an import lands).</summary>
    static int AddLayer(EditorSession s, SKImage image, double x, double y, string name)
    {
        int id = s.TakeId();
        var layer = new Layer { Id = id, Image = image, Name = name, X = x, Y = y };
        Assert.True(s.ApplyWithSelection(snap => new EditorSnapshot(snap.Document with { Layers = snap.Document.Layers.Add(layer) }, id)));
        return id;
    }

    static Layer LayerOf(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    /// <summary>The point <paramref name="r"/> template px from the center at a 4.1 pointer angle (atan2(dx, -dy), editor.js:1176).</summary>
    static Point At(double cx, double cy, double degrees, double r) =>
        new(cx + r * Math.Sin(degrees * Math.PI / 180), cy - r * Math.Cos(degrees * Math.PI / 180));

    static byte[] Png(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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

    // ─── M4a Task 1 scenario: save, edit, undo to clean, open, and the prompt only for unsaved work ───

    [Fact]
    public async System.Threading.Tasks.Task Save_open_and_the_unsaved_prompt_through_the_real_composition()
    {
        // project-save-format, project-load-flow, project-load-history-interplay, shell-no-unsaved-changes-guard,
        // home-file-group (editor.js:2550-2687): Save work, an edit undone back to the save, then Open through the
        // File group's commands, with the canvas's Changing and Replaced wiring live.
        using var red = Solid(20, 20, Red);
        var s = new EditorSession();
        var dialogs = new Dialogs();
        var files = new Files(null);
        var main = TestShell.Quiet(Composition.Build(s, files, dialogs, new Picker(), new Fakes.FakeSettingsStore(), TestShell.Storage(), new Fakes.ManualTickSource(), new Fakes.FakeProcessProbe()));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, red, s.TakeId(), "art")));
        string first = Path.Combine(_dir, "first.r15proj");
        files.ProjectPath = first;

        main.Home.File.SaveCommand.Execute(null);                       // untitled: Save As with the template name's file name
        Assert.Equal("untitled.r15proj", Assert.Single(files.ProjectSuggested));
        Assert.Equal("first.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);

        main.Canvas.Interaction.Wheel(-1);                              // a wheel burst, still open: the title reads dirty at once (R2)
        Assert.Equal("*first.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        Assert.True(s.Undo());                                          // Undo closes the burst as one step and undoes it: back to the save
        Assert.False(s.IsDirty);
        Assert.True(main.Files.ConfirmDiscardChanges());                // clean: no prompt
        Assert.Empty(dialogs.SavePrompts);

        string second = WriteText("second.r15proj", """{ "mode": "Pants", "bgColor": "#112233" }""");
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        files.ProjectPath = second;
        dialogs.SaveAnswer = SaveChoice.DontSave;
        await main.Home.File.OpenCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "first.r15proj" }, dialogs.SavePrompts);
        Assert.Equal((TemplateMode.Pants, "#112233"), (s.Document.Mode, s.Document.BgColor));
        Assert.Empty(main.Layers.Rows);
        Assert.False(s.Undo());                                         // history cleared on open
        Assert.Equal("second.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        var reread = ProjectIO.Read(File.ReadAllText(first));
        Assert.Equal("art", Assert.Single(reread.Document.Layers).Name);   // first.r15proj kept the save, not the unsaved grid toggle
        Assert.False(reread.Document.ShowGrid);
    }

    /// <summary>The M4a scenarios' shell: the real composition with recording fakes for the dialogs and the settings file.</summary>
    static (EditorSession Session, MainViewModel Main, ProjectDialogs Files, PromptDialogs Dialogs, MemorySettings Settings) Shell4a()
    {
        var s = new EditorSession();
        var files = new ProjectDialogs();
        var dialogs = new PromptDialogs();
        var settings = new MemorySettings();
        return (s, TestShell.Quiet(Composition.Build(s, files, dialogs, new Picker(), settings, TestShell.Storage(), new Fakes.ManualTickSource(), new Fakes.FakeProcessProbe())), files, dialogs, settings);
    }

    /// <summary>Pumps the STA dispatcher until <paramref name="task"/> completes: Open reads on the pool and resumes here.</summary>
    static T Await<T>(Task<T> task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>File dialogs that answer from queues (an empty queue is a cancel) and record the Save dialog's suggestions.</summary>
    sealed class ProjectDialogs : IFileDialogs
    {
        public Queue<string?> Saves { get; } = new();
        public Queue<string?> Opens { get; } = new();
        public Queue<string?> Folders { get; } = new();
        public List<string> SuggestedProjects { get; } = [];

        public IReadOnlyList<string> OpenImages() => [];

        public string? SavePng(string suggestedFileName) => null;

        public string? OpenProject(string? initialDirectory) => Opens.Count > 0 ? Opens.Dequeue() : null;

        public string? SaveProject(string suggestedFileName, string? initialDirectory)
        {
            SuggestedProjects.Add(suggestedFileName);
            return Saves.Count > 0 ? Saves.Dequeue() : null;
        }

        public string? PickFolder(string? initialDirectory) => Folders.Count > 0 ? Folders.Dequeue() : null;
    }

    /// <summary>Themed dialogs that record every message; the save prompt answers from a queue (empty: Cancel).</summary>
    sealed class PromptDialogs : IDialogService
    {
        public Queue<SaveChoice> Choices { get; } = new();
        public List<string> SaveChanges { get; } = [];
        public List<string> Alerts { get; } = [];

        public bool Confirm(string message) => true;

        public void Alert(string message) => Alerts.Add(message);

        public SaveChoice AskSaveChanges(string documentName)
        {
            SaveChanges.Add(documentName);
            return Choices.Count > 0 ? Choices.Dequeue() : SaveChoice.Cancel;
        }

        public RestoreChoice AskRestore(string documentName, DateTimeOffset autosavedAt) => RestoreChoice.Later;
    }

    /// <summary>An in-memory settings file that keeps the remembered folders.</summary>
    sealed class MemorySettings : ISettingsStore
    {
        public Dictionary<SettingsFolder, string> Folders { get; } = [];

        public WindowPlacement? LoadWindowPlacement() => null;

        public void SaveWindowPlacement(WindowPlacement placement) { }

        public string? LoadFolder(SettingsFolder which) => Folders.GetValueOrDefault(which);

        public void SaveFolder(SettingsFolder which, string folder) => Folders[which] = folder;

        public IReadOnlyList<string> LoadRecents() => [];

        public void SaveRecents(IReadOnlyList<string> paths) { }
    }

    sealed class Picker(params string?[] answers) : IColorPicker
    {
        readonly Queue<string?> _answers = new(answers);
        public List<string> Opened { get; } = [];

        public string? Pick(string initialHex, Action<string>? preview = null, object? anchor = null)
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

        public SaveChoice SaveAnswer { get; set; } = SaveChoice.Cancel;

        public List<string> SavePrompts { get; } = [];

        public SaveChoice AskSaveChanges(string documentName)
        {
            SavePrompts.Add(documentName);
            return SaveAnswer;
        }

        public RestoreChoice AskRestore(string documentName, DateTimeOffset autosavedAt) => RestoreChoice.Later;
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

        public string? ProjectPath { get; set; }

        public List<string> ProjectSuggested { get; } = [];

        public string? OpenProject(string? initialDirectory) => ProjectPath;

        public string? SaveProject(string suggestedFileName, string? initialDirectory)
        {
            ProjectSuggested.Add(suggestedFileName);
            return ProjectPath;
        }

        public string? PickFolder(string? initialDirectory) => null;
    }

    sealed class Status : IStatus
    {
        public List<string> Shown { get; } = [];
        public void Show(string message) => Shown.Add(message);
        public void Clear() { }
    }
}
