# ItsjustEste's RBX Classic Shirt and Pants Maker v4.0 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the R15 template editor with rebranding, file decomposition, evolved UI, image adjustment tools, canvas drop positioning, and layer item redesign.

**Architecture:** The monolith `shirt_editor.html` is split into 3 files (`editor.html`, `styles.css`, `editor.js`) with no build step or module system. New features (image adjustments, drop positioning) are added to `editor.js`. The `frontend-design` skill is used for visual evolution of the CSS. Electron's `main.js` is updated for branding.

**Tech Stack:** Electron 41, vanilla HTML/CSS/JS, Canvas 2D API, CSS `ctx.filter` for image adjustments, `ImageData` pixel manipulation for RGB channels.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `backup/shirt_editor_v3_backup.html` | Create | Pristine copy of original monolith |
| `backup/shirt_editor_working.html` | Create | Standalone working copy |
| `editor.html` | Create | Markup only — header, ribbon, panels, canvas, footer. Links `styles.css` and `editor.js` |
| `styles.css` | Create | All CSS — evolved with depth, gradients, better spacing |
| `editor.js` | Create | All JS logic + new features (image adjustments, drop positioning, layer item redesign) |
| `main.js` | Modify | Window title update |
| `package.json` | Modify | Branding updates |
| `shirt_editor.html` | Keep | Original stays in place until decomposition is verified working |

---

### Task 1: Create Backup Copies

**Files:**
- Create: `backup/shirt_editor_v3_backup.html`
- Create: `backup/shirt_editor_working.html`

- [ ] **Step 1: Create backup directory and copy files**

```bash
mkdir -p backup
cp shirt_editor.html backup/shirt_editor_v3_backup.html
cp shirt_editor.html backup/shirt_editor_working.html
```

- [ ] **Step 2: Verify backups exist and match original**

Run: `ls -la backup/ && diff shirt_editor.html backup/shirt_editor_v3_backup.html`
Expected: Two files listed, diff shows no differences.

- [ ] **Step 3: Commit**

```bash
git add backup/shirt_editor_v3_backup.html backup/shirt_editor_working.html
git commit -m "chore: create v3 backup copies before v4 refactor"
```

---

### Task 2: Extract CSS into `styles.css`

**Files:**
- Create: `styles.css`
- Source: `shirt_editor.html` lines 7-135 (everything inside the style tags)

- [ ] **Step 1: Create `styles.css`**

Extract all CSS from lines 7-135 of `shirt_editor.html` into a new file `styles.css`. This is a direct copy — no modifications yet. The CSS starts with `* { margin:0;` and ends with `@media (max-width: 900px) { .panel, .rpanel { width:200px; } }`.

Add one new rule at the end for the footer:

```css
/* Footer */
footer {
  background: #0a0a14;
  border-top: 1px solid #333;
  padding: 6px 24px;
  text-align: center;
  font-size: 11px;
  color: #555;
  flex-shrink: 0;
}
```

- [ ] **Step 2: Verify file was created with correct content**

Run: `head -5 styles.css && echo "---" && tail -5 styles.css && wc -l styles.css`
Expected: Starts with `* { margin:0;`, ends with footer rule, approximately 140+ lines.

- [ ] **Step 3: Commit**

```bash
git add styles.css
git commit -m "feat: extract CSS into styles.css and add footer rule"
```

---

### Task 3: Extract JS into `editor.js`

**Files:**
- Create: `editor.js`
- Source: `shirt_editor.html` lines 443-2682 (everything between the script tags)

- [ ] **Step 1: Create `editor.js`**

Extract all JavaScript from lines 443-2682 of `shirt_editor.html` (after the opening script tag, before the closing script tag) into a new file `editor.js`. This is a direct copy of the JS content — no modifications yet.

The file should start with the ribbon tab switching comment block:
```javascript
// RIBBON TAB SWITCHING
```

And end with:
```javascript
// Initial render
render();
```

- [ ] **Step 2: Verify file was created with correct content**

Run: `head -5 editor.js && echo "---" && tail -3 editor.js && wc -l editor.js`
Expected: Starts with ribbon tab switching comment, ends with `render();`, approximately 2240 lines.

- [ ] **Step 3: Commit**

```bash
git add editor.js
git commit -m "feat: extract JS into editor.js"
```

---

### Task 4: Create `editor.html` (Markup Only)

