namespace RBX15.Engine.Tests;

/// <summary>The approximate R15 body (ledger avatar-preview-r15): the DevForum split of the template, the pose, the rigging.</summary>
public class AvatarR15Tests
{
    static readonly Face[] AllFaces = { Face.Up, Face.Right, Face.Front, Face.Left, Face.Back, Face.Down };

    static AvatarFace F(AvatarBox b, Face f) => b.Faces.Single(x => x.Face == f);
    static Vec3 Sub(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    static double Len(Vec3 a, Vec3 b) { var d = Sub(a, b); return Math.Sqrt(Dot(d, d)); }
    static bool Near(Vec3 a, Vec3 b) => Len(a, b) < 1e-9;
    static bool SameSet(IEnumerable<Vec3> a, IEnumerable<Vec3> b)
    {
        var x = a.ToArray();
        var y = b.ToArray();
        return x.All(p => y.Any(q => Near(p, q))) && y.All(q => x.Any(p => Near(p, q)));
    }

    static AvatarBox R(TemplateMode m, AvatarPart p, AvatarPose? pose = null) =>
        Avatar.Build(m, AvatarBody.R15, pose).Single(b => b.Part == p);

    static IEnumerable<Vec3> AllCorners(AvatarBox b) => b.Faces.SelectMany(f => f.Corners);

    static Vec3 Centroid(AvatarBox b)
    {
        var c = AllCorners(b).ToArray();
        return new(c.Average(v => v.X), c.Average(v => v.Y), c.Average(v => v.Z));
    }

    static readonly AvatarPart[] R15Parts =
    {
        AvatarPart.Head, AvatarPart.UpperTorso, AvatarPart.LowerTorso,
        AvatarPart.RightUpperArm, AvatarPart.RightLowerArm, AvatarPart.RightHand,
        AvatarPart.LeftUpperArm, AvatarPart.LeftLowerArm, AvatarPart.LeftHand,
        AvatarPart.RightUpperLeg, AvatarPart.RightLowerLeg, AvatarPart.RightFoot,
        AvatarPart.LeftUpperLeg, AvatarPart.LeftLowerLeg, AvatarPart.LeftFoot,
    };

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Build_defaults_to_Classic_and_matches_it_face_for_face(TemplateMode mode)
    {
        var plain = Avatar.Build(mode);
        var classic = Avatar.Build(mode, AvatarBody.Classic);
        Assert.Equal(6, classic.Count);
        Assert.Equal(plain.Count, classic.Count);
        for (int i = 0; i < plain.Count; i++)
        {
            Assert.Equal(plain[i].Part, classic[i].Part);
            Assert.Equal(plain[i].Min, classic[i].Min);
            Assert.Equal(plain[i].Max, classic[i].Max);
            Assert.Equal(plain[i].Dressed, classic[i].Dressed);
            for (int f = 0; f < 6; f++)
            {
                Assert.Equal(plain[i].Faces[f].Face, classic[i].Faces[f].Face);
                Assert.Equal(plain[i].Faces[f].Region, classic[i].Faces[f].Region);
                Assert.Equal(plain[i].Faces[f].Corners, classic[i].Faces[f].Corners);
                Assert.Equal(plain[i].Faces[f].Uv, classic[i].Faces[f].Uv);
            }
        }
    }

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void R15_has_exactly_15_boxes_in_order_with_six_faces_each(TemplateMode mode)
    {
        var boxes = Avatar.Build(mode, AvatarBody.R15);
        Assert.Equal(15, boxes.Count);
        Assert.Equal(R15Parts, boxes.Select(b => b.Part));
        Assert.All(boxes, b => Assert.Equal(AllFaces, b.Faces.Select(f => f.Face)));
    }

    static (float, float)[] SliceUv(Region r, int from, int to) => new (float, float)[]
    {
        (r.X + 0.5f, r.Y + from + 0.5f), (r.X + r.W - 0.5f, r.Y + from + 0.5f),
        (r.X + r.W - 0.5f, r.Y + to - 0.5f), (r.X + 0.5f, r.Y + to - 0.5f),
    };

    [Theory]
    [InlineData(AvatarPart.UpperTorso, 0, 96)]
    [InlineData(AvatarPart.LowerTorso, 96, 128)]
    public void R15_torso_sides_take_their_slice_of_the_region_rows_inset_half_a_pixel(AvatarPart part, int from, int to)
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
            foreach (var face in new[] { Face.Front, Face.Back, Face.Left, Face.Right })
            {
                var f = F(R(mode, part), face);
                var region = Template.Slot(mode, BodyPart.Body, face);
                Assert.Equal(region, f.Region);
                Assert.Equal(SliceUv(region, from, to), f.Uv);
            }
    }

