# Changelog

All notable changes to RBX15 Classic Shirt and Pants Maker are documented here. Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

**Quality bar + Microsoft Store prep.** Mirroring the Sanduhr shipping model — README and legal in place, design system done, MSIX packaging wired, Partner Center identity locked in.

### Added

- README.md full rewrite in 626 Labs voice; landing page at `docs/index.html`; screenshots, CHANGELOG, CONTRIBUTING, SECURITY, LICENSE (MIT), PRIVACY, FUNDING
- 626 Labs brand palette + voice pass across the editor (styles.css, editor.html, editor.js)
- 7 app screenshots wired into README + landing page
- Microsoft Store assets at `docs/store-assets/` (tiles + publisher logos + generator script)
- MSIX packaging scaffold at `windows/msix/` with manifest template, image generator, build script
- TEST_PLAN, RELEASE_NOTES, SIGNING runbook at `windows/`
- Built-in asset packs: 156 drips + 25 word-art PNGs under `packs/`, loadable via new Asset library buttons
- `.626labs/manifest.json` product manifest for 626labs.dev hub ingestion

### Changed

- **App rebranded** from *ItsjustEste's RBX Classic Shirt and Pants Maker* to **RBX15 Classic Shirt and Pants Maker** to match the Partner Center reservation. ItsjustEste credit preserved as author / header subtitle.
- MSIX manifest identity wired to Partner Center reservation (Name `626LabsLLC.RBX15ClassicShirtandPantsMaker`, Publisher CN `CN=177BCE59-0966-4975-9962-10E36652141F`, Store ID `9MV9G4XFJ8S0`).
- `package.json` license field: `ISC` → `MIT` (matches LICENSE file).
- `package.json` appId: `com.itsjusteste.rbx-maker` → `com.626labs.rbx15maker`.

### Up next

- Reserve final Partner Center listing copy + screenshots
- Run `windows/msix/make-msix.ps1 -Build` and upload to Partner Center
- Enable GitHub Pages on `/docs`
- Add product card to 626labs.dev hub
- Code signing for the NSIS installer (Azure Trusted Signing — see `windows/SIGNING.md`)

---

## [4.0.0] — 2026-04-02

**Polish release — monolith decomposition, image adjustments, redesigned layer panel, and canvas drop positioning.**

The app shipped as a 2,684-line single HTML file in v3. This release breaks it apart cleanly, rebrands from the old "Conundrum by Este" codename to the final product name, and adds real image-editing capability.

### Added

- **Image adjustments — non-destructive, per layer.** Brightness, contrast, saturation, hue rotate, plus per-channel RGB shift. Collapsible panel in the Selected Layer pane with a one-click reset. Adjustments render via `ctx.filter` (CSS filters) for the first four and `ImageData` pixel manipulation for RGB channels. Baked into the exported PNG.
- **Canvas drop positioning.** Dragging an image onto the canvas now lands it where you drop it, not at a default spot. Drop coordinates are captured from `e.clientX` / `e.clientY`, converted to canvas space (zoom-aware), and passed through to `addLayer(img, name, x, y)`. Click-to-add and drop-zone behavior preserved (still centers on front torso when no coords provided).
- **Asset browser drag-to-canvas.** Dragging a thumbnail from the asset library to the canvas preserves the original file name. Intercepts `dragstart` on the thumbnail and stashes the original `File` reference so the canvas drop handler can use it instead of the browser-synthesized blob (which had a UUID-style name).
- **Redesigned layer panel.** 48×48 thumbnails (up from 32×32), name top-right, labeled `Dupe` / `Hide` / `Del` buttons below the name. Color-coded: cyan for duplicate, neutral for hide, red for delete. Old tiny emoji buttons retired.
- **Evolved UI depth.** Panel gradients replace flat backgrounds, buttons gain subtle shadows and hover glows, ribbon group labels bumped from 8px to 10px/#888/bold for readability.
- **Footer.** "Developed at 626 Labs LLC" in a slim bar at the bottom.
- **`Save Work` / `Download Template` button clarity.** Renamed from the ambiguous "Save" / "Export PNG" to be obvious to younger users. Icons added (floppy disk / download arrow).
- **Design spec and implementation plan** committed to `docs/superpowers/specs/` and `docs/superpowers/plans/`.

