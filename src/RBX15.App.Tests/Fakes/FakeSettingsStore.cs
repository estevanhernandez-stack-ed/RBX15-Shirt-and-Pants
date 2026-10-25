using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An in-memory <see cref="ISettingsStore"/> that records every save.</summary>
internal sealed class FakeSettingsStore(WindowPlacement? saved = null) : ISettingsStore
{
    public WindowPlacement? Saved { get; private set; } = saved;

    public List<WindowPlacement> Saves { get; } = [];

    /// <summary>The remembered folders; LoadFolder returns these as they are (the fake does not check the disk).</summary>
    public Dictionary<SettingsFolder, string> Folders { get; } = [];

    public List<(SettingsFolder Which, string Folder)> FolderSaves { get; } = [];

    /// <summary>The stored recents; LoadRecents returns these as they are (the fake does not check the disk).</summary>
    public List<string> Recents { get; set; } = [];

    public List<IReadOnlyList<string>> RecentSaves { get; } = [];

    public WindowPlacement? LoadWindowPlacement() => Saved;

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        Saves.Add(placement);
        Saved = placement;
    }

    public string? LoadFolder(SettingsFolder which) => Folders.GetValueOrDefault(which);

    public void SaveFolder(SettingsFolder which, string folder)
    {
        FolderSaves.Add((which, folder));
        Folders[which] = folder;
    }

    public IReadOnlyList<string> LoadRecents() => [.. Recents];

    public void SaveRecents(IReadOnlyList<string> paths)
    {
        RecentSaves.Add([.. paths]);
        Recents = [.. paths];
    }
}
