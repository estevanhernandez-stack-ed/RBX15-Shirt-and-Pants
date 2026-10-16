# RBX15 v5 M3b: select, draw, properties and look sign-off Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**M3b finishes the spec's M3 row** (spec §7: "Shell: theme, layout, ribbon, canvas host, layers panel and properties; select and draw tools wired"). M3a built the shell and the canvas, and a canvas click only selected. M3b adds the select gestures (move, uniform corner resize, rotate with the 45° snap, hover cursors, deselect, wheel scaling), the engine-drawn selection overlay and live stroke preview, the Pen, Line, Rect, Ellipse and Eraser tools with the Brush group, the layer properties panel, drag reorder in the layers panel, canvas render cost, the canvas automation peer, Ctrl+Z, and Este's side-by-side look sign-off against 4.1. M4 adds Save and Open, the asset library, the Layout, Details and Text tabs, the Placement group, the dialog inventory and the rest of the keyboard. **What does not work yet is hidden, never shown dead** (Este's standing ruling).

**Goal:** A WPF app in which a user can select, move, resize, rotate and wheel-scale a layer on the canvas with 4.1's handles and snap, draw with Pen, Line, Rect and Ellipse and erase from the selected layer with a live preview, edit every property and image adjustment of the selected layer in the "Selected layer" panel, reorder layers by drag or Alt+Up and Alt+Down, and undo every one of those changes as exactly one step with the button or Ctrl+Z. 10 new frozen view goldens hold the overlay and preview to 4.1's own pixels, and Este signs off a side-by-side capture of 4.1 and v5 in seven matched states.

**Architecture:**

- **Engine (WPF-free, Task 1).** `ViewOptions` gains `SelectedLayerId` and `Preview`. `Renderer.DrawSelectionOverlay` ports 4.1's overlay (editor.js:361-425): box, handles, stem, knob, glyph and label. `LivePreview` is the stroke the commit draws, stroked once (approved divergence), and the eraser previews at half alpha. `ViewRenderer` keeps one persistent 585x559 offscreen surface, a preview surface and the checker tile, as 4.1 keeps `_offCanvas`; `Renderer.RenderView` runs through it, so the 14 M3a view goldens prove its output is unchanged. `SelectionGeometry` holds the select math as pure functions: hit order, handle positions, rotate, snap and normalize, resize, the one 1-500% clamp and the wheel step. `LayerOps` gains the property, reorder and drawing edits, each returning its input when nothing changes. `tools/parity-ref/view-select-ref.js` captures 4.1's selected and mid-stroke views once into `tests/goldens/view-select` (controller-run).
- **The canvas (Task 2).** `CanvasInteraction` (namespace `RBX15.App.Canvas`) is 4.1's mouse handler (editor.js:1056-1320) as a state machine over `EditorSession`. It works on plain template points, so every gesture is unit-tested through seams with no desktop input. Each drag is one `BeginGesture` scope, and a wheel burst is one scope closed by a 400 ms quiet period or by any other session change (`EditorSession.Changing`). `ToolsViewModel` and `BrushViewModel` back the Tools and Brush groups. `TemplateCanvasHost` replaces M3a's press-as-click with pointer seams, left-button capture, the wheel timer, cursors per tool and state, render coalescing to one frame per `CompositionTarget.Rendering`, and a `TemplateCanvasHostAutomationPeer`.
- **The properties panel (Task 3).** `LayerPropertiesViewModel` and `LayerPropertiesPanel` port 4.1's "Selected layer" section (editor.js:747-913). Every edit is one step: typed fields commit on Enter, focus loss or a press elsewhere, and slider drags are gestures through `SliderGestureTracker`, which also moves Fill Style onto it and fixes M3a's `Begin(false)` minor.
- **Layers and shell (Task 4).** A capture-based in-process row drag with a stale-target fix, Alt+Up and Alt+Down on a focused row, the "(drag to reorder)" hint, the measured look notes (one fix, three pinned keeps), and the window's Ctrl+Z `KeyBinding`.
- **Close-out (Task 5).** `LedgerScenarioTests` replays M3b's scenarios through the real composition. `tools/parity-ref/look-ref.js` captures 4.1's whole window, and a scratch UI Automation driver captures v5 (UIA patterns and PrintWindow only). The controller publishes the comparison page as a claude.ai Artifact, Este runs his hand checks, the ledger closes only what the evidence proves, and the plan ends at Este's sign-off.

**Tech Stack:** .NET 10 (`net10.0` engine, `net10.0-windows` App and App.Tests), C# with nullable and warnings as errors, WPF, CommunityToolkit.Mvvm `8.4.2` (unchanged), SkiaSharp `4.153.1` and HarfBuzzSharp `14.2.1.301` (engine, unchanged), Win32 `CreateIconIndirect` through `CursorInteropHelper` for the eraser ring cursor, xUnit 2.9.3 with `xunit.runner.visualstudio` 3.1.4 and `Microsoft.NET.Test.Sdk` 17.14.1, Electron 41.1.0 (devDependency; the view and look capture tools only), `System.Windows.Automation` (the scratch look driver only), Python 3 stdlib (ledger and page scripts), Git Bash.

**Spec:** [`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`](../specs/2026-10-11-native-v5-port-design.md), sections 2 (Tools, History, Render), 4 (shell, canvas, keyboard reach, accessibility), 5 (input, tools and the layers panel), 6 (parity) and the §7 M3 row. Ledger: [`docs/port/parity-ledger.json`](../../port/parity-ledger.json) and [`docs/port/divergences.md`](../../port/divergences.md). Planning contract: `.superpowers/m3b-planning/contract.md`; interface sheet: `.superpowers/m3b-planning/interfaces.md` (both git-ignored; the sheet was synced to this plan on 2026-10-15). Previous milestone: [M3a plan](2026-10-14-v5-m3a-shell-canvas.md).

**Contract amendments for Este's OK** (2026-10-15). Each departs from the planning contract's wording or from 4.1 in a way the contract does not rule. Each is applied below so nothing waits, and each names the exact change if Este declines.

- **CA1: the rotate knob is hit where it is drawn** (Task 1 ruling T1-1; critic MJ3). 4.1 draws the knob at `c.x + sin(r)·d` (editor.js:372, :396) but hit-tests it at `c.x - sin(r)·d` (`getRotHandlePos`, editor.js:1036), its mirror image across the layer's vertical axis. The two agree only at 0° and 180°, so on a rotated layer 4.1's visible knob does nothing and a spot of empty canvas rotates. v5 hits the drawn knob, and Task 5 rewords `select-rotate-handle-hit`'s divergence to say so. **If declined:** `SelectionGeometry.RotateHandle` returns `(c.x - sin(r)·d, c.y - cos(r)·d)`; `SelectionGeometryTests.The_rotate_handle_turns_clockwise_with_the_layer` and `RendererOverlayTests.The_rotate_handle_is_hit_where_the_overlay_draws_it` are replaced by a getRotHandlePos equality to 1e-9; Task 5 drops the `select-rotate-handle-hit` REWORD and both NARROW entries, which then close on their 4.1 checks, minus zoom 4. Review Focus 1 goes, and the runner-up below replaces it.
- **CA2: the panel carries Duplicate, Up and Down, and the list loses M3a's Up and Down** (interfaces R13; Task 3, Task 4). The contract's panel list is Name, X and Y, opacity, scale, rotation, flips and adjustments. 4.1's panel also holds Duplicate, Up and Down (editor.js:752-803, :846-853), and M3a parked Up and Down under the list "until M3b". "Center on front torso" (ledger `props-center-front`, App.Ribbon.Layout) stays hidden until M4. **If declined:** Task 3 drops `DuplicateCommand`, `MoveUpCommand` and `MoveDownCommand` and their three buttons from `LayerPropertiesViewModel` and `LayerPropertiesPanel.xaml`, with their tests (`LayerPropertiesViewModelTests` loses the Up/Down/Duplicate facts); Task 4 keeps M3a's Up/Down pair in `LayersPanel.xaml`, and its Step 12 check expects `2` instead of `0`.
- **CA3: a wheel burst is one undo step** (interfaces R7; Task 2; new ledger row `wheel-scale-undo-burst`). The contract says each drag is one step and leaves the wheel open. One step per notch would put dozens of steps on the 30-deep history for one touchpad flick, so a burst is one step: it closes 400 ms after its last event, at a press, at a tool or selection change, or at any other edit, which stays its own step (critic MJ1). **If declined:** `CanvasInteraction.Wheel` applies each notch with a plain `session.Apply`, with no gesture; `EndWheelBurst`, the host's 400 ms timer and `EditorSession.Changing` go. `CanvasInteractionTests.An_edit_during_a_wheel_burst_is_its_own_step`, `A_gesture_opened_during_a_wheel_burst_records_separately` and `EditorSessionTests.Changing_is_raised_first_by_every_entry_point` go (App.Tests 3 lower from Task 2 Step 3 on), and `Wheel_scales_the_selection_about_its_center_symmetrically_and_a_burst_is_one_step`, `A_press_a_selection_change_or_a_tool_change_closes_the_wheel_burst`, `TemplateCanvasHostTests.The_wheel_is_sign_only_and_a_quiet_period_closes_the_burst` and Task 5's S2 assert one step per notch. Task 5 drops the `wheel-scale-undo-burst` row.
- **CA4: two look notes stay as they are** (Task 4 rulings T4-R9 and T4-R10). The contract says "fix where in scope". Measured: v5's canvas area at 1200x800 is 540 DIPs against 4.1's 531 on the Layout tab that holds the same two groups, so v5 already gives the canvas more room and shows a scrollbar where 4.1 clipped. The lingering small windows are WPF's keyboard-focus tooltips, which show the same text as 4.1's hover tooltips to keyboard users (WCAG 1.4.13). Task 5 amends `cross-text-select-and-selection` to `fix` for them. **If declined:** for the canvas, the only change that reaches 4.1's Home-tab 558 is moving Fill Style and Template Regions off Home, which is M4's tab work; M3b then records `shell-layout-skeleton` as open with the measured 540 against 531 and 558. For the tooltips, add `<Setter Property="ToolTipService.ShowsToolTipOnKeyboardFocus" Value="False" />` to a `FrameworkElement`-based implicit style in `Themes/Controls.xaml`, rewrite `ShellLookTests.The_lingering_windows_are_the_persistent_keyboard_focus_tooltips` to assert `False`, and drop the `cross-text-select-and-selection` amendment from Task 5 Step 13.
- **CA5: the 4.1 look capture does not inject `app-harness.js`** (Task 5 ruling 2). The contract says to capture 4.1 "through the existing Electron harness". The harness pins `#mainCanvas` to the viewport origin with `position: fixed` (app-harness.js:15-19), which would draw the canvas over the header in a whole-window capture. `look-ref.js` reuses the harness's font interception and network block from `fonts.js` and drives real control events from a small in-page driver. **If declined:** `look-ref.js` injects `app-harness.js` and every state is captured with the canvas over the header; the sign-off page then cannot compare the shell layout, and `shell-layout-skeleton` stays open.
- **CA6: a seventh look state and one extra 4.1 capture** (Task 5 ruling 4). `07-narrow-700` (a 700 DIP client) gives `ribbon-overflow-hscroll` its evidence on both sides. State 02 adds a 4.1-only capture of the Layout tab, where 4.1 keeps the region grid. The six contract states keep their names and contents. **If declined:** delete `07-narrow-700` and the `alsoTab41` entry from `look-states.json`, the two matching `STATES` entries in `build_page.py`, and `ribbon-overflow-hscroll` and `regions-grid-layout-shirt` from Step 16's `LOOK` ids (both stay open).
- **CA7: two ledger dispositions change from keep to fix** (Task 5 ruling 8; critic mn6). `eraser-cursor` (keep, "capped at diameter 240") contradicts `cursor-eraser` (fix, the true footprint); `cross-text-select-and-selection` (keep) gains the keyboard-focus tooltips (CA4). A keep row carries no divergence sentence, so each is amended to `fix` with one. The ledger honesty rule is unchanged. **If declined:** remove block 1 and block 1b from `m3b-ledger.py`; `eraser-cursor` and `cross-text-select-and-selection` stay `keep` and open, `eraser-cursor` leaves the Step 13 ids, and the expected lines become `amended 0`, `417 rows; 201 done; 198 fix` (re-measure the numstat lines).

**Not an amendment: Ctrl+Z** (critic MJ2). The contract's out-of-scope line keeps "keyboard shortcuts (bare keys and Ctrl combos, **except Ctrl+Z**)" out, so Ctrl+Z is M3b's. Task 4 Step 11 binds it on the window to the Edit group's Undo, with no other shortcut.

**Interface sheet amendments** (`.superpowers/m3b-planning/interfaces.md`, synced to this plan on 2026-10-15; the plan below is authoritative): `EditorSession.Changing` (Task 2; R7 now closes a wheel burst on any other session change, critic MJ1); `SelectionGeometry.RotateHandle` returns the drawn knob (T1-1) and `RotationLabel` uses JavaScript's `toFixed(1)` through `SelectionGeometry.ToFixed1`, not .NET's `"F1"` (T1-4); R15's cursor cap is `128 - ceil(2 x display scale)` device px (Task 2 ruling 6); `PositionLabel` uses 4.1's no-break space (Task 3); `look-ref.js` does not inject `app-harness.js` (CA5); Task 4's look notes are one fix (the drop zone label) and three pinned keeps, plus the Ctrl+Z `KeyBinding`; `ShellWiringTests` gains one fact in Task 2; the citation corrections of Task 3's Additions item 8; and each task's "Additions requested" list at its end is adopted verbatim, with the names aligned to one spelling.

## Global Constraints

The M3a plan's Global Constraints apply verbatim (copied below from [the M3a plan](2026-10-14-v5-m3a-shell-canvas.md)), then M3b's additions. Where an M3a bullet names an M3a-only artifact (M3a's task steps, the `tests/goldens/view` capture, the M3a scratch folders, the hidden Tools and Brush groups and drag hint), the M3b bullets after it say how it applies here, and the M3b bullet wins.


The M2b plan's constraints apply verbatim, then M3a's additions. Where an M2b bullet names an M2b-only artifact (the Word Art rule, the M2b task order, the M2b scratch folders), the M3a bullets below say how it applies here.

- **The engine never references WPF.** `RBX15.Engine` targets `net10.0` and depends only on SkiaSharp `4.153.1` and HarfBuzzSharp `14.2.1.301` (the version `SkiaSharp.HarfBuzz` `4.153.1` pins; approved in ruling R1).
- **No obsolete SkiaSharp APIs.** Warnings are errors. Verified against 4.153.1 on 2026-10-13:
  - Build paths with `SKPathBuilder` (`MoveTo`, `LineTo`, `ArcTo(SKRect, float, float, bool)`, `Close()`, `Detach()`).
  - Draw images with `DrawImage(SKImage, SKRect, SKSamplingOptions, SKPaint)` or `DrawImage(SKImage, float, float, SKSamplingOptions, SKPaint)`.
  - `SKColorFilter.CreateColorMatrix` translate values are in **0..1** units.
  - If any other call is obsolete, use the replacement the warning names. Never suppress the warning.
- **The render order is a contract** (spec section 2): clear, background (a stretched image, or the color, times opacity), region fills, then visible layers bottom to top. Export always applies the mask.
- **Sampling.** Layer and eraser-mask draws use `Renderer.LayerSampling` (`SKFilterMode.Linear`, `SKMipmapMode.None`), Canvas2D's default `imageSmoothingQuality = 'low'`. Any non scale-translate image draw goes through `Renderer.FloatPipeline`, and antialiasing follows `Renderer.DrawImageAntialiased` (Blink `ShouldDrawImageAntialiased`).
- **Strokes are drawn once** (approved divergence `draw-pen-incremental-stroke`). Production code never replays 4.1's per-move stroking. The only replay is the internal `EraseStroking.Replay41`, used by parity tests to meet the frozen eraser goldens.
- **The golden rule** (spec section 6): zero interior differences, and edge deltas of 3 or less. Scenes with an RGB-shifted layer use M1's rule (approved divergence `rgb-shift-compute`): the full rule on pixels whose golden alpha is 255, plus an alpha delta of at most 1 everywhere. Both golden gates apply it through the shared `GoldenRule`. When a scene fails, fix the engine. The test, the rule and the golden stay as they are.
  - **Detail and project scenes** use the unchanged rule.
  - **Word Art scenes** (every scene with a `wordart` action) use the Word Art rule of ruling R2, `GoldenRule.AssertWordArtMatches`: the same size, and every interior pixel within 1 level per channel; edge pixels are not bounded. Task 3 Step 20 still runs them under the unchanged rule first and checks the measured numbers, so an engine change that moves them is caught. An interior difference above 1 level is an engine bug: fix it.
  - **The Word Art guard.** `WordArtSwapTests` swaps 4.1's own Word Art bitmap (frozen in `tests/goldens/app/wordart-ref/`) into the engine's document, and each of the 22 exports must equal its golden exactly under the unchanged rule. A failure there is a layout, placement or compositing bug: fix the engine.
- **Goldens are frozen.** `tests/goldens/**` (82 app scenes, 34 render scenes, 3 project fixtures and their indexes) was captured in M2a and is never edited, recaptured or regenerated in M2b. CI never regenerates goldens. `ImageDiff` never changes. **The detail and wordart golden scenes are already frozen; nothing is recaptured in M2b.** The one addition is ruling R2's: Task 3 Step 21 captures 4.1's own Word Art bitmaps once into the new folder `tests/goldens/app/wordart-ref/` (22 PNGs, `metrics.json`, `index.json`), with the reproducibility check M2a Task 3 used. From that commit on, those files are frozen like every other golden. No existing file under `tests/goldens` changes.
- **`assets/fonts` is read-only.** The vendored fonts and their README manifest are M2a Task 1 output, pinned by sha256 and checked by `VendoredFontsTests`. M2b reads them, and copies them into build outputs only through the engine's `Content` items.
- **Logs never land in the tree.** Test logs go to `.superpowers/logs/` (git-ignored). Scratch renders and measurements go to `.superpowers/m2b-measure/`, and throwaway scripts and projects go to `.superpowers/m2b/`. Heatmaps go to `diff-heatmaps/` (git-ignored, a CI artifact).
- **Region keys stay verbatim** (`torso_front`, `waist_front` and so on); saved projects store them.
- **The Electron app is untouched.** `main.js`, `editor.js`, `editor.html`, `styles.css`, `lib/`, `packs/`, `package.json`, `package-lock.json`, `tests/*.test.js` and `windows/` do not change. `tools/parity-ref` reads them as-is.
- **`ELECTRON_RUN_AS_NODE` is cleared** for every Electron run (`env -u ELECTRON_RUN_AS_NODE`), or `electron.exe` starts as plain Node.
- **Line endings.** `core.autocrlf=true` leaves CRLF in the working tree. The Edit tool and the enable scripts keep each file's own line endings. The file states this plan prints are shown with LF.
- **Task order is 1, 2, 3, 4.** Task 1's loader and gate edits are exact-text against `main`, so Task 1 lands before Tasks 2 and 3. Tasks 2 and 3 edit by pattern and fail loudly if an anchor is missing. Task 4 starts only after Task 3 has committed its Step 23.
- **Conventional commits.** Each commit ends with the `Co-Authored-By` trailer of the model that made it; a subagent commit carries its own model's trailer, not the controller's.
- **Dashboard:** log forks with project `6KpHItSNg9NYHkJg9thn` and origin `Dunder · Claude Code`. Push, PR and dashboard steps belong to the controller.

**M3a additions** (planning contract, "Plan rules"):

- **The Electron app files stay untouched.** `main.js`, `editor.js`, `editor.html`, `styles.css`, `lib/`, `packs/`, `package.json`, `package-lock.json`, `tests/*.test.js` and `windows/` do not change. `ThemeTokenTests` and `ShellTests` read `styles.css` and `editor.html`; `view-ref.js` runs them as-is.
- **Goldens.** `tests/goldens/render`, `tests/goldens/app` and `tests/goldens/projects` are frozen. `tests/goldens/view` is created once, by the controller-run capture in Task 2 Step 10 (provenance in `index.json`, CPU raster, `env -u ELECTRON_RUN_AS_NODE`), and is frozen from that commit on. `GoldenViewTests` holds the engine's view to it under the unchanged `GoldenRule`. No other file under `tests/goldens` is added or changed.
- **The engine never references WPF.** `RBX15.Engine` stays `net10.0` with no `UseWPF`, no `PresentationCore`, `PresentationFramework`, `WindowsBase` or `System.Windows` reference. `ViewOptions`, `DocumentOps`, `LayerOps` and `ExportGuard` are SkiaSharp-only. Task 5 Step 8 greps for it.
- **No network requests.** The app makes none, and `view-ref.js` fails a capture on any request it does not answer from `assets/fonts`. The one network use is the implementer's build-time `dotnet package search` check in Task 1 Step 5, which is not app code.
- **No secrets in config.** No key, token or password in any file. Task 5 Step 8 greps the diff.
- **626 branding:** none in the app beyond what 4.1 already shows. The header (`RBX15 Classic Shirt & Pants Maker`, `ItsjustEste | v5.0`) and the footer (`Developed at 626 Labs LLC`) copy editor.html:16-17 and :343 verbatim; Task 5 Step 8 greps for `626` in the app.
- **Hide what doesn't work yet.** The Layout, Details and Text tabs, the Home tab's Tools, Brush and Placement groups, the File group's Save work and Open buttons, and the "(drag to reorder)" hint are absent in M3a. `HomeRibbonViewTests` and `Task4ViewTests` pin the absences.
- **One composition root.** `MainViewModel` is built only in `Composition.Build` (app) and `TestShell.Main` (tests). Tasks 2 to 4 add their lines at Task 1's anchor comments, and Task 4 Step 21 prints both files in full.
- **One theme, one spelling.** Every theme key is Task 1's (`Tokens.xaml`, `Controls.xaml`). A view never defines a duplicate of a theme key; `ThemeKeyGuardTests` (Task 5) fails on a `DynamicResource` key no dictionary defines.
- **View tests run on `StaThread`.** Build WPF objects inside `StaThread.Run`, and call `StaThread.Pump()` after setting `DataContext`, before reading a bound value.
- **Task order is 1, 2, 3, 4, 5.** Each task edits files the previous ones created, at named anchors, and fails loudly if an anchor is missing. Task 2 Step 10 (the view capture) and Task 5 Step 5 (the smoke checklist) are the controller's; a subagent stops there and reports.
- **Scratch.** Measurements go to `.superpowers/m3-measure/`, throwaway scripts to `.superpowers/m3/`, ledger scripts and logs to `.superpowers/logs/`, smoke files to `.superpowers/m3a-smoke/` (all git-ignored).
- **Pixel values come from 4.1 or a golden, never invented.** A value with no 4.1 source is tagged "new v5 surface; provisional; M3b look sign-off" in its file and listed in Task 5's left-open table.
- **Dashboard and git hosting.** Push, the PR and dashboard steps belong to the controller. No task pushes.

**M3b additions** (planning contract, "Rulings already made" and "Plan rules"):

- **No synthetic global input, ever:** no SendInput, mouse_event, SendKeys or screen-coordinate clicks. Automation may use only UIA patterns on the RBX15 process and keys posted to its own HWNDs, with PrintWindow screenshots. WPF ignores posted mouse messages, so mouse gestures are proven by unit tests through test seams, plus Este's manual check.
- **Implementers never launch the app to drive it.** Gestures are tested through `CanvasInteraction`'s and the hosts' seams, and view tests use `RaiseEvent` on in-process elements. The only runs of the live app are the controller's look run (Task 5 Step 9, the scratch UIA driver) and Este's hand checks (Task 5 Step 11). No step opens a real file dialog except the driver's Add image dialog on the app's own process. `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` prints nothing at every task's end.
- **Rulings already made, not reopened:** pen strokes are drawn once at mouseup and a press draws a dot (approved divergence); the eraser is single-stroke in production (`Eraser.Apply`, never `Replay41`); undo is synchronous with a capped redo, and selection survives undo; the hit test is box-based, not alpha-based; scale is clamped to 1-500% everywhere; the color picker is the themed in-app popup behind `IColorPicker` with live preview; the caption is themed through DWM; the footer reads "Developed at 626Labs"; hide until it works.
- **The ledger honesty rule:** a row closes only on evidence that meets its own parityCheck; otherwise the parityCheck is reworded to the evidence ("Narrowed in M3b", "Reworded in M3b") or the row stays open. Tasks 1-4 close no rows; Task 5 does.
- **Goldens.** Every existing file under `tests/goldens` (render, app, projects, app/wordart-ref, view) is frozen. `tests/goldens/view-select` is created once, by the controller-run capture in Task 1 Step 9 (provenance in `index.json`, CPU raster, `env -u ELECTRON_RUN_AS_NODE`), and is frozen from that commit on. `GoldenViewSelectTests` holds the overlay and preview to it under the unchanged `GoldenRule`, with only the rotation label's box blanked (interfaces R4) and held to the Word Art rule instead. Pixel values come from 4.1 or a golden, never invented; a value with no 4.1 source is tagged "provisional; M3b look sign-off" and listed on the sign-off page.
- **Hide what doesn't work yet.** The Layout, Details and Text tabs, the Placement group, Save work and Open, the asset library, the presets, "Center on front torso" and "Keys: ON" stay absent. Tool tooltips carry no shortcut letters (interfaces R14). Ctrl+Z is the one keyboard shortcut M3b binds (Task 4 Step 11).
- **Task order is 1, 2, 3, 4, 5.** Each task edits files the previous ones created, at named anchors, and fails loudly if an anchor is missing. The controller's steps are Task 1 Step 9 (the view-select capture) and Task 5 Steps 9, 10, 11 and 16 (the look run, the page, Este's hand checks and the sign-off); a subagent stops there and reports.
- **Scratch.** Measurements and throwaway scripts go to `.superpowers/m3b/`, the look captures, driver and page to `.superpowers/m3b-look/`, ledger scripts and logs to `.superpowers/logs/` (all git-ignored).
- **Commits** end with the `Co-Authored-By` trailer of the model that made them: an implementer's own model, the controller's for the controller's commits.

## Review Focus

The critic picked five input classes as the most likely to cause bugs. Each has a pinning test in its owning task.

- **Pressing the visible knob of a rotated layer** (Task 1 ruling T1-1; contract amendment CA1). 4.1 hit-tests the knob's mirror image, so at 30° or 90° 4.1's drawn knob does not rotate the layer. Expected: the drawn knob is the hit and hover target at every angle, and the mirrored spot is an ordinary pick. Pinned in Task 1 (`SelectionGeometryTests.The_rotate_handle_turns_clockwise_with_the_layer`, `RendererOverlayTests.The_rotate_handle_is_hit_where_the_overlay_draws_it`); Task 2's press and hover use it; Este's hand check H2.
- **A gesture that loses the mouse** (Task 2, interfaces R9 and R10): a release outside the window, Alt+Tab, a tool switch mid-stroke, and the stray release after. Expected: a lost capture commits a stroke or a move at its last point as one step; a tool switch discards a draw or erase stroke with no layer and no step; a select drag ends as if released, as one step; the stray release does nothing. Pinned in Task 2 (`CanvasInteractionTests.Capture_loss_mid_stroke_commits_it_at_the_last_point`, `A_tool_switch_mid_stroke_discards_it`, `A_tool_change_mid_select_drag_keeps_the_change_as_one_step`) and Task 5 (`LedgerScenarioTests.A_tool_switch_mid_stroke_discards_it_and_a_lost_capture_commits_it`).
- **A fast wheel or a precision touchpad, then another edit** (Task 2, interfaces R6 and R7; contract amendment CA3; critic MJ1). Expected: sign only; ×1.05 and ÷1.05 about the center, so ten down and ten up return to the start (4.1 drifts to 0.9794); one step per burst; nothing recorded at the 500% clamp. An edit made during the 400 ms quiet period (a panel slider, Flip, an import) is its own undo step and is never folded into the wheel's. Pinned in Task 2 (`CanvasInteractionTests.Wheel_scales_the_selection_about_its_center_symmetrically_and_a_burst_is_one_step`, `A_press_a_selection_change_or_a_tool_change_closes_the_wheel_burst`, `An_edit_during_a_wheel_burst_is_its_own_step`, `A_gesture_opened_during_a_wheel_burst_records_separately`, `EditorSessionTests.Changing_is_raised_first_by_every_entry_point`).
- **Typing in a properties field, then clicking the canvas** (Task 3 ruling 7). The canvas takes no keyboard focus, so the field never loses focus on that click. Expected: the typing commits to the layer it was typed for, as one step, before the canvas pick acts. Pinned in Task 3 (`LayerPropertiesPanelTests.A_press_elsewhere_commits_typing_before_it_acts_and_a_press_inside_does_not`) and Este's hand check H6b.
- **A row drag whose target goes stale or whose document changes underneath** (Task 4 ruling T4-R2): hovering back over the dragged row, releasing off every row, a release on a different half than the last move, or an Undo or Delete mid-drag. Expected: nothing moves and no step is recorded; the drop reads the row under the release point; no row stays dimmed or marked. Pinned in Task 4 (`LayersReorderTests.Hovering_the_dragged_row_after_another_clears_the_stale_target`, `An_undo_that_removes_the_dragged_layer_ends_the_drag`, `LayersPanelDragTests.A_release_off_every_row_drops_nothing`, `The_release_point_picks_the_target`).

Runner-up (Review Focus 1's replacement if CA1 is declined): **the next frame on the reused renderer after a live preview, a DPI change or a different document** (Task 1). Expected: a reused `ViewRenderer` draws exactly what a fresh one draws, byte for byte. Pinned in Task 1 (`ViewRendererTests.Reuse_is_byte_equal_to_fresh_across_a_document_sequence`, `A_device_scale_change_rebuilds_the_checker`, and the 14 M3a view goldens, which now run through `ViewRenderer`).

The drafters pinned more edge cases: a rotate drag across straight down, or a layer already near ±180° (`SelectionGeometryTests.A_drag_across_the_atan2_seam_stays_in_range`, `Rotation_is_normalized_to_minus_180_to_180`); the rotation label on an exact binary tie, 17.25 reading "17.3°" (`SelectionGeometryTests.ToFixed1_is_javascript_not_dotnet_F1_on_exact_ties`); junk such as "NaN" or "1e400" in the X and Y fields (`LayerPropertiesViewModelTests.Unusable_position_text_is_refused_and_reverts`); a slider press whose class handler marks the event handled, and a `DragStarted` with the button already up (`SliderGestureTrackerTests.DragStarted_with_the_button_up_closes_a_stale_gesture_and_opens_none`); Alt+Up held on a focused row while `ObservableCollection.Move` drops its container (`LayersPanelDragTests.Alt_up_and_down_move_the_focused_row_one_step_each_and_focus_follows_it`); the eraser with no selection, a hidden selection or a click without movement (`LedgerScenarioTests.The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden`); and a wheel burst past the 500% clamp (`LedgerScenarioTests.A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step`).

---

## File structure

```text
src/RBX15.Engine/
  ViewOptions.cs               + SelectedLayerId, Preview (init-only) (Task 1)
  LivePreview.cs               Draw(Stroke), Erase(points, size), Stroke, IsEraser, Alpha (Task 1)
  ViewRenderer.cs              persistent 585x559 offscreen, preview surface, checker tile; Render, Dispose (Task 1)
  Renderer.cs                  RenderView through ViewRenderer; RenderComposite preview overload; DrawSelectionOverlay,
                                 DrawRotationLabel, TemplateInfo; NearestSampling and CheckerTile internal (Task 1)
  SelectionGeometry.cs         ResizeCorner, SelectHitKind, SelectHit; hit order, handles, rotate, snap, normalize,
                                 resize, ClampScale, WheelScale, RotationLabel, ToFixed1, LabelBounds (Task 1)
  Tools.cs                     + Min/Max/DefaultBrushSize, SanitizeWidth, StepBrushSize, DrawStroke (Task 1)
  Eraser.cs                    + DrawStroke, ApplyTo (Task 1)
  LayerOps.cs                  + MoveTo, SetScale, ScaleAboutCenter, SetRotation, Flip, SetOpacity, SetName,
                                 SetAdjustments, ResetAdjustments, Reorder, DropIndices, AddDrawing; LayerAdjustments (Task 1)
src/RBX15.Engine.Tests/
  SelectionGeometryTests.cs 74, LayerOpsEditTests.cs 19, ViewRendererTests.cs 4, RendererOverlayTests.cs 18,
    LivePreviewTests.cs 6, GoldenViewSelectTests.cs 14; ToolsTests.cs +19, EraserTests.cs +5 (Task 1)
  GoldenRule.cs                + AssertImageMatchesExcept; thresholds and ImageDiff unchanged (Task 1)
src/RBX15.App/
  EditorSession.cs             + Changing, raised first by every change entry point (Task 2)
  Canvas/Tool.cs, CanvasCursor.cs, ViewOverlay.cs, CanvasInteraction.cs   namespace RBX15.App.Canvas (Task 2)
  ViewModels/ToolsViewModel.cs, BrushViewModel.cs (Task 2); LayerPropertiesViewModel.cs (Task 3)
  ViewModels/CanvasViewModel.cs, HomeViewModel.cs (Task 2, full); LayersViewModel.cs (LayerPicked Task 2; drag Task 4);
    LayerRowViewModel.cs (IsDragSource, DropIndicator Task 4); MainViewModel.cs (+ LayerProperties Task 3)
  Views/ToolsGroup.xaml(.cs), BrushGroup.xaml(.cs) (Task 2); LayerPropertiesPanel.xaml(.cs) (Task 3)
  Views/CanvasView.xaml, HomeRibbon.xaml (Task 2, full); ColorPickerPopup.xaml.cs (Canvas qualified, Task 2);
    FillStyleGroup.xaml.cs (onto SliderGestureTracker, Task 3); LayersPanel.xaml(.cs) (Task 4, full); DropZone.xaml (Task 4)
  Controls/TemplateCanvasHost.cs (Task 2, full); CanvasCursors.cs, TemplateCanvasHostAutomationPeer.cs (Task 2);
    SliderGestureTracker.cs (Task 3)
  Services/ImportService.cs    + LayersAdded (Task 2)
  Composition.cs               tools, brush and the auto-revert wiring (Task 2, full); layerProperties (Task 3)
  MainWindow.xaml              LayerPropertiesPanel in LeftPanelContent (Task 3); the Ctrl+Z KeyBinding (Task 4)
src/RBX15.App.Tests/
  ToolsViewModelTests.cs 4, BrushViewModelTests.cs 6, CanvasInteractionTests.cs 33, CanvasCursorsTests.cs 13,
    CanvasAutomationPeerTests.cs 2; EditorSessionTests.cs +1, HomeRibbonViewTests.cs +2, CanvasViewModelTests.cs +1
    (-1, +2), TemplateCanvasHostTests.cs +5 (-2, +7), ShellWiringTests.cs +1, ImportServiceTests.cs +1,
    LayersViewModelTests.cs +1; CanvasViewTests.cs, HomeTestKit.cs, ColorPickerPopupTests.cs and
    ThemeKeyGuardTests.cs edited with no count change (Task 2)
  SliderGestureTrackerTests.cs 7, LayerPropertiesViewModelTests.cs 58, LayerPropertiesPanelTests.cs 11; FillStyleTests.cs
    and HomeRibbonViewTests.cs edited with no count change (Task 3)
  LayersReorderTests.cs 32, LayersPanelDragTests.cs 15, ShellLookTests.cs 5, ShellTests.cs +1; Task4ViewTests.cs edited
    with no count change (Task 4)
  LedgerScenarioTests.cs       + 12 M3b scenarios (26 in all) (Task 5)
tools/parity-ref/
  view-select-ref.js, view-select-scenes.json (10 scenes) (Task 1)
  look-ref.js, look-states.json (7 states) (Task 5)
  README.md                    the two new tools and their scene and state keys (Tasks 1 and 5)
tests/goldens/view-select/     10 PNGs, index.json, scenes.json; captured once by the controller, then frozen (Task 1 Step 9)
docs/port/                     parity-ledger.json, parity-ledger.md, divergences.md: 2 amended, 4 reworded, 4 appended,
                                 1 behavior corrected, 4 added, 69 narrowed, 80 closed (Task 5 Step 13); 11 look rows
                                 narrowed and closed on Este's sign-off (Task 5 Step 16)
.superpowers/ (git-ignored)    m3b/freeze-view-select-ref.py (Task 1); m3b-look/driver/uia/ (the v5 look driver),
                                 m3b-look/build_page.py and the page (Task 5); logs/m3b-*.py and the hand-check results
```

**Test counts.** The baseline on `feat/v5-m3b-select-draw` (`4e0be14`, M3a merged) is Engine.Tests 718 passed and 0 skipped, App.Tests 411, Parity.Tests 35. Every run step below states the cumulative count; nothing is skipped and Parity.Tests stays 35 throughout:

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| `4e0be14` | 718 | 411 | |
| Task 1 Step 3 | 748 | 411 | ToolsTests +19, EraserTests +5, LivePreviewTests +6 |
| Task 1 Step 5 | 841 | 411 | SelectionGeometryTests +74, LayerOpsEditTests +19 |
| Task 1 Step 7 | 863 | 411 | RendererOverlayTests +18, ViewRendererTests +4 |
| **Task 1 Steps 10-11** | **877** | **411** | GoldenViewSelectTests +14 |
| Task 2 Step 2 | 877 | 421 | ToolsViewModelTests +4, BrushViewModelTests +6 |
| Task 2 Step 3 | 877 | 455 | CanvasInteractionTests +33, EditorSessionTests +1 |
| Task 2 Step 4 | 877 | 468 | CanvasCursorsTests +13 |
| Task 2 Step 5 | 877 | 470 | HomeRibbonViewTests +2 |
| **Task 2 Steps 6-7** | **877** | **481** | CanvasViewModelTests +1 (-1, +2), TemplateCanvasHostTests +5 (-2, +7), ShellWiringTests +1, ImportServiceTests +1, LayersViewModelTests +1, CanvasAutomationPeerTests +2 |
| Task 3 Step 3 | 877 | 488 | SliderGestureTrackerTests +7 |
| Task 3 Step 5 | 877 | 546 | LayerPropertiesViewModelTests +58 |
| **Task 3 Steps 7-8** | **877** | **557** | LayerPropertiesPanelTests +11 |
| Task 4 Step 3 | 877 | 589 | LayersReorderTests +32 |
| Task 4 Step 6 | 877 | 604 | LayersPanelDragTests +15, Task4ViewTests ±0 |
| Task 4 Step 9 | 877 | 609 | ShellLookTests +5 |
| **Task 4 Steps 11-12** | **877** | **610** | ShellTests +1 (Ctrl+Z) |
| **Task 5 Steps 6 and 14** | **877** | **622** | LedgerScenarioTests +12 |

The per-class counts are the drafters' measured counts, recounted per fact and theory row by the critic. The reconciler's additions (`CanvasInteractionTests` +2 and `EditorSessionTests` +1 in Task 2, `ShellTests` +1 in Task 4) were written after the drafters' measured runs; the `EditorSessionTests` and `ShellTests` facts were then compiled and run green on a scratch copy of `4e0be14`, and the two `CanvasInteractionTests` facts are checked by the implementer's run at Task 2 Step 3. On a desktop whose display scale is not 100%, M3a's `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails before and after every task alike (157.6 against 158, measured at `4e0be14`); every Expected line assumes a 100% display, as CI runs.

---

### Task 1: Engine overlay, live preview, render cost, selection geometry and the view-select goldens

**Files:**

- Create:
  - Engine: `src/RBX15.Engine/LivePreview.cs`, `src/RBX15.Engine/ViewRenderer.cs`, `src/RBX15.Engine/SelectionGeometry.cs`.
  - Engine tests: `src/RBX15.Engine.Tests/SelectionGeometryTests.cs`, `LayerOpsEditTests.cs`, `ViewRendererTests.cs`, `RendererOverlayTests.cs`, `LivePreviewTests.cs`, `GoldenViewSelectTests.cs`.
  - Harness: `tools/parity-ref/view-select-ref.js`, `tools/parity-ref/view-select-scenes.json`.
  - Goldens: `tests/goldens/view-select/` with 10 PNGs, `index.json` and `scenes.json`. The controller captures them once in Step 9; they are frozen from then on.
  - Controller scratch (git-ignored): `.superpowers/m3b/freeze-view-select-ref.py`.
- Modify:
  - `src/RBX15.Engine/ViewOptions.cs`: `SelectedLayerId` and `Preview`, init-only.
  - `src/RBX15.Engine/Renderer.cs`: the `RenderComposite` preview overload, `RenderView` through `ViewRenderer`, `DrawSelectionOverlay`, `DrawRotationLabel`, and `TemplateInfo`. `NearestSampling` and `CheckerTile` go from private to internal.
  - `src/RBX15.Engine/Tools.cs`: `MinBrushSize`, `MaxBrushSize`, `DefaultBrushSize`, `SanitizeWidth`, `StepBrushSize` and `DrawStroke`.
  - `src/RBX15.Engine/Eraser.cs`: `DrawStroke` and `ApplyTo`.
  - `src/RBX15.Engine/LayerOps.cs`: the 12 edits of sheet §1 and the `LayerAdjustments` record.
  - `src/RBX15.Engine.Tests/GoldenRule.cs`: `AssertImageMatchesExcept`. The thresholds and `ImageDiff` are unchanged.
  - `src/RBX15.Engine.Tests/ToolsTests.cs` (+19) and `src/RBX15.Engine.Tests/EraserTests.cs` (+5), both appended.
  - `tools/parity-ref/README.md`: the new tool, its command and the scene keys.
- Not modified:
  - `src/RBX15.Engine/History.cs` and `HistoryTests.cs`. The `History.Record` null guard already exists (`ArgumentNullException.ThrowIfNull(before)`, History.cs:72), and `HistoryTests.Capacity_must_be_positive_and_snapshots_non_null` already asserts `h.Record(null!)` throws, so the sheet's conditional `Record_refuses_null` is not added.
  - `src/RBX15.Engine/Document.cs`. `Layer` gains no field, so `ContentEquals` stays valid; `LayerOpsEditTests.Layer_gains_no_field` pins that.
  - `LayerOpsTests.cs`, `view-ref.js`, `view-scenes.json`, `tests/goldens/view` and every other golden, and every App file.
- Read only: `editor.js` (blob `2790c41e418cd5bfea82cf1ae4d71626a7b54e61`), `editor.html` (`401a5d5f33c4accbb452cb080c9c544a0f79a6ce`), `styles.css` (`65110106efac704fd80e9fcb841835ccc0b487da`), `lib/erase.js`, `tools/parity-ref/app-harness.js` (injected unchanged), `tools/parity-ref/fonts.js`, and `assets/fonts`.

**Interfaces:**

- Consumes:
  - M3a engine: `Renderer` (`RenderComposite`, `ViewSize`, `CheckerHalf`, `LayerSampling`, `DrawLayer`), `Tools` (`Stroke`, `ShapeTool`, `RenderStroke`, `CommitDrawing`, `DrawingName`), `Eraser` (`Apply`, `StrokeCanvas`, `EraseStroking`, `StrokeColor`, `HasContent`), `LayerOps` (`HitTest`, the private `RequireFreshId`), `Document`, `Layer`, `EditorSnapshot`, `ViewOptions`, `Fonts.CreateFont`, `Template.Regions` and `Template.Mask`, and `TemplateSize`.
  - Test helpers: `ImageDiff.Load`, `FromImage`, `Compare` and `Heatmap`, `RepoRoot.Find`, `SceneLoader.Parse`, `Px.Solid`, `Px.Render`, `Px.At` and `Px.Near`, and `GoldenRule`'s private `Report` and internal `AssertWordArtBitmaps`.
  - Harness: `app-harness.js` (`__pref.render`) and `fonts.js` (`respond`, `FONTS_DIR`).
- Produces (sheet §1, §3 and §6, verbatim names):
  - `ViewOptions.SelectedLayerId` (`int?`) and `ViewOptions.Preview` (`LivePreview?`), both init-only.
  - `LivePreview`: `Draw(Stroke)`, `Erase(IReadOnlyList<SKPoint>, float)`, `Stroke`, `IsEraser` and `Alpha`.
  - `ViewRenderer : IDisposable`: `Render(SKCanvas, Document, ViewOptions)` and `Dispose()`. `Renderer.RenderView` keeps its signature and runs through a `ViewRenderer` made for the call.
  - `internal static void Renderer.RenderComposite(SKCanvas, Document, bool, LivePreview?, SKSurface?)` and `internal static void Renderer.DrawSelectionOverlay(SKCanvas, Layer, ViewOptions)`.
  - `SelectionGeometry`: `RotateHandleDistance`, `RotateHitRadius`, `ResizeHitRadius`, `MinScale`, `MaxScale`, `WheelStep`, `SnapTolerance`, `SnapAngles`, `Center`, `RotateHandle`, `Corner`, `HitTest`, `PointerAngle`, `RotateStartOffset`, `RotateAngle`, `NormalizeRotation`, `SnapRotation`, `ResizeScale`, `ClampScale`, `WheelScale`, `RotationLabel` and `LabelBounds`. Also `enum ResizeCorner`, `enum SelectHitKind` and `readonly record struct SelectHit`.
  - `LayerOps`: `MoveTo`, `SetScale`, `ScaleAboutCenter`, `SetRotation`, `Flip`, `SetOpacity`, `SetName`, `SetAdjustments`, `ResetAdjustments`, `Reorder`, `DropIndices` and `AddDrawing`. Also `sealed record LayerAdjustments` with `Default` and `From`.
  - `Tools`: `MinBrushSize`, `MaxBrushSize`, `DefaultBrushSize`, `SanitizeWidth`, `StepBrushSize` and the internal `DrawStroke`.
  - `Eraser`: the internal `DrawStroke` and the public `ApplyTo`.
  - `GoldenRule.AssertImageMatchesExcept(string name, string goldenPath, SKImage rendered, string heatmapPath, IReadOnlyList<SKRectI> excluded)`.
  - `tools/parity-ref/view-select-ref.js`, `view-select-scenes.json` and the frozen `tests/goldens/view-select/`.
  - Additions beyond the sheet are listed at the end: `Renderer.DrawRotationLabel`, `Renderer.TemplateInfo`, the internal `Renderer.NearestSampling` and `Renderer.CheckerTile`, and `SelectionGeometry.ToFixed1`.

**What 4.1 does (cited).** All of it is `editor.js` at the blob above.

- **The persistent offscreen.** `_offCanvas` is 585x559 and is allocated once, then reused by every render (editor.js:295-298). The checker tile `_checkerboard` is also built once (:431-438).
- **The live preview.** render() composites `drawCanvas` after the layers and before the mask. It uses `oc.globalAlpha = currentTool === 'eraser' ? 0.5 : 1` and `oc.drawImage(drawCanvas, 0, 0)` (:324-330). The mask follows only `if (showMask)` (:332-337).
  - On mousedown the pen and the eraser set `strokeStyle` (#f22f89 for the eraser), `lineWidth = drawSize`, round caps and joins, then `moveTo` (:1133-1144). Each mousemove does `lineTo` and `stroke` (:1200-1203).
  - Line, Rect and Ellipse clear the stroke canvas and redraw from the press point to the pointer on every move (:1205-1234).
- **The selection overlay** draws only in the Select tool with a selected layer that has an image (:361-364). Hidden layers are drawn too; that is the fix in ruling R2.
  - The frame: `w = img.width*scale*zoom` and `h` likewise, centered at `x*zoom + w/2, y*zoom + h/2`, under `translate` then `rotate(rotation*π/180)` (:365-372). Flips are not applied.
  - The box is `strokeRect(-w/2, -h/2, w, h)` in #17d4fa, width 2, with `setLineDash([5,5])` (:375-379).
  - The handles are `fillRect(hx-4, hy-4, 8, 8)` at TL, TR, BR and BL in #17d4fa (:382-384).
  - The stem runs from (0, -h/2) to (0, -h/2 - 30) in #f22f89, width 2 (:387-393).
  - The knob is `arc(0, -h/2-30, 8, 0, 2π)`, filled #f22f89 and stroked #fff at 1.5 (:395-401).
  - The glyph is `arc(0, -h/2-30, 4, -0.7π, 0.4π)` stroked #fff at 1.5, plus the arrowhead `moveTo(ax-3, ay-2)`, `lineTo(ax, ay)`, `lineTo(ax+2, ay-3)` with `ax = cos(0.4π)·4` and `ay = -h/2 - 30 + sin(0.4π)·4` (:403-414).
  - The label appears only `if (layer.rotation !== 0)`: `11px "JetBrains Mono"` in #f22f89, `textAlign = 'center'`, text `rotation.toFixed(1) + '°'` at (0, -h/2 - 30 - 16) (:416-420). styles.css:6 imports JetBrains Mono from Google Fonts, so 4.1 draws its fallback face until that font loads.
  - All sizes are canvas px of the zoomed view, so the handles keep their screen size at every zoom.
- **Select geometry.**
  - `getLayerCenter` is at :1023-1027.
  - `getRotHandlePos` uses `dist = h/2 + 30/zoom` and returns `(c.x - sin(r)·dist, c.y - cos(r)·dist)` (:1029-1039). That x is mirrored; see ruling T1-1.
  - `ptDist` is at :1041.
  - The press order (:1064-1117): the rotate handle within `< 14/zoom` (:1069), then the corners TL, TR, BR, BL within `< 10/zoom` (:1083-1088), then the topmost visible box (`pointInRotatedRect`, :1101-1111), else deselect (:1114).
  - The rotate start is `rotStartAngle = atan2(mx - c.x, -(my - c.y))·180/π - rotation` (:1073).
  - The rotate move computes `newRot = angle - rotStartAngle`, then snaps to the first of [-180, -135, ..., 180] within `< 3` (:1174-1180).
  - The resize move computes `ratio = d / resizeStartDist` and `scale = max(0.01, startScale·ratio)`, with x and y re-centered (:1183-1190).
- **Wheel.** In Select it applies `delta = deltaY > 0 ? 0.95 : 1.05` about the top-left (:1310-1311). In a draw tool it sets `drawSize = max(1, min(50, drawSize + (deltaY > 0 ? -1 : 1)))` (:1316-1318). The brush range is `min="1" max="50" value="4"` (editor.html:57).
- **Properties panel edits** (rebuilt by Task 3; this task supplies the pure ops):
  - Name (:825), X and Y (:826-827), Scale as `value/100` anchored at the top-left (:828-832).
  - Rotation (:833-837), Opacity as `value/100` (:838-842), Flip H and Flip V (:843-844).
  - The adjustment sliders (:873-907) with ranges B, C and S 0..200, hue 0..360, R, G and B -100..100 (:785-797), and Reset (:908-911).
- **The layer drop** (`handleLayerDrop`, :621-657): splice the dragged layer out, `findIndex` the target again (:645), then insert after it for 'top' or before it for 'bottom'.
- **The drawing commit** (`addDrawingLayer`, :528-540): push the autocropped drawing on top and select it.
- **The eraser commit** (:1265-1283): erase the selected layer through the inverse transform.

**Ledger rows this task serves** (Task 5 closes them on the evidence under "Handed to Task 5"; dispositions as the ledger has them):

- Engine render and overlay: `render-live-preview` (keep), `draw-preview-composite` (keep), `render-selection-box` (fix), `render-resize-handles` (keep), `render-rotation-handle` (keep), `render-rotation-label` (fix), `select-handle-render` (keep), `select-hidden-layer-still-handled` (fix, render half), `render-pipeline-canvas-reset` (keep: "cache ... output must remain identical").
- Select math: `select-hit-test` (keep), `select-hit-test-rect-not-alpha` (keep), `select-rotate-handle-hit` (fix), `select-resize-handle-hit` (keep), `select-resize-handles` (fix), `select-resize-drag` (fix), `select-rotate-drag` (fix), `select-rotate-drag-2` (fix), `select-move-hit-and-drag` (fix, engine half), `props-scale-clamp-inconsistency` (fix), `wheel-scale-layer` (fix, the math), `wheel-brush-size` (keep, the math).
- Edits and tools: `undo-empty-steps` (fix, the engine half: every op returns its input when nothing changes), `layers-model` (fix, `LayerAdjustments`; the 0 display is Task 3), `draw-pen-single-click` (fix, the preview dot), `draw-pen-incremental-stroke` (fix, preview and commit stroked once), `eraser-apply` (fix, `ApplyTo`), `draw-commit-layer` and `layers-add-drawing` (keep, `AddDrawing`), `layers-reorder-drag` (fix, the index math).

**Rulings.** The sheet's R2 (hidden layers), R3 (eraser click), R4 (label excluded), R5 (rotation), R6 (scale) and R12 (the preview is the commit) are applied as written. These are this task's own, each pinned by a named test:

1. **T1-1: the rotate handle is hit where it is drawn (fix; new divergence for Task 5).**
   - 4.1's `getRotHandlePos` returns `c.x - sin(r)·d` (editor.js:1036). Its overlay rotates (0, -d) to `(d·sin r, -d·cos r)` (editor.js:372, :396). Measured on 4.1's own capture: in `vs-select-rot30-z100` the knob is up and to the right of the layer.
   - A verbatim port would leave every rotated layer's knob unclickable.
   - `SelectionGeometry.RotateHandle` returns the drawn point. Task 2's press and hover use it.
   - Pinned: `SelectionGeometryTests.The_rotate_handle_turns_clockwise_with_the_layer` and `RendererOverlayTests.The_rotate_handle_is_hit_where_the_overlay_draws_it`.
2. **T1-2: the arc angles go to Skia as 4.1 passes them.** Blink's canvas `arc()` reaches Skia with the start angle as given. -0.7π stays -126 degrees, sweep 198, with no canonicalizing into [0, 2π).
   - Measured on a 10x10 sub-pixel grid of knobs drawn by Chromium 146 and by the engine: canonicalized (234 degrees), 100 of 100 positions differ by up to 46 levels; as given, 100 of 100 match exactly.
   - `Renderer.CanvasArc` documents it. Pinned by the view-select goldens.
3. **T1-3: the knob glyph's sub-pixel antialiasing, and one scene choice.**
   - **The finding.** With 4.1's own call structure (the arcs at (0, -h/2 - 30) in the layer frame), the engine matches Chromium exactly for most knob offsets. For a few, the 1.5 px white glyph's antialiasing differs.
     - The knob offset is `L = h/2 + 30`, in screen px.
     - Measured: 100 random device positions per offset, against Chromium 146 drawing 4.1's knob code.
       - Offsets 0, 10, 20, 30, 63, 67.5, 70, 80, 100, 129 and 130: exact.
       - Offsets 5, 35, 40, 45, 48.75, 50, 55 and 60: all 100 positions differ, by up to 41 levels, on at most about 14 px of the glyph.
     - Chromium's own output is identical for offsets 0, 30 and 48.75 but not for 67.5 or 130.
     - Drawing the knob in a frame centered on itself fixes 48.75 and breaks 67.5 and 130. So no construction reproduces Chromium at every offset: this is float rounding inside two different Skia builds (Chromium 146's, and SkiaSharp 4.153.1's), not geometry.
   - **The ruling.** Keep 4.1's call structure, which is exact at most offsets, the common 100% case included.
     - `vs-select-fliph-z50` puts its word art at scale 1.2 (offset 67.5) rather than 0.6 (offset 48.75, which measured 7 px off, max 24 levels).
     - The other scenes' offsets measured exact: 67.5, 130, 67.5, 86.25 and 75.
   - **Handed on.** Task 5 records the residual as a rasterization note, like `wordart-glyph-rasterization`, and the look sign-off shows it.
4. **T1-4: the label is JavaScript's `toFixed(1)`, not .NET's `"F1"`.** The sheet's comment says `"F1"` matches `toFixed`, and it does not.
   - .NET rounds an exact binary tie to even (`17.25.ToString("F1")` is "17.2", measured). `toFixed` takes the larger magnitude ("17.3").
   - `SelectionGeometry.ToFixed1` rounds the exact `"F20"` expansion half away from zero, and keeps the sign of a negative value that rounds to 0 ("-0.0").
   - Pinned: `ToFixed1_is_javascript_not_dotnet_F1_on_exact_ties` and 12 label rows checked against node's `toFixed` (17.25, -17.25, 0.25, -0.25, 1.45, 2.675, -179.95, -0.04, ...).
5. **T1-5: values that are not finite.**
   - `NormalizeRotation` maps NaN and the infinities to 0, which has no angle.
   - `MoveTo` and `SetOpacity` return the input document for a value that is not finite, so an empty or garbled field changes nothing.
   - `SetAdjustments` keeps the layer's current value for a field that is not finite.
   - `ClampScale(NaN)` is 0.01 (sheet R6). `ResizeScale` returns the start scale unchanged when the start distance is under 1e-9 (sheet: "d0 < 1e-9 -> start").
   - Pinned in `SelectionGeometryTests` and `LayerOpsEditTests`.
6. **T1-6: R4 kept, with a stronger cross-check.** The sheet excludes the label box because "any 1 px glyph-placement shift ... would still fail the flat background pixels around them".
   - Measured on the two label scenes: the box holds 160 and 222 differing pixels, max delta 54 and 51, every one an edge pixel, and zero interior differences. Under the Word Art rule the label passes.
   - So the gate blanks the box (R4, unchanged rule elsewhere) and also holds it to the Word Art rule: `GoldenViewSelectTests.The_label_box_meets_the_word_art_rule`. A misplaced or missing label turns flat 4.1 background pixels into interior differences.
   - `RendererOverlayTests` pins the face (vendored JetBrains Mono 400, 11 px), the color, the anchor and the text, and checks that no label pixel falls outside `LabelBounds`.
7. **T1-7: ScaleAboutCenter at an unchanged scale returns the input.** Recomputing x and y from the center would add a rounding-only change, which would record an empty step at the clamp. Pinned: `ScaleAboutCenter_at_the_limit_returns_the_same_document`.

**Measured while drafting.**

- **Setup.** A scratch copy of `4e0be14` (`git archive`, outside the repo), .NET SDK 10.0.302, SkiaSharp 4.153.1, and Electron 41.1.0 from the repo's `node_modules`, run offscreen. Nothing was built inside the repo, and its git state is untouched.
- **Build.** Every file below compiled under `-warnaserror` with 0 warnings. Each step was then staged on a second fresh copy, in step order, and every Expected line below is that staged run's output. The failing builds' first errors are quoted as they printed.
- **Capture.** `view-select-ref.js` ran twice with byte-identical PNGs and `index.json`. The 10-scene digest is `9f8f7e77261e35212928a42cadb5b1a9bf0e5485478e92de48d46ac99e648b99`, and the provenance equals `tests/goldens/app` and `tests/goldens/view`.
- **The golden gate.** All 10 pass under the unchanged rule, with only the label box blanked:

  | Scene | Size | Max delta | Differing pixels | Interior |
  | --- | --- | --- | --- | --- |
  | `vs-select-z100` | 585x559 | 0 | 0 | 0 |
  | `vs-select-z200` | 1170x1118 | 0 | 0 | 0 |
  | `vs-select-rot30-z100` | 585x559 | 1 | 668 | 0 |
  | `vs-select-rotneg135-z150` | 877x838 | 1 | 1541 | 0 |
  | `vs-select-fliph-z50` | 292x279 | 0 | 0 | 0 |
  | `vs-select-pants-grid-z100` | 585x559 | 0 | 0 | 0 |
  | `vs-preview-rect-z100` | 585x559 | 0 | 0 | 0 |
  | `vs-preview-ellipse-fill-z200` | 1170x1118 | 0 | 0 | 0 |
  | `vs-preview-line-z50` | 292x279 | 0 | 0 | 0 |
  | `vs-preview-erase-z100` | 585x559 | 0 | 0 | 0 |

  - **Overlay and preview.** Every overlay and preview pixel is exact.
  - **The two rotated scenes.** Their max-1 differences are antialiased edge pixels of the rotated word art layer in the composite, within the rule's edge tolerance. Checked: none of them is an overlay pixel.
  - **The label boxes.** Before blanking: 160 differing pixels (max 54) at 30 degrees and 222 (max 51) at -135 degrees, all edge pixels.
- **The suites.** The full suite passed twice in a row: Engine.Tests 877 (718 + 159), Parity.Tests 35.
  - App.Tests ran 410 of 411 in this environment both before and after the change. The failing fact is `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits`: it expects 158,71 and got 157.6,71.2. The cause in this environment was not investigated. It fails on the untouched baseline too, and Task 1 changes no App file. The Expected lines keep 411.
- **The preview is the commit (R12).** For pen, line, rect and ellipse, the preview composite and the composite with the committed drawing layer differ by at most 1 level. That is the premultiply round trip of `CommitDrawing`'s stored image.

**Test deltas:** Engine.Tests **+159**:

- new classes: SelectionGeometryTests 74, LayerOpsEditTests 19, ViewRendererTests 4, RendererOverlayTests 18, LivePreviewTests 6, GoldenViewSelectTests 14;
- appended to existing classes: ToolsTests +19, EraserTests +5.

App.Tests **+0**, Parity.Tests **+0**. Cumulative: Engine.Tests 718 to 877, App.Tests 411, Parity.Tests 35.

- [ ] **Step 1: Baseline**

Task 1 starts on the branch with M3a merged. Confirm the branch and the starting counts.

Run: `git branch --show-current && dotnet build src/RBX15.slnx -c Release -warnaserror && dotnet test src/RBX15.slnx -c Release`
Expected: `feat/v5-m3b-select-draw`, `Build succeeded.` with `0 Warning(s)`, then Engine.Tests `Passed: 718, Failed: 0, Skipped: 0`, Parity.Tests `Passed: 35, Failed: 0`, App.Tests `Passed: 411, Failed: 0`.

- [ ] **Step 2: Write the failing tool, eraser and preview tests**

These pin the M2a carry-forwards (width sanitizing, the eraser click ruling R3), the wheel's brush step, the shared stroke bodies the preview reuses (R12), and `LivePreview`.

In `src/RBX15.Engine.Tests/ToolsTests.cs`, append the new facts before the class's closing brace:

Replace:

```csharp
    public void Drawing_layers_are_named_after_the_4_1_tool_id(ShapeTool tool, string name) =>
        Assert.Equal(name, Tools.DrawingName(tool));
}
```

with:

```csharp
    public void Drawing_layers_are_named_after_the_4_1_tool_id(ShapeTool tool, string name) =>
        Assert.Equal(name, Tools.DrawingName(tool));

    // ── M3b: width sanitizing, the wheel's brush step, DrawStroke ──

    [Fact] // editor.html:57: the brush range 1..50, default 4
    public void Brush_size_constants_are_editor_html_57() =>
        Assert.Equal((1, 50, 4), (Tools.MinBrushSize, Tools.MaxBrushSize, Tools.DefaultBrushSize));

    [Theory] // M2a carry-forward: canvas ignores a lineWidth that is not finite or not positive, keeping its default 1
    [InlineData(0f, 1f)]
    [InlineData(-3f, 1f)]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, 1f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(4f, 4f)]
    [InlineData(50f, 50f)]
    public void SanitizeWidth_keeps_canvas_default_1_for_bad_widths(float width, float sanitized) =>
        Assert.Equal(sanitized, Tools.SanitizeWidth(width));

    [Fact] // a bad width draws exactly like width 1, never Skia's hairline (width 0) or a NaN path
    public void A_bad_width_strokes_like_width_1()
    {
        foreach (var tool in new[] { ShapeTool.Pen, ShapeTool.Line, ShapeTool.Rect, ShapeTool.Ellipse })
        {
            using var one = Tools.RenderStroke(Shape(tool, 100, 100, 200, 160, width: 1));
            foreach (var bad in new[] { 0f, -2f, float.NaN })
            {
                using var got = Tools.RenderStroke(Shape(tool, 100, 100, 200, 160, width: bad));
                Assert.Equal(0, Differing(one, got));
            }
        }
        using var dot = Tools.RenderStroke(new Stroke(ShapeTool.Pen, new[] { new SKPoint(50.5f, 50.5f) }, Cyan, float.NaN, false));
        using var dotOne = Tools.RenderStroke(new Stroke(ShapeTool.Pen, new[] { new SKPoint(50.5f, 50.5f) }, Cyan, 1, false));
        Assert.Equal(0, Differing(dotOne, dot));
    }

    [Theory] // wheel-brush-size (editor.js:1316-1318): one step per event by sign, clamped to 1..50; sign 0 grows
    [InlineData(4, 1, 5)]
    [InlineData(4, -1, 3)]
    [InlineData(4, 0, 5)]
    [InlineData(4, 120, 5)]
    [InlineData(4, -120, 3)]
    [InlineData(50, 1, 50)]
    [InlineData(1, -1, 1)]
    [InlineData(60, -1, 50)]
    public void StepBrushSize_is_one_step_by_sign(int size, int sign, int next) =>
        Assert.Equal(next, Tools.StepBrushSize(size, sign));

    [Fact] // ruling R12: DrawStroke into a cleared template canvas is RenderStroke, for every tool
    public void DrawStroke_is_the_RenderStroke_body()
    {
        foreach (var tool in new[] { ShapeTool.Pen, ShapeTool.Line, ShapeTool.Rect, ShapeTool.Ellipse })
        {
            var stroke = new Stroke(tool, new[] { new SKPoint(80, 60), new SKPoint(140, 200), new SKPoint(300, 120) }, Cyan, 6, tool == ShapeTool.Rect);
            using var rendered = Tools.RenderStroke(stroke);
            using var surface = SKSurface.Create(new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            Tools.DrawStroke(surface.Canvas, stroke);
            using var drawn = surface.Snapshot();
            Assert.Equal(0, Differing(rendered, drawn));
        }
        Assert.Throws<ArgumentNullException>(() => Tools.RenderStroke(null!));
    }
}
```

In `src/RBX15.Engine.Tests/EraserTests.cs`, append before the class's closing brace. `At`, `SolidLayer`, `AlphaAt`, `Alphas`, `Zigzag`, `TW` and `TH` are the file's existing helpers. The M2a fact `A_click_without_movement_erases_nothing` stays; the new `A_press_without_movement_erases_nothing` is ruling R3's pin at the document level:

Replace:

```csharp
        Assert.True(fullyErased > 0);
    }
}
```

with:

```csharp
        Assert.True(fullyErased > 0);
    }

    // ---- M3b: the eraser on the selection ----

    static Document TwoLayers(bool firstVisible = true) => new()
    {
        Layers = System.Collections.Immutable.ImmutableList.Create(
            At(SolidLayer(100, 100), x: 50, y: 50, id: 1) with { Visible = firstVisible },
            At(SolidLayer(100, 100), x: 300, y: 50, id: 2)),
    };

    [Fact] // ruling R3: a press without movement erases nothing and changes no document (no undo step)
    public void A_press_without_movement_erases_nothing()
    {
        var doc = TwoLayers();
        Assert.Same(doc, Eraser.ApplyTo(doc, 1, [new SKPoint(100, 100)], 20));
        Assert.Same(doc, Eraser.ApplyTo(doc, 1, [], 20));
        using var surface = SKSurface.Create(new SKImageInfo(TW, TH, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        Eraser.DrawStroke(surface.Canvas, [new SKPoint(100, 100)], 20, EraseStroking.Once);
        using var blank = surface.Snapshot();
        Assert.False(Eraser.HasContent(blank));
    }

    [Fact] // eraser-apply, eraser-selection-semantics: only the given layer changes; the rest keep their instances
    public void ApplyTo_erases_the_given_layer_and_keeps_the_rest()
    {
        var doc = TwoLayers();
        var erased = Eraser.ApplyTo(doc, 1, [new SKPoint(60, 100), new SKPoint(140, 100)], 10);
        Assert.NotSame(doc, erased);
        Assert.Equal(new[] { 1, 2 }, erased.Layers.Select(l => l.Id));
        Assert.NotSame(doc.Layers[0].Image, erased.Layers[0].Image);
        Assert.Same(doc.Layers[1], erased.Layers[1]);
        Assert.Equal(0, AlphaAt(erased.Layers[0].Image, 50, 50));   // template (100, 100) is image (50, 50)
        Assert.Equal(255, AlphaAt(erased.Layers[0].Image, 50, 10));
        Assert.Equal((50.0, 50.0, 1.0), (erased.Layers[0].X, erased.Layers[0].Y, erased.Layers[0].Scale));
    }

    [Fact] // ruling R2 and eraser-apply: a hidden selected layer still erases, as in 4.1
    public void ApplyTo_erases_a_hidden_layer()
    {
        var doc = TwoLayers(firstVisible: false);
        var erased = Eraser.ApplyTo(doc, 1, [new SKPoint(60, 100), new SKPoint(140, 100)], 10);
        Assert.Equal(0, AlphaAt(erased.Layers[0].Image, 50, 50));
        Assert.False(erased.Layers[0].Visible);
    }

    [Fact] // no step when nothing changes: a missing id, or a drag that misses the template
    public void ApplyTo_returns_the_same_document_when_nothing_is_erased()
    {
        var doc = TwoLayers();
        Assert.Same(doc, Eraser.ApplyTo(doc, 9, [new SKPoint(60, 100), new SKPoint(140, 100)], 10));
        Assert.Same(doc, Eraser.ApplyTo(doc, 1, [new SKPoint(-100, -100), new SKPoint(-50, -50)], 10));
        Assert.Throws<ArgumentNullException>(() => Eraser.ApplyTo(null!, 1, [], 1));
        Assert.Throws<ArgumentNullException>(() => Eraser.ApplyTo(doc, 1, null!, 1));
    }

    [Fact] // DrawStroke is StrokeCanvas's body, in both stroking modes
    public void DrawStroke_is_the_StrokeCanvas_body()
    {
        foreach (var stroking in new[] { EraseStroking.Once, EraseStroking.Replay41 })
        {
            using var canvas = Eraser.StrokeCanvas(Zigzag, 20, stroking);
            using var surface = SKSurface.Create(new SKImageInfo(TW, TH, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            Eraser.DrawStroke(surface.Canvas, Zigzag, 20, stroking);
            using var drawn = surface.Snapshot();
            Assert.Equal(Alphas(canvas), Alphas(drawn));
        }
    }
}
```

Create `src/RBX15.Engine.Tests/LivePreviewTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>LivePreview: the stroke in progress (editor.js:1120-1234) and its alpha (editor.js:327). Ruling R12.</summary>
public class LivePreviewTests
{
    [Fact] // a draw preview keeps the stroke as given, at alpha 1, and owns a copy of the points
    public void Draw_keeps_the_stroke_and_copies_the_points()
    {
        var points = new List<SKPoint> { new(10, 10), new(20, 30) };
        var stroke = new Stroke(ShapeTool.Rect, points, SKColors.Lime, 6, true);
        var p = LivePreview.Draw(stroke);
        points.Add(new SKPoint(99, 99));
        Assert.Equal(new[] { new SKPoint(10, 10), new SKPoint(20, 30) }, p.Stroke.Points);
        Assert.Equal((ShapeTool.Rect, SKColors.Lime, 6f, true), (p.Stroke.Tool, p.Stroke.Color, p.Stroke.Width, p.Stroke.Fill));
        Assert.False(p.IsEraser);
        Assert.Equal(1f, p.Alpha);
    }

    [Theory] // editor.js:1138-1140: an opaque #f22f89 pen at the brush size, round caps; Tools.SanitizeWidth applies
    [InlineData(12f, 12f)]
    [InlineData(0f, 1f)]
    [InlineData(-4f, 1f)]
    [InlineData(float.NaN, 1f)]
    public void Erase_is_a_magenta_pen_at_half_alpha(float size, float width)
    {
        var points = new List<SKPoint> { new(1, 2), new(3, 4) };
        var p = LivePreview.Erase(points, size);
        points.Clear();
        Assert.True(p.IsEraser);
        Assert.Equal(0.5f, p.Alpha);
        Assert.Equal((ShapeTool.Pen, Eraser.StrokeColor, width, false), (p.Stroke.Tool, p.Stroke.Color, p.Stroke.Width, p.Stroke.Fill));
        Assert.Equal(2, p.Stroke.Points.Count);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => LivePreview.Draw(null!));
        Assert.Throws<ArgumentNullException>(() => LivePreview.Draw(new Stroke(ShapeTool.Pen, null!, SKColors.Red, 1, false)));
        Assert.Throws<ArgumentNullException>(() => LivePreview.Erase(null!, 4));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL. The errors include `error CS0103: The name 'LivePreview' does not exist in the current context`, `error CS0117: 'Eraser' does not contain a definition for 'ApplyTo'`, `error CS0117: 'Eraser' does not contain a definition for 'DrawStroke'`, and `error CS0117: 'Tools' does not contain a definition for 'SanitizeWidth'` (also `MinBrushSize`, `MaxBrushSize`, `DefaultBrushSize`, `DrawStroke`).

- [ ] **Step 3: Implement the tool and eraser members and LivePreview**

In `src/RBX15.Engine/Tools.cs`, make these replacements in order. Each old block is unique in the file at the point it is applied:

Edit 1 of 5. Replace:

```csharp
public static class Tools
{
    /// <summary>The name 4.1 gives a drawing layer: the tool id capitalized plus ' drawing' (editor.js:1285).</summary>
    public static string DrawingName(ShapeTool tool) => tool switch
```

with:

```csharp
public static class Tools
{
    /// <summary>The brush size range and default (editor.html:57: range 1..50, value 4).</summary>
    public const int MinBrushSize = 1, MaxBrushSize = 50, DefaultBrushSize = 4;

    /// <summary>
    /// Canvas2D's lineWidth rule: assigning a width that is not finite, or 0 or less, is ignored, so a fresh stroke canvas
    /// keeps its default of 1 (HTML canvas, "lineWidth"). Otherwise the width is unchanged. <see cref="RenderStroke"/>,
    /// <see cref="DrawStroke"/>, <see cref="CommitDrawing"/>, the eraser and <see cref="LivePreview"/> all apply it, so a
    /// bad width can never become Skia's hairline (width 0) or a NaN path (M2a carry-forward: width sanitizing).
    /// </summary>
    public static float SanitizeWidth(float width) => float.IsFinite(width) && width > 0 ? width : 1;

    /// <summary>
    /// The wheel's brush step in a draw tool (editor.js:1316-1318): one size per event, sign only, clamped to 1..50. A
    /// <paramref name="sign"/> of 0 or more grows, as 4.1's <c>deltaY &lt;= 0</c> does (ledger wheel-brush-size).
    /// </summary>
    public static int StepBrushSize(int size, int sign) => Math.Clamp(size + (sign >= 0 ? 1 : -1), MinBrushSize, MaxBrushSize);

    /// <summary>The name 4.1 gives a drawing layer: the tool id capitalized plus ' drawing' (editor.js:1285).</summary>
    public static string DrawingName(ShapeTool tool) => tool switch
```

Edit 2 of 5. Replace:

```csharp
    public static SKImage RenderStroke(Stroke stroke)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var pts = stroke.Points;
        if (stroke.Tool == ShapeTool.Pen)
```

with:

```csharp
    public static SKImage RenderStroke(Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        surface.Canvas.Clear(SKColors.Transparent);
        DrawStroke(surface.Canvas, stroke);
        return surface.Snapshot();
    }

    /// <summary>
    /// The stroke drawn into an existing, already cleared, template-size kN32 canvas: the body of
    /// <see cref="RenderStroke"/>, shared with the live preview (<see cref="LivePreview"/>, M3b ruling R12) so the
    /// preview and the commit draw the same pixels. The width goes through <see cref="SanitizeWidth"/>.
    /// </summary>
    internal static void DrawStroke(SKCanvas canvas, Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(stroke);
        var pts = stroke.Points;
        if (stroke.Tool == ShapeTool.Pen)
```

Edit 3 of 5. Replace:

```csharp
            DrawShape(canvas, stroke, pts[0], pts[pts.Count - 1]);
        }
        return surface.Snapshot();
    }
```

with:

```csharp
            DrawShape(canvas, stroke, pts[0], pts[pts.Count - 1]);
        }
    }
```

Edit 4 of 5. Replace:

```csharp
    static SKPaint StrokePaint(Stroke s, SKStrokeJoin join) => new()
    {
        Color = s.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = s.Width,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = join, StrokeMiter = 10,
    };
```

with:

```csharp
    static SKPaint StrokePaint(Stroke s, SKStrokeJoin join) => new()
    {
        Color = s.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = SanitizeWidth(s.Width),
        StrokeCap = SKStrokeCap.Round, StrokeJoin = join, StrokeMiter = 10,
    };
```

Edit 5 of 5. Replace:

```csharp
        {
            using var dot = FillPaint(s);
            canvas.DrawCircle(pts[0], s.Width / 2, dot);
            return;
        }
```

with:

```csharp
        {
            using var dot = FillPaint(s);
            canvas.DrawCircle(pts[0], SanitizeWidth(s.Width) / 2, dot);
            return;
        }
```

In `src/RBX15.Engine/Eraser.cs`, make this replacement. It rewrites the body of `StrokeCanvas` (its signature and doc comment stay) and adds `DrawStroke` and `ApplyTo` after it:

Replace:

```csharp
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (points.Count >= 2)
        {
            using var paint = new SKPaint
            {
                Color = StrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = float.IsFinite(size) && size > 0 ? size : 1,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
            };
            // Once: only the full path. Replay41: the path through points[0..end] after every move.
            int firstEnd = stroking == EraseStroking.Replay41 ? 1 : points.Count - 1;
            for (int end = firstEnd; end < points.Count; end++)
            {
                using var builder = new SKPathBuilder();
                builder.MoveTo(points[0].X, points[0].Y);
                for (int k = 1; k <= end; k++) builder.LineTo(points[k].X, points[k].Y);
                using var path = builder.Detach();
                canvas.DrawPath(path, paint);
            }
        }
        return surface.Snapshot();
    }
```

with:

```csharp
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        surface.Canvas.Clear(SKColors.Transparent);
        DrawStroke(surface.Canvas, points, size, stroking);
        return surface.Snapshot();
    }

    /// <summary>
    /// The eraser stroke into an existing, already cleared, template-size kN32 canvas: <see cref="StrokeColor"/>,
    /// <see cref="Tools.SanitizeWidth"/>(<paramref name="size"/>), round caps and joins, antialiased. Fewer than two points
    /// draw nothing (M3b ruling R3). Shared by <see cref="StrokeCanvas"/> and the live preview (<see cref="LivePreview"/>).
    /// </summary>
    internal static void DrawStroke(SKCanvas canvas, IReadOnlyList<SKPoint> points, float size, EraseStroking stroking)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2) return;
        using var paint = new SKPaint
        {
            Color = StrokeColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Tools.SanitizeWidth(size),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        // Once: only the full path. Replay41: the path through points[0..end] after every move.
        int firstEnd = stroking == EraseStroking.Replay41 ? 1 : points.Count - 1;
        for (int end = firstEnd; end < points.Count; end++)
        {
            using var builder = new SKPathBuilder();
            builder.MoveTo(points[0].X, points[0].Y);
            for (int k = 1; k <= end; k++) builder.LineTo(points[k].X, points[k].Y);
            using var path = builder.Detach();
            canvas.DrawPath(path, paint);
        }
    }

    /// <summary>
    /// The eraser on the selection (editor.js:1265-1283; ledger eraser-apply, eraser-selection-semantics): the document
    /// with layer <paramref name="layerId"/> replaced by <see cref="Apply(Layer, IReadOnlyList{SKPoint}, float)"/>, stroked
    /// once. A hidden layer erases too, as in 4.1 (M3b ruling R2). Returns <paramref name="doc"/> itself, so the caller
    /// records no undo step, when the id is missing or the stroke erased nothing (fewer than two points, or entirely off
    /// the template).
    /// </summary>
    public static Document ApplyTo(Document doc, int layerId, IReadOnlyList<SKPoint> points, float size)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(points);
        int i = doc.Layers.FindIndex(l => l.Id == layerId);
        if (i < 0) return doc;
        var layer = doc.Layers[i];
        var erased = Apply(layer, points, size);
        return ReferenceEquals(erased, layer) ? doc : doc with { Layers = doc.Layers.SetItem(i, erased) };
    }
```

Create `src/RBX15.Engine/LivePreview.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The live drawing preview of render() (editor.js:324-330): 4.1 composites its template-size drawCanvas over the layers
/// and before the mask, at globalAlpha 0.5 for the eraser and 1 for every other tool (ledger render-live-preview,
/// draw-preview-composite). v5 keeps the stroke, not a canvas, and draws it the way its commit draws it (M3b ruling R12,
/// "the preview is the commit"):
/// <list type="bullet">
/// <item><see cref="Draw"/>: <see cref="Tools.DrawStroke"/>, the path <see cref="Tools.CommitDrawing"/> autocrops, so a pen
///   preview is the whole path stroked once and a pen press shows its dot (approved divergences draw-pen-incremental-stroke,
///   draw-pen-single-click).</item>
/// <item><see cref="Erase"/>: <see cref="Eraser.DrawStroke"/> stroked once at half alpha. Fewer than 2 points show nothing,
///   which is exactly what a press without movement erases (ruling R3).</item>
/// </list>
/// Both factories copy the points, so the interaction can keep extending its own list.
/// </summary>
public sealed record LivePreview
{
    LivePreview(Stroke stroke, bool isEraser)
    {
        Stroke = stroke;
        IsEraser = isEraser;
    }

    /// <summary>A pen, line, rect or ellipse stroke in progress (editor.js:1196-1234). Alpha 1.</summary>
    public static LivePreview Draw(Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(stroke.Points);
        return new LivePreview(stroke with { Points = stroke.Points.ToArray() }, isEraser: false);
    }

    /// <summary>
    /// An eraser drag in progress (editor.js:1133-1144, :1200-1203): opaque <see cref="Eraser.StrokeColor"/>, round caps
    /// and joins, width <see cref="Tools.SanitizeWidth"/>(<paramref name="size"/>). Alpha 0.5 (editor.js:327).
    /// </summary>
    public static LivePreview Erase(IReadOnlyList<SKPoint> points, float size)
    {
        ArgumentNullException.ThrowIfNull(points);
        return new LivePreview(new Stroke(ShapeTool.Pen, points.ToArray(), Eraser.StrokeColor, Tools.SanitizeWidth(size), false), isEraser: true);
    }

    /// <summary>The stroke drawn. For an eraser preview: <see cref="ShapeTool.Pen"/>, <see cref="Eraser.StrokeColor"/>, the sanitized size, no fill.</summary>
    public Stroke Stroke { get; }

    /// <summary>True for an eraser preview.</summary>
    public bool IsEraser { get; }

    /// <summary>The preview's globalAlpha: 0.5 for the eraser, else 1 (editor.js:327).</summary>
    public float Alpha => IsEraser ? 0.5f : 1f;
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ToolsTests|FullyQualifiedName~RBX15.Engine.Tests.EraserTests|FullyQualifiedName~RBX15.Engine.Tests.LivePreviewTests"`
Expected: `Passed!  - Failed:     0, Passed:    71, Skipped:     0, Total:    71` for `RBX15.Engine.Tests.dll` (ToolsTests 41, EraserTests 24, LivePreviewTests 6).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 748, Failed: 0, Skipped: 0` (718 + 30), Parity.Tests `35`, App.Tests `411`. The frozen eraser and shape goldens (GoldenAppTests) are unchanged: `StrokeCanvas` and `RenderStroke` run the same calls, and `SanitizeWidth` is the identity on every golden's width.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/Tools.cs src/RBX15.Engine/Eraser.cs src/RBX15.Engine/LivePreview.cs src/RBX15.Engine.Tests/ToolsTests.cs src/RBX15.Engine.Tests/EraserTests.cs src/RBX15.Engine.Tests/LivePreviewTests.cs
git commit -m "feat(engine): brush width sanitizing, the wheel's brush step and the live stroke preview"
```

- [ ] **Step 4: Write the failing selection geometry and layer edit tests**

Create `src/RBX15.Engine.Tests/SelectionGeometryTests.cs`:

```csharp
using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// SelectionGeometry: the Select tool's press, rotate, resize and wheel math (editor.js:1018-1192, :1305-1313) and the
/// overlay label's text and bounds (editor.js:415-420). Ledger rows: select-hit-test, select-rotate-handle-hit,
/// select-resize-handle-hit, select-resize-handles, select-resize-drag, select-rotate-drag, select-rotate-drag-2,
/// select-move-hit-and-drag, select-hidden-layer-still-handled, props-scale-clamp-inconsistency, wheel-scale-layer,
/// render-rotation-label.
/// </summary>
public class SelectionGeometryTests
{
    static Layer L(int id = 1, int w = 100, int h = 50, double x = 200, double y = 200, double scale = 1, double rotation = 0, bool visible = true) =>
        new() { Id = id, Image = Px.Solid(w, h, SKColors.Red), X = x, Y = y, Scale = scale, Rotation = rotation, Visible = visible };

    static Document Doc(params Layer[] layers) => new() { Layers = ImmutableList.Create(layers) };

    static void Near((double X, double Y) expected, (double X, double Y) actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    [Fact] // getLayerCenter (editor.js:1023-1027)
    public void Center_is_the_scaled_box_center() =>
        Near((110, 70), SelectionGeometry.Center(L(w: 100, h: 50, x: 10, y: 20, scale: 2)));

    [Fact] // editor.js:1080-1088: center + R(rotation) * (±w/2, ±h/2), TL TR BR BL
    public void Corners_rotate_about_the_center_in_4_1_order()
    {
        var unrotated = L();
        Near((200, 200), SelectionGeometry.Corner(unrotated, ResizeCorner.TopLeft));
        Near((300, 200), SelectionGeometry.Corner(unrotated, ResizeCorner.TopRight));
        Near((300, 250), SelectionGeometry.Corner(unrotated, ResizeCorner.BottomRight));
        Near((200, 250), SelectionGeometry.Corner(unrotated, ResizeCorner.BottomLeft));
        var quarter = L(rotation: 90); // center (250, 225): local (x, y) maps to (250 - y, 225 + x)
        Near((275, 175), SelectionGeometry.Corner(quarter, ResizeCorner.TopLeft));
        Near((275, 275), SelectionGeometry.Corner(quarter, ResizeCorner.TopRight));
        Near((225, 275), SelectionGeometry.Corner(quarter, ResizeCorner.BottomRight));
        Near((225, 175), SelectionGeometry.Corner(quarter, ResizeCorner.BottomLeft));
    }

    [Theory] // editor.js:1035: h/2 + 30/zoom, so the knob stays 30 screen px above the top edge
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void The_rotate_handle_is_30_screen_px_above_the_top_edge(double zoom) =>
        Near((250, 200 - 30 / zoom), SelectionGeometry.RotateHandle(L(), zoom));

    [Fact] // ruling T1-1: rotate(r) maps (0, -d) to (d sin r, -d cos r); 4.1's getRotHandlePos uses c.x - sin r * d (mirrored)
    public void The_rotate_handle_turns_clockwise_with_the_layer()
    {
        var layer = L(rotation: 90); // center (250, 225), d = 25 + 30
        Near((305, 225), SelectionGeometry.RotateHandle(layer, 1));
        Near((250 + 55 * Math.Sin(Math.PI / 6), 225 - 55 * Math.Cos(Math.PI / 6)), SelectionGeometry.RotateHandle(layer with { Rotation = 30 }, 1));
        Near((250, 280), SelectionGeometry.RotateHandle(layer with { Rotation = 180 }, 1)); // 4.1 agrees only at 0 and 180
        var doc = Doc(layer);
        Assert.Equal(new SelectHit(SelectHitKind.Rotate, 1, default), SelectionGeometry.HitTest(doc, 1, 305, 225, 1));
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(doc, 1, 195, 225, 1).Kind); // 4.1's mirrored spot
    }

    [Theory] // select-rotate-handle-hit parityCheck: 13.9 hits, 14.1 misses (screen px, strict <)
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void The_rotate_handle_hits_strictly_inside_14_screen_px(double zoom)
    {
        var doc = Doc(L());
        var (hx, hy) = SelectionGeometry.RotateHandle(doc.Layers[0], zoom);
        Assert.Equal(SelectHitKind.Rotate, SelectionGeometry.HitTest(doc, 1, hx, hy - 13.9 / zoom, zoom).Kind);
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(doc, 1, hx, hy - 14.1 / zoom, zoom).Kind);
    }

    [Theory] // select-resize-handle-hit parityCheck: 9.9 hits, 10.1 misses, each corner in 4.1's order
    [InlineData(ResizeCorner.TopLeft, -1, -1)]
    [InlineData(ResizeCorner.TopRight, 1, -1)]
    [InlineData(ResizeCorner.BottomRight, 1, 1)]
    [InlineData(ResizeCorner.BottomLeft, -1, 1)]
    public void A_corner_hits_strictly_inside_10_screen_px(ResizeCorner corner, int dx, int dy)
    {
        var doc = Doc(L());
        var (cx, cy) = SelectionGeometry.Corner(doc.Layers[0], corner);
        double u = Math.Sqrt(0.5);
        foreach (double zoom in new[] { 0.5, 1, 2 })
        {
            Assert.Equal(new SelectHit(SelectHitKind.Resize, 1, corner),
                SelectionGeometry.HitTest(doc, 1, cx + dx * u * 9.9 / zoom, cy + dy * u * 9.9 / zoom, zoom));
            Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(doc, 1, cx + dx * u * 10.1 / zoom, cy + dy * u * 10.1 / zoom, zoom).Kind);
        }
    }

    [Fact] // editor.js:1064-1117 order: knob, corners, then the topmost visible box
    public void The_knob_beats_other_layers_and_a_corner_beats_the_body()
    {
        var selected = L(id: 1);
        var above = L(id: 2, w: 40, h: 40, x: 230, y: 150); // on top, covering the knob at (250, 170)
        var doc = Doc(selected, above);
        Assert.Equal(new SelectHit(SelectHitKind.Rotate, 1, default), SelectionGeometry.HitTest(doc, 1, 250, 170, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Resize, 1, ResizeCorner.TopLeft), SelectionGeometry.HitTest(doc, 1, 203.5, 203.5, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 2, default), SelectionGeometry.HitTest(doc, null, 250, 170, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, 1, 250, 225, 1));
    }

    [Fact] // ruling R2, select-hidden-layer-still-handled: no handles on a hidden layer; the press falls through to the pick
    public void A_hidden_selected_layer_has_no_handles()
    {
        var under = L(id: 1, w: 585, h: 559, x: 0, y: 0);
        var hidden = L(id: 2, visible: false);
        var doc = Doc(under, hidden);
        var (kx, ky) = SelectionGeometry.RotateHandle(hidden, 1);
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, 2, kx, ky, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, 2, 200, 200, 1));
        var alone = Doc(hidden);
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(alone, 2, kx, ky, 1).Kind);
        Assert.Equal(SelectHitKind.None, SelectionGeometry.HitTest(alone, 2, 250, 225, 1).Kind);
    }

    [Fact] // select-hit-test: no selection, or a selection that is gone, is just the topmost visible box or none
    public void Without_a_live_selection_the_press_is_the_pick()
    {
        var doc = Doc(L(id: 1), L(id: 2, x: 250));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 2, default), SelectionGeometry.HitTest(doc, null, 275, 225, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 2, default), SelectionGeometry.HitTest(doc, 99, 275, 225, 1));
        Assert.Equal(new SelectHit(SelectHitKind.Move, 1, default), SelectionGeometry.HitTest(doc, null, 210, 225, 1));
        Assert.Equal(new SelectHit(SelectHitKind.None, null, default), SelectionGeometry.HitTest(doc, null, 10, 10, 1));
        Assert.Throws<ArgumentNullException>(() => SelectionGeometry.HitTest(null!, null, 0, 0, 1));
    }

    [Theory] // atan2(dx, -dy): 0 is up, clockwise positive (editor.js:1076, :1176)
    [InlineData(0, -10, 0)]
    [InlineData(10, 0, 90)]
    [InlineData(0, 10, 180)]
    [InlineData(-10, 0, -90)]
    [InlineData(10, -10, 45)]
    public void The_pointer_angle_is_0_up_and_clockwise_positive(double dx, double dy, double degrees) =>
        Assert.Equal(degrees, SelectionGeometry.PointerAngle((100, 100), 100 + dx, 100 + dy), 9);

    [Fact] // editor.js:1076: the offset keeps the layer's rotation under the press point
    public void A_press_without_movement_keeps_the_rotation()
    {
        var layer = L(rotation: 20);
        var c = SelectionGeometry.Center(layer);
        double offset = SelectionGeometry.RotateStartOffset(layer, 320, 140);
        Assert.Equal(20, SelectionGeometry.RotateAngle(c, offset, 320, 140), 9);
    }

    [Theory] // select-rotate-drag parityCheck: the first snap angle strictly within 3 degrees, else unchanged
    [InlineData(44.2, 45)]
    [InlineData(47.9, 45)]
    [InlineData(48.1, 48.1)]
    [InlineData(42, 42)]
    [InlineData(-177.5, -180)]
    [InlineData(179, 180)]
    [InlineData(0.5, 0)]
    [InlineData(-91, -90)]
    public void The_snap_takes_the_first_angle_strictly_within_3_degrees(double degrees, double snapped) =>
        Assert.Equal(snapped, SelectionGeometry.SnapRotation(degrees));

    [Theory] // spec §5: rotation normalized to -180..180; the identity inside, so a slider value of 180 stays
    [InlineData(180, 180)]
    [InlineData(-180, -180)]
    [InlineData(37.5, 37.5)]
    [InlineData(181, -179)]
    [InlineData(-181, 179)]
    [InlineData(540, -180)]
    [InlineData(359.5, -0.5)]
    [InlineData(-450, -90)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void Rotation_is_normalized_to_minus_180_to_180(double degrees, double normalized) =>
        Assert.Equal(normalized, SelectionGeometry.NormalizeRotation(degrees), 9);

    [Theory] // select-rotate-drag: 4.1 stores 190 here (angle 20 + 170); v5 stays in range and keeps the snap
    [InlineData(0, 170)]
    [InlineData(10, 180)]
    [InlineData(12, -180)]
    [InlineData(20, -170)]
    [InlineData(100, -90)]
    public void A_drag_across_the_atan2_seam_stays_in_range(double pointerDegrees, double rotation)
    {
        var c = (X: 300.0, Y: 300.0);
        double rad = pointerDegrees * Math.PI / 180;
        double offset = 0 - 170; // pressed straight up on a layer rotated 170
        double got = SelectionGeometry.RotateAngle(c, offset, c.X + 50 * Math.Sin(rad), c.Y - 50 * Math.Cos(rad));
        Assert.Equal(rotation, got, 9);
        Assert.InRange(got, -180, 180);
    }

    [Theory] // select-resize-drag parityCheck: distance 100 to 150 gives 1.5x; one clamp; a zero start distance is no change
    [InlineData(1, 100, 150, 1.5)]
    [InlineData(0.5, 100, 200, 1)]
    [InlineData(0.02, 100, 10, 0.01)]
    [InlineData(4, 100, 200, 5)]
    [InlineData(1, 0, 50, 1)]
    [InlineData(1, 1e-10, 5, 1)]
    public void The_resize_scale_is_the_distance_ratio_under_the_clamp(double start, double startDistance, double distance, double scale) =>
        Assert.Equal(scale, SelectionGeometry.ResizeScale(start, startDistance, distance), 12);

    [Theory] // props-scale-clamp-inconsistency: one range, 1 to 500%
    [InlineData(0, 0.01)]
    [InlineData(-1, 0.01)]
    [InlineData(double.NaN, 0.01)]
    [InlineData(0.5, 0.5)]
    [InlineData(5, 5)]
    [InlineData(7, 5)]
    [InlineData(double.PositiveInfinity, 5)]
    public void The_scale_clamp_is_1_to_500_percent(double scale, double clamped) =>
        Assert.Equal(clamped, SelectionGeometry.ClampScale(scale));

    [Fact] // wheel-scale-layer parityCheck: 10 down then 10 up; 4.1 ends at 0.9794, v5 returns to the start
    public void Wheel_steps_are_symmetric_and_clamped()
    {
        double s = 1;
        for (int i = 0; i < 10; i++) s = SelectionGeometry.WheelScale(s, -1);
        for (int i = 0; i < 10; i++) s = SelectionGeometry.WheelScale(s, +1);
        Assert.Equal(1, s, 12);
        Assert.Equal(1.05, SelectionGeometry.WheelScale(1, 0), 12); // WPF Delta 0 counts as up, as 4.1's deltaY <= 0 grows
        Assert.Equal(5, SelectionGeometry.WheelScale(5, 1));
        Assert.Equal(0.01, SelectionGeometry.WheelScale(0.01, -1));
        Assert.Equal(5, SelectionGeometry.WheelScale(4.9, 1));
    }

    [Theory] // render-rotation-label parityCheck: toFixed(1) + U+00B0; no label at 0 (and -0)
    [InlineData(30, "30.0°")]
    [InlineData(17.25, "17.3°")]
    [InlineData(-17.25, "-17.3°")]
    [InlineData(-0.04, "-0.0°")]
    [InlineData(0.05, "0.1°")]
    [InlineData(1.45, "1.4°")]
    [InlineData(0.25, "0.3°")]
    [InlineData(-179.95, "-179.9°")]
    [InlineData(180, "180.0°")]
    [InlineData(-135, "-135.0°")]
    [InlineData(2.675, "2.7°")]
    [InlineData(-0.25, "-0.3°")]
    public void The_rotation_label_is_toFixed_1_with_a_degree_sign(double rotation, string label) =>
        Assert.Equal(label, SelectionGeometry.RotationLabel(rotation));

    [Fact] // the ledger's "toFixed vs F1 rounding on halves: verify": .NET's F1 rounds an exact tie to even, toFixed does not
    public void ToFixed1_is_javascript_not_dotnet_F1_on_exact_ties()
    {
        Assert.Equal("17.2", 17.25.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("17.3", SelectionGeometry.ToFixed1(17.25));
        Assert.Equal("0.0", SelectionGeometry.ToFixed1(1e-25));
        Assert.Equal("123456789.1", SelectionGeometry.ToFixed1(123456789.125)); // binary 123456789.125 exactly: a tie at 2 places, not at 1
        Assert.Equal(("NaN", "Infinity", "-Infinity"), (SelectionGeometry.ToFixed1(double.NaN), SelectionGeometry.ToFixed1(double.PositiveInfinity), SelectionGeometry.ToFixed1(double.NegativeInfinity)));
    }

    [Fact]
    public void There_is_no_label_at_zero()
    {
        Assert.Null(SelectionGeometry.RotationLabel(0));
        Assert.Null(SelectionGeometry.RotationLabel(-0.0));
        Assert.Equal(SKRectI.Empty, SelectionGeometry.LabelBounds(L(), new ViewOptions(1)));
    }

    [Fact] // ruling R4: the label box holds the anchor, rotates with the layer and scales with the device scale
    public void The_label_bounds_follow_the_anchor_rotation_and_device_scale()
    {
        var layer = L(rotation: 30); // center (250, 225); anchor (0, -25 - 46) in the rotated frame
        var box = SelectionGeometry.LabelBounds(layer, new ViewOptions(1));
        double rad = 30 * Math.PI / 180;
        double ax = 250 + 71 * Math.Sin(rad), ay = 225 - 71 * Math.Cos(rad);
        Assert.True(box.Left < ax && ax < box.Right && box.Top < ay && ay < box.Bottom, $"{box} must hold the anchor ({ax}, {ay})");
        var upsideDown = SelectionGeometry.LabelBounds(layer with { Rotation = 180 }, new ViewOptions(1));
        Assert.True(upsideDown.Top > 225, "at 180 degrees the label sits below the layer");
        var doubled = SelectionGeometry.LabelBounds(layer, new ViewOptions(1, 2));
        Assert.InRange(doubled.Left, 2 * box.Left - 1, 2 * box.Left + 1);
        Assert.InRange(doubled.Bottom, 2 * box.Bottom - 1, 2 * box.Bottom + 1);
        Assert.InRange(doubled.Width, 2 * box.Width - 2, 2 * box.Width + 2);
        var zoomed = SelectionGeometry.LabelBounds(layer, new ViewOptions(2)); // zoom moves the anchor, not the label's screen size
        Assert.InRange(zoomed.Width, box.Width - 1, box.Width + 1);
        Assert.InRange(zoomed.Height, box.Height - 1, box.Height + 1);
    }
}
```

Create `src/RBX15.Engine.Tests/LayerOpsEditTests.cs`:

```csharp
using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M3b layer edits: the canvas drags (editor.js:1169-1190), the properties panel (editor.js:825-911), the layer list
/// drop (editor.js:621-657) and the drawing commit (editor.js:528-540). Every op returns its input instance when it
/// changes nothing, so the session records no step (undo-empty-steps). LayerOpsTests.cs (M3a) is untouched.
/// </summary>
public class LayerOpsEditTests
{
    static Layer L(int id, double x = 200, double y = 200, double scale = 1, int w = 100, int h = 50) =>
        new() { Id = id, Name = "L" + id, Image = Px.Solid(w, h, SKColors.Red), X = x, Y = y, Scale = scale };

    static Document Doc(params Layer[] layers) => new() { Layers = ImmutableList.Create(layers) };

    static Layer Only(Document d) => Assert.Single(d.Layers);

    [Fact] // select-move-hit-and-drag, props-name-xy: the layer lands at (x, y); nothing else moves
    public void MoveTo_places_the_layer()
    {
        var doc = Doc(L(1));
        var moved = Only(LayerOps.MoveTo(doc, 1, 215.5, -40));
        Assert.Equal((215.5, -40.0, 1.0), (moved.X, moved.Y, moved.Scale));
        Assert.Same(doc, LayerOps.MoveTo(doc, 1, 200, 200));
        Assert.Same(doc, LayerOps.MoveTo(doc, 1, double.NaN, 0));
        Assert.Same(doc, LayerOps.MoveTo(doc, 1, 0, double.PositiveInfinity));
    }

    [Fact] // props-scale (editor.js:828-832): the slider keeps the top-left; props-scale-clamp-inconsistency: 1 to 500%
    public void SetScale_keeps_the_top_left_and_clamps()
    {
        var doc = Doc(L(1));
        var s = Only(LayerOps.SetScale(doc, 1, 2.5));
        Assert.Equal((200.0, 200.0, 2.5), (s.X, s.Y, s.Scale));
        Assert.Equal(5, Only(LayerOps.SetScale(doc, 1, 7)).Scale);
        Assert.Equal(0.01, Only(LayerOps.SetScale(doc, 1, 0)).Scale);
        Assert.Equal(0.01, Only(LayerOps.SetScale(doc, 1, double.NaN)).Scale);
        Assert.Same(doc, LayerOps.SetScale(doc, 1, 1));
    }

    [Theory] // select-resize-handles parityCheck: center invariant to 1e-9; wheel-scale-layer fix: centered
    [InlineData(0.5)]
    [InlineData(2)]
    [InlineData(3.3)]
    [InlineData(9)]
    public void ScaleAboutCenter_keeps_the_center(double scale)
    {
        var doc = Doc(L(1, x: 13.7, y: -4.25, scale: 0.8));
        var before = SelectionGeometry.Center(doc.Layers[0]);
        var after = Only(LayerOps.ScaleAboutCenter(doc, 1, scale));
        Assert.Equal(SelectionGeometry.ClampScale(scale), after.Scale);
        var c = SelectionGeometry.Center(after);
        Assert.Equal(before.X, c.X, 9);
        Assert.Equal(before.Y, c.Y, 9);
    }

    [Fact] // a clamped value equal to the current scale changes nothing, so no rounding-only step is recorded
    public void ScaleAboutCenter_at_the_limit_returns_the_same_document()
    {
        var doc = Doc(L(1, scale: 5));
        Assert.Same(doc, LayerOps.ScaleAboutCenter(doc, 1, 7));
        Assert.Same(doc, LayerOps.ScaleAboutCenter(doc, 1, 5));
        var tiny = Doc(L(1, scale: 0.01));
        Assert.Same(tiny, LayerOps.ScaleAboutCenter(tiny, 1, SelectionGeometry.WheelScale(0.01, -1)));
    }

    [Fact] // props-rotation, select-rotate-drag: normalized to -180..180
    public void SetRotation_normalizes()
    {
        var doc = Doc(L(1));
        Assert.Equal(-160, Only(LayerOps.SetRotation(doc, 1, 200)).Rotation, 9);
        Assert.Equal(180, Only(LayerOps.SetRotation(doc, 1, 180)).Rotation);
        Assert.Same(doc, LayerOps.SetRotation(doc, 1, 0));
        Assert.Same(doc, LayerOps.SetRotation(doc, 1, 360));
    }

    [Fact] // props-flip (editor.js:843-844): each button toggles its own axis
    public void Flip_toggles_one_axis()
    {
        var doc = Doc(L(1));
        var h = LayerOps.Flip(doc, 1, horizontal: true);
        Assert.Equal((true, false), (Only(h).FlipH, Only(h).FlipV));
        var hv = LayerOps.Flip(h, 1, horizontal: false);
        Assert.Equal((true, true), (Only(hv).FlipH, Only(hv).FlipV));
        Assert.True(doc.ContentEquals(LayerOps.Flip(LayerOps.Flip(doc, 1, true), 1, true)));
    }

    [Fact] // props-opacity (editor.js:838-842): 0..1
    public void SetOpacity_clamps()
    {
        var doc = Doc(L(1));
        Assert.Equal(0.4, Only(LayerOps.SetOpacity(doc, 1, 0.4)).Opacity);
        Assert.Equal(1, Only(LayerOps.SetOpacity(LayerOps.SetOpacity(doc, 1, 0.4), 1, 1.5)).Opacity);
        Assert.Equal(0, Only(LayerOps.SetOpacity(doc, 1, -0.2)).Opacity);
        Assert.Same(doc, LayerOps.SetOpacity(doc, 1, 1));
        Assert.Same(doc, LayerOps.SetOpacity(doc, 1, double.NaN));
    }

    [Fact] // props-name-xy (editor.js:825): any text, empty allowed
    public void SetName_takes_any_text()
    {
        var doc = Doc(L(1));
        Assert.Equal("drip <b>", Only(LayerOps.SetName(doc, 1, "drip <b>")).Name);
        Assert.Equal("", Only(LayerOps.SetName(doc, 1, "")).Name);
        Assert.Equal("", Only(LayerOps.SetName(doc, 1, null)).Name);
        Assert.Same(doc, LayerOps.SetName(doc, 1, "L1"));
    }

    [Fact] // props-adjust-sliders: B/C/S 0..200, hue 0..360, RGB -100..100; a non-finite field keeps its value
    public void SetAdjustments_clamps_to_the_slider_ranges()
    {
        var doc = Doc(L(1));
        var a = Only(LayerOps.SetAdjustments(doc, 1, new LayerAdjustments(250, -5, 0, 400, 150, -150, 7)));
        Assert.Equal(new LayerAdjustments(200, 0, 0, 360, 100, -100, 7), LayerAdjustments.From(a));
        var keep = Only(LayerOps.SetAdjustments(doc, 1, new LayerAdjustments(double.NaN, 50, double.PositiveInfinity, 90, 0, 0, 0)));
        Assert.Equal(new LayerAdjustments(100, 50, 100, 90, 0, 0, 0), LayerAdjustments.From(keep));
        Assert.Same(doc, LayerOps.SetAdjustments(doc, 1, LayerAdjustments.Default));
        Assert.Throws<ArgumentNullException>(() => LayerOps.SetAdjustments(doc, 1, null!));
    }

    [Fact] // props-adjust-reset (editor.js:908-911); layers-model defaults (editor.js:134-142)
    public void ResetAdjustments_restores_the_defaults()
    {
        Assert.Equal(new LayerAdjustments(100, 100, 100, 0, 0, 0, 0), LayerAdjustments.Default);
        var doc = Doc(L(1) with { Brightness = 0, Contrast = 0, Saturate = 0, HueRotate = 90, RShift = 5, GShift = -5, BShift = 1 });
        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(Only(LayerOps.ResetAdjustments(doc, 1))));
        var clean = Doc(L(1));
        Assert.Same(clean, LayerOps.ResetAdjustments(clean, 1));
    }

    [Fact] // Reorder: Layers order, 0 = bottom; no-op and out-of-range indices change nothing
    public void Reorder_moves_one_layer()
    {
        var doc = Doc(L(1), L(2), L(3));
        Assert.Equal(new[] { 2, 3, 1 }, LayerOps.Reorder(doc, 0, 2).Layers.Select(l => l.Id));
        Assert.Equal(new[] { 3, 1, 2 }, LayerOps.Reorder(doc, 2, 0).Layers.Select(l => l.Id));
        Assert.Same(doc, LayerOps.Reorder(doc, 1, 1));
        Assert.Same(doc, LayerOps.Reorder(doc, -1, 1));
        Assert.Same(doc, LayerOps.Reorder(doc, 0, 3));
    }

    /// <summary>handleLayerDrop (editor.js:621-657) as 4.1 runs it: splice the dragged layer out, find the target again, splice it back.</summary>
    static int[] Splice41(int[] ids, int dragId, int targetId, bool above)
    {
        var layers = ids.ToList();
        int dragIdx = layers.IndexOf(dragId);
        layers.RemoveAt(dragIdx);
        int insertIdx = layers.IndexOf(targetId);
        layers.Insert(above ? insertIdx + 1 : insertIdx, dragId);
        return layers.ToArray();
    }

    [Fact] // layers-reorder-drag: every drag, target and half of a 4-layer list lands where 4.1's splice puts it
    public void DropIndices_plus_Reorder_is_4_1_s_splice()
    {
        int[] ids = { 1, 2, 3, 4 };
        var doc = Doc(ids.Select(i => L(i)).ToArray());
        foreach (int drag in ids)
            foreach (int target in ids.Where(t => t != drag))
                foreach (bool above in new[] { true, false })
                {
                    var (from, to) = LayerOps.DropIndices(doc, drag, target, above)!.Value;
                    var got = LayerOps.Reorder(doc, from, to).Layers.Select(l => l.Id).ToArray();
                    Assert.Equal(Splice41(ids, drag, target, above), got);
                    if (got.SequenceEqual(ids)) Assert.Equal(from, to); // an order-keeping drop records nothing (undo-empty-steps)
                }
    }

    [Fact] // editor.js:625: a drop on itself, or a missing id, does nothing
    public void DropIndices_refuses_a_self_drop_or_a_missing_id()
    {
        var doc = Doc(L(1), L(2));
        Assert.Null(LayerOps.DropIndices(doc, 1, 1, true));
        Assert.Null(LayerOps.DropIndices(doc, 9, 1, true));
        Assert.Null(LayerOps.DropIndices(doc, 1, 9, false));
    }

    [Fact] // draw-commit-layer, layers-add-drawing (editor.js:534-537): pushed on top and selected
    public void AddDrawing_pushes_on_top_and_selects()
    {
        var s = new EditorSnapshot(Doc(L(1), L(2)), 1);
        var drawing = L(7) with { Name = "Rect drawing" };
        var next = LayerOps.AddDrawing(s, drawing);
        Assert.Equal(new[] { 1, 2, 7 }, next.Document.Layers.Select(l => l.Id));
        Assert.Same(drawing, next.Document.Layers[^1]);
        Assert.Equal(7, next.SelectedLayerId);
        Assert.Throws<ArgumentException>(() => LayerOps.AddDrawing(s, L(2)));
        Assert.Throws<ArgumentNullException>(() => LayerOps.AddDrawing(s, null!));
    }

    [Fact] // a missing id changes nothing, for every edit
    public void Every_edit_of_a_missing_layer_returns_the_same_document()
    {
        var doc = Doc(L(1));
        Assert.Same(doc, LayerOps.MoveTo(doc, 9, 1, 1));
        Assert.Same(doc, LayerOps.SetScale(doc, 9, 2));
        Assert.Same(doc, LayerOps.ScaleAboutCenter(doc, 9, 2));
        Assert.Same(doc, LayerOps.SetRotation(doc, 9, 10));
        Assert.Same(doc, LayerOps.Flip(doc, 9, true));
        Assert.Same(doc, LayerOps.SetOpacity(doc, 9, 0.5));
        Assert.Same(doc, LayerOps.SetName(doc, 9, "x"));
        Assert.Same(doc, LayerOps.SetAdjustments(doc, 9, new LayerAdjustments(1, 1, 1, 1, 1, 1, 1)));
        Assert.Same(doc, LayerOps.ResetAdjustments(doc, 9));
    }

    [Fact] // carry-forward: Document.ContentEquals compares Layer by record equality, so M3b adds no Layer state
    public void Layer_gains_no_field()
    {
        var names = typeof(Layer).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(new[]
        {
            "BShift", "Brightness", "Contrast", "FlipH", "FlipV", "GShift", "HueRotate", "Id", "Image", "Name", "Opacity",
            "RShift", "Rotation", "Saturate", "Scale", "Visible", "X", "Y",
        }, names);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'ResizeCorner' could not be found` in `SelectionGeometryTests.cs`, and `error CS0103: The name 'ResizeCorner' does not exist in the current context`.

- [ ] **Step 5: Implement SelectionGeometry and the layer edits**

Create `src/RBX15.Engine/SelectionGeometry.cs`:

```csharp
using System.Globalization;
using System.Numerics;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>The four corner handles in 4.1's order (editor.js:1083: TL, TR, BR, BL).</summary>
public enum ResizeCorner { TopLeft, TopRight, BottomRight, BottomLeft }

/// <summary>What a Select-tool press hit (editor.js:1064-1117).</summary>
public enum SelectHitKind { None, Rotate, Resize, Move }

/// <summary>A press result: the kind, the layer it acts on (null for None) and, for Resize, the corner.</summary>
public readonly record struct SelectHit(SelectHitKind Kind, int? LayerId, ResizeCorner Corner);

/// <summary>
/// The Select tool's geometry (editor.js:1018-1192) as pure functions over doubles in template px, operation for operation
/// where 4.1 is kept. "Screen px" constants are divided by the zoom, as 4.1 does (editor.js:1034, :1069, :1088), so the
/// handles keep their on-screen size at every zoom. The overlay that draws them is <c>Renderer.DrawSelectionOverlay</c>.
/// <para>
/// Fixes, each a ledger divergence: hidden layers have no handles (M3b ruling R2, select-hidden-layer-still-handled,
/// select-rotate-handle-hit); rotation is normalized to -180..180 before the snap (R5, select-rotate-drag); one scale
/// clamp of 1 to 500% (R6, props-scale-clamp-inconsistency); the wheel steps symmetrically about the center (R6,
/// wheel-scale-layer); and the rotate handle is hit where it is drawn (ruling T1-1 below <see cref="RotateHandle"/>).
/// </para>
/// </summary>
public static class SelectionGeometry
{
    /// <summary>The rotate knob's distance beyond the top edge, in screen px (editor.js:387 draws it, :1034 hit-tests it).</summary>
    public const double RotateHandleDistance = 30;

    /// <summary>The rotate knob's hit radius in screen px, strict &lt; (editor.js:1069).</summary>
    public const double RotateHitRadius = 14;

    /// <summary>A corner handle's hit radius in screen px, strict &lt; (editor.js:1088).</summary>
    public const double ResizeHitRadius = 10;

    /// <summary>The one scale clamp, 1 to 500% (spec §5; ledger props-scale-clamp-inconsistency).</summary>
    public const double MinScale = 0.01, MaxScale = 5;

    /// <summary>The wheel's step: x1.05 up, /1.05 down (spec §5 "symmetric steps"; 4.1's up step, editor.js:1310).</summary>
    public const double WheelStep = 1.05;

    /// <summary>The rotate snap tolerance in degrees, strict &lt; (editor.js:1178).</summary>
    public const double SnapTolerance = 3;

    /// <summary>The snap angles in 4.1's order; the first within <see cref="SnapTolerance"/> wins (editor.js:1176-1179).</summary>
    public static readonly IReadOnlyList<double> SnapAngles = new double[] { -180, -135, -90, -45, 0, 45, 90, 135, 180 };

    /// <summary>getLayerCenter (editor.js:1023-1027): x + img.width*scale/2, y + img.height*scale/2.</summary>
    public static (double X, double Y) Center(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        double w = layer.Image.Width * layer.Scale, h = layer.Image.Height * layer.Scale;
        return (layer.X + w / 2, layer.Y + h / 2);
    }

    /// <summary>
    /// The rotate knob's center in template px: <see cref="RotateHandleDistance"/> screen px above the top edge in the
    /// layer's rotated frame, exactly where <c>Renderer.DrawSelectionOverlay</c> draws it (editor.js:387-401:
    /// <c>rotate(r)</c> maps the local point (0, -d) to (d sin r, -d cos r)).
    /// <para>
    /// Ruling T1-1 (fix): 4.1's getRotHandlePos (editor.js:1029-1039) returns <c>c.x - sin(r)*d</c>, the knob mirrored
    /// across the layer's vertical axis. The two agree only at 0 and 180 degrees, so in 4.1 a rotated layer's visible knob
    /// does not rotate (the press falls through to a corner or a move) and an empty spot on the other side does. v5
    /// hit-tests the drawn knob: <c>c.x + sin(r)*d</c>. Pinned by
    /// <c>SelectionGeometryTests.The_rotate_handle_turns_clockwise_with_the_layer</c> and
    /// <c>RendererOverlayTests.The_rotate_handle_is_hit_where_the_overlay_draws_it</c>; Task 5 records the divergence.
    /// </para>
    /// </summary>
    public static (double X, double Y) RotateHandle(Layer layer, double zoom)
    {
        ArgumentNullException.ThrowIfNull(layer);
        double h = layer.Image.Height * layer.Scale;
        var c = Center(layer);
        double rad = layer.Rotation * Math.PI / 180;
        double dist = h / 2 + RotateHandleDistance / zoom;
        return (c.X + Math.Sin(rad) * dist, c.Y - Math.Cos(rad) * dist);
    }

    /// <summary>A corner of the rotated box in template px (editor.js:1083-1088): center + R(rotation) * (±w/2, ±h/2).</summary>
    public static (double X, double Y) Corner(Layer layer, ResizeCorner corner)
    {
        ArgumentNullException.ThrowIfNull(layer);
        double w = layer.Image.Width * layer.Scale, h = layer.Image.Height * layer.Scale;
        var c = Center(layer);
        double rad = layer.Rotation * Math.PI / 180;
        var (lx, ly) = corner switch
        {
            ResizeCorner.TopLeft => (-w / 2, -h / 2),
            ResizeCorner.TopRight => (w / 2, -h / 2),
            ResizeCorner.BottomRight => (w / 2, h / 2),
            ResizeCorner.BottomLeft => (-w / 2, h / 2),
            _ => throw new ArgumentOutOfRangeException(nameof(corner)),
        };
        return (c.X + lx * Math.Cos(rad) - ly * Math.Sin(rad), c.Y + lx * Math.Sin(rad) + ly * Math.Cos(rad));
    }

    /// <summary>
    /// A Select-tool press at template point (x, y), in 4.1's order (editor.js:1064-1117):
    /// <list type="number">
    /// <item>the selected layer's rotate knob, distance &lt; 14/zoom (Rotate);</item>
    /// <item>its corners TL, TR, BR, BL, distance &lt; 10/zoom (Resize);</item>
    /// <item><see cref="LayerOps.HitTest"/>: the topmost visible layer whose rotated box holds the point (Move);</item>
    /// <item>else None, which deselects.</item>
    /// </list>
    /// A hidden selected layer has no handles (ruling R2), so a press on its knob or corners falls through to the pick.
    /// </summary>
    public static SelectHit HitTest(Document doc, int? selectedId, double x, double y, double zoom)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (selectedId is int id && doc.Layers.Find(l => l.Id == id) is { Visible: true } sel)
        {
            if (Distance(RotateHandle(sel, zoom), x, y) < RotateHitRadius / zoom)
                return new SelectHit(SelectHitKind.Rotate, sel.Id, default);
            foreach (var corner in new[] { ResizeCorner.TopLeft, ResizeCorner.TopRight, ResizeCorner.BottomRight, ResizeCorner.BottomLeft })
                if (Distance(Corner(sel, corner), x, y) < ResizeHitRadius / zoom)
                    return new SelectHit(SelectHitKind.Resize, sel.Id, corner);
        }
        return LayerOps.HitTest(doc, x, y) is int hit
            ? new SelectHit(SelectHitKind.Move, hit, default)
            : new SelectHit(SelectHitKind.None, null, default);
    }

    /// <summary>ptDist (editor.js:1041).</summary>
    static double Distance((double X, double Y) p, double x, double y) => Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));

    /// <summary>The pointer's angle about <paramref name="center"/>: atan2(dx, -dy) in degrees, 0 up, clockwise positive (editor.js:1073, :1174).</summary>
    public static double PointerAngle((double X, double Y) center, double x, double y) =>
        Math.Atan2(x - center.X, -(y - center.Y)) * 180 / Math.PI;

    /// <summary>rotStartAngle at the press (editor.js:1073): the pointer angle minus the layer's rotation.</summary>
    public static double RotateStartOffset(Layer layer, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return PointerAngle(Center(layer), x, y) - layer.Rotation;
    }

    /// <summary>
    /// The rotation for a rotate-drag move (ruling R5): <c>raw = PointerAngle - startOffset</c> (editor.js:1174-1175),
    /// normalized into [-180, 180], then 4.1's snap (editor.js:1176-1179). The result is in [-180, 180].
    /// </summary>
    public static double RotateAngle((double X, double Y) center, double startOffset, double x, double y) =>
        SnapRotation(NormalizeRotation(PointerAngle(center, x, y) - startOffset));

    /// <summary>
    /// Rotation normalized to -180..180 (spec §5; ledger select-rotate-drag). The identity inside [-180, 180], so a slider
    /// value of 180 stays 180; anything else wraps into [-180, 180). A value that is not finite has no angle and gives 0.
    /// </summary>
    public static double NormalizeRotation(double degrees)
    {
        if (!double.IsFinite(degrees)) return 0;
        if (degrees >= -180 && degrees <= 180) return degrees;
        double r = (degrees + 180) % 360;
        if (r < 0) r += 360;
        return r - 180;
    }

    /// <summary>4.1's snap (editor.js:1176-1179): the first of <see cref="SnapAngles"/> strictly within 3 degrees, else unchanged.</summary>
    public static double SnapRotation(double degrees)
    {
        foreach (var s in SnapAngles)
            if (Math.Abs(degrees - s) < SnapTolerance) return s;
        return degrees;
    }

    /// <summary>
    /// The corner-drag scale (editor.js:1183-1185): <c>startScale * (distance / startDistance)</c> under
    /// <see cref="ClampScale"/>. A start distance under 1e-9 (a press at the center of a vanishing layer) keeps
    /// <paramref name="startScale"/>, where 4.1 divided by zero (ledger select-resize-handles).
    /// </summary>
    public static double ResizeScale(double startScale, double startDistance, double distance)
    {
        if (!(startDistance >= 1e-9)) return startScale;
        return ClampScale(startScale * (distance / startDistance));
    }

    /// <summary>The one scale clamp: [0.01, 5] (1 to 500%). NaN clamps to 0.01 (ruling R6).</summary>
    public static double ClampScale(double scale) => double.IsNaN(scale) ? MinScale : Math.Clamp(scale, MinScale, MaxScale);

    /// <summary>The wheel's scale (ruling R6, wheel-scale-layer fix): x1.05 when <paramref name="sign"/> &gt;= 0, /1.05 otherwise, clamped.</summary>
    public static double WheelScale(double scale, int sign) => ClampScale(sign >= 0 ? scale * WheelStep : scale / WheelStep);

    /// <summary>
    /// The canvas label (editor.js:416-420): null when <paramref name="rotation"/> == 0 (as <c>rotation !== 0</c>; -0 is 0),
    /// else <c>rotation.toFixed(1)</c> plus U+00B0: 17.25 gives "17.3°", -17.25 gives "-17.3°", 1.45 (binary
    /// 1.4499...) gives "1.4°", and -0.04 gives "-0.0°" (<see cref="ToFixed1"/>).
    /// </summary>
    public static string? RotationLabel(double rotation) =>
        rotation == 0 ? null : ToFixed1(rotation) + "°";

    /// <summary>
    /// JavaScript's <c>Number.prototype.toFixed(1)</c> (ECMA-262): the exact binary value rounded to one decimal, an
    /// exact tie taking the larger magnitude, and a negative value keeping its sign when it rounds to 0 ("-0.0").
    /// .NET's "F1" rounds an exact tie to even (17.25 gives "17.2", measured), so the digits come from the exact
    /// "F20" expansion: a double of magnitude 1e-21 or more is never within 5e-21 of a tie it is not equal to, so the
    /// 20th place cannot fake one. NaN and the infinities print as JavaScript prints them; at 1e21 and above toFixed
    /// falls back to ToString, which "R" matches for the label's purpose.
    /// </summary>
    internal static string ToFixed1(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        double a = Math.Abs(value);
        if (a >= 1e21) return value.ToString("R", CultureInfo.InvariantCulture);
        string exact = a.ToString("F20", CultureInfo.InvariantCulture);
        int dot = exact.IndexOf('.');
        var tenths = BigInteger.Parse(exact.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture) * 10 + (exact[dot + 1] - '0');
        if (exact[dot + 2] >= '5') tenths += 1;
        string text = (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (char)('0' + (int)(tenths % 10));
        return value < 0 ? "-" + text : text;
    }

    /// <summary>Half the label exclusion box's width, in CSS px (ruling R4).</summary>
    const double LabelHalfWidth = 30;

    /// <summary>The label exclusion box's extent above and below the alphabetic baseline, in CSS px (ruling R4).</summary>
    const double LabelAbove = 12, LabelBelow = 4;

    /// <summary>
    /// Ruling R4: the device-px axis-aligned box that holds the rotated label, so the view-select golden gate can leave
    /// the label out of the pixel comparison. The label box is local (-30, b - 12) to (30, b + 4) with the baseline
    /// b = -h/2 - 46 (editor.js:420), in CSS px of the zoomed canvas, rotated with the layer about its center and scaled
    /// by <c>DeviceScale</c>, then rounded outward. <see cref="SKRectI.Empty"/> when <see cref="RotationLabel"/> is null.
    /// </summary>
    public static SKRectI LabelBounds(Layer layer, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(options);
        if (RotationLabel(layer.Rotation) is null) return SKRectI.Empty;
        double zoom = options.Zoom, ds = options.DeviceScale;
        double w = layer.Image.Width * layer.Scale * zoom, h = layer.Image.Height * layer.Scale * zoom;
        double cx = layer.X * zoom + w / 2, cy = layer.Y * zoom + h / 2;
        double rad = layer.Rotation * Math.PI / 180, cos = Math.Cos(rad), sin = Math.Sin(rad);
        double baseline = -h / 2 - RotateHandleDistance - 16;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (lx, ly) in new[] { (-LabelHalfWidth, baseline - LabelAbove), (LabelHalfWidth, baseline - LabelAbove), (LabelHalfWidth, baseline + LabelBelow), (-LabelHalfWidth, baseline + LabelBelow) })
        {
            double x = (cx + lx * cos - ly * sin) * ds, y = (cy + lx * sin + ly * cos) * ds;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        return new SKRectI((int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY));
    }
}
```

In `src/RBX15.Engine/LayerOps.cs`, make these two replacements. The first inserts the edits before the private `Swap` helper; the second appends `LayerAdjustments` after the class:

Edit 1 of 2. Replace:

```csharp
    }

    static Document Swap(Document doc, int a, int b)
    {
```

with:

```csharp
    }

    /// <summary>
    /// The move drag (editor.js:1169-1171) and the X and Y fields (editor.js:826-827): the layer at (x, y). A value that is
    /// not finite (an empty or garbled field) changes nothing.
    /// </summary>
    public static Document MoveTo(Document doc, int id, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (!double.IsFinite(x) || !double.IsFinite(y)) return doc;
        return Edit(doc, id, l => l with { X = x, Y = y });
    }

    /// <summary>
    /// The Scale slider (editor.js:828-832): <c>scale = value / 100</c> anchored at the top-left, as 4.1 (x and y kept),
    /// under the one clamp (<see cref="SelectionGeometry.ClampScale"/>; props-scale is a clamp-and-undo fix, ruling R6).
    /// </summary>
    public static Document SetScale(Document doc, int id, double scale)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => l with { Scale = SelectionGeometry.ClampScale(scale) });
    }

    /// <summary>
    /// The wheel and the corner handles: the clamped scale with the layer's center kept (editor.js:1185-1190, which 4.1's
    /// resize does and its wheel does not; spec §5 "centered"). An unchanged scale returns the input instance, so the
    /// recomputed x and y can never add a rounding-only step.
    /// </summary>
    public static Document ScaleAboutCenter(Document doc, int id, double scale)
    {
        ArgumentNullException.ThrowIfNull(doc);
        double s = SelectionGeometry.ClampScale(scale);
        return Edit(doc, id, l =>
        {
            if (s == l.Scale) return l;
            var c = SelectionGeometry.Center(l);
            double w = l.Image.Width * s, h = l.Image.Height * s;
            return l with { Scale = s, X = c.X - w / 2, Y = c.Y - h / 2 };
        });
    }

    /// <summary>The Rotation slider (editor.js:833-837) and the rotate handle: <see cref="SelectionGeometry.NormalizeRotation"/>.</summary>
    public static Document SetRotation(Document doc, int id, double degrees)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => l with { Rotation = SelectionGeometry.NormalizeRotation(degrees) });
    }

    /// <summary>Flip H and Flip V (editor.js:843-844): toggles <c>FlipH</c> when <paramref name="horizontal"/>, else <c>FlipV</c>.</summary>
    public static Document Flip(Document doc, int id, bool horizontal)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => horizontal ? l with { FlipH = !l.FlipH } : l with { FlipV = !l.FlipV });
    }

    /// <summary>The Opacity slider (editor.js:838-842): clamped to 0..1, the slider's 0..100 over 100. Not finite: no change.</summary>
    public static Document SetOpacity(Document doc, int id, double opacity)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (!double.IsFinite(opacity)) return doc;
        return Edit(doc, id, l => l with { Opacity = Math.Clamp(opacity, 0, 1) });
    }

    /// <summary>The Name field (editor.js:825): any text, empty allowed as in 4.1; null is stored as "".</summary>
    public static Document SetName(Document doc, int id, string? name)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return Edit(doc, id, l => l with { Name = name ?? "" });
    }

    /// <summary>
    /// The Image adjustments sliders (editor.js:873-907), each clamped to its slider's range (<see cref="LayerAdjustments"/>).
    /// A field that is not finite keeps the layer's current value.
    /// </summary>
    public static Document SetAdjustments(Document doc, int id, LayerAdjustments adjustments)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(adjustments);
        return Edit(doc, id, l => l with
        {
            Brightness = Range(adjustments.Brightness, 0, 200, l.Brightness),
            Contrast = Range(adjustments.Contrast, 0, 200, l.Contrast),
            Saturate = Range(adjustments.Saturate, 0, 200, l.Saturate),
            HueRotate = Range(adjustments.HueRotate, 0, 360, l.HueRotate),
            RShift = Math.Clamp(adjustments.RShift, -100, 100),
            GShift = Math.Clamp(adjustments.GShift, -100, 100),
            BShift = Math.Clamp(adjustments.BShift, -100, 100),
        });
    }

    /// <summary>Reset adjustments (editor.js:908-911): <see cref="LayerAdjustments.Default"/>.</summary>
    public static Document ResetAdjustments(Document doc, int id) => SetAdjustments(doc, id, LayerAdjustments.Default);

    /// <summary>
    /// Removes the layer at <paramref name="fromIndex"/> and inserts it so it ends at <paramref name="toIndex"/>
    /// (<see cref="Document.Layers"/> order, 0 = bottom). The same instance when the indices are equal or either is out of range.
    /// </summary>
    public static Document Reorder(Document doc, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int n = doc.Layers.Count;
        if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= n || toIndex < 0 || toIndex >= n) return doc;
        var moved = doc.Layers[fromIndex];
        return doc with { Layers = doc.Layers.RemoveAt(fromIndex).Insert(toIndex, moved) };
    }

    /// <summary>
    /// handleLayerDrop's index math (editor.js:621-657): the (from, to) pair for <see cref="Reorder"/> when
    /// <paramref name="dragId"/> is dropped on <paramref name="targetId"/>. <paramref name="above"/> is the row's top half
    /// ('top', editor.js:612), which puts the layer after the target in the array (above it in z-order). Null when either
    /// id is missing or the two are the same layer. A drop that keeps the order gives from == to, which records nothing.
    /// </summary>
    public static (int From, int To)? DropIndices(Document doc, int dragId, int targetId, bool above)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (dragId == targetId) return null;
        int from = doc.Layers.FindIndex(l => l.Id == dragId), over = doc.Layers.FindIndex(l => l.Id == targetId);
        if (from < 0 || over < 0) return null;
        int insert = over > from ? over - 1 : over; // layers.findIndex after the splice (editor.js:645)
        return (from, above ? insert + 1 : insert);
    }

    /// <summary>
    /// addDrawingLayer's tail (editor.js:534-537): the committed drawing pushed on top and selected. The id must be fresh.
    /// </summary>
    public static EditorSnapshot AddDrawing(EditorSnapshot s, Layer layer)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(layer);
        RequireFreshId(s.Document, layer.Id);
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Add(layer) }, layer.Id);
    }

    /// <summary>Replaces layer <paramref name="id"/> with <paramref name="edit"/>'s result; the input instance when the id is missing or the layer is unchanged.</summary>
    static Document Edit(Document doc, int id, Func<Layer, Layer> edit)
    {
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        var before = doc.Layers[i];
        var after = edit(before);
        return after.Equals(before) ? doc : doc with { Layers = doc.Layers.SetItem(i, after) };
    }

    static double Range(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    static Document Swap(Document doc, int a, int b)
    {
```

Edit 2 of 2. Replace:

```csharp
    }
}
```

with:

```csharp
    }
}

/// <summary>
/// A layer's seven image adjustments, as the properties panel edits them (editor.js:785-797). The slider ranges are
/// Brightness, Contrast and Saturate 0..200, HueRotate 0..360, and each RGB shift -100..100;
/// <see cref="LayerOps.SetAdjustments"/> clamps to them.
/// </summary>
public sealed record LayerAdjustments(double Brightness, double Contrast, double Saturate, double HueRotate, int RShift, int GShift, int BShift)
{
    /// <summary>Reset adjustments' values (editor.js:909-910) and makeLayer's defaults (editor.js:134-142).</summary>
    public static readonly LayerAdjustments Default = new(100, 100, 100, 0, 0, 0, 0);

    /// <summary>The layer's current adjustments.</summary>
    public static LayerAdjustments From(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return new LayerAdjustments(layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate, layer.RShift, layer.GShift, layer.BShift);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.SelectionGeometryTests|FullyQualifiedName~RBX15.Engine.Tests.LayerOpsEditTests"`
Expected: `Passed!  - Failed:     0, Passed:    93, Skipped:     0, Total:    93` (SelectionGeometryTests 74, LayerOpsEditTests 19).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 841, Failed: 0, Skipped: 0` (748 + 93), Parity.Tests `35`, App.Tests `411`. `LayerOpsTests` (30, M3a) is unchanged.

Commit. End the message with your own model's `Co-Authored-By` trailer.

```bash
git add src/RBX15.Engine/SelectionGeometry.cs src/RBX15.Engine/LayerOps.cs src/RBX15.Engine.Tests/SelectionGeometryTests.cs src/RBX15.Engine.Tests/LayerOpsEditTests.cs
git commit -m "feat(engine): SelectionGeometry and the M3b layer edits, one scale clamp and normalized rotation"
```

- [ ] **Step 6: Write the failing overlay, preview and renderer tests**

Create `src/RBX15.Engine.Tests/RendererOverlayTests.cs`:

```csharp
using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The view's selection overlay (editor.js:361-425) and live preview (editor.js:324-330) as unit facts: what the
/// view-select goldens cannot show (the device scale, hidden layers, the label's face and bounds, the preview's place in
/// the composite, the preview being the commit). Ledger rows: render-selection-box, render-resize-handles,
/// render-rotation-handle, render-rotation-label, select-handle-render, select-hidden-layer-still-handled,
/// render-live-preview, draw-preview-composite.
/// </summary>
public class RendererOverlayTests
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa);
    static readonly SKColor Magenta = new(0xf2, 0x2f, 0x89);

    /// <summary>A 100x50 layer at (200, 200): center (250, 225), knob at (250, 170) at zoom 1.</summary>
    static Document OneLayer(bool visible = true, double rotation = 0) => new()
    {
        BgColor = "#000000",
        ShowMask = false,
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(100, 50, SKColors.Red), X = 200, Y = 200, Visible = visible, Rotation = rotation }),
    };

    static SKBitmap Frame(Document doc, ViewOptions options)
    {
        using var image = Renderer.RenderViewImage(doc, options);
        return ImageDiff.FromImage(image);
    }

    [Fact] // ViewOptions additions are init-only, null by default, and keep M3a's guards on with
    public void View_options_default_to_no_overlay_and_no_preview()
    {
        var o = new ViewOptions(1);
        Assert.Null(o.SelectedLayerId);
        Assert.Null(o.Preview);
        var p = LivePreview.Erase(new[] { new SKPoint(1, 1), new SKPoint(9, 9) }, 4);
        var s = o with { SelectedLayerId = 3, Preview = p };
        Assert.Equal((3, p), (s.SelectedLayerId, s.Preview));
        Assert.Throws<ArgumentOutOfRangeException>(() => s with { Zoom = 0 });
    }

    [Fact] // editor.js:362-363; ruling R2: no selection, a missing id or a hidden layer draws no overlay
    public void No_overlay_without_a_visible_selected_layer()
    {
        using var plain = Frame(OneLayer(), new ViewOptions(1));
        using var missing = Frame(OneLayer(), new ViewOptions(1) { SelectedLayerId = 9 });
        Assert.Equal(plain.Bytes, missing.Bytes);
        using var hiddenPlain = Frame(OneLayer(visible: false), new ViewOptions(1));
        using var hiddenSelected = Frame(OneLayer(visible: false, rotation: 30), new ViewOptions(1) { SelectedLayerId = 1 });
        Assert.Equal(hiddenPlain.Bytes, hiddenSelected.Bytes);
        using var shown = Frame(OneLayer(), new ViewOptions(1) { SelectedLayerId = 1 });
        Assert.NotEqual(plain.Bytes, shown.Bytes);
    }

    [Theory] // render-resize-handles parityCheck: 8 px squares at every zoom; the device scale multiplies them
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(0.5, 1)]
    [InlineData(1, 2)]
    public void Handles_are_8_css_px_at_every_zoom(double zoom, double deviceScale)
    {
        using var f = Frame(OneLayer(), new ViewOptions(zoom, deviceScale) { SelectedLayerId = 1 });
        double s = zoom * deviceScale;
        int x = (int)Math.Round(200 * s), y = (int)Math.Round(200 * s); // the top-left corner, device px
        int inside = (int)(3 * deviceScale), outside = (int)(5 * deviceScale);
        Assert.Equal(Cyan, f.GetPixel(x - inside, y - inside));
        Assert.Equal(Cyan, f.GetPixel(x + inside - 1, y + inside - 1));
        Assert.NotEqual(Cyan, f.GetPixel(x - outside, y - outside));
    }

    [Fact] // render-selection-box parityCheck: 2 px #17d4fa, dash [5, 5] from the top-left corner, clockwise
    public void The_box_is_a_2_px_cyan_dash_from_the_top_left()
    {
        using var f = Frame(OneLayer(), new ViewOptions(1) { SelectedLayerId = 1 });
        foreach (int row in new[] { 199, 200 }) // the 2 px stroke centered on y = 200
        {
            Assert.Equal(Cyan, f.GetPixel(204, row));  // dash 0..5 (past the 8 px handle)
            Assert.NotEqual(Cyan, f.GetPixel(207, row)); // gap 5..10
            Assert.Equal(Cyan, f.GetPixel(212, row));  // dash 10..15
        }
        Assert.NotEqual(Cyan, f.GetPixel(207, 198));
        Assert.NotEqual(Cyan, f.GetPixel(212, 201));
    }

    [Fact] // render-rotation-handle: the stem is 2 px #f22f89 up to the knob, the knob #f22f89 30 screen px above the edge
    public void The_stem_and_knob_sit_30_screen_px_above_the_top_edge()
    {
        foreach (double zoom in new[] { 1.0, 2.0 })
        {
            using var f = Frame(OneLayer(), new ViewOptions(zoom) { SelectedLayerId = 1 });
            int cx = (int)(250 * zoom), top = (int)(200 * zoom);
            Assert.Equal(Magenta, f.GetPixel(cx, top - 15));     // the stem, between the edge and the knob
            Assert.Equal(Magenta, f.GetPixel(cx - 1, top - 15));
            Assert.NotEqual(Magenta, f.GetPixel(cx + 2, top - 15));
            Assert.Equal(Magenta, f.GetPixel(cx, top - 30));     // the knob's center
            Assert.NotEqual(Magenta, f.GetPixel(cx, top - 40));  // above the knob (radius 8 plus its white rim)
        }
    }

    [Fact] // ruling T1-1: the knob is drawn where SelectionGeometry hit-tests it, not at 4.1's mirrored getRotHandlePos
    public void The_rotate_handle_is_hit_where_the_overlay_draws_it()
    {
        var doc = OneLayer(rotation: 90); // center (250, 225); the knob 25 + 30 to the right
        var (kx, ky) = SelectionGeometry.RotateHandle(doc.Layers[0], 1);
        using var f = Frame(doc, new ViewOptions(1) { SelectedLayerId = 1 });
        Assert.Equal(Magenta, f.GetPixel((int)Math.Round(kx), (int)Math.Round(ky)));
        Assert.NotEqual(Magenta, f.GetPixel(195, 225)); // 4.1's mirrored hit point: nothing is drawn there
        Assert.Equal(SelectHitKind.Rotate, SelectionGeometry.HitTest(doc, 1, kx, ky, 1).Kind);
    }

    [Fact] // ruling R4: every label pixel lies inside LabelBounds; rotation 0 has no label
    public void The_label_draws_only_inside_its_bounds()
    {
        foreach (var (rotation, zoom, scale) in new[] { (30.0, 1.0, 1.0), (-135.0, 1.5, 1.0), (17.25, 2.0, 1.0), (90.0, 1.0, 2.0), (-0.04, 0.5, 1.0) })
        {
            var layer = OneLayer(rotation: rotation).Layers[0];
            var options = new ViewOptions(zoom, scale);
            var size = Renderer.ViewSize(options);
            using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            Renderer.DrawRotationLabel(surface.Canvas, layer, options);
            using var image = surface.Snapshot();
            using var px = ImageDiff.FromImage(image);
            var box = SelectionGeometry.LabelBounds(layer, options);
            int drawn = 0;
            for (int y = 0; y < px.Height; y++)
                for (int x = 0; x < px.Width; x++)
                    if (px.GetPixel(x, y).Alpha > 0)
                    {
                        drawn++;
                        Assert.True(box.Contains(x, y), $"rotation {rotation}: label pixel ({x}, {y}) outside {box}");
                    }
            Assert.True(drawn > 20, $"rotation {rotation}: the label drew {drawn} pixels");
        }
        using var none = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        none.Canvas.Clear(SKColors.Transparent);
        Renderer.DrawRotationLabel(none.Canvas, OneLayer().Layers[0], new ViewOptions(1));
        using var blank = none.Snapshot();
        Assert.False(Eraser.HasContent(blank));
    }

    [Fact] // render-rotation-label: "30.0°" in 11 px JetBrains Mono 400, #f22f89, centered on (0, -h/2 - 46) in the rotated frame
    public void The_label_is_11_px_jetbrains_mono_magenta_centered_46_px_above_the_top_edge()
    {
        var layer = OneLayer(rotation: 30).Layers[0];
        using var got = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        got.Canvas.Clear(SKColors.Transparent);
        Renderer.DrawRotationLabel(got.Canvas, layer, new ViewOptions(1));
        using var want = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var c = want.Canvas;
        c.Clear(SKColors.Transparent);
        c.Translate(250, 225);
        c.RotateRadians((float)(30 * Math.PI / 180));
        using (var font = Fonts.CreateFont("JetBrains Mono", 400, 11))
        using (var paint = new SKPaint { Color = Magenta, IsAntialias = true })
            c.DrawText("30.0°", 0, -25 - 30 - 16, SKTextAlign.Center, font, paint);
        using var a = got.Snapshot();
        using var b = want.Snapshot();
        using var ga = ImageDiff.FromImage(a);
        using var gb = ImageDiff.FromImage(b);
        Assert.Equal(gb.Bytes, ga.Bytes);
        Assert.True(Eraser.HasContent(a));
    }

    static Document Template(bool mask) => new() { BgColor = "#000000", ShowMask = mask };

    [Fact] // render-live-preview, draw-preview-composite: after the layers, before the mask
    public void A_draw_preview_sits_between_the_layers_and_the_mask()
    {
        // A red rect from torso_front (231..359) across the gutter at x = 360 into torso_left (361..425).
        var preview = LivePreview.Draw(new Stroke(ShapeTool.Rect, new[] { new SKPoint(300, 100), new SKPoint(400, 150) }, SKColors.Red, 4, true));
        using var masked = Frame(Template(mask: true), new ViewOptions(1) { Preview = preview });
        using var open = Frame(Template(mask: false), new ViewOptions(1) { Preview = preview });
        Assert.Equal(SKColors.Red, masked.GetPixel(320, 120));      // inside a region
        Assert.NotEqual(SKColors.Red, masked.GetPixel(360, 120));   // the gutter is masked
        Assert.Equal(SKColors.Red, open.GetPixel(360, 120));        // mask off shows it
        var layered = Template(mask: false) with
        {
            Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(50, 50, SKColors.Blue), X = 290, Y = 90 }),
        };
        using var over = Frame(layered, new ViewOptions(1) { Preview = preview });
        Assert.Equal(SKColors.Red, over.GetPixel(320, 120));         // the preview is above the layers
    }

    [Fact] // editor.js:327: the eraser previews #f22f89 at half alpha; ruling R3: one point shows nothing
    public void An_eraser_preview_is_magenta_at_half_alpha_and_a_click_shows_nothing()
    {
        var doc = Template(mask: false);
        using var plain = Frame(doc, new ViewOptions(1));
        using var drag = Frame(doc, new ViewOptions(1) { Preview = LivePreview.Erase(new[] { new SKPoint(100, 100.5f), new SKPoint(200, 100.5f) }, 9) });
        Px.Near(new SKColor(121, 24, 68), drag.GetPixel(150, 100)); // 0.5 x #f22f89 over black
        using var click = Frame(doc, new ViewOptions(1) { Preview = LivePreview.Erase(new[] { new SKPoint(150, 100) }, 9) });
        Assert.Equal(plain.Bytes, click.Bytes);
    }

    [Fact] // ruling R12, draw-pen-single-click: a pen press previews the brush-size dot the commit will make
    public void A_pen_press_previews_its_dot()
    {
        var doc = Template(mask: false);
        using var dot = Frame(doc, new ViewOptions(1) { Preview = LivePreview.Draw(new Stroke(ShapeTool.Pen, new[] { new SKPoint(150.5f, 100.5f) }, SKColors.White, 10, false)) });
        Assert.Equal(SKColors.White, dot.GetPixel(150, 100));
        Assert.Equal(SKColors.White, dot.GetPixel(153, 100));
        Assert.Equal(SKColors.Black, dot.GetPixel(157, 100));
    }

    [Theory] // ruling R12: the preview composite equals the composite with the committed drawing layer, within 1 level
    [InlineData(ShapeTool.Pen)]
    [InlineData(ShapeTool.Line)]
    [InlineData(ShapeTool.Rect)]
    [InlineData(ShapeTool.Ellipse)]
    public void The_preview_is_the_commit(ShapeTool tool)
    {
        var stroke = new Stroke(tool, new[] { new SKPoint(120, 90), new SKPoint(260, 140), new SKPoint(330, 260) }, new SKColor(0x17, 0xd4, 0xfa), 7, false);
        var doc = Template(mask: false);
        using var previewSurface = SKSurface.Create(Renderer.TemplateInfo);
        Renderer.RenderComposite(previewSurface.Canvas, doc, false, LivePreview.Draw(stroke), null);
        var layer = Tools.CommitDrawing(stroke, 1, Tools.DrawingName(tool))!;
        using var commitSurface = SKSurface.Create(Renderer.TemplateInfo);
        Renderer.RenderComposite(commitSurface.Canvas, doc with { Layers = ImmutableList.Create(layer) }, false);
        using var a = previewSurface.Snapshot();
        using var b = commitSurface.Snapshot();
        using var pa = ImageDiff.FromImage(a);
        using var pb = ImageDiff.FromImage(b);
        Assert.True(ImageDiff.Compare(pb, pa, edgeTolerance: 1).MaxDelta <= 1, $"{tool}: {ImageDiff.Compare(pb, pa)}");
    }
}
```

Create `src/RBX15.Engine.Tests/ViewRendererTests.cs`:

```csharp
using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// ViewRenderer: 4.1's persistent offscreen (editor.js:296-298) and checker tile (editor.js:431-438) kept across frames
/// for the canvas host (M3a final-review carry-forward: render cost). Reuse must never change a pixel, and the native
/// surfaces must be released (M2a carry-forward: native memory lifetime). Ledger render-pipeline-canvas-reset (keep:
/// "cache ... but output must remain identical").
/// </summary>
public class ViewRendererTests
{
    static Document Scene() => new()
    {
        BgOpacity = 0.6,
        RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, null)), ("rarm_front", new Fill("dots", "#050c18", "#17d4fa", null, 12))),
        Layers = ImmutableList.Create(
            new Layer { Id = 1, Image = Px.Solid(60, 24, new SKColor(242, 47, 137)), X = 250, Y = 100, Rotation = 30 },
            new Layer { Id = 2, Image = Px.Solid(80, 40, new SKColor(23, 212, 250)), X = 100, Y = 380, Opacity = 0.7 }),
    };

    /// <summary>A sequence that changes everything the renderer reuses: size, device scale, mask, grid, overlay, preview kind, and back.</summary>
    static IEnumerable<(Document Doc, ViewOptions Options)> Sequence()
    {
        var doc = Scene();
        var pen = LivePreview.Draw(new Stroke(ShapeTool.Pen, new[] { new SKPoint(240, 90), new SKPoint(300, 150), new SKPoint(340, 120) }, SKColors.Yellow, 9, false));
        var erase = LivePreview.Erase(new[] { new SKPoint(250, 105), new SKPoint(300, 130) }, 14);
        yield return (doc, new ViewOptions(1));
        yield return (doc, new ViewOptions(1) { SelectedLayerId = 1 });
        yield return (doc, new ViewOptions(1) { Preview = pen });
        yield return (doc with { ShowMask = false }, new ViewOptions(0.5) { Preview = erase });
        yield return (doc, new ViewOptions(1.5, 1, true) { SelectedLayerId = 2 });
        yield return (doc, new ViewOptions(1, 1.25) { SelectedLayerId = 1, Preview = pen });
        yield return (doc with { Layers = doc.Layers.RemoveAt(0) }, new ViewOptions(1));
        yield return (new Document(), new ViewOptions(0.25));
        yield return (doc, new ViewOptions(1));
    }

    static byte[] Draw(Action<SKCanvas> render, ViewOptions options)
    {
        var size = Renderer.ViewSize(options);
        using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        render(surface.Canvas);
        using var image = surface.Snapshot();
        using var bitmap = ImageDiff.FromImage(image);
        return bitmap.Bytes;
    }

    [Fact] // render-pipeline-canvas-reset: one renderer reused across frames draws what a fresh one draws, byte for byte
    public void Reuse_is_byte_equal_to_fresh_across_a_document_sequence()
    {
        using var reused = new ViewRenderer();
        int frame = 0;
        foreach (var (doc, options) in Sequence())
        {
            var fresh = Draw(c => Renderer.RenderView(c, doc, options), options);
            var again = Draw(c => reused.Render(c, doc, options), options);
            Assert.True(fresh.AsSpan().SequenceEqual(again), $"frame {frame} differs on the reused renderer");
            frame++;
        }
    }

    [Fact] // a DPI change on the same renderer rebuilds the checker tile (CheckerHalf 8 -> 12 -> 8)
    public void A_device_scale_change_rebuilds_the_checker()
    {
        using var r = new ViewRenderer();
        foreach (double scale in new[] { 1, 1.5, 1 })
        {
            var options = new ViewOptions(1, scale);
            var fresh = Draw(c => Renderer.RenderView(c, new Document(), options), options);
            Assert.True(fresh.AsSpan().SequenceEqual(Draw(c => r.Render(c, new Document(), options), options)), $"device scale {scale}");
        }
    }

    [Fact] // M2a carry-forward: the persistent surfaces are released, and a disposed renderer refuses to draw
    public void Dispose_releases_and_refuses_reuse()
    {
        var r = new ViewRenderer();
        var options = new ViewOptions(1) { Preview = LivePreview.Erase(new[] { new SKPoint(1, 1), new SKPoint(50, 50) }, 4) };
        Draw(c => r.Render(c, Scene(), options), options);
        r.Dispose();
        r.Dispose(); // idempotent
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Assert.Throws<ObjectDisposedException>(() => r.Render(surface.Canvas, Scene(), new ViewOptions(1)));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        using var r = new ViewRenderer();
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Assert.Throws<ArgumentNullException>(() => r.Render(null!, new Document(), new ViewOptions(1)));
        Assert.Throws<ArgumentNullException>(() => r.Render(surface.Canvas, null!, new ViewOptions(1)));
        Assert.Throws<ArgumentNullException>(() => r.Render(surface.Canvas, new Document(), null!));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL. The errors include `error CS0117: 'ViewOptions' does not contain a definition for 'SelectedLayerId'`, the same for `'Preview'`, `error CS0246: The type or namespace name 'ViewRenderer' could not be found`, `error CS0117: 'Renderer' does not contain a definition for 'DrawRotationLabel'`, and `error CS0117: 'Renderer' does not contain a definition for 'TemplateInfo'`. A follow-on `CS0619` about `Assert.Throws<T>(Func<Task>)` can print alongside them; it is the compiler mis-binding the lambdas around the missing members, and it goes with them.

- [ ] **Step 7: Implement the overlay, the preview composite and ViewRenderer**

In `src/RBX15.Engine/ViewOptions.cs`, make this replacement:

Replace:

```csharp
    public double Scale => Zoom * DeviceScale;

    static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
```

with:

```csharp
    public double Scale => Zoom * DeviceScale;

    /// <summary>
    /// Draw 4.1's selection overlay (editor.js:361-425) for this layer: box, corner handles, rotation stem, knob, glyph
    /// and the label when Rotation != 0. Null, a missing id or a hidden layer (M3b ruling R2) draws nothing. The app
    /// passes it only while the tool is Select (editor.js:362).
    /// </summary>
    public int? SelectedLayerId { get; init; }

    /// <summary>The live drawing preview (editor.js:324-330), composited after the layers and before the mask. Null: none.</summary>
    public LivePreview? Preview { get; init; }

    static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
```

In `src/RBX15.Engine/Renderer.cs`, make these replacements in order, top to bottom. Each old block is unique in the file at the point it is applied:

Edit 1 of 7. Replace:

```csharp
    /// <summary>
    /// The offscreen half of render() (editor.js:300-337) without the live drawing preview (M3b):
    /// clear, background, region fills, visible layers bottom to top, then the mask with
    /// destination-in only when <paramref name="applyMask"/>. Export always masks
```

with:

```csharp
    /// <summary>
    /// The offscreen half of render() (editor.js:300-337) without a live drawing preview:
    /// clear, background, region fills, visible layers bottom to top, then the mask with
    /// destination-in only when <paramref name="applyMask"/>. Export always masks
```

Edit 2 of 7. Replace:

```csharp
    /// Same precondition as <see cref="RenderExport"/>: a kN32 premultiplied canvas in template space.
    /// </summary>
    public static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask)
    {
        ArgumentNullException.ThrowIfNull(canvas);
```

with:

```csharp
    /// Same precondition as <see cref="RenderExport"/>: a kN32 premultiplied canvas in template space.
    /// </summary>
    public static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask) =>
        RenderComposite(canvas, doc, applyMask, null, null);

    /// <summary>
    /// <see cref="RenderComposite(SKCanvas, Document, bool)"/> with the live drawing preview (editor.js:324-330) between
    /// the layers and the mask, so a masked view also masks the preview (ledger render-live-preview,
    /// draw-preview-composite). The preview is drawn into <paramref name="previewSurface"/> (a template-size kN32 surface
    /// the caller keeps, <see cref="ViewRenderer"/>) or, when null, into a surface made and disposed here; then that image
    /// is drawn at (0, 0) with <see cref="LivePreview.Alpha"/>, as 4.1's <c>drawImage(drawCanvas, 0, 0)</c> under
    /// globalAlpha. The export never passes a preview.
    /// </summary>
    internal static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask, LivePreview? preview, SKSurface? previewSurface)
    {
        ArgumentNullException.ThrowIfNull(canvas);
```

Edit 3 of 7. Replace:

```csharp
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        if (!applyMask) return;
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

    /// <summary>
```

with:

```csharp
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        if (preview is not null) DrawPreview(canvas, preview, previewSurface);
        if (!applyMask) return;
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

    static void DrawPreview(SKCanvas canvas, LivePreview preview, SKSurface? reuse)
    {
        SKSurface? owned = null;
        var surface = reuse ?? (owned = SKSurface.Create(TemplateInfo) ?? throw new InvalidOperationException("cannot create the preview surface"));
        try
        {
            var pc = surface.Canvas;
            pc.Clear(SKColors.Transparent);
            if (preview.IsEraser) Eraser.DrawStroke(pc, preview.Stroke.Points, preview.Stroke.Width, EraseStroking.Once);
            else Tools.DrawStroke(pc, preview.Stroke);
            using var image = surface.Snapshot();
            using var paint = new SKPaint { ColorF = new SKColorF(0, 0, 0, preview.Alpha) };
            canvas.DrawImage(image, 0, 0, LayerSampling, paint);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>The template-size kN32 premultiplied surface info every composite, stroke and preview surface uses.</summary>
    internal static readonly SKImageInfo TemplateInfo =
        new(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

    /// <summary>
```

Edit 4 of 7. Replace:

```csharp
    static readonly SKColor CheckerBase = new(0x0f, 0x1f, 0x31);  // editor.js:435
    static readonly SKColor CheckerSquare = new(0x05, 0x0c, 0x18); // editor.js:436
    static readonly SKSamplingOptions NearestSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
```

with:

```csharp
    static readonly SKColor CheckerBase = new(0x0f, 0x1f, 0x31);  // editor.js:435
    static readonly SKColor CheckerSquare = new(0x05, 0x0c, 0x18); // editor.js:436
    internal static readonly SKSamplingOptions NearestSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
```

Edit 5 of 7. Replace:

```csharp
    /// <summary>
    /// The view (render(), editor.js:300-359) minus the live drawing preview and the selection overlay
    /// (both M3b). The composite renders at 585x559 identity (the Renderer's precondition), masked
    /// iff <c>doc.ShowMask</c>. The frame then gets:
    /// <list type="number">
```

with:

```csharp
    /// <summary>
    /// The view (render(), editor.js:300-429), through a <see cref="ViewRenderer"/> made for this call. The composite
    /// (with <see cref="ViewOptions.Preview"/>) renders at 585x559 identity (the Renderer's precondition), masked
    /// iff <c>doc.ShowMask</c>. The frame then gets:
    /// <list type="number">
```

Edit 6 of 7. Replace:

```csharp
    ///   (x, y, w, h) times the scale, unsnapped (editor.js:352-359; render-grid-overlay, overlay-grid-checkbox). The line
    ///   width is <c>DeviceScale</c>, 4.1's 1 CSS px.</item>
    /// </list>
    /// Precondition: <paramref name="canvas"/> is <see cref="ViewSize"/> pixels, kN32 premultiplied, identity matrix.
    /// </summary>
    public static void RenderView(SKCanvas canvas, Document doc, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        var size = ViewSize(options);

        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var offscreen = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the composite surface");
        RenderComposite(offscreen.Canvas, doc, doc.ShowMask);
        using var composite = offscreen.Snapshot();

        var frame = SKRect.Create(0, 0, size.Width, size.Height);
        canvas.Clear(SKColors.Transparent);
        using (var tile = CheckerTile(CheckerHalf(options.DeviceScale)))
        using (var shader = tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, NearestSampling))
        using (var checker = new SKPaint { Shader = shader })
            canvas.DrawRect(frame, checker);

        double scale = options.Scale;
        canvas.DrawImage(composite, frame, scale < 1 ? LayerSampling : NearestSampling);

        if (!options.ShowGrid) return;
        using var grid = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)options.DeviceScale,
            IsAntialias = true,
            ColorF = new SKColorF(1, 1, 0, 0.3f),
        };
        foreach (var r in Template.Regions(doc.Mode))
            canvas.DrawRect(SKRect.Create((float)(r.X * scale), (float)(r.Y * scale), (float)(r.W * scale), (float)(r.H * scale)), grid);
    }

    static SKImage CheckerTile(int half)
    {
        var info = new SKImageInfo(half * 2, half * 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
```

with:

```csharp
    ///   (x, y, w, h) times the scale, unsnapped (editor.js:352-359; render-grid-overlay, overlay-grid-checkbox). The line
    ///   width is <c>DeviceScale</c>, 4.1's 1 CSS px.</item>
    /// <item>when <see cref="ViewOptions.SelectedLayerId"/> names a visible layer, the selection overlay
    ///   (<see cref="DrawSelectionOverlay"/>, editor.js:361-425).</item>
    /// </list>
    /// Precondition: <paramref name="canvas"/> is <see cref="ViewSize"/> pixels, kN32 premultiplied, identity matrix.
    /// The app keeps one <see cref="ViewRenderer"/> instead, so the offscreen surface and checker tile persist.
    /// </summary>
    public static void RenderView(SKCanvas canvas, Document doc, ViewOptions options)
    {
        using var renderer = new ViewRenderer();
        renderer.Render(canvas, doc, options);
    }

    internal static SKImage CheckerTile(int half)
    {
        var info = new SKImageInfo(half * 2, half * 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
```

Edit 7 of 7. Replace:

```csharp
        surface.Canvas.DrawRect(SKRect.Create(half, half, half, half), square);
        return surface.Snapshot();
    }
```

with:

```csharp
        surface.Canvas.DrawRect(SKRect.Create(half, half, half, half), square);
        return surface.Snapshot();
    }

    static readonly SKColor SelectCyan = new(0x17, 0xd4, 0xfa);    // editor.js:375, :382
    static readonly SKColor SelectMagenta = new(0xf2, 0x2f, 0x89); // editor.js:391, :397, :418

    /// <summary>
    /// The selection overlay (editor.js:361-425; ledger render-selection-box, render-resize-handles,
    /// render-rotation-handle, render-rotation-label, select-handle-render), drawn on the view frame after the grid.
    /// Every size is 4.1's CSS px; the canvas is scaled by <c>DeviceScale</c> first, so at device scale 1 this is 4.1's
    /// drawing call for call. In the frame of the zoomed layer box (w = img.width*scale*zoom, centered at
    /// x*zoom + w/2, y*zoom + h/2, rotated by the layer's rotation; flips are not applied, editor.js:365-373):
    /// <list type="bullet">
    /// <item>the box: strokeRect(-w/2, -h/2, w, h), #17d4fa, width 2, dash [5, 5] from the top-left corner (:375-379);</item>
    /// <item>the corner handles: fillRect(hx-4, hy-4, 8, 8) at TL, TR, BR, BL, #17d4fa (:382-384);</item>
    /// <item>the stem: (0, -h/2) to (0, -h/2 - 30), #f22f89, width 2 (:387-393);</item>
    /// <item>the knob: a circle of radius 8 at (0, -h/2 - 30), filled #f22f89, stroked white 1.5 (:395-401);</item>
    /// <item>the glyph: an arc of radius 4 from -0.7π to 0.4π and its arrowhead, stroked white 1.5 (:403-414);</item>
    /// <item>the label when rotation != 0: <see cref="SelectionGeometry.RotationLabel"/> in 11 px JetBrains Mono 400,
    ///   #f22f89, centered, alphabetic baseline at (0, -h/2 - 46) (:416-420). JetBrains Mono is the vendored face
    ///   (render-rotation-label fix: bundled, so offline users get it too).</item>
    /// </list>
    /// Canvas defaults apply where 4.1 leaves them: antialiased, butt caps, miter joins with limit 10.
    /// A hidden layer draws nothing (M3b ruling R2).
    /// </summary>
    internal static void DrawSelectionOverlay(SKCanvas canvas, Layer layer, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(options);
        if (!layer.Visible) return;
        double zoom = options.Zoom;
        double w = layer.Image.Width * layer.Scale * zoom, h = layer.Image.Height * layer.Scale * zoom; // editor.js:365-366
        double cx = layer.X * zoom + w / 2, cy = layer.Y * zoom + h / 2;                               // editor.js:367-368
        int restoreTo = canvas.Save();
        canvas.Scale((float)options.DeviceScale);
        canvas.Translate((float)cx, (float)cy);
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));

        using (var dash = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0))
        using (var box = OverlayStroke(SelectCyan, 2))
        {
            box.PathEffect = dash;
            canvas.DrawRect(SKRect.Create((float)(-w / 2), (float)(-h / 2), (float)w, (float)h), box);
        }

        using (var handle = new SKPaint { Color = SelectCyan, IsAntialias = true, Style = SKPaintStyle.Fill })
            foreach (var (hx, hy) in new[] { (-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2) })
                canvas.DrawRect(SKRect.Create((float)(hx - 4), (float)(hy - 4), 8, 8), handle);

        double knobY = -h / 2 - SelectionGeometry.RotateHandleDistance;
        using (var stem = OverlayStroke(SelectMagenta, 2))
        using (var pb = new SKPathBuilder())
        {
            pb.MoveTo(0, (float)(-h / 2));
            pb.LineTo(0, (float)knobY);
            using var path = pb.Detach();
            canvas.DrawPath(path, stem);
        }

        using (var white = OverlayStroke(SKColors.White, 1.5f))
        {
            using (var knob = CanvasArc(0, (float)knobY, 8, 0, Math.PI * 2))
            {
                using (var fill = new SKPaint { Color = SelectMagenta, IsAntialias = true, Style = SKPaintStyle.Fill })
                    canvas.DrawPath(knob, fill);
                canvas.DrawPath(knob, white);
            }
            using (var glyph = CanvasArc(0, (float)knobY, 4, -Math.PI * 0.7, Math.PI * 0.4))
                canvas.DrawPath(glyph, white);
            double ax = Math.Cos(Math.PI * 0.4) * 4; // editor.js:408-409
            double ay = -h / 2 - SelectionGeometry.RotateHandleDistance + Math.Sin(Math.PI * 0.4) * 4;
            using var pb = new SKPathBuilder();
            pb.MoveTo((float)(ax - 3), (float)(ay - 2));
            pb.LineTo((float)ax, (float)ay);
            pb.LineTo((float)(ax + 2), (float)(ay - 3));
            using var arrow = pb.Detach();
            canvas.DrawPath(arrow, white);
        }

        canvas.RestoreToCount(restoreTo);
        DrawRotationLabel(canvas, layer, options);
    }

    /// <summary>
    /// The overlay's rotation label (editor.js:416-420; render-rotation-label): nothing when
    /// <see cref="SelectionGeometry.RotationLabel"/> is null, else that text in 11 px JetBrains Mono 400 (the vendored
    /// face), #f22f89, centered, alphabetic baseline at (0, -h/2 - 46) in the zoomed, rotated layer frame, the canvas scaled
    /// by <c>DeviceScale</c>. Every pixel it draws lies inside <see cref="SelectionGeometry.LabelBounds"/> (ruling R4).
    /// </summary>
    internal static void DrawRotationLabel(SKCanvas canvas, Layer layer, ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(options);
        if (SelectionGeometry.RotationLabel(layer.Rotation) is not { } label) return;
        double zoom = options.Zoom;
        double w = layer.Image.Width * layer.Scale * zoom, h = layer.Image.Height * layer.Scale * zoom;
        int restoreTo = canvas.Save();
        canvas.Scale((float)options.DeviceScale);
        canvas.Translate((float)(layer.X * zoom + w / 2), (float)(layer.Y * zoom + h / 2));
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));
        using var font = Fonts.CreateFont("JetBrains Mono", 400, 11);
        using var text = new SKPaint { Color = SelectMagenta, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawText(label, 0, (float)(-h / 2 - SelectionGeometry.RotateHandleDistance - 16), SKTextAlign.Center, font, text);
        canvas.RestoreToCount(restoreTo);
    }

    static SKPaint OverlayStroke(SKColor color, float width) => new()
    {
        Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
        StrokeCap = SKStrokeCap.Butt, StrokeJoin = SKStrokeJoin.Miter, StrokeMiter = 10,
    };

    /// <summary>
    /// <c>beginPath(); arc(x, y, r, start, end)</c>, clockwise, as Chromium 146 draws it (measured, ruling T1-2): one Skia
    /// arcTo of the sweep from the start angle as given, in degrees converted from 4.1's doubles, or two arcTo of 180
    /// degrees for a full turn. The start is not canonicalized into [0, 2π): -0.7π stays -126 degrees. Canonicalizing it to
    /// 234 degrees moves the 1.5 px glyph's antialiasing by up to 46 levels at every one of 100 sub-pixel positions;
    /// as given, all 100 match 4.1 exactly. The builder is empty, so the first arcTo starts with Skia's own moveTo to
    /// the arc's start point, as on canvas after beginPath().
    /// </summary>
    static SKPath CanvasArc(float x, float y, float r, double start, double end)
    {
        double sweep = end - start;
        float startDeg = (float)(start * 180 / Math.PI), sweepDeg = (float)(sweep * 180 / Math.PI);
        var oval = new SKRect(x - r, y - r, x + r, y + r);
        using var pb = new SKPathBuilder();
        if (sweep >= Math.PI * 2)
        {
            pb.ArcTo(oval, startDeg, 180, false);
            pb.ArcTo(oval, startDeg + 180, 180, false);
        }
        else
        {
            pb.ArcTo(oval, startDeg, sweepDeg, false);
        }
        return pb.Detach();
    }
```

Create `src/RBX15.Engine/ViewRenderer.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// A reusable view renderer: render() (editor.js:300-429) with 4.1's persistent offscreen. 4.1 allocates its 585x559
/// composite canvas once and reuses it on every render (editor.js:296-298, <c>_offCanvas</c>) and builds its checker tile
/// once (editor.js:431-438). This class keeps the same three things for the app's canvas host:
/// <list type="bullet">
/// <item>one 585x559 kN32 premultiplied composite surface, cleared at the start of every render;</item>
/// <item>one 585x559 preview surface, made on the first render with a <see cref="ViewOptions.Preview"/>;</item>
/// <item>the checker tile and its shader, rebuilt only when <see cref="Renderer.CheckerHalf"/> changes (a DPI change).</item>
/// </list>
/// Every render clears what it reuses, so the output is byte-identical to a fresh <see cref="Renderer.RenderView"/>
/// (pinned by <c>ViewRendererTests</c>, and by the 14 frozen view goldens, which run through this class). Per-call
/// surfaces and images (the composite snapshot, the preview snapshot) are disposed in the call that made them, and
/// <see cref="Dispose"/> releases the persistent ones (M2a carry-forward: native memory lifetime). UI thread only.
/// </summary>
public sealed class ViewRenderer : IDisposable
{
    SKSurface? _composite;
    SKSurface? _preview;
    SKImage? _tile;
    SKShader? _checker;
    int _tileHalf;
    bool _disposed;

    /// <summary>
    /// The view frame: the composite (clear, background, fills, layers, <see cref="ViewOptions.Preview"/> at its alpha,
    /// the mask iff <c>doc.ShowMask</c>) at identity on the persistent surface; then on <paramref name="canvas"/> the
    /// checker, the composite stretched to <see cref="Renderer.ViewSize"/>, the grid when <see cref="ViewOptions.ShowGrid"/>,
    /// and the selection overlay when <see cref="ViewOptions.SelectedLayerId"/> names a visible layer.
    /// Precondition as <see cref="Renderer.RenderView"/>: <paramref name="canvas"/> is <see cref="Renderer.ViewSize"/>
    /// pixels, kN32 premultiplied, identity matrix. Throws <see cref="ObjectDisposedException"/> after <see cref="Dispose"/>.
    /// </summary>
    public void Render(SKCanvas canvas, Document doc, ViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        var size = Renderer.ViewSize(options);

        _composite ??= SKSurface.Create(Renderer.TemplateInfo) ?? throw new InvalidOperationException("cannot create the composite surface");
        if (options.Preview is not null)
            _preview ??= SKSurface.Create(Renderer.TemplateInfo) ?? throw new InvalidOperationException("cannot create the preview surface");
        Renderer.RenderComposite(_composite.Canvas, doc, doc.ShowMask, options.Preview, _preview);

        var frame = SKRect.Create(0, 0, size.Width, size.Height);
        canvas.Clear(SKColors.Transparent);
        using (var checker = new SKPaint { Shader = Checker(Renderer.CheckerHalf(options.DeviceScale)) })
            canvas.DrawRect(frame, checker);

        double scale = options.Scale;
        using (var composite = _composite.Snapshot())
            canvas.DrawImage(composite, frame, scale < 1 ? Renderer.LayerSampling : Renderer.NearestSampling);

        if (options.ShowGrid)
        {
            using var grid = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)options.DeviceScale,
                IsAntialias = true,
                ColorF = new SKColorF(1, 1, 0, 0.3f),
            };
            foreach (var r in Template.Regions(doc.Mode))
                canvas.DrawRect(SKRect.Create((float)(r.X * scale), (float)(r.Y * scale), (float)(r.W * scale), (float)(r.H * scale)), grid);
        }

        if (options.SelectedLayerId is int id && doc.Layers.Find(l => l.Id == id) is { Visible: true } selected)
            Renderer.DrawSelectionOverlay(canvas, selected, options);
    }

    SKShader Checker(int half)
    {
        if (_checker is not null && _tileHalf == half) return _checker;
        _checker?.Dispose();
        _tile?.Dispose();
        _tile = Renderer.CheckerTile(half);
        _checker = _tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, Renderer.NearestSampling);
        _tileHalf = half;
        return _checker;
    }

    /// <summary>Releases the persistent surfaces, tile and shader. Idempotent; <see cref="Render"/> refuses afterwards.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _checker?.Dispose();
        _tile?.Dispose();
        _preview?.Dispose();
        _composite?.Dispose();
        _checker = null;
        _tile = null;
        _preview = null;
        _composite = null;
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.RendererOverlayTests|FullyQualifiedName~RBX15.Engine.Tests.ViewRendererTests"`
Expected: `Passed!  - Failed:     0, Passed:    22, Skipped:     0, Total:    22` (RendererOverlayTests 18, ViewRendererTests 4).

Run the render regression: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.GoldenViewTests|FullyQualifiedName~RBX15.Engine.Tests.RendererViewTests|FullyQualifiedName~RBX15.Engine.Tests.GoldenRenderTests|FullyQualifiedName~RBX15.Engine.Tests.GoldenAppTests|FullyQualifiedName~RBX15.Engine.Tests.WordArtSwapTests"`
Expected: `Passed!  - Failed:     0, Passed:   200, Skipped:     0, Total:   200`: GoldenViewTests 15, RendererViewTests 40, GoldenRenderTests 34, GoldenAppTests 88 and WordArtSwapTests 23.
- The 14 frozen view goldens now run through `ViewRenderer`, under the unchanged rule.
- The export path is the same calls in the same order: `RenderComposite(c, d, true)` with no preview.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 863, Failed: 0, Skipped: 0` (841 + 22), Parity.Tests `35`, App.Tests `411`. The App's `TemplateCanvasHost` still calls `Renderer.RenderView`, whose signature is unchanged; Task 2 moves it onto a held `ViewRenderer`.

Commit. End the message with your own model's `Co-Authored-By` trailer.

```bash
git add src/RBX15.Engine/ViewOptions.cs src/RBX15.Engine/Renderer.cs src/RBX15.Engine/ViewRenderer.cs src/RBX15.Engine.Tests/RendererOverlayTests.cs src/RBX15.Engine.Tests/ViewRendererTests.cs
git commit -m "feat(engine): selection overlay, live preview composite and a persistent ViewRenderer"
```

- [ ] **Step 8: Add the view-select harness (tool only; no capture yet)**

Create `tools/parity-ref/view-select-scenes.json`. There are 10 scenes, one per line.
- **The base content.** The shirt scenes share it: background at 0.6 opacity, solid torso fills, a dots and a checker sleeve, a drip at 0.5 and the 626 word art at 0.6.
- **The selection scenes.** `view.select` names the base layer whose overlay is drawn.
- **The preview scenes.** `preview` leaves a shape or a two-point eraser drag mid-stroke. Every preview point times the zoom is an integer client coordinate.
- **`vs-select-fliph-z50`** puts the word art at scale 1.2 (ruling T1-3).

```json
[
  {"name": "vs-select-z100", "view": {"zoom": 1, "mask": true, "grid": false, "select": 1}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6}]},
  {"name": "vs-select-z200", "view": {"zoom": 2, "mask": true, "grid": false, "select": 0}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6}]},
  {"name": "vs-select-rot30-z100", "view": {"zoom": 1, "mask": true, "grid": false, "select": 1}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6, "rotation": 30}]},
  {"name": "vs-select-rotneg135-z150", "view": {"zoom": 1.5, "mask": true, "grid": false, "select": 1}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6, "rotation": -135}]},
  {"name": "vs-select-fliph-z50", "view": {"zoom": 0.5, "mask": true, "grid": false, "select": 1}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 176, "y": 90, "scale": 1.2, "flipH": true}]},
  {"name": "vs-select-pants-grid-z100", "view": {"zoom": 1, "mask": true, "grid": true, "select": 0}, "mode": "Pants", "regionColors": {"waist_front": "#222222", "waist_back": "#17d4fa", "rleg_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}, "lleg_front": "#f22f89"}, "layers": [{"image": "packs/drips/drip_blue_pants_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}]},
  {"name": "vs-preview-rect-z100", "view": {"zoom": 1, "mask": true, "grid": false}, "preview": {"op": "shape", "tool": "rect", "from": [200, 150], "to": [380, 300], "color": "#f22f89", "width": 6, "fill": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6}]},
  {"name": "vs-preview-ellipse-fill-z200", "view": {"zoom": 2, "mask": true, "grid": false}, "preview": {"op": "shape", "tool": "ellipse", "from": [220, 180], "to": [360, 260], "color": "#17d4fa", "width": 4, "fill": true}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6}]},
  {"name": "vs-preview-line-z50", "view": {"zoom": 0.5, "mask": true, "grid": false}, "preview": {"op": "shape", "tool": "line", "from": [120, 100], "to": [460, 420], "color": "#ffcc00", "width": 10, "fill": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6}]},
  {"name": "vs-preview-erase-z100", "view": {"zoom": 1, "mask": true, "grid": false}, "preview": {"op": "erase", "layer": 1, "points": [[230, 120], [360, 160]], "size": 14}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#5ce6ff", "torso_back": "#f22f89", "torso_up": "#17d4fa", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 200, "y": 380, "scale": 0.5}, {"image": "packs/word_art/word_626_anton.png", "x": 236, "y": 100, "scale": 0.6}]}
]
```

Create `tools/parity-ref/view-select-ref.js`:

```js
// Electron main. Renders the 4.1 VIEW canvas (the visible #mainCanvas after render(), editor.js:300-429)
// with the two things view-ref.js leaves out: the selection overlay (editor.js:361-425) and the live
// drawing preview (editor.js:324-330). The engine's Renderer.RenderView with ViewOptions.SelectedLayerId
// and ViewOptions.Preview is held to it. M3b Task 1 froze its output once in tests/goldens/view-select;
// never re-run it into that folder. view-ref.js and tests/goldens/view are untouched.
//
// Each scene is an app scene (tools/parity-ref/README.md, "Scene format") without actions, plus:
//   view:    { zoom: 0.25..3 in 0.25 steps, mask: boolean (default true), grid: boolean (default false),
//              select?: 0-based base layer index }
//   preview: { op: "shape", tool: "line"|"rect"|"ellipse", from: [x,y], to: [x,y], color, width, fill }
//          | { op: "erase", layer, points: [[x,y], [x,y]], size }
// A scene has exactly one of view.select and preview. The base runs through the frozen app-harness.js
// (__pref.render, whose export capture is discarded). Then the real zoom buttons and the Mask and Grid
// checkboxes are driven, the vendored JetBrains Mono (styles.css:6) is loaded for the rotation label,
// and either selectLayer(id) runs (editor.js:575-580, which sets the Select tool) or the preview is
// started with in-page synthetic mouse events on #mainCanvas: mousedown and mousemoves, never mouseup,
// so drawCanvas stays live. These are page events in an offscreen window, as app-harness.js sends;
// nothing reaches the desktop. GPU raster is off, Google Fonts requests are answered from assets/fonts
// (fonts.js), and any other network request fails the run.
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-select-ref.js \
//     --scenes tools/parity-ref/view-select-scenes.json --out <dir> [--only name1,name2]
'use strict';
const { app, BrowserWindow, protocol } = require('electron');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const fonts = require('./fonts.js');

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
app.commandLine.appendSwitch('force-device-scale-factor', '1');

const ROOT = path.resolve(__dirname, '..', '..');
const HARNESS = fs.readFileSync(path.join(__dirname, 'app-harness.js'), 'utf8');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const blob = (rel) => execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();
const ZOOMS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5, 2.75, 3]; // zoom-controls: 0.25..3 in 0.25 steps
const SHAPE_TOOLS = { line: 'line', rect: 'rect', ellipse: 'circle' }; // 4.1 tool ids, editor.js:464-471

const violations = [];

function dataUrl(rel) {
  const file = path.resolve(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg' }[path.extname(file).toLowerCase()];
  if (!mime) throw new Error(`unsupported scene image type: ${rel}`);
  return `data:${mime};base64,` + fs.readFileSync(file).toString('base64');
}

function assetsFor(scene) {
  const images = {};
  for (const rel of [scene.bgImage, ...(scene.layers || []).map((l) => l.image)].filter(Boolean)) images[rel] = dataUrl(rel);
  return { images, projects: {} };
}

// A template point the page can send exactly: integers whose client coordinate x*zoom is an integer too.
function checkPoint(name, p, zoom) {
  if (!Array.isArray(p) || p.length !== 2 || !p.every(Number.isInteger)) throw new Error(`${name}: point ${JSON.stringify(p)} must be two integers`);
  if (!p.every((c) => Number.isInteger(c * zoom))) throw new Error(`${name}: point ${JSON.stringify(p)} times zoom ${zoom} is not an integer client coordinate`);
}

// Validates a view-select scene. The golden gate holds every frame to the unchanged rule (with the
// rotation label's box left out, ruling R4), so RGB-shifted layers are refused, as in view-ref.js.
function validate(scene) {
  if (!/^[a-z0-9][a-z0-9-]*$/.test(scene.name || '')) throw new Error(`bad scene name: ${scene.name}`);
  const v = scene.view;
  if (!v || typeof v !== 'object') throw new Error(`${scene.name}: a view-select scene needs a "view" object`);
  if (!ZOOMS.includes(v.zoom)) throw new Error(`${scene.name}: zoom ${v.zoom} is not a 4.1 zoom step`);
  for (const k of ['mask', 'grid']) if (v[k] !== undefined && typeof v[k] !== 'boolean') throw new Error(`${scene.name}: view.${k} must be a boolean`);
  if (scene.actions) throw new Error(`${scene.name}: view-select scenes take no actions`);
  const layers = scene.layers || [];
  for (const l of layers) {
    if (l.rShift || l.gShift || l.bShift) throw new Error(`${scene.name}: RGB-shifted layers are not view scenes (rgb-shift-compute)`);
  }
  const hasSelect = v.select !== undefined;
  const p = scene.preview;
  if (hasSelect === (p !== undefined)) throw new Error(`${scene.name}: give exactly one of view.select and preview`);
  if (hasSelect && !(Number.isInteger(v.select) && v.select >= 0 && v.select < layers.length)) throw new Error(`${scene.name}: view.select must index a base layer`);
  if (p !== undefined) {
    if (p.op === 'shape') {
      if (!SHAPE_TOOLS[p.tool]) throw new Error(`${scene.name}: unknown shape tool "${p.tool}"`);
      checkPoint(scene.name, p.from, v.zoom);
      checkPoint(scene.name, p.to, v.zoom);
      if (!/^#[0-9a-f]{6}$/.test(p.color || '')) throw new Error(`${scene.name}: color must be #rrggbb lowercase`);
      if (!(Number.isInteger(p.width) && p.width >= 1 && p.width <= 50)) throw new Error(`${scene.name}: width must be 1..50 (editor.html:57)`);
      if (typeof p.fill !== 'boolean') throw new Error(`${scene.name}: fill must be a boolean`);
    } else if (p.op === 'erase') {
      if (!(Number.isInteger(p.layer) && p.layer >= 0 && p.layer < layers.length)) throw new Error(`${scene.name}: preview.layer must index a base layer`);
      // Two points only: 4.1 re-strokes the path per move and v5 strokes it once; the two agree exactly for one segment.
      if (!Array.isArray(p.points) || p.points.length !== 2) throw new Error(`${scene.name}: an erase preview has exactly two points`);
      for (const q of p.points) checkPoint(scene.name, q, v.zoom);
      if (!(Number.isInteger(p.size) && p.size >= 1 && p.size <= 50)) throw new Error(`${scene.name}: size must be 1..50 (editor.html:57)`);
    } else {
      throw new Error(`${scene.name}: unknown preview op "${p.op}"`);
    }
  }
  return {
    view: { zoom: v.zoom, mask: v.mask !== false, grid: v.grid === true, select: hasSelect ? v.select : null },
    preview: p === undefined ? null : p,
  };
}

function loadScenes(file) {
  let scenes = JSON.parse(fs.readFileSync(path.resolve(file), 'utf8'));
  const only = arg('--only');
  if (only) {
    const want = only.split(',');
    for (const n of want) if (!scenes.some((s) => s.name === n)) throw new Error(`--only names an unknown scene: ${n}`);
    scenes = scenes.filter((s) => want.includes(s.name));
  }
  const names = scenes.map((s) => s.name);
  if (new Set(names).size !== names.length) throw new Error('duplicate scene names');
  return scenes.map((s) => ({ scene: s, ...validate(s) }));
}

// Runs in the page after __pref.render. Drives the real zoom buttons and checkboxes, loads the label
// face, selects or starts the preview, renders, and returns the visible canvas.
function viewSelectPage() {
  const byId = (id) => {
    const el = document.getElementById(id);
    if (!el) throw new Error(`#${id} not found in editor.html`);
    return el;
  };
  // A real control, set and fired the way its 4.1 listener expects (editor.js:492-494). The value must stick.
  const setInput = (id, value, eventName = 'input') => {
    const el = byId(id);
    el.value = String(value);
    if (el.value !== String(value)) throw new Error(`#${id} refused ${value} (holds ${JSON.stringify(el.value)})`);
    el.dispatchEvent(new Event(eventName, { bubbles: true }));
  };
  // A synthetic page event on #mainCanvas, mapped back by (clientX - rect.left) / zoom (editor.js:1059-1061,
  // :1149-1152). app-harness.js pins the canvas at the viewport origin.
  const mouse = (type, point) => {
    const c = byId('mainCanvas');
    const rect = c.getBoundingClientRect();
    if (!Number.isInteger(rect.left) || !Number.isInteger(rect.top)) throw new Error(`canvas not at integer client coordinates (${rect.left}, ${rect.top})`);
    const clientX = rect.left + point[0] * zoom;
    const clientY = rect.top + point[1] * zoom;
    const ev = new MouseEvent(type, { clientX, clientY, button: 0, buttons: 1, bubbles: true, cancelable: true, view: window });
    if (ev.clientX !== clientX || ev.clientY !== clientY) throw new Error(`MouseEvent rounded (${clientX}, ${clientY})`);
    c.dispatchEvent(ev);
  };

  window.__viewSelect = async function (v, p) {
    const steps = Math.round((v.zoom - zoom) / 0.25);
    for (let i = 0; i < Math.abs(steps); i++) byId(steps > 0 ? 'zoomIn' : 'zoomOut').click(); // editor.js:1490-1491
    if (zoom !== v.zoom) throw new Error(`zoom is ${zoom}, wanted ${v.zoom}`);
    for (const [id, on] of [['showMask', v.mask], ['showGrid', v.grid]]) { // editor.js:1494-1495
      const el = byId(id);
      el.checked = on;
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }
    if (showMask !== v.mask || showGrid !== v.grid) throw new Error('mask or grid did not take');

    // The label's face (editor.js:416) is the vendored JetBrains Mono that styles.css:6 imports. The canvas
    // fetches a web font lazily, so load it first or 4.1 draws its fallback.
    const spec = '11px "JetBrains Mono"';
    const faces = await document.fonts.load(spec);
    await document.fonts.ready;
    if (!faces.length || faces.some((f) => f.status !== 'loaded') || !document.fonts.check(spec)) throw new Error(`vendored font did not load for ${spec}`);

    if (v.select !== null) {
      const id = layers[v.select].id;
      selectLayer(id); // editor.js:575-580: selects and sets the Select tool
      if (currentTool !== 'select' || selectedLayerId !== id) throw new Error('selectLayer did not take');
    } else {
      selectedLayerId = null;
    }

    if (p && p.op === 'shape') {
      setTool({ line: 'line', rect: 'rect', ellipse: 'circle' }[p.tool]); // editor.js:473-478
      setInput('drawColor', p.color); // editor.js:492
      setInput('drawSize', p.width); // editor.js:493
      const fill = byId('drawFill');
      fill.checked = p.fill;
      fill.dispatchEvent(new Event('change', { bubbles: true })); // editor.js:494
      mouse('mousedown', p.from); // editor.js:1120-1147
      mouse('mousemove', p.to); // clear and redraw the shape, editor.js:1204-1234
    } else if (p && p.op === 'erase') {
      selectLayer(layers[p.layer].id); // the eraser acts on the selection (editor.js:1265-1271)
      setTool('eraser');
      setInput('drawSize', p.size);
      mouse('mousedown', p.points[0]); // moveTo only, editor.js:1133-1144
      mouse('mousemove', p.points[1]); // lineTo + stroke, editor.js:1200-1203
    }

    render();
    if (p && (!drawCanvas || !isDrawing)) throw new Error('the live preview is not open');
    if (!p && drawCanvas) throw new Error('a live preview is open in a selection scene');
    if (p && currentTool === 'select') throw new Error('a preview scene must not show the selection overlay');
    const c = byId('mainCanvas');
    return { width: c.width, height: c.height, dpr: window.devicePixelRatio, url: c.toDataURL('image/png') };
  };
}

async function callPage(win, expr) {
  const r = await win.webContents.executeJavaScript(
    `(${expr}).then((value) => ({ ok: true, value }), (e) => ({ ok: false, error: String((e && e.message) || e) }))`);
  if (!r.ok) throw new Error(r.error);
  return r.value;
}

app.whenReady().then(async () => {
  try {
    for (const scheme of ['https', 'http']) {
      protocol.handle(scheme, (request) => {
        try {
          const r = fonts.respond(request.url);
          if (r) return new Response(r.body, { status: 200, headers: r.headers });
        } catch (e) {
          violations.push(e.message);
          return new Response(e.message, { status: 500 });
        }
        violations.push(`blocked network request ${request.url}`);
        return new Response('blocked by tools/parity-ref', { status: 404 });
      });
    }
    const scenesFile = arg('--scenes');
    const out = arg('--out');
    if (!scenesFile || !out) throw new Error('usage: --scenes <file> --out <dir> [--only a,b]');
    const scenes = loadScenes(scenesFile);
    const outDir = path.resolve(out);
    fs.mkdirSync(outDir, { recursive: true });

    const win = new BrowserWindow({
      show: false, width: 1200, height: 800,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false },
    });
    const run = {
      tool: 'tools/parity-ref/view-select-ref.js',
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), stylesCssBlob: blob('styles.css'),
      fillsJsBlob: blob('lib/fills.js'), appHarnessBlob: blob('tools/parity-ref/app-harness.js'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
    };
    const index = { ...run, scenes: [] };
    for (const { scene, view, preview } of scenes) {
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(HARNESS);
      await win.webContents.executeJavaScript(`(${viewSelectPage.toString()})(); 0;`);
      await callPage(win, `__pref.render(${JSON.stringify(scene)}, ${JSON.stringify(assetsFor(scene))})`);
      const r = await callPage(win, `__viewSelect(${JSON.stringify(view)}, ${JSON.stringify(preview)})`);
      if (violations.length) throw new Error(`${scene.name}: ${violations.join('; ')}`);
      if (r.dpr !== 1) throw new Error(`${scene.name}: devicePixelRatio ${r.dpr}, expected 1`);
      const want = [Math.trunc(585 * view.zoom), Math.trunc(559 * view.zoom)]; // canvas.width = TW * zoom, editor.js:340-341
      if (r.width !== want[0] || r.height !== want[1]) throw new Error(`${scene.name}: canvas ${r.width}x${r.height}, expected ${want.join('x')}`);
      const bytes = Buffer.from(r.url.split(',')[1], 'base64');
      fs.writeFileSync(path.join(outDir, scene.name + '.png'), bytes);
      index.scenes.push({ name: scene.name, sha256: sha256(bytes), width: r.width, height: r.height, ...view, preview: preview ? preview.op : null });
      console.log(`rendered ${scene.name} ${r.width}x${r.height}`);
    }
    fs.writeFileSync(path.join(outDir, 'index.json'), JSON.stringify(index, null, 2) + '\n');
    console.log(`ok ${index.scenes.length}`);
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
```

In `tools/parity-ref/README.md`, make three edits. First, directly after the intro bullet that starts with `` - `view-ref.js` (M3a) ``, add this bullet:

```markdown
- `view-select-ref.js` (M3b) drives the real `editor.html` like `view-ref.js` (it injects `app-harness.js` unchanged), sets the zoom, Mask and Grid with the real controls, then either selects a layer (the selection overlay, editor.js:361-425) or leaves a shape or eraser drag mid-stroke with in-page mouse events (the live preview, editor.js:324-330), and captures the visible `#mainCanvas`. It renders the view-select scenes into `tests/goldens/view-select`.
```

Second, at the end of the `## Use` code block (after the two `view-ref.js` lines), add these two lines:

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-select-ref.js \
  --scenes tools/parity-ref/view-select-scenes.json --out <dir> [--only name1,name2]
```

Third, append this bullet at the end of the `## Scene format` section, after the `view` bullet:

```markdown
- `view.select` and `preview` (view-select scenes, `view-select-ref.js` only; exactly one per scene, no `actions`).
  - `view.select` is the 0-based base layer that `selectLayer` selects (editor.js:575-580), which also sets the Select tool, so the overlay draws.
  - `preview` is `{ op: "shape", tool: "line"|"rect"|"ellipse", from, to, color: "#rrggbb", width: 1..50, fill }` or `{ op: "erase", layer, points: [[x,y], [x,y]], size: 1..50 }`. The real controls are set, then mousedown and mousemoves are sent with no mouseup, so the stroke stays live.
  - An erase preview has exactly two points, where 4.1's per-move stroking and v5's single stroke agree. Every point times the zoom must be an integer client coordinate.
  - The label face (JetBrains Mono, styles.css:6) is loaded from `assets/fonts` before the final render.
```

Check that the tool parses: `node --check tools/parity-ref/view-select-ref.js && python -c "import json; s=json.load(open('tools/parity-ref/view-select-scenes.json')); print(len(s), sorted({x['view']['zoom'] for x in s}), sum('preview' in x for x in s))"`
Expected: `10 [0.5, 1, 1.5, 2] 4`.

Commit the tool on its own. End the message with your own model's `Co-Authored-By` trailer.

```bash
git add tools/parity-ref/view-select-ref.js tools/parity-ref/view-select-scenes.json tools/parity-ref/README.md
git commit -m "feat(parity-ref): view-select-ref.js captures 4.1's selection overlay and live preview"
```

- [ ] **Step 9: (Controller) Capture 4.1's view-select frames once, and freeze them**

**This step is the controller's.** A subagent implementing Task 1 stops after Step 8 and reports that Steps 1 to 8 are done and Step 9 is waiting for the controller.
- The capture runs 4.1 in Electron on this machine, offscreen, under the README rules: CPU raster, `env -u ELECTRON_RUN_AS_NODE`, and provenance in `index.json`.
- Its mouse events are page events dispatched to `#mainCanvas` inside a hidden window, as `app-harness.js` already sends. Nothing reaches the desktop.
- From the commit at the end of this step, `tests/goldens/view-select` is frozen like every other golden: never re-run the tool into it.

Check that the 4.1 sources are the ones the app and view goldens were captured from:

```bash
git hash-object editor.js editor.html styles.css
python -c "import json; d=json.load(open('tests/goldens/app/index.json')); print(d['editorJsBlob']); print(d['editorHtmlBlob'])"
```

Expected: `2790c41e418cd5bfea82cf1ae4d71626a7b54e61`, `401a5d5f33c4accbb452cb080c9c544a0f79a6ce`, `65110106efac704fd80e9fcb841835ccc0b487da`, then the first two again. If they differ, the Electron app changed (a global-constraint violation): stop and report it.

Render all 10 scenes twice into the git-ignored `.superpowers/`:

```bash
for run in a b; do
  env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-select-ref.js \
    --scenes tools/parity-ref/view-select-scenes.json --out .superpowers/m3b-measure/view-select-ref-$run 2>&1 | grep -E "^ok|Error"
done
```

Expected: `ok 10`, twice.

The index each run writes has this shape (values from the drafting runs):

```json
{
  "tool": "tools/parity-ref/view-select-ref.js",
  "electron": "41.1.0",
  "chrome": "146.0.7680.166",
  "editorJsBlob": "2790c41e418cd5bfea82cf1ae4d71626a7b54e61",
  "editorHtmlBlob": "401a5d5f33c4accbb452cb080c9c544a0f79a6ce",
  "stylesCssBlob": "65110106efac704fd80e9fcb841835ccc0b487da",
  "fillsJsBlob": "208d3297cb0920974d034639d754a65e75ae14d4",
  "appHarnessBlob": "fa7b41a07a2c618d629c83880d8204dab038d82f",
  "fontsManifestSha256": "ba0dafad93f0830be40f41301e1169c94730ccb8814d7dd0e9f21040f8813c9a",
  "scenes": [
    { "name": "vs-select-z100", "sha256": "<64 hex>", "width": 585, "height": 559, "zoom": 1, "mask": true, "grid": false, "select": 1, "preview": null },
    { "name": "vs-preview-erase-z100", "sha256": "<64 hex>", "width": 585, "height": 559, "zoom": 1, "mask": true, "grid": false, "select": null, "preview": "erase" }
  ]
}
```

The real file lists all 10 scenes in `view-select-scenes.json` order; two are shown.

Save this as `.superpowers/m3b/freeze-view-select-ref.py` and run it from the repo root. It does four things:
- compares the two runs;
- checks the provenance against the app and view goldens;
- refuses to overwrite an existing folder;
- copies run `a` into the frozen folder, with the scene file beside it.

```python
import hashlib, json, pathlib, shutil
a = pathlib.Path(".superpowers/m3b-measure/view-select-ref-a")
b = pathlib.Path(".superpowers/m3b-measure/view-select-ref-b")
scenes_src = pathlib.Path("tools/parity-ref/view-select-scenes.json")
out = pathlib.Path("tests/goldens/view-select")
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
names = [s["name"] for s in json.loads(scenes_src.read_text(encoding="utf-8"))]
assert len(names) == 10 and sorted(p.stem for p in a.glob("*.png")) == sorted(names), "run a must hold the 10 view-select scenes"
assert sorted(p.stem for p in b.glob("*.png")) == sorted(names), "run b must hold the 10 view-select scenes"
bad = [n for n in names if sha(a / f"{n}.png") != sha(b / f"{n}.png")]
assert not bad, f"MISMATCH {bad}: 4.1's view-select canvas is not deterministic; never freeze it"
ia = json.loads((a / "index.json").read_text(encoding="utf-8"))
ib = json.loads((b / "index.json").read_text(encoding="utf-8"))
assert ia == ib, "the two runs' index.json differ"
assert ia["tool"] == "tools/parity-ref/view-select-ref.js"
assert [s["name"] for s in ia["scenes"]] == names, "index.json must list the scenes in view-select-scenes.json order"
for s in ia["scenes"]:
    assert s["sha256"] == sha(a / f"{s['name']}.png"), f"{s['name']}: index sha256 does not match the file"
    assert (s["select"] is None) != (s["preview"] is None), f"{s['name']}: exactly one of select and preview"
print(f"reproducible: {len(names)}")
digest = hashlib.sha256("".join(f"{n} {sha(a / f'{n}.png')}\n" for n in sorted(names)).encode()).hexdigest()
print("digest", digest)
app = json.loads(pathlib.Path("tests/goldens/app/index.json").read_text(encoding="utf-8"))
for k in ("electron", "chrome", "editorJsBlob", "editorHtmlBlob", "fontsManifestSha256"):
    assert ia[k] == app[k], f"{k} differs from the app goldens' index: {ia[k]} vs {app[k]}"
view = json.loads(pathlib.Path("tests/goldens/view/index.json").read_text(encoding="utf-8"))
for k in ("fillsJsBlob", "appHarnessBlob"):
    assert ia[k] == view[k], f"{k} differs from the view goldens' index: {ia[k]} vs {view[k]}"
assert not out.exists(), f"{out} already exists: the view-select goldens are frozen, never recaptured"
out.mkdir()
for n in names:
    shutil.copyfile(a / f"{n}.png", out / f"{n}.png")
shutil.copyfile(a / "index.json", out / "index.json")
shutil.copyfile(scenes_src, out / "scenes.json")
print(f"froze {len(names)} views, index.json and scenes.json in {out.as_posix()}")
```

```bash
python .superpowers/m3b/freeze-view-select-ref.py
ls tests/goldens/view-select | wc -l
python -c "import json; d=json.load(open('tests/goldens/view-select/index.json')); print(len(d['scenes']), d['electron'], d['chrome'], *[f\"{s['name']}={s['width']}x{s['height']}\" for s in d['scenes'] if s['zoom'] != 1])"
```

Expected:

- `reproducible: 10`.
- `digest 9f8f7e77261e35212928a42cadb5b1a9bf0e5485478e92de48d46ac99e648b99`. That is the hash of the 10 sorted `name sha256` lines. It equals the drafting runs, so these are the frames every number in this task was measured against. A different digest with `reproducible: 10` means the rendering environment changed (Electron, the fonts, `editor.js`, `styles.css` or the scene file): stop and report the digest; do not commit.
- `froze 10 views, index.json and scenes.json in tests/goldens/view-select`, then `12`.
- `10 41.1.0 146.0.7680.166 vs-select-z200=1170x1118 vs-select-rotneg135-z150=877x838 vs-select-fliph-z50=292x279 vs-preview-ellipse-fill-z200=1170x1118 vs-preview-line-z50=292x279`.

Commit the frozen frames on their own, with the controller's own `Co-Authored-By` trailer:

```bash
git add tests/goldens/view-select
git commit -m "test(goldens): freeze 4.1's selection overlay and live preview views (view-select-ref.js)"
git status --short
```

Expected: `git status --short` prints nothing.

- [ ] **Step 10: The view-select golden gate**

In `src/RBX15.Engine.Tests/GoldenRule.cs`, add `AssertImageMatchesExcept` directly above the private `Report`. `Report`, the thresholds and `ImageDiff` are unchanged:

Replace:

```csharp
        using var golden = ImageDiff.Load(goldenPath);
        using var candidate = ImageDiff.FromImage(rendered);
        Report(name, golden, candidate, ImageDiff.Compare(golden, candidate), heatmapPath);
    }
```

with:

```csharp
        using var golden = ImageDiff.Load(goldenPath);
        using var candidate = ImageDiff.FromImage(rendered);
        Report(name, golden, candidate, ImageDiff.Compare(golden, candidate), heatmapPath);
    }

    /// <summary>
    /// The unchanged rule with some rectangles left out (M3b ruling R4): every pixel of <paramref name="excluded"/>
    /// (device px, clipped to the image) is set to transparent in both the golden and the candidate, then
    /// <see cref="ImageDiff.Compare"/> runs with its default tolerance as in <see cref="AssertImageMatches"/>. The
    /// view-select gate passes the rotation label's box (<c>SelectionGeometry.LabelBounds</c>); everything else in the
    /// frame, the box, dashes, handles, stem, knob and glyph included, is held to the unchanged rule. An empty list is
    /// exactly <see cref="AssertImageMatches"/>.
    /// </summary>
    public static void AssertImageMatchesExcept(string name, string goldenPath, SKImage rendered, string heatmapPath, IReadOnlyList<SKRectI> excluded)
    {
        ArgumentNullException.ThrowIfNull(excluded);
        using var golden = ImageDiff.Load(goldenPath);
        using var candidate = ImageDiff.FromImage(rendered);
        foreach (var bitmap in new[] { golden, candidate })
            foreach (var rect in excluded)
            {
                var r = SKRectI.Intersect(rect, new SKRectI(0, 0, bitmap.Width, bitmap.Height));
                for (int y = r.Top; y < r.Bottom; y++)
                    for (int x = r.Left; x < r.Right; x++)
                        bitmap.SetPixel(x, y, SKColors.Transparent);
            }
        Report(name, golden, candidate, ImageDiff.Compare(golden, candidate), heatmapPath);
    }
```

Create `src/RBX15.Engine.Tests/GoldenViewSelectTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The view-select gate: Renderer.RenderView with a selection overlay or a live preview meets 4.1's visible #mainCanvas
/// (render(), editor.js:300-429) for every frozen scene in tests/goldens/view-select, captured once by
/// tools/parity-ref/view-select-ref.js (M3b Task 1) and never edited. Each scene is a view scene plus either
/// <c>view.select</c> (the 0-based base layer the overlay is drawn for; 4.1's selectLayer sets the Select tool) or a
/// <c>preview</c> (a shape or a two-point eraser drag left mid-stroke). The frame is held to the unchanged rule, with
/// only the rotation label's box blanked in both images (ruling R4, <see cref="SelectionGeometry.LabelBounds"/>).
/// A failing scene writes diff-heatmaps/view-select/&lt;name&gt;.png.
/// </summary>
public class GoldenViewSelectTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "view-select");

    sealed record SelectScene(string Name, JsonElement Raw, double Zoom, bool Mask, bool Grid, int? Select, JsonElement? Preview);

    static readonly Lazy<IReadOnlyList<SelectScene>> Scenes = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e =>
        {
            var v = e.GetProperty("view");
            bool Flag(string k, bool fallback) => v.TryGetProperty(k, out var b) ? b.GetBoolean() : fallback;
            int? select = v.TryGetProperty("select", out var sel) ? sel.GetInt32() : null;
            JsonElement? preview = e.TryGetProperty("preview", out var p) ? p.Clone() : null;
            return new SelectScene(e.GetProperty("name").GetString()!, e.Clone(), v.GetProperty("zoom").GetDouble(),
                Flag("mask", true), Flag("grid", false), select, preview);
        }).ToList();
    });

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    static SKPoint Point(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle());

    /// <summary>The engine's preview for a scene's <c>preview</c> object, built from the same fields 4.1 was driven with.</summary>
    static LivePreview PreviewFor(JsonElement p) => p.GetProperty("op").GetString() switch
    {
        "shape" => LivePreview.Draw(new Stroke(
            p.GetProperty("tool").GetString() switch
            {
                "line" => ShapeTool.Line,
                "rect" => ShapeTool.Rect,
                "ellipse" => ShapeTool.Ellipse,
                var t => throw new InvalidDataException($"unknown shape tool {t}"),
            },
            new[] { Point(p.GetProperty("from")), Point(p.GetProperty("to")) },
            SKColor.Parse(p.GetProperty("color").GetString()!),
            p.GetProperty("width").GetSingle(),
            p.GetProperty("fill").GetBoolean())),
        "erase" => LivePreview.Erase(p.GetProperty("points").EnumerateArray().Select(Point).ToArray(), p.GetProperty("size").GetSingle()),
        var op => throw new InvalidDataException($"unknown preview op {op}"),
    };

    static (Document Doc, ViewOptions Options) Frame(SelectScene s)
    {
        var doc = SceneLoader.Parse(s.Raw, Root).Document with { ShowMask = s.Mask, ShowGrid = s.Grid };
        var options = new ViewOptions(s.Zoom, 1, s.Grid)
        {
            SelectedLayerId = s.Select is int i ? doc.Layers[i].Id : null,
            Preview = s.Preview is { } p ? PreviewFor(p) : null,
        };
        return (doc, options);
    }

    static IReadOnlyList<SKRectI> Excluded(Document doc, ViewOptions options)
    {
        if (options.SelectedLayerId is not int id) return [];
        var label = SelectionGeometry.LabelBounds(doc.Layers.Single(l => l.Id == id), options);
        return label.IsEmpty ? [] : [label];
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_view_with_overlay_or_preview_matches_the_frozen_4_1_view(string name)
    {
        var (doc, options) = Frame(Scenes.Value.Single(s => s.Name == name));
        using var rendered = Renderer.RenderViewImage(doc, options);
        GoldenRule.AssertImageMatchesExcept(name, Path.Combine(Dir, name + ".png"), rendered,
            Path.Combine(Root, "diff-heatmaps", "view-select", name + ".png"), Excluded(doc, options));
    }

    [Fact] // the gate is not vacuous: without the overlay or the preview every frozen frame fails the same rule
    public void Without_the_overlay_or_the_preview_every_scene_fails()
    {
        foreach (var s in Scenes.Value)
        {
            var (doc, options) = Frame(s);
            using var plain = Renderer.RenderViewImage(doc, options with { SelectedLayerId = null, Preview = null });
            using var golden = ImageDiff.Load(Path.Combine(Dir, s.Name + ".png"));
            using var candidate = ImageDiff.FromImage(plain);
            foreach (var bitmap in new[] { golden, candidate })
                foreach (var r in Excluded(doc, options))
                    for (int y = r.Top; y < r.Bottom; y++)
                        for (int x = r.Left; x < r.Right; x++)
                            bitmap.SetPixel(x, y, SKColors.Transparent);
            Assert.False(ImageDiff.Compare(golden, candidate).Pass, $"{s.Name}: the frame without its overlay or preview passed");
        }
    }

    [Fact] // ruling R4's cross-check: inside the label box every flat pixel of 4.1's frame still matches within 1 level
    public void The_label_box_meets_the_word_art_rule()
    {
        int labelled = 0;
        foreach (var s in Scenes.Value)
        {
            var (doc, options) = Frame(s);
            var excluded = Excluded(doc, options);
            if (excluded.Count == 0) continue;
            labelled++;
            using var rendered = Renderer.RenderViewImage(doc, options);
            using var golden = ImageDiff.Load(Path.Combine(Dir, s.Name + ".png"));
            using var candidate = ImageDiff.FromImage(rendered);
            var box = SKRectI.Intersect(excluded[0], new SKRectI(0, 0, golden.Width, golden.Height));
            using var g = new SKBitmap(new SKImageInfo(box.Width, box.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using var c = new SKBitmap(new SKImageInfo(box.Width, box.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (int y = 0; y < box.Height; y++)
                for (int x = 0; x < box.Width; x++)
                {
                    g.SetPixel(x, y, golden.GetPixel(box.Left + x, box.Top + y));
                    c.SetPixel(x, y, candidate.GetPixel(box.Left + x, box.Top + y));
                }
            GoldenRule.AssertWordArtBitmaps(s.Name + " label", g, c, Path.Combine(Root, "diff-heatmaps", "view-select", s.Name + "-label.png"));
        }
        Assert.Equal(2, labelled);
    }

    [Fact] // the blanked rects are the only pixels the rule skips
    public void The_exclusion_blanks_only_its_rects()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rbx15-view-select-rule-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string goldenPath = Path.Combine(dir, "golden.png");
            using (var golden = Px.Solid(40, 40, SKColors.Navy))
            using (var data = golden.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(goldenPath, data.ToArray());
            using var patched = Px.Render(40, 40, c =>
            {
                c.Clear(SKColors.Navy);
                using var p = new SKPaint { Color = SKColors.Yellow };
                c.DrawRect(SKRect.Create(10, 10, 8, 8), p);
            });
            string heat = Path.Combine(dir, "heat.png");
            GoldenRule.AssertImageMatchesExcept("inside", goldenPath, patched, heat, [new SKRectI(10, 10, 18, 18)]);
            GoldenRule.AssertImageMatchesExcept("clipped", goldenPath, patched, heat, [new SKRectI(-5, -5, 30, 30)]);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                GoldenRule.AssertImageMatchesExcept("short", goldenPath, patched, heat, [new SKRectI(10, 10, 17, 18)]));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                GoldenRule.AssertImageMatchesExcept("none", goldenPath, patched, heat, []));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact] // contract: box, handles, rotation handle, the label at a nonzero angle, zoom, flip, grid, and each preview kind
    public void The_frozen_set_covers_the_contracted_frames_and_matches_its_index()
    {
        var scenes = Scenes.Value;
        Assert.Equal(new[]
        {
            "vs-select-z100", "vs-select-z200", "vs-select-rot30-z100", "vs-select-rotneg135-z150", "vs-select-fliph-z50",
            "vs-select-pants-grid-z100", "vs-preview-rect-z100", "vs-preview-ellipse-fill-z200", "vs-preview-line-z50",
            "vs-preview-erase-z100",
        }, scenes.Select(s => s.Name));
        Assert.Equal(new[] { 0.5, 1, 1.5, 2 }, scenes.Select(s => s.Zoom).Distinct().Order());
        Assert.All(scenes, s => Assert.True(s.Select.HasValue != s.Preview.HasValue, $"{s.Name}: exactly one of select and preview"));

        var frames = scenes.ToDictionary(s => s.Name, Frame);
        var rotations = scenes.Where(s => s.Select.HasValue)
            .Select(s => frames[s.Name].Doc.Layers.Single(l => l.Id == frames[s.Name].Options.SelectedLayerId).Rotation).ToList();
        Assert.Contains(0.0, rotations);                       // no label
        Assert.Contains(30.0, rotations);                      // a label at a nonzero angle
        Assert.Contains(-135.0, rotations);                    // past 90, the knob below the center
        Assert.Contains(scenes, s => s.Select.HasValue && frames[s.Name].Doc.Layers[s.Select.Value].FlipH);
        Assert.Contains(scenes, s => s.Select.HasValue && s.Grid && frames[s.Name].Doc.Mode == TemplateMode.Pants);
        var kinds = scenes.Where(s => s.Preview.HasValue).Select(s => frames[s.Name].Options.Preview!)
            .Select(p => p.IsEraser ? "erase" : p.Stroke.Tool.ToString()).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { "Ellipse", "Line", "Rect", "erase" }, kinds);
        Assert.All(scenes.Where(s => s.Preview.HasValue), s => Assert.True(frames[s.Name].Options.Preview!.Stroke.Points.Count == 2));
        Assert.All(scenes, s => Assert.DoesNotContain(frames[s.Name].Doc.Layers, l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "index.json")));
        Assert.Equal("tools/parity-ref/view-select-ref.js", index.RootElement.GetProperty("tool").GetString());
        var entries = index.RootElement.GetProperty("scenes").EnumerateArray().ToList();
        Assert.Equal(scenes.Select(s => s.Name), entries.Select(e => e.GetProperty("name").GetString()));
        foreach (var (s, e) in scenes.Zip(entries))
        {
            var bytes = File.ReadAllBytes(Path.Combine(Dir, s.Name + ".png"));
            Assert.Equal(e.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var size = Renderer.ViewSize(new ViewOptions(s.Zoom));
            Assert.Equal((size.Width, size.Height), (e.GetProperty("width").GetInt32(), e.GetProperty("height").GetInt32()));
            Assert.Equal(s.Select, e.GetProperty("select").ValueKind == JsonValueKind.Null ? null : e.GetProperty("select").GetInt32());
            Assert.Equal(s.Preview?.GetProperty("op").GetString(), e.GetProperty("preview").GetString());
        }
        Assert.Equal(scenes.Count + 2, Directory.EnumerateFiles(Dir).Count()); // the PNGs, scenes.json, index.json
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.GoldenViewSelectTests"`
Expected: `Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14`: the 10 scenes plus four facts (coverage and index, non-vacuity, the exclusion rule, and the label's Word Art cross-check).

If a scene fails, the heatmap is at `diff-heatmaps/view-select/<name>.png`. The engine is wrong; the golden, the rule and the test stay as they are (global constraint). Start from the measured table in this task's notes.
- A failure only on the white knob glyph, at most about 14 px: the knob offset `h/2 + 30` landed in a band T1-3 measured, or `CanvasArc` canonicalized its angles (T1-2).
- A dash pattern that is off by a phase: the box is no longer one `DrawRect` with `CreateDash([5, 5], 0)`.
- A label failure under `The_label_box_meets_the_word_art_rule`: the label moved, or lost its face or size.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.GoldenViewTests|FullyQualifiedName~RBX15.Engine.Tests.GoldenRenderTests|FullyQualifiedName~RBX15.Engine.Tests.GoldenAppTests|FullyQualifiedName~RBX15.Engine.Tests.WordArtSwapTests"`
Expected: `Passed!  - Failed:     0, Passed:   160, Skipped:     0, Total:   160` (15 + 34 + 88 + 23; the same as before).

Commit. End the message with your own model's `Co-Authored-By` trailer.

```bash
git add src/RBX15.Engine.Tests/GoldenRule.cs src/RBX15.Engine.Tests/GoldenViewSelectTests.cs
git commit -m "test(engine): the view-select gate, overlay and preview held to 4.1 under the unchanged rule"
```

- [ ] **Step 11: Final verification**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror && dotnet test src/RBX15.slnx -c Release`
Expected: `Build succeeded.` with `0 Warning(s)`, then Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`, Parity.Tests `Passed: 35, Failed: 0`, App.Tests `Passed: 411, Failed: 0`.

Run: `git status --short`
Expected: nothing. `diff-heatmaps/` and `.superpowers/` are git-ignored.

Run: `git diff --stat 4e0be14 -- src/RBX15.App src/RBX15.App.Tests tests/goldens/view tests/goldens/app tests/goldens/render tests/goldens/projects editor.js editor.html styles.css lib`
Expected: nothing. Task 1 touches no App file, no earlier golden and no 4.1 file.

No commit in this step.

**Handed to Task 5** (ledger and divergences; this task changes neither). Each row closes only where its own parityCheck is met (the honesty rule).

- **Close, with this evidence:**
  - `draw-preview-composite`. Its parityCheck: "eraser preview is 50% magenta over art; shapes fully opaque; both clipped by mask". Evidence: `vs-preview-erase-z100`, `vs-preview-rect-z100`, `vs-preview-line-z50` and `vs-preview-ellipse-fill-z200` (the line crosses the gutters, masked), plus `RendererOverlayTests.An_eraser_preview_is_magenta_at_half_alpha_and_a_click_shows_nothing` and `A_draw_preview_sits_between_the_layers_and_the_mask`.
  - `render-selection-box`. Its parityCheck: box at rotation 0 and 30 with the dash phase from the top-left. Evidence: `vs-select-z100` and `vs-select-rot30-z100` (exact outside the label), plus `The_box_is_a_2_px_cyan_dash_from_the_top_left`. The hidden-layer divergence is pinned by `No_overlay_without_a_visible_selected_layer`.
  - `render-rotation-label`. Its parityCheck: 17.25 gives "17.3°", with the bundled font. Evidence: the label rows, `ToFixed1_is_javascript_not_dotnet_F1_on_exact_ties`, `The_label_is_11_px_jetbrains_mono_magenta_centered_46_px_above_the_top_edge` and `The_label_box_meets_the_word_art_rule`.
  - `select-resize-handle-hit`. Evidence: `A_corner_hits_strictly_inside_10_screen_px` (9.9 and 10.1, every corner, three zooms).
  - The engine halves of `select-resize-handles` and `select-resize-drag`: `The_resize_scale_is_the_distance_ratio_under_the_clamp` and `ScaleAboutCenter_keeps_the_center`. Their one-undo-step halves are Task 2's (`CanvasInteractionTests`).
- **Reword the parityCheck to the evidence, then close:**
  - `render-resize-handles`: "zoom 1/2/4" becomes "zoom 0.5/1/1.5/2". 4 is not a 4.1 zoom. Evidence: the select goldens and `Handles_are_8_css_px_at_every_zoom`.
  - `select-handle-render`: "rotation 0 and 37.5" becomes "0, 30 and -135". Evidence: the 6 select goldens.
  - `render-rotation-handle`: "rotation 0, 90, 180" becomes "0, 30 and -135". Evidence: the knob is exact in all 6 select goldens; T1-3's residual is noted.
  - `select-rotate-drag` and `select-rotate-drag-2`: their checks ask the sequence to equal JS. v5 normalizes on purpose, so the check becomes "snap plateaus at ±3 of each multiple of 45, and a crossing of ±180 stays in range". Evidence: the snap, normalize and seam theories.
- **Partial; leave open for later tasks:**
  - `render-live-preview`. Its check drags a pen. Pen previews are not frozen (approved divergence), so it waits on Task 2's interaction tests and Este's manual check. Or reword it to the shape and eraser goldens above.
  - `select-hidden-layer-still-handled`: the render and hit halves are here; the knob drag is Task 2's.
  - `select-rotate-handle-hit`: the math is here, the press is Task 2's. **Its divergence text grows T1-1.** Suggested text: "4.1: the rotation handle stays hittable for a hidden selected layer, and is hit-tested at the knob's mirror image across the layer's vertical axis (getRotHandlePos uses c.x - sin r·d), so a rotated layer's visible knob does not rotate it. v5: hidden layers have no handles, and the handle is hit where it is drawn."
  - `props-scale-clamp-inconsistency`, `wheel-scale-layer`, `wheel-brush-size`, `undo-empty-steps` and `layers-reorder-drag`: the engine math is here; the paths that use it are Tasks 2 to 4.
  - `render-pipeline-canvas-reset`: the persistent renderer is here; coalescing and the 20-layer budget are Task 2's.
  - `layers-model`: `LayerAdjustments` is here; the 0 display is Task 3's.
- **New rows for Task 5:**
  - The divergence half of T1-1, if Task 5 prefers its own row (suggested id `select-rotate-handle-mirrored`, owner Engine.Tools, check unit), instead of widening `select-rotate-handle-hit`.
  - A rasterization note for T1-3, in the form of `wordart-glyph-rasterization` (suggested id `overlay-knob-glyph-rasterization`): "The 1.5 px white rotate-glyph arc is antialiased by SkiaSharp 4.153.1 and Chromium 146's Skia differently for some knob offsets (measured: offsets 5 and 35-60 screen px, up to 41 levels on about 14 px; exact at 0-30 and 63 and above). Geometry is 4.1's call for call; the view-select goldens use exact offsets."

---

**Additions requested by Task 1** (beyond `.superpowers/m3b-planning/interfaces.md`; the sheet names are unchanged):

1. `internal static void Renderer.DrawRotationLabel(SKCanvas canvas, Layer layer, ViewOptions options)`. `DrawSelectionOverlay` calls it last; it is the test seam for R4's "no label pixel outside the box" and the face pin.
2. `internal static readonly SKImageInfo Renderer.TemplateInfo` (585x559 kN32 premul), used by `ViewRenderer` and the tests. `Renderer.NearestSampling` and `Renderer.CheckerTile(int)` go from private to internal so `ViewRenderer` can share them.
3. `internal static string SelectionGeometry.ToFixed1(double value)` (ruling T1-4). It replaces the sheet's `"F1"` comment on `RotationLabel`, which was measured wrong.
4. `SelectionGeometry.RotateHandle` returns the knob where it is drawn, `c.x + sin(r)·d`, not getRotHandlePos's mirrored `c.x - sin(r)·d` (ruling T1-1). The sheet's comment "getRotHandlePos :1029-1039" should read "the drawn knob (editor.js:372, :396); 4.1's getRotHandlePos mirrors it". Task 2's press and hover cursor use it as is.
5. Behavior the sheet leaves open (ruling T1-5):
   - `NormalizeRotation` of a value that is not finite is 0.
   - `MoveTo` and `SetOpacity` return the input for a value that is not finite.
   - `SetAdjustments` keeps the layer's value for a field that is not finite.
   - `ResizeScale` returns the start scale, unclamped, when the start distance is under 1e-9.
6. `GoldenViewSelectTests` has four facts beyond its 10 scenes: `The_frozen_set_covers_the_contracted_frames_and_matches_its_index`, `Without_the_overlay_or_the_preview_every_scene_fails`, `The_exclusion_blanks_only_its_rects`, and `The_label_box_meets_the_word_art_rule` (ruling T1-6).
7. The scene and index details beyond the sheet's table:
   - `vs-select-fliph-z50` uses the word art at scale 1.2 (ruling T1-3).
   - The index records `stylesCssBlob`, plus `select` and `preview` per scene.
   - Scenes take no `actions`, and an erase preview has exactly two points.
8. The sheet's R3 pin `EraserTests.A_press_without_movement_erases_nothing` is added; the M2a `A_click_without_movement_erases_nothing` stays. The sheet's conditional `HistoryTests.Record_refuses_null` is not added, because the guard and its assert already exist.
9. `LivePreview` copies its points in both factories, so Task 2 can keep growing its own point list between frames.

---

### Task 2: The canvas gesture state machine, the tools and brush, the eraser, cursors, the canvas peer and render coalescing

**Files:**

- Create:
  - Canvas model (namespace `RBX15.App.Canvas`): `src/RBX15.App/Canvas/Tool.cs`, `src/RBX15.App/Canvas/CanvasCursor.cs`, `src/RBX15.App/Canvas/ViewOverlay.cs`, `src/RBX15.App/Canvas/CanvasInteraction.cs`.
  - View-models: `src/RBX15.App/ViewModels/ToolsViewModel.cs` (ToolsViewModel and ToolButtonViewModel), `src/RBX15.App/ViewModels/BrushViewModel.cs`.
  - Views: `src/RBX15.App/Views/ToolsGroup.xaml`, `ToolsGroup.xaml.cs`, `BrushGroup.xaml`, `BrushGroup.xaml.cs`.
  - Controls: `src/RBX15.App/Controls/CanvasCursors.cs`, `src/RBX15.App/Controls/TemplateCanvasHostAutomationPeer.cs`.
  - Tests: `src/RBX15.App.Tests/ToolsViewModelTests.cs`, `BrushViewModelTests.cs`, `CanvasInteractionTests.cs`, `CanvasCursorsTests.cs`, `CanvasAutomationPeerTests.cs`.
- Modify:
  - `src/RBX15.App/Controls/TemplateCanvasHost.cs`: printed in full in Step 6. The click path goes (interfaces R1); the Interaction, Overlay, CanvasCursor and AutomationValue properties, the pointer seams, the wheel-burst timer, render coalescing through one `ViewRenderer`, and the automation peer come in.
  - `src/RBX15.App/ViewModels/CanvasViewModel.cs` (full, Step 6), `src/RBX15.App/Views/CanvasView.xaml` (full, Step 6), `src/RBX15.App/Composition.cs` (full, Steps 5 and 6), `src/RBX15.App/ViewModels/HomeViewModel.cs` (full, Step 5), `src/RBX15.App/Views/HomeRibbon.xaml` (full, Step 5), `src/RBX15.App/Services/ImportService.cs` (full, Step 6).
  - `src/RBX15.App/ViewModels/LayersViewModel.cs`: the `Selected` setter and the `LayerPicked` event only (Step 6).
  - `src/RBX15.App/EditorSession.cs`: the additive `Changing` event, raised first by `Apply`, `ApplyWithSelection`, `BeginGesture` and `Step` (Step 3; R7, critic MJ1), and `src/RBX15.App.Tests/EditorSessionTests.cs` (+1).
  - `src/RBX15.App/Views/ColorPickerPopup.xaml.cs` and `src/RBX15.App.Tests/ColorPickerPopupTests.cs`: `Canvas.` becomes `System.Windows.Controls.Canvas.` (Step 2). The new `RBX15.App.Canvas` namespace shadows WPF's `Canvas` class inside every `RBX15.App.*` namespace (measured: `error CS0234: The type or namespace name 'SetLeft' does not exist in the namespace 'RBX15.App.Canvas'`).
  - Tests: `CanvasViewModelTests.cs` (full, Step 6), `TemplateCanvasHostTests.cs`, `CanvasViewTests.cs`, `ShellWiringTests.cs` (full, Step 6), `HomeRibbonViewTests.cs`, `HomeTestKit.cs`, `ImportServiceTests.cs`, `LayersViewModelTests.cs`, `ThemeKeyGuardTests.cs` (12 view files become 14).
- Read only: `editor.js`, `editor.html`, `styles.css`, `lib/erase.js`. Task 1's engine files (`ViewOptions.cs`, `LivePreview.cs`, `ViewRenderer.cs`, `SelectionGeometry.cs`, `Tools.cs`, `Eraser.cs`, `LayerOps.cs`) are consumed, never edited here.

**Interfaces:**

- Consumes:
  - Task 1 (interfaces §1): `ViewOptions.SelectedLayerId` and `ViewOptions.Preview` (init-only); `LivePreview.Draw(Stroke)`, `LivePreview.Erase(IReadOnlyList<SKPoint>, float)`, `.Stroke`, `.IsEraser`, `.Alpha`; `ViewRenderer` (`Render`, `Dispose`); `Renderer.RenderViewImage` (through `ViewRenderer`); `SelectionGeometry.HitTest`, `Center`, `RotateStartOffset`, `RotateAngle`, `ResizeScale`, `WheelScale`; `SelectHit`, `SelectHitKind`; `LayerOps.MoveTo`, `ScaleAboutCenter`, `SetRotation`, `AddDrawing`; `Tools.MinBrushSize`, `MaxBrushSize`, `DefaultBrushSize`, `SanitizeWidth`, `StepBrushSize`; `Eraser.ApplyTo`, `Eraser.StrokeColor`.
  - M1 to M3a: `Tools.CommitDrawing`, `Tools.DrawingName`, `Stroke`, `ShapeTool`, `TemplateSize`; `EditorSession` (`Document`, `SelectedLayerId`, `SelectedLayer`, `NextId`, `Apply`, `ApplyWithSelection`, `BeginGesture`, `Select`, `Undo`, `UndoCount`, `InGesture`, `Changed`); `SessionChange`; `IStatus`; `IColorPicker`; `CanvasCoordinates.ToTemplate`; `CanvasDrop`; `StatusViewModel`; `ImportService`; `LayersViewModel`; `HomeViewModel`; `Composition.Build`; `TestShell.Main`; `StaThread.Run`/`Pump`; `HomeTestKit` (`SessionWith`, `LayerOf`, `Solid`, `Track`, `Home`); `FakeColorPicker`, `FakeStatus`, `TestFiles`; theme keys `SecondaryButtonSm`, `RibbonGroup`, `RibbonGroupLabel`, `BrandCyanBrush`, `FocusRing`; `HomeStyles.xaml`'s `ColorSwatchButton`.
- Produces (interfaces §2, verbatim):
  - `RBX15.App.Canvas`: `enum Tool { Select, Pen, Line, Rect, Ellipse, Eraser }`; `enum CanvasCursorKind { Crosshair, Move, Grab, Grabbing, ResizeNwse, EraserRing }`; `readonly record struct CanvasCursor(CanvasCursorKind Kind, double RingDiameter)`; `enum CanvasGesture { None, Move, Resize, Rotate, Draw, Erase }`; `sealed record ViewOverlay(int? SelectedLayerId, LivePreview? Preview)` with `None`; `sealed class CanvasInteraction` with the constructor `(EditorSession, ToolsViewModel, BrushViewModel, IStatus)`, `Gesture`, `IsActive`, `Preview`, `Cursor`, `Changed`, `PointerDown`, `PointerMove`, `PointerUp`, `PointerLeave`, `CaptureLost`, `Cancel`, `Wheel`, `EndWheelBurst`.
  - `RBX15.App.ViewModels`: `ToolsViewModel` (`Current`, `Buttons`, `ToggleCommand`, `Set`, `CurrentChanged`); `ToolButtonViewModel` (`Tool`, `Label`, `ToolTip`, `IsActive`, `Command`); `BrushViewModel(IColorPicker)` (`Color`, `Size`, `Fill`, `PickColorCommand`, `StepSize`, `SkColor`); `CanvasViewModel(EditorSession, ToolsViewModel, BrushViewModel, IStatus)` with `Interaction`, `Overlay`, `Cursor`, `AutomationValue` (click members removed); `HomeViewModel.Tools` and `.Brush` (`required init`); `LayersViewModel.LayerPicked`.
  - `RBX15.App.Services`: `ImportService.LayersAdded`.
  - `RBX15.App`: `public event EventHandler? EditorSession.Changing` (raised first by every change entry point; R7, critic MJ1).
  - `RBX15.App.Controls`: `TemplateCanvasHost` DPs `Interaction`, `Overlay`, `CanvasCursor`, `AutomationValue`; seams `HandlePointerDown`, `HandlePointerMove`, `HandlePointerUp`, `HandleWheel`, `HandleLostCapture`, `HandlePointerLeave`; `RenderCount`; `CanvasCursors.ToWpf` and `RenderEraserRing`; `TemplateCanvasHostAutomationPeer`.
  - Views: `ToolsGroup`, `BrushGroup`, in HomeRibbon between Mode and Edit.
  - Additions beyond the sheet: listed at the end.

**What 4.1 does (cited).** `editor.js`, `editor.html` and `styles.css` as shipped (the blobs `tests/goldens/app/index.json` records).

- **Tools** (`editor.html:41-52`): six `.btn.btn-sm.btn-secondary` buttons, `Select` "Select/Move (V)", `Draw` "Freehand draw (B)", `Line` "Line tool (L)", `Rect` "Rectangle (U)", `Ellipse` "Ellipse (O)", `Eraser` "Eraser (E)" (`:43-48`), then "Keys: ON" (`:49`). `toolButtons` maps the ids select, pen, line, rect, circle, eraser (`editor.js:464-471`). `setTool` sets `currentTool`, moves `.btn-active` and calls `updateCursor` (`:473-478`); the app starts on Select (`:479`). `toggleTool`: the active tool again returns to Select, and Select never toggles off (`:484-486`); every button uses it (`:488-490`). `.btn-active` is a cyan border, `rgba(23,212,250,.1)` and cyan text (`styles.css:408-412`); `.btn-sm` is padding 5px 10px, 11px (`styles.css:407`); `.ribbon-group-items` is a row with gap 4 (`styles.css:208`), `.vert` a column (`:209`).
- **Brush** (`editor.html:53-62`): `#drawColor` color input, `#17d4fa`, 26x24, no border, radius 4, "Draw color" (`:56`); `#drawSize` range 1..50, value 4, width 60, "Brush size" (`:57`); `#drawFill` checkbox labelled "Fill", 10px #888, gap 2 (`:59`). `drawColor`, `drawSize` and `drawFill` follow every input event (`editor.js:492-494`); defaults `editor.js:90-92`. None of it is in an undo snapshot.
- **Auto-select** (`tools-auto-select-switch`): `addLayer` calls `setTool('select')` (`editor.js:523`) and so does `selectLayer` (`:577`); `addDrawingLayer` does not (`:528-540`).
- **Select press** (`editor.js:1058-1118`): `(clientX - rect.left) / zoom` (`:1059-1061`). On the selected layer: the rotate handle within `14/zoom` (`:1068-1076`; `rotStartAngle = atan2(mx - cx, -(my - cy)) * 180/pi - rotation`, `:1073`; cursor `grabbing`, `:1074`), then the corners TL, TR, BR, BL within `10/zoom` (`:1078-1097`; start scale and distance to the center, `:1092-1093`; cursor `nwse-resize`, `:1094`). Then the topmost visible layer under the point is selected through `selectLayer` and starts a move (`:1100-1113`; `dragStart`, `layerStart`, `:1108-1109`; cursor `move`, `:1110`). Else deselect (`:1114-1117`).
- **Select move** (`editor.js:1154-1194`): idle hover is `grab` over the rotate handle, else `crosshair` (`:1156-1167`). Move: `x = layerStart.x + (mx - dragStart.x)` (`:1169-1171`). Rotate: `newRot = angle - rotStartAngle`, then the first of `[-180,-135,-90,-45,0,45,90,135,180]` within `< 3` (`:1172-1180`), never normalized. Resize: `scale = max(0.01, startScale * d / d0)`, center kept (`:1181-1190`). `render()` per move (`:1191`). Release and leave end the drag and reset the cursor (`:1241-1246`, `:1296-1302`). No undo for any of it.
- **Draw** (`editor.js:1120-1146`, `:1196-1236`, `:1249-1293`): every press pushes an undo snapshot (`:1122`). Pen and eraser fix their style at the press (`:1133-1144`; eraser `#f22f89`, `:1138`; `lineWidth = drawSize`, `:1139`) and re-stroke per move (`:1200-1203`); Line, Rect and Ellipse re-read `drawColor`, `drawSize` and `drawFill` on every move (`:1206-1210`) and draw from the press to the latest point (`:1212-1233`). The release scans alpha (`:1257-1262`); a drawing becomes `addDrawingLayer(drawCanvas, 'Pen drawing' | ...)` (`:1285`); the eraser erases the selected layer, or alerts "Select a layer to erase from, then try again." (`:1265-1283`, `:1270`). The live preview is the stroke canvas composited before the mask, at 0.5 for the eraser (`:324-330`).
- **Wheel** (`editor.js:1305-1320`): always `preventDefault` (`:1306`). Select with a selection: `scale = max(0.01, scale * (deltaY > 0 ? 0.95 : 1.05))`, top-left anchored (`:1307-1313`). Other tools: `drawSize = clamp(drawSize + (deltaY > 0 ? -1 : 1), 1, 50)` and the cursor refresh (`:1316-1318`).
- **Cursors** (`editor.js:440-459`): the eraser ring, `r = max(4, min(120, drawSize*zoom/2))`, `d = ceil(r)*2 + 4`, a black 3 px ring at 0.5 under a `#f22f89` 1.5 px dashed (4 3) ring, a 1.5 px center dot, hotspot at the center (`:443-454`); every other tool crosshair (`:456-459`). `render()` ends with `updateCursor()` (`:428`), which overwrites the drag cursors on the next move (canvas-cursor-states).
- **Render** (`editor.js:295-298`, `:300-429`): a persistent offscreen; one full render per mouse event (render-pipeline-canvas-reset).

**Ledger rows this task serves** (Task 5 closes them under the honesty rule; evidence per row under "Handed to Task 5"):

- Select: `select-hit-test` (keep), `select-move` (fix), `select-move-hit-and-drag` (fix), `select-deselect-click` (keep), `select-rotate-handle` (fix), `select-rotate-handle-hit` (fix), `select-rotate-drag` (fix), `select-rotate-drag-2` (fix), `select-resize-handles` (fix), `select-resize-handle-hit` (keep), `select-resize-drag` (fix), `select-hover-idle` (fix), `select-hover-and-end` (fix), `select-hidden-layer-still-handled` (fix), `canvas-select-gestures` (fix), `select-hit-test-rect-not-alpha` (keep, through Task 1's HitTest).
- Wheel: `wheel-scale-layer` (fix), `wheel-brush-size` (keep), `wheel-sign-only-and-midstroke` (fix).
- Tools and brush: `tools-set-tool`, `tools-toggle-off`, `tools-auto-select-switch`, `home-tool-buttons`, `home-brush-group`, `tools-draw-props`, `draw-brush-controls` (all keep).
- Draw: `draw-begin` (fix), `draw-tool-start` (fix), `draw-pen-incremental-stroke` (fix, approved), `draw-pen-single-click` (fix, approved), `draw-commit-layer` (keep), `layers-add-drawing` (keep), `draw-empty-no-layer` (keep), `draw-release-outside-canvas` (fix), `draw-tool-switch-midstroke` (fix), `canvas-draw-tools` (fix), `render-live-preview` (keep, app half).
- Eraser: `eraser-apply` (fix, app half), `eraser-selection-semantics` (fix), `eraser-cursor` (keep), `cursor-eraser` (fix).
- Cursors and input: `cursor-default` (fix), `tools-cursor` (fix), `canvas-cursor-states` (fix), `canvas-coord-map` (fix, the left-button and capture half), `canvas-mouse-button-agnostic` (fix), `render-restore-and-cursor` (keep).
- Undo: `undo-coverage-gaps` and `undo-empty-steps` (fix, the canvas half: drags, wheel, strokes).
- Render cost: `render-pipeline-canvas-reset` (keep: behavior kept, cost reduced).

**Rulings** (the sheet's R1 to R15 are applied as written; these are this task's own, each pinned):

1. **The release point is applied as a move first** when it differs from the last move (sheet's PointerUp). A mouse that jumps between the last WM_MOUSEMOVE and the button-up still lands the layer or the stroke where the button went up. Pinned: `CanvasInteractionTests.A_corner_drag_scales_uniformly_about_the_center_as_one_step` and every Drag-helper test.
2. **The wheel does nothing in the Select tool while a select drag is active.** 4.1 scaled during a drag, but here a resize recomputes the scale from the press on every move and would undo the wheel's step on the next move, and a move drag would fold the scale into the move's step. Draw tools still step the brush mid-stroke (R11). Pinned: `Wheel_with_no_selection_or_at_the_clamp_records_nothing` covers the no-op path; the drag guard is the `IsActive` check in `Wheel`.
3. **A shape stroke is the press point and the latest point** (Tools uses the first and the last): `_points[^1]` is replaced per move instead of growing, so a long rect drag does not copy hundreds of points per frame. Pinned: `Shapes_use_the_press_and_the_latest_point_and_reread_the_brush`.
4. **The overlay's selection is shown only in the Select tool** (editor.js:362), so after a drawing commit (R8: the tool stays) the new layer is selected but shows no handles until Select, as in 4.1. Pinned: `CanvasViewModelTests.The_overlay_shows_the_selection_only_in_select_and_carries_the_live_preview`.
5. **Measure is invalidated only when the frame's pixel size changes.** `Document` and `Zoom` lose `AffectsMeasure`; a content change only marks the frame dirty and waits for `CompositionTarget.Rendering`, because WPF's layout runs at Render priority, above Input, so a measure-time render would still render once per mouse event. Zoom, DPI and a null document still invalidate. Pinned: `TemplateCanvasHostTests.Content_changes_keep_the_measure_and_a_zoom_invalidates_it`, `Many_frame_input_changes_render_once`.
6. **R15 at other scales.** The cursor side is `2*ceil(r) + 2*ceil(2*DeviceScale)` (4.1's `ceil(r)*2 + 4` at 100%, with the margin scaled so the 3 px under-ring is never clipped), so the radius cap is `128 - ceil(2*DeviceScale)` device px: 126 at 100% (the sheet's `(256 - 4)/2`), 124 at 200%. Pinned: `CanvasCursorsTests.The_ring_radius_is_half_the_brush_times_zoom_at_least_4_and_fits_a_cursor` (8 rows), `The_largest_ring_is_256_px`.
7. **The cursor bitmap is straight alpha.** `CreateIconIndirect` takes a 32 bpp color bitmap with non-premultiplied alpha; the ring is rendered premultiplied and read back unpremultiplied. Only antialiased magenta edge pixels depend on it; it goes on the look sign-off list. Not pixel-pinned (Windows draws the cursor).
8. **Tool buttons tell UIA which one is active** through `AutomationProperties.ItemStatus = "Active"` set by the `.btn-active` style trigger, not a ToggleButton: a ToggleButton flips `IsChecked` itself before its command runs, which desyncs when Select is pressed on Select. Pinned: `HomeRibbonViewTests.The_tool_buttons_show_4_1_labels_and_the_active_tool`.

**Measured while drafting.** A scratch copy of this branch's working tree at `4e0be14` in `%TEMP%\rbx15-m3b-t2` (outside the repo; git state untouched), .NET SDK 10.0.302, SkiaSharp 4.153.1, CommunityToolkit.Mvvm 8.4.2, with stand-ins for Task 1's engine surface shaped exactly as interfaces §1 states: the two `ViewOptions` init properties; `LivePreview` (`Draw`, `Erase`, `Alpha`); `ViewRenderer` (a stub that calls `RenderView`, then strokes the preview and a box, so overlay-reaches-frame tests could see a difference; `RenderViewImage` was pointed at it, as Task 1 does); `SelectionGeometry` (HitTest, Center, RotateHandle, Corner, PointerAngle, RotateStartOffset, RotateAngle, NormalizeRotation, SnapRotation, ResizeScale, ClampScale, WheelScale, with 4.1's math); `LayerOps.MoveTo`, `SetRotation`, `ScaleAboutCenter`, `AddDrawing`; `Tools` brush constants, `SanitizeWidth`, `StepBrushSize`; `Eraser.ApplyTo`.

- **Build.** Every file below compiled with `dotnet build src/RBX15.slnx -c Release -warnaserror`: `0 Warning(s)`, `0 Error(s)`.
- **The suites, twice in a row:** Engine.Tests 718 (the stand-ins add no tests), Parity.Tests 35, App.Tests 478 run: 477 passed and 1 failed, `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` (host at (157.6, 71.2) instead of (158, 71)). That fact fails identically at the untouched baseline on this desktop (410 of 411, measured in a second scratch copy at `4e0be14`), because this display is not at 100% and the view's layout rounding follows it. It is not this task's; on a 100% desktop it passes, and the Expected lines below assume that.
- **Per-class counts** (`--list-tests`, diffed against the baseline copy): ToolsViewModelTests 4, BrushViewModelTests 6, CanvasInteractionTests 31, CanvasCursorsTests 13, CanvasAutomationPeerTests 2, CanvasViewModelTests 8 to 9, TemplateCanvasHostTests 21 to 26, HomeRibbonViewTests 9 to 11, ImportServiceTests 9 to 10, LayersViewModelTests 12 to 13, ShellWiringTests 1 to 2. Total App.Tests 411 + 67 = 478.
- **Win32 cursors.** `CanvasCursorsTests` built real HICON ring cursors through `CreateIconIndirect` and `CursorInteropHelper.Create` in the test process; no window, no desktop input.
- **Routed events, not desktop input.** `Only_the_left_button_reaches_the_interaction` raises `Mouse.MouseDownEvent` on an unparented host with `RaiseEvent`, the in-process pattern `HomeRibbonViewTests` already uses; WPF reads the position of an element outside a window as (0, 0), measured.
- **Reconciler changes since that run** (written to the critic's fixes, not compiled in the measured run; the implementer's build at each step is their check): `EditorSession.Changing` and its fact, the `_ownChange` guard in `CanvasInteraction.Wheel` and the two burst facts (critic MJ1, Step 3), the capture-failure guard in `TemplateCanvasHost.OnMouseLeftButtonDown` (critic mn2), the `UnhookRendering` call in `FlushFrame` (critic mn3), and the split `ColorPickerPopupTests` edit (critic mn1).
- **Not measured:** the live app (never launched, per the safety rules), the real Task 1 overlay pixels, and `CompositionTarget.Rendering` timing in a shown window. The tests read `Frame`, which flushes, so they never wait on a Rendering tick.

**Test deltas:** Engine.Tests **+0**, Parity.Tests **+0**, App.Tests **+70** (ToolsViewModelTests 4, BrushViewModelTests 6, CanvasInteractionTests 33, EditorSessionTests +1, CanvasCursorsTests 13, CanvasAutomationPeerTests 2, HomeRibbonViewTests +2, CanvasViewModelTests +1 (-1 click, +2), TemplateCanvasHostTests +5 (-2 click, +7), ShellWiringTests +1, ImportServiceTests +1, LayersViewModelTests +1). Task 1 leaves Engine.Tests at **877** and App.Tests at **411**; this task ends at Engine.Tests **877**, App.Tests **481** (411 + 70), Parity.Tests **35**. The reconciler added three facts after the measured run (critic MJ1: `CanvasInteractionTests` +2, `EditorSessionTests` +1), so the measured 478 becomes 481.

- [ ] **Step 1: Baseline**

Task 2 starts after Task 1 has committed. Confirm the branch and the starting counts.

Run: `git branch --show-current && dotnet build src/RBX15.slnx -c Release -warnaserror && dotnet test src/RBX15.slnx -c Release`
Expected: `feat/v5-m3b-select-draw`, `Build succeeded.` with `0 Warning(s)`, then Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`, Parity.Tests `Passed: 35, Failed: 0`, App.Tests `Passed: 411, Failed: 0`.

Also confirm Task 1's surface exists, since every later step compiles against it:

Run: `grep -n "SelectedLayerId\|LivePreview? Preview" src/RBX15.Engine/ViewOptions.cs; grep -ln "class ViewRenderer\|record LivePreview\|class SelectionGeometry" src/RBX15.Engine/*.cs; grep -n "static Document MoveTo\|static Document ScaleAboutCenter\|static Document SetRotation\|static EditorSnapshot AddDrawing" src/RBX15.Engine/LayerOps.cs; grep -n "StepBrushSize\|SanitizeWidth\|DefaultBrushSize" src/RBX15.Engine/Tools.cs; grep -n "ApplyTo" src/RBX15.Engine/Eraser.cs`
Expected: both ViewOptions properties; `LivePreview.cs`, `SelectionGeometry.cs`, `ViewRenderer.cs`; the four LayerOps members; the three Tools members; `Eraser.ApplyTo`. If any is missing, stop: Task 1 is not done.

- [ ] **Step 2: The tool and brush models (failing tests first)**

Serves `tools-set-tool`, `tools-toggle-off`, `home-tool-buttons` (R14 titles), `draw-brush-controls`, `tools-draw-props`, `home-brush-group`, `wheel-brush-size` (the step).

Create `src/RBX15.App.Tests/ToolsViewModelTests.cs`:

```csharp
using RBX15.App.Canvas;
using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>The Tools group's model: setTool and toggleTool (editor.js:464-490; tools-set-tool, tools-toggle-off, home-tool-buttons).</summary>
public class ToolsViewModelTests
{
    [Fact] // editor.html:43-48 order and labels; R14: titles without the shortcut text; editor.js:479 starts on Select
    public void Starts_on_select_with_six_buttons_in_4_1_order_and_no_shortcut_text()
    {
        var vm = new ToolsViewModel();
        Assert.Equal(Tool.Select, vm.Current);
        Assert.Equal(new[] { Tool.Select, Tool.Pen, Tool.Line, Tool.Rect, Tool.Ellipse, Tool.Eraser }, vm.Buttons.Select(b => b.Tool));
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser" }, vm.Buttons.Select(b => b.Label));
        Assert.Equal(new[] { "Select/Move", "Freehand draw", "Line tool", "Rectangle", "Ellipse", "Eraser" }, vm.Buttons.Select(b => b.ToolTip));
        Assert.Equal(new[] { true, false, false, false, false, false }, vm.Buttons.Select(b => b.IsActive));
    }

    [Fact] // tools-set-tool: one .btn-active at a time; setting the current tool raises nothing
    public void Set_makes_exactly_one_button_active_and_raises_once_per_change()
    {
        var vm = new ToolsViewModel();
        int changed = 0;
        vm.CurrentChanged += (_, _) => changed++;
        var names = HomeTestKit.Track(vm);
        vm.Set(Tool.Eraser);
        Assert.Equal(Tool.Eraser, vm.Current);
        Assert.Equal(new[] { false, false, false, false, false, true }, vm.Buttons.Select(b => b.IsActive));
        Assert.Equal(1, changed);
        Assert.Equal(new[] { nameof(ToolsViewModel.Current) }, names);
        vm.Set(Tool.Eraser);
        Assert.Equal(1, changed);
    }

    [Fact] // tools-toggle-off parityCheck: B twice gives pen then select; V twice stays select (editor.js:484-486)
    public void Pressing_the_active_tool_again_returns_to_select_and_select_never_toggles_off()
    {
        var vm = new ToolsViewModel();
        var draw = vm.Buttons.Single(b => b.Tool == Tool.Pen);
        draw.Command.Execute(null);
        Assert.Equal(Tool.Pen, vm.Current);
        draw.Command.Execute(null);
        Assert.Equal(Tool.Select, vm.Current);
        vm.ToggleCommand.Execute(Tool.Select);
        vm.ToggleCommand.Execute(Tool.Select);
        Assert.Equal(Tool.Select, vm.Current);
        vm.ToggleCommand.Execute(Tool.Rect);
        vm.ToggleCommand.Execute(Tool.Line);                     // another tool switches, it does not toggle off
        Assert.Equal(Tool.Line, vm.Current);
    }

    [Fact]
    public void An_unknown_tool_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolsViewModel().Set((Tool)42));
}
```

Create `src/RBX15.App.Tests/BrushViewModelTests.cs`:

```csharp
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The Brush group's model (editor.html:53-62, editor.js:90-92, :492-494; draw-brush-controls, home-brush-group, tools-draw-props).</summary>
public class BrushViewModelTests
{
    [Fact] // draw-brush-controls parityCheck: defaults on first launch equal 4.1's
    public void Defaults_are_4_1s()
    {
        var vm = new BrushViewModel(new FakeColorPicker());
        Assert.Equal(("#17d4fa", 4, false), (vm.Color, vm.Size, vm.Fill));
        Assert.Equal(new SKColor(0x17, 0xd4, 0xfa), vm.SkColor);
    }

    [Fact] // draw-brush-controls: range 1..50 enforced; wheel-brush-size: +1 / -1 per event
    public void Size_is_clamped_to_1_through_50_and_steps_by_one()
    {
        var vm = new BrushViewModel(new FakeColorPicker());
        vm.Size = 0;
        Assert.Equal(1, vm.Size);
        vm.Size = 51;
        Assert.Equal(50, vm.Size);
        vm.StepSize(+1);
        Assert.Equal(50, vm.Size);
        vm.Size = 4;
        vm.StepSize(-1);
        Assert.Equal(3, vm.Size);
        vm.StepSize(0);                                          // sign 0 counts as up, as deltaY 0 does (editor.js:1316)
        Assert.Equal(4, vm.Size);
    }

    [Fact] // tools-draw-props: drawColor follows every input event; the themed picker previews live; cancel restores
    public void The_picker_previews_live_stores_lowercase_and_cancel_restores()
    {
        var picker = new FakeColorPicker().Answer("#FF0000", "#00FF00", "#0000ff").Answer(null, "#123456");
        var vm = new BrushViewModel(picker);
        var seen = new List<string>();
        picker.DuringPick = () => seen.Add(vm.Color);
        var names = HomeTestKit.Track(vm);
        var anchor = new object();

        vm.PickColorCommand.Execute(anchor);
        Assert.Equal("#ff0000", vm.Color);
        Assert.Equal(new[] { "#0000ff" }, seen);                 // the last preview, while the picker was open
        Assert.Equal(("#17d4fa", anchor), (picker.Seeds[0], picker.Anchors[0]));
        Assert.Contains(nameof(BrushViewModel.SkColor), names);

        vm.PickColorCommand.Execute(null);
        Assert.Equal("#ff0000", vm.Color);                       // cancelled after a preview: the seed again
        Assert.Equal("#ff0000", picker.Seeds[1]);
    }

    [Theory]
    [InlineData("17d4fa")]
    [InlineData("#17d4f")]
    [InlineData("#17d4fz")]
    public void A_color_that_is_not_hex_is_refused(string hex) =>
        Assert.Throws<ArgumentException>(() => new BrushViewModel(new FakeColorPicker()).Color = hex);
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0234: The type or namespace name 'Canvas' does not exist in the namespace 'RBX15.App'` in `ToolsViewModelTests.cs`; `CS0246` for `ToolsViewModel` and `BrushViewModel` may appear alongside.

Create `src/RBX15.App/Canvas/Tool.cs`:

```csharp
using RBX15.Engine;

namespace RBX15.App.Canvas;

/// <summary>
/// The six canvas tools (editor.js:464-471; ledger tools-set-tool). 4.1's ids are select, pen, line,
/// rect, circle and eraser; Ellipse is 4.1's 'circle', whose layers are named "Circle drawing"
/// (Tools.DrawingName, editor.js:1285).
/// </summary>
public enum Tool { Select, Pen, Line, Rect, Ellipse, Eraser }

/// <summary>Tool to engine shape mapping.</summary>
public static class ToolShapes
{
    /// <summary>The engine shape a drawing tool strokes; null for Select and Eraser, which make no layer.</summary>
    public static ShapeTool? ShapeOf(Tool tool) => tool switch
    {
        Tool.Pen => ShapeTool.Pen,
        Tool.Line => ShapeTool.Line,
        Tool.Rect => ShapeTool.Rect,
        Tool.Ellipse => ShapeTool.Ellipse,
        Tool.Select or Tool.Eraser => null,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "not a canvas tool"),
    };
}
```

Create `src/RBX15.App/Canvas/CanvasCursor.cs`:

```csharp
namespace RBX15.App.Canvas;

/// <summary>
/// The canvas cursor shapes (ledger canvas-cursor-states, tools-cursor, select-hover-idle,
/// cursor-default, cursor-eraser). 4.1 uses crosshair, move, grab, grabbing, nwse-resize and the
/// generated eraser ring (editor.js:443-459, :1074, :1094, :1110, :1161, :1165).
/// </summary>
public enum CanvasCursorKind { Crosshair, Move, Grab, Grabbing, ResizeNwse, EraserRing }

/// <summary>RingDiameter: the brush size in template px when Kind == EraserRing, else 0.</summary>
public readonly record struct CanvasCursor(CanvasCursorKind Kind, double RingDiameter)
{
    public static readonly CanvasCursor Crosshair = new(CanvasCursorKind.Crosshair, 0);

    /// <summary>The eraser ring for a brush of <paramref name="size"/> template px (eraserCursorURI, editor.js:443-454).</summary>
    public static CanvasCursor Ring(double size) => new(CanvasCursorKind.EraserRing, size);

    /// <summary>A cursor of <paramref name="kind"/> with no ring.</summary>
    public static CanvasCursor Of(CanvasCursorKind kind) => new(kind, 0);
}
```

Create `src/RBX15.App/Canvas/ViewOverlay.cs`:

```csharp
using RBX15.Engine;

namespace RBX15.App.Canvas;

/// <summary>
/// The host's frame input beyond Document and Zoom: the layer whose selection overlay the engine
/// draws (only while the tool is Select, editor.js:362) and the live drawing preview
/// (editor.js:324-330). TemplateCanvasHost passes both into ViewOptions.
/// </summary>
public sealed record ViewOverlay(int? SelectedLayerId, LivePreview? Preview)
{
    public static readonly ViewOverlay None = new(null, null);
}
```

Create `src/RBX15.App/ViewModels/ToolsViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Canvas;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's Tools group (editor.html:41-52; ledger home-tool-buttons, tools-set-tool, tools-toggle-off).
/// <see cref="Current"/> is 4.1's currentTool: Select at launch (editor.js:479). <see cref="Set"/> is setTool
/// (editor.js:473-478); <see cref="ToggleCommand"/> is toggleTool (editor.js:484-486): pressing the active tool
/// again returns to Select, and Select never toggles off. The tool is view state, never an undo step.
/// </summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    public ToolsViewModel()
    {
        ToggleCommand = new RelayCommand<Tool>(Toggle);
        Buttons =
        [
            // editor.html:43-48: label and title, minus the shortcut text until M4 ships the keyboard (R14)
            new(Tool.Select, "Select", "Select/Move", ToggleCommand),
            new(Tool.Pen, "Draw", "Freehand draw", ToggleCommand),
            new(Tool.Line, "Line", "Line tool", ToggleCommand),
            new(Tool.Rect, "Rect", "Rectangle", ToggleCommand),
            new(Tool.Ellipse, "Ellipse", "Ellipse", ToggleCommand),
            new(Tool.Eraser, "Eraser", "Eraser", ToggleCommand),
        ];
        Buttons[0].IsActive = true;
    }

    /// <summary>The active tool (currentTool, editor.js:89, :474).</summary>
    public Tool Current { get; private set; } = Tool.Select;

    /// <summary>The six buttons in editor.html:43-48 order.</summary>
    public IReadOnlyList<ToolButtonViewModel> Buttons { get; }

    /// <summary>toggleTool (editor.js:484-486): the active tool again returns to Select; Select stays Select.</summary>
    public IRelayCommand<Tool> ToggleCommand { get; }

    /// <summary>Raised after <see cref="Current"/> changes. CanvasInteraction cancels a stroke on it (R9).</summary>
    public event EventHandler? CurrentChanged;

    /// <summary>setTool (editor.js:473-478): one active button (.btn-active). Setting the current tool does nothing.</summary>
    public void Set(Tool tool)
    {
        if (!Enum.IsDefined(tool)) throw new ArgumentOutOfRangeException(nameof(tool), tool, "not a canvas tool");
        if (tool == Current) return;
        Current = tool;
        foreach (var button in Buttons) button.IsActive = button.Tool == tool;
        OnPropertyChanged(nameof(Current));
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    void Toggle(Tool tool) => Set(Current == tool && tool != Tool.Select ? Tool.Select : tool);
}

/// <summary>One tool button: 4.1's label and title (R14: no shortcut text before M4), and whether it is .btn-active.</summary>
public sealed partial class ToolButtonViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    internal ToolButtonViewModel(Tool tool, string label, string toolTip, IRelayCommand<Tool> toggle)
    {
        Tool = tool;
        Label = label;
        ToolTip = toolTip;
        Command = new RelayCommand(() => toggle.Execute(tool));
    }

    public Tool Tool { get; }
    public string Label { get; }
    public string ToolTip { get; }

    /// <summary>The group's ToggleCommand with this button's tool (btn.onclick = () => toggleTool(tool), editor.js:488-490).</summary>
    public IRelayCommand Command { get; }
}
```

Create `src/RBX15.App/ViewModels/BrushViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's Brush group (editor.html:53-62; ledger home-brush-group, draw-brush-controls,
/// tools-draw-props): the draw color, the brush size and the shape fill. Defaults are 4.1's:
/// #17d4fa, 4 and off (editor.js:90-92, editor.html:56-59). None of it is document state, so no
/// change here is ever an undo step (draw-brush-controls; interfaces R7). The color opens the themed
/// picker with live preview; a cancel puts the seed back (IColorPicker contract).
/// </summary>
public sealed partial class BrushViewModel : ObservableObject
{
    public const string DefaultColor = "#17d4fa";

    readonly IColorPicker _picker;
    string _color = DefaultColor;
    int _size = Tools.DefaultBrushSize;

    public BrushViewModel(IColorPicker picker)
    {
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        PickColorCommand = new RelayCommand<object?>(Pick);
    }

    /// <summary>drawColor (editor.js:492): lowercase #rrggbb, as an input of type color reports it.</summary>
    public string Color
    {
        get => _color;
        set
        {
            var next = NormalizeHex(value);
            if (SetProperty(ref _color, next)) OnPropertyChanged(nameof(SkColor));
        }
    }

    /// <summary>drawSize (editor.js:493): whole px, clamped to the range input's 1..50 (editor.html:57).</summary>
    public int Size
    {
        get => _size;
        set => SetProperty(ref _size, Math.Clamp(value, Tools.MinBrushSize, Tools.MaxBrushSize));
    }

    /// <summary>drawFill (editor.js:494): Rect and Ellipse fill instead of stroking (editor.js:1222, :1231).</summary>
    [ObservableProperty]
    private bool _fill;

    /// <summary><see cref="Color"/> as the opaque Skia color strokes are drawn in.</summary>
    public SKColor SkColor => SKColor.Parse(_color);

    /// <summary>Opens the picker under the anchor (the swatch); every preview sets Color, and a cancel restores the seed.</summary>
    public IRelayCommand<object?> PickColorCommand { get; }

    /// <summary>The wheel's brush step (editor.js:1316-1318): +1 for sign &gt;= 0, else -1, clamped 1..50.</summary>
    public void StepSize(int sign) => Size = Tools.StepBrushSize(Size, sign);

    void Pick(object? anchor)
    {
        var seed = _color;
        string? picked = null;
        try { picked = _picker.Pick(seed, hex => Color = hex, anchor); }
        finally { Color = picked ?? seed; }   // a cancel, or a picker that throws, reverts
    }

    static string NormalizeHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != 7 || hex[0] != '#' || hex.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF"))
            throw new ArgumentException($"'{hex}' is not #rrggbb.", nameof(hex));
        return hex.ToLowerInvariant();
    }
}
```

The new `RBX15.App.Canvas` namespace shadows WPF's `Canvas` class in every `RBX15.App.*` namespace (C# looks up `Canvas` in `RBX15.App` before the file's `using` directives). Two existing files name the class unqualified. In `src/RBX15.App/Views/ColorPickerPopup.xaml.cs`, replace the three lines

```csharp
        Canvas.SetLeft(SvMarker, ViewModel.Saturation * SquareWidth - SvMarker.Width / 2);
        Canvas.SetTop(SvMarker, (1 - ViewModel.Value) * SquareHeight - SvMarker.Height / 2);
        Canvas.SetLeft(HueMarker, ViewModel.Hue / 360 * StripWidth - HueMarker.Width / 2);
```

with

```csharp
        System.Windows.Controls.Canvas.SetLeft(SvMarker, ViewModel.Saturation * SquareWidth - SvMarker.Width / 2);
        System.Windows.Controls.Canvas.SetTop(SvMarker, (1 - ViewModel.Value) * SquareHeight - SvMarker.Height / 2);
        System.Windows.Controls.Canvas.SetLeft(HueMarker, ViewModel.Hue / 360 * StripWidth - HueMarker.Width / 2);
```

and in `src/RBX15.App.Tests/ColorPickerPopupTests.cs` (`The_markers_follow_the_color`), make two edits.

Edit A (line 103). Replace:

```csharp
        Assert.Equal((200 - 6.0, -6.0), (Canvas.GetLeft(p.SvMarker), Canvas.GetTop(p.SvMarker)));
```

with:

```csharp
        Assert.Equal((200 - 6.0, -6.0), (System.Windows.Controls.Canvas.GetLeft(p.SvMarker), System.Windows.Controls.Canvas.GetTop(p.SvMarker)));
```

Edit B (lines 106-107). Replace:

```csharp
        Assert.Equal((100 - 6.0, 35 - 6.0), (Canvas.GetLeft(p.SvMarker), Canvas.GetTop(p.SvMarker)));
        Assert.Equal(100 - 3.0, Canvas.GetLeft(p.HueMarker));
```

with:

```csharp
        Assert.Equal((100 - 6.0, 35 - 6.0), (System.Windows.Controls.Canvas.GetLeft(p.SvMarker), System.Windows.Controls.Canvas.GetTop(p.SvMarker)));
        Assert.Equal(100 - 3.0, System.Windows.Controls.Canvas.GetLeft(p.HueMarker));
```

Without these, the build fails with `error CS0234: The type or namespace name 'SetLeft' does not exist in the namespace 'RBX15.App.Canvas'` (measured).

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ToolsViewModelTests|FullyQualifiedName~RBX15.App.Tests.BrushViewModelTests"`
Expected: `Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 421, Failed: 0` (411 + 10). `ColorPickerPopupTests` still passes with the qualified names.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Canvas/Tool.cs src/RBX15.App/Canvas/CanvasCursor.cs src/RBX15.App/Canvas/ViewOverlay.cs src/RBX15.App/ViewModels/ToolsViewModel.cs src/RBX15.App/ViewModels/BrushViewModel.cs src/RBX15.App/Views/ColorPickerPopup.xaml.cs src/RBX15.App.Tests/ToolsViewModelTests.cs src/RBX15.App.Tests/BrushViewModelTests.cs src/RBX15.App.Tests/ColorPickerPopupTests.cs
git commit -m "feat(app): the canvas tool and brush models, 4.1's setTool, toggleTool and brush defaults"
```

- [ ] **Step 3: The canvas gesture state machine (failing tests first)**

Serves every select, draw, eraser, wheel and cursor row listed above, the app half; Review Focus candidates 1 to 3; interfaces R2, R3, R5 to R12; this task's rulings 1 to 4.

Create `src/RBX15.App.Tests/CanvasInteractionTests.cs`:

```csharp
using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas gesture state machine (editor.js:1056-1320) driven with template points, no desktop input:
/// select move, resize, rotate, hover, deselect and wheel; the draw tools with live preview and commit; the
/// eraser on the selection; one undo step per gesture (interfaces R2, R3, R5 to R12).
/// </summary>
public class CanvasInteractionTests
{
    /// <summary>A session over <paramref name="layers"/>, the shared tool and brush, a fake status strip and the interaction.</summary>
    sealed class Rig
    {
        public Rig(params Layer[] layers)
        {
            Session = SessionWith(layers);
            I = new CanvasInteraction(Session, Tools, Brush, Status);
        }

        public EditorSession Session { get; }
        public ToolsViewModel Tools { get; } = new();
        public BrushViewModel Brush { get; } = new(new FakeColorPicker());
        public FakeStatus Status { get; } = new();
        public CanvasInteraction I { get; }

        public Layer L(int id) => Session.Document.Layers.Find(l => l.Id == id)!;

        /// <summary>Press at the first point, move through the rest, release at the last.</summary>
        public void Drag(double zoom, params Point[] points)
        {
            I.PointerDown(points[0], zoom);
            foreach (var p in points.Skip(1)) I.PointerMove(p, zoom);
            I.PointerUp(points[^1], zoom);
        }
    }

    /// <summary>A 100x50 layer at (100, 100): center (150, 125), rotate handle (150, 70) at zoom 1.</summary>
    static Layer Box(int id = 1, bool visible = true) => LayerOf(id, 100, 50) with { X = 100, Y = 100, Visible = visible };

    /// <summary>The point at <paramref name="degrees"/> (0 = up, clockwise) and <paramref name="radius"/> from (cx, cy).</summary>
    static Point At(double cx, double cy, double degrees, double radius) =>
        new(cx + radius * Math.Sin(degrees * Math.PI / 180), cy - radius * Math.Cos(degrees * Math.PI / 180));

    static byte[] Pixels(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.Bytes;
    }

    static void AssertSameLayerImage(Layer expected, Layer actual)
    {
        Assert.Equal((expected.X, expected.Y, expected.Image.Width, expected.Image.Height), (actual.X, actual.Y, actual.Image.Width, actual.Image.Height));
        Assert.Equal(Pixels(expected.Image), Pixels(actual.Image));
    }

    // ---- Select: move, click, deselect ----

    [Fact] // select-move, select-move-hit-and-drag: x = start + (mx - dragStart.x) (editor.js:1169-1171); one step (R7)
    public void A_press_on_a_layer_selects_it_and_a_drag_moves_it_by_the_template_delta_as_one_step()
    {
        var rig = new Rig(Box());
        Assert.True(rig.I.PointerDown(new Point(120, 110), 2));
        Assert.Equal((CanvasGesture.Move, 1), (rig.I.Gesture, rig.Session.SelectedLayerId));
        rig.I.PointerMove(new Point(150, 95), 2);
        rig.I.PointerMove(new Point(170, 80), 2);
        Assert.Equal(0, rig.Session.UndoCount);   // adopted live, recorded at the release
        rig.I.PointerUp(new Point(170, 80), 2);

        Assert.Equal((150.0, 70.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.True(rig.Session.Undo());
        Assert.Equal((100.0, 100.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(1, rig.Session.SelectedLayerId);  // selection survives undo (spec §5)
    }

    [Fact] // select-hit-test: the press picks (editor.js:1100-1111); undo-empty-steps: a click records nothing
    public void A_click_without_movement_selects_and_records_nothing()
    {
        var rig = new Rig(Box());
        rig.Drag(1, new Point(120, 110));
        Assert.Equal(1, rig.Session.SelectedLayerId);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.False(rig.Session.InGesture);
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
    }

    [Fact] // select-deselect-click (editor.js:1114-1117): no gesture, no capture, never an undo step
    public void Empty_canvas_deselects_and_starts_nothing()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.False(rig.I.PointerDown(new Point(10, 500), 1));
        Assert.Null(rig.Session.SelectedLayerId);
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    // ---- Select: resize ----

    [Fact] // select-resize-handles, select-resize-drag: scale = start * d/d0, center kept (editor.js:1181-1190)
    public void A_corner_drag_scales_uniformly_about_the_center_as_one_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.True(rig.I.PointerDown(new Point(200, 100), 1));   // the top-right corner
        Assert.Equal(CanvasGesture.Resize, rig.I.Gesture);
        Assert.Equal(CanvasCursorKind.ResizeNwse, rig.I.Cursor.Kind);
        rig.I.PointerMove(new Point(225, 87.5), 1);              // 1.5 times the distance from (150, 125)
        var l = rig.L(1);
        Assert.Equal(1.5, l.Scale, 12);
        Assert.Equal((75.0, 87.5), (Math.Round(l.X, 9), Math.Round(l.Y, 9)));
        Assert.Equal(CanvasCursorKind.ResizeNwse, rig.I.Cursor.Kind); // held for the whole drag (canvas-cursor-states fix)
        rig.I.PointerUp(new Point(225, 87.5), 1);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // props-scale-clamp-inconsistency, select-resize-drag fix: one 1%-500% clamp (spec §5)
    public void Resize_is_clamped_to_1_and_500_percent()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.I.PointerDown(new Point(200, 100), 1);
        rig.I.PointerMove(new Point(650, -125), 1);              // 10 times the distance
        Assert.Equal(5, rig.L(1).Scale);
        rig.I.PointerMove(new Point(150.001, 125), 1);           // almost on the center
        Assert.Equal(0.01, rig.L(1).Scale);
        rig.I.PointerUp(new Point(150.001, 125), 1);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    // ---- Select: rotate ----

    [Fact] // select-rotate-handle-hit, select-rotate-drag, -2: snap to the first of -180..180 within < 3 (editor.js:1172-1180)
    public void The_rotate_handle_drag_snaps_within_3_degrees_and_is_one_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.True(rig.I.PointerDown(new Point(150, 70), 1));
        Assert.Equal((CanvasGesture.Rotate, CanvasCursorKind.Grabbing), (rig.I.Gesture, rig.I.Cursor.Kind));
        rig.I.PointerMove(At(150, 125, 44.2, 55), 1);
        Assert.Equal(45, rig.L(1).Rotation);
        rig.I.PointerMove(At(150, 125, 48.1, 55), 1);
        Assert.Equal(48.1, rig.L(1).Rotation, 9);
        rig.I.PointerMove(At(150, 125, 47.9, 55), 1);
        Assert.Equal(45, rig.L(1).Rotation);
        Assert.Equal(CanvasCursorKind.Grabbing, rig.I.Cursor.Kind);
        rig.I.PointerUp(At(150, 125, 47.9, 55), 1);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // select-rotate-drag fix: normalized to -180..180 (R5), so 4.1's -185.3 is 174.7 and -181.5 snaps to 180
    public void A_rotate_drag_across_the_bottom_stays_within_180_and_snaps_at_the_ends()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        Assert.True(rig.I.PointerDown(new Point(160, 70), 1));   // 10 px right of the handle, inside its 14 px
        double offset = Math.Atan2(10, 55) * 180 / Math.PI;      // editor.js:1073
        rig.I.PointerMove(At(150, 125, -175, 55), 1);
        Assert.Equal(360 + (-175 - offset), rig.L(1).Rotation, 9);
        rig.I.PointerMove(At(150, 125, -181.5 + offset, 55), 1);  // raw -181.5, normalized 178.5
        Assert.Equal(180, rig.L(1).Rotation);
        rig.I.PointerUp(At(150, 125, -181.5 + offset, 55), 1);
        Assert.InRange(rig.L(1).Rotation, -180, 180);
    }

    // ---- Select: cursors, hidden layers, capture, cancel ----

    [Fact] // select-hover-idle, canvas-cursor-states, cursor-default fixes: hover cursors; the handle is 30 screen px out
    public void Hover_shows_grab_resize_move_or_crosshair_and_follows_zoom()
    {
        var rig = new Rig(Box(), LayerOf(2, 40, 40) with { X = 400, Y = 400 });
        rig.Session.Select(1);
        CanvasCursorKind Hover(double x, double y, double zoom = 1)
        {
            rig.I.PointerMove(new Point(x, y), zoom);
            return rig.I.Cursor.Kind;
        }
        Assert.Equal(CanvasCursorKind.Grab, Hover(150, 70));
        Assert.Equal(CanvasCursorKind.ResizeNwse, Hover(200, 100));
        Assert.Equal(CanvasCursorKind.Move, Hover(150, 125));
        Assert.Equal(CanvasCursorKind.Move, Hover(420, 420));     // any visible layer box, not only the selection
        Assert.Equal(CanvasCursorKind.Crosshair, Hover(10, 10));
        Assert.Equal(CanvasCursorKind.Grab, Hover(150, 85, 2));   // 125 - 25 - 30/2
        Assert.Equal(CanvasCursorKind.Crosshair, Hover(150, 70, 2));

        rig.I.PointerMove(new Point(150, 125), 1);
        rig.I.PointerLeave();
        Assert.Equal(CanvasCursor.Crosshair, rig.I.Cursor);
        rig.Tools.Set(Tool.Line);
        Assert.Equal(CanvasCursorKind.Crosshair, Hover(150, 125));
    }

    [Fact] // select-hidden-layer-still-handled fix (R2): no handles on the canvas; the wheel still acts on it
    public void A_hidden_selected_layer_has_no_canvas_handles_but_the_wheel_still_scales_it()
    {
        var rig = new Rig(Box(visible: false));
        rig.Session.Select(1);
        Assert.False(rig.I.PointerDown(new Point(150, 70), 1));   // where its rotate handle would be
        Assert.Null(rig.Session.SelectedLayerId);                 // nothing visible there: deselect
        rig.Session.Select(1);
        Assert.False(rig.I.PointerDown(new Point(200, 100), 1));  // its corner
        Assert.Equal(0, rig.Session.UndoCount);

        rig.Session.Select(1);
        rig.I.Wheel(+1);
        Assert.Equal(1.05, rig.L(1).Scale, 12);
    }

    [Fact] // R10, select-hover-and-end fix: a lost capture ends the drag where it was, as one step
    public void Capture_loss_mid_drag_keeps_the_move_as_one_step()
    {
        var rig = new Rig(Box());
        rig.I.PointerDown(new Point(120, 110), 1);
        rig.I.PointerMove(new Point(150, 130), 1);
        rig.I.CaptureLost();
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.Equal((130.0, 120.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(1, rig.Session.UndoCount);
        rig.I.PointerMove(new Point(300, 300), 1);              // hover only now
        Assert.Equal((130.0, 120.0), (rig.L(1).X, rig.L(1).Y));
        rig.I.CaptureLost();                                    // idle: nothing
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // R9: a tool change mid select drag ends it as a release, keeping its change as one step
    public void A_tool_change_mid_select_drag_keeps_the_change_as_one_step()
    {
        var rig = new Rig(Box());
        rig.I.PointerDown(new Point(120, 110), 1);
        rig.I.PointerMove(new Point(130, 110), 1);
        rig.Tools.Set(Tool.Pen);
        Assert.Equal(CanvasGesture.None, rig.I.Gesture);
        Assert.False(rig.Session.InGesture);
        Assert.Equal((110.0, 100.0), (rig.L(1).X, rig.L(1).Y));
        Assert.Equal(1, rig.Session.UndoCount);
    }

    // ---- Wheel ----

    [Fact] // wheel-scale-layer, wheel-sign-only-and-midstroke fixes: centered, x1.05 / /1.05, a burst is one step (R6, R7)
    public void Wheel_scales_the_selection_about_its_center_symmetrically_and_a_burst_is_one_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        for (int i = 0; i < 10; i++) rig.I.Wheel(-1);
        Assert.Equal(1 / Math.Pow(1.05, 10), rig.L(1).Scale, 12);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.True(rig.Session.InGesture);
        rig.I.EndWheelBurst();
        Assert.Equal(1, rig.Session.UndoCount);

        for (int i = 0; i < 10; i++) rig.I.Wheel(+1);
        rig.I.EndWheelBurst();
        Assert.Equal(2, rig.Session.UndoCount);
        var l = rig.L(1);
        Assert.Equal(1, l.Scale, 12);                            // 4.1: 0.95^10 * 1.05^10 = 0.9794
        var (cx, cy) = SelectionGeometry.Center(l);
        Assert.Equal((150.0, 125.0), (Math.Round(cx, 9), Math.Round(cy, 9)));
    }

    [Fact] // undo-empty-steps: no selection does nothing; a wheel against the 500% clamp records nothing
    public void Wheel_with_no_selection_or_at_the_clamp_records_nothing()
    {
        var rig = new Rig(Box() with { Scale = 5 });
        rig.I.Wheel(+1);
        Assert.False(rig.Session.InGesture);
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        rig.I.EndWheelBurst();
        Assert.Equal(5, rig.L(1).Scale);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // R7: a press, a selection change or a tool change closes the wheel burst
    public void A_press_a_selection_change_or_a_tool_change_closes_the_wheel_burst()
    {
        var rig = new Rig(Box(), LayerOf(2, 40, 40) with { X = 400, Y = 400 });
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        rig.I.PointerDown(new Point(10, 500), 1);               // deselects, and closes the burst first
        Assert.Equal((1, false), (rig.Session.UndoCount, rig.Session.InGesture));

        rig.Session.Select(1);
        rig.I.Wheel(+1);
        rig.Session.Select(2);
        Assert.Equal((2, false), (rig.Session.UndoCount, rig.Session.InGesture));

        rig.I.Wheel(+1);
        rig.Tools.Set(Tool.Pen);
        Assert.Equal((3, false), (rig.Session.UndoCount, rig.Session.InGesture));
    }

    [Fact] // R7: an edit elsewhere during a burst's quiet period closes the burst first: two steps, undone one at a time
    public void An_edit_during_a_wheel_burst_is_its_own_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        Assert.True(rig.Session.Apply(d => LayerOps.SetOpacity(d, 1, 0.5)));
        rig.I.EndWheelBurst();
        Assert.Equal(2, rig.Session.UndoCount);
        Assert.True(rig.Session.Undo());
        Assert.Equal((1.05, 1.0), (Math.Round(rig.L(1).Scale, 12), rig.L(1).Opacity));
    }

    [Fact] // R7: a slider gesture opened during a burst records separately (no nesting into the wheel's scope)
    public void A_gesture_opened_during_a_wheel_burst_records_separately()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.I.Wheel(+1);
        using (rig.Session.BeginGesture()) rig.Session.Apply(d => LayerOps.SetOpacity(d, 1, 0.3));
        Assert.Equal((2, false), (rig.Session.UndoCount, rig.Session.InGesture));
    }

    // ---- Draw ----

    [Fact] // draw-commit-layer, layers-add-drawing, R12: the preview is the commit; one step; selected; the tool stays (R8)
    public void A_pen_drag_previews_the_whole_path_and_commits_one_selected_layer_named_Pen_drawing()
    {
        var rig = new Rig();
        rig.Brush.Color = "#ff0000";
        rig.Brush.Size = 6;
        rig.Tools.Set(Tool.Pen);
        var points = new[] { new Point(100, 100), new Point(140, 100), new Point(140, 140) };
        rig.I.PointerDown(points[0], 1);
        rig.I.PointerMove(points[1], 1);
        rig.I.PointerMove(points[2], 1);
        var preview = Assert.IsType<LivePreview>(rig.I.Preview);
        Assert.False(preview.IsEraser);
        Assert.Equal(1f, preview.Alpha);
        Assert.Equal(3, preview.Stroke.Points.Count);
        Assert.Equal(0, rig.Session.UndoCount);                  // nothing at the press (draw-tool-start fix)
        rig.I.PointerUp(points[2], 1);

        var stroke = new Stroke(ShapeTool.Pen, points.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToArray(), new SKColor(255, 0, 0), 6, false);
        var expected = Tools.CommitDrawing(stroke, 1, "Pen drawing")!;
        var layer = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal((1, "Pen drawing", 1.0), (layer.Id, layer.Name, layer.Scale));
        AssertSameLayerImage(expected, layer);
        Assert.Equal(1, rig.Session.SelectedLayerId);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(Tool.Pen, rig.Tools.Current);               // editor.js:528-540: no setTool
        Assert.Null(rig.I.Preview);
    }

    [Fact] // draw-pen-single-click fix (approved): a press with no movement draws a dot the size of the brush
    public void A_pen_press_without_movement_commits_a_brush_size_dot()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Pen);
        rig.Drag(1, new Point(200, 200));
        var stroke = new Stroke(ShapeTool.Pen, [new SKPoint(200, 200)], SKColor.Parse(BrushViewModel.DefaultColor), Tools.DefaultBrushSize, false);
        AssertSameLayerImage(Tools.CommitDrawing(stroke, 1, "Pen drawing")!, Assert.Single(rig.Session.Document.Layers));
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Theory] // draw-empty-no-layer, draw-tool-start fix: shapes need two points (editor.js:1205-1234); no empty step
    [InlineData(Tool.Line)]
    [InlineData(Tool.Rect)]
    [InlineData(Tool.Ellipse)]
    public void A_shape_click_draws_nothing_and_records_nothing(Tool tool)
    {
        var rig = new Rig();
        rig.Tools.Set(tool);
        var before = rig.Session.Document;
        rig.Drag(1, new Point(200, 200));
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.Equal(1, rig.Session.NextId);
    }

    [Fact] // R11 (draw-brush-controls quirk): shapes re-read color, size and fill live; the pen keeps its press style
    public void Shapes_use_the_press_and_the_latest_point_and_reread_the_brush()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Rect);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        var s = rig.I.Preview!.Stroke;
        Assert.Equal(new[] { new SKPoint(100, 100), new SKPoint(150, 150) }, s.Points);
        Assert.Equal((SKColor.Parse("#17d4fa"), 4f, false), (s.Color, s.Width, s.Fill));

        rig.Brush.Fill = true;                                   // the preview follows at once
        Assert.True(rig.I.Preview!.Stroke.Fill);
        rig.Brush.Color = "#00ff00";
        rig.I.PointerMove(new Point(200, 180), 1);
        s = rig.I.Preview!.Stroke;
        Assert.Equal(new[] { new SKPoint(100, 100), new SKPoint(200, 180) }, s.Points);
        Assert.Equal((SKColor.Parse("#00ff00"), true), (s.Color, s.Fill));
        rig.I.Cancel();

        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(10, 10), 1);
        rig.Brush.Color = "#0000ff";
        rig.I.PointerMove(new Point(20, 20), 1);
        Assert.Equal(SKColor.Parse("#00ff00"), rig.I.Preview!.Stroke.Color);   // fixed at the press (editor.js:1138)
    }

    [Fact] // wheel-sign-only-and-midstroke: the wheel mid-shape changes the live width and the committed shape
    public void The_wheel_mid_shape_changes_the_live_width_and_the_committed_shape_but_not_a_pen_stroke()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Rect);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(200, 180), 1);
        rig.I.Wheel(+1);
        rig.I.Wheel(+1);
        Assert.Equal((6, 6f), (rig.Brush.Size, rig.I.Preview!.Stroke.Width));
        rig.I.PointerUp(new Point(200, 180), 1);
        var stroke = new Stroke(ShapeTool.Rect, [new SKPoint(100, 100), new SKPoint(200, 180)], SKColor.Parse("#17d4fa"), 6, false);
        AssertSameLayerImage(Tools.CommitDrawing(stroke, 1, "Rect drawing")!, rig.L(1));
        Assert.Equal("Rect drawing", rig.L(1).Name);

        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(300, 300), 1);
        rig.I.Wheel(-1);
        rig.I.PointerMove(new Point(320, 300), 1);
        Assert.Equal((5, 6f), (rig.Brush.Size, rig.I.Preview!.Stroke.Width)); // pen width fixed at the press (editor.js:1139)
    }

    [Fact] // draw-tool-switch-midstroke fix (R9): the stroke is discarded, no layer, no step, the preview cleared
    public void A_tool_switch_mid_stroke_discards_it()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        rig.Tools.Set(Tool.Line);
        Assert.Equal((CanvasGesture.None, (LivePreview?)null), (rig.I.Gesture, rig.I.Preview));
        rig.I.PointerUp(new Point(150, 150), 1);                 // the release that follows does nothing
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // draw-release-outside-canvas fix (R10): a lost capture commits the stroke at the last point
    public void Capture_loss_mid_stroke_commits_it_at_the_last_point()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Line);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        rig.I.CaptureLost();
        var stroke = new Stroke(ShapeTool.Line, [new SKPoint(100, 100), new SKPoint(150, 150)], SKColor.Parse("#17d4fa"), 4, false);
        AssertSameLayerImage(Tools.CommitDrawing(stroke, 1, "Line drawing")!, rig.L(1));
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // canvas-coord-map: points are unclamped; a stroke released past the edge keeps its on-template part
    public void A_stroke_released_outside_the_template_keeps_the_part_inside()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Line);
        rig.I.PointerDown(new Point(500, 100), 1);
        rig.I.PointerMove(new Point(700, 100), 1);
        Assert.Equal(new SKPoint(700, 100), rig.I.Preview!.Stroke.Points[^1]);
        rig.I.PointerUp(new Point(700, 100), 1);
        var l = rig.L(1);
        Assert.True(l.X >= 0 && l.X + l.Image.Width <= TemplateSize.Width);
    }

    // ---- Erase ----

    [Fact] // eraser-selection-semantics fix (spec §5): refuse at the press with 4.1's text (editor.js:1270)
    public void The_eraser_with_no_selection_refuses_at_the_press_with_a_status_message()
    {
        var rig = new Rig(Box());
        rig.Tools.Set(Tool.Eraser);
        Assert.False(rig.I.PointerDown(new Point(120, 110), 1));
        Assert.Equal("Select a layer to erase from, then try again.", rig.Status.Current);
        Assert.Equal((CanvasGesture.None, (LivePreview?)null), (rig.I.Gesture, rig.I.Preview));
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // eraser-apply (Eraser.ApplyTo, never Replay41), render-live-preview (half alpha): the selection, not the top layer
    public void An_eraser_drag_erases_the_selected_layer_under_another_as_one_step()
    {
        var top = LayerOf(2, 100, 50) with { X = 100, Y = 100 };
        var rig = new Rig(Box(), top);
        rig.Session.Select(1);
        rig.Brush.Size = 20;
        rig.Tools.Set(Tool.Eraser);
        var before = rig.Session.Document;
        rig.I.PointerDown(new Point(120, 125), 1);
        rig.I.PointerMove(new Point(180, 125), 1);
        var preview = rig.I.Preview!;
        Assert.Equal((true, 0.5f, 20f, Eraser.StrokeColor), (preview.IsEraser, preview.Alpha, preview.Stroke.Width, preview.Stroke.Color));
        rig.I.PointerUp(new Point(180, 125), 1);

        var expected = Eraser.ApplyTo(before, 1, [new SKPoint(120, 125), new SKPoint(180, 125)], 20);
        AssertSameLayerImage(expected.Layers[0], rig.L(1));
        Assert.NotSame(before.Layers[0].Image, rig.L(1).Image);
        Assert.Same(top.Image, rig.L(2).Image);
        Assert.Equal((1, 1), (rig.Session.UndoCount, rig.Session.SelectedLayerId));
        Assert.Equal(Tool.Eraser, rig.Tools.Current);
        rig.Session.Undo();
        Assert.Same(before.Layers[0].Image, rig.L(1).Image);
    }

    [Fact] // eraser-apply: "Erase applies to the selected layer even if hidden" (R2 keeps it)
    public void The_eraser_erases_a_hidden_selected_layer()
    {
        var rig = new Rig(Box(visible: false));
        rig.Session.Select(1);
        rig.Brush.Size = 20;
        rig.Tools.Set(Tool.Eraser);
        var before = rig.L(1).Image;
        rig.Drag(1, new Point(120, 125), new Point(180, 125));
        Assert.NotSame(before, rig.L(1).Image);
        Assert.Equal(1, rig.Session.UndoCount);
    }

    [Fact] // R3 (M2a carry-forward, 4.1 kept): an eraser click erases nothing and records nothing
    public void Eraser_click_without_move_changes_nothing_and_records_no_step()
    {
        var rig = new Rig(Box());
        rig.Session.Select(1);
        rig.Tools.Set(Tool.Eraser);
        var before = rig.Session.Document;
        Assert.True(rig.I.PointerDown(new Point(120, 125), 1));
        rig.I.PointerUp(new Point(120, 125), 1);
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(0, rig.Session.UndoCount);
    }

    [Fact] // cursor-eraser, eraser-cursor, wheel-brush-size: the ring is the brush size; the wheel steps it by 1 in 1..50
    public void The_eraser_ring_follows_the_brush_and_the_wheel_but_a_stroke_keeps_its_press_size()
    {
        var rig = new Rig(Box());
        rig.Tools.Set(Tool.Eraser);
        Assert.Equal(CanvasCursor.Ring(4), rig.I.Cursor);
        for (int i = 0; i < 20; i++) rig.I.Wheel(+1);
        Assert.Equal((24, CanvasCursor.Ring(24)), (rig.Brush.Size, rig.I.Cursor));   // wheel-brush-size parityCheck
        for (int i = 0; i < 40; i++) rig.I.Wheel(+1);
        Assert.Equal(50, rig.Brush.Size);
        for (int i = 0; i < 60; i++) rig.I.Wheel(-1);
        Assert.Equal((1, CanvasCursor.Ring(1)), (rig.Brush.Size, rig.I.Cursor));

        rig.Session.Select(1);
        rig.I.PointerDown(new Point(120, 125), 1);
        rig.I.Wheel(+1);
        rig.I.PointerMove(new Point(130, 125), 1);
        Assert.Equal((2, 1f), (rig.Brush.Size, rig.I.Preview!.Stroke.Width));        // editor.js:1139: fixed at the press
        Assert.Equal(CanvasCursor.Ring(2), rig.I.Cursor);
        Assert.Equal(0, rig.Session.UndoCount);                                      // brush changes are never steps
    }

    // ---- Guards ----

    [Fact] // tools-set-tool: setTool refreshes the cursor (editor.js:477)
    public void A_tool_change_sets_its_default_cursor()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Eraser);
        Assert.Equal(CanvasCursorKind.EraserRing, rig.I.Cursor.Kind);
        rig.Tools.Set(Tool.Ellipse);
        Assert.Equal(CanvasCursor.Crosshair, rig.I.Cursor);
    }

    [Fact]
    public void Bad_arguments_are_refused()
    {
        var session = new EditorSession();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(new FakeColorPicker());
        var status = new FakeStatus();
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(null!, tools, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(session, null!, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(session, tools, null!, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(session, tools, brush, null!));
        var i = new CanvasInteraction(session, tools, brush, status);
        Assert.Throws<ArgumentOutOfRangeException>(() => i.PointerDown(new Point(1, 1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => i.PointerMove(new Point(1, 1), double.NaN));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'CanvasInteraction' could not be found` in `CanvasInteractionTests.cs`; `CS0246` for `CanvasGesture` may appear alongside.

**`EditorSession.Changing` (R7; critic MJ1).** The wheel burst keeps a gesture scope open for up to 400 ms after the last notch, and inside an open scope `EditorSession.Adopt` records nothing. Without a signal, a panel slider, Flip, Reset, a region paint or an import made in that window would fold into the wheel's step, and one Undo would revert both (spec §5: every document change is exactly one step). The session therefore raises `Changing` first from every change entry point, and `CanvasInteraction` closes its burst on it. The event is additive: no existing caller or test changes.

In `src/RBX15.App.Tests/EditorSessionTests.cs`, append before the class's closing brace:

Replace:

```csharp
    [Fact] // an unchanged gesture leaves redo alone
    public void Redo_during_an_unchanged_gesture_still_redoes()
```

with:

```csharp
    [Fact] // M3b R7 (critic MJ1): Changing comes first from Apply, ApplyWithSelection, BeginGesture, Undo and Redo, never from Select or the title
    public void Changing_is_raised_first_by_every_entry_point()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        var order = new List<string>();
        Document? atChanging = null;
        s.Changing += (_, _) => { order.Add("Changing"); atChanging = s.Document; };
        s.Changed += (_, _) => order.Add("Changed");

        var before = s.Document;
        Assert.True(s.Apply(d => d with { Layers = d.Layers.RemoveAt(1) }));
        Assert.Same(before, atChanging);   // raised before the change landed
        Assert.Equal(new[] { "Changing", "Changed" }, order);

        order.Clear();
        s.ApplyWithSelection(snap => snap with { SelectedLayerId = 1 });
        using (s.BeginGesture()) { }
        s.Undo();
        s.Redo();
        Assert.Equal(4, order.Count(o => o == "Changing"));

        s.Select(null);
        s.TemplateTitle = "shirt";
        Assert.Equal(4, order.Count(o => o == "Changing"));   // selection and title are not document changes
    }

    [Fact] // an unchanged gesture leaves redo alone
    public void Redo_during_an_unchanged_gesture_still_redoes()
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS1061: 'EditorSession' does not contain a definition for 'Changing'` in `EditorSessionTests.cs` (alongside the `CanvasInteraction` errors above).

In `src/RBX15.App/EditorSession.cs`, make five edits.

Edit A. Replace:

```csharp
    public event EventHandler<SessionChangedEventArgs>? Changed;
```

with:

```csharp
    public event EventHandler<SessionChangedEventArgs>? Changed;

    /// <summary>
    /// Raised first by every change entry point (<see cref="Apply"/>, <see cref="ApplyWithSelection"/>,
    /// <see cref="BeginGesture"/>, <see cref="Undo"/>, <see cref="Redo"/>), before it changes anything. The canvas
    /// closes an open wheel burst on it, so an unrelated edit is never folded into the burst's step (M3b R7).
    /// </summary>
    public event EventHandler? Changing;

    void RaiseChanging() => Changing?.Invoke(this, EventArgs.Empty);
```

Edit B (`Apply`). Replace:

```csharp
        ArgumentNullException.ThrowIfNull(change);
        var before = Snapshot;
        var after = change(before.Document) ?? throw new InvalidOperationException("A document change returned null.");
```

with:

```csharp
        ArgumentNullException.ThrowIfNull(change);
        RaiseChanging();
        var before = Snapshot;
        var after = change(before.Document) ?? throw new InvalidOperationException("A document change returned null.");
```

Edit C (`ApplyWithSelection`). Replace:

```csharp
        ArgumentNullException.ThrowIfNull(change);
        var before = Snapshot;
        var after = change(before) ?? throw new InvalidOperationException("A document change returned null.");
```

with:

```csharp
        ArgumentNullException.ThrowIfNull(change);
        RaiseChanging();
        var before = Snapshot;
        var after = change(before) ?? throw new InvalidOperationException("A document change returned null.");
```

Edit D (`BeginGesture`). Replace:

```csharp
        if (_gestureDepth++ == 0) _gestureStart = Snapshot;
```

with:

```csharp
        RaiseChanging();
        if (_gestureDepth++ == 0) _gestureStart = Snapshot;
```

Edit E (`Step`, which Undo and Redo share). Replace:

```csharp
        var what = RecordGesture();
        var current = Snapshot;
```

with:

```csharp
        RaiseChanging();
        var what = RecordGesture();
        var current = Snapshot;
```

Create `src/RBX15.App/Canvas/CanvasInteraction.cs`:

```csharp
using System.ComponentModel;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Canvas;

/// <summary>What the canvas is doing between a press and its release.</summary>
public enum CanvasGesture { None, Move, Resize, Rotate, Draw, Erase }

/// <summary>
/// The canvas's input state machine: 4.1's unified mouse handler (editor.js:1056-1320) over the
/// session, with v5's fixes. Points are template px (CanvasCoordinates.ToTemplate), so it is tested
/// with plain Points and no desktop input; TemplateCanvasHost only forwards to it.
/// <list type="bullet">
/// <item><b>Select</b> (editor.js:1064-1118, :1154-1194, :1239-1246): the press hit-tests through
///   SelectionGeometry.HitTest (rotate handle, corners TL TR BR BL, then the topmost visible box, else
///   deselect). A drag is one EditorSession gesture, opened at the press, closed at the release, the
///   capture loss or a cancel: one undo step iff the content changed (R7; select-move, select-resize-drag,
///   select-rotate-drag fixes). Each move applies from the press state, as 4.1 does.</item>
/// <item><b>Draw</b> (Pen, Line, Rect, Ellipse; editor.js:1120-1146, :1196-1236, :1249-1293): no undo at the
///   press (draw-tool-start fix). The preview is the stroke the commit draws (R12). Pen color and width are
///   fixed at the press; shapes re-read color, size and fill on every move and wheel step (R11). The release
///   commits Tools.CommitDrawing as a new selected top layer in one step, or nothing when nothing was drawn
///   (draw-empty-no-layer). The drawing tool stays (R8, editor.js:528-540).</item>
/// <item><b>Erase</b> (editor.js:1265-1283): only with a selection, else the press is refused with a status
///   message (eraser-selection-semantics fix). The release erases the layer selected at the press through
///   Eraser.ApplyTo, one step, none when nothing was erased (R3).</item>
/// <item><b>Wheel</b> (editor.js:1305-1320): sign only. Select scales the selection about its center, and a
///   burst of wheel events is one step (R6, R7). Other tools step the brush size (wheel-brush-size).</item>
/// <item><b>Cursors</b>: hover shows rotate (Grab), resize (ResizeNwse) and move (Move) over handles and
///   layers (select-hover-idle fix); a drag holds its cursor (canvas-cursor-states fix); the eraser shows
///   its ring at the brush size (cursor-eraser).</item>
/// </list>
/// UI thread only.
/// </summary>
public sealed class CanvasInteraction
{
    /// <summary>editor.js:1270's alert text, shown on the status strip at the press instead (spec §5).</summary>
    public const string NoLayerToErase = "Select a layer to erase from, then try again.";

    readonly EditorSession _session;
    readonly ToolsViewModel _tools;
    readonly BrushViewModel _brush;
    readonly IStatus _status;
    readonly List<SKPoint> _points = [];
    IDisposable? _drag;
    IDisposable? _wheel;
    bool _ownChange;   // true while Wheel itself changes the session, so its own Changing does not close its burst
    int _layerId;
    Point _press;
    Point _last;
    double _zoom = 1;
    (double X, double Y) _layerStart;
    (double X, double Y) _center;
    double _startOffset;
    double _startScale;
    double _startDistance;
    ShapeTool _shape;
    SKColor _penColor;
    float _penWidth;
    float _eraseSize;

    public CanvasInteraction(EditorSession session, ToolsViewModel tools, BrushViewModel brush, IStatus status)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _brush = brush ?? throw new ArgumentNullException(nameof(brush));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        Cursor = DefaultCursor();
        _tools.CurrentChanged += (_, _) => OnToolChanged();
        _brush.PropertyChanged += OnBrushChanged;
        _session.Changed += (_, e) =>
        {
            if (e.What.HasFlag(SessionChange.Selection)) EndWheelBurst();
        };
        // R7 (critic MJ1): any other change entry point (a panel field or slider, Flip, Reset, a region paint, an
        // import, Undo, Redo) closes the burst before it changes anything, so it is never folded into the wheel's step.
        _session.Changing += (_, _) =>
        {
            if (!_ownChange) EndWheelBurst();
        };
    }

    public CanvasGesture Gesture { get; private set; }

    public bool IsActive => Gesture != CanvasGesture.None;

    /// <summary>The live preview: non-null during Draw and Erase (editor.js:324-330).</summary>
    public LivePreview? Preview { get; private set; }

    public CanvasCursor Cursor { get; private set; }

    /// <summary>Raised when Gesture, Preview or Cursor changed.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// A left press at <paramref name="template"/>. True = a gesture started (the host captures the mouse).
    /// Closes any wheel burst. Select: a handle or a layer starts a drag (a layer is picked and selected
    /// first); empty canvas deselects and returns false. Draw tools start a stroke. The eraser with no
    /// selection shows <see cref="NoLayerToErase"/> and returns false.
    /// </summary>
    public bool PointerDown(Point template, double zoom)
    {
        CheckZoom(zoom);
        EndWheelBurst();
        if (IsActive) return false;   // a second press while a gesture runs cannot happen under capture
        _zoom = zoom;
        _press = _last = template;
        switch (_tools.Current)
        {
            case Tool.Select:
                return PressSelect(template, zoom);
            case Tool.Eraser:
                if (_session.SelectedLayerId is not int id)
                {
                    _status.Show(NoLayerToErase);
                    return false;
                }
                _layerId = id;
                _eraseSize = _brush.Size;   // fixed at the press (editor.js:1139)
                _points.Clear();
                _points.Add(ToSk(template));
                Update(CanvasGesture.Erase, LivePreview.Erase(_points.ToArray(), _eraseSize), CanvasCursor.Ring(_brush.Size));
                return true;
            default:
                _shape = ToolShapes.ShapeOf(_tools.Current)!.Value;
                _penColor = _brush.SkColor;   // the pen's style is fixed at the press (editor.js:1138-1141)
                _penWidth = _brush.Size;
                _points.Clear();
                _points.Add(ToSk(template));
                Update(CanvasGesture.Draw, LivePreview.Draw(CurrentStroke()), CanvasCursor.Crosshair);
                return true;
        }
    }

    bool PressSelect(Point t, double zoom)
    {
        var hit = SelectionGeometry.HitTest(_session.Document, _session.SelectedLayerId, t.X, t.Y, zoom);
        if (hit.Kind == SelectHitKind.None || hit.LayerId is not int id)
        {
            _session.Select(null);   // editor.js:1114-1117
            Update(CanvasGesture.None, null, HoverCursor(t, zoom));
            return false;
        }
        if (hit.Kind == SelectHitKind.Move)
        {
            _session.Select(id);     // selectLayer (editor.js:1105, :575-580) ...
            _tools.Set(Tool.Select); // ... which sets Select (already current here)
        }
        var layer = _session.Document.Layers.Find(l => l.Id == id)!;
        _layerId = id;
        _center = SelectionGeometry.Center(layer);
        CanvasGesture gesture;
        CanvasCursorKind cursor;
        switch (hit.Kind)
        {
            case SelectHitKind.Rotate:
                _startOffset = SelectionGeometry.RotateStartOffset(layer, t.X, t.Y);   // editor.js:1073
                gesture = CanvasGesture.Rotate;
                cursor = CanvasCursorKind.Grabbing;
                break;
            case SelectHitKind.Resize:
                _startScale = layer.Scale;                                        // editor.js:1092-1093
                _startDistance = Distance(t, _center);
                gesture = CanvasGesture.Resize;
                cursor = CanvasCursorKind.ResizeNwse;
                break;
            default:
                _layerStart = (layer.X, layer.Y);                                 // editor.js:1108-1109
                gesture = CanvasGesture.Move;
                cursor = CanvasCursorKind.Move;
                break;
        }
        _drag = _session.BeginGesture();
        Update(gesture, null, CanvasCursor.Of(cursor));
        return true;
    }

    /// <summary>
    /// A move. During a drag it applies from the press state (move, resize, rotate through session.Apply inside the
    /// gesture) or extends the stroke; a move to the last point does nothing. Idle, it only sets the hover cursor.
    /// </summary>
    public void PointerMove(Point template, double zoom)
    {
        CheckZoom(zoom);
        _zoom = zoom;
        if (!IsActive)
        {
            Update(CanvasGesture.None, null, HoverCursor(template, zoom));
            return;
        }
        if (template == _last) return;
        _last = template;
        switch (Gesture)
        {
            case CanvasGesture.Move:   // editor.js:1169-1171
                double x = _layerStart.X + (template.X - _press.X), y = _layerStart.Y + (template.Y - _press.Y);
                _session.Apply(d => LayerOps.MoveTo(d, _layerId, x, y));
                break;
            case CanvasGesture.Rotate: // editor.js:1172-1180, normalized (R5)
                double angle = SelectionGeometry.RotateAngle(_center, _startOffset, template.X, template.Y);
                _session.Apply(d => LayerOps.SetRotation(d, _layerId, angle));
                break;
            case CanvasGesture.Resize: // editor.js:1181-1190, clamped 1..500% (R6)
                double scale = SelectionGeometry.ResizeScale(_startScale, _startDistance, Distance(template, _center));
                _session.Apply(d => LayerOps.ScaleAboutCenter(d, _layerId, scale));
                break;
            case CanvasGesture.Draw:   // pen: every point; shapes: the press and the latest point (editor.js:1200-1234)
                if (_shape != ShapeTool.Pen && _points.Count > 1) _points[^1] = ToSk(template);
                else _points.Add(ToSk(template));
                Update(Gesture, LivePreview.Draw(CurrentStroke()), Cursor);
                break;
            case CanvasGesture.Erase:  // editor.js:1200-1203
                _points.Add(ToSk(template));
                Update(Gesture, LivePreview.Erase(_points.ToArray(), _eraseSize), Cursor);
                break;
        }
    }

    /// <summary>
    /// The release. The point is applied as a move first when it differs from the last one; then select drags close
    /// their gesture (one step iff changed), Draw commits its layer in one ApplyWithSelection, and Erase applies
    /// Eraser.ApplyTo in one Apply. Idle: nothing.
    /// </summary>
    public void PointerUp(Point template, double zoom)
    {
        CheckZoom(zoom);
        if (!IsActive) return;
        if (template != _last) PointerMove(template, zoom);
        Finish();
    }

    /// <summary>The pointer left the canvas. Idle only: the cursor goes back to the tool's default; a drag continues under capture.</summary>
    public void PointerLeave()
    {
        if (!IsActive) Update(CanvasGesture.None, null, DefaultCursor());
    }

    /// <summary>Mouse capture was lost mid-gesture: end it as a release at the last point (R10; draw-release-outside-canvas).</summary>
    public void CaptureLost()
    {
        if (IsActive) Finish();
    }

    /// <summary>
    /// R9 (draw-tool-switch-midstroke): a draw or erase stroke is discarded (no layer, no step, the preview cleared);
    /// a select drag ends as if released, keeping its change as one step.
    /// </summary>
    public void Cancel()
    {
        switch (Gesture)
        {
            case CanvasGesture.None:
                return;
            case CanvasGesture.Draw or CanvasGesture.Erase:
                _points.Clear();
                break;
            default:
                EndDrag();
                break;
        }
        Update(CanvasGesture.None, null, DefaultCursor());
    }

    /// <summary>
    /// One wheel event, sign only (wheel-sign-only-and-midstroke). Select with a selection: ScaleAboutCenter by
    /// WheelScale inside the wheel-burst gesture (R6, R7); nothing during a select drag. Other tools:
    /// BrushViewModel.StepSize, and a live shape re-renders at the new width (R11).
    /// </summary>
    public void Wheel(int sign)
    {
        if (_tools.Current != Tool.Select)
        {
            _brush.StepSize(sign);   // editor.js:1316-1318; OnBrushChanged refreshes a live shape and the ring
            return;
        }
        if (IsActive || _session.SelectedLayer is not { } layer) return;
        double scale = SelectionGeometry.WheelScale(layer.Scale, sign);
        _ownChange = true;
        try
        {
            _wheel ??= _session.BeginGesture();
            _session.Apply(d => LayerOps.ScaleAboutCenter(d, layer.Id, scale));
        }
        finally { _ownChange = false; }
    }

    /// <summary>Closes the open wheel-burst gesture, if any: the burst is recorded as one step iff it changed the scale.</summary>
    public void EndWheelBurst()
    {
        var wheel = _wheel;
        _wheel = null;
        wheel?.Dispose();
    }

    void Finish()
    {
        var gesture = Gesture;
        var points = _points.ToArray();
        var stroke = gesture == CanvasGesture.Draw ? CurrentStroke() : null;
        _points.Clear();
        if (gesture is CanvasGesture.Move or CanvasGesture.Rotate or CanvasGesture.Resize) EndDrag();
        Update(CanvasGesture.None, null, HoverCursor(_last, _zoom));
        if (stroke is not null)
        {
            // editor.js:1257-1285, :528-540: autocrop into a new top layer, selected; nothing drawn adds nothing.
            if (Tools.CommitDrawing(stroke, _session.NextId, Tools.DrawingName(_shape)) is { } layer)
                _session.ApplyWithSelection(s => LayerOps.AddDrawing(s, layer));
        }
        else if (gesture == CanvasGesture.Erase)
        {
            int id = _layerId;
            float size = _eraseSize;
            _session.Apply(d => Eraser.ApplyTo(d, id, points, size));
        }
    }

    void EndDrag()
    {
        var drag = _drag;
        _drag = null;
        drag?.Dispose();
    }

    /// <summary>The stroke drawn so far. Pen: its press style; shapes: the brush as it is now (R11).</summary>
    Stroke CurrentStroke() => _shape == ShapeTool.Pen
        ? new Stroke(ShapeTool.Pen, _points.ToArray(), _penColor, Tools.SanitizeWidth(_penWidth), false)
        : new Stroke(_shape, _points.ToArray(), _brush.SkColor, Tools.SanitizeWidth(_brush.Size), _brush.Fill);

    void OnToolChanged()
    {
        Cancel();
        EndWheelBurst();
        if (!IsActive) Update(CanvasGesture.None, null, DefaultCursor());
    }

    void OnBrushChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Gesture == CanvasGesture.Draw && _shape != ShapeTool.Pen)
            Update(Gesture, LivePreview.Draw(CurrentStroke()), Cursor);    // wheel-sign-only-and-midstroke
        if (Cursor.Kind == CanvasCursorKind.EraserRing) Update(Gesture, Preview, CanvasCursor.Ring(_brush.Size));
    }

    /// <summary>The idle cursor at a point (select-hover-idle fix; cursor-default; cursor-eraser).</summary>
    CanvasCursor HoverCursor(Point t, double zoom)
    {
        if (_tools.Current != Tool.Select) return DefaultCursor();
        return SelectionGeometry.HitTest(_session.Document, _session.SelectedLayerId, t.X, t.Y, zoom).Kind switch
        {
            SelectHitKind.Rotate => CanvasCursor.Of(CanvasCursorKind.Grab),
            SelectHitKind.Resize => CanvasCursor.Of(CanvasCursorKind.ResizeNwse),
            SelectHitKind.Move => CanvasCursor.Of(CanvasCursorKind.Move),
            _ => CanvasCursor.Crosshair,
        };
    }

    /// <summary>updateCursor (editor.js:456-459): the eraser's ring at the brush size, else crosshair.</summary>
    CanvasCursor DefaultCursor() => _tools.Current == Tool.Eraser ? CanvasCursor.Ring(_brush.Size) : CanvasCursor.Crosshair;

    void Update(CanvasGesture gesture, LivePreview? preview, CanvasCursor cursor)
    {
        if (gesture == Gesture && ReferenceEquals(preview, Preview) && cursor == Cursor) return;
        Gesture = gesture;
        Preview = preview;
        Cursor = cursor;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    static SKPoint ToSk(Point p) => new((float)p.X, (float)p.Y);

    static double Distance(Point p, (double X, double Y) c) => Math.Sqrt((p.X - c.X) * (p.X - c.X) + (p.Y - c.Y) * (p.Y - c.Y));

    static void CheckZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "zoom must be finite and positive");
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.CanvasInteractionTests"`
Expected: `Passed!  - Failed:     0, Passed:    33, Skipped:     0, Total:    33` for `RBX15.App.Tests.dll`.

If a rotate, resize or wheel number is off, the cause is in Task 1's `SelectionGeometry` or `LayerOps`, whose math these tests take from 4.1 (editor.js:1073, :1172-1190): fix the engine, never the expected value. If `A_pen_drag_previews_the_whole_path_and_commits_one_selected_layer_named_Pen_drawing` fails on pixels, the interaction built a different `Stroke` from the one the test builds (color, width or points); the commit and the test both call `Tools.CommitDrawing`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877`; Parity.Tests `Passed: 35`; App.Tests `Passed: 455, Failed: 0` (411 + 44: Step 2's 10, CanvasInteractionTests 33, EditorSessionTests +1).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/EditorSession.cs src/RBX15.App.Tests/EditorSessionTests.cs src/RBX15.App/Canvas/CanvasInteraction.cs src/RBX15.App.Tests/CanvasInteractionTests.cs
git commit -m "feat(app): CanvasInteraction, select drags, draw and erase strokes, wheel bursts, one undo step each"
```

- [ ] **Step 4: Canvas cursors (failing tests first)**

Serves `cursor-eraser` (fix: 4.1's 120 cap dropped, R15), `eraser-cursor`, `tools-cursor`, `canvas-cursor-states`, `cursor-default`; this task's rulings 6 and 7.

Create `src/RBX15.App.Tests/CanvasCursorsTests.cs`:

```csharp
using System.Windows.Input;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas cursors (canvas-cursor-states, tools-cursor, cursor-eraser, eraser-cursor): stock shapes, and
/// eraserCursorURI's ring (editor.js:443-454) at device resolution with 4.1's 120 px cap dropped (R15).
/// </summary>
public class CanvasCursorsTests
{
    static readonly SKColor Magenta = new(0xF2, 0x2F, 0x89);

    [Fact] // canvas-cursor-states: crosshair, move, grab, grabbing, nwse-resize (Grab and Grabbing are Hand: provisional)
    public void Stock_kinds_map_to_windows_cursors() => StaThread.Run(() =>
    {
        Assert.Same(Cursors.Cross, CanvasCursors.ToWpf(CanvasCursor.Crosshair, 1, 1));
        Assert.Same(Cursors.SizeAll, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.Move), 1, 1));
        Assert.Same(Cursors.Hand, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.Grab), 1, 1));
        Assert.Same(Cursors.Hand, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.Grabbing), 1, 1));
        Assert.Same(Cursors.SizeNWSE, CanvasCursors.ToWpf(CanvasCursor.Of(CanvasCursorKind.ResizeNwse), 1, 1));
    });

    [Theory] // cursor-eraser: r = max(4, size*zoom/2) CSS px, times the device scale; no 120 cap; the 256 px cursor limit
    [InlineData(4, 1, 1, 4)]       // min 4 (8 px diameter)
    [InlineData(1, 0.25, 1, 4)]
    [InlineData(20, 1, 1, 10)]
    [InlineData(20, 2, 1, 20)]
    [InlineData(20, 1, 1.5, 15)]
    [InlineData(50, 3, 1, 75)]
    [InlineData(300, 1, 1, 126)]   // (256 - 4) / 2 at 100%
    [InlineData(50, 3, 2, 124)]    // 150 device px wanted; (256 - 8) / 2 at 200%
    public void The_ring_radius_is_half_the_brush_times_zoom_at_least_4_and_fits_a_cursor(double size, double zoom, double scale, double radius)
    {
        double r = CanvasCursors.RingRadius(size, zoom, scale);
        Assert.Equal(radius, r, 9);
        Assert.True(CanvasCursors.RingSide(r, scale) <= CanvasCursors.MaxCursorSide);
    }

    [Fact] // eraserCursorURI: d = ceil(r)*2 + 4; black 3 px at .5 under the #f22f89 ring; a 1.5 px center dot; hotspot d/2
    public void The_ring_bitmap_is_4_1s_svg()
    {
        using var ring = CanvasCursors.RenderEraserRing(20, 1, 1);
        Assert.Equal((24, 24), (ring.Width, ring.Height));
        var center = ring.GetPixel(11, 11);
        Assert.True(center.Alpha > 200 && center.Red > center.Green, $"center dot {center}");
        Assert.Equal(0, ring.GetPixel(12, 6).Alpha);                // inside the ring, off the dot
        Assert.Equal(0, ring.GetPixel(0, 0).Alpha);                 // the corner
        Assert.True(ring.GetPixel(22, 12).Alpha >= 127, $"ring {ring.GetPixel(22, 12)}"); // r = 10 right of the center
        var ringPixels = Enumerable.Range(0, 24).SelectMany(y => Enumerable.Range(0, 24).Select(x => ring.GetPixel(x, y)))
            .Where(p => p.Alpha == 255).ToList();
        Assert.Contains(ringPixels, p => p.Red == Magenta.Red && p.Green == Magenta.Green && p.Blue == Magenta.Blue);
    }

    [Fact] // R15: the largest ring fills the 256 px cursor exactly at 100%
    public void The_largest_ring_is_256_px()
    {
        using var ring = CanvasCursors.RenderEraserRing(300, 1, 1);
        Assert.Equal(256, ring.Width);
    }

    [Fact] // tools-cursor fix: the ring follows zoom and the device scale; one cursor per size, cached
    public void Ring_cursors_are_cached_per_size_and_follow_zoom() => StaThread.Run(() =>
    {
        var a = CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1);
        Assert.Same(a, CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1));
        Assert.Same(a, CanvasCursors.ToWpf(CanvasCursor.Ring(10), 2, 1));   // the same screen size
        Assert.NotSame(a, CanvasCursors.ToWpf(CanvasCursor.Ring(20), 2, 1));
        Assert.NotSame(a, CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1.5));
    });

    [Fact]
    public void Bad_ring_inputs_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCursors.RingRadius(-1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCursors.RingRadius(4, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCursors.RingRadius(4, 1, double.NaN));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0103: The name 'CanvasCursors' does not exist in the current context` in `CanvasCursorsTests.cs`.

Create `src/RBX15.App/Controls/CanvasCursors.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;
using RBX15.App.Canvas;
using SkiaSharp;

namespace RBX15.App.Controls;

/// <summary>
/// WPF cursors for <see cref="CanvasCursor"/> (ledger canvas-cursor-states, tools-cursor, cursor-eraser).
/// <list type="bullet">
/// <item>Crosshair is Cross (editor.js:458), Move is SizeAll (CSS move, :1110), ResizeNwse is SizeNWSE
///   (:1094). Grab and Grabbing (:1161, :1074) have no stock Windows cursor; both are Hand. Provisional; M3b look
///   sign-off.</item>
/// <item>EraserRing is eraserCursorURI (editor.js:443-454) drawn at device resolution: a black 3 px ring at 0.5
///   opacity under a #f22f89 1.5 px dashed (4 3) ring, and a 1.5 px #f22f89 center dot, every length in CSS px
///   times the device scale; radius max(4, size*zoom/2) CSS px, hotspot at the center. 4.1's 120 px cap is
///   dropped (cursor-eraser fix): the only cap is the Win32 cursor size limit of 256 device px a side.</item>
/// </list>
/// Ring cursors are cached by side length and radius in hundredths of a device pixel. UI thread only.
/// </summary>
public static class CanvasCursors
{
    /// <summary>The largest cursor bitmap Windows shows, in device px a side.</summary>
    public const int MaxCursorSide = 256;

    static readonly Dictionary<(int Side, long Radius), Cursor> RingCache = [];

    public static Cursor ToWpf(CanvasCursor cursor, double zoom, double deviceScale) => cursor.Kind switch
    {
        CanvasCursorKind.Crosshair => Cursors.Cross,
        CanvasCursorKind.Move => Cursors.SizeAll,
        CanvasCursorKind.Grab or CanvasCursorKind.Grabbing => Cursors.Hand,
        CanvasCursorKind.ResizeNwse => Cursors.SizeNWSE,
        CanvasCursorKind.EraserRing => EraserRing(cursor.RingDiameter, zoom, deviceScale),
        _ => throw new ArgumentOutOfRangeException(nameof(cursor), cursor.Kind, "unknown cursor kind"),
    };

    /// <summary>The device-px margin around the ring (4.1: d = ceil(r)*2 + 4 at device scale 1, editor.js:445).</summary>
    internal static int Margin(double deviceScale) => (int)Math.Ceiling(2 * deviceScale);

    /// <summary>The ring radius in device px: max(4, size*zoom/2) CSS px times the scale, capped so the cursor fits 256 px.</summary>
    internal static double RingRadius(double diameterTemplatePx, double zoom, double deviceScale)
    {
        Check(diameterTemplatePx, zoom, deviceScale);
        double r = Math.Max(4, diameterTemplatePx * zoom / 2) * deviceScale;
        return Math.Min(r, MaxCursorSide / 2 - Margin(deviceScale));
    }

    /// <summary>The cursor bitmap's side in device px: 2*ceil(r) plus the margin on both sides.</summary>
    internal static int RingSide(double radius, double deviceScale) => 2 * (int)Math.Ceiling(radius) + 2 * Margin(deviceScale);

    static Cursor EraserRing(double diameterTemplatePx, double zoom, double deviceScale)
    {
        double r = RingRadius(diameterTemplatePx, zoom, deviceScale);
        var key = (RingSide(r, deviceScale), (long)Math.Round(r * 100));
        if (RingCache.TryGetValue(key, out var cached)) return cached;
        using var ring = RenderEraserRing(diameterTemplatePx, zoom, deviceScale);
        var cursor = CreateCursor(ring);
        RingCache[key] = cursor;
        return cursor;
    }

    /// <summary>The ring bitmap, premultiplied BGRA, square, the ring centered at side/2 (the hotspot).</summary>
    internal static SKBitmap RenderEraserRing(double diameterTemplatePx, double zoom, double deviceScale)
    {
        double r = RingRadius(diameterTemplatePx, zoom, deviceScale);
        int side = RingSide(r, deviceScale);
        float c = side / 2f, ds = (float)deviceScale, radius = (float)r;
        var bitmap = new SKBitmap(new SKImageInfo(side, side, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var magenta = new SKColor(0xF2, 0x2F, 0x89);
        using (var under = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ds, Color = SKColors.Black.WithAlpha(128) })
            canvas.DrawCircle(c, c, radius, under);
        using (var dash = SKPathEffect.CreateDash([4 * ds, 3 * ds], 0))
        using (var ringPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ds, Color = magenta, PathEffect = dash })
            canvas.DrawCircle(c, c, radius, ringPaint);
        using (var dot = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = magenta })
            canvas.DrawCircle(c, c, 1.5f * ds, dot);
        return bitmap;
    }

    /// <summary>A Win32 cursor from the ring: straight-alpha 32 bpp color, an empty mask, the hotspot at the center.</summary>
    static Cursor CreateCursor(SKBitmap ring)
    {
        int side = ring.Width;
        var straight = new byte[side * side * 4];
        var info = new SKImageInfo(side, side, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pin = GCHandle.Alloc(straight, GCHandleType.Pinned);
        IntPtr color = IntPtr.Zero, mask = IntPtr.Zero;
        try
        {
            using (var pixels = ring.PeekPixels())
                if (!pixels.ReadPixels(info, pin.AddrOfPinnedObject(), side * 4, 0, 0)) throw new InvalidOperationException("cannot read the ring pixels");
            color = CreateBitmap(side, side, 1, 32, pin.AddrOfPinnedObject());
            var maskBits = new byte[(side + 15) / 16 * 2 * side];
            var maskPin = GCHandle.Alloc(maskBits, GCHandleType.Pinned);
            try { mask = CreateBitmap(side, side, 1, 1, maskPin.AddrOfPinnedObject()); }
            finally { maskPin.Free(); }
            if (color == IntPtr.Zero || mask == IntPtr.Zero) throw new Win32Exception();
            var iconInfo = new IconInfo { fIcon = false, xHotspot = side / 2, yHotspot = side / 2, hbmMask = mask, hbmColor = color };
            IntPtr icon = CreateIconIndirect(ref iconInfo);
            if (icon == IntPtr.Zero) throw new Win32Exception();
            return CursorInteropHelper.Create(new IconHandle(icon));
        }
        finally
        {
            pin.Free();
            if (color != IntPtr.Zero) DeleteObject(color);
            if (mask != IntPtr.Zero) DeleteObject(mask);
        }
    }

    static void Check(double diameter, double zoom, double deviceScale)
    {
        if (!double.IsFinite(diameter) || diameter < 0) throw new ArgumentOutOfRangeException(nameof(diameter), diameter, "ring diameter must be finite and not negative");
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "zoom must be finite and positive");
        if (!double.IsFinite(deviceScale) || deviceScale <= 0) throw new ArgumentOutOfRangeException(nameof(deviceScale), deviceScale, "device scale must be finite and positive");
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    sealed class IconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public IconHandle(IntPtr handle) : base(ownsHandle: true) => SetHandle(handle);

        protected override bool ReleaseHandle() => DestroyIcon(handle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll", SetLastError = true)]
    static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, IntPtr bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteObject(IntPtr gdiObject);
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`. `SKBitmap` has no `ReadPixels`; the read goes through `PeekPixels()` (measured: `CS1061` otherwise).

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.CanvasCursorsTests"`
Expected: `Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877`; Parity.Tests `Passed: 35`; App.Tests `Passed: 468, Failed: 0` (411 + 57).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Controls/CanvasCursors.cs src/RBX15.App.Tests/CanvasCursorsTests.cs
git commit -m "feat(app): canvas cursors, the eraser ring at device resolution without 4.1's 120 px cap"
```

- [ ] **Step 5: The Tools and Brush groups in the Home tab (failing tests first)**

Serves `home-tool-buttons`, `home-brush-group`, `tools-set-tool` (the visible half), `draw-brush-controls`; the contract's "the Draw tool buttons, previously hidden, become visible"; R14.

In `src/RBX15.App.Tests/HomeTestKit.cs`, replace

```csharp
        return new(session, picker ?? new FakeColorPicker())
        {
            File = new FileGroupViewModel(
```

with

```csharp
        return new(session, picker ?? new FakeColorPicker())
        {
            Tools = new ToolsViewModel(),
            Brush = new BrushViewModel(picker ?? new FakeColorPicker()),
            File = new FileGroupViewModel(
```

In `src/RBX15.App.Tests/HomeRibbonViewTests.cs`, add `using RBX15.App.Canvas;` after `using System.Windows.Media;`. Replace the head of `Mode_settings_and_edit_bind_and_hidden_groups_are_absent`

```csharp
    [Fact] // the mode button, settings and edit bindings; contract: no Tools, Brush or Placement group in M3a
```

with

```csharp
    [Fact] // the mode button, settings and edit bindings; M3b shows Tools and Brush; Placement, Save work and Open stay hidden (M4)
```

and its group-label assertions

```csharp
        Assert.Equal(new[] { "MODE", "EDIT", "SETTINGS", "FILL STYLE", "TEMPLATE REGIONS" },
            texts.Where(t => t is "MODE" or "TOOLS" or "BRUSH" or "PLACEMENT" or "EDIT" or "SETTINGS" or "FILL STYLE" or "TEMPLATE REGIONS"));
        Assert.DoesNotContain(buttons, b => b.Content is "Draw" or "Fit Front" or "Save work" or "Open");
```

with

```csharp
        Assert.Equal(new[] { "MODE", "TOOLS", "BRUSH", "EDIT", "SETTINGS", "FILL STYLE", "TEMPLATE REGIONS" },
            texts.Where(t => t is "MODE" or "TOOLS" or "BRUSH" or "PLACEMENT" or "EDIT" or "SETTINGS" or "FILL STYLE" or "TEMPLATE REGIONS"));
        Assert.Contains(buttons, b => b.Content is "Draw");
        Assert.DoesNotContain(buttons, b => b.Content is "Fit Front" or "Save work" or "Open" or "Keys: ON");
```

Then add these two facts directly above `Undo_and_redo_tooltips_name_no_shortcut_yet` (its `[Fact] // fix round 1: hide-until-it-works` line):

```csharp
    [Fact] // home-tool-buttons, tools-set-tool: six buttons in editor.html:43-48 order; the active one is .btn-active and "Active" to UIA
    public void The_tool_buttons_show_4_1_labels_and_the_active_tool() => StaThread.Run(() =>
    {
        var home = Home(new EditorSession());
        var view = Load(home);
        var tools = Descendants<Button>(view).Where(b => b.DataContext is ToolButtonViewModel).ToArray();
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser" }, tools.Select(b => (string)b.Content));
        Assert.Equal(new[] { "Select/Move", "Freehand draw", "Line tool", "Rectangle", "Ellipse", "Eraser" }, tools.Select(b => (string)b.ToolTip)); // R14
        Assert.All(tools, b => Assert.True(b.Focusable && b.IsTabStop));
        Assert.Equal(new[] { "Active", "", "", "", "", "" }, tools.Select(AutomationProperties.GetItemStatus));
        var cyan = (Color)ColorConverter.ConvertFromString("#17D4FA");
        Assert.Equal(cyan, ((SolidColorBrush)tools[0].Foreground).Color);   // .btn-active text (styles.css:411)

        var draw = tools[1];
        draw.Command.Execute(null);
        view.UpdateLayout();
        Assert.Equal(Tool.Pen, home.Tools.Current);
        Assert.Equal(new[] { "", "Active", "", "", "", "" }, tools.Select(AutomationProperties.GetItemStatus));
        Assert.Equal(cyan, ((SolidColorBrush)draw.Foreground).Color);
        Assert.NotEqual(cyan, ((SolidColorBrush)tools[0].Foreground).Color);
        draw.Command.Execute(null);
        Assert.Equal(Tool.Select, home.Tools.Current);                       // tools-toggle-off
    });

    [Fact] // home-brush-group: #drawColor 26x24, #drawSize 1..50 value 4 width 60, Fill (editor.html:56-59); the picker opens under the swatch
    public void The_brush_group_binds_color_size_and_fill() => StaThread.Run(() =>
    {
        var picker = new FakeColorPicker().Answer("#ff0000");
        var home = Home(new EditorSession(), picker);
        var view = Load(home);
        var color = Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Draw color");
        var size = Descendants<Slider>(view).Single(s => AutomationProperties.GetName(s) == "Brush size");
        var fill = Descendants<CheckBox>(view).Single(c => (string)c.Content == "Fill");

        Assert.Equal((26.0, 24.0), (color.Width, color.Height));
        Assert.Equal((1.0, 50.0, 4.0, 60.0), (size.Minimum, size.Maximum, size.Value, size.Width));
        Assert.False(fill.IsChecked);

        color.Command.Execute(color.CommandParameter);
        Assert.Same(color, Assert.Single(picker.Anchors));
        Assert.Equal("#ff0000", home.Brush.Color);
        size.Value = 12;
        Assert.Equal(12, home.Brush.Size);
        home.Brush.StepSize(+1);
        Assert.Equal(13, size.Value);
        fill.IsChecked = true;
        Assert.True(home.Brush.Fill);
    });
```

In `src/RBX15.App.Tests/ThemeKeyGuardTests.cs`, replace

```csharp
        Assert.Equal(12, views.Count); // the 10 M3a view files, ColorPickerPopup.xaml and MainWindow.xaml
```

with

```csharp
        Assert.Equal(14, views.Count); // the 10 M3a view files, ColorPickerPopup.xaml, MainWindow.xaml, and M3b's ToolsGroup.xaml and BrushGroup.xaml
```

(Task 3's `LayerPropertiesPanel.xaml` makes it 15; that is Task 3's edit.)

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0117: 'HomeViewModel' does not contain a definition for 'Tools'` in `HomeTestKit.cs`; the same for `Brush` may appear alongside.

Replace `src/RBX15.App/ViewModels/HomeViewModel.cs` with:

```csharp
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab (editor.html:29-110): the Mode, Tools, Brush, Edit, File and Settings groups, then
/// Fill Style and Template Regions, which 4.1 kept on the Layout tab (editor.html:145-171) and which
/// M3a hosts here until M4 brings the Layout tab back. Placement stays hidden until it works
/// (contract: hide what does not work yet).
/// </summary>
public sealed class HomeViewModel
{
    public HomeViewModel(EditorSession session, IColorPicker colorPicker)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(colorPicker);
        Mode = new ModeGroupViewModel(session);
        FillStyle = new FillStyleViewModel(session, colorPicker);
        Regions = new RegionsGroupViewModel(session, FillStyle);
        Settings = new SettingsGroupViewModel(session);
        Edit = new EditGroupViewModel(session);
    }

    public ModeGroupViewModel Mode { get; }
    public RegionsGroupViewModel Regions { get; }
    public FillStyleViewModel FillStyle { get; }
    public SettingsGroupViewModel Settings { get; }
    public EditGroupViewModel Edit { get; }

    /// <summary>The Tools group (editor.html:41-52), shared with the canvas (M3b).</summary>
    public required ToolsViewModel Tools { get; init; }

    /// <summary>The Brush group (editor.html:53-62), shared with the canvas (M3b).</summary>
    public required BrushViewModel Brush { get; init; }

    /// <summary>The File group (Task 4): Add image and Download template.</summary>
    public required FileGroupViewModel File { get; init; }
}
```

Create `src/RBX15.App/Views/ToolsGroup.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.ToolsGroup"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- The Home tab's Tools group (editor.html:41-52; ledger home-tool-buttons). DataContext: ToolsViewModel.
         Six .btn.btn-sm.btn-secondary buttons in one row, gap 4 (.ribbon-group-items, styles.css:208). The active one
         is .btn-active (styles.css:408-412): cyan border, rgba(23,212,250,.1) background, cyan text; screen readers
         hear it as the item status "Active". 4.1's "Keys: ON" button (editor.html:49) is M4's keyboard and stays absent. -->
    <UserControl.Resources>
        <Style x:Key="ToolButton" TargetType="Button" BasedOn="{StaticResource SecondaryButtonSm}">
            <Setter Property="Content" Value="{Binding Label}" />
            <Setter Property="ToolTip" Value="{Binding ToolTip}" />
            <Setter Property="Command" Value="{Binding Command}" />
            <Setter Property="AutomationProperties.Name" Value="{Binding Label}" />
            <Setter Property="AutomationProperties.HelpText" Value="{Binding ToolTip}" />
            <Setter Property="Margin" Value="0,0,4,0" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsActive}" Value="True">
                    <Setter Property="BorderBrush" Value="{DynamicResource BrandCyanBrush}" />
                    <Setter Property="Background" Value="#1A17D4FA" />
                    <Setter Property="Foreground" Value="{DynamicResource BrandCyanBrush}" />
                    <Setter Property="AutomationProperties.ItemStatus" Value="Active" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <Border Style="{DynamicResource RibbonGroup}">
        <DockPanel>
            <TextBlock Style="{DynamicResource RibbonGroupLabel}" Text="TOOLS" />
            <ItemsControl x:Name="Buttons" ItemsSource="{Binding Buttons}" VerticalAlignment="Center" Focusable="False"
                          AutomationProperties.Name="Tools">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <StackPanel Orientation="Horizontal" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Button Style="{StaticResource ToolButton}" />
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </DockPanel>
    </Border>
</UserControl>
```

Create `src/RBX15.App/Views/ToolsGroup.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Home tab's Tools group. DataContext is a ToolsViewModel.</summary>
public partial class ToolsGroup : UserControl
{
    public ToolsGroup() => InitializeComponent();
}
```

Create `src/RBX15.App/Views/BrushGroup.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.BrushGroup"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- The Home tab's Brush group (editor.html:53-62; ledger home-brush-group, draw-brush-controls). DataContext:
         BrushViewModel. .ribbon-group-items.vert: a column, gap 4 (styles.css:208-209). Row 1 (editor.html:55): the
         color swatch and the size slider, gap 4, centered. Row 2 (editor.html:59): the Fill checkbox, 10px #888. -->
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="HomeStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
            <!-- #drawColor (editor.html:56): 26x24, no border, radius 4, the face shows the color. -->
            <Style x:Key="BrushSwatchButton" TargetType="Button" BasedOn="{StaticResource ColorSwatchButton}">
                <Setter Property="Height" Value="24" />
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{TemplateBinding Background}" CornerRadius="4" />
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
        </ResourceDictionary>
    </UserControl.Resources>

    <Border Style="{DynamicResource RibbonGroup}">
        <DockPanel>
            <TextBlock Style="{DynamicResource RibbonGroupLabel}" Text="BRUSH" />
            <StackPanel VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                    <Button x:Name="BrushColor" Style="{StaticResource BrushSwatchButton}" Background="{Binding Color}"
                            Command="{Binding PickColorCommand}" CommandParameter="{Binding RelativeSource={RelativeSource Self}}"
                            ToolTip="Draw color" AutomationProperties.Name="Draw color" Margin="0,0,4,0" />
                    <!-- #drawSize (editor.html:57): range 1..50, value 4, width 60. accent-color cyan has no WPF slider
                         equivalent in the theme; the stock slider is provisional; M3b look sign-off. -->
                    <Slider x:Name="BrushSize" Width="60" Minimum="1" Maximum="50" SmallChange="1" LargeChange="1"
                            TickFrequency="1" IsSnapToTickEnabled="True" Value="{Binding Size}" VerticalAlignment="Center"
                            ToolTip="Brush size" AutomationProperties.Name="Brush size" />
                </StackPanel>
                <CheckBox x:Name="BrushFill" Content="Fill" IsChecked="{Binding Fill}" FontSize="10" Foreground="#888888"
                          VerticalContentAlignment="Center" FocusVisualStyle="{DynamicResource FocusRing}"
                          AutomationProperties.Name="Fill shapes" />
            </StackPanel>
        </DockPanel>
    </Border>
</UserControl>
```

Create `src/RBX15.App/Views/BrushGroup.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Home tab's Brush group. DataContext is a BrushViewModel.</summary>
public partial class BrushGroup : UserControl
{
    public BrushGroup() => InitializeComponent();
}
```

Replace `src/RBX15.App/Views/HomeRibbon.xaml` with (Tools and Brush between Mode and Edit, editor.html:41-62):

```xml
<UserControl x:Class="RBX15.App.Views.HomeRibbon"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:RBX15.App.Views"
             AutomationProperties.Name="Home">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="HomeStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>

    <!-- .ribbon-pane.active (styles.css:197-198): padding 8px 12px, groups never shrink, horizontal scroll when narrow -->
    <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled" Focusable="False">
        <StackPanel x:Name="Groups" Orientation="Horizontal" Margin="12,8">

            <!-- Mode (editor.html:31-40): the group's inline padding is 0 14px -->
            <Border Style="{StaticResource RibbonGroup}" Padding="14,0">
                <DockPanel>
                    <TextBlock Style="{StaticResource RibbonGroupLabel}" Text="MODE" />
                    <Button x:Name="ModeToggle" DataContext="{Binding Mode}" Style="{StaticResource ModeToggleButton}"
                            Content="{Binding Label}" Command="{Binding ToggleCommand}"
                            AutomationProperties.Name="{Binding Label, StringFormat=Mode: {0}}" VerticalAlignment="Top" />
                </DockPanel>
            </Border>

            <!-- Tools (editor.html:41-52) and Brush (editor.html:53-62); Placement (editor.html:63-76) is M4 -->
            <views:ToolsGroup x:Name="Tools" DataContext="{Binding Tools}" />
            <views:BrushGroup x:Name="Brush" DataContext="{Binding Brush}" />

            <!-- Edit (editor.html:77-84): .btn .btn-sm .btn-secondary, a centered row with a 4px gap -->
            <Border Style="{StaticResource RibbonGroup}" DataContext="{Binding Edit}">
                <DockPanel>
                    <TextBlock Style="{StaticResource RibbonGroupLabel}" Text="EDIT" />
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Button x:Name="UndoButton" Content="Undo" ToolTip="Undo" Command="{Binding UndoCommand}"
                                Style="{DynamicResource SecondaryButton}" Padding="10,5" FontSize="11" Margin="0,0,4,0" />
                        <Button x:Name="RedoButton" Content="Redo" ToolTip="Redo" Command="{Binding RedoCommand}"
                                Style="{DynamicResource SecondaryButton}" Padding="10,5" FontSize="11" Margin="0,0,4,0" />
                        <Button x:Name="ClearButton" Content="Clear" Command="{Binding ClearCommand}"
                                Style="{DynamicResource SecondaryButton}" Padding="10,5" FontSize="11" />
                    </StackPanel>
                </DockPanel>
            </Border>

            <!-- File (editor.html:85-97): Add image and Download template; Save work and Open arrive with M4 -->
            <views:FileGroup DataContext="{Binding File}" />

            <!-- Settings (editor.html:98-107) -->
            <Border Style="{StaticResource RibbonGroup}" DataContext="{Binding Settings}">
                <DockPanel>
                    <TextBlock Style="{StaticResource RibbonGroupLabel}" Text="SETTINGS" />
                    <StackPanel VerticalAlignment="Top">
                        <TextBox x:Name="TemplateName" Style="{StaticResource TemplateNameBox}" Tag="Template name..."
                                 AutomationProperties.Name="Template name"
                                 Text="{Binding TemplateName, UpdateSourceTrigger=PropertyChanged}" />
                        <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                            <CheckBox x:Name="MaskBox" Content="Mask" Style="{StaticResource SettingsCheckBox}"
                                      IsChecked="{Binding ShowMask}" Margin="0,0,8,0" />
                            <CheckBox x:Name="GridBox" Content="Grid" Style="{StaticResource SettingsCheckBox}"
                                      IsChecked="{Binding ShowGrid}" />
                        </StackPanel>
                    </StackPanel>
                </DockPanel>
            </Border>

            <!-- Fill Style and Template Regions: 4.1's Layout tab groups (editor.html:145-171), on Home until M4 -->
            <views:FillStyleGroup x:Name="FillStyle" DataContext="{Binding FillStyle}" />
            <views:RegionsGrid x:Name="Regions" DataContext="{Binding Regions}" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Composition.Build` creates the tool and the brush now, so the Home tab has them. In `src/RBX15.App/Composition.cs`, add `using RBX15.App.Canvas;` above `using RBX15.App.Services;` (Step 6 uses `Tool`), and replace

```csharp
        var home = new HomeViewModel(session, colorPicker)
        {
            File = fileGroup,
        };
```

with

```csharp
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(colorPicker);
        var home = new HomeViewModel(session, colorPicker)
        {
            Tools = tools,
            Brush = brush,
            File = fileGroup,
        };
```

(Step 6 moves those two lines to the top of `Build` and prints the file in full.)

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`. A `CS9035` (required member `Tools` not set) names a `HomeViewModel` built outside `Composition` and `HomeTestKit`: route it through one of them.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.HomeRibbonViewTests|FullyQualifiedName~RBX15.App.Tests.ThemeKeyGuardTests"`
Expected: `Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12` for `RBX15.App.Tests.dll` (HomeRibbonViewTests 11, ThemeKeyGuardTests 1).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877`; Parity.Tests `Passed: 35`; App.Tests `Passed: 470, Failed: 0` (411 + 59).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/ViewModels/HomeViewModel.cs src/RBX15.App/Views/ToolsGroup.xaml src/RBX15.App/Views/ToolsGroup.xaml.cs src/RBX15.App/Views/BrushGroup.xaml src/RBX15.App/Views/BrushGroup.xaml.cs src/RBX15.App/Views/HomeRibbon.xaml src/RBX15.App/Composition.cs src/RBX15.App.Tests/HomeTestKit.cs src/RBX15.App.Tests/HomeRibbonViewTests.cs src/RBX15.App.Tests/ThemeKeyGuardTests.cs
git commit -m "feat(app): the Tools and Brush groups on the Home tab, with 4.1's buttons and brush controls"
```

- [ ] **Step 6: The canvas wiring: host rework, peer, coalescing, auto-revert (failing tests first)**

Serves interfaces R1 (the click path goes), R7 (the wheel timer), R8 (auto-revert), R10 (capture, left button); `canvas-mouse-button-agnostic`, `canvas-coord-map` (left button and capture), `select-move` (capture), `draw-release-outside-canvas`, `tools-auto-select-switch`, `render-pipeline-canvas-reset` (the cost), `tools-cursor` (zoom refresh), the canvas AutomationPeer; this task's rulings 4 and 5.

Replace `src/RBX15.App.Tests/CanvasViewModelTests.cs` with (the click fact goes; the overlay, the automation value and the constructor guards come in):

```csharp
using System.ComponentModel;
using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The zoom pill (zoom-controls, canvas-zoom-controls) and the document the canvas shows.</summary>
public class CanvasViewModelTests
{
    /// <summary>A canvas view-model with its own tool, brush and status strip.</summary>
    internal static CanvasViewModel Vm(EditorSession session, ToolsViewModel? tools = null) =>
        new(session, tools ?? new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus());

    [Fact] // editor.js:81 zoom = 1; editor.html:334 "100%"
    public void Starts_at_100_percent_showing_the_session_document()
    {
        var session = new EditorSession();
        var vm = Vm(session);
        Assert.Equal(1, vm.Zoom);
        Assert.Equal("100%", vm.ZoomLabel);
        Assert.Same(session.Document, vm.Document);
        Assert.True(vm.ZoomInCommand.CanExecute(null));
        Assert.True(vm.ZoomOutCommand.CanExecute(null));
    }

    [Fact] // zoom-controls parityCheck: the label for all 12 levels; editor.js:1490-1491
    public void Zoom_steps_by_a_quarter_from_25_to_300_percent_with_4_1_labels()
    {
        var vm = Vm(new EditorSession());
        var labels = new List<string>();
        while (vm.ZoomOutCommand.CanExecute(null)) vm.ZoomOutCommand.Execute(null);
        labels.Add(vm.ZoomLabel);
        while (vm.ZoomInCommand.CanExecute(null))
        {
            vm.ZoomInCommand.Execute(null);
            labels.Add(vm.ZoomLabel);
        }
        Assert.Equal(new[] { "25%", "50%", "75%", "100%", "125%", "150%", "175%", "200%", "225%", "250%", "275%", "300%" }, labels);
        Assert.Equal(CanvasViewModel.MaxZoom, vm.Zoom);
    }

    [Fact] // divergence zoom-pill-limits: a click that would clamp to a no-op is disabled instead
    public void Each_button_is_disabled_at_its_limit_and_comes_back()
    {
        var vm = Vm(new EditorSession());
        int inChanged = 0;
        vm.ZoomInCommand.CanExecuteChanged += (_, _) => inChanged++;
        for (int i = 0; i < 8; i++) vm.ZoomInCommand.Execute(null);
        Assert.Equal(3, vm.Zoom);
        Assert.False(vm.ZoomInCommand.CanExecute(null));

        vm.ZoomInCommand.Execute(null); // a programmatic Execute past the limit still clamps (editor.js:1490)
        Assert.Equal(3, vm.Zoom);

        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(2.75, vm.Zoom);
        Assert.True(vm.ZoomInCommand.CanExecute(null));
        Assert.True(inChanged > 0);

        for (int i = 0; i < 20; i++) vm.ZoomOutCommand.Execute(null);
        Assert.Equal(CanvasViewModel.MinZoom, vm.Zoom);
        Assert.False(vm.ZoomOutCommand.CanExecute(null));
    }

    [Fact] // zoom-controls: not in undo, not in the document
    public void Zoom_never_touches_the_session()
    {
        var session = new EditorSession();
        int changes = 0;
        session.Changed += (_, _) => changes++;
        var vm = Vm(session);
        var before = session.Document;
        vm.ZoomInCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(0, changes);
        Assert.Same(before, session.Document);
    }

    [Fact] // the host re-renders on Document (render-pipeline-canvas-reset); undo and redo raise Document too
    public void Document_follows_every_session_change_that_touches_it()
    {
        var session = new EditorSession();
        var vm = Vm(session);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Assert.Same(session.Document, vm.Document);
        Assert.True(vm.Document.ShowGrid);
        Assert.Equal(new[] { nameof(CanvasViewModel.Document) }, raised);

        raised.Clear();
        session.TemplateTitle = "Drip set"; // a Title-only change leaves the canvas alone
        Assert.Empty(raised);
    }

    [Fact] // drop-canvas: Task 4 sets DropHandler to ImportService.Import
    public void Drop_runs_the_handler_and_is_disabled_without_one()
    {
        var vm = Vm(new EditorSession());
        var drop = new CanvasDrop(new[] { @"C:\art\a.png", @"C:\art\b.png" }, new Point(120, 90));
        Assert.False(vm.DropCommand.CanExecute(drop));

        int changed = 0;
        vm.DropCommand.CanExecuteChanged += (_, _) => changed++;
        CanvasDrop? got = null;
        vm.DropHandler = d => got = d;
        Assert.Equal(1, changed);
        Assert.True(vm.DropCommand.CanExecute(drop));
        Assert.False(vm.DropCommand.CanExecute(null));

        vm.DropCommand.Execute(drop);
        Assert.Same(drop, got);
    }

    [Fact] // editor.js:362: the overlay only in the Select tool; the live preview while a stroke runs (render-live-preview)
    public void The_overlay_shows_the_selection_only_in_select_and_carries_the_live_preview()
    {
        var session = SessionWith(LayerOf(1) with { X = 100, Y = 100 });
        var tools = new ToolsViewModel();
        var vm = Vm(session, tools);
        Assert.Equal(ViewOverlay.None, vm.Overlay);
        session.Select(1);
        Assert.Equal(new ViewOverlay(1, null), vm.Overlay);
        tools.Set(Tool.Pen);
        Assert.Equal(ViewOverlay.None, vm.Overlay);
        vm.Interaction.PointerDown(new Point(10, 10), 1);
        vm.Interaction.PointerMove(new Point(30, 10), 1);
        Assert.Same(vm.Interaction.Preview, vm.Overlay.Preview);
        Assert.NotNull(vm.Overlay.Preview);
        Assert.Equal(CanvasCursor.Crosshair, vm.Cursor);
        tools.Set(Tool.Eraser);                                  // R9 discards the stroke
        Assert.Equal(ViewOverlay.None, vm.Overlay);
        Assert.Equal(CanvasCursor.Ring(4), vm.Cursor);
    }

    [Fact] // canvas automation peer: Value is the selected layer's name, or ""
    public void AutomationValue_is_the_selected_layers_name()
    {
        var session = SessionWith(LayerOf(1) with { Name = "Taft" });
        var vm = Vm(session);
        Assert.Equal("", vm.AutomationValue);
        session.Select(1);
        Assert.Equal("Taft", vm.AutomationValue);
        session.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Name = "Spider Based Bruh" }) });
        Assert.Equal("Spider Based Bruh", vm.AutomationValue);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        var session = new EditorSession();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(new FakeColorPicker());
        var status = new FakeStatus();
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(null!, tools, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(session, null!, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(session, tools, null!, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(session, tools, brush, null!));
    }
}
```

In `src/RBX15.App.Tests/CanvasViewTests.cs`, replace both `new CanvasViewModel(session)` and `new CanvasViewModel(new EditorSession())` with `CanvasViewModelTests.Vm(session)` and `CanvasViewModelTests.Vm(new EditorSession())`, and replace

```csharp
        Assert.Same(vm.ClickCommand, host.ClickCommand);
```

with

```csharp
        Assert.Same(vm.Interaction, host.Interaction);
        Assert.Equal((vm.Overlay, vm.Cursor, vm.AutomationValue), (host.Overlay, host.CanvasCursor, host.AutomationValue));
```

Replace `src/RBX15.App.Tests/ShellWiringTests.cs` with (the click fact keeps its five assertions, driven through the interaction; the auto-revert fact is new):

```csharp
using System.Collections.Immutable;
using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The wiring Composition.Build adds between the canvas, the tools and the session.</summary>
public class ShellWiringTests
{
    [Fact] // select-hit-test, select-deselect-click: editor.js:1100-1116, topmost visible layer under the press, else deselect
    public void A_canvas_click_selects_the_topmost_layer_under_it_and_empty_canvas_deselects() => StaThread.Run(() =>
    {
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        var image = surface.Snapshot();
        var session = new EditorSession(new Document
        {
            Layers = ImmutableList.Create(
                new Layer { Id = 1, Image = image, X = 100, Y = 100 },
                new Layer { Id = 2, Image = image, X = 120, Y = 120 },
                new Layer { Id = 3, Image = image, X = 300, Y = 300, Visible = false }),
        });
        var main = Composition.Build(session, () => null);
        void Click(double x, double y)
        {
            main.Canvas.Interaction.PointerDown(new Point(x, y), 1);
            main.Canvas.Interaction.PointerUp(new Point(x, y), 1);
        }

        Click(130, 130); // inside both: the top one wins
        Assert.Equal(2, session.SelectedLayerId);
        Click(105, 105); // only inside layer 1
        Assert.Equal(1, session.SelectedLayerId);
        Click(310, 310); // a hidden layer is never hit
        Assert.Null(session.SelectedLayerId);
        Click(105, 105);
        Click(10, 500);  // empty canvas deselects
        Assert.Null(session.SelectedLayerId);
        Assert.Equal(0, session.UndoCount); // selection is never an undo step
    });

    [Fact] // tools-auto-select-switch (R8): import and a list pick set Select; a drawing commit and undo keep the tool
    public void The_tool_reverts_to_select_on_import_and_layer_pick_but_not_on_a_drawing_or_undo() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var tools = main.Home.Tools;
        tools.Set(Tool.Eraser);                                  // the Home tab and the canvas share one tool
        Assert.Equal(CanvasCursorKind.EraserRing, main.Canvas.Cursor.Kind);

        tools.Set(Tool.Pen);
        main.Canvas.DropCommand.Execute(new CanvasDrop([files.Image("a.png", 40, 40, SKColors.Red)], new Point(300, 200)));
        Assert.Equal(Tool.Select, tools.Current);                // addLayer (editor.js:523)

        tools.Set(Tool.Pen);
        main.Canvas.Interaction.PointerDown(new Point(50, 50), 1);
        main.Canvas.Interaction.PointerMove(new Point(80, 60), 1);
        main.Canvas.Interaction.PointerUp(new Point(80, 60), 1);
        Assert.Equal("Pen drawing", session.SelectedLayer!.Name);
        Assert.Equal(Tool.Pen, tools.Current);                   // addDrawingLayer has no setTool (editor.js:528-540)

        main.Layers.Selected = main.Layers.Rows.Single(r => r.Name == "a");
        Assert.Equal(Tool.Select, tools.Current);                // selectLayer (editor.js:577)

        tools.Set(Tool.Rect);
        Assert.True(session.Undo());
        Assert.Equal(Tool.Rect, tools.Current);                  // undo leaves the tool alone
    });
}
```

In `src/RBX15.App.Tests/ImportServiceTests.cs`, replace

```csharp
        var canvas = new CanvasViewModel(session) { DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y)) };
```

with

```csharp
        var canvas = new CanvasViewModel(session, new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus()) { DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y)) };
```

and add this fact directly above `A_canvas_drop_centers_every_layer_on_the_drop_point` (its `[Fact] // drop-canvas:` line):

```csharp
    [Fact] // tools-auto-select-switch (R8): addLayer sets Select (editor.js:523); one event per import that added a layer
    public void LayersAdded_is_raised_once_per_import_that_added_layers()
    {
        using var files = new TestFiles();
        var import = new ImportService(new EditorSession(), new FakeStatus());
        int raised = 0;
        import.LayersAdded += (_, _) => raised++;
        import.Import([files.Image("a.png", 4, 4, SKColors.Red), files.Image("b.png", 4, 4, SKColors.Blue)]);
        Assert.Equal(1, raised);
        import.Import([files.Write("bad.svg", [1])]);           // refused: nothing added
        import.Import([]);
        Assert.Equal(1, raised);
    }
```

In `src/RBX15.App.Tests/LayersViewModelTests.cs`, add this fact directly above `Rows_list_the_top_layer_first_and_mark_the_selection` (its `[Fact] // top = highest z` line):

```csharp
    [Fact] // tools-auto-select-switch (R8): a user pick raises LayerPicked; a sync, the same row again or a deselect does not
    public void Picking_a_row_raises_LayerPicked_and_syncs_do_not() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        var vm = Vm(session);
        int picked = 0;
        vm.LayerPicked += (_, _) => picked++;
        session.Select(1);                    // the canvas or an undo moved the selection
        Assert.Equal(0, picked);
        vm.Selected = vm.Rows[0];             // the user picks B
        Assert.Equal((1, (int?)2), (picked, session.SelectedLayerId));
        vm.Selected = vm.Rows[0];
        vm.Selected = null;
        Assert.Equal(1, picked);
    });
```

In `src/RBX15.App.Tests/TemplateCanvasHostTests.cs`:

1. Replace the usings `using RBX15.App.Controls;` and `using RBX15.Engine;` with

```csharp
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
```

2. Delete the two facts `A_click_runs_the_click_command_with_the_template_point_and_a_drag_does_not` and `A_click_hit_tests_at_the_press_point_not_the_release_point` (R1; their behavior is `CanvasInteractionTests` and `ShellWiringTests` now).
3. In their place, directly above the `ThrowingData` class's `/// <summary>An IDataObject whose reads throw` comment, insert:

```csharp
    /// <summary>A host at <paramref name="zoom"/> over <paramref name="session"/>'s document, wired to a fresh interaction.</summary>
    static (TemplateCanvasHost Host, CanvasInteraction Interaction, ToolsViewModel Tools) Wired(EditorSession session, double zoom = 1)
    {
        var tools = new ToolsViewModel();
        var interaction = new CanvasInteraction(session, tools, new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var host = Host(session.Document, zoom);
        host.Interaction = interaction;
        session.Changed += (_, _) => host.Document = session.Document;
        return (host, interaction, tools);
    }

    static Layer Box() => new() { Id = 1, Image = Solid(100, 50, new SKColor(242, 47, 137)), X = 100, Y = 100 };

    [Fact] // select-move parityCheck: drag by (50,30) screen px at zoom 2 moves (25,15); canvas-coord-map: position / zoom
    public void Pointer_seams_map_by_zoom_and_drive_the_interaction() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(Box()) });
        var (host, interaction, _) = Wired(session, zoom: 2);
        Assert.True(host.HandlePointerDown(new Point(240, 220)));   // template (120, 110): on the layer
        Assert.Equal(CanvasGesture.Move, interaction.Gesture);
        host.HandlePointerMove(new Point(290, 250));
        host.HandlePointerUp(new Point(290, 250));
        var l = session.Document.Layers[0];
        Assert.Equal((125.0, 115.0), (l.X, l.Y));
        Assert.Equal(1, session.UndoCount);
        Assert.False(host.IsMouseCaptured);

        Assert.False(host.HandlePointerDown(new Point(20, 1000)));  // empty canvas: deselect, no gesture
        Assert.Null(session.SelectedLayerId);
        host.HandleLostCapture();                                   // idle: nothing
        host.HandlePointerLeave();
        Assert.Equal(1, session.UndoCount);

        host.Interaction = null;                                    // no interaction: input is ignored
        Assert.False(host.HandlePointerDown(new Point(240, 220)));
        host.HandlePointerMove(new Point(10, 10));
        host.HandlePointerUp(new Point(10, 10));
        host.HandleWheel(120);
    });

    [Fact] // canvas-mouse-button-agnostic fix (R10): only the left button reaches the interaction; right and middle stay unhandled
    public void Only_the_left_button_reaches_the_interaction() => StaThread.Run(() =>
    {
        var layer = new Layer { Id = 1, Image = Solid(100, 50, SKColors.Red), X = 0, Y = 0 };
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(layer) });
        var (host, interaction, _) = Wired(session);
        MouseButtonEventArgs Press(MouseButton button) =>
            new(Mouse.PrimaryDevice, Environment.TickCount, button) { RoutedEvent = Mouse.MouseDownEvent };

        foreach (var button in new[] { MouseButton.Right, MouseButton.Middle })
        {
            var e = Press(button);
            host.RaiseEvent(e);
            Assert.False(e.Handled);
            Assert.Equal((CanvasGesture.None, (int?)null), (interaction.Gesture, session.SelectedLayerId));
        }
        var left = Press(MouseButton.Left);
        host.RaiseEvent(left);                                      // an element outside a window reads the position as (0, 0)
        Assert.True(left.Handled);
        Assert.Equal((CanvasGesture.Move, (int?)1), (interaction.Gesture, session.SelectedLayerId));
    });

    [Fact] // wheel-sign-only-and-midstroke fix: sign only, Delta >= 0 grows; a 400 ms quiet period closes the burst (R7)
    public void The_wheel_is_sign_only_and_a_quiet_period_closes_the_burst() => StaThread.Run(() =>
    {
        var session = new EditorSession(new Document { Layers = ImmutableList.Create(Box()) });
        session.Select(1);
        var (host, _, _) = Wired(session);
        host.HandleWheel(120);
        host.HandleWheel(1);
        host.HandleWheel(0);
        Assert.Equal(Math.Pow(1.05, 3), session.Document.Layers[0].Scale, 12);
        host.HandleWheel(-480);
        Assert.Equal(Math.Pow(1.05, 2), session.Document.Layers[0].Scale, 12);
        Assert.True(host.WheelBurstPending);
        Assert.Equal(TimeSpan.FromMilliseconds(400), TemplateCanvasHost.WheelBurstDelay);
        Assert.Equal(0, session.UndoCount);
        host.FlushWheelBurst();                                     // the timer's tick
        Assert.False(host.WheelBurstPending);
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // render coalescing: N frame-input changes in one dispatcher turn render once, and the result is the last state
    public void Many_frame_input_changes_render_once() => StaThread.Run(() =>
    {
        var host = Host(Sample());
        _ = host.Frame;
        int before = host.RenderCount;
        for (int i = 0; i < 20; i++)
            host.Document = Sample() with { BgOpacity = i / 20.0 };
        host.Overlay = new ViewOverlay(1, null);
        Assert.Equal(before, host.RenderCount);                     // nothing rendered yet: one Rendering tick is pending
        var frame = Pixels(host.Frame!);
        Assert.Equal(before + 1, host.RenderCount);
        _ = host.Frame;
        Assert.Equal(before + 1, host.RenderCount);                 // nothing pending: no render
        var last = Sample() with { BgOpacity = 19 / 20.0 };
        Assert.Equal(EngineFrame(last, new ViewOptions(1, 1) { SelectedLayerId = 1 }), frame);
    });

    [Fact] // a drag changes only the frame's content: no layout pass per mouse event; a zoom resizes it
    public void Content_changes_keep_the_measure_and_a_zoom_invalidates_it() => StaThread.Run(() =>
    {
        var host = Host(Sample());
        Assert.True(host.IsMeasureValid);
        host.Document = Sample(grid: true);
        host.Overlay = new ViewOverlay(1, null);
        Assert.True(host.IsMeasureValid);
        host.Zoom = 2;
        Assert.False(host.IsMeasureValid);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal(new Size(1170, 1118), host.DesiredSize);
    });

    [Fact] // the overlay and the preview reach the engine: ViewOptions.SelectedLayerId and Preview (interfaces section 2)
    public void The_overlay_reaches_the_engine_frame() => StaThread.Run(() =>
    {
        var doc = Sample();
        var host = Host(doc, zoom: 1.5);
        var plain = Pixels(host.Frame!);
        var preview = LivePreview.Draw(new Stroke(ShapeTool.Line, [new SKPoint(20, 20), new SKPoint(300, 300)], SKColors.Lime, 8, false));
        host.Overlay = new ViewOverlay(1, preview);
        var shown = Pixels(host.Frame!);
        Assert.Equal(EngineFrame(doc, new ViewOptions(1.5, 1) { SelectedLayerId = 1, Preview = preview }), shown);
        Assert.NotEqual(plain, shown);
        host.Overlay = ViewOverlay.None;
        Assert.Equal(plain, Pixels(host.Frame!));
    });

    [Fact] // tools-cursor fix: the cursor follows the canvas cursor, and the eraser ring follows zoom and the device scale
    public void The_cursor_follows_the_canvas_cursor_zoom_and_device_scale() => StaThread.Run(() =>
    {
        var host = Host(new Document());
        Assert.Same(Cursors.Cross, host.Cursor);
        host.CanvasCursor = CanvasCursor.Of(CanvasCursorKind.Move);
        Assert.Same(Cursors.SizeAll, host.Cursor);
        host.CanvasCursor = CanvasCursor.Ring(20);
        var ring = host.Cursor;
        Assert.Same(CanvasCursors.ToWpf(CanvasCursor.Ring(20), 1, 1), ring);
        host.Zoom = 2;
        Assert.Same(CanvasCursors.ToWpf(CanvasCursor.Ring(20), 2, 1), host.Cursor);
        host.ApplyDpi(1.5);
        Assert.Same(CanvasCursors.ToWpf(CanvasCursor.Ring(20), 2, 1.5), host.Cursor);
    });
```

Create `src/RBX15.App.Tests/CanvasAutomationPeerTests.cs`:

```csharp
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The canvas automation peer (contract "Canvas accessibility"; spec §4: every control has an automation name).</summary>
public class CanvasAutomationPeerTests
{
    [Fact] // an Image named "Template canvas", not focusable, with a read-only Value
    public void The_peer_is_a_named_image_with_a_read_only_value() => StaThread.Run(() =>
    {
        var host = new TemplateCanvasHost { AutomationValue = "Taft" };
        var peer = Assert.IsType<TemplateCanvasHostAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host));
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal(("TemplateCanvasHost", "Template canvas"), (peer.GetClassName(), peer.GetName()));
        Assert.False(peer.IsKeyboardFocusable());
        var value = Assert.IsAssignableFrom<IValueProvider>(peer.GetPattern(PatternInterface.Value));
        Assert.Equal(("Taft", true), (value.Value, value.IsReadOnly));
        Assert.Throws<ElementNotEnabledException>(() => value.SetValue("x"));
        host.AutomationValue = null!;
        Assert.Equal("", value.Value);
    });

    [Fact] // the Value is the selected layer's name through CanvasView's binding, "" with nothing selected
    public void The_value_follows_the_selection() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1) with { Name = "drip_blood_front_v2" });
        var vm = new CanvasViewModel(session, new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        var host = (TemplateCanvasHost)view.FindName("Host");
        var value = (IValueProvider)UIElementAutomationPeer.CreatePeerForElement(host).GetPattern(PatternInterface.Value);
        Assert.Equal("", value.Value);
        session.Select(1);
        Assert.Equal("drip_blood_front_v2", value.Value);
        session.Select(null);
        Assert.Equal("", value.Value);
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL. The errors include `error CS1729: 'CanvasViewModel' does not contain a constructor that takes 4 arguments` (in `CanvasViewModelTests.cs` and the other test files that build the canvas view-model); `CS1061` for `TemplateCanvasHost.Interaction`, `HandlePointerDown`, `RenderCount`, `ImportService.LayersAdded`, `LayersViewModel.LayerPicked`, and `CS0246` for `TemplateCanvasHostAutomationPeer` may appear alongside.

Replace `src/RBX15.App/ViewModels/CanvasViewModel.cs` with:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The canvas area: the document the host shows, the zoom pill (canvas-zoom-controls, zoom-controls),
/// and the canvas input (<see cref="Interaction"/>). Zoom starts at 1 (editor.js:81), steps by 0.25
/// between 0.25 and 3 (editor.js:1490-1491), and is view state: never an undo step, never saved, 100%
/// on launch. Each command is disabled at its limit, where 4.1's button clamped to a no-op (divergence
/// zoom-pill-limits, Este's "never show a dead button").
/// <para>
/// <see cref="Overlay"/> is the host's frame input beyond the document: the selection overlay only while
/// the tool is Select (editor.js:362), and the live preview. <see cref="Cursor"/> mirrors the interaction's
/// cursor, and <see cref="AutomationValue"/> is the selected layer's name for the canvas automation peer.
/// </para>
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 3;
    public const double ZoomStep = 0.25;

    readonly EditorSession _session;
    readonly ToolsViewModel _tools;
    Action<CanvasDrop>? _dropHandler;

    public CanvasViewModel(EditorSession session, ToolsViewModel tools, BrushViewModel brush, IStatus status)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        Interaction = new CanvasInteraction(session, tools, brush, status);
        _document = session.Document;
        _overlay = CurrentOverlay();
        _cursor = Interaction.Cursor;
        _automationValue = SelectedName();
        session.Changed += OnSessionChanged;
        tools.CurrentChanged += (_, _) => Overlay = CurrentOverlay();
        Interaction.Changed += (_, _) =>
        {
            Overlay = CurrentOverlay();
            Cursor = Interaction.Cursor;
        };
    }

    /// <summary>The canvas input state machine the host forwards pointer and wheel events to.</summary>
    public CanvasInteraction Interaction { get; }

    /// <summary>The session's document, refreshed on every change that touches it (edits, undo, redo).</summary>
    [ObservableProperty]
    private Document _document;

    /// <summary>The selection overlay (Select tool only) and the live preview.</summary>
    [ObservableProperty]
    private ViewOverlay _overlay;

    /// <summary>The canvas cursor, mirrored from <see cref="Interaction"/>.</summary>
    [ObservableProperty]
    private CanvasCursor _cursor;

    /// <summary>The selected layer's name, or "" (the canvas peer's Value).</summary>
    [ObservableProperty]
    private string _automationValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    private double _zoom = 1;

    /// <summary><c>(zoom*100)+'%'</c> (editor.js:1490): "25%", "100%", "125%".</summary>
    public string ZoomLabel => (Zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>Called with every canvas drop the host accepts. Composition sets it to ImportService.Import.</summary>
    public Action<CanvasDrop>? DropHandler
    {
        get => _dropHandler;
        set
        {
            _dropHandler = value;
            DropCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    void ZoomIn() => Zoom = Math.Min(MaxZoom, Zoom + ZoomStep);

    bool CanZoomIn() => Zoom < MaxZoom;

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    void ZoomOut() => Zoom = Math.Max(MinZoom, Zoom - ZoomStep);

    bool CanZoomOut() => Zoom > MinZoom;

    [RelayCommand(CanExecute = nameof(CanDrop))]
    void Drop(CanvasDrop drop)
    {
        if (drop is not null) _dropHandler?.Invoke(drop);
    }

    bool CanDrop(CanvasDrop drop) => drop is not null && _dropHandler is not null;

    ViewOverlay CurrentOverlay() =>
        new(_tools.Current == Tool.Select ? _session.SelectedLayerId : null, Interaction.Preview);

    string SelectedName() => _session.SelectedLayer?.Name ?? "";

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (e.What.HasFlag(SessionChange.Document)) Document = _session.Document;
        if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0)
        {
            Overlay = CurrentOverlay();
            AutomationValue = SelectedName();
        }
    }
}
```

Replace `src/RBX15.App/Controls/TemplateCanvasHost.cs` with:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RBX15.App.Canvas;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Controls;

/// <summary>
/// Shows engine view frames in a Pbgra32 <see cref="WriteableBitmap"/>, one bitmap pixel per device pixel, and
/// forwards the pointer to <see cref="Interaction"/>. The engine draws every pixel, the selection overlay and the
/// live preview included; this control only blits (spec §1).
/// <list type="bullet">
/// <item>The frame is <c>Renderer.ViewSize(new ViewOptions(Zoom, DeviceScale, Document.ShowGrid))</c> pixels, and the
///   control measures <c>pixels / DeviceScale</c> DIPs. At 96 DPI, zoom 1 is 585x559 DIPs, the CSS size of 4.1's
///   canvas (canvas-coord-map).</item>
/// <item><b>Render coalescing</b> (M3a final-review carry-forward; render-pipeline-canvas-reset keeps its behavior,
///   not its cost). 4.1 re-rendered on every mouse event (editor.js:1191, :1235). Here a change to Document, Zoom,
///   Overlay or the DPI only marks the frame dirty and hooks one CompositionTarget.Rendering tick, so any number of
///   changes in one frame render once, through one persistent ViewRenderer (4.1's persistent offscreen,
///   editor.js:295-298). Reading Frame or measuring flushes a pending frame at once.</item>
/// <item>Bitmap scaling is nearest-neighbor, so a fractional layout offset never blurs the frame.</item>
/// <item>Files dropped on it run DropCommand with a <see cref="CanvasDrop"/> (drop-canvas). Every drop is handled, so
///   none falls through to the window (shell-external-drop-guard).</item>
/// <item><b>Pointer.</b> Left button only (canvas-mouse-button-agnostic fix). A press that starts a gesture captures
///   the mouse, so a drag goes on past the canvas edge and a release outside still ends it (select-move,
///   draw-release-outside-canvas fixes); a lost capture ends it like a release. The wheel is always handled
///   (editor.js:1306 preventDefault) and a 400 ms quiet period closes a wheel burst (interfaces R7).</item>
/// <item>The cursor is <see cref="CanvasCursor"/> through <see cref="CanvasCursors.ToWpf"/> at the current zoom and
///   device scale, so the eraser ring follows zoom too (tools-cursor fix).</item>
/// <item>Its automation peer is an Image named "Template canvas" whose Value is <see cref="AutomationValue"/>.</item>
/// </list>
/// </summary>
public sealed class TemplateCanvasHost : FrameworkElement
{
    /// <summary>The quiet time after the last wheel event that closes a wheel-scale burst (interfaces R7).</summary>
    public static readonly TimeSpan WheelBurstDelay = TimeSpan.FromMilliseconds(400);

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(Document), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnFrameInputChanged));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(1.0, OnZoomChanged),
        value => value is double z && double.IsFinite(z) && z > 0);

    public static readonly DependencyProperty DropCommandProperty = DependencyProperty.Register(
        nameof(DropCommand), typeof(ICommand), typeof(TemplateCanvasHost));

    public static readonly DependencyProperty InteractionProperty = DependencyProperty.Register(
        nameof(Interaction), typeof(CanvasInteraction), typeof(TemplateCanvasHost));

    public static readonly DependencyProperty OverlayProperty = DependencyProperty.Register(
        nameof(Overlay), typeof(ViewOverlay), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnFrameInputChanged));

    public static readonly DependencyProperty CanvasCursorProperty = DependencyProperty.Register(
        nameof(CanvasCursor), typeof(CanvasCursor), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(Canvas.CanvasCursor.Crosshair, (d, _) => ((TemplateCanvasHost)d).UpdateCursor()));

    public static readonly DependencyProperty AutomationValueProperty = DependencyProperty.Register(
        nameof(AutomationValue), typeof(string), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata("", OnAutomationValueChanged));

    readonly DispatcherTimer _wheelTimer;
    WriteableBitmap? _frame;
    double _deviceScale;
    ViewRenderer? _renderer;
    bool _framePending;
    bool _renderingHooked;

    public TemplateCanvasHost()
    {
        _deviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        AllowDrop = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        AutomationProperties.SetName(this, "Template canvas");
        _wheelTimer = new DispatcherTimer { Interval = WheelBurstDelay };
        _wheelTimer.Tick += (_, _) => FlushWheelBurst();
        UpdateCursor();
        // A window that opens on a monitor whose DPI differs from the one the control was built for.
        Loaded += (_, _) =>
        {
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (scale != _deviceScale) ApplyDpi(scale);
            else if (_framePending) RequestFrame();
        };
        // The persistent renderer's native surfaces go with the control; a later frame recreates them.
        Unloaded += (_, _) =>
        {
            UnhookRendering();
            FlushWheelBurst();
            _renderer?.Dispose();
            _renderer = null;
        };
    }

    public Document? Document
    {
        get => (Document?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public ICommand? DropCommand
    {
        get => (ICommand?)GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    /// <summary>The canvas input state machine the pointer and wheel are forwarded to; null ignores input.</summary>
    public CanvasInteraction? Interaction
    {
        get => (CanvasInteraction?)GetValue(InteractionProperty);
        set => SetValue(InteractionProperty, value);
    }

    /// <summary>The selection overlay and live preview the engine draws into the frame; null draws neither.</summary>
    public ViewOverlay? Overlay
    {
        get => (ViewOverlay?)GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    /// <summary>The canvas cursor; the control's Cursor is <see cref="CanvasCursors.ToWpf"/> of it.</summary>
    public CanvasCursor CanvasCursor
    {
        get => (CanvasCursor)GetValue(CanvasCursorProperty);
        set => SetValue(CanvasCursorProperty, value);
    }

    /// <summary>The automation peer's Value: the selected layer's name, or "".</summary>
    public string AutomationValue
    {
        get => (string?)GetValue(AutomationValueProperty) ?? "";
        set => SetValue(AutomationValueProperty, value);
    }

    /// <summary>The bitmap on screen, rendered now if a frame is pending; null while there is no document. Read by tests.</summary>
    internal WriteableBitmap? Frame
    {
        get
        {
            FlushFrame();
            return _frame;
        }
    }

    /// <summary>Device pixels per DIP that the frame is rendered for.</summary>
    internal double DeviceScale => _deviceScale;

    /// <summary>How many frames this control has rendered. N frame-input changes in one dispatcher turn render once.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>True while a wheel burst waits for its quiet period.</summary>
    internal bool WheelBurstPending => _wheelTimer.IsEnabled;

    static void OnFrameInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TemplateCanvasHost)d).RequestFrame();

    static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        host.RequestFrame();
        host.UpdateCursor();
    }

    static void OnAutomationValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (UIElementAutomationPeer.FromElement((TemplateCanvasHost)d) is TemplateCanvasHostAutomationPeer peer)
            peer.RaiseValueChanged((string?)e.OldValue ?? "", (string?)e.NewValue ?? "");
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TemplateCanvasHostAutomationPeer(this);

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplyDpi(newDpi.DpiScaleX);
    }

    /// <summary>Re-renders for a new device scale. OnDpiChanged calls it; tests call it to pin a scale.</summary>
    internal void ApplyDpi(double deviceScale)
    {
        if (!double.IsFinite(deviceScale) || deviceScale <= 0) throw new ArgumentOutOfRangeException(nameof(deviceScale), deviceScale, "device scale must be finite and positive");
        _deviceScale = deviceScale;
        RequestFrame();
        InvalidateMeasure();
        UpdateCursor();
    }

    /// <summary>
    /// Marks the frame dirty and hooks one CompositionTarget.Rendering tick to render it. The measure is invalidated
    /// only when the frame's pixel size changes, so a drag, which changes only the content, never forces a layout
    /// pass per mouse event.
    /// </summary>
    void RequestFrame()
    {
        _framePending = true;
        if (!_renderingHooked)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderingHooked = true;
        }
        if (TargetPixelSize() != FramePixelSize()) InvalidateMeasure();
    }

    void OnRendering(object? sender, EventArgs e)
    {
        UnhookRendering();
        FlushFrame();
    }

    void UnhookRendering()
    {
        if (!_renderingHooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _renderingHooked = false;
    }

    /// <summary>Renders the pending frame, if any, once through the persistent <see cref="ViewRenderer"/>.</summary>
    void FlushFrame()
    {
        if (!_framePending) return;
        _framePending = false;
        UnhookRendering();   // a lazy flush (Frame, MeasureOverride) leaves no stale Rendering hook behind (critic mn3)
        if (Document is not { } doc)
        {
            if (_frame is null) return;
            _frame = null;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var overlay = Overlay;
        var options = new ViewOptions(Zoom, _deviceScale, doc.ShowGrid)
        {
            SelectedLayerId = overlay?.SelectedLayerId,
            Preview = overlay?.Preview,
        };
        var size = Renderer.ViewSize(options);
        double dpi = 96 * _deviceScale;
        if (_frame is null || _frame.PixelWidth != size.Width || _frame.PixelHeight != size.Height || _frame.DpiX != dpi)
        {
            _frame = new WriteableBitmap(size.Width, size.Height, dpi, dpi, PixelFormats.Pbgra32, null);
            InvalidateMeasure();
        }

        _frame.Lock();
        try
        {
            // Pbgra32 is premultiplied BGRA, which is Skia's kN32 on Windows: the Renderer's precondition.
            var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var surface = SKSurface.Create(info, _frame.BackBuffer, _frame.BackBufferStride)
                ?? throw new InvalidOperationException("cannot wrap the frame bitmap"))
            {
                (_renderer ??= new ViewRenderer()).Render(surface.Canvas, doc, options);
                surface.Canvas.Flush();
            }
            _frame.AddDirtyRect(new Int32Rect(0, 0, size.Width, size.Height));
        }
        finally
        {
            _frame.Unlock();
        }
        RenderCount++;
        InvalidateVisual();
    }

    (int Width, int Height) TargetPixelSize()
    {
        if (Document is null) return (0, 0);
        var size = Renderer.ViewSize(new ViewOptions(Zoom, _deviceScale));
        return (size.Width, size.Height);
    }

    (int Width, int Height) FramePixelSize() => _frame is null ? (0, 0) : (_frame.PixelWidth, _frame.PixelHeight);

    Size FrameSize => _frame is null ? default : new Size(_frame.PixelWidth / _deviceScale, _frame.PixelHeight / _deviceScale);

    protected override Size MeasureOverride(Size availableSize)
    {
        FlushFrame();
        return FrameSize;
    }

    protected override Size ArrangeOverride(Size finalSize) => FrameSize;

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_frame is not null) drawingContext.DrawImage(_frame, new Rect(FrameSize));
    }

    void UpdateCursor() => Cursor = CanvasCursors.ToWpf(CanvasCursor, Zoom, _deviceScale);

    // ---- pointer: the overrides only forward to the seams below, which tests call directly ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Interaction is null) return;
        // A started gesture that could not capture (no visible source) would never see a release or a
        // LostMouseCapture and would hold the session in a gesture: end it now (critic mn2). The seam stays as is.
        if (HandlePointerDown(e.GetPosition(this)) && !IsMouseCaptured) Interaction?.CaptureLost();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        HandlePointerMove(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (Interaction is null) return;
        HandlePointerUp(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        HandleLostCapture();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HandlePointerLeave();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        HandleWheel(e.Delta);
        e.Handled = true;
    }

    /// <summary>A left press at a point in the control (DIPs). Captures the mouse when a gesture starts.</summary>
    internal bool HandlePointerDown(Point inHost)
    {
        if (Interaction is not { } interaction) return false;
        bool started = interaction.PointerDown(CanvasCoordinates.ToTemplate(inHost, Zoom), Zoom);
        if (started) CaptureMouse();
        return started;
    }

    internal void HandlePointerMove(Point inHost) => Interaction?.PointerMove(CanvasCoordinates.ToTemplate(inHost, Zoom), Zoom);

    /// <summary>The left release: ends the gesture, then releases capture (whose LostMouseCapture then finds nothing active).</summary>
    internal void HandlePointerUp(Point inHost)
    {
        Interaction?.PointerUp(CanvasCoordinates.ToTemplate(inHost, Zoom), Zoom);
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    internal void HandleLostCapture() => Interaction?.CaptureLost();

    internal void HandlePointerLeave() => Interaction?.PointerLeave();

    /// <summary>One wheel event: sign only (WPF Delta &gt;= 0 is 4.1's deltaY &lt;= 0, which grows), then restart the burst timer.</summary>
    internal void HandleWheel(int delta)
    {
        Interaction?.Wheel(delta >= 0 ? 1 : -1);
        _wheelTimer.Stop();
        _wheelTimer.Start();
    }

    /// <summary>Ends a wheel burst now: the timer's tick, Unloaded, and tests.</summary>
    internal void FlushWheelBurst()
    {
        _wheelTimer.Stop();
        Interaction?.EndWheelBurst();
    }

    protected override void OnDragEnter(DragEventArgs e) => ShowDropEffect(e);

    protected override void OnDragOver(DragEventArgs e) => ShowDropEffect(e);

    void ShowDropEffect(DragEventArgs e)
    {
        e.Effects = DropEffect(e.Data, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// The cursor a drag shows over the canvas: Copy only when the data carries files and DropCommand
    /// would accept them, otherwise None, so the cursor never promises a drop that does nothing.
    /// </summary>
    internal DragDropEffects DropEffect(IDataObject data, Point inHost)
    {
        if (Files(data) is not { } paths) return DragDropEffects.None;
        var drop = new CanvasDrop(paths, CanvasCoordinates.ToTemplate(inHost, Zoom));
        return DropCommand is { } command && command.CanExecute(drop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        HandleDrop(e.Data, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// The drop logic without DragEventArgs, which tests cannot construct. Runs DropCommand and returns
    /// true when <paramref name="data"/> carries at least one file and the command accepts the drop.
    /// Text, a browser image with no file behind it, or an empty file list does nothing.
    /// </summary>
    internal bool HandleDrop(IDataObject data, Point inHost)
    {
        if (Files(data) is not { } paths) return false;
        var drop = new CanvasDrop(paths, CanvasCoordinates.ToTemplate(inHost, Zoom));
        if (DropCommand is not { } command || !command.CanExecute(drop)) return false;
        command.Execute(drop);
        return true;
    }

    /// <summary>
    /// The dropped file paths, or null. Shell virtual items and malformed OLE data can throw from
    /// GetData (COMException and others); an unreadable drag is not a file drag, so it yields null.
    /// </summary>
    internal static string[]? Files(IDataObject? data)
    {
        if (data is null) return null;
        try
        {
            return data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths
                ? paths : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

Create `src/RBX15.App/Controls/TemplateCanvasHostAutomationPeer.cs`:

```csharp
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace RBX15.App.Controls;

/// <summary>
/// The canvas's UI Automation face (spec §4 accessibility; contract "Canvas accessibility"). 4.1's
/// &lt;canvas&gt; had no role, name or value (a11y-focus-rings). v5: an Image named "Template canvas"
/// (AutomationProperties.Name), bounds from the element, not keyboard focusable, with a read-only Value
/// pattern whose value is the selected layer's name, or "" with nothing selected.
/// </summary>
public sealed class TemplateCanvasHostAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
{
    public TemplateCanvasHostAutomationPeer(TemplateCanvasHost owner) : base(owner) { }

    TemplateCanvasHost Host => (TemplateCanvasHost)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => nameof(TemplateCanvasHost);

    protected override bool IsKeyboardFocusableCore() => false;

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

    public bool IsReadOnly => true;

    public string Value => Host.AutomationValue;

    /// <summary>Read-only: the layer is renamed in the properties panel, not through the canvas.</summary>
    public void SetValue(string value) => throw new ElementNotEnabledException();

    /// <summary>Tells listening clients (Narrator, Accessibility Insights) that the selected layer changed.</summary>
    internal void RaiseValueChanged(string oldValue, string newValue)
    {
        if (ListenerExists(AutomationEvents.PropertyChanged))
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, newValue);
    }
}
```

Replace `src/RBX15.App/Views/CanvasView.xaml` with (the host binds the interaction, overlay, cursor and automation value; `ClickCommand` is gone):

```xml
<UserControl x:Class="RBX15.App.Views.CanvasView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:RBX15.App.Controls"
             UseLayoutRounding="True"
             Background="{DynamicResource Ink950Brush}">
    <!-- .canvas-area (styles.css:441-450): ink-950, the frame centered. v5 adds scrollbars when the
         frame is larger than the area (canvas-zoom-clipped-no-pan); 4.1 clipped it (overflow: hidden). -->
    <UserControl.Resources>
        <!-- .zoom-controls background rgba(15,31,49,.85) (styles.css:546). No backdrop blur in WPF (canvas-zoom-controls). -->
        <SolidColorBrush x:Key="ZoomPillBackground" Color="#D90F1F31" />
        <!-- .zoom-controls button (styles.css:552-559): no background or border, fg-1, 16px, padding 2px 6px.
             Disabled at a limit with .btn:disabled's opacity .4 (styles.css:414-416). -->
        <Style x:Key="ZoomButton" TargetType="Button">
            <Setter Property="Foreground" Value="{DynamicResource Fg1Brush}" />
            <Setter Property="FontFamily" Value="{DynamicResource FontBody}" />
            <Setter Property="FontSize" Value="16" />
            <Setter Property="Padding" Value="6,2" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="Transparent" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Opacity" Value="0.4" />
                                <Setter Property="Cursor" Value="Arrow" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </UserControl.Resources>

    <Grid>
        <ScrollViewer x:Name="Scroller" Style="{DynamicResource ThemedScrollViewer}"
                      HorizontalScrollBarVisibility="Auto"
                      VerticalScrollBarVisibility="Auto"
                      Focusable="False">
            <!-- .canvas-wrap (styles.css:451-456): 1px border-1 and shadow-2 (0 4px 16px rgba(0,0,0,.45)).
                 The shadow sits on a sibling, so no effect ever re-rasterizes the frame. -->
            <Grid HorizontalAlignment="Center" VerticalAlignment="Center">
                <Border Background="{DynamicResource Ink950Brush}">
                    <Border.Effect>
                        <DropShadowEffect Color="Black" Opacity="0.45" BlurRadius="16" ShadowDepth="4" Direction="270" />
                    </Border.Effect>
                </Border>
                <Border BorderBrush="{DynamicResource Border1Brush}" BorderThickness="1">
                    <controls:TemplateCanvasHost x:Name="Host"
                                                 Document="{Binding Document}"
                                                 Zoom="{Binding Zoom}"
                                                 DropCommand="{Binding DropCommand}"
                                                 Interaction="{Binding Interaction}"
                                                 Overlay="{Binding Overlay}"
                                                 CanvasCursor="{Binding Cursor}"
                                                 AutomationValue="{Binding AutomationValue}" />
                </Border>
            </Grid>
        </ScrollViewer>

        <!-- .zoom-controls (editor.html:332-336, styles.css:537-564): bottom 12, right 12, pill, padding 4px 8px,
             gap 8px, 1px border-1; "-" and "+" around a mono 11px fg-3 label at least 36px wide, centered. -->
        <Border HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,12,12"
                Background="{StaticResource ZoomPillBackground}"
                BorderBrush="{DynamicResource Border1Brush}" BorderThickness="1"
                CornerRadius="999" Padding="8,4">
            <StackPanel Orientation="Horizontal">
                <Button x:Name="ZoomOutButton" Style="{StaticResource ZoomButton}" Content="-"
                        Command="{Binding ZoomOutCommand}"
                        AutomationProperties.Name="Zoom out" ToolTip="Zoom out" />
                <TextBlock x:Name="ZoomLabel" Text="{Binding ZoomLabel}" Margin="8,0"
                           MinWidth="36" TextAlignment="Center" VerticalAlignment="Center"
                           FontFamily="{DynamicResource FontMono}" FontSize="11"
                           Foreground="{DynamicResource Fg3Brush}"
                           AutomationProperties.Name="{Binding ZoomLabel, StringFormat=Zoom {0}}" />
                <Button x:Name="ZoomInButton" Style="{StaticResource ZoomButton}" Content="+"
                        Command="{Binding ZoomInCommand}"
                        AutomationProperties.Name="Zoom in" ToolTip="Zoom in" />
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

Replace `src/RBX15.App/Services/ImportService.cs` with:

```csharp
using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// Turns picked or dropped files into layers: Add image, the drop zone and a canvas drop all land
/// here. Every successful file of one call is added in the given order as ONE undo step (spec §3;
/// ledger layers-multi-file-async-order, drop-filetypes, project-load-image-layer). The last file
/// ends on top and selected, as 4.1's last addLayer did. Failures go to the status strip, named
/// (diag-console-only-errors); a call that adds files and fails none clears the strip.
/// </summary>
public sealed class ImportService(EditorSession session, IStatus status)
{
    /// <summary>Raised after an import added at least one layer: addLayer sets the Select tool (editor.js:523; R8).</summary>
    public event EventHandler? LayersAdded;

    /// <summary>
    /// Imports <paramref name="paths"/>; every layer is centered on <paramref name="dropAt"/>
    /// (template pixels) when given, else fit in the front slot. Returns the number of layers added.
    /// </summary>
    public int Import(IReadOnlyList<string> paths, (double X, double Y)? dropAt = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0) return 0;
        var result = ImageImporter.Load(paths);
        if (result.Images.Count > 0)
            session.ApplyWithSelection(s =>
            {
                foreach (var image in result.Images) s = LayerOps.AddImage(s, image.Image, session.TakeId(), image.Name, dropAt);
                return s;
            });
        if (result.Failures.Count > 0) status.Show(FailureMessage(result.Failures));
        else if (result.Images.Count > 0) status.Clear();
        if (result.Images.Count > 0) LayersAdded?.Invoke(this, EventArgs.Empty);
        return result.Images.Count;
    }

    /// <summary>One line naming every file that failed and why.</summary>
    public static string FailureMessage(IReadOnlyList<ImportFailure> failures)
    {
        ArgumentOutOfRangeException.ThrowIfZero(failures.Count);
        if (failures.Count == 1) return $"Couldn't add {failures[0].FileName}: {failures[0].Reason}";
        return $"Couldn't add {failures.Count} files. " + string.Join(" ", failures.Select(f => $"{f.FileName}: {f.Reason}"));
    }
}
```

In `src/RBX15.App/ViewModels/LayersViewModel.cs`, replace

```csharp
    /// <summary>The selected row; setting it selects that layer (selectLayer, editor.js:575; never an undo step).</summary>
    public LayerRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (_syncing || ReferenceEquals(value, _selected)) return;
            _session.Select(value?.Id);
        }
    }
```

with

```csharp
    /// <summary>
    /// The selected row; setting it selects that layer (selectLayer, editor.js:575; never an undo step). A user
    /// pick of a row raises <see cref="LayerPicked"/>; a sync, or a deselect, does not.
    /// </summary>
    public LayerRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (_syncing || ReferenceEquals(value, _selected)) return;
            _session.Select(value?.Id);
            if (value is not null) LayerPicked?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A user picked a row: selectLayer sets the Select tool (editor.js:577; R8). Composition wires it.</summary>
    public event EventHandler? LayerPicked;
```

Replace `src/RBX15.App/Composition.cs` with (the status strip, tool and brush first, the canvas over them, the auto-revert wiring, and the `ClickHandler` line gone):

```csharp
using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// Builds the view-models over one <see cref="EditorSession"/>. Each task that adds a required member
/// to MainViewModel adds its construction line and its member here, at the two anchors, and the tests'
/// TestShell reuses them through the injectable overload. <paramref name="owner"/> returns the window dialogs and
/// the color picker are owned by.
/// </summary>
internal static class Composition
{
    public static MainViewModel Build(EditorSession session, Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Build(session, new Win32FileDialogs(owner), new ThemedDialogService(owner), new ThemedColorPicker(owner));
    }

    /// <summary>
    /// The wiring, once: the native services come in, so the app passes the real ones and the tests'
    /// TestShell passes fakes. Every line that connects view-models lives here and nowhere else.
    /// </summary>
    public static MainViewModel Build(EditorSession session, IFileDialogs fileDialogs, IDialogService dialogs, IColorPicker colorPicker)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fileDialogs);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(colorPicker);
        // M3b: the status strip, the tool and the brush come first; the canvas, the Home tab and the eraser share them.
        var status = new StatusViewModel();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(colorPicker);
        var canvas = new CanvasViewModel(session, tools, brush, status);
        // Task 4: import, export, layers and status (one ImportService serves Add image, the drop zone and the canvas).
        var import = new ImportService(session, status);
        // addLayer and selectLayer set the Select tool; a drawing commit keeps the drawing tool (editor.js:523, :577; R8).
        import.LayersAdded += (_, _) => tools.Set(Tool.Select);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, dialogs, status, import);
        var layers = new LayersViewModel(session, import, fileDialogs);
        layers.LayerPicked += (_, _) => tools.Set(Tool.Select);
        // A canvas drop lands every file on the drop point (editor.js:961-986, drop-canvas).
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        var home = new HomeViewModel(session, colorPicker)
        {
            Tools = tools,
            Brush = brush,
            File = fileGroup,
        };
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            Canvas = canvas,
            Home = home,
            Layers = layers,
            Status = status,
            // M3a: required members above this line.
        };
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`. Then confirm the click path is gone:

Run: `grep -rn "ClickCommand\|ClickHandler\|HandleClick\|CompleteClick\|IsClick(" src/RBX15.App src/RBX15.App.Tests --include=*.cs --include=*.xaml`
Expected: no output (`ThemedColorPicker.IsClickOnDisabledOwner` does not match `IsClick(`).

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.TemplateCanvasHostTests|FullyQualifiedName~RBX15.App.Tests.CanvasViewModelTests|FullyQualifiedName~RBX15.App.Tests.CanvasAutomationPeerTests|FullyQualifiedName~RBX15.App.Tests.ShellWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    39, Skipped:     0, Total:    39` for `RBX15.App.Tests.dll` (TemplateCanvasHostTests 26, CanvasViewModelTests 9, CanvasAutomationPeerTests 2, ShellWiringTests 2).

If `The_frame_is_the_engine_view_pixel_for_pixel` or `The_overlay_reaches_the_engine_frame` fails, the host is drawing something itself or not passing `Overlay` into `ViewOptions`; the host only blits `ViewRenderer.Render`. Never loosen the byte equality. If `Content_changes_keep_the_measure_and_a_zoom_invalidates_it` fails, a frame input regained `AffectsMeasure` or `RequestFrame` invalidates the measure unconditionally (ruling 5).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 481, Failed: 0` (411 + 70). On a desktop whose display scale is not 100%, `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails before and after this task alike (measured at `4e0be14`: layout rounding follows the monitor); it is not this task's. CI runs at 100%.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/ViewModels/CanvasViewModel.cs src/RBX15.App/Controls/TemplateCanvasHost.cs src/RBX15.App/Controls/TemplateCanvasHostAutomationPeer.cs src/RBX15.App/Views/CanvasView.xaml src/RBX15.App/Services/ImportService.cs src/RBX15.App/ViewModels/LayersViewModel.cs src/RBX15.App/Composition.cs src/RBX15.App.Tests/CanvasViewModelTests.cs src/RBX15.App.Tests/CanvasViewTests.cs src/RBX15.App.Tests/ShellWiringTests.cs src/RBX15.App.Tests/ImportServiceTests.cs src/RBX15.App.Tests/LayersViewModelTests.cs src/RBX15.App.Tests/TemplateCanvasHostTests.cs src/RBX15.App.Tests/CanvasAutomationPeerTests.cs
git commit -m "feat(app): the canvas host drives CanvasInteraction with capture, coalesces frames and exposes an automation peer"
```

- [ ] **Step 7: Final verification**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror && dotnet test src/RBX15.slnx -c Release`
Expected: `Build succeeded.` with `0 Warning(s)`, then Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`, Parity.Tests `Passed: 35, Failed: 0`, App.Tests `Passed: 481, Failed: 0`.

Run: `grep -rn "System.Windows\|PresentationCore\|PresentationFramework\|WindowsBase\|UseWPF" src/RBX15.Engine --include=*.cs --include=*.csproj`
Expected: no output. This task adds nothing to the engine.

Run: `grep -rn "SendInput\|mouse_event\|keybd_event\|SendKeys\|SetCursorPos" src --include=*.cs`
Expected: no output (the no-synthetic-input rule).

Run: `git status --short`
Expected: nothing. `.superpowers/` and `diff-heatmaps/` are git-ignored.

The implementer does not launch the app to drive the canvas: mouse gestures are proven by these unit tests through the seams, and by Este's manual checklist (Task 5). No commit in this step.

**Handed to Task 5** (ledger and divergences; this task changes neither):

- **Close on this evidence where the row's own parityCheck is met, or reword the check to it (honesty rule):**
  - `tools-set-tool`: `ToolsViewModelTests.Starts_on_select_with_six_buttons_in_4_1_order_and_no_shortcut_text`, `Set_makes_exactly_one_button_active_and_raises_once_per_change`, `HomeRibbonViewTests.The_tool_buttons_show_4_1_labels_and_the_active_tool`.
  - `tools-toggle-off`: `Pressing_the_active_tool_again_returns_to_select_and_select_never_toggles_off`. Its parityCheck's key presses (B, V, Ctrl+E) are M4's keyboard: narrow to the buttons, or leave the key half open.
  - `tools-auto-select-switch`: `ShellWiringTests.The_tool_reverts_to_select_on_import_and_layer_pick_but_not_on_a_drawing_or_undo`, `LayersViewModelTests.Picking_a_row_raises_LayerPicked_and_syncs_do_not`, `ImportServiceTests.LayersAdded_is_raised_once_per_import_that_added_layers`.
  - `home-tool-buttons`, `home-brush-group`, `draw-brush-controls`, `tools-draw-props`: the HomeRibbonViewTests facts, `BrushViewModelTests` (defaults, 1..50 clamp, live picker).
  - `wheel-brush-size`: `The_eraser_ring_follows_the_brush_and_the_wheel_but_a_stroke_keeps_its_press_size` (20 up from 4 is 24; 60 more is 50; down clamps at 1).
  - `select-move`, `select-move-hit-and-drag`, `select-deselect-click`, `select-hit-test`: `A_press_on_a_layer_selects_it_and_a_drag_moves_it_by_the_template_delta_as_one_step`, `A_click_without_movement_selects_and_records_nothing`, `Empty_canvas_deselects_and_starts_nothing`, `TemplateCanvasHostTests.Pointer_seams_map_by_zoom_and_drive_the_interaction` (drag (50,30) screen px at zoom 2 moves (25,15)), `ShellWiringTests.A_canvas_click_selects_the_topmost_layer_under_it_and_empty_canvas_deselects`.
  - `select-resize-handles`, `select-resize-drag`, `select-resize-handle-hit`: `A_corner_drag_scales_uniformly_about_the_center_as_one_step`, `Resize_is_clamped_to_1_and_500_percent` (with Task 1's SelectionGeometry hit tests).
  - `select-rotate-handle`, `select-rotate-handle-hit`, `select-rotate-drag`, `select-rotate-drag-2`: `The_rotate_handle_drag_snaps_within_3_degrees_and_is_one_step` (44.2 and 47.9 snap, 48.1 does not), `A_rotate_drag_across_the_bottom_stays_within_180_and_snaps_at_the_ends`. The -2 row's "1 degree steps around the center" sequence is Task 1's SelectionGeometry unit test.
  - `select-hover-idle`, `select-hover-and-end`, `canvas-cursor-states`, `cursor-default`: `Hover_shows_grab_resize_move_or_crosshair_and_follows_zoom`, the held-cursor assertions in the resize and rotate facts, `Capture_loss_mid_drag_keeps_the_move_as_one_step`. Their checks are manual or ui-auto: keep the look half open for Este's checklist (the Hand cursor for grab is provisional).
  - `select-hidden-layer-still-handled`: `A_hidden_selected_layer_has_no_canvas_handles_but_the_wheel_still_scales_it` (input half) plus Task 1's overlay test (render half).
  - `wheel-scale-layer`, `wheel-sign-only-and-midstroke`: the five wheel facts in CanvasInteractionTests (two of them pin that an edit made during a burst is its own step), `The_wheel_mid_shape_changes_the_live_width_and_the_committed_shape_but_not_a_pen_stroke`, `TemplateCanvasHostTests.The_wheel_is_sign_only_and_a_quiet_period_closes_the_burst`. "Count the steps for one notch on a touchpad" is manual: Este's checklist.
  - `draw-begin`, `draw-tool-start`, `draw-commit-layer`, `layers-add-drawing`, `draw-empty-no-layer`, `draw-pen-single-click`, `canvas-draw-tools`: the Draw facts (pen commit equals `Tools.CommitDrawing`, the dot, shape clicks add nothing and record nothing, no step at the press).
  - `draw-release-outside-canvas`, `draw-tool-switch-midstroke`: `Capture_loss_mid_stroke_commits_it_at_the_last_point`, `A_stroke_released_outside_the_template_keeps_the_part_inside`, `A_tool_switch_mid_stroke_discards_it`.
  - `eraser-selection-semantics`: `The_eraser_with_no_selection_refuses_at_the_press_with_a_status_message` (exact text).
  - `eraser-apply` (app half): `An_eraser_drag_erases_the_selected_layer_under_another_as_one_step`, `The_eraser_erases_a_hidden_selected_layer`, `Eraser_click_without_move_changes_nothing_and_records_no_step` (R3).
  - `cursor-eraser`, `eraser-cursor`, `tools-cursor`: `CanvasCursorsTests` (radius rows, the bitmap, the 256 cap, zoom and scale caching) and `TemplateCanvasHostTests.The_cursor_follows_the_canvas_cursor_zoom_and_device_scale`. The on-screen look is manual.
  - `canvas-mouse-button-agnostic`: `TemplateCanvasHostTests.Only_the_left_button_reaches_the_interaction`.
  - `canvas-coord-map` (the remaining half): the left-button fact plus `Pointer_seams_map_by_zoom_and_drive_the_interaction`. Its "inject at 125% and 150% Windows scale" check needs a live window: narrow it, or leave it open for the checklist.
  - `render-pipeline-canvas-reset` (keep): `Many_frame_input_changes_render_once`, `Content_changes_keep_the_measure_and_a_zoom_invalidates_it`; its "60 fps on 20 layers" budget is manual.
  - `undo-coverage-gaps`, `undo-empty-steps` (canvas half): every "one step" and "records nothing" assertion above.
- **Este's manual checklist** (Task 5), what automation cannot do here: drag a layer past the canvas edge and back; rotate past straight down; a corner drag; the wheel on a mouse and on a precision touchpad (one burst, one Ctrl+Z); a pen stroke released outside the window; Alt+Tab mid-stroke; right-drag does nothing; the eraser ring at sizes 1, 4 and 50 at 25%, 100% and 300%, on a 100% and a 150% monitor; the grab and grabbing cursors (Hand, provisional); Narrator reads "Template canvas, image" and the selected layer's name.
- **Divergence wording** for Task 5 (rows stay as the ledger has them unless noted):
  - `cursor-eraser`: append R15's cap with ruling 6's formula: "v5: ring drawn at the true brush footprint, centered, up to the 256 px Win32 cursor limit (radius 126 device px at 100%, 124 at 200%)."
  - `canvas-cursor-states`: "grab and grabbing show the Windows Hand cursor (no stock grab cursor); provisional, M3b look sign-off."
  - `wheel-scale-layer`: add "a burst of wheel events within 400 ms is one undo step".

**Additions requested by Task 2** (not in the interface sheet; each is used by the code above):

1. `public static class ToolShapes { public static ShapeTool? ShapeOf(Tool tool); }` in `Canvas/Tool.cs`: the drawing tool to engine shape map (null for Select and Eraser).
2. `CanvasCursor` helpers: `public static readonly CanvasCursor Crosshair`, `public static CanvasCursor Ring(double size)`, `public static CanvasCursor Of(CanvasCursorKind kind)`.
3. `public const string CanvasInteraction.NoLayerToErase = "Select a layer to erase from, then try again."` (editor.js:1270).
4. `public const string BrushViewModel.DefaultColor = "#17d4fa"`.
5. `TemplateCanvasHost`: `public static readonly TimeSpan WheelBurstDelay` (400 ms), `internal bool WheelBurstPending`, `internal void FlushWheelBurst()` (the timer's tick, Unloaded and tests). Unloaded also unhooks the Rendering tick and closes a wheel burst; Loaded re-requests a pending frame.
6. `CanvasCursors`: `public const int MaxCursorSide = 256`, `internal static int Margin(double)`, `internal static double RingRadius(double, double, double)`, `internal static int RingSide(double, double)`; R15's cap generalized per ruling 6.
7. `TemplateCanvasHostAutomationPeer.RaiseValueChanged(string, string)` (internal), called from the `AutomationValue` property-changed callback.
8. `ToolButtonViewModel`'s constructor is `internal`; `IsActive` is an `[ObservableProperty]` (its setter is public, as the toolkit generates it); the view's `ToolButton` style sets `AutomationProperties.ItemStatus = "Active"` (ruling 8).
9. `internal static CanvasViewModel CanvasViewModelTests.Vm(EditorSession, ToolsViewModel?)`, the canvas view-model builder `CanvasViewTests` and `CanvasViewModelTests` share.
10. `HomeTestKit.Home` sets `Tools` and `Brush` (required members).
11. **Namespace shadowing (for Tasks 3 and 4):** inside any `RBX15.App.*` namespace, `Canvas` now names `RBX15.App.Canvas`. Code that means WPF's panel writes `System.Windows.Controls.Canvas`. XAML is unaffected (it maps by xmlns).
12. `ThemeKeyGuardTests` counts 14 view files after this task (Task 3 adds the 15th).
13. `Document` and `Zoom` on `TemplateCanvasHost` no longer carry `FrameworkPropertyMetadataOptions.AffectsMeasure` (ruling 5); the measure follows the frame's pixel size.
14. Ruling 2 (no wheel scaling during a select drag) extends R6/R7; the sheet says nothing about a wheel during a drag.
15. `public event EventHandler? EditorSession.Changing`, raised first by `Apply`, `ApplyWithSelection`, `BeginGesture`, `Undo` and `Redo` (reconciler, critic MJ1). `CanvasInteraction` closes an open wheel burst on it unless the change is its own (`_ownChange`). R7 now reads: a burst is one step, and any other session change closes it first.

---

### Task 3: The layer properties panel ("Selected layer": fields, sliders, flip, adjustments, Reset, live refresh, one undo step per edit)

**Files:**

- Create (app): `src/RBX15.App/Controls/SliderGestureTracker.cs`, `src/RBX15.App/ViewModels/LayerPropertiesViewModel.cs`, `src/RBX15.App/Views/LayerPropertiesPanel.xaml`, `src/RBX15.App/Views/LayerPropertiesPanel.xaml.cs`.
- Create (tests): `src/RBX15.App.Tests/SliderGestureTrackerTests.cs`, `LayerPropertiesViewModelTests.cs`, `LayerPropertiesPanelTests.cs` (all in `src/RBX15.App.Tests/`).
- Modify:
  - `src/RBX15.App/Views/FillStyleGroup.xaml.cs` (printed in full: onto the tracker; `FillStyleGroup.xaml` is unchanged).
  - `src/RBX15.App/ViewModels/MainViewModel.cs` (`required LayerProperties`), `src/RBX15.App/Composition.cs` (construct it at the anchors), `src/RBX15.App/MainWindow.xaml` (the panel after `LayersPanel` in `LeftPanelContent`).
  - Tests whose assertions the M3a-minor fix changes, same counts: `FillStyleTests.A_stale_slider_gesture_is_closed_when_the_next_press_begins` (one assertion), `HomeRibbonViewTests.A_slider_gesture_ends_on_lost_mouse_capture_as_well_as_drag_completed` and `A_pressed_slider_is_one_step_however_many_changes_it_repeats` (build the group with the button held), and `ThemeKeyGuardTests` (the view-file count, plus 1).
- Unchanged: `TestShell.cs` (no new native service), `FillStyleGroup.xaml`, `LayersPanel.xaml` (Task 4 removes its M3a Up/Down pair, interface R13), the engine.
- Read only: `editor.html:311-323`, `editor.js:542-560, 746-912, 929-931, 1168-1194, 1611`, `styles.css:246-263, 324-370, 375-419, 640-647`.

**Interfaces:**

- Consumes:
  - Task 1 (engine): `LayerOps.SetName`, `MoveTo`, `SetScale`, `SetRotation`, `SetOpacity`, `Flip(Document, int, bool horizontal)`, `SetAdjustments`, `ResetAdjustments`; `LayerAdjustments` (`Default`, `From`, the record's `with`); `SelectionGeometry.MinScale` and `MaxScale` (the panel's scale range is defined from them).
  - M3a: `LayerOps.Duplicate`, `MoveUp`, `MoveDown`, `Remove`; `Renderer.Export`; `EditorSession` (`Apply`, `ApplyWithSelection`, `BeginGesture`, `TakeId`, `Select`, `SelectedLayer`, `Undo`, `Changed`, `SessionChange`); `MainViewModel`, `Composition.Build` and its two anchor lines; `MainWindow` (`LeftPanelContent`); `FillStyleGroup` (its internal `Begin(bool)` and `FillStyleGroup(Func<bool>)` ctor); theme keys `PanelHeading`, `SecondaryButtonSm`, `FocusRing`, `UpperCase`, `FontMono`, `FontBody`, `Fg1Brush`, `Fg3Brush`, `FgMutedBrush`, `BrandNavyBrush`, `Border1Brush`, `BrandCyanBrush`, `BrandMagentaBrush`, `BrandMagentaBrightBrush`, `RXsRadius`; tests `StaThread.Run`, `StaThread.Pump`, `HomeTestKit` (`LayerOf`, `SessionWith`, `Track`), `TestShell.Main`, `Fakes.FakeSettingsStore`.
  - Task 2: nothing by name. Live refresh during a canvas drag relies on Task 2's one `session.Apply` per move inside a gesture, which raises `SessionChange.Document`; the tests drive that contract directly with `BeginGesture` and `Apply`. Every panel edit and `BeginSliderDrag` goes through `Apply` or `BeginGesture`, which raise Task 2's `EditorSession.Changing` first, so a canvas wheel burst still open from the last 400 ms is closed as its own step before the panel's edit lands (R7, critic MJ1); nothing in this task calls `Changing` by name.
- Produces (interfaces.md names verbatim, plus "Additions requested by Task 3" at the end):
  - `public sealed partial class LayerPropertiesViewModel : ObservableObject` with `LayerPropertiesViewModel(EditorSession)`, `HasSelection`, `EmptyText`, `Name`, `XText`, `YText`, `PositionLabel`, `ScalePercent`, `ScaleLabel`, `Rotation`, `RotationLabel`, `OpacityPercent`, `OpacityLabel`, `Brightness`, `Contrast`, `Saturate`, `HueRotate`, `RShift`, `GShift`, `BShift`, `BrightnessLabel`, `ContrastLabel`, `SaturateLabel`, `HueLabel`, `RLabel`, `GLabel`, `BLabel`, `AdjustmentsExpanded`, `FlipHCommand`, `FlipVCommand`, `DuplicateCommand`, `MoveUpCommand`, `MoveDownCommand`, `ResetAdjustmentsCommand`, `IDisposable BeginSliderDrag()`.
  - `Views/LayerPropertiesPanel.xaml(.cs)`.
  - `Controls/SliderGestureTracker`: `SliderGestureTracker(Func<IDisposable?> open, Func<bool> leftButtonDown)`, `void Begin(bool freshPress)`, `void End()`; FillStyleGroup on it, with the M3a minor fixed.
  - `MainViewModel.LayerProperties` (`public required LayerPropertiesViewModel LayerProperties { get; init; }`), placed in `LeftPanelContent` after `LayersPanel`.

**What 4.1 does (cited).** Every line is from the shipped `editor.js`, `editor.html` and `styles.css`.

- **Where** (editor.html:311-323): the left panel holds "Drop art here", "Layers (drag to reorder)", then `<h3>Selected layer</h3>` over `#layerControls`, whose initial content is `<p class="info">Select a layer to edit.</p>`.
- **Empty state** (editor.js:747-752): with no selection, `#layerControls` is the `.info` paragraph and updateUI returns. `.info` is mono 10px fg-muted, margin 4px 0, uppercase (styles.css:640-647).
- **The panel** (editor.js:753-803), rebuilt as an innerHTML string on every updateUI:
  - Name: a text input (:754-755), value set by DOM property (:811), `onchange` sets the name and calls updateUI (:825).
  - X and Y: the label `X: <span>n</span> &nbsp; Y: <span>n</span>` (:756, toFixed(0)), then a `.row` of two number inputs (:757-760), values toFixed(0) (:812-813), `onchange` sets `sel.x = +value` and renders only (:826-827).
  - Scale: `Scale: N%` from `(sel.scale*100).toFixed(0)` (:761), a range 1..500 (:762), value `scale*100` (:814), `oninput` sets `scale = value/100` and the label (:828-832). The anchor is the top-left: x and y are untouched.
  - Rotation: `Rotation: ${sel.rotation}°` with no rounding (:763), a range -180..180 (:764), `oninput` (:833-837).
  - Opacity: `Opacity: N%` (:765), a range 0..100 (:766), `oninput` sets `opacity = value/100` (:838-842).
  - Buttons, `btn btn-sm btn-secondary`: Flip H, Flip V, Duplicate in a `.row` with margin-top 6 (:767-771, titles "Flip horizontally.", "Flip vertically.", "Duplicate layer."), Up and Down in a `.row` with margin-top 4 (:772-775, "Move layer up.", "Move layer down."), and a full-width "Center on front torso" (:776-778). Flip toggles and renders (:843-844); Duplicate calls duplicateLayer (:845); Up and Down swap with the neighbour, no-op at the ends (:846-853).
  - Image adjustments (:779-803): margin-top 8; a header row `#adjToggle` (flex, space-between, cursor pointer, padding 4px 0) with "Image adjustments" in 11px cyan uppercase mono 600 and a 10px cyan arrow ▶ (:780-783); `#adjPanel` starts `display:none` (:784). The click toggles it and the arrow between ▶ and ▼ (:862-872).
  - Inside: Brightness, Contrast, Saturation 0..200 with `%` and Hue rotate 0..360 with `°` (:785-792); a margin-top-6 block of R, G, B shifts -100..100, labels colored #ff5472, #4f4 and #66f with matching accent-color (:793-800); "Reset adjustments", full width, margin-top 6, border #f22f89, text #ff5aa3 (:801).
  - Labels and slider values use `sel.brightness || 100`, `sel.contrast || 100`, `sel.saturate || 100`, `sel.hueRotate || 0` and `|| 0` for the shifts (:785-799, :817-823). Each `oninput` stores `+value` and sets its label to the raw slider value (:873-907). Reset sets all seven to 100, 100, 100, 0, 0, 0, 0 and calls updateUI (:908-912).
- **Undo:** none of the above pushes a snapshot. 4.1's sliders elsewhere snapshot once on pointerdown so one drag is one step (editor.js:926-931, :1611).
- **Refresh during canvas drags** (editor.js:1168-1194): a move drag only renders (the X/Y fields go stale); a rotate or resize drag calls updateUI on every mousemove (:1192), which rebuilds the panel, collapses Image adjustments and drops focus.
- **Styles:** `.controls label` mono 10px fg-3, margin 8px 0 3px, uppercase (styles.css:324-332); range inputs full width with a cyan accent (:333); text and number inputs navy, 1px border-1, fg-1, padding 5px 8px, radius r-xs, 12px body, focus border cyan (:335-352); `.row` flex, gap 8, children flex 1 (:369-370); `.btn-sm` padding 5px 10px, 11px (:407); `.btn-secondary` (:398-406).

**Rulings made here** (each pinned by a named test):

1. **"Center on front torso" is absent** (interface R13; ledger `props-center-front` is App.Ribbon.Layout, M4). Everything else in editor.js:753-803 is ported. Pinned by `Every_control_is_named_and_the_buttons_keep_4_1s_titles` (no such button).
2. **The panel is built once and bound** (`props-panel-rebuild-resets`, `props-live-refresh`, fix). The view-model re-reads the selected layer on every Document or Selection change and raises only the properties whose value changed, from one table that the getters also read. A move drag refreshes X and Y live (4.1 left them stale), and nothing collapses or loses focus. Pinned by `A_canvas_move_drag_refreshes_X_and_Y_on_every_move_and_is_one_step`, `Only_changed_properties_are_raised`, `Edits_update_the_same_controls_and_keep_the_adjustments_open`.
3. **Image adjustments stay open** across every edit, Reset, selection change, deselect and undo (`props-adjust-panel`, `props-adjust-reset`, fix). The open state is view state on the view-model, false at start as 4.1's `display:none`. Pinned by `The_adjustments_stay_open_across_selection_edits_and_undo`, `Reset_is_one_step_and_keeps_the_adjustments_open`.
4. **Labels show the stored value** (`props-falsy-zero-adjust-display`, fix): a stored 0 shows "0%". X, Y, scale and opacity use JS `toFixed(0)` exactly (ties away from zero on the exact double, "-0" for an input in (-0.5, 0), "0" for -0), checked against node: `(0.575*100).toFixed(0)` is "57" and `(-0.4).toFixed(0)` is "-0". The adjustment labels print the number as a template string would ("90°", "-7"), never "-0".
5. **The rotation label is whole degrees** (`props-rotation-label-unrounded`, fix): half away from zero, never "-0", the slider's own unit. The canvas label keeps 4.1's toFixed(1) (Task 1's `SelectionGeometry.RotationLabel`).
6. **Slider values are whole numbers, clamped to their ranges** (a step-1 range; FillStyleViewModel's rule). Scale is 1..500 percent, defined from `SelectionGeometry.MinScale` and `MaxScale` so the panel and the engine share one clamp (`props-scale-clamp-inconsistency`, fix), anchored at the top-left as 4.1 (interface R6). Rotation is -180..180, opacity 0..100, B/C/S 0..200, hue 0..360, shifts -100..100.
7. **Typed fields commit on Enter, on focus loss, and on a press anywhere else in the window** (Review Focus 1). The panel hooks its window's `PreviewMouseDown` (tunnelling, so it runs before the canvas acts) and commits any field with uncommitted typing (`BindingExpression.IsDirty`) unless the press is inside that field. Committing the shown text changes nothing, so a field is never "committed" into a step by focus traffic alone.
8. **Refused text** (Review Focus 2): X and Y accept what `double.TryParse(NumberStyles.Float, InvariantCulture)` parses to a finite value whose size is below 1e21, the point where JS `toFixed` switches to exponent form, so the field can always show it. Anything else reverts the field and records nothing. "" is 0 (`+''`), as 4.1.
9. **The M3a minor is fixed in one place** (`SliderGestureTracker`). `Begin(false)` (Thumb.DragStarted) with the button already up closes any stale gesture and opens none; before, it opened a gesture that nothing closed until the next press. Two consequences in existing tests, same counts: `FillStyleTests.A_stale_slider_gesture_is_closed_when_the_next_press_begins` now expects the edit after that DragStarted to be its own step (2 becomes 3), and the two `HomeRibbonViewTests` slider tests build `FillStyleGroup(() => true)` because `Mouse.LeftButton` always reads up in a test, which made them pass only through the minor. The press handler is registered with `handledEventsToo`, so a slider whose class handler marks the press handled still opens its gesture.
10. **Track presses step, they do not jump.** WPF's `IsMoveToPointEnabled` marks the press handled and moves the value before any gesture can open (measured: five steps for one press), and the drag does not continue from there. The panel keeps WPF's stepping track, as the Fill Style sliders do, with PageUp, PageDown and the track step at a tenth of the range: Chromium's range big step, `max((max - min) / 10, step)` (RangeInputType::HandleKeydownEvent): 50, 36, 10, 20, 20, 20, 36, 20, 20, 20.
11. **Look items left to the M3b sign-off** (no 4.1 pixel value is invented): the sliders keep WPF's chrome, so 4.1's cyan and red, green and blue `accent-color` (styles.css:333, editor.js:795-799) is not drawn; the Reset button's border turns cyan on hover through `SecondaryButton`'s trigger, where 4.1's inline `border-color` stays magenta; CSS `letter-spacing` (.08em on labels, .12em on the header) has no WPF property (M3a ruling 5). Task 5 lists them in the sign-off captions.

**Measured while drafting.** A scratch copy of `feat/v5-m3b-select-draw` at `4e0be14` (`git archive`, outside the repo; .NET SDK 10.0.302), with Task 1's members stood in per interfaces.md: `LayerAdjustments`, `SelectionGeometry.MinScale`, `MaxScale`, `ClampScale` and `NormalizeRotation`, and `LayerOps.SetName`, `MoveTo`, `SetScale`, `SetRotation`, `Flip`, `SetOpacity`, `SetAdjustments` and `ResetAdjustments` written to the sheet's contracts (missing id or equal value returns the input; clamps as listed). Task 2 was not stood in: nothing here names its types, and the scratch kept M3a's ClickCommand. Every code block below was compiled there and is printed from those files.

- `dotnet build src/RBX15.slnx -c Release -warnaserror`: 0 warnings, 0 errors.
- Engine.Tests 718, Parity.Tests 35 (unchanged); App.Tests 411 + 76 = 487: SliderGestureTrackerTests 7, LayerPropertiesViewModelTests 58, LayerPropertiesPanelTests 11, FillStyleTests 27 (unchanged count), three full App.Tests runs in a row.
- One App.Tests failure on the drafting machine, in the baseline too and untouched by this task: `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` (expected 158,71, got 157.6,71.2), which depends on the machine's display scale. The implementer's machine and CI are the judge; it is not counted as a Task 3 failure.
- Measured facts used above: `0.575 * 100` is `57.49999999999999` and node prints `(0.575*100).toFixed(0)` as "57"; a track press with `IsMoveToPointEnabled` recorded five steps for one press; with the tracker's `handledEventsToo` and no move-to-point, one.
- Reconciler note: the scratch's `ThemeKeyGuardTests` count was 13 (12 + this panel). In the plan the line reads 14 after Task 2 (ToolsGroup.xaml, BrushGroup.xaml), and Step 7 sets 15.

**Counts.** Task 2 ends at Engine.Tests 877, App.Tests 481 (net of R1's -3 and of the reconciler's three MJ1 facts), Parity.Tests 35. This task adds **Engine.Tests +0, App.Tests +76, Parity.Tests +0**: SliderGestureTrackerTests 7, LayerPropertiesViewModelTests 58, LayerPropertiesPanelTests 11. It ends at Engine.Tests 877 and App.Tests 557.

| After step | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| 1 (baseline) | 877 | 481 | |
| 3 | 877 | 488 | SliderGestureTrackerTests +7 |
| 5 | 877 | 546 | LayerPropertiesViewModelTests +58 |
| **7** | **877** | **557** | LayerPropertiesPanelTests +11 |

- [ ] **Step 1: Baseline**

Task 2 has committed. Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`; App.Tests `Passed: 481, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`.

- [ ] **Step 2: Write the failing SliderGestureTracker tests** (ledger `fillstyle-undo-snapshot-timing`, `props-scale`, `props-rotation`, `props-opacity`, `props-adjust-sliders`; the M3a final-review minor)

Create `src/RBX15.App.Tests/SliderGestureTrackerTests.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RBX15.App.Controls;

namespace RBX15.App.Tests;

/// <summary>
/// The one slider-gesture rule for every slider (FillStyleGroup, LayerPropertiesPanel): a press is one undo
/// gesture (editor.js:929-931, :1611; spec §5), closed by the first release event, and a stale gesture never
/// swallows a later edit. Ledger fillstyle-undo-snapshot-timing, props-scale, props-rotation, props-opacity,
/// props-adjust-sliders.
/// </summary>
public class SliderGestureTrackerTests
{
    /// <summary>Counts opens and closes; Open returns a fresh scope each time.</summary>
    sealed class Gestures
    {
        public int Opened, Closed;
        public bool Down = true;
        public IDisposable? Open() { Opened++; return new Scope(this); }
        sealed class Scope(Gestures g) : IDisposable
        {
            bool _done;
            public void Dispose() { if (_done) return; _done = true; g.Closed++; }
        }
    }

    static (Gestures G, SliderGestureTracker T) Make()
    {
        var g = new Gestures();
        return (g, new SliderGestureTracker(g.Open, () => g.Down));
    }

    [Fact]
    public void A_press_opens_one_gesture_and_the_release_closes_it()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        Assert.True(t.IsOpen);
        t.End();
        t.End();                                         // LostMouseCapture and DragCompleted after the button-up find nothing
        Assert.False(t.IsOpen);
        Assert.Equal((1, 1), (g.Opened, g.Closed));
    }

    [Fact] // Thumb.DragStarted follows the press that opened the gesture: the same gesture
    public void DragStarted_after_the_press_keeps_the_same_gesture()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        t.Begin(freshPress: false);
        Assert.Equal((1, 0), (g.Opened, g.Closed));
    }

    [Fact] // a release that never arrived: the next press closes the stale gesture before opening its own
    public void A_fresh_press_closes_a_stale_gesture_first()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        t.Begin(freshPress: true);
        Assert.Equal((2, 1), (g.Opened, g.Closed));
        Assert.True(t.IsOpen);
    }

    [Fact] // the M3a final-review minor: Begin(false) with the button already up used to open a gesture nothing closed
    public void DragStarted_with_the_button_up_closes_a_stale_gesture_and_opens_none()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        g.Down = false;
        t.Begin(freshPress: false);
        Assert.False(t.IsOpen);
        Assert.Equal((1, 1), (g.Opened, g.Closed));
    }

    [Fact]
    public void DragStarted_with_the_button_up_and_nothing_open_opens_nothing()
    {
        var (g, t) = Make();
        g.Down = false;
        t.Begin(freshPress: false);
        Assert.False(t.IsOpen);
        Assert.Equal((0, 0), (g.Opened, g.Closed));
    }

    [Fact] // a view with no view-model yet: open returns null, and nothing throws
    public void A_null_gesture_is_harmless()
    {
        var t = new SliderGestureTracker(() => null, () => true);
        t.Begin(freshPress: true);
        Assert.False(t.IsOpen);
        t.End();
        Assert.Throws<ArgumentNullException>(() => new SliderGestureTracker(null!, () => true));
        Assert.Throws<ArgumentNullException>(() => new SliderGestureTracker(() => null, null!));
    }

    [Fact] // Attach wires the press, DragStarted, the button-up, DragCompleted and LostMouseCapture
    public void Attach_routes_a_sliders_press_drag_and_release() => StaThread.Run(() =>
    {
        var (g, t) = Make();
        var slider = new Slider();
        t.Attach(slider);

        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.RaiseEvent(new DragStartedEventArgs(0, 0));
        Assert.Equal((1, 0), (g.Opened, g.Closed));
        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        Assert.Equal((1, 1), (g.Opened, g.Closed));

        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
        Assert.Equal((2, 2), (g.Opened, g.Closed));

        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        Assert.Equal((3, 3), (g.Opened, g.Closed));
        Assert.False(t.IsOpen);
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'SliderGestureTracker' could not be found` in `SliderGestureTrackerTests.cs`.

- [ ] **Step 3: Implement SliderGestureTracker and move FillStyleGroup onto it**

Create `src/RBX15.App/Controls/SliderGestureTracker.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RBX15.App.Controls;

/// <summary>
/// One slider press is one undo gesture, as in 4.1, which snapshots once on pointerdown whether the
/// press lands on the thumb or the track (editor.js:929-931, :1611). The press
/// (PreviewMouseLeftButtonDown) opens it; the first of PreviewMouseLeftButtonUp, LostMouseCapture or
/// Thumb.DragCompleted closes it. On a normal release the button-up fires first, then LostMouseCapture,
/// then DragCompleted; the later ones find nothing open. Holding the track fires LargeChange
/// repeatedly, and all of it is the one step (spec §5: no history flood). Keyboard changes arrive
/// outside a press, so each is its own step.
/// <para>
/// A gesture still open when its release never arrived is stale. A fresh press closes it before it
/// opens its own. Thumb.DragStarted (the same press) leaves an open gesture alone while the button is
/// down; with the button already up it closes any stale gesture and opens none (the M3a final-review
/// minor: the old code opened a new gesture there that nothing would close until the next press).
/// </para>
/// One tracker serves every slider of one view, so at most one slider gesture is open per view.
/// </summary>
public sealed class SliderGestureTracker
{
    readonly Func<IDisposable?> _open;
    readonly Func<bool> _leftButtonDown;
    IDisposable? _gesture;

    /// <param name="open">Opens the gesture (a view-model's BeginSliderDrag); null when there is nothing to edit.</param>
    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    public SliderGestureTracker(Func<IDisposable?> open, Func<bool> leftButtonDown)
    {
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _leftButtonDown = leftButtonDown ?? throw new ArgumentNullException(nameof(leftButtonDown));
    }

    /// <summary>The real mouse: <c>Mouse.LeftButton == Pressed</c>.</summary>
    public static bool RealLeftButtonDown() => Mouse.LeftButton == MouseButtonState.Pressed;

    /// <summary>True while a gesture is open.</summary>
    public bool IsOpen => _gesture is not null;

    /// <param name="freshPress">True for the press itself; false for Thumb.DragStarted, which follows it.</param>
    public void Begin(bool freshPress)
    {
        bool down = freshPress || _leftButtonDown();
        if (_gesture is not null && (freshPress || !down)) End();
        if (!down) return;
        _gesture ??= _open();
    }

    /// <summary>Closes the open gesture, if any. Calling it again does nothing.</summary>
    public void End()
    {
        var g = _gesture;
        _gesture = null;
        g?.Dispose();
    }

    /// <summary>Routes <paramref name="slider"/>'s press, release, drag and capture-loss events to this tracker.</summary>
    public void Attach(Slider slider)
    {
        ArgumentNullException.ThrowIfNull(slider);
        // handledEventsToo: a slider template or class handler that marks the press handled must not skip the gesture.
        slider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, _) => Begin(freshPress: true)), handledEventsToo: true);
        slider.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => End()), handledEventsToo: true);
        slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => Begin(freshPress: false)));
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => End()));
        // A press that loses capture without a DragCompleted (focus stolen mid-drag, a dialog, Alt+Tab)
        // would leave the gesture open and fold every later edit into one step.
        slider.LostMouseCapture += (_, _) => End();
    }
}
```

Replace `src/RBX15.App/Views/FillStyleGroup.xaml.cs` with (the XAML and its five handler names are unchanged; the handlers forward to the tracker):

```csharp
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Fill Style group. Its two sliders share one <see cref="SliderGestureTracker"/>: a slider press is
/// one undo gesture, as in 4.1, which snapshots once on pointerdown (editor.js:1611). The tracker holds the
/// open, close and stale-gesture rules for every slider in the app (FillStyleGroup and LayerPropertiesPanel).
/// </summary>
public partial class FillStyleGroup : UserControl
{
    readonly SliderGestureTracker _tracker;

    public FillStyleGroup() : this(SliderGestureTracker.RealLeftButtonDown) { }

    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    internal FillStyleGroup(Func<bool> leftButtonDown)
    {
        _tracker = new SliderGestureTracker(() => (DataContext as FillStyleViewModel)?.BeginSliderDrag(), leftButtonDown);
        InitializeComponent();
    }

    void OnSliderPressed(object sender, MouseButtonEventArgs e) => Begin(freshPress: true);

    // Thumb.DragStarted follows the press that already opened the gesture; opening a second one is skipped.
    void OnSliderDragStarted(object sender, DragStartedEventArgs e) => Begin(freshPress: false);

    void OnSliderReleased(object sender, MouseButtonEventArgs e) => _tracker.End();

    void OnSliderDragCompleted(object sender, DragCompletedEventArgs e) => _tracker.End();

    // A press that loses mouse capture without a DragCompleted (focus stolen mid-drag, a dialog, Alt+Tab)
    // would leave the gesture open and fold every later edit into one undo step.
    void OnSliderLostMouseCapture(object sender, MouseEventArgs e) => _tracker.End();

    /// <param name="freshPress">True for the press itself: any gesture still open then missed its release.</param>
    internal void Begin(bool freshPress) => _tracker.Begin(freshPress);
}
```

In `src/RBX15.App.Tests/FillStyleTests.cs`, test `A_stale_slider_gesture_is_closed_when_the_next_press_begins` (ruling 9), replace:

```csharp
        fill.Angle = 120;
        Assert.Equal(2, session.UndoCount);              // the new gesture is open
```

with:

```csharp
        fill.Angle = 120;
        Assert.Equal(3, session.UndoCount);              // and none opened (the M3a minor): the edit is its own step
```

In `src/RBX15.App.Tests/HomeRibbonViewTests.cs`, in both `A_slider_gesture_ends_on_lost_mouse_capture_as_well_as_drag_completed` and `A_pressed_slider_is_one_step_however_many_changes_it_repeats` (ruling 9; the block occurs exactly twice), replace:

```csharp
        var view = Load(home);
        Tile(home.Regions, "torso_front").PaintCommand.Execute(null);
        var angle = Descendants<Slider>(view).Single(s => AutomationProperties.GetName(s) == "Angle");
```

with:

```csharp
        Tile(home.Regions, "torso_front").PaintCommand.Execute(null);
        // The group with the button held, as during a real press: Mouse.LeftButton always reads up in a test, and
        // SliderGestureTracker ignores a DragStarted that arrives with the button up (M3b Task 3).
        var group = new FillStyleGroup(() => true) { DataContext = home.FillStyle };
        var angle = (Slider)group.FindName("AngleSlider");
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.SliderGestureTrackerTests"`
Expected: `Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.FillStyleTests|FullyQualifiedName~RBX15.App.Tests.HomeRibbonViewTests"`
Expected: `Passed!  - Failed:     0, Passed:    38, Skipped:     0, Total:    38` for `RBX15.App.Tests.dll` (FillStyleTests 27, HomeRibbonViewTests 11; the same counts as at Step 1).

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 877`; App.Tests `Passed: 488, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`.

Commit (end with your own model's `Co-Authored-By` trailer, global constraint):

```bash
git add src/RBX15.App/Controls/SliderGestureTracker.cs src/RBX15.App/Views/FillStyleGroup.xaml.cs src/RBX15.App.Tests/SliderGestureTrackerTests.cs src/RBX15.App.Tests/FillStyleTests.cs src/RBX15.App.Tests/HomeRibbonViewTests.cs
git commit -m "refactor(app): one SliderGestureTracker for slider undo gestures; a DragStarted with the button up opens none"
```

- [ ] **Step 4: Write the failing LayerPropertiesViewModel tests** (ledger `layers-selected-panel`, `layers-empty-state`, `props-name-xy`, `props-scale`, `props-rotation`, `props-opacity`, `props-flip`, `props-adjust-panel`, `props-adjust-sliders`, `props-adjust-reset`, `props-live-refresh`, `props-falsy-zero-adjust-display`, `props-rotation-label-unrounded`, `props-panel-rebuild-resets`, `props-scale-clamp-inconsistency`, `layers-move-updown`)

Create `src/RBX15.App.Tests/LayerPropertiesViewModelTests.cs`:

```csharp
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The "Selected layer" panel's view-model (editor.js:746-912). Ledger rows: layers-selected-panel,
/// layers-empty-state, props-name-xy, props-scale, props-rotation, props-opacity, props-flip, props-adjust-panel,
/// props-adjust-sliders, props-adjust-reset, props-live-refresh, props-falsy-zero-adjust-display,
/// props-rotation-label-unrounded, props-panel-rebuild-resets, props-scale-clamp-inconsistency, layers-move-updown.
/// </summary>
public class LayerPropertiesViewModelTests
{
    static (EditorSession Session, LayerPropertiesViewModel Props) Make(params Layer[] layers)
    {
        var session = SessionWith(layers.Length == 0 ? [LayerOf(1)] : layers);
        session.Select(session.Document.Layers[^1].Id);
        return (session, new LayerPropertiesViewModel(session));
    }

    static Layer Shown(EditorSession s) => s.SelectedLayer!;

    [Fact] // layers-empty-state: editor.js:749-751
    public void With_nothing_selected_the_panel_is_empty_and_inert()
    {
        var session = SessionWith(LayerOf(1));
        var props = new LayerPropertiesViewModel(session);

        Assert.False(props.HasSelection);
        Assert.Equal("Select a layer to edit.", props.EmptyText);
        Assert.Equal(("", "", "", "", "", ""), (props.Name, props.XText, props.YText, props.PositionLabel, props.ScaleLabel, props.RotationLabel));
        Assert.False(props.FlipHCommand.CanExecute(null));
        Assert.False(props.ResetAdjustmentsCommand.CanExecute(null));
        Assert.False(props.MoveUpCommand.CanExecute(null));

        props.Name = "x";
        props.XText = "40";
        props.ScalePercent = 50;
        props.Brightness = 0;
        Assert.Equal(0, session.UndoCount);
        Assert.Equal(LayerOf(1).Name, session.Document.Layers[0].Name);
    }

    [Fact] // editor.js:756-765, :811-823: toFixed(0) for X, Y, scale and opacity; the stored values keep their fractions
    public void Shows_the_selected_layer()
    {
        var (_, props) = Make(LayerOf(1) with { Name = "Logo", X = 12.7, Y = -3.2, Scale = 0.575, Rotation = 37.28194618, Opacity = 0.5 });

        Assert.True(props.HasSelection);
        Assert.Equal("Logo", props.Name);
        Assert.Equal(("13", "-3"), (props.XText, props.YText));
        Assert.Equal("X: 13 \u00A0 Y: -3", props.PositionLabel);
        Assert.Equal(0.575 * 100, props.ScalePercent);
        Assert.Equal("57%", props.ScaleLabel);                     // 0.575 * 100 = 57.49999999999999 -> "57", as node's toFixed(0)
        Assert.Equal(37.28194618, props.Rotation);
        Assert.Equal("37\u00B0", props.RotationLabel);
        Assert.Equal((50.0, "50%"), (props.OpacityPercent, props.OpacityLabel));
        Assert.Equal(("100%", "100%", "100%", "0\u00B0", "0", "0", "0"),
            (props.BrightnessLabel, props.ContrastLabel, props.SaturateLabel, props.HueLabel, props.RLabel, props.GLabel, props.BLabel));
        Assert.False(props.AdjustmentsExpanded);                  // display:none at build (editor.js:784)
    }

    [Theory] // ECMA-262 Number.prototype.toFixed(0): nearest, ties away from zero on the exact double, "-" for any negative
    [InlineData(12.5, "13")]
    [InlineData(-12.5, "-13")]
    [InlineData(2.5, "3")]
    [InlineData(0.49999999999999994, "0")]
    [InlineData(85.49999999999999, "85")]
    [InlineData(-0.4, "-0")]
    [InlineData(-0.0, "0")]
    [InlineData(100000000000000000000.0, "100000000000000000000")]
    public void Position_and_percent_text_is_JS_toFixed_0(double value, string text) =>
        Assert.Equal(text, LayerPropertiesViewModel.ToFixed0(value));

    [Theory] // props-rotation-label-unrounded (fix): whole degrees, half away from zero, never "-0"
    [InlineData(37.28194618, "37")]
    [InlineData(37.5, "38")]
    [InlineData(-37.5, "-38")]
    [InlineData(-0.4, "0")]
    [InlineData(179.6, "180")]
    [InlineData(-179.5, "-180")]
    [InlineData(0.0, "0")]
    public void The_rotation_label_is_whole_degrees(double rotation, string text)
    {
        var (_, props) = Make(LayerOf(1) with { Rotation = rotation });
        Assert.Equal(text + "\u00B0", props.RotationLabel);
        Assert.Equal(text, LayerPropertiesViewModel.WholeDegrees(rotation));
    }

    [Fact] // props-falsy-zero-adjust-display (fix): 4.1's `|| 100` showed a stored 0 as 100 after any rebuild
    public void Zero_adjustments_show_as_zero_after_reselect()
    {
        var (session, props) = Make(LayerOf(1), LayerOf(2));
        props.Brightness = 0;
        props.Contrast = 0;
        props.Saturate = 0;
        session.Select(1);
        session.Select(2);

        Assert.Equal((0.0, 0.0, 0.0), (props.Brightness, props.Contrast, props.Saturate));
        Assert.Equal(("0%", "0%", "0%"), (props.BrightnessLabel, props.ContrastLabel, props.SaturateLabel));
        Assert.Equal((0.0, 0.0, 0.0), (Shown(session).Brightness, Shown(session).Contrast, Shown(session).Saturate));
    }

    [Fact] // props-scale (fix): one step; the slider anchors at the top-left (editor.js:828-832); the same value records nothing
    public void A_scale_edit_is_one_step_anchored_at_the_top_left()
    {
        var (session, props) = Make(LayerOf(1) with { X = 10, Y = 20 });
        props.ScalePercent = 50;
        Assert.Equal((0.5, 10.0, 20.0), (Shown(session).Scale, Shown(session).X, Shown(session).Y));
        Assert.Equal(1, session.UndoCount);
        Assert.Equal("50%", props.ScaleLabel);

        props.ScalePercent = 50;
        Assert.Equal(1, session.UndoCount);
    }

    [Theory] // props-scale-clamp-inconsistency (fix): one clamp, 1..500 %; whole numbers, as a step-1 range
    [InlineData(0, 0.01)]
    [InlineData(-20, 0.01)]
    [InlineData(1000, 5)]
    [InlineData(49.6, 0.5)]
    [InlineData(500, 5)]
    public void The_scale_slider_is_whole_percent_in_1_to_500(double percent, double scale)
    {
        var (session, props) = Make(LayerOf(1) with { Scale = 2 });
        props.ScalePercent = percent;
        Assert.Equal(scale, Shown(session).Scale);
        Assert.Equal((1.0, 500.0), (LayerPropertiesViewModel.MinScalePercent, LayerPropertiesViewModel.MaxScalePercent));
        Assert.Equal((SelectionGeometry.MinScale * 100, SelectionGeometry.MaxScale * 100),
            (LayerPropertiesViewModel.MinScalePercent, LayerPropertiesViewModel.MaxScalePercent));
    }

    [Theory] // props-rotation (fix): -180..180 whole degrees (editor.js:764, :833-837), one step
    [InlineData(45.4, 45)]
    [InlineData(-44.5, -45)]
    [InlineData(200, 180)]
    [InlineData(-200, -180)]
    public void The_rotation_slider_is_whole_degrees_in_range(double value, double stored)
    {
        var (session, props) = Make();
        props.Rotation = value;
        Assert.Equal(stored, Shown(session).Rotation);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // props-opacity (fix): 0..100 %, one step
    public void Opacity_is_whole_percent_and_one_step()
    {
        var (session, props) = Make();
        props.OpacityPercent = 49.5;
        Assert.Equal(0.5, Shown(session).Opacity);
        props.OpacityPercent = 150;
        Assert.Equal(1, Shown(session).Opacity);
        Assert.Equal(2, session.UndoCount);
        Assert.Equal("100%", props.OpacityLabel);
    }

    [Fact] // props-adjust-sliders (fix): each is one step, clamped to its range, and leaves the other six alone
    public void Each_adjustment_is_one_clamped_step_that_keeps_the_others()
    {
        var (session, props) = Make(LayerOf(1) with { Contrast = 40, GShift = -7 });
        props.Brightness = 250;
        props.Saturate = -5;
        props.HueRotate = 90.4;
        props.RShift = 150;
        props.BShift = -150;

        var l = Shown(session);
        Assert.Equal((200.0, 40.0, 0.0, 90.0, 100, -7, -100), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate, l.RShift, l.GShift, l.BShift));
        Assert.Equal(5, session.UndoCount);
        Assert.Equal(("200%", "0%", "90\u00B0", "100", "-7", "-100"),
            (props.BrightnessLabel, props.SaturateLabel, props.HueLabel, props.RLabel, props.GLabel, props.BLabel));
    }

    [Fact] // props-adjust-reset (fix): one step to (100,100,100,0,0,0,0) (editor.js:908-912), the panel stays open; at defaults, nothing
    public void Reset_is_one_step_and_keeps_the_adjustments_open()
    {
        var (session, props) = Make(LayerOf(1) with { Brightness = 0, HueRotate = 180, BShift = 30 });
        props.AdjustmentsExpanded = true;

        props.ResetAdjustmentsCommand.Execute(null);

        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(Shown(session)));
        Assert.Equal(1, session.UndoCount);
        Assert.True(props.AdjustmentsExpanded);
        props.ResetAdjustmentsCommand.Execute(null);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // props-flip (fix): each flip is one step and undo reverts it (editor.js:843-844)
    public void Flips_are_one_step_each_and_undo()
    {
        var (session, props) = Make();
        props.FlipHCommand.Execute(null);
        props.FlipVCommand.Execute(null);
        Assert.Equal((true, true, 2), (Shown(session).FlipH, Shown(session).FlipV, session.UndoCount));
        session.Undo();
        Assert.Equal((true, false), (Shown(session).FlipH, Shown(session).FlipV));
    }

    [Fact] // props-name-xy (fix): a committed name is one step; the same name none; empty is allowed, as 4.1
    public void A_name_commit_is_one_step_and_may_be_empty()
    {
        var (session, props) = Make();
        props.Name = "Logo";
        props.Name = "Logo";
        Assert.Equal(("Logo", 1), (Shown(session).Name, session.UndoCount));
        props.Name = "";
        Assert.Equal(("", 2), (Shown(session).Name, session.UndoCount));
    }

    [Theory] // props-name-xy: +value (editor.js:826-827); "" is 0; the stored value keeps its fraction, shown toFixed(0)
    [InlineData("40", 40.0, "40")]
    [InlineData("12.7", 12.7, "13")]
    [InlineData(" -5 ", -5.0, "-5")]
    [InlineData("", 0.0, "0")]
    [InlineData("1e3", 1000.0, "1000")]
    [InlineData("-0", 0.0, "0")]
    public void A_typed_X_commits_one_step_and_keeps_Y(string typed, double x, string shown)
    {
        var (session, props) = Make(LayerOf(1) with { X = 3, Y = 9 });
        props.XText = typed;
        Assert.Equal((x, 9.0), (Shown(session).X, Shown(session).Y));
        Assert.Equal(shown, props.XText);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // a field committed unchanged (focus loss with nothing typed) records nothing and keeps the fraction
    public void Committing_the_shown_text_changes_nothing()
    {
        var (session, props) = Make(LayerOf(1) with { X = 12.7, Y = 4.2 });
        props.XText = "13";
        props.YText = "4";
        Assert.Equal((12.7, 4.2, 0), (Shown(session).X, Shown(session).Y, session.UndoCount));
    }

    [Theory] // Review Focus: text that is not a finite number below 1e21 is refused, the field re-reads, nothing is recorded
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    [InlineData("1e21")]
    [InlineData("12,5")]
    [InlineData("0x10")]
    public void Unusable_position_text_is_refused_and_reverts(string typed)
    {
        var (session, props) = Make(LayerOf(1) with { X = 3, Y = 9 });
        var raised = Track(props);
        props.YText = typed;
        Assert.Equal((3.0, 9.0, 0), (Shown(session).X, Shown(session).Y, session.UndoCount));
        Assert.Equal("9", props.YText);
        Assert.Contains(nameof(LayerPropertiesViewModel.YText), raised);
    }

    [Fact] // a far position below 1e21 is accepted, and the document still exports (the layer is simply off the canvas)
    public void A_far_position_is_accepted_and_still_exports()
    {
        var (session, props) = Make();
        props.XText = "9e20";
        Assert.Equal(9e20, Shown(session).X);
        using var image = Renderer.Export(session.Document);
        Assert.Equal((585, 559), (image.Width, image.Height));
    }

    [Fact] // props-live-refresh (fix): a canvas move drag (Task 2: one Apply per move inside a gesture) refreshes X/Y live
    public void A_canvas_move_drag_refreshes_X_and_Y_on_every_move_and_is_one_step()
    {
        var (session, props) = Make(LayerOf(1) with { X = 10, Y = 10 });
        var raised = Track(props);
        using (session.BeginGesture())
        {
            session.Apply(d => LayerOps.MoveTo(d, 1, 25, 10));
            Assert.Equal(("25", "10", "X: 25 \u00A0 Y: 10"), (props.XText, props.YText, props.PositionLabel));
            session.Apply(d => LayerOps.MoveTo(d, 1, 40, 31));
            Assert.Equal(("40", "31"), (props.XText, props.YText));
        }
        Assert.Equal(1, session.UndoCount);
        // only what changed is raised: no rebuild, so focus and the open adjustments survive (props-panel-rebuild-resets)
        Assert.Equal(new[] { "XText", "PositionLabel", "XText", "YText", "PositionLabel" }, raised);
    }

    [Fact] // props-live-refresh: during a rotate drag the slider tracks and the label stays rounded
    public void A_rotate_drag_moves_the_rotation_slider_and_rounds_the_label()
    {
        var (session, props) = Make();
        using (session.BeginGesture())
            session.Apply(d => LayerOps.SetRotation(d, 1, 37.28194618));
        Assert.Equal((37.28194618, "37\u00B0"), (props.Rotation, props.RotationLabel));
    }

    [Fact] // a flip shows nowhere in the panel, so it raises nothing; an opacity edit raises only its own two
    public void Only_changed_properties_are_raised()
    {
        var (_, props) = Make();
        var raised = Track(props);
        props.FlipHCommand.Execute(null);
        Assert.Empty(raised);
        props.OpacityPercent = 40;
        Assert.Equal(new[] { "OpacityPercent", "OpacityLabel" }, raised);
    }

    [Fact] // props-adjust-panel and props-panel-rebuild-resets (fix): the open state survives selection, deselect, edits and undo
    public void The_adjustments_stay_open_across_selection_edits_and_undo()
    {
        var (session, props) = Make(LayerOf(1), LayerOf(2));
        props.AdjustmentsExpanded = true;
        props.HueRotate = 45;
        session.Select(1);
        session.Select(null);
        Assert.False(props.HasSelection);
        session.Select(2);
        session.Undo();
        Assert.True(props.AdjustmentsExpanded);
        Assert.Equal((0.0, "0\u00B0"), (props.HueRotate, props.HueLabel));
    }

    [Fact] // a slider drag is one gesture: any number of moves is one step; a drag back to the start records nothing
    public void A_slider_drag_is_one_step_and_a_round_trip_records_nothing()
    {
        var (session, props) = Make();
        using (props.BeginSliderDrag())
        {
            for (int v = 99; v >= 60; v--) props.ScalePercent = v;
        }
        Assert.Equal((0.6, 1), (Shown(session).Scale, session.UndoCount));

        using (props.BeginSliderDrag())
        {
            props.OpacityPercent = 30;
            props.OpacityPercent = 100;
        }
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // the layer's removal returns the panel to its empty state
    public void Removing_the_shown_layer_empties_the_panel()
    {
        var (session, props) = Make();
        session.ApplyWithSelection(s => LayerOps.Remove(s, 1));
        Assert.False(props.HasSelection);
        Assert.Equal("", props.Name);
    }

    [Fact] // btnDupe (editor.js:845): one step, the copy is selected and shown
    public void Duplicate_is_one_step_and_shows_the_copy()
    {
        var (session, props) = Make(LayerOf(1) with { Name = "Logo", X = 5 });
        props.DuplicateCommand.Execute(null);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(("Logo copy", "15"), (props.Name, props.XText));
    }

    [Fact] // layers-move-updown (fix): one step each; disabled at the ends (editor.js:846-853)
    public void Up_and_Down_are_one_step_and_disabled_at_the_ends()
    {
        var (session, props) = Make(LayerOf(1), LayerOf(2));     // 2 is selected and on top
        Assert.False(props.MoveUpCommand.CanExecute(null));
        Assert.True(props.MoveDownCommand.CanExecute(null));
        props.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { 2, 1 }, session.Document.Layers.Select(l => l.Id));
        Assert.Equal(1, session.UndoCount);
        Assert.True(props.MoveUpCommand.CanExecute(null));
        Assert.False(props.MoveDownCommand.CanExecute(null));
    }

    [Fact] // interface R2: a hidden selected layer is still edited from the panel, as in 4.1
    public void A_hidden_selected_layer_is_still_edited()
    {
        var (session, props) = Make(LayerOf(1) with { Visible = false });
        props.OpacityPercent = 20;
        Assert.Equal(0.2, Shown(session).Opacity);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'LayerPropertiesViewModel' could not be found` in `LayerPropertiesViewModelTests.cs`.

- [ ] **Step 5: Implement LayerPropertiesViewModel**

Create `src/RBX15.App/ViewModels/LayerPropertiesViewModel.cs`:

```csharp
using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The left panel's "Selected layer" section (editor.html:320-323; updateUI's controls, editor.js:746-912;
/// ledger <c>layers-selected-panel</c>). It shows the selected layer's Name, X and Y, Scale, Rotation,
/// Opacity, Flip H and Flip V, Duplicate, Up and Down, and the collapsible Image adjustments with Reset.
/// "Center on front torso" (editor.js:776-778) is <c>props-center-front</c>, owned by the Layout tab in M4,
/// so it is absent (interface ruling R13).
/// <para>
/// <b>One undo step per edit</b> (spec §5; every <c>props-*</c> row is a fix because 4.1 pushed none). A
/// slider drag is one <see cref="BeginSliderDrag"/> gesture; a keyboard slider change, a button, and a typed
/// field committed on Enter or focus loss are one <see cref="EditorSession.Apply"/> each, and a commit that
/// changes nothing records nothing.
/// </para>
/// <para>
/// <b>Live, never rebuilt</b> (<c>props-live-refresh</c>, <c>props-panel-rebuild-resets</c>). 4.1 rebuilt the
/// whole panel on every updateUI (editor.js:753), collapsing the adjustments and dropping focus, and left X/Y
/// stale during a move drag (editor.js:1191-1192). Here every property is re-read from the selected layer on
/// each Document or Selection change, including each move of a canvas drag, and only the properties whose
/// value changed are raised, so the controls, their focus and <see cref="AdjustmentsExpanded"/> survive.
/// </para>
/// </summary>
public sealed partial class LayerPropertiesViewModel : ObservableObject
{
    /// <summary>The scale slider's range in percent (editor.js:762), equal to the engine's one clamp (props-scale-clamp-inconsistency).</summary>
    public const double MinScalePercent = SelectionGeometry.MinScale * 100, MaxScalePercent = SelectionGeometry.MaxScale * 100;

    /// <summary>The rotation slider's range (editor.js:764).</summary>
    public const double MinRotation = -180, MaxRotation = 180;

    /// <summary>The adjustment slider ranges (editor.js:786-799).</summary>
    public const double MaxPercentAdjust = 200, MaxHue = 360;
    public const int MinShift = -100, MaxShift = 100;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Every displayed property, read from the layer shown (null: the empty state). It is the one definition:
    /// each getter reads its own row, and Refresh raises the rows whose value differs.
    /// </summary>
    static readonly (string Name, Func<Layer?, object> Read)[] Shown =
    [
        (nameof(HasSelection), l => l is not null),
        (nameof(Name), l => l?.Name ?? ""),
        (nameof(XText), l => l is null ? "" : ToFixed0(l.X)),
        (nameof(YText), l => l is null ? "" : ToFixed0(l.Y)),
        (nameof(PositionLabel), l => l is null ? "" : "X: " + ToFixed0(l.X) + " \u00A0 Y: " + ToFixed0(l.Y)),
        (nameof(ScalePercent), l => l is null ? 100.0 : l.Scale * 100),
        (nameof(ScaleLabel), l => l is null ? "" : ToFixed0(l.Scale * 100) + "%"),
        (nameof(Rotation), l => l?.Rotation ?? 0.0),
        (nameof(RotationLabel), l => l is null ? "" : WholeDegrees(l.Rotation) + "\u00B0"),
        (nameof(OpacityPercent), l => l is null ? 100.0 : l.Opacity * 100),
        (nameof(OpacityLabel), l => l is null ? "" : ToFixed0(l.Opacity * 100) + "%"),
        (nameof(Brightness), l => l?.Brightness ?? 100.0),
        (nameof(Contrast), l => l?.Contrast ?? 100.0),
        (nameof(Saturate), l => l?.Saturate ?? 100.0),
        (nameof(HueRotate), l => l?.HueRotate ?? 0.0),
        (nameof(RShift), l => l?.RShift ?? 0),
        (nameof(GShift), l => l?.GShift ?? 0),
        (nameof(BShift), l => l?.BShift ?? 0),
        (nameof(BrightnessLabel), l => l is null ? "" : Num(l.Brightness) + "%"),
        (nameof(ContrastLabel), l => l is null ? "" : Num(l.Contrast) + "%"),
        (nameof(SaturateLabel), l => l is null ? "" : Num(l.Saturate) + "%"),
        (nameof(HueLabel), l => l is null ? "" : Num(l.HueRotate) + "\u00B0"),
        (nameof(RLabel), l => l is null ? "" : l.RShift.ToString(Inv)),
        (nameof(GLabel), l => l is null ? "" : l.GShift.ToString(Inv)),
        (nameof(BLabel), l => l is null ? "" : l.BShift.ToString(Inv)),
    ];

    static readonly Dictionary<string, Func<Layer?, object>> ShownByName = Shown.ToDictionary(p => p.Name, p => p.Read);

    readonly EditorSession _session;
    Layer? _layer;
    bool _syncing;
    bool _adjustmentsExpanded;

    public LayerPropertiesViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        FlipHCommand = new RelayCommand(() => Edit((d, id) => LayerOps.Flip(d, id, horizontal: true)), () => HasSelection);
        FlipVCommand = new RelayCommand(() => Edit((d, id) => LayerOps.Flip(d, id, horizontal: false)), () => HasSelection);
        // btnDupe -> duplicateLayer(sel.id) (editor.js:845, :542-560), as the row's Dupe does.
        DuplicateCommand = new RelayCommand(Duplicate, () => HasSelection);
        // btnMoveUp / btnMoveDown (editor.js:846-853): disabled at the ends (layers-move-updown).
        MoveUpCommand = new RelayCommand(() => Edit(LayerOps.MoveUp), () => Index() is int i && i < _session.Document.Layers.Count - 1);
        MoveDownCommand = new RelayCommand(() => Edit(LayerOps.MoveDown), () => Index() is > 0);
        ResetAdjustmentsCommand = new RelayCommand(() => Edit(LayerOps.ResetAdjustments), () => HasSelection);
        _session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0) Refresh();
        };
        Refresh();
    }

    public bool HasSelection => Get<bool>();

    /// <summary>The empty state (editor.js:750; ledger layers-empty-state). The view upper-cases it (.info, styles.css:640-647).</summary>
    public string EmptyText => "Select a layer to edit.";

    /// <summary>#ctrlName (editor.js:755, :811, :825). The view commits it on Enter or focus loss. Empty is allowed, as 4.1.</summary>
    public string Name
    {
        get => Get<string>();
        set => Edit((d, id) => LayerOps.SetName(d, id, value));
    }

    /// <summary>#ctrlX, shown toFixed(0) (editor.js:812). See <see cref="CommitPosition"/>.</summary>
    public string XText
    {
        get => Get<string>();
        set => CommitPosition(value, horizontal: true);
    }

    /// <summary>#ctrlY, shown toFixed(0) (editor.js:813).</summary>
    public string YText
    {
        get => Get<string>();
        set => CommitPosition(value, horizontal: false);
    }

    /// <summary>The label above X and Y: "X: 12 &#160; Y: 34" (editor.js:756, toFixed(0), space nbsp space).</summary>
    public string PositionLabel => Get<string>();

    /// <summary>#ctrlScale, 1..500 percent (editor.js:762, :814, :828-832). The slider anchors at the top-left (interface R6).</summary>
    public double ScalePercent
    {
        get => Get<double>();
        set => Edit((d, id) => LayerOps.SetScale(d, id, Whole(value, MinScalePercent, MaxScalePercent) / 100));
    }

    /// <summary>#scaleVal: (scale*100).toFixed(0) + "%" (editor.js:761).</summary>
    public string ScaleLabel => Get<string>();

    /// <summary>#ctrlRot, -180..180 whole degrees (editor.js:764, :815, :833-837).</summary>
    public double Rotation
    {
        get => Get<double>();
        set => Edit((d, id) => LayerOps.SetRotation(d, id, Whole(value, MinRotation, MaxRotation)));
    }

    /// <summary>
    /// #rotVal. 4.1 printed the raw float (editor.js:763, "37.28194618°" after a handle drag). v5 shows whole
    /// degrees, half away from zero, never "-0" (props-rotation-label-unrounded, fix): the slider's own unit.
    /// </summary>
    public string RotationLabel => Get<string>();

    /// <summary>#ctrlOpa, 0..100 percent (editor.js:766, :816, :838-842).</summary>
    public double OpacityPercent
    {
        get => Get<double>();
        set => Edit((d, id) => LayerOps.SetOpacity(d, id, Whole(value, 0, 100) / 100));
    }

    /// <summary>#opaVal: (opacity*100).toFixed(0) + "%" (editor.js:765).</summary>
    public string OpacityLabel => Get<string>();

    /// <summary>#ctrlBright 0..200 (editor.js:786, :873-877).</summary>
    public double Brightness
    {
        get => Get<double>();
        set => Adjust(a => a with { Brightness = Whole(value, 0, MaxPercentAdjust) });
    }

    /// <summary>#ctrlContrast 0..200 (editor.js:788, :878-882).</summary>
    public double Contrast
    {
        get => Get<double>();
        set => Adjust(a => a with { Contrast = Whole(value, 0, MaxPercentAdjust) });
    }

    /// <summary>#ctrlSaturate 0..200 (editor.js:790, :883-887).</summary>
    public double Saturate
    {
        get => Get<double>();
        set => Adjust(a => a with { Saturate = Whole(value, 0, MaxPercentAdjust) });
    }

    /// <summary>#ctrlHue 0..360 (editor.js:792, :888-892).</summary>
    public double HueRotate
    {
        get => Get<double>();
        set => Adjust(a => a with { HueRotate = Whole(value, 0, MaxHue) });
    }

    /// <summary>#ctrlR -100..100 (editor.js:795, :893-897).</summary>
    public int RShift
    {
        get => Get<int>();
        set => Adjust(a => a with { RShift = Math.Clamp(value, MinShift, MaxShift) });
    }

    /// <summary>#ctrlG -100..100 (editor.js:797, :898-902).</summary>
    public int GShift
    {
        get => Get<int>();
        set => Adjust(a => a with { GShift = Math.Clamp(value, MinShift, MaxShift) });
    }

    /// <summary>#ctrlB -100..100 (editor.js:799, :903-907).</summary>
    public int BShift
    {
        get => Get<int>();
        set => Adjust(a => a with { BShift = Math.Clamp(value, MinShift, MaxShift) });
    }

    /// <summary>
    /// #brightVal and the rest show the stored value. 4.1's <c>sel.brightness || 100</c> showed a stored 0 as 100
    /// after any rebuild (editor.js:785-791, :817-819; props-falsy-zero-adjust-display, fix): here 0 shows "0%".
    /// </summary>
    public string BrightnessLabel => Get<string>();

    public string ContrastLabel => Get<string>();

    public string SaturateLabel => Get<string>();

    /// <summary>#hueVal: value + "°" (editor.js:791, :890).</summary>
    public string HueLabel => Get<string>();

    /// <summary>#rVal, #gVal, #bVal: the plain number (editor.js:794-798, :895, :900, :905).</summary>
    public string RLabel => Get<string>();

    public string GLabel => Get<string>();

    public string BLabel => Get<string>();

    /// <summary>
    /// Whether Image adjustments is open (#adjToggle, editor.js:780-783, :862-872). View state, not document
    /// state: closed at start as 4.1 (display:none, editor.js:784), and kept across every edit, Reset and selection
    /// change (props-adjust-panel, props-adjust-reset and props-panel-rebuild-resets, fix: 4.1 collapsed it on
    /// every rebuild).
    /// </summary>
    public bool AdjustmentsExpanded
    {
        get => _adjustmentsExpanded;
        set => SetProperty(ref _adjustmentsExpanded, value);
    }

    /// <summary>btnFlipH (editor.js:768, :843): one step (props-flip, fix).</summary>
    public IRelayCommand FlipHCommand { get; }

    /// <summary>btnFlipV (editor.js:769, :844): one step.</summary>
    public IRelayCommand FlipVCommand { get; }

    public IRelayCommand DuplicateCommand { get; }

    public IRelayCommand MoveUpCommand { get; }

    public IRelayCommand MoveDownCommand { get; }

    /// <summary>btnResetAdj (editor.js:801, :908-912): all seven to their defaults, one step (props-adjust-reset, fix).</summary>
    public IRelayCommand ResetAdjustmentsCommand { get; }

    /// <summary>One slider drag as one undo step (<see cref="EditorSession.BeginGesture"/>): it records one step iff the drag changed the layer.</summary>
    public IDisposable BeginSliderDrag() => _session.BeginGesture();

    /// <summary>
    /// X and Y commit (onchange, editor.js:826-827). Text equal to the shown value changes nothing (4.1's change
    /// event fires only on a changed value), so a field committed on every focus loss records no step. An empty
    /// field is 0 (<c>+''</c>). Text that is not a finite number, or whose size reaches 1e21 (where JS toFixed
    /// switches to exponent form), is refused: the field shows the stored value again and nothing is recorded.
    /// The stored value keeps its fraction; the field shows it toFixed(0) (props-name-xy).
    /// </summary>
    void CommitPosition(string? text, bool horizontal)
    {
        if (_syncing || _layer is null) return;
        string shown = horizontal ? XText : YText;
        string t = (text ?? "").Trim();
        if (t == shown) return;
        double v = 0;
        if (t.Length > 0 && (!double.TryParse(t, NumberStyles.Float, Inv, out v) || !double.IsFinite(v) || Math.Abs(v) >= 1e21))
        {
            OnPropertyChanged(horizontal ? nameof(XText) : nameof(YText));
            return;
        }
        v += 0.0;   // -0 typed is 0
        var layer = _layer;
        _session.Apply(d => LayerOps.MoveTo(d, layer.Id, horizontal ? v : layer.X, horizontal ? layer.Y : v));
        OnPropertyChanged(horizontal ? nameof(XText) : nameof(YText));   // "12.0" or "+12" reads back as "12"
    }

    void Adjust(Func<LayerAdjustments, LayerAdjustments> change) =>
        Edit((d, id) => LayerOps.SetAdjustments(d, id, change(LayerAdjustments.From(d.Layers.Find(l => l.Id == id)!))));

    /// <summary>Applies <paramref name="op"/> to the shown layer as one step (none when it changes nothing).</summary>
    void Edit(Func<Document, int, Document> op)
    {
        if (_syncing || _layer is null) return;
        int id = _layer.Id;
        _session.Apply(d => d.Layers.Exists(l => l.Id == id) ? op(d, id) : d);
    }

    void Duplicate()
    {
        if (_layer is null) return;
        int id = _layer.Id;
        int newId = _session.TakeId();
        _session.ApplyWithSelection(s => LayerOps.Duplicate(s, id, newId));
    }

    int? Index()
    {
        if (_layer is null) return null;
        int id = _layer.Id;
        int i = _session.Document.Layers.FindIndex(l => l.Id == id);
        return i < 0 ? null : i;
    }

    T Get<T>([CallerMemberName] string name = "") => (T)ShownByName[name](_layer);

    /// <summary>Re-reads the selected layer and raises only what changed (no rebuild: focus and expansion survive).</summary>
    void Refresh()
    {
        var before = _layer;
        var after = _session.SelectedLayer;
        _syncing = true;
        try
        {
            _layer = after;
            if (!ReferenceEquals(before, after))
                foreach (var (name, read) in Shown)
                    if (!Equals(read(before), read(after))) OnPropertyChanged(name);
        }
        finally
        {
            _syncing = false;
        }
        FlipHCommand.NotifyCanExecuteChanged();
        FlipVCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        ResetAdjustmentsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// JS Number.prototype.toFixed(0) for |v| &lt; 1e21: the nearest integer, ties away from zero on the exact
    /// double, and a "-" for any negative input, so -0.4 gives "-0" and -0 gives "0" (ECMA-262 §21.1.3.3).
    /// </summary>
    internal static string ToFixed0(double v) =>
        (v < 0 ? "-" : "") + Math.Round(Math.Abs(v), MidpointRounding.AwayFromZero).ToString("F0", Inv);

    /// <summary>Whole degrees, half away from zero, with no "-0" (props-rotation-label-unrounded).</summary>
    internal static string WholeDegrees(double degrees) =>
        (Math.Round(degrees, MidpointRounding.AwayFromZero) + 0.0).ToString("F0", Inv);

    /// <summary>A stored number as JS prints it inside a template string, for the slider-range values the labels show.</summary>
    static string Num(double v) => (v + 0.0).ToString(Inv);

    /// <summary>A step-1 range value: rounded half away from zero, clamped to [min, max], never -0. NaN throws, as FillStyleViewModel does.</summary>
    static double Whole(double value, double min, double max)
    {
        if (double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), "NaN is not a slider value.");
        return Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), min, max) + 0.0;
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.LayerPropertiesViewModelTests"`
Expected: `Passed!  - Failed:     0, Passed:    58, Skipped:     0, Total:    58` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 877`; App.Tests `Passed: 546, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`.

Commit (end with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/ViewModels/LayerPropertiesViewModel.cs src/RBX15.App.Tests/LayerPropertiesViewModelTests.cs
git commit -m "feat(app): LayerPropertiesViewModel, live from the selected layer, one undo step per edit"
```

- [ ] **Step 6: Write the failing panel tests** (ledger `layers-selected-panel`, `layers-empty-state`, `props-name-xy`, `props-adjust-panel`, `props-live-refresh`, `props-panel-rebuild-resets`, `props-center-front` absence; spec §4 Accessibility; Review Focus 1 to 3)

Create `src/RBX15.App.Tests/LayerPropertiesPanelTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The "Selected layer" view (editor.html:320-323, editor.js:746-912): it binds, it is never rebuilt, each
/// slider press is one step, typed fields commit on Enter or on a press elsewhere, and every control has a name
/// (spec §4 Accessibility). Laid out off-screen on StaThread, whose Application holds Task 1's theme.
/// </summary>
public class LayerPropertiesPanelTests
{
    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(252, 4000));
        e.Arrange(new Rect(0, 0, 252, 4000));
        e.UpdateLayout();
    }

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    static T Named<T>(FrameworkElement view, string name) => (T)view.FindName(name);

    /// <summary>Visible on screen if laid out: it and every visual ancestor are Visibility.Visible (IsVisible needs a window).</summary>
    static bool Shown(DependencyObject e)
    {
        for (var d = e; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    static (EditorSession Session, LayerPropertiesViewModel Props, LayerPropertiesPanel View) Make(Func<bool>? leftDown = null, params Layer[] layers)
    {
        var session = SessionWith(layers.Length == 0 ? [LayerOf(1) with { Name = "drip_red", X = 12.7, Y = 30 }] : layers);
        session.Select(session.Document.Layers[^1].Id);
        var props = new LayerPropertiesViewModel(session);
        var view = new LayerPropertiesPanel(leftDown ?? (() => true)) { DataContext = props };
        Layout(view);
        return (session, props, view);
    }

    [Fact] // layers-empty-state: "Select a layer to edit." as .info (upper case, mono 10px fg-muted), controls hidden
    public void The_empty_state_shows_the_info_text_and_hides_the_controls() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        Assert.Equal(Visibility.Collapsed, Named<TextBlock>(view, "EmptyState").Visibility);
        Assert.Equal(Visibility.Visible, Named<StackPanel>(view, "Controls").Visibility);

        session.Select(null);
        Layout(view);
        var empty = Named<TextBlock>(view, "EmptyState");
        Assert.Equal(Visibility.Visible, empty.Visibility);
        Assert.Equal("SELECT A LAYER TO EDIT.", empty.Text);
        Assert.Equal((10.0, "#FF5E6B7F"), (empty.FontSize, ((SolidColorBrush)empty.Foreground).Color.ToString()));
        Assert.Equal(Visibility.Collapsed, Named<StackPanel>(view, "Controls").Visibility);
    });

    [Fact] // the controls show the layer (editor.js:811-823) and the adjustments start closed with ▶ (editor.js:782, :784)
    public void The_controls_bind_to_the_selected_layer() => StaThread.Run(() =>
    {
        var (_, _, view) = Make();
        Assert.Equal("drip_red", Named<TextBox>(view, "NameBox").Text);
        Assert.Equal(("13", "30"), (Named<TextBox>(view, "XBox").Text, Named<TextBox>(view, "YBox").Text));
        Assert.Equal("X: 13 \u00A0 Y: 30", Named<TextBlock>(view, "PositionText").Text);
        Assert.Equal(("100%", "0\u00B0", "100%"), (Named<Run>(view, "ScaleText").Text, Named<Run>(view, "RotationText").Text, Named<Run>(view, "OpacityText").Text));
        Assert.Equal((100.0, 0.0, 100.0), (Named<Slider>(view, "ScaleSlider").Value, Named<Slider>(view, "RotationSlider").Value, Named<Slider>(view, "OpacitySlider").Value));
        var adjustments = Named<Expander>(view, "Adjustments");
        Assert.False(adjustments.IsExpanded);
        Assert.Equal("\u25B6", Descendants<TextBlock>(adjustments).First(t => t.Name == "Arrow").Text);
    });

    [Fact] // editor.js:762-799: the ten ranges, in panel order
    public void The_ten_sliders_have_4_1s_ranges() => StaThread.Run(() =>
    {
        var (_, _, view) = Make();
        Assert.Equal(
            new[] { (1.0, 500.0), (-180, 180), (0, 100), (0, 200), (0, 200), (0, 200), (0, 360), (-100, 100), (-100, 100), (-100, 100) },
            view.Sliders.Select(s => (s.Minimum, s.Maximum)));
        Assert.All(view.Sliders, s => Assert.True(s.IsSnapToTickEnabled && s.TickFrequency == 1 && s.SmallChange == 1));
    });

    [Fact] // spec §4 Accessibility: every control has a name; 4.1's button titles are the tooltips (editor.js:768-774)
    public void Every_control_is_named_and_the_buttons_keep_4_1s_titles() => StaThread.Run(() =>
    {
        var (_, props, view) = Make();
        props.AdjustmentsExpanded = true;
        Layout(view);
        var focusable = Descendants<Control>(view).Where(c => c.Focusable && Shown(c) && c is not ScrollViewer).ToList();
        Assert.Equal(20, focusable.OfType<TextBox>().Count() + focusable.OfType<Slider>().Count() + focusable.OfType<ButtonBase>().Count());
        Assert.All(focusable, c => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(c)), c.GetType().Name + " " + c.Name));
        Assert.Equal(
            new[] { "Flip horizontally.", "Flip vertically.", "Duplicate layer.", "Move layer up.", "Move layer down." },
            new[] { "FlipHButton", "FlipVButton", "DuplicateButton", "UpButton", "DownButton" }.Select(n => (string)Named<Button>(view, n).ToolTip));
        Assert.Null(view.FindName("CenterFrontButton"));             // props-center-front is M4 (interface R13)
        Assert.DoesNotContain(Descendants<Button>(view), b => b.Content as string == "Center on front torso");
    });

    [Fact] // the Expander exposes expand/collapse to assistive tech and opens with ▼ (editor.js:862-872)
    public void The_adjustments_expander_opens_from_the_view_model_and_for_assistive_tech() => StaThread.Run(() =>
    {
        var (_, props, view) = Make();
        var adjustments = Named<Expander>(view, "Adjustments");
        var expand = (IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(adjustments).GetPattern(PatternInterface.ExpandCollapse);
        expand.Expand();
        Layout(view);
        Assert.True(props.AdjustmentsExpanded);
        Assert.Equal("\u25BC", Descendants<TextBlock>(adjustments).First(t => t.Name == "Arrow").Text);
        Assert.True(Shown(Named<Slider>(view, "BrightnessSlider")));
    });

    [Fact] // one slider press is one step however many values it passes through (spec §5)
    public void A_slider_press_is_one_step() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var scale = Named<Slider>(view, "ScaleSlider");
        scale.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        foreach (var v in new[] { 120.0, 150, 180, 140 }) scale.Value = v;
        Assert.Equal(0, session.UndoCount);
        scale.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        Assert.Equal((1.4, 1), (session.SelectedLayer!.Scale, session.UndoCount));

        scale.Value = 150;                                          // a keyboard step outside a press: its own step
        Assert.Equal(2, session.UndoCount);
    });

    [Fact] // props-name-xy: Enter commits (4.1's change event); other keys do not
    public void Enter_commits_a_typed_field() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var name = Named<TextBox>(view, "NameBox");
        name.Text = "Logo";
        Assert.False(LayerPropertiesPanel.HandleTextKey(name, Key.A));
        Assert.Equal("drip_red", session.SelectedLayer!.Name);
        Assert.True(LayerPropertiesPanel.HandleTextKey(name, Key.Enter));
        Assert.Equal(("Logo", 1), (session.SelectedLayer!.Name, session.UndoCount));
    });

    [Fact] // Review Focus: a press anywhere else commits typing first (a browser blurs the input), so a canvas pick never eats it
    public void A_press_elsewhere_commits_typing_before_it_acts_and_a_press_inside_does_not() => StaThread.Run(() =>
    {
        var (session, _, view) = Make(null, LayerOf(1) with { Name = "Back" }, LayerOf(2) with { Name = "drip_red", X = 5 });
        var name = Named<TextBox>(view, "NameBox");
        var x = Named<TextBox>(view, "XBox");
        name.Text = "Logo";
        x.Text = "40";

        view.CommitTextEdits(name);                                 // a press inside the name box commits only X
        Assert.Equal(("drip_red", 40.0, 1), (session.SelectedLayer!.Name, session.SelectedLayer!.X, session.UndoCount));

        view.CommitTextEdits(new Border());                         // a press on the canvas: the name lands on layer 2 ...
        session.Select(1);                                          // ... before the canvas pick changes the selection
        Layout(view);
        Assert.Equal("Logo", session.Document.Layers.Single(l => l.Id == 2).Name);
        Assert.Equal(("Back", 2), (name.Text, session.UndoCount));

        view.CommitTextEdits(null);                                 // nothing typed: nothing recorded
        Assert.Equal(2, session.UndoCount);
    });

    [Fact] // Review Focus: refused text in X puts the stored value back into the field, and records nothing
    public void Refused_position_text_reverts_the_field() => StaThread.Run(() =>
    {
        var (session, _, view) = Make();
        var x = Named<TextBox>(view, "XBox");
        x.Text = "abc";
        LayerPropertiesPanel.HandleTextKey(x, Key.Enter);
        Assert.Equal(("13", 0), (x.Text, session.UndoCount));
        x.Text = "40.0";
        LayerPropertiesPanel.HandleTextKey(x, Key.Enter);
        Assert.Equal(("40", 40.0, 1), (x.Text, session.SelectedLayer!.X, session.UndoCount));
    });

    [Fact] // props-live-refresh, props-panel-rebuild-resets: an edit elsewhere updates the same controls; nothing is rebuilt or collapsed
    public void Edits_update_the_same_controls_and_keep_the_adjustments_open() => StaThread.Run(() =>
    {
        var (session, props, view) = Make();
        props.AdjustmentsExpanded = true;
        Layout(view);
        var x = Named<TextBox>(view, "XBox");
        var rot = Named<Slider>(view, "RotationSlider");

        using (session.BeginGesture())                              // a canvas drag: one Apply per move (Task 2)
        {
            session.Apply(d => LayerOps.MoveTo(d, 1, 77, 30));
            session.Apply(d => LayerOps.SetRotation(d, 1, -44.6));
        }
        props.ResetAdjustmentsCommand.Execute(null);
        Layout(view);

        Assert.Same(x, Named<TextBox>(view, "XBox"));
        Assert.Same(rot, Named<Slider>(view, "RotationSlider"));
        Assert.Equal(("77", -44.6, "-45\u00B0"), (x.Text, rot.Value, Named<Run>(view, "RotationText").Text));
        Assert.True(Named<Expander>(view, "Adjustments").IsExpanded);
    });

    [Fact] // the panel sits in the left panel after the layers, as editor.html:311-323 orders them
    public void The_main_window_puts_the_panel_after_the_layers() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        var children = ((StackPanel)window.FindName("LeftPanelContent")).Children.Cast<FrameworkElement>().ToList();
        Assert.Equal(new[] { typeof(DropZone), typeof(LayersPanel), typeof(LayerPropertiesPanel) }, children.Select(c => c.GetType()));
        Assert.Same(main.LayerProperties, children[2].DataContext);
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'LayerPropertiesPanel' could not be found` and `error CS1061: 'MainViewModel' does not contain a definition for 'LayerProperties'` in `LayerPropertiesPanelTests.cs`.

- [ ] **Step 7: Implement the panel and wire it into the shell**

Create `src/RBX15.App/Views/LayerPropertiesPanel.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.LayerPropertiesPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- "Selected layer" (editor.html:320-323; updateUI's controls, editor.js:746-912; ledger layers-selected-panel).
         DataContext: LayerPropertiesViewModel. Built once and bound, never rebuilt (props-panel-rebuild-resets).
         "Center on front torso" (editor.js:776-778, props-center-front) is M4's Layout tab and is absent.
         The sliders keep WPF's own chrome, as the Fill Style sliders do: 4.1's accent-color (styles.css:333,
         editor.js:795-799) is part of the M3b look sign-off. -->
    <UserControl.Resources>
        <!-- .controls label (styles.css:324-332): mono 10px fg-3, margin 8px 0 3px, uppercase (written upper case). -->
        <Style x:Key="PropLabel" TargetType="TextBlock">
            <Setter Property="FontFamily" Value="{DynamicResource FontMono}" />
            <Setter Property="FontSize" Value="10" />
            <Setter Property="Foreground" Value="{DynamicResource Fg3Brush}" />
            <Setter Property="Margin" Value="0,8,0,3" />
        </Style>
        <!-- .controls input[type=text|number] (styles.css:335-352): navy, 1px border-1, fg-1, padding 5px 8px,
             radius r-xs 4, 12px body font; focus border cyan. -->
        <Style x:Key="PropTextBox" TargetType="TextBox">
            <Setter Property="FontFamily" Value="{DynamicResource FontBody}" />
            <Setter Property="FontSize" Value="12" />
            <Setter Property="Foreground" Value="{DynamicResource Fg1Brush}" />
            <Setter Property="CaretBrush" Value="{DynamicResource Fg1Brush}" />
            <Setter Property="Background" Value="{DynamicResource BrandNavyBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource Border1Brush}" />
            <Setter Property="Padding" Value="8,5" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TextBox">
                        <Border x:Name="Box" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="1" CornerRadius="{DynamicResource RXsRadius}" SnapsToDevicePixels="True">
                            <ScrollViewer x:Name="PART_ContentHost" Padding="{TemplateBinding Padding}" Focusable="False"
                                          HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsKeyboardFocused" Value="True">
                                <Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource BrandCyanBrush}" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <!-- .controls input[type=range] (styles.css:333): full width, whole-number steps (a step-1 range).
             PageUp/PageDown and a track press move a tenth of the range: Chromium's range big step,
             max((max - min) / 10, step) (RangeInputType::HandleKeydownEvent), snapped to a whole number.
             WPF's track press steps toward the point instead of jumping to it (IsMoveToPointEnabled marks the
             press handled and ends the drag there); the Fill Style sliders behave the same way. -->
        <Style x:Key="PropSlider" TargetType="Slider">
            <Setter Property="SmallChange" Value="1" />
            <Setter Property="TickFrequency" Value="1" />
            <Setter Property="IsSnapToTickEnabled" Value="True" />
        </Style>
    </UserControl.Resources>

    <StackPanel>
        <TextBlock Text="Selected layer" Style="{DynamicResource PanelHeading}" />

        <!-- The empty state (editor.js:750; layers-empty-state): .info, mono 10px fg-muted, margin 4px 0,
             uppercase (styles.css:640-647). -->
        <TextBlock x:Name="EmptyState" Text="{Binding EmptyText, Mode=OneTime, Converter={StaticResource UpperCase}}"
                   FontFamily="{DynamicResource FontMono}" FontSize="10" Foreground="{DynamicResource FgMutedBrush}" Margin="0,4">
            <TextBlock.Style>
                <Style TargetType="TextBlock">
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding HasSelection}" Value="True">
                            <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </TextBlock.Style>
        </TextBlock>

        <StackPanel x:Name="Controls">
            <StackPanel.Style>
                <Style TargetType="StackPanel">
                    <Setter Property="Visibility" Value="Collapsed" />
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding HasSelection}" Value="True">
                            <Setter Property="Visibility" Value="Visible" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </StackPanel.Style>

            <!-- Name (editor.js:754-755, :825): commits on Enter or focus loss. -->
            <TextBlock Style="{StaticResource PropLabel}" Text="NAME" />
            <TextBox x:Name="NameBox" Style="{StaticResource PropTextBox}" KeyDown="OnTextKeyDown"
                     Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" AutomationProperties.Name="Layer name" />

            <!-- X and Y (editor.js:756-760, :826-827): "X: n   Y: n", then a .row of two fields, gap 8 (styles.css:369-370). -->
            <TextBlock x:Name="PositionText" Style="{StaticResource PropLabel}" Text="{Binding PositionLabel, Mode=OneWay}" />
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="8" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBox x:Name="XBox" Grid.Column="0" Style="{StaticResource PropTextBox}" KeyDown="OnTextKeyDown"
                         Text="{Binding XText, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" AutomationProperties.Name="X position" />
                <TextBox x:Name="YBox" Grid.Column="2" Style="{StaticResource PropTextBox}" KeyDown="OnTextKeyDown"
                         Text="{Binding YText, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" AutomationProperties.Name="Y position" />
            </Grid>

            <!-- Scale 1..500 (editor.js:761-762), Rotation -180..180 (:763-764), Opacity 0..100 (:765-766). -->
            <TextBlock Style="{StaticResource PropLabel}"><Run Text="SCALE: " /><Run x:Name="ScaleText" Text="{Binding ScaleLabel, Mode=OneWay}" /></TextBlock>
            <Slider x:Name="ScaleSlider" Style="{StaticResource PropSlider}" Minimum="1" Maximum="500" LargeChange="50"
                    Value="{Binding ScalePercent}" AutomationProperties.Name="Scale percent" />
            <TextBlock Style="{StaticResource PropLabel}"><Run Text="ROTATION: " /><Run x:Name="RotationText" Text="{Binding RotationLabel, Mode=OneWay}" /></TextBlock>
            <Slider x:Name="RotationSlider" Style="{StaticResource PropSlider}" Minimum="-180" Maximum="180" LargeChange="36"
                    Value="{Binding Rotation}" AutomationProperties.Name="Rotation degrees" />
            <TextBlock Style="{StaticResource PropLabel}"><Run Text="OPACITY: " /><Run x:Name="OpacityText" Text="{Binding OpacityLabel, Mode=OneWay}" /></TextBlock>
            <Slider x:Name="OpacitySlider" Style="{StaticResource PropSlider}" Minimum="0" Maximum="100" LargeChange="10"
                    Value="{Binding OpacityPercent}" AutomationProperties.Name="Opacity percent" />

            <!-- Flip H, Flip V, Duplicate (editor.js:767-771): .row, margin-top 6, btn-sm btn-secondary. -->
            <Grid Margin="0,6,0,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="8" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="8" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Button x:Name="FlipHButton" Grid.Column="0" Style="{DynamicResource SecondaryButtonSm}" Command="{Binding FlipHCommand}"
                        Content="Flip H" ToolTip="Flip horizontally." AutomationProperties.Name="Flip horizontally" />
                <Button x:Name="FlipVButton" Grid.Column="2" Style="{DynamicResource SecondaryButtonSm}" Command="{Binding FlipVCommand}"
                        Content="Flip V" ToolTip="Flip vertically." AutomationProperties.Name="Flip vertically" />
                <Button x:Name="DuplicateButton" Grid.Column="4" Style="{DynamicResource SecondaryButtonSm}" Command="{Binding DuplicateCommand}"
                        Content="Duplicate" ToolTip="Duplicate layer." AutomationProperties.Name="Duplicate layer" />
            </Grid>

            <!-- Up, Down (editor.js:772-775): .row, margin-top 4 (moved here from under the list; interface R13). -->
            <Grid Margin="0,4,0,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="8" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Button x:Name="UpButton" Grid.Column="0" Style="{DynamicResource SecondaryButtonSm}" Command="{Binding MoveUpCommand}"
                        Content="Up" ToolTip="Move layer up." AutomationProperties.Name="Move layer up" />
                <Button x:Name="DownButton" Grid.Column="2" Style="{DynamicResource SecondaryButtonSm}" Command="{Binding MoveDownCommand}"
                        Content="Down" ToolTip="Move layer down." AutomationProperties.Name="Move layer down" />
            </Grid>

            <!-- Image adjustments (editor.js:779-803, :861-872): margin-top 8; the header row is padding 4px 0,
                 "Image adjustments" mono 11px 600 cyan uppercase with a cyan 10px arrow, ▶ closed and ▼ open.
                 An Expander, so assistive tech gets the expand/collapse pattern; open state is the view-model's. -->
            <Expander x:Name="Adjustments" Margin="0,8,0,0" Focusable="False" IsExpanded="{Binding AdjustmentsExpanded, Mode=TwoWay}">
                <Expander.Template>
                    <ControlTemplate TargetType="Expander">
                        <DockPanel>
                            <ToggleButton x:Name="AdjToggle" DockPanel.Dock="Top" Padding="0,4" Cursor="Hand"
                                          FocusVisualStyle="{DynamicResource FocusRing}" AutomationProperties.Name="Image adjustments"
                                          IsChecked="{Binding IsExpanded, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                                <ToggleButton.Template>
                                    <ControlTemplate TargetType="ToggleButton">
                                        <Border Background="Transparent" Padding="{TemplateBinding Padding}">
                                            <DockPanel>
                                                <TextBlock x:Name="Arrow" DockPanel.Dock="Right" Text="&#x25B6;" FontSize="10"
                                                           Foreground="{DynamicResource BrandCyanBrush}" VerticalAlignment="Center" />
                                                <TextBlock Text="IMAGE ADJUSTMENTS" FontFamily="{DynamicResource FontMono}" FontSize="11"
                                                           FontWeight="SemiBold" Foreground="{DynamicResource BrandCyanBrush}" />
                                            </DockPanel>
                                        </Border>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property="IsChecked" Value="True">
                                                <Setter TargetName="Arrow" Property="Text" Value="&#x25BC;" />
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </ToggleButton.Template>
                            </ToggleButton>
                            <ContentPresenter x:Name="ExpandSite" Visibility="Collapsed" />
                        </DockPanel>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsExpanded" Value="True">
                                <Setter TargetName="ExpandSite" Property="Visibility" Value="Visible" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Expander.Template>
                <StackPanel>
                    <!-- Brightness, Contrast, Saturation 0..200 %, Hue rotate 0..360 ° (editor.js:785-792). -->
                    <TextBlock Style="{StaticResource PropLabel}"><Run Text="BRIGHTNESS: " /><Run x:Name="BrightnessText" Text="{Binding BrightnessLabel, Mode=OneWay}" /></TextBlock>
                    <Slider x:Name="BrightnessSlider" Style="{StaticResource PropSlider}" Minimum="0" Maximum="200" LargeChange="20"
                            Value="{Binding Brightness}" AutomationProperties.Name="Brightness percent" />
                    <TextBlock Style="{StaticResource PropLabel}"><Run Text="CONTRAST: " /><Run x:Name="ContrastText" Text="{Binding ContrastLabel, Mode=OneWay}" /></TextBlock>
                    <Slider x:Name="ContrastSlider" Style="{StaticResource PropSlider}" Minimum="0" Maximum="200" LargeChange="20"
                            Value="{Binding Contrast}" AutomationProperties.Name="Contrast percent" />
                    <TextBlock Style="{StaticResource PropLabel}"><Run Text="SATURATION: " /><Run x:Name="SaturateText" Text="{Binding SaturateLabel, Mode=OneWay}" /></TextBlock>
                    <Slider x:Name="SaturateSlider" Style="{StaticResource PropSlider}" Minimum="0" Maximum="200" LargeChange="20"
                            Value="{Binding Saturate}" AutomationProperties.Name="Saturation percent" />
                    <TextBlock Style="{StaticResource PropLabel}"><Run Text="HUE ROTATE: " /><Run x:Name="HueText" Text="{Binding HueLabel, Mode=OneWay}" /></TextBlock>
                    <Slider x:Name="HueSlider" Style="{StaticResource PropSlider}" Minimum="0" Maximum="360" LargeChange="36"
                            Value="{Binding HueRotate}" AutomationProperties.Name="Hue rotate degrees" />

                    <!-- R, G, B shift -100..100 (editor.js:793-800): margin-top 6; labels #ff5472, #4f4, #66f. -->
                    <StackPanel Margin="0,6,0,0">
                        <TextBlock Style="{StaticResource PropLabel}" Foreground="#FF5472"><Run Text="R: " /><Run x:Name="RText" Text="{Binding RLabel, Mode=OneWay}" /></TextBlock>
                        <Slider x:Name="RSlider" Style="{StaticResource PropSlider}" Minimum="-100" Maximum="100" LargeChange="20"
                                Value="{Binding RShift}" AutomationProperties.Name="Red shift" />
                        <TextBlock Style="{StaticResource PropLabel}" Foreground="#44FF44"><Run Text="G: " /><Run x:Name="GText" Text="{Binding GLabel, Mode=OneWay}" /></TextBlock>
                        <Slider x:Name="GSlider" Style="{StaticResource PropSlider}" Minimum="-100" Maximum="100" LargeChange="20"
                                Value="{Binding GShift}" AutomationProperties.Name="Green shift" />
                        <TextBlock Style="{StaticResource PropLabel}" Foreground="#6666FF"><Run Text="B: " /><Run x:Name="BText" Text="{Binding BLabel, Mode=OneWay}" /></TextBlock>
                        <Slider x:Name="BSlider" Style="{StaticResource PropSlider}" Minimum="-100" Maximum="100" LargeChange="20"
                                Value="{Binding BShift}" AutomationProperties.Name="Blue shift" />
                    </StackPanel>

                    <!-- Reset adjustments (editor.js:801): full width, margin-top 6, border #f22f89, text #ff5aa3. -->
                    <Button x:Name="ResetButton" Margin="0,6,0,0" Style="{DynamicResource SecondaryButtonSm}" Command="{Binding ResetAdjustmentsCommand}"
                            BorderBrush="{DynamicResource BrandMagentaBrush}" Foreground="{DynamicResource BrandMagentaBrightBrush}"
                            Content="Reset adjustments" AutomationProperties.Name="Reset adjustments" />
                </StackPanel>
            </Expander>
        </StackPanel>
    </StackPanel>
</UserControl>
```

Create `src/RBX15.App/Views/LayerPropertiesPanel.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The "Selected layer" panel. Every slider shares one <see cref="SliderGestureTracker"/>, so a slider drag
/// is one undo step. The three text fields commit on Enter, on focus loss, and on any mouse press elsewhere
/// in the window: a browser blurs a focused input on a press anywhere, so 4.1's change event fired before
/// the press acted (props-name-xy), but the WPF canvas takes no keyboard focus, and without this a typed
/// name or position would be overwritten, uncommitted, when a canvas click selects another layer.
/// </summary>
public partial class LayerPropertiesPanel : UserControl
{
    readonly SliderGestureTracker _tracker;
    Window? _window;

    public LayerPropertiesPanel() : this(SliderGestureTracker.RealLeftButtonDown) { }

    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    internal LayerPropertiesPanel(Func<bool> leftButtonDown)
    {
        _tracker = new SliderGestureTracker(() => (DataContext as LayerPropertiesViewModel)?.BeginSliderDrag(), leftButtonDown);
        InitializeComponent();
        foreach (var slider in Sliders) _tracker.Attach(slider);
        TextFields = [NameBox, XBox, YBox];
        Loaded += (_, _) => HookWindow(Window.GetWindow(this));
        Unloaded += (_, _) => { HookWindow(null); _tracker.End(); };
        DataContextChanged += (_, _) => _tracker.End();
    }

    /// <summary>The ten sliders, in panel order.</summary>
    internal IReadOnlyList<Slider> Sliders =>
        [ScaleSlider, RotationSlider, OpacitySlider, BrightnessSlider, ContrastSlider, SaturateSlider, HueSlider, RSlider, GSlider, BSlider];

    /// <summary>Name, X and Y.</summary>
    internal IReadOnlyList<TextBox> TextFields { get; }

    internal SliderGestureTracker Tracker => _tracker;

    void HookWindow(Window? window)
    {
        if (_window is not null) _window.PreviewMouseDown -= OnWindowPreviewMouseDown;
        _window = window;
        if (_window is not null) _window.PreviewMouseDown += OnWindowPreviewMouseDown;
    }

    void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e) => CommitTextEdits(e.OriginalSource as DependencyObject);

    void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleTextKey((TextBox)sender, e.Key)) e.Handled = true;
    }

    /// <summary>Enter commits the field (4.1's change event fires on Enter). True when handled.</summary>
    internal static bool HandleTextKey(TextBox box, Key key)
    {
        if (key != Key.Enter) return false;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        return true;
    }

    /// <summary>
    /// Commits every text field with uncommitted typing, except the one <paramref name="pressed"/> is inside.
    /// A field with nothing typed is left alone; its commit would change nothing anyway.
    /// </summary>
    internal void CommitTextEdits(DependencyObject? pressed)
    {
        foreach (var box in TextFields)
        {
            if (pressed is not null && IsWithin(pressed, box)) continue;
            if (box.GetBindingExpression(TextBox.TextProperty) is { IsDirty: true } binding) binding.UpdateSource();
        }
    }

    static bool IsWithin(DependencyObject node, DependencyObject ancestor)
    {
        for (var d = node; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (ReferenceEquals(d, ancestor)) return true;
        return false;
    }
}
```

In `src/RBX15.App/ViewModels/MainViewModel.cs`, after the `Status` member:

```csharp
    /// <summary>The status strip (Task 4).</summary>
    public required StatusViewModel Status { get; init; }
```

insert:

```csharp

    /// <summary>The left panel's "Selected layer" section (M3b Task 3; editor.html:320-323).</summary>
    public required LayerPropertiesViewModel LayerProperties { get; init; }
```

In `src/RBX15.App/Composition.cs` (interface sheet §2, construction order step 5: after layers, before home), insert directly above `        var home = new HomeViewModel(session, colorPicker)` (the line Task 2 Step 6 prints in its full Composition):

```csharp
        // M3b Task 3: the "Selected layer" panel (editor.html:320-323).
        var layerProperties = new LayerPropertiesViewModel(session);
```

and directly above `            // M3a: required members above this line.`:

```csharp
            LayerProperties = layerProperties,
```

In `src/RBX15.App/MainWindow.xaml`, inside `LeftPanelContent`, replace:

```xml
            <views:LayersPanel DataContext="{Binding Layers}" />
```

with:

```xml
            <views:LayersPanel DataContext="{Binding Layers}" />
            <views:LayerPropertiesPanel DataContext="{Binding LayerProperties}" />
```

In `src/RBX15.App.Tests/ThemeKeyGuardTests.cs`, replace the `Assert.Equal(14, views.Count); ...` line (14 after Task 2's ToolsGroup.xaml and BrushGroup.xaml; see "Additions requested") with:

```csharp
        Assert.Equal(15, views.Count); // the 10 M3a view files, ColorPickerPopup.xaml, MainWindow.xaml, ToolsGroup.xaml and BrushGroup.xaml (M3b Task 2), LayerPropertiesPanel.xaml (M3b Task 3)
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.LayerPropertiesPanelTests|FullyQualifiedName~RBX15.App.Tests.ThemeKeyGuardTests"`
Expected: `Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12` for `RBX15.App.Tests.dll` (LayerPropertiesPanelTests 11, ThemeKeyGuardTests 1).

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`; App.Tests `Passed: 557, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`.

Commit (end with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/Views/LayerPropertiesPanel.xaml src/RBX15.App/Views/LayerPropertiesPanel.xaml.cs src/RBX15.App/ViewModels/MainViewModel.cs src/RBX15.App/Composition.cs src/RBX15.App/MainWindow.xaml src/RBX15.App.Tests/LayerPropertiesPanelTests.cs src/RBX15.App.Tests/ThemeKeyGuardTests.cs
git commit -m "feat(app): the Selected layer panel with adjustments, flip, duplicate and Up/Down, committed per edit"
```

- [ ] **Step 8: Verify the task**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run the suite three times in a row: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected each time: Engine.Tests `Passed: 877`; App.Tests `Passed: 557`; Parity.Tests `Passed: 35`; `Failed: 0` and `Skipped: 0` everywhere.

Run: `git status --short`
Expected: empty (three commits in this task, nothing left over).

**Additions requested by Task 3** (not in interfaces.md; adopted):

1. `LayerPropertiesViewModel` constants: `MinScalePercent` and `MaxScalePercent` (defined as `SelectionGeometry.MinScale * 100` and `MaxScale * 100`), `MinRotation`, `MaxRotation`, `MaxPercentAdjust`, `MaxHue`, `MinShift`, `MaxShift`; and `internal static string ToFixed0(double)` and `WholeDegrees(double)`.
2. `SliderGestureTracker` beyond the sheet: `bool IsOpen`, `static bool RealLeftButtonDown()`, and `void Attach(Slider)` (wires the five events; the press and release with `handledEventsToo`).
3. `LayerPropertiesPanel` seams: `internal LayerPropertiesPanel(Func<bool> leftButtonDown)`, `internal IReadOnlyList<Slider> Sliders`, `internal IReadOnlyList<TextBox> TextFields`, `internal SliderGestureTracker Tracker`, `internal static bool HandleTextKey(TextBox, Key)`, `internal void CommitTextEdits(DependencyObject? pressed)`; and the window `PreviewMouseDown` hook (ruling 7).
4. `PositionLabel` text is 4.1's: "X: 12", a space, a no-break space, a space, "Y: 34" (editor.js:756, `&nbsp;` between two spaces), not the sheet's two-space example.
5. `BeginSliderDrag()` never returns null (the sheet's signature allows it; the session always opens a scope).
6. **Task 2** bumps `ThemeKeyGuardTests`'s view count from 12 to 14 when it adds ToolsGroup.xaml and BrushGroup.xaml (Task 2 Step 5); Step 7 here sets 15.
7. **Task 5** (carried out there as hand check H6b and the state 03 captions; critic mn7, mn8): add to Este's manual checklist "Type a new name in the panel, then click another layer on the canvas: the typed name lands on the first layer as one undo step"; close or narrow `layers-empty-state`, `props-*`, `layers-selected-panel` under the honesty rule (their parityChecks are ui-auto; this task's evidence is unit and view tests plus the look sign-off state 03); list ruling 11's look items in the sign-off captions; record ruling 10 (track step, not jump) as a divergence if Este asks for the jump.
8. **Sheet citation corrections** (the plan cites the real lines): SetName is editor.js:825 (the sheet says :813), ResetAdjustments is :908-912 (sheet :898-902), the default adjustments are :909-910, and the adjustment ranges are :786-799.

---

### Task 4: Layers panel drag reorder, the shell look notes and the shell carry-forwards

**Files:**

- Modify (app):
  - `src/RBX15.App/ViewModels/LayerRowViewModel.cs`: the `DropIndicator` enum, plus `IsDragSource` and `DropIndicator`.
  - `src/RBX15.App/ViewModels/LayersViewModel.cs`: `ReorderHint`, `BeginRowDrag`, `DragOverRow`, `ClearDropTarget`, `CompleteRowDrop`, `EndRowDrag`, and a drag cleanup in `Sync`.
  - `src/RBX15.App/Views/LayersPanel.xaml`, printed in full: the hint, the drop edges and the dimmed source. The M3a Up/Down pair is removed (R13).
  - `src/RBX15.App/Views/LayersPanel.xaml.cs`, printed in full: the drag and the keys.
  - `src/RBX15.App/Views/DropZone.xaml`: the label wraps, and the margin carries the border.
- Create (app tests): `src/RBX15.App.Tests/LayersReorderTests.cs`, `src/RBX15.App.Tests/LayersPanelDragTests.cs`, `src/RBX15.App.Tests/ShellLookTests.cs`.
- Modify (app tests): `src/RBX15.App.Tests/Task4ViewTests.cs`. Up and Down are gone from the list, and the hint is present.
- Modify (shell, Step 11; critic MJ2): `src/RBX15.App/MainWindow.xaml` gains the window's one `KeyBinding`, Ctrl+Z to the Edit group's Undo, and `src/RBX15.App.Tests/ShellTests.cs` gains its fact (+1).
- Read only:
  - 4.1: `editor.js`, `editor.html`, `styles.css`, untouched.
  - `docs/port/parity-ledger.json`.
  - Task 3's `src/RBX15.App/Views/FillStyleGroup.xaml.cs` and `SliderGestureTrackerTests` (Step 1 check).
- Not touched:
  - the engine, `Composition.cs`, `HomeRibbon.xaml` and `RegionsGrid.xaml` (`MainWindow.xaml` changes only by Step 11's `Window.InputBindings`). The ribbon and canvas-height look notes are measured rulings pinned by tests, with no layout change (T4-R8, T4-R9).
  - The ledger and `divergences.md` (Task 5, from the handoff at the end).

**Interfaces:**

- Consumes:
  - **Task 1:**
    - `LayerOps.DropIndices(Document doc, int dragId, int targetId, bool above) : (int From, int To)?`
    - `LayerOps.Reorder(Document doc, int fromIndex, int toIndex) : Document`, which returns the same instance when from == to or an index is out of range.
  - **M3a:**
    - `EditorSession`: `Apply` (returns true iff the content changed), `Undo`, `UndoCount`, `SelectedLayerId`, `Changed`.
    - `LayersViewModel`: `Rows`, `Selected`, `MoveUpCommand`, `MoveDownCommand`, `Sync`.
    - `LayerRowViewModel` (`Id`, `Name`), `ImportService`, `FileGroupViewModel`, `MainWindow`, `ShellLayout.PanelWidthFor`.
    - Test helpers and fakes: `StaThread.Run`, `StaThread.Pump`, `TestShell.Main`, `FakeStatus`, `FakeFileDialogs`, `FakeDialogService`, `FakeSettingsStore`, `RepoRoot.Find` (RBX15.Parity).
    - Theme keys `PanelHeading`, `FocusRing`, `Fg2Brush` and `FontBody`, all existing; no new key.
  - **Task 2:** `LayersViewModel.LayerPicked`, raised by the `Selected` setter. `HandleKey` sets `Selected`, so an Alt+arrow pick reverts the tool to Select per R8. It is not called directly.
  - **Task 3:** Up and Down are in the Selected layer panel (R13), so the M3a pair here is removed. `SliderGestureTracker` carries the M3a `Begin(false)` fix; Step 1 checks that it landed.
- Produces (interfaces.md §2; "+" marks an addition listed at the end):
  - `public enum DropIndicator { None, Above, Below }` in `LayerRowViewModel.cs`.
  - On `LayerRowViewModel`: `bool IsDragSource { get; internal set; }` and `DropIndicator DropIndicator { get; internal set; }` (+ the internal setters).
  - On `LayersViewModel`:
    - `string ReorderHint`.
    - `void BeginRowDrag(int id)`.
    - `void DragOverRow(int targetId, bool above)`. On the dragged row it clears the target (+ clarification).
    - `void ClearDropTarget()`.
    - `bool CompleteRowDrop()`: true iff the order changed, and it always ends the drag (+ clarification).
    - `void EndRowDrag()`.
  - On `LayersPanel`, the internal seams (+): `HalfGap`, `IsDragging`, `HandleRowPress(LayerRowViewModel?, Point)`, `HandleMove(Point, bool)`, `HandleRelease(Point)`, `HandleCaptureLost()`, `HandleKey(Key, ModifierKeys, LayerRowViewModel?)`, `RowAt(Point) : (int Id, bool Above)?` and `static RowFrom(DependencyObject?, bool)`.
  - Named parts (+): `LayersHeading`, `ReorderHintRun`, and the item template's `DropAbove` and `DropBelow`.
  - Test classes:
    - `LayersReorderTests` (32 cases).
    - `LayersPanelDragTests` (15).
    - `ShellLookTests` (5, +).
    - `ShellTests.Ctrl_Z_on_the_window_runs_the_Edit_groups_undo` (+1; critic MJ2).
  - `MainWindow`'s `Window.InputBindings`: one `KeyBinding`, Ctrl+Z to `Home.Edit.UndoCommand` (the contract's one in-scope shortcut).
    - `Task4ViewTests`: one fact rewritten, one edited; the count is unchanged.

**What 4.1 does (cited).** These are the shipped `editor.js`, `editor.html` and `styles.css`, 4.1, untouched.

- **The row.** Every row is `draggable = true` (editor.js:680). Its listeners are `dragstart`, `dragover`, `drop` and `dragend` (editor.js:738-741).
- **handleLayerDragStart** (editor.js:591-600):
  - Sets `layerDragId` and `effectAllowed = 'move'`.
  - On the next tick (`setTimeout`, :595-599), adds `.dragging-layer`, so the native drag image is captured before the dimming (ledger `layer-reorder-dragging-class-timer`).
- **handleLayerDragOver** (editor.js:602-619):
  - Over the dragged row it returns early and keeps the old target (:605). This is the stale target.
  - Otherwise it reads the row's rect and midpoint (:607-608): `pos = clientY < midY ? 'top' : 'bottom'` (:609).
  - It clears every indicator (:611-614), adds `.drag-over-top` or `.drag-over-bottom` to this row (:616), and stores `layerDragOverId` and `layerDragPos` (:617-618).
- **handleLayerDrop** (editor.js:621-657):
  - No drag, no target, or the target is the dragged row: it cleans up and returns before `saveUndo` (:625-628).
  - Otherwise `saveUndo()` (:630), even when the order ends up the same.
  - Splices the moved layer out (:640), finds the target again (:645), and inserts after it for `'top'` or before it for `'bottom'` (:646-652). Then cleanup, `updateUI` and `render` (:654-656).
  - The selection is untouched.
- **cleanupLayerDrag** (editor.js:659-666): clears the ids and all three classes. `dragend` calls it (:741), which is also how Esc cancels an HTML5 drag.
- **Styles** (styles.css):
  - `.layer-list`: gap 4px (:287).
  - `.layer-item`: `cursor: grab` (:296), `:active { cursor: grabbing }` (:303).
  - `.drag-over-top` and `.drag-over-bottom`: `border-top` or `border-bottom: 2px solid var(--brand-cyan)` (:310-311).
  - `.dragging-layer`: `opacity: 0.4` (:312).
- **The heading** (editor.html:317): `<h3>Layers <span style="font-size:10px;color:#555;text-transform:none">(drag to reorder)</span></h3>`. The `.panel h3` it sits in is JetBrains Mono 11px fg-3, weight 600, uppercase, letter-spacing .12em (styles.css:254-263).
- **Up and Down** (editor.js:773-774, :846-853) live in the Selected layer panel, not under the list.
- **Keys** (editor.js:1775-1830): there is no reorder key. The arrows nudge the selected layer 1 px, or 10 with Shift (:1807-1813), and Alt is not excluded (the guard at :1806 skips only Ctrl and Meta).
- **The drop zone** (styles.css:265-276; editor.html:311-315):
  - A 2px dashed border with padding 24px 12px around 13px text that wraps.
  - At 280 px the panel's zone is 251x84 and at 200 px it is 161x100 (measured, below).
- **Tooltips:** native `title=` tooltips, shown on hover only (ledger `cross-text-select-and-selection`, keep).

**Ledger rows this task serves.** The dispositions are followed; Task 5 closes rows under the honesty rule.

- `layers-reorder-drag` (fix): the stale target is cleared.
- `layers-reorder-drag-2` (fix): reorder is keyboard operable, uses the same above/below z semantics, and is one step.
- `layer-reorder-dragging-class-timer` (keep): the source is dimmed to .4, and the edge is drawn at the top or bottom.
- `undo-empty-steps` and `undo-coverage-gaps` (fix): a drop that keeps the order records nothing.
- `layers-list` and `layers-list-render` (fix): the hint, and keyboard-operable rows.
- `panel-drop-zone` (fix): the label clip.
- `ribbon-overflow-hscroll` (keep), `shell-layout-skeleton` (fix, unchanged here), `cross-text-select-and-selection` (keep) and `shell-window-size` (fix): the look notes.

**Rulings** (each pinned by a named test):

- **T4-R1: the mouse drag is in-process and capture-based, not OLE `DoDragDrop`.**
  - interfaces.md §2 is binding ("capture-based and in-process (no OLE DoDragDrop)"). It wins over the computed brief's "WPF drag-drop" wording and the ledger portTarget's `DragDrop.DoDragDrop`.
  - `DoDragDrop` runs a modal OLE loop. Tests can only reach that through synthetic desktop input, which the rules forbid. It would also put an OLE drag source inside the window whose external-drop guard (`shell-external-drop-guard`) refuses unknown data.
  - Capture gives the same press, move-past-distance, hover, release and Esc semantics, with seams the tests drive.
  - Pinned by every `LayersPanelDragTests` drag fact.
- **T4-R2: the stale-target fix.** There are three parts:
  - Over the dragged row there is no target. `DragOverRow` on the source clears it; 4.1 returned early at :605 and kept it.
  - Off every row there is no target (`ClearDropTarget`).
  - The release re-reads the row under the release point, so a wheel scroll or a fast release never drops on the last move's row.
  - Pinned by Review Focus 1's tests.
- **T4-R3: pressing a row still selects it** (the M3a ListBox's press, and Task 2's R8 tool revert). A dragged row therefore ends up selected; the reorder itself never changes the selection. 4.1 selected only on `click`, which a drag suppresses.
  - Task 5 adds to `layers-reorder-drag-2`'s divergence: "pressing a row selects it, so a dragged row ends selected".
  - Pinned by the theory's `Assert.Equal(4, session.SelectedLayerId)`, the selection through a drop.
- **T4-R4: a press on a row's Dupe, Hide or Del never starts a drag.** It stays a button press with the button's own capture. Chromium would let a drag start on a button inside a draggable row; v5 keeps the button predictable.
  - Pinned by `LayersPanelDragTests.Only_a_rows_body_arms_a_drag_and_its_buttons_do_not`.
- **T4-R5: each row owns half of the 4 px gap on either side** (`HalfGap = 2`). Crossing a gap never flickers the edge off.
  - The half still follows each row's own midpoint (editor.js:607-609).
  - Above the first row and below the last there is no target.
  - Pinned by `Row_at_splits_each_gap_and_uses_the_rows_midpoint`.
- **T4-R6: no edge autoscroll during a drag.** Chromium may autoscroll a scrollable panel while an HTML5 drag hovers its edge. Its timing has no source in the 4.1 files, so v5 invents none.
  - Instead, the wheel scrolls the left panel mid-drag: under capture, the panel is the wheel target and the event bubbles to the left panel's ScrollViewer.
  - The release re-reads the target (T4-R2).
  - Alt+Up and Alt+Down move a row any distance.
  - It is a manual check in the handoff.
- **T4-R7: Alt+Up and Alt+Down on a focused row** select it and run `MoveUpCommand` or `MoveDownCommand` (interfaces.md §2). Each press is one step; at an end the key is used and records nothing; focus follows the row.
  - Plain arrows stay the ListBox's: they move the selection.
  - Shift and Ctrl combinations are not used.
  - Esc cancels a running drag; with no drag it is not handled here.
  - The panel works from `e.SystemKey` when `e.Key == Key.System`, because Alt+arrow arrives as a system key.
  - **M4 note:** 4.1's arrow nudge (editor.js:1810-1813) did not exclude Alt. M4's keyboard must leave Alt+Up and Alt+Down to a focused layer row.
  - Pinned by `Alt_up_and_down_move_the_focused_row_one_step_each_and_focus_follows_it` and `Other_keys_are_left_to_the_list` (5 rows).
- **T4-R8: look note 1, ribbon cut-off: keep** (`ribbon-overflow-hscroll`).
  - 4.1's Home pane is **1432 px** wide in a 1184 px client (measured), so 4.1 cut off Home at 1200x800 too. The ledger quirk "At 1200 px width Home fits" is wrong; Task 5 corrects it.
  - v5's Home scrolls sideways, and no group shrinks. It is 1284 px wide at M3a and **1746 px** (1745.6) with Task 2's Tools (351 px; 4.1: 418) and Brush (110 px; 4.1: 111), re-measured by the reconciler on a scratch copy with Task 2's ribbon files applied (critic mn4).
  - A keyboard-focused tile scrolls into view, so Mode can scroll out. This is the focus-visibility half of `a11y-focus-rings`: kept.
  - Pinned by `ShellLookTests.At_the_default_size_the_ribbon_scrolls_sideways_and_no_group_shrinks`.
- **T4-R9: look note 2, bottom limb faces cut at 100%: keep.**
  - At the 1184x761 client, 4.1's canvas area measures (all measured):
    - Home tab: **558** (ribbon 128), which fits the 559 px canvas within 1 px.
    - Layout tab, which holds Fill Style and Template Regions: **531** (ribbon 155). 4.1 clipped the canvas there by 28 px, with no way to pan.
  - v5's Home carries those two Layout groups until M4. Its ribbon measures 147-150 px, and its area 536 (smoke, 100%) to 540 (measured here at 125%).
  - So v5 gives the canvas more room than 4.1 did with the same groups, and shows a scrollbar instead of clipping (`canvas-zoom-clipped-no-pan`). Reaching 4.1's Home figure would mean moving the Layout groups off Home, which is M4's tab work.
  - Pinned by `ShellLookTests.At_the_default_size_the_canvas_area_is_no_shorter_than_4_1s_with_the_same_groups` (ribbon ≤ 155, area ≥ 531). This also guards against Tasks 2 and 3 making the ribbon taller.
  - Flag for M4: `shell-layout-skeleton`'s fixed ribbon height, if set to the tallest tab (4.1's Text: ribbon 178, area 508), cuts the canvas on every tab.
- **T4-R10: look note 3, lingering untitled windows: keep, now identified.**
  - The default WPF `ToolTip` measures **52.33x25.96** for "Color 1" and **162.21x25.96** for "Add an image file as a layer." (measured). These are exactly the smoke's 52x26 window under Color 1 and its 162x26 window under Add image.
  - They are keyboard-focus tooltips:
    - `ToolTipService.ShowsToolTipOnKeyboardFocus` is unset, so they show on keyboard focus.
    - `ShowDuration` is `int.MaxValue`: they persist until focus moves, Esc or Ctrl (WCAG 1.4.13).
    - They opened because focus came back by keyboard: Esc closed the picker, and Enter closed the Open dialog.
  - 4.1 had hover-only `title=` tooltips. v5 shows the same text to keyboard users. That is accessible and consistent with the cyan focus ring (`a11y-focus-rings`).
  - Task 5 adds this to `cross-text-select-and-selection` as a divergence.
  - Pinned by `ShellLookTests.The_lingering_windows_are_the_persistent_keyboard_focus_tooltips`.
- **T4-R11: look note 4, drop zone label clip: fix** (`panel-drop-zone`). The label gets `TextWrapping="Wrap"`, as 4.1's div wraps. The margin becomes `14,26`, because the dashed `Rectangle`'s 2 px stroke takes no layout room, while CSS's border does (styles.css:266-268).
  - The zone in the 280 panel goes from 251x79 to **251x83.2**; 4.1 is 251x84.
  - In the 200 panel the label wraps to three lines: **171x99.2**; 4.1 is 161x100. The 10 px width difference is 4.1's visible panel scrollbar at 624x441.
  - Pinned by `ShellLookTests.The_drop_zone_label_wraps_like_4_1s` (2 rows).
- **T4-R12: the M3a carry-forward "`Begin(false)` with the button already up" belongs to Task 3** (interfaces.md §2: `SliderGestureTracker`, "The M3a minor is fixed", with FillStyleGroup refactored onto it). Task 4 only checks in Step 1 that it landed and does not edit `FillStyleGroup`. No other carry-forward lands in the shell:
  - `ContentEquals` and the M2a items are Task 1's.
  - The tooltip, ribbon, canvas-height and drop-zone notes are T4-R8 to T4-R11.
- **Observation for the look sign-off, not changed here.** M3a's `PanelHeading` (Controls.xaml:302-308) has no uppercase and no letter-spacing, so the panel shows "Layers" and "Drop art here" where 4.1 shows LAYERS and DROP ART HERE (styles.css:258-259). WPF has no letter-spacing, and changing the shared heading style is outside this task's listed notes. It belongs in Task 5's sign-off page captions.

**Measured while drafting:**

- **Setup.**
  - A scratch copy of `feat/v5-m3b-select-draw` at `4e0be14` (`git archive HEAD` under `%TEMP%`, outside the repo; the repo stayed clean).
  - .NET SDK 10.0.302.
  - Stand-ins for Task 1's `LayerOps.Reorder` and `LayerOps.DropIndices`, written from interfaces.md §1. `Reorder` is RemoveAt then Insert. `DropIndices` gives `insert = over > from ? over - 1 : over` and `to = above ? insert + 1 : insert`.
  - No Task 2 or Task 3 code is needed by this task's code. **Re-measured with Task 2's ribbon** (reconciler, critic mn4): a scratch copy of `4e0be14` with Task 2's `Tool.cs`, `CanvasCursor.cs`, `ViewOverlay.cs`, `ToolsViewModel.cs`, `BrushViewModel.cs`, `HomeViewModel.cs`, `ToolsGroup.xaml(.cs)`, `BrushGroup.xaml(.cs)`, `HomeRibbon.xaml` and its Step 5 Composition and HomeTestKit edits, plus stand-ins for Task 1's brush constants and `LivePreview`. At 1184x761 (this session's display at 125%): ribbon **147.2**, canvas area **540**, Home extent **1745.6** in a 1184 viewport. The ribbon height and the area are the same as without Task 2, because Tools and Brush (95.2 tall) are no taller than Regions. `At_the_default_size_the_ribbon_scrolls_sideways_and_no_group_shrinks` and `At_the_default_size_the_canvas_area_is_no_shorter_than_4_1s_with_the_same_groups` passed there, so Step 8's "3 already pass" holds. Task 3's panel sits inside the left panel's ScrollViewer and changes no row height.
- **Build and tests.**
  - Every file below compiled under `-warnaserror` with 0 warnings.
  - Engine.Tests stayed 718 and Parity.Tests 35.
  - App.Tests went from 411 to 463 (+52). The filter `LayersReorderTests|LayersPanelDragTests|ShellLookTests|Task4ViewTests` gave 58 passed (32 + 15 + 5 + 6).
  - The full App run was repeated three times; no Task 4 test failed in any of them.
- **One pre-existing failure.** This session's display is scaled to 125%, and there `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails on the untouched baseline (`158,71` expected, `157.60000000000002,71.2` actual). It fails identically with Task 4 applied. It is M3a's, and it is not counted against this task.
- **Failure counts, measured by removing the implementation:**
  - Steps 2 and 5: the build fails with CS1061, CS0103 and CS0117 naming the missing members.
  - Step 8: 2 failed (`Expected: Wrap`, `Actual: NoWrap`) and 3 passed.
- **Two findings shaped the code:**
  - After `ObservableCollection.Move`, `ContainerFromItem(row)` is null until a layout pass. Hence `LayerList.UpdateLayout()` before refocusing.
  - Off-screen, `UIElement.IsVisible` is false for every container. Hence `RowAt` does not filter on it.
- **4.1 numbers:**
  - Measured once with the scratch script in the appendix: a hidden `BrowserWindow`, CPU raster, device scale 1, the vendored fonts through `tools/parity-ref/fonts.js`, and content sizes 1184x761 and 624x441.
  - Run: `env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/measure41.js` from the scratch copy.
  - The drop orders in Step 2 were printed by node running handleLayerDrop's array lines (editor.js:634-652) verbatim.

**Counts.** Task 3 ends at Engine.Tests 877 and App.Tests 557 (net of Task 2's R1 removals and with the reconciler's MJ1 facts). Task 4 changes no engine file and adds App.Tests +53: LayersReorderTests 32, LayersPanelDragTests 15, ShellLookTests 5 and ShellTests +1 (critic MJ2). Parity.Tests stays 35.

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| Task 3 (Step 1 baseline) | 877 | 557 | |
| Step 3 | 877 | 589 | LayersReorderTests +32 |
| Step 6 | 877 | 604 | LayersPanelDragTests +15; Task4ViewTests ±0 |
| Step 9 | 877 | 609 | ShellLookTests +5 |
| **Step 11** | **877** | **610** | ShellTests +1 (Ctrl+Z) |

- [ ] **Step 1: Baseline and the Task 3 carry-forward check**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected:
- Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`.
- Parity.Tests `Passed: 35, Failed: 0`.
- App.Tests `Passed: 557, Failed: 0`.

On a display scaled to 125%, `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails before any Task 4 change (measured while drafting). It is M3a's layout-rounding assertion: run at 100% or report it as pre-existing; do not change it here.

Run: `grep -n "SliderGestureTracker" src/RBX15.App/Views/FillStyleGroup.xaml.cs`
Expected: at least one line. Task 3 moved FillStyleGroup onto the tracker that fixes `Begin(false)` with the button already up (T4-R12).

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SliderGestureTrackerTests"`
Expected: `Failed: 0` and a nonzero `Passed` for `RBX15.App.Tests.dll`.

If either check finds nothing, stop and report: the carry-forward is Task 3's and has not landed.

- [ ] **Step 2: Write the failing reorder tests for the view-model**

Ledger: `layers-reorder-drag`, `layers-reorder-drag-2`, `undo-empty-steps`, `layers-list`. Create `src/RBX15.App.Tests/LayersReorderTests.cs`:

```csharp
using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Drag reorder through the layers view-model (handleLayerDragStart/Over/Drop and cleanupLayerDrag,
/// editor.js:584-666; ledger layers-reorder-drag, layers-reorder-drag-2, undo-empty-steps). Row thumbnails
/// are WPF bitmaps, so every test runs on the STA thread.
/// </summary>
public class LayersReorderTests
{
    static SKImage Img()
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    /// <summary>Layers 1..n named A, B, C... (1 at the bottom), the top one selected, in one undo step.</summary>
    static EditorSession Seeded(int count)
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            for (int i = 0; i < count; i++) s = LayerOps.AddImage(s, Img(), session.TakeId(), ((char)('A' + i)).ToString());
            return s;
        });
        return session;
    }

    static LayersViewModel Vm(EditorSession session) => new(session, new ImportService(session, new FakeStatus()), new FakeFileDialogs());

    /// <summary>Document.Layers ids, bottom first, as one string ("1234").</summary>
    static string Order(EditorSession session) => string.Concat(session.Document.Layers.Select(l => l.Id));

    static LayerRowViewModel Row(LayersViewModel vm, int id) => vm.Rows.Single(r => r.Id == id);

    static void AssertNoDragState(LayersViewModel vm)
    {
        Assert.All(vm.Rows, r => Assert.False(r.IsDragSource));
        Assert.All(vm.Rows, r => Assert.Equal(DropIndicator.None, r.DropIndicator));
    }

    // The expected orders are 4.1's own: node ran handleLayerDrop's array code (editor.js:634-652, the
    // findIndex/splice lines verbatim) on ids [1,2,3,4] for every (drag, target, half) and printed the ids.
    // above = layerDragPos 'top' (inserted after the target, higher z); false = 'bottom'.
    [Theory]
    [InlineData(1, 2, true, "2134")]
    [InlineData(1, 2, false, "1234")]
    [InlineData(1, 3, true, "2314")]
    [InlineData(1, 3, false, "2134")]
    [InlineData(1, 4, true, "2341")]
    [InlineData(1, 4, false, "2314")]
    [InlineData(2, 1, true, "1234")]
    [InlineData(2, 1, false, "2134")]
    [InlineData(2, 3, true, "1324")]
    [InlineData(2, 3, false, "1234")]
    [InlineData(2, 4, true, "1342")]
    [InlineData(2, 4, false, "1324")]
    [InlineData(3, 1, true, "1324")]
    [InlineData(3, 1, false, "3124")]
    [InlineData(3, 2, true, "1234")]
    [InlineData(3, 2, false, "1324")]
    [InlineData(3, 4, true, "1243")]
    [InlineData(3, 4, false, "1234")]
    [InlineData(4, 1, true, "1423")]
    [InlineData(4, 1, false, "4123")]
    [InlineData(4, 2, true, "1243")]
    [InlineData(4, 2, false, "1423")]
    [InlineData(4, 3, true, "1234")]
    [InlineData(4, 3, false, "1243")]
    public void A_drop_reorders_as_4_1s_splice_in_one_step_or_none(int drag, int target, bool above, string expected) => StaThread.Run(() =>
    {
        var session = Seeded(4);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.BeginRowDrag(drag);
        vm.DragOverRow(target, above);
        bool changed = vm.CompleteRowDrop();
        Assert.Equal(expected, Order(session));
        Assert.Equal(expected != "1234", changed);
        Assert.Equal(steps + (changed ? 1 : 0), session.UndoCount);   // 4.1 pushed a step even for "1234" (undo-empty-steps fix)
        Assert.Equal(4, session.SelectedLayerId);                      // selection unchanged by reorder
        Assert.Equal(expected.Reverse().Select(c => c - '0'), vm.Rows.Select(r => r.Id));   // rows top first
        AssertNoDragState(vm);
    });

    [Fact] // layers-reorder-drag-2 parityCheck: the bottom layer onto the top half of the top row is the new top; one Ctrl+Z restores
    public void Bottom_onto_the_top_rows_upper_half_becomes_the_top_and_one_undo_restores() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: true);
        Assert.True(vm.CompleteRowDrop());
        Assert.Equal(new[] { "A", "C", "B" }, vm.Rows.Select(r => r.Name));
        Assert.True(session.Undo());
        Assert.Equal(new[] { "C", "B", "A" }, vm.Rows.Select(r => r.Name));
    });

    [Fact] // .dragging-layer on the source; one live .drag-over-top/-bottom at a time (editor.js:595-617)
    public void The_dragged_row_is_marked_and_only_the_live_target_shows_its_edge() => StaThread.Run(() =>
    {
        var vm = Vm(Seeded(3));
        vm.BeginRowDrag(1);
        Assert.True(Row(vm, 1).IsDragSource);
        vm.DragOverRow(3, above: true);
        Assert.Equal(DropIndicator.Above, Row(vm, 3).DropIndicator);
        vm.DragOverRow(3, above: false);
        Assert.Equal(DropIndicator.Below, Row(vm, 3).DropIndicator);
        vm.DragOverRow(2, above: true);
        Assert.Equal(DropIndicator.None, Row(vm, 3).DropIndicator);
        Assert.Equal(DropIndicator.Above, Row(vm, 2).DropIndicator);
        vm.EndRowDrag();
        AssertNoDragState(vm);
    });

    [Fact] // layers-reorder-drag fix: 4.1 kept layerDragOverId while over the dragged row (editor.js:605), so a drop there moved it
    public void Hovering_the_dragged_row_after_another_clears_the_stale_target() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: true);
        vm.DragOverRow(1, above: true);
        Assert.Equal(DropIndicator.None, Row(vm, 3).DropIndicator);
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal("123", Order(session));
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // layers-reorder-drag fix: off every row there is no target, so the release drops nothing
    public void A_drop_after_the_pointer_left_every_row_does_nothing() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.BeginRowDrag(1);
        vm.DragOverRow(2, above: true);
        vm.ClearDropTarget();
        Assert.Equal(DropIndicator.None, Row(vm, 2).DropIndicator);
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // handleLayerDrop's guard (editor.js:625-628): no drag or a missing id does nothing
    public void Without_a_drag_or_with_a_missing_id_nothing_happens() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.DragOverRow(2, above: true);
        Assert.Equal(DropIndicator.None, Row(vm, 2).DropIndicator);
        Assert.False(vm.CompleteRowDrop());
        vm.BeginRowDrag(99);
        vm.DragOverRow(2, above: true);
        Assert.False(vm.CompleteRowDrop());
        vm.BeginRowDrag(1);
        vm.DragOverRow(99, above: true);
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // an undo mid-drag that removes the dragged layer ends the drag; the release drops nothing and throws nothing
    public void An_undo_that_removes_the_dragged_layer_ends_the_drag() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(), session.TakeId(), "New"));   // id 3, its own step
        var vm = Vm(session);
        vm.BeginRowDrag(3);
        vm.DragOverRow(1, above: false);
        Assert.True(session.Undo());
        int steps = session.UndoCount;
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal("12", Order(session));
        Assert.Equal(steps, session.UndoCount);
        AssertNoDragState(vm);
    });

    [Fact] // the same for the target: an undo that removes it leaves no target
    public void An_undo_that_removes_the_target_leaves_no_target() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(), session.TakeId(), "New"));
        var vm = Vm(session);
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: true);
        Assert.True(session.Undo());
        Assert.False(vm.CompleteRowDrop());
        Assert.Equal("12", Order(session));
        AssertNoDragState(vm);
    });

    [Fact] // editor.html:317, verbatim
    public void The_reorder_hint_is_4_1s_text()
    {
        var html = File.ReadAllText(Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "editor.html"));
        Assert.Contains("<h3>Layers <span style=\"font-size:10px;color:#555;text-transform:none\">(drag to reorder)</span></h3>", html);
        Assert.Equal("(drag to reorder)", StaThread.Run(() => Vm(new EditorSession()).ReorderHint));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in `RBX15.App.Tests` with `error CS1061: 'LayersViewModel' does not contain a definition for 'BeginRowDrag'` (likewise `DragOverRow`, `CompleteRowDrop`, `ClearDropTarget`, `EndRowDrag` and `ReorderHint`), `error CS1061: 'LayerRowViewModel' does not contain a definition for 'IsDragSource'` (likewise `DropIndicator`), and `error CS0103: The name 'DropIndicator' does not exist in the current context`.

- [ ] **Step 3: Implement the drag state in the row and list view-models**

Ledger: `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer`, `undo-empty-steps`.

In `src/RBX15.App/ViewModels/LayerRowViewModel.cs`, make three edits. Anchors are M3a text; if one is missing, stop and report.

Edit 1. Replace:

```csharp
namespace RBX15.App.ViewModels;

/// <summary>
/// One layer row
```

with:

```csharp
namespace RBX15.App.ViewModels;

/// <summary>The row's drop indicator while another row is dragged over it (.drag-over-top / .drag-over-bottom, styles.css:310-311).</summary>
public enum DropIndicator { None, Above, Below }

/// <summary>
/// One layer row
```

Edit 2. Replace:

```csharp
    bool _isSelected;
    ImageSource? _thumbnail;
```

with:

```csharp
    bool _isSelected;
    bool _isDragSource;
    DropIndicator _dropIndicator;
    ImageSource? _thumbnail;
```

Edit 3. Replace:

```csharp
    public IRelayCommand ToggleVisibilityCommand { get; }
```

with:

```csharp
    /// <summary>The row being dragged: .dragging-layer, drawn at opacity .4 (editor.js:595-599, styles.css:312). Set by LayersViewModel.</summary>
    public bool IsDragSource
    {
        get => _isDragSource;
        internal set => SetProperty(ref _isDragSource, value);
    }

    /// <summary>The live drop target's 2 px cyan edge (editor.js:611-618, styles.css:310-311). Set by LayersViewModel.</summary>
    public DropIndicator DropIndicator
    {
        get => _dropIndicator;
        internal set => SetProperty(ref _dropIndicator, value);
    }

    public IRelayCommand ToggleVisibilityCommand { get; }
```

In `src/RBX15.App/ViewModels/LayersViewModel.cs`, make five edits. Anchors are M3a text that Task 2's `LayerPicked` edit does not touch; if one is missing, stop and report.

Edit 1, the class comment. Replace:

```csharp
/// <summary>
/// The basic layers panel and its drop zone (editor.html:311-318; ledger layers-list,
/// layers-list-render, layers-select, layers-move-updown, panel-drop-zone, drop-panel). Rows are
/// top first, the highest z-order on top (editor.js:672-675). No drag reorder in M3a.
/// </summary>
```

with:

```csharp
/// <summary>
/// The layers panel and its drop zone (editor.html:311-318; ledger layers-list, layers-list-render,
/// layers-select, layers-move-updown, panel-drop-zone, drop-panel). Rows are top first, the highest
/// z-order on top (editor.js:672-675). Drag reorder (editor.js:584-666; ledger layers-reorder-drag,
/// layers-reorder-drag-2) runs through BeginRowDrag, DragOverRow, ClearDropTarget, CompleteRowDrop and
/// EndRowDrag; LayersPanel's code-behind drives them from the mouse and the keyboard.
/// </summary>
```

Edit 2, the fields. Replace:

```csharp
    LayerRowViewModel? _selected;
    bool _syncing;
```

with:

```csharp
    LayerRowViewModel? _selected;
    bool _syncing;
    int? _dragId;      // layerDragId (editor.js:587)
    int? _targetId;    // layerDragOverId (editor.js:588), live only: cleared whenever the pointer is off every other row
    bool _above;       // layerDragPos === 'top' (editor.js:589)
```

Edit 3, the drag members, after `BrowseCommand`. Replace:

```csharp
    /// <summary>The drop zone's click-to-browse (dropZone.onclick, editor.js:954).</summary>
    public IRelayCommand BrowseCommand { get; }
```

with:

```csharp
    /// <summary>The drop zone's click-to-browse (dropZone.onclick, editor.js:954).</summary>
    public IRelayCommand BrowseCommand { get; }

    /// <summary>The heading's hint, verbatim (editor.html:317). Shown now that drag reorder works (M3a hid it).</summary>
    public string ReorderHint => "(drag to reorder)";

    /// <summary>
    /// handleLayerDragStart (editor.js:591-600): marks the dragged row (.dragging-layer, opacity .4). Ends any drag
    /// still running. A missing id starts nothing. Never an undo step and never a selection change.
    /// </summary>
    public void BeginRowDrag(int id)
    {
        EndRowDrag();
        if (Row(id) is not { } row) return;
        _dragId = id;
        row.IsDragSource = true;
    }

    /// <summary>
    /// handleLayerDragOver (editor.js:602-619): the row under the pointer becomes the drop target, its top half
    /// (<paramref name="above"/>) or its bottom half, with one indicator on it and none on any other row. The dragged
    /// row itself is never a target: hovering it clears the target, which fixes 4.1's stale target (editor.js:605
    /// returned early and kept layerDragOverId; ledger layers-reorder-drag). Ignored with no drag running.
    /// </summary>
    public void DragOverRow(int targetId, bool above)
    {
        if (_dragId is null) return;
        if (targetId == _dragId || Row(targetId) is not { } row)
        {
            ClearDropTarget();
            return;
        }
        if (_targetId is int old && old != targetId && Row(old) is { } previous) previous.DropIndicator = DropIndicator.None;
        _targetId = targetId;
        _above = above;
        row.DropIndicator = above ? DropIndicator.Above : DropIndicator.Below;
    }

    /// <summary>The pointer is off every row: no target and no indicator, so a release there drops nothing (the stale-target fix).</summary>
    public void ClearDropTarget()
    {
        if (_targetId is int id && Row(id) is { } row) row.DropIndicator = DropIndicator.None;
        _targetId = null;
    }

    /// <summary>
    /// handleLayerDrop (editor.js:621-657) on the live target only: LayerOps.DropIndices then LayerOps.Reorder in one
    /// session Apply, so a reorder is one undo step and a drop that leaves the order as it was records none (4.1 pushed
    /// an empty step, editor.js:630; ledger undo-empty-steps). The selection is unchanged (layers-reorder-drag). Always
    /// ends the drag, as 4.1 cleaned up on every path. True iff the order changed.
    /// </summary>
    public bool CompleteRowDrop()
    {
        bool changed = false;
        if (_dragId is int drag && _targetId is int target)
        {
            bool above = _above;
            changed = _session.Apply(d => LayerOps.DropIndices(d, drag, target, above) is (int from, int to) ? LayerOps.Reorder(d, from, to) : d);
        }
        EndRowDrag();
        return changed;
    }

    /// <summary>cleanupLayerDrag (editor.js:659-666): no drag, no target, no indicator. Idempotent.</summary>
    public void EndRowDrag()
    {
        ClearDropTarget();
        if (_dragId is int id && Row(id) is { } row) row.IsDragSource = false;
        _dragId = null;
    }
```

Edit 4, the row lookup. Replace:

```csharp
    int? SelectedIndex()
```

with:

```csharp
    LayerRowViewModel? Row(int id) => Rows.FirstOrDefault(r => r.Id == id);

    int? SelectedIndex()
```

Edit 5, a drag whose layer left the document. In `Sync`, replace:

```csharp
            // Raised on every sync: a Move or Remove can make the ListBox drop its selection, and the
```

with:

```csharp
            // A drag whose row or target left the document (an undo, a delete) ends or loses its target here,
            // so a later release drops nothing.
            if (_dragId is int dragging && !ids.Contains(dragging)) EndRowDrag();
            if (_targetId is int dropTarget && !ids.Contains(dropTarget)) _targetId = null;
            // Raised on every sync: a Move or Remove can make the ListBox drop its selection, and the
```

The names `dragging` and `dropTarget` avoid `Sync`'s loop variable `target`.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.LayersReorderTests"`
Expected: `Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877`; Parity.Tests `Passed: 35`; App.Tests `Passed: 589`.

- [ ] **Step 4: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/ViewModels/LayerRowViewModel.cs src/RBX15.App/ViewModels/LayersViewModel.cs src/RBX15.App.Tests/LayersReorderTests.cs
git commit -m "feat(app): drag reorder state in the layers view-model, one undo step per drop"
```

- [ ] **Step 5: Write the failing panel tests, and update Task4ViewTests**

Ledger: `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer`, `layers-list`. Create `src/RBX15.App.Tests/LayersPanelDragTests.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The layers panel's drag and keys (LayersPanel.xaml.cs; editor.js:584-666; ledger layers-reorder-drag,
/// layers-reorder-drag-2, layer-reorder-dragging-class-timer). WPF ignores posted mouse input and the rules forbid
/// synthetic desktop input, so the tests drive the Handle* seams the mouse and key overrides forward to, with
/// points in panel coordinates taken from the laid-out rows. The panel is laid out off-screen, 280 wide (the panel
/// width, styles.css:247).
/// </summary>
public class LayersPanelDragTests
{
    static SKImage Img()
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    /// <summary>A, B, C (ids 1, 2, 3; C on top and selected), in a laid-out panel. Rows top first: C, B, A.</summary>
    static (EditorSession Session, LayersViewModel Vm, LayersPanel Panel) Panel(int count = 3)
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            for (int i = 0; i < count; i++) s = LayerOps.AddImage(s, Img(), session.TakeId(), ((char)('A' + i)).ToString());
            return s;
        });
        var vm = new LayersViewModel(session, new ImportService(session, new FakeStatus()), new FakeFileDialogs());
        var panel = new LayersPanel { DataContext = vm };
        Layout(panel);
        return (session, vm, panel);
    }

    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(280, 2000));
        e.Arrange(new Rect(0, 0, 280, 2000));
        e.UpdateLayout();
    }

    static ListBoxItem Item(LayersPanel panel, LayerRowViewModel row) =>
        (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(row);

    /// <summary>A panel point inside <paramref name="row"/>'s box, <paramref name="fraction"/> of the way down.</summary>
    static Point At(LayersPanel panel, LayerRowViewModel row, double fraction)
    {
        var item = Item(panel, row);
        var top = item.TranslatePoint(new Point(0, 0), panel);
        return new Point(top.X + 40, top.Y + item.ActualHeight * fraction);
    }

    static string Names(LayersViewModel vm) => string.Concat(vm.Rows.Select(r => r.Name));

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    [Fact] // layers-reorder-drag-2 parityCheck through the mouse path: A onto C's top half is the new top, one step
    public void A_drag_past_the_drag_distance_reorders_on_release_in_one_step() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        var a = vm.Rows[2];
        int steps = session.UndoCount;
        var press = At(panel, a, 0.5);
        panel.HandleRowPress(a, press);
        panel.HandleMove(press + new Vector(0, 2), leftButtonDown: true);
        Assert.False(panel.IsDragging);
        var overC = At(panel, vm.Rows[0], 0.25);
        panel.HandleMove(overC, leftButtonDown: true);
        Assert.True(panel.IsDragging);
        Assert.True(a.IsDragSource);
        Assert.Equal(DropIndicator.Above, vm.Rows[0].DropIndicator);
        panel.HandleRelease(overC);
        Assert.False(panel.IsDragging);
        Assert.Equal("ACB", Names(vm));
        Assert.Equal(steps + 1, session.UndoCount);
        Assert.All(vm.Rows, r => Assert.Equal((false, DropIndicator.None), (r.IsDragSource, r.DropIndicator)));
    });

    [Fact] // the system drag distance (SystemParameters.MinimumVerticalDragDistance) separates a click from a drag
    public void A_move_within_the_drag_distance_stays_a_click() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        var press = At(panel, vm.Rows[2], 0.5);
        panel.HandleRowPress(vm.Rows[2], press);
        panel.HandleMove(press + new Vector(SystemParameters.MinimumHorizontalDragDistance / 2, -SystemParameters.MinimumVerticalDragDistance / 2), leftButtonDown: true);
        Assert.False(panel.IsDragging);
        panel.HandleRelease(press);
        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // the stale-target fix through the mouse path: below every row there is no target, and the release drops nothing
    public void A_release_off_every_row_drops_nothing() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(At(panel, vm.Rows[0], 0.25), leftButtonDown: true);
        Assert.Equal(DropIndicator.Above, vm.Rows[0].DropIndicator);
        var below = At(panel, vm.Rows[2], 1.0) + new Vector(0, 40);
        panel.HandleMove(below, leftButtonDown: true);
        Assert.Equal(DropIndicator.None, vm.Rows[0].DropIndicator);
        panel.HandleRelease(below);
        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // the drop reads the row under the release point, never the last move's (no stale target after a wheel scroll)
    public void The_release_point_picks_the_target() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(At(panel, vm.Rows[0], 0.25), leftButtonDown: true);   // C, top half
        panel.HandleRelease(At(panel, vm.Rows[0], 0.75));                       // C, bottom half: A goes just below C
        Assert.Equal("CAB", Names(vm));
    });

    [Fact] // dragend (editor.js:741): Esc or a lost capture cancels with nothing changed
    public void Esc_and_a_capture_loss_cancel_the_drag() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        var overC = At(panel, vm.Rows[0], 0.25);

        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        Assert.True(panel.HandleKey(Key.Escape, ModifierKeys.None, null));
        Assert.False(panel.IsDragging);
        panel.HandleRelease(overC);

        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        panel.HandleCaptureLost();
        Assert.False(panel.IsDragging);
        panel.HandleRelease(overC);

        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
        Assert.All(vm.Rows, r => Assert.Equal((false, DropIndicator.None), (r.IsDragSource, r.DropIndicator)));
        Assert.False(panel.HandleKey(Key.Escape, ModifierKeys.None, null));   // Esc with no drag is not the panel's
    });

    [Fact] // a release the panel never saw: the next move with the button up, or the next press, cancels
    public void A_missed_release_cancels_on_the_next_move_or_press() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        var overC = At(panel, vm.Rows[0], 0.25);
        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        panel.HandleMove(overC, leftButtonDown: false);
        Assert.False(panel.IsDragging);

        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        panel.HandleRowPress(vm.Rows[1], At(panel, vm.Rows[1], 0.5));
        Assert.False(panel.IsDragging);
        Assert.All(vm.Rows, r => Assert.False(r.IsDragSource));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // a press on Dupe, Hide or Del is a button press, never a drag; the row body arms one
    public void Only_a_rows_body_arms_a_drag_and_its_buttons_do_not() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        var item = Item(panel, vm.Rows[1]);
        var del = Descendants<Button>(item).Last();
        var name = Descendants<TextBlock>(item).First(t => t.Name == "NameText");
        Assert.Null(LayersPanel.RowFrom((TextBlock)del.Content, stopAtButtons: true));
        Assert.Same(vm.Rows[1], LayersPanel.RowFrom(name, stopAtButtons: true));
        Assert.Same(vm.Rows[1], LayersPanel.RowFrom((TextBlock)del.Content, stopAtButtons: false));   // keys: a focused button still names its row
        Assert.Null(LayersPanel.RowFrom(panel, stopAtButtons: true));
    });

    [Fact] // each row owns half the 4px gap on either side; above = the top half by its own midpoint (editor.js:607-609)
    public void Row_at_splits_each_gap_and_uses_the_rows_midpoint() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        foreach (var row in vm.Rows)
        {
            var item = Item(panel, row);
            double top = item.TranslatePoint(new Point(0, 0), panel).Y, h = item.ActualHeight;
            Assert.Equal((row.Id, true), panel.RowAt(new Point(40, top - LayersPanel.HalfGap)));
            Assert.Equal((row.Id, true), panel.RowAt(new Point(40, top + h / 2 - 0.01)));
            Assert.Equal((row.Id, false), panel.RowAt(new Point(40, top + h / 2)));
            Assert.Equal((row.Id, false), panel.RowAt(new Point(40, top + h + LayersPanel.HalfGap - 0.01)));
        }
        var first = Item(panel, vm.Rows[0]).TranslatePoint(new Point(0, 0), panel).Y;
        Assert.Null(panel.RowAt(new Point(40, first - LayersPanel.HalfGap - 0.5)));
        Assert.Null(panel.RowAt(new Point(-1, first + 10)));
        Assert.Null(panel.RowAt(new Point(panel.LayerList.ActualWidth + 1, first + 10)));
    });

    [Fact] // layers-reorder-drag-2 (keyboard operable): Alt+Up / Alt+Down move the focused row, one step each, none at an end
    public void Alt_up_and_down_move_the_focused_row_one_step_each_and_focus_follows_it() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        var a = vm.Rows[2];
        int steps = session.UndoCount;
        Assert.True(panel.HandleKey(Key.Up, ModifierKeys.Alt, a));
        Assert.Equal("CAB", Names(vm));
        Assert.Equal(1, session.SelectedLayerId);   // the focused row is selected first
        var item = Item(panel, a);
        Assert.NotNull(item);                        // laid out again, so the moved row has a container to focus
        Assert.Same(item, FocusManager.GetFocusedElement(FocusManager.GetFocusScope(item)));
        Assert.True(panel.HandleKey(Key.Up, ModifierKeys.Alt, a));
        Assert.Equal("ACB", Names(vm));
        Assert.True(panel.HandleKey(Key.Up, ModifierKeys.Alt, a));   // at the top: used, nothing moves, no step
        Assert.Equal("ACB", Names(vm));
        Assert.True(panel.HandleKey(Key.Down, ModifierKeys.Alt, a));
        Assert.Equal("CAB", Names(vm));
        Assert.Equal(steps + 3, session.UndoCount);
    });

    [Theory] // only Alt+Up / Alt+Down on a row; arrows, Shift and Ctrl stay the list's (selection) and M4's
    [InlineData(Key.Up, ModifierKeys.None)]
    [InlineData(Key.Down, ModifierKeys.None)]
    [InlineData(Key.Up, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData(Key.Down, ModifierKeys.Control)]
    [InlineData(Key.Left, ModifierKeys.Alt)]
    public void Other_keys_are_left_to_the_list(Key key, ModifierKeys modifiers) => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        Assert.False(panel.HandleKey(key, modifiers, vm.Rows[1]));
        Assert.False(panel.HandleKey(Key.Up, ModifierKeys.Alt, null));
        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // styles.css:310-312: the source row at opacity .4, a 2px #17d4fa edge on the target's top or bottom
    public void The_drag_dims_the_source_row_and_draws_the_targets_edge() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: false);
        Layout(panel);
        Assert.Equal(0.4, Item(panel, vm.Rows[2]).Opacity);
        Assert.Equal(1.0, Item(panel, vm.Rows[0]).Opacity);
        var target = Item(panel, vm.Rows[0]);
        var above = (Rectangle)target.Template.FindName("DropAbove", target);
        var below = (Rectangle)target.Template.FindName("DropBelow", target);
        Assert.Equal((Visibility.Collapsed, Visibility.Visible), (above.Visibility, below.Visibility));
        Assert.Equal((2.0, "#FF17D4FA"), (below.Height, ((SolidColorBrush)below.Fill).Color.ToString()));
        vm.EndRowDrag();
        Layout(panel);
        Assert.Equal(1.0, Item(panel, vm.Rows[2]).Opacity);
        Assert.Equal(Visibility.Collapsed, below.Visibility);
    });
}
```

In `src/RBX15.App.Tests/Task4ViewTests.cs` make two edits.

Edit 1 (R13: Up and Down moved to the Selected layer panel). Replace:

```csharp
        var all = Descendants<Button>(panel).Select(b => b.Content as string).ToList();
        Assert.Contains("Up", all);
        Assert.Contains("Down", all);
    });
```

with:

```csharp
        // Up and Down moved into the Selected layer panel (Task 3; editor.js:773-774), so the list has none (R13).
        var all = Descendants<Button>(panel).Select(b => b.Content as string).ToList();
        Assert.DoesNotContain("Up", all);
        Assert.DoesNotContain("Down", all);
    });
```

Edit 2 (the hint is back). Replace the whole fact:

```csharp
    [Fact] // controller ruling 3: drag reorder is M3b, so the heading carries no "(drag to reorder)" hint
    public void The_layers_panel_header_shows_no_drag_to_reorder_hint() => StaThread.Run(() =>
    {
        var panel = new LayersPanel { DataContext = Layers(new EditorSession()) };
        Layout(panel);
        var texts = Descendants<TextBlock>(panel).Select(t => t.Text).ToList();
        Assert.Contains("Layers", texts);
        Assert.DoesNotContain(texts, t => t.Contains("drag", StringComparison.OrdinalIgnoreCase) || t.Contains("reorder", StringComparison.OrdinalIgnoreCase));
    });
```

with:

```csharp
    [Fact] // editor.html:317: drag reorder works in M3b, so the heading carries 4.1's hint (10px, #555, the heading's mono 600)
    public void The_layers_heading_shows_4_1s_drag_to_reorder_hint() => StaThread.Run(() =>
    {
        var panel = new LayersPanel { DataContext = Layers(new EditorSession()) };
        Layout(panel);
        var heading = (TextBlock)panel.FindName("LayersHeading");
        Assert.Equal("Layers (drag to reorder)", string.Concat(heading.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text)));
        var hint = (System.Windows.Documents.Run)panel.FindName("ReorderHintRun");
        Assert.Equal((10.0, "#FF555555"), (hint.FontSize, ((SolidColorBrush)hint.Foreground).Color.ToString()));
        Assert.Equal((FontWeights.SemiBold, heading.FontFamily), (hint.FontWeight, hint.FontFamily));
    });
```

`TextBlock.Text` is empty when the text is in `Inlines` (measured), so the test joins the runs.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in `RBX15.App.Tests` with:
- `error CS1061: 'LayersPanel' does not contain a definition for 'HandleRowPress'`, and likewise for `HandleMove`, `HandleRelease`, `HandleCaptureLost`, `HandleKey`, `RowAt` and `IsDragging`;
- `error CS0117: 'LayersPanel' does not contain a definition for 'RowFrom'`, and likewise `HalfGap`.

- [ ] **Step 6: Implement the panel: the hint, the drop edges and the drag**

Ledger: `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer`, `layers-list`, `layers-list-render`.

Replace `src/RBX15.App/Views/LayersPanel.xaml` with the following (in full). What changes from M3a:
- the heading gains 4.1's hint;
- the item template gains the two drop edges and the dimmed source;
- the M3a Up/Down pair under the list is removed (R13).

```xml
<UserControl x:Class="RBX15.App.Views.LayersPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:RBX15.App.ViewModels">
    <!-- The layers panel (editor.html:317-318; editor.js:671-744; styles.css:287-319; ledger layers-list,
         layers-list-render, layers-reorder-drag, layers-reorder-drag-2). DataContext: LayersViewModel. Top row =
         highest z. Arrow keys move the selection; Tab walks each row's buttons. Drag a row by its body to reorder
         it; Alt+Up and Alt+Down move the focused row; Esc cancels a drag. The code-behind drives the drag. -->
    <UserControl.Resources>
        <!-- Row action buttons: 4.1's inline styles, editor.js:703, :709, :715 (flex:1; padding 3px 0;
             font-size 10px; radius 3px; weight 600). -->
        <Style x:Key="RowActionButton" TargetType="Button">
            <Setter Property="Padding" Value="0,3" />
            <Setter Property="FontSize" Value="10" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="FontFamily" Value="{DynamicResource FontBody}" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="3"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <!-- Dupe: rgba(23,212,250,.1) on #17d4fa (editor.js:703) -->
        <Style x:Key="DupeButton" TargetType="Button" BasedOn="{StaticResource RowActionButton}">
            <Setter Property="Background" Value="#1A17D4FA" />
            <Setter Property="BorderBrush" Value="#17D4FA" />
            <Setter Property="Foreground" Value="#17D4FA" />
        </Style>
        <!-- Hide/Show: rgba(255,255,255,.04), border rgba(255,255,255,.14), text #c4cdda or #5e6b7f (editor.js:709) -->
        <Style x:Key="VisibilityButton" TargetType="Button" BasedOn="{StaticResource RowActionButton}">
            <Setter Property="Background" Value="#0AFFFFFF" />
            <Setter Property="BorderBrush" Value="#24FFFFFF" />
            <Setter Property="Foreground" Value="#C4CDDA" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding Visible}" Value="False">
                    <Setter Property="Foreground" Value="#5E6B7F" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
        <!-- Del: rgba(255,84,114,.1) on #ff5472 (editor.js:715) -->
        <Style x:Key="DeleteButton" TargetType="Button" BasedOn="{StaticResource RowActionButton}">
            <Setter Property="Background" Value="#1AFF5472" />
            <Setter Property="BorderBrush" Value="#FF5472" />
            <Setter Property="Foreground" Value="#FF5472" />
        </Style>
    </UserControl.Resources>

    <StackPanel>
        <!-- <h3>Layers <span style="font-size:10px;color:#555;text-transform:none">(drag to reorder)</span></h3>
             (editor.html:317): the hint inherits the heading's mono face and weight 600 at 10px, #555. -->
        <TextBlock x:Name="LayersHeading" Style="{DynamicResource PanelHeading}"><Run Text="Layers " /><Run x:Name="ReorderHintRun" Text="{Binding ReorderHint, Mode=OneWay}" FontSize="10" Foreground="#555555" /></TextBlock>
        <!-- .layer-list: column, gap 4px, min-height 20px (styles.css:287). The template has no
             ScrollViewer, so the left panel scrolls the list and the wheel is never swallowed. -->
        <ListBox x:Name="LayerList"
                 ItemsSource="{Binding Rows}"
                 SelectedItem="{Binding Selected, Mode=TwoWay}"
                 SelectionMode="Single"
                 MinHeight="20"
                 KeyboardNavigation.TabNavigation="Continue"
                 AutomationProperties.Name="Layers, top layer first">
            <ListBox.Template>
                <ControlTemplate TargetType="ListBox">
                    <ItemsPresenter />
                </ControlTemplate>
            </ListBox.Template>
            <ListBox.ItemContainerStyle>
                <Style TargetType="ListBoxItem">
                    <Setter Property="AutomationProperties.Name" Value="{Binding AutomationName}" />
                    <Setter Property="Margin" Value="0,0,0,4" />
                    <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
                    <Setter Property="Foreground" Value="{DynamicResource Fg2Brush}" />
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="ListBoxItem">
                                <Grid>
                                    <!-- .layer-item: padding 6, min-height 56, radius r-sm 6, 1px transparent
                                         border, navy-soft to navy gradient (styles.css:289-302) -->
                                    <Border x:Name="Row"
                                            Padding="6"
                                            MinHeight="56"
                                            CornerRadius="6"
                                            BorderThickness="1"
                                            BorderBrush="Transparent"
                                            Cursor="Hand">
                                        <Border.Background>
                                            <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                                <GradientStop Color="#223A54" Offset="0" />
                                                <GradientStop Color="#192E44" Offset="1" />
                                            </LinearGradientBrush>
                                        </Border.Background>
                                        <ContentPresenter />
                                    </Border>
                                    <!-- .drag-over-top / .drag-over-bottom: a 2px brand-cyan top or bottom border
                                         (styles.css:310-311), drawn as a straight bar over the row's edge rather than
                                         CSS's rounded border edge (new v5 surface; provisional; M3b look sign-off). -->
                                    <Rectangle x:Name="DropAbove" Height="2" VerticalAlignment="Top" Fill="#17D4FA"
                                               Visibility="Collapsed" IsHitTestVisible="False" />
                                    <Rectangle x:Name="DropBelow" Height="2" VerticalAlignment="Bottom" Fill="#17D4FA"
                                               Visibility="Collapsed" IsHitTestVisible="False" />
                                </Grid>
                                <ControlTemplate.Triggers>
                                    <DataTrigger Binding="{Binding DropIndicator}" Value="{x:Static vm:DropIndicator.Above}">
                                        <Setter TargetName="DropAbove" Property="Visibility" Value="Visible" />
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding DropIndicator}" Value="{x:Static vm:DropIndicator.Below}">
                                        <Setter TargetName="DropBelow" Property="Visibility" Value="Visible" />
                                    </DataTrigger>
                                    <!-- .dragging-layer: opacity .4 (styles.css:312) -->
                                    <DataTrigger Binding="{Binding IsDragSource}" Value="True">
                                        <Setter Property="Opacity" Value="0.4" />
                                    </DataTrigger>
                                    <!-- :hover: border-accent rgba(23,212,250,.45) (styles.css:304) -->
                                    <Trigger Property="IsMouseOver" Value="True">
                                        <Setter TargetName="Row" Property="BorderBrush" Value="#7317D4FA" />
                                    </Trigger>
                                    <!-- .selected: cyan border, rgba(23,212,250,.15) to rgba(242,47,137,.08),
                                         glow-duo (styles.css:305-309; the duo glow is drawn as its cyan half) -->
                                    <Trigger Property="IsSelected" Value="True">
                                        <Setter TargetName="Row" Property="BorderBrush" Value="#17D4FA" />
                                        <Setter TargetName="Row" Property="Background">
                                            <Setter.Value>
                                                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                                    <GradientStop Color="#2617D4FA" Offset="0" />
                                                    <GradientStop Color="#14F22F89" Offset="1" />
                                                </LinearGradientBrush>
                                            </Setter.Value>
                                        </Setter>
                                        <Setter TargetName="Row" Property="Effect">
                                            <Setter.Value>
                                                <DropShadowEffect Color="#17D4FA" BlurRadius="24" ShadowDepth="0" Opacity="0.25" />
                                            </Setter.Value>
                                        </Setter>
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </ListBox.ItemContainerStyle>
            <ListBox.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:LayerRowViewModel}">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="48" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <!-- 48x48, object-fit contain, #000, radius 4 (editor.js:685) -->
                        <Border Width="48" Height="48" CornerRadius="4" Background="#000000" VerticalAlignment="Center">
                            <Image Source="{Binding Thumbnail}" Stretch="Uniform" RenderOptions.BitmapScalingMode="HighQuality" />
                        </Border>
                        <!-- column, gap 4, padding-left 8, centered (editor.js:688) -->
                        <StackPanel Grid.Column="1" Margin="8,0,0,0" VerticalAlignment="Center">
                            <!-- 12px, weight 600, ellipsis; 40% opacity when hidden (editor.js:693-694) -->
                            <TextBlock x:Name="NameText"
                                       Text="{Binding Name}"
                                       FontSize="12"
                                       FontWeight="SemiBold"
                                       TextTrimming="CharacterEllipsis"
                                       ToolTip="{Binding Name}" />
                            <!-- three equal buttons, gap 3 (editor.js:697-715). Their content is a TextBlock, not a string:
                                 with string content, the button's automation peer strips the first "_" from its
                                 name as an access-key marker, and layer names like drip_blood_front have them. -->
                            <Grid Margin="0,4,0,0">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="3" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="3" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <Button Grid.Column="0"
                                        ToolTip="Duplicate layer"
                                        Command="{Binding DuplicateCommand}"
                                        Style="{StaticResource DupeButton}"
                                        AutomationProperties.Name="{Binding Name, StringFormat=Dupe {0}}">
                                    <TextBlock Text="Dupe" />
                                </Button>
                                <Button Grid.Column="2"
                                        ToolTip="{Binding VisibilityToolTip}"
                                        Command="{Binding ToggleVisibilityCommand}"
                                        Style="{StaticResource VisibilityButton}"
                                        AutomationProperties.Name="{Binding VisibilityAutomationName}">
                                    <TextBlock Text="{Binding VisibilityLabel}" />
                                </Button>
                                <Button Grid.Column="4"
                                        ToolTip="Delete layer"
                                        Command="{Binding DeleteCommand}"
                                        Style="{StaticResource DeleteButton}"
                                        AutomationProperties.Name="{Binding Name, StringFormat=Delete {0}}">
                                    <TextBlock Text="Del" />
                                </Button>
                            </Grid>
                        </StackPanel>
                    </Grid>
                    <DataTemplate.Triggers>
                        <DataTrigger Binding="{Binding Visible}" Value="False">
                            <Setter TargetName="NameText" Property="Opacity" Value="0.4" />
                        </DataTrigger>
                    </DataTemplate.Triggers>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </StackPanel>
</UserControl>
```

Replace `src/RBX15.App/Views/LayersPanel.xaml.cs` with:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The layers panel's input: drag a row to reorder (editor.js:584-666) and Alt+Up / Alt+Down on the focused row.
/// The drag is in-process and capture-based, not OLE DoDragDrop: a press on a row's body arms it, a move past the
/// system drag distance starts it and captures the mouse on this panel, every move re-reads the row under the
/// pointer by its midpoint (handleLayerDragOver, editor.js:607-609), the release drops on the row under the release
/// point only, and Esc or a capture loss cancels. A press on a row's Dupe, Hide or Del button never starts a drag.
/// The overrides only forward to the internal Handle* seams, which tests call with panel coordinates.
/// </summary>
public partial class LayersPanel : UserControl
{
    /// <summary>Half the 4 px gap between rows (.layer-list gap 4px, styles.css:287): a row owns the gap's half next to it.</summary>
    internal const double HalfGap = 2;

    LayerRowViewModel? _pressed;   // the row a left press armed, until it moves or releases
    Point _pressPoint;
    bool _dragging;

    public LayersPanel() => InitializeComponent();

    LayersViewModel? ViewModel => DataContext as LayersViewModel;

    /// <summary>True between the drag's start and its drop or cancel.</summary>
    internal bool IsDragging => _dragging;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        HandleRowPress(RowFrom(e.OriginalSource as DependencyObject, stopAtButtons: true), e.GetPosition(this));
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        HandleMove(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed);
        if (_dragging) e.Handled = true;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        bool wasDragging = _dragging;
        HandleRelease(e.GetPosition(this));
        if (wasDragging) e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // LostMouseCapture bubbles: the list losing its own press capture when the drag captures this panel must not cancel it.
        if (ReferenceEquals(e.OriginalSource, this)) HandleCaptureLost();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HandleKey(key, Keyboard.Modifiers, RowFrom(e.OriginalSource as DependencyObject, stopAtButtons: false))) e.Handled = true;
    }

    /// <summary>A left press. On a row's body it arms a drag; anywhere else it disarms. A drag still running (its release was missed) is cancelled.</summary>
    internal void HandleRowPress(LayerRowViewModel? row, Point inPanel)
    {
        if (_dragging) Cancel();
        _pressed = row;
        _pressPoint = inPanel;
    }

    /// <summary>
    /// A move. Armed and past the system drag distance (SystemParameters.MinimumHorizontalDragDistance and
    /// MinimumVerticalDragDistance), it starts the drag and captures the mouse. Dragging, it sets the target to the
    /// row under the point, or clears it off every row. A move with the button up ends a drag whose release was
    /// missed, as a cancel.
    /// </summary>
    internal void HandleMove(Point inPanel, bool leftButtonDown)
    {
        if (!leftButtonDown)
        {
            _pressed = null;
            if (_dragging) Cancel();
            return;
        }
        if (!_dragging)
        {
            if (_pressed is null || ViewModel is not { } vm) return;
            if (Math.Abs(inPanel.X - _pressPoint.X) <= SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(inPanel.Y - _pressPoint.Y) <= SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = true;
            vm.BeginRowDrag(_pressed.Id);
            Cursor = Cursors.Hand;   // .layer-item:active is cursor: grabbing (styles.css:303); WPF has none, Hand is provisional (M3b look sign-off)
            CaptureMouse();
        }
        Retarget(inPanel);
    }

    /// <summary>The release: a drag drops on the row under <paramref name="inPanel"/> (re-read there, never a stale one) and ends.</summary>
    internal void HandleRelease(Point inPanel)
    {
        _pressed = null;
        if (!_dragging || ViewModel is not { } vm) return;
        Retarget(inPanel);
        _dragging = false;
        vm.CompleteRowDrop();
        Finish();
    }

    /// <summary>Capture lost mid-drag (Alt+Tab, a dialog): cancel, as 4.1's dragend cleanup (editor.js:741).</summary>
    internal void HandleCaptureLost()
    {
        if (_dragging) Cancel();
    }

    /// <summary>
    /// Esc cancels a running drag. Alt+Up / Alt+Down on a focused row select it and run MoveUpCommand /
    /// MoveDownCommand (one undo step each, none at an end), then keep the keyboard focus on that row.
    /// True when the key was used.
    /// </summary>
    internal bool HandleKey(Key key, ModifierKeys modifiers, LayerRowViewModel? focusedRow)
    {
        if (key == Key.Escape && _dragging)
        {
            Cancel();
            return true;
        }
        if (modifiers != ModifierKeys.Alt || key is not (Key.Up or Key.Down) || focusedRow is null || ViewModel is not { } vm) return false;
        if (_dragging) Cancel();
        vm.Selected = focusedRow;
        var command = key == Key.Up ? vm.MoveUpCommand : vm.MoveDownCommand;
        if (command.CanExecute(null)) command.Execute(null);
        // The collection's Move drops the row's container and its keyboard focus; the next layout pass makes a new
        // one. Lay out now so the focus lands on the moved row and a held Alt+Up keeps moving it.
        LayerList.UpdateLayout();
        if (LayerList.ItemContainerGenerator.ContainerFromItem(focusedRow) is ListBoxItem item) item.Focus();
        return true;
    }

    /// <summary>
    /// The row under a panel point and its half: each row's band is its box plus half the gap on either side, so
    /// the list has no dead stripes between rows; above is the top half, by the row's own midpoint (editor.js:607-609).
    /// Null outside the list's width or past the first and last rows.
    /// </summary>
    internal (int Id, bool Above)? RowAt(Point inPanel)
    {
        var inList = TranslatePoint(inPanel, LayerList);
        if (inList.X < 0 || inList.X > LayerList.ActualWidth) return null;
        foreach (var row in LayerList.Items.OfType<LayerRowViewModel>())
        {
            if (LayerList.ItemContainerGenerator.ContainerFromItem(row) is not ListBoxItem item) continue;
            double top = item.TranslatePoint(new Point(0, 0), this).Y, height = item.ActualHeight;
            if (inPanel.Y >= top - HalfGap && inPanel.Y < top + height + HalfGap) return (row.Id, inPanel.Y < top + height / 2);
        }
        return null;
    }

    void Retarget(Point inPanel)
    {
        if (ViewModel is not { } vm) return;
        if (RowAt(inPanel) is (int id, bool above)) vm.DragOverRow(id, above);
        else vm.ClearDropTarget();
    }

    void Cancel()
    {
        _dragging = false;
        _pressed = null;
        ViewModel?.EndRowDrag();
        Finish();
    }

    void Finish()
    {
        ClearValue(CursorProperty);
        // _dragging is already false, so the LostMouseCapture this raises is ignored.
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>The row a visual belongs to, or null. With <paramref name="stopAtButtons"/>, a row's own buttons count as no row.</summary>
    internal static LayerRowViewModel? RowFrom(DependencyObject? source, bool stopAtButtons)
    {
        for (var d = source; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (stopAtButtons && d is ButtonBase) return null;
            if (d is ListBoxItem { DataContext: LayerRowViewModel row }) return row;
        }
        return null;
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.LayersPanelDragTests|FullyQualifiedName~RBX15.App.Tests.Task4ViewTests"`
Expected: `Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21` for `RBX15.App.Tests.dll` (15 + 6).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877`; Parity.Tests `Passed: 35`; App.Tests `Passed: 604`. `ThemeKeyGuardTests` still passes, because no new theme key is used.

- [ ] **Step 7: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Views/LayersPanel.xaml src/RBX15.App/Views/LayersPanel.xaml.cs src/RBX15.App.Tests/LayersPanelDragTests.cs src/RBX15.App.Tests/Task4ViewTests.cs
git commit -m "feat(app): drag and Alt+arrow reorder in the layers panel, with 4.1's hint"
```

- [ ] **Step 8: Write the look-note tests**

Ledger: `ribbon-overflow-hscroll`, `shell-layout-skeleton`, `shell-window-size`, `panel-drop-zone`, `cross-text-select-and-selection`. Create `src/RBX15.App.Tests/ShellLookTests.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;

namespace RBX15.App.Tests;

/// <summary>
/// The look notes from the M3a smoke runs (.superpowers/m3a-smoke/RESULTS.md "Look notes", RESULTS-themed.md),
/// each checked against 4.1 laid out at the same size. The 4.1 numbers were measured once while planning M3b: a
/// scratch Electron script loaded the shipped editor.html in a hidden BrowserWindow (show: false, CPU raster, device
/// scale 1, the vendored fonts, no input of any kind) at a content size of 1184x761, the client area of the
/// 1200x800 window (the M3a smoke's UIA tree: 47 header + 28 tabs + 122 ribbon pane + 536 canvas area + 28 footer),
/// and at 624x441, the client area of the 640x480 minimum.
/// </summary>
public class ShellLookTests
{
    const double ClientWidth = 1184, ClientHeight = 761;

    /// <summary>4.1's Home pane (.ribbon-pane.active) scrollWidth in a 1184 client: Home overflows in 4.1 too.</summary>
    const double Home41ScrollWidth = 1432;

    /// <summary>4.1's .canvas-area height at 1184x761 on the Layout tab, the tab that holds Fill Style and Template Regions.</summary>
    const double Layout41CanvasArea = 531;

    /// <summary>4.1's .ribbon height on that tab.</summary>
    const double Layout41Ribbon = 155;

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    /// <summary>The main window's content laid out off-screen at a client size, the panel column set as the window would.</summary>
    static MainWindow Shell(double width, double height)
    {
        var w = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        ((ColumnDefinition)w.FindName("PanelColumn")).Width = new GridLength(ShellLayout.PanelWidthFor(width));
        var root = (FrameworkElement)w.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        return w;
    }

    [Fact] // look note 1 (ribbon-overflow-hscroll, keep): 4.1's Home is 1432 wide in a 1184 client, so it scrolls too
    public void At_the_default_size_the_ribbon_scrolls_sideways_and_no_group_shrinks() => StaThread.Run(() =>
    {
        // A pinned ruling, not a measurement in this test: it records 4.1's measured 1432 against the 1184 client (T4-R8).
        Assert.True(Home41ScrollWidth > ClientWidth);
        var w = Shell(ClientWidth, ClientHeight);
        var home = Descendants<HomeRibbon>((DependencyObject)w.Content).Single();
        var scroller = Descendants<ScrollViewer>(home).First();
        Assert.Equal(ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);
        Assert.True(scroller.ExtentWidth > scroller.ViewportWidth);
        Assert.Equal(Visibility.Visible, scroller.ComputedHorizontalScrollBarVisibility);
        var groups = (StackPanel)home.FindName("Groups");
        Assert.All(groups.Children.OfType<FrameworkElement>(), g => Assert.Equal(g.DesiredSize.Width - g.Margin.Left - g.Margin.Right, g.ActualWidth, 1));
    });

    [Fact] // look note 2: v5's Home carries Fill Style and Template Regions (4.1's Layout tab) until M4, so the area is held to that tab's
    public void At_the_default_size_the_canvas_area_is_no_shorter_than_4_1s_with_the_same_groups() => StaThread.Run(() =>
    {
        var w = Shell(ClientWidth, ClientHeight);
        double ribbon = ((FrameworkElement)w.FindName("Ribbon")).ActualHeight;
        double area = ((FrameworkElement)w.FindName("CanvasArea")).ActualHeight;
        Assert.InRange(ribbon, 0, Layout41Ribbon);
        Assert.InRange(area, Layout41CanvasArea, ClientHeight);
    });

    [Theory] // look note 4: 4.1's div wraps its text; measured 251x84 in the 280 panel and 161x100 (three lines) in the 200 panel
    [InlineData(251, 84)]
    [InlineData(161, 100)]
    public void The_drop_zone_label_wraps_like_4_1s(double width, double height41) => StaThread.Run(() =>
    {
        var zone = new DropZone { DataContext = new LayersViewModel(new EditorSession(), new ImportService(new EditorSession(), new FakeStatus()), new FakeFileDialogs()) };
        StaThread.Pump();
        zone.Measure(new Size(width, 2000));
        zone.Arrange(new Rect(0, 0, width, 2000));
        zone.UpdateLayout();
        var button = (Button)zone.FindName("Zone");
        var label = (TextBlock)button.Template.FindName("Label", button);
        Assert.Equal(TextWrapping.Wrap, label.TextWrapping);
        Assert.InRange(label.DesiredSize.Width - label.Margin.Left - label.Margin.Right, 0, width - 28 + 0.01);   // never wider than the box: no clipped words
        Assert.InRange(button.ActualHeight, height41 - 2, height41 + 2);
    });

    [Fact] // look note 3: the untitled 52x26 and 162x26 windows are the keyboard-focus tooltips of Color 1 and Add image
    public void The_lingering_windows_are_the_persistent_keyboard_focus_tooltips() => StaThread.Run(() =>
    {
        // The smoke saw them after Esc returned focus to Color 1 and after the Open dialog returned focus to Add image.
        // WPF shows a tooltip on keyboard focus (ShowsToolTipOnKeyboardFocus unset) and keeps it until focus moves,
        // Esc or Ctrl (ShowDuration int.MaxValue): WCAG 1.4.13's persistent, dismissible content. This fact mostly pins
        // WPF's defaults (the ToolTip's size, ShowDuration) as the identification of the smoke's windows: a pinned ruling (T4-R10).
        foreach (var (text, size) in new[] { ("Color 1", (52.0, 26.0)), ("Add an image file as a layer.", (162.0, 26.0)) })
        {
            var tip = new ToolTip { Content = text };
            tip.ApplyTemplate();
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(size, (Math.Round(tip.DesiredSize.Width), Math.Round(tip.DesiredSize.Height)));
        }
        var button = new Button();
        Assert.Null(ToolTipService.GetShowsToolTipOnKeyboardFocus(button));
        Assert.Equal(int.MaxValue, ToolTipService.GetShowDuration(button));
        var session = new EditorSession();
        var status = new FakeStatus();
        var file = new FileGroup { DataContext = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)) };
        StaThread.Pump();
        file.Measure(new Size(400, 200));
        file.Arrange(new Rect(0, 0, 400, 200));
        Assert.Contains(Descendants<Button>(file), b => Equals(b.ToolTip, "Add an image file as a layer."));
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`, then `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ShellLookTests"`
Expected:
- `Failed:     2, Passed:     3, Total:     5`: both `The_drop_zone_label_wraps_like_4_1s` rows fail with `Expected: Wrap` / `Actual: NoWrap`.
- The ribbon, canvas-area and tooltip facts already pass. They pin the keep rulings T4-R8 to T4-R10.

- [ ] **Step 9: Fix the drop zone label**

Ledger: `panel-drop-zone`. In `src/RBX15.App/Views/DropZone.xaml`, replace:

```xml
                        <!-- padding 24px 12px; 13px; fg-muted #5e6b7f; centered (styles.css:268-271) -->
                        <TextBlock x:Name="Label"
                                   Margin="12,24"
                                   TextAlignment="Center"
```

with:

```xml
                        <!-- padding 24px 12px inside the 2px border; 13px; fg-muted #5e6b7f; centered (styles.css:266-271).
                             The Rectangle's stroke takes no layout room, so the margin carries the border too (14,26).
                             The text wraps like the div's, so the 200px panel (styles.css:694-697) shows three
                             lines instead of clipping "Drag & drop PNG images". -->
                        <TextBlock x:Name="Label"
                                   Margin="14,26"
                                   TextWrapping="Wrap"
                                   TextAlignment="Center"
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ShellLookTests"`
Expected: `Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 609, Failed: 0`.

- [ ] **Step 10: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Views/DropZone.xaml src/RBX15.App.Tests/ShellLookTests.cs
git commit -m "fix(app): drop zone label wraps at the narrow panel; pin the ribbon, canvas and tooltip look notes"
```

- [ ] **Step 11: Ctrl+Z on the window (critic MJ2)**

The contract's out-of-scope line reads "keyboard shortcuts (bare keys and Ctrl combos, **except Ctrl+Z**)", so Ctrl+Z is M3b's, and nothing at `4e0be14` binds it. 4.1 handled it in its global key handler (`e.ctrlKey && e.key.toLowerCase() === 'z'`, editor.js:1797; Ctrl+Y and Ctrl+Shift+Z redo at :1794 stay M4). This step binds it on the window to the Edit group's command, so it runs the same `EditorSession.Undo` the button runs: one step, and an open gesture is committed first (M3a Review Focus 1). A focused TextBox handles its own Ctrl+Z first through `TextBoxBase`'s Undo command, so typing in a field is undone before the document. Ledger: `layers-reorder-drag-2` ("one Ctrl+Z restores"), `fillstyle-color-pickers`.

In `src/RBX15.App.Tests/ShellTests.cs`, add `using System.Windows.Input;` after `using System.Windows.Controls;`, then replace:

```csharp
        Assert.Equal("PerMonitorV2", manifest.Descendants(ws + "dpiAwareness").Single().Value);
    }
}
```

with:

```csharp
        Assert.Equal("PerMonitorV2", manifest.Descendants(ws + "dpiAwareness").Single().Value);
    }

    [Fact] // M3b: Ctrl+Z is the one shortcut before M4 (planning contract); it runs the Edit group's Undo, no input sent
    public void Ctrl_Z_on_the_window_runs_the_Edit_groups_undo() => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var binding = Assert.Single(window.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.Z && b.Modifiers == ModifierKeys.Control);
        Assert.Same(main.Home.Edit.UndoCommand, binding.Command);
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`, then `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.ShellTests"`
Expected: `Failed:     1, Passed:    14, Total:    15`: `Ctrl_Z_on_the_window_runs_the_Edit_groups_undo` fails in `Assert.Single()` (no `KeyBinding`).

In `src/RBX15.App/MainWindow.xaml`, replace:

```xml
        mc:Ignorable="d">

  <Grid x:Name="Viewport">
```

with:

```xml
        mc:Ignorable="d">

  <Window.InputBindings>
    <!-- Ctrl+Z is M3b's one shortcut (planning contract: shortcuts are M4 except Ctrl+Z). A focused TextBox
         handles its own Ctrl+Z first (TextBoxBase's Undo command), so typing is undone before the document. -->
    <KeyBinding Key="Z" Modifiers="Control" Command="{Binding Home.Edit.UndoCommand}" />
  </Window.InputBindings>

  <Grid x:Name="Viewport">
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.ShellTests"`
Expected: `Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15` for `RBX15.App.Tests.dll` (measured by the reconciler on a scratch copy of `4e0be14` with this edit).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 877, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 610, Failed: 0`.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/MainWindow.xaml src/RBX15.App.Tests/ShellTests.cs
git commit -m "feat(app): Ctrl+Z on the window runs the Edit group's undo"
```

- [ ] **Step 12: Task 4 checks**

Run: `grep -c "MoveUpCommand\|MoveDownCommand" src/RBX15.App/Views/LayersPanel.xaml`
Expected: `0`. Up and Down live in the Selected layer panel; the keys reach the commands from code-behind.

Run: `grep -rn "DoDragDrop" src/RBX15.App`
Expected: no output (T4-R1).

Run: `git diff --stat 4e0be14 -- editor.js editor.html styles.css main.js lib tools/parity-ref`
Expected: no output. The Electron app and the 4.1 harness are untouched; the 4.1 measure script is scratch only.

Run: `git status --short`
Expected: no output after Step 11.

**Handoff to Task 5** (evidence only; Task 5 owns the ledger, `divergences.md` and the checklist; nothing here is edited by Task 4):

1. **`layers-reorder-drag`:**
   - The parityCheck's first half, "reorder index math with 4 layers across all (from,to,pos) combos against JS", is met by `A_drop_reorders_as_4_1s_splice_in_one_step_or_none`: 24 rows whose expected orders node printed from editor.js:634-652.
   - Its second half, "UI test drag top row to bottom half of last row", cannot be automated (WPF ignores posted mouse input). It is proven through seams by `LayersPanelDragTests` plus Este's manual line M1 below.
   - Under the honesty rule, Task 5 either rewords the parityCheck to that evidence or leaves the UI half open.
2. **`layers-reorder-drag-2`:**
   - Divergence addition: "Alt+Up and Alt+Down move the focused row; pressing a row selects it, so a dragged row ends selected" (T4-R3, T4-R7).
   - Evidence: `Bottom_onto_the_top_rows_upper_half_becomes_the_top_and_one_undo_restores`, `Alt_up_and_down_...`, and M1 and M2.
3. **`layer-reorder-dragging-class-timer`** (keep): `The_drag_dims_the_source_row_and_draws_the_targets_edge`, plus M1's look. Wording: v5 shows the row in place at .4 with no drag ghost; the edge is a straight 2 px bar (provisional, look sign-off).
4. **`ribbon-overflow-hscroll`** (keep): correct the quirk text. 4.1's Home pane is 1432 px in a 1184 px client, so it overflows at 1200x800 (measured, T4-R8). Evidence: `At_the_default_size_the_ribbon_scrolls_sideways_and_no_group_shrinks`.
5. **`cross-text-select-and-selection`** (keep): add the divergence that v5 also shows a control's tooltip while it has keyboard focus, persistent until focus moves, Esc or Ctrl (T4-R10).
6. **`panel-drop-zone`:** the narrow-panel clip is fixed; evidence `The_drop_zone_label_wraps_like_4_1s`.
7. **`shell-layout-skeleton`:** note T4-R9's measured 531/558/508 for M4's fixed-height decision.
8. **Manual lines for Este's checklist.** Automation cannot do these: WPF ignores posted mouse input, and real keyboard focus needs the live app.
   - **M1:** With five layers, drag the bottom row onto the top half of the top row. The source dims while dragging, and a cyan bar shows on the target's top edge. On release it becomes the top row, and one Ctrl+Z restores the order.
   - **M2:** Click a row, then press Alt+Up three times. The row climbs one place per press, the focus ring stays on it, and at the top nothing happens.
   - **M3:** Start a drag, then press Esc; then start another and Alt+Tab away. Both cancel with nothing moved.
   - **M4:** With about twenty layers, drag the top row and wheel the left panel down mid-drag. Drop on the last row's lower half: the row lands at the bottom (T4-R6).
   - **M5:** Tab to Color 1 and to Add image. Each shows its tooltip under it, and it closes when Tab moves on (T4-R10).
   - **M6** (Step 11): Ctrl+Z undoes one step from anywhere in the window; with typing pending in the X field, Ctrl+Z undoes the typing only.

**Interface additions requested by Task 4:**

1. **New, `LayersPanel` internal seams** (interfaces.md §2 says only "Code lives in LayersPanel.xaml.cs"):
   - `internal const double HalfGap = 2` and `internal bool IsDragging`.
   - `internal void HandleRowPress(LayerRowViewModel? row, Point inPanel)`, `internal void HandleMove(Point inPanel, bool leftButtonDown)` and `internal void HandleRelease(Point inPanel)`.
   - `internal void HandleCaptureLost()` and `internal bool HandleKey(Key key, ModifierKeys modifiers, LayerRowViewModel? focusedRow)`.
   - `internal (int Id, bool Above)? RowAt(Point inPanel)` and `internal static LayerRowViewModel? RowFrom(DependencyObject? source, bool stopAtButtons)`.
   - The mouse and key overrides only forward to these.
2. **New, named parts:** `LayersHeading` (TextBlock) and `ReorderHintRun` (Run) in `LayersPanel.xaml`, and `DropAbove` and `DropBelow` (Rectangle) in its item template.
3. **Change, `LayerRowViewModel.IsDragSource` and `DropIndicator`:** `{ get; internal set; }`. Only `LayersViewModel` sets them.
4. **Clarification, `DragOverRow`:** "ignored for the source row" is implemented as "clears the target over the source row". That is the stale-target fix (T4-R2); keeping the old target there is 4.1's bug.
5. **Clarification, `CompleteRowDrop`:** returns true iff the order changed. False covers no live target, a missing id, and a drop that keeps the order (no step). It always ends the drag, as 4.1's handleLayerDrop cleaned up on every path.
6. **New test class, `ShellLookTests`** (5 cases), not in interfaces.md §5's table. It holds the measured look-note rulings T4-R8 to T4-R11.
7. **For Task 5:** the handoff above, meaning the ledger wording, the divergences and manual lines M1 to M5. For the look sign-off captions, add the `PanelHeading` uppercase observation.
8. **For M4:** reserve Alt+Up and Alt+Down on a focused layer row in the keyboard map (T4-R7). Also weigh T4-R9's numbers before fixing the ribbon height to the tallest tab.

<details>
<summary>Appendix (scratch, not committed): the 4.1 layout measure script behind T4-R8 to T4-R11</summary>

Run from a scratch copy, with the file at `tools/parity-ref/measure41.js` in that copy: `env -u ELECTRON_RUN_AS_NODE <repo>/node_modules/electron/dist/electron.exe tools/parity-ref/measure41.js --sizes=1184x761,624x441`.

```js
// Scratch (not committed): measures 4.1's layout in a hidden window (show: false). In-page click() switches
// ribbon tabs, as app-harness.js drives the page; no desktop input of any kind.
'use strict';
const { app, BrowserWindow, protocol } = require('electron');
const path = require('node:path');
const ROOT = path.resolve(__dirname, '..', '..');
const fonts = require(path.join(ROOT, 'tools/parity-ref/fonts.js'));
app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
app.on('window-all-closed', () => {});
app.commandLine.appendSwitch('force-device-scale-factor', '1');
const sizes = (process.argv.find(a => a.startsWith('--sizes=')) || '--sizes=1184x761,624x441').slice(8).split(',');
process.on('unhandledRejection', e => { console.error('UR', e); app.exit(2); });
app.whenReady().then(async () => {
  for (const scheme of ['https', 'http']) protocol.handle(scheme, (req) => {
    try { const r = fonts.respond(req.url); if (r) return new Response(r.body, { status: 200, headers: r.headers }); } catch (e) {}
    return new Response('blocked', { status: 404 });
  });
  const out = [];
  for (const s of sizes) {
    const [w, h] = s.split('x').map(Number);
    const win = new BrowserWindow({ show: false, width: w, height: h, useContentSize: true,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false } });
    try { await win.loadFile(path.join(ROOT, 'editor.html')); } catch (e) { console.error('load', e.message); }
    await new Promise(r => setTimeout(r, 1500));
    const r = await win.webContents.executeJavaScript(`(async () => {
      await document.fonts.ready;
      const box = (el) => { if (!el) return null; const b = el.getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height }; };
      const res = { inner: [innerWidth, innerHeight] };
      res.header = box(document.querySelector('header'));
      res.ribbon = box(document.querySelector('.ribbon'));
      res.tabs = box(document.querySelector('.ribbon-tabs'));
      res.footer = box(document.querySelector('footer'));
      res.canvasArea = box(document.querySelector('.canvas-area'));
      res.canvas = box(document.getElementById('mainCanvas'));
      res.panel = box(document.querySelector('.panel'));
      res.dropZone = box(document.getElementById('dropZone'));
      res.dropZoneScroll = [document.getElementById('dropZone').scrollWidth, document.getElementById('dropZone').clientWidth];
      res.panes = {};
      const tabs = [...document.querySelectorAll('.ribbon-tab')];
      for (const t of tabs) {
        t.click();
        await new Promise(r => setTimeout(r, 50));
        const pane = document.querySelector('.ribbon-pane.active');
        res.panes[t.textContent.trim()] = { pane: box(pane), scrollW: pane.scrollWidth, clientW: pane.clientWidth,
          ribbon: box(document.querySelector('.ribbon')), canvasArea: box(document.querySelector('.canvas-area')),
          groups: [...pane.querySelectorAll(':scope > .ribbon-group')].map(g => ({ label: (g.querySelector('.ribbon-group-label')||{}).textContent, w: g.getBoundingClientRect().width, h: g.getBoundingClientRect().height })) };
      }
      tabs[0].click();
      return res;
    })()`);
    out.push({ size: s, ...r });
    win.destroy();
  }
  console.log(JSON.stringify(out, null, 1));
  app.exit(0);
});
```

Output, condensed:

| Content | Header | Ribbon (Home / Layout / Details / Text) | Canvas area (Home / Layout / Details / Text) | Home pane scrollWidth | Panel | Drop zone |
| --- | --- | --- | --- | --- | --- | --- |
| 1184x761 | 47 | 128 / 155 / 84 / 178 | 558 / 531 / 602 / 508 | 1432 | 280 | 251x84 |
| 624x441 | 47 | 128 / 155 / 94 / 188 | 238 / 211 / 272 / 178 | 1432 | 200 | 161x100 |

The Home groups measure, in px: Mode 109, Tools 418, Brush 111, Placement 196, Edit 179, File 234 and Settings 161, all 74 tall. The Layout groups: Fill Style 272 and Template Regions 412, both 101 tall.

</details>

---

### Task 5: Close-out: look captures, the sign-off page, Este's hand checks, the ledger and final verification

**Files:**

- Create (committed):
  - `tools/parity-ref/look-states.json`: the seven look states as semantic ops (six contract states plus one supplementary narrow-window state).
  - `tools/parity-ref/look-ref.js`: the 4.1 full-window capture through Electron `webContents.capturePage`, run by the controller.
- Modify (committed):
  - `tools/parity-ref/README.md`: one bullet for `look-ref.js` and a "Look capture (M3b)" section.
  - `src/RBX15.App.Tests/LedgerScenarioTests.cs`: three `using` lines and 12 M3b scenario facts with five private helpers (App.Tests +12).
  - `docs/port/parity-ledger.json`: amend `eraser-cursor` and `cross-text-select-and-selection` to `fix`; reword four divergence texts and append one sentence to four more; correct one behavior text; add four rows; narrow 69 parityCheck texts; close up to 80 rows (Step 13), then up to 11 look rows after the sign-off (Step 16).
  - `docs/port/parity-ledger.md`, `docs/port/divergences.md` (generated by `tools/ledger/ledger.py`).
- Scratch only (git-ignored under `.superpowers/`):
  - `.superpowers/m3b-look/driver/uia/uia.csproj` and `Program.cs`: the v5 look driver, built from `.superpowers/m3a-smoke/driver-themed/uia/`.
  - `.superpowers/m3b-look/build_page.py`: assembles the comparison page.
  - `.superpowers/m3b-look/ref/`, `v5/`, `page/`, `SIGNOFF.txt`: the captures, the page and Este's verdict.
  - `.superpowers/logs/m3b-evidence.py`, `m3b-ledger.py`, `m3b-look-ledger.py`, `m3b-hand.txt` and the test logs.

**Interfaces:**

- Consumes (evidence and scripted use; Task 5 changes no production code):
  - Task 1: `ViewOptions.SelectedLayerId`, `LivePreview` (`Stroke`, `IsEraser`, `Alpha`), `LayerOps.SetOpacity`, `LayerAdjustments` (`Default`, `From`), `Tools.CommitDrawing`, `Tools.DrawingName`, `Renderer.RenderViewImage`; `tests/goldens/view-select/` (`scenes.json` with each scene's `name`); test classes `SelectionGeometryTests`, `LayerOpsEditTests`, `ViewRendererTests`, `RendererOverlayTests`, `LivePreviewTests`, `GoldenViewSelectTests`, `ToolsTests`, `EraserTests`, `HistoryTests`.
  - Task 2: `Tool`, `CanvasGesture`, `CanvasInteraction` (`PointerDown`, `PointerMove`, `PointerUp`, `CaptureLost`, `Wheel`, `EndWheelBurst`, `Gesture`, `Preview`), `ToolsViewModel` (`Current`, `Buttons`, `ToggleCommand`, `Set`), `ToolButtonViewModel` (`Tool`, `Label`, `IsActive`, `Command`), `BrushViewModel` (`Color`, `Size`, `Fill`, `SkColor`), `CanvasViewModel.Interaction`, `HomeViewModel.Tools` and `.Brush`, `ImportService.LayersAdded` and `LayersViewModel.LayerPicked` through `Composition.Build`, the canvas peer's name `Template canvas` and its value; `EditorSession.Changing` (a wheel burst closes before any other edit); test classes `CanvasInteractionTests`, `EditorSessionTests`, `ToolsViewModelTests`, `BrushViewModelTests`, `CanvasCursorsTests`, `CanvasAutomationPeerTests`, `TemplateCanvasHostTests`.
  - Task 3: `MainViewModel.LayerProperties`, `LayerPropertiesViewModel` (`HasSelection`, `EmptyText`, `Name`, `XText`, `YText`, `ScalePercent`, `ScaleLabel`, `Rotation`, `RotationLabel`, `OpacityPercent`, `OpacityLabel`, `Brightness`, `BrightnessLabel`, `AdjustmentsExpanded`, `FlipHCommand`, `ResetAdjustmentsCommand`, `BeginSliderDrag`); the panel's automation names `Rotation degrees` and `Image adjustments`; test classes `LayerPropertiesViewModelTests`, `LayerPropertiesPanelTests`, `SliderGestureTrackerTests`.
  - Task 4: `LayersViewModel` (`ReorderHint`, `BeginRowDrag`, `DragOverRow`, `ClearDropTarget`, `CompleteRowDrop`, `EndRowDrag`, `MoveUpCommand`, `Selected`, `Rows`, `DropFiles`), `LayerRowViewModel` (`IsDragSource`, `DropIndicator`, `Name`, `Id`), `DropIndicator`; the window's Ctrl+Z `KeyBinding` (Task 4 Step 11); test classes `LayersReorderTests`, `LayersPanelDragTests`, `ShellLookTests`, `ShellTests`.
  - M3a: `EditorSession` (`Apply`, `ApplyWithSelection`, `Select`, `TakeId`, `Undo`, `UndoCount`, `SelectedLayerId`), `TestShell.Main`, `StaThread.Run`, `CanvasCoordinates.ToTemplate`, `MainViewModel.Status` (`StatusViewModel.Message`), `HomeViewModel.Regions`, `CanvasViewModel.ZoomInCommand` and `.Zoom`; `LedgerScenarioTests`' helpers `Solid`, `PixelOf`, `WritePng` and colors `Red`, `Blue`, `Slate`.
  - Tooling: `tools/parity-ref/fonts.js` (`respond`, `FONTS_DIR`), Electron 41.1.0, `python tools/ledger/ledger.py done | check`.
- Produces:
  - `look-ref.js` and `look-states.json` (committed tooling); the scratch v5 driver; the published sign-off page; `LedgerScenarioTests` +12 facts.
  - The ledger: 417 rows, with `122 + C1 + C2` done, where C1 is the number of ids Step 13 closes (80 if every gate holds) and C2 the look ids Step 16 closes (11 if Este signs off and both metric verdicts hold).
  - Este's hand-check results and sign-off verdict, recorded under `.superpowers/`. Push, PR and dashboard steps belong to the controller.

**Rulings made in this task:**

1. **The look capture is a reference for a person, not a golden.** `look-ref.js` writes only under `.superpowers/m3b-look/ref/`; no test reads it and nothing under `tests/goldens` changes. Two whole-window renderers (Chromium, WPF) never meet the golden rule on text and chrome, so the comparison is Este's side-by-side sign-off plus a metrics table of element rectangles, which is what the look rows' checks ask for.
2. **`look-ref.js` does not inject `app-harness.js`** (interface sheet section 4 says it drives the page with it). The harness pins `#mainCanvas` to the viewport origin with `position: fixed` (app-harness.js:15-19) so synthesized mouse coordinates are integers; in a full-window capture that would move the canvas over the header. The look ops need no mouse events: every op is a real control's click, input or change event, run by a small in-page driver (`lookPage`), with the same font interception and network block as `view-ref.js`.
3. **Both captures use the v5 client size.** The driver measures the v5 client area of the 1200x800 window and prints `client=<W>x<H>`; `look-ref.js` sets 4.1's content size to exactly that (`useContentSize`, menu hidden as main.js:23 does). 4.1's Electron frame at 1200x800 has the same Windows frame metrics, so the content areas match a real 4.1 window, and the PNGs pair pixel for pixel.
4. **State 07 is supplementary** (`07-narrow-700`, a 700 DIP client): it gives `ribbon-overflow-hscroll` (keep, "shrink to 700 px") its evidence on both sides at no extra risk. The six contract states keep their names and contents (interfaces section 4). State 02 adds one 4.1-only capture of the Layout tab, because 4.1's region grid lives there (editor.html:145-171) while v5 shows it on Home until M4. States 03 and 05 scroll the left panel to its end (`scrollPanel`), or the expanded Image adjustments sit below the fold in both apps (measured on 4.1).
5. **The v5 driver is scratch and posts nothing.** It is built from `driver-themed/uia/` with every click and key command removed: UI Automation patterns only (Invoke, SelectionItem, RangeValue, Value, Scroll, ExpandCollapse, Toggle, Transform, Window), on elements PID-checked against the process it launched, and PrintWindow of that process's own main window. The one dialog it touches is the app's own Add image file dialog. It refuses to run below or above 100% display scale. Its full safety rules head `Program.cs`.
6. **Manual items are numbered H1-H10** ("hand"), so they never read as milestones M1-M6 in ledger text.
7. **Closing versus narrowing** follows M3a ruling 1 and the honesty rule: a row closes when the M3b evidence meets its parityCheck, or its check is narrowed to the evidence used ("Narrowed in M3b", "Reworded in M3b"). A row stays open when the unmet clause is behavior a later milestone ships. That keeps open:
   - `tools-toggle-off` and `home-tool-buttons`: their checks press tool keys (M4).
   - `draw-tool-switch-midstroke`: `CanvasInteraction.Cancel` is pinned (the Review Focus scenario), but under mouse capture the only mid-stroke tool switch is a tool key (M4).
   - `draw-pen-incremental-stroke`: no pen golden exists (approved divergence; pen previews are not frozen).
   - `props-adjust-sliders` and `layers-selected-panel`: per-slider extremes against Electron are not captured (as M3a left `fillstyle-angle-scale`).
   - `render-pipeline-canvas-reset`: no frame-time measurement; correctness alone is not its 60 fps budget.
8. **`eraser-cursor` (keep) contradicts `cursor-eraser` (fix)**: the keep row says "capped at diameter 240", the fix row drops 4.1's 120 radius cap. `eraser-cursor` is amended to `fix` with a divergence, as M3a amended `overlay-mask-checkbox`, and `cursor-eraser`'s divergence is reworded to state the Win32 256 px cursor cap (interfaces R15).
9. **Four new rows**: `wheel-scale-undo-burst` (fix; the 400 ms burst is a v5 choice with no 4.1 counterpart), `eraser-click-without-move` (keep; the M2a carry-forward ruling R3, pinned), `canvas-automation-peer` (fix; the new canvas peer), and `overlay-knob-glyph-rasterization` (fix; Task 1's T1-3 note, in the form of `wordart-glyph-rasterization`; reconciler, critic mn6). All four close in Step 13 when their gates hold.
12. **The Tasks 1-4 handoffs land here** (reconciler, critic MJ3, MJ4, mn5, mn6, mn13): `select-rotate-handle-hit` gets Task 1's T1-1 divergence, and it and `select-rotate-handle` are narrowed to the drawn-knob evidence instead of closing on checks v5 fails on purpose; `draw-empty-no-layer` is narrowed, because a Pen click draws the approved dot; `cursor-eraser` states Task 2 ruling 6's per-scale cap; `cross-text-select-and-selection` is amended to `fix` for the keyboard-focus tooltips (Task 4 T4-R10) and stays open; `layers-reorder-drag-2`, `canvas-cursor-states`, `props-name-xy` and `panel-drop-zone` each gain one divergence sentence; and `ribbon-overflow-hscroll`'s behavior text is corrected to the measured 1432 px Home pane.
10. **Look rows close only after the sign-off**, in Step 16, and two of them also need the measured verdicts the page builder prints: `shell-layout-skeleton` (every boundary within 1 DIP) and `font-ui-runtime-google` (the Download template button within 2 DIPs of 4.1's width). A failed verdict leaves the row open even when Este signs off, and the controller lists it for the fix round.
11. **Ctrl+Z is bound** (Task 4 Step 11, critic MJ2): the contract keeps Ctrl+Z in M3b, so the window's `KeyBinding` runs the Edit group's Undo, pinned by `ShellTests.Ctrl_Z_on_the_window_runs_the_Edit_groups_undo` and hand check H6. The narrowed texts of `layers-reorder-drag-2` and `fillstyle-color-pickers` keep their "one Ctrl+Z" clauses and name that evidence.

**The state Task 5 starts from.** Tasks 1-4 are committed, the view-select goldens are captured (Task 1's controller-run step) and every suite is green. Tasks 1-4 close no ledger rows: `docs/port` is unchanged from `4e0be14`. If any of that is not true, stop and report to the controller.

**Test counts.** Task 5 adds Engine.Tests +0, Parity.Tests +0, App.Tests +12. Tasks 1-4 end on Engine.Tests 877 (Task 1's +159; Tasks 2-4 add no engine test) and App.Tests 610 (Task 2 ends at 481, already net of interfaces R1's three removed click facts; Task 3 +76; Task 4 +53, its Ctrl+Z fact included). The `node --test "tools/parity-ref/*.test.js"` count stays 14 (measured on `4e0be14` 2026-10-15; no task adds a `.test.js`).

| After | Engine.Tests | Parity.Tests | App.Tests |
| --- | --- | --- | --- |
| `4e0be14` (main) | 718 | 35 | 411 |
| Task 4 (Step 1 baseline) | 877 | 35 | 610 |
| Task 5 Step 6 and Step 14 | 877 | 35 | 622 |

Every run has 0 failed and 0 skipped.

**Measured for this task (2026-10-15, scratch copies under the session scratchpad; the repo was never built into or written):**

- `look-ref.js` ran all seven states offscreen against a `git archive` copy of `4e0be14` with the repo's Electron 41.1.0: `ok 7` in 39 s, eight PNGs at 1184x761 (700x761 for state 07), `metrics.json` and `index.json` with the blob hashes. The captures were inspected: state 03 shows the drip at 30° with its overlay and the expanded panel after the scroll op. `git hash-object` works outside a git repository, so provenance is recorded from any checkout.
- 4.1 metrics at 1184x761, state 01, in CSS px: header title `24, 10, 311.9x26`; Home ribbon pane `0, 74, 1184x100`; Download template `1035.1, 112, 122.6x24`; left panel `0, 175, 280x558`; canvas area `280, 175, 904x558`. The M3a launch tree puts v5's Home ribbon at y 75 with height 121 and its canvas area at y 197 with height 536, so `shell-layout-skeleton`'s 1 px verdict depends on Task 4's canvas-height look note; the gate in Step 16 is honest either way.
- The v5 driver (`Program.cs` and `uia.csproj` below) builds with `-c Release`: `0 Warning(s)`, `0 Error(s)` (TreatWarningsAsErrors on). It was not run: running it launches the app.
- The 12 scenarios compile under `dotnet build src/RBX15.slnx -c Release -warnaserror` (xUnit analyzers included) against compile-only stand-ins of the interface sheet's Task 1-4 members (bodies throw): `0 Warning(s)`, `0 Error(s)`. `--list-tests` counts 26 `LedgerScenarioTests` cases and 423 App.Tests cases (411 + 12). On the stand-ins the 14 M3a facts pass and the 12 new ones fail as expected; their pass is Step 6's check against the real Tasks 1-4.
- **Measured defect for Task 2** (Additions requested, item 1): with a namespace `RBX15.App.Canvas`, the unqualified `Canvas.SetLeft`/`SetTop` at `ColorPickerPopup.xaml.cs:168-170` and `Canvas.GetLeft`/`GetTop` at `ColorPickerPopupTests.cs:103`, `:106`, `:107` resolve to the namespace and fail with CS0234. Qualifying them as `System.Windows.Controls.Canvas` builds clean.
- Both ledger scripts, as printed below after the reconciler's changes, ran on a scratch copy of `4e0be14`'s `docs/port` and `tools/ledger` (a fresh git repository under the session scratchpad) with all 80 ids: Step 13 prints `amended 2, reworded 4, appended 4, added 4, narrowed 69; 417 rows`, then `417 rows, 0 problem(s)` and `417 rows; 202 done; 200 fix`; numstat `235 158` JSON, `82 78` ledger md, `14 9` divergences md. Step 16 prints `narrowed 11 look rows; 417 rows`, `417 rows; 213 done`; numstat `22 22` JSON, `12 12` ledger md. `python -m unittest discover -s tools/ledger` ran 8 tests, OK.
- `build_page.py` built the page from the 4.1 captures with stand-in v5 files (copies plus a 0.5 px offset): 15 images, 1.6 MB, both verdicts printed. `m3b-evidence.py` parsed a real normal-verbosity App.Tests log (410 passed lines) and printed one line per row.
- Observed, not Task 5's: on a pristine `git archive` copy of `4e0be14` in this session, `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails with `Expected: 158,71 Actual: 157.60000000000002,71.2` (a layout-rounding difference that depends on the session's environment). Este's machine ran 411 green; Step 1 says what to do if it shows up.

**The rule for closing.** As in M3a: a row closes only when the evidence on this branch proves its whole parityCheck, or, for a `fix` row, also its whole v5 divergence sentence. Evidence means a unit test, a golden, a `LedgerScenarioTests` fact, an H-item Este passed, a line of the look run's `RESULTS.md`, or Este's sign-off. Ruling 7 says when a parityCheck is narrowed and when a row stays open.

- [ ] **Step 1: Record the baseline Tasks 1-4 left**

Run from the repo root:

```bash
mkdir -p .superpowers/logs
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test src/RBX15.slnx -c Release --no-build > .superpowers/logs/m3b-t5-baseline.log 2>&1
grep -E "(Passed|Failed)!" .superpowers/logs/m3b-t5-baseline.log
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
git diff --stat "$BASE" -- docs/port
python -c "import json,os; s=json.load(open('tests/goldens/view-select/scenes.json',encoding='utf-8')); print(len(s), 'scenes;', len([f for f in os.listdir('tests/goldens/view-select') if f.endswith('.png')]), 'PNGs')"
grep -n "Assert.Equal(15, views.Count)" src/RBX15.App.Tests/ThemeKeyGuardTests.cs
grep -c "^    \[Fact\]" src/RBX15.App.Tests/LedgerScenarioTests.cs
```

Expected:

- `base 4e0be14`.
- `0 Warning(s)` and `0 Error(s)`.
- Three summary lines, each `Failed: 0` and `Skipped: 0`: `RBX15.Engine.Tests.dll` `Passed: 877`, `RBX15.Parity.Tests.dll` `Passed: 35`, `RBX15.App.Tests.dll` `Passed: 610`.
- `413 rows; 122 done; 195 fix`.
- The `docs/port` diff prints nothing.
- `10 scenes; 10 PNGs` (interfaces section 3).
- One line: Task 3 raised the view count to 15 (Additions requested, item 9).
- `14`: the M3a scenarios.

If a summary shows `Failed` or `Skipped` above 0, or the ledger is not at 122 done, stop and report it to the controller: Tasks 1-4 are not finished. If the only failure is `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` with `157.6` against `158`, report it as environment-dependent (it fails the same way on `4e0be14` in some sessions; see the measurements above) and ask the controller before going on.

- [ ] **Step 2: Write the look states and the 4.1 capture script**

Create `tools/parity-ref/look-states.json`:

```json
[
  {"name": "01-launch", "shows": "The app at launch, Home tab, empty template, 100% zoom.", "ops": []},
  {"name": "02-home-painted", "shows": "Back solid, Front linear gradient, Right Arm front dots, all with the default colors; Home tab.", "alsoTab41": "layout",
   "ops": [
    {"op": "paint", "region": "torso_back"},
    {"op": "fillType", "type": "linear"}, {"op": "undo"},
    {"op": "paint", "region": "torso_front"},
    {"op": "fillType", "type": "dots"}, {"op": "undo"},
    {"op": "paint", "region": "rarm_front"}
  ]},
  {"name": "03-layer-selected", "shows": "drip_red_wide_v1 added through Add image (selected, Select tool), rotation 30 through the slider, Image adjustments expanded, the left panel scrolled to its end.",
   "ops": [
    {"op": "addImage", "image": "packs/drips/drip_red_wide_v1.png"},
    {"op": "rotation", "value": 30},
    {"op": "expandAdjustments"},
    {"op": "scrollPanel"}
  ]},
  {"name": "04-draw-tools", "shows": "The Draw (pen) tool active; the Tools and Brush groups at their defaults.", "ops": [{"op": "tool", "tool": "pen"}]},
  {"name": "05-zoom-200", "shows": "State 03 at 200% zoom.",
   "ops": [
    {"op": "addImage", "image": "packs/drips/drip_red_wide_v1.png"},
    {"op": "rotation", "value": 30},
    {"op": "expandAdjustments"},
    {"op": "scrollPanel"},
    {"op": "zoom", "value": 2}
  ]},
  {"name": "06-pants", "shows": "Pants mode with Waist Front painted solid.", "ops": [{"op": "mode"}, {"op": "paint", "region": "waist_front"}]},
  {"name": "07-narrow-700", "shows": "Supplementary: the launch state with the client area 700 DIPs wide (ribbon-overflow-hscroll).", "contentWidth": 700, "ops": []}
]
```

Each op names the 4.1 control it drives (look-ref.js's header) and the UIA pattern the v5 driver uses (`Program.cs`, `Run`). State 02's sequence works around `applyFillEdit` (editor.js:1581-1598, kept in v5 by `FillStyleViewModel`): changing the fill type retargets every region painted with the current fill, so the run changes the type, undoes the retarget (Undo keeps the current fill, editor.js:1422-1468) and then paints. Every color is the default (`currentFill`, editor.js:1552; Brush, editor.html:56).

Create `tools/parity-ref/look-ref.js`:

```js
// Electron main. Captures the WHOLE 4.1 window content (editor.html with styles.css, the ribbon, the
// panels, the canvas and the footer) for the M3b look sign-off, so v5's shell can be compared with 4.1
// side by side in matched states. It is a reference capture for a person, not a golden: nothing under
// tests/goldens is written, and no test reads its output.
//
// Each state (look-states.json) is a list of semantic ops, which this script maps onto 4.1's own
// controls (the v5 UIA driver maps the same ops onto v5's):
//   paint {region}        click the region tile on the Layout tab (editor.js:1657-1675; no layer is selected)
//   fillType {type}       click the fill type button on the Layout tab (editor.js:1600-1607)
//   undo                  click #btnUndo (editor.js:1477-1481)
//   mode                  click #btnModeToggle (editor.js:1719-1725)
//   addImage {image}      hand the file to #imageLoadInput, the Add image path (editor.js:2692-2707)
//   rotation {value}      set #ctrlRot and fire input (editor.js:833-837)
//   expandAdjustments     click #adjToggle (editor.js:862-872)
//   tool {tool}           click the tool button (editor.js:464-490); pen is #btnDrawPen
//   zoom {value}          click #zoomIn or #zoomOut until zoom equals value (editor.js:1490-1491)
//   scrollPanel           scroll the left panel to its end, so the Selected layer section shows (styles.css:246-253)
// app-harness.js is NOT injected: it pins #mainCanvas to the viewport origin for integer mouse
// coordinates, which would move the canvas in a full-window capture. These ops need no mouse events.
//
// The window is hidden, GPU raster is off, the device scale is 1, Google Fonts requests are answered
// from assets/fonts (fonts.js) and any other network request fails the run. The content size is the v5
// client size the UIA driver measured, so both captures have the same pixel size:
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/look-ref.js \
//     --states tools/parity-ref/look-states.json --content 1184x761 --out .superpowers/m3b-look/ref [--only a,b]
// Output: <state>-41.png (plus <state>-<tab>-41.png for alsoTab41), metrics.json and index.json.
'use strict';
const { app, BrowserWindow, protocol } = require('electron');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const fonts = require('./fonts.js');

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
app.commandLine.appendSwitch('force-device-scale-factor', '1');

const ROOT = path.resolve(__dirname, '..', '..');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const blob = (rel) => execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();

const OPS = new Set(['paint', 'fillType', 'undo', 'mode', 'addImage', 'rotation', 'expandAdjustments', 'tool', 'zoom', 'scrollPanel']);
const TOOLS = { select: 'btnSelect', pen: 'btnDrawPen', line: 'btnDrawLine', rect: 'btnDrawRect', ellipse: 'btnDrawCircle', eraser: 'btnEraser' };
const FILL_TYPES = ['solid', 'linear', 'radial', 'stripes', 'checker', 'dots', 'camo']; // editor.html:148-154
const ZOOMS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5, 2.75, 3];
const TABS = ['home', 'layout', 'details', 'text']; // editor.html:22-25

// The elements whose client rectangles both sides record (CSS px here, DIPs on the v5 side). The v5
// driver's METRICS table names the same keys.
const METRICS = {
  headerTitle: 'header h1',
  homeRibbonPane: '.ribbon-pane[data-tab="home"]',
  modeToggle: '#btnModeToggle',
  downloadTemplate: '#btnExport',
  leftPanel: '.app > .panel',
  canvasArea: '.canvas-area',
  zoomIn: '#zoomIn',
  footer: 'footer',
};

function validate(s) {
  if (!/^\d\d-[a-z0-9-]+$/.test(s.name || '')) throw new Error(`bad state name: ${s.name}`);
  if (typeof s.shows !== 'string' || !s.shows) throw new Error(`${s.name}: "shows" describes the state for the page`);
  if (s.alsoTab41 !== undefined && !TABS.includes(s.alsoTab41)) throw new Error(`${s.name}: alsoTab41 ${s.alsoTab41}`);
  if (s.contentWidth !== undefined && !(Number.isInteger(s.contentWidth) && s.contentWidth >= 640)) throw new Error(`${s.name}: contentWidth`);
  for (const op of s.ops) {
    if (!OPS.has(op.op)) throw new Error(`${s.name}: unknown op ${op.op}`);
    if (op.op === 'fillType' && !FILL_TYPES.includes(op.type)) throw new Error(`${s.name}: fill type ${op.type}`);
    if (op.op === 'paint' && !/^(torso|waist|rarm|larm|rleg|lleg)_(up|down|front|back|left|right)$/.test(op.region)) throw new Error(`${s.name}: region ${op.region}`);
    if (op.op === 'tool' && !TOOLS[op.tool]) throw new Error(`${s.name}: tool ${op.tool}`);
    if (op.op === 'zoom' && !ZOOMS.includes(op.value)) throw new Error(`${s.name}: zoom ${op.value}`);
    if (op.op === 'rotation' && !(Number.isInteger(op.value) && op.value >= -180 && op.value <= 180)) throw new Error(`${s.name}: rotation ${op.value}`);
    if (op.op === 'addImage' && !fs.existsSync(path.resolve(ROOT, op.image))) throw new Error(`${s.name}: image not found ${op.image}`);
  }
  return s;
}

function loadStates(file) {
  let states = JSON.parse(fs.readFileSync(path.resolve(file), 'utf8')).map(validate);
  const only = arg('--only');
  if (only) {
    const want = only.split(',');
    for (const n of want) if (!states.some((s) => s.name === n)) throw new Error(`--only names an unknown state: ${n}`);
    states = states.filter((s) => want.includes(s.name));
  }
  if (new Set(states.map((s) => s.name)).size !== states.length) throw new Error('duplicate state names');
  return states;
}

// Runs in the page. Every op goes through a real control and waits for 4.1 to settle.
function lookPage() {
  const alerts = [];
  window.alert = (m) => { alerts.push(String(m)); };
  window.confirm = () => true;
  const byId = (id) => {
    const el = document.getElementById(id);
    if (!el) throw new Error(`#${id} not found in editor.html`);
    return el;
  };
  const one = (sel) => {
    const el = document.querySelector(sel);
    if (!el) throw new Error(`${sel} not found`);
    return el;
  };
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  async function waitFor(done, what, ms = 15000) {
    const start = performance.now();
    while (!done()) {
      if (alerts.length) throw new Error(`4.1 raised an alert while waiting for ${what}: ${alerts.join(' | ')}`);
      if (performance.now() - start > ms) throw new Error(`timed out waiting for ${what}`);
      await sleep(10);
    }
  }
  const frames = () => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
  const tab = (name) => one(`.ribbon-tab[data-tab="${name}"]`).click(); // editor.js:4-13

  async function run(op, files) {
    switch (op.op) {
      case 'paint': {
        if (selectedLayerId !== null) throw new Error('paint needs no selection: a tile click with a layer selected fits the layer (editor.js:1655-1666)');
        tab('layout');
        const before = JSON.stringify(regionColors);
        one(`.region-btn[data-r="${op.region}"]`).click();
        if (JSON.stringify(regionColors) === before && !regionColors[op.region]) throw new Error(`${op.region} did not paint`);
        break;
      }
      case 'fillType':
        tab('layout');
        one(`.fill-type-btn[data-fill="${op.type}"]`).click();
        if (currentFill.type !== op.type) throw new Error(`fill type is ${currentFill.type}`);
        break;
      case 'undo': {
        const u = byId('btnUndo');
        if (u.disabled) throw new Error('Undo is disabled');
        u.click();
        break;
      }
      case 'mode': {
        const was = currentMode;
        byId('btnModeToggle').click();
        if (currentMode === was) throw new Error('mode did not switch');
        break;
      }
      case 'addImage': {
        const f = files[op.image];
        const input = byId('imageLoadInput');
        const dt = new DataTransfer();
        dt.items.add(new File([Uint8Array.from(atob(f.b64), (c) => c.charCodeAt(0))], f.name, { type: 'image/png' }));
        input.files = dt.files;
        const n = layers.length;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        await waitFor(() => layers.length === n + 1, `the ${f.name} layer`);
        if (currentTool !== 'select' || selectedLayerId !== layers[layers.length - 1].id) throw new Error('addLayer did not select the new layer');
        break;
      }
      case 'rotation': {
        const el = byId('ctrlRot');
        el.value = String(op.value);
        if (el.value !== String(op.value)) throw new Error(`#ctrlRot refused ${op.value}`);
        el.dispatchEvent(new Event('input', { bubbles: true }));
        if (getSelected().rotation !== op.value) throw new Error('rotation did not take');
        break;
      }
      case 'expandAdjustments':
        byId('adjToggle').click();
        if (byId('adjPanel').style.display !== 'block') throw new Error('Image adjustments did not expand');
        break;
      case 'tool': {
        const ids = { select: 'btnSelect', pen: 'btnDrawPen', line: 'btnDrawLine', rect: 'btnDrawRect', ellipse: 'btnDrawCircle', eraser: 'btnEraser' };
        byId(ids[op.tool]).click();
        const want = op.tool === 'ellipse' ? 'circle' : op.tool;
        if (currentTool !== want) throw new Error(`tool is ${currentTool}`);
        break;
      }
      case 'zoom': {
        const steps = Math.round((op.value - zoom) / 0.25);
        for (let i = 0; i < Math.abs(steps); i++) byId(steps > 0 ? 'zoomIn' : 'zoomOut').click();
        if (zoom !== op.value) throw new Error(`zoom is ${zoom}`);
        break;
      }
      case 'scrollPanel': {
        const p = one('.app > .panel');
        p.scrollTop = p.scrollHeight;
        if (p.scrollHeight > p.clientHeight && p.scrollTop === 0) throw new Error('the left panel did not scroll');
        break;
      }
      default: throw new Error(`unknown op ${op.op}`);
    }
    await frames();
  }

  window.__look = {
    async apply(state, files) {
      await document.fonts.ready;
      for (const [i, op] of state.ops.entries()) {
        try { await run(op, files); } catch (e) { throw new Error(`${state.name} op ${i} (${op.op}): ${e.message}`); }
      }
      if (alerts.length) throw new Error(`4.1 raised an alert: ${alerts.join(' | ')}`);
      return this.show('home');
    },
    async show(name) {
      tab(name);
      await document.fonts.ready;
      await frames();
      return { dpr: window.devicePixelRatio, w: document.documentElement.clientWidth, h: window.innerHeight };
    },
    metrics(map) {
      const out = {};
      for (const [k, sel] of Object.entries(map)) {
        const el = document.querySelector(sel);
        if (!el) throw new Error(`metric ${k}: ${sel} not found`);
        const r = el.getBoundingClientRect();
        out[k] = { x: r.left, y: r.top, w: r.width, h: r.height };
      }
      return out;
    },
  };
}

async function callPage(win, expr) {
  const r = await win.webContents.executeJavaScript(
    `Promise.resolve(${expr}).then((value) => ({ ok: true, value }), (e) => ({ ok: false, error: String((e && e.message) || e) }))`);
  if (!r.ok) throw new Error(r.error);
  return r.value;
}

async function capture(win, file, size) {
  const img = await win.webContents.capturePage(undefined, { stayHidden: true });
  const got = img.getSize();
  if (got.width !== size.w || got.height !== size.h) throw new Error(`${path.basename(file)}: captured ${got.width}x${got.height}, expected ${size.w}x${size.h}`);
  const png = img.toPNG();
  fs.writeFileSync(file, png);
  return { file: path.basename(file), sha256: sha256(png), width: got.width, height: got.height };
}

app.whenReady().then(async () => {
  const violations = [];
  try {
    for (const scheme of ['https', 'http']) {
      protocol.handle(scheme, (request) => {
        try {
          const r = fonts.respond(request.url);
          if (r) return new Response(r.body, { status: 200, headers: r.headers });
        } catch (e) {
          violations.push(e.message);
          return new Response(e.message, { status: 500 });
        }
        violations.push(`blocked network request ${request.url}`);
        return new Response('blocked by tools/parity-ref', { status: 404 });
      });
    }
    const statesFile = arg('--states');
    const out = arg('--out');
    const content = /^(\d+)x(\d+)$/.exec(arg('--content') || '');
    if (!statesFile || !out || !content) throw new Error('usage: --states <file> --content <W>x<H> --out <dir> [--only a,b]');
    const base = { w: +content[1], h: +content[2] };
    if (base.w < 640 || base.h < 400) throw new Error(`--content ${base.w}x${base.h} is not a window client size`);
    const states = loadStates(statesFile);
    const outDir = path.resolve(out);
    fs.mkdirSync(outDir, { recursive: true });

    const win = new BrowserWindow({
      show: false, useContentSize: true, width: base.w, height: base.h, paintWhenInitiallyHidden: true,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false },
    });
    win.setMenuBarVisibility(false); // main.js:23
    const index = {
      tool: 'tools/parity-ref/look-ref.js',
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), stylesCssBlob: blob('styles.css'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
      content: `${base.w}x${base.h}`, states: [],
    };
    const metrics = {};
    for (const state of states) {
      const size = { w: state.contentWidth ?? base.w, h: base.h };
      win.setContentSize(size.w, size.h);
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(`(${lookPage.toString()})(); 0;`);
      const files = {};
      for (const op of state.ops.filter((o) => o.op === 'addImage')) {
        files[op.image] = { name: path.basename(op.image), b64: fs.readFileSync(path.resolve(ROOT, op.image)).toString('base64') };
      }
      const page = await callPage(win, `__look.apply(${JSON.stringify(state)}, ${JSON.stringify(files)})`);
      if (page.dpr !== 1) throw new Error(`${state.name}: devicePixelRatio ${page.dpr}, expected 1`);
      if (page.w > size.w || page.h !== size.h) throw new Error(`${state.name}: viewport ${page.w}x${page.h}, expected ${size.w}x${size.h}`);
      metrics[state.name] = await callPage(win, `__look.metrics(${JSON.stringify(METRICS)})`);
      const shots = [await capture(win, path.join(outDir, `${state.name}-41.png`), size)];
      if (state.alsoTab41) {
        await callPage(win, `__look.show(${JSON.stringify(state.alsoTab41)})`);
        shots.push(await capture(win, path.join(outDir, `${state.name}-${state.alsoTab41}-41.png`), size));
      }
      if (violations.length) throw new Error(`${state.name}: ${violations.join('; ')}`);
      index.states.push({ name: state.name, shows: state.shows, shots });
      console.log(`captured ${state.name} ${size.w}x${size.h}${state.alsoTab41 ? ` (+${state.alsoTab41})` : ''}`);
    }
    fs.writeFileSync(path.join(outDir, 'metrics.json'), JSON.stringify(metrics, null, 2) + '\n');
    fs.writeFileSync(path.join(outDir, 'index.json'), JSON.stringify(index, null, 2) + '\n');
    console.log(`ok ${index.states.length}`);
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
```

Then add the tool to `tools/parity-ref/README.md`. Task 1 may also have added a bullet there, so the edit anchors on the `## Rules` heading, not on a line number (the fence string keeps a code fence out of this plan's own fence):

```bash
python - <<'EOF'
p = "tools/parity-ref/README.md"
s = open(p, encoding="utf-8").read()
assert "look-ref.js" not in s, "README already names look-ref.js"
bullet = ("- `look-ref.js` (M3b) loads the real `editor.html` in a hidden window at the v5 client size, drives it through its own "
          "controls (not `app-harness.js`, which pins the canvas), and captures the whole window with `webContents.capturePage` "
          "for the M3b look sign-off. It writes only under `.superpowers/m3b-look/ref`; its PNGs are a reference for a person, "
          "not goldens.\n")
anchor = "\n\n## Rules\n"
assert s.count(anchor) == 1, "README lost its Rules heading"
s = s.replace(anchor, "\n" + bullet + "\n## Rules\n", 1)
fence = "`" * 3
s = s.rstrip("\n") + "\n\n## Look capture (M3b)\n\n" + (
    "`look-states.json` lists the sign-off states as semantic ops (`paint`, `fillType`, `undo`, `mode`, `addImage`, `rotation`, "
    "`expandAdjustments`, `tool`, `zoom`, `scrollPanel`). `look-ref.js` maps each op onto 4.1's own controls; the scratch v5 driver "
    "maps the same ops onto UI Automation patterns. Run the v5 driver first: it prints the client size this script takes.\n\n"
    + fence + "bash\n"
    "env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/look-ref.js \\\n"
    "  --states tools/parity-ref/look-states.json --content <W>x<H> --out .superpowers/m3b-look/ref [--only a,b]\n"
    + fence + "\n\n"
    "Output: `<state>-41.png`, `<state>-<tab>-41.png` for a state's `alsoTab41`, `metrics.json` (client rectangles of the elements "
    "the v5 driver also measures) and `index.json` (Electron and Chrome versions, the blob hashes of `editor.js`, `editor.html` and "
    "`styles.css`, and the font manifest hash). The window is hidden, GPU raster is off, the device scale is 1, fonts come from "
    "`assets/fonts`, and any other network request fails the run.\n")
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("README updated")
EOF
grep -n "look-ref.js" tools/parity-ref/README.md | cut -c1-60
```

Expected: `README updated`, then three lines naming `look-ref.js` (the bullet, the section's prose and the command).

- [ ] **Step 3: Check the capture script offscreen**

The full capture is the controller's (Step 9), but the script must work before it is committed. It opens a hidden window and sends no desktop input, so the implementer runs it into a scratch folder at the client size measured in the M3a smoke run:

```bash
node --check tools/parity-ref/look-ref.js && echo syntax-ok
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/look-ref.js \
  --states tools/parity-ref/look-states.json --content 1184x761 --out .superpowers/m3b-measure/look-ref-check 2>&1 | tail -9
python -c "import json; m=json.load(open('.superpowers/m3b-measure/look-ref-check/metrics.json')); print(sorted(m), sorted(m['01-launch']))"
```

Expected (measured 2026-10-15 on a copy of `4e0be14`; about 40 s):

- `syntax-ok`.
- `captured 01-launch 1184x761`, `captured 02-home-painted 1184x761 (+layout)`, `captured 03-layer-selected 1184x761`, `captured 04-draw-tools 1184x761`, `captured 05-zoom-200 1184x761`, `captured 06-pants 1184x761`, `captured 07-narrow-700 700x761`, `ok 7`.
- `['01-launch', '02-home-painted', '03-layer-selected', '04-draw-tools', '05-zoom-200', '06-pants', '07-narrow-700'] ['canvasArea', 'downloadTemplate', 'headerTitle', 'homeRibbonPane', 'leftPanel', 'modeToggle', 'zoomIn']`.

Open `03-layer-selected-41.png`: the drip sits on the front torso at 30° with its dashed box, handles, knob and `30.0°` label, and the left panel shows Rotation through Reset adjustments. A failure names the state and op; fix the script, never the states' semantics.

- [ ] **Step 4: Commit the capture tooling**

Add your own model's `Co-Authored-By` trailer as the last line (Global Constraints) before running it.

```bash
git add tools/parity-ref/look-states.json tools/parity-ref/look-ref.js tools/parity-ref/README.md
git commit -F - <<'EOF'
feat(tools): capture 4.1's whole window in matched look states for the M3b sign-off

look-ref.js loads the real editor.html in a hidden window at the v5 client
size and drives it through its own controls (region tiles, fill types,
Undo, the mode button, Add image's file input, the rotation slider, the
adjustments toggle, the tool buttons, the zoom pill), then captures the
window with webContents.capturePage. It does not inject app-harness.js,
which pins the canvas to the viewport origin. look-states.json holds the
six sign-off states plus a 700 DIP narrow window. Output goes to
.superpowers/m3b-look/ref with element metrics and provenance; nothing
under tests/goldens is written.
EOF
git log --oneline -1
```

Expected: one commit with the subject `feat(tools): capture 4.1's whole window in matched look states for the M3b sign-off`.

- [ ] **Step 5: Write the M3b ledger scenarios**

Each fact replays one or more ledger rows' parityCheck end to end through the real composition (`TestShell.Main`, so `Composition.Build`'s wiring is under test) on the StaThread. They add no production code. A failure is a defect in the owning task, never a reason to edit the scenario.

Save the facts and helpers as `.superpowers/m3b/scenarios-m3b.cs.txt` (git-ignored scratch); the script after it inserts the block verbatim before the existing `Solid` helper:

```csharp
    // ─── M3b close-out scenarios (Task 5). Each drives the real composition (TestShell.Main) on the
    // StaThread, the way the canvas host, the panels and the ribbon do, through their public surface.

    [Fact]
    public void Select_resize_rotate_and_move_are_one_step_each_and_a_click_records_nothing() => StaThread.Run(() =>
    {
        // select-resize-drag, select-resize-handles, select-rotate-drag, select-rotate-drag-2, select-move,
        // select-move-hit-and-drag, select-deselect-click, canvas-select-gestures, canvas-coord-map
        // (editor.js:1023-1039, :1058-1117, :1169-1192)
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");          // center (250, 130)
        var canvas = main.Canvas.Interaction;
        Assert.Equal(Tool.Select, main.Home.Tools.Current);

        Assert.True(canvas.PointerDown(new Point(300, 160), 1));          // the bottom-right corner
        Assert.Equal(CanvasGesture.Resize, canvas.Gesture);
        canvas.PointerMove(new Point(312.5, 167.5), 1);
        canvas.PointerUp(new Point(325, 175), 1);                          // 1.5 times the start distance
        Assert.Equal(1.5, LayerOf(s, id).Scale, 12);
        Assert.Equal(175, LayerOf(s, id).X, 9);                            // the center stays put
        Assert.Equal(85, LayerOf(s, id).Y, 9);
        Assert.Equal(2, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(250, 55), 1));           // the knob: h/2 + 30 px above the center
        Assert.Equal(CanvasGesture.Rotate, canvas.Gesture);
        canvas.PointerMove(At(250, 130, 44, 80), 1);
        Assert.Equal(45, LayerOf(s, id).Rotation, 9);                      // within 3 degrees of 45: snapped
        canvas.PointerMove(At(250, 130, 50, 80), 1);
        canvas.PointerUp(At(250, 130, 50, 80), 1);
        Assert.Equal(50, LayerOf(s, id).Rotation, 9);
        Assert.Equal(3, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(250, 130), 1));          // the body
        Assert.Equal(CanvasGesture.Move, canvas.Gesture);
        canvas.PointerMove(new Point(265, 120), 1);
        canvas.PointerUp(new Point(280, 110), 1);
        Assert.Equal((205.0, 65.0), (LayerOf(s, id).X, LayerOf(s, id).Y));
        Assert.Equal(4, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(280, 110), 1));          // a click: press and release in place
        canvas.PointerUp(new Point(280, 110), 1);
        Assert.Equal(4, s.UndoCount);

        var from = CanvasCoordinates.ToTemplate(new Point(560, 220), 2);  // a 50x30 px drag in the frame at 200%
        var to = CanvasCoordinates.ToTemplate(new Point(610, 250), 2);
        Assert.True(canvas.PointerDown(from, 2));
        canvas.PointerUp(to, 2);
        Assert.Equal((230.0, 80.0), (LayerOf(s, id).X, LayerOf(s, id).Y)); // is 25x15 template px
        Assert.Equal(5, s.UndoCount);

        Assert.False(canvas.PointerDown(new Point(10, 540), 1));          // empty canvas deselects
        Assert.Null(s.SelectedLayerId);
        Assert.False(main.LayerProperties.HasSelection);
        Assert.Equal(5, s.UndoCount);

        for (int i = 0; i < 4; i++) Assert.True(s.Undo());
        var back = LayerOf(s, id);
        Assert.Equal((200.0, 100.0, 1.0, 0.0), (back.X, back.Y, back.Scale, back.Rotation));
        Assert.Equal(id, s.SelectedLayerId);                               // selection survives undo
    });

    [Fact]
    public void A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step() => StaThread.Run(() =>
    {
        // wheel-scale-layer, wheel-scale-undo-burst, props-scale-clamp-inconsistency, props-scale
        // (editor.js:1305-1313; spec §5; interfaces R6, R7)
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");
        var canvas = main.Canvas.Interaction;

        for (int i = 0; i < 3; i++) canvas.Wheel(+1);
        canvas.EndWheelBurst();
        var l = LayerOf(s, id);
        Assert.Equal(1.05 * 1.05 * 1.05, l.Scale, 12);
        Assert.Equal(250, l.X + 50 * l.Scale, 9);                          // centered, not top-left (4.1)
        Assert.Equal(130, l.Y + 30 * l.Scale, 9);
        Assert.Equal(2, s.UndoCount);

        for (int i = 0; i < 200; i++) canvas.Wheel(+1);
        canvas.EndWheelBurst();
        Assert.Equal(5, LayerOf(s, id).Scale, 12);                         // one clamp for every path: 1-500%
        Assert.Equal(500, main.LayerProperties.ScalePercent, 9);
        Assert.Equal("500%", main.LayerProperties.ScaleLabel);
        Assert.Equal(3, s.UndoCount);

        Assert.True(s.Undo());
        Assert.Equal(1.05 * 1.05 * 1.05, LayerOf(s, id).Scale, 12);
        Assert.True(s.Undo());
        Assert.Equal(1, LayerOf(s, id).Scale, 12);

        for (int i = 0; i < 10; i++) canvas.Wheel(-1);
        for (int i = 0; i < 10; i++) canvas.Wheel(+1);
        canvas.EndWheelBurst();
        Assert.Equal(1, LayerOf(s, id).Scale, 12);                         // symmetric steps; 4.1 drifts to 0.9794
        Assert.InRange(s.UndoCount, 1, 2);   // the burst is at most one step: none when the center recompute lands on the start exactly

        s.Select(null);
        canvas.Wheel(+1);
        canvas.EndWheelBurst();
        Assert.Equal(1, LayerOf(s, id).Scale, 12);                         // nothing selected: nothing scales
    });

    [Fact]
    public void The_pick_is_the_box_not_the_pixels_and_a_hidden_selected_layer_has_no_handles() => StaThread.Run(() =>
    {
        // select-hit-test, select-hit-test-rect-not-alpha, select-hidden-layer-still-handled,
        // select-rotate-handle-hit, select-resize-handle-hit, render-selection-box
        // (editor.js:1043-1053, :1064-1117, :361-363; spec §5; interfaces R2)
        using var small = Solid(20, 20, Red);
        using var clear = Solid(200, 200, SKColors.Transparent);
        var (s, main) = Shell();
        AddLayer(s, small, 240, 120, "small");
        int over = AddLayer(s, clear, 150, 30, "clear");                  // a transparent box over the small layer
        var canvas = main.Canvas.Interaction;

        s.Select(null);
        Assert.True(canvas.PointerDown(new Point(250, 130), 1));          // on the small layer's red pixels
        canvas.PointerUp(new Point(250, 130), 1);
        Assert.Equal(over, s.SelectedLayerId);                              // the topmost box wins (kept)

        Assert.True(canvas.PointerDown(new Point(150, 30), 1));           // its top-left corner
        Assert.Equal(CanvasGesture.Resize, canvas.Gesture);
        canvas.PointerUp(new Point(150, 30), 1);
        using (var plain = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
        using (var selected = Renderer.RenderViewImage(s.Document, new ViewOptions(1) { SelectedLayerId = over }))
            Assert.NotEqual(Png(plain), Png(selected));                     // a visible selection draws its overlay

        Assert.True(s.Apply(d => LayerOps.ToggleVisibility(d, over)));
        Assert.Equal(over, s.SelectedLayerId);
        using (var plain = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
        using (var selected = Renderer.RenderViewImage(s.Document, new ViewOptions(1) { SelectedLayerId = over }))
            Assert.Equal(Png(plain), Png(selected));                        // hidden: no box, handles, knob or label

        Assert.False(canvas.PointerDown(new Point(250, 0), 1));           // where its knob would be: falls through
        Assert.Null(s.SelectedLayerId);                                     // to the pick, which skips hidden layers
        s.Select(over);
        Assert.False(canvas.PointerDown(new Point(150, 30), 1));          // its corner is dead too
        Assert.Null(s.SelectedLayerId);
        Assert.Equal(3, s.UndoCount);                                       // two adds and the hide; no gesture step
    });

    [Fact]
    public void Draw_tools_commit_one_named_layer_and_keep_the_tool_and_a_click_draws_only_the_pen_dot() => StaThread.Run(() =>
    {
        // draw-commit-layer, draw-empty-no-layer, draw-pen-single-click, draw-begin, draw-tool-start,
        // layers-add-drawing, canvas-draw-tools, home-brush-group, layers-default-name-offbyone
        // (editor.js:528-540, :1120-1146, :1204-1293)
        var (s, main) = Shell();
        var tools = main.Home.Tools;
        var canvas = main.Canvas.Interaction;
        var cyan = new SKColor(0x17, 0xd4, 0xfa);                           // the brush default (editor.html:56)

        tools.ToggleCommand.Execute(Tool.Rect);
        main.Home.Brush.Fill = true;
        Assert.True(canvas.PointerDown(new Point(100, 100), 1));
        Assert.Equal(0, s.UndoCount);                                       // no undo step at the press (4.1 pushes one)
        canvas.PointerMove(new Point(150, 140), 1);
        Assert.NotNull(canvas.Preview);
        canvas.PointerUp(new Point(150, 140), 1);
        var rect = Assert.Single(s.Document.Layers);
        Assert.Equal(Tools.DrawingName(ShapeTool.Rect), rect.Name);        // "Rect drawing" (editor.js:1285)
        Assert.Equal(rect.Id, s.SelectedLayerId);
        Assert.Equal(Tool.Rect, tools.Current);                             // a drawing keeps its tool
        Assert.Null(canvas.Preview);
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(cyan, PixelOf(rect.Image, (int)(125 - rect.X), (int)(120 - rect.Y)));   // filled in the brush color

        foreach (var tool in new[] { Tool.Line, Tool.Rect, Tool.Ellipse })
        {
            tools.Set(tool);
            canvas.PointerDown(new Point(300, 300), 1);
            canvas.PointerUp(new Point(300, 300), 1);
        }
        Assert.Single(s.Document.Layers);                                   // a shape click draws nothing
        Assert.Equal(1, s.UndoCount);

        tools.Set(Tool.Pen);
        Assert.True(canvas.PointerDown(new Point(400, 300), 1));
        canvas.PointerUp(new Point(400, 300), 1);
        Assert.Equal(2, s.Document.Layers.Count);                           // a pen click is a brush-size dot (approved)
        var dot = s.Document.Layers[^1];
        Assert.Equal(Tools.DrawingName(ShapeTool.Pen), dot.Name);
        Assert.Equal(cyan, PixelOf(dot.Image, (int)(400 - dot.X), (int)(300 - dot.Y)));
        Assert.Equal(2, s.UndoCount);
    });

    [Fact]
    public void The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden() => StaThread.Run(() =>
    {
        // eraser-selection-semantics, eraser-apply, eraser-click-without-move, canvas-draw-tools
        // (editor.js:1133-1144, :1265-1283; spec §5; interfaces R2, R3)
        using var a = Solid(100, 60, Red);
        using var b = Solid(100, 60, Blue);
        var (s, main) = Shell();
        int ida = AddLayer(s, a, 200, 100, "a");
        int idb = AddLayer(s, b, 200, 300, "b");
        var canvas = main.Canvas.Interaction;
        main.Home.Brush.Size = 10;
        s.Select(null);
        main.Home.Tools.Set(Tool.Eraser);
        var before = s.Document;

        Assert.False(canvas.PointerDown(new Point(210, 130), 1));
        Assert.Equal("Select a layer to erase from, then try again.", main.Status.Message);   // editor.js:1270, at the press
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Same(before, s.Document);

        s.Select(ida);
        main.Home.Tools.Set(Tool.Eraser);
        Assert.True(canvas.PointerDown(new Point(250, 130), 1));          // a click without movement erases nothing
        canvas.PointerUp(new Point(250, 130), 1);
        Assert.Same(before, s.Document);
        Assert.Equal(2, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(210, 130), 1));
        canvas.PointerMove(new Point(250, 130), 1);
        Assert.True(canvas.Preview!.IsEraser);
        Assert.Equal(0.5f, canvas.Preview!.Alpha);                           // the preview at half alpha (editor.js:327)
        canvas.PointerUp(new Point(290, 130), 1);
        Assert.Equal(3, s.UndoCount);
        Assert.Equal(0, PixelOf(LayerOf(s, ida).Image, 50, 30).Alpha);      // through the middle of layer a
        Assert.Equal(Red, PixelOf(LayerOf(s, ida).Image, 50, 5));           // outside the 10 px stroke
        Assert.Same(b, LayerOf(s, idb).Image);                              // layer b is untouched

        Assert.True(s.Apply(d => LayerOps.ToggleVisibility(d, ida)));
        var hidden = LayerOf(s, ida).Image;
        Assert.True(canvas.PointerDown(new Point(210, 110), 1));          // hidden but selected: it still erases (4.1)
        canvas.PointerUp(new Point(290, 110), 1);
        Assert.NotSame(hidden, LayerOf(s, ida).Image);
        Assert.Equal(5, s.UndoCount);
    });

    [Fact]
    public void A_tool_switch_mid_stroke_discards_it_and_a_lost_capture_commits_it() => StaThread.Run(() =>
    {
        // draw-tool-switch-midstroke (R9), draw-release-outside-canvas, select-hover-and-end (R10)
        // (editor.js:1239-1246, :1296-1302)
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");
        var tools = main.Home.Tools;
        var canvas = main.Canvas.Interaction;

        tools.Set(Tool.Pen);
        Assert.True(canvas.PointerDown(new Point(50, 400), 1));
        canvas.PointerMove(new Point(120, 450), 1);
        Assert.NotNull(canvas.Preview);
        tools.Set(Tool.Line);                                                // 4.1 orphans the preview; v5 cancels
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Null(canvas.Preview);
        Assert.Single(s.Document.Layers);
        Assert.Equal(1, s.UndoCount);

        Assert.True(canvas.PointerDown(new Point(50, 400), 1));
        canvas.PointerMove(new Point(120, 450), 1);
        canvas.CaptureLost();                                                // released outside the window, or Alt+Tab
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Equal(Tools.DrawingName(ShapeTool.Line), s.Document.Layers[^1].Name);
        Assert.Equal(2, s.UndoCount);

        tools.Set(Tool.Select);
        s.Select(id);
        Assert.True(canvas.PointerDown(new Point(250, 130), 1));
        canvas.PointerMove(new Point(270, 140), 1);
        tools.Set(Tool.Pen);                                                 // a select drag ends as if released
        Assert.Equal(CanvasGesture.None, canvas.Gesture);
        Assert.Equal((220.0, 110.0), (LayerOf(s, id).X, LayerOf(s, id).Y));
        Assert.Equal(3, s.UndoCount);
        canvas.PointerUp(new Point(300, 160), 1);                            // the stray release changes nothing
        Assert.Equal((220.0, 110.0), (LayerOf(s, id).X, LayerOf(s, id).Y));
        Assert.Equal(3, s.UndoCount);
    });

    [Fact]
    public void The_wheel_steps_the_brush_in_draw_tools_and_widens_a_live_shape() => StaThread.Run(() =>
    {
        // wheel-brush-size, wheel-sign-only-and-midstroke (R11), tools-draw-props, draw-brush-controls, home-brush-group
        // (editor.html:56-59; editor.js:90-92, :1207-1210, :1314-1319)
        var (s, main) = Shell();
        var brush = main.Home.Brush;
        var canvas = main.Canvas.Interaction;
        Assert.Equal(("#17d4fa", 4, false), (brush.Color, brush.Size, brush.Fill));

        main.Home.Tools.Set(Tool.Pen);
        for (int i = 0; i < 20; i++) canvas.Wheel(+1);
        Assert.Equal(24, brush.Size);
        for (int i = 0; i < 60; i++) canvas.Wheel(+1);
        Assert.Equal(50, brush.Size);
        for (int i = 0; i < 100; i++) canvas.Wheel(-1);
        Assert.Equal(1, brush.Size);
        Assert.Equal(0, s.UndoCount);                                       // the brush is never document state

        brush.Size = 10;
        main.Home.Tools.Set(Tool.Rect);
        Assert.True(canvas.PointerDown(new Point(100, 100), 1));
        canvas.PointerMove(new Point(200, 180), 1);
        Assert.Equal(10f, canvas.Preview!.Stroke.Width);
        canvas.Wheel(+1);
        Assert.Equal(11, brush.Size);
        Assert.Equal(11f, canvas.Preview!.Stroke.Width);                    // the live shape takes the new width
        canvas.PointerUp(new Point(200, 180), 1);
        var expected = Tools.CommitDrawing(new Stroke(ShapeTool.Rect, [new SKPoint(100, 100), new SKPoint(200, 180)], brush.SkColor, 11, false), 99, "expected")!;
        var got = Assert.Single(s.Document.Layers);
        Assert.Equal((expected.X, expected.Y, expected.Image.Width, expected.Image.Height), (got.X, got.Y, got.Image.Width, got.Image.Height));
        Assert.Equal(1, s.UndoCount);
        expected.Image.Dispose();
    });

    [Fact]
    public void Every_property_edit_is_one_step_zero_shows_as_zero_and_adjustments_stay_open() => StaThread.Run(() =>
    {
        // layers-empty-state, layers-select, props-name-xy, props-scale, props-rotation, props-opacity, props-flip,
        // props-adjust-panel, props-adjust-reset, props-falsy-zero-adjust-display, props-rotation-label-unrounded,
        // props-panel-rebuild-resets, layers-model (editor.js:747-913)
        using var art = Solid(100, 60, Slate);
        using var other = Solid(10, 10, Blue);
        var (s, main) = Shell();
        var props = main.LayerProperties;
        Assert.False(props.HasSelection);
        Assert.Equal("Select a layer to edit.", props.EmptyText);          // editor.js:750
        int idOther = AddLayer(s, other, 20, 20, "other");
        int id = AddLayer(s, art, 200, 100, "art");
        Assert.True(props.HasSelection);
        Assert.Equal(2, s.UndoCount);

        props.Name = "logo";
        Assert.Equal("logo", LayerOf(s, id).Name);
        Assert.Contains(main.Layers.Rows, r => r.Name == "logo");
        props.XText = "12.7";
        Assert.Equal(12.7, LayerOf(s, id).X);
        Assert.Equal("13", props.XText);                                    // stored 12.7, shown toFixed(0) (editor.js:756)
        props.YText = "abc";
        Assert.Equal("100", props.YText);                                   // unparsable: reverts, no step
        Assert.Equal(4, s.UndoCount);

        props.ScalePercent = 250;
        Assert.Equal((2.5, 12.7, 100.0), (LayerOf(s, id).Scale, LayerOf(s, id).X, LayerOf(s, id).Y));   // top-left anchor
        Assert.Equal("250%", props.ScaleLabel);
        props.ScalePercent = 900;
        Assert.Equal(5, LayerOf(s, id).Scale, 12);                          // one clamp, 1-500%
        props.Rotation = 30.4;
        Assert.Equal("30°", props.RotationLabel);
        props.Rotation = -0.4;
        Assert.Equal("0°", props.RotationLabel);                            // rounded, never "-0"
        props.OpacityPercent = 50;
        Assert.Equal(0.5, LayerOf(s, id).Opacity);
        Assert.Equal("50%", props.OpacityLabel);
        props.FlipHCommand.Execute(null);
        Assert.True(LayerOf(s, id).FlipH);
        Assert.Equal(10, s.UndoCount);

        props.AdjustmentsExpanded = true;
        props.Brightness = 0;
        Assert.Equal(0, LayerOf(s, id).Brightness);
        Assert.Equal("0%", props.BrightnessLabel);
        s.Select(idOther);
        s.Select(id);
        Assert.Equal(0, props.Brightness);                                  // 4.1 shows 100 here (editor.js:785)
        Assert.Equal("0%", props.BrightnessLabel);
        Assert.True(props.AdjustmentsExpanded);                             // 4.1 collapses it on every rebuild
        props.ResetAdjustmentsCommand.Execute(null);
        Assert.Equal(LayerAdjustments.Default, LayerAdjustments.From(LayerOf(s, id)));
        Assert.True(props.AdjustmentsExpanded);
        Assert.Equal(12, s.UndoCount);
        props.ResetAdjustmentsCommand.Execute(null);                        // already at the defaults: no step
        Assert.Equal(12, s.UndoCount);

        using (props.BeginSliderDrag())
        {
            foreach (double r in new[] { 10.0, 20.0, 25.0 }) props.Rotation = r;
        }
        Assert.Equal(25, LayerOf(s, id).Rotation, 9);
        Assert.Equal(13, s.UndoCount);                                      // one drag, one step

        s.Select(null);
        Assert.False(props.HasSelection);
    });

    [Fact]
    public void The_panel_follows_a_canvas_drag_live_and_refreshes_after_a_region_fit() => StaThread.Run(() =>
    {
        // props-live-refresh, props-name-xy, regions-click-fit-layer (editor.js:1169-1192, :1658-1667)
        using var art = Solid(200, 100, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 100, 100, "art");                        // center (200, 150)
        var props = main.LayerProperties;
        var canvas = main.Canvas.Interaction;

        Assert.True(canvas.PointerDown(new Point(200, 70), 1));           // the knob
        canvas.PointerMove(At(200, 150, 60, 90), 1);
        Assert.Equal(60, props.Rotation, 9);                                // live, mid-drag
        Assert.Equal("60°", props.RotationLabel);
        canvas.PointerMove(At(200, 150, 77.4, 90), 1);
        Assert.Equal("77°", props.RotationLabel);
        canvas.PointerUp(At(200, 150, 77.4, 90), 1);

        Assert.True(canvas.PointerDown(new Point(200, 150), 1));
        canvas.PointerMove(new Point(230, 170), 1);
        Assert.Equal(("130", "120"), (props.XText, props.YText));          // 4.1 leaves X/Y stale during a move
        canvas.PointerUp(new Point(230, 170), 1);
        Assert.Equal(3, s.UndoCount);

        main.Home.Regions.Body.Single(t => t.Region == "torso_front").PaintCommand.Execute(null);
        // 200x100 into torso_front (231, 74, 128x128): scale 0.544, x 240.6, y 110.8, rotation 0 (editor.js:1658-1667)
        Assert.Equal(54.4, props.ScalePercent, 9);
        Assert.Equal("54%", props.ScaleLabel);
        Assert.Equal(("241", "111"), (props.XText, props.YText));
        Assert.Equal(0, props.Rotation, 9);
        Assert.Equal(4, s.UndoCount);
    });

    [Fact]
    public void Dragging_a_row_reorders_in_one_step_a_stale_target_does_nothing_and_move_up_is_one_step() => StaThread.Run(() =>
    {
        // layers-reorder-drag, layers-reorder-drag-2, layer-reorder-dragging-class-timer (editor.js:584-666)
        using var img = Solid(16, 16, Slate);
        var (s, main) = Shell();
        int a = AddLayer(s, img, 0, 0, "a");
        int b = AddLayer(s, img, 0, 0, "b");
        int c = AddLayer(s, img, 0, 0, "c");
        int d = AddLayer(s, img, 0, 0, "d");
        var layers = main.Layers;
        string[] Names() => layers.Rows.Select(r => r.Name).ToArray();
        Assert.Equal(new[] { "d", "c", "b", "a" }, Names());
        Assert.Equal("(drag to reorder)", layers.ReorderHint);              // editor.html:317, now that reorder works

        layers.BeginRowDrag(a);
        Assert.True(layers.Rows.Single(r => r.Id == a).IsDragSource);
        layers.DragOverRow(d, above: true);
        Assert.Equal(DropIndicator.Above, layers.Rows.Single(r => r.Id == d).DropIndicator);
        Assert.True(layers.CompleteRowDrop());
        layers.EndRowDrag();
        Assert.Equal(new[] { "a", "d", "c", "b" }, Names());
        Assert.All(layers.Rows, r => Assert.False(r.IsDragSource));
        Assert.All(layers.Rows, r => Assert.Equal(DropIndicator.None, r.DropIndicator));
        Assert.Equal(5, s.UndoCount);

        layers.BeginRowDrag(b);
        layers.DragOverRow(c, above: false);
        layers.ClearDropTarget();                                           // off every row: 4.1 reuses the old target
        Assert.False(layers.CompleteRowDrop());
        layers.EndRowDrag();
        Assert.Equal(new[] { "a", "d", "c", "b" }, Names());
        Assert.Equal(5, s.UndoCount);

        s.Select(c);
        layers.MoveUpCommand.Execute(null);                                 // Alt+Up on a focused row runs it
        Assert.Equal(new[] { "a", "c", "d", "b" }, Names());
        Assert.Equal(6, s.UndoCount);
        Assert.True(s.Undo());
        Assert.True(s.Undo());
        Assert.Equal(new[] { "d", "c", "b", "a" }, Names());
    });

    [Fact]
    public void Tool_buttons_toggle_off_to_select_and_an_import_or_a_list_pick_returns_to_select() => StaThread.Run(() =>
    {
        // tools-set-tool, tools-auto-select-switch (interfaces R8; editor.js:464-490, :523, :528-540, :577)
        var (s, main) = Shell();
        var tools = main.Home.Tools;
        ToolButtonViewModel Button(Tool t) => tools.Buttons.Single(x => x.Tool == t);
        Tool[] Active() => tools.Buttons.Where(x => x.IsActive).Select(x => x.Tool).ToArray();
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser" }, tools.Buttons.Select(x => x.Label));
        Assert.Equal(new[] { Tool.Select }, Active());                      // Select at launch (editor.js:479)

        Button(Tool.Rect).Command.Execute(null);
        Assert.Equal(new[] { Tool.Rect }, Active());
        Button(Tool.Rect).Command.Execute(null);                            // the active tool again: back to Select
        Assert.Equal(new[] { Tool.Select }, Active());
        Button(Tool.Select).Command.Execute(null);                          // Select has no "off"
        Assert.Equal(Tool.Select, tools.Current);

        tools.Set(Tool.Pen);
        main.Layers.DropFiles([WritePng("art.png", 16, 16, Blue)]);
        Assert.Equal(Tool.Select, tools.Current);                           // an import returns to Select (editor.js:523)

        tools.Set(Tool.Pen);
        Assert.True(main.Canvas.Interaction.PointerDown(new Point(450, 450), 1));
        main.Canvas.Interaction.PointerUp(new Point(450, 450), 1);
        Assert.Equal(Tool.Pen, tools.Current);                              // a drawing keeps its tool (editor.js:528-540)
        Assert.Equal(2, s.Document.Layers.Count);

        main.Layers.Selected = main.Layers.Rows.Single(r => r.Name == "art");
        Assert.Equal(Tool.Select, tools.Current);                           // a pick in the list returns to Select (:577)

        tools.Set(Tool.Eraser);
        Assert.True(s.Undo());
        Assert.Equal(Tool.Eraser, tools.Current);                           // undo leaves the tool alone
    });

    [Fact]
    public void Brush_tool_and_zoom_are_never_undo_steps_and_undo_leaves_them_alone() => StaThread.Run(() =>
    {
        // undo-snapshot-contents (editor.js:1404-1415): a snapshot holds the document and the selection only
        using var art = Solid(100, 60, Slate);
        var (s, main) = Shell();
        int id = AddLayer(s, art, 200, 100, "art");
        main.Home.Brush.Size = 30;
        main.Home.Brush.Fill = true;
        main.Home.Tools.Set(Tool.Rect);
        main.Canvas.ZoomInCommand.Execute(null);
        Assert.Equal(1, s.UndoCount);

        Assert.True(s.Apply(d => LayerOps.SetOpacity(d, id, 0.5)));
        Assert.True(s.Undo());
        Assert.Equal(1, LayerOf(s, id).Opacity);
        Assert.Equal((30, true, Tool.Rect, 1.25), (main.Home.Brush.Size, main.Home.Brush.Fill, main.Home.Tools.Current, main.Canvas.Zoom));
    });

    static (EditorSession Session, MainViewModel Main) Shell()
    {
        var s = new EditorSession();
        return (s, TestShell.Main(s));
    }

    /// <summary>Adds a scale-1 layer at (x, y) and selects it, as one undo step (the way an import lands).</summary>
    static int AddLayer(EditorSession s, SKImage image, double x, double y, string name)
    {
        int id = s.TakeId();
        var layer = new Layer { Id = id, Image = image, Name = name, X = x, Y = y };
        Assert.True(s.ApplyWithSelection(snap => new EditorSnapshot(snap.Document with { Layers = snap.Document.Layers.Add(layer) }, id)));
        return id;
    }

    static Layer LayerOf(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    /// <summary>The point <paramref name="r"/> template px from the center at a 4.1 pointer angle (atan2(dx, -dy), editor.js:1176).</summary>
    static Point At(double cx, double cy, double degrees, double r) =>
        new(cx + r * Math.Sin(degrees * Math.PI / 180), cy - r * Math.Cos(degrees * Math.PI / 180));

    static byte[] Png(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

```

Then add three `using` lines to `src/RBX15.App.Tests/LedgerScenarioTests.cs` and insert the block:

```bash
python - <<'EOF'
p = "src/RBX15.App.Tests/LedgerScenarioTests.cs"
s = open(p, encoding="utf-8").read()
assert "using RBX15.App.Canvas;" not in s, "the M3b scenarios are already in"
for old, new in [("using RBX15.App.Services;\n", "using RBX15.App.Canvas;\nusing RBX15.App.Controls;\nusing RBX15.App.Services;\n"),
                 ("using Xunit;\n", "using Xunit;\nusing Point = System.Windows.Point;\n")]:
    assert s.count(old) == 1, old
    s = s.replace(old, new, 1)
anchor = "    static SKImage Solid(int width, int height, SKColor color)\n"
assert s.count(anchor) == 1, "the Solid helper moved"
block = open(".superpowers/m3b/scenarios-m3b.cs.txt", encoding="utf-8").read()
s = s.replace(anchor, block + anchor, 1)
open(p, "w", encoding="utf-8").write(s)
print("inserted", block.count("    [Fact]"), "facts")
EOF
```


Expected from the script: `inserted 12 facts`. The pixel and geometry values come from the rows and the template:

- A 100x60 layer at (200, 100) has its center at (250, 130) (`getLayerCenter`, editor.js:1023-1027); its bottom-right corner is (300, 160); at scale 1.5 the knob is `h/2 + 30/zoom` above the center, (250, 55) (`getRotHandlePos`, editor.js:1029-1039). A drag to (325, 175) is 1.5 times the start distance, so the scale is 1.5 and the corner moves to (175, 85) with the center fixed (editor.js:1183-1190).
- `At` places the pointer at a 4.1 pointer angle, `atan2(dx, -dy)` (editor.js:1176): 44° is within 3 of 45 and snaps; 50° does not (editor.js:1179-1182).
- `torso_front` is (231, 74, 128x128) (Template.cs); the fit of a 200x100 layer is `min(128/200, 128/100) x 0.85 = 0.544`, at (240.6, 110.8) (editor.js:1658-1667; ledger `regions-click-fit-layer`).
- The brush default is `#17d4fa`, size 4, Fill off (editor.html:56-59). The status text is editor.js:1270 verbatim; the empty-panel text editor.js:750.
- The eraser stroke runs from (210, 130) to (290, 130) at size 10, so layer pixel (50, 30) is inside it and (50, 5) is not.

- [ ] **Step 6: Run the scenarios**

These facts characterize code Tasks 1-4 committed, so they pass on the first run. Run them alone, three times, because they build view-models on the STA thread and write temp files:

```bash
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
for i in 1 2 3; do dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~LedgerScenarioTests" 2>&1 | grep -E "(Passed|Failed)!"; done
dotnet test src/RBX15.slnx -c Release --no-build 2>&1 | grep -E "(Passed|Failed)!"
```

Expected:

- `0 Warning(s)` and `0 Error(s)` (measured on compile stand-ins; the xUnit analyzers run under `-warnaserror`, which is why no fact compares a count to 0 or 1 with `Assert.Equal`).
- Three lines `Passed!  - Failed:     0, Passed:    26, Skipped:     0, Total:    26` for `RBX15.App.Tests.dll` (14 M3a and 12 M3b).
- The full run: Engine `877`, Parity `35`, App `622`, each `Failed: 0`, `Skipped: 0`.

If a fact fails, do not edit it and do not change production code in this task. Report the fact and its owner:

| Fact | Owner |
| --- | --- |
| `Select_resize_rotate_and_move_*`, `A_wheel_burst_*`, `The_pick_is_the_box_*` | Task 2 (`CanvasInteraction`), with Task 1 when a value is off (`SelectionGeometry`, `LayerOps`, the overlay) |
| `Draw_tools_commit_*`, `The_eraser_refuses_*`, `A_tool_switch_mid_stroke_*`, `The_wheel_steps_the_brush_*`, `Tool_buttons_toggle_off_*` | Task 2, with Task 1 for `Tools`, `Eraser.ApplyTo` and `LivePreview` |
| `Every_property_edit_*`, `The_panel_follows_*` | Task 3 (`LayerPropertiesViewModel`), with Task 2 for the live drag |
| `Dragging_a_row_*` | Task 4 (`LayersViewModel`), with Task 1 for `LayerOps.DropIndices` and `Reorder` |
| `Brush_tool_and_zoom_*` | Task 2 (brush, tools) and M3a (`EditorSession`) |

- [ ] **Step 7: Commit the scenarios**

Add your own model's `Co-Authored-By` trailer as the last line before running it.

```bash
git add src/RBX15.App.Tests/LedgerScenarioTests.cs
git commit -F - <<'EOF'
test(app): replay the M3b ledger scenarios across the canvas, tools, panels and layers

Twelve facts through the real composition: select resize, rotate with the
45-degree snap and move as one undo step each, a click as none, and a
200% drag through CanvasCoordinates; a wheel burst centered, symmetric,
clamped at 500% and one step; the pick by box not alpha, and no handles
on a hidden selected layer; draw tools naming and selecting one layer,
keeping the tool, a shape click drawing nothing and a pen click a dot;
the eraser refusing at the press, erasing only the selected layer even
hidden, a click erasing nothing; a tool switch discarding a stroke and a
lost capture committing it; the wheel stepping the brush and widening a
live shape; every property edit as one step, 0 shown as 0 and the
adjustments staying open; the panel following a drag and a region fit;
row drag reorder, the stale target and Move up; tool toggle-off and the
auto-revert to Select; brush, tool and zoom outside undo.
EOF
git log --oneline -1
```

Expected: one commit with the subject `test(app): replay the M3b ledger scenarios across the canvas, tools, panels and layers`.

- [ ] **Step 8: Build the v5 look driver (scratch)**

The driver lives under `.superpowers/m3b-look/driver/uia/` (git-ignored). It is scratch for the reasons in interfaces section 4: it needs a live desktop and a PID-specific window, it backs up and restores `settings.json`, and spec §6's committed UI-automation tier is a later milestone. The implementer builds it; only the controller runs it (Step 9).

Create `.superpowers/m3b-look/driver/uia/uia.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <AssemblyName>drv</AssemblyName>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Create `.superpowers/m3b-look/driver/uia/Program.cs`:

```csharp
// M3b look sign-off driver (scratch, git-ignored; built from .superpowers/m3a-smoke/driver-themed/uia).
// It launches the v5 Release build, puts it in each look state and saves a PrintWindow capture of the
// client area, plus the client rectangles of a few named elements.
//
// SAFETY RULES (the planning contract's "no synthetic global input" ruling):
//   1. It acts only on windows and UI Automation elements whose process id is the RBX15 process it
//      launched. Every target is PID-checked before any pattern call; a mismatch aborts the run.
//   2. Its only inputs are UI Automation patterns: Invoke, SelectionItem, RangeValue, Value, Scroll,
//      ExpandCollapse, Toggle, Transform and Window. It posts no window messages at all: no mouse
//      messages, and no keys (the look states need none).
//   3. No SendInput, mouse_event, keybd_event, SendKeys, SetCursorPos, SetForegroundWindow or
//      screen-coordinate clicks. It never captures the screen: PrintWindow(PW_RENDERFULLCONTENT) on the
//      app's own main window, cropped to its client area.
//   4. The only dialog it touches is the app's own Add image file dialog (a #32770 window owned by the
//      RBX15 process), through ValuePattern on its file name box and Invoke on its Open button.
//   5. It backs up %LOCALAPPDATA%\RBX15\settings.json, deletes it before every launch so the window
//      opens at its 1200x800 default, and restores it at the end, even on failure.
//   6. It refuses to run unless the main window's monitor is at 100% scale, the scale the 4.1
//      capture uses (look-ref.js forces device scale 1).
//
//   drv look <look-states.json> <out-dir> [--only a,b]
// Output: <state>-v5.png, metrics-v5.json and RESULTS.md in <out-dir>. The first line it prints is
// "client=<W>x<H>", the --content argument for look-ref.js.
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Condition = System.Windows.Automation.Condition;

static class P
{
    const string Repo = @"C:\Users\estev\Projects\RBX15-Shirt-and-Pants";
    static readonly string Exe = Path.Combine(Repo, @"src\RBX15.App\bin\Release\net10.0-windows\RBX15.App.exe");
    static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RBX15");
    static readonly string Settings = Path.Combine(SettingsDir, "settings.json");

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint PW_RENDERFULLCONTENT = 2;

    // Region keys to the tiles' automation names (RegionTileViewModel.AutomationName).
    static readonly Dictionary<string, string> Tiles = new()
    {
        ["torso_back"] = "Torso Back", ["torso_front"] = "Torso Front", ["rarm_front"] = "Right Arm Fr (Front)",
        ["waist_front"] = "Waist Front",
    };
    // Fill type ids (lib/fills.js) to the Fill type list items (editor.html:148-154 labels).
    static readonly Dictionary<string, string> FillItems = new()
    {
        ["solid"] = "Solid", ["linear"] = "Gradient", ["radial"] = "Radial", ["stripes"] = "Stripes",
        ["checker"] = "Checker", ["dots"] = "Dots", ["camo"] = "Camo",
    };
    // 4.1 tool ids to the tool buttons' automation names (their labels, editor.html:43-48).
    static readonly Dictionary<string, string> ToolButtons = new()
    {
        ["select"] = "Select", ["pen"] = "Draw", ["line"] = "Line", ["rect"] = "Rect", ["ellipse"] = "Ellipse", ["eraser"] = "Eraser",
    };

    static Process App;
    static int Pid;

    static int Main(string[] a)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4)); // per-monitor v2, so rectangles are device pixels
        Console.OutputEncoding = Encoding.UTF8;
        if (a.Length < 3 || a[0] != "look") { Console.WriteLine("usage: drv look <look-states.json> <out-dir> [--only a,b]"); return 2; }
        string backup = Settings + ".m3b-look-backup";
        bool hadSettings = File.Exists(Settings);
        if (hadSettings) File.Copy(Settings, backup, overwrite: true);
        try { return Look(a[1], a[2], a.Length > 4 && a[3] == "--only" ? a[4].Split(',') : null); }
        catch (Exception e) { Console.WriteLine("ERROR " + e.GetType().Name + ": " + e.Message); return 1; }
        finally
        {
            StopApp();
            if (hadSettings) { File.Copy(backup, Settings, overwrite: true); File.Delete(backup); }
            else if (File.Exists(Settings)) File.Delete(Settings);
            Console.WriteLine(hadSettings ? "settings.json restored" : "settings.json removed (there was none before)");
        }
    }

    static int Look(string statesFile, string outDir, string[] only)
    {
        if (!File.Exists(Exe)) throw new Exception("build first: " + Exe);
        Directory.CreateDirectory(outDir);
        var states = JsonNode.Parse(File.ReadAllText(statesFile))!.AsArray().Select(n => n!.AsObject()).ToList();
        if (only != null) states = states.Where(s => only.Contains((string)s["name"]!)).ToList();
        var metrics = new JsonObject();
        var results = new StringBuilder();
        results.AppendLine("# M3b look capture, v5 side").AppendLine();
        results.AppendLine($"Run {DateTime.Now:yyyy-MM-dd HH:mm}, `{Exe}`. UI Automation patterns only; no posted messages; PrintWindow of the app's own window.").AppendLine();
        results.AppendLine("| State | Client | Scale | Canvas value | Notes |").AppendLine("| --- | --- | --- | --- | --- |");
        string baseClient = null;
        foreach (var state in states)
        {
            string name = (string)state["name"]!;
            Launch();
            var main = MainElement();
            var (cw, ch, scale) = Client();
            if (scale != 100) throw new Exception($"monitor scale is {scale}%: the look capture runs at 100%");
            if (baseClient == null)
            {
                baseClient = $"{cw}x{ch}";
                Console.WriteLine($"client={baseClient}");
            }
            if (state["contentWidth"] is JsonNode wNode)
            {
                GetWindowRect(MainHwnd(), out var wr);
                var t = (TransformPattern)Guard(main).GetCurrentPattern(TransformPattern.Pattern);
                t.Resize((int)wNode - cw + (wr.R - wr.L), wr.B - wr.T);
                Settle();
                (cw, ch, _) = Client();
                if (cw != (int)wNode) throw new Exception($"{name}: client width {cw} after resize, wanted {(int)wNode}");
            }
            var notes = new List<string>();
            foreach (var op in state["ops"]!.AsArray().Select(o => o!.AsObject())) Run(op, notes);
            if (name == "01-launch") notes.Add("hint: " + Find("id:Hint", null).Current.Name);
            Settle(1200);
            Shot(Path.Combine(outDir, name + "-v5.png"), cw, ch);
            metrics[name] = Metrics();
            string canvasValue = CanvasValue();
            results.AppendLine($"| {name} | {cw}x{ch} | {scale}% | {canvasValue} | {string.Join("; ", notes)} |");
            Console.WriteLine($"captured {name} {cw}x{ch}");
            StopApp();
        }
        File.WriteAllText(Path.Combine(outDir, "metrics-v5.json"), metrics.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllText(Path.Combine(outDir, "RESULTS.md"), results.ToString());
        Console.WriteLine($"ok {states.Count}");
        return 0;
    }

    static void Run(JsonObject op, List<string> notes)
    {
        string kind = (string)op["op"]!;
        switch (kind)
        {
            case "paint":
                Invoke(Find(Tiles[(string)op["region"]!], "Button"));
                break;
            case "fillType":
                {
                    var item = Find(FillItems[(string)op["type"]!], "ListItem");
                    ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                    Settle();
                    break;
                }
            case "undo":
                {
                    var undo = Find("id:UndoButton", null);
                    if (!undo.Current.IsEnabled) throw new Exception("Undo is disabled");
                    Invoke(undo);
                    break;
                }
            case "mode":
                {
                    var m = Find("id:ModeToggle", null);
                    string before = m.Current.Name;
                    Invoke(m);
                    if (Find("id:ModeToggle", null).Current.Name == before) throw new Exception("the mode did not switch");
                    notes.Add(Find("id:ModeToggle", null).Current.Name);
                    break;
                }
            case "addImage":
                {
                    string file = Path.Combine(Repo, ((string)op["image"]!).Replace('/', '\\'));
                    if (!File.Exists(file)) throw new Exception("image not found: " + file);
                    var known = AppWindows();
                    Invoke(Find("Add image", "Button"), settle: false);
                    IntPtr dlg = IntPtr.Zero;
                    for (int i = 0; i < 50 && dlg == IntPtr.Zero; i++)
                    {
                        Thread.Sleep(200);
                        dlg = AppWindows().FirstOrDefault(h => !known.Contains(h) && ClassOf(h) == "#32770");
                    }
                    if (dlg == IntPtr.Zero) throw new Exception("the Add image dialog did not open");
                    var d = Guard(AutomationElement.FromHandle(dlg));
                    var box = Guard(d.FindFirst(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "1148"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))) ?? throw new Exception("file name box not found"));
                    ((ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern)).SetValue(file);
                    var open = Guard(d.FindFirst(TreeScope.Children, new AndCondition(
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "1"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))) ?? throw new Exception("Open button not found"));
                    ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    string layer = Path.GetFileNameWithoutExtension(file);
                    for (int i = 0; i < 50 && AppWindows().Contains(dlg); i++) Thread.Sleep(200);
                    if (AppWindows().Contains(dlg)) throw new Exception("the Add image dialog did not close");
                    Find(layer, "ListItem");   // the new row, named by file name minus extension
                    notes.Add("added " + layer);
                    break;
                }
            case "rotation":
                {
                    var slider = Find("Rotation degrees", "Slider");
                    var r = (RangeValuePattern)slider.GetCurrentPattern(RangeValuePattern.Pattern);
                    r.SetValue((double)op["value"]!);
                    Settle();
                    if (Math.Abs(r.Current.Value - (double)op["value"]!) > 1e-9) throw new Exception($"rotation is {r.Current.Value}");
                    break;
                }
            case "expandAdjustments":
                {
                    var e = Find("Image adjustments", null);
                    if (e.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var ec))
                    {
                        var p = (ExpandCollapsePattern)ec;
                        if (p.Current.ExpandCollapseState != ExpandCollapseState.Expanded) p.Expand();
                    }
                    else if (e.TryGetCurrentPattern(TogglePattern.Pattern, out var tg))
                    {
                        var p = (TogglePattern)tg;
                        if (p.Current.ToggleState != ToggleState.On) p.Toggle();
                    }
                    else throw new Exception("Image adjustments has neither ExpandCollapse nor Toggle");
                    Settle();
                    break;
                }
            case "tool":
                Invoke(Find(ToolButtons[(string)op["tool"]!], "Button"));
                break;
            case "zoom":
                {
                    string want = "Zoom " + ((double)op["value"]! * 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
                    for (int i = 0; i < 12 && Find("id:ZoomLabel", null).Current.Name != want; i++)
                        Invoke(Find("Zoom in", "Button"));
                    if (Find("id:ZoomLabel", null).Current.Name != want) throw new Exception("zoom did not reach " + want);
                    break;
                }
            case "scrollPanel":
                {
                    var walker = TreeWalker.ControlViewWalker;
                    AutomationElement e = Find("cls:DropZone", null);
                    while (e != null && !e.TryGetCurrentPattern(ScrollPattern.Pattern, out _)) e = walker.GetParent(e);
                    if (e == null) throw new Exception("the left panel has no ScrollPattern");
                    var s = (ScrollPattern)Guard(e).GetCurrentPattern(ScrollPattern.Pattern);
                    if (s.Current.VerticallyScrollable) s.SetScrollPercent(ScrollPattern.NoScroll, 100);
                    Settle();
                    notes.Add(s.Current.VerticallyScrollable ? "left panel scrolled to its end" : "left panel does not scroll");
                    break;
                }
            default: throw new Exception("unknown op " + kind);
        }
    }

    // The same keys as look-ref.js's METRICS, as client-area DIP rectangles.
    static JsonObject Metrics()
    {
        var o = new JsonObject();
        void Add(string key, AutomationElement e)
        {
            var r = e.Current.BoundingRectangle;
            var c = ClientOrigin();
            double s = GetDpiForWindow(MainHwnd()) / 96.0;
            o[key] = new JsonObject { ["x"] = (r.X - c.X) / s, ["y"] = (r.Y - c.Y) / s, ["w"] = r.Width / s, ["h"] = r.Height / s };
        }
        Add("headerTitle", Find("id:HeaderTitle", null));
        Add("homeRibbonPane", Find("cls:HomeRibbon", null));
        Add("modeToggle", Find("id:ModeToggle", null));
        Add("downloadTemplate", Find("Download template", "Button"));
        var walker = TreeWalker.ControlViewWalker;
        AutomationElement panel = Find("cls:DropZone", null);
        while (panel != null && panel.Current.ClassName != "ScrollViewer") panel = walker.GetParent(panel);
        Add("leftPanel", panel ?? throw new Exception("left panel ScrollViewer not found"));
        Add("canvasArea", Find("cls:CanvasView", null));
        Add("zoomIn", Find("id:ZoomInButton", null));
        return o;
    }

    static string CanvasValue()
    {
        var c = Find("Template canvas", null);
        return c.TryGetCurrentPattern(ValuePattern.Pattern, out var v) ? "\"" + ((ValuePattern)v).Current.Value + "\"" : "(no ValuePattern)";
    }

    static void Launch()
    {
        if (File.Exists(Settings)) File.Delete(Settings);
        var psi = new ProcessStartInfo(Exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Exe) };
        App = Process.Start(psi) ?? throw new Exception("launch failed");
        Pid = App.Id;
        for (int i = 0; i < 100 && App.MainWindowHandle == IntPtr.Zero; i++) { Thread.Sleep(200); App.Refresh(); }
        if (App.MainWindowHandle == IntPtr.Zero) throw new Exception("no main window");
        Settle(1500);
    }

    static void StopApp()
    {
        if (App == null || App.HasExited) return;
        try { ((WindowPattern)MainElement().GetCurrentPattern(WindowPattern.Pattern)).Close(); } catch { }
        if (!App.WaitForExit(8000)) { App.Kill(); App.WaitForExit(5000); }
        App = null;
    }

    static (int W, int H, int Scale) Client()
    {
        GetClientRect(MainHwnd(), out var cr);
        double s = GetDpiForWindow(MainHwnd()) / 96.0;
        return ((int)Math.Round(cr.R / s), (int)Math.Round(cr.B / s), (int)Math.Round(s * 100));
    }

    static System.Windows.Point ClientOrigin()
    {
        var p = new POINT();
        ClientToScreen(MainHwnd(), ref p);
        return new System.Windows.Point(p.X, p.Y);
    }

    static void Settle(int ms = 600) => Thread.Sleep(ms);

    static IntPtr MainHwnd() { App.Refresh(); return App.MainWindowHandle; }

    static uint PidOf(IntPtr h) { GetWindowThreadProcessId(h, out uint p); return p; }

    static string ClassOf(IntPtr h) { var c = new StringBuilder(256); GetClassName(h, c, 256); return c.ToString(); }

    static List<IntPtr> AppWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => { if (PidOf(h) == Pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    static AutomationElement Guard(AutomationElement e)
    {
        if (e.Current.ProcessId != Pid) throw new Exception("PID mismatch: refusing to touch an element of another process");
        return e;
    }

    static AutomationElement MainElement() => Guard(AutomationElement.FromHandle(MainHwnd()));

    static void Invoke(AutomationElement e, bool settle = true)
    {
        if (!e.Current.IsEnabled) throw new Exception($"\"{e.Current.Name}\" is disabled");
        ((InvokePattern)Guard(e).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        if (settle) Settle();
    }

    // spec: "Name", "id:AutomationId" or "cls:ClassName"; type is the ControlType's short name or null.
    static AutomationElement Find(string spec, string type)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            foreach (var h in AppWindows())
            {
                AutomationElement top;
                try { top = AutomationElement.FromHandle(h); } catch { continue; }
                if (top.Current.ProcessId != Pid) continue;
                foreach (AutomationElement e in top.FindAll(TreeScope.Subtree, Condition.TrueCondition))
                {
                    var c = e.Current;
                    bool m = spec.StartsWith("id:") ? c.AutomationId == spec[3..]
                           : spec.StartsWith("cls:") ? c.ClassName == spec[4..]
                           : c.Name == spec;
                    if (m && type != null) m = c.ControlType.ProgrammaticName == "ControlType." + type;
                    if (m) return Guard(e);
                }
            }
            Thread.Sleep(300);
        }
        throw new Exception($"not found: {spec}{(type == null ? "" : " (" + type + ")")}");
    }

    static void Shot(string path, int clientW, int clientH)
    {
        IntPtr h = MainHwnd();
        if (PidOf(h) != Pid) throw new Exception("REFUSED: not the app's window");
        GetWindowRect(h, out var r);
        int w = r.R - r.L, hh = r.B - r.T;
        IntPtr wdc = GetDC(h);
        IntPtr mdc = CreateCompatibleDC(wdc);
        IntPtr bmp = CreateCompatibleBitmap(wdc, w, hh);
        var old = SelectObject(mdc, bmp);
        bool ok = PrintWindow(h, mdc, PW_RENDERFULLCONTENT);
        SelectObject(mdc, old);
        if (!ok) throw new Exception("PrintWindow failed");
        var full = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        DeleteObject(bmp); DeleteDC(mdc); ReleaseDC(h, wdc);
        var origin = ClientOrigin();
        double s = GetDpiForWindow(h) / 96.0;
        var crop = new CroppedBitmap(full, new Int32Rect((int)origin.X - r.L, (int)origin.Y - r.T, (int)Math.Round(clientW * s), (int)Math.Round(clientH * s)));
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(crop, PixelFormats.Bgr32, null, 0)));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
```

Build it, then check the safety rule mechanically:

```bash
dotnet build .superpowers/m3b-look/driver/uia/uia.csproj -c Release 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
grep -nE "PostMessage|SendMessage|SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|SetForegroundWindow|BitBlt|CopyFromScreen" .superpowers/m3b-look/driver/uia/Program.cs | grep -v "^[0-9]*://"
```

Expected: `0 Warning(s)` and `0 Error(s)` (measured 2026-10-15), and the grep prints only header comment lines (it prints nothing: every hit is on a `//` line, which the second grep drops). Any hit in code is a safety violation: remove it before the controller runs the driver. Report to the controller that the driver is built; do not run it.

- [ ] **Step 9: Capture v5, then 4.1 (controller-run)**

This step is the controller's. It launches the Release build, so the desktop must be Este's, unlocked, with the main display at 100% scale (the driver refuses otherwise). It posts no input, but the app's windows appear and close seven times, and the Add image dialog appears for states 03 and 05; ask Este not to type or click in them while it runs (about a minute).

```bash
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
mkdir -p .superpowers/m3b-look/v5 .superpowers/m3b-look/ref
.superpowers/m3b-look/driver/uia/bin/Release/net10.0-windows/drv.exe look tools/parity-ref/look-states.json .superpowers/m3b-look/v5 | tee .superpowers/logs/m3b-look-v5.log
CONTENT=$(sed -n 's/^client=//p' .superpowers/logs/m3b-look-v5.log); echo "content=$CONTENT"
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/look-ref.js \
  --states tools/parity-ref/look-states.json --content "$CONTENT" --out .superpowers/m3b-look/ref | tee .superpowers/logs/m3b-look-41.log
cat .superpowers/m3b-look/v5/RESULTS.md
```

Expected:

- Build: `0 Warning(s)`, `0 Error(s)`.
- The driver: `client=<W>x<H>` first (the 1200x800 window's client area at 100%, about 1184x761 by the M3a launch tree), seven `captured <state> <W>x<H>` lines (`700x<H>` for state 07), `ok 7`, then `settings.json restored` (or `settings.json removed (there was none before)`).
- `look-ref.js`: seven `captured` lines at the same sizes and `ok 7`.
- `RESULTS.md`: the `01-launch` row's notes include `hint: Set a style, then click a region to paint it. Right-click clears.`; the `03-layer-selected` row reads `"drip_red_wide_v1"` in the Canvas value column and `added drip_red_wide_v1; left panel scrolled to its end` in its notes; the `06-pants` notes read `Mode: PANTS`.

A `not found:` error names the element the driver looked for: compare it with Additions requested item 7 and report the owning task's automation name to the controller. Never add mouse or key posting to get past it.

- [ ] **Step 10: Build and publish the comparison page (controller-run)**

Save the builder as `.superpowers/m3b-look/build_page.py` (scratch):

```python
"""M3b look sign-off page. Usage: python build_page.py <look-dir> <commit> <capture-date>
<look-dir> holds ref/ (look-ref.js output), v5/ (the UIA driver's output) and receives page/:
page/look-signoff.html, page/img/*.png and page/metrics-verdict.json. Prints the verdicts the ledger
close-out reads (shell-layout-skeleton within 1 px, font-ui-runtime-google within 2 px)."""
import html, json, os, shutil, sys

look, commit, date = sys.argv[1], sys.argv[2], sys.argv[3]
ref, v5, page = (os.path.join(look, d) for d in ("ref", "v5", "page"))
os.makedirs(os.path.join(page, "img"), exist_ok=True)
index = json.load(open(os.path.join(ref, "index.json"), encoding="utf-8"))
m41 = json.load(open(os.path.join(ref, "metrics.json"), encoding="utf-8"))
m5 = json.load(open(os.path.join(v5, "metrics-v5.json"), encoding="utf-8"))
results = open(os.path.join(v5, "RESULTS.md"), encoding="utf-8").read()

# Shown on every state: what differs by ruling, not by accident.
COMMON = [
    ("The byline reads V5.0 (4.1: V4.1); one version source", "shell-header-version-hardcoded"),
    ("The footer reads DEVELOPED AT 626LABS (4.1: 626 LABS LLC), Este 2026-10-14", "shell-footer"),
    ("Layout, Details and Text tabs, the asset library, Placement, Save work and Open are hidden until M4", "home-placement-group"),
    ("Fill Style and Template Regions sit on Home until M4 moves them to Layout", "layout-fill-style"),
    ("No Keys: ON button and no shortcut letters in tool tips until M4", "cross-text-select-and-selection"),
    ("Space Grotesk and JetBrains Mono labels draw at 700 where 4.1 draws 600", "font-ui-weight-600"),
    ("Captures are the client area: the DWM-themed caption is not in them", "shell-window-frame"),
]
STATES = {
    "01-launch": {
        "title": "Launch",
        "diffs": [("Zoom buttons disable at 25% and 300% (not visible at 100%)", "zoom-pill-limits")],
        "rows": ["theme-tokens-colors", "font-ui-runtime-google", "font-ui-weight-600", "shell-layout-skeleton", "mode-toggle-button", "regions-hint-text"],
    },
    "02-home-painted": {
        "title": "Home tab, painted shirt",
        "diffs": [
            ("v5 swatches are painted by the Skia fill painter with the canvas angle convention; 4.1 uses CSS gradients", "fills-to-css"),
            ("4.1 shows the region grid on its Layout tab: compare v5's grid with the extra 4.1 capture below", "regions-grid-layout-shirt"),
            ("The Color 1 and Color 2 swatch radius 2 is provisional (M3a); confirm or set it", "fillstyle-color-pickers"),
        ],
        "rows": ["regions-grid-layout-shirt", "regions-hint-text", "theme-tokens-colors"],
        "extra": ("4.1 Layout tab, same state", "layout"),
    },
    "03-layer-selected": {
        "title": "Selected layer, properties panel",
        "diffs": [
            ("Up and Down sit in the panel; Center on front torso is hidden until M4", "props-center-front"),
            ("The rotation label is excluded from the overlay goldens (interfaces R4): judge it here", "render-rotation-label"),
            ("Image adjustments stays open across edits and selections", "props-adjust-panel"),
            ("Sliders keep WPF chrome: no cyan or R, G and B accent-color (styles.css:333, editor.js:795-799)", "props-adjust-sliders"),
            ("Reset's border turns cyan on hover where 4.1 stays magenta", "props-adjust-reset"),
            ("No CSS letter-spacing: WPF has no letter-spacing property (M3a ruling 5)", "theme-tokens-shape-motion-type"),
            ("Panel headings are not uppercase (PanelHeading; styles.css:258-259)", "layers-list-render"),
            ("Knob glyph antialiasing may differ by a few levels at some offsets (Task 1 T1-3)", "overlay-knob-glyph-rasterization"),
        ],
        "rows": ["layers-list-render", "theme-scrollbars", "render-rotation-label", "props-adjust-panel"],
    },
    "04-draw-tools": {
        "title": "Draw tool active",
        "diffs": [
            ("The brush color is the themed picker button, not Chromium's input type=color", "tools-draw-props"),
            ("The brush size slider is WPF's stock slider (provisional)", "draw-brush-controls"),
            ("Grab and grabbing show the Windows Hand cursor (no stock grab cursor; not in a capture)", "canvas-cursor-states"),
        ],
        "rows": ["tools-set-tool", "draw-brush-controls"],
    },
    "05-zoom-200": {
        "title": "Zoom 200%",
        "diffs": [("v5 scrolls to reach every face; 4.1 clips the canvas", "render-zoom-canvas")],
        "rows": ["render-zoom-canvas", "theme-scrollbars"],
    },
    "06-pants": {
        "title": "Pants mode",
        "diffs": [("The region tiles read Waist, Right Leg and Left Leg on Home", "home-mode-toggle")],
        "rows": ["mode-toggle-button"],
    },
    "07-narrow-700": {
        "title": "Narrow window (supplementary)",
        "diffs": [("Below 900 DIPs the left panel is 200 wide, as in 4.1", "shell-responsive-breakpoint")],
        "rows": ["ribbon-overflow-hscroll"],
    },
}
LABELS = {
    "headerTitle": "Header title", "homeRibbonPane": "Home ribbon", "modeToggle": "Mode button",
    "downloadTemplate": "Download template", "leftPanel": "Left panel", "canvasArea": "Canvas area", "zoomIn": "Zoom in",
}
# The boundaries shell-layout-skeleton names: header bottom = ribbon top, ribbon bottom, panel right edge, footer top.
BOUNDS = {
    "ribbon top": ("homeRibbonPane", lambda r: r["y"]),
    "ribbon bottom": ("homeRibbonPane", lambda r: r["y"] + r["h"]),
    "panel right": ("leftPanel", lambda r: r["x"] + r["w"]),
    "footer top": ("canvasArea", lambda r: r["y"] + r["h"]),
}

def chip(row):
    return f'<code class="chip">{html.escape(row)}</code>'

verdict = {"layout": {}, "downloadTemplateWidth": None}
sections = []
for st in index["states"]:
    name = st["name"]
    meta = STATES[name]
    shots = {s["file"]: s for s in st["shots"]}
    v5file = f"{name}-v5.png"
    if not os.path.exists(os.path.join(v5, v5file)):
        raise SystemExit(f"missing v5 capture {v5file}")
    for f in list(shots) + [v5file]:
        shutil.copyfile(os.path.join(ref if f.endswith("-41.png") else v5, f), os.path.join(page, "img", f))
    a, b = f"img/{name}-41.png", f"img/{v5file}"
    extra = ""
    if "extra" in meta:
        cap, tab = meta["extra"]
        extra = (f'<figure class="extra"><img src="img/{name}-{tab}-41.png" alt="4.1 {html.escape(cap)}" loading="lazy">'
                 f"<figcaption>{html.escape(cap)}</figcaption></figure>")
    diffs = "".join(f"<li>{html.escape(t)} {chip(r)}</li>" for t, r in meta["diffs"])
    rows = " ".join(chip(r) for r in meta["rows"])
    sections.append(f"""
<section class="state" id="s{name[:2]}">
  <h2><span class="num">{name[:2]}</span> {html.escape(meta['title'])}</h2>
  <p class="shows">{html.escape(st['shows'])}</p>
  <div class="pair" data-pair>
    <figure><img src="{a}" alt="4.1, {html.escape(meta['title'])}" loading="lazy"><figcaption>4.1</figcaption></figure>
    <figure><img src="{b}" alt="v5, {html.escape(meta['title'])}" loading="lazy"><figcaption>v5</figcaption></figure>
    <div class="stack" aria-hidden="true"><img src="{a}" alt=""><img class="top" src="{b}" alt=""></div>
  </div>
  {extra}
  <div class="notes"><h3>Known differences</h3><ul>{diffs}</ul><p class="signs">Signs off: {rows}</p></div>
</section>""")
    for label, (key, fn) in BOUNDS.items():
        d = fn(m5[name][key]) - fn(m41[name][key])
        prev = verdict["layout"].get(label, 0)
        verdict["layout"][label] = max(prev, abs(d), key=abs)
    if name == "01-launch":
        verdict["downloadTemplateWidth"] = m5[name]["downloadTemplate"]["w"] - m41[name]["downloadTemplate"]["w"]

rows_html = []
for name in m41:
    for key, label in LABELS.items():
        r1, r5 = m41[name][key], m5[name][key]
        d = [r5[k] - r1[k] for k in ("x", "y", "w", "h")]
        flag = ' class="off"' if any(abs(x) > 1 for x in d) else ""
        cells = "".join(f"<td>{x:+.1f}</td>" for x in d)
        rows_html.append(f"<tr{flag}><td>{name[:2]}</td><td>{label}</td>"
                         f"<td>{r1['x']:.1f}, {r1['y']:.1f}, {r1['w']:.1f}x{r1['h']:.1f}</td>"
                         f"<td>{r5['x']:.1f}, {r5['y']:.1f}, {r5['w']:.1f}x{r5['h']:.1f}</td>{cells}</tr>")
layout_ok = all(abs(v) <= 1 for v in verdict["layout"].values())
font_ok = verdict["downloadTemplateWidth"] is not None and abs(verdict["downloadTemplateWidth"]) <= 2
verdict.update({"shell-layout-skeleton": layout_ok, "font-ui-runtime-google": font_ok})
json.dump(verdict, open(os.path.join(page, "metrics-verdict.json"), "w", encoding="utf-8"), indent=2)

common = "".join(f"<li>{html.escape(t)} {chip(r)}</li>" for t, r in COMMON)
bounds = "".join(f"<li>{html.escape(k)}: {v:+.1f} DIP</li>" for k, v in verdict["layout"].items())
doc = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Look Sign-off</title>
<style>
:root {{
  --bg: #f5f7fb; --surface: #ffffff; --ink: #152135; --muted: #5e6b7f; --line: #c4cdda;
  --accent: #0fa8c9; --accent-2: #c21f6c; --off: #fde8f1;
}}
@media (prefers-color-scheme: dark) {{
  :root:not([data-theme="light"]) {{
    --bg: #050c18; --surface: #0f1f31; --ink: #e6ebf3; --muted: #8e9bad; --line: #2a3649;
    --accent: #17d4fa; --accent-2: #f22f89; --off: #3a1428;
  }}
}}
:root[data-theme="dark"] {{
  --bg: #050c18; --surface: #0f1f31; --ink: #e6ebf3; --muted: #8e9bad; --line: #2a3649;
  --accent: #17d4fa; --accent-2: #f22f89; --off: #3a1428;
}}
* {{ box-sizing: border-box; }}
body {{ margin: 0; background: var(--bg); color: var(--ink); font: 15px/1.5 system-ui, sans-serif; }}
main {{ max-width: 1680px; margin: 0 auto; padding: 24px 16px 64px; }}
h1 {{ font-size: 26px; margin: 0 0 4px; }}
.sub, .shows, figcaption, .meta {{ color: var(--muted); }}
.panel {{ background: var(--surface); border: 1px solid var(--line); border-radius: 10px; padding: 14px 16px; margin: 16px 0; }}
.controls {{ position: sticky; top: 0; z-index: 5; display: flex; flex-wrap: wrap; gap: 12px; align-items: center; }}
.seg button {{ font: inherit; padding: 6px 12px; border: 1px solid var(--line); background: var(--surface); color: var(--ink); cursor: pointer; }}
.seg button[aria-pressed="true"] {{ border-color: var(--accent); color: var(--accent); }}
.seg button:first-child {{ border-radius: 6px 0 0 6px; }} .seg button:last-child {{ border-radius: 0 6px 6px 0; }}
.state {{ margin: 36px 0; }}
h2 {{ font-size: 20px; margin: 0; }} .num {{ color: var(--accent); font-variant-numeric: tabular-nums; }}
h3 {{ font-size: 14px; margin: 0 0 6px; }}
.pair {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 10px; }}
figure {{ margin: 0; }}
img {{ display: block; max-width: 100%; height: auto; border: 1px solid var(--line); border-radius: 6px; }}
.stack {{ display: none; position: relative; }}
.stack img.top {{ position: absolute; inset: 0; opacity: var(--mix, .5); }}
body[data-mode="overlay"] .pair, body[data-mode="difference"] .pair {{ grid-template-columns: 1fr; }}
body[data-mode="overlay"] .pair figure, body[data-mode="difference"] .pair figure {{ display: none; }}
body[data-mode="overlay"] .stack, body[data-mode="difference"] .stack {{ display: block; }}
body[data-mode="difference"] .stack img.top {{ opacity: 1; mix-blend-mode: difference; }}
.extra {{ margin-top: 12px; max-width: 50%; }}
.notes ul {{ margin: 0; padding-left: 18px; }}
.chip {{ font-size: 12px; padding: 1px 6px; border-radius: 4px; background: var(--bg); border: 1px solid var(--line); }}
table {{ border-collapse: collapse; width: 100%; font-size: 13px; font-variant-numeric: tabular-nums; }}
.scroll {{ overflow-x: auto; }}
th, td {{ border-bottom: 1px solid var(--line); padding: 4px 8px; text-align: left; white-space: nowrap; }}
tr.off td {{ background: var(--off); }}
@media (max-width: 720px) {{ .pair {{ grid-template-columns: 1fr; }} .extra {{ max-width: 100%; }} }}
</style>
</head>
<body data-mode="side">
<main>
  <h1>RBX15 v5 look sign-off (M3b)</h1>
  <p class="sub">4.1 on the left, v5 on the right, in matched states at a 1200x800 window and 100% display scale.</p>
  <p class="meta">Branch commit {html.escape(commit)}, captured {html.escape(date)}. 4.1: tools/parity-ref/look-ref.js, Electron {html.escape(index['electron'])}, client {html.escape(index['content'])}. v5: the Release build through the UI Automation driver, PrintWindow of the app's own window, no synthetic input.</p>
  <div class="panel"><h3>How to sign off</h3><p>Reply in the chat with <b>signed off</b>, or name the state numbers and what should change. Changes go into a follow-up fix round; nothing ships on this page alone.</p></div>
  <div class="panel controls" role="group" aria-label="Comparison mode">
    <div class="seg"><button data-mode="side" aria-pressed="true">Side by side</button><button data-mode="overlay" aria-pressed="false">Overlay</button><button data-mode="difference" aria-pressed="false">Difference</button></div>
    <label>v5 opacity <input id="mix" type="range" min="0" max="100" value="50"></label>
  </div>
  <div class="panel"><h3>Different on purpose, every state</h3><ul>{common}</ul></div>
  {''.join(sections)}
  <section class="panel"><h3>Measured boundaries (client DIPs, v5 minus 4.1)</h3>
    <p>Largest difference per boundary across all states: </p><ul>{bounds}</ul>
    <p>shell-layout-skeleton within 1 px: <b>{'yes' if layout_ok else 'no'}</b>. Download template width within 2 px (font-ui-runtime-google): <b>{'yes' if font_ok else 'no'}</b> ({verdict['downloadTemplateWidth']:+.1f}).</p>
    <div class="scroll"><table><thead><tr><th>State</th><th>Element</th><th>4.1 x, y, w x h</th><th>v5 x, y, w x h</th><th>dx</th><th>dy</th><th>dw</th><th>dh</th></tr></thead>
    <tbody>{''.join(rows_html)}</tbody></table></div>
  </section>
  <section class="panel"><h3>v5 capture log</h3><pre style="white-space:pre-wrap">{html.escape(results)}</pre></section>
</main>
<script>
(function () {{
  var body = document.body, mix = document.getElementById('mix');
  function setMode(m) {{
    body.dataset.mode = m;
    document.querySelectorAll('.seg button').forEach(function (b) {{ b.setAttribute('aria-pressed', String(b.dataset.mode === m)); }});
    try {{ localStorage.setItem('look-mode', m); }} catch (e) {{}}
  }}
  document.querySelectorAll('.seg button').forEach(function (b) {{ b.addEventListener('click', function () {{ setMode(b.dataset.mode); }}); }});
  mix.addEventListener('input', function () {{ document.documentElement.style.setProperty('--mix', mix.value / 100); }});
  try {{ var saved = localStorage.getItem('look-mode'); if (saved) setMode(saved); }} catch (e) {{}}
}})();
</script>
</body>
</html>
"""
open(os.path.join(page, "look-signoff.html"), "w", encoding="utf-8", newline="\n").write(doc)
print(json.dumps({k: verdict[k] for k in ("shell-layout-skeleton", "font-ui-runtime-google")}))
print(f"page {os.path.join(page, 'look-signoff.html')}; {len(os.listdir(os.path.join(page, 'img')))} images")
```

The page, top to bottom:

- A title, one line on what is compared, and provenance: the branch commit, the capture date, Electron and Chrome versions, the client size, and how v5 was driven (UI Automation only, PrintWindow).
- "How to sign off": reply in the chat with `signed off`, or name state numbers and what to change.
- A sticky mode switch: Side by side (two columns, 4.1 left, v5 right), Overlay (v5 over 4.1 with an opacity slider) and Difference (CSS `mix-blend-mode: difference`, where identical pixels go black). The choice is remembered per viewer in `localStorage`, behind try/catch.
- "Different on purpose, every state": the seven rulings that show in every capture, each with its ledger id.
- One section per state: number and title, what it shows, the pair (state 02 adds 4.1's Layout tab), "Known differences" with ledger ids, and "Signs off" naming the look rows the state is evidence for.
- "Measured boundaries": the largest v5 minus 4.1 difference per boundary across all states, the two verdicts, and the full metrics table, with rows over 1 DIP tinted.
- The v5 capture log (`RESULTS.md`).
- Light and dark themes through `:root` tokens with the `prefers-color-scheme` guard and `data-theme` overrides; one column under 720 px; no external scripts or fonts.

Build it, then publish:

```bash
python .superpowers/m3b-look/build_page.py .superpowers/m3b-look "$(git rev-parse --short HEAD)" "$(date +%F)"
ls .superpowers/m3b-look/page/img | wc -l
```

Expected: one JSON line `{"shell-layout-skeleton": <true|false>, "font-ui-runtime-google": <true|false>}`, then `page .../look-signoff.html; 15 images`, and `15` (eight 4.1 captures, seven v5).

Publish it as a claude.ai Artifact with the controller's Artifact tool. Load the `artifact-design` skill first (the page already follows its contract: a two-word title, tokens on `:root` with the dark-mode guard and `data-theme` overrides, an explicit body background, a 16 px gutter, no horizontal page scroll, inline script only). Call `publish` with `file_path` `.superpowers/m3b-look/page/look-signoff.html`, `icon` `compare`, a one-sentence `description` ("RBX15 4.1 and v5 side by side in seven matched states for the M3b look sign-off."), and `files` mapping each `img/<name>.png` to `.superpowers/m3b-look/page/img/<name>.png` (15 entries). Do not present it yet: Step 16 does, after Este's hand checks, so he reviews it once.

- [ ] **Step 11: Este's hand checks (controller-run, with Este)**

Automation cannot drag a real mouse (WPF ignores posted mouse messages), drag from Explorer, right-click, hear a ding, or move a window between monitors. Este runs the Release build (`dotnet run --project src/RBX15.App/RBX15.App.csproj -c Release --no-build`) and answers each item pass, fail or not run. A failed item takes the rows in its last column out of Step 13 and goes to the controller as a defect for the owning task.

| # | Do | Expect | Rows it evidences |
| --- | --- | --- | --- |
| H1 | Add a drip. Drag it on the canvas, out past the canvas edge and back, and release outside the window. Click it without moving. Undo. | It follows the pointer the whole way, also outside the canvas, and stays where released. The click changes nothing. One Undo puts it back where the drag started. | `select-move`, `select-move-hit-and-drag`, `select-hover-and-end`, `canvas-select-gestures` |
| H2 | Hover the knob, a corner, the body and empty canvas. Drag a corner outward; drag the knob around past 45° and past 180°. Watch the Selected layer panel while dragging. | Hand on the knob, diagonal arrows on a corner, four-way move on the body, crosshair elsewhere, and the drag keeps its cursor until release. The corner drag grows the drip about its center; the knob snaps at each 45° and wraps cleanly at 180°. Scale and Rotation in the panel move live, Rotation in whole degrees. One Undo per drag. | `cursor-default`, `canvas-cursor-states`, `select-hover-idle`, `canvas-select-gestures`, `props-live-refresh` |
| H3 | With the drip selected, roll the mouse wheel one notch up, one down, then fast up until it stops growing; on a touchpad, one two-finger flick. Undo. | Each notch scales about the center; it stops at 500% (panel reads 500%); a touchpad flick steps by its events, not by distance. One Undo reverts each burst. | `wheel-scale-layer`, `wheel-sign-only-and-midstroke`, `wheel-scale-undo-burst`, `canvas-select-gestures` |
| H4 | Draw: a pen stroke, a pen click, a filled Rect, an Ellipse; during the Rect drag roll the wheel up three notches. Draw a Line and release outside the window. Right-drag with Draw. | Each stroke becomes one layer named `Pen drawing`, `Rect drawing`, `Circle drawing` or `Line drawing`, selected, and the tool stays. The click makes a brush-size dot. The Rect widens live and commits at the new width. The Line released outside is committed. The right-drag draws nothing. | `canvas-mouse-button-agnostic`, `draw-release-outside-canvas` |
| H5 | Eraser at size 4 and 50, zoom 100% and 200%. With nothing selected, press on the canvas. Select a layer; click without moving; then erase a stroke across it. | The ring matches the brush footprint at each size and zoom and re-sizes as soon as size or zoom changes; the cursor elsewhere is a crosshair for draw tools. No selection: the status strip reads `Select a layer to erase from, then try again.` and nothing changes. The click erases nothing; the stroke erases only the selected layer, one Undo. | `tools-cursor`, `cursor-eraser`, `eraser-cursor`, `render-restore-and-cursor`, `cursor-default`, `eraser-click-without-move` |
| H6 | In the panel drag the Opacity slider and the Brightness slider; type 12.7 in X and press Tab; open Image adjustments and select another layer and back. Press Ctrl+Z with the canvas focused; type in X and press Ctrl+Z before leaving the field. With no layer selected paint Front, then right-click Front. | One Undo per slider drag; X shows 13; Image adjustments stays open. Ctrl+Z undoes one step; in the X field with typing pending, Ctrl+Z undoes the typing only. After the right-click the Front tile shows its purple resting tint again. | `region-front-highlight-lost`, `layers-reorder-drag-2` (Ctrl+Z), `fillstyle-color-pickers` (Ctrl+Z) |
| H6b | Type a new name in the panel's Name field, then click another layer on the canvas without pressing Enter or Tab. | The name lands on the first layer, as one step, before the canvas pick selects the other layer (Task 3 ruling 7). | `props-name-xy` |
| H7 | Add four layers. Drag the bottom row onto the top half of the top row; drag a row and release between the list and the Selected layer panel; focus a row and press Alt+Up. | While dragging, the source row dims and a line shows above or below the row under the pointer. The first drop makes it the top layer; the drop outside every row does nothing; Alt+Up moves the focused row one place. One Undo per reorder. | `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer` |
| H7b | Start a row drag, then press Esc; start another and Alt+Tab away. Then, with about twenty layers, drag the top row, wheel the left panel down mid-drag, and drop on the last row's lower half (Task 4 M3, M4). | Esc and Alt+Tab each cancel with nothing moved and no row left dimmed. The wheeled drag lands the row at the bottom, one Undo. | `layers-reorder-drag-2` |
| H8 | Hide a layer; rename one to a 40-character name; press Dupe until there are 20 layers; operate the list with Tab, arrows, Space and Enter only. | A hidden row's name dims to 40%; the long name is cut with an ellipsis; the list scrolls; every stop shows the cyan focus ring. | `layers-list` |
| H9 | Optional: if a display at 125% or 150% is available, move the window there. Zoom 100%, then Download template. | Template edges are crisp; the layout matches the 100% display in DIPs; the PNG is 585x559. Answer `not run` if no such display is available. | `canvas-device-pixel-render`, `cross-window-dpi-rendering` |
| H10 | Drag a PNG from Explorer over the ribbon and the layers list, then onto the drop zone, then onto the canvas. Open Color 1, drag in the square and along the hue strip, then click the canvas. Move the window to a second monitor, close, relaunch. Look at the 1 px window border. | Over the ribbon and list: no-drop cursor, nothing happens; the drop zone highlights and lands the layer centered in the front torso; the canvas lands it on the drop point. Painted regions follow the square and strip live; the outside click commits without a ding, and one Undo reverts. The window reopens on the second monitor. The border reads navy (#223A54). | `shell-external-drop-guard`, `fillstyle-color-pickers` (the monitor and border are defect checks for `shell-window-size` and `shell-window-frame`, which stay open) |
| H10b | Tab to Color 1, then to Add image (Task 4 M5). | Each shows its tooltip under it while focused, and the tooltip closes when Tab moves on. | `cross-text-select-and-selection` (stays open: its hover audit is M4's) |

Record the answers in `.superpowers/logs/m3b-hand.txt` (git-ignored), one line per item in exactly this form, then one summary line, which Step 15's commit body reads verbatim:

```text
H1: pass
H2: pass
...
H9: not run
H10: pass
H10b: pass
Hand checks: H1-H8, H6b, H7b, H10 and H10b pass, H9 not run, at 100% display scale, run by Este on 2026-10-16.
```

A failing item's line reads `H4: fail` and the summary says what Este saw.

- [ ] **Step 12: Confirm the evidence for each row before closing it**

Save the gate as `.superpowers/logs/m3b-evidence.py`:

```python
"""M3b Task 5 evidence gate.
Usage: python m3b-evidence.py <normal-verbosity dotnet test log> <hand-check results> <v5 RESULTS.md>
Prints `proven` or `OPEN` per candidate row and exits 1 if any row is OPEN. The look rows are gated by
the sign-off step, not here."""
import collections, json, re, sys

log = open(sys.argv[1], encoding="utf-8", errors="replace").read()
hand_text = open(sys.argv[2], encoding="utf-8").read()
results = open(sys.argv[3], encoding="utf-8").read()
LINE = re.compile(r'^\s+(Passed|Failed|Skipped) RBX15\.(App|Engine|Parity)\.Tests\.([\w.]+)\.(\w+)(?:\((.*)\))? \[', re.M)
passed, failed = collections.Counter(), collections.Counter()
methods, select_passed = set(), set()
for status, proj, ns_class, method, args in LINE.findall(log):
    cls = ns_class.rsplit(".", 1)[-1]
    if status == "Passed":
        passed[(proj, cls)] += 1
        methods.add((cls, method))
        m = re.match(r'name: "([^"]+)"', args or "")
        if cls == "GoldenViewSelectTests" and m:
            select_passed.add(m.group(1))
    elif status == "Failed":
        failed[(proj, cls)] += 1
hand = dict(re.findall(r'^(H\d+[a-z]?): (pass|fail|not run)', hand_text, re.M))

scenes = [s["name"] for s in json.load(open("tests/goldens/view-select/scenes.json", encoding="utf-8"))]
SELECT = [n for n in scenes if n.startswith("vs-select-")]
PREVIEW = [n for n in scenes if n.startswith("vs-preview-")]

E, A = "Engine", "App"
SC = "LedgerScenarioTests"
S = {
    "select": "Select_resize_rotate_and_move_are_one_step_each_and_a_click_records_nothing",
    "wheel": "A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step",
    "pick": "The_pick_is_the_box_not_the_pixels_and_a_hidden_selected_layer_has_no_handles",
    "draw": "Draw_tools_commit_one_named_layer_and_keep_the_tool_and_a_click_draws_only_the_pen_dot",
    "eraser": "The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden",
    "switch": "A_tool_switch_mid_stroke_discards_it_and_a_lost_capture_commits_it",
    "brush": "The_wheel_steps_the_brush_in_draw_tools_and_widens_a_live_shape",
    "props": "Every_property_edit_is_one_step_zero_shows_as_zero_and_adjustments_stay_open",
    "live": "The_panel_follows_a_canvas_drag_live_and_refreshes_after_a_region_fit",
    "reorder": "Dragging_a_row_reorders_in_one_step_a_stale_target_does_nothing_and_move_up_is_one_step",
    "tools": "Tool_buttons_toggle_off_to_select_and_an_import_or_a_list_pick_returns_to_select",
    "undo": "Brush_tool_and_zoom_are_never_undo_steps_and_undo_leaves_them_alone",
    "list": "Three_imports_list_newest_first_and_hide_and_dupe_follow_the_z_order",
    "color": "A_color_pick_retargets_matching_regions_in_one_step_and_a_cancel_changes_nothing",
}
SG, LO, LE, RO, CI, TH = (E, "SelectionGeometryTests"), (E, "LayerOpsTests"), (E, "LayerOpsEditTests"), (E, "RendererOverlayTests"), (A, "CanvasInteractionTests"), (A, "TemplateCanvasHostTests")
GA, TO, ER, CC, LP, BR = (E, "GoldenAppTests"), (E, "ToolsTests"), (E, "EraserTests"), (A, "CanvasCursorsTests"), (A, "LayerPropertiesViewModelTests"), (A, "BrushViewModelTests")
GR = (E, "GoldenRenderTests")
# row -> (test classes that must pass with no failure, scenario keys, view-select scenes, hand checks)
ROWS = {
    "select-hit-test": ([LO, SG], ["pick"], [], []),
    "select-hit-test-rect-not-alpha": ([LO], ["pick"], [], []),
    "select-rotate-handle-hit": ([SG], ["pick"], [], []),
    "select-resize-handle-hit": ([SG], ["pick"], [], []),
    "select-resize-drag": ([SG, CI], ["select"], [], []),
    "select-rotate-drag": ([SG], ["select"], [], []),
    "select-rotate-drag-2": ([SG], [], [], []),
    "select-resize-handles": ([SG], ["select"], [], []),
    "select-move-hit-and-drag": ([LO, SG, TH], ["select"], [], ["H1"]),
    "draw-begin": ([GA, TO, CI], ["draw"], [], []),
    "draw-tool-start": ([GA, CI], ["draw"], [], []),
    "draw-pen-single-click": ([CI], ["draw"], [], []),
    "draw-commit-layer": ([GA, CI], ["draw"], [], []),
    "draw-empty-no-layer": ([CI], ["draw"], [], []),
    "layers-add-drawing": ([TO], ["draw"], [], []),
    "eraser-apply": ([GA, ER], ["eraser"], [], []),
    "canvas-draw-tools": ([GA, ER, TO, CI], ["draw", "eraser"], [], []),
    "draw-preview-composite": ([RO, (E, "LivePreviewTests")], [], PREVIEW, []),
    "render-live-preview": ([RO], [], PREVIEW, []),
    "tools-set-tool": ([(A, "ToolsViewModelTests")], ["tools"], [], []),
    "tools-auto-select-switch": ([(A, "ToolsViewModelTests")], ["tools"], [], []),
    "tools-cursor": ([CC, TH], [], [], ["H5"]),
    "cursor-eraser": ([CC], [], [], ["H5"]),
    "eraser-cursor": ([CC], [], [], ["H5"]),
    "cursor-default": ([CI, CC], [], [], ["H2", "H5"]),
    "canvas-cursor-states": ([CI], [], [], ["H2"]),
    "select-hover-idle": ([CI], [], [], ["H2"]),
    "render-restore-and-cursor": ([TH, CC], [], [], ["H5"]),
    "render-selection-box": ([RO], ["pick"], ["vs-select-z100", "vs-select-rot30-z100"], []),
    "render-resize-handles": ([], [], ["vs-select-z100", "vs-select-z200"], []),
    "render-rotation-handle": ([RO], [], SELECT, []),
    "render-rotation-label": ([SG, RO], [], [], []),
    "select-handle-render": ([], [], SELECT, []),
    "select-hidden-layer-still-handled": ([RO, SG], ["pick"], [], []),
    "select-rotate-handle": ([SG], [], [], []),
    "select-move": ([TH], ["select"], [], ["H1"]),
    "select-deselect-click": ([RO], ["select"], [], []),
    "select-hover-and-end": ([TH, CI], ["switch"], [], ["H1"]),
    "canvas-select-gestures": ([CI, SG], ["select", "wheel"], [], ["H1", "H2", "H3"]),
    "canvas-coord-map": ([(A, "CanvasCoordinatesTests"), TH], ["select"], [], []),
    "canvas-mouse-button-agnostic": ([TH], [], [], ["H4"]),
    "draw-release-outside-canvas": ([CI, TH], ["switch"], [], ["H4"]),
    "wheel-scale-layer": ([SG], ["wheel"], [], ["H3"]),
    "wheel-brush-size": ([TO, BR], ["brush"], [], []),
    "wheel-sign-only-and-midstroke": ([CI, TH], ["brush"], [], ["H3"]),
    "eraser-selection-semantics": ([CI], ["eraser"], [], []),
    "layers-empty-state": ([LP], ["props", "select"], [], []),
    "props-name-xy": ([LP], ["props", "live"], [], ["H6b"]),
    "props-scale": ([LP, GR], ["props"], [], []),
    "props-rotation": ([LP, GR], [], ["vs-select-rot30-z100"], []),
    "props-opacity": ([LP, GR], ["props"], [], []),
    "props-flip": ([LE, GR], [], ["vs-select-fliph-z50"], []),
    "props-adjust-panel": ([LP], ["props"], [], []),
    "props-adjust-reset": ([LP, LE], ["props"], [], []),
    "props-live-refresh": ([LP], ["live"], [], ["H2"]),
    "props-falsy-zero-adjust-display": ([LP], ["props"], [], []),
    "props-rotation-label-unrounded": ([LP], ["props", "live"], [], []),
    "props-panel-rebuild-resets": ([LP], ["props"], [], []),
    "props-scale-clamp-inconsistency": ([SG], ["wheel"], [], []),
    "layers-model": ([(E, "ProjectIOTests"), LP], [], [], []),
    "layers-default-name-offbyone": ([LO, TO], ["draw"], [], []),
    "regions-click-fit-layer": ([(E, "DocumentOpsTests")], ["live"], [], []),
    "layers-select": ([(A, "LayersViewModelTests")], ["props"], [], []),
    "layers-reorder-drag": ([LE, (A, "LayersReorderTests")], ["reorder"], [], ["H7"]),
    "layers-reorder-drag-2": ([(A, "LayersReorderTests"), (A, "LayersPanelDragTests"), (A, "ShellTests")], ["reorder"], [], ["H6", "H7", "H7b"]),
    "layer-reorder-dragging-class-timer": ([(A, "LayersPanelDragTests")], ["reorder"], [], ["H7"]),
    "layers-list": ([(A, "LayersViewModelTests")], ["list"], [], ["H8"]),
    "home-brush-group": ([BR, CC], ["draw"], [], []),
    "tools-draw-props": ([BR, TH], ["brush"], [], []),
    "draw-brush-controls": ([BR], ["brush"], [], []),
    "undo-snapshot-contents": ([(A, "EditorSessionTests"), (E, "HistoryTests")], ["undo"], [], []),
    "region-front-highlight-lost": ([(A, "RegionsGroupTests")], [], [], ["H6"]),
    "shell-external-drop-guard": ([], [], [], ["H10"]),
    "fillstyle-color-pickers": ([(A, "ColorMathTests"), (A, "ColorPickerViewModelTests"), (A, "ColorPickerPopupTests"), (A, "FillStyleTests"), (A, "ShellTests")], ["color"], [], ["H10"]),
    "canvas-device-pixel-render": ([(E, "RendererViewTests")], [], [], ["H9"]),
    "cross-window-dpi-rendering": ([], [], [], ["H9"]),
    "wheel-scale-undo-burst": ([CI, TH], ["wheel"], [], ["H3"]),
    "eraser-click-without-move": ([ER, CI], ["eraser"], [], ["H5"]),
    "canvas-automation-peer": ([(A, "CanvasAutomationPeerTests")], [], [], []),
    "overlay-knob-glyph-rasterization": ([(E, "GoldenViewSelectTests")], [], SELECT, []),
}
assert len(ROWS) == 80, len(ROWS)
peer_ok = re.search(r'^\| 03-layer-selected \|[^\n]*\| "drip_red_wide_v1" \|', results, re.M) is not None
bad = 0
for row, (classes, scen, gold, hands) in ROWS.items():
    why = [f"{p}.{c} not run green" for p, c in classes if passed[(p, c)] == 0 or failed[(p, c)] > 0]
    why += [f"{SC}.{S[k]} did not pass" for k in scen if (SC, S[k]) not in methods]
    why += [f"golden {n} did not pass" for n in gold if n not in select_passed]
    if gold and not all(n in scenes for n in gold):
        why.append(f"golden scenes missing from tests/goldens/view-select: {[n for n in gold if n not in scenes]}")
    why += [f"hand check {h}: {hand.get(h, 'no result')}" for h in hands if hand.get(h) != "pass"]
    if row == "canvas-automation-peer" and not peer_ok:
        why.append('RESULTS.md state 03 does not read "drip_red_wide_v1"')
    bad += bool(why)
    print(("proven   " if not why else "OPEN     ") + row + ("" if not why else "  " + "; ".join(why)))
print(f"{len(ROWS)} rows, {len(ROWS) - bad} proven; {len(select_passed)} of {len(scenes)} view-select scenes passed; "
      f"{sum(1 for k in S if (SC, S[k]) in methods)} of {len(S)} scenarios passed; failures in any class: {sum(failed.values())}")
sys.exit(1 if bad else 0)
```

Run the whole suite at normal verbosity and feed the log, the hand checks and the look log to the gate:

```bash
dotnet test src/RBX15.slnx -c Release --no-build --logger "console;verbosity=normal" > .superpowers/logs/m3b-evidence.log 2>&1
python .superpowers/logs/m3b-evidence.py .superpowers/logs/m3b-evidence.log .superpowers/logs/m3b-hand.txt .superpowers/m3b-look/v5/RESULTS.md
```

Expected: `proven` for each of the 80 rows if every hand check passed (78 if H9 was not run: `canvas-device-pixel-render` and `cross-window-dpi-rendering` print `OPEN ... hand check H9: not run`), then `80 rows, P proven; 10 of 10 view-select scenes passed; 14 of 14 scenarios passed; failures in any class: 0`. An `OPEN` row prints why: a class that did not run green, a scenario or view-select scene that did not pass, a hand check that did not pass, or `RESULTS.md` missing the canvas value.

The gate proves the right classes ran green; it cannot prove each class pins the exact behavior. List the tests Tasks 1-4 wrote:

```bash
grep -n "public void\|public async Task" src/RBX15.Engine.Tests/SelectionGeometryTests.cs src/RBX15.Engine.Tests/LayerOpsEditTests.cs src/RBX15.Engine.Tests/RendererOverlayTests.cs src/RBX15.Engine.Tests/LivePreviewTests.cs src/RBX15.Engine.Tests/ViewRendererTests.cs src/RBX15.App.Tests/CanvasInteractionTests.cs src/RBX15.App.Tests/CanvasCursorsTests.cs src/RBX15.App.Tests/CanvasAutomationPeerTests.cs src/RBX15.App.Tests/LayerPropertiesViewModelTests.cs src/RBX15.App.Tests/LayersReorderTests.cs src/RBX15.App.Tests/LayersPanelDragTests.cs src/RBX15.App.Tests/TemplateCanvasHostTests.cs src/RBX15.App.Tests/BrushViewModelTests.cs src/RBX15.App.Tests/ToolsViewModelTests.cs
```

Write the test name next to each pin below, and note each pin with no test. `S1` to `S12` are Step 5's facts in file order (`S1` `Select_resize_rotate_and_move_*` to `S12` `Brush_tool_and_zoom_*`).

| Row | Behavior that must be pinned | Evidence | 4.1 source |
| --- | --- | --- | --- |
| `select-hit-test` | points on the edges hit; rotated 30° and 90° boxes; hidden skipped; overlap picks the topmost | `LayerOpsTests` (M3a), `SelectionGeometryTests`, S3 | `editor.js:1041-1053`, `:1100-1116` |
| `select-hit-test-rect-not-alpha` | a transparent box over an opaque layer wins the pick | S3 | `editor.js:1043-1053` |
| `select-rotate-handle-hit` | knob position to 1e-9 for given layer and zoom; 13.9 hits, 14.1 misses | `SelectionGeometryTests` | `editor.js:1029-1039`, `:1073` |
| `select-resize-handle-hit` | 9.9 hits and 10.1 misses at each rotated corner | `SelectionGeometryTests` | `editor.js:1078-1097` |
| `select-resize-drag`, `select-resize-handles` | distance x1.5 gives scale x1.5; doubling doubles; floor 0.01 and cap 5; center fixed to 1e-9; one step | `SelectionGeometryTests`, S1 | `editor.js:1181-1190` |
| `select-rotate-drag`, `select-rotate-drag-2` | 44.2 and 47.9 snap to 45 and 48.1 does not; the 1° sweep with plateaus; normalized across ±180 | `SelectionGeometryTests`, S1 | `editor.js:1172-1182` |
| `select-move-hit-and-drag`, `select-move` | 50x30 frame px at zoom 2 moves 25x15; one step; capture past the edge | S1, `TemplateCanvasHostTests`, H1 | `editor.js:1100-1113`, `:1169-1171` |
| `draw-begin`, `draw-tool-start`, `draw-pen-single-click`, `draw-empty-no-layer` | no step at the press; shape clicks draw nothing; a pen click is a dot | S4, `CanvasInteractionTests`, `ToolsTests` | `editor.js:1120-1146`, `:1253-1264` |
| `draw-commit-layer`, `layers-add-drawing`, `layers-default-name-offbyone` | one layer, named by `Tools.DrawingName`, selected, topmost; the tool kept; crop bounds | S4, `ToolsTests`, `GoldenAppTests` shape-* | `editor.js:528-540`, `:1285` |
| `eraser-apply`, `eraser-selection-semantics`, `eraser-click-without-move` | refusal text at the press; selected layer only, hidden too; a click is nothing | S5, `EraserTests`, `CanvasInteractionTests`, `GoldenAppTests` erase-* | `editor.js:1133-1144`, `:1265-1283` |
| `canvas-draw-tools` | all of the above for every tool | S4, S5, the goldens | `editor.js:1120-1294` |
| `draw-preview-composite`, `render-live-preview` | shape and eraser previews against 4.1; eraser at 0.5 alpha; a gutter pixel masked with Mask on | `GoldenViewSelectTests` vs-preview-*, `RendererOverlayTests`, `LivePreviewTests` | `editor.js:324-337` |
| `render-selection-box`, `render-resize-handles`, `render-rotation-handle`, `select-handle-render` | the overlay against 4.1 at zoom 0.5-2, rotation 0, 30 and -135, flipped, over the Pants grid, the label blanked | `GoldenViewSelectTests` vs-select-* | `editor.js:361-414` |
| `render-rotation-label` | `17.3°`, `-17.3°`, `-0.0°`, none at 0; JetBrains Mono 400 11 px #f22f89 at (0, -h/2 - 46) | `SelectionGeometryTests`, `RendererOverlayTests` | `editor.js:415-421` |
| `select-hidden-layer-still-handled`, `select-rotate-handle` | a hidden selected layer renders as no selection; its handles are dead | S3, `RendererOverlayTests`, `SelectionGeometryTests` | `editor.js:361-363`, `:1066-1067` |
| `select-deselect-click`, `layers-empty-state` | an empty press deselects; the panel shows `Select a layer to edit.` | S1, S8, `LayerPropertiesViewModelTests` | `editor.js:747-752`, `:1114-1117` |
| `select-hover-and-end`, `draw-release-outside-canvas` | capture loss commits at the last point; a stray release does nothing | S6, `CanvasInteractionTests`, `TemplateCanvasHostTests`, H1, H4 | `editor.js:1239-1246`, `:1296-1302` |
| `canvas-select-gestures` | the scripted sequences, each one step | S1, S2, `CanvasInteractionTests`, H1-H3 | `editor.js:1015-1320` |
| `canvas-coord-map`, `canvas-mouse-button-agnostic` | `/ zoom` mapping; right and middle presses unhandled | `CanvasCoordinatesTests` (M3a), `TemplateCanvasHostTests`, H4 | `editor.js:1058-1061`, `:1149-1152` |
| `cursor-default`, `canvas-cursor-states`, `select-hover-idle` | hover and drag cursor kinds per tool and handle | `CanvasInteractionTests`, `CanvasCursorsTests`, H2 | `editor.js:456-459`, `:1155-1167` |
| `tools-cursor`, `cursor-eraser`, `eraser-cursor`, `render-restore-and-cursor` | ring radius `max(4, size x zoom / 2)` DIPs at device scale, the 256-px cap, centered hotspot; rebuilt on zoom and size | `CanvasCursorsTests`, `TemplateCanvasHostTests`, H5 | `editor.js:440-459` |
| `tools-set-tool`, `tools-auto-select-switch` | one active button; Select at launch; import and list pick revert; drawing and undo keep the tool | S11, `ToolsViewModelTests` | `editor.js:464-490`, `:523`, `:577` |
| `wheel-scale-layer`, `props-scale-clamp-inconsistency`, `wheel-scale-undo-burst` | centered symmetric steps, 1-500%, one step per burst, the closes | S2, `SelectionGeometryTests`, `CanvasInteractionTests`, `TemplateCanvasHostTests`, H3 | `editor.js:1305-1313` |
| `wheel-brush-size`, `wheel-sign-only-and-midstroke` | 4 -> 24 -> 50 -> 1; one step per event by sign; a live shape takes the new width | S7, `ToolsTests`, `BrushViewModelTests`, H3 | `editor.js:1314-1319` |
| `home-brush-group`, `tools-draw-props`, `draw-brush-controls` | defaults; filled Rect in the brush color; ring at size 50 | S4, S7, `BrushViewModelTests`, `CanvasCursorsTests` | `editor.html:53-60`, `editor.js:90-92`, `:492-494` |
| `props-name-xy`, `props-scale`, `props-rotation`, `props-opacity`, `props-flip` | each edit one step; X 12.7 shows 13; slider anchor top-left; clamp; flips | S8, `LayerPropertiesViewModelTests`, `LayerOpsEditTests`, `GoldenRenderTests` | `editor.js:752-844` |
| `props-adjust-panel`, `props-adjust-reset`, `props-falsy-zero-adjust-display`, `props-panel-rebuild-resets`, `props-rotation-label-unrounded` | stays open; Reset one step or none; 0 shows 0; whole-degree label | S8, S9, `LayerPropertiesViewModelTests` | `editor.js:779-913` |
| `props-live-refresh`, `regions-click-fit-layer` | live rotation and X/Y mid-drag; the panel after a fit | S9, H2 | `editor.js:1191-1192`, `:1658-1667` |
| `layers-model` | v4 project fields round-trip; 0 adjustments display 0 | `ProjectIOTests` (M2b), `LayerPropertiesViewModelTests` | `editor.js:134-142`, `:499-519` |
| `layers-select`, `layers-list` | a list pick selects and fills the panel; the M3a list behaviors; the 40% dim | S8, `LayersViewModelTests`, H8 | `editor.js:575-580`, `:671-744` |
| `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer` | `DropIndices` over every combination; one step; stale target cleared; indicators; Alt+Up | S10, `LayerOpsEditTests`, `LayersReorderTests`, `LayersPanelDragTests`, H7 | `editor.js:584-666` |
| `undo-snapshot-contents` | document and selection only; brush, tool and zoom survive undo | S12, `EditorSessionTests`, `HistoryTests` | `editor.js:1404-1415` |
| `region-front-highlight-lost`, `shell-external-drop-guard`, `fillstyle-color-pickers`, `canvas-device-pixel-render`, `cross-window-dpi-rendering` | the hand-checked halves | H6, H10, H9 and the M3a classes | as each row cites |
| `canvas-automation-peer` | Image, `Template canvas`, bounds, read-only value, change event; the live read | `CanvasAutomationPeerTests`, `RESULTS.md` | `editor.html:327` |
| `overlay-knob-glyph-rasterization` | the knob exact in the six select scenes at their offsets | `GoldenViewSelectTests` vs-select-* | `editor.js:395-414` |
| `select-rotate-handle-hit`, `select-rotate-handle` (Ctrl+Z half of `layers-reorder-drag-2`, `fillstyle-color-pickers`) | the knob hit where it is drawn at 0, 30, 90 and 180 degrees; the Ctrl+Z KeyBinding runs the Edit group's Undo | `SelectionGeometryTests`, `RendererOverlayTests`, `ShellTests`, H6 | `editor.js:372`, `:396`, `:1029-1039`, `:1797` |

Then apply the gate:

- **An `OPEN` row, a pin with no test, or a failed hand check for that row:** take the row out of Step 13's ids and add it, with the reason, to the "Left open" list of Step 15's commit body. Report it to the controller. Do not write the missing test here; the gap belongs to the task that owns the module.
- **Everything else** closes in Step 13.

- [ ] **Step 13: Amend, reword, add, narrow and close**

Save the ledger edit as `.superpowers/logs/m3b-ledger.py`. It refuses to run on a ledger that moved since planning:

```python
"""M3b Task 5: amend eraser-cursor and cross-text-select-and-selection to fix, reword four divergence texts,
append the M3b handoffs to six more rows, add four rows, and narrow the parityCheck of the rows being closed. Usage: python m3b-ledger.py <parity-ledger.json> <id to close>...
Run once, before `ledger.py done` with the same ids. Fails loudly if any row moved since planning."""
import json, sys

path, closing = sys.argv[1], set(sys.argv[2:])
data = json.load(open(path, encoding="utf-8"))
rows = {r["id"]: r for r in data["rows"]}
assert len(data["rows"]) == 413, f"expected 413 rows, found {len(data['rows'])}"
NEW_IDS = {"wheel-scale-undo-burst", "eraser-click-without-move", "canvas-automation-peer", "overlay-knob-glyph-rasterization"}
assert not (NEW_IDS & rows.keys()), "the M3b rows were already added"

# 1. eraser-cursor (keep) says "capped at diameter 240"; cursor-eraser (fix) drops 4.1's cap. Amend it to fix.
r = rows["eraser-cursor"]
assert (r["disposition"], r["divergence"], r["status"]) == ("keep", "", "open"), "eraser-cursor was already amended"
r["disposition"] = "fix"
r["divergence"] = ("4.1: the ring radius is max(4, min(120, drawSize x zoom / 2)) CSS px, an SVG cursor that is not redrawn "
                   "on zoom (editor.js:443-454). v5: the ring is the true brush footprint, max(4, size x zoom / 2) DIPs, drawn "
                   "at device pixels and redrawn on zoom, tool and size changes; the Win32 cursor-size cap is stated on "
                   "cursor-eraser.")

# 1b. cross-text-select-and-selection (keep) gains a deliberate change in M3b (Task 4 T4-R10): WPF shows a control's
# tooltip on keyboard focus. A keep row carries no divergence, so it is amended to fix and stays open (M4).
r = rows["cross-text-select-and-selection"]
assert (r["disposition"], r["divergence"], r["status"]) == ("keep", "", "open"), "cross-text-select-and-selection was already amended"
r["disposition"] = "fix"
r["divergence"] = ("4.1: tooltips are native title= attributes, shown on mouse hover only. v5: a control's tooltip also shows "
                   "while the control has keyboard focus and stays until focus moves, Esc or Ctrl (WPF's default, WCAG "
                   "1.4.13), so keyboard users read the same text; the tool tooltips drop their shortcut letters until "
                   "M4's keys exist (measured M3b: the 52x26 and 162x26 windows under Color 1 and Add image).")

# 2. Divergence texts reworded to what M3b ships (each row stays a fix row; the old text must match).
REWORD = {
    "cursor-eraser": (
        "4.1: eraser ring radius is capped at 120 so it under-represents the real footprint at large size and zoom, and is off-center by under 1px. v5: ring drawn at the true brush footprint, centered (clearly unintended mismatch).",
        "4.1: eraser ring radius is capped at 120 so it under-represents the real footprint at large size and zoom, and is off-center by under 1px. v5: ring drawn at the true brush footprint, radius max(4, size x zoom / 2) DIPs, centered, at device pixels (clearly unintended mismatch). A Win32 cursor bitmap is at most 256x256 device px, so the radius is capped at 128 - ceil(2 x display scale) device px (126 at 100%, 124 at 200%), which binds only above a 124 DIP ring diameter at 200%.",
    ),
    "select-rotate-handle-hit": (
        "4.1: rotation handle stays hittable for a hidden selected layer. v5: hidden layers have no handles, so no handle hit.",
        "4.1: the rotation handle stays hittable for a hidden selected layer, and is hit-tested at the knob's mirror image across the layer's vertical axis (getRotHandlePos uses c.x - sin r x d, editor.js:1036), so a rotated layer's visible knob does not rotate it. v5: hidden layers have no handles, and the handle is hit where it is drawn.",
    ),
    "wheel-sign-only-and-midstroke": (
        "4.1: wheel scaling uses delta sign with asymmetric x1.05/x0.95 steps, no max clamp, and changes width mid-stroke. v5: wheel scaling centered with symmetric steps under the single 1%-500% clamp.",
        "4.1: wheel scaling uses delta sign with asymmetric x1.05/x0.95 steps, no max clamp, and changes width mid-stroke. v5: wheel scaling centered with symmetric steps (x1.05 up, /1.05 down) under the single 1%-500% clamp. The wheel still counts one step per event by its sign, and a step mid-shape still redraws a Line, Rect or Ellipse at the new brush width (kept); Pen and Eraser keep the width of the press, as in 4.1.",
    ),
    "render-zoom-canvas": (
        "4.1: the canvas is reallocated and cleared on every render and clipped when it outgrows the area. v5: the frame keeps 4.1's truncated size (TW*zoom, editor.js:340-341), renders into a reused bitmap, and sits in a scroll host with scrollbars to pan; zoom stays 25-300% in 0.25 steps.",
        "4.1: the canvas is reallocated and cleared on every render and clipped when it outgrows the area. v5: the frame keeps 4.1's truncated size (TW*zoom, editor.js:340-341), renders through one persistent offscreen surface and checker tile into a reused bitmap, at most once per display frame while dragging, and sits in a scroll host with scrollbars to pan; zoom stays 25-300% in 0.25 steps.",
    ),
}
for rid, (old, new) in REWORD.items():
    r = rows[rid]
    assert (r["disposition"], r["status"]) == ("fix", "open") and r["divergence"] == old, f"{rid} divergence moved since planning"
    r["divergence"] = new

# 2b. The handoffs Tasks 1-4 recorded: one sentence appended to a divergence (fix rows only), and one behavior
# correction. Each asserts the row has not moved and the sentence is not there yet.
APPEND = {
    "layers-reorder-drag-2": "Pressing a row selects it, so a dragged row ends selected; Alt+Up and Alt+Down move a focused row (M3b Task 4).",
    "canvas-cursor-states": "Grab and grabbing show the Windows Hand cursor (WPF has no stock grab cursor); provisional, M3b look sign-off.",
    "props-name-xy": "Text that is not a finite number below 1e21 is refused and the field reverts (4.1's +value stored Infinity for 1e400).",
    "panel-drop-zone": "The label wraps in the narrow 200 px panel instead of clipping (M3b Task 4).",
}
for rid, text in APPEND.items():
    r = rows[rid]
    assert (r["disposition"], r["status"]) == ("fix", "open") and r["divergence"].startswith("4.1:") and text not in r["divergence"], f"{rid} moved since planning"
    r["divergence"] = r["divergence"] + " " + text
r = rows["ribbon-overflow-hscroll"]
assert r["status"] == "open" and "At 1200 px width Home fits." in r["behavior"], "ribbon-overflow-hscroll moved since planning"
r["behavior"] = r["behavior"].replace("At 1200 px width Home fits.", "Home overflows a 1200 px window too (pane 1432 px in a 1184 px client, measured M3b).")

# 3. New rows.
NEW = [
    {
        "id": "wheel-scale-undo-burst",
        "area": "canvas-input",
        "name": "A burst of wheel scaling is one undo step",
        "source": "gap:m3b-canvas",
        "behavior": "4.1's wheel scales the selected layer with no undo step at all (editor.js:1305-1313; undo-coverage-gaps), so a wheel change can be undone only together with the action before it.",
        "codeRefs": ["editor.js:1305-1313"],
        "quirks": "",
        "portTarget": "CanvasInteraction opens one EditorSession gesture at the first wheel scale event of a burst; EndWheelBurst closes it, called by TemplateCanvasHost's 400 ms one-shot timer after the last wheel event, and by a press, a tool change, a selection change or any other session change (EditorSession.Changing, raised first by every change entry point). The gesture records one step iff the content changed.",
        "risk": "low",
        "parityCheck": "LedgerScenarioTests.A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step: three wheel steps are one undo step, two hundred more are one more, clamped at 500%. CanvasInteractionTests and TemplateCanvasHostTests pin the closes (the 400 ms timer, a press, a tool change, a selection change, and an edit or slider gesture made during the burst, which records as its own step). By hand in the M3b manual checklist (H3): a burst on a real wheel undoes in one step.",
        "owner": "App.Canvas",
        "check": "unit",
        "disposition": "fix",
        "divergence": "4.1: wheel scaling records no undo step. v5: each burst of wheel steps is one undo step; a burst ends 400 ms after its last wheel event, or at a press, a tool change, a selection change or any other edit, which stays its own step.",
        "status": "open",
    },
    {
        "id": "eraser-click-without-move",
        "area": "draw-tools",
        "name": "An eraser click without movement erases nothing",
        "source": "gap:m3b-canvas",
        "behavior": "4.1's eraser only moves to the press point on mousedown (editor.js:1143-1144) and strokes at mouseup only for the pen (editor.js:1253-1255), so a click without movement leaves no alpha on the stroke canvas and erases nothing, with no undo step.",
        "codeRefs": ["editor.js:1133-1144", "editor.js:1249-1283"],
        "quirks": "The approved pen dot (draw-pen-single-click) is not carried to the eraser: a click is not a destructive punch-out.",
        "portTarget": "Eraser.Apply returns the layer itself for fewer than 2 points and Eraser.ApplyTo returns the same document, so EditorSession records no step; the live preview shows nothing for one point.",
        "risk": "low",
        "parityCheck": "EraserTests.A_press_without_movement_erases_nothing; CanvasInteractionTests.Eraser_click_without_move_changes_nothing_and_records_no_step; LedgerScenarioTests.The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden. By hand in the M3b manual checklist (H5).",
        "owner": "Engine.Tools",
        "check": "unit",
        "disposition": "keep",
        "divergence": "",
        "status": "open",
    },
    {
        "id": "canvas-automation-peer",
        "area": "canvas",
        "name": "The canvas is exposed to UI Automation",
        "source": "gap:m3b-a11y",
        "behavior": "4.1's <canvas id=mainCanvas> has no accessible name, role or value (editor.html:327), so a screen reader skips it and cannot tell which layer is selected.",
        "codeRefs": ["editor.html:327"],
        "quirks": "",
        "portTarget": "TemplateCanvasHostAutomationPeer (FrameworkElementAutomationPeer, IValueProvider): ControlType Image, ClassName TemplateCanvasHost, Name 'Template canvas', bounds from the element, not keyboard focusable, Value = the selected layer's name or '', read-only, with a ValueProperty change event on selection change.",
        "risk": "low",
        "parityCheck": "CanvasAutomationPeerTests pin the control type, name, bounds, read-only value and its change event. The M3b look run reads the value through UI Automation: state 03 reads \"drip_red_wide_v1\" (.superpowers/m3b-look/v5/RESULTS.md).",
        "owner": "App.Canvas",
        "check": "unit",
        "disposition": "fix",
        "divergence": "4.1: the canvas is invisible to screen readers. v5: the canvas exposes its name, its bounds and the selected layer's name as a read-only value.",
        "status": "open",
    },
    {
        "id": "overlay-knob-glyph-rasterization",
        "area": "render",
        "name": "The rotate knob's glyph antialiasing differs at some offsets",
        "source": "gap:m3b-render",
        "behavior": "4.1 strokes the rotate knob's 1.5 px white glyph arc and arrowhead with Chromium's Skia (editor.js:403-414), at the knob offset h/2 + 30 screen px above the layer's center.",
        "codeRefs": ["editor.js:395-414"],
        "quirks": "Measured 2026-10-15 (M3b Task 1 ruling T1-3), 100 device positions per offset against Chromium 146: offsets 0, 10, 20, 30, 63, 67.5, 70, 80, 100, 129 and 130 screen px are exact; offsets 5 and 35 to 60 differ on up to about 14 glyph px by up to 41 levels. No construction reproduces Chromium at every offset: float rounding inside two Skia builds, not geometry.",
        "portTarget": "Renderer.DrawSelectionOverlay draws the knob with 4.1's call structure, arc angles as given (ruling T1-2); the view-select goldens use offsets that measure exact.",
        "risk": "low",
        "parityCheck": "GoldenViewSelectTests: the six vs-select-* scenes (knob offsets 67.5, 130, 67.5, 86.25 and 75) match 4.1's view under the unchanged golden rule with the knob exact. The residual at other offsets is the measured quirk above, compared by eye in the M3b look sign-off (state 03).",
        "owner": "Engine.Render",
        "check": "golden",
        "disposition": "fix",
        "divergence": "4.1: Chromium 146's Skia antialiases the knob's 1.5 px white glyph. v5: SkiaSharp 4.153.1 antialiases it differently at some knob offsets (5 and 35 to 60 screen px: up to 41 levels on about 14 px); the geometry is 4.1's call for call, and the view-select goldens hold it exact at the offsets they use.",
        "status": "open",
    },
]
data["rows"].extend(NEW)
rows.update({r["id"]: r for r in NEW})

# 4. parityCheck narrowed to the evidence used (ledger honesty rule), applied only to rows being closed.
SEAM = "Narrowed in M3b: the gesture runs through CanvasInteraction and TemplateCanvasHost's pointer seams in unit tests, because WPF ignores posted mouse input and UI automation lands with M6; the real mouse is checked by hand in the M3b manual checklist"
NARROW = {
    "select-rotate-handle-hit": "SelectionGeometryTests: the knob is at the center plus R(rotation) x (0, -(h/2 + 30/zoom)), where the overlay draws it, to 1e-9 at rotation 0 for zoom 0.5, 1 and 2 and at 30, 90 and 180 for zoom 1 (4.1's getRotHandlePos agrees at 0 and 180 only); a press 13.9 screen px from it hits and 14.1 misses; a hidden selected layer has no handle hit (SelectionGeometryTests, RendererOverlayTests.The_rotate_handle_is_hit_where_the_overlay_draws_it, LedgerScenarioTests). Reworded in M3b: the mirrored 4.1 hit point is the fixed behavior this row's divergence states.",
    "select-rotate-handle": "SelectionGeometryTests and RendererOverlayTests: the knob sits where the overlay draws it at rotation 0 (zoom 0.5, 1, 2) and 30, 90, 180 (zoom 1); hit boundary 13.9 vs 14.1 screen px; no handle on a hidden selected layer. Narrowed in M3b: zoom 4 is outside v5's 25-300% range, and 30 and 90 stand in for 45.",
    "draw-empty-no-layer": "A click without movement with Line, Rect, Ellipse or the Eraser leaves the layer count and the undo count unchanged (CanvasInteractionTests.A_shape_click_draws_nothing_and_records_nothing, Eraser_click_without_move_changes_nothing_and_records_no_step; LedgerScenarioTests S4, S5); a stroke that leaves no pixels adds no layer (Tools.CommitDrawing returns null). Reworded in M3b: a Pen click draws a brush-size dot (approved divergence draw-pen-single-click).",
    "select-rotate-drag-2": "SelectionGeometryTests sweeps the pointer around the center in 1-degree steps and compares each rotation with editor.js:1176-1182's formula, normalized to -180..180, including the snap plateaus within 3 degrees of each multiple of 45 and the crossing of +-180. Narrowed in M3b: the sequence is checked against the ported formula, not a recorded 4.1 run.",
    "select-move-hit-and-drag": f"Overlapping layers: a press selects the topmost visible box; a transparent area of a PNG still selects; hidden layers are skipped (LayerOpsTests, SelectionGeometryTests, LedgerScenarioTests). A 50x30 px drag at zoom 2 moves the layer 25x15 template px, as one undo step, and the selection survives undo (LedgerScenarioTests, TemplateCanvasHostTests). {SEAM} (H1).",
    "draw-begin": "Reworded in M3b to the approved divergence: a Line, Rect and Ellipse stroke with identical points renders as 4.1 renders it (GoldenAppTests shape-* scenes, M2a); a pen stroke is the whole path stroked once and a pen press is a brush-size dot (ToolsTests, LedgerScenarioTests); no press records an undo step (CanvasInteractionTests, LedgerScenarioTests).",
    "draw-tool-start": "A press records no undo step and a click with Line, Rect or Ellipse adds nothing (CanvasInteractionTests, LedgerScenarioTests). Shape pixels: GoldenAppTests shape-* scenes (M2a). Reworded in M3b: 4.1's consumed history entry is the fixed behavior, and the pen pixels follow the approved draw-pen-incremental-stroke divergence.",
    "draw-pen-single-click": "Line, Rect and Ellipse: a click without movement adds no layer and no undo step. Pen: a click adds one layer, a brush-size dot. Eraser: a click erases nothing (eraser-click-without-move). LedgerScenarioTests, CanvasInteractionTests. Reworded in M3b to the approved divergence (a pen click draws a dot).",
    "draw-commit-layer": "After a stroke the layer count is +1, the new layer is selected and topmost, it is named per 4.1 (Tools.DrawingName) and the tool is unchanged (LedgerScenarioTests, CanvasInteractionTests); its origin, size and pixels are Tools.CommitDrawing's, held to 4.1 by GoldenAppTests shape-* scenes (M2a).",
    "eraser-apply": "GoldenAppTests erase-* scenes (M2a) on scaled, rotated and flipped layers against 4.1's frozen exports; EraserTests for the single-stroke production path; LedgerScenarioTests: the eraser acts on the selected layer only, hidden or not, as one undo step. Narrowed in M3b: the production path's fringe divergence is the one this row states (M2a Task 6 measurement).",
    "canvas-draw-tools": "Shapes: GoldenAppTests shape-* scenes (M2a); eraser: GoldenAppTests erase-* scenes and EraserTests; pen: the approved divergence (ToolsTests). The eraser refuses at the press with a status message and no undo step is empty (LedgerScenarioTests, CanvasInteractionTests). Narrowed in M3b: pen strokes are not compared with 4.1 (approved divergence).",
    "draw-preview-composite": "GoldenViewSelectTests: the live preview of a rect, a filled ellipse, a line and a two-point eraser stroke mid-gesture, at zoom 0.5, 1 and 2, with Mask on, match 4.1's view under the golden rule; the eraser preview is magenta at half alpha (RendererOverlayTests, LivePreviewTests).",
    "render-live-preview": "GoldenViewSelectTests vs-preview-* scenes hold the shape and eraser previews to 4.1 with Mask on; RendererOverlayTests pins that a preview pixel outside every region is masked when Mask is on and shown when it is off, and that the eraser preview draws at half alpha. Narrowed in M3b: pen previews are not frozen (approved divergence draw-pen-incremental-stroke), so the gutter clause is checked on a shape preview.",
    "tools-set-tool": "ToolsViewModelTests and LedgerScenarioTests: each tool button makes its tool current and only that button active; Select is active at launch. The active look is compared side by side in the M3b look sign-off (state 04). Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "tools-auto-select-switch": "LedgerScenarioTests.Tool_buttons_toggle_off_to_select_and_an_import_or_a_list_pick_returns_to_select: with Pen active an import returns to Select, a pen stroke keeps Pen, a pick in the layer list returns to Select, and undo leaves the tool alone (ImportService.LayersAdded and LayersViewModel.LayerPicked through Composition). Narrowed in M3b: the import is a file drop through LayersViewModel, not a drag from Explorer.",
    "tools-cursor": "CanvasCursorsTests: the eraser ring radius is max(4, size x zoom / 2) DIPs at sizes 1, 4 and 50 and zoom 0.5 and 2, rendered at device scale and capped by the Win32 cursor size (cursor-eraser); Pen, Line, Rect and Ellipse use the crosshair; the cursor is rebuilt when zoom, tool or size changes (TemplateCanvasHostTests). By hand in the M3b manual checklist (H5). Reworded in M3b: 4.1's 4..120 clamp is the fixed behavior, and brush sizes stop at 50.",
    "cursor-eraser": "CanvasCursorsTests: ring diameter = size x zoom DIPs for size 4 and 20 at zoom 1 and 2, minimum radius 4, hotspot at the center, the 256-px Win32 cap. By hand in the M3b manual checklist (H5). Reworded in M3b: 4.1's 240 px clamp is the fixed behavior; size 100 and zoom 4 are outside v5's ranges (brush 1-50, zoom 25-300%).",
    "eraser-cursor": "CanvasCursorsTests: the ring diameter equals size x zoom DIPs at zoom 0.25 and 3, with no 240 cap (amended to fix in M3b). By hand in the M3b manual checklist (H5).",
    "cursor-default": "CanvasInteractionTests: idle hover gives the crosshair for every tool but the eraser, and for Select gives Grab on the knob, ResizeNwse on a corner and Move over a visible layer box. By hand in the M3b manual checklist (H2, H5). Narrowed in M3b: the cursor kinds are asserted on the interaction, the WPF cursors by CanvasCursorsTests.",
    "canvas-cursor-states": f"CanvasInteractionTests: Grab on the knob, Grabbing for the whole rotate drag, ResizeNwse for a resize, Move for a move, held until the release. {SEAM} (H2).",
    "select-hover-idle": f"CanvasInteractionTests: idle hover over the knob, a corner and the body gives Grab, ResizeNwse and Move, else the crosshair; a hidden selected layer gives no handle cursor. {SEAM} (H2).",
    "render-restore-and-cursor": "TemplateCanvasHostTests and CanvasCursorsTests: with the eraser active, a brush size change and a zoom change each rebuild the ring at the new size before the next frame. By hand in the M3b manual checklist (H5). Narrowed in M3b: the cursor is set by a binding, not by the render tail.",
    "render-selection-box": "GoldenViewSelectTests vs-select-z100 and vs-select-rot30-z100: the dashed box, its dash phase from (-w/2, -h/2) clockwise and the handles match 4.1's view under the golden rule; the rotation label's box is blanked in both images (interfaces R4). RendererOverlayTests: a hidden selected layer draws nothing. Narrowed in M3b: the label is pinned by unit tests, not pixels.",
    "render-resize-handles": "GoldenViewSelectTests vs-select-z100 and vs-select-z200: the 8 px corner handles stay 8 px at zoom 1 and 2. Narrowed in M3b: zoom 4 is outside v5's 25-300% range.",
    "render-rotation-handle": "GoldenViewSelectTests at rotation 0, 30 and -135 (zoom 1 and 1.5): stem, knob and glyph match 4.1's view under the golden rule. Narrowed in M3b: rotations 90 and 180 are covered by RendererOverlayTests' geometry pins, not frozen pixels.",
    "render-rotation-label": "SelectionGeometryTests: 17.25 gives '17.3°', -17.25 gives '-17.3°', -0.04 gives '-0.0°', 0 gives no label (JS toFixed(1)). RendererOverlayTests: the face is the vendored JetBrains Mono 400 at 11 px, #f22f89, centered at (0, -h/2 - 46) in the rotated frame, with no label pixel outside SelectionGeometry.LabelBounds. Compared side by side in the M3b look sign-off (state 03). Narrowed in M3b: the label is excluded from the golden comparison (interfaces R4).",
    "select-handle-render": "GoldenViewSelectTests at zoom 1, 1.5 and 2, rotation 0, 30 and -135, flipped and over the Pants grid: boxes and handles in identical positions under the golden rule, the label blanked (interfaces R4). Narrowed in M3b: rotation 30 and -135 stand in for 37.5.",
    "select-hidden-layer-still-handled": "RendererOverlayTests and LedgerScenarioTests: a hidden selected layer renders exactly as with no selection (no box, handles, knob or label), and a press on its knob or corner falls through to the pick, which skips hidden layers. Narrowed in M3b: the knob drag is checked as a press that does not rotate, not as a screenshot.",
    "select-move": f"A 50x30 px drag at zoom 2 moves the layer 25x15 template px, as one undo step (LedgerScenarioTests); the drag keeps the left-button capture past the canvas edge (TemplateCanvasHostTests). {SEAM} (H1).",
    "select-deselect-click": "A press on empty canvas clears the selection, the properties panel shows its empty state and the view draws no overlay, with no undo step (LedgerScenarioTests, RendererOverlayTests). Narrowed in M3b: view-model and render evidence, not UI automation (M6).",
    "select-hover-and-end": f"A move drag continues under mouse capture past the canvas edge, a lost capture ends it as a release at the last point (one step), and a release after a cancel changes nothing (TemplateCanvasHostTests, CanvasInteractionTests, LedgerScenarioTests). Reworded in M3b to the fixed behavior: 4.1 stopped at the edge. {SEAM} (H1).",
    "canvas-select-gestures": f"Scripted pointer sequences through CanvasInteraction: a move by (30,20) at zoom 1 and 2, a rotation across 45 that snaps, a corner drag whose scale ratio equals the distance ratio with the center fixed, the wheel at the 500% clamp, each one undo step (LedgerScenarioTests, CanvasInteractionTests, SelectionGeometryTests). {SEAM} (H1-H3).",
    "canvas-coord-map": "CanvasCoordinatesTests (M3a): template = position in the frame / zoom at zoom 0.25, 1 and 3 and device scale 1, 1.25 and 1.5, to 1e-9, matching editor.js:1059-1061's formula. TemplateCanvasHostTests: only the left button presses, and the press captures the mouse. Narrowed in M3b: compared with the ported formula, not a logged Electron run.",
    "canvas-mouse-button-agnostic": "TemplateCanvasHostTests: a right or middle press is not handled and starts nothing. By hand in the M3b manual checklist (H4): a right-drag with Pen draws nothing.",
    "draw-release-outside-canvas": f"A stroke whose capture is lost (a release outside the window, Alt+Tab) is committed at its last point as one step; under capture a release outside the canvas commits too (CanvasInteractionTests, TemplateCanvasHostTests, LedgerScenarioTests). {SEAM} (H4).",
    "wheel-scale-layer": "Reworded in M3b to the fixed behavior: 10 down-steps then 10 up-steps return the scale to s within 1e-12 and keep the center, and the scale clamps at 1% and 500% (SelectionGeometryTests, LedgerScenarioTests). By hand in the M3b manual checklist (H3) on a real wheel.",
    "wheel-brush-size": "From 4, 20 wheel-up steps give 24 and 60 more give 50 (clamped); wheel-down clamps at 1 (ToolsTests.StepBrushSize, BrushViewModelTests, LedgerScenarioTests).",
    "wheel-sign-only-and-midstroke": "A wheel event is one step by its sign (CanvasInteractionTests, TemplateCanvasHostTests); a wheel step during a Rect drag redraws the live shape and the committed layer at the new width (LedgerScenarioTests). By hand in the M3b manual checklist (H3): one notch on a mouse and a touchpad flick each step by event count.",
    "eraser-selection-semantics": "Eraser with no selection: the press shows the status message 'Select a layer to erase from, then try again.' (editor.js:1270), starts no stroke and changes nothing (LedgerScenarioTests, CanvasInteractionTests). Reworded in M3b to the fixed behavior: a status message at the press replaces 4.1's alert after the stroke.",
    "layers-empty-state": "LayerPropertiesViewModelTests and LedgerScenarioTests: with no selection the panel shows 'Select a layer to edit.'; a press on empty canvas returns to it. Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "props-name-xy": "LayerPropertiesViewModelTests and LedgerScenarioTests: X '12.7' stores 12.7 and shows '13'; an unparsable entry reverts; a name change shows in the list; X and Y follow a move drag live; each committed edit is one undo step. Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "props-scale": "LayerPropertiesViewModelTests and LedgerScenarioTests: the slider keeps the top-left corner, clamps to 1-500% and each change is one undo step (a drag is one); scale 1 renders at natural size (GoldenRenderTests, M1). Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "props-rotation": "LayerPropertiesViewModelTests: the slider stores the rotation normalized to -180..180, the label is whole degrees, and a drag is one undo step. Rotated pixels are held to 4.1 by GoldenRenderTests at 30, -135, 90 and 15 degrees (M1) and GoldenViewSelectTests vs-select-rot30-z100. Narrowed in M3b: 30 degrees stands in for 45.",
    "props-opacity": "LayerPropertiesViewModelTests and LedgerScenarioTests: 50% stores 0.5 and is one undo step; GoldenRenderTests gap-opacity-with-adjust (M1) holds a 0.5-opacity layer to 4.1's export.",
    "props-flip": "LayerOpsEditTests: Flip H and Flip V toggle and are one undo step each; the flipped pixels are held to 4.1 by GoldenRenderTests layer-rotate-scale-fliph-opacity and layer-rotate-neg-flipv-upscale (M1) and GoldenViewSelectTests vs-select-fliph-z50.",
    "props-adjust-panel": "LayerPropertiesViewModelTests and LedgerScenarioTests: Image adjustments stays open across a selection change, every edit and Reset (the documented fix). Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "props-adjust-reset": "LayerPropertiesViewModelTests and LedgerScenarioTests: Reset restores LayerAdjustments.Default as one undo step and none when already at the defaults, the panel stays open, and the render equals the unadjusted layer (LayerOpsEditTests).",
    "props-live-refresh": f"LedgerScenarioTests and LayerPropertiesViewModelTests: during a rotate drag the rotation slider and label track each move, X and Y track a move drag, and a region fit refreshes scale and position, with no rebuild. {SEAM} (H2).",
    "props-falsy-zero-adjust-display": "LayerPropertiesViewModelTests and LedgerScenarioTests: brightness 0, then another selection and back: the slider reads 0 and the label '0%'. Narrowed in M3b: view-model evidence, not a screenshot.",
    "props-rotation-label-unrounded": "LayerPropertiesViewModelTests and LedgerScenarioTests: after a handle drag to 77.4 the label reads '77°', 30.4 reads '30°' and -0.4 reads '0°'. Narrowed in M3b: view-model evidence, not a screenshot.",
    "props-panel-rebuild-resets": "LayerPropertiesViewModelTests and LedgerScenarioTests: with Image adjustments open, a rename, a slider edit, Reset, a selection change and a canvas drag leave it open. Narrowed in M3b: the ']' key lands with M4's keyboard, so the same refresh is driven by the edits that reach it today.",
    "props-scale-clamp-inconsistency": "LedgerScenarioTests and SelectionGeometryTests: wheel scaling past 500% stops at 500% and the slider reads 500 with the label '500%'; the slider, the wheel and the handles share SelectionGeometry.ClampScale (1-500%).",
    "layers-model": "ProjectIOTests (M2b) load a v4 project and compare every layer field and round-trip it; LayerPropertiesViewModelTests: a stored adjustment of 0 displays as 0. Narrowed in M3b: the display half is the view-model.",
    "layers-default-name-offbyone": "LayerOpsTests (M3a): LayerOps.DefaultName(id) is 'Image <id>'; ToolsTests and LedgerScenarioTests: drawing layers are named by Tools.DrawingName, so 4.1's 'Drawing <nextId+1>' fallback never applies.",
    "regions-click-fit-layer": "DocumentOpsTests (M3a): a 200x100 layer fits torso_front at scale 0.544, x 240.6, y 110.8, rotation 0; LedgerScenarioTests: the properties panel shows 54%, X 241 and Y 111 right after the fit.",
    "layers-select": "LayersViewModelTests and LedgerScenarioTests: a pick in the list selects the layer, highlights its row and fills the properties panel. Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "layers-reorder-drag": "LayerOpsEditTests: LayerOps.DropIndices for 4 layers over every (from, to, half) combination equals editor.js:621-657's arithmetic. LayersReorderTests and LedgerScenarioTests: dragging the top row onto the bottom half of the last row reorders as one step; a pointer off every row clears the target. By hand in the M3b manual checklist (H7). Narrowed in M3b: compared with the ported arithmetic, not a JS run.",
    "layers-reorder-drag-2": "LayersReorderTests, LayersPanelDragTests and LedgerScenarioTests: the bottom layer dropped on the top half of the top row becomes the top, the indicator draws on that edge; Alt+Up and Alt+Down move a focused row one step each. By hand in the M3b manual checklist (H7, H7b). One Ctrl+Z restores it (the window's KeyBinding, ShellTests.Ctrl_Z_on_the_window_runs_the_Edit_groups_undo; H6). Narrowed in M3b: the drag runs through LayersPanel's seams in unit tests, because WPF ignores posted mouse input.",
    "layer-reorder-dragging-class-timer": "LayersPanelDragTests: the dragged row's IsDragSource dims it to .4 and the drop indicator sits above or below the target row. By hand in the M3b manual checklist (H7).",
    "layers-list": "LayersViewModelTests and LedgerScenarioTests (M3a): 3 imports list newest first and selected; Hide flips the label to 'Show'; Dupe inserts above the original; the order equals the z-order. The 40% name dim of a hidden row and keyboard-only operation are checked by hand in the M3b manual checklist (H8).",
    "home-brush-group": "LedgerScenarioTests: Fill checked with Rect draws a filled rectangle in the brush color; wheel-down at 4 gives 3 (BrushViewModelTests); CanvasCursorsTests: size 50 gives a ring of radius 25 x zoom DIPs. Narrowed in M3b: view-model and cursor evidence, not UI automation (M6).",
    "tools-draw-props": "BrushViewModelTests and LedgerScenarioTests: color #17d4fa, size 4 and Fill off at launch; a size change rebuilds the eraser ring (TemplateCanvasHostTests). Narrowed in M3b: view-model evidence, not UI automation (M6).",
    "draw-brush-controls": "BrushViewModelTests: the defaults (#17d4fa, 4, off) and the size clamp 1..50; compared side by side in the M3b look sign-off (state 04).",
    "undo-snapshot-contents": "EditorSessionTests and HistoryTests: a snapshot holds the document (every Layer and Document field, compared by Document.ContentEquals) and the selection, and undo restores them; LedgerScenarioTests: the brush, the current tool and the zoom are not in it and survive undo. Narrowed in M3b: the field round trip is ContentEquals over the record, not one assertion per field.",
    "region-front-highlight-lost": "RegionsGroupTests (M3a): a cleared Front tile has no swatch and keeps IsFront; by hand in the M3b manual checklist (H6): paint Front, right-click it, and the tile shows its purple resting tint again.",
    "canvas-device-pixel-render": "RendererViewTests (M3a): the frame is Renderer.ViewSize at DeviceScale 1.25, 1.5 and 2, and at DeviceScale 1 it equals the view goldens. By hand in the M3b manual checklist (H9) at 125% or 150% display scale: template edges are crisp at zoom 1, and the export stays 585x559.",
    "shell-external-drop-guard": "By hand in the M3b manual checklist (H10): a PNG dragged from Explorer over the layers panel and the ribbon shows the no-drop cursor and does nothing; dropped on the drop zone it highlights the zone and lands centered in the front torso; dropped on the canvas it lands centered on the drop point. Narrowed in M3b: a hand check, not UI automation (M6).",
    "fillstyle-color-pickers": "Tests: ColorMathTests (HSV/RGB round trips for primaries, grays and edge hues; hex parsing), ColorPickerViewModelTests (preview, keys, hex field, commit, cancel), ColorPickerPopupTests (automation names, arrow keys, Esc/Enter, anchored on-screen placement, theme-only brushes, owner-click detection), FillStyleTests (preview, cancel, commit and unchanged picks as one or zero steps; anchor plumbing; a preview through another region's color leaves it alone), LedgerScenarioTests.A_color_pick_retargets_matching_regions_in_one_step_and_a_cancel_changes_nothing. Smoke: the M3a themed run (2026-10-14, UI Automation) typed a hex, moved the square and hue strip by arrow keys, and checked Esc and Enter for Color 1 and Color 2; by hand in the M3b manual checklist (H10): dragging the square and the hue strip moves painted regions live, a click outside commits with no ding, and one Ctrl+Z reverts the pick (the window's KeyBinding, ShellTests.Ctrl_Z_on_the_window_runs_the_Edit_groups_undo).",
    "cross-window-dpi-rendering": "By hand in the M3b manual checklist (H9): at 100% and at 125% or 150% display scale the layout matches in DIPs and the export is 585x559. Narrowed in M3b: two display scales, not four.",
}
unknown = closing - rows.keys()
assert not unknown, f"unknown ids: {sorted(unknown)}"
for rid, text in NARROW.items():
    if rid not in closing:
        continue
    r = rows[rid]
    assert r["status"] == "open" and r["parityCheck"] != text, f"{rid} was already narrowed or closed"
    r["parityCheck"] = text

open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
print(f"amended 2, reworded {len(REWORD)}, appended {len(APPEND)}, added {len(NEW)}, narrowed {len(closing & NARROW.keys())}; {len(data['rows'])} rows")
```

Print the state first:

```bash
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
```

Expected: `413 rows; 122 done; 195 fix`.

Then run the edit and the close with the same ids, minus every id Steps 11-12 removed. The edit narrows only the ids it is given; the two amendments, the four rewords, the four appends, the behavior correction and the four new rows always apply:

```bash
IDS="select-hit-test select-hit-test-rect-not-alpha select-rotate-handle-hit select-resize-handle-hit
select-resize-drag select-rotate-drag select-rotate-drag-2 select-resize-handles select-move-hit-and-drag draw-begin
draw-tool-start draw-pen-single-click draw-commit-layer draw-empty-no-layer layers-add-drawing eraser-apply
canvas-draw-tools draw-preview-composite render-live-preview tools-set-tool tools-auto-select-switch tools-cursor
cursor-eraser eraser-cursor cursor-default canvas-cursor-states select-hover-idle render-restore-and-cursor
render-selection-box render-resize-handles render-rotation-handle render-rotation-label select-handle-render
select-hidden-layer-still-handled select-rotate-handle select-move select-deselect-click select-hover-and-end
canvas-select-gestures canvas-coord-map canvas-mouse-button-agnostic draw-release-outside-canvas wheel-scale-layer
wheel-brush-size wheel-sign-only-and-midstroke eraser-selection-semantics layers-empty-state props-name-xy props-scale
props-rotation props-opacity props-flip props-adjust-panel props-adjust-reset props-live-refresh
props-falsy-zero-adjust-display props-rotation-label-unrounded props-panel-rebuild-resets
props-scale-clamp-inconsistency layers-model layers-default-name-offbyone regions-click-fit-layer layers-select
layers-reorder-drag layers-reorder-drag-2 layer-reorder-dragging-class-timer layers-list home-brush-group
tools-draw-props draw-brush-controls undo-snapshot-contents region-front-highlight-lost shell-external-drop-guard
fillstyle-color-pickers canvas-device-pixel-render cross-window-dpi-rendering wheel-scale-undo-burst
eraser-click-without-move canvas-automation-peer overlay-knob-glyph-rasterization"
python .superpowers/logs/m3b-ledger.py docs/port/parity-ledger.json $IDS
python tools/ledger/ledger.py done $IDS
python tools/ledger/ledger.py check
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
grep -n "^## " docs/port/divergences.md
git diff --numstat -- docs/port
```

Expected, measured on a scratch copy of `4e0be14`'s `docs/port` and `tools/ledger` on 2026-10-15 with all 80 ids and exactly this script:

- `amended 2, reworded 4, appended 4, added 4, narrowed 69; 417 rows` (11 of the 80 ids close on their check as written: seven pure unit rows and the four new rows).
- `rendered docs/port/parity-ledger.md and docs/port/divergences.md`.
- `417 rows, 0 problem(s)`.
- `417 rows; 202 done; 200 fix`: `122 + C1` done with C1 the number of ids given (80 here), and `195 + 2 + 3` fix (the amended `eraser-cursor` and `cross-text-select-and-selection`, and the three new fix rows; `eraser-click-without-move` is `keep`).
- `## Fixed in v5 (200)`, `## Not carried into v5 (26)`, `## New in v5 (1)`, at lines 5, 210 and 241.
- The numstat lines:
  - `235	158	docs/port/parity-ledger.json`;
  - `82	78	docs/port/parity-ledger.md`;
  - `14	9	docs/port/divergences.md`.

  The JSON loses one `status` line per closed existing id (76), one `parityCheck` per narrowed id (69), the two amended rows' `disposition` and `divergence` (4), the four reworded and the four appended `divergence` lines (8) and `ribbon-overflow-hscroll`'s `behavior` (1): 158. It gains those back and the four new rows (77 lines). Each id removed by Steps 11-12 takes one line off each side of the JSON (two if it is in NARROW) and one off each side of `parity-ledger.md`. A larger JSON diff means the file was reformatted: stop, run `git checkout -- docs/port`, and report it.

- [ ] **Step 14: Final verification**

Run, in order, from the repo root:

```bash
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror
dotnet test src/RBX15.slnx -c Release > .superpowers/logs/m3b-final.log 2>&1; grep -E "(Passed|Failed)!" .superpowers/logs/m3b-final.log
python -m unittest discover -s tools/ledger -p "test_*.py"
python tools/ledger/ledger.py check
node --test "tools/parity-ref/*.test.js" 2>&1 | grep -E "^# (tests|pass|fail)"
npm test 2>&1 | grep -E "^# (tests|pass|fail)"
git diff --stat "$BASE" -- main.js editor.js editor.html styles.css lib packs package.json package-lock.json "tests/*.test.js" windows
git diff --stat --diff-filter=MDRT "$BASE" -- tests/goldens assets/fonts
git diff --name-only --diff-filter=A "$BASE" -- tests/goldens assets/fonts | sed 's|/[^/]*$||' | sort | uniq -c
git log --format=%s "$BASE"..HEAD -- tests/goldens
grep -rnE "UseWPF|PresentationCore|PresentationFramework|WindowsBase|System\.Windows" src/RBX15.Engine --include=*.cs --include=*.csproj --exclude-dir=bin --exclude-dir=obj
grep -rnE "HttpClient|WebRequest|WebClient|System\.Net\.Sockets|TcpClient|ClientWebSocket" src/RBX15.Engine src/RBX15.App --include=*.cs --exclude-dir=bin --exclude-dir=obj
grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src --include=*.cs --exclude-dir=bin --exclude-dir=obj
dotnet list src/RBX15.App/RBX15.App.csproj package
grep -rnE "626 ?Labs" src/RBX15.App --include=*.cs --include=*.xaml --exclude-dir=bin --exclude-dir=obj
git diff "$BASE" -U0 -- src tools | grep -inE "^\+.*(api[_-]?key|secret|password|bearer )"
git status --short
```

Expected:

- `base 4e0be14`.
- Build: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: three summary lines, each `Failed: 0` and `Skipped: 0`: `RBX15.Engine.Tests.dll` `Passed: 877`; `RBX15.Parity.Tests.dll` `Passed: 35`; `RBX15.App.Tests.dll` `Passed: 622`.
- Ledger unit tests: `Ran 8 tests`, `OK`. `ledger.py check`: `417 rows, 0 problem(s)`.
- `node --test`: `# tests 14`, `# pass 14`, `# fail 0` (no M3b task adds a `.test.js`). `npm test`: `# tests 31`, `# pass 31`, `# fail 0`.
- The Electron diff prints nothing: Electron is untouched.
- The `--diff-filter=MDRT` diff prints nothing: no existing golden, scene file or font changed.
- The added-files count prints exactly one line, `12 tests/goldens/view-select` (10 PNGs, `index.json`, `scenes.json`), and the log prints exactly one subject, Task 1's controller-run capture commit. Anything else: stop and show `git log --stat "$BASE"..HEAD -- tests/goldens` to the controller.
- The WPF grep on the engine, the network grep and the synthetic-input grep print nothing (no app code posts input; the drivers live under `.superpowers/`).
- `dotnet list`: one top-level package, `CommunityToolkit.Mvvm`, Requested and Resolved `8.4.2`.
- The `626` grep prints exactly three lines, all in `src/RBX15.App/ViewModels/MainViewModel.cs`: the two doc-comment lines about the footer and `public string FooterText => "Developed at 626Labs";` (measured on `4e0be14`: lines 27, 28 and 30). Any other hit is branding 4.1 does not show: report it.
- The secrets grep prints nothing.
- `git status --short` lists exactly three lines, each with `M` in the second column: `docs/port/divergences.md`, `docs/port/parity-ledger.json`, `docs/port/parity-ledger.md`. `.superpowers/` and `diff-heatmaps/` are git-ignored.

- [ ] **Step 15: Commit the ledger**

Write the body from Steps 11-13. The "Left open" list is the fixed table below plus every id Steps 11-12 removed, with its reason. The `Hand checks:` line is the last line of `.superpowers/logs/m3b-hand.txt`. Add your own model's `Co-Authored-By` trailer as the last line before running it.

| Rows | Why open | Closes with |
| --- | --- | --- |
| `theme-tokens-colors`, `font-ui-runtime-google`, `font-ui-weight-600`, `shell-layout-skeleton`, `mode-toggle-button`, `regions-grid-layout-shirt`, `regions-hint-text`, `layers-list-render`, `render-zoom-canvas`, `theme-scrollbars`, `ribbon-overflow-hscroll` | Their checks are look comparisons. | Step 16, on Este's sign-off |
| `tools-toggle-off`, `home-tool-buttons`, `draw-tool-switch-midstroke` | The tool keys (V, B, L, U, O, E) and the only mid-stroke tool switch under capture. | M4 |
| `draw-pen-incremental-stroke` | No pen golden; pen previews are not frozen (approved divergence). | M6 parity tier |
| `props-adjust-sliders`, `layers-selected-panel` | Per-slider extremes against Electron are not captured. | M6 |
| `render-pipeline-canvas-reset` | No frame-time measurement for its 60 fps budget. | M6 |
| `undo-coverage-gaps`, `undo-empty-steps`, `file-undo-snapshot-scope` | Their matrices list presets, details, keys and project load. | M4 |
| `drop-canvas` | The stale asset-library drag. | M4 |
| `regions-button-swatch-paint`, `home-mode-toggle`, `ribbon-group-structure`, `ribbon-tab-switch`, `ribbon-tabs-switching`, `layout-fill-style`, `layout-template-regions`, `fillstyle-type-buttons`, `fillstyle-ui-not-synced-from-region`, `fillstyle-angle-scale`, `fillstyle-live-render-hook` | The Layout, Details and Text tabs; all seven swatch types; project load. | M4 |
| `cross-text-select-and-selection` | Tool tips drop their shortcut letters until the keys exist (interfaces R14); amended to `fix` in Step 13 for the keyboard-focus tooltips (H10b). | M4 |
| `panel-drop-zone` | Step 13 appends the narrow-panel label wrap to its divergence (Task 4 T4-R11); its parityCheck (three PNGs dropped, the dragover look) is outside this run's close list. | M4 |
| Every other open row in `.superpowers/m3b-planning/open-rows.txt` (`btn-*`, `preset-*`, `asset-*`, `home-placement-group`, `home-file-group`, `dialogs-inventory`, `io-alert-inventory`, `regions-right-click-clear` and the rest) | Outside M3b's scope; untouched. | M4 and later |
| `theme-tokens-shape-motion-type`, `cross-undo-disabled-style`, `cross-inline-style-overrides` | Hover, pressed and motion states and the other tabs are not in the captures. | M4, M6 |
| `shell-header`, `shell-header-version-hardcoded`, `project-startup`, `a11y-focus-rings` | The MSIX version, first-frame time and the Accessibility Insights scan. | M6 |
| `shell-window-frame`, `shell-window-size`, `shell-lifecycle-quit`, `shell-single-instance`, `shell-responsive-breakpoint` | The close prompt, two display scales, process count and the 900/901 breakpoint. | M4, M5, M6 |
| Provisional values not in any state: the themed dialog's MinWidth 360, MaxWidth 480, padding 20,18,20,16, 13px message and 18 DIP gap; the status strip's padding 12,6, 12px text and 12 DIP gap | No 4.1 source (M3a critic P3); the dialog inventory reviews them. | M4 |

```bash
HAND=$(tail -n 1 .superpowers/logs/m3b-hand.txt)
git add docs/port/parity-ledger.json docs/port/parity-ledger.md docs/port/divergences.md
git commit -F - <<EOF
chore(ledger): close the rows M3b proves; add the M3b divergence rows

Closed by the M3b unit tests, the view-select goldens
(GoldenViewSelectTests), the LedgerScenarioTests and Este's hand checks:
the select tool (hit test, handles, resize, rotate and snap, move,
deselect, hover and drag cursors, capture, coordinate map, left button
only), the overlay (box, handles, knob, label, none on a hidden layer),
the draw tools (press, commit, naming, empty and click strokes, release
outside, previews), the eraser (selected layer, refusal at the press,
click without movement, ring cursor), the wheel (layer and brush), the
layer properties panel (every field, adjustments, Reset, live refresh,
0 shown as 0), drag reorder, the brush group, the snapshot contents, the
Front tile tint, the Explorer drop guard, the themed color picker, and,
when H9 passed, the device-pixel view at 125% or 150%.
69 parityCheck texts are narrowed to the evidence used ("Narrowed in
M3b" or "Reworded in M3b").

Amended to fix: eraser-cursor, whose "capped at diameter 240"
contradicted cursor-eraser's fix, which draws the true footprint; and
cross-text-select-and-selection, for the keyboard-focus tooltips (left
open for M4's hover audit).

Reworded to what M3b ships: cursor-eraser (the Win32 cursor cap per
display scale), wheel-sign-only-and-midstroke (symmetric centered steps;
mid-shape width kept), render-zoom-canvas (the persistent offscreen and
one render per frame), select-rotate-handle-hit (the knob is hit where
it is drawn; 4.1 hit-tested its mirror image). Appended:
layers-reorder-drag-2, canvas-cursor-states, props-name-xy and
panel-drop-zone. Corrected: ribbon-overflow-hscroll's behavior (4.1's
Home pane overflows a 1200 px window too).

New: wheel-scale-undo-burst (a wheel burst is one undo step),
eraser-click-without-move (kept: a click erases nothing),
canvas-automation-peer (the canvas's name, bounds and selected layer for
UI Automation) and overlay-knob-glyph-rasterization (the knob glyph's
antialiasing at some offsets), each closed.

Left open on purpose: the look rows wait for Este's sign-off of the
comparison page (Step 16); the tool keys and the mid-stroke switch they
trigger (M4); the pen golden, per-slider extremes, frame time, the
Accessibility Insights scan and the MSIX version (M6); the undo
matrices, the asset drag, the Layout, Details and Text tabs, swatch
types, tool-tip shortcut letters and the dialog values (M4); the close
prompt and window rows (M5, M6).

$HAND
Electron, every existing golden and assets/fonts are untouched;
tests/goldens/view-select was added once by Task 1's capture.
EOF
git log --oneline -3
git status --short
```

The heredoc is unquoted so `$HAND` expands; the body holds no other `$` or backtick. If Steps 11-12 removed ids, delete their topics from the "Closed" paragraph and add each, with its reason, to "Left open" before committing.

Expected: three commits from this task on top of Task 4's, newest first: `chore(ledger): close the rows M3b proves; add the M3b divergence rows`, `test(app): replay the M3b ledger scenarios across the canvas, tools, panels and layers`, `feat(tools): capture 4.1's whole window in matched look states for the M3b sign-off`; and an empty `git status --short`. Do not push.

- [ ] **Step 16: The sign-off gate (controller-run; the last step)**

Present the page published in Step 10 to Este (open it with the Artifact tool and give him the link), with one short note: what the page compares, the two measured verdicts from Step 10, and that his reply is `signed off` or a list of fixes by state number. Then stop and wait for his answer. Nothing below runs before it.

Record his answer verbatim in `.superpowers/m3b-look/SIGNOFF.txt`, first line `Signed off by Este on <YYYY-MM-DD>: <his words>` or `Fixes requested by Este on <YYYY-MM-DD>: <his words>`.

**If he asks for fixes:** the look rows stay open, the ledger does not change, and each fix goes into a follow-up fix round the controller plans (the owning task, a new commit, then Steps 9, 10 and this step again). Captions in the page that he disputes are rulings to revisit, not ledger text.

**If he signs off:** close the look rows whose evidence holds. `shell-layout-skeleton` and `font-ui-runtime-google` close only if Step 10's verdict for each was `true` (`.superpowers/m3b-look/page/metrics-verdict.json`); `layers-list-render` also needs H8. Save the edit as `.superpowers/logs/m3b-look-ledger.py`:

```python
"""M3b Task 5, after Este's look sign-off: narrow the parityCheck of the look rows being closed to the
sign-off evidence. Usage: python m3b-look-ledger.py <parity-ledger.json> <YYYY-MM-DD sign-off date> <id>...
Run once, after m3b-ledger.py and before `ledger.py done` with the same ids."""
import json, re, sys

path, date, closing = sys.argv[1], sys.argv[2], set(sys.argv[3:])
assert re.fullmatch(r"\d{4}-\d\d-\d\d", date), f"bad date {date}"
data = json.load(open(path, encoding="utf-8"))
rows = {r["id"]: r for r in data["rows"]}
assert len(data["rows"]) == 417, f"expected 417 rows (m3b-ledger.py first), found {len(data['rows'])}"
SO = f"signed off by Este on {date} (.superpowers/m3b-look/SIGNOFF.txt)"
LOOK = {
    "theme-tokens-colors": f"ThemeTokenTests (M3a): every token hex in the theme equals styles.css. The ribbon and panel regions compared side by side with 4.1 at 1200x800 and 100% scale in the M3b look sign-off (states 01-07), {SO}. Narrowed in M3b: Este's side-by-side review stands in for a pixel diff.",
    "font-ui-runtime-google": f"No network: the app has no network code (the M3b final verification grep) and the UI faces resolve to the vendored files (ThemeTokenTests.Ui_fonts_resolve_to_the_vendored_files). Glyph metrics: the 'Download template' button is within 2 px of 4.1's width in the M3b look run's metrics table; {SO}. Narrowed in M3b: the button box stands in for the text box.",
    "font-ui-weight-600": f"ThemeTokenTests.Ui_fonts_resolve_to_the_vendored_files pins the face each family resolves to; the header title, ribbon tabs, group labels and panel headings were compared side by side in the M3b look sign-off, {SO}.",
    "shell-layout-skeleton": f"The M3b look run at a 1200x800 window and 100% scale: the Home ribbon's top (the header bottom), the ribbon bottom, the left panel's right edge and the canvas area's bottom (the footer top) match 4.1 within 1 px in the metrics table; {SO}. Narrowed in M3b: tools/parity-ref/look-ref.js captures at the v5 client size stand in for docs/screenshots at 1920x1080.",
    "mode-toggle-button": f"ModeGroupTests (M3a): the label reads SHIRT, then PANTS. Both states compared side by side in the M3b look sign-off (states 01 and 06), gradient direction included, {SO}. Narrowed in M3b: the gradient endpoints are judged by eye, not sampled.",
    "regions-grid-layout-shirt": f"RegionsGroupTests (M3a): 14 tiles in 4.1's cell order with its labels, each with a region automation name. The grid compared side by side with 4.1's Layout tab in the M3b look sign-off (state 02), {SO}. Narrowed in M3b: the grid sits on the Home tab until M4 moves it to Layout.",
    "regions-hint-text": f"The M3b look run reads the Fill Style hint through UI Automation as 'Set a style, then click a region to paint it. Right-click clears.' (editor.html:164; .superpowers/m3b-look/v5/RESULTS.md); compared side by side in state 02, {SO}.",
    "layers-list-render": f"A selected row (state 03) compared side by side in the M3b look sign-off, and a hidden row, a long name truncated and 20 layers scrolling checked by hand in the M3b manual checklist (H8); {SO}. Narrowed in M3b: Este's review stands in for a screenshot diff.",
    "render-zoom-canvas": f"GoldenViewTests (M3a) at zoom 0.25, 0.5, 1, 1.5 and 3 and GoldenViewSelectTests at 0.5, 1, 1.5 and 2: crisp magnification and smooth downsampling; 1.5 gives 877x838 (RendererViewTests). The 200% scroll host compared side by side in the M3b look sign-off (state 05), {SO}. Narrowed in M3b: zoom 4 is outside v5's 25-300% range.",
    "theme-scrollbars": f"The left panel's scrollbar (states 03 and 05) and the canvas scrollbars (state 05) compared side by side with 4.1 in the M3b look sign-off, which also confirmed M3a's provisional width 8 and thumb inset 1; {SO}. Narrowed in M3b: 1200x800 captures stand in for 02-layers.png.",
    "ribbon-overflow-hscroll": f"The launch state at a 700 DIP client width (state 07): the ribbon scrolls horizontally and its groups keep their size, compared side by side with 4.1 at the same width, {SO}. Narrowed in M3b: a 700 DIP client stands in for a 700 px window.",
}
unknown = closing - LOOK.keys()
assert not unknown, f"not look rows: {sorted(unknown)}"
for rid in sorted(closing):
    r = rows[rid]
    assert r["status"] == "open" and r["parityCheck"] != LOOK[rid], f"{rid} was already narrowed or closed"
    r["parityCheck"] = LOOK[rid]
open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
print(f"narrowed {len(closing)} look rows; {len(data['rows'])} rows")
```

Run it with the sign-off date and the ids that hold:

```bash
DATE=$(sed -n '1s/^Signed off by Este on \([0-9-]*\):.*/\1/p' .superpowers/m3b-look/SIGNOFF.txt); echo "date=$DATE"
python -c "import json; v=json.load(open('.superpowers/m3b-look/page/metrics-verdict.json')); print(v['shell-layout-skeleton'], v['font-ui-runtime-google'])"
LOOK="theme-tokens-colors font-ui-runtime-google font-ui-weight-600 shell-layout-skeleton mode-toggle-button
regions-grid-layout-shirt regions-hint-text layers-list-render render-zoom-canvas theme-scrollbars ribbon-overflow-hscroll"
python .superpowers/logs/m3b-look-ledger.py docs/port/parity-ledger.json "$DATE" $LOOK
python tools/ledger/ledger.py done $LOOK
python tools/ledger/ledger.py check
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done')"
git diff --numstat -- docs/port
```

Remove `shell-layout-skeleton` or `font-ui-runtime-google` from `LOOK` when its verdict printed `False`, and `layers-list-render` when H8 did not pass.

Expected with all 11 ids (measured on the scratch copy after Step 13's 80): `date=<the date>`, `True True`, `narrowed 11 look rows; 417 rows`, `rendered ...`, `417 rows, 0 problem(s)`, `417 rows; 213 done` (`122 + C1 + C2`), and numstat `22	22	docs/port/parity-ledger.json`, `12	12	docs/port/parity-ledger.md` (`divergences.md` does not change: these rows keep their texts). Each id left out takes one line off each side of both files.

Commit it, with the controller's own model trailer as the last line:

```bash
git add docs/port/parity-ledger.json docs/port/parity-ledger.md
git commit -F - <<EOF
chore(ledger): close the look rows on Este's M3b sign-off

$(head -n 1 .superpowers/m3b-look/SIGNOFF.txt)
The comparison page paired 4.1 (tools/parity-ref/look-ref.js) and v5 (the
UI Automation driver, PrintWindow) in seven states at a 1200x800 window
and 100% scale, with a metrics table of element rectangles. Closed:
$(echo $LOOK | sed 's/ /, /g').
EOF
git log --oneline -1
git status --short
```

Expected: one commit `chore(ledger): close the look rows on Este's M3b sign-off` and an empty `git status --short`. The controller then handles push, the PR and the dashboard (log the sign-off as a decision with project `6KpHItSNg9NYHkJg9thn`).

**Additions requested by Task 5.**

1. **The `RBX15.App.Canvas` namespace shadows WPF's `Canvas` (measured).** Inside any namespace under `RBX15.App` (the app's `RBX15.App.Views` and the tests' `RBX15.App.Tests`), an unqualified `Canvas.X` resolves to the namespace and fails with CS0234. Existing hits on `4e0be14`: `src/RBX15.App/Views/ColorPickerPopup.xaml.cs:168-170` (`Canvas.SetLeft`, `Canvas.SetTop`) and `src/RBX15.App.Tests/ColorPickerPopupTests.cs:103`, `:106`, `:107` (`Canvas.GetLeft`, `Canvas.GetTop`). Task 2 Step 2 qualifies them as `System.Windows.Controls.Canvas.*` in the step that creates `src/RBX15.App/Canvas/`; the namespace keeps the sheet's name (critic: qualify, do not rename). Measured fix: qualifying the eight calls (three in the view, five in the test) builds clean under `-warnaserror`. XAML `Canvas` elements are unaffected (XAML resolves through its own namespace map).
2. **`CanvasInteraction` subscribes to `ToolsViewModel.CurrentChanged`** and runs `Cancel()` when the tool changes during a gesture (interfaces R9), so any caller of `tools.Set` (a button, M4's keys) cancels a stroke and ends a select drag. `A_tool_switch_mid_stroke_discards_it_and_a_lost_capture_commits_it` drives it through `tools.Set`.
3. **`PointerUp` with no active gesture does nothing** (the stray release after a cancel or a refused press).
4. **Label formats on `LayerPropertiesViewModel`**: `RotationLabel` is the whole degrees plus `°` (editor.js:763 appends `°`): `"30°"`, `"0°"`, never `"-0°"`; `ScaleLabel` and `OpacityLabel` are `toFixed(0)` plus `%` (`"250%"`, `"54%"`, `"500%"`, editor.js:761, :765); `BrightnessLabel` is the value plus `%` (`"0%"`, editor.js:785); `XText` and `YText` are `toFixed(0)` (`"13"`, `"241"`, editor.js:756). `ScalePercent` reads `Scale x 100` unrounded (`54.4` after the fit).
5. **`LayersViewModel.LayerPicked` fires only on a user change of `Selected`**, never when the list re-syncs from `EditorSession.Changed` (undo, redo, add, remove, `Select`), so undo leaves the tool alone (R8).
6. **`CanvasInteraction`'s `IStatus` is `MainViewModel.Status`** (Composition step 1 creates one status for the canvas and import), so the eraser refusal shows on the strip and in `StatusViewModel.Message`.
7. **Automation names the look driver relies on**: the tool buttons are named by their labels (`Select`, `Draw`, `Line`, `Rect`, `Ellipse`, `Eraser`); the properties panel's rotation slider is a `Slider` named `Rotation degrees` with RangeValue (Task 3's `AutomationProperties.Name`, the `Scale percent` and `Opacity percent` pattern; critic MJ5); the Image adjustments header is named `Image adjustments` and exposes ExpandCollapse (an `Expander`) or Toggle; a new layer's list row is a `ListItem` named by the layer name (M3a's `AutomationName` for a visible layer); the Pants body tile is `Waist Front`; the left panel's scroll host is the `ScrollViewer` ancestor of the `DropZone`.
8. **`GoldenViewSelectTests` is a `[Theory]` whose only parameter is `string name`**, so a normal-verbosity log prints `Passed RBX15.Engine.Tests.GoldenViewSelectTests.<Method>(name: "<scene>")`, as `GoldenViewTests` does; and `tests/goldens/view-select/scenes.json` is an array of objects with a `name` field (the gate in Step 12 reads both).
9. **`ThemeKeyGuardTests`' view count**: Task 2 raises `Assert.Equal(12, views.Count)` to 14 (`ToolsGroup.xaml`, `BrushGroup.xaml`) and Task 3 to 15 (`LayerPropertiesPanel.xaml`), with the comment naming the files; otherwise the guard fails at Task 2. Step 1 checks for 15.
10. **Interface sheet section 4, amended by this task**: `look-ref.js` does not inject `app-harness.js` (ruling 2); the states file adds `shows`, `alsoTab41`, `contentWidth` and the `scrollPanel` op, and a supplementary `07-narrow-700` state (ruling 4); `look-ref.js` also writes `metrics.json`, the driver `metrics-v5.json` with the same keys (`headerTitle`, `homeRibbonPane`, `modeToggle`, `downloadTemplate`, `leftPanel`, `canvasArea`, `zoomIn`); the page builder is `.superpowers/m3b-look/build_page.py` and writes `page/look-signoff.html`, `page/img/` and `page/metrics-verdict.json`.
