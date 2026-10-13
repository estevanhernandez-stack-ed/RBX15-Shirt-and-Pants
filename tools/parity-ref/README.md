# tools/parity-ref

Renders parity scenes through shipped 4.1 code, lifted verbatim out of `editor.js` by `extract.js`, in Electron with GPU raster off. The output is the frozen golden set the native v5 engine is held to (spec section 6).

## Rules

- Goldens in `tests/goldens/` are made **only** by this tool, run by a person. CI never regenerates them.
- They are frozen from 4.1. Regenerating them after `editor.js` changes is wrong unless the change is a 4.x release the native port must also match.
- A deliberate v5 divergence with a visual effect gets a **new** golden next to the 4.1 one, plus a note in `docs/port/divergences.md`. The 4.1 golden is never overwritten.
- This directory is deleted at cutover. The goldens stay.

## Use

    node --test "tools/parity-ref/*.test.js"         # the tool's own tests
    env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/render.js \
      --scenes tools/parity-ref/scenes.json --out tests/goldens/render [--only name1,name2]

`index.json` records, for each golden, its SHA-256 plus the Electron and Chrome versions and the git blob hashes of `editor.js` and `lib/fills.js` that produced it. A run with `--only` merges into the existing index and never drops other goldens' records.

## Scene format

`{ name, mode?, bgColor?, bgOpacity?, bgImage?, regionColors?, layers?: [{ image, x?, y?, scale?, rotation?, flipH?, flipV?, opacity?, visible?, brightness?, contrast?, saturate?, hueRotate?, rShift?, gShift?, bShift? }] }`. Image paths are repo-relative. Defaults match `makeLayer` (`editor.js:134-142`) and the editor's initial state. The output is always the export composition, so the mask is always applied.

Word Art, details, eraser and project round-trip scenes are added at the start of M2, before cutover.
