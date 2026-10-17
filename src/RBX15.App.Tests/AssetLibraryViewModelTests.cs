using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using RBX15.App.Canvas;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The asset library's view-model (editor.js:2302-2545; ledger panel-asset-library and the asset-* and pack* rows).
/// Placement literals are 4.1's own doubles: node evaluated addLayer (editor.js:502-511) and the right-click scale
/// (editor.js:2525) for each size and printed them.
/// </summary>
public class AssetLibraryViewModelTests
{
    sealed record Rig(AssetLibraryViewModel Vm, EditorSession Session, FakeFileDialogs Files, FakeDialogService Dialogs,
        FakeStatus Status, FakeSettingsStore Settings);

    static Rig Make(string? packsDirectory = null, AssetThumbnailLoader? thumbnails = null, TemplateMode mode = TemplateMode.Shirt)
    {
        var session = new EditorSession(new Document { Mode = mode });
        var status = new FakeStatus();
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var settings = new FakeSettingsStore();
        var vm = new AssetLibraryViewModel(new ImportService(session, status), files, dialogs, status, settings,
            new PackSource(packsDirectory ?? PackSource.DefaultDirectory), thumbnails ?? new AssetThumbnailLoader());
        return new Rig(vm, session, files, dialogs, status, settings);
    }

