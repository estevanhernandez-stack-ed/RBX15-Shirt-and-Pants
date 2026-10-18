using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The Background and BG Settings groups on the Layout tab (editor.html:125-144, editor.js:916-946; ledger bg-palette,
/// bg-swatches, layout-background-swatches, bg-color-opacity, bg-image, layout-bg-settings, bg-color-pick-clears-image,
/// io-image-formats, image-decode-format-matrix), with decision B1: a pick on a 0% background also sets 100%.
/// </summary>
[Collection("MainWindow")] // the window facts build a MainWindow, as ShellLookTests does
public class BackgroundGroupTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    sealed record Rig(EditorSession Session, BackgroundViewModel Bg, FakeColorPicker Picker, FakeFileDialogs Dialogs, FakeStatus Status);

    static Rig Make(Document? doc = null)
    {
        var session = new EditorSession(doc);
        var picker = new FakeColorPicker();
        var dialogs = new FakeFileDialogs();
        var status = new FakeStatus();
        return new Rig(session, new BackgroundViewModel(session, picker, dialogs, status), picker, dialogs, status);
    }

    /// <summary>A document with an opaque Navy background, so B1 does not apply.</summary>
    static Document Opaque() => new() { BgColor = "#192e44", BgOpacity = 1 };

    static SKColor PixelOf(SKImage image, int x, int y)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    static SKColor AtUnpremul(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixel(x, y);
    }

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    static void Invoke(Button b)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
    }

    static void Layout(FrameworkElement view, double width = 1200, double height = 200)
    {
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();
    }

    // ---- the palette (bg-palette, bg-swatches)

    [Fact] // editor.js:66-71: 12 swatches, 4.1's order, names, hex values
    public void The_twelve_swatches_match_4_1s_order_names_and_hex_values()
    {
        Assert.Equal(
            new[]
            {
                ("Navy", "#192e44"), ("Navy Deep", "#0f1f31"), ("Black", "#050c18"), ("Charcoal", "#2d2d2d"), ("Wine", "#4a1942"),
                ("Forest", "#1b4332"), ("Ocean", "#0c2461"), ("Storm", "#2c3e50"), ("White", "#f5f5f5"), ("Red", "#b71c1c"),
                ("Magenta", "#f22f89"), ("Cyan", "#17d4fa"),
            },
            Make().Bg.Swatches.Select(s => (s.Name, s.Hex)));
        Assert.Equal("#192e44", BackgroundViewModel.DefaultColor);   // editor.js:78: the default bgColor is Navy
    }

    public static TheoryData<string, string> EverySwatch()
    {
        var data = new TheoryData<string, string>();
        foreach (var s in BackgroundViewModel.StandardSwatches) data.Add(s.Name, s.Hex);
        return data;
    }

    [Theory] // bg-swatches parityCheck: click each: bg = hex, image removed, one undo restores the previous background
    [MemberData(nameof(EverySwatch))]
    public void Each_swatch_sets_its_color_clears_the_image_and_undoes_in_one_step(string name, string hex)
    {
        var image = Px(8, 8, SKColors.Red);
        var r = Make(new Document { BgColor = "#123456", BgOpacity = 0.6, BgImage = image });

        r.Bg.SetSwatchCommand.Execute(hex);

        Assert.Equal((hex, null, 0.6), (r.Session.Document.BgColor, r.Session.Document.BgImage, r.Session.Document.BgOpacity));
        Assert.Equal(hex, r.Bg.Color);
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Equal(("#123456", 0.6), (r.Session.Document.BgColor, r.Session.Document.BgOpacity));
        Assert.Same(image, r.Session.Document.BgImage);   // undo brings the image back (bg-color-pick-clears-image)
        Assert.False(r.Session.CanUndo);
        Assert.NotEmpty(name);
    }

    [Fact] // no empty undo steps: the swatch the background already shows records nothing
    public void A_swatch_that_changes_nothing_records_no_step()
    {
        var r = Make(Opaque());
        r.Bg.SetSwatchCommand.Execute("#192e44");
        Assert.Equal(0, r.Session.UndoCount);
    }

    // ---- B1: the transparent default

    [Fact] // B1: a swatch at opacity 0 also gives opacity 100, in the same single undo step
    public void B1_a_swatch_on_a_new_document_also_sets_opacity_to_100_percent_in_one_step()
    {
        var r = Make();   // EditorSession.NewDocument(): BgOpacity 0
        Assert.Equal(0, r.Session.Document.BgOpacity);

        r.Bg.SetSwatchCommand.Execute("#b71c1c");

        Assert.Equal(("#b71c1c", 1.0), (r.Session.Document.BgColor, r.Session.Document.BgOpacity));
        Assert.Equal(100, r.Bg.OpacityPercent);
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Equal(("#192e44", 0.0), (r.Session.Document.BgColor, r.Session.Document.BgOpacity));   // one Ctrl+Z puts both back
        Assert.False(r.Session.CanUndo);
    }

    [Fact] // B1 reveals even when the swatch is the color already stored: the pick must show something
    public void B1_applies_when_the_swatch_color_is_already_stored()
    {
        var r = Make();
        r.Bg.SetSwatchCommand.Execute("#192e44");
        Assert.Equal(1.0, r.Session.Document.BgOpacity);
        Assert.Equal(1, r.Session.UndoCount);
    }

    [Theory] // B1 only fires at exactly 0: any other opacity is the user's own
    [InlineData(0.4)]
    [InlineData(0.01)]
    [InlineData(1.0)]
    public void B1_leaves_a_nonzero_opacity_alone(double opacity)
    {
        var r = Make(new Document { BgOpacity = opacity });
        r.Bg.SetSwatchCommand.Execute("#f22f89");
        Assert.Equal(opacity, r.Session.Document.BgOpacity);
    }

    [Fact] // B1: the sentinel path of the plan, a swatch pick on a new document makes torso_front's export pixel the swatch color; the guide is covered; the untouched export is unchanged
    public void B1_a_swatch_on_a_new_document_makes_torso_fronts_export_pixel_the_swatch_color()
    {
        var r = Make();
        using (var before = Renderer.Export(r.Session.Document))
            Assert.Equal(0, PixelOf(before, 295, 138).Alpha);   // untouched: transparent, as v5.1 ships it

        r.Bg.SetSwatchCommand.Execute("#b71c1c");

        using var export = Renderer.Export(r.Session.Document);
        Assert.Equal(new SKColor(0xb7, 0x1c, 0x1c, 0xff), PixelOf(export, 295, 138));
        // The region guide is drawn under the composite, so an opaque background covers it in the view too.
        using var view = Renderer.RenderViewImage(r.Session.Document, new ViewOptions(1) { ShowRegionGuide = true });
        Assert.Equal(new SKColor(0xb7, 0x1c, 0x1c, 0xff), PixelOf(view, 295, 138));
        Assert.Equal(new SKColor(0xb7, 0x1c, 0x1c, 0xff), PixelOf(view, 231, 74));   // the guide outline's own pixel
        // Undo returns the export to transparent.
        Assert.True(r.Session.Undo());
        using var undone = Renderer.Export(r.Session.Document);
        Assert.Equal(0, PixelOf(undone, 295, 138).Alpha);
    }

    // ---- the color well (bg-color-opacity)

    [Fact] // a 30-tick drag through the themed picker is one step, seeded with the current color
    public void A_thirty_tick_color_drag_is_one_undo_step()
    {
        var ticks = Enumerable.Range(0, 30).Select(i => $"#{i * 8:x2}{i * 4:x2}{255 - i * 8:x2}").ToArray();
        var r = Make(Opaque());
        r.Picker.Answer(ticks[^1], ticks);

        r.Bg.PickColorCommand.Execute(null);

        Assert.Equal(new[] { "#192e44" }, r.Picker.Seeds);
        Assert.Equal(ticks[^1], r.Session.Document.BgColor);
        Assert.Equal(1, r.Session.UndoCount);
        Assert.False(r.Session.InGesture);
        Assert.True(r.Session.Undo());
        Assert.Equal("#192e44", r.Session.Document.BgColor);   // returns to the color before the drag
        Assert.False(r.Session.CanUndo);
    }

    [Fact] // the picker is opened under the well (the command parameter)
    public void The_picker_opens_under_the_anchor()
    {
        var r = Make(Opaque());
        var anchor = new object();
        r.Bg.PickColorCommand.Execute(anchor);
        Assert.Same(anchor, Assert.Single(r.Picker.Anchors));
    }

    [Fact] // the live preview clears the image as the input event did (editor.js:930)
    public void A_color_pick_clears_the_image_and_undo_brings_it_back()
    {
        var image = Px(4, 4, SKColors.Blue);
        var r = Make(new Document { BgColor = "#192e44", BgImage = image });
        r.Picker.Answer("#00ff00", "#00aa00", "#00ff00");

        r.Bg.PickColorCommand.Execute(null);

        Assert.Equal(("#00ff00", null), (r.Session.Document.BgColor, r.Session.Document.BgImage));
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Same(image, r.Session.Document.BgImage);
    }

    [Fact] // a cancel puts the opening document back: nothing recorded, the image stays, opacity stays
    public void A_cancelled_color_pick_records_nothing_and_restores_everything()
    {
        var image = Px(4, 4, SKColors.Blue);
        var start = new Document { BgColor = "#192e44", BgOpacity = 0, BgImage = image };
        var r = Make(start);
        r.Picker.Answer(null, "#ff0000", "#00ff00");

        r.Bg.PickColorCommand.Execute(null);

        Assert.Equal(0, r.Session.UndoCount);
        Assert.False(r.Session.InGesture);
        Assert.Same(image, r.Session.Document.BgImage);
        Assert.Equal((0.0, "#192e44"), (r.Session.Document.BgOpacity, r.Session.Document.BgColor));
    }

    [Fact] // OK on the seed color without moving is not an input event in 4.1: it keeps the image and records nothing
    public void Accepting_the_seed_without_moving_changes_nothing()
    {
        var image = Px(4, 4, SKColors.Blue);
        var r = Make(new Document { BgColor = "#192e44", BgOpacity = 1, BgImage = image });
        r.Picker.Answer("#192e44");

        r.Bg.PickColorCommand.Execute(null);

        Assert.Equal(0, r.Session.UndoCount);
        Assert.Same(image, r.Session.Document.BgImage);
    }

    [Fact] // B1 in a pick: the preview reveals a 0% background, the commit keeps it in the one step, a cancel hides it again
    public void B1_in_a_color_pick_reveals_the_background_in_the_same_step()
    {
        var r = Make();
        double duringPreview = -1;
        r.Picker.Answer("#f22f89", "#ff0000", "#f22f89");
        r.Picker.DuringPick = () => duringPreview = r.Session.Document.BgOpacity;

        r.Bg.PickColorCommand.Execute(null);

        Assert.Equal(1.0, duringPreview);
        Assert.Equal(("#f22f89", 1.0), (r.Session.Document.BgColor, r.Session.Document.BgOpacity));
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Equal(0.0, r.Session.Document.BgOpacity);

        var cancelled = Make();
        cancelled.Picker.Answer(null, "#ff0000");
        cancelled.Bg.PickColorCommand.Execute(null);
        Assert.Equal((0.0, 0), (cancelled.Session.Document.BgOpacity, cancelled.Session.UndoCount));
    }

    [Fact] // a picker that throws leaves no preview behind and no gesture open
    public void A_picker_failure_reverts_the_preview()
    {
        var r = Make(Opaque());
        r.Picker.Answer("#00ff00", "#abcdef");
        r.Picker.DuringPick = () => throw new InvalidOperationException("boom");

        Assert.Throws<InvalidOperationException>(() => r.Bg.PickColorCommand.Execute(null));

        Assert.Equal("#192e44", r.Session.Document.BgColor);
        Assert.Equal(0, r.Session.UndoCount);
        Assert.False(r.Session.InGesture);
    }

    [Theory] // a color the well cannot show (a project's "transparent", a short form) reads as the default Navy
    [InlineData("transparent")]
    [InlineData("#fff")]
    [InlineData("")]
    public void A_color_that_is_not_a_plain_hex_reads_as_navy(string stored) =>
        Assert.Equal("#192e44", Make(new Document { BgColor = stored }).Bg.Color);

    // ---- opacity (bg-color-opacity)

    [Fact] // a thumb drag is one gesture: 30 ticks, one step
    public void A_thirty_tick_opacity_drag_is_one_undo_step()
    {
        var r = Make(Opaque());
        using (r.Bg.BeginOpacityDrag())
            for (int i = 1; i <= 30; i++) r.Bg.OpacityPercent = 100 - i;

        Assert.Equal(70, r.Bg.OpacityPercent);
        Assert.Equal(0.7, r.Session.Document.BgOpacity, 10);
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Equal(1.0, r.Session.Document.BgOpacity);
        Assert.False(r.Session.CanUndo);
    }

    [Fact] // 4.1 skipped the snapshot for a keyboard change (no pointerdown); v5 records each arrow press
    public void A_change_outside_a_drag_is_a_step_of_its_own()
    {
        var r = Make(Opaque());
        r.Bg.OpacityPercent = 99;
        r.Bg.OpacityPercent = 98;
        Assert.Equal(2, r.Session.UndoCount);
        r.Bg.OpacityPercent = 98;          // the same value: nothing
        Assert.Equal(2, r.Session.UndoCount);
    }

    [Fact] // a drag that ends where it began records nothing
    public void A_drag_that_returns_to_its_start_records_no_step()
    {
        var r = Make(Opaque());
        using (r.Bg.BeginOpacityDrag())
        {
            r.Bg.OpacityPercent = 40;
            r.Bg.OpacityPercent = 100;
        }
        Assert.Equal(0, r.Session.UndoCount);
    }

    [Fact] // the slider's range is 0..100 whole numbers
    public void Opacity_clamps_to_zero_through_one_hundred()
    {
        var r = Make(Opaque());
        r.Bg.OpacityPercent = 250;
        Assert.Equal(100, r.Bg.OpacityPercent);
        r.Bg.OpacityPercent = -5;
        Assert.Equal(0.0, r.Session.Document.BgOpacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Bg.OpacityPercent = double.NaN);
    }

    [Fact] // an opened project's 0.555 shows 56 and the echo of that never rewrites the document
    public void A_re_sync_echo_does_not_rewrite_a_fractional_opacity()
    {
        var r = Make(new Document { BgOpacity = 0.555 });
        Assert.Equal(56, r.Bg.OpacityPercent);
        r.Bg.OpacityPercent = 56;
        Assert.Equal(0.555, r.Session.Document.BgOpacity);
        Assert.Equal(0, r.Session.UndoCount);
    }

    // ---- re-sync (restoreState, editor.js:1429-1432)

    [Fact] // Undo and Redo bring the color and the opacity controls with them
    public void Undo_and_redo_re_sync_color_and_opacity()
    {
        var r = Make(Opaque());
        var raised = new List<string>();
        ((INotifyPropertyChanged)r.Bg).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        r.Bg.SetSwatchCommand.Execute("#f22f89");
        r.Bg.OpacityPercent = 30;
        Assert.Equal(("#f22f89", 30.0), (r.Bg.Color, r.Bg.OpacityPercent));

        raised.Clear();
        r.Session.Undo();
        Assert.Equal(("#f22f89", 100.0), (r.Bg.Color, r.Bg.OpacityPercent));
        r.Session.Undo();
        Assert.Equal(("#192e44", 100.0), (r.Bg.Color, r.Bg.OpacityPercent));
        Assert.Contains(nameof(BackgroundViewModel.Color), raised);
        Assert.Contains(nameof(BackgroundViewModel.OpacityPercent), raised);
        r.Session.Redo();
        r.Session.Redo();
        Assert.Equal(("#f22f89", 30.0), (r.Bg.Color, r.Bg.OpacityPercent));
    }

    [Fact] // Open swaps the whole document; the controls follow
    public void Open_re_syncs_color_and_opacity()
    {
        var r = Make(Opaque());
        var loaded = new Document { BgColor = "#0c2461", BgOpacity = 0.25 };
        r.Session.Open(new ProjectLoad(loaded, 1, ImmutableList<string>.Empty), @"C:\p\a.r15proj");
        Assert.Equal(("#0c2461", 25.0), (r.Bg.Color, r.Bg.OpacityPercent));
    }

    // ---- the Background image button (bg-image, io-image-formats, image-decode-format-matrix)

    [Fact] // a cancelled dialog does nothing
    public void A_cancelled_image_dialog_changes_nothing()
    {
        var r = Make(Opaque());
        r.Bg.PickImageCommand.Execute(null);
        Assert.Equal((1, 0), (r.Dialogs.OpenImageCalls, r.Session.UndoCount));
        Assert.Empty(r.Status.Shown);
    }

    [Theory] // the matrix through the background route: PNG, JPG, GIF (first frame), WebP, BMP
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("gif")]
    [InlineData("webp")]
    [InlineData("bmp")]
    public void Each_accepted_format_decodes_through_the_background_route(string format)
    {
        using var files = new TestFiles();
        string path = format switch
        {
            "png" => files.Image("bg.png", 6, 4, SKColors.Red),
            "jpg" => files.Image("bg.jpg", 6, 4, SKColors.Red, SKEncodedImageFormat.Jpeg),
            "webp" => files.Image("bg.webp", 6, 4, SKColors.Red, SKEncodedImageFormat.Webp),
            "bmp" => files.Write("bg.bmp", TestFiles.Bmp2x1()),
            _ => files.Write("bg.gif", TestFiles.Gif1x1TwoFrames()),
        };
        var r = Make(new Document { BgColor = "#0f1f31", BgOpacity = 0.5 });
        r.Dialogs.OpenImageResult = path;

        r.Bg.PickImageCommand.Execute(null);

        var image = Assert.IsType<SKImage>(r.Session.Document.BgImage);
        Assert.Equal(format switch { "bmp" => (2, 1), "gif" => (1, 1), _ => (6, 4) }, (image.Width, image.Height));
        var corner = AtUnpremul(image, 0, 0);   // a GIF is its first (red) frame; a JPEG is lossy
        Assert.InRange(corner.Red, 250, 255);
        Assert.InRange(corner.Green + corner.Blue, 0, 12);
        Assert.Equal(255, corner.Alpha);
        Assert.Equal(("#0f1f31", 0.5), (r.Session.Document.BgColor, r.Session.Document.BgOpacity));   // color and opacity stay
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Null(r.Session.Document.BgImage);
    }

    [Fact] // spec §3 and io-image-formats: SVG is refused with a message naming the file, no step
    public void An_svg_gives_the_message_and_no_step()
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Write("logo.svg", File.ReadAllBytes(files.Image("x.png", 2, 2, SKColors.Red)));

        r.Bg.PickImageCommand.Execute(null);

        Assert.Equal(0, r.Session.UndoCount);
        Assert.Null(r.Session.Document.BgImage);
        Assert.Equal("Couldn't use logo.svg as the background: " + ImageImporter.SvgReason, r.Status.Current);
    }

    [Fact] // bg-image fix: a corrupt file leaves no undo entry (4.1 saved one first) and the strip names it
    public void A_corrupt_image_gives_no_step_and_a_status_line()
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Write("broken.png", [1, 2, 3, 4]);

        r.Bg.PickImageCommand.Execute(null);

        Assert.Equal(0, r.Session.UndoCount);
        Assert.Null(r.Session.Document.BgImage);
        Assert.Equal("Couldn't use broken.png as the background: " + ImageImporter.UndecodableReason, r.Status.Current);
    }

    [Fact] // the caps hold on this route too: 20000 px on a side is refused before any pixel memory
    public void An_oversize_image_is_refused_by_the_caps()
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Write("huge.png", TestFiles.PngHeader(20000, 20000));

        r.Bg.PickImageCommand.Execute(null);

        Assert.Equal(0, r.Session.UndoCount);
        Assert.StartsWith("Couldn't use huge.png as the background: ", r.Status.Current);
        Assert.Contains("16384", r.Status.Current);
    }

    [Fact] // a file that vanished between the dialog and the read is named, not thrown
    public void A_missing_file_is_named_on_the_strip()
    {
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = Path.Combine(Path.GetTempPath(), "rbx15-does-not-exist", "gone.png");

        r.Bg.PickImageCommand.Execute(null);

        Assert.Equal(0, r.Session.UndoCount);
        Assert.StartsWith("Couldn't use gone.png as the background:", r.Status.Current);
    }

    [Fact] // file-input-same-file-reselect (fix): choosing the same file twice acts twice, each its own step
    public void Picking_the_same_file_twice_acts_twice()
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Image("bg.png", 4, 4, SKColors.Red);

        r.Bg.PickImageCommand.Execute(null);
        var first = r.Session.Document.BgImage;
        r.Bg.PickImageCommand.Execute(null);

        Assert.Equal(2, r.Session.UndoCount);
        Assert.NotSame(first, r.Session.Document.BgImage);
        Assert.Equal(2, r.Dialogs.OpenImageCalls);
    }

    [Fact] // a success clears a stale failure line, as every import does
    public void A_successful_pick_clears_the_status_strip()
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Write("broken.png", [9, 9]);
        r.Bg.PickImageCommand.Execute(null);
        Assert.NotNull(r.Status.Current);

        r.Dialogs.OpenImageResult = files.Image("ok.png", 3, 3, SKColors.Lime);
        r.Bg.PickImageCommand.Execute(null);

        Assert.Null(r.Status.Current);
    }

    [Fact] // B1 for an image: a new document shows the picture at once, one step
    public void B1_an_image_pick_on_a_new_document_also_sets_opacity_to_100_percent()
    {
        using var files = new TestFiles();
        var r = Make();
        r.Dialogs.OpenImageResult = files.Image("bg.png", 4, 4, SKColors.Red);

        r.Bg.PickImageCommand.Execute(null);

        Assert.Equal(1.0, r.Session.Document.BgOpacity);
        Assert.NotNull(r.Session.Document.BgImage);
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Equal((0.0, null), (r.Session.Document.BgOpacity, r.Session.Document.BgImage));
    }

    [Fact] // a swatch after an image drops it (bg-color-pick-clears-image), and the pick can be undone back to the image
    public void A_swatch_after_an_image_drops_it_and_undo_restores_it()
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Image("bg.png", 4, 4, SKColors.Red);
        r.Bg.PickImageCommand.Execute(null);
        var image = r.Session.Document.BgImage;

        r.Bg.SetSwatchCommand.Execute("#050c18");

        Assert.Null(r.Session.Document.BgImage);
        Assert.True(r.Session.Undo());
        Assert.Same(image, r.Session.Document.BgImage);
    }

    [Fact] // the 100x100 image at 40% against the frozen render golden bg-image-opacity: already proven by the golden suite, cited
    public void The_stretch_and_opacity_render_is_held_by_the_frozen_golden()
    {
        // GoldenRenderTests.Engine_export_matches_the_frozen_4_1_golden("bg-image-opacity") holds the 40% render of drip_red_wide_v1.png.
        // Here: the controls feed that same renderer, so a background image at 40% reaches the export at 40%.
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Image("red.png", 100, 100, SKColors.Red);
        r.Bg.PickImageCommand.Execute(null);
        r.Bg.OpacityPercent = 40;

        using var export = Renderer.Export(r.Session.Document);
        var px = PixelOf(export, 295, 138);   // torso_front, inside the mask: red at 40% alpha (SKBitmap.GetPixel un-premultiplies)
        Assert.InRange(px.Alpha, 101, 103);
        Assert.Equal((255, 0, 0), (px.Red, px.Green, px.Blue));
    }

    // ---- the view

    static (BackgroundGroup Swatches, BgSettingsGroup Settings) Load(BackgroundViewModel bg, Func<bool>? leftDown = null)
    {
        var swatches = new BackgroundGroup { DataContext = bg };
        var settings = new BgSettingsGroup(leftDown ?? (() => false)) { DataContext = bg };
        StaThread.Pump();
        Layout(swatches);
        Layout(settings);
        return (swatches, settings);
    }

    [Fact] // editor.html:127: twelve 24x24 buttons on one row, 3 apart, tooltip = name, focusable, with automation names
    public void The_swatches_are_twelve_focusable_buttons_on_one_row() => StaThread.Run(() =>
    {
        var r = Make(Opaque());
        var (view, _) = Load(r.Bg);
        var buttons = Descendants<Button>(view).ToList();

        Assert.Equal(12, buttons.Count);
        Assert.Equal(r.Bg.Swatches.Select(s => s.Name), buttons.Select(b => (string)b.ToolTip));
        Assert.Equal(r.Bg.Swatches.Select(s => "Background " + s.Name), buttons.Select(b => AutomationProperties.GetName(b)));
        Assert.All(buttons, b =>
        {
            Assert.True(b.Focusable);
            Assert.Equal((24.0, 24.0), (b.ActualWidth, b.ActualHeight));
        });
        Assert.Single(buttons.Select(b => Math.Round(b.TranslatePoint(new Point(0, 0), view).Y)).Distinct());   // one row
        Assert.Equal(27.0, buttons[1].TranslatePoint(new Point(0, 0), view).X - buttons[0].TranslatePoint(new Point(0, 0), view).X);   // 24 + gap 3
        Assert.Equal(r.Bg.Swatches.Select(s => (Color)ColorConverter.ConvertFromString(s.Hex)), buttons.Select(b => ((SolidColorBrush)b.Background).Color));
    });

    [Fact] // the click a user makes: a swatch through the button is one step and, on a new document, shows the color
    public void Clicking_a_swatch_button_sets_the_background() => StaThread.Run(() =>
    {
        var r = Make();
        var (view, _) = Load(r.Bg);
        Invoke(Descendants<Button>(view).First(b => AutomationProperties.GetName(b) == "Background Cyan"));

        Assert.Equal(("#17d4fa", 1.0, 1), (r.Session.Document.BgColor, r.Session.Document.BgOpacity, r.Session.UndoCount));
    });

    [Fact] // editor.html:131-144: the color well (30x24), the Background button, the Opacity label and an 80 px slider
    public void BG_Settings_has_the_well_the_image_button_and_the_slider() => StaThread.Run(() =>
    {
        var r = Make(Opaque());
        var (_, view) = Load(r.Bg);
        var well = (Button)view.FindName("BgColor");
        var image = (Button)view.FindName("BgImage");
        var slider = (Slider)view.FindName("OpacitySlider");

        Assert.Equal((30.0, 24.0), (well.ActualWidth, well.ActualHeight));
        Assert.Equal("Background", image.Content);
        Assert.Equal((0.0, 100.0, 80.0), (slider.Minimum, slider.Maximum, slider.ActualWidth));
        Assert.Equal(100.0, slider.Value);
        Assert.Equal("Background color", AutomationProperties.GetName(well));
        Assert.Equal("#192e44", AutomationProperties.GetHelpText(well));
        Assert.Equal("Background opacity", AutomationProperties.GetName(slider));
        Assert.Equal(((SolidColorBrush)well.Background).Color, (Color)ColorConverter.ConvertFromString("#192e44"));
        Assert.Contains(Descendants<TextBlock>(view), t => t.Text == "Opacity");
        Assert.Contains(Descendants<TextBlock>(view), t => t.Text == "BG SETTINGS");
    });

    [Fact] // the well and the slider follow the document: undo re-syncs them
    public void The_controls_follow_the_document() => StaThread.Run(() =>
    {
        var r = Make(Opaque());
        var (_, view) = Load(r.Bg);
        var well = (Button)view.FindName("BgColor");
        var slider = (Slider)view.FindName("OpacitySlider");

        r.Bg.SetSwatchCommand.Execute("#f22f89");
        r.Bg.OpacityPercent = 30;
        StaThread.Pump();
        Assert.Equal((Color)ColorConverter.ConvertFromString("#f22f89"), ((SolidColorBrush)well.Background).Color);
        Assert.Equal(30.0, slider.Value);

        r.Session.Undo();
        r.Session.Undo();
        StaThread.Pump();
        Assert.Equal((Color)ColorConverter.ConvertFromString("#192e44"), ((SolidColorBrush)well.Background).Color);
        Assert.Equal(100.0, slider.Value);
    });

    [Fact] // the Background button runs the image pick through the dialog
    public void The_Background_button_asks_for_one_image() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var r = Make(Opaque());
        r.Dialogs.OpenImageResult = files.Image("bg.png", 5, 5, SKColors.Blue);
        var (_, view) = Load(r.Bg);

        Invoke((Button)view.FindName("BgImage"));

        Assert.Equal((1, 1), (r.Dialogs.OpenImageCalls, r.Session.UndoCount));
        Assert.NotNull(r.Session.Document.BgImage);
    });

    [Fact] // the well opens the picker under itself
    public void The_color_well_opens_the_picker_under_itself() => StaThread.Run(() =>
    {
        var r = Make(Opaque());
        r.Picker.Answer("#f22f89", "#ff0000");
        var (_, view) = Load(r.Bg);
        var well = (Button)view.FindName("BgColor");

        Invoke(well);

        Assert.Same(well, Assert.Single(r.Picker.Anchors));
        Assert.Equal("#f22f89", r.Session.Document.BgColor);
        Assert.Equal(1, r.Session.UndoCount);
    });

    [Fact] // a slider press is one undo gesture: 30 ticks through the real slider and the real release event, one step
    public void A_slider_press_and_release_around_thirty_ticks_is_one_step() => StaThread.Run(() =>
    {
        var r = Make(Opaque());
        bool down = false;
        var (_, view) = Load(r.Bg, () => down);
        var slider = (Slider)view.FindName("OpacitySlider");

        down = true;
        view.Begin(freshPress: true);
        for (int i = 1; i <= 30; i++) slider.Value = 100 - i;
        Assert.Equal(0, r.Session.UndoCount);   // still open
        down = false;
        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });

        Assert.Equal(70.0, r.Bg.OpacityPercent);
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Equal(100.0, r.Bg.OpacityPercent);
    });

    [Fact] // the keyboard arrow on the slider records a step (4.1 recorded none)
    public void The_keyboard_arrow_on_the_slider_records_a_step() => StaThread.Run(() =>
    {
        var r = Make(Opaque());
        var (_, view) = Load(r.Bg);
        var slider = (Slider)view.FindName("OpacitySlider");

        Slider.DecreaseSmall.Execute(null, slider);   // what the Left arrow runs
        StaThread.Pump();
        Assert.Equal(99.0, r.Bg.OpacityPercent);
        Assert.Equal(1, r.Session.UndoCount);
        Slider.DecreaseSmall.Execute(null, slider);
        Assert.Equal(2, r.Session.UndoCount);
    });

    // ---- the single-select picker

    [Fact] // the bridge answers OpenImage from its queue and logs it; an empty queue cancels
    public void The_bridge_dialogs_answer_the_single_image_picker()
    {
        var log = new StringWriter();
        var dialogs = new Testing.BridgeFileDialogs(log);
        dialogs.Enqueue(@"C:rtg.png");
        Assert.Equal(@"C:rtg.png", dialogs.OpenImage());
        Assert.Null(dialogs.OpenImage());
        Assert.Equal(new[]
        {
            @"RBX15-BRIDGE-DIALOG OpenImage  -> C:rtg.png",
            @"RBX15-BRIDGE-DIALOG OpenImage  -> cancel (queue empty)",
        }, log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    sealed class MultiOnly(params string[] picks) : IFileDialogs
    {
        public IReadOnlyList<string> OpenImages() => picks;
        public string? SavePng(string suggestedFileName) => null;
        public string? OpenProject(string? initialDirectory) => null;
        public string? SaveProject(string suggestedFileName, string? initialDirectory) => null;
        public string? PickFolder(string? initialDirectory) => null;
    }

    [Fact] // a dialog service without its own single picker falls back to the first multi-select pick
    public void The_default_single_picker_takes_the_first_pick()
    {
        Assert.Equal("a.png", ((IFileDialogs)new MultiOnly("a.png", "b.png")).OpenImage());
        Assert.Null(((IFileDialogs)new MultiOnly()).OpenImage());
    }

    // ---- the window

    [Fact] // always visible: the Layout pane has the Background groups with nothing selected, while Presets stays collapsed
    public void The_Layout_pane_shows_the_Background_groups_with_nothing_selected() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        main.ActiveTab = RibbonTab.Layout;
        StaThread.Pump();
        var pane = (LayoutRibbon)w.FindName("LayoutPane");
        Layout(pane);

        var groups = (StackPanel)pane.FindName("Groups");
        Assert.Equal(new[] { typeof(PresetsGroup), typeof(BackgroundGroup), typeof(BgSettingsGroup) }, groups.Children.Cast<object>().Select(c => c.GetType()));
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)pane.FindName("Presets")).Visibility);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)pane.FindName("BackgroundSwatches")).Visibility);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)pane.FindName("BgSettings")).Visibility);
        Assert.True(((FrameworkElement)pane.FindName("BackgroundSwatches")).ActualWidth > 0);
    });

    [Fact] // the shell end to end: click a swatch in the composed Layout pane on a new document and the export shows it
    public void The_composed_shell_paints_the_background_from_the_Layout_pane() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var main = TestShell.Quiet(TestShell.Main(session));
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        main.ActiveTab = RibbonTab.Layout;
        StaThread.Pump();
        var pane = (LayoutRibbon)w.FindName("LayoutPane");
        Layout(pane);
        var swatches = (BackgroundGroup)pane.FindName("BackgroundSwatches");

        Invoke(Descendants<Button>(swatches).First(b => AutomationProperties.GetName(b) == "Background Magenta"));

        using var export = Renderer.Export(session.Document);
        Assert.Equal(new SKColor(0xf2, 0x2f, 0x89, 0xff), PixelOf(export, 295, 138));
        Assert.Equal(1, session.UndoCount);
        Assert.Equal("#f22f89", main.Layout.Background.Color);
    });

    [Fact] // the Pants mode keeps the groups and the rule: the background is mode-independent
    public void The_background_works_in_Pants() => StaThread.Run(() =>
    {
        var r = Make(new Document { Mode = TemplateMode.Pants, BgOpacity = 0 });
        r.Bg.SetSwatchCommand.Execute("#2c3e50");
        Assert.Equal((TemplateMode.Pants, "#2c3e50", 1.0), (r.Session.Document.Mode, r.Session.Document.BgColor, r.Session.Document.BgOpacity));
    });

    static SKImage Px(int w, int h, SKColor color)
    {
        using var bitmap = new SKBitmap(w, h);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }
}
