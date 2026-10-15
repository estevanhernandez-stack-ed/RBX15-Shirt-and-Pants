using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Template Regions group (buildRegionGrid, editor.js:1615-1687; ledger
/// <c>regions-grid-layout-shirt</c>, <c>regions-grid-layout-pants</c>, keep). Fourteen tiles: the body
/// cross (six faces) and four faces per limb; limb up and down have no tile, as in 4.1. Keys come from
/// the semantic slots (<see cref="Template.Slot"/>), so Pants tiles write <c>waist_</c>, <c>rleg_</c>
/// and <c>lleg_</c> keys. A mode change rebuilds the tiles (editor.js:1715); any other document change
/// updates each tile's fill, so undo, redo and live edits repaint the swatches (editor.js:1445-1448,
/// :1592-1595).
/// </summary>
public sealed class RegionsGroupViewModel : ObservableObject
{
    // editor.js:1626-1628 in document order: Top, R, Front, L, Back, Bot.
    static readonly (Face Face, string Label)[] BodyFaces =
        [(Face.Up, "Top"), (Face.Right, "R"), (Face.Front, "Front"), (Face.Left, "L"), (Face.Back, "Back"), (Face.Down, "Bot")];

    // editor.js:1634-1637: the right limb runs L, Bk, R, Fr.
    static readonly (Face Face, string Label)[] Limb1Faces =
        [(Face.Left, "L"), (Face.Back, "Bk"), (Face.Right, "R"), (Face.Front, "Fr")];

    // editor.js:1643-1646: the left limb runs Fr, L, Bk, R (mirrored to the atlas; keep).
    static readonly (Face Face, string Label)[] Limb2Faces =
        [(Face.Front, "Fr"), (Face.Left, "L"), (Face.Back, "Bk"), (Face.Right, "R")];

    readonly EditorSession _session;
    readonly FillStyleViewModel _fillStyle;
    TemplateMode _mode;

    public RegionsGroupViewModel(EditorSession session, FillStyleViewModel fillStyle)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fillStyle = fillStyle ?? throw new ArgumentNullException(nameof(fillStyle));
        Build(session.Document.Mode);
        session.Changed += (_, e) =>
        {
            if (!e.What.HasFlag(SessionChange.Document)) return;
            var doc = _session.Document;
            if (doc.Mode != _mode)
            {
                Build(doc.Mode);
                OnPropertyChanged(nameof(BodyLabel));
                OnPropertyChanged(nameof(LimbLabel1));
                OnPropertyChanged(nameof(LimbLabel2));
                OnPropertyChanged(nameof(Body));
                OnPropertyChanged(nameof(Limb1));
                OnPropertyChanged(nameof(Limb2));
                return;
            }
            foreach (var tile in Body.Concat(Limb1).Concat(Limb2))
                tile.Fill = DocumentOps.RegionFill(doc, tile.Region);
        };
    }

    /// <summary>"Torso" or "Waist" (editor.js:1622). 4.1 never rendered it; v5 uses it in the body tiles' automation names.</summary>
    public string BodyLabel => _mode == TemplateMode.Shirt ? "Torso" : "Waist";

    /// <summary>"Right Arm" or "Right Leg" (editor.js:1620).</summary>
    public string LimbLabel1 => _mode == TemplateMode.Shirt ? "Right Arm" : "Right Leg";

    /// <summary>"Left Arm" or "Left Leg" (editor.js:1621).</summary>
    public string LimbLabel2 => _mode == TemplateMode.Shirt ? "Left Arm" : "Left Leg";

    /// <summary>Up, Right, Front, Left, Back, Down: the cross's cells in editor.js:1626-1628 order.</summary>
    public IReadOnlyList<RegionTileViewModel> Body { get; private set; } = [];

    public IReadOnlyList<RegionTileViewModel> Limb1 { get; private set; } = [];

    public IReadOnlyList<RegionTileViewModel> Limb2 { get; private set; } = [];

    void Build(TemplateMode mode)
    {
        _mode = mode;
        Body = Tiles(BodyPart.Body, BodyLabel, BodyFaces);
        Limb1 = Tiles(BodyPart.RightLimb, LimbLabel1, Limb1Faces);
        Limb2 = Tiles(BodyPart.LeftLimb, LimbLabel2, Limb2Faces);
    }

    /// <summary>
    /// Contains the tile's visible text (WCAG 2.5.3 label in name): "Torso Front", "Torso Bot (Bottom)",
    /// "Right Arm Bk (Back)". The spelled-out face follows only when the tile abbreviates it.
    /// </summary>
    static string AutomationName(string partLabel, Face face, string label)
    {
        string full = face switch { Face.Up => "Top", Face.Down => "Bottom", _ => face.ToString() };
        return label == full ? $"{partLabel} {label}" : $"{partLabel} {label} ({full})";
    }

    RegionTileViewModel[] Tiles(BodyPart part, string partLabel, (Face Face, string Label)[] faces) =>
        faces.Select(f => new RegionTileViewModel(
            _session, _fillStyle, _mode,
            Template.Slot(_mode, part, f.Face).Name,
            f.Label,
            AutomationName(partLabel, f.Face, f.Label),
            isFront: part == BodyPart.Body && f.Face == Face.Front)).ToArray();
}
