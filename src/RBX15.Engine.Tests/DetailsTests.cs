using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Engine Details (editor.js:1834-2103). Coverage values are either analytic (a pixel fully inside
/// or fully outside a stroke) or were measured on the engine build that matches all 37 frozen
/// detail goldens with max-delta 0; each is explained by the geometry beside it.
/// </summary>
public class DetailsTests
{
    static readonly SKColor Ink = new(0x33, 0x33, 0x33); // #detailColor default, editor.html:179

    static DetailStyle Style(float width = 2, bool stitch = false) => new(Ink, width, stitch);

    /// <summary>The whole alpha plane, row-major, read once (Px.At decodes per call).</summary>
    static byte[] Alpha(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Alpha8, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        var a = new byte[image.Width * image.Height];
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                a[y * image.Width + x] = bmp.GetPixel(x, y).Alpha;
        return a;
    }

    static byte[] Canvas(DetailType type, DetailStyle style, TemplateMode mode = TemplateMode.Shirt)
    {
        using var img = Details.Render(type, mode, style);
        return Alpha(img);
    }

    static int At(byte[] canvas, int x, int y) => canvas[y * TemplateSize.Width + x];

    public static TheoryData<DetailType> AllTypes()
    {
        var data = new TheoryData<DetailType>();
        foreach (var t in Enum.GetValues<DetailType>()) data.Add(t);
        return data;
    }

    // ── Ids and names ────────────────────────────────────────────

    [Theory]
    [InlineData("collar-v", DetailType.CollarV, "V-Neck Collar")]
    [InlineData("collar-crew", DetailType.CollarCrew, "Crew Neck")]
    [InlineData("collar-scoop", DetailType.CollarScoop, "Scoop Neck")]
    [InlineData("collar-henley", DetailType.CollarHenley, "Henley Collar")]
    [InlineData("cuff-left", DetailType.CuffLeft, "Left Cuff")]
    [InlineData("cuff-right", DetailType.CuffRight, "Right Cuff")]
    [InlineData("cuffs-both", DetailType.CuffsBoth, "Both Cuffs")]
    [InlineData("hem", DetailType.Hem, "Hem Line")]
    [InlineData("seam-center", DetailType.SeamCenter, "Center Seam")]
    [InlineData("seam-sides", DetailType.SeamSides, "Side Seams")]
    [InlineData("pocket", DetailType.Pocket, "Chest Pocket")]
    [InlineData("placket", DetailType.Placket, "Button Placket")]
    [InlineData("shoulder-stripe", DetailType.ShoulderStripe, "Shoulder Stripe")]
    [InlineData("arm-bands", DetailType.ArmBands, "Arm Bands")]
    [InlineData("yoke", DetailType.Yoke, "Back Yoke")]
    [InlineData("full-detail", DetailType.FullDetail, "Full Detail Set")]
    public void Ids_map_to_types_and_4_1_layer_names(string id, DetailType type, string name)
    {
        // ids: editor.html:187-222 data-detail; names: editor.js:2082-2099
        Assert.Equal(type, Details.FromId(id));
        Assert.Equal(id, Details.Id(type));
        Assert.Equal(name, Details.LayerName(type));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hem")]
    [InlineData("collar")]
    [InlineData("full-set")]
    public void Unknown_or_miscased_ids_throw(string id)
    {
        // 4.1 looks ids up in DETAIL_GENERATORS by exact key (editor.js:2080).
        Assert.Throws<ArgumentException>(() => Details.FromId(id));
    }

    // ── The canvas and the layer ─────────────────────────────────

    [Fact]
    public void Detail_canvas_is_template_sized_kN32_premul_and_transparent_elsewhere()
    {
        using var img = Details.Render(DetailType.Yoke, TemplateMode.Shirt, Style());
        Assert.Equal((TemplateSize.Width, TemplateSize.Height), (img.Width, img.Height));
        Assert.Equal(SKImageInfo.PlatformColorType, img.ColorType);
        Assert.Equal(0, Px.At(img, 10, 10).Alpha);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Widths_that_are_not_finite_and_positive_are_refused(float width)
    {
        // Review Focus: #detailWidth only yields 1..8 (editor.html:181); Canvas2D would silently
        // ignore such a lineWidth, so the engine refuses it rather than guess.
        Assert.Throws<ArgumentOutOfRangeException>(() => Details.Create(DetailType.Hem, TemplateMode.Shirt, Style(width), 1));
    }

    [Fact]
    public void A_fully_transparent_color_draws_nothing_and_makes_no_layer()
    {
        // autocrop returns null and createDetailLayer returns before makeLayer (editor.js:1853-1854).
        Assert.Null(Details.Create(DetailType.FullDetail, TemplateMode.Shirt, new DetailStyle(SKColors.Transparent, 2, false), 1));
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Layer_is_the_autocropped_canvas_at_scale_1_with_the_4_1_name(DetailType type)
    {
        using var canvas = Details.Render(type, TemplateMode.Shirt, Style(8, stitch: true));
        var crop = Tools.Autocrop(canvas)!.Value;
        using var cropped = crop.Image;
        var layer = Details.Create(type, TemplateMode.Shirt, Style(8, stitch: true), 7)!;

        Assert.Equal(7, layer.Id);
        Assert.Equal(Details.LayerName(type), layer.Name);
        Assert.Equal((crop.X, crop.Y, 1.0), (layer.X, layer.Y, layer.Scale));
        Assert.Equal((cropped.Width, cropped.Height), (layer.Image.Width, layer.Image.Height));
        Assert.Equal(Alpha(cropped), Alpha(layer.Image));
        // Every other field is a makeLayer default (editor.js:134-142).
        Assert.Equal(new Layer { Id = 7, Image = layer.Image, Name = layer.Name, X = layer.X, Y = layer.Y }, layer);
    }

    [Theory]
    // The layer box at width 2: geometry, plus the round cap or half width (1), plus pad 10, clamped
    // (editor.js:144-170). CollarV: x 265-1-10 = 254, y 74-1-10 = 63, right 325 -> 335, w 82.
    [InlineData(DetailType.CollarV, 254, 63, 82, 50)]
    [InlineData(DetailType.CollarCrew, 252, 63, 278, 40)]
    [InlineData(DetailType.CollarScoop, 246, 63, 98, 38)]
    [InlineData(DetailType.CollarHenley, 252, 63, 278, 70)]
    [InlineData(DetailType.CuffLeft, 297, 464, 284, 22)]
    [InlineData(DetailType.CuffRight, 8, 464, 284, 22)]
    [InlineData(DetailType.CuffsBoth, 8, 464, 573, 22)]
    [InlineData(DetailType.Hem, 154, 187, 412, 22)]
    [InlineData(DetailType.SeamCenter, 284, 63, 22, 150)]
    [InlineData(DetailType.SeamSides, 220, 63, 346, 150)]
    [InlineData(DetailType.Pocket, 299, 95, 45, 46)]
    [InlineData(DetailType.Placket, 284, 67, 27, 142)]
    [InlineData(DetailType.ShoulderStripe, 206, 29, 177, 303)]
    [InlineData(DetailType.ArmBands, 8, 388, 573, 23)]
    [InlineData(DetailType.Yoke, 416, 91, 150, 23)]
    [InlineData(DetailType.FullDetail, 8, 63, 573, 423)]
    public void Layer_box_at_width_2(DetailType type, int x, int y, int w, int h)
    {
        var layer = Details.Create(type, TemplateMode.Shirt, Style(), 1)!;
        Assert.Equal((x, y, w, h), ((int)layer.X, (int)layer.Y, layer.Image.Width, layer.Image.Height));
    }

    // ── Geometry ─────────────────────────────────────────────────

    [Fact]
    public void Hem_runs_4_px_above_the_bottom_of_the_right_front_left_and_back_faces()
    {
        // editor.js:1948-1965: y = 74 + 128 - 4 = 198; width 2 covers rows 197 and 198 exactly.
        var c = Canvas(DetailType.Hem, Style());
        foreach (int x in new[] { 197, 295, 393, 491 }) // right, front, left, back face centers
        {
            Assert.Equal((255, 255), (At(c, x, 197), At(c, x, 198)));
            Assert.Equal((0, 0), (At(c, x, 196), At(c, x, 199)));
        }
        Assert.Equal(0, At(c, 295, 265)); // torso_down gets no line (the unused d, editor.js:1950)
    }

    [Fact]
    public void Cuffs_sit_8_px_above_the_limb_bottom_on_the_named_arm_only()
    {
        // editor.js:1923-1943: y = 355 + 128 - 8 = 475 on larm faces (x 308..570) only.
        var c = Canvas(DetailType.CuffLeft, Style());
        Assert.Equal((255, 255), (At(c, 340, 474), At(c, 340, 475)));
        Assert.Equal(0, At(c, 249, 475)); // rarm_front
    }

    [Fact]
    public void Both_cuffs_are_the_left_and_right_cuffs_on_one_canvas()
    {
        // editor.js:1944-1947. The two arms never overlap, so the union is exact.
        var both = Canvas(DetailType.CuffsBoth, Style(2, stitch: true));
        var left = Canvas(DetailType.CuffLeft, Style(2, stitch: true));
        var right = Canvas(DetailType.CuffRight, Style(2, stitch: true));
        for (int i = 0; i < both.Length; i++)
        {
            Assert.False(left[i] > 0 && right[i] > 0);
            Assert.Equal(Math.Max(left[i], right[i]), both[i]);
        }
    }

    [Fact]
    public void Stitch_is_a_2w_on_2w_dash_with_round_caps()
    {
        // editor.js:1849: setLineDash([4, 4]) at width 2. The hem's right-face line starts at x 165:
        // dash 165..169 (caps to 164..170), gap to 173 (next cap from 172). The solid line covers it all.
        var stitch = Canvas(DetailType.Hem, Style(2, stitch: true));
        var solid = Canvas(DetailType.Hem, Style(2));
        Assert.Equal(255, At(stitch, 166, 198));
        Assert.Equal((0, 0), (At(stitch, 170, 198), At(stitch, 171, 198)));
        Assert.Equal(255, At(stitch, 173, 198));
        Assert.Equal((255, 255), (At(solid, 170, 198), At(solid, 171, 198)));
    }

    [Fact]
    public void Fractional_coordinates_stay_fractional()
    {
        // JS doubles handed to the canvas as floats, never rounded to pixels.
        // Pocket left side x = 231 + 128*0.62 = 310.36, width 2: 309.36..311.36.
        var pocket = Canvas(DetailType.Pocket, Style());
        Assert.Equal(255, At(pocket, 310, 120));
        Assert.InRange(At(pocket, 309, 120), 1, 254);
        Assert.InRange(At(pocket, 311, 120), 1, 254);
        Assert.True(At(pocket, 309, 120) > At(pocket, 311, 120)); // 0.64 vs 0.36 coverage

        // Arm bands y = 355 + 128*0.35 = 399.8, width 2: 398.8..400.8.
        var bands = Canvas(DetailType.ArmBands, Style());
        Assert.Equal(255, At(bands, 40, 399));
        Assert.True(At(bands, 40, 398) > 0 && At(bands, 40, 398) < At(bands, 40, 400) && At(bands, 40, 400) < 255);

        // Yoke y = 74 + 128*0.22 = 102.16, width 1: 101.66..102.66, so two partial rows.
        var yoke = Canvas(DetailType.Yoke, Style(1));
        Assert.InRange(At(yoke, 480, 101), 1, 254);
        Assert.InRange(At(yoke, 480, 102), 1, 254);
        Assert.Equal((0, 0), (At(yoke, 480, 100), At(yoke, 480, 103)));
    }

    [Fact]
    public void Henley_buttons_have_radius_lineWidth_and_are_never_dashed()
    {
        // editor.js:1916-1921: r = c.lineWidth. At width 8 the first button is centered (295, 98):
        // pixel 301 (center 6.5 away) is inside radius 8 and past the placket line (291..299).
        var w8 = Canvas(DetailType.CollarHenley, Style(8));
        Assert.Equal(255, At(w8, 301, 98));
        Assert.Equal(0, At(w8, 303, 98)); // center 8.5 away

        // fill() ignores the dash: with Stitch the button at (295, 98) sits in a placket gap
        // (dash 92..96, gap to 100) and is still solid.
        var stitched = Canvas(DetailType.CollarHenley, Style(2, stitch: true));
        Assert.Equal((255, 255), (At(stitched, 294, 98), At(stitched, 295, 98)));
    }

    [Fact]
    public void Placket_buttons_have_radius_max_1_5_and_0_8_width()
    {
        // editor.js:2023-2029: at width 1, r = 1.5; the first button is centered (297.5, 82), between
        // the lines at 295 and 300. Pixel (297, 82) has its far corners 1.118 away: inside 1.5,
        // outside 0.8.
        var w1 = Canvas(DetailType.Placket, Style(1));
        Assert.Equal(255, At(w1, 297, 82));
        var stitched = Canvas(DetailType.Placket, Style(2, stitch: true));
        Assert.Equal(255, At(stitched, 297, 82));
    }

    // ── Pants (approved divergence details-pants-mode-behavior) ──

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Pants_draws_the_same_detail_on_the_equivalent_faces(DetailType type)
    {
        // 4.1 throws a TypeError in Pants mode (editor.js:1869 and every generator). v5 resolves
        // waist/rleg/lleg through Template.Slot, whose rectangles equal the shirt's (editor.js:20-60).
        var shirt = Details.Create(type, TemplateMode.Shirt, Style(8, stitch: true), 1)!;
        var pants = Details.Create(type, TemplateMode.Pants, Style(8, stitch: true), 1)!;
        Assert.Equal((shirt.X, shirt.Y, shirt.Name), (pants.X, pants.Y, pants.Name));
        Assert.Equal(Alpha(shirt.Image), Alpha(pants.Image));
    }
}
