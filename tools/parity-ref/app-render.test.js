// node --test tools/parity-ref/app-render.test.js
// Guards the app-driven reference renderer: every font 4.1 asks Google for resolves to a vendored
// file, the real app agrees with the M0 goldens, the ops drive the real handlers, and a project
// round-trips through 4.1's own saveProject and loadProject.
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { loadImage, createCanvas } = require('@napi-rs/canvas');
const fonts = require('./fonts.js');

const ROOT = path.resolve(__dirname, '..', '..');
const ELECTRON = require('electron'); // in plain Node this is the path to electron.exe

function appRender(args) {
  const env = { ...process.env };
  delete env.ELECTRON_RUN_AS_NODE;
  return spawnSync(ELECTRON, [path.join(__dirname, 'app-render.js'), ...args], { env, encoding: 'utf8', timeout: 300000 });
}

function renderOk(scenes, dir, extra = []) {
  const file = path.join(dir, 'in-scenes.json');
  fs.writeFileSync(file, JSON.stringify(scenes));
  const r = appRender(['--scenes', file, '--out', dir, ...extra]);
  assert.strictEqual(r.status, 0, `app-render failed:\n${r.stdout}\n${r.stderr}`);
  return r.stdout;
}

async function pixels(file) {
  const img = await loadImage(fs.readFileSync(file));
  const c = createCanvas(img.width, img.height);
  const ctx = c.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return Buffer.from(ctx.getImageData(0, 0, img.width, img.height).data);
}

async function pixel(file, x, y) {
  const img = await loadImage(fs.readFileSync(file));
  const c = createCanvas(img.width, img.height);
  const ctx = c.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return Array.from(ctx.getImageData(x, y, 1, 1).data);
}

const tmp = (prefix) => fs.mkdtempSync(path.join(os.tmpdir(), prefix));
const label = (faces) => faces.map((f) => `${f.family} ${f.weight}`);

test('fonts: every face editor.html and styles.css request maps to a vendored file', () => {
  const html = fs.readFileSync(path.join(ROOT, 'editor.html'), 'utf8');
  const css = fs.readFileSync(path.join(ROOT, 'styles.css'), 'utf8');
  const wordArtUrl = /href="(https:\/\/fonts\.googleapis\.com\/css2\?[^"]+)"/.exec(html)[1].replace(/&amp;/g, '&');
  const uiUrl = /@import url\('(https:\/\/fonts\.googleapis\.com\/css2\?[^']+)'\)/.exec(css)[1];
  assert.deepStrictEqual(label(fonts.facesFor(wordArtUrl)), [
    'Anton 400', 'Bebas Neue 400', 'Dancing Script 700', 'Great Vibes 400', 'Pacifico 400', 'Creepster 400',
    'Poppins 700', 'Poppins 900', 'Lato 900', 'Bangers 400', 'Permanent Marker 400', 'Bungee Shade 400',
    'Rubik Mono One 400', 'Press Start 2P 400', 'Righteous 400', 'Orbitron 700', 'Orbitron 900',
  ]);
  assert.deepStrictEqual(label(fonts.facesFor(uiUrl)), [
    'Space Grotesk 400', 'Space Grotesk 500', 'Space Grotesk 600', 'Space Grotesk 700',
    'JetBrains Mono 400', 'JetBrains Mono 500', 'JetBrains Mono 600',
    'Inter 400', 'Inter 500', 'Inter 600', 'Inter 700',
  ]);
  for (const f of [...fonts.facesFor(wordArtUrl), ...fonts.facesFor(uiUrl)]) {
    assert.ok(fs.existsSync(path.join(fonts.FONTS_DIR, ...f.file.split('/'))), f.file);
  }
  const select = /<select id="waFont"[\s\S]*?<\/select>/.exec(html)[0];
  assert.deepStrictEqual(fonts.WORDART_FAMILIES, [...select.matchAll(/value="([^"]+)"/g)].map((m) => m[1]));
  assert.strictEqual(fonts.wordArtFaces().length, 17);
});

