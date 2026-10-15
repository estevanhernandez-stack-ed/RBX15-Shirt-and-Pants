using System.IO;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>A decoded file ready to become a layer, named as 4.1 named it.</summary>
public sealed record ImportedImage(SKImage Image, string Name);

/// <summary>A file that did not become a layer, and why, for the status strip.</summary>
public sealed record ImportFailure(string FileName, string Reason);

/// <summary>Everything one pick or drop produced, in the order given.</summary>
public sealed record ImportResult(IReadOnlyList<ImportedImage> Images, IReadOnlyList<ImportFailure> Failures);

/// <summary>
/// Reads and decodes picked or dropped image files (4.1: isImageFile and handleFiles,
/// editor.js:988-1012; Add image, editor.js:2692-2708). Formats are Este's ruling of 2026-10-14:
/// PNG, JPEG, WebP, BMP and GIF (first frame); SVG is refused with a message (ledger drop-filetypes,
/// image-decode-format-matrix, io-image-formats). Every file is read synchronously, in the order
/// given, so the layer order is deterministic (layers-multi-file-async-order).
/// </summary>
public static class ImageImporter
{
    /// <summary>The accepted extensions, lowercase, matched case-insensitively.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    /// <summary>The Open dialog filter.</summary>
    public const string OpenFilter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif";

    public const string SvgReason = "SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF.";
    public const string UnsupportedReason = "Not a supported image type. Use PNG, JPEG, WebP, BMP or GIF.";
    public const string UndecodableReason = "The file isn't a readable image, or it is damaged.";
    public const string ReadFailedReason = "The file couldn't be read.";

    /// <summary>
    /// The layer name: the file name minus its last extension, as 4.1's
    /// <c>f.name.replace(/\.[^.]+$/, '')</c> (editor.js:1001, :2698). "a.b.png" gives "a.b",
    /// ".png" gives "" (the caller's default name then applies), "file." stays "file.".
    /// </summary>
    public static string LayerName(string path)
    {
        string name = Path.GetFileName(path);
        int dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[..dot] : name;
    }

    /// <summary>
    /// Loads <paramref name="paths"/> in the given order. An .svg fails without being read. Any other
    /// extension outside <see cref="Extensions"/> fails without being read (4.1 skipped it with a
    /// console line). A file over <see cref="Images.MaxEncodedBytes"/> fails before it is read. The
    /// rest go through <see cref="Images.Decode"/>; a cap or decode failure is reported, never thrown.
    /// </summary>
    public static ImportResult Load(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var images = new List<ImportedImage>();
        var failures = new List<ImportFailure>();
        foreach (var path in paths)
        {
            string fileName = Path.GetFileName(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".svg") { failures.Add(new(fileName, SvgReason)); continue; }
            if (!Extensions.Contains(ext)) { failures.Add(new(fileName, UnsupportedReason)); continue; }
            try
            {
                Images.CheckEncodedSize(new FileInfo(path).Length);
                images.Add(new ImportedImage(Images.Decode(File.ReadAllBytes(path)), LayerName(path)));
            }
            catch (ImageTooLargeException e) { failures.Add(new(fileName, e.Message)); }
            catch (InvalidDataException) { failures.Add(new(fileName, UndecodableReason)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failures.Add(new(fileName, ReadFailedReason)); }
        }
        return new ImportResult(images, failures);
    }
}
