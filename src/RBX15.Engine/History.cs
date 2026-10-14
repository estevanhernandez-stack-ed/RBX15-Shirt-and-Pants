namespace RBX15.Engine;

/// <summary>
/// One recoverable editor state: the immutable <see cref="Document"/> and the selected layer id.
/// The document and its layer bitmaps are shared by reference, never copied or re-encoded.
/// 4.1 serialized every layer as a PNG data URL in every snapshot (editor.js:1404-1415); see the
/// approved divergence <c>history-snapshot-memory</c>.
/// </summary>
public sealed record EditorSnapshot(Document Document, int? SelectedLayerId);

/// <summary>
/// Undo and redo stacks, ported from lib/history.js:25-58. The undo stack holds PRIOR states: the
/// caller records the state from before a change, and <see cref="Undo"/>/<see cref="Redo"/> take the
/// live state so they can park it on the opposite stack (lib/history.js:42-53).
/// <para>
/// <b>One step per gesture is the caller's job</b> (the M3/M4 editor session and view-models), not
/// History's. History stores exactly what it is given: it never merges, compares or drops snapshots.
/// Spec §5 ("Undo", docs/superpowers/specs/2026-10-11-native-v5-port-design.md) sets the rule:
/// every document change is exactly one step, with no empty or double steps. A gesture is one
/// complete user action that changes the document: a press-drag-release on the canvas (move,
/// resize, rotate, a shape, an eraser stroke), one slider or color-picker drag from press to
/// release, one key press (nudge, rotate, reset, scale), or one command (a button, a preset, a fill,
/// add, duplicate, delete, reorder, a mode switch). For each gesture the caller calls
/// <see cref="Record"/> once, with the snapshot from before the gesture, and only when the gesture
/// actually changed the document. A preset that runs several placement helpers is still one record.
/// </para>
/// <para>
/// Restore is synchronous: the caller adopts the returned snapshot as its live state, and nothing
/// loads (divergences <c>undo-restore</c>, <c>undo-rapid-restore-race</c>). The returned
/// <see cref="EditorSnapshot.SelectedLayerId"/> is the live selection when that layer exists in the
/// returned document, otherwise null; 4.1 deselected on every restore (editor.js:1451). Layer ids can
/// come back through undo and redo, so the caller's id counter must only grow (editor.js:1465).
/// </para>
/// <para>
/// Undo depth is <see cref="Capacity"/> (30, editor.js:86), evicting the oldest. Redo is capped at
/// the same capacity (divergence <c>history-redo-stack-uncapped</c>). Because each step moves one
/// entry between the stacks and <see cref="Record"/> empties redo, the two counts never sum past the
/// capacity, so the caps are defensive. Not thread-safe; use it from the UI thread.
/// </para>
/// </summary>
public sealed class History
{
    readonly List<EditorSnapshot> _undo = [];
    readonly List<EditorSnapshot> _redo = [];

    /// <param name="capacity">Maximum undo depth (and redo depth). The editor uses 30 (editor.js:86).</param>
    public History(int capacity = 30)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Undo depth, the <c>undo</c> half of 4.1's <c>sizes()</c> (lib/history.js:57).</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Redo depth, the <c>redo</c> half of 4.1's <c>sizes()</c> (lib/history.js:57).</summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    /// Records the state from BEFORE a change: pushes it onto undo, evicts the oldest past
    /// <see cref="Capacity"/>, and clears redo, because a new edit abandons the redo branch
    /// (lib/history.js:33-36).
    /// </summary>
    public void Record(EditorSnapshot before)
    {
        ArgumentNullException.ThrowIfNull(before);
        PushCapped(_undo, before);
        _redo.Clear();
    }

    /// <summary>
    /// Empties both stacks. Opening a project calls it (spec §3, divergences
    /// <c>project-load-history-interplay</c> and <c>project-load-flow</c>): 4.1's loadProject neither
    /// pushed nor cleared history (editor.js:2593-2680), so undo after an open restored the previous
    /// session's document.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>
    /// Reverts one step. Returns null, parking nothing, when there is nothing to undo
    /// (lib/history.js:43). Otherwise parks <paramref name="current"/> on redo and returns the
    /// previous state with the selection resolved (lib/history.js:44-45).
    /// </summary>
    public EditorSnapshot? Undo(EditorSnapshot current) => Step(_undo, _redo, current);

    /// <summary>Reapplies one undone step. The mirror image of <see cref="Undo"/> (lib/history.js:49-53).</summary>
    public EditorSnapshot? Redo(EditorSnapshot current) => Step(_redo, _undo, current);

    EditorSnapshot? Step(List<EditorSnapshot> from, List<EditorSnapshot> to, EditorSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (from.Count == 0) return null;
        PushCapped(to, current);
        var target = from[^1];
        from.RemoveAt(from.Count - 1);
        int? selected = current.SelectedLayerId is int id && target.Document.Layers.Exists(l => l.Id == id) ? id : null;
        return target with { SelectedLayerId = selected };
    }

    void PushCapped(List<EditorSnapshot> stack, EditorSnapshot snapshot)
    {
        stack.Add(snapshot);
        if (stack.Count > Capacity) stack.RemoveAt(0);
    }
}
