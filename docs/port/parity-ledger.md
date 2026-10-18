# Parity ledger

Generated from `parity-ledger.json` by `tools/ledger/ledger.py render` (do not edit by hand). Every user-visible behavior of RBX15 4.1, inventoried 2026-10-11, with the module that owns it in v5, how parity is proven, and whether v5 keeps it, fixes it, drops it, or adds it. **v5 does not ship with a row whose status is `open`.** Design: [native v5 port design](../superpowers/specs/2026-10-11-native-v5-port-design.md). Deliberate changes: [divergences](divergences.md).

**427 rows.** Status: done 261, open 166. Disposition: fix 207, keep 188, drop 26, new 6. Checks: unit 124, ui-auto 123, manual 67, golden 66, none-retired 25, doc 22.

## App.AssetLibrary

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `drop-panel` | Drop zone panel: click-to-browse and drag-over highlight | ui-auto | keep | low |
| done | `drop-filetypes` | Image file acceptance and naming | ui-auto | fix | med |
| open | `packs-resolve-dir` | Packs directory resolution: dev vs packaged | manual | keep | low |
| done | `packs-load-builtin` | Load built-in pack into asset browser | ui-auto | fix | low |
| done | `asset-open-folder` | Open folder (webkitdirectory) and image filtering | ui-auto | fix | low |
| done | `asset-dedupe-by-name` | Deduplicate by filename (deepest path wins) | unit | keep | med |
| done | `asset-folder-grouping` | Grouping by immediate parent folder and subfolder buttons | unit | keep | med |
| done | `asset-count-label` | Asset count label text | ui-auto | fix | low |
| open | `asset-grid-thumbnails` | Asset grid thumbnails rendering | ui-auto | fix | med |
| done | `asset-drag-to-canvas` | Drag a thumbnail onto the canvas | ui-auto | fix | med |
| open | `asset-resizable-browser` | Resizable asset browser (drag handle) | ui-auto | keep | low |
| done | `panel-asset-library` | Asset library: packs, folder, browser | manual | fix | high |
| done | `panel-asset-resize-handle` | Asset browser vertical resize handle | ui-auto | keep | low |
| open | `panel-drop-zone` | Drop art here zone | ui-auto | fix | low |
| done | `io-folder-picker` | Asset library folder picker semantics | unit | fix | med |
| open | `io-image-formats` | Accepted image inputs (drop, Add image, Background, folder) | manual | fix | med |
| done | `pack-builtin-load` | Built-in Drips and Word art pack loading | manual | keep | low |
| done | `asset-drag-stale-reference` | Stale asset-drag file hijacks the next OS file drop | ui-auto | fix | low |
| done | `asset-thumb-uuid-label` | Thumbnail caption substitutes parent folder name for UUID filenames | manual | keep | low |
| done | `asset-count-label-overwritten` | Count label's dedupe/pack message is immediately overwritten | ui-auto | fix | low |
| done | `pack-load-sync-and-mime` | Built-in pack load is synchronous and labels every file image/png | ui-auto | fix | low |
| open | `asset-objecturl-lifecycle` | Thumbnail blob URLs created eagerly and revoked on folder switch | manual | fix | low |

## App.Canvas

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `render-zoom-canvas` | Zoom resize and smoothing rule | manual | fix | med |
| done | `render-checkerboard` | Transparency checkerboard | manual | keep | low |
| done | `render-grid-overlay` | Region grid overlay | manual | keep | low |
| done | `render-selection-box` | Selection bounding box | manual | fix | med |
| done | `render-resize-handles` | Corner resize handles | manual | keep | low |
| done | `render-rotation-handle` | Rotation handle (stem, knob, glyph) | manual | keep | med |
| done | `render-rotation-label` | Rotation angle label | manual | fix | med |
| done | `render-restore-and-cursor` | Render tail: restore then updateCursor | manual | keep | low |
| done | `cursor-default` | Default tool cursor | manual | fix | low |
| done | `cursor-eraser` | Eraser brush ring cursor | manual | fix | med |
| done | `tools-set-tool` | setTool: active tool, button highlight, cursor refresh | ui-auto | keep | low |
| open | `tools-toggle-off` | Re-click or re-press active tool returns to Select | ui-auto | keep | low |
| done | `tools-auto-select-switch` | Tool auto-reverts to Select on layer add and layer select | ui-auto | keep | low |
| done | `tools-cursor` | Canvas cursor per tool, eraser ring cursor | manual | fix | med |
| done | `drop-canvas` | Drop on canvas places layer at drop point | ui-auto | fix | med |
| done | `select-move` | Move drag | ui-auto | fix | low |
| done | `select-rotate-handle` | Rotation handle | ui-auto | fix | med |
| done | `select-hover-idle` | Idle hover cursor | ui-auto | fix | low |
| done | `canvas-coord-map` | Mouse to template-space coordinate mapping | ui-auto | fix | med |
| done | `canvas-cursor-states` | Cursor shapes per tool and drag state | ui-auto | fix | low |
| done | `select-deselect-click` | Click on empty space deselects | ui-auto | keep | low |
| done | `select-hover-and-end` | Select tool mousemove idle, mouseup and mouseleave | ui-auto | fix | low |
| done | `select-handle-render` | Selection box and handle rendering (editor overlay) | manual | keep | low |
| done | `draw-release-outside-canvas` | Mouse released or left outside canvas while drawing | ui-auto | fix | med |
| open | `draw-tool-switch-midstroke` | Tool switch during a stroke | ui-auto | fix | low |
| done | `wheel-scale-layer` | Wheel in Select tool scales selected layer | ui-auto | fix | med |
| done | `wheel-brush-size` | Wheel with draw tools changes brush size | ui-auto | keep | low |
| done | `eraser-cursor` | Eraser ring cursor | manual | fix | low |
| done | `eraser-selection-semantics` | Eraser operates on selection, not on topmost under cursor | ui-auto | fix | low |
| done | `zoom-controls` | Zoom buttons and label | ui-auto | keep | med |
| done | `overlay-grid-checkbox` | Grid checkbox (default off) region outlines | manual | fix | low |
| open | `render-pipeline-canvas-reset` | Render loop characteristics that affect input | manual | keep | low |
| done | `canvas-zoom-controls` | Zoom controls | ui-auto | fix | med |
| done | `canvas-select-gestures` | Select tool gestures: move, resize, rotate, wheel | ui-auto | fix | high |
| done | `select-hidden-layer-still-handled` | Hidden selected layer keeps box and live handles | ui-auto | fix | low |
| done | `canvas-mouse-button-agnostic` | Right and middle mouse buttons act like left on canvas | ui-auto | fix | low |
| done | `wheel-sign-only-and-midstroke` | Wheel uses sign only; wheel mid-shape changes live stroke width | ui-auto | fix | low |
| done | `layers-multi-file-async-order` | Multi-file add: z-order follows decode completion, one undo per file | ui-auto | fix | low |
| done | `canvas-zoom-clipped-no-pan` | Zoomed canvas is clipped with no scrollbars or pan | ui-auto | fix | med |
| done | `canvas-device-pixel-render` | View canvas rendered at device pixels | manual | fix | low |
| done | `zoom-pill-limits` | Zoom buttons disabled at 25% and 300% | unit | fix | low |
| done | `wheel-scale-undo-burst` | A burst of wheel scaling is one undo step | unit | fix | low |
| done | `canvas-automation-peer` | The canvas is exposed to UI Automation | unit | fix | low |
| done | `canvas-layer-automation` | Each canvas layer is exposed to UI Automation and can be moved through it | unit | fix | low |

