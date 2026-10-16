// Electron main. Renders the 4.1 VIEW canvas (the visible #mainCanvas after render(), editor.js:300-429)
// with the two things view-ref.js leaves out: the selection overlay (editor.js:361-425) and the live
// drawing preview (editor.js:324-330). The engine's Renderer.RenderView with ViewOptions.SelectedLayerId
// and ViewOptions.Preview is held to it. M3b Task 1 froze its output once in tests/goldens/view-select;
// never re-run it into that folder. view-ref.js and tests/goldens/view are untouched.
//
// Each scene is an app scene (tools/parity-ref/README.md, "Scene format") without actions, plus:
//   view:    { zoom: 0.25..3 in 0.25 steps, mask: boolean (default true), grid: boolean (default false),
//              select?: 0-based base layer index }
//   preview: { op: "shape", tool: "line"|"rect"|"ellipse", from: [x,y], to: [x,y], color, width, fill }
//          | { op: "erase", layer, points: [[x,y], [x,y]], size }
// A scene has exactly one of view.select and preview. The base runs through the frozen app-harness.js
// (__pref.render, whose export capture is discarded). Then the real zoom buttons and the Mask and Grid
// checkboxes are driven, the vendored JetBrains Mono (styles.css:6) is loaded for the rotation label,
// and either selectLayer(id) runs (editor.js:575-580, which sets the Select tool) or the preview is
// started with in-page synthetic mouse events on #mainCanvas: mousedown and mousemoves, never mouseup,
// so drawCanvas stays live. These are page events in an offscreen window, as app-harness.js sends;
// nothing reaches the desktop. GPU raster is off, Google Fonts requests are answered from assets/fonts
// (fonts.js), and any other network request fails the run.
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-select-ref.js \
//     --scenes tools/parity-ref/view-select-scenes.json --out <dir> [--only name1,name2]
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
const SHAPE_TOOLS = { line: 'line', rect: 'rect', ellipse: 'circle' }; // 4.1 tool ids, editor.js:464-471

const violations = [];