test('fonts: a face with no vendored file is refused, never served from the network', () => {
  assert.throws(() => fonts.facesFor('https://fonts.googleapis.com/css2?family=Comic+Sans'), /no vendored face for Comic Sans 400/);
  assert.throws(() => fonts.facesFor('https://fonts.googleapis.com/css2?family=Anton:wght@700'), /no vendored face for Anton 700/);
  assert.throws(() => fonts.facesFor('https://fonts.googleapis.com/css2?family=Anton:ital,wght@1,400'), /unsupported axis spec/);
  assert.throws(() => fonts.fileFor('https://fonts.gstatic.com/rbx15-vendored/anton/evil.ttf'), /not a vendored font/);
  assert.strictEqual(fonts.respond('https://example.com/x'), null);
});

test('fonts: cssFor emits one @font-face per requested face, with percent-encoded file urls', () => {
  const css = fonts.cssFor('https://fonts.googleapis.com/css2?family=Dancing+Script:wght@700&display=swap');
  assert.match(css, /font-family: 'Dancing Script';/);
  assert.match(css, /font-weight: 700;/);
  assert.match(css, /src: url\(https:\/\/fonts\.gstatic\.com\/rbx15-vendored\/dancingscript\/DancingScript%5Bwght%5D\.ttf\) format\('truetype'\);/);
  assert.strictEqual((css.match(/@font-face/g) || []).length, 1);
  const url = fonts.urlFor('dancingscript/DancingScript[wght].ttf');
  assert.strictEqual(fonts.fileFor(url), path.join(fonts.FONTS_DIR, 'dancingscript', 'DancingScript[wght].ttf'));
});

test('fonts: respond serves css and font bytes with CORS for the file:// page', () => {
  const css = fonts.respond('https://fonts.googleapis.com/css2?family=Anton&display=swap');
  assert.strictEqual(css.headers['access-control-allow-origin'], '*');
  assert.match(css.headers['content-type'], /^text\/css/);
  const ttf = fonts.respond(fonts.urlFor('anton/Anton-Regular.ttf'));
  assert.strictEqual(ttf.headers['content-type'], 'font/ttf');
  assert.ok(ttf.body.equals(fs.readFileSync(path.join(fonts.FONTS_DIR, 'anton', 'Anton-Regular.ttf'))));
});

test('the real app agrees with the frozen M0 goldens on base scenes', async () => {
  const all = JSON.parse(fs.readFileSync(path.join(ROOT, 'tests', 'goldens', 'render', 'scenes.json'), 'utf8'));
  const names = ['fill-linear-0-torso', 'layer-rotate-scale-fliph-opacity', 'rgb-shift', 'full-scene'];
  const dir = tmp('pref-app-m0-');
  const out = renderOk(all.filter((s) => names.includes(s.name)), dir);
  assert.match(out, /fonts ok: 17 word art faces/);
  for (const n of names) {
    const same = (await pixels(path.join(dir, n + '.png'))).equals(await pixels(path.join(ROOT, 'tests', 'goldens', 'render', n + '.png')));
    assert.ok(same, `${n}: app-driven export differs from the M0 golden`);
  }
});

test('shape and erase ops drive the real canvas handlers, deterministically', async () => {
  const scene = {
    name: 'probe-ops', bgColor: '#ff00ff',
    layers: [{ image: 'tests/goldens/render/bg-default.png', x: 0, y: 0 }],
    actions: [
      { op: 'erase', layer: 0, points: [[260, 138], [330, 138]], size: 20 },
      { op: 'shape', tool: 'line', from: [240, 100], to: [350, 100], color: '#ff0000', width: 9, fill: false },
    ],
  };
  const a = tmp('pref-ops-a-');
  const b = tmp('pref-ops-b-');
  renderOk([scene], a);
  renderOk([scene], b);
  const file = path.join(a, 'probe-ops.png');
  assert.deepStrictEqual(fs.readFileSync(file), fs.readFileSync(path.join(b, 'probe-ops.png')));
  // The 9px line covers rows 95.5-104.5, so row 100 is fully covered: opaque drawColor
  // (editor.js:1207-1216), committed at scale 1 on integer coordinates by autocrop (editor.js:533).
  assert.deepStrictEqual(await pixel(file, 295, 100), [255, 0, 0, 255]);
  // The 20px erase stroke covers rows 128-148 at identity (layer at 0,0, scale 1, lib/erase.js:43-73),
  // so the layer is gone there and the #ff00ff background shows.
  assert.deepStrictEqual(await pixel(file, 295, 138), [255, 0, 255, 255]);
  // Untouched layer pixels keep bg-default.png's navy, #192e44 (editor.js:78).
  assert.deepStrictEqual(await pixel(file, 295, 180), [25, 46, 68, 255]);
});