**Files:**
- Create: `editor.html`

- [ ] **Step 1: Create `editor.html`**

Build the new HTML file from the markup in `shirt_editor.html`. The structure is:
- Lines 1-6 for doctype/head open
- Lines 136-139 for font links
- Lines 141-441 for body markup

Key changes from the original:
1. Remove the inline `<style>` block — replaced with `<link rel="stylesheet" href="styles.css">`
2. Remove the inline `<script>` block — replaced with `<script src="editor.js"></script>` at end of body
3. Update header H1 to "RBX Classic Shirt & Pants Maker"
4. Update header subtitle to "ItsjustEste | v4.0"
5. Update HTML title to "ItsjustEste's RBX Classic Shirt and Pants Maker"
6. Add footer before closing body: `<footer>Developed at 626 Labs LLC</footer>`
7. Update the File ribbon group buttons:
   - "Save" button becomes: text content `Save Work` with floppy disk unicode prefix, title "Save your work - reopen and edit later"
   - "Export PNG" button becomes: text content `Download Template` with down arrow unicode prefix, title "Export as PNG ready for Roblox"
   - "Open" button gets folder unicode prefix
   - "Image" button gets image unicode prefix

- [ ] **Step 2: Verify the file structure**

Run: `head -15 editor.html && echo "---" && tail -5 editor.html && wc -l editor.html`
Expected: Starts with DOCTYPE, has rebranded header, ends with closing html tag, approximately 310 lines.

- [ ] **Step 3: Commit**

```bash
git add editor.html
git commit -m "feat: create editor.html with rebranded markup, footer, and external CSS/JS links"
```

---

### Task 5: Update `main.js` to Load `editor.html`

**Files:**
- Modify: `main.js:8` (title), `main.js:15` (loadFile)

- [ ] **Step 1: Update main.js**

Change the window title on line 8 from:
```javascript
    title: "Conundrum by Este - R15 Shirt Editor",
```
to:
```javascript
    title: "ItsjustEste's RBX Classic Shirt and Pants Maker",
```

Change the loadFile call on line 15 from:
```javascript
  win.loadFile('shirt_editor.html')
```
to:
```javascript
  win.loadFile('editor.html')
```

- [ ] **Step 2: Verify changes**

Run: `cat main.js`
Expected: Title says the new name, loadFile points to `editor.html`.

- [ ] **Step 3: Commit**

```bash
git add main.js
git commit -m "feat: update main.js branding and point to editor.html"
```

---

### Task 6: Update `package.json` Branding

**Files:**
- Modify: `package.json`

- [ ] **Step 1: Update package.json**

Change these fields:
- `"name"`: `"itsjusteste-rbx-maker"`
- `"productName"`: `"ItsjustEste's RBX Classic Shirt and Pants Maker"`
- `"version"`: `"4.0.0"`
- `"description"`: `"Roblox Classic Shirt and Pants Template Maker by ItsjustEste"`
- `"author"`: `"ItsjustEste / 626 Labs LLC"`
- `"build.appId"`: `"com.itsjusteste.rbx-maker"`

- [ ] **Step 2: Verify changes**

Run: `cat package.json`
Expected: All branding fields updated, version 4.0.0.

- [ ] **Step 3: Commit**

```bash
git add package.json
git commit -m "feat: rebrand package.json to v4.0"
```

---

### Task 7: Smoke Test the Decomposition

**Files:** None (verification only)

- [ ] **Step 1: Launch the app in Electron dev mode**

Run: `npx electron .`
Expected: App opens, shows rebranded header, footer visible. All existing features work.

- [ ] **Step 2: Test critical workflows**

Manual checklist:
1. Drop an image onto the canvas — it appears as a layer
2. Select, move, rotate, resize a layer
3. Switch between Shirt and Pants mode
4. Add word art
5. Save project (.r15proj) and reload it
6. Export PNG

- [ ] **Step 3: If anything is broken, fix it before proceeding**

The script tag is at the bottom of body so DOM should be ready. Google Fonts link is in head. File paths are relative and co-located. These are the common breakage points to check.

---

### Task 8: Evolve CSS with Visual Depth

**Files:**
- Modify: `styles.css`

**Use the `frontend-design` skill** for this task to ensure high visual quality.

- [ ] **Step 1: Evolve panel backgrounds from flat to gradient**

