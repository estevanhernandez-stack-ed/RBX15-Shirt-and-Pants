using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>
/// Decodes asset-browser thumbnails off the UI thread (spec §3; ledger asset-grid-thumbnails, asset-objecturl-lifecycle,
/// pack-load-sync-and-mime, all fix: 4.1 read every pack file synchronously and decoded every image at full size,
/// editor.js:2347-2353, :2475). One background worker takes the newest request first, so the rows the user has just
/// scrolled to load before the ones scrolled past. Each file goes through the engine's decode caps
/// (<see cref="Images.CheckEncodedSize"/> on the file length before reading, then <see cref="Images.DecodeThumbnail"/>)
/// and comes back as a frozen Pbgra32 <see cref="BitmapSource"/> at 96 DPI, so a file smaller than
/// <see cref="PixelSide"/> shows at its own size in DIPs, as 4.1's img did at its own CSS size.
/// </summary>
public sealed class AssetThumbnailLoader : IDisposable
{
    /// <summary>The thumbnail's longer side in pixels: two device pixels per DIP of the widest tile (81 DIP in the 280 panel).</summary>
    public const int PixelSide = 160;

    readonly Func<string, BitmapSource?> _decode;
    readonly object _gate = new();
    readonly List<Request> _pending = [];
    bool _running;
    bool _disposed;

    public AssetThumbnailLoader() : this(Decode) { }

    /// <summary>Tests pass a decode that blocks or records, to observe the order and the cancellation.</summary>
    internal AssetThumbnailLoader(Func<string, BitmapSource?> decode) => _decode = decode ?? throw new ArgumentNullException(nameof(decode));

    /// <summary>
    /// Queues <paramref name="path"/>. The task gives the thumbnail, or null when the file is over a cap, cannot be
    /// decoded or cannot be read (or the decode fails in any other way: a thumbnail never stops the worker). A request
    /// cancelled before the worker reaches it ends cancelled and is never decoded.
    /// </summary>
    public Task<BitmapSource?> LoadAsync(string path, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (cancel.IsCancellationRequested) return Task.FromCanceled<BitmapSource?>(cancel);
        var request = new Request(path, cancel);
        bool start;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pending.Add(request);
            start = !_running;
            _running = true;
        }
        if (start) _ = Task.Run(Drain);
        return request.Done.Task;
    }

    void Drain()
    {
        while (true)
        {
            Request next;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _running = false;
                    return;
                }
                next = _pending[^1];
                _pending.RemoveAt(_pending.Count - 1);
            }
            if (next.Cancel.IsCancellationRequested)
            {
                next.Done.TrySetCanceled(next.Cancel);
                continue;
            }
            BitmapSource? result;
            try
            {
                result = _decode(next.Path);
            }
            catch (Exception)
            {
                result = null;   // a cap, a damaged file, a vanished file or a Skia failure: the tile shows no picture
            }
            next.Done.TrySetResult(result);
        }
    }

    /// <summary>Cancels every queued request; a decode already running finishes and is delivered.</summary>
    public void Dispose()
    {
        List<Request> dropped;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            dropped = [.. _pending];
            _pending.Clear();
        }
        foreach (var r in dropped) r.Done.TrySetCanceled();
    }

    static BitmapSource? Decode(string path)
    {
        Images.CheckEncodedSize(new FileInfo(path).Length);
        using var image = Images.DecodeThumbnail(File.ReadAllBytes(path), PixelSide);
        return ToBitmapSource(image);
    }

    /// <summary>A frozen Pbgra32 copy of <paramref name="image"/> at <paramref name="dpi"/> (96 by default). Safe on any thread.</summary>
    internal static BitmapSource ToBitmapSource(SKImage image, double dpi = 96)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0)) throw new InvalidOperationException("cannot read thumbnail pixels");
        var bitmap = BitmapSource.Create(info.Width, info.Height, dpi, dpi, PixelFormats.Pbgra32, null, bmp.GetPixels(), bmp.RowBytes * info.Height, bmp.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }

    sealed record Request(string Path, CancellationToken Cancel)
    {
        public TaskCompletionSource<BitmapSource?> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
