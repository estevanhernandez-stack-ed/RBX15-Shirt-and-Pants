# Windows release test plan

Run this checklist on a clean Windows 10 or 11 VM before every tagged release.
Estimated time: 15 minutes.

## Prerequisites

- Windows 10 1809+ or Windows 11 VM with no prior RBX Maker install
- ~300 MB free disk for the installer + app
- A folder of PNG images for the asset-library test (any PNGs work, they don't need to be Roblox art)

## 1. Fresh NSIS install

- [ ] Double-click `ItsjustEste's RBX Classic Shirt and Pants Maker Setup.exe`
- [ ] SmartScreen warns → click "More info" → "Run anyway" (expected until code signing is set up)
- [ ] Installer wizard appears with the RBX Maker banner
- [ ] "Desktop shortcut" and "Start Menu shortcut" are checked by default
- [ ] Install directory picker defaults to `%LOCALAPPDATA%\Programs\ItsjustEste's RBX Classic Shirt and Pants Maker\`
- [ ] Install completes without errors
- [ ] App launches (either auto or via the Start Menu shortcut)

## 2. First launch

- [ ] Window opens at 1200x800, centered
- [ ] Menu bar is hidden
- [ ] Title bar reads "ItsjustEste's RBX Classic Shirt and Pants Maker"
- [ ] Header shows "RBX Classic Shirt & Pants Maker" with "ItsjustEste | v4.0" subtitle
- [ ] Ribbon tab reads "HOME" in brand cyan with a cyan→magenta gradient underline
- [ ] Canvas shows the 585×559 template grid with the navy/black checkerboard backdrop
- [ ] Footer reads "DEVELOPED AT 626 LABS LLC" in JetBrains Mono
- [ ] No console errors (DevTools should not auto-open)

## 3. Built-in asset packs

- [ ] Click **Drips** — asset browser expands, shows ~156 thumbnails
- [ ] Asset count shows "156 images in built-in drips pack"
- [ ] Click any thumbnail — layer added at center of front torso
- [ ] Drag a thumbnail to the canvas — layer lands where you drop it
- [ ] Click **Word art** — grid refreshes to 25 word-art PNGs
- [ ] Click any word-art thumbnail — layer added with correct name (not UUID)

## 4. Drop zone and canvas drop

- [ ] Drag a PNG from File Explorer onto the **drop zone** → layer added at center of front torso
- [ ] Drag a PNG from File Explorer onto the **canvas** at various positions → layer lands at the drop location
- [ ] Drag from the asset library grid onto the canvas → layer name is the file's name, not a UUID

## 5. Layer manipulation

- [ ] Select a layer by clicking it on the canvas → cyan dashed bounding box + corner handles + magenta rotation handle appear
- [ ] Drag corner handle → layer scales
- [ ] Drag rotation handle → layer rotates (readout in JetBrains Mono, magenta)
- [ ] Scroll wheel on canvas → selected layer scales
- [ ] Arrow keys nudge; Shift+arrow nudges by 10px
- [ ] `[` / `]` rotates 5° / 15° with Shift
- [ ] `R` resets rotation
- [ ] `Ctrl+D` duplicates
- [ ] `Delete` or `Backspace` removes
- [ ] `Ctrl+Z` undoes

## 6. Image adjustments

- [ ] Select a layer → scroll to bottom of left panel → click **Image adjustments** header
- [ ] Panel expands revealing brightness, contrast, saturation, hue-rotate, R/G/B sliders
- [ ] Each slider updates the canvas in real time
- [ ] **Reset adjustments** button returns all sliders to defaults
- [ ] Layer appearance on canvas returns to original

## 7. Word art generator

- [ ] Click the **TEXT** ribbon tab
- [ ] Type "RIZZ" → preview canvas renders text
- [ ] Change font → preview updates
- [ ] Click through style buttons (Solid, Gradient, Neon, Shadow, Outline, Retro) → preview reflects each
- [ ] Click **Add to canvas** → word art appears as a new layer

## 8. Placement presets

- [ ] Select a layer → click **Fit Front** → fits front torso region
- [ ] **F+B** → front + back duplicate
- [ ] **Stretch** → stretched across torso
- [ ] **Allover** → tiles across all regions
- [ ] **Scatter** → randomly placed copies

## 9. Clothing details

- [ ] Click **DETAILS** ribbon tab
- [ ] Adjust color, width, stitch toggle
- [ ] Click each generator (V-Neck, Crew, Scoop, Henley, cuffs, hem, seams, pocket, placket, etc.) → detail layer added to canvas

## 10. Shirt / Pants mode

- [ ] Click the **SHIRT** toggle → changes to **PANTS**
- [ ] Region grid in Layout tab updates to pants regions (waist, legs)
- [ ] Mode toggle gradient flips direction (magenta ↔ cyan)
- [ ] Click back to **PANTS** toggle → returns to **SHIRT**

## 11. Save / load project

- [ ] Type a template name in the Settings group input
- [ ] Click **Save work** → `.r15proj` file downloads
- [ ] Click **Clear** → canvas empties
- [ ] Click **Open** → pick the saved `.r15proj` → all layers, positions, adjustments, and region colors restore
- [ ] Layer names preserved (not UUIDs)

## 12. Export PNG

- [ ] Click **Download template** → 585x559 PNG downloads with the template name as filename
- [ ] Opened in any image viewer: matches the canvas exactly
- [ ] Image adjustments from step 6 are baked in (if active on any visible layer)

## 13. Background and region colors

- [ ] Click **LAYOUT** ribbon tab
- [ ] Click each BG swatch → canvas background changes
- [ ] Click **Background** button → pick a PNG → replaces solid color
- [ ] Click any template region button → color picker → region fills
- [ ] Right-click a region button → fill clears

## 14. Privacy / network

- [ ] With a network monitor (Fiddler, Wireshark, or just Task Manager → Performance → Open Resource Monitor → Network), confirm the app makes only **two** outbound connections:
  - [ ] `fonts.googleapis.com` (once per install, cached after)
  - [ ] `fonts.gstatic.com` (once per install, cached after)
- [ ] No other domains hit. No telemetry. No `claude.ai`, no `626labs.dev`, nothing.

## 15. Uninstall

- [ ] Settings → Apps & features → ItsjustEste's RBX Classic Shirt and Pants Maker → Uninstall
- [ ] Uninstaller runs to completion
- [ ] Desktop and Start Menu shortcuts removed
- [ ] Install directory cleaned up

---

## MSIX submission specific checks

Only needed for Store submission builds.

- [ ] `MakeAppx pack` exits 0
- [ ] Packed MSIX opens with Package Viewer and shows correct Identity, DisplayName, logos, and capabilities
- [ ] Self-signed MSIX installs without errors on a test machine (after the dev cert is trusted)
- [ ] Launched from Start Menu, app looks identical to the NSIS install
- [ ] Uninstalling via Settings → Apps removes the app cleanly
- [ ] Partner Center's [App Certification Kit](https://developer.microsoft.com/windows/downloads/app-certification-kit/) passes all test categories
