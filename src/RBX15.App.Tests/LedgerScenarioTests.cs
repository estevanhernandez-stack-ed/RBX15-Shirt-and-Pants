using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using Xunit;
using Point = System.Windows.Point;

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

        main.Home.Regions.Body.Single(t => t.Region == "torso_front").PaintCommand.Execute(null);
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
