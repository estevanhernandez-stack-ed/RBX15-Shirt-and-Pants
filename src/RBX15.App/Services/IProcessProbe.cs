using System.ComponentModel;
using System.Diagnostics;

namespace RBX15.App.Services;

/// <summary>
/// Tells recovery whether an autosave's owner is still alive (M5 item 3). A seam so the tests name which owners are running and
/// never look at the real process table.
/// </summary>
public interface IProcessProbe
{
    /// <summary>True when process <paramref name="pid"/> is running and started at <paramref name="startedUtc"/>: a reused pid is a different process.</summary>
    bool IsRunning(int pid, DateTime startedUtc);
}

/// <summary>The app's probe: the pid and the process start time, both. An owner whose start time cannot be read (access denied) counts as running, so a live window's autosave is never offered.</summary>
public sealed class ProcessProbe : IProcessProbe
{
    /// <summary>The slack between the start time the owner recorded and the one read back.</summary>
    static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    public bool IsRunning(int pid, DateTime startedUtc)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return (process.StartTime.ToUniversalTime() - startedUtc.ToUniversalTime()).Duration() <= Tolerance;
        }
        catch (ArgumentException)
        {
            return false;   // no such process
        }
        catch (InvalidOperationException)
        {
            return false;   // it exited while we looked
        }
        catch (Win32Exception)
        {
            return true;    // alive, but not ours to inspect
        }
    }
}