    /// <summary>Writes a PNG at a '/'-separated path under <paramref name="files"/>' folder, creating folders.</summary>
    static string Png(TestFiles files, string relative, int w = 8, int h = 8)
    {
        string path = Path.Combine(files.Dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Red);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    static string[] Names(AssetLibraryViewModel vm) => vm.Rows.SelectMany(r => r.Tiles).Select(t => t.Entry.FileName).ToArray();

    static void WaitUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("the thumbnail never arrived");
            StaThread.Pump();
            Thread.Sleep(5);
        }
    }

    [Fact] // packs-load-builtin, pack-builtin-load, pack-load-sync-and-mime: 156 sorted, the pack's own label, no buttons, 3 per row
    public void Drips_loads_156_sorted_with_its_own_count_and_no_group_buttons()
    {
        var r = Make();
        Assert.False(r.Vm.IsBrowserVisible);
        Assert.Equal("", r.Vm.CountText);
        r.Vm.LoadDripsCommand.Execute(null);
        Assert.True(r.Vm.IsBrowserVisible);
        Assert.Equal("156 images in built-in drips pack", r.Vm.CountText);
        Assert.Empty(r.Vm.GroupButtons);
        Assert.Equal(52, r.Vm.Rows.Count);
        Assert.All(r.Vm.Rows, row => Assert.Equal(3, row.Tiles.Count));
        var names = Names(r.Vm);
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
        Assert.Equal("drip_blood_front_flipped.png", names[0]);
        Assert.Equal(AssetScan.AllKey, r.Vm.CurrentGroup!.Key);
        Assert.DoesNotContain(r.Vm.Rows.SelectMany(x => x.Tiles), t => t.ThumbnailRequested);   // nothing decodes until a tile is shown
        Assert.Equal(0, r.Session.UndoCount);
    }

    [Fact] // packs-inventory-wordart: 25 items, the last row holds one; the folder token in the label (pack-builtin-load quirk)
    public void Word_art_loads_25_and_replaces_the_previous_set()
    {
        var r = Make();
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.LoadWordArtCommand.Execute(null);
        Assert.Equal("25 images in built-in word_art pack", r.Vm.CountText);
        Assert.Equal(9, r.Vm.Rows.Count);
        Assert.Single(r.Vm.Rows[^1].Tiles);
        Assert.Equal(25, Names(r.Vm).Length);
        Assert.All(Names(r.Vm), n => Assert.StartsWith("word_", n));
    }

    [Fact] // packs-resolve-dir: a missing pack alerts 4.1's text (editor.js:2344) and leaves the browser as it was
    public void A_missing_pack_alerts_and_changes_nothing()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        r.Vm.LoadDripsCommand.Execute(null);
        Assert.Equal(["Built-in pack \"drips\" not found. Reinstall the app to restore it."], r.Dialogs.Alerts);
        Assert.False(r.Vm.IsBrowserVisible);
        Assert.Empty(r.Vm.Rows);
    }

    [Fact] // a pack folder with no images alerts 4.1's text (editor.js:2364) and keeps what was shown
    public void An_empty_pack_alerts_and_keeps_the_shown_set()
    {
        using var files = new TestFiles();
        Png(files, "drips/one.png");
        Directory.CreateDirectory(Path.Combine(files.Dir, "word_art"));
        File.WriteAllText(Path.Combine(files.Dir, "word_art", "README.md"), "only bundle assets you own");
        var r = Make(files.Dir);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.LoadWordArtCommand.Execute(null);
        Assert.Equal(["Built-in pack \"word_art\" is empty."], r.Dialogs.Alerts);
        Assert.Equal(["one.png"], Names(r.Vm));
        Assert.Equal("1 images in built-in drips pack", r.Vm.CountText);
    }

    [Fact] // asset-open-folder, asset-dedupe-by-name, asset-folder-grouping, asset-count-label (fix), io-folder-picker
    public void Open_folder_lists_dedupes_groups_and_shows_the_duplicates_count()
    {
        using var files = new TestFiles();
        Png(files, "art/top.png");
        Png(files, "art/a/x.png");
        Png(files, "art/a/sub/x.png");
        Png(files, "art/b/y.PNG");
        Png(files, "art/b/z.webp");
        File.WriteAllText(Path.Combine(files.Dir, "art", "notes.txt"), "not an image");
        var r = Make();
        r.Settings.SaveFolder(SettingsFolder.Asset, @"C:\last");
        r.Files.PickFolderResult = Path.Combine(files.Dir, "art");
        r.Vm.OpenFolderCommand.Execute(null);

        Assert.Equal([@"C:\last"], r.Files.PickFolderDirs);                 // last folder remembered (spec §3)
        Assert.Equal("4 unique images (1 duplicates removed)", r.Vm.CountText);        // shown, not overwritten (editor.js:2448-2449)
        Assert.Equal(["All (4)", "Root (1)", "b (2)", "sub (1)"], r.Vm.GroupButtons.Select(g => g.Label));
        Assert.Equal(["x.png", "y.PNG", "z.webp", "top.png"], Names(r.Vm));   // sorted paths, x.png in its first-seen slot
        Assert.Equal("art/a/sub/x.png", r.Vm.Set!.Entries.Single(e => e.FileName == "x.png").RelativePath);   // the deepest copy
        Assert.Equal(Path.Combine(files.Dir, "art"), r.Settings.LoadFolder(SettingsFolder.Asset));
        Assert.Equal(AssetScan.AllKey, r.Vm.CurrentGroup!.Key);
    }

    [Fact] // a cancelled picker does nothing and remembers nothing
    public void A_cancelled_folder_pick_changes_nothing()
    {
        var r = Make();
        r.Vm.OpenFolderCommand.Execute(null);
        Assert.Equal([null], r.Files.PickFolderDirs);
        Assert.False(r.Vm.IsBrowserVisible);
        Assert.Null(r.Settings.LoadFolder(SettingsFolder.Asset));
        Assert.Empty(r.Dialogs.Alerts);
    }

    [Fact] // a folder with no images alerts 4.1's text (editor.js:2385) and the existing browser state is left unchanged
    public void A_folder_without_images_alerts_and_keeps_the_shown_set()
    {
        using var files = new TestFiles();
        Directory.CreateDirectory(Path.Combine(files.Dir, "empty"));
        File.WriteAllText(Path.Combine(files.Dir, "empty", "a.svg"), "<svg/>");
        var r = Make();
        r.Vm.LoadWordArtCommand.Execute(null);
        r.Files.PickFolderResult = Path.Combine(files.Dir, "empty");
        r.Vm.OpenFolderCommand.Execute(null);
        Assert.Equal(["No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files."], r.Dialogs.Alerts);
        Assert.Equal(25, Names(r.Vm).Length);
        Assert.Null(r.Settings.LoadFolder(SettingsFolder.Asset));
    }

    [Fact] // a folder that cannot be listed is named on the status strip (diag-console-only-errors)
    public void A_folder_that_cannot_be_listed_goes_to_the_status_strip()
    {
        using var files = new TestFiles();
        var r = Make();
        string gone = Path.Combine(files.Dir, "gone");
        r.Files.PickFolderResult = gone;
        r.Vm.OpenFolderCommand.Execute(null);
        Assert.NotNull(r.Status.Current);
        Assert.StartsWith("Couldn't open gone: ", r.Status.Current);
        Assert.Empty(r.Dialogs.Alerts);
        Assert.False(r.Vm.IsBrowserVisible);
    }

    [Fact] // showAssetFolder (editor.js:2452-2466, :2544): the switch keeps the tiles (asset-objecturl-lifecycle) and sets the label
    public void A_group_switch_reuses_the_tiles_and_sets_the_count_text()
    {
        using var files = new TestFiles();
        Png(files, "art/top.png");
        Png(files, "art/s/1.png");
        Png(files, "art/s/2.png");
        var r = Make();
        r.Files.PickFolderResult = Path.Combine(files.Dir, "art");
        r.Vm.OpenFolderCommand.Execute(null);
        var before = r.Vm.Rows.SelectMany(x => x.Tiles).ToDictionary(t => t.Entry.FileName);
        var s = r.Vm.GroupButtons.Single(g => g.Key == "s");
        r.Vm.ShowGroupCommand.Execute(s);
        Assert.Same(s, r.Vm.CurrentGroup);
        Assert.Equal("2 images in s", r.Vm.CountText);
        Assert.All(r.Vm.Rows.SelectMany(x => x.Tiles), t => Assert.Same(before[t.Entry.FileName], t));
        r.Vm.ShowGroupCommand.Execute(r.Vm.GroupButtons[0]);
        Assert.Equal("3 images in all folders", r.Vm.CountText);
        r.Vm.ShowGroupCommand.Execute(r.Vm.GroupButtons[1]);
        Assert.Equal("1 images in (root)", r.Vm.CountText);
    }

    [Theory] // asset-click-add parityCheck: drip_blue_front_v1 (128x150) fit at 85%, 4.1's doubles; Pants uses its slot (same geometry)
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void A_click_adds_the_file_fit_in_the_front_slot_as_one_step(TemplateMode mode)
    {
        using var files = new TestFiles();
        var r = Make(files.Dir, mode: mode);
        Png(files, "drips/drip_blue_front_v1.png", 128, 150);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.AddCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        var l = r.Session.SelectedLayer!;
        Assert.Equal(("drip_blue_front_v1", 248.57866666666666, 83.6, 0.7253333333333334), (l.Name, l.X, l.Y, l.Scale));
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Empty(r.Session.Document.Layers);
    }

    [Fact] // asset-rightclick-full parityCheck: a 390x200 drip, scale 2.795, at 0,0, "(full)", undo removes it
    public void A_right_click_adds_the_file_at_full_size_as_one_step()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        Png(files, "drips/drip_blue_wide_v1.png", 390, 200);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.AddFullCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        var l = r.Session.SelectedLayer!;
        Assert.Equal(("drip_blue_wide_v1 (full)", 0.0, 0.0, 2.795), (l.Name, l.X, l.Y, l.Scale));
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Empty(r.Session.Document.Layers);
    }

    [Fact] // asset-drag-to-canvas: centered on the drop point (editor.js:505-507), 4.1's doubles, one step
    public void A_canvas_drop_centers_the_file_on_the_drop_point()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        Png(files, "drips/drip_blue_front_v1.png", 128, 150);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.DropOnCanvas(r.Vm.Rows[0].Tiles[0], new Point(300, 200));
        var l = r.Session.SelectedLayer!;
        Assert.Equal((253.57866666666666, 145.6, 0.7253333333333334), (l.X, l.Y, l.Scale));
        Assert.Equal(1, r.Session.UndoCount);
    }

    [Fact] // a file removed after the listing: named on the status strip (diag-console-only-errors), no step
    public void A_file_gone_since_the_listing_is_named_on_the_status_strip()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        string path = Png(files, "drips/gone.png");
        r.Vm.LoadDripsCommand.Execute(null);
        File.Delete(path);
        r.Vm.AddCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        r.Vm.AddFullCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        Assert.Equal(["Couldn't add gone.png: The file couldn't be read.", "Couldn't add gone.png: The file couldn't be read."], r.Status.Shown);
        Assert.Equal(0, r.Session.UndoCount);
    }

    [Fact] // addLayer and the right-click add both set the Select tool (editor.js:523, :2532), through the real composition
    public void Library_adds_revert_the_tool_to_select()
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        main.Assets.LoadWordArtCommand.Execute(null);
        main.Home.Tools.Set(Tool.Pen);
        main.Assets.AddCommand.Execute(main.Assets.Rows[0].Tiles[0]);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        main.Home.Tools.Set(Tool.Eraser);
        main.Assets.AddFullCommand.Execute(main.Assets.Rows[0].Tiles[0]);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        Assert.Equal(["word_626_anton", "word_626_anton (full)"], session.Document.Layers.Select(l => l.Name));
        Assert.Equal(2, session.UndoCount);
    }

    [Fact] // the folder filter is import's (R14), and the entry's layer name is the one import gives
    public void The_folder_filter_and_layer_names_match_the_importer()
    {
        Assert.Equal(ImageImporter.Extensions, AssetScan.Extensions);
        foreach (var name in new[] { "a.png", "a.b.png", ".png", "file.", "UPPER.JPEG" })
        {
            var entry = AssetScan.FromPack("p", [@"C:\p\" + name]).Entries.SingleOrDefault();
            if (entry is not null) Assert.Equal(ImageImporter.LayerName(name), entry.LayerName);
        }
    }

    [Theory] // the resize clamp: Math.max(80, Math.min(innerHeight * 0.7, startH + delta)) (editor.js:2732); non-finite ignored
    [InlineData(180, 180)]
    [InlineData(50, 80)]
    [InlineData(300, 300)]
    [InlineData(10000, 532.7)]
    [InlineData(double.NaN, 180)]
    [InlineData(double.PositiveInfinity, 180)]
    public void The_browser_height_clamps_to_80_and_70_percent(double set, double expected)
    {
        var r = Make();
        Assert.Equal(180, r.Vm.BrowserHeight);
        r.Vm.MaxBrowserHeight = 0.7 * 761;
        r.Vm.BrowserHeight = set;
        Assert.Equal(expected, r.Vm.BrowserHeight, 9);
    }

    [Fact] // the lower bound wins when 70% of a short window is under 80 (Math.max outside Math.min)
    public void The_lower_clamp_wins_in_a_short_window()
    {
        var r = Make();
        r.Vm.MaxBrowserHeight = 0.7 * 100;
        r.Vm.BrowserHeight = 300;
        Assert.Equal(80, r.Vm.BrowserHeight);
    }

    [Fact] // R5: the first read queues the decode, the property changes when it lands; a damaged file marks the tile failed
    public void A_tile_decodes_on_first_read_and_reports_a_failure() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        Png(files, "drips/a_ok.png", 400, 100);
        Directory.CreateDirectory(Path.Combine(files.Dir, "drips"));
        File.WriteAllBytes(Path.Combine(files.Dir, "drips", "b_bad.png"), [1, 2, 3]);
        r.Vm.LoadDripsCommand.Execute(null);
        var ok = r.Vm.Rows[0].Tiles[0];
        var bad = r.Vm.Rows[0].Tiles[1];
        var changed = new List<string?>();
        ok.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(ok.ThumbnailRequested);
        Assert.Null(ok.Thumbnail);
        Assert.Null(bad.Thumbnail);
        WaitUntil(() => ok.Thumbnail is not null && bad.ThumbnailFailed);
        Assert.Equal([nameof(AssetTileViewModel.Thumbnail)], changed);
        Assert.Equal((160, 40), (ok.Thumbnail!.PixelWidth, ok.Thumbnail.PixelHeight));
        Assert.Null(bad.Thumbnail);
        Assert.False(ok.ThumbnailFailed);
    });

    [Fact] // a new load cancels the old set's queued decodes (asset-objecturl-lifecycle): an old tile never gets a picture
    public void A_new_load_cancels_the_previous_sets_queued_thumbnails() => StaThread.Run(() =>
    {
        using var gate = new ManualResetEventSlim();
        var decoded = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var loader = new AssetThumbnailLoader(p =>
        {
            decoded.Enqueue(Path.GetFileName(p));
            gate.Wait(TimeSpan.FromSeconds(10));
            var pixel = BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, new byte[4], 4);
            pixel.Freeze();
            return pixel;
        });
        var r = Make(thumbnails: loader);
        r.Vm.LoadDripsCommand.Execute(null);
        var first = r.Vm.Rows[0].Tiles[0];
        var second = r.Vm.Rows[0].Tiles[1];
        _ = first.Thumbnail;                                    // the worker takes it and blocks
        Assert.True(SpinWait.SpinUntil(() => !decoded.IsEmpty, TimeSpan.FromSeconds(10)));
        _ = second.Thumbnail;                                   // queued behind it
        r.Vm.LoadWordArtCommand.Execute(null);                  // a new set: the queued request is cancelled
        gate.Set();
        WaitUntil(() => first.Thumbnail is not null);           // a decode already running is delivered
        StaThread.Pump();
        Assert.Equal([first.Entry.FileName], decoded);
        Assert.Null(second.Thumbnail);
        Assert.False(second.ThumbnailFailed);
    });
}