## App.Dialogs

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `export-png-guard` | Empty-template confirm before export | manual | keep | low |
| done | `dialogs-inventory` | All dialogs, prompts, toasts (complete list) | ui-auto | fix | low |
| done | `io-empty-export-confirm` | Blank-export guard confirm() | ui-auto | keep | low |
| done | `io-alert-inventory` | All user-facing alert() prompts (exact strings) | ui-auto | fix | low |
| done | `file-input-same-file-reselect` | Drop-zone and Background pickers ignore re-picking the same file | manual | fix | low |
| done | `diag-console-only-errors` | Image decode/read failures are console-only (silent to user) | manual | fix | low |

## App.Keyboard

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `undo-buttons-shortcuts` | Undo/Redo buttons and shortcuts | ui-auto | fix | med |
| open | `keyboard-tool-toggle-interplay` | Tool shortcuts that gate canvas behavior | ui-auto | fix | med |
| open | `keys-toggle-button` | Keys ON/OFF toggle button | ui-auto | fix | low |
| open | `keys-text-input-guard` | Shortcut suppression while an input has focus | ui-auto | fix | med |
| open | `keys-tool-shortcuts` | Tool shortcuts V B L U O E | ui-auto | fix | low |
| open | `keys-duplicate-ctrl-d` | Ctrl+D duplicate layer | ui-auto | keep | low |
| open | `keys-undo-redo` | Undo/Redo shortcuts | ui-auto | keep | low |
| open | `keys-selection-gate-and-modifiers` | Selection gate and Ctrl/Meta exclusion for nudge keys | ui-auto | fix | low |
| open | `keys-arrow-nudge` | Arrow keys nudge layer | ui-auto | fix | low |
| open | `keys-rotate-brackets` | [ ] rotate and R reset | unit | fix | med |
| open | `keys-scale-plus-minus` | + / - scale keys | unit | keep | low |
| open | `keys-delete` | Delete and Backspace remove selected layer | ui-auto | keep | low |
| open | `home-keys-toggle` | Keys: ON/OFF shortcut toggle | ui-auto | fix | low |
| open | `kbd-shortcuts` | Global keyboard shortcuts | ui-auto | fix | med |
| open | `kbd-ctrl-combo-tool-toggles` | Ctrl+letter combos still toggle tools (V B L U O); Ctrl+E does not | ui-auto | fix | med |
| open | `kbd-bracket-rotate-negative-drift` | [ rotate wrap formula breaks below -180 | ui-auto | fix | low |
| open | `kbd-input-focus-trap` | Shortcuts die after clicking any slider, checkbox or color input | ui-auto | fix | med |
| open | `kbd-focused-button-enter-space` | Enter/Space re-fire the last clicked button | manual | keep | low |

## App.LayerProperties

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `layers-empty-state` | Empty selection panel | ui-auto | keep | low |
| done | `props-name-xy` | Name and X/Y fields | ui-auto | fix | low |
| done | `props-scale` | Scale slider | ui-auto | fix | low |
| done | `props-rotation` | Rotation slider | ui-auto | fix | low |
| done | `props-opacity` | Layer opacity slider | ui-auto | fix | low |
| done | `props-adjust-panel` | Image adjustments collapsible panel | ui-auto | fix | low |
| open | `props-adjust-sliders` | Brightness/Contrast/Saturation/Hue/RGB shift sliders | ui-auto | fix | high |
| done | `props-adjust-reset` | Reset adjustments | ui-auto | fix | low |
| done | `props-live-refresh` | Property panel refresh behavior while dragging | ui-auto | fix | low |
| open | `layers-selected-panel` | Selected layer controls | ui-auto | fix | high |
| done | `props-falsy-zero-adjust-display` | Brightness/Contrast/Saturation of 0 display as 100 after reselect | ui-auto | fix | low |
| done | `props-rotation-label-unrounded` | Rotation label shows raw float after handle drag | ui-auto | fix | low |
| done | `props-panel-rebuild-resets` | Every updateUI rebuilds the property panel and collapses Image adjustments | ui-auto | fix | low |

## App.Layers

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `layers-select` | Select layer | ui-auto | keep | low |
| done | `layers-list-render` | Layer list rows (top = highest z) | ui-auto | fix | med |
| done | `layers-reorder-drag` | Drag-to-reorder layers in panel | ui-auto | fix | med |
| done | `layers-list` | Layer list rows | ui-auto | fix | med |
| done | `layers-reorder-drag-2` | Drag to reorder layers | ui-auto | fix | med |
| done | `layer-reorder-dragging-class-timer` | Layer drag ghost styling via setTimeout(0) and native drag image | ui-auto | keep | low |

## App.Ribbon.Details

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `details-settings-controls` | Detail settings (color, width, Stitch) | ui-auto | keep | low |
| open | `details-buttons-inventory` | Detail button inventory and grouping | ui-auto | keep | low |
| open | `details-settings` | Details tab: stroke settings | ui-auto | keep | low |

## App.Ribbon.Home

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `tools-draw-props` | Draw color, size, fill controls | ui-auto | keep | low |
| done | `draw-brush-controls` | Brush color, size, fill controls and defaults | manual | keep | low |
| done | `overlay-mask-checkbox` | Mask checkbox (default on) | ui-auto | fix | low |
| done | `regions-grid-layout-shirt` | Template Regions grid layout (Shirt) | ui-auto | keep | low |
| done | `regions-grid-layout-pants` | Template Regions grid in Pants mode (labels and keys) | ui-auto | keep | low |
| open | `regions-button-swatch-paint` | Region button swatch preview (paintRegionButton) | manual | fix | med |
| done | `regions-click-fit-layer` | Click region button with a layer selected: fit layer into region | unit | fix | low |
| done | `regions-right-click-clear` | Right-click region button clears its fill | ui-auto | keep | low |
| done | `regions-hint-text` | Fill Style hint line | manual | keep | low |
| open | `fillstyle-type-buttons` | Fill type button strip (7 types) | ui-auto | fix | low |
| done | `fillstyle-color-pickers` | Color 1 and Color 2 pickers | manual | fix | med |
| open | `fillstyle-angle-scale` | Angle and Scale sliders | ui-auto | keep | low |
| done | `project-load-image-layer` | Add image (load image as layer) | ui-auto | fix | med |
| open | `home-mode-toggle` | Shirt/Pants mode toggle button | ui-auto | fix | low |
| open | `home-tool-buttons` | Tool buttons: Select, Draw, Line, Rect, Ellipse, Eraser | ui-auto | keep | low |
| done | `home-brush-group` | Brush group: color, size, fill | ui-auto | keep | med |
| open | `home-placement-group` | Placement buttons: Fit Front, F+B, Stretch, Allover, Scatter | ui-auto | fix | low |
| done | `home-edit-group` | Edit group: Undo, Redo, Clear | ui-auto | keep | med |
| done | `home-file-group` | File group: Save work, Open, Add image, Download template | manual | fix | med |
| done | `home-settings-group` | Settings group: Template name, Mask, Grid | ui-auto | keep | low |
| done | `region-front-highlight-lost` | Front region button loses its purple base highlight once cleared | ui-auto | fix | low |
| open | `fillstyle-ui-not-synced-from-region` | Fill Style controls are one-way (never read back from regions) | manual | keep | low |
| done | `art-palette` | Palette from the user's art (new in v5.1) | unit | new | low |

## App.Ribbon.Layout

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `bg-palette` | Background color swatch palette | ui-auto | keep | low |
| open | `fills-to-css` | fillToCSS swatch approximation | manual | fix | low |
| open | `props-center-front` | Center on front torso | ui-auto | fix | low |
| open | `bg-swatches` | Background color swatches | ui-auto | keep | low |
| open | `bg-color-opacity` | Background color picker and opacity slider | ui-auto | keep | med |
| open | `bg-image` | Background image picker | ui-auto | fix | med |
| open | `placement-selection-guard` | Placement actions with no selection | ui-auto | fix | low |
| done | `regions-limb-caps` | Limb top and bottom tiles in the region grid | ui-auto | fix | low |
| open | `presets-six-buttons` | Layout presets row (6 buttons) | ui-auto | fix | low |
| open | `layout-presets` | Layout tab: Presets (6 buttons) | ui-auto | fix | low |
| open | `layout-background-swatches` | Background swatches (12) | ui-auto | fix | low |
| open | `layout-bg-settings` | BG Settings: color, image, opacity | ui-auto | fix | low |
| open | `layout-fill-style` | Fill Style: type, colors, angle, scale | ui-auto | keep | med |
| open | `layout-template-regions` | Template Regions grid (dynamic) | ui-auto | fix | med |
| done | `ribbon-fill-regions-joined` | Fill Style and the region grid read as one fill tool | ui-auto | fix | low |

## App.Ribbon.Text

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `wordart-state-defaults` | Word Art control set and default values | ui-auto | fix | low |
| open | `wordart-preview-live` | Live preview wiring | ui-auto | keep | low |
| open | `text-inputs` | Text tab: text, font, colors, size | ui-auto | keep | med |
| open | `text-preview` | Word Art live preview canvas | ui-auto | fix | low |
| open | `text-add-to-canvas` | Add to canvas (Word Art layer) | unit | fix | med |

## App.Shell

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `ribbon-tab-switch` | Ribbon tab switching | ui-auto | keep | low |
| done | `mode-toggle-button` | Shirt/Pants toggle button | ui-auto | keep | low |
| open | `project-startup` | Initial render and startup order | manual | fix | low |
| open | `shell-window-frame` | Window frame, title, menu, default size | manual | fix | low |
| open | `shell-layout-skeleton` | Page skeleton and flex layout | manual | fix | low |
| open | `shell-responsive-breakpoint` | Narrow-window breakpoint | ui-auto | keep | low |
| open | `shell-header` | Header bar content and styling | manual | fix | med |
| done | `shell-footer` | Footer line | unit | fix | low |
| done | `theme-tokens-colors` | Color token set (:root) | manual | keep | low |
| open | `theme-tokens-shape-motion-type` | Radii, shadows, motion, type tokens | manual | keep | med |
| done | `font-ui-runtime-google` | UI fonts loaded at runtime from Google Fonts | manual | fix | med |
| done | `theme-scrollbars` | Scrollbar styling (effective result) | manual | keep | low |
| open | `a11y-focus-rings` | Keyboard focus ring | ui-auto | fix | med |
| open | `ribbon-tabs-switching` | Ribbon tab strip and switching | ui-auto | keep | low |
| open | `ribbon-group-structure` | Ribbon group chrome | manual | keep | low |
| done | `ribbon-overflow-hscroll` | Ribbon horizontal overflow | manual | keep | low |
| open | `cross-undo-disabled-style` | Button visual states (base, hover, active, disabled) | manual | keep | med |
| open | `cross-inline-style-overrides` | Inline styles override CSS (effective values) | manual | keep | low |
| open | `cross-text-select-and-selection` | Text selection, context menu, tooltips | manual | fix | low |
| done | `cross-window-dpi-rendering` | Pixel scale / DPI behavior | manual | keep | med |
| open | `shell-window-size` | Initial window 1200x800, centered, resizable | ui-auto | fix | low |
| done | `shell-window-title` | Window title is a static product name | ui-auto | keep | low |
| open | `shell-lifecycle-quit` | Quit on last window closed; macOS activate branch is dead code | ui-auto | fix | low |
| open | `shell-single-instance` | No single-instance lock | manual | keep | low |
| done | `shell-external-drop-guard` | Dropping files outside the drop zone/canvas is swallowed, not navigated | ui-auto | keep | low |
| open | `shell-header-version-hardcoded` | Header version string is hand-typed and drifts from docs | manual | fix | low |
| done | `avatar-preview-3d` | 3D avatar preview (new in v5.1) | ui-auto | new | low |
| done | `font-ui-weight-600` | UI labels at CSS weight 600 in the vendored variable fonts | manual | fix | low |
| done | `window-title-dirty-marker` | Window title names the project file and marks unsaved changes | unit | fix | low |

## App.Storage

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `doc-new-transparent-background` | A new template's background starts transparent | unit | fix | low |
| done | `project-save-filename` | Save filename and dialog behavior | ui-auto | keep | low |
| open | `shell-no-persistence` | Zero persisted settings: window rect, tool state, last folder, template name | doc | fix | low |
| done | `io-download-save-dialog` | Exports and project saves go through anchor-download, relying on Chromium/Electron's Save As behavior | manual | fix | med |
| open | `shell-no-unsaved-changes-guard` | No unsaved-work prompt on close or reload | ui-auto | fix | low |
| open | `shell-userdata-footprint` | Electron writes a hidden per-user profile folder despite the 'no hidden data' claim | doc | fix | low |
| done | `shell-unsaved-work-loss` | No unsaved-changes guard on close, reload or quit | ui-auto | new | med |

## Docs

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `tests-count-claims` | Test-count claims are inconsistent across docs | doc | fix | low |
| open | `tests-manual-smoke` | docs/SMOKE_TEST.md: 4 revival regression groups + 7 core groups | manual | fix | low |
| open | `tests-windows-test-plan` | windows/TEST_PLAN.md: 15-section clean-VM release checklist | manual | fix | low |
| open | `docs-privacy-claims` | PRIVACY.md claims to preserve or correct (last updated 2026-04-20) | doc | fix | med |
| open | `docs-security-md` | SECURITY.md scope list references Electron surfaces | doc | fix | low |
| open | `docs-index-landing` | docs/index.html landing page (GitHub Pages) claims | doc | fix | low |
| open | `docs-readme-claims` | README.md feature/controls claims that the native app must satisfy | doc | fix | low |
| open | `docs-store-listing` | Store listing copy and keywords (4.1.0) | doc | fix | low |
| open | `docs-reviewer-letter` | Notes-for-certification letter: statements the native build must still make true | doc | fix | med |
| open | `docs-store-assets` | docs/store-assets and make-store-assets.py | doc | keep | low |
| open | `docs-manifest-626labs` | .626labs/manifest.json product card | doc | fix | low |
| open | `docs-feedback-md` | feedback.md: four seeded audit findings, all marked fixed | doc | keep | low |
| open | `legacy-docs-superpowers` | docs/superpowers specs and plans (v4 polish) | doc | fix | low |
| open | `docs-known-limitations` | Known limitations documented in RELEASE_NOTES that the port can close | doc | fix | low |
| open | `wordart-claim-wrap-any-size` | Landing page claims 'full text wraps' and 'add at any size'; the code does neither | doc | fix | low |
| open | `claim-fullscreen-canvas` | 'Fullscreen canvas' claim has no fullscreen feature behind it | doc | fix | low |
| open | `claim-region-click-color-picker` | Test docs say clicking a region opens a color picker; it paints the current Fill Style | doc | fix | low |
| open | `claim-network-surface-inconsistent` | Network claims contradict each other and the code | doc | fix | med |
| open | `claim-undo-everything` | Store 'Undo everything' overstates coverage | doc | fix | low |
| open | `docs-dev-workflow-claims` | Build-from-source and dev-mode instructions are Node/Electron-specific | doc | fix | low |

## Engine.Adjust

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `rgb-shift-compute` | RGB channel shift (pixel operation) | golden | fix | high |
| done | `rgb-shift-cache` | RGB shift single-entry cache | unit | fix | med |
| done | `has-adjustments` | hasAdjustments predicate | unit | keep | low |
| done | `css-filter-chain` | CSS ctx.filter brightness/contrast/saturate/hue-rotate | golden | keep | high |
| done | `rgb-shift-draw-order` | RGB shift applied before CSS filter | golden | keep | med |
| done | `imgdata-premult-note` | Premultiplied alpha behaviors across adjustments | golden | fix | high |
| done | `layers-adjust-pipeline` | Adjustment render order and RGB shift cache | golden | fix | high |

## Engine.Details

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `details-layer-creation-pipeline` | Detail layer creation pipeline (canvas, style, autocrop, insertion) | golden | keep | med |
| done | `details-mode-independence-and-selection` | Detail layers ignore selection and ride on the layer stack | unit | keep | low |
| done | `detail-collar-v` | V-Neck collar geometry | golden | keep | low |
| done | `detail-collar-crew` | Crew neck geometry (front and back) | golden | keep | low |
| done | `detail-collar-scoop` | Scoop neck geometry | golden | keep | low |
| done | `detail-collar-henley` | Henley collar (crew plus placket and 3 buttons) | golden | keep | low |
| done | `detail-cuffs` | Cuff lines (left, right, both) | golden | keep | low |
| done | `detail-hem` | Hem line | golden | keep | low |
| done | `detail-seams` | Center seam, side seams | golden | keep | low |
| done | `detail-pocket` | Chest pocket | golden | keep | low |
| done | `detail-placket` | Button placket | golden | keep | low |
| done | `detail-shoulder-stripe` | Shoulder stripe | golden | keep | low |
| done | `detail-arm-bands` | Arm bands | golden | keep | low |
| done | `detail-yoke` | Back yoke | golden | keep | low |
| done | `detail-full-set` | Full Detail Set composite | golden | keep | low |
| done | `details-pants-mode-behavior` | Details behavior in Pants mode | unit | fix | high |
| open | `details-buttons` | Details tab: 16 detail generators | golden | fix | med |

## Engine.Document

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `tpl-active-regions-swap` | Active region reference and mode variable | unit | keep | med |
| done | `state-defaults` | Initial editor state defaults | unit | keep | low |
| done | `layer-factory` | makeLayer factory defaults | unit | keep | low |
| done | `layers-model` | Layer data model and defaults | unit | fix | med |
| done | `layers-add-placement` | Add image layer: auto-fit and placement | unit | fix | med |
| done | `layers-add-drawing` | Add drawing layer from stroke canvas | unit | keep | med |
| done | `layers-duplicate` | Duplicate layer | unit | keep | low |
| done | `layers-remove` | Delete layer and reselection | unit | fix | low |
| done | `layers-visibility` | Toggle layer visibility | unit | fix | low |
| done | `layers-move-updown` | Up/Down buttons | unit | fix | low |
| done | `props-flip` | Flip H / Flip V | unit | fix | low |
| open | `btn-fit-front` | Fit Front | unit | fix | med |
| open | `btn-fit-fb` | F+B (front and back) | unit | fix | med |
| open | `btn-stretch` | Stretch (across torso) | unit | fix | low |
| open | `btn-allover` | Allover (tile on every region) | unit | keep | low |
| open | `btn-scatter` | Scatter (randomized placement) | unit | keep | low |
| done | `btn-clear` | Clear (remove all layers) | unit | keep | low |
| done | `layer-sharing-img` | Cloned layers share the Image object | unit | keep | med |
| done | `regions-state-model` | Region fill state model (regionColors map and currentFill) | unit | keep | low |
| done | `regions-click-paint` | Click region button with no layer selected: paint with current fill | unit | fix | low |
| done | `mode-switch-effects` | What switchMode changes and resets | unit | keep | med |
| open | `preset-meme` | Meme Tee preset | unit | fix | low |
| open | `preset-allover` | Allover preset | unit | fix | low |
| open | `preset-sleeve-pop` | Sleeve Pop preset | unit | fix | low |
| open | `preset-mirror` | Mirror F+B preset | unit | fix | low |
| open | `preset-scattered` | Scatter preset (randomized) | unit | keep | low |
| open | `preset-fullbleed` | Fullbleed preset | unit | keep | low |
| done | `asset-click-add` | Click thumbnail: add as layer (fit 85% in front region) | unit | keep | low |
| done | `asset-rightclick-full` | Right-click thumbnail: add at full template size | unit | keep | low |
| done | `claim-mode-per-mode-region-data` | CHANGELOG 3.0 'per-mode region data' is false: a mode switch wipes fills | unit | keep | low |
| done | `props-scale-clamp-inconsistency` | Scale bounds differ by input path | unit | fix | low |
| done | `layers-default-name-offbyone` | Default layer names are off by one from the id | unit | fix | low |
| open | `bg-color-pick-clears-image` | Picking a background color or swatch silently discards the background image | unit | keep | low |
| open | `image-decode-format-matrix` | Decoder matrix: SVG, BMP, GIF, WebP accepted by extension or MIME | unit | drop | med |
| done | `image-decode-caps` | Imported image size caps (file bytes, side length, pixel count) | unit | fix | med |
| done | `image-decode-exif-orientation` | EXIF orientation applied when an image is decoded | unit | keep | med |

## Engine.Export

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `export-composite-order` | Export composition: size, order, what is and isn't included | golden | keep | high |
| done | `export-filename-save` | Export file naming and save dialog | unit | keep | med |
| done | `export-png-encoding` | PNG encoding and alpha handling | golden | keep | med |
| done | `canvas-export-pipeline` | Export (Download template) pipeline | golden | keep | high |
| done | `shell-gpu-canvas-edge-drift` | Export PNG edges depend on Electron GPU canvas vs software canvas | golden | fix | med |
| done | `io-export-filename` | Filename derivation and sanitization | unit | keep | low |

## Engine.Fills

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `fills-normalize` | Fill normalization and legacy string fills | unit | keep | med |
| done | `fills-hex-parse` | Hex color parse and mix helpers | unit | keep | med |
| done | `fills-prng` | mulberry32 PRNG and FNV-1a seed | unit | keep | high |
| done | `fills-defaults` | Fill parameter defaults and clamps | unit | keep | low |
| done | `fills-solid` | Solid fill | golden | keep | low |
| done | `fills-linear` | Linear gradient fill | golden | keep | med |
| done | `fills-radial` | Radial gradient fill | golden | keep | low |
| done | `fills-stripes` | Stripes fill (global lattice) | golden | keep | med |
| done | `fills-checker` | Checker fill | golden | keep | low |
| done | `fills-dots` | Dots fill | golden | keep | med |
| done | `fills-camo` | Camo fill (seeded blobs on global cells) | golden | keep | high |
| done | `fills-key` | fillKey grouping identity | unit | keep | med |
| done | `region-union-rect` | unionRect | unit | keep | low |
| done | `region-connected-groups` | connectedRegionGroups (gradient spans connected faces) | unit | keep | med |
| done | `paint-regions-grouping` | paintRegions: group by fill then by connectivity | golden | keep | med |
| done | `project-and-export-dependency-note` | Export depends on fill/paintRegions code paths (spike-proven) | golden | keep | med |
| done | `fillstyle-live-edit-rule` | Live-edit rule: edits retarget every region sharing the current fill key | unit | keep | med |
| done | `tests-lib-fills` | fills.test.js: 14 tests that define the fill contract | unit | keep | med |
| done | `fill-gradient-stops` | Color stops on gradient and radial fills (new in v5.1) | unit | new | low |

## Engine.History

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `history-snapshot-coupling` | State captured by undo snapshots | unit | keep | med |
| done | `undo-model` | Undo/redo snapshot model and stack discipline | unit | keep | low |
| done | `undo-snapshot-contents` | What a snapshot captures (and what it does not) | unit | keep | high |
| done | `undo-restore` | restoreState: order of operations, async image loading | unit | fix | high |
| open | `undo-coverage-gaps` | Which actions create undo steps | unit | fix | med |
| done | `history-snapshot-memory` | Snapshot size and 30-step cap behavior | unit | fix | low |
| done | `restore-mode-switch-side-effects` | Mode switch interplay with undo | unit | keep | med |
| done | `fillstyle-undo-snapshot-timing` | Undo snapshot timing for fill edits | unit | fix | med |
| done | `mode-switch-undo-and-callers` | Mode switch undo semantics and other callers | unit | keep | med |
| open | `file-undo-snapshot-scope` | What undo/redo snapshots and which actions push | unit | fix | med |
| done | `tests-lib-history` | history.test.js: 7 tests for undo/redo stack semantics | unit | keep | low |
| open | `undo-empty-steps` | Actions that push an undo step but change nothing | unit | fix | med |
| done | `undo-rapid-restore-race` | Rapid undo/redo during async restore corrupts history | unit | fix | med |
| done | `undo-restore-ui-side-effects` | Undo/redo drops selection and doesn't touch Fill Style UI | unit | fix | low |
| done | `history-redo-stack-uncapped` | Redo stack unbounded; redo can push undo past 30 | unit | fix | low |

## Engine.ProjectIO

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `layer-fields-schema` | Layer field schema and persistence shape | unit | keep | med |
| done | `project-save-format` | Save project (.r15proj) JSON format | unit | fix | high |
| done | `project-load-flow` | Open project: file picker and restore order | unit | fix | high |
| done | `project-load-layers` | Open project: layer restoration algorithm and ordering | unit | fix | high |
| done | `project-version-compat` | Project file version compatibility policy | unit | fix | high |
| done | `file-project-format` | Project save/load (.r15proj) | unit | fix | high |
| done | `project-format-r15proj` | .r15proj JSON contract (read-compat required, shell-level facts) | unit | fix | high |
| done | `project-load-untrusted-src` | .r15proj load does no sanitization; image fields accept any URL | unit | fix | med |
| done | `project-load-no-imgsrc-hang` | Opening a project whose layers all lack imgSrc leaves stale UI | unit | fix | low |
| done | `project-template-name-not-saved` | Template name is not stored in .r15proj | unit | keep | low |
| done | `project-load-error-path-bugs` | Project load: failed or missing images skip finalize (order, nextId, selection, render) | unit | fix | med |
| done | `project-load-history-interplay` | Open project neither snapshots nor clears undo history | unit | fix | med |
| done | `project-load-field-coercion` | Project load field coercions and fallbacks | unit | fix | med |

## Engine.Render

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `mask-build` | Region mask canvas | golden | keep | med |
| done | `layer-draw-transform` | Layer draw transform and opacity | golden | keep | high |
| done | `layer-skip-rules` | Layer visibility and null image skip | golden | keep | low |
| done | `render-offscreen` | Persistent offscreen composite | golden | keep | med |
| done | `render-background` | Background image vs color with opacity | golden | keep | med |
| done | `view-region-guide` | Region guide on the canvas (view only) | unit | new | low |
| done | `render-region-fills-step` | Region fills painted over background, under layers | golden | keep | low |
| done | `render-live-preview` | Live drawing preview composite | golden | keep | low |
| done | `render-mask-step` | Mask application (destination-in) | golden | keep | med |
| done | `render-order-summary` | Full render order (contract) | golden | keep | high |
| done | `draw-preview-composite` | Live preview compositing | golden | keep | low |
| open | `fillstyle-live-render-hook` | Region fills rendered in canvas with connected-component gradient extent | golden | keep | med |
| done | `canvas-element-render` | Main canvas, zoom and compositing order | golden | keep | high |
| done | `overlay-knob-glyph-rasterization` | The rotate knob's glyph antialiasing differs at some offsets | golden | fix | low |

## Engine.Template

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `tpl-canvas-size` | Template canvas size 585x559 | unit | keep | low |
| done | `tpl-shirt-regions` | Shirt region table (18 regions) | unit | keep | low |
| done | `tpl-pants-regions` | Pants region table (18 regions) | unit | keep | low |
| open | `mode-pants-crash-hardcoded-keys` | BUG: Pants mode breaks code that hardcodes torso/arm keys | unit | fix | high |
| open | `bug-pants-mode-region-keys` | Pants mode breaks layer-adding actions (static finding) | unit | fix | high |

## Engine.Tools

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `autocrop` | autocrop of drawn canvas | unit | keep | low |
| done | `select-hit-test` | Topmost layer hit-testing and deselect | unit | keep | med |
| done | `select-rotate-drag` | Rotate drag with 45-degree snapping | unit | fix | med |
| done | `select-resize-handles` | Corner resize handles (uniform scale) | unit | fix | med |
| done | `draw-begin` | Drawing tools mousedown setup (pen, line, rect, circle, eraser) | unit | fix | med |
| done | `select-rotate-handle-hit` | Rotation handle hit test and drag start | unit | fix | med |
| done | `select-resize-handle-hit` | Corner resize handle hit and start | unit | keep | low |
| done | `select-resize-drag` | Resize drag math | unit | fix | low |
| done | `select-rotate-drag-2` | Rotate drag with 45-degree snap | unit | fix | med |
| done | `select-move-hit-and-drag` | Layer pick (topmost) and move drag | unit | fix | low |
| done | `draw-tool-start` | Drawing tools mousedown: snapshot, offscreen canvas, stroke style | unit | fix | med |
| open | `draw-pen-incremental-stroke` | Pen and eraser incremental stroking | golden | fix | high |
| done | `draw-pen-single-click` | Single click / zero-length stroke handling | unit | fix | med |
| done | `draw-shape-line` | Line tool geometry | golden | keep | low |
| done | `draw-shape-rect` | Rectangle tool geometry | golden | keep | low |
| done | `draw-shape-ellipse` | Ellipse tool geometry | golden | keep | low |
| done | `draw-commit-layer` | Mouseup commit of a drawing as a new layer | unit | keep | med |
| done | `draw-empty-no-layer` | Nothing drawn means no layer | unit | keep | low |
| done | `eraser-apply` | Eraser commit: erase from selected layer through inverse transform | golden | fix | high |
| done | `eraser-math-inverse` | lib/erase.js inverse-transform and destination-out details | golden | keep | high |
| done | `canvas-draw-tools` | Drawing tools (pen, line, rect, ellipse) and eraser | golden | fix | high |
| done | `tests-lib-erase` | erase.test.js: 5 tests for eraser transform math | unit | keep | med |
| done | `select-hit-test-rect-not-alpha` | Hit-test uses the rotated bounding rect, not pixel alpha | unit | keep | low |
| done | `draw-canvas-template-clip` | Drawings are clipped to the 585x559 template and auto-cropped with 10px pad | golden | keep | low |
| done | `eraser-click-without-move` | An eraser click without movement erases nothing | unit | keep | low |

## Engine.WordArt

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `wordart-font-list` | Word Art font list (15 fonts) and display labels | golden | keep | med |
| open | `wordart-font-loading-race` | Font load timing and initial preview | unit | fix | low |
| open | `wordart-render-measure` | renderWordArt layout: measure, line height, padding, canvas size | golden | keep | high |
| done | `wordart-style-solid` | Style: Solid (fill then optional stroke then refill) | golden | keep | med |
| done | `wordart-style-gradient` | Style: Gradient (per-line linear gradient fill + angle slider) | golden | keep | med |
| done | `wordart-style-neon` | Style: Neon glow | golden | keep | high |
| done | `wordart-style-shadow` | Style: Shadow | golden | keep | low |
| done | `wordart-style-outline` | Style: Outline (hollow, stroked in the Fill color) | golden | keep | low |
| done | `wordart-style-retro` | Style: Retro stacked 3D | golden | keep | low |
| done | `wordart-glyph-rasterization` | Word Art glyph rasterization (antialiased glyph edges) | golden | fix | low |
| open | `wordart-add-to-canvas` | Add to canvas: full-resolution render and new layer | unit | keep | med |
| open | `wordart-edge-cases` | Word Art edge cases and invalid input | unit | keep | low |
| done | `font-wordart-runtime-google` | 15 Word Art fonts loaded from Google Fonts | golden | fix | med |
| open | `text-styles` | Word Art styles (6) and render pipeline | golden | keep | high |
| open | `fonts-google-runtime` | 15 Word Art fonts load from Google Fonts at runtime (offline users get fallbacks) | golden | fix | med |
| open | `wordart-layer-name-emoji` | Word Art layer name has a sparkle emoji prefix | unit | keep | low |

## Package

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `packs-inventory-drips` | packs/drips contents (156 PNGs) | manual | keep | low |
| done | `packs-inventory-wordart` | packs/word_art contents (25 PNGs) and packs/README.md | manual | keep | low |
| open | `shell-window-no-icon` | No window or exe icon configured | manual | fix | low |
| done | `pack-contents` | Pack inventory that must ship byte-identical | unit | keep | low |
| open | `msix-identity` | MSIX Identity must be preserved exactly (same Store identity) | manual | keep | high |
| open | `msix-application-entry` | Application element: Id, Executable, EntryPoint, tiles | manual | keep | med |
| open | `msix-capabilities` | Capabilities: only runFullTrust; justification text; network capability gap | doc | fix | med |
| open | `msix-target-os` | TargetDeviceFamily min/max | manual | keep | low |
| open | `msix-arch-self-contained` | x64 only; self-contained publish layout | manual | keep | med |
| open | `msix-make-script` | make-msix.ps1 pipeline: stage, inject version, pack, optional self-sign | manual | fix | med |
| open | `msix-images` | Generated tile images (Pillow) and their required set | manual | keep | low |
| open | `msix-signing-policy` | Signing policy per channel | doc | fix | med |
| open | `ci-windows-ci` | windows-ci.yml: lint, unit tests, NSIS build, MSIX build, artifact existence | manual | fix | med |
| open | `ci-windows-release` | windows-release.yml: tag-triggered release with NSIS + MSIX assets | manual | fix | med |
| open | `ci-docs-check` | docs-check.yml: informational markdownlint | manual | keep | low |
| open | `ci-funding` | FUNDING.yml sponsor link | manual | keep | low |
| open | `parity-store-update-path` | Upgrade path from the live Electron package to the native build | manual | keep | med |

## Parity

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| done | `test-input-bridge` | Test-only input bridge (absent from Release and Store builds) | unit | new | medium |

## Retired

| Status | ID | Behavior | Check | Disposition | Risk |
|---|---|---|---|---|---|
| open | `fills-exports-api` | Module export shape and test dual-loading | none-retired | drop | low |
| open | `props-injection-safe` | Layer name never interpolated into markup | none-retired | drop | low |
| open | `drop-guard` | Block browser default open-file on drop | none-retired | drop | low |
| open | `cross-state-globals` | Shared mutable editor state | none-retired | drop | med |
| open | `keys-single-handler` | Single document.onkeydown (no keyup, no global Escape/Space) | none-retired | drop | low |
| open | `project-electron-integration-risks` | Electron-specific mechanisms in this slice that vanish | none-retired | drop | low |
| open | `shell-webprefs-node-integration` | nodeIntegration:true, contextIsolation:false in the renderer | none-retired | drop | low |
| open | `shell-menu-hidden` | Menu bar hidden via setMenuBarVisibility(false); default menu still exists | none-retired | drop | med |
| open | `shell-devtools-off` | DevTools do not auto-open | none-retired | drop | low |
| open | `build-electron-builder-config` | electron-builder config: appId, files allowlist, NSIS | none-retired | drop | high |
| open | `build-extra-resources-packs` | extraResources copies packs/**/*.png to resources/packs | none-retired | drop | low |
| open | `build-package-lock-stale` | CI uses npm install because package-lock has stale 'conundrum-r15-designer' identity | none-retired | drop | low |
| open | `tests-injection` | injection.test.js: source-grep guard (3 tests) | none-retired | drop | low |
| open | `tests-smoke-parse` | smoke.test.js: main.js and editor.js parse | none-retired | drop | low |
| open | `tests-eslint-gate` | ESLint no-undef gate across main.js, editor.js, lib, tests | none-retired | drop | low |
| open | `legacy-launcher-bat` | LaunchShirtEditor.bat opens the v3 monolith in the default browser | none-retired | drop | low |
| open | `legacy-create-shortcut` | create_shortcut.ps1 is a one-machine dev script with hardcoded paths | none-retired | drop | low |
| open | `legacy-roblox-uploader` | roblox_uploader.py: Chrome cookie extraction + Roblox upload (credential-lifting code) | none-retired | drop | high |
| open | `legacy-shirt-maker-py` | roblox_shirt_maker.py: CLI that composites art onto torso_front/back with optional Gemini | none-retired | drop | low |
| open | `legacy-variation-gen` | variation_generator.py: random shirt variations from backgrounds x art x 6 styles | none-retired | drop | low |
| open | `legacy-manifests-outputs` | upload_manifest.json, pants_manifest.json, shirts/, pants/, variations/, root PNGs | none-retired | drop | low |
| open | `legacy-scratch` | scratch/ is gitignored and holds the .NET spike | none-retired | drop | low |
| open | `shell-default-menu-accelerators` | Hidden Electron default menu still fires its accelerators (Ctrl+R reload, Ctrl+Shift+I DevTools, page zoom, F11) | none-retired | drop | med |
| open | `dist-github-nsis-channel` | Second distribution channel: unsigned NSIS installer on GitHub Releases | none-retired | drop | med |
| open | `vestigial-hidden-controls` | Hidden vestigial elements: assetType select and btnExport2 | none-retired | drop | low |
