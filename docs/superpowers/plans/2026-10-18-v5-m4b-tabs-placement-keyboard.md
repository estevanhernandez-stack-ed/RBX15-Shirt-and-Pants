# RBX15 v5 M4b: the Layout, Details and Text tabs, Placement and Presets, the keyboard, and the ribbon re-measure. Implementation plan

Drafted 2026-10-18 by a planning agent and reviewed by the controller. Sources: origin/main at 9175fd4, the ledger (`docs/port/parity-ledger.json`, 429 rows), the spec (`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`), the M4a plan and its Cart run (`docs/cart-runs/2026-10-16-m4a-files-assets-bridge/`), and the memory note `v5-m3-carry-forwards.md`.

> **For /take:** each `- [ ]` item below is one Cart item: one worker, one branch, about 1-2 h. Run the items one at a time, in order. Items 3-6, 7-9 and 12-16 edit the same XAML, `Composition.cs`, `TestShell.cs` and `ShellTests.cs`, so running them in parallel would cause merge conflicts.

**M4b is the second half of the spec's §7 M4 row.** M4a shipped files, assets, dialogs, the status strip and the test input bridge. M4b brings back the Layout, Details and Text tabs, the Placement group, the Presets, "Center on front torso", "Keys: ON/OFF" and the rest of 4.1's keyboard. It also moves Fill Style and Template Regions from Home back to Layout and re-measures the ribbon. **Done when:** every open ledger row that is not an upgrade, Docs, Package or Retired row is closed under the honesty rule, or deferred below with a reason.

Already done, so not in scope: the app icon (`shell-window-no-icon`, commit 36d4e16, PR #52), the header mark (e96c0bd), Pants mode for the MODE toggle, imports and the region grid, the conflict between `fillstyle-type-buttons` and `fillstyle-ui-not-synced-from-region` (settled in `FillStyleViewModel`: Current lives only in the view-model and the controls bind to it, so both rows hold), and v5.1's 3D preview, palette, gradient stops, limb caps and ALL tile, transparent default and region guide.

---

## Global constraints

The M4a plan's Global Constraints apply as written. The main ones: the engine never references WPF; goldens are frozen; no global synthetic input (`grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` prints exactly one line, in `src/RBX15.App.Tests/BridgeExclusionTests.cs`); implementers never launch the app; StaThread view tests; one composition root; hide what does not work yet; the ledger honesty rule; conventional commits with the model's Co-Authored-By trailer. M4b adds these:

- **Items 2-15 close no ledger rows.** Each one ends its commit body with `Ledger evidence: <row-id> <- <TestClass.Method>, ...` for its rows. Item 16 closes rows from that evidence, so `parity-ledger.json` never conflicts between branches. This is the M4a model.
- **Display preflight:** until item 1 merges, an unattended run checks `reg query "HKCU\Control Panel\Desktop\WindowMetrics" /v AppliedDPI` and must see `0x60` before item 1 starts. After item 1, the App suite passes at 100%, 125% and 150%.
- **No new mutable state in the engine.** Scatter takes an injected `Random` (spec §5). The app passes `Random.Shared` and tests pass `new Random(seed)`.
- **One undo step per action.** Every new command goes through one `EditorSession.Apply` or `ApplyWithSelection`. A command that changes nothing records nothing (`Document.ContentEquals`). Slider and color gestures use the existing `BeginGesture` and `SliderGestureTracker` pattern (see `FillStyleViewModel`).
- **Ribbon panes are UserControls** (`Views/LayoutRibbon.xaml`, `DetailsRibbon.xaml`, `TextRibbon.xaml`), each with its own horizontal `ScrollViewer` and a 12,8 margin, as `HomeRibbon.xaml` has.
- **Pixel values come from 4.1** (`editor.html`, `styles.css`, the ledger rows). A value with no 4.1 source is tagged "provisional; M4b look sign-off" and listed for item 15.
- **Verify, every item:** `dotnet build src/RBX15.slnx -c Release -warnaserror`, then `dotnet test src/RBX15.slnx -c Release --no-build` (0 failed, Parity.Tests count unchanged), then the synthetic-input grep. Each item reports its Engine and App test counts. No count is fixed in advance, because v5.1 moved the baseline; item 1 measures it first.

## Decisions for Este before the run

Each has a recommended answer. The run applies the recommendation unless Este says otherwise.

| # | Question | Recommended | If declined |
|---|---|---|---|
| B1 | With the transparent default (`doc-new-transparent-background`, BgOpacity 0), a 4.1 swatch click, color pick or Background image changes nothing visible, because 4.1 never touches opacity. | When `BgOpacity == 0`, a swatch, color or image pick also sets opacity to 100%, in the same single undo step. Appended as a divergence to `layout-background-swatches` and `layout-bg-settings`. The region guide is view-only and drawn under the composite, so an opaque background covers it as intended. Export is unchanged. | Keep 4.1 exactly. The status strip says "Background opacity is 0%. Raise it to see the color." |
| R1 | `shell-layout-skeleton`'s divergence promises a fixed ribbon height. At M3b Este kept Home content-sized (100 DIP against 4.1's 121). | One fixed height: the tallest pane, measured with a layer selected. All four panes share one Grid cell, and inactive panes are `Visibility.Hidden`, which keeps their size. Item 12 reports the height. If Layout makes Home more than 24 DIP taller than today, item 12 holds for Este. | Content-sized per tab, with the divergence reworded. |
| K1 | Which focused controls keep a key that is also a shortcut? | Text fields (every `TextBoxBase`, so the Word Art text and size boxes), the layer list (Up, Down, Alt+Up, Alt+Down, Delete), a focused region tile (Delete, Shift+F10, Menu, per `layout-template-regions`), the 3D viewport (arrows, Home), and any open color picker popup. Everything else, including sliders, checkboxes, swatches, buttons and ribbon tabs, never traps a shortcut (`kbd-input-focus-trap`). Space and Enter on a focused button stay WPF's own (`kbd-focused-button-enter-space`, keep). | Text fields only. The layer list then loses arrow navigation, which is an accessibility regression. |
| K2 | How are character shortcuts matched? 4.1 matched `e.key`, the character, for `[ ] { } + = - _ r v b l u o e`. | Characters come from the window's `PreviewTextInput`, so they follow the keyboard layout as 4.1 did. Arrows, Delete, Backspace and Ctrl chords come from `PreviewKeyDown` and KeyBindings. | Virtual keys only (`OemOpenBrackets` and so on). Non-US layouts then differ from 4.1, recorded as a divergence. |
| K3 | `kbd-shortcuts`, `keys-tool-shortcuts` and `kbd-ctrl-combo-tool-toggles` promise "Ctrl+V pastes", which is an M5 upgrade. | M4b proves that Ctrl+V no longer toggles a tool, and item 16 narrows the Ctrl+V paste clause to M5's new paste row ("Narrowed in M4b"). | The three rows stay open until M5. |
| T1 | Word Art with long text at size 300 throws `InvalidOperationException` (carry-forward from M2b). | `WordArt.Render` refuses a full render above the image caps (16384 per side, 50 MP) with a typed failure. The Text tab shows "Text is too large to add. Shorten it or lower the size." on the status strip and records no step. The preview still renders scaled to fit. | Clamp the size silently. |
| T2 | Preview resolution | The preview renders at device pixels (sharp at 125% and above). Parity applies only to the full-resolution output (ledger `wordart-preview-live` quirk). | 200x80 logical pixels, soft on HiDPI as in 4.1. |
| W1 | `wordart-font-list` and `fonts-google-runtime` ask for all 15 fonts compared against 4.1. The frozen goldens cover 3 fonts. | Item 8 adds a controller-run capture: `tools/parity-ref/wordart-ref.js`, with the M2b Task 3 Step 21 procedure, writes 15 new PNGs ("RIZZ", Solid, every font) to `tests/goldens/app/wordart-fonts/`. They are held to the Word Art rule and frozen from that commit. | Narrow both rows to the 3 golden fonts plus a test that each of the 15 renders in its vendored face with no fallback. |
| F1 | Item 3 moves Fill Style and the region grid back to a Layout tab, as in 4.1. On 2026-10-18 Este reworked both on Home into one fill tool (no divider, "CLICK REGIONS TO FILL", gradient stops, limb caps, ALL; ledger ribbon-fill-regions-joined). | Keep Fill Style and the region grid on Home as the one fill tool. Layout carries Background, BG Settings, Placement and Presets. Item 3 shrinks to the fill rows' remaining checks, recorded as a divergence on `layout-fill-style` and `layout-template-regions` ("4.1: on the Layout tab. v5: on Home, next to the tools"). Item 12 measures the ribbon with that split. | Move them to Layout as the plan drafted. |

