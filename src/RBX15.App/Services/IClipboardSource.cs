using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RBX15.App.Services;

/// <summary>What the clipboard held when read, in decision P2's order of preference.</summary>
public abstract record ClipboardContent
{
    /// <summary>The clipboard's <c>PNG</c> format: encoded PNG bytes, alpha kept.</summary>
    public sealed record Png(byte[] Bytes) : ClipboardContent;

    /// <summary>A file list (an Explorer copy): the paths, image or not.</summary>
    public sealed record Files(IReadOnlyList<string> Paths) : ClipboardContent;

    /// <summary>A bitmap format (<c>CF_DIBV5</c>, then <c>CF_DIB</c>), already encoded as PNG bytes.</summary>
    public sealed record Bitmap(byte[] PngBytes) : ClipboardContent;

    /// <summary>Nothing an image can come from (empty, or text only).</summary>
    public sealed record None : ClipboardContent;
}

/// <summary>
/// The system clipboard, as a seam (M5 item 6): the app's source reads WPF's <see cref="Clipboard"/>, the tests' fake returns what it was given.
/// No test reads the real clipboard.
/// </summary>
public interface IClipboardSource
{
    /// <summary>
    /// Reads the clipboard once. Throws <see cref="COMException"/> (<see cref="PasteService.ClipboardCantOpen"/>) when another app holds it;
    /// <see cref="PasteService"/> retries.
    /// </summary>
    ClipboardContent Read();
}

/// <summary>
/// The app's source (decision P2): the <c>PNG</c> stream first, so alpha survives; then a file list; then a bitmap, encoded to PNG.
/// A browser's copied image often carries only <c>CF_DIB</c>, whose alpha channel may be all zero: that is treated as opaque.
/// </summary>
public sealed class WpfClipboardSource : IClipboardSource
{
    public ClipboardContent Read()
    {
        var data = Clipboard.GetDataObject();
        if (data is null) return new ClipboardContent.None();
        if (data.GetDataPresent("PNG") && data.GetData("PNG") is MemoryStream png)
            return new ClipboardContent.Png(png.ToArray());
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            return new ClipboardContent.Files(files);
        if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            return new ClipboardContent.Bitmap(EncodeBitmap(image));
        return new ClipboardContent.None();
    }

    static byte[] EncodeBitmap(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        bool anyAlpha = false;
        for (int i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0) { anyAlpha = true; break; }
        if (!anyAlpha)
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(bgra.PixelWidth, bgra.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
