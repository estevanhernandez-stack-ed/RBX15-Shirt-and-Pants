using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Views;
using SkiaSharp;

namespace RBX15.App.Testing;

/// <summary>
/// Runs bridge commands on the app's UI thread, into the app's own visual tree only (M4a contract "test input
/// bridge"; interfaces R7, R8). Tests drive it directly; <see cref="TestInputBridge"/> puts a pipe in front of it.
/// <list type="bullet">
/// <item><b>Pointer and wheel</b> go through WPF's raw mouse report (<see cref="RawMouse"/>), so WPF hit-tests,
///   promotes, captures and routes them as it does hardware input. Each command sends an Activate report when the
///   mouse device is not active on the target's window, a move-only report, then a button-only or wheel-only report.
///   The left button's state reaches app code through <see cref="PointerState"/>.</item>
/// <item><b>While it lives, hardware cannot act on the app.</b> A PreProcessInput handler cancels every raw mouse
///   report without <see cref="Marker"/> that moves, presses or turns the wheel, and replaces it with a move to the
///   bridge's last point (WPF's own Mouse.Synchronize reads the real cursor; this keeps it from moving the pointer).
///   It is a WPF event handler in this process, not a Windows hook.</item>
/// <item><b>Keys</b> are raised on a <see cref="BridgeKeyboardDevice"/>, so <c>e.KeyboardDevice.Modifiers</c> carries
///   the command's modifiers. A chord's KeyBindings are matched by the bridge itself, because WPF's KeyGesture.Matches
///   reads the physical modifier state (R7).</item>
/// <item><b>A click on a window disabled by a modal</b> does what Windows does: it sends WM_SETCURSOR with HTERROR and
///   the button-down message to that window's own HWND (ThemedColorPicker.OwnerHook commits on it). That is the only
///   message the bridge ever sends, and only to this process's windows.</item>
/// </list>
/// A command whose input opens a modal window answers <c>{"modal":true}</c> as soon as the modal opens; the next
/// command runs inside the modal's loop. An exception thrown by app code is answered as an error starting
/// <see cref="AppExceptionPrefix"/>, never swallowed silently.
/// </summary>
public sealed class InputBridgeDispatcher : IInputBridge, IDisposable
{
    /// <summary>The extraInformation every bridge mouse report carries: "RBX1".</summary>
    public const nint Marker = 0x52425831;

    /// <summary>The error prefix for an exception that app code threw while handling bridge input.</summary>
    public const string AppExceptionPrefix = "APP-EXCEPTION ";

    const int DefaultTimeoutMs = 5000, MaxTimeoutMs = 60000;
    const int WM_SETCURSOR = 0x0020, HTERROR = -2;

    [ThreadStatic] static BridgeKeyboardDevice? t_keyboard;
    static readonly MethodInfo? MarkSystem = typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly MainWindow _main;
    readonly Dispatcher _dispatcher;
    readonly BridgeFileDialogs? _fileDialogs;
    bool _leftDown;
    Point? _lastScreen;
    bool _disposed;

    /// <summary>Installs the hardware neutralizer and the left-button override. UI thread.</summary>
    public InputBridgeDispatcher(MainWindow main) : this(main, null) { }

