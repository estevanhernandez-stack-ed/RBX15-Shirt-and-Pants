// M2b Task 3 froze this tool's output once in tests/goldens/app/wordart-ref, which WordArtSwapTests
// reads; never re-run it into that folder. Renders each Word Art scene's text bitmap with 4.1's own
// renderWordArt (editor.js:2120-2240) in the real editor.html: Electron on CPU raster, Google Fonts
// answered from assets/fonts through fonts.js, the controls set as app-harness.js wordart() sets
// them. Each PNG is the bitmap Add to canvas decodes (editor.js:2259-2269), so swapping it into the
// engine's layer isolates the text bitmap from placement and compositing. metrics.json records
// canvas size, measureText widths and the middle-baseline offset per scene.
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/wordart-ref.js \
//     --out <dir> [--scenes tests/goldens/app/scenes.json] [--only name1,name2]
'use strict';
const { app, BrowserWindow, protocol } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const fonts = require('./fonts.js');

const ROOT = path.resolve(__dirname, '..', '..');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const OUT = path.resolve(arg('--out') || path.join(ROOT, '.superpowers', 'm2b-measure', 'wordart-ref'));
const SCENES = path.resolve(arg('--scenes') || path.join(ROOT, 'tests', 'goldens', 'app', 'scenes.json'));
const ONLY = arg('--only') ? new Set(arg('--only').split(',')) : null;

// Runs in the page. Sets only the controls the op names, selects the font, clicks the style,
// waits for the exact face renderWordArt asks for, then renders at full size (cw x ch).
function page() {
  window.__wa = async function (op) {
    const set = (id, v, ev = 'input') => { const el = document.getElementById(id); el.value = String(v); el.dispatchEvent(new Event(ev, { bubbles: true })); };
    for (const [k, id] of [['text', 'waText'], ['fill', 'waColor'], ['stroke', 'waStroke'], ['grad', 'waColor2'], ['size', 'waSize'], ['strokeWidth', 'waStrokeW'], ['angle', 'waAngle']]) if (op[k] !== undefined) set(id, op[k]);
    set('waFont', op.font, 'change');
    document.querySelector('.wordart-style-btn[data-style="' + op.style + '"]').click();
    const spec = '900 ' + (+document.getElementById('waSize').value || 64) + 'px "' + op.font + '"';
    await document.fonts.load(spec); await document.fonts.ready;
    const c = document.createElement('canvas'); c.width = 1024; c.height = 512;
    const r1 = renderWordArt(c, 0, 0); c.width = r1.cw; c.height = r1.ch; renderWordArt(c, 0, 0);
    // measureText with renderWordArt's own font string (editor.js:2131-2132) and text (:2121).
    const t = document.createElement('canvas').getContext('2d'); t.font = spec + ', sans-serif';
    const lines = (document.getElementById('waText').value || 'TEXT').split('\n');
    const m = lines.map((l) => { const mm = t.measureText(l); return { w: mm.width, chars: [...l].map((ch) => t.measureText(ch).width), abl: mm.actualBoundingBoxLeft, abr: mm.actualBoundingBoxRight, aba: mm.actualBoundingBoxAscent, abd: mm.actualBoundingBoxDescent, fba: mm.fontBoundingBoxAscent, fbd: mm.fontBoundingBoxDescent }; });
    t.textBaseline = 'middle'; const mid = t.measureText('R');
    return { cw: r1.cw, ch: r1.ch, m, midAlphabetic: mid.alphabeticBaseline, url: c.toDataURL('image/png') };
  };
}

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
app.whenReady().then(async () => {
  try {
    for (const scheme of ['https', 'http']) {
      protocol.handle(scheme, (req) => {
        const r = fonts.respond(req.url);
        return r ? new Response(r.body, { status: 200, headers: r.headers }) : new Response('not vendored', { status: 404 });
      });
    }
    const scenes = JSON.parse(fs.readFileSync(SCENES, 'utf8'))
      .filter((s) => (s.actions || []).some((a) => a.op === 'wordart'))
      .filter((s) => !ONLY || ONLY.has(s.name));
    fs.mkdirSync(OUT, { recursive: true });
    const win = new BrowserWindow({ show: false, width: 1200, height: 800, webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false } });
    const results = {};
    for (const sc of scenes) {
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(`(${page.toString()})(); 0;`);
      const op = sc.actions.find((a) => a.op === 'wordart');
      const r = await win.webContents.executeJavaScript(`__wa(${JSON.stringify(op)}).then((v) => ({ ok: true, v }), (e) => ({ ok: false, e: String((e && e.stack) || e) }))`);
      if (!r.ok) throw new Error(sc.name + ': ' + r.e);
      fs.writeFileSync(path.join(OUT, sc.name + '.png'), Buffer.from(r.v.url.split(',')[1], 'base64'));
      delete r.v.url;
      results[sc.name] = r.v;
      console.log('rendered ' + sc.name);
    }
    fs.writeFileSync(path.join(OUT, 'metrics.json'), JSON.stringify(results, null, 1) + '\n');
    console.log('ok ' + Object.keys(results).length);
    app.exit(0);
  } catch (e) {
    console.error(e.stack);
    app.exit(1);
  }
});
