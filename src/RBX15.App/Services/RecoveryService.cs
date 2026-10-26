using System.IO;
using System.Text;
using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// One autosave pair left by a process that is no longer running (M5 item 3).
/// </summary>
/// <param name="SessionId">The pair's file stem: the id of the session that wrote it, or of the one that claimed it.</param>
/// <param name="Sidecar">What the sidecar says; after a claim, the claimer is the owner.</param>
/// <param name="ProjectPath">The autosaved <c>.r15proj</c>.</param>
/// <param name="SidecarPath">The sidecar <c>.json</c>.</param>
/// <param name="Origin">After <see cref="RecoveryService.TryClaim"/>: the orphan as it was before the claim, so a release can put it back. Null otherwise.</param>
public sealed record Orphan(string SessionId, AutosaveSidecar Sidecar, string ProjectPath, string SidecarPath, Orphan? Origin = null)
{
    /// <summary>The file name the document was saved under, or <see cref="DialogText.Untitled"/>.</summary>
    public string DocumentName => ProjectFileService.DocumentName(Sidecar.OriginalPath);
}

/// <summary>
/// Crash recovery (spec §3 "Upgrades"; M5 decisions R1, R2): finds the autosaves of processes that died, claims one for this
/// process, and reads, releases or deletes it. Nothing here deletes a file the user has not decided about: an unreadable
/// sidecar, a pair with no project file and a project that fails to read are all skipped or kept, and logged.
/// <list type="bullet">
/// <item>An orphan is a sidecar whose owner is not running: no such pid, or the pid now belongs to a process that started at another time.</item>
/// <item>A claim moves the pair to the claimer's session id, with the claimer as the owner, before anything is asked (R2). An exclusive
///   lock file is the arbiter: of two processes that claim the same orphan, exactly one gets it. A claimed pair looks live to every
///   other launch, and dead again if the claimer crashes, so the next launch offers it again.</item>
/// </list>
/// </summary>
/// <param name="pid">This process's id (the claimed sidecar's owner).</param>
/// <param name="processStart">This process's start time.</param>
/// <param name="log">Where a skipped or failed file is reported (<c>ErrorLog.Write</c>); null logs nothing.</param>
public sealed class RecoveryService(AppStorage storage, IProcessProbe processes, Func<int> pid, Func<DateTime> processStart, Action<Exception, string>? log = null)
{
    const string ClaimSuffix = ".claim";
    const string LockSuffix = ".claim-lock";
    const string Source = "recovery";

