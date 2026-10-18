using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Testing;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>InputBridgeTests run with no other test class in parallel (critic M7).</summary>
[CollectionDefinition("Bridge", DisableParallelization = true)]
public sealed class BridgeCollection
{
}

/// <summary>
/// The test input bridge's dispatcher against a real MainWindow (TestShell's view model), shown off-screen at
/// (-12000, -12000) and never activated (M4a interfaces §4, R7, R8). Commands go through
/// <see cref="InputBridgeDispatcher.ExecuteAsync"/> from the test thread, as the pipe sends them, and run on the STA
/// thread's dispatcher. Nothing here sends input outside this process: the bridge raises WPF input in-process only.
/// While a rig lives, PointerState's override and the hardware neutralizer are installed on the shared STA thread, and
/// the modal fact runs a nested loop there, so this class runs alone (critic M7).
/// </summary>
[Collection("Bridge")]
public class InputBridgeTests
{
    sealed class Rig(MainWindow window, InputBridgeDispatcher bridge) : IDisposable
    {
        public MainWindow Window { get; } = window;
        public MainViewModel Main => Window.ViewModel;
        public EditorSession Session => Main.Session;
        public InputBridgeDispatcher Bridge { get; } = bridge;

        public BridgeResponse Send(string json) => Bridge.ExecuteAsync(BridgeJson.Parse(json)).GetAwaiter().GetResult();

        public JsonObject Ok(string json)
        {
            var r = Send(json);
            Assert.True(r.Ok, r.Error);
            return r.Result ?? [];
        }

        public void Dispose() => StaThread.Run(() =>
        {
            Bridge.Dispose();
            Session.MarkSaved("bridge-test.r15proj", Session.Document);   // closing never prompts (M4a Task 1)
            Window.Close();
        });
    }

