using System.Collections.Immutable;

namespace RBX15.Engine;

/// <summary>
/// The Placement buttons (editor.js:1325-1392), the Presets (editor.js:1730-1760) and "Center on front torso"
/// (editor.js:854-859), as pure functions. Every region comes from <see cref="Template.Slot"/> or
/// <see cref="Template.Regions"/>, so all of it works in Pants (4.1 read SHIRT_REGIONS.torso_*, which Pants leaves
/// undefined, and threw; ledger btn-fit-front, btn-fit-fb, btn-stretch, preset-sleeve-pop). Each op returns its input
/// instance when the layer id does not exist, and the document unchanged when the result equals the input, so the
/// caller records no undo step. Clones share the original's image and keep every property the 4.1 spread
/// ({...sel}) copied, are appended on top in the order 4.1 pushed them, and leave the selection on the original.
/// Presets: Meme is <see cref="FitFront"/>, Mirror F+B is <see cref="FrontAndBack"/>, and the Allover and Scatter
/// presets are <see cref="Allover"/> and <see cref="Scatter"/>. Ids come from the caller; a clone's id must be fresh.
/// </summary>
public static class Placement
{
    /// <summary>Fit Front, F+B and Meme leave a 10% margin (editor.js:1329).</summary>
    public const double FitMargin = 0.9;

    /// <summary>Allover, Sleeve Pop and their presets shrink each tile to 80% of its region (editor.js:1357, :1745).</summary>
    public const double TileFraction = 0.8;

    /// <summary>Scatter's face count: 3 copies in a region wider than 100, else 1 (editor.js:1368).</summary>
    public const int WideRegionThreshold = 100;

    /// <summary>
    /// btnFitFront (editor.js:1325-1334): scale = min(w/iw, h/ih) * 0.9 in the Body Front slot, centered in it,
    /// rotation 0 (flips and opacity unchanged). Also the Meme Tee preset.
    /// </summary>
    public static Document FitFront(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        var fitted = Fit(doc.Layers[i], Template.Slot(doc.Mode, BodyPart.Body, Face.Front), FitMargin);
        return Replace(doc, i, fitted);
    }

    /// <summary>
    /// btnFitFB (editor.js:1336-1351): <see cref="FitFront"/>, then a "(back)" clone at the same scale, centered in the
    /// Body Back slot, pushed on top. Not mirrored, despite the preset's "Mirror F+B" name. Also that preset.
    /// </summary>
    public static EditorSnapshot FrontAndBack(EditorSnapshot s, int id, int newId)
    {
        ArgumentNullException.ThrowIfNull(s);
        var doc = s.Document;
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        RequireFresh(doc.Layers, newId);
        var fitted = Fit(doc.Layers[i], Template.Slot(doc.Mode, BodyPart.Body, Face.Front), FitMargin);
        var back = Template.Slot(doc.Mode, BodyPart.Body, Face.Back);
        var clone = Clone(fitted, newId, fitted.Name + " (back)", fitted.Scale, back);
        return With(s, doc.Layers.SetItem(i, fitted).Add(clone));
    }

    /// <summary>
    /// btnStretch (editor.js:1379-1392): fit (no 0.9 factor) into the union of the Body Right, Front and Left slots,
    /// then a "(back)" clone fit to the Body Back slot, also with no margin. 4.1 hard-coded the box as
    /// (165, 74, 260, 128); the slot union is that exact box in Shirt and in Pants (both templates share geometry).
    /// The clone copies the fitted layer, so its rotation is 0 as well.
    /// </summary>
    public static EditorSnapshot Stretch(EditorSnapshot s, int id, int newId)
    {
        ArgumentNullException.ThrowIfNull(s);
        var doc = s.Document;
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        RequireFresh(doc.Layers, newId);
        var fitted = Fit(doc.Layers[i], StretchBox(doc.Mode), 1);
        var back = Template.Slot(doc.Mode, BodyPart.Body, Face.Back);
        var clone = Clone(fitted, newId, fitted.Name + " (back)", FitScale(fitted, back, 1), back);
        return With(s, doc.Layers.SetItem(i, fitted).Add(clone));
    }

