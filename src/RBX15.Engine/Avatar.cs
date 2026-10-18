using SkiaSharp;

namespace RBX15.Engine;

/// <summary>A point or extent in studs (v5.1 spec, Coordinates). WPF's right-handed space, Y up, avatar facing +Z.</summary>
public readonly record struct Vec3(double X, double Y, double Z);

/// <summary>The classic blocky parts, then the R15 parts (appended, so the classic values keep their numbers).</summary>
public enum AvatarPart
{
    Head, Torso, RightArm, LeftArm, RightLeg, LeftLeg,
    UpperTorso, LowerTorso,
    RightUpperArm, RightLowerArm, RightHand, LeftUpperArm, LeftLowerArm, LeftHand,
    RightUpperLeg, RightLowerLeg, RightFoot, LeftUpperLeg, LeftLowerLeg, LeftFoot,
}

/// <summary>Which body the preview wears. R15 is approximate: the classic silhouette split where Roblox cuts the template.</summary>
public enum AvatarBody { Classic, R15 }

/// <summary>
/// The R15 pose, in degrees of magnitude: the arms swing <paramref name="ShoulderDegrees"/> outward at the shoulder, the
/// forearm and hand bend <paramref name="ElbowDegrees"/> forward at the elbow, the legs swing <paramref name="HipDegrees"/>
/// outward at the hip. <see cref="None"/> leaves the parts at rest, where each chain meets edge to edge.
/// </summary>
public sealed record AvatarPose(double ShoulderDegrees, double ElbowDegrees, double HipDegrees)
{
    public static readonly AvatarPose Default = new(6, 14, 3);
    public static readonly AvatarPose None = new(0, 0, 0);
}

/// <summary>
/// One face of an avatar box. <paramref name="Corners"/> run TL, TR, BR, BL seen from outside; <paramref name="Uv"/> is the
/// matching template-pixel corners, inset 0.5 px. An undressed face has a null <paramref name="Region"/> and no UV.
/// </summary>
public sealed record AvatarFace(Face Face, Region? Region, Vec3[] Corners, (float U, float V)[] Uv);

/// <summary>
/// A box of the body. <paramref name="Faces"/> is in <see cref="Face"/> order: Up, Right, Front, Left, Back, Down.
/// <paramref name="Min"/> and <paramref name="Max"/> are the box at rest; a posed R15 box is those corners rotated, and the
/// faces' <c>Corners</c> are the geometry. <paramref name="Dressed"/> is true when any face wears the template.
/// </summary>
public sealed record AvatarBox(AvatarPart Part, Vec3 Min, Vec3 Max, IReadOnlyList<AvatarFace> Faces, bool Dressed);

/// <summary>The classic avatar rig and its preview texture (v5.1 spec, Engine). WPF-free.</summary>
public static class Avatar
{
    /// <summary>Roblox "Medium stone grey".</summary>
    public static readonly SKColor BodyColor = new(0xA3, 0xA2, 0xA5);

    const float Inset = 0.5f;

    /// <summary>
    /// The body's boxes: the classic six (torso and the mode's two limbs dressed through <see cref="Template.Slot"/>), or the
    /// 15 of the approximate R15 body. <paramref name="pose"/> applies to R15 only (null = <see cref="AvatarPose.Default"/>).
    /// </summary>
    public static IReadOnlyList<AvatarBox> Build(TemplateMode mode, AvatarBody body = AvatarBody.Classic, AvatarPose? pose = null) =>
        body == AvatarBody.R15 ? BuildR15(mode, pose ?? AvatarPose.Default) : BuildClassic(mode);

    static IReadOnlyList<AvatarBox> BuildClassic(TemplateMode mode)
    {
        bool shirt = mode == TemplateMode.Shirt;
        return new[]
        {
            Box(mode, AvatarPart.Head, new(-0.6, 4, -0.6), new(0.6, 5.2, 0.6), null),
            Box(mode, AvatarPart.Torso, new(-1, 2, -0.5), new(1, 4, 0.5), BodyPart.Body),
            Box(mode, AvatarPart.RightArm, new(-2, 2, -0.5), new(-1, 4, 0.5), shirt ? BodyPart.RightLimb : null),
            Box(mode, AvatarPart.LeftArm, new(1, 2, -0.5), new(2, 4, 0.5), shirt ? BodyPart.LeftLimb : null),
            Box(mode, AvatarPart.RightLeg, new(-1, 0, -0.5), new(0, 2, 0.5), shirt ? null : BodyPart.RightLimb),
            Box(mode, AvatarPart.LeftLeg, new(0, 0, -0.5), new(1, 2, 0.5), shirt ? null : BodyPart.LeftLimb),
        };
    }

