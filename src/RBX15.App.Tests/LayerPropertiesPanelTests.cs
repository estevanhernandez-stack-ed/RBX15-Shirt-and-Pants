using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Canvas;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The "Selected layer" view (editor.html:320-323, editor.js:746-912): it binds, it is never rebuilt, each
/// slider press is one step, typed fields commit on Enter or on a press elsewhere, and every control has a name
/// (spec §4 Accessibility). Laid out off-screen on StaThread, whose Application holds Task 1's theme.
/// </summary>
[Collection("MainWindow")] // see ShellLookTests
public class LayerPropertiesPanelTests
{
    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(252, 4000));
        e.Arrange(new Rect(0, 0, 252, 4000));
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

    static T Named<T>(FrameworkElement view, string name) => (T)view.FindName(name);

    /// <summary>Visible on screen if laid out: it and every visual ancestor are Visibility.Visible (IsVisible needs a window).</summary>
    static bool Shown(DependencyObject e)
    {
        for (var d = e; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    static (EditorSession Session, LayerPropertiesViewModel Props, LayerPropertiesPanel View) Make(Func<bool>? leftDown = null, params Layer[] layers)
    {
        var session = SessionWith(layers.Length == 0 ? [LayerOf(1) with { Name = "drip_red", X = 12.7, Y = 30 }] : layers);
        session.Select(session.Document.Layers[^1].Id);
        var props = new LayerPropertiesViewModel(session);
        var view = new LayerPropertiesPanel(leftDown ?? (() => true)) { DataContext = props };
        Layout(view);
        return (session, props, view);
    }

    [Fact] // layers-empty-state: "Select a layer to edit." as .info (upper case, mono 10px fg-muted), controls hidden
    public void The_empty_state_shows_the_info_text_and_hides_the_controls() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        Assert.Equal(Visibility.Collapsed, Named<TextBlock>(view, "EmptyState").Visibility);
        Assert.Equal(Visibility.Visible, Named<StackPanel>(view, "Controls").Visibility);

        session.Select(null);
        Layout(view);
        var empty = Named<TextBlock>(view, "EmptyState");
        Assert.Equal(Visibility.Visible, empty.Visibility);
        Assert.Equal("SELECT A LAYER TO EDIT.", empty.Text);
        Assert.Equal((10.0, "#FF5E6B7F"), (empty.FontSize, ((SolidColorBrush)empty.Foreground).Color.ToString()));
        Assert.Equal(Visibility.Collapsed, Named<StackPanel>(view, "Controls").Visibility);
    });

    [Fact] // the controls show the layer (editor.js:811-823) and the adjustments start closed with ▶ (editor.js:782, :784)
    public void The_controls_bind_to_the_selected_layer() => StaThread.Run(() =>
    {
        var (_, _, view) = Make();
        Assert.Equal("drip_red", Named<TextBox>(view, "NameBox").Text);
        Assert.Equal(("13", "30"), (Named<TextBox>(view, "XBox").Text, Named<TextBox>(view, "YBox").Text));
        Assert.Equal("X: 13 \u00A0 Y: 30", Named<TextBlock>(view, "PositionText").Text);
        Assert.Equal(("100%", "0\u00B0", "100%"), (Named<Run>(view, "ScaleText").Text, Named<Run>(view, "RotationText").Text, Named<Run>(view, "OpacityText").Text));
        Assert.Equal((100.0, 0.0, 100.0), (Named<Slider>(view, "ScaleSlider").Value, Named<Slider>(view, "RotationSlider").Value, Named<Slider>(view, "OpacitySlider").Value));
        var adjustments = Named<Expander>(view, "Adjustments");
        Assert.Equal("Image adjustments", AutomationProperties.GetName(adjustments));   // controller ruling 2: the Expander and its toggle
        Assert.False(adjustments.IsExpanded);
        Assert.Equal("\u25B6", Descendants<TextBlock>(adjustments).First(t => t.Name == "Arrow").Text);
    });

    [Fact] // editor.js:762-799: the ten ranges, in panel order
    public void The_ten_sliders_have_4_1s_ranges() => StaThread.Run(() =>
    {
        var (_, _, view) = Make();
        Assert.Equal(
            new[] { (1.0, 500.0), (-180, 180), (0, 100), (0, 200), (0, 200), (0, 200), (0, 360), (-100, 100), (-100, 100), (-100, 100) },
            view.Sliders.Select(s => (s.Minimum, s.Maximum)));
        Assert.All(view.Sliders, s => Assert.True(s.IsSnapToTickEnabled && s.TickFrequency == 1 && s.SmallChange == 1));
    });

    [Fact] // spec §4 Accessibility: every control has a name; 4.1's button titles are the tooltips (editor.js:768-774)
    public void Every_control_is_named_and_the_buttons_keep_4_1s_titles() => StaThread.Run(() =>
    {
        var (_, props, view) = Make();
        props.AdjustmentsExpanded = true;
        Layout(view);
        var focusable = Descendants<Control>(view).Where(c => c.Focusable && Shown(c) && c is not ScrollViewer).ToList();
        Assert.Equal(21, focusable.OfType<TextBox>().Count() + focusable.OfType<Slider>().Count() + focusable.OfType<ButtonBase>().Count());
        Assert.All(focusable, c => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(c)), c.GetType().Name + " " + c.Name));
        Assert.Equal(
            new[] { "Flip horizontally.", "Flip vertically.", "Duplicate layer.", "Move layer up.", "Move layer down." },
            new[] { "FlipHButton", "FlipVButton", "DuplicateButton", "UpButton", "DownButton" }.Select(n => (string)Named<Button>(view, n).ToolTip));
        var center = Named<Button>(view, "CenterFrontButton");       // props-center-front (M4b item 5): 4.1's button has no title, so it has no tooltip
        Assert.Equal("Center on front torso", center.Content);
        Assert.Equal("Center on front torso", AutomationProperties.GetName(center));
        Assert.Null(center.ToolTip);
        Assert.Same(Application.Current.FindResource("FocusRing"), center.FocusVisualStyle);
    });

    [Fact] // props-center-front: full width, under Up and Down and above Image adjustments (editor.js:776-778)
    public void The_center_button_is_full_width_between_Up_Down_and_the_adjustments() => StaThread.Run(() =>
    {
        var (_, _, view) = Make();
        var center = Named<Button>(view, "CenterFrontButton");
        var controls = Named<StackPanel>(view, "Controls");
        var order = controls.Children.Cast<UIElement>().ToList();

        Assert.Equal(HorizontalAlignment.Stretch, center.HorizontalAlignment);
        Assert.Equal(controls.ActualWidth, center.ActualWidth, 0.5);
        Assert.Equal(order.IndexOf((UIElement)Named<Button>(view, "DownButton").Parent) + 1, order.IndexOf(center));
        Assert.Equal(order.IndexOf(center) + 1, order.IndexOf(Named<Expander>(view, "Adjustments")));
    });

    [Theory] // props-center-front parityCheck: numeric x,y for several scales; scale and rotation stay; one undo step
    [InlineData(1.0, TemplateMode.Shirt)]
    [InlineData(0.5, TemplateMode.Shirt)]
    [InlineData(0.25, TemplateMode.Shirt)]
    [InlineData(0.5, TemplateMode.Pants)]   // the label stays "front torso" and the Body Front slot is the target in Pants too
    public void Center_on_front_torso_is_one_undo_step_that_leaves_scale_and_rotation_alone(double scale, TemplateMode mode) => StaThread.Run(() =>
    {
        var layer = LayerOf(1, 200, 100) with { Scale = scale, Rotation = 33, X = 5, Y = 6 };
        var session = new EditorSession(new Document { Mode = mode, Layers = SessionWith(layer).Document.Layers });
        session.Select(1);
        var props = new LayerPropertiesViewModel(session);
        var view = new LayerPropertiesPanel(() => true) { DataContext = props };
        Layout(view);

        Invoke(Named<Button>(view, "CenterFrontButton"));

        var front = Template.Slot(mode, BodyPart.Body, Face.Front);
        var l = session.Document.Layers.Single();
        Assert.Equal(front.X + (front.W - 200 * scale) / 2, l.X, 9);
        Assert.Equal(front.Y + (front.H - 100 * scale) / 2, l.Y, 9);
        Assert.Equal((scale, 33.0), (l.Scale, l.Rotation));
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.Undo());
        Assert.Equal((5.0, 6.0), (session.Document.Layers.Single().X, session.Document.Layers.Single().Y));
    });

    [Fact] // 200x100 at scale 1 in the Shirt front slot (231, 74, 128, 128): x 231 + (128 - 200) / 2, y 74 + (128 - 100) / 2
    public void Center_on_front_torso_gives_the_hand_computed_position() => StaThread.Run(() =>
    {
        var (session, _, view) = Make(null, LayerOf(1, 200, 100));
        Invoke(Named<Button>(view, "CenterFrontButton"));
        Assert.Equal((195.0, 88.0), (session.Document.Layers.Single().X, session.Document.Layers.Single().Y));
    });

    [Fact] // a layer already centered records no step (undo-empty-steps), and the button is off with nothing selected
    public void Centering_twice_records_one_step_and_the_button_follows_the_selection() => StaThread.Run(() =>
    {
        var (session, props, view) = Make(null, LayerOf(1, 200, 100));
        var button = Named<Button>(view, "CenterFrontButton");
        Invoke(button);
        var doc = session.Document;
        Invoke(button);
        Assert.Equal(1, session.UndoCount);
        Assert.Same(doc, session.Document);

        session.Select(null);
        Assert.False(props.CenterFrontCommand.CanExecute(null));
        props.CenterFrontCommand.Execute(null);   // a stray invocation with nothing selected still does nothing
        Assert.Equal(1, session.UndoCount);
    });

    /// <summary>A click as UI Automation sends it; the peer queues the click on the dispatcher, so the queue runs before this returns.</summary>
    static void Invoke(Button b)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
    }

    [Fact] // the Expander exposes expand/collapse to assistive tech and opens with ▼ (editor.js:862-872)
    public void The_adjustments_expander_opens_from_the_view_model_and_for_assistive_tech() => StaThread.Run(() =>
    {
        var (_, props, view) = Make();
        var adjustments = Named<Expander>(view, "Adjustments");
        var expand = (IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(adjustments).GetPattern(PatternInterface.ExpandCollapse);
        expand.Expand();
        Layout(view);
        Assert.True(props.AdjustmentsExpanded);
        Assert.Equal("\u25BC", Descendants<TextBlock>(adjustments).First(t => t.Name == "Arrow").Text);
        Assert.True(Shown(Named<Slider>(view, "BrightnessSlider")));
    });

    [Fact] // one slider press is one step however many values it passes through (spec §5)
    public void A_slider_press_is_one_step() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var scale = Named<Slider>(view, "ScaleSlider");
        scale.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        foreach (var v in new[] { 120.0, 150, 180, 140 }) scale.Value = v;
        Assert.Equal(0, session.UndoCount);
        scale.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        Assert.Equal((1.4, 1), (session.SelectedLayer!.Scale, session.UndoCount));

        scale.Value = 150;                                          // a keyboard step outside a press: its own step
        Assert.Equal(2, session.UndoCount);
    });

    [Fact] // props-name-xy: Enter commits (4.1's change event); other keys do not
    public void Enter_commits_a_typed_field() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var name = Named<TextBox>(view, "NameBox");
        name.Text = "Logo";
        Assert.False(LayerPropertiesPanel.HandleTextKey(name, Key.A));
        Assert.Equal("drip_red", session.SelectedLayer!.Name);
        Assert.True(LayerPropertiesPanel.HandleTextKey(name, Key.Enter));
        Assert.Equal(("Logo", 1), (session.SelectedLayer!.Name, session.UndoCount));
    });

    [Fact] // Review Focus: a press anywhere else commits typing first (a browser blurs the input), so a canvas pick never eats it
    public void A_press_elsewhere_commits_typing_before_it_acts_and_a_press_inside_does_not() => StaThread.Run(() =>
    {
        var (session, _, view) = Make(null, LayerOf(1) with { Name = "Back" }, LayerOf(2) with { Name = "drip_red", X = 5 });
        var name = Named<TextBox>(view, "NameBox");
        var x = Named<TextBox>(view, "XBox");
        name.Text = "Logo";
        x.Text = "40";

        view.CommitTextEdits(name);                                 // a press inside the name box commits only X
        Assert.Equal(("drip_red", 40.0, 1), (session.SelectedLayer!.Name, session.SelectedLayer!.X, session.UndoCount));

        view.CommitTextEdits(new Border());                         // a press on the canvas: the name lands on layer 2 ...
        session.Select(1);                                          // ... before the canvas pick changes the selection
        Layout(view);
        Assert.Equal("Logo", session.Document.Layers.Single(l => l.Id == 2).Name);
        Assert.Equal(("Back", 2), (name.Text, session.UndoCount));

        view.CommitTextEdits(null);                                 // nothing typed: nothing recorded
        Assert.Equal(2, session.UndoCount);
    });

    [Fact] // Review Focus: refused text in X puts the stored value back into the field, and records nothing
    public void Refused_position_text_reverts_the_field() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var x = Named<TextBox>(view, "XBox");
        x.Text = "abc";
        LayerPropertiesPanel.HandleTextKey(x, Key.Enter);
        Assert.Equal(("13", 0), (x.Text, session.UndoCount));
        x.Text = "40.0";
        LayerPropertiesPanel.HandleTextKey(x, Key.Enter);
        Assert.Equal(("40", 40.0, 1), (x.Text, session.SelectedLayer!.X, session.UndoCount));
    });

    [Fact] // props-live-refresh, props-panel-rebuild-resets: an edit elsewhere updates the same controls; nothing is rebuilt or collapsed
    public void Edits_update_the_same_controls_and_keep_the_adjustments_open() => StaThread.Run(() =>
    {
        var (session, props, view) = Make();
        props.AdjustmentsExpanded = true;
        Layout(view);
        var x = Named<TextBox>(view, "XBox");
        var rot = Named<Slider>(view, "RotationSlider");

        using (session.BeginGesture())                              // a canvas drag: one Apply per move (Task 2)
        {
            session.Apply(d => LayerOps.MoveTo(d, 1, 77, 30));
            session.Apply(d => LayerOps.SetRotation(d, 1, -44.6));
        }
        props.ResetAdjustmentsCommand.Execute(null);
        Layout(view);

        Assert.Same(x, Named<TextBox>(view, "XBox"));
        Assert.Same(rot, Named<Slider>(view, "RotationSlider"));
        Assert.Equal(("77", -44.6, "-45\u00B0"), (x.Text, rot.Value, Named<Run>(view, "RotationText").Text));
        Assert.True(Named<Expander>(view, "Adjustments").IsExpanded);
    });

    [Fact] // final fix wave 3b: a Document-only change of the shown layer keeps uncommitted typing; Enter then commits it
    public void Wheel_scaling_while_typing_in_X_keeps_the_text() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var canvas = new CanvasInteraction(session, new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var x = Named<TextBox>(view, "XBox");
        x.Text = "40";                                              // typed, not committed
        canvas.Wheel(+1);                                           // the wheel moves X and Y (scaled about the center)
        canvas.EndWheelBurst();
        Layout(view);
        Assert.NotEqual(12.7, session.SelectedLayer!.X);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal("40", x.Text);
        Assert.Equal("X: " + LayerPropertiesViewModel.ToFixed0(session.SelectedLayer!.X) + "   Y: " + LayerPropertiesViewModel.ToFixed0(session.SelectedLayer!.Y),
            Named<TextBlock>(view, "PositionText").Text);           // the label follows the layer
        LayerPropertiesPanel.HandleTextKey(x, Key.Enter);
        Assert.Equal((40.0, 2), (session.SelectedLayer!.X, session.UndoCount));
    });

    [Fact] // final fix wave 3a: a selection change resets dirty fields to the new layer even when its values are equal
    public void A_selection_change_to_an_equal_layer_resets_typing_and_a_later_focus_loss_records_nothing() => StaThread.Run(() =>
    {
        var (session, _, view) = Make(null, LayerOf(1) with { Name = "art", X = 5, Y = 6 }, LayerOf(2) with { Name = "art", X = 5, Y = 6 });
        var name = Named<TextBox>(view, "NameBox");
        var x = Named<TextBox>(view, "XBox");
        var y = Named<TextBox>(view, "YBox");
        name.Text = "Logo";
        x.Text = "40";
        y.Text = "41";
        session.Select(1);                                          // no commit first (the press-elsewhere hook did not run)
        Layout(view);
        Assert.Equal(("art", "5", "6"), (name.Text, x.Text, y.Text));
        foreach (var box in new[] { name, x, y }) box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        view.CommitTextEdits(null);
        Assert.Equal(0, session.UndoCount);
        Assert.All(session.Document.Layers, l => Assert.Equal(("art", 5.0, 6.0), (l.Name, l.X, l.Y)));
    });

    [Fact] // final fix wave 4: a fresh bind to out-of-range values coerces the sliders but never writes back a step
    public void Binding_out_of_range_rotation_and_brightness_records_nothing() => StaThread.Run(() =>
    {
        var (session, props, view) = Make(null, LayerOf(1) with { Rotation = 270, Brightness = 300, Scale = 9, RShift = 150 });
        Layout(view);
        Assert.Equal((180.0, 200.0), (Named<Slider>(view, "RotationSlider").Value, Named<Slider>(view, "BrightnessSlider").Value));
        Assert.Equal(0, session.UndoCount);
        Assert.Equal((270.0, 300.0, 9.0, 150), (session.SelectedLayer!.Rotation, session.SelectedLayer!.Brightness, session.SelectedLayer!.Scale, session.SelectedLayer!.RShift));

        var other = new LayerPropertiesPanel(() => true) { DataContext = new LayerPropertiesViewModel(session) };   // a second bind
        Layout(other);
        view.DataContext = new LayerPropertiesViewModel(session);   // and a DataContext swap
        Layout(view);
        Assert.Equal(0, session.UndoCount);
        Assert.Equal((270.0, 300.0), (session.SelectedLayer!.Rotation, session.SelectedLayer!.Brightness));
    });

    [Fact] // final fix wave 4: the Refresh path (selection change, then a Document change) never writes a coerced value back
    public void Refreshing_to_out_of_range_values_records_nothing() => StaThread.Run(() =>
    {
        var (session, _, view) = Make(null, LayerOf(1) with { Rotation = 270, Brightness = 300, HueRotate = 400, GShift = -150 }, LayerOf(2));
        session.Select(1);
        Layout(view);
        Assert.Equal(0, session.UndoCount);
        Assert.True(session.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Rotation = -300, Opacity = 3 }) }));
        Layout(view);
        Assert.Equal(1, session.UndoCount);
        var l = session.SelectedLayer!;
        Assert.Equal((-300.0, 3.0, 300.0, 400.0, -150), (l.Rotation, l.Opacity, l.Brightness, l.HueRotate, l.GShift));
    });

    [Fact] // the panel sits in the left panel after the layers, as editor.html:288-323 orders them (the asset library first, M4a Task 2)
    public void The_main_window_puts_the_panel_after_the_layers() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        var children = ((StackPanel)window.FindName("LeftPanelContent")).Children.Cast<FrameworkElement>().ToList();
        Assert.Equal(new[] { typeof(AssetLibraryPanel), typeof(DropZone), typeof(LayersPanel), typeof(LayerPropertiesPanel) }, children.Select(c => c.GetType()));
        Assert.Same(main.Assets, children[0].DataContext);
        Assert.Same(main.LayerProperties, children[3].DataContext);
    });
}
