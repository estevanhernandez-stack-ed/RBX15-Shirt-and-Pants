using System.Collections.Immutable;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
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
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The palette of the user's art and where it is offered (ledger art-palette, new): the session-following ArtPalette, the
/// picker's "From your art" row, and the Brush group's Palette button. Views are checked by their tree and bindings, never by pixel.
/// </summary>
public class ArtPaletteTests
{
    static readonly SKColor Blue = new(0, 0, 255);

    static Layer Of(int id, SKColor color) => new() { Id = id, Image = Solid(10, 10, color), Name = "Image " + id };

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>Lays <paramref name="view"/> out; a window that was never shown is laid out through its content, as the picker's own placement does.</summary>
    static void Layout(FrameworkElement view)
    {
        StaThread.Pump();
        if (view is Window w) view = (FrameworkElement)w.Content;
        view.Measure(new Size(2000, 2000));
        view.Arrange(new Rect(view.DesiredSize));
        view.UpdateLayout();
    }

    // ---- ArtPalette ----

    [Fact] // the first look is at construction; it reads what the session already holds
    public void It_starts_from_the_document_it_is_given() => StaThread.Run(() =>
    {
        var session = SessionWith(Of(1, SKColors.Red));
        var palette = new ArtPalette(session);
        Assert.Equal(new[] { "#ff0000" }, palette.Colors);
        Assert.Equal(1, palette.Recomputes);
        Assert.Empty(new ArtPalette(new EditorSession()).Colors);
    });

