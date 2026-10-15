using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The theme dictionary against styles.css itself (ledger theme-tokens-colors and
/// theme-tokens-shape-motion-type, keep: "Unit test that every token hex in the ResourceDictionary equals
/// styles.css values"). The :root block (styles.css:8-92) is parsed here, var() chains resolved, and every
/// token is either checked or listed in <see cref="NotChecked"/> with its reason, so a token added to
/// styles.css fails this test until the theme handles it.
/// </summary>
public partial class ThemeTokenTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    /// <summary>Tokens with no numeric check here, and why.</summary>
    static readonly Dictionary<string, string> NotChecked = new()
    {
        ["brand-gradient-glow"] = "radial pair; no M3a surface uses it",
        ["shadow-1"] = "unused in 4.1 (no var(--shadow-1) in styles.css)",
        ["glow-cyan"] = "unused in 4.1",
        ["glow-magenta"] = "unused in 4.1",
        ["glow-duo"] = "two stacked shadows; WPF keeps the cyan layer (GlowDuoEffect); tuned visually (ledger theme-tokens-shape-motion-type)",
        ["inner-stroke"] = "unused in 4.1",
        ["font-display"] = "checked by family in Ui_fonts_resolve_to_the_vendored_files",
        ["font-body"] = "checked by family in Ui_fonts_resolve_to_the_vendored_files",
        ["font-mono"] = "checked by family in Ui_fonts_resolve_to_the_vendored_files",
    };

    [GeneratedRegex(@"--([a-z0-9-]+):\s*([^;]+);")]
    private static partial Regex TokenPattern();

    /// <summary>name to raw value, from the first :root block of styles.css.</summary>
    static Dictionary<string, string> CssTokens()
    {
        var css = File.ReadAllText(Path.Combine(Root, "styles.css"));
        int start = css.IndexOf(":root {", StringComparison.Ordinal);
        int end = css.IndexOf('}', start);
        return TokenPattern().Matches(css[start..end]).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    static string Resolve(Dictionary<string, string> tokens, string value)
    {
        var m = Regex.Match(value, @"^var\(--([a-z0-9-]+)\)$");
        return m.Success ? Resolve(tokens, tokens[m.Groups[1].Value]) : value;
    }

    /// <summary>"brand-navy-deep" to "BrandNavyDeep".</summary>
    static string Pascal(string name) =>
        string.Concat(name.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    /// <summary>#rrggbb, #rgb or rgba(r,g,b,a) to a WPF color; alpha byte = round(a * 255).</summary>
    static Color? CssColor(string v)
    {
        var hex = Regex.Match(v, "^#([0-9a-fA-F]{6})$");
        if (hex.Success)
        {
            uint rgb = uint.Parse(hex.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
        var rgba = Regex.Match(v, @"^rgba\((\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\)$");
        if (!rgba.Success) return null;
        double a = double.Parse(rgba.Groups[4].Value, CultureInfo.InvariantCulture);
        return Color.FromArgb((byte)Math.Round(a * 255, MidpointRounding.AwayFromZero),
                              byte.Parse(rgba.Groups[1].Value, CultureInfo.InvariantCulture),
                              byte.Parse(rgba.Groups[2].Value, CultureInfo.InvariantCulture),
                              byte.Parse(rgba.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    static object Resource(string key) => StaThread.Run(() => Application.Current.FindResource(key));

    static IEnumerable<string> ColorTokenNames()
    {
        var tokens = CssTokens();
        return tokens.Keys.Where(name => CssColor(Resolve(tokens, tokens[name])) is not null);
    }

    public static TheoryData<string> ColorTokens() => new(ColorTokenNames());

    [Theory, MemberData(nameof(ColorTokens))] // styles.css:10-66
    public void Each_color_token_is_a_color_and_a_brush_with_the_css_value(string token)
    {
        var tokens = CssTokens();
        var expected = CssColor(Resolve(tokens, tokens[token]))!.Value;
        var key = Pascal(token);
        Assert.Equal(expected, (Color)Resource(key + "Color"));
        Assert.Equal(expected, StaThread.Run(() => ((SolidColorBrush)Application.Current.FindResource(key + "Brush")).Color));
    }

    [Fact] // every :root token is checked somewhere, or listed with a reason
    public void Every_root_token_is_accounted_for()
    {
        var tokens = CssTokens();
        Assert.Equal(60, tokens.Count);
        var handled = new HashSet<string>(ColorTokenNames())
        {
            "brand-gradient", "brand-gradient-soft", "r-xs", "r-sm", "r-md", "r-lg", "r-pill",
            "shadow-2", "ease-out", "dur-fast", "dur-med",
        };
        handled.UnionWith(NotChecked.Keys);
        Assert.Empty(tokens.Keys.Except(handled));
        Assert.Empty(handled.Except(tokens.Keys));
    }

    [Theory] // styles.css:22-23: 135deg, two stops at 0% and 100%
    [InlineData("brand-gradient", "BrandGradientBrush")]
    [InlineData("brand-gradient-soft", "BrandGradientSoftBrush")]
    public void Gradients_have_the_css_stops(string token, string key)
    {
        var tokens = CssTokens();
        var m = Regex.Match(tokens[token], @"^linear-gradient\(135deg, (.+) 0%, (.+) 100%\)$");
        Assert.True(m.Success, tokens[token]);
        var c0 = CssColor(Resolve(tokens, m.Groups[1].Value))!.Value;
        var c1 = CssColor(Resolve(tokens, m.Groups[2].Value))!.Value;
        var stops = StaThread.Run(() =>
        {
            var b = (LinearGradientBrush)Application.Current.FindResource(key);
            return (b.StartPoint, b.EndPoint, b.MappingMode, Stops: b.GradientStops.Select(s => (s.Offset, s.Color)).ToArray());
        });
        Assert.Equal((new Point(0, 0), new Point(1, 1), BrushMappingMode.RelativeToBoundingBox), (stops.StartPoint, stops.EndPoint, stops.MappingMode));
        Assert.Equal([(0.0, c0), (1.0, c1)], stops.Stops);
    }

    [Theory] // styles.css:69-73
    [InlineData("r-xs", "RXsRadius")]
    [InlineData("r-sm", "RSmRadius")]
    [InlineData("r-md", "RMdRadius")]
    [InlineData("r-lg", "RLgRadius")]
    [InlineData("r-pill", "RPillRadius")]
    public void Radii_match(string token, string key)
    {
        double px = double.Parse(CssTokens()[token].TrimEnd('p', 'x'), CultureInfo.InvariantCulture);
        Assert.Equal(new CornerRadius(px), (CornerRadius)Resource(key));
    }

    [Theory] // styles.css:85-86
    [InlineData("dur-fast", "DurFastDuration")]
    [InlineData("dur-med", "DurMedDuration")]
    public void Durations_match(string token, string key)
    {
        double ms = double.Parse(CssTokens()[token].TrimEnd('m', 's'), CultureInfo.InvariantCulture);
        Assert.Equal(new Duration(TimeSpan.FromMilliseconds(ms)), (Duration)Resource(key));
    }

    [Fact] // styles.css:84, cubic-bezier(.2,.7,.2,1)
    public void The_ease_is_the_css_cubic_bezier()
    {
        Assert.Equal("cubic-bezier(.2,.7,.2,1)", CssTokens()["ease-out"]);
        var points = StaThread.Run(() =>
        {
            var k = (KeySpline)Application.Current.FindResource("EaseOutSpline");
            return (k.ControlPoint1, k.ControlPoint2);
        });
        Assert.Equal((new Point(0.2, 0.7), new Point(0.2, 1)), points);
    }

    [Fact] // styles.css:77, shadow-2: 0 4px 16px rgba(0,0,0,.45)
    public void Shadow_2_is_offset_4_blur_16_at_45_percent_black()
    {
        Assert.Equal("0 4px 16px rgba(0,0,0,.45)", CssTokens()["shadow-2"]);
        var e = StaThread.Run(() =>
        {
            var s = (DropShadowEffect)Application.Current.FindResource("Shadow2Effect");
            return (s.Color, s.Direction, s.ShadowDepth, s.BlurRadius, s.Opacity);
        });
        Assert.Equal((Colors.Black, 270.0, 4.0, 16.0, 0.45), e);
    }

    [Fact] // theme-scrollbars: thumb #223a54 on track #050c18 (styles.css:115)
    public void Scrollbars_are_navy_soft_on_ink_950()
    {
        Assert.Contains("scrollbar-color: var(--brand-navy-soft) var(--ink-950)", File.ReadAllText(Path.Combine(Root, "styles.css")));
        var (track, thumb) = StaThread.Run(() =>
        {
            var bar = new System.Windows.Controls.Primitives.ScrollBar { Orientation = System.Windows.Controls.Orientation.Vertical, Height = 100 };
            var host = new System.Windows.Controls.Grid { Children = { bar } };
            host.Measure(new Size(50, 100));
            host.Arrange(new Rect(0, 0, 50, 100));
            var thumbBorder = FindVisual<System.Windows.Controls.Border>(bar, b => b.TemplatedParent is System.Windows.Controls.Primitives.Thumb);
            return (((SolidColorBrush)bar.Background).Color, ((SolidColorBrush)thumbBorder!.Background).Color);
        });
        Assert.Equal(Color.FromRgb(0x05, 0x0c, 0x18), track);
        Assert.Equal(Color.FromRgb(0x22, 0x3a, 0x54), thumb);
    }

    static T? FindVisual<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && match(t)) return t;
            if (FindVisual(child, match) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// Review Focus candidate: a wrong pack URI makes WPF silently draw a system face. Each UI family must
    /// resolve to its embedded vendored file. Space Grotesk and JetBrains Mono expose no 600 instance to
    /// WPF, so the CSS weight 600 (tabs, labels, h1) draws their Bold (700) instance; Inter has a real
    /// SemiBold. Measured 2026-10-14 against assets/fonts at google/fonts@66928797.
    /// </summary>
    [Theory]
    [InlineData("FontDisplay", "Space Grotesk", "SpaceGrotesk.ttf", 600, 700)]
    [InlineData("FontBody", "Inter", "Inter.ttf", 400, 400)]
    [InlineData("FontBody", "Inter", "Inter.ttf", 600, 600)]
    [InlineData("FontMono", "JetBrains Mono", "JetBrainsMono.ttf", 400, 400)]
    [InlineData("FontMono", "JetBrains Mono", "JetBrainsMono.ttf", 600, 700)]
    public void Ui_fonts_resolve_to_the_vendored_files(string key, string family, string file, int cssWeight, int drawnWeight)
    {
        var css = CssTokens()[key switch { "FontDisplay" => "font-display", "FontBody" => "font-body", _ => "font-mono" }];
        Assert.StartsWith("'" + family + "'", css);
        var (uri, weight, sims) = StaThread.Run(() =>
        {
            var ff = (FontFamily)Application.Current.FindResource(key);
            var tf = new Typeface(ff, FontStyles.Normal, FontWeight.FromOpenTypeWeight(cssWeight), FontStretches.Normal);
            Assert.True(tf.TryGetGlyphTypeface(out var g), "no glyph typeface for " + key);
            return (g.FontUri.ToString(), g.Weight.ToOpenTypeWeight(), g.StyleSimulations);
        });
        Assert.EndsWith("/fonts/" + file.ToLowerInvariant(), uri.ToLowerInvariant());
        Assert.Equal(drawnWeight, weight);
        Assert.Equal(StyleSimulations.None, sims);
    }
}
