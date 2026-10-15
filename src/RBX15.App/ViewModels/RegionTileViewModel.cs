using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// One Template Regions tile (a <c>.region-btn</c>, editor.js:1624-1685; ledger
/// <c>layout-template-regions</c>, fix). A real button, so it is keyboard-reachable and carries an
/// automation name (spec §4 accessibility). <see cref="PaintCommand"/> is the click: with a layer
/// selected it fits that layer into the region, otherwise it paints the region with the current Fill
/// Style (editor.js:1657-1675). <see cref="ClearCommand"/> is the right-click (editor.js:1677-1684).
/// Each is one <see cref="EditorSession.Apply"/>, so one undo step when it changes something.
/// </summary>
public sealed class RegionTileViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly FillStyleViewModel _fillStyle;
    readonly TemplateMode _mode;
    Fill? _fill;
    ImageSource? _swatch;

    internal RegionTileViewModel(EditorSession session, FillStyleViewModel fillStyle, TemplateMode mode,
                                 string region, string label, string automationName, bool isFront)
    {
        _session = session;
        _fillStyle = fillStyle;
        _mode = mode;
        Region = region;
        Label = label;
        AutomationName = automationName;
        IsFront = isFront;
        _fill = DocumentOps.RegionFill(session.Document, region);
        PaintCommand = new RelayCommand(Paint);
        ClearCommand = new RelayCommand(() => _session.Apply(d => DocumentOps.ClearRegion(d, Region)));
    }

    /// <summary>The verbatim region key, e.g. <c>torso_front</c> (data-r, editor.js:1626).</summary>
    public string Region { get; }

    /// <summary>4.1's tile text before CSS uppercasing: Top, R, Front, L, Back, Bot, Bk, Fr (editor.js:1626-1646).</summary>
    public string Label { get; }

    /// <summary>The part and face, e.g. "Torso Front" or "Right Leg Back".</summary>
    public string AutomationName { get; }

    /// <summary>The body's front tile, which rests on the #2a1040 tint (editor.js:1627; <c>region-front-highlight-lost</c> fix).</summary>
    public bool IsFront { get; }

    /// <summary>The region's fill, or null when unpainted.</summary>
    public Fill? Fill
    {
        get => _fill;
        internal set
        {
            if (_fill == value) return;
            _fill = value;
            _swatch = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFilled));
            OnPropertyChanged(nameof(Swatch));
        }
    }

    /// <summary>The <c>.filled</c> class (editor.js:1559-1562).</summary>
    public bool IsFilled => _fill is not null;

    /// <summary>The engine-painted preview of <see cref="Fill"/>; null when unpainted. Rendered on first read.</summary>
    public ImageSource? Swatch => _fill is null ? null : _swatch ??= RegionSwatch.Render(_mode, Region, _fill);

    public IRelayCommand PaintCommand { get; }
    public IRelayCommand ClearCommand { get; }

    void Paint()
    {
        if (_session.SelectedLayer is { } layer)
            _session.Apply(d => DocumentOps.FitLayerToRegion(d, layer.Id, Region));
        else
            _session.Apply(d => DocumentOps.PaintRegion(d, Region, _fillStyle.Current));
    }
}
