using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>One Fill Style type button: the lib/fills.js type and its 4.1 label (editor.html:148-154).</summary>
public sealed record FillTypeOption(string Type, string Label);

/// <summary>
/// The Fill Style group (editor.html:145-167, editor.js:1550-1613; ledger <c>layout-fill-style</c>).
/// <see cref="Current"/> is 4.1's <c>currentFill</c>. Every control edit runs applyFillEdit's rule
/// (<c>fillstyle-live-edit-rule</c>, keep): the edit replaces every region painted with the current
/// fill's key, through one <see cref="EditorSession.Apply"/>.
/// <para>
/// Undo (interfaces sheet §1, <c>fillstyle-undo-snapshot-timing</c>, fix): a slider thumb drag is one
/// gesture (<see cref="BeginSliderDrag"/>), so it records one step at the end, and only if a region
/// changed. A color pick is one gesture too (<c>fillstyle-color-pickers</c>, fix): its live preview
/// repaints as the color moves, a cancel puts the seed back, and the pick records one step, or none when
/// the color ends where it started. Anything else (a type click, a keyboard or track-click slider change)
/// is one <c>Apply</c>, so one step when a region changed and none otherwise.
/// </para>
/// <para>
/// One-way (<c>fillstyle-ui-not-synced-from-region</c>, keep): <see cref="Current"/> lives only here.
/// Undo, redo and region clicks never change it. The controls bind to it, so they always show the
/// current fill (the <c>fillstyle-type-buttons</c> fix: the active button can no longer disagree).
/// </para>
/// </summary>
public sealed class FillStyleViewModel : ObservableObject
{
    /// <summary>currentFill's initial value (editor.js:1552), equal to the HTML control defaults (editor.html:148-162).</summary>
    public static readonly Fill DefaultFill = new("solid", "#17d4fa", "#f22f89", 45, 16);

    public const double MinAngle = 0;   // editor.html:160 min
    public const double MaxAngle = 360; // editor.html:160 max
    public const double MinScale = 4;   // editor.html:162 min
    public const double MaxScale = 48;  // editor.html:162 max

    readonly EditorSession _session;
    readonly IColorPicker _picker;
    Fill _current = DefaultFill;