Replace flat `background:#111827` on `.panel` and `.rpanel` with:
```css
background: linear-gradient(180deg, #111827 0%, #0d1117 100%);
```
Update border colors from `#333` to `#2a2a3e` for slightly more dimension.

- [ ] **Step 2: Evolve header with glow and shadow**

Update header CSS:
```css
header {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #1a1a2e 100%);
  padding: 10px 24px;
  display: flex;
  align-items: center;
  gap: 16px;
  border-bottom: 1px solid rgba(224, 64, 251, 0.2);
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}
```

- [ ] **Step 3: Evolve ribbon backgrounds**

Update `.ribbon` and `.ribbon-tabs` with subtle gradients instead of flat backgrounds.

- [ ] **Step 4: Add depth to buttons**

Update `.btn-primary` with box-shadow glow, `.btn-secondary` with gradient background and hover glow. Update `.rbtn` with consistent sizing.

- [ ] **Step 5: Add depth to layer items and drop zone**

Update `.layer-item` with gradient background, glow on selected state. Update `.drop-zone` with inset glow on hover.

- [ ] **Step 6: Commit**

```bash
git add styles.css
git commit -m "feat: evolve CSS with visual depth - gradients, shadows, better hover states"
```

---

### Task 9: Redesign Layer Items in `editor.js`

**Files:**
- Modify: `editor.js` — the `updateUI()` function
- Modify: `styles.css` — remove old layer button CSS

- [ ] **Step 1: Redesign the layer item generation**

In `editor.js`, find the `updateUI()` function. Replace the layer item generation loop content. The new layout:
- Thumbnail (48x48) on the left, spanning full item height
- Right side: name on top, three labeled buttons (Dupe/Hide/Del) on bottom
- Buttons use text labels with color coding: Dupe=cyan border, Hide=gray border, Del=red border
- All button actions use `data-action` attributes and `stopPropagation` (same pattern as current code, using safe DOM event listeners)

- [ ] **Step 2: Remove old layer button CSS from `styles.css`**

Remove the old `.layer-item .layer-btns` rules, `.layer-item img` fixed sizing, and individual button color rules that are now inline-styled.

- [ ] **Step 3: Verify layer items render correctly**

Run: `npx electron .`
Expected: Layer items show larger thumbnail, labeled action buttons. Color coding preserved.

- [ ] **Step 4: Commit**

```bash
git add editor.js styles.css
git commit -m "feat: redesign layer items with larger thumbnails and labeled buttons"
```

---

### Task 10: Add Image Adjustment Properties to Layers

**Files:**
- Modify: `editor.js` — all layer creation points

- [ ] **Step 1: Add adjustment defaults to `addLayer()`**

Add to the layer object after `flipV: false`:
```javascript
brightness: 100, contrast: 100, saturate: 100, hueRotate: 0,
rShift: 0, gShift: 0, bShift: 0,
```

- [ ] **Step 2: Add adjustment defaults to `addDrawingLayer()`**

Same properties added to the drawing layer object.

- [ ] **Step 3: Add adjustment defaults to asset browser click handler**

Both the left-click and right-click handlers in `showAssetFolder()` need the properties.

- [ ] **Step 4: Add adjustment defaults to word art handler and `createDetailLayer()`**

Add to both layer object creation points.

- [ ] **Step 5: Verify `duplicateLayer()` copies adjustments**

It uses spread operator `{...src}` which copies all properties automatically. No changes needed.

- [ ] **Step 6: Commit**

```bash
git add editor.js
git commit -m "feat: add image adjustment properties to all layer types"
```

---

### Task 11: Apply Image Adjustments in Render

**Files:**
- Modify: `editor.js` — `render()` and `exportPNG()` functions

- [ ] **Step 1: Add helper functions before `render()`**

Add `applyRGBShift(ctx, img, x, y, w, h, rShift, gShift, bShift)` — draws image to temp canvas, manipulates pixel data for RGB channel shifts, returns the temp canvas.

Add `hasAdjustments(layer)` — returns true if any adjustment differs from default.

- [ ] **Step 2: Update `render()` layer drawing loop**

In the layer loop, after setting up transforms and before `drawImage`:
1. If `hasAdjustments(layer)`, set `oc.filter` to the CSS filter string
2. If RGB shifts are non-zero, use `applyRGBShift` to get a shifted temp canvas, draw that instead
3. Reset `oc.filter = 'none'` after drawing

