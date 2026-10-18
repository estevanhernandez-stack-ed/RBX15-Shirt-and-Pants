using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class AvatarTests
{
    static readonly Face[] AllFaces = { Face.Up, Face.Right, Face.Front, Face.Left, Face.Back, Face.Down };

    static AvatarBox Part(TemplateMode m, AvatarPart p) => Avatar.Build(m).Single(b => b.Part == p);
    static AvatarFace F(AvatarBox b, Face f) => b.Faces.Single(x => x.Face == f);
    static Vec3 Sub(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    [Theory]
    [InlineData(AvatarPart.Head, -0.6, 4, -0.6, 0.6, 5.2, 0.6)]
    [InlineData(AvatarPart.Torso, -1, 2, -0.5, 1, 4, 0.5)]
    [InlineData(AvatarPart.RightArm, -2, 2, -0.5, -1, 4, 0.5)]
    [InlineData(AvatarPart.LeftArm, 1, 2, -0.5, 2, 4, 0.5)]
    [InlineData(AvatarPart.RightLeg, -1, 0, -0.5, 0, 2, 0.5)]
    [InlineData(AvatarPart.LeftLeg, 0, 0, -0.5, 1, 2, 0.5)]
    public void Build_places_the_six_boxes_at_the_spec_coordinates(AvatarPart p, double x0, double y0, double z0, double x1, double y1, double z1)
    {
        var boxes = Avatar.Build(TemplateMode.Shirt);
        Assert.Equal(6, boxes.Count);
        Assert.Equal(new[] { AvatarPart.Head, AvatarPart.Torso, AvatarPart.RightArm, AvatarPart.LeftArm, AvatarPart.RightLeg, AvatarPart.LeftLeg }, boxes.Select(b => b.Part));
        var box = boxes.Single(b => b.Part == p);
        Assert.Equal(new Vec3(x0, y0, z0), box.Min);
        Assert.Equal(new Vec3(x1, y1, z1), box.Max);
        Assert.Equal(AllFaces, box.Faces.Select(f => f.Face));
    }

    [Fact]
    public void Faces_follow_the_spec_face_table_on_every_box()
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
            foreach (var b in Avatar.Build(mode))
            {
                double x0 = b.Min.X, y0 = b.Min.Y, z0 = b.Min.Z, x1 = b.Max.X, y1 = b.Max.Y, z1 = b.Max.Z;
                Assert.Equal(new Vec3[] { new(x0, y1, z1), new(x1, y1, z1), new(x1, y0, z1), new(x0, y0, z1) }, F(b, Face.Front).Corners);
                Assert.Equal(new Vec3[] { new(x1, y1, z0), new(x0, y1, z0), new(x0, y0, z0), new(x1, y0, z0) }, F(b, Face.Back).Corners);
                Assert.Equal(new Vec3[] { new(x0, y1, z0), new(x0, y1, z1), new(x0, y0, z1), new(x0, y0, z0) }, F(b, Face.Right).Corners);
                Assert.Equal(new Vec3[] { new(x1, y1, z1), new(x1, y1, z0), new(x1, y0, z0), new(x1, y0, z1) }, F(b, Face.Left).Corners);
                Assert.Equal(new Vec3[] { new(x0, y1, z0), new(x1, y1, z0), new(x1, y1, z1), new(x0, y1, z1) }, F(b, Face.Up).Corners);
                Assert.Equal(new Vec3[] { new(x0, y0, z1), new(x1, y0, z1), new(x1, y0, z0), new(x0, y0, z0) }, F(b, Face.Down).Corners);
            }
    }

    // Edges as (start, end) corner pairs: left = TL,BL; right = TR,BR; top = TL,TR; bottom = BL,BR.
    static (Vec3, Vec3) Left(AvatarFace f) => (f.Corners[0], f.Corners[3]);
    static (Vec3, Vec3) Right(AvatarFace f) => (f.Corners[1], f.Corners[2]);
    static (Vec3, Vec3) Top(AvatarFace f) => (f.Corners[0], f.Corners[1]);
    static (Vec3, Vec3) Bottom(AvatarFace f) => (f.Corners[3], f.Corners[2]);

    [Fact]
    public void Faces_of_a_box_share_edges()
    {
        foreach (var b in Avatar.Build(TemplateMode.Shirt))
        {
            Assert.Equal(Left(F(b, Face.Front)), Right(F(b, Face.Right)));
            Assert.Equal(Right(F(b, Face.Front)), Left(F(b, Face.Left)));
            Assert.Equal(Bottom(F(b, Face.Up)), Top(F(b, Face.Front)));
            Assert.Equal(Top(F(b, Face.Down)), Bottom(F(b, Face.Front)));
        }
    }

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Image_neighbours_in_the_template_share_a_3D_edge(TemplateMode mode)
    {
        var faces = new List<(Region R, AvatarFace F)>();
        foreach (var b in Avatar.Build(mode).Where(b => b.Dressed))
            foreach (var f in b.Faces) faces.Add((f.Region!.Value, f));

        // Every template region is dressed exactly once.
        Assert.Equal(Template.Regions(mode).Count, faces.Count);

        int pairs = 0;
        foreach (var a in faces)
            foreach (var c in faces)
            {
                if (a.R.X + a.R.W + 2 != c.R.X || a.R.Y != c.R.Y || a.R.H != c.R.H) continue;
                pairs++;
                Assert.Equal(Right(a.F), Left(c.F));
            }
        // torso strip 3 + right limb strip 3 + left limb strip 3
        Assert.Equal(9, pairs);
    }

    [Fact]
    public void Triangles_0_3_2_and_0_2_1_face_outward()
    {
        foreach (var b in Avatar.Build(TemplateMode.Shirt))
        {
            var center = new Vec3((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2, (b.Min.Z + b.Max.Z) / 2);
            foreach (var f in b.Faces)
            {
                var c = f.Corners;
                var fc = new Vec3(c.Average(v => v.X), c.Average(v => v.Y), c.Average(v => v.Z));
                var outward = Sub(fc, center);
                foreach (var (i, j, k) in new[] { (0, 3, 2), (0, 2, 1) })
                {
                    var n = Cross(Sub(c[j], c[i]), Sub(c[k], c[i]));
                    Assert.True(Dot(n, outward) > 0, $"{b.Part} {f.Face} triangle {i},{j},{k}");
                }
            }
        }
    }

    [Fact]
    public void Uvs_are_the_slot_corners_inset_half_a_pixel()
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
            foreach (var b in Avatar.Build(mode).Where(b => b.Dressed))
                foreach (var f in b.Faces)
                {
                    var r = f.Region!.Value;
                    Assert.Equal(new (float, float)[]
                    {
                        (r.X + 0.5f, r.Y + 0.5f), (r.X + r.W - 0.5f, r.Y + 0.5f),
                        (r.X + r.W - 0.5f, r.Y + r.H - 0.5f), (r.X + 0.5f, r.Y + r.H - 0.5f),
                    }, f.Uv);
                }
    }

    [Fact]
    public void Regions_come_from_Template_Slot()
    {
        var torso = Part(TemplateMode.Shirt, AvatarPart.Torso);
        Assert.Equal(Template.Slot(TemplateMode.Shirt, BodyPart.Body, Face.Front), F(torso, Face.Front).Region);
        var rarm = Part(TemplateMode.Shirt, AvatarPart.RightArm);
        Assert.Equal(Template.Slot(TemplateMode.Shirt, BodyPart.RightLimb, Face.Left), F(rarm, Face.Left).Region);
        var lleg = Part(TemplateMode.Pants, AvatarPart.LeftLeg);
        Assert.Equal("lleg_back", F(lleg, Face.Back).Region!.Value.Name);
    }

    [Theory]
    [InlineData(TemplateMode.Shirt, new[] { AvatarPart.Torso, AvatarPart.RightArm, AvatarPart.LeftArm })]
    [InlineData(TemplateMode.Pants, new[] { AvatarPart.Torso, AvatarPart.RightLeg, AvatarPart.LeftLeg })]
    public void Mode_dresses_torso_and_its_limbs_and_never_the_head(TemplateMode mode, AvatarPart[] dressed)
    {
        var boxes = Avatar.Build(mode);
        Assert.Equal(dressed.OrderBy(p => p), boxes.Where(b => b.Dressed).Select(b => b.Part).OrderBy(p => p));
        Assert.False(boxes.Single(b => b.Part == AvatarPart.Head).Dressed);
        foreach (var b in boxes.Where(b => !b.Dressed))
            foreach (var f in b.Faces)
            {
                Assert.Null(f.Region);
                Assert.Empty(f.Uv);
            }
        foreach (var b in boxes.Where(b => b.Dressed))
            Assert.All(b.Faces, f => Assert.NotNull(f.Region));
    }

    [Fact]
    public void RenderTexture_is_opaque_template_sized_and_body_colored_where_nothing_is_drawn()
    {
        var doc = new Document { BgOpacity = 0 };
        using var tex = Avatar.RenderTexture(doc);
        Assert.Equal((585, 559), (tex.Width, tex.Height));

        var info = new SKImageInfo(tex.Width, tex.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(tex.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                Assert.Equal(255, bmp.GetPixel(x, y).Alpha);

        Assert.Equal(new SKColor(0xA3, 0xA2, 0xA5), Avatar.BodyColor);
        Assert.Equal(Avatar.BodyColor, Px.At(tex, 0, 0));
        // gap between torso_right (165..228) and torso_front (231..): x 229 and 230
        Assert.Equal(Avatar.BodyColor, Px.At(tex, 230, 100));
        // inside torso_front, nothing drawn
        Assert.Equal(Avatar.BodyColor, Px.At(tex, 290, 140));
    }

    [Fact]
    public void RenderTexture_matches_the_export_inside_a_filled_region()
    {
        var doc = new Document
        {
            BgOpacity = 0,
            RegionFills = ImmutableList.Create(("torso_front", Fill.Solid("#ff0000"))),
        };
        using var tex = Avatar.RenderTexture(doc);
        using var export = Renderer.Export(doc);
        Assert.Equal(new SKColor(255, 0, 0), Px.At(export, 290, 140));
        Assert.Equal(Px.At(export, 290, 140), Px.At(tex, 290, 140));
        // outside the filled region it is still the body color
        Assert.Equal(Avatar.BodyColor, Px.At(tex, 20, 20));
    }
}
