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
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject
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

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (e.What.HasFlag(SessionChange.Document)) Document = _session.Document;
        if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0)
        {
            Overlay = CurrentOverlay();
            AutomationValue = SelectedName();
        }
    }
}
