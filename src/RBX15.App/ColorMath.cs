using System.Globalization;

namespace RBX15.App;

/// <summary>
/// The color picker's pure math (ledger <c>fillstyle-color-pickers</c>, fix): HSV and 8-bit RGB both
/// ways, and the hex field's parsing. Hue is degrees in [0, 360) (360 reads as 0); saturation and value
/// are 0 to 1. Every 8-bit color survives RGB to HSV to RGB unchanged.
/// </summary>
public static class ColorMath
{
    /// <summary>8-bit RGB to HSV. A gray (no saturation) has hue 0; black has saturation 0.</summary>
    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double v = max / 255.0;
        double s = max == 0 ? 0 : delta / max;
        if (delta == 0) return (0, s, v);
        double h = max == r ? (g - b) / delta
                 : max == g ? 2 + (b - r) / delta
                 : 4 + (r - g) / delta;
        h *= 60;
        if (h < 0) h += 360;
        return (h, s, v);
    }

    /// <summary>HSV to 8-bit RGB, each channel rounded half away from zero. Out-of-range inputs are wrapped (hue) or clamped.</summary>
    public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        if (double.IsNaN(h) || double.IsNaN(s) || double.IsNaN(v)) throw new ArgumentOutOfRangeException(nameof(h), "NaN is not a color.");
        h %= 360;
        if (h < 0) h += 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        double c = v * s;
        double hp = h / 60;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        (double r, double g, double b) = (int)hp switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        double m = v - c;
        return (Channel(r + m), Channel(g + m), Channel(b + m));
    }

    /// <summary>
    /// Parses the hex field: <c>#rgb</c> or <c>#rrggbb</c>, any case, surrounding spaces ignored. Anything
    /// else (no <c>#</c>, other lengths, non-hex digits) returns false.
    /// </summary>
    public static bool TryParseHex(string? text, out (byte R, byte G, byte B) rgb)
    {
        rgb = default;
        if (text is null) return false;
        var t = text.AsSpan().Trim();
        if (t.Length is not (4 or 7) || t[0] != '#') return false;
        var digits = t[1..];
        if (digits.ContainsAnyExcept("0123456789abcdefABCDEF")) return false;
        if (digits.Length == 3)
        {
            rgb = (Short(digits[0]), Short(digits[1]), Short(digits[2]));
            return true;
        }
        rgb = (Byte(digits[0..2]), Byte(digits[2..4]), Byte(digits[4..6]));
        return true;
    }

    /// <summary>Lowercase <c>#rrggbb</c>, as an input of type color reports it.</summary>
    public static string ToHex(byte r, byte g, byte b) => string.Create(CultureInfo.InvariantCulture, $"#{r:x2}{g:x2}{b:x2}");

    /// <summary>The hex field's text as lowercase <c>#rrggbb</c>, or null when it does not parse.</summary>
    public static string? NormalizeHex(string? text) => TryParseHex(text, out var c) ? ToHex(c.R, c.G, c.B) : null;

    static byte Channel(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255, MidpointRounding.AwayFromZero);

    static byte Short(char digit)
    {
        int n = Convert.ToInt32(digit.ToString(), 16);
        return (byte)(n * 17);
    }

    static byte Byte(ReadOnlySpan<char> two) => byte.Parse(two, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
