namespace RBX15.App.Services;

/// <summary>
/// What an unhandled exception does (M5 decision E2), as plain methods so the policy is testable without crashing the test
/// host; <c>App.OnStartup</c> subscribes the three events to them. A dispatcher or domain exception is logged, the document is
/// emergency-autosaved if dirty, one themed dialog names the log, and the process ends: continuing after an unknown failure
/// risks saving a corrupt document over a good one. An unobserved task exception is logged only (it never reached the UI).
/// </summary>
/// <param name="log">Writes one log entry (<c>ErrorLog.Write</c>).</param>
/// <param name="logPath">The log file the dialog names.</param>
/// <param name="showFatal">Shows the fatal dialog with the given text.</param>
/// <param name="exit">Ends the process.</param>
/// <param name="emergencyAutosave">Item 2 fills this with <c>AutosaveService.WriteNow</c>; a no-op until then.</param>
public sealed class CrashHandler(Action<Exception, string> log, string logPath, Action<string> showFatal, Action exit, Action? emergencyAutosave = null)
{
    /// <summary>The emergency autosave hook; set once the autosave service exists.</summary>
    public Action EmergencyAutosave { get; set; } = emergencyAutosave ?? (() => { });

    public void HandleDispatcher(Exception e) => Fatal(e, "dispatcher");

    public void HandleDomain(Exception e) => Fatal(e, "appdomain");

    public void HandleUnobservedTask(Exception e) => log(e, "unobserved task");

    void Fatal(Exception e, string source)
    {
        log(e, source);
        // Each step is best effort: a failing autosave or dialog must not keep the process alive.
        try { EmergencyAutosave(); } catch (Exception) { }
        try { showFatal(DialogText.FatalError(logPath)); } catch (Exception) { }
        exit();
    }
}
