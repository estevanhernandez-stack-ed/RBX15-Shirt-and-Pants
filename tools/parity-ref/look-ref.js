// Electron main. Captures the WHOLE 4.1 window content (editor.html with styles.css, the ribbon, the
// panels, the canvas and the footer) for the M3b look sign-off, so v5's shell can be compared with 4.1
// side by side in matched states. It is a reference capture for a person, not a golden: nothing under
// tests/goldens is written, and no test reads its output.
//
// Each state (look-states.json) is a list of semantic ops, which this script maps onto 4.1's own
// controls (the v5 UIA driver maps the same ops onto v5's):
//   paint {region}        click the region tile on the Layout tab (editor.js:1657-1675; no layer is selected)
//   fillType {type}       click the fill type button on the Layout tab (editor.js:1600-1607)
//   undo                  click #btnUndo (editor.js:1477-1481)
//   mode                  click #btnModeToggle (editor.js:1719-1725)
//   addImage {image}      hand the file to #imageLoadInput, the Add image path (editor.js:2692-2707)
//   rotation {value}      set #ctrlRot and fire input (editor.js:833-837)
//   expandAdjustments     click #adjToggle (editor.js:862-872)
//   tool {tool}           click the tool button (editor.js:464-490); pen is #btnDrawPen
//   zoom {value}          click #zoomIn or #zoomOut until zoom equals value (editor.js:1490-1491)
//   scrollPanel           scroll the left panel to its end, so the Selected layer section shows (styles.css:246-253)
// app-harness.js is NOT injected: it pins #mainCanvas to the viewport origin for integer mouse
// coordinates, which would move the canvas in a full-window capture. These ops need no mouse events.
//
// The window is hidden, GPU raster is off, the device scale is 1, Google Fonts requests are answered
// from assets/fonts (fonts.js) and any other network request fails the run. The content size is the v5
// client size the UIA driver measured, so both captures have the same pixel size:
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/look-ref.js \
//     --states tools/parity-ref/look-states.json --content 1184x761 --out .superpowers/m3b-look/ref [--only a,b]
// Output: <state>-41.png (plus <state>-<tab>-41.png for alsoTab41), metrics.json and index.json.
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
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const blob = (rel) => execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();

const OPS = new Set(['paint', 'fillType', 'undo', 'mode', 'addImage', 'rotation', 'expandAdjustments', 'tool', 'zoom', 'scrollPanel']);
const TOOLS = { select: 'btnSelect', pen: 'btnDrawPen', line: 'btnDrawLine', rect: 'btnDrawRect', ellipse: 'btnDrawCircle', eraser: 'btnEraser' };
const FILL_TYPES = ['solid', 'linear', 'radial', 'stripes', 'checker', 'dots', 'camo']; // editor.html:148-154
const ZOOMS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5, 2.75, 3];
const TABS = ['home', 'layout', 'details', 'text']; // editor.html:22-25

// The elements whose client rectangles both sides record (CSS px here, DIPs on the v5 side). The v5
// driver's METRICS table names the same keys.
const METRICS = {
  headerTitle: 'header h1',
  homeRibbonPane: '.ribbon-pane[data-tab="home"]',
  modeToggle: '#btnModeToggle',
  downloadTemplate: '#btnExport',
  leftPanel: '.app > .panel',
  canvasArea: '.canvas-area',
  zoomIn: '#zoomIn',
  footer: 'footer',
};

function validate(s) {
  if (!/^\d\d-[a-z0-9-]+$/.test(s.name || '')) throw new Error(`bad state name: ${s.name}`);
  if (typeof s.shows !== 'string' || !s.shows) throw new Error(`${s.name}: "shows" describes the state for the page`);
  if (s.alsoTab41 !== undefined && !TABS.includes(s.alsoTab41)) throw new Error(`${s.name}: alsoTab41 ${s.alsoTab41}`);
  if (s.contentWidth !== undefined && !(Number.isInteger(s.contentWidth) && s.contentWidth >= 640)) throw new Error(`${s.name}: contentWidth`);
  for (const op of s.ops) {
    if (!OPS.has(op.op)) throw new Error(`${s.name}: unknown op ${op.op}`);
    if (op.op === 'fillType' && !FILL_TYPES.includes(op.type)) throw new Error(`${s.name}: fill type ${op.type}`);
    if (op.op === 'paint' && !/^(torso|waist|rarm|larm|rleg|lleg)_(up|down|front|back|left|right)$/.test(op.region)) throw new Error(`${s.name}: region ${op.region}`);
    if (op.op === 'tool' && !TOOLS[op.tool]) throw new Error(`${s.name}: tool ${op.tool}`);
    if (op.op === 'zoom' && !ZOOMS.includes(op.value)) throw new Error(`${s.name}: zoom ${op.value}`);
    if (op.op === 'rotation' && !(Number.isInteger(op.value) && op.value >= -180 && op.value <= 180)) throw new Error(`${s.name}: rotation ${op.value}`);
    if (op.op === 'addImage' && !fs.existsSync(path.resolve(ROOT, op.image))) throw new Error(`${s.name}: image not found ${op.image}`);
  }
  return s;
}

