# RBX15 Maker — smoke test

Fast confidence pass. Run it before every merge and before every tagged
release. ~5 minutes. For the exhaustive 15-minute install/uninstall/network
sweep, use [`windows/TEST_PLAN.md`](../windows/TEST_PLAN.md) instead — this
is the quick "is it alive and did we break anything" check.

**How to run:** `npm start` (dev) or launch the installed build. Open DevTools
(Ctrl+Shift+I) and keep the Console visible — a clean console is part of the
pass. Green means the app loaded with no uncaught errors.

Automated floor first: `npm run lint && npm test` must be green (17+ tests).
That covers the pure logic (history stacks, erase math, injection invariant,
parse). Everything below is the canvas/DOM behavior that only a human can see.

---

## Part A — the revival-run fixes (PRs #25–#28)

These four were broken or dangerous in the shipped v4.0.1. Test them first —
they're the whole point of the revival run.

### A1. Asset click-to-add (was: dead — ReferenceError) · PR #25

- [ ] Home tab → **Drips** → the asset browser expands with thumbnails
- [ ] **Click** a thumbnail → a layer appears on the front torso
- [ ] Console shows **no** `clickName is not defined` error
- [ ] Drag a thumbnail onto the canvas → it lands where you drop it, name preserved

### A2. Eraser (was: complete no-op) · PR #26

- [ ] Add any image layer, keep it selected
- [ ] Press **E** (or click Eraser), drag across the layer
- [ ] Live preview shows a half-transparent magenta stroke while dragging
- [ ] On release, the painted pixels are **gone** from that layer
- [ ] Rotate the layer ~30°, erase again → the hole lands under the cursor, not offset
- [ ] **Ctrl+Z** restores the erased pixels
- [ ] With no layer selected, pressing E and dragging → prompt: "Select a layer to erase from"

### A3. Layer-name injection (was: code-execution sink) · PR #27

- [ ] Add a layer, select it, rename it (Selected layer → Name) to:
      `"><img src=x onerror=alert(1)>`
- [ ] Press Tab / click away → **no** alert dialog fires
- [ ] The name renders as literal text in the field and the layer list
- [ ] Console clean

### A4. Undo / redo (was: no redo; undo missed bg/region/mode) · PR #28

- [ ] **Redo** button is present in the Home → Edit group, next to Undo
- [ ] On fresh load, Undo and Redo are both greyed out (disabled)
- [ ] Add a layer → Undo enables. Click **Undo** → layer gone, **Redo** enables
- [ ] Click **Redo** → layer returns
- [ ] Layout tab → click a **BG swatch** → **Undo** reverts the background color
- [ ] Fill a **region** color (click a region button with nothing selected, pick a color) → **Undo** clears it
- [ ] Switch **Shirt → Pants**, then **Undo** → returns to Shirt *with region colors intact* (the old build wiped them for good)
- [ ] **Ctrl+Y** and **Ctrl+Shift+Z** both redo
- [ ] Click **Clear**, then **Ctrl+Z** → the cleared layers come back (undo works with nothing selected)

---

## Part B — core smoke (did we break the happy path?)

### B1. Load & first paint
- [ ] Window opens, header + ribbon + 585×559 canvas render
- [ ] Console clean, DevTools didn't auto-open on the packaged build

### B2. Layer basics
- [ ] Select a layer → cyan bounding box + corner handles + magenta rotation handle
- [ ] Drag to move, corner-drag to scale, rotation-handle to rotate
- [ ] Scroll wheel scales; arrow keys nudge; `[` `]` rotate; `Delete` removes; `Ctrl+D` duplicates

### B3. Adjustments
- [ ] Selected layer → Image adjustments → each slider (brightness/contrast/saturation/hue/R/G/B) moves the canvas live
- [ ] **Reset adjustments** returns to original

### B4. Word art
- [ ] Text tab → type text → preview renders → cycle the six styles → **Add to canvas** drops a layer

### B5. Details & placement
- [ ] Details tab → a couple of generators (V-Neck, Hem, Pocket) add detail layers
- [ ] Home → placement (Fit Front, F+B, Allover) reposition the selected layer

### B6. Save / load / export
- [ ] **Save work** → `.r15proj` downloads
- [ ] **Clear**, then **Open** the file → layers, positions, adjustments, region colors, and mode all restore
- [ ] **Download template** → 585×559 PNG; opened externally it matches the canvas (adjustments baked in)

### B7. Mode
- [ ] **Shirt → Pants** flips the toggle gradient and swaps the region grid to waist/legs

---

## Result

- Date / build: ___________
- Tester: ___________
- Part A (fixes): ___ / 4 groups pass
- Part B (core): ___ / 7 groups pass
- Console clean: ☐ yes ☐ no
- Blocking issues: ___________

A release ships only when Part A is 4/4 and Part B has no regressions.
