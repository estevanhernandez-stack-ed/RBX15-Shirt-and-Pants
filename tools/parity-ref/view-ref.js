// Electron main. Renders the 4.1 VIEW canvas (the visible #mainCanvas after render(), editor.js:300-429)
// for each view scene, so the engine's Renderer.RenderView is held to what 4.1 put on screen: the
// zoomed composite, the 16px checkerboard, the mask toggle and the grid overlay. M3a Task 2 froze its
// output once in tests/goldens/view; never re-run it into that folder.
//
// Each scene is an app scene (tools/parity-ref/README.md, "Scene format") plus a "view" object:
//   { zoom: 0.25..3 in 0.25 steps, mask: boolean (default true), grid: boolean (default false) }.
// The base and its actions run through the frozen app-harness.js (__pref.render, whose export
// capture is discarded), then the real controls are driven: #zoomIn/#zoomOut (editor.js:1490-1491),
// #showMask and #showGrid (editor.js:1494-1495). The selection is cleared before the final render(),
// because the selection overlay is M3b's. GPU raster is off, Google Fonts requests are answered from
// assets/fonts (fonts.js), and any other network request fails the run.
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-ref.js \
//     --scenes tools/parity-ref/view-scenes.json --out <dir> [--only name1,name2]
'use strict';
const { app, BrowserWindow, protocol } = require('electron');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const fonts = require('./fonts.js');

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
app.commandLine.appendSwitch('force-device-scale-factor', '1');

const ROOT = path.resolve(__dirname, '..', '..');
const HARNESS = fs.readFileSync(path.join(__dirname, 'app-harness.js'), 'utf8');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const blob = (rel) => execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();
const ZOOMS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5, 2.75, 3]; // zoom-controls: 0.25..3 in 0.25 steps

const violations = [];

