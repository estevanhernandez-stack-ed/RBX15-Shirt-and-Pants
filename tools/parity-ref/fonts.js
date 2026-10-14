// Maps the Google Fonts css2 requests 4.1 makes (editor.html:11 for Word Art, styles.css:6 for the
// UI) to the vendored files in assets/fonts (pinned by assets/fonts/README.md). app-render.js
// answers fonts.googleapis.com and fonts.gstatic.com from here, so goldens are drawn with exactly
// these bytes and never with whatever Google serves today. A face 4.1 requests that has no vendored
// file is an error, not a network fetch.
const fs = require('node:fs');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..', '..');
const FONTS_DIR = path.join(ROOT, 'assets', 'fonts');
const HOST = 'https://fonts.gstatic.com/rbx15-vendored/';

// family -> requested weight -> path under assets/fonts. The weights are exactly the ones 4.1
// requests; a variable file serves several weights and Chromium sets its wght axis to each.
const FACES = {
  'Anton': { 400: 'anton/Anton-Regular.ttf' },
  'Bebas Neue': { 400: 'bebasneue/BebasNeue-Regular.ttf' },
  'Dancing Script': { 700: 'dancingscript/DancingScript[wght].ttf' },
  'Great Vibes': { 400: 'greatvibes/GreatVibes-Regular.ttf' },
  'Pacifico': { 400: 'pacifico/Pacifico-Regular.ttf' },
  'Creepster': { 400: 'creepster/Creepster-Regular.ttf' },
  'Poppins': { 700: 'poppins/Poppins-Bold.ttf', 900: 'poppins/Poppins-Black.ttf' },
  'Lato': { 900: 'lato/Lato-Black.ttf' },
  'Bangers': { 400: 'bangers/Bangers-Regular.ttf' },
  'Permanent Marker': { 400: 'permanentmarker/PermanentMarker-Regular.ttf' },
  'Bungee Shade': { 400: 'bungeeshade/BungeeShade-Regular.ttf' },
  'Rubik Mono One': { 400: 'rubikmonoone/RubikMonoOne-Regular.ttf' },
  'Press Start 2P': { 400: 'pressstart2p/PressStart2P-Regular.ttf' },
  'Righteous': { 400: 'righteous/Righteous-Regular.ttf' },
  'Orbitron': { 700: 'orbitron/Orbitron[wght].ttf', 900: 'orbitron/Orbitron[wght].ttf' },
  'Space Grotesk': {
    400: 'spacegrotesk/SpaceGrotesk[wght].ttf', 500: 'spacegrotesk/SpaceGrotesk[wght].ttf',
    600: 'spacegrotesk/SpaceGrotesk[wght].ttf', 700: 'spacegrotesk/SpaceGrotesk[wght].ttf',
  },
  'JetBrains Mono': {
    400: 'jetbrainsmono/JetBrainsMono[wght].ttf', 500: 'jetbrainsmono/JetBrainsMono[wght].ttf',
    600: 'jetbrainsmono/JetBrainsMono[wght].ttf',
  },
  'Inter': {
    400: 'inter/Inter[opsz,wght].ttf', 500: 'inter/Inter[opsz,wght].ttf',
    600: 'inter/Inter[opsz,wght].ttf', 700: 'inter/Inter[opsz,wght].ttf',
  },
};

// The <select id="waFont"> options, in order (editor.html:231-245).
const WORDART_FAMILIES = ['Anton', 'Bebas Neue', 'Dancing Script', 'Great Vibes', 'Pacifico', 'Creepster',
  'Poppins', 'Lato', 'Bangers', 'Permanent Marker', 'Bungee Shade', 'Rubik Mono One', 'Press Start 2P',
  'Righteous', 'Orbitron'];

const VENDORED = new Set(Object.values(FACES).flatMap((w) => Object.values(w)));

function facesFor(url) {
  const u = new URL(url);
  if (u.origin !== 'https://fonts.googleapis.com' || u.pathname !== '/css2') throw new Error(`not a Google Fonts css2 url: ${url}`);
  const out = [];
  for (const spec of u.searchParams.getAll('family')) {
    const [family, axes] = spec.split(':');
    let weights = [400];
    if (axes !== undefined) {
      const m = /^wght@(\d+(?:;\d+)*)$/.exec(axes);
      if (!m) throw new Error(`unsupported axis spec "${axes}" for ${family}`);
      weights = m[1].split(';').map(Number);
    }
    for (const weight of weights) {
      const file = FACES[family] && FACES[family][weight];
      if (!file) throw new Error(`no vendored face for ${family} ${weight}`);
      out.push({ family, weight, file });
    }
  }
  return out;
}

const urlFor = (file) => HOST + file.split('/').map(encodeURIComponent).join('/');

function cssFor(url) {
  return facesFor(url).map((f) => [
    '@font-face {',
    `  font-family: '${f.family}';`,
    '  font-style: normal;',
    `  font-weight: ${f.weight};`,
    '  font-display: swap;',
    `  src: url(${urlFor(f.file)}) format('truetype');`,
    '}',
    '',
  ].join('\n')).join('');
}

function fileFor(url) {
  if (!url.startsWith(HOST)) return null;
  const rel = new URL(url).pathname.slice('/rbx15-vendored/'.length).split('/').map(decodeURIComponent).join('/');
  if (!VENDORED.has(rel)) throw new Error(`not a vendored font: ${rel}`);
  return path.join(FONTS_DIR, ...rel.split('/'));
}

function respond(url) {
  const u = new URL(url);
  if (u.hostname === 'fonts.googleapis.com') {
    return { body: cssFor(url), headers: { 'content-type': 'text/css; charset=utf-8', 'access-control-allow-origin': '*' } };
  }
  const file = fileFor(url);
  if (file) return { body: fs.readFileSync(file), headers: { 'content-type': 'font/ttf', 'access-control-allow-origin': '*' } };
  return null;
}

const wordArtFaces = () => WORDART_FAMILIES.flatMap((family) => Object.keys(FACES[family]).map((w) => ({ family, weight: Number(w) })));

module.exports = { FACES, WORDART_FAMILIES, FONTS_DIR, HOST, facesFor, cssFor, urlFor, fileFor, respond, wordArtFaces };
