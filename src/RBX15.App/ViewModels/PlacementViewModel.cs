using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's Placement group (editor.html:63-76; ledger home-placement-group, placement-selection-guard): Fit Front,
/// F+B and Stretch on row one, Allover and Scatter on row two. The group is shown only while a layer is selected
/// (<see cref="HasSelection"/>, spec §4); 4.1 always showed it and returned silently without one. Every command is one
/// undo step and does nothing without a selection (<see cref="PlacementActions"/>).
/// </summary>
public sealed class PlacementViewModel : ObservableObject
{
    readonly EditorSession _session;
    bool _hasSelection;

    /// <param name="random">Where Scatter gets its random numbers: the app passes Random.Shared, tests a seeded Random (spec §5).</param>
    public PlacementViewModel(EditorSession session, Func<Random> random)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(random);
        FitFrontCommand = new RelayCommand(() => PlacementActions.FitFront(_session), () => HasSelection);
        FrontBackCommand = new RelayCommand(() => PlacementActions.FrontAndBack(_session), () => HasSelection);
        StretchCommand = new RelayCommand(() => PlacementActions.Stretch(_session), () => HasSelection);
        AlloverCommand = new RelayCommand(() => PlacementActions.Allover(_session), () => HasSelection);
        ScatterCommand = new RelayCommand(() => PlacementActions.Scatter(_session, random), () => HasSelection);
        _hasSelection = _session.SelectedLayer is not null;
        _session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Selection | SessionChange.Document)) != 0) Refresh();
        };
    }

    /// <summary>True while a layer is selected; the group's visibility.</summary>
    public bool HasSelection => _hasSelection;

    /// <summary>btnFitFront, "Fit to front torso" (editor.html:66).</summary>
    public RelayCommand FitFrontCommand { get; }

    /// <summary>btnFitFB, "Front + Back" (editor.html:67).</summary>
    public RelayCommand FrontBackCommand { get; }

    /// <summary>btnStretch, "Stretch across torso" (editor.html:68).</summary>
    public RelayCommand StretchCommand { get; }

    /// <summary>btnAllover, "Tile allover" (editor.html:72).</summary>
    public RelayCommand AlloverCommand { get; }

    /// <summary>btnScatter, "Scatter randomly" (editor.html:73).</summary>
    public RelayCommand ScatterCommand { get; }

    void Refresh()
    {
        bool now = _session.SelectedLayer is not null;
        if (now == _hasSelection) return;
        _hasSelection = now;
        OnPropertyChanged(nameof(HasSelection));
        foreach (var c in new[] { FitFrontCommand, FrontBackCommand, StretchCommand, AlloverCommand, ScatterCommand })
            c.NotifyCanExecuteChanged();
    }
}
