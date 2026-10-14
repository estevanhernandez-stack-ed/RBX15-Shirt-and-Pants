using SkiaSharp;

namespace RBX15.Engine.Tests;

public class TemplateTests
{
    static readonly string[] ShirtNames =
    {
        "torso_up", "torso_right", "torso_front", "torso_left", "torso_back", "torso_down",
        "rarm_up", "rarm_left", "rarm_back", "rarm_right", "rarm_front", "rarm_down",
        "larm_up", "larm_front", "larm_left", "larm_back", "larm_right", "larm_down",
    };

    static readonly string[] PantsNames =
    {
        "waist_up", "waist_right", "waist_front", "waist_left", "waist_back", "waist_down",
        "rleg_up", "rleg_left", "rleg_back", "rleg_right", "rleg_front", "rleg_down",
        "lleg_up", "lleg_front", "lleg_left", "lleg_back", "lleg_right", "lleg_down",
    };

    [Fact]
    public void Shirt_regions_match_the_4_1_table_in_key_order()
    {
        var r = Template.Regions(TemplateMode.Shirt);
        Assert.Equal(ShirtNames, r.Select(x => x.Name));
        Assert.Equal(new Region("torso_front", 231, 74, 128, 128), r[2]);
        Assert.Equal(new Region("rarm_left", 19, 355, 64, 128), r[7]);
        Assert.Equal(new Region("larm_down", 308, 485, 64, 64), r[17]);
    }

    [Fact]
    public void Pants_regions_have_shirt_geometry_with_pants_names()
    {
        var shirt = Template.Regions(TemplateMode.Shirt);
        var pants = Template.Regions(TemplateMode.Pants);
        Assert.Equal(PantsNames, pants.Select(x => x.Name));
        for (int i = 0; i < 18; i++)
            Assert.Equal((shirt[i].X, shirt[i].Y, shirt[i].W, shirt[i].H), (pants[i].X, pants[i].Y, pants[i].W, pants[i].H));
    }

    [Fact]
    public void Slots_resolve_per_mode()
    {
        Assert.Equal("torso_front", Template.Slot(TemplateMode.Shirt, BodyPart.Body, Face.Front).Name);
        Assert.Equal("waist_front", Template.Slot(TemplateMode.Pants, BodyPart.Body, Face.Front).Name);
        Assert.Equal("rarm_back", Template.Slot(TemplateMode.Shirt, BodyPart.RightLimb, Face.Back).Name);
        Assert.Equal("lleg_down", Template.Slot(TemplateMode.Pants, BodyPart.LeftLimb, Face.Down).Name);
    }

    [Fact]
    public void TryFind_misses_names_from_the_other_mode()
    {
        Assert.True(Template.TryFind(TemplateMode.Shirt, "torso_front", out var r));
        Assert.Equal(231, r.X);
        Assert.False(Template.TryFind(TemplateMode.Pants, "torso_front", out _));
    }

    [Fact]
    public void Mask_is_opaque_inside_regions_and_clear_in_gutters()
    {
        var mask = Template.Mask(TemplateMode.Shirt);
        Assert.Equal((585, 559), (mask.Width, mask.Height));
        Assert.Equal(255, Px.At(mask, 295, 138).Alpha);   // torso_front center
        Assert.Equal(0, Px.At(mask, 230, 138).Alpha);     // gutter between torso_right and torso_front
        Assert.Equal(0, Px.At(mask, 0, 0).Alpha);
    }
}
