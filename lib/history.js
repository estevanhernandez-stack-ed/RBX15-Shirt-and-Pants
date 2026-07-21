// Undo/redo stack manager for the editor.
//
// Storage-agnostic on purpose: it holds opaque snapshot values (the editor
// hands it JSON strings of full editor state) and never inspects them. That
// keeps the tricky part — stack discipline — pure and unit-testable without
// any canvas or DOM. The editor owns snapshot/restore; this owns the stacks.
//
// Model: the undo stack holds PRIOR states (snapshot-before-mutation). The
// live current state lives in the editor, not here. undo()/redo() take the
// current snapshot so they can swap it onto the opposite stack — that swap
// is what makes redo work, and getting it wrong (the classic "redo survives
// a new edit" bug) is exactly what the tests pin down.
//
// Loaded two ways: <script> tag in editor.html (attaches window.RBX15History)
// and require() in node:test (module.exports). Keep it dependency-free.
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  }
  if (root) {
    root.RBX15History = factory();
  }
}(typeof self !== 'undefined' ? self : this, function () {

  function createHistory(limit) {
    limit = limit || 30;
    const undoStack = [];
    const redoStack = [];

    return {
      // Record a new prior-state snapshot. A fresh edit invalidates any
      // redo history — you can't redo past a branch you just abandoned.
      push(snapshot) {
        undoStack.push(snapshot);
        if (undoStack.length > limit) undoStack.shift();
        redoStack.length = 0;
      },

      // Revert one step. `current` (the live snapshot) is parked on the redo
      // stack so redo() can bring it back. Returns the snapshot to restore,
      // or null if there's nothing to undo.
      undo(current) {
        if (!undoStack.length) return null;
        redoStack.push(current);
        return undoStack.pop();
      },

      // Reapply one undone step. Mirror image of undo().
      redo(current) {
        if (!redoStack.length) return null;
        undoStack.push(current);
        return redoStack.pop();
      },

      canUndo() { return undoStack.length > 0; },
      canRedo() { return redoStack.length > 0; },
      sizes() { return { undo: undoStack.length, redo: redoStack.length }; },
    };
  }

  return { createHistory };
}));
