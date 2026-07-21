// Regression tests for feedback.md:7 — the layer name (user-controlled
// text, usually a filename) was interpolated into updateUI's innerHTML as
// an attribute value (value="${sel.name}"). A name containing a quote
// breaks out of the attribute; with nodeIntegration enabled in main.js
// that's markup injection with Node access — e.g. an asset pack shipping
// a file named  "><img src=x onerror=require('child_process')...>.png
//
// The invariant: NO attribute value in editor.js is ever built by template
// interpolation. User-visible control values are assigned through DOM
// properties after the markup is parsed, so the parser never sees them.
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');

const src = fs.readFileSync(path.join(__dirname, '..', 'editor.js'), 'utf8');

test('no attribute value is built by template interpolation', () => {
  const hits = src.match(/value="\$\{[^}]*\}"/g) || [];
  assert.deepStrictEqual(hits, [],
    'value attributes must be assigned via DOM properties, not interpolated into markup: ' + hits.join(', '));
});

test('layer name reaches the name input via property assignment', () => {
  assert.match(src, /getElementById\('ctrlName'\)\.value = sel\.name/,
    'ctrlName must receive sel.name through the .value property');
});

test('layer names render in the layer list via textContent, not markup', () => {
  assert.match(src, /nameSpan\.textContent = l\.name/,
    'layer-list names must stay on the textContent path');
});
