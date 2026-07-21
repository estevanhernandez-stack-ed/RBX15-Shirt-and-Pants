// Surgical lint gate: no-undef only. This class of bug (a reference to a
// variable that doesn't exist) shipped to production in v4.0 — see PR for
// the clickName incident. Broader style rules are intentionally off.
const globals = require('globals');

module.exports = [
  {
    files: ['main.js', 'editor.js'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'script',
      globals: {
        ...globals.browser,
        // Renderer runs with nodeIntegration enabled, so Node globals
        // (require, process, __dirname) are live in editor.js too.
        ...globals.node,
      },
    },
    rules: {
      'no-undef': 'error',
    },
  },
];
