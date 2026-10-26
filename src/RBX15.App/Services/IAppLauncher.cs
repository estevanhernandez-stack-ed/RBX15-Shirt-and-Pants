using System.Diagnostics;

namespace RBX15.App.Services;

/// <summary>
/// Starts another window of the app on a project file (M5 decisions S1, R3): every launch is its own process and window, with no
/// pipe or mutex between instances. A seam so the tests record the request and never start a process.
/// </summary>
public interface IAppLauncher
{
    /// <summary>Starts this app with <paramref name="path"/> as its argument. A failure to start is the caller's to report.</summary>
    void Open(string path);

    /// <summary>Starts this app with no argument: a new, empty window (New, Ctrl+N). A failure to start is the caller's to report.</summary>
    void OpenNew();
}

/// <summary>The app's launcher: a self-launch of <see cref="Environment.ProcessPath"/>, the path passed as one quoted argument.</summary>
public sealed class ProcessAppLauncher : IAppLauncher
{
    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("The app's own path is unknown.")) { UseShellExecute = false };
        start.ArgumentList.Add(path);   // quoted for the command line, spaces and all
        Process.Start(start)?.Dispose();
    }

    public void OpenNew()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("The app's own path is unknown.")) { UseShellExecute = false };
        Process.Start(start)?.Dispose();
    }
}
