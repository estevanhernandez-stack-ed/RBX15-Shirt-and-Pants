using System.Collections.Immutable;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas host shows engine frames 1:1 at device pixels (spec §4 canvas host). Every test pins the
/// device scale with ApplyDpi, so the result never depends on the test machine's monitor.
/// </summary>
public class TemplateCanvasHostTests
{
    static SKImage Solid(int w, int h, SKColor c)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(c);
        return surface.Snapshot();
    }

    /// <summary>A document with a fill, a layer that crosses the mask and an off-region area.</summary>
    static Document Sample(bool grid = false) => new()
    {
        BgOpacity = 0.6,
        ShowGrid = grid,
        RegionFills = ImmutableList.Create(("torso_front", Fill.Solid("#17d4fa"))),
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Solid(120, 40, new SKColor(242, 47, 137)), X = 180, Y = 120, Rotation = 12 }),
    };

    static TemplateCanvasHost Host(Document? doc, double zoom = 1, double deviceScale = 1)
    {
        var host = new TemplateCanvasHost();
        host.ApplyDpi(deviceScale);
        host.Zoom = zoom;
        host.Document = doc;
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return host;
    }

    static byte[] Pixels(BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        var bytes = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(bytes, stride, 0);
        return bytes;
    }

    /// <summary>The engine's own frame as premultiplied BGRA bytes, the layout of Pbgra32.</summary>
    static byte[] EngineFrame(Document doc, ViewOptions options)
    {
        using var image = Renderer.RenderViewImage(doc, options);
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.Bytes;
    }

    [Fact] // the Renderer's precondition: Pbgra32 wrapped as Bgra8888 is kN32 premul on Windows
    public void Pbgra32_is_skias_native_premultiplied_format_here() =>
        Assert.Equal(SKColorType.Bgra8888, SKImageInfo.PlatformColorType);

    [Fact] // spec §1: the app never draws pixels itself; it shows engine frames
    public void The_frame_is_the_engine_view_pixel_for_pixel() => StaThread.Run(() =>
    {
        var doc = Sample(grid: true);
        var host = Host(doc, zoom: 1.5);
        var frame = Assert.IsType<WriteableBitmap>(host.Frame);
        Assert.Equal(PixelFormats.Pbgra32, frame.Format);
        Assert.Equal((877, 838), (frame.PixelWidth, frame.PixelHeight));
        Assert.Equal(EngineFrame(doc, new ViewOptions(1.5, 1, true)), Pixels(frame));
    });

    [Theory] // render-zoom-canvas sizes; cross-window-dpi-rendering: DIP size = pixels / device scale
    [InlineData(0.25, 1, 146, 139)]
    [InlineData(1, 1, 585, 559)]
    [InlineData(1.5, 1, 877, 838)]
    [InlineData(3, 1, 1755, 1677)]
    [InlineData(1, 1.25, 731, 698)]
    [InlineData(1, 1.5, 877, 838)]
    [InlineData(0.5, 2, 585, 559)]
    public void Frame_pixels_follow_zoom_and_device_scale_and_DIPs_divide_them_back(double zoom, double scale, int w, int h) => StaThread.Run(() =>
    {
        var host = Host(new Document(), zoom, scale);
        Assert.Equal((w, h), (host.Frame!.PixelWidth, host.Frame.PixelHeight));
        Assert.Equal(96 * scale, host.Frame.DpiX, 9);
        Assert.Equal(w / scale, host.DesiredSize.Width, 9);
        Assert.Equal(h / scale, host.DesiredSize.Height, 9);
    });

    [Fact] // a document, zoom or grid change re-renders (render-pipeline-canvas-reset)
    public void Document_and_zoom_changes_re_render() => StaThread.Run(() =>
    {
        var host = Host(Sample());
        var plain = Pixels(host.Frame!);

        host.Document = Sample(grid: true);
        Assert.NotEqual(plain, Pixels(host.Frame!));
        Assert.Equal(EngineFrame(Sample(grid: true), new ViewOptions(1, 1, true)), Pixels(host.Frame!));

        host.Zoom = 0.5;
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal((292, 279), (host.Frame!.PixelWidth, host.Frame.PixelHeight));
        Assert.Equal(new Size(292, 279), host.DesiredSize);
    });

    [Fact] // OnDpiChanged: moving the window to a 150% monitor renders at 1.5 device pixels per DIP
    public void A_dpi_change_re_renders_at_the_new_device_scale() => StaThread.Run(() =>
    {
        var doc = Sample(grid: true);
        var host = Host(doc);
        host.ApplyDpi(1.5);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal(1.5, host.DeviceScale);
        Assert.Equal(144, host.Frame!.DpiX, 9);
        Assert.Equal(EngineFrame(doc, new ViewOptions(1, 1.5, true)), Pixels(host.Frame));
        Assert.Equal(877 / 1.5, host.DesiredSize.Width, 9);
    });

    [Fact]
    public void Without_a_document_there_is_no_frame_and_no_size() => StaThread.Run(() =>
    {
        var host = Host(null);
        Assert.Null(host.Frame);
        Assert.Equal(new Size(0, 0), host.DesiredSize);
        host.Document = new Document();
        Assert.NotNull(host.Frame);
        host.Document = null;
        Assert.Null(host.Frame);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void A_zoom_that_is_not_finite_and_positive_is_refused(double zoom) => StaThread.Run(() =>
    {
        var host = new TemplateCanvasHost();
        Assert.Throws<ArgumentException>(() => host.Zoom = zoom);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.ApplyDpi(zoom));
    });

    sealed class RecordingCommand(bool canExecute = true) : ICommand
    {
        public List<object?> Executed { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    [Fact] // drop-canvas: drop point = position / zoom (editor.js:966-967); every file, in the order given
    public void Dropped_files_run_the_command_with_the_template_point() => StaThread.Run(() =>
    {
        var command = new RecordingCommand();
        var host = Host(new Document(), zoom: 2);
        host.DropCommand = command;
        var data = new DataObject(DataFormats.FileDrop, new[] { @"C:\art\b.png", @"C:\art\a.png" });

        Assert.True(host.HandleDrop(data, new Point(100, 50)));
        var drop = Assert.IsType<CanvasDrop>(Assert.Single(command.Executed));
        Assert.Equal(new[] { @"C:\art\b.png", @"C:\art\a.png" }, drop.Paths);
        Assert.Equal(new Point(50, 25), drop.TemplatePoint);
    });

    [Fact] // Review Focus: text, a browser image with no file, or no handler does nothing and never throws
    public void A_drop_without_files_or_without_a_willing_command_does_nothing() => StaThread.Run(() =>
    {
        var host = Host(new Document());
        var command = new RecordingCommand();
        host.DropCommand = command;
        Assert.False(host.HandleDrop(new DataObject(DataFormats.UnicodeText, "https://example.com/shirt.png"), new Point(10, 10)));
        Assert.False(host.HandleDrop(new DataObject(DataFormats.FileDrop, Array.Empty<string>()), new Point(10, 10)));
        Assert.Empty(command.Executed);

        host.DropCommand = new RecordingCommand(canExecute: false);
        Assert.False(host.HandleDrop(new DataObject(DataFormats.FileDrop, new[] { @"C:\art\a.png" }), new Point(10, 10)));

        host.DropCommand = null;
        Assert.False(host.HandleDrop(new DataObject(DataFormats.FileDrop, new[] { @"C:\art\a.png" }), new Point(10, 10)));
    });

    /// <summary>A host at <paramref name="zoom"/> over <paramref name="session"/>'s document, wired to a fresh interaction.</summary>
    static (TemplateCanvasHost Host, CanvasInteraction Interaction, ToolsViewModel Tools) Wired(EditorSession session, double zoom = 1)
    {
        var tools = new ToolsViewModel();
        var interaction = new CanvasInteraction(session, tools, new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var host = Host(session.Document, zoom);
        host.Interaction = interaction;
        session.Changed += (_, _) => host.Document = session.Document;
        return (host, interaction, tools);
    }

    static Layer Box() => new() { Id = 1, Image = Solid(100, 50, new SKColor(242, 47, 137)), X = 100, Y = 100 };

    [Fact] // select-move parityCheck: drag by (50,30) screen px at zoom 2 moves (25,15); canvas-coord-map: position / zoom
    public void Pointer_seams_map_by_zoom_and_drive_the_interaction() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(Box()) });
        var (host, interaction, _) = Wired(session, zoom: 2);
        Assert.True(host.HandlePointerDown(new Point(240, 220)));   // template (120, 110): on the layer
        Assert.Equal(CanvasGesture.Move, interaction.Gesture);
        host.HandlePointerMove(new Point(290, 250));
        host.HandlePointerUp(new Point(290, 250));
        var l = session.Document.Layers[0];
        Assert.Equal((125.0, 115.0), (l.X, l.Y));
        Assert.Equal(1, session.UndoCount);
        Assert.False(host.IsMouseCaptured);

        Assert.False(host.HandlePointerDown(new Point(20, 1000)));  // empty canvas: deselect, no gesture
        Assert.Null(session.SelectedLayerId);
        host.HandleLostCapture();                                   // idle: nothing
        host.HandlePointerLeave();
        Assert.Equal(1, session.UndoCount);

        host.Interaction = null;                                    // no interaction: input is ignored
        Assert.False(host.HandlePointerDown(new Point(240, 220)));
        host.HandlePointerMove(new Point(10, 10));
        host.HandlePointerUp(new Point(10, 10));
        host.HandleWheel(120);
    });

    [Fact] // canvas-mouse-button-agnostic fix (R10): only the left button reaches the interaction; right and middle stay unhandled
    public void Only_the_left_button_reaches_the_interaction() => StaThread.Run(() =>
    {
        var layer = new Layer { Id = 1, Image = Solid(100, 50, SKColors.Red), X = 0, Y = 0 };
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(layer) });
        var (host, interaction, _) = Wired(session);
        MouseButtonEventArgs Press(MouseButton button) =>
            new(Mouse.PrimaryDevice, Environment.TickCount, button) { RoutedEvent = Mouse.MouseDownEvent };

        foreach (var button in new[] { MouseButton.Right, MouseButton.Middle })
        {
            var e = Press(button);
            host.RaiseEvent(e);
            Assert.False(e.Handled);
            Assert.Equal((CanvasGesture.None, (int?)null), (interaction.Gesture, session.SelectedLayerId));
        }
        var left = Press(MouseButton.Left);
        host.RaiseEvent(left);                                      // an element outside a window reads the position as (0, 0)
        Assert.True(left.Handled);
        // critic mn2 (pre-flight B1): an unparented host cannot capture the mouse, so the guard ends the started gesture
        Assert.Equal((CanvasGesture.None, (int?)1), (interaction.Gesture, session.SelectedLayerId));
        Assert.False(session.InGesture);
    });

    [Fact] // wheel-sign-only-and-midstroke fix: sign only, Delta >= 0 grows; a 400 ms quiet period closes the burst (R7)
    public void The_wheel_is_sign_only_and_a_quiet_period_closes_the_burst() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(Box()) });
        session.Select(1);
        var (host, _, _) = Wired(session);
        host.HandleWheel(120);
        host.HandleWheel(1);
        host.HandleWheel(0);
        Assert.Equal(Math.Pow(1.05, 3), session.Document.Layers[0].Scale, 12);
        host.HandleWheel(-480);
        Assert.Equal(Math.Pow(1.05, 2), session.Document.Layers[0].Scale, 12);
        Assert.True(host.WheelBurstPending);
        Assert.Equal(TimeSpan.FromMilliseconds(400), TemplateCanvasHost.WheelBurstDelay);
        Assert.Equal(0, session.UndoCount);
        host.FlushWheelBurst();                                     // the timer's tick
        Assert.False(host.WheelBurstPending);
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // render coalescing: N frame-input changes in one dispatcher turn render once, and the result is the last state
    public void Many_frame_input_changes_render_once() => StaThread.Run(() =>
    {
        var host = Host(Sample());
        _ = host.Frame;
        int before = host.RenderCount;
        for (int i = 0; i < 20; i++)
            host.Document = Sample() with { BgOpacity = i / 20.0 };
        host.Overlay = new ViewOverlay(1, null);
        Assert.Equal(before, host.RenderCount);                     // nothing rendered yet: one Rendering tick is pending
        var frame = Pixels(host.Frame!);
        Assert.Equal(before + 1, host.RenderCount);
        _ = host.Frame;
        Assert.Equal(before + 1, host.RenderCount);                 // nothing pending: no render
        var last = Sample() with { BgOpacity = 19 / 20.0 };
        Assert.Equal(EngineFrame(last, new ViewOptions(1, 1) { SelectedLayerId = 1 }), frame);
    });

    [Fact] // a drag changes only the frame's content: no layout pass per mouse event; a zoom resizes it
    public void Content_changes_keep_the_measure_and_a_zoom_invalidates_it() => StaThread.Run(() =>
    {
        var host = Host(Sample());
        Assert.True(host.IsMeasureValid);
        host.Document = Sample(grid: true);
        host.Overlay = new ViewOverlay(1, null);
        Assert.True(host.IsMeasureValid);
        host.Zoom = 2;
        Assert.False(host.IsMeasureValid);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal(new Size(1170, 1118), host.DesiredSize);
    });

    [Fact] // the overlay and the preview reach the engine: ViewOptions.SelectedLayerId and Preview (interfaces section 2)
    public void The_overlay_reaches_the_engine_frame() => StaThread.Run(() =>
    {
        var doc = Sample();
        var host = Host(doc, zoom: 1.5);
        var plain = Pixels(host.Frame!);
        var preview = LivePreview.Draw(new Stroke(ShapeTool.Line, [new SKPoint(20, 20), new SKPoint(300, 300)], SKColors.Lime, 8, false));
        host.Overlay = new ViewOverlay(1, preview);
        var shown = Pixels(host.Frame!);
        Assert.Equal(EngineFrame(doc, new ViewOptions(1.5, 1) { SelectedLayerId = 1, Preview = preview }), shown);
        Assert.NotEqual(plain, shown);
        host.Overlay = ViewOverlay.None;
        Assert.Equal(plain, Pixels(host.Frame!));
    });

    [Fact] // tools-cursor fix: the cursor follows the canvas cursor, and the eraser ring follows zoom and the device scale
    public void The_cursor_follows_the_canvas_cursor_zoom_and_device_scale() => StaThread.Run(() =>
    {
        var host = Host(new Document());
        Assert.Same(Cursors.Cross, host.Cursor);
        host.CanvasCursor = CanvasCursor.Of(CanvasCursorKind.Move);
        Assert.Same(Cursors.SizeAll, host.Cursor);
        host.CanvasCursor = CanvasCursor.Ring(20);
        var ring = host.Cursor;
        Assert.Same(CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1), ring);
        host.Zoom = 2;
        Assert.Same(CanvasCursors.ToWpf(CanvasCursor.Ring(20), 2, 1), host.Cursor);
        host.ApplyDpi(1.5);
        Assert.Same(CanvasCursors.ToWpf(CanvasCursor.Ring(20), 2, 1.5), host.Cursor);
    });

    /// <summary>An IDataObject whose reads throw, like a shell virtual item or malformed OLE data.</summary>
    sealed class ThrowingData : IDataObject
    {
        public bool PresentThrows { get; init; }
        public object GetData(string format, bool autoConvert) => throw new System.Runtime.InteropServices.COMException("bad OLE data");
        public object GetData(string format) => throw new System.Runtime.InteropServices.COMException("bad OLE data");
        public object GetData(Type format) => throw new System.Runtime.InteropServices.COMException("bad OLE data");
        public bool GetDataPresent(string format, bool autoConvert) => PresentThrows ? throw new System.Runtime.InteropServices.COMException("bad OLE data") : true;
        public bool GetDataPresent(string format) => PresentThrows ? throw new System.Runtime.InteropServices.COMException("bad OLE data") : true;
        public bool GetDataPresent(Type format) => true;
        public string[] GetFormats(bool autoConvert) => [DataFormats.FileDrop];
        public string[] GetFormats() => [DataFormats.FileDrop];
        public void SetData(string format, object data, bool autoConvert) { }
        public void SetData(string format, object data) { }
        public void SetData(Type format, object data) { }
        public void SetData(object data) { }
    }

    [Fact] // controller ruling 2b: a COMException from GetData is "no files", never a crash
    public void An_unreadable_drag_is_not_a_file_drag_and_never_throws() => StaThread.Run(() =>
    {
        var host = Host(new Document());
        var command = new RecordingCommand();
        host.DropCommand = command;
        foreach (var data in new[] { new ThrowingData(), new ThrowingData { PresentThrows = true } })
        {
            Assert.False(host.HandleDrop(data, new Point(10, 10)));
            Assert.Equal(DragDropEffects.None, host.DropEffect(data, new Point(10, 10)));
        }
        Assert.Empty(command.Executed);
    });

    [Fact] // controller ruling 2c: Copy only when the drop would do something
    public void The_drag_cursor_is_none_unless_files_are_carried_and_the_command_would_accept_them() => StaThread.Run(() =>
    {
        var host = Host(new Document());
        var files = new DataObject(DataFormats.FileDrop, new[] { @"C:\art.png" });
        var at = new Point(10, 10);
        Assert.Equal(DragDropEffects.None, host.DropEffect(files, at)); // no command
        host.DropCommand = new RecordingCommand(canExecute: false);
        Assert.Equal(DragDropEffects.None, host.DropEffect(files, at)); // command cannot run
        host.DropCommand = new RecordingCommand();
        Assert.Equal(DragDropEffects.Copy, host.DropEffect(files, at));
        Assert.Equal(DragDropEffects.None, host.DropEffect(new DataObject(DataFormats.UnicodeText, "x"), at)); // not files
    });
}