    [Theory]
    [InlineData(TemplateMode.Shirt, AvatarPart.RightUpperArm, 0, 64, BodyPart.RightLimb)]
    [InlineData(TemplateMode.Shirt, AvatarPart.RightLowerArm, 64, 112, BodyPart.RightLimb)]
    [InlineData(TemplateMode.Shirt, AvatarPart.RightHand, 112, 128, BodyPart.RightLimb)]
    [InlineData(TemplateMode.Shirt, AvatarPart.LeftUpperArm, 0, 64, BodyPart.LeftLimb)]
    [InlineData(TemplateMode.Shirt, AvatarPart.LeftLowerArm, 64, 112, BodyPart.LeftLimb)]
    [InlineData(TemplateMode.Shirt, AvatarPart.LeftHand, 112, 128, BodyPart.LeftLimb)]
    [InlineData(TemplateMode.Pants, AvatarPart.RightUpperLeg, 0, 64, BodyPart.RightLimb)]
    [InlineData(TemplateMode.Pants, AvatarPart.RightLowerLeg, 64, 112, BodyPart.RightLimb)]
    [InlineData(TemplateMode.Pants, AvatarPart.RightFoot, 112, 128, BodyPart.RightLimb)]
    [InlineData(TemplateMode.Pants, AvatarPart.LeftUpperLeg, 0, 64, BodyPart.LeftLimb)]
    [InlineData(TemplateMode.Pants, AvatarPart.LeftLowerLeg, 64, 112, BodyPart.LeftLimb)]
    [InlineData(TemplateMode.Pants, AvatarPart.LeftFoot, 112, 128, BodyPart.LeftLimb)]
    public void R15_limb_sides_take_their_slice_of_the_region_rows_inset_half_a_pixel(TemplateMode mode, AvatarPart part, int from, int to, BodyPart slot)
    {
        foreach (var face in new[] { Face.Front, Face.Back, Face.Left, Face.Right })
        {
            var f = F(R(mode, part), face);
            var region = Template.Slot(mode, slot, face);
            Assert.Equal(region, f.Region);
            Assert.Equal(SliceUv(region, from, to), f.Uv);
        }
    }

    [Fact]
    public void R15_slices_of_a_limb_tile_the_region_rows_with_a_one_pixel_inset_seam()
    {
        var region = Template.Slot(TemplateMode.Shirt, BodyPart.RightLimb, Face.Front);
        var rows = new[] { AvatarPart.RightUpperArm, AvatarPart.RightLowerArm, AvatarPart.RightHand }
            .Select(p => (V0: F(R(TemplateMode.Shirt, p), Face.Front).Uv[0].V, V1: F(R(TemplateMode.Shirt, p), Face.Front).Uv[3].V))
            .ToArray();
        Assert.Equal(region.Y + 0.5f, rows[0].V0);
        Assert.Equal(rows[0].V1 + 1, rows[1].V0);
        Assert.Equal(rows[1].V1 + 1, rows[2].V0);
        Assert.Equal(region.Y + region.H - 0.5f, rows[2].V1);
    }

    static void AssertCap(AvatarBox box, Face face, Region? expected)
    {
        var f = F(box, face);
        if (expected is not { } r)
        {
            Assert.Null(f.Region);
            Assert.Empty(f.Uv);
        }
        else
        {
            Assert.Equal(r, f.Region);
            Assert.Equal(SliceUv(r, 0, r.H), f.Uv);
        }
    }

