using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The layers panel and its drop zone (editor.html:311-318; ledger layers-list, layers-list-render,
/// layers-select, layers-move-updown, panel-drop-zone, drop-panel). Rows are top first, the highest
/// z-order on top (editor.js:672-675). Drag reorder (editor.js:584-666; ledger layers-reorder-drag,
/// layers-reorder-drag-2) runs through BeginRowDrag, DragOverRow, ClearDropTarget, CompleteRowDrop and
/// EndRowDrag; LayersPanel's code-behind drives them from the mouse and the keyboard.
/// </summary>
public sealed class LayersViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly ImportService _import;
    readonly IFileDialogs _dialogs;
    LayerRowViewModel? _selected;
    bool _syncing;
    int? _dragId;      // layerDragId (editor.js:587)
    int? _targetId;    // layerDragOverId (editor.js:588), live only: cleared whenever the pointer is off every other row
    bool _above;       // layerDragPos === 'top' (editor.js:589)

    public LayersViewModel(EditorSession session, ImportService import, IFileDialogs dialogs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(dialogs);
        _session = session;
        _import = import;
        _dialogs = dialogs;
        MoveUpCommand = new RelayCommand(() => Move(LayerOps.MoveUp), () => SelectedIndex() is int i && i < _session.Document.Layers.Count - 1);
        MoveDownCommand = new RelayCommand(() => Move(LayerOps.MoveDown), () => SelectedIndex() is > 0);
        BrowseCommand = new RelayCommand(Browse);
        _session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0) Sync();
        };
        Sync();
    }

    /// <summary>Top first: Rows[0] is the last layer in Document.Layers.</summary>
    public ObservableCollection<LayerRowViewModel> Rows { get; } = [];

    /// <summary>
    /// The selected row; setting it selects that layer (selectLayer, editor.js:575; never an undo step). A user
    /// pick of a row raises <see cref="LayerPicked"/>; a sync, or a deselect, does not.
    /// </summary>
    public LayerRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (_syncing || ReferenceEquals(value, _selected)) return;
            _session.Select(value?.Id);
            if (value is not null) LayerPicked?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A user picked a row: selectLayer sets the Select tool (editor.js:577; R8). Composition wires it.</summary>
    public event EventHandler? LayerPicked;

    /// <summary>btnMoveUp (editor.js:846-849): one undo step in v5 (layers-move-updown fix). Disabled at the top.</summary>
    public IRelayCommand MoveUpCommand { get; }

    /// <summary>btnMoveDown (editor.js:850-853). Disabled at the bottom.</summary>
    public IRelayCommand MoveDownCommand { get; }

    /// <summary>The drop zone's click-to-browse (dropZone.onclick, editor.js:954).</summary>
    public IRelayCommand BrowseCommand { get; }

    /// <summary>The heading's hint, verbatim (editor.html:317). Shown now that drag reorder works (M3a hid it).</summary>
    public string ReorderHint => "(drag to reorder)";

    /// <summary>
    /// handleLayerDragStart (editor.js:591-600): marks the dragged row (.dragging-layer, opacity .4). Ends any drag
    /// still running. A missing id starts nothing. Never an undo step and never a selection change.
    /// </summary>
    public void BeginRowDrag(int id)
    {
        EndRowDrag();
        if (Row(id) is not { } row) return;
        _dragId = id;
        row.IsDragSource = true;
    }

    /// <summary>
    /// handleLayerDragOver (editor.js:602-619): the row under the pointer becomes the drop target, its top half
    /// (<paramref name="above"/>) or its bottom half, with one indicator on it and none on any other row. The dragged
    /// row itself is never a target: hovering it clears the target, which fixes 4.1's stale target (editor.js:605
    /// returned early and kept layerDragOverId; ledger layers-reorder-drag). Ignored with no drag running.
    /// </summary>
    public void DragOverRow(int targetId, bool above)
    {
        if (_dragId is null) return;
        if (targetId == _dragId || Row(targetId) is not { } row)
        {
            ClearDropTarget();
            return;
        }
        if (_targetId is int old && old != targetId && Row(old) is { } previous) previous.DropIndicator = DropIndicator.None;
        _targetId = targetId;
        _above = above;
        row.DropIndicator = above ? DropIndicator.Above : DropIndicator.Below;
    }

    /// <summary>The pointer is off every row: no target and no indicator, so a release there drops nothing (the stale-target fix).</summary>
    public void ClearDropTarget()
    {
        if (_targetId is int id && Row(id) is { } row) row.DropIndicator = DropIndicator.None;
        _targetId = null;
    }

    /// <summary>
    /// handleLayerDrop (editor.js:621-657) on the live target only: LayerOps.DropIndices then LayerOps.Reorder in one
    /// session Apply, so a reorder is one undo step and a drop that leaves the order as it was records none (4.1 pushed
    /// an empty step, editor.js:630; ledger undo-empty-steps). The selection is unchanged (layers-reorder-drag). Always
    /// ends the drag, as 4.1 cleaned up on every path. True iff the order changed.
    /// </summary>
    public bool CompleteRowDrop()
    {
        bool changed = false;
        if (_dragId is int drag && _targetId is int target)
        {
            bool above = _above;
            changed = _session.Apply(d => LayerOps.DropIndices(d, drag, target, above) is (int from, int to) ? LayerOps.Reorder(d, from, to) : d);
        }
        EndRowDrag();
        return changed;
    }

    /// <summary>cleanupLayerDrag (editor.js:659-666): no drag, no target, no indicator. Idempotent.</summary>
    public void EndRowDrag()
    {
        ClearDropTarget();
        if (_dragId is int id && Row(id) is { } row) row.IsDragSource = false;
        _dragId = null;
    }

    /// <summary>A drop on the zone: default placement, no drop point (editor.js:956; drop-panel).</summary>
    public void DropFiles(IReadOnlyList<string> paths) => _import.Import(paths);

    void Browse()
    {
        var paths = _dialogs.OpenImages();
        if (paths.Count > 0) _import.Import(paths);
    }

    void Move(Func<Document, int, Document> op)
    {
        if (_session.SelectedLayerId is int id) _session.Apply(d => op(d, id));
    }

    LayerRowViewModel? Row(int id) => Rows.FirstOrDefault(r => r.Id == id);

    int? SelectedIndex()
    {
        if (_session.SelectedLayerId is not int id) return null;
        int i = _session.Document.Layers.FindIndex(l => l.Id == id);
        return i < 0 ? null : i;
    }

    /// <summary>Reconciles Rows with the document in place: rows are moved, added or removed, never rebuilt.</summary>
    void Sync()
    {
        _syncing = true;
        try
        {
            var layers = _session.Document.Layers;
            int? selectedId = _session.SelectedLayerId;
            var ids = layers.Select(l => l.Id).ToHashSet();
            for (int i = Rows.Count - 1; i >= 0; i--)
                if (!ids.Contains(Rows[i].Id)) Rows.RemoveAt(i);
            for (int target = 0; target < layers.Count; target++)
            {
                var layer = layers[layers.Count - 1 - target];
                int at = -1;
                for (int j = target; j < Rows.Count; j++)
                    if (Rows[j].Id == layer.Id) { at = j; break; }
                if (at < 0) Rows.Insert(target, new LayerRowViewModel(_session, layer, layer.Id == selectedId));
                else
                {
                    if (at != target) Rows.Move(at, target);
                    Rows[target].Update(layer, layer.Id == selectedId);
                }
            }
            // A drag whose row or target left the document (an undo, a delete) ends or loses its target here,
            // so a later release drops nothing.
            if (_dragId is int dragging && !ids.Contains(dragging)) EndRowDrag();
            if (_targetId is int dropTarget && !ids.Contains(dropTarget)) _targetId = null;
            // Raised on every sync: a Move or Remove can make the ListBox drop its selection, and the
            // write-back it tries during the sync is ignored (_syncing), so the binding re-reads it here.
            _selected = Rows.FirstOrDefault(r => r.Id == selectedId);
            OnPropertyChanged(nameof(Selected));
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            _syncing = false;
        }
    }
}
