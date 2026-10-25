using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The left panel's "Selected layer" section (editor.html:320-323; updateUI's controls, editor.js:746-912;
/// ledger <c>layers-selected-panel</c>). It shows the selected layer's Name, X and Y, Scale, Rotation,
/// Opacity, Flip H and Flip V, Duplicate, Up and Down, and the collapsible Image adjustments with Reset.
/// "Center on front torso" (editor.js:776-778) is <c>props-center-front</c>: a full-width button under Up and Down
/// (M4b item 5; it was absent until then, interface ruling R13).
/// <para>
/// <b>One undo step per edit</b> (spec §5; every <c>props-*</c> row is a fix because 4.1 pushed none). A
/// slider drag is one <see cref="BeginSliderDrag"/> gesture; a keyboard slider change, a button, and a typed
/// field committed on Enter or focus loss are one <see cref="EditorSession.Apply"/> each, and a commit that
/// changes nothing records nothing.
/// </para>
/// <para>
/// <b>Live, never rebuilt</b> (<c>props-live-refresh</c>, <c>props-panel-rebuild-resets</c>). 4.1 rebuilt the
/// whole panel on every updateUI (editor.js:753), collapsing the adjustments and dropping focus, and left X/Y
/// stale during a move drag (editor.js:1191-1192). Here every property is re-read from the selected layer on
/// each Document or Selection change, including each move of a canvas drag, and only the properties whose
/// value changed are raised, so the controls, their focus and <see cref="AdjustmentsExpanded"/> survive.
/// </para>
/// </summary>
public sealed partial class LayerPropertiesViewModel : ObservableObject
{
    /// <summary>The scale slider's range in percent (editor.js:762), equal to the engine's one clamp (props-scale-clamp-inconsistency).</summary>
    public const double MinScalePercent = SelectionGeometry.MinScale * 100, MaxScalePercent = SelectionGeometry.MaxScale * 100;

    /// <summary>The rotation slider's range (editor.js:764).</summary>
    public const double MinRotation = -180, MaxRotation = 180;

    /// <summary>The adjustment slider ranges (editor.js:786-799).</summary>
    public const double MaxPercentAdjust = 200, MaxHue = 360;
    public const int MinShift = -100, MaxShift = 100;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Every displayed property, read from the layer shown (null: the empty state). It is the one definition:
    /// each getter reads its own row, and Refresh raises the rows whose value differs.
    /// </summary>
    static readonly (string Name, Func<Layer?, object> Read)[] Shown =
    [
        (nameof(HasSelection), l => l is not null),
        (nameof(Name), l => l?.Name ?? ""),
        (nameof(XText), l => l is null ? "" : ToFixed0(l.X)),
        (nameof(YText), l => l is null ? "" : ToFixed0(l.Y)),
        (nameof(PositionLabel), l => l is null ? "" : "X: " + ToFixed0(l.X) + " \u00A0 Y: " + ToFixed0(l.Y)),
        (nameof(ScalePercent), l => l is null ? 100.0 : l.Scale * 100),
        (nameof(ScaleLabel), l => l is null ? "" : ToFixed0(l.Scale * 100) + "%"),
        (nameof(Rotation), l => l?.Rotation ?? 0.0),
        (nameof(RotationLabel), l => l is null ? "" : WholeDegrees(l.Rotation) + "\u00B0"),
        (nameof(OpacityPercent), l => l is null ? 100.0 : l.Opacity * 100),
        (nameof(OpacityLabel), l => l is null ? "" : ToFixed0(l.Opacity * 100) + "%"),
        (nameof(Brightness), l => l?.Brightness ?? 100.0),
        (nameof(Contrast), l => l?.Contrast ?? 100.0),
        (nameof(Saturate), l => l?.Saturate ?? 100.0),
        (nameof(HueRotate), l => l?.HueRotate ?? 0.0),
        (nameof(RShift), l => l?.RShift ?? 0),
        (nameof(GShift), l => l?.GShift ?? 0),
        (nameof(BShift), l => l?.BShift ?? 0),
        (nameof(BrightnessLabel), l => l is null ? "" : Num(l.Brightness) + "%"),
        (nameof(ContrastLabel), l => l is null ? "" : Num(l.Contrast) + "%"),
        (nameof(SaturateLabel), l => l is null ? "" : Num(l.Saturate) + "%"),
        (nameof(HueLabel), l => l is null ? "" : Num(l.HueRotate) + "\u00B0"),
        (nameof(RLabel), l => l is null ? "" : l.RShift.ToString(Inv)),
        (nameof(GLabel), l => l is null ? "" : l.GShift.ToString(Inv)),
        (nameof(BLabel), l => l is null ? "" : l.BShift.ToString(Inv)),
    ];

