using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas's layer automation (M4a contract "Canvas UI Automation"; spec §4 accessibility; ledger a11y-focus-rings,
/// undo-empty-steps). 4.1's canvas (editor.html:330) exposed no layers. Every test drives the peers through their
/// provider interfaces, as a UIA client does, with no input of any kind.
/// </summary>
[Collection("MainWindow")] // shows windows on the shared STA thread, like ShellTests and ShellLookTests (critic M7)
public class CanvasLayerAutomationTests
{
    /// <summary>The canvas view over a session (Select tool), its host and the host's peer.</summary>
    static (EditorSession Session, CanvasViewModel Vm, TemplateCanvasHost Host, TemplateCanvasHostAutomationPeer Peer) Canvas(params Layer[] layers) =>
        Canvas(new ToolsViewModel(), layers);

    static (EditorSession Session, CanvasViewModel Vm, TemplateCanvasHost Host, TemplateCanvasHostAutomationPeer Peer) Canvas(ToolsViewModel tools, params Layer[] layers)
    {
        var session = SessionWith(layers);
        var vm = new CanvasViewModel(session, tools, new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        var host = (TemplateCanvasHost)view.FindName("Host");
        var peer = (TemplateCanvasHostAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(host);
        return (session, vm, host, peer);
    }

    static LayerAutomationPeer Child(TemplateCanvasHostAutomationPeer peer, int id) =>
        Assert.Single(peer.GetChildren().OfType<LayerAutomationPeer>(), p => p.LayerId == id);

    static ITransformProvider Transform(LayerAutomationPeer p) => (ITransformProvider)p.GetPattern(PatternInterface.Transform)!;

    static ISelectionItemProvider Item(LayerAutomationPeer p) => (ISelectionItemProvider)p.GetPattern(PatternInterface.SelectionItem)!;

    static Layer L(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    [Fact] // the visible layers, top first, as named list items; hidden layers are not elements; the canvas stays an Image
    public void The_children_are_the_visible_layers_top_first() => StaThread.Run(() =>
    {
        var (_, _, _, peer) = Canvas(LayerOf(1) with { Name = "bottom" }, LayerOf(2) with { Name = "hidden", Visible = false }, LayerOf(3) with { Name = "top" });
        var children = peer.GetChildren().Cast<LayerAutomationPeer>().ToList();
        Assert.Equal(new[] { 3, 1 }, children.Select(c => c.LayerId));
        Assert.Equal(new[] { "top", "bottom" }, children.Select(c => c.GetName()));
        Assert.Equal(new[] { "Layer_3", "Layer_1" }, children.Select(c => c.GetAutomationId()));
        Assert.All(children, c =>
        {
            Assert.Equal(AutomationControlType.ListItem, c.GetAutomationControlType());
            Assert.Equal("CanvasLayer", c.GetClassName());
            Assert.False(c.IsKeyboardFocusable());
            Assert.Same(peer, c.GetParent());
        });
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
    });

    [Fact] // a client keeps the same element across an edit; a removed layer's element goes
    public void A_layers_element_keeps_its_identity_across_edits() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1), LayerOf(2));
        var before = Child(peer, 1);
        session.Apply(d => LayerOps.MoveTo(d, 1, 40, 50));
        Assert.Same(before, Child(peer, 1));
        session.Apply(d => d with { Layers = d.Layers.RemoveAll(l => l.Id == 2) });
        Assert.Equal(new[] { 1 }, peer.GetChildren().Cast<LayerAutomationPeer>().Select(c => c.LayerId));
    });

    [Fact] // SelectionItem: Select picks the layer and the Select tool (as a list pick); one layer at a time; Remove deselects
    public void Selecting_through_automation_picks_the_layer_and_the_select_tool() => StaThread.Run(() =>
    {
        var tools = new ToolsViewModel();
        var (session, _, _, peer) = Canvas(tools, LayerOf(1), LayerOf(2));
        tools.Set(Tool.Pen);
        var one = Item(Child(peer, 1));
        var two = Item(Child(peer, 2));
        var selection = (ISelectionProvider)peer.GetPattern(PatternInterface.Selection);
        Assert.Empty(selection.GetSelection());
        Assert.Equal((false, false), (selection.CanSelectMultiple, selection.IsSelectionRequired));

        one.Select();
        Assert.Equal(1, session.SelectedLayerId);
        Assert.Equal(Tool.Select, tools.Current);
        Assert.True(one.IsSelected);
        Assert.False(two.IsSelected);
        Assert.Single(selection.GetSelection());
        Assert.Equal(0, session.UndoCount);   // selection is never an undo step

        Assert.Throws<InvalidOperationException>(() => two.AddToSelection());
        one.AddToSelection();                 // already selected: allowed, nothing changes
        Assert.Equal(1, session.SelectedLayerId);
        two.RemoveFromSelection();            // not the selected one: nothing
        Assert.Equal(1, session.SelectedLayerId);
        one.RemoveFromSelection();
        Assert.Null(session.SelectedLayerId);
        Assert.Empty(selection.GetSelection());
    });

    [Fact] // only the selected visible layer transforms; any other call is refused and records nothing
    public void Only_the_selected_layer_can_be_transformed() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1), LayerOf(2));
        var t1 = Transform(Child(peer, 1));
        Assert.Equal((false, false, false), (t1.CanMove, t1.CanResize, t1.CanRotate));
        Assert.Throws<ElementNotEnabledException>(() => t1.Move(5, 5));
        Assert.Throws<ElementNotEnabledException>(() => t1.Resize(5, 5));
        Assert.Throws<ElementNotEnabledException>(() => t1.Rotate(5));
        session.Select(1);
        Assert.Equal((true, true, true), (t1.CanMove, t1.CanResize, t1.CanRotate));
        Assert.False(Transform(Child(peer, 2)).CanMove);
        Assert.Equal(0, session.UndoCount);
    });

    [Fact] // Move sets the top-left in template pixels as one step; the same position again is no step; junk is refused
    public void Move_is_template_pixels_and_one_undo_step() => StaThread.Run(() =>
    {
        var (session, _, host, peer) = Canvas(LayerOf(1));
        host.Zoom = 2;                         // template units, so zoom never enters
        session.Select(1);
        var t = Transform(Child(peer, 1));
        t.Move(10.5, -20);
        Assert.Equal((10.5, -20.0), (L(session, 1).X, L(session, 1).Y));
        Assert.Equal(1, session.UndoCount);
        t.Move(10.5, -20);
        Assert.Equal(1, session.UndoCount);
        Assert.Throws<ArgumentException>(() => t.Move(double.NaN, 0));
        Assert.Throws<ArgumentException>(() => t.Move(0, double.PositiveInfinity));
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.Undo());
        Assert.Equal((0.0, 0.0), (L(session, 1).X, L(session, 1).Y));
    });

    [Fact] // Resize fits the displayed size uniformly about the center (editor.js:1181-1190), clamped to 1-500%, one step
    public void Resize_fits_the_size_about_the_center_under_the_one_clamp() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1, 200, 100) with { X = 100, Y = 100 });   // center (200, 150)
        session.Select(1);
        var t = Transform(Child(peer, 1));
        t.Resize(100, 100);                    // min(100/200, 100/100) = 0.5
        var l = L(session, 1);
        Assert.Equal(0.5, l.Scale);
        Assert.Equal((150.0, 125.0), (l.X, l.Y));
        Assert.Equal(1, session.UndoCount);
        t.Resize(100, 100);
        Assert.Equal(1, session.UndoCount);
        t.Resize(100000, 100000);
        Assert.Equal(SelectionGeometry.MaxScale, L(session, 1).Scale);
        Assert.Equal(2, session.UndoCount);
        Assert.Throws<ArgumentException>(() => t.Resize(0, 10));
        Assert.Throws<ArgumentException>(() => t.Resize(10, double.NaN));
        Assert.Equal(2, session.UndoCount);
    });

    [Fact] // Rotate is relative and normalized to -180..180 (select-rotate-drag); a full turn changes nothing and records nothing
    public void Rotate_is_relative_normalized_and_one_step() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1) with { Rotation = 170 });
        session.Select(1);
        var t = Transform(Child(peer, 1));
        t.Rotate(20);
        Assert.Equal(-170, L(session, 1).Rotation, 9);
        Assert.Equal(1, session.UndoCount);
        t.Rotate(360);
        Assert.Equal(1, session.UndoCount);
        Assert.Throws<ArgumentException>(() => t.Rotate(double.NaN));
    });

    [Fact] // a pattern change is its own step, like any other edit during a wheel burst (M3b R7): the burst records first
    public void A_pattern_change_during_a_wheel_burst_is_its_own_step() => StaThread.Run(() =>
    {
        var (session, vm, _, peer) = Canvas(LayerOf(1));
        session.Select(1);
        vm.Interaction.Wheel(1);
        vm.Interaction.Wheel(1);
        var wheeled = L(session, 1);           // x1.05 twice about the center, adopted live
        Transform(Child(peer, 1)).Move(30, 40);
        vm.Interaction.EndWheelBurst();
        Assert.Equal(2, session.UndoCount);
        Assert.True(session.Undo());           // the move only
        Assert.Same(wheeled, L(session, 1));
        Assert.True(session.Undo());           // the burst
        Assert.Equal((1.0, 0.0, 0.0), (L(session, 1).Scale, L(session, 1).X, L(session, 1).Y));
    });

    [Fact] // a hidden selected layer is no element and cannot be moved
    public void A_hidden_layer_is_neither_an_element_nor_movable() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1), LayerOf(2));
        var element = Child(peer, 1);
        session.Select(1);
        session.Apply(d => LayerOps.ToggleVisibility(d, 1));
        Assert.Equal(new[] { 2 }, peer.GetChildren().Cast<LayerAutomationPeer>().Select(c => c.LayerId));
        Assert.False(Transform(element).CanMove);
        Assert.Throws<ElementNotEnabledException>(() => Transform(element).Move(1, 1));
        Assert.Empty(((ISelectionProvider)peer.GetPattern(PatternInterface.Selection)).GetSelection());
    });

    [Fact] // BoundingRectangle is the screen box of the rotated corners at the current zoom; empty while not shown
    public void The_bounds_are_the_rotated_box_on_screen() => StaThread.Run(() =>
    {
        var (session, vm, host, peer) = Canvas(LayerOf(1, 200, 100) with { X = 10, Y = 20 });
        var element = Child(peer, 1);
        Assert.Equal(Rect.Empty, element.GetBoundingRectangle());
        Assert.True(element.IsOffscreen());
        var window = new Window
        {
            Content = new CanvasView { DataContext = vm },
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000,
            Width = 900, Height = 800, ShowActivated = false, ShowInTaskbar = false,
        };
        window.Show();
        try
        {
            StaThread.Pump();
            var shown = (TemplateCanvasHost)((CanvasView)window.Content).FindName("Host");
            var shownPeer = (TemplateCanvasHostAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(shown);
            var layer = Child(shownPeer, 1);
            var origin = shown.PointToScreen(new Point(0, 0));
            double scale = shown.DeviceScale;
            AssertRect(new Rect(origin.X + 10 * scale, origin.Y + 20 * scale, 200 * scale, 100 * scale), layer.GetBoundingRectangle());
            Assert.False(layer.IsOffscreen());
            vm.Zoom = 2;
            StaThread.Pump();
            origin = shown.PointToScreen(new Point(0, 0));
            AssertRect(new Rect(origin.X + 20 * scale, origin.Y + 40 * scale, 400 * scale, 200 * scale), layer.GetBoundingRectangle());
            vm.Zoom = 1;
            session.Apply(d => LayerOps.SetRotation(d, 1, 90));   // about the center (110, 70): 100 wide, 200 tall
            StaThread.Pump();
            origin = shown.PointToScreen(new Point(0, 0));
            AssertRect(new Rect(origin.X + 60 * scale, origin.Y - 30 * scale, 100 * scale, 200 * scale), layer.GetBoundingRectangle());
        }
        finally
        {
            window.Close();
        }
    });

    [Fact] // end to end through the real UI Automation client (another thread, as Narrator or a test driver): find, select, move
    public void Through_the_UIA_client_a_layer_is_found_selected_and_moved()
    {
        var (window, session) = StaThread.Run(() =>
        {
            var (s, vm, _, _) = Canvas(LayerOf(1) with { Name = "drip" });
            var w = new Window
            {
                Content = new CanvasView { DataContext = vm },
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000,
                Width = 900, Height = 800, ShowActivated = false, ShowInTaskbar = false,
            };
            w.Show();
            return (w, s);
        });
        try
        {
            var hwnd = StaThread.Run(() => new System.Windows.Interop.WindowInteropHelper(window).Handle);
            var root = AutomationElement.FromHandle(hwnd);
            var canvas = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Template canvas"));
            Assert.NotNull(canvas);
            var layer = canvas.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "Layer_1"));
            Assert.NotNull(layer);
            Assert.Equal("drip", layer.Current.Name);
            Assert.Equal(ControlType.ListItem, layer.Current.ControlType);
            ((SelectionItemPattern)layer.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            var transform = (TransformPattern)layer.GetCurrentPattern(TransformPattern.Pattern);
            Assert.True(transform.Current.CanMove);
            transform.Move(25, 35);
            StaThread.Run(() =>
            {
                Assert.Equal(1, session.SelectedLayerId);
                Assert.Equal((25.0, 35.0), (L(session, 1).X, L(session, 1).Y));
                Assert.Equal(1, session.UndoCount);
            });
        }
        finally
        {
            StaThread.Run(window.Close);
        }
    }

    static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }
}
