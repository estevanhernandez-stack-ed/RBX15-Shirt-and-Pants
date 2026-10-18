using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The canvas area: the document the host shows, the zoom pill (canvas-zoom-controls, zoom-controls),
/// and the canvas input (<see cref="Interaction"/>). Zoom starts at 1 (editor.js:81), steps by 0.25
/// between 0.25 and 3 (editor.js:1490-1491), and is view state: never an undo step, never saved, 100%
/// on launch. Each command is disabled at its limit, where 4.1's button clamped to a no-op (divergence
/// zoom-pill-limits, Este's "never show a dead button").
/// <para>
/// <see cref="Overlay"/> is the host's frame input beyond the document: the selection overlay only while
/// the tool is Select (editor.js:362), and the live preview. <see cref="Cursor"/> mirrors the interaction's
/// cursor, and <see cref="AutomationValue"/> is the selected layer's name for the canvas automation peer.
/// </para>
/// <para>
/// M4a: it is the canvas's <see cref="ICanvasLayerAutomation"/>. The layer automation peers select and transform
/// through it, over the same session entry points the mouse uses, so each change is one undo step and a no-op is
/// none (spec §5); <see cref="SelectedLayerId"/> raises PropertyChanged on every selection change, which the canvas
/// peer turns into ElementSelected.
/// </para>
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject, ICanvasLayerAutomation
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 3;
    public const double ZoomStep = 0.25;

    readonly EditorSession _session;
    readonly ToolsViewModel _tools;
    Action<CanvasDrop>? _dropHandler;

    public CanvasViewModel(EditorSession session, ToolsViewModel tools, BrushViewModel brush, IStatus status)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        Interaction = new CanvasInteraction(session, tools, brush, status);
        _document = session.Document;
        _overlay = CurrentOverlay();
        _cursor = Interaction.Cursor;
        _automationValue = SelectedName();
        session.Changed += OnSessionChanged;
        tools.CurrentChanged += (_, _) => Overlay = CurrentOverlay();
        Interaction.Changed += (_, _) =>
        {
            Overlay = CurrentOverlay();
            Cursor = Interaction.Cursor;
        };
    }

    /// <summary>The canvas input state machine the host forwards pointer and wheel events to.</summary>
    public CanvasInteraction Interaction { get; }

    /// <summary>The session's document, refreshed on every change that touches it (edits, undo, redo).</summary>
    [ObservableProperty]
    private Document _document;

    /// <summary>The selection overlay (Select tool only) and the live preview.</summary>
    [ObservableProperty]
    private ViewOverlay _overlay;

    /// <summary>The canvas cursor, mirrored from <see cref="Interaction"/>.</summary>
    [ObservableProperty]
    private CanvasCursor _cursor;

    /// <summary>The selected layer's name, or "" (the canvas peer's Value).</summary>
    [ObservableProperty]
    private string _automationValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    private double _zoom = 1;

    /// <summary><c>(zoom*100)+'%'</c> (editor.js:1490): "25%", "100%", "125%".</summary>
    public string ZoomLabel => (Zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>Called with every canvas drop the host accepts. Composition sets it to ImportService.Import.</summary>
    public Action<CanvasDrop>? DropHandler
    {
        get => _dropHandler;
        set
        {
            _dropHandler = value;
            DropCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    void ZoomIn() => Zoom = Math.Min(MaxZoom, Zoom + ZoomStep);

    bool CanZoomIn() => Zoom < MaxZoom;

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    void ZoomOut() => Zoom = Math.Max(MinZoom, Zoom - ZoomStep);

    bool CanZoomOut() => Zoom > MinZoom;

    [RelayCommand(CanExecute = nameof(CanDrop))]
    void Drop(CanvasDrop drop)
    {
        if (drop is not null) _dropHandler?.Invoke(drop);
    }

    bool CanDrop(CanvasDrop drop) => drop is not null && _dropHandler is not null;

    ViewOverlay CurrentOverlay() =>
        new(_tools.Current == Tool.Select ? _session.SelectedLayerId : null, Interaction.Preview);

    string SelectedName() => _session.SelectedLayer?.Name ?? "";

    /// <summary>The selected layer's id (<see cref="ICanvasLayerAutomation"/>); PropertyChanged on every selection change.</summary>
    public int? SelectedLayerId => _session.SelectedLayerId;

    /// <summary>Selects through the session; a layer pick also sets the Select tool, as a layers-list pick does (editor.js:575-577).</summary>
    public void SelectLayer(int? id)
    {
        _session.Select(id);
        if (id is not null && _session.SelectedLayerId == id) _tools.Set(Tool.Select);
    }

    /// <summary>The X and Y fields' edit (editor.js:825-826) through LayerOps.MoveTo: one step, none if unchanged.</summary>
    public bool MoveLayer(int id, double x, double y) => _session.Apply(d => LayerOps.MoveTo(d, id, x, y));

    /// <summary>A corner-handle resize to a size (editor.js:1181-1190): uniform, about the center, clamped to 1-500%.</summary>
    public bool ResizeLayer(int id, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "The size must be finite and positive.");
        if (_session.Document.Layers.Find(l => l.Id == id) is not { } layer) return false;
        double scale = Math.Min(width / layer.Image.Width, height / layer.Image.Height);
        return _session.Apply(d => LayerOps.ScaleAboutCenter(d, id, scale));
    }

    /// <summary>A rotate drag by a relative angle (editor.js:1172-1180), normalized to -180..180.</summary>
    public bool RotateLayer(int id, double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees), "The angle must be finite.");
        if (_session.Document.Layers.Find(l => l.Id == id) is not { } layer) return false;
        return _session.Apply(d => LayerOps.SetRotation(d, id, layer.Rotation + degrees));
    }

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (e.What.HasFlag(SessionChange.Document)) Document = _session.Document;
        if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0)
        {
            Overlay = CurrentOverlay();
            AutomationValue = SelectedName();
        }
        if (e.What.HasFlag(SessionChange.Selection)) OnPropertyChanged(nameof(SelectedLayerId));
    }
}
