// node --test tools/parity-ref/
// Guards the reference renderer itself: extraction finds the shipped code,
// output is deterministic, and the export path masks the gutters.
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { loadImage, createCanvas } = require('@napi-rs/canvas');
const { extractShipped } = require('./extract.js');

const ROOT = path.resolve(__dirname, '..', '..');
const ELECTRON = require('electron'); // in plain Node this is the path to electron.exe

function render(scenes, outDir, extra = []) {
  const scenesFile = path.join(outDir, 'scenes.json');
  fs.writeFileSync(scenesFile, JSON.stringify(scenes));
  const env = { ...process.env };
  delete env.ELECTRON_RUN_AS_NODE;
  const r = spawnSync(ELECTRON, [path.join(__dirname, 'render.js'), '--scenes', scenesFile, '--out', outDir, ...extra],
    { env, encoding: 'utf8', timeout: 120000 });
  assert.strictEqual(r.status, 0, `render failed:\n${r.stdout}\n${r.stderr}`);
}

async function pixel(file, x, y) {
  const img = await loadImage(fs.readFileSync(file));
  const c = createCanvas(img.width, img.height);
  const ctx = c.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return Array.from(ctx.getImageData(x, y, 1, 1).data);
}

const SOLID = { name: 'probe-solid', regionColors: { torso_front: { type: 'solid', c1: '#ff0000' } } };
const LAYER = { name: 'probe-layer', layers: [{ image: 'packs/drips/drip_cyan_front_v1.png', x: 231, y: 74 }] };

test('extraction finds every shipped piece exactly once', () => {
  const src = fs.readFileSync(path.join(ROOT, 'editor.js'), 'utf8');
  const x = extractShipped(src);
  for (const name of ['unionRect', 'connectedRegionGroups', 'paintRegions', 'applyRGBShift',
    'hasAdjustments', 'drawLayerWithAdjustments', 'buildMask']) {
    assert.ok(x.functions[name].startsWith(`function ${name}(`), name);
    assert.doesNotThrow(() => new Function(x.functions[name]), name);
  }
  assert.match(x.constants, /const TW = 585, TH = 559;/);
  assert.match(x.constants, /const SHIRT_REGION_DATA = \{/);
  assert.match(x.constants, /const PANTS_REGION_DATA = \{/);
  assert.match(x.exportBody, /ec\.globalCompositeOperation = 'destination-in';/);
});

test('extraction refuses source where a function is missing', () => {
  assert.throws(() => extractShipped('const TW = 585, TH = 559;\n'), /not found/);
});

test('renders are deterministic and the export masks the gutters', async () => {
  const a = fs.mkdtempSync(path.join(os.tmpdir(), 'pref-a-'));
  const b = fs.mkdtempSync(path.join(os.tmpdir(), 'pref-b-'));
  render([SOLID, LAYER], a);
  render([SOLID, LAYER], b);
  for (const n of ['probe-solid', 'probe-layer']) {
    assert.deepStrictEqual(fs.readFileSync(path.join(a, n + '.png')), fs.readFileSync(path.join(b, n + '.png')), n);
  }
  // torso_front is {x:231,y:74,w:128,h:128}; its center is solid red.
  assert.deepStrictEqual(await pixel(path.join(a, 'probe-solid.png'), 295, 138), [255, 0, 0, 255]);
  // x=230 is the gutter between torso_right and torso_front: the export mask makes it transparent.
  assert.strictEqual((await pixel(path.join(a, 'probe-solid.png'), 230, 138))[3], 0);
  const index = JSON.parse(fs.readFileSync(path.join(a, 'index.json'), 'utf8'));
  assert.ok(index.electron && index.chrome && index.editorJsBlob, 'provenance recorded');
  assert.strictEqual(index.scenes.length, 2);
});

test('--only keeps every other scene in index.json, with per-scene provenance', () => {
  // index.json is the only provenance record after cutover: regenerating one golden
  // must not erase the others' hashes or claim they came from this run's Electron.
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'pref-only-'));
  render([SOLID, LAYER], dir);
  const before = JSON.parse(fs.readFileSync(path.join(dir, 'index.json'), 'utf8'));
  render([SOLID, LAYER], dir, ['--only', 'probe-solid']);
  const after = JSON.parse(fs.readFileSync(path.join(dir, 'index.json'), 'utf8'));
  assert.deepStrictEqual(after.scenes.map((s) => s.name), ['probe-solid', 'probe-layer']);
  const layer = (ix) => ix.scenes.find((s) => s.name === 'probe-layer');
  assert.deepStrictEqual(layer(after), layer(before));
  for (const s of after.scenes) assert.ok(s.electron && s.chrome && s.editorJsBlob && s.fillsJsBlob, s.name);
});
