using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
        Assert.Equal(20, focusable.OfType<TextBox>().Count() + focusable.OfType<Slider>().Count() + focusable.OfType<ButtonBase>().Count());
        Assert.All(focusable, c => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(c)), c.GetType().Name + " " + c.Name));
        Assert.Equal(
            new[] { "Flip horizontally.", "Flip vertically.", "Duplicate layer.", "Move layer up.", "Move layer down." },
            new[] { "FlipHButton", "FlipVButton", "DuplicateButton", "UpButton", "DownButton" }.Select(n => (string)Named<Button>(view, n).ToolTip));
        Assert.Null(view.FindName("CenterFrontButton"));             // props-center-front is M4 (interface R13)
        Assert.DoesNotContain(Descendants<Button>(view), b => b.Content as string == "Center on front torso");
    });

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

    [Fact] // the panel sits in the left panel after the layers, as editor.html:311-323 orders them
    public void The_main_window_puts_the_panel_after_the_layers() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        var children = ((StackPanel)window.FindName("LeftPanelContent")).Children.Cast<FrameworkElement>().ToList();
        Assert.Equal(new[] { typeof(DropZone), typeof(LayersPanel), typeof(LayerPropertiesPanel) }, children.Select(c => c.GetType()));
        Assert.Same(main.LayerProperties, children[2].DataContext);
    });
}