    static readonly Dictionary<string, Func<Layer?, object>> ShownByName = Shown.ToDictionary(p => p.Name, p => p.Read);

    readonly EditorSession _session;
    Layer? _layer;
    bool _syncing;
    bool _adjustmentsExpanded;

    public LayerPropertiesViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        FlipHCommand = new RelayCommand(() => Edit((d, id) => LayerOps.Flip(d, id, horizontal: true)), () => HasSelection);
        FlipVCommand = new RelayCommand(() => Edit((d, id) => LayerOps.Flip(d, id, horizontal: false)), () => HasSelection);
        // btnDupe -> duplicateLayer(sel.id) (editor.js:845, :542-560), as the row's Dupe does.
        DuplicateCommand = new RelayCommand(Duplicate, () => HasSelection);
        // btnMoveUp / btnMoveDown (editor.js:846-853): disabled at the ends (layers-move-updown).
        MoveUpCommand = new RelayCommand(() => Edit(LayerOps.MoveUp), () => Index() is int i && i < _session.Document.Layers.Count - 1);
        MoveDownCommand = new RelayCommand(() => Edit(LayerOps.MoveDown), () => Index() is > 0);
        ResetAdjustmentsCommand = new RelayCommand(() => Edit(LayerOps.ResetAdjustments), () => HasSelection);
        // btnCenterFront (editor.js:776-778, :854-859): one step, scale and rotation untouched (props-center-front, fix).
        CenterFrontCommand = new RelayCommand(() => PlacementActions.CenterOnFront(_session), () => HasSelection);
        _session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0) Refresh();
        };
        Refresh();
    }

    public bool HasSelection => Get<bool>();

    /// <summary>The empty state (editor.js:750; ledger layers-empty-state). The view upper-cases it (.info, styles.css:640-647).</summary>
    public string EmptyText => "Select a layer to edit.";

    /// <summary>#ctrlName (editor.js:755, :811, :825). The view commits it on Enter or focus loss. Empty is allowed, as 4.1.</summary>
    public string Name
    {
        get => Get<string>();
        set => Edit((d, id) => LayerOps.SetName(d, id, value));
    }

    /// <summary>#ctrlX, shown toFixed(0) (editor.js:812). See <see cref="CommitPosition"/>.</summary>
    public string XText
    {
        get => Get<string>();
        set => CommitPosition(value, horizontal: true);
    }

    /// <summary>#ctrlY, shown toFixed(0) (editor.js:813).</summary>
    public string YText
    {
        get => Get<string>();
        set => CommitPosition(value, horizontal: false);
    }

    /// <summary>The label above X and Y: "X: 12 &#160; Y: 34" (editor.js:756, toFixed(0), space nbsp space).</summary>
    public string PositionLabel => Get<string>();

    /// <summary>#ctrlScale, 1..500 percent (editor.js:762, :814, :828-832). The slider anchors at the top-left (interface R6).</summary>
    public double ScalePercent
    {
        get => Get<double>();
        set
        {
            // Controller ruling (Task 3): non-finite input is refused like X/Y; SetScale would write 1% as a real step.
            if (RefusedNonFinite(value)) return;
            Edit((d, id) => LayerOps.SetScale(d, id, Whole(value, MinScalePercent, MaxScalePercent) / 100));
        }
    }

    /// <summary>#scaleVal: (scale*100).toFixed(0) + "%" (editor.js:761).</summary>
    public string ScaleLabel => Get<string>();

    /// <summary>#ctrlRot, -180..180 whole degrees (editor.js:764, :815, :833-837).</summary>
    public double Rotation
    {
        get => Get<double>();
        set
        {
            // Controller ruling (Task 3): non-finite input is refused like X/Y; SetRotation would write 0 degrees as a real step.
            if (RefusedNonFinite(value)) return;
            Edit((d, id) => LayerOps.SetRotation(d, id, Whole(value, MinRotation, MaxRotation)));
        }
    }

    /// <summary>
    /// #rotVal. 4.1 printed the raw float (editor.js:763, "37.28194618°" after a handle drag). v5 shows whole
    /// degrees, half away from zero, never "-0" (props-rotation-label-unrounded, fix): the slider's own unit.
    /// </summary>
    public string RotationLabel => Get<string>();

    /// <summary>#ctrlOpa, 0..100 percent (editor.js:766, :816, :838-842).</summary>
    public double OpacityPercent
    {
        get => Get<double>();
        set
        {
            if (RefusedNonFinite(value)) return;
            Edit((d, id) => LayerOps.SetOpacity(d, id, Whole(value, 0, 100) / 100));
        }
    }

    /// <summary>#opaVal: (opacity*100).toFixed(0) + "%" (editor.js:765).</summary>
    public string OpacityLabel => Get<string>();

    /// <summary>#ctrlBright 0..200 (editor.js:786, :873-877).</summary>
    public double Brightness
    {
        get => Get<double>();
        set
        {
            if (RefusedNonFinite(value)) return;
            Adjust(a => a with { Brightness = Whole(value, 0, MaxPercentAdjust) });
        }
    }

    /// <summary>#ctrlContrast 0..200 (editor.js:788, :878-882).</summary>
    public double Contrast
    {
        get => Get<double>();
        set
        {
            if (RefusedNonFinite(value)) return;
            Adjust(a => a with { Contrast = Whole(value, 0, MaxPercentAdjust) });
        }
    }

    /// <summary>#ctrlSaturate 0..200 (editor.js:790, :883-887).</summary>
    public double Saturate
    {
        get => Get<double>();
        set
        {
            if (RefusedNonFinite(value)) return;
            Adjust(a => a with { Saturate = Whole(value, 0, MaxPercentAdjust) });
        }
    }

    /// <summary>#ctrlHue 0..360 (editor.js:792, :888-892).</summary>
    public double HueRotate
    {
        get => Get<double>();
        set
        {
            if (RefusedNonFinite(value)) return;
            Adjust(a => a with { HueRotate = Whole(value, 0, MaxHue) });
        }
    }

    /// <summary>#ctrlR -100..100 (editor.js:795, :893-897).</summary>
    public int RShift
    {
        get => Get<int>();
        set => Adjust(a => a with { RShift = Math.Clamp(value, MinShift, MaxShift) });
    }

    /// <summary>#ctrlG -100..100 (editor.js:797, :898-902).</summary>
    public int GShift
    {
        get => Get<int>();
        set => Adjust(a => a with { GShift = Math.Clamp(value, MinShift, MaxShift) });
    }

    /// <summary>#ctrlB -100..100 (editor.js:799, :903-907).</summary>
    public int BShift
    {
        get => Get<int>();
        set => Adjust(a => a with { BShift = Math.Clamp(value, MinShift, MaxShift) });
    }

    /// <summary>
    /// #brightVal and the rest show the stored value. 4.1's <c>sel.brightness || 100</c> showed a stored 0 as 100
    /// after any rebuild (editor.js:785-791, :817-819; props-falsy-zero-adjust-display, fix): here 0 shows "0%".
    /// </summary>
    public string BrightnessLabel => Get<string>();

    public string ContrastLabel => Get<string>();

    public string SaturateLabel => Get<string>();

    /// <summary>#hueVal: value + "°" (editor.js:791, :890).</summary>
    public string HueLabel => Get<string>();

    /// <summary>#rVal, #gVal, #bVal: the plain number (editor.js:794-798, :895, :900, :905).</summary>
    public string RLabel => Get<string>();

    public string GLabel => Get<string>();

    public string BLabel => Get<string>();

    /// <summary>
    /// Whether Image adjustments is open (#adjToggle, editor.js:780-783, :862-872). View state, not document
    /// state: closed at start as 4.1 (display:none, editor.js:784), and kept across every edit, Reset and selection
    /// change (props-adjust-panel, props-adjust-reset and props-panel-rebuild-resets, fix: 4.1 collapsed it on
    /// every rebuild).
    /// </summary>
    public bool AdjustmentsExpanded
    {
        get => _adjustmentsExpanded;
        set => SetProperty(ref _adjustmentsExpanded, value);
    }

    /// <summary>btnFlipH (editor.js:768, :843): one step (props-flip, fix).</summary>
    public IRelayCommand FlipHCommand { get; }

    /// <summary>btnFlipV (editor.js:769, :844): one step.</summary>
    public IRelayCommand FlipVCommand { get; }

    public IRelayCommand DuplicateCommand { get; }

    public IRelayCommand MoveUpCommand { get; }

    public IRelayCommand MoveDownCommand { get; }

    /// <summary>btnResetAdj (editor.js:801, :908-912): all seven to their defaults, one step (props-adjust-reset, fix).</summary>
    public IRelayCommand ResetAdjustmentsCommand { get; }

    /// <summary>
    /// "Center on front torso" (editor.js:854-859; ledger props-center-front): the layer's unrotated box goes to the middle of
    /// the Body Front slot, in Shirt and in Pants (the label stays "front torso" in both). One undo step; a layer already
    /// there records nothing.
    /// </summary>
    public IRelayCommand CenterFrontCommand { get; }

    /// <summary>One slider drag as one undo step (<see cref="EditorSession.BeginGesture"/>): it records one step iff the drag changed the layer.</summary>
    public IDisposable BeginSliderDrag() => _session.BeginGesture();

    /// <summary>
    /// Set by the view: true while the text field bound to the named property (<see cref="Name"/>, <see cref="XText"/>
    /// or <see cref="YText"/>) holds typing not yet committed. A Document-only change of the shown layer (a wheel burst,
    /// a canvas drag, a panel key) does not raise that field, so WPF never writes the layer's value over the typing;
    /// Enter or focus loss then commits it (final fix wave 3b).
    /// </summary>
    public Func<string, bool>? HasUncommittedTyping { get; set; }

    /// <summary>
    /// X and Y commit (onchange, editor.js:826-827). Text equal to the shown value changes nothing (4.1's change
    /// event fires only on a changed value), so a field committed on every focus loss records no step. An empty
    /// field is 0 (<c>+''</c>). Text that is not a finite number, or whose size reaches 1e21 (where JS toFixed
    /// switches to exponent form), is refused: the field shows the stored value again and nothing is recorded.
    /// The stored value keeps its fraction; the field shows it toFixed(0) (props-name-xy).
    /// </summary>
    void CommitPosition(string? text, bool horizontal)
    {
        if (_syncing || _layer is null) return;
        string shown = horizontal ? XText : YText;
        string t = (text ?? "").Trim();
        if (t == shown) return;
        double v = 0;
        if (t.Length > 0 && (!double.TryParse(t, NumberStyles.Float, Inv, out v) || !double.IsFinite(v) || Math.Abs(v) >= 1e21))
        {
            OnPropertyChanged(horizontal ? nameof(XText) : nameof(YText));
            return;
        }
        v += 0.0;   // -0 typed is 0
        var layer = _layer;
        _session.Apply(d => LayerOps.MoveTo(d, layer.Id, horizontal ? v : layer.X, horizontal ? layer.Y : v));
        OnPropertyChanged(horizontal ? nameof(XText) : nameof(YText));   // "12.0" or "+12" reads back as "12"
    }

    void Adjust(Func<LayerAdjustments, LayerAdjustments> change) =>
        Edit((d, id) => LayerOps.SetAdjustments(d, id, change(LayerAdjustments.From(d.Layers.Find(l => l.Id == id)!))));

    /// <summary>Applies <paramref name="op"/> to the shown layer as one step (none when it changes nothing).</summary>
    void Edit(Func<Document, int, Document> op)
    {
        if (_syncing || _layer is null) return;
        int id = _layer.Id;
        _session.Apply(d => d.Layers.Exists(l => l.Id == id) ? op(d, id) : d);
    }

    void Duplicate()
    {
        if (_layer is null) return;
        int id = _layer.Id;
        int newId = _session.TakeId();
        _session.ApplyWithSelection(s => LayerOps.Duplicate(s, id, newId));
    }

    int? Index()
    {
        if (_layer is null) return null;
        int id = _layer.Id;
        int i = _session.Document.Layers.FindIndex(l => l.Id == id);
        return i < 0 ? null : i;
    }

    T Get<T>([CallerMemberName] string name = "") => (T)ShownByName[name](_layer);

    /// <summary>
    /// Re-reads the selected layer and raises only what changed (no rebuild: focus and expansion survive). When the shown
    /// layer id changes (to or from null too), Name, X and Y are raised whatever their values, so a field with uncommitted
    /// typing resets to the new layer and can never commit into it (final fix wave 3a). For the same layer, a field with
    /// uncommitted typing is not raised (<see cref="HasUncommittedTyping"/>).
    /// </summary>
    void Refresh()
    {
        var before = _layer;
        var after = _session.SelectedLayer;
        bool otherLayer = before?.Id != after?.Id;
        _syncing = true;
        try
        {
            _layer = after;
            if (!ReferenceEquals(before, after))
                foreach (var (name, read) in Shown)
                {
                    bool text = name is nameof(Name) or nameof(XText) or nameof(YText);
                    if (text && otherLayer) OnPropertyChanged(name);
                    else if (!Equals(read(before), read(after)) && !(text && HasUncommittedTyping?.Invoke(name) == true)) OnPropertyChanged(name);
                }
        }
        finally
        {
            _syncing = false;
        }
        FlipHCommand.NotifyCanExecuteChanged();
        FlipVCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        ResetAdjustmentsCommand.NotifyCanExecuteChanged();
        CenterFrontCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// JS Number.prototype.toFixed(0) for |v| &lt; 1e21: the nearest integer, ties away from zero on the exact
    /// double, and a "-" for any negative input, so -0.4 gives "-0" and -0 gives "0" (ECMA-262 §21.1.3.3).
    /// </summary>
    internal static string ToFixed0(double v) =>
        (v < 0 ? "-" : "") + Math.Round(Math.Abs(v), MidpointRounding.AwayFromZero).ToString("F0", Inv);

    /// <summary>Whole degrees, half away from zero, with no "-0" (props-rotation-label-unrounded).</summary>
    internal static string WholeDegrees(double degrees) =>
        (Math.Round(degrees, MidpointRounding.AwayFromZero) + 0.0).ToString("F0", Inv);

    /// <summary>A stored number as JS prints it inside a template string, for the slider-range values the labels show.</summary>
    static string Num(double v) => (v + 0.0).ToString(Inv);

    /// <summary>
    /// The one non-finite policy (final fix wave 15): NaN and both infinities are refused by every number the panel
    /// takes, as X and Y refuse unusable text. Nothing is recorded, and the property is raised so the control shows the
    /// stored value again. True when refused.
    /// </summary>
    bool RefusedNonFinite(double value, [CallerMemberName] string name = "")
    {
        if (double.IsFinite(value)) return false;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>A step-1 range value: rounded half away from zero, clamped to [min, max], never -0. Callers refuse non-finite input first.</summary>
    static double Whole(double value, double min, double max) =>
        Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), min, max) + 0.0;
}
