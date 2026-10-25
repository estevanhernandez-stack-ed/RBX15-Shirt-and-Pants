using System.IO;

namespace RBX15.App.Services;

/// <summary>
/// Keeps the taskbar jump list in step with <see cref="RecentProjects"/> (M5 item 5; decisions H1, J1): on every change, one task per
/// recent under the "Recent" category, no other tasks. A jump-list click is a launch with a file path, so opening one needs no code
/// beyond <see cref="LaunchArguments"/>. An item the user removes from the jump list leaves the recents too.
/// </summary>
public sealed class JumpListPublisher
{
    readonly RecentProjects _recents;
    readonly IJumpListSink _sink;
    readonly string _exePath;

    /// <param name="exePath">The exe the entries launch and take their icon from; this process's by default.</param>
    public JumpListPublisher(RecentProjects recents, IJumpListSink sink, string? exePath = null)
    {
        ArgumentNullException.ThrowIfNull(recents);
        ArgumentNullException.ThrowIfNull(sink);
        (_recents, _sink) = (recents, sink);
        _exePath = exePath ?? Environment.ProcessPath ?? "";
        recents.Changed += (_, _) => Publish();
        sink.ItemsRemovedByUser += paths =>
        {
            foreach (var path in paths) recents.Remove(path);
        };
    }

    /// <summary>The entries for a list of recent paths.</summary>
    internal static IReadOnlyList<JumpEntry> EntriesFor(IReadOnlyList<string> paths, string exePath) =>
        [.. paths.Select(p => new JumpEntry(Path.GetFileName(p), p, "\"" + p + "\"", exePath, exePath))];

    void Publish() => _sink.Publish(EntriesFor(_recents.Items, _exePath));
}
