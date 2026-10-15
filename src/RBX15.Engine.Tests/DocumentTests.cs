using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class DocumentTests
{
    static byte[] Png(SKColor c, int w = 1, int h = 1)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) bmp.SetPixel(x, y, c);
        using var img = SKImage.FromPixelCopy(bmp.PeekPixels());
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void Document_defaults_match_the_4_1_editor_state()
    {
        var d = new Document();
        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#192e44", d.BgColor);
        Assert.Equal(1, d.BgOpacity);
        Assert.Null(d.BgImage);
        Assert.Empty(d.RegionFills);
        Assert.Empty(d.Layers);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
    }

    [Fact]
    public void Layer_defaults_match_makeLayer()
    {
        var l = new Layer { Id = 1, Image = Images.Decode(Png(SKColors.Red)) };
        Assert.Equal("Layer", l.Name);
        Assert.True(l.Visible);
        Assert.Equal((1.0, 0.0, 0.0, 1.0, 0.0), (l.Opacity, l.X, l.Y, l.Scale, l.Rotation));
        Assert.False(l.FlipH || l.FlipV);
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate));
        Assert.Equal((0, 0, 0), (l.RShift, l.GShift, l.BShift));
    }

    [Fact]
    public void Decode_keeps_low_alpha_pixels_exact()
    {
        var img = Images.Decode(Png(new SKColor(200, 100, 50, 3)));
        Assert.Equal(SKAlphaType.Unpremul, img.AlphaType);
        Assert.Equal(new SKColor(200, 100, 50, 3), Px.At(img, 0, 0));
    }

    [Fact]
    public void Decode_rejects_non_images() =>
        Assert.Throws<InvalidDataException>(() => Images.Decode("not an image"u8.ToArray()));

    [Fact]
    public void ForDrawing_is_a_cached_premultiplied_copy()
    {
        var img = Images.Decode(Png(new SKColor(10, 20, 30, 128), 3, 2));
        var a = Images.ForDrawing(img);
        Assert.Equal(SKAlphaType.Premul, a.AlphaType);
        Assert.Equal((3, 2), (a.Width, a.Height));
        Assert.Same(a, Images.ForDrawing(img));
    }

    [Fact]
    public void With_expressions_share_the_bitmap()
    {
        var l = new Layer { Id = 1, Image = Images.Decode(Png(SKColors.Red)) };
        Assert.Same(l.Image, (l with { Id = 2, Name = "copy" }).Image);
    }

    // ---- ContentEquals (spec §5: the session records a step only when content changed) ----

    static readonly SKImage Red = Px.Solid(2, 2, SKColors.Red);

    static Document Rich() => new()
    {
        BgColor = "#0f1f31",
        RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, 16))),
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Red, Name = "Image 1", X = 10 }),
    };

    [Fact] // two edits that rebuild equal lists are the same content; record == compares the lists by reference
    public void Rebuilt_lists_with_equal_items_are_the_same_content()
    {
        var a = Rich();
        var b = Rich();
        Assert.NotEqual(a, b);
        Assert.True(a.ContentEquals(b));
        Assert.True(b.ContentEquals(a));
        Assert.True(a.ContentEquals(a));
        Assert.True(new Document().ContentEquals(new Document()));
    }

    [Fact] // a no-op with expression keeps content (undo-empty-steps)
    public void A_with_expression_that_sets_the_same_values_keeps_content() =>
        Assert.True(Rich().ContentEquals(Rich() with { BgColor = "#0f1f31", ShowMask = true, Mode = TemplateMode.Shirt }));

    public static TheoryData<string> Changes => new()
    {
        "Mode", "BgColor", "BgOpacity", "BgImage", "ShowMask", "ShowGrid",
        "fill added", "fill value", "fill order", "layer added", "layer field", "layer image", "layer order",
    };

    [Theory, MemberData(nameof(Changes))] // file-undo-snapshot-scope: every Document field is content
    public void Every_document_field_is_content(string change)
    {
        var a = Rich();
        var two = a with
        {
            RegionFills = a.RegionFills.Add(("torso_back", Fill.Solid("#333"))),
            Layers = a.Layers.Add(new Layer { Id = 2, Image = Red }),
        };
        var (before, after) = change switch
        {
            "Mode" => (a, a with { Mode = TemplateMode.Pants }),
            "BgColor" => (a, a with { BgColor = "#0F1F31" }),
            "BgOpacity" => (a, a with { BgOpacity = 0.5 }),
            "BgImage" => (a, a with { BgImage = Red }),
            "ShowMask" => (a, a with { ShowMask = false }),
            "ShowGrid" => (a, a with { ShowGrid = true }),
            "fill added" => (a, a with { RegionFills = two.RegionFills }),
            "fill value" => (a, a with { RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 90, 16))) }),
            "fill order" => (two, two with { RegionFills = two.RegionFills.Reverse() }),
            "layer added" => (a, a with { Layers = two.Layers }),
            "layer field" => (a, a with { Layers = ImmutableList.Create(a.Layers[0] with { Visible = false }) }),
            "layer image" => (a, a with { Layers = ImmutableList.Create(a.Layers[0] with { Image = Px.Solid(2, 2, SKColors.Red) }) }),
            "layer order" => (two, two with { Layers = two.Layers.Reverse() }),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        Assert.False(before.ContentEquals(after), change);
        Assert.False(after.ContentEquals(before), change);
    }

    [Fact] // a null other is never equal content
    public void Null_is_never_the_same_content() => Assert.False(new Document().ContentEquals(null!));
}