function dataUrl(rel) {
  const file = path.resolve(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg' }[path.extname(file).toLowerCase()];
  if (!mime) throw new Error(`unsupported scene image type: ${rel}`);
  return `data:${mime};base64,` + fs.readFileSync(file).toString('base64');
}

function assetsFor(scene) {
  const images = {};
  for (const rel of [scene.bgImage, ...(scene.layers || []).map((l) => l.image)].filter(Boolean)) images[rel] = dataUrl(rel);
  return { images, projects: {} };
}

// A template point the page can send exactly: integers whose client coordinate x*zoom is an integer too.
function checkPoint(name, p, zoom) {
  if (!Array.isArray(p) || p.length !== 2 || !p.every(Number.isInteger)) throw new Error(`${name}: point ${JSON.stringify(p)} must be two integers`);
  if (!p.every((c) => Number.isInteger(c * zoom))) throw new Error(`${name}: point ${JSON.stringify(p)} times zoom ${zoom} is not an integer client coordinate`);
}

// Validates a view-select scene. The golden gate holds every frame to the unchanged rule (with the
// rotation label's box left out, ruling R4), so RGB-shifted layers are refused, as in view-ref.js.
function validate(scene) {
  if (!/^[a-z0-9][a-z0-9-]*$/.test(scene.name || '')) throw new Error(`bad scene name: ${scene.name}`);
  const v = scene.view;
  if (!v || typeof v !== 'object') throw new Error(`${scene.name}: a view-select scene needs a "view" object`);
  if (!ZOOMS.includes(v.zoom)) throw new Error(`${scene.name}: zoom ${v.zoom} is not a 4.1 zoom step`);
  for (const k of ['mask', 'grid']) if (v[k] !== undefined && typeof v[k] !== 'boolean') throw new Error(`${scene.name}: view.${k} must be a boolean`);
  if (scene.actions) throw new Error(`${scene.name}: view-select scenes take no actions`);
  const layers = scene.layers || [];
  for (const l of layers) {
    if (l.rShift || l.gShift || l.bShift) throw new Error(`${scene.name}: RGB-shifted layers are not view scenes (rgb-shift-compute)`);
  }
  const hasSelect = v.select !== undefined;
  const p = scene.preview;
  if (hasSelect === (p !== undefined)) throw new Error(`${scene.name}: give exactly one of view.select and preview`);
  if (hasSelect && !(Number.isInteger(v.select) && v.select >= 0 && v.select < layers.length)) throw new Error(`${scene.name}: view.select must index a base layer`);
  if (p !== undefined) {
    if (p.op === 'shape') {
      if (!SHAPE_TOOLS[p.tool]) throw new Error(`${scene.name}: unknown shape tool "${p.tool}"`);
      checkPoint(scene.name, p.from, v.zoom);
      checkPoint(scene.name, p.to, v.zoom);
      if (!/^#[0-9a-f]{6}$/.test(p.color || '')) throw new Error(`${scene.name}: color must be #rrggbb lowercase`);
      if (!(Number.isInteger(p.width) && p.width >= 1 && p.width <= 50)) throw new Error(`${scene.name}: width must be 1..50 (editor.html:57)`);
      if (typeof p.fill !== 'boolean') throw new Error(`${scene.name}: fill must be a boolean`);
    } else if (p.op === 'erase') {
      if (!(Number.isInteger(p.layer) && p.layer >= 0 && p.layer < layers.length)) throw new Error(`${scene.name}: preview.layer must index a base layer`);
      // Two points only: 4.1 re-strokes the path per move and v5 strokes it once; the two agree exactly for one segment.
      if (!Array.isArray(p.points) || p.points.length !== 2) throw new Error(`${scene.name}: an erase preview has exactly two points`);
      for (const q of p.points) checkPoint(scene.name, q, v.zoom);
      if (!(Number.isInteger(p.size) && p.size >= 1 && p.size <= 50)) throw new Error(`${scene.name}: size must be 1..50 (editor.html:57)`);
    } else {
      throw new Error(`${scene.name}: unknown preview op "${p.op}"`);
    }
  }
  return {
    view: { zoom: v.zoom, mask: v.mask !== false, grid: v.grid === true, select: hasSelect ? v.select : null },
    preview: p === undefined ? null : p,
  };
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
  return scenes.map((s) => ({ scene: s, ...validate(s) }));
}

// Runs in the page after __pref.render. Drives the real zoom buttons and checkboxes, loads the label
// face, selects or starts the preview, renders, and returns the visible canvas.
function viewSelectPage() {
  const byId = (id) => {
    const el = document.getElementById(id);
    if (!el) throw new Error(`#${id} not found in editor.html`);
    return el;
  };
  // A real control, set and fired the way its 4.1 listener expects (editor.js:492-494). The value must stick.
  const setInput = (id, value, eventName = 'input') => {
    const el = byId(id);
    el.value = String(value);
    if (el.value !== String(value)) throw new Error(`#${id} refused ${value} (holds ${JSON.stringify(el.value)})`);
    el.dispatchEvent(new Event(eventName, { bubbles: true }));
  };
  // A synthetic page event on #mainCanvas, mapped back by (clientX - rect.left) / zoom (editor.js:1059-1061,
  // :1149-1152). app-harness.js pins the canvas at the viewport origin.
  const mouse = (type, point) => {
    const c = byId('mainCanvas');
    const rect = c.getBoundingClientRect();
    if (!Number.isInteger(rect.left) || !Number.isInteger(rect.top)) throw new Error(`canvas not at integer client coordinates (${rect.left}, ${rect.top})`);
    const clientX = rect.left + point[0] * zoom;
    const clientY = rect.top + point[1] * zoom;
    const ev = new MouseEvent(type, { clientX, clientY, button: 0, buttons: 1, bubbles: true, cancelable: true, view: window });
    if (ev.clientX !== clientX || ev.clientY !== clientY) throw new Error(`MouseEvent rounded (${clientX}, ${clientY})`);
    c.dispatchEvent(ev);
  };

  window.__viewSelect = async function (v, p) {
    const steps = Math.round((v.zoom - zoom) / 0.25);
    for (let i = 0; i < Math.abs(steps); i++) byId(steps > 0 ? 'zoomIn' : 'zoomOut').click(); // editor.js:1490-1491
    if (zoom !== v.zoom) throw new Error(`zoom is ${zoom}, wanted ${v.zoom}`);
    for (const [id, on] of [['showMask', v.mask], ['showGrid', v.grid]]) { // editor.js:1494-1495
      const el = byId(id);
      el.checked = on;
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }
    if (showMask !== v.mask || showGrid !== v.grid) throw new Error('mask or grid did not take');

    // The label's face (editor.js:416) is the vendored JetBrains Mono that styles.css:6 imports. The canvas
    // fetches a web font lazily, so load it first or 4.1 draws its fallback.
    const spec = '11px "JetBrains Mono"';
    const faces = await document.fonts.load(spec);
    await document.fonts.ready;
    if (!faces.length || faces.some((f) => f.status !== 'loaded') || !document.fonts.check(spec)) throw new Error(`vendored font did not load for ${spec}`);

    if (v.select !== null) {
      const id = layers[v.select].id;
      selectLayer(id); // editor.js:575-580: selects and sets the Select tool
      if (currentTool !== 'select' || selectedLayerId !== id) throw new Error('selectLayer did not take');
    } else {
      selectedLayerId = null;
    }

    if (p && p.op === 'shape') {
      setTool({ line: 'line', rect: 'rect', ellipse: 'circle' }[p.tool]); // editor.js:473-478
      setInput('drawColor', p.color); // editor.js:492
      setInput('drawSize', p.width); // editor.js:493
      const fill = byId('drawFill');
      fill.checked = p.fill;
      fill.dispatchEvent(new Event('change', { bubbles: true })); // editor.js:494
      mouse('mousedown', p.from); // editor.js:1120-1147
      mouse('mousemove', p.to); // clear and redraw the shape, editor.js:1204-1234
    } else if (p && p.op === 'erase') {
      selectLayer(layers[p.layer].id); // the eraser acts on the selection (editor.js:1265-1271)
      setTool('eraser');
      setInput('drawSize', p.size);
      mouse('mousedown', p.points[0]); // moveTo only, editor.js:1133-1144
      mouse('mousemove', p.points[1]); // lineTo + stroke, editor.js:1200-1203
    }

    render();
    if (p && (!drawCanvas || !isDrawing)) throw new Error('the live preview is not open');
    if (!p && drawCanvas) throw new Error('a live preview is open in a selection scene');
    if (p && currentTool === 'select') throw new Error('a preview scene must not show the selection overlay');
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
      tool: 'tools/parity-ref/view-select-ref.js',
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), stylesCssBlob: blob('styles.css'),
      fillsJsBlob: blob('lib/fills.js'), appHarnessBlob: blob('tools/parity-ref/app-harness.js'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
    };
    const index = { ...run, scenes: [] };
    for (const { scene, view, preview } of scenes) {
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(HARNESS);
      await win.webContents.executeJavaScript(`(${viewSelectPage.toString()})(); 0;`);
      await callPage(win, `__pref.render(${JSON.stringify(scene)}, ${JSON.stringify(assetsFor(scene))})`);
      const r = await callPage(win, `__viewSelect(${JSON.stringify(view)}, ${JSON.stringify(preview)})`);
      if (violations.length) throw new Error(`${scene.name}: ${violations.join('; ')}`);
      if (r.dpr !== 1) throw new Error(`${scene.name}: devicePixelRatio ${r.dpr}, expected 1`);
      const want = [Math.trunc(585 * view.zoom), Math.trunc(559 * view.zoom)]; // canvas.width = TW * zoom, editor.js:340-341
      if (r.width !== want[0] || r.height !== want[1]) throw new Error(`${scene.name}: canvas ${r.width}x${r.height}, expected ${want.join('x')}`);
      const bytes = Buffer.from(r.url.split(',')[1], 'base64');
      fs.writeFileSync(path.join(outDir, scene.name + '.png'), bytes);
      index.scenes.push({ name: scene.name, sha256: sha256(bytes), width: r.width, height: r.height, ...view, preview: preview ? preview.op : null });
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
