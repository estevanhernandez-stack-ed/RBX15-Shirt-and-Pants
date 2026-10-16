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

    /// <summary>
    /// The move drag (editor.js:1169-1171) and the X and Y fields (editor.js:826-827): the layer at (x, y). A value that is
    /// not finite (an empty or garbled field) changes nothing.
    /// </summary>
    public static Document MoveTo(Document doc, int id, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (!double.IsFinite(x) || !double.IsFinite(y)) return doc;
        return Edit(doc, id, l => l with { X = x, Y = y });
    }

    /// <summary>
    /// The Scale slider (editor.js:828-832): <c>scale = value / 100</c> anchored at the top-left, as 4.1 (x and y kept),
    /// under the one clamp (<see cref="SelectionGeometry.ClampScale"/>; props-scale is a clamp-and-undo fix, ruling R6).
    /// </summary>
    public static Document SetScale(Document doc, int id, double scale)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => l with { Scale = SelectionGeometry.ClampScale(scale) });
    }

    /// <summary>
    /// The wheel and the corner handles: the clamped scale with the layer's center kept (editor.js:1185-1190, which 4.1's
    /// resize does and its wheel does not; spec §5 "centered"). An unchanged scale returns the input instance, so the
    /// recomputed x and y can never add a rounding-only step.
    /// </summary>
    public static Document ScaleAboutCenter(Document doc, int id, double scale)
    {
        ArgumentNullException.ThrowIfNull(doc);
        double s = SelectionGeometry.ClampScale(scale);
        return Edit(doc, id, l =>
        {
            if (s == l.Scale) return l;
            var c = SelectionGeometry.Center(l);
            double w = l.Image.Width * s, h = l.Image.Height * s;
            return l with { Scale = s, X = c.X - w / 2, Y = c.Y - h / 2 };
        });
    }

    /// <summary>The Rotation slider (editor.js:833-837) and the rotate handle: <see cref="SelectionGeometry.NormalizeRotation"/>.</summary>
    public static Document SetRotation(Document doc, int id, double degrees)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => l with { Rotation = SelectionGeometry.NormalizeRotation(degrees) });
    }

    /// <summary>Flip H and Flip V (editor.js:843-844): toggles <c>FlipH</c> when <paramref name="horizontal"/>, else <c>FlipV</c>.</summary>
    public static Document Flip(Document doc, int id, bool horizontal)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => horizontal ? l with { FlipH = !l.FlipH } : l with { FlipV = !l.FlipV });
    }

    /// <summary>The Opacity slider (editor.js:838-842): clamped to 0..1, the slider's 0..100 over 100. Not finite: no change.</summary>
    public static Document SetOpacity(Document doc, int id, double opacity)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (!double.IsFinite(opacity)) return doc;
        return Edit(doc, id, l => l with { Opacity = Math.Clamp(opacity, 0, 1) });
    }

    /// <summary>The Name field (editor.js:825): any text, empty allowed as in 4.1; null is stored as "".</summary>
    public static Document SetName(Document doc, int id, string? name)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => l with { Name = name ?? "" });
    }

    /// <summary>
    /// The Image adjustments sliders (editor.js:873-907), each clamped to its slider's range (<see cref="LayerAdjustments"/>).
    /// A field that is not finite keeps the layer's current value.
    /// </summary>
    public static Document SetAdjustments(Document doc, int id, LayerAdjustments adjustments)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(adjustments);
        return Edit(doc, id, l => l with
        {
            Brightness = Range(adjustments.Brightness, 0, 200, l.Brightness),
            Contrast = Range(adjustments.Contrast, 0, 200, l.Contrast),
            Saturate = Range(adjustments.Saturate, 0, 200, l.Saturate),
            HueRotate = Range(adjustments.HueRotate, 0, 360, l.HueRotate),
            RShift = Math.Clamp(adjustments.RShift, -100, 100),
            GShift = Math.Clamp(adjustments.GShift, -100, 100),
            BShift = Math.Clamp(adjustments.BShift, -100, 100),
        });
    }

    /// <summary>Reset adjustments (editor.js:908-911): <see cref="LayerAdjustments.Default"/>.</summary>
    public static Document ResetAdjustments(Document doc, int id) => SetAdjustments(doc, id, LayerAdjustments.Default);

    /// <summary>
    /// Removes the layer at <paramref name="fromIndex"/> and inserts it so it ends at <paramref name="toIndex"/>
    /// (<see cref="Document.Layers"/> order, 0 = bottom). The same instance when the indices are equal or either is out of range.
    /// </summary>
    public static Document Reorder(Document doc, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int n = doc.Layers.Count;
        if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= n || toIndex < 0 || toIndex >= n) return doc;
        var moved = doc.Layers[fromIndex];
        return doc with { Layers = doc.Layers.RemoveAt(fromIndex).Insert(toIndex, moved) };
    }

    /// <summary>
    /// handleLayerDrop's index math (editor.js:621-657): the (from, to) pair for <see cref="Reorder"/> when
    /// <paramref name="dragId"/> is dropped on <paramref name="targetId"/>. <paramref name="above"/> is the row's top half
    /// ('top', editor.js:612), which puts the layer after the target in the array (above it in z-order). Null when either
    /// id is missing or the two are the same layer. A drop that keeps the order gives from == to, which records nothing.
    /// </summary>
    public static (int From, int To)? DropIndices(Document doc, int dragId, int targetId, bool above)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (dragId == targetId) return null;
        int from = doc.Layers.FindIndex(l => l.Id == dragId), over = doc.Layers.FindIndex(l => l.Id == targetId);
        if (from < 0 || over < 0) return null;
        int insert = over > from ? over - 1 : over; // layers.findIndex after the splice (editor.js:645)
        return (from, above ? insert + 1 : insert);
    }

    /// <summary>
    /// addDrawingLayer's tail (editor.js:534-537): the committed drawing pushed on top and selected. The id must be fresh.
    /// </summary>
    public static EditorSnapshot AddDrawing(EditorSnapshot s, Layer layer)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(layer);
        RequireFreshId(s.Document, layer.Id);
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Add(layer) }, layer.Id);
    }

    /// <summary>Replaces layer <paramref name="id"/> with <paramref name="edit"/>'s result; the input instance when the id is missing or the layer is unchanged.</summary>
    static Document Edit(Document doc, int id, Func<Layer, Layer> edit)
    {
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        var before = doc.Layers[i];
        var after = edit(before);
        return after.Equals(before) ? doc : doc with { Layers = doc.Layers.SetItem(i, after) };
    }

    static double Range(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

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

/// <summary>
/// A layer's seven image adjustments, as the properties panel edits them (editor.js:785-797). The slider ranges are
/// Brightness, Contrast and Saturate 0..200, HueRotate 0..360, and each RGB shift -100..100;
/// <see cref="LayerOps.SetAdjustments"/> clamps to them.
/// </summary>
public sealed record LayerAdjustments(double Brightness, double Contrast, double Saturate, double HueRotate, int RShift, int GShift, int BShift)
{
    /// <summary>Reset adjustments' values (editor.js:909-910) and makeLayer's defaults (editor.js:134-142).</summary>
    public static readonly LayerAdjustments Default = new(100, 100, 100, 0, 0, 0, 0);

    /// <summary>The layer's current adjustments.</summary>
    public static LayerAdjustments From(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return new LayerAdjustments(layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate, layer.RShift, layer.GShift, layer.BShift);
    }
}
