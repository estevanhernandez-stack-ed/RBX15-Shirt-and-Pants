using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Records every dialog; Confirm answers <see cref="ConfirmAnswer"/>, the unsaved-changes prompt <see cref="SaveChoiceAnswer"/>.</summary>
sealed class FakeDialogService : IDialogService
{
    public bool ConfirmAnswer { get; set; }
    public List<string> Confirms { get; } = [];
    public List<string> Alerts { get; } = [];

    /// <summary>The prompt's answer; Cancel by default, so a test that forgets to answer never discards work.</summary>
    public SaveChoice SaveChoiceAnswer { get; set; } = SaveChoice.Cancel;

    /// <summary>The document name each unsaved-changes prompt named.</summary>
    public List<string> SaveChanges { get; } = [];

    public bool Confirm(string message)
    {
        Confirms.Add(message);
        return ConfirmAnswer;
    }

    public void Alert(string message) => Alerts.Add(message);

    public SaveChoice AskSaveChanges(string documentName)
    {
        SaveChanges.Add(documentName);
        return SaveChoiceAnswer;
    }
}
