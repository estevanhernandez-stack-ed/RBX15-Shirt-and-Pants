# Release notes

## v4.0.0 — 2026-04-20

**First production-ready release.** Full polish pass, rebrand, 626 Labs design system, Microsoft Store submission prep, and two built-in asset packs.

### What you get

- **Layer-based editor** for Roblox Classic R15 shirt and pants templates. Drag to reorder, on-canvas move / scale / rotate, non-destructive image adjustments.
- **Built-in asset packs** — 156 drip overlays and 25 word-art PNGs ship with the installer. One-click load from the Asset library panel.
- **Word art generator** — 15 Google Fonts, six styles (solid, gradient, neon, shadow, outline, retro), live preview.
- **Clothing details** — auto-generate collars (V-neck, crew, scoop, henley), cuffs, hems, seams, pockets, plackets, shoulder stripes, arm bands, yokes.
- **Canvas drop positioning** — drag an image onto the canvas at any point, it lands where you drop it.
- **Save / load** `.r15proj` workspaces with full state (layers, adjustments, positions, region colors, backgrounds).
- **No network, no accounts, no cloud.** Everything local. See [PRIVACY.md](../docs/PRIVACY.md).

### What's new vs v3

- **Monolith decomposed** — the 2,684-line single HTML file is now `editor.html` + `styles.css` + `editor.js`.
- **626 Labs brand palette** — cyberpunk purple / cyan swapped for brand navy + signature cyan `#17d4fa` / magenta `#f22f89` duo.
- **Voice pass** — sentence-case buttons, period-terminated microcopy, no emoji / unicode dingbats in UI.
- **Non-destructive image adjustments** — brightness, contrast, saturation, hue rotate, plus per-channel RGB shift. Every slider renders in real time. Baked into the exported PNG.
- **Redesigned layer panel** — 48×48 thumbnails, labeled Dupe / Hide / Del buttons. Retired the tiny emoji buttons.
- **Landing page** at [docs/index.html](../docs/index.html) in 626 Labs brand.
- **Store assets** — Microsoft Store tiles + publisher logos in [docs/store-assets/](../docs/store-assets/).
- **MSIX packaging scaffold** in [windows/msix/](msix/) for Partner Center submission.

### Install

**Windows 10 or 11.** Download `RBX15 Classic Shirt and Pants Maker Setup.exe` from [the latest release](https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/releases). SmartScreen will warn about the unknown publisher — click "More info" → "Run anyway" until Microsoft Store approval lands (in review).

Or build from source:

```bash
git clone https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants.git
cd RBX15-Shirt-and-Pants
npm install
npm run build
```

### Upgrading from v3

The `.r15proj` format is backward compatible. v3 project files open in v4 with default adjustment values applied. No migration needed.

### Known limitations

- **No code signing yet.** Windows SmartScreen warns on first run. Microsoft Store submission in review — approval eliminates the prompt.
- **No `winget install`** yet — blocked on MS Store certificate.
- **No accessibility pass yet.** Canvas-heavy UI isn't screen-reader-friendly; tracked on the roadmap.
- **No historical versions browser** — you can only open one `.r15proj` at a time. Saved files are plain JSON if you want to script that yourself.

### What's next

Tracked in [README.md Roadmap](../README.md#roadmap):

- Microsoft Store MSIX approval and listing
- Code signing for the Windows binary
- `winget install rbx-maker` once the Store cert lands
- Accessibility pass — keyboard navigation for the canvas, screen-reader-friendly controls
- Historical versions browser — open any `.r15proj` as a starting point
- Possible `.rbxm` export (Roblox Studio model format) for streaming templates directly in

### Thanks

Built by Este at [626 Labs LLC](https://626labs.dev). Imagine something else.
