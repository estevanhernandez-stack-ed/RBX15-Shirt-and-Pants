using System.Collections.Immutable;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Canvas;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Details tab (editor.html:175-222, editor.js:1834-2103; ledger details-settings-controls, details-buttons-inventory,
/// details-settings, details-buttons, details-layer-creation-pipeline). The engine side (the 16 generators, the autocrop and
/// the layer offsets) is held to the 4.1 goldens by <c>GoldenAppTests.Engine_export_matches_the_frozen_4_1_app_golden</c> over
/// the 37 frozen detail-* scenes in tests/goldens/app and by <c>DetailsTests</c>; these tests hold the tab: labels, groups,
/// defaults, the click pipeline and the window wiring.
/// </summary>
[Collection("MainWindow")] // the window facts build a MainWindow, as ShellLookTests does
public class DetailsTabTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    /// <summary>Every button as 4.1 lists it: group, label, data-detail id, layer name (details-buttons-inventory).</summary>
    public static TheoryData<string, string, string, string> Inventory() => new()
    {
        { "Neckline", "V-Neck", "collar-v", "V-Neck Collar" },
        { "Neckline", "Crew", "collar-crew", "Crew Neck" },
        { "Neckline", "Scoop", "collar-scoop", "Scoop Neck" },
        { "Neckline", "Henley", "collar-henley", "Henley Collar" },
        { "Edges", "L Cuff", "cuff-left", "Left Cuff" },
        { "Edges", "R Cuff", "cuff-right", "Right Cuff" },
        { "Edges", "Both", "cuffs-both", "Both Cuffs" },
        { "Edges", "Hem", "hem", "Hem Line" },
        { "Seams", "Center", "seam-center", "Center Seam" },
        { "Seams", "Sides", "seam-sides", "Side Seams" },
        { "Seams", "Shoulder", "shoulder-stripe", "Shoulder Stripe" },
        { "Seams", "Bands", "arm-bands", "Arm Bands" },
        { "Seams", "Yoke", "yoke", "Back Yoke" },
        { "Accents", "Pocket", "pocket", "Chest Pocket" },
        { "Accents", "Placket", "placket", "Button Placket" },
        { "Accents", "Full Set", "full-detail", "Full Detail Set" },
    };

    static DetailsViewModel Make(EditorSession session, FakeColorPicker? picker = null) => new(session, picker ?? new FakeColorPicker());

    static DetailButton Button(DetailsViewModel d, string id) => d.Groups.SelectMany(g => g.Buttons).Single(b => b.Id == id);

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

    static byte[] Pixels(SKImage image)
    {
        using var bmp = SKBitmap.FromImage(image);
        return bmp.Bytes;
    }

    // ---- details-settings-controls, details-settings: the defaults

    [Fact] // parityCheck: default values assertion
    public void The_settings_start_at_333333_width_2_and_no_stitch()
    {
        var d = Make(new EditorSession());
        Assert.Equal(("#333333", 2, false), (d.Color, d.Width, d.Stitch));
    }

    [Fact] // #detailWidth is an integer range 1..8 (editor.html:181)
    public void The_width_stays_between_1_and_8()
    {
        var d = Make(new EditorSession());
        d.Width = 0;
        Assert.Equal(1, d.Width);
        d.Width = 99;
        Assert.Equal(8, d.Width);
        d.Width = 5;
        Assert.Equal(5, d.Width);
    }

    [Fact]
    public void A_color_that_is_not_a_canvas_fill_is_refused()
    {
        var d = Make(new EditorSession());
        Assert.Throws<ArgumentException>(() => d.Color = "not-a-color");
        Assert.Equal("#333333", d.Color);
        d.Color = "#F22F89";
        Assert.Equal("#f22f89", d.Color);
    }

    // ---- details-buttons-inventory: labels, groups, order, ids, names

    [Fact]
    public void There_are_four_groups_titled_Neckline_Edges_Seams_and_Accents_with_16_buttons()
    {
        var d = Make(new EditorSession());
        Assert.Equal(["Neckline", "Edges", "Seams", "Accents"], d.Groups.Select(g => g.Label));
        Assert.Equal([4, 4, 5, 3], d.Groups.Select(g => g.Buttons.Count));
        Assert.Equal(16, d.Groups.Sum(g => g.Buttons.Count));
        Assert.Equal([false, false, false, true], d.Groups.Select(g => g.IsLast));
    }

    [Theory]
    [MemberData(nameof(Inventory))]
    public void Each_button_has_its_4_1_group_label_id_and_layer_name(string group, string label, string id, string layerName)
    {
        var d = Make(new EditorSession());
        var g = d.Groups.Single(x => x.Label == group);
        var b = g.Buttons.Single(x => x.Id == id);
        Assert.Equal(label, b.Label);
        Assert.Equal(layerName, b.LayerName);
        Assert.Equal(layerName, Details.LayerName(Details.FromId(id)));
        Assert.Equal(id == "full-detail", b.IsAccent);   // only Full Set has the pink accent
        Assert.False(string.IsNullOrWhiteSpace(b.AutomationName));
    }

    [Fact] // the order within and across groups is 4.1's (editor.html:187-222), not the engine enum's
    public void The_buttons_run_in_4_1s_order()
    {
        var d = Make(new EditorSession());
        Assert.Equal(
            ["collar-v", "collar-crew", "collar-scoop", "collar-henley", "cuff-left", "cuff-right", "cuffs-both", "hem",
             "seam-center", "seam-sides", "shoulder-stripe", "arm-bands", "yoke", "pocket", "placket", "full-detail"],
            d.Groups.SelectMany(g => g.Buttons).Select(b => b.Id));
    }

    // ---- details-layer-creation-pipeline, details-buttons: the click

    [Theory] // one click: one layer, one undo step, on top, selected, at the crop origin the engine computes, scale 1
    [MemberData(nameof(Inventory))]
    public void A_click_adds_one_selected_layer_in_one_step_at_the_engine_crop(string group, string label, string id, string layerName)
    {
        _ = (group, label);
        var session = SessionWith(LayerOf(1), LayerOf(2));
        var d = Make(session);
        int nextId = session.NextId;
        var expected = Details.Create(Details.FromId(id), TemplateMode.Shirt, new DetailStyle(CssColor.ToSk("#333333"), 2, false), nextId)!;
        int raised = 0;
        d.LayersAdded += (_, _) => raised++;

        Button(d, id).Command.Execute(null);

        Assert.Equal(1, session.UndoCount);
        Assert.Equal(3, session.Document.Layers.Count);
        var layer = session.Document.Layers[^1];   // pushed on top
        Assert.Equal((nextId, layerName, 1.0, 0.0), (layer.Id, layer.Name, layer.Scale, layer.Rotation));
        Assert.Equal((expected.X, expected.Y, expected.Image.Width, expected.Image.Height), (layer.X, layer.Y, layer.Image.Width, layer.Image.Height));
        Assert.Equal(Pixels(expected.Image), Pixels(layer.Image));
        Assert.Equal(layer.Id, session.SelectedLayerId);
        Assert.Equal(nextId + 1, session.NextId);
        Assert.Equal(1, raised);
    }

    [Fact] // the click is one undo step: Undo removes the layer and Redo brings it back
    public void Undo_removes_a_detail_layer_in_one_step()
    {
        var session = SessionWith(LayerOf(1));
        var d = Make(session);
        Button(d, "hem").Command.Execute(null);
        session.Undo();
        Assert.Single(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
        session.Redo();
        Assert.Equal("Hem Line", session.Document.Layers[^1].Name);
    }

    [Fact] // details-settings-controls parityCheck: changing width after creation leaves earlier layers untouched
    public void Changing_the_width_after_a_creation_leaves_the_earlier_layer_untouched()
    {
        var session = new EditorSession();
        var d = Make(session);
        Button(d, "collar-v").Command.Execute(null);
        var first = session.Document.Layers[0];
        byte[] firstPixels = Pixels(first.Image);

        d.Width = 8;
        d.Stitch = true;
        d.Color = "#ff0000";
        Assert.Same(first, session.Document.Layers[0]);
        Button(d, "collar-v").Command.Execute(null);

        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Same(first, session.Document.Layers[0]);
        Assert.Equal(firstPixels, Pixels(first.Image));
        Assert.NotEqual(firstPixels, Pixels(session.Document.Layers[1].Image));
    }

    [Fact] // the values are read when the button is clicked (editor.js:1840-1842)
    public void The_color_width_and_stitch_are_read_at_click_time()
    {
        var session = new EditorSession();
        var d = Make(session);
        d.Color = "#ff8800";
        d.Width = 6;
        d.Stitch = true;
        Button(d, "seam-center").Command.Execute(null);
        var expected = Details.Create(DetailType.SeamCenter, TemplateMode.Shirt, new DetailStyle(CssColor.ToSk("#ff8800"), 6, true), 1)!;

        var layer = session.Document.Layers[0];
        Assert.Equal((expected.X, expected.Y), (layer.X, layer.Y));
        Assert.Equal(Pixels(expected.Image), Pixels(layer.Image));
    }

    [Fact] // details-buttons divergence: a fully transparent color draws nothing, so no layer, no step, no id taken
    public void A_fully_transparent_color_creates_no_layer_and_no_step()
    {
        var session = SessionWith(LayerOf(1));
        var d = Make(session);
        d.Color = "transparent";
        int nextId = session.NextId;
        int raised = 0;
        d.LayersAdded += (_, _) => raised++;
        var doc = session.Document;

        foreach (var b in d.Groups.SelectMany(g => g.Buttons)) b.Command.Execute(null);

        Assert.Same(doc, session.Document);
        Assert.Equal((0, nextId, 0), (session.UndoCount, session.NextId, raised));
        Assert.Null(session.SelectedLayerId);
    }

    [Fact] // details-buttons divergence: 4.1 threw in Pants mode; v5 draws on the equivalent slots
    public void Every_button_works_on_a_Pants_document()
    {
        var session = Pants();
        var d = Make(session);
        foreach (var b in d.Groups.SelectMany(g => g.Buttons))
        {
            int before = session.Document.Layers.Count;
            b.Command.Execute(null);
            Assert.Equal(before + 1, session.Document.Layers.Count);
            Assert.Equal(b.LayerName, session.Document.Layers[^1].Name);
        }
        Assert.Equal(16, session.UndoCount);
    }

    [Fact] // Pants details match what the engine gives for Pants (not the Shirt crop)
    public void A_Pants_click_uses_the_Pants_crop()
    {
        var session = Pants();
        var d = Make(session);
        Button(d, "hem").Command.Execute(null);
        var expected = Details.Create(DetailType.Hem, TemplateMode.Pants, new DetailStyle(CssColor.ToSk("#333333"), 2, false), 1)!;
        Assert.Equal((expected.X, expected.Y), (session.Document.Layers[0].X, session.Document.Layers[0].Y));
    }

    // ---- the color well

    [Fact] // the pick opens the themed picker under the well, previews live, and a cancel restores the color; none of it is an undo step
    public void The_color_pick_previews_commits_and_cancels_without_touching_the_document()
    {
        var session = new EditorSession();
        var picker = new FakeColorPicker().Answer("#00ff00", "#112233").Answer(null, "#445566");
        var d = Make(session, picker);
        var anchor = new object();
        var seen = new List<string?>();
        d.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DetailsViewModel.Color)) seen.Add(d.Color); };

        d.PickColorCommand.Execute(anchor);
        Assert.Equal("#00ff00", d.Color);
        Assert.Equal(["#112233", "#00ff00"], seen);
        d.PickColorCommand.Execute(anchor);
        Assert.Equal("#00ff00", d.Color);   // cancelled: back to the color before the pick

        Assert.Equal(["#333333", "#00ff00"], picker.Seeds);
        Assert.All(picker.Anchors, a => Assert.Same(anchor, a));
        Assert.Equal(0, session.UndoCount);
    }

    // ---- the view

    static DetailsRibbon Load(DetailsViewModel d)
    {
        var view = new DetailsRibbon { DataContext = d };
        StaThread.Pump();
        view.Measure(new Size(1200, 200));
        view.Arrange(new Rect(0, 0, 1200, 200));
        view.UpdateLayout();
        return view;
    }

    [Fact] // group titles and labels as the ribbon shows them (parityCheck: UI test enumerates labels and group titles)
    public void The_pane_shows_Settings_then_the_four_groups_with_4_1s_labels() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        var groups = ((StackPanel)view.FindName("Groups")).Children;
        Assert.Equal(5, groups.Count);
        Assert.IsType<DetailsSettingsGroup>(groups[0]);
        Assert.All(groups.Cast<UIElement>().Skip(1), g => Assert.IsType<DetailButtonsGroup>(g));

        var titles = Descendants<TextBlock>(view).Where(t => t.Style is not null && t.Text.Length > 0 && t.Text == t.Text.ToUpperInvariant() && t.FontSize == 10).Select(t => t.Text).ToArray();
        Assert.Equal(["SETTINGS", "NECKLINE", "EDGES", "SEAMS", "ACCENTS"], titles);

        var buttons = Descendants<Button>(view).Where(b => b.Content is string && b.Name != "DetailColor").ToArray();
        Assert.Equal(
            ["V-Neck", "Crew", "Scoop", "Henley", "L Cuff", "R Cuff", "Both", "Hem", "Center", "Sides", "Shoulder", "Bands", "Yoke", "Pocket", "Placket", "Full Set"],
            buttons.Select(b => (string)b.Content));
    });

    [Fact] // every control has an automation name; Full Set has the pink accent and the last group has no divider
    public void Every_control_is_named_Full_Set_is_pink_and_the_last_group_has_no_divider() => StaThread.Run(() =>
    {
        var view = Load(Make(new EditorSession()));
        var buttons = Descendants<Button>(view).ToArray();
        Assert.Equal(17, buttons.Length);   // 16 detail buttons and the color well
        Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)), b.Content?.ToString()));
        Assert.Equal("Detail color", AutomationProperties.GetName((Button)((DetailsSettingsGroup)view.FindName("Settings")).FindName("DetailColor")));
        var settings = (DetailsSettingsGroup)view.FindName("Settings");
        Assert.Equal("Detail width", AutomationProperties.GetName((Slider)settings.FindName("WidthSlider")));
        Assert.Equal("Stitch", AutomationProperties.GetName((CheckBox)settings.FindName("StitchBox")));
        Assert.All(Descendants<Button>(view), b => Assert.NotNull(b.FocusVisualStyle));

        var full = buttons.Single(b => Equals(b.Content, "Full Set"));
        Assert.Equal(Color.FromRgb(0xf2, 0x2f, 0x89), ((SolidColorBrush)full.BorderBrush).Color);
        Assert.Equal(Color.FromRgb(0xff, 0x5a, 0xa3), ((SolidColorBrush)full.Foreground).Color);
        var other = buttons.Single(b => Equals(b.Content, "Hem"));
        Assert.NotEqual(((SolidColorBrush)full.BorderBrush).Color, ((SolidColorBrush)other.BorderBrush).Color);

        var frames = new[] { "Neckline", "Edges", "Seams", "Accents" }
            .Select(n => ((Border)((DetailButtonsGroup)view.FindName(n)).FindName("Frame")).BorderThickness).ToArray();
        Assert.Equal([new Thickness(0, 0, 1, 0), new Thickness(0, 0, 1, 0), new Thickness(0, 0, 1, 0), new Thickness(0)], frames);
    });

    [Fact] // the Settings controls edit the view-model: slider 1..8 ticks, Stitch box, and the well shows the color
    public void The_settings_controls_bind_to_the_view_model() => StaThread.Run(() =>
    {
        var d = Make(new EditorSession());
        var view = Load(d);
        var settings = (DetailsSettingsGroup)view.FindName("Settings");
        var slider = (Slider)settings.FindName("WidthSlider");
        var stitch = (CheckBox)settings.FindName("StitchBox");
        var well = (Button)settings.FindName("DetailColor");

        Assert.Equal((1.0, 8.0, 2.0, 50.0), (slider.Minimum, slider.Maximum, slider.Value, slider.Width));
        Assert.True(slider.IsSnapToTickEnabled);
        Assert.False(stitch.IsChecked);
        Assert.Equal(Color.FromRgb(0x33, 0x33, 0x33), ((SolidColorBrush)well.Background).Color);

        slider.Value = 7;
        stitch.IsChecked = true;
        d.Color = "#abcdef";
        StaThread.Pump();
        Assert.Equal((7, true), (d.Width, d.Stitch));
        Assert.Equal(Color.FromRgb(0xab, 0xcd, 0xef), ((SolidColorBrush)well.Background).Color);
    });

    [Fact] // the well opens the picker under itself
    public void Clicking_the_color_well_opens_the_picker_under_it() => StaThread.Run(() =>
    {
        var picker = new FakeColorPicker().Answer("#123456");
        var d = Make(new EditorSession(), picker);
        var view = Load(d);
        var well = (Button)((DetailsSettingsGroup)view.FindName("Settings")).FindName("DetailColor");
        Invoke(well);
        Assert.Equal("#123456", d.Color);
        Assert.Same(well, Assert.Single(picker.Anchors));
    });

    // ---- the window

    [Fact] // the Details tab shows because the pane has groups (item 2's rule); the Text tab does too since item 9
    public void The_Details_tab_shows_in_the_window() => StaThread.Run(() =>
    {
        var w = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        Assert.Equal(Visibility.Visible, ((RadioButton)w.FindName("DetailsTab")).Visibility);
        Assert.Equal(Visibility.Visible, ((RadioButton)w.FindName("TextTab")).Visibility);
        Assert.True(((IRibbonPane)w.FindName("DetailsPane")).HasGroups);
    });

    [Fact] // the shell end to end: pick another tool, open Details, click Pocket; the layer is selected and the Select tool is back
    public void The_composed_shell_creates_a_detail_selects_it_and_sets_the_Select_tool() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1));
        var main = TestShell.Quiet(TestShell.Main(session));
        var w = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        main.Home.Tools.Set(Tool.Pen);
        main.ActiveTab = RibbonTab.Details;
        StaThread.Pump();
        var pane = (DetailsRibbon)w.FindName("DetailsPane");
        pane.Measure(new Size(1200, 200));
        pane.Arrange(new Rect(0, 0, 1200, 200));
        pane.UpdateLayout();
        var pocket = Descendants<Button>(pane).Single(b => Equals(b.Content, "Pocket"));

        Invoke(pocket);

        Assert.Equal(2, session.Document.Layers.Count);
        Assert.Equal("Chest Pocket", session.Document.Layers[^1].Name);
        Assert.Equal(session.Document.Layers[^1].Id, session.SelectedLayerId);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        Assert.Same(main.Details, pane.DataContext);
    });

    [Fact] // a transparent color in the composed shell leaves the tool alone (nothing was added)
    public void The_composed_shell_keeps_the_tool_when_nothing_is_drawn() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var main = TestShell.Quiet(TestShell.Main(session));
        main.Home.Tools.Set(Tool.Pen);
        main.Details.Color = "transparent";
        Button(main.Details, "hem").Command.Execute(null);
        Assert.Equal((Tool.Pen, 0, 0), (main.Home.Tools.Current, session.UndoCount, session.Document.Layers.Count));
    });
}
