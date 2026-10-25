using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>One entry of the Recent drop-down: the file name on the button, the full path as its tooltip (M5 decision H1).</summary>
public sealed record RecentItemViewModel(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// The File group's "Recent" drop-down (M5 item 5; decision H1): the last 10 projects, newest first, then "Clear recent list".
/// Opening one goes through <see cref="ProjectFileService.OpenAsync(string)"/>, so it asks the unsaved-changes question first; a file
/// that is gone shows <see cref="DialogText.RecentMissing"/> on the status strip and leaves the list. Opening the drop-down
/// re-checks the files (<see cref="RecentProjects.RefreshAsync"/>), so a deleted one is not offered.
/// </summary>
public sealed class RecentMenuViewModel : ObservableObject
{
    readonly RecentProjects _recents;
    readonly ProjectFileService _files;
    readonly IStatus _status;

    public RecentMenuViewModel(RecentProjects recents, ProjectFileService files, IStatus status)
    {
        ArgumentNullException.ThrowIfNull(recents);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(status);
        (_recents, _files, _status) = (recents, files, status);
        OpenRecentCommand = new AsyncRelayCommand<string>(OpenRecentAsync, path => !string.IsNullOrEmpty(path) && !files.IsBusy);
        ClearCommand = new RelayCommand(recents.Clear, () => Items.Count > 0);
        RefreshCommand = new AsyncRelayCommand(async () => await recents.RefreshAsync());
        files.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProjectFileService.IsBusy)) OpenRecentCommand.NotifyCanExecuteChanged();
        };
        recents.Changed += (_, _) => Rebuild();
        Rebuild();
    }

    /// <summary>The entries, newest first.</summary>
    public ObservableCollection<RecentItemViewModel> Items { get; } = [];

    /// <summary>False while the list is empty: the drop-down shows its empty line instead of the entries and the clear entry.</summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>Opens the project at the path it is given (an entry's full path).</summary>
    public IAsyncRelayCommand<string> OpenRecentCommand { get; }

    /// <summary>"Clear recent list": empties the recents (and with them the jump list).</summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>Re-checks the files; the view runs it as the drop-down opens.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!await Task.Run(() => Exists(path)))
        {
            _status.Show(DialogText.RecentMissing(Path.GetFileName(path)));
            _recents.Remove(path);
            return;
        }
        await _files.OpenAsync(path);
    }

    static bool Exists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    void Rebuild()
    {
        Items.Clear();
        foreach (var path in _recents.Items) Items.Add(new RecentItemViewModel(path));
        OnPropertyChanged(nameof(HasItems));
        ClearCommand.NotifyCanExecuteChanged();
    }
}
