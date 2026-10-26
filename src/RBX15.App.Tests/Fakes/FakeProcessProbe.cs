using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An <see cref="IProcessProbe"/> over a list the test fills: only the owners it names are running. Never looks at the real process table.</summary>
public sealed class FakeProcessProbe : IProcessProbe
{
    /// <summary>The (pid, start time) pairs that count as running.</summary>
    public HashSet<(int Pid, DateTime StartedUtc)> Running { get; } = [];

    public bool IsRunning(int pid, DateTime startedUtc) => Running.Contains((pid, startedUtc.ToUniversalTime()));
}
