# ItsjustEste's RBX Classic Shirt and Pants Maker — v4.0 Design Spec

## Overview

Polish pass on the R15 template editor: rebrand, decompose the monolith, evolve the UI, add image adjustment tools, fix canvas drop positioning, and improve layer item UX. The app works — this is about making it feel professional and adding the editing tools it's missing.

## 1. Rebranding

| Location | Old | New |
|---|---|---|
| Window title (Electron) | Conundrum by Este - R15 Shirt Editor | ItsjustEste's RBX Classic Shirt and Pants Maker |
| Header H1 | Roblox R15 Shirt Editor | RBX Classic Shirt & Pants Maker |
| Header subtitle | Conundrum by Este \| 626Labs \| v3.0 | ItsjustEste \| v4.0 |
| Footer (new) | — | Developed at 626 Labs LLC |
| HTML `<title>` | Roblox R15 Shirt Editor \| Conundrum by Este | ItsjustEste's RBX Classic Shirt and Pants Maker |
| package.json productName | R15 Template Designer | ItsjustEste's RBX Classic Shirt and Pants Maker |
| package.json appId | com.conundrum.r15 | com.itsjusteste.rbx-maker |
| package.json author | Conundrum by Este | ItsjustEste / 626 Labs LLC |

## 2. File Decomposition

### Before
```
shirt_editor.html   (2,684 lines — CSS + HTML + JS monolith)
main.js             (39 lines — Electron main process)
```

### After
```
backup/
  shirt_editor_v3_backup.html    (full original, untouched)
  shirt_editor_working.html      (working standalone copy)

editor.html          (~310 lines — markup only, links to styles.css and editor.js)
styles.css           (~160+ lines — all CSS, evolved with new visual depth)
editor.js            (~2,500+ lines — all JS logic + new features)
main.js              (updated window title)
package.json         (updated branding)
```

No abstractions, no module system, no build step. Just "CSS in the CSS file, HTML in the HTML file, JS in the JS file."

## 3. Visual Evolution

The current purple/cyan cyberpunk palette stays but evolves from flat to dimensional:

- **Subtle gradients** on panels and surfaces instead of flat solid backgrounds
- **Soft inner shadows / glows** on interactive elements for depth
- **Better contrast hierarchy** — primary actions pop, secondary recede
- **Breathing room** — more consistent padding/margins throughout
- **Footer bar** — slim bar at bottom: "Developed at 626 Labs LLC" in muted text

The `frontend-design` skill will be used during implementation to nail the visual quality.

## 4. Ribbon Toolbar Cleanup

**Problem:** Buttons are cramped, awkwardly stacked, and arranged in odd positions to fit the space.

**Solution:**
- Re-layout ribbon groups with consistent spacing and alignment
- Buttons get uniform sizing within each group — no more mixed sizes
- Groups flow naturally left-to-right without forcing stacks where they don't make sense
- The Mode toggle (SHIRT/PANTS) stays prominent but doesn't force awkward vertical stacking on its neighbors

### Save/Export Buttons — Clarity for Young Users

Replace the current ambiguous "Save" / "Export PNG" with clear, icon-labeled buttons:

| Button | Icon | Label | Subtitle | What it does |
|---|---|---|---|---|
| Save Work | Floppy disk / folder icon | **Save Work** | "reopen & edit later" | Saves project JSON with all layers, positions, settings |
| Download Template | Download arrow icon | **Download Template** | "ready for Roblox" | Exports the final 585x559 PNG template |

Both buttons get distinct visual treatments so they can't be confused:
- **Save Work** — secondary style (outlined)
- **Download Template** — primary style (gradient fill, stands out)

## 5. Layer Item Redesign

**Problem:** Tiny emoji buttons (duplicate, visibility, delete) are hard to read and click. Unclear what they do.

**New layout:**
```
+--------+------------------------+
|        |  my_design             |
|  IMG   +--------+------+-------+
|        |  Dupe  | Hide |  Del  |
+--------+--------+------+-------+
```

- Thumbnail gets full row height for a bigger asset preview
- Name sits top-right of the thumbnail
- Action buttons sit below the name, to the right of the thumbnail
- Buttons are labeled text ("Dupe", "Hide", "Del"), not emoji
- Color coding preserved: Dupe = cyan, Hide = gray, Del = red
- Bigger click targets (full button styling, not just tiny icons)
- Selected state: purple border glow on the whole item

## 6. Image Adjustment Tools

**Added to the "Selected Layer" panel**, below the existing controls (X/Y, scale, rotation, opacity, flip).

### Controls
| Adjustment | Type | Range | Default | Implementation |
|---|---|---|---|---|
| Brightness | Slider | 0–200% | 100% | CSS `filter: brightness()` |
| Contrast | Slider | 0–200% | 100% | CSS `filter: contrast()` |
| Saturation | Slider | 0–200% | 100% | CSS `filter: saturate()` |
| Hue Rotate | Slider | 0–360deg | 0deg | CSS `filter: hue-rotate()` |
| Red channel | Slider | -100 to +100 | 0 | Pixel manipulation via ImageData |
| Green channel | Slider | -100 to +100 | 0 | Pixel manipulation via ImageData |
| Blue channel | Slider | -100 to +100 | 0 | Pixel manipulation via ImageData |

### Implementation approach
- **CSS filter properties** handle brightness, contrast, saturation, and hue-rotate. These are applied during canvas `drawImage()` via `ctx.filter` — real-time, no lag.
- **RGB channel shifts** use `getImageData()` / `putImageData()` pixel manipulation. Applied per-layer during render. The original image data is preserved; adjustments are non-destructive.
- **Reset button** restores all adjustments to defaults.
- Each layer stores its adjustments: `{ brightness: 100, contrast: 100, saturate: 100, hueRotate: 0, r: 0, g: 0, b: 0 }`

### UI treatment
- Collapsible section header "Image Adjustments" (collapsed by default to save space)
- Sliders match the app's accent color (purple)
- RGB channels shown as three compact sliders with R/G/B color labels
- "Reset Adjustments" button at the bottom

## 7. Canvas Drop Positioning

**Problem:** Dropping an image onto the canvas always places it center-of-front-torso regardless of where you drop it.

**Solution:**
- `canvas.ondrop` captures `e.clientX` / `e.clientY`
- Convert to canvas coordinates accounting for canvas offset, zoom level, and scroll
- Pass coordinates to `addLayer(img, name, x, y)` as optional params
- When `x, y` are provided: image centers on that point
- When `x, y` are omitted (click-to-add, drop zone, asset browser): default center-of-front-torso behavior preserved

### Updated function signature
```javascript
function addLayer(img, name, dropX, dropY) {
  // ...
  if (dropX !== undefined && dropY !== undefined) {
    layer.x = dropX - (img.width * scale) / 2;
    layer.y = dropY - (img.height * scale) / 2;
  } else {
    // existing default: center of front torso
    layer.x = front.x + (front.w - img.width * scale) / 2;
    layer.y = front.y + (front.h - img.height * scale) / 2;
  }
}
```

## 8. Safety & Backup

Before any changes:
1. Create `backup/` directory
2. Copy `shirt_editor.html` to `backup/shirt_editor_v3_backup.html` (pristine original)
3. Copy `shirt_editor.html` to `backup/shirt_editor_working.html` (standalone working copy that can be opened directly in a browser or re-pointed to by main.js if needed)

## 9. Out of Scope

- No new drawing tools
- No new presets or templates
- No Roblox API integration
- No modularization beyond the 4-file split
- No build tooling or bundler