    [Fact]
    public void R15_outer_caps_wear_the_Up_and_Down_regions_and_inner_caps_are_undressed()
    {
        var m = TemplateMode.Shirt;
        var body = (Face f) => Template.Slot(m, BodyPart.Body, f);
        AssertCap(R(m, AvatarPart.UpperTorso), Face.Up, body(Face.Up));
        AssertCap(R(m, AvatarPart.UpperTorso), Face.Down, null);
        AssertCap(R(m, AvatarPart.LowerTorso), Face.Up, null);
        AssertCap(R(m, AvatarPart.LowerTorso), Face.Down, body(Face.Down));
        foreach (var (u, l, h, slot) in new[]
        {
            (AvatarPart.RightUpperArm, AvatarPart.RightLowerArm, AvatarPart.RightHand, BodyPart.RightLimb),
            (AvatarPart.LeftUpperArm, AvatarPart.LeftLowerArm, AvatarPart.LeftHand, BodyPart.LeftLimb),
        })
        {
            AssertCap(R(m, u), Face.Up, Template.Slot(m, slot, Face.Up));
            AssertCap(R(m, u), Face.Down, null);
            AssertCap(R(m, l), Face.Up, null);
            AssertCap(R(m, l), Face.Down, null);
            AssertCap(R(m, h), Face.Up, null);
            AssertCap(R(m, h), Face.Down, Template.Slot(m, slot, Face.Down));
        }
        var p = TemplateMode.Pants;
        foreach (var (u, l, h, slot) in new[]
        {
            (AvatarPart.RightUpperLeg, AvatarPart.RightLowerLeg, AvatarPart.RightFoot, BodyPart.RightLimb),
            (AvatarPart.LeftUpperLeg, AvatarPart.LeftLowerLeg, AvatarPart.LeftFoot, BodyPart.LeftLimb),
        })
        {
            AssertCap(R(p, u), Face.Up, Template.Slot(p, slot, Face.Up));
            AssertCap(R(p, u), Face.Down, null);
            AssertCap(R(p, l), Face.Up, null);
            AssertCap(R(p, l), Face.Down, null);
            AssertCap(R(p, h), Face.Up, null);
            AssertCap(R(p, h), Face.Down, Template.Slot(p, slot, Face.Down));
        }
    }

    [Theory]
    [InlineData(TemplateMode.Shirt, new[] { AvatarPart.UpperTorso, AvatarPart.LowerTorso, AvatarPart.RightUpperArm, AvatarPart.RightLowerArm, AvatarPart.RightHand, AvatarPart.LeftUpperArm, AvatarPart.LeftLowerArm, AvatarPart.LeftHand })]
    [InlineData(TemplateMode.Pants, new[] { AvatarPart.UpperTorso, AvatarPart.LowerTorso, AvatarPart.RightUpperLeg, AvatarPart.RightLowerLeg, AvatarPart.RightFoot, AvatarPart.LeftUpperLeg, AvatarPart.LeftLowerLeg, AvatarPart.LeftFoot })]
    public void R15_mode_dresses_both_torso_parts_and_its_limb_parts_and_never_the_head(TemplateMode mode, AvatarPart[] dressed)
    {
        var boxes = Avatar.Build(mode, AvatarBody.R15);
        Assert.Equal(dressed.OrderBy(p => p), boxes.Where(b => b.Dressed).Select(b => b.Part).OrderBy(p => p));
        Assert.False(boxes.Single(b => b.Part == AvatarPart.Head).Dressed);
        foreach (var b in boxes.Where(b => !b.Dressed))
            Assert.All(b.Faces, f => { Assert.Null(f.Region); Assert.Empty(f.Uv); });
        foreach (var b in boxes.Where(b => b.Dressed))
            foreach (var f in b.Faces.Where(f => f.Face is Face.Front or Face.Back or Face.Left or Face.Right))
                Assert.NotNull(f.Region);
        foreach (var b in boxes)
            foreach (var f in b.Faces)
                Assert.Equal(f.Region is not null, f.Uv.Length == 4);
    }

    static readonly (AvatarPart A, AvatarPart B)[] Chains =
    {
        (AvatarPart.UpperTorso, AvatarPart.LowerTorso),
        (AvatarPart.RightUpperArm, AvatarPart.RightLowerArm), (AvatarPart.RightLowerArm, AvatarPart.RightHand),
        (AvatarPart.LeftUpperArm, AvatarPart.LeftLowerArm), (AvatarPart.LeftLowerArm, AvatarPart.LeftHand),
        (AvatarPart.RightUpperLeg, AvatarPart.RightLowerLeg), (AvatarPart.RightLowerLeg, AvatarPart.RightFoot),
        (AvatarPart.LeftUpperLeg, AvatarPart.LeftLowerLeg), (AvatarPart.LeftLowerLeg, AvatarPart.LeftFoot),
    };