---

## 1. Scope table: every M4b row and its item

98 rows. None is unassigned.

| Item | Rows |
|---|---|
| 1 | `asset-resizable-browser` |
| 2 | `ribbon-tab-switch`, `ribbon-tabs-switching` |
| 3 | `layout-fill-style`, `layout-template-regions`, `fillstyle-type-buttons`, `fillstyle-angle-scale`, `fillstyle-ui-not-synced-from-region`, `fillstyle-live-render-hook`, `regions-button-swatch-paint`, `fills-to-css`, `home-mode-toggle` |
| 4 | `btn-fit-front`, `btn-fit-fb`, `btn-stretch`, `btn-allover`, `btn-scatter`, `preset-meme`, `preset-allover`, `preset-sleeve-pop`, `preset-mirror`, `preset-scattered`, `preset-fullbleed` |
| 5 | `home-placement-group`, `placement-selection-guard`, `presets-six-buttons`, `layout-presets`, `props-center-front`, `layers-selected-panel` |
| 6 | `bg-palette`, `bg-swatches`, `layout-background-swatches`, `bg-color-opacity`, `bg-image`, `layout-bg-settings`, `bg-color-pick-clears-image`, `io-image-formats`, `image-decode-format-matrix` |
| 7 | `details-settings-controls`, `details-buttons-inventory`, `details-settings`, `details-buttons`, `details-layer-creation-pipeline` |
| 8 | `wordart-font-list`, `fonts-google-runtime`, `wordart-edge-cases`, `wordart-render-measure`, `text-styles`, `wordart-layer-name-emoji` |
| 9 | `wordart-state-defaults`, `text-inputs`, `text-preview`, `wordart-preview-live`, `wordart-font-loading-race`, `text-add-to-canvas`, `wordart-add-to-canvas` |
| 10 | `kbd-shortcuts`, `keys-text-input-guard`, `kbd-input-focus-trap`, `keys-toggle-button`, `home-keys-toggle`, `keys-tool-shortcuts`, `kbd-ctrl-combo-tool-toggles`, `kbd-focused-button-enter-space`, `keyboard-tool-toggle-interplay`, `tools-toggle-off`, `home-tool-buttons`, `draw-tool-switch-midstroke` |
| 11 | `keys-undo-redo`, `undo-buttons-shortcuts`, `keys-duplicate-ctrl-d`, `keys-delete`, `keys-selection-gate-and-modifiers`, `keys-arrow-nudge`, `keys-rotate-brackets`, `kbd-bracket-rotate-negative-drift`, `keys-scale-plus-minus` |
| 12 | `shell-layout-skeleton`, `shell-responsive-breakpoint`, `ribbon-group-structure` |
| 13 | `render-pipeline-canvas-reset` |
| 14 | `undo-coverage-gaps`, `undo-empty-steps`, `file-undo-snapshot-scope`, `bug-pants-mode-region-keys`, `mode-pants-crash-hardcoded-keys` |
| 15 | `asset-grid-thumbnails`, `panel-drop-zone`, `props-adjust-sliders`, `draw-pen-incremental-stroke`, `shell-header`, `shell-header-version-hardcoded`, `theme-tokens-shape-motion-type`, `cross-undo-disabled-style`, `cross-inline-style-overrides`, `cross-text-select-and-selection`, `project-startup`, `shell-single-instance` |
| 16 | Closes all of the above from the evidence. |

Rows that cut across items close at item 16 only when every contributing item has merged:

- `keyboard-tool-toggle-interplay` and `kbd-shortcuts`: items 10 and 11.
- `home-mode-toggle`: item 3 (region labels) and item 14 (the Pants matrix).
- `layers-selected-panel`: item 5, plus item 15 for the adjustment narrowing.

### Deferred (9 rows open, outside M4b)

| Row | To | Reason |
|---|---|---|
| `shell-no-persistence` | M5 | Its divergence promises autosave, recents and the jump list. |
| `shell-no-unsaved-changes-guard` | M5 | The prompt shipped in M4a. The divergence still promises 60 s autosave and recovery (M4a ruling). |
| `shell-lifecycle-quit` | M5 | Its divergence promises autosave and crash recovery on close. |
| `shell-userdata-footprint` | M5, then M6 | It discloses recents and autosave storage. PRIVACY.md is M6's docs pass. |
| `shell-window-frame` | M6 | Ruled by M4a: launch-size, resize and caption clauses (MAN-2 was a defect check only). |
| `shell-window-size` | M6 | Same M4a ruling. |
| `packs-resolve-dir` | M6 | Needs a run from an installed MSIX. M6a packaging has merged, so this could close early in M6. |
| `asset-objecturl-lifecycle` | M6 | Needs a measured run with 500 images and a 20,000-file folder (M4a ruling). |
| `a11y-focus-rings` | M6 | M6's accessibility pass (M4a ruling). M4b still gives every new control an automation name and the `FocusRing` style. |

Excluded by owner (53 rows): Retired 25 (close at cutover), Docs 20 (M6), Package 8 (M6).

