using RBX15.Engine;

namespace RBX15.App;

/// <summary>What a <see cref="EditorSession.Changed"/> event reports.</summary>
[Flags]
public enum SessionChange
{
    None = 0,
    Document = 1,
    Selection = 2,
    History = 4,
    Title = 8,
}

public sealed class SessionChangedEventArgs(SessionChange what) : EventArgs
{
    public SessionChange What { get; } = what;
}

/// <summary>
/// The one editing state the shell's view-models share (spec §1 and §4): the immutable engine
/// <see cref="Engine.Document"/>, its <see cref="History"/>, the selection, the layer id counter, the
/// current file and the unsaved-changes flag. WPF-free, so it is tested headless.
/// <para>
/// <b>Every document change is exactly one undo step</b> (spec §5; ledger undo-empty-steps,
/// file-undo-snapshot-scope and undo-coverage-gaps, all fix). <see cref="Apply"/> records the state
/// from before a change only when <see cref="Engine.Document.ContentEquals"/> says the content changed,
/// so a no-op never pushes a step (4.1 pushed before no-op actions, editor.js:1117, :1601-1606), and a
/// preset written as one lambda is one step (4.1 pushed two, editor.js:1734-1749). A drag is one
/// <see cref="BeginGesture"/> scope: each change inside it is adopted at once, and the scope records
/// one step when it closes.
/// </para>
/// <para>
/// Selection survives undo and redo when its layer still exists (ledger undo-restore-ui-side-effects,
/// fix; 4.1 deselected on every restore, editor.js:1451). <see cref="History"/> resolves it and the
/// session adopts it. Not thread-safe; use it from the UI thread.
/// </para>
/// </summary>
public sealed class EditorSession
{
    readonly History _history;
    int _gestureDepth;
    EditorSnapshot? _gestureStart;
    string _templateTitle = "";

    /// <param name="document">The starting document; null gives 4.1's startup state (editor.js:76-107, the <see cref="Engine.Document"/> defaults).</param>
    /// <param name="historyCapacity">Undo depth; 4.1 uses 30 (editor.js:86).</param>
    public EditorSession(Document? document = null, int historyCapacity = 30)
    {
        _history = new History(historyCapacity);
        Document = document ?? new Document();
        NextId = MaxLayerId(Document) + 1;
    }

    public Document Document { get; private set; }

    public int? SelectedLayerId { get; private set; }

    /// <summary>The selected layer, or null (getSelected, editor.js:582).</summary>
    public Layer? SelectedLayer => SelectedLayerId is int id ? Document.Layers.Find(l => l.Id == id) : null;

    /// <summary>
    /// The next layer id. Starts at 1 (editor.js:499) and only grows: undo and redo never lower it, and
    /// adopting a document whose ids reach it raises it past them (editor.js:1465).
    /// </summary>
    public int NextId { get; private set; }

    /// <summary>The open project file. Always null in M3a; M4's Save and Open set it.</summary>
    public string? CurrentFile { get; private set; }

    /// <summary>True after any recorded step, undo or redo. M4's save clears it.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// The export title box (editor.html:100, <c>#exportTitle</c>). It is not part of the document, so it
    /// is never an undo step and never makes the session dirty (ledger file-undo-snapshot-scope). Null
    /// is stored as "".
    /// </summary>
    public string TemplateTitle
    {
        get => _templateTitle;
        set
        {
            var next = value ?? "";
            if (string.Equals(next, _templateTitle, StringComparison.Ordinal)) return;
            _templateTitle = next;
            Raise(SessionChange.Title);
        }
    }

    /// <summary>True when a step is recorded, or an open gesture has changed the content (Undo commits it first).</summary>
    public bool CanUndo => _history.CanUndo || GestureChanged;

    public bool CanRedo => _history.CanRedo;

    public int UndoCount => _history.UndoCount;

    public int RedoCount => _history.RedoCount;

    public bool InGesture => _gestureDepth > 0;

    public event EventHandler<SessionChangedEventArgs>? Changed;

    /// <summary>
    /// Raised first by every change entry point (<see cref="Apply"/>, <see cref="ApplyWithSelection"/>,
    /// <see cref="BeginGesture"/>, <see cref="Undo"/>, <see cref="Redo"/>), before it changes anything. The canvas
    /// closes an open wheel burst on it, so an unrelated edit is never folded into the burst's step (M3b R7).
    /// </summary>
    public event EventHandler? Changing;

    void RaiseChanging() => Changing?.Invoke(this, EventArgs.Empty);

    EditorSnapshot Snapshot => new(Document, SelectedLayerId);

    bool GestureChanged => _gestureStart is not null && !_gestureStart.Document.ContentEquals(Document);

    /// <summary>Returns <see cref="NextId"/>, then increments it (<c>id: nextId++</c>, editor.js:513).</summary>
    public int TakeId() => NextId++;