    [Fact]
    public void Unposed_R15_chains_share_their_joint_face_with_no_gap()
    {
        foreach (var (a, b) in Chains)
        {
            var upper = R(TemplateMode.Shirt, a, AvatarPose.None);
            var lower = R(TemplateMode.Shirt, b, AvatarPose.None);
            Assert.True(SameSet(F(upper, Face.Down).Corners, F(lower, Face.Up).Corners), $"{a} / {b}");
        }
        // the legs hang from the lower torso's bottom plane
        var waist = F(R(TemplateMode.Pants, AvatarPart.LowerTorso, AvatarPose.None), Face.Down).Corners;
        foreach (var leg in new[] { AvatarPart.RightUpperLeg, AvatarPart.LeftUpperLeg })
            Assert.All(F(R(TemplateMode.Pants, leg, AvatarPose.None), Face.Up).Corners, c => Assert.Equal(waist[0].Y, c.Y));
        // and the pieces add up to the classic silhouette
        var classic = Avatar.Build(TemplateMode.Shirt).SelectMany(AllCorners).ToArray();
        var all = Avatar.Build(TemplateMode.Shirt, AvatarBody.R15, AvatarPose.None).SelectMany(AllCorners).ToArray();
        Assert.Equal(classic.Min(c => c.Y), all.Min(c => c.Y), 9);
        Assert.Equal(classic.Max(c => c.Y), all.Max(c => c.Y), 9);
        Assert.Equal(classic.Min(c => c.X), all.Min(c => c.X), 9);
        Assert.Equal(classic.Max(c => c.X), all.Max(c => c.X), 9);
    }

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Posing_keeps_every_R15_box_rigid(TemplateMode mode)
    {
        var rest = Avatar.Build(mode, AvatarBody.R15, AvatarPose.None);
        var posed = Avatar.Build(mode, AvatarBody.R15);
        int moved = 0;
        for (int i = 0; i < rest.Count; i++)
        {
            // every edge of every face: together the 12 edges of the box
            for (int f = 0; f < 6; f++)
                for (int k = 0; k < 4; k++)
                {
                    int n = (k + 1) % 4;
                    Assert.Equal(Len(rest[i].Faces[f].Corners[k], rest[i].Faces[f].Corners[n]),
                                 Len(posed[i].Faces[f].Corners[k], posed[i].Faces[f].Corners[n]), 9);
                }
            // the diagonals too: a shear keeps edge lengths and changes these
            var rc = AllCorners(rest[i]).ToArray();
            var pc = AllCorners(posed[i]).ToArray();
            for (int a = 0; a < rc.Length; a++)
                for (int b = a + 1; b < rc.Length; b++)
                    Assert.Equal(Len(rc[a], rc[b]), Len(pc[a], pc[b]), 9);
            if (rc.Zip(pc).Any(t => !Near(t.First, t.Second))) moved++;
        }
        Assert.Equal(12, moved);   // the 12 limb parts move; the head and both torso parts do not
    }

    [Fact]
    public void Posing_moves_only_the_arms_and_legs()
    {
        var rest = Avatar.Build(TemplateMode.Shirt, AvatarBody.R15, AvatarPose.None);
        var posed = Avatar.Build(TemplateMode.Shirt, AvatarBody.R15);
        foreach (var part in new[] { AvatarPart.Head, AvatarPart.UpperTorso, AvatarPart.LowerTorso })
            Assert.Equal(AllCorners(rest.Single(b => b.Part == part)), AllCorners(posed.Single(b => b.Part == part)));
        foreach (var part in R15Parts.Skip(3))
            Assert.NotEqual(AllCorners(rest.Single(b => b.Part == part)), AllCorners(posed.Single(b => b.Part == part)));
    }