    static AvatarBox Box(TemplateMode mode, AvatarPart part, Vec3 min, Vec3 max, BodyPart? slot)
    {
        var faces = new List<AvatarFace>(6);
        foreach (var face in new[] { Face.Up, Face.Right, Face.Front, Face.Left, Face.Back, Face.Down })
        {
            Region? region = slot is { } p ? Template.Slot(mode, p, face) : null;
            faces.Add(new AvatarFace(face, region, Corners(face, min, max), region is { } r ? Uv(r) : Array.Empty<(float, float)>()));
        }
        return new AvatarBox(part, min, max, faces, slot is not null);
    }

    // ---- the approximate R15 body ----
    // Roblox cuts the classic template where the R15 parts meet (DevForum, "Classic Clothing UV Mapping Guidelines [R15]"):
    // on every 128 px tall torso face UpperTorso takes the top 96 rows and LowerTorso the bottom 32; on every 128 px tall
    // limb side the upper limb takes the top 64, the lower limb the next 48 and the hand or foot the last 16. No stud sizes
    // are published, so each part is its template slice at 64 px a stud: the classic silhouette in pieces.

    static IReadOnlyList<AvatarBox> BuildR15(TemplateMode mode, AvatarPose pose)
    {
        bool shirt = mode == TemplateMode.Shirt;
        var boxes = new List<AvatarBox>(15)
        {
            Box(mode, AvatarPart.Head, new(-0.6, 4, -0.6), new(0.6, 5.2, 0.6), null),
            Slice(mode, AvatarPart.UpperTorso, new(-1, 2.5, -0.5), new(1, 4, 0.5), BodyPart.Body, 0, 96, true, false, null),
            Slice(mode, AvatarPart.LowerTorso, new(-1, 2, -0.5), new(1, 2.5, 0.5), BodyPart.Body, 96, 128, false, true, null),
        };

        // Arms: the elbow bend (about X through the elbow line, y 3 and z 0) first, then the shoulder swing (about Z through the
        // top-inner edge of the upper arm). The right side swings toward -X, the left toward +X; the bend is the same on both.
        foreach (var right in new[] { true, false })
        {
            BodyPart? slot = shirt ? (right ? BodyPart.RightLimb : BodyPart.LeftLimb) : null;
            double x0 = right ? -2 : 1, x1 = right ? -1 : 2, pivotX = right ? -1 : 1, swing = right ? -pose.ShoulderDegrees : pose.ShoulderDegrees;
            Vec3 Shoulder(Vec3 v) => RotateZ(v, pivotX, 4, swing);
            Vec3 Elbow(Vec3 v) => Shoulder(RotateX(v, 3, 0, -pose.ElbowDegrees));
            var (upper, lower, hand) = right
                ? (AvatarPart.RightUpperArm, AvatarPart.RightLowerArm, AvatarPart.RightHand)
                : (AvatarPart.LeftUpperArm, AvatarPart.LeftLowerArm, AvatarPart.LeftHand);
            boxes.Add(Slice(mode, upper, new(x0, 3, -0.5), new(x1, 4, 0.5), slot, 0, 64, true, false, Shoulder));
            boxes.Add(Slice(mode, lower, new(x0, 2.25, -0.5), new(x1, 3, 0.5), slot, 64, 112, false, false, Elbow));
            boxes.Add(Slice(mode, hand, new(x0, 2, -0.5), new(x1, 2.25, 0.5), slot, 112, 128, false, true, Elbow));
        }

        // Legs: one swing at the hip (about Z through the top-inner edge), straight knees.
        foreach (var right in new[] { true, false })
        {
            BodyPart? slot = shirt ? null : (right ? BodyPart.RightLimb : BodyPart.LeftLimb);
            double x0 = right ? -1 : 0, x1 = right ? 0 : 1, swing = right ? -pose.HipDegrees : pose.HipDegrees;
            Vec3 Hip(Vec3 v) => RotateZ(v, 0, 2, swing);
            var (upper, lower, foot) = right
                ? (AvatarPart.RightUpperLeg, AvatarPart.RightLowerLeg, AvatarPart.RightFoot)
                : (AvatarPart.LeftUpperLeg, AvatarPart.LeftLowerLeg, AvatarPart.LeftFoot);
            boxes.Add(Slice(mode, upper, new(x0, 1, -0.5), new(x1, 2, 0.5), slot, 0, 64, true, false, Hip));
            boxes.Add(Slice(mode, lower, new(x0, 0.25, -0.5), new(x1, 1, 0.5), slot, 64, 112, false, false, Hip));
            boxes.Add(Slice(mode, foot, new(x0, 0, -0.5), new(x1, 0.25, 0.5), slot, 112, 128, false, true, Hip));
        }
        return boxes;
    }

