using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>
/// Returns queued answers in order (null = cancel), first sending each answer's queued previews as a
/// drag would, and records every seed and anchor it was opened with. <see cref="DuringPick"/> runs after
/// the previews, while the picker is still open.
/// </summary>
internal sealed class FakeColorPicker : IColorPicker
{
    readonly Queue<(string? Answer, string[] Previews)> _answers = new();

    public List<string> Seeds { get; } = [];

    public List<object?> Anchors { get; } = [];

    public Action? DuringPick { get; set; }

    public FakeColorPicker Answer(string? hex, params string[] previews)
    {
        _answers.Enqueue((hex, previews));
        return this;
    }

    public string? Pick(string initialHex, Action<string>? preview = null, object? anchor = null)
    {
        Seeds.Add(initialHex);
        Anchors.Add(anchor);
        if (_answers.Count == 0) return null;
        var (answer, previews) = _answers.Dequeue();
        foreach (var p in previews) preview?.Invoke(p);
        DuringPick?.Invoke();
        return answer;
    }
}