- [ ] **Step 3: Apply same logic in `exportPNG()` layer loop**

Mirror the render changes in the export function so adjustments bake into the final PNG.

- [ ] **Step 4: Commit**

```bash
git add editor.js
git commit -m "feat: apply image adjustments in render and export"
```

---

### Task 12: Add Image Adjustment Controls to Selected Layer Panel

**Files:**
- Modify: `editor.js` — the `updateUI()` function (layer controls section)

- [ ] **Step 1: Add adjustment sliders to controls template**

After the "Center on Front Torso" button, add a collapsible "Image Adjustments" section with:
- Toggle header (click to expand/collapse, collapsed by default)
- Brightness slider (0-200%, default 100%)
- Contrast slider (0-200%, default 100%)
- Saturation slider (0-200%, default 100%)
- Hue Rotate slider (0-360deg, default 0)
- R/G/B channel sliders (-100 to +100, default 0) with colored accent
- Reset Adjustments button

- [ ] **Step 2: Wire up event handlers**

Each slider updates its layer property and calls `render()`. Toggle shows/hides the panel. Reset button restores all defaults and calls `updateUI()` + `render()`.

- [ ] **Step 3: Commit**

```bash
git add editor.js
git commit -m "feat: add collapsible image adjustment controls to layer panel"
```

---

### Task 13: Canvas Drop Positioning

**Files:**
- Modify: `editor.js` — `addLayer()`, `canvas.ondrop`, `handleFiles()`

- [ ] **Step 1: Update `addLayer()` signature**

Change from `function addLayer(img, name)` to `function addLayer(img, name, dropX, dropY)`. When `dropX` and `dropY` are provided, center the image on those coordinates. Otherwise use the existing default (center of front torso).

- [ ] **Step 2: Update `canvas.ondrop` to capture and convert coordinates**

Capture `e.clientX`/`e.clientY`, convert to canvas space using `getBoundingClientRect()` and dividing by `zoom`, pass to `handleFiles()`.

- [ ] **Step 3: Update `handleFiles()` to accept and forward coordinates**

Add `dropX, dropY` params, pass through to `addLayer()` calls. When called from the drop zone (no coordinates), `addLayer` uses defaults.

- [ ] **Step 4: Commit**

```bash
git add editor.js
git commit -m "feat: canvas drop positioning - images land where you drop them"
```

---

### Task 14: Update Save/Load for Adjustment Properties

**Files:**
- Modify: `editor.js` — `saveProject()` and `loadProject()`

- [ ] **Step 1: Add adjustment props to `saveProject()` serialization**

Add brightness, contrast, saturate, hueRotate, rShift, gShift, bShift to the layer map. Update version to `'4.0'` and app name.

- [ ] **Step 2: Restore adjustment props in `loadProject()`**

Add adjustment properties to the layer restoration with null-safe defaults for backward compatibility with v3 files.

- [ ] **Step 3: Commit**

```bash
git add editor.js
git commit -m "feat: save and load image adjustment properties in project files"
```

---

### Task 15: Verify Undo Handles Adjustments

**Files:**
- Modify: `editor.js` (if needed)

- [ ] **Step 1: Verify undo serialization**

The current `saveUndo()` uses spread operator which captures all properties. The restore pushes the full object back. Verify by reading the code — no changes should be needed.

- [ ] **Step 2: Commit if changes were needed**

Skip if no changes.

---

### Task 16: Final Smoke Test

**Files:** None (verification only)

- [ ] **Step 1: Launch app and test all features**

Run: `npx electron .`

Test checklist:
1. Header shows "RBX Classic Shirt & Pants Maker" with "ItsjustEste | v4.0"
2. Footer shows "Developed at 626 Labs LLC"
3. Window title is correct
4. Save/Export buttons show icons and new labels
5. Drop image on canvas — lands at drop position
6. Drop image on drop zone — lands center of front torso
7. Layer items show large thumbnail + labeled buttons
8. Image adjustments: brightness, contrast, saturation, hue all work in real time
9. RGB channel sliders work
10. Reset Adjustments works
11. Save project preserves adjustments
12. Export PNG bakes in adjustments
13. Undo preserves adjustments
14. UI has visual depth (gradients, shadows, hover effects)

- [ ] **Step 2: Fix any issues found**

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "feat: ItsjustEste's RBX Classic Shirt and Pants Maker v4.0 complete"
```