    static Rig Open(params Layer[] layers) => StaThread.Run(() =>
    {
        var window = new MainWindow(TestShell.Main(SessionWith(layers)), new FakeSettingsStore(), [new Rect(-12000, -12000, 1920, 1040)])
        {
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        StaThread.Pump();
        return new Rig(window, new InputBridgeDispatcher(window));
    });

    static T First<T>(DependencyObject root) where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>([root]);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t) return t;
            for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        throw new InvalidOperationException("no " + typeof(T).Name);
    }

    static Layer L(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    sealed class CountingCommand(ICommand inner) : ICommand
    {
        public int Executed { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => inner.CanExecute(parameter);
        public void Execute(object? parameter) => Executed++;
    }

    [Fact] // H1-style move drag (select-move): press on the layer, move, release; one undo step; capture held mid-drag
    public void A_canvas_move_drag_is_one_undo_step()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"canvas":{"x":50,"y":50}}}""");
        var mid = rig.Ok("""{"id":2,"cmd":"inspect","target":"canvas"}""");
        Assert.Equal(("Move", true), (mid["gesture"]!.GetValue<string>(), mid["captured"]!.GetValue<bool>()));
        Assert.Equal(("Move", "Select"), (mid["cursor"]!["kind"]!.GetValue<string>(), mid["tool"]!.GetValue<string>()));   // the smoke's Inspect shape (critic C3)
        rig.Ok("""{"id":3,"cmd":"pointer","action":"move","at":{"canvas":{"x":65,"y":70}}}""");
        rig.Ok("""{"id":4,"cmd":"pointer","action":"move","at":{"canvas":{"x":80,"y":90}}}""");
        rig.Ok("""{"id":5,"cmd":"pointer","action":"up","at":{"canvas":{"x":80,"y":90}}}""");
        var end = rig.Ok("""{"id":6,"cmd":"inspect","target":"canvas"}""");
        Assert.Equal(("None", false, 1, 1), (end["gesture"]!.GetValue<string>(), end["captured"]!.GetValue<bool>(), end["undo"]!.GetValue<int>(), end["selected"]!.GetValue<int>()));
        StaThread.Run(() =>
        {
            // The bridge injects whole device pixels, so at 125% a canvas y of 90 lands on a pixel 0.4 DIP off. The layer moves
            // by the difference of the two points as they landed, which is exactly (30, 40) only when the scale makes them whole.
            var host = First<TemplateCanvasHost>(rig.Window);
            var down = Landed(host, new Point(50, 50));
            var up = Landed(host, new Point(80, 90));
            Assert.Equal(up.X - down.X, L(rig.Session, 1).X, 6);
            Assert.Equal(up.Y - down.Y, L(rig.Session, 1).Y, 6);
        });
    }

    [Fact] // draw-release-outside-canvas / select-move: a release outside the window reaches the captured canvas
    public void A_release_outside_the_window_ends_the_drag_where_it_was_released()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"canvas":{"x":50,"y":50}}}""");
        rig.Ok("""{"id":2,"cmd":"pointer","action":"move","at":{"window":{"x":-40,"y":-30}}}""");
        rig.Ok("""{"id":3,"cmd":"pointer","action":"up","at":{"window":{"x":-40,"y":-30}}}""");
        StaThread.Run(() =>
        {
            var host = First<TemplateCanvasHost>(rig.Window);
            // Both ends are whole device pixels as injected: the press at canvas (50, 50), the release at window (-40, -30).
            var released = CanvasCoordinates.ToTemplate(rig.Window.TranslatePoint(DeviceRounding.Injected(rig.Window, new Point(-40, -30)), host), host.Zoom);
            var pressed = Landed(host, new Point(50, 50));
            Assert.Equal(released.X - pressed.X, L(rig.Session, 1).X, 6);
            Assert.Equal(released.Y - pressed.Y, L(rig.Session, 1).Y, 6);
            Assert.Equal(CanvasGesture.None, host.Interaction!.Gesture);
            Assert.False(host.IsMouseCaptured);
            Assert.Equal(1, rig.Session.UndoCount);
        });
    }

    [Fact] // Ctrl+S reaches the window's Ctrl+S binding exactly once (the chord check R7 does instead of KeyGesture.Matches)
    public void Ctrl_S_runs_the_save_binding_exactly_once()
    {
        using var rig = Open();
        var counter = StaThread.Run(() =>
        {
            var binding = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.S && b.Modifiers == ModifierKeys.Control);
            var counting = new CountingCommand(binding.Command);
            binding.Command = counting;
            return counting;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"S","modifiers":["Ctrl"]}""");
        Assert.Equal(nameof(CountingCommand), result["binding"]!.GetValue<string>());
        Assert.Equal(1, counter.Executed);
        var shift = rig.Ok("""{"id":2,"cmd":"key","key":"S","modifiers":["Ctrl","Shift"]}""");   // a different chord: Save As's binding
        Assert.Equal(1, counter.Executed);
        Assert.NotNull(shift["binding"]);
    }

    [Theory] // canvas-mouse-button-agnostic (fix): only the left button starts a canvas gesture; right and middle do nothing
    [InlineData("right")]
    [InlineData("middle")]
    public void A_right_or_middle_press_starts_no_canvas_gesture(string button)
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("{\"id\":1,\"cmd\":\"pointer\",\"action\":\"down\",\"button\":\"" + button + "\",\"at\":{\"canvas\":{\"x\":50,\"y\":50}}}");
        rig.Ok("{\"id\":2,\"cmd\":\"pointer\",\"action\":\"move\",\"at\":{\"canvas\":{\"x\":90,\"y\":90}}}");
        var state = rig.Ok("{\"id\":3,\"cmd\":\"inspect\",\"target\":\"canvas\"}");
        rig.Ok("{\"id\":4,\"cmd\":\"pointer\",\"action\":\"up\",\"button\":\"" + button + "\",\"at\":{\"canvas\":{\"x\":90,\"y\":90}}}");
        Assert.Equal(("None", false), (state["gesture"]!.GetValue<string>(), state["captured"]!.GetValue<bool>()));
        StaThread.Run(() =>
        {
            Assert.Equal((0.0, 0.0), (L(rig.Session, 1).X, L(rig.Session, 1).Y));
            Assert.Null(rig.Session.SelectedLayerId);
            Assert.Equal(0, rig.Session.UndoCount);
        });
    }

    [Fact] // spec §4: text fields trap shortcuts; the bridge calls CanExecute, so TextFieldShortcutCommand's guard holds
    public void Ctrl_S_with_a_text_field_focused_does_nothing()
    {
        using var rig = Open(LayerOf(1));
        var counter = StaThread.Run(() =>
        {
            rig.Session.Select(1);
            StaThread.Pump();
            var field = (TextBox)First<LayerPropertiesPanel>(rig.Window).FindName("XBox");
            rig.Window.FocusedElement = () => field;
            var binding = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.S && b.Modifiers == ModifierKeys.Control);
            var counting = new CountingCommand(binding.Command);
            binding.Command = counting;
            return counting;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"S","modifiers":["Ctrl"]}""");
        Assert.Equal("XBox", result["target"]!.GetValue<string>());
        Assert.Null(result["binding"]);
        Assert.Equal(0, counter.Executed);
    }

    [Fact] // layers-reorder-drag-2: Alt+Up reaches the focused row as Key.System with Alt in e.KeyboardDevice.Modifiers
    public void Alt_up_on_a_focused_layer_row_moves_it_one_step()
    {
        using var rig = Open(LayerOf(1), LayerOf(2), LayerOf(3));
        var seen = StaThread.Run(() =>
        {
            var panel = First<LayersPanel>(rig.Window);
            var row = rig.Main.Layers.Rows.Single(r => r.Id == 1);
            var item = (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(row);
            rig.Window.FocusedElement = () => item;
            var log = new List<(Key, Key, ModifierKeys)>();
            rig.Window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => log.Add((e.Key, e.SystemKey, e.KeyboardDevice.Modifiers))), true);
            return log;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"Up","modifiers":["Alt"]}""");
        Assert.True(result["previewHandled"]!.GetValue<bool>());
        StaThread.Run(() =>
        {
            Assert.Equal(new[] { (Key.System, Key.Up, ModifierKeys.Alt) }, seen);   // as real input delivers an Alt chord
            Assert.Equal(new[] { 2, 1, 3 }, rig.Session.Document.Layers.Select(l => l.Id));
            Assert.Equal(1, rig.Session.SelectedLayerId);
            Assert.Equal(1, rig.Session.UndoCount);
        });
    }

    [Fact] // a bare key: PreviewKeyDown, then KeyDown exactly once (WPF does not promote it for the bridge's device), then the ups
    public void A_bare_key_reaches_key_down_once_with_no_modifiers()
    {
        using var rig = Open();
        var seen = StaThread.Run(() =>
        {
            var log = new List<string>();
            rig.Window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => log.Add($"PreviewKeyDown {e.Key} {e.KeyboardDevice.Modifiers}")), true);
            rig.Window.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) => log.Add($"KeyDown {e.Key} {e.KeyboardDevice.Modifiers}")), true);
            rig.Window.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler((_, e) => log.Add($"PreviewKeyUp {e.Key}")), true);
            rig.Window.AddHandler(Keyboard.KeyUpEvent, new KeyEventHandler((_, e) => log.Add($"KeyUp {e.Key}")), true);
            return log;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"F7"}""");
        Assert.False(result["previewHandled"]!.GetValue<bool>());
        StaThread.Run(() => Assert.Equal(new[] { "PreviewKeyDown F7 None", "KeyDown F7 None", "PreviewKeyUp F7", "KeyUp F7" }, seen));
    }

    [Fact] // R7 step 1: with a physical modifier held, a key command is refused, so a real Ctrl never mixes into a bridge chord
    public void A_key_is_refused_while_a_physical_modifier_is_held()
    {
        using var rig = Open();
        var seen = StaThread.Run(() =>
        {
            rig.Bridge.PhysicalModifiers = () => ModifierKeys.Control;
            var log = new List<Key>();
            rig.Window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => log.Add(e.Key)), true);
            return log;
        });
        var r = rig.Send("""{"id":7,"cmd":"key","key":"F7"}""");
        Assert.False(r.Ok);
        Assert.Equal(7, r.Id);
        Assert.StartsWith("physical modifier keys are held (Control)", r.Error);
        StaThread.Run(() => Assert.Empty(seen));
    }

    [Fact] // wheel-scale-undo-burst: wait-idle waits out an open wheel burst and the frame it leaves; then the burst is one step
    public void Wait_idle_returns_once_the_wheel_burst_and_its_frame_are_done()
    {
        using var rig = Open(LayerOf(1));
        StaThread.Run(() =>
        {
            rig.Session.Select(1);
            var host = First<TemplateCanvasHost>(rig.Window);
            host.HandleWheel(120);   // inside one dispatcher turn, so the 400 ms quiet period cannot elapse before the checks
            Assert.Equal("a wheel burst is open", rig.Bridge.BusyReason());
            host.FlushWheelBurst();
            Assert.Equal("a frame is waiting to render", rig.Bridge.BusyReason());
            host.HandleWheel(120);
        });
        rig.Ok("""{"id":1,"cmd":"wheel","delta":120,"at":{"canvas":{"x":50,"y":50}}}""");
        var idle = rig.Ok("""{"id":2,"cmd":"wait-idle","timeoutMs":3000}""");
        Assert.InRange(idle["ms"]!.GetValue<long>(), 0, 3000);
        StaThread.Run(() =>
        {
            Assert.Null(rig.Bridge.BusyReason());
            Assert.Equal(2, rig.Session.UndoCount);   // the seam's flushed burst, then the second burst (seam + bridge notch)
            Assert.Equal(Math.Pow(1.05, 3), L(rig.Session, 1).Scale, 9);
        });
        var r = rig.Send("""{"id":3,"cmd":"wait-idle","timeoutMs":60001}""");
        Assert.Equal((false, "\"timeoutMs\" must be 0..60000"), (r.Ok, r.Error));
    }

    [Fact] // R8: while the bridge lives, an unmarked (hardware) report cannot move or press; after Dispose it can again
    public void Hardware_mouse_reports_are_pinned_to_the_bridge_point_until_it_is_disposed()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"move","at":{"canvas":{"x":100,"y":80}}}""");
        StaThread.Run(() =>
        {
            var host = First<TemplateCanvasHost>(rig.Window);
            var source = PresentationSource.FromVisual(host)!;
            var elsewhere = source.CompositionTarget.TransformToDevice.Transform(host.TranslatePoint(new Point(10, 10), rig.Window));
            RawMouse.Inject(source, "AbsoluteMove", elsewhere, 0, extraInformation: 0);
            RawMouse.Inject(source, "Button1Press", elsewhere, 0, extraInformation: 0);
            AssertTemplate(Landed(host, new Point(100, 80)), host);   // still the bridge's point, to the whole device pixel it landed on
            Assert.Equal(CanvasGesture.None, host.Interaction!.Gesture);
            Assert.Null(rig.Session.SelectedLayerId);

            rig.Bridge.Dispose();
            RawMouse.Inject(source, "AbsoluteMove", elsewhere, 0, extraInformation: 0);
            AssertTemplate(Landed(host, new Point(10, 10)), host);   // the hardware report now lands, rounded to a whole device pixel
        });
    }

    [Fact] // R8, smoke D1 (H4/H5): a bare unmarked move, like the one Windows sends at SetCapture, is pinned to the bridge point
    public void A_bare_hardware_move_during_a_drag_never_reaches_the_canvas()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"canvas":{"x":50,"y":50}}}""");
        StaThread.Run(() =>
        {
            var host = First<TemplateCanvasHost>(rig.Window);
            var source = PresentationSource.FromVisual(host)!;
            var elsewhere = source.CompositionTarget.TransformToDevice.Transform(host.TranslatePoint(new Point(400, 300), rig.Window));
            RawMouse.Inject(source, "AbsoluteMove", elsewhere, 0, extraInformation: 0);
            Assert.Equal(new Point(50, 50), RoundedTemplate(host));
            Assert.Equal((CanvasGesture.Move, true), (host.Interaction!.Gesture, host.IsMouseCaptured));
            Assert.Equal((0.0, 0.0), (L(rig.Session, 1).X, L(rig.Session, 1).Y));
            RawMouse.Inject(source, "Activate, AbsoluteMove", elsewhere, 0, extraInformation: 0);
            Assert.Equal(new Point(50, 50), RoundedTemplate(host));
            Assert.Equal((0.0, 0.0), (L(rig.Session, 1).X, L(rig.Session, 1).Y));
        });
        rig.Ok("""{"id":2,"cmd":"pointer","action":"up","at":{"canvas":{"x":50,"y":50}}}""");
        StaThread.Run(() => Assert.Equal((0.0, 0.0), (L(rig.Session, 1).X, L(rig.Session, 1).Y)));
    }

    /// <summary>Where a template point lands once injected as whole device pixels (DeviceRounding.Injected), back in template space.</summary>
    static Point Landed(TemplateCanvasHost host, Point template) =>
        CanvasCoordinates.ToTemplate(DeviceRounding.Injected(host, CanvasCoordinates.ToFrame(template, host.Zoom)), host.Zoom);

    /// <summary>The mouse position in template space, exact: the report landed on a whole device pixel and nothing else rounds it.</summary>
    static void AssertTemplate(Point expected, TemplateCanvasHost host)
    {
        var actual = CanvasCoordinates.ToTemplate(Mouse.GetPosition(host), host.Zoom);
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
    }

    static Point RoundedTemplate(TemplateCanvasHost host)
    {
        var p = CanvasCoordinates.ToTemplate(Mouse.GetPosition(host), host.Zoom);
        return new Point(Math.Round(p.X), Math.Round(p.Y));
    }

    [Fact] // R8: app code reads the left button only through PointerState, which follows the bridge's presses; a dispose clears it
    public void The_left_button_seam_follows_the_bridge()
    {
        using var rig = Open();
        Assert.False(StaThread.Run(PointerState.LeftButtonDown));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"window":{"x":5,"y":5}}}""");
        Assert.True(StaThread.Run(PointerState.LeftButtonDown));
        rig.Ok("""{"id":2,"cmd":"pointer","action":"up","at":{"window":{"x":5,"y":5}}}""");
        Assert.False(StaThread.Run(PointerState.LeftButtonDown));
        StaThread.Run(rig.Bridge.Dispose);
        Assert.Null(StaThread.Run(() => PointerState.LeftButtonOverride));
    }

    [Fact] // H7 (layers-reorder-drag): a row drag through the bridge, which LayersPanel sees through PointerState
    public void A_layers_row_drag_reorders_in_one_step()
    {
        using var rig = Open(LayerOf(1), LayerOf(2), LayerOf(3));
        var (from, to) = StaThread.Run(() =>
        {
            var panel = First<LayersPanel>(rig.Window);
            Point Centre(int id)
            {
                var item = (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(rig.Main.Layers.Rows.Single(r => r.Id == id));
                return item.TranslatePoint(new Point(item.ActualWidth / 3, item.ActualHeight * 0.25), rig.Window);   // upper half, off the buttons
            }
            return (Centre(1), Centre(3));
        });
        string At(Point p) => "{\"window\":{\"x\":" + BridgeJson.Invariant(p.X) + ",\"y\":" + BridgeJson.Invariant(p.Y) + "}}";
        rig.Ok($$"""{"id":1,"cmd":"pointer","action":"down","at":{{At(from)}}}""");
        rig.Ok($$"""{"id":2,"cmd":"pointer","action":"move","at":{{At(new Point(from.X, from.Y - 12))}}}""");
        rig.Ok($$"""{"id":3,"cmd":"pointer","action":"move","at":{{At(to)}}}""");
        rig.Ok($$"""{"id":4,"cmd":"pointer","action":"up","at":{{At(to)}}}""");
        StaThread.Run(() =>
        {
            Assert.Equal(new[] { 2, 3, 1 }, rig.Session.Document.Layers.Select(l => l.Id));   // the bottom row onto the top row's upper half
            Assert.Equal(1, rig.Session.UndoCount);
        });
    }

    [Fact] // asset-drag-to-canvas through the bridge (critic C2): the asset panel reads the left button through PointerState, so a tile drag lands
    public void An_asset_tile_drag_onto_the_canvas_adds_one_layer_in_one_step()
    {
        using var rig = Open();
        StaThread.Run(() =>
        {
            rig.Main.Assets.LoadWordArtCommand.Execute(null);
            StaThread.Pump();
        });
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"element":"AssetTile","x":20,"y":20}}""");
        rig.Ok("""{"id":2,"cmd":"pointer","action":"move","at":{"element":"AssetTile","x":32,"y":32}}""");   // past the system drag distance
        rig.Ok("""{"id":3,"cmd":"pointer","action":"move","at":{"canvas":{"x":300,"y":200}}}""");
        rig.Ok("""{"id":4,"cmd":"pointer","action":"up","at":{"canvas":{"x":300,"y":200}}}""");
        StaThread.Run(() =>
        {
            var l = Assert.Single(rig.Session.Document.Layers);
            Assert.Equal("word_626_anton", l.Name);                          // the first Word art tile (AssetLibraryPanelTests)
            Assert.Equal(1, rig.Session.UndoCount);
            Assert.False(First<AssetLibraryPanel>(rig.Window).IsDragging);
        });
    }

    [Fact] // H7's mid-drag read (critic C3): inspect "layers" reports the running drag, the marked source row and the target's drop line
    public void Inspect_layers_reports_a_row_drag_in_progress()
    {
        using var rig = Open(LayerOf(1), LayerOf(2), LayerOf(3));
        var (from, to) = StaThread.Run(() =>
        {
            var panel = First<LayersPanel>(rig.Window);
            Point UpperQuarter(int id)
            {
                var item = (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(rig.Main.Layers.Rows.Single(r => r.Id == id));
                return item.TranslatePoint(new Point(item.ActualWidth / 3, item.ActualHeight * 0.25), rig.Window);   // upper half, off the buttons
            }
            return (UpperQuarter(1), UpperQuarter(3));
        });
        string At(Point p) => "{\"window\":{\"x\":" + BridgeJson.Invariant(p.X) + ",\"y\":" + BridgeJson.Invariant(p.Y) + "}}";
        rig.Ok($$"""{"id":1,"cmd":"pointer","action":"down","at":{{At(from)}}}""");
        rig.Ok($$"""{"id":2,"cmd":"pointer","action":"move","at":{{At(new Point(from.X, from.Y - 12))}}}""");
        rig.Ok($$"""{"id":3,"cmd":"pointer","action":"move","at":{{At(to)}}}""");
        var mid = rig.Ok("""{"id":4,"cmd":"inspect","target":"layers"}""");
        rig.Ok($$"""{"id":5,"cmd":"pointer","action":"up","at":{{At(to)}}}""");
        var after = rig.Ok("""{"id":6,"cmd":"inspect","target":"layers"}""");

        Assert.True(mid["dragging"]!.GetValue<bool>());
        var rows = mid["rows"]!.AsArray().Select(n => (Id: n!["id"]!.GetValue<int>(), Source: n["dragSource"]!.GetValue<bool>(), Drop: n["drop"]!.GetValue<string>())).ToList();
        Assert.Equal(new[] { 3, 2, 1 }, rows.Select(r => r.Id));                 // top first, as the list shows them
        Assert.Equal((true, "None"), (rows[2].Source, rows[2].Drop));            // the dragged row (layer 1) is marked
        Assert.Equal((false, "Above"), (rows[0].Source, rows[0].Drop));          // the target row (layer 3) shows the line above
        Assert.False(after["dragging"]!.GetValue<bool>());
        Assert.All(after["rows"]!.AsArray(), n => Assert.Equal((false, "None"), (n!["dragSource"]!.GetValue<bool>(), n["drop"]!.GetValue<string>())));
    }

    [Fact] // a chord whose command opens a modal answers {"modal":true} at once; a click on the disabled owner sends HTERROR
    public void An_input_that_opens_a_modal_answers_at_once_and_a_click_on_the_owner_goes_through_set_cursor()
    {
        using var rig = Open();
        var (messages, dialog) = StaThread.Run(() =>
        {
            var log = new List<bool>();
            HwndSource.FromHwnd(new WindowInteropHelper(rig.Window).Handle)!.AddHook((IntPtr _, int msg, IntPtr _, IntPtr lParam, ref bool handled) =>
            {
                if (msg != 0x0020) return IntPtr.Zero;
                bool click = ThemedColorPicker.IsClickOnDisabledOwner(msg, lParam);
                log.Add(click);
                if (!click) return IntPtr.Zero;
                handled = true;            // as ThemedColorPicker.OwnerHook does, so Windows does not beep
                return new IntPtr(1);
            });
            var modal = new Window
            {
                Owner = rig.Window, ShowActivated = false, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -11000, Width = 200, Height = 100,
            };
            rig.Window.InputBindings.Add(new KeyBinding(new RelayCommandStub(() => modal.ShowDialog()), Key.M, ModifierKeys.Control));
            return (log, modal);
        });
        var opened = rig.Ok("""{"id":1,"cmd":"key","key":"M","modifiers":["Ctrl"]}""");
        Assert.True(opened["modal"]!.GetValue<bool>());
        var click = rig.Ok("""{"id":2,"cmd":"pointer","action":"down","at":{"canvas":{"x":10,"y":10}}}""");
        Assert.True(click["disabledOwner"]!.GetValue<bool>());
        Assert.Equal((true, true), (click["ownerClick"]!.GetValue<bool>(), click["handled"]!.GetValue<bool>()));   // what the smoke's H10 reads (critic C3)
        StaThread.Run(() =>
        {
            Assert.Equal(new[] { true }, messages);
            Assert.Equal(CanvasGesture.None, First<TemplateCanvasHost>(rig.Window).Interaction!.Gesture);
            dialog.Close();
        });
    }

    sealed class RelayCommandStub(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }
}
