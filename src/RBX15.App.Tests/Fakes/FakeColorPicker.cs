using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Returns queued answers in order (null = cancel) and records every seed it was opened with.</summary>
internal sealed class FakeColorPicker : IColorPicker
{
    readonly Queue<string?> _answers = new();

    public List<string> Seeds { get; } = [];

    public FakeColorPicker Answer(string? hex)
    {
        _answers.Enqueue(hex);
        return this;
    }

    public string? Pick(string initialHex)
    {
        Seeds.Add(initialHex);
        return _answers.Count > 0 ? _answers.Dequeue() : null;
    }
}
