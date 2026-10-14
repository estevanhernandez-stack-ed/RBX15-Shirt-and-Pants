using System.Globalization;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>Color handling from lib/fills.js and the canvas fillStyle rules 4.1 relies on.</summary>
public static class CssColor
{
    /// <summary>hexToRgb: #rgb or #rrggbb; unparsable reads as black (JS parseInt -> NaN -> 0).</summary>
    public static (int R, int G, int B) HexToRgb(string? hex)
    {
        var h = (hex ?? "#000000").Replace("#", "");
        if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
        if (!int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int n)) n = 0;
        return ((n >> 16) & 255, (n >> 8) & 255, n & 255);
    }

    /// <summary>JS Math.round rounds .5 up; values here are non-negative, so floor(v + 0.5).</summary>
    static string RgbToHex(double r, double g, double b)
    {
        static string To(double v) => ((int)Math.Max(0, Math.Min(255, Math.Floor(v + 0.5)))).ToString("x2", CultureInfo.InvariantCulture);
        return "#" + To(r) + To(g) + To(b);
    }

    public static string Mix(string a, string b, double t)
    {
        var x = HexToRgb(a);
        var y = HexToRgb(b);
        return RgbToHex(x.R + (y.R - x.R) * t, x.G + (y.G - x.G) * t, x.B + (y.B - x.B) * t);
    }

    public static SKColor ToSk(string hex)
    {
        var (r, g, b) = HexToRgb(hex);
        return new SKColor((byte)r, (byte)g, (byte)b);
    }

    /// <summary>The subset of CSS colors 4.1 ever assigns to fillStyle: #rgb, #rrggbb, transparent.</summary>
    public static bool TryParseCanvasFill(string? s, out SKColor color)
    {
        color = SKColors.Transparent;
        if (s is null) return false;
        if (s == "transparent") return true;
        if (s.Length is not (4 or 7) || s[0] != '#') return false;
        foreach (char ch in s.AsSpan(1))
            if (!Uri.IsHexDigit(ch)) return false;
        color = ToSk(s);
        return true;
    }
}
