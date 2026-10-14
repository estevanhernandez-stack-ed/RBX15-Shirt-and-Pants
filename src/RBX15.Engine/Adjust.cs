using System.Runtime.CompilerServices;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// Layer adjustments (editor.js:173-234). 4.1 sets ctx.filter = 'brightness(B%) contrast(C%) saturate(S%)
/// hue-rotate(Hdeg)' whenever any adjustment, including RGB shift, is off default. Chromium applies the
/// four functions in order and clamps to [0,1] after each, so this builds one color filter per stage
/// instead of one multiplied matrix: 8-bit transfer tables for brightness and contrast (Blink's
/// FEComponentTransfer), Filter Effects spec matrices for saturate and hue-rotate (FEColorMatrix).
/// </summary>
public static class Adjust
{
    public static bool HasAdjustments(Layer l) =>
        l.Brightness != 100 || l.Contrast != 100 || l.Saturate != 100 || l.HueRotate != 0 ||
        l.RShift != 0 || l.GShift != 0 || l.BShift != 0;

    public static SKColorFilter FilterChain(double brightnessPct, double contrastPct, double saturatePct, double hueDeg)
    {
        float b = (float)(brightnessPct / 100), c = (float)(contrastPct / 100);
        var brightness = LinearTransfer(b, 0f);
        var contrast = LinearTransfer(c, -0.5f * c + 0.5f);
        var saturate = SKColorFilter.CreateColorMatrix(Saturate((float)(saturatePct / 100)));
        var hue = SKColorFilter.CreateColorMatrix(HueRotate(hueDeg));
        // CreateCompose(outer, inner) = outer(inner(x)): brightness runs first, hue-rotate last.
        return SKColorFilter.CreateCompose(hue, SKColorFilter.CreateCompose(saturate, SKColorFilter.CreateCompose(contrast, brightness)));
    }

    /// <summary>
    /// Blink builds brightness() and contrast() as FEComponentTransfer type linear, an 8-bit lookup
    /// table per color channel (fe_component_transfer.cc Linear): slope * i + 255 * intercept,
    /// evaluated in float32 because slope and intercept are float members, then widened to double,
    /// clamped to 0..255 and truncated by static_cast to uint8. Both the float32 evaluation and the
    /// truncation are observable: brightness(130%) maps 10 to 13 (1.3f * 10 rounds up to 13.0f, where
    /// double arithmetic truncates 12.9999995 to 12), and contrast(40%) maps 250 to 176.5, stored as
    /// 176 where a float matrix rounds to 177. Alpha is untouched.
    /// </summary>
    static SKColorFilter LinearTransfer(float slope, float intercept)
    {
        var table = new byte[256];
        var identity = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float v = slope * i + 255f * intercept;
            table[i] = (byte)Math.Clamp((double)v, 0.0, 255.0);
            identity[i] = (byte)i;
        }
        return SKColorFilter.CreateTable(identity, table, table, table);
    }

    static float[] Saturate(float s) => new[]
    {
        0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s, 0, 0,
        0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s, 0, 0,
        0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s, 0, 0,
        0, 0, 0, 1, 0,
    };

    static float[] HueRotate(double degrees)
    {
        float cos = (float)Math.Cos(degrees * Math.PI / 180), sin = (float)Math.Sin(degrees * Math.PI / 180);
        return new[]
        {
            0.213f + cos * 0.787f - sin * 0.213f, 0.715f - cos * 0.715f - sin * 0.715f, 0.072f - cos * 0.072f + sin * 0.928f, 0, 0,
            0.213f - cos * 0.213f + sin * 0.143f, 0.715f + cos * 0.285f + sin * 0.140f, 0.072f - cos * 0.072f - sin * 0.283f, 0, 0,
            0.213f - cos * 0.213f - sin * 0.787f, 0.715f - cos * 0.715f + sin * 0.715f, 0.072f + cos * 0.928f + sin * 0.072f, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    sealed record ShiftEntry(int R, int G, int B, SKImage Result);
    static readonly ConditionalWeakTable<SKImage, ShiftEntry> ShiftCache = new();

    /// <summary>
    /// applyRGBShift on exact un-premultiplied pixels: add per channel, clamp 0..255, skip alpha 0,
    /// leave alpha alone. Cached per source image. Images are immutable, so a pixel edit is a new
    /// image and can never be served a stale result (ledger rgb-shift-cache).
    /// </summary>
    public static SKImage ShiftRgb(SKImage source, int r, int g, int b)
    {
        if (ShiftCache.TryGetValue(source, out var hit) && hit.R == r && hit.G == g && hit.B == b) return hit.Result;
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!source.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("RGB shift: cannot read pixels");
        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.Alpha == 0) continue;
                bmp.SetPixel(x, y, new SKColor(Clamp(p.Red + r), Clamp(p.Green + g), Clamp(p.Blue + b), p.Alpha));
            }
        var result = SKImage.FromPixelCopy(bmp.PeekPixels());
        ShiftCache.AddOrUpdate(source, new ShiftEntry(r, g, b, result));
        return result;
    }
}