    [Fact] // many layer changes in one dispatcher turn cost one recompute
    public void Many_layer_changes_in_one_turn_recompute_once() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var palette = new ArtPalette(session);
        var names = Track(palette);
        for (int i = 1; i <= 5; i++)
        {
            var layer = Of(i, i % 2 == 0 ? Blue : SKColors.Red);
            session.Apply(d => d with { Layers = d.Layers.Add(layer) });
        }
        Assert.Equal(1, palette.Recomputes);   // nothing yet: the pass waits for an idle moment
        StaThread.Pump();
        Assert.Equal(2, palette.Recomputes);
        Assert.Equal(new[] { "#ff0000", "#0000ff" }, palette.Colors);
        Assert.Contains(nameof(ArtPalette.Colors), names);
    });

    [Fact] // moving a layer, a view toggle and a region fill leave the images alone
    public void A_change_that_is_not_to_the_visible_images_does_not_recompute() => StaThread.Run(() =>
    {
        var session = SessionWith(Of(1, SKColors.Red));
        var palette = new ArtPalette(session);
        var names = Track(palette);
        StaThread.Pump();
        Assert.Equal(1, palette.Recomputes);

        session.Apply(d => d with { ShowGrid = !d.ShowGrid });
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#00ff00")));
        session.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { X = 40, Opacity = 0.5 }) });
        StaThread.Pump();
        Assert.Equal(1, palette.Recomputes);
        Assert.DoesNotContain(nameof(ArtPalette.Colors), names);
        Assert.Equal(new[] { "#ff0000" }, palette.Colors);   // a region fill is not "your art"
    });

    [Fact]
    public void Hiding_a_layer_takes_its_colors_out_and_showing_it_brings_them_back() => StaThread.Run(() =>
    {
        var session = SessionWith(Of(1, SKColors.Red), Of(2, Blue));
        var palette = new ArtPalette(session);
        Assert.Equal(2, palette.Colors.Count);

        session.Apply(d => d with { Layers = d.Layers.SetItem(1, d.Layers[1] with { Visible = false }) });
        StaThread.Pump();
        Assert.Equal(new[] { "#ff0000" }, palette.Colors);
        Assert.Equal(2, palette.Recomputes);

        session.Apply(d => d with { Layers = d.Layers.SetItem(1, d.Layers[1] with { Visible = true }) });
        StaThread.Pump();
        Assert.Equal(2, palette.Colors.Count);
        Assert.Equal(3, palette.Recomputes);
    });

    [Fact] // opening a project swaps the images
    public void It_follows_a_replaced_document() => StaThread.Run(() =>
    {
        var session = SessionWith(Of(1, SKColors.Red));
        var palette = new ArtPalette(session);
        session.Apply(d => d with { Layers = ImmutableList.Create(Of(2, Blue)) });
        StaThread.Pump();
        Assert.Equal(new[] { "#0000ff" }, palette.Colors);
    });

    [Fact]
    public void A_disposed_palette_stops_following() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var palette = new ArtPalette(session);
        palette.Dispose();
        session.Apply(d => d with { Layers = d.Layers.Add(Of(1, SKColors.Red)) });
        StaThread.Pump();
        Assert.Equal(1, palette.Recomputes);
    });

    // ---- the picker ----

    [Fact]
    public void A_picker_has_no_palette_unless_it_is_given_one()
    {
        Assert.Empty(new ColorPickerViewModel("#ff0000").Palette);
        Assert.Equal(new[] { "#00ff00" }, new ColorPickerViewModel("#ff0000", null, new[] { "#00ff00" }).Palette);
    }

    [Fact] // a swatch sets the color as typing its hex would: the preview fires, the field follows, the pick stays open
    public void A_swatch_sets_the_color_and_previews()
    {
        var seen = new List<string>();
        var vm = new ColorPickerViewModel("#ff0000", seen.Add, new[] { "#00ff00" });
        Assert.True(vm.UseSwatch("#00FF00"));
        Assert.Equal(new[] { "#00ff00" }, seen);
        Assert.Equal(("#00ff00", "#00ff00"), (vm.Hex, vm.HexText));
        Assert.False(vm.IsFinished);

        var typed = new List<string>();
        var twin = new ColorPickerViewModel("#ff0000", typed.Add) { HexText = "#00ff00" };
        Assert.Equal((twin.Hex, twin.Hue, twin.Saturation, twin.Value), (vm.Hex, vm.Hue, vm.Saturation, vm.Value));
        Assert.Equal(seen, typed);

        Assert.False(vm.UseSwatch("not a color"));
        Assert.Equal("#00ff00", vm.Hex);
        vm.Commit();
        Assert.Equal("#00ff00", vm.Result);
        Assert.False(vm.UseSwatch("#0000ff"));   // the pick has ended
        Assert.Equal("#00ff00", vm.Hex);
    }

    [Fact] // the row shows only when there is something to show
    public void The_popup_shows_the_palette_row_only_when_the_palette_is_not_empty() => StaThread.Run(() =>
    {
        var empty = new ColorPickerPopup(new ColorPickerViewModel("#ff0000"));
        Layout(empty);
        Assert.Equal(Visibility.Collapsed, empty.PaletteSection.Visibility);

        var full = new ColorPickerPopup(new ColorPickerViewModel("#ff0000", null, new[] { "#00ff00", "#0000ff", "#ffff00" }));
        Layout(full);
        Assert.Equal(Visibility.Visible, full.PaletteSection.Visibility);
        var label = Descendants<TextBlock>(full.PaletteSection).First();
        Assert.Equal("FROM YOUR ART", label.Text);
        var swatches = Descendants<Button>(full.PaletteSwatches).ToArray();
        Assert.Equal(new[] { "#00ff00", "#0000ff", "#ffff00" }, swatches.Select(AutomationProperties.GetName));
        Assert.All(swatches, s =>
        {
            Assert.True(s.Focusable && s.IsTabStop);
            Assert.Equal(AutomationProperties.GetName(s), s.ToolTip);
            Assert.Equal((20.0, 20.0), (s.Width, s.Height));
        });
        var fullSize = ((FrameworkElement)full.Content).DesiredSize;
        var emptySize = ((FrameworkElement)empty.Content).DesiredSize;
        Assert.True(fullSize.Height > emptySize.Height);
        Assert.Equal(fullSize.Width, emptySize.Width);   // wraps at the square's width
    });

    [Fact] // a click sets the color and leaves the picker open; Enter on a swatch is its click, not the picker's OK
    public void Clicking_a_swatch_sets_the_color_without_ending_the_pick() => StaThread.Run(() =>
    {
        var seen = new List<string>();
        var vm = new ColorPickerViewModel("#ff0000", seen.Add, new[] { "#00ff00", "#0000ff" });
        var popup = new ColorPickerPopup(vm);
        Layout(popup);
        var swatch = Descendants<Button>(popup.PaletteSwatches).Last();
        swatch.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(("#0000ff", false), (vm.Hex, vm.IsFinished));
        Assert.Equal(new[] { "#0000ff" }, seen);
        Assert.Equal("#0000ff", popup.HexBox.Text);

        Assert.False(popup.HandleKey(Key.Enter, ModifierKeys.None, swatch));
        Assert.False(vm.IsFinished);
        Assert.True(popup.HandleKey(Key.Enter, ModifierKeys.None, popup.HexBox));   // elsewhere Enter still commits
        Assert.Equal("#0000ff", vm.Result);
    });

    [Fact] // every themed pick gets the palette: the app's picker reads it from the one ArtPalette
    public void The_app_picker_is_fed_by_the_art_palette() => StaThread.Run(() =>
    {
        var session = SessionWith(Of(1, SKColors.Red));
        var main = TestShell.Quiet(Composition.Build(session, () => null, new FakeSettingsStore()));
        var field = typeof(BrushViewModel).GetField("_picker", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var picker = Assert.IsType<ThemedColorPicker>(field.GetValue(main.Home.Brush));
        Assert.NotNull(picker.Palette);
        Assert.Equal(new[] { "#ff0000" }, picker.Palette!());
        Assert.Same(main.Palette, main.Home.Brush.Palette);
    });

    // ---- the Brush group ----

    static BrushGroup Group(BrushViewModel vm)
    {
        var view = new BrushGroup { DataContext = vm };
        Layout(view);
        return view;
    }

    /// <summary>The popup's content isn't in the visual tree until the popup opens, so measure it on its own.</summary>
    static FrameworkElement Content(BrushGroup view)
    {
        var child = (FrameworkElement)view.PalettePopup.Child;
        Layout(child);
        return child;
    }

    [Fact]
    public void The_brush_group_has_a_palette_button_that_fits_the_fill_row() => StaThread.Run(() =>
    {
        var view = Group(new BrushViewModel(new FakeColorPicker()));
        var button = Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Palette");
        Assert.Same(view.PaletteButton, button);
        Assert.Equal("Palette", button.Content);
        Assert.True(button.IsTabStop);
        Assert.True(button.ActualHeight <= view.BrushFill.ActualHeight, "the group must not grow for the button");
        Assert.True(view.PaletteButton.TranslatePoint(new Point(), view).X >= view.BrushFill.ActualWidth);   // right of Fill
        Assert.False(view.PalettePopup.StaysOpen);
        Assert.Equal(PlacementMode.Bottom, view.PalettePopup.Placement);
        Assert.Same(view.PaletteButton, view.PalettePopup.PlacementTarget);
    });

    [Fact] // empty palette: one muted line, no swatches
    public void An_empty_palette_shows_the_hint() => StaThread.Run(() =>
    {
        var view = Group(new BrushViewModel(new FakeColorPicker()));
        Content(view);
        Assert.Equal(Visibility.Visible, view.PaletteEmpty.Visibility);
        Assert.Equal("Add an image or asset to build a palette.", view.PaletteEmpty.Text);
        Assert.Equal(Visibility.Collapsed, view.PaletteSwatches.Visibility);
    });

    [Fact] // a swatch sets the brush color, closes the popup, and is no undo step
    public void Choosing_a_palette_color_sets_the_brush_and_is_no_undo_step() => StaThread.Run(() =>
    {
        var session = SessionWith(Of(1, SKColors.Red), Of(2, Blue));
        var palette = new ArtPalette(session);
        var vm = new BrushViewModel(new FakeColorPicker()) { Palette = palette };
        var view = Group(vm);
        Content(view);
        Assert.Equal(Visibility.Collapsed, view.PaletteEmpty.Visibility);
        var swatches = Descendants<Button>(view.PaletteSwatches).ToArray();
        Assert.Equal(palette.Colors, swatches.Select(AutomationProperties.GetName));

        var doc = session.Document;
        int undo = session.UndoCount;
        swatches[1].Command.Execute(swatches[1].CommandParameter);
        Assert.Equal(palette.Colors[1], vm.Color);
        Assert.Same(doc, session.Document);
        Assert.Equal(undo, session.UndoCount);
        Assert.False(session.IsDirty);
    });

    [Fact]
    public void The_brush_command_sets_the_color_and_ignores_a_missing_one()
    {
        var vm = new BrushViewModel(new FakeColorPicker());
        var names = Track(vm);
        vm.UsePaletteColorCommand.Execute("#12AB34");
        Assert.Equal("#12ab34", vm.Color);
        Assert.Contains(nameof(BrushViewModel.SkColor), names);
        vm.UsePaletteColorCommand.Execute(null);
        Assert.Equal("#12ab34", vm.Color);
        Assert.Empty(vm.PaletteColors);
        Assert.False(vm.HasPaletteColors);
    }

    [Fact] // the popup follows the palette as the art changes
    public void The_brush_palette_follows_the_art() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = new BrushViewModel(new FakeColorPicker()) { Palette = new ArtPalette(session) };
        var names = Track(vm);
        session.Apply(d => d with { Layers = d.Layers.Add(Of(1, SKColors.Red)) });
        StaThread.Pump();
        Assert.Equal(new[] { "#ff0000" }, vm.PaletteColors);
        Assert.True(vm.HasPaletteColors);
        Assert.Contains(nameof(BrushViewModel.PaletteColors), names);
    });
}
