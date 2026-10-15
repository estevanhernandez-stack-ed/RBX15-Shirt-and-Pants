using System.Globalization;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// Layer edits from 4.1's LAYERS section (editor.js:499-582) and the Up/Down buttons
/// (editor.js:846-853), as pure functions. Each returns the input instance unchanged when it changes
/// nothing, so the caller records no undo step (divergence <c>undo-empty-steps</c>). Undo and
/// selection policy belong to the caller (EditorSession); these only compute the next state.
/// </summary>
public static class LayerOps
{
    /// <summary>addLayer's fit: 85% of the front slot (editor.js:503).</summary>
    public const double FitFraction = 0.85;

    /// <summary>duplicateLayer's offset on both axes (editor.js:550-551).</summary>
    public const double DuplicateOffset = 10;

    /// <summary>
    /// "Image " + id. 4.1 named an unnamed layer after the already-incremented counter, id + 1
    /// (editor.js:513-514); v5 matches the id (divergence <c>layers-default-name-offbyone</c>).
    /// </summary>
    public static string DefaultName(int id) => "Image " + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// addLayer's placement (editor.js:500-526): scale = min(fw/iw, fh/ih) * 0.85 against the front
    /// body slot, centered in that slot, or centered on <paramref name="dropAt"/> (template pixels).
    /// The arithmetic is 4.1's, operation for operation, so the doubles match. 4.1 read
    /// SHIRT_REGIONS.torso_front, which Pants mode leaves undefined, so adding threw there
    /// (ledger <c>bug-pants-mode-region-keys</c>); the slot resolves to waist_front, same geometry.
    /// A null or empty name takes <see cref="DefaultName"/>, as 4.1's <c>name || ...</c> did.
    /// </summary>
    public static Layer Place(SKImage image, int id, string? name, TemplateMode mode, (double X, double Y)? dropAt = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var front = Template.Slot(mode, BodyPart.Body, Face.Front);
        double scale = Math.Min((double)front.W / image.Width, (double)front.H / image.Height) * FitFraction;
        double x, y;
        if (dropAt is { } p)
        {
            x = p.X - image.Width * scale / 2;
            y = p.Y - image.Height * scale / 2;
        }
        else
        {
            x = front.X + (front.W - image.Width * scale) / 2;
            y = front.Y + (front.H - image.Height * scale) / 2;
        }
        return new Layer
        {
            Id = id,
            Image = image,
            Name = string.IsNullOrEmpty(name) ? DefaultName(id) : name,
            X = x,
            Y = y,
            Scale = scale,
        };
    }

    /// <summary>addLayer's tail (editor.js:521-522): <see cref="Place"/>, push on top, select it.</summary>
    public static EditorSnapshot AddImage(EditorSnapshot s, SKImage image, int id, string? name, (double X, double Y)? dropAt = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        RequireFreshId(s.Document, id);
        var layer = Place(image, id, name, s.Document.Mode, dropAt);
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Add(layer) }, id);
    }

    /// <summary>
    /// removeLayer (editor.js:562-568): removing the selected layer selects the new topmost (last)
    /// layer, or none; removing another keeps the selection. A missing id returns <paramref name="s"/>
    /// (4.1 pushed an empty undo step there; divergence <c>layers-remove</c>).
    /// </summary>
    public static EditorSnapshot Remove(EditorSnapshot s, int id)
    {
        ArgumentNullException.ThrowIfNull(s);
        int i = s.Document.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        var layers = s.Document.Layers.RemoveAt(i);
        int? selected = s.SelectedLayerId == id ? (layers.Count > 0 ? layers[^1].Id : null) : s.SelectedLayerId;
        return new EditorSnapshot(s.Document with { Layers = layers }, selected);
    }

    /// <summary>
    /// duplicateLayer (editor.js:542-560): a copy of every field with the new id, name + " copy",
    /// X and Y + 10, the same image, inserted directly above the original and selected. A missing id
    /// returns <paramref name="s"/>.
    /// </summary>
    public static EditorSnapshot Duplicate(EditorSnapshot s, int id, int newId)
    {
        ArgumentNullException.ThrowIfNull(s);
        int i = s.Document.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        RequireFreshId(s.Document, newId);
        var src = s.Document.Layers[i];
        var clone = src with { Id = newId, Name = src.Name + " copy", X = src.X + DuplicateOffset, Y = src.Y + DuplicateOffset };
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Insert(i + 1, clone) }, newId);
    }

    /// <summary>toggleLayerVisibility (editor.js:570-573). Missing id: the same document.</summary>
    public static Document ToggleVisibility(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        return doc with { Layers = doc.Layers.SetItem(i, doc.Layers[i] with { Visible = !doc.Layers[i].Visible }) };
    }

    /// <summary>btnMoveUp (editor.js:846-849): swap with the layer above (index + 1). No-op at the top or for a missing id.</summary>
    public static Document MoveUp(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        return i < 0 || i == doc.Layers.Count - 1 ? doc : Swap(doc, i, i + 1);
    }

    /// <summary>btnMoveDown (editor.js:850-853): swap with the layer below (index - 1). No-op at the bottom or for a missing id.</summary>
    public static Document MoveDown(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        return i <= 0 ? doc : Swap(doc, i, i - 1);
    }

    /// <summary>
    /// The select tool's pick (editor.js:1100-1111 with pointInRotatedRect, :1043-1053): the topmost
    /// visible layer whose rotated box contains the template point, or null. The box, not pixel alpha
    /// (ledger <c>select-hit-test-rect-not-alpha</c>, kept).
    /// </summary>
    public static int? HitTest(Document doc, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(doc);
        for (int i = doc.Layers.Count - 1; i >= 0; i--)
        {
            var l = doc.Layers[i];
            if (l.Visible && Contains(l, x, y)) return l.Id;
        }
        return null;
    }

    /// <summary>pointInRotatedRect (editor.js:1043-1053) with getLayerCenter (editor.js:1023-1027), operation for operation.</summary>
    static bool Contains(Layer l, double px, double py)
    {
        double cx = l.X + l.Image.Width * l.Scale / 2, cy = l.Y + l.Image.Height * l.Scale / 2;
        double rad = -l.Rotation * Math.PI / 180;
        double dx = px - cx, dy = py - cy;
        double lx = dx * Math.Cos(rad) - dy * Math.Sin(rad);
        double ly = dx * Math.Sin(rad) + dy * Math.Cos(rad);
        double hw = l.Image.Width * l.Scale / 2, hh = l.Image.Height * l.Scale / 2;
        return lx >= -hw && lx <= hw && ly >= -hh && ly <= hh;
    }

    static Document Swap(Document doc, int a, int b)
    {
        var la = doc.Layers[a];
        return doc with { Layers = doc.Layers.SetItem(a, doc.Layers[b]).SetItem(b, la) };
    }

    static void RequireFreshId(Document doc, int id)
    {
        if (doc.Layers.Exists(l => l.Id == id))
            throw new ArgumentException($"layer id {id} is already in the document", nameof(id));
    }
}
