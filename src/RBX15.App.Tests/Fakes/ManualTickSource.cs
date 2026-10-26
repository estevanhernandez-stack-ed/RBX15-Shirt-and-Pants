using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An <see cref="ITickSource"/> the test raises by hand, so autosave never waits on a real 60 s timer.</summary>
sealed class ManualTickSource : ITickSource
{
    public event EventHandler? Tick;

    public void Raise() => Tick?.Invoke(this, EventArgs.Empty);
}
