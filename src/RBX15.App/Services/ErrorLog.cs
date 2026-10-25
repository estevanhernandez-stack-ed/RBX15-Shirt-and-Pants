using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RBX15.App.Services;

/// <summary>
/// The local error log (M5 decisions E1, E2; spec §3 "Upgrades": it replaces DevTools as the smoke diagnostic). One entry
/// per unhandled exception in <c>error.log</c>, rolled to <c>error.1.log</c> when the next entry would pass
/// <paramref name="rollBytes"/>, so at most two files and about twice the ceiling on disk. An entry holds the time, app and OS
/// versions, and each exception's type, HResult, message and stack, inner exceptions included. It never holds document
/// content or layer names, the user profile path is rewritten to <c>%USERPROFILE%</c>, and nothing is ever sent.
/// </summary>
public sealed class ErrorLog(string directory, TimeProvider clock, long rollBytes = ErrorLog.DefaultRollBytes)
{
    public const long DefaultRollBytes = 256 * 1024;
    public const string FileName = "error.log";
    public const string RolledFileName = "error.1.log";

    readonly object _gate = new();

    /// <summary>The live log file; the crash dialog and SMOKE_TEST name it.</summary>
    public string FilePath { get; } = Path.Combine(directory, FileName);

    /// <summary>
    /// Appends one entry for <paramref name="e"/> (<paramref name="source"/> names the handler that caught it). Never
    /// throws: a log that can fail the crash path would hide the crash.
    /// </summary>
    public void Write(Exception e, string source)
    {
        try
        {
            string entry = Redact(Format(e, source)) + Environment.NewLine;
            lock (_gate)
            {
                Directory.CreateDirectory(directory);
                long length = File.Exists(FilePath) ? new FileInfo(FilePath).Length : 0;
                if (length > 0 && length + Encoding.UTF8.GetByteCount(entry) > rollBytes)
                    File.Move(FilePath, Path.Combine(directory, RolledFileName), overwrite: true);
                File.AppendAllText(FilePath, entry, new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // Best effort by design (E1).
        }
    }

    string Format(Exception e, string source)
    {
        var sb = new StringBuilder();
        sb.Append("=== ").AppendLine(clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        sb.Append("source: ").AppendLine(source);
        sb.Append("app: ").Append(AppInfo.ProductName).Append(' ').AppendLine(AppInfo.Version);
        sb.Append("os: ").AppendLine(RuntimeInformation.OSDescription);
        for (Exception? current = e; current is not null; current = current.InnerException)
        {
            if (current != e) sb.AppendLine("--- inner exception");
            sb.Append(current.GetType().FullName).Append(" (HResult 0x").Append(current.HResult.ToString("X8")).AppendLine(")");
            sb.AppendLine(current.Message);
            sb.AppendLine(current.StackTrace ?? "(no stack)");
        }
        return sb.ToString();
    }

    /// <summary>E1: the profile path (and so the user name) never reaches the file.</summary>
    static string Redact(string text)
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length == 0) return text;
        text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text.Replace(profile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
    }
}
