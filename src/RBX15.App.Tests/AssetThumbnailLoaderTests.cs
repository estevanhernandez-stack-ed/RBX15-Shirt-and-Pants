using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Services;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Thumbnails decode on one background worker, newest first, at 160 px through the engine caps (interfaces R5;
/// ledger asset-grid-thumbnails, asset-objecturl-lifecycle, pack-load-sync-and-mime).
/// </summary>
public class AssetThumbnailLoaderTests
{
    [Fact] // newest first: rows scrolled to load before rows scrolled past
    public async Task The_newest_request_is_decoded_first()
    {
        using var gate = new ManualResetEventSlim();
        var order = new ConcurrentQueue<string>();
        using var loader = new AssetThumbnailLoader(p =>
        {
            order.Enqueue(p);
            if (p == "a") gate.Wait(TimeSpan.FromSeconds(10));
            return null;
        });
        var a = loader.LoadAsync("a", default);
        Assert.True(SpinWait.SpinUntil(() => !order.IsEmpty, TimeSpan.FromSeconds(10)));   // the worker is inside "a"
        var rest = new[] { "b", "c", "d" }.Select(p => loader.LoadAsync(p, default)).ToArray();
        gate.Set();
        await Task.WhenAll(rest.Prepend(a));
        Assert.Equal(["a", "d", "c", "b"], order);
    }

    [Fact] // a request cancelled while queued ends cancelled and is never decoded (a newer set replaced it)
    public async Task A_request_cancelled_while_queued_is_never_decoded()
    {
        using var gate = new ManualResetEventSlim();
        var decoded = new ConcurrentQueue<string>();
        using var loader = new AssetThumbnailLoader(p =>
        {
            decoded.Enqueue(p);
            if (p == "a") gate.Wait(TimeSpan.FromSeconds(10));
            return null;
        });
        using var cts = new CancellationTokenSource();
        var a = loader.LoadAsync("a", default);
        Assert.True(SpinWait.SpinUntil(() => !decoded.IsEmpty, TimeSpan.FromSeconds(10)));
        var b = loader.LoadAsync("b", cts.Token);
        cts.Cancel();
        gate.Set();
        await a;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);
        Assert.Equal(["a"], decoded);
        Assert.True(loader.LoadAsync("c", cts.Token).IsCanceled);   // already cancelled: never queued
    }

    [Fact] // the real decode, off the UI thread: frozen Pbgra32 at 96 DPI, the longer side at 160
    public async Task A_file_decodes_to_a_frozen_thumbnail_at_160()
    {
        using var files = new TestFiles();
        using var loader = new AssetThumbnailLoader();
        var thumb = await loader.LoadAsync(files.Image("wide.png", 400, 100, SKColors.Red), default);
        Assert.NotNull(thumb);
        Assert.True(thumb.IsFrozen);
        Assert.Equal((160, 40, 96.0, PixelFormats.Pbgra32), (thumb.PixelWidth, thumb.PixelHeight, thumb.DpiX, thumb.Format));
        var small = await loader.LoadAsync(files.Image("small.png", 50, 30, SKColors.Blue), default);
        Assert.Equal((50, 30), (small!.PixelWidth, small.PixelHeight));   // never upscaled: it shows at its own size
        var px = new byte[4];
        small.CopyPixels(new System.Windows.Int32Rect(25, 15, 1, 1), px, 4, 0);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px);   // BGRA: blue
    }

    [Fact] // over a cap, damaged or gone: null, and the worker goes on to the next request
    public async Task A_failed_decode_is_null_and_the_worker_goes_on()
    {
        using var files = new TestFiles();
        using var loader = new AssetThumbnailLoader();
        Assert.Null(await loader.LoadAsync(files.Write("bomb.png", TestFiles.PngHeader(30000, 30000)), default));
        Assert.Null(await loader.LoadAsync(files.Write("junk.png", [1, 2, 3, 4]), default));
        Assert.Null(await loader.LoadAsync(Path.Combine(files.Dir, "gone.png"), default));
        Assert.NotNull(await loader.LoadAsync(files.Image("fine.png", 8, 8, SKColors.Lime), default));
        using var throwing = new AssetThumbnailLoader(_ => throw new InvalidOperationException("skia"));
        Assert.Null(await throwing.LoadAsync("x", default));
        Assert.Null(await throwing.LoadAsync("y", default));
    }

    [Fact] // Dispose cancels what is queued; nothing more is accepted
    public async Task Dispose_cancels_queued_requests()
    {
        using var gate = new ManualResetEventSlim();
        var started = new ManualResetEventSlim();
        var loader = new AssetThumbnailLoader(p =>
        {
            started.Set();
            gate.Wait(TimeSpan.FromSeconds(10));
            return null;
        });
        var a = loader.LoadAsync("a", default);
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        var b = loader.LoadAsync("b", default);
        loader.Dispose();
        gate.Set();
        Assert.Null(await a);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);
        Assert.Throws<ObjectDisposedException>(() => { _ = loader.LoadAsync("c", default); });
        started.Dispose();
    }

    [Fact] // the bitmap a tile shows is a straight copy of the engine image, premultiplied BGRA
    public void To_bitmap_source_copies_the_pixels()
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(new SKColor(255, 0, 0, 128));
        using var image = surface.Snapshot();
        BitmapSource b = AssetThumbnailLoader.ToBitmapSource(image);
        var px = new byte[8];
        b.CopyPixels(px, 8, 0);
        Assert.Equal(new byte[] { 0, 0, 128, 128, 0, 0, 128, 128 }, px);
    }
}