### Changed

- **Decomposed the monolith.** `shirt_editor.html` (2,684 lines) split into `editor.html` (markup), `styles.css` (CSS), and `editor.js` (JS). No build step, no module system — just "CSS in the CSS file, HTML in the HTML file, JS in the JS file."
- **Rebranded.** Window title, header, HTML title, and `package.json` branding all updated from "Conundrum by Este / R15 Template Designer" to "ItsjustEste's RBX Classic Shirt and Pants Maker." `appId` changed to `com.itsjusteste.rbx-maker`.
- **Project save format bumped to 4.0.** Adds brightness, contrast, saturate, hueRotate, rShift, gShift, bShift per layer. Null-safe defaults on load — v3 `.r15proj` files open without modification.
- **Default `main.js`** now loads `editor.html` instead of `shirt_editor.html`. The original is preserved at `backup/shirt_editor_v3_backup.html` for reference.

### Fixed

- **Asset browser dragged the wrong file name.** When dragging a thumbnail from the asset library to the canvas, the browser wraps it in a synthetic blob with a UUID name. The canvas drop handler was using that name instead of the original. Now intercepts `dragstart` on the thumbnail, stores the `File` reference + display name on module-scoped variables, and the canvas drop handler uses those if present.
- **Encoded UUIDs shown as layer names from the asset browser.** Same root cause as above; the layer-item display name now comes from the original file name via `displayName`, not `file.name` of the drag payload.
- **Ribbon group labels were unreadable.** 8px `#555` on the ribbon was too washed out. Now 10px `#888` bold with `+0.5px` tracking.
- **DevTools no longer opens on startup.** The `openDevTools()` call was left on during the drop-name debug session. Reverted.

### Removed

- **`makeCheckerboard()` per-frame allocation.** The 16×16 checkerboard canvas was being rebuilt on every render. Now built once at module load and reused.
- **Per-frame offscreen canvas allocation.** `render()` was allocating a fresh 585×559 canvas on every call — meaningful cost during mouse-drag at 60fps. Now reuses a persistent `_offCanvas`.
- **Per-frame RGB shift pixel pass.** `applyRGBShift()` was running the full pixel loop every render for every layer with adjustments. Now keyed-cached — only recomputes when layer ID or RGB shift values change.

### Tests

- Manual smoke test: drop image, move / rotate / resize layer, switch Shirt / Pants mode, word art, save + load project, export PNG, undo, image adjustments in real time, drag from asset browser at various zoom levels.
- Backward compat: opened a v3 `.r15proj` file and confirmed layers restore with default adjustment values.

---

## [3.0] — pre-2026-04-02

**Pre-decomposition state. "Conundrum by Este" branding.** The 2,684-line single-file HTML that ran in Electron. Feature-complete but unmaintainable at that size.

### Included

- Electron shell + single HTML/CSS/JS file (`shirt_editor.html`)
- Layer-based editor with on-canvas transform handles
- Drawing tools (pen, line, rectangle, ellipse, eraser)
- Word art generator with 15 Google Fonts and 6 styles
- Clothing detail generators (collars, cuffs, hems, seams, pockets, plackets)
- Placement presets (Fit Front, F+B, Stretch, Allover, Scatter) and layout presets (Meme Tee, Mirror, Fullbleed, Sleeve Pop)
- Asset library with folder browser and subfolder grouping
- Region mask overlay, grid overlay, zoom controls
- Save / load `.r15proj` JSON workspace files
- Export PNG at the 585×559 R15 template size
- 30-step undo stack
- Shirt / Pants mode toggle with per-mode region data
- Python helper scripts (`roblox_shirt_maker.py`, `roblox_uploader.py`, `variation_generator.py`) — compositing and Roblox API utilities, kept for CLI workflows

---

## [1.0.0] — initial release

- First Electron build + NSIS installer config (`dist/R15 Template Designer Setup 1.0.0.exe`)
- Package name `conundrum-r15-designer`, appId `com.conundrum.r15`

---

[Unreleased]: https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/compare/v4.0.0...HEAD
[4.0.0]: https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/releases/tag/v4.0.0
