using System.Windows.Input;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Paste as layer (M5 item 6; decisions P1, P2; ledger upgrade-paste-as-layer): Ctrl+V adds new layers on top through the import caps,
/// behind <see cref="IClipboardSource"/>. No test reads the real clipboard, and none waits: the retry delay is a no-op.
/// </summary>
public class PasteServiceTests
{
    static byte[] Png(int w, int h, SKColor color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static (PasteService Paste, EditorSession Session, FakeStatus Status, FakeClipboardSource Clipboard) Rig(EditorSession? session = null)
    {
        session ??= new EditorSession();
        var status = new FakeStatus();
        var clipboard = new FakeClipboardSource();
        return (new PasteService(session, new ImportService(session, status), clipboard, status, _ => { }), session, status, clipboard);
    }

    [Fact] // upgrade-paste-as-layer: a 50% alpha pixel survives, the layer is fit like Add image, selected, Select tool on, one undo step
    public void A_png_with_alpha_pastes_as_one_layer_placed_like_add_image()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var clipboard = new FakeClipboardSource { Content = new ClipboardContent.Png(Png(40, 20, new SKColor(255, 0, 0, 128))) };
        var main = TestShell.Main(session, clipboard: clipboard);
        main.Home.Tools.Set(Tool.Pen);

        Assert.Equal(1, main.Paste.Paste());

        var layer = Assert.Single(session.Document.Layers);
        using (var bitmap = SKBitmap.FromImage(layer.Image))
            Assert.Equal(128, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(layer.Id, session.SelectedLayerId);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        Assert.Equal(1, session.UndoCount);

        var reference = new EditorSession();
        new ImportService(reference, new FakeStatus()).Import([files.Image("same.png", 40, 20, SKColors.Red)]);
        var expected = reference.Document.Layers[0];
        Assert.Equal((expected.X, expected.Y, expected.Scale), (layer.X, layer.Y, layer.Scale));
        Assert.Equal(LayerOps.DefaultName(layer.Id), layer.Name);

        Assert.True(session.Undo());
        Assert.Empty(session.Document.Layers);
        Assert.False(session.Undo());
    }

    [Fact] // P1: paste never replaces the selected layer; it adds on top and selects the new one
    public void A_paste_with_a_layer_selected_adds_a_new_layer_on_top()
    {
        var (paste, session, _, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Png(Png(8, 8, SKColors.Red));
        paste.Paste();
        var first = session.Document.Layers[0];
        clipboard.Content = new ClipboardContent.Png(Png(8, 8, SKColors.Blue));
        paste.Paste();
        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Same(first.Image, session.Document.Layers[0].Image);
        Assert.Equal(session.Document.Layers[1].Id, session.SelectedLayerId);
        Assert.Equal(2, session.UndoCount);
    }

    [Fact] // P2: the PNG format wins over a bitmap on the same data object (the source hands over the PNG first), and a bitmap alone pastes
    public void A_bitmap_pastes_when_the_clipboard_has_no_png()
    {
        var (paste, session, _, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Bitmap(Png(16, 16, SKColors.Green));
        Assert.Equal(1, paste.Paste());
        using var bitmap = SKBitmap.FromImage(session.Document.Layers[0].Image);
        Assert.Equal(SKColors.Green, bitmap.GetPixel(0, 0));
    }

    [Fact] // P2: PNG wins over a bitmap: the source's content is the PNG, and that is what lands
    public void The_png_wins_over_a_bitmap_on_the_same_data_object()
    {
        // The order lives in WpfClipboardSource.Read (PNG, file list, bitmap); the service pastes whichever content it is handed, once.
        var (paste, session, _, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Png(Png(16, 16, SKColors.Red));
        Assert.Equal(1, paste.Paste());
        Assert.Equal(1, clipboard.Reads);
        using var bitmap = SKBitmap.FromImage(session.Document.Layers[0].Image);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0));
    }

    [Fact] // P2: two PNGs and a .txt add two layers in order in one step, and the skipped file is named
    public void A_file_list_adds_its_images_in_order_as_one_step_and_names_the_skipped_file()
    {
        using var files = new TestFiles();
        var (paste, session, status, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Files([
            files.Image("one.png", 8, 8, SKColors.Red),
            files.Write("notes.txt", [0x68, 0x69]),
            files.Image("two.png", 8, 8, SKColors.Blue),
        ]);
        Assert.Equal(2, paste.Paste());
        Assert.Equal(new[] { "one", "two" }, session.Document.Layers.Select(l => l.Name));
        Assert.Equal(1, session.UndoCount);
        Assert.Contains("notes.txt", status.Current);
    }

    [Fact] // an empty clipboard and a text-only clipboard (both read as None) give the status line and no step
    public void Nothing_usable_gives_the_status_line_and_no_step()
    {
        var (paste, session, status, _) = Rig();
        Assert.Equal(0, paste.Paste());
        Assert.Equal(0, paste.Paste());
        Assert.Equal(["Nothing to paste. Copy an image first.", "Nothing to paste. Copy an image first."], status.Shown);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact] // a clipboard held by another app: three reads, then the status line and no step
    public void A_clipboard_held_for_every_attempt_is_named_on_the_status_strip()
    {
        var (paste, session, status, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Png(Png(8, 8, SKColors.Red));
        clipboard.BusyReads = 3;
        Assert.Equal(0, paste.Paste());
        Assert.Equal(PasteService.Attempts, clipboard.Reads);
        Assert.Equal(DialogText.ClipboardBusy, status.Current);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact] // held once, then free: the retry pastes
    public void A_clipboard_busy_once_pastes_on_the_retry()
    {
        var (paste, session, _, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Png(Png(8, 8, SKColors.Red));
        clipboard.BusyReads = 1;
        Assert.Equal(1, paste.Paste());
        Assert.Equal(2, clipboard.Reads);
        Assert.Single(session.Document.Layers);
    }

    [Fact] // image-decode-caps: over 16384 pixels on a side is refused with a status line, through the same caps as an import
    public void An_oversized_image_is_refused_through_the_caps()
    {
        var (paste, session, status, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Png(TestFiles.PngHeader(16385, 10));
        Assert.Equal(0, paste.Paste());
        Assert.Contains("16384", status.Current);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact] // bytes that are not an image are named, not thrown
    public void Bytes_that_are_not_an_image_are_named_on_the_status_strip()
    {
        var (paste, session, status, clipboard) = Rig();
        clipboard.Content = new ClipboardContent.Bitmap([1, 2, 3]);
        Assert.Equal(0, paste.Paste());
        Assert.Equal(DialogText.PasteFailed(ImageImporter.UndecodableReason), status.Current);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact] // the front slot comes from Template.Slot, so Pants mode places a paste like an import in Pants mode
    public void Pants_mode_pastes_onto_the_pants_front_slot()
    {
        using var files = new TestFiles();
        var session = new EditorSession(new Document { Mode = TemplateMode.Pants });
        var (paste, _, _, clipboard) = Rig(session);
        clipboard.Content = new ClipboardContent.Png(Png(30, 60, SKColors.Red));
        paste.Paste();

        var reference = new EditorSession(new Document { Mode = TemplateMode.Pants });
        new ImportService(reference, new FakeStatus()).Import([files.Image("p.png", 30, 60, SKColors.Red)]);
        var layer = session.Document.Layers[0];
        var expected = reference.Document.Layers[0];
        Assert.Equal((expected.X, expected.Y, expected.Scale), (layer.X, layer.Y, layer.Scale));
    }

    [Fact] // a focused text field keeps Ctrl+V; Keys OFF does not block it (a KeyBinding is not routed)
    public void Ctrl_v_in_a_text_field_does_not_paste_and_keys_off_does_not_block_it() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var clipboard = new FakeClipboardSource { Content = new ClipboardContent.Png(Png(8, 8, SKColors.Red)) };
        var main = TestShell.Main(session, clipboard: clipboard);
        var window = new MainWindow(main, new FakeSettingsStore(), [new System.Windows.Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        var binding = Assert.Single(window.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.V && b.Modifiers == ModifierKeys.Control);
        var guard = Assert.IsType<TextFieldShortcutCommand>(binding.Command);
        Assert.Same(main.Paste.PasteCommand, guard.Inner);

        window.FocusedElement = () => new System.Windows.Controls.TextBox();   // the Word Art text box is a TextBox
        Assert.False(guard.CanExecute(null));
        window.FocusedElement = () => null;
        main.Home.Keys.Enabled = false;
        Assert.True(guard.CanExecute(null));
        guard.Execute(null);
        Assert.Single(session.Document.Layers);
        window.Close();
    });
}
