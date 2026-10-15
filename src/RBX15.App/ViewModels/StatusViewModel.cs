using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The status strip (spec §4, new in v5; ledger diag-console-only-errors, dialogs-inventory). One
/// message at a time; a new message replaces the old one; Dismiss hides it.
/// </summary>
public sealed class StatusViewModel : ObservableObject, IStatus
{
    string? _message;

    public StatusViewModel() => DismissCommand = new RelayCommand(Clear);

    public string? Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(IsVisible));
        }
    }

    public bool IsVisible => _message is not null;

    public IRelayCommand DismissCommand { get; }

    public void Show(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        Message = message;
    }

    public void Clear() => Message = null;
}
