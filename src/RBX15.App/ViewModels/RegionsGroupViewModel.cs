using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    // v5: each limb's top and bottom, labelled as the body's are (ledger regions-limb-caps).
    static readonly (Face Face, string Label)[] LimbCaps = [(Face.Up, "Top"), (Face.Down, "Bot")];

    readonly EditorSession _session;
    readonly FillStyleViewModel _fillStyle;
    TemplateMode _mode;

    public RegionsGroupViewModel(EditorSession session, FillStyleViewModel fillStyle)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fillStyle = fillStyle ?? throw new ArgumentNullException(nameof(fillStyle));
        Build(session.Document.Mode);
        FillAllCommand = new RelayCommand(() => _session.Apply(d =>
            Template.Regions(d.Mode).Aggregate(d, (doc, r) => DocumentOps.PaintRegion(doc, r.Name, _fillStyle.Current))));
        ClearAllCommand = new RelayCommand(() => _session.Apply(d => d.RegionFills.IsEmpty ? d : d with { RegionFills = d.RegionFills.Clear() }));
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
                OnPropertyChanged(nameof(Limb1Top));
                OnPropertyChanged(nameof(Limb1Bot));
                OnPropertyChanged(nameof(Limb2Top));
                OnPropertyChanged(nameof(Limb2Bot));
                return;
            }
            foreach (var tile in AllTiles)
                tile.Fill = DocumentOps.RegionFill(doc, tile.Region);
        };
    }

    /// <summary>
    /// The All tile (Este, 2026-10-18; ledger regions-fill-all, new): paints every region of the mode with the current
    /// Fill Style as one undo step. Regions sharing a fill share one gradient or pattern across all of them, as 4.1 draws
    /// any group of regions with the same fill. Unlike a region tile it never fits the selected layer.
    /// </summary>
    public IRelayCommand FillAllCommand { get; }

    /// <summary>The All tile's right-click and Delete: clears every region fill, one undo step.</summary>
    public IRelayCommand ClearAllCommand { get; }

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

    /// <summary>
    /// The limbs' top (shoulder or hip) and bottom (hand or foot opening) regions. 4.1's grid had no button for them
    /// (editor.js:1630-1646), so they could not be filled (ledger regions-limb-caps, fix).
    /// </summary>
    public RegionTileViewModel Limb1Top { get; private set; } = null!;

    public RegionTileViewModel Limb1Bot { get; private set; } = null!;

    public RegionTileViewModel Limb2Top { get; private set; } = null!;

    public RegionTileViewModel Limb2Bot { get; private set; } = null!;

    /// <summary>Every tile: the cross, both limbs' sides, then their tops and bottoms.</summary>
    public IEnumerable<RegionTileViewModel> AllTiles =>
        Body.Concat(Limb1).Concat(Limb2).Concat([Limb1Top, Limb1Bot, Limb2Top, Limb2Bot]);

    void Build(TemplateMode mode)
    {
        _mode = mode;
        Body = Tiles(BodyPart.Body, BodyLabel, BodyFaces);
        Limb1 = Tiles(BodyPart.RightLimb, LimbLabel1, Limb1Faces);
        Limb2 = Tiles(BodyPart.LeftLimb, LimbLabel2, Limb2Faces);
        var caps1 = Tiles(BodyPart.RightLimb, LimbLabel1, LimbCaps);
        var caps2 = Tiles(BodyPart.LeftLimb, LimbLabel2, LimbCaps);
        (Limb1Top, Limb1Bot, Limb2Top, Limb2Bot) = (caps1[0], caps1[1], caps2[0], caps2[1]);
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
