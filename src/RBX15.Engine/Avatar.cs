using SkiaSharp;

namespace RBX15.Engine;

/// <summary>A point or extent in studs (v5.1 spec, Coordinates). WPF's right-handed space, Y up, avatar facing +Z.</summary>
public readonly record struct Vec3(double X, double Y, double Z);

public enum AvatarPart { Head, Torso, RightArm, LeftArm, RightLeg, LeftLeg }

/// <summary>
/// One face of an avatar box. <paramref name="Corners"/> run TL, TR, BR, BL seen from outside; <paramref name="Uv"/> is the
/// matching template-pixel corners, inset 0.5 px. An undressed face has a null <paramref name="Region"/> and no UV.
/// </summary>
public sealed record AvatarFace(Face Face, Region? Region, Vec3[] Corners, (float U, float V)[] Uv);

/// <summary>A box of the classic blocky body. <paramref name="Faces"/> is in <see cref="Face"/> order: Up, Right, Front, Left, Back, Down.</summary>
public sealed record AvatarBox(AvatarPart Part, Vec3 Min, Vec3 Max, IReadOnlyList<AvatarFace> Faces, bool Dressed);

/// <summary>The classic avatar rig and its preview texture (v5.1 spec, Engine). WPF-free.</summary>
public static class Avatar
{
    /// <summary>Roblox "Medium stone grey".</summary>
    public static readonly SKColor BodyColor = new(0xA3, 0xA2, 0xA5);

    const float Inset = 0.5f;

    /// <summary>The six boxes, torso and the mode's two limbs dressed through <see cref="Template.Slot"/>.</summary>
    public static IReadOnlyList<AvatarBox> Build(TemplateMode mode)
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
