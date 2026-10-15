using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Controls;

/// <summary>
/// Shows engine view frames (<c>Renderer.RenderView</c>) in a Pbgra32 <see cref="WriteableBitmap"/>, one
/// bitmap pixel per device pixel. The engine draws every pixel; this control only blits (spec §1).
/// <list type="bullet">
/// <item>The frame is <c>Renderer.ViewSize(new ViewOptions(Zoom, DeviceScale, Document.ShowGrid))</c> pixels, and the
///   control measures <c>pixels / DeviceScale</c> DIPs. At 96 DPI, zoom 1 is 585x559 DIPs, the CSS size of 4.1's
///   canvas (canvas-coord-map).</item>
/// <item>It re-renders when Document or Zoom changes, and when the monitor's DPI changes (OnDpiChanged; app.manifest
///   makes the app per-monitor DPI v2). 4.1 re-rendered on every change too (render-pipeline-canvas-reset).</item>
/// <item>Bitmap scaling is nearest-neighbor, so a fractional layout offset never blurs the frame. CanvasView sets
///   UseLayoutRounding, so the frame lands on whole device pixels.</item>
/// <item>Files dropped on it run DropCommand with a <see cref="CanvasDrop"/> (drop-canvas). Every drop is handled, so
///   none falls through to the window (shell-external-drop-guard).</item>
/// <item>A left press and release within the system drag distance runs ClickCommand with the template point of
///   the press (canvas-coord-map; 4.1 selects on mousedown). Task 4 wires it to select the topmost layer there, or none (editor.js:1100-1116). The drag
///   gestures are M3b's select tool.</item>
/// </list>
/// </summary>
public sealed class TemplateCanvasHost : FrameworkElement
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(Document), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnFrameInputChanged));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnFrameInputChanged),
        value => value is double z && double.IsFinite(z) && z > 0);

    public static readonly DependencyProperty DropCommandProperty = DependencyProperty.Register(
        nameof(DropCommand), typeof(ICommand), typeof(TemplateCanvasHost));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(TemplateCanvasHost));

    WriteableBitmap? _frame;
    double _deviceScale;
    Point? _pressedAt;

    public TemplateCanvasHost()
    {
        _deviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        AllowDrop = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        AutomationProperties.SetName(this, "Template canvas");
        // A window that opens on a monitor whose DPI differs from the one the control was built for.
        Loaded += (_, _) =>
        {
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (scale != _deviceScale) ApplyDpi(scale);
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

    /// <summary>Runs with the template <see cref="Point"/> of a left click that did not drag.</summary>
    public ICommand? ClickCommand
    {
        get => (ICommand?)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    /// <summary>The bitmap on screen; null while there is no document. Read by tests.</summary>
    internal WriteableBitmap? Frame => _frame;

    /// <summary>Device pixels per DIP that the frame is rendered for.</summary>
    internal double DeviceScale => _deviceScale;

    static void OnFrameInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TemplateCanvasHost)d).Refresh();

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
        Refresh();
        InvalidateMeasure();
    }

    void Refresh()
    {
        if (Document is not { } doc)
        {
            _frame = null;
            InvalidateVisual();
            return;
        }
        var options = new ViewOptions(Zoom, _deviceScale, doc.ShowGrid);
        var size = Renderer.ViewSize(options);
        double dpi = 96 * _deviceScale;
        if (_frame is null || _frame.PixelWidth != size.Width || _frame.PixelHeight != size.Height || _frame.DpiX != dpi)
            _frame = new WriteableBitmap(size.Width, size.Height, dpi, dpi, PixelFormats.Pbgra32, null);

        _frame.Lock();
        try
        {
            // Pbgra32 is premultiplied BGRA, which is Skia's kN32 on Windows: the Renderer's precondition.
            var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var surface = SKSurface.Create(info, _frame.BackBuffer, _frame.BackBufferStride)
                ?? throw new InvalidOperationException("cannot wrap the frame bitmap"))
            {
                Renderer.RenderView(surface.Canvas, doc, options);
                surface.Canvas.Flush();
            }
            _frame.AddDirtyRect(new Int32Rect(0, 0, size.Width, size.Height));
        }
        finally
        {
            _frame.Unlock();
        }
        InvalidateVisual();
    }

    Size FrameSize => _frame is null ? default : new Size(_frame.PixelWidth / _deviceScale, _frame.PixelHeight / _deviceScale);

    protected override Size MeasureOverride(Size availableSize) => FrameSize;

    protected override Size ArrangeOverride(Size finalSize) => FrameSize;

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_frame is not null) drawingContext.DrawImage(_frame, new Rect(FrameSize));
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
    static string[]? Files(IDataObject? data)
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

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _pressedAt = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pressedAt is not { } down) return;
        _pressedAt = null;
        ReleaseMouseCapture();
        CompleteClick(down, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// A press and release that is a click hit-tests at the PRESS point: 4.1 selects on mousedown
    /// (editor.js:1100-1116), so a slight slide before release never changes what was picked.
    /// </summary>
    internal bool CompleteClick(Point down, Point up) => IsClick(down, up) && HandleClick(down);

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _pressedAt = null;
    }

    /// <summary>A press and release within the system drag distance is a click; farther apart is a drag (M3b).</summary>
    internal static bool IsClick(Point down, Point up) =>
        Math.Abs(up.X - down.X) <= SystemParameters.MinimumHorizontalDragDistance &&
        Math.Abs(up.Y - down.Y) <= SystemParameters.MinimumVerticalDragDistance;

    /// <summary>
    /// The click logic without MouseButtonEventArgs. Runs ClickCommand with <c>position / zoom</c>
    /// (CanvasCoordinates.ToTemplate) and returns true when the command accepts the point.
    /// </summary>
    internal bool HandleClick(Point inHost)
    {
        var point = CanvasCoordinates.ToTemplate(inHost, Zoom);
        if (ClickCommand is not { } command || !command.CanExecute(point)) return false;
        command.Execute(point);
        return true;
    }
}