    [Fact]
    public void Posed_left_and_right_limbs_are_mirror_images()
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
            foreach (var (r, l) in new[]
            {
                (AvatarPart.RightUpperArm, AvatarPart.LeftUpperArm), (AvatarPart.RightLowerArm, AvatarPart.LeftLowerArm), (AvatarPart.RightHand, AvatarPart.LeftHand),
                (AvatarPart.RightUpperLeg, AvatarPart.LeftUpperLeg), (AvatarPart.RightLowerLeg, AvatarPart.LeftLowerLeg), (AvatarPart.RightFoot, AvatarPart.LeftFoot),
            })
            {
                var mirrored = AllCorners(R(mode, r)).Select(v => v with { X = -v.X });
                Assert.True(SameSet(mirrored, AllCorners(R(mode, l))), $"{r} / {l}");
            }
    }

    [Fact]
    public void The_pose_swings_arms_outward_bends_forearms_forward_and_swings_legs_outward()
    {
        double MeanX(AvatarPart p, AvatarPose? pose = null) => AllCorners(R(TemplateMode.Shirt, p, pose)).Average(v => v.X);
        double MeanZ(AvatarPart p, AvatarPose? pose = null) => AllCorners(R(TemplateMode.Shirt, p, pose)).Average(v => v.Z);

        // the right hand is further out (-X) than at rest, the left hand further +X; the upper arm's top-inner edge stays put
        Assert.True(MeanX(AvatarPart.RightHand) < MeanX(AvatarPart.RightHand, AvatarPose.None) - 0.1);
        Assert.True(MeanX(AvatarPart.LeftHand) > MeanX(AvatarPart.LeftHand, AvatarPose.None) + 0.1);
        Assert.Equal(new Vec3(-1, 4, -0.5), F(R(TemplateMode.Shirt, AvatarPart.RightUpperArm), Face.Left).Corners[1]);
        Assert.Equal(new Vec3(1, 4, -0.5), F(R(TemplateMode.Shirt, AvatarPart.LeftUpperArm), Face.Right).Corners[0]);
        // the forearm and hand bend forward: toward +Z; the upper arm does not
        Assert.True(MeanZ(AvatarPart.RightHand) > MeanZ(AvatarPart.RightHand, AvatarPose.None) + 0.15);
        Assert.True(MeanZ(AvatarPart.RightLowerArm) > MeanZ(AvatarPart.RightLowerArm, AvatarPose.None) + 0.05);
        Assert.Equal(0, MeanZ(AvatarPart.RightUpperArm), 9);
        // legs: the feet swing out, the knees stay straight (no z)
        var footR = Centroid(R(TemplateMode.Pants, AvatarPart.RightFoot));
        Assert.True(footR.X < Centroid(R(TemplateMode.Pants, AvatarPart.RightFoot, AvatarPose.None)).X - 0.05);
        Assert.Equal(0, footR.Z, 9);
        // the posed arms stay inside the camera's frame (about +/-2.4 studs across)
        var all = Avatar.Build(TemplateMode.Shirt, AvatarBody.R15).SelectMany(AllCorners).ToArray();
        Assert.InRange(all.Min(v => v.X), -2.4, -2.0);
        Assert.InRange(all.Max(v => v.X), 2.0, 2.4);
    }

    [Fact]
    public void Every_posed_R15_face_still_faces_outward()
    {
        foreach (var mode in new[] { TemplateMode.Shirt, TemplateMode.Pants })
            foreach (var b in Avatar.Build(mode, AvatarBody.R15))
            {
                var center = Centroid(b);
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
    public void R15_faces_follow_the_spec_face_table_before_posing()
    {
        foreach (var b in Avatar.Build(TemplateMode.Shirt, AvatarBody.R15, AvatarPose.None))
        {
            double x0 = b.Min.X, y0 = b.Min.Y, z0 = b.Min.Z, x1 = b.Max.X, y1 = b.Max.Y, z1 = b.Max.Z;
            Assert.Equal(new Vec3[] { new(x0, y1, z1), new(x1, y1, z1), new(x1, y0, z1), new(x0, y0, z1) }, F(b, Face.Front).Corners);
            Assert.Equal(new Vec3[] { new(x1, y1, z0), new(x0, y1, z0), new(x0, y0, z0), new(x1, y0, z0) }, F(b, Face.Back).Corners);
            Assert.Equal(new Vec3[] { new(x0, y0, z1), new(x1, y0, z1), new(x1, y0, z0), new(x0, y0, z0) }, F(b, Face.Down).Corners);
        }
    }
}
