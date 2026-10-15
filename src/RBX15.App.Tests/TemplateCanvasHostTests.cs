using System.Collections.Immutable;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Controls;
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

    [Fact] // ruling R5 (critic C13): a left click without a drag runs ClickCommand with position / zoom (editor.js:1058-1061, :1100-1116)
    public void A_click_runs_the_click_command_with_the_template_point_and_a_drag_does_not() => StaThread.Run(() =>
    {
        var host = Host(new Document(), zoom: 2);
        Assert.False(host.HandleClick(new Point(100, 50))); // no command yet

        var command = new RecordingCommand();
        host.ClickCommand = command;
        Assert.True(host.HandleClick(new Point(100, 50)));
        Assert.Equal(new Point(50, 25), Assert.IsType<Point>(Assert.Single(command.Executed)));

        host.ClickCommand = new RecordingCommand(canExecute: false);
        Assert.False(host.HandleClick(new Point(100, 50)));

        // A press and release farther apart than the system drag distance is a drag (M3b), not a click.
        var down = new Point(10, 10);
        Assert.True(TemplateCanvasHost.IsClick(down, new Point(10 + SystemParameters.MinimumHorizontalDragDistance, 10)));
        Assert.False(TemplateCanvasHost.IsClick(down, new Point(11 + SystemParameters.MinimumHorizontalDragDistance, 10)));
        Assert.False(TemplateCanvasHost.IsClick(down, new Point(10, 11 + SystemParameters.MinimumVerticalDragDistance)));
    });

    [Fact] // controller ruling 2a: 4.1 selects on mousedown, so the PRESS point is hit-tested, not the release point
    public void A_click_hit_tests_at_the_press_point_not_the_release_point() => StaThread.Run(() =>
    {
        var host = Host(new Document(), zoom: 2);
        var command = new RecordingCommand();
        host.ClickCommand = command;
        Assert.True(host.CompleteClick(new Point(100, 50), new Point(102, 51)));
        Assert.Equal(new Point(50, 25), Assert.IsType<Point>(Assert.Single(command.Executed)));
        // A drag runs nothing.
        Assert.False(host.CompleteClick(new Point(100, 50), new Point(300, 50)));
        Assert.Single(command.Executed);
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
