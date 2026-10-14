using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// renderWordArt layout and Add to canvas (editor.js:2120-2291). The widths, middle offsets and
/// canvas sizes are what 4.1 itself measured and returned for these settings: renderWordArt and
/// measureText run in the real editor.html under Electron 41 with the vendored fonts (the goldens-v2
/// capture setup), 2026-10-14.
/// </summary>
public class WordArtTests
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa), Black = SKColors.Black, Pink = new(0xf2, 0x2f, 0x89);

    static WordArtSettings S(string font = "Anton", WordArtStyle style = WordArtStyle.Solid, string text = "RIZZ", float size = 64,
                             float strokeWidth = 3, float angle = 0, SKColor? stroke = null) =>
        new(text, font, style, size, Cyan, stroke ?? Black, Pink, strokeWidth, angle);

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

    [Theory]
    [InlineData("Anton", 64, "RIZZ", 97.5)]
    [InlineData("Poppins", 64, "RIZZ", 144.128)]
    [InlineData("Dancing Script", 64, "RIZZ", 158.272)] // includes the Z-Z GPOS kern of -6 units at wght 700
    [InlineData("Orbitron", 72, "NO", 119.52)]
    [InlineData("Orbitron", 72, "CAP", 176.328)]
    public void Lines_are_shaped_to_chromiums_measureText_width(string font, float size, string text, double width) =>
        Assert.Equal(width, WordArt.Shape(font, size, text).Width, 3);

    [Fact]
    public void Shaping_from_many_threads_at_once_gives_the_single_threaded_result()
    {
        // xUnit runs test classes in parallel, and the golden gates shape Word Art from several at once.
        // HarfBuzzSharp's Blob.FromStream is not safe there: concurrent shapes read another font's bytes
        // (wrong glyph ids, .notdef boxes in a golden) or freed memory (0xC0000005 in hb_shape_full).
        string[] fonts = { "Anton", "Poppins", "Dancing Script", "Orbitron" };
        static string Signature(WordArt.ShapedLine line) =>
            string.Join("|", line.Runs.Select(r => string.Join(",", r.Glyphs) + "@" + string.Join(",", r.Xs))) + "=" + line.Width;
        var expected = fonts.ToDictionary(f => f, f => Signature(WordArt.Shape(f, 64, "RIZZ")));
        var wrong = new System.Collections.Concurrent.ConcurrentBag<string>();
        var threads = Enumerable.Range(0, 16).Select(i => new Thread(() =>
        {
            for (int k = 0; k < 1000; k++)
            {
                var font = fonts[(i + k) % fonts.Length];
                var got = Signature(WordArt.Shape(font, 64, "RIZZ"));
                if (got != expected[font]) wrong.Add($"{font}: {got}");
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());
        Assert.Empty(wrong);
    }

    [Fact]
    public void Plain_text_shapes_as_one_run_in_the_vendored_face()
    {
        var line = WordArt.Shape("Dancing Script", 64, "RIZZ");
        var run = Assert.Single(line.Runs);
        Assert.True(run.Primary);
        Assert.Same(Fonts.Get("Dancing Script", WordArt.Weight), run.Typeface);
        Assert.Equal(4, run.Glyphs.Length);
    }

    [Fact]
    public void A_character_the_family_lacks_is_shaped_in_a_system_fallback_face()
    {
        // 4.1's font string ends in ", sans-serif" (editor.js:2131), so Chromium draws a character the
        // family lacks in a system face, never the family's .notdef glyph (id 0).
        var a = WordArt.Shape("Anton", 64, "A");
        var line = WordArt.Shape("Anton", 64, "A\U0001F525");
        Assert.Equal(2, line.Runs.Count);
        Assert.True(line.Runs[0].Primary);
        Assert.Equal("Anton", line.Runs[0].Typeface.FamilyName);
        var fire = line.Runs[1];
        Assert.False(fire.Primary);
        Assert.NotEqual("Anton", fire.Typeface.FamilyName);
        using (var fallback = new SKFont(fire.Typeface))
            Assert.True(fallback.ContainsGlyph(0x1F525));
        Assert.All(line.Runs, r => Assert.DoesNotContain((ushort)0, r.Glyphs));
        Assert.Equal(a.Width, fire.Xs[0], 3); // the fallback run continues the pen
        Assert.True(line.Width > a.Width);
    }

    [Theory]
    [InlineData(0x03B1)] // GREEK SMALL LETTER ALPHA
    [InlineData(0x2665)] // BLACK HEART SUIT
    public void A_character_the_family_lacks_tries_arial_before_system_fallback(int codepoint)
    {
        // 4.1's font string is '900 64px "Anton", sans-serif' (editor.js:2131), and Chromium on Windows
        // resolves the generic sans-serif to Arial, so a character Anton lacks draws in Arial when Arial
        // has it; only then does the system fallback (MatchCharacter) choose. Both characters are
        // checked missing from the vendored Anton (assets/fonts/anton/Anton-Regular.ttf).
        var style = new SKFontStyle(SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using (var anton = new SKFont(Fonts.Get("Anton", WordArt.Weight)))
            Assert.False(anton.ContainsGlyph(codepoint));
        var line = WordArt.Shape("Anton", 64, "A" + char.ConvertFromUtf32(codepoint));
        Assert.Equal(2, line.Runs.Count);
        var run = line.Runs[1];
        Assert.False(run.Primary);
        Assert.All(line.Runs, r => Assert.DoesNotContain((ushort)0, r.Glyphs));

        // Typefaces from the font manager are shared wrappers; the test never disposes them.
        var arial = SKFontManager.Default.MatchFamily("Arial", style);
        bool arialHas = false;
        if (arial is { FamilyName: "Arial" })
            using (var probe = new SKFont(arial))
                arialHas = probe.ContainsGlyph(codepoint);
        if (arialHas)
        {
            Assert.Equal("Arial", run.Typeface.FamilyName);
        }
        else
        {
            // No Arial with this character on this machine: the system fallback decides.
            var system = SKFontManager.Default.MatchCharacter("Anton", style, null, codepoint);
            Assert.Equal(system?.FamilyName, run.Typeface.FamilyName);
        }
    }

    [Theory]
    [InlineData("Anton", 64, 18.015625)]
    [InlineData("Poppins", 64, 16)]
    [InlineData("Dancing Script", 64, 17.0625)]
    [InlineData("Orbitron", 72, 22.046875)]
    public void Middle_baseline_is_the_normalized_typo_metrics_in_layout_units(string font, float size, float offset) =>
        Assert.Equal(offset, WordArt.MiddleOffset(Fonts.Get(font, WordArt.Weight), size));

    [Theory]
    [InlineData(WordArtStyle.Solid, 3, 10)]
    [InlineData(WordArtStyle.Gradient, 3, 10)]
    [InlineData(WordArtStyle.Outline, 0, 10)]
    [InlineData(WordArtStyle.Neon, 3, 30)]
    [InlineData(WordArtStyle.Shadow, 3, 15)]
    [InlineData(WordArtStyle.Retro, 3, 12)]
    [InlineData(WordArtStyle.Solid, 12, 24)]
    [InlineData(WordArtStyle.Neon, 20, 40)]
    public void Pad_is_the_largest_effect_allowance(WordArtStyle style, float strokeWidth, double pad) =>
        Assert.Equal(pad, WordArt.Pad(style, strokeWidth));

    [Theory]
    [InlineData("Anton", WordArtStyle.Solid, "RIZZ", 64, 3, 118, 94)]
    [InlineData("Anton", WordArtStyle.Neon, "RIZZ", 64, 3, 158, 134)]
    [InlineData("Anton", WordArtStyle.Shadow, "RIZZ", 64, 3, 128, 104)]
    [InlineData("Anton", WordArtStyle.Retro, "RIZZ", 64, 3, 122, 98)]
    [InlineData("Anton", WordArtStyle.Solid, "RIZZ", 64, 12, 146, 122)]
    [InlineData("Poppins", WordArtStyle.Solid, "RIZZ", 64, 3, 165, 94)]
    [InlineData("Dancing Script", WordArtStyle.Outline, "RIZZ", 64, 0, 179, 94)]
    [InlineData("Orbitron", WordArtStyle.Solid, "NO\nCAP", 72, 3, 197, 186)]
    public void Canvas_size_is_4_1s_cw_by_ch(string font, WordArtStyle style, string text, float size, float strokeWidth, int cw, int ch)
    {
        var (image, w, h) = WordArt.Render(S(font, style, text, size, strokeWidth));
        using (image)
        {
            Assert.Equal((cw, ch), (w, h));
            Assert.Equal((cw, ch), (image.Width, image.Height));
        }
    }

    [Fact]
    public void The_preview_clamps_the_canvas_and_scales_the_content()
    {
        var (image, w, h) = WordArt.Render(S("Poppins"), 200, 80);
        using (image)
        {
            Assert.Equal((165, 94), (w, h));
            Assert.Equal((165, 80), (image.Width, image.Height));
        }
    }

    [Fact]
    public void Negative_maxima_are_rejected() => Assert.Throws<ArgumentOutOfRangeException>(() => WordArt.Render(S(), -1, 80));

    [Fact]
    public void Empty_text_renders_TEXT() => Assert.Equal(RenderPixels(S(text: "TEXT")), RenderPixels(S(text: "")));

    [Theory]
    [InlineData(0, 64)]
    [InlineData(float.NaN, 64)]
    [InlineData(2, 8)]
    [InlineData(5000, 300)]
    [InlineData(64, 64)]
    public void Size_falls_back_to_64_and_clamps_to_8_through_300(float size, float effective) =>
        Assert.Equal(effective, WordArt.EffectiveSize(size));

    [Fact]
    public void Outline_with_stroke_width_0_draws_at_3() =>
        Assert.Equal(RenderPixels(S("Dancing Script", WordArtStyle.Outline, strokeWidth: 3)),
                     RenderPixels(S("Dancing Script", WordArtStyle.Outline, strokeWidth: 0)));

    [Fact] // editor.js:2226: strokeW 0 is fill only, so the stroke color cannot reach the pixels
    public void Solid_with_stroke_width_0_draws_no_stroke()
    {
        Assert.Equal(RenderPixels(S(strokeWidth: 0, stroke: SKColors.White)), RenderPixels(S(strokeWidth: 0, stroke: Black)));
        Assert.NotEqual(RenderPixels(S(strokeWidth: 3, stroke: SKColors.White)), RenderPixels(S(strokeWidth: 3, stroke: Black)));
    }

    [Fact]
    public void Neon_ignores_the_stroke_settings() =>
        Assert.Equal(RenderPixels(S(style: WordArtStyle.Neon, strokeWidth: 3, stroke: SKColors.White)),
                     RenderPixels(S(style: WordArtStyle.Neon, strokeWidth: 12, stroke: Black)));

    [Fact]
    public void The_angle_only_moves_the_gradient()
    {
        Assert.Equal(RenderPixels(S(angle: 0)), RenderPixels(S(angle: 135)));
        Assert.NotEqual(RenderPixels(S(style: WordArtStyle.Gradient, angle: 0)), RenderPixels(S(style: WordArtStyle.Gradient, angle: 135)));
    }

    [Fact]
    public void A_trailing_newline_adds_an_empty_line()
    {
        var (image, w, h) = WordArt.Render(S("Orbitron", text: "NO\n", size: 72));
        using (image)
            Assert.Equal((140, (int)Math.Ceiling(2 * 72 * 1.15 + 20)), (w, h)); // 119.52 + 20 wide; two lines of 82.8
    }

    [Theory]
    [InlineData("RIZZ", "Anton", "✨ RIZZ [Anton]")]
    [InlineData("", "Anton", "✨ TEXT [Anton]")]
    [InlineData("NO\nCAP", "Orbitron", "✨ NO\nCAP [Orbitron]")]
    [InlineData("HELLO WORLD THIS IS LONG TEXT", "Poppins", "✨ HELLO WORLD THIS IS  [Poppins]")]
    public void Layer_names_follow_4_1(string text, string font, string name) => Assert.Equal(name, WordArt.LayerName(text, font));

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Add_to_canvas_fits_85_percent_of_the_front_slot_and_centers(TemplateMode mode)
    {
        // Poppins RIZZ renders 165x94 (above). The front slot is 128x128 at (231, 74) in both modes, so
        // the scale is 128/165*0.85, X = 231 + (128 - 165*scale)/2 and Y = 74 + (128 - 94*scale)/2.
        var layer = WordArt.CreateLayer(S("Poppins"), mode, 7);
        Assert.Equal(7, layer.Id);
        Assert.Equal("✨ RIZZ [Poppins]", layer.Name);
        Assert.Equal((165, 94), (layer.Image.Width, layer.Image.Height));
        Assert.Equal(SKAlphaType.Unpremul, layer.Image.AlphaType);
        Assert.Equal(0.6593939393939394, layer.Scale, 12);
        Assert.Equal(240.6, layer.X, 9);
        Assert.Equal(107.00848484848485, layer.Y, 9);
        Assert.Equal((true, 1.0, 0.0, false, false), (layer.Visible, layer.Opacity, layer.Rotation, layer.FlipH, layer.FlipV));
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate));
        Assert.Equal((0, 0, 0), (layer.RShift, layer.GShift, layer.BShift));
    }
}