function loadStates(file) {
  let states = JSON.parse(fs.readFileSync(path.resolve(file), 'utf8')).map(validate);
  const only = arg('--only');
  if (only) {
    const want = only.split(',');
    for (const n of want) if (!states.some((s) => s.name === n)) throw new Error(`--only names an unknown state: ${n}`);
    states = states.filter((s) => want.includes(s.name));
  }
  if (new Set(states.map((s) => s.name)).size !== states.length) throw new Error('duplicate state names');
  return states;
}

// Runs in the page. Every op goes through a real control and waits for 4.1 to settle.
function lookPage() {
  const alerts = [];
  window.alert = (m) => { alerts.push(String(m)); };
  window.confirm = () => true;
  const byId = (id) => {
    const el = document.getElementById(id);
    if (!el) throw new Error(`#${id} not found in editor.html`);
    return el;
  };
  const one = (sel) => {
    const el = document.querySelector(sel);
    if (!el) throw new Error(`${sel} not found`);
    return el;
  };
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  async function waitFor(done, what, ms = 15000) {
    const start = performance.now();
    while (!done()) {
      if (alerts.length) throw new Error(`4.1 raised an alert while waiting for ${what}: ${alerts.join(' | ')}`);
      if (performance.now() - start > ms) throw new Error(`timed out waiting for ${what}`);
      await sleep(10);
    }
  }
  const frames = () => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
  const tab = (name) => one(`.ribbon-tab[data-tab="${name}"]`).click(); // editor.js:4-13

  async function run(op, files) {
    switch (op.op) {
      case 'paint': {
        if (selectedLayerId !== null) throw new Error('paint needs no selection: a tile click with a layer selected fits the layer (editor.js:1655-1666)');
        tab('layout');
        const before = JSON.stringify(regionColors);
        one(`.region-btn[data-r="${op.region}"]`).click();
        if (JSON.stringify(regionColors) === before && !regionColors[op.region]) throw new Error(`${op.region} did not paint`);
        break;
      }
      case 'fillType':
        tab('layout');
        one(`.fill-type-btn[data-fill="${op.type}"]`).click();
        if (currentFill.type !== op.type) throw new Error(`fill type is ${currentFill.type}`);
        break;
      case 'undo': {
        const u = byId('btnUndo');
        if (u.disabled) throw new Error('Undo is disabled');
        u.click();
        break;
      }
      case 'mode': {
        const was = currentMode;
        byId('btnModeToggle').click();
        if (currentMode === was) throw new Error('mode did not switch');
        break;
      }
      case 'addImage': {
        const f = files[op.image];
        const input = byId('imageLoadInput');
        const dt = new DataTransfer();
        dt.items.add(new File([Uint8Array.from(atob(f.b64), (c) => c.charCodeAt(0))], f.name, { type: 'image/png' }));
        input.files = dt.files;
        const n = layers.length;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        await waitFor(() => layers.length === n + 1, `the ${f.name} layer`);
        if (currentTool !== 'select' || selectedLayerId !== layers[layers.length - 1].id) throw new Error('addLayer did not select the new layer');
        break;
      }
      case 'rotation': {
        const el = byId('ctrlRot');
        el.value = String(op.value);
        if (el.value !== String(op.value)) throw new Error(`#ctrlRot refused ${op.value}`);
        el.dispatchEvent(new Event('input', { bubbles: true }));
        if (getSelected().rotation !== op.value) throw new Error('rotation did not take');
        break;
      }
      case 'expandAdjustments':
        byId('adjToggle').click();
        if (byId('adjPanel').style.display !== 'block') throw new Error('Image adjustments did not expand');
        break;
      case 'tool': {
        const ids = { select: 'btnSelect', pen: 'btnDrawPen', line: 'btnDrawLine', rect: 'btnDrawRect', ellipse: 'btnDrawCircle', eraser: 'btnEraser' };
        byId(ids[op.tool]).click();
        const want = op.tool === 'ellipse' ? 'circle' : op.tool;
        if (currentTool !== want) throw new Error(`tool is ${currentTool}`);
        break;
      }
      case 'zoom': {
        const steps = Math.round((op.value - zoom) / 0.25);
        for (let i = 0; i < Math.abs(steps); i++) byId(steps > 0 ? 'zoomIn' : 'zoomOut').click();
        if (zoom !== op.value) throw new Error(`zoom is ${zoom}`);
        break;
      }
      case 'scrollPanel': {
        const p = one('.app > .panel');
        p.scrollTop = p.scrollHeight;
        if (p.scrollHeight > p.clientHeight && p.scrollTop === 0) throw new Error('the left panel did not scroll');
        break;
      }
      default: throw new Error(`unknown op ${op.op}`);
    }
    await frames();
  }

  window.__look = {
    async apply(state, files) {
      await document.fonts.ready;
      for (const [i, op] of state.ops.entries()) {
        try { await run(op, files); } catch (e) { throw new Error(`${state.name} op ${i} (${op.op}): ${e.message}`); }
      }
      if (alerts.length) throw new Error(`4.1 raised an alert: ${alerts.join(' | ')}`);
      return this.show('home');
    },
    async show(name) {
      tab(name);
      await document.fonts.ready;
      await frames();
      return { dpr: window.devicePixelRatio, w: document.documentElement.clientWidth, h: window.innerHeight };
    },
    metrics(map) {
      const out = {};
      for (const [k, sel] of Object.entries(map)) {
        const el = document.querySelector(sel);
        if (!el) throw new Error(`metric ${k}: ${sel} not found`);
        const r = el.getBoundingClientRect();
        out[k] = { x: r.left, y: r.top, w: r.width, h: r.height };
      }
      return out;
    },
  };
}