    internal InputBridgeDispatcher(MainWindow main, BridgeFileDialogs? fileDialogs)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _dispatcher = main.Dispatcher;
        _dispatcher.VerifyAccess();
        _fileDialogs = fileDialogs;
        InputManager.Current.PreProcessInput += OnPreProcessInput;
        PointerState.LeftButtonOverride = () => _leftDown ? MouseButtonState.Pressed : MouseButtonState.Released;
    }

    /// <summary>The physical keyboard's modifiers. A key command refuses while any is held (R7). Tests replace it.</summary>
    internal Func<ModifierKeys> PhysicalModifiers { get; set; } = () => Keyboard.PrimaryDevice.Modifiers;

    /// <summary>Removes the neutralizer and the left-button override. UI thread; a second call does nothing.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _dispatcher.VerifyAccess();
        _disposed = true;
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        PointerState.LeftButtonOverride = null;
    }

    public async Task<BridgeResponse> ExecuteAsync(BridgeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            JsonObject? result = command.Cmd switch
            {
                "ping" => await OnUi(() => new JsonObject { ["pid"] = Environment.ProcessId, ["version"] = AppInfo.Version }),
                "pointer" => await OnUi(() => Pointer(command)),
                "wheel" => await OnUi(() => Wheel(command)),
                "key" => await OnUi(() => KeyCommand(command)),
                "wait-idle" => await WaitIdle(command.TimeoutMs),
                "file-dialog" => await OnUi(() => FileDialog(command)),
                "inspect" => await OnUi(() => Inspect(command)),
                _ => throw new BridgeException($"unknown cmd \"{command.Cmd}\""),
            };
            return new BridgeResponse(command.Id, true, null, result);
        }
        catch (BridgeException e)
        {
            return new BridgeResponse(command.Id, false, e.Message, null);
        }
        catch (Exception e)
        {
            return new BridgeResponse(command.Id, false, AppExceptionPrefix + e.GetType().Name + ": " + e.Message, null);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread. If the input it raises opens a modal window
    /// (ComponentDispatcher.EnterThreadModal), the task completes then with {"modal":true}, so the client is not
    /// blocked until the modal closes; later commands run inside the modal's loop.
    /// </summary>
    Task<JsonObject?> OnUi(Func<JsonObject?> action)
    {
        var done = new TaskCompletionSource<JsonObject?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var op = _dispatcher.InvokeAsync(() =>
        {
            EventHandler modal = (_, _) => done.TrySetResult(new JsonObject { ["modal"] = true });
            ComponentDispatcher.EnterThreadModal += modal;
            try
            {
                if (_disposed) throw new BridgeException("the bridge is closed");
                done.TrySetResult(action());
            }
            catch (Exception e)
            {
                done.TrySetException(e);
            }
            finally
            {
                ComponentDispatcher.EnterThreadModal -= modal;
            }
        });
        op.Aborted += (_, _) => done.TrySetException(new BridgeException("the app's dispatcher shut down"));
        return done.Task;
    }

    // ---- pointer and wheel (R8) ----

    readonly record struct Target(PresentationSource Source, Point Device, Point Screen, Window Window, bool OwnerDisabled);

    JsonObject Pointer(BridgeCommand c)
    {
        string action = c.Action is "down" or "move" or "up" ? c.Action : throw new BridgeException("\"action\" must be down, move or up");
        int button = (c.Button ?? "left") switch
        {
            "left" => 1,
            "right" => 2,
            "middle" => 3,
            var other => throw new BridgeException($"unknown button \"{other}\" (left, right, middle)"),
        };
        var target = Resolve(c.At);
        if (target.OwnerDisabled)
        {
            // Windows sends a disabled window no mouse input; a press reaches it only as WM_SETCURSOR + HTERROR.
            // ownerClick and handled are what the smoke's H10 reads (critic C3; Task 5 Addition 3).
            bool? handled = action == "down" ? ClickDisabledWindow(target.Window, button) : null;
            var described = Describe(target);
            if (handled is { } h)
            {
                described["ownerClick"] = true;
                described["handled"] = h;
            }
            return described;
        }
        EnsureActive(target);
        MoveTo(target);
        if (action == "down")
        {
            if (button == 1) _leftDown = true;
            RawMouse.Inject(target.Source, $"Button{button}Press", target.Device, 0);
        }
        else if (action == "up")
        {
            if (button == 1) _leftDown = false;
            RawMouse.Inject(target.Source, $"Button{button}Release", target.Device, 0);
        }
        return Describe(target);
    }

    JsonObject Wheel(BridgeCommand c)
    {
        if (c.Delta == 0) throw new BridgeException("\"delta\" must be nonzero (120 is one notch)");
        var target = Resolve(c.At);
        if (target.OwnerDisabled) return Describe(target);
        EnsureActive(target);
        MoveTo(target);
        RawMouse.Inject(target.Source, "VerticalWheelRotate", target.Device, c.Delta);
        return Describe(target);
    }

    void EnsureActive(Target target)
    {
        if (!ReferenceEquals(Mouse.PrimaryDevice.ActiveSource, target.Source)) RawMouse.Inject(target.Source, "Activate", target.Device, 0);
    }

    void MoveTo(Target target)
    {
        RawMouse.Inject(target.Source, "AbsoluteMove", target.Device, 0);
        _lastScreen = target.Screen;
    }

    Target Resolve(BridgePoint? at)
    {
        if (at is null) throw new BridgeException("\"at\" is required");
        Visual anchor;
        Point dip = new(at.X, at.Y);
        if (at.Canvas)
        {
            var host = Host() ?? throw new BridgeException("the main window has no canvas");
            anchor = host;
            dip = CanvasCoordinates.ToFrame(dip, host.Zoom);
        }
        else if (at.Window)
        {
            anchor = _main;
        }
        else
        {
            anchor = FindElement(at.Element!) ?? throw new BridgeException($"no element \"{at.Element}\" in the app's windows");
        }
        var source = PresentationSource.FromVisual(anchor) ?? throw new BridgeException("the target is not shown");
        var root = source.RootVisual ?? throw new BridgeException("the target's window has no root");
        Point rootDip = ReferenceEquals(anchor, root) ? dip : anchor.TransformToAncestor(root).Transform(dip);
        Point device = source.CompositionTarget.TransformToDevice.Transform(rootDip);
        var window = (anchor as DependencyObject is { } d ? Window.GetWindow(d) : null) ?? _main;
        bool disabled = !IsWindowEnabled(new WindowInteropHelper(window).Handle);
        return new Target(source, device, anchor.PointToScreen(dip), window, disabled);
    }

    /// <summary>Sends the owner its WM_SETCURSOR + HTERROR; true when a hook handled it (nonzero result, so Windows does not beep).</summary>
    static bool ClickDisabledWindow(Window window, int button)
    {
        int down = button switch { 1 => 0x0201, 2 => 0x0204, _ => 0x0207 };   // WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN
        nint hwnd = new WindowInteropHelper(window).Handle;
        return SendMessage(hwnd, WM_SETCURSOR, hwnd, (nint)((down << 16) | (HTERROR & 0xFFFF))) != 0;
    }

    JsonObject Describe(Target target) => new()
    {
        ["x"] = target.Device.X,
        ["y"] = target.Device.Y,
        ["over"] = Name(Mouse.DirectlyOver),
        ["captured"] = Name(Mouse.Captured),
        ["disabledOwner"] = target.OwnerDisabled,
    };

    /// <summary>
    /// The hardware neutralizer (R8): an unmarked raw mouse report that moves, presses or turns the wheel is cancelled
    /// and replaced by a marked move to the bridge's last point. Activate, Deactivate and WPF's own bookkeeping pass.
    /// </summary>
    void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (!RawMouse.TryRead(e.StagingItem.Input, out var actions, out var extra, out var source) || extra == Marker) return;
        var kept = Neutralize(actions);
        if (kept == actions) return;
        e.Cancel();
        if (source?.RootVisual is not { } root) return;
        if (_lastScreen is { } screen)
        {
            var device = source.CompositionTarget.TransformToDevice.Transform(root.PointFromScreen(screen));
            e.PushInput(RawMouse.Create(source, kept, device, 0), e.StagingItem);
        }
        else if (Neutralize(actions, withMove: false) is { Length: > 0 } rest)
        {
            e.PushInput(RawMouse.Create(source, rest, default, 0), e.StagingItem);
        }
    }

    /// <summary>
    /// The actions an unmarked report keeps: unchanged when it neither moves, presses nor turns the wheel; otherwise
    /// only its Activate, Deactivate, AttributesChanged, QueryCursor and CancelCapture parts, plus AbsoluteMove
    /// (to the bridge's last point) when <paramref name="withMove"/>.
    /// </summary>
    internal static string Neutralize(string actions, bool withMove = true)
    {
        var parts = actions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool acts = parts.Any(p => p is "AbsoluteMove" or "RelativeMove" or "VirtualDesktopMove" or "VerticalWheelRotate" or "HorizontalWheelRotate"
                                   || p.StartsWith("Button", StringComparison.Ordinal));
        if (!acts) return actions;
        var kept = parts.Where(p => p is "Activate" or "Deactivate" or "AttributesChanged" or "QueryCursor" or "CancelCapture");
        return string.Join(", ", withMove ? kept.Append("AbsoluteMove") : kept);
    }

    // ---- keys (R7) ----

    JsonObject KeyCommand(BridgeCommand c)
    {
        if (c.Key is null || !Enum.GetNames<Key>().Contains(c.Key, StringComparer.Ordinal))
            throw new BridgeException($"unknown key \"{c.Key}\" (a System.Windows.Input.Key name)");
        var key = Enum.Parse<Key>(c.Key);
        if (key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
            throw new BridgeException($"\"{c.Key}\" is not sent as a key; modifiers go in \"modifiers\"");
        string action = (c.Action ?? "press") is "press" or "down" or "up" ? c.Action ?? "press" : throw new BridgeException("\"action\" must be press, down or up");
        var physical = PhysicalModifiers();
        if (physical != ModifierKeys.None)
            throw new BridgeException($"physical modifier keys are held ({physical}); release them and retry");

        var target = KeyTarget();
        var source = PresentationSource.FromDependencyObject((DependencyObject)target) ?? PresentationSource.FromVisual(_main)
            ?? throw new BridgeException("the app has no shown window");
        var keyboard = t_keyboard ??= new BridgeKeyboardDevice();
        keyboard.Modifiers = c.Modifiers;
        var result = new JsonObject { ["target"] = Name(target) };
        try
        {
            if (action is "press" or "down")
            {
                var preview = Raise(keyboard, source, key, Keyboard.PreviewKeyDownEvent, target, c.Modifiers);
                result["previewHandled"] = preview.Handled;
                if (!preview.Handled)
                {
                    if (c.Modifiers == ModifierKeys.None) result["keyDownHandled"] = Raise(keyboard, source, key, Keyboard.KeyDownEvent, target, c.Modifiers).Handled;
                    else result["binding"] = RunKeyBinding(target, key, c.Modifiers);
                }
            }
            if (action is "press" or "up")
            {
                var preview = Raise(keyboard, source, key, Keyboard.PreviewKeyUpEvent, target, c.Modifiers);
                if (!preview.Handled && c.Modifiers == ModifierKeys.None) Raise(keyboard, source, key, Keyboard.KeyUpEvent, target, c.Modifiers);
            }
        }
        finally
        {
            keyboard.Modifiers = ModifierKeys.None;
        }
        return result;
    }

    static KeyEventArgs Raise(BridgeKeyboardDevice keyboard, PresentationSource source, Key key, RoutedEvent routedEvent, IInputElement target, ModifierKeys modifiers)
    {
        var args = new KeyEventArgs(keyboard, source, Environment.TickCount, key) { RoutedEvent = routedEvent, Source = target };
        // Real input delivers an Alt chord as Key.System with SystemKey = the key; handlers read it that way.
        if ((modifiers & ModifierKeys.Alt) != 0)
            (MarkSystem ?? throw new BridgeException("KeyEventArgs.MarkSystem is missing")).Invoke(args, null);
        InputManager.Current.ProcessInput(args);
        return args;
    }

    /// <summary>
    /// CommandManager's KeyDown binding match, done here because KeyGesture.Matches reads the physical modifiers (R7):
    /// from the target up through visual parents (logical when there is none) to the window, the first KeyBinding
    /// with this key and these exact modifiers whose command can execute runs, and the walk stops. Null if none ran.
    /// </summary>
    static string? RunKeyBinding(IInputElement target, Key key, ModifierKeys modifiers)
    {
        for (var d = target as DependencyObject; d is not null; d = Parent(d))
        {
            var bindings = d switch
            {
                UIElement u => u.InputBindings,
                ContentElement c => c.InputBindings,
                UIElement3D u3 => u3.InputBindings,
                _ => null,
            };
            var match = bindings?.OfType<KeyBinding>().FirstOrDefault(b => b.Gesture is KeyGesture g && g.Key == key && g.Modifiers == modifiers);
            if (match?.Command is not { } command) continue;
            var commandTarget = match.CommandTarget ?? d as IInputElement;
            if (command is RoutedCommand routed)
            {
                if (!routed.CanExecute(match.CommandParameter, commandTarget)) continue;
                routed.Execute(match.CommandParameter, commandTarget);
            }
            else
            {
                if (!command.CanExecute(match.CommandParameter)) continue;
                command.Execute(match.CommandParameter);
            }
            return command.GetType().Name;
        }
        return null;
    }

    static DependencyObject? Parent(DependencyObject d) =>
        (d is Visual or Visual3D ? VisualTreeHelper.GetParent(d) : null) ?? LogicalTreeHelper.GetParent(d);

    /// <summary>Keyboard focus when it is in an app window; else the active app window's (or the main window's) focused element; else that window.</summary>
    IInputElement KeyTarget()
    {
        if (Keyboard.FocusedElement is DependencyObject focused && Window.GetWindow(focused) is { } owner && AppWindows().Contains(owner))
            return (IInputElement)focused;
        var window = AppWindows().FirstOrDefault(w => w.IsActive) ?? _main;
        var element = ReferenceEquals(window, _main) ? _main.FocusedElement() : FocusManager.GetFocusedElement(window);
        return element ?? window;
    }

    // ---- wait-idle, file-dialog, inspect ----

    async Task<JsonObject?> WaitIdle(int timeoutMs)
    {
        if (timeoutMs < 0 || timeoutMs > MaxTimeoutMs) throw new BridgeException($"\"timeoutMs\" must be 0..{MaxTimeoutMs}");
        int limit = timeoutMs == 0 ? DefaultTimeoutMs : timeoutMs;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            string? busy = await _dispatcher.InvokeAsync(BusyReason, DispatcherPriority.ApplicationIdle);
            if (busy is null) return new JsonObject { ["ms"] = clock.ElapsedMilliseconds };
            if (clock.ElapsedMilliseconds >= limit) throw new BridgeException($"not idle after {limit} ms: {busy}");
            await Task.Delay(10);
        }
    }

    /// <summary>Why the app is not idle yet, or null: an open wheel burst, a frame waiting to render, a project read.</summary>
    internal string? BusyReason()
    {
        var host = Host();
        if (host is { WheelBurstPending: true }) return "a wheel burst is open";
        if (host is { RenderingHooked: true }) return "a frame is waiting to render";
        if (_main.ViewModel.Files.IsBusy) return "a project is being read";
        return null;
    }

    JsonObject FileDialog(BridgeCommand c)
    {
        var dialogs = _fileDialogs ?? throw new BridgeException("this launch has no bridge file dialogs");
        dialogs.Enqueue(c.Answer);
        return new JsonObject { ["queued"] = c.Answer };
    }

    /// <summary>Read-only state for the smoke's assertions (interfaces §2.7; Task 5 Additions 1 and 2): no input, no change.</summary>
    JsonObject Inspect(BridgeCommand c) => c.Target switch
    {
        "canvas" => InspectCanvas(),
        "layers" => InspectLayers(),
        _ => throw new BridgeException("\"target\" must be \"canvas\" or \"layers\""),
    };

    /// <summary>
    /// The canvas: the cursor the canvas sets (kind and ring, the ring's radius in device px, the host's WPF cursor and
    /// any override, by name), zoom, gesture, capture, history, selection, tool, brush, the layers bottom to top with
    /// their image size and image identity, and the region fills.
    /// </summary>
    JsonObject InspectCanvas()
    {
        var host = Host() ?? throw new BridgeException("the main window has no canvas");
        var session = _main.ViewModel.Session;
        var layers = new JsonArray();
        foreach (var l in session.Document.Layers)
            layers.Add(new JsonObject
            {
                ["id"] = l.Id, ["name"] = l.Name, ["x"] = l.X, ["y"] = l.Y, ["scale"] = l.Scale, ["rotation"] = l.Rotation, ["visible"] = l.Visible,
                ["w"] = l.Image.Width, ["h"] = l.Image.Height, ["image"] = ImageId(l.Image),
            });
        var fills = new JsonArray();
        foreach (var (region, fill) in session.Document.RegionFills)
            fills.Add(new JsonObject { ["region"] = region, ["type"] = fill.Type, ["c1"] = fill.C1, ["c2"] = fill.C2 });
        var cursor = host.CanvasCursor;
        return new JsonObject
        {
            ["cursor"] = new JsonObject { ["kind"] = cursor.Kind.ToString(), ["ring"] = cursor.RingDiameter },
            ["ringRadiusPx"] = cursor.Kind == CanvasCursorKind.EraserRing ? CanvasCursors.RingRadius(cursor.RingDiameter, host.Zoom, host.DeviceScale) : 0.0,
            ["hostCursor"] = CursorName(host.Cursor),
            ["overrideCursor"] = CursorName(Mouse.OverrideCursor),
            ["zoom"] = host.Zoom,
            ["gesture"] = host.Interaction?.Gesture.ToString(),
            ["captured"] = host.IsMouseCaptured,
            ["undo"] = session.UndoCount,
            ["redo"] = session.RedoCount,
            ["selected"] = session.SelectedLayerId,
            ["tool"] = _main.ViewModel.Home.Tools.Current.ToString(),
            ["brush"] = _main.ViewModel.Home.Brush.Size,
            ["layers"] = layers,
            ["fills"] = fills,
        };
    }

    /// <summary>The layers panel: whether a row drag runs, and each row top first with its drag mark and drop line.</summary>
    JsonObject InspectLayers()
    {
        var panel = Descendants(_main).OfType<LayersPanel>().FirstOrDefault() ?? throw new BridgeException("the main window has no layers panel");
        var rows = new JsonArray();
        foreach (var r in _main.ViewModel.Layers.Rows)
            rows.Add(new JsonObject { ["id"] = r.Id, ["name"] = r.Name, ["dragSource"] = r.IsDragSource, ["drop"] = r.DropIndicator.ToString() });
        return new JsonObject { ["dragging"] = panel.IsDragging, ["rows"] = rows };
    }

    /// <summary>The stock WPF cursors the app sets, matched by reference (the Cursors members return cached instances).</summary>
    static readonly (Cursor Cursor, string? Name)[] KnownCursors =
    [
        (Cursors.Cross, "Cross"), (Cursors.SizeAll, "SizeAll"), (Cursors.Hand, "Hand"), (Cursors.SizeNWSE, "SizeNWSE"),
        (Cursors.SizeNS, "SizeNS"), (Cursors.Arrow, "Arrow"), (Cursors.No, "No"), (Cursors.Wait, "Wait"),
    ];

    /// <summary>A stock cursor's name, "Custom" for any other (the eraser ring), null for none.</summary>
    static string? CursorName(Cursor? cursor) =>
        cursor is null ? null : KnownCursors.FirstOrDefault(k => ReferenceEquals(k.Cursor, cursor)).Name ?? "Custom";

    static readonly ConditionalWeakTable<SKImage, StrongBox<long>> ImageIds = new();
    static long s_nextImageId;

    /// <summary>A per-process identity number for an image instance: equal for the same instance, new for a replaced one.</summary>
    static long ImageId(SKImage image) => ImageIds.GetValue(image, _ => new StrongBox<long>(Interlocked.Increment(ref s_nextImageId))).Value;

    // ---- the app's own windows and elements ----

    IEnumerable<Window> AppWindows()
    {
        yield return _main;
        if (Application.Current is { } app)
            foreach (Window w in app.Windows)
                if (!ReferenceEquals(w, _main)) yield return w;
    }

    TemplateCanvasHost? Host() => Descendants(_main).OfType<TemplateCanvasHost>().FirstOrDefault();

    /// <summary>The first element in visual-tree order, over the app's windows, whose AutomationId (else Name) is <paramref name="id"/>.</summary>
    internal Visual? FindElement(string id)
    {
        foreach (var window in AppWindows())
            foreach (var d in Descendants(window))
                if (d is Visual v && (AutomationProperties.GetAutomationId(d) is { Length: > 0 } aid ? aid == id : (d as FrameworkElement)?.Name == id))
                    return v;
        return null;
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            yield return d;
            int n = d is Visual or Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = n - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
    }

    static string? Name(IInputElement? element) => element switch
    {
        null => null,
        DependencyObject d when AutomationProperties.GetAutomationId(d) is { Length: > 0 } id => id,
        FrameworkElement { Name.Length: > 0 } f => f.Name,
        _ => element.GetType().Name,
    };

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowEnabled(nint hWnd);

    [DllImport("user32.dll")]
    static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
}

/// <summary>A command the bridge refuses (bad arguments, no such element): answered as an error, never thrown to the app.</summary>
public sealed class BridgeException(string message) : Exception(message);