M5 note: none of the four upgrades has a ledger row yet (paste, recents, jump list, file association). M5's plan adds them. K3's narrowing points at the paste row M5 will add.

---

## 2. Run list

- [ ] **1. The App tests no longer depend on the display scale**
  Rows: `asset-resizable-browser` (adds the missing SizeNS cursor test).
  What to build (App tests only):
  - The 6 tests that fail at 125% compute their expected positions through the element's real DPI, with device-pixel rounding, instead of assuming 96 DPI:
    - `CanvasLayerAutomationTests.The_bounds_are_the_rotated_box_on_screen`
    - `AssetLibraryPanelTests.The_handle_resizes_the_browser_from_its_rendered_height_within_the_clamps`, `In_the_window_the_resize_clamps_at_70_percent_of_the_client` and `A_drag_starts_past_the_distance_and_a_release_off_the_canvas_adds_nothing`
    - `InputBridgeTests.Hardware_mouse_reports_are_pinned_to_the_bridge_point_until_it_is_disposed`, `A_canvas_move_drag_is_one_undo_step` and `A_release_outside_the_window_ends_the_drag_where_it_was_released`
  - The real DPI comes from `PresentationSource.FromVisual(v).CompositionTarget.TransformToDevice` or `VisualTreeHelper.GetDpi`. Add a shared helper `src/RBX15.App.Tests/DeviceRounding.cs` with `ToDevice` and `Snap`.
  - Where an element is tested outside a window, theory rows run it at 96, 120 and 144 DPI through `VisualTreeHelper.SetRootDpi`. Off-screen MainWindow tests can't fake DPI (the HwndSource takes the monitor's), so they compute through the real DPI and carry no DPI rows.
  - Also check `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits`, which M4a recorded as failing off 100%. Fix it the same way if it still fails.
  - Add `AssetLibraryPanelTests.The_handle_shows_the_ns_resize_cursor_and_restores_it_after_release`.
  Done when:
  - The App suite passes at 100% (CI).
  - The listed tests' DPI rows pass at 96, 120 and 144 under `SetRootDpi`.
  - If the builder's display is at 125%, the full App suite passes there too. Report AppliedDPI.
  - No product code changes.
  Verify: the standard three, plus `dotnet test src/RBX15.App.Tests -c Release --no-build --filter "FullyQualifiedName~CanvasLayerAutomationTests|FullyQualifiedName~AssetLibraryPanelTests|FullyQualifiedName~InputBridgeTests|FullyQualifiedName~CanvasViewTests"`.
  Stop class: go. Risk: a test that only passes because of a tolerance is not a fix. Tolerances are allowed only for device-pixel rounding (half a device pixel) and must say so in a comment.

- [ ] **2. Four ribbon tabs and tab switching**
  Rows: `ribbon-tab-switch`, `ribbon-tabs-switching`.
  What to build (App):
  - `MainViewModel.ActiveTab` (`enum RibbonTab { Home, Layout, Details, Text }`, ObservableProperty, starts Home). It is not in the document, not an undo step and not saved.
  - `MainWindow.xaml`: `RibbonTabs` gets `LayoutTab`, `DetailsTab` and `TextTab` RadioButtons in 4.1's order. They keep the existing `RibbonTab` style, no GroupName (wave-6 rule) and the automation names "Layout tab" and so on, bound two-way to ActiveTab.
  - `RibbonPane` becomes a Grid holding four panes in one cell. The inactive panes are `Hidden` (R1), and switching never changes the document.
  - Empty `Views/LayoutRibbon.xaml`, `DetailsRibbon.xaml` and `TextRibbon.xaml` panes with their ScrollViewers. The Layout, Details and Text tabs stay hidden until items 3, 7 and 9 give them content (the hide rule): each RadioButton is `Collapsed` until its pane has a group. Item 2 adds the tab plumbing and its tests with the tabs visible only in a test hook (`MainWindow.ShowAllTabsForTest`). Alternatively, item 2 merges together with item 3; pick one when the run starts and record it.
  - Tabs take Tab focus and Space/Enter, as 4.1's buttons did. Arrows are not tab navigation (K1).
  Done when:
  - Clicking each tab makes exactly one tab checked and one pane visible.
  - The active tab has cyan text, a navy-deep fill and the gradient underline (style unchanged).
  - The window starts on Home.
  - Switching tabs leaves `Document`, `UndoCount` and `IsDirty` unchanged.
  - Two windows don't share tab state.
  - `ShellTests.The_main_window_builds_the_skeleton` expects four tabs.
  - New `RibbonTabsTests` covers these lines.
  Verify: the standard three.
  Stop class: go. Risk: `Hidden` panes stay in the UIA tree as off-screen. Assert that the tab's automation name is reachable and that hidden panes report `IsOffscreen`.

- [ ] **3. The Layout tab: Fill Style and Template Regions move back from Home**
  Rows: `layout-fill-style`, `layout-template-regions`, `fillstyle-type-buttons`, `fillstyle-angle-scale`, `fillstyle-ui-not-synced-from-region`, `fillstyle-live-render-hook`, `regions-button-swatch-paint`, `fills-to-css`, `home-mode-toggle` (the region-label half).
  What to build (App):
  - New `LayoutViewModel` with `FillStyle` and `Regions`, which move out of `HomeViewModel`. Constructor: `(EditorSession, IColorPicker)`. Item 6 adds Background and item 5 adds Presets.
  - `MainViewModel.Layout` is a required member, wired at Composition's two anchors and in `TestShell.Main`.
  - Remove `FillStyleGroup` and `RegionsGrid` from `HomeRibbon.xaml`, keeping the "joined" look from `ribbon-fill-regions-joined` (no divider, "CLICK REGIONS TO FILL"). Host both in `LayoutRibbon.xaml`. Show the Layout tab.
  - Tests that change: move the Fill and Regions facts in `HomeRibbonViewTests` (lines ~60-250 and ~346) to a new `LayoutRibbonViewTests`; replace `HomeTestKit.Home` with `LayoutTestKit.Layout`; `FillStopsTests` (~line 433: `new HomeRibbon` becomes `new LayoutRibbon`); `LedgerScenarioTests` (~line 845: `main.Home.Regions` becomes `main.Layout.Regions`); `ShellLookTests` (~line 62).
  - Add `LedgerScenarioTests.Opening_a_project_with_a_camo_region_leaves_fill_style_at_its_defaults` (`fillstyle-ui-not-synced-from-region`).
  - Add `LayoutRibbonViewTests.Region_labels_follow_the_mode`: Torso, Right Arm and Left Arm become Waist, Right Leg and Left Leg, and switching back and undoing restores the fills.
  - Region swatches: `RegionSwatch` already paints through the Skia fill painter. Add a test that a stripes swatch at 0° and a linear swatch at 0° run left to right, the canvas convention (`fills-to-css`, the approved divergence).
  Done when: Home holds Mode, Tools, Brush, Edit, File and Settings only; Layout holds Fill Style and Regions; every moved test passes from its new home; the new facts pass; `fillstyle-live-render-hook`'s evidence lists the frozen render goldens (`fill-*`, `pants-fills`, `fill-mixed-groups`) and the live-edit facts.
  Verify: the standard three.
  Stop class: go. Risk: a large test-file move. Rename facts only when the old name is wrong; the ledger evidence cites method names.

