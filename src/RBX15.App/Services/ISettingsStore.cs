namespace RBX15.App.Services;

/// <summary>Which remembered folder (spec §3 "Saving": last folder remembered).</summary>
public enum SettingsFolder
{
    /// <summary>The Save and Open dialogs' folder (<c>lastProjectFolder</c>).</summary>
    Project,

    /// <summary>The asset library's Open folder picker (<c>lastAssetFolder</c>, Task 2).</summary>
    Asset,
}

/// <summary>The app's small local settings file (spec §3 "Upgrades": the saved window position; spec §3 "Saving": the last folder).</summary>
public interface ISettingsStore
{
    /// <summary>The saved placement, or null when there is none or the file is unreadable.</summary>
    WindowPlacement? LoadWindowPlacement();

    /// <summary>Saves the placement. Best effort: a failure to write never throws.</summary>
    void SaveWindowPlacement(WindowPlacement placement);

    /// <summary>The remembered folder, or null when it is unset, unreadable, or no longer a directory.</summary>
    string? LoadFolder(SettingsFolder which);

    /// <summary>Remembers <paramref name="folder"/>. Best effort and atomic, keeps sibling keys, never throws, and skips the write after a failed read (interfaces §2.5).</summary>
    void SaveFolder(SettingsFolder which, string folder);

    /// <summary>The recent project paths, newest first (<c>recentProjects</c>); empty when unset or unreadable. Existence is not checked here: <see cref="RecentProjects"/> prunes.</summary>
    IReadOnlyList<string> LoadRecents();

    /// <summary>Saves the recent paths. Best effort and atomic, keeps sibling keys, never throws, and skips the write after a failed read (the same <c>Update</c> path as the folders).</summary>
    void SaveRecents(IReadOnlyList<string> paths);
}
