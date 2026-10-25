using System.IO;
using System.Text.RegularExpressions;
using RBX15.App.Services;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>The local error log and the crash policy (M5 decisions E1, E2; ledger upgrade-error-log).</summary>
public sealed class ErrorLogTests : IDisposable
{
    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    readonly string _dir = Path.Combine(Path.GetTempPath(), "rbx15-errorlog-" + Guid.NewGuid().ToString("N"));
    static readonly FixedClock Clock = new(new DateTimeOffset(2026, 10, 25, 12, 30, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    static Exception Thrown(Func<Exception> make)
    {
        try { throw make(); }
        catch (Exception e) { return e; }
    }

    [Fact] // AppStorage: the three folders sit under one root (A1)
    public void The_storage_root_holds_settings_autosave_and_logs()
    {
        var storage = new AppStorage(_dir);
        Assert.Equal(_dir, storage.SettingsDirectory);
        Assert.Equal(Path.Combine(_dir, "autosave"), storage.AutosaveDirectory);
        Assert.Equal(Path.Combine(_dir, "logs"), storage.LogDirectory);
    }

    [Fact] // E1: type, HResult, message and stack for the exception and its inner one, plus the version and OS lines
    public void An_exception_with_an_inner_exception_writes_one_entry_with_both()
    {
        var inner = Thrown(() => new InvalidOperationException("inner failure"));
        var outer = Thrown(() => new ApplicationException("outer failure", inner));
        var log = new ErrorLog(_dir, Clock);

        log.Write(outer, "dispatcher");

        string text = File.ReadAllText(log.FilePath);
        Assert.Single(Regex.Matches(text, "^=== ", RegexOptions.Multiline));
        Assert.Contains("=== 2026-10-25T12:30:00.000Z", text);
        Assert.Contains("source: dispatcher", text);
        Assert.Contains($"app: {AppInfo.ProductName} {AppInfo.Version}", text);
        Assert.Matches(@"(?m)^os: \S", text);
        Assert.Contains($"System.ApplicationException (HResult 0x{outer.HResult:X8})", text);
        Assert.Contains($"System.InvalidOperationException (HResult 0x{inner.HResult:X8})", text);
        Assert.Contains("outer failure", text);
        Assert.Contains("inner failure", text);
        Assert.True(Regex.Matches(text, @"(?m)^\s+at \S").Count >= 2, "a stack for each exception");
    }

    [Fact] // E1: the profile path, and so the user name, never reaches the file
    public void The_user_profile_path_is_written_as_USERPROFILE()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var log = new ErrorLog(_dir, Clock);

        log.Write(new IOException($"Could not read {Path.Combine(profile, "art", "shirt.r15proj")} or {profile.Replace('\\', '/')}/x.png"), "test");

        string text = File.ReadAllText(log.FilePath);
        Assert.Contains(@"%USERPROFILE%\art\shirt.r15proj", text);
        Assert.Contains("%USERPROFILE%/x.png", text);
        Assert.DoesNotContain(profile, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // E1: error.log rolls to error.1.log, and a later roll replaces it: never more than two files
    public void Writes_past_the_ceiling_roll_to_one_older_file()
    {
        var log = new ErrorLog(_dir, Clock, rollBytes: 2000);
        var e = new InvalidOperationException(new string('x', 600));

        for (int i = 0; i < 12; i++) log.Write(e, "test");

        string[] files = Directory.GetFiles(_dir).Select(f => Path.GetFileName(f)!).Order().ToArray();
        Assert.Equal(["error.1.log", "error.log"], files);
        Assert.True(new FileInfo(log.FilePath).Length <= 2000);
        Assert.True(new FileInfo(Path.Combine(_dir, "error.1.log")).Length <= 2000);
    }

    [Fact] // E1: a log that cannot be written must not take the crash path down with it
    public void A_directory_that_cannot_be_created_makes_Write_return_quietly()
    {
        string blocker = Path.Combine(_dir, "blocker");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(blocker, "a file where the log folder should be");

        var log = new ErrorLog(Path.Combine(blocker, "logs"), Clock);

        log.Write(new InvalidOperationException("x"), "test");
        Assert.False(Directory.Exists(Path.Combine(blocker, "logs")));
    }

    [Fact] // E1: a read-only log file is swallowed too
    public void A_read_only_log_file_makes_Write_return_quietly()
    {
        var log = new ErrorLog(_dir, Clock);
        log.Write(new InvalidOperationException("first"), "test");
        File.SetAttributes(log.FilePath, FileAttributes.ReadOnly);
        try
        {
            log.Write(new InvalidOperationException("second"), "test");
            Assert.DoesNotContain("second", File.ReadAllText(log.FilePath));
        }
        finally { File.SetAttributes(log.FilePath, FileAttributes.Normal); }
    }

    // ------------------------------------------------------------------ CrashHandler

    sealed class Calls
    {
        public List<string> Order { get; } = [];
        public string? Shown;

        public CrashHandler Handler(bool autosaveThrows = false)
        {
            var h = new CrashHandler(
                (e, source) => Order.Add($"log:{source}:{e.Message}"),
                @"C:\logs\error.log",
                text => { Shown = text; Order.Add("show"); },
                () => Order.Add("exit"));
            h.EmergencyAutosave = () =>
            {
                Order.Add("autosave");
                if (autosaveThrows) throw new IOException("disk full");
            };
            return h;
        }
    }

    [Fact] // E2: the dispatcher handler logs, emergency-autosaves, shows the fatal text, then exits, in that order
    public void A_dispatcher_exception_logs_then_autosaves_then_shows_then_exits()
    {
        var calls = new Calls();
        calls.Handler().HandleDispatcher(new InvalidOperationException("boom"));

        Assert.Equal(["log:dispatcher:boom", "autosave", "show", "exit"], calls.Order);
        Assert.Equal(DialogText.FatalError(@"C:\logs\error.log"), calls.Shown);
    }

    [Fact] // E2: same policy for an exception on any thread
    public void A_domain_exception_logs_then_autosaves_then_shows_then_exits()
    {
        var calls = new Calls();
        calls.Handler().HandleDomain(new InvalidOperationException("boom"));

        Assert.Equal(["log:appdomain:boom", "autosave", "show", "exit"], calls.Order);
    }

    [Fact] // E2: an unobserved task exception never reached the UI; it is logged and the app keeps running
    public void An_unobserved_task_exception_is_only_logged()
    {
        var calls = new Calls();
        calls.Handler().HandleUnobservedTask(new InvalidOperationException("late"));

        Assert.Equal(["log:unobserved task:late"], calls.Order);
    }

    [Fact] // item 2 fills the hook: the handler calls whatever is set on EmergencyAutosave, once
    public void The_emergency_autosave_hook_is_called_once_per_fatal_exception()
    {
        int saved = 0;
        var handler = new Calls().Handler();
        handler.EmergencyAutosave = () => saved++;

        handler.HandleDispatcher(new InvalidOperationException("boom"));

        Assert.Equal(1, saved);
    }

    [Fact] // a failing emergency autosave must not keep the process alive
    public void A_failing_emergency_autosave_still_shows_the_dialog_and_exits()
    {
        var calls = new Calls();
        calls.Handler(autosaveThrows: true).HandleDispatcher(new InvalidOperationException("boom"));

        Assert.Equal(["log:dispatcher:boom", "autosave", "show", "exit"], calls.Order);
    }

    [Fact] // E2's text, verbatim, names the log
    public void The_fatal_text_names_the_log()
    {
        Assert.Equal(
            @"Something went wrong and RBX15 has to close. Your work was autosaved and will be offered next time you open the app. Details: C:\x\error.log",
            DialogText.FatalError(@"C:\x\error.log"));
    }

    [Fact] // E1: the log is never sent; the app holds no network client at all
    public void The_app_source_has_no_network_client()
    {
        string app = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "src", "RBX15.App");
        string sep = Path.DirectorySeparatorChar.ToString();
        var banned = new Regex(@"\b(HttpClient|WebClient|Socket|WebRequest)\b");
        var hits = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
            .Where(f => banned.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(hits);
    }
}
