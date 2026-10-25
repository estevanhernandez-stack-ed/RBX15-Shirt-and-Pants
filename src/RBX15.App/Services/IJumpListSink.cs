using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Shell;

namespace RBX15.App.Services;

/// <summary>One jump-list entry: a task that launches this exe with a project path (M5 decision J1).</summary>
/// <param name="Title">The file name.</param>
/// <param name="Description">The full path (the shell shows it as the item's tooltip, and the sink maps a removed item back to its recent by it).</param>
/// <param name="Arguments">The quoted full path, as a launch with a file receives it.</param>
public sealed record JumpEntry(string Title, string Description, string Arguments, string ApplicationPath, string IconResourcePath);

/// <summary>
/// The taskbar jump list, as a seam (M5 item 5): the app's sink drives <see cref="JumpList"/>, the tests' fake records what it was handed.
/// No test touches the process's real jump list.
/// </summary>
public interface IJumpListSink
{
    /// <summary>The category the entries are listed under.</summary>
    const string Category = "Recent";

    /// <summary>Replaces the whole jump list with <paramref name="entries"/> under <see cref="Category"/>, no tasks. Best effort: never throws.</summary>
    void Publish(IReadOnlyList<JumpEntry> entries);

    /// <summary>Raised with the <see cref="JumpEntry.Description"/> (the full path) of each item the user removed from the jump list.</summary>
    event Action<IReadOnlyList<string>>? ItemsRemovedByUser;
}

/// <summary>
/// The app's sink: WPF's <see cref="JumpList"/> on <see cref="Application.Current"/>, one <see cref="JumpTask"/> per entry (decision J1;
/// a JumpTask works in the MSIX and the zip, where a JumpPath needs the file type registered to the caller). MAN-11 checks the packaged taskbar.
/// <see cref="JumpList.JumpItemsRejected"/> is left alone: a shell-rejected item stays in the in-app Recent list.
/// </summary>
public sealed class WpfJumpListSink : IJumpListSink
{
    JumpList? _list;

    public event Action<IReadOnlyList<string>>? ItemsRemovedByUser;

    public void Publish(IReadOnlyList<JumpEntry> entries)
    {
        if (Application.Current is not { } app) return;
        try
        {
            var list = _list;
            if (list is null)
            {
                list = new JumpList { ShowRecentCategory = false, ShowFrequentCategory = false };
                list.JumpItemsRemovedByUser += (_, e) =>
                {
                    var paths = e.RemovedItems.OfType<JumpTask>().Select(t => t.Description).Where(d => !string.IsNullOrEmpty(d)).ToList();
                    // Raised from inside Apply: the republish a removal causes waits for it to return.
                    if (paths.Count > 0) app.Dispatcher.BeginInvoke(() => ItemsRemovedByUser?.Invoke(paths));
                };
                JumpList.SetJumpList(app, list);
                _list = list;
            }
            list.JumpItems.Clear();
            foreach (var entry in entries)
            {
                list.JumpItems.Add(new JumpTask
                {
                    Title = entry.Title,
                    Description = entry.Description,
                    Arguments = entry.Arguments,
                    ApplicationPath = entry.ApplicationPath,
                    IconResourcePath = entry.IconResourcePath,
                    CustomCategory = IJumpListSink.Category,
                });
            }
            list.Apply();
        }
        catch (Exception e) when (e is InvalidOperationException or COMException or IOException or UnauthorizedAccessException)
        {
            // Best effort: a jump list the shell refuses must never break saving or opening.
        }
    }
}
