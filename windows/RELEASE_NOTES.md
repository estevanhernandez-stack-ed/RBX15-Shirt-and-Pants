# Release notes

## v4.1.0 — 2026-07-21

**The revival release.** Two advertised features that were broken in v4.0 now work, a big new creative capability lands, and the project gets its first automated test suite.

### New: pattern & gradient region fills

Region fills go far beyond flat color. A new **Fill Style** group in the Layout tab lets you paint any template region with:

- **Solid**, **linear gradient**, **radial gradient**, **stripes**, **checker**, **dots**, or **camo** — set the type, two colors, an angle, and a pattern scale.
- **Patterns that wrap.** The same fill on connecting faces tiles continuously across the seams, so a pattern reads as one fabric around the body instead of restarting at every panel.
- **Gradients that span.** Paint a gradient across connected regions and they share one continuous ramp across the whole group.
- **Live editing.** Change the angle or colors after painting and the already-painted regions update in place.

What you see on the canvas is exactly what the exported PNG contains.

### Fixed (things that were broken in v4.0)

- **Asset-browser click-to-add** — clicking a thumbnail threw an error and added nothing. Fixed.
- **Eraser tool** — did nothing at all. Now erases from the selected layer, with a brush-ring cursor.
- **Undo** — only covered layers. Now covers region fills, background, and Shirt/Pants mode too, and there's a proper **Redo** (`Ctrl+Y` / `Ctrl+Shift+Z`).
- **Security** — a crafted layer name (e.g. from a downloaded asset pack) could execute code. Hardened.
- **`Ctrl+R`** no longer quietly resets a layer's rotation.

### Also new

- Themed scrollbars, tool toggle-off (re-click a tool to return to Select), and keyboard focus rings for accessibility.
- Guards on dead-end paths (empty-folder pick, empty-template export).
- First automated test suite — 31 unit tests plus a lint gate, wired into Windows CI.

### Install

**Windows 10 or 11.** Download `RBX15 Classic Shirt and Pants Maker Setup 4.1.0.exe` from the assets below. SmartScreen will warn about the unknown publisher — click "More info" → "Run anyway" (code signing is still pending). The `.msix` is for the Microsoft Store pipeline.

Or build from source:

```bash
git clone https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants.git
cd RBX15-Shirt-and-Pants
npm install
npm run build
```

### Upgrading

The `.r15proj` format is backward compatible — older project files (including flat region colors) open unchanged.

### Known limitations

- **No code signing yet** — SmartScreen warns on first run.
- **Word-art fonts load from Google Fonts** on first launch (cached after). No telemetry or analytics of any kind; bundling the fonts for full offline use is the top roadmap item.
- **Canvas UI isn't fully keyboard/screen-reader accessible** yet — focus rings landed this release; deeper access is on the roadmap.

### Thanks

Built by Este at [626 Labs LLC](https://626labs.dev). Imagine something else.
