# tools/parity-ref

Renders parity scenes through shipped 4.1 code in Electron with GPU raster off. The output is the frozen golden set the native v5 engine is held to (spec section 6). There are two renderers:

- `render.js` (M0) lifts the render functions verbatim out of `editor.js` (`extract.js`) and renders the render-core scenes into `tests/goldens/render`.
- `app-render.js` (goldens v2) loads the real `editor.html` and drives it the way a person does: real globals, real inputs and buttons, and synthetic mouse events on the canvas. It renders the v2 scenes into `tests/goldens/app`.

## Rules

- Goldens in `tests/goldens/` are made **only** by these tools, run by a person. CI never regenerates them.
- They are frozen from 4.1. Regenerating them after `editor.js` changes is wrong unless the change is a 4.x release the native port must also match.
- A deliberate v5 divergence with a visual effect gets a **new** golden next to the 4.1 one, plus a note in `docs/port/divergences.md`. The 4.1 golden is never overwritten.
- Word Art goldens are drawn with the vendored fonts in `assets/fonts` (see its README). `app-render.js` answers every Google Fonts request from those files and fails on any other network request.
- Project fixtures in `tests/goldens/projects` were written once by 4.1's own `saveProject`. They embed `savedAt`, so re-saving never reproduces them. Never re-run `--save-projects` into that folder.
- This directory is deleted at cutover. The goldens and `assets/fonts` stay.

## Use

```bash
node --test "tools/parity-ref/*.test.js"   # the tools' own tests
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/render.js \
  --scenes tools/parity-ref/scenes.json --out tests/goldens/render [--only name1,name2]
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js \
  --scenes tools/parity-ref/app-scenes.json --out tests/goldens/app [--only name1,name2]
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js \
  --save-projects tools/parity-ref/project-scenes.json --out-dir <dir> [--only name1,name2]
```

Each `index.json` records, per golden, its SHA-256, the Electron and Chrome versions, and the git blob hashes of the shipped files that produced it. `app-render.js` also records `editor.html`, `lib/erase.js` and `lib/history.js`, plus the SHA-256 of the font manifest. A run with `--only` merges into the existing index and never drops other goldens' records.

## Scene format

`{ name, mode?, bgColor?, bgOpacity?, bgImage?, regionColors?, layers?: [{ image, x?, y?, scale?, rotation?, flipH?, flipV?, opacity?, visible?, brightness?, contrast?, saturate?, hueRotate?, rShift?, gShift?, bShift? }], actions? }`

- Image paths are repo-relative. Defaults match `makeLayer` (`editor.js:134-142`) and the editor's initial state. The output is always the export composition, so the mask is always applied.
- `actions` (v2, `app-render.js` only) run in order after the base fields:
  - `{ op: "shape", tool: "line"|"rect"|"ellipse", from: [x,y], to: [x,y], color: "#rrggbb", width, fill }` sends mousedown, one mousemove and mouseup through the real canvas handlers.
  - `{ op: "erase", layer, points: [[x,y], ...], size }` selects base layer `layer` (0-based), switches to the eraser and drags through `points`. At least two points are required.
  - `{ op: "detail", type, color, width, stitch }` sets the Details settings and clicks the real detail button. Shirt mode only.
  - `{ op: "wordart", text?, font?, style?, size?, fill?, stroke?, grad?, strokeWidth?, angle? }` sets the Text-tab controls, waits for the vendored font, and clicks "Add to canvas". Omitted fields keep the `editor.html` defaults.
  - `{ op: "project", file }` loads a `.r15proj` through the real Open path.
- Template points are integers, and every value must fit its control's range: brush 1-50, detail width 1-8, stroke width 0-20, angle 0-360. Pen strokes are not frozen (approved divergence); the renderer refuses any op it does not know.
