using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Text tab (editor.html:226-283, editor.js:2108-2300; ledger wordart-state-defaults, text-inputs, text-preview,
/// wordart-preview-live, wordart-font-loading-race, text-add-to-canvas, wordart-add-to-canvas). The engine side (the render,
/// the 15 fonts, the layer, the caps) is held by the Word Art goldens and <c>WordArtHardeningTests</c>; these tests hold the tab:
/// defaults, clamps, the coalesced live preview, Add, the keyboard guard and the view's pixel values.
/// </summary>
[Collection("MainWindow")] // the window facts build a MainWindow, as ShellLookTests does
public class TextTabTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    static TextViewModel Make(EditorSession session, FakeColorPicker? picker = null, IStatus? status = null) =>
        new(session, picker ?? new FakeColorPicker(), status ?? new FakeStatus());

    static EditorSession Pants() => new(new Document { Mode = TemplateMode.Pants });

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>A click as UI Automation sends it; the peer queues the click on the dispatcher, so the queue runs before this returns.</summary>
    static void Invoke(Button b)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
    }

    static byte[] PixelsOf(ImageSource source)
    {
        var bmp = (BitmapSource)source;
        var bytes = new byte[bmp.PixelHeight * bmp.PixelWidth * 4];
        bmp.CopyPixels(bytes, bmp.PixelWidth * 4, 0);
        return bytes;
    }

    static string LongText(int characters) => string.Concat(Enumerable.Repeat("RIZZ ", characters / 5));

    static TextRibbon Load(TextViewModel vm)
    {
        var view = new TextRibbon { DataContext = vm };
        StaThread.Pump();
        view.Measure(new Size(1200, 200));
        view.Arrange(new Rect(0, 0, 1200, 200));
        view.UpdateLayout();
        return view;
    }

    static T Part<T>(FrameworkElement view, string name) where T : class => (T)view.FindName(name);

    // ---- wordart-state-defaults, text-inputs: the defaults

    [Fact] // parityCheck: assert every control's initial value
    public void Every_control_starts_at_its_4_1_default() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        Assert.Equal(("RIZZ", "Anton", WordArtStyle.Solid, 64.0), (t.Text, t.Font, t.Style, t.Size));
        Assert.Equal("64", t.SizeText);
        Assert.Equal(("#17d4fa", "#000000", "#f22f89"), (t.FillColor, t.StrokeColor, t.GradColor));
        Assert.Equal((3, 0), (t.StrokeWidth, t.Angle));
        Assert.Equal("Type text...", TextViewModel.Placeholder);
        Assert.Equal(1.0, t.DeviceScale);
    });

    [Fact] // the settings the controls hold are the engine's WordArtSettings, defaults included
    public void The_defaults_are_the_settings_the_engine_renders()
    {
        StaThread.Run(() =>
        {
            var s = Make(new EditorSession()).Settings;
            Assert.Equal(new WordArtSettings("RIZZ", "Anton", WordArtStyle.Solid, 64, CssColor.ToSk("#17d4fa"), CssColor.ToSk("#000000"),
                                             CssColor.ToSk("#f22f89"), 3, 0), s);
        });
    }

    [Fact] // text-inputs: 15 options, labels apart from values, the waFont order; the default is Anton
    public void The_font_select_has_the_15_options_in_4_1s_order() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        Assert.Equal(15, t.Fonts.Count);
        Assert.Equal(WordArt.FontOptions.Select(o => (o.Family, o.Label)), t.Fonts.Select(f => (f.Family, f.Label)));
        Assert.Equal(["Anton (Block)", "Bebas Neue (Tall)", "Dancing Script", "Great Vibes", "Pacifico (Retro)", "Creepster", "Poppins (Bold)",
                      "Lato (Modern)", "Bangers (Comic)", "Permanent Marker", "Bungee Shade (3D)", "Rubik Mono", "Press Start 2P", "Righteous",
                      "Orbitron (Cyber)"], t.Fonts.Select(f => f.Label));
        Assert.Equal("Poppins", t.Fonts.Single(f => f.Label == "Poppins (Bold)").Family);   // the value differs from the label
        Assert.Equal("Rubik Mono One", t.Fonts.Single(f => f.Label == "Rubik Mono").Family);
    });

    [Fact] // P-1 (smoke): the closed box and the open list draw each font's label, not the record's ToString
    public void The_font_select_draws_labels_in_the_closed_box_and_the_open_list() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var view = Load(t);
        var box = Part<ComboBox>(view, "FontBox");

        string ClosedText() => string.Concat(Descendants<TextBlock>(box).Where(b => b.Name != "PART_Popup").Select(b => b.Text));
        Assert.Equal("Anton (Block)", ClosedText());

        t.Font = "Poppins";
        StaThread.Pump();
        view.UpdateLayout();
        Assert.Equal("Poppins (Bold)", ClosedText());

        // The list lives in the popup, which has no window here: lay its child out by hand so the item containers realize.
        var popup = (System.Windows.Controls.Primitives.Popup)box.Template.FindName("PART_Popup", box);
        var list = (FrameworkElement)popup.Child;
        list.Measure(new Size(400, 1000));
        list.Arrange(new Rect(0, 0, 400, 1000));
        list.UpdateLayout();
        var shown = Enumerable.Range(0, box.Items.Count)
            .Select(i => (ComboBoxItem)box.ItemContainerGenerator.ContainerFromIndex(i))
            .Select(c => { c.ApplyTemplate(); c.UpdateLayout(); return string.Concat(Descendants<TextBlock>(c).Select(b => b.Text)); })
            .ToList();
        Assert.Equal(t.Fonts.Select(f => f.Label), shown);
    });

    [Fact]
    public void A_font_that_is_not_one_of_the_15_is_refused() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        Assert.Throws<ArgumentException>(() => t.Font = "Comic Sans MS");
        Assert.Equal("Anton", t.Font);
        t.Font = "Orbitron";
        Assert.Equal("Orbitron", t.Font);
    });

    [Fact] // wordart-state-defaults: Solid is the only active style, and the six are in 4.1's order
    public void Solid_is_the_only_active_style() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        Assert.Equal(["Solid", "Gradient", "Neon", "Shadow", "Outline", "Retro"], t.Styles.Select(s => s.Label));
        Assert.Equal([WordArtStyle.Solid, WordArtStyle.Gradient, WordArtStyle.Neon, WordArtStyle.Shadow, WordArtStyle.Outline, WordArtStyle.Retro],
                     t.Styles.Select(s => s.Style));
        Assert.Equal(WordArtStyle.Solid, t.Style);
        Assert.Equal(1, t.Styles.Count(s => s.Style == t.Style));
        t.Style = WordArtStyle.Neon;
        Assert.Equal(WordArtStyle.Neon, t.Style);   // one active at a time: Style holds one value
    });

    // ---- the clamps (wordart-state-defaults, approved divergence)

    [Theory] // size is +v || 64, then 8..300
    [InlineData(5, 8)]
    [InlineData(8, 8)]
    [InlineData(300, 300)]
    [InlineData(5000, 300)]
    [InlineData(0, 64)]
    [InlineData(double.NaN, 64)]
    [InlineData(-20, 8)]
    [InlineData(96.5, 96.5)]
    public void The_size_is_clamped_to_8_through_300(double typed, double expected) => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        t.Size = typed;
        Assert.Equal(expected, t.Size);
        Assert.Equal(expected.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), t.SizeText);
    });

    [Theory] // typed text is read live and settles on commit
    [InlineData("5", 8, "8")]
    [InlineData("5000", 300, "300")]
    [InlineData("", 64, "64")]
    [InlineData("abc", 64, "64")]
    [InlineData("0", 64, "64")]
    [InlineData(" 120 ", 120, "120")]
    [InlineData("12.5", 12.5, "12.5")]
    public void Typed_size_text_is_read_live_and_settles_on_commit(string typed, double size, string settled) => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        t.SizeText = typed;
        Assert.Equal(size, t.Size);
        Assert.Equal(typed, t.SizeText);   // not rewritten under the caret
        t.CommitSize();
        Assert.Equal(settled, t.SizeText);
        Assert.Equal(size, t.Size);
    });

    [Fact]
    public void The_stroke_width_and_angle_are_whole_numbers_in_range() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        t.StrokeWidth = -3;
        Assert.Equal(0, t.StrokeWidth);
        t.StrokeWidth = 99;
        Assert.Equal(20, t.StrokeWidth);
        t.Angle = -1;
        Assert.Equal(0, t.Angle);
        t.Angle = 720;
        Assert.Equal(360, t.Angle);
        t.Angle = 45;
        Assert.Equal(45, t.Angle);
    });

    [Fact]
    public void A_color_is_a_plain_hex_and_comes_back_lowercase() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        t.FillColor = "#ABCDEF";
        Assert.Equal("#abcdef", t.FillColor);
        foreach (var bad in new[] { "red", "#abc", "#12345g", "17d4fa", "" })
            Assert.Throws<ArgumentException>(() => t.StrokeColor = bad);
        Assert.Equal("#000000", t.StrokeColor);
    });

    [Fact] // the pick previews live, a cancel restores, and none of it touches the document
    public void A_color_pick_previews_commits_and_cancels_without_touching_the_document() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var picker = new FakeColorPicker().Answer("#00ff00", "#112233").Answer(null, "#445566").Answer("#aabbcc");
        var t = Make(session, picker);
        var anchor = new object();
        var seen = new List<string>();
        t.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TextViewModel.FillColor)) seen.Add(t.FillColor); };

        t.PickFillCommand.Execute(anchor);
        Assert.Equal("#00ff00", t.FillColor);
        Assert.Equal(["#112233", "#00ff00"], seen);
        t.PickFillCommand.Execute(anchor);
        Assert.Equal("#00ff00", t.FillColor);   // cancelled: back to the color before the pick
        t.PickStrokeCommand.Execute(anchor);
        Assert.Equal("#aabbcc", t.StrokeColor);
        Assert.Equal("#f22f89", t.GradColor);
        Assert.Equal(["#17d4fa", "#00ff00", "#000000"], picker.Seeds);
        Assert.All(picker.Anchors, a => Assert.Same(anchor, a));
        Assert.Equal(0, session.UndoCount);
    });

    // ---- wordart-font-loading-race: the preview exists with no font gate

    [Fact] // parityCheck: RIZZ preview on first frame, no blank state
    public void The_preview_is_there_right_after_construction() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var preview = Assert.IsAssignableFrom<BitmapSource>(t.Preview);
        Assert.True(preview.IsFrozen);
        Assert.InRange(preview.Width, 1, TextViewModel.PreviewMaxWidth);
        Assert.InRange(preview.Height, 1, TextViewModel.PreviewMaxHeight);
        Assert.Same(preview, t.Preview);   // read twice, drawn once
        Assert.Equal(1, t.PreviewRenders);
        Assert.Contains(PixelsOf(preview), b => b != 0);   // not blank
    });

    [Fact] // the preview is the art of the default settings in its vendored face, not a fallback
    public void The_default_preview_is_the_engine_render_of_RIZZ_in_Anton_cropped_to_its_content() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var (image, cw, ch, sc) = WordArt.Render(t.Settings, TextViewModel.PreviewMaxWidth, TextViewModel.PreviewMaxHeight);
        using (image)
        {
            var preview = (BitmapSource)t.Preview;
            Assert.Equal((int)Math.Ceiling(cw * sc), preview.PixelWidth);
            Assert.Equal((int)Math.Ceiling(ch * sc), preview.PixelHeight);
            Assert.Equal(80, preview.PixelHeight);   // RIZZ at 64 is taller than the 80 DIP box, so the height fits
        }
    });

    // ---- wordart-preview-live: every change re-renders, coalesced at Background priority

    public static TheoryData<string> Controls() => new()
    {
        { "Text" }, { "Font" }, { "Style" }, { "Size" }, { "SizeText" }, { "Fill" }, { "Stroke" }, { "Grad" }, { "StrokeWidth" }, { "Angle" },
    };

    static void Change(TextViewModel t, string control)
    {
        switch (control)
        {
            case "Text": t.Text = "RIZZ UP"; break;
            case "Font": t.Font = "Bangers"; break;
            case "Style": t.Style = WordArtStyle.Retro; break;
            case "Size": t.Size = 40; break;
            case "SizeText": t.SizeText = "48"; break;
            case "Fill": t.FillColor = "#ff0000"; break;
            case "Stroke": t.StrokeColor = "#00ff00"; break;
            case "Grad": t.GradColor = "#0000ff"; break;
            case "StrokeWidth": t.StrokeWidth = 9; break;
            case "Angle": t.Angle = 90; break;
            default: throw new ArgumentOutOfRangeException(nameof(control));
        }
    }

    [Theory] // parityCheck: change each control; the preview updates within one dispatcher pass
    [MemberData(nameof(Controls))]
    public void Each_control_change_updates_the_preview_within_one_dispatcher_pass(string control) => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var before = t.Preview;
        int renders = t.PreviewRenders;
        var changed = new List<string?>();
        t.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TextViewModel.Preview)) changed.Add(e.PropertyName); };

        Change(t, control);
        Assert.Same(before, t.Preview);   // not on the keystroke: the pass redraws it
        Assert.Equal(renders, t.PreviewRenders);

        StaThread.Pump();
        Assert.NotSame(before, t.Preview);
        Assert.Equal(renders + 1, t.PreviewRenders);
        Assert.Single(changed);
    });

    [Fact] // 40 keystrokes and a slider drag between two passes cost one render
    public void A_burst_of_changes_costs_one_render() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        _ = t.Preview;
        int renders = t.PreviewRenders;
        for (int i = 1; i <= 40; i++) t.Text = new string('A', i);
        for (int a = 0; a <= 360; a += 10) t.Angle = a;
        t.FillColor = "#ff00ff";
        Assert.Equal(renders, t.PreviewRenders);
        StaThread.Pump();
        Assert.Equal(renders + 1, t.PreviewRenders);
        StaThread.Pump();
        Assert.Equal(renders + 1, t.PreviewRenders);   // nothing left to draw
    });

    [Fact] // the redraw is the new settings, not a repeat of the old picture
    public void The_redrawn_preview_follows_the_settings() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var cyan = PixelsOf(t.Preview);
        t.FillColor = "#ff0000";
        StaThread.Pump();
        var red = PixelsOf(t.Preview);
        Assert.NotEqual(cyan, red);
        t.Style = WordArtStyle.Neon;
        StaThread.Pump();
        Assert.NotEqual(red, PixelsOf(t.Preview));
    });

    [Fact] // a preview nobody has looked at is not drawn on every keystroke
    public void An_unread_preview_is_drawn_when_it_is_first_read() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        t.Text = "HELLO";
        StaThread.Pump();
        Assert.Equal(0, t.PreviewRenders);
        _ = t.Preview;
        Assert.Equal(1, t.PreviewRenders);
    });

    // ---- T2: device pixels

    [Theory]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void The_preview_renders_at_device_pixels_and_shows_at_the_same_size_in_DIPs(double scale) => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var one = (BitmapSource)t.Preview;
        t.DeviceScale = scale;
        var device = (BitmapSource)t.Preview;   // a DPI change redraws at once, not on a later pass
        Assert.NotSame(one, device);
        Assert.Equal(96 * scale, device.DpiX, 6);
        Assert.Equal(Math.Round(80 * scale), device.PixelHeight);
        Assert.InRange(device.PixelWidth, one.PixelWidth * scale - 2, one.PixelWidth * scale + 2);
        Assert.InRange(device.Width, one.Width - 1.5, one.Width + 1.5);     // the same size on screen
        Assert.InRange(device.Height, one.Height - 1.5, one.Height + 1.5);
        Assert.True(device.IsFrozen);
    });

    [Fact]
    public void The_device_scale_is_a_positive_number() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        Assert.Throws<ArgumentOutOfRangeException>(() => t.DeviceScale = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => t.DeviceScale = double.NaN);
    });

    // ---- wordart-add-to-canvas, text-add-to-canvas

    [Fact] // parityCheck: layer +1, name, scale/x/y equal to the engine's within 1e-6, selected, one undo step
    public void Add_puts_one_selected_layer_on_top_in_one_undo_step() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1), LayerOf(2));
        var t = Make(session);
        int nextId = session.NextId;
        var expected = WordArt.CreateLayer(t.Settings, TemplateMode.Shirt, nextId);
        int raised = 0;
        t.LayersAdded += (_, _) => raised++;

        t.AddCommand.Execute(null);

        Assert.Equal(1, session.UndoCount);
        Assert.Equal(3, session.Document.Layers.Count);
        var layer = session.Document.Layers[^1];   // pushed on top
        Assert.Equal("✨ RIZZ [Anton]", layer.Name);
        Assert.Equal(nextId, layer.Id);
        Assert.True(Math.Abs(expected.Scale - layer.Scale) < 1e-6, $"scale {layer.Scale} vs {expected.Scale}");
        Assert.True(Math.Abs(expected.X - layer.X) < 1e-6, $"x {layer.X} vs {expected.X}");
        Assert.True(Math.Abs(expected.Y - layer.Y) < 1e-6, $"y {layer.Y} vs {expected.Y}");
        Assert.Equal((expected.Image.Width, expected.Image.Height), (layer.Image.Width, layer.Image.Height));
        Assert.Equal(layer.Id, session.SelectedLayerId);
        Assert.Equal(nextId + 1, session.NextId);
        Assert.Equal(1, raised);

        session.Undo();   // one step takes it away
        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Equal(0, session.UndoCount);
        session.Redo();
        Assert.Equal("✨ RIZZ [Anton]", session.Document.Layers[^1].Name);
    });

    [Fact] // the settings at click time are the layer: text, font and style travel into the name and the pixels
    public void Add_uses_the_controls_as_they_are_when_clicked() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var t = Make(session);
        t.Text = "HELLO WORLD";
        t.Font = "Bebas Neue";
        t.Style = WordArtStyle.Shadow;
        t.Size = 120;
        t.FillColor = "#ff8800";
        t.StrokeWidth = 6;
        var expected = WordArt.CreateLayer(t.Settings, TemplateMode.Shirt, session.NextId);

        t.AddCommand.Execute(null);

        var layer = session.Document.Layers[0];
        Assert.Equal("✨ HELLO WORLD [Bebas Neue]", layer.Name);
        Assert.Equal((expected.Image.Width, expected.Image.Height), (layer.Image.Width, layer.Image.Height));
        Assert.True(Math.Abs(expected.Scale - layer.Scale) < 1e-6);
    });

    [Fact] // empty text is "TEXT" (editor.js:2270); a second Add is a second layer and a second step
    public void Empty_text_adds_a_TEXT_layer_and_every_click_is_its_own_step() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var t = Make(session);
        t.Text = "";
        t.AddCommand.Execute(null);
        t.AddCommand.Execute(null);
        Assert.Equal(2, session.UndoCount);
        Assert.Equal(["✨ TEXT [Anton]", "✨ TEXT [Anton]"], session.Document.Layers.Select(l => l.Name));
        Assert.NotEqual(session.Document.Layers[0].Id, session.Document.Layers[1].Id);
    });

    [Fact] // mode-pants-crash-hardcoded-keys: the front slot resolves per mode, so Add works on Pants too
    public void Add_works_on_a_Pants_document() => StaThread.Run(() =>
    {
        var session = Pants();
        var t = Make(session);
        int id = session.NextId;
        var expected = WordArt.CreateLayer(t.Settings, TemplateMode.Pants, id);

        t.AddCommand.Execute(null);

        var layer = Assert.Single(session.Document.Layers);
        Assert.Equal("✨ RIZZ [Anton]", layer.Name);
        Assert.True(Math.Abs(expected.Scale - layer.Scale) < 1e-6);
        Assert.True(Math.Abs(expected.X - layer.X) < 1e-6);
        Assert.True(Math.Abs(expected.Y - layer.Y) < 1e-6);
        Assert.Equal(layer.Id, session.SelectedLayerId);
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // ruling T1 (supersedes the plan's status-line row): one layer, one step, the requested size, no exception, no message
    public void A_2000_character_text_at_size_300_adds_one_layer_at_the_requested_size_with_no_message() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var status = new FakeStatus();
        var t = Make(session, status: status);
        t.Text = LongText(2000);
        t.Size = 300;
        Assert.Equal(2000, t.Text.Length);

        // 4.1's numbers for this text: the content size cw x ch the render would have had with no caps.
        var (probe, cw, ch, _) = WordArt.Render(t.Settings, 1, 1);
        probe.Dispose();
        Assert.True(cw > Images.MaxDimension, $"cw {cw}: the string must outgrow the caps for this test to mean anything");
        var front = Template.Slot(TemplateMode.Shirt, BodyPart.Body, Face.Front);
        double uncappedScale = Math.Min((double)front.W / cw, (double)front.H / ch) * 0.85;

        var thrown = Record.Exception(() => t.AddCommand.Execute(null));

        Assert.Null(thrown);
        Assert.Empty(status.Shown);
        Assert.Null(status.Current);
        Assert.Equal(1, session.UndoCount);
        var layer = Assert.Single(session.Document.Layers);
        Assert.InRange(layer.Image.Width, 1, Images.MaxDimension);
        Assert.InRange(layer.Image.Height, 1, Images.MaxDimension);
        Assert.True((long)layer.Image.Width * layer.Image.Height <= Images.MaxPixels);
        // On-canvas size = image size x layer scale = the uncapped size, to within the one image pixel the render rounds up by.
        Assert.Equal(cw * uncappedScale, layer.Image.Width * layer.Scale, layer.Scale + 1e-9);
        Assert.Equal(ch * uncappedScale, layer.Image.Height * layer.Scale, layer.Scale + 1e-9);
        Assert.Equal(layer.Id, session.SelectedLayerId);
        session.Undo();
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
    });

    // ---- the keyboard guard (TextBoxBase), with what exists today

    [Fact] // V, Delete and the arrows typed in the text box change neither the tool nor the layers
    public void Keys_typed_in_the_text_box_change_neither_the_tool_nor_the_layers() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1), LayerOf(2));
        session.Select(2);
        var main = TestShell.Quiet(TestShell.Main(session));
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        main.ActiveTab = RibbonTab.Text;
        StaThread.Pump();
        var pane = (TextRibbon)w.FindName("TextPane");
        var box = Part<TextBox>(pane, "WordArtText");
        main.Home.Tools.Set(Tool.Pen);
        w.FocusedElement = () => box;
        var layers = session.Document.Layers;
        using var source = new HwndSource(new HwndSourceParameters("text-tab-guard"));

        Assert.True(TextFieldShortcutCommand.TextFieldHasFocus(box));   // a TextBox is a TextBoxBase
        foreach (var key in new[] { Key.V, Key.B, Key.E, Key.Delete, Key.Back, Key.Left, Key.Right, Key.Up, Key.Down, Key.OemOpenBrackets })
        {
            foreach (var routed in new[] { Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent })
                box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = routed });
        }
        foreach (var typed in new[] { "v", "V", "b", "[", "]", "+", "-" })
            box.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, typed))
                { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
        StaThread.Pump();

        Assert.Equal(Tool.Pen, main.Home.Tools.Current);
        Assert.Same(layers, session.Document.Layers);
        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Equal(2, session.SelectedLayerId);
        Assert.Equal(0, session.UndoCount);
    });

    [Fact] // the box takes Enter as a new line, so a multi-line text can be typed
    public void The_text_box_is_multi_line() => StaThread.Run(() =>
    {
        var box = Part<TextBox>(Load(Make(new EditorSession())), "WordArtText");
        Assert.True(box.AcceptsReturn);
        Assert.Equal(TextWrapping.Wrap, box.TextWrapping);
        Assert.True(box.MinLines >= 2);
        Assert.Equal(ScrollBarVisibility.Auto, box.VerticalScrollBarVisibility);
    });

    // ---- the view: groups, labels and the 4.1 pixel values

    [Fact] // text-inputs, wordart-state-defaults: the group titles and their order
    public void The_pane_has_the_groups_Text_Colors_and_Size_Style_and_Preview() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        var groups = ((StackPanel)view.FindName("Groups")).Children.Cast<FrameworkElement>().ToArray();
        Assert.Equal(["TextGroup", "ColorsGroup", "StyleGroup", "PreviewGroup"], groups.Select(g => g.Name));
        var titles = Descendants<TextBlock>(view).Where(t => t.Text.Length > 0 && t.Text == t.Text.ToUpperInvariant() && t.FontSize == 10)
                                                  .Select(t => t.Text).ToArray();
        Assert.Equal(["TEXT", "COLORS & SIZE", "STYLE", "PREVIEW"], titles);
        // the last group has no divider (styles.css:207), the others have a 1px right one
        Assert.Equal([new Thickness(0, 0, 1, 0), new Thickness(0, 0, 1, 0), new Thickness(0, 0, 1, 0), new Thickness(0)],
                     groups.Select(g => ((Border)g).BorderThickness));
        Assert.Equal(new Thickness(10, 0, 10, 0), ((Border)groups[0]).Padding);
        Assert.True(((IRibbonPane)view).HasGroups);
    });

    [Fact] // the Text group: the 150 px box (min-height 36, #1a1a2e, 1px #333, 12px, padding 4x8) over the 150 px select (10px)
    public void The_Text_group_has_4_1s_pixel_values() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        var box = Part<TextBox>(view, "WordArtText");
        Assert.Equal((150.0, 36.0, 12.0, new Thickness(8, 4, 8, 4)), (box.Width, box.MinHeight, box.FontSize, box.Padding));
        Assert.Equal(Color.FromRgb(0x1a, 0x1a, 0x2e), ((SolidColorBrush)box.Background).Color);
        Assert.Equal(Color.FromRgb(0x33, 0x33, 0x33), ((SolidColorBrush)box.BorderBrush).Color);
        Assert.Equal(Color.FromRgb(0xe0, 0xe0, 0xe0), ((SolidColorBrush)box.Foreground).Color);
        Assert.Equal("RIZZ", box.Text);
        Assert.Equal(TextViewModel.Placeholder, box.Tag);   // the hint shown when the box is empty
        Assert.Equal("Word Art text", AutomationProperties.GetName(box));

        var font = Part<ComboBox>(view, "FontBox");
        Assert.Equal((150.0, 10.0, new Thickness(6, 2, 6, 2)), (font.Width, font.FontSize, font.Padding));
        Assert.Equal(Color.FromRgb(0x1a, 0x1a, 0x2e), ((SolidColorBrush)font.Background).Color);
        Assert.Equal(15, font.Items.Count);
        Assert.Equal("Anton", font.SelectedValue);
        Assert.Equal("Anton (Block)", ((FontChoice)font.SelectedItem).Label);
        Assert.Equal("Word Art font", AutomationProperties.GetName(font));
    });

    [Fact] // the Colors & Size group: three 28x20 wells with 8px #666 captions, the 44 px size box, 60 and 50 px sliders
    public void The_Colors_and_Size_group_has_4_1s_pixel_values() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        var wells = new[] { "FillWell", "StrokeWell", "GradWell" }.Select(n => Part<Button>(view, n)).ToArray();
        Assert.All(wells, w => Assert.Equal((28.0, 20.0), (w.Width, w.Height)));
        Assert.Equal([Color.FromRgb(0x17, 0xd4, 0xfa), Color.FromRgb(0, 0, 0), Color.FromRgb(0xf2, 0x2f, 0x89)],
                     wells.Select(w => ((SolidColorBrush)w.Background).Color));
        Assert.Equal(["Fill color", "Stroke color", "Gradient end color"], wells.Select(AutomationProperties.GetName));

        var captions = Descendants<TextBlock>(view).Where(t => t.FontSize == 8).ToArray();
        Assert.Equal(["Fill", "Stroke", "Grad", "Size", "Stroke W", "Angle"], captions.Select(c => c.Text));
        Assert.All(captions, c => Assert.Equal(Color.FromRgb(0x66, 0x66, 0x66), ((SolidColorBrush)c.Foreground).Color));

        var size = Part<TextBox>(view, "SizeBox");
        Assert.Equal((44.0, 10.0, "64"), (size.Width, size.FontSize, size.Text));
        Assert.Equal("Font size", AutomationProperties.GetName(size));

        var stroke = Part<Slider>(view, "StrokeWidthSlider");
        var angle = Part<Slider>(view, "AngleSlider");
        Assert.Equal((60.0, 0.0, 20.0, 3.0), (stroke.Width, stroke.Minimum, stroke.Maximum, stroke.Value));
        Assert.Equal((50.0, 0.0, 360.0, 0.0), (angle.Width, angle.Minimum, angle.Maximum, angle.Value));
        Assert.Equal("Stroke width", AutomationProperties.GetName(stroke));
        Assert.Equal("Gradient angle", AutomationProperties.GetName(angle));
    });

    [Fact] // the Style group: six buttons in a 3-column grid (gap 2), 9px, Solid active
    public void The_Style_group_is_a_3_column_grid_of_six_with_Solid_active() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        var list = Part<ListBox>(view, "StyleList");
        Assert.Equal(6, list.Items.Count);
        Assert.Equal(WordArtStyle.Solid, list.SelectedValue);
        Assert.Equal(3, Descendants<UniformGrid>(list).Single().Columns);
        var items = Enumerable.Range(0, 6).Select(i => (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i)).ToArray();
        Assert.Equal([true, false, false, false, false, false], items.Select(i => i.IsSelected));
        Assert.All(items, i => Assert.Equal(new Thickness(1), i.Margin));   // 1 + 1 = the 2px gap
        var captions = items.Select(i => Descendants<TextBlock>(i).Single()).ToArray();
        Assert.All(captions, c => Assert.Equal(9.0, c.FontSize));
        Assert.Equal(["Solid", "Gradient", "Neon", "Shadow", "Outline", "Retro"], captions.Select(c => c.Text));
        Assert.All(items.Select(i => Descendants<Border>(i).First()), b => Assert.Equal(new Thickness(6, 3, 6, 3), b.Padding));
        Assert.Equal(Color.FromRgb(0x17, 0xd4, 0xfa), ((SolidColorBrush)captions[0].Foreground).Color);   // the active one is cyan
        Assert.Equal("Word Art style", AutomationProperties.GetName(list));
    });

    [Fact] // clicking another style button selects it alone, and the view-model follows
    public void Selecting_a_style_button_makes_it_the_only_active_style() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var list = Part<ListBox>(Load(t), "StyleList");
        var neon = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(2);
        neon.IsSelected = true;
        StaThread.Pump();
        Assert.Equal(WordArtStyle.Neon, t.Style);
        Assert.Equal(1, list.Items.Cast<object>().Count(o => ((ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(o)).IsSelected));
    });

    [Fact] // the Preview group: a black box (1px #333, radius 4, min-height 44) over a full-width 10px Add to canvas
    public void The_Preview_group_has_4_1s_pixel_values() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var view = Load(t);
        var box = Part<Border>(view, "PreviewBox");
        Assert.Equal(Color.FromRgb(0, 0, 0), ((SolidColorBrush)box.Background).Color);
        Assert.Equal(Color.FromRgb(0x33, 0x33, 0x33), ((SolidColorBrush)box.BorderBrush).Color);
        Assert.Equal((new Thickness(1), new CornerRadius(4), 44.0), (box.BorderThickness, box.CornerRadius, box.MinHeight));
        Assert.True(box.ActualHeight >= 44);

        var image = Part<Image>(view, "PreviewImage");
        Assert.Same(t.Preview, image.Source);
        Assert.Equal((HorizontalAlignment.Center, VerticalAlignment.Center, Stretch.None), (image.HorizontalAlignment, image.VerticalAlignment, image.Stretch));
        Assert.True(image.ActualWidth <= 200 && image.ActualHeight <= 80);

        var add = Part<Button>(view, "AddButton");
        Assert.Equal(("Add to canvas", 10.0, new Thickness(8, 3, 8, 3)), ((string)add.Content, add.FontSize, add.Padding));
        Assert.Equal(box.ActualWidth, add.ActualWidth, 1);   // width: 100% of the group, which is the box
        Assert.Equal("Add to canvas", AutomationProperties.GetName(add));
        Assert.Equal(Color.FromRgb(0x05, 0x0c, 0x18), ((SolidColorBrush)add.Foreground).Color);   // the primary button
    });

    [Fact] // every interactive control is named and keyboard focusable with a focus ring where 4.1 had one
    public void Every_control_is_named() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        foreach (var name in new[] { "WordArtText", "FontBox", "FillWell", "StrokeWell", "GradWell", "SizeBox", "StrokeWidthSlider", "AngleSlider", "StyleList", "AddButton" })
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(Part<UIElement>(view, name))), name);
        Assert.All(new[] { "FillWell", "StrokeWell", "GradWell", "AddButton" }, n => Assert.NotNull(Part<Button>(view, n).FocusVisualStyle));
    });

    [Fact] // the controls edit the view-model, and the view-model drives the controls
    public void The_controls_bind_to_the_view_model() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var view = Load(t);
        Part<TextBox>(view, "WordArtText").Text = "NEW\nLINE";
        Part<Slider>(view, "StrokeWidthSlider").Value = 7;
        Part<Slider>(view, "AngleSlider").Value = 200;
        Part<ComboBox>(view, "FontBox").SelectedIndex = 3;
        StaThread.Pump();
        Assert.Equal(("NEW\nLINE", 7, 200, "Great Vibes"), (t.Text, t.StrokeWidth, t.Angle, t.Font));

        t.FillColor = "#abcdef";
        t.Size = 90;
        StaThread.Pump();
        Assert.Equal(Color.FromRgb(0xab, 0xcd, 0xef), ((SolidColorBrush)Part<Button>(view, "FillWell").Background).Color);
        Assert.Equal("90", Part<TextBox>(view, "SizeBox").Text);
    });

    [Fact] // the size box reads live, settles on lost focus and on Enter
    public void The_size_box_settles_on_lost_focus_and_on_Enter() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var view = Load(t);
        var box = Part<TextBox>(view, "SizeBox");
        box.Text = "5";
        Assert.Equal(8, t.Size);
        Assert.Equal("5", box.Text);
        box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        StaThread.Pump();
        Assert.Equal("8", box.Text);

        box.Text = "9000";
        using var source = new HwndSource(new HwndSourceParameters("text-tab-size"));
        box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        StaThread.Pump();
        Assert.Equal(("300", 300.0), (box.Text, t.Size));
    });

    [Fact] // the pane shows the live preview: a change swaps the image the view shows
    public void The_view_shows_the_preview_and_follows_it() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var view = Load(t);
        var image = Part<Image>(view, "PreviewImage");
        var first = image.Source;
        t.Text = "RIZZ UP";
        StaThread.Pump();
        Assert.NotSame(first, image.Source);
        Assert.Same(t.Preview, image.Source);
    });

    [Fact] // the pane tells the view-model the display's scale
    public void The_pane_sets_the_device_scale_from_its_DPI() => StaThread.Run(() =>
    {
        var t = Make(new EditorSession());
        var view = Load(t);
        Assert.Equal(VisualTreeHelper.GetDpi(view).DpiScaleX, t.DeviceScale);
    });

    [Fact] // clicking a color well opens the picker under itself; Add to canvas through UI Automation adds the layer
    public void The_wells_open_the_picker_under_themselves_and_the_button_adds() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var picker = new FakeColorPicker().Answer("#123456");
        var t = Make(session, picker);
        var view = Load(t);
        var well = Part<Button>(view, "FillWell");
        Invoke(well);
        Assert.Equal("#123456", t.FillColor);
        Assert.Same(well, Assert.Single(picker.Anchors));

        Invoke(Part<Button>(view, "AddButton"));
        Assert.Equal("✨ RIZZ [Anton]", Assert.Single(session.Document.Layers).Name);
    });

    // ---- the window

    [Fact] // the Text tab shows because the pane has groups (item 2's rule), with no test hook
    public void The_Text_tab_shows_in_the_window() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        Assert.Equal(Visibility.Visible, ((RadioButton)w.FindName("TextTab")).Visibility);
        Assert.True(((IRibbonPane)w.FindName("TextPane")).HasGroups);
        Assert.Same(main.Text, ((FrameworkElement)w.FindName("TextPane")).DataContext);
    });

    [Fact] // the shell end to end: pick another tool, open Text, click Add to canvas; the layer is selected and the Select tool is back
    public void The_composed_shell_adds_Word_Art_selects_it_and_sets_the_Select_tool() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1));
        var main = TestShell.Quiet(TestShell.Main(session));
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        main.Home.Tools.Set(Tool.Pen);
        main.ActiveTab = RibbonTab.Text;
        StaThread.Pump();
        var pane = (TextRibbon)w.FindName("TextPane");
        pane.Measure(new Size(1200, 200));
        pane.Arrange(new Rect(0, 0, 1200, 200));
        pane.UpdateLayout();

        Invoke(Part<Button>(pane, "AddButton"));

        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Equal("✨ RIZZ [Anton]", session.Document.Layers[^1].Name);
        Assert.Equal(session.Document.Layers[^1].Id, session.SelectedLayerId);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
    });

    [Fact] // the same on Pants
    public void The_composed_shell_adds_Word_Art_on_a_Pants_document() => StaThread.Run(() =>
    {
        var session = Pants();
        var main = TestShell.Quiet(TestShell.Main(session));
        main.Home.Tools.Set(Tool.Eraser);
        main.Text.AddCommand.Execute(null);
        Assert.Equal("✨ RIZZ [Anton]", Assert.Single(session.Document.Layers).Name);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        Assert.Equal(1, session.UndoCount);
    });
}
