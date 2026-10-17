using System.IO;
using RBX15.App.Services;

namespace RBX15.App.Testing;

/// <summary>
/// The file dialogs under the bridge (M4a interfaces R10): no native dialog ever opens. Each call takes the oldest
/// answer the <c>file-dialog</c> command queued; an empty queue or a null answer is a cancel. Every call is written
/// to the log (stdout in the app) as <c>RBX15-BRIDGE-DIALOG &lt;method&gt; &lt;arg&gt; -&gt; &lt;answer&gt;</c>, so the
/// smoke driver sees what the app asked for. OpenImages splits an answer on '|' (no Windows path holds one).
/// </summary>
internal sealed class BridgeFileDialogs(TextWriter log) : IFileDialogs
{
    public const string LogPrefix = "RBX15-BRIDGE-DIALOG ";

    readonly Queue<string?> _answers = new();
    readonly TextWriter _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <summary>Queues the next answer: a path, or null to cancel.</summary>
    public void Enqueue(string? answer) => _answers.Enqueue(answer);

    public IReadOnlyList<string> OpenImages() =>
        Next(nameof(OpenImages), "") is { } answer ? answer.Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

    public string? SavePng(string suggestedFileName) => Next(nameof(SavePng), suggestedFileName);

    public string? OpenProject(string? initialDirectory) => Next(nameof(OpenProject), initialDirectory ?? "");

    public string? SaveProject(string suggestedFileName, string? initialDirectory) => Next(nameof(SaveProject), suggestedFileName);

    public string? PickFolder(string? initialDirectory) => Next(nameof(PickFolder), initialDirectory ?? "");

    string? Next(string method, string arg)
    {
        bool queued = _answers.TryDequeue(out var answer);
        _log.WriteLine($"{LogPrefix}{method} {arg} -> {(queued ? answer ?? "cancel" : "cancel (queue empty)")}");
        _log.Flush();
        return queued ? answer : null;
    }
}
