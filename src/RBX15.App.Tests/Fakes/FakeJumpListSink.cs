using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An <see cref="IJumpListSink"/> that records every list it was handed and touches no taskbar.</summary>
sealed class FakeJumpListSink : IJumpListSink
{
    public List<IReadOnlyList<JumpEntry>> Published { get; } = [];

    /// <summary>The last list handed over, or null when nothing was published.</summary>
    public IReadOnlyList<JumpEntry>? Last => Published.Count == 0 ? null : Published[^1];

    public event Action<IReadOnlyList<string>>? ItemsRemovedByUser;

    public void Publish(IReadOnlyList<JumpEntry> entries) => Published.Add(entries);

    /// <summary>Plays the shell telling the app the user removed these items (their full paths).</summary>
    public void RemoveByUser(params string[] paths) => ItemsRemovedByUser?.Invoke(paths);
}