- [ ] **4. Placement and preset geometry in the engine**
  Rows: `btn-fit-front`, `btn-fit-fb`, `btn-stretch`, `btn-allover`, `btn-scatter`, `preset-meme`, `preset-allover`, `preset-sleeve-pop`, `preset-mirror`, `preset-scattered`, `preset-fullbleed`.
  What to build (engine): `src/RBX15.Engine/Placement.cs` and `src/RBX15.Engine.Tests/PlacementTests.cs`. Every region comes from `Template.Slot`, so all of it works in Pants. Each op returns its input unchanged when there is no such layer. Clones share the image and keep every property, are appended on top, and the original stays selected.
  - `FitFront(Document, int id)`: scale = min(w/iw, h/ih) × 0.9 in the Body Front slot, centered, rotation 0.
  - `FrontAndBack(EditorSnapshot, int id, int newId)`: FitFront, plus a "<name> (back)" clone at the same scale centered in Body Back. Not mirrored.
  - `Stretch(EditorSnapshot, int id, int newId)`: fit into the union of the Body Right, Front and Left slots (shirt: 165, 74, 260x128) with no 0.9 factor, plus a back clone at a full fit.
  - `Allover(EditorSnapshot, int id, Func<int> nextId)`: one "<name> tile" clone per region in `Template.Regions(mode)` order, at 0.8.
  - `Scatter(EditorSnapshot, int id, Func<int> nextId, Random rng)`: 3 clones when r.w > 100, otherwise 1; 4.1's formulas, in 4.1's order of rng calls; "<name> scat".
  - `SleevePop(EditorSnapshot, int id, Func<int> nextId)`: 8 "<name> sleeve" clones in the order rarm front, back, left, right, then larm, through RightLimb and LeftLimb slots (legs in Pants), at 0.8.
  - `Fullbleed(Document, int id)`: scale = max(585/iw, 559/ih), centered, no clamp (spec: presets compute exact fits).
  - `CenterOnFront(Document, int id)`: centered in the front slot by the unrotated box; scale and rotation unchanged.
  - Meme is FitFront, Mirror is FrontAndBack, and the presets' Allover and Scatter are the same functions.
  Done when the ledger's own numbers pass:
  - Fit Front, 200x100 layer: scale 0.576, x 237.4, y 109.2.
  - Stretch, 300x100 layer: scale 0.8667, x 165, y 94.67.
  - Fullbleed: 400x400 gives scale 1.4625 and x 0; 100x100 gives scale 5.85, x 0, y -13.
  - Meme, 300x200 layer: scale 0.384.
  - Allover adds 18 layers in region order; Sleeve Pop adds 8 in the stated order.
  - Scatter with a seeded rng adds 26 in Shirt mode, with every scale ≥ 0.02, rotation in [-30, 30) and x in [r.x, r.x + 0.7w).
  - Every op gives the same counts in Pants (Theory rows Shirt and Pants), with no exception.
  - A missing id returns the same instance.
  Verify: the standard three.
  Stop class: go. Risk: 4.1's Stretch box is hard-coded, so assert that the slot union equals (165, 74, 260, 128) in Shirt and record what it is in Pants. Scatter's order of rng calls must match 4.1's (scale, x, y, rotation per copy), or the "same seed" claim in the tests is meaningless.

- [ ] **5. Placement group, Presets group and "Center on front torso"**
  Rows: `home-placement-group`, `placement-selection-guard`, `presets-six-buttons`, `layout-presets`, `props-center-front`, `layers-selected-panel`.
  What to build (App):
  - `PlacementViewModel(EditorSession, ToolsViewModel?, Func<Random>)` with FitFront, FrontBack, Stretch, Allover and Scatter commands (labels and tooltips from `editor.html:63-76`), shown as a two-row group on Home after Brush.
  - `PresetsViewModel` with Meme Tee, Allover, Sleeve Pop, Mirror F+B, Scatter and Fullbleed in a 3-column grid, as the first group on Layout.
  - Both groups bind `Visibility` to `HasSelection` (`Collapsed` when nothing is selected, spec §4). Every command is one `ApplyWithSelection` over `Placement`.
  - `LayerPropertiesPanel`: a full-width `CenterFrontButton` "Center on front torso" (the label is kept in Pants) whose command is one undo step.
  - Tests that change: `HomeRibbonViewTests` fact at ~line 118 (now asserts Placement appears only with a selection; Keys stays absent until item 10) and `LayerPropertiesPanelTests` ~line 118 (the button now exists).
  - New `PlacementGroupTests` and `PresetsGroupTests`.
  Done when:
  - With nothing selected, both groups are collapsed and running any command adds no history.
  - With a selection, each button gives item 4's geometry in exactly one undo step. Presets push one step, not 4.1's two.
  - Center is one step and leaves scale and rotation alone.
  - The Pants rows pass.
  Verify: the standard three.
  Stop class: go. Risk: collapsing groups shifts the ribbon on every selection. Item 12 measures with the groups shown.

- [ ] **6. The Background and BG Settings groups on Layout**
  Rows: `bg-palette`, `bg-swatches`, `layout-background-swatches`, `bg-color-opacity`, `bg-image`, `layout-bg-settings`, `bg-color-pick-clears-image`, `io-image-formats` (the Background picker half), `image-decode-format-matrix`.
  What to build:
  - Engine (`DocumentOps`): `SetBackgroundColor(Document, string hex)`, which clears BgImage; `SetBackgroundImage(Document, SKImage)`; `SetBackgroundOpacity(Document, double)`.
  - App: `BackgroundViewModel(EditorSession, IColorPicker, IFileDialogs, IStatus)`. It holds the 12 `BgSwatch(Name, Hex)` in 4.1's order with name tooltips, as focusable buttons with automation names. The Color well uses the themed picker as one gesture with live preview. `OpacityPercent` is a slider gesture. The "Background" button picks one image, using the same filter as Add image (R14 formats, SVG refused with a message).
  - The controls re-read the document on `SessionChange.Document` (undo, open and so on). Unlike Fill Style, 4.1's restoreState re-synced the bg controls.
  - Apply decision B1.
  - An image that fails to decode adds no undo step and names the file on the status strip (`bg-image` divergence; the background route of `diag-console-only-errors`). Picking the same file twice acts twice.
  - Groups "BACKGROUND" and "BG SETTINGS" go on Layout between Presets and Fill Style.
  Done when:
  - Clicking each swatch sets that color, clears an image and undoes in one step.
  - A 30-tick color or opacity drag is one step.
  - The keyboard arrow on the opacity slider records a step.
  - B1's rule is pinned (a swatch at opacity 0 gives opacity 1 in one step).
  - A corrupt image gives no step plus a status line.
  - Format matrix: PNG, JPG, GIF (first frame), WebP and BMP decode through the background route; SVG gives the message.
  - Undo and Open re-sync Color and Opacity.
  - A 100x100 background image at 40% matches the frozen `bg-image-opacity` render golden (already proven; cite it).
  Verify: the standard three.
  Stop class: go. Risk: B1 is a behavior change, so it needs Este's answer first. Large images go through `Images` caps (16384 per side, 50 MP).

