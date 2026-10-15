using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// One layer row (updateUI's list, editor.js:671-744; ledger layers-list, layers-list-render). The
/// row is reused while its layer exists, so keyboard focus survives every edit; the list calls
/// <see cref="Update"/> with the layer's new state.
/// </summary>
public sealed class LayerRowViewModel : ObservableObject
{
    readonly EditorSession _session;
    string _name = "";
    bool _visible;
    bool _isSelected;
    ImageSource? _thumbnail;

    public LayerRowViewModel(EditorSession session, Layer layer, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(layer);
        _session = session;
        Id = layer.Id;
        // toggleLayerVisibility (editor.js:570-573): one undo step in v5 (layers-visibility fix).
        ToggleVisibilityCommand = new RelayCommand(() => _session.Apply(d => LayerOps.ToggleVisibility(d, Id)));
        // duplicateLayer (editor.js:542-560; layers-duplicate).
        DuplicateCommand = new RelayCommand(() =>
        {
            int newId = _session.TakeId();
            _session.ApplyWithSelection(s => LayerOps.Duplicate(s, Id, newId));
        });
        // removeLayer (editor.js:562-568; layers-remove). No confirmation, as 4.1.
        DeleteCommand = new RelayCommand(() => _session.ApplyWithSelection(s => LayerOps.Remove(s, Id)));
        Update(layer, isSelected);
    }

    public int Id { get; }

    public string Name => _name;

    public bool Visible => _visible;

    public bool IsSelected => _isSelected;

    /// <summary>The raw layer image fit in 48x48 (editor.js:682-685).</summary>
    public ImageSource Thumbnail => _thumbnail!;

    /// <summary>editor.js:708: 'Hide' while visible, 'Show' while hidden.</summary>
    public string VisibilityLabel => _visible ? "Hide" : "Show";

    /// <summary>editor.js:707: the button title.</summary>
    public string VisibilityToolTip => _visible ? "Hide layer" : "Show layer";

    /// <summary>The row's name for assistive tech (spec §4 Accessibility; 4.1 rows had none).</summary>
    public string AutomationName => _visible ? _name : _name + " (hidden)";

    public IRelayCommand ToggleVisibilityCommand { get; }

    public IRelayCommand DuplicateCommand { get; }

    public IRelayCommand DeleteCommand { get; }

    internal void Update(Layer layer, bool isSelected)
    {
        if (layer.Id != Id) throw new ArgumentException($"row {Id} cannot show layer {layer.Id}", nameof(layer));
        bool nameChanged = SetProperty(ref _name, layer.Name, nameof(Name));
        bool visibleChanged = SetProperty(ref _visible, layer.Visible, nameof(Visible));
        SetProperty(ref _isSelected, isSelected, nameof(IsSelected));
        SetProperty(ref _thumbnail, LayerThumbnails.For(layer.Image), nameof(Thumbnail));
        if (visibleChanged)
        {
            OnPropertyChanged(nameof(VisibilityLabel));
            OnPropertyChanged(nameof(VisibilityToolTip));
        }
        if (nameChanged || visibleChanged) OnPropertyChanged(nameof(AutomationName));
    }
}
