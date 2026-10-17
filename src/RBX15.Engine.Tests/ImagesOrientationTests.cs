using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// A phone JPEG stores its pixels sideways plus an EXIF orientation. Chromium draws an img element
/// with that orientation applied (image-orientation: from-image), so 4.1 showed it upright and fit
/// it by its displayed size. The source is 32x16: a white 16x8 block in the stored top-left corner,
/// black elsewhere (luma only, block-aligned, so JPEG keeps it clean at quality 100).
/// </summary>
public class ImagesOrientationTests
{
    static byte[] JpegWithOrientation(int orientation)
    {
        using var src = Px.Render(32, 16, c =>
        {
            c.Clear(SKColors.Black);
            using var white = new SKPaint { Color = SKColors.White };
            c.DrawRect(SKRect.Create(0, 0, 16, 8), white);
        });
        using var data = src.Encode(SKEncodedImageFormat.Jpeg, 100);
        var jpeg = data.ToArray();
        // APP1 "Exif\0\0", big-endian TIFF, one IFD entry: 0x0112 Orientation, SHORT, count 1.
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    [Theory] // EXIF 1..8: displayed size, and the displayed corner the stored top-left lands in
    [InlineData(1, 32, 16, "TL")]
    [InlineData(2, 32, 16, "TR")]
    [InlineData(3, 32, 16, "BR")]
    [InlineData(4, 32, 16, "BL")]
    [InlineData(5, 16, 32, "TL")]
    [InlineData(6, 16, 32, "TR")]
    [InlineData(7, 16, 32, "BR")]
    [InlineData(8, 16, 32, "BL")]
    public void Exif_orientation_is_applied_like_chromium(int orientation, int w, int h, string corner)
    {
        using var img = Images.Decode(JpegWithOrientation(orientation));
        Assert.Equal((w, h), (img.Width, img.Height));
        // 3 px in from each corner: the white block is 16x8 or 8x16, so only its corner reads white.
        var probes = new Dictionary<string, (int X, int Y)>
        {
            ["TL"] = (3, 3), ["TR"] = (w - 4, 3), ["BR"] = (w - 4, h - 4), ["BL"] = (3, h - 4),
        };
        foreach (var (name, (x, y)) in probes)
        {
            var px = Px.At(img, x, y);
            if (name == corner) Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200, $"{name} should be white, got {px}");
            else Assert.True(px.Red < 55 && px.Green < 55 && px.Blue < 55, $"{name} should be black, got {px}");
        }
    }

    [Theory] // the thumbnail is oriented like the full decode (M4a Task 2): EXIF 6 turns 32x16 into 16x32, white top-right
    [InlineData(1, 16, 8, "TL")]
    [InlineData(6, 8, 16, "TR")]
    public void A_thumbnail_applies_the_exif_orientation(int orientation, int w, int h, string corner)
    {
        using var img = Images.DecodeThumbnail(JpegWithOrientation(orientation), 16);
        Assert.Equal((w, h), (img.Width, img.Height));
        var (x, y) = corner == "TL" ? (1, 1) : (w - 2, 1);
        var px = Px.At(img, x, y);
        Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200, $"{corner} should be white, got {px}");
        var far = Px.At(img, corner == "TL" ? w - 2 : 1, h - 2);
        Assert.True(far.Red < 55, $"the opposite corner should be black, got {far}");
    }
}