function dataUrl(rel) {
  const file = path.resolve(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg' }[path.extname(file).toLowerCase()];
  if (!mime) throw new Error(`unsupported scene image type: ${rel}`);
  return `data:${mime};base64,` + fs.readFileSync(file).toString('base64');
}

// The same asset shape app-render.js hands __pref.render.
function assetsFor(scene) {
  const images = {};
  const projects = {};
  for (const rel of [scene.bgImage, ...(scene.layers || []).map((l) => l.image)].filter(Boolean)) images[rel] = dataUrl(rel);
  for (const op of scene.actions || []) {
    if (op.op !== 'project') continue;
    const file = path.resolve(ROOT, op.file);
    if (!fs.existsSync(file)) throw new Error(`project file not found: ${op.file}`);
    projects[op.file] = { name: path.basename(file), text: fs.readFileSync(file, 'utf8') };
  }
  return { images, projects };
}

// Validates a view scene. The engine's golden gate compares every view under the unchanged rule,
// so a layer with an RGB shift (approved divergence rgb-shift-compute, held to a different rule)
// is refused here rather than frozen.
function validate(scene) {
  if (!/^[a-z0-9][a-z0-9-]*$/.test(scene.name || '')) throw new Error(`bad scene name: ${scene.name}`);
  const v = scene.view;
  if (!v || typeof v !== 'object') throw new Error(`${scene.name}: a view scene needs a "view" object`);
  if (!ZOOMS.includes(v.zoom)) throw new Error(`${scene.name}: zoom ${v.zoom} is not a 4.1 zoom step`);
  for (const k of ['mask', 'grid']) if (v[k] !== undefined && typeof v[k] !== 'boolean') throw new Error(`${scene.name}: view.${k} must be a boolean`);
  for (const l of scene.layers || []) {
    if (l.rShift || l.gShift || l.bShift) throw new Error(`${scene.name}: RGB-shifted layers are not view scenes (rgb-shift-compute)`);
  }
  return { zoom: v.zoom, mask: v.mask !== false, grid: v.grid === true };
}

function loadScenes(file) {
  let scenes = JSON.parse(fs.readFileSync(path.resolve(file), 'utf8'));
  const only = arg('--only');
  if (only) {
    const want = only.split(',');
    for (const n of want) if (!scenes.some((s) => s.name === n)) throw new Error(`--only names an unknown scene: ${n}`);
    scenes = scenes.filter((s) => want.includes(s.name));
  }
  const names = scenes.map((s) => s.name);
  if (new Set(names).size !== names.length) throw new Error('duplicate scene names');
  return scenes.map((s) => ({ scene: s, view: validate(s) }));
}

// Runs in the page after __pref.render. Drives the real zoom buttons and checkboxes, clears the
// selection, renders, and returns the visible canvas.
function viewPage() {
  window.__view = async function (v) {
    const byId = (id) => {
      const el = document.getElementById(id);
      if (!el) throw new Error(`#${id} not found in editor.html`);
      return el;
    };
    const steps = Math.round((v.zoom - zoom) / 0.25);
    for (let i = 0; i < Math.abs(steps); i++) byId(steps > 0 ? 'zoomIn' : 'zoomOut').click(); // editor.js:1490-1491
    if (zoom !== v.zoom) throw new Error(`zoom is ${zoom}, wanted ${v.zoom}`);
    if (byId('zoomLevel').textContent !== (v.zoom * 100) + '%') throw new Error(`zoom label ${byId('zoomLevel').textContent}`);
    for (const [id, on] of [['showMask', v.mask], ['showGrid', v.grid]]) { // editor.js:1494-1495
      const el = byId(id);
      el.checked = on;
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }
    if (showMask !== v.mask || showGrid !== v.grid) throw new Error('mask or grid did not take');
    selectedLayerId = null; // the selection overlay (editor.js:362-421) is M3b's
    render();
    if (drawCanvas) throw new Error('a live drawing preview is still open');
    const c = byId('mainCanvas');
    return { width: c.width, height: c.height, dpr: window.devicePixelRatio, url: c.toDataURL('image/png') };
  };
}

async function callPage(win, expr) {
  const r = await win.webContents.executeJavaScript(
    `(${expr}).then((value) => ({ ok: true, value }), (e) => ({ ok: false, error: String((e && e.message) || e) }))`);
  if (!r.ok) throw new Error(r.error);
  return r.value;
}

app.whenReady().then(async () => {
  try {
    for (const scheme of ['https', 'http']) {
      protocol.handle(scheme, (request) => {
        try {
          const r = fonts.respond(request.url);
          if (r) return new Response(r.body, { status: 200, headers: r.headers });
        } catch (e) {
          violations.push(e.message);
          return new Response(e.message, { status: 500 });
        }
        violations.push(`blocked network request ${request.url}`);
        return new Response('blocked by tools/parity-ref', { status: 404 });
      });
    }
    const scenesFile = arg('--scenes');
    const out = arg('--out');
    if (!scenesFile || !out) throw new Error('usage: --scenes <file> --out <dir> [--only a,b]');
    const scenes = loadScenes(scenesFile);
    const outDir = path.resolve(out);
    fs.mkdirSync(outDir, { recursive: true });

    const win = new BrowserWindow({
      show: false, width: 1200, height: 800,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false },
    });
    const run = {
      tool: 'tools/parity-ref/view-ref.js',
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), fillsJsBlob: blob('lib/fills.js'),
      appHarnessBlob: blob('tools/parity-ref/app-harness.js'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
    };
    const index = { ...run, scenes: [] };
    for (const { scene, view } of scenes) {
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(HARNESS);
      await win.webContents.executeJavaScript(`(${viewPage.toString()})(); 0;`);
      await callPage(win, `__pref.render(${JSON.stringify(scene)}, ${JSON.stringify(assetsFor(scene))})`);
      const r = await callPage(win, `__view(${JSON.stringify(view)})`);
      if (violations.length) throw new Error(`${scene.name}: ${violations.join('; ')}`);
      if (r.dpr !== 1) throw new Error(`${scene.name}: devicePixelRatio ${r.dpr}, expected 1`);
      const want = [Math.trunc(585 * view.zoom), Math.trunc(559 * view.zoom)]; // canvas.width = TW * zoom, editor.js:340-341
      if (r.width !== want[0] || r.height !== want[1]) throw new Error(`${scene.name}: canvas ${r.width}x${r.height}, expected ${want.join('x')}`);
      const bytes = Buffer.from(r.url.split(',')[1], 'base64');
      fs.writeFileSync(path.join(outDir, scene.name + '.png'), bytes);
      index.scenes.push({ name: scene.name, sha256: sha256(bytes), width: r.width, height: r.height, ...view });
      console.log(`rendered ${scene.name} ${r.width}x${r.height}`);
    }
    fs.writeFileSync(path.join(outDir, 'index.json'), JSON.stringify(index, null, 2) + '\n');
    console.log(`ok ${index.scenes.length}`);
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
