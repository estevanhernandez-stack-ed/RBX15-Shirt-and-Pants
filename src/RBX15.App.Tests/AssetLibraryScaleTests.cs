using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using SkiaSharp;
using Xunit.Abstractions;

namespace RBX15.App.Tests;

/// <summary>
/// The asset library at scale (plan M6 item 8; ledger asset-objecturl-lifecycle; spec §3 "Asset library"): 500 PNGs of
/// 1024x1024 through the real <see cref="AssetLibraryViewModel"/>, <see cref="AssetThumbnailLoader"/> and a shown
/// <see cref="AssetLibraryPanel"/>, then the same 500 hidden among 19,500 other files. The images are generated once per
/// class into a temp folder that the fixture deletes; the 20,000-file case and every wall-clock or memory ceiling run only
/// under RBX15_PERF=1 (the M4b D20 rule). The numbers go to the test output either way.
/// </summary>
[Collection("MainWindow")] // shows panels on the shared STA thread, like AssetLibraryPanelTests
public class AssetLibraryScaleTests(AssetLibraryScaleTests.Images images, ITestOutputHelper output) : IClassFixture<AssetLibraryScaleTests.Images>
{
    const int ImageCount = 500;
    const int ImageSide = 1024;
    const int FolderFiles = 20_000;
    const double PanelWidth = 251;

    static bool Perf => Environment.GetEnvironmentVariable("RBX15_PERF") == "1";