async function callPage(win, expr) {
  const r = await win.webContents.executeJavaScript(
    `Promise.resolve(${expr}).then((value) => ({ ok: true, value }), (e) => ({ ok: false, error: String((e && e.message) || e) }))`);
  if (!r.ok) throw new Error(r.error);
  return r.value;
}

async function capture(win, file, size) {
  const img = await win.webContents.capturePage(undefined, { stayHidden: true });
  const got = img.getSize();
  if (got.width !== size.w || got.height !== size.h) throw new Error(`${path.basename(file)}: captured ${got.width}x${got.height}, expected ${size.w}x${size.h}`);
  const png = img.toPNG();
  fs.writeFileSync(file, png);
  return { file: path.basename(file), sha256: sha256(png), width: got.width, height: got.height };
}

app.whenReady().then(async () => {
  const violations = [];
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
    const statesFile = arg('--states');
    const out = arg('--out');
    const content = /^(\d+)x(\d+)$/.exec(arg('--content') || '');
    if (!statesFile || !out || !content) throw new Error('usage: --states <file> --content <W>x<H> --out <dir> [--only a,b]');
    const base = { w: +content[1], h: +content[2] };
    if (base.w < 640 || base.h < 400) throw new Error(`--content ${base.w}x${base.h} is not a window client size`);
    const states = loadStates(statesFile);
    const outDir = path.resolve(out);
    fs.mkdirSync(outDir, { recursive: true });

    const win = new BrowserWindow({
      show: false, useContentSize: true, width: base.w, height: base.h, paintWhenInitiallyHidden: true,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false },
    });
    win.setMenuBarVisibility(false); // main.js:23
    const index = {
      tool: 'tools/parity-ref/look-ref.js',
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), stylesCssBlob: blob('styles.css'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
      content: `${base.w}x${base.h}`, states: [],
    };
    const metrics = {};
    for (const state of states) {
      const size = { w: state.contentWidth ?? base.w, h: base.h };
      win.setContentSize(size.w, size.h);
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(`(${lookPage.toString()})(); 0;`);
      const files = {};
      for (const op of state.ops.filter((o) => o.op === 'addImage')) {
        files[op.image] = { name: path.basename(op.image), b64: fs.readFileSync(path.resolve(ROOT, op.image)).toString('base64') };
      }
      const page = await callPage(win, `__look.apply(${JSON.stringify(state)}, ${JSON.stringify(files)})`);
      if (page.dpr !== 1) throw new Error(`${state.name}: devicePixelRatio ${page.dpr}, expected 1`);
      if (page.w > size.w || page.h !== size.h) throw new Error(`${state.name}: viewport ${page.w}x${page.h}, expected ${size.w}x${size.h}`);
      metrics[state.name] = await callPage(win, `__look.metrics(${JSON.stringify(METRICS)})`);
      const shots = [await capture(win, path.join(outDir, `${state.name}-41.png`), size)];
      if (state.alsoTab41) {
        await callPage(win, `__look.show(${JSON.stringify(state.alsoTab41)})`);
        shots.push(await capture(win, path.join(outDir, `${state.name}-${state.alsoTab41}-41.png`), size));
      }
      if (violations.length) throw new Error(`${state.name}: ${violations.join('; ')}`);
      index.states.push({ name: state.name, shows: state.shows, shots });
      console.log(`captured ${state.name} ${size.w}x${size.h}${state.alsoTab41 ? ` (+${state.alsoTab41})` : ''}`);
    }
    fs.writeFileSync(path.join(outDir, 'metrics.json'), JSON.stringify(metrics, null, 2) + '\n');
    fs.writeFileSync(path.join(outDir, 'index.json'), JSON.stringify(index, null, 2) + '\n');
    console.log(`ok ${index.states.length}`);
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
