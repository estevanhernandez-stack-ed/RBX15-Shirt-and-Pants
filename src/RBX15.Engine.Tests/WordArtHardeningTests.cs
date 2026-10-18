using System.Diagnostics;
using System.Text.RegularExpressions;
using RBX15.Parity;
using SkiaSharp;
using Xunit.Abstractions;

namespace RBX15.Engine.Tests;

/// <summary>
/// M4b item 8: the Word Art engine hardening. The edge-case table (editor.js:2121, :2125, :2217), the
/// 15-font list and its faces, the layer-name cut, the preview-path time, and the graceful cap (ruling T1,
/// 2026-10-18): text too big for the image caps renders inside them and the layer's scale makes up the
/// difference, with no error.
/// </summary>
public partial class WordArtHardeningTests(ITestOutputHelper output)
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa), Black = SKColors.Black, Pink = new(0xf2, 0x2f, 0x89);

    static WordArtSettings S(string text = "RIZZ", string font = "Anton", WordArtStyle style = WordArtStyle.Solid, float size = 64,
                             float strokeWidth = 3, float angle = 0) =>
        new(text, font, style, size, Cyan, Black, Pink, strokeWidth, angle);

    static byte[] Pixels(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixelSpan().ToArray();
    }

    static byte[] RenderPixels(WordArtSettings s)
    {
        using var image = WordArt.Render(s).Image;
        return Pixels(image);
    }

    // ---- the edge-case table -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(float.NaN, 0)]       // +v || 0
    [InlineData(-5, 0)]              // below the slider's minimum
    [InlineData(float.NegativeInfinity, 0)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(20, 20)]
    [InlineData(25, 20)]             // above the slider's maximum
    [InlineData(float.PositiveInfinity, 20)]
    public void Stroke_width_follows_4_1s_rule_then_clamps_to_0_through_20(float strokeWidth, float effective) =>
        Assert.Equal(effective, WordArt.EffectiveStrokeWidth(strokeWidth));

    [Theory]
    [InlineData(WordArtStyle.Solid)]
    [InlineData(WordArtStyle.Gradient)]
    [InlineData(WordArtStyle.Shadow)]
    [InlineData(WordArtStyle.Retro)]
    public void A_NaN_or_negative_stroke_width_draws_as_stroke_0(WordArtStyle style)
    {
        var none = RenderPixels(S(style: style, strokeWidth: 0));
        Assert.Equal(none, RenderPixels(S(style: style, strokeWidth: float.NaN)));
        Assert.Equal(none, RenderPixels(S(style: style, strokeWidth: -7)));
    }

    [Fact]
    public void Outline_with_a_NaN_or_negative_stroke_width_draws_at_3()
    {
        var three = RenderPixels(S("RIZZ", "Dancing Script", WordArtStyle.Outline, strokeWidth: 3));
        Assert.Equal(three, RenderPixels(S("RIZZ", "Dancing Script", WordArtStyle.Outline, strokeWidth: float.NaN)));
        Assert.Equal(three, RenderPixels(S("RIZZ", "Dancing Script", WordArtStyle.Outline, strokeWidth: -2)));
    }

    [Fact]
    public void A_stroke_width_above_20_renders_as_20_and_pads_for_20()
    {
        using var twenty = WordArt.Render(S(strokeWidth: 20)).Image;
        var (huge, w, h) = WordArt.Render(S(strokeWidth: 400));
        using (huge)
        {
            Assert.Equal((twenty.Width, twenty.Height), (w, h));
            Assert.Equal(Pixels(twenty), Pixels(huge));
        }
    }

    [Theory]
    [InlineData(0f, 64)]
    [InlineData(float.NaN, 64)]
    [InlineData(2f, 8)]
    [InlineData(5000f, 300)]
    public void Render_applies_the_size_rule_to_the_bitmap(float size, float effective)
    {
        using var asked = WordArt.Render(S(size: size)).Image;
        using var expected = WordArt.Render(S(size: effective)).Image;
        Assert.Equal((expected.Width, expected.Height), (asked.Width, asked.Height));
        Assert.Equal(Pixels(expected), Pixels(asked));
    }

    [Fact]
    public void Empty_text_names_the_layer_TEXT_and_renders_TEXT()
    {
        Assert.Equal("✨ TEXT [Anton]", WordArt.LayerName("", "Anton"));
        Assert.Equal(RenderPixels(S("TEXT")), RenderPixels(S("")));
    }

    [Fact]
    public void The_20_unit_name_cut_may_split_a_surrogate_pair()
    {
        // editor.js:2275 text.substring(0, 20) counts UTF-16 units. 19 letters then a fire emoji (2 units):
        // the cut keeps the high surrogate and drops the low one.
        var name = WordArt.LayerName(new string('A', 19) + "\U0001F525B", "Anton");
        Assert.Equal("✨ " + new string('A', 19) + "\uD83D" + " [Anton]", name);
        Assert.True(char.IsHighSurrogate(name[2 + 19]));
        Assert.False(char.IsLowSurrogate(name[2 + 20]));
        Assert.Equal(2 + 20 + " [Anton]".Length, name.Length);
    }

    [Fact]
    public void A_name_of_exactly_20_units_is_kept_whole() =>
        Assert.Equal("✨ " + new string('A', 18) + "\U0001F525" + " [Lato]", WordArt.LayerName(new string('A', 18) + "\U0001F525", "Lato"));

    // ---- the 15 fonts --------------------------------------------------------------------------------------------

    [GeneratedRegex("<option value=\"(?<family>[^\"]+)\">(?<label>[^<]+)</option>")]
    private static partial Regex OptionRow();

    [Fact]
    public void FontOptions_are_the_15_waFont_options_in_4_1s_order()
    {
        var html = File.ReadAllText(Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "editor.html")).Replace("\r\n", "\n");
        var start = html.IndexOf("id=\"waFont\"", StringComparison.Ordinal);
        var select = html[start..html.IndexOf("</select>", start, StringComparison.Ordinal)];
        var fromHtml = OptionRow().Matches(select).Select(m => (m.Groups["family"].Value, m.Groups["label"].Value)).ToList();

        Assert.Equal(15, WordArt.FontOptions.Count);
        Assert.Equal(fromHtml, WordArt.FontOptions);
        Assert.Equal(Fonts.WordArtFamilies, WordArt.FontOptions.Select(o => o.Family));
        Assert.Equal("Anton (Block)", WordArt.FontOptions[0].Label);
        Assert.Equal("Rubik Mono", WordArt.FontOptions[11].Label);
    }

    public static TheoryData<string> Families()
    {
        var data = new TheoryData<string>();
        foreach (var (family, _) in WordArt.FontOptions) data.Add(family);
        return data;
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Every_font_renders_RIZZ_in_its_vendored_face_with_no_fallback(string family)
    {
        var line = WordArt.Shape(family, 64, "RIZZ");
        var run = Assert.Single(line.Runs);
        Assert.True(run.Primary, $"{family}: RIZZ fell back to {run.Typeface.FamilyName}");
        Assert.Same(Fonts.Get(family, WordArt.Weight), run.Typeface);
        Assert.Equal(family, run.Typeface.FamilyName);
        Assert.Equal(4, run.Glyphs.Length);
        Assert.DoesNotContain((ushort)0, run.Glyphs);
        Assert.True(line.Width > 0);

        var (image, cw, ch) = WordArt.Render(S("RIZZ", family));
        using (image)
        {
            Assert.Equal((cw, ch), (image.Width, image.Height));
            Assert.Contains(Pixels(image), b => b != 0); // it drew something
        }
    }

    [Fact]
    public void Fonts_Get_from_many_threads_returns_one_typeface_per_face()
    {
        var results = new System.Collections.Concurrent.ConcurrentBag<SKTypeface>();
        Parallel.For(0, 64, _ => results.Add(Fonts.Get("Righteous", WordArt.Weight)));
        Assert.Single(results.Distinct());
    }

    // ---- the preview path ----------------------------------------------------------------------------------------

    [Fact]
    public void Forty_preview_renders_stay_under_50_ms_a_frame()
    {
        // The Text tab re-renders the 200x80 preview on every keystroke (M4b item 9). 40 keystrokes grow the
        // text one character at a time. A first render warms the font and shaping caches, as the app's first
        // preview does before the first frame.
        const string typed = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG!";
        WordArt.Render(S("R"), 200, 80).Image.Dispose();
        var times = new List<double>();
        for (int i = 1; i <= 40; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            var (image, _, _) = WordArt.Render(S(typed[..i]), 200, 80);
            times.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            image.Dispose();
        }
        double mean = times.Average();
        output.WriteLine($"40 preview renders: total {times.Sum():0.0} ms, mean {mean:0.00} ms, max {times.Max():0.00} ms");
        Assert.True(mean < 50, $"mean {mean:0.0} ms a frame");
    }

    // ---- ruling T1: degrade gracefully, never refuse --------------------------------------------------------------

    static void AssertInsideTheCaps(SKImage image)
    {
        Assert.InRange(image.Width, 1, Images.MaxDimension);
        Assert.InRange(image.Height, 1, Images.MaxDimension);
        Assert.True((long)image.Width * image.Height <= Images.MaxPixels, $"{image.Width} x {image.Height}");
    }

    [Theory]
    [InlineData(585, 585)]                       // the template
    [InlineData(16384, 16384 * 3 + 1)]           // already over, height only
    [InlineData(16384, 3051)]                    // 49.99 MP, fits
    [InlineData(16384, 3052)]                    // just over 50 MP
    [InlineData(20000, 100)]                     // width over
    [InlineData(100, 20000)]                     // height over
    [InlineData(12000, 6000)]                    // 72 MP, each side fine
    [InlineData(600000, 100)]                    // very wide
    [InlineData(int.MaxValue, int.MaxValue)]     // saturated
    public void FitToCaps_returns_the_largest_size_inside_the_caps(int width, int height)
    {
        var (w, h, factor) = WordArt.FitToCaps(width, height);
        Assert.InRange(w, 1, Images.MaxDimension);
        Assert.InRange(h, 1, Images.MaxDimension);
        Assert.True((long)w * h <= Images.MaxPixels);
        if (width <= Images.MaxDimension && height <= Images.MaxDimension && (long)width * height <= Images.MaxPixels)
        {
            Assert.Equal((width, height, 1.0), (w, h, factor)); // a render that fits is untouched
            return;
        }
        Assert.InRange(factor, double.Epsilon, 1.0);
        // Rounded up from the content's scaled size, so the content is never clipped, and never more than a pixel past it.
        Assert.InRange(w - width * factor, -1e-6, 1.0 + 1e-6);
        Assert.InRange(h - height * factor, -1e-6, 1.0 + 1e-6);
        // Largest: one more percent of scale would break a cap.
        double more = factor * 1.01;
        Assert.False(Math.Ceiling(width * more) <= Images.MaxDimension && Math.Ceiling(height * more) <= Images.MaxDimension
                     && Math.Ceiling(width * more) * Math.Ceiling(height * more) <= Images.MaxPixels,
                     $"{width} x {height} at {factor} could have been bigger");
    }

    static string LongText(int characters) => string.Concat(Enumerable.Repeat("RIZZ ", characters / 5));

    [Fact]
    public void A_2000_character_size_300_string_adds_one_layer_in_one_undo_step_at_the_requested_size()
    {
        var settings = S(LongText(2000), size: 300);
        Assert.Equal(2000, settings.Text.Length);

        // 4.1's numbers for this text, worked out without Render: cw = ceil(width + 2 * pad), ch = ceil(1.15 * 300 + 2 * pad).
        double pad = WordArt.Pad(WordArtStyle.Solid, 3);
        int cw = (int)Math.Ceiling(WordArt.Shape("Anton", 300, settings.Text).Width + pad * 2);
        int ch = (int)Math.Ceiling(300 * 1.15 + pad * 2);
        Assert.True(cw > Images.MaxDimension, $"cw {cw}: the string must outgrow the caps for this test to mean anything");

        var front = Template.Slot(TemplateMode.Shirt, BodyPart.Body, Face.Front);
        double uncappedScale = Math.Min((double)front.W / cw, (double)front.H / ch) * 0.85;

        Layer layer = null!;
        var thrown = Record.Exception(() => layer = WordArt.CreateLayer(settings, TemplateMode.Shirt, 1));
        Assert.Null(thrown);
        using var image = layer.Image;

        AssertInsideTheCaps(image);
        // On-canvas size = image size x layer scale = the uncapped size, to within the one image pixel the render rounds up by.
        Assert.Equal(cw * uncappedScale, image.Width * layer.Scale, layer.Scale + 1e-9);
        Assert.Equal(ch * uncappedScale, image.Height * layer.Scale, layer.Scale + 1e-9);
        Assert.Equal(front.X + (front.W - image.Width * layer.Scale) / 2, layer.X, 9);
        Assert.Equal(front.Y + (front.H - image.Height * layer.Scale) / 2, layer.Y, 9);
        Assert.True(layer.Scale > uncappedScale); // the shrunk bitmap needs a bigger scale
        Assert.Equal("✨ RIZZ RIZZ RIZZ RIZZ  [Anton]", layer.Name);
        Assert.Equal(SKAlphaType.Unpremul, image.AlphaType);
        using (var pixels = image.PeekPixels())
            Assert.True(pixels.GetPixelSpan().IndexOfAnyExcept((byte)0) >= 0, "the capped bitmap is blank");

        // One layer, one undo step: the engine's History records the before state once, and one undo removes the layer.
        var before = new Document { Mode = TemplateMode.Shirt };
        var after = before with { Layers = before.Layers.Add(layer) };
        var history = new History();
        history.Record(new EditorSnapshot(before, null));
        Assert.Single(after.Layers);
        Assert.Equal(1, history.UndoCount);
        var undone = history.Undo(new EditorSnapshot(after, layer.Id));
        Assert.NotNull(undone);
        Assert.Empty(undone.Document.Layers);
        Assert.False(history.CanUndo);
    }

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void A_string_wider_than_16384_renders_inside_the_caps_and_reports_its_scale(TemplateMode mode)
    {
        // 700 characters at size 100 is about 26,000 px wide and 140 tall: capped by the side, a cheap bitmap.
        var settings = S(LongText(700), size: 100);
        var (image, cw, ch, contentScale) = WordArt.Render(settings);
        using (image)
        {
            Assert.True(cw > Images.MaxDimension, $"cw {cw}");
            AssertInsideTheCaps(image);
            Assert.Equal(Images.MaxDimension, image.Width);
            Assert.Equal((double)Images.MaxDimension / cw, contentScale, 12);
            Assert.InRange(image.Height - ch * contentScale, 0, 1.0 + 1e-9);
        }

        var layer = WordArt.CreateLayer(settings, mode, 5);
        using var layerImage = layer.Image;
        AssertInsideTheCaps(layerImage);
        var front = Template.Slot(mode, BodyPart.Body, Face.Front);
        double uncappedScale = Math.Min((double)front.W / cw, (double)front.H / ch) * 0.85;
        Assert.Equal(cw * uncappedScale, layerImage.Width * layer.Scale, layer.Scale + 1e-9);
        Assert.Equal(ch * uncappedScale, layerImage.Height * layer.Scale, layer.Scale + 1e-9);
    }

    [Fact]
    public void A_2000_character_block_over_the_50_megapixel_cap_adds_one_layer_at_the_requested_size()
    {
        // 40 lines of 50 characters at size 300: each side is under 16384 but the area is about 77 MP, so the
        // pixel cap binds and the bitmap is the largest inside 50 MP.
        var settings = S(string.Join('\n', Enumerable.Repeat(string.Concat(Enumerable.Repeat("RIZZ ", 10)), 40)), size: 300);
        Assert.Equal(2000 + 39, settings.Text.Length);
        var front = Template.Slot(TemplateMode.Shirt, BodyPart.Body, Face.Front);

        var layer = WordArt.CreateLayer(settings, TemplateMode.Shirt, 9);
        using var image = layer.Image;
        var (rendered, cw, ch, contentScale) = WordArt.Render(settings);
        rendered.Dispose();
        Assert.True((long)cw * ch > Images.MaxPixels, $"{cw} x {ch} must be over the pixel cap for this test to mean anything");
        Assert.True(cw <= Images.MaxDimension && ch <= Images.MaxDimension);
        AssertInsideTheCaps(image);
        Assert.True((long)image.Width * image.Height > Images.MaxPixels * 0.99, "the cap should be used, not undershot");
        double uncappedScale = Math.Min((double)front.W / cw, (double)front.H / ch) * 0.85;
        Assert.Equal(cw * uncappedScale, image.Width * layer.Scale, layer.Scale + 1e-9);
        Assert.Equal(ch * uncappedScale, image.Height * layer.Scale, layer.Scale + 1e-9);
        Assert.True(contentScale < 1);
    }

    [Fact]
    public void A_text_taller_than_16384_renders_inside_the_caps()
    {
        var settings = S(string.Join('\n', Enumerable.Repeat("RIZZ", 120)), size: 150);
        var (image, cw, ch, contentScale) = WordArt.Render(settings);
        using (image)
        {
            Assert.True(ch > Images.MaxDimension, $"ch {ch}");
            AssertInsideTheCaps(image);
            Assert.Equal(Images.MaxDimension, image.Height);
            Assert.Equal((double)Images.MaxDimension / ch, contentScale, 12);
            Assert.InRange(image.Width - cw * contentScale, 0, 1.0 + 1e-9);
        }
    }

    [Theory]
    [InlineData(WordArtStyle.Gradient)]
    [InlineData(WordArtStyle.Neon)]
    [InlineData(WordArtStyle.Shadow)]
    [InlineData(WordArtStyle.Outline)]
    [InlineData(WordArtStyle.Retro)]
    public void Every_style_renders_over_the_cap_without_throwing(WordArtStyle style)
    {
        var (image, _, _, contentScale) = WordArt.Render(S(LongText(700), "Poppins", style, size: 100));
        using (image)
        {
            AssertInsideTheCaps(image);
            Assert.True(contentScale < 1);
            Assert.Contains(Pixels(image), b => b != 0);
        }
    }

    [Fact]
    public void Text_that_fits_is_untouched_by_the_cap()
    {
        var (image, cw, ch, contentScale) = WordArt.Render(S());
        using (image)
        {
            Assert.Equal(1.0, contentScale);
            Assert.Equal((cw, ch), (image.Width, image.Height));
        }
    }

    [Fact]
    public void The_preview_reports_the_scale_it_shrank_the_content_by()
    {
        var (image, cw, ch, contentScale) = WordArt.Render(S(font: "Poppins"), 200, 80);
        using (image)
            Assert.Equal(Math.Min(200.0 / cw, 80.0 / ch), contentScale, 12);
    }
}
