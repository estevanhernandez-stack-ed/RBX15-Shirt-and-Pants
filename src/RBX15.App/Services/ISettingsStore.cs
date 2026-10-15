namespace RBX15.App.Services;

/// <summary>The app's small local settings file (spec §3 "Upgrades": the saved window position).</summary>
public interface ISettingsStore
{
    /// <summary>The saved placement, or null when there is none or the file is unreadable.</summary>
    WindowPlacement? LoadWindowPlacement();

    /// <summary>Saves the placement. Best effort: a failure to write never throws.</summary>
    void SaveWindowPlacement(WindowPlacement placement);
}
