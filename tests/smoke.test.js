// Syntax smoke: both scripts must parse. Guards against a broken file
// shipping at all — the cheapest possible floor under the lint gate.
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');

for (const file of ['main.js', 'editor.js']) {
  test(`${file} parses as a valid script`, () => {
    const src = fs.readFileSync(path.join(__dirname, '..', file), 'utf8');
    assert.doesNotThrow(() => new Function(src));
  });
}
