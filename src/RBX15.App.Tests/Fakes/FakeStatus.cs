using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Records status calls; <see cref="Current"/> is what the strip would show.</summary>
sealed class FakeStatus : IStatus
{
    public List<string> Shown { get; } = [];
    public int Clears { get; private set; }
    public string? Current { get; private set; }

    public void Show(string message)
    {
        Shown.Add(message);
        Current = message;
    }

    public void Clear()
    {
        Clears++;
        Current = null;
    }
}
