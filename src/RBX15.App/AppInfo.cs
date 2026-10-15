using System.Reflection;
using RBX15.Engine;

namespace RBX15.App;

/// <summary>Product identity shown by the shell.</summary>
public static class AppInfo
{
    /// <summary>
    /// The window and dialog title. 4.1's BrowserWindow title and editor.html's title element carry this
    /// string (main.js:8, editor.html:6; ledger shell-window-title, keep). ProjectIO writes the same
    /// string as a project's <c>app</c> field.
    /// </summary>
    public const string ProductName = ProjectIO.AppName;

    /// <summary>
    /// The assembly's informational version without any "+metadata" suffix: "5.0.0". 4.1 hand-typed
    /// "v4.1" in the header (editor.html:17); v5 reads it from the build (ledger
    /// shell-header-version-hardcoded, fix).
    /// </summary>
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        var info = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? throw new InvalidOperationException("RBX15.App has no informational version.");
        int plus = info.IndexOf('+');
        return plus < 0 ? info : info[..plus];
    }
}
