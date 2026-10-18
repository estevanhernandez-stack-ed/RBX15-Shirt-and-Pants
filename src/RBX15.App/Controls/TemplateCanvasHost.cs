using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RBX15.App.Canvas;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Controls;

/// <summary>
/// Shows engine view frames in a Pbgra32 <see cref="WriteableBitmap"/>, one bitmap pixel per device pixel, and
/// forwards the pointer to <see cref="Interaction"/>. The engine draws every pixel, the selection overlay and the
/// live preview included; this control only blits (spec §1).
/// <list type="bullet">
/// <item>The frame is <c>Renderer.ViewSize(new ViewOptions(Zoom, DeviceScale, Document.ShowGrid))</c> pixels, and the
///   control measures <c>pixels / DeviceScale</c> DIPs. At 96 DPI, zoom 1 is 585x559 DIPs, the CSS size of 4.1's
///   canvas (canvas-coord-map).</item>
/// <item><b>Render coalescing</b> (M3a final-review carry-forward; render-pipeline-canvas-reset keeps its behavior,
///   not its cost). 4.1 re-rendered on every mouse event (editor.js:1191, :1235). Here a change to Document, Zoom,
///   Overlay or the DPI only marks the frame dirty and hooks one CompositionTarget.Rendering tick, so any number of
///   changes in one frame render once, through one persistent ViewRenderer (4.1's persistent offscreen,
///   editor.js:295-298). Reading Frame or measuring flushes a pending frame at once.</item>
/// <item>Bitmap scaling is nearest-neighbor, so a fractional layout offset never blurs the frame.</item>
/// <item>Files dropped on it run DropCommand with a <see cref="CanvasDrop"/> (drop-canvas). Every drop is handled, so
///   none falls through to the window (shell-external-drop-guard).</item>
/// <item><b>Pointer.</b> Left button only (canvas-mouse-button-agnostic fix). A press that starts a gesture captures
///   the mouse, so a drag goes on past the canvas edge and a release outside still ends it (select-move,
///   draw-release-outside-canvas fixes); a lost capture ends it like a release. The wheel is always handled
///   (editor.js:1306 preventDefault) and a 400 ms quiet period closes a wheel burst (interfaces R7).</item>
/// <item>The cursor is <see cref="CanvasCursor"/> through <see cref="CanvasCursors.ToWpf"/> at the current zoom and
///   device scale, so the eraser ring follows zoom too (tools-cursor fix).</item>
/// <item>Its automation peer is an Image named "Template canvas" whose Value is <see cref="AutomationValue"/>. M4a: the
///   peer's children are the visible layers (<see cref="LayerAutomationPeer"/>), which select and transform through
///   <see cref="LayerAutomation"/>.</item>
/// </list>
/// </summary>
public sealed class TemplateCanvasHost : FrameworkElement
{
    /// <summary>The quiet time after the last wheel event that closes a wheel-scale burst (interfaces R7).</summary>
    public static readonly TimeSpan WheelBurstDelay = TimeSpan.FromMilliseconds(400);

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(Document), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnDocumentChanged));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(1.0, OnZoomChanged),
        value => value is double z && double.IsFinite(z) && z > 0);

    public static readonly DependencyProperty DropCommandProperty = DependencyProperty.Register(
        nameof(DropCommand), typeof(ICommand), typeof(TemplateCanvasHost));

    public static readonly DependencyProperty InteractionProperty = DependencyProperty.Register(
        nameof(Interaction), typeof(CanvasInteraction), typeof(TemplateCanvasHost));

    public static readonly DependencyProperty OverlayProperty = DependencyProperty.Register(
        nameof(Overlay), typeof(ViewOverlay), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnFrameInputChanged));

    public static readonly DependencyProperty CanvasCursorProperty = DependencyProperty.Register(
        nameof(CanvasCursor), typeof(CanvasCursor), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(Canvas.CanvasCursor.Crosshair, (d, _) => ((TemplateCanvasHost)d).UpdateCursor()));

    public static readonly DependencyProperty AutomationValueProperty = DependencyProperty.Register(
        nameof(AutomationValue), typeof(string), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata("", OnAutomationValueChanged));

    public static readonly DependencyProperty LayerAutomationProperty = DependencyProperty.Register(
        nameof(LayerAutomation), typeof(ICanvasLayerAutomation), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnLayerAutomationChanged));

    readonly DispatcherTimer _wheelTimer;
    WriteableBitmap? _frame;
    double _deviceScale;
    ViewRenderer? _renderer;
    bool _framePending;
    bool _renderingHooked;

    public TemplateCanvasHost()
    {
        _deviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        AllowDrop = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        AutomationProperties.SetName(this, "Template canvas");
        _wheelTimer = new DispatcherTimer { Interval = WheelBurstDelay };
        _wheelTimer.Tick += (_, _) => FlushWheelBurst();
        UpdateCursor();
        // A window that opens on a monitor whose DPI differs from the one the control was built for.
        Loaded += (_, _) =>
        {
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (scale != _deviceScale) ApplyDpi(scale);
            else if (_framePending) RequestFrame();
        };
        // The persistent renderer's native surfaces go with the control; a later frame recreates them.
        Unloaded += (_, _) =>
        {
            UnhookRendering();
            FlushWheelBurst();
            _renderer?.Dispose();
            _renderer = null;
        };
    }

    public Document? Document
    {
        get => (Document?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public ICommand? DropCommand
    {
        get => (ICommand?)GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    /// <summary>The canvas input state machine the pointer and wheel are forwarded to; null ignores input.</summary>
    public CanvasInteraction? Interaction
    {
        get => (CanvasInteraction?)GetValue(InteractionProperty);
        set => SetValue(InteractionProperty, value);
    }

    /// <summary>The selection overlay and live preview the engine draws into the frame; null draws neither.</summary>
    public ViewOverlay? Overlay
    {
        get => (ViewOverlay?)GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    /// <summary>The canvas cursor; the control's Cursor is <see cref="CanvasCursors.ToWpf"/> of it.</summary>
    public CanvasCursor CanvasCursor
    {
        get => (CanvasCursor)GetValue(CanvasCursorProperty);
        set => SetValue(CanvasCursorProperty, value);
    }

    /// <summary>The automation peer's Value: the selected layer's name, or "".</summary>
    public string AutomationValue
    {
        get => (string?)GetValue(AutomationValueProperty) ?? "";
        set => SetValue(AutomationValueProperty, value);
    }

    /// <summary>
    /// What the layer automation peers select and transform through (M4a); CanvasView binds the CanvasViewModel.
    /// Null leaves the layers readable but not selectable or movable through UI Automation.
    /// </summary>
    public ICanvasLayerAutomation? LayerAutomation
    {
        get => (ICanvasLayerAutomation?)GetValue(LayerAutomationProperty);
        set => SetValue(LayerAutomationProperty, value);
    }

    /// <summary>The bitmap on screen, rendered now if a frame is pending; null while there is no document. Read by tests.</summary>
    internal WriteableBitmap? Frame
    {
        get
        {
            FlushFrame();
            return _frame;
        }
    }

    /// <summary>Device pixels per DIP that the frame is rendered for.</summary>
    internal double DeviceScale => _deviceScale;

    /// <summary>How many frames this control has rendered. N frame-input changes in one dispatcher turn render once.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>True while a wheel burst waits for its quiet period.</summary>
    internal bool WheelBurstPending => _wheelTimer.IsEnabled;

    /// <summary>True while a CompositionTarget.Rendering tick is hooked for a pending frame. Read by tests.</summary>
    internal bool RenderingHooked => _renderingHooked;

    /// <summary>True while the control keeps a persistent <see cref="ViewRenderer"/>. Read by tests.</summary>
    internal bool HasRenderer => _renderer is not null;

    static void OnFrameInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TemplateCanvasHost)d).RequestFrame();

    static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        host.RequestFrame();
        host.CreatedPeer()?.OnLayersChanged();
    }

    static void OnLayerAutomationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= host.OnLayerAutomationPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged now) now.PropertyChanged += host.OnLayerAutomationPropertyChanged;
        host.CreatedPeer()?.OnSelectionChanged();
    }

    void OnLayerAutomationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ICanvasLayerAutomation.SelectedLayerId)) CreatedPeer()?.OnSelectionChanged();
    }

    /// <summary>The automation peer if a client has asked for it; null otherwise (no peer is made just to notify).</summary>
    TemplateCanvasHostAutomationPeer? CreatedPeer() => UIElementAutomationPeer.FromElement(this) as TemplateCanvasHostAutomationPeer;

    /// <summary>
    /// The screen rectangle, in device pixels, around template points: each goes through CanvasCoordinates.ToFrame at
    /// the current zoom, then PointToScreen. Rect.Empty while the control has no PresentationSource (not shown).
    /// </summary>
    internal Rect ScreenBounds(IEnumerable<Point> templatePoints)
    {
        if (PresentationSource.FromVisual(this) is null) return Rect.Empty;
        var bounds = Rect.Empty;
        foreach (var p in templatePoints) bounds.Union(PointToScreen(CanvasCoordinates.ToFrame(p, Zoom)));
        return bounds;
    }

    static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        host.RequestFrame();
        host.UpdateCursor();
    }

    static void OnAutomationValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TemplateCanvasHost)d).CreatedPeer()?.RaiseValueChanged((string?)e.OldValue ?? "", (string?)e.NewValue ?? "");

    protected override AutomationPeer OnCreateAutomationPeer() => new TemplateCanvasHostAutomationPeer(this);

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplyDpi(newDpi.DpiScaleX);
    }

    /// <summary>Re-renders for a new device scale. OnDpiChanged calls it; tests call it to pin a scale.</summary>
    internal void ApplyDpi(double deviceScale)
    {
        if (!double.IsFinite(deviceScale) || deviceScale <= 0) throw new ArgumentOutOfRangeException(nameof(deviceScale), deviceScale, "device scale must be finite and positive");
        _deviceScale = deviceScale;
        RequestFrame();
        InvalidateMeasure();
        UpdateCursor();
    }

    /// <summary>
    /// Marks the frame dirty and, while the control is loaded, hooks one CompositionTarget.Rendering tick to render
    /// it. Unloaded, the frame only stays pending (final fix wave 2): Loaded requests it again, and a lazy read
    /// (Frame, MeasureOverride) still renders it. The measure is invalidated only when the frame's pixel size
    /// changes, so a drag, which changes only the content, never forces a layout pass per mouse event.
    /// </summary>
    void RequestFrame()
    {
        _framePending = true;
        if (IsLoaded && !_renderingHooked)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderingHooked = true;
        }
        if (TargetPixelSize() != FramePixelSize()) InvalidateMeasure();
    }

    void OnRendering(object? sender, EventArgs e)
    {
        UnhookRendering();
        FlushFrame();
    }

    void UnhookRendering()
    {
        if (!_renderingHooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _renderingHooked = false;
    }

    /// <summary>Renders the pending frame, if any, once through the persistent <see cref="ViewRenderer"/>.</summary>
    void FlushFrame()
    {
        if (!_framePending) return;
        _framePending = false;
        UnhookRendering();   // a lazy flush (Frame, MeasureOverride) leaves no stale Rendering hook behind (critic mn3)
        if (Document is not { } doc)
        {
            if (_frame is null) return;
            _frame = null;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var overlay = Overlay;
        var options = new ViewOptions(Zoom, _deviceScale, doc.ShowGrid)
        {
            SelectedLayerId = overlay?.SelectedLayerId,
            Preview = overlay?.Preview,
        };
        var size = Renderer.ViewSize(options);
        double dpi = 96 * _deviceScale;
        if (_frame is null || _frame.PixelWidth != size.Width || _frame.PixelHeight != size.Height || _frame.DpiX != dpi)
        {
            _frame = new WriteableBitmap(size.Width, size.Height, dpi, dpi, PixelFormats.Pbgra32, null);
            InvalidateMeasure();
        }

        _frame.Lock();
        try
        {
            // Pbgra32 is premultiplied BGRA, which is Skia's kN32 on Windows: the Renderer's precondition.
            var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var surface = SKSurface.Create(info, _frame.BackBuffer, _frame.BackBufferStride)
                ?? throw new InvalidOperationException("cannot wrap the frame bitmap"))
            {
                // Unloaded, a lazy read renders through a renderer it does not keep: the persistent one's native
                // surfaces went with Unloaded and come back only with the next loaded frame (final fix wave 2).
                if (IsLoaded) (_renderer ??= new ViewRenderer()).Render(surface.Canvas, doc, options);
                else using (var once = new ViewRenderer()) once.Render(surface.Canvas, doc, options);
                surface.Canvas.Flush();
            }
            _frame.AddDirtyRect(new Int32Rect(0, 0, size.Width, size.Height));
        }
        finally
        {
            _frame.Unlock();
        }
        RenderCount++;
        InvalidateVisual();
    }

    (int Width, int Height) TargetPixelSize()
    {
        if (Document is null) return (0, 0);
        var size = Renderer.ViewSize(new ViewOptions(Zoom, _deviceScale));
        return (size.Width, size.Height);
    }

    (int Width, int Height) FramePixelSize() => _frame is null ? (0, 0) : (_frame.PixelWidth, _frame.PixelHeight);

    Size FrameSize => _frame is null ? default : new Size(_frame.PixelWidth / _deviceScale, _frame.PixelHeight / _deviceScale);

    protected override Size MeasureOverride(Size availableSize)
    {
        FlushFrame();
        return FrameSize;
    }

    protected override Size ArrangeOverride(Size finalSize) => FrameSize;

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_frame is not null) drawingContext.DrawImage(_frame, new Rect(FrameSize));
    }

    void UpdateCursor() => Cursor = CanvasCursors.ToWpf(CanvasCursor, Zoom, _deviceScale);

    // ---- pointer: the overrides only forward to the seams below, which tests call directly ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Interaction is null) return;
        // A started gesture that could not capture (no visible source) would never see a release or a
        // LostMouseCapture and would hold the session in a gesture: cancel it now (critic mn2; final fix wave 1).
        // Cancel discards a Draw or Erase stroke, so a failed capture never commits a dot layer, and ends a select
        // drag as one step (none here: nothing moved yet). The seam stays as is.
        if (HandlePointerDown(e.GetPosition(this)) && !IsMouseCaptured) Interaction?.Cancel();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        HandlePointerMove(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (Interaction is null) return;
        HandlePointerUp(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        HandleLostCapture();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HandlePointerLeave();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        HandleWheel(e.Delta);
        e.Handled = true;
    }

    /// <summary>A left press at a point in the control (DIPs). Captures the mouse when a gesture starts.</summary>
    internal bool HandlePointerDown(Point inHost)
    {
        if (Interaction is not { } interaction) return false;
        bool started = interaction.PointerDown(CanvasCoordinates.ToTemplate(inHost, Zoom), Zoom);
        if (started) CaptureMouse();
        return started;
    }

    internal void HandlePointerMove(Point inHost) => Interaction?.PointerMove(CanvasCoordinates.ToTemplate(inHost, Zoom), Zoom);

    /// <summary>The left release: ends the gesture, then releases capture (whose LostMouseCapture then finds nothing active).</summary>
    internal void HandlePointerUp(Point inHost)
    {
        Interaction?.PointerUp(CanvasCoordinates.ToTemplate(inHost, Zoom), Zoom);
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    internal void HandleLostCapture() => Interaction?.CaptureLost();

    internal void HandlePointerLeave() => Interaction?.PointerLeave();

    /// <summary>One wheel event: sign only (WPF Delta &gt;= 0 is 4.1's deltaY &lt;= 0, which grows), then restart the burst timer.</summary>
    internal void HandleWheel(int delta)
    {
        Interaction?.Wheel(delta >= 0 ? 1 : -1);
        _wheelTimer.Stop();
        _wheelTimer.Start();
    }

    /// <summary>Ends a wheel burst now: the timer's tick, Unloaded, and tests.</summary>
    internal void FlushWheelBurst()
    {
        _wheelTimer.Stop();
        Interaction?.EndWheelBurst();
    }

    protected override void OnDragEnter(DragEventArgs e) => ShowDropEffect(e);

    protected override void OnDragOver(DragEventArgs e) => ShowDropEffect(e);

    void ShowDropEffect(DragEventArgs e)
    {
        e.Effects = DropEffect(e.Data, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// The cursor a drag shows over the canvas: Copy only when the data carries files and DropCommand
    /// would accept them, otherwise None, so the cursor never promises a drop that does nothing.
    /// </summary>
    internal DragDropEffects DropEffect(IDataObject data, Point inHost)
    {
        if (Files(data) is not { } paths) return DragDropEffects.None;
        var drop = new CanvasDrop(paths, CanvasCoordinates.ToTemplate(inHost, Zoom));
        return DropCommand is { } command && command.CanExecute(drop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        HandleDrop(e.Data, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// The drop logic without DragEventArgs, which tests cannot construct. Runs DropCommand and returns
    /// true when <paramref name="data"/> carries at least one file and the command accepts the drop.
    /// Text, a browser image with no file behind it, or an empty file list does nothing.
    /// </summary>
    internal bool HandleDrop(IDataObject data, Point inHost)
    {
        if (Files(data) is not { } paths) return false;
        var drop = new CanvasDrop(paths, CanvasCoordinates.ToTemplate(inHost, Zoom));
        if (DropCommand is not { } command || !command.CanExecute(drop)) return false;
        command.Execute(drop);
        return true;
    }

    /// <summary>
    /// The dropped file paths, or null. Shell virtual items and malformed OLE data can throw from
    /// GetData (COMException and others); an unreadable drag is not a file drag, so it yields null.
    /// </summary>
    internal static string[]? Files(IDataObject? data)
    {
        if (data is null) return null;
        try
        {
            return data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths
                ? paths : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