    public FillStyleViewModel(EditorSession session, IColorPicker picker)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        SetTypeCommand = new RelayCommand<string>(t => Type = t ?? throw new ArgumentNullException(nameof(t)));
        PickColor1Command = new RelayCommand<object?>(anchor => Pick(second: false, anchor));
        PickColor2Command = new RelayCommand<object?>(anchor => Pick(second: true, anchor));
    }

    /// <summary>The seven type buttons in 4.1's order (editor.html:148-154).</summary>
    public IReadOnlyList<FillTypeOption> Types { get; } =
    [
        new("solid", "Solid"),
        new("linear", "Gradient"),
        new("radial", "Radial"),
        new("stripes", "Stripes"),
        new("checker", "Checker"),
        new("dots", "Dots"),
        new("camo", "Camo"),
    ];

    public Fill Current => _current;

    /// <summary>The active type (editor.js:1605). Setting a type that is not one of <see cref="Types"/> throws.</summary>
    public string Type
    {
        get => _current.Type;
        set
        {
            // A ListBox deselect (Ctrl+click, Ctrl+Space) pushes null. Keep the type and snap the list back.
            if (value is null) { OnPropertyChanged(nameof(Type)); return; }
            if (!Types.Any(t => t.Type == value)) throw new ArgumentException($"Unknown fill type '{value}'.", nameof(value));
            Edit(_current with { Type = value });
        }
    }

    /// <summary>Color 1, lowercase <c>#rrggbb</c> (editor.js:1608; an input of type color yields lowercase).</summary>
    public string Color1
    {
        get => _current.C1!;
        set => Edit(_current with { C1 = NormalizeHex(value) });
    }

    /// <summary>Color 2, lowercase <c>#rrggbb</c> (editor.js:1608).</summary>
    public string Color2
    {
        get => _current.C2!;
        set => Edit(_current with { C2 = NormalizeHex(value) });
    }

    /// <summary>Angle in whole degrees, 0 to 360 (editor.html:160; <c>+v</c> of a step-1 range, editor.js:1609).</summary>
    public double Angle
    {
        get => _current.Angle!.Value;
        set => Edit(_current with { Angle = Whole(value, MinAngle, MaxAngle) });
    }

    /// <summary>Pattern scale, whole numbers 4 to 48 (editor.html:162).</summary>
    public double Scale
    {
        get => _current.Scale!.Value;
        set => Edit(_current with { Scale = Whole(value, MinScale, MaxScale) });
    }

    public IRelayCommand<string> SetTypeCommand { get; }
    /// <summary>Opens the Color 1 picker; the parameter is the element it opens under (the swatch button), or null.</summary>
    public IRelayCommand<object?> PickColor1Command { get; }

    /// <summary>Opens the Color 2 picker; the parameter is the element it opens under (the swatch button), or null.</summary>
    public IRelayCommand<object?> PickColor2Command { get; }

    /// <summary>
    /// Opens one slider gesture (Thumb.DragStarted). Dispose it on DragCompleted: the drag records one
    /// undo step iff it changed a region (editor.js:1570-1579, :1611). Wraps <see cref="EditorSession.BeginGesture"/>.
    /// </summary>
    public IDisposable BeginSliderDrag() => _session.BeginGesture();

    /// <summary>
    /// One color pick as one gesture. applyFillEdit's rule picks the regions once, at the open: those
    /// painted with the current fill's key. Each preview repaints exactly those regions from the document
    /// as it was at the open, so a preview that passes through another region's color never captures that
    /// region. Cancel (or a picker that throws) shows the seed again, which restores the open's document, so nothing is recorded.
    /// </summary>
    void Pick(bool second, object? anchor)
    {
        var seed = _current;
        var start = _session.Document;
        string seedHex = second ? seed.C2! : seed.C1!;
        using (_session.BeginGesture())
        {
            void Show(string hex)
            {
                var next = second ? seed with { C2 = NormalizeHex(hex) } : seed with { C1 = NormalizeHex(hex) };
                SetCurrent(next);
                _session.Apply(_ => DocumentOps.RetargetFill(start, seed.Key, next));
            }
            string? picked = null;
            try { picked = _picker.Pick(seedHex, Show, anchor); }
            finally { Show(picked ?? seedHex); }                 // a cancel, or a picker that throws, reverts
        }
    }

    /// <summary>applyFillEdit (editor.js:1581-1598): mutate currentFill, then retarget the regions that had its old key.</summary>
    void Edit(Fill next)
    {
        if (next == _current) return;
        string beforeKey = _current.Key;
        SetCurrent(next);
        _session.Apply(d => DocumentOps.RetargetFill(d, beforeKey, next));
    }

    /// <summary>Makes <paramref name="next"/> the current fill and raises the controls that changed.</summary>
    void SetCurrent(Fill next)
    {
        if (next == _current) return;
        var before = _current;
        _current = next;
        OnPropertyChanged(nameof(Current));
        if (before.Type != next.Type) OnPropertyChanged(nameof(Type));
        if (before.C1 != next.C1) OnPropertyChanged(nameof(Color1));
        if (before.C2 != next.C2) OnPropertyChanged(nameof(Color2));
        if (before.Angle != next.Angle) OnPropertyChanged(nameof(Angle));
        if (before.Scale != next.Scale) OnPropertyChanged(nameof(Scale));
    }

    static double Whole(double value, double min, double max)
    {
        if (double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), "NaN is not a slider value.");
        return Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), min, max);
    }

    static string NormalizeHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != 7 || hex[0] != '#' || hex.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF"))
            throw new ArgumentException($"'{hex}' is not #rrggbb.", nameof(hex));
        return hex.ToLowerInvariant();
    }
}
