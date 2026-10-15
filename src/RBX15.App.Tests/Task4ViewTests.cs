using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Task 4's views load, bind and expose automation names (spec §4 Accessibility). They are laid out
/// off-screen on StaThread, whose Application holds Task 1's theme. Theme keys are DynamicResource,
/// so a misspelled key would fail silently here; Task 5's ThemeKeyGuardTests catches it.
/// </summary>
public class Task4ViewTests
{
    /// <summary>Runs the dispatcher queue once so the bindings attach (StaThread.Pump), then lays the view out.</summary>
    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(280, 2000));
        e.Arrange(new Rect(0, 0, 280, 2000));
        e.UpdateLayout();
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

    static LayersViewModel Layers(EditorSession session) => new(session, new ImportService(session, new FakeStatus()), new FakeFileDialogs());

    [Fact]
    public void The_layers_panel_shows_one_named_row_per_layer_with_named_buttons() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            using var surface = SKSurface.Create(new SKImageInfo(4, 4));
            var img = surface.Snapshot();
            s = LayerOps.AddImage(s, img, session.TakeId(), "Back");
            return LayerOps.AddImage(s, img, session.TakeId(), "Logo");
        });
        session.Apply(d => LayerOps.ToggleVisibility(d, 1));
        var panel = new LayersPanel { DataContext = Layers(session) };
        Layout(panel);

        var items = Descendants<ListBoxItem>(panel).ToList();
        Assert.Equal(new[] { "Logo", "Back (hidden)" }, items.Select(AutomationProperties.GetName));
        Assert.True(items[0].IsSelected);
        var buttons = Descendants<Button>(items[1]).Select(AutomationProperties.GetName).ToList();
        Assert.Equal(new[] { "Dupe Back", "Show Back", "Delete Back" }, buttons);
        var all = Descendants<Button>(panel).Select(b => b.Content as string).ToList();
        Assert.Contains("Up", all);
        Assert.Contains("Down", all);
    });

    [Fact] // smoke find: a string Content made the button peers strip the first "_" as an access-key marker
    public void Row_button_names_keep_every_underscore_through_their_automation_peers() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            using var surface = SKSurface.Create(new SKImageInfo(4, 4));
            return LayerOps.AddImage(s, surface.Snapshot(), session.TakeId(), "drip_blood_front_v2");
        });
        var panel = new LayersPanel { DataContext = Layers(session) };
        Layout(panel);

        var item = Assert.Single(Descendants<ListBoxItem>(panel));
        var names = Descendants<Button>(item).Select(b => UIElementAutomationPeer.CreatePeerForElement(b).GetName()).ToList();
        Assert.Equal(new[] { "Dupe drip_blood_front_v2", "Hide drip_blood_front_v2", "Delete drip_blood_front_v2" }, names);
        Assert.Equal("drip_blood_front_v2", UIElementAutomationPeer.CreatePeerForElement(item).GetName());
        var labels = Descendants<Button>(item).Select(b => ((TextBlock)b.Content).Text).ToList();
        Assert.Equal(new[] { "Dupe", "Hide", "Del" }, labels);
    });

    [Fact] // controller ruling 3: drag reorder is M3b, so the heading carries no "(drag to reorder)" hint
    public void The_layers_panel_header_shows_no_drag_to_reorder_hint() => StaThread.Run(() =>
    {
        var panel = new LayersPanel { DataContext = Layers(new EditorSession()) };
        Layout(panel);
        var texts = Descendants<TextBlock>(panel).Select(t => t.Text).ToList();
        Assert.Contains("Layers", texts);
        Assert.DoesNotContain(texts, t => t.Contains("drag", StringComparison.OrdinalIgnoreCase) || t.Contains("reorder", StringComparison.OrdinalIgnoreCase));
    });

    [Fact]
    public void The_drop_zone_is_a_named_focusable_button_that_accepts_drops() => StaThread.Run(() =>
    {
        var zone = new DropZone { DataContext = Layers(new EditorSession()) };
        Layout(zone);
        var button = Assert.Single(Descendants<Button>(zone));
        Assert.True(button.Focusable);
        Assert.True(button.AllowDrop);
        Assert.Equal("Drop art here. Drag and drop images, or press to browse.", AutomationProperties.GetName(button));
        Assert.Same(((LayersViewModel)zone.DataContext).BrowseCommand, button.Command);
    });

    [Fact]
    public void The_file_group_shows_add_image_and_download_template_only() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var status = new FakeStatus();
        var group = new FileGroup { DataContext = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)) };
        Layout(group);
        var labels = Descendants<Button>(group).Select(b => b.Content as string).ToArray();
        Assert.Equal(new[] { "Add image", "Download template" }, labels);
    });

    [Fact]
    public void The_status_strip_collapses_without_a_message() => StaThread.Run(() =>
    {
        var vm = new StatusViewModel();
        var strip = new StatusStrip { DataContext = vm };
        Layout(strip);
        var border = Assert.IsType<Border>(strip.FindName("Strip"));
        Assert.Equal(Visibility.Collapsed, border.Visibility);
        vm.Show("Couldn't add a.svg: no.");
        Assert.Equal(Visibility.Visible, border.Visibility);
        Assert.Equal("Couldn't add a.svg: no.", Assert.IsType<TextBlock>(strip.FindName("MessageText")).Text);
    });
}
