using System.IO;
using RBX15.App.Services;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// ImageImporter: 4.1's handleFiles and Add image (editor.js:988-1012, :2692-2708) with Este's format
/// ruling of 2026-10-14 (PNG, JPEG, WebP, BMP, GIF first frame; SVG refused) and the decode caps.
/// </summary>
public class ImageImporterTests
{
    static SKColor At(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixel(x, y);
    }

    [Theory] // f.name.replace(/\.[^.]+$/, '') (editor.js:1001, :2698; drop-filetypes)
    [InlineData(@"C:\art\shirt.png", "shirt")]
    [InlineData("a.b.png", "a.b")]
    [InlineData(".png", "")]
    [InlineData("file.", "file.")]
    [InlineData("a..png", "a.")]
    [InlineData("noext", "noext")]
    public void Layer_name_strips_the_last_extension_like_4_1(string path, string expected) =>
        Assert.Equal(expected, ImageImporter.LayerName(path));

    [Fact]
    public void Extensions_and_filter_cover_the_five_formats()
    {
        Assert.Equal(new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" }, ImageImporter.Extensions);
        Assert.Equal("Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif", ImageImporter.OpenFilter);
    }

    [Theory] // each accepted format decodes to its pixel size
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("gif")]
    public void Each_accepted_format_decodes(string format)
    {
        using var files = new TestFiles();
        string path = format switch
        {
            "png" => files.Image("a.png", 6, 4, SKColors.Red),
            "jpg" => files.Image("a.jpg", 6, 4, SKColors.Red, SKEncodedImageFormat.Jpeg),
            "webp" => files.Image("a.webp", 6, 4, SKColors.Red, SKEncodedImageFormat.Webp),
            "bmp" => files.Write("a.bmp", TestFiles.Bmp2x1()),
            _ => files.Write("a.gif", TestFiles.Gif1x1TwoFrames()),
        };
        var result = ImageImporter.Load([path]);
        Assert.Empty(result.Failures);
        var image = Assert.Single(result.Images);
        Assert.Equal("a", image.Name);
        var expected = format switch { "bmp" => (2, 1), "gif" => (1, 1), _ => (6, 4) };
        Assert.Equal(expected, (image.Image.Width, image.Image.Height));
    }

    [Fact] // io-image-formats: first frame only for animated files
    public void An_animated_gif_takes_its_first_frame()
    {
        using var files = new TestFiles();
        var result = ImageImporter.Load([files.Write("anim.gif", TestFiles.Gif1x1TwoFrames())]);
        Assert.Equal(new SKColor(255, 0, 0, 255), At(result.Images[0].Image, 0, 0));
    }

    [Fact] // BMP rows are bottom-up BGR; the decode is left to right as stored
    public void A_bmp_decodes_its_pixels()
    {
        using var files = new TestFiles();
        var image = ImageImporter.Load([files.Write("two.bmp", TestFiles.Bmp2x1())]).Images[0].Image;
        Assert.Equal((SKColors.Red, SKColors.Blue), (At(image, 0, 0), At(image, 1, 0)));
    }

    [Fact] // layers-multi-file-async-order: the given order, not decode completion
    public void Files_load_in_the_given_order()
    {
        using var files = new TestFiles();
        var big = files.Image("big.png", 800, 800, SKColors.Red);
        var small = files.Image("small.png", 2, 2, SKColors.Blue);
        var mid = files.Image("mid.jpeg", 40, 40, SKColors.Green, SKEncodedImageFormat.Jpeg);
        var result = ImageImporter.Load([big, small, mid]);
        Assert.Equal(new[] { "big", "small", "mid" }, result.Images.Select(i => i.Name));
    }

    [Fact] // spec §3: SVG is dropped with a message, even when the bytes are a PNG; never read
    public void Svg_is_refused_by_extension()
    {
        using var files = new TestFiles();
        var path = files.Write("logo.svg", File.ReadAllBytes(files.Image("x.png", 2, 2, SKColors.Red)));
        var result = ImageImporter.Load([path]);
        Assert.Empty(result.Images);
        Assert.Equal(new ImportFailure("logo.svg", ImageImporter.SvgReason), Assert.Single(result.Failures));
        Assert.Equal("SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF.", ImageImporter.SvgReason);
    }

    [Theory] // isImageFile skipped these with a console line (editor.js:1000); v5 names them
    [InlineData("notes.txt")]
    [InlineData("noext")]
    [InlineData("photo.heic")]
    public void Other_extensions_are_refused_without_reading(string name)
    {
        var result = ImageImporter.Load([Path.Combine(Path.GetTempPath(), "rbx15-does-not-exist", name)]);
        Assert.Equal(new ImportFailure(name, ImageImporter.UnsupportedReason), Assert.Single(result.Failures));
    }

    [Fact]
    public void Extensions_match_case_insensitively()
    {
        using var files = new TestFiles();
        var result = ImageImporter.Load([files.Image("UP.PNG", 3, 3, SKColors.Red)]);
        Assert.Equal("UP", Assert.Single(result.Images).Name);
    }

    [Fact] // diag-console-only-errors: a damaged file is reported, not swallowed
    public void A_damaged_image_is_reported()
    {
        using var files = new TestFiles();
        var result = ImageImporter.Load([files.Write("broken.png", [1, 2, 3, 4, 5])]);
        Assert.Equal(new ImportFailure("broken.png", ImageImporter.UndecodableReason), Assert.Single(result.Failures));
    }

    [Fact] // image-decode-caps: a file over 100 MiB fails on its length, before it is read
    public void An_oversized_file_is_refused_before_reading()
    {
        using var files = new TestFiles();
        var path = Path.Combine(files.Dir, "huge.png");
        using (var fs = new FileStream(path, FileMode.CreateNew)) fs.SetLength(Images.MaxEncodedBytes + 1);
        var failure = Assert.Single(ImageImporter.Load([path]).Failures);
        Assert.Equal("huge.png", failure.FileName);
        Assert.StartsWith("The file is 100 MB;", failure.Reason);
    }

    [Fact] // image-decode-caps: the header's size is refused before pixels are allocated
    public void A_decompression_bomb_is_refused_from_its_header()
    {
        using var files = new TestFiles();
        var failure = Assert.Single(ImageImporter.Load([files.Write("bomb.png", TestFiles.PngHeader(30000, 30000))]).Failures);
        Assert.Equal(new ImportFailure("bomb.png", "The image is 30000 x 30000 pixels; the limit is 16384 pixels on a side."), failure);
    }

    [Fact]
    public void A_missing_file_is_reported_as_unreadable()
    {
        var result = ImageImporter.Load([Path.Combine(Path.GetTempPath(), "rbx15-does-not-exist", "gone.png")]);
        Assert.Equal(new ImportFailure("gone.png", ImageImporter.ReadFailedReason), Assert.Single(result.Failures));
    }
}