    /// <summary>
    /// One R15 part: the side faces sample rows <paramref name="rowFrom"/>..<paramref name="rowTo"/> of their region, inset 0.5 px
    /// on every side. The Up and Down caps wear their region only where the part is the chain's outer end
    /// (<paramref name="upOuter"/>, <paramref name="downOuter"/>); an inner cap, where two parts meet, is undressed geometry.
    /// <paramref name="pose"/> rotates the corners rigidly; null leaves the part at rest.
    /// </summary>
    static AvatarBox Slice(TemplateMode mode, AvatarPart part, Vec3 min, Vec3 max, BodyPart? slot, int rowFrom, int rowTo,
        bool upOuter, bool downOuter, Func<Vec3, Vec3>? pose)
    {
        var faces = new List<AvatarFace>(6);
        foreach (var face in new[] { Face.Up, Face.Right, Face.Front, Face.Left, Face.Back, Face.Down })
        {
            Region? region = slot is { } p && face switch { Face.Up => upOuter, Face.Down => downOuter, _ => true }
                ? Template.Slot(mode, p, face)
                : null;
            var uv = region is not { } r
                ? Array.Empty<(float, float)>()
                : face is Face.Up or Face.Down ? Uv(r) : Uv(new Region(r.Name, r.X, r.Y + rowFrom, r.W, rowTo - rowFrom));
            var corners = Corners(face, min, max);
            if (pose is not null) corners = corners.Select(pose).ToArray();
            faces.Add(new AvatarFace(face, region, corners, uv));
        }
        return new AvatarBox(part, min, max, faces, slot is not null);
    }

    static Vec3 RotateZ(Vec3 v, double px, double py, double degrees)
    {
        double a = degrees * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a), dx = v.X - px, dy = v.Y - py;
        return new(px + dx * c - dy * s, py + dx * s + dy * c, v.Z);
    }

    static Vec3 RotateX(Vec3 v, double py, double pz, double degrees)
    {
        double a = degrees * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a), dy = v.Y - py, dz = v.Z - pz;
        return new(v.X, py + dy * c - dz * s, pz + dy * s + dz * c);
    }

    /// <summary>The spec's face table: TL, TR, BR, BL seen from outside the box.</summary>
    static Vec3[] Corners(Face face, Vec3 a, Vec3 b)
    {
        double x0 = a.X, y0 = a.Y, z0 = a.Z, x1 = b.X, y1 = b.Y, z1 = b.Z;
        return face switch
        {
            Face.Front => new Vec3[] { new(x0, y1, z1), new(x1, y1, z1), new(x1, y0, z1), new(x0, y0, z1) },
            Face.Back => new Vec3[] { new(x1, y1, z0), new(x0, y1, z0), new(x0, y0, z0), new(x1, y0, z0) },
            Face.Right => new Vec3[] { new(x0, y1, z0), new(x0, y1, z1), new(x0, y0, z1), new(x0, y0, z0) },
            Face.Left => new Vec3[] { new(x1, y1, z1), new(x1, y1, z0), new(x1, y0, z0), new(x1, y0, z1) },
            Face.Up => new Vec3[] { new(x0, y1, z0), new(x1, y1, z0), new(x1, y1, z1), new(x0, y1, z1) },
            _ => new Vec3[] { new(x0, y0, z1), new(x1, y0, z1), new(x1, y0, z0), new(x0, y0, z0) },
        };
    }

    static (float U, float V)[] Uv(Region r) => new[]
    {
        (r.X + Inset, r.Y + Inset),
        (r.X + r.W - Inset, r.Y + Inset),
        (r.X + r.W - Inset, r.Y + r.H - Inset),
        (r.X + Inset, r.Y + r.H - Inset),
    };

    /// <summary>
    /// The export (<see cref="Renderer.Export"/>) over a fill of <see cref="BodyColor"/>: opaque, template-sized. A transparent
    /// pixel on the clothing shows the body through. The caller disposes the image.
    /// </summary>
    public static SKImage RenderTexture(Document doc)
    {
        using var export = Renderer.Export(doc);
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create texture surface");
        surface.Canvas.Clear(BodyColor);
        surface.Canvas.DrawImage(export, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), null);
        return surface.Snapshot();
    }
}
