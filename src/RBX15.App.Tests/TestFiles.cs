using System.Buffers.Binary;
using System.IO;
using System.Text;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>A temp folder of image files for one test; deleted on dispose.</summary>
sealed class TestFiles : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "rbx15-app-tests", Guid.NewGuid().ToString("N"));

    public TestFiles() => Directory.CreateDirectory(Dir);

    public string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(Dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A solid PNG, JPEG or WebP of the given size, encoded by Skia.</summary>
    public string Image(string name, int w, int h, SKColor color, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(color);
        using var image = surface.Snapshot();
        using var data = image.Encode(format, 100);
        return Write(name, data.ToArray());
    }

    /// <summary>A 2x1 24-bit BMP: red then blue (Skia has no BMP encoder).</summary>
    public static byte[] Bmp2x1()
    {
        var b = new byte[62];
        b[0] = (byte)'B'; b[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(2), 62);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(22), 1);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(28), 24);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(34), 8);
        byte[] row = [0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00]; // BGR red, BGR blue, pad
        row.CopyTo(b, 54);
        return b;
    }

    /// <summary>
    /// A 1x1 two-frame GIF: frame 1 red, frame 2 blue (Skia has no GIF encoder). Each frame's LZW
    /// stream is clear, the index, end (min code size 2).
    /// </summary>
    public static byte[] Gif1x1TwoFrames() =>
    [
        .. "GIF89a"u8,
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,       // 1x1, global table of 2
        0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF,             // red, blue
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // frame 1 control
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x44, 0x01, 0x00,                   // index 0
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // frame 2 control
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x4C, 0x01, 0x00,                   // index 1
        0x3B,
    ];

    /// <summary>A PNG whose IHDR claims width x height, with a token IDAT: a decompression bomb's header.</summary>
    public static byte[] PngHeader(int width, int height)
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
        s.Write(four);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(typed);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in typed)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(four, ~crc);
        s.Write(four);
    }

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); }
        catch (IOException) { }
    }
}
