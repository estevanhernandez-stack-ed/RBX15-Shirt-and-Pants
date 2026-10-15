using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Mode group's SHIRT/PANTS button (editor.html:31-40, editor.js:1692-1725; ledger
/// <c>home-mode-toggle</c>, <c>mode-toggle-button</c>). One click is one undo step that switches the
/// mode and wipes every region fill (<c>mode-switch-undo-and-callers</c>, keep). The label follows the
/// document, so undo and redo flip it back (editor.js:1427).
/// </summary>
public sealed class ModeGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    TemplateMode _mode;

    public ModeGroupViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _mode = session.Document.Mode;
        ToggleCommand = new RelayCommand(() => _session.Apply(DocumentOps.ToggleMode));
        session.Changed += (_, e) =>
        {
            if (!e.What.HasFlag(SessionChange.Document) || _session.Document.Mode == _mode) return;
            _mode = _session.Document.Mode;
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Label));
        };
    }

    public TemplateMode Mode => _mode;

    /// <summary>"SHIRT" or "PANTS" (editor.js:1702, :1706; editor.html:33).</summary>
    public string Label => _mode == TemplateMode.Shirt ? "SHIRT" : "PANTS";

    public IRelayCommand ToggleCommand { get; }
}
