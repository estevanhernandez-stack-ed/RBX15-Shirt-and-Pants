using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// Layer-row thumbnails: the raw layer image (4.1 used img.src, so no adjustments, rotation or
/// opacity; ledger layers-list-render), downscaled to fit <see cref="PixelSide"/> pixels and frozen.
/// The row shows it at 48x48 DIP with Stretch=Uniform on black (object-fit: contain, editor.js:685).
/// Cached per image, so rebuilding a row never re-renders an unchanged image.
/// </summary>
internal static class LayerThumbnails
{
    /// <summary>Two device pixels per DIP of the 48 DIP box: sharp up to 200% display scale.</summary>
    public const int PixelSide = 96;

    static readonly ConditionalWeakTable<SKImage, BitmapSource> Cache = new();

    public static BitmapSource For(SKImage image) => Cache.GetValue(image, Render);

    static BitmapSource Render(SKImage image)
    {
        double s = Math.Min(1.0, Math.Min((double)PixelSide / image.Width, (double)PixelSide / image.Height));
        int w = Math.Max(1, (int)Math.Round(image.Width * s)), h = Math.Max(1, (int)Math.Round(image.Height * s));
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create thumbnail surface");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(Images.ForDrawing(image), SKRect.Create(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var pixmap = surface.PeekPixels();
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixmap.GetPixels(), pixmap.RowBytes * h, pixmap.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }
}
