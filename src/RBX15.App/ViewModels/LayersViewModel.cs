using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The basic layers panel and its drop zone (editor.html:311-318; ledger layers-list,
/// layers-list-render, layers-select, layers-move-updown, panel-drop-zone, drop-panel). Rows are
/// top first, the highest z-order on top (editor.js:672-675). No drag reorder in M3a.
/// </summary>
public sealed class LayersViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly ImportService _import;
    readonly IFileDialogs _dialogs;
    LayerRowViewModel? _selected;
    bool _syncing;

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

    /// <summary>The selected row; setting it selects that layer (selectLayer, editor.js:575; never an undo step).</summary>
    public LayerRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (_syncing || ReferenceEquals(value, _selected)) return;
            _session.Select(value?.Id);
        }
    }

    /// <summary>btnMoveUp (editor.js:846-849): one undo step in v5 (layers-move-updown fix). Disabled at the top.</summary>
    public IRelayCommand MoveUpCommand { get; }

    /// <summary>btnMoveDown (editor.js:850-853). Disabled at the bottom.</summary>
    public IRelayCommand MoveDownCommand { get; }

    /// <summary>The drop zone's click-to-browse (dropZone.onclick, editor.js:954).</summary>
    public IRelayCommand BrowseCommand { get; }

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
