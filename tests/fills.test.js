// Tests for the region-fill renderer (pattern & gradient fills feature).
// Pixel-probe real 2D contexts; the key invariants are back-compat with the
// old string form, correct color placement per fill type, and camo being
// deterministic (so screen render and PNG export never drift).
const { test } = require('node:test');
const assert = require('node:assert');
const { createCanvas } = require('@napi-rs/canvas');
const { normalizeFill, paintRegionFill, fillToCSS, fillKey } = require('../lib/fills.js');

const REGION = { x: 0, y: 0, w: 40, h: 40 };

function paint(fill, region) {
  const c = createCanvas(40, 40);
  paintRegionFill(c.getContext('2d'), region || REGION, fill);
  return c.getContext('2d');
}
function rgbAt(ctx, x, y) {
  const d = ctx.getImageData(x, y, 1, 1).data;
  return [d[0], d[1], d[2]];
}

test('normalizeFill folds a bare color string into a solid descriptor', () => {
  assert.deepStrictEqual(normalizeFill('#ff0000'), { type: 'solid', c1: '#ff0000' });
  assert.strictEqual(normalizeFill({ type: 'linear', c1: '#000', c2: '#fff' }).type, 'linear');
  assert.strictEqual(normalizeFill(null).type, 'solid');
});

test('legacy string fill paints solid (back-compat with v4.0 projects)', () => {
  const ctx = paint('#ff0000');
  assert.deepStrictEqual(rgbAt(ctx, 20, 20), [255, 0, 0]);
});

test('solid descriptor fills the region with c1', () => {
  const ctx = paint({ type: 'solid', c1: '#00ff00' });
  assert.deepStrictEqual(rgbAt(ctx, 5, 5), [0, 255, 0]);
  assert.deepStrictEqual(rgbAt(ctx, 35, 35), [0, 255, 0]);
});

test('linear gradient runs c1 -> c2 across the region', () => {
  const ctx = paint({ type: 'linear', c1: '#ff0000', c2: '#0000ff', angle: 0 });
  const left = rgbAt(ctx, 1, 20);
  const right = rgbAt(ctx, 39, 20);
  assert.ok(left[0] > 200 && left[2] < 60, 'left edge is red-ish: ' + left);
  assert.ok(right[2] > 200 && right[0] < 60, 'right edge is blue-ish: ' + right);
});

test('stripes alternate c1 and c2 on the global lattice', () => {
  // scale 8, angle 0 -> c2 bands at global [0,8),[16,24)...; c1 base in gaps.
  const ctx = paint({ type: 'stripes', c1: '#ff0000', c2: '#0000ff', angle: 0, scale: 8 });
  assert.deepStrictEqual(rgbAt(ctx, 2, 20), [0, 0, 255], 'c2 band at global 0-8');
  assert.deepStrictEqual(rgbAt(ctx, 11, 20), [255, 0, 0], 'c1 gap at global 8-16');
});

test('patterns are continuous across adjacent regions (global lattice)', () => {
  // Two side-by-side regions, same dots fill. Dots sit on ONE global lattice
  // (centers x = 8,24,40,56,72) — region B does not restart the pattern at
  // its own left edge. The old per-region code would have dotted x=48.
  const c = createCanvas(80, 40);
  const ctx = c.getContext('2d');
  const fill = { type: 'dots', c1: '#ff0000', c2: '#0000ff', scale: 16 };
  paintRegionFill(ctx, { x: 0, y: 0, w: 40, h: 40 }, fill);
  paintRegionFill(ctx, { x: 40, y: 0, w: 40, h: 40 }, fill);
  assert.deepStrictEqual(rgbAt(ctx, 56, 8), [0, 0, 255], 'dot on the shared global lattice in region B');
  assert.deepStrictEqual(rgbAt(ctx, 48, 8), [255, 0, 0], 'gap where a per-region restart would have dotted');
});

test('checker alternates on the diagonal', () => {
  const ctx = paint({ type: 'checker', c1: '#ff0000', c2: '#0000ff', scale: 10 });
  const cellA = rgbAt(ctx, 5, 5);    // rx0,ry0 -> c2 painted over c1
  const cellB = rgbAt(ctx, 15, 5);   // rx1,ry0 -> c1
  assert.notDeepStrictEqual(cellA, cellB, 'adjacent checker cells differ');
});

