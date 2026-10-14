// Electron main. Renders v2 parity scenes by driving the REAL 4.1 app (editor.html loaded as-is,
// with the window settings main.js:5-13 uses) instead of lifted functions, so shapes, the eraser,
// details, Word Art and project loading are captured from the code 4.1 ships. GPU raster is off,
// as in render.js. Google Fonts requests are answered from assets/fonts (fonts.js); any other
// network request fails the run.
//   electron tools/parity-ref/app-render.js --scenes tools/parity-ref/app-scenes.json --out tests/goldens/app [--only a,b]
//   electron tools/parity-ref/app-render.js --save-projects tools/parity-ref/project-scenes.json --out-dir tests/goldens/projects [--only a,b]
const { app, BrowserWindow, protocol } = require('electron');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const fonts = require('./fonts.js');

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');

const ROOT = path.resolve(__dirname, '..', '..');
const HARNESS = fs.readFileSync(path.join(__dirname, 'app-harness.js'), 'utf8');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const blob = (rel) => execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();

const violations = [];
function checkNetwork(where) {
  if (violations.length) throw new Error(`${where}: ${violations.join('; ')}`);
}

function dataUrl(rel) {
  const file = path.resolve(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg' }[path.extname(file).toLowerCase()];
  if (!mime) throw new Error(`unsupported scene image type: ${rel}`);
  return `data:${mime};base64,` + fs.readFileSync(file).toString('base64');
}

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
  for (const n of names) if (!/^[a-z0-9][a-z0-9-]*$/.test(n || '')) throw new Error(`bad scene name: ${n}`);
  return scenes;
}

// Run an async page expression; a rejection comes back with its message instead of Electron's
// generic "Script failed to execute".
async function callPage(win, expr) {
  const r = await win.webContents.executeJavaScript(
    `(${expr}).then((value) => ({ ok: true, value }), (e) => ({ ok: false, error: String((e && e.message) || e) }))`);
  if (!r.ok) throw new Error(r.error);
  return r.value;
}

async function freshPage(win) {
  await win.loadFile(path.join(ROOT, 'editor.html'));
  await win.webContents.executeJavaScript(HARNESS);
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
    const saveFile = arg('--save-projects');
    const out = scenesFile ? arg('--out') : arg('--out-dir');
    if (!!scenesFile === !!saveFile || !out) {
      throw new Error('usage: --scenes <file> --out <dir> | --save-projects <file> --out-dir <dir> [--only a,b]');
    }
    const scenes = loadScenes(scenesFile || saveFile);
    const outDir = path.resolve(out);
    fs.mkdirSync(outDir, { recursive: true });

    const win = new BrowserWindow({
      show: false, width: 1200, height: 800,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false },
    });
    await freshPage(win);
    const n = await callPage(win, `__pref.selfCheck(${JSON.stringify(fonts.wordArtFaces())}, ${JSON.stringify(fonts.urlFor(fonts.FACES['Dancing Script'][700]))})`);
    checkNetwork('font self-check');
    console.log(`fonts ok: ${n} word art faces`);

    // Provenance is per scene and merged like render.js: a run with --only keeps every other
    // entry. Top-level fields describe the most recent run.
    const run = {
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), fillsJsBlob: blob('lib/fills.js'),
      eraseJsBlob: blob('lib/erase.js'), historyJsBlob: blob('lib/history.js'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
    };
    const indexFile = path.join(outDir, 'index.json');
    const prior = fs.existsSync(indexFile) ? JSON.parse(fs.readFileSync(indexFile, 'utf8')).scenes || [] : [];
    const index = { ...run, scenes: prior.slice() };

    for (const scene of scenes) {
      await freshPage(win);
      const call = scenesFile ? 'render' : 'save';
      const result = await callPage(win, `__pref.${call}(${JSON.stringify(scene)}, ${JSON.stringify(assetsFor(scene))})`);
      checkNetwork(scene.name);
      const bytes = scenesFile ? Buffer.from(result.split(',')[1], 'base64') : Buffer.from(result, 'utf8');
      fs.writeFileSync(path.join(outDir, scene.name + (scenesFile ? '.png' : '.r15proj')), bytes);
      const entry = { name: scene.name, sha256: sha256(bytes), ...run };
      const at = index.scenes.findIndex((s) => s.name === scene.name);
      if (at >= 0) index.scenes[at] = entry; else index.scenes.push(entry);
      console.log(scenesFile ? 'rendered' : 'saved', scene.name);
    }
    fs.writeFileSync(indexFile, JSON.stringify(index, null, 2) + '\n');
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
