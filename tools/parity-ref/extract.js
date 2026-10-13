// Lifts the shipped render code out of editor.js, verbatim, so goldens are made by
// the code 4.1 actually ships rather than a re-implementation of it. Every piece must
// be found exactly once; anything else throws, so editor.js drift can't silently
// produce goldens of the wrong code.
const FUNCTIONS = ['unionRect', 'connectedRegionGroups', 'paintRegions', 'applyRGBShift',
  'hasAdjustments', 'drawLayerWithAdjustments', 'buildMask'];

function once(src, re, what) {
  const all = [...src.matchAll(new RegExp(re.source, re.flags.includes('g') ? re.flags : re.flags + 'g'))];
  if (all.length !== 1) throw new Error(`parity-ref: ${what} ${all.length ? 'found ' + all.length + ' times' : 'not found'} in editor.js`);
  return all[0][0];
}

function extractShipped(editorSource) {
  const src = editorSource.replace(/\r\n/g, '\n');
  const constants = [
    once(src, /^const TW = 585, TH = 559;$/m, 'TW/TH'),
    once(src, /^const SHIRT_REGION_DATA = \{[\s\S]*?\n\};$/m, 'SHIRT_REGION_DATA'),
    once(src, /^const PANTS_REGION_DATA = \{[\s\S]*?\n\};$/m, 'PANTS_REGION_DATA'),
  ].join('\n');
  const functions = {};
  for (const name of FUNCTIONS) {
    functions[name] = once(src, new RegExp(`^function ${name}\\([\\s\\S]*?\\n\\}$`, 'm'), `function ${name}`);
  }
  const start = src.indexOf('  ec.clearRect(0, 0, TW, TH);');
  const end = src.indexOf('  const link = document.createElement', start);
  if (start < 0 || end < 0 || src.indexOf('  ec.clearRect(0, 0, TW, TH);', start + 1) >= 0) {
    throw new Error('parity-ref: exportPNG composition body not found exactly once in editor.js');
  }
  return { constants, functions, exportBody: src.slice(start, end) };
}

module.exports = { extractShipped, FUNCTIONS };
