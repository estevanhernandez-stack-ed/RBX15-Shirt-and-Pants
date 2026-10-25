using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Layout tab's Presets group (editor.html:113-124; ledger presets-six-buttons, layout-presets): Meme Tee, Allover, Sleeve
/// Pop, Mirror F+B, Scatter and Fullbleed in a 3-column grid. Shown only while a layer is selected. Each preset is one undo
/// step (4.1 pushed two for Meme, Allover, Mirror and Scatter) and does nothing without a selection. Meme is Fit Front,
/// Mirror is F+B, and the Allover and Scatter presets are the Placement group's own (<see cref="PlacementActions"/>).
/// </summary>
public sealed class PresetsViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly RelayCommand[] _all;
    bool _hasSelection;

    /// <param name="random">Where Scatter gets its random numbers (Random.Shared in the app, a seeded Random in tests).</param>
    public PresetsViewModel(EditorSession session, Func<Random> random)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(random);
        MemeCommand = new RelayCommand(() => PlacementActions.FitFront(_session), () => HasSelection);
        AlloverCommand = new RelayCommand(() => PlacementActions.Allover(_session), () => HasSelection);
        SleevePopCommand = new RelayCommand(() => PlacementActions.SleevePop(_session), () => HasSelection);
        MirrorCommand = new RelayCommand(() => PlacementActions.FrontAndBack(_session), () => HasSelection);
        ScatterCommand = new RelayCommand(() => PlacementActions.Scatter(_session, random), () => HasSelection);
        FullbleedCommand = new RelayCommand(() => PlacementActions.Fullbleed(_session), () => HasSelection);
        _all = [MemeCommand, AlloverCommand, SleevePopCommand, MirrorCommand, ScatterCommand, FullbleedCommand];
        _hasSelection = _session.SelectedLayer is not null;
        _session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Selection | SessionChange.Document)) != 0) Refresh();
        };
    }

    /// <summary>True while a layer is selected; the group's visibility.</summary>
    public bool HasSelection => _hasSelection;

    public RelayCommand MemeCommand { get; }
    public RelayCommand AlloverCommand { get; }
    public RelayCommand SleevePopCommand { get; }
    public RelayCommand MirrorCommand { get; }
    public RelayCommand ScatterCommand { get; }
    public RelayCommand FullbleedCommand { get; }

    void Refresh()
    {
        bool now = _session.SelectedLayer is not null;
        if (now == _hasSelection) return;
        _hasSelection = now;
        OnPropertyChanged(nameof(HasSelection));
        foreach (var c in _all) c.NotifyCanExecuteChanged();
    }
}