    /// <summary>
    /// Runs <paramref name="change"/> on the document. The change runs first, so if it throws nothing
    /// changes. Unchanged content returns false and raises nothing. Otherwise the session records the
    /// state from before (unless a gesture is open), adopts the new document, drops the selection if its
    /// layer is gone, and raises Document | History (| Selection). Returns true iff the content changed.
    /// </summary>
    public bool Apply(Func<Document, Document> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        RaiseChanging();
        var before = Snapshot;
        var after = change(before.Document) ?? throw new InvalidOperationException("A document change returned null.");
        return Adopt(before, new EditorSnapshot(after, before.SelectedLayerId));
    }

    /// <summary>
    /// <see cref="Apply"/> for operations that also choose the selection (add, duplicate, remove). The
    /// returned selection is adopted even when the content is unchanged (then only Selection is raised
    /// and no step is recorded); an id the returned document lacks becomes null. Returns true iff the
    /// content changed.
    /// </summary>
    public bool ApplyWithSelection(Func<EditorSnapshot, EditorSnapshot> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        RaiseChanging();
        var before = Snapshot;
        var after = change(before) ?? throw new InvalidOperationException("A document change returned null.");
        if (after.Document is null) throw new InvalidOperationException("A document change returned a null document.");
        return Adopt(before, after);
    }

    /// <summary>
    /// Selects a layer (selectLayer, editor.js:575). An id the document lacks selects nothing. Raises
    /// Selection when the selection changes; never an undo step.
    /// </summary>
    public void Select(int? id)
    {
        int? next = Resolve(Document, id);
        if (next == SelectedLayerId) return;
        SelectedLayerId = next;
        Raise(SessionChange.Selection);
    }

    /// <summary>
    /// Opens a gesture. Until the outermost scope is disposed, <see cref="Apply"/> and
    /// <see cref="ApplyWithSelection"/> adopt each change without recording. Disposing the outermost
    /// scope records the state from its opening once, iff the content changed (ledger
    /// fillstyle-undo-snapshot-timing: one slider drag is one step). Scopes nest by count, and disposing
    /// a scope twice does nothing.
    /// </summary>
    public IDisposable BeginGesture()
    {
        RaiseChanging();
        if (_gestureDepth++ == 0) _gestureStart = Snapshot;
        return new GestureScope(this);
    }

    /// <summary>
    /// Undo (btnUndo, editor.js:1477-1481). An open gesture is committed first, then undone, and it stays
    /// open from the restored state, so a drag that goes on after Ctrl+Z is one new step when it ends.
    /// False when there is nothing to undo.
    /// </summary>
    public bool Undo() => Step(redo: false);

    /// <summary>Redo (btnRedo, editor.js:1482-1486), the mirror of <see cref="Undo"/>.</summary>
    public bool Redo() => Step(redo: true);

    bool Step(bool redo)
    {
        RaiseChanging();
        var what = RecordGesture();
        var current = Snapshot;
        var target = redo ? _history.Redo(current) : _history.Undo(current);
        if (target is not null)
        {
            Document = target.Document;
            SelectedLayerId = target.SelectedLayerId;
            BumpNextId(Document);
            IsDirty = true;
            what |= SessionChange.Document | SessionChange.History;
            if (current.SelectedLayerId != SelectedLayerId) what |= SessionChange.Selection;
        }
        if (InGesture) _gestureStart = Snapshot;  // the open gesture goes on from here; its scopes stay valid
        if (what != SessionChange.None) Raise(what);
        return target is not null;
    }

    bool Adopt(EditorSnapshot before, EditorSnapshot after)
    {
        var what = SessionChange.None;
        bool changed = !before.Document.ContentEquals(after.Document);
        if (changed)
        {
            if (!InGesture)
            {
                _history.Record(before);
                IsDirty = true;
                what |= SessionChange.History;
            }
            Document = after.Document;
            BumpNextId(Document);
            what |= SessionChange.Document;
        }
        int? selection = Resolve(Document, after.SelectedLayerId);
        if (selection != SelectedLayerId)
        {
            SelectedLayerId = selection;
            what |= SessionChange.Selection;
        }
        if (what != SessionChange.None) Raise(what);
        return changed;
    }

    /// <summary>Records an open gesture's opening state iff the content changed since it opened.</summary>
    SessionChange RecordGesture()
    {
        if (_gestureStart is not { } start || start.Document.ContentEquals(Document)) return SessionChange.None;
        _history.Record(start);
        IsDirty = true;
        return SessionChange.History;
    }

    void EndGesture()
    {
        if (_gestureDepth == 0 || --_gestureDepth > 0) return;
        var what = RecordGesture();
        _gestureStart = null;
        if (what != SessionChange.None) Raise(what);
    }

    void BumpNextId(Document doc) => NextId = Math.Max(NextId, MaxLayerId(doc) + 1);

    static int MaxLayerId(Document doc) => doc.Layers.IsEmpty ? 0 : doc.Layers.Max(l => l.Id);

    static int? Resolve(Document doc, int? id) => id is int v && doc.Layers.Exists(l => l.Id == v) ? v : null;

    void Raise(SessionChange what) => Changed?.Invoke(this, new SessionChangedEventArgs(what));

    sealed class GestureScope(EditorSession session) : IDisposable
    {
        bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            session.EndGesture();
        }
    }
}
