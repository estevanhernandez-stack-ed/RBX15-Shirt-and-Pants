using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The Home tab's document edits: mode switch, region paint and clear, the fill-style live-edit
/// retarget, region fit and Clear. Pure functions over the immutable <see cref="Document"/>. Each one
/// returns the input instance itself when it changes nothing, so a caller can tell a no-op by
/// reference (EditorSession also compares content; spec §5, no empty undo steps, ledger
/// <c>undo-empty-steps</c>).
/// </summary>
public static class DocumentOps
{
    /// <summary>The region-click fit fraction (editor.js:1663). Meme and Fit use 0.9 and Allover 0.8; each keeps its own.</summary>
    public const double RegionFitFraction = 0.85;

    /// <summary>
    /// switchMode (editor.js:1695-1717): sets the mode and empties every region fill, because the two
    /// modes use different keys (editor.js:1710-1711). Layers, background, mask and grid are kept
    /// (ledger <c>mode-switch-effects</c>). Switching to the current mode still empties the fills, as
    /// 4.1's switchMode did; with no fills it changes nothing.
    /// </summary>
    public static Document SwitchMode(Document doc, TemplateMode mode)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (doc.Mode == mode && doc.RegionFills.IsEmpty) return doc;
        return doc with { Mode = mode, RegionFills = doc.RegionFills.Clear() };
    }

    /// <summary>modeBtn.onclick (editor.js:1719-1725): the opposite mode. One undo step with its fill wipe (<c>mode-switch-undo-and-callers</c>).</summary>
    public static Document ToggleMode(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return SwitchMode(doc, doc.Mode == TemplateMode.Shirt ? TemplateMode.Pants : TemplateMode.Shirt);
    }

    /// <summary><c>regionColors[r]</c>: the region's fill, or null when it has none.</summary>
    public static Fill? RegionFill(Document doc, string region)
    {
        ArgumentNullException.ThrowIfNull(doc);
        foreach (var (name, fill) in doc.RegionFills)
            if (name == region) return fill;
        return null;
    }

    /// <summary>
    /// The region click with no layer selected (editor.js:1668-1674, ledger <c>regions-click-paint</c>):
    /// stores <paramref name="fill"/> under the region. An existing entry is replaced where it stands,
    /// as a JS object keeps a key's position on assignment; a new key is appended. Repainting with an
    /// equal fill changes nothing (the row's fix: 4.1 pushed an undo step anyway).
    /// </summary>
    /// <exception cref="ArgumentException">The region is not one of the document mode's 18 keys.</exception>
    public static Document PaintRegion(Document doc, string region, Fill fill)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(fill);
        RequireRegion(doc.Mode, region);
        int at = IndexOf(doc, region);
        if (at < 0) return doc with { RegionFills = doc.RegionFills.Add((region, fill)) };
        if (doc.RegionFills[at].Fill == fill) return doc;
        return doc with { RegionFills = doc.RegionFills.SetItem(at, (region, fill)) };
    }

    /// <summary>
    /// The region right-click (editor.js:1677-1684, ledger <c>regions-right-click-clear</c>): deletes the
    /// region's fill. A region without a fill changes nothing (4.1 returned before saveUndo, :1679).
    /// Any key may be cleared, including a key the mode lacks.
    /// </summary>
    public static Document ClearRegion(Document doc, string region)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int at = IndexOf(doc, region);
        return at < 0 ? doc : doc with { RegionFills = doc.RegionFills.RemoveAt(at) };
    }

    /// <summary>
    /// applyFillEdit (editor.js:1581-1598, ledger <c>fillstyle-live-edit-rule</c>, keep): every region
    /// whose fill key equals <paramref name="beforeKey"/> (the current fill's key before the edit) takes
    /// <paramref name="next"/>, in place. Matching is by <see cref="Fill.Key"/>, so two groups whose keys
    /// an edit makes equal move together from then on, as in 4.1. Every entry is considered, including
    /// keys the mode lacks (4.1 looped over all of regionColors, :1585).
    /// </summary>
    public static Document RetargetFill(Document doc, string beforeKey, Fill next)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(beforeKey);
        ArgumentNullException.ThrowIfNull(next);
        var fills = doc.RegionFills;
        for (int i = 0; i < fills.Count; i++)
        {
            var (name, fill) = fills[i];
            if (fill.Key == beforeKey && fill != next) fills = fills.SetItem(i, (name, next));
        }
        return ReferenceEquals(fills, doc.RegionFills) ? doc : doc with { RegionFills = fills };
    }

    /// <summary>
    /// The region click with a layer selected (editor.js:1658-1667, ledger <c>regions-click-fit-layer</c>):
    /// scale = min(r.w / iw, r.h / ih) * 0.85, centered in the region, rotation 0. Flips, opacity and
    /// adjustments are kept. A missing layer, or a fit that lands where the layer already is, changes
    /// nothing.
    /// </summary>
    /// <exception cref="ArgumentException">The region is not one of the document mode's 18 keys.</exception>
    public static Document FitLayerToRegion(Document doc, int layerId, string region)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var r = RequireRegion(doc.Mode, region);
        int at = doc.Layers.FindIndex(l => l.Id == layerId);
        if (at < 0) return doc;
        var layer = doc.Layers[at];
        double iw = layer.Image.Width, ih = layer.Image.Height;
        double scale = Math.Min(r.W / iw, r.H / ih) * RegionFitFraction;
        var fitted = layer with
        {
            Scale = scale,
            X = r.X + (r.W - iw * scale) / 2,
            Y = r.Y + (r.H - ih * scale) / 2,
            Rotation = 0,
        };
        return fitted == layer ? doc : doc with { Layers = doc.Layers.SetItem(at, fitted) };
    }

    /// <summary>
    /// btnClear (editor.js:1394, ledger <c>btn-clear</c>, keep): removes every layer. Fills, background,
    /// mode, mask and grid stay. With no layers it changes nothing.
    /// </summary>
    public static Document ClearLayers(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return doc.Layers.IsEmpty ? doc : doc with { Layers = doc.Layers.Clear() };
    }

    /// <summary>
    /// A background swatch click or color pick (editor.js:922, :930; ledger <c>bg-swatches</c>, <c>bg-color-pick-clears-image</c>,
    /// keep): sets <see cref="Document.BgColor"/> and CLEARS <see cref="Document.BgImage"/>. Opacity is not touched here
    /// (4.1 never touched it; the app's rule for a transparent background lives in BackgroundViewModel). Setting the color the
    /// document already has, with no image, changes nothing. The hex is stored lowercase, as an input of type color reports it.
    /// </summary>
    /// <exception cref="ArgumentException">Not <c>#rrggbb</c>.</exception>
    public static Document SetBackgroundColor(Document doc, string hex)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != 7 || hex[0] != '#' || hex.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF"))
            throw new ArgumentException($"'{hex}' is not #rrggbb.", nameof(hex));
        string color = hex.ToLowerInvariant();
        if (doc.BgImage is null && string.Equals(doc.BgColor, color, StringComparison.Ordinal)) return doc;
        return doc with { BgColor = color, BgImage = null };
    }

    /// <summary>
    /// The Background button's pick (editor.js:934-946; ledger <c>bg-image</c>, fix): the image becomes the background,
    /// drawn stretched over the whole template (editor.js:304-307). The color stays, as 4.1 kept it. The document keeps
    /// the image by reference; the caller owns decoding it (<see cref="Images.Decode"/>, so the caps apply). The image
    /// the document already holds changes nothing.
    /// </summary>
    public static Document SetBackgroundImage(Document doc, SKImage image)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(image);
        return ReferenceEquals(doc.BgImage, image) ? doc : doc with { BgImage = image };
    }

    /// <summary>
    /// The Opacity slider (editor.js:932; ledger <c>bg-color-opacity</c>): <see cref="Document.BgOpacity"/> to
    /// <paramref name="opacity"/>, 0 to 1 (clamped), applying to the color and the image alike.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">NaN.</exception>
    public static Document SetBackgroundOpacity(Document doc, double opacity)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (double.IsNaN(opacity)) throw new ArgumentOutOfRangeException(nameof(opacity), "NaN is not an opacity.");
        double value = Math.Clamp(opacity, 0, 1);
        return doc.BgOpacity.Equals(value) ? doc : doc with { BgOpacity = value };
    }

    static int IndexOf(Document doc, string region) => doc.RegionFills.FindIndex(e => e.Region == region);

    static Region RequireRegion(TemplateMode mode, string region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Template.TryFind(mode, region, out var r)
            ? r
            : throw new ArgumentException($"'{region}' is not a {mode} region.", nameof(region));
    }
}
