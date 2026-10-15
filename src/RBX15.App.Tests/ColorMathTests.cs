namespace RBX15.App.Tests;

/// <summary>The themed color picker's math: HSV and RGB both ways, and the hex field (ledger fillstyle-color-pickers, fix).</summary>
public class ColorMathTests
{
    [Theory] // primaries, secondaries, grays and edge hues: the expected HSV, and back to the same bytes
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(255, 255, 0, 60, 1, 1)]
    [InlineData(0, 255, 255, 180, 1, 1)]
    [InlineData(255, 0, 255, 300, 1, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(255, 255, 255, 0, 0, 1)]
    [InlineData(128, 128, 128, 0, 0, 128 / 255.0)]
    [InlineData(255, 0, 1, 359.76470588235293, 1, 1)]   // just below 360
    [InlineData(255, 1, 0, 0.23529411764705882, 1, 1)]  // just above 0
    public void Rgb_to_hsv_and_back(byte r, byte g, byte b, double h, double s, double v)
    {
        var hsv = ColorMath.RgbToHsv(r, g, b);
        Assert.Equal(h, hsv.H, 9);
        Assert.Equal(s, hsv.S, 9);
        Assert.Equal(v, hsv.V, 9);
        Assert.Equal((r, g, b), ColorMath.HsvToRgb(hsv.H, hsv.S, hsv.V));
    }

    [Fact]
    public void Every_8_bit_gray_and_a_sweep_of_colors_round_trip_exactly()
    {
        for (int i = 0; i < 256; i++)
        {
            byte n = (byte)i;
            var hsv = ColorMath.RgbToHsv(n, n, n);
            Assert.Equal((n, n, n), ColorMath.HsvToRgb(hsv.H, hsv.S, hsv.V));
        }
        for (int r = 0; r < 256; r += 15)
            for (int g = 0; g < 256; g += 17)
                for (int b = 0; b < 256; b += 13)
                {
                    var hsv = ColorMath.RgbToHsv((byte)r, (byte)g, (byte)b);
                    Assert.Equal(((byte)r, (byte)g, (byte)b), ColorMath.HsvToRgb(hsv.H, hsv.S, hsv.V));
                }
    }

    [Theory] // hue 360 is hue 0, negatives wrap, and saturation and value clamp
    [InlineData(360, 1, 1, 255, 0, 0)]
    [InlineData(-120, 1, 1, 0, 0, 255)]
    [InlineData(720 + 120, 1, 1, 0, 255, 0)]
    [InlineData(0, 2, 1, 255, 0, 0)]
    [InlineData(0, -1, 1, 255, 255, 255)]
    [InlineData(0, 1, 1.5, 255, 0, 0)]
    [InlineData(200, 1, -0.5, 0, 0, 0)]
    public void Hsv_wraps_hue_and_clamps_the_rest(double h, double s, double v, byte r, byte g, byte b) =>
        Assert.Equal((r, g, b), ColorMath.HsvToRgb(h, s, v));

    [Fact]
    public void NaN_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorMath.HsvToRgb(double.NaN, 1, 1));

    [Theory]
    [InlineData("#17d4fa", "#17d4fa")]
    [InlineData("#17D4FA", "#17d4fa")]
    [InlineData("  #F22f89 ", "#f22f89")]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("#ABC", "#aabbcc")]
    [InlineData("#000", "#000000")]
    [InlineData("#fff", "#ffffff")]
    public void Hex_field_accepts_short_and_long_forms_in_any_case(string text, string hex) =>
        Assert.Equal(hex, ColorMath.NormalizeHex(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("17d4fa")]
    [InlineData("#17d4f")]
    [InlineData("#17d4fa0")]
    [InlineData("#17d4fz")]
    [InlineData("#ab")]
    [InlineData("#abcd")]
    [InlineData("# abc")]
    [InlineData("red")]
    public void Invalid_hex_is_ignored(string? text)
    {
        Assert.False(ColorMath.TryParseHex(text, out _));
        Assert.Null(ColorMath.NormalizeHex(text));
    }

    [Fact]
    public void ToHex_is_lowercase_rrggbb() => Assert.Equal("#0a0bff", ColorMath.ToHex(10, 11, 255));
}
