using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An <see cref="IAppLauncher"/> that records the paths it was asked to open and starts nothing.</summary>
sealed class FakeAppLauncher : IAppLauncher
{
    public List<string> Opened { get; } = [];

    public void Open(string path) => Opened.Add(path);
}
