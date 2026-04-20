# Built-in asset packs

Bundled PNGs that ship with the app. The renderer picks them up at runtime via the **Built-in packs** buttons in the left panel of the editor (Drips / Word art), so end users get a useful starter library on first launch.

## Packs

| Pack | Count | Purpose |
|---|---|---|
| [`drips/`](drips/) | 156 PNGs | Blood and liquid drip overlays — front, back, sleeve, and pants variants at multiple colors and flipped orientations |
| [`word_art/`](word_art/) | 25 PNGs | Pre-rendered text treatments (based, bruh, bussin, conundrum vibes, etc.) using the in-app word art generator's output |

## How they're loaded

**Dev mode (`npm start`):** the renderer resolves `packs/<name>/` relative to `__dirname` (the repo root).

**Packaged (MSIX / NSIS):** electron-builder's `extraResources` entry in [`package.json`](../package.json) copies the `packs/` folder into `resources/` alongside `app.asar`. The renderer falls back to `process.resourcesPath/packs/<name>/` when the dev path doesn't exist.

## Adding a new pack

1. Drop your PNGs into `packs/your-pack-name/`.
2. Add a button in [`editor.html`](../editor.html) inside the "Built-in packs" row.
3. Wire the click handler in [`editor.js`](../editor.js) to call `loadBuiltInPack('your-pack-name')`.
4. `electron-builder` picks the folder up automatically via the wildcard `extraResources` filter — no config changes required.

Keep packs under a few MB each. Large packs bloat the installer and slow first-launch file enumeration.

## Licensing

Only bundle assets you created yourself or have explicit redistribution rights for. These ship inside a Microsoft Store submission and become part of a commercial product.
