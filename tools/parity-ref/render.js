// Electron main. Renders parity scenes through SHIPPED 4.1 code (lifted by extract.js)
// with GPU raster off, the honest reference from the 2026-10-11 spike: GPU canvas
// changes antialiased edges machine to machine; CPU raster does not.
//   electron tools/parity-ref/render.js --scenes tools/parity-ref/scenes.json --out tests/goldens/render [--only a,b]
const { app, BrowserWindow } = require('electron');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { extractShipped } = require('./extract.js');

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');

const ROOT = path.resolve(__dirname, '..', '..');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };

function page() {
  const x = extractShipped(fs.readFileSync(path.join(ROOT, 'editor.js'), 'utf8'));
  const fills = fs.readFileSync(path.join(ROOT, 'lib', 'fills.js'), 'utf8');
  return `<!doctype html><meta charset="utf-8"><body><script>
${fills}
${x.constants}
let SHIRT_REGIONS, regionColors, layers, bgImage, bgColor, bgOpacity, maskCanvas, maskCtx;
let _rgbCache = { layerId: null, r: 0, g: 0, b: 0, canvas: null };
${Object.values(x.functions).join('\n')}
function loadImg(src) {
  return new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = () => rej(new Error('decode failed')); i.src = src; });
}
// Defaults mirror makeLayer (editor.js:134-142) and the editor state (editor.js:76-107).
async function renderScene(scene, images) {
  SHIRT_REGIONS = scene.mode === 'Pants' ? { ...PANTS_REGION_DATA } : { ...SHIRT_REGION_DATA };
  regionColors = scene.regionColors || {};
  bgColor = scene.bgColor ?? '#192e44';
  bgOpacity = scene.bgOpacity ?? 1;
  bgImage = scene.bgImage ? await loadImg(images[scene.bgImage]) : null;
  _rgbCache = { layerId: null, r: 0, g: 0, b: 0, canvas: null };
  layers = [];
  for (const [i, d] of (scene.layers || []).entries()) {
    layers.push({
      id: i + 1, name: 'L' + (i + 1), img: await loadImg(images[d.image]),
      visible: d.visible ?? true, opacity: d.opacity ?? 1, x: d.x ?? 0, y: d.y ?? 0, scale: d.scale ?? 1,
      rotation: d.rotation ?? 0, flipH: !!d.flipH, flipV: !!d.flipV,
      brightness: d.brightness ?? 100, contrast: d.contrast ?? 100, saturate: d.saturate ?? 100,
      hueRotate: d.hueRotate ?? 0, rShift: d.rShift ?? 0, gShift: d.gShift ?? 0, bShift: d.bShift ?? 0,
    });
  }
  buildMask();
  const exp = document.createElement('canvas');
  exp.width = TW; exp.height = TH;
  const ec = exp.getContext('2d');
${x.exportBody}
  return exp.toDataURL('image/png');
}
</script></body>`;
}

function dataUrl(rel) {
  const file = path.join(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  return 'data:image/png;base64,' + fs.readFileSync(file).toString('base64');
}

function blob(rel) {
  return execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();
}

app.whenReady().then(async () => {
  try {
    const scenesFile = arg('--scenes');
    const out = arg('--out');
    if (!scenesFile || !out) throw new Error('usage: --scenes <file> --out <dir> [--only a,b]');
    let scenes = JSON.parse(fs.readFileSync(path.resolve(scenesFile), 'utf8'));
    const only = arg('--only');
    if (only) scenes = scenes.filter((s) => only.split(',').includes(s.name));
    const names = scenes.map((s) => s.name);
    if (new Set(names).size !== names.length) throw new Error('duplicate scene names');
    fs.mkdirSync(path.resolve(out), { recursive: true });

    const win = new BrowserWindow({ show: false, webPreferences: { contextIsolation: false } });
    await win.loadURL('data:text/html;base64,' + Buffer.from(page()).toString('base64'));
    const index = {
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), fillsJsBlob: blob('lib/fills.js'), scenes: [],
    };
    for (const scene of scenes) {
      const images = {};
      for (const rel of [scene.bgImage, ...(scene.layers || []).map((l) => l.image)].filter(Boolean)) images[rel] = dataUrl(rel);
      const url = await win.webContents.executeJavaScript(`renderScene(${JSON.stringify(scene)}, ${JSON.stringify(images)})`);
      const png = Buffer.from(url.split(',')[1], 'base64');
      fs.writeFileSync(path.join(path.resolve(out), scene.name + '.png'), png);
      index.scenes.push({ name: scene.name, sha256: crypto.createHash('sha256').update(png).digest('hex') });
      console.log('rendered', scene.name);
    }
    fs.writeFileSync(path.join(path.resolve(out), 'index.json'), JSON.stringify(index, null, 2) + '\n');
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
