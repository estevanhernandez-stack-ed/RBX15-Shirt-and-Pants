using System.IO;

namespace RBX15.App.Services;

/// <summary>
/// The recent projects (spec §3 "Upgrades"; M5 decision H1): the last <see cref="Max"/> project files saved or opened, newest first,
/// kept in <c>settings.json</c> through <see cref="ISettingsStore"/> (a recents write is the same read-modify-write as the window and
/// folder keys, so the siblings survive and a failed read skips the write). Paths are full paths, deduplicated without regard to case.
/// <list type="bullet">
/// <item><see cref="Add"/> is what a successful Save, Save As or Open calls (<see cref="ProjectFileService"/>), never a restore. It
///   re-reads the store first, so a second window's additions are kept; the two windows can still lose an add to each other in a race
///   of a few milliseconds (S1: one process per window), which is accepted.</item>
/// <item>A file that no longer exists leaves the list in <see cref="RefreshAsync"/>, whose existence checks run on a pool thread so a
///   dead network share never blocks the UI. <see cref="Add"/> does no existence check for the same reason.</item>
/// </list>
/// UI thread. <see cref="Changed"/> follows every change to <see cref="Items"/>: the jump list republishes and the File group's
/// Recent menu rebuilds from it.
/// </summary>
public sealed class RecentProjects(ISettingsStore settings, Func<string, bool>? exists = null)
{
    /// <summary>How many recent projects are kept (H1).</summary>
    public const int Max = 10;

    readonly Func<string, bool> _exists = exists ?? File.Exists;
    bool _loaded;

    /// <summary>The list as of the last add, remove, clear or refresh, newest first.</summary>
    public IReadOnlyList<string> Items { get; private set; } = [];

    /// <summary>Raised after <see cref="Items"/> changes, and on the first <see cref="RefreshAsync"/> so the jump list is published once at launch.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Puts <paramref name="path"/> first (an existing entry for the same file moves up), trims to <see cref="Max"/> and saves.
    /// A path that is not fully qualified is ignored. Never throws.
    /// </summary>
    public void Add(string path)
    {
        if (Normalize(path) is not { } full) return;
        var next = new List<string>(Max) { full };
        foreach (var existing in settings.LoadRecents())
        {
            if (next.Count == Max) break;
            if (!Same(existing, full)) next.Add(existing);
        }
        Commit(next);
    }

    /// <summary>Drops <paramref name="path"/> from the list (a moved file, a jump-list item the user removed).</summary>
    public void Remove(string path)
    {
        if (Normalize(path) is not { } full) return;
        var current = settings.LoadRecents();
        if (!current.Any(p => Same(p, full))) return;
        Commit(current.Where(p => !Same(p, full)).ToList());
    }

    /// <summary>"Clear recent list" (H1).</summary>
    public void Clear()
    {
        if (Items.Count == 0 && settings.LoadRecents().Count == 0) return;
        Commit([]);
    }

    /// <summary>
    /// Reads the stored list, checks each file on a pool thread and drops the ones that are gone from <see cref="Items"/> and from the
    /// store. What another add put in the store while the checks ran is kept: only the paths found missing are dropped. Returns <see cref="Items"/>.
    /// </summary>
    public async Task<IReadOnlyList<string>> RefreshAsync()
    {
        var snapshot = settings.LoadRecents().Take(Max).ToList();
        var missing = await Task.Run(() => snapshot.Where(p => !SafeExists(p)).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var stored = settings.LoadRecents();
        var pruned = stored.Where(p => !missing.Contains(p)).Take(Max).ToList();
        if (pruned.Count != stored.Count) settings.SaveRecents(pruned);
        bool first = !_loaded;
        _loaded = true;
        if (first || !pruned.SequenceEqual(Items, StringComparer.OrdinalIgnoreCase))
        {
            Items = pruned;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return Items;
    }

    void Commit(List<string> next)
    {
        settings.SaveRecents(next);
        _loaded = true;
        Items = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    bool SafeExists(string path)
    {
        try
        {
            return _exists(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static string? Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
