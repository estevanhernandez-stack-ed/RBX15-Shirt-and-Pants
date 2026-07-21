// Regression tests for the eraser (feedback.md:9 — the tool was a no-op in
// v4.0: destination-out strokes landed on an empty offscreen canvas and the
// mouseup content check discarded them). These exercise the extracted erase
// math directly with real 2D contexts.
const { test } = require('node:test');
const assert = require('node:assert');
const { createCanvas } = require('@napi-rs/canvas');
const { applyEraseStroke } = require('../lib/erase.js');

const TW = 585, TH = 559;

function solidLayer(w, h, color) {
  const c = createCanvas(w, h);
  const ctx = c.getContext('2d');
  ctx.fillStyle = color;
  ctx.fillRect(0, 0, w, h);
  return c;
}

function strokeRect(x, y, w, h) {
  const c = createCanvas(TW, TH);
  const ctx = c.getContext('2d');
  ctx.fillStyle = '#000';
  ctx.fillRect(x, y, w, h);
  return c;
}

function alphaAt(canvas, x, y) {
  return canvas.getContext('2d').getImageData(x, y, 1, 1).data[3];
}

const IDENTITY = { x: 0, y: 0, scale: 1, rotation: 0, flipH: false, flipV: false };

test('erases stroked pixels at identity transform, leaves the rest', () => {
  const layer = solidLayer(20, 20, '#f00');
  const out = applyEraseStroke(layer, IDENTITY, strokeRect(5, 5, 6, 6), createCanvas);
  assert.strictEqual(alphaAt(out, 7, 7), 0, 'inside the stroke should be erased');
  assert.strictEqual(alphaAt(out, 15, 15), 255, 'outside the stroke should survive');
});

test('maps template-space stroke through offset + scale', () => {
  // 10x10 layer at (20,20) scaled 2x -> occupies template 20..40.
  const layer = solidLayer(10, 10, '#f00');
  const t = { ...IDENTITY, x: 20, y: 20, scale: 2 };
  // Stroke covers template (20,20)-(30,30) = the layer's left/top quadrant.
  const out = applyEraseStroke(layer, t, strokeRect(20, 20, 10, 10), createCanvas);
  assert.strictEqual(alphaAt(out, 2, 2), 0, 'covered quadrant erased');
  assert.strictEqual(alphaAt(out, 8, 8), 255, 'uncovered quadrant survives');
});

test('respects rotation: 180deg maps a top-left stroke to bottom-right pixels', () => {
  const layer = solidLayer(10, 10, '#f00');
  const t = { ...IDENTITY, x: 20, y: 20, scale: 2, rotation: 180 };
  const out = applyEraseStroke(layer, t, strokeRect(20, 20, 10, 10), createCanvas);
  assert.strictEqual(alphaAt(out, 8, 8), 0, 'rotated: bottom-right erased');
  assert.strictEqual(alphaAt(out, 2, 2), 255, 'rotated: top-left survives');
});

test('respects flipH: a left-side stroke erases right-side pixels', () => {
  const layer = solidLayer(10, 10, '#f00');
  const t = { ...IDENTITY, x: 20, y: 20, scale: 2, flipH: true };
  const out = applyEraseStroke(layer, t, strokeRect(20, 20, 10, 20), createCanvas);
  assert.strictEqual(alphaAt(out, 8, 5), 0, 'flipped: right half erased');
  assert.strictEqual(alphaAt(out, 2, 5), 255, 'flipped: left half survives');
});

test('does not mutate the source layer image', () => {
  const layer = solidLayer(20, 20, '#f00');
  applyEraseStroke(layer, IDENTITY, strokeRect(0, 0, 20, 20), createCanvas);
  assert.strictEqual(alphaAt(layer, 10, 10), 255, 'source stays intact');
});
