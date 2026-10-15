# RBX15 v5.0: native port design

**Status:** approved section by section, 2026-10-11. **Issue:** #38. **Dashboard project:** RBX15 Classic Shirt and Pants Maker.
**Appendices:** [`docs/port/divergences.md`](../../port/divergences.md) (every deliberate behavior change), [`docs/port/parity-ledger.md`](../../port/parity-ledger.md) (407 rows, each with an owner, a check and a status; source data in `parity-ledger.json`).

## Goal

Ship RBX15 as a native Windows app, WPF on .NET 10 with SkiaSharp, replacing the Electron 4.1 build in the same Microsoft Store listing. v5.0 matches 4.1 feature for feature, carries four named upgrades, and fixes 4.1's unintended behavior, with each fix named in the divergence table. **Nothing in 4.1 is dropped by accident:** a 407-row parity ledger built from a full inventory of the app decides the ship.

### Why native

The spike (#38, 2026-10-11) ported `lib/fills.js` and `paintRegions` to SkiaSharp and found zero interior pixel differences across 10 fixtures. Against Electron with CPU raster, the worst channel delta was 3. Measured against Electron 4.1:

| | Electron 4.1 | WPF + Skia |
|---|---|---|
| Store package | 141 MB | ~66 MB (self-contained, compressed) |
| Processes / private memory | 4 / 239 MB | 1 / 75 MB |
| Launch to first frame (warm) | 1.18 s | 0.96 s |

Also gained:

- **Deterministic export.** Electron's GPU canvas changes the antialiased edges in exported PNGs from machine to machine.
- **Zero network requests,** with fonts bundled.
- **No more `nodeIntegration`.**
- **The Store copy's "native" claim becomes true.**

### Scope

- **Parity** with 4.1 under the bug policy below.
- **Upgrades:**
  - `.r15proj` file association
  - autosave with crash recovery
  - recent projects and the jump list
  - Ctrl+V paste as a layer
- **Free fixes** that come with the port:
  - bundled fonts
  - zero network requests
  - deterministic export
- **The Pants-mode fix.** In 4.1, Pants mode cannot add images, apply placements, presets or details, or add Word Art. `editor.js:502` reads `SHIRT_REGIONS.torso_front`, which doesn't exist in Pants mode, and throws. No 4.x hotfix (Este, 2026-10-11).
- **Out of scope for 5.0:**
  - pants-specific details (waistband, belt loops)
  - SVG import
  - an alpha-aware hit test
  - renaming "Mirror F+B"
  - code signing for a GitHub installer

### Bug policy

**Fix anything clearly unintended. Keep anything a user could reasonably rely on.** Examples of the kept kind: undo depth 30, export size and naming, layer geometry, button labels, the box-based hit test, and Scatter's randomness. Every fix is a row in `divergences.md`, so no change happens silently.

### What the ledger adds beyond these sections

Classifying the 407 rows under this policy produced 197 keep, 183 fix, 26 drop and 1 new. Most fixes are the decisions below, applied row by row. Five were inferred under "clearly unintended" rather than decided in a section. Este approved all five on 2026-10-12:

- The Word Art size is enforced to the 8 to 300 range the input already declares.
- Opening a project clears undo history; 4.1 leaks the previous project's history into the new one.
- Fill swatch previews use the canvas angle convention, so they match the render.
- The RGB-shift cache is per layer and is invalidated on pixel edits; 4.1 can show stale pixels.
- The Word Art preview is centered.

## 1. Architecture

A headless engine plus an MVVM shell, side by side with Electron in this repo until cutover.

```text
src/
  RBX15.slnx, global.json (SDK 10.0.x pin)
  RBX15.Engine/         net10.0, SkiaSharp only. No WPF, no ambient clock or randomness.
  RBX15.App/            net10.0-windows WPF, CommunityToolkit.Mvvm, Package.appxmanifest
  RBX15.Engine.Tests/   xUnit: unit tests and golden-image tests
  RBX15.Parity/         golden diff runner, heatmaps, ledger checks
tools/parity-ref/       Node + Electron scripts that render scenes with shipped 4.1 code (deleted at cutover)
scripts/build-msix.ps1  RoRoRo/Sanduhr pattern; also produces the portable zip
```

**Rules:**

- **The engine never references WPF.** The app never draws pixels itself; it shows engine frames in a `WriteableBitmap` (proven in the spike).
- **Every ledger row has exactly one owning engine module or view-model.**
- **The Electron files stay untouched at the repo root until the cutover PR.**

## 2. The engine

| Module | Owns | Decisions |
|---|---|---|
| Template | the 18-region shirt and pants tables (key order preserved), the mask | Region keys stay verbatim, because projects store them. **Semantic slots** (front, back, right arm, left arm, up, down) are resolved per mode. This is the Pants fix. |
| Document | an immutable record: mode, background color/image/opacity, region fills, layers, mask and grid flags | Layers hold shared, immutable decoded bitmaps. No pixels are re-encoded per undo snapshot; 4.1 embeds a PNG data URL for every layer in every snapshot. |
| Render | `Render(SKCanvas, Document, RenderOptions)` | The order is a contract: clear, background (stretched image or color, times opacity), region fills, visible layers bottom to top, live stroke preview (half alpha for the eraser), mask. View overlays: checkerboard, grid, selection box, handles, rotation label. |
| Fills | the spike port | `SKPathBuilder` (SkiaSharp 4 deprecates `SKPath` mutation). Camo's negative-modulo tone index is carried verbatim; it can't change pixels within the UI's 4 to 48 scale range. |
| Adjust | brightness, contrast, saturate and hue-rotate as **chained per-stage color filters**; RGB shift | Chromium clamps between filter stages, so one multiplied matrix would drift. RGB shift runs on un-premultiplied pixels and skips alpha 0 (divergence: more precise on low-alpha pixels). |
| WordArt | a pure `Render(settings) -> bitmap + size`: 6 styles, padding rules, multi-line, the "middle" baseline | Fonts are bundled. Weight is always 900, synthesized where a family lacks it, as Chromium does. Neon's blur sigma is tuned against goldens. |
| Details | the 16 generators | Geometry copied verbatim. Driven by slots, so they work in Pants mode. |
| Tools | pen, line, rect and ellipse geometry; autocrop with 10 px padding; eraser inverse transform (`erase.js`) | **Pen strokes are drawn once at mouseup** (4.1 re-strokes on every move, so edges depend on mouse speed). **A press with no movement draws a dot the size of the brush.** |
| History | 30-step undo, **synchronous** restore, capped redo | Every document change is exactly one step (see section 5). |
| ProjectIO | `.r15proj` read and write | See section 3. |
| Export | 585x559 PNG | Same render as the view, minus overlays, with the **mask always applied** (4.1 behavior, regardless of the Mask checkbox). Bilinear layer sampling, matching Canvas2D's default. |

## 3. Document, files and local storage

**Project files (`.r15proj`):**

- **Reading:** reads every 4.1 file, whether a fill is a legacy color string or a descriptor.
- **Writing:** writes the same fields with `version: "5.0"`; 4.1 never reads `version`, so v5 files still open in 4.1.
- **Load:** all-or-nothing. Parse and validate fully before changing any state. Image fields must be `data:` URLs; `http(s)` and `file:` are refused, which makes `SECURITY.md`'s "we sanitize on load" claim true. A layer that fails to decode is reported by name.
- **Saving:** there is a **current file**. Ctrl+S saves to it and Ctrl+Shift+S is Save As. Native dialogs, last folder remembered.

**Export:** native Save dialog, `<sanitized title or roblox_shirt>_roblox.png`, and the "template is empty" confirmation kept.

**Import:**

- **Formats:** PNG, JPEG, WebP, BMP and GIF (first frame).
- **SVG is dropped,** with a message.
- **Order:** multiple files are added in the order picked, as **one** undo step.
- **The stale asset-drag hijack is fixed.**

**Asset library:**

- **Packs:** both built-in packs (156 drips, 25 Word Art) ship inside the package, sorted ordinal-ignore-case.
- **Folders:** native folder picker. Name-only dedupe keeps the deepest copy (kept on purpose).
- **Thumbnails** decode lazily at thumbnail size.
- **The "duplicates removed" count is shown.** In 4.1 it is overwritten in the same tick.

**Fonts and network:**

- **Bundled:** the 15 Word Art families and the 3 UI families, with their OFL licenses.
- **Network:** zero requests.
- **Docs:** `PRIVACY.md` and `SECURITY.md` are corrected. Today they contradict each other and the code: they omit the UI fonts and claim "once per install".

**Upgrades.** All data lives in the package's local app data, which uninstall removes; `PRIVACY.md` discloses the recents list, the autosave file, the saved window position, and a local **error log**. The log records unhandled exceptions only, never document content, rolls over, and is never sent anywhere. It replaces DevTools as the smoke-test diagnostic.

| Upgrade | Behavior |
|---|---|
| Autosave + recovery | Every 60 s while there are unsaved changes, written atomically (temp file, then rename). A clean exit deletes it; the next launch after a crash offers to restore. Adds a Save / Don't save / Cancel prompt on close. |
| Recent projects | Last 10, missing files pruned. Also on the taskbar jump list. |
| File association | `.r15proj` declared in the manifest. Opening a second project starts a new window. |
| Paste as layer | Ctrl+V reads the clipboard's PNG format first (keeps alpha), then other bitmaps. Centered on the front slot. |

## 4. The UI shell

**Window and layout:**

- **Window:** 1200x800 default; remembers size, position and maximized state.
- **Layout:** 4.1's, unchanged: header, ribbon tabs (Home, Layout, Details, Text), left panel (asset library, drop zone, layers, selected-layer properties and adjustments; editor.html:288-341), center canvas. 4.1 has no right panel; `.rpanel` in styles.css is dead (corrected 2026-10-14, M3a amendment CA2).
- **Ribbon groups:** never shrink; the active tab scrolls horizontally when narrow.
- **Header version:** read from the assembly, not hand-typed.

**Theme:**

- **One `ResourceDictionary`** of 4.1's actual values: navy/cyan/magenta, the ink scale, radii 4/6/10/14, motion 120/220 ms.
- **Button styles:** primary, secondary, active and disabled.
- **Scrollbars:** thin, matching what Electron actually renders (thumb `#223a54` on track `#050c18`).
- **Display scaling:** per-monitor DPI v2.

**View-models and commands:**

- **View-models:** Home, Layout, Details, Text, Layers, LayerProperties, AssetLibrary and Canvas, all over one `EditorSession` (engine document, history, current file, unsaved-changes flag).
- **Commands:** every button is an `ICommand` that knows when it can run.
- **Placement and Presets groups are hidden** until a layer is selected. In 4.1 they show enabled and silently do nothing.

**Canvas:**

- **Host:** shows the engine frame at the current zoom, pixel-snapped, nearest-neighbor at 100% and above.
- **Zoom:** the 25 to 300% pill in 0.25 steps stays. **Scrollbars pan when zoomed in**; 4.1 clips the canvas.
- **Wheel:** scales the layer, or the brush size in draw tools.

**Dialogs and status:**

- **Dialogs:** 4.1's six blocking `alert`/`confirm` calls become themed dialogs titled with the product name.
- **Status strip:** a new non-blocking strip shows failures 4.1 only wrote to the developer console.

**Keyboard:**

- **Every 4.1 shortcut keeps its key.**
- **Fixes:** sliders and checkboxes no longer trap shortcuts (only text fields do); Ctrl+letter no longer toggles tools; `[` rotation wraps correctly.
- **New:** Ctrl+O (open), Ctrl+S, Ctrl+Shift+S, Ctrl+V.
- **Keys OFF** disables only bare-key shortcuts; Ctrl combos always work.

**Accessibility:** region tiles, layer rows and swatches are keyboard-reachable; every control has an automation name; the cyan 2 px focus ring is kept. Electron's hidden accelerators (Ctrl+R, Ctrl+Shift+I) are gone.

## 5. Input, tools and the layers panel

**Mouse:**

- **Left button only** on the canvas.
- **Mouse capture** keeps drags going past the canvas edge (no phantom strokes).
- **Hover cursors** for move, resize and rotate.
- **Hit test stays box-based** (kept).
- **Rotation** normalized to -180..180; hidden layers show no handles.

**Undo:**

- **Every document change is exactly one step:** move, resize, opacity, flip, visibility and reorder become undoable.
- **No empty or double steps.**
- **Depth stays 30, and selection survives undo** when the layer still exists.

**Tools:**

- **Shapes:** 4.1 geometry, with the pen changes from section 2.
- **Eraser:** refuses on mousedown with a status message when no layer is selected.
- **Wheel scaling:** centered, with symmetric steps.
- **Scale clamp:** one range, **1 to 500%**, for the slider, keys, wheel and handles; presets compute exact fits.

**Placement, presets and details:** geometry and labels verbatim; they work in Pants mode through slots. Scatter takes an injectable random source so tests are reproducible.

**Layers panel:**

- **Keyboard-operable list** with drag reorder; the stale drop target is fixed.
- **Fixed displays:** an adjustment of 0 shows as 0; the adjustments section stays open while editing; the rotation label rounds; default layer names match the layer ID.

## 6. Parity: how it is proven

**Tier 1: xUnit.**

- **Ported:** the six Node suites (fills, erase, history, injection becoming load sanitization, smoke).
- **New:** a test per module and per behavioral divergence.

**Tier 2: golden images.**

- **Reference:** `tools/parity-ref` renders defined scenes with **shipped 4.1 code** in Electron with CPU raster, and commits the PNGs as **frozen goldens** before cutover. After cutover there is no 4.1 to compare against, so the goldens are the record.
- **Scenes:** every fill type and group; adjustments at their extremes and in combination; transforms, opacity and RGB shift; erase vectors; all 16 details; the deterministic presets; Word Art (6 styles across several fonts); full exports.
- **Extra goldens:** the 25 Word Art pack PNGs.
- **Pass rule:** **zero interior differences** (pixels whose reference 3x3 neighborhood is a single flat color) and **edge deltas of 3 or less**. Word Art uses a perceptual threshold, because glyph placement differs between Skia and Chromium.
- **Visual divergences:** each gets a new golden plus a note in `divergences.md`. Goldens change only on purpose.
- **Projects:** 4.1 `.r15proj` samples must load and render to their 4.1 render.

**Tier 3: traceability.**

- **Parity ledger:** `docs/port/parity-ledger.md` is generated from `parity-ledger.json`. **v5 does not ship with an unchecked row.**
- **UI automation:** a thin suite covering launch, add image, region paint, Word Art, undo, save and reopen, and export.
- **Manual:** `docs/SMOKE_TEST.md` rewritten for v5.

**CI on every PR:** Tier 1, the Tier 2 diffs (no Node or Electron needed), and a ledger orphan check. Heatmaps of failing diffs upload as build artifacts.

## 7. Shipping, cutover and phasing

**Package:**

- **Identity:** kept exactly: `Name="626LabsLLC.RBX15ClassicShirtandPantsMaker"`, `Publisher="CN=177BCE59-0966-4975-9962-10E36652141F"`, Store ID `9MV9G4XFJ8S0`, version `5.0.0.0`. Existing installs update in place.
- **Manifest:** `runFullTrust` only, no `internetClient`; `.r15proj` file association.
- **Build:** `scripts/build-msix.ps1` produces the MSIX and an unsigned self-contained portable zip.
- **Release:** `release.yml` attaches both on a `v5.*` tag, with real notes (`/cut-release`).
- **Retired:** the NSIS channel.

**Store 5.0:**

- **Listing copy:** a What's new; the "native" claim is now accurate.
- **Docs:** privacy and security corrected.
- **Reviewer letter:** carries the `runFullTrust` justification.
- **Screenshots:** reshot only if the look changes.
- **Quit before updating:** a line in the README and the What's new.
- **Submission:** `/store-api rbx15`, then Este's Packages and Notes pages, then `/store-commit rbx15`.
- **Last 4.x:** a GitHub note points NSIS users to the Store or the zip.

**Cutover PR (after 5.0 certifies):**

- **Deletes** the Electron app, `package.json`, the Node tests and `tools/parity-ref`.
- **Keeps** the goldens.
- **Rewrites** README, CONTRIBUTING, SMOKE_TEST, TEST_PLAN and the repo keystone.

| # | Milestone | Done when |
|---|---|---|
| M0 | `roblox_uploader.py` and its manifests move to a private repo (own PR). `src/` skeleton, `global.json`, CI. `tools/parity-ref`; goldens frozen from 4.1; ledger committed. | CI green on an empty engine; goldens committed |
| M1 | Engine render core: Template, Document, Render, Fills, Adjust, Export, plus a headless export CLI | render goldens pass |
| M2 | Tools, eraser, History, ProjectIO, Details, WordArt with bundled fonts | all engine goldens and 4.1 project round-trips pass |
| M3 | Shell: theme, layout, ribbon, canvas host, layers panel and properties; select and draw tools wired | basic edit and export works in the app |
| M4 | File I/O, assets and packs, the Layout, Details and Text tabs, keyboard, dialogs and status strip | every non-upgrade ledger row checked |
| M5 | Autosave and recovery, recents and jump list, file association, paste | upgrade rows checked |
| M6 | MSIX and zip, accessibility pass, UI automation, manual smoke test, docs, Store 5.0.0.0, cutover | certified; cutover merged |

Estimate: 8 to 11 focused sessions. M1 and M2 carry the risk and are also the most mechanical, because the goldens define done.

## Risks

- **Word Art text rendering.** Glyph placement, the "middle" baseline and neon blur won't be pixel-exact against Chromium. Mitigation: a perceptual threshold, neon sigma tuned against goldens, and the pack PNGs as extra references.
- **Golden freeze timing.** If cutover lands before every scene is frozen, coverage is lost for good. Mitigation: freezing happens in M0, and the ledger orphan check blocks cutover.
- **SkiaSharp 4.x API drift** (newer than most references). Mitigation: pin the version in `global.json` and the csproj, and write against `SKPathBuilder` from the start.
- **The Store update path from Electron to WPF under the same identity.** No sibling app has crossed this exact path. Mitigation: sideload-test an update from an installed 4.1 MSIX to the 5.0 MSIX before submitting.
