using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Decode caps for untrusted images (divergence image-decode-caps; 4.1 had none). Over-cap headers
/// are hand-built PNGs: a real IHDR with any size and a token IDAT, so the codec reads the size
/// without a single pixel being allocated.
/// </summary>
public class ImagesCapsTests
{
    /// <summary>A PNG whose IHDR claims width x height (8-bit RGBA) followed by a token IDAT and IEND.</summary>
    internal static byte[] PngHeader(int width, int height)
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(len, Crc32(typed));
        s.Write(len);
    }

    static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    static byte[] RealPng(int w, int h)
    {
        using var img = Px.Solid(w, h, SKColors.Lime);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact] // the caps and their rationale live in Images (interface sheet §3.4)
    public void The_caps_are_100_MiB_16384_a_side_and_50_megapixels()
    {
        Assert.Equal(104_857_600L, Images.MaxEncodedBytes);
        Assert.Equal(16384, Images.MaxDimension);
        Assert.Equal(50_000_000L, Images.MaxPixels);
    }

    [Fact]
    public void The_encoded_size_cap_is_inclusive_and_names_the_size()
    {
        Images.CheckEncodedSize(Images.MaxEncodedBytes);
        var e = Assert.Throws<ImageTooLargeException>(() => Images.CheckEncodedSize(157_286_400));
        Assert.Equal("The file is 150 MB; the limit is 100 MB.", e.Message);
    }

    [Fact] // Decode checks the payload length before copying or parsing it
    public void Decode_refuses_a_payload_over_the_size_cap_before_parsing_it()
    {
        var e = Assert.Throws<ImageTooLargeException>(() => Images.Decode(new byte[Images.MaxEncodedBytes + 1]));
        Assert.StartsWith("The file is 100 MB;", e.Message);
    }

    [Theory] // MaxDimension per side, MaxPixels overall; both inclusive
    [InlineData(16384, 1, true)]
    [InlineData(16385, 1, false)]
    [InlineData(1, 16385, false)]
    [InlineData(10000, 5000, true)]
    [InlineData(10000, 5001, false)]
    [InlineData(7072, 7072, false)]
    public void Dimensions_pass_up_to_each_cap(int w, int h, bool ok)
    {
        if (ok)
        {
            // Ruling 4: a passing row proves the decoded size, not just "doesn't throw".
            Images.CheckDimensions(w, h);
            using var decoded = Images.Decode(RealPng(w, h));
            Assert.Equal((w, h), (decoded.Width, decoded.Height));
        }
        else
        {
            Assert.Throws<ImageTooLargeException>(() => Images.CheckDimensions(w, h));
            // Same verdict from a header-only PNG, before any pixel memory exists.
            Assert.Throws<ImageTooLargeException>(() => Images.Decode(PngHeader(w, h)));
        }
    }

    [Fact] // a decompression bomb: a tiny file whose header claims a huge side
    public void A_side_over_the_cap_is_refused_from_the_header()
    {
        var bytes = PngHeader(20000, 20000);
        Assert.True(bytes.Length < 100);
        var e = Assert.Throws<ImageTooLargeException>(() => Images.Decode(bytes));
        Assert.Equal("The image is 20000 x 20000 pixels; the limit is 16384 pixels on a side.", e.Message);
    }

    [Fact] // under the side cap, over the pixel cap
    public void A_pixel_count_over_the_cap_is_refused_from_the_header()
    {
        var e = Assert.Throws<ImageTooLargeException>(() => Images.Decode(PngHeader(8000, 8000)));
        Assert.Equal("The image is 8000 x 8000 pixels (64 megapixels); the limit is 50 megapixels.", e.Message);
    }

    [Fact] // the side cap is inclusive on a real decode
    public void An_image_exactly_at_the_side_cap_decodes()
    {
        using var img = Images.Decode(RealPng(16384, 1));
        Assert.Equal((16384, 1), (img.Width, img.Height));
    }

    [Fact] // M4 inherits: ProjectIO drops an over-cap layer by name (ProjectIO.TryDecodeDataUrl)
    public void A_project_layer_over_the_cap_is_dropped_by_name()
    {
        string Url(byte[] png) => "data:image/png;base64," + Convert.ToBase64String(png);
        var json = new JsonObject
        {
            ["version"] = "4.0",
            ["mode"] = "Shirt",
            ["layers"] = new JsonArray
            {
                new JsonObject { ["id"] = 1, ["name"] = "Fine", ["imgSrc"] = Url(RealPng(2, 2)) },
                new JsonObject { ["id"] = 2, ["name"] = "Too wide", ["imgSrc"] = Url(RealPng(16385, 1)) },
            },
        };
        var load = ProjectIO.Read(json.ToJsonString());
        Assert.Equal(new[] { "Fine" }, load.Document.Layers.Select(l => l.Name));
        Assert.Equal(new[] { "Too wide" }, load.DroppedLayers);
    }
}
