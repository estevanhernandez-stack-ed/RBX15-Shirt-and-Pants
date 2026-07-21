// Regression tests for feedback.md:10 — the v4.0 editor had no redo at all,
// and undo captured only layers (region fills, background, and mode switches
// were unrecoverable; switching Shirt<->Pants wiped region colors for good).
// The fix routes ALL of that through this stack manager, so its invariants
// are the guard. These tests use plain strings as stand-in snapshots.
const { test } = require('node:test');
const assert = require('node:assert');
const { createHistory } = require('../lib/history.js');

test('undo returns the pushed snapshot', () => {
  const h = createHistory();
  h.push('A');
  assert.strictEqual(h.undo('B'), 'A');
});

test('undo on an empty stack returns null', () => {
  const h = createHistory();
  assert.strictEqual(h.undo('current'), null);
});

test('redo brings back the state undo parked', () => {
  const h = createHistory();
  h.push('A');            // prior state A recorded before editing to B
  const restored = h.undo('B');   // revert to A, park B on redo
  assert.strictEqual(restored, 'A');
  assert.strictEqual(h.redo('A'), 'B');  // reapply -> back to B
});

test('a new push clears the redo stack (no redo across a branch)', () => {
  const h = createHistory();
  h.push('A');
  h.undo('B');            // redo now holds B
  assert.strictEqual(h.canRedo(), true);
  h.push('A2');           // new edit -> redo history is abandoned
  assert.strictEqual(h.canRedo(), false);
  assert.strictEqual(h.redo('X'), null);
});

test('multi-step undo/redo preserves order', () => {
  const h = createHistory();
  h.push('s0'); h.push('s1'); h.push('s2');   // edited s0->s1->s2->s3(live)
  assert.strictEqual(h.undo('s3'), 's2');
  assert.strictEqual(h.undo('s2'), 's1');
  assert.strictEqual(h.redo('s1'), 's2');
  assert.strictEqual(h.redo('s2'), 's3');
  assert.strictEqual(h.canRedo(), false);
});

test('respects the cap, evicting the oldest', () => {
  const h = createHistory(3);
  ['a', 'b', 'c', 'd', 'e'].forEach(s => h.push(s));  // only c,d,e survive
  assert.deepStrictEqual(h.sizes(), { undo: 3, redo: 0 });
  assert.strictEqual(h.undo('live'), 'e');
  assert.strictEqual(h.undo('e'), 'd');
  assert.strictEqual(h.undo('d'), 'c');
  assert.strictEqual(h.undo('c'), null); // 'a' and 'b' were evicted
});

test('canUndo / canRedo reflect stack state', () => {
  const h = createHistory();
  assert.strictEqual(h.canUndo(), false);
  assert.strictEqual(h.canRedo(), false);
  h.push('A');
  assert.strictEqual(h.canUndo(), true);
  h.undo('B');
  assert.strictEqual(h.canUndo(), false);
  assert.strictEqual(h.canRedo(), true);
});