    /// <summary>The orphans, newest autosave first.</summary>
    public IReadOnlyList<Orphan> FindOrphans()
    {
        string[] sidecars;
        try
        {
            if (!Directory.Exists(storage.AutosaveDirectory)) return [];
            sidecars = Directory.GetFiles(storage.AutosaveDirectory, "*.json");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log?.Invoke(e, Source);
            return [];
        }
        var found = new List<Orphan>();
        foreach (var sidecarPath in sidecars)
        {
            if (!sidecarPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            string id = Path.GetFileNameWithoutExtension(sidecarPath);
            string projectPath = Path.Combine(storage.AutosaveDirectory, id + ".r15proj");
            AutosaveSidecar? meta;
            try
            {
                meta = AutosaveService.ReadSidecar(File.ReadAllText(sidecarPath));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log?.Invoke(e, Source);
                continue;
            }
            if (meta is null || meta.Pid <= 0)
            {
                log?.Invoke(new InvalidDataException($"The autosave sidecar {sidecarPath} is unreadable."), Source);
                continue;
            }
            if (!File.Exists(projectPath))
            {
                log?.Invoke(new FileNotFoundException($"The autosave sidecar {sidecarPath} has no project file."), Source);
                continue;
            }
            if (processes.IsRunning(meta.Pid, meta.ProcessStartUtc)) continue;
            found.Add(new Orphan(id, meta, projectPath, sidecarPath));
        }
        return found.OrderByDescending(o => o.Sidecar.AutosavedAt).ToList();
    }

    /// <summary>
    /// Renames <paramref name="orphan"/>'s pair to <paramref name="sessionId"/> and makes this process its owner. False, with the pair
    /// untouched, when another process claimed or deleted it first or is claiming it now.
    /// </summary>
    /// <param name="claimed">The pair under its new name; null when false.</param>
    public bool TryClaim(Orphan orphan, string sessionId, out Orphan? claimed)
    {
        ArgumentNullException.ThrowIfNull(orphan);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        claimed = null;
        string project = Path.Combine(storage.AutosaveDirectory, sessionId + ".r15proj");
        string sidecar = Path.Combine(storage.AutosaveDirectory, sessionId + ".json");
        string holding = sidecar + ClaimSuffix;   // not a .json, so no other launch lists it while the claim finishes
        // The arbiter is an exclusive create, not a rename: a rename goes through a handle opened by name, so two claimers that both
        // opened the sidecar before either renamed it would both succeed. Delete-on-close drops the lock if the claimer dies mid-claim.
        FileStream gate;
        try
        {
            gate = new FileStream(Path.Combine(storage.AutosaveDirectory, orphan.SessionId + LockSuffix), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (DirectoryNotFoundException)
        {
            return false;   // the pair is gone
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;   // another process holds this claim (the lock exists, or is going away)
        }
        using (gate)
        {
            try
            {
                File.Move(orphan.SidecarPath, holding);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;   // another process claimed or deleted it before this one got the lock
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log?.Invoke(e, Source);
                return false;
            }
            bool projectMoved = false;
            try
            {
                File.Move(orphan.ProjectPath, project);
                projectMoved = true;
                var owned = orphan.Sidecar with { Pid = pid(), ProcessStartUtc = processStart().ToUniversalTime() };
                string temp = sidecar + ".tmp";
                File.WriteAllText(temp, AutosaveService.WriteSidecar(owned), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temp, sidecar, overwrite: true);
                File.Delete(holding);
                claimed = new Orphan(sessionId, owned, project, sidecar, orphan);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log?.Invoke(e, Source);
                try
                {
                    if (projectMoved) File.Move(project, orphan.ProjectPath, overwrite: true);
                    File.Move(holding, orphan.SidecarPath, overwrite: true);
                    File.Delete(sidecar + ".tmp");
                }
                catch (Exception undo) when (undo is IOException or UnauthorizedAccessException)
                {
                    log?.Invoke(undo, Source);   // the pair stays where it is: nothing is deleted
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Reads the claimed project (the size cap, then <see cref="ProjectIO.Read"/>). Throws what Open's read throws
    /// (<see cref="ProjectFormatException"/>, <see cref="IOException"/>); the pair is not touched.
    /// </summary>
    public ProjectLoad Load(Orphan orphan)
    {
        ArgumentNullException.ThrowIfNull(orphan);
        return ProjectFileService.ReadFile(orphan.ProjectPath);
    }

    /// <summary>Removes the pair (Discard). The sidecar goes first: it is what recovery lists.</summary>
    public void Delete(Orphan orphan)
    {
        ArgumentNullException.ThrowIfNull(orphan);
        Remove(orphan.SidecarPath);
        Remove(orphan.ProjectPath);
    }

    /// <summary>
    /// Gives a claimed pair back (Later, or a restore that failed): the project and the original sidecar go back under the dead
    /// owner's name, so the next launch offers it again. Nothing is deleted if the move fails.
    /// </summary>
    public void Release(Orphan claimed)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        if (claimed.Origin is not { } origin) return;
        try
        {
            File.Move(claimed.ProjectPath, origin.ProjectPath, overwrite: true);
            string temp = origin.SidecarPath + ".tmp";
            File.WriteAllText(temp, AutosaveService.WriteSidecar(origin.Sidecar), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temp, origin.SidecarPath, overwrite: true);   // last: it is what makes the pair listable
            Remove(claimed.SidecarPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log?.Invoke(e, Source);
        }
    }

    static void Remove(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
