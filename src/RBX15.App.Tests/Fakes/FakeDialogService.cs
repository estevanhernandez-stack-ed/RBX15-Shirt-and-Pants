using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Records every dialog; Confirm answers <see cref="ConfirmAnswer"/>.</summary>
sealed class FakeDialogService : IDialogService
{
    public bool ConfirmAnswer { get; set; }
    public List<string> Confirms { get; } = [];
    public List<string> Alerts { get; } = [];

    public bool Confirm(string message)
    {
        Confirms.Add(message);
        return ConfirmAnswer;
    }

    public void Alert(string message) => Alerts.Add(message);
}
