using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// A region tile's swatch, painted by the engine's own fill painter (ledger <c>fills-to-css</c> and
/// <c>regions-button-swatch-paint</c>, fix). 4.1 approximated the fill in CSS (lib/fills.js:167-185),
/// whose angle convention (0deg is bottom to top) contradicts the canvas (0 is left to right). The
/// swatch is the region's own rectangle painted at template scale exactly as the canvas paints it, so
/// a checker or dots cell is the same template-space cell. The region is its own gradient extent, so a
/// tile shows the whole c1 to c2 run whatever its neighbors hold. The tile scales the bitmap to fit.
/// </summary>
internal static class RegionSwatch
{
    /// <summary>Pbgra32 at 96 DPI, frozen, so any thread can read it.</summary>
    public static BitmapSource Render(TemplateMode mode, string region, Fill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        if (!Template.TryFind(mode, region, out var r))
            throw new ArgumentException($"'{region}' is not a {mode} region.", nameof(region));
        var info = new SKImageInfo(r.W, r.H, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create swatch surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(-r.X, -r.Y);
        Fills.PaintRegionFill(canvas, r.Rect, fill);
        canvas.Flush();
        using var pixmap = surface.PeekPixels();
        var bitmap = BitmapSource.Create(r.W, r.H, 96, 96, PixelFormats.Pbgra32, null, pixmap.GetPixelSpan().ToArray(), pixmap.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }
}