    /// <summary>500 deterministic 1024x1024 PNGs (a gradient and a few blocks per index), generated in parallel once per class.</summary>
    public sealed class Images : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "rbx15-app-tests", "scale-" + Guid.NewGuid().ToString("N"));
        public long GenerateMilliseconds { get; }

        public Images()
        {
            Directory.CreateDirectory(Dir);
            var clock = Stopwatch.StartNew();
            Parallel.For(0, ImageCount, i =>
            {
                using var surface = SKSurface.Create(new SKImageInfo(ImageSide, ImageSide, SKColorType.Rgba8888, SKAlphaType.Premul));
                var canvas = surface.Canvas;
                using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(ImageSide, ImageSide),
                    [new SKColor((byte)(i * 7), (byte)(255 - i % 256), (byte)(i * 3)), new SKColor((byte)(i * 11), (byte)(i * 5), 200)], SKShaderTileMode.Clamp))
                using (var paint = new SKPaint { Shader = shader })
                    canvas.DrawRect(0, 0, ImageSide, ImageSide, paint);
                using var block = new SKPaint { Color = new SKColor((byte)(i * 13), 255, (byte)(i * 17), 200) };
                for (int b = 0; b < 8; b++)
                    canvas.DrawRect(((i * 37 + b * 211) % 900), ((i * 53 + b * 97) % 900), 64 + b * 8, 64 + b * 8, block);
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(Dir, $"img{i:D3}.png"), data.ToArray());
            });
            GenerateMilliseconds = clock.ElapsedMilliseconds;
        }

        public void Dispose()
        {
            try { Directory.Delete(Dir, recursive: true); }
            catch (IOException) { }
        }
    }

    static AssetLibraryViewModel Vm(string folder) =>
        new(new ImportService(new EditorSession(), new FakeStatus()), new FakeFileDialogs { PickFolderResult = folder }, new FakeDialogService(),
            new FakeStatus(), new FakeSettingsStore(), new PackSource(PackSource.DefaultDirectory), new AssetThumbnailLoader());

    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        for (int pass = 0; pass < 2; pass++)
        {
            e.Measure(new Size(PanelWidth, 2000));
            e.Arrange(new Rect(0, 0, PanelWidth, Math.Max(e.DesiredSize.Height, 1)));
            e.UpdateLayout();
        }
    }

    static int Realized(AssetLibraryPanel panel) =>
        Enumerable.Range(0, panel.AssetGrid.Items.Count).Count(i => panel.AssetGrid.ItemContainerGenerator.ContainerFromIndex(i) is not null);

    static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            var inner = FindDescendant<T>(child);
            if (inner is not null) return inner;
        }
        return null!;
    }

    /// <summary>Runs the dispatcher until every tile has a thumbnail or has failed (or the deadline passes), without sleeping.</summary>
    static void PumpUntilResolved(IReadOnlyList<AssetTileViewModel> tiles, TimeSpan deadline)
    {
        int pending = tiles.Count(t => t.Thumbnail is null && !t.ThumbnailFailed);   // reading Thumbnail queues the decode
        if (pending == 0) return;
        var frame = new DispatcherFrame();
        void OnChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var t = (AssetTileViewModel)s!;
            if (e.PropertyName is nameof(AssetTileViewModel.Thumbnail) or nameof(AssetTileViewModel.ThumbnailFailed) && --pending == 0)
                frame.Continue = false;
        }
        foreach (var t in tiles) t.PropertyChanged += OnChanged;
        var timeout = new DispatcherTimer(deadline, DispatcherPriority.Normal, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame);
        timeout.Stop();
        foreach (var t in tiles) t.PropertyChanged -= OnChanged;
    }

    /// <summary>Reads the whole panel's reported figures; the numbers every run prints.</summary>
    static long PrivateBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var p = Process.GetCurrentProcess();
        return p.PrivateMemorySize64;
    }

    [Fact] // asset-objecturl-lifecycle: 500 large images resolve at thumbnail size, and only the viewport's rows are realized
    public void Five_hundred_1024_images_resolve_as_thumbnails_and_the_grid_stays_virtualized() => StaThread.Run(() =>
    {
        var vm = Vm(images.Dir);
        var panel = new AssetLibraryPanel { DataContext = vm };
        Layout(panel);
        long before = PrivateBytes();

        var scan = Stopwatch.StartNew();
        vm.OpenFolderCommand.Execute(null);
        long scanMs = scan.ElapsedMilliseconds;
        Layout(panel);

        var tiles = vm.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(ImageCount, tiles.Count);
        Assert.Equal(ImageCount, vm.Set!.Entries.Length);
        int rowCount = vm.Rows.Count;
        int bound = (int)Math.Ceiling(180 / ((PanelWidth - 8 - 2 * 4) / 3 + 4)) + 5;   // the 180 DIP browser's rows, plus the recycler's margin
        int afterLoad = Realized(panel);
        Assert.InRange(afterLoad, 1, bound);
        Assert.True(afterLoad < rowCount / 4, $"{afterLoad} of {rowCount} rows realized");

        var scroller = FindDescendant<ScrollViewer>(panel.AssetGrid);
        scroller.ScrollToEnd();
        Layout(panel);
        int afterScroll = Realized(panel);
        Assert.InRange(afterScroll, 1, bound);
        Assert.True(scroller.VerticalOffset > 0, "the grid did not scroll");

        var decode = Stopwatch.StartNew();
        PumpUntilResolved(tiles, TimeSpan.FromMinutes(5));
        long decodeMs = decode.ElapsedMilliseconds;
        Assert.All(tiles, t =>
        {
            Assert.False(t.ThumbnailFailed, t.ToolTip);
            BitmapSource bmp = t.Thumbnail ?? throw new Xunit.Sdk.XunitException(t.ToolTip + " did not resolve");
            Assert.InRange(Math.Max(bmp.PixelWidth, bmp.PixelHeight), 1, AssetThumbnailLoader.PixelSide);   // never 1024x1024
        });
        var first = tiles[0].Thumbnail!;
        long after = PrivateBytes();
        long growthMb = (after - before) / (1024 * 1024);
        GC.KeepAlive(panel);

        output.WriteLine($"generate {ImageCount} x {ImageSide}x{ImageSide} PNGs: {images.GenerateMilliseconds} ms (fixture, once)");
        output.WriteLine($"folder scan + show ({ImageCount} files): {scanMs} ms");
        output.WriteLine($"rows {rowCount}; realized after load {afterLoad}, after scroll to end {afterScroll} (bound {bound})");
        output.WriteLine($"decode all {ImageCount} thumbnails: {decodeMs} ms; decoded size {first.PixelWidth}x{first.PixelHeight} (loader side {AssetThumbnailLoader.PixelSide})");
        output.WriteLine($"private bytes {before / (1024 * 1024)} MB -> {after / (1024 * 1024)} MB (growth {growthMb} MB)");
        if (Perf) Assert.True(growthMb < 300, $"private bytes grew {growthMb} MB");
    });

    [Fact] // asset-objecturl-lifecycle: a 20,000-file folder lists exactly its 500 images and the UI thread stays responsive (RBX15_PERF=1 only)
    public void A_twenty_thousand_file_folder_lists_its_images_and_keeps_the_ui_thread_free()
    {
        if (!Perf)
        {
            output.WriteLine("skipped without RBX15_PERF=1: the 20,000-file scan, its responsiveness probe and its ceilings");
            return;
        }
        string root = Path.Combine(Path.GetTempPath(), "rbx15-app-tests", "scale20k-" + Guid.NewGuid().ToString("N"));
        try
        {
            var build = Stopwatch.StartNew();
            const int folders = 20;
            for (int f = 0; f < folders; f++) Directory.CreateDirectory(Path.Combine(root, $"set{f:D2}"));
            for (int i = 0; i < ImageCount; i++)
                File.Copy(Path.Combine(images.Dir, $"img{i:D3}.png"), Path.Combine(root, $"set{i % folders:D2}", $"img{i:D3}.png"));
            for (int i = 0; i < FolderFiles - ImageCount; i++)
                File.WriteAllText(Path.Combine(root, $"set{i % folders:D2}", $"note{i:D5}.txt"), "x");
            output.WriteLine($"build the {FolderFiles}-file folder: {build.ElapsedMilliseconds} ms (fixture)");

            StaThread.Run(() =>
            {
                var vm = Vm(root);
                var panel = new AssetLibraryPanel { DataContext = vm };
                Layout(panel);
                long before = PrivateBytes();

                // A probe posts a Normal-priority callback every few ms from another thread and records how long each waited
                // for the UI thread. A scan that blocks the dispatcher shows up as one wait as long as the scan.
                var dispatcher = Dispatcher.CurrentDispatcher;
                var waits = new List<double>();
                using var stop = new CancellationTokenSource();
                var probe = Task.Run(() =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        long posted = Stopwatch.GetTimestamp();
                        dispatcher.BeginInvoke(DispatcherPriority.Normal, () => waits.Add(Stopwatch.GetElapsedTime(posted).TotalMilliseconds));
                        Thread.Yield();
                        stop.Token.WaitHandle.WaitOne(4);
                    }
                });
                var scan = Stopwatch.StartNew();
                vm.OpenFolderCommand.Execute(null);
                long scanMs = scan.ElapsedMilliseconds;
                stop.Cancel();
                probe.Wait();
                StaThread.Pump();
                Layout(panel);

                var tiles = vm.Rows.SelectMany(r => r.Tiles).ToList();
                Assert.Equal(ImageCount, tiles.Count);
                Assert.All(tiles, t => Assert.EndsWith(".png", t.ToolTip));
                Assert.Equal(ImageCount, vm.Set!.Entries.Length);
                double worst = waits.Count == 0 ? 0 : waits.Max();
                long growthMb = (PrivateBytes() - before) / (1024 * 1024);
                output.WriteLine($"{FolderFiles}-file scan + show: {scanMs} ms; worst UI-thread wait {worst:F1} ms over {waits.Count} probes; private bytes growth {growthMb} MB");
                Assert.True(scanMs < 5000, $"the scan took {scanMs} ms");
                Assert.True(growthMb < 300, $"private bytes grew {growthMb} MB");
                Assert.True(worst < 250, $"the UI thread was blocked {worst:F0} ms while the folder scanned");
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }
    }
}
