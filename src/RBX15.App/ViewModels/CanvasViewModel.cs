using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Controls;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The canvas area: the document the host shows, and the zoom pill (canvas-zoom-controls,
/// zoom-controls). Zoom starts at 1 (editor.js:81), steps by 0.25 between 0.25 and 3
/// (editor.js:1490-1491), and is view state: never an undo step, never saved, 100% on launch.
/// Each command is disabled at its limit, where 4.1's button clamped to a no-op
/// (divergence zoom-pill-limits, Este's "never show a dead button"). A canvas click runs
/// <see cref="ClickHandler"/> with the template point (ruling R5); Task 4 makes it select the topmost
/// layer there, or none.
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 3;
    public const double ZoomStep = 0.25;

    readonly EditorSession _session;
    Action<CanvasDrop>? _dropHandler;
    Action<Point>? _clickHandler;

    public CanvasViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _document = session.Document;
        session.Changed += OnSessionChanged;
    }

    /// <summary>The session's document, refreshed on every change that touches it (edits, undo, redo).</summary>
    [ObservableProperty]
    private Document _document;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    private double _zoom = 1;

    /// <summary><c>(zoom*100)+'%'</c> (editor.js:1490): "25%", "100%", "125%".</summary>
    public string ZoomLabel => (Zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>Called with every canvas drop the host accepts. Task 4 sets it to ImportService.Import.</summary>
    public Action<CanvasDrop>? DropHandler
    {
        get => _dropHandler;
        set
        {
            _dropHandler = value;
            DropCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Called with the template point of every canvas click (a press and release without a drag). Task 4
    /// sets it to select the topmost visible layer under the point, or none (editor.js:1100-1116).
    /// </summary>
    public Action<Point>? ClickHandler
    {
        get => _clickHandler;
        set
        {
            _clickHandler = value;
            ClickCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanClick))]
    void Click(Point point) => _clickHandler?.Invoke(point);

    bool CanClick(Point point) => _clickHandler is not null;

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (e.What.HasFlag(SessionChange.Document)) Document = _session.Document;
    }
}