test('dots put c2 at cell centers and c1 in the gaps', () => {
  const ctx = paint({ type: 'dots', c1: '#ff0000', c2: '#0000ff', scale: 16 });
  assert.deepStrictEqual(rgbAt(ctx, 8, 8), [0, 0, 255], 'dot center is c2');
  assert.deepStrictEqual(rgbAt(ctx, 0, 0), [255, 0, 0], 'corner gap is c1');
});

test('camo is deterministic — same params render identically', () => {
  const a = paint({ type: 'camo', c1: '#33421a', c2: '#7a8a4a', scale: 14 });
  const b = paint({ type: 'camo', c1: '#33421a', c2: '#7a8a4a', scale: 14 });
  for (const [x, y] of [[5, 5], [20, 20], [33, 12], [10, 30]]) {
    assert.deepStrictEqual(rgbAt(a, x, y), rgbAt(b, x, y), 'camo pixel drift at ' + x + ',' + y);
  }
});

test('fill is clipped to the region rect', () => {
  const c = createCanvas(40, 40);
  paintRegionFill(c.getContext('2d'), { x: 10, y: 10, w: 10, h: 10 }, { type: 'solid', c1: '#00ff00' });
  const ctx = c.getContext('2d');
  assert.deepStrictEqual(rgbAt(ctx, 15, 15), [0, 255, 0], 'inside region painted');
  assert.deepStrictEqual(ctx.getImageData(2, 2, 1, 1).data[3], 0, 'outside region untouched (transparent)');
});

test('a linear gradient spans a shared extent across regions', () => {
  // Regions A [0,40) and B [40,80) painted with one gradient (angle 0) over
  // the full [0,80) extent. The ramp is continuous across the A/B seam — B
  // does NOT restart at c1 on its left edge.
  const c = createCanvas(80, 20);
  const ctx = c.getContext('2d');
  const fill = { type: 'linear', c1: '#ff0000', c2: '#0000ff', angle: 0 };
  const extent = { x: 0, y: 0, w: 80, h: 20 };
  paintRegionFill(ctx, { x: 0, y: 0, w: 40, h: 20 }, fill, extent);
  paintRegionFill(ctx, { x: 40, y: 0, w: 40, h: 20 }, fill, extent);
  const left = rgbAt(ctx, 2, 10), right = rgbAt(ctx, 78, 10);
  const seamA = rgbAt(ctx, 38, 10), seamB = rgbAt(ctx, 42, 10);
  assert.ok(left[0] > 230 && left[2] < 30, 'left edge ~c1: ' + left);
  assert.ok(right[2] > 230 && right[0] < 30, 'right edge ~c2: ' + right);
  assert.ok(Math.abs(seamA[0] - seamB[0]) < 25 && Math.abs(seamA[2] - seamB[2]) < 25,
    'gradient continuous across the seam (' + seamA + ' vs ' + seamB + ')');
});

test('without a shared extent each region is its own gradient (default)', () => {
  // Same two regions, no extent passed -> region B restarts at c1 on its left,
  // so the seam jumps. Proves the spanning is doing the work.
  const c = createCanvas(80, 20);
  const ctx = c.getContext('2d');
  const fill = { type: 'linear', c1: '#ff0000', c2: '#0000ff', angle: 0 };
  paintRegionFill(ctx, { x: 0, y: 0, w: 40, h: 20 }, fill);
  paintRegionFill(ctx, { x: 40, y: 0, w: 40, h: 20 }, fill);
  const seamA = rgbAt(ctx, 38, 10), seamB = rgbAt(ctx, 42, 10);
  assert.ok(Math.abs(seamA[2] - seamB[2]) > 150, 'per-region gradients jump at the seam');
});

test('fillKey groups equal fills and separates different ones', () => {
  const a = { type: 'linear', c1: '#000000', c2: '#ffffff', angle: 45 };
  const b = { type: 'linear', c1: '#000000', c2: '#ffffff', angle: 45 };
  const d = { type: 'linear', c1: '#000000', c2: '#ffffff', angle: 90 };
  assert.strictEqual(fillKey(a), fillKey(b));
  assert.notStrictEqual(fillKey(a), fillKey(d));
  assert.strictEqual(fillKey('#aabbcc'), fillKey({ type: 'solid', c1: '#aabbcc' }));
});

test('fillToCSS produces a plain color for solid and a gradient string otherwise', () => {
  assert.strictEqual(fillToCSS('#abcdef'), '#abcdef');
  assert.match(fillToCSS({ type: 'linear', c1: '#000', c2: '#fff', angle: 45 }), /linear-gradient\(45deg/);
  assert.match(fillToCSS({ type: 'dots', c1: '#000', c2: '#fff', scale: 12 }), /radial-gradient/);
});
