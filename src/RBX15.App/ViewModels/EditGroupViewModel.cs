using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Edit group (editor.html:77-84; ledger <c>home-edit-group</c>, keep). Undo and Redo can run only
/// when their stack has a step, so both start disabled and grey out exactly when updateHistoryButtons
/// did (editor.js:1470-1475). Inside a gesture (a slider drag) the first change makes Undo possible
/// while only Document is raised, so Document changes during a gesture refresh them too (critic L1;
/// M4's Ctrl+Z mid-drag relies on it). Clear removes every layer as one step (editor.js:1394,
/// <c>btn-clear</c>, keep) and never confirms. It can run only while the document has layers (spec §4:
/// every command knows when it can run; controller ruling, 4.1 left it always enabled).
/// </summary>
public sealed class EditGroupViewModel
{
    readonly EditorSession _session;

    public EditGroupViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        UndoCommand = new RelayCommand(() => _session.Undo(), () => _session.CanUndo);
        RedoCommand = new RelayCommand(() => _session.Redo(), () => _session.CanRedo);
        ClearCommand = new RelayCommand(() => _session.Apply(DocumentOps.ClearLayers), () => _session.Document.Layers.Count > 0);
        session.Changed += (_, e) =>
        {
            bool document = e.What.HasFlag(SessionChange.Document);
            if (document) ClearCommand.NotifyCanExecuteChanged();
            bool history = e.What.HasFlag(SessionChange.History);
            bool gestureEdit = document && _session.InGesture;
            if (!history && !gestureEdit) return;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
    }

    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand ClearCommand { get; }
}
