using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// What the layer keys do to the selected layer (M4b item 11; ledger keys-arrow-nudge, keys-rotate-brackets, keys-scale-plus-minus,
/// keys-delete, keys-duplicate-ctrl-d; editor.js:1788, :1801-1827). Each is one undo step through the session (the History
/// contract; 4.1 pushed none for the transforms), and a press that changes nothing records nothing. The selection is read when the
/// key arrives. <see cref="RBX15.App.Controls.ShortcutRouter"/> decides which key means which of these; this only does them.
/// </summary>
public sealed class LayerKeys
{
    /// <summary>The arrow keys' step in template pixels (editor.js:1806), and with Shift.</summary>
    public const double NudgeStep = 1, NudgeStepBig = 10;

    /// <summary>The bracket keys' step in degrees (editor.js:1807), and with Shift.</summary>
    public const double RotateStep = 5, RotateStepBig = 15;

    /// <summary>The + and - keys' factors (editor.js:1823-1825).</summary>
    public const double ScaleUpFactor = 1.1, ScaleDownFactor = 0.9;

    readonly EditorSession _session;

    public LayerKeys(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        // Always enabled, for the window's Ctrl+D binding: 4.1 swallowed the key with nothing selected (it blocked the browser's
        // bookmark), and a disabled command would leave the key unhandled. With nothing selected it does nothing.
        DuplicateCommand = new RelayCommand(Duplicate);
    }

    /// <summary>Ctrl+D (editor.js:1788): duplicates the selected layer, or does nothing.</summary>
    public IRelayCommand DuplicateCommand { get; }

    /// <summary>The arrows: x and y move by (<paramref name="dx"/>, <paramref name="dy"/>) template pixels.</summary>
    public void Nudge(double dx, double dy) => Edit(id => d => LayerOps.Nudge(d, id, dx, dy));

    /// <summary>The brackets: the rotation steps by <paramref name="degrees"/>, wrapped into [-180, 180).</summary>
    public void Rotate(double degrees) => Edit(id => d => LayerOps.RotateStep(d, id, degrees));

    /// <summary>R: rotation 0.</summary>
    public void ResetRotation() => Edit(id => d => LayerOps.SetRotation(d, id, 0));

    /// <summary>+ and -: the scale times <paramref name="factor"/>, about the top-left.</summary>
    public void Scale(double factor) => Edit(id => d => LayerOps.ScaleStep(d, id, factor));

    /// <summary>Delete and Backspace (removeLayer, editor.js:562-568): the selection falls to the new top layer.</summary>
    public void Delete()
    {
        if (_session.SelectedLayerId is not int id) return;
        _session.ApplyWithSelection(s => LayerOps.Remove(s, id));
    }

    /// <summary>duplicateLayer (editor.js:542-560) on the selection: one step, the copy above the original and selected.</summary>
    public void Duplicate()
    {
        if (_session.SelectedLayerId is not int id) return;
        int newId = _session.TakeId();
        _session.ApplyWithSelection(s => LayerOps.Duplicate(s, id, newId));
    }

    void Edit(Func<int, Func<Document, Document>> change)
    {
        if (_session.SelectedLayerId is not int id) return;
        _session.Apply(change(id));
    }
}
