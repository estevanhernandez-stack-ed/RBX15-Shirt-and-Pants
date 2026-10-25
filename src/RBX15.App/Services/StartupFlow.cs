using System.IO;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>How the recovery step of the launch ended.</summary>
public enum RecoveryOutcome
{
    /// <summary>No orphaned autosave, or none this launch could claim.</summary>
    None,

    /// <summary>The autosaved work is open, dirty.</summary>
    Restored,

    /// <summary>The user threw the autosave away.</summary>
    Discarded,

    /// <summary>Esc, the close button, a restore that failed to read, or a cancelled unsaved-changes prompt: the autosave is kept for the next launch.</summary>
    Later,
}

/// <summary>
/// What the app does once the window is up (M5 item 3), as a class of its own so the order is tested without starting the app:
/// the crash-recovery prompt comes first, before any other prompt (decision R1). Item 4 adds the launch file after it.
/// <list type="bullet">
/// <item>One prompt per launch, the newest orphan first (R2). The orphan is claimed before it is asked about, so two launches never
///   offer the same file; one lost to another launch gives way to the next. The rest wait for later launches.</item>
/// <item>Restore reads the claimed project off the UI thread, then opens it dirty (<see cref="EditorSession.Recover"/>) and keeps the
///   claimed pair: it is this window's autosave from now on, and the autosave service rewrites it under the same session id.
///   Discard deletes the pair. Later, and a restore that cannot read the file (<see cref="DialogText.LoadFailed"/>), give the pair
///   back to the dead owner's name untouched.</item>
/// </list>
/// </summary>
public sealed class StartupFlow
{
    readonly EditorSession _session;
    readonly RecoveryService _recovery;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ProjectFileService _files;
    readonly string _sessionId;
    readonly Action<IReadOnlyCollection<SKImage>> _release;

    /// <param name="sessionId">This window's autosave session id: the claimed pair is renamed to it.</param>
    /// <param name="release">Frees images nothing references any more; by default <see cref="ProjectFileService.ReleaseOnIdle"/>.</param>
    public StartupFlow(EditorSession session, RecoveryService recovery, IDialogService dialogs, IStatus status, ProjectFileService files,
        string sessionId, Action<IReadOnlyCollection<SKImage>>? release = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        (_session, _recovery, _dialogs, _status, _files, _sessionId) = (session, recovery, dialogs, status, files, sessionId);
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _release = release ?? (images => ProjectFileService.ReleaseOnIdle(dispatcher, images));
    }

    /// <summary>The recovery step. UI thread.</summary>
    public async Task<RecoveryOutcome> RecoverAsync()
    {
        Orphan? claimed = null;
        foreach (var orphan in _recovery.FindOrphans())
        {
            if (_recovery.TryClaim(orphan, _sessionId, out claimed)) break;
        }
        if (claimed is null) return RecoveryOutcome.None;

        switch (_dialogs.AskRestore(claimed.DocumentName, claimed.Sidecar.AutosavedAt))
        {
            case RestoreChoice.Discard:
                _recovery.Delete(claimed);
                return RecoveryOutcome.Discarded;
            case RestoreChoice.Restore:
                return await RestoreAsync(claimed);
            default:
                _recovery.Release(claimed);
                return RecoveryOutcome.Later;
        }
    }

    async Task<RecoveryOutcome> RestoreAsync(Orphan claimed)
    {
        ProjectLoad load;
        try
        {
            load = await Task.Run(() => _recovery.Load(claimed));
        }
        catch (Exception e) when (e is ProjectFormatException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // A corrupt or unreadable autosave is the user's to look at: it goes back, never deleted on a failed read.
            _dialogs.Alert(DialogText.LoadFailed(e.Message));
            _recovery.Release(claimed);
            return RecoveryOutcome.Later;
        }
        // The window was up before the prompt: edits made since would be replaced, so they get the unsaved-changes guard first.
        if (!_files.ConfirmDiscardChanges())
        {
            _release(ProjectFileService.ImagesOf(load.Document));
            _recovery.Release(claimed);
            return RecoveryOutcome.Later;
        }
        _release(_session.Recover(load, claimed.Sidecar.OriginalPath));
        if (load.DroppedLayers.IsEmpty) _status.Clear();
        else _status.Show($"Restored {claimed.DocumentName}. Couldn't load: {string.Join(", ", load.DroppedLayers)}.");
        return RecoveryOutcome.Restored;
    }
}
