namespace RBX15.App.Services;

/// <summary>
/// Where the app keeps what it writes locally (M5 decision A1): one root, <c>App.SettingsDirectory</c>
/// (%LOCALAPPDATA%\RBX15), holding <c>settings.json</c>, <c>autosave\</c> and <c>logs\</c>. The packaged build redirects the
/// root into the package's local data; the portable zip writes the real folder. The bridged launch and the tests pass a temp root.
/// </summary>
public sealed record AppStorage(string Root)
{
    /// <summary>Holds <c>settings.json</c>.</summary>
    public string SettingsDirectory => Root;

    /// <summary>Holds the autosave pairs (<c>&lt;session-id&gt;.r15proj</c> and its sidecar).</summary>
    public string AutosaveDirectory => System.IO.Path.Combine(Root, "autosave");

    /// <summary>Holds <c>error.log</c> and <c>error.1.log</c>.</summary>
    public string LogDirectory => System.IO.Path.Combine(Root, "logs");
}
