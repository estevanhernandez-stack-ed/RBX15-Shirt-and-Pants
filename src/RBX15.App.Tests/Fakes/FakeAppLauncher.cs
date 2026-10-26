using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An <see cref="IAppLauncher"/> that records the paths it was asked to open and the new windows it was asked for, and starts nothing.</summary>
sealed class FakeAppLauncher : IAppLauncher
{
    public List<string> Opened { get; } = [];

    /// <summary>How many times a new, empty window was asked for.</summary>
    public int NewWindows { get; private set; }

    public void Open(string path) => Opened.Add(path);

    public void OpenNew() => NewWindows++;
}
