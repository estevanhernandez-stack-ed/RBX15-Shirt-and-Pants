using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An in-memory <see cref="ISettingsStore"/> that records every save.</summary>
internal sealed class FakeSettingsStore(WindowPlacement? saved = null) : ISettingsStore
{
    public WindowPlacement? Saved { get; private set; } = saved;

    public List<WindowPlacement> Saves { get; } = [];

    public WindowPlacement? LoadWindowPlacement() => Saved;

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        Saves.Add(placement);
        Saved = placement;
    }
}