test('detail and word art ops build layers through the real buttons, deterministically', () => {
  const scene = {
    name: 'probe-detail-wordart',
    actions: [
      { op: 'detail', type: 'collar-v', color: '#ffffff', width: 4, stitch: true },
      { op: 'wordart', text: 'RIZZ', font: 'Dancing Script', style: 'neon', size: 64, fill: '#17d4fa', stroke: '#000000', grad: '#f22f89', strokeWidth: 3, angle: 0 },
    ],
  };
  const a = tmp('pref-dw-a-');
  const b = tmp('pref-dw-b-');
  assert.match(renderOk([scene], a), /rendered probe-detail-wordart/);
  renderOk([scene], b);
  assert.deepStrictEqual(fs.readFileSync(path.join(a, 'probe-detail-wordart.png')), fs.readFileSync(path.join(b, 'probe-detail-wordart.png')));
});

test('a project round-trips through the real saveProject and loadProject', async () => {
  const source = {
    name: 'probe-roundtrip', bgColor: '#0f1f31',
    regionColors: { torso_front: { type: 'linear', c1: '#ff0000', c2: '#0000ff', angle: 30 }, torso_back: '#ff8800' },
    layers: [{ image: 'packs/word_art/word_626_anton.png', x: 240, y: 100, scale: 0.6, rotation: 12, brightness: 130, rShift: 20 }],
  };
  const projDir = tmp('pref-proj-');
  const scenesFile = path.join(projDir, 'in-projects.json');
  fs.writeFileSync(scenesFile, JSON.stringify([source]));
  const saved = appRender(['--save-projects', scenesFile, '--out-dir', projDir]);
  assert.strictEqual(saved.status, 0, `save failed:\n${saved.stdout}\n${saved.stderr}`);
  assert.match(saved.stdout, /saved probe-roundtrip/);
  const projFile = path.join(projDir, 'probe-roundtrip.r15proj');
  const project = JSON.parse(fs.readFileSync(projFile, 'utf8'));
  assert.strictEqual(project.version, '4.0'); // editor.js:2552
  assert.strictEqual(project.mode, 'Shirt');
  assert.strictEqual(project.regionColors.torso_back, '#ff8800'); // legacy string kept verbatim, editor.js:2559
  assert.strictEqual(project.layers.length, 1);
  assert.strictEqual(project.layers[0].rShift, 20);
  assert.match(project.layers[0].imgSrc, /^data:image\/png;base64,/);

  const dir = tmp('pref-reload-');
  renderOk([source, { name: 'probe-reloaded', actions: [{ op: 'project', file: projFile }] }], dir);
  assert.ok((await pixels(path.join(dir, 'probe-roundtrip.png'))).equals(await pixels(path.join(dir, 'probe-reloaded.png'))),
    'the reloaded project must export the same pixels as the scene that saved it');
});

test('a failing action stops the run with the scene, the action index and the op', () => {
  for (const [scene, pattern] of [
    [{ name: 'probe-bad-erase', actions: [{ op: 'erase', layer: 0, points: [[1, 1], [2, 2]], size: 5 }] }, /probe-bad-erase action 0 \(erase\): scene has no layer 0/],
    [{ name: 'probe-pen', actions: [{ op: 'pen', points: [[1, 1], [2, 2]] }] }, /probe-pen action 0 \(pen\): unknown op "pen"/],
    [{ name: 'probe-clamped', actions: [{ op: 'shape', tool: 'line', from: [1, 1], to: [9, 9], color: '#ffffff', width: 60, fill: false }] }, /#drawSize refused 60/],
  ]) {
    const dir = tmp('pref-bad-');
    const file = path.join(dir, 'in-scenes.json');
    fs.writeFileSync(file, JSON.stringify([scene]));
    const r = appRender(['--scenes', file, '--out', dir]);
    assert.strictEqual(r.status, 1, `${scene.name} should fail:\n${r.stdout}\n${r.stderr}`);
    assert.match(r.stderr, pattern);
  }
});
