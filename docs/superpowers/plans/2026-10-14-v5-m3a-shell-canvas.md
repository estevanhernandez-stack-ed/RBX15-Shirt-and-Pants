# RBX15 v5 M3a: shell and canvas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**M3 is split, and this plan is M3a.** The spec's M3 row (spec §7: "Shell: theme, layout, ribbon, canvas host, layers panel and properties; select and draw tools wired") is too large for one branch. M3a builds the shell and the canvas: theme, window, layout, header and footer, ribbon chrome, `EditorSession`, the canvas host (zoom, scroll-pan, checkerboard, grid, mask), the Home tab's Mode, Regions, Fill Style, Settings and Edit groups, export, image import, the basic layers panel, a minimal themed dialog and a status strip. M3b adds the select gestures, the selection overlay, the draw tools, cursors, the layer properties panel, drag reorder and the side-by-side look sign-off. M4 adds Save and Open, the asset library, the Layout, Details and Text tabs, the Placement group, the dialog inventory and the keyboard. **What does not work yet is hidden, never shown dead** (Este's standing ruling).

**Goal:** A WPF app on the M1 to M2b engine in which a user can open the editor, paint regions with any of the 7 fill styles, import images (picker, drop zone or canvas drop), arrange basic layers, zoom and pan the canvas, undo and redo every document change as exactly one step, and download the 585x559 template, with the visible canvas held to 4.1's own pixels by 14 new view goldens and every theme token held to `styles.css` by unit tests.

**Architecture:**

- **Engine (WPF-free).** `Document.ContentEquals` (Task 1) lets the session record a step only when content changed. `ViewOptions` and `Renderer.RenderView` (Task 2) port 4.1's visible `render()` minus the selection overlay and the live preview: the composite at template size, the 16 px checkerboard, smoothing only below an effective scale of 1, and the grid, at any zoom and device scale. `DocumentOps` (Task 3) holds the region and mode edits, and `LayerOps`, `ExportGuard` and the `Images` decode caps (Task 4) hold the layer edits, the empty-export guard and the untrusted-image limits. Every op is pure and static and returns its input instance when it changes nothing.
- **`EditorSession`** (RBX15.App, WPF-free, Task 1) owns the document, the 30-step `History`, the selection, the id counter and the template title. `Apply` and `ApplyWithSelection` make every document change one undo step, and none when nothing changed; `BeginGesture` folds a slider drag into one step.
- **The shell.** One `ResourceDictionary` theme with `styles.css`'s effective values and the vendored UI fonts (Task 1). A `MainWindow` skeleton with 4.1's panel widths and breakpoint and remembered placement (Task 1). `TemplateCanvasHost` blits `RenderView` frames into a `WriteableBitmap` 1:1 in device pixels, inside a scroll host, with drop and click (Task 2). Home-tab view-models and views (Task 3); the layers panel, import, export, the themed dialog and the status strip (Task 4). CommunityToolkit.Mvvm view-models over the session, built in one composition root (`Composition.Build`) and, for tests, one builder (`TestShell.Main`).
- **Parity.** `tools/parity-ref/view-ref.js` captures 4.1's visible canvas once into `tests/goldens/view` (Task 2, controller-run); `GoldenViewTests` holds `RenderView` to it under the unchanged golden rule. Task 5 replays ledger scenarios across Tasks 1 to 4, gets Este's smoke checklist, and closes only the ledger rows the evidence proves.

**Tech Stack:** .NET 10 (`net10.0` engine, `net10.0-windows` App and App.Tests), C# with nullable and warnings as errors, WPF, CommunityToolkit.Mvvm `8.4.2` (Task 1, exact pin), SkiaSharp `4.153.1` and HarfBuzzSharp `14.2.1.301` (engine, unchanged), Win32 `comdlg32!ChooseColorW` by P/Invoke and the `Microsoft.Win32` file dialogs (no WinForms), xUnit 2.9.3 with `xunit.runner.visualstudio` 3.1.4 and `Microsoft.NET.Test.Sdk` 17.14.1, Electron 41.1.0 (devDependency; the view capture tool only), Python 3 stdlib (ledger scripts), Git Bash.

**Spec:** [`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`](../specs/2026-10-11-native-v5-port-design.md), sections 1, 3, 4, 5, 6 and the §7 M3 row. Ledger: [`docs/port/parity-ledger.json`](../../port/parity-ledger.json) and [`docs/port/divergences.md`](../../port/divergences.md). Planning contract: `.superpowers/m3-planning/contract.md` (git-ignored). Previous milestone: [M2b plan](2026-10-14-v5-m2b-projectio-details-wordart.md).

**Contract amendments for Este's OK** (2026-10-14). Each departs from the planning contract's wording, is applied below so nothing waits, and names the exact change if Este declines.

- **CA1: the view frame truncates, not `ceil`** (Task 2 ruling R1). The contract says the frame is `ceil(585*Zoom*DeviceScale) x ceil(559*Zoom*DeviceScale)`. 4.1 truncates (`canvas.width = TW*zoom`, editor.js:339-341; the ledger's own check says "1.5 -> 877x838"), and truncation is the only way the 25% and 50% goldens pass under the unchanged rule (measured: with `ceil` the stretched composite shifts up to a full source pixel at the right edge). `RendererViewTests.View_size_is_the_template_times_zoom_and_device_scale_truncated` pins it. If declined: `ceil` fails the 0.25, 0.5 and 1.5 view goldens, which the unchanged golden rule forbids, so the alternative is a new divergence row plus a cropped comparison.
- **CA2: no right panel** (Task 1 ruling 1). The contract and spec §4 name a right panel; 4.1 has none (editor.html:288-341; `.rpanel`, styles.css:462-469, is never used), and ledger `shell-layout-skeleton` says "Drop the dead .rpanel". The layers go in the left panel, where 4.1 has them. If declined: Task 1 adds an empty 240 px right column (styles.css:462) and Task 4 moves `LayersPanel` into it.
- **CA3: a canvas click selects or deselects** (Task 2 ruling R5, Task 4 Step 21; critic C13). Not in the contract's M3a list (the select tool is M3b), but without it the plan has a dead end: every import selects the new layer (editor.js:521), a region click with a selection fits the layer instead of painting (editor.js:1655-1666), and the list has no deselect, so region painting is unreachable once any layer exists. The click selects the topmost visible layer under the point or deselects (editor.js:1100-1116); dragging stays M3b. If declined: remove `ClickCommand`/`HandleClick`/`IsClick` (Task 2 Step 8), `ClickHandler`/`ClickCommand` (Task 2 Step 7), the two `ClickHandler` lines and `ShellWiringTests` (Task 4 Step 21), and the S6 click line (Task 5 Step 5); App.Tests drops by 3 at every later count; and the smoke checklist must paint regions before importing.
- **CA4: EXIF orientation in `Images.Decode`** (Task 4 Steps 9 and 10; critic S1). Not in the contract, the spec or a ledger row, and it changes M2b's project decode path. It is 4.1 parity: Chromium draws an `<img>` with `image-orientation: from-image`, so 4.1 shows a phone photo upright and fits it by its displayed size (editor.js:503). Task 5 adds the `keep` row `image-decode-exif-orientation`. If declined: cut Task 4 Steps 9 and 10 and `ImagesOrientationTests` (Engine.Tests drops by 8 from Task 4 Step 10 on: 708 instead of 716), drop the row, the S5 photo line, and Review Focus 4 (use the runner-up below).

**Interface sheet amendments** (`.superpowers/m3-planning/interfaces.md`; the plan below is authoritative): `Renderer.ViewSize` truncates (CA1, critic C9); `ImageTooLargeException` derives from `Exception`, because `InvalidDataException` is sealed (CS0509), and `ProjectIO.TryDecodeDataUrl` catches it alongside `InvalidDataException` (critic C8); `ImportService` shows one combined status message per call and clears the strip after a clean import (critic C16); and each task's "Interface additions adopted" list at its end is adopted verbatim (critic C18), with the names aligned to one spelling (critic C6, C7).

## Global Constraints

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

## Review Focus

The critic picked five input classes as the most likely to cause bugs. Each has a pinning test in its owning task.

- **Ctrl+Z pressed while a slider is still being dragged** (Task 1; Task 3's Fill Style sliders open a gesture on `Thumb.DragStarted`). Expected: the drag so far is committed as one step and undone; movement after the undo, with the thumb still held, becomes one new step when the drag ends, never one step per move. This is the subtlest state machine in M3a and M4's keyboard contract. The Edit group's Undo and Redo also refresh while a gesture changes the document (critic L1, Task 3 Step 8). Pinned in Task 1 (`EditorSessionTests.Undo_during_a_gesture_commits_it_undoes_it_and_the_drag_goes_on_as_a_new_step`, `Redo_during_a_changed_gesture_commits_it_and_finds_no_redo`).
- **A decompression bomb inside a mixed drop** (Task 4): a tiny PNG whose header claims 30000x30000, next to good images, an SVG and a text file. Expected: the bomb is refused from its header before any pixel memory is allocated, the good files land in the given order as one undo step, and one status message names every refused file and why. This is the first untrusted-image path, and M4's Open inherits it through `ProjectIO`. Pinned in Task 4 (`ImageImporterTests.A_decompression_bomb_is_refused_from_its_header`, `ImagesCapsTests.A_side_over_the_cap_is_refused_from_the_header`, `ImportServiceTests.A_mixed_drop_adds_the_good_files_as_one_step_and_names_every_failure`).
- **A fractional zoom (25%, 50%, 150%)** (Task 2). 585 x 0.25 is 146.25. Expected: 4.1's truncation (146x139, 292x279, 877x838), the composite stretched to exactly that frame, and the view goldens passing under the unchanged rule. This is contract amendment CA1, so it needs eyes. Pinned in Task 2 (`RendererViewTests.View_size_is_the_template_times_zoom_and_device_scale_truncated`, 10 rows; the 14 `GoldenViewTests` scenes; `TemplateCanvasHostTests.Frame_pixels_follow_zoom_and_device_scale_and_DIPs_divide_them_back`).
- **A portrait phone JPEG** (Task 4): pixels stored sideways plus an EXIF orientation. Expected: the layer is upright and fit by its displayed size, as 4.1 shows it. This is new engine behavior that also changes M2b's project decode (contract amendment CA4). Pinned in Task 4 (`ImagesOrientationTests.Exif_orientation_is_applied_like_chromium`, 8 cases) and smoke S5.
- **A missing monitor or a corrupt `settings.json`** (Task 1): a laptop undocked from its external display; a truncated, hand-edited or wrong-shaped settings file; an unwritable settings folder. Expected: the window opens 1200x800 centered on the primary work area, never off-screen; a placement hanging over an edge is moved inside, and one larger than the work area is shrunk; startup never throws and closing never blocks. Pinned in Task 1 (`PlacementGuardTests.A_placement_on_a_missing_monitor_falls_back_to_the_default`, `A_placement_hanging_off_an_edge_is_moved_inside`, `A_placement_larger_than_the_work_area_is_shrunk_to_fit`; `JsonSettingsStoreTests.A_corrupt_file_loads_as_no_placement`, 11 cases, and `A_save_that_cannot_write_does_not_throw`).

Runner-up (Review Focus 4's replacement if CA4 is declined): **undo after a live fill edit** leaves the Fill Style controls on the newer value while the regions revert, so the next slider move leaves the reverted regions alone and records nothing. Pinned in Task 3 (`FillStyleTests.Undo_of_a_live_edit_restores_regions_and_leaves_the_controls`).

The drafters pinned more edge cases: a slider drag that returns to its start records nothing (`FillStyleTests.A_drag_that_returns_to_its_start_records_nothing`, `An_edit_with_no_matching_region_changes_current_and_records_nothing`); the color picker returning the same color in upper case, or a cancel (`FillStyleTests.The_color_picker_is_seeded_stores_lowercase_and_cancel_or_same_color_records_nothing`); text or a browser image with no file dropped on the canvas (`TemplateCanvasHostTests.A_drop_without_files_or_without_a_willing_command_does_nothing`); a window moved between monitors at different scales (`TemplateCanvasHostTests.A_dpi_change_re_renders_at_the_new_device_scale`); an export that cannot be written, or a document whose only layers are hidden (`FileGroupTests.A_write_failure_shows_a_status_message`, `Hidden_layers_only_still_ask`); and a UI font silently falling back to a system face (`ThemeTokenTests.Ui_fonts_resolve_to_the_vendored_files`).

---

## File structure

```text
src/RBX15.Engine/
  Document.cs                  + ContentEquals (Task 1)
  ViewOptions.cs               ViewOptions(Zoom, DeviceScale = 1, ShowGrid = false), Scale (Task 2)
  Renderer.cs                  + RenderComposite, ViewSize (truncated), CheckerHalf (internal), RenderView, RenderViewImage;
                                 RenderExport becomes RenderComposite(c, d, true) (Task 2)
  DocumentOps.cs               SwitchMode, ToggleMode, RegionFill, PaintRegion, ClearRegion, RetargetFill,
                                 FitLayerToRegion, ClearLayers, RegionFitFraction (Task 3)
  LayerOps.cs                  DefaultName, Place, AddImage, Remove, Duplicate, ToggleVisibility, MoveUp, MoveDown,
                                 HitTest, FitFraction, DuplicateOffset (Task 4)
  ExportGuard.cs               EmptyTemplateMessage, HasArt (Task 4)
  Images.cs                    + MaxEncodedBytes, MaxDimension, MaxPixels, CheckEncodedSize, CheckDimensions,
                                 ImageTooLargeException; Decode enforces the caps and applies EXIF orientation (Task 4)
  ProjectIO.cs                 TryDecodeDataUrl also catches ImageTooLargeException (Task 4)
src/RBX15.Engine.Tests/
  DocumentTests.cs             + 16 cases (Task 1)
  RendererViewTests.cs         40 cases (Task 2)
  GoldenViewTests.cs           15 cases: 14 view scenes and the coverage fact (Task 2)
  GoldenRule.cs                + AssertImageMatches and a private Report; thresholds unchanged (Task 2)
  DocumentOpsTests.cs          21 cases (Task 3)
  LayerOpsTests.cs             30 cases (Task 4)
  ExportGuardTests.cs          7 cases (Task 4)
  ImagesCapsTests.cs           13 cases (Task 4)
  ImagesOrientationTests.cs    8 cases (Task 4)
src/RBX15.App/
  RBX15.App.csproj             CommunityToolkit.Mvvm 8.4.2, Version 5.0.0, ApplicationManifest, InternalsVisibleTo,
                                 the 3 UI fonts linked as Resources (Task 1)
  app.manifest                 per-monitor DPI v2 (Task 1; pinned by Task 2)
  App.xaml, App.xaml.cs        theme merge; OnStartup calls Composition.Build (Task 1)
  AppInfo.cs, ShellLayout.cs, EditorSession.cs (Task 1)
  Composition.cs               the one composition root (Task 1; Tasks 2-3 add lines; final form in Task 4 Step 21)
  MainWindow.xaml(.cs)         the skeleton with anchors (Task 1; Tasks 2-3 fill; final form in Task 4 Step 21)
  Converters/UpperCaseConverter.cs (Task 1)
  Themes/Tokens.xaml, Themes/Controls.xaml (Task 1; Controls holds RibbonGroup and RibbonGroupLabel for Tasks 3-4)
  Controls/CanvasCoordinates.cs, CanvasDrop.cs, TemplateCanvasHost.cs (Task 2)
  ViewModels/MainViewModel.cs  (Task 1; + Canvas Task 2, + Home Task 3, + Layers and Status Task 4)
  ViewModels/CanvasViewModel.cs (Task 2)
  ViewModels/HomeViewModel.cs, ModeGroupViewModel.cs, RegionsGroupViewModel.cs, RegionTileViewModel.cs, RegionSwatch.cs,
             FillStyleViewModel.cs, SettingsGroupViewModel.cs, EditGroupViewModel.cs (Task 3; HomeViewModel + File Task 4)
  ViewModels/StatusViewModel.cs, LayerThumbnails.cs, LayerRowViewModel.cs, LayersViewModel.cs, FileGroupViewModel.cs (Task 4)
  Views/CanvasView.xaml(.cs) (Task 2)
  Views/HomeStyles.xaml, HomeRibbon.xaml(.cs), FillStyleGroup.xaml(.cs), RegionsGrid.xaml(.cs) (Task 3; HomeRibbon + File group Task 4)
  Views/ThemedDialog.xaml(.cs), StatusStrip.xaml(.cs), DropZone.xaml(.cs), LayersPanel.xaml(.cs), FileGroup.xaml(.cs) (Task 4)
  Services/WindowPlacement.cs, ISettingsStore.cs, JsonSettingsStore.cs, PlacementGuard.cs, MonitorWorkAreas.cs (Task 1)
  Services/IColorPicker.cs, Win32ColorPicker.cs (Task 3)
  Services/IStatus.cs, IFileDialogs.cs, IDialogService.cs, ImageImporter.cs, ImportService.cs, Win32FileDialogs.cs,
           ThemedDialogService.cs (Task 4)
src/RBX15.App.Tests/           new project, net10.0-windows, UseWPF, xUnit 2.9.3, in src/RBX15.slnx (CI already runs the slnx)
  RBX15.App.Tests.csproj, StaThread.cs (Run, Pump, Theme), TestShell.cs, Fakes/FakeSettingsStore.cs (Task 1)
  EditorSessionTests.cs 30, PlacementGuardTests.cs 13, JsonSettingsStoreTests.cs 17, ThemeTokenTests.cs 58, ShellTests.cs 14 (Task 1)
  CanvasCoordinatesTests.cs 17, CanvasViewModelTests.cs 8, TemplateCanvasHostTests.cs 18, CanvasViewTests.cs 5 (Task 2)
  HomeTestKit.cs, Fakes/FakeColorPicker.cs, ModeGroupTests.cs 3, SettingsGroupTests.cs 4, EditGroupTests.cs 4,
    FillStyleTests.cs 22, RegionsGroupTests.cs 17, HomeRibbonViewTests.cs 5 (Task 3)
  TestFiles.cs, Fakes/FakeStatus.cs, FakeFileDialogs.cs, FakeDialogService.cs, StatusViewModelTests.cs 4,
    ImageImporterTests.cs 24, ImportServiceTests.cs 9, LayersViewModelTests.cs 12, FileGroupTests.cs 8,
    ThemedDialogTests.cs 2, Task4ViewTests.cs 4, ShellWiringTests.cs 1 (Task 4)
  LedgerScenarioTests.cs 14, ThemeKeyGuardTests.cs 1 (Task 5)
src/RBX15.slnx                 + RBX15.App.Tests (Task 1)
tools/parity-ref/
  view-ref.js, view-scenes.json (14 scenes) (Task 2)
  README.md                    the view renderer, the view scene key (Task 2)
tests/goldens/view/            14 PNGs, index.json, scenes.json; captured once by the controller, then frozen (Task 2 Step 10)
docs/port/                     parity-ledger.json, parity-ledger.md, divergences.md: 2 amended, 5 reworded, 5 added,
                                 26 narrowed, 46 closed (Task 5)
```

**Test counts.** The baseline on `main` (`1126e7c`) is Engine.Tests 566 passed and 0 skipped, Parity.Tests 35, and no App.Tests project. Every run step below states the cumulative count; nothing is skipped and Parity.Tests stays 35 throughout:

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| `main` 1126e7c | 566 | none | |
| Task 1 Step 3 | 582 | none | DocumentTests +16 |
| Task 1 Step 7 | 582 | 30 | EditorSessionTests +30 |
| Task 1 Step 10 | 582 | 60 | PlacementGuardTests +13, JsonSettingsStoreTests +17 |
| Task 1 Step 13 | 582 | 118 | ThemeTokenTests +58 |
| **Task 1 Step 16** | **582** | **132** | ShellTests +14 |
| Task 2 Step 3 | 622 | 132 | RendererViewTests +40 |
| Task 2 Step 7 | 622 | 157 | CanvasCoordinatesTests +17, CanvasViewModelTests +8 |
| Task 2 Step 8 | 622 | 175 | TemplateCanvasHostTests +18 |
| Task 2 Step 9 | 622 | 180 | CanvasViewTests +5 |
| **Task 2 Steps 11-12** | **637** | **180** | GoldenViewTests +15 |
| Task 3 Step 3 | 658 | 180 | DocumentOpsTests +21 |
| Task 3 Step 6 | 658 | 183 | ModeGroupTests +3 |
| Task 3 Step 8 | 658 | 191 | SettingsGroupTests +4, EditGroupTests +4 |
| Task 3 Step 10 | 658 | 230 | FillStyleTests +22, RegionsGroupTests +17 |
| **Task 3 Step 13** | **658** | **235** | HomeRibbonViewTests +5 |
| Task 4 Step 3 | 688 | 235 | LayerOpsTests +30 |
| Task 4 Step 5 | 695 | 235 | ExportGuardTests +7 |
| Task 4 Step 8 | 708 | 235 | ImagesCapsTests +13 |
| Task 4 Step 10 | 716 | 235 | ImagesOrientationTests +8 |
| Task 4 Step 11 | 716 | 239 | StatusViewModelTests +4 |
| Task 4 Step 12 | 716 | 263 | ImageImporterTests +24 |
| Task 4 Step 14 | 716 | 272 | ImportServiceTests +9 |
| Task 4 Step 16 | 716 | 284 | LayersViewModelTests +12 |
| Task 4 Step 18 | 716 | 292 | FileGroupTests +8 |
| Task 4 Step 20 | 716 | 298 | ThemedDialogTests +2, Task4ViewTests +4 |
| **Task 4 Step 21** | **716** | **299** | ShellWiringTests +1 |
| **Task 5 Steps 3 and 8** | **716** | **314** | LedgerScenarioTests +14, ThemeKeyGuardTests +1 |

The per-class counts are the drafters' measured counts, recounted per fact and theory row by the critic. The reconciler's additions (`CanvasViewModelTests` +1, `TemplateCanvasHostTests` +1, `ShellWiringTests` +1 and `ThemeKeyGuardTests` +1) were written after the measured runs; the implementer's run at each step is their check.

---

### Task 1: App foundation (Mvvm, RBX15.App.Tests, EditorSession, theme, window, skeleton)

**Files:**

- Create:
  - Engine: nothing new (`ContentEquals` goes into `Document.cs`).
  - App: `src/RBX15.App/app.manifest` (Task 1 is its only owner), `AppInfo.cs`, `EditorSession.cs`, `ShellLayout.cs`, `Composition.cs` (internal; the one composition root), `Converters/UpperCaseConverter.cs`, `Themes/Tokens.xaml`, `Themes/Controls.xaml`, `ViewModels/MainViewModel.cs`, `Services/WindowPlacement.cs`, `Services/ISettingsStore.cs`, `Services/JsonSettingsStore.cs`, `Services/PlacementGuard.cs`, `Services/MonitorWorkAreas.cs` (internal).
  - Tests: `src/RBX15.App.Tests/RBX15.App.Tests.csproj`, `StaThread.cs`, `TestShell.cs` (internal; the tests' one `MainViewModel` builder), `Fakes/FakeSettingsStore.cs`, `EditorSessionTests.cs`, `PlacementGuardTests.cs`, `JsonSettingsStoreTests.cs`, `ThemeTokenTests.cs`, `ShellTests.cs`.
- Modify:
  - `src/RBX15.Engine/Document.cs` (`ContentEquals`) and `src/RBX15.Engine.Tests/DocumentTests.cs` (16 cases).
  - `src/RBX15.App/RBX15.App.csproj` (package, version, manifest, fonts, `InternalsVisibleTo`).
  - `src/RBX15.App/App.xaml` and `App.xaml.cs` (theme merge, composition root).
  - `src/RBX15.App/MainWindow.xaml` and `MainWindow.xaml.cs` (the skeleton), `src/RBX15.slnx` (the test project).
- Unchanged: `src/RBX15.App/AssemblyInfo.cs`, and CI, because `.github/workflows/dotnet-ci.yml` already runs `dotnet test src/RBX15.slnx`.
- Read only: `styles.css` (the token test parses it), `editor.html` (the shell tests quote it), `assets/fonts/{inter,spacegrotesk,jetbrainsmono}/*.ttf` (embedded through `Link`; the files are not touched).

**Interfaces:**

- Consumes:
  - `Document`, `Layer` (`src/RBX15.Engine/Document.cs`, M1), `Fill` (`Fills.cs`, M1), `TemplateMode` (`Template.cs`, M1).
  - `History` (`Record`/`Undo`/`Redo`, capacity 30) and `EditorSnapshot` (`History.cs`, M2a).
  - `ProjectIO.AppName` (`ProjectIO.cs`, M2b), `RepoRoot.Find` (`src/RBX15.Parity/RepoRoot.cs`, M1).
- Produces (names verbatim from `interfaces.md`, plus the additions listed at the end of this task):
  - Engine: `public bool Document.ContentEquals(Document other)`.
  - `RBX15.App`:
    - `SessionChange` (`[Flags]`: `None`, `Document`, `Selection`, `History`, `Title`) and `SessionChangedEventArgs(SessionChange what)` with `What`.
    - `EditorSession(Document? document = null, int historyCapacity = 30)`. State: `Document`, `SelectedLayerId`, `SelectedLayer`, `NextId`, `CurrentFile`, `IsDirty`, `TemplateTitle { get; set; }`, `CanUndo`, `CanRedo`, `UndoCount`, `RedoCount`, `InGesture`, event `Changed`. Methods: `TakeId()`, `Apply(Func<Document, Document>)`, `ApplyWithSelection(Func<EditorSnapshot, EditorSnapshot>)`, `Select(int?)`, `BeginGesture()`, `Undo()`, `Redo()`.
    - `AppInfo.ProductName`, `AppInfo.Version`, and `ShellLayout` (addition).
    - `MainWindow(MainViewModel viewModel, ISettingsStore settings, IReadOnlyList<Rect>? workAreas = null)`, with the named regions `RibbonPane`, `LeftPanelContent` and `CanvasArea` (addition).
    - `App.SettingsDirectory` (addition).
    - `internal static class Composition { public static MainViewModel Build(EditorSession session, Func<Window?> owner) }` with the two anchor lines `        // M3a: construction lines above this line.` and `            // M3a: required members above this line.` (addition; Tasks 2 to 4 insert above them).
  - `RBX15.App.ViewModels`: `MainViewModel(EditorSession session)` with `Session`, `Title`, `HeaderTitle`, `HeaderByline`, `FooterText`.
  - `RBX15.App.Services`:
    - `WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized)`, `ISettingsStore`.
    - `JsonSettingsStore(string directory)` with `FileName` and `FilePath` (additions).
    - `PlacementGuard.Resolve(WindowPlacement?, IReadOnlyList<Rect>, double defaultWidth = 1200, double defaultHeight = 800)`.
  - `RBX15.App.Converters.UpperCaseConverter` (addition).
  - Theme: the resource keys listed under "Interface additions adopted (Task 1)" at the end of this task, including `RibbonGroup` (Border) and `RibbonGroupLabel` (TextBlock), which Tasks 3 and 4 share.
  - Tests: `RBX15.App.Tests` (net10.0-windows), `StaThread.Run(Action)`, `StaThread.Run<T>(Func<T>)`, `StaThread.Theme`, `StaThread.Pump()`, `internal static class TestShell { public static MainViewModel Main(EditorSession session) }` with the same two anchor lines as `Composition`, `Fakes/FakeSettingsStore`.

**What 4.1 does (cited).**

- **Window** (main.js:5-13): `BrowserWindow` 1200x800, centered by Electron, title `RBX15 Classic Shirt and Pants Maker` (main.js:8, editor.html:6). No minimum size, and nothing persisted: no localStorage, no config file (ledger `shell-no-persistence`).
- **Layout** (styles.css:99-108, editor.html:15-343): a column of header, `.ribbon`, `.app` and footer.
  - `.app` (styles.css:241) holds `.panel` (styles.css:246-253: 280 px; 180deg gradient from navy-deep to ink-900; 1px border-1 on the right; padding 14; scrolls vertically) and `.canvas-area` (styles.css:441-450: ink-950).
  - `@media (max-width: 900px)` narrows the panel to 200 (styles.css:694-697).
- **Header** (styles.css:129-155, editor.html:15-18):
  - Padding 10/24, gap 16, a 135deg gradient navy, navy-deep, navy; a 1px border-accent bottom; shadow `0 2px 8px rgba(0,0,0,.35)`.
  - The h1 `RBX15 Classic Shirt &amp; Pants Maker` is Space Grotesk 20px/600, filled with the brand gradient.
  - The span `ItsjustEste | v4.1` is JetBrains Mono 11px fg-3, uppercase.
- **Ribbon chrome**:
  - `.ribbon` (styles.css:160-164): navy-deep to ink-900, 1px border-1 bottom.
  - `.ribbon-tabs` (styles.css:165-170): padding 0 8, ink-950 to ink-900, 1px border-1 bottom.
  - `.ribbon-tab` (styles.css:171-196): mono 11px/600 uppercase, padding 6/16, fg-3, hover fg-2. Active is cyan on navy-deep with a 2px brand-gradient underline at bottom -1px.
  - `.ribbon-pane.active` (styles.css:197-198): padding 8/12, scrolls horizontally.
  - Tabs: Home is active by default (editor.html:23). Switching is in editor.js:4-13.
- **Footer** (styles.css:678-689, editor.html:343): `Developed at 626 Labs LLC`, mono 10px fg-muted, uppercase, centered, padding 7/24, 1px border-1 top, gradient ink-950 to ink-900.
- **Tokens** (styles.css:8-92): parsed and checked by `ThemeTokenTests`. The UI fonts load from Google at runtime (styles.css:6); v5 embeds the vendored files (ledger `font-ui-runtime-google`, fix).
- **Undo** (editor.js:1404-1487):
  - A snapshot is pushed BEFORE each mutation (`saveUndo`, editor.js:1418), with depth 30 (editor.js:86).
  - Every restore deselects (editor.js:1451), and `nextId` only grows (editor.js:1465).
  - Mask and Grid changes are captured but never pushed (editor.js:1494-1495).
  - No-op actions push empty steps, and presets push two (ledger `undo-empty-steps`).
  - `#exportTitle` (editor.html:100) is not in the snapshot.

**Rulings made in this task** (each pinned by a named test or carried to Task 5):

1. **No right panel (interface sheet §9 item 1).** The contract asks for "empty left, center and right panels". 4.1's DOM has only the left `.panel` and the `.canvas-area` (editor.html:288-341). `.rpanel` is styled at styles.css:462-469 but no element uses it. Ledger `shell-layout-skeleton` says "Drop the dead .rpanel". Task 1 builds the left panel (280, or 200 at the breakpoint) and the center only. Task 4 puts the drop zone and layers in `LeftPanelContent`, where 4.1 has them (editor.html:311-318). Spec §4's "right panel (layers, properties, adjustments)" describes a layout 4.1 never shipped, and the ledger row, which cites the code, wins. This deviates from the contract's wording, so the plan header lists it for Este's OK (contract amendment CA2).
2. **The fixed ribbon height (`shell-layout-skeleton`, disposition fix) waits for M4.** The divergence is "4.1: ribbon height changes per tab". M3a shows one tab, so nothing switches and there is no second pane to measure the fixed height against. The ribbon row stays `Auto`. Task 5 must **not** close `shell-layout-skeleton`. M4 sets the height when Layout, Details and Text arrive.
3. **The header byline is `ItsjustEste | v5.0`** (major.minor). `AppInfo.Version` stays `"5.0.0"` as the interface sheet fixes it. The row `shell-header-version-hardcoded` asks for the header to read `v<major.minor>`, matching 4.1's `v4.1` for `4.1.0`. `IncludeSourceRevisionInInformationalVersion=false` stops the SDK from appending `+<sha>`, and `AppInfo` also strips any `+` suffix.
4. **Weight 600 in Space Grotesk and JetBrains Mono draws the Bold (700) instance.** This was measured on the vendored variable files. WPF exposes their named instances as separate faces, but it lists no 600 instance for these two families (Space Grotesk: Light, Regular, Medium, Bold; JetBrains Mono: Thin through ExtraBold without SemiBold). Inter has a real SemiBold. The CSS asks for 600 on the h1, the ribbon tabs and the group and panel labels. Vendoring static 600 cuts would mean new font files and new manifest rows. Instead, `Ui_fonts_resolve_to_the_vendored_files` pins the mapping, and Task 5 adds the divergence row `font-ui-weight-600` (text below).
5. **CSS `letter-spacing` and the 120/220 ms hover transitions are not in M3a.** WPF has no letter-spacing property, and the transitions belong with the look. Both go to the M3b side-by-side sign-off. Task 5 must **not** close `theme-tokens-shape-motion-type`, `shell-header`, `shell-footer` or `ribbon-tabs-switching`. The motion tokens (`DurFastDuration`, `DurMedDuration`, `EaseOutSpline`) are defined and tested now, so M3b only wires them.
6. **Window minimum size is 640x480.** The row `shell-window-size` says to decide it. 4.1 had none, and its panels overlap when shrunk. 640x480 is the floor in the row's own parity check. Task 5 appends this to the row's divergence text.
7. **The settings file is `%LOCALAPPDATA%\RBX15\settings.json`.** The row `shell-no-persistence` warns against a settings store that `PRIVACY.md` does not disclose. Spec §7 puts the docs correction in M6, and nothing ships before M6. Task 5 must **not** close `shell-no-persistence` or `shell-userdata-footprint`. The M6 docs task discloses this file next to the autosave file and the recents list.
8. **Monitor coordinates.** `MonitorWorkAreas` divides each monitor's physical work area by that monitor's own scale. That is how WPF reports `Left` and `Top` for a per-monitor-v2 window on that monitor. A mixed-DPI desk could not be checked while drafting (one monitor), so Step 16's smoke run includes a two-monitor check.
9. **The 135-degree gradients use `0,0` to `1,1` in the bounding box.** That is the row `shell-header`'s own port target. It matches CSS on a square box. On the wide header it puts cyan at the left and magenta at the right, which is what the row's parity check asks. The exact angle is part of the M3b look sign-off.
10. **The scrollbar is 8 DIP wide.** The `theme-scrollbars` port target says "about 8 px". The 4.1 screenshots are resampled 1920x1080 store frames, and their scrollbar spans about 8 to 10 resampled pixels, which is not a measurement. The colors are exact and tested. The width is part of the M3b look sign-off.
11. **`glow-duo` is one layer.** WPF allows one `DropShadowEffect` per element, so `GlowDuoEffect` keeps the 24 px cyan layer and drops the 48 px magenta halo. The row `theme-tokens-shape-motion-type` says to tune the glows visually.
12. **EditorSession details the sheet left open.** Both `Apply` and `ApplyWithSelection` return true iff the content changed. Inside a gesture that change becomes part of the gesture's one step. The sheet's "true iff a step" for `ApplyWithSelection` is read the same way, so the two methods agree. Adopting any document raises `NextId` past its highest id (editor.js:1465 `Math.max`). `TemplateTitle = null` stores `""`. `CanUndo` is also true while an open gesture has changed the content, because Undo commits that gesture first.
13. **StaThread is one shared thread.** It keeps the signature the sheet fixes and runs every call on one long-lived STA thread whose dispatcher is running. That thread owns a plain `Application` holding `Themes/Controls.xaml`, the dictionary `App.xaml` merges. It is not `RBX15.App.App`: the `Application` constructor queues its startup on the dispatcher, and `App.OnStartup` would open the real main window. A drafting run with `StartupUri` still set crashed the test host with "Cannot locate resource 'mainwindow.xaml'".
14. **`StaThread.Pump()` runs the dispatcher queue once.** A parsed view's bindings attach at `DispatcherPriority.DataBind`, so inside one `StaThread.Run` call they stay `Unattached` until the queue runs (measured by Task 2's drafter). Every view test in Tasks 2 to 4 calls `StaThread.Pump()` after it sets `DataContext` and before it reads a bound value.
15. **One composition root and one test builder.** `Composition.Build` (App) and `TestShell.Main` (tests) are the only places a `MainViewModel` is constructed. Each later task adds a `required` member to `MainViewModel`, and each adds its construction line above `// M3a: construction lines above this line.` and its member above `// M3a: required members above this line.` in both files, so no earlier test breaks with `CS9035`. Task 4 Step 21 prints both files in full in their final M3a form.
16. **`MainWindow.xaml` carries every anchor later tasks fill**, so no later task restructures it: `xmlns:views` on the root, `RibbonPane` as a `ContentControl` (Task 3 puts `HomeRibbon`, which scrolls itself, inside it), `<!-- T4: left panel -->` inside `LeftPanelContent`, `CanvasArea` as a `Grid` (Task 2 adds the `CanvasView` child), and an Auto row 3 holding `<!-- T4: status strip -->` above the footer. Task 4 Step 21 prints the final file in full.

**Measured while drafting** (a scratch copy of `main` at `1126e7c`, .NET SDK 10.0.302, CommunityToolkit.Mvvm 8.4.2): every step's code compiled under `-warnaserror` and produced the Expected lines below, and the final state passed three full runs in a row.

- **CommunityToolkit.Mvvm 8.4.2** is the newest stable release on nuget.org (published 2026-03-25; 8.4.1 was published 2026-03-18). It is in the local NuGet cache.
- **Startup checked.** The built app launched: the title bar read `RBX15 Classic Shirt and Pants Maker`, the header `ITSJUSTESTE | V5.0`, and the footer `DEVELOPED AT 626 LABS LLC`. Closing it wrote `settings.json` with `{ "window": { "left": 360, "top": 116, "width": 1200, "height": 800, "maximized": false } }` on a 1920x1032 work area. The drafting run deleted that file afterwards.
- **Reconciler changes since that run** (written to the critic's fixes, not compiled in the measured run; the implementer's build at each step is their check): `StaThread.Pump`, `Composition.cs`, `TestShell.cs`, the `MainWindow.xaml` anchors (rulings 14 to 16), `ShellTests` building through `TestShell`, and the `RibbonGroup` (Border) and `RibbonGroupLabel` styles moved into `Controls.xaml` from Task 3. None of them changes a test count.

| After step | Engine.Tests | Parity.Tests | App.Tests |
| --- | --- | --- | --- |
| 1 (baseline) | 566 | 35 | none |
| 3 | 582 (566 + 16) | 35 | none |
| 7 | 582 | 35 | 30 |
| 10 | 582 | 35 | 60 |
| 13 | 582 | 35 | 118 |
| 16 | 582 | 35 | 132 |

Task 1 adds **Engine.Tests +16, App.Tests +132, Parity.Tests +0**.

- [ ] **Step 1: Baseline**

M3a builds on `main` at `1126e7c`, on `feat/v5-m3-shell`, which the controller made.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 566, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`.

- [ ] **Step 2: Write the failing Document.ContentEquals tests** (ledger `undo-empty-steps`, `file-undo-snapshot-scope`)

In `src/RBX15.Engine.Tests/DocumentTests.cs`, add `using System.Collections.Immutable;` above `using SkiaSharp;`. Then add these members after `With_expressions_share_the_bitmap` (the last fact, before the class's closing brace):

```csharp
    // ---- ContentEquals (spec §5: the session records a step only when content changed) ----

    static readonly SKImage Red = Px.Solid(2, 2, SKColors.Red);

    static Document Rich() => new()
    {
        BgColor = "#0f1f31",
        RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, 16))),
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Red, Name = "Image 1", X = 10 }),
    };

    [Fact] // two edits that rebuild equal lists are the same content; record == compares the lists by reference
    public void Rebuilt_lists_with_equal_items_are_the_same_content()
    {
        var a = Rich();
        var b = Rich();
        Assert.NotEqual(a, b);
        Assert.True(a.ContentEquals(b));
        Assert.True(b.ContentEquals(a));
        Assert.True(a.ContentEquals(a));
        Assert.True(new Document().ContentEquals(new Document()));
    }

    [Fact] // a no-op with expression keeps content (undo-empty-steps)
    public void A_with_expression_that_sets_the_same_values_keeps_content() =>
        Assert.True(Rich().ContentEquals(Rich() with { BgColor = "#0f1f31", ShowMask = true, Mode = TemplateMode.Shirt }));

    public static TheoryData<string> Changes => new()
    {
        "Mode", "BgColor", "BgOpacity", "BgImage", "ShowMask", "ShowGrid",
        "fill added", "fill value", "fill order", "layer added", "layer field", "layer image", "layer order",
    };

    [Theory, MemberData(nameof(Changes))] // file-undo-snapshot-scope: every Document field is content
    public void Every_document_field_is_content(string change)
    {
        var a = Rich();
        var two = a with
        {
            RegionFills = a.RegionFills.Add(("torso_back", Fill.Solid("#333"))),
            Layers = a.Layers.Add(new Layer { Id = 2, Image = Red }),
        };
        var (before, after) = change switch
        {
            "Mode" => (a, a with { Mode = TemplateMode.Pants }),
            "BgColor" => (a, a with { BgColor = "#0F1F31" }),
            "BgOpacity" => (a, a with { BgOpacity = 0.5 }),
            "BgImage" => (a, a with { BgImage = Red }),
            "ShowMask" => (a, a with { ShowMask = false }),
            "ShowGrid" => (a, a with { ShowGrid = true }),
            "fill added" => (a, a with { RegionFills = two.RegionFills }),
            "fill value" => (a, a with { RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 90, 16))) }),
            "fill order" => (two, two with { RegionFills = two.RegionFills.Reverse() }),
            "layer added" => (a, a with { Layers = two.Layers }),
            "layer field" => (a, a with { Layers = ImmutableList.Create(a.Layers[0] with { Visible = false }) }),
            "layer image" => (a, a with { Layers = ImmutableList.Create(a.Layers[0] with { Image = Px.Solid(2, 2, SKColors.Red) }) }),
            "layer order" => (two, two with { Layers = two.Layers.Reverse() }),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        Assert.False(before.ContentEquals(after), change);
        Assert.False(after.ContentEquals(before), change);
    }

    [Fact] // a null other is never equal content
    public void Null_is_never_the_same_content() => Assert.False(new Document().ContentEquals(null!));
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS1061: 'Document' does not contain a definition for 'ContentEquals'` at `DocumentTests.cs`.

- [ ] **Step 3: Implement Document.ContentEquals**

In `src/RBX15.Engine/Document.cs`, add this member to `Document` after `public bool ShowGrid { get; init; }` (the record's last property), with one blank line before it:

```csharp
    /// <summary>
    /// True when <paramref name="other"/> holds the same content: every scalar field equal,
    /// <see cref="BgImage"/> by reference, <see cref="RegionFills"/> sequence-equal (region key and
    /// <see cref="Fill"/> value equality) and <see cref="Layers"/> sequence-equal (<see cref="Layer"/>
    /// record equality, so each layer's image by reference). The compiler's record equality compares
    /// the two immutable lists by reference, so it calls two equal edits different. The editor session
    /// records an undo step only when this returns false (spec §5, ledger undo-empty-steps: 4.1 pushed
    /// a snapshot before no-op actions, editor.js:1117, :1601-1606, :1734-1749).
    /// </summary>
    public bool ContentEquals(Document other) =>
        other is not null &&
        (ReferenceEquals(this, other) ||
         Mode == other.Mode &&
         string.Equals(BgColor, other.BgColor, StringComparison.Ordinal) &&
         BgOpacity.Equals(other.BgOpacity) &&
         ReferenceEquals(BgImage, other.BgImage) &&
         ShowMask == other.ShowMask &&
         ShowGrid == other.ShowGrid &&
         RegionFills.SequenceEqual(other.RegionFills) &&
         Layers.SequenceEqual(other.Layers));
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.Engine.Tests.DocumentTests"`
Expected: `Passed!  - Failed:     0, Passed:    22, Skipped:     0, Total:    22` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 582, Failed: 0, Skipped: 0` (566 + 16); Parity.Tests `Passed: 35, Failed: 0`.

- [ ] **Step 4: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/Document.cs src/RBX15.Engine.Tests/DocumentTests.cs
git commit -m "feat(engine): Document.ContentEquals for one-step undo"
```

- [ ] **Step 5: App project settings, the manifest and the test project** (ledger `shell-header-version-hardcoded`, `cross-window-dpi-rendering`, `font-ui-runtime-google`)

Replace `src/RBX15.App/RBX15.App.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <!-- The header reads the version from the assembly (ledger shell-header-version-hardcoded).
         The SDK would append "+<commit sha>" to the informational version; AppInfo shows it bare. -->
    <Version>5.0.0</Version>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RBX15.Engine\RBX15.Engine.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.App.Tests" />
  </ItemGroup>

  <!-- The 3 UI families (styles.css:6, styles.css:89-91), embedded from the read-only vendored set
       (assets/fonts/README.md rows inter, spacegrotesk, jetbrainsmono). Link renames each file so the
       pack URI has no brackets or commas: pack://application:,,,/RBX15.App;component/Fonts/#Inter.
       Their OFL.txt files reach the output through the engine's Content items. -->
  <ItemGroup>
    <Resource Include="..\..\assets\fonts\inter\Inter[opsz,wght].ttf" Link="Fonts\Inter.ttf" />
    <Resource Include="..\..\assets\fonts\spacegrotesk\SpaceGrotesk[wght].ttf" Link="Fonts\SpaceGrotesk.ttf" />
    <Resource Include="..\..\assets\fonts\jetbrainsmono\JetBrainsMono[wght].ttf" Link="Fonts\JetBrainsMono.ttf" />
  </ItemGroup>

</Project>
```

Create `src/RBX15.App/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="5.0.0.0" name="RBX15.App"/>
  <!-- Spec §4 "Display scaling: per-monitor DPI v2" (ledger cross-window-dpi-rendering). -->
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10 and 11 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

Create `src/RBX15.App.Tests/RBX15.App.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="System.IO" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RBX15.App\RBX15.App.csproj" />
    <ProjectReference Include="..\RBX15.Engine\RBX15.Engine.csproj" />
    <ProjectReference Include="..\RBX15.Parity\RBX15.Parity.csproj" />
  </ItemGroup>

</Project>
```

Replace `src/RBX15.slnx` with:

```xml
<Solution>
  <Project Path="RBX15.App.Tests/RBX15.App.Tests.csproj" />
  <Project Path="RBX15.App/RBX15.App.csproj" />
  <Project Path="RBX15.Engine.Tests/RBX15.Engine.Tests.csproj" />
  <Project Path="RBX15.Engine/RBX15.Engine.csproj" />
  <Project Path="RBX15.Parity.Tests/RBX15.Parity.Tests.csproj" />
  <Project Path="RBX15.Parity/RBX15.Parity.csproj" />
</Solution>
```

Create `src/RBX15.App.Tests/StaThread.cs`:

```csharp
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace RBX15.App.Tests;

/// <summary>
/// Runs test code that needs WPF on one shared STA thread with a running <see cref="Dispatcher"/>.
/// That thread owns the process's one <see cref="System.Windows.Application"/>, holding the theme App.xaml merges
/// (Themes/Controls.xaml, which merges Themes/Tokens.xaml), so views built in a test resolve their
/// StaticResource keys as they do in the app. It is a plain Application, not RBX15.App.App: the
/// Application constructor queues its startup on the dispatcher, and App's OnStartup would open the
/// real main window. WPF objects belong to the thread that made them, so a test builds and inspects
/// them inside one call. Calls from parallel test classes queue on the thread and run one at a time.
/// </summary>
internal static class StaThread
{
    /// <summary>The dictionary App.xaml merges, as a component path (the pack scheme exists only once WPF has started).</summary>
    public const string Theme = "/RBX15.App;component/Themes/Controls.xaml";

    static readonly Lazy<Dispatcher> Ui = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static void Run(Action body) => Run<object?>(() => { body(); return null; });

    /// <summary>
    /// Runs the current dispatcher's queue once, down to Background priority. A parsed view's bindings
    /// attach at DataBind priority, so inside one Run call they stay Unattached until the queue runs.
    /// View tests call it after setting DataContext, on the StaThread.
    /// </summary>
    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

    public static T Run<T>(Func<T> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        Ui.Value.Invoke(() =>
        {
            try { result = body(); }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
        });
        failure?.Throw();
        return result;
    }

    static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        Exception? error = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri(Theme, UriKind.Relative) });
            }
            catch (Exception e) { error = e; }
            ready.Set();
            if (error is null) Dispatcher.Run();
        })
        { IsBackground = true, Name = "RBX15.App.Tests STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (error is not null) throw new InvalidOperationException("The test STA thread failed to start.", error);
        return dispatcher!;
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`. The package restore pulls CommunityToolkit.Mvvm 8.4.2, and `RBX15.App.Tests` builds with no tests yet.

Check the pin against nuget.org (contract: an exact stable version at least two weeks old; a build-time query, not an app request):

Run: `dotnet package search CommunityToolkit.Mvvm --exact-match`
Expected: a row for `CommunityToolkit.Mvvm` that lists version `8.4.2`. 8.4.2 was published 2026-03-25, more than two weeks before this plan. If the query lists a newer stable version, keep 8.4.2 and name the newer one in the commit body; a version change is the controller's call.

- [ ] **Step 6: Write the failing EditorSession tests** (ledger `undo-empty-steps`, `file-undo-snapshot-scope`, `undo-coverage-gaps`, `undo-restore-ui-side-effects`, `fillstyle-undo-snapshot-timing`, `history-snapshot-coupling`)

Create `src/RBX15.App.Tests/EditorSessionTests.cs`:

```csharp
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// EditorSession: one undo step per document change (spec §5; ledger undo-empty-steps,
/// file-undo-snapshot-scope, undo-coverage-gaps, fillstyle-undo-snapshot-timing), selection that survives
/// undo (undo-restore-ui-side-effects), and an id counter that only grows (editor.js:499, :1465).
/// </summary>
public class EditorSessionTests
{
    static readonly SKImage Pixel = MakePixel();

    static SKImage MakePixel()
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    static Layer L(int id) => new() { Id = id, Image = Pixel, Name = "Image " + id };

    static Document WithLayers(Document d, params int[] ids) => d with { Layers = d.Layers.AddRange(ids.Select(L)) };

    /// <summary>Every Changed event's flags, in order.</summary>
    static List<SessionChange> Events(EditorSession s)
    {
        var seen = new List<SessionChange>();
        s.Changed += (_, e) => seen.Add(e.What);
        return seen;
    }

    // ---- defaults ----

    [Fact] // editor.js:76-107 (startup state), :86 (depth 30), :499 (nextId = 1)
    public void A_new_session_is_4_1s_startup_state()
    {
        var s = new EditorSession();
        Assert.True(s.Document.ContentEquals(new Document()));
        Assert.Null(s.SelectedLayerId);
        Assert.Null(s.SelectedLayer);
        Assert.Equal(1, s.NextId);
        Assert.Null(s.CurrentFile);
        Assert.False(s.IsDirty);
        Assert.Equal("", s.TemplateTitle);
        Assert.False(s.CanUndo);
        Assert.False(s.CanRedo);
        Assert.False(s.InGesture);
        Assert.Equal((0, 0), (s.UndoCount, s.RedoCount));
    }

    [Fact] // the id counter starts above every id the starting document holds (editor.js:1465)
    public void A_starting_document_sets_the_id_counter_above_its_ids()
    {
        var s = new EditorSession(WithLayers(new Document(), 4, 9, 2));
        Assert.Equal(10, s.NextId);
        Assert.Equal(10, s.TakeId());
        Assert.Equal(11, s.NextId);
    }

    // ---- Apply ----

    [Fact] // one change, one step (spec §5)
    public void A_change_is_one_step_and_raises_document_and_history()
    {
        var s = new EditorSession();
        var seen = Events(s);

        Assert.True(s.Apply(d => d with { BgColor = "#000000" }));

        Assert.Equal("#000000", s.Document.BgColor);
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.IsDirty);
        Assert.Equal([SessionChange.Document | SessionChange.History], seen);
    }

    [Fact] // undo-empty-steps: a no-op pushes nothing (4.1 pushed, editor.js:1601-1606)
    public void A_change_that_keeps_the_content_records_nothing_and_raises_nothing()
    {
        var s = new EditorSession();
        var seen = Events(s);

        Assert.False(s.Apply(d => d with { BgColor = "#192e44" }));
        Assert.False(s.Apply(d => d));
        Assert.False(s.Apply(d => d with { Layers = d.Layers.Clear() }));

        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Empty(seen);
    }

    [Fact] // undo-empty-steps: 4.1's presets pushed two snapshots (editor.js:1734-1749); one lambda is one step
    public void Several_edits_in_one_change_are_one_step()
    {
        var s = new EditorSession();
        s.Apply(d => WithLayers(d, 1, 2, 3) with { ShowGrid = true, BgOpacity = 0.5 });
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.Undo());
        Assert.True(s.Document.ContentEquals(new Document()));
    }

    [Theory] // file-undo-snapshot-scope: every Document field is a step, including Mask and Grid (4.1 never pushed for them, editor.js:1494-1495)
    [InlineData("mask")]
    [InlineData("grid")]
    [InlineData("mode")]
    [InlineData("fill")]
    [InlineData("visibility")]
    public void Every_document_edit_is_undoable(string edit)
    {
        var s = new EditorSession(WithLayers(new Document(), 1));
        var start = s.Document;
        s.Apply(edit switch
        {
            "mask" => d => d with { ShowMask = false },
            "grid" => d => d with { ShowGrid = true },
            "mode" => d => d with { Mode = TemplateMode.Pants },
            "fill" => d => d with { RegionFills = d.RegionFills.Add(("torso_front", Fill.Solid("#ff8800"))) },
            "visibility" => d => d with { Layers = [d.Layers[0] with { Visible = false }] },
            _ => throw new ArgumentOutOfRangeException(nameof(edit)),
        });
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.Undo());
        Assert.True(s.Document.ContentEquals(start));
    }

    [Fact] // the change runs before anything is touched
    public void A_change_that_throws_changes_nothing()
    {
        var s = new EditorSession();
        var before = s.Document;
        var seen = Events(s);

        Assert.Throws<ArgumentException>(() => s.Apply(_ => throw new ArgumentException("bad region")));
        Assert.Throws<InvalidOperationException>(() => s.Apply(_ => null!));

        Assert.Same(before, s.Document);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Empty(seen);
    }

    [Fact] // a document change that removes the selected layer drops the selection
    public void Removing_the_selected_layer_drops_the_selection()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        s.Select(2);
        var seen = Events(s);

        s.Apply(d => d with { Layers = d.Layers.RemoveAll(l => l.Id == 2) });

        Assert.Null(s.SelectedLayerId);
        Assert.Equal([SessionChange.Document | SessionChange.History | SessionChange.Selection], seen);
    }

    [Fact] // observers see the finished state when Changed fires
    public void Changed_fires_after_the_state_is_updated()
    {
        var s = new EditorSession();
        (string Bg, int Undo, bool Dirty)? observed = null;
        s.Changed += (_, _) => observed = (s.Document.BgColor, s.UndoCount, s.IsDirty);
        s.Apply(d => d with { BgColor = "#123456" });
        Assert.Equal(("#123456", 1, true), observed);
    }

    // ---- ApplyWithSelection ----

    [Fact] // addLayer pushes, adds on top and selects (editor.js:500-526)
    public void Adding_and_selecting_is_one_step_with_a_selection_change()
    {
        var s = new EditorSession();
        var seen = Events(s);

        bool step = s.ApplyWithSelection(snap =>
        {
            int id = s.TakeId();
            return new EditorSnapshot(WithLayers(snap.Document, id), id);
        });

        Assert.True(step);
        Assert.Equal(1, s.SelectedLayerId);
        Assert.Equal("Image 1", s.SelectedLayer!.Name);
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(2, s.NextId);
        Assert.Equal([SessionChange.Document | SessionChange.History | SessionChange.Selection], seen);
    }

    [Fact] // the selection is adopted even when the content is unchanged; no step
    public void A_selection_only_result_raises_selection_and_records_nothing()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        var seen = Events(s);

        Assert.False(s.ApplyWithSelection(snap => snap with { SelectedLayerId = 2 }));

        Assert.Equal(2, s.SelectedLayerId);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.Selection], seen);
    }

    [Fact] // an id the returned document lacks selects nothing
    public void A_selection_the_document_lacks_becomes_null()
    {
        var s = new EditorSession(WithLayers(new Document(), 1));
        s.Select(1);
        s.ApplyWithSelection(snap => new EditorSnapshot(snap.Document with { ShowGrid = true }, 99));
        Assert.Null(s.SelectedLayerId);
    }

    // ---- Select ----

    [Fact] // selectLayer (editor.js:575) is never an undo step
    public void Select_changes_only_the_selection()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        var seen = Events(s);

        s.Select(2);
        s.Select(2);
        s.Select(7);
        s.Select(null);

        Assert.Null(s.SelectedLayerId);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.Selection, SessionChange.Selection], seen);
    }

    // ---- Undo and Redo ----

    [Fact] // btnUndo/btnRedo (editor.js:1477-1486); nothing to do returns false and raises nothing
    public void Undo_and_redo_walk_the_steps()
    {
        var s = new EditorSession();
        s.Apply(d => d with { BgColor = "#000001" });
        s.Apply(d => d with { BgColor = "#000002" });
        var seen = Events(s);

        Assert.True(s.Undo());
        Assert.Equal("#000001", s.Document.BgColor);
        Assert.True(s.Undo());
        Assert.Equal("#192e44", s.Document.BgColor);
        Assert.False(s.Undo());
        Assert.Equal((0, 2), (s.UndoCount, s.RedoCount));
        Assert.True(s.Redo());
        Assert.True(s.Redo());
        Assert.False(s.Redo());
        Assert.Equal("#000002", s.Document.BgColor);
        Assert.Equal(4, seen.Count);
        Assert.All(seen, w => Assert.Equal(SessionChange.Document | SessionChange.History, w));
    }

    [Fact] // a new edit after undo abandons the redo branch (lib/history.js:33-36)
    public void A_new_edit_drops_redo()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        s.Undo();
        Assert.True(s.CanRedo);
        s.Apply(d => d with { ShowMask = false });
        Assert.False(s.CanRedo);
    }

    [Fact] // undo-restore-ui-side-effects (fix): the selection survives when its layer still exists
    public void Selection_survives_undo_when_the_layer_exists()
    {
        var s = new EditorSession(WithLayers(new Document(), 1));
        s.Select(1);
        s.Apply(d => d with { ShowGrid = true });

        Assert.True(s.Undo());
        Assert.Equal(1, s.SelectedLayerId);
        Assert.True(s.Redo());
        Assert.Equal(1, s.SelectedLayerId);
    }

    [Fact] // undoing an add removes the layer, so its selection goes too
    public void Undoing_an_add_drops_the_selection_of_the_removed_layer()
    {
        var s = new EditorSession();
        s.ApplyWithSelection(snap => { int id = s.TakeId(); return new EditorSnapshot(WithLayers(snap.Document, id), id); });
        var seen = Events(s);

        Assert.True(s.Undo());

        Assert.Empty(s.Document.Layers);
        Assert.Null(s.SelectedLayerId);
        Assert.Equal([SessionChange.Document | SessionChange.History | SessionChange.Selection], seen);
    }

    [Fact] // nextId only grows (editor.js:1465): an undone add never gives its id back
    public void Undo_never_lowers_the_id_counter()
    {
        var s = new EditorSession();
        s.ApplyWithSelection(snap => { int id = s.TakeId(); return new EditorSnapshot(WithLayers(snap.Document, id), id); });
        s.Undo();
        Assert.Equal(2, s.NextId);
        s.Redo();
        Assert.Equal(2, s.NextId);
        s.Apply(d => WithLayers(d, 40));
        Assert.Equal(41, s.NextId);
    }

    [Fact] // depth 30 (editor.js:86)
    public void Undo_depth_is_thirty()
    {
        var s = new EditorSession();
        for (int i = 1; i <= 31; i++) { int n = i; s.Apply(d => d with { BgOpacity = n / 100.0 }); }
        Assert.Equal(30, s.UndoCount);
        while (s.Undo()) { }
        Assert.Equal(0.01, s.Document.BgOpacity);
    }

    // ---- TemplateTitle ----

    [Fact] // #exportTitle (editor.html:100) is not document state: no step, not dirty (file-undo-snapshot-scope)
    public void The_template_title_raises_title_and_is_never_a_step()
    {
        var s = new EditorSession();
        var seen = Events(s);

        s.TemplateTitle = "My Shirt";
        s.TemplateTitle = "My Shirt";
        s.TemplateTitle = null!;

        Assert.Equal("", s.TemplateTitle);
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.Title, SessionChange.Title], seen);
    }

    // ---- gestures (fillstyle-undo-snapshot-timing) ----

    [Fact] // one slider drag is one step, recorded when the drag ends
    public void A_gesture_is_one_step_recorded_when_it_closes()
    {
        var s = new EditorSession();
        var seen = Events(s);

        using (s.BeginGesture())
        {
            Assert.True(s.InGesture);
            for (int i = 1; i <= 5; i++) { int n = i; Assert.True(s.Apply(d => d with { BgOpacity = n / 10.0 })); }
            Assert.Equal(0, s.UndoCount);
            Assert.False(s.IsDirty);
            Assert.True(s.CanUndo);
        }

        Assert.False(s.InGesture);
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.IsDirty);
        Assert.Equal(Enumerable.Repeat(SessionChange.Document, 5).Append(SessionChange.History), seen);
        Assert.True(s.Undo());
        Assert.Equal(1, s.Document.BgOpacity);
    }

    [Fact] // a drag that ends where it started records nothing (editor.js:1570-1579 recorded nothing for an idle drag)
    public void A_gesture_that_returns_to_its_start_records_nothing()
    {
        var s = new EditorSession();
        using (s.BeginGesture())
        {
            s.Apply(d => d with { BgOpacity = 0.3 });
            s.Apply(d => d with { BgOpacity = 1 });
        }
        Assert.Equal(0, s.UndoCount);
        Assert.False(s.IsDirty);
    }

    [Fact] // scopes nest by count; disposing one twice does nothing
    public void Nested_gestures_record_once_at_the_outermost_close()
    {
        var s = new EditorSession();
        var outer = s.BeginGesture();
        var inner = s.BeginGesture();
        s.Apply(d => d with { ShowGrid = true });
        inner.Dispose();
        inner.Dispose();
        Assert.True(s.InGesture);
        Assert.Equal(0, s.UndoCount);
        s.Apply(d => d with { ShowMask = false });
        outer.Dispose();
        outer.Dispose();
        Assert.False(s.InGesture);
        Assert.Equal(1, s.UndoCount);
        s.Undo();
        Assert.True(s.Document.ContentEquals(new Document()));
    }

    [Fact] // Review Focus: Ctrl+Z in the middle of a slider drag
    public void Undo_during_a_gesture_commits_it_undoes_it_and_the_drag_goes_on_as_a_new_step()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        var drag = s.BeginGesture();
        s.Apply(d => d with { BgOpacity = 0.2 });
        s.Apply(d => d with { BgOpacity = 0.4 });

        Assert.True(s.Undo());                       // commits the drag so far, then undoes it
        Assert.Equal(1, s.Document.BgOpacity);
        Assert.True(s.Document.ShowGrid);
        Assert.Equal((1, 1), (s.UndoCount, s.RedoCount));
        Assert.True(s.InGesture);

        s.Apply(d => d with { BgOpacity = 0.6 });     // the thumb is still held
        s.Apply(d => d with { BgOpacity = 0.8 });
        Assert.Equal(1, s.UndoCount);
        drag.Dispose();

        Assert.Equal((2, 0), (s.UndoCount, s.RedoCount));
        Assert.True(s.Undo());
        Assert.Equal(1, s.Document.BgOpacity);
        Assert.True(s.Undo());
        Assert.False(s.Document.ShowGrid);
    }

    [Fact] // a gesture that changed content is a new edit, so redo after it has nothing to redo
    public void Redo_during_a_changed_gesture_commits_it_and_finds_no_redo()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        s.Undo();
        using (s.BeginGesture())
        {
            s.Apply(d => d with { BgOpacity = 0.5 });
            Assert.False(s.Redo());
            Assert.Equal((1, 0), (s.UndoCount, s.RedoCount));
        }
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(0.5, s.Document.BgOpacity);
    }

    [Fact] // an unchanged gesture leaves redo alone
    public void Redo_during_an_unchanged_gesture_still_redoes()
    {
        var s = new EditorSession();
        s.Apply(d => d with { ShowGrid = true });
        s.Undo();
        using (s.BeginGesture())
            Assert.True(s.Redo());
        Assert.True(s.Document.ShowGrid);
        Assert.Equal((1, 0), (s.UndoCount, s.RedoCount));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'EditorSession' could not be found` at `EditorSessionTests.cs`.

- [ ] **Step 7: Implement EditorSession**

Create `src/RBX15.App/EditorSession.cs`:

```csharp
using RBX15.Engine;

namespace RBX15.App;

/// <summary>What a <see cref="EditorSession.Changed"/> event reports.</summary>
[Flags]
public enum SessionChange
{
    None = 0,
    Document = 1,
    Selection = 2,
    History = 4,
    Title = 8,
}

public sealed class SessionChangedEventArgs(SessionChange what) : EventArgs
{
    public SessionChange What { get; } = what;
}

/// <summary>
/// The one editing state the shell's view-models share (spec §1 and §4): the immutable engine
/// <see cref="Engine.Document"/>, its <see cref="History"/>, the selection, the layer id counter, the
/// current file and the unsaved-changes flag. WPF-free, so it is tested headless.
/// <para>
/// <b>Every document change is exactly one undo step</b> (spec §5; ledger undo-empty-steps,
/// file-undo-snapshot-scope and undo-coverage-gaps, all fix). <see cref="Apply"/> records the state
/// from before a change only when <see cref="Engine.Document.ContentEquals"/> says the content changed,
/// so a no-op never pushes a step (4.1 pushed before no-op actions, editor.js:1117, :1601-1606), and a
/// preset written as one lambda is one step (4.1 pushed two, editor.js:1734-1749). A drag is one
/// <see cref="BeginGesture"/> scope: each change inside it is adopted at once, and the scope records
/// one step when it closes.
/// </para>
/// <para>
/// Selection survives undo and redo when its layer still exists (ledger undo-restore-ui-side-effects,
/// fix; 4.1 deselected on every restore, editor.js:1451). <see cref="History"/> resolves it and the
/// session adopts it. Not thread-safe; use it from the UI thread.
/// </para>
/// </summary>
public sealed class EditorSession
{
    readonly History _history;
    int _gestureDepth;
    EditorSnapshot? _gestureStart;
    string _templateTitle = "";

    /// <param name="document">The starting document; null gives 4.1's startup state (editor.js:76-107, the <see cref="Engine.Document"/> defaults).</param>
    /// <param name="historyCapacity">Undo depth; 4.1 uses 30 (editor.js:86).</param>
    public EditorSession(Document? document = null, int historyCapacity = 30)
    {
        _history = new History(historyCapacity);
        Document = document ?? new Document();
        NextId = MaxLayerId(Document) + 1;
    }

    public Document Document { get; private set; }

    public int? SelectedLayerId { get; private set; }

    /// <summary>The selected layer, or null (getSelected, editor.js:582).</summary>
    public Layer? SelectedLayer => SelectedLayerId is int id ? Document.Layers.Find(l => l.Id == id) : null;

    /// <summary>
    /// The next layer id. Starts at 1 (editor.js:499) and only grows: undo and redo never lower it, and
    /// adopting a document whose ids reach it raises it past them (editor.js:1465).
    /// </summary>
    public int NextId { get; private set; }

    /// <summary>The open project file. Always null in M3a; M4's Save and Open set it.</summary>
    public string? CurrentFile { get; private set; }

    /// <summary>True after any recorded step, undo or redo. M4's save clears it.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// The export title box (editor.html:100, <c>#exportTitle</c>). It is not part of the document, so it
    /// is never an undo step and never makes the session dirty (ledger file-undo-snapshot-scope). Null
    /// is stored as "".
    /// </summary>
    public string TemplateTitle
    {
        get => _templateTitle;
        set
        {
            var next = value ?? "";
            if (string.Equals(next, _templateTitle, StringComparison.Ordinal)) return;
            _templateTitle = next;
            Raise(SessionChange.Title);
        }
    }

    /// <summary>True when a step is recorded, or an open gesture has changed the content (Undo commits it first).</summary>
    public bool CanUndo => _history.CanUndo || GestureChanged;

    public bool CanRedo => _history.CanRedo;

    public int UndoCount => _history.UndoCount;

    public int RedoCount => _history.RedoCount;

    public bool InGesture => _gestureDepth > 0;

    public event EventHandler<SessionChangedEventArgs>? Changed;

    EditorSnapshot Snapshot => new(Document, SelectedLayerId);

    bool GestureChanged => _gestureStart is not null && !_gestureStart.Document.ContentEquals(Document);

    /// <summary>Returns <see cref="NextId"/>, then increments it (<c>id: nextId++</c>, editor.js:513).</summary>
    public int TakeId() => NextId++;

    /// <summary>
    /// Runs <paramref name="change"/> on the document. The change runs first, so if it throws nothing
    /// changes. Unchanged content returns false and raises nothing. Otherwise the session records the
    /// state from before (unless a gesture is open), adopts the new document, drops the selection if its
    /// layer is gone, and raises Document | History (| Selection). Returns true iff the content changed.
    /// </summary>
    public bool Apply(Func<Document, Document> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var before = Snapshot;
        var after = change(before.Document) ?? throw new InvalidOperationException("A document change returned null.");
        return Adopt(before, new EditorSnapshot(after, before.SelectedLayerId));
    }

    /// <summary>
    /// <see cref="Apply"/> for operations that also choose the selection (add, duplicate, remove). The
    /// returned selection is adopted even when the content is unchanged (then only Selection is raised
    /// and no step is recorded); an id the returned document lacks becomes null. Returns true iff the
    /// content changed.
    /// </summary>
    public bool ApplyWithSelection(Func<EditorSnapshot, EditorSnapshot> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var before = Snapshot;
        var after = change(before) ?? throw new InvalidOperationException("A document change returned null.");
        if (after.Document is null) throw new InvalidOperationException("A document change returned a null document.");
        return Adopt(before, after);
    }

    /// <summary>
    /// Selects a layer (selectLayer, editor.js:575). An id the document lacks selects nothing. Raises
    /// Selection when the selection changes; never an undo step.
    /// </summary>
    public void Select(int? id)
    {
        int? next = Resolve(Document, id);
        if (next == SelectedLayerId) return;
        SelectedLayerId = next;
        Raise(SessionChange.Selection);
    }

    /// <summary>
    /// Opens a gesture. Until the outermost scope is disposed, <see cref="Apply"/> and
    /// <see cref="ApplyWithSelection"/> adopt each change without recording. Disposing the outermost
    /// scope records the state from its opening once, iff the content changed (ledger
    /// fillstyle-undo-snapshot-timing: one slider drag is one step). Scopes nest by count, and disposing
    /// a scope twice does nothing.
    /// </summary>
    public IDisposable BeginGesture()
    {
        if (_gestureDepth++ == 0) _gestureStart = Snapshot;
        return new GestureScope(this);
    }

    /// <summary>
    /// Undo (btnUndo, editor.js:1477-1481). An open gesture is committed first, then undone, and it stays
    /// open from the restored state, so a drag that goes on after Ctrl+Z is one new step when it ends.
    /// False when there is nothing to undo.
    /// </summary>
    public bool Undo() => Step(redo: false);

    /// <summary>Redo (btnRedo, editor.js:1482-1486), the mirror of <see cref="Undo"/>.</summary>
    public bool Redo() => Step(redo: true);

    bool Step(bool redo)
    {
        var what = RecordGesture();
        var current = Snapshot;
        var target = redo ? _history.Redo(current) : _history.Undo(current);
        if (target is not null)
        {
            Document = target.Document;
            SelectedLayerId = target.SelectedLayerId;
            BumpNextId(Document);
            IsDirty = true;
            what |= SessionChange.Document | SessionChange.History;
            if (current.SelectedLayerId != SelectedLayerId) what |= SessionChange.Selection;
        }
        if (InGesture) _gestureStart = Snapshot;  // the open gesture goes on from here; its scopes stay valid
        if (what != SessionChange.None) Raise(what);
        return target is not null;
    }

    bool Adopt(EditorSnapshot before, EditorSnapshot after)
    {
        var what = SessionChange.None;
        bool changed = !before.Document.ContentEquals(after.Document);
        if (changed)
        {
            if (!InGesture)
            {
                _history.Record(before);
                IsDirty = true;
                what |= SessionChange.History;
            }
            Document = after.Document;
            BumpNextId(Document);
            what |= SessionChange.Document;
        }
        int? selection = Resolve(Document, after.SelectedLayerId);
        if (selection != SelectedLayerId)
        {
            SelectedLayerId = selection;
            what |= SessionChange.Selection;
        }
        if (what != SessionChange.None) Raise(what);
        return changed;
    }

    /// <summary>Records an open gesture's opening state iff the content changed since it opened.</summary>
    SessionChange RecordGesture()
    {
        if (_gestureStart is not { } start || start.Document.ContentEquals(Document)) return SessionChange.None;
        _history.Record(start);
        IsDirty = true;
        return SessionChange.History;
    }

    void EndGesture()
    {
        if (_gestureDepth == 0 || --_gestureDepth > 0) return;
        var what = RecordGesture();
        _gestureStart = null;
        if (what != SessionChange.None) Raise(what);
    }

    void BumpNextId(Document doc) => NextId = Math.Max(NextId, MaxLayerId(doc) + 1);

    static int MaxLayerId(Document doc) => doc.Layers.IsEmpty ? 0 : doc.Layers.Max(l => l.Id);

    static int? Resolve(Document doc, int? id) => id is int v && doc.Layers.Exists(l => l.Id == v) ? v : null;

    void Raise(SessionChange what) => Changed?.Invoke(this, new SessionChangedEventArgs(what));

    sealed class GestureScope(EditorSession session) : IDisposable
    {
        bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            session.EndGesture();
        }
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.EditorSessionTests"`
Expected: `Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30` for `RBX15.App.Tests.dll`.

Commit (end the message with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/RBX15.App.csproj src/RBX15.App/app.manifest src/RBX15.App/EditorSession.cs src/RBX15.App.Tests src/RBX15.slnx
git commit -m "feat(app): EditorSession, one undo step per document change, and RBX15.App.Tests"
```

- [ ] **Step 8: Write the failing window-placement tests** (ledger `shell-window-size`, `shell-no-persistence`)

Create `src/RBX15.App.Tests/Fakes/FakeSettingsStore.cs`:

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An in-memory <see cref="ISettingsStore"/> that records every save.</summary>
internal sealed class FakeSettingsStore(WindowPlacement? saved = null) : ISettingsStore
{
    public WindowPlacement? Saved { get; private set; } = saved;

    public List<WindowPlacement> Saves { get; } = [];

    public WindowPlacement? LoadWindowPlacement() => Saved;

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        Saves.Add(placement);
        Saved = placement;
    }
}
```

Create `src/RBX15.App.Tests/PlacementGuardTests.cs`:

```csharp
using System.Windows;
using RBX15.App.Services;

namespace RBX15.App.Tests;

/// <summary>
/// PlacementGuard (ledger shell-window-size, fix): 4.1 always opened 1200x800 centered on the primary
/// display (main.js:5-13); v5 restores the saved placement when it is still on a monitor.
/// </summary>
public class PlacementGuardTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);          // 1920x1080 minus a 40 DIP taskbar
    static readonly Rect LeftMonitor = new(-1920, 0, 1920, 1080);

    [Fact] // first launch: 1200x800 centered on the primary work area (windows/TEST_PLAN.md:24)
    public void No_saved_placement_gives_1200x800_centered_on_the_primary()
    {
        Assert.Equal(new WindowPlacement(360, 120, 1200, 800, false), PlacementGuard.Resolve(null, [Primary, LeftMonitor]));
    }

    [Fact] // a saved placement fully on a monitor comes back as saved, maximized flag included
    public void A_visible_placement_is_kept()
    {
        var saved = new WindowPlacement(-1700, 50, 1000, 700, true);
        Assert.Equal(saved, PlacementGuard.Resolve(saved, [Primary, LeftMonitor]));
    }

    [Fact] // Review Focus: the monitor the window was on is unplugged
    public void A_placement_on_a_missing_monitor_falls_back_to_the_default()
    {
        var saved = new WindowPlacement(-1700, 50, 1000, 700, true);
        Assert.Equal(new WindowPlacement(360, 120, 1200, 800, false), PlacementGuard.Resolve(saved, [Primary]));
    }

    [Fact] // partly off the right edge: moved back inside, size kept
    public void A_placement_hanging_off_an_edge_is_moved_inside()
    {
        var saved = new WindowPlacement(1500, 900, 1000, 600, false);
        Assert.Equal(new WindowPlacement(920, 440, 1000, 600, false), PlacementGuard.Resolve(saved, [Primary]));
    }

    [Fact] // larger than the work area (a smaller monitor now): shrunk to it
    public void A_placement_larger_than_the_work_area_is_shrunk_to_fit()
    {
        var saved = new WindowPlacement(-10, -10, 2500, 1400, false);
        Assert.Equal(new WindowPlacement(0, 0, 1920, 1040, false), PlacementGuard.Resolve(saved, [Primary]));
    }

    [Fact] // straddling two monitors: lands on the one it overlaps most
    public void A_straddling_placement_lands_on_the_monitor_it_overlaps_most()
    {
        var saved = new WindowPlacement(-300, 100, 1000, 700, false);
        Assert.Equal(new WindowPlacement(0, 100, 1000, 700, false), PlacementGuard.Resolve(saved, [Primary, LeftMonitor]));
        var mostlyLeft = new WindowPlacement(-800, 100, 1000, 700, false);
        Assert.Equal(new WindowPlacement(-1000, 100, 1000, 700, false), PlacementGuard.Resolve(mostlyLeft, [Primary, LeftMonitor]));
    }

    [Fact] // a small primary: the default shrinks to it
    public void The_default_shrinks_to_a_small_primary()
    {
        Assert.Equal(new WindowPlacement(0, 0, 1024, 728, false), PlacementGuard.Resolve(null, [new Rect(0, 0, 1024, 728)]));
    }

    [Theory] // a corrupt size or position is treated as no placement
    [InlineData(double.NaN, 0, 800, 600)]
    [InlineData(0, double.PositiveInfinity, 800, 600)]
    [InlineData(0, 0, 0, 600)]
    [InlineData(0, 0, 800, -1)]
    [InlineData(0, 0, double.NaN, 600)]
    public void A_corrupt_placement_falls_back_to_the_default(double left, double top, double width, double height)
    {
        Assert.Equal(new WindowPlacement(360, 120, 1200, 800, false),
                     PlacementGuard.Resolve(new WindowPlacement(left, top, width, height, true), [Primary]));
    }

    [Fact]
    public void Work_areas_are_required()
    {
        Assert.Throws<ArgumentException>(() => PlacementGuard.Resolve(null, []));
        Assert.Throws<ArgumentException>(() => PlacementGuard.Resolve(null, [Rect.Empty]));
    }
}
```

Create `src/RBX15.App.Tests/JsonSettingsStoreTests.cs`:

```csharp
using System.Text.Json.Nodes;
using RBX15.App.Services;

namespace RBX15.App.Tests;

/// <summary>
/// JsonSettingsStore (spec §3 "Upgrades": the saved window position; ledger shell-no-persistence, fix):
/// atomic writes, and reads that never throw.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "rbx15-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    string FilePath => Path.Combine(_dir, "settings.json");

    [Fact] // the directory is created on first save; the file round-trips
    public void A_saved_placement_loads_back()
    {
        var store = new JsonSettingsStore(_dir);
        Assert.Null(store.LoadWindowPlacement());

        var placement = new WindowPlacement(-1700.5, 50, 1000, 700.25, true);
        store.SaveWindowPlacement(placement);

        Assert.Equal(FilePath, store.FilePath);
        Assert.Equal(placement, new JsonSettingsStore(_dir).LoadWindowPlacement());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact] // the file's shape is fixed: { "window": { left, top, width, height, maximized } }
    public void The_file_holds_a_window_object()
    {
        new JsonSettingsStore(_dir).SaveWindowPlacement(new WindowPlacement(10, 20, 1200, 800, false));
        var w = JsonNode.Parse(File.ReadAllText(FilePath))!["window"]!;
        Assert.Equal((10.0, 20.0, 1200.0, 800.0, false),
                     (w["left"]!.GetValue<double>(), w["top"]!.GetValue<double>(), w["width"]!.GetValue<double>(),
                      w["height"]!.GetValue<double>(), w["maximized"]!.GetValue<bool>()));
    }

    [Fact] // a save rewrites only "window" and keeps keys later milestones add
    public void A_save_keeps_other_keys()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "recent": ["a.r15proj"], "window": { "left": 1 } }""");
        new JsonSettingsStore(_dir).SaveWindowPlacement(new WindowPlacement(5, 6, 700, 500, false));
        var root = JsonNode.Parse(File.ReadAllText(FilePath))!;
        Assert.Equal("a.r15proj", root["recent"]![0]!.GetValue<string>());
        Assert.Equal(700, root["window"]!["width"]!.GetValue<double>());
    }

    public static TheoryData<string> CorruptFiles => new()
    {
        "",
        "{",
        "null",
        "[1, 2]",
        """{ "window": null }""",
        """{ "window": { "left": 1, "top": 2, "width": 3 } }""",
        """{ "window": { "left": "1", "top": 2, "width": 3, "height": 4, "maximized": false } }""",
        """{ "window": { "left": 1, "top": 2, "width": 3, "height": 4, "maximized": "yes" } }""",
        """{ "window": { "left": 1, "top": 2, "width": -3, "height": 4, "maximized": false } }""",
        """{ "window": { "left": 1, "top": 2, "width": 0, "height": 4, "maximized": false } }""",
        """{ "window": { "left": 1e400, "top": 2, "width": 3, "height": 4, "maximized": false } }""",
    };

    [Theory, MemberData(nameof(CorruptFiles))] // Review Focus: a truncated or hand-edited settings file
    public void A_corrupt_file_loads_as_no_placement(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);
        Assert.Null(new JsonSettingsStore(_dir).LoadWindowPlacement());
    }

    [Fact] // a corrupt file is replaced by the next save
    public void A_save_over_a_corrupt_file_replaces_it()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ truncated");
        var store = new JsonSettingsStore(_dir);
        store.SaveWindowPlacement(new WindowPlacement(1, 2, 300, 400, false));
        Assert.Equal(new WindowPlacement(1, 2, 300, 400, false), store.LoadWindowPlacement());
    }

    [Fact] // writing is best effort: an unwritable location never throws (closing must not fail)
    public void A_save_that_cannot_write_does_not_throw()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "not-a-directory");
        File.WriteAllText(blocker, "a file where the settings directory should be");
        var store = new JsonSettingsStore(blocker);
        store.SaveWindowPlacement(new WindowPlacement(1, 2, 300, 400, false));
        Assert.Null(store.LoadWindowPlacement());
    }

    [Fact] // a non-finite or non-positive placement is never written
    public void An_unusable_placement_is_not_saved()
    {
        var store = new JsonSettingsStore(_dir);
        store.SaveWindowPlacement(new WindowPlacement(double.NaN, 0, 800, 600, false));
        store.SaveWindowPlacement(new WindowPlacement(0, 0, 0, 600, false));
        Assert.False(File.Exists(FilePath));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0234: The type or namespace name 'Services' does not exist in the namespace 'RBX15.App'`.

- [ ] **Step 9: Implement WindowPlacement, the settings store and PlacementGuard**

Create `src/RBX15.App/Services/WindowPlacement.cs`:

```csharp
namespace RBX15.App.Services;

/// <summary>
/// The main window's restore bounds in device-independent pixels, and whether it was maximized.
/// 4.1 remembered none of this (main.js:5-13; ledger shell-window-size and shell-no-persistence, fix).
/// </summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);
```

Create `src/RBX15.App/Services/ISettingsStore.cs`:

```csharp
namespace RBX15.App.Services;

/// <summary>The app's small local settings file (spec §3 "Upgrades": the saved window position).</summary>
public interface ISettingsStore
{
    /// <summary>The saved placement, or null when there is none or the file is unreadable.</summary>
    WindowPlacement? LoadWindowPlacement();

    /// <summary>Saves the placement. Best effort: a failure to write never throws.</summary>
    void SaveWindowPlacement(WindowPlacement placement);
}
```

Create `src/RBX15.App/Services/JsonSettingsStore.cs`:

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RBX15.App.Services;

/// <summary>
/// <c>settings.json</c> in one directory (the app passes <c>%LOCALAPPDATA%\RBX15</c>, which MSIX
/// redirects into the package's local data, removed on uninstall). The file holds one object:
/// <c>{ "window": { "left", "top", "width", "height", "maximized" } }</c>. Later milestones add
/// sibling keys, so a save rewrites only <c>window</c> and keeps every other key it read.
/// <para>
/// Writes are atomic: the whole file goes to <c>settings.json.tmp</c>, then
/// <see cref="File.Move(string, string, bool)"/> replaces <c>settings.json</c>, so a crash mid-write
/// leaves the previous file intact. Reads never throw: a missing, unreadable, malformed or
/// out-of-range file gives null and the window opens at its default.
/// </para>
/// </summary>
public sealed class JsonSettingsStore(string directory) : ISettingsStore
{
    public const string FileName = "settings.json";
    const string WindowKey = "window";

    public string FilePath { get; } = Path.Combine(directory, FileName);

    public WindowPlacement? LoadWindowPlacement()
    {
        if (ReadRoot()?[WindowKey] is not JsonObject w) return null;
        if (Num(w["left"]) is not double left || Num(w["top"]) is not double top ||
            Num(w["width"]) is not double width || Num(w["height"]) is not double height ||
            w["maximized"] is not JsonValue m || !m.TryGetValue(out bool maximized)) return null;
        var p = new WindowPlacement(left, top, width, height, maximized);
        return IsUsable(p) ? p : null;
    }

    static double? Num(JsonNode? node) => node is JsonValue v && v.TryGetValue(out double d) ? d : null;

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!IsUsable(placement)) return;
        var root = ReadRoot() ?? new JsonObject();
        root[WindowKey] = new JsonObject
        {
            ["left"] = placement.Left,
            ["top"] = placement.Top,
            ["width"] = placement.Width,
            ["height"] = placement.Height,
            ["maximized"] = placement.Maximized,
        };
        var temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: losing the window position must never block closing the app.
            try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Finite coordinates and a positive finite size; anything else is a corrupt file.</summary>
    static bool IsUsable(WindowPlacement p) =>
        double.IsFinite(p.Left) && double.IsFinite(p.Top) &&
        double.IsFinite(p.Width) && double.IsFinite(p.Height) && p.Width > 0 && p.Height > 0;

    JsonObject? ReadRoot()
    {
        try
        {
            return File.Exists(FilePath) ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
```

Create `src/RBX15.App/Services/PlacementGuard.cs`:

```csharp
using System.Windows;

namespace RBX15.App.Services;

/// <summary>
/// Turns a saved <see cref="WindowPlacement"/> into one that is safe to restore (pure; the window
/// passes the monitors' work areas). 4.1 always opened 1200x800 centered on the primary display
/// (main.js:5-13, windows/TEST_PLAN.md:24; ledger shell-window-size, fix: v5 remembers size and
/// position).
/// </summary>
public static class PlacementGuard
{
    /// <summary>
    /// <list type="bullet">
    /// <item>No saved placement, a size that is not positive and finite, or a rectangle that overlaps
    /// no work area (its monitor was unplugged or rearranged): the default size, shrunk to fit, centered
    /// on <c>workAreas[0]</c> (the primary monitor), not maximized.</item>
    /// <item>Otherwise the saved placement, moved and if needed shrunk to lie wholly inside the work
    /// area it overlaps most. <see cref="WindowPlacement.Maximized"/> is kept: the window maximizes on
    /// that monitor and restores to these bounds.</item>
    /// </list>
    /// </summary>
    /// <param name="workAreas">Every monitor's work area in DIPs, the primary first. Must not be empty.</param>
    public static WindowPlacement Resolve(WindowPlacement? saved, IReadOnlyList<Rect> workAreas, double defaultWidth = 1200, double defaultHeight = 800)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0 || workAreas.Any(a => a.IsEmpty || a.Width <= 0 || a.Height <= 0))
            throw new ArgumentException("At least one non-empty work area is required.", nameof(workAreas));

        if (saved is not null && double.IsFinite(saved.Left) && double.IsFinite(saved.Top) &&
            double.IsFinite(saved.Width) && double.IsFinite(saved.Height) && saved.Width > 0 && saved.Height > 0)
        {
            var rect = new Rect(saved.Left, saved.Top, saved.Width, saved.Height);
            Rect? best = null;
            double bestArea = 0;
            foreach (var area in workAreas)
            {
                var overlap = Rect.Intersect(rect, area);
                double size = overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
                if (size > bestArea) (best, bestArea) = (area, size);
            }
            if (best is Rect target)
                return Fit(saved.Left, saved.Top, saved.Width, saved.Height, target) with { Maximized = saved.Maximized };
        }

        var primary = workAreas[0];
        double w = Math.Min(defaultWidth, primary.Width), h = Math.Min(defaultHeight, primary.Height);
        return new WindowPlacement(primary.Left + (primary.Width - w) / 2, primary.Top + (primary.Height - h) / 2, w, h, false);
    }

    static WindowPlacement Fit(double left, double top, double width, double height, Rect area)
    {
        double w = Math.Min(width, area.Width), h = Math.Min(height, area.Height);
        double x = Math.Clamp(left, area.Left, area.Right - w), y = Math.Clamp(top, area.Top, area.Bottom - h);
        return new WindowPlacement(x, y, w, h, false);
    }
}
```

Create `src/RBX15.App/Services/MonitorWorkAreas.cs` (internal; the window reads the live work areas through it, and tests pass their own):

```csharp
using System.Runtime.InteropServices;
using System.Windows;

namespace RBX15.App.Services;

/// <summary>
/// Every monitor's work area (the desktop minus the taskbar) for <see cref="PlacementGuard"/>, primary
/// first, without a WinForms reference. Each rectangle is the monitor's physical work area divided by
/// that monitor's own scale (GetDpiForMonitor / 96), the per-monitor DPI v2 mapping WPF applies to
/// <see cref="Window.Left"/> and <see cref="Window.Top"/> for a window on that monitor.
/// </summary>
internal static class MonitorWorkAreas
{
    public static IReadOnlyList<Rect> Get()
    {
        var areas = new List<(bool Primary, Rect Area)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfoW(monitor, ref info)) return true;
            double scale = GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 && dpiX > 0 ? dpiX / 96.0 : 1;
            var r = info.Work;
            areas.Add(((info.Flags & 1) != 0, new Rect(r.Left / scale, r.Top / scale, (r.Right - r.Left) / scale, (r.Bottom - r.Top) / scale)));
            return true;
        }, IntPtr.Zero);
        if (areas.Count == 0) return [SystemParameters.WorkArea];
        return areas.OrderByDescending(a => a.Primary).Select(a => a.Area).ToList();
    }

    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
```

- [ ] **Step 10: Run and commit**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.PlacementGuardTests|FullyQualifiedName~RBX15.App.Tests.JsonSettingsStoreTests"`
Expected: `Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30` for `RBX15.App.Tests.dll` (13 PlacementGuard, 17 JsonSettingsStore).

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 582`; Parity.Tests `Passed: 35`; App.Tests `Passed: 60, Failed: 0`.

Commit (end the message with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/Services src/RBX15.App.Tests/Fakes src/RBX15.App.Tests/PlacementGuardTests.cs src/RBX15.App.Tests/JsonSettingsStoreTests.cs
git commit -m "feat(app): window placement memory with an atomic settings file and an off-screen guard"
```

- [ ] **Step 11: Write the failing theme tests** (ledger `theme-tokens-colors`, `theme-tokens-shape-motion-type`, `font-ui-runtime-google`, `theme-scrollbars`)

Create `src/RBX15.App.Tests/ThemeTokenTests.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The theme dictionary against styles.css itself (ledger theme-tokens-colors and
/// theme-tokens-shape-motion-type, keep: "Unit test that every token hex in the ResourceDictionary equals
/// styles.css values"). The :root block (styles.css:8-92) is parsed here, var() chains resolved, and every
/// token is either checked or listed in <see cref="NotChecked"/> with its reason, so a token added to
/// styles.css fails this test until the theme handles it.
/// </summary>
public partial class ThemeTokenTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    /// <summary>Tokens with no numeric check here, and why.</summary>
    static readonly Dictionary<string, string> NotChecked = new()
    {
        ["brand-gradient-glow"] = "radial pair; no M3a surface uses it",
        ["shadow-1"] = "unused in 4.1 (no var(--shadow-1) in styles.css)",
        ["glow-cyan"] = "unused in 4.1",
        ["glow-magenta"] = "unused in 4.1",
        ["glow-duo"] = "two stacked shadows; WPF keeps the cyan layer (GlowDuoEffect); tuned visually (ledger theme-tokens-shape-motion-type)",
        ["inner-stroke"] = "unused in 4.1",
        ["font-display"] = "checked by family in Ui_fonts_resolve_to_the_vendored_files",
        ["font-body"] = "checked by family in Ui_fonts_resolve_to_the_vendored_files",
        ["font-mono"] = "checked by family in Ui_fonts_resolve_to_the_vendored_files",
    };

    [GeneratedRegex(@"--([a-z0-9-]+):\s*([^;]+);")]
    private static partial Regex TokenPattern();

    /// <summary>name to raw value, from the first :root block of styles.css.</summary>
    static Dictionary<string, string> CssTokens()
    {
        var css = File.ReadAllText(Path.Combine(Root, "styles.css"));
        int start = css.IndexOf(":root {", StringComparison.Ordinal);
        int end = css.IndexOf('}', start);
        return TokenPattern().Matches(css[start..end]).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    static string Resolve(Dictionary<string, string> tokens, string value)
    {
        var m = Regex.Match(value, @"^var\(--([a-z0-9-]+)\)$");
        return m.Success ? Resolve(tokens, tokens[m.Groups[1].Value]) : value;
    }

    /// <summary>"brand-navy-deep" to "BrandNavyDeep".</summary>
    static string Pascal(string name) =>
        string.Concat(name.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    /// <summary>#rrggbb, #rgb or rgba(r,g,b,a) to a WPF color; alpha byte = round(a * 255).</summary>
    static Color? CssColor(string v)
    {
        var hex = Regex.Match(v, "^#([0-9a-fA-F]{6})$");
        if (hex.Success)
        {
            uint rgb = uint.Parse(hex.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
        var rgba = Regex.Match(v, @"^rgba\((\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\)$");
        if (!rgba.Success) return null;
        double a = double.Parse(rgba.Groups[4].Value, CultureInfo.InvariantCulture);
        return Color.FromArgb((byte)Math.Round(a * 255, MidpointRounding.AwayFromZero),
                              byte.Parse(rgba.Groups[1].Value, CultureInfo.InvariantCulture),
                              byte.Parse(rgba.Groups[2].Value, CultureInfo.InvariantCulture),
                              byte.Parse(rgba.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    static object Resource(string key) => StaThread.Run(() => Application.Current.FindResource(key));

    static IEnumerable<string> ColorTokenNames()
    {
        var tokens = CssTokens();
        return tokens.Keys.Where(name => CssColor(Resolve(tokens, tokens[name])) is not null);
    }

    public static TheoryData<string> ColorTokens() => new(ColorTokenNames());

    [Theory, MemberData(nameof(ColorTokens))] // styles.css:10-66
    public void Each_color_token_is_a_color_and_a_brush_with_the_css_value(string token)
    {
        var tokens = CssTokens();
        var expected = CssColor(Resolve(tokens, tokens[token]))!.Value;
        var key = Pascal(token);
        Assert.Equal(expected, (Color)Resource(key + "Color"));
        Assert.Equal(expected, StaThread.Run(() => ((SolidColorBrush)Application.Current.FindResource(key + "Brush")).Color));
    }

    [Fact] // every :root token is checked somewhere, or listed with a reason
    public void Every_root_token_is_accounted_for()
    {
        var tokens = CssTokens();
        Assert.Equal(60, tokens.Count);
        var handled = new HashSet<string>(ColorTokenNames())
        {
            "brand-gradient", "brand-gradient-soft", "r-xs", "r-sm", "r-md", "r-lg", "r-pill",
            "shadow-2", "ease-out", "dur-fast", "dur-med",
        };
        handled.UnionWith(NotChecked.Keys);
        Assert.Empty(tokens.Keys.Except(handled));
        Assert.Empty(handled.Except(tokens.Keys));
    }

    [Theory] // styles.css:22-23: 135deg, two stops at 0% and 100%
    [InlineData("brand-gradient", "BrandGradientBrush")]
    [InlineData("brand-gradient-soft", "BrandGradientSoftBrush")]
    public void Gradients_have_the_css_stops(string token, string key)
    {
        var tokens = CssTokens();
        var m = Regex.Match(tokens[token], @"^linear-gradient\(135deg, (.+) 0%, (.+) 100%\)$");
        Assert.True(m.Success, tokens[token]);
        var c0 = CssColor(Resolve(tokens, m.Groups[1].Value))!.Value;
        var c1 = CssColor(Resolve(tokens, m.Groups[2].Value))!.Value;
        var stops = StaThread.Run(() =>
        {
            var b = (LinearGradientBrush)Application.Current.FindResource(key);
            return (b.StartPoint, b.EndPoint, b.MappingMode, Stops: b.GradientStops.Select(s => (s.Offset, s.Color)).ToArray());
        });
        Assert.Equal((new Point(0, 0), new Point(1, 1), BrushMappingMode.RelativeToBoundingBox), (stops.StartPoint, stops.EndPoint, stops.MappingMode));
        Assert.Equal([(0.0, c0), (1.0, c1)], stops.Stops);
    }

    [Theory] // styles.css:69-73
    [InlineData("r-xs", "RXsRadius")]
    [InlineData("r-sm", "RSmRadius")]
    [InlineData("r-md", "RMdRadius")]
    [InlineData("r-lg", "RLgRadius")]
    [InlineData("r-pill", "RPillRadius")]
    public void Radii_match(string token, string key)
    {
        double px = double.Parse(CssTokens()[token].TrimEnd('p', 'x'), CultureInfo.InvariantCulture);
        Assert.Equal(new CornerRadius(px), (CornerRadius)Resource(key));
    }

    [Theory] // styles.css:85-86
    [InlineData("dur-fast", "DurFastDuration")]
    [InlineData("dur-med", "DurMedDuration")]
    public void Durations_match(string token, string key)
    {
        double ms = double.Parse(CssTokens()[token].TrimEnd('m', 's'), CultureInfo.InvariantCulture);
        Assert.Equal(new Duration(TimeSpan.FromMilliseconds(ms)), (Duration)Resource(key));
    }

    [Fact] // styles.css:84, cubic-bezier(.2,.7,.2,1)
    public void The_ease_is_the_css_cubic_bezier()
    {
        Assert.Equal("cubic-bezier(.2,.7,.2,1)", CssTokens()["ease-out"]);
        var points = StaThread.Run(() =>
        {
            var k = (KeySpline)Application.Current.FindResource("EaseOutSpline");
            return (k.ControlPoint1, k.ControlPoint2);
        });
        Assert.Equal((new Point(0.2, 0.7), new Point(0.2, 1)), points);
    }

    [Fact] // styles.css:77, shadow-2: 0 4px 16px rgba(0,0,0,.45)
    public void Shadow_2_is_offset_4_blur_16_at_45_percent_black()
    {
        Assert.Equal("0 4px 16px rgba(0,0,0,.45)", CssTokens()["shadow-2"]);
        var e = StaThread.Run(() =>
        {
            var s = (DropShadowEffect)Application.Current.FindResource("Shadow2Effect");
            return (s.Color, s.Direction, s.ShadowDepth, s.BlurRadius, s.Opacity);
        });
        Assert.Equal((Colors.Black, 270.0, 4.0, 16.0, 0.45), e);
    }

    [Fact] // theme-scrollbars: thumb #223a54 on track #050c18 (styles.css:115)
    public void Scrollbars_are_navy_soft_on_ink_950()
    {
        Assert.Contains("scrollbar-color: var(--brand-navy-soft) var(--ink-950)", File.ReadAllText(Path.Combine(Root, "styles.css")));
        var (track, thumb) = StaThread.Run(() =>
        {
            var bar = new System.Windows.Controls.Primitives.ScrollBar { Orientation = System.Windows.Controls.Orientation.Vertical, Height = 100 };
            var host = new System.Windows.Controls.Grid { Children = { bar } };
            host.Measure(new Size(50, 100));
            host.Arrange(new Rect(0, 0, 50, 100));
            var thumbBorder = FindVisual<System.Windows.Controls.Border>(bar, b => b.TemplatedParent is System.Windows.Controls.Primitives.Thumb);
            return (((SolidColorBrush)bar.Background).Color, ((SolidColorBrush)thumbBorder!.Background).Color);
        });
        Assert.Equal(Color.FromRgb(0x05, 0x0c, 0x18), track);
        Assert.Equal(Color.FromRgb(0x22, 0x3a, 0x54), thumb);
    }

    static T? FindVisual<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && match(t)) return t;
            if (FindVisual(child, match) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// Review Focus candidate: a wrong pack URI makes WPF silently draw a system face. Each UI family must
    /// resolve to its embedded vendored file. Space Grotesk and JetBrains Mono expose no 600 instance to
    /// WPF, so the CSS weight 600 (tabs, labels, h1) draws their Bold (700) instance; Inter has a real
    /// SemiBold. Measured 2026-10-14 against assets/fonts at google/fonts@66928797.
    /// </summary>
    [Theory]
    [InlineData("FontDisplay", "Space Grotesk", "SpaceGrotesk.ttf", 600, 700)]
    [InlineData("FontBody", "Inter", "Inter.ttf", 400, 400)]
    [InlineData("FontBody", "Inter", "Inter.ttf", 600, 600)]
    [InlineData("FontMono", "JetBrains Mono", "JetBrainsMono.ttf", 400, 400)]
    [InlineData("FontMono", "JetBrains Mono", "JetBrainsMono.ttf", 600, 700)]
    public void Ui_fonts_resolve_to_the_vendored_files(string key, string family, string file, int cssWeight, int drawnWeight)
    {
        var css = CssTokens()[key switch { "FontDisplay" => "font-display", "FontBody" => "font-body", _ => "font-mono" }];
        Assert.StartsWith("'" + family + "'", css);
        var (uri, weight, sims) = StaThread.Run(() =>
        {
            var ff = (FontFamily)Application.Current.FindResource(key);
            var tf = new Typeface(ff, FontStyles.Normal, FontWeight.FromOpenTypeWeight(cssWeight), FontStretches.Normal);
            Assert.True(tf.TryGetGlyphTypeface(out var g), "no glyph typeface for " + key);
            return (g.FontUri.ToString(), g.Weight.ToOpenTypeWeight(), g.StyleSimulations);
        });
        Assert.EndsWith("/fonts/" + file.ToLowerInvariant(), uri.ToLowerInvariant());
        Assert.Equal(drawnWeight, weight);
        Assert.Equal(StyleSimulations.None, sims);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` (the tests reach the theme only at run time).

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.ThemeTokenTests"`
Expected: `Failed!  - Failed:    57, Passed:     1, Skipped:     0, Total:    58`. The one pass is `Every_root_token_is_accounted_for`, which reads only styles.css. The 57 failures read `The test STA thread failed to start.` with the inner exception `Cannot locate resource 'themes/controls.xaml'.`

- [ ] **Step 12: Implement the theme dictionaries and the upper-case converter** (also `a11y-focus-rings`, `cross-undo-disabled-style`, `ribbon-tabs-switching`, `ribbon-group-structure`)

Create `src/RBX15.App/Themes/Tokens.xaml`:

```xml
<!--
  4.1's design tokens: the :root block of styles.css (styles.css:8-92), effective values.
  Naming: the color token brand-navy-deep is the Color "BrandNavyDeepColor" and the SolidColorBrush
  "BrandNavyDeepBrush" (each hyphen-separated word capitalized); var() references are resolved to
  their final value. rgba() alpha becomes the
  byte round(alpha * 255). ThemeTokenTests parses styles.css and checks every entry below against it.
  Ledger rows: theme-tokens-colors, theme-tokens-shape-motion-type (keep), font-ui-runtime-google (fix).
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Brand / signature colors (styles.css:10-20) -->
  <Color x:Key="BrandNavyColor">#FF192E44</Color>
  <Color x:Key="BrandNavyDeepColor">#FF0F1F31</Color>
  <Color x:Key="BrandNavySoftColor">#FF223A54</Color>
  <Color x:Key="BrandCyanColor">#FF17D4FA</Color>
  <Color x:Key="BrandCyanBrightColor">#FF5CE6FF</Color>
  <Color x:Key="BrandCyanDimColor">#FF0FA8C9</Color>
  <Color x:Key="BrandMagentaColor">#FFF22F89</Color>
  <Color x:Key="BrandMagentaBrightColor">#FFFF5AA3</Color>
  <Color x:Key="BrandMagentaDimColor">#FFC21F6C</Color>

  <!-- Neutral scale (styles.css:28-39) -->
  <Color x:Key="Ink0Color">#FFFFFFFF</Color>
  <Color x:Key="Ink50Color">#FFF5F7FB</Color>
  <Color x:Key="Ink100Color">#FFE6EBF3</Color>
  <Color x:Key="Ink200Color">#FFC4CDDA</Color>
  <Color x:Key="Ink300Color">#FF8E9BAD</Color>
  <Color x:Key="Ink400Color">#FF5E6B7F</Color>
  <Color x:Key="Ink500Color">#FF3D4A5E</Color>
  <Color x:Key="Ink600Color">#FF2A3649</Color>
  <Color x:Key="Ink700Color">#FF1E2A3D</Color>
  <Color x:Key="Ink800Color">#FF152135</Color>
  <Color x:Key="Ink900Color">#FF0A1524</Color>
  <Color x:Key="Ink950Color">#FF050C18</Color>

  <!-- Foreground (styles.css:42-47) -->
  <Color x:Key="Fg1Color">#FFFFFFFF</Color>
  <Color x:Key="Fg2Color">#FFC4CDDA</Color>
  <Color x:Key="Fg3Color">#FF8E9BAD</Color>
  <Color x:Key="FgMutedColor">#FF5E6B7F</Color>
  <Color x:Key="FgAccentColor">#FF17D4FA</Color>
  <Color x:Key="FgAccentAltColor">#FFF22F89</Color>

  <!-- Background (styles.css:50-53) -->
  <Color x:Key="Bg0Color">#FF0F1F31</Color>
  <Color x:Key="Bg1Color">#FF192E44</Color>
  <Color x:Key="Bg2Color">#FF223A54</Color>
  <Color x:Key="Bg3Color">#FF2A3649</Color>

  <!-- Semantic state (styles.css:56-59) -->
  <Color x:Key="SuccessColor">#FF2BD99A</Color>
  <Color x:Key="WarningColor">#FFFFB454</Color>
  <Color x:Key="DangerColor">#FFFF5472</Color>
  <Color x:Key="DangerBgColor">#1FFF5472</Color>

  <!-- Borders (styles.css:62-66) -->
  <Color x:Key="Border1Color">#14FFFFFF</Color>
  <Color x:Key="Border2Color">#24FFFFFF</Color>
  <Color x:Key="BorderStrongColor">#3DFFFFFF</Color>
  <Color x:Key="BorderAccentColor">#7317D4FA</Color>
  <Color x:Key="BorderMagentaColor">#73F22F89</Color>

  <SolidColorBrush x:Key="BrandNavyBrush" Color="{StaticResource BrandNavyColor}" />
  <SolidColorBrush x:Key="BrandNavyDeepBrush" Color="{StaticResource BrandNavyDeepColor}" />
  <SolidColorBrush x:Key="BrandNavySoftBrush" Color="{StaticResource BrandNavySoftColor}" />
  <SolidColorBrush x:Key="BrandCyanBrush" Color="{StaticResource BrandCyanColor}" />
  <SolidColorBrush x:Key="BrandCyanBrightBrush" Color="{StaticResource BrandCyanBrightColor}" />
  <SolidColorBrush x:Key="BrandCyanDimBrush" Color="{StaticResource BrandCyanDimColor}" />
  <SolidColorBrush x:Key="BrandMagentaBrush" Color="{StaticResource BrandMagentaColor}" />
  <SolidColorBrush x:Key="BrandMagentaBrightBrush" Color="{StaticResource BrandMagentaBrightColor}" />
  <SolidColorBrush x:Key="BrandMagentaDimBrush" Color="{StaticResource BrandMagentaDimColor}" />
  <SolidColorBrush x:Key="Ink0Brush" Color="{StaticResource Ink0Color}" />
  <SolidColorBrush x:Key="Ink50Brush" Color="{StaticResource Ink50Color}" />
  <SolidColorBrush x:Key="Ink100Brush" Color="{StaticResource Ink100Color}" />
  <SolidColorBrush x:Key="Ink200Brush" Color="{StaticResource Ink200Color}" />
  <SolidColorBrush x:Key="Ink300Brush" Color="{StaticResource Ink300Color}" />
  <SolidColorBrush x:Key="Ink400Brush" Color="{StaticResource Ink400Color}" />
  <SolidColorBrush x:Key="Ink500Brush" Color="{StaticResource Ink500Color}" />
  <SolidColorBrush x:Key="Ink600Brush" Color="{StaticResource Ink600Color}" />
  <SolidColorBrush x:Key="Ink700Brush" Color="{StaticResource Ink700Color}" />
  <SolidColorBrush x:Key="Ink800Brush" Color="{StaticResource Ink800Color}" />
  <SolidColorBrush x:Key="Ink900Brush" Color="{StaticResource Ink900Color}" />
  <SolidColorBrush x:Key="Ink950Brush" Color="{StaticResource Ink950Color}" />
  <SolidColorBrush x:Key="Fg1Brush" Color="{StaticResource Fg1Color}" />
  <SolidColorBrush x:Key="Fg2Brush" Color="{StaticResource Fg2Color}" />
  <SolidColorBrush x:Key="Fg3Brush" Color="{StaticResource Fg3Color}" />
  <SolidColorBrush x:Key="FgMutedBrush" Color="{StaticResource FgMutedColor}" />
  <SolidColorBrush x:Key="FgAccentBrush" Color="{StaticResource FgAccentColor}" />
  <SolidColorBrush x:Key="FgAccentAltBrush" Color="{StaticResource FgAccentAltColor}" />
  <SolidColorBrush x:Key="Bg0Brush" Color="{StaticResource Bg0Color}" />
  <SolidColorBrush x:Key="Bg1Brush" Color="{StaticResource Bg1Color}" />
  <SolidColorBrush x:Key="Bg2Brush" Color="{StaticResource Bg2Color}" />
  <SolidColorBrush x:Key="Bg3Brush" Color="{StaticResource Bg3Color}" />
  <SolidColorBrush x:Key="SuccessBrush" Color="{StaticResource SuccessColor}" />
  <SolidColorBrush x:Key="WarningBrush" Color="{StaticResource WarningColor}" />
  <SolidColorBrush x:Key="DangerBrush" Color="{StaticResource DangerColor}" />
  <SolidColorBrush x:Key="DangerBgBrush" Color="{StaticResource DangerBgColor}" />
  <SolidColorBrush x:Key="Border1Brush" Color="{StaticResource Border1Color}" />
  <SolidColorBrush x:Key="Border2Brush" Color="{StaticResource Border2Color}" />
  <SolidColorBrush x:Key="BorderStrongBrush" Color="{StaticResource BorderStrongColor}" />
  <SolidColorBrush x:Key="BorderAccentBrush" Color="{StaticResource BorderAccentColor}" />
  <SolidColorBrush x:Key="BorderMagentaBrush" Color="{StaticResource BorderMagentaColor}" />

  <!-- Gradients (styles.css:22-23). CSS 135deg runs top-left to bottom-right; 0,0 to 1,1 in the
       bounding box matches it on a square box and is the closest WPF form on other boxes. -->
  <LinearGradientBrush x:Key="BrandGradientBrush" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Offset="0" Color="{StaticResource BrandCyanColor}" />
    <GradientStop Offset="1" Color="{StaticResource BrandMagentaColor}" />
  </LinearGradientBrush>
  <LinearGradientBrush x:Key="BrandGradientSoftBrush" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Offset="0" Color="#2E17D4FA" />
    <GradientStop Offset="1" Color="#2EF22F89" />
  </LinearGradientBrush>

  <!-- Radii (styles.css:69-73) -->
  <CornerRadius x:Key="RXsRadius">4</CornerRadius>
  <CornerRadius x:Key="RSmRadius">6</CornerRadius>
  <CornerRadius x:Key="RMdRadius">10</CornerRadius>
  <CornerRadius x:Key="RLgRadius">14</CornerRadius>
  <CornerRadius x:Key="RPillRadius">999</CornerRadius>

  <!-- Shadows (styles.css:77, :80). WPF has one DropShadowEffect per element: shadow-2 is exact in
       offset and blur; glow-duo keeps its cyan layer and drops the 48px magenta halo. -->
  <DropShadowEffect x:Key="Shadow2Effect" Color="Black" Direction="270" ShadowDepth="4" BlurRadius="16" Opacity="0.45" />
  <DropShadowEffect x:Key="GlowDuoEffect" Color="{StaticResource BrandCyanColor}" ShadowDepth="0" BlurRadius="24" Opacity="0.25" />

  <!-- Motion (styles.css:84-86) -->
  <KeySpline x:Key="EaseOutSpline" ControlPoint1="0.2,0.7" ControlPoint2="0.2,1" />
  <Duration x:Key="DurFastDuration">0:0:0.12</Duration>
  <Duration x:Key="DurMedDuration">0:0:0.22</Duration>

  <!-- Typography (styles.css:89-91), the vendored UI families (assets/fonts/README.md) embedded by
       RBX15.App.csproj. Fallbacks follow the CSS stacks on Windows: system-ui is Segoe UI, ui-monospace is Consolas. -->
  <FontFamily x:Key="FontDisplay">pack://application:,,,/RBX15.App;component/Fonts/#Space Grotesk, pack://application:,,,/RBX15.App;component/Fonts/#Inter, Segoe UI</FontFamily>
  <FontFamily x:Key="FontBody">pack://application:,,,/RBX15.App;component/Fonts/#Inter, Segoe UI</FontFamily>
  <FontFamily x:Key="FontMono">pack://application:,,,/RBX15.App;component/Fonts/#JetBrains Mono, Consolas</FontFamily>

</ResourceDictionary>
```

Create `src/RBX15.App/Converters/UpperCaseConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>
/// CSS <c>text-transform: uppercase</c>, which 4.1 puts on the header byline, ribbon tabs, group
/// labels, panel headings and the footer (styles.css:153, :180, :215, :258, :686). The view-models
/// keep 4.1's source strings; the view upper-cases them. Invariant culture, as Chromium upper-cases
/// with the document language (en, editor.html:2). One-way only.
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class UpperCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? s.ToUpperInvariant() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("UpperCaseConverter is one-way.");
}
```

Create `src/RBX15.App/Themes/Controls.xaml`:

```xml
<!--
  Shared control styles over Tokens.xaml. Later tasks consume these keys; every value cites styles.css.
  Not in M3a: hover transitions (styles.css:182, :232, :385; 120/220 ms) and CSS letter-spacing, which
  WPF has no property for. Both belong to the M3b look sign-off (ledger theme-tokens-shape-motion-type
  and shell-header stay open until then).
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:conv="clr-namespace:RBX15.App.Converters">

  <ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="Tokens.xaml" />
  </ResourceDictionary.MergedDictionaries>

  <conv:UpperCaseConverter x:Key="UpperCase" />

  <!-- Keyboard focus ring: outline 2px solid cyan, offset 2px (styles.css:357-367; ledger
       a11y-focus-rings). WPF draws FocusVisualStyle for keyboard focus only, like :focus-visible. -->
  <Style x:Key="FocusRing">
    <Setter Property="Control.Template">
      <Setter.Value>
        <ControlTemplate>
          <Rectangle Margin="-4" Stroke="{StaticResource BrandCyanBrush}" StrokeThickness="2" SnapsToDevicePixels="True" />
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Thin scrollbars: thumb brand-navy-soft on an ink-950 track, no hover recolor. Electron 41 ignores
       the ::-webkit-scrollbar rules once scrollbar-color/width are set (styles.css:115; ledger
       theme-scrollbars: match the effective thin flat look, not the dead gradient).
       The 8 DIP width and the 1 DIP thumb inset have no 4.1 measurement (ruling 10): new v5 surface;
       provisional; M3b look sign-off. The colors are exact and tested. -->
  <Style x:Key="ThinScrollThumb" TargetType="Thumb">
    <Setter Property="OverridesDefaultStyle" Value="True" />
    <Setter Property="IsTabStop" Value="False" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Thumb">
          <Border Margin="1" Background="{StaticResource BrandNavySoftBrush}" CornerRadius="{StaticResource RXsRadius}" />
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="ThinScrollPage" TargetType="RepeatButton">
    <Setter Property="OverridesDefaultStyle" Value="True" />
    <Setter Property="IsTabStop" Value="False" />
    <Setter Property="Focusable" Value="False" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RepeatButton">
          <Border Background="Transparent" />
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ScrollBar">
    <Setter Property="OverridesDefaultStyle" Value="True" />
    <Setter Property="Background" Value="{StaticResource Ink950Brush}" />
    <Setter Property="Width" Value="8" />
    <Setter Property="MinWidth" Value="8" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollBar">
          <Border Background="{TemplateBinding Background}">
            <Track x:Name="PART_Track" IsDirectionReversed="True">
              <Track.DecreaseRepeatButton>
                <RepeatButton Style="{StaticResource ThinScrollPage}" Command="ScrollBar.PageUpCommand" />
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb Style="{StaticResource ThinScrollThumb}" />
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Style="{StaticResource ThinScrollPage}" Command="ScrollBar.PageDownCommand" />
              </Track.IncreaseRepeatButton>
            </Track>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Width" Value="Auto" />
        <Setter Property="MinWidth" Value="0" />
        <Setter Property="Height" Value="8" />
        <Setter Property="MinHeight" Value="8" />
        <Setter Property="Template">
          <Setter.Value>
            <ControlTemplate TargetType="ScrollBar">
              <Border Background="{TemplateBinding Background}">
                <Track x:Name="PART_Track" IsDirectionReversed="False">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Style="{StaticResource ThinScrollPage}" Command="ScrollBar.PageLeftCommand" />
                  </Track.DecreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb Style="{StaticResource ThinScrollThumb}" />
                  </Track.Thumb>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Style="{StaticResource ThinScrollPage}" Command="ScrollBar.PageRightCommand" />
                  </Track.IncreaseRepeatButton>
                </Track>
              </Border>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Trigger>
    </Style.Triggers>
  </Style>

  <!-- .btn (styles.css:375-388) with .btn-secondary (:398-406) and .btn-primary (:389-397);
       :disabled is opacity .4 with no shadow (:414-419; ledger cross-undo-disabled-style). -->
  <Style x:Key="SecondaryButton" TargetType="ButtonBase">
    <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}" />
    <Setter Property="FontFamily" Value="{StaticResource FontBody}" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="{StaticResource Fg1Brush}" />
    <Setter Property="BorderBrush" Value="{StaticResource Border1Brush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="16,8" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Background">
      <Setter.Value>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
          <GradientStop Offset="0" Color="{StaticResource BrandNavySoftColor}" />
          <GradientStop Offset="1" Color="{StaticResource BrandNavyColor}" />
        </LinearGradientBrush>
      </Setter.Value>
    </Setter>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ButtonBase">
          <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{StaticResource RSmRadius}"
                  Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="False" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{StaticResource BrandCyanBrush}" />
              <Setter TargetName="Chrome" Property="Effect">
                <Setter.Value>
                  <DropShadowEffect Color="{StaticResource BrandCyanColor}" ShadowDepth="0" BlurRadius="8" Opacity="0.15" />
                </Setter.Value>
              </Setter>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4" />
              <Setter Property="Cursor" Value="Arrow" />
              <Setter TargetName="Chrome" Property="Effect" Value="{x:Null}" />
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{StaticResource Border1Brush}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="PrimaryButton" TargetType="ButtonBase" BasedOn="{StaticResource SecondaryButton}">
    <Setter Property="Foreground" Value="{StaticResource Ink950Brush}" />
    <Setter Property="Background" Value="{StaticResource BrandGradientBrush}" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ButtonBase">
          <Border x:Name="Chrome" Background="{TemplateBinding Background}" CornerRadius="{StaticResource RSmRadius}"
                  Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
            <Border.Effect>
              <DropShadowEffect Color="{StaticResource BrandCyanColor}" Direction="270" ShadowDepth="2" BlurRadius="8" Opacity="0.25" />
            </Border.Effect>
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="False" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="RenderTransform">
                <Setter.Value>
                  <TranslateTransform Y="-1" />
                </Setter.Value>
              </Setter>
              <Setter TargetName="Chrome" Property="Effect" Value="{StaticResource GlowDuoEffect}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4" />
              <Setter Property="Cursor" Value="Arrow" />
              <Setter TargetName="Chrome" Property="RenderTransform" Value="{x:Null}" />
              <Setter TargetName="Chrome" Property="Effect" Value="{x:Null}" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- .btn-sm (styles.css:407) -->
  <Style x:Key="SecondaryButtonSm" TargetType="ButtonBase" BasedOn="{StaticResource SecondaryButton}">
    <Setter Property="Padding" Value="10,5" />
    <Setter Property="FontSize" Value="11" />
  </Style>
  <Style x:Key="PrimaryButtonSm" TargetType="ButtonBase" BasedOn="{StaticResource PrimaryButton}">
    <Setter Property="Padding" Value="10,5" />
    <Setter Property="FontSize" Value="11" />
  </Style>

  <!-- .ribbon-tab (styles.css:171-196): mono 11px 600 uppercase, padding 6px 16px, fg-3; hover fg-2;
       active cyan on navy-deep with a 2px brand-gradient underline at bottom -1px. -->
  <Style x:Key="RibbonTab" TargetType="RadioButton">
    <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}" />
    <Setter Property="FontFamily" Value="{StaticResource FontMono}" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="{StaticResource Fg3Brush}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Padding" Value="16,6" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RadioButton">
          <Grid>
            <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
              <TextBlock Text="{Binding Content, RelativeSource={RelativeSource TemplatedParent}, Converter={StaticResource UpperCase}}" />
            </Border>
            <Rectangle x:Name="Underline" Height="2" VerticalAlignment="Bottom" Margin="0,0,0,-1"
                       Fill="{StaticResource BrandGradientBrush}" Visibility="Collapsed" />
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter Property="Foreground" Value="{StaticResource Fg2Brush}" />
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter Property="Foreground" Value="{StaticResource BrandCyanBrush}" />
              <Setter Property="Background" Value="{StaticResource BrandNavyDeepBrush}" />
              <Setter TargetName="Underline" Property="Visibility" Value="Visible" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- .ribbon-group (styles.css:200-207): padding 0 10px, 1px right divider border-1; the last group
       sets BorderThickness 0 (:207). A group is this Border around a DockPanel whose first child is
       the RibbonGroupLabel TextBlock (docked bottom) and whose second child is the items.
       Tasks 3 and 4 share it (one definition; the critic's C7). -->
  <Style x:Key="RibbonGroup" TargetType="Border">
    <Setter Property="Padding" Value="10,0" />
    <Setter Property="BorderThickness" Value="0,0,1,0" />
    <Setter Property="BorderBrush" Value="{DynamicResource Border1Brush}" />
  </Style>

  <!-- .ribbon-group-label (styles.css:211-221): mono 10px, fg-muted, 600, padding-top 5. Labels write
       their text upper case (CSS text-transform: uppercase). -->
  <Style x:Key="RibbonGroupLabel" TargetType="TextBlock">
    <Setter Property="DockPanel.Dock" Value="Bottom" />
    <Setter Property="FontFamily" Value="{DynamicResource FontMono}" />
    <Setter Property="FontSize" Value="10" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="{DynamicResource FgMutedBrush}" />
    <Setter Property="HorizontalAlignment" Value="Center" />
    <Setter Property="Padding" Value="0,5,0,0" />
  </Style>

  <!-- .panel h3 (styles.css:254-263): mono 11px 600 uppercase fg-3, margin 14px 0 8px (0 on the first). -->
  <Style x:Key="PanelHeading" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource FontMono}" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="{StaticResource Fg3Brush}" />
    <Setter Property="Margin" Value="0,14,0,8" />
  </Style>

</ResourceDictionary>
```

Replace `src/RBX15.App/App.xaml` with this interim version. `StartupUri` stays until Step 15 replaces the template window:

```xml
<Application x:Class="RBX15.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- Controls.xaml merges Tokens.xaml; one theme for every window and dialog (spec §4). -->
        <ResourceDictionary Source="Themes/Controls.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 13: Run and commit**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.ThemeTokenTests"`
Expected: `Passed!  - Failed:     0, Passed:    58, Skipped:     0, Total:    58` for `RBX15.App.Tests.dll`. The 58 are 40 color tokens, 1 accounting check, 2 gradients, 5 radii, 2 durations, 1 ease, 1 shadow, 1 scrollbar and 5 font cases.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 582`; Parity.Tests `Passed: 35`; App.Tests `Passed: 118, Failed: 0`.

Commit (end the message with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/Themes src/RBX15.App/Converters src/RBX15.App/App.xaml src/RBX15.App.Tests/ThemeTokenTests.cs
git commit -m "feat(app): theme dictionary from styles.css tokens, control styles and bundled UI fonts"
```

- [ ] **Step 14: Write the failing shell tests** (ledger `shell-window-frame`, `shell-window-title`, `shell-header`, `shell-header-version-hardcoded`, `shell-footer`, `shell-layout-skeleton`, `shell-responsive-breakpoint`, `shell-window-size`, `cross-window-dpi-rendering`)

Create `src/RBX15.App.Tests/ShellTests.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using RBX15.App.Converters;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>The window, header, footer and layout skeleton (ledger shell-* rows).</summary>
public class ShellTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly Rect Primary = new(0, 0, 1920, 1040);

    [Fact] // shell-header-version-hardcoded (fix): the version comes from the assembly, without "+sha"
    public void The_app_version_is_5_0_0()
    {
        Assert.Equal("5.0.0", AppInfo.Version);
        Assert.Equal(new Version(5, 0, 0, 0), typeof(AppInfo).Assembly.GetName().Version);
    }

    [Fact] // shell-window-title (keep): main.js:8 and editor.html:6
    public void The_product_name_is_4_1s_window_title()
    {
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", AppInfo.ProductName);
        Assert.Contains("<title>RBX15 Classic Shirt and Pants Maker</title>", File.ReadAllText(Path.Combine(Root, "editor.html")));
    }

    [Fact] // shell-header, shell-footer: editor.html:16-17, :343 verbatim; the byline's version is major.minor
    public void Header_and_footer_text_match_4_1()
    {
        var vm = TestShell.Main(new EditorSession());
        var html = File.ReadAllText(Path.Combine(Root, "editor.html"));
        Assert.Contains("<h1>RBX15 Classic Shirt &amp; Pants Maker</h1>", html);
        Assert.Contains("<span>ItsjustEste | v4.1</span>", html);
        Assert.Contains("<footer>Developed at 626 Labs LLC</footer>", html);

        Assert.Equal(AppInfo.ProductName, vm.Title);
        Assert.Equal("RBX15 Classic Shirt & Pants Maker", vm.HeaderTitle);
        Assert.Equal("ItsjustEste | v5.0", vm.HeaderByline);
        Assert.Equal("Developed at 626 Labs LLC", vm.FooterText);
    }

    [Fact] // text-transform: uppercase (styles.css:153, :686)
    public void Upper_case_converter_matches_css_text_transform()
    {
        var c = new UpperCaseConverter();
        Assert.Equal("ITSJUSTESTE | V5.0", c.Convert("ItsjustEste | v5.0", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("DEVELOPED AT 626 LABS LLC", c.Convert("Developed at 626 Labs LLC", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Null(c.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => c.ConvertBack("X", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Theory] // shell-responsive-breakpoint (keep): @media (max-width: 900px) { .panel { width: 200px } } (styles.css:694-697)
    [InlineData(1200, 280)]
    [InlineData(901, 280)]
    [InlineData(900.5, 280)]
    [InlineData(900, 200)]
    [InlineData(640, 200)]
    public void The_left_panel_narrows_at_900(double viewport, double panel)
    {
        Assert.Contains("@media (max-width: 900px) {\n  .panel { width: 200px; }", File.ReadAllText(Path.Combine(Root, "styles.css")).Replace("\r\n", "\n"));
        Assert.Equal(panel, ShellLayout.PanelWidthFor(viewport));
    }

    [Fact] // the window: title, theme, 4.1's size default, the skeleton's regions, one Home tab
    public void The_main_window_builds_the_skeleton()
    {
        var store = new FakeSettingsStore();
        var facts = StaThread.Run(() =>
        {
            var w = new MainWindow(TestShell.Main(new EditorSession()), store, [Primary]);
            string Text(string name) => ((TextBlock)w.FindName(name)).Text;
            var tabs = (StackPanel)w.FindName("RibbonTabs");
            return new
            {
                w.Title,
                Size = (w.Left, w.Top, w.Width, w.Height, w.WindowState),
                Min = (w.MinWidth, w.MinHeight),
                Header = Text("HeaderTitle"),
                Byline = Text("HeaderByline"),
                Footer = Text("FooterText"),
                Tabs = tabs.Children.OfType<RadioButton>().Select(r => ((string)r.Content, r.IsChecked)).ToArray(),
                Panel = ((ColumnDefinition)w.FindName("PanelColumn")).Width.Value,
                Regions = new[] { "RibbonPane", "LeftPanelContent", "CanvasArea" }.All(n => w.FindName(n) is not null),
                RightPanel = w.FindName("RightPanel"),
                Background = ((System.Windows.Media.SolidColorBrush)w.Background).Color.ToString(),
            };
        });
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", facts.Title);
        Assert.Equal((360.0, 120.0, 1200.0, 800.0, WindowState.Normal), facts.Size);
        Assert.Equal((640.0, 480.0), facts.Min);
        Assert.Equal("RBX15 Classic Shirt & Pants Maker", facts.Header);
        Assert.Equal("ITSJUSTESTE | V5.0", facts.Byline);
        Assert.Equal("DEVELOPED AT 626 LABS LLC", facts.Footer);
        Assert.Equal([("Home", (bool?)true)], facts.Tabs);
        Assert.Equal(280, facts.Panel);
        Assert.True(facts.Regions);
        Assert.Null(facts.RightPanel);
        Assert.Equal("#FF0F1F31", facts.Background);
        Assert.Empty(store.Saves);
    }

    [Fact] // shell-window-size (fix): a saved placement is restored before the window shows, maximized included
    public void The_main_window_restores_a_saved_placement()
    {
        var store = new FakeSettingsStore(new WindowPlacement(100, 50, 900, 700, true));
        var (size, saved) = StaThread.Run(() =>
        {
            var w = new MainWindow(TestShell.Main(new EditorSession()), store, [Primary]);
            return ((w.Left, w.Top, w.Width, w.Height, w.WindowState), w.CapturePlacement());
        });
        Assert.Equal((100.0, 50.0, 900.0, 700.0, WindowState.Maximized), size);
        Assert.True(saved.Maximized);
    }

    [Fact] // a normal window captures its own bounds
    public void A_normal_window_captures_its_bounds()
    {
        var saved = StaThread.Run(() =>
        {
            var w = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
            w.Left = 10; w.Top = 20; w.Width = 1000; w.Height = 600;
            return w.CapturePlacement();
        });
        Assert.Equal(new WindowPlacement(10, 20, 1000, 600, false), saved);
    }

    [Fact] // StaThread loads the same dictionary App.xaml merges, so theme tests see the app's theme
    public void App_xaml_merges_the_theme_the_tests_load()
    {
        var app = XDocument.Load(Path.Combine(Root, "src", "RBX15.App", "App.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var merged = app.Descendants(p + "ResourceDictionary").Select(d => (string?)d.Attribute("Source")).OfType<string>().ToArray();
        Assert.Equal(["Themes/Controls.xaml"], merged);
        Assert.Equal(StaThread.Theme, "/RBX15.App;component/" + merged[0]);
        Assert.Null(app.Root!.Attribute("StartupUri"));
    }

    [Fact] // spec §4 "Display scaling: per-monitor DPI v2" (cross-window-dpi-rendering)
    public void The_app_manifest_declares_per_monitor_v2()
    {
        var manifest = XDocument.Load(Path.Combine(Root, "src", "RBX15.App", "app.manifest"));
        XNamespace ws = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        Assert.Equal("PerMonitorV2", manifest.Descendants(ws + "dpiAwareness").Single().Value);
    }
}
```

Create `src/RBX15.App.Tests/TestShell.cs`, the tests' one `MainViewModel` builder (ruling 15). Tasks 2 to 4 insert their lines above the two anchor comments, exactly as they do in `Composition.cs`:

```csharp
using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>
/// The tests' one MainViewModel builder: the composition root of App (Composition.Build) with fakes in
/// place of the native services. Every later task that adds a required member to MainViewModel adds
/// its construction line and its member here, at the two anchors, so no earlier test breaks (CS9035).
/// </summary>
internal static class TestShell
{
    public static MainViewModel Main(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            // M3a: required members above this line.
        };
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL. The errors include `error CS0234: The type or namespace name 'ViewModels' does not exist in the namespace 'RBX15.App'` and `error CS0103: The name 'AppInfo' does not exist in the current context`.

- [ ] **Step 15: Implement AppInfo, ShellLayout, MainViewModel, the window and the composition root**

Create `src/RBX15.App/AppInfo.cs`:

```csharp
using System.Reflection;
using RBX15.Engine;

namespace RBX15.App;

/// <summary>Product identity shown by the shell.</summary>
public static class AppInfo
{
    /// <summary>
    /// The window and dialog title. 4.1's BrowserWindow title and editor.html's title element carry this
    /// string (main.js:8, editor.html:6; ledger shell-window-title, keep). ProjectIO writes the same
    /// string as a project's <c>app</c> field.
    /// </summary>
    public const string ProductName = ProjectIO.AppName;

    /// <summary>
    /// The assembly's informational version without any "+metadata" suffix: "5.0.0". 4.1 hand-typed
    /// "v4.1" in the header (editor.html:17); v5 reads it from the build (ledger
    /// shell-header-version-hardcoded, fix).
    /// </summary>
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        var info = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? throw new InvalidOperationException("RBX15.App has no informational version.");
        int plus = info.IndexOf('+');
        return plus < 0 ? info : info[..plus];
    }
}
```

Create `src/RBX15.App/ShellLayout.cs`:

```csharp
namespace RBX15.App;

/// <summary>
/// 4.1's layout numbers that are not theme tokens (ledger shell-layout-skeleton,
/// shell-responsive-breakpoint, shell-window-size).
/// </summary>
public static class ShellLayout
{
    /// <summary>The left <c>.panel</c> width (styles.css:247).</summary>
    public const double PanelWidth = 280;

    /// <summary>The left panel width at the breakpoint (styles.css:694-697).</summary>
    public const double NarrowPanelWidth = 200;

    /// <summary><c>@media (max-width: 900px)</c> (styles.css:694): a viewport 900 wide or less is narrow.</summary>
    public const double Breakpoint = 900;

    /// <summary>4.1's window size (main.js:5-13). Also the restore default in <see cref="Services.PlacementGuard"/>.</summary>
    public const double DefaultWidth = 1200, DefaultHeight = 800;

    /// <summary>
    /// The smallest window. 4.1 set none (ledger shell-window-size, quirk: the panels overlap when
    /// shrunk); 640x480 is the floor the row's parity check resizes to.
    /// </summary>
    public const double MinWidth = 640, MinHeight = 480;

    /// <summary>The left panel width for a viewport (window client) width in DIPs, which equal CSS px.</summary>
    public static double PanelWidthFor(double viewportWidth) => viewportWidth <= Breakpoint ? NarrowPanelWidth : PanelWidth;
}
```

Create `src/RBX15.App/ViewModels/MainViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace RBX15.App.ViewModels;

/// <summary>
/// The window's view-model: the shell text and, from later tasks, one sub-view-model per region
/// (Canvas in Task 2, Home in Task 3, Layers and Status in Task 4).
/// </summary>
public sealed partial class MainViewModel(EditorSession session) : ObservableObject
{
    public EditorSession Session { get; } = session ?? throw new ArgumentNullException(nameof(session));

    /// <summary>The window title (main.js:8, editor.html:6; ledger shell-window-title, keep).</summary>
    public string Title => AppInfo.ProductName;

    /// <summary>The header h1, verbatim (editor.html:16, <c>&amp;amp;</c> decoded; ledger shell-header).</summary>
    public string HeaderTitle => "RBX15 Classic Shirt & Pants Maker";

    /// <summary>
    /// The header byline. 4.1 hand-typed "ItsjustEste | v4.1" (editor.html:17); v5 builds the same shape
    /// from the assembly's major.minor (ledger shell-header-version-hardcoded, fix, whose parity check
    /// reads 'ItsjustEste | v&lt;major.minor&gt;'). The view upper-cases it (styles.css:153).
    /// </summary>
    public string HeaderByline => "ItsjustEste | v" + MajorMinor(AppInfo.Version);

    /// <summary>The footer, verbatim (editor.html:343; ledger shell-footer). The view upper-cases it (styles.css:686).</summary>
    public string FooterText => "Developed at 626 Labs LLC";

    static string MajorMinor(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? parts[0] + "." + parts[1] : version;
    }
}
```

Replace `src/RBX15.App/MainWindow.xaml` with:

```xml
<!--
  The shell skeleton (ledger shell-layout-skeleton): header, ribbon, the app row (left panel and
  canvas area), the status strip row, footer, top to bottom (styles.css:99-108, editor.html:15-343).
  4.1 has no right panel: .rpanel (styles.css:462-469) is styled but never used, so it is not ported.
  Later tasks fill the named regions at their anchors: Task 2 the CanvasArea, Task 3 the RibbonPane,
  Task 4 the LeftPanelContent and the status strip row. Task 4 Step 21 prints the final file.
-->
<Window x:Class="RBX15.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:RBX15.App.Views"
        xmlns:vm="clr-namespace:RBX15.App.ViewModels"
        xmlns:app="clr-namespace:RBX15.App"
        x:Name="Root"
        Title="{x:Static app:AppInfo.ProductName}"
        Width="{x:Static app:ShellLayout.DefaultWidth}" Height="{x:Static app:ShellLayout.DefaultHeight}"
        MinWidth="{x:Static app:ShellLayout.MinWidth}" MinHeight="{x:Static app:ShellLayout.MinHeight}"
        Background="{StaticResource Bg0Brush}"
        Foreground="{StaticResource Fg2Brush}"
        FontFamily="{StaticResource FontBody}"
        UseLayoutRounding="True"
        TextOptions.TextRenderingMode="Grayscale"
        d:DataContext="{d:DesignInstance Type=vm:MainViewModel}"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d">

  <Grid x:Name="Viewport">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <!-- header (styles.css:129-155): padding 10px 24px, gap 16px, 135deg navy / navy-deep / navy,
         1px border-accent bottom, shadow 0 2px 8px rgba(0,0,0,.35); drawn above the ribbon. -->
    <Border x:Name="Header" Grid.Row="0" Panel.ZIndex="1" Padding="24,10"
            BorderBrush="{StaticResource BorderAccentBrush}" BorderThickness="0,0,0,1">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
          <GradientStop Offset="0" Color="{StaticResource BrandNavyColor}" />
          <GradientStop Offset="0.5" Color="{StaticResource BrandNavyDeepColor}" />
          <GradientStop Offset="1" Color="{StaticResource BrandNavyColor}" />
        </LinearGradientBrush>
      </Border.Background>
      <Border.Effect>
        <DropShadowEffect Color="Black" Direction="270" ShadowDepth="2" BlurRadius="8" Opacity="0.35" />
      </Border.Effect>
      <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
        <!-- header h1 (styles.css:139-148): Space Grotesk 20px 600, text filled with the brand gradient. -->
        <TextBlock x:Name="HeaderTitle" Text="{Binding HeaderTitle, Mode=OneTime}" VerticalAlignment="Center"
                   FontFamily="{StaticResource FontDisplay}" FontSize="20" FontWeight="SemiBold"
                   Foreground="{StaticResource BrandGradientBrush}" />
        <!-- header span (styles.css:149-155): JetBrains Mono 11px fg-3 uppercase; gap 16px (:133). -->
        <TextBlock x:Name="HeaderByline" Margin="16,0,0,0" VerticalAlignment="Center"
                   FontFamily="{StaticResource FontMono}" FontSize="11" Foreground="{StaticResource Fg3Brush}"
                   Text="{Binding HeaderByline, Mode=OneTime, Converter={StaticResource UpperCase}}" />
      </StackPanel>
    </Border>

    <!-- .ribbon (styles.css:160-164): 180deg navy-deep to ink-900, 1px border-1 bottom. -->
    <Border x:Name="Ribbon" Grid.Row="1" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,0,0,1">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
          <GradientStop Offset="0" Color="{StaticResource BrandNavyDeepColor}" />
          <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
        </LinearGradientBrush>
      </Border.Background>
      <DockPanel>
        <!-- .ribbon-tabs (styles.css:165-170): padding 0 8px, 180deg ink-950 to ink-900, 1px border-1 bottom.
             M3a shows Home only (editor.html:23); Layout, Details and Text arrive in M4. -->
        <Border DockPanel.Dock="Top" Padding="8,0" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,0,0,1">
          <Border.Background>
            <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
              <GradientStop Offset="0" Color="{StaticResource Ink950Color}" />
              <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
            </LinearGradientBrush>
          </Border.Background>
          <StackPanel x:Name="RibbonTabs" Orientation="Horizontal">
            <RadioButton x:Name="HomeTab" Style="{StaticResource RibbonTab}" GroupName="RibbonTabs"
                         Content="Home" IsChecked="True" AutomationProperties.Name="Home tab" />
          </StackPanel>
        </Border>
        <!-- .ribbon-pane.active (styles.css:197-198): padding 8px 12px, groups in a row that never
             shrink and scroll horizontally (ledger ribbon-overflow-hscroll). Task 3 puts HomeRibbon
             inside; HomeRibbon is the pane, so it brings its own ScrollViewer and its 12,8 margin. -->
        <ContentControl x:Name="RibbonPane" Focusable="False" />
      </DockPanel>
    </Border>

    <!-- .app (styles.css:241): the left panel and the canvas area. -->
    <Grid x:Name="AppRow" Grid.Row="2">
      <Grid.ColumnDefinitions>
        <ColumnDefinition x:Name="PanelColumn" Width="280" />
        <ColumnDefinition Width="*" />
      </Grid.ColumnDefinitions>

      <!-- .panel (styles.css:246-253): 280px (200px at the breakpoint), 180deg navy-deep to ink-900,
           1px border-1 right, padding 14px, vertical scroll. Task 4 fills LeftPanelContent. -->
      <Border x:Name="LeftPanel" Grid.Column="0" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,0,1,0">
        <Border.Background>
          <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
            <GradientStop Offset="0" Color="{StaticResource BrandNavyDeepColor}" />
            <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
          </LinearGradientBrush>
        </Border.Background>
        <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Focusable="False">
          <StackPanel x:Name="LeftPanelContent" Margin="14"><!-- T4: left panel --></StackPanel>
        </ScrollViewer>
      </Border>

      <!-- .canvas-area (styles.css:441-450): ink-950. Task 2 fills it with the canvas host and zoom pill. -->
      <Grid x:Name="CanvasArea" Grid.Column="1" Background="{StaticResource Ink950Brush}" ClipToBounds="True" />
    </Grid>

    <!-- The status strip row (spec §4, new in v5): Task 4 puts the strip here, above the footer. -->
    <!-- T4: status strip -->

    <!-- footer (styles.css:678-689): 180deg ink-950 to ink-900, 1px border-1 top, padding 7px 24px,
         JetBrains Mono 10px fg-muted uppercase, centered. -->
    <Border x:Name="Footer" Grid.Row="4" Padding="24,7" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,1,0,0">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
          <GradientStop Offset="0" Color="{StaticResource Ink950Color}" />
          <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
        </LinearGradientBrush>
      </Border.Background>
      <TextBlock x:Name="FooterText" HorizontalAlignment="Center" FontFamily="{StaticResource FontMono}" FontSize="10"
                 Foreground="{StaticResource FgMutedBrush}"
                 Text="{Binding FooterText, Mode=OneTime, Converter={StaticResource UpperCase}}" />
    </Border>
  </Grid>
</Window>
```

Replace `src/RBX15.App/MainWindow.xaml.cs` with:

```csharp
using System.ComponentModel;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// The main window: the shell skeleton plus window placement memory (ledger shell-window-size, fix).
/// The placement is restored before the window is shown and saved when it closes.
/// </summary>
public partial class MainWindow : Window
{
    readonly ISettingsStore _settings;
    bool _maximizedBeforeMinimize;

    /// <param name="workAreas">The monitors' work areas, primary first; null reads them from Windows.</param>
    public MainWindow(MainViewModel viewModel, ISettingsStore settings, IReadOnlyList<Rect>? workAreas = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        DataContext = viewModel;
        InitializeComponent();
        ApplyPlacement(PlacementGuard.Resolve(settings.LoadWindowPlacement(), workAreas ?? MonitorWorkAreas.Get(),
                                              ShellLayout.DefaultWidth, ShellLayout.DefaultHeight));
        Viewport.SizeChanged += (_, e) => PanelColumn.Width = new GridLength(ShellLayout.PanelWidthFor(e.NewSize.Width));
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) _maximizedBeforeMinimize = WindowState == WindowState.Maximized; };
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    internal void ApplyPlacement(WindowPlacement placement)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        WindowState = placement.Maximized ? WindowState.Maximized : WindowState.Normal;
        _maximizedBeforeMinimize = placement.Maximized;
    }

    /// <summary>
    /// The placement to save: the restore bounds whenever the window is maximized or minimized, and
    /// maximized when it is maximized or was maximized before it was minimized.
    /// </summary>
    internal WindowPlacement CapturePlacement()
    {
        var bounds = WindowState == WindowState.Normal || RestoreBounds.IsEmpty
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        bool maximized = WindowState == WindowState.Maximized || (WindowState == WindowState.Minimized && _maximizedBeforeMinimize);
        return new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel) _settings.SaveWindowPlacement(CapturePlacement());
    }
}
```

Replace `src/RBX15.App/App.xaml` with this version, which has no `StartupUri`, because `OnStartup` builds the window:

```xml
<Application x:Class="RBX15.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowClose">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- Controls.xaml merges Tokens.xaml; one theme for every window and dialog (spec §4). -->
        <ResourceDictionary Source="Themes/Controls.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

Replace `src/RBX15.App/App.xaml.cs` with:

```csharp
using System.IO;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// Startup. 4.1's startup state is a fresh document (editor.js:2745-2746; ledger project-startup).
/// The view-models are built in one place, <see cref="Composition.Build"/>; later tasks add their
/// services and view-models there, never here.
/// </summary>
public partial class App : Application
{
    /// <summary>The settings directory: %LOCALAPPDATA%\RBX15 (MSIX redirects it into the package's local data).</summary>
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RBX15");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var session = new EditorSession();
        var settings = new JsonSettingsStore(SettingsDirectory);
        var main = Composition.Build(session, () => Current?.MainWindow);
        MainWindow = new MainWindow(main, settings);
        MainWindow.Show();
    }
}
```

Create `src/RBX15.App/Composition.cs`, the app's one composition root (ruling 15). `owner` returns the window native dialogs are owned by; Tasks 3 and 4 hand it to their services. Tasks 2 to 4 insert their lines above the two anchor comments:

```csharp
using System.Windows;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// Builds the view-models over one <see cref="EditorSession"/>. Each task that adds a required member
/// to MainViewModel adds its construction line and its member here, at the two anchors, and the same
/// lines with fakes in the tests' TestShell.
/// </summary>
internal static class Composition
{
    public static MainViewModel Build(EditorSession session, Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(owner);
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            // M3a: required members above this line.
        };
    }
}
```

- [ ] **Step 16: Run, launch and commit**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.ShellTests"`
Expected: `Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 582, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`; App.Tests `Passed: 132, Failed: 0, Skipped: 0`.

Launch check (by the implementer; record the result in the commit body):

1. If `%LOCALAPPDATA%\RBX15\settings.json` exists, note that. Then run `src/RBX15.App/bin/Release/net10.0-windows/RBX15.App.exe`.
2. Check the window:
   - The title bar reads `RBX15 Classic Shirt and Pants Maker`.
   - The header reads `RBX15 Classic Shirt & Pants Maker` (cyan to magenta) and `ITSJUSTESTE | V5.0`.
   - There is one `HOME` tab, active with the gradient underline.
   - The left panel is 280 wide and the center area is dark.
   - The footer reads `DEVELOPED AT 626 LABS LLC`.
3. Close the window. `settings.json` now holds the window rectangle.
4. Move the window, maximize it, and close it. Relaunch: it reopens maximized on the same monitor, and restoring it returns the moved bounds.
5. Narrow the window below 900 DIP: the left panel drops to 200.
6. With two monitors at different scales, close the window on the secondary monitor and relaunch: it opens there.
7. Delete the `%LOCALAPPDATA%\RBX15` folder if step 1 found none.

Commit (end the message with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App src/RBX15.App.Tests/ShellTests.cs src/RBX15.App.Tests/TestShell.cs
git commit -m "feat(app): shell skeleton with header, Home ribbon chrome, panels, footer and versioned byline"
```

**Handed to Task 5** (Task 5 Step 7's `m3a-ledger.py` applies both; the row stays open for the M3b look sign-off):

New divergence row (Task 5 adds it with `tools/ledger`, owner `App.Shell`):

- `font-ui-weight-600`. 4.1: Chromium instances the Google variable fonts at weight 600 for the header title, ribbon tabs, group labels and panel headings (styles.css:142, :175, :220, :261). v5: WPF exposes only the named instances of the vendored variable files, and Space Grotesk and JetBrains Mono have no 600 instance there, so those labels draw at 700. Inter's 600 is exact. Pinned by `ThemeTokenTests.Ui_fonts_resolve_to_the_vendored_files`.

Append to `shell-window-size`'s divergence: "The window has a 640x480 minimum (4.1 had none, and its panels overlapped when shrunk)."

Rows Task 1 serves but that must stay open after M3a: `shell-layout-skeleton` (fixed ribbon height, M4), `theme-tokens-shape-motion-type`, `shell-header`, `shell-footer`, `ribbon-tabs-switching` (letter-spacing and transitions, M3b), `shell-no-persistence` and `shell-userdata-footprint` (PRIVACY.md, M6), `ribbon-tab-switch` (only one tab exists).

**Interface additions adopted (Task 1).** These public names are beyond `interfaces.md`; Task 1 produces them, and Tasks 2 to 5 consume them verbatim.

1. **`RBX15.App.ShellLayout`** (static): `PanelWidth = 280`, `NarrowPanelWidth = 200`, `Breakpoint = 900`, `DefaultWidth = 1200`, `DefaultHeight = 800`, `MinWidth = 640`, `MinHeight = 480`, `double PanelWidthFor(double viewportWidth)`.
2. **`RBX15.App.Converters.UpperCaseConverter`**, registered in `Controls.xaml` as the key `UpperCase`. CSS `text-transform: uppercase` has no WPF property. Labels bind their 4.1 source text through this converter, or write the displayed upper-case literal.
3. **Theme keys** (Tokens.xaml):
   - Colors and brushes: `{Name}Color` (Color) and `{Name}Brush` (SolidColorBrush) for every styles.css color token. `Name` is the token's words capitalized: `BrandNavyDeep`, `Ink950`, `Fg2`, `FgMuted`, `Bg0`, `Border1`, `BorderAccent`, `DangerBg` and so on.
   - Gradients: `BrandGradientBrush`, `BrandGradientSoftBrush`.
   - Radii: `RXsRadius`, `RSmRadius`, `RMdRadius`, `RLgRadius`, `RPillRadius` (CornerRadius).
   - Effects: `Shadow2Effect`, `GlowDuoEffect`.
   - Motion: `EaseOutSpline` (KeySpline), `DurFastDuration`, `DurMedDuration`.
   - Fonts: `FontDisplay`, `FontBody`, `FontMono` (FontFamily).
4. **Control style keys** (Controls.xaml):
   - `FocusRing` (FocusVisualStyle).
   - Buttons: `SecondaryButton`, `PrimaryButton`, `SecondaryButtonSm`, `PrimaryButtonSm` (all `TargetType="ButtonBase"`, so ToggleButton can use them).
   - Ribbon: `RibbonTab` (RadioButton); `RibbonGroup` (Border: padding 0 10, 1px right divider; the last group sets `BorderThickness="0"`) and `RibbonGroupLabel` (TextBlock docked bottom, mono 10px 600 fg-muted, text written upper case). A group is a `Border Style="{StaticResource RibbonGroup}"` around a `DockPanel` whose first child is the label. Task 3 drafted these two; they live here once (critic C7).
   - Panel: `PanelHeading` (TextBlock; the first heading sets `Margin="0,0,0,8"`, styles.css:263).
   - Scrollbars: `ThinScrollThumb`, `ThinScrollPage`, and an implicit `ScrollBar` style.
5. **`MainWindow(MainViewModel viewModel, ISettingsStore settings, IReadOnlyList<Rect>? workAreas = null)`.** Its named regions, which later tasks fill without restructuring the skeleton:
   - `RibbonPane` (ContentControl): Task 3 puts `HomeRibbon` inside it.
   - `CanvasArea` (Grid): Task 2 adds `CanvasView` (the canvas host's ScrollViewer and the zoom pill) as its child.
   - `LeftPanelContent` (StackPanel): Task 4 replaces `<!-- T4: left panel -->` with the drop zone and the layers panel.
   - Row 3 of `Viewport` (Auto): Task 4 replaces `<!-- T4: status strip -->` with the status strip. The footer is row 4.
   - The root declares `xmlns:views="clr-namespace:RBX15.App.Views"`.
   - Also named: `PanelColumn`, `Viewport`, `HomeTab`, `Header`, `Footer`, `HeaderTitle`, `HeaderByline`, `FooterText`.
6. **`App.SettingsDirectory`** (`%LOCALAPPDATA%\RBX15`), and **`JsonSettingsStore.FileName`** (`"settings.json"`) and **`FilePath`**. Later milestones add sibling keys to the same file (the store keeps keys it does not own).
7. **`StaThread.Theme`** (`"/RBX15.App;component/Themes/Controls.xaml"`), and StaThread's semantics: one shared STA thread with a plain `Application` holding the theme. View tests in Tasks 2 to 4 build their controls inside `StaThread.Run` and get the app's StaticResources. **`StaThread.Pump()`** runs the dispatcher queue once; view tests call it after setting `DataContext` (ruling 14).
8. **`TestShell.Main(EditorSession)`** (tests) is the only `MainViewModel` construction in the tests. `ShellTests` builds through it at its 4 sites, so later `required` members never break it (CS9035).
9. **`Composition.Build(EditorSession, Func<Window?> owner)`** (App, internal) is the only `MainViewModel` construction in the app. `App.OnStartup` builds `EditorSession`, `JsonSettingsStore`, then `Composition.Build`, then `MainWindow`. Tasks 2 to 4 insert their construction lines above `        // M3a: construction lines above this line.` and their members above `            // M3a: required members above this line.`, in `Composition.cs` and in `TestShell.cs`.
10. **Interface-sheet cites corrected.** `TemplateTitle` is `#exportTitle` at editor.html:**100** (the sheet says 99). 4.1's restore deselects at editor.js:**1451** (History.cs's own comment agrees).

---

### Task 2: Engine view render, the view goldens and the canvas host

**Files:**

- Create:
  - Engine: `src/RBX15.Engine/ViewOptions.cs`.
  - Engine tests: `src/RBX15.Engine.Tests/RendererViewTests.cs`, `src/RBX15.Engine.Tests/GoldenViewTests.cs`.
  - Harness: `tools/parity-ref/view-ref.js`, `tools/parity-ref/view-scenes.json`.
  - Goldens: `tests/goldens/view/` with 14 PNGs, `index.json` and `scenes.json`. The controller captures them once in Step 10; they are frozen from then on.
  - Controls: `src/RBX15.App/Controls/CanvasCoordinates.cs`, `src/RBX15.App/Controls/CanvasDrop.cs`, `src/RBX15.App/Controls/TemplateCanvasHost.cs`.
  - View model and view: `src/RBX15.App/ViewModels/CanvasViewModel.cs`, `src/RBX15.App/Views/CanvasView.xaml`, `src/RBX15.App/Views/CanvasView.xaml.cs`.
  - App tests: `src/RBX15.App.Tests/CanvasCoordinatesTests.cs`, `src/RBX15.App.Tests/CanvasViewModelTests.cs`, `src/RBX15.App.Tests/TemplateCanvasHostTests.cs`, `src/RBX15.App.Tests/CanvasViewTests.cs`.
- Modify:
  - `src/RBX15.Engine/Renderer.cs`: `RenderExport` becomes `RenderComposite(c, d, true)`. Adds `RenderComposite`, `ViewSize`, `CheckerHalf`, `RenderView` and `RenderViewImage`.
  - `src/RBX15.Engine.Tests/GoldenRule.cs`: `AssertImageMatches` factored out. The thresholds are unchanged and `ImageDiff` is untouched.
  - `tools/parity-ref/README.md`: the view renderer, the `view` scene key, and provenance.
  - Task 1's files:
    - `src/RBX15.App/ViewModels/MainViewModel.cs`: `required init Canvas`.
    - `src/RBX15.App/Composition.cs` and `src/RBX15.App.Tests/TestShell.cs`: the `Canvas` construction line and member, at Task 1's anchors.
    - `src/RBX15.App/MainWindow.xaml`: the `CanvasArea` grid's child.
  - Not modified: `src/RBX15.App/app.manifest` and `src/RBX15.App/RBX15.App.csproj`. Task 1 owns the manifest, its `ApplicationManifest` line and `InternalsVisibleTo`; this task pins the manifest with `CanvasViewTests.The_app_is_per_monitor_dpi_v2`.
- Read only: `editor.js`, `editor.html`, `styles.css`, `lib/`, `tools/parity-ref/app-harness.js` (injected unchanged) and `tools/parity-ref/fonts.js`. Also `tests/goldens/app/index.json`, whose provenance the view capture must equal.

**Interfaces:**

- Consumes:
  - Task 1: `EditorSession` (constructor, `Document`, `Apply`, `TemplateTitle`, `Changed`, `Select`), `SessionChange`, `SessionChangedEventArgs`, `MainViewModel`, `StaThread.Run`, `StaThread.Pump`, `Composition.Build` and `TestShell.Main` (their anchors), Themes (`Tokens.xaml` and `Controls.xaml`, keys in Addition 4), `app.manifest`, the `RBX15.App.Tests` project, and the `CanvasArea` grid of `MainWindow.xaml`.
  - Engine (M1 to M2b): `Renderer.RenderExport`/`Export`/`LayerSampling`/`DrawLayer`, `Template.Regions`/`Mask`, `TemplateSize`, `Document`, `Layer`, `Fill`.
  - Test helpers: `ImageDiff.Load`/`FromImage`/`Compare`/`Heatmap`, `RepoRoot.Find`, `SceneLoader.Parse`, `Px.Solid`/`Px.At`.
  - Harness: `app-harness.js` `__pref.render`, `fonts.js` `respond`/`FONTS_DIR`.
- Produces (sheet §3.1 and §5, verbatim):
  - Engine:
    - `public sealed record ViewOptions(double Zoom, double DeviceScale = 1, bool ShowGrid = false)`, guarded on construction and on `with`.
    - `public static void Renderer.RenderComposite(SKCanvas canvas, Document doc, bool applyMask)`.
    - `public static SKSizeI Renderer.ViewSize(ViewOptions options)`, truncated (ruling R1).
    - `public static void Renderer.RenderView(SKCanvas canvas, Document doc, ViewOptions options)`.
    - `public static SKImage Renderer.RenderViewImage(Document doc, ViewOptions options)`.
  - Test helper: `GoldenRule.AssertImageMatches(string name, string goldenPath, SKImage rendered, string heatmapPath)`.
  - Harness and goldens: `tools/parity-ref/view-ref.js`, `tools/parity-ref/view-scenes.json`, and `tests/goldens/view/<scene>.png`, `index.json` and `scenes.json`.
  - App, namespace `RBX15.App.Controls`:
    - `public static class CanvasCoordinates { ToTemplate(Point inFrame, double zoom); ToFrame(Point template, double zoom); }`.
    - `public sealed record CanvasDrop(IReadOnlyList<string> Paths, Point TemplatePoint)`.
    - `public sealed class TemplateCanvasHost : FrameworkElement` with `DocumentProperty`/`Document`, `ZoomProperty`/`Zoom`, `DropCommandProperty`/`DropCommand` and `ClickCommandProperty`/`ClickCommand` (addition, critic C13: a left click without a drag runs it with the template point).
  - App, namespace `RBX15.App.ViewModels`:
    - `public sealed partial class CanvasViewModel(EditorSession session)` with `Document Document`, `double Zoom` (1), `string ZoomLabel`, `MinZoom` 0.25, `MaxZoom` 3, `ZoomStep` 0.25, `ZoomInCommand`, `ZoomOutCommand`, `IRelayCommand<CanvasDrop> DropCommand` and `Action<CanvasDrop>? DropHandler`, plus (addition, C13) `IRelayCommand<Point> ClickCommand` and `Action<Point>? ClickHandler` (Task 4 sets it to select the topmost layer under the point, or none).
    - `MainViewModel.Canvas` (`required init`).
  - Additions (listed at the end): `ViewOptions.Scale`, `Renderer.CheckerHalf` (internal), and the host's internal test seams `Frame`, `DeviceScale`, `ApplyDpi(double)`, `HandleDrop(IDataObject, Point)` and `HandleClick(Point)`. Also `Views/CanvasView.xaml` and `CanvasViewTests`.

**What 4.1 does (cited).** All of it is `editor.js` at blob `2790c41e418cd5bfea82cf1ae4d71626a7b54e61` (the blob `tests/goldens/app/index.json` records), `editor.html` at `401a5d5f33c4accbb452cb080c9c544a0f79a6ce`, and `styles.css` on `main`.

- **The composite.** render() builds the composite on a persistent 585x559 offscreen canvas (`editor.js:296-298`). The steps are:
  1. `clearRect`;
  2. the background: a stretched image or the color, times `bgOpacity` (`:303-313`);
  3. `paintRegions` (`:316`);
  4. visible layers through `drawLayerWithAdjustments` (`:319-322`);
  5. the live drawing preview (`:326-330`; M3b);
  6. the mask with `destination-in` only `if (showMask)` (`:332-337`).

  The export repeats the same steps without the preview, and always masks (`:1512-1534`).
- **The visible canvas.**
  - Size: `canvas.width = TW * zoom; canvas.height = TH * zoom` (`:340-341`). The browser truncates a fractional size: 146.25 becomes 146 (ledger `render-zoom-canvas`: "zoom 1.5 -> 877x838").
  - Smoothing: `ctx.imageSmoothingEnabled = zoom < 1` (`:342`). Then `clearRect` (`:343`).
  - Checkerboard: a `createPattern(_checkerboard, 'repeat')` fill of the whole canvas (`:345-348`). The tile is 16x16, base `#0f1f31`, with `#050c18` in the top-left and bottom-right 8x8 quarters (`:431-438`). The pattern is anchored at the canvas origin and never scales with zoom (`render-checkerboard`).
  - The composite: `ctx.drawImage(off, 0, 0, canvas.width, canvas.height)` (`:350`). It is stretched to the truncated size, not to `585*zoom`.
  - The grid, when `showGrid`: `strokeStyle = 'rgba(255,255,0,0.3)'`, `lineWidth = 1`, and `strokeRect(r.x*zoom, r.y*zoom, r.w*zoom, r.h*zoom)` for every region of the active table (`:352-359`). The table is named `SHIRT_REGIONS`, but `switchMode` points it at the Pants table (`:1700-1702`), whose rects have Shirt's geometry (`overlay-grid-checkbox`).
  - The selection overlay is drawn only in the Select tool with a selection (`:362-421`; M3b).
- **Zoom.**
  - `let zoom = 1` (`:81`).
  - `zoomIn: zoom = Math.min(3, zoom + 0.25)` and `zoomOut: Math.max(0.25, zoom - 0.25)`. Each sets `#zoomLevel` to `(zoom*100)+'%'` and calls render() (`:1490-1491`).
  - The pill (`editor.html:332-336`) is `<button id="zoomOut">-</button><span id="zoomLevel">100%</span><button id="zoomIn">+</button>`, styled by `styles.css:537-564`:
    - the pill: bottom 12 and right 12, background `rgba(15,31,49,.85)`, `blur(8px)`, padding 4px 8px, `--r-pill`, a 1px `--border-1` border, gap 8px;
    - the buttons: no background or border, `--fg-1`, 16px, padding 2px 6px;
    - the label: `--font-mono` 11px `--fg-3`, at least 36px wide, centered.
  - Zoom is not in undo or project files and resets on launch (`zoom-controls`).
- **The canvas area** (`styles.css:441-457`):
  - `.canvas-area` is `--ink-950`, flex-centered, with `overflow: hidden`. At zoom over the fit, the canvas is clipped and cannot be panned (`canvas-zoom-clipped-no-pan`).
  - `.canvas-wrap` has a 1px `--border-1` border and `--shadow-2` (`0 4px 16px rgba(0,0,0,.45)`, `styles.css:77`).
- **The mask and grid checkboxes** set `showMask` and `showGrid` and re-render (`:1494-1495`).
- **Coordinates and drop.**
  - Every canvas handler maps `(clientX - rect.left) / zoom` and `(clientY - rect.top) / zoom`, unclamped (`:1058-1061`, `:1149-1152`; `canvas-coord-map`).
  - A drop on the canvas computes the same point (`:966-967`) and hands every file to `handleFiles(e.dataTransfer.files, dropX, dropY)`, so all of them land on that point (`:984`; `drop-canvas`).
  - Drops elsewhere are swallowed by the document (`:989-990`; `shell-external-drop-guard`).
- **High DPI.** 4.1 draws in CSS px and lets Chromium stretch the bitmap by the device pixel ratio (`cross-window-dpi-rendering`).

**Ledger rows this task serves** (Task 5 closes them; the evidence per row is under "Handed to Task 5"):

- Engine render: `canvas-element-render` (owner Engine.Render, check golden), `render-zoom-canvas`, `render-checkerboard`, `render-grid-overlay`, the view half of `overlay-grid-checkbox` and of `overlay-mask-checkbox`.
- Pill and scrolling: `zoom-controls`, `canvas-zoom-controls`, `canvas-zoom-clipped-no-pan`.
- Input: `canvas-coord-map` (the mapping half; the left-button and capture half is M3b) and `drop-canvas` (the host half; placement is Task 4).
- Shell: `cross-window-dpi-rendering`, `render-pipeline-canvas-reset`, and the canvas part of `shell-external-drop-guard`.

**Rulings on the open questions** (each pinned by a named test):

1. **R1: the frame size truncates, as 4.1 does** (interfaces §9 item 2; this overrides the contract's and sheet's `ceil`, so the plan header lists it as contract amendment CA1 for Este's OK). `ViewSize = (trunc(585*Zoom*DeviceScale), trunc(559*Zoom*DeviceScale))`, and the composite is stretched to the whole frame, as `editor.js:350` stretches it.
   - **Why:** with `ceil`, the frame is one column and row larger at 25%, 50% and 150%, and the stretched composite lands up to a full source pixel away at the right edge on downscales. The 25% and 50% goldens then fail on the edges, and a cropped comparison does not fix the bilinear shift. Truncation passes all 14 under the unchanged rule, with no special case.
   - The visible cost is under one device pixel at the right and bottom edge, as in 4.1.
   - `CanvasCoordinates` keeps 4.1's `/zoom`, which ignores the truncation too (the ledger's cross-cutting note: "mouse-to-template coordinate mapping ... must agree with the truncation behavior").
   - The ledger's `render-zoom-canvas` divergence text says "exact scaled size", which Task 5 rewords (see "Handed to Task 5").
   - Pinned: `View_size_is_the_template_times_zoom_and_device_scale_truncated`, and every golden's size check in `The_frozen_set_covers_the_contracted_views_and_matches_its_index`.
2. **R2: the device scale.** The frame renders at device resolution with `scale = Zoom * DeviceScale`:
   - the checker half-tile is `max(1, round(8 * DeviceScale))` device px (8 CSS px kept crisp: 10 at 125%, 12 at 150%);
   - the grid stroke is `DeviceScale` px wide (one CSS px) at `rect * scale`, unsnapped, as 4.1 leaves it;
   - smoothing is on only when `scale < 1` (contract; spec §4).

   At `DeviceScale == 1` this is exactly 4.1, which the view goldens pin. Pinned: `Checker_half_is_8_px_times_the_device_scale_rounded`, `Checker_squares_follow_the_device_scale`, `Grid_lines_are_device_scale_pixels_wide`, `Device_scale_and_zoom_multiply`.
3. **R3: the two 300% goldens use flat content.** Measured on the first capture: the gradient fills and the rotated Word Art layer differ from 4.1 by 1 or 2 levels on M1's tolerated edge pixels (a gradient has no flat 3x3 neighborhoods, so those pixels are edges). Nearest-neighbor 3x magnification turns each of them into a flat 3x3 block, which `ImageDiff` counts as interior.
   - `view-shirt-z300` failed with 15094 interior differences (max delta 2), and `view-pants-z300-grid-nomask` with 16916 (max delta 1). The engine's magnification itself was exact.
   - So the 300% scenes swap the linear fills for solids and drop the rotation (`view-shirt-flat-z300`, `view-pants-flat-z300-grid-nomask`); the other 12 scenes keep the gradients and the rotation.
   - Exact nearest magnification of edge content is pinned by a unit test instead: `At_300_percent_every_pixel_is_an_exact_3x3_copy_of_100_percent`, on a mask-off, opaque composite with a gradient and a 30° layer.
4. **R4: the zoom buttons are disabled at their limits** (new divergence `zoom-pill-limits`, Este's "never show a dead button"). 4.1's clamped click is a silent no-op (`zoom-controls` quirks: "no disabled state at limits"). `Execute` past a limit still clamps. Disabled uses `.btn:disabled`'s opacity .4 (`styles.css:414-416`). Pinned: `Each_button_is_disabled_at_its_limit_and_comes_back`.
5. **R5: a canvas click, without the drag** (interfaces §9 item 3; critic C13, adopted by the reconciler). Every import selects the new layer (editor.js:521), and while a layer is selected a region click fits that layer instead of painting (editor.js:1655-1666). 4.1's only way back to "nothing selected" is a click on empty canvas in the Select tool (editor.js:1100-1116). So the host gets a `ClickCommand` dependency property: a left-button press and release within the system drag distance runs it with `CanvasCoordinates.ToTemplate(e.GetPosition(this), Zoom)`. `CanvasViewModel.ClickHandler` receives the point, and Task 4 sets it to `p => session.Select(LayerOps.HitTest(session.Document, p.X, p.Y))`: the topmost visible layer under the point, else none (editor.js:1100-1116). The drag half (move, resize, rotate) stays M3b. Pinned: `TemplateCanvasHostTests.A_click_runs_the_click_command_with_the_template_point_and_a_drag_does_not`, `CanvasViewModelTests.Click_runs_the_handler_and_is_disabled_without_one`, and Task 4's `ShellWiringTests`.
6. **R6: view-ref.js gets no node test file.** The capture tool has no node tests, because `wordart-ref.js` (its model) has none. Instead:
   - view-ref.js validates every scene: the zoom is a 4.1 step, `mask` and `grid` are booleans, no layer has an RGB shift, the canvas size equals the truncated size, and `devicePixelRatio` is 1;
   - the controller's freeze script checks two runs byte for byte, plus the provenance and the scene order.
7. **R7: the view capture reuses the frozen app harness unchanged.**
   - view-ref.js injects `tools/parity-ref/app-harness.js` as is and calls `__pref.render(scene, assets)`, which builds the base and actions exactly as the app goldens did; its export capture is discarded.
   - Then it drives the real `#zoomIn`/`#zoomOut` buttons and the `#showMask`/`#showGrid` checkboxes, sets `selectedLayerId = null` (no overlay), calls render(), and reads `#mainCanvas.toDataURL()`.
   - `force-device-scale-factor=1` keeps the window at DPR 1, which the canvas size does not depend on but the tool asserts anyway.
8. **R8: theme keys and the test seams.**
   - `CanvasView` reads Task 1's tokens through `DynamicResource`, so a missing key cannot crash a view test. `Every_theme_key_the_view_uses_is_defined_in_the_theme` reads the files and fails on any missing key; the focus ring key is Task 1's `FocusRing`. Task 5 widens the same guard to every view (`ThemeKeyGuardTests`).
   - DPI, drop and click are testable through internal seams (`ApplyDpi`, `HandleDrop`, `HandleClick`), because DPI depends on the monitor and `DragEventArgs` and `MouseButtonEventArgs` cannot be raised meaningfully without a window.
9. **R9: Task 1 owns `app.manifest`** (PerMonitorV2, critic C1). Without it WPF never raises `OnDpiChanged`, so this task pins it: `The_app_is_per_monitor_dpi_v2` passes against Task 1's file.

**Measured while drafting.** The setup: a scratch copy of `main` at `1126e7c`, .NET SDK 10.0.302, SkiaSharp 4.153.1, Electron 41.1.0, CommunityToolkit.Mvvm 8.4.2 from the local cache, and minimal stand-ins for Task 1's `EditorSession`, `MainViewModel`, `StaThread`, `Tokens.xaml` and `MainWindow.xaml` center panel, shaped exactly as the interface sheet and Additions 4 to 6 describe them.

- **Build.** Every file below compiled under `-warnaserror`.
- **Capture.** `view-ref.js` ran twice with byte-identical output. The 14-scene digest is `e69fad047547ea4e6f5311362b3cfa10501d3d1612deb80cc6a33157a904264a`.
- **The golden gate.** `GoldenViewTests` passed all 14 under the unchanged rule:

  | Scene | Max delta | Differing pixels | Interior |
  | --- | --- | --- | --- |
  | `view-shirt-z25` | 2 | 1121 | 0 |
  | `view-shirt-z50` | 2 | 2598 | 0 |
  | `view-shirt-z100` | 2 | 15070 | 0 |
  | `view-shirt-z150` | 2 | 29672 | 0 |
  | `view-shirt-z25-nomask` | 2 | 1117 | 0 |
  | `view-shirt-z100-nomask` | 2 | 15070 | 0 |
  | `view-shirt-z150-nomask` | 2 | 29672 | 0 |
  | `view-shirt-z50-grid` | 2 | 2585 | 0 |
  | `view-shirt-z100-grid` | 2 | 15034 | 0 |
  | `view-shirt-z150-grid` | 2 | 29613 | 0 |
  | `view-pants-z100-grid` | 1 | 17338 | 0 |
  | `view-shirt-flat-z300` | 0 | 0 | 0 |
  | `view-pants-flat-z300-grid-nomask` | 0 | 0 | 0 |
  | `view-empty-z100` | 0 | 0 | 0 |

  Every differing pixel at 100% sits in a linear-gradient region (`torso_front`/`torso_back`, `waist_front`/`waist_back`), which are M1's tolerated edge pixels.
- **The suites.** The full suite passed twice in a row: Engine.Tests 621 (566 + 55), Parity.Tests 35, and App.Tests 46 for this task's files alone.
- **A live run.** The app ran with the stand-in window. It showed the empty template at 100%, centered, with the zoom pill at the bottom right.
- **Reconciler changes since that run** (not compiled in the measured run; the implementer's build at each step is their check): the click seam (ruling R5: `ClickCommand`, `HandleClick`, `ClickHandler`, and their 2 tests), `StaThread.Pump` in place of the local pump, the `FocusRing` key, the `CanvasArea` anchor, the `Composition`/`TestShell` lines, and the manifest staying Task 1's. The 14 goldens, the digest and every measured count above are unchanged by them.

**Test deltas:** Engine.Tests **+55** (RendererViewTests 40, GoldenViewTests 15), App.Tests **+48** (CanvasCoordinatesTests 17, CanvasViewModelTests 8, TemplateCanvasHostTests 18, CanvasViewTests 5), Parity.Tests **+0**. Cumulative: Task 1 leaves Engine.Tests 582 and App.Tests 132; this task ends at Engine.Tests 637 and App.Tests 180.

- [ ] **Step 1: Baseline**

Task 2 starts after Task 1 has committed. Confirm the branch and the starting counts.

Run: `git branch --show-current && dotnet build src/RBX15.slnx -c Release -warnaserror && dotnet test src/RBX15.slnx -c Release`
Expected: `feat/v5-m3-shell`, `Build succeeded.` with `0 Warning(s)`, then Engine.Tests `Passed: 582, Failed: 0, Skipped: 0`, Parity.Tests `Passed: 35, Failed: 0`, and App.Tests `Passed: 132, Failed: 0`.

- [ ] **Step 2: Write the failing engine view tests**

Create `src/RBX15.Engine.Tests/RendererViewTests.cs`:

```csharp
using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Renderer.RenderView, ViewSize and RenderComposite (render(), editor.js:300-359). The view goldens
/// (GoldenViewTests) hold device scale 1 to 4.1's pixels; these facts pin the rules the goldens cannot
/// show: the device scale, exact nearest magnification of edge content, and the guards.
/// </summary>
public class RendererViewTests
{
    static readonly SKColor CheckerBase = new(0x0f, 0x1f, 0x31);
    static readonly SKColor CheckerSquare = new(0x05, 0x0c, 0x18);

    static SKBitmap View(Document doc, ViewOptions options)
    {
        using var image = Renderer.RenderViewImage(doc, options);
        return ImageDiff.FromImage(image);
    }

    /// <summary>A composite that is opaque everywhere (mask off, opaque background) with gradient and rotated edges.</summary>
    static Document OpaqueEdgy() => new()
    {
        ShowMask = false,
        RegionFills = ImmutableList.Create(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, null))),
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(60, 24, new SKColor(242, 47, 137)), X = 250, Y = 100, Rotation = 30 }),
    };

    [Theory] // ViewOptions guards Zoom and DeviceScale on construction and on with
    [InlineData(0, 1)]
    [InlineData(-0.25, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, 0)]
    [InlineData(1, double.NaN)]
    public void View_options_refuse_a_zoom_or_scale_that_is_not_finite_and_positive(double zoom, double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewOptions(zoom, scale));
        var ok = new ViewOptions(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ok with { Zoom = zoom, DeviceScale = scale });
    }

    [Fact]
    public void View_options_default_to_device_scale_1_without_grid()
    {
        var o = new ViewOptions(1.5);
        Assert.Equal((1.5, 1.0, false, 1.5), (o.Zoom, o.DeviceScale, o.ShowGrid, o.Scale));
        Assert.Equal(new ViewOptions(1.5, 1, false), o);
    }

    [Theory] // canvas.width = TW * zoom truncates (editor.js:340-341; render-zoom-canvas: 1.5 -> 877x838)
    [InlineData(0.25, 1, 146, 139)]
    [InlineData(0.5, 1, 292, 279)]
    [InlineData(0.75, 1, 438, 419)]
    [InlineData(1, 1, 585, 559)]
    [InlineData(1.5, 1, 877, 838)]
    [InlineData(3, 1, 1755, 1677)]
    [InlineData(1, 1.25, 731, 698)]
    [InlineData(1, 1.5, 877, 838)]
    [InlineData(0.25, 1.75, 255, 244)]
    [InlineData(3, 2, 3510, 3354)]
    public void View_size_is_the_template_times_zoom_and_device_scale_truncated(double zoom, double scale, int w, int h) =>
        Assert.Equal(new SKSizeI(w, h), Renderer.ViewSize(new ViewOptions(zoom, scale)));

    [Fact]
    public void A_view_too_small_to_hold_a_pixel_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer.ViewSize(new ViewOptions(0.001)));

    [Theory] // the 8 px checker half (editor.js:432-437) at device resolution, whole pixels
    [InlineData(1, 8)]
    [InlineData(1.25, 10)]
    [InlineData(1.5, 12)]
    [InlineData(1.75, 14)]
    [InlineData(2, 16)]
    [InlineData(0.5, 4)]
    [InlineData(1.0625, 9)]
    [InlineData(0.01, 1)]
    public void Checker_half_is_8_px_times_the_device_scale_rounded(double scale, int half) =>
        Assert.Equal(half, Renderer.CheckerHalf(scale));

    [Theory] // render-checkerboard parityCheck: corners (0,0),(8,0),(8,8),(0,8); 16 px tile at every zoom
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(3)]
    public void Checker_is_anchored_at_the_origin_and_never_scales_with_zoom(double zoom)
    {
        using var v = View(new Document(), new ViewOptions(zoom)); // (0,0)..(16,16) is off every region at every zoom; mask on
        Assert.Equal(CheckerSquare, v.GetPixel(0, 0));
        Assert.Equal(CheckerBase, v.GetPixel(8, 0));
        Assert.Equal(CheckerSquare, v.GetPixel(8, 8));
        Assert.Equal(CheckerBase, v.GetPixel(0, 8));
        Assert.Equal(CheckerSquare, v.GetPixel(15, 15));
        Assert.Equal(CheckerSquare, v.GetPixel(16, 0));
    }

    [Fact] // DeviceScale 1.5: 12 px squares, so a 150% monitor shows 4.1's 8 CSS px squares crisply
    public void Checker_squares_follow_the_device_scale()
    {
        using var v = View(new Document(), new ViewOptions(1, 1.5));
        Assert.Equal(CheckerSquare, v.GetPixel(11, 11));
        Assert.Equal(CheckerBase, v.GetPixel(12, 0));
        Assert.Equal(CheckerSquare, v.GetPixel(12, 12));
        Assert.Equal(CheckerSquare, v.GetPixel(24, 0));
    }

    [Theory] // 4.1's visible canvas is opaque: the checker fills it before the composite (editor.js:344-350)
    [InlineData(0.25, true)]
    [InlineData(1, false)]
    public void Every_frame_pixel_is_opaque(double zoom, bool mask)
    {
        using var v = View(new Document { BgOpacity = 0.3, ShowMask = mask }, new ViewOptions(zoom));
        for (int y = 0; y < v.Height; y++)
            for (int x = 0; x < v.Width; x++)
                Assert.Equal(255, v.GetPixel(x, y).Alpha);
    }

    [Fact] // overlay-mask-checkbox: the view masks iff Document.ShowMask; export always masks
    public void The_view_masks_by_the_document_flag_and_export_always_masks()
    {
        var red = new Document { BgColor = "#ff0000" };
        using (var on = View(red, new ViewOptions(1))) Assert.Equal(CheckerSquare, on.GetPixel(2, 2));
        using (var off = View(red with { ShowMask = false }, new ViewOptions(1))) Assert.Equal(SKColors.Red, off.GetPixel(2, 2));
        Assert.Equal(0, Px.At(Renderer.Export(red with { ShowMask = false }), 2, 2).Alpha);
    }

    [Fact] // RenderExport is RenderComposite with the mask, byte for byte
    public void Export_is_the_masked_composite()
    {
        var doc = OpaqueEdgy() with { ShowMask = false };
        using var export = ImageDiff.FromImage(Renderer.Export(doc));
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Renderer.RenderComposite(surface.Canvas, doc, applyMask: true);
        using var masked = ImageDiff.FromImage(surface.Snapshot());
        Assert.Equal(export.Bytes, masked.Bytes);

        Renderer.RenderComposite(surface.Canvas, doc, applyMask: false);
        using var unmasked = ImageDiff.FromImage(surface.Snapshot());
        Assert.Equal(255, unmasked.GetPixel(2, 2).Alpha);
        Assert.Equal(0, masked.GetPixel(2, 2).Alpha);
    }

    [Fact] // spec §4 nearest at 100% and above: 300% is an exact 3x copy of 100% (the goldens at 300% use flat content)
    public void At_300_percent_every_pixel_is_an_exact_3x3_copy_of_100_percent()
    {
        var doc = OpaqueEdgy();
        using var one = View(doc, new ViewOptions(1));
        using var three = View(doc, new ViewOptions(3));
        for (int y = 0; y < three.Height; y++)
            for (int x = 0; x < three.Width; x++)
                Assert.Equal(one.GetPixel(x / 3, y / 3), three.GetPixel(x, y));
    }

    [Fact] // the device scale and the zoom multiply: zoom 1.5 at 200% DPI is the 300% frame (checker hidden)
    public void Device_scale_and_zoom_multiply()
    {
        var doc = OpaqueEdgy();
        using var a = View(doc, new ViewOptions(1.5, 2));
        using var b = View(doc, new ViewOptions(3));
        Assert.Equal(b.Bytes, a.Bytes);
    }

    [Fact] // editor.js:342 imageSmoothingEnabled = zoom < 1: below 100% a hard edge blends
    public void Below_100_percent_the_composite_is_smoothed()
    {
        var doc = new Document
        {
            BgColor = "#000000",
            ShowMask = false,
            Layers = ImmutableList.Create(new Layer { Id = 1, Image = Px.Solid(101, 559, SKColors.White) }),
        };
        using var half = View(doc, new ViewOptions(0.5));
        var edge = half.GetPixel(50, 100);
        Assert.InRange(edge.Red, 1, 254);
        Assert.Equal((edge.Red, edge.Red), (edge.Green, edge.Blue));
        Assert.Equal(SKColors.White, half.GetPixel(40, 100));
        Assert.Equal(SKColors.Black, half.GetPixel(60, 100));
    }

    [Fact] // overlay-grid-checkbox: Pants strokes its own regions, which share Shirt's geometry
    public void The_grid_is_the_same_in_shirt_and_pants_and_absent_without_ShowGrid()
    {
        using var shirt = View(new Document(), new ViewOptions(1, 1, true));
        using var pants = View(new Document { Mode = TemplateMode.Pants }, new ViewOptions(1, 1, true));
        using var none = View(new Document(), new ViewOptions(1));
        Assert.Equal(shirt.Bytes, pants.Bytes);
        Assert.NotEqual(none.Bytes, shirt.Bytes);
        Assert.Equal(none.GetPixel(300, 40), shirt.GetPixel(300, 40)); // inside torso_up, off every outline
        Assert.NotEqual(none.GetPixel(231, 40), shirt.GetPixel(231, 40)); // torso_up's left edge, x = 231
    }

    [Fact] // the grid line is 1 CSS px: DeviceScale device pixels wide, centered on the scaled edge
    public void Grid_lines_are_device_scale_pixels_wide()
    {
        var doc = new Document { ShowMask = false, BgColor = "#000000" };
        using var plain = View(doc, new ViewOptions(1, 2));
        using var grid = View(doc, new ViewOptions(1, 2, true));
        // torso_up's left edge is x = 231, so 462 at scale 2; a 2 px line covers 461 and 462 fully.
        Assert.Equal(plain.GetPixel(460, 80), grid.GetPixel(460, 80));
        Assert.NotEqual(plain.GetPixel(461, 80), grid.GetPixel(461, 80));
        Assert.Equal(grid.GetPixel(461, 80), grid.GetPixel(462, 80));
        Assert.Equal(plain.GetPixel(463, 80), grid.GetPixel(463, 80));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        using var surface = SKSurface.Create(new SKImageInfo(585, 559, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        Assert.Throws<ArgumentNullException>(() => Renderer.RenderView(surface.Canvas, null!, new ViewOptions(1)));
        Assert.Throws<ArgumentNullException>(() => Renderer.RenderView(surface.Canvas, new Document(), null!));
        Assert.Throws<ArgumentNullException>(() => Renderer.RenderComposite(null!, new Document(), true));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'ViewOptions' could not be found` in `RendererViewTests.cs`. `CS0117` errors for `Renderer.RenderViewImage`, `ViewSize`, `RenderComposite`, `RenderView` and `CheckerHalf` may appear alongside.

- [ ] **Step 3: Implement ViewOptions and the view render**

Create `src/RBX15.Engine/ViewOptions.cs`:

```csharp
namespace RBX15.Engine;

/// <summary>
/// How the editor view shows a document (render(), editor.js:339-359).
/// <list type="bullet">
/// <item><c>Zoom</c>: 4.1's zoom (editor.js:81; the app steps it 0.25 to 3, zoom-controls). Any finite positive value here.</item>
/// <item><c>DeviceScale</c>: the monitor's device pixels per DIP (1 at 96 DPI, 1.5 at 144 DPI). The view renders at
///   device resolution, so it is crisp at per-monitor DPI v2 (cross-window-dpi-rendering). 4.1 drew in CSS px and let
///   Chromium stretch the bitmap.</item>
/// <item><c>ShowGrid</c>: the region outlines (editor.js:352-359). The app passes <c>Document.ShowGrid</c>.</item>
/// </list>
/// At <c>DeviceScale == 1</c> the frame is exactly 4.1's visible canvas; the view goldens pin that.
/// Both numbers are checked on construction and on <c>with</c>.
/// </summary>
public sealed record ViewOptions(double Zoom, double DeviceScale = 1, bool ShowGrid = false)
{
    readonly double _zoom = Positive(Zoom, nameof(Zoom));
    readonly double _deviceScale = Positive(DeviceScale, nameof(DeviceScale));

    public double Zoom { get => _zoom; init => _zoom = Positive(value, nameof(Zoom)); }
    public double DeviceScale { get => _deviceScale; init => _deviceScale = Positive(value, nameof(DeviceScale)); }

    /// <summary>Device pixels per template pixel: <c>Zoom * DeviceScale</c>.</summary>
    public double Scale => Zoom * DeviceScale;

    static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
}
```

In `src/RBX15.Engine/Renderer.cs`, replace the whole `RenderExport` method. Its current text is:

```csharp
    public static void RenderExport(SKCanvas canvas, Document doc)
    {
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, doc);
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }
```

Its doc comment above stays as it is. Replace the method with:

```csharp
    public static void RenderExport(SKCanvas canvas, Document doc) => RenderComposite(canvas, doc, applyMask: true);

    /// <summary>
    /// The offscreen half of render() (editor.js:300-337) without the live drawing preview (M3b):
    /// clear, background, region fills, visible layers bottom to top, then the mask with
    /// destination-in only when <paramref name="applyMask"/>. Export always masks
    /// (<see cref="RenderExport"/>); the view masks when <c>Document.ShowMask</c> (editor.js:332-337).
    /// Same precondition as <see cref="RenderExport"/>: a kN32 premultiplied canvas in template space.
    /// </summary>
    public static void RenderComposite(SKCanvas canvas, Document doc, bool applyMask)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(doc);
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, doc);
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        if (!applyMask) return;
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

    /// <summary>
    /// The view frame's size in device pixels. 4.1 assigns <c>canvas.width = TW * zoom</c>
    /// (editor.js:340-341), and the browser truncates a fractional width, so zoom 1.5 gives
    /// 877x838 and zoom 0.25 gives 146x139 (render-zoom-canvas). The frame keeps that truncation,
    /// with the device scale folded in, because the view goldens freeze 4.1's sizes and the
    /// composite is stretched to exactly this frame (editor.js:350).
    /// </summary>
    public static SKSizeI ViewSize(ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        double scale = options.Scale;
        int w = (int)Math.Truncate(TemplateSize.Width * scale), h = (int)Math.Truncate(TemplateSize.Height * scale);
        if (w < 1 || h < 1) throw new ArgumentOutOfRangeException(nameof(options), scale, "the view frame would be empty");
        return new SKSizeI(w, h);
    }

    static readonly SKColor CheckerBase = new(0x0f, 0x1f, 0x31);  // editor.js:435
    static readonly SKColor CheckerSquare = new(0x05, 0x0c, 0x18); // editor.js:436
    static readonly SKSamplingOptions NearestSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
    /// Half the checkerboard tile in device pixels: 8 CSS px (editor.js:432-437) times the device
    /// scale, rounded to a whole pixel and at least 1, so the squares stay crisp at any DPI.
    /// </summary>
    internal static int CheckerHalf(double deviceScale) => Math.Max(1, (int)Math.Round(8 * deviceScale, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The view (render(), editor.js:300-359) minus the live drawing preview and the selection overlay
    /// (both M3b). The composite renders at 585x559 identity (the Renderer's precondition), masked
    /// iff <c>doc.ShowMask</c>. The frame then gets:
    /// <list type="number">
    /// <item>the checkerboard (editor.js:345-348, :431-438): a tile of 2 x <see cref="CheckerHalf"/> device pixels anchored
    ///   at the frame origin, base #0f1f31 with #050c18 in the top-left and bottom-right quarters. It does not scale with zoom
    ///   (render-checkerboard);</item>
    /// <item>the composite stretched to the whole frame (editor.js:350), bilinear only when <c>Zoom * DeviceScale &lt; 1</c>
    ///   (editor.js:342; spec §4, nearest at 100% and above);</item>
    /// <item>when <c>options.ShowGrid</c>, every region of the document's mode stroked with rgba(255,255,0,0.3) at
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
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the checker tile");
        surface.Canvas.Clear(CheckerBase);
        using var square = new SKPaint { Color = CheckerSquare };
        surface.Canvas.DrawRect(SKRect.Create(0, 0, half, half), square);
        surface.Canvas.DrawRect(SKRect.Create(half, half, half, half), square);
        return surface.Snapshot();
    }

    /// <summary><see cref="RenderView"/> into a new kN32 premultiplied image of <see cref="ViewSize"/>. For tests and goldens; the app renders into its own bitmap.</summary>
    public static SKImage RenderViewImage(Document doc, ViewOptions options)
    {
        var size = ViewSize(options);
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create the view surface");
        RenderView(surface.Canvas, doc, options);
        return surface.Snapshot();
    }
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.RendererViewTests"`
Expected: `Passed!  - Failed:     0, Passed:    40, Skipped:     0, Total:    40` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 622, Failed: 0, Skipped: 0`, Parity.Tests `35`, App.Tests `132`. The 34 render, 88 app and 23 swap golden cases are unchanged, because `RenderExport` is now `RenderComposite(c, d, true)`, which runs the same calls in the same order.

- [ ] **Step 4: Commit the engine view**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/ViewOptions.cs src/RBX15.Engine/Renderer.cs src/RBX15.Engine.Tests/RendererViewTests.cs
git commit -m "feat(engine): RenderView, 4.1's visible canvas at any zoom and device scale"
```

- [ ] **Step 5: Add the view harness (tool only; no capture yet)**

Create `tools/parity-ref/view-scenes.json`. There are 14 scenes, one per line:

- `shirt`: background at 0.6 opacity, so the checker shows through inside the regions. It has linear, solid, dots and checker fills, plus three layers: one crossing the mask gaps at 0.6 scale, a 0.5-scale Word Art PNG at -8°, and a drip at 0.8 opacity running off the top regions.
- `shirt-flat`: `shirt` with solid torso fills and no rotation (ruling R3).
- `pants`: the frozen `pants-fills` fills plus a pants drip at 0.8 scale.
- `pants-flat`: `pants` with solid waist fills (ruling R3).
- `empty`: a fresh document.

```json
[
  {"name": "view-shirt-z25", "view": {"zoom": 0.25, "mask": true, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z50", "view": {"zoom": 0.5, "mask": true, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z100", "view": {"zoom": 1, "mask": true, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z150", "view": {"zoom": 1.5, "mask": true, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-flat-z300", "view": {"zoom": 3, "mask": true, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": "#17d4fa", "torso_back": "#f22f89", "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z25-nomask", "view": {"zoom": 0.25, "mask": false, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z100-nomask", "view": {"zoom": 1, "mask": false, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z150-nomask", "view": {"zoom": 1.5, "mask": false, "grid": false}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z50-grid", "view": {"zoom": 0.5, "mask": true, "grid": true}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z100-grid", "view": {"zoom": 1, "mask": true, "grid": true}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-shirt-z150-grid", "view": {"zoom": 1.5, "mask": true, "grid": true}, "bgOpacity": 0.6, "regionColors": {"torso_front": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_back": {"type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45}, "torso_up": "#5ce6ff", "rarm_front": {"type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12}, "larm_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}}, "layers": [{"image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6}, {"image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8}, {"image": "packs/drips/drip_cyan_front_v1.png", "x": 380, "y": 10, "opacity": 0.8}]},
  {"name": "view-pants-z100-grid", "view": {"zoom": 1, "mask": true, "grid": true}, "mode": "Pants", "regionColors": {"waist_front": {"type": "linear", "c1": "#222222", "c2": "#17d4fa", "angle": 90}, "waist_back": {"type": "linear", "c1": "#222222", "c2": "#17d4fa", "angle": 90}, "rleg_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}, "lleg_front": "#f22f89"}, "layers": [{"image": "packs/drips/drip_blue_pants_wide_v1.png", "x": 150, "y": 250, "scale": 0.8}]},
  {"name": "view-pants-flat-z300-grid-nomask", "view": {"zoom": 3, "mask": false, "grid": true}, "mode": "Pants", "regionColors": {"waist_front": "#222222", "waist_back": "#17d4fa", "rleg_front": {"type": "checker", "c1": "#000", "c2": "#fff", "scale": 8}, "lleg_front": "#f22f89"}, "layers": [{"image": "packs/drips/drip_blue_pants_wide_v1.png", "x": 150, "y": 250, "scale": 0.8}]},
  {"name": "view-empty-z100", "view": {"zoom": 1, "mask": true, "grid": false}}
]
```

Create `tools/parity-ref/view-ref.js`:

```js
// Electron main. Renders the 4.1 VIEW canvas (the visible #mainCanvas after render(), editor.js:300-429)
// for each view scene, so the engine's Renderer.RenderView is held to what 4.1 put on screen: the
// zoomed composite, the 16px checkerboard, the mask toggle and the grid overlay. M3a Task 2 froze its
// output once in tests/goldens/view; never re-run it into that folder.
//
// Each scene is an app scene (tools/parity-ref/README.md, "Scene format") plus a "view" object:
//   { zoom: 0.25..3 in 0.25 steps, mask: boolean (default true), grid: boolean (default false) }.
// The base and its actions run through the frozen app-harness.js (__pref.render, whose export
// capture is discarded), then the real controls are driven: #zoomIn/#zoomOut (editor.js:1490-1491),
// #showMask and #showGrid (editor.js:1494-1495). The selection is cleared before the final render(),
// because the selection overlay is M3b's. GPU raster is off, Google Fonts requests are answered from
// assets/fonts (fonts.js), and any other network request fails the run.
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-ref.js \
//     --scenes tools/parity-ref/view-scenes.json --out <dir> [--only name1,name2]
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

const violations = [];

function dataUrl(rel) {
  const file = path.resolve(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg' }[path.extname(file).toLowerCase()];
  if (!mime) throw new Error(`unsupported scene image type: ${rel}`);
  return `data:${mime};base64,` + fs.readFileSync(file).toString('base64');
}

// The same asset shape app-render.js hands __pref.render.
function assetsFor(scene) {
  const images = {};
  const projects = {};
  for (const rel of [scene.bgImage, ...(scene.layers || []).map((l) => l.image)].filter(Boolean)) images[rel] = dataUrl(rel);
  for (const op of scene.actions || []) {
    if (op.op !== 'project') continue;
    const file = path.resolve(ROOT, op.file);
    if (!fs.existsSync(file)) throw new Error(`project file not found: ${op.file}`);
    projects[op.file] = { name: path.basename(file), text: fs.readFileSync(file, 'utf8') };
  }
  return { images, projects };
}

// Validates a view scene. The engine's golden gate compares every view under the unchanged rule,
// so a layer with an RGB shift (approved divergence rgb-shift-compute, held to a different rule)
// is refused here rather than frozen.
function validate(scene) {
  if (!/^[a-z0-9][a-z0-9-]*$/.test(scene.name || '')) throw new Error(`bad scene name: ${scene.name}`);
  const v = scene.view;
  if (!v || typeof v !== 'object') throw new Error(`${scene.name}: a view scene needs a "view" object`);
  if (!ZOOMS.includes(v.zoom)) throw new Error(`${scene.name}: zoom ${v.zoom} is not a 4.1 zoom step`);
  for (const k of ['mask', 'grid']) if (v[k] !== undefined && typeof v[k] !== 'boolean') throw new Error(`${scene.name}: view.${k} must be a boolean`);
  for (const l of scene.layers || []) {
    if (l.rShift || l.gShift || l.bShift) throw new Error(`${scene.name}: RGB-shifted layers are not view scenes (rgb-shift-compute)`);
  }
  return { zoom: v.zoom, mask: v.mask !== false, grid: v.grid === true };
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
  return scenes.map((s) => ({ scene: s, view: validate(s) }));
}

// Runs in the page after __pref.render. Drives the real zoom buttons and checkboxes, clears the
// selection, renders, and returns the visible canvas.
function viewPage() {
  window.__view = async function (v) {
    const byId = (id) => {
      const el = document.getElementById(id);
      if (!el) throw new Error(`#${id} not found in editor.html`);
      return el;
    };
    const steps = Math.round((v.zoom - zoom) / 0.25);
    for (let i = 0; i < Math.abs(steps); i++) byId(steps > 0 ? 'zoomIn' : 'zoomOut').click(); // editor.js:1490-1491
    if (zoom !== v.zoom) throw new Error(`zoom is ${zoom}, wanted ${v.zoom}`);
    if (byId('zoomLevel').textContent !== (v.zoom * 100) + '%') throw new Error(`zoom label ${byId('zoomLevel').textContent}`);
    for (const [id, on] of [['showMask', v.mask], ['showGrid', v.grid]]) { // editor.js:1494-1495
      const el = byId(id);
      el.checked = on;
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }
    if (showMask !== v.mask || showGrid !== v.grid) throw new Error('mask or grid did not take');
    selectedLayerId = null; // the selection overlay (editor.js:362-421) is M3b's
    render();
    if (drawCanvas) throw new Error('a live drawing preview is still open');
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
      tool: 'tools/parity-ref/view-ref.js',
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), fillsJsBlob: blob('lib/fills.js'),
      appHarnessBlob: blob('tools/parity-ref/app-harness.js'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
    };
    const index = { ...run, scenes: [] };
    for (const { scene, view } of scenes) {
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(HARNESS);
      await win.webContents.executeJavaScript(`(${viewPage.toString()})(); 0;`);
      await callPage(win, `__pref.render(${JSON.stringify(scene)}, ${JSON.stringify(assetsFor(scene))})`);
      const r = await callPage(win, `__view(${JSON.stringify(view)})`);
      if (violations.length) throw new Error(`${scene.name}: ${violations.join('; ')}`);
      if (r.dpr !== 1) throw new Error(`${scene.name}: devicePixelRatio ${r.dpr}, expected 1`);
      const want = [Math.trunc(585 * view.zoom), Math.trunc(559 * view.zoom)]; // canvas.width = TW * zoom, editor.js:340-341
      if (r.width !== want[0] || r.height !== want[1]) throw new Error(`${scene.name}: canvas ${r.width}x${r.height}, expected ${want.join('x')}`);
      const bytes = Buffer.from(r.url.split(',')[1], 'base64');
      fs.writeFileSync(path.join(outDir, scene.name + '.png'), bytes);
      index.scenes.push({ name: scene.name, sha256: sha256(bytes), width: r.width, height: r.height, ...view });
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

In `tools/parity-ref/README.md`, make three edits. First, directly after the intro bullet that starts with `` - `app-render.js` (goldens v2) ``, add this bullet:

```markdown
- `view-ref.js` (M3a) drives the real `editor.html` like `app-render.js` (it injects `app-harness.js` unchanged), then sets the zoom, Mask and Grid with the real controls and captures the visible `#mainCanvas`, not the export. It renders the view scenes into `tests/goldens/view`.
```

Second, at the end of the `## Use` code block (after the two `--save-projects` lines), add these two lines:

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-ref.js \
  --scenes tools/parity-ref/view-scenes.json --out <dir> [--only name1,name2]
```

Third, append this bullet at the end of the `## Scene format` section, after its last bullet:

```markdown
- `view` (view scenes, `view-ref.js` only): `{ zoom, mask?, grid? }`. `zoom` is one of 4.1's steps (0.25 to 3 by 0.25), `mask` defaults to true and `grid` to false. The base fields and actions run exactly as in an app scene; then the zoom buttons and the Mask and Grid checkboxes are driven, the selection is cleared, and the visible canvas is captured. A layer with an RGB shift is refused, because the view gate uses the unchanged rule.
```

Check the tool parses: `node --check tools/parity-ref/view-ref.js && python -c "import json; s=json.load(open('tools/parity-ref/view-scenes.json')); print(len(s), sorted({x['view']['zoom'] for x in s}))"`
Expected: `14 [0.25, 0.5, 1, 1.5, 3]`.

Commit the tool on its own. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add tools/parity-ref/view-ref.js tools/parity-ref/view-scenes.json tools/parity-ref/README.md
git commit -m "feat(parity-ref): view-ref.js captures 4.1's visible canvas at zoom, mask and grid"
```

- [ ] **Step 6: Write the failing coordinate map tests**

Create `src/RBX15.App.Tests/CanvasCoordinatesTests.cs`:

```csharp
using System.Windows;
using RBX15.App.Controls;

namespace RBX15.App.Tests;

/// <summary>canvas-coord-map: 4.1's (client - rect) / zoom (editor.js:1058-1061, :1149-1152, :966-967), in DIPs.</summary>
public class CanvasCoordinatesTests
{
    [Theory]
    [InlineData(100, 50, 1, 100, 50)]
    [InlineData(292.5, 139.5, 0.5, 585, 279)]
    [InlineData(0.5, 0.5, 0.25, 2, 2)]
    [InlineData(146, 70, 0.25, 584, 280)]   // a drop near the right edge at 25% (editor.js:966-967)
    [InlineData(300, 150, 3, 100, 50)]
    [InlineData(346.5, 111, 1.5, 231, 74)]  // torso_front's corner at 150%
    public void Frame_points_divide_by_zoom(double fx, double fy, double zoom, double tx, double ty)
    {
        var p = CanvasCoordinates.ToTemplate(new Point(fx, fy), zoom);
        Assert.Equal(tx, p.X, 12);
        Assert.Equal(ty, p.Y, 12);
    }

    [Fact] // 4.1 never clamps: a drag past the edge keeps going (M3b's mouse capture relies on it)
    public void Points_outside_the_frame_map_outside_the_template_unclamped()
    {
        Assert.Equal(new Point(-10, 600), CanvasCoordinates.ToTemplate(new Point(-30, 1800), 3));
        Assert.Equal(new Point(-30, 1800), CanvasCoordinates.ToFrame(new Point(-10, 600), 3));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.75)]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(2.75)]
    [InlineData(3)]
    public void ToFrame_inverts_ToTemplate(double zoom)
    {
        var frame = new Point(123.25, 456.5);
        var back = CanvasCoordinates.ToFrame(CanvasCoordinates.ToTemplate(frame, zoom), zoom);
        Assert.Equal(frame.X, back.X, 9);
        Assert.Equal(frame.Y, back.Y, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.25)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_zoom_that_is_not_finite_and_positive_is_refused(double zoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCoordinates.ToTemplate(new Point(1, 1), zoom));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasCoordinates.ToFrame(new Point(1, 1), zoom));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0234: The type or namespace name 'Controls' does not exist in the namespace 'RBX15.App'` (or `CS0103: The name 'CanvasCoordinates' does not exist`) in `CanvasCoordinatesTests.cs`.

Create `src/RBX15.App/Controls/CanvasCoordinates.cs`:

```csharp
using System.Windows;

namespace RBX15.App.Controls;

/// <summary>
/// Mouse position to template space and back (canvas-coord-map). 4.1 maps every canvas event with
/// <c>(clientX - rect.left) / zoom</c> (editor.js:1058-1061, :1149-1152; the drop point at :966-967):
/// CSS px relative to the canvas, divided by zoom, unclamped. v5 takes the position relative to the
/// <see cref="TemplateCanvasHost"/> in DIPs, which are CSS px. The host is <c>ViewSize / DeviceScale</c>
/// DIPs, so the device scale cancels and only zoom remains. A point outside the frame maps outside
/// the template (negative, or past 585x559); callers clamp if they need to. M3b's tools consume this.
/// </summary>
public static class CanvasCoordinates
{
    public static Point ToTemplate(Point inFrame, double zoom)
    {
        CheckZoom(zoom);
        return new Point(inFrame.X / zoom, inFrame.Y / zoom);
    }

    public static Point ToFrame(Point template, double zoom)
    {
        CheckZoom(zoom);
        return new Point(template.X * zoom, template.Y * zoom);
    }

    static void CheckZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "zoom must be finite and positive");
    }
}
```

Create `src/RBX15.App/Controls/CanvasDrop.cs`:

```csharp
using System.Windows;

namespace RBX15.App.Controls;

/// <summary>
/// Files dropped on the canvas (drop-canvas): the paths in the order the drag source lists them,
/// and the drop point in template pixels (editor.js:966-967). Every file lands on that one point,
/// as 4.1's <c>handleFiles(e.dataTransfer.files, dropX, dropY)</c> does (editor.js:984); Task 4's
/// ImportService places them.
/// </summary>
public sealed record CanvasDrop(IReadOnlyList<string> Paths, Point TemplatePoint);
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.CanvasCoordinatesTests"`
Expected: `Passed!  - Failed:     0, Passed:    17, Skipped:     0, Total:    17` for `RBX15.App.Tests.dll`.

- [ ] **Step 7: Write the failing canvas view-model tests, then the view-model**

Create `src/RBX15.App.Tests/CanvasViewModelTests.cs`:

```csharp
using System.ComponentModel;
using System.Windows;
using RBX15.App.Controls;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>The zoom pill (zoom-controls, canvas-zoom-controls) and the document the canvas shows.</summary>
public class CanvasViewModelTests
{
    [Fact] // editor.js:81 zoom = 1; editor.html:334 "100%"
    public void Starts_at_100_percent_showing_the_session_document()
    {
        var session = new EditorSession();
        var vm = new CanvasViewModel(session);
        Assert.Equal(1, vm.Zoom);
        Assert.Equal("100%", vm.ZoomLabel);
        Assert.Same(session.Document, vm.Document);
        Assert.True(vm.ZoomInCommand.CanExecute(null));
        Assert.True(vm.ZoomOutCommand.CanExecute(null));
    }

    [Fact] // zoom-controls parityCheck: the label for all 12 levels; editor.js:1490-1491
    public void Zoom_steps_by_a_quarter_from_25_to_300_percent_with_4_1_labels()
    {
        var vm = new CanvasViewModel(new EditorSession());
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
        var vm = new CanvasViewModel(new EditorSession());
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
        var vm = new CanvasViewModel(session);
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
        var vm = new CanvasViewModel(session);
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
        var vm = new CanvasViewModel(new EditorSession());
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

    [Fact] // ruling R5 (critic C13): Task 4 sets ClickHandler to select the topmost layer under the point, or none
    public void Click_runs_the_handler_and_is_disabled_without_one()
    {
        var vm = new CanvasViewModel(new EditorSession());
        var at = new Point(120, 90);
        Assert.False(vm.ClickCommand.CanExecute(at));

        int changed = 0;
        vm.ClickCommand.CanExecuteChanged += (_, _) => changed++;
        Point? got = null;
        vm.ClickHandler = p => got = p;
        Assert.Equal(1, changed);
        Assert.True(vm.ClickCommand.CanExecute(at));

        vm.ClickCommand.Execute(at);
        Assert.Equal(at, got);
    }

    [Fact]
    public void A_null_session_is_refused() =>
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(null!));
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'CanvasViewModel' could not be found` in `CanvasViewModelTests.cs`.

Create `src/RBX15.App/ViewModels/CanvasViewModel.cs`:

```csharp
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Controls;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The canvas area: the document the host shows, and the zoom pill (canvas-zoom-controls,
/// zoom-controls). Zoom starts at 1 (editor.js:81), steps by 0.25 between 0.25 and 3
/// (editor.js:1490-1491), and is view state: never an undo step, never saved, 100% on launch.
/// Each command is disabled at its limit, where 4.1's button clamped to a no-op
/// (divergence zoom-pill-limits, Este's "never show a dead button"). A canvas click runs
/// <see cref="ClickHandler"/> with the template point (ruling R5); Task 4 makes it select the topmost
/// layer there, or none.
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 3;
    public const double ZoomStep = 0.25;

    readonly EditorSession _session;
    Action<CanvasDrop>? _dropHandler;
    Action<Point>? _clickHandler;

    public CanvasViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _document = session.Document;
        session.Changed += OnSessionChanged;
    }

    /// <summary>The session's document, refreshed on every change that touches it (edits, undo, redo).</summary>
    [ObservableProperty]
    private Document _document;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    private double _zoom = 1;

    /// <summary><c>(zoom*100)+'%'</c> (editor.js:1490): "25%", "100%", "125%".</summary>
    public string ZoomLabel => (Zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>Called with every canvas drop the host accepts. Task 4 sets it to ImportService.Import.</summary>
    public Action<CanvasDrop>? DropHandler
    {
        get => _dropHandler;
        set
        {
            _dropHandler = value;
            DropCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Called with the template point of every canvas click (a press and release without a drag). Task 4
    /// sets it to select the topmost visible layer under the point, or none (editor.js:1100-1116).
    /// </summary>
    public Action<Point>? ClickHandler
    {
        get => _clickHandler;
        set
        {
            _clickHandler = value;
            ClickCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanClick))]
    void Click(Point point) => _clickHandler?.Invoke(point);

    bool CanClick(Point point) => _clickHandler is not null;

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (e.What.HasFlag(SessionChange.Document)) Document = _session.Document;
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`. The toolkit's generators emit `Document`, `Zoom`, `ZoomInCommand`, `ZoomOutCommand`, `DropCommand` (an `IRelayCommand<CanvasDrop>`) and `ClickCommand` (an `IRelayCommand<Point>`), and field-backed `[ObservableProperty]` raises no MVVMTK diagnostic at 8.4.2 (measured).

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.CanvasCoordinatesTests|FullyQualifiedName~RBX15.App.Tests.CanvasViewModelTests"`
Expected: `Passed!  - Failed:     0, Passed:    25, Skipped:     0, Total:    25` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 622, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 157, Failed: 0`.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Controls/CanvasCoordinates.cs src/RBX15.App/Controls/CanvasDrop.cs src/RBX15.App/ViewModels/CanvasViewModel.cs src/RBX15.App.Tests/CanvasCoordinatesTests.cs src/RBX15.App.Tests/CanvasViewModelTests.cs
git commit -m "feat(app): canvas coordinate map, CanvasDrop and the zoom pill view-model"
```

- [ ] **Step 8: Write the failing canvas host tests, then the host**

Create `src/RBX15.App.Tests/TemplateCanvasHostTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Controls;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas host shows engine frames 1:1 at device pixels (spec §4 canvas host). Every test pins the
/// device scale with ApplyDpi, so the result never depends on the test machine's monitor.
/// </summary>
public class TemplateCanvasHostTests
{
    static SKImage Solid(int w, int h, SKColor c)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(c);
        return surface.Snapshot();
    }

    /// <summary>A document with a fill, a layer that crosses the mask and an off-region area.</summary>
    static Document Sample(bool grid = false) => new()
    {
        BgOpacity = 0.6,
        ShowGrid = grid,
        RegionFills = ImmutableList.Create(("torso_front", Fill.Solid("#17d4fa"))),
        Layers = ImmutableList.Create(new Layer { Id = 1, Image = Solid(120, 40, new SKColor(242, 47, 137)), X = 180, Y = 120, Rotation = 12 }),
    };

    static TemplateCanvasHost Host(Document? doc, double zoom = 1, double deviceScale = 1)
    {
        var host = new TemplateCanvasHost();
        host.ApplyDpi(deviceScale);
        host.Zoom = zoom;
        host.Document = doc;
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return host;
    }

    static byte[] Pixels(BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        var bytes = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(bytes, stride, 0);
        return bytes;
    }

    /// <summary>The engine's own frame as premultiplied BGRA bytes, the layout of Pbgra32.</summary>
    static byte[] EngineFrame(Document doc, ViewOptions options)
    {
        using var image = Renderer.RenderViewImage(doc, options);
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.Bytes;
    }

    [Fact] // the Renderer's precondition: Pbgra32 wrapped as Bgra8888 is kN32 premul on Windows
    public void Pbgra32_is_skias_native_premultiplied_format_here() =>
        Assert.Equal(SKColorType.Bgra8888, SKImageInfo.PlatformColorType);

    [Fact] // spec §1: the app never draws pixels itself; it shows engine frames
    public void The_frame_is_the_engine_view_pixel_for_pixel() => StaThread.Run(() =>
    {
        var doc = Sample(grid: true);
        var host = Host(doc, zoom: 1.5);
        var frame = Assert.IsType<WriteableBitmap>(host.Frame);
        Assert.Equal(PixelFormats.Pbgra32, frame.Format);
        Assert.Equal((877, 838), (frame.PixelWidth, frame.PixelHeight));
        Assert.Equal(EngineFrame(doc, new ViewOptions(1.5, 1, true)), Pixels(frame));
    });

    [Theory] // render-zoom-canvas sizes; cross-window-dpi-rendering: DIP size = pixels / device scale
    [InlineData(0.25, 1, 146, 139)]
    [InlineData(1, 1, 585, 559)]
    [InlineData(1.5, 1, 877, 838)]
    [InlineData(3, 1, 1755, 1677)]
    [InlineData(1, 1.25, 731, 698)]
    [InlineData(1, 1.5, 877, 838)]
    [InlineData(0.5, 2, 585, 559)]
    public void Frame_pixels_follow_zoom_and_device_scale_and_DIPs_divide_them_back(double zoom, double scale, int w, int h) => StaThread.Run(() =>
    {
        var host = Host(new Document(), zoom, scale);
        Assert.Equal((w, h), (host.Frame!.PixelWidth, host.Frame.PixelHeight));
        Assert.Equal(96 * scale, host.Frame.DpiX, 9);
        Assert.Equal(w / scale, host.DesiredSize.Width, 9);
        Assert.Equal(h / scale, host.DesiredSize.Height, 9);
    });

    [Fact] // a document, zoom or grid change re-renders (render-pipeline-canvas-reset)
    public void Document_and_zoom_changes_re_render() => StaThread.Run(() =>
    {
        var host = Host(Sample());
        var plain = Pixels(host.Frame!);

        host.Document = Sample(grid: true);
        Assert.NotEqual(plain, Pixels(host.Frame!));
        Assert.Equal(EngineFrame(Sample(grid: true), new ViewOptions(1, 1, true)), Pixels(host.Frame!));

        host.Zoom = 0.5;
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal((292, 279), (host.Frame!.PixelWidth, host.Frame.PixelHeight));
        Assert.Equal(new Size(292, 279), host.DesiredSize);
    });

    [Fact] // OnDpiChanged: moving the window to a 150% monitor renders at 1.5 device pixels per DIP
    public void A_dpi_change_re_renders_at_the_new_device_scale() => StaThread.Run(() =>
    {
        var doc = Sample(grid: true);
        var host = Host(doc);
        host.ApplyDpi(1.5);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal(1.5, host.DeviceScale);
        Assert.Equal(144, host.Frame!.DpiX, 9);
        Assert.Equal(EngineFrame(doc, new ViewOptions(1, 1.5, true)), Pixels(host.Frame));
        Assert.Equal(877 / 1.5, host.DesiredSize.Width, 9);
    });

    [Fact]
    public void Without_a_document_there_is_no_frame_and_no_size() => StaThread.Run(() =>
    {
        var host = Host(null);
        Assert.Null(host.Frame);
        Assert.Equal(new Size(0, 0), host.DesiredSize);
        host.Document = new Document();
        Assert.NotNull(host.Frame);
        host.Document = null;
        Assert.Null(host.Frame);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void A_zoom_that_is_not_finite_and_positive_is_refused(double zoom) => StaThread.Run(() =>
    {
        var host = new TemplateCanvasHost();
        Assert.Throws<ArgumentException>(() => host.Zoom = zoom);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.ApplyDpi(zoom));
    });

    sealed class RecordingCommand(bool canExecute = true) : ICommand
    {
        public List<object?> Executed { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    [Fact] // drop-canvas: drop point = position / zoom (editor.js:966-967); every file, in the order given
    public void Dropped_files_run_the_command_with_the_template_point() => StaThread.Run(() =>
    {
        var command = new RecordingCommand();
        var host = Host(new Document(), zoom: 2);
        host.DropCommand = command;
        var data = new DataObject(DataFormats.FileDrop, new[] { @"C:\art\b.png", @"C:\art\a.png" });

        Assert.True(host.HandleDrop(data, new Point(100, 50)));
        var drop = Assert.IsType<CanvasDrop>(Assert.Single(command.Executed));
        Assert.Equal(new[] { @"C:\art\b.png", @"C:\art\a.png" }, drop.Paths);
        Assert.Equal(new Point(50, 25), drop.TemplatePoint);
    });

    [Fact] // Review Focus: text, a browser image with no file, or no handler does nothing and never throws
    public void A_drop_without_files_or_without_a_willing_command_does_nothing() => StaThread.Run(() =>
    {
        var host = Host(new Document());
        var command = new RecordingCommand();
        host.DropCommand = command;
        Assert.False(host.HandleDrop(new DataObject(DataFormats.UnicodeText, "https://example.com/shirt.png"), new Point(10, 10)));
        Assert.False(host.HandleDrop(new DataObject(DataFormats.FileDrop, Array.Empty<string>()), new Point(10, 10)));
        Assert.Empty(command.Executed);

        host.DropCommand = new RecordingCommand(canExecute: false);
        Assert.False(host.HandleDrop(new DataObject(DataFormats.FileDrop, new[] { @"C:\art\a.png" }), new Point(10, 10)));

        host.DropCommand = null;
        Assert.False(host.HandleDrop(new DataObject(DataFormats.FileDrop, new[] { @"C:\art\a.png" }), new Point(10, 10)));
    });

    [Fact] // ruling R5 (critic C13): a left click without a drag runs ClickCommand with position / zoom (editor.js:1058-1061, :1100-1116)
    public void A_click_runs_the_click_command_with_the_template_point_and_a_drag_does_not() => StaThread.Run(() =>
    {
        var host = Host(new Document(), zoom: 2);
        Assert.False(host.HandleClick(new Point(100, 50))); // no command yet

        var command = new RecordingCommand();
        host.ClickCommand = command;
        Assert.True(host.HandleClick(new Point(100, 50)));
        Assert.Equal(new Point(50, 25), Assert.IsType<Point>(Assert.Single(command.Executed)));

        host.ClickCommand = new RecordingCommand(canExecute: false);
        Assert.False(host.HandleClick(new Point(100, 50)));

        // A press and release farther apart than the system drag distance is a drag (M3b), not a click.
        var down = new Point(10, 10);
        Assert.True(TemplateCanvasHost.IsClick(down, new Point(10 + SystemParameters.MinimumHorizontalDragDistance, 10)));
        Assert.False(TemplateCanvasHost.IsClick(down, new Point(11 + SystemParameters.MinimumHorizontalDragDistance, 10)));
        Assert.False(TemplateCanvasHost.IsClick(down, new Point(10, 11 + SystemParameters.MinimumVerticalDragDistance)));
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'TemplateCanvasHost' could not be found` in `TemplateCanvasHostTests.cs`.

Create `src/RBX15.App/Controls/TemplateCanvasHost.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Controls;

/// <summary>
/// Shows engine view frames (<c>Renderer.RenderView</c>) in a Pbgra32 <see cref="WriteableBitmap"/>, one
/// bitmap pixel per device pixel. The engine draws every pixel; this control only blits (spec §1).
/// <list type="bullet">
/// <item>The frame is <c>Renderer.ViewSize(new ViewOptions(Zoom, DeviceScale, Document.ShowGrid))</c> pixels, and the
///   control measures <c>pixels / DeviceScale</c> DIPs. At 96 DPI, zoom 1 is 585x559 DIPs, the CSS size of 4.1's
///   canvas (canvas-coord-map).</item>
/// <item>It re-renders when Document or Zoom changes, and when the monitor's DPI changes (OnDpiChanged; app.manifest
///   makes the app per-monitor DPI v2). 4.1 re-rendered on every change too (render-pipeline-canvas-reset).</item>
/// <item>Bitmap scaling is nearest-neighbor, so a fractional layout offset never blurs the frame. CanvasView sets
///   UseLayoutRounding, so the frame lands on whole device pixels.</item>
/// <item>Files dropped on it run DropCommand with a <see cref="CanvasDrop"/> (drop-canvas). Every drop is handled, so
///   none falls through to the window (shell-external-drop-guard).</item>
/// <item>A left press and release within the system drag distance runs ClickCommand with the template point
///   (canvas-coord-map). Task 4 wires it to select the topmost layer there, or none (editor.js:1100-1116). The drag
///   gestures are M3b's select tool.</item>
/// </list>
/// </summary>
public sealed class TemplateCanvasHost : FrameworkElement
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(Document), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnFrameInputChanged));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnFrameInputChanged),
        value => value is double z && double.IsFinite(z) && z > 0);

    public static readonly DependencyProperty DropCommandProperty = DependencyProperty.Register(
        nameof(DropCommand), typeof(ICommand), typeof(TemplateCanvasHost));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(TemplateCanvasHost));

    WriteableBitmap? _frame;
    double _deviceScale;
    Point? _pressedAt;

    public TemplateCanvasHost()
    {
        _deviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        AllowDrop = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        AutomationProperties.SetName(this, "Template canvas");
        // A window that opens on a monitor whose DPI differs from the one the control was built for.
        Loaded += (_, _) =>
        {
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (scale != _deviceScale) ApplyDpi(scale);
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

    /// <summary>Runs with the template <see cref="Point"/> of a left click that did not drag.</summary>
    public ICommand? ClickCommand
    {
        get => (ICommand?)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    /// <summary>The bitmap on screen; null while there is no document. Read by tests.</summary>
    internal WriteableBitmap? Frame => _frame;

    /// <summary>Device pixels per DIP that the frame is rendered for.</summary>
    internal double DeviceScale => _deviceScale;

    static void OnFrameInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TemplateCanvasHost)d).Refresh();

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
        Refresh();
        InvalidateMeasure();
    }

    void Refresh()
    {
        if (Document is not { } doc)
        {
            _frame = null;
            InvalidateVisual();
            return;
        }
        var options = new ViewOptions(Zoom, _deviceScale, doc.ShowGrid);
        var size = Renderer.ViewSize(options);
        double dpi = 96 * _deviceScale;
        if (_frame is null || _frame.PixelWidth != size.Width || _frame.PixelHeight != size.Height || _frame.DpiX != dpi)
            _frame = new WriteableBitmap(size.Width, size.Height, dpi, dpi, PixelFormats.Pbgra32, null);

        _frame.Lock();
        try
        {
            // Pbgra32 is premultiplied BGRA, which is Skia's kN32 on Windows: the Renderer's precondition.
            var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var surface = SKSurface.Create(info, _frame.BackBuffer, _frame.BackBufferStride)
                ?? throw new InvalidOperationException("cannot wrap the frame bitmap"))
            {
                Renderer.RenderView(surface.Canvas, doc, options);
                surface.Canvas.Flush();
            }
            _frame.AddDirtyRect(new Int32Rect(0, 0, size.Width, size.Height));
        }
        finally
        {
            _frame.Unlock();
        }
        InvalidateVisual();
    }

    Size FrameSize => _frame is null ? default : new Size(_frame.PixelWidth / _deviceScale, _frame.PixelHeight / _deviceScale);

    protected override Size MeasureOverride(Size availableSize) => FrameSize;

    protected override Size ArrangeOverride(Size finalSize) => FrameSize;

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_frame is not null) drawingContext.DrawImage(_frame, new Rect(FrameSize));
    }

    protected override void OnDragEnter(DragEventArgs e) => ShowDropEffect(e);

    protected override void OnDragOver(DragEventArgs e) => ShowDropEffect(e);

    static void ShowDropEffect(DragEventArgs e)
    {
        e.Effects = Files(e.Data) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
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

    static string[]? Files(IDataObject? data) =>
        data is not null && data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths
            ? paths : null;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _pressedAt = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pressedAt is not { } down) return;
        _pressedAt = null;
        ReleaseMouseCapture();
        var up = e.GetPosition(this);
        if (IsClick(down, up)) HandleClick(up);
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _pressedAt = null;
    }

    /// <summary>A press and release within the system drag distance is a click; farther apart is a drag (M3b).</summary>
    internal static bool IsClick(Point down, Point up) =>
        Math.Abs(up.X - down.X) <= SystemParameters.MinimumHorizontalDragDistance &&
        Math.Abs(up.Y - down.Y) <= SystemParameters.MinimumVerticalDragDistance;

    /// <summary>
    /// The click logic without MouseButtonEventArgs. Runs ClickCommand with <c>position / zoom</c>
    /// (CanvasCoordinates.ToTemplate) and returns true when the command accepts the point.
    /// </summary>
    internal bool HandleClick(Point inHost)
    {
        var point = CanvasCoordinates.ToTemplate(inHost, Zoom);
        if (ClickCommand is not { } command || !command.CanExecute(point)) return false;
        command.Execute(point);
        return true;
    }
}
```

The tests reach the host's internal seams through Task 1's `InternalsVisibleTo`. The per-monitor DPI v2 manifest that makes WPF raise `OnDpiChanged` is Task 1's `app.manifest` (ruling R9); this task does not touch it or the project file.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.TemplateCanvasHostTests"`
Expected: `Passed!  - Failed:     0, Passed:    18, Skipped:     0, Total:    18` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 622, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 175, Failed: 0`.

If `The_frame_is_the_engine_view_pixel_for_pixel` fails, the host is drawing something itself or wrapping the bitmap in a non-kN32 format. The host must only blit `Renderer.RenderView` into the Pbgra32 back buffer; fix the host. Never loosen the byte equality.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Controls/TemplateCanvasHost.cs src/RBX15.App.Tests/TemplateCanvasHostTests.cs
git commit -m "feat(app): TemplateCanvasHost shows engine frames 1:1 at device pixels, with drop and click"
```

- [ ] **Step 9: Write the failing view tests, then CanvasView and its place in the window**

Create `src/RBX15.App.Tests/CanvasViewTests.cs`:

```csharp
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using RBX15.App.Controls;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>CanvasView wires the host and the zoom pill to CanvasViewModel; its theme keys exist; the app is per-monitor DPI v2.</summary>
public class CanvasViewTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static string AppFile(params string[] parts) => Path.Combine([Root, "src", "RBX15.App", .. parts]);

    /// <summary>
    /// Bindings in a parsed view attach at DataBind priority, so the test runs the queue once
    /// (StaThread.Pump, to Background, below DataBind) before reading bound values.
    /// </summary>
    static CanvasView Load(CanvasViewModel vm)
    {
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        return view;
    }

    [Fact]
    public void The_host_and_the_pill_follow_the_view_model() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = new CanvasViewModel(session);
        var view = Load(vm);
        var host = (TemplateCanvasHost)view.FindName("Host");
        var label = (TextBlock)view.FindName("ZoomLabel");
        var zoomIn = (Button)view.FindName("ZoomInButton");
        var zoomOut = (Button)view.FindName("ZoomOutButton");

        Assert.Same(session.Document, host.Document);
        Assert.Equal(1, host.Zoom);
        Assert.Same(vm.DropCommand, host.DropCommand);
        Assert.Same(vm.ClickCommand, host.ClickCommand);
        Assert.Equal("100%", label.Text);
        Assert.Equal(("-", "+"), ((string)zoomOut.Content, (string)zoomIn.Content)); // editor.html:333-335
        Assert.Equal(("Zoom out", "Zoom in"), (AutomationProperties.GetName(zoomOut), AutomationProperties.GetName(zoomIn)));
        Assert.Equal("Template canvas", AutomationProperties.GetName(host));

        zoomIn.Command.Execute(null);
        Assert.Equal(1.25, host.Zoom);
        Assert.Equal("125%", label.Text);

        session.Apply(d => d with { ShowGrid = true });
        Assert.Same(session.Document, host.Document);

        while (zoomOut.Command.CanExecute(null)) zoomOut.Command.Execute(null);
        Assert.False(zoomOut.IsEnabled);
        Assert.Equal(0.25, host.Zoom);
    });

    [Fact] // canvas-zoom-clipped-no-pan: scrollbars appear only when the frame outgrows the area
    public void The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits() => StaThread.Run(() =>
    {
        var vm = new CanvasViewModel(new EditorSession());
        var view = Load(vm);
        var host = (TemplateCanvasHost)view.FindName("Host");
        host.ApplyDpi(1);
        var scroller = (ScrollViewer)view.FindName("Scroller");

        view.Measure(new Size(901, 701));
        view.Arrange(new Rect(0, 0, 901, 701));
        Assert.Equal(0, scroller.ScrollableWidth);
        Assert.Equal(0, scroller.ScrollableHeight);
        // The 587x561 wrap (585x559 and its 1px border) is centered, so the frame starts 1px inside it.
        Assert.Equal(new Point((901 - 587) / 2 + 1, (701 - 561) / 2 + 1), host.TranslatePoint(new Point(0, 0), view));

        for (int i = 0; i < 8; i++) vm.ZoomInCommand.Execute(null); // 300%: 1755x1677
        view.UpdateLayout(); // the host invalidated its measure; run the queued layout pass
        Assert.True(scroller.ScrollableWidth > 0);
        Assert.True(scroller.ScrollableHeight > 0);
    });

    [Fact] // every theme key the view names exists in T1's dictionaries (a missing DynamicResource fails silently)
    public void Every_theme_key_the_view_uses_is_defined_in_the_theme()
    {
        var view = File.ReadAllText(AppFile("Views", "CanvasView.xaml"));
        var theme = string.Concat(Directory.EnumerateFiles(AppFile("Themes"), "*.xaml").Select(File.ReadAllText));
        var keys = Regex.Matches(view, @"\{DynamicResource (\w+)\}").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.Contains($"x:Key=\"{k}\"", theme));
    }

    [Fact] // spec §4 display scaling; cross-window-dpi-rendering. Without it WPF never raises OnDpiChanged.
    public void The_app_is_per_monitor_dpi_v2()
    {
        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", File.ReadAllText(AppFile("RBX15.App.csproj")));
        var manifest = File.ReadAllText(AppFile("app.manifest"));
        Assert.Contains(">PerMonitorV2</dpiAwareness>", manifest);
        Assert.Contains(">true/pm</dpiAware>", manifest);
    }

    [Fact] // the CanvasArea grid shows the canvas (Task 1's skeleton, filled here)
    public void The_main_window_hosts_the_canvas_view_on_MainViewModel_Canvas()
    {
        var xaml = File.ReadAllText(AppFile("MainWindow.xaml"));
        Assert.Contains("xmlns:views=\"clr-namespace:RBX15.App.Views\"", xaml);
        Assert.Matches(@"<views:CanvasView\s+DataContext=""\{Binding Canvas\}""\s*/>", xaml);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0234: The type or namespace name 'Views' does not exist in the namespace 'RBX15.App'` in `CanvasViewTests.cs`.

Create `src/RBX15.App/Views/CanvasView.xaml`:

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
        <ScrollViewer x:Name="Scroller"
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
                                                 ClickCommand="{Binding ClickCommand}" />
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
                           AutomationProperties.Name="Zoom level" />
                <Button x:Name="ZoomInButton" Style="{StaticResource ZoomButton}" Content="+"
                        Command="{Binding ZoomInCommand}"
                        AutomationProperties.Name="Zoom in" ToolTip="Zoom in" />
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

Create `src/RBX15.App/Views/CanvasView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The canvas area: the scrolled, centered template frame and the zoom pill. DataContext is a CanvasViewModel.</summary>
public partial class CanvasView : UserControl
{
    public CanvasView()
    {
        InitializeComponent();
    }
}
```

In `src/RBX15.App/ViewModels/MainViewModel.cs`, add this member as the last member of the `MainViewModel` class, directly before the class's closing brace:

```csharp
    /// <summary>The canvas area (Task 2). Required, so the composition root cannot forget it.</summary>
    public required CanvasViewModel Canvas { get; init; }
```

`required` makes every construction site a compile error until it sets `Canvas`. There are exactly two, Task 1's `Composition.Build` and `TestShell.Main` (Task 1 ruling 15), and they take the same two lines.

In `src/RBX15.App/Composition.cs` and in `src/RBX15.App.Tests/TestShell.cs`, insert directly above the line `        // M3a: construction lines above this line.`:

```csharp
        var canvas = new CanvasViewModel(session);
```

and directly above the line `            // M3a: required members above this line.`:

```csharp
            Canvas = canvas,
```

Both files already have `using RBX15.App.ViewModels;`.

In `src/RBX15.App/MainWindow.xaml`, replace Task 1's line

```xml
      <Grid x:Name="CanvasArea" Grid.Column="1" Background="{StaticResource Ink950Brush}" ClipToBounds="True" />
```

with

```xml
      <Grid x:Name="CanvasArea" Grid.Column="1" Background="{StaticResource Ink950Brush}" ClipToBounds="True">
        <views:CanvasView DataContext="{Binding Canvas}" />
      </Grid>
```

The root already declares `xmlns:views` (Task 1 ruling 16).

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`. A `CS9035` (required member `Canvas` not set) means a construction outside `Composition` and `TestShell`: route it through `TestShell.Main`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.CanvasViewTests"`
Expected: `Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5` for `RBX15.App.Tests.dll`. If `Every_theme_key_the_view_uses_is_defined_in_the_theme` fails, the view names a key Task 1 spells differently. Rename the view's key to Task 1's spelling (Addition 4 lists the CSS token each one stands for); never add a duplicate token.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 622`, Parity.Tests `35`, App.Tests `Passed: 180, Failed: 0`. `ShellTests` still passes: it builds the window through `TestShell`, which now sets `Canvas`.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Views/CanvasView.xaml src/RBX15.App/Views/CanvasView.xaml.cs src/RBX15.App/ViewModels/MainViewModel.cs src/RBX15.App/Composition.cs src/RBX15.App/MainWindow.xaml src/RBX15.App.Tests
git commit -m "feat(app): CanvasView, the scrolled template frame and the zoom pill, in the center panel"
```

- [ ] **Step 10: (Controller) Capture 4.1's view canvas once, and freeze it**

**This step is the controller's.** A subagent implementing Task 2 stops after Step 9 and reports that Steps 1 to 9 are done and Step 10 is waiting for the controller. The capture runs 4.1 in Electron on this machine, under the README rules: CPU raster, `env -u ELECTRON_RUN_AS_NODE`, and provenance in `index.json`. From the commit at the end of this step, `tests/goldens/view` is frozen like every other golden: never re-run the tool into it.

Check that the 4.1 sources are the ones the app goldens were captured from:

```bash
git hash-object editor.js editor.html
python -c "import json; d=json.load(open('tests/goldens/app/index.json')); print(d['editorJsBlob']); print(d['editorHtmlBlob'])"
```

Expected: `2790c41e418cd5bfea82cf1ae4d71626a7b54e61` and `401a5d5f33c4accbb452cb080c9c544a0f79a6ce`, twice, in that order. If they differ, the Electron app changed (a global-constraint violation): stop and report it.

Render all 14 scenes twice into the git-ignored `.superpowers/`:

```bash
for run in a b; do
  env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/view-ref.js \
    --scenes tools/parity-ref/view-scenes.json --out .superpowers/m3-measure/view-ref-$run 2>&1 | grep -E "^ok|Error"
done
```

Expected: `ok 14`, twice.

Save this as `.superpowers/m3/freeze-view-ref.py` and run it from the repo root. It compares the runs, checks provenance against the app goldens, and copies run `a` into the frozen folder with the scene file beside it:

```python
import hashlib, json, pathlib, shutil
a = pathlib.Path(".superpowers/m3-measure/view-ref-a")
b = pathlib.Path(".superpowers/m3-measure/view-ref-b")
scenes_src = pathlib.Path("tools/parity-ref/view-scenes.json")
out = pathlib.Path("tests/goldens/view")
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
names = [s["name"] for s in json.loads(scenes_src.read_text(encoding="utf-8"))]
assert len(names) == 14 and sorted(p.stem for p in a.glob("*.png")) == sorted(names), "run a must hold the 14 view scenes"
assert sorted(p.stem for p in b.glob("*.png")) == sorted(names), "run b must hold the 14 view scenes"
bad = [n for n in names if sha(a / f"{n}.png") != sha(b / f"{n}.png")]
assert not bad, f"MISMATCH {bad}: 4.1's view canvas is not deterministic; never freeze it"
ia = json.loads((a / "index.json").read_text(encoding="utf-8"))
ib = json.loads((b / "index.json").read_text(encoding="utf-8"))
assert ia == ib, "the two runs' index.json differ"
assert [s["name"] for s in ia["scenes"]] == names, "index.json must list the scenes in view-scenes.json order"
for s in ia["scenes"]:
    assert s["sha256"] == sha(a / f"{s['name']}.png"), f"{s['name']}: index sha256 does not match the file"
print(f"reproducible: {len(names)}")
digest = hashlib.sha256("".join(f"{n} {sha(a / f'{n}.png')}\n" for n in sorted(names)).encode()).hexdigest()
print("digest", digest)
app = json.loads(pathlib.Path("tests/goldens/app/index.json").read_text(encoding="utf-8"))
for k in ("electron", "chrome", "editorJsBlob", "editorHtmlBlob", "fontsManifestSha256"):
    assert ia[k] == app[k], f"{k} differs from the app goldens' index: {ia[k]} vs {app[k]}"
assert not out.exists(), f"{out} already exists: the view goldens are frozen, never recaptured"
out.mkdir()
for n in names:
    shutil.copyfile(a / f"{n}.png", out / f"{n}.png")
shutil.copyfile(a / "index.json", out / "index.json")
shutil.copyfile(scenes_src, out / "scenes.json")
print(f"froze {len(names)} views, index.json and scenes.json in {out.as_posix()}")
```

```bash
python .superpowers/m3/freeze-view-ref.py
ls tests/goldens/view | wc -l
python -c "import json; d=json.load(open('tests/goldens/view/index.json')); print(len(d['scenes']), d['electron'], d['chrome'], *[f\"{s['name']}={s['width']}x{s['height']}\" for s in d['scenes'] if s['zoom'] in (0.25, 1.5, 3)])"
```

Expected:

- `reproducible: 14`.
- `digest e69fad047547ea4e6f5311362b3cfa10501d3d1612deb80cc6a33157a904264a`. That is the hash of the 14 sorted `name sha256` lines; it equals the drafting runs, so these are the frames every number in this task was measured against. A different digest with `reproducible: 14` means the rendering environment changed (Electron, the fonts, `editor.js` or the scene file): stop and report the digest; do not commit.
- `froze 14 views, index.json and scenes.json in tests/goldens/view`, then `16`.
- `14 41.1.0 146.0.7680.166 view-shirt-z25=146x139 view-shirt-z150=877x838 view-shirt-flat-z300=1755x1677 view-shirt-z25-nomask=146x139 view-shirt-z150-nomask=877x838 view-shirt-z150-grid=877x838 view-pants-flat-z300-grid-nomask=1755x1677`.

Commit the frozen views on their own, with the controller's own `Co-Authored-By` trailer:

```bash
git add tests/goldens/view
git commit -m "test(goldens): freeze 4.1's visible canvas at five zooms, mask off and grid on (view-ref.js)"
git status --short
```

Expected: `git status --short` prints nothing.

- [ ] **Step 11: The view golden gate**

Factor the unchanged rule so it takes any rendered image. In `src/RBX15.Engine.Tests/GoldenRule.cs`, replace the whole `AssertMatches` method. Its current text is:

```csharp
    public static void AssertMatches(string name, string goldenPath, Document document, string heatmapPath)
    {
        using var golden = ImageDiff.Load(goldenPath);
        using var rendered = Renderer.Export(document);
        using var candidate = ImageDiff.FromImage(rendered);

        bool rgbShift = document.Layers.Any(l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0);
        DiffResult result;
        if (rgbShift)
        {
            using var g = OpaqueOnly(golden, golden);
            using var c = OpaqueOnly(candidate, golden);
            result = ImageDiff.Compare(g, c);
            int worstAlpha = 0;
            for (int y = 0; y < golden.Height; y++)
                for (int x = 0; x < golden.Width; x++)
                    worstAlpha = Math.Max(worstAlpha, Math.Abs(golden.GetPixel(x, y).Alpha - candidate.GetPixel(x, y).Alpha));
            Assert.True(worstAlpha <= 1, $"{name}: RGB shift must never change alpha (worst alpha delta {worstAlpha})");
        }
        else
        {
            result = ImageDiff.Compare(golden, candidate);
        }

        if (!result.Pass)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(heatmapPath, data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }
```

with:

```csharp
    public static void AssertMatches(string name, string goldenPath, Document document, string heatmapPath)
    {
        using var rendered = Renderer.Export(document);
        bool rgbShift = document.Layers.Any(l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0);
        if (!rgbShift)
        {
            AssertImageMatches(name, goldenPath, rendered, heatmapPath);
            return;
        }

        using var golden = ImageDiff.Load(goldenPath);
        using var candidate = ImageDiff.FromImage(rendered);
        using var g = OpaqueOnly(golden, golden);
        using var c = OpaqueOnly(candidate, golden);
        var result = ImageDiff.Compare(g, c);
        int worstAlpha = 0;
        for (int y = 0; y < golden.Height; y++)
            for (int x = 0; x < golden.Width; x++)
                worstAlpha = Math.Max(worstAlpha, Math.Abs(golden.GetPixel(x, y).Alpha - candidate.GetPixel(x, y).Alpha));
        Assert.True(worstAlpha <= 1, $"{name}: RGB shift must never change alpha (worst alpha delta {worstAlpha})");
        Report(name, golden, candidate, result, heatmapPath);
    }

    /// <summary>
    /// The unchanged rule on any rendered image: zero interior differences and edge deltas of 3 or less
    /// (<see cref="ImageDiff.Compare"/> with its default tolerance). The export gates reach it through
    /// <see cref="AssertMatches"/>; the view gate (GoldenViewTests) calls it with Renderer.RenderViewImage.
    /// </summary>
    public static void AssertImageMatches(string name, string goldenPath, SKImage rendered, string heatmapPath)
    {
        using var golden = ImageDiff.Load(goldenPath);
        using var candidate = ImageDiff.FromImage(rendered);
        Report(name, golden, candidate, ImageDiff.Compare(golden, candidate), heatmapPath);
    }

    static void Report(string name, SKBitmap golden, SKBitmap candidate, DiffResult result, string heatmapPath)
    {
        if (!result.Pass)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(heatmapPath, data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }
```

The RGB-shift branch, the comparison, the heatmap and the assert message are the same statements in the same order. Only the plain branch moves into `AssertImageMatches`.

Create `src/RBX15.Engine.Tests/GoldenViewTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The view gate: Renderer.RenderView at device scale 1 meets 4.1's visible #mainCanvas (render(),
/// editor.js:300-429) under the unchanged golden rule, for every frozen view scene. tests/goldens/view
/// was captured once by tools/parity-ref/view-ref.js (M3a Task 2) and is never edited. Each scene
/// is an app scene plus a "view" object (zoom, mask, grid); the base document comes from SceneLoader,
/// exactly as the export gates build it. A failing scene writes diff-heatmaps/view/&lt;name&gt;.png.
/// </summary>
public class GoldenViewTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "view");

    sealed record ViewScene(string Name, JsonElement Raw, double Zoom, bool Mask, bool Grid);

    static readonly Lazy<IReadOnlyList<ViewScene>> Scenes = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e =>
        {
            var v = e.GetProperty("view");
            bool Flag(string k, bool fallback) => v.TryGetProperty(k, out var b) ? b.GetBoolean() : fallback;
            return new ViewScene(e.GetProperty("name").GetString()!, e.Clone(), v.GetProperty("zoom").GetDouble(), Flag("mask", true), Flag("grid", false));
        }).ToList();
    });

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    static Document DocumentFor(ViewScene s) =>
        SceneLoader.Parse(s.Raw, Root).Document with { ShowMask = s.Mask, ShowGrid = s.Grid };

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_view_matches_the_frozen_4_1_view(string name)
    {
        var scene = Scenes.Value.Single(s => s.Name == name);
        var doc = DocumentFor(scene);
        using var rendered = Renderer.RenderViewImage(doc, new ViewOptions(scene.Zoom, 1, doc.ShowGrid));
        GoldenRule.AssertImageMatches(name, Path.Combine(Dir, name + ".png"), rendered, Path.Combine(Root, "diff-heatmaps", "view", name + ".png"));
    }

    [Fact] // contract: zoom 0.25/0.5/1/1.5/3, mask on and off, grid in Shirt and in Pants, no selection
    public void The_frozen_set_covers_the_contracted_views_and_matches_its_index()
    {
        var scenes = Scenes.Value;
        Assert.Equal(14, scenes.Count);
        Assert.Equal(new[] { 0.25, 0.5, 1, 1.5, 3 }, scenes.Select(s => s.Zoom).Distinct().Order());
        Assert.Contains(scenes, s => !s.Mask);
        Assert.Contains(scenes, s => s.Grid && SceneLoader.Parse(s.Raw, Root).Document.Mode == TemplateMode.Shirt);
        Assert.Contains(scenes, s => s.Grid && SceneLoader.Parse(s.Raw, Root).Document.Mode == TemplateMode.Pants);
        // The unchanged rule needs scenes without RGB shift (rgb-shift-compute has its own rule).
        Assert.All(scenes, s => Assert.DoesNotContain(SceneLoader.Parse(s.Raw, Root).Document.Layers, l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "index.json")));
        var entries = index.RootElement.GetProperty("scenes").EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!);
        Assert.Equal(scenes.Select(s => s.Name).Order(StringComparer.Ordinal), entries.Keys.Order(StringComparer.Ordinal));
        foreach (var s in scenes)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Dir, s.Name + ".png"));
            Assert.Equal(entries[s.Name].GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var size = Renderer.ViewSize(new ViewOptions(s.Zoom));
            Assert.Equal((size.Width, size.Height), (entries[s.Name].GetProperty("width").GetInt32(), entries[s.Name].GetProperty("height").GetInt32()));
        }
        Assert.Equal(scenes.Count + 2, Directory.EnumerateFiles(Dir).Count()); // the PNGs, scenes.json, index.json
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.GoldenViewTests"`
Expected: `Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15` for `RBX15.Engine.Tests.dll`.

If a scene fails, the heatmap is at `diff-heatmaps/view/<name>.png`. The engine is wrong; the golden, the rule and the test stay as they are (global constraint). Start from the measured table in this task's notes. A failure only at 300% with max delta 1 or 2 means a scene with gradient or rotated content was put at 300% (ruling R3). A failure at 25% or 50% on the right or bottom edge means `ViewSize` is not truncating, or the composite is not stretched to the whole frame (ruling R1).

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.GoldenRenderTests|FullyQualifiedName~RBX15.Engine.Tests.GoldenAppTests|FullyQualifiedName~RBX15.Engine.Tests.WordArtSwapTests"`
Expected: `Passed!  - Failed:     0, Passed:   145, Skipped:     0, Total:   145` (GoldenRenderTests 34, GoldenAppTests 88, WordArtSwapTests 23; the same as before the factoring).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine.Tests/GoldenRule.cs src/RBX15.Engine.Tests/GoldenViewTests.cs
git commit -m "test(engine): the view gate, RenderView meets 4.1's visible canvas under the unchanged rule"
```

- [ ] **Step 12: Final verification and a look**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror && dotnet test src/RBX15.slnx -c Release`
Expected: `Build succeeded.` with `0 Warning(s)`, then Engine.Tests `Passed: 637, Failed: 0, Skipped: 0`, Parity.Tests `Passed: 35, Failed: 0`, App.Tests `Passed: 180, Failed: 0`.

Run: `git status --short`
Expected: nothing. `diff-heatmaps/` and `.superpowers/` are git-ignored.

Launch the app (`dotnet run --project src/RBX15.App -c Release`) and look:

- the empty template is centered in the canvas area at 100% on the dark checker, and the pill at the bottom right reads `100%`;
- `+` eight times reaches `300%`, `+` greys out, and both scrollbars appear and reach every edge (`canvas-zoom-clipped-no-pan`);
- `-` eleven times from 300% reaches `25%`, and `-` greys out;
- on a monitor at 125% or more, the checker squares and grid stay sharp;
- a click on the canvas does nothing visible yet (Task 4 wires it to selection).

Report anything that differs; no commit in this step.

**Handed to Task 5** (ledger and divergences; this task changes neither):

- **Close with this evidence, where the row's own parityCheck is met:**
  - `canvas-element-render`: GoldenViewTests (14 scenes, unchanged rule).
  - `render-checkerboard`: `Checker_is_anchored_at_the_origin_and_never_scales_with_zoom`.
  - `render-grid-overlay`: `view-shirt-z100-grid`, which shows the 2px-AA lines.
  - `overlay-grid-checkbox`: `The_grid_is_the_same_in_shirt_and_pants_and_absent_without_ShowGrid` and the Shirt and Pants grid goldens. It is not in the export: `RenderExport` has no grid path.
  - The view half of `overlay-mask-checkbox`: `The_view_masks_by_the_document_flag_and_export_always_masks`. The toggle is Task 3's.
  - `zoom-controls` (label for all 12 levels): `Zoom_steps_by_a_quarter_from_25_to_300_percent_with_4_1_labels`. Its parityCheck also asks for 25%, 100% and 300% screenshots: GoldenViewTests at 0.25, 1 and 3.
  - `canvas-zoom-controls`: the same, plus `The_host_and_the_pill_follow_the_view_model`.
  - `canvas-zoom-clipped-no-pan`: `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits`, plus Step 12's look and smoke S8.
- **Left open for the M3b look sign-off** (Task 5's conservative ruling, critic C19): `render-zoom-canvas` (sizes and smoothing are pinned, but its divergence is reworded below and the scroll host gets a look check) and `cross-window-dpi-rendering` (the DPI tests and `The_app_is_per_monitor_dpi_v2` pin the mechanism; its parityCheck's "run at 100/125/150/200%" is a look).
- **Partial closes:**
  - `canvas-coord-map`: the mapping only. Its parityCheck injects mouse events, which is M3b, so it stays open or gets its check narrowed.
  - `drop-canvas`: the point mapping is pinned by `Dropped_files_run_the_command_with_the_template_point`; placement is Task 4.
- **New divergence rows** (Task 5 Step 7 adds both):
  - `zoom-pill-limits`. "4.1: the zoom buttons stay enabled at 25% and 300%, and a click there clamps to a no-op (editor.js:1490-1491). v5: each button is disabled at its limit (opacity .4, styles.css:414-416)." Closes on `Each_button_is_disabled_at_its_limit_and_comes_back`.
  - The device-resolution divergence is Task 5's row `canvas-device-pixel-render` (one row, not two; it stays open for the M3b look sign-off). This task's draft id `view-device-resolution` is not added.
- **Rewording** (Task 5 Step 7 applies it; the row stays open):
  - `render-zoom-canvas`. The divergence promises "exact scaled size", but v5 keeps 4.1's truncated frame size (ruling R1). Suggested text: "4.1: the canvas is reallocated and cleared on every render and clipped when it outgrows the area. v5: the frame keeps 4.1's truncated size (TW*zoom), renders into a reused bitmap, and sits in a scroll host with scrollbars to pan; zoom stays 25-300% in 0.25 steps."
  - Its parityCheck lists zoom 2 and 4. 4 is not a 4.1 zoom, so narrow the check to 0.25, 0.5, 1, 1.5 and 3 (the goldens).


**Interface additions adopted (Task 2).**

1. **Sheet §3.1 and the contract: `ViewSize` truncates** (contract amendment CA1 in the plan header). The sheet's comment `(ceil(585*Zoom*DeviceScale), ceil(559*Zoom*DeviceScale))` and the contract's "Frame size is `ceil(585*Zoom*DeviceScale) x ceil(559*Zoom*DeviceScale)`" read "truncated, as 4.1's `canvas.width = TW*zoom` (editor.js:340-341)" (ruling R1, measured). The signature is unchanged.
2. **Engine members beyond the sheet:**
   - `public double ViewOptions.Scale => Zoom * DeviceScale;`
   - `internal static int Renderer.CheckerHalf(double deviceScale)`, tested through the existing `InternalsVisibleTo RBX15.Engine.Tests`.
3. **App members beyond the sheet.** `TemplateCanvasHost` has these, the internal ones reached through Task 1's `InternalsVisibleTo`:
   - `public static readonly DependencyProperty ClickCommandProperty` and `public ICommand? ClickCommand` (ruling R5).
   - `internal WriteableBitmap? Frame { get; }`
   - `internal double DeviceScale { get; }`
   - `internal void ApplyDpi(double deviceScale)`
   - `internal bool HandleDrop(IDataObject data, Point inHost)`
   - `internal bool HandleClick(Point inHost)` and `internal static bool IsClick(Point down, Point up)`.

   `CanvasViewModel` has `public Action<Point>? ClickHandler { get; set; }` and the generated `IRelayCommand<Point> ClickCommand`.
4. **Task 1 theme keys.** `CanvasView.xaml` uses these keys through `DynamicResource`. The naming is one rule: a CSS custom property `--ink-950` becomes `Ink950Brush` (a `SolidColorBrush`), and a font token `--font-mono` becomes `FontMono` (a `FontFamily`).

   | Key | CSS source |
   | --- | --- |
   | `Ink950Brush` | `--ink-950` (`styles.css:39`) |
   | `Border1Brush` | `--border-1`, rgba(255,255,255,.08) (`:62`) |
   | `Fg1Brush` | `--fg-1` (`:42`) |
   | `Fg3Brush` | `--fg-3` (`:44`) |
   | `FontBody` | `--font-body` (`:90`) |
   | `FontMono` | `--font-mono` (`:91`) |
   | `FocusRing` | the `:focus-visible` ring, `outline: 2px solid var(--brand-cyan); outline-offset: 2px` (`:357-367`); Task 1's `FocusVisualStyle` (the draft's `FocusRingStyle` was renamed to Task 1's key, critic C6) |

   `Every_theme_key_the_view_uses_is_defined_in_the_theme` catches a mismatch, and Task 5's `ThemeKeyGuardTests` checks every view.
5. **Task 1 anchors this task edits:** the `CanvasArea` grid in `MainWindow.xaml` (it gains the `CanvasView` child), and the two anchor lines in `Composition.cs` and `TestShell.cs` (the `canvas` local and `Canvas = canvas,`). Tasks 3 and 4 add their lines at the same anchors.
6. **`StaThread.Pump()`** (Task 1) replaces this task's local dispatcher pump. A parsed view's bindings attach at `DispatcherPriority.DataBind`, so within one `StaThread.Run` call they stay `Unattached` until the queue runs (measured). WPF test projects get no implicit `using System.IO;`, so test files that touch files add it.
7. **`app.manifest` is Task 1's** (per-monitor DPI v2, critic C1); this task only pins it. Under PMv2, Task 1's saved window placement and `PlacementGuard` work areas are DIPs on the window's own monitor, which is what `Window.Left/Top/Width/Height` already use.
8. **Test files beyond the sheet's list:** `src/RBX15.App.Tests/CanvasViewTests.cs` (5 facts: bindings, scrolling and centering, theme keys, manifest, `CanvasArea` child).
9. **`GoldenRule` gains a private `Report(name, golden, candidate, result, heatmapPath)`**, shared by `AssertMatches` and `AssertImageMatches`. The thresholds, the RGB-shift rule and `ImageDiff` are unchanged.
10. **Deselect (interfaces §9 item 3) is adopted** (critic C13): this task builds the click seam (ruling R5), and Task 4 Step 21 wires `ClickHandler` to `LayerOps.HitTest`.

---

### Task 3: The Home tab (Mode, Fill Style, Template Regions, Settings, Edit)

**Files:**

- Create (engine): `src/RBX15.Engine/DocumentOps.cs`, `src/RBX15.Engine.Tests/DocumentOpsTests.cs`
- Create (app): `src/RBX15.App/Services/IColorPicker.cs`, `src/RBX15.App/Services/Win32ColorPicker.cs`, `src/RBX15.App/ViewModels/HomeViewModel.cs`, `ModeGroupViewModel.cs`, `RegionsGroupViewModel.cs`, `RegionTileViewModel.cs`, `RegionSwatch.cs`, `FillStyleViewModel.cs` (with `FillTypeOption`), `SettingsGroupViewModel.cs`, `EditGroupViewModel.cs` (all in `src/RBX15.App/ViewModels/`), `src/RBX15.App/Views/HomeStyles.xaml`, `HomeRibbon.xaml(.cs)`, `FillStyleGroup.xaml(.cs)`, `RegionsGrid.xaml(.cs)` (all in `src/RBX15.App/Views/`). No converter of its own: the views use Task 1's app-level `UpperCase` (critic C7).
- Create (tests): `src/RBX15.App.Tests/HomeTestKit.cs`, `Fakes/FakeColorPicker.cs`, `ModeGroupTests.cs`, `RegionsGroupTests.cs`, `FillStyleTests.cs`, `SettingsGroupTests.cs`, `EditGroupTests.cs`, `HomeRibbonViewTests.cs` (all in `src/RBX15.App.Tests/`)
- Modify: `src/RBX15.App/ViewModels/MainViewModel.cs` (adds `required Home`), `src/RBX15.App/Composition.cs` and `src/RBX15.App.Tests/TestShell.cs` (construct it, at Task 1's anchors), and `src/RBX15.App/MainWindow.xaml` (`HomeRibbon` inside Task 1's `RibbonPane`). `RBX15.App.csproj` is not touched: Task 1 adds `InternalsVisibleTo`. See "Interface additions adopted (Task 3)" at the end of this task.
- Read only: `editor.html:29-110, 145-171`, `editor.js:1394, 1404-1495, 1547-1725`, `styles.css:8-92, 197-221, 375-419, 474-532`, `lib/fills.js:167-192`

**Interfaces:**

- Consumes:
  - Task 1: `EditorSession` (`Document`, `SelectedLayer`, `TemplateTitle`, `UndoCount`, `RedoCount`, `CanUndo`, `CanRedo`, `IsDirty`, `Apply`, `BeginGesture`, `Undo`, `Redo`, `Select`, `Changed`), `SessionChange`, `SessionChangedEventArgs`, `Document.ContentEquals` (behind `Apply`), `MainViewModel`, `StaThread.Run`, `StaThread.Pump`, the Themes resource keys listed under the additions (including `RibbonGroup`, `RibbonGroupLabel`, `UpperCase` and `RXsRadius`), `Composition.Build` and `TestShell.Main` (their anchors), and `RibbonPane` in `MainWindow.xaml`.
  - Engine (main `1126e7c`): `Document`, `Layer`, `Fill` (`Key`, `Solid`, record equality), `Fills.PaintRegionFill(SKCanvas, SKRect, Fill, SKRect?)`, `Template.Slot/TryFind/Regions`, `Region`, `TemplateMode`, `BodyPart`, `Face`, `TemplateSize`, `CssColor.HexToRgb`.
- Produces (names verbatim from interfaces.md §3.2, §4 and §6):
  - `public static class DocumentOps` with `RegionFitFraction`, `SwitchMode`, `ToggleMode`, `RegionFill`, `PaintRegion`, `ClearRegion`, `RetargetFill`, `FitLayerToRegion`, `ClearLayers`. Each returns its input instance when it changes nothing.
  - `public interface IColorPicker { string? Pick(string initialHex); }` and `public sealed class Win32ColorPicker(Func<IntPtr> ownerHandle) : IColorPicker` (`RBX15.App.Services`).
  - `HomeViewModel(EditorSession session, IColorPicker colorPicker)` with `Mode`, `Regions`, `FillStyle`, `Settings`, `Edit`. Task 4 adds `required File`.
  - `ModeGroupViewModel(EditorSession)`: `TemplateMode Mode`, `string Label`, `IRelayCommand ToggleCommand`.
  - `RegionsGroupViewModel(EditorSession, FillStyleViewModel)`: `BodyLabel`, `LimbLabel1`, `LimbLabel2`, `IReadOnlyList<RegionTileViewModel> Body, Limb1, Limb2`.
  - `RegionTileViewModel`: `Region`, `Label`, `AutomationName`, `IsFront`, `Fill? Fill`, `ImageSource? Swatch`, `PaintCommand`, `ClearCommand`, plus `bool IsFilled` (an addition, listed at the end).
  - `FillStyleViewModel(EditorSession, IColorPicker)`: `Fill Current`, `static Fill DefaultFill`, `Types`, `Type`, `Color1`, `Color2`, `Angle`, `Scale`, `SetTypeCommand`, `PickColor1Command`, `PickColor2Command`, `IDisposable BeginSliderDrag()`, and the constants `MinAngle`, `MaxAngle`, `MinScale`, `MaxScale`. `public sealed record FillTypeOption(string Type, string Label)`.
  - `SettingsGroupViewModel(EditorSession)`: `TemplateName`, `ShowMask`, `ShowGrid`.
  - `EditGroupViewModel(EditorSession)`: `UndoCommand`, `RedoCommand`, `ClearCommand`.
  - `MainViewModel.Home` (`public required HomeViewModel Home { get; init; }`).
  - Test helpers: `HomeTestKit` (`Solid`, `LayerOf`, `SessionWith`, `Home`, `Tile`, `Track`) and `Fakes.FakeColorPicker` (`Answer`, `Seeds`).

**What 4.1 does (cited).** Every line below is from the shipped `editor.js`, `editor.html` and `styles.css` on `main`.

- **Mode** (`editor.html:31-40`, `editor.js:1692-1725`). The button reads `SHIRT` or `PANTS`, styled `13px/700`, `padding 6x18`, `min-width 80`, radius 6, text `#050c18`, with a `135deg` gradient from `#17d4fa` to `#f22f89`, reversed in Pants (`:1703`, `:1707`). A click runs `saveUndo()` and then `switchMode(opposite)` (`:1723-1724`). switchMode sets the mode, empties `regionColors` (`:1711`), rebuilds the mask and the region grid, and renders. Layers, selection, background, mask, grid and `currentFill` are untouched.
- **Template Regions** (`buildRegionGrid`, `editor.js:1615-1687`). The body cross is a 5-column grid with Top, R, Front, L and Back (`:1626-1628`), and Front carries an inline `#2a1040` (`:1627`). The limbs sit under the headings `Right Arm`/`Left Arm` (`9px #666`) and run `L, Bk, R, Fr` for the right limb and `Fr, L, Bk, R` for the left (`:1630-1648`). Pants swaps the prefixes to `waist`, `rleg` and `lleg`, and the headings to `Leg` (`:1616-1622`). Limb up and down faces have no tile.
  - **Click** (`:1657-1675`). With a layer selected, the layer is fitted at `min(r.w/iw, r.h/ih) * 0.85`, centered, rotation 0. Otherwise the region is painted with `{...currentFill}`. Both push undo, even when nothing changes.
  - **Right-click** (`:1677-1684`). It clears the region's fill, and does nothing (and pushes nothing) when the region has none.
  - **Swatches** (`paintRegionButton`, `:1556-1564`). They are CSS from `fillToCSS` (`lib/fills.js:167-185`), which uses the CSS angle convention. Clearing a swatch sets `style.background = ''`, which wipes Front's tint for good.
  - **Tile style** (`styles.css:475-496`). Mono `9px` uppercase, navy, a 1px `border-1`, `fg-3` text and radius 4. Hover turns the border and text cyan. `.filled` has a magenta border and `fg-1` text.
- **Fill Style** (`editor.html:145-167`, `editor.js:1550-1613`). The default is `currentFill = {solid, #17d4fa, #f22f89, 45, 16}` (`:1552`). There are 7 type buttons in 4 columns (`editor.html:147-155`), and an active button is cyan (`styles.css:528-532`). Color 1 and Color 2 are `26x22` (`:157-158`). `Ang` is a `0..360` range and `Scale` a `4..48` range, both 44px wide (`:159-162`). The hint reads `Set a style, then click a region to paint it. Right-click clears.` (`:164`).
  - **applyFillEdit** (`:1581-1598`). Every region whose `fillKey` equals the pre-edit key takes the new fill.
  - **Undo.** A snapshot is pushed on a click or on `pointerdown`, and only when a region shares the key (`:1570-1579`, `:1611`). A keyboard change records nothing.
- **Settings** (`editor.html:98-107`). `#exportTitle` shows the hint text `Template name...` and the style `#1a1a2e`, `1px #333`, `#e0e0e0`, `11px`, `140px`. Mask is checked by default and Grid is unchecked. The toggles push no undo (`editor.js:1494-1495`), but undo restores them (`:1433-1436`).
- **Edit** (`editor.html:77-84`). Undo has the tooltip `Undo (Ctrl+Z)` and Redo `Redo (Ctrl+Y / Ctrl+Shift+Z)`. Both are disabled while their stack is empty (`editor.js:1470-1475`, `styles.css:414-419` opacity .4). Clear runs `saveUndo(); layers = []; selectedLayerId = null` (`:1394`), with no confirm.

**Rulings made here** (each pinned by a named test):

1. **Open question 5 (fill controls after undo): one-way, always in agreement.** `FillStyleViewModel.Current` is the only fill state, and every control binds to it, so the active type and the sliders can never disagree with the current fill. That is the `fillstyle-type-buttons` fix. Undo, redo and region clicks never change `Current` (`fillstyle-ui-not-synced-from-region`, keep). Pinned by `Undo_of_a_live_edit_restores_regions_and_leaves_the_controls`.
2. **Where Fill Style and Template Regions sit in M3a.** The Layout tab is hidden in M3a (contract), so both groups go on Home, after Settings, in 4.1's Layout order. The order is Mode, Edit, [File: Task 4], Settings, Fill Style, Template Regions. Tools, Brush and Placement are absent. Pinned by `Mode_settings_and_edit_bind_and_hidden_groups_are_absent`. Task 5 should record this as a temporary divergence that M4 retires when the Layout tab returns.
3. **Swatch geometry.** A swatch is the region's own rectangle, painted at template scale by `Fills.PaintRegionFill` with the region as its own extent, then scaled into the tile (`UniformToFill`). Checker, dots and camo cells are the canvas's own cells, and a gradient shows its full c1-to-c2 run in the canvas angle convention. Pinned by `Swatch_is_the_canvas_painter_cropped_to_the_region` (7 types) and `A_zero_degree_gradient_swatch_runs_left_to_right`.
4. **Clear stays enabled and never confirms** (`btn-clear`, keep). With no layers it records nothing (`undo-empty-steps`). Pinned by `Clear_with_no_layers_records_nothing_and_stays_enabled`.
5. **Keyboard clear on a tile.** Delete, Shift+F10 and the Menu key run `ClearCommand`, as right-click does. Shift+F10 and Menu are the keyboard forms of Chromium's `contextmenu` event, and Delete is the obvious key. This is a new affordance for spec §4 accessibility. Pinned by `A_tile_clears_on_right_click_and_from_the_keyboard`.
6. **Undo and Redo keep 4.1's tooltips** (`Undo (Ctrl+Z)` and so on), although the shortcuts are M4's (contract: keyboard is M4). The tooltip text is the 4.1 string, and M4 makes it true. Kept as drafted: a tooltip that names a shortcut M4 ships is not a dead button, and nothing else depends on the text.
7. **Mask and Grid toggles are undo steps**, one each (interfaces.md §1, `file-undo-snapshot-scope` fix). Undo restores the checkboxes, as 4.1 did (`editor.js:1433-1436`).
8. **Slider values are whole numbers, clamped to the range's bounds.** `Math.Round` uses `AwayFromZero`, the rounding a step-1 range applies, and NaN throws. Pinned by `Angle_is_a_whole_number_in_0_to_360` and `Scale_is_a_whole_number_in_4_to_48`.

**Measured while drafting.** Every code block below was compiled and run on a scratch copy of `main` at `1126e7c` (.NET SDK 10.0.302, SkiaSharp 4.153.1, CommunityToolkit.Mvvm 8.4.2 from the local cache). Task 1's types were stood in by an `EditorSession` written to interfaces.md §2, a `StaThread`, `Document.ContentEquals` and an `RBX15.App.Tests` project, all per the interface sheet. There: `dotnet build src/RBX15.slnx -c Release -warnaserror` gave 0 warnings and 0 errors; Engine.Tests 566 + 21 = 587, App.Tests 0 + 55, Parity.Tests 35, three App.Tests runs in a row green.

**Counts.** Task 2 leaves Engine.Tests 637, App.Tests 180 and Parity.Tests 35, with 0 skipped. This task adds **Engine.Tests +21, App.Tests +55, Parity.Tests +0**: DocumentOpsTests 21; ModeGroupTests 3, SettingsGroupTests 4, EditGroupTests 4, FillStyleTests 22, RegionsGroupTests 17 and HomeRibbonViewTests 5. It ends at Engine.Tests 658 and App.Tests 235.

**Reconciler changes since the measured run** (not compiled in it; the implementer's build at each step is their check): the shared `RibbonGroup`, `RibbonGroupLabel` and `UpperCase` now come from Task 1's `Controls.xaml` (HomeStyles no longer defines them), `RadiusXs` is Task 1's `RXsRadius`, view tests call `StaThread.Pump()`, the Edit group also refreshes Undo and Redo while a gesture changes the document (critic L1), and `HomeRibbon` sits inside Task 1's `RibbonPane` `ContentControl`. None of them changes a test count.

- [ ] **Step 1: Baseline**

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 637, Failed: 0, Skipped: 0`; App.Tests `Passed: 180, Failed: 0`; Parity.Tests `Passed: 35, Failed: 0`.

- [ ] **Step 2: Write the failing DocumentOps tests** (ledger `mode-switch-effects`, `mode-switch-undo-and-callers`, `tpl-active-regions-swap`, `regions-click-paint`, `regions-right-click-clear`, `fillstyle-live-edit-rule`, `regions-click-fit-layer`, `btn-clear`, `mode-pants-crash-hardcoded-keys`, `claim-mode-per-mode-region-data`)

Create `src/RBX15.Engine.Tests/DocumentOpsTests.cs`:

```csharp
using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// DocumentOps ports the Home tab's region and mode edits (editor.js:1394, :1581-1598, :1655-1725).
/// Every op returns its input instance when it changes nothing (interfaces sheet §3.2), which the
/// tests pin with Assert.Same.
/// </summary>
public class DocumentOpsTests
{
    static readonly Fill Cyan = Fill.Solid("#17d4fa");
    static readonly Fill Linear45 = new("linear", "#17d4fa", "#f22f89", 45, 16);
    static readonly Fill Linear90 = new("linear", "#17d4fa", "#f22f89", 90, 16);

    static Document WithFills(params (string Region, Fill Fill)[] fills) =>
        new() { RegionFills = fills.ToImmutableList() };

    static Layer LayerOf(int id, int w, int h) => new() { Id = id, Image = Px.Solid(w, h, SKColors.Red), Name = "L" + id };

    // ---- mode (mode-switch-effects, mode-switch-undo-and-callers, tpl-active-regions-swap) ----

    [Fact] // editor.js:1696-1716: mode set, fills emptied, layers/background/mask/grid kept
    public void ToggleMode_switches_and_empties_fills_and_keeps_everything_else()
    {
        var layer = LayerOf(1, 10, 10);
        var doc = WithFills(("torso_front", Cyan), ("rarm_left", Linear45)) with
        {
            Layers = ImmutableList.Create(layer),
            BgColor = "#ff0000",
            BgOpacity = 0.5,
            ShowMask = false,
            ShowGrid = true,
        };

        var pants = DocumentOps.ToggleMode(doc);

        Assert.Equal(TemplateMode.Pants, pants.Mode);
        Assert.Empty(pants.RegionFills);
        Assert.Same(layer, Assert.Single(pants.Layers));
        Assert.Equal(("#ff0000", 0.5, false, true), (pants.BgColor, pants.BgOpacity, pants.ShowMask, pants.ShowGrid));

        var back = DocumentOps.ToggleMode(pants);
        Assert.Equal(TemplateMode.Shirt, back.Mode);
        Assert.Empty(back.RegionFills); // claim-mode-per-mode-region-data: fills do not come back
    }

    [Fact] // switchMode to the current mode wipes fills like 4.1; with none it is a no-op
    public void SwitchMode_to_the_same_mode_wipes_fills_and_is_a_no_op_without_them()
    {
        var painted = WithFills(("torso_front", Cyan));
        Assert.Empty(DocumentOps.SwitchMode(painted, TemplateMode.Shirt).RegionFills);

        var blank = new Document();
        Assert.Same(blank, DocumentOps.SwitchMode(blank, TemplateMode.Shirt));
    }

    // ---- paint and clear (regions-click-paint, regions-right-click-clear, regions-state-model) ----

    [Fact] // editor.js:1671: a new key is appended
    public void PaintRegion_appends_a_new_region()
    {
        var doc = DocumentOps.PaintRegion(DocumentOps.PaintRegion(new Document(), "torso_front", Cyan), "torso_back", Linear45);
        Assert.Equal(new[] { ("torso_front", Cyan), ("torso_back", Linear45) }, doc.RegionFills);
        Assert.Same(Linear45, DocumentOps.RegionFill(doc, "torso_back"));
        Assert.Null(DocumentOps.RegionFill(doc, "torso_up"));
    }

    [Fact] // a JS object keeps a key's position on reassignment
    public void PaintRegion_replaces_an_existing_region_in_place()
    {
        var doc = WithFills(("torso_front", Cyan), ("torso_back", Cyan));
        var repainted = DocumentOps.PaintRegion(doc, "torso_front", Linear45);
        Assert.Equal(new[] { ("torso_front", Linear45), ("torso_back", Cyan) }, repainted.RegionFills);
    }

    [Fact] // regions-click-paint fix: an identical repaint is no change, so no undo step
    public void PaintRegion_with_an_equal_fill_returns_the_same_document()
    {
        var doc = WithFills(("torso_front", new Fill("solid", "#17d4fa")));
        Assert.Same(doc, DocumentOps.PaintRegion(doc, "torso_front", new Fill("solid", "#17d4fa")));
    }

    [Theory] // only the mode's 18 keys can be painted (Pants keys in Shirt mode and the reverse)
    [InlineData(TemplateMode.Shirt, "waist_front")]
    [InlineData(TemplateMode.Pants, "torso_front")]
    [InlineData(TemplateMode.Shirt, "")]
    public void PaintRegion_refuses_a_key_the_mode_lacks(TemplateMode mode, string region)
    {
        var doc = new Document { Mode = mode };
        Assert.Throws<ArgumentException>(() => DocumentOps.PaintRegion(doc, region, Cyan));
    }

    [Fact] // Pants keys paint in Pants mode (mode-pants-crash-hardcoded-keys)
    public void PaintRegion_paints_pants_keys_in_pants_mode()
    {
        var doc = DocumentOps.PaintRegion(new Document { Mode = TemplateMode.Pants }, "lleg_back", Cyan);
        Assert.Equal(("lleg_back", Cyan), Assert.Single(doc.RegionFills));
    }

    [Fact] // editor.js:1681: delete the key; the rest keep their order
    public void ClearRegion_removes_only_that_region()
    {
        var doc = WithFills(("torso_up", Cyan), ("torso_front", Linear45), ("torso_down", Cyan));
        var cleared = DocumentOps.ClearRegion(doc, "torso_front");
        Assert.Equal(new[] { ("torso_up", Cyan), ("torso_down", Cyan) }, cleared.RegionFills);
    }

    [Fact] // editor.js:1679: no fill, no change (and so no undo step)
    public void ClearRegion_of_an_unpainted_region_returns_the_same_document()
    {
        var doc = WithFills(("torso_up", Cyan));
        Assert.Same(doc, DocumentOps.ClearRegion(doc, "torso_front"));
    }

    // ---- live edit (fillstyle-live-edit-rule, keep) ----

    [Fact] // the row's scenario: A,B linear 45, C solid; angle 90 moves A and B only
    public void RetargetFill_moves_every_region_with_the_before_key_and_no_other()
    {
        var doc = WithFills(("torso_front", Linear45), ("torso_back", Linear45), ("torso_up", Cyan));
        var next = DocumentOps.RetargetFill(doc, Linear45.Key, Linear90);
        Assert.Equal(new[] { ("torso_front", Linear90), ("torso_back", Linear90), ("torso_up", Cyan) }, next.RegionFills);
    }

    [Fact] // matching is by key, so an equal-valued descriptor painted separately follows too
    public void RetargetFill_matches_by_key_not_by_reference()
    {
        var doc = WithFills(("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45, 16)));
        var next = DocumentOps.RetargetFill(doc, Linear45.Key, Linear90);
        Assert.Equal(Linear90, DocumentOps.RegionFill(next, "torso_front"));
    }

    [Fact] // editor.js:1591: nothing matched, nothing changes (idle edits make no step)
    public void RetargetFill_with_no_match_returns_the_same_document()
    {
        var doc = WithFills(("torso_front", Cyan));
        Assert.Same(doc, DocumentOps.RetargetFill(doc, Linear45.Key, Linear90));
    }

    [Fact] // a key the mode lacks (from a 4.1 file) is retargeted too, like 4.1's for-in loop
    public void RetargetFill_includes_keys_the_mode_lacks()
    {
        var doc = WithFills(("waist_front", Linear45));
        Assert.Equal(Linear90, DocumentOps.RegionFill(DocumentOps.RetargetFill(doc, Linear45.Key, Linear90), "waist_front"));
    }

    // ---- fit (regions-click-fit-layer) ----

    [Fact] // the row's own numbers: 200x100 into torso_front 128x128
    public void FitLayerToRegion_matches_the_ledger_numbers()
    {
        var layer = LayerOf(7, 200, 100) with { Rotation = 30, FlipH = true, Opacity = 0.5 };
        var doc = new Document { Layers = ImmutableList.Create(LayerOf(1, 5, 5), layer) };

        var fitted = Assert.Single(DocumentOps.FitLayerToRegion(doc, 7, "torso_front").Layers, l => l.Id == 7);

        Assert.Equal(0.544, fitted.Scale, 12);
        Assert.Equal(240.6, fitted.X, 12);
        Assert.Equal(110.8, fitted.Y, 12);
        Assert.Equal(0, fitted.Rotation);
        Assert.True(fitted.FlipH);
        Assert.Equal(0.5, fitted.Opacity);
    }

    [Fact] // Pants mode fits into waist_* with the same geometry (mode-pants-crash-hardcoded-keys)
    public void FitLayerToRegion_works_in_pants_mode()
    {
        var doc = new Document { Mode = TemplateMode.Pants, Layers = ImmutableList.Create(LayerOf(1, 64, 128)) };
        var fitted = DocumentOps.FitLayerToRegion(doc, 1, "rleg_front").Layers[0];
        // rleg_front is 217,355 64x128: scale min(1,1)*0.85, centered
        Assert.Equal((0.85, 217 + (64 - 54.4) / 2, 355 + (128 - 108.8) / 2), (fitted.Scale, fitted.X, fitted.Y));
    }

    [Fact] // a missing layer, or a second identical fit, changes nothing
    public void FitLayerToRegion_no_ops_return_the_same_document()
    {
        var doc = new Document { Layers = ImmutableList.Create(LayerOf(1, 200, 100)) };
        Assert.Same(doc, DocumentOps.FitLayerToRegion(doc, 99, "torso_front"));
        var once = DocumentOps.FitLayerToRegion(doc, 1, "torso_front");
        Assert.Same(once, DocumentOps.FitLayerToRegion(once, 1, "torso_front"));
    }

    [Fact]
    public void FitLayerToRegion_refuses_a_key_the_mode_lacks()
    {
        var doc = new Document { Layers = ImmutableList.Create(LayerOf(1, 10, 10)) };
        Assert.Throws<ArgumentException>(() => DocumentOps.FitLayerToRegion(doc, 1, "waist_front"));
    }

    // ---- clear (btn-clear, keep) ----

    [Fact] // editor.js:1394: layers go; fills, background, mode, mask and grid stay
    public void ClearLayers_removes_layers_only()
    {
        var doc = WithFills(("torso_front", Cyan)) with
        {
            Mode = TemplateMode.Shirt,
            BgColor = "#00ff00",
            ShowGrid = true,
            Layers = ImmutableList.Create(LayerOf(1, 4, 4), LayerOf(2, 4, 4)),
        };
        var cleared = DocumentOps.ClearLayers(doc);
        Assert.Empty(cleared.Layers);
        Assert.Equal(doc.RegionFills, cleared.RegionFills);
        Assert.Equal(("#00ff00", true), (cleared.BgColor, cleared.ShowGrid));

        Assert.Same(cleared, DocumentOps.ClearLayers(cleared));
    }

    [Fact]
    public void RegionFitFraction_is_4_1s_constant() => Assert.Equal(0.85, DocumentOps.RegionFitFraction);
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0103: The name 'DocumentOps' does not exist in the current context` in `DocumentOpsTests.cs`.

- [ ] **Step 3: Implement DocumentOps**

Create `src/RBX15.Engine/DocumentOps.cs`:

```csharp
namespace RBX15.Engine;

/// <summary>
/// The Home tab's document edits: mode switch, region paint and clear, the fill-style live-edit
/// retarget, region fit and Clear. Pure functions over the immutable <see cref="Document"/>. Each one
/// returns the input instance itself when it changes nothing, so a caller can tell a no-op by
/// reference (EditorSession also compares content; spec §5, no empty undo steps, ledger
/// <c>undo-empty-steps</c>).
/// </summary>
public static class DocumentOps
{
    /// <summary>The region-click fit fraction (editor.js:1663). Meme and Fit use 0.9 and Allover 0.8; each keeps its own.</summary>
    public const double RegionFitFraction = 0.85;

    /// <summary>
    /// switchMode (editor.js:1695-1717): sets the mode and empties every region fill, because the two
    /// modes use different keys (editor.js:1710-1711). Layers, background, mask and grid are kept
    /// (ledger <c>mode-switch-effects</c>). Switching to the current mode still empties the fills, as
    /// 4.1's switchMode did; with no fills it changes nothing.
    /// </summary>
    public static Document SwitchMode(Document doc, TemplateMode mode)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (doc.Mode == mode && doc.RegionFills.IsEmpty) return doc;
        return doc with { Mode = mode, RegionFills = doc.RegionFills.Clear() };
    }

    /// <summary>modeBtn.onclick (editor.js:1719-1725): the opposite mode. One undo step with its fill wipe (<c>mode-switch-undo-and-callers</c>).</summary>
    public static Document ToggleMode(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return SwitchMode(doc, doc.Mode == TemplateMode.Shirt ? TemplateMode.Pants : TemplateMode.Shirt);
    }

    /// <summary><c>regionColors[r]</c>: the region's fill, or null when it has none.</summary>
    public static Fill? RegionFill(Document doc, string region)
    {
        ArgumentNullException.ThrowIfNull(doc);
        foreach (var (name, fill) in doc.RegionFills)
            if (name == region) return fill;
        return null;
    }

    /// <summary>
    /// The region click with no layer selected (editor.js:1668-1674, ledger <c>regions-click-paint</c>):
    /// stores <paramref name="fill"/> under the region. An existing entry is replaced where it stands,
    /// as a JS object keeps a key's position on assignment; a new key is appended. Repainting with an
    /// equal fill changes nothing (the row's fix: 4.1 pushed an undo step anyway).
    /// </summary>
    /// <exception cref="ArgumentException">The region is not one of the document mode's 18 keys.</exception>
    public static Document PaintRegion(Document doc, string region, Fill fill)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(fill);
        RequireRegion(doc.Mode, region);
        int at = IndexOf(doc, region);
        if (at < 0) return doc with { RegionFills = doc.RegionFills.Add((region, fill)) };
        if (doc.RegionFills[at].Fill == fill) return doc;
        return doc with { RegionFills = doc.RegionFills.SetItem(at, (region, fill)) };
    }

    /// <summary>
    /// The region right-click (editor.js:1677-1684, ledger <c>regions-right-click-clear</c>): deletes the
    /// region's fill. A region without a fill changes nothing (4.1 returned before saveUndo, :1679).
    /// Any key may be cleared, including a key the mode lacks.
    /// </summary>
    public static Document ClearRegion(Document doc, string region)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int at = IndexOf(doc, region);
        return at < 0 ? doc : doc with { RegionFills = doc.RegionFills.RemoveAt(at) };
    }

    /// <summary>
    /// applyFillEdit (editor.js:1581-1598, ledger <c>fillstyle-live-edit-rule</c>, keep): every region
    /// whose fill key equals <paramref name="beforeKey"/> (the current fill's key before the edit) takes
    /// <paramref name="next"/>, in place. Matching is by <see cref="Fill.Key"/>, so two groups whose keys
    /// an edit makes equal move together from then on, as in 4.1. Every entry is considered, including
    /// keys the mode lacks (4.1 looped over all of regionColors, :1585).
    /// </summary>
    public static Document RetargetFill(Document doc, string beforeKey, Fill next)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(beforeKey);
        ArgumentNullException.ThrowIfNull(next);
        var fills = doc.RegionFills;
        for (int i = 0; i < fills.Count; i++)
        {
            var (name, fill) = fills[i];
            if (fill.Key == beforeKey && fill != next) fills = fills.SetItem(i, (name, next));
        }
        return ReferenceEquals(fills, doc.RegionFills) ? doc : doc with { RegionFills = fills };
    }

    /// <summary>
    /// The region click with a layer selected (editor.js:1658-1667, ledger <c>regions-click-fit-layer</c>):
    /// scale = min(r.w / iw, r.h / ih) * 0.85, centered in the region, rotation 0. Flips, opacity and
    /// adjustments are kept. A missing layer, or a fit that lands where the layer already is, changes
    /// nothing.
    /// </summary>
    /// <exception cref="ArgumentException">The region is not one of the document mode's 18 keys.</exception>
    public static Document FitLayerToRegion(Document doc, int layerId, string region)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var r = RequireRegion(doc.Mode, region);
        int at = doc.Layers.FindIndex(l => l.Id == layerId);
        if (at < 0) return doc;
        var layer = doc.Layers[at];
        double iw = layer.Image.Width, ih = layer.Image.Height;
        double scale = Math.Min(r.W / iw, r.H / ih) * RegionFitFraction;
        var fitted = layer with
        {
            Scale = scale,
            X = r.X + (r.W - iw * scale) / 2,
            Y = r.Y + (r.H - ih * scale) / 2,
            Rotation = 0,
        };
        return fitted == layer ? doc : doc with { Layers = doc.Layers.SetItem(at, fitted) };
    }

    /// <summary>
    /// btnClear (editor.js:1394, ledger <c>btn-clear</c>, keep): removes every layer. Fills, background,
    /// mode, mask and grid stay. With no layers it changes nothing.
    /// </summary>
    public static Document ClearLayers(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return doc.Layers.IsEmpty ? doc : doc with { Layers = doc.Layers.Clear() };
    }

    static int IndexOf(Document doc, string region) => doc.RegionFills.FindIndex(e => e.Region == region);

    static Region RequireRegion(TemplateMode mode, string region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Template.TryFind(mode, region, out var r)
            ? r
            : throw new ArgumentException($"'{region}' is not a {mode} region.", nameof(region));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.DocumentOpsTests"`
Expected: `Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 658, Failed: 0, Skipped: 0`; App.Tests `Passed: 180`; Parity.Tests `Passed: 35`.

- [ ] **Step 4: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/DocumentOps.cs src/RBX15.Engine.Tests/DocumentOpsTests.cs
git commit -m "feat(engine): DocumentOps for mode switch, region paint and clear, live fill retarget, region fit and Clear"
```

- [ ] **Step 5: Write the failing Mode tests** (ledger `home-mode-toggle`, `mode-toggle-button`, `mode-switch-undo-and-callers`, `restore-mode-switch-side-effects`)

Create `src/RBX15.App.Tests/HomeTestKit.cs`. Steps 9 and 11 add one member each.

```csharp
using System.Collections.Immutable;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>Shared builders for the Home tab tests (Task 3).</summary>
internal static class HomeTestKit
{
    public static SKImage Solid(int w, int h, SKColor c)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(c);
        return surface.Snapshot();
    }

    public static Layer LayerOf(int id, int w = 200, int h = 100) =>
        new() { Id = id, Image = Solid(w, h, SKColors.Red), Name = "Image " + id };

    /// <summary>A session whose document holds the given layers (no history, nothing selected).</summary>
    public static EditorSession SessionWith(params Layer[] layers) =>
        new(new Document { Layers = layers.ToImmutableList() });

    /// <summary>Records every PropertyChanged name raised by <paramref name="source"/>.</summary>
    public static List<string?> Track(System.ComponentModel.INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }
}
```

Create `src/RBX15.App.Tests/ModeGroupTests.cs`:

```csharp
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The Mode button (editor.js:1692-1725; ledger home-mode-toggle, mode-switch-undo-and-callers).</summary>
public class ModeGroupTests
{
    [Fact] // editor.html:33, editor.js:1702-1706
    public void Label_reads_SHIRT_then_PANTS()
    {
        var session = new EditorSession();
        var mode = new ModeGroupViewModel(session);
        Assert.Equal((TemplateMode.Shirt, "SHIRT"), (mode.Mode, mode.Label));

        var raised = Track(mode);
        mode.ToggleCommand.Execute(null);

        Assert.Equal((TemplateMode.Pants, "PANTS"), (mode.Mode, mode.Label));
        Assert.Equal(new[] { nameof(ModeGroupViewModel.Mode), nameof(ModeGroupViewModel.Label) }, raised);
    }

    [Fact] // one click = one step: the mode and the fill wipe together (editor.js:1723)
    public void Toggle_is_one_step_that_wipes_fills_and_keeps_layers()
    {
        var session = SessionWith(LayerOf(1));
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#17d4fa")));
        var mode = new ModeGroupViewModel(session);

        mode.ToggleCommand.Execute(null);

        Assert.Equal(2, session.UndoCount);
        Assert.Equal(TemplateMode.Pants, session.Document.Mode);
        Assert.Empty(session.Document.RegionFills);
        Assert.Single(session.Document.Layers);
    }

    [Fact] // the ledger's script: paint, toggle to Pants, undo -> Shirt with fills; redo -> Pants, empty
    public void Undo_and_redo_of_a_toggle_restore_mode_label_and_fills()
    {
        var session = new EditorSession();
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#17d4fa")));
        var mode = new ModeGroupViewModel(session);
        mode.ToggleCommand.Execute(null);

        Assert.True(session.Undo());
        Assert.Equal("SHIRT", mode.Label);
        Assert.Equal(("torso_front", Fill.Solid("#17d4fa")), Assert.Single(session.Document.RegionFills));

        Assert.True(session.Redo());
        Assert.Equal("PANTS", mode.Label);
        Assert.Empty(session.Document.RegionFills);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'ModeGroupViewModel' could not be found` in `ModeGroupTests.cs`.

- [ ] **Step 6: Implement ModeGroupViewModel**

Create `src/RBX15.App/ViewModels/ModeGroupViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Mode group's SHIRT/PANTS button (editor.html:31-40, editor.js:1692-1725; ledger
/// <c>home-mode-toggle</c>, <c>mode-toggle-button</c>). One click is one undo step that switches the
/// mode and wipes every region fill (<c>mode-switch-undo-and-callers</c>, keep). The label follows the
/// document, so undo and redo flip it back (editor.js:1427).
/// </summary>
public sealed class ModeGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    TemplateMode _mode;

    public ModeGroupViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _mode = session.Document.Mode;
        ToggleCommand = new RelayCommand(() => _session.Apply(DocumentOps.ToggleMode));
        session.Changed += (_, e) =>
        {
            if (!e.What.HasFlag(SessionChange.Document) || _session.Document.Mode == _mode) return;
            _mode = _session.Document.Mode;
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Label));
        };
    }

    public TemplateMode Mode => _mode;

    /// <summary>"SHIRT" or "PANTS" (editor.js:1702, :1706; editor.html:33).</summary>
    public string Label => _mode == TemplateMode.Shirt ? "SHIRT" : "PANTS";

    public IRelayCommand ToggleCommand { get; }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ModeGroupTests"`
Expected: `Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3` for `RBX15.App.Tests.dll`.

- [ ] **Step 7: Write the failing Settings and Edit tests** (ledger `home-settings-group`, `overlay-mask-checkbox`, `overlay-grid-checkbox`, `file-undo-snapshot-scope`, `home-edit-group`, `undo-buttons-shortcuts`, `btn-clear`, `undo-empty-steps`)

Create `src/RBX15.App.Tests/SettingsGroupTests.cs`:

```csharp
using RBX15.App.ViewModels;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The Settings group (editor.html:98-107; ledger home-settings-group, overlay-mask-checkbox, overlay-grid-checkbox, file-undo-snapshot-scope).</summary>
public class SettingsGroupTests
{
    [Fact] // editor.html:100: the name is never an undo step and never dirties the document
    public void Template_name_is_session_state_not_an_undo_step()
    {
        var session = new EditorSession();
        var settings = new SettingsGroupViewModel(session);
        var raised = Track(settings);

        settings.TemplateName = "My Shirt";

        Assert.Equal("My Shirt", session.TemplateTitle);
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
        Assert.Contains(nameof(SettingsGroupViewModel.TemplateName), raised);

        settings.TemplateName = null!;
        Assert.Equal("", session.TemplateTitle);
    }

    [Fact] // defaults: Mask on, Grid off (editor.html:102-103)
    public void Mask_starts_on_and_grid_off()
    {
        var settings = new SettingsGroupViewModel(new EditorSession());
        Assert.Equal((true, false), (settings.ShowMask, settings.ShowGrid));
    }

    [Fact] // file-undo-snapshot-scope fix: each toggle is one step; undo puts the checkbox back (editor.js:1433-1436)
    public void Mask_and_grid_toggles_are_one_step_each_and_undo_restores_them()
    {
        var session = new EditorSession();
        var settings = new SettingsGroupViewModel(session);
        var raised = Track(settings);

        settings.ShowMask = false;
        settings.ShowGrid = true;
        Assert.Equal(2, session.UndoCount);
        Assert.Equal((false, true), (session.Document.ShowMask, session.Document.ShowGrid));

        session.Undo();
        Assert.False(settings.ShowGrid);
        session.Undo();
        Assert.True(settings.ShowMask);
        Assert.Equal(2, raised.Count(n => n == nameof(SettingsGroupViewModel.ShowMask)));
        Assert.Equal(2, raised.Count(n => n == nameof(SettingsGroupViewModel.ShowGrid)));
    }

    [Fact] // setting the value it already has changes nothing
    public void Setting_the_current_value_records_nothing()
    {
        var session = new EditorSession();
        var settings = new SettingsGroupViewModel(session);
        settings.ShowMask = true;
        settings.ShowGrid = false;
        Assert.Equal(0, session.UndoCount);
    }
}
```

Create `src/RBX15.App.Tests/EditGroupTests.cs`:

```csharp
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The Edit group (editor.html:77-84, editor.js:1394, :1470-1487; ledger home-edit-group, btn-clear, undo-buttons-shortcuts).</summary>
public class EditGroupTests
{
    [Fact] // undo-buttons-shortcuts parityCheck: launch both off, an edit turns undo on, undo turns redo on, a new edit turns redo off
    public void Undo_and_redo_enable_exactly_when_their_stack_has_a_step()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        int undoChanged = 0;
        edit.UndoCommand.CanExecuteChanged += (_, _) => undoChanged++;

        Assert.False(edit.UndoCommand.CanExecute(null));
        Assert.False(edit.RedoCommand.CanExecute(null));

        session.Apply(d => d with { ShowGrid = true });
        Assert.True(edit.UndoCommand.CanExecute(null));
        Assert.False(edit.RedoCommand.CanExecute(null));

        edit.UndoCommand.Execute(null);
        Assert.False(edit.UndoCommand.CanExecute(null));
        Assert.True(edit.RedoCommand.CanExecute(null));
        Assert.False(session.Document.ShowGrid);

        edit.RedoCommand.Execute(null);
        Assert.True(session.Document.ShowGrid);

        session.Undo();
        session.Apply(d => d with { ShowMask = false });
        Assert.False(edit.RedoCommand.CanExecute(null));
        Assert.True(undoChanged >= 4);
    }

    [Fact] // home-edit-group parityCheck: after 31 edits only 30 undos are available
    public void Undo_depth_is_30()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        for (int i = 0; i < 31; i++) session.Apply(d => d with { BgOpacity = i / 100.0 });

        int undos = 0;
        while (edit.UndoCommand.CanExecute(null)) { edit.UndoCommand.Execute(null); undos++; }
        Assert.Equal(30, undos);
    }

    [Fact] // btn-clear parityCheck: layers gone in one step, fills intact, undo restores the layers
    public void Clear_removes_all_layers_in_one_step_and_keeps_fills()
    {
        var session = SessionWith(LayerOf(1), LayerOf(2));
        session.Select(2);
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#17d4fa")));
        var edit = new EditGroupViewModel(session);

        Assert.True(edit.ClearCommand.CanExecute(null));
        edit.ClearCommand.Execute(null);

        Assert.Empty(session.Document.Layers);
        Assert.Null(session.SelectedLayerId);
        Assert.Single(session.Document.RegionFills);
        Assert.Equal(2, session.UndoCount);

        session.Undo();
        Assert.Equal(new[] { 1, 2 }, session.Document.Layers.Select(l => l.Id));
    }

    [Fact] // undo-empty-steps: Clear with no layers changes nothing; like 4.1 it stays enabled
    public void Clear_with_no_layers_records_nothing_and_stays_enabled()
    {
        var session = new EditorSession();
        var edit = new EditGroupViewModel(session);
        edit.ClearCommand.Execute(null);
        Assert.Equal(0, session.UndoCount);
        Assert.True(edit.ClearCommand.CanExecute(null));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246` naming `SettingsGroupViewModel` and `EditGroupViewModel`.

- [ ] **Step 8: Implement SettingsGroupViewModel and EditGroupViewModel**

Create `src/RBX15.App/ViewModels/SettingsGroupViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Settings group (editor.html:98-107; ledger <c>home-settings-group</c>, keep). The template
/// name is session state: never an undo step, not in the document (editor.html:100). Mask and Grid
/// are document flags, so each toggle is one undo step (interfaces sheet §1,
/// <c>file-undo-snapshot-scope</c> fix; 4.1 captured them but never pushed, editor.js:1494-1495), and
/// undo puts the checkboxes back (editor.js:1433-1436).
/// </summary>
public sealed class SettingsGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    bool _showMask, _showGrid;

    public SettingsGroupViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _showMask = session.Document.ShowMask;
        _showGrid = session.Document.ShowGrid;
        session.Changed += (_, e) =>
        {
            if (e.What.HasFlag(SessionChange.Title)) OnPropertyChanged(nameof(TemplateName));
            if (!e.What.HasFlag(SessionChange.Document)) return;
            SetProperty(ref _showMask, _session.Document.ShowMask, nameof(ShowMask));
            SetProperty(ref _showGrid, _session.Document.ShowGrid, nameof(ShowGrid));
        };
    }

    /// <summary>#exportTitle (editor.html:100): the export and save file name source.</summary>
    public string TemplateName
    {
        get => _session.TemplateTitle;
        set => _session.TemplateTitle = value ?? "";
    }

    /// <summary>#showMask (editor.html:102, checked by default; ledger <c>overlay-mask-checkbox</c>).</summary>
    public bool ShowMask
    {
        get => _showMask;
        set => _session.Apply(d => d.ShowMask == value ? d : d with { ShowMask = value });
    }

    /// <summary>#showGrid (editor.html:103; ledger <c>overlay-grid-checkbox</c>).</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set => _session.Apply(d => d.ShowGrid == value ? d : d with { ShowGrid = value });
    }
}
```

Create `src/RBX15.App/ViewModels/EditGroupViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Edit group (editor.html:77-84; ledger <c>home-edit-group</c>, keep). Undo and Redo can run only
/// when their stack has a step, so both start disabled and grey out exactly when updateHistoryButtons
/// did (editor.js:1470-1475). Inside a gesture (a slider drag) the first change makes Undo possible
/// while only Document is raised, so Document changes during a gesture refresh them too (critic L1;
/// M4's Ctrl+Z mid-drag relies on it). Clear removes every layer as one step (editor.js:1394,
/// <c>btn-clear</c>, keep). Like 4.1 it is always enabled and never confirms; with no layers it
/// changes nothing, so it records nothing.
/// </summary>
public sealed class EditGroupViewModel
{
    readonly EditorSession _session;

    public EditGroupViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        UndoCommand = new RelayCommand(() => _session.Undo(), () => _session.CanUndo);
        RedoCommand = new RelayCommand(() => _session.Redo(), () => _session.CanRedo);
        ClearCommand = new RelayCommand(() => _session.Apply(DocumentOps.ClearLayers));
        session.Changed += (_, e) =>
        {
            bool history = e.What.HasFlag(SessionChange.History);
            bool gestureEdit = e.What.HasFlag(SessionChange.Document) && _session.InGesture;
            if (!history && !gestureEdit) return;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
    }

    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand ClearCommand { get; }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.SettingsGroupTests|FullyQualifiedName~RBX15.App.Tests.EditGroupTests"`
Expected: `Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 658`; App.Tests `Passed: 191, Failed: 0`; Parity.Tests `Passed: 35`.

Commit (end with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/ViewModels/ModeGroupViewModel.cs src/RBX15.App/ViewModels/SettingsGroupViewModel.cs src/RBX15.App/ViewModels/EditGroupViewModel.cs src/RBX15.App.Tests/HomeTestKit.cs src/RBX15.App.Tests/ModeGroupTests.cs src/RBX15.App.Tests/SettingsGroupTests.cs src/RBX15.App.Tests/EditGroupTests.cs
git commit -m "feat(app): Mode, Settings and Edit group view-models over EditorSession"
```

- [ ] **Step 9: Write the failing Fill Style and Template Regions tests** (ledger `regions-state-model`, `fillstyle-type-buttons`, `fillstyle-live-edit-rule`, `fillstyle-undo-snapshot-timing`, `fillstyle-color-pickers`, `fillstyle-angle-scale`, `fillstyle-ui-not-synced-from-region`, `layout-fill-style`, `regions-grid-layout-shirt`, `regions-grid-layout-pants`, `regions-click-paint`, `regions-click-fit-layer`, `claim-region-click-color-picker`, `regions-right-click-clear`, `regions-button-swatch-paint`, `fills-to-css`, `layout-template-regions`)

In `src/RBX15.App.Tests/HomeTestKit.cs`, add `using RBX15.App.ViewModels;` after `using System.Collections.Immutable;`, and add this member directly above the `/// <summary>Records every PropertyChanged` line:

```csharp
    public static RegionTileViewModel Tile(RegionsGroupViewModel regions, string region) =>
        regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Single(t => t.Region == region);

```

Create `src/RBX15.App.Tests/Fakes/FakeColorPicker.cs`:

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Returns queued answers in order (null = cancel) and records every seed it was opened with.</summary>
internal sealed class FakeColorPicker : IColorPicker
{
    readonly Queue<string?> _answers = new();

    public List<string> Seeds { get; } = [];

    public FakeColorPicker Answer(string? hex)
    {
        _answers.Enqueue(hex);
        return this;
    }

    public string? Pick(string initialHex)
    {
        Seeds.Add(initialHex);
        return _answers.Count > 0 ? _answers.Dequeue() : null;
    }
}
```

Create `src/RBX15.App.Tests/FillStyleTests.cs`:

```csharp
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Fill Style group (editor.js:1550-1613). Ledger rows: regions-state-model, fillstyle-type-buttons,
/// fillstyle-live-edit-rule, fillstyle-undo-snapshot-timing, fillstyle-color-pickers,
/// fillstyle-angle-scale, fillstyle-ui-not-synced-from-region.
/// </summary>
public class FillStyleTests
{
    static (EditorSession Session, FillStyleViewModel Fill, RegionsGroupViewModel Regions) Make(FakeColorPicker? picker = null)
    {
        var session = new EditorSession();
        var fill = new FillStyleViewModel(session, picker ?? new FakeColorPicker());
        return (session, fill, new RegionsGroupViewModel(session, fill));
    }

    static void Paint(RegionsGroupViewModel regions, params string[] keys)
    {
        foreach (var k in keys) Tile(regions, k).PaintCommand.Execute(null);
    }

    [Fact] // editor.js:1552 and editor.html:148-162
    public void Defaults_and_type_buttons_match_4_1()
    {
        var (_, fill, _) = Make();
        Assert.Equal(new Fill("solid", "#17d4fa", "#f22f89", 45, 16), fill.Current);
        Assert.Equal(("solid", "#17d4fa", "#f22f89", 45.0, 16.0), (fill.Type, fill.Color1, fill.Color2, fill.Angle, fill.Scale));
        Assert.Equal(
            new[] { ("solid", "Solid"), ("linear", "Gradient"), ("radial", "Radial"), ("stripes", "Stripes"), ("checker", "Checker"), ("dots", "Dots"), ("camo", "Camo") },
            fill.Types.Select(t => (t.Type, t.Label)));
        Assert.Equal((0.0, 360.0, 4.0, 48.0), (FillStyleViewModel.MinAngle, FillStyleViewModel.MaxAngle, FillStyleViewModel.MinScale, FillStyleViewModel.MaxScale));
    }

    [Fact] // fillstyle-live-edit-rule scenario: A,B follow the controls, C (another style) does not; one step per edit
    public void An_edit_retargets_regions_with_the_current_key_only()
    {
        var (session, fill, regions) = Make();
        fill.SetTypeCommand.Execute("linear");           // nothing painted yet: no step
        Paint(regions, "torso_front", "torso_back");     // A, B: linear 45
        fill.SetTypeCommand.Execute("solid");            // A, B follow to solid (one step)
        fill.SetTypeCommand.Execute("linear");           // and back
        Assert.Equal(4, session.UndoCount);
        var other = Fill.Solid("#123456");               // C: a style the controls do not hold
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_up", other));

        fill.Angle = 90;

        Assert.Equal(6, session.UndoCount);
        Assert.Equal(fill.Current, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal(fill.Current, DocumentOps.RegionFill(session.Document, "torso_back"));
        Assert.Equal(90, fill.Current.Angle);
        Assert.Equal("linear", fill.Current.Type);
        Assert.Same(other, DocumentOps.RegionFill(session.Document, "torso_up"));
    }

    [Fact] // undo-empty-steps: re-clicking the active type changes nothing, so no step
    public void Reclicking_the_active_type_records_nothing()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        fill.SetTypeCommand.Execute("solid");
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // editor.js:1570-1579: an edit that touches no painted region records nothing
    public void An_edit_with_no_matching_region_changes_current_and_records_nothing()
    {
        var (session, fill, _) = Make();
        fill.Angle = 10;
        fill.Scale = 30;
        Assert.Equal((10.0, 30.0), (fill.Current.Angle, fill.Current.Scale));
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
    }

    [Fact] // fillstyle-undo-snapshot-timing parityCheck: a drag with a matching region is exactly one step
    public void A_slider_drag_is_one_step_and_undo_restores_the_starting_fills()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front", "torso_left");
        var start = fill.Current;

        using (fill.BeginSliderDrag())
        {
            for (int a = 46; a <= 120; a++) fill.Angle = a;
            Assert.Equal(2, session.UndoCount);          // the two paints; nothing yet for the drag
        }

        Assert.Equal(3, session.UndoCount);
        Assert.Equal(120, DocumentOps.RegionFill(session.Document, "torso_left")!.Angle);
        session.Undo();
        Assert.Equal(start, DocumentOps.RegionFill(session.Document, "torso_front"));
        Assert.Equal(start, DocumentOps.RegionFill(session.Document, "torso_left"));
    }

    [Fact] // Review Focus: a drag that ends where it started leaves the document unchanged, so no step
    public void A_drag_that_returns_to_its_start_records_nothing()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");

        using (fill.BeginSliderDrag())
        {
            fill.Scale = 30;
            fill.Scale = 16;
        }

        Assert.Equal(1, session.UndoCount);
        Assert.Equal(FillStyleViewModel.DefaultFill, DocumentOps.RegionFill(session.Document, "torso_front"));
    }

    [Fact] // the fix: a change outside a drag (arrow key, track click) is its own step; 4.1 recorded none
    public void Keyboard_slider_changes_are_one_step_each()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        fill.Angle = 46;
        fill.Angle = 47;
        Assert.Equal(3, session.UndoCount);
        session.Undo();
        Assert.Equal(46, DocumentOps.RegionFill(session.Document, "torso_front")!.Angle);
    }

    [Theory] // fillstyle-angle-scale: whole numbers inside the range's bounds
    [InlineData(45.4, 45)]
    [InlineData(45.5, 46)]
    [InlineData(-3, 0)]
    [InlineData(400, 360)]
    public void Angle_is_a_whole_number_in_0_to_360(double set, double expected)
    {
        var (_, fill, _) = Make();
        fill.Angle = set;
        Assert.Equal(expected, fill.Angle);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(48.6, 48)]
    [InlineData(12.5, 13)]
    public void Scale_is_a_whole_number_in_4_to_48(double set, double expected)
    {
        var (_, fill, _) = Make();
        fill.Scale = set;
        Assert.Equal(expected, fill.Scale);
    }

    [Fact] // Review Focus: cancel, the same color, and an uppercase answer
    public void The_color_picker_is_seeded_stores_lowercase_and_cancel_or_same_color_records_nothing()
    {
        var picker = new FakeColorPicker().Answer(null).Answer("#17D4FA").Answer("#ABCDEF").Answer("#00ff00");
        var (session, fill, regions) = Make(picker);
        Paint(regions, "torso_front");

        fill.PickColor1Command.Execute(null);            // cancel
        fill.PickColor1Command.Execute(null);            // same color, uppercase
        Assert.Equal(1, session.UndoCount);
        Assert.Equal("#17d4fa", fill.Color1);

        fill.PickColor1Command.Execute(null);            // a new color: one step, lowercase
        Assert.Equal("#abcdef", fill.Color1);
        Assert.Equal("#abcdef", DocumentOps.RegionFill(session.Document, "torso_front")!.C1);
        Assert.Equal(2, session.UndoCount);

        fill.PickColor2Command.Execute(null);
        Assert.Equal("#00ff00", fill.Color2);
        Assert.Equal(new[] { "#17d4fa", "#17d4fa", "#17d4fa", "#f22f89" }, picker.Seeds);
    }

    [Fact] // Review Focus and open question 5: undo restores regions, never the controls
    public void Undo_of_a_live_edit_restores_regions_and_leaves_the_controls()
    {
        var (session, fill, regions) = Make();
        Paint(regions, "torso_front");
        fill.Angle = 90;

        session.Undo();

        Assert.Equal(45, DocumentOps.RegionFill(session.Document, "torso_front")!.Angle);
        Assert.Equal(90, fill.Angle);                    // fillstyle-ui-not-synced-from-region, keep
        Assert.Equal(90, fill.Current.Angle);

        fill.Angle = 100;                                // the restored region no longer has the current key
        Assert.Equal(45, DocumentOps.RegionFill(session.Document, "torso_front")!.Angle);
        Assert.Equal((1, 1), (session.UndoCount, session.RedoCount)); // no step, so redo survives
    }

    [Fact]
    public void Unknown_types_and_malformed_colors_are_refused()
    {
        var (_, fill, _) = Make();
        Assert.Throws<ArgumentException>(() => fill.SetTypeCommand.Execute("gradient"));
        Assert.Throws<ArgumentException>(() => fill.Color1 = "17d4fa");
        Assert.Throws<ArgumentException>(() => fill.Color2 = "#17d4fz");
        Assert.Equal(FillStyleViewModel.DefaultFill, fill.Current);
    }

    [Fact] // the controls bind to these names
    public void Edits_raise_the_changed_control_and_Current()
    {
        var (_, fill, _) = Make();
        var raised = Track(fill);
        fill.SetTypeCommand.Execute("dots");
        fill.Angle = 45;                                  // unchanged: nothing raised
        Assert.Equal(new[] { nameof(FillStyleViewModel.Current), nameof(FillStyleViewModel.Type) }, raised);
    }

    [Theory] // ChooseColorW speaks COLORREF 0x00BBGGRR; the picker answers lowercase #rrggbb
    [InlineData("#17d4fa", 0x00FAD417u)]
    [InlineData("#f22f89", 0x00892FF2u)]
    [InlineData("#000000", 0x00000000u)]
    [InlineData("#ffffff", 0x00FFFFFFu)]
    public void Hex_and_COLORREF_round_trip(string hex, uint colorRef)
    {
        Assert.Equal(colorRef, Win32ColorPicker.ToColorRef(hex));
        Assert.Equal(hex, Win32ColorPicker.FromColorRef(colorRef));
        Assert.Equal(hex, Win32ColorPicker.FromColorRef(colorRef | 0xFF000000u));
    }
}
```

Create `src/RBX15.App.Tests/RegionsGroupTests.cs`:

```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Template Regions grid (buildRegionGrid, editor.js:1615-1687). Ledger rows:
/// regions-grid-layout-shirt/pants, regions-click-paint, regions-click-fit-layer,
/// regions-right-click-clear, regions-button-swatch-paint, fills-to-css, region-front-highlight-lost.
/// </summary>
public class RegionsGroupTests
{
    static (EditorSession Session, RegionsGroupViewModel Regions, FillStyleViewModel FillStyle) Make(EditorSession? session = null)
    {
        session ??= new EditorSession();
        var fillStyle = new FillStyleViewModel(session, new FakeColorPicker());
        return (session, new RegionsGroupViewModel(session, fillStyle), fillStyle);
    }

    static string[] Keys(IEnumerable<RegionTileViewModel> tiles) => tiles.Select(t => t.Region).ToArray();
    static string[] Labels(IEnumerable<RegionTileViewModel> tiles) => tiles.Select(t => t.Label).ToArray();

    [Fact] // editor.js:1625-1648: cross Top/R/Front/L/Back/Bot; right arm L,Bk,R,Fr; left arm Fr,L,Bk,R
    public void Shirt_grid_has_4_1s_fourteen_tiles_in_order()
    {
        var (_, regions, _) = Make();

        Assert.Equal(new[] { "torso_up", "torso_right", "torso_front", "torso_left", "torso_back", "torso_down" }, Keys(regions.Body));
        Assert.Equal(new[] { "Top", "R", "Front", "L", "Back", "Bot" }, Labels(regions.Body));
        Assert.Equal(new[] { "rarm_left", "rarm_back", "rarm_right", "rarm_front" }, Keys(regions.Limb1));
        Assert.Equal(new[] { "L", "Bk", "R", "Fr" }, Labels(regions.Limb1));
        Assert.Equal(new[] { "larm_front", "larm_left", "larm_back", "larm_right" }, Keys(regions.Limb2));
        Assert.Equal(new[] { "Fr", "L", "Bk", "R" }, Labels(regions.Limb2));
        Assert.Equal(("Torso", "Right Arm", "Left Arm"), (regions.BodyLabel, regions.LimbLabel1, regions.LimbLabel2));
        // limb up and down have no tile (regions-grid-layout-shirt quirk, keep)
        Assert.DoesNotContain(regions.Limb1.Concat(regions.Limb2), t => t.Region.EndsWith("_up") || t.Region.EndsWith("_down"));
    }

    [Fact] // regions-grid-layout-pants: same structure, waist/rleg/lleg keys, Leg headings, rebuilt on toggle
    public void Pants_grid_rebuilds_with_pants_keys_and_headings()
    {
        var (session, regions, _) = Make();
        var raised = Track(regions);

        session.Apply(DocumentOps.ToggleMode);

        Assert.Equal(new[] { "waist_up", "waist_right", "waist_front", "waist_left", "waist_back", "waist_down" }, Keys(regions.Body));
        Assert.Equal(new[] { "rleg_left", "rleg_back", "rleg_right", "rleg_front" }, Keys(regions.Limb1));
        Assert.Equal(new[] { "lleg_front", "lleg_left", "lleg_back", "lleg_right" }, Keys(regions.Limb2));
        Assert.Equal(("Waist", "Right Leg", "Left Leg"), (regions.BodyLabel, regions.LimbLabel1, regions.LimbLabel2));
        Assert.Contains(nameof(RegionsGroupViewModel.Body), raised);
        Assert.Contains(nameof(RegionsGroupViewModel.LimbLabel1), raised);
    }

    [Fact] // spec §4: every tile has an automation name, unique within the grid
    public void Tiles_have_unique_part_and_face_automation_names()
    {
        var (_, regions, _) = Make();
        var names = regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Select(t => t.AutomationName).ToArray();

        Assert.Equal(14, names.Distinct().Count());
        Assert.Equal("Torso Front", Tile(regions, "torso_front").AutomationName);
        Assert.Equal("Right Arm Back", Tile(regions, "rarm_back").AutomationName);
        Assert.Equal("Left Arm Front", Tile(regions, "larm_front").AutomationName);
    }

    [Fact] // only the body front carries the resting tint (editor.js:1627)
    public void Only_the_body_front_is_the_front_tile()
    {
        var (session, regions, _) = Make();
        Assert.Equal(new[] { "torso_front" }, regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Where(t => t.IsFront).Select(t => t.Region));
        session.Apply(DocumentOps.ToggleMode);
        Assert.True(Tile(regions, "waist_front").IsFront);
    }

    [Fact] // editor.js:1668-1674 plus the regions-click-paint fix: an identical repaint is no step
    public void Click_with_nothing_selected_paints_the_current_fill_once()
    {
        var (session, regions, fillStyle) = Make();
        var front = Tile(regions, "torso_front");

        front.PaintCommand.Execute(null);

        Assert.Equal(fillStyle.Current, front.Fill);
        Assert.True(front.IsFilled);
        Assert.Equal(("torso_front", FillStyleViewModel.DefaultFill), Assert.Single(session.Document.RegionFills));
        Assert.Equal(1, session.UndoCount);

        front.PaintCommand.Execute(null);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // claim-region-click-color-picker: with a layer selected the click fits it; fills untouched
    public void Click_with_a_layer_selected_fits_it_and_leaves_fills_alone()
    {
        var session = SessionWith(LayerOf(1, 200, 100));
        session.Select(1);
        var (_, regions, _) = Make(session);

        Tile(regions, "torso_front").PaintCommand.Execute(null);

        var layer = session.Document.Layers[0];
        Assert.Equal((0.544, 240.6, 110.8), (Math.Round(layer.Scale, 9), Math.Round(layer.X, 9), Math.Round(layer.Y, 9)));
        Assert.Empty(session.Document.RegionFills);
        Assert.Null(Tile(regions, "torso_front").Fill);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(1, session.SelectedLayerId);
    }

    [Fact] // regions-right-click-clear: unpainted -> no step; painted -> one step and the swatch goes
    public void Right_click_clears_a_painted_tile_in_one_step_and_ignores_an_empty_one()
    {
        var (session, regions, _) = Make();
        var back = Tile(regions, "torso_back");

        back.ClearCommand.Execute(null);
        Assert.Equal(0, session.UndoCount);

        back.PaintCommand.Execute(null);
        var raised = Track(back);
        back.ClearCommand.Execute(null);

        Assert.Null(back.Fill);
        Assert.False(back.IsFilled);
        Assert.Null(back.Swatch);
        Assert.Equal(2, session.UndoCount);
        Assert.Contains(nameof(RegionTileViewModel.Swatch), raised);
    }

    [Fact] // undo and redo repaint the tiles (editor.js:1445-1448)
    public void Undo_and_redo_repaint_tiles()
    {
        var (session, regions, _) = Make();
        var up = Tile(regions, "torso_up");
        up.PaintCommand.Execute(null);

        session.Undo();
        Assert.Null(up.Fill);

        session.Redo();
        Assert.Equal(FillStyleViewModel.DefaultFill, up.Fill);
    }

    [Fact] // Pants tiles write pants keys (mode-pants-crash-hardcoded-keys)
    public void Pants_tiles_paint_pants_keys()
    {
        var (session, regions, _) = Make(new EditorSession(new Document { Mode = TemplateMode.Pants }));
        Tile(regions, "lleg_back").PaintCommand.Execute(null);
        Assert.Equal("lleg_back", Assert.Single(session.Document.RegionFills).Region);
    }

    static byte[] Bgra(BitmapSource bmp)
    {
        var bytes = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        bmp.CopyPixels(bytes, bmp.PixelWidth * 4, 0);
        return bytes;
    }

    [Theory] // fills-to-css fix: the swatch is the canvas painter's own pixels for the region
    [InlineData("solid")]
    [InlineData("linear")]
    [InlineData("radial")]
    [InlineData("stripes")]
    [InlineData("checker")]
    [InlineData("dots")]
    [InlineData("camo")]
    public void Swatch_is_the_canvas_painter_cropped_to_the_region(string type) => StaThread.Run(() =>
    {
        var fill = FillStyleViewModel.DefaultFill with { Type = type, Angle = 30, Scale = 12 };
        var session = new EditorSession();
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", fill));
        var (_, regions, _) = Make(session);

        var swatch = Assert.IsAssignableFrom<BitmapSource>(Tile(regions, "torso_front").Swatch);

        Assert.True(swatch.IsFrozen);
        Assert.Equal(PixelFormats.Pbgra32, swatch.Format);
        Assert.Equal((128, 128), (swatch.PixelWidth, swatch.PixelHeight));
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        Template.TryFind(TemplateMode.Shirt, "torso_front", out var r);
        Fills.PaintRegionFill(surface.Canvas, r.Rect, fill);
        using var full = surface.Snapshot();
        using var crop = full.Subset(SKRectI.Create(r.X, r.Y, r.W, r.H));
        var expected = new byte[r.W * r.H * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(expected, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { Assert.True(crop.ReadPixels(new SKImageInfo(r.W, r.H, SKColorType.Bgra8888, SKAlphaType.Premul), handle.AddrOfPinnedObject(), r.W * 4, 0, 0)); }
        finally { handle.Free(); }
        Assert.Equal(expected, Bgra(swatch));
    });

    [Fact] // fills-to-css divergence: canvas angle 0 runs left to right (CSS 0deg would run bottom to top)
    public void A_zero_degree_gradient_swatch_runs_left_to_right() => StaThread.Run(() =>
    {
        var fill = new Fill("linear", "#ff0000", "#0000ff", 0, 16);
        var session = new EditorSession();
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", fill));
        var (_, regions, _) = Make(session);
        var px = Bgra((BitmapSource)Tile(regions, "torso_front").Swatch!);

        (byte B, byte G, byte R) At(int x, int y) { int i = (y * 128 + x) * 4; return (px[i], px[i + 1], px[i + 2]); }

        Assert.True(At(0, 64).R > 240 && At(0, 64).B < 15, $"left {At(0, 64)}");
        Assert.True(At(127, 64).B > 240 && At(127, 64).R < 15, $"right {At(127, 64)}");
        Assert.Equal(At(64, 0), At(64, 127));
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246` naming `IColorPicker`, `FillStyleViewModel`, `RegionsGroupViewModel`, `RegionTileViewModel` and `Win32ColorPicker`. Task 1 already created the `RBX15.App.Services` namespace, so the error is CS0246, not CS0234.

- [ ] **Step 10: Implement the color picker, Fill Style and Template Regions**

The tests read `Win32ColorPicker.ToColorRef`/`FromColorRef`, which are internal; Task 1's `InternalsVisibleTo` already exposes them.

Create `src/RBX15.App/Services/IColorPicker.cs`:

```csharp
namespace RBX15.App.Services;

/// <summary>
/// The Fill Style group's Color 1 and Color 2 pickers (editor.html:157-158, ledger
/// <c>fillstyle-color-pickers</c>). Chromium on Windows opens the Win32 ChooseColor dialog for an
/// <c>&lt;input type=color&gt;</c>; v5 opens the same dialog (contract ruling). The view-models see only
/// this interface, so they test headless with a fake.
/// </summary>
public interface IColorPicker
{
    /// <summary>
    /// Shows the picker seeded with <paramref name="initialHex"/> (<c>#rrggbb</c>). Returns the picked
    /// color as lowercase <c>#rrggbb</c>, as an input of type color reports it (ledger
    /// <c>regions-state-model</c>), or null when the user cancels.
    /// </summary>
    string? Pick(string initialHex);
}
```

Create `src/RBX15.App/Services/Win32ColorPicker.cs`:

```csharp
using System.Globalization;
using System.Runtime.InteropServices;
using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// <see cref="IColorPicker"/> over comdlg32 <c>ChooseColorW</c>, the dialog Chromium opens for 4.1's
/// <c>&lt;input type=color&gt;</c> on Windows (contract ruling: P/Invoke, no WinForms reference). The
/// dialog opens fully expanded (<c>CC_FULLOPEN</c>) on the current color (<c>CC_RGBINIT</c>). The 16
/// custom colors persist for the life of the process, shared by both pickers. UI thread only.
/// </summary>
public sealed class Win32ColorPicker(Func<IntPtr> ownerHandle) : IColorPicker
{
    const int CC_RGBINIT = 0x1;
    const int CC_FULLOPEN = 0x2;

    // 16 COLORREFs, white until the user stores one. Allocated once and never freed: the process owns it.
    static readonly IntPtr CustomColors = AllocCustomColors();

    public string? Pick(string initialHex)
    {
        var cc = new ChooseColorStruct
        {
            lStructSize = Marshal.SizeOf<ChooseColorStruct>(),
            hwndOwner = ownerHandle(),
            rgbResult = ToColorRef(initialHex),
            lpCustColors = CustomColors,
            Flags = CC_RGBINIT | CC_FULLOPEN,
        };
        return ChooseColorW(ref cc) ? FromColorRef(cc.rgbResult) : null;
    }

    /// <summary><c>#rrggbb</c> (or <c>#rgb</c>) to a Win32 COLORREF, <c>0x00BBGGRR</c>. Unparsable reads as black, as <see cref="CssColor.HexToRgb"/> does.</summary>
    internal static uint ToColorRef(string hex)
    {
        var (r, g, b) = CssColor.HexToRgb(hex);
        return (uint)(r | (g << 8) | (b << 16));
    }

    /// <summary>A COLORREF to lowercase <c>#rrggbb</c>; the high byte is ignored.</summary>
    internal static string FromColorRef(uint colorRef) =>
        string.Create(CultureInfo.InvariantCulture, $"#{colorRef & 0xFF:x2}{(colorRef >> 8) & 0xFF:x2}{(colorRef >> 16) & 0xFF:x2}");

    static IntPtr AllocCustomColors()
    {
        var block = Marshal.AllocHGlobal(16 * sizeof(uint));
        for (int i = 0; i < 16; i++) Marshal.WriteInt32(block, i * sizeof(uint), 0x00FFFFFF);
        return block;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ChooseColorStruct
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public uint rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ChooseColorW(ref ChooseColorStruct cc);
}
```

Create `src/RBX15.App/ViewModels/FillStyleViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>One Fill Style type button: the lib/fills.js type and its 4.1 label (editor.html:148-154).</summary>
public sealed record FillTypeOption(string Type, string Label);

/// <summary>
/// The Fill Style group (editor.html:145-167, editor.js:1550-1613; ledger <c>layout-fill-style</c>).
/// <see cref="Current"/> is 4.1's <c>currentFill</c>. Every control edit runs applyFillEdit's rule
/// (<c>fillstyle-live-edit-rule</c>, keep): the edit replaces every region painted with the current
/// fill's key, through one <see cref="EditorSession.Apply"/>.
/// <para>
/// Undo (interfaces sheet §1, <c>fillstyle-undo-snapshot-timing</c>, fix): a slider thumb drag is one
/// gesture (<see cref="BeginSliderDrag"/>), so it records one step at the end, and only if a region
/// changed. Anything else (a type click, a picked color, a keyboard or track-click slider change) is one
/// <c>Apply</c>, so one step when a region changed and none otherwise.
/// </para>
/// <para>
/// One-way (<c>fillstyle-ui-not-synced-from-region</c>, keep): <see cref="Current"/> lives only here.
/// Undo, redo and region clicks never change it. The controls bind to it, so they always show the
/// current fill (the <c>fillstyle-type-buttons</c> fix: the active button can no longer disagree).
/// </para>
/// </summary>
public sealed class FillStyleViewModel : ObservableObject
{
    /// <summary>currentFill's initial value (editor.js:1552), equal to the HTML control defaults (editor.html:148-162).</summary>
    public static readonly Fill DefaultFill = new("solid", "#17d4fa", "#f22f89", 45, 16);

    public const double MinAngle = 0;   // editor.html:160 min
    public const double MaxAngle = 360; // editor.html:160 max
    public const double MinScale = 4;   // editor.html:162 min
    public const double MaxScale = 48;  // editor.html:162 max

    readonly EditorSession _session;
    readonly IColorPicker _picker;
    Fill _current = DefaultFill;

    public FillStyleViewModel(EditorSession session, IColorPicker picker)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        SetTypeCommand = new RelayCommand<string>(t => Type = t ?? throw new ArgumentNullException(nameof(t)));
        PickColor1Command = new RelayCommand(() => { if (Pick(Color1) is { } hex) Color1 = hex; });
        PickColor2Command = new RelayCommand(() => { if (Pick(Color2) is { } hex) Color2 = hex; });
    }

    /// <summary>The seven type buttons in 4.1's order (editor.html:148-154).</summary>
    public IReadOnlyList<FillTypeOption> Types { get; } =
    [
        new("solid", "Solid"),
        new("linear", "Gradient"),
        new("radial", "Radial"),
        new("stripes", "Stripes"),
        new("checker", "Checker"),
        new("dots", "Dots"),
        new("camo", "Camo"),
    ];

    public Fill Current => _current;

    /// <summary>The active type (editor.js:1605). Setting a type that is not one of <see cref="Types"/> throws.</summary>
    public string Type
    {
        get => _current.Type;
        set
        {
            if (!Types.Any(t => t.Type == value)) throw new ArgumentException($"Unknown fill type '{value}'.", nameof(value));
            Edit(_current with { Type = value });
        }
    }

    /// <summary>Color 1, lowercase <c>#rrggbb</c> (editor.js:1608; an input of type color yields lowercase).</summary>
    public string Color1
    {
        get => _current.C1!;
        set => Edit(_current with { C1 = NormalizeHex(value) });
    }

    /// <summary>Color 2, lowercase <c>#rrggbb</c> (editor.js:1608).</summary>
    public string Color2
    {
        get => _current.C2!;
        set => Edit(_current with { C2 = NormalizeHex(value) });
    }

    /// <summary>Angle in whole degrees, 0 to 360 (editor.html:160; <c>+v</c> of a step-1 range, editor.js:1609).</summary>
    public double Angle
    {
        get => _current.Angle!.Value;
        set => Edit(_current with { Angle = Whole(value, MinAngle, MaxAngle) });
    }

    /// <summary>Pattern scale, whole numbers 4 to 48 (editor.html:162).</summary>
    public double Scale
    {
        get => _current.Scale!.Value;
        set => Edit(_current with { Scale = Whole(value, MinScale, MaxScale) });
    }

    public IRelayCommand<string> SetTypeCommand { get; }
    public IRelayCommand PickColor1Command { get; }
    public IRelayCommand PickColor2Command { get; }

    /// <summary>
    /// Opens one slider gesture (Thumb.DragStarted). Dispose it on DragCompleted: the drag records one
    /// undo step iff it changed a region (editor.js:1570-1579, :1611). Wraps <see cref="EditorSession.BeginGesture"/>.
    /// </summary>
    public IDisposable BeginSliderDrag() => _session.BeginGesture();

    string? Pick(string seed)
    {
        var hex = _picker.Pick(seed);
        return hex is null ? null : NormalizeHex(hex);
    }

    /// <summary>applyFillEdit (editor.js:1581-1598): mutate currentFill, then retarget the regions that had its old key.</summary>
    void Edit(Fill next)
    {
        if (next == _current) return;
        string beforeKey = _current.Key;
        var before = _current;
        _current = next;
        OnPropertyChanged(nameof(Current));
        if (before.Type != next.Type) OnPropertyChanged(nameof(Type));
        if (before.C1 != next.C1) OnPropertyChanged(nameof(Color1));
        if (before.C2 != next.C2) OnPropertyChanged(nameof(Color2));
        if (before.Angle != next.Angle) OnPropertyChanged(nameof(Angle));
        if (before.Scale != next.Scale) OnPropertyChanged(nameof(Scale));
        _session.Apply(d => DocumentOps.RetargetFill(d, beforeKey, next));
    }

    static double Whole(double value, double min, double max)
    {
        if (double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), "NaN is not a slider value.");
        return Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), min, max);
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

Create `src/RBX15.App/ViewModels/RegionSwatch.cs`:

```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// A region tile's swatch, painted by the engine's own fill painter (ledger <c>fills-to-css</c> and
/// <c>regions-button-swatch-paint</c>, fix). 4.1 approximated the fill in CSS (lib/fills.js:167-185),
/// whose angle convention (0deg is bottom to top) contradicts the canvas (0 is left to right). The
/// swatch is the region's own rectangle painted at template scale exactly as the canvas paints it, so
/// a checker or dots cell is the same template-space cell. The region is its own gradient extent, so a
/// tile shows the whole c1 to c2 run whatever its neighbors hold. The tile scales the bitmap to fit.
/// </summary>
internal static class RegionSwatch
{
    /// <summary>Pbgra32 at 96 DPI, frozen, so any thread can read it.</summary>
    public static BitmapSource Render(TemplateMode mode, string region, Fill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        if (!Template.TryFind(mode, region, out var r))
            throw new ArgumentException($"'{region}' is not a {mode} region.", nameof(region));
        var info = new SKImageInfo(r.W, r.H, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create swatch surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(-r.X, -r.Y);
        Fills.PaintRegionFill(canvas, r.Rect, fill);
        canvas.Flush();
        using var pixmap = surface.PeekPixels();
        var bitmap = BitmapSource.Create(r.W, r.H, 96, 96, PixelFormats.Pbgra32, null, pixmap.GetPixelSpan().ToArray(), pixmap.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }
}
```

Create `src/RBX15.App/ViewModels/RegionTileViewModel.cs`:

```csharp
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// One Template Regions tile (a <c>.region-btn</c>, editor.js:1624-1685; ledger
/// <c>layout-template-regions</c>, fix). A real button, so it is keyboard-reachable and carries an
/// automation name (spec §4 accessibility). <see cref="PaintCommand"/> is the click: with a layer
/// selected it fits that layer into the region, otherwise it paints the region with the current Fill
/// Style (editor.js:1657-1675). <see cref="ClearCommand"/> is the right-click (editor.js:1677-1684).
/// Each is one <see cref="EditorSession.Apply"/>, so one undo step when it changes something.
/// </summary>
public sealed class RegionTileViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly FillStyleViewModel _fillStyle;
    readonly TemplateMode _mode;
    Fill? _fill;
    ImageSource? _swatch;

    internal RegionTileViewModel(EditorSession session, FillStyleViewModel fillStyle, TemplateMode mode,
                                 string region, string label, string automationName, bool isFront)
    {
        _session = session;
        _fillStyle = fillStyle;
        _mode = mode;
        Region = region;
        Label = label;
        AutomationName = automationName;
        IsFront = isFront;
        _fill = DocumentOps.RegionFill(session.Document, region);
        PaintCommand = new RelayCommand(Paint);
        ClearCommand = new RelayCommand(() => _session.Apply(d => DocumentOps.ClearRegion(d, Region)));
    }

    /// <summary>The verbatim region key, e.g. <c>torso_front</c> (data-r, editor.js:1626).</summary>
    public string Region { get; }

    /// <summary>4.1's tile text before CSS uppercasing: Top, R, Front, L, Back, Bot, Bk, Fr (editor.js:1626-1646).</summary>
    public string Label { get; }

    /// <summary>The part and face, e.g. "Torso Front" or "Right Leg Back".</summary>
    public string AutomationName { get; }

    /// <summary>The body's front tile, which rests on the #2a1040 tint (editor.js:1627; <c>region-front-highlight-lost</c> fix).</summary>
    public bool IsFront { get; }

    /// <summary>The region's fill, or null when unpainted.</summary>
    public Fill? Fill
    {
        get => _fill;
        internal set
        {
            if (_fill == value) return;
            _fill = value;
            _swatch = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFilled));
            OnPropertyChanged(nameof(Swatch));
        }
    }

    /// <summary>The <c>.filled</c> class (editor.js:1559-1562).</summary>
    public bool IsFilled => _fill is not null;

    /// <summary>The engine-painted preview of <see cref="Fill"/>; null when unpainted. Rendered on first read.</summary>
    public ImageSource? Swatch => _fill is null ? null : _swatch ??= RegionSwatch.Render(_mode, Region, _fill);

    public IRelayCommand PaintCommand { get; }
    public IRelayCommand ClearCommand { get; }

    void Paint()
    {
        if (_session.SelectedLayer is { } layer)
            _session.Apply(d => DocumentOps.FitLayerToRegion(d, layer.Id, Region));
        else
            _session.Apply(d => DocumentOps.PaintRegion(d, Region, _fillStyle.Current));
    }
}
```

Create `src/RBX15.App/ViewModels/RegionsGroupViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Template Regions group (buildRegionGrid, editor.js:1615-1687; ledger
/// <c>regions-grid-layout-shirt</c>, <c>regions-grid-layout-pants</c>, keep). Fourteen tiles: the body
/// cross (six faces) and four faces per limb; limb up and down have no tile, as in 4.1. Keys come from
/// the semantic slots (<see cref="Template.Slot"/>), so Pants tiles write <c>waist_</c>, <c>rleg_</c>
/// and <c>lleg_</c> keys. A mode change rebuilds the tiles (editor.js:1715); any other document change
/// updates each tile's fill, so undo, redo and live edits repaint the swatches (editor.js:1445-1448,
/// :1592-1595).
/// </summary>
public sealed class RegionsGroupViewModel : ObservableObject
{
    // editor.js:1626-1628 in document order: Top, R, Front, L, Back, Bot.
    static readonly (Face Face, string Label)[] BodyFaces =
        [(Face.Up, "Top"), (Face.Right, "R"), (Face.Front, "Front"), (Face.Left, "L"), (Face.Back, "Back"), (Face.Down, "Bot")];

    // editor.js:1634-1637: the right limb runs L, Bk, R, Fr.
    static readonly (Face Face, string Label)[] Limb1Faces =
        [(Face.Left, "L"), (Face.Back, "Bk"), (Face.Right, "R"), (Face.Front, "Fr")];

    // editor.js:1643-1646: the left limb runs Fr, L, Bk, R (mirrored to the atlas; keep).
    static readonly (Face Face, string Label)[] Limb2Faces =
        [(Face.Front, "Fr"), (Face.Left, "L"), (Face.Back, "Bk"), (Face.Right, "R")];

    readonly EditorSession _session;
    readonly FillStyleViewModel _fillStyle;
    TemplateMode _mode;

    public RegionsGroupViewModel(EditorSession session, FillStyleViewModel fillStyle)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _fillStyle = fillStyle ?? throw new ArgumentNullException(nameof(fillStyle));
        Build(session.Document.Mode);
        session.Changed += (_, e) =>
        {
            if (!e.What.HasFlag(SessionChange.Document)) return;
            var doc = _session.Document;
            if (doc.Mode != _mode)
            {
                Build(doc.Mode);
                OnPropertyChanged(nameof(BodyLabel));
                OnPropertyChanged(nameof(LimbLabel1));
                OnPropertyChanged(nameof(LimbLabel2));
                OnPropertyChanged(nameof(Body));
                OnPropertyChanged(nameof(Limb1));
                OnPropertyChanged(nameof(Limb2));
                return;
            }
            foreach (var tile in Body.Concat(Limb1).Concat(Limb2))
                tile.Fill = DocumentOps.RegionFill(doc, tile.Region);
        };
    }

    /// <summary>"Torso" or "Waist" (editor.js:1622). 4.1 never rendered it; v5 uses it in the body tiles' automation names.</summary>
    public string BodyLabel => _mode == TemplateMode.Shirt ? "Torso" : "Waist";

    /// <summary>"Right Arm" or "Right Leg" (editor.js:1620).</summary>
    public string LimbLabel1 => _mode == TemplateMode.Shirt ? "Right Arm" : "Right Leg";

    /// <summary>"Left Arm" or "Left Leg" (editor.js:1621).</summary>
    public string LimbLabel2 => _mode == TemplateMode.Shirt ? "Left Arm" : "Left Leg";

    /// <summary>Up, Right, Front, Left, Back, Down: the cross's cells in editor.js:1626-1628 order.</summary>
    public IReadOnlyList<RegionTileViewModel> Body { get; private set; } = [];

    public IReadOnlyList<RegionTileViewModel> Limb1 { get; private set; } = [];

    public IReadOnlyList<RegionTileViewModel> Limb2 { get; private set; } = [];

    void Build(TemplateMode mode)
    {
        _mode = mode;
        Body = Tiles(BodyPart.Body, BodyLabel, BodyFaces);
        Limb1 = Tiles(BodyPart.RightLimb, LimbLabel1, Limb1Faces);
        Limb2 = Tiles(BodyPart.LeftLimb, LimbLabel2, Limb2Faces);
    }

    RegionTileViewModel[] Tiles(BodyPart part, string partLabel, (Face Face, string Label)[] faces) =>
        faces.Select(f => new RegionTileViewModel(
            _session, _fillStyle, _mode,
            Template.Slot(_mode, part, f.Face).Name,
            f.Label,
            $"{partLabel} {f.Face}",
            isFront: part == BodyPart.Body && f.Face == Face.Front)).ToArray();
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.FillStyleTests|FullyQualifiedName~RBX15.App.Tests.RegionsGroupTests"`
Expected: `Passed!  - Failed:     0, Passed:    39, Skipped:     0, Total:    39` for `RBX15.App.Tests.dll`. FillStyleTests has 22 cases and RegionsGroupTests 17. The 8 swatch cases (7 types plus the zero-degree gradient) run on `StaThread`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 658`; App.Tests `Passed: 230, Failed: 0`; Parity.Tests `Passed: 35`.

Commit (end with your own model's `Co-Authored-By` trailer):

```bash
git add src/RBX15.App/Services/IColorPicker.cs src/RBX15.App/Services/Win32ColorPicker.cs src/RBX15.App/ViewModels/FillStyleViewModel.cs src/RBX15.App/ViewModels/RegionSwatch.cs src/RBX15.App/ViewModels/RegionTileViewModel.cs src/RBX15.App/ViewModels/RegionsGroupViewModel.cs src/RBX15.App.Tests/HomeTestKit.cs src/RBX15.App.Tests/Fakes/FakeColorPicker.cs src/RBX15.App.Tests/FillStyleTests.cs src/RBX15.App.Tests/RegionsGroupTests.cs
git commit -m "feat(app): Fill Style live edit, Template Regions with engine-painted swatches, Win32 color picker"
```

- [ ] **Step 11: Write the failing Home tab view tests** (ledger `regions-grid-layout-shirt` parityCheck "exactly 14", `region-front-highlight-lost`, `layout-template-regions` keyboard reach, `fillstyle-type-buttons`, `regions-hint-text`, `home-settings-group`, `home-edit-group`; contract "hide what doesn't work")

In `src/RBX15.App.Tests/HomeTestKit.cs`, add `using RBX15.App.Tests.Fakes;` after `using System.Collections.Immutable;`, and add this member directly above `public static RegionTileViewModel Tile(`:

```csharp
    public static HomeViewModel Home(EditorSession session, FakeColorPicker? picker = null) =>
        new(session, picker ?? new FakeColorPicker());

```

The file now reads:

```csharp
using System.Collections.Immutable;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>Shared builders for the Home tab tests (Task 3).</summary>
internal static class HomeTestKit
{
    public static SKImage Solid(int w, int h, SKColor c)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(c);
        return surface.Snapshot();
    }

    public static Layer LayerOf(int id, int w = 200, int h = 100) =>
        new() { Id = id, Image = Solid(w, h, SKColors.Red), Name = "Image " + id };

    /// <summary>A session whose document holds the given layers (no history, nothing selected).</summary>
    public static EditorSession SessionWith(params Layer[] layers) =>
        new(new Document { Layers = layers.ToImmutableList() });

    public static HomeViewModel Home(EditorSession session, FakeColorPicker? picker = null) =>
        new(session, picker ?? new FakeColorPicker());

    public static RegionTileViewModel Tile(RegionsGroupViewModel regions, string region) =>
        regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Single(t => t.Region == region);

    /// <summary>Records every PropertyChanged name raised by <paramref name="source"/>.</summary>
    public static List<string?> Track(System.ComponentModel.INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }
}
```

Create `src/RBX15.App.Tests/HomeRibbonViewTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Home tab XAML, loaded headless on StaThread's one shared STA thread, whose Application holds
/// Task 1's theme, so the shared styles (RibbonGroup, RibbonGroupLabel, UpperCase) and the theme keys
/// resolve as in the app. Pins what the view-model tests cannot: the bindings, the 14
/// keyboard-reachable tiles and their clear gestures, the persistent front tint, and that hidden
/// groups are absent.
/// </summary>
public class HomeRibbonViewTests
{
    static HomeRibbon Load(HomeViewModel home)
    {
        var view = new HomeRibbon { DataContext = home };
        StaThread.Pump();
        view.Measure(new Size(2000, 200));
        view.Arrange(new Rect(0, 0, 2000, 200));
        view.UpdateLayout();
        return view;
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

    static Button[] Tiles(HomeRibbon view) =>
        Descendants<Button>(view).Where(b => b.DataContext is RegionTileViewModel).ToArray();

    [Fact] // regions-grid-layout-shirt parityCheck: exactly 14 paintable tiles, each a focusable button with an automation name
    public void The_grid_shows_fourteen_named_focusable_tiles() => StaThread.Run(() =>
    {
        var view = Load(Home(new EditorSession()));
        var tiles = Tiles(view);

        Assert.Equal(14, tiles.Length);
        Assert.All(tiles, t => Assert.True(t.Focusable && t.IsTabStop));
        Assert.Equal(14, tiles.Select(AutomationProperties.GetName).Distinct().Count());
        Assert.Contains("Torso Front", tiles.Select(AutomationProperties.GetName));
    });

    [Fact] // the clear gestures reach ClearCommand: right-click (editor.js:1677) and Delete, Shift+F10, Menu
    public void A_tile_clears_on_right_click_and_from_the_keyboard() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var home = Home(session);
        var view = Load(home);
        var front = Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).Region == "torso_front");

        var gestures = front.InputBindings.Cast<InputBinding>().Select(b => b.Gesture switch
        {
            MouseGesture m => m.MouseAction.ToString(),
            KeyGesture k => (k.Modifiers == ModifierKeys.None ? "" : k.Modifiers + "+") + k.Key,
            _ => "?",
        }).ToArray();
        Assert.Equal(new[] { "RightClick", "Delete", "Shift+F10", "Apps" }, gestures);

        front.Command.Execute(null);
        Assert.NotNull(Tile(home.Regions, "torso_front").Fill);
        front.InputBindings.OfType<MouseBinding>().Single().Command.Execute(null);
        Assert.Null(Tile(home.Regions, "torso_front").Fill);
    });

    [Fact] // region-front-highlight-lost fix: after paint then clear, Front rests on #2a1040 again
    public void The_front_tile_keeps_its_tint_after_paint_and_clear() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var home = Home(session);
        var view = Load(home);
        Border Bd() => (Border)Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).IsFront).Template
            .FindName("Bd", Tiles(view).Single(t => ((RegionTileViewModel)t.DataContext).IsFront));
        var tint = (Color)ColorConverter.ConvertFromString("#2A1040");

        Assert.Equal(tint, ((SolidColorBrush)Bd().Background).Color);
        Tile(home.Regions, "torso_front").PaintCommand.Execute(null);
        view.UpdateLayout();
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)Bd().Background).Color);
        Tile(home.Regions, "torso_front").ClearCommand.Execute(null);
        view.UpdateLayout();
        Assert.Equal(tint, ((SolidColorBrush)Bd().Background).Color);
    });

    [Fact] // the type list's selection is FillStyle.Type, both ways
    public void The_fill_type_list_tracks_the_current_type() => StaThread.Run(() =>
    {
        var home = Home(new EditorSession());
        var view = Load(home);
        var list = Descendants<ListBox>(view).Single(l => AutomationProperties.GetName(l) == "Fill type");

        Assert.Equal("solid", list.SelectedValue);
        list.SelectedIndex = 6;
        Assert.Equal("camo", home.FillStyle.Type);
        home.FillStyle.SetTypeCommand.Execute("dots");
        Assert.Equal(5, list.SelectedIndex);
    });

    [Fact] // the mode button, settings and edit bindings; contract: no Tools, Brush or Placement group in M3a
    public void Mode_settings_and_edit_bind_and_hidden_groups_are_absent() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var view = Load(Home(session));
        var buttons = Descendants<Button>(view).ToArray();
        var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToArray();

        var mode = buttons.Single(b => AutomationProperties.GetName(b) == "Shirt or Pants mode");
        Assert.Equal("SHIRT", mode.Content);
        mode.Command.Execute(null);
        Assert.Equal("PANTS", mode.Content);

        var undo = buttons.Single(b => (string)b.Content == "Undo");
        Assert.True(undo.IsEnabled);                       // the toggle was one step
        Assert.False(buttons.Single(b => (string)b.Content == "Redo").IsEnabled);

        Descendants<CheckBox>(view).Single(c => (string)c.Content == "Grid").IsChecked = true;
        Assert.True(session.Document.ShowGrid);
        Descendants<TextBox>(view).Single().Text = "Hoodie";
        Assert.Equal("Hoodie", session.TemplateTitle);

        Assert.Equal(new[] { "MODE", "EDIT", "SETTINGS", "FILL STYLE", "TEMPLATE REGIONS" },
            texts.Where(t => t is "MODE" or "TOOLS" or "BRUSH" or "PLACEMENT" or "EDIT" or "SETTINGS" or "FILL STYLE" or "TEMPLATE REGIONS"));
        Assert.DoesNotContain(buttons, b => b.Content is "Draw" or "Fit Front" or "Save work" or "Open");
        Assert.Contains("Set a style, then click a region to paint it. Right-click clears.", texts);
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246` naming `HomeViewModel` and `HomeRibbon`.

- [ ] **Step 12: Implement HomeViewModel and the Home tab views**

Create `src/RBX15.App/ViewModels/HomeViewModel.cs`:

```csharp
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab (editor.html:29-110). M3a shows the Mode, Edit, File (Task 4) and Settings groups,
/// then Fill Style and Template Regions, which 4.1 kept on the Layout tab (editor.html:145-171) and
/// which M3a hosts here until M4 brings the Layout tab back. Tools, Brush and Placement stay hidden
/// until they work (contract: hide what does not work yet).
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
}
```

Create `src/RBX15.App/Views/HomeStyles.xaml`. Theme keys are `DynamicResource`. The shared group styles (`RibbonGroup`, `RibbonGroupLabel`) and the `UpperCase` converter are Task 1's, in `Controls.xaml`, and resolve from the application's resources (critic C7). The values are cited in place.

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:engine="clr-namespace:RBX15.Engine;assembly=RBX15.Engine">
    <!--
      Home tab styles. Token brushes, fonts, radii, SecondaryButton and FocusRing come from Task 1's
      Themes as DynamicResource. RibbonGroup, RibbonGroupLabel and the UpperCase converter are Task 1's
      Controls.xaml styles, shared with Task 4's File group; they resolve from the application's
      resources (StaThread holds the same theme in tests). Literal colors below are 4.1 inline styles
      that are not :root tokens; each cites its source line.
    -->

    <!-- #btnModeToggle (editor.html:33; editor.js:1703, :1707): 13px/700, padding 6x18, min-width 80, radius 6, text #050c18, 135deg cyan to magenta; Pants reverses -->
    <Style x:Key="ModeToggleButton" TargetType="Button">
        <Setter Property="FontFamily" Value="{DynamicResource FontBody}" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="Foreground" Value="{DynamicResource Ink950Brush}" />
        <Setter Property="MinWidth" Value="80" />
        <Setter Property="Padding" Value="18,6" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
        <Setter Property="Background">
            <Setter.Value>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#17D4FA" Offset="0" />
                    <GradientStop Color="#F22F89" Offset="1" />
                </LinearGradientBrush>
            </Setter.Value>
        </Setter>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <DataTrigger Binding="{Binding Mode}" Value="{x:Static engine:TemplateMode.Pants}">
                <Setter Property="Background">
                    <Setter.Value>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                            <GradientStop Color="#F22F89" Offset="0" />
                            <GradientStop Color="#17D4FA" Offset="1" />
                        </LinearGradientBrush>
                    </Setter.Value>
                </Setter>
            </DataTrigger>
        </Style.Triggers>
    </Style>

    <!--
      .region-btn (styles.css:475-496): padding 4, mono 9px uppercase, navy, 1px border-1, fg-3, radius 4;
      hover cyan border and text; .filled (declared after :hover, so it wins) magenta border, fg-1 text,
      background = the swatch (the inline style, editor.js:1558). The body front rests on #2a1040
      (editor.js:1627) whenever it is unfilled (region-front-highlight-lost fix).
    -->
    <Style x:Key="RegionTile" TargetType="Button">
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Margin" Value="1" />
        <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
        <Setter Property="AutomationProperties.Name" Value="{Binding AutomationName}" />
        <Setter Property="Command" Value="{Binding PaintCommand}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid>
                        <Border x:Name="SwatchLayer" CornerRadius="{DynamicResource RXsRadius}" Visibility="Collapsed">
                            <Border.Background>
                                <ImageBrush ImageSource="{Binding Swatch}" Stretch="UniformToFill" />
                            </Border.Background>
                        </Border>
                        <Border x:Name="Bd" CornerRadius="{DynamicResource RXsRadius}" BorderThickness="1" Padding="4"
                                Background="{DynamicResource BrandNavyBrush}" BorderBrush="{DynamicResource Border1Brush}">
                            <TextBlock x:Name="Text" Text="{Binding Label, Converter={StaticResource UpperCase}}"
                                       FontFamily="{DynamicResource FontMono}" FontSize="9"
                                       Foreground="{DynamicResource Fg3Brush}" HorizontalAlignment="Center" />
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <DataTrigger Binding="{Binding IsFront}" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="#2A1040" />
                        </DataTrigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource BrandCyanBrush}" />
                            <Setter TargetName="Text" Property="Foreground" Value="{DynamicResource BrandCyanBrush}" />
                        </Trigger>
                        <DataTrigger Binding="{Binding IsFilled}" Value="True">
                            <Setter TargetName="SwatchLayer" Property="Visibility" Value="Visible" />
                            <Setter TargetName="Bd" Property="Background" Value="Transparent" />
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource BrandMagentaBrush}" />
                            <Setter TargetName="Text" Property="Foreground" Value="{DynamicResource Fg1Brush}" />
                        </DataTrigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!--
      A tile with its clear gestures. Right-click is 4.1's contextmenu (editor.js:1677); Delete,
      Shift+F10 and the Menu key reach the same command from the keyboard (spec §4 accessibility).
    -->
    <DataTemplate x:Key="RegionTileTemplate">
        <Button Style="{StaticResource RegionTile}">
            <Button.InputBindings>
                <MouseBinding Gesture="RightClick" Command="{Binding ClearCommand}" />
                <KeyBinding Key="Delete" Command="{Binding ClearCommand}" />
                <KeyBinding Key="F10" Modifiers="Shift" Command="{Binding ClearCommand}" />
                <KeyBinding Key="Apps" Command="{Binding ClearCommand}" />
            </Button.InputBindings>
        </Button>
    </DataTemplate>

    <!-- Limb headings (editor.js:1632, :1641): 9px #666 centered -->
    <Style x:Key="LimbLabel" TargetType="TextBlock">
        <Setter Property="FontSize" Value="9" />
        <Setter Property="Foreground" Value="#666666" />
        <Setter Property="HorizontalAlignment" Value="Center" />
    </Style>

    <!-- .fill-type-btn (styles.css:518-532) at editor.html:148's padding 3x4 and 9px; .active = cyan border, rgba(23,212,250,.12), cyan text -->
    <Style x:Key="FillTypeItem" TargetType="ListBoxItem">
        <Setter Property="Margin" Value="1" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
        <Setter Property="AutomationProperties.Name" Value="{Binding Label}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <Border x:Name="Bd" CornerRadius="{DynamicResource RXsRadius}" BorderThickness="1" Padding="4,3"
                            Background="{DynamicResource BrandNavyBrush}" BorderBrush="{DynamicResource Border1Brush}">
                        <TextBlock x:Name="Text" Text="{Binding Label}" FontFamily="{DynamicResource FontBody}" FontSize="9"
                                   FontWeight="Medium" Foreground="{DynamicResource Fg2Brush}" HorizontalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource BrandCyanBrush}" />
                            <Setter TargetName="Text" Property="Foreground" Value="{DynamicResource BrandCyanBrush}" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource BrandCyanBrush}" />
                            <Setter TargetName="Bd" Property="Background" Value="#1F17D4FA" />
                            <Setter TargetName="Text" Property="Foreground" Value="{DynamicResource BrandCyanBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- #fillColor1/#fillColor2 (editor.html:157-158): 26x22, no border; the face shows the color.
         The 2 DIP corner has no 4.1 source (the input has border:none and no radius): new v5 surface;
         provisional; M3b look sign-off. -->
    <Style x:Key="ColorSwatchButton" TargetType="Button">
        <Setter Property="Width" Value="26" />
        <Setter Property="Height" Value="22" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" CornerRadius="2" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 'Ang' and 'Scale' (editor.html:159, :161): 9px #888 -->
    <Style x:Key="MiniLabel" TargetType="TextBlock">
        <Setter Property="FontSize" Value="9" />
        <Setter Property="Foreground" Value="#888888" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>

    <!-- The fill hint (editor.html:164): 9px #555 -->
    <Style x:Key="HintText" TargetType="TextBlock">
        <Setter Property="FontSize" Value="9" />
        <Setter Property="Foreground" Value="#555555" />
    </Style>

    <!-- #exportTitle (editor.html:100): #1a1a2e, 1px #333, #e0e0e0, 11px, radius 4, padding 3x8, width 140; the hint text in Chromium's darkGray -->
    <Style x:Key="TemplateNameBox" TargetType="TextBox">
        <Setter Property="Width" Value="140" />
        <Setter Property="FontSize" Value="11" />
        <Setter Property="Padding" Value="8,3" />
        <Setter Property="Background" Value="#1A1A2E" />
        <Setter Property="BorderBrush" Value="#333333" />
        <Setter Property="Foreground" Value="#E0E0E0" />
        <Setter Property="CaretBrush" Value="#E0E0E0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="1" CornerRadius="4">
                        <Grid>
                            <ScrollViewer x:Name="PART_ContentHost" Padding="{TemplateBinding Padding}" Focusable="False"
                                          HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden" />
                            <TextBlock x:Name="Placeholder" Text="{TemplateBinding Tag}" Foreground="#A9A9A9"
                                       Margin="10,3,8,3" IsHitTestVisible="False" Visibility="Collapsed" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="Text" Value="">
                            <Setter TargetName="Placeholder" Property="Visibility" Value="Visible" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Mask and Grid labels (editor.html:102-103): 10px #999 -->
    <Style x:Key="SettingsCheckBox" TargetType="CheckBox">
        <Setter Property="FontSize" Value="10" />
        <Setter Property="Foreground" Value="#999999" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}" />
    </Style>
</ResourceDictionary>
```

Create `src/RBX15.App/Views/HomeRibbon.xaml`. Task 4 replaces the `<!-- Task 4: File group (editor.html:85-97) -->` comment with its group.

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
                            AutomationProperties.Name="Shirt or Pants mode" VerticalAlignment="Top" />
                </DockPanel>
            </Border>

            <!-- Edit (editor.html:77-84): .btn .btn-sm .btn-secondary, a centered row with a 4px gap -->
            <Border Style="{StaticResource RibbonGroup}" DataContext="{Binding Edit}">
                <DockPanel>
                    <TextBlock Style="{StaticResource RibbonGroupLabel}" Text="EDIT" />
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Button x:Name="UndoButton" Content="Undo" ToolTip="Undo (Ctrl+Z)" Command="{Binding UndoCommand}"
                                Style="{DynamicResource SecondaryButton}" Padding="10,5" FontSize="11" Margin="0,0,4,0" />
                        <Button x:Name="RedoButton" Content="Redo" ToolTip="Redo (Ctrl+Y / Ctrl+Shift+Z)" Command="{Binding RedoCommand}"
                                Style="{DynamicResource SecondaryButton}" Padding="10,5" FontSize="11" Margin="0,0,4,0" />
                        <Button x:Name="ClearButton" Content="Clear" Command="{Binding ClearCommand}"
                                Style="{DynamicResource SecondaryButton}" Padding="10,5" FontSize="11" />
                    </StackPanel>
                </DockPanel>
            </Border>

            <!-- Task 4: File group (editor.html:85-97) -->

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

Create `src/RBX15.App/Views/HomeRibbon.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Home tab pane. DataContext: <see cref="ViewModels.HomeViewModel"/>.</summary>
public partial class HomeRibbon : UserControl
{
    public HomeRibbon() => InitializeComponent();
}
```

Create `src/RBX15.App/Views/FillStyleGroup.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.FillStyleGroup"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="HomeStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>

    <!-- Fill Style (editor.html:145-167) -->
    <Border Style="{StaticResource RibbonGroup}">
        <DockPanel>
            <TextBlock Style="{StaticResource RibbonGroupLabel}" Text="FILL STYLE" />
            <StackPanel VerticalAlignment="Top">
                <!-- 7 type buttons, 4 columns, 2px gap (editor.html:147-155); the selection is Type -->
                <ListBox x:Name="Types" ItemsSource="{Binding Types}" SelectedValuePath="Type"
                         SelectedValue="{Binding Type}" ItemContainerStyle="{StaticResource FillTypeItem}"
                         AutomationProperties.Name="Fill type" Background="Transparent" BorderThickness="0" Padding="0"
                         ScrollViewer.HorizontalScrollBarVisibility="Disabled" ScrollViewer.VerticalScrollBarVisibility="Disabled">
                    <ListBox.ItemsPanel>
                        <ItemsPanelTemplate>
                            <UniformGrid Columns="4" />
                        </ItemsPanelTemplate>
                    </ListBox.ItemsPanel>
                </ListBox>

                <!-- Colors, Ang and Scale (editor.html:156-163): 4px gap, 2px top margin -->
                <StackPanel Orientation="Horizontal" Margin="0,2,0,0">
                    <Button x:Name="Color1" Style="{StaticResource ColorSwatchButton}" Background="{Binding Color1}"
                            Command="{Binding PickColor1Command}" ToolTip="Color 1" AutomationProperties.Name="Color 1" Margin="0,0,4,0" />
                    <Button x:Name="Color2" Style="{StaticResource ColorSwatchButton}" Background="{Binding Color2}"
                            Command="{Binding PickColor2Command}" ToolTip="Color 2" AutomationProperties.Name="Color 2" Margin="0,0,4,0" />
                    <TextBlock Style="{StaticResource MiniLabel}" Text="Ang" Margin="0,0,4,0" />
                    <Slider x:Name="AngleSlider" Width="44" Minimum="0" Maximum="360" SmallChange="1" LargeChange="1"
                            TickFrequency="1" IsSnapToTickEnabled="True" Value="{Binding Angle}" VerticalAlignment="Center"
                            ToolTip="Angle" AutomationProperties.Name="Angle" Margin="0,0,4,0"
                            Thumb.DragStarted="OnSliderDragStarted" Thumb.DragCompleted="OnSliderDragCompleted" />
                    <TextBlock Style="{StaticResource MiniLabel}" Text="Scale" Margin="0,0,4,0" />
                    <Slider x:Name="ScaleSlider" Width="44" Minimum="4" Maximum="48" SmallChange="1" LargeChange="1"
                            TickFrequency="1" IsSnapToTickEnabled="True" Value="{Binding Scale}" VerticalAlignment="Center"
                            ToolTip="Pattern scale" AutomationProperties.Name="Pattern scale"
                            Thumb.DragStarted="OnSliderDragStarted" Thumb.DragCompleted="OnSliderDragCompleted" />
                </StackPanel>

                <TextBlock x:Name="Hint" Style="{StaticResource HintText}"
                           Text="Set a style, then click a region to paint it. Right-click clears." />
            </StackPanel>
        </DockPanel>
    </Border>
</UserControl>
```

Create `src/RBX15.App/Views/FillStyleGroup.xaml.cs`:

```csharp
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Fill Style group. A slider thumb drag is one undo gesture: DragStarted opens it and
/// DragCompleted (which also fires when capture is lost) closes it (interfaces sheet §1,
/// <c>fillstyle-undo-snapshot-timing</c>). Keyboard and track-click changes arrive outside a drag, so
/// each is its own step.
/// </summary>
public partial class FillStyleGroup : UserControl
{
    IDisposable? _drag;

    public FillStyleGroup() => InitializeComponent();

    void OnSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _drag?.Dispose();
        _drag = (DataContext as FillStyleViewModel)?.BeginSliderDrag();
    }

    void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _drag?.Dispose();
        _drag = null;
    }
}
```

Create `src/RBX15.App/Views/RegionsGrid.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.RegionsGrid"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="HomeStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>

    <!-- Template Regions (editor.html:168-171; buildRegionGrid, editor.js:1624-1649). The last group: no right divider (styles.css:207). -->
    <Border Style="{StaticResource RibbonGroup}" BorderThickness="0">
        <DockPanel>
            <TextBlock Style="{StaticResource RibbonGroupLabel}" Text="TEMPLATE REGIONS" />
            <!-- #regionGrid is a .ribbon-group-items row (styles.css:208): the cross and the limbs side by side, centered, 4px gap -->
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">

                <!-- The body cross: 5 equal columns, 3 rows, 2px gap (editor.js:1625-1629). Body = Up, Right, Front, Left, Back, Down. -->
                <Grid x:Name="Cross" Grid.IsSharedSizeScope="True" VerticalAlignment="Center">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition SharedSizeGroup="CrossColumn" />
                        <ColumnDefinition SharedSizeGroup="CrossColumn" />
                        <ColumnDefinition SharedSizeGroup="CrossColumn" />
                        <ColumnDefinition SharedSizeGroup="CrossColumn" />
                        <ColumnDefinition SharedSizeGroup="CrossColumn" />
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>
                    <ContentPresenter Grid.Row="0" Grid.Column="2" Content="{Binding Body[0]}" ContentTemplate="{StaticResource RegionTileTemplate}" />
                    <ContentPresenter Grid.Row="1" Grid.Column="1" Content="{Binding Body[1]}" ContentTemplate="{StaticResource RegionTileTemplate}" />
                    <ContentPresenter Grid.Row="1" Grid.Column="2" Content="{Binding Body[2]}" ContentTemplate="{StaticResource RegionTileTemplate}" />
                    <ContentPresenter Grid.Row="1" Grid.Column="3" Content="{Binding Body[3]}" ContentTemplate="{StaticResource RegionTileTemplate}" />
                    <ContentPresenter Grid.Row="1" Grid.Column="4" Content="{Binding Body[4]}" ContentTemplate="{StaticResource RegionTileTemplate}" />
                    <ContentPresenter Grid.Row="2" Grid.Column="2" Content="{Binding Body[5]}" ContentTemplate="{StaticResource RegionTileTemplate}" />
                </Grid>

                <!-- The limbs: two equal columns, 4px gap, 4px top margin (editor.js:1630); 4 tiles each, 1px gap (:1633, :1642) -->
                <UniformGrid x:Name="Limbs" Columns="2" Margin="4,4,0,0" VerticalAlignment="Center">
                    <StackPanel Margin="0,0,2,0">
                        <TextBlock Style="{StaticResource LimbLabel}" Text="{Binding LimbLabel1}" />
                        <ItemsControl ItemsSource="{Binding Limb1}" ItemTemplate="{StaticResource RegionTileTemplate}"
                                      Focusable="False" Margin="0,1,0,0">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate>
                                    <UniformGrid Columns="4" />
                                </ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                        </ItemsControl>
                    </StackPanel>
                    <StackPanel Margin="2,0,0,0">
                        <TextBlock Style="{StaticResource LimbLabel}" Text="{Binding LimbLabel2}" />
                        <ItemsControl ItemsSource="{Binding Limb2}" ItemTemplate="{StaticResource RegionTileTemplate}"
                                      Focusable="False" Margin="0,1,0,0">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate>
                                    <UniformGrid Columns="4" />
                                </ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                        </ItemsControl>
                    </StackPanel>
                </UniformGrid>
            </StackPanel>
        </DockPanel>
    </Border>
</UserControl>
```

Create `src/RBX15.App/Views/RegionsGrid.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Template Regions group. DataContext: <see cref="ViewModels.RegionsGroupViewModel"/>.</summary>
public partial class RegionsGrid : UserControl
{
    public RegionsGrid() => InitializeComponent();
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.HomeRibbonViewTests"`
Expected: `Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5` for `RBX15.App.Tests.dll`.

- [ ] **Step 13: Wire the Home tab into the shell**

These edits land on Task 1's files at the anchors Task 1 ruling 15 and 16 define. Type names are fully qualified, so no `using` lines change.

1. In `src/RBX15.App/ViewModels/MainViewModel.cs`, add this property after the last property in the class:

```csharp
    /// <summary>The Home tab (Task 3).</summary>
    public required HomeViewModel Home { get; init; }
```

2. In `src/RBX15.App/Composition.cs`, insert directly above the line `        // M3a: construction lines above this line.`:

```csharp
        var home = new RBX15.App.ViewModels.HomeViewModel(session, new RBX15.App.Services.Win32ColorPicker(
            () => owner() is { } w ? new System.Windows.Interop.WindowInteropHelper(w).Handle : IntPtr.Zero));
```

and directly above the line `            // M3a: required members above this line.`:

```csharp
            Home = home,
```

3. In `src/RBX15.App.Tests/TestShell.cs`, insert directly above the line `            // M3a: required members above this line.`:

```csharp
            Home = new RBX15.App.ViewModels.HomeViewModel(session, new RBX15.App.Tests.Fakes.FakeColorPicker()),
```

4. In `src/RBX15.App/MainWindow.xaml`, replace Task 1's line `<ContentControl x:Name="RibbonPane" Focusable="False" />` with:

```xml
        <ContentControl x:Name="RibbonPane" Focusable="False">
          <views:HomeRibbon DataContext="{Binding Home}" />
        </ContentControl>
```

`HomeRibbon` is the `.ribbon-pane`: it brings its own horizontal `ScrollViewer` and its `12,8` margin, so the host adds neither (no nested scrolling, no doubled padding). The root already declares `xmlns:views`.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`. A `CS9035` error ("Required member 'MainViewModel.Home' must be set") means a test builds `MainViewModel` without `TestShell`. Route that test through `TestShell` (addition A2) rather than setting `Home` inline.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 658, Failed: 0, Skipped: 0`; App.Tests `Passed: 235, Failed: 0`; Parity.Tests `Passed: 35, Failed: 0`.

Run it twice more with `--no-build`. Expected: the same three lines each time. The view and swatch tests create WPF objects on StaThread's one shared STA thread, and a flaky run there means a test leaked a `DispatcherObject` out of its `StaThread.Run` call.

- [ ] **Step 14: Look at it**

Run: `dotnet run --project src/RBX15.App -c Release`. This is a WPF app; the `ELECTRON_RUN_AS_NODE` note does not apply.
Expected:

- The Home tab shows MODE, EDIT, SETTINGS, FILL STYLE and TEMPLATE REGIONS, plus Task 4's FILE group if it has landed. Undo and Redo are greyed out.
- Click Front: the tile fills with a cyan swatch, the canvas front torso turns cyan (Task 2's host), and Undo lights up.
- Pick Gradient: the tile and the canvas both become a left-to-right cyan-to-magenta gradient.
- Drag Ang: the canvas rotates live. On release, a single Undo returns to the pre-drag gradient.
- Right-click Front: it returns to its purple resting tint.
- Click SHIRT: it becomes PANTS, the tiles read the leg headings, and the fills are gone. Undo brings back SHIRT and the fills.
- Tab reaches every tile, and Delete on a painted tile clears it.

This step is a look, not a gate. Write any mismatch with 4.1 into the M3b look-check notes. Letter-spacing (styles.css `letter-spacing`) has no WPF equivalent, and the range accent colors are not ported.

- [ ] **Step 15: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/ViewModels/HomeViewModel.cs src/RBX15.App/Views/HomeStyles.xaml src/RBX15.App/Views/HomeRibbon.xaml src/RBX15.App/Views/HomeRibbon.xaml.cs src/RBX15.App/Views/FillStyleGroup.xaml src/RBX15.App/Views/FillStyleGroup.xaml.cs src/RBX15.App/Views/RegionsGrid.xaml src/RBX15.App/Views/RegionsGrid.xaml.cs src/RBX15.App.Tests/HomeTestKit.cs src/RBX15.App.Tests/HomeRibbonViewTests.cs src/RBX15.App/ViewModels/MainViewModel.cs src/RBX15.App/Composition.cs src/RBX15.App.Tests/TestShell.cs src/RBX15.App/MainWindow.xaml
git commit -m "feat(app): Home tab ribbon with Mode, Edit, Settings, Fill Style and Template Regions"
```

**Ledger evidence this task produces (for Task 5).** Task 5 closes a row only where the row's own parityCheck is met. The test named after each row is the evidence.

- **Met by this task alone:**
  - `regions-click-paint`: Click_with_nothing_selected_paints_the_current_fill_once and PaintRegion_with_an_equal_fill_returns_the_same_document. The row's parityCheck says "2 undo entries". That is 4.1's behavior; v5's fix gives 1. Reword the check to the fix.
  - `regions-click-fit-layer`: FitLayerToRegion_matches_the_ledger_numbers.
  - `regions-right-click-clear`
  - `regions-grid-layout-pants`
  - `fillstyle-live-edit-rule`: An_edit_retargets_regions_with_the_current_key_only. The row's scenario cannot occur through the UI without undo, so the test seeds C directly. Reword.
  - `fillstyle-undo-snapshot-timing`
  - `fillstyle-ui-not-synced-from-region`: the project-load half is M4. Narrow the check to undo.
  - `mode-switch-effects`
  - `mode-switch-undo-and-callers`
  - `restore-mode-switch-side-effects`
  - `claim-mode-per-mode-region-data`
  - `btn-clear`
  - `regions-state-model`: the round-trip half is ProjectIO, M2b.
  - `region-front-highlight-lost`
  - `fills-to-css`: the parityCheck is visual. The swatch tests prove the painter is the canvas's own.
- **Needs Task 2's canvas or a look check before closing:**
  - `overlay-mask-checkbox`
  - `overlay-grid-checkbox`
  - `home-settings-group`
  - `mode-toggle-button`: screenshot.
  - `home-mode-toggle`
  - `regions-grid-layout-shirt`: screenshot half.
  - `layout-fill-style`
  - `layout-template-regions`
  - `fillstyle-angle-scale`: the pixel half is M2a's fills goldens.
  - `fillstyle-color-pickers`: UI automation is M6.
  - `regions-button-swatch-paint`
  - `cross-undo-disabled-style`: Task 1's style.
- **Partly served (M4 keeps them open):**
  - `home-edit-group`: shortcuts.
  - `undo-buttons-shortcuts`
  - `undo-empty-steps`: draw tools and presets.
  - `file-undo-snapshot-scope`
  - `undo-coverage-gaps`
  - `claim-region-click-color-picker`: test docs, M6.
  - `mode-pants-crash-hardcoded-keys` and `bug-pants-mode-region-keys`: the region path only.
  - `fillstyle-type-buttons`: the restore half on project load is M4.

**Divergences this task creates, as Task 5 records them** (critic C12):

- Fill Style and Template Regions on Home: **no row**. It is interim (Task 5 ruling 5): `layout-fill-style` and `layout-template-regions` stay open, and M4 moves both groups back to the Layout tab.
- The tile keyboard clear (Delete, Shift+F10, Menu; ruling 5): folded into `layout-template-regions`' divergence text by Task 5 Step 7 ("Delete, Shift+F10 and the Menu key clear a focused tile, as right-click does."). The row stays open.
- The swatch's own gradient extent (ruling 3): folded into `fills-to-css`' divergence text by Task 5 Step 7. The row stays open.

**Interface additions adopted (Task 3).**

All five are adopted: Task 1 produces A1 to A4 and this task produces A5.

- **A1. Theme resource keys** in Task 1's `Themes/Tokens.xaml` and `Controls.xaml`. Task 3's XAML references the tokens as `DynamicResource` and the shared styles as `StaticResource`.
  - `SolidColorBrush`:
    - `BrandNavyBrush` `#192e44`
    - `BrandCyanBrush` `#17d4fa`
    - `BrandMagentaBrush` `#f22f89`
    - `Fg1Brush` `#ffffff`
    - `Fg2Brush` `#c4cdda`
    - `Fg3Brush` `#8e9bad`
    - `FgMutedBrush` `#5e6b7f`
    - `Border1Brush` `#14FFFFFF` (rgba(255,255,255,.08))
    - `Ink950Brush` `#050c18`
  - `FontFamily`: `FontBody` (Inter) and `FontMono` (JetBrains Mono).
  - `CornerRadius`: `RXsRadius` (4; the draft's `RadiusXs` was renamed to Task 1's key, critic C6).
  - Shared styles and converter (Task 1's `Controls.xaml`, critic C7): `RibbonGroup` (Border), `RibbonGroupLabel` (TextBlock) and `UpperCase` (`RBX15.App.Converters.UpperCaseConverter`).
  - `Style` for `Button`: `SecondaryButton`, which is `.btn` plus `.btn-secondary` (styles.css:375-406) with `:disabled` at opacity .4 (styles.css:414-419). Task 3 sets the `.btn-sm` padding and font size locally.
  - `Style` used as a `FocusVisualStyle`: `FocusRing`, the cyan 2px ring (spec §4).
- **A2. One construction point per process** (Task 1 rulings 15 and 16).
  - `src/RBX15.App/Composition.cs` holds `internal static class Composition { public static MainViewModel Build(EditorSession session, Func<Window?> owner) }`. The body contains the exact line `        // M3a: construction lines above this line.` before `return new MainViewModel(session) {`. The object initializer contains the exact line `            // M3a: required members above this line.`.
  - `src/RBX15.App.Tests/TestShell.cs` holds `internal static class TestShell { public static MainViewModel Main(EditorSession session) }`, with the same two anchor lines. Every Task 1 test that needs a `MainViewModel` goes through it.
  - Why: each later task adds a `required` member to `MainViewModel`, and without one construction point every earlier test breaks with `CS9035`. Tasks 2 and 4 use the same anchors.
- **A3. The Home pane host.** `MainWindow.xaml` declares `xmlns:views="clr-namespace:RBX15.App.Views"`. The Home tab's host is Task 1's exact line `<ContentControl x:Name="RibbonPane" Focusable="False" />`, a container that adds no `ScrollViewer` or padding of its own: `HomeRibbon` is the `.ribbon-pane` and scrolls itself (styles.css:198). The window's `DataContext` is the `MainViewModel`.
- **A4.** `<InternalsVisibleTo Include="RBX15.App.Tests" />` in `RBX15.App.csproj`. Task 1 Step 5 adds it; this task does not touch the project file (critic C17).
- **A5. `RegionTileViewModel.IsFilled`** (`bool`, `Fill is not null`) is a public member beyond interfaces.md §6, because the tile template's `.filled` trigger binds to it. It is additive, and no other task consumes it.

---

### Task 4: Layers and files

**Files:**

- Create (engine): `src/RBX15.Engine/LayerOps.cs`, `src/RBX15.Engine/ExportGuard.cs`
- Create (engine tests): `src/RBX15.Engine.Tests/LayerOpsTests.cs`, `src/RBX15.Engine.Tests/ExportGuardTests.cs`, `src/RBX15.Engine.Tests/ImagesCapsTests.cs`, `src/RBX15.Engine.Tests/ImagesOrientationTests.cs`
- Modify (engine): `src/RBX15.Engine/Images.cs` (caps, `ImageTooLargeException`, EXIF orientation), `src/RBX15.Engine/ProjectIO.cs` (one `catch` widened so an over-cap layer is dropped by name)
- Create (app): `src/RBX15.App/Services/IStatus.cs`, `IFileDialogs.cs`, `IDialogService.cs`, `ImageImporter.cs`, `ImportService.cs`, `Win32FileDialogs.cs`, `ThemedDialogService.cs`; `src/RBX15.App/ViewModels/StatusViewModel.cs`, `LayerThumbnails.cs`, `LayerRowViewModel.cs`, `LayersViewModel.cs`, `FileGroupViewModel.cs`; `src/RBX15.App/Views/ThemedDialog.xaml(.cs)`, `StatusStrip.xaml(.cs)`, `DropZone.xaml(.cs)`, `LayersPanel.xaml(.cs)`, `FileGroup.xaml(.cs)`
- Create (app tests): `src/RBX15.App.Tests/TestFiles.cs`, `Fakes/FakeStatus.cs`, `Fakes/FakeFileDialogs.cs`, `Fakes/FakeDialogService.cs`, `StatusViewModelTests.cs`, `ImageImporterTests.cs`, `ImportServiceTests.cs`, `LayersViewModelTests.cs`, `FileGroupTests.cs`, `ThemedDialogTests.cs`, `Task4ViewTests.cs`, `ShellWiringTests.cs`
- Modify (app, Task 1 to 3 files): `src/RBX15.App/ViewModels/HomeViewModel.cs` (+`File`), `src/RBX15.App/ViewModels/MainViewModel.cs` (+`Layers`, +`Status`), `src/RBX15.App/Views/HomeRibbon.xaml` (File group, at Task 3's anchor), and, printed in full in their final M3a form by Step 21: `src/RBX15.App/MainWindow.xaml` (drop zone, layers panel, status strip), `src/RBX15.App/Composition.cs` (composition, canvas drop and click wiring) and `src/RBX15.App.Tests/TestShell.cs`. Also `src/RBX15.App.Tests/HomeTestKit.cs` (`Home` sets the new `required File`).
- Read only: `editor.js`, `editor.html`, `styles.css` (4.1, untouched), `docs/port/parity-ledger.json`

**Interfaces:**

- Consumes:
  - Engine: `Document`, `Layer` (`Document.cs`, M1); `EditorSnapshot`, `History` (`History.cs`, M2a); `Template.Slot`, `TemplateMode`, `BodyPart`, `Face` (`Template.cs`, M1); `Images.Decode`, `Images.ForDrawing` (`Images.cs`, M1); `ExportNaming.PngFileName` (`ExportNaming.cs`, M1); `Renderer.ExportPng` (`Renderer.cs`, M1); `ProjectIO.Read`, `ProjectIO.AppName`, `ProjectLoad.DroppedLayers` (`ProjectIO.cs`, M2b); `Fill` (`Fills.cs`, M1); test helpers `Px.Solid`, `Px.Render`, `Px.At` (`src/RBX15.Engine.Tests/Px.cs`, M1).
  - Task 1: `EditorSession` (`Document`, `SelectedLayerId`, `NextId`, `TakeId()`, `Apply`, `ApplyWithSelection`, `Select`, `Undo`, `UndoCount`, `IsDirty`, `TemplateTitle`, `Changed`), `SessionChange`, `SessionChangedEventArgs`, `AppInfo.ProductName`, `MainViewModel`, `StaThread.Run`, `StaThread.Pump`, `Composition.Build`, `TestShell.Main`, the `MainWindow.xaml` anchors (`<!-- T4: left panel -->`, `<!-- T4: status strip -->`), the theme keys listed under "Interface additions adopted (Task 4)", `RBX15.App.Tests` (CommunityToolkit.Mvvm `ObservableObject`, `RelayCommand`, `IRelayCommand`).
  - Task 2: `CanvasDrop(IReadOnlyList<string> Paths, Point TemplatePoint)` (`RBX15.App.Controls`), `CanvasViewModel(EditorSession)` with `IRelayCommand<CanvasDrop> DropCommand`, `Action<CanvasDrop>? DropHandler { get; set; }` and `Action<Point>? ClickHandler { get; set; }` (ruling R5).
  - Task 3: `HomeViewModel`, `HomeTestKit.Home`, `Views/HomeRibbon.xaml` and its anchor `<!-- Task 4: File group (editor.html:85-97) -->`, the `Composition.cs` line `var home = ...`.
- Produces (interfaces.md §3.3, §3.4, §4, §6; additions marked "+" are listed at the end of this task):
  - `public static class LayerOps` (namespace `RBX15.Engine`): `FitFraction` (0.85), `DuplicateOffset` (10), `DefaultName(int)`, `Place(SKImage, int, string?, TemplateMode, (double X, double Y)? = null)`, `AddImage(EditorSnapshot, SKImage, int, string?, (double X, double Y)? = null)`, `Remove(EditorSnapshot, int)`, `Duplicate(EditorSnapshot, int, int)`, `ToggleVisibility(Document, int)`, `MoveUp(Document, int)`, `MoveDown(Document, int)`, + `HitTest(Document, double, double) : int?`.
  - `public static class ExportGuard`: `EmptyTemplateMessage`, `HasArt(Document)`.
  - `Images`: `MaxEncodedBytes` (100 MiB), `MaxDimension` (16384), `MaxPixels` (50,000,000), + `CheckEncodedSize(long)`, + `CheckDimensions(int, int)`; `Decode(byte[])` enforces all three before allocating and applies EXIF orientation; `public sealed class ImageTooLargeException(string message) : Exception` (+ base class changed from the sheet; see ruling 1).
  - `RBX15.App.Services`: `IStatus`, `IFileDialogs`, `Win32FileDialogs(Func<Window?>)`, `IDialogService`, `ThemedDialogService(Func<Window?>)`, `ImportedImage`, `ImportFailure`, `ImportResult`, `ImageImporter` (`Extensions`, `OpenFilter`, `LayerName`, `Load`, + four reason constants), `ImportService(EditorSession, IStatus)` (`Import`, + `FailureMessage`).
  - `RBX15.App.ViewModels`: `StatusViewModel : ObservableObject, IStatus`, `LayerRowViewModel`, `LayersViewModel`, `FileGroupViewModel`; `HomeViewModel.File`, `MainViewModel.Layers`, `MainViewModel.Status` (all `required init`).
  - `RBX15.App.Views`: `ThemedDialog`, `StatusStrip`, `DropZone`, `LayersPanel`, + `FileGroup`.
  - Test fakes: `FakeStatus`, `FakeFileDialogs`, `FakeDialogService`; + `TestFiles`.

**What 4.1 does (cited).** Every line below is the shipped `editor.js`, `editor.html` and `styles.css` (4.1, untouched).

- **addLayer** (`editor.js:500-526`): `saveUndo()` first, then `front = SHIRT_REGIONS.torso_front` (`:502`), `scale = Math.min(front.w / img.width, front.h / img.height) * 0.85` (`:503`). With both drop coordinates the image is centered on them (`:505-507`), else centered in `front` (`:509-510`). The layer literal (`:512-519`) takes `id: nextId++` and `name: name || 'Image ' + nextId`, so an unnamed layer is named after id + 1 (`:513-514`). It is pushed on top and selected, and the tool reverts to Select (`:521-523`). In Pants mode `SHIRT_REGIONS` holds only `waist_*`/`rleg_*`/`lleg_*` keys (`switchMode`, `:1700-1701`), so `front` is undefined and addLayer throws after the snapshot (ledger `bug-pants-mode-region-keys`).
- **duplicateLayer** (`:542-560`): a missing id returns (`:544`); otherwise `saveUndo`, a shallow clone with `nextId++`, `name + ' copy'`, `x + 10`, `y + 10` and the same `img` (`:546-553`), spliced at `idx + 1` (`:555-556`) and selected (`:557`).
- **removeLayer** (`:562-568`): `saveUndo` even for a missing id (`:563`), filter, and if the removed layer was selected, select the new topmost layer or none (`:565`).
- **toggleLayerVisibility** (`:570-573`): flips `visible` with no `saveUndo`.
- **selectLayer** (`:575-580`): sets the selection, never a snapshot.
- **Up / Down** (`:846-853`, buttons `:773-774`, titles "Move layer up." and "Move layer down."): swap with index + 1 / index - 1, no-op at the ends, no `saveUndo`.
- **Layer rows** (`updateUI`, `:671-744`): built from the last layer to the first (`:672-675`); each row is a 48x48 thumbnail of `img.src` (object-fit contain, `#000`, radius 4; `:682-685`), a 12px weight-600 ellipsized name at 40% opacity when hidden (`:690-694`), and three buttons in a gap-3 row (`:697`): "Dupe" (title "Duplicate layer"; `:699-703`), "Hide"/"Show" (title "Hide layer"/"Show layer", text `#c4cdda`/`#5e6b7f`; `:705-709`), "Del" (title "Delete layer"; `:711-715`). Row chrome: `styles.css:287-319` (padding 6, min-height 56, radius 6, navy gradient, accent border on hover, cyan border plus tinted gradient plus duo glow when selected). The heading carries the hint "(drag to reorder)" (`editor.html:317`).
- **Hit test** (`:1100-1111` with `pointInRotatedRect`, `:1043-1053`, and `getLayerCenter`, `:1023-1027`): the topmost visible layer whose rotated box holds the point.
- **Drop zone** (`editor.html:311-315`; `editor.js:951-958`; `styles.css:265-282`): heading "Drop art here", text "Drag & drop PNG images" / "or click to browse.", click opens a hidden multi-file input, a drop calls `handleFiles(files)` with no point.
- **Canvas drop** (`:961-986`): the point is `(clientX - rect.left) / zoom`, every file of the drop is centered on it.
- **handleFiles** (`:998-1012`) and **Add image** (`:2692-2708`): `isImageFile` (`:992-996`) accepts an `image/*` MIME or the extensions png, jpg, jpeg, gif, webp, bmp, svg; a non-image is skipped with `console.log` (`:1000`); the name is `f.name.replace(/\.[^.]+$/, '')` (`:1001`, `:2698`); each file is read by its own FileReader, so layers land in decode-completion order with one undo step each; a decode failure is `console.error` only (`:1006-1009`).
- **exportPNG** (`:1500-1539`): `hasArt = layers.some(l => l.visible && l.img) || Object.keys(regionColors).length > 0 || !!bgImage` (`:1504-1505`); without art, `confirm('The template is empty — download a blank PNG anyway?')` (`:1506`) aborts on Cancel; the 585x559 composite always masks (`:1508-1533`); the anchor download names it `(title || 'roblox_shirt')` sanitized plus `_roblox.png` (`:1535-1537`).
- **File group** (`editor.html:85-97`): row 1 "Save work", "Open", "Add image" (title "Add an image file as a layer."); row 2 the primary "Download template" (title "Export the finished 585x559 PNG. Ready to upload to Roblox."); label "File".

**Approved divergences implemented here** (`docs/port/parity-ledger.json`):

- `layers-default-name-offbyone`, `layers-add-placement`: default names match the id.
- `bug-pants-mode-region-keys`, `mode-pants-crash-hardcoded-keys`: placement resolves `Template.Slot(mode, Body, Front)`, so Pants adds work.
- `layers-remove`, `undo-empty-steps`: a missing id changes nothing and records nothing.
- `layers-visibility`, `layers-move-updown`, `file-undo-snapshot-scope`: visibility and Up/Down are one undo step each.
- `layers-list`, `layers-list-render`, `panel-drop-zone`: keyboard-operable rows and drop zone with automation names and the cyan focus ring.
- `drop-filetypes`, `project-load-image-layer`, `io-image-formats`, `image-decode-format-matrix`, `layers-multi-file-async-order`: PNG, JPEG, WebP, BMP, GIF (first frame); SVG refused with a message; given order; one undo step.
- `diag-console-only-errors`, `dialogs-inventory`, `io-alert-inventory`: failures 4.1 logged to the console go to the status strip; the confirm is a themed dialog titled with the product name.
- `io-download-save-dialog`, `file-input-same-file-reselect`: native Open and Save dialogs; a status line after saving.
- `drop-canvas`: geometry here; the stale asset-drag half is M4's (the asset library).
- New (Task 5 adds the row): `image-decode-caps`.

**Contract ambiguities, resolved here** (each pinned by a named test):

1. **`ImageTooLargeException` cannot derive from `InvalidDataException`.** `System.IO.InvalidDataException` is sealed; the sheet's declaration fails with `error CS0509: 'ImageTooLargeException': cannot derive from sealed type 'InvalidDataException'` (measured). It derives from `Exception`, and `ProjectIO.TryDecodeDataUrl`'s catch is widened to `InvalidDataException or ImageTooLargeException` so M4 still drops an over-cap layer by name. Pinned: `A_project_layer_over_the_cap_is_dropped_by_name`.
2. **The caps** keep the sheet's values. 100 MiB is far above any real PNG or JPEG artwork and still bounds the copy `SKData.CreateCopy` makes. 16384 per side is the common GPU texture limit, so no accepted image is wider than a texture the canvas can hold. 50 megapixels admits a 48 MP phone photo (8000x6000) and bounds the decode plus its premultiplied drawing copy near 400 MB. All three are checked before pixel memory exists: the length before reading the file (`ImageImporter`) or before copying the payload (`Decode`), the size from `SKCodec.Info`, which reads only the header. Pinned: `Dimensions_pass_up_to_each_cap` (6), `A_side_over_the_cap_is_refused_from_the_header`, `A_pixel_count_over_the_cap_is_refused_from_the_header`, `An_oversized_file_is_refused_before_reading`.
3. **"In the order picked"** (spec §3; interfaces §9.4) is the order `OpenFileDialog.FileNames` returns, or the order of the `FileDrop` array. Windows does not promise click order; v5 promises the dialog's order, deterministic for a given pick. Pinned: `Files_load_in_the_given_order`, `A_multi_file_import_is_one_undo_step_in_the_given_order`.
4. **Formats.** The ledger's `io-image-formats` divergence text lists PNG/JPG/GIF/WEBP; Este's ruling of 2026-10-14 adds BMP. The ruling wins, and Task 5 rewords that row's divergence. An extension outside the five is refused unread; 4.1's MIME fallback has no Windows equivalent worth keeping (Explorer derives the MIME from the extension). Pinned: `Each_accepted_format_decodes` (5), `Other_extensions_are_refused_without_reading` (3).
5. **Status policy.** One `Import` call shows at most one message, naming every failed file; a call that adds files and fails none clears the strip. A successful export shows "Saved <file>." (`io-download-save-dialog`: "a status message"). Pinned: `A_mixed_drop_...`, `One_failure_reads_as_one_sentence`, `A_clean_import_after_a_failure_clears_the_status`, `Confirm_ok_saves_the_export_named_by_the_title`.
6. **Where the layers panel goes.** 4.1 has one left panel (interfaces §9.1): drop zone, then layers. Task 4 places both there, in that order, below Task 1's anchor. The heading drops 4.1's "(drag to reorder)" hint, because drag reorder is M3b (hide what doesn't work). Up and Down lived in the Selected layer panel (M3b); M3a shows them under the list, disabled at the ends and with no selection (spec §4: every command knows when it can run). Pinned: `Up_and_down_reorder_one_step_each_and_disable_at_the_ends`, `The_layers_panel_shows_one_named_row_per_layer_with_named_buttons`.
7. **EXIF orientation** is applied in `Images.Decode` (Review Focus 1). No ledger row covers it; Task 5 adds `image-decode-exif-orientation` as `keep` (4.1 behavior through Chromium) with a smoke line (S5). The contract does not name it and it changes M2b's project decode path, so the plan header lists it for Este's OK (contract amendment CA4) with the exact cut if he declines.
8. **`LayerOps.HitTest`** (interfaces §9.3) is produced and tested here, and Step 21 wires it: Task 2's canvas click (ruling R5) runs `session.Select(LayerOps.HitTest(session.Document, p.X, p.Y))`, so a click on a layer selects the topmost visible one there and a click on empty canvas deselects (editor.js:1100-1116). Without it a region click could never paint again once a layer exists (critic C13). Pinned: `ShellWiringTests.A_canvas_click_selects_the_topmost_layer_under_it_and_empty_canvas_deselects`.
9. **Thumbnails** render at most 96x96 device pixels (two per DIP of the 48 DIP box), cached per image, and show the raw image as 4.1 did (`img.src`).

**Measured while drafting** (a scratch copy of `main` at `1126e7c`, .NET SDK 10.0.302, CommunityToolkit.Mvvm 8.4.2 from the local cache, with stand-ins for the Task 1 and Task 2 types written from interfaces.md): every file below compiled under `-warnaserror`; Engine.Tests went from 566 to 624 passed and Parity.Tests stayed at 35; the 63 Task 4 App.Tests passed, twice in a row. The Step 3, 9 and 11 failure counts below were measured by removing the implementation.

**Reconciler changes since that run** (not compiled in it; the implementer's build at each step is their check): the theme keys renamed to Task 1's (`FocusRing`, `PanelHeading`, `SecondaryButton`, `PrimaryButton`, `RibbonGroup`, `RibbonGroupLabel`; critic C6), the first heading's `0,0,0,8` margin, the File group label written `FILE`, `StaThread.Pump` in the view tests, provisional-value comments on the dialog and the strip, and Step 21 rewritten onto Task 1's `Composition` and `TestShell` with the canvas click wiring and `ShellWiringTests` (+1 App fact). Cumulative counts: Task 3 leaves Engine.Tests 658, App.Tests 235 and Parity.Tests 35; this task ends at Engine.Tests 716 and App.Tests 299 (+58 and +64).

- [ ] **Step 1: Baseline**

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 658, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`; App.Tests `Passed: 235, Failed: 0`.

- [ ] **Step 2: Write the failing LayerOps tests**

Ledger: `layers-add-placement`, `drop-canvas`, `layers-default-name-offbyone`, `layers-duplicate`, `layers-remove`, `layers-visibility`, `layers-move-updown`, `bug-pants-mode-region-keys`, `select-hit-test`, `select-hit-test-rect-not-alpha`. Create `src/RBX15.Engine.Tests/LayerOpsTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// LayerOps is 4.1's LAYERS section (editor.js:499-582) and Up/Down (editor.js:846-853). Placement
/// literals are 4.1's own doubles: node evaluated addLayer's arithmetic (editor.js:502-512) for each
/// size and printed String(x), String(y), String(scale).
/// </summary>
public class LayerOpsTests
{
    static SKImage Img(int w, int h) => Px.Solid(w, h, SKColors.Red);

    static Layer L(int id, string name = "L", int w = 10, int h = 10) => new() { Id = id, Image = Img(w, h), Name = name };

    static Document Doc(params Layer[] layers) => new() { Layers = [.. layers] };

    static int[] Ids(Document d) => d.Layers.Select(l => l.Id).ToArray();

    [Theory] // layers-default-name-offbyone: v5 names match the id (4.1 used id + 1, editor.js:513-514)
    [InlineData(1, "Image 1")]
    [InlineData(2, "Image 2")]
    [InlineData(42, "Image 42")]
    public void Default_name_matches_the_id(int id, string expected) => Assert.Equal(expected, LayerOps.DefaultName(id));

    [Theory] // layers-add-placement: 85% fit centered in the front slot, 4.1's doubles exactly
    [InlineData(512, 512, TemplateMode.Shirt, 240.6, 83.6, 0.2125)]
    [InlineData(100, 400, TemplateMode.Shirt, 281.4, 83.6, 0.272)]
    [InlineData(400, 100, TemplateMode.Shirt, 240.6, 124.4, 0.272)]
    [InlineData(33, 77, TemplateMode.Shirt, 271.6857142857143, 83.6, 1.412987012987013)]
    [InlineData(512, 512, TemplateMode.Pants, 240.6, 83.6, 0.2125)] // bug-pants-mode-region-keys: waist_front, same geometry
    public void Place_fits_85_percent_centered_in_the_front_slot(int w, int h, TemplateMode mode, double x, double y, double scale)
    {
        var l = LayerOps.Place(Img(w, h), 7, "Art", mode);
        Assert.Equal((x, y, scale), (l.X, l.Y, l.Scale));
        Assert.Equal((7, "Art"), (l.Id, l.Name));
        Assert.Equal((true, 1.0, 0.0, false, false), (l.Visible, l.Opacity, l.Rotation, l.FlipH, l.FlipV));
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate));
        Assert.Equal((0, 0, 0), (l.RShift, l.GShift, l.BShift));
    }

    [Theory] // drop-canvas: the image is centered on the drop point, at the same fit scale
    [InlineData(512, 512, 300, 200, 245.6, 145.6, 0.2125)]
    [InlineData(100, 400, 0, 0, -13.600000000000001, -54.400000000000006, 0.272)]
    [InlineData(33, 77, 584.5, 558.5, 561.1857142857143, 504.1, 1.412987012987013)]
    public void Place_centers_on_the_drop_point(int w, int h, double dx, double dy, double x, double y, double scale)
    {
        var l = LayerOps.Place(Img(w, h), 1, "Art", TemplateMode.Shirt, (dx, dy));
        Assert.Equal((x, y, scale), (l.X, l.Y, l.Scale));
        Assert.Equal(dx, l.X + w * l.Scale / 2, 9);
        Assert.Equal(dy, l.Y + h * l.Scale / 2, 9);
    }

    [Theory] // 4.1: name || 'Image ' + nextId (editor.js:514)
    [InlineData(null)]
    [InlineData("")]
    public void Place_without_a_name_uses_the_default(string? name) => Assert.Equal("Image 3", LayerOps.Place(Img(4, 4), 3, name, TemplateMode.Shirt).Name);

    [Fact] // addLayer pushes on top and selects (editor.js:521-522)
    public void Add_image_pushes_on_top_and_selects_it()
    {
        var s = new EditorSnapshot(Doc(L(1), L(2)), 1);
        var next = LayerOps.AddImage(s, Img(64, 32), 5, "New");
        Assert.Equal(new[] { 1, 2, 5 }, Ids(next.Document));
        Assert.Equal(5, next.SelectedLayerId);
        Assert.Equal("New", next.Document.Layers[^1].Name);
        Assert.Equal(new[] { 1, 2 }, Ids(s.Document));
    }

    [Fact] // ids are unique; EditorSession's NextId only grows (editor.js:1465)
    public void Add_image_refuses_an_id_already_in_the_document() =>
        Assert.Throws<ArgumentException>(() => LayerOps.AddImage(new EditorSnapshot(Doc(L(1)), null), Img(4, 4), 1, "Dup"));

    [Fact] // layers-remove: removing the selected layer selects the new topmost (editor.js:565)
    public void Removing_the_selected_layer_selects_the_new_top_layer()
    {
        var next = LayerOps.Remove(new EditorSnapshot(Doc(L(1), L(2), L(3)), 2), 2);
        Assert.Equal(new[] { 1, 3 }, Ids(next.Document));
        Assert.Equal(3, next.SelectedLayerId);
    }

    [Fact] // layers-remove: removing another layer keeps the selection
    public void Removing_another_layer_keeps_the_selection()
    {
        var next = LayerOps.Remove(new EditorSnapshot(Doc(L(1), L(2), L(3)), 1), 3);
        Assert.Equal(new[] { 1, 2 }, Ids(next.Document));
        Assert.Equal(1, next.SelectedLayerId);
    }

    [Fact]
    public void Removing_the_last_layer_selects_none()
    {
        var next = LayerOps.Remove(new EditorSnapshot(Doc(L(1)), 1), 1);
        Assert.Empty(next.Document.Layers);
        Assert.Null(next.SelectedLayerId);
    }

    [Fact] // undo-empty-steps: 4.1 pushed a snapshot even for a missing id (editor.js:563)
    public void Removing_a_missing_id_returns_the_same_snapshot()
    {
        var s = new EditorSnapshot(Doc(L(1)), 1);
        Assert.Same(s, LayerOps.Remove(s, 9));
    }

    [Fact] // layers-duplicate: index + 1, " copy", +10/+10, same image, selected (editor.js:542-560)
    public void Duplicate_inserts_above_the_original_offset_and_selected()
    {
        var src = L(2, "Logo") with { X = 5, Y = -3, Scale = 0.5, Rotation = 30, Opacity = 0.4, FlipH = true, Brightness = 0, RShift = -20 };
        var s = new EditorSnapshot(Doc(L(1), src, L(3)), 1);
        var next = LayerOps.Duplicate(s, 2, 9);
        Assert.Equal(new[] { 1, 2, 9, 3 }, Ids(next.Document));
        Assert.Equal(9, next.SelectedLayerId);
        var clone = next.Document.Layers[2];
        Assert.Equal(src with { Id = 9, Name = "Logo copy", X = 15, Y = 7 }, clone);
        Assert.Same(src.Image, clone.Image);
    }

    [Fact] // the suffix stacks: 'A copy copy'
    public void Duplicating_a_copy_stacks_the_suffix()
    {
        var once = LayerOps.Duplicate(new EditorSnapshot(Doc(L(1, "A")), 1), 1, 2);
        var twice = LayerOps.Duplicate(once, 2, 3);
        Assert.Equal(new[] { "A", "A copy", "A copy copy" }, twice.Document.Layers.Select(l => l.Name));
    }

    [Fact] // editor.js:544: if (!src) return
    public void Duplicating_a_missing_id_returns_the_same_snapshot()
    {
        var s = new EditorSnapshot(Doc(L(1)), null);
        Assert.Same(s, LayerOps.Duplicate(s, 9, 10));
    }

    [Fact]
    public void Duplicate_refuses_a_new_id_already_in_the_document() =>
        Assert.Throws<ArgumentException>(() => LayerOps.Duplicate(new EditorSnapshot(Doc(L(1), L(2)), null), 1, 2));

    [Fact] // layers-visibility: flips Visible; a missing id is the same document
    public void Toggle_visibility_flips_one_layer()
    {
        var d = Doc(L(1), L(2));
        var hidden = LayerOps.ToggleVisibility(d, 2);
        Assert.Equal((true, false), (hidden.Layers[0].Visible, hidden.Layers[1].Visible));
        Assert.True(LayerOps.ToggleVisibility(hidden, 2).Layers[1].Visible);
        Assert.Same(d, LayerOps.ToggleVisibility(d, 9));
    }

    [Fact] // layers-move-updown: Up swaps with index + 1, no-op at the top (editor.js:846-849)
    public void Move_up_swaps_with_the_layer_above_and_stops_at_the_top()
    {
        var d = Doc(L(1), L(2), L(3));
        Assert.Equal(new[] { 2, 1, 3 }, Ids(LayerOps.MoveUp(d, 1)));
        Assert.Same(d, LayerOps.MoveUp(d, 3));
    }

    [Fact] // layers-move-updown: Down swaps with index - 1, no-op at the bottom (editor.js:850-853)
    public void Move_down_swaps_with_the_layer_below_and_stops_at_the_bottom()
    {
        var d = Doc(L(1), L(2), L(3));
        Assert.Equal(new[] { 1, 3, 2 }, Ids(LayerOps.MoveDown(d, 3)));
        Assert.Same(d, LayerOps.MoveDown(d, 1));
    }

    [Fact]
    public void Moving_a_missing_id_returns_the_same_document()
    {
        var d = Doc(L(1), L(2));
        Assert.Same(d, LayerOps.MoveUp(d, 9));
        Assert.Same(d, LayerOps.MoveDown(d, 9));
    }

    [Fact] // select-hit-test: topmost visible layer under the point (editor.js:1100-1111)
    public void Hit_test_picks_the_topmost_layer_under_the_point()
    {
        var bottom = L(1, w: 100, h: 100);
        var top = L(2, w: 20, h: 20) with { X = 40, Y = 40 };
        var d = Doc(bottom, top);
        Assert.Equal(2, LayerOps.HitTest(d, 50, 50));
        Assert.Equal(1, LayerOps.HitTest(d, 5, 5));
        Assert.Equal(1, LayerOps.HitTest(d, 100, 100)); // the box edge is inclusive (lx <= hw)
        Assert.Null(LayerOps.HitTest(d, 100.5, 50));
    }

    [Fact] // select-hit-test: hidden layers are skipped (editor.js:1103)
    public void Hit_test_skips_hidden_layers()
    {
        var d = Doc(L(1, w: 100, h: 100), L(2, w: 100, h: 100) with { Visible = false });
        Assert.Equal(1, LayerOps.HitTest(d, 50, 50));
        Assert.Null(LayerOps.HitTest(Doc(L(1) with { Visible = false }), 5, 5));
    }

    [Fact] // pointInRotatedRect: the rotated box, not the axis-aligned one (editor.js:1043-1053)
    public void Hit_test_uses_the_rotated_box()
    {
        var d = Doc(L(1, w: 100, h: 20) with { Rotation = 90 }); // center (50, 10); rotated it spans x 40..60, y -40..60
        Assert.Equal(1, LayerOps.HitTest(d, 50, 55));
        Assert.Null(LayerOps.HitTest(d, 5, 10));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0103: The name 'LayerOps' does not exist in the current context` in `LayerOpsTests.cs`.

- [ ] **Step 3: Implement LayerOps**

Create `src/RBX15.Engine/LayerOps.cs`:

```csharp
using System.Globalization;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// Layer edits from 4.1's LAYERS section (editor.js:499-582) and the Up/Down buttons
/// (editor.js:846-853), as pure functions. Each returns the input instance unchanged when it changes
/// nothing, so the caller records no undo step (divergence <c>undo-empty-steps</c>). Undo and
/// selection policy belong to the caller (EditorSession); these only compute the next state.
/// </summary>
public static class LayerOps
{
    /// <summary>addLayer's fit: 85% of the front slot (editor.js:503).</summary>
    public const double FitFraction = 0.85;

    /// <summary>duplicateLayer's offset on both axes (editor.js:550-551).</summary>
    public const double DuplicateOffset = 10;

    /// <summary>
    /// "Image " + id. 4.1 named an unnamed layer after the already-incremented counter, id + 1
    /// (editor.js:513-514); v5 matches the id (divergence <c>layers-default-name-offbyone</c>).
    /// </summary>
    public static string DefaultName(int id) => "Image " + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// addLayer's placement (editor.js:500-526): scale = min(fw/iw, fh/ih) * 0.85 against the front
    /// body slot, centered in that slot, or centered on <paramref name="dropAt"/> (template pixels).
    /// The arithmetic is 4.1's, operation for operation, so the doubles match. 4.1 read
    /// SHIRT_REGIONS.torso_front, which Pants mode leaves undefined, so adding threw there
    /// (ledger <c>bug-pants-mode-region-keys</c>); the slot resolves to waist_front, same geometry.
    /// A null or empty name takes <see cref="DefaultName"/>, as 4.1's <c>name || ...</c> did.
    /// </summary>
    public static Layer Place(SKImage image, int id, string? name, TemplateMode mode, (double X, double Y)? dropAt = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var front = Template.Slot(mode, BodyPart.Body, Face.Front);
        double scale = Math.Min((double)front.W / image.Width, (double)front.H / image.Height) * FitFraction;
        double x, y;
        if (dropAt is { } p)
        {
            x = p.X - image.Width * scale / 2;
            y = p.Y - image.Height * scale / 2;
        }
        else
        {
            x = front.X + (front.W - image.Width * scale) / 2;
            y = front.Y + (front.H - image.Height * scale) / 2;
        }
        return new Layer
        {
            Id = id,
            Image = image,
            Name = string.IsNullOrEmpty(name) ? DefaultName(id) : name,
            X = x,
            Y = y,
            Scale = scale,
        };
    }

    /// <summary>addLayer's tail (editor.js:521-522): <see cref="Place"/>, push on top, select it.</summary>
    public static EditorSnapshot AddImage(EditorSnapshot s, SKImage image, int id, string? name, (double X, double Y)? dropAt = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        RequireFreshId(s.Document, id);
        var layer = Place(image, id, name, s.Document.Mode, dropAt);
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Add(layer) }, id);
    }

    /// <summary>
    /// removeLayer (editor.js:562-568): removing the selected layer selects the new topmost (last)
    /// layer, or none; removing another keeps the selection. A missing id returns <paramref name="s"/>
    /// (4.1 pushed an empty undo step there; divergence <c>layers-remove</c>).
    /// </summary>
    public static EditorSnapshot Remove(EditorSnapshot s, int id)
    {
        ArgumentNullException.ThrowIfNull(s);
        int i = s.Document.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        var layers = s.Document.Layers.RemoveAt(i);
        int? selected = s.SelectedLayerId == id ? (layers.Count > 0 ? layers[^1].Id : null) : s.SelectedLayerId;
        return new EditorSnapshot(s.Document with { Layers = layers }, selected);
    }

    /// <summary>
    /// duplicateLayer (editor.js:542-560): a copy of every field with the new id, name + " copy",
    /// X and Y + 10, the same image, inserted directly above the original and selected. A missing id
    /// returns <paramref name="s"/>.
    /// </summary>
    public static EditorSnapshot Duplicate(EditorSnapshot s, int id, int newId)
    {
        ArgumentNullException.ThrowIfNull(s);
        int i = s.Document.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return s;
        RequireFreshId(s.Document, newId);
        var src = s.Document.Layers[i];
        var clone = src with { Id = newId, Name = src.Name + " copy", X = src.X + DuplicateOffset, Y = src.Y + DuplicateOffset };
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Insert(i + 1, clone) }, newId);
    }

    /// <summary>toggleLayerVisibility (editor.js:570-573). Missing id: the same document.</summary>
    public static Document ToggleVisibility(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        if (i < 0) return doc;
        return doc with { Layers = doc.Layers.SetItem(i, doc.Layers[i] with { Visible = !doc.Layers[i].Visible }) };
    }

    /// <summary>btnMoveUp (editor.js:846-849): swap with the layer above (index + 1). No-op at the top or for a missing id.</summary>
    public static Document MoveUp(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        return i < 0 || i == doc.Layers.Count - 1 ? doc : Swap(doc, i, i + 1);
    }

    /// <summary>btnMoveDown (editor.js:850-853): swap with the layer below (index - 1). No-op at the bottom or for a missing id.</summary>
    public static Document MoveDown(Document doc, int id)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int i = doc.Layers.FindIndex(l => l.Id == id);
        return i <= 0 ? doc : Swap(doc, i, i - 1);
    }

    /// <summary>
    /// The select tool's pick (editor.js:1100-1111 with pointInRotatedRect, :1043-1053): the topmost
    /// visible layer whose rotated box contains the template point, or null. The box, not pixel alpha
    /// (ledger <c>select-hit-test-rect-not-alpha</c>, kept).
    /// </summary>
    public static int? HitTest(Document doc, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(doc);
        for (int i = doc.Layers.Count - 1; i >= 0; i--)
        {
            var l = doc.Layers[i];
            if (l.Visible && Contains(l, x, y)) return l.Id;
        }
        return null;
    }

    /// <summary>pointInRotatedRect (editor.js:1043-1053) with getLayerCenter (editor.js:1023-1027), operation for operation.</summary>
    static bool Contains(Layer l, double px, double py)
    {
        double cx = l.X + l.Image.Width * l.Scale / 2, cy = l.Y + l.Image.Height * l.Scale / 2;
        double rad = -l.Rotation * Math.PI / 180;
        double dx = px - cx, dy = py - cy;
        double lx = dx * Math.Cos(rad) - dy * Math.Sin(rad);
        double ly = dx * Math.Sin(rad) + dy * Math.Cos(rad);
        double hw = l.Image.Width * l.Scale / 2, hh = l.Image.Height * l.Scale / 2;
        return lx >= -hw && lx <= hw && ly >= -hh && ly <= hh;
    }

    static Document Swap(Document doc, int a, int b)
    {
        var la = doc.Layers[a];
        return doc with { Layers = doc.Layers.SetItem(a, doc.Layers[b]).SetItem(b, la) };
    }

    static void RequireFreshId(Document doc, int id)
    {
        if (doc.Layers.Exists(l => l.Id == id))
            throw new ArgumentException($"layer id {id} is already in the document", nameof(id));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.LayerOpsTests"`
Expected: `Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 688, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 235`.

- [ ] **Step 4: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/LayerOps.cs src/RBX15.Engine.Tests/LayerOpsTests.cs
git commit -m "feat(engine): LayerOps for add, duplicate, remove, visibility, reorder and hit test"
```

- [ ] **Step 5: ExportGuard, test first**

Ledger: `export-png-guard`, `io-empty-export-confirm`, `canvas-export-pipeline`. Create `src/RBX15.Engine.Tests/ExportGuardTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>exportPNG's blank-template guard (editor.js:1500-1506; export-png-guard, io-empty-export-confirm).</summary>
public class ExportGuardTests
{
    static Layer Layer(bool visible) => new() { Id = 1, Image = Px.Solid(4, 4, SKColors.Red), Visible = visible };

    [Fact]
    public void The_message_is_4_1_verbatim_with_its_em_dash() =>
        Assert.Equal("The template is empty — download a blank PNG anyway?", ExportGuard.EmptyTemplateMessage);

    [Fact]
    public void A_fresh_document_has_no_art() => Assert.False(ExportGuard.HasArt(new Document()));

    [Fact] // a background color alone, at any opacity, is not art (ledger io-empty-export-confirm)
    public void A_background_color_alone_is_not_art() =>
        Assert.False(ExportGuard.HasArt(new Document { BgColor = "#b71c1c", BgOpacity = 0.5, ShowMask = false, ShowGrid = true }));

    [Fact] // l.visible && l.img: hidden layers do not count (ledger canvas-export-pipeline quirk)
    public void Hidden_layers_only_are_not_art() => Assert.False(ExportGuard.HasArt(new Document { Layers = [Layer(false)] }));

    [Fact]
    public void A_visible_layer_is_art() => Assert.True(ExportGuard.HasArt(new Document { Layers = [Layer(false), Layer(true) with { Id = 2 }] }));

    [Fact] // Object.keys(regionColors).length > 0
    public void A_region_fill_is_art() =>
        Assert.True(ExportGuard.HasArt(new Document { RegionFills = [("torso_front", new Fill("solid", "#17d4fa"))] }));

    [Fact] // !!bgImage
    public void A_background_image_is_art() => Assert.True(ExportGuard.HasArt(new Document { BgImage = Px.Solid(2, 2, SKColors.Blue) }));
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0103: The name 'ExportGuard' does not exist in the current context`.

Create `src/RBX15.Engine/ExportGuard.cs`:

```csharp
namespace RBX15.Engine;

/// <summary>
/// exportPNG's blank-template guard (editor.js:1500-1506; ledger <c>export-png-guard</c> and
/// <c>io-empty-export-confirm</c>, both kept). The caller asks <see cref="EmptyTemplateMessage"/>
/// when <see cref="HasArt"/> is false, and exports only on OK.
/// </summary>
public static class ExportGuard
{
    /// <summary>4.1's confirm text, verbatim with its em dash (editor.js:1506).</summary>
    public const string EmptyTemplateMessage = "The template is empty — download a blank PNG anyway?";

    /// <summary>
    /// editor.js:1504-1505: any visible layer, any region fill, or a background image. A background
    /// color alone is not art, and hidden layers do not count.
    /// </summary>
    public static bool HasArt(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return doc.Layers.Exists(l => l.Visible) || !doc.RegionFills.IsEmpty || doc.BgImage is not null;
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ExportGuardTests"`
Expected: `Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 695, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 235`.

```bash
git add src/RBX15.Engine/ExportGuard.cs src/RBX15.Engine.Tests/ExportGuardTests.cs
git commit -m "feat(engine): ExportGuard for the blank-template confirm"
```

- [ ] **Step 6: Write the failing decode-cap tests**

Ledger: new `image-decode-caps`; `image-decode-format-matrix`, `project-load-untrusted-src` (M4 inherits). Create `src/RBX15.Engine.Tests/ImagesCapsTests.cs`:

```csharp
using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Decode caps for untrusted images (divergence image-decode-caps; 4.1 had none). Over-cap headers
/// are hand-built PNGs: a real IHDR with any size and a token IDAT, so the codec reads the size
/// without a single pixel being allocated.
/// </summary>
public class ImagesCapsTests
{
    /// <summary>A PNG whose IHDR claims width x height (8-bit RGBA) followed by a token IDAT and IEND.</summary>
    internal static byte[] PngHeader(int width, int height)
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(len, Crc32(typed));
        s.Write(len);
    }

    static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    static byte[] RealPng(int w, int h)
    {
        using var img = Px.Solid(w, h, SKColors.Lime);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact] // the caps and their rationale live in Images (interface sheet §3.4)
    public void The_caps_are_100_MiB_16384_a_side_and_50_megapixels()
    {
        Assert.Equal(104_857_600L, Images.MaxEncodedBytes);
        Assert.Equal(16384, Images.MaxDimension);
        Assert.Equal(50_000_000L, Images.MaxPixels);
    }

    [Fact]
    public void The_encoded_size_cap_is_inclusive_and_names_the_size()
    {
        Images.CheckEncodedSize(Images.MaxEncodedBytes);
        var e = Assert.Throws<ImageTooLargeException>(() => Images.CheckEncodedSize(157_286_400));
        Assert.Equal("The file is 150 MB; the limit is 100 MB.", e.Message);
    }

    [Fact] // Decode checks the payload length before copying or parsing it
    public void Decode_refuses_a_payload_over_the_size_cap_before_parsing_it()
    {
        var e = Assert.Throws<ImageTooLargeException>(() => Images.Decode(new byte[Images.MaxEncodedBytes + 1]));
        Assert.StartsWith("The file is 100 MB;", e.Message);
    }

    [Theory] // MaxDimension per side, MaxPixels overall; both inclusive
    [InlineData(16384, 1, true)]
    [InlineData(16385, 1, false)]
    [InlineData(1, 16385, false)]
    [InlineData(10000, 5000, true)]
    [InlineData(10000, 5001, false)]
    [InlineData(7072, 7072, false)]
    public void Dimensions_pass_up_to_each_cap(int w, int h, bool ok)
    {
        if (ok) Images.CheckDimensions(w, h);
        else Assert.Throws<ImageTooLargeException>(() => Images.CheckDimensions(w, h));
    }

    [Fact] // a decompression bomb: a tiny file whose header claims a huge side
    public void A_side_over_the_cap_is_refused_from_the_header()
    {
        var bytes = PngHeader(20000, 20000);
        Assert.True(bytes.Length < 100);
        var e = Assert.Throws<ImageTooLargeException>(() => Images.Decode(bytes));
        Assert.Equal("The image is 20000 x 20000 pixels; the limit is 16384 pixels on a side.", e.Message);
    }

    [Fact] // under the side cap, over the pixel cap
    public void A_pixel_count_over_the_cap_is_refused_from_the_header()
    {
        var e = Assert.Throws<ImageTooLargeException>(() => Images.Decode(PngHeader(8000, 8000)));
        Assert.Equal("The image is 8000 x 8000 pixels (64 megapixels); the limit is 50 megapixels.", e.Message);
    }

    [Fact] // the side cap is inclusive on a real decode
    public void An_image_exactly_at_the_side_cap_decodes()
    {
        using var img = Images.Decode(RealPng(16384, 1));
        Assert.Equal((16384, 1), (img.Width, img.Height));
    }

    [Fact] // M4 inherits: ProjectIO drops an over-cap layer by name (ProjectIO.TryDecodeDataUrl)
    public void A_project_layer_over_the_cap_is_dropped_by_name()
    {
        string Url(byte[] png) => "data:image/png;base64," + Convert.ToBase64String(png);
        var json = new JsonObject
        {
            ["version"] = "4.0",
            ["mode"] = "Shirt",
            ["layers"] = new JsonArray
            {
                new JsonObject { ["id"] = 1, ["name"] = "Fine", ["imgSrc"] = Url(RealPng(2, 2)) },
                new JsonObject { ["id"] = 2, ["name"] = "Too wide", ["imgSrc"] = Url(RealPng(16385, 1)) },
            },
        };
        var load = ProjectIO.Read(json.ToJsonString());
        Assert.Equal(new[] { "Fine" }, load.Document.Layers.Select(l => l.Name));
        Assert.Equal(new[] { "Too wide" }, load.DroppedLayers);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0117: 'Images' does not contain a definition for 'MaxEncodedBytes'` and `error CS0246: The type or namespace name 'ImageTooLargeException' could not be found`.

- [ ] **Step 7: Implement the caps**

In `src/RBX15.Engine/Images.cs`:

1. Add `using System.Globalization;` above `using System.Runtime.CompilerServices;`.
2. Replace the whole `Decode` method (from `    public static SKImage Decode(byte[] encoded)` through its closing brace) with:

```csharp
    /// <summary>Largest encoded file or payload <see cref="Decode"/> accepts: 100 MiB. Checked before any copy or parse.</summary>
    public const long MaxEncodedBytes = 100L * 1024 * 1024;

    /// <summary>Largest width or height <see cref="Decode"/> accepts: 16384, the common GPU texture limit.</summary>
    public const int MaxDimension = 16384;

    /// <summary>
    /// Largest pixel count <see cref="Decode"/> accepts: 50 million. A 48 MP phone photo (8000x6000)
    /// fits; at 4 bytes a pixel the decode plus its premultiplied drawing copy stay near 400 MB.
    /// </summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>
    /// Throws <see cref="ImageTooLargeException"/> when an encoded payload is over
    /// <see cref="MaxEncodedBytes"/>. Import calls it with the file length before reading the file.
    /// </summary>
    public static void CheckEncodedSize(long bytes)
    {
        if (bytes > MaxEncodedBytes)
            throw new ImageTooLargeException(string.Create(CultureInfo.InvariantCulture,
                $"The file is {bytes / 1048576.0:0.#} MB; the limit is {MaxEncodedBytes / 1048576} MB."));
    }

    /// <summary>
    /// Throws <see cref="ImageTooLargeException"/> when either side is over <see cref="MaxDimension"/>
    /// or the pixel count is over <see cref="MaxPixels"/>.
    /// </summary>
    public static void CheckDimensions(int width, int height)
    {
        if (width > MaxDimension || height > MaxDimension)
            throw new ImageTooLargeException(string.Create(CultureInfo.InvariantCulture,
                $"The image is {width} x {height} pixels; the limit is {MaxDimension} pixels on a side."));
        if ((long)width * height > MaxPixels)
            throw new ImageTooLargeException(string.Create(CultureInfo.InvariantCulture,
                $"The image is {width} x {height} pixels ({(long)width * height / 1e6:0.#} megapixels); the limit is {MaxPixels / 1_000_000} megapixels."));
    }

    /// <summary>
    /// Decodes an encoded image (PNG, JPEG, WebP, BMP, GIF; the first frame of an animation) to
    /// un-premultiplied RGBA. Untrusted input: the payload size is checked first, then the
    /// dimensions from the codec header, both before any pixel memory is allocated (divergence
    /// <c>image-decode-caps</c>; 4.1 had no caps). Throws <see cref="ImageTooLargeException"/> over a
    /// cap and <see cref="InvalidDataException"/> for bytes it cannot decode.
    /// </summary>
    public static SKImage Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        CheckEncodedSize(encoded.LongLength);
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("not a supported image");
        CheckDimensions(codec.Info.Width, codec.Info.Height);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException($"image decode failed: {result}");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }
```

3. Append after the closing brace of `public static class Images` (end of file):

```csharp
/// <summary>
/// An image over one of the <see cref="Images"/> decode caps. <see cref="InvalidDataException"/> is
/// sealed, so this derives from <see cref="Exception"/>; every caller that drops undecodable images
/// (ProjectIO.TryDecodeDataUrl, the App's ImageImporter) catches both.
/// </summary>
public sealed class ImageTooLargeException(string message) : Exception(message);
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ImagesCapsTests"`
Expected: `Failed!  - Failed:     1, Passed:    12, Skipped:     0, Total:    13`. The failure is `A_project_layer_over_the_cap_is_dropped_by_name` with `RBX15.Engine.ImageTooLargeException : The image is 16385 x 1 pixels; the limit is 16384 pixels on a side.`: ProjectIO only catches `InvalidDataException` (ruling 1).

- [ ] **Step 8: ProjectIO drops an over-cap layer by name**

In `src/RBX15.Engine/ProjectIO.cs`, `TryDecodeDataUrl`, replace:

```csharp
        try { return Images.Decode(bytes); }
        catch (InvalidDataException) { return null; }
```

with:

```csharp
        try { return Images.Decode(bytes); }
        catch (Exception e) when (e is InvalidDataException or ImageTooLargeException) { return null; }
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ImagesCapsTests|FullyQualifiedName~RBX15.Engine.Tests.ProjectIOTests"`
Expected: `Failed:     0`, with ImagesCapsTests' 13 passing alongside every ProjectIOTests case.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 708, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 235`. Every golden still passes: the caps never touch a frozen input (the largest is the 2160x2160 background of gap-bg-image-prescale).

```bash
git add src/RBX15.Engine/Images.cs src/RBX15.Engine/ProjectIO.cs src/RBX15.Engine.Tests/ImagesCapsTests.cs
git commit -m "feat(engine): image decode caps for untrusted input"
```

- [ ] **Step 9: Write the failing EXIF orientation test**

Review Focus 1. Create `src/RBX15.Engine.Tests/ImagesOrientationTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// A phone JPEG stores its pixels sideways plus an EXIF orientation. Chromium draws an img element
/// with that orientation applied (image-orientation: from-image), so 4.1 showed it upright and fit
/// it by its displayed size. The source is 32x16: a white 16x8 block in the stored top-left corner,
/// black elsewhere (luma only, block-aligned, so JPEG keeps it clean at quality 100).
/// </summary>
public class ImagesOrientationTests
{
    static byte[] JpegWithOrientation(int orientation)
    {
        using var src = Px.Render(32, 16, c =>
        {
            c.Clear(SKColors.Black);
            using var white = new SKPaint { Color = SKColors.White };
            c.DrawRect(SKRect.Create(0, 0, 16, 8), white);
        });
        using var data = src.Encode(SKEncodedImageFormat.Jpeg, 100);
        var jpeg = data.ToArray();
        // APP1 "Exif\0\0", big-endian TIFF, one IFD entry: 0x0112 Orientation, SHORT, count 1.
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    [Theory] // EXIF 1..8: displayed size, and the displayed corner the stored top-left lands in
    [InlineData(1, 32, 16, "TL")]
    [InlineData(2, 32, 16, "TR")]
    [InlineData(3, 32, 16, "BR")]
    [InlineData(4, 32, 16, "BL")]
    [InlineData(5, 16, 32, "TL")]
    [InlineData(6, 16, 32, "TR")]
    [InlineData(7, 16, 32, "BR")]
    [InlineData(8, 16, 32, "BL")]
    public void Exif_orientation_is_applied_like_chromium(int orientation, int w, int h, string corner)
    {
        using var img = Images.Decode(JpegWithOrientation(orientation));
        Assert.Equal((w, h), (img.Width, img.Height));
        // 3 px in from each corner: the white block is 16x8 or 8x16, so only its corner reads white.
        var probes = new Dictionary<string, (int X, int Y)>
        {
            ["TL"] = (3, 3), ["TR"] = (w - 4, 3), ["BR"] = (w - 4, h - 4), ["BL"] = (3, h - 4),
        };
        foreach (var (name, (x, y)) in probes)
        {
            var px = Px.At(img, x, y);
            if (name == corner) Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200, $"{name} should be white, got {px}");
            else Assert.True(px.Red < 55 && px.Green < 55 && px.Blue < 55, $"{name} should be black, got {px}");
        }
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ImagesOrientationTests"`
Expected: `Failed!  - Failed:     7, Passed:     1, Skipped:     0, Total:     8` (orientation 1 passes; 2 to 4 find white in the wrong corner; 5 to 8 have the stored, unswapped size).

- [ ] **Step 10: Apply the orientation in Decode**

In `src/RBX15.Engine/Images.cs`, replace the `Decode` method from Step 7 (its doc comment through its closing brace) with the version below, and add `Orient` directly after it:

```csharp
    /// <summary>
    /// Decodes an encoded image (PNG, JPEG, WebP, BMP, GIF; the first frame of an animation) to
    /// un-premultiplied RGBA. Untrusted input: the payload size is checked first, then the
    /// dimensions from the codec header, both before any pixel memory is allocated (divergence
    /// <c>image-decode-caps</c>; 4.1 had no caps). An EXIF orientation is applied, as Chromium
    /// draws an img element (image-orientation: from-image), so the result has the displayed size.
    /// Throws <see cref="ImageTooLargeException"/> over a cap and <see cref="InvalidDataException"/>
    /// for bytes it cannot decode.
    /// </summary>
    public static SKImage Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        CheckEncodedSize(encoded.LongLength);
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("not a supported image");
        CheckDimensions(codec.Info.Width, codec.Info.Height);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException($"image decode failed: {result}");
        return codec.EncodedOrigin == SKEncodedOrigin.TopLeft
            ? SKImage.FromPixelCopy(bmp.PeekPixels())
            : Orient(bmp, codec.EncodedOrigin);
    }

    /// <summary>
    /// Applies an EXIF orientation as a pure pixel permutation, so the un-premultiplied values stay
    /// exact. Each case maps a displayed pixel (x, y) back to its stored pixel; 5 to 8 swap the sides.
    /// </summary>
    static SKImage Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        int w = source.Width, h = source.Height;
        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int dw = swap ? h : w, dh = swap ? w : h;
        using var dest = new SKBitmap(new SKImageInfo(dw, dh, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var srcMap = source.PeekPixels();
        using var dstMap = dest.PeekPixels();
        var src = srcMap.GetPixelSpan<uint>();
        var dst = dstMap.GetPixelSpan<uint>();
        int srcStride = srcMap.RowBytes / 4, dstStride = dstMap.RowBytes / 4;
        for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                (int sx, int sy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (w - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (w - 1 - x, h - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, h - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (y, h - 1 - x),
                    SKEncodedOrigin.RightBottom => (w - 1 - y, h - 1 - x),
                    SKEncodedOrigin.LeftBottom => (w - 1 - y, x),
                    _ => (x, y),
                };
                dst[y * dstStride + x] = src[sy * srcStride + sx];
            }
        return SKImage.FromPixelCopy(dest.PeekPixels());
    }
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ImagesOrientationTests"`
Expected: `Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 716, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 235`.

```bash
git add src/RBX15.Engine/Images.cs src/RBX15.Engine.Tests/ImagesOrientationTests.cs
git commit -m "fix(engine): apply EXIF orientation on decode, as Chromium draws img elements"
```

- [ ] **Step 11: The status strip's view-model**

Ledger: `diag-console-only-errors`, `dialogs-inventory`. WPF projects drop `System.IO` from the implicit usings, so every App file that touches files says `using System.IO;`.

Create `src/RBX15.App.Tests/StatusViewModelTests.cs`:

```csharp
using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>The status strip's view-model (spec §4; diag-console-only-errors).</summary>
public class StatusViewModelTests
{
    [Fact]
    public void It_starts_hidden()
    {
        var s = new StatusViewModel();
        Assert.Null(s.Message);
        Assert.False(s.IsVisible);
    }

    [Fact]
    public void Show_replaces_the_message_and_raises_both_properties()
    {
        var s = new StatusViewModel();
        var raised = new List<string?>();
        s.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        s.Show("first");
        s.Show("second");
        Assert.Equal("second", s.Message);
        Assert.True(s.IsVisible);
        Assert.Equal(new[] { "Message", "IsVisible", "Message", "IsVisible" }, raised);
    }

    [Fact]
    public void Dismiss_hides_the_strip()
    {
        var s = new StatusViewModel();
        s.Show("x");
        s.DismissCommand.Execute(null);
        Assert.Null(s.Message);
        Assert.False(s.IsVisible);
    }

    [Fact]
    public void An_empty_message_is_refused()
    {
        var s = new StatusViewModel();
        Assert.Throws<ArgumentException>(() => s.Show(""));
        Assert.Throws<ArgumentNullException>(() => s.Show(null!));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'StatusViewModel' could not be found`.

Create `src/RBX15.App/Services/IStatus.cs`:

```csharp
namespace RBX15.App.Services;

/// <summary>
/// The non-blocking status strip (spec §4): failures 4.1 only wrote to the developer console
/// (ledger diag-console-only-errors) and the result of an export (io-download-save-dialog).
/// </summary>
public interface IStatus
{
    /// <summary>Shows <paramref name="message"/>, replacing any message already shown.</summary>
    void Show(string message);

    /// <summary>Hides the strip.</summary>
    void Clear();
}
```

Create `src/RBX15.App/ViewModels/StatusViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The status strip (spec §4, new in v5; ledger diag-console-only-errors, dialogs-inventory). One
/// message at a time; a new message replaces the old one; Dismiss hides it.
/// </summary>
public sealed class StatusViewModel : ObservableObject, IStatus
{
    string? _message;

    public StatusViewModel() => DismissCommand = new RelayCommand(Clear);

    public string? Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(IsVisible));
        }
    }

    public bool IsVisible => _message is not null;

    public IRelayCommand DismissCommand { get; }

    public void Show(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        Message = message;
    }

    public void Clear() => Message = null;
}
```

Create `src/RBX15.App.Tests/Fakes/FakeStatus.cs` (used from Step 13 on):

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Records status calls; <see cref="Current"/> is what the strip would show.</summary>
sealed class FakeStatus : IStatus
{
    public List<string> Shown { get; } = [];
    public int Clears { get; private set; }
    public string? Current { get; private set; }

    public void Show(string message)
    {
        Shown.Add(message);
        Current = message;
    }

    public void Clear()
    {
        Clears++;
        Current = null;
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.StatusViewModelTests"`
Expected: `Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4` for `RBX15.App.Tests.dll`.

```bash
git add src/RBX15.App/Services/IStatus.cs src/RBX15.App/ViewModels/StatusViewModel.cs src/RBX15.App.Tests/StatusViewModelTests.cs src/RBX15.App.Tests/Fakes/FakeStatus.cs
git commit -m "feat(app): status strip view-model"
```

- [ ] **Step 12: ImageImporter, test first**

Ledger: `drop-filetypes`, `io-image-formats`, `image-decode-format-matrix`, `project-load-image-layer`, `layers-multi-file-async-order`, `image-decode-caps`. Create `src/RBX15.App.Tests/TestFiles.cs` (temp image files, plus hand-built BMP, two-frame GIF and bomb-header PNG, since Skia encodes neither BMP nor GIF):

```csharp
using System.Buffers.Binary;
using System.IO;
using System.Text;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>A temp folder of image files for one test; deleted on dispose.</summary>
sealed class TestFiles : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "rbx15-app-tests", Guid.NewGuid().ToString("N"));

    public TestFiles() => Directory.CreateDirectory(Dir);

    public string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(Dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A solid PNG, JPEG or WebP of the given size, encoded by Skia.</summary>
    public string Image(string name, int w, int h, SKColor color, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(color);
        using var image = surface.Snapshot();
        using var data = image.Encode(format, 100);
        return Write(name, data.ToArray());
    }

    /// <summary>A 2x1 24-bit BMP: red then blue (Skia has no BMP encoder).</summary>
    public static byte[] Bmp2x1()
    {
        var b = new byte[62];
        b[0] = (byte)'B'; b[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(2), 62);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(22), 1);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(28), 24);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(34), 8);
        byte[] row = [0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00]; // BGR red, BGR blue, pad
        row.CopyTo(b, 54);
        return b;
    }

    /// <summary>
    /// A 1x1 two-frame GIF: frame 1 red, frame 2 blue (Skia has no GIF encoder). Each frame's LZW
    /// stream is clear, the index, end (min code size 2).
    /// </summary>
    public static byte[] Gif1x1TwoFrames() =>
    [
        .. "GIF89a"u8,
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,       // 1x1, global table of 2
        0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF,             // red, blue
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // frame 1 control
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x44, 0x01, 0x00,                   // index 0
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // frame 2 control
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x4C, 0x01, 0x00,                   // index 1
        0x3B,
    ];

    /// <summary>A PNG whose IHDR claims width x height, with a token IDAT: a decompression bomb's header.</summary>
    public static byte[] PngHeader(int width, int height)
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
        s.Write(four);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(typed);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in typed)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(four, ~crc);
        s.Write(four);
    }

    public void Dispose()
    {
        try { Directory.Delete(Dir, recursive: true); }
        catch (IOException) { }
    }
}
```

Create `src/RBX15.App.Tests/ImageImporterTests.cs`:

```csharp
using System.IO;
using RBX15.App.Services;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// ImageImporter: 4.1's handleFiles and Add image (editor.js:988-1012, :2692-2708) with Este's format
/// ruling of 2026-10-14 (PNG, JPEG, WebP, BMP, GIF first frame; SVG refused) and the decode caps.
/// </summary>
public class ImageImporterTests
{
    static SKColor At(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixel(x, y);
    }

    [Theory] // f.name.replace(/\.[^.]+$/, '') (editor.js:1001, :2698; drop-filetypes)
    [InlineData(@"C:\art\shirt.png", "shirt")]
    [InlineData("a.b.png", "a.b")]
    [InlineData(".png", "")]
    [InlineData("file.", "file.")]
    [InlineData("a..png", "a.")]
    [InlineData("noext", "noext")]
    public void Layer_name_strips_the_last_extension_like_4_1(string path, string expected) =>
        Assert.Equal(expected, ImageImporter.LayerName(path));

    [Fact]
    public void Extensions_and_filter_cover_the_five_formats()
    {
        Assert.Equal(new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" }, ImageImporter.Extensions);
        Assert.Equal("Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif", ImageImporter.OpenFilter);
    }

    [Theory] // each accepted format decodes to its pixel size
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("gif")]
    public void Each_accepted_format_decodes(string format)
    {
        using var files = new TestFiles();
        string path = format switch
        {
            "png" => files.Image("a.png", 6, 4, SKColors.Red),
            "jpg" => files.Image("a.jpg", 6, 4, SKColors.Red, SKEncodedImageFormat.Jpeg),
            "webp" => files.Image("a.webp", 6, 4, SKColors.Red, SKEncodedImageFormat.Webp),
            "bmp" => files.Write("a.bmp", TestFiles.Bmp2x1()),
            _ => files.Write("a.gif", TestFiles.Gif1x1TwoFrames()),
        };
        var result = ImageImporter.Load([path]);
        Assert.Empty(result.Failures);
        var image = Assert.Single(result.Images);
        Assert.Equal("a", image.Name);
        var expected = format switch { "bmp" => (2, 1), "gif" => (1, 1), _ => (6, 4) };
        Assert.Equal(expected, (image.Image.Width, image.Image.Height));
    }

    [Fact] // io-image-formats: first frame only for animated files
    public void An_animated_gif_takes_its_first_frame()
    {
        using var files = new TestFiles();
        var result = ImageImporter.Load([files.Write("anim.gif", TestFiles.Gif1x1TwoFrames())]);
        Assert.Equal(new SKColor(255, 0, 0, 255), At(result.Images[0].Image, 0, 0));
    }

    [Fact] // BMP rows are bottom-up BGR; the decode is left to right as stored
    public void A_bmp_decodes_its_pixels()
    {
        using var files = new TestFiles();
        var image = ImageImporter.Load([files.Write("two.bmp", TestFiles.Bmp2x1())]).Images[0].Image;
        Assert.Equal((SKColors.Red, SKColors.Blue), (At(image, 0, 0), At(image, 1, 0)));
    }

    [Fact] // layers-multi-file-async-order: the given order, not decode completion
    public void Files_load_in_the_given_order()
    {
        using var files = new TestFiles();
        var big = files.Image("big.png", 800, 800, SKColors.Red);
        var small = files.Image("small.png", 2, 2, SKColors.Blue);
        var mid = files.Image("mid.jpeg", 40, 40, SKColors.Green, SKEncodedImageFormat.Jpeg);
        var result = ImageImporter.Load([big, small, mid]);
        Assert.Equal(new[] { "big", "small", "mid" }, result.Images.Select(i => i.Name));
    }

    [Fact] // spec §3: SVG is dropped with a message, even when the bytes are a PNG; never read
    public void Svg_is_refused_by_extension()
    {
        using var files = new TestFiles();
        var path = files.Write("logo.svg", File.ReadAllBytes(files.Image("x.png", 2, 2, SKColors.Red)));
        var result = ImageImporter.Load([path]);
        Assert.Empty(result.Images);
        Assert.Equal(new ImportFailure("logo.svg", ImageImporter.SvgReason), Assert.Single(result.Failures));
        Assert.Equal("SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF.", ImageImporter.SvgReason);
    }

    [Theory] // isImageFile skipped these with a console line (editor.js:1000); v5 names them
    [InlineData("notes.txt")]
    [InlineData("noext")]
    [InlineData("photo.heic")]
    public void Other_extensions_are_refused_without_reading(string name)
    {
        var result = ImageImporter.Load([Path.Combine(Path.GetTempPath(), "rbx15-does-not-exist", name)]);
        Assert.Equal(new ImportFailure(name, ImageImporter.UnsupportedReason), Assert.Single(result.Failures));
    }

    [Fact]
    public void Extensions_match_case_insensitively()
    {
        using var files = new TestFiles();
        var result = ImageImporter.Load([files.Image("UP.PNG", 3, 3, SKColors.Red)]);
        Assert.Equal("UP", Assert.Single(result.Images).Name);
    }

    [Fact] // diag-console-only-errors: a damaged file is reported, not swallowed
    public void A_damaged_image_is_reported()
    {
        using var files = new TestFiles();
        var result = ImageImporter.Load([files.Write("broken.png", [1, 2, 3, 4, 5])]);
        Assert.Equal(new ImportFailure("broken.png", ImageImporter.UndecodableReason), Assert.Single(result.Failures));
    }

    [Fact] // image-decode-caps: a file over 100 MiB fails on its length, before it is read
    public void An_oversized_file_is_refused_before_reading()
    {
        using var files = new TestFiles();
        var path = Path.Combine(files.Dir, "huge.png");
        using (var fs = new FileStream(path, FileMode.CreateNew)) fs.SetLength(Images.MaxEncodedBytes + 1);
        var failure = Assert.Single(ImageImporter.Load([path]).Failures);
        Assert.Equal("huge.png", failure.FileName);
        Assert.StartsWith("The file is 100 MB;", failure.Reason);
    }

    [Fact] // image-decode-caps: the header's size is refused before pixels are allocated
    public void A_decompression_bomb_is_refused_from_its_header()
    {
        using var files = new TestFiles();
        var failure = Assert.Single(ImageImporter.Load([files.Write("bomb.png", TestFiles.PngHeader(30000, 30000))]).Failures);
        Assert.Equal(new ImportFailure("bomb.png", "The image is 30000 x 30000 pixels; the limit is 16384 pixels on a side."), failure);
    }

    [Fact]
    public void A_missing_file_is_reported_as_unreadable()
    {
        var result = ImageImporter.Load([Path.Combine(Path.GetTempPath(), "rbx15-does-not-exist", "gone.png")]);
        Assert.Equal(new ImportFailure("gone.png", ImageImporter.ReadFailedReason), Assert.Single(result.Failures));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0103: The name 'ImageImporter' does not exist in the current context`.

Create `src/RBX15.App/Services/ImageImporter.cs`:

```csharp
using System.IO;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>A decoded file ready to become a layer, named as 4.1 named it.</summary>
public sealed record ImportedImage(SKImage Image, string Name);

/// <summary>A file that did not become a layer, and why, for the status strip.</summary>
public sealed record ImportFailure(string FileName, string Reason);

/// <summary>Everything one pick or drop produced, in the order given.</summary>
public sealed record ImportResult(IReadOnlyList<ImportedImage> Images, IReadOnlyList<ImportFailure> Failures);

/// <summary>
/// Reads and decodes picked or dropped image files (4.1: isImageFile and handleFiles,
/// editor.js:988-1012; Add image, editor.js:2692-2708). Formats are Este's ruling of 2026-10-14:
/// PNG, JPEG, WebP, BMP and GIF (first frame); SVG is refused with a message (ledger drop-filetypes,
/// image-decode-format-matrix, io-image-formats). Every file is read synchronously, in the order
/// given, so the layer order is deterministic (layers-multi-file-async-order).
/// </summary>
public static class ImageImporter
{
    /// <summary>The accepted extensions, lowercase, matched case-insensitively.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    /// <summary>The Open dialog filter.</summary>
    public const string OpenFilter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif";

    public const string SvgReason = "SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF.";
    public const string UnsupportedReason = "Not a supported image type. Use PNG, JPEG, WebP, BMP or GIF.";
    public const string UndecodableReason = "The file isn't a readable image, or it is damaged.";
    public const string ReadFailedReason = "The file couldn't be read.";

    /// <summary>
    /// The layer name: the file name minus its last extension, as 4.1's
    /// <c>f.name.replace(/\.[^.]+$/, '')</c> (editor.js:1001, :2698). "a.b.png" gives "a.b",
    /// ".png" gives "" (the caller's default name then applies), "file." stays "file.".
    /// </summary>
    public static string LayerName(string path)
    {
        string name = Path.GetFileName(path);
        int dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[..dot] : name;
    }

    /// <summary>
    /// Loads <paramref name="paths"/> in the given order. An .svg fails without being read. Any other
    /// extension outside <see cref="Extensions"/> fails without being read (4.1 skipped it with a
    /// console line). A file over <see cref="Images.MaxEncodedBytes"/> fails before it is read. The
    /// rest go through <see cref="Images.Decode"/>; a cap or decode failure is reported, never thrown.
    /// </summary>
    public static ImportResult Load(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var images = new List<ImportedImage>();
        var failures = new List<ImportFailure>();
        foreach (var path in paths)
        {
            string fileName = Path.GetFileName(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".svg") { failures.Add(new(fileName, SvgReason)); continue; }
            if (!Extensions.Contains(ext)) { failures.Add(new(fileName, UnsupportedReason)); continue; }
            try
            {
                Images.CheckEncodedSize(new FileInfo(path).Length);
                images.Add(new ImportedImage(Images.Decode(File.ReadAllBytes(path)), LayerName(path)));
            }
            catch (ImageTooLargeException e) { failures.Add(new(fileName, e.Message)); }
            catch (InvalidDataException) { failures.Add(new(fileName, UndecodableReason)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failures.Add(new(fileName, ReadFailedReason)); }
        }
        return new ImportResult(images, failures);
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ImageImporterTests"`
Expected: `Passed!  - Failed:     0, Passed:    24, Skipped:     0, Total:    24`.

```bash
git add src/RBX15.App/Services/ImageImporter.cs src/RBX15.App.Tests/TestFiles.cs src/RBX15.App.Tests/ImageImporterTests.cs
git commit -m "feat(app): ImageImporter for picked and dropped images"
```

- [ ] **Step 13: ImportService, test first**

Ledger: `layers-multi-file-async-order`, `drop-canvas`, `drop-panel`, `diag-console-only-errors`, `undo-empty-steps`, `bug-pants-mode-region-keys`. Create `src/RBX15.App.Tests/ImportServiceTests.cs`:

```csharp
using System.IO;
using System.Windows;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// ImportService: every file of one pick or drop is one undo step, in the given order (spec §3;
/// layers-multi-file-async-order), and every failure is named on the status strip (diag-console-only-errors).
/// </summary>
public class ImportServiceTests
{
    [Fact] // 4.1: N files = N undo steps in decode order; v5: one step, given order, last on top and selected
    public void A_multi_file_import_is_one_undo_step_in_the_given_order()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        int added = new ImportService(session, status).Import([
            files.Image("first.png", 512, 512, SKColors.Red),
            files.Image("second.png", 100, 400, SKColors.Green),
            files.Image("third.png", 400, 100, SKColors.Blue),
        ]);
        Assert.Equal(3, added);
        Assert.Equal(new[] { "first", "second", "third" }, session.Document.Layers.Select(l => l.Name));
        Assert.Equal(new[] { 1, 2, 3 }, session.Document.Layers.Select(l => l.Id));
        Assert.Equal(3, session.SelectedLayerId);
        Assert.Equal(1, session.UndoCount);
        var l = session.Document.Layers[1];
        Assert.Equal((281.4, 83.6, 0.272), (l.X, l.Y, l.Scale)); // layers-add-placement, 4.1's doubles

        Assert.True(session.Undo());
        Assert.Empty(session.Document.Layers);
        Assert.Null(session.SelectedLayerId);
        Assert.Equal(1, status.Clears);
    }

    [Fact] // Review Focus: a mixed drop keeps the good files and names every bad one
    public void A_mixed_drop_adds_the_good_files_as_one_step_and_names_every_failure()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        int added = new ImportService(session, status).Import([
            files.Image("one.png", 8, 8, SKColors.Red),
            files.Write("logo.svg", [0x3C, 0x73, 0x76, 0x67, 0x3E]),
            files.Write("readme.txt", [0x68, 0x69]),
            files.Write("bomb.png", TestFiles.PngHeader(30000, 30000)),
            files.Image("two.webp", 8, 8, SKColors.Blue, SKEncodedImageFormat.Webp),
        ]);
        Assert.Equal(2, added);
        Assert.Equal(new[] { "one", "two" }, session.Document.Layers.Select(l => l.Name));
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(
            "Couldn't add 3 files. logo.svg: SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF. " +
            "readme.txt: Not a supported image type. Use PNG, JPEG, WebP, BMP or GIF. " +
            "bomb.png: The image is 30000 x 30000 pixels; the limit is 16384 pixels on a side.",
            Assert.Single(status.Shown));
    }

    [Fact]
    public void One_failure_reads_as_one_sentence() =>
        Assert.Equal("Couldn't add logo.svg: " + ImageImporter.SvgReason,
            ImportService.FailureMessage([new ImportFailure("logo.svg", ImageImporter.SvgReason)]));

    [Fact] // no file decoded: no layer, no undo step, nothing dirty
    public void A_pick_where_everything_fails_changes_nothing()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        Assert.Equal(0, new ImportService(session, status).Import([files.Write("a.svg", [1])]));
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
        Assert.False(session.IsDirty);
        Assert.Equal("Couldn't add a.svg: " + ImageImporter.SvgReason, status.Current);
    }

    [Fact] // a clean import clears a stale failure from the strip
    public void A_clean_import_after_a_failure_clears_the_status()
    {
        using var files = new TestFiles();
        var status = new StatusViewModel();
        var import = new ImportService(new EditorSession(), status);
        import.Import([files.Write("a.svg", [1])]);
        Assert.True(status.IsVisible);
        import.Import([files.Image("ok.png", 4, 4, SKColors.Red)]);
        Assert.False(status.IsVisible);
    }

    [Fact] // drop-canvas: the canvas drop handler centers every file on the drop point
    public void A_canvas_drop_centers_every_layer_on_the_drop_point()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var import = new ImportService(session, new FakeStatus());
        var canvas = new CanvasViewModel(session) { DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y)) };
        canvas.DropCommand.Execute(new CanvasDrop([files.Image("a.png", 512, 512, SKColors.Red), files.Image("b.png", 33, 77, SKColors.Blue)], new Point(300, 200)));
        Assert.Equal(1, session.UndoCount);
        foreach (var l in session.Document.Layers)
        {
            Assert.Equal(300, l.X + l.Image.Width * l.Scale / 2, 9);
            Assert.Equal(200, l.Y + l.Image.Height * l.Scale / 2, 9);
        }
        Assert.Equal((245.6, 145.6), (session.Document.Layers[0].X, session.Document.Layers[0].Y));
    }

    [Fact] // bug-pants-mode-region-keys: 4.1 threw here; v5 lands on waist_front
    public void Pants_mode_import_lands_on_the_front_slot()
    {
        using var files = new TestFiles();
        var session = new EditorSession(new Document { Mode = TemplateMode.Pants });
        new ImportService(session, new FakeStatus()).Import([files.Image("p.png", 512, 512, SKColors.Red)]);
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal((240.6, 83.6, 0.2125), (l.X, l.Y, l.Scale));
    }

    [Fact]
    public void An_empty_pick_does_nothing()
    {
        var session = new EditorSession();
        var status = new FakeStatus();
        Assert.Equal(0, new ImportService(session, status).Import([]));
        Assert.Equal((0, 0, 0), (session.UndoCount, status.Shown.Count, status.Clears));
    }

    [Fact] // ids only grow, so an undone layer's id is never reused (editor.js:1465)
    public void Ids_keep_growing_across_undo()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var import = new ImportService(session, new FakeStatus());
        import.Import([files.Image("a.png", 4, 4, SKColors.Red)]);
        session.Undo();
        import.Import([files.Image("b.png", 4, 4, SKColors.Red)]);
        Assert.Equal(2, Assert.Single(session.Document.Layers).Id);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'ImportService' could not be found`.

- [ ] **Step 14: Implement ImportService**

Create `src/RBX15.App/Services/ImportService.cs`:

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

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ImportServiceTests"`
Expected: `Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9`.

```bash
git add src/RBX15.App/Services/ImportService.cs src/RBX15.App.Tests/ImportServiceTests.cs
git commit -m "feat(app): ImportService adds a pick or drop as one undo step"
```

- [ ] **Step 15: The layers panel's view-models, test first**

Ledger: `layers-list`, `layers-list-render`, `layers-select`, `layers-visibility`, `layers-duplicate`, `layers-remove`, `layers-move-updown`, `panel-drop-zone`, `drop-panel`. Create `src/RBX15.App/Services/IFileDialogs.cs`:

```csharp
namespace RBX15.App.Services;

/// <summary>The native file pickers that replace 4.1's hidden file inputs and anchor downloads.</summary>
public interface IFileDialogs
{
    /// <summary>
    /// Multi-select image picker (Add image, drop-zone click). Empty on cancel. The order is the
    /// order the dialog returns its file names in, which v5 defines as "the order picked" (spec §3).
    /// </summary>
    IReadOnlyList<string> OpenImages();

    /// <summary>Save dialog for the exported PNG, starting at <paramref name="suggestedFileName"/>. Null on cancel.</summary>
    string? SavePng(string suggestedFileName);
}
```

Create `src/RBX15.App.Tests/Fakes/FakeFileDialogs.cs`:

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Returns canned picks and records what the Save dialog was asked to suggest.</summary>
sealed class FakeFileDialogs : IFileDialogs
{
    public IReadOnlyList<string> OpenResult { get; set; } = [];
    public string? SaveResult { get; set; }
    public int OpenCalls { get; private set; }
    public List<string> SaveSuggestions { get; } = [];

    public IReadOnlyList<string> OpenImages()
    {
        OpenCalls++;
        return OpenResult;
    }

    public string? SavePng(string suggestedFileName)
    {
        SaveSuggestions.Add(suggestedFileName);
        return SaveResult;
    }
}
```

Create `src/RBX15.App.Tests/LayersViewModelTests.cs`:

```csharp
using System.Windows.Media.Imaging;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The basic layers panel (editor.js:671-744, :846-853; ledger layers-list, layers-list-render,
/// layers-select, layers-visibility, layers-duplicate, layers-remove, layers-move-updown). Thumbnails
/// are WPF bitmaps, so every test runs on an STA thread.
/// </summary>
public class LayersViewModelTests
{
    static SKImage Img(int w = 10, int h = 10)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    /// <summary>A session holding layers named A, B, C... (ids 1..n, A at the bottom) with the top selected.</summary>
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

    static LayersViewModel Vm(EditorSession session, FakeFileDialogs? dialogs = null) =>
        new(session, new ImportService(session, new FakeStatus()), dialogs ?? new FakeFileDialogs());

    static string[] Names(LayersViewModel vm) => vm.Rows.Select(r => r.Name).ToArray();

    [Fact] // top = highest z (editor.js:672-675); .selected marks the selection
    public void Rows_list_the_top_layer_first_and_mark_the_selection() => StaThread.Run(() =>
    {
        var vm = Vm(Seeded(3));
        Assert.Equal(new[] { "C", "B", "A" }, Names(vm));
        Assert.Equal(new[] { true, false, false }, vm.Rows.Select(r => r.IsSelected));
        Assert.Same(vm.Rows[0], vm.Selected);
    });

    [Fact] // layers-list: add 3, newest on top and selected
    public void Imported_files_appear_newest_on_top_and_selected() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var dialogs = new FakeFileDialogs
        {
            OpenResult = [files.Image("one.png", 4, 4, SKColors.Red), files.Image("two.png", 4, 4, SKColors.Red), files.Image("three.png", 4, 4, SKColors.Red)],
        };
        var vm = Vm(session, dialogs);
        vm.BrowseCommand.Execute(null);
        Assert.Equal(1, dialogs.OpenCalls);
        Assert.Equal(new[] { "three", "two", "one" }, Names(vm));
        Assert.Equal("three", vm.Selected?.Name);
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // a cancelled pick adds nothing
    public void A_cancelled_browse_adds_nothing() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session, new FakeFileDialogs());
        vm.BrowseCommand.Execute(null);
        Assert.Empty(vm.Rows);
        Assert.Equal(0, session.UndoCount);
    });

    [Fact] // drop-panel: a zone drop uses the default placement
    public void A_zone_drop_uses_the_default_placement() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        Vm(session).DropFiles([files.Image("z.png", 512, 512, SKColors.Red)]);
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal((240.6, 83.6), (l.X, l.Y));
    });

    [Fact] // layers-select: selecting is never an undo step
    public void Selecting_a_row_selects_the_layer_without_an_undo_step() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.Selected = vm.Rows[2];
        Assert.Equal(1, session.SelectedLayerId);
        Assert.Equal(new[] { false, false, true }, vm.Rows.Select(r => r.IsSelected));
        vm.Rows[1].SelectCommand.Execute(null);
        Assert.Equal(2, session.SelectedLayerId);
        Assert.Same(vm.Rows[1], vm.Selected);
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // layers-visibility fix: one undo step; label, title and dimming follow
    public void Toggling_visibility_is_one_undo_step_and_relabels_the_row() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        var vm = Vm(session);
        var row = vm.Rows[1];
        Assert.Equal(("Hide", "Hide layer", "A"), (row.VisibilityLabel, row.VisibilityToolTip, row.AutomationName));
        int steps = session.UndoCount;
        row.ToggleVisibilityCommand.Execute(null);
        Assert.False(session.Document.Layers[0].Visible);
        Assert.Equal(("Show", "Show layer", "A (hidden)"), (row.VisibilityLabel, row.VisibilityToolTip, row.AutomationName));
        Assert.Equal(steps + 1, session.UndoCount);
        session.Undo();
        Assert.True(row.Visible);
    });

    [Fact] // layers-duplicate: above the original, selected, one step
    public void Duplicate_inserts_the_copy_above_and_selects_it() => StaThread.Run(() =>
    {
        var session = Seeded(2);
        var vm = Vm(session);
        int steps = session.UndoCount;
        vm.Rows[1].DuplicateCommand.Execute(null); // A
        Assert.Equal(new[] { "B", "A copy", "A" }, Names(vm));
        Assert.Equal("A copy", vm.Selected?.Name);
        Assert.Equal(3, vm.Selected?.Id);
        Assert.Equal(steps + 1, session.UndoCount);
    });

    [Fact] // layers-remove: deleting the selected row selects the new top layer, one step
    public void Delete_of_the_selected_row_selects_the_new_top_layer() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        vm.Selected = vm.Rows[1]; // B
        int steps = session.UndoCount;
        vm.Rows[1].DeleteCommand.Execute(null);
        Assert.Equal(new[] { "C", "A" }, Names(vm));
        Assert.Equal("C", vm.Selected?.Name);
        Assert.Equal(steps + 1, session.UndoCount);
    });

    [Fact] // layers-move-updown fix: one step each; disabled at the ends and with no selection
    public void Up_and_down_reorder_one_step_each_and_disable_at_the_ends() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        Assert.False(vm.MoveUpCommand.CanExecute(null)); // C is on top
        Assert.True(vm.MoveDownCommand.CanExecute(null));
        int steps = session.UndoCount;
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "B", "C", "A" }, Names(vm));
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "B", "A", "C" }, Names(vm));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
        Assert.True(vm.MoveUpCommand.CanExecute(null));
        Assert.Equal(steps + 2, session.UndoCount);
        Assert.Equal("C", vm.Selected?.Name);

        session.Select(null);
        Assert.False(vm.MoveUpCommand.CanExecute(null));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
    });

    [Fact] // Review Focus (keyboard): rows are reused, so focus is not torn down by an edit
    public void Rows_are_updated_in_place_across_edits_and_undo() => StaThread.Run(() =>
    {
        var session = Seeded(3);
        var vm = Vm(session);
        var before = vm.Rows.ToDictionary(r => r.Id);
        vm.Rows[2].ToggleVisibilityCommand.Execute(null);
        vm.MoveDownCommand.Execute(null);
        session.Undo();
        session.Undo();
        foreach (var row in vm.Rows) Assert.Same(before[row.Id], row);
        Assert.Equal(new[] { "C", "B", "A" }, Names(vm));
        Assert.True(vm.Rows.All(r => r.Visible));
    });

    [Fact] // 48x48 contain (editor.js:685): the bitmap keeps the aspect inside 96 device pixels
    public void Thumbnails_keep_the_aspect_inside_96_pixels() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(400, 100), session.TakeId(), "Wide"));
        session.ApplyWithSelection(s => LayerOps.AddImage(s, Img(10, 30), session.TakeId(), "Small"));
        var vm = Vm(session);
        var wide = Assert.IsAssignableFrom<BitmapSource>(vm.Rows[1].Thumbnail);
        var small = Assert.IsAssignableFrom<BitmapSource>(vm.Rows[0].Thumbnail);
        Assert.Equal((96, 24), (wide.PixelWidth, wide.PixelHeight));
        Assert.Equal((10, 30), (small.PixelWidth, small.PixelHeight));
        Assert.True(wide.IsFrozen);
    });

    [Fact] // the row's automation name is the layer name; renamed layers follow
    public void The_automation_name_follows_the_layer_name() => StaThread.Run(() =>
    {
        var session = Seeded(1);
        var vm = Vm(session);
        session.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Name = "Logo" }) });
        Assert.Equal(("Logo", "Logo"), (vm.Rows[0].Name, vm.Rows[0].AutomationName));
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'LayersViewModel' could not be found`.

- [ ] **Step 16: Implement the row and list view-models**

Create `src/RBX15.App/ViewModels/LayerThumbnails.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// Layer-row thumbnails: the raw layer image (4.1 used img.src, so no adjustments, rotation or
/// opacity; ledger layers-list-render), downscaled to fit <see cref="PixelSide"/> pixels and frozen.
/// The row shows it at 48x48 DIP with Stretch=Uniform on black (object-fit: contain, editor.js:685).
/// Cached per image, so rebuilding a row never re-renders an unchanged image.
/// </summary>
internal static class LayerThumbnails
{
    /// <summary>Two device pixels per DIP of the 48 DIP box: sharp up to 200% display scale.</summary>
    public const int PixelSide = 96;

    static readonly ConditionalWeakTable<SKImage, BitmapSource> Cache = new();

    public static BitmapSource For(SKImage image) => Cache.GetValue(image, Render);

    static BitmapSource Render(SKImage image)
    {
        double s = Math.Min(1.0, Math.Min((double)PixelSide / image.Width, (double)PixelSide / image.Height));
        int w = Math.Max(1, (int)Math.Round(image.Width * s)), h = Math.Max(1, (int)Math.Round(image.Height * s));
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create thumbnail surface");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(Images.ForDrawing(image), SKRect.Create(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var pixmap = surface.PeekPixels();
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixmap.GetPixels(), pixmap.RowBytes * h, pixmap.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }
}
```

Create `src/RBX15.App/ViewModels/LayerRowViewModel.cs`:

```csharp
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// One layer row (updateUI's list, editor.js:671-744; ledger layers-list, layers-list-render). The
/// row is reused while its layer exists, so keyboard focus survives every edit; the list calls
/// <see cref="Update"/> with the layer's new state.
/// </summary>
public sealed class LayerRowViewModel : ObservableObject
{
    readonly EditorSession _session;
    string _name = "";
    bool _visible;
    bool _isSelected;
    ImageSource? _thumbnail;

    public LayerRowViewModel(EditorSession session, Layer layer, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(layer);
        _session = session;
        Id = layer.Id;
        // selectLayer (editor.js:575-580; layers-select): never an undo step.
        SelectCommand = new RelayCommand(() => _session.Select(Id));
        // toggleLayerVisibility (editor.js:570-573): one undo step in v5 (layers-visibility fix).
        ToggleVisibilityCommand = new RelayCommand(() => _session.Apply(d => LayerOps.ToggleVisibility(d, Id)));
        // duplicateLayer (editor.js:542-560; layers-duplicate).
        DuplicateCommand = new RelayCommand(() =>
        {
            int newId = _session.TakeId();
            _session.ApplyWithSelection(s => LayerOps.Duplicate(s, Id, newId));
        });
        // removeLayer (editor.js:562-568; layers-remove). No confirmation, as 4.1.
        DeleteCommand = new RelayCommand(() => _session.ApplyWithSelection(s => LayerOps.Remove(s, Id)));
        Update(layer, isSelected);
    }

    public int Id { get; }

    public string Name => _name;

    public bool Visible => _visible;

    public bool IsSelected => _isSelected;

    /// <summary>The raw layer image fit in 48x48 (editor.js:682-685).</summary>
    public ImageSource Thumbnail => _thumbnail!;

    /// <summary>editor.js:708: 'Hide' while visible, 'Show' while hidden.</summary>
    public string VisibilityLabel => _visible ? "Hide" : "Show";

    /// <summary>editor.js:707: the button title.</summary>
    public string VisibilityToolTip => _visible ? "Hide layer" : "Show layer";

    /// <summary>The row's name for assistive tech (spec §4 Accessibility; 4.1 rows had none).</summary>
    public string AutomationName => _visible ? _name : _name + " (hidden)";

    public IRelayCommand SelectCommand { get; }

    public IRelayCommand ToggleVisibilityCommand { get; }

    public IRelayCommand DuplicateCommand { get; }

    public IRelayCommand DeleteCommand { get; }

    internal void Update(Layer layer, bool isSelected)
    {
        if (layer.Id != Id) throw new ArgumentException($"row {Id} cannot show layer {layer.Id}", nameof(layer));
        bool nameChanged = SetProperty(ref _name, layer.Name, nameof(Name));
        bool visibleChanged = SetProperty(ref _visible, layer.Visible, nameof(Visible));
        SetProperty(ref _isSelected, isSelected, nameof(IsSelected));
        SetProperty(ref _thumbnail, LayerThumbnails.For(layer.Image), nameof(Thumbnail));
        if (visibleChanged)
        {
            OnPropertyChanged(nameof(VisibilityLabel));
            OnPropertyChanged(nameof(VisibilityToolTip));
        }
        if (nameChanged || visibleChanged) OnPropertyChanged(nameof(AutomationName));
    }
}
```

Create `src/RBX15.App/ViewModels/LayersViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The basic layers panel and its drop zone (editor.html:311-318; ledger layers-list,
/// layers-list-render, layers-select, layers-move-updown, panel-drop-zone, drop-panel). Rows are
/// top first, the highest z-order on top (editor.js:672-675). No drag reorder in M3a.
/// </summary>
public sealed class LayersViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly ImportService _import;
    readonly IFileDialogs _dialogs;
    LayerRowViewModel? _selected;
    bool _syncing;

    public LayersViewModel(EditorSession session, ImportService import, IFileDialogs dialogs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(dialogs);
        _session = session;
        _import = import;
        _dialogs = dialogs;
        MoveUpCommand = new RelayCommand(() => Move(LayerOps.MoveUp), () => SelectedIndex() is int i && i < _session.Document.Layers.Count - 1);
        MoveDownCommand = new RelayCommand(() => Move(LayerOps.MoveDown), () => SelectedIndex() is > 0);
        BrowseCommand = new RelayCommand(Browse);
        _session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Document | SessionChange.Selection)) != 0) Sync();
        };
        Sync();
    }

    /// <summary>Top first: Rows[0] is the last layer in Document.Layers.</summary>
    public ObservableCollection<LayerRowViewModel> Rows { get; } = [];

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

    /// <summary>btnMoveUp (editor.js:846-849): one undo step in v5 (layers-move-updown fix). Disabled at the top.</summary>
    public IRelayCommand MoveUpCommand { get; }

    /// <summary>btnMoveDown (editor.js:850-853). Disabled at the bottom.</summary>
    public IRelayCommand MoveDownCommand { get; }

    /// <summary>The drop zone's click-to-browse (dropZone.onclick, editor.js:954).</summary>
    public IRelayCommand BrowseCommand { get; }

    /// <summary>A drop on the zone: default placement, no drop point (editor.js:956; drop-panel).</summary>
    public void DropFiles(IReadOnlyList<string> paths) => _import.Import(paths);

    void Browse()
    {
        var paths = _dialogs.OpenImages();
        if (paths.Count > 0) _import.Import(paths);
    }

    void Move(Func<Document, int, Document> op)
    {
        if (_session.SelectedLayerId is int id) _session.Apply(d => op(d, id));
    }

    int? SelectedIndex()
    {
        if (_session.SelectedLayerId is not int id) return null;
        int i = _session.Document.Layers.FindIndex(l => l.Id == id);
        return i < 0 ? null : i;
    }

    /// <summary>Reconciles Rows with the document in place: rows are moved, added or removed, never rebuilt.</summary>
    void Sync()
    {
        _syncing = true;
        try
        {
            var layers = _session.Document.Layers;
            int? selectedId = _session.SelectedLayerId;
            var ids = layers.Select(l => l.Id).ToHashSet();
            for (int i = Rows.Count - 1; i >= 0; i--)
                if (!ids.Contains(Rows[i].Id)) Rows.RemoveAt(i);
            for (int target = 0; target < layers.Count; target++)
            {
                var layer = layers[layers.Count - 1 - target];
                int at = -1;
                for (int j = target; j < Rows.Count; j++)
                    if (Rows[j].Id == layer.Id) { at = j; break; }
                if (at < 0) Rows.Insert(target, new LayerRowViewModel(_session, layer, layer.Id == selectedId));
                else
                {
                    if (at != target) Rows.Move(at, target);
                    Rows[target].Update(layer, layer.Id == selectedId);
                }
            }
            // Raised on every sync: a Move or Remove can make the ListBox drop its selection, and the
            // write-back it tries during the sync is ignored (_syncing), so the binding re-reads it here.
            _selected = Rows.FirstOrDefault(r => r.Id == selectedId);
            OnPropertyChanged(nameof(Selected));
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            _syncing = false;
        }
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.LayersViewModelTests"`
Expected: `Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12`.

```bash
git add src/RBX15.App/Services/IFileDialogs.cs src/RBX15.App/ViewModels/LayerThumbnails.cs src/RBX15.App/ViewModels/LayerRowViewModel.cs src/RBX15.App/ViewModels/LayersViewModel.cs src/RBX15.App.Tests/Fakes/FakeFileDialogs.cs src/RBX15.App.Tests/LayersViewModelTests.cs
git commit -m "feat(app): layers panel view-models"
```

- [ ] **Step 17: The File group's view-model, test first**

Ledger: `home-file-group`, `export-png-guard`, `io-empty-export-confirm`, `canvas-export-pipeline`, `io-download-save-dialog`, `io-export-filename`, `project-load-image-layer`. Create `src/RBX15.App/Services/IDialogService.cs`:

```csharp
namespace RBX15.App.Services;

/// <summary>
/// 4.1's blocking alert and confirm (ledger dialogs-inventory), as themed modal dialogs titled
/// <see cref="AppInfo.ProductName"/> (io-empty-export-confirm quirk: Electron showed the page title).
/// </summary>
public interface IDialogService
{
    /// <summary>OK and Cancel; true on OK. Enter is OK, Esc is Cancel, as Chromium's confirm.</summary>
    bool Confirm(string message);

    /// <summary>One OK button.</summary>
    void Alert(string message);
}
```

Create `src/RBX15.App.Tests/Fakes/FakeDialogService.cs`:

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Records every dialog; Confirm answers <see cref="ConfirmAnswer"/>.</summary>
sealed class FakeDialogService : IDialogService
{
    public bool ConfirmAnswer { get; set; }
    public List<string> Confirms { get; } = [];
    public List<string> Alerts { get; } = [];

    public bool Confirm(string message)
    {
        Confirms.Add(message);
        return ConfirmAnswer;
    }

    public void Alert(string message) => Alerts.Add(message);
}
```

Create `src/RBX15.App.Tests/FileGroupTests.cs`:

```csharp
using System.IO;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The File group: Add image (editor.js:2692-2708) and Download template (exportPNG,
/// editor.js:1500-1541; export-png-guard, io-empty-export-confirm, canvas-export-pipeline,
/// io-download-save-dialog, io-export-filename).
/// </summary>
public class FileGroupTests
{
    sealed record Rig(EditorSession Session, FakeFileDialogs Files, FakeDialogService Dialogs, FakeStatus Status, FileGroupViewModel Vm);

    static Rig Make(Document? doc = null)
    {
        var session = new EditorSession(doc);
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var status = new FakeStatus();
        return new Rig(session, files, dialogs, status, new FileGroupViewModel(session, files, dialogs, status, new ImportService(session, status)));
    }

    static SKImage Img()
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    [Fact] // io-empty-export-confirm: the 4.1 text; Cancel aborts before any Save dialog
    public void An_empty_template_asks_first_and_cancel_opens_no_save_dialog()
    {
        var r = Make();
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal(new[] { "The template is empty — download a blank PNG anyway?" }, r.Dialogs.Confirms);
        Assert.Empty(r.Files.SaveSuggestions);
        Assert.Empty(r.Status.Shown);
    }

    [Fact] // Review Focus: hidden layers are not art, so the confirm still appears (editor.js:1504)
    public void Hidden_layers_only_still_ask()
    {
        var r = Make(new Document { Layers = [new Layer { Id = 1, Image = Img(), Visible = false }] });
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Single(r.Dialogs.Confirms);
    }

    [Fact] // OK on the confirm, then the Save dialog named by ExportNaming, then exactly Renderer.ExportPng's bytes
    public void Confirm_ok_saves_the_export_named_by_the_title()
    {
        using var files = new TestFiles();
        var r = Make();
        r.Dialogs.ConfirmAnswer = true;
        r.Session.TemplateTitle = "My Shirt!";
        r.Files.SaveResult = Path.Combine(files.Dir, "out.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal(new[] { "My_Shirt__roblox.png" }, r.Files.SaveSuggestions);
        var bytes = File.ReadAllBytes(r.Files.SaveResult);
        Assert.Equal(Renderer.ExportPng(r.Session.Document), bytes);
        using var png = SKImage.FromEncodedData(bytes);
        Assert.Equal((585, 559), (png.Width, png.Height));
        Assert.Equal("Saved out.png.", r.Status.Current);
    }

    [Fact] // export-png-guard: a region fill is art, so no confirm; an empty title uses roblox_shirt
    public void With_art_there_is_no_confirm()
    {
        using var files = new TestFiles();
        var r = Make(new Document { RegionFills = [("torso_front", new Fill("solid", "#17d4fa"))] });
        r.Files.SaveResult = Path.Combine(files.Dir, "a.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Empty(r.Dialogs.Confirms);
        Assert.Equal(new[] { "roblox_shirt_roblox.png" }, r.Files.SaveSuggestions);
        Assert.True(File.Exists(r.Files.SaveResult));
    }

    [Fact] // io-download-save-dialog: cancel writes nothing and says nothing
    public void Cancelling_the_save_dialog_writes_nothing()
    {
        var r = Make(new Document { BgImage = Img() });
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Single(r.Files.SaveSuggestions);
        Assert.Empty(r.Status.Shown);
    }

    [Fact] // Review Focus: a write that fails (missing folder, locked file) is a status message, never a crash
    public void A_write_failure_shows_a_status_message()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        r.Files.SaveResult = Path.Combine(files.Dir, "no-such-folder", "x.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.StartsWith("Couldn't save x.png: ", r.Status.Current);
        Assert.False(File.Exists(r.Files.SaveResult));
    }

    [Fact] // export is not a document change
    public void Export_is_not_an_undo_step()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        r.Files.SaveResult = Path.Combine(files.Dir, "a.png");
        r.Vm.DownloadTemplateCommand.Execute(null);
        Assert.Equal((0, false), (r.Session.UndoCount, r.Session.IsDirty));
    }

    [Fact] // project-load-image-layer: Add image imports what the picker returns, one step
    public void Add_image_imports_the_picked_files()
    {
        using var files = new TestFiles();
        var r = Make();
        r.Files.OpenResult = [files.Image("p.png", 4, 4, SKColors.Red), files.Image("q.jpg", 4, 4, SKColors.Red, SKEncodedImageFormat.Jpeg)];
        r.Vm.AddImageCommand.Execute(null);
        Assert.Equal(new[] { "p", "q" }, r.Session.Document.Layers.Select(l => l.Name));
        Assert.Equal(1, r.Session.UndoCount);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'FileGroupViewModel' could not be found`.

- [ ] **Step 18: Implement FileGroupViewModel**

Review Focus 3 is pinned by `Hidden_layers_only_still_ask` and `A_write_failure_shows_a_status_message`. Create `src/RBX15.App/ViewModels/FileGroupViewModel.cs`:

```csharp
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's File group in M3a: Add image and Download template (editor.html:85-97; ledger
/// home-file-group). Save work and Open are M4 and stay hidden until they work.
/// </summary>
public sealed class FileGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ImportService _import;

    public FileGroupViewModel(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status, ImportService import)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(import);
        (_session, _files, _dialogs, _status, _import) = (session, files, dialogs, status, import);
        AddImageCommand = new RelayCommand(AddImage);
        DownloadTemplateCommand = new RelayCommand(DownloadTemplate);
    }

    /// <summary>btnLoadImage (editor.js:2692-2708; project-load-image-layer): pick, then one undo step for all.</summary>
    public IRelayCommand AddImageCommand { get; }

    /// <summary>btnExport (exportPNG, editor.js:1500-1541; canvas-export-pipeline, export-png-guard, io-download-save-dialog).</summary>
    public IRelayCommand DownloadTemplateCommand { get; }

    void AddImage()
    {
        var paths = _files.OpenImages();
        if (paths.Count > 0) _import.Import(paths);
    }

    void DownloadTemplate()
    {
        var doc = _session.Document;
        if (!ExportGuard.HasArt(doc) && !_dialogs.Confirm(ExportGuard.EmptyTemplateMessage)) return;
        var path = _files.SavePng(ExportNaming.PngFileName(_session.TemplateTitle));
        if (path is null) return;
        string name = Path.GetFileName(path);
        try
        {
            File.WriteAllBytes(path, Renderer.ExportPng(doc));
            _status.Show($"Saved {name}.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Show($"Couldn't save {name}: {e.Message}");
        }
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.FileGroupTests"`
Expected: `Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 716`; Parity.Tests `Passed: 35`; App.Tests `Passed: 292, Failed: 0`.

```bash
git add src/RBX15.App/Services/IDialogService.cs src/RBX15.App/ViewModels/FileGroupViewModel.cs src/RBX15.App.Tests/Fakes/FakeDialogService.cs src/RBX15.App.Tests/FileGroupTests.cs
git commit -m "feat(app): File group view-model with Download template and Add image"
```

- [ ] **Step 19: The views and the native services, test first**

Ledger: `dialogs-inventory`, `io-alert-inventory`, `layers-list`, `panel-drop-zone`, `home-file-group`, `a11y-focus-rings` (consumes Task 1's `FocusRing`). XML comments cannot hold `--`, so the XAML names CSS tokens without their leading dashes. Create `src/RBX15.App.Tests/ThemedDialogTests.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using RBX15.App.Views;

namespace RBX15.App.Tests;

/// <summary>
/// The themed dialog's shape, checked without showing it: title, message, buttons and their keys
/// (dialogs-inventory, io-empty-export-confirm). Showing it is the manual smoke step.
/// </summary>
public class ThemedDialogTests
{
    static T Find<T>(Window w, string name) where T : class => Assert.IsType<T>(w.FindName(name), exactMatch: false);

    [Fact]
    public void A_confirm_has_the_product_title_and_ok_and_cancel() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Sure?", confirm: true);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", d.Title);
        Assert.Equal("Sure?", Find<TextBlock>(d, "MessageText").Text);
        var ok = Find<Button>(d, "OkButton");
        var cancel = Find<Button>(d, "CancelButton");
        Assert.Equal(("OK", true, false), (ok.Content, ok.IsDefault, ok.IsCancel));
        Assert.Equal(("Cancel", true, Visibility.Visible), (cancel.Content, cancel.IsCancel, cancel.Visibility));
        Assert.True(d.IsConfirm);
    });

    [Fact]
    public void An_alert_has_only_ok() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Done.", confirm: false);
        Assert.Equal(Visibility.Collapsed, Find<Button>(d, "CancelButton").Visibility);
        Assert.False(Find<Button>(d, "CancelButton").IsCancel);
        Assert.False(d.IsConfirm);
    });
}
```

Create `src/RBX15.App.Tests/Task4ViewTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Task 4's views load, bind and expose automation names (spec §4 Accessibility). They are laid out
/// off-screen on StaThread, whose Application holds Task 1's theme. Theme keys are DynamicResource,
/// so a misspelled key would fail silently here; Task 5's ThemeKeyGuardTests catches it.
/// </summary>
public class Task4ViewTests
{
    /// <summary>Runs the dispatcher queue once so the bindings attach (StaThread.Pump), then lays the view out.</summary>
    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(280, 2000));
        e.Arrange(new Rect(0, 0, 280, 2000));
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

    static LayersViewModel Layers(EditorSession session) => new(session, new ImportService(session, new FakeStatus()), new FakeFileDialogs());

    [Fact]
    public void The_layers_panel_shows_one_named_row_per_layer_with_named_buttons() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            using var surface = SKSurface.Create(new SKImageInfo(4, 4));
            var img = surface.Snapshot();
            s = LayerOps.AddImage(s, img, session.TakeId(), "Back");
            return LayerOps.AddImage(s, img, session.TakeId(), "Logo");
        });
        session.Apply(d => LayerOps.ToggleVisibility(d, 1));
        var panel = new LayersPanel { DataContext = Layers(session) };
        Layout(panel);

        var items = Descendants<ListBoxItem>(panel).ToList();
        Assert.Equal(new[] { "Logo", "Back (hidden)" }, items.Select(AutomationProperties.GetName));
        Assert.True(items[0].IsSelected);
        var buttons = Descendants<Button>(items[1]).Select(AutomationProperties.GetName).ToList();
        Assert.Equal(new[] { "Duplicate Back", "Show layer", "Delete Back" }, buttons);
        var all = Descendants<Button>(panel).Select(b => b.Content as string).ToList();
        Assert.Contains("Up", all);
        Assert.Contains("Down", all);
    });

    [Fact]
    public void The_drop_zone_is_a_named_focusable_button_that_accepts_drops() => StaThread.Run(() =>
    {
        var zone = new DropZone { DataContext = Layers(new EditorSession()) };
        Layout(zone);
        var button = Assert.Single(Descendants<Button>(zone));
        Assert.True(button.Focusable);
        Assert.True(button.AllowDrop);
        Assert.Equal("Drop art here. Drag and drop images, or press to browse.", AutomationProperties.GetName(button));
        Assert.Same(((LayersViewModel)zone.DataContext).BrowseCommand, button.Command);
    });

    [Fact]
    public void The_file_group_shows_add_image_and_download_template_only() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var status = new FakeStatus();
        var group = new FileGroup { DataContext = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)) };
        Layout(group);
        var labels = Descendants<Button>(group).Select(b => b.Content as string).ToArray();
        Assert.Equal(new[] { "Add image", "Download template" }, labels);
    });

    [Fact]
    public void The_status_strip_collapses_without_a_message() => StaThread.Run(() =>
    {
        var vm = new StatusViewModel();
        var strip = new StatusStrip { DataContext = vm };
        Layout(strip);
        var border = Assert.IsType<Border>(strip.FindName("Strip"));
        Assert.Equal(Visibility.Collapsed, border.Visibility);
        vm.Show("Couldn't add a.svg: no.");
        Assert.Equal(Visibility.Visible, border.Visibility);
        Assert.Equal("Couldn't add a.svg: no.", Assert.IsType<TextBlock>(strip.FindName("MessageText")).Text);
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'ThemedDialog' could not be found` (and the same for `LayersPanel`, `DropZone`, `FileGroup`, `StatusStrip`).

- [ ] **Step 20: Implement the views and native services**

Create `src/RBX15.App/Views/ThemedDialog.xaml`:

```xml
<Window x:Class="RBX15.App.Views.ThemedDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight"
        ShowInTaskbar="False"
        MinWidth="360"
        MaxWidth="480"
        Background="{DynamicResource Bg1Brush}"
        Foreground="{DynamicResource Fg2Brush}"
        FontFamily="{DynamicResource FontBody}"
        UseLayoutRounding="True">
    <!-- 4.1's alert/confirm (dialogs-inventory) as a themed modal. The title is set in code to
         AppInfo.ProductName. Buttons follow Chromium's confirm: OK then Cancel, Enter = OK, Esc = Cancel.
         4.1's confirm is Chromium's native box, so it has no CSS to copy: MinWidth 360, MaxWidth 480,
         the 20,18,20,16 padding, the 13px message and its 18 DIP bottom margin are a new v5 surface;
         provisional; M3b look sign-off. The buttons are .btn-sm (padding 5px 10px, 11px, styles.css:407). -->
    <Border BorderBrush="{DynamicResource BorderAccentBrush}" BorderThickness="0,1,0,0" Padding="20,18,20,16">
        <StackPanel>
            <TextBlock x:Name="MessageText"
                       TextWrapping="Wrap"
                       FontSize="13"
                       Margin="0,0,0,18"
                       AutomationProperties.Name="Message" />
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button x:Name="OkButton"
                        Content="OK"
                        IsDefault="True"
                        MinWidth="72"
                        Padding="10,5"
                        FontSize="11"
                        Style="{DynamicResource PrimaryButton}"
                        Click="OnOk" />
                <Button x:Name="CancelButton"
                        Content="Cancel"
                        IsCancel="True"
                        MinWidth="72"
                        Padding="10,5"
                        FontSize="11"
                        Margin="8,0,0,0"
                        Style="{DynamicResource SecondaryButton}" />
            </StackPanel>
        </StackPanel>
    </Border>
</Window>
```

Create `src/RBX15.App/Views/ThemedDialog.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;

namespace RBX15.App.Views;

/// <summary>
/// The minimal themed dialog: a message with OK, plus Cancel for a confirm. Shown modally by
/// <see cref="Services.ThemedDialogService"/>; <see cref="Window.DialogResult"/> is true only on OK.
/// </summary>
public partial class ThemedDialog : Window
{
    public ThemedDialog(string message, bool confirm)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        InitializeComponent();
        Title = AppInfo.ProductName;
        MessageText.Text = message;
        IsConfirm = confirm;
        if (!confirm)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            CancelButton.IsCancel = false;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close(); // an alert closes on Esc too
            };
        }
    }

    public bool IsConfirm { get; }

    void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
```

Create `src/RBX15.App/Services/ThemedDialogService.cs`:

```csharp
using System.Windows;
using RBX15.App.Views;

namespace RBX15.App.Services;

/// <summary><see cref="IDialogService"/> over <see cref="ThemedDialog"/>, owned by the main window when it is showing.</summary>
public sealed class ThemedDialogService(Func<Window?> owner) : IDialogService
{
    public bool Confirm(string message) => Show(message, confirm: true);

    public void Alert(string message) => Show(message, confirm: false);

    bool Show(string message, bool confirm)
    {
        var dialog = new ThemedDialog(message, confirm);
        if (owner() is { IsVisible: true } o) dialog.Owner = o;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }
}
```

Create `src/RBX15.App/Services/Win32FileDialogs.cs`:

```csharp
using System.Windows;
using Microsoft.Win32;

namespace RBX15.App.Services;

/// <summary>
/// The WPF wrappers over the Windows common file dialogs (no WinForms). Replaces 4.1's hidden
/// <c>&lt;input type=file&gt;</c> elements (editor.html:108-109, :314) and the anchor-download Save As
/// (editor.js:1535-1539; ledger io-download-save-dialog). A native dialog returns on every pick, so
/// re-picking the same file works (file-input-same-file-reselect).
/// </summary>
public sealed class Win32FileDialogs(Func<Window?> owner) : IFileDialogs
{
    public IReadOnlyList<string> OpenImages()
    {
        var dialog = new OpenFileDialog { Filter = ImageImporter.OpenFilter, Multiselect = true };
        return Show(dialog) ? dialog.FileNames : [];
    }

    public string? SavePng(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "PNG image|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    bool Show(CommonDialog dialog) => (owner() is { } o ? dialog.ShowDialog(o) : dialog.ShowDialog()) == true;
}
```

Create `src/RBX15.App/Views/StatusStrip.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.StatusStrip"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- The status strip (spec §4, new in v5). DataContext: StatusViewModel. Collapsed when there is
         no message. New look, built from 4.1 tokens: bg-1 and border-accent (styles.css:51, :65). The
         12,6 padding, the 12px text and its 12 DIP gap to the button have no 4.1 source: new v5
         surface; provisional; M3b look sign-off. Dismiss is .btn-sm (padding 5px 10px, 11px, styles.css:407). -->
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisibility" />
    </UserControl.Resources>
    <Border x:Name="Strip"
            Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisibility}}"
            Background="{DynamicResource Bg1Brush}"
            BorderBrush="{DynamicResource BorderAccentBrush}"
            BorderThickness="0,1,0,0"
            Padding="12,6">
        <DockPanel LastChildFill="True">
            <Button DockPanel.Dock="Right"
                    Content="Dismiss"
                    Command="{Binding DismissCommand}"
                    Padding="10,5"
                    FontSize="11"
                    Style="{DynamicResource SecondaryButton}"
                    AutomationProperties.Name="Dismiss message" />
            <TextBlock x:Name="MessageText"
                       Text="{Binding Message, NotifyOnTargetUpdated=True}"
                       TargetUpdated="OnMessageUpdated"
                       TextWrapping="Wrap"
                       VerticalAlignment="Center"
                       Margin="0,0,12,0"
                       FontSize="12"
                       FontFamily="{DynamicResource FontBody}"
                       Foreground="{DynamicResource Fg2Brush}"
                       AutomationProperties.LiveSetting="Polite" />
        </DockPanel>
    </Border>
</UserControl>
```

Create `src/RBX15.App/Views/StatusStrip.xaml.cs`:

```csharp
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;

namespace RBX15.App.Views;

/// <summary>The status strip. A new message is announced to screen readers as a polite live region.</summary>
public partial class StatusStrip : UserControl
{
    public StatusStrip() => InitializeComponent();

    void OnMessageUpdated(object? sender, DataTransferEventArgs e) =>
        UIElementAutomationPeer.CreatePeerForElement(MessageText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
}
```

Create `src/RBX15.App/Views/DropZone.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.DropZone"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- "Drop art here" (editor.html:311-315; ledger panel-drop-zone, drop-panel). DataContext:
         LayersViewModel. A Button, so it is keyboard-reachable (Enter/Space browse), which 4.1's div was
         not. Values: styles.css:265-282. The inset glow (box-shadow inset 0 0 20px) and the 220 ms
         transition are not reproduced; M3b's side-by-side sign-off judges the look. -->
    <StackPanel>
        <!-- The panel's first heading: .panel h3:first-child has no top margin (styles.css:263). -->
        <TextBlock Text="Drop art here" Style="{DynamicResource PanelHeading}" Margin="0,0,0,8" />
        <Button x:Name="Zone"
                Command="{Binding BrowseCommand}"
                AllowDrop="True"
                Cursor="Hand"
                Margin="0,0,0,8"
                FocusVisualStyle="{DynamicResource FocusRing}"
                AutomationProperties.Name="Drop art here. Drag and drop images, or press to browse."
                DragEnter="OnDragOver"
                DragOver="OnDragOver"
                DragLeave="OnDragLeave"
                Drop="OnDrop">
            <Button.Template>
                <ControlTemplate TargetType="Button">
                    <Grid>
                        <!-- border: 2px dashed border-2 rgba(255,255,255,.14); radius r-md 10;
                             background rgba(25,46,68,.5) (styles.css:266-267, :275) -->
                        <Rectangle x:Name="Box"
                                   RadiusX="10" RadiusY="10"
                                   StrokeThickness="2"
                                   StrokeDashArray="3 3"
                                   Stroke="#24FFFFFF"
                                   Fill="#80192E44" />
                        <!-- padding 24px 12px; 13px; fg-muted #5e6b7f; centered (styles.css:268-271) -->
                        <TextBlock x:Name="Label"
                                   Margin="12,24"
                                   TextAlignment="Center"
                                   FontSize="13"
                                   FontFamily="{DynamicResource FontBody}"
                                   Foreground="#5E6B7F">Drag &amp; drop PNG images<LineBreak />or click to browse.</TextBlock>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <!-- :hover and .dragover: brand-cyan border and text, rgba(23,212,250,.05) (styles.css:277-282) -->
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Box" Property="Stroke" Value="#17D4FA" />
                            <Setter TargetName="Box" Property="Fill" Value="#0D17D4FA" />
                            <Setter TargetName="Label" Property="Foreground" Value="#17D4FA" />
                        </Trigger>
                        <Trigger Property="Tag" Value="dragover">
                            <Setter TargetName="Box" Property="Stroke" Value="#17D4FA" />
                            <Setter TargetName="Box" Property="Fill" Value="#0D17D4FA" />
                            <Setter TargetName="Label" Property="Foreground" Value="#17D4FA" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Button.Template>
        </Button>
    </StackPanel>
</UserControl>
```

Create `src/RBX15.App/Views/DropZone.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The drop zone. A file drag highlights it (the .dragover class, editor.js:955-956); a drop hands
/// the paths to <see cref="LayersViewModel.DropFiles"/> at the default placement (drop-panel). A drag
/// that carries no files shows the no-drop cursor and does nothing.
/// </summary>
public partial class DropZone : UserControl
{
    public DropZone() => InitializeComponent();

    void OnDragOver(object sender, DragEventArgs e)
    {
        bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
        Zone.Tag = files ? "dragover" : null;
        e.Handled = true;
    }

    void OnDragLeave(object sender, DragEventArgs e) => Zone.Tag = null;

    void OnDrop(object sender, DragEventArgs e)
    {
        Zone.Tag = null;
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && DataContext is LayersViewModel vm) vm.DropFiles(paths);
    }
}
```

Create `src/RBX15.App/Views/LayersPanel.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.LayersPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:RBX15.App.ViewModels">
    <!-- The basic layers panel (editor.html:317-318; editor.js:671-744; styles.css:287-319; ledger
         layers-list, layers-list-render). DataContext: LayersViewModel. Top row = highest z. Arrow keys
         move the selection; Tab walks each row's buttons. No drag reorder in M3a, so the heading drops
         4.1's "(drag to reorder)" hint until it works. -->
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
        <TextBlock Text="Layers" Style="{DynamicResource PanelHeading}" />
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
                                <ControlTemplate.Triggers>
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
                            <!-- three equal buttons, gap 3 (editor.js:697-715) -->
                            <Grid Margin="0,4,0,0">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="3" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="3" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <Button Grid.Column="0"
                                        Content="Dupe"
                                        ToolTip="Duplicate layer"
                                        Command="{Binding DuplicateCommand}"
                                        Style="{StaticResource DupeButton}"
                                        AutomationProperties.Name="{Binding Name, StringFormat=Duplicate {0}}" />
                                <Button Grid.Column="2"
                                        Content="{Binding VisibilityLabel}"
                                        ToolTip="{Binding VisibilityToolTip}"
                                        Command="{Binding ToggleVisibilityCommand}"
                                        Style="{StaticResource VisibilityButton}"
                                        AutomationProperties.Name="{Binding VisibilityToolTip}" />
                                <Button Grid.Column="4"
                                        Content="Del"
                                        ToolTip="Delete layer"
                                        Command="{Binding DeleteCommand}"
                                        Style="{StaticResource DeleteButton}"
                                        AutomationProperties.Name="{Binding Name, StringFormat=Delete {0}}" />
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
        <!-- Up / Down (editor.js:773-774, titles "Move layer up." / "Move layer down."). 4.1 put them in the
             Selected layer panel, which is M3b; M3a shows them under the list. .row gap 8 (styles.css:369). -->
        <Grid Margin="0,4,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="8" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <Button Grid.Column="0"
                    Content="Up"
                    ToolTip="Move layer up."
                    Command="{Binding MoveUpCommand}"
                    Padding="10,5"
                    FontSize="11"
                    Style="{DynamicResource SecondaryButton}"
                    AutomationProperties.Name="Move selected layer up" />
            <Button Grid.Column="2"
                    Content="Down"
                    ToolTip="Move layer down."
                    Command="{Binding MoveDownCommand}"
                    Padding="10,5"
                    FontSize="11"
                    Style="{DynamicResource SecondaryButton}"
                    AutomationProperties.Name="Move selected layer down" />
        </Grid>
    </StackPanel>
</UserControl>
```

Create `src/RBX15.App/Views/LayersPanel.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

public partial class LayersPanel : UserControl
{
    public LayersPanel() => InitializeComponent();
}
```

Create `src/RBX15.App/Views/FileGroup.xaml`:

```xml
<UserControl x:Class="RBX15.App.Views.FileGroup"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- The Home tab's File group (editor.html:85-97; ledger home-file-group). DataContext:
         FileGroupViewModel. M3a shows Add image and Download template; Save work and Open (M4) stay
         hidden until they work. .ribbon-group-items.vert: column, stretch, gap 4 (styles.css:207-208);
         each row is a flex row with gap 3 (editor.html:87, :92); .btn-sm is padding 5px 10px, 11px (styles.css:407). -->
    <Border Style="{DynamicResource RibbonGroup}">
        <DockPanel>
            <TextBlock DockPanel.Dock="Bottom" Text="FILE" Style="{DynamicResource RibbonGroupLabel}" />
            <StackPanel VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                    <Button Content="Add image"
                            ToolTip="Add an image file as a layer."
                            Command="{Binding AddImageCommand}"
                            Padding="10,5"
                            FontSize="11"
                            Style="{DynamicResource SecondaryButton}" />
                </StackPanel>
                <StackPanel Orientation="Horizontal">
                    <Button Content="Download template"
                            ToolTip="Export the finished 585x559 PNG. Ready to upload to Roblox."
                            Command="{Binding DownloadTemplateCommand}"
                            Padding="10,5"
                            FontSize="11"
                            Style="{DynamicResource PrimaryButton}" />
                </StackPanel>
            </StackPanel>
        </DockPanel>
    </Border>
</UserControl>
```

Create `src/RBX15.App/Views/FileGroup.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace RBX15.App.Views;

public partial class FileGroup : UserControl
{
    public FileGroup() => InitializeComponent();
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ThemedDialogTests|FullyQualifiedName~RBX15.App.Tests.Task4ViewTests"`
Expected: `Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 716, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 298, Failed: 0`.

```bash
git add src/RBX15.App/Views/ThemedDialog.xaml src/RBX15.App/Views/ThemedDialog.xaml.cs src/RBX15.App/Views/StatusStrip.xaml src/RBX15.App/Views/StatusStrip.xaml.cs src/RBX15.App/Views/DropZone.xaml src/RBX15.App/Views/DropZone.xaml.cs src/RBX15.App/Views/LayersPanel.xaml src/RBX15.App/Views/LayersPanel.xaml.cs src/RBX15.App/Views/FileGroup.xaml src/RBX15.App/Views/FileGroup.xaml.cs src/RBX15.App/Services/ThemedDialogService.cs src/RBX15.App/Services/Win32FileDialogs.cs src/RBX15.App.Tests/ThemedDialogTests.cs src/RBX15.App.Tests/Task4ViewTests.cs
git commit -m "feat(app): themed dialog, file dialogs, status strip, drop zone, layers panel and File group views"
```

- [ ] **Step 21: Wire Task 4 into the shell**

Ledger: `home-file-group`, `drop-canvas`, `panel-drop-zone`, `layers-list`, plus the canvas click (Task 2 ruling R5, critic C13). This step prints the three shell files in their final M3a form (`MainWindow.xaml`, `Composition.cs`, `TestShell.cs`), so the end state never depends on anchor edits.

1. `src/RBX15.App/ViewModels/HomeViewModel.cs`: add this member to the class body, after the `Edit` property:

```csharp
    /// <summary>The File group (Task 4): Add image and Download template.</summary>
    public required FileGroupViewModel File { get; init; }
```

2. `src/RBX15.App/ViewModels/MainViewModel.cs`: add these members to the class body, after the `Home` property:

```csharp
    /// <summary>The drop zone and layers panel (Task 4).</summary>
    public required LayersViewModel Layers { get; init; }

    /// <summary>The status strip (Task 4).</summary>
    public required StatusViewModel Status { get; init; }
```

3. `src/RBX15.App/Views/HomeRibbon.xaml`: replace Task 3's line `            <!-- Task 4: File group (editor.html:85-97) -->` with:

```xml
            <!-- File (editor.html:85-97): Add image and Download template; Save work and Open arrive with M4 -->
            <views:FileGroup DataContext="{Binding File}" />
```

4. Replace `src/RBX15.App/MainWindow.xaml` with its final M3a form:

```xml
<!--
  The shell skeleton (ledger shell-layout-skeleton): header, ribbon, the app row (left panel and
  canvas area), the status strip row, footer, top to bottom (styles.css:99-108, editor.html:15-343).
  4.1 has no right panel: .rpanel (styles.css:462-469) is styled but never used, so it is not ported.
  This is the final M3a file: Task 2's CanvasView in the CanvasArea, Task 3's HomeRibbon in the
  RibbonPane, and Task 4's drop zone, layers panel and status strip.
-->
<Window x:Class="RBX15.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:RBX15.App.Views"
        xmlns:vm="clr-namespace:RBX15.App.ViewModels"
        xmlns:app="clr-namespace:RBX15.App"
        x:Name="Root"
        Title="{x:Static app:AppInfo.ProductName}"
        Width="{x:Static app:ShellLayout.DefaultWidth}" Height="{x:Static app:ShellLayout.DefaultHeight}"
        MinWidth="{x:Static app:ShellLayout.MinWidth}" MinHeight="{x:Static app:ShellLayout.MinHeight}"
        Background="{StaticResource Bg0Brush}"
        Foreground="{StaticResource Fg2Brush}"
        FontFamily="{StaticResource FontBody}"
        UseLayoutRounding="True"
        TextOptions.TextRenderingMode="Grayscale"
        d:DataContext="{d:DesignInstance Type=vm:MainViewModel}"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d">

  <Grid x:Name="Viewport">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <!-- header (styles.css:129-155): padding 10px 24px, gap 16px, 135deg navy / navy-deep / navy,
         1px border-accent bottom, shadow 0 2px 8px rgba(0,0,0,.35); drawn above the ribbon. -->
    <Border x:Name="Header" Grid.Row="0" Panel.ZIndex="1" Padding="24,10"
            BorderBrush="{StaticResource BorderAccentBrush}" BorderThickness="0,0,0,1">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
          <GradientStop Offset="0" Color="{StaticResource BrandNavyColor}" />
          <GradientStop Offset="0.5" Color="{StaticResource BrandNavyDeepColor}" />
          <GradientStop Offset="1" Color="{StaticResource BrandNavyColor}" />
        </LinearGradientBrush>
      </Border.Background>
      <Border.Effect>
        <DropShadowEffect Color="Black" Direction="270" ShadowDepth="2" BlurRadius="8" Opacity="0.35" />
      </Border.Effect>
      <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
        <!-- header h1 (styles.css:139-148): Space Grotesk 20px 600, text filled with the brand gradient. -->
        <TextBlock x:Name="HeaderTitle" Text="{Binding HeaderTitle, Mode=OneTime}" VerticalAlignment="Center"
                   FontFamily="{StaticResource FontDisplay}" FontSize="20" FontWeight="SemiBold"
                   Foreground="{StaticResource BrandGradientBrush}" />
        <!-- header span (styles.css:149-155): JetBrains Mono 11px fg-3 uppercase; gap 16px (:133). -->
        <TextBlock x:Name="HeaderByline" Margin="16,0,0,0" VerticalAlignment="Center"
                   FontFamily="{StaticResource FontMono}" FontSize="11" Foreground="{StaticResource Fg3Brush}"
                   Text="{Binding HeaderByline, Mode=OneTime, Converter={StaticResource UpperCase}}" />
      </StackPanel>
    </Border>

    <!-- .ribbon (styles.css:160-164): 180deg navy-deep to ink-900, 1px border-1 bottom. -->
    <Border x:Name="Ribbon" Grid.Row="1" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,0,0,1">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
          <GradientStop Offset="0" Color="{StaticResource BrandNavyDeepColor}" />
          <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
        </LinearGradientBrush>
      </Border.Background>
      <DockPanel>
        <!-- .ribbon-tabs (styles.css:165-170): padding 0 8px, 180deg ink-950 to ink-900, 1px border-1 bottom.
             M3a shows Home only (editor.html:23); Layout, Details and Text arrive in M4. -->
        <Border DockPanel.Dock="Top" Padding="8,0" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,0,0,1">
          <Border.Background>
            <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
              <GradientStop Offset="0" Color="{StaticResource Ink950Color}" />
              <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
            </LinearGradientBrush>
          </Border.Background>
          <StackPanel x:Name="RibbonTabs" Orientation="Horizontal">
            <RadioButton x:Name="HomeTab" Style="{StaticResource RibbonTab}" GroupName="RibbonTabs"
                         Content="Home" IsChecked="True" AutomationProperties.Name="Home tab" />
          </StackPanel>
        </Border>
        <!-- .ribbon-pane.active (styles.css:197-198): padding 8px 12px, groups in a row that never
             shrink and scroll horizontally (ledger ribbon-overflow-hscroll). HomeRibbon (Task 3) is the
             pane, so it brings its own ScrollViewer and its 12,8 margin. -->
        <ContentControl x:Name="RibbonPane" Focusable="False">
          <views:HomeRibbon DataContext="{Binding Home}" />
        </ContentControl>
      </DockPanel>
    </Border>

    <!-- .app (styles.css:241): the left panel and the canvas area. -->
    <Grid x:Name="AppRow" Grid.Row="2">
      <Grid.ColumnDefinitions>
        <ColumnDefinition x:Name="PanelColumn" Width="280" />
        <ColumnDefinition Width="*" />
      </Grid.ColumnDefinitions>

      <!-- .panel (styles.css:246-253): 280px (200px at the breakpoint), 180deg navy-deep to ink-900,
           1px border-1 right, padding 14px, vertical scroll. The drop zone, then the layers
           (editor.html:311-318), both on the LayersViewModel (Task 4). -->
      <Border x:Name="LeftPanel" Grid.Column="0" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,0,1,0">
        <Border.Background>
          <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
            <GradientStop Offset="0" Color="{StaticResource BrandNavyDeepColor}" />
            <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
          </LinearGradientBrush>
        </Border.Background>
        <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Focusable="False">
          <StackPanel x:Name="LeftPanelContent" Margin="14">
            <views:DropZone DataContext="{Binding Layers}" />
            <views:LayersPanel DataContext="{Binding Layers}" />
          </StackPanel>
        </ScrollViewer>
      </Border>

      <!-- .canvas-area (styles.css:441-450): ink-950, holding the canvas host and the zoom pill (Task 2). -->
      <Grid x:Name="CanvasArea" Grid.Column="1" Background="{StaticResource Ink950Brush}" ClipToBounds="True">
        <views:CanvasView DataContext="{Binding Canvas}" />
      </Grid>
    </Grid>

    <!-- The status strip row (spec §4, new in v5), above the footer; collapsed with no message. -->
    <views:StatusStrip Grid.Row="3" DataContext="{Binding Status}" />

    <!-- footer (styles.css:678-689): 180deg ink-950 to ink-900, 1px border-1 top, padding 7px 24px,
         JetBrains Mono 10px fg-muted uppercase, centered. -->
    <Border x:Name="Footer" Grid.Row="4" Padding="24,7" BorderBrush="{StaticResource Border1Brush}" BorderThickness="0,1,0,0">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
          <GradientStop Offset="0" Color="{StaticResource Ink950Color}" />
          <GradientStop Offset="1" Color="{StaticResource Ink900Color}" />
        </LinearGradientBrush>
      </Border.Background>
      <TextBlock x:Name="FooterText" HorizontalAlignment="Center" FontFamily="{StaticResource FontMono}" FontSize="10"
                 Foreground="{StaticResource FgMutedBrush}"
                 Text="{Binding FooterText, Mode=OneTime, Converter={StaticResource UpperCase}}" />
    </Border>
  </Grid>
</Window>
```

5. Replace `src/RBX15.App/Composition.cs` with its final M3a form. Task 4's block goes above Task 3's `home` line, because `home` now takes the File group:

```csharp
using System.Windows;
using System.Windows.Interop;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App;

/// <summary>
/// Builds the view-models over one <see cref="EditorSession"/>. Each task that adds a required member
/// to MainViewModel adds its construction line and its member here, at the two anchors, and the same
/// lines with fakes in the tests' TestShell. <paramref name="owner"/> returns the window native
/// dialogs are owned by.
/// </summary>
internal static class Composition
{
    public static MainViewModel Build(EditorSession session, Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(owner);
        var canvas = new CanvasViewModel(session);
        // Task 4: import, export, layers and status (one ImportService serves Add image, the drop zone and the canvas).
        var status = new StatusViewModel();
        var fileDialogs = new Win32FileDialogs(owner);
        var import = new ImportService(session, status);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, new ThemedDialogService(owner), status, import);
        var layers = new LayersViewModel(session, import, fileDialogs);
        // A canvas drop lands every file on the drop point (editor.js:961-986, drop-canvas).
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        // A canvas click selects the topmost visible layer under it, or none (editor.js:1100-1116; Task 2 ruling R5).
        canvas.ClickHandler = p => session.Select(LayerOps.HitTest(session.Document, p.X, p.Y));
        var home = new HomeViewModel(session, new Win32ColorPicker(
            () => owner() is { } w ? new WindowInteropHelper(w).Handle : IntPtr.Zero))
        {
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

6. Replace `src/RBX15.App.Tests/TestShell.cs` with its final M3a form, the same wiring over fakes:

```csharp
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The tests' one MainViewModel builder: the composition root of App (Composition.Build) with fakes in
/// place of the native services. Every later task that adds a required member to MainViewModel adds
/// its construction line and its member here, at the two anchors, so no earlier test breaks (CS9035).
/// </summary>
internal static class TestShell
{
    public static MainViewModel Main(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var canvas = new CanvasViewModel(session);
        var status = new StatusViewModel();
        var fileDialogs = new FakeFileDialogs();
        var import = new ImportService(session, status);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, new FakeDialogService(), status, import);
        var layers = new LayersViewModel(session, import, fileDialogs);
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        canvas.ClickHandler = p => session.Select(LayerOps.HitTest(session.Document, p.X, p.Y));
        var home = new HomeViewModel(session, new FakeColorPicker())
        {
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

7. `src/RBX15.App.Tests/HomeTestKit.cs`: `HomeViewModel` now has a `required File`, so replace Task 3's `Home` builder

```csharp
    public static HomeViewModel Home(EditorSession session, FakeColorPicker? picker = null) =>
        new(session, picker ?? new FakeColorPicker());
```

with

```csharp
    public static HomeViewModel Home(EditorSession session, FakeColorPicker? picker = null)
    {
        var status = new FakeStatus();
        return new(session, picker ?? new FakeColorPicker())
        {
            File = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)),
        };
    }
```

and add `using RBX15.App.Services;` after `using System.Collections.Immutable;`. `RBX15.App.Tests.Fakes` and `RBX15.App.ViewModels` are already imported.

8. Create `src/RBX15.App.Tests/ShellWiringTests.cs`, which pins the click wiring through the app's own composition root:

```csharp
using System.Collections.Immutable;
using System.Windows;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The wiring Composition.Build adds between the canvas and the session (Task 4 Step 21).</summary>
public class ShellWiringTests
{
    [Fact] // Task 2 ruling R5 (critic C13): editor.js:1100-1116, topmost visible layer under the point, else deselect
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

        main.Canvas.ClickCommand.Execute(new Point(130, 130)); // inside both: the top one wins
        Assert.Equal(2, session.SelectedLayerId);
        main.Canvas.ClickCommand.Execute(new Point(105, 105)); // only inside layer 1
        Assert.Equal(1, session.SelectedLayerId);
        main.Canvas.ClickCommand.Execute(new Point(310, 310)); // a hidden layer is never hit
        Assert.Null(session.SelectedLayerId);
        main.Canvas.ClickCommand.Execute(new Point(105, 105));
        main.Canvas.ClickCommand.Execute(new Point(10, 500));  // empty canvas deselects
        Assert.Null(session.SelectedLayerId);
        Assert.Equal(0, session.UndoCount);                     // selection is never an undo step
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`. A `CS9035` (`Required member 'HomeViewModel.File' must be set`, or the same for `MainViewModel.Layers` / `MainViewModel.Status`) names a construction outside `Composition`, `TestShell` and `HomeTestKit.Home`: route it through one of them.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ShellWiringTests|FullyQualifiedName~RBX15.App.Tests.ShellTests|FullyQualifiedName~RBX15.App.Tests.HomeRibbonViewTests"`
Expected: `Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20` for `RBX15.App.Tests.dll` (ShellWiringTests 1, ShellTests 14, HomeRibbonViewTests 5).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 716, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`; App.Tests `Passed: 299, Failed: 0`.

```bash
git add src/RBX15.App/ViewModels/HomeViewModel.cs src/RBX15.App/ViewModels/MainViewModel.cs src/RBX15.App/Views/HomeRibbon.xaml src/RBX15.App/MainWindow.xaml src/RBX15.App/Composition.cs src/RBX15.App.Tests/TestShell.cs src/RBX15.App.Tests/HomeTestKit.cs src/RBX15.App.Tests/ShellWiringTests.cs
git commit -m "feat(app): wire the layers panel, import, export, status strip and canvas click into the shell"
```

- [ ] **Step 22: Controller check in the running app**

The controller runs this (it needs a display): `dotnet run --project src/RBX15.App -c Release`. Expected, in order:

1. The left panel shows "Drop art here" with the dashed zone, then "Layers" (no hint text), an empty list and disabled Up/Down.
2. Tab reaches the zone; Enter opens the Open dialog filtered to "Images"; pick two PNGs: two rows, the second picked on top and selected; one Undo removes both.
3. Drag a PNG, an `.svg` and a `.txt` from Explorer onto the zone: the zone turns cyan during the drag; one layer is added; the strip names `x.svg` and `x.txt` with their reasons; Dismiss hides it.
4. Drag a PNG onto the canvas at the left sleeve: the layer is centered where it was dropped. Click empty canvas: no row is selected. Click a region tile: it paints (with a layer selected it would fit the layer). Click the layer on the canvas: its row is selected again.
5. Drag a file onto the ribbon or the header: no-drop cursor, nothing happens (`shell-external-drop-guard`).
6. On a row: Hide dims the name and relabels the button "Show"; Dupe adds "<name> copy" above and selects it; Del removes it and selects the new top layer; each is one Undo. Arrow keys move the selection; Tab walks Dupe, Hide, Del.
7. With a fresh template, Download template shows the themed confirm titled "RBX15 Classic Shirt and Pants Maker" with the em-dash text; Esc cancels and no Save dialog opens; OK opens Save As at `roblox_shirt_roblox.png`; saving shows "Saved roblox_shirt_roblox.png." in the strip.
8. Add a phone photo taken in portrait: it shows upright, as in 4.1.

**Interface additions adopted (Task 4).** Each was a change to a sheet entry (**change**) or a new name (**new**); all are adopted as written here.

1. **change, `ImageTooLargeException`:** `public sealed class ImageTooLargeException(string message) : Exception(message)`, not `: InvalidDataException` (sealed; CS0509, measured; critic C8). `ProjectIO.TryDecodeDataUrl` catches `InvalidDataException or ImageTooLargeException` (Step 8), so M4's Open still drops an over-cap layer by name; `ImageImporter.Load` catches both.
2. **new, `Images`:** `public static void CheckEncodedSize(long bytes)` and `public static void CheckDimensions(int width, int height)`, both throwing `ImageTooLargeException` with the messages Step 7 pins. `ImageImporter` calls `CheckEncodedSize(new FileInfo(path).Length)` before reading a file.
3. **change, `Images.Decode`:** applies the EXIF orientation (`SKCodec.EncodedOrigin`), so a decoded image has its displayed size (Review Focus 1; contract amendment CA4, Este's OK). Task 5 adds ledger row `image-decode-exif-orientation` (owner Engine.Document, disposition `keep`, parityCheck "`Exif_orientation_is_applied_like_chromium`, 8 orientations", plus smoke S5's portrait photo) and closes it on that evidence.
4. **new, `LayerOps.HitTest(Document doc, double x, double y) : int?`** (interfaces §9.3). Produced and tested here, and wired by Step 21 to Task 2's canvas click (critic C13).
5. **new, `ImageImporter` reason constants:** `SvgReason`, `UnsupportedReason`, `UndecodableReason`, `ReadFailedReason` (Step 12 pins the text).
6. **change, `ImportService` status policy** (critic C16, adopted over the sheet): one `IStatus.Show` per `Import` call naming every failure (`public static string FailureMessage(IReadOnlyList<ImportFailure>)`), and `IStatus.Clear()` after a call that added files and failed none. The sheet's "each failure -> status.Show" would leave only the last failure visible. A successful export shows "Saved <file>." (`io-download-save-dialog` asks for "a status message"; critic S4, kept).
7. **new, `Views/FileGroup.xaml`** (the sheet lists no view for the File group). Task 3's `Views/HomeRibbon.xaml` hosts it at its anchor `<!-- Task 4: File group (editor.html:85-97) -->`.
8. **new, `ThemedDialog(string message, bool confirm)`** and `bool IsConfirm { get; }`; named parts `MessageText`, `OkButton`, `CancelButton`.
9. **Theme keys** Task 4's XAML resolves with `DynamicResource`, all Task 1's (the draft's names were renamed to Task 1's, critic C6): `Bg1Brush` (`--bg-1`), `Fg2Brush` (`--fg-2`), `BorderAccentBrush` (`--border-accent`), `FontBody` (a `FontFamily`, `--font-body`), `PanelHeading` (TextBlock, `.panel h3`, styles.css:254-263; the drop zone's first heading sets `Margin="0,0,0,8"`), `FocusRing` (the `FocusVisualStyle`, the 2 px cyan ring offset 2, styles.css:357-367), `SecondaryButton` and `PrimaryButton` (ButtonBase, `.btn-secondary` / `.btn-primary`, styles.css:375-418), `RibbonGroup` (Border, `.ribbon-group`, styles.css:200-207) and `RibbonGroupLabel` (TextBlock, `.ribbon-group-label`, styles.css:211-221). Task 5's `ThemeKeyGuardTests` fails on any key a view names that the theme lacks.
10. **Anchors** Step 21 uses: Task 3's `<!-- Task 4: File group (editor.html:85-97) -->` in `HomeRibbon.xaml`; the `Edit` property of `HomeViewModel` and the `Home` property of `MainViewModel` for the "after" placement. `MainWindow.xaml`, `Composition.cs` and `TestShell.cs` are printed in full in Step 21 (critic P2), so Task 1's anchors in them are replaced wholesale.
11. **new test files:** `src/RBX15.App.Tests/TestFiles.cs`, `ImagesOrientationTests.cs` (Engine.Tests), `ThemedDialogTests.cs`, `Task4ViewTests.cs`, `ShellWiringTests.cs`.
12. **For every drafter:** `UseWPF` projects remove `System.IO` from the implicit usings (measured: `CS0103: The name 'Path' does not exist`), so App and App.Tests files that use `File`, `Path` or `FileInfo` need `using System.IO;`.

---

### Task 5: Close-out: ledger, divergences, smoke checklist and final verification

**Files:**

- Create:
  - `src/RBX15.App.Tests/LedgerScenarioTests.cs`. It holds 14 scenario facts, each replaying a ledger row's parityCheck across Tasks 1-4, with private fakes of the four service interfaces.
  - `src/RBX15.App.Tests/ThemeKeyGuardTests.cs`. One fact: every `{DynamicResource X}` a view names is a key the theme or that view defines (critic C6; a missing DynamicResource key fails silently at run time).
- Modify: `docs/port/parity-ledger.json`:
  - amend `overlay-mask-checkbox` and `overlay-grid-checkbox` to `fix`;
  - reword five divergence texts to what M3a ships (`render-zoom-canvas`, `shell-window-size`, `layout-template-regions`, `fills-to-css`, `io-image-formats`);
  - add five rows: `image-decode-caps`, `canvas-device-pixel-render`, `font-ui-weight-600`, `zoom-pill-limits`, `image-decode-exif-orientation`;
  - narrow 26 parityCheck texts;
  - `ledger.py done` the proven rows.
- Modify (generated): `docs/port/parity-ledger.md` and `docs/port/divergences.md`.
- Scratch only (git-ignored): `.superpowers/logs/m3a-ledger.py`, `.superpowers/logs/m3a-evidence.py`, the test logs and `.superpowers/m3a-smoke/`.

**Interfaces:**

- Consumes (evidence only; Task 5 changes no production code):
  - Task 1: `EditorSession` (`Apply`, `ApplyWithSelection`, `TakeId`, `Undo`, `Redo`, `UndoCount`, `RedoCount`, `CanRedo`, `CanUndo`, `NextId`, `SelectedLayerId`, `TemplateTitle`), `Document.ContentEquals`, `AppInfo.ProductName`, `AppInfo.Version`, `StaThread.Run(Action)`. Test classes `EditorSessionTests`, `PlacementGuardTests`, `JsonSettingsStoreTests`, `ThemeTokenTests`, `DocumentTests`.
  - Task 2: `ViewOptions`, `Renderer.RenderViewImage`, `Renderer.ViewSize`, `tools/parity-ref/view-scenes.json`, `tests/goldens/view`. Test classes `RendererViewTests`, `GoldenViewTests`, `CanvasCoordinatesTests`, `CanvasViewModelTests`, `TemplateCanvasHostTests`, `CanvasViewTests`.
  - Task 3: `DocumentOps.PaintRegion`, `DocumentOps.RegionFill`, `DocumentOps.ToggleMode`, `FillStyleViewModel(EditorSession, IColorPicker)` (`Current`, `Color1`, `PickColor1Command`), `RegionsGroupViewModel(EditorSession, FillStyleViewModel)` (`Body`), `RegionTileViewModel` (`Region`, `Fill`, `Swatch`, `PaintCommand`, `ClearCommand`), `SettingsGroupViewModel(EditorSession)` (`ShowMask`, `ShowGrid`), `EditGroupViewModel(EditorSession)` (`UndoCommand`, `RedoCommand`, `ClearCommand`), `IColorPicker`. Test classes `DocumentOpsTests`, `ModeGroupTests`, `RegionsGroupTests`, `FillStyleTests`, `SettingsGroupTests`, `EditGroupTests`.
  - Task 4:
    - `LayerOps.AddImage`, `LayerOps.Remove`, `Images.MaxDimension`, `ExportGuard.EmptyTemplateMessage`;
    - `IDialogService`, `IFileDialogs`, `IStatus`;
    - `ImportService(EditorSession, IStatus).Import`;
    - `FileGroupViewModel(EditorSession, IFileDialogs, IDialogService, IStatus, ImportService)` (`DownloadTemplateCommand`);
    - `LayersViewModel(EditorSession, ImportService, IFileDialogs)` (`Rows`);
    - `LayerRowViewModel` (`Id`, `Name`, `Visible`, `IsSelected`, `VisibilityLabel`, `VisibilityToolTip`, `ToggleVisibilityCommand`, `DuplicateCommand`).
    - Test classes `LayerOpsTests`, `ImagesCapsTests`, `ImagesOrientationTests`, `ExportGuardTests`, `LayersViewModelTests`, `ImageImporterTests`, `ImportServiceTests`, `FileGroupTests`, `StatusViewModelTests`, `ShellWiringTests`.
  - The view files every task wrote (`src/RBX15.App/Views/*.xaml`, `MainWindow.xaml`) and the theme (`src/RBX15.App/Themes/*.xaml`), read as text by `ThemeKeyGuardTests`.
  - Engine (main): `Renderer.Export`, `Renderer.ExportPng`, `ProjectIO.AppName`, `Fill`, `Layer`, `TemplateMode`.
  - `python tools/ledger/ledger.py done <id...>` and `check` (M1 Task 7).
- Produces:
  - `LedgerScenarioTests` (14 facts) and `ThemeKeyGuardTests` (1 fact): App.Tests +15.
  - The ledger with the rows M3a proves set to `done`: 46 if every gate holds, so 81 to 127 done of 413 rows. The rows left open are listed with reasons in the commit body.
  - Five new rows: `image-decode-caps`, `zoom-pill-limits` and `image-decode-exif-orientation` (closed), `canvas-device-pixel-render` and `font-ui-weight-600` (open for the M3b look sign-off). Two rows amended to `fix`, and five divergence texts reworded.
  - Este's smoke checklist with its results, and a clean, verified branch. Push, PR and dashboard steps belong to the controller.

**Rulings made in this task** (the reconciler adjusted 4 and 6 and added 10 and 11; see the reconcile notes):

1. **Closing versus narrowing.** A row closes as written when the M3a evidence meets its parityCheck. When the check names a method M3a does not use, its parityCheck is narrowed to the evidence used, with "Narrowed in M3a" and the reason (ledger honesty rule, decision `GCi6o7MKYnPYZ3Dq0khH`, as M2b did in `48d03a3`). That covers a view-model test instead of UI Automation, the Edit group's Undo instead of the Ctrl+Z key, or a hand check in the smoke checklist. A row stays open when an unmet clause is behavior that a later milestone ships (M3b, M4, M5 or M6). Rows whose check is a screenshot or look comparison stay open for M3b's side-by-side sign-off and are not narrowed (contract, "Look check").
2. **Mask and Grid undo.** `overlay-mask-checkbox` and `overlay-grid-checkbox` are `keep` rows whose behavior says "no undo entry". The interface sheet's `file-undo-snapshot-scope` ruling makes each toggle one step. Both rows are amended to `fix` with a divergence, the way M2b ruling R4 amended `project-load-field-coercion`, instead of adding a third row.
3. **Scroll-pan needs no new row.** `canvas-zoom-clipped-no-pan`, `canvas-zoom-controls` and `render-zoom-canvas` already carry the divergence "scrollbars to pan".
4. **New rows** (critic C12 gathered them from every task):
   - `image-decode-caps` (Task 4) closes in M3a.
   - `zoom-pill-limits` (Task 2 ruling R4) closes in M3a: its check is the unit test that disables each button at its limit.
   - `image-decode-exif-orientation` (Task 4 ruling 7, `keep`) closes in M3a on `ImagesOrientationTests` and smoke S5, if Este keeps it (contract amendment CA4).
   - `canvas-device-pixel-render` stays open. The DPI behavior is a visible divergence that 4.1 never had: 4.1's canvas is CSS pixels stretched by Chromium (`cross-window-dpi-rendering`). It closes at M3b's look sign-off at a display scale above 100%. Task 2's draft id `view-device-resolution` described the same divergence and is not added.
   - `font-ui-weight-600` (Task 1 ruling 4) stays open for the M3b look sign-off.
5. **Fill Style and Template Regions on the Home tab.** 4.1 has them on the Layout tab (`editor.html:145-171`). M3a shows them on Home because the Layout tab is hidden until M4 (contract, "Hide what doesn't work yet"). This is interim, so there is no divergence row. `layout-fill-style` and `layout-template-regions` stay open for M4, which moves both groups to the Layout tab when that tab ships.
6. **`render-zoom-canvas` stays open.** v5 keeps 4.1's truncation (Task 2 ruling R1, contract amendment CA1): 877.5 becomes 877 (`editor.js:340`). Step 7 rewords its divergence to say so (Task 2's text); M3b's look sign-off of the scroll host closes it.
7. **`fillstyle-type-buttons` (fix: re-sync the controls after undo) and `fillstyle-ui-not-synced-from-region` (keep: one-way) contradict each other** (interface open item 5). Both stay open regardless, because each check also needs a project load (M4). The commit body flags the conflict so it is ruled before M4 closes them.
8. **`layers-default-name-offbyone` stays open.** `LayerOps.DefaultName` proves image layers, but the row's divergence also covers the drawing-layer fallback (`editor.js:533`), which arrives with M3b's draw tools.
9. **`shell-header-version-hardcoded` stays open.** Its check compares against the MSIX Identity version, which M6 packages. `The_product_name_and_version_come_from_one_place` pins the assembly half (`AppInfo.Version` is `5.0.0`) so that M6 inherits it.
10. **Divergence texts follow what shipped** (critic C12): `shell-window-size` gains the 640x480 minimum (Task 1 ruling 6), `layout-template-regions` the tile keyboard clear (Task 3 ruling 5), `fills-to-css` the swatch's own gradient extent (Task 3 ruling 3), and `io-image-formats` adds BMP (Task 4 ruling 4, Este's 2026-10-14 format list). Each row keeps its status.
11. **Values with no 4.1 source are provisional** (critic P3). Each is tagged "new v5 surface; provisional; M3b look sign-off" in its XAML and listed in Step 9's left-open table, so M3b's side-by-side sign-off sets or confirms it: the scrollbar width 8 and thumb inset 1 (Task 1), the Color 1/2 swatch corner radius 2 (Task 3), the themed dialog's MinWidth 360, MaxWidth 480, padding 20,18,20,16, 13px message and 18 DIP gap (Task 4), and the status strip's padding 12,6, 12px text and 12 DIP gap (Task 4).

**The state Task 5 starts from.** Tasks 1-4 are committed, the view goldens are captured (Task 2's controller-run step) and every suite is green. Tasks 1-4 close no ledger rows; `docs/port` is unchanged from `main`. If any of that is not true, stop and report to the controller.

**Test counts.** The cumulative counts each task's last run step states:

| Task | Engine.Tests classes it adds to | App.Tests classes |
| --- | --- | --- |
| 1 | `DocumentTests` (+`ContentEquals`, +16) | `EditorSessionTests`, `PlacementGuardTests`, `JsonSettingsStoreTests`, `ThemeTokenTests`, `ShellTests` (+132) |
| 2 | `RendererViewTests`, `GoldenViewTests` (+55) | `CanvasCoordinatesTests`, `CanvasViewModelTests`, `TemplateCanvasHostTests`, `CanvasViewTests` (+48) |
| 3 | `DocumentOpsTests` (+21) | `ModeGroupTests`, `SettingsGroupTests`, `EditGroupTests`, `FillStyleTests`, `RegionsGroupTests`, `HomeRibbonViewTests` (+55) |
| 4 | `LayerOpsTests`, `ExportGuardTests`, `ImagesCapsTests`, `ImagesOrientationTests` (+58) | `StatusViewModelTests`, `ImageImporterTests`, `ImportServiceTests`, `LayersViewModelTests`, `FileGroupTests`, `ThemedDialogTests`, `Task4ViewTests`, `ShellWiringTests` (+64) |
| 5 | none | `LedgerScenarioTests`, `ThemeKeyGuardTests` (+15) |

| After | Engine.Tests | Parity.Tests | App.Tests |
| --- | --- | --- | --- |
| `main` (1126e7c) | 566 | 35 | 0 (project absent) |
| Task 4 (Step 1 baseline) | 716 | 35 | 299 |
| Task 5 | 716 | 35 | 314 |

Task 5 adds Engine.Tests +0, Parity.Tests +0, App.Tests +15. Every run has 0 failed and 0 skipped.

**The rule for closing.** As in M2a and M2b, the ledger closes conservatively and only here. A row closes only when the evidence on this branch proves its whole parityCheck, or, for a `fix` row, also its whole v5 divergence sentence. Evidence means a unit test, a golden, or an item of the Step 5 smoke checklist that Este passed. Ruling 1 above says when a parityCheck is narrowed and when a row stays open.

- [ ] **Step 1: Record the baseline Tasks 1-4 left**

Run from the repo root:

```bash
mkdir -p .superpowers/logs
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test src/RBX15.slnx -c Release --no-build > .superpowers/logs/m3a-t5-baseline.log 2>&1
grep -E "(Passed|Failed)!" .superpowers/logs/m3a-t5-baseline.log
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done')"
git diff --stat "$BASE" -- docs/port
python -c "import json,os; s=json.load(open('tools/parity-ref/view-scenes.json',encoding='utf-8')); print(len(s), 'scenes;', sorted({x['view']['zoom'] for x in s}), 'zooms;', len([f for f in os.listdir('tests/goldens/view') if f.endswith('.png')]), 'view PNGs')"
grep -c "RBX15.App.Tests" src/RBX15.slnx
```

Expected:

- `base 1126e7c`.
- `0 Warning(s)` and `0 Error(s)`.
- Three summary lines, each `Failed: 0` and `Skipped: 0`:
  - `RBX15.Engine.Tests.dll` `Passed: 716`;
  - `RBX15.Parity.Tests.dll` `Passed: 35`;
  - `RBX15.App.Tests.dll` `Passed: 299`.
- `408 rows; 81 done`.
- The `docs/port` diff prints nothing: Tasks 1-4 close no rows.
- `14 scenes; [0.25, 0.5, 1, 1.5, 3] zooms; 14 view PNGs`. Each scene keeps its zoom, Mask and Grid in a nested `view` object (Task 2 Step 5), so every script below reads `s["view"]`.
- `1`: the App test project is in the solution, so CI's `dotnet test src/RBX15.slnx` runs it.

If any summary shows `Failed` or `Skipped` above 0, or the ledger is not at 81 done, stop and report it to the controller: Tasks 1-4 are not finished.

- [ ] **Step 2: Write the ledger scenario tests**

Each fact replays one ledger row's parityCheck (or one Review Focus case) end to end through the public surface Tasks 1-4 built: `EditorSession`, the engine ops and the view-models, with in-file fakes of the four service interfaces. They add no production code. The view-models that build `ImageSource`s (the region tiles, the layer rows) run inside `StaThread.Run`.

Create `src/RBX15.App.Tests/LedgerScenarioTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using Xunit;

namespace RBX15.App.Tests;

/// <summary>
/// M3a close-out scenarios. Each fact replays one parity-ledger row's parityCheck (named in its
/// comment) across the pieces Tasks 1-4 built, through their public surface only. A failure is a
/// defect in the owning task's code, never a reason to edit the scenario.
/// </summary>
public sealed class LedgerScenarioTests : IDisposable
{
    static readonly SKColor Red = new(255, 0, 0);
    static readonly SKColor Blue = new(0, 0, 255);
    static readonly SKColor Slate = new(100, 150, 200);
    readonly string _dir = Directory.CreateTempSubdirectory("rbx15-m3a-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Mode_switch_wipes_fills_keeps_layers_and_undo_brings_the_fills_back()
    {
        // mode-switch-effects, mode-switch-undo-and-callers, restore-mode-switch-side-effects,
        // tpl-active-regions-swap, claim-mode-per-mode-region-data (editor.js:1695-1725)
        using var art = Solid(40, 40, Slate);
        var s = new EditorSession();
        var fill = new Fill("linear", "#17d4fa", "#f22f89", 45, 16);
        foreach (string region in new[] { "torso_front", "torso_back", "rarm_front" })
            Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, region, fill)));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, art, s.TakeId(), "art")));
        int id = s.SelectedLayerId!.Value;
        var shirt = s.Document;

        Assert.True(s.Apply(DocumentOps.ToggleMode));
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
        Assert.Equal(shirt.Layers.ToArray(), s.Document.Layers.ToArray());
        Assert.Equal((shirt.BgColor, shirt.BgOpacity, shirt.ShowMask, shirt.ShowGrid),
                     (s.Document.BgColor, s.Document.BgOpacity, s.Document.ShowMask, s.Document.ShowGrid));

        Assert.True(s.Apply(DocumentOps.ToggleMode));       // back to Shirt: the fills do not come back
        Assert.Equal(TemplateMode.Shirt, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
        Assert.Equal(6, s.UndoCount);

        Assert.True(s.Undo());
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
        Assert.True(s.Undo());
        Assert.True(s.Document.ContentEquals(shirt));
        Assert.Equal(3, s.Document.RegionFills.Count);
        Assert.Equal(id, s.SelectedLayerId);
        Assert.True(s.Redo());
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Empty(s.Document.RegionFills);
    }

    [Fact]
    public void Repainting_with_the_same_fill_is_no_step_and_clearing_an_unpainted_tile_is_none() => StaThread.Run(() =>
    {
        // regions-click-paint, regions-right-click-clear (editor.js:1668-1684)
        var s = new EditorSession();
        var fillStyle = new FillStyleViewModel(s, new Picker());
        var regions = new RegionsGroupViewModel(s, fillStyle);
        RegionTileViewModel Front() => regions.Body.Single(t => t.Region == "torso_front");
        byte[] blank = Renderer.ExportPng(s.Document);

        Front().ClearCommand.Execute(null);
        Assert.Equal(0, s.UndoCount);

        Front().PaintCommand.Execute(null);
        Front().PaintCommand.Execute(null);                 // same fill again: nothing changes, no step
        Assert.Equal(1, s.UndoCount);
        Assert.Equal(fillStyle.Current, DocumentOps.RegionFill(s.Document, "torso_front"));
        Assert.Equal(fillStyle.Current, Front().Fill);

        Front().ClearCommand.Execute(null);
        Assert.Equal(2, s.UndoCount);
        Assert.Null(Front().Fill);
        Assert.Null(Front().Swatch);
        Assert.Equal(blank, Renderer.ExportPng(s.Document));

        Assert.True(s.Undo());
        Assert.Equal(fillStyle.Current, DocumentOps.RegionFill(s.Document, "torso_front"));
        Assert.True(s.Undo());
        Assert.Empty(s.Document.RegionFills);
        Assert.False(s.CanUndo);
    });

    [Fact]
    public void Clear_removes_only_layers_and_undo_restores_them()
    {
        // btn-clear (editor.js:1394)
        using var a = Solid(30, 30, Slate);
        using var b = Solid(20, 40, Blue);
        var s = new EditorSession();
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#ff0000"))));
        Assert.True(s.Apply(d => d with { BgColor = "#336699" }));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, a, s.TakeId(), "a")));
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, b, s.TakeId(), "b")));
        var full = s.Document;
        var edit = new EditGroupViewModel(s);

        edit.ClearCommand.Execute(null);
        Assert.Empty(s.Document.Layers);
        Assert.Null(s.SelectedLayerId);
        Assert.Equal(full.RegionFills.ToArray(), s.Document.RegionFills.ToArray());
        Assert.Equal((full.Mode, full.BgColor, full.BgOpacity), (s.Document.Mode, s.Document.BgColor, s.Document.BgOpacity));
        using (var cleared = Renderer.Export(s.Document))
        {
            Assert.Equal(Red, PixelOf(cleared, 295, 138));                       // torso_front center: its fill
            Assert.Equal(new SKColor(0x33, 0x66, 0x99), PixelOf(cleared, 200, 100)); // torso_right: the background
        }

        Assert.True(edit.UndoCommand.CanExecute(null));
        edit.UndoCommand.Execute(null);
        Assert.True(s.Document.ContentEquals(full));
        Assert.Same(a, s.Document.Layers[0].Image);
        Assert.Same(b, s.Document.Layers[1].Image);
    }

    [Fact]
    public void Thirty_one_edits_leave_thirty_undos_and_a_new_edit_empties_redo()
    {
        // home-edit-group (editor.js:86, :1470-1487; lib/history.js:23-57)
        var s = new EditorSession();
        var edit = new EditGroupViewModel(s);
        Assert.False(edit.UndoCommand.CanExecute(null));
        Assert.False(edit.RedoCommand.CanExecute(null));
        for (int i = 1; i <= 31; i++)
        {
            double opacity = i / 100.0;
            Assert.True(s.Apply(d => d with { BgOpacity = opacity }));
        }

        int undos = 0;
        while (edit.UndoCommand.CanExecute(null) && undos < 100)
        {
            edit.UndoCommand.Execute(null);
            undos++;
        }
        Assert.Equal(30, undos);
        Assert.Equal(0.01, s.Document.BgOpacity);           // the oldest of the 31 snapshots was evicted
        Assert.True(edit.RedoCommand.CanExecute(null));

        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        Assert.False(edit.RedoCommand.CanExecute(null));
        Assert.Equal(0, s.RedoCount);
    }

    [Fact]
    public void Undo_and_redo_restore_the_exact_export_through_every_kind_of_edit()
    {
        // undo-restore, history-snapshot-coupling (editor.js:1404-1468)
        using var art = Solid(64, 64, Slate);
        var s = new EditorSession();
        var exports = new List<byte[]> { Renderer.ExportPng(s.Document) };
        void Step(bool recorded)
        {
            Assert.True(recorded);
            exports.Add(Renderer.ExportPng(s.Document));
        }

        Step(s.ApplyWithSelection(x => LayerOps.AddImage(x, art, s.TakeId(), "art")));
        int id = s.SelectedLayerId!.Value;
        Step(s.Apply(d => d with { BgColor = "#336699" }));
        Step(s.Apply(d => DocumentOps.PaintRegion(d, "torso_back", new Fill("stripes", "#ffffff", "#000000", 30, 8))));
        Step(s.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Brightness = 150 }) }));
        Step(s.Apply(DocumentOps.ToggleMode));
        Step(s.ApplyWithSelection(x => LayerOps.Remove(x, id)));
        Assert.Equal(7, exports.Count);

        for (int i = exports.Count - 2; i >= 0; i--)
        {
            Assert.True(s.Undo());
            Assert.Equal(exports[i], Renderer.ExportPng(s.Document));
        }
        Assert.False(s.Undo());
        for (int i = 1; i < exports.Count; i++)
        {
            Assert.True(s.Redo());
            Assert.Equal(exports[i], Renderer.ExportPng(s.Document));
        }
        Assert.False(s.Redo());
    }

    [Fact]
    public void Selection_survives_undoing_a_region_paint()
    {
        // undo-restore-ui-side-effects (4.1 deselects on every restore, editor.js:1451)
        using var art = Solid(32, 32, Slate);
        var s = new EditorSession();
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, art, s.TakeId(), "art")));
        int id = s.SelectedLayerId!.Value;
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_back", Fill.Solid("#00ff00"))));

        Assert.True(s.Undo());
        Assert.Empty(s.Document.RegionFills);
        Assert.Equal(id, s.SelectedLayerId);
        Assert.True(s.Redo());
        Assert.Equal(id, s.SelectedLayerId);
    }

    [Fact]
    public void Mask_and_grid_toggles_are_one_step_each_and_never_reach_the_export()
    {
        // overlay-mask-checkbox, overlay-grid-checkbox, home-settings-group
        // (editor.js:332-337, :352-359, :1494-1495; the export always masks, :1530-1532)
        using var red = Solid(20, 20, Red);
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = red, Name = "outside" }) }));
        byte[] export = Renderer.ExportPng(s.Document);
        var settings = new SettingsGroupViewModel(s);
        Assert.True(settings.ShowMask);
        Assert.False(settings.ShowGrid);

        using (var masked = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
            Assert.Equal(new SKColor(0x05, 0x0c, 0x18), PixelOf(masked, 5, 5));   // checkerboard (editor.js:436)

        settings.ShowMask = false;
        Assert.Equal(2, s.UndoCount);
        Assert.False(s.Document.ShowMask);
        using (var unmasked = Renderer.RenderViewImage(s.Document, new ViewOptions(1)))
            Assert.Equal(Red, PixelOf(unmasked, 5, 5));
        Assert.Equal(export, Renderer.ExportPng(s.Document));

        settings.ShowGrid = true;
        Assert.Equal(3, s.UndoCount);
        Assert.True(s.Document.ShowGrid);
        Assert.Equal(export, Renderer.ExportPng(s.Document));

        Assert.True(s.Undo());
        Assert.False(s.Document.ShowGrid);
        Assert.True(s.Undo());
        Assert.True(s.Document.ShowMask);
        Assert.True(settings.ShowMask);
    }

    [Fact]
    public void Download_asks_first_with_only_hidden_layers_or_a_background_color_and_cancel_writes_nothing()
    {
        // Review Focus; export-png-guard, io-empty-export-confirm (editor.js:1500-1506)
        using var red = Solid(20, 20, Red);
        var s = new EditorSession();
        var dialogs = new Dialogs();
        string path = Path.Combine(_dir, "blank.png");
        var files = new Files(path);
        var status = new Status();
        var file = new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status));

        file.DownloadTemplateCommand.Execute(null);         // a fresh document
        Assert.Equal(ExportGuard.EmptyTemplateMessage, Assert.Single(dialogs.Confirms));
        Assert.Empty(files.Suggested);

        Assert.True(s.Apply(d => d with
        {
            BgColor = "#ff8800",
            Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = red, Visible = false, X = 280, Y = 120 }),
        }));
        file.DownloadTemplateCommand.Execute(null);         // a background color and a hidden layer are not art
        Assert.Equal(2, dialogs.Confirms.Count);
        Assert.Empty(files.Suggested);
        Assert.False(File.Exists(path));

        dialogs.Answer = true;
        file.DownloadTemplateCommand.Execute(null);
        Assert.Equal(3, dialogs.Confirms.Count);
        Assert.Equal("roblox_shirt_roblox.png", Assert.Single(files.Suggested));
        Assert.Equal(Renderer.ExportPng(s.Document), File.ReadAllBytes(path));
    }

    [Fact]
    public void Download_with_the_mask_off_still_writes_the_masked_template()
    {
        // Review Focus; canvas-export-pipeline, overlay-mask-checkbox (editor.js:1508-1539)
        using var red = Solid(20, 20, Red);
        var s = new EditorSession { TemplateTitle = "My Shirt!" };
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#00ff00"))));
        Assert.True(s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = s.TakeId(), Image = red, Name = "outside" }) }));
        Assert.True(s.Apply(d => d with { ShowMask = false }));
        var dialogs = new Dialogs();
        string path = Path.Combine(_dir, "shirt.png");
        var files = new Files(path);
        var status = new Status();

        new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status)).DownloadTemplateCommand.Execute(null);

        Assert.Empty(dialogs.Confirms);
        Assert.Equal("My_Shirt__roblox.png", Assert.Single(files.Suggested));
        using var written = SKImage.FromEncodedData(File.ReadAllBytes(path));
        Assert.Equal((585, 559), (written.Width, written.Height));
        Assert.Equal(0, PixelOf(written, 5, 5).Alpha);                        // the layer outside every region is masked
        Assert.Equal(new SKColor(0, 255, 0), PixelOf(written, 295, 138));     // the torso_front fill
    }

    [Fact]
    public void A_mixed_batch_adds_the_good_files_in_pick_order_as_one_step_and_names_each_failure()
    {
        // Review Focus; layers-multi-file-async-order, image-decode-caps, drop-filetypes (editor.js:998-1011)
        string[] paths =
        [
            WritePng("big.png", 300, 300, Slate),
            WriteText("broken.png", "not an image"),
            WriteText("logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"),
            WritePng("strip.png", Images.MaxDimension + 1, 1, Blue),
            WritePng("small.png", 10, 10, Red),
        ];
        var s = new EditorSession();
        var status = new Status();

        Assert.Equal(2, new ImportService(s, status).Import(paths));

        Assert.Equal(new[] { "big", "small" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal(s.Document.Layers[^1].Id, s.SelectedLayerId);
        Assert.Equal(1, s.UndoCount);
        var message = Assert.Single(status.Shown);          // one combined message per import (Task 4 ruling 5)
        foreach (string name in new[] { "broken.png", "logo.svg", "strip.png" })
            Assert.Contains(name, message, StringComparison.Ordinal);

        Assert.True(s.Undo());
        Assert.Empty(s.Document.Layers);
    }

    [Fact]
    public void Importing_after_undoing_an_import_takes_a_fresh_id()
    {
        // Review Focus (nextId only grows, editor.js:499, :1465)
        string path = WritePng("art.png", 24, 24, Slate);
        var s = new EditorSession();
        var import = new ImportService(s, new Status());

        Assert.Equal(1, import.Import([path]));
        int first = Assert.Single(s.Document.Layers).Id;
        Assert.True(s.Undo());
        Assert.True(s.CanRedo);

        Assert.Equal(1, import.Import([path]));
        var layer = Assert.Single(s.Document.Layers);
        Assert.Equal(first + 1, layer.Id);
        Assert.Equal(layer.Id, s.SelectedLayerId);
        Assert.False(s.CanRedo);
        Assert.Equal(first + 2, s.NextId);
    }

    [Fact]
    public void Three_imports_list_newest_first_and_hide_and_dupe_follow_the_z_order() => StaThread.Run(() =>
    {
        // layers-list, layers-visibility, layers-duplicate (editor.js:542-560, :570-573, :671-744)
        string[] paths = [WritePng("a.png", 16, 16, Red), WritePng("b.png", 16, 16, Blue), WritePng("c.png", 16, 16, Slate)];
        var s = new EditorSession();
        var import = new ImportService(s, new Status());
        var layers = new LayersViewModel(s, import, new Files(null));
        LayerRowViewModel Row(string name) => layers.Rows.Single(r => r.Name == name);

        Assert.Equal(3, import.Import(paths));
        Assert.Equal(new[] { "c", "b", "a" }, layers.Rows.Select(r => r.Name));
        Assert.True(Row("c").IsSelected);

        Row("b").ToggleVisibilityCommand.Execute(null);
        Assert.False(Row("b").Visible);
        Assert.Equal("Show", Row("b").VisibilityLabel);
        Assert.Equal("Show layer", Row("b").VisibilityToolTip);

        Row("b").DuplicateCommand.Execute(null);
        Assert.Equal(new[] { "c", "b copy", "b", "a" }, layers.Rows.Select(r => r.Name));
        Assert.True(Row("b copy").IsSelected);
        Assert.False(Row("b copy").Visible);
        Assert.Equal(s.Document.Layers.Reverse().Select(l => l.Id), layers.Rows.Select(r => r.Id));
        Assert.Equal(3, s.UndoCount);

        Assert.True(s.Undo());                              // the duplicate
        Assert.True(s.Undo());                              // the hide
        Assert.True(Row("b").Visible);
        Assert.Equal("Hide", Row("b").VisibilityLabel);
    });

    [Fact]
    public void A_color_pick_retargets_matching_regions_in_one_step_and_a_cancel_changes_nothing() => StaThread.Run(() =>
    {
        // fillstyle-color-pickers, fillstyle-live-edit-rule (editor.html:157-158; editor.js:1566-1598, :1608-1612)
        var s = new EditorSession();
        var picker = new Picker("#123456", null);
        var fillStyle = new FillStyleViewModel(s, picker);
        var regions = new RegionsGroupViewModel(s, fillStyle);
        regions.Body.Single(t => t.Region == "torso_front").PaintCommand.Execute(null);
        regions.Body.Single(t => t.Region == "torso_back").PaintCommand.Execute(null);
        Assert.Equal(2, s.UndoCount);

        fillStyle.PickColor1Command.Execute(null);
        Assert.Equal("#123456", fillStyle.Color1);
        Assert.Equal("#123456", fillStyle.Current.C1);
        foreach (string region in new[] { "torso_front", "torso_back" })
            Assert.Equal(fillStyle.Current, DocumentOps.RegionFill(s.Document, region));
        Assert.Equal(3, s.UndoCount);

        fillStyle.PickColor1Command.Execute(null);          // cancelled
        Assert.Equal("#123456", fillStyle.Current.C1);
        Assert.Equal(3, s.UndoCount);
        Assert.Equal(new[] { "#17d4fa", "#123456" }, picker.Opened);

        Assert.True(s.Undo());
        foreach (string region in new[] { "torso_front", "torso_back" })
            Assert.Equal("#17d4fa", DocumentOps.RegionFill(s.Document, region)!.C1);
    });

    [Fact]
    public void The_product_name_and_version_come_from_one_place()
    {
        // shell-window-title (main.js:8, editor.html:6); shell-header-version-hardcoded (editor.html:17)
        string name = AppInfo.ProductName;
        string version = AppInfo.Version;
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", name);
        Assert.Equal(ProjectIO.AppName, name);
        Assert.Equal("5.0.0", version);
    }

    static SKImage Solid(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(color);
        return SKImage.FromPixelCopy(bitmap.PeekPixels());
    }

    static SKColor PixelOf(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.GetPixel(x, y);
    }

    string WritePng(string fileName, int width, int height, SKColor color)
    {
        using var image = Solid(width, height, color);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    string WriteText(string fileName, string text)
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, text);
        return path;
    }

    sealed class Picker(params string?[] answers) : IColorPicker
    {
        readonly Queue<string?> _answers = new(answers);
        public List<string> Opened { get; } = [];

        public string? Pick(string initialHex)
        {
            Opened.Add(initialHex);
            return _answers.Count > 0 ? _answers.Dequeue() : null;
        }
    }

    sealed class Dialogs : IDialogService
    {
        public bool Answer { get; set; }
        public List<string> Confirms { get; } = [];
        public List<string> Alerts { get; } = [];

        public bool Confirm(string message)
        {
            Confirms.Add(message);
            return Answer;
        }

        public void Alert(string message) => Alerts.Add(message);
    }

    sealed class Files(string? savePath) : IFileDialogs
    {
        public List<string> Suggested { get; } = [];

        public IReadOnlyList<string> OpenImages() => [];

        public string? SavePng(string suggestedFileName)
        {
            Suggested.Add(suggestedFileName);
            return savePath;
        }
    }

    sealed class Status : IStatus
    {
        public List<string> Shown { get; } = [];
        public void Show(string message) => Shown.Add(message);
        public void Clear() { }
    }
}
```

Create `src/RBX15.App.Tests/ThemeKeyGuardTests.cs`, which widens Task 2's `Every_theme_key_the_view_uses_is_defined_in_the_theme` to every view (critic C6):

```csharp
using System.IO;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// A DynamicResource key that no dictionary defines fails silently at run time: no style, no brush, no
/// focus ring. Every key a view names must be defined in Task 1's theme (Themes/*.xaml) or in that
/// view's own file.
/// </summary>
public class ThemeKeyGuardTests
{
    static readonly string App = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "src", "RBX15.App");

    [Fact]
    public void Every_dynamic_resource_a_view_names_is_defined()
    {
        var theme = string.Concat(Directory.EnumerateFiles(Path.Combine(App, "Themes"), "*.xaml").Select(File.ReadAllText));
        var views = Directory.EnumerateFiles(Path.Combine(App, "Views"), "*.xaml").Append(Path.Combine(App, "MainWindow.xaml")).ToList();
        Assert.Equal(11, views.Count); // the 10 M3a view files and MainWindow.xaml
        var missing = new List<string>();
        foreach (var file in views)
        {
            var text = File.ReadAllText(file);
            foreach (var key in Regex.Matches(text, @"\{DynamicResource (\w+)\}").Select(m => m.Groups[1].Value).Distinct())
                if (!theme.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal) && !text.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
        }
        Assert.Empty(missing);
    }
}
```

`Views` holds 10 XAML files at the end of Task 4 (`CanvasView`, `HomeStyles`, `HomeRibbon`, `FillStyleGroup`, `RegionsGrid`, `ThemedDialog`, `StatusStrip`, `DropZone`, `LayersPanel`, `FileGroup`), and `MainWindow.xaml` makes 11. The exact count keeps a wrong glob from passing on an empty list; a later milestone that adds a view raises it.

The pixel coordinates are template geometry from `Template` (main, `Template.cs:22-41`). `(295, 138)` is the center of `torso_front` (231, 74, 128x128). `(200, 100)` is inside `torso_right` (165, 74, 64x128). `(5, 5)` is outside every region and inside the checkerboard's dark top-left 8x8 square (`editor.js:436`). The file is UTF-8, so the em dash in the empty-template message round-trips as written (`editor.js:1506`).

- [ ] **Step 3: Run the scenarios**

These facts characterize code that Tasks 1-4 already committed, so they pass on the first run. Run them in isolation, then three times in a row, because they write temp files and run STA threads:

```bash
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
for i in 1 2 3; do dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~LedgerScenarioTests|FullyQualifiedName~ThemeKeyGuardTests" 2>&1 | grep -E "(Passed|Failed)!"; done
```

Expected:

- `0 Warning(s)` and `0 Error(s)`. The xUnit analyzers run under `-warnaserror`, which is why no fact compares a collection's `Count` to 0 or 1 and why constants are read into locals before `Assert.Equal`.
- Three lines `Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15` for `RBX15.App.Tests.dll` (14 scenarios and the key guard).

If a fact fails, do not edit it and do not change production code in this task. Report the failing fact and its owner to the controller. A failure is a defect in the task that owns the behavior:

| Fact | Owner |
| --- | --- |
| `Mode_switch_*`, `Repainting_*`, `Clear_*`, `Thirty_one_*`, `Mask_and_grid_*`, `A_color_pick_*` | Task 3 (DocumentOps, the Home view-models), with Task 1 when a step count is off (`EditorSession`) |
| `Undo_and_redo_*`, `Selection_survives_*`, `The_product_name_*` | Task 1 |
| `Download_*` | Task 4 (`FileGroupViewModel`, `ExportGuard`) |
| `A_mixed_batch_*`, `Importing_after_*`, `Three_imports_*` | Task 4 (`ImageImporter`, `ImportService`, `Images` caps, `LayersViewModel`), with Task 1 for ids |
| `Every_dynamic_resource_a_view_names_is_defined` | the task whose view the message names (the key must be renamed to Task 1's spelling, never duplicated in the theme) |

- [ ] **Step 4: Commit the scenarios**

Add your own model's `Co-Authored-By` trailer as the last line (Global Constraints) before running it.

```bash
git add src/RBX15.App.Tests/LedgerScenarioTests.cs src/RBX15.App.Tests/ThemeKeyGuardTests.cs
git commit -F - <<'EOF'
test(app): replay the M3a ledger scenarios across session, regions, layers, import and export

Fourteen facts, each replaying one parity-ledger row's parityCheck (or a
Review Focus case) through the public surface Tasks 1-4 built: mode
switch and its undo, region repaint and clear step counts, Clear, the
30-step history through the Edit group, exact exports through undo and
redo, selection surviving undo, Mask and Grid as one step each and never
in the export, the empty-template confirm for hidden-only layers and a
bare background color, a masked export with Mask off, a mixed import
batch (corrupt, SVG and over-cap files) as one step with one status
message naming each failure, fresh ids after an undone import, the layer
list order, the color pick retarget, and the product name and version.
One guard: every DynamicResource key a view names is defined.
EOF
git log --oneline -1
```

Expected: one commit with the subject `test(app): replay the M3a ledger scenarios across session, regions, layers, import and export`.

- [ ] **Step 5: Este's smoke checklist (controller-run, with Este)**

This step is the controller's: Este runs the app and answers each item pass or fail, and the controller records the answers. Every item lists the ledger rows it is evidence for. A failed item takes those rows out of Step 7 and goes to the controller as a defect for the owning task.

Prepare the sample files and a clean settings state, then launch:

```bash
mkdir -p .superpowers/m3a-smoke
cp packs/drips/drip_blood_front_v1.png packs/drips/drip_blood_front_v2.png packs/word_art/word_626_anton.png .superpowers/m3a-smoke/
printf '<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"/>' > .superpowers/m3a-smoke/logo.svg
printf 'not an image' > .superpowers/m3a-smoke/notes.png
rm -f "$LOCALAPPDATA/RBX15/settings.json"
dotnet run --project src/RBX15.App/RBX15.App.csproj -c Release
```

Este also needs one JPG photo of his own in `.superpowers/m3a-smoke/`, taken in portrait on a phone (so it carries an EXIF orientation). Note the Windows display scale in use (Settings, System, Display) next to the results.

| # | Do | Expect | Rows it evidences |
| --- | --- | --- | --- |
| S1 | Launch (first run, no settings file). | The caption reads `RBX15 Classic Shirt and Pants Maker`. The window opens centered at its default size, about 1200x800. The header reads `RBX15 Classic Shirt & Pants Maker` with the byline ending `V5.0` (4.1 shows `ITSJUSTESTE \| V4.1`, `editor.html:16-17`, uppercased by `styles.css:129-155`), and the footer reads `DEVELOPED AT 626 LABS LLC` (`editor.html:343`). Only the Home tab shows. There are no Tools, Brush or Placement groups and no Save work or Open buttons; the File group shows Add image and Download template. The canvas shows the empty template at 100%. | `shell-window-title`, `shell-window-size` |
| S2 | Move and resize the window, close, relaunch. Then maximize, close, relaunch. Then close, open `%LOCALAPPDATA%\RBX15\settings.json` in Notepad, set the left coordinate to `-100000`, save, relaunch. | It comes back at the moved size and position, then maximized, then centered at the default size, because the off-screen placement is refused. | `shell-window-size` |
| S3 | With no layer, click the Front region tile. Click it again. Right-click it. Right-click an unpainted tile. Click Front, choose Gradient, then drag the angle slider. Click Color 1, pick a color, OK; click Color 1 again and Cancel. Tab to a region tile and press Space. | Front paints cyan (Solid, `#17d4fa`). The second click leaves Undo's depth unchanged: one Undo returns to empty. Right-click clears it; right-click on an unpainted tile does nothing (no context menu, no undo step). Choosing Gradient turns Front into a gradient live, and the angle drag rotates it live; one Undo returns it to the starting angle. Color 1 opens the Windows color dialog on the current color; OK recolors Front; Cancel changes nothing. The focused tile shows the cyan 2 px ring, and Space paints it. | `regions-right-click-clear`, `fillstyle-color-pickers` |
| S4 | Paint two tiles, click SHIRT. Undo. | The button reads PANTS, the fills are gone and the limb headings read Right Leg and Left Leg. Undo brings back SHIRT with both fills. | (sanity: `mode-switch-*`) |
| S5 | Add image: pick the JPG, the two drips and the Word Art PNG (Ctrl-click, in that order). Undo once, then Redo. Drag `logo.svg` and `notes.png` onto the drop zone. Drag one drip over the ribbon and over the layers list, then drop it on the canvas near the top-left corner of the front torso. Tab to the drop zone and press Enter. | Four layers named by file name without the extension, in the order the file dialog returned them, the last one selected and on top. The portrait JPG shows upright, as 4.1 shows it, and is fit by its upright size. One Undo removes all four; Redo brings them back. The drop zone highlights while dragging over it. The two bad files add no layer, and the status strip names each one; it can be dismissed. Over the ribbon and the list the cursor shows no-drop and nothing happens. On the canvas the layer lands centered on the drop point. Enter on the drop zone opens the image picker. | `panel-drop-zone`, `shell-external-drop-guard`, `image-decode-exif-orientation` |
| S6 | In the layers list: Hide a layer, Undo; Dupe a layer; Up and Down; Del the selected layer. Then do the same with the keyboard only (Tab, the arrow keys, Space or Enter). With a layer selected, click the Back region tile. Then click empty canvas, and click the Front region tile. Then click a layer on the canvas. | Hide dims the row's name and the button reads Show; Undo shows it again. Dupe puts `<name> copy` right above the original, selected, offset 10 px. Up and Down move the layer one place and do nothing at the ends. Del selects the new top layer. Every stop shows the cyan focus ring. The heading reads `Layers` with no "(drag to reorder)" hint, because drag lands in M3b. The Back click fits the selected layer centered in the back face at 85%, rotation 0. The empty-canvas click clears the selection (no row highlighted), so the Front click paints Front; the click on a layer selects its row (the topmost layer there). | `layers-list` |
| S7 | Undo and Redo from launch; Clear; Undo. Untick Mask; Undo. Tick Grid. | Undo and Redo are dimmed at launch, Undo enables after an edit, and Redo after an undo. Clear removes every layer but keeps the fills; Undo brings the layers back. With Mask off, art outside the template shows; Undo turns Mask back on in one step. Grid draws faint yellow outlines on all 18 faces. | (sanity: `home-edit-group`, `home-settings-group`) |
| S8 | Zoom with the pill: minus down to the floor, plus up to the ceiling. At 300%, use the scrollbars. | The label steps 75, 50, 25% and stops at 25%, where the minus button greys out; it then climbs to 300% and stops, where the plus button greys out. Below 100% the template is smooth; at 100% and above the pixels are crisp. At 300% scrollbars appear and reach the top torso face (`torso_up`) and the bottom limb faces; nothing is clipped out of reach. | `canvas-zoom-controls`, `canvas-zoom-clipped-no-pan`, `zoom-pill-limits` |
| S9 | Relaunch fresh and click Download template; Cancel. Click it again; OK; cancel the save dialog. Type `My Shirt!` as the template name, paint a region, untick Mask, Download template, save. Open the PNG. | The empty-template dialog is themed, titled `RBX15 Classic Shirt and Pants Maker`, and reads `The template is empty — download a blank PNG anyway?`. Cancel opens no save dialog. OK opens the save dialog, and cancelling it writes nothing and shows no error. With a fill there is no prompt, and the dialog suggests `My_Shirt__roblox.png`. The file is 585x559 and transparent outside the template even though Mask was off. | (sanity: `export-png-guard`, `canvas-export-pipeline`) |

Record the results in `.superpowers/logs/m3a-smoke.txt` (git-ignored) as one line in this shape, which Step 9's commit body reads verbatim: `Smoke: S1-S9 pass at 150% display scale, run by Este on 2026-10-15.` Name any failed item and what Este saw, for example `Smoke: S1-S4, S6-S9 pass; S5 fails (the SVG drop showed no status message), at 100% display scale, run by Este on 2026-10-15.` If an item fails, the rows in its last column are removed from Step 7's ids and listed as left open with the reason. Sanity items close no row by themselves, but a failed sanity item is still a defect to report.

- [ ] **Step 6: Confirm the evidence for each row before closing it**

Save the evidence gate as `.superpowers/logs/m3a-evidence.py` (git-ignored):

```python
"""M3a Task 5 evidence gate. Usage: python m3a-evidence.py <normal-verbosity dotnet test log>
Prints `proven` or `OPEN` per candidate row and exits 1 if any row is OPEN. Smoke-checklist items
are not visible here; Step 7 gates them."""
import collections, json, re, sys

log = open(sys.argv[1], encoding="utf-8", errors="replace").read()
LINE = re.compile(r'^\s+(Passed|Failed|Skipped) RBX15\.(App|Engine|Parity)\.Tests\.([\w.]+)\.(\w+)(?:\((.*)\))? \[', re.M)
passed, failed = collections.Counter(), collections.Counter()
methods, view_passed = set(), set()
for status, proj, ns_class, method, args in LINE.findall(log):
    cls = ns_class.rsplit(".", 1)[-1]
    key = (proj, cls)
    if status == "Passed":
        passed[key] += 1
        methods.add((cls, method))
        if cls == "GoldenViewTests":
            m = re.match(r'name: "([^"]+)"', args or "")
            if m:
                view_passed.add(m.group(1))
    elif status == "Failed":
        failed[key] += 1

scenes = json.load(open("tools/parity-ref/view-scenes.json", encoding="utf-8"))
def view(pred):
    return [s["name"] for s in scenes if pred(s)]
# Each scene keeps zoom, mask and grid in its nested "view" object (Task 2 Step 5, tools/parity-ref/README.md).
def zoom(z):          return lambda s: s["view"]["zoom"] == z
def grid(mode):       return lambda s: s["view"].get("grid", False) and s.get("mode", "Shirt") == mode and s["view"]["zoom"] == 1
def mask(on):         return lambda s: s["view"].get("mask", True) == on
rich = lambda s: bool(s.get("layers")) and bool(s.get("regionColors"))

E, A = "Engine", "App"
SC = "LedgerScenarioTests"
S = {
    "mode": "Mode_switch_wipes_fills_keeps_layers_and_undo_brings_the_fills_back",
    "repaint": "Repainting_with_the_same_fill_is_no_step_and_clearing_an_unpainted_tile_is_none",
    "clear": "Clear_removes_only_layers_and_undo_restores_them",
    "thirty": "Thirty_one_edits_leave_thirty_undos_and_a_new_edit_empties_redo",
    "restore": "Undo_and_redo_restore_the_exact_export_through_every_kind_of_edit",
    "selection": "Selection_survives_undoing_a_region_paint",
    "maskgrid": "Mask_and_grid_toggles_are_one_step_each_and_never_reach_the_export",
    "asks": "Download_asks_first_with_only_hidden_layers_or_a_background_color_and_cancel_writes_nothing",
    "maskoff": "Download_with_the_mask_off_still_writes_the_masked_template",
    "batch": "A_mixed_batch_adds_the_good_files_in_pick_order_as_one_step_and_names_each_failure",
    "freshid": "Importing_after_undoing_an_import_takes_a_fresh_id",
    "list": "Three_imports_list_newest_first_and_hide_and_dupe_follow_the_z_order",
    "color": "A_color_pick_retargets_matching_regions_in_one_step_and_a_cancel_changes_nothing",
    "name": "The_product_name_and_version_come_from_one_place",
}
# row -> (test classes that must pass with no failure, scenario keys, view-scene predicates)
ROWS = {
    "mode-switch-effects": ([(E, "DocumentOpsTests"), (E, "TemplateTests"), (A, "RegionsGroupTests")], ["mode"], []),
    "mode-switch-undo-and-callers": ([(A, "ModeGroupTests"), (A, "EditorSessionTests")], ["mode"], []),
    "restore-mode-switch-side-effects": ([(A, "ModeGroupTests"), (A, "EditorSessionTests")], ["mode"], []),
    "tpl-active-regions-swap": ([(E, "DocumentOpsTests"), (E, "TemplateTests")], ["mode"], [grid("Pants")]),
    "claim-mode-per-mode-region-data": ([(E, "DocumentOpsTests")], ["mode"], []),
    "regions-click-paint": ([(E, "DocumentOpsTests"), (A, "RegionsGroupTests"), (E, "GoldenRenderTests")], ["repaint"], []),
    "regions-state-model": ([(A, "FillStyleTests"), (E, "ProjectIOTests")], [], []),
    "regions-grid-layout-pants": ([(A, "RegionsGroupTests")], [], []),
    "regions-right-click-clear": ([(A, "RegionsGroupTests")], ["repaint"], []),
    "btn-clear": ([(E, "DocumentOpsTests"), (A, "EditGroupTests")], ["clear"], []),
    "fillstyle-live-edit-rule": ([(E, "DocumentOpsTests"), (A, "FillStyleTests")], ["color"], []),
    "fillstyle-undo-snapshot-timing": ([(A, "FillStyleTests")], [], []),
    "fillstyle-color-pickers": ([(A, "FillStyleTests")], ["color"], []),
    "history-snapshot-coupling": ([(A, "EditorSessionTests"), (E, "HistoryTests")], ["restore"], []),
    "undo-restore": ([(A, "EditorSessionTests"), (E, "HistoryTests")], ["restore"], []),
    "undo-restore-ui-side-effects": ([(A, "EditorSessionTests")], ["selection", "restore"], []),
    "home-edit-group": ([(A, "EditGroupTests"), (E, "HistoryTests")], ["thirty"], []),
    "home-settings-group": ([(A, "SettingsGroupTests")], ["maskgrid", "maskoff"], [mask(False), grid("Shirt"), grid("Pants")]),
    "overlay-mask-checkbox": ([(A, "SettingsGroupTests")], ["maskgrid"], [mask(True), mask(False)]),
    "overlay-grid-checkbox": ([(A, "SettingsGroupTests")], ["maskgrid"], [grid("Shirt"), grid("Pants")]),
    "zoom-controls": ([(A, "CanvasViewModelTests")], [], [zoom(0.25), zoom(1), zoom(3)]),
    "canvas-zoom-controls": ([(A, "CanvasViewModelTests"), (E, "RendererViewTests")], [], []),
    "canvas-zoom-clipped-no-pan": ([(A, "CanvasViewTests")], [], []),
    "canvas-element-render": ([(E, "RendererViewTests")], [], [zoom(0.25), zoom(0.5), zoom(1), zoom(1.5), zoom(3), mask(True), mask(False), rich]),
    "render-checkerboard": ([(E, "RendererViewTests")], [], [zoom(1), zoom(3)]),
    "render-grid-overlay": ([(E, "RendererViewTests")], [], [grid("Shirt")]),
    "layers-add-placement": ([(E, "LayerOpsTests")], [], []),
    "layers-duplicate": ([(E, "LayerOpsTests"), (A, "LayersViewModelTests")], ["list"], []),
    "layers-remove": ([(E, "LayerOpsTests")], [], []),
    "layers-visibility": ([(E, "LayerOpsTests"), (A, "LayersViewModelTests")], ["list"], []),
    "layers-move-updown": ([(E, "LayerOpsTests"), (A, "LayersViewModelTests")], [], []),
    "layers-list": ([(A, "LayersViewModelTests")], ["list"], []),
    "layers-multi-file-async-order": ([(A, "ImportServiceTests")], ["batch"], []),
    "project-load-image-layer": ([(A, "ImageImporterTests"), (A, "ImportServiceTests")], [], []),
    "drop-filetypes": ([(A, "ImageImporterTests"), (A, "ImportServiceTests")], [], []),
    "panel-drop-zone": ([(A, "LayersViewModelTests"), (A, "ImportServiceTests"), (E, "LayerOpsTests")], [], []),
    "drop-panel": ([(A, "LayersViewModelTests"), (A, "ImportServiceTests")], [], []),
    "export-png-guard": ([(E, "ExportGuardTests"), (A, "FileGroupTests")], ["asks"], []),
    "io-empty-export-confirm": ([(E, "ExportGuardTests"), (A, "FileGroupTests")], ["asks"], []),
    "canvas-export-pipeline": ([(E, "GoldenRenderTests"), (A, "FileGroupTests")], ["maskoff"], []),
    "shell-window-title": ([], ["name"], []),
    "shell-window-size": ([(A, "PlacementGuardTests"), (A, "JsonSettingsStoreTests")], [], []),
    "shell-external-drop-guard": ([], [], []),
    "image-decode-caps": ([(E, "ImagesCapsTests"), (A, "ImageImporterTests")], ["batch"], []),
    "zoom-pill-limits": ([(A, "CanvasViewModelTests"), (A, "CanvasViewTests")], [], []),
    "image-decode-exif-orientation": ([(E, "ImagesOrientationTests")], [], []),
}

caps = open("src/RBX15.Engine/Images.cs", encoding="utf-8").read()
caps_ok = all(re.search(p, caps) for p in (r"MaxEncodedBytes\s*=\s*100L\s*\*\s*1024\s*\*\s*1024\s*;",
                                           r"MaxDimension\s*=\s*16384\s*;", r"MaxPixels\s*=\s*50_000_000\s*;"))
bad = 0
for row, (classes, scen, preds) in ROWS.items():
    why = []
    why += [f"{p}.{c} not run green" for p, c in classes if passed[(p, c)] == 0 or failed[(p, c)] > 0]
    why += [f"{SC}.{S[k]} did not pass" for k in scen if (SC, S[k]) not in methods]
    for i, pred in enumerate(preds):
        names = view(pred)
        if not names or not all(n in view_passed for n in names):
            why.append(f"view predicate {i}: scenes {names} not all passed")
    if row == "image-decode-caps" and not caps_ok:
        why.append("Images.cs caps differ from the row text (100 MiB, 16384, 50,000,000)")
    bad += bool(why)
    print(("proven   " if not why else "OPEN     ") + row + ("" if not why else "  " + "; ".join(why)))
print(f"{len(ROWS)} rows; {len(view_passed)} of {len(scenes)} view scenes passed; "
      f"{passed[(A, SC)]} {SC} cases passed; failures in any class: {sum(failed.values())}")
sys.exit(1 if bad else 0)
```

Run the whole suite at normal verbosity and feed the log to the gate:

```bash
dotnet test src/RBX15.slnx -c Release --no-build --logger "console;verbosity=normal" > .superpowers/logs/m3a-evidence.log 2>&1
python .superpowers/logs/m3a-evidence.py .superpowers/logs/m3a-evidence.log
```

Expected: `proven` for each of the 46 rows, then `46 rows; 14 of 14 view scenes passed; 14 LedgerScenarioTests cases passed; failures in any class: 0`, and exit 0. An `OPEN` row prints its reason: a class that did not run green, a scenario that did not pass, a view-scene predicate with no passing scene, or `Images.cs` caps that differ from the `image-decode-caps` row text. In that last case the row text is wrong, not the code: stop and report it, because the row's wording has to follow Task 4's stated values.

The gate proves that the right test classes ran green. It cannot prove that each class pins the exact behavior a row needs, so list the tests Tasks 1-4 wrote and find a name for each pin below:

```bash
grep -n "public void\|public async Task" src/RBX15.App.Tests/*Tests.cs src/RBX15.Engine.Tests/DocumentOpsTests.cs src/RBX15.Engine.Tests/LayerOpsTests.cs src/RBX15.Engine.Tests/ImagesCapsTests.cs src/RBX15.Engine.Tests/ExportGuardTests.cs src/RBX15.Engine.Tests/RendererViewTests.cs
```

Write the test name next to each row in the table below, and write down each row whose pin has no test. `F1` to `F14` are Step 2's facts in file order (`F1` `Mode_switch_*` to `F14` `The_product_name_*`), and `smoke Sn` is Step 5's item.

| Row | Behavior that must be pinned | Evidence | 4.1 source |
| --- | --- | --- | --- |
| `mode-switch-effects` | `SwitchMode` sets the mode, empties fills, and keeps layers, background, Mask and Grid; the Pants tiles carry `waist_`/`rleg_`/`lleg_` keys | `DocumentOpsTests` (T3), `RegionsGroupTests` (T3), `TemplateTests` (M1 mask), F1 | `editor.js:1695-1717` |
| `mode-switch-undo-and-callers` | the toggle and the wipe are one step; Undo restores the mode with its fills; Redo wipes again | `ModeGroupTests` (T3), `EditorSessionTests` (T1), F1 | `editor.js:1719-1725` |
| `restore-mode-switch-side-effects` | as above, starting from one painted torso_front | F1, `ModeGroupTests` | `editor.js:1427`, `:1444-1448` |
| `tpl-active-regions-swap` | the region table, mask and grid follow the mode, and fills are emptied | `DocumentOpsTests`, `TemplateTests`, the view golden with Grid on in Pants | `editor.js:62-64`, `:1696-1724` |
| `claim-mode-per-mode-region-data` | Shirt, then Pants, then Shirt leaves the fills empty; two Undos bring them back | F1 | `editor.js:1710-1711` |
| `regions-click-paint` | `PaintRegion` replaces an existing key in place; a repaint with the same fill is no step | `DocumentOpsTests`, `RegionsGroupTests`, F2; fill pixels: M1 `GoldenRenderTests` | `editor.js:1668-1674` |
| `regions-state-model` | `FillStyleViewModel` defaults to (solid, `#17d4fa`, `#f22f89`, 45, 16), with Angle held to 0-360 and Scale to 4-48 | `FillStyleTests` (T3); `ProjectIOTests` legacy string fills (M2b) | `editor.js:1552`, `editor.html:148-162` |
| `regions-grid-layout-pants` | in Pants the limb labels are Right Leg and Left Leg, and every tile's `Region` has a `waist_`, `rleg_` or `lleg_` prefix | `RegionsGroupTests` | `editor.js:1616-1646` |
| `regions-right-click-clear` | Clear on an unpainted tile is no step; on a painted tile it is one step and removes the fill and swatch | `RegionsGroupTests`, F2, smoke S3 | `editor.js:1677-1684` |
| `btn-clear` | Clear removes the layers and the selection only; Undo restores them | `DocumentOpsTests`, `EditGroupTests`, F3 | `editor.js:1394` |
| `fillstyle-live-edit-rule` | the row's scenario: regions sharing the old key follow each edit; others stay | `DocumentOpsTests` (`RetargetFill`), `FillStyleTests`, F13 | `editor.js:1566-1598` |
| `fillstyle-undo-snapshot-timing` | a slider drag that touches a matching region is 1 step; a drag with no match is 0; a keyboard change is 1 | `FillStyleTests` | `editor.js:1570-1579`, `:1608-1613` |
| `fillstyle-color-pickers` | a pick is stored verbatim and retargets regions in one step; Cancel does nothing; the picker opens on the current color | `FillStyleTests`, F13, smoke S3 | `editor.html:157-158`, `editor.js:1608-1612` |
| `history-snapshot-coupling` | undo and redo across fill and layer-adjustment edits give identical exports | `EditorSessionTests`, `HistoryTests` (M2a), F5 | `editor.js:84-86`, `:1444`, `:1465` |
| `undo-restore` | undo and redo through mode, background color, fill, and layer add and remove give byte-equal exports | F5, `EditorSessionTests` | `editor.js:1422-1468` |
| `undo-restore-ui-side-effects` | the selection survives undo while its layer exists | `EditorSessionTests`, F6 | `editor.js:1446-1451` |
| `home-edit-group` | Undo and Redo start disabled; 30 of 31 edits can be undone; a new edit empties redo | `EditGroupTests`, F4 | `editor.js:1470-1487` |
| `home-settings-group` | Mask and Grid are one step each; the export is always masked; Mask off and Grid on match the view goldens | `SettingsGroupTests`, F7, F9, view goldens | `editor.js:1493-1495` |
| `overlay-mask-checkbox` | as above, Mask half; the view golden pair with Mask on and off | `SettingsGroupTests`, F7, view goldens | `editor.js:332-337`, `:1494` |
| `overlay-grid-checkbox` | as above, Grid half; Grid view goldens at zoom 1 in Shirt and Pants | `SettingsGroupTests`, F7, view goldens | `editor.js:352-359`, `:1495` |
| `zoom-controls` | the 12 labels from 25% to 300%; view goldens at 0.25, 1 and 3 | `CanvasViewModelTests` (T2), view goldens | `editor.js:1489-1491` |
| `canvas-zoom-controls` | five + clicks from 100% reach 225%; the zoom clamps at 25% and 300%; the frame is `ViewSize` | `CanvasViewModelTests`, `RendererViewTests`, smoke S8 | `editor.js:1489-1491`, `editor.html:332-336` |
| `canvas-zoom-clipped-no-pan` | the scrollbars reach every face at 300% | smoke S8, `CanvasViewTests` (`The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits`) | `styles.css:441-456` |
| `canvas-element-render` | the view goldens at every captured zoom, Mask on and off, with fills and layers | `RendererViewTests`, `GoldenViewTests` | `editor.js:300-438` |
| `render-checkerboard` | the checker tile matches at zoom 1 and 3 | `GoldenViewTests` | `editor.js:345-348`, `:431-438` |
| `render-grid-overlay` | the Grid view golden at zoom 1 | `GoldenViewTests` | `editor.js:352-359` |
| `layers-add-placement` | `Place` for 512x512 and 100x400 to 1e-9; a drop at (300, 200) centers there; `DefaultName(id)` is `Image <id>` | `LayerOpsTests` (T4) | `editor.js:500-526` |
| `layers-duplicate` | placed at index+1, offset +10/+10, named `<name> copy`, selected; Undo removes it | `LayerOpsTests`, `LayersViewModelTests`, F12 | `editor.js:542-560` |
| `layers-remove` | a missing id changes nothing (no step); removing the selected layer selects the top layer or none; otherwise the selection is kept | `LayerOpsTests` | `editor.js:562-568` |
| `layers-visibility` | the toggle is one step and Undo restores it | `LayerOpsTests`, `LayersViewModelTests`, F12 | `editor.js:570-573` |
| `layers-move-updown` | swaps with the neighbor, does nothing at the ends, and is one step | `LayerOpsTests`, `LayersViewModelTests` | `editor.js:846-853` |
| `layers-list` | rows are newest first; Hide shows the Show label; Dupe goes above; the order equals the z-order | `LayersViewModelTests`, F12, smoke S6 | `editor.js:671-744` |
| `layers-multi-file-async-order` | the given order is kept as one step | `ImportServiceTests`, F10 | `editor.js:998-1011` |
| `project-load-image-layer` | PNG, JPG, GIF, BMP and WEBP decode and are named by file name; SVG and .txt fail with a message | `ImageImporterTests`, `ImportServiceTests` | `editor.js:2692-2708` |
| `drop-filetypes` | as above, plus an extensionless text file fails | `ImageImporterTests`, `ImportServiceTests` | `editor.js:992-1012` |
| `panel-drop-zone` | `DropFiles` with 3 PNGs centers them in front at 0.85 as one step; a non-image file shows a status message | `LayersViewModelTests`, `ImportServiceTests`, `LayerOpsTests`, smoke S5 | `editor.js:951-958`, `:988-1012` |
| `drop-panel` | `DropFiles` with 3 PNGs gives 3 layers centered on torso_front | `LayersViewModelTests`, `ImportServiceTests` | `editor.js:951-958` |
| `export-png-guard` | the message is verbatim; Cancel writes nothing; OK writes; a region fill does not prompt | `ExportGuardTests`, `FileGroupTests`, F8, F9 | `editor.js:1500-1506` |
| `io-empty-export-confirm` | a background color alone, and hidden-only layers, still prompt | `ExportGuardTests`, `FileGroupTests`, F8 | `editor.js:1502-1507` |
| `canvas-export-pipeline` | the export is always masked; the file name; the prompt | `GoldenRenderTests` (M1), `FileGroupTests`, F9 | `editor.js:1500-1542` |
| `shell-window-title` | `AppInfo.ProductName` | F14, smoke S1 | `main.js:8`, `editor.html:6` |
| `shell-window-size` | the default 1200x800 centered; off-screen refused; atomic save | `PlacementGuardTests`, `JsonSettingsStoreTests` (T1), smoke S1-S2 | `main.js:5-13` |
| `shell-external-drop-guard` | drops land only on the drop zone and the canvas | smoke S5 | `editor.js:955-957` |
| `image-decode-caps` | the three caps refuse before decode; a batch names the over-cap file | `ImagesCapsTests` (T4), `ImageImporterTests`, F10 | `editor.js:992-1011` |
| `zoom-pill-limits` | each zoom command is disabled at its limit and enabled one step in; an Execute at the limit clamps; the button shows disabled | `CanvasViewModelTests` (`Each_button_is_disabled_at_its_limit_and_comes_back`, T2), `CanvasViewTests`, smoke S8 | `editor.js:1490-1491` |
| `image-decode-exif-orientation` | all 8 EXIF orientations decode upright at their displayed size; a portrait phone photo imports upright | `ImagesOrientationTests` (T4), smoke S5 | `editor.js:500-526` |

Then apply the gate:

- **An `OPEN` row, a pin with no test, or a failed smoke item for that row:** take the row out of Step 7's command, and add it with the reason to the "left open" list in the Step 9 commit body. Report it to the controller. Do not write the missing test here; the gap belongs to the task that owns the module.
- **Everything else** closes in Step 7.

- [ ] **Step 7: Amend, add, narrow and close**

Save the ledger edit as `.superpowers/logs/m3a-ledger.py` (git-ignored). It refuses to run on a ledger that changed since planning:

```python
"""M3a Task 5: amend two Mask/Grid rows, reword five divergence texts, add five rows, and narrow the
parityCheck of the rows being closed. Usage: python m3a-ledger.py <parity-ledger.json> <id to close>...
Run once, before `ledger.py done` with the same ids. Fails loudly if any row moved since planning."""
import json, sys

path, closing = sys.argv[1], set(sys.argv[2:])
data = json.load(open(path, encoding="utf-8"))
rows = {r["id"]: r for r in data["rows"]}
assert len(data["rows"]) == 408, f"expected 408 rows, found {len(data['rows'])}"
NEW_IDS = {"image-decode-caps", "canvas-device-pixel-render", "font-ui-weight-600", "zoom-pill-limits", "image-decode-exif-orientation"}
assert not (NEW_IDS & rows.keys()), "the M3a rows were already added"

# 1. Mask and Grid toggles become one undo step each (interfaces.md section 1, file-undo-snapshot-scope).
AMEND = {
    "overlay-mask-checkbox": "4.1: toggling Mask re-renders without an undo step, though showMask is in every undo snapshot and project file, so a later Undo of an unrelated edit flips it back (editor.js:1494, :1404-1415, :1432-1436). v5: each Mask toggle is exactly one undo step.",
    "overlay-grid-checkbox": "4.1: toggling Grid re-renders without an undo step, though showGrid is in every undo snapshot and project file, so a later Undo of an unrelated edit flips it back (editor.js:1495, :1404-1415, :1432-1436). v5: each Grid toggle is exactly one undo step.",
}
for rid, text in AMEND.items():
    r = rows[rid]
    assert (r["disposition"], r["divergence"], r["status"]) == ("keep", "", "open"), f"{rid} was already amended"
    r["disposition"], r["divergence"] = "fix", text

# 2. Divergence texts reworded to what M3a ships (each row stays a fix row; the old text must match).
REWORD = {
    "render-zoom-canvas": (
        "4.1: canvas is resized and reset every frame with truncated fractional dimensions, giving a half-pixel edge offset at fractional zoom, and it grows with zoom. v5: exact scaled size in a scroll host with scrollbars to pan, zoom 25-300% in 0.25 steps.",
        "4.1: the canvas is reallocated and cleared on every render and clipped when it outgrows the area. v5: the frame keeps 4.1's truncated size (TW*zoom, editor.js:340-341), renders into a reused bitmap, and sits in a scroll host with scrollbars to pan; zoom stays 25-300% in 0.25 steps.",
    ),
    "shell-window-size": (
        "4.1: fixed 1200x800 centered window with no memory of size or position. v5: window remembers size and position.",
        "4.1: fixed 1200x800 centered window with no memory of size or position. v5: window remembers size and position. The window has a 640x480 minimum (4.1 had none, and its panels overlapped when shrunk).",
    ),
    "layout-template-regions": (
        "4.1: region tiles are mouse-only divs and the Front tile permanently loses its built-in tint after any paint or clear. v5: keyboard-reachable tiles with automation names and a consistent Front tile style, with region keys resolved through semantic slots per mode.",
        "4.1: region tiles are mouse-only divs and the Front tile permanently loses its built-in tint after any paint or clear. v5: keyboard-reachable tiles with automation names and a consistent Front tile style, with region keys resolved through semantic slots per mode. Delete, Shift+F10 and the Menu key clear a focused tile, as right-click does.",
    ),
    "fills-to-css": (
        "4.1: swatch gradients use the CSS angle convention (0deg is bottom-to-top) so direction can contradict the canvas render (0 is left-to-right). v5: swatches are drawn with the canvas angle convention so they match the render (clearly unintended inconsistency).",
        "4.1: swatch gradients use the CSS angle convention (0deg is bottom-to-top) so direction can contradict the canvas render (0 is left-to-right). v5: swatches are drawn with the canvas angle convention so they match the render (clearly unintended inconsistency). Each swatch is the region painted by the canvas painter with the region as its own gradient extent, so a tile shows the full gradient even where the canvas spans a group.",
    ),
    "io-image-formats": (
        "4.1: accept=image/* silently lets SVG/AVIF through the picker. v5: explicit PNG/JPG/GIF/WEBP filter, SVG import dropped with a message, first frame only for animated files.",
        "4.1: accept=image/* silently lets SVG/AVIF through the picker. v5: explicit PNG/JPG/GIF/WEBP/BMP filter, SVG import dropped with a message, first frame only for animated files.",
    ),
}
for rid, (old, new) in REWORD.items():
    r = rows[rid]
    assert (r["disposition"], r["status"]) == ("fix", "open") and r["divergence"] == old, f"{rid} divergence moved since planning"
    r["divergence"] = new

# 3. New rows.
NEW = [
    {
        "id": "image-decode-caps",
        "area": "file-drop",
        "name": "Imported image size caps (file bytes, side length, pixel count)",
        "source": "gap:m3a-import",
        "behavior": "4.1 reads every dropped or picked image whole with FileReader and decodes it with an Image element (editor.js:992-1011, :2692-2707). It sets no limit of its own on file size, image dimensions or pixel count.",
        "codeRefs": ["editor.js:992-1011", "editor.js:2692-2707"],
        "quirks": "Cap rationale: 50,000,000 pixels admits 48 MP phone photos (8000x6000); at 4 bytes per pixel the unpremultiplied layer image and its premultiplied draw copy then take about 400 MB together. 16384 per side bounds long strips that stay under the pixel cap. The 100 MiB file cap is checked from the file length before any byte is read.",
        "portTarget": "Images.Decode reads SKCodec.Info and refuses before allocating: MaxEncodedBytes 100 MiB, MaxDimension 16384 per side, MaxPixels 50,000,000. It throws ImageTooLargeException, which ProjectIO's layer decode catches alongside InvalidDataException, so M4's Open inherits the caps.",
        "risk": "med",
        "parityCheck": "ImagesCapsTests: a payload over 100 MiB, a side over 16384 and a pixel count over 50,000,000 are each refused before decode, and images at the caps decode. ImageImporterTests and LedgerScenarioTests: an over-cap file in a batch is reported on the status strip by file name while the other files import as one undo step.",
        "owner": "Engine.Document",
        "check": "unit",
        "disposition": "fix",
        "divergence": "4.1: imports set no limit on file size, image dimensions or pixel count. v5: an image file over 100 MiB, with a side over 16384 px, or over 50,000,000 pixels is refused before decoding, with a status message naming the file; 48 MP phone photos fit.",
        "status": "open",
    },
    {
        "id": "canvas-device-pixel-render",
        "area": "canvas",
        "name": "View canvas rendered at device pixels",
        "source": "gap:m3a-view",
        "behavior": "The view canvas backing store is TW*zoom x TH*zoom CSS pixels (editor.js:339-341) with no devicePixelRatio handling, so above 100% display scale Chromium stretches it onto the device pixels: at 150% and zoom 1 a 585-pixel bitmap covers 877 device pixels (cross-window-dpi-rendering).",
        "codeRefs": ["editor.js:339-350"],
        "quirks": "",
        "portTarget": "Renderer.RenderView with ViewOptions.DeviceScale set to the monitor's DPI scale; TemplateCanvasHost shows the frame 1:1 in device pixels under per-monitor DPI v2 and re-renders on DPI change. The checker half-tile is round(8 x scale) device px and the grid line is one CSS px wide.",
        "risk": "low",
        "parityCheck": "RendererViewTests: the frame is Renderer.ViewSize at DeviceScale 1.25, 1.5 and 2, and at DeviceScale 1 it equals the view goldens. By hand at 125% or 150% display scale: template edges are crisp at zoom 1, and the export stays 585x559.",
        "owner": "App.Canvas",
        "check": "manual",
        "disposition": "fix",
        "divergence": "4.1: above 100% display scale the view canvas is a CSS-pixel bitmap stretched by Chromium, so the template, checker and grid look soft. v5: the view is rendered at device pixels and stays crisp at every display scale; at 100% it equals 4.1's view, and the export is unchanged.",
        "status": "open",
    },
    {
        "id": "font-ui-weight-600",
        "area": "fonts",
        "name": "UI labels at CSS weight 600 in the vendored variable fonts",
        "source": "gap:m3a-theme",
        "behavior": "Chromium instances the Google variable fonts at weight 600 for the header title, the ribbon tabs, the ribbon group labels and the panel headings (styles.css:142, :175, :220, :261).",
        "codeRefs": ["styles.css:142", "styles.css:175", "styles.css:220", "styles.css:261"],
        "quirks": "WPF exposes only the named instances of a variable font file. Space Grotesk lists Light, Regular, Medium and Bold; JetBrains Mono lists Thin through ExtraBold without SemiBold. Inter has a real SemiBold.",
        "portTarget": "The theme asks for SemiBold; WPF resolves it to the Bold (700) instance of Space Grotesk and JetBrains Mono and to Inter SemiBold. Vendoring static 600 cuts would mean new font files and manifest rows.",
        "risk": "low",
        "parityCheck": "ThemeTokenTests.Ui_fonts_resolve_to_the_vendored_files pins the face each family resolves to; the M3b side-by-side sign-off compares the header title, ribbon tabs, group labels and panel headings.",
        "owner": "App.Shell",
        "check": "manual",
        "disposition": "fix",
        "divergence": "4.1: Chromium instances the Google variable fonts at weight 600 for the header title, ribbon tabs, group labels and panel headings (styles.css:142, :175, :220, :261). v5: WPF exposes only the named instances of the vendored variable files, and Space Grotesk and JetBrains Mono have no 600 instance there, so those labels draw at 700. Inter's 600 is exact.",
        "status": "open",
    },
    {
        "id": "zoom-pill-limits",
        "area": "zoom",
        "name": "Zoom buttons disabled at 25% and 300%",
        "source": "gap:m3a-view",
        "behavior": "4.1's zoom buttons stay enabled at 25% and 300%; a click there clamps to a no-op (editor.js:1490-1491; zoom-controls quirks: no disabled state at limits).",
        "codeRefs": ["editor.js:1490-1491", "styles.css:414-416"],
        "quirks": "",
        "portTarget": "CanvasViewModel.ZoomInCommand and ZoomOutCommand cannot execute at MaxZoom and MinZoom; CanvasView's ZoomButton style draws a disabled button at .btn:disabled's opacity .4. An Execute past a limit still clamps.",
        "risk": "low",
        "parityCheck": "CanvasViewModelTests.Each_button_is_disabled_at_its_limit_and_comes_back: each command is disabled at its limit and enabled again one step in, and an Execute at the limit still clamps. CanvasViewTests.The_host_and_the_pill_follow_the_view_model: the zoom-out button is disabled at 25%.",
        "owner": "App.Canvas",
        "check": "unit",
        "disposition": "fix",
        "divergence": "4.1: the zoom buttons stay enabled at 25% and 300%, and a click there clamps to a no-op (editor.js:1490-1491). v5: each button is disabled at its limit (opacity .4, styles.css:414-416), so no button is dead.",
        "status": "open",
    },
    {
        "id": "image-decode-exif-orientation",
        "area": "file-io",
        "name": "EXIF orientation applied when an image is decoded",
        "source": "gap:m3a-import",
        "behavior": "Chromium draws img elements with the EXIF orientation applied (image-orientation: from-image), so 4.1 shows a portrait phone photo upright and addLayer fits it by its displayed size (img.width and img.height, editor.js:503). A 4.1 project stores the original data URL (editor.js:2580), so the photo is upright again on load.",
        "codeRefs": ["editor.js:500-526", "editor.js:2580"],
        "quirks": "A plain SKCodec decode returns the stored pixels, which for most phone photos are sideways.",
        "portTarget": "Images.Decode applies SKCodec.EncodedOrigin, so the decoded image has its displayed size; import and M4's project open share the path.",
        "risk": "med",
        "parityCheck": "ImagesOrientationTests.Exif_orientation_is_applied_like_chromium, 8 orientations. By hand in the M3a smoke checklist: a portrait phone photo imports upright.",
        "owner": "Engine.Document",
        "check": "unit",
        "disposition": "keep",
        "divergence": "",
        "status": "open",
    },
]
data["rows"].extend(NEW)
rows.update({r["id"]: r for r in NEW})

# 4. parityCheck narrowed to the evidence used (ledger honesty rule), applied only to rows being closed.
NARROW = {
    "mode-switch-undo-and-callers": "Paint, toggle to Pants, Undo: Shirt mode with the fills restored; Redo: Pants with empty fills (EditorSession.Undo and Redo; ModeGroupTests, LedgerScenarioTests). Narrowed in M3a: the Ctrl+Z and Ctrl+Y keys land with M4's keyboard.",
    "regions-click-paint": "No selection: paint torso_front twice with the same style through its region tile: 1 undo step, because the repaint changes nothing; Undo returns to empty (RegionsGroupTests, LedgerScenarioTests). The 7 fill types' pixels are the M1 render goldens (fills-* rows). Reworded in M3a to v5's count; 4.1 pushes 2.",
    "regions-state-model": "A new document has empty RegionFills, and FillStyleViewModel.Current equals the default (solid, #17d4fa, #f22f89, 45, 16) with Angle held to 0-360 and Scale to 4-48 (FillStyleTests). A v4 project with string- and object-valued region entries round-trips through ProjectIOTests (M2b). Narrowed in M3a: the defaults are asserted on the view-model the XAML binds, not on the XAML.",
    "btn-clear": "After Clear: no layers and no selection, background and region fills intact; Undo restores every layer with its image; the export of the cleared document still holds the background and region fills (DocumentOpsTests, EditGroupTests, LedgerScenarioTests). Narrowed in M3a: Undo is the Edit group's command; the Ctrl+Z key lands with M4's keyboard.",
    "regions-right-click-clear": "Clear an unpainted region tile: the undo count is unchanged. Clear a painted one: +1 step, the tile's fill and swatch are gone, and the export is back to its pixels before the paint (RegionsGroupTests, LedgerScenarioTests run the tile's Clear command). The right-click binding is checked by hand in the M3a smoke checklist.",
    "io-empty-export-confirm": "Fresh document, Download: the confirm appears; Cancel: no save dialog; OK: the save dialog. A background color alone, and hidden-only layers, still prompt (FileGroupTests, LedgerScenarioTests). Narrowed in M3a: the background color is set on the document; the Layout tab's swatches land with M4.",
    "fillstyle-undo-snapshot-timing": "Drag fillAngle with a matching painted region: exactly 1 undo step, and Undo restores the starting-angle fills in one step; a drag with no matching region: 0 steps; a keyboard slider change: 1 step (FillStyleTests). Narrowed in M3a: Undo is the Edit group's command; the Ctrl+Z key lands with M4's keyboard.",
    "fillstyle-color-pickers": "A Color 1 pick through IColorPicker (a fake in tests) is stored verbatim as lowercase #rrggbb in the current fill and in every region sharing its key, as one undo step; a cancelled pick changes nothing; the picker opens on the current color (FillStyleTests, LedgerScenarioTests). The fill's pixels are the Skia painter's (M1 render goldens). The native ChooseColor dialog is opened by hand in the M3a smoke checklist. Narrowed in M3a: no UI automation run.",
    "undo-restore-ui-side-effects": "Select a layer, paint a region, Undo: the layer is still selected (EditorSessionTests, LedgerScenarioTests). Narrowed in M3a: the arrow nudge lands with M4's keyboard, and the Fill Style controls after an undo are checked on fillstyle-type-buttons.",
    "home-edit-group": "The history sequence of tests/history.test.js runs in C# (HistoryTests, M2a). Through the Edit group: Undo and Redo start disabled, after 31 edits only 30 undos are available, and a new edit empties redo (EditGroupTests, LedgerScenarioTests). Narrowed in M3a: the disabled look (opacity .4) is checked on cross-undo-disabled-style.",
    "home-settings-group": "Mask off: areas outside the template show in the view and the export is still masked (view goldens with Mask off; LedgerScenarioTests). Grid on: the region outlines match the view goldens at zoom 1 in Shirt and in Pants. Narrowed in M3a: zoom 2 not captured.",
    "overlay-mask-checkbox": "A layer placed outside every region: hidden in the view with Mask on, shown with it off, and the exported PNG hides it either way; each toggle is one undo step (LedgerScenarioTests, SettingsGroupTests, view goldens with Mask on and off). Narrowed in M3a: the layer is placed, not dragged; dragging lands with M3b's select tool.",
    "overlay-grid-checkbox": "View goldens: the grid lines in Shirt and in Pants at zoom 1 match 4.1 under the golden rule; the export with Grid on equals the export with Grid off; each toggle is one undo step (LedgerScenarioTests, SettingsGroupTests). Narrowed in M3a: zoom 2 not captured.",
    "canvas-zoom-controls": "Zoom in from 100% five times: 125, 150, 175, 200, 225%, and never past 300%; zoom out stops at 25% (CanvasViewModelTests). The frame is Renderer.ViewSize, 585*zoom px wide at display scale 1 (RendererViewTests). Panning by the scrollbars at 300% is checked by hand in the M3a smoke checklist.",
    "canvas-zoom-clipped-no-pan": "At 300% in a 1200x800 window, torso_up and the bottom limb faces are reached with the scrollbars, checked by hand in the M3a smoke checklist; CanvasViewTests pins that the scroll host gains scrollable extent when the frame outgrows the area. Narrowed in M3a: UI automation lands with M6.",
    "canvas-element-render": "View goldens (tests/goldens/view): the visible canvas at zoom 0.25, 0.5, 1, 1.5 and 3, with region fills and layers, Mask on and off, under the unchanged golden rule; the checkerboard is not scaled with zoom. Narrowed in M3a: zoom 2 not captured.",
    "render-checkerboard": "View goldens: the corner pixels (0,0), (8,0), (8,8) and (0,8) match 4.1 at every captured zoom, and the tile stays 16 px at zoom 3. Narrowed in M3a: zoom 4 is outside v5's 25-300% range.",
    "layers-visibility": "Toggle, then Undo: the visibility comes back in one step (LayerOpsTests, LayersViewModelTests, LedgerScenarioTests). Reworded in M3a to the fixed behavior; 4.1's toggle was not undoable.",
    "layers-move-updown": "Up and Down swap with the neighbor and do nothing at the ends; Undo reverts a move in one step (LayerOpsTests, LayersViewModelTests). Reworded in M3a to the fixed behavior; 4.1's Up and Down were not undoable.",
    "layers-list": "Add 3 layers: the newest is on top and selected; Hide flips the label to 'Show'; Dupe inserts the copy above the original and selects it; the list order equals the z-order (LayersViewModelTests, LedgerScenarioTests). The 40% name dim on a hidden layer and keyboard-only operation are checked by hand in the M3a smoke checklist.",
    "project-load-image-layer": "Add a PNG, JPG, GIF (first frame), BMP and WEBP: each becomes a layer named by its file name minus the last extension; an SVG and a .txt become no layer and each shows a status message (ImageImporterTests, ImportServiceTests). Reworded in M3a: v5 refuses SVG, so there is no SVG intrinsic size to compare.",
    "drop-filetypes": "Drop png, jpg, gif, webp, bmp, svg, txt and an extensionless text file: the five raster files become layers named by file name minus the last extension; svg, txt and the extensionless file become no layer and each shows a status message (ImageImporterTests, ImportServiceTests). Reworded in M3a: v5 refuses SVG.",
    "panel-drop-zone": "Drop 3 PNGs: three layers named by file name (extension stripped), centered in the front torso at 0.85 fit, as one undo step (LayersViewModelTests, ImportServiceTests, LayerOpsTests); a non-image file adds nothing and shows a status message, not a dialog. The drag-over highlight and reaching the zone with Tab and Enter are checked by hand in the M3a smoke checklist.",
    "shell-window-title": "AppInfo.ProductName, which is ProjectIO.AppName and which MainViewModel.Title and every dialog title return, equals 'RBX15 Classic Shirt and Pants Maker' (LedgerScenarioTests); the caption shows it in the M3a smoke checklist. Narrowed in M3a: the UI Automation Name read lands with M6.",
    "shell-window-size": "PlacementGuardTests: no saved placement, or one off every work area, gives 1200x800 centered on the primary work area, and a saved placement is restored and clamped to fit. JsonSettingsStoreTests: the placement round-trips through an atomic write. The M3a smoke checklist launches at the display's own scale, then moves, resizes, maximizes and relaunches. Narrowed in M3a: one display scale, not both 100% and 150%.",
    "shell-external-drop-guard": "Checked by hand in the M3a smoke checklist: a PNG dragged over the layers panel and the ribbon shows the no-drop cursor and does nothing; dropped on the drop zone it highlights the zone and lands centered in the front torso; dropped on the canvas it lands centered on the drop point. Narrowed in M3a: UI automation lands with M6.",
}
unknown = closing - rows.keys()
assert not unknown, f"unknown ids: {sorted(unknown)}"
for rid, text in NARROW.items():
    if rid not in closing:
        continue
    r = rows[rid]
    assert r["status"] == "open" and "M3a" not in r["parityCheck"], f"{rid} was already narrowed or closed"
    r["parityCheck"] = text

open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
print(f"amended 2, reworded {len(REWORD)}, added {len(NEW)}, narrowed {len(closing & NARROW.keys())}; {len(data['rows'])} rows")
```

Print the state first:

```bash
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
```

Expected: `408 rows; 81 done; 186 fix`.

Then run the edit and the close with the same ids, minus any id that Steps 5-6 removed. The edit narrows only the ids it is given; the amendments, the five rewords and the five new rows always apply:

```bash
IDS="mode-switch-effects mode-switch-undo-and-callers restore-mode-switch-side-effects tpl-active-regions-swap
claim-mode-per-mode-region-data regions-click-paint regions-state-model regions-grid-layout-pants
regions-right-click-clear btn-clear fillstyle-live-edit-rule fillstyle-undo-snapshot-timing fillstyle-color-pickers
history-snapshot-coupling undo-restore undo-restore-ui-side-effects home-edit-group home-settings-group
overlay-mask-checkbox overlay-grid-checkbox zoom-controls canvas-zoom-controls canvas-zoom-clipped-no-pan
canvas-element-render render-checkerboard render-grid-overlay layers-add-placement layers-duplicate layers-remove
layers-visibility layers-move-updown layers-list layers-multi-file-async-order project-load-image-layer
drop-filetypes panel-drop-zone drop-panel export-png-guard io-empty-export-confirm canvas-export-pipeline
shell-window-title shell-window-size shell-external-drop-guard image-decode-caps zoom-pill-limits
image-decode-exif-orientation"
python .superpowers/logs/m3a-ledger.py docs/port/parity-ledger.json $IDS
python tools/ledger/ledger.py done $IDS
python tools/ledger/ledger.py check
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
grep -n "^## " docs/port/divergences.md
git diff --numstat -- docs/port
```

Expected:

- `amended 2, reworded 5, added 5, narrowed 26; 413 rows`.
- `rendered docs/port/parity-ledger.md and docs/port/divergences.md`.
- `413 rows, 0 problem(s)`.
- `413 rows; 127 done; 192 fix`: the 81 already done plus the 46 ids. If Steps 5-6 removed ids, 127 minus their number. The fix count is 186 plus the 2 amended rows and the 4 new `fix` rows (`image-decode-exif-orientation` is `keep`).
- `## Fixed in v5 (192)`, `## Not carried into v5 (26)`, `## New in v5 (1)`, each with its line number.
- The numstat lines, measured by the reconciler on a scratch copy of `main`'s `docs/port` and `tools/ledger` on 2026-10-14 with all 46 ids and exactly this script:
  - `179	78	docs/port/parity-ledger.json`;
  - `49	44	docs/port/parity-ledger.md`;
  - `12	6	docs/port/divergences.md`.

  The JSON carries one `status` line per closed existing id (43; the other 3 closed ids are new rows), the 26 narrowed `parityCheck` lines, the two amended rows' `disposition` and `divergence` lines, the five reworded `divergence` lines, and the five new rows appended at the end. `parity-ledger.md` changes the counts line and the 43 closed existing rows, and adds the 5 new rows. `divergences.md` changes the "Fixed in v5" heading and the 5 reworded rows, and adds the 2 amended rows and the 4 new `fix` rows.

  Each removed existing id takes one line off each side of the JSON (two if Step 7 also narrows it) and one off each side of `parity-ledger.md`. A larger JSON diff means the file was reformatted: stop, run `git checkout -- docs/port`, and report it.

- [ ] **Step 8: Final verification**

Run, in order, from the repo root:

```bash
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror
dotnet test src/RBX15.slnx -c Release > .superpowers/logs/m3a-final.log 2>&1; grep -E "(Passed|Failed)!" .superpowers/logs/m3a-final.log
python -m unittest discover -s tools/ledger -p "test_*.py"
python tools/ledger/ledger.py check
node --test "tools/parity-ref/*.test.js"
npm test
git diff --stat "$BASE" -- main.js editor.js editor.html styles.css lib packs package.json package-lock.json "tests/*.test.js" windows
git diff --stat --diff-filter=MDRT "$BASE" -- tests/goldens assets/fonts
git diff --name-only --diff-filter=A "$BASE" -- tests/goldens assets/fonts | sed 's|/[^/]*$||' | sort | uniq -c
git log --format=%s "$BASE"..HEAD -- tests/goldens
grep -rnE "UseWPF|PresentationCore|PresentationFramework|WindowsBase|System\.Windows" src/RBX15.Engine --include=*.cs --include=*.csproj --exclude-dir=bin --exclude-dir=obj
grep -rnE "HttpClient|WebRequest|WebClient|System\.Net\.Sockets|TcpClient|ClientWebSocket" src/RBX15.Engine src/RBX15.App --include=*.cs --exclude-dir=bin --exclude-dir=obj
dotnet list src/RBX15.App/RBX15.App.csproj package
grep -rn "626" src/RBX15.App --include=*.cs --include=*.xaml --exclude-dir=bin --exclude-dir=obj
git diff "$BASE" -U0 -- src | grep -inE "^\+.*(api[_-]?key|secret|password|bearer )"
git status --short
```

Expected:

- `base 1126e7c`.
- Build: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: three summary lines, each with `Failed: 0` and `Skipped: 0`:
  - `RBX15.Engine.Tests.dll` `Passed: 716` (Task 5 adds none);
  - `RBX15.Parity.Tests.dll` `Passed: 35`;
  - `RBX15.App.Tests.dll` `Passed: 314`.
- Ledger unit tests: `Ran 8 tests`, `OK`. `ledger.py check` prints `413 rows, 0 problem(s)`.
- `node --test`: `# tests 14`, `# pass 14`, `# fail 0`. Main had 14 tests (measured 2026-10-14), and Task 2 adds no `.test.js` (its ruling R6). `npm test`: `# tests 31`, `# pass 31`, `# fail 0`.
- The Electron `git diff --stat` prints nothing: Electron is untouched.
- The `--diff-filter=MDRT` diff prints nothing. No existing golden, scene file or font changed, so `tests/goldens/render`, `tests/goldens/app`, `tests/goldens/projects` and `tests/goldens/fixtures` are frozen, and the UI fonts were already vendored in M2a.
- The added-files count prints exactly one line, `16 tests/goldens/view`: the 14 PNGs, `index.json` and `scenes.json` (Task 2 Step 10). The log prints exactly one subject, Task 2's controller-run capture commit. Anything else means a frozen file moved: stop and show `git log --stat "$BASE"..HEAD -- tests/goldens` to the controller.
- The WPF grep on the engine prints nothing: the engine never references WPF.
- The network grep prints nothing: no network requests.
- `dotnet list`: one top-level package, `CommunityToolkit.Mvvm`, with Requested and Resolved equal to the exact version Task 1 pinned.
- The `626` grep prints exactly one line, the footer string `Developed at 626 Labs LLC` (`editor.html:343`), in `MainViewModel`'s `FooterText`. Any other hit is branding 4.1 does not show: report it.
- The secrets grep prints nothing. Review any hit and report it; never commit a key.
- `git status --short` lists exactly three lines, each with `M` in the second column: `docs/port/divergences.md`, `docs/port/parity-ledger.json` and `docs/port/parity-ledger.md`. Logs, `.superpowers/m3a-smoke/` and `diff-heatmaps/` are git-ignored.

- [ ] **Step 9: Commit the ledger**

Write the body from the outcomes of Steps 5 and 6. The "Left open" list is the fixed table below, plus any row those steps removed, with its reason. The `Smoke:` line is read from `.superpowers/logs/m3a-smoke.txt` (Step 5). Add your own model's `Co-Authored-By` trailer as the last line before running it.

| Rows | Why open | Closes with |
| --- | --- | --- |
| `theme-tokens-colors`, `theme-tokens-shape-motion-type`, `theme-scrollbars`, `font-ui-runtime-google`, `font-ui-weight-600`, `shell-header`, `shell-footer`, `shell-layout-skeleton`, `ribbon-group-structure`, `ribbon-overflow-hscroll`, `cross-undo-disabled-style`, `mode-toggle-button`, `home-mode-toggle`, `regions-grid-layout-shirt`, `regions-hint-text`, `regions-button-swatch-paint`, `fills-to-css`, `region-front-highlight-lost`, `layers-list-render`, `cross-window-dpi-rendering`, `project-startup`, `canvas-device-pixel-render` | Their checks are screenshot or look comparisons. Token values are unit-tested (`ThemeTokenTests`), but the comparison itself is the side-by-side sign-off. | M3b look sign-off |
| Provisional values (no ledger row of their own): scrollbar width 8 and thumb inset 1 (`theme-scrollbars`); Color 1/2 swatch radius 2 (`fillstyle-color-pickers`' look); themed dialog MinWidth 360, MaxWidth 480, padding 20,18,20,16, 13px message, 18 DIP gap (`dialogs-inventory`); status strip padding 12,6, 12px text, 12 DIP gap (`diag-console-only-errors`) | No 4.1 source exists for them (critic P3); each is tagged "provisional" in its XAML. | M3b look sign-off |
| `render-zoom-canvas` | Its divergence now states 4.1's truncated frame size (reworded in Step 7); the scroll host still gets a look check. | M3b |
| `canvas-coord-map`, `regions-click-fit-layer`, `layers-select`, `layers-model`, `layers-default-name-offbyone` | Left button only and mouse capture; the inspector refresh after a fit; selection showing the property controls; an adjustment of 0 shown as 0; the drawing-layer default name. | M3b |
| `undo-empty-steps`, `undo-coverage-gaps`, `file-undo-snapshot-scope`, `claim-undo-everything`, `undo-snapshot-contents` | Their lists name draw, drag, wheel, preset, detail and keyboard actions. | M3b, M4 |
| `fillstyle-type-buttons`, `fillstyle-ui-not-synced-from-region` | Both need a project load. They also disagree on re-syncing the controls after undo, which needs a ruling before M4 closes them. | M4 |
| `fillstyle-angle-scale`, `fillstyle-live-render-hook` | The slider bounds by automation and the pixel diffs at slider extremes are not captured. | M4 or M6 |
| `layout-fill-style`, `layout-template-regions`, `ribbon-tab-switch`, `ribbon-tabs-switching` | M3a shows only the Home tab and carries Fill Style and Regions there until the Layout tab ships. | M4 |
| `drop-canvas`, `io-image-formats`, `image-decode-format-matrix`, `diag-console-only-errors`, `io-download-save-dialog`, `home-file-group`, `project-save-filename`, `dialogs-inventory`, `io-alert-inventory`, `mode-pants-crash-hardcoded-keys`, `bug-pants-mode-region-keys`, `shell-responsive-breakpoint`, `undo-buttons-shortcuts` | The stale asset drag; the Background picker; the other failure paths; Save and Open; the other dialogs; Details, presets, Fit and Word Art in Pants; the asset grid; the keyboard. | M4 |
| `shell-window-frame`, `shell-no-persistence`, `shell-lifecycle-quit`, `shell-unsaved-work-loss` | The close prompt, autosave and the privacy docs. | M5 |
| `shell-header-version-hardcoded`, `a11y-focus-rings` | The MSIX Identity version, and the Accessibility Insights scan. | M6 |

```bash
SMOKE=$(cat .superpowers/logs/m3a-smoke.txt)
git add docs/port/parity-ledger.json docs/port/parity-ledger.md docs/port/divergences.md
git commit -F - <<EOF
chore(ledger): close the rows M3a proves; add the M3a divergence rows

Closed by the M3a unit tests, the view goldens (GoldenViewTests), the
LedgerScenarioTests and Este's smoke checklist:
mode-switch-effects, mode-switch-undo-and-callers,
restore-mode-switch-side-effects, tpl-active-regions-swap,
claim-mode-per-mode-region-data, regions-click-paint, regions-state-model,
regions-grid-layout-pants, regions-right-click-clear, btn-clear,
fillstyle-live-edit-rule, fillstyle-undo-snapshot-timing,
fillstyle-color-pickers, history-snapshot-coupling, undo-restore,
undo-restore-ui-side-effects, home-edit-group, home-settings-group,
overlay-mask-checkbox, overlay-grid-checkbox, zoom-controls,
canvas-zoom-controls, canvas-zoom-clipped-no-pan, canvas-element-render,
render-checkerboard, render-grid-overlay, layers-add-placement,
layers-duplicate, layers-remove, layers-visibility, layers-move-updown,
layers-list, layers-multi-file-async-order, project-load-image-layer,
drop-filetypes, panel-drop-zone, drop-panel, export-png-guard,
io-empty-export-confirm, canvas-export-pipeline, shell-window-title,
shell-window-size, shell-external-drop-guard, image-decode-caps,
zoom-pill-limits, image-decode-exif-orientation.
26 parityCheck texts are narrowed to the evidence used ("Narrowed in
M3a" or "Reworded in M3a").

Amended to fix: overlay-mask-checkbox and overlay-grid-checkbox. Each
toggle is now one undo step; 4.1 pushed none while snapshotting the
state, so a later undo flipped it back.

Reworded to what M3a ships: render-zoom-canvas (4.1's truncated frame
size), shell-window-size (640x480 minimum), layout-template-regions (the
tile keyboard clear), fills-to-css (the swatch's own gradient extent)
and io-image-formats (BMP).

New: image-decode-caps (closed: 100 MiB, 16384 px per side, 50,000,000
pixels, refused before decoding and named on the status strip),
zoom-pill-limits (closed: the zoom buttons disable at 25% and 300%),
image-decode-exif-orientation (closed: decode applies EXIF orientation,
as Chromium does), canvas-device-pixel-render (open until the M3b look
sign-off: the view renders at device pixels; 4.1 stretched a CSS-pixel
canvas) and font-ui-weight-600 (open: weight 600 draws at 700 in two
vendored families).

Left open on purpose: the look rows wait for the M3b side-by-side
sign-off (theme tokens, scrollbars, UI fonts, header, footer, layout,
ribbon groups and overflow, button states, mode toggle look, region
grid look, hint text, swatches, front highlight, layer row look, DPI,
startup, canvas-device-pixel-render, font-ui-weight-600, and the
provisional dialog, strip, scrollbar and swatch values);
render-zoom-canvas (a look check of the scroll host); select-tool and
inspector halves
(canvas-coord-map, regions-click-fit-layer, layers-select, layers-model,
layers-default-name-offbyone); undo matrices that list M3b and M4
actions (undo-empty-steps, undo-coverage-gaps, file-undo-snapshot-scope,
claim-undo-everything, undo-snapshot-contents); fillstyle-type-buttons
and fillstyle-ui-not-synced-from-region (project load, and the two rows
disagree on re-syncing after undo: rule before M4); fillstyle-angle-scale
and fillstyle-live-render-hook (extremes not captured); the Layout tab
and tab switching (layout-fill-style, layout-template-regions,
ribbon-tab-switch, ribbon-tabs-switching); M4's file, dialog, asset,
Pants-placement and keyboard rows (drop-canvas, io-image-formats,
image-decode-format-matrix, diag-console-only-errors,
io-download-save-dialog, home-file-group, project-save-filename,
dialogs-inventory, io-alert-inventory, mode-pants-crash-hardcoded-keys,
bug-pants-mode-region-keys, shell-responsive-breakpoint,
undo-buttons-shortcuts); M5's close prompt and persistence rows
(shell-window-frame, shell-no-persistence, shell-lifecycle-quit,
shell-unsaved-work-loss); M6's shell-header-version-hardcoded (MSIX
version) and a11y-focus-rings (Accessibility Insights).

$SMOKE
Electron, the render, app and project goldens and assets/fonts are
untouched; tests/goldens/view was added once by Task 2's capture.
EOF
git log --oneline -2
git status --short
```

The heredoc is unquoted so `$SMOKE` expands; the body holds no other `$` or backtick.

Expected: two commits from this task, `chore(ledger): close the rows M3a proves; add the M3a divergence rows` on top of `test(app): replay the M3a ledger scenarios across session, regions, layers, import and export`, and an empty `git status --short`. If Steps 5-6 removed any id, delete it from the "Closed" paragraph and add it, with its reason, to the "Left open" paragraph before committing. Do not push: the controller handles push, the PR and the dashboard after the final review.

**Interface additions adopted (Task 5).**

1. **New test files (Task 5):** `src/RBX15.App.Tests/LedgerScenarioTests.cs`, class `LedgerScenarioTests` in namespace `RBX15.App.Tests`, with 14 facts and private nested fakes (`Picker : IColorPicker`, `Dialogs : IDialogService`, `Files : IFileDialogs`, `Status : IStatus`); and `src/RBX15.App.Tests/ThemeKeyGuardTests.cs` (1 fact).
2. **`StaThread` (Task 1)** is in namespace `RBX15.App.Tests`, so the scenarios call it unqualified.
3. **View scene fields (Task 2, as captured; critic C10).** Each entry of `tools/parity-ref/view-scenes.json` carries:
   - `name`;
   - `view`, an object with `zoom` (a number), `mask` (default true) and `grid` (default false);
   - `mode` (`"Shirt"` or `"Pants"`, default Shirt) at the top level;
   - the app scene format's `regionColors` and `layers`.

   The scene set includes at least:
   - each of zoom 0.25, 0.5, 1, 1.5 and 3;
   - one scene with Mask off and one with Mask on;
   - Grid on at zoom 1 in Shirt and Grid on at zoom 1 in Pants;
   - one scene with both `layers` and `regionColors`.

   Step 6's gate selects scenes by these fields.
4. **`GoldenViewTests` (Task 2)** is a `[Theory]` whose only parameter is `string name`, so normal-verbosity logs print `Passed RBX15.Engine.Tests.GoldenViewTests.<Method>(name: "<scene>")`, the way `GoldenAppTests` does.
5. **Synchronous view-model updates (Tasks 3 and 4).** `RegionTileViewModel.Fill` and `Swatch`, `SettingsGroupViewModel.ShowMask` and `ShowGrid`, and `LayersViewModel.Rows` update inside the `EditorSession.Changed` handler on the calling thread (no `Dispatcher.BeginInvoke`), so a test sees the new state when the command returns.
6. **`RegionTileViewModel.Swatch` is null when its region has no fill (Task 3).** The resting tint, including the Front tile's (`region-front-highlight-lost`), is XAML styling, not a swatch image.
7. **`FileGroupViewModel.DownloadTemplateCommand` (Task 4)** is synchronous: a `[RelayCommand]` over a `void` method. It runs `ExportGuard.HasArt`, then `IDialogService.Confirm(ExportGuard.EmptyTemplateMessage)` only when there is no art, then `IFileDialogs.SavePng(ExportNaming.PngFileName(session.TemplateTitle))`, and writes `Renderer.ExportPng(session.Document)` to the returned path with `File.WriteAllBytes`. A null path writes nothing and shows nothing.
8. **Import failure messages name the file (Task 4).** The one status message an `Import` call shows names every failed file (Task 4 ruling 5; critic C11). The user has to know which file of a batch failed.
9. **`AppInfo.Version` is exactly `"5.0.0"` (Task 1).** The SDK's source-revision suffix (`+<sha>`) is off (`IncludeSourceRevisionInInformationalVersion` false) or stripped, so the header never shows a commit hash.
10. **No "(drag to reorder)" hint in M3a (Task 4).** 4.1's Layers heading carries it (`editor.html:317`), but drag reorder lands in M3b. The hide-what-doesn't-work ruling applies, and smoke item S6 checks it.