- [ ] **7. The Details tab**
  Rows: `details-settings-controls`, `details-buttons-inventory`, `details-settings`, `details-buttons`, `details-layer-creation-pipeline`.
  What to build (App):
  - `DetailsViewModel(EditorSession, IColorPicker)`: Color "#333333", Width 2 (1-8), Stitch false. The values are read only when a button is clicked.
  - Groups Neckline, Edges, Seams and Accents with the 16 buttons, labels and order from `editor.html:187-222`. The ids and names come from `Details.Id` and `Details.LayerName`. Full Set has the pink accent (#f22f89 border, #ff5aa3 text).
  - Click: `Details.Create(type, mode, style, session.TakeId())`. A null result records no step. Otherwise one `ApplyWithSelection` adds the layer on top and selects it, then a `LayersAdded` event sets the Select tool (wired in Composition like `import.LayersAdded`).
  - `DetailsRibbon.xaml` with groups Settings, Neckline, Edges, Seams and Accents. Show the tab.
  - New `DetailsTabTests`.
  Done when:
  - The labels, group titles and resulting layer names match the ledger list.
  - The defaults assert.
  - Changing the width after a creation leaves the earlier layers untouched.
  - Each button adds one layer in one step, selected, with the Select tool on, at the crop origin the engine computes. The 37 frozen detail goldens already cover pixels and offsets; cite them.
  - Pants rows produce layers with no exception.
  - A fully transparent color creates no layer and no step.
  Verify: the standard three.
  Stop class: go.

- [ ] **8. Word Art engine hardening (and, if W1 is approved, the 15-font capture)**
  Rows: `wordart-font-list`, `fonts-google-runtime`, `wordart-edge-cases`, `wordart-render-measure`, `text-styles`, `wordart-layer-name-emoji`.
  What to build:
  - Engine fixes for the M2b carry-forwards:
    - NaN or negative stroke width goes through 4.1's rule (`+v || 0`, then clamped 0-20).
    - Long or oversized text gives a typed `WordArtTooLarge` result (T1) instead of throwing.
    - Dispose the `SKPixmap` in `WordArt.CreateLayer`.
    - `WordArt.Covers` caches one `SKFont` per family and size instead of one per character.
    - `Fonts.Get` uses `Lazy` so the GetOrAdd race cannot leak a typeface.
  - Add `WordArt.FontOptions`: 15 `(Family, Label)` pairs from `editor.html:230-246`.
  - Tests: an edge-case table (empty text gives "TEXT"; size 0, NaN or empty gives 64; 5000 clamps to 300; stroke 0 disables the stroke, except Outline, which forces 3); a 20-UTF-16-unit name cut that may split a surrogate pair; all 15 fonts render "RIZZ" in their vendored face (`Primary` runs only); a preview-path render of 40 keystrokes stays under a time budget (report the time, assert under 50 ms per frame).
  - If W1 is approved: a controller-run capture of 15 PNGs with `tools/parity-ref/wordart-ref.js` (`env -u ELECTRON_RUN_AS_NODE`, CPU raster, fonts from `assets/fonts`) into `tests/goldens/app/wordart-fonts/`, plus `index.json` provenance, and `GoldenWordArtFontTests` under `GoldenRule.AssertWordArtMatches`.
  Done when: the tests pass, the existing 22 Word Art goldens and `WordArtSwapTests` still pass unchanged, and nothing throws for text of 2,000 characters at size 300.
  Verify: the standard three, plus `dotnet test src/RBX15.Engine.Tests -c Release --no-build --filter "FullyQualifiedName~WordArt|FullyQualifiedName~Fonts"`.
  Stop class: go. The capture step is the controller's; a worker stops there and reports. Risk: Word Art rendering parity is the spec's named top risk. Any interior difference above 1 level means an engine bug, never a tolerance change.

- [ ] **9. The Text tab**
  Rows: `wordart-state-defaults`, `text-inputs`, `text-preview`, `wordart-preview-live`, `wordart-font-loading-race`, `text-add-to-canvas`, `wordart-add-to-canvas`.
  What to build (App):
  - `TextViewModel(EditorSession, IColorPicker, IStatus)`:
    - Text "RIZZ" (multi-line; `AcceptsReturn`; placeholder "Type text...")
    - Font (the 15 options, default Anton; items are not drawn in their own face, as in 4.1)
    - Style (Solid, Gradient, Neon, Shadow, Outline, Retro; one active)
    - Size 64 (a TextBox clamped 8-300 when committed)
    - Fill #17d4fa, Stroke #000000 and Grad #f22f89 (themed picker)
    - StrokeWidth 0-20 (3) and Angle 0-360 (0)
    - `Preview : ImageSource`, rendered through `WordArt.Render(s, 200, 80)` at device scale (T2), centered in a black box with a #333 border (the approved divergence)
    - `AddCommand`
  - Every control change re-renders the preview, coalesced at Dispatcher `Background` priority. The first preview exists before the first frame (no font gate).
  - Add: `WordArt.CreateLayer` gives one `ApplyWithSelection`; select the layer, set the Select tool, name "✨ RIZZ [Anton]". A T1 failure gives a status line and no step.
  - `TextRibbon.xaml` with groups Text, Colors & Size, Style and Preview. Show the tab.
  - New `TextTabTests`.
  Done when:
  - Every default asserts.
  - Solid is the only active style.
  - The preview is non-null right after construction.
  - Each control change updates the preview within one dispatcher pass.
  - Add gives one layer named "✨ RIZZ [Anton]" with scale, x and y equal to `WordArt.CreateLayer`'s within 1e-6, selected, Select tool on, undone in one step. The Pants row passes.
  - Typing V, Delete or arrows in the text box changes neither the tool nor the layers (the TextBoxBase guard).
  - A 2,000-character text at size 300 gives the status line and no exception.
  Verify: the standard three.
  Stop class: go. Risk: the bridge's keyboard device raises KeyDown, not TextInput, so the smoke sets text through UIA ValuePattern, not typing. Preview cost per keystroke depends on item 8's Covers fix.

- [ ] **10. The keyboard router: tool keys, the Keys toggle and focus rules**
  Rows: `kbd-shortcuts`, `keys-text-input-guard`, `kbd-input-focus-trap`, `keys-toggle-button`, `home-keys-toggle`, `keys-tool-shortcuts`, `kbd-ctrl-combo-tool-toggles`, `kbd-focused-button-enter-space`, `keyboard-tool-toggle-interplay`, `tools-toggle-off`, `home-tool-buttons`, `draw-tool-switch-midstroke`.
  What to build (App):
  - `src/RBX15.App/Controls/ShortcutRouter.cs`, a pure function of its inputs:
    - `ShortcutAction? Route(Key key, ModifierKeys mods, IInputElement? focused, bool keysEnabled)`
    - `ShortcutAction? RouteText(string text, ModifierKeys mods, IInputElement? focused, bool keysEnabled)`
    - `Execute(ShortcutAction)`
  - `MainWindow` wires `PreviewKeyDown` and `PreviewTextInput` to it (K1, K2).
  - Bare tool keys v, b, l, u, o, e go to `ToolsViewModel.ToggleCommand`.
  - Any Ctrl or Alt held means no tool toggle, so Ctrl+V, Ctrl+B and the others do nothing to the tool (K3).
  - Keys OFF disables bare keys only. Ctrl chords always work.
  - Add `KeysViewModel` (Enabled; label "Keys: ON" or "Keys: OFF"; colors #2d4a2d/#7fff7f and #4a2d2d/#ff7f7f; tooltip "Toggle keyboard shortcuts"; session-only) to Home's Tools group.
  - Tool tooltips get their letters back: "Select/Move (V)" and so on. Update `ToolsViewModelTests` (~line 16) and `HomeRibbonViewTests` (~line 256).
  - A tool change mid-stroke cancels the stroke cleanly (`CanvasInteraction.Cancel`, the `draw-tool-switch-midstroke` divergence).
  Done when, as table-driven tests of `Route` (focus: none, TextBox, Slider, CheckBox, Button, layer row, region tile, 3D viewport; modifiers: none, Shift, Ctrl, Alt; Keys: on and off):
  - B twice gives Pen and then Select. V twice stays Select. Shift+B equals b. Ctrl+E and Ctrl+V do nothing to the tool.
  - After a Slider has focus, V still switches the tool.
  - In a TextBox, nothing fires.
  - Keys OFF blocks V but not Ctrl+Z.
  - The Keys button flips its label and colors.
  - Pressing V mid-stroke leaves no live preview and no layer.
  - Space on a focused Scatter button repeats the click (WPF's own; pinned by a RaiseEvent view test).
  - `ShellTests.The_window_binds_only_ctrl_z_and_the_three_file_chords` is rewritten to the full chord list, after item 11.
  Verify: the standard three.
  Stop class: go. Risk: a window-level PreviewKeyDown can break keyboard accessibility (ScrollViewers, ListBox, popups). K1's exemption list is the contract, and each exemption has its own test row.

- [ ] **11. Layer keys, Undo and Redo, Duplicate and Delete**
  Rows: `keys-undo-redo`, `undo-buttons-shortcuts`, `keys-duplicate-ctrl-d`, `keys-delete`, `keys-selection-gate-and-modifiers`, `keys-arrow-nudge`, `keys-rotate-brackets`, `kbd-bracket-rotate-negative-drift`, `keys-scale-plus-minus`.
  What to build:
  - Router actions:
    - Ctrl+Y and Ctrl+Shift+Z redo (KeyBindings through `AddShortcut`).
    - Ctrl+D duplicates through `LayerOps.Duplicate`. With nothing selected it is handled and does nothing.
    - Delete and Backspace remove the selected layer; selection falls to the new top.
    - Arrows move 1 template pixel (10 with Shift).
    - `[` `]` `{` `}` rotate ±5 (±15 with Shift), with a true modulo into [-180, 180).
    - R and r reset rotation.
    - `+` and `=` scale ×1.1 (cap 5); `-` and `_` scale ×0.9 (floor 0.01). Both scale about the top-left, as 4.1 does.
    - Every action needs a selection and no Ctrl or Meta. Each press is one undo step (the History contract; autorepeat makes one step per repeat).
  - Engine: `LayerOps.Nudge`, `RotateStep` and `ScaleStep` (pure), if `MoveTo` and `SetRotation` don't already cover them.
  - Undo and Redo tooltips become "Undo (Ctrl+Z)" and "Redo (Ctrl+Y / Ctrl+Shift+Z)". Update `HomeRibbonViewTests` (~line 330).
  - The inspector refreshes after a nudge (`Changed` already drives it; add an assertion).
  Done when:
  - Rotation 3 plus `]` gives 8.
  - 178 plus `]` gives -177.
  - -180 plus `[` gives 175, not -185.
  - Scale 1, then 1.1, then 1.21; 4.8 gives 5; 0.0105 gives 0.01.
  - ArrowRight ×5 adds 5 to x; Shift+Down adds 10 to y.
  - Each press is one undo step.
  - With nothing selected, the arrows do nothing. Ctrl+ArrowLeft does nothing.
  - Ctrl+D adds 1 layer at (+10, +10) and selects it, in one step.
  - Delete on the top layer moves the selection to the new top, and one undo restores it.
  - The disabled-state matrix holds: at launch both buttons are off; one edit turns Undo on; an undo turns Redo on; a new edit turns Redo off.
  - 31 edits followed by 31 undos apply 30.
  - Ctrl+Shift+Z is redo.
  - The final chord list is pinned in `ShellTests`.
  Verify: the standard three.
  Stop class: go.

- [ ] **12. Ribbon re-measure: fixed height, no horizontal scroll at 1200**
  Rows: `shell-layout-skeleton`, `shell-responsive-breakpoint`, `ribbon-group-structure`.
  What to build:
  - `RibbonMeasureTests` lays out MainWindow at 1200x800 (default client) with a layer selected and the preview column shown. For each of the four tabs it asserts that the pane's desired width is at most the RibbonPane width (no horizontal scroll), and that the ribbon height is the same on every tab (R1). It reports the widths and the height.
  - If a tab overflows, compact only by 4.1-sourced means (for example, swatches wrap to 2 rows of 6). Each such change is "provisional; M4b look sign-off".
  - Group labels per tab equal 4.1's list (`ribbon-group-structure`): MODE, TOOLS, BRUSH, PLACEMENT, EDIT, FILE, SETTINGS / PRESETS, BACKGROUND, BG SETTINGS, the joined fill label / SETTINGS, NECKLINE, EDGES, SEAMS, ACCENTS / TEXT, COLORS & SIZE, STYLE, PREVIEW.
  - The 900/901 breakpoint keeps the 200/280 panel and three asset columns.
  - Controller step: `tools/parity-ref/look-ref.js` captures 4.1's four tab states at 1920x1080. A scratch PrintWindow of v5 compares the header bottom, ribbon bottom, panel right edge and footer top within 1 px, or names the deliberate differences (fixed height).
  Done when the tests pass and the measurements and captures are written to `.superpowers/m4b-look/`.
  Verify: the standard three.
  Stop class: go, but **hold** if R1's height grows Home by more than 24 DIP, or if a tab cannot fit without a look change Este hasn't seen.
  Risk: Layout (Presets, 12 swatches, BG Settings, Fill Style with stops, and the Regions grid with limb caps) is the widest pane. This is M4b's most likely look change.

- [ ] **13. Canvas frame budget**
  Rows: `render-pipeline-canvas-reset`.
  What to build: the M3b carry-forward. `RenderView` and `TemplateCanvasHost` keep their offscreen surface and tile across frames and reallocate only on a size or DPI change. Pointer-driven renders are coalesced to one per `CompositionTarget.Rendering`.
  Done when:
  - The existing "one renderer reused draws what a fresh one draws, byte for byte" test passes.
  - A new App test drives a 120-point pen stroke over a 20-layer document through `CanvasInteraction`'s seams and reports the mean and p95 frame time; the assertion is p95 under 16.7 ms on CI hardware, or the measured number is recorded as a provisional baseline.
  - There is no per-frame `SKSurface.Create` (pinned with an allocation counter seam).
  Verify: the standard three.
  Stop class: go. Risk: coalescing must not drop the final point of a stroke. Pin the last point.

- [ ] **14. The undo matrix and the Pants matrix**
  Rows: `undo-coverage-gaps`, `undo-empty-steps`, `file-undo-snapshot-scope`, `bug-pants-mode-region-keys`, `mode-pants-crash-hardcoded-keys`.
  What to build (App tests only):
  - `LedgerScenarioTests.Every_action_is_exactly_one_undo_step` (Theory, Shirt and Pants rows). Run each action once through the real composition, assert `UndoCount` +1 and one Ctrl+Z restores; run a no-op version and assert +0. Actions:
    - add image, duplicate, delete, reorder
    - each of the 5 placements and 6 presets, Center
    - each of the 16 details, Word Art add
    - bg swatch, bg color, opacity and image
    - mode toggle, mask, grid
    - region paint, clear and ALL
    - a fill edit and every keyboard transform
    - draw commit and eraser
    - move, resize, rotate and wheel bursts
  - Snapshot scope: tool, zoom, brush, fill, detail and Word Art settings and the template title are never restored by undo.
  - Pants matrix: add image, Fit Front, every detail button, every preset and Word Art all succeed in Pants with no exception.
  Done when the matrices pass, with no product change or only a fix the matrix exposes, made test-first and named in the commit.
  Verify: the standard three.
  Stop class: go.

- [ ] **15. Smoke and look sign-off (controller)**
  Rows: `asset-grid-thumbnails`, `panel-drop-zone`, `props-adjust-sliders`, `draw-pen-incremental-stroke`, `shell-header`, `shell-header-version-hardcoded`, `theme-tokens-shape-motion-type`, `cross-undo-disabled-style`, `cross-inline-style-overrides`, `cross-text-select-and-selection`, `project-startup`, `shell-single-instance`.
  What to build (scratch only, `.superpowers/m4b-smoke/` and `.superpowers/m4b-look/`):
  - Extend M4a's `brg` bridge driver with items for:
    - tab switching
    - Placement, Presets and Center with and without a selection
    - Background (swatch, opacity drag, image pick through `BridgeFileDialogs`)
    - Details click
    - Text: Word Art text set through UIA ValuePattern, then Add
    - keys V, B, V-mid-stroke, arrows after a slider drag, Ctrl+Y, Ctrl+D and Delete
  - Build the look page of PrintWindow captures next to `tools/parity-ref/look-ref.js` 4.1 states (Home, Layout, Details, Text, the button state matrix, the header, the asset grid) and list every "provisional" value from items 2-12.
  - Cold start: zero network (no socket in the process; the existing `view-ref` rule), title, default visuals, first-frame time.
  - Launch the exe twice: two windows (`shell-single-instance`, keep).
  - Write the manual list `docs/port/m4b-manual.md` (MAN-4 onward): an Explorer drop of 3 PNGs with the dragover look (`panel-drop-zone`); the bracket keys on a non-US layout if K2 is applied; the look page sign-off.
  Done when the smoke log shows every bridge item passing, or it is triaged as in M4a's D8 and D9 (driver fixes only; product defects written up, not fixed overnight), and the look page and manual list exist.
  Verify: the driver refuses any display scale but 100%, and the synthetic-input grep is unchanged.
  Stop class: go for the automated run. Este's look sign-off and MAN items arrive asynchronously, and item 16 closes only what they pass.

- [ ] **16. Ledger close and final verification**
  Rows: all 98 in the scope table.
  What to build: a ledger script `.superpowers/logs/m4b-ledger.py`, in M4a's form. It:
  - gates each row on its item's evidence trailers, the smoke log and the MAN results;
  - narrows checks to the evidence, marked "Narrowed in M4b":
    - `props-adjust-sliders`: the frozen `adjust-*` and `rgb-shift*` goldens plus the LayerProperties range tests, in place of 4.1 renders at every extreme;
    - `draw-pen-incremental-stroke`: `LivePreviewTests` and `CanvasInteractionTests` prove stroke-once; the pen has no frozen golden;
    - K3's Ctrl+V clause, moved to M5;
    - `fonts-google-runtime` and `wordart-font-list`, if W1 was declined;
    - `fillstyle-live-render-hook`: the frozen fill goldens;
  - appends divergences for B1, K1, K2 and T1 and the preview DPI decision (T2);
  - closes everything proven and leaves MAN-gated rows open with the MAN id;
  - runs `python tools/ledger/ledger.py check` and the Markdown render.
  Done when `ledger.py check` reports 0 problems, the only open non-excluded rows are the 9 deferred plus any MAN-gated rows with their MAN id, and the final build and test are green.
  Verify: the standard three, plus `python tools/ledger/ledger.py check` and `git status --short` (only `docs/port/parity-ledger.json`, `parity-ledger.md`, `divergences.md` and `m4b-manual.md`).
  Stop class: go.

Dependencies: 1 comes before everything else. Then 2 → 3 → 5 (Presets on Layout; 5 also needs 4) → 6 → 7; 8 → 9; 10 → 11; 12 after 3, 5, 6, 7 and 9; 13 after 1; 14 after 6, 7, 9 and 11; 15 after 1-14; 16 last.

---

## 3. Contracts

**MainViewModel** (`src/RBX15.App/ViewModels/MainViewModel.cs`):
- New required members `Layout`, `Details` and `Text`.
- A new `RibbonTab ActiveTab`, ObservableProperty, starting Home.
- `Home` loses `FillStyle` and `Regions` and gains `required PlacementViewModel Placement` and `required KeysViewModel Keys`.

**Composition** (`src/RBX15.App/Composition.cs`): new lines go above `// M3a: construction lines above this line.` and new members above `// M3a: required members above this line.`, mirrored in `src/RBX15.App.Tests/TestShell.cs`. The order:

```text
var placementRng = () => Random.Shared;
var placement = new PlacementViewModel(session, placementRng);
var layout = new LayoutViewModel(session, colorPicker)
    { Background = new BackgroundViewModel(session, colorPicker, fileDialogs, status),
      Presets = new PresetsViewModel(session, placementRng) };
var details = new DetailsViewModel(session, colorPicker);
details.LayersAdded += (_, _) => tools.Set(Tool.Select);
var text = new TextViewModel(session, colorPicker, status);
text.LayersAdded += (_, _) => tools.Set(Tool.Select);
var keys = new KeysViewModel();
```

Every color well (Background, Details, Text) uses the shared picker, so `picker.Palette` (the art palette) reaches it with no extra wiring.

**View-models:**
- `LayoutViewModel { FillStyle, Regions, required Background, required Presets }`
- `BackgroundViewModel { Swatches: IReadOnlyList<BgSwatch>, Color, PickColorCommand, OpacityPercent, BeginOpacityDrag(), PickImageCommand }`
- `PlacementViewModel` and `PresetsViewModel { IsVisible (HasSelection), FitFrontCommand ... ; MemeCommand, AlloverCommand, SleevePopCommand, MirrorCommand, ScatterCommand, FullbleedCommand }`
- `DetailsViewModel { Color, Width, Stitch, Groups: IReadOnlyList<DetailGroup(Label, IReadOnlyList<DetailButton(Label, Id, IsAccent, Command)>)>, event LayersAdded }`
- `TextViewModel { Text, Font, Fonts, Style, Styles, Size, FillColor, StrokeColor, GradColor, StrokeWidth, Angle, Preview, AddCommand, event LayersAdded }`
- `KeysViewModel { Enabled, Label, ToggleCommand }`
- `LayerPropertiesViewModel` gains `CenterFrontCommand`.

**Engine:**

- `Placement` (item 4)
- `DocumentOps.SetBackgroundColor`, `SetBackgroundImage` and `SetBackgroundOpacity` (item 6)
- `WordArt.FontOptions` and `WordArt` failure on oversize renders (item 8)
- `LayerOps` key steps, if needed (item 11)

**Window** (`src/RBX15.App/MainWindow.xaml(.cs)`): `LayoutTab`, `DetailsTab` and `TextTab` RadioButtons. A `RibbonPane` Grid with four panes, inactive ones `Hidden`. `PreviewKeyDown` and `PreviewTextInput` go to `ShortcutRouter`. Chords go through `AddShortcut`, behind `TextFieldShortcutCommand`, all seven: Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Ctrl+D, Ctrl+S, Ctrl+Shift+S and Ctrl+O. Ctrl+V is left unbound for M5.

**Existing tests whose pinned structure changes:**

- `ShellTests.The_main_window_builds_the_skeleton` (one Home tab becomes four)
- `ShellTests.The_window_binds_only_ctrl_z_and_the_three_file_chords` (4 bindings become 7)
- `ShellTests`' per-window HomeTab grouping test (~line 307; extend to all tabs)
- `HomeRibbonViewTests` Fill and Regions facts (move to Layout) and the "Placement and Keys stay hidden" fact (~line 118)
- tooltip facts (~lines 256 and 330)
- `HomeTestKit.Home`
- `FillStopsTests` (~line 433)
- `LedgerScenarioTests` (~line 845)
- `ShellLookTests` (~line 62)
- `LayerPropertiesPanelTests` (~line 118)
- `ToolsViewModelTests` (~line 16)

`ThemeKeyGuardTests` scans the new XAML automatically.

---

## 4. Estimate and risks

**Estimate:** 16 items, about 22-30 worker-hours. Items 3, 6, 9, 10 and 15 are L; 4, 5, 7, 8, 11 and 14 are M; 1, 2, 12, 13 and 16 are S to M. As sessions:

- 1 session for Este's rulings (B1, R1, K1-K3, T1, T2, W1) and starting the run.
- 2 unattended Cart runs: items 1-8 overnight, then 9-14.
- 1 session for the smoke, look sign-off, MAN items and ledger close (15-16), with the usual triage pass.

Total: 4 sessions, 5 if the ribbon width forces a look iteration. That is about M4a's size again. M4a's dense, fully scripted plan took one long session plus a triage, but this plan is less scripted, so workers make more choices.

**Biggest risks, highest first:**

1. **Ribbon width and height at 1200 DIP (item 12).** Layout now carries Presets, 12 swatches, BG Settings, Fill Style with stops and the Regions grid with limb caps. A fixed height taken from Layout may make Home visibly taller. This is the most likely place Este has to make a look call; item 12 holds rather than guesses.
2. **Keyboard routing in WPF (items 10-11).** A window-level PreviewKeyDown competes with ScrollViewers, the layer ListBox, region tiles, the 3D viewport and popups. Getting K1 wrong either traps shortcuts again (the 4.1 bug the spec fixes) or breaks keyboard accessibility. Character keys depend on PreviewTextInput, which the bridge cannot raise, so they are proven by router unit tests and RaiseEvent, plus a MAN item for non-US layouts.
3. **Word Art (items 8-9).** It is the spec's named parity risk: preview cost per keystroke (per-character SKFont allocation, shaping), long-text failure, emoji in layer names, and, with W1, a new 15-font golden set that must pass the Word Art rule with no tolerance change. Text entry through the bridge is UIA ValuePattern only.
4. **Transparent default against 4.1's background semantics (item 6).** Without B1, the Background swatches look broken on a new document. With B1, it is a new divergence to record. It also has to keep v5.1's region guide and export unchanged.
5. **Test-structure churn (item 3).** Moving Fill Style and Regions to Layout touches about 8 test files, and ledger evidence cites method names, so renames must be deliberate.
6. **DPI (item 1).** Off-screen bridge windows take the monitor's DPI, not `SetRootDpi`'s, so those tests must compute through the real transform. Until item 1 merges, unattended runs must preflight AppliedDPI 96 or every item parks on the "all tests pass" gate.
7. **Placement geometry in Pants (items 4 and 14).** The Stretch box and Sleeve Pop on legs come from slots, not 4.1 numbers, so a Pants regression is caught only by the matrix, not by goldens. There are no preset goldens: the ledger's numeric checks are the evidence.

### Critical files for implementation

- src/RBX15.App/MainWindow.xaml (and MainWindow.xaml.cs)
- src/RBX15.App/Composition.cs (with src/RBX15.App.Tests/TestShell.cs)
- src/RBX15.App/Views/HomeRibbon.xaml (with ViewModels/HomeViewModel.cs)
- src/RBX15.Engine/WordArt.cs (and the new src/RBX15.Engine/Placement.cs)
- docs/port/parity-ledger.json