    /// <summary>The union of the Body Right, Front and Left slots: (165, 74, 260, 128) in both modes.</summary>
    public static Region StretchBox(TemplateMode mode)
    {
        var right = Template.Slot(mode, BodyPart.Body, Face.Right);
        var front = Template.Slot(mode, BodyPart.Body, Face.Front);
        var left = Template.Slot(mode, BodyPart.Body, Face.Left);
        int x = Math.Min(right.X, Math.Min(front.X, left.X));
        int y = Math.Min(right.Y, Math.Min(front.Y, left.Y));
        int x2 = Math.Max(right.X + right.W, Math.Max(front.X + front.W, left.X + left.W));
        int y2 = Math.Max(right.Y + right.H, Math.Max(front.Y + front.H, left.Y + left.H));
        return new Region("stretch", x, y, x2 - x, y2 - y);
    }

    /// <summary>
    /// btnAllover (editor.js:1353-1362): one "tile" clone per region of <see cref="Template.Regions"/> in table order
    /// (4.1's SHIRT_REGION_DATA and v5's table are both the same 18 regions, caps included), at min(w/iw, h/ih) * 0.8,
    /// centered, rotation 0. The original is untouched and stays selected. Also the Allover preset.
    /// </summary>
    public static EditorSnapshot Allover(EditorSnapshot s, int id, Func<int> nextId)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(nextId);
        var doc = s.Document;
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        var src = doc.Layers[i];
        var layers = doc.Layers;
        foreach (var r in Template.Regions(doc.Mode))
        {
            int cid = nextId();
            RequireFresh(layers, cid);
            layers = layers.Add(Clone(src, cid, src.Name + " tile", FitScale(src, r, TileFraction), r));
        }
        return With(s, layers);
    }

    /// <summary>
    /// btnScatter (editor.js:1364-1377): per region in table order, 3 clones when w &gt; 100 else 1. Per copy, in 4.1's
    /// order of random calls: scale (rand * 0.3 + 0.15) * min(w, h) / max(iw, ih) floored at 0.02, then x = r.x + rand * (w * 0.7),
    /// then y = r.y + rand * (h * 0.7), then rotation = rand * 60 - 30. Four draws per copy, 104 in all. Named "scat"; the
    /// original is untouched. <paramref name="rng"/> is injected: the app passes Random.Shared, tests a seeded Random.
    /// </summary>
    public static EditorSnapshot Scatter(EditorSnapshot s, int id, Func<int> nextId, Random rng)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(nextId);
        ArgumentNullException.ThrowIfNull(rng);
        var doc = s.Document;
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        var src = doc.Layers[i];
        double iw = src.Image.Width, ih = src.Image.Height;
        var layers = doc.Layers;
        foreach (var r in Template.Regions(doc.Mode))
        {
            int count = r.W > WideRegionThreshold ? 3 : 1;
            for (int k = 0; k < count; k++)
            {
                double sc = (rng.NextDouble() * 0.3 + 0.15) * Math.Min(r.W, r.H) / Math.Max(iw, ih);
                double x = r.X + rng.NextDouble() * (r.W * 0.7);
                double y = r.Y + rng.NextDouble() * (r.H * 0.7);
                double rotation = rng.NextDouble() * 60 - 30;
                int cid = nextId();
                RequireFresh(layers, cid);
                layers = layers.Add(src with { Id = cid, Name = src.Name + " scat", Scale = Math.Max(0.02, sc), X = x, Y = y, Rotation = rotation });
            }
        }
        return With(s, layers);
    }

    /// <summary>
    /// The Sleeve Pop preset (editor.js:1740-1748): 8 "sleeve" clones at min(w/iw, h/ih) * 0.8, centered, rotation 0, in
    /// the order right limb front, back, left, right, then left limb front, back, left, right. The limb slots are the
    /// arms in Shirt and the legs in Pants (4.1 threw on the first iteration in Pants). The original does not move.
    /// </summary>
    public static EditorSnapshot SleevePop(EditorSnapshot s, int id, Func<int> nextId)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(nextId);
        var doc = s.Document;
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        var src = doc.Layers[i];
        var layers = doc.Layers;
        foreach (var part in new[] { BodyPart.RightLimb, BodyPart.LeftLimb })
            foreach (var face in new[] { Face.Front, Face.Back, Face.Left, Face.Right })
            {
                var r = Template.Slot(doc.Mode, part, face);
                int cid = nextId();
                RequireFresh(layers, cid);
                layers = layers.Add(Clone(src, cid, src.Name + " sleeve", FitScale(src, r, TileFraction), r));
            }
        return With(s, layers);
    }

    /// <summary>
    /// The Fullbleed preset (editor.js:1753-1759): scale = max(585/iw, 559/ih), centered on the template, rotation 0.
    /// No clamp: the preset stores the exact cover scale, above the 5.0 keyboard cap if need be.
    /// </summary>
    public static Document Fullbleed(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        var l = doc.Layers[i];
        double scale = Math.Max((double)TemplateSize.Width / l.Image.Width, (double)TemplateSize.Height / l.Image.Height);
        return Replace(doc, i, l with
        {
            Scale = scale,
            X = (TemplateSize.Width - l.Image.Width * scale) / 2,
            Y = (TemplateSize.Height - l.Image.Height * scale) / 2,
            Rotation = 0,
        });
    }

    /// <summary>
    /// btnCenterFront (editor.js:854-859): x and y put the unrotated scaled box in the middle of the Body Front slot.
    /// Scale and rotation unchanged (a rotated layer is centered by its box, as the center anchor is).
    /// </summary>
    public static Document CenterOnFront(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        var l = doc.Layers[i];
        var front = Template.Slot(doc.Mode, BodyPart.Body, Face.Front);
        return Replace(doc, i, l with
        {
            X = front.X + (front.W - l.Image.Width * l.Scale) / 2,
            Y = front.Y + (front.H - l.Image.Height * l.Scale) / 2,
        });
    }

    static double FitScale(Layer l, Region r, double fraction) =>
        Math.Min((double)r.W / l.Image.Width, (double)r.H / l.Image.Height) * fraction;

    /// <summary>The layer scaled to fit <paramref name="r"/> by <paramref name="fraction"/>, centered in it, rotation 0.</summary>
    static Layer Fit(Layer l, Region r, double fraction)
    {
        double scale = FitScale(l, r, fraction);
        return l with
        {
            Scale = scale,
            X = r.X + (r.W - l.Image.Width * scale) / 2,
            Y = r.Y + (r.H - l.Image.Height * scale) / 2,
            Rotation = 0,
        };
    }

    /// <summary>A copy of <paramref name="src"/> (4.1's {...sel}) at <paramref name="scale"/>, centered in <paramref name="r"/>, rotation 0.</summary>
    static Layer Clone(Layer src, int id, string name, double scale, Region r) => src with
    {
        Id = id,
        Name = name,
        Scale = scale,
        X = r.X + (r.W - src.Image.Width * scale) / 2,
        Y = r.Y + (r.H - src.Image.Height * scale) / 2,
        Rotation = 0,
    };

    static Document Replace(Document doc, int index, Layer after) =>
        after.Equals(doc.Layers[index]) ? doc : doc with { Layers = doc.Layers.SetItem(index, after) };

    static EditorSnapshot With(EditorSnapshot s, ImmutableList<Layer> layers) =>
        new(s.Document with { Layers = layers }, s.SelectedLayerId);

    static void RequireFresh(ImmutableList<Layer> layers, int id)
    {
        if (layers.Exists(l => l.Id == id))
            throw new ArgumentException($"layer id {id} is already in the document", nameof(id));
    }
}
