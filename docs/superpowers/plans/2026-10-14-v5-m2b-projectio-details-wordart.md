# RBX15 v5 M2b (ProjectIO, Details and Word Art) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**M2b finishes M2.** M2a froze every M2 golden while the real 4.1 app was still the reference, and skipped 62 app scenes by op name: 37 `detail`, 22 `wordart` and 3 `project`. This plan ports the three engine modules those ops need and enables the 62 scenes, one op per task. Nothing is recaptured.

**Goal:** Port 4.1's project save and load, its 16 clothing details and its Word Art renderer into `RBX15.Engine`, so that all 82 app goldens run, the detail and project scenes pass the unchanged golden rule, and the Word Art scenes pass the Word Art rule ruled on measured numbers (ruling R2), with 4.1's own Word Art bitmap holding layout and placement exact.

**Architecture:**

- **Engine.** Four new static modules over the M1 and M2a records:
  - `ProjectIO`: an all-or-nothing `.r15proj` reader that opens every 4.1 file, and a writer of saveProject's fields at version `5.0`.
  - `Details`: a port of createDetailLayer and its 16 generators, resolved through `Template.Slot` so Pants works.
  - `Fonts`: resolves the vendored faces from `assets/fonts` the way Chromium resolved 4.1's `@font-face` rules.
  - `WordArt`: a port of renderWordArt and Add to canvas, with HarfBuzz-shaped text.

  `History` gains `Clear()` for project open.
- **Parity.** `SceneLoader` runs the `project`, `detail` and `wordart` ops through the engine, and `GoldenAppTests` stops skipping them. Each op is enabled in one change: the op joins `SupportedOps` with its handler, leaves `PendingOps`, and loses its skip row. The accounting fact stays green throughout.
- **The Word Art guard.** `tools/parity-ref/wordart-ref.js` renders 4.1's own Word Art bitmap in the real `editor.html`. Task 3 captures it once into `tests/goldens/app/wordart-ref/` (frozen from then on), and `WordArtSwapTests` swaps it into the engine's document: the export must equal the golden exactly under the unchanged rule. That isolates text rasterization, the one thing the Word Art rule lets differ, from placement and compositing.
- **Carry-forwards from M2a.** `History.Clear()` (Task 1). The Renderer's Chromium-compat index distinguishes cc decode-cache levels from Skia mip levels, names the `ScalePixels` dependency, and states the bounded uncertainty in `SkiaMipLevel` (Task 4).

**Tech Stack:** .NET 10 (`net10.0`), C# with nullable and warnings as errors, SkiaSharp `4.153.1`, HarfBuzzSharp `14.2.1.301` (Task 3, approved in ruling R1), xUnit 2.9.3, Electron 41.1.0 (devDependency; the diagnostic tool only), Python 3 stdlib (ledger tool and enable scripts), Git Bash.

**Spec:** [`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`](../specs/2026-10-11-native-v5-port-design.md), sections 2, 3 and 6. Ledger: [`docs/port/parity-ledger.json`](../../port/parity-ledger.json) and [`docs/port/divergences.md`](../../port/divergences.md). Previous milestone: [M2a plan](2026-10-14-v5-m2a-goldens-tools-eraser-history.md).

**Controller rulings (binding, 2026-10-14).** The controller ruled before execution, and logs each as a dashboard decision. The plan below applies them; nothing waits on a ruling.

- **R1 (Este): HarfBuzzSharp in the engine is approved.** Task 3 adds `HarfBuzzSharp` `14.2.1.301`. That is the version `SkiaSharp.HarfBuzz` `4.153.1` pins, and the only HarfBuzzSharp in the local NuGet cache. Blink shapes every canvas string with HarfBuzz, and the drafters measured a GPOS kern of -6 units on Dancing Script's Z-Z at wght 700. Without shaping, `RIZZ` measures 158.656 px against 4.1's 158.272, `wordart-solid-dancing` rises from max-delta 56 to 92, and `wordart-shadow-dancing` from 76 to 108.
- **R2 (Este): Word Art scenes use an edge-tolerant golden rule.** `GoldenRule.AssertWordArtMatches` passes a scene when the sizes match and every interior pixel (its golden 3x3 neighborhood flat, as `ImageDiff` defines interior) differs by at most 1 level per channel. Edge pixels are not bounded. The 7 one-level interior pixels on `wordart-outline-anton` are inside that bound. `ImageDiff` and `GoldenRule.AssertMatches` do not change, and every other scene keeps the unchanged rule. The permanent guard is `WordArtSwapTests`: 4.1's own Word Art bitmaps, captured once by `tools/parity-ref/wordart-ref.js` into `tests/goldens/app/wordart-ref/` and frozen, are swapped into the engine's document, and each export must equal its golden exactly under the unchanged rule. The divergence is the new ledger row `wordart-glyph-rasterization`, added in Task 3 Step 23 and closed in Task 4.
- **R3 (controller): missing glyphs fall back to a system face, as Chromium does.** 4.1's font string ends in `, sans-serif` (`editor.js:2131`), so Chromium never draws a family's `.notdef` glyph. `WordArt.Shape` splits each line into runs by font coverage; a character the vendored face lacks takes `SKFontManager.Default.MatchCharacter(family, 900 upright, null, codepoint)`, and each run is shaped with HarfBuzz (Task 3 Steps 11 to 15). This matches 4.1, so no divergence row is added.
- **R4 (controller): `project-load-field-coercion` refuses an unknown mode, all or nothing.** Task 4 Step 7 sets the row's disposition to `fix` with the divergence "4.1: an unknown mode string is applied without validation (editor.js loadProject): switchMode loads the Shirt regions while currentMode holds the bad string. v5: an unknown mode refuses the whole file; nothing is applied. Duplicate layer ids (which 4.1 kept) are renumbered: the first holder keeps the id, later holders take fresh ids above every id in the file." The duplicate-id half is pinned by `Duplicate_ids_keep_file_order_and_later_holders_get_fresh_ids`. The 4.1 half was checked against `editor.js:2599` (`if (project.mode) switchMode(project.mode)`) and `:1695-1709` (any mode other than `'Pants'` takes the Shirt branch, and `currentMode` keeps the string). Task 1's tests pin the refusal, so Task 4 closes the row.
- **R5 (controller): the contract's weight wording.** `contract.md:68` says variable files are instanced "at the requested weight". The plan reads that as the matched `@font-face`'s declared weight, which is what Blink does and what was measured: Dancing Script requested at 900 draws at wght 700. `Variable_files_draw_at_the_declared_weight` pins it.

## Global Constraints

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

## Review Focus

Five input classes are the most likely to cause bugs. Each now has a pinning test in its owning task.

- **A project layer whose `imgSrc` is an `http:`, `https:`, `file:` or `blob:` URL, or a bare path** (a crafted or hand-edited `.r15proj`). Expected: nothing is fetched or read from disk. The layer is dropped and named in `ProjectLoad.DroppedLayers`. A non-data `bgImageSrc` refuses the whole file. Pinned in Task 1 (`A_layer_image_that_is_not_a_data_URL_is_dropped_and_named`, 5 cases; `A_background_that_is_not_a_data_URL_refuses_the_file`, 2 cases).
- **A bad field in the last layer, after valid layers that decode** (for example `"opacity": "half"`). Expected: `ProjectFormatException` names the field, and nothing is applied. The whole file is validated before any `Document` exists, unlike 4.1, which changed mode, background and fills before it parsed the layers. Pinned in Task 1 (`A_problem_in_the_last_layer_refuses_the_file_even_after_valid_layers`).
- **A detail in Pants mode** (4.1 throws a TypeError for all 16). Expected: the same pixels at the same position as in Shirt mode, drawn on the `waist_*`, `rleg_*` and `lleg_*` faces. Pinned in Task 2 (`Pants_draws_the_same_detail_on_the_equivalent_faces`, 16 rows) and in the loader (`A_pants_scene_runs_details_on_the_pants_faces`).
- **A detail width of 0, negative, NaN or infinity** (a script caller, or a corrupted future settings file). Expected: `ArgumentOutOfRangeException`, never a silent default to a 1 px canvas. Pinned in Task 2 (`Widths_that_are_not_finite_and_positive_are_refused`).
- **A Word Art size of 0, NaN, below 8 or above 300** (typed into the number input, which 4.1 never clamps). Expected: 0 and NaN fall back to 64, and then the size clamps to 8..300 (an approved divergence). Pinned in Task 3 (`Size_falls_back_to_64_and_clamps_to_8_through_300`).

Task 3 pins more edge cases: an empty trailing line, Outline at stroke width 0, Solid at stroke width 0, Pants placement, an unknown family, and a character the family lacks (ruling R3). They are listed in Task 3's own notes.

---

## File structure

```text
src/RBX15.Engine/
  History.cs                   + Clear() (Task 1)
  ProjectIO.cs                 ProjectFormatException, ProjectLoad, ProjectIO.Read/Write (Task 1)
  Details.cs                   DetailType, DetailStyle, Details.FromId/Id/LayerName/Render/Create (Task 2)
  Fonts.cs                     vendored faces: CSS weight match, wght instancing, synthetic bold, CreateFont (Task 3)
  WordArt.cs                   WordArtStyle, WordArtSettings, WordArt.Render/CreateLayer/LayerName; system-face fallback (Task 3)
  RBX15.Engine.csproj          + HarfBuzzSharp 14.2.1.301; assets/fonts copied to every output (Task 3)
  Renderer.cs, Images.cs       decode-cache wording: cc levels vs Skia mip levels (Task 4, comments only)
src/RBX15.Engine.Tests/
  HistoryTests.cs              + 1 fact (Task 1)
  ProjectIOTests.cs            read half, 66 cases (Task 1)
  ProjectIOTests.Write.cs      write half, 13 cases (Task 1)
  DetailsTests.cs              81 cases (Task 2)
  FontsTests.cs                32 cases (Task 3)
  WordArtTests.cs              46 cases (Task 3)
  GoldenAppTests.cs            project, detail, wordart skip rows removed (Tasks 1, 2, 3); wordart scenes routed to the Word Art rule (Task 3 Step 23)
  GoldenRule.cs                + the Word Art rule: WordArtResult, CompareWordArt, AssertWordArtBitmaps, AssertWordArtMatches (Task 3 Step 23)
  GoldenRuleWordArtTests.cs    the Word Art rule on synthetic bitmaps, 4 cases (Task 3)
  WordArtSwapTests.cs          4.1's own Word Art bitmap swapped in, exact under the unchanged rule, 23 cases (Task 3)
  RendererTests.cs             + 1 characterization fact (Task 4)
src/RBX15.Parity/
  SceneLoader.cs               project, detail, wordart join SupportedOps; ApplyProject, ApplyDetail, ApplyWordArt (Tasks 1, 2, 3)
src/RBX15.Parity.Tests/
  SceneLoaderProjectTests.cs   3 cases (Task 1)
  SceneLoaderDetailTests.cs    5 cases (Task 2)
  SceneLoaderTests.cs          the unsupported-op example uses sticker/warp (Task 2)
  SceneLoaderWordArtTests.cs   4 cases (Task 3)
tests/goldens/app/wordart-ref/ 4.1's own Word Art bitmaps: 22 PNGs, metrics.json, index.json; captured once, then frozen (Task 3 Step 21)
tools/parity-ref/
  wordart-ref.js               4.1's own renderWordArt bitmap and metrics (Task 3 Step 21)
docs/port/                     wordart-glyph-rasterization added (Task 3 Step 23); project-load-field-coercion amended; rows closed (Task 4)
```

**Test counts.** The baseline on `main` (`db6b1cd`) is Engine.Tests 188 passed and 62 skipped, and Parity.Tests 23 passed. Each run step below states the cumulative count after its task:

| After | Engine.Tests | Parity.Tests |
| --- | --- | --- |
| Task 1 | 271 passed, 59 skipped | 26 |
| Task 2 | 389 passed, 22 skipped | 31 |
| Task 3 Step 14 (fallback, op not yet enabled) | 467 passed, 22 skipped | 31 |
| Task 3 Step 23 (Word Art rule, all 82 scenes run) | 516 passed, 0 skipped | 35 |
| Task 4 | 517 passed, 0 skipped | 35 |

The reconciler re-ran Tasks 1, 2 and 3 in order on a scratch copy of `main` with every file as written here, and got these counts through the unchanged-rule run (Task 3 Step 20, where the enabled run gives Engine.Tests 465 passed and 22 failed before the fallback tests, and Parity.Tests 35 passed). The amendment for rulings R2 and R3 re-ran Task 3 on that copy with Steps 11 to 23 applied: 516 and 35, three full-suite runs in a row, and the 22 Step 20 lines unchanged by the fallback.

---

### Task 1: History.Clear, ProjectIO read and write, and the project golden op

**Files:**

- Create: `src/RBX15.Engine/ProjectIO.cs`, `src/RBX15.Engine.Tests/ProjectIOTests.cs`, `src/RBX15.Engine.Tests/ProjectIOTests.Write.cs`, `src/RBX15.Parity.Tests/SceneLoaderProjectTests.cs`
- Modify: `src/RBX15.Engine/History.cs` (add `Clear`), `src/RBX15.Engine.Tests/HistoryTests.cs` (one fact), `src/RBX15.Parity/SceneLoader.cs` (the `project` op), `src/RBX15.Engine.Tests/GoldenAppTests.cs` (drop the `project` skip row)
- Read only: `tests/goldens/projects/*.r15proj`, `tests/goldens/app/scenes.json`, `packs/**` (the pixels behind each fixture layer)

**Interfaces:**

- Consumes:
  - `Document`, `Layer` (`src/RBX15.Engine/Document.cs`, M1), `Fill` and `Fill.Solid` (`src/RBX15.Engine/Fills.cs`, M1), `TemplateMode` (`src/RBX15.Engine/Template.cs`, M1), `Images.Decode(byte[])` (`src/RBX15.Engine/Images.cs`, M1; throws `InvalidDataException` on bytes it cannot decode).
  - `History`, `EditorSnapshot` (`src/RBX15.Engine/History.cs`, M2a Task 7).
  - `SceneLoader.Parse`, `SceneLoader.UnsupportedOps`, the private `SupportedOps` set (`src/RBX15.Parity/SceneLoader.cs`, M2a Task 5); `GoldenAppTests.PendingOps` and its skip rows (`src/RBX15.Engine.Tests/GoldenAppTests.cs`, M2a Task 5); `GoldenRule.AssertMatches` (M2a Task 5); `RepoRoot.Find` (M1); test helpers `Px.Solid`, `Px.Render` (`src/RBX15.Engine.Tests/Px.cs`, M1).
- Produces:
  - `public void History.Clear()`: empties both stacks.
  - `public sealed class ProjectFormatException : Exception` with `(string message)` and `(string message, Exception inner)`.
  - `public sealed record ProjectLoad(Document Document, int NextId, ImmutableList<string> DroppedLayers)`.
  - `public static ProjectLoad ProjectIO.Read(string json)`: all-or-nothing; throws `ProjectFormatException` for every structural problem.
  - `public static string ProjectIO.Write(Document doc, DateTimeOffset savedAt, string? title = null)`: deterministic given `savedAt`.
  - `public const string ProjectIO.AppName` (`"RBX15 Classic Shirt and Pants Maker"`), `public const string ProjectIO.Version` (`"5.0"`), `internal const string ProjectIO.BackgroundImageName` (`"Background image"`).
  - `public static Document SceneLoader.ApplyProject(JsonElement action, string repoRoot)` and `"project"` in `SupportedOps`. `GoldenAppTests.PendingOps` becomes `{ "detail", "wordart" }`.

**What 4.1 does (cited).** Every line below is `editor.js` at blob `2790c41e418cd5bfea82cf1ae4d71626a7b54e61`, the one `tests/goldens/projects/index.json` records.

- **saveProject** (`:2550-2591`) builds the object at `:2551-2582`: top level `version` (`:2552`, literal `'4.0'`), `app`, `savedAt` (`:2554`, `new Date().toISOString()`), `mode`, `bgColor`, `bgOpacity`, `bgImageSrc` (`bgImage ? bgImage.src : null`), `regionColors` (`{...regionColors}`), `showMask`, `showGrid`, `layers`. Each layer is `id, name, visible, opacity, x, y, scale, rotation, flipH, flipV, brightness, contrast, saturate, hueRotate, rShift, gShift, bShift, imgSrc` (`:2561-2580`). It is serialized with `JSON.stringify(project, null, 2)` (`:2583`). The file name comes from `#exportTitle` (`:2586-2587`); the title itself is never written.
- **loadProject** (`:2593-2680`), in order:
  - `JSON.parse` (`:2597`); any throw becomes `alert('Failed to load project. ' + err.message)` (`:2675-2677`).
  - `if (project.mode) switchMode(project.mode)` (`:2599`). The value is unvalidated, and an absent or empty mode keeps the editor's mode. switchMode clears `regionColors` (`:1711`), which is why fills are applied after it.
  - `bgColor = project.bgColor || '#192e44'` (`:2601`) and `bgOpacity = project.bgOpacity != null ? project.bgOpacity : 1` (`:2602`).
  - A truthy `bgImageSrc` loads asynchronously into `bgImage`, with no `onerror`; otherwise `bgImage = null` (`:2606-2612`).
  - `regionColors = project.regionColors || {}` (`:2615`), stored verbatim: legacy color strings and descriptors alike, including keys the mode does not have.
  - `showMask`/`showGrid` are assigned only when `!= null` (`:2621-2622`).
  - Layers are cleared and the selection is reset (`:2625-2626`). `project.layers || []` (`:2627`).
  - A record without `imgSrc` is skipped silently (`:2632`).
  - Each loaded layer is coerced field by field (`:2636-2652`): `name || 'Layer'`, `visible !== false`, `opacity != null ? : 1`, `x || 0`, `y || 0`, `scale || 1` (so a stored 0 becomes 1), `rotation || 0`, `!!flipH`, `!!flipV`, `brightness`/`contrast`/`saturate != null ? : 100`, `hueRotate != null ? : 0`, `rShift`/`gShift`/`bShift || 0`.
  - When the last image to finish is an `onload`: sort by file position via `findIndex` on id (`:2658-2662`), `nextId = Math.max(...ids) + 1` over the loaded layers (`:2663`), select the top layer (`:2664`). When the last to finish is an `onerror`, none of that runs (`:2669-2672`).
- **No history interaction:** loadProject neither pushes nor clears undo (no `saveUndo` or `history` call in `:2593-2680`).
- **paintRegions** skips a falsy fill: `if (!r || !fill) continue;` (`:282`). lib/fills.js `normalizeFill` reads a string as `{ type: 'solid', c1: string }` and returns an object as-is (`lib/fills.js:27-31`). A descriptor without `type` falls to the solid branch of `paintRegionFill`, which the M1 port reproduces in `Fills.PaintRegionFill`'s `default` arm.
- **How the goldens drove it** (`tools/parity-ref/app-harness.js:176-190`): a fresh page (`applyBase` of an empty scene, `:77-101`), then the file is fed through `#projectFileInput`'s change handler (`editor.js:2684-2687`). The harness waits until every image layer and the background have loaded, then `exportPNG` is captured. All three fixtures were saved by 4.1 itself (`app-harness.js:229-244`, `savedAt` pinned to `2026-10-11T00:00:00.000Z`), so every image field is a `data:image/png;base64` URL and `version` is `"4.0"`.

**Approved divergences implemented here** (`docs/port/divergences.md`):

- `file-project-format`, `project-format-r15proj`, `project-load-flow`, `io-alert-inventory`: all-or-nothing. `Read` parses and validates everything before a `Document` exists, so a structural problem leaves the caller's state untouched. The M3 caller swaps documents only on success.
- `project-load-untrusted-src`: image fields must be `data:` URLs, and nothing is fetched or read from disk. Mode is validated.
- `project-load-error-path-bugs`, `project-load-layers`, `project-load-no-imgsrc-hang`: synchronous, deterministic file order. `NextId` is always set, and dropped layers are named.
- `project-save-format`, `project-version-compat`: writes `version: "5.0"`, ignores `version` on read.
- `project-load-history-interplay`: `History.Clear()` exists for the caller to run on open. The M3 session wires it; this task ships and tests the method.

**Contract ambiguities, resolved here** (the controller should read these; each is pinned by a named test):

1. **Non-data image URLs.** A layer `imgSrc` that is not a `data:` URL **drops the layer and names it**. A `bgImageSrc` that is not a `data:` URL **refuses the whole file**. Why: spec §3 and the divergence text promise "a visible warning naming dropped layers", so a layer has a reporting channel. The background has no name to report, and ledger `project-load-untrusted-src` establishes that every image 4.1 saves is a data URL (`editor.js:539, 943, 979, 1007, 1282, 1864, 2290, 2507, 2536, 2703`), so a non-data background only appears in a crafted file. Pinned: `A_layer_image_that_is_not_a_data_URL_is_dropped_and_named`, `A_background_that_is_not_a_data_URL_refuses_the_file`.
2. **A data-URL background that does not decode** (4.1 could hold an SVG background, which v5 cannot decode, since spec §3 drops SVG) is **dropped and reported as `"Background image"`** in `DroppedLayers`, not refused. Refusing would make a real 4.1 file unopenable over one field. Pinned: `A_background_that_does_not_decode_is_dropped_and_reported`.
3. **A layer with no `imgSrc`** (null, absent or `""`) is dropped like 4.1 (`:2632`) **and named**, since the spec promises a warning for any layer that cannot load. Pinned: `A_layer_without_an_image_is_dropped_and_named`.
4. **Wrong types throw; 4.1's coercions apply within the right type.** Each field must be absent, `null`, or the JSON type saveProject writes (string, number, boolean, object, array). Inside that type the `||`, `!= null`, `!== false` and `!!` rules apply exactly. So `scale: 0` gives 1 and `"name": ""` gives `"Layer"`, while `"x": "10"` or `"visible": 0` throws. Integer fields (`id`, `rShift`, `gShift`, `bShift`) must be whole numbers, because `Layer` stores them as `int` and 4.1's range inputs only produce integers. Pinned: `Layer_fields_coerce_like_loadProject`, `A_structural_problem_throws_naming_it`.
5. **Mode.** `"Shirt"` and `"Pants"` are accepted; absent, `null` or `""` gives Shirt (a fresh v5 document; 4.1 kept the editor's current mode); anything else throws, **including lowercase `"pants"`**. Ledger `project-load-field-coercion` said "keep or normalize". The contract says unknown modes throw, and 4.1 never writes lowercase. Pinned in `A_structural_problem_throws_naming_it`.
6. **Duplicate layer ids** (possible in 4.1 files after the `:2669-2672` error path left `nextId` stale): file order is kept, the first holder keeps the id, and each later duplicate takes the next id above every id in the file. Pinned: `Duplicate_ids_keep_file_order_and_later_holders_get_fresh_ids`. `id` must be a positive whole number; a missing id throws.
7. **`NextId`** is `max(id) + 1` over the layers that loaded (`:2663`), or 1 when none did. Pinned: `NextId_counts_only_layers_that_loaded`.
8. **Selection.** 4.1 selects the top layer after a load (`:2664`). `ProjectLoad` has no selection field (the record is binding), so the doc comment tells the caller to select `Document.Layers[^1]`.
9. **`title`.** `Write(..., title)` adds an optional `title` field **after** `layers`, so 4.1's field order stays an exact prefix and 4.1 ignores the field (ledger `project-template-name-not-saved`). `Read` ignores `title`, because `ProjectLoad` has no slot for it; if M3 wants the title back, the controller must widen the record. Pinned: `A_title_is_written_last_so_4_1_field_order_stays_a_prefix`.
10. **Fill output form.** Fills are written as descriptors carrying only their non-null fields, in lib/fills.js order (`type, c1, c2, angle, scale`), so a 4.1 descriptor round-trips exactly. A legacy string comes back as `{ "type": "solid", "c1": ... }`, which 4.1 reads identically (`normalizeFill`). Falsy fills are dropped on read, so they are not re-written. Region keys the mode lacks are kept and re-written, as 4.1 did. Pinned: `A_legacy_string_fill_is_written_as_a_solid_descriptor`, `Falsy_fills_are_skipped_and_descriptors_keep_their_fields`.
11. **Data URL parsing.** The scheme is matched case-insensitively and the header must end in `;base64`. The media type is ignored and the bytes are sniffed by the codec, as an `<img>` does. A non-base64 data URL counts as undecodable (dropped and named); 4.1 never writes one. Pinned: `A_data_URL_decodes_by_its_bytes_not_its_media_type`, `A_layer_image_that_does_not_decode_is_dropped_and_named`.

**Measured while drafting** (a scratch copy of `main` at `db6b1cd`, .NET SDK 10.0.302, SkiaSharp 4.153.1): every step's code below compiled under `-warnaserror` and produced exactly the Expected lines below. **All three project goldens pass under the unchanged `GoldenRule` with no engine change.** The M1 renderer already reproduces 4.1's composite of these files. The reconciler re-applied Step 12's edits exactly as written below and got the same Step 12 counts.

- [ ] **Step 1: Branch**

M2b builds on `main` (M1 and M2a merged at `db6b1cd`). Run: `git checkout main && git pull --ff-only && git checkout -b feat/v5-m2b-projectio-details-wordart`. Skip this if you are executing in a worktree already on the M2b branch the controller made.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 188, Failed: 0, Skipped: 62`; Parity.Tests `Passed: 23, Failed: 0`.

- [ ] **Step 2: Write the failing History.Clear test**

In `src/RBX15.Engine.Tests/HistoryTests.cs`, add this fact after `Capacity_must_be_positive_and_snapshots_non_null` (the last fact in the class, before the class's closing brace):

```csharp
    [Fact] // spec §3, project-load-history-interplay: opening a project empties both stacks
    public void Clear_empties_both_stacks_and_history_keeps_working()
    {
        var h = new History();
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        h.Undo(Snap("C"));
        Assert.Equal((1, 1), (h.UndoCount, h.RedoCount));

        h.Clear();

        Assert.Equal((0, 0), (h.UndoCount, h.RedoCount));
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
        Assert.Null(h.Undo(Snap("opened")));
        Assert.Null(h.Redo(Snap("opened")));
        Assert.Equal((0, 0), (h.UndoCount, h.RedoCount));

        h.Record(Snap("opened"));
        Assert.Equal("opened", Label(h.Undo(Snap("edited"))));
    }
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS1061: 'History' does not contain a definition for 'Clear'` at `HistoryTests.cs`.

- [ ] **Step 3: Implement History.Clear**

In `src/RBX15.Engine/History.cs`, insert this member directly above the `/// <summary>` that opens the doc comment of `Undo` (the line after `Record`'s closing brace):

```csharp
    /// <summary>
    /// Empties both stacks. Opening a project calls it (spec §3, divergences
    /// <c>project-load-history-interplay</c> and <c>project-load-flow</c>): 4.1's loadProject neither
    /// pushed nor cleared history (editor.js:2593-2680), so undo after an open restored the previous
    /// session's document.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.HistoryTests"`
Expected: `Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 189, Failed: 0, Skipped: 62`; Parity.Tests `Passed: 23, Failed: 0`.

- [ ] **Step 4: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/History.cs src/RBX15.Engine.Tests/HistoryTests.cs
git commit -m "feat(engine): History.Clear for project open"
```

- [ ] **Step 5: Write the failing ProjectIO read tests**

Create `src/RBX15.Engine.Tests/ProjectIOTests.cs`. It is a `partial` class; Step 8 adds the write half in a second file.

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// ProjectIO is the port of saveProject (editor.js:2550-2591) and loadProject (editor.js:2593-2680)
/// with the spec §3 divergences. The three fixtures are the frozen files 4.1 saved
/// (tests/goldens/projects, captured by tools/parity-ref/app-render.js --save-projects).
/// </summary>
public partial class ProjectIOTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static string Fixture(string name) => File.ReadAllText(Path.Combine(Root, "tests", "goldens", "projects", name + ".r15proj"));

    static JsonObject FixtureNode(string name) => JsonNode.Parse(Fixture(name))!.AsObject();

    static SKImage Pack(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(Root, rel)));

    /// <summary>Un-premultiplied RGBA bytes, like canvas getImageData.</summary>
    static byte[] Pixels(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.Bytes;
    }

    static void SamePixels(SKImage expected, SKImage actual)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        Assert.True(Pixels(expected).AsSpan().SequenceEqual(Pixels(actual)), "pixels differ");
    }

    static string PngDataUrl(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    static readonly string RedDot = PngDataUrl(Px.Solid(2, 2, SKColors.Red));

    /// <summary>A minimal valid file with one layer per imgSrc given, ids 1..n, names N1..Nn.</summary>
    static JsonObject Minimal(params string?[] imgSrcs)
    {
        var layers = new JsonArray();
        for (int i = 0; i < imgSrcs.Length; i++)
            layers.Add(new JsonObject { ["id"] = i + 1, ["name"] = $"N{i + 1}", ["imgSrc"] = imgSrcs[i] });
        return new JsonObject { ["version"] = "4.0", ["mode"] = "Shirt", ["layers"] = layers };
    }

    static ProjectLoad Read(JsonNode node) => ProjectIO.Read(node.ToJsonString());

    // ---- the three frozen 4.1 files ----

    [Fact] // proj-shirt-rich: gradient, legacy-string, dots and camo fills; transforms, adjustments, a hidden layer
    public void Reads_the_rich_shirt_fixture_field_for_field()
    {
        var load = ProjectIO.Read(Fixture("proj-shirt-rich"));
        var d = load.Document;

        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#0f1f31", d.BgColor);
        Assert.Equal(0.85, d.BgOpacity);
        Assert.Null(d.BgImage);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
        Assert.Equal(new (string, Fill)[]
        {
            ("torso_front", new Fill("linear", "#17d4fa", "#f22f89", 45)),
            ("torso_back", new Fill("linear", "#17d4fa", "#f22f89", 45)),
            ("torso_left", Fill.Solid("#ff8800")),
            ("rarm_front", Fill.Solid("#333")),
            ("larm_front", new Fill("dots", "#050c18", "#17d4fa", null, 12)),
            ("torso_up", new Fill("camo", "#556b2f", "#c2b280", null, 16)),
        }, d.RegionFills);

        Assert.Equal(new[] { 1, 2, 3, 4 }, d.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, d.Layers.Select(l => l.Name));
        Assert.Equal((0.8, 217.0, 355.0), (d.Layers[0].Opacity, d.Layers[0].X, d.Layers[0].Y));
        var l2 = d.Layers[1];
        Assert.Equal((245.0, 110.0, 0.5, -8.0, true, false, 140.0), (l2.X, l2.Y, l2.Scale, l2.Rotation, l2.FlipH, l2.FlipV, l2.Saturate));
        var l3 = d.Layers[2];
        Assert.Equal((100.0, 380.0, 0.6, 200.0, 30, 0, 0), (l3.X, l3.Y, l3.Scale, l3.HueRotate, l3.RShift, l3.GShift, l3.BShift));
        Assert.False(d.Layers[3].Visible);
        Assert.Equal(5, load.NextId);
        Assert.Empty(load.DroppedLayers);

        // tools/parity-ref/project-scenes.json names the pack image behind each layer.
        SamePixels(Pack("packs/drips/drip_white_sleeve_v1.png"), d.Layers[0].Image);
        SamePixels(Pack("packs/word_art/word_626_anton.png"), d.Layers[1].Image);
        SamePixels(Pack("packs/drips/drip_red_wide_v1.png"), d.Layers[2].Image);
        SamePixels(Pack("packs/drips/drip_cyan_front_v1.png"), d.Layers[3].Image);
    }

    [Fact] // proj-pants-legacy: Pants mode, a background image, legacy color-string fills
    public void Reads_the_legacy_pants_fixture_with_its_background_image()
    {
        var load = ProjectIO.Read(Fixture("proj-pants-legacy"));
        var d = load.Document;

        Assert.Equal(TemplateMode.Pants, d.Mode);
        Assert.Equal("#192e44", d.BgColor);
        Assert.Equal(0.6, d.BgOpacity);
        SamePixels(Pack("packs/drips/drip_red_wide_v1.png"), d.BgImage!);
        Assert.Equal(new (string, Fill)[]
        {
            ("waist_front", Fill.Solid("#ff8800")),
            ("waist_back", Fill.Solid("#0088ff")),
            ("rleg_front", Fill.Solid("#333")),
            ("lleg_front", Fill.Solid("#f22f89")),
        }, d.RegionFills);
        var only = Assert.Single(d.Layers);
        Assert.Equal((1, "L1", 217.0, 355.0), (only.Id, only.Name, only.X, only.Y));
        SamePixels(Pack("packs/drips/drip_cyan_pants_leg_v1.png"), only.Image);
        Assert.Equal(2, load.NextId);
        Assert.Empty(load.DroppedLayers);
    }

    [Fact] // proj-shirt-drawn: drawn, detail and Word Art layers 4.1 rasterized, with fractional placement
    public void Reads_the_drawn_shirt_fixture_with_4_1_layer_names_and_fractions()
    {
        var load = ProjectIO.Read(Fixture("proj-shirt-drawn"));
        var d = load.Document;

        Assert.Equal(new (string, Fill)[] { ("torso_front", new Fill("solid", "#2d2d2d")) }, d.RegionFills);
        Assert.Equal(new[] { "L1", "Rect drawing", "Crew Neck", "✨ W [Anton]" }, d.Layers.Select(l => l.Name));
        Assert.Equal(new[] { 1, 2, 3, 4 }, d.Layers.Select(l => l.Id));
        Assert.Equal((240.0, 80.0), (d.Layers[1].X, d.Layers[1].Y));
        Assert.Equal((251.0, 62.0), (d.Layers[2].X, d.Layers[2].Y));
        Assert.Equal((251.9671641791045, 83.6, 0.8119402985074626), (d.Layers[3].X, d.Layers[3].Y, d.Layers[3].Scale));
        Assert.Equal(5, load.NextId);
        Assert.Empty(load.DroppedLayers);
    }

    // ---- 4.1 file compatibility ----

    [Theory] // 4.1 never reads version (ledger project-version-compat)
    [InlineData("\"4.0\"")]
    [InlineData("\"5.0\"")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData(null)]
    public void Version_is_never_read(string? version)
    {
        var node = Minimal(RedDot);
        if (version is not null) node["version"] = JsonNode.Parse(version);
        else node.Remove("version");
        Assert.Single(Read(node).Document.Layers);
    }

    [Fact] // editor.js:2599-2627 on an empty object: every default applies
    public void An_empty_object_is_a_fresh_document()
    {
        var load = ProjectIO.Read("{}");
        var d = load.Document;
        Assert.Equal((TemplateMode.Shirt, "#192e44", 1.0, true, false), (d.Mode, d.BgColor, d.BgOpacity, d.ShowMask, d.ShowGrid));
        Assert.Null(d.BgImage);
        Assert.Empty(d.RegionFills);
        Assert.Empty(d.Layers);
        Assert.Equal(1, load.NextId);
        Assert.Empty(load.DroppedLayers);
    }

    [Fact] // editor.js:2599, 2601, 2602, 2606, 2615, 2621-2622, 2627: falsy and null fields
    public void Top_level_falsy_and_null_fields_take_4_1s_defaults()
    {
        var load = ProjectIO.Read("""
            { "mode": "", "bgColor": "", "bgOpacity": 0, "bgImageSrc": "", "regionColors": null,
              "showMask": null, "showGrid": null, "layers": null }
            """);
        var d = load.Document;
        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#192e44", d.BgColor);       // `||`
        Assert.Equal(0, d.BgOpacity);             // `!= null` keeps 0
        Assert.Null(d.BgImage);                   // falsy src, no image
        Assert.Empty(d.RegionFills);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
        Assert.Empty(d.Layers);
    }

    [Fact] // editor.js:2636-2652, field for field
    public void Layer_fields_coerce_like_loadProject()
    {
        var node = Minimal(RedDot, RedDot);
        var a = node["layers"]![0]!.AsObject();
        a["name"] = ""; a["visible"] = null; a["opacity"] = 0; a["x"] = null; a["y"] = 0; a["scale"] = 0; a["rotation"] = null;
        a["flipH"] = null; a["flipV"] = false; a["brightness"] = 0; a["contrast"] = null; a["saturate"] = 0; a["hueRotate"] = null;
        a["rShift"] = null; a["gShift"] = 0; a["bShift"] = -12;
        var b = node["layers"]![1]!.AsObject();
        foreach (var key in new[] { "name", "visible", "opacity", "x", "y", "scale", "rotation", "flipH", "flipV", "brightness", "contrast", "saturate", "hueRotate", "rShift", "gShift", "bShift" })
            b.Remove(key);

        var layers = Read(node).Document.Layers;

        var first = layers[0];
        Assert.Equal("Layer", first.Name);                                                          // `|| 'Layer'`
        Assert.True(first.Visible);                                                                // `!== false`
        Assert.Equal(0, first.Opacity);                                                            // `!= null` keeps 0
        Assert.Equal((0.0, 0.0, 1.0, 0.0), (first.X, first.Y, first.Scale, first.Rotation));      // scale `|| 1`: 0 becomes 1
        Assert.False(first.FlipH || first.FlipV);
        Assert.Equal((0.0, 100.0, 0.0, 0.0), (first.Brightness, first.Contrast, first.Saturate, first.HueRotate));
        Assert.Equal((0, 0, -12), (first.RShift, first.GShift, first.BShift));

        var second = layers[1];
        Assert.Equal(new Layer { Id = 2, Image = second.Image }, second);                          // every default is makeLayer's
    }

    [Fact] // editor.js:2632 skips a record without imgSrc; v5 also names it
    public void A_layer_without_an_image_is_dropped_and_named()
    {
        var node = Minimal(RedDot, null, "", RedDot);
        node["layers"]![2]!.AsObject().Remove("imgSrc");

        var load = Read(node);

        Assert.Equal(new[] { 1, 4 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "N2", "N3" }, load.DroppedLayers);
        Assert.Equal(5, load.NextId);
    }

    [Fact] // paintRegions skips a falsy fill (editor.js:282); a descriptor without a type paints as a solid
    public void Falsy_fills_are_skipped_and_descriptors_keep_their_fields()
    {
        var load = ProjectIO.Read("""
            { "regionColors": { "torso_front": null, "torso_back": false, "torso_left": 0, "torso_right": "",
                                "rarm_front": "#abc", "larm_front": { "c1": "#123456" },
                                "torso_up": { "type": "stripes", "c1": "#000000", "c2": "#ffffff", "angle": 90, "scale": 8, "extra": [1] },
                                "waist_front": "#ff0000" } }
            """);
        Assert.Equal(new (string, Fill)[]
        {
            ("rarm_front", Fill.Solid("#abc")),
            ("larm_front", new Fill("solid", "#123456")),
            ("torso_up", new Fill("stripes", "#000000", "#ffffff", 90, 8)),
            ("waist_front", Fill.Solid("#ff0000")), // a Pants key in a Shirt file: kept, as 4.1 kept and re-saved it
        }, load.Document.RegionFills);
    }

    [Fact] // JSON.parse keeps a repeated key's first position and its last value
    public void A_repeated_region_key_keeps_its_first_position_and_last_value()
    {
        var load = ProjectIO.Read("""{ "regionColors": { "torso_front": "#111111", "torso_back": "#222222", "torso_front": "#333333" } }""");
        Assert.Equal(new (string, Fill)[] { ("torso_front", Fill.Solid("#333333")), ("torso_back", Fill.Solid("#222222")) }, load.Document.RegionFills);
    }

    [Fact] // ledger project-load-layers quirk 3: 4.1 kept duplicate ids and sorted them in an unspecified order
    public void Duplicate_ids_keep_file_order_and_later_holders_get_fresh_ids()
    {
        var node = Minimal(RedDot, RedDot, RedDot, RedDot);
        node["layers"]![0]!["id"] = 7;
        node["layers"]![1]!["id"] = 3;
        node["layers"]![2]!["id"] = 7;
        node["layers"]![3]!["id"] = 3;

        var load = Read(node);

        Assert.Equal(new[] { "N1", "N2", "N3", "N4" }, load.Document.Layers.Select(l => l.Name));
        Assert.Equal(new[] { 7, 3, 8, 9 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(10, load.NextId);
    }

    [Fact] // editor.js:2663 counts the layers that loaded
    public void NextId_counts_only_layers_that_loaded()
    {
        var node = Minimal(RedDot, "data:image/png;base64,AAAA");
        node["layers"]![1]!["id"] = 40;
        var load = Read(node);
        Assert.Equal(2, load.NextId);
        Assert.Equal(new[] { "N2" }, load.DroppedLayers);
    }

    // ---- image sources (spec §3: data: URLs only) ----

    [Theory] // project-load-untrusted-src: never fetched, never read from disk; the layer is dropped and named
    [InlineData("http://example.com/a.png")]
    [InlineData("https://example.com/a.png")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("C:\\art\\a.png")]
    [InlineData("blob:https://x/1")]
    public void A_layer_image_that_is_not_a_data_URL_is_dropped_and_named(string src)
    {
        var load = Read(Minimal(RedDot, src));
        Assert.Equal(new[] { 1 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "N2" }, load.DroppedLayers);
    }

    [Theory] // undecodable data URLs: bad base64, not base64, not an image, SVG (spec §3 drops SVG)
    [InlineData("data:image/png;base64,@@@@")]
    [InlineData("data:image/png,%89PNG")]
    [InlineData("data:image/png;base64,AAAAAAAA")]
    [InlineData("data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=")]
    [InlineData("data:")]
    public void A_layer_image_that_does_not_decode_is_dropped_and_named(string src)
    {
        var load = Read(Minimal(src, RedDot));
        Assert.Equal(new[] { 2 }, load.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "N1" }, load.DroppedLayers);
    }

    [Fact] // the media type is not trusted; the bytes are sniffed, as an img element does
    public void A_data_URL_decodes_by_its_bytes_not_its_media_type()
    {
        var src = RedDot.Replace("data:image/png;base64,", "DATA:application/octet-stream;BASE64,");
        var only = Assert.Single(Read(Minimal(src)).Document.Layers);
        Assert.Equal((2, 2), (only.Image.Width, only.Image.Height));
    }

    [Theory] // a non-data background refuses the whole file: nothing 4.1 saves carries one
    [InlineData("http://example.com/bg.png")]
    [InlineData("file:///C:/bg.png")]
    public void A_background_that_is_not_a_data_URL_refuses_the_file(string src)
    {
        var node = Minimal(RedDot);
        node["bgImageSrc"] = src;
        var ex = Assert.Throws<ProjectFormatException>(() => Read(node));
        Assert.Contains("bgImageSrc must be a data: URL", ex.Message);
    }

    [Fact] // a data-URL background that does not decode is dropped and reported
    public void A_background_that_does_not_decode_is_dropped_and_reported()
    {
        var node = Minimal(RedDot);
        node["bgImageSrc"] = "data:image/png;base64,AAAA";
        var load = Read(node);
        Assert.Null(load.Document.BgImage);
        Assert.Equal(new[] { "Background image" }, load.DroppedLayers);
        Assert.Single(load.Document.Layers);
    }

    // ---- structural problems: all-or-nothing ----

    [Theory]
    [InlineData("", "not JSON")]
    [InlineData("{ \"mode\": \"Shirt\", ", "not JSON")]
    [InlineData("{ 'mode': 'Shirt' }", "not JSON")]
    [InlineData("[]", "a JSON array, not a project object")]
    [InlineData("null", "a JSON null, not a project object")]
    [InlineData("\"Shirt\"", "a JSON string, not a project object")]
    [InlineData("{ \"mode\": \"pants\" }", "unknown mode \"pants\"")]
    [InlineData("{ \"mode\": \"Robot\" }", "unknown mode \"Robot\"")]
    [InlineData("{ \"mode\": 1 }", "mode must be a string")]
    [InlineData("{ \"bgColor\": 25 }", "bgColor must be a string")]
    [InlineData("{ \"bgOpacity\": \"0.5\" }", "bgOpacity must be a number")]
    [InlineData("{ \"bgOpacity\": 1e400 }", "bgOpacity is out of range")]
    [InlineData("{ \"bgImageSrc\": {} }", "bgImageSrc must be a string")]
    [InlineData("{ \"showMask\": 1 }", "showMask must be true or false")]
    [InlineData("{ \"showGrid\": \"no\" }", "showGrid must be true or false")]
    [InlineData("{ \"regionColors\": [] }", "regionColors must be an object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": 5 } }", "regionColors.torso_front must be a color string or a fill object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": true } }", "regionColors.torso_front must be a color string or a fill object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": [\"#fff\"] } }", "regionColors.torso_front must be a color string or a fill object")]
    [InlineData("{ \"regionColors\": { \"torso_front\": { \"c1\": 255 } } }", "regionColors.torso_front.c1 must be a string")]
    [InlineData("{ \"regionColors\": { \"torso_front\": { \"angle\": \"45\" } } }", "regionColors.torso_front.angle must be a number")]
    [InlineData("{ \"layers\": {} }", "layers must be an array")]
    [InlineData("{ \"layers\": [5] }", "layers[0] must be an object")]
    [InlineData("{ \"layers\": [null] }", "layers[0] must be an object")]
    [InlineData("{ \"layers\": [{ \"name\": \"A\" }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": \"1\" }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 1.5 }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 0 }] }", "layers[0].id must be a positive whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"x\": \"10\" }] }", "layers[0].x must be a number")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"visible\": 0 }] }", "layers[0].visible must be true or false")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"flipH\": \"true\" }] }", "layers[0].flipH must be true or false")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"name\": 7 }] }", "layers[0].name must be a string")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"rShift\": 1.5 }] }", "layers[0].rShift must be a whole number")]
    [InlineData("{ \"layers\": [{ \"id\": 1, \"imgSrc\": 12 }] }", "layers[0].imgSrc must be a string")]
    public void A_structural_problem_throws_naming_it(string json, string expected)
    {
        var ex = Assert.Throws<ProjectFormatException>(() => ProjectIO.Read(json));
        Assert.Contains(expected, ex.Message);
    }

    [Fact] // validate first: a bad field after good images still refuses the whole file
    public void A_problem_in_the_last_layer_refuses_the_file_even_after_valid_layers()
    {
        var node = Minimal(RedDot, RedDot);
        node["layers"]![1]!["opacity"] = "half";
        var ex = Assert.Throws<ProjectFormatException>(() => Read(node));
        Assert.Contains("layers[1].opacity must be a number", ex.Message);
    }

    [Fact]
    public void Read_refuses_null() => Assert.Throws<ArgumentNullException>(() => ProjectIO.Read(null!));
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0246: The type or namespace name 'ProjectLoad' could not be found` (and `CS0103` for `ProjectIO`, `CS0246` for `ProjectFormatException`) in `ProjectIOTests.cs`.

- [ ] **Step 6: Implement ProjectIO.Read**

Create `src/RBX15.Engine/ProjectIO.cs`:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>A project file that cannot be opened. Nothing was applied; the message names the problem.</summary>
public sealed class ProjectFormatException : Exception
{
    public ProjectFormatException(string message) : base(message) { }

    public ProjectFormatException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// The result of <see cref="ProjectIO.Read"/>: the document, the next free layer id
/// (editor.js:2663), and the names of the layers that could not be loaded, in file order.
/// 4.1 selects the top layer after a load (editor.js:2664); the caller does the same with
/// <c>Document.Layers[^1]</c> when there is one.
/// </summary>
public sealed record ProjectLoad(Document Document, int NextId, ImmutableList<string> DroppedLayers);

/// <summary>
/// <c>.r15proj</c> read and write: the port of saveProject (editor.js:2550-2591) and loadProject
/// (editor.js:2593-2680), with the spec §3 divergences (ledger <c>file-project-format</c>,
/// <c>project-format-r15proj</c>, <c>project-load-untrusted-src</c>, <c>project-load-error-path-bugs</c>,
/// <c>project-load-layers</c>, <c>project-save-format</c>, <c>project-version-compat</c>):
/// <list type="bullet">
/// <item>Read is all-or-nothing. The whole file is parsed and validated before a Document exists, so a
/// structural problem throws <see cref="ProjectFormatException"/> and the caller's state is untouched.
/// 4.1 mutated mode, background and fills before the layers were even parsed (editor.js:2599-2619).</item>
/// <item>Image fields must be <c>data:</c> URLs with a base64 payload. A layer whose <c>imgSrc</c> is missing,
/// is not a data URL, or does not decode is dropped and named in <see cref="ProjectLoad.DroppedLayers"/>.
/// A <c>bgImageSrc</c> that is not a data URL refuses the whole file (every image 4.1 saves is a data
/// URL, so only a crafted file carries one); a data-URL background that does not decode is dropped and
/// reported as <see cref="BackgroundImageName"/>.</item>
/// <item>Write emits 4.1's field set and order with <c>version: "5.0"</c>; 4.1 never reads <c>version</c>.</item>
/// </list>
/// </summary>
public static class ProjectIO
{
    public const string AppName = "RBX15 Classic Shirt and Pants Maker";
    public const string Version = "5.0";

    /// <summary>The <see cref="ProjectLoad.DroppedLayers"/> entry for a background image that did not decode.</summary>
    internal const string BackgroundImageName = "Background image";

    // ---------------------------------------------------------------- read

    /// <summary>Parses and validates a whole project file. Throws <see cref="ProjectFormatException"/> on any structural problem.</summary>
    public static ProjectLoad Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ProjectFormatException($"the file is not JSON: {e.Message}", e);
        }
        using (parsed)
        {
            var p = parsed.RootElement;
            if (p.ValueKind != JsonValueKind.Object)
                throw new ProjectFormatException($"the file is a JSON {Kind(p)}, not a project object");

            // editor.js:2599 `if (project.mode) switchMode(project.mode)`: an absent or empty mode keeps the
            // editor's mode; a fresh v5 document is Shirt. 4.1 passed any other value through unvalidated
            // (ledger project-load-untrusted-src); v5 accepts only the two modes 4.1 ever writes.
            var mode = OptString(p, "mode", "mode") switch
            {
                null or "" => TemplateMode.Shirt,
                "Shirt" => TemplateMode.Shirt,
                "Pants" => TemplateMode.Pants,
                var m => throw new ProjectFormatException($"unknown mode \"{m}\" (expected \"Shirt\" or \"Pants\")"),
            };

            var bgColor = OptString(p, "bgColor", "bgColor");                      // editor.js:2601 `|| '#192e44'`
            double bgOpacity = OptNumber(p, "bgOpacity", "bgOpacity") ?? 1;        // editor.js:2602 `!= null ? : 1`
            var bgSrc = OptString(p, "bgImageSrc", "bgImageSrc");                  // editor.js:2606 truthy
            if (!string.IsNullOrEmpty(bgSrc) && !IsDataUrl(bgSrc))
                throw new ProjectFormatException($"bgImageSrc must be a data: URL, not \"{Clip(bgSrc)}\"");

            var fills = ReadRegionColors(p);                                       // editor.js:2615
            bool showMask = OptBool(p, "showMask", "showMask") ?? true;            // editor.js:2621 `!= null`
            bool showGrid = OptBool(p, "showGrid", "showGrid") ?? false;           // editor.js:2622 `!= null`
            var records = ReadLayerRecords(p);                                     // editor.js:2625-2673

            // Every structural check has passed. Only images remain, and a bad image drops, never throws.
            var dropped = ImmutableList.CreateBuilder<string>();
            SKImage? bgImage = null;
            if (!string.IsNullOrEmpty(bgSrc))
            {
                bgImage = TryDecodeDataUrl(bgSrc);
                if (bgImage is null) dropped.Add(BackgroundImageName);
            }

            // Layer ids come from the file. 4.1 kept duplicates and then sorted by findIndex, so their order
            // was unspecified (ledger project-load-layers, quirk 3). v5 keeps file order, the first holder
            // keeps the id, and each later duplicate takes the next id above every id in the file.
            long fresh = records.Count == 0 ? 0 : records.Max(r => (long)r.Id);
            var used = new HashSet<int>();
            var layers = ImmutableList.CreateBuilder<Layer>();
            foreach (var r in records)
            {
                // editor.js:2632 skips a record without imgSrc; v5 names it, like any layer that cannot load.
                var image = string.IsNullOrEmpty(r.Src) || !IsDataUrl(r.Src) ? null : TryDecodeDataUrl(r.Src);
                if (image is null) { dropped.Add(r.Layer.Name); continue; }
                int id = r.Id;
                if (!used.Add(id))
                {
                    if (++fresh >= int.MaxValue) throw new ProjectFormatException("layer ids run past the largest id");
                    id = (int)fresh;
                    used.Add(id);
                }
                layers.Add(r.Layer with { Id = id, Image = image });
            }

            // editor.js:2663: nextId = max(layer ids) + 1 over the layers that loaded; a fresh editor's is 1.
            int nextId = layers.Count == 0 ? 1 : layers.Max(l => l.Id) + 1;

            var document = new Document
            {
                Mode = mode,
                BgColor = string.IsNullOrEmpty(bgColor) ? "#192e44" : bgColor,
                BgOpacity = bgOpacity,
                BgImage = bgImage,
                RegionFills = fills,
                Layers = layers.ToImmutable(),
                ShowMask = showMask,
                ShowGrid = showGrid,
            };
            return new ProjectLoad(document, nextId, dropped.ToImmutable());
        }
    }

    sealed record LayerRecord(int Id, Layer Layer, string? Src);

    /// <summary>A 1x1 transparent placeholder; every record's image is replaced or the record dropped.</summary>
    static readonly SKImage Placeholder = SKImage.Create(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul));

    static List<LayerRecord> ReadLayerRecords(JsonElement p)
    {
        var list = new List<LayerRecord>();
        if (!p.TryGetProperty("layers", out var ls) || ls.ValueKind == JsonValueKind.Null) return list; // editor.js:2627 `|| []`
        if (ls.ValueKind != JsonValueKind.Array) throw new ProjectFormatException($"layers must be an array, not a {Kind(ls)}");
        int i = 0;
        foreach (var d in ls.EnumerateArray())
        {
            string at = $"layers[{i++}]";
            if (d.ValueKind != JsonValueKind.Object) throw new ProjectFormatException($"{at} must be an object, not a {Kind(d)}");
            if (!d.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number || !idEl.TryGetInt32(out int id) || id < 1)
                throw new ProjectFormatException($"{at}.id must be a positive whole number");

            // The coercions of editor.js:2636-2652, field for field. `||` maps 0 and "" to the default;
            // `!= null` keeps 0; `!== false` is true unless the value is false; `!!` is false unless true.
            string? name = OptString(d, "name", at + ".name");
            double? scale = OptNumber(d, "scale", at + ".scale");
            var layer = new Layer
            {
                Id = id,
                Image = Placeholder,
                Name = string.IsNullOrEmpty(name) ? "Layer" : name,                       // :2637 `|| 'Layer'`
                Visible = OptBool(d, "visible", at + ".visible") != false,                // :2638 `!== false`
                Opacity = OptNumber(d, "opacity", at + ".opacity") ?? 1,                  // :2639 `!= null`
                X = OptNumber(d, "x", at + ".x") ?? 0,                                    // :2640 `|| 0`
                Y = OptNumber(d, "y", at + ".y") ?? 0,                                    // :2641 `|| 0`
                Scale = scale is null or 0 ? 1 : scale.Value,                             // :2642 `|| 1`: 0 becomes 1
                Rotation = OptNumber(d, "rotation", at + ".rotation") ?? 0,               // :2643 `|| 0`
                FlipH = OptBool(d, "flipH", at + ".flipH") == true,                       // :2644 `!!`
                FlipV = OptBool(d, "flipV", at + ".flipV") == true,                       // :2645 `!!`
                Brightness = OptNumber(d, "brightness", at + ".brightness") ?? 100,       // :2646 `!= null`
                Contrast = OptNumber(d, "contrast", at + ".contrast") ?? 100,             // :2647 `!= null`
                Saturate = OptNumber(d, "saturate", at + ".saturate") ?? 100,             // :2648 `!= null`
                HueRotate = OptNumber(d, "hueRotate", at + ".hueRotate") ?? 0,            // :2649 `!= null`
                RShift = OptInt(d, "rShift", at + ".rShift") ?? 0,                        // :2650 `|| 0`
                GShift = OptInt(d, "gShift", at + ".gShift") ?? 0,                        // :2651 `|| 0`
                BShift = OptInt(d, "bShift", at + ".bShift") ?? 0,                        // :2652 `|| 0`
            };
            list.Add(new LayerRecord(id, layer, OptString(d, "imgSrc", at + ".imgSrc")));
        }
        return list;
    }

    /// <summary>
    /// regionColors (editor.js:2615 `project.regionColors || {}`): a legacy color string reads as a
    /// solid (lib/fills.js normalizeFill); a descriptor keeps its fields. A falsy value (null, false,
    /// 0, "") is skipped, as paintRegions skips it (editor.js:282). Keys stay verbatim, including keys
    /// the mode does not have: 4.1 kept and re-saved them, and the renderer skips them. A key that
    /// appears twice keeps its first position and its last value, as JSON.parse does.
    /// </summary>
    static ImmutableList<(string Region, Fill Fill)> ReadRegionColors(JsonElement p)
    {
        var result = new List<(string Region, Fill Fill)>();
        if (!p.TryGetProperty("regionColors", out var rc) || rc.ValueKind == JsonValueKind.Null) return [.. result];
        if (rc.ValueKind != JsonValueKind.Object) throw new ProjectFormatException($"regionColors must be an object, not a {Kind(rc)}");
        foreach (var e in rc.EnumerateObject())
        {
            string at = $"regionColors.{e.Name}";
            var v = e.Value;
            Fill? fill = v.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.False => null,
                JsonValueKind.Number when v.GetDouble() == 0 => null,
                JsonValueKind.String => v.GetString() is { Length: > 0 } s ? Fill.Solid(s) : null,
                JsonValueKind.Object => new Fill(
                    OptString(v, "type", at + ".type") ?? "solid",
                    OptString(v, "c1", at + ".c1"), OptString(v, "c2", at + ".c2"),
                    OptNumber(v, "angle", at + ".angle"), OptNumber(v, "scale", at + ".scale")),
                _ => throw new ProjectFormatException($"{at} must be a color string or a fill object, not a {Kind(v)}"),
            };
            int existing = result.FindIndex(r => r.Region == e.Name);
            if (existing >= 0) result.RemoveAt(existing);
            if (fill is null) continue;
            if (existing >= 0) result.Insert(Math.Min(existing, result.Count), (e.Name, fill));
            else result.Add((e.Name, fill));
        }
        return [.. result];
    }

    static string? OptString(JsonElement o, string key, string what) =>
        !o.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null ? null
        : v.ValueKind == JsonValueKind.String ? v.GetString()
        : throw new ProjectFormatException($"{what} must be a string, not a {Kind(v)}");

    static double? OptNumber(JsonElement o, string key, string what)
    {
        if (!o.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.Number) throw new ProjectFormatException($"{what} must be a number, not a {Kind(v)}");
        if (!v.TryGetDouble(out double d) || !double.IsFinite(d)) throw new ProjectFormatException($"{what} is out of range");
        return d;
    }

    static int? OptInt(JsonElement o, string key, string what)
    {
        if (OptNumber(o, key, what) is not { } d) return null;
        if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw new ProjectFormatException($"{what} must be a whole number");
        return (int)d;
    }

    static bool? OptBool(JsonElement o, string key, string what) =>
        !o.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null ? null
        : v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean()
        : throw new ProjectFormatException($"{what} must be true or false, not a {Kind(v)}");

    static string Kind(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => "null",
    };

    static string Clip(string s) => s.Length <= 40 ? s : s[..40] + "...";

    static bool IsDataUrl(string s) => s.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decodes <c>data:[&lt;type&gt;][;params];base64,&lt;payload&gt;</c>. The media type is not trusted:
    /// the bytes are sniffed by the codec, as an img element does. Null when the URL is not base64,
    /// the payload is not valid base64, or the bytes are not an image the engine decodes (an SVG data
    /// URL, which 4.1 could hold, is one; spec §3 drops SVG).
    /// </summary>
    static SKImage? TryDecodeDataUrl(string url)
    {
        int comma = url.IndexOf(',');
        if (comma < 0) return null;
        var header = url.AsSpan(5, comma - 5);
        if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(url[(comma + 1)..]); }
        catch (FormatException) { return null; }
        try { return Images.Decode(bytes); }
        catch (InvalidDataException) { return null; }
    }
}
```

Notes for the implementer:

- `Placeholder` exists because `Layer.Image` is `required`. Every record either gets its decoded image through `with { Image = image }` or is dropped, so no `Layer` in a returned `Document` ever holds it.
- `JsonElement.TryGetProperty` returns the **last** value of a repeated key, as `JSON.parse` does. `ReadRegionColors` reproduces JSON.parse's object order: first position, last value.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ProjectIOTests"`
Expected: `Passed!  - Failed:     0, Passed:    66, Skipped:     0, Total:    66` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 255, Failed: 0, Skipped: 62`; Parity.Tests `Passed: 23, Failed: 0`.

- [ ] **Step 7: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/ProjectIO.cs src/RBX15.Engine.Tests/ProjectIOTests.cs
git commit -m "feat(engine): ProjectIO.Read, an all-or-nothing .r15proj loader that reads every 4.1 file"
```

- [ ] **Step 8: Write the failing ProjectIO write tests**

Create `src/RBX15.Engine.Tests/ProjectIOTests.Write.cs`:

```csharp
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>ProjectIO.Write: saveProject's fields and order (editor.js:2551-2582), version "5.0", round trips.</summary>
public partial class ProjectIOTests
{
    static readonly DateTimeOffset Pinned = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);

    static void AssertSameDocument(Document expected, Document actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.BgColor, actual.BgColor);
        Assert.Equal(expected.BgOpacity, actual.BgOpacity);
        Assert.Equal(expected.BgImage is null, actual.BgImage is null);
        if (expected.BgImage is { } bg) SamePixels(bg, actual.BgImage!);
        Assert.Equal(expected.RegionFills, actual.RegionFills);
        Assert.Equal(expected.ShowMask, actual.ShowMask);
        Assert.Equal(expected.ShowGrid, actual.ShowGrid);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int i = 0; i < expected.Layers.Count; i++)
        {
            var e = expected.Layers[i];
            var a = actual.Layers[i];
            Assert.Equal(e with { Image = a.Image }, a);
            SamePixels(e.Image, a.Image);
        }
    }

    [Theory]
    [InlineData("proj-shirt-rich")]
    [InlineData("proj-pants-legacy")]
    [InlineData("proj-shirt-drawn")]
    public void Round_trip_reproduces_an_equal_document(string name)
    {
        var first = ProjectIO.Read(Fixture(name));
        var second = ProjectIO.Read(ProjectIO.Write(first.Document, Pinned));
        AssertSameDocument(first.Document, second.Document);
        Assert.Equal(first.NextId, second.NextId);
        Assert.Empty(second.DroppedLayers);
    }

    [Theory]
    [InlineData("proj-shirt-rich")]
    [InlineData("proj-pants-legacy")]
    [InlineData("proj-shirt-drawn")]
    public void Write_keeps_saveProjects_field_set_and_order(string name)
    {
        var fixture = FixtureNode(name);
        var written = JsonNode.Parse(ProjectIO.Write(ProjectIO.Read(Fixture(name)).Document, Pinned))!.AsObject();

        Assert.Equal(fixture.Select(p => p.Key), written.Select(p => p.Key));
        var fixtureLayers = fixture["layers"]!.AsArray();
        var writtenLayers = written["layers"]!.AsArray();
        Assert.Equal(fixtureLayers.Count, writtenLayers.Count);
        for (int i = 0; i < fixtureLayers.Count; i++)
            Assert.Equal(fixtureLayers[i]!.AsObject().Select(p => p.Key), writtenLayers[i]!.AsObject().Select(p => p.Key));
        Assert.Equal(fixture["regionColors"]!.AsObject().Select(p => p.Key), written["regionColors"]!.AsObject().Select(p => p.Key));

        Assert.Equal("5.0", (string?)written["version"]);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", (string?)written["app"]);
        Assert.Equal("2026-10-11T00:00:00.000Z", (string?)written["savedAt"]);
        Assert.StartsWith("data:image/png;base64,", (string?)writtenLayers[0]!["imgSrc"]);
    }

    [Fact] // editor.js:2583 JSON.stringify(project, null, 2)
    public void Write_is_deterministic_two_space_LF_JSON()
    {
        var doc = ProjectIO.Read(Fixture("proj-shirt-drawn")).Document;
        var a = ProjectIO.Write(doc, Pinned);
        var b = ProjectIO.Write(doc, Pinned);

        Assert.Equal(a, b);
        Assert.DoesNotContain('\r', a);
        Assert.StartsWith("{\n  \"version\": \"5.0\",\n  \"app\": \"RBX15 Classic Shirt and Pants Maker\",\n  \"savedAt\": \"2026-10-11T00:00:00.000Z\",\n  \"mode\": \"Shirt\",\n", a);
        Assert.Contains("\n  \"layers\": [\n    {\n      \"id\": 1,\n      \"name\": \"L1\",\n      \"visible\": true,\n      \"opacity\": 1,\n", a);
        Assert.Contains("\"name\": \"✨ W [Anton]\"", a);
        Assert.Contains("\"x\": 251.9671641791045,", a);
        Assert.EndsWith("\n  ]\n}", a);
    }

    [Fact] // toISOString is UTC with milliseconds
    public void SavedAt_is_written_as_UTC_with_milliseconds()
    {
        var json = ProjectIO.Write(new Document(), new DateTimeOffset(2026, 10, 14, 9, 5, 7, 42, TimeSpan.FromHours(-5)));
        Assert.Equal("2026-10-14T14:05:07.042Z", (string?)JsonNode.Parse(json)!["savedAt"]);
    }

    [Fact] // empty document: no image, no fills, no layers
    public void Writes_an_empty_document_with_null_background_and_empty_collections()
    {
        var node = JsonNode.Parse(ProjectIO.Write(new Document(), Pinned))!.AsObject();
        Assert.Null(node["bgImageSrc"]);
        Assert.True(node.ContainsKey("bgImageSrc"));
        Assert.Empty(node["regionColors"]!.AsObject());
        Assert.Empty(node["layers"]!.AsArray());
        Assert.False(node.ContainsKey("title"));
    }

    [Fact] // lib/fills.js normalizeFill reads both forms identically, so a legacy string may come back as a descriptor
    public void A_legacy_string_fill_is_written_as_a_solid_descriptor()
    {
        var json = ProjectIO.Write(ProjectIO.Read(Fixture("proj-pants-legacy")).Document, Pinned);
        var fill = JsonNode.Parse(json)!["regionColors"]!["rleg_front"]!.AsObject();
        Assert.Equal(new[] { "type", "c1" }, fill.Select(p => p.Key));
        Assert.Equal(("solid", "#333"), ((string?)fill["type"], (string?)fill["c1"]));
    }

    [Fact] // ledger project-template-name-not-saved: an optional field 4.1 ignores, after 4.1's fields
    public void A_title_is_written_last_so_4_1_field_order_stays_a_prefix()
    {
        var node = JsonNode.Parse(ProjectIO.Write(new Document(), Pinned, "My Shirt"))!.AsObject();
        Assert.Equal("title", node.Last().Key);
        Assert.Equal("My Shirt", (string?)node["title"]);
        Assert.Equal("layers", node.Reverse().Skip(1).First().Key);
    }

    [Fact]
    public void Write_refuses_null() => Assert.Throws<ArgumentNullException>(() => ProjectIO.Write(null!, Pinned));

    [Fact] // a hand-built document round-trips too: fractional fields, adjustments, a background image, a premultiplied drawing
    public void A_built_document_round_trips_including_a_semi_transparent_image()
    {
        var soft = Px.Render(8, 8, c =>
        {
            using var p = new SKPaint { Color = new SKColor(200, 40, 90, 37), IsAntialias = true };
            c.DrawCircle(4, 4, 3.3f, p);
        });
        var doc = new Document
        {
            Mode = TemplateMode.Pants,
            BgColor = "#0a0b0c",
            BgOpacity = 0.25,
            BgImage = Px.Solid(3, 2, new SKColor(1, 2, 3)),
            RegionFills = ImmutableList.Create(("waist_front", new Fill("radial", "#ffffff", "#000000")), ("rleg_back", Fill.Solid("#123"))),
            ShowMask = false,
            ShowGrid = true,
            Layers = ImmutableList.Create(
                new Layer { Id = 9, Image = soft, Name = "Soft \"quoted\" <b>", Opacity = 0.333, X = -12.5, Y = 1e-7, Scale = 2.75, Rotation = -179.5, FlipV = true, Brightness = 0, Contrast = 200, Saturate = 0, HueRotate = 359, RShift = -255, GShift = 255, BShift = 1 }),
        };

        var back = ProjectIO.Read(ProjectIO.Write(doc, Pinned, "t"));

        AssertSameDocument(doc, back.Document);
        Assert.Equal(10, back.NextId);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: FAIL with `error CS0117: 'ProjectIO' does not contain a definition for 'Write'` in `ProjectIOTests.Write.cs`.

- [ ] **Step 9: Implement ProjectIO.Write**

In `src/RBX15.Engine/ProjectIO.cs`, replace the three `using` lines at the top:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using SkiaSharp;
```

with:

```csharp
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SkiaSharp;
```

Then insert the following block inside `ProjectIO`, after `TryDecodeDataUrl`'s closing brace and before the class's closing brace (the file's last line):

```csharp
    // ---------------------------------------------------------------- write

    /// <summary>
    /// Serializes <paramref name="doc"/> with saveProject's fields in saveProject's order
    /// (editor.js:2551-2582), <c>version</c> "5.0", <c>savedAt</c> as JS toISOString of
    /// <paramref name="savedAt"/> in UTC, every image as a PNG data URL, JSON.stringify's 2-space
    /// indent and LF line breaks (editor.js:2583). Deterministic for a given savedAt.
    /// <para>
    /// A non-null <paramref name="title"/> adds a <c>title</c> field after <c>layers</c>, so 4.1's
    /// field order stays an exact prefix; 4.1 ignores unknown fields (ledger project-template-name-not-saved).
    /// Region fills are written as descriptors with only the fields they carry, in lib/fills.js order
    /// (type, c1, c2, angle, scale); a legacy color string read from a 4.1 file comes back as
    /// <c>{ "type": "solid", "c1": ... }</c>, which 4.1 reads identically (lib/fills.js normalizeFill).
    /// </para>
    /// </summary>
    public static string Write(Document doc, DateTimeOffset savedAt, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var buffer = new MemoryStream();
        var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using (var w = new Utf8JsonWriter(buffer, options))
        {
            w.WriteStartObject();
            w.WriteString("version", Version);
            w.WriteString("app", AppName);
            w.WriteString("savedAt", savedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            w.WriteString("mode", doc.Mode == TemplateMode.Pants ? "Pants" : "Shirt");
            w.WriteString("bgColor", doc.BgColor);
            Number(w, "bgOpacity", doc.BgOpacity);
            if (doc.BgImage is { } bg) w.WriteString("bgImageSrc", PngDataUrl(bg));
            else w.WriteNull("bgImageSrc");

            w.WriteStartObject("regionColors");
            foreach (var (region, fill) in MergeLastWins(doc.RegionFills))
            {
                w.WriteStartObject(region);
                w.WriteString("type", fill.Type);
                if (fill.C1 is { } c1) w.WriteString("c1", c1);
                if (fill.C2 is { } c2) w.WriteString("c2", c2);
                if (fill.Angle is { } angle) Number(w, "angle", angle);
                if (fill.Scale is { } scale) Number(w, "scale", scale);
                w.WriteEndObject();
            }
            w.WriteEndObject();

            w.WriteBoolean("showMask", doc.ShowMask);
            w.WriteBoolean("showGrid", doc.ShowGrid);

            w.WriteStartArray("layers");
            foreach (var l in doc.Layers)
            {
                w.WriteStartObject();
                w.WriteNumber("id", l.Id);
                w.WriteString("name", l.Name);
                w.WriteBoolean("visible", l.Visible);
                Number(w, "opacity", l.Opacity);
                Number(w, "x", l.X);
                Number(w, "y", l.Y);
                Number(w, "scale", l.Scale);
                Number(w, "rotation", l.Rotation);
                w.WriteBoolean("flipH", l.FlipH);
                w.WriteBoolean("flipV", l.FlipV);
                Number(w, "brightness", l.Brightness);
                Number(w, "contrast", l.Contrast);
                Number(w, "saturate", l.Saturate);
                Number(w, "hueRotate", l.HueRotate);
                w.WriteNumber("rShift", l.RShift);
                w.WriteNumber("gShift", l.GShift);
                w.WriteNumber("bShift", l.BShift);
                w.WriteString("imgSrc", PngDataUrl(l.Image));
                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (title is not null) w.WriteString("title", title);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>JSON.stringify writes -0 as 0 and has no NaN or Infinity (it writes null); neither does a project.</summary>
    static void Number(Utf8JsonWriter w, string key, double v)
    {
        if (!double.IsFinite(v)) { w.WriteNull(key); return; }
        w.WriteNumber(key, v == 0 ? 0 : v);
    }

    /// <summary>A JS object keeps a key's first position and its last value.</summary>
    static IEnumerable<(string Region, Fill Fill)> MergeLastWins(IEnumerable<(string Region, Fill Fill)> fills)
    {
        var order = new List<string>();
        var last = new Dictionary<string, Fill>(StringComparer.Ordinal);
        foreach (var (region, fill) in fills)
        {
            if (!last.ContainsKey(region)) order.Add(region);
            last[region] = fill;
        }
        return order.Select(r => (r, last[r]));
    }

    static string PngDataUrl(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("PNG encode failed");
        return "data:image/png;base64," + Convert.ToBase64String(data.AsSpan());
    }
```

Why these writer options:

- `NewLine = "\n"`: on .NET 9 and later, `Utf8JsonWriter`'s default `NewLine` is `Environment.NewLine`, which is CRLF on Windows. `JSON.stringify` writes LF.
- `UnsafeRelaxedJsonEscaping`: the default encoder escapes `+` (present in every base64 payload) and non-ASCII such as `✨` in Word Art layer names. That output is valid JSON, but it is not what 4.1 writes.
- **Doubles** are written shortest-round-trip, which matches JS for every value 4.1 writes in practice (for example `0.8119402985074626`, `1`, `-8`). Exponent forms differ in spelling (`Utf8JsonWriter` writes `1E-07` where `JSON.stringify` writes `1e-7`) and parse identically, so round trips and 4.1 compatibility hold. `-0` is written as `0`, like `JSON.stringify`. `NaN` and `Infinity`, which a read can never produce, are written as `null`, like `JSON.stringify`.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.ProjectIOTests"`
Expected: `Passed!  - Failed:     0, Passed:    79, Skipped:     0, Total:    79` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 268, Failed: 0, Skipped: 62`; Parity.Tests `Passed: 23, Failed: 0`.

- [ ] **Step 10: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/ProjectIO.cs src/RBX15.Engine.Tests/ProjectIOTests.Write.cs
git commit -m "feat(engine): ProjectIO.Write, saveProject's fields and order at version 5.0, deterministic"
```

- [ ] **Step 11: Write the failing SceneLoader project tests**

Create `src/RBX15.Parity.Tests/SceneLoaderProjectTests.cs`:

```csharp
using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderProjectTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void The_project_op_is_supported()
    {
        var scene = Json("""{ "name": "p", "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-shirt-drawn.r15proj" }] }""");
        Assert.Empty(SceneLoader.UnsupportedOps(scene));
    }

    [Fact] // loadProject replaces mode, background, fills and layers (editor.js:2599-2627)
    public void A_project_op_replaces_the_scene_document_with_the_file()
    {
        var scene = Json("""
            { "name": "p", "bgColor": "#ffffff", "regionColors": { "torso_back": "#000000" },
              "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-pants-legacy.r15proj" }] }
            """);

        var doc = SceneLoader.Parse(scene, Root).Document;

        Assert.Equal(TemplateMode.Pants, doc.Mode);
        Assert.Equal("#192e44", doc.BgColor);
        Assert.NotNull(doc.BgImage);
        Assert.Equal(new[] { "waist_front", "waist_back", "rleg_front", "lleg_front" }, doc.RegionFills.Select(f => f.Region));
        Assert.Equal(new[] { "L1" }, doc.Layers.Select(l => l.Name));
    }

    [Fact] // the harness froze only projects whose every layer loaded
    public void A_project_that_drops_a_layer_refuses_to_parse()
    {
        var file = Path.Combine(Path.GetTempPath(), $"rbx15-drop-{Guid.NewGuid():N}.r15proj");
        File.WriteAllText(file, """{ "layers": [{ "id": 1, "name": "Broken", "imgSrc": "data:image/png;base64,AAAA" }] }""");
        try
        {
            var scene = Json($$"""{ "name": "p", "actions": [{ "op": "project", "file": {{JsonSerializer.Serialize(file)}} }] }""");
            var ex = Assert.Throws<InvalidDataException>(() => SceneLoader.Parse(scene, Root));
            Assert.Contains("Broken", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Parity.Tests"`
Expected: `Failed!  - Failed:     3, Passed:    23, Skipped:     0, Total:    26` for `RBX15.Parity.Tests.dll`. The failures are `The_project_op_is_supported` (UnsupportedOps returns `["project"]`), and `A_project_op_replaces_the_scene_document_with_the_file` and `A_project_that_drops_a_layer_refuses_to_parse`, which both throw `NotSupportedException: scene p: ops the engine does not run yet: project`.

- [ ] **Step 12: Enable the project op (one change: SupportedOps plus handler, PendingOps, skip row)**

Per the contract's enabling rule, these three edits land together, and the accounting fact `Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps` stays green.

In `src/RBX15.Parity/SceneLoader.cs`:

1. Replace

   ```csharp
   static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape", "erase" };
   ```

   with

   ```csharp
   static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape", "erase", "project" };
   ```

2. In the `actions` switch, replace

   ```csharp
                       "erase" => ApplyErase(document, action, eraseStroking),
   ```

   with

   ```csharp
                       "erase" => ApplyErase(document, action, eraseStroking),
                       "project" => ApplyProject(action, repoRoot),
   ```

3. Insert this method directly above the `/// <summary>` that opens the doc comment of `public static Document ApplyErase(Document document, JsonElement action)`:

   ```csharp
       /// <summary>
       /// Scene v2 project op: <c>{ "op": "project", "file": "&lt;repo-relative .r15proj&gt;" }</c>. 4.1
       /// (app-harness.js project) feeds the file to #projectFileInput, whose change handler runs
       /// loadProject (editor.js:2684-2687, 2593-2680), and waits until every image layer and the
       /// background have loaded. The loaded project replaces the document, so the result is the file's
       /// document alone. A file that drops a layer is refused: the harness only froze projects whose
       /// every layer loaded, so a drop would compare a different document against the golden.
       /// </summary>
       public static Document ApplyProject(JsonElement action, string repoRoot)
       {
           var file = action.GetProperty("file").GetString() ?? throw new InvalidDataException("project: file is null");
           var load = ProjectIO.Read(File.ReadAllText(Path.Combine(repoRoot, file)));
           if (!load.DroppedLayers.IsEmpty)
               throw new InvalidDataException($"project {file}: layers that did not load: {string.Join(", ", load.DroppedLayers)}");
           return load.Document;
       }
   ```

In `src/RBX15.Engine.Tests/GoldenAppTests.cs`:

1. Delete the line

   ```csharp
       const string ProjectSkip = "op 'project' is not run by the engine yet (Engine ProjectIO, M2b)";
   ```

2. Replace

   ```csharp
       static readonly string[] PendingOps = { "detail", "wordart", "project" };
   ```

   with

   ```csharp
       static readonly string[] PendingOps = { "detail", "wordart" };
   ```

3. Delete the line

   ```csharp
       [MemberData(nameof(ScenesBlockedBy), "project", Skip = ProjectSkip)]
   ```

4. In the class doc comment, replace `/// (M2b for detail, wordart and project).` with `/// (M2b enabled detail, wordart and project one task at a time; PendingOps lists what is still skipped).` The wording stays true after Tasks 2 and 3, so neither touches it.

After these edits the two files read in full as follows; Tasks 2 and 3 edit from this state. They are shown with LF, and the working tree keeps CRLF.

`src/RBX15.Parity/SceneLoader.cs` after Task 1:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// <para>
/// Scene format v2 adds an ordered <c>actions</c> array, run after the scene's own layers exist.
/// The loader runs only the ops the engine implements (SupportedOps). A scene holding any other op
/// refuses to parse, so no test ever compares a half-built document against a golden.
/// </para>
/// <para>
/// The public API strokes the eraser once, as the editor does. The internal overloads take an
/// <see cref="EraseStroking"/> so the golden gate can replay 4.1's per-move stroking.
/// </para>
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    /// <summary>Ops the engine runs. Each engine task that implements an op adds it here.</summary>
    static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape", "erase", "project" };

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    /// <summary>The scene's action ops the engine does not run yet, each once, in first-use order.</summary>
    public static IReadOnlyList<string> UnsupportedOps(JsonElement scene)
    {
        var ops = new List<string>();
        if (scene.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
            {
                var op = action.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                if (!SupportedOps.Contains(op) && !ops.Contains(op)) ops.Add(op);
            }
        return ops;
    }

    public static Scene Parse(JsonElement e, string repoRoot) => Parse(e, repoRoot, EraseStroking.Once);

    internal static Scene Parse(JsonElement e, string repoRoot, EraseStroking eraseStroking)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

        var name = Str(e, "name") ?? throw new InvalidDataException("scene without a name");
        var unsupported = UnsupportedOps(e);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"scene {name}: ops the engine does not run yet: {string.Join(", ", unsupported)}");

        var fills = ImmutableList.CreateBuilder<(string, Fill)>();
        if (e.TryGetProperty("regionColors", out var rc))
            foreach (var p in rc.EnumerateObject())
                fills.Add((p.Name, p.Value.ValueKind == JsonValueKind.String
                    ? Fill.Solid(p.Value.GetString()!)
                    : new Fill(Str(p.Value, "type") ?? "solid", Str(p.Value, "c1"), Str(p.Value, "c2"), Num(p.Value, "angle"), Num(p.Value, "scale"))));

        var layers = ImmutableList.CreateBuilder<Layer>();
        if (e.TryGetProperty("layers", out var ls))
        {
            int i = 0;
            foreach (var l in ls.EnumerateArray())
            {
                i++;
                layers.Add(new Layer
                {
                    Id = i, Name = "L" + i, Image = Image(Str(l, "image")!),
                    Visible = Bool(l, "visible") ?? true, Opacity = Num(l, "opacity") ?? 1,
                    X = Num(l, "x") ?? 0, Y = Num(l, "y") ?? 0, Scale = Num(l, "scale") ?? 1, Rotation = Num(l, "rotation") ?? 0,
                    FlipH = Bool(l, "flipH") ?? false, FlipV = Bool(l, "flipV") ?? false,
                    Brightness = Num(l, "brightness") ?? 100, Contrast = Num(l, "contrast") ?? 100,
                    Saturate = Num(l, "saturate") ?? 100, HueRotate = Num(l, "hueRotate") ?? 0,
                    RShift = (int)(Num(l, "rShift") ?? 0), GShift = (int)(Num(l, "gShift") ?? 0), BShift = (int)(Num(l, "bShift") ?? 0),
                });
            }
        }

        var bgImage = Str(e, "bgImage");
        var document = new Document
        {
            Mode = Str(e, "mode") == "Pants" ? TemplateMode.Pants : TemplateMode.Shirt,
            BgColor = Str(e, "bgColor") ?? "#192e44",
            BgOpacity = Num(e, "bgOpacity") ?? 1,
            BgImage = bgImage is null ? null : Image(bgImage),
            RegionFills = fills.ToImmutable(),
            Layers = layers.ToImmutable(),
        };

        if (e.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
                document = action.GetProperty("op").GetString() switch
                {
                    "shape" => ApplyShape(document, action),
                    "erase" => ApplyErase(document, action, eraseStroking),
                    "project" => ApplyProject(action, repoRoot),
                    var op => throw new InvalidDataException($"scene {name}: op '{op}' has no handler"),
                };

        return new Scene(name, document);
    }

    /// <summary>
    /// A shape gesture from <c>from</c> to <c>to</c>, committed as 4.1 commits it: a new drawing layer
    /// on top, with the next id after every layer already there (editor.js:1285, :528-540).
    /// tool, from, to, color and width are required; fill defaults to false (drawFill, editor.js:92).
    /// </summary>
    static Document ApplyShape(Document doc, JsonElement a)
    {
        var tool = a.GetProperty("tool").GetString() switch
        {
            "line" => ShapeTool.Line,
            "rect" => ShapeTool.Rect,
            "ellipse" => ShapeTool.Ellipse,
            var t => throw new InvalidDataException($"unknown shape tool '{t}'"),
        };
        SKPoint Pt(string key)
        {
            var p = a.GetProperty(key);
            return new SKPoint(p[0].GetSingle(), p[1].GetSingle());
        }
        bool fill = a.TryGetProperty("fill", out var f) && f.GetBoolean();
        var stroke = new Stroke(tool, new[] { Pt("from"), Pt("to") },
            CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), fill);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Tools.CommitDrawing(stroke, id, Tools.DrawingName(tool));
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

    /// <summary>
    /// Scene v2 project op: <c>{ "op": "project", "file": "&lt;repo-relative .r15proj&gt;" }</c>. 4.1
    /// (app-harness.js project) feeds the file to #projectFileInput, whose change handler runs
    /// loadProject (editor.js:2684-2687, 2593-2680), and waits until every image layer and the
    /// background have loaded. The loaded project replaces the document, so the result is the file's
    /// document alone. A file that drops a layer is refused: the harness only froze projects whose
    /// every layer loaded, so a drop would compare a different document against the golden.
    /// </summary>
    public static Document ApplyProject(JsonElement action, string repoRoot)
    {
        var file = action.GetProperty("file").GetString() ?? throw new InvalidDataException("project: file is null");
        var load = ProjectIO.Read(File.ReadAllText(Path.Combine(repoRoot, file)));
        if (!load.DroppedLayers.IsEmpty)
            throw new InvalidDataException($"project {file}: layers that did not load: {string.Join(", ", load.DroppedLayers)}");
        return load.Document;
    }

    /// <summary>
    /// Scene v2 erase op: <c>{ "op": "erase", "layer": i, "points": [[x,y],...], "size": n }</c>, stroked
    /// once like the editor. 4.1 (app-render.js) selects layer i, picks the eraser, presses at
    /// points[0], moves through the rest and releases (editor.js:1120-1146, 1196-1203, 1248-1283).
    /// Drawings append above the scene's layers (editor.js:534 layers.push), so i indexes the
    /// document's layers directly.
    /// </summary>
    public static Document ApplyErase(Document document, JsonElement action) => ApplyErase(document, action, EraseStroking.Once);

    internal static Document ApplyErase(Document document, JsonElement action, EraseStroking stroking)
    {
        int index = action.GetProperty("layer").GetInt32();
        if (index < 0 || index >= document.Layers.Count)
            throw new InvalidDataException($"erase: the scene has no layer {index}");
        var points = action.GetProperty("points").EnumerateArray()
            .Select(p => new SKPoint((float)p[0].GetDouble(), (float)p[1].GetDouble()))
            .ToList();
        float size = (float)action.GetProperty("size").GetDouble();
        var erased = Eraser.Apply(document.Layers[index], points, size, stroking);
        return document with { Layers = document.Layers.SetItem(index, erased) };
    }
}
```

`src/RBX15.Engine.Tests/GoldenAppTests.cs` after Task 1:

```csharp
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2b enabled detail, wordart and project one task at a time; PendingOps lists what is still skipped).
/// <para>
/// Erase actions run through the internal EraseStroking.Replay41, which re-strokes the eraser path
/// after every mousemove exactly as app-render.js drove 4.1 (mousedown at points[0], one mousemove
/// per later point, mouseup with no stroke), so every erase scene meets the unchanged rule.
/// Production strokes once (approved divergence draw-pen-incremental-stroke); it is held to the
/// goldens directly on the two-point drags, where 4.1 strokes once too.
/// </para>
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    const string DetailSkip = "op 'detail' is not run by the engine yet (Engine Details, M2b)";
    const string WordArtSkip = "op 'wordart' is not run by the engine yet (Engine WordArt, M2b)";
    static readonly string[] PendingOps = { "detail", "wordart" };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    static JsonElement Scene(string name) => Raw.Value.Single(e => NameOf(e) == name);

    /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
    public static TheoryData<string> ScenesBlockedBy(string op)
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            var unsupported = SceneLoader.UnsupportedOps(e);
            if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
        }
        return data;
    }

    /// <summary>Runnable scenes with at least one erase, every erase a two-point drag (one stroke in 4.1).</summary>
    public static TheoryData<string> TwoPointEraseScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            if (SceneLoader.UnsupportedOps(e).Count != 0 || !e.TryGetProperty("actions", out var actions)) continue;
            var erases = actions.EnumerateArray().Where(a => a.GetProperty("op").GetString() == "erase").ToList();
            if (erases.Count > 0 && erases.All(a => a.GetProperty("points").GetArrayLength() == 2)) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    [MemberData(nameof(ScenesBlockedBy), "detail", Skip = DetailSkip)]
    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root, EraseStroking.Replay41);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Theory]
    [MemberData(nameof(TwoPointEraseScenes))]
    public void Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".once.png"));
    }

    [Fact]
    public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
    {
        var unclassified = Raw.Value
            .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
            .ToList();
        Assert.Empty(unclassified);
    }

    [Fact]
    public void Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps()
    {
        var rows = typeof(GoldenAppTests).GetMethod(nameof(Engine_export_matches_the_frozen_4_1_app_golden))!
            .GetCustomAttributes(typeof(MemberDataAttribute), false).Cast<MemberDataAttribute>()
            .Select(a => (Op: (string)a.Parameters[0], a.Skip))
            .ToList();

        var skipOps = rows.Where(r => r.Skip != null).Select(r => r.Op).ToList();
        Assert.Equal(PendingOps.OrderBy(o => o), skipOps.OrderBy(o => o));
        Assert.Single(rows, r => r.Skip == null && r.Op == "");

        var accounted = rows.Sum(r => ((System.Collections.IEnumerable)ScenesBlockedBy(r.Op)).Cast<object>().Count());
        Assert.Equal(Raw.Value.Count, accounted);
        Assert.Equal(82, Raw.Value.Count);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Parity.Tests"`
Expected: `Passed!  - Failed:     0, Passed:    26, Skipped:     0, Total:    26` for `RBX15.Parity.Tests.dll`.

Run the three project goldens by name, with a log:

```bash
mkdir -p .superpowers/logs
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" --logger "console;verbosity=normal" > .superpowers/logs/m2b-project-goldens.log 2>&1
grep -E "project-|Every_scene_is_run|Passed!|Failed!" .superpowers/logs/m2b-project-goldens.log
```

Expected (measured while drafting):

```text
  Passed RBX15.Engine.Tests.GoldenAppTests.Engine_export_matches_the_frozen_4_1_app_golden(name: "project-shirt-rich") [...]
  Passed RBX15.Engine.Tests.GoldenAppTests.Engine_export_matches_the_frozen_4_1_app_golden(name: "project-pants-legacy") [...]
  Passed RBX15.Engine.Tests.GoldenAppTests.Engine_export_matches_the_frozen_4_1_app_golden(name: "project-shirt-drawn") [...]
  Passed RBX15.Engine.Tests.GoldenAppTests.Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps [...]
```

**If a project golden fails (fix-the-engine rule).** Do not edit the golden, `GoldenRule`, `ImageDiff`, the fixture or the test. Use superpowers:systematic-debugging:

1. Open the heatmap at `diff-heatmaps/app/<scene>.png`.
2. Compare the loaded `Document` field by field with the fixture. `Reads_the_*_fixture_*` already pins every field.
3. A difference confined to layer pixels means the renderer: the same layer's scene in `tests/goldens/app` or `tests/goldens/render` will fail too, and the fix belongs in `Renderer`. A difference only in this scene means the loader: a coercion, the order or a field. Fix `ProjectIO`, add a unit test that pins the cause, and re-run.

If the cause is outside both, stop and report the measured `DiffResult` per scene to the controller.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 271, Failed: 0, Skipped: 59`; Parity.Tests `Passed: 26, Failed: 0`. The 3 project scenes moved from skipped to passed, and the 59 skipped are the 37 detail and 22 Word Art scenes.

- [ ] **Step 13: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Parity/SceneLoader.cs src/RBX15.Parity.Tests/SceneLoaderProjectTests.cs src/RBX15.Engine.Tests/GoldenAppTests.cs
git commit -m "feat(parity): run the project op; the 3 frozen 4.1 project goldens pass"
```

- [ ] **Step 14: Hand the ledger evidence to Task 4**

Don't run `ledger.py done` here. Task 4 closes rows in one batch and decides closure. Most rows below stay open there because the App owns half of their text (the visible dropped-layer warning, the Open command that calls `History.Clear`, the Save button). Only `layer-fields-schema`, `project-version-compat` and, under ruling R4, `project-load-field-coercion` close in M2b.

**Evidence this task supplies (Task 4 decides closure):**

| Row | Evidence |
| --- | --- |
| `project-save-format` | `Write_keeps_saveProjects_field_set_and_order` (3 fixtures), `Write_is_deterministic_two_space_LF_JSON` |
| `project-version-compat` | `Version_is_never_read` (5 cases), `Write_keeps_saveProjects_field_set_and_order` (`version` `"5.0"`) |
| `project-load-field-coercion` (fix, ruling R4) | `Layer_fields_coerce_like_loadProject`, `Top_level_falsy_and_null_fields_take_4_1s_defaults`; the unknown-mode refusal (`pants`, `Robot`, `1`) in `A_structural_problem_throws_naming_it`; nothing applied, in `A_problem_in_the_last_layer_refuses_the_file_even_after_valid_layers` |
| `project-load-untrusted-src` | `A_layer_image_that_is_not_a_data_URL_is_dropped_and_named` (5 cases), `A_background_that_is_not_a_data_URL_refuses_the_file` (2), unknown-mode rows of `A_structural_problem_throws_naming_it`. Its Process Monitor/Fiddler check is a manual M3 smoke item. |
| `project-load-error-path-bugs` | `A_layer_image_that_does_not_decode_is_dropped_and_named` (5 cases), `NextId_counts_only_layers_that_loaded` |
| `project-load-layers` | `Duplicate_ids_keep_file_order_and_later_holders_get_fresh_ids`, the three `Reads_the_*` fixture facts |
| `layer-fields-schema` | `Layer_fields_coerce_like_loadProject`, the three round-trip cases |
| `file-project-format` (engine half) | all of the above plus the 3 project goldens |
| `project-format-r15proj` (engine half) | all of the above plus the 3 project goldens |

**Ruled (R4).** `project-load-field-coercion`'s mode clause ("any non-`Pants` string loads Shirt") conflicts with the validated-mode fix this task implements: an unknown mode throws `ProjectFormatException`. Ruling R4 makes the row a `fix` row whose divergence is that refusal; Task 4 Step 7 amends the row and closes it on this task's tests.

These rows keep a half the M3 shell owns:

- `project-load-flow`, `project-load-history-interplay`, `project-load-no-imgsrc-hang`: calling `History.Clear()` on open, and the UI refresh.
- `io-alert-inventory`: the message box.
- `project-save-filename` (`App.Storage`).
- `project-template-name-not-saved`: the field is written, but reading it back needs a widened `ProjectLoad`, a controller decision.
- `docs-security-md`: decode size limits.

---

### Task 2: Engine Details and the detail golden op

**Files:**

- Create: `src/RBX15.Engine/Details.cs`, `src/RBX15.Engine.Tests/DetailsTests.cs`, `src/RBX15.Parity.Tests/SceneLoaderDetailTests.cs`
- Modify:
  - `src/RBX15.Parity/SceneLoader.cs` (`detail` joins `SupportedOps`, one handler arm, `ApplyDetail`)
  - `src/RBX15.Engine.Tests/GoldenAppTests.cs` (the `detail` skip row and its constant go, `detail` leaves `PendingOps`)
  - `src/RBX15.Parity.Tests/SceneLoaderTests.cs` (the unsupported-op example)
- Test: `DetailsTests.cs`, `SceneLoaderDetailTests.cs`, `SceneLoaderTests.cs`, `GoldenAppTests.cs`

**Interfaces:**

- Consumes:
  - `Template.Slot(TemplateMode, BodyPart, Face) -> Region` and `Region(Name, X, Y, W, H)` (`src/RBX15.Engine/Template.cs`)
  - `TemplateSize.Width/Height` (585, 559)
  - `Tools.Autocrop(SKImage canvas, int pad = 10) -> (SKImage Image, int X, int Y)?` (`src/RBX15.Engine/Tools.cs`, M2a; caller owns the image)
  - `Layer` (`src/RBX15.Engine/Document.cs`), `CssColor.ToSk(string)` (`src/RBX15.Engine/CssColor.cs`)
  - `SceneLoader.Parse`, `SceneLoader.UnsupportedOps`, the private `SupportedOps` and the `actions` switch (`src/RBX15.Parity/SceneLoader.cs`, M2a)
  - `GoldenAppTests.PendingOps`, `ScenesBlockedBy`, the accounting fact `Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps` (M2a), `GoldenRule.AssertMatches` (unchanged)
  - `ImageDiff.FromImage`, `ImageDiff.Compare` (`src/RBX15.Parity/ImageDiff.cs`), `Px.At` (test helper)
  - The 37 frozen detail scenes in `tests/goldens/app/scenes.json` (all Shirt mode)
- Produces:
  - `public enum DetailType { CollarV, CollarCrew, CollarScoop, CollarHenley, CuffLeft, CuffRight, CuffsBoth, Hem, SeamCenter, SeamSides, Pocket, Placket, ShoulderStripe, ArmBands, Yoke, FullDetail }`
  - `public sealed record DetailStyle(SKColor Color, float Width, bool Stitch)`
  - `public static DetailType Details.FromId(string id)` (throws `ArgumentException` on an unknown id)
  - `public static string Details.Id(DetailType type)`, `public static string Details.LayerName(DetailType type)`
  - `public static SKImage Details.Render(DetailType type, TemplateMode mode, DetailStyle style)` (585x559 kN32 premul; caller owns it)
  - `public static Layer? Details.Create(DetailType type, TemplateMode mode, DetailStyle style, int id)`
  - `public static Document SceneLoader.ApplyDetail(Document doc, JsonElement a)`

**What 4.1 does (port source):**

- **The canvas** (`createDetailLayer`, `editor.js:1834-1865`): `saveUndo()` first; a fresh `TW x TH` (585x559) canvas; `strokeStyle = fillStyle = #detailColor`, `lineWidth = +#detailWidth`, `lineCap = lineJoin = 'round'`, and with Stitch `setLineDash([width * 2, width * 2])` (`:1840-1849`). The generator draws; `autocrop(dc)` with the default pad 10 (`:1853`, `editor.js:144-170`); nothing drawn returns with the undo entry already pushed (`:1854`). Otherwise the crop goes through `toDataURL()` into an `Image` and `makeLayer(img, name, crop.x, crop.y, 1)` is pushed on top, selected, and the tool becomes Select (`:1856-1864`).
- **Names** (`editor.js:2082-2099`) and **ids** (`editor.html:189-219`): the table in `Details.Table`.
- **Generators** (`editor.js:1867-2075`). Each `beginPath ... stroke()` or `beginPath ... fill()` is one draw. Coordinates are JS doubles.

| Id | Lines | What it draws |
| --- | --- | --- |
| `collar-v` | 1868-1877 | one path `(cx-30, top) -> (cx, top+28) -> (cx+30, top)` on the body front |
| `collar-crew` | 1878-1894 | front path: moveTo `(cx-32, top)`, quad `(cx-32, top+16) (cx, top+18)`, quad `(cx+32, top+16) (cx+32, top)`; then a separate back path: moveTo `(bcx-28, b.y)`, quad `(bcx, b.y+10) (bcx+28, b.y)` |
| `collar-scoop` | 1895-1903 | moveTo `(cx-38, top)`, quad `(cx, top+32) (cx+38, top)` |
| `collar-henley` | 1904-1922 | crew; line `(cx, f.y+18)` down 30; three filled arcs at `(cx, f.y+18+6+10i)` of radius `c.lineWidth` |
| `cuff-left` / `cuff-right` | 1923-1943 | per limb face front, left, back, right: a line across at `reg.y + reg.h - 8` |
| `cuffs-both` | 1944-1947 | left cuffs, then right cuffs |
| `hem` | 1948-1965 | body right, front, left, then back: a line across at `r.y + r.h - 4` |
| `seam-center` | 1966-1973 | vertical at the front's center, full height |
| `seam-sides` | 1974-1995 | verticals at the front's left and right edges, then the back's |
| `pocket` | 1996-2008 | open-top box from `(f.x + 0.62 f.w, f.y + 0.25 f.h)`, 22 wide, 24 tall, one path |
| `placket` | 2009-2030 | verticals at `cx` and `cx+5` from `f.y+4` to `f.y+f.h-4`; seven filled arcs at `(cx+2.5, f.y+8+i*(f.h-16)/6)` of radius `max(1.5, 0.8 * c.lineWidth)` |
| `shoulder-stripe` | 2031-2048 | across the middle of the body up face, then the right and left limb up faces |
| `arm-bands` | 2049-2059 | right limb front, back, left, right, then the left limb's: a line across at `r.y + 0.35 r.h` |
| `yoke` | 2060-2068 | across the back at `b.y + 0.22 b.h` |
| `full-detail` | 2069-2074 | crew, hem, cuffs-both, seam-sides on one canvas |

- **How the goldens drove it** (`tools/parity-ref/app-harness.js`, `detail()`): refuse any mode but Shirt; `setInput('detailColor', op.color)`, `setInput('detailWidth', op.width)`, `setCheck('detailDash', !!op.stitch)`; click `.detail-btn[data-detail="<type>"]`; wait for one new layer. The 37 scenes are each a single detail action on an empty Shirt document: all 16 ids at `#333333` width 2, plain and stitched, plus five width cases (`collar-henley` w8, `placket` w1, `placket` w8 stitched, `seam-sides` w1 stitched, `full-detail` w8 stitched).

**Port decisions that the goldens hold exactly:**

- One `DrawPath` per 4.1 `stroke()` or `fill()`, in 4.1's order. Merging the hem's four lines into one path, for example, would change the antialiased overlap where the round caps meet.
- `arc(x, y, r, 0, 2pi)` is built as Blink's `Path::AddEllipse` builds a full turn: moveTo `(x+r, y)`, then two 180-degree `ArcTo` calls, the same construction as `Tools.EllipsePath`. No `DrawCircle`.
- Geometry in `double`, cast to `float` at each point, as Blink receives JS numbers. `310.36`, `399.8`, `102.16` and `18.666...` are never rounded.
- The dash is `SKPathEffect.CreateDash([2w, 2w], 0)` on the stroke paint only; the fill paint has none.
- The canvas is `SKImageInfo.PlatformColorType` premul (Chromium's kN32 software canvas), and the layer image is the crop read back un-premultiplied, as `Tools.CommitDrawing` stores drawings.

**Drafting evidence (measured 2026-10-14).** The code in this task was compiled and run in a scratch copy of `main` at `db6b1cd` (src, `global.json`, `tests/goldens`, `packs`, `assets/fonts`), with every file exactly as written below:

- `dotnet build src/RBX15.slnx -c Release -warnaserror`: `0 Warning(s)`, `0 Error(s)`.
- `DetailsTests`: 81 passed. `SceneLoaderDetailTests` + `SceneLoaderTests`: 15 passed (5 failed before the enable step, all with `NotSupportedException: ... ops the engine does not run yet: detail`).
- `GoldenAppTests`: 63 passed, 25 skipped (the 22 wordart and 3 project rows). **All 37 detail goldens pass on the first run, bit-exact: `RBX15.Parity diff` printed `max-delta=0 differing=0 interior=0 edge-over-tolerance=0 pass=True` for every one of the 37 scenes.**
- Whole suite from `main` plus this task alone: Engine.Tests `Passed: 306, Skipped: 25`; Parity.Tests `Passed: 28`. The run steps below state the cumulative counts after Task 1, which the reconciler measured on a scratch copy holding Tasks 1 and 2 exactly as written: Engine.Tests `Passed: 389, Skipped: 22`; Parity.Tests `Passed: 31`.
- The enable script (Step 8) was run against pristine copies of `main`'s `SceneLoader.cs` and `GoldenAppTests.cs` (CRLF working-tree files) and kept CRLF.

**Contract points this task settles:**

- **`Details.Create` takes no `Pants` exception.** The approved divergence `details-pants-mode-behavior` is met by resolving every region through `Template.Slot`; the Pants layers are pixel-identical to the Shirt layers because the two region tables share rectangles (`editor.js:20-60`). Unit tests only, as the contract says.
- **Extra public members beyond the contract**, all additive: `Details.Render(DetailType, TemplateMode, DetailStyle) -> SKImage` (the pre-crop canvas, for tests and the App's preview), `Details.Id(DetailType)` and `Details.LayerName(DetailType)` (the App's button table, ledger `details-buttons-inventory`), and `SceneLoader.ApplyDetail(Document, JsonElement)` (public, like `ApplyErase`).
- **Invalid widths throw.** `#detailWidth` is a 1..8 integer range (`editor.html:181`), so 4.1 never passes anything else. Canvas2D would silently ignore a non-positive or non-finite `lineWidth`, and the exact Blink behavior for a zero dash list was not verified. So `Render` and `Create` throw `ArgumentOutOfRangeException` for width `<= 0`, NaN or infinity instead of guessing. This is not a ledger divergence because no 4.1 input reaches it.
- **`Create` returns null only when nothing has alpha** (`editor.js:1853-1854`), which no opaque color can produce on this template. A fully transparent `SKColor` is the reachable case and is pinned. Undo is the App's: 4.1 pushes an undo entry before drawing even when no layer results, and the v5 rule (ledger `details-buttons`) is one real step, never an empty one. The App records history only on a non-null result.
- **`Tools.cs` is not touched.** `Create` repeats `Tools.CommitDrawing`'s six-line un-premultiply tail instead of refactoring `Tools`, so this slice cannot conflict with the WordArt slice's edits. A later cleanup can extract a shared `internal` helper.
- **Enabling the op.** Step 8's script edits `SupportedOps`, `PendingOps` and the skip rows by pattern, not by whole-file replacement, and fails loudly if its anchors are missing. The plan runs it after Task 1; Step 8 prints the full state it leaves.
- **The M2a loader test that used `detail` as its example unsupported op** (`SceneLoaderTests.Unsupported_ops_are_listed_once_in_order_and_refuse_to_parse`) breaks the moment `detail` is supported, and so would its `wordart` half under Task 3. Step 6 rewrites it once with two ops no milestone plans (`sticker`, `warp`). **Task 3 must not edit that test again.**

- [ ] **Step 1: Write the failing Details tests**

Create `src/RBX15.Engine.Tests/DetailsTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Engine Details (editor.js:1834-2103). Coverage values are either analytic (a pixel fully inside
/// or fully outside a stroke) or were measured on the engine build that matches all 37 frozen
/// detail goldens with max-delta 0; each is explained by the geometry beside it.
/// </summary>
public class DetailsTests
{
    static readonly SKColor Ink = new(0x33, 0x33, 0x33); // #detailColor default, editor.html:179

    static DetailStyle Style(float width = 2, bool stitch = false) => new(Ink, width, stitch);

    /// <summary>The whole alpha plane, row-major, read once (Px.At decodes per call).</summary>
    static byte[] Alpha(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Alpha8, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        var a = new byte[image.Width * image.Height];
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                a[y * image.Width + x] = bmp.GetPixel(x, y).Alpha;
        return a;
    }

    static byte[] Canvas(DetailType type, DetailStyle style, TemplateMode mode = TemplateMode.Shirt)
    {
        using var img = Details.Render(type, mode, style);
        return Alpha(img);
    }

    static int At(byte[] canvas, int x, int y) => canvas[y * TemplateSize.Width + x];

    public static TheoryData<DetailType> AllTypes()
    {
        var data = new TheoryData<DetailType>();
        foreach (var t in Enum.GetValues<DetailType>()) data.Add(t);
        return data;
    }

    // ── Ids and names ────────────────────────────────────────────

    [Theory]
    [InlineData("collar-v", DetailType.CollarV, "V-Neck Collar")]
    [InlineData("collar-crew", DetailType.CollarCrew, "Crew Neck")]
    [InlineData("collar-scoop", DetailType.CollarScoop, "Scoop Neck")]
    [InlineData("collar-henley", DetailType.CollarHenley, "Henley Collar")]
    [InlineData("cuff-left", DetailType.CuffLeft, "Left Cuff")]
    [InlineData("cuff-right", DetailType.CuffRight, "Right Cuff")]
    [InlineData("cuffs-both", DetailType.CuffsBoth, "Both Cuffs")]
    [InlineData("hem", DetailType.Hem, "Hem Line")]
    [InlineData("seam-center", DetailType.SeamCenter, "Center Seam")]
    [InlineData("seam-sides", DetailType.SeamSides, "Side Seams")]
    [InlineData("pocket", DetailType.Pocket, "Chest Pocket")]
    [InlineData("placket", DetailType.Placket, "Button Placket")]
    [InlineData("shoulder-stripe", DetailType.ShoulderStripe, "Shoulder Stripe")]
    [InlineData("arm-bands", DetailType.ArmBands, "Arm Bands")]
    [InlineData("yoke", DetailType.Yoke, "Back Yoke")]
    [InlineData("full-detail", DetailType.FullDetail, "Full Detail Set")]
    public void Ids_map_to_types_and_4_1_layer_names(string id, DetailType type, string name)
    {
        // ids: editor.html:187-222 data-detail; names: editor.js:2082-2099
        Assert.Equal(type, Details.FromId(id));
        Assert.Equal(id, Details.Id(type));
        Assert.Equal(name, Details.LayerName(type));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hem")]
    [InlineData("collar")]
    [InlineData("full-set")]
    public void Unknown_or_miscased_ids_throw(string id)
    {
        // 4.1 looks ids up in DETAIL_GENERATORS by exact key (editor.js:2080).
        Assert.Throws<ArgumentException>(() => Details.FromId(id));
    }

    // ── The canvas and the layer ─────────────────────────────────

    [Fact]
    public void Detail_canvas_is_template_sized_kN32_premul_and_transparent_elsewhere()
    {
        using var img = Details.Render(DetailType.Yoke, TemplateMode.Shirt, Style());
        Assert.Equal((TemplateSize.Width, TemplateSize.Height), (img.Width, img.Height));
        Assert.Equal(SKImageInfo.PlatformColorType, img.ColorType);
        Assert.Equal(0, Px.At(img, 10, 10).Alpha);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Widths_that_are_not_finite_and_positive_are_refused(float width)
    {
        // Review Focus: #detailWidth only yields 1..8 (editor.html:181); Canvas2D would silently
        // ignore such a lineWidth, so the engine refuses it rather than guess.
        Assert.Throws<ArgumentOutOfRangeException>(() => Details.Create(DetailType.Hem, TemplateMode.Shirt, Style(width), 1));
    }

    [Fact]
    public void A_fully_transparent_color_draws_nothing_and_makes_no_layer()
    {
        // autocrop returns null and createDetailLayer returns before makeLayer (editor.js:1853-1854).
        Assert.Null(Details.Create(DetailType.FullDetail, TemplateMode.Shirt, new DetailStyle(SKColors.Transparent, 2, false), 1));
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Layer_is_the_autocropped_canvas_at_scale_1_with_the_4_1_name(DetailType type)
    {
        using var canvas = Details.Render(type, TemplateMode.Shirt, Style(8, stitch: true));
        var crop = Tools.Autocrop(canvas)!.Value;
        using var cropped = crop.Image;
        var layer = Details.Create(type, TemplateMode.Shirt, Style(8, stitch: true), 7)!;

        Assert.Equal(7, layer.Id);
        Assert.Equal(Details.LayerName(type), layer.Name);
        Assert.Equal((crop.X, crop.Y, 1.0), (layer.X, layer.Y, layer.Scale));
        Assert.Equal((cropped.Width, cropped.Height), (layer.Image.Width, layer.Image.Height));
        Assert.Equal(Alpha(cropped), Alpha(layer.Image));
        // Every other field is a makeLayer default (editor.js:134-142).
        Assert.Equal(new Layer { Id = 7, Image = layer.Image, Name = layer.Name, X = layer.X, Y = layer.Y }, layer);
    }

    [Theory]
    // The layer box at width 2: geometry, plus the round cap or half width (1), plus pad 10, clamped
    // (editor.js:144-170). CollarV: x 265-1-10 = 254, y 74-1-10 = 63, right 325 -> 335, w 82.
    [InlineData(DetailType.CollarV, 254, 63, 82, 50)]
    [InlineData(DetailType.CollarCrew, 252, 63, 278, 40)]
    [InlineData(DetailType.CollarScoop, 246, 63, 98, 38)]
    [InlineData(DetailType.CollarHenley, 252, 63, 278, 70)]
    [InlineData(DetailType.CuffLeft, 297, 464, 284, 22)]
    [InlineData(DetailType.CuffRight, 8, 464, 284, 22)]
    [InlineData(DetailType.CuffsBoth, 8, 464, 573, 22)]
    [InlineData(DetailType.Hem, 154, 187, 412, 22)]
    [InlineData(DetailType.SeamCenter, 284, 63, 22, 150)]
    [InlineData(DetailType.SeamSides, 220, 63, 346, 150)]
    [InlineData(DetailType.Pocket, 299, 95, 45, 46)]
    [InlineData(DetailType.Placket, 284, 67, 27, 142)]
    [InlineData(DetailType.ShoulderStripe, 206, 29, 177, 303)]
    [InlineData(DetailType.ArmBands, 8, 388, 573, 23)]
    [InlineData(DetailType.Yoke, 416, 91, 150, 23)]
    [InlineData(DetailType.FullDetail, 8, 63, 573, 423)]
    public void Layer_box_at_width_2(DetailType type, int x, int y, int w, int h)
    {
        var layer = Details.Create(type, TemplateMode.Shirt, Style(), 1)!;
        Assert.Equal((x, y, w, h), ((int)layer.X, (int)layer.Y, layer.Image.Width, layer.Image.Height));
    }

    // ── Geometry ─────────────────────────────────────────────────

    [Fact]
    public void Hem_runs_4_px_above_the_bottom_of_the_right_front_left_and_back_faces()
    {
        // editor.js:1948-1965: y = 74 + 128 - 4 = 198; width 2 covers rows 197 and 198 exactly.
        var c = Canvas(DetailType.Hem, Style());
        foreach (int x in new[] { 197, 295, 393, 491 }) // right, front, left, back face centers
        {
            Assert.Equal((255, 255), (At(c, x, 197), At(c, x, 198)));
            Assert.Equal((0, 0), (At(c, x, 196), At(c, x, 199)));
        }
        Assert.Equal(0, At(c, 295, 265)); // torso_down gets no line (the unused d, editor.js:1950)
    }

    [Fact]
    public void Cuffs_sit_8_px_above_the_limb_bottom_on_the_named_arm_only()
    {
        // editor.js:1923-1943: y = 355 + 128 - 8 = 475 on larm faces (x 308..570) only.
        var c = Canvas(DetailType.CuffLeft, Style());
        Assert.Equal((255, 255), (At(c, 340, 474), At(c, 340, 475)));
        Assert.Equal(0, At(c, 249, 475)); // rarm_front
    }

    [Fact]
    public void Both_cuffs_are_the_left_and_right_cuffs_on_one_canvas()
    {
        // editor.js:1944-1947. The two arms never overlap, so the union is exact.
        var both = Canvas(DetailType.CuffsBoth, Style(2, stitch: true));
        var left = Canvas(DetailType.CuffLeft, Style(2, stitch: true));
        var right = Canvas(DetailType.CuffRight, Style(2, stitch: true));
        for (int i = 0; i < both.Length; i++)
        {
            Assert.False(left[i] > 0 && right[i] > 0);
            Assert.Equal(Math.Max(left[i], right[i]), both[i]);
        }
    }

    [Fact]
    public void Stitch_is_a_2w_on_2w_dash_with_round_caps()
    {
        // editor.js:1849: setLineDash([4, 4]) at width 2. The hem's right-face line starts at x 165:
        // dash 165..169 (caps to 164..170), gap to 173 (next cap from 172). The solid line covers it all.
        var stitch = Canvas(DetailType.Hem, Style(2, stitch: true));
        var solid = Canvas(DetailType.Hem, Style(2));
        Assert.Equal(255, At(stitch, 166, 198));
        Assert.Equal((0, 0), (At(stitch, 170, 198), At(stitch, 171, 198)));
        Assert.Equal(255, At(stitch, 173, 198));
        Assert.Equal((255, 255), (At(solid, 170, 198), At(solid, 171, 198)));
    }

    [Fact]
    public void Fractional_coordinates_stay_fractional()
    {
        // JS doubles handed to the canvas as floats, never rounded to pixels.
        // Pocket left side x = 231 + 128*0.62 = 310.36, width 2: 309.36..311.36.
        var pocket = Canvas(DetailType.Pocket, Style());
        Assert.Equal(255, At(pocket, 310, 120));
        Assert.InRange(At(pocket, 309, 120), 1, 254);
        Assert.InRange(At(pocket, 311, 120), 1, 254);
        Assert.True(At(pocket, 309, 120) > At(pocket, 311, 120)); // 0.64 vs 0.36 coverage

        // Arm bands y = 355 + 128*0.35 = 399.8, width 2: 398.8..400.8.
        var bands = Canvas(DetailType.ArmBands, Style());
        Assert.Equal(255, At(bands, 40, 399));
        Assert.True(At(bands, 40, 398) > 0 && At(bands, 40, 398) < At(bands, 40, 400) && At(bands, 40, 400) < 255);

        // Yoke y = 74 + 128*0.22 = 102.16, width 1: 101.66..102.66, so two partial rows.
        var yoke = Canvas(DetailType.Yoke, Style(1));
        Assert.InRange(At(yoke, 480, 101), 1, 254);
        Assert.InRange(At(yoke, 480, 102), 1, 254);
        Assert.Equal((0, 0), (At(yoke, 480, 100), At(yoke, 480, 103)));
    }

    [Fact]
    public void Henley_buttons_have_radius_lineWidth_and_are_never_dashed()
    {
        // editor.js:1916-1921: r = c.lineWidth. At width 8 the first button is centered (295, 98):
        // pixel 301 (center 6.5 away) is inside radius 8 and past the placket line (291..299).
        var w8 = Canvas(DetailType.CollarHenley, Style(8));
        Assert.Equal(255, At(w8, 301, 98));
        Assert.Equal(0, At(w8, 303, 98)); // center 8.5 away

        // fill() ignores the dash: with Stitch the button at (295, 98) sits in a placket gap
        // (dash 92..96, gap to 100) and is still solid.
        var stitched = Canvas(DetailType.CollarHenley, Style(2, stitch: true));
        Assert.Equal((255, 255), (At(stitched, 294, 98), At(stitched, 295, 98)));
    }

    [Fact]
    public void Placket_buttons_have_radius_max_1_5_and_0_8_width()
    {
        // editor.js:2023-2029: at width 1, r = 1.5; the first button is centered (297.5, 82), between
        // the lines at 295 and 300. Pixel (297, 82) has its far corners 1.118 away: inside 1.5,
        // outside 0.8.
        var w1 = Canvas(DetailType.Placket, Style(1));
        Assert.Equal(255, At(w1, 297, 82));
        var stitched = Canvas(DetailType.Placket, Style(2, stitch: true));
        Assert.Equal(255, At(stitched, 297, 82));
    }

    // ── Pants (approved divergence details-pants-mode-behavior) ──

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Pants_draws_the_same_detail_on_the_equivalent_faces(DetailType type)
    {
        // 4.1 throws a TypeError in Pants mode (editor.js:1869 and every generator). v5 resolves
        // waist/rleg/lleg through Template.Slot, whose rectangles equal the shirt's (editor.js:20-60).
        var shirt = Details.Create(type, TemplateMode.Shirt, Style(8, stitch: true), 1)!;
        var pants = Details.Create(type, TemplateMode.Pants, Style(8, stitch: true), 1)!;
        Assert.Equal((shirt.X, shirt.Y, shirt.Name), (pants.X, pants.Y, pants.Name));
        Assert.Equal(Alpha(shirt.Image), Alpha(pants.Image));
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build src/RBX15.slnx -c Release`
Expected: the build fails in `RBX15.Engine.Tests` with `error CS0246` naming `DetailType` and `DetailStyle` (there is no `Details.cs` yet).

- [ ] **Step 3: Implement Details**

Create `src/RBX15.Engine/Details.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>The 16 clothing details of 4.1's Details tab, in DETAIL_GENERATORS order (editor.js:1867-2075).</summary>
public enum DetailType
{
    CollarV, CollarCrew, CollarScoop, CollarHenley,
    CuffLeft, CuffRight, CuffsBoth, Hem,
    SeamCenter, SeamSides, Pocket, Placket,
    ShoulderStripe, ArmBands, Yoke, FullDetail,
}

/// <summary>
/// The Details tab settings read at click time (editor.js:1840-1842): color (#detailColor),
/// width (#detailWidth, an integer range 1..8, editor.html:181) and Stitch (#detailDash).
/// </summary>
public sealed record DetailStyle(SKColor Color, float Width, bool Stitch);

/// <summary>
/// The port of createDetailLayer and DETAIL_GENERATORS (editor.js:1834-2103). Each generator draws
/// on a fresh 585x559 canvas in template coordinates, the canvas is autocropped with pad 10, and
/// the crop becomes a scale-1 layer at the crop origin. Every <c>stroke()</c> and <c>fill()</c> call
/// in 4.1 is one draw here, in the same order and with the same path, so antialiased overlaps
/// composite as they do in Chromium. Coordinates are computed in doubles, as JS does, and handed
/// to Skia as floats, as Blink does.
/// <para>
/// Regions resolve through <see cref="Template.Slot"/>, so every detail also draws in Pants mode on
/// the equivalent waist and leg faces (approved divergence details-pants-mode-behavior: 4.1 throws).
/// </para>
/// </summary>
public static class Details
{
    static readonly (string Id, DetailType Type, string Name)[] Table =
    {
        // ids: editor.html:187-222 data-detail; names: editor.js:2082-2099
        ("collar-v", DetailType.CollarV, "V-Neck Collar"),
        ("collar-crew", DetailType.CollarCrew, "Crew Neck"),
        ("collar-scoop", DetailType.CollarScoop, "Scoop Neck"),
        ("collar-henley", DetailType.CollarHenley, "Henley Collar"),
        ("cuff-left", DetailType.CuffLeft, "Left Cuff"),
        ("cuff-right", DetailType.CuffRight, "Right Cuff"),
        ("cuffs-both", DetailType.CuffsBoth, "Both Cuffs"),
        ("hem", DetailType.Hem, "Hem Line"),
        ("seam-center", DetailType.SeamCenter, "Center Seam"),
        ("seam-sides", DetailType.SeamSides, "Side Seams"),
        ("pocket", DetailType.Pocket, "Chest Pocket"),
        ("placket", DetailType.Placket, "Button Placket"),
        ("shoulder-stripe", DetailType.ShoulderStripe, "Shoulder Stripe"),
        ("arm-bands", DetailType.ArmBands, "Arm Bands"),
        ("yoke", DetailType.Yoke, "Back Yoke"),
        ("full-detail", DetailType.FullDetail, "Full Detail Set"),
    };

    /// <summary>The 4.1 data-detail id (editor.html:187-222) to its type. Unknown ids throw.</summary>
    public static DetailType FromId(string id)
    {
        foreach (var row in Table)
            if (row.Id == id) return row.Type;
        throw new ArgumentException($"unknown detail id '{id}'", nameof(id));
    }

    /// <summary>The 4.1 data-detail id of a type.</summary>
    public static string Id(DetailType type) => Row(type).Id;

    /// <summary>The layer name 4.1 gives the detail (editor.js:2082-2099).</summary>
    public static string LayerName(DetailType type) => Row(type).Name;

    static (string Id, DetailType Type, string Name) Row(DetailType type)
    {
        foreach (var row in Table)
            if (row.Type == type) return row;
        throw new ArgumentOutOfRangeException(nameof(type));
    }

    /// <summary>
    /// The 585x559 canvas createDetailLayer draws (editor.js:1836-1851), before autocrop. Caller owns
    /// the image. Width must be finite and positive: 4.1's range input only yields 1..8, and Canvas2D
    /// would silently ignore anything else, so v5 refuses it instead of guessing.
    /// </summary>
    public static SKImage Render(DetailType type, TemplateMode mode, DetailStyle style)
    {
        if (!float.IsFinite(style.Width) || style.Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(style), style.Width, "detail width must be finite and positive");
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create detail surface");
        surface.Canvas.Clear(SKColors.Transparent);
        using (var pen = new Pen(surface.Canvas, mode, style))
            Draw(type, pen);
        return surface.Snapshot();
    }

    /// <summary>
    /// createDetailLayer (editor.js:1834-1865): render, autocrop (pad 10, editor.js:144-170), and a
    /// makeLayer(img, name, crop.x, crop.y, 1) layer (editor.js:1858) holding the crop un-premultiplied,
    /// the form 4.1's toDataURL + Image decode leaves it in. Null when nothing has alpha (no layer;
    /// the caller records no undo step, ledger details-buttons). Selection and the tool switch
    /// (editor.js:1860-1861) are the App's.
    /// </summary>
    public static Layer? Create(DetailType type, TemplateMode mode, DetailStyle style, int id)
    {
        using var drawn = Render(type, mode, style);
        if (Tools.Autocrop(drawn) is not { } crop) return null;
        using var premul = crop.Image;
        var info = new SKImageInfo(premul.Width, premul.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!premul.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read cropped detail");
        return new Layer { Id = id, Name = LayerName(type), Image = SKImage.FromPixelCopy(bmp.PeekPixels()), X = crop.X, Y = crop.Y, Scale = 1 };
    }

    static void Draw(DetailType type, Pen p)
    {
        switch (type)
        {
            case DetailType.CollarV: CollarV(p); break;
            case DetailType.CollarCrew: CollarCrew(p); break;
            case DetailType.CollarScoop: CollarScoop(p); break;
            case DetailType.CollarHenley: CollarHenley(p); break;
            case DetailType.CuffLeft: Cuffs(p, BodyPart.LeftLimb); break;
            case DetailType.CuffRight: Cuffs(p, BodyPart.RightLimb); break;
            case DetailType.CuffsBoth: CuffsBoth(p); break;
            case DetailType.Hem: Hem(p); break;
            case DetailType.SeamCenter: SeamCenter(p); break;
            case DetailType.SeamSides: SeamSides(p); break;
            case DetailType.Pocket: Pocket(p); break;
            case DetailType.Placket: Placket(p); break;
            case DetailType.ShoulderStripe: ShoulderStripe(p); break;
            case DetailType.ArmBands: ArmBands(p); break;
            case DetailType.Yoke: Yoke(p); break;
            case DetailType.FullDetail: FullDetail(p); break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    // ── Generators (editor.js:1867-2075) ─────────────────────────

    // editor.js:1868-1877
    static void CollarV(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y;
        p.Stroke(b => { b.MoveTo(P(cx - 30, top)); b.LineTo(P(cx, top + 28)); b.LineTo(P(cx + 30, top)); });
    }

    // editor.js:1878-1894: front U of two quads, then a separate back-collar stroke.
    static void CollarCrew(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y;
        p.Stroke(b =>
        {
            b.MoveTo(P(cx - 32, top));
            b.QuadTo(P(cx - 32, top + 16), P(cx, top + 18));
            b.QuadTo(P(cx + 32, top + 16), P(cx + 32, top));
        });
        var bk = p.Region(BodyPart.Body, Face.Back);
        double bcx = bk.X + bk.W / 2.0;
        p.Stroke(b => { b.MoveTo(P(bcx - 28, bk.Y)); b.QuadTo(P(bcx, bk.Y + 10), P(bcx + 28, bk.Y)); });
    }

    // editor.js:1895-1903
    static void CollarScoop(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y;
        p.Stroke(b => { b.MoveTo(P(cx - 38, top)); b.QuadTo(P(cx, top + 32), P(cx + 38, top)); });
    }

    // editor.js:1904-1922: crew, a 30 px placket line, three buttons of radius lineWidth, filled.
    static void CollarHenley(Pen p)
    {
        CollarCrew(p);
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0, top = f.Y + 18;
        p.Stroke(b => { b.MoveTo(P(cx, top)); b.LineTo(P(cx, top + 30)); });
        double r = p.LineWidth; // editor.js:1916, the full width
        for (int i = 0; i < 3; i++) p.FillCircle(cx, top + 6 + i * 10, r);
    }

    // editor.js:1923-1943: one line 8 px above the bottom of each of the limb's four side faces,
    // in 4.1's order front, left, back, right.
    static void Cuffs(Pen p, BodyPart limb)
    {
        foreach (var face in new[] { Face.Front, Face.Left, Face.Back, Face.Right })
        {
            var reg = p.Region(limb, face);
            HLine(p, reg, reg.Y + reg.H - 8);
        }
    }

    // editor.js:1944-1947
    static void CuffsBoth(Pen p)
    {
        Cuffs(p, BodyPart.LeftLimb);
        Cuffs(p, BodyPart.RightLimb);
    }

    // editor.js:1948-1965: 4 px above the bottom of the right, front and left faces, then the back.
    static void Hem(Pen p)
    {
        foreach (var face in new[] { Face.Right, Face.Front, Face.Left, Face.Back })
        {
            var r = p.Region(BodyPart.Body, face);
            HLine(p, r, r.Y + r.H - 4);
        }
    }

    // editor.js:1966-1973
    static void SeamCenter(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        VLine(p, f.X + f.W / 2.0, f.Y, f.Y + f.H);
    }

    // editor.js:1974-1995: both edges of the front, then both edges of the back.
    static void SeamSides(Pen p)
    {
        foreach (var face in new[] { Face.Front, Face.Back })
        {
            var r = p.Region(BodyPart.Body, face);
            VLine(p, r.X, r.Y, r.Y + r.H);
            VLine(p, r.X + r.W, r.Y, r.Y + r.H);
        }
    }

    // editor.js:1996-2008: an open-top 22x24 box at 62 % across, 25 % down the front.
    static void Pocket(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double px = f.X + f.W * 0.62, py = f.Y + f.H * 0.25;
        const double pw = 22, ph = 24;
        p.Stroke(b => { b.MoveTo(P(px, py)); b.LineTo(P(px, py + ph)); b.LineTo(P(px + pw, py + ph)); b.LineTo(P(px + pw, py)); });
    }

    // editor.js:2009-2030: two lines 5 px apart, then 7 buttons of radius max(1.5, 0.8 lineWidth).
    static void Placket(Pen p)
    {
        var f = p.Region(BodyPart.Body, Face.Front);
        double cx = f.X + f.W / 2.0;
        VLine(p, cx, f.Y + 4, f.Y + f.H - 4);
        VLine(p, cx + 5, f.Y + 4, f.Y + f.H - 4);
        double r = Math.Max(1.5, p.LineWidth * 0.8);
        double spacing = (f.H - 16) / 6.0;
        for (int i = 0; i < 7; i++) p.FillCircle(cx + 2.5, f.Y + 8 + i * spacing, r);
    }

    // editor.js:2031-2048: through the middle of the body's up face, then the right and left limb up faces.
    static void ShoulderStripe(Pen p)
    {
        var tu = p.Region(BodyPart.Body, Face.Up);
        HLine(p, tu, tu.Y + tu.H / 2.0);
        foreach (var limb in new[] { BodyPart.RightLimb, BodyPart.LeftLimb })
        {
            var r = p.Region(limb, Face.Up);
            HLine(p, r, r.Y + r.H / 2.0);
        }
    }

    // editor.js:2049-2059: 35 % down each limb side face, right limb front/back/left/right then left.
    static void ArmBands(Pen p)
    {
        foreach (var limb in new[] { BodyPart.RightLimb, BodyPart.LeftLimb })
            foreach (var face in new[] { Face.Front, Face.Back, Face.Left, Face.Right })
            {
                var r = p.Region(limb, face);
                HLine(p, r, r.Y + r.H * 0.35);
            }
    }

    // editor.js:2060-2068
    static void Yoke(Pen p)
    {
        var b = p.Region(BodyPart.Body, Face.Back);
        HLine(p, b, b.Y + b.H * 0.22);
    }

    // editor.js:2069-2074
    static void FullDetail(Pen p)
    {
        CollarCrew(p);
        Hem(p);
        CuffsBoth(p);
        SeamSides(p);
    }

    // ── Shared shapes ────────────────────────────────────────────

    /// <summary>beginPath; moveTo(r.x, y); lineTo(r.x + r.w, y); stroke(): a line across the face.</summary>
    static void HLine(Pen p, Region r, double y) =>
        p.Stroke(b => { b.MoveTo(P(r.X, y)); b.LineTo(P(r.X + r.W, y)); });

    /// <summary>beginPath; moveTo(x, y0); lineTo(x, y1); stroke().</summary>
    static void VLine(Pen p, double x, double y0, double y1) =>
        p.Stroke(b => { b.MoveTo(P(x, y0)); b.LineTo(P(x, y1)); });

    static SKPoint P(double x, double y) => new((float)x, (float)y);

    /// <summary>
    /// The canvas state createDetailLayer sets (editor.js:1844-1849) and the two draw calls the
    /// generators use. stroke(): lineWidth, round cap and join, and when Stitch is on the dash
    /// [2w, 2w] at lineDashOffset 0, which Blink hands Skia as an SKPathEffect dash; every stroke()
    /// is its own draw, so each starts the pattern afresh. fill(): the same color, never dashed.
    /// StrokeMiter stays at the canvas default 10; round joins never read it.
    /// </summary>
    sealed class Pen : IDisposable
    {
        readonly SKCanvas _canvas;
        readonly TemplateMode _mode;
        readonly SKPaint _stroke;
        readonly SKPaint _fill;
        readonly SKPathEffect? _dash;

        public Pen(SKCanvas canvas, TemplateMode mode, DetailStyle style)
        {
            _canvas = canvas;
            _mode = mode;
            LineWidth = style.Width;
            if (style.Stitch) _dash = SKPathEffect.CreateDash(new[] { style.Width * 2, style.Width * 2 }, 0);
            _stroke = new SKPaint
            {
                Color = style.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = style.Width,
                StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, StrokeMiter = 10, PathEffect = _dash,
            };
            _fill = new SKPaint { Color = style.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
        }

        /// <summary>c.lineWidth as the generators read it back (editor.js:1916, :2023).</summary>
        public double LineWidth { get; }

        public Region Region(BodyPart part, Face face) => Template.Slot(_mode, part, face);

        public void Stroke(Action<SKPathBuilder> build)
        {
            using var pb = new SKPathBuilder();
            build(pb);
            using var path = pb.Detach();
            _canvas.DrawPath(path, _stroke);
        }

        /// <summary>
        /// beginPath; arc(cx, cy, r, 0, 2pi); fill(). Blink's Path::AddEllipse splits the full turn into
        /// two 180-degree arcTo calls from angle 0, as Tools builds the ellipse.
        /// </summary>
        public void FillCircle(double cx, double cy, double r)
        {
            float x = (float)cx, y = (float)cy, rad = (float)r;
            var oval = new SKRect(x - rad, y - rad, x + rad, y + rad);
            using var pb = new SKPathBuilder();
            pb.MoveTo(x + rad, y);
            pb.ArcTo(oval, 0, 180, false);
            pb.ArcTo(oval, 180, 180, false);
            using var path = pb.Detach();
            _canvas.DrawPath(path, _fill);
        }

        public void Dispose()
        {
            _stroke.Dispose();
            _fill.Dispose();
            _dash?.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run the Details tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~DetailsTests"`
Expected: `Passed!  - Failed:     0, Passed:    81, Skipped:     0, Total:    81` for `RBX15.Engine.Tests.dll` (16 id rows, 4 bad ids, 1 canvas fact, 4 bad widths, 1 transparent fact, 16 layer rows, 16 box rows, 7 geometry facts, 16 Pants rows).

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `0 Warning(s)`, `0 Error(s)`.

If a `Layer_box_at_width_2` row is off by one, an extra or missing antialiased pixel moved the alpha bounds: check that the point is built from `double` math cast to `float` (not `float` math) and that each stroke is its own `DrawPath`. If `Pants_draws_the_same_detail_on_the_equivalent_faces` fails, a generator read a region by name instead of through `Pen.Region`.

- [ ] **Step 5: Commit the engine details**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/Details.cs src/RBX15.Engine.Tests/DetailsTests.cs
git commit -m "feat(engine): the 16 clothing details, a port of createDetailLayer that also draws in Pants mode"
```

- [ ] **Step 6: Write the failing loader tests, and retire `detail` as the unsupported-op example**

Create `src/RBX15.Parity.Tests/SceneLoaderDetailTests.cs`:

```csharp
using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderDetailTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static SceneLoader.Scene Load(string json) => SceneLoader.Parse(JsonDocument.Parse(json).RootElement, Root);

    [Fact]
    public void A_detail_appends_a_named_layer_after_the_scene_layers()
    {
        // app-harness.js detail(): set #detailColor, #detailWidth, #detailDash, click the button;
        // createDetailLayer pushes the layer on top with the next id (editor.js:1858-1859).
        var s = Load("""
            { "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ],
              "actions": [ { "op": "detail", "type": "hem", "color": "#333333", "width": 2, "stitch": false } ] }
            """);
        Assert.Equal(new[] { 1, 2 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal("Hem Line", s.Document.Layers[1].Name);
        Assert.Equal((154.0, 187.0, 1.0), (s.Document.Layers[1].X, s.Document.Layers[1].Y, s.Document.Layers[1].Scale));
    }

    [Fact]
    public void Stitch_defaults_to_off()
    {
        // app-harness.js: setCheck('detailDash', !!op.stitch).
        var plain = Load("""{ "name": "x", "actions": [ { "op": "detail", "type": "yoke", "color": "#333333", "width": 2 } ] }""");
        var off = Details.Create(DetailType.Yoke, TemplateMode.Shirt, new DetailStyle(CssColor.ToSk("#333333"), 2, false), 1)!;
        var layer = plain.Document.Layers.Single();
        Assert.Equal((off.X, off.Y, off.Image.Width, off.Image.Height), (layer.X, layer.Y, layer.Image.Width, layer.Image.Height));
        using var a = ImageDiff.FromImage(off.Image);
        using var b = ImageDiff.FromImage(layer.Image);
        Assert.Equal(0, ImageDiff.Compare(a, b).DifferingPixels);
    }

    [Fact]
    public void Two_clicks_stack_two_identical_layers()
    {
        // Ledger details-mode-independence-and-selection: duplicates are not deduplicated.
        var s = Load("""
            { "name": "x", "actions": [
                { "op": "detail", "type": "pocket", "color": "#ffffff", "width": 2 },
                { "op": "detail", "type": "pocket", "color": "#ffffff", "width": 2 } ] }
            """);
        Assert.Equal(new[] { 1, 2 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal((s.Document.Layers[0].X, s.Document.Layers[0].Y), (s.Document.Layers[1].X, s.Document.Layers[1].Y));
        var (a, b) = (s.Document.Layers[0].Image, s.Document.Layers[1].Image);
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        using var ba = ImageDiff.FromImage(a);
        using var bb = ImageDiff.FromImage(b);
        Assert.Equal(0, ImageDiff.Compare(ba, bb).DifferingPixels);
    }

    [Fact]
    public void An_unknown_detail_type_is_a_scene_error()
    {
        Assert.Throws<InvalidDataException>(() => Load("""
            { "name": "x", "actions": [ { "op": "detail", "type": "belt-loops", "color": "#333333", "width": 2 } ] }
            """));
    }

    [Fact]
    public void A_pants_scene_runs_details_on_the_pants_faces()
    {
        // Approved divergence details-pants-mode-behavior: 4.1 throws (app-harness.js refuses Pants).
        var s = Load("""{ "name": "x", "mode": "Pants", "actions": [ { "op": "detail", "type": "full-detail", "color": "#333333", "width": 2 } ] }""");
        Assert.Equal("Full Detail Set", s.Document.Layers.Single().Name);
        Assert.Equal((8.0, 63.0), (s.Document.Layers[0].X, s.Document.Layers[0].Y));
    }
}
```

In `src/RBX15.Parity.Tests/SceneLoaderTests.cs`, replace the body of `Unsupported_ops_are_listed_once_in_order_and_refuse_to_parse`. Old:

```csharp
        // detail stays unsupported through M2a (Engine Details is M2b), so Task 6 adding erase leaves this intact.
        var e = JsonDocument.Parse("""
            { "name": "x", "actions": [
                { "op": "shape", "tool": "line", "from": [0, 0], "to": [10, 10], "color": "#000000", "width": 2 },
                { "op": "wordart", "text": "HI" }, { "op": "detail", "type": "hem" }, { "op": "wordart", "text": "YO" } ] }
            """).RootElement;
        Assert.Equal(new[] { "wordart", "detail" }, SceneLoader.UnsupportedOps(e));
        var ex = Assert.Throws<NotSupportedException>(() => SceneLoader.Parse(e, Root));
        Assert.Contains("wordart, detail", ex.Message);
```

New:

```csharp
        // "sticker" and "warp" are ops no milestone plans, so enabling detail, wordart and project
        // in M2b leaves this intact.
        var e = JsonDocument.Parse("""
            { "name": "x", "actions": [
                { "op": "shape", "tool": "line", "from": [0, 0], "to": [10, 10], "color": "#000000", "width": 2 },
                { "op": "sticker", "id": 1 }, { "op": "warp", "amount": 2 }, { "op": "sticker", "id": 2 } ] }
            """).RootElement;
        Assert.Equal(new[] { "sticker", "warp" }, SceneLoader.UnsupportedOps(e));
        var ex = Assert.Throws<NotSupportedException>(() => SceneLoader.Parse(e, Root));
        Assert.Contains("sticker, warp", ex.Message);
```

- [ ] **Step 7: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderDetailTests|FullyQualifiedName~SceneLoaderTests"`
Expected: `Failed!  - Failed:     5, Passed:    10, Skipped:     0, Total:    15`. All five `SceneLoaderDetailTests` fail; four with `System.NotSupportedException : scene x: ops the engine does not run yet: detail`, and `An_unknown_detail_type_is_a_scene_error` with `Actual: typeof(System.NotSupportedException)`. The rewritten unsupported-op test passes already.

- [ ] **Step 8: Enable the op (SupportedOps, handler arm, PendingOps and the skip row in one change)**

This is the contract's enabling rule: one change adds `detail` to `SupportedOps` with its handler, removes it from `PendingOps`, and deletes its skip row, so the accounting fact never sees a half state. The script edits by pattern from the state Task 1 left (shown in Task 1 Step 12), and it stops with an `AssertionError` if an anchor is missing. If that happens, read the two files and report; do not hand-edit around it.

```bash
mkdir -p .superpowers/m2b
cat > .superpowers/m2b/enable-detail.py <<'PYEOF'
import re

def edit(path, fn):
    # Keep the file's own line endings (core.autocrlf=true leaves CRLF in the working tree).
    nl = "\r\n" if b"\r\n" in open(path, "rb").read() else "\n"
    src = open(path, encoding="utf-8").read()
    out = fn(src)
    open(path, "w", encoding="utf-8", newline=nl).write(out)
    print("edited", path)

def loader(s):
    # 1. "detail" joins SupportedOps, after "erase" (whatever else Task 1 or 3 added).
    m = re.search(r'(static readonly HashSet<string> SupportedOps = new\(StringComparer\.Ordinal\) \{ )([^}]*)( \};)', s)
    assert m and '"detail"' not in m.group(2), "SupportedOps not found, or detail already there"
    ops = [o.strip() for o in m.group(2).split(",")]
    ops.insert(ops.index('"erase"') + 1, '"detail"')
    s = s[:m.start()] + m.group(1) + ", ".join(ops) + m.group(3) + s[m.end():]
    # 2. The handler arm, right after the erase arm.
    arm = '                    "erase" => ApplyErase(document, action, eraseStroking),\n'
    assert s.count(arm) == 1, "erase arm not found exactly once"
    s = s.replace(arm, arm + '                    "detail" => ApplyDetail(document, action),\n')
    # 3. ApplyDetail, just above the erase op's doc comment.
    anchor = '    /// <summary>\n    /// Scene v2 erase op'
    assert s.count(anchor) == 1, "erase doc comment not found exactly once"
    s = s.replace(anchor, APPLY_DETAIL + anchor)
    return s

APPLY_DETAIL = '''    /// <summary>
    /// Scene v2 detail op: <c>{ "op": "detail", "type": id, "color": "#rrggbb", "width": n, "stitch": bool }</c>.
    /// app-harness.js detail() sets #detailColor, #detailWidth and #detailDash (stitch defaults to
    /// false) and clicks the button (editor.js:2077-2103), so createDetailLayer reads those values
    /// (editor.js:1840-1842) and pushes the layer on top with the next id (editor.js:1858-1859).
    /// The document's mode is passed through, so a Pants scene draws on the pants faces (approved
    /// divergence details-pants-mode-behavior; the harness refuses Pants because 4.1 throws).
    /// </summary>
    public static Document ApplyDetail(Document doc, JsonElement a)
    {
        DetailType type;
        try { type = Details.FromId(a.GetProperty("type").GetString()!); }
        catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        bool stitch = a.TryGetProperty("stitch", out var st) && st.GetBoolean();
        var style = new DetailStyle(CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), stitch);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Details.Create(type, doc.Mode, style, id);
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

'''

def gate(s):
    const = '    const string DetailSkip = "op \'detail\' is not run by the engine yet (Engine Details, M2b)";\n'
    row = '    [MemberData(nameof(ScenesBlockedBy), "detail", Skip = DetailSkip)]\n'
    assert s.count(const) == 1 and s.count(row) == 1, "detail skip constant or row not found exactly once"
    s = s.replace(const, "").replace(row, "")
    m = re.search(r'static readonly string\[\] PendingOps = \{ ([^}]*) \};', s)
    assert m, "PendingOps not found"
    ops = [o.strip() for o in m.group(1).split(",") if o.strip()]
    assert '"detail"' in ops, "detail is not pending"
    ops.remove('"detail"')
    return s[:m.start()] + 'static readonly string[] PendingOps = { ' + ", ".join(ops) + ' };' + s[m.end():]

edit("src/RBX15.Parity/SceneLoader.cs", loader)
edit("src/RBX15.Engine.Tests/GoldenAppTests.cs", gate)
PYEOF
python .superpowers/m2b/enable-detail.py
git diff --stat
```

Expected:

- `edited src/RBX15.Parity/SceneLoader.cs` and `edited src/RBX15.Engine.Tests/GoldenAppTests.cs`.
- `git diff --stat` lists exactly three files: `src/RBX15.Parity/SceneLoader.cs`, `src/RBX15.Engine.Tests/GoldenAppTests.cs` and Step 6's `src/RBX15.Parity.Tests/SceneLoaderTests.cs` (Step 6's `SceneLoaderDetailTests.cs` is new and untracked, so `--stat` does not list it).
- In `SceneLoader.cs`, `SupportedOps` reads `{ "shape", "erase", "detail", "project" }`, and the switch has `"detail" => ApplyDetail(document, action),` right after the erase arm and before the project arm.
- In `GoldenAppTests.cs`, `DetailSkip` and its `MemberData` row are gone and `PendingOps` reads `{ "wordart" }`.

The two files then read in full as follows; Task 3 edits from this state. They are shown with LF, and the working tree keeps CRLF.

`src/RBX15.Parity/SceneLoader.cs` after Task 2:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// <para>
/// Scene format v2 adds an ordered <c>actions</c> array, run after the scene's own layers exist.
/// The loader runs only the ops the engine implements (SupportedOps). A scene holding any other op
/// refuses to parse, so no test ever compares a half-built document against a golden.
/// </para>
/// <para>
/// The public API strokes the eraser once, as the editor does. The internal overloads take an
/// <see cref="EraseStroking"/> so the golden gate can replay 4.1's per-move stroking.
/// </para>
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    /// <summary>Ops the engine runs. Each engine task that implements an op adds it here.</summary>
    static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape", "erase", "detail", "project" };

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    /// <summary>The scene's action ops the engine does not run yet, each once, in first-use order.</summary>
    public static IReadOnlyList<string> UnsupportedOps(JsonElement scene)
    {
        var ops = new List<string>();
        if (scene.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
            {
                var op = action.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                if (!SupportedOps.Contains(op) && !ops.Contains(op)) ops.Add(op);
            }
        return ops;
    }

    public static Scene Parse(JsonElement e, string repoRoot) => Parse(e, repoRoot, EraseStroking.Once);

    internal static Scene Parse(JsonElement e, string repoRoot, EraseStroking eraseStroking)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

        var name = Str(e, "name") ?? throw new InvalidDataException("scene without a name");
        var unsupported = UnsupportedOps(e);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"scene {name}: ops the engine does not run yet: {string.Join(", ", unsupported)}");

        var fills = ImmutableList.CreateBuilder<(string, Fill)>();
        if (e.TryGetProperty("regionColors", out var rc))
            foreach (var p in rc.EnumerateObject())
                fills.Add((p.Name, p.Value.ValueKind == JsonValueKind.String
                    ? Fill.Solid(p.Value.GetString()!)
                    : new Fill(Str(p.Value, "type") ?? "solid", Str(p.Value, "c1"), Str(p.Value, "c2"), Num(p.Value, "angle"), Num(p.Value, "scale"))));

        var layers = ImmutableList.CreateBuilder<Layer>();
        if (e.TryGetProperty("layers", out var ls))
        {
            int i = 0;
            foreach (var l in ls.EnumerateArray())
            {
                i++;
                layers.Add(new Layer
                {
                    Id = i, Name = "L" + i, Image = Image(Str(l, "image")!),
                    Visible = Bool(l, "visible") ?? true, Opacity = Num(l, "opacity") ?? 1,
                    X = Num(l, "x") ?? 0, Y = Num(l, "y") ?? 0, Scale = Num(l, "scale") ?? 1, Rotation = Num(l, "rotation") ?? 0,
                    FlipH = Bool(l, "flipH") ?? false, FlipV = Bool(l, "flipV") ?? false,
                    Brightness = Num(l, "brightness") ?? 100, Contrast = Num(l, "contrast") ?? 100,
                    Saturate = Num(l, "saturate") ?? 100, HueRotate = Num(l, "hueRotate") ?? 0,
                    RShift = (int)(Num(l, "rShift") ?? 0), GShift = (int)(Num(l, "gShift") ?? 0), BShift = (int)(Num(l, "bShift") ?? 0),
                });
            }
        }

        var bgImage = Str(e, "bgImage");
        var document = new Document
        {
            Mode = Str(e, "mode") == "Pants" ? TemplateMode.Pants : TemplateMode.Shirt,
            BgColor = Str(e, "bgColor") ?? "#192e44",
            BgOpacity = Num(e, "bgOpacity") ?? 1,
            BgImage = bgImage is null ? null : Image(bgImage),
            RegionFills = fills.ToImmutable(),
            Layers = layers.ToImmutable(),
        };

        if (e.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
                document = action.GetProperty("op").GetString() switch
                {
                    "shape" => ApplyShape(document, action),
                    "erase" => ApplyErase(document, action, eraseStroking),
                    "detail" => ApplyDetail(document, action),
                    "project" => ApplyProject(action, repoRoot),
                    var op => throw new InvalidDataException($"scene {name}: op '{op}' has no handler"),
                };

        return new Scene(name, document);
    }

    /// <summary>
    /// A shape gesture from <c>from</c> to <c>to</c>, committed as 4.1 commits it: a new drawing layer
    /// on top, with the next id after every layer already there (editor.js:1285, :528-540).
    /// tool, from, to, color and width are required; fill defaults to false (drawFill, editor.js:92).
    /// </summary>
    static Document ApplyShape(Document doc, JsonElement a)
    {
        var tool = a.GetProperty("tool").GetString() switch
        {
            "line" => ShapeTool.Line,
            "rect" => ShapeTool.Rect,
            "ellipse" => ShapeTool.Ellipse,
            var t => throw new InvalidDataException($"unknown shape tool '{t}'"),
        };
        SKPoint Pt(string key)
        {
            var p = a.GetProperty(key);
            return new SKPoint(p[0].GetSingle(), p[1].GetSingle());
        }
        bool fill = a.TryGetProperty("fill", out var f) && f.GetBoolean();
        var stroke = new Stroke(tool, new[] { Pt("from"), Pt("to") },
            CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), fill);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Tools.CommitDrawing(stroke, id, Tools.DrawingName(tool));
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

    /// <summary>
    /// Scene v2 project op: <c>{ "op": "project", "file": "&lt;repo-relative .r15proj&gt;" }</c>. 4.1
    /// (app-harness.js project) feeds the file to #projectFileInput, whose change handler runs
    /// loadProject (editor.js:2684-2687, 2593-2680), and waits until every image layer and the
    /// background have loaded. The loaded project replaces the document, so the result is the file's
    /// document alone. A file that drops a layer is refused: the harness only froze projects whose
    /// every layer loaded, so a drop would compare a different document against the golden.
    /// </summary>
    public static Document ApplyProject(JsonElement action, string repoRoot)
    {
        var file = action.GetProperty("file").GetString() ?? throw new InvalidDataException("project: file is null");
        var load = ProjectIO.Read(File.ReadAllText(Path.Combine(repoRoot, file)));
        if (!load.DroppedLayers.IsEmpty)
            throw new InvalidDataException($"project {file}: layers that did not load: {string.Join(", ", load.DroppedLayers)}");
        return load.Document;
    }

    /// <summary>
    /// Scene v2 detail op: <c>{ "op": "detail", "type": id, "color": "#rrggbb", "width": n, "stitch": bool }</c>.
    /// app-harness.js detail() sets #detailColor, #detailWidth and #detailDash (stitch defaults to
    /// false) and clicks the button (editor.js:2077-2103), so createDetailLayer reads those values
    /// (editor.js:1840-1842) and pushes the layer on top with the next id (editor.js:1858-1859).
    /// The document's mode is passed through, so a Pants scene draws on the pants faces (approved
    /// divergence details-pants-mode-behavior; the harness refuses Pants because 4.1 throws).
    /// </summary>
    public static Document ApplyDetail(Document doc, JsonElement a)
    {
        DetailType type;
        try { type = Details.FromId(a.GetProperty("type").GetString()!); }
        catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        bool stitch = a.TryGetProperty("stitch", out var st) && st.GetBoolean();
        var style = new DetailStyle(CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), stitch);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Details.Create(type, doc.Mode, style, id);
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

    /// <summary>
    /// Scene v2 erase op: <c>{ "op": "erase", "layer": i, "points": [[x,y],...], "size": n }</c>, stroked
    /// once like the editor. 4.1 (app-render.js) selects layer i, picks the eraser, presses at
    /// points[0], moves through the rest and releases (editor.js:1120-1146, 1196-1203, 1248-1283).
    /// Drawings append above the scene's layers (editor.js:534 layers.push), so i indexes the
    /// document's layers directly.
    /// </summary>
    public static Document ApplyErase(Document document, JsonElement action) => ApplyErase(document, action, EraseStroking.Once);

    internal static Document ApplyErase(Document document, JsonElement action, EraseStroking stroking)
    {
        int index = action.GetProperty("layer").GetInt32();
        if (index < 0 || index >= document.Layers.Count)
            throw new InvalidDataException($"erase: the scene has no layer {index}");
        var points = action.GetProperty("points").EnumerateArray()
            .Select(p => new SKPoint((float)p[0].GetDouble(), (float)p[1].GetDouble()))
            .ToList();
        float size = (float)action.GetProperty("size").GetDouble();
        var erased = Eraser.Apply(document.Layers[index], points, size, stroking);
        return document with { Layers = document.Layers.SetItem(index, erased) };
    }
}
```

`src/RBX15.Engine.Tests/GoldenAppTests.cs` after Task 2:

```csharp
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2b enabled detail, wordart and project one task at a time; PendingOps lists what is still skipped).
/// <para>
/// Erase actions run through the internal EraseStroking.Replay41, which re-strokes the eraser path
/// after every mousemove exactly as app-render.js drove 4.1 (mousedown at points[0], one mousemove
/// per later point, mouseup with no stroke), so every erase scene meets the unchanged rule.
/// Production strokes once (approved divergence draw-pen-incremental-stroke); it is held to the
/// goldens directly on the two-point drags, where 4.1 strokes once too.
/// </para>
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    const string WordArtSkip = "op 'wordart' is not run by the engine yet (Engine WordArt, M2b)";
    static readonly string[] PendingOps = { "wordart" };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    static JsonElement Scene(string name) => Raw.Value.Single(e => NameOf(e) == name);

    /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
    public static TheoryData<string> ScenesBlockedBy(string op)
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            var unsupported = SceneLoader.UnsupportedOps(e);
            if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
        }
        return data;
    }

    /// <summary>Runnable scenes with at least one erase, every erase a two-point drag (one stroke in 4.1).</summary>
    public static TheoryData<string> TwoPointEraseScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            if (SceneLoader.UnsupportedOps(e).Count != 0 || !e.TryGetProperty("actions", out var actions)) continue;
            var erases = actions.EnumerateArray().Where(a => a.GetProperty("op").GetString() == "erase").ToList();
            if (erases.Count > 0 && erases.All(a => a.GetProperty("points").GetArrayLength() == 2)) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root, EraseStroking.Replay41);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Theory]
    [MemberData(nameof(TwoPointEraseScenes))]
    public void Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".once.png"));
    }

    [Fact]
    public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
    {
        var unclassified = Raw.Value
            .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
            .ToList();
        Assert.Empty(unclassified);
    }

    [Fact]
    public void Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps()
    {
        var rows = typeof(GoldenAppTests).GetMethod(nameof(Engine_export_matches_the_frozen_4_1_app_golden))!
            .GetCustomAttributes(typeof(MemberDataAttribute), false).Cast<MemberDataAttribute>()
            .Select(a => (Op: (string)a.Parameters[0], a.Skip))
            .ToList();

        var skipOps = rows.Where(r => r.Skip != null).Select(r => r.Op).ToList();
        Assert.Equal(PendingOps.OrderBy(o => o), skipOps.OrderBy(o => o));
        Assert.Single(rows, r => r.Skip == null && r.Op == "");

        var accounted = rows.Sum(r => ((System.Collections.IEnumerable)ScenesBlockedBy(r.Op)).Cast<object>().Count());
        Assert.Equal(Raw.Value.Count, accounted);
        Assert.Equal(82, Raw.Value.Count);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
```

- [ ] **Step 9: Run the loader tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderDetailTests|FullyQualifiedName~SceneLoaderTests"`
Expected: `Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15`.

- [ ] **Step 10: Run the app golden gate on the detail scenes**

First count what the gate should report:

```bash
python - <<'EOF'
import json
s = json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))
d = [x["name"] for x in s if any(a["op"] == "detail" for a in x.get("actions", []))]
pure = [x for x in s if any(a["op"] == "detail" for a in x.get("actions", [])) and all(a["op"] == "detail" for a in x["actions"])]
modes = {x.get("mode", "Shirt") for x in s if x["name"] in d}
print("detail scenes", len(d), "detail-only", len(pure), "modes", sorted(modes))
EOF
```

Expected: `detail scenes 37 detail-only 37 modes ['Shirt']`.

Run: `mkdir -p .superpowers/logs && dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" > .superpowers/logs/app-golden-detail.log 2>&1; tail -5 .superpowers/logs/app-golden-detail.log`
Expected: `Passed!  - Failed:     0, Passed:    66, Skipped:    22, Total:    88`. That is Task 1's 29 passed plus the 37 detail rows, with the 22 wordart rows still skipped. The accounting fact passes.

If a detail scene fails, the port is wrong (fix-the-engine rule: never edit a golden, `ImageDiff` or `GoldenRule`). Use superpowers:systematic-debugging from `diff-heatmaps/app/<scene>.png`. In order of likelihood:

- **Only the stitched scenes fail.** The dash: intervals must be `[2w, 2w]` at phase 0 on the stroke paint, and the fill paint must carry no path effect.
- **A line that should be crisp is soft, or a subpixel line is crisp.** A coordinate was rounded or computed in `float`; compare with the JS expression on the cited line.
- **A seam where round caps meet differs by a few levels.** Two 4.1 strokes were merged into one path, or their order changed. One `Pen.Stroke` per `stroke()`, in 4.1's order.
- **Buttons differ at their rims.** The circle was drawn with `DrawCircle` or `AddCircle`. Build it as two 180-degree `ArcTo` calls from angle 0.
- **The whole layer is shifted.** The crop origin: `Create` must place the layer at `Tools.Autocrop`'s `X, Y` with pad 10.
- **Semi-transparent edge pixels are off by one level everywhere.** The premultiply round trip: the crop must be read back `Unpremul` as `Tools.CommitDrawing` does.

If none of these explains it, stop and report the scene, the heatmap and the `DiffResult`.

- [ ] **Step 11: Record the per-scene numbers**

```bash
mkdir -p .superpowers/m2b-measure .superpowers/logs
python - <<'EOF'
import json
s = json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))
d = [x for x in s if any(a["op"] == "detail" for a in x.get("actions", []))]
open(".superpowers/m2b-measure/detail-scenes.json", "w", encoding="utf-8", newline="\n").write(json.dumps(d))
print(len(d), "detail scenes")
EOF
dotnet run --project src/RBX15.Parity -c Release -- render .superpowers/m2b-measure/detail-scenes.json .superpowers/m2b-measure/detail > /dev/null
for f in .superpowers/m2b-measure/detail/*.png; do
  n=$(basename "$f" .png); printf '%s ' "$n"
  dotnet run --project src/RBX15.Parity -c Release --no-build -- diff "tests/goldens/app/$n.png" "$f"
done | tee .superpowers/logs/detail-finding.txt
```

Expected: `37 detail scenes`, then 37 lines `<scene> same-size=True max-delta=... differing=... interior=0 edge-over-tolerance=0 pass=True`. The prototype measured `max-delta=0 differing=0` on all 37. Any `pass=False` contradicts Step 10's green gate: stop and report.

- [ ] **Step 12: Commit the loader and the detail gate**

```bash
python - <<'EOF'
lines = open(".superpowers/logs/detail-finding.txt", encoding="utf-8").read().strip().splitlines()
msg = "\n".join([
    "test(engine): every detail golden met; the detail op runs in SceneLoader",
    "",
    "detail joins SupportedOps with ApplyDetail, leaves PendingOps and loses its skip row in",
    "one change. SceneLoaderTests' unsupported-op example now uses ops no milestone plans.",
    "",
    "Measured against each detail golden (RBX15.Parity diff):",
    *lines,
])
open(".superpowers/logs/detail-commit-msg.txt", "w", encoding="utf-8", newline="\n").write(msg + "\n")
print(msg)
EOF
```

Append your model's `Co-Authored-By` trailer as the last line of `.superpowers/logs/detail-commit-msg.txt` (after a blank line), then:

```bash
git add src/RBX15.Parity/SceneLoader.cs src/RBX15.Engine.Tests/GoldenAppTests.cs src/RBX15.Parity.Tests/SceneLoaderTests.cs src/RBX15.Parity.Tests/SceneLoaderDetailTests.cs
git commit -F .superpowers/logs/detail-commit-msg.txt
```

Expected: `git log -1 --format=%s` prints `test(engine): every detail golden met; the detail op runs in SceneLoader`. The enable script stays under `.superpowers/` (git-ignored) and is not committed.

- [ ] **Step 13: Run everything**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: `0 Warning(s)`. Engine.Tests `Passed: 389, Failed: 0, Skipped: 22` (Task 1's 271 + 81 `DetailsTests` + 37 detail goldens; 59 - 37 skipped). Parity.Tests `Passed: 31, Failed: 0` (26 + 5). `GoldenRenderTests` still passes all 34 scenes; nothing in the render path changed.

**Evidence this task supplies (Task 4 decides closure).** Rows close in Task 4 in one batch, never here:

- `details-layer-creation-pipeline` (golden): `Create` follows `editor.js:1834-1865`; all 37 detail scenes pass bit-exact, covering stitch on and off and widths 1, 2 and 8. Task 4 holds the row open: saveUndo, the push on top, the selection and the switch to Select are App behavior.
- `detail-collar-v`, `detail-collar-crew`, `detail-collar-scoop`, `detail-collar-henley`, `detail-cuffs`, `detail-hem`, `detail-seams`, `detail-pocket`, `detail-placket`, `detail-shoulder-stripe`, `detail-arm-bands`, `detail-yoke`, `detail-full-set` (golden): each id's plain and stitched scenes, plus the width scenes for henley, placket, seam-sides and full-detail.
- `details-mode-independence-and-selection` (unit): `Two_clicks_stack_two_identical_layers` (distinct ids, equal position, size and pixels). Selection itself is the App's.
- `details-pants-mode-behavior` (unit, divergence already approved): `Pants_draws_the_same_detail_on_the_equivalent_faces` (16 rows) and `A_pants_scene_runs_details_on_the_pants_faces`.
- `details-buttons` (golden, owner Engine.Details): the engine half is met (16 generators, names, crop). Its "no empty undo entry" half is the App's (record history only when `Create` returns a layer), so it stays open unless the ledger task splits it.
- Stay open for the App: `details-settings-controls`, `details-buttons-inventory`, `details-settings` (the ribbon controls; `Details.Id` and `Details.LayerName` feed them).

---

### Task 3: Fonts, Engine WordArt and the wordart golden op

**Files:**

- Create: `src/RBX15.Engine/Fonts.cs`, `src/RBX15.Engine/WordArt.cs`, `src/RBX15.Engine.Tests/FontsTests.cs`, `src/RBX15.Engine.Tests/WordArtTests.cs`, `src/RBX15.Parity.Tests/SceneLoaderWordArtTests.cs`, `tools/parity-ref/wordart-ref.js` and `tests/goldens/app/wordart-ref/` (Step 21, captured once, then frozen), `src/RBX15.Engine.Tests/GoldenRuleWordArtTests.cs` and `src/RBX15.Engine.Tests/WordArtSwapTests.cs` (Step 22)
- Modify: `src/RBX15.Engine/RBX15.Engine.csproj` (HarfBuzzSharp, font content), `src/RBX15.Parity/SceneLoader.cs` (the `wordart` op), `src/RBX15.Engine.Tests/GoldenAppTests.cs` (drop the `wordart` skip row; Step 23 routes scenes with a `wordart` action to the Word Art rule), `src/RBX15.Engine.Tests/GoldenRule.cs` (Step 23, the Word Art rule), `docs/port/*` (Step 23, the `wordart-glyph-rasterization` row)
- Read only: `assets/fonts/**` (never edited), `tests/goldens/app/**` (frozen; Step 21 only adds the new `wordart-ref` folder)

**Interfaces:**

- Consumes:
  - `Layer`, `Document` (`src/RBX15.Engine/Document.cs`, M1), `TemplateMode`, `BodyPart`, `Face`, `Template.Slot` (`src/RBX15.Engine/Template.cs`, M1), `CssColor.ToSk` (`src/RBX15.Engine/CssColor.cs`, M1).
  - `assets/fonts/README.md` manifest rows and files (M2a Task 1), `RepoRoot.Find` (M1, tests only).
  - `SceneLoader.Parse`, `SceneLoader.UnsupportedOps`, the private `SupportedOps` and the `actions` switch (`src/RBX15.Parity/SceneLoader.cs`, M2a Task 5); `GoldenAppTests.PendingOps` and its skip rows (M2a Task 5); `GoldenRule.AssertMatches` (M2a Task 5).
  - `ImageDiff.Load`, `ImageDiff.FromImage`, `ImageDiff.Compare`, `ImageDiff.Heatmap` (`src/RBX15.Parity/ImageDiff.cs`, M1, unchanged), `Images.Decode` and `Renderer.Export` (M1).
  - NuGet `HarfBuzzSharp` `14.2.1.301` (Global Constraints; approved in ruling R1).
- Produces:
  - `public static partial class Fonts`
    - `IReadOnlyList<string> WordArtFamilies` (15, in `waFont` order), `const int BoldThreshold = 600`, `string Root`
    - `SKTypeface Get(string family, int weight)`: cache-owned; throws `ArgumentException` for a family the manifest lacks
    - `bool IsSyntheticBold(string family, int weight)`
    - `SKFont CreateFont(string family, int weight, float size)`: caller disposes
    - internal: `Face` record, `ParseManifest`, `Match`, `AxisWeight`, `FileBytes`, `WghtTag`
  - `public enum WordArtStyle { Solid, Gradient, Neon, Shadow, Outline, Retro }`
  - `public sealed record WordArtSettings(string Text, string Font, WordArtStyle Style, float Size, SKColor Fill, SKColor Stroke, SKColor Grad, float StrokeWidth, float Angle)`, `Angle` in degrees
  - `public static class WordArt`
    - `const int Weight = 900`, `const float MinSize = 8, MaxSize = 300`
    - `(SKImage Image, int Width, int Height) Render(WordArtSettings s, int? maxWidth = null, int? maxHeight = null)`: the image is caller-owned; `Width` and `Height` are 4.1's `cw` and `ch`
    - `Layer CreateLayer(WordArtSettings s, TemplateMode mode, int id)`
    - `string LayerName(string text, string font)`
    - internal: `ShapedRun(SKTypeface Typeface, bool Primary, ushort[] Glyphs, float[] Xs)`, `ShapedLine(IReadOnlyList<ShapedRun> Runs, float Width)`, `FallbackFace`, `Shape`, `MiddleOffset`, `Pad`, `EffectiveText`, `EffectiveSize`, `OutlineWidth`
  - `public static Document SceneLoader.ApplyWordArt(Document doc, JsonElement action)` and `"wordart"` in `SupportedOps`; `GoldenAppTests.PendingOps` loses `"wordart"` (committed in Step 23 with the Word Art rule).
  - The Word Art rule (ruling R2, Step 23), in `GoldenRule`:
    - `public static void AssertWordArtMatches(string name, string goldenPath, Document document, string heatmapPath)`
    - `internal sealed record WordArtResult(bool SameSize, int InteriorMaxDelta, int InteriorOverOne, int EdgeMaxDelta)` with `bool Pass`
    - `internal static WordArtResult CompareWordArt(SKBitmap golden, SKBitmap candidate)` and `internal static void AssertWordArtBitmaps(string name, SKBitmap golden, SKBitmap candidate, string heatmapPath)`
  - `tools/parity-ref/wordart-ref.js`: 4.1's own Word Art bitmap and layout metrics per scene (Step 21).
  - `tests/goldens/app/wordart-ref/`: those 22 bitmaps, `metrics.json` and a provenance `index.json`, frozen (Step 21), read by `WordArtSwapTests` (Step 22).
  - Ledger row `wordart-glyph-rasterization` (Engine.WordArt, golden, fix, open), Step 23.

**What 4.1 does (cited).** `editor.js` and `editor.html` at the blobs `tests/goldens/app/index.json` records.

- **Inputs** (`editor.html:229-262`): `waText` textarea (default `RIZZ`), `waFont` select (15 families, first `Anton`, `:230-246`), `waColor` `#17d4fa`, `waStroke` `#000000`, `waColor2` `#f22f89`, `waSize` number 64 (min 8, max 300, not enforced), `waStrokeW` range 0..20 (3), `waAngle` range 0..360 (0). Style buttons at `:269-274`; `waStyle` starts `'solid'` (`editor.js:2108`) and a click sets it (`:2111-2118`).
- **renderWordArt** (`editor.js:2120-2240`):
  - Reads the inputs (`:2121-2128`): `text = waText || 'TEXT'`, `fontSize = +waSize || 64`, `angle = waAngle * PI / 180`.
  - Font `900 <size>px "<family>", sans-serif` (`:2131-2132`).
  - Lines split on `\n`, `lineHeight = fontSize * 1.15`, `totalW` the widest `measureText(line).width`, `totalH = lines * lineHeight` (`:2135-2142`).
  - `pad = max(strokeW * 2, neon ? 30 : 10, shadow ? 15 : 0, retro ? 12 : 0)`, `cw = ceil(totalW + 2 pad)`, `ch = ceil(totalH + 2 pad)` (`:2145-2147`).
  - Canvas `min(cw, maxW)` by `min(ch, maxH)` when maxima are given, else `cw` by `ch`; uniform `sc = min(width/cw, height/ch)`; `scale(sc, sc)`, `textAlign = 'center'`, `textBaseline = 'middle'` (`:2149-2161`).
  - `cx = cw / 2`, line i at `ty = pad + lineHeight / 2 + i * lineHeight` (`:2163-2167`).
  - Gradient: `gx = cos(angle) * cw / 2`, `gy = sin(angle) * ch / 2`, linear from `(cx - gx, ty - gy)` to `(cx + gx, ty + gy)`, stop 0 fill, stop 1 grad (`:2171-2179`).
  - Shadow: `rgba(0,0,0,0.6)` at `(cx + 4, ty + 4)` (`:2182-2188`).
  - Retro: `i = 8..1`, even `i` the stroke color, odd the grad color, at `(cx + i, ty + i)` (`:2190-2196`).
  - Neon: `shadowColor = fill`, `shadowBlur = 20`, fill; `shadowBlur = 40`, fill; then `#fff` at `globalAlpha 0.8` (`:2198-2213`).
  - Outline: `strokeStyle = fill`, `lineWidth = strokeW || 3`, `lineJoin = 'round'`, `strokeText` (`:2214-2219`).
  - Otherwise: fill; when `strokeW > 0`, stroke (`waStroke`, `strokeW`, round join) then fill again (`:2220-2235`).
- **Add to canvas** (`editor.js:2257-2291`): `saveUndo()`, a full-size render (`cw` by `ch`, `:2259-2266`), `toDataURL` and an `img` decode (`:2268-2269`, `:2290`), then a layer with `id: nextId++`, name `'\u{2728} ' + text.substring(0, 20) + ' [' + fontName + ']'`, scale `min(front.w / img.width, front.h / img.height) * 0.85`, centered in `torso_front` (`:2272-2283`), every other field a `makeLayer` default.
- **How the goldens drove it** (`tools/parity-ref/app-harness.js:154-176`): set only the inputs a scene names, select the font (`change`), click the style button, wait for the exact `900 <size>px "<family>"` face to load, click Add to canvas. `tools/parity-ref/fonts.js` declares one `@font-face` per family and requested weight, which is what `Fonts` resolves.

**Risk notes that act in this task:**

- **Line endings.** `core.autocrlf=true` leaves CRLF in the working tree; the enable script keeps each file's own line endings, as Task 2's does.
- **Never commit a red gate.** Step 20 is expected to fail on all 22 scenes under the unchanged rule. The enabling change stays uncommitted in the working tree until Step 23 commits it together with the Word Art rule. The Fonts, WordArt, fallback and fixture commits (Steps 5, 10, 15 and 21) are green on their own.

**Drafting evidence (measured 2026-10-14).** Every file in this task was compiled and run in a scratch copy of `main` at `db6b1cd` (`src`, `global.json`, with `tests`, `assets` and `packs` linked read-only), exactly as written below.

- `dotnet build -c Release -warnaserror` for `RBX15.Engine.Tests`, `RBX15.Parity.Tests` and `RBX15.App`: `0 Warning(s)`, `0 Error(s)`. The App's output holds `assets/fonts` (18 family directories plus `README.md`) and `libHarfBuzzSharp.dll`.
- `FontsTests` 32 passed, `WordArtTests` 43 passed, `SceneLoaderWordArtTests` 4 passed (4 failed before the enable step, as Step 17 says). The reconciler added `Solid_with_stroke_width_0_draws_no_stroke` (44 passed), and re-ran Tasks 1 to 3 in order on a scratch copy. The cumulative counts in the run steps below are from that run, and so are the 22 Step 20 lines, which are identical.
- The amendment for rulings R2 and R3 applied Steps 11 to 23 to that copy: `WordArtTests` 46, `GoldenRuleWordArtTests` 4, `WordArtSwapTests` 23, and the full suite at Engine.Tests 516 and Parity.Tests 35 on three runs in a row. The 22 Step 20 lines are byte-identical before and after the fallback change. Changing the 0.85 fit to 0.849 fails all 22 swap cases while the Word Art rule still passes, which is why the swap guard is permanent.
- From `main` plus this task alone: Engine.Tests `Passed: 263, Failed: 22, Skipped: 40`; the 22 failures are exactly the 22 Word Art goldens. Parity.Tests `Passed: 27` (with Task 2's rewrite of the unsupported-op example present).
- **The Add-to-canvas composition is bit-exact.** Swapping the engine's Word Art bitmap for the one 4.1's own `renderWordArt` drew (rendered from the real `editor.html` in Electron 41 with the vendored fonts, scratch tool) makes all 22 exports match their goldens with `max-delta=0 differing=0`. Placement, the 85% fit, the un-premultiplied round trip and the downscaled draw are right; every remaining difference is inside the text bitmap.
- **Every layout number matches 4.1.** Canvas sizes (all 22 scenes), `measureText` widths, and the `middle` baseline offsets are equal to what 4.1 measured in Electron; `WordArtTests` pins them.
- **The rest is glyph rasterization.** Under the unchanged `GoldenRule`, all 22 scenes fail on glyph edges only: interior differences are 0 on 21 scenes, and 7 on `wordart-outline-anton` (delta 1, see below). Per-scene results are in Step 20.

**Why the glyph edges differ:**

- Chromium on Windows rasterizes web-font glyphs through DirectWrite. Its masks show ClearType-style horizontal spreading (one row of `wordart-outline-anton` reads `0, 42, 217, 255` in 4.1 against `0, 0, 224, 255` in the engine) and 87 to 184 distinct coverage levels in a fill-only render.
- SkiaSharp's default font manager on Windows produces 17 coverage levels for a fill-only render of a face without synthetic bold (`0, 30, 55, 77, 97, ...`). That is consistent with GDI grayscale antialiasing. `ForceAutoHinting`, every `SKFontHinting` value and `LinearMetrics` leave the measured differences unchanged, so those switches do not reach it. SkiaSharp 4.153.1 exposes no font manager choice and no text gamma or contrast (`SKSurfaceProperties` holds only flags and pixel geometry).
- Drawing glyph outlines as paths instead (Skia's own scan converter) was measured worse: interior differences appear on 2 scenes and `wordart-outline-anton`'s max delta rises from 48 to 179.
- The only route to near-exact text is calling DirectWrite directly (Windows COM interop: in-memory font loader, `IDWriteGlyphRunAnalysis`, Chromium's text gamma and contrast). That is a controller decision, not part of this task.

**Chromium behaviors this task matches, each measured:**

- **Shaping.** Dancing Script's `RIZZ` is 158.272 px in 4.1, not the 158.656 px of the summed advances: a GPOS `kern` of -6 units on Z-Z at wght 700. HarfBuzz shaping reproduces it. Without shaping, `wordart-solid-dancing` rises from max-delta 56 to 92 and `wordart-shadow-dancing` from 76 to 108.
- **Neon blur.** `shadowBlur` B maps to sigma B/2, ignoring the canvas transform. On the 3 neon goldens, sigma `0.288675*B + 0.5` gives 80 to 164 interior differences and `0.57735*B + 0.5` gives 221 to 329; B/2 gives 0.
- **Gradient dither.** Chromium dithers canvas gradients. Against 4.1's own pre-scale bitmap, `wordart-gradient-poppins` differs on 4779 pixels with dither off and 1946 with it on, about the solid scene's 1955. Dither on a neon paint adds interior differences (164 on `wordart-neon-dancing`), so only the gradient paint dithers.
- **Middle baseline.** The normalized OS/2 typo ascent and descent in LayoutUnits: Anton 64 px 18.015625, Poppins 16, Dancing Script 17.0625, Orbitron 72 px 22.046875, equal to 4.1's `measureText` with `textBaseline = 'middle'`.
- **Faces.** A 900 request draws Poppins, Lato and Orbitron at true 900, Dancing Script at wght 700, and the 11 single-weight families with synthetic bold. Synthetic bold keeps the advance (Anton R is 30.5 px in 4.1 and in the engine).

**The 7 interior pixels on wordart-outline-anton.** All seven are in one column, `x=314, y=147..153`: golden `(22, 212, 250, 255)`, engine `(23, 212, 250, 255)`. The fill is `#17d4fa` (23, 212, 250). The golden column is one level darker because 4.1's stroke coverage there is just under full, and the 0.922 downscale spreads it into a run the 3x3 rule calls flat. It is a glyph-coverage difference of one level that the golden rule classifies as interior. Ruling R2 classified it as glyph rasterization, on the reconciler's measurement of this scene: 4.1's bitmap and the engine's are the same size, no pixel is full in one and empty in the other, the alpha-weighted centroids differ by under 0.04 px, and with 4.1's bitmap swapped in the export equals the golden (`max-delta=0 differing=0`). The Word Art rule's 1-level interior bound admits these 7 pixels, and `WordArtSwapTests` keeps the swap result exact for all 22 scenes.

**Delta distribution (the evidence ruling R2 used).** Pixels per scene whose delta exceeds each level, with the max delta (engine export against golden; transparent-on-transparent counts as equal, as in `ImageDiff`):

| Scene | Max | > 3 | > 8 | > 16 | > 32 | > 48 |
| --- | --- | --- | --- | --- | --- | --- |
| `wordart-gradient-135-poppins` | 30 | 798 | 324 | 119 | 0 | 0 |
| `wordart-gradient-anton` | 42 | 1154 | 490 | 140 | 22 | 0 |
| `wordart-gradient-dancing` | 39 | 867 | 272 | 59 | 1 | 0 |
| `wordart-gradient-poppins` | 37 | 789 | 295 | 122 | 4 | 0 |
| `wordart-multiline-orbitron` | 45 | 1168 | 519 | 271 | 37 | 0 |
| `wordart-neon-anton` | 61 | 459 | 275 | 150 | 73 | 34 |
| `wordart-neon-dancing` | 56 | 335 | 64 | 12 | 4 | 1 |
| `wordart-neon-poppins` | 38 | 241 | 162 | 39 | 11 | 0 |
| `wordart-outline-anton` | 48 | 1585 | 719 | 300 | 26 | 0 |
| `wordart-outline-dancing` | 47 | 907 | 436 | 177 | 26 | 0 |
| `wordart-outline-poppins` | 44 | 944 | 485 | 200 | 44 | 0 |
| `wordart-outline-stroke-0-dancing` | 47 | 907 | 436 | 177 | 26 | 0 |
| `wordart-retro-anton` | 63 | 1696 | 1039 | 563 | 143 | 7 |
| `wordart-retro-dancing` | 76 | 1680 | 935 | 457 | 80 | 10 |
| `wordart-retro-poppins` | 79 | 1037 | 530 | 254 | 24 | 14 |
| `wordart-shadow-anton` | 67 | 972 | 553 | 198 | 92 | 24 |
| `wordart-shadow-dancing` | 76 | 833 | 264 | 67 | 13 | 3 |
| `wordart-shadow-poppins` | 39 | 548 | 280 | 121 | 3 | 0 |
| `wordart-solid-anton` | 49 | 1210 | 584 | 256 | 52 | 3 |
| `wordart-solid-dancing` | 56 | 893 | 330 | 92 | 8 | 2 |
| `wordart-solid-poppins` | 32 | 807 | 320 | 147 | 0 | 0 |
| `wordart-stroke-12-anton` | 77 | 736 | 466 | 197 | 77 | 26 |

The largest edge delta over all 22 scenes is 79. The Word Art rule leaves edge pixels unbounded and bounds interior pixels at 1 level, so every scene in the table passes it; `WordArtSwapTests` is what holds layout and placement exact.

**Contract points this task settles:**

- **Synthetic bold is a font setting, not a typeface.** Skia applies fake bold through `SKFont.Embolden`, so `Fonts.Get` returns the typeface the contract names and two additive members carry the rest: `Fonts.IsSyntheticBold(family, weight)` and `Fonts.CreateFont(family, weight, size)` (the SkFont Chromium's canvas used). `Fonts.WordArtFamilies` and `Fonts.Root` are additive too.
- **How the fonts reach every build output.** `RBX15.Engine.csproj` includes `..\..\assets\fonts\**\*` as `Content` with `LinkBase="assets\fonts"` and `CopyToOutputDirectory="PreserveNewest"`. Content items flow to every referencing project, so the test outputs and the App output get `assets/fonts/<family>/<file>` and `assets/fonts/README.md`. `Fonts.Root` is `AppContext.BaseDirectory/assets/fonts`; the engine never looks for the repo root. `FontsTests` proves the copy is byte-identical to `assets/fonts`.
- **The manifest is the face table.** `Fonts` parses `assets/fonts/README.md` rows: the file, the family, and the "4.1 requests" weights. That is exactly `tools/parity-ref/fonts.js` `FACES` (28 faces), so the engine resolves what the goldens were captured with.
- **Size.** `+waSize || 64` first (0 and NaN become 64, `editor.js:2125`), then the approved clamp to 8..300.
- **Render maxima.** Null or 0 means none, as 4.1's falsy `maxW ?` check (`editor.js:2149-2150`); a negative maximum throws `ArgumentOutOfRangeException`.
- **Pants mode.** `CreateLayer` places through `Template.Slot(mode, Body, Front)`, so Pants works (`mode-pants-crash-hardcoded-keys`); unit tests only, as no Pants golden exists.
- **Enabling the op.** Step 18's script edits by pattern, like Task 2's, from the state Task 2 left, and leaves `PendingOps` empty. Task 2 already rewrote the M2a unsupported-op example (`sticker`, `warp`), so this task does not touch `SceneLoaderTests.cs`.
- **Variable files are instanced at the matched `@font-face`'s declared weight, not the raw request** (ruling R5 reads the contract's "requested weight" at `contract.md:68` as this declared weight). Dancing Script requested at 900 draws at wght 700, because Blink clamps to the face's descriptor. That behavior is measured, and `Variable_files_draw_at_the_declared_weight` pins it.
- **`Fonts` is `public static partial class`**, not the contract's `public static class`: the manifest regex is a `GeneratedRegex`. Callers see no difference.

**Edge cases this task pins** (beyond the plan-level Review Focus):

- **Word Art with an empty line** (a trailing Enter in the textarea). Expected: an empty line still adds its line height, and nothing is drawn for it. Pinned in `A_trailing_newline_adds_an_empty_line`.
- **Outline with stroke width 0.** Expected: drawn at 3, pixel-identical (`strokeW || 3`, `editor.js:2217`). Pinned in `Outline_with_stroke_width_0_draws_at_3`.
- **Solid with stroke width 0.** Expected: fill only, so the stroke color cannot reach the pixels (`editor.js:2226`). Pinned in `Solid_with_stroke_width_0_draws_no_stroke`.
- **Word Art in Pants mode** (4.1 throws). Expected: the same fit and position on `waist_front`. Pinned in `Add_to_canvas_fits_85_percent_of_the_front_slot_and_centers`, Pants row.
- **A font family that is not vendored.** Expected: `ArgumentException`, never a system face standing in for the family. Pinned in `An_unknown_family_throws`.
- **A character the vendored family lacks** (an emoji, or a symbol outside the face). Expected: it draws in the system face `SKFontManager.MatchCharacter` picks, never as glyph 0 (`.notdef`), and the pen carries on from the vendored run; text the face covers is still one HarfBuzz run (ruling R3). Pinned in `A_character_the_family_lacks_is_shaped_in_a_system_fallback_face` and `Plain_text_shapes_as_one_run_in_the_vendored_face`.

**Other risks:**

- **`Fonts` shares its name with `System.Windows.Media.Fonts`.** A probe file in the App with only `using RBX15.Engine;` builds clean, but an App file that also imports `System.Windows.Media` gets CS0104 and should alias it (`using EngineFonts = RBX15.Engine.Fonts;`). Nothing in M2b touches the App.
- **Rasterization is Windows-specific.** SkiaSharp's text output depends on the Windows font manager, so the Word Art numbers hold for Windows, which is what CI runs (`windows-latest`). They would differ on another OS. The fallback face is the system's choice too, so the fallback test asserts only that it is not the vendored face and that it has the character.
- **The fallback query is not Chromium's exact chain.** Chromium tries the generic `sans-serif` (Arial on Windows) before the system fallback; `MatchCharacter` with the vendored family name goes straight to the system fallback. They agree on emoji (neither Arial nor a vendored face has them). A character Arial has and the vendored face lacks could pick a different face. No golden exercises it.
- **Renderer's Chromium-compat index.** This task keeps its Chromium points in `WordArt`'s own doc comments and does not touch `Renderer.cs`, so it cannot conflict with Task 4's carry-forward edit there. Task 4 may add one index row pointing at `WordArt`.
- **One test-host crash seen while reconciling.** On the first full-suite run after enabling Word Art (a cold build), the reconciler's scratch copy aborted `RBX15.Parity.Tests` with `Test host process crashed : Fatal error` after 24 tests passed. The next 31 full-suite and Parity-only runs, rebuilds included, were clean, and so were the amendment's three full-suite runs with Steps 11 to 23 applied. **If it recurs during execution, treat it as a bug to diagnose, not a flake to retry past.** The likely cause is native SkiaSharp or HarfBuzz object lifetime across xUnit's parallel test classes: the cache-owned `Fonts` typefaces, the fallback faces `WordArt` caches, and the HarfBuzz blob, face and font each `ShapeRun` creates and disposes. Keep the log, use superpowers:systematic-debugging, and do not move on until the cause is found and pinned by a test.

- [ ] **Step 1: Write the failing Fonts tests**

Create `src/RBX15.Engine.Tests/FontsTests.cs`:

```csharp
using System.Security.Cryptography;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// Fonts resolves the vendored faces as Chromium resolved 4.1's @font-face rules
/// (tools/parity-ref/fonts.js FACES): CSS weight matching, the variable wght axis at the declared
/// weight, and synthetic bold for a 900 request on a face declared below 600.
/// </summary>
public class FontsTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void The_manifest_declares_the_28_faces_fonts_js_declares()
    {
        // 17 Word Art faces (Poppins and Orbitron declare 700 and 900) and 11 UI faces.
        var faces = Fonts.ParseManifest(File.ReadAllText(Path.Combine(Root, "assets", "fonts", "README.md")));
        Assert.Equal(28, faces.Count);
        Assert.Contains(new Fonts.Face("Orbitron", 900, "orbitron/Orbitron[wght].ttf"), faces);
        Assert.Contains(new Fonts.Face("Dancing Script", 700, "dancingscript/DancingScript[wght].ttf"), faces);
    }

    [Fact]
    public void Every_word_art_family_resolves_at_900()
    {
        Assert.Equal(15, Fonts.WordArtFamilies.Count);
        foreach (var family in Fonts.WordArtFamilies)
            Assert.Equal(family, Fonts.Get(family, 900).FamilyName);
    }

    [Theory]
    [InlineData("Poppins", 900, 900)]
    [InlineData("Poppins", 700, 700)]
    [InlineData("Poppins", 800, 900)]
    [InlineData("Poppins", 400, 700)]
    [InlineData("Dancing Script", 900, 700)]
    [InlineData("Orbitron", 900, 900)]
    [InlineData("Anton", 900, 400)]
    [InlineData("Inter", 450, 500)]
    [InlineData("Inter", 300, 400)]
    public void Weight_matching_follows_css(string family, int requested, int declared) =>
        Assert.Equal(declared, Fonts.Match(family, requested).Weight);

    [Theory]
    [InlineData("Anton", true)]
    [InlineData("Bebas Neue", true)]
    [InlineData("Great Vibes", true)]
    [InlineData("Pacifico", true)]
    [InlineData("Creepster", true)]
    [InlineData("Bangers", true)]
    [InlineData("Permanent Marker", true)]
    [InlineData("Bungee Shade", true)]
    [InlineData("Rubik Mono One", true)]
    [InlineData("Press Start 2P", true)]
    [InlineData("Righteous", true)]
    [InlineData("Dancing Script", false)]
    [InlineData("Poppins", false)]
    [InlineData("Lato", false)]
    [InlineData("Orbitron", false)]
    public void A_900_request_is_synthetic_bold_only_on_faces_below_600(string family, bool synthetic)
    {
        Assert.Equal(synthetic, Fonts.IsSyntheticBold(family, 900));
        using var font = Fonts.CreateFont(family, 900, 64);
        Assert.Equal(synthetic, font.Embolden);
    }

    [Fact]
    public void A_request_below_the_bold_threshold_is_never_synthetic() => Assert.False(Fonts.IsSyntheticBold("Anton", 400));

    [Fact]
    public void Variable_files_draw_at_the_declared_weight()
    {
        Assert.Equal(700f, Fonts.AxisWeight("Dancing Script", 900));
        Assert.Equal(900f, Fonts.AxisWeight("Orbitron", 900));
        Assert.Null(Fonts.AxisWeight("Anton", 900));
        // hmtx advances at those instances (1000 upem): Dancing Script R is 616 units at wght 700,
        // Orbitron N is 832 units at wght 900. Chromium's measureText reports the same widths.
        using var dancing = Fonts.CreateFont("Dancing Script", 900, 64);
        Assert.Equal(616 * 64 / 1000f, dancing.GetGlyphWidths(dancing.GetGlyphs("R"))[0], 3);
        using var orbitron = Fonts.CreateFont("Orbitron", 900, 72);
        Assert.Equal(832 * 72 / 1000f, orbitron.GetGlyphWidths(orbitron.GetGlyphs("N"))[0], 3);
    }

    [Fact]
    public void Synthetic_bold_keeps_the_advance()
    {
        // Anton R is 976 units of 2048; Chromium measures 30.5 at 64px with synthetic bold on.
        using var anton = Fonts.CreateFont("Anton", 900, 64);
        Assert.Equal(30.5f, anton.GetGlyphWidths(anton.GetGlyphs("R"))[0], 3);
    }

    [Fact]
    public void Canvas_text_settings_match_chromium()
    {
        using var font = Fonts.CreateFont("Poppins", 900, 64);
        Assert.Equal((SKFontEdging.Antialias, true, SKFontHinting.None, true), (font.Edging, font.Subpixel, font.Hinting, font.LinearMetrics));
    }

    [Fact]
    public void An_unknown_family_throws() => Assert.Throws<ArgumentException>(() => Fonts.Get("Comic Sans MS", 900));

    [Fact]
    public void The_build_output_carries_a_byte_identical_copy_of_assets_fonts()
    {
        var repo = Path.Combine(Root, "assets", "fonts");
        static List<string> Listing(string dir) =>
            Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f)).Order(StringComparer.Ordinal).ToList();
        var expected = Listing(repo);
        Assert.Equal(expected, Listing(Fonts.Root));
        foreach (var rel in expected)
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, rel))), SHA256.HashData(File.ReadAllBytes(Path.Combine(Fonts.Root, rel))));
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in `RBX15.Engine.Tests` with `error CS0103: The name 'Fonts' does not exist in the current context` (36 sites) and `error CS0246: The type or namespace name 'Fonts' could not be found` (4 sites).

- [ ] **Step 3: Copy the fonts into every output, add HarfBuzzSharp, implement Fonts**

Replace `src/RBX15.Engine/RBX15.Engine.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SkiaSharp" Version="4.153.1" />
    <PackageReference Include="HarfBuzzSharp" Version="14.2.1.301" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.Engine.Tests" />
    <InternalsVisibleTo Include="RBX15.Parity" />
  </ItemGroup>

  <!-- The vendored fonts (assets/fonts, read-only) ship next to every build output that references
       the engine, at assets/fonts/<family>/<file>. Fonts resolves them from AppContext.BaseDirectory. -->
  <ItemGroup>
    <Content Include="..\..\assets\fonts\**\*" LinkBase="assets\fonts" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

Create `src/RBX15.Engine/Fonts.cs`:

```csharp
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The vendored Word Art and UI faces (assets/fonts, pinned by its README manifest), resolved the way
/// Chromium resolved the @font-face rules 4.1 loaded. tools/parity-ref/fonts.js declares one
/// @font-face per family and per weight 4.1 requests (editor.html:11), and the manifest's "4.1
/// requests" column lists exactly those weights.
/// <list type="bullet">
/// <item>A request picks a declared face by the CSS weight-matching rule (CSS Fonts 4, 5.2).</item>
/// <item>A variable file has its wght axis set to the request clamped to the face's declared weight
/// (Blink FontCustomPlatformData), so a 900 request draws Dancing Script at 700.</item>
/// <item>A request of 600 or more on a face declared below 600 is synthetic bold (Blink
/// CSSSegmentedFontFace sets SyntheticBold when the face's maximum weight is under BoldThreshold),
/// drawn with SkFont embolden. Advances do not change.</item>
/// </list>
/// Files load from <c>assets/fonts</c> under <see cref="AppContext.BaseDirectory"/>. RBX15.Engine.csproj
/// copies them there for every project that references the engine.
/// </summary>
public static partial class Fonts
{
    /// <summary>The Word Art families in the order of 4.1's waFont select (editor.html:230-246).</summary>
    public static readonly IReadOnlyList<string> WordArtFamilies = new[]
    {
        "Anton", "Bebas Neue", "Dancing Script", "Great Vibes", "Pacifico", "Creepster", "Poppins", "Lato",
        "Bangers", "Permanent Marker", "Bungee Shade", "Rubik Mono One", "Press Start 2P", "Righteous", "Orbitron",
    };

    /// <summary>Blink's BoldThreshold: a request at or above it on a lighter face is emboldened.</summary>
    public const int BoldThreshold = 600;

    /// <summary>One declared face: a family, the weight its @font-face declares, and its file under <see cref="Root"/>.</summary>
    internal sealed record Face(string Family, int Weight, string File);

    /// <summary>The directory holding the manifest (README.md) and the font files.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");

    static readonly Lazy<IReadOnlyList<Face>> Declared = new(() => ParseManifest(File.ReadAllText(Path.Combine(Root, "README.md"))));
    static readonly ConcurrentDictionary<string, byte[]> Files = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<Face, SKTypeface> Typefaces = new();

    internal static readonly uint WghtTag = 'w' << 24 | 'g' << 16 | 'h' << 8 | 't';

    [GeneratedRegex(@"^\| `(?<file>[^`]+\.ttf)` \| (?<family>[^|]+?) \| (?<weights>[0-9]+(?:, [0-9]+)*) \|", RegexOptions.Multiline)]
    private static partial Regex FaceRow();

    /// <summary>One face per weight listed in each font row's "4.1 requests" cell.</summary>
    internal static IReadOnlyList<Face> ParseManifest(string readme)
    {
        var faces = new List<Face>();
        foreach (Match m in FaceRow().Matches(readme.Replace("\r\n", "\n")))
            foreach (var w in m.Groups["weights"].Value.Split(", "))
                faces.Add(new Face(m.Groups["family"].Value, int.Parse(w, CultureInfo.InvariantCulture), m.Groups["file"].Value));
        return faces;
    }

    /// <summary>
    /// The declared face a request matches (CSS Fonts 4, 5.2, font-weight): above 500, the lightest
    /// at or above it, else the heaviest below; 400 to 500, the lightest from it up to 500, else the
    /// heaviest below, else the lightest above 500; below 400, the heaviest at or below it, else the
    /// lightest above.
    /// </summary>
    internal static Face Match(string family, int weight)
    {
        var faces = Declared.Value.Where(f => f.Family == family).ToList();
        if (faces.Count == 0) throw new ArgumentException($"no vendored font family '{family}'", nameof(family));
        Face? Lightest(Func<int, bool> p) => faces.Where(f => p(f.Weight)).MinBy(f => f.Weight);
        Face? Heaviest(Func<int, bool> p) => faces.Where(f => p(f.Weight)).MaxBy(f => f.Weight);
        if (weight > 500) return Lightest(w => w >= weight) ?? Heaviest(w => w < weight)!;
        if (weight >= 400) return Lightest(w => w >= weight && w <= 500) ?? Heaviest(w => w < weight) ?? Lightest(w => w > 500)!;
        return Heaviest(w => w <= weight) ?? Lightest(w => w > weight)!;
    }

    /// <summary>True when Chromium draws this request with synthetic bold.</summary>
    public static bool IsSyntheticBold(string family, int weight) =>
        weight >= BoldThreshold && Match(family, weight).Weight < BoldThreshold;

    /// <summary>
    /// The typeface for a request: the matched file, its wght axis (when it has one) at the face's
    /// declared weight. Cache-owned; callers must not dispose.
    /// </summary>
    public static SKTypeface Get(string family, int weight) => Typefaces.GetOrAdd(Match(family, weight), face =>
    {
        using var data = SKData.CreateCopy(FileBytes(face.File));
        var typeface = SKTypeface.FromData(data) ?? throw new InvalidDataException($"not a font: {face.File}");
        if (!typeface.VariationDesignParameters.Any(a => a.Tag == WghtTag)) return typeface;
        var instanced = typeface.Clone(new SKFontArguments
        {
            VariationDesignPosition = new[] { new SKFontVariationPositionCoordinate { Axis = WghtTag, Value = face.Weight } },
        });
        typeface.Dispose();
        return instanced;
    });

    /// <summary>The wght axis value a request draws at, or null for a static file.</summary>
    internal static float? AxisWeight(string family, int weight) =>
        Get(family, weight).VariationDesignParameters.Any(a => a.Tag == WghtTag) ? Match(family, weight).Weight : null;

    /// <summary>The matched file's bytes, which HarfBuzz shapes from. Cache-owned.</summary>
    internal static byte[] FileBytes(string family, int weight) => FileBytes(Match(family, weight).File);

    static byte[] FileBytes(string file) =>
        Files.GetOrAdd(file, f => File.ReadAllBytes(Path.Combine(Root, f.Replace('/', Path.DirectorySeparatorChar))));

    /// <summary>
    /// The SkFont Chromium's canvas draws a request with (Blink FontPlatformData::SetupSkFont): the
    /// typeface and size, embolden for synthetic bold, grayscale antialiasing (canvas text on a
    /// transparent canvas is never LCD), subpixel positioning, no hinting and linear metrics.
    /// Caller disposes.
    /// </summary>
    public static SKFont CreateFont(string family, int weight, float size) => new(Get(family, weight), size)
    {
        Embolden = IsSyntheticBold(family, weight),
        Edging = SKFontEdging.Antialias,
        Subpixel = true,
        Hinting = SKFontHinting.None,
        LinearMetrics = true,
    };
}
```

- [ ] **Step 4: Run the Fonts tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `ls src/RBX15.Engine.Tests/bin/Release/net10.0/assets/fonts`
Expected: `README.md` and the 18 family directories (`anton` through `spacegrotesk`).

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.FontsTests"`
Expected: `Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32` for `RBX15.Engine.Tests.dll` (1 manifest, 1 families, 9 weight-match rows, 15 synthetic-bold rows, 6 facts).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 421, Failed: 0, Skipped: 22` (389 + 32); Parity.Tests `Passed: 31, Failed: 0`.

If `Every_word_art_family_resolves_at_900` fails with a family name mismatch, the typeface came from a system font, not the vendored file: check that `Fonts.Root` points into the test output and that the build copied `assets/fonts`. If `Variable_files_draw_at_the_declared_weight` reports a Dancing Script R narrower than 39.424, the wght instance was not applied: `Get` must `Clone` with `SKFontArguments`.

- [ ] **Step 5: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/RBX15.Engine.csproj src/RBX15.Engine/Fonts.cs src/RBX15.Engine.Tests/FontsTests.cs
git commit -m "feat(engine): Fonts resolves the vendored faces as Chromium did, and ships them with every build"
```

- [ ] **Step 6: Write the failing WordArt tests**

Create `src/RBX15.Engine.Tests/WordArtTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// renderWordArt layout and Add to canvas (editor.js:2120-2291). The widths, middle offsets and
/// canvas sizes are what 4.1 itself measured and returned for these settings: renderWordArt and
/// measureText run in the real editor.html under Electron 41 with the vendored fonts (the goldens-v2
/// capture setup), 2026-10-14.
/// </summary>
public class WordArtTests
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa), Black = SKColors.Black, Pink = new(0xf2, 0x2f, 0x89);

    static WordArtSettings S(string font = "Anton", WordArtStyle style = WordArtStyle.Solid, string text = "RIZZ", float size = 64,
                             float strokeWidth = 3, float angle = 0, SKColor? stroke = null) =>
        new(text, font, style, size, Cyan, stroke ?? Black, Pink, strokeWidth, angle);

    static byte[] Pixels(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixelSpan().ToArray();
    }

    static byte[] RenderPixels(WordArtSettings s)
    {
        using var image = WordArt.Render(s).Image;
        return Pixels(image);
    }

    [Theory]
    [InlineData("Anton", 64, "RIZZ", 97.5)]
    [InlineData("Poppins", 64, "RIZZ", 144.128)]
    [InlineData("Dancing Script", 64, "RIZZ", 158.272)] // includes the Z-Z GPOS kern of -6 units at wght 700
    [InlineData("Orbitron", 72, "NO", 119.52)]
    [InlineData("Orbitron", 72, "CAP", 176.328)]
    public void Lines_are_shaped_to_chromiums_measureText_width(string font, float size, string text, double width) =>
        Assert.Equal(width, WordArt.Shape(font, size, text).Width, 3);

    [Theory]
    [InlineData("Anton", 64, 18.015625)]
    [InlineData("Poppins", 64, 16)]
    [InlineData("Dancing Script", 64, 17.0625)]
    [InlineData("Orbitron", 72, 22.046875)]
    public void Middle_baseline_is_the_normalized_typo_metrics_in_layout_units(string font, float size, float offset) =>
        Assert.Equal(offset, WordArt.MiddleOffset(Fonts.Get(font, WordArt.Weight), size));

    [Theory]
    [InlineData(WordArtStyle.Solid, 3, 10)]
    [InlineData(WordArtStyle.Gradient, 3, 10)]
    [InlineData(WordArtStyle.Outline, 0, 10)]
    [InlineData(WordArtStyle.Neon, 3, 30)]
    [InlineData(WordArtStyle.Shadow, 3, 15)]
    [InlineData(WordArtStyle.Retro, 3, 12)]
    [InlineData(WordArtStyle.Solid, 12, 24)]
    [InlineData(WordArtStyle.Neon, 20, 40)]
    public void Pad_is_the_largest_effect_allowance(WordArtStyle style, float strokeWidth, double pad) =>
        Assert.Equal(pad, WordArt.Pad(style, strokeWidth));

    [Theory]
    [InlineData("Anton", WordArtStyle.Solid, "RIZZ", 64, 3, 118, 94)]
    [InlineData("Anton", WordArtStyle.Neon, "RIZZ", 64, 3, 158, 134)]
    [InlineData("Anton", WordArtStyle.Shadow, "RIZZ", 64, 3, 128, 104)]
    [InlineData("Anton", WordArtStyle.Retro, "RIZZ", 64, 3, 122, 98)]
    [InlineData("Anton", WordArtStyle.Solid, "RIZZ", 64, 12, 146, 122)]
    [InlineData("Poppins", WordArtStyle.Solid, "RIZZ", 64, 3, 165, 94)]
    [InlineData("Dancing Script", WordArtStyle.Outline, "RIZZ", 64, 0, 179, 94)]
    [InlineData("Orbitron", WordArtStyle.Solid, "NO\nCAP", 72, 3, 197, 186)]
    public void Canvas_size_is_4_1s_cw_by_ch(string font, WordArtStyle style, string text, float size, float strokeWidth, int cw, int ch)
    {
        var (image, w, h) = WordArt.Render(S(font, style, text, size, strokeWidth));
        using (image)
        {
            Assert.Equal((cw, ch), (w, h));
            Assert.Equal((cw, ch), (image.Width, image.Height));
        }
    }

    [Fact]
    public void The_preview_clamps_the_canvas_and_scales_the_content()
    {
        var (image, w, h) = WordArt.Render(S("Poppins"), 200, 80);
        using (image)
        {
            Assert.Equal((165, 94), (w, h));
            Assert.Equal((165, 80), (image.Width, image.Height));
        }
    }

    [Fact]
    public void Negative_maxima_are_rejected() => Assert.Throws<ArgumentOutOfRangeException>(() => WordArt.Render(S(), -1, 80));

    [Fact]
    public void Empty_text_renders_TEXT() => Assert.Equal(RenderPixels(S(text: "TEXT")), RenderPixels(S(text: "")));

    [Theory]
    [InlineData(0, 64)]
    [InlineData(float.NaN, 64)]
    [InlineData(2, 8)]
    [InlineData(5000, 300)]
    [InlineData(64, 64)]
    public void Size_falls_back_to_64_and_clamps_to_8_through_300(float size, float effective) =>
        Assert.Equal(effective, WordArt.EffectiveSize(size));

    [Fact]
    public void Outline_with_stroke_width_0_draws_at_3() =>
        Assert.Equal(RenderPixels(S("Dancing Script", WordArtStyle.Outline, strokeWidth: 3)),
                     RenderPixels(S("Dancing Script", WordArtStyle.Outline, strokeWidth: 0)));

    [Fact] // editor.js:2226: strokeW 0 is fill only, so the stroke color cannot reach the pixels
    public void Solid_with_stroke_width_0_draws_no_stroke()
    {
        Assert.Equal(RenderPixels(S(strokeWidth: 0, stroke: SKColors.White)), RenderPixels(S(strokeWidth: 0, stroke: Black)));
        Assert.NotEqual(RenderPixels(S(strokeWidth: 3, stroke: SKColors.White)), RenderPixels(S(strokeWidth: 3, stroke: Black)));
    }

    [Fact]
    public void Neon_ignores_the_stroke_settings() =>
        Assert.Equal(RenderPixels(S(style: WordArtStyle.Neon, strokeWidth: 3, stroke: SKColors.White)),
                     RenderPixels(S(style: WordArtStyle.Neon, strokeWidth: 12, stroke: Black)));

    [Fact]
    public void The_angle_only_moves_the_gradient()
    {
        Assert.Equal(RenderPixels(S(angle: 0)), RenderPixels(S(angle: 135)));
        Assert.NotEqual(RenderPixels(S(style: WordArtStyle.Gradient, angle: 0)), RenderPixels(S(style: WordArtStyle.Gradient, angle: 135)));
    }

    [Fact]
    public void A_trailing_newline_adds_an_empty_line()
    {
        var (image, w, h) = WordArt.Render(S("Orbitron", text: "NO\n", size: 72));
        using (image)
            Assert.Equal((140, (int)Math.Ceiling(2 * 72 * 1.15 + 20)), (w, h)); // 119.52 + 20 wide; two lines of 82.8
    }

    [Theory]
    [InlineData("RIZZ", "Anton", "\u2728 RIZZ [Anton]")]
    [InlineData("", "Anton", "\u2728 TEXT [Anton]")]
    [InlineData("NO\nCAP", "Orbitron", "\u2728 NO\nCAP [Orbitron]")]
    [InlineData("HELLO WORLD THIS IS LONG TEXT", "Poppins", "\u2728 HELLO WORLD THIS IS  [Poppins]")]
    public void Layer_names_follow_4_1(string text, string font, string name) => Assert.Equal(name, WordArt.LayerName(text, font));

    [Theory]
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void Add_to_canvas_fits_85_percent_of_the_front_slot_and_centers(TemplateMode mode)
    {
        // Poppins RIZZ renders 165x94 (above). The front slot is 128x128 at (231, 74) in both modes, so
        // the scale is 128/165*0.85, X = 231 + (128 - 165*scale)/2 and Y = 74 + (128 - 94*scale)/2.
        var layer = WordArt.CreateLayer(S("Poppins"), mode, 7);
        Assert.Equal(7, layer.Id);
        Assert.Equal("\u2728 RIZZ [Poppins]", layer.Name);
        Assert.Equal((165, 94), (layer.Image.Width, layer.Image.Height));
        Assert.Equal(SKAlphaType.Unpremul, layer.Image.AlphaType);
        Assert.Equal(0.6593939393939394, layer.Scale, 12);
        Assert.Equal(240.6, layer.X, 9);
        Assert.Equal(107.00848484848485, layer.Y, 9);
        Assert.Equal((true, 1.0, 0.0, false, false), (layer.Visible, layer.Opacity, layer.Rotation, layer.FlipH, layer.FlipV));
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate));
        Assert.Equal((0, 0, 0), (layer.RShift, layer.GShift, layer.BShift));
    }
}
```

- [ ] **Step 7: Run them and see them fail**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in `RBX15.Engine.Tests` with `error CS0103: The name 'WordArtStyle' does not exist in the current context` and `error CS0246: The type or namespace name 'WordArtSettings' could not be found` (and `'WordArtStyle'`).

- [ ] **Step 8: Implement WordArt**

Create `src/RBX15.Engine/WordArt.cs`:

```csharp
using SkiaSharp;
using HB = HarfBuzzSharp;

namespace RBX15.Engine;

/// <summary>The six style buttons (editor.html:269-274, data-style solid..retro).</summary>
public enum WordArtStyle { Solid, Gradient, Neon, Shadow, Outline, Retro }

/// <summary>
/// The Word Art controls (editor.html:229-262): text, font family, style, size, fill (waColor),
/// stroke (waStroke), gradient end (waColor2), stroke width (waStrokeW) and gradient angle in
/// degrees (waAngle).
/// </summary>
public sealed record WordArtSettings(string Text, string Font, WordArtStyle Style, float Size, SKColor Fill, SKColor Stroke,
                                     SKColor Grad, float StrokeWidth, float Angle);

/// <summary>
/// The Word Art generator: renderWordArt (editor.js:2120-2240) and Add to canvas (editor.js:2257-2291).
/// Lines are shaped with HarfBuzz as Blink shapes them (kerning included) and drawn as text blobs
/// with the SkFont Chromium's canvas uses (<see cref="Fonts.CreateFont"/>), on a kN32 premultiplied
/// surface like every other engine draw.
/// </summary>
public static class WordArt
{
    /// <summary>4.1 always asks for weight 900 (editor.js:2131).</summary>
    public const int Weight = 900;

    /// <summary>The waSize input's min and max (editor.html:256), enforced (approved divergence; 4.1 accepts any typed value).</summary>
    public const float MinSize = 8, MaxSize = 300;

    /// <summary>One shaped line: glyph ids, each glyph's x from the line's left edge, and the advance width.</summary>
    internal sealed record ShapedLine(ushort[] Glyphs, float[] Xs, float Width);

    /// <summary>waText || 'TEXT' (editor.js:2121, :2270).</summary>
    internal static string EffectiveText(string text) => text.Length == 0 ? "TEXT" : text;

    /// <summary>+waSize || 64 (editor.js:2125), then clamped to 8..300.</summary>
    internal static float EffectiveSize(float size) => float.IsNaN(size) || size == 0 ? 64 : Math.Clamp(size, MinSize, MaxSize);

    /// <summary>pad (editor.js:2145): max(strokeW*2, neon 30 else 10, shadow 15, retro 12).</summary>
    internal static double Pad(WordArtStyle style, float strokeWidth) =>
        Math.Max(Math.Max(strokeWidth * 2.0, style == WordArtStyle.Neon ? 30 : 10),
                 Math.Max(style == WordArtStyle.Shadow ? 15 : 0, style == WordArtStyle.Retro ? 12 : 0));

    /// <summary>The Outline style's line width: strokeW || 3 (editor.js:2217).</summary>
    internal static float OutlineWidth(float strokeWidth) => strokeWidth == 0 || float.IsNaN(strokeWidth) ? 3 : strokeWidth;

    /// <summary>
    /// What textBaseline 'middle' adds to y (Blink TextMetrics::GetFontBaseline): half of ascent minus
    /// descent, where the OS/2 typo ascent and descent are scaled to sum to the font size keeping their
    /// ratio, each rounded to a LayoutUnit (1/64 px) (SimpleFontData::NormalizeEmHeightMetrics).
    /// </summary>
    internal static float MiddleOffset(SKTypeface typeface, float size)
    {
        var os2 = typeface.GetTableData(Os2Tag);
        if (os2 is null || os2.Length < 72) throw new InvalidDataException($"{typeface.FamilyName} has no OS/2 typo metrics");
        float ascent = (short)(os2[68] << 8 | os2[69]), descent = -(short)(os2[70] << 8 | os2[71]);
        float emAscent = ascent * size / (ascent + descent);
        return (LayoutUnit(emAscent) - LayoutUnit(size - emAscent)) / 2;
    }

    static float LayoutUnit(float v) => MathF.Round(v * 64, MidpointRounding.AwayFromZero) / 64;

    static readonly uint Os2Tag = 'O' << 24 | 'S' << 16 | '/' << 8 | '2';

    /// <summary>
    /// Shapes one line with HarfBuzz at the font size in 16.16 fixed point and the request's wght, as
    /// Blink's HarfBuzzShaper does; advances and offsets come back as floats summed left to right.
    /// </summary>
    internal static ShapedLine Shape(string family, float size, string line)
    {
        using var blob = HB.Blob.FromStream(new MemoryStream(Fonts.FileBytes(family, Weight), writable: false));
        using var face = new HB.Face(blob, 0);
        using var font = new HB.Font(face);
        int scale = (int)(size * 65536);
        font.SetScale(scale, scale);
        if (Fonts.AxisWeight(family, Weight) is { } wght)
            font.SetVariations(new[] { new HB.Variation { Tag = Fonts.WghtTag, Value = wght } });
        using var buffer = new HB.Buffer();
        buffer.AddUtf16(line);
        buffer.GuessSegmentProperties();
        font.Shape(buffer);
        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var glyphs = new ushort[infos.Length];
        var xs = new float[infos.Length];
        float pen = 0;
        for (int i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            xs[i] = pen + positions[i].XOffset / 65536f;
            pen += positions[i].XAdvance / 65536f;
        }
        return new ShapedLine(glyphs, xs, pen);
    }

    /// <summary>
    /// renderWordArt (editor.js:2120-2240) on a new surface. With no maxima (null or 0, as 4.1's falsy
    /// check) the surface is the content size cw x ch, as Add to canvas renders it; with maxima it is
    /// min(cw, max) x min(ch, max) and the content is scaled by the smaller ratio, as the preview
    /// renders it. Returns the image (caller owns it) and cw, ch.
    /// </summary>
    public static (SKImage Image, int Width, int Height) Render(WordArtSettings s, int? maxWidth = null, int? maxHeight = null)
    {
        if (maxWidth < 0 || maxHeight < 0) throw new ArgumentOutOfRangeException(nameof(maxWidth), "maxima are positive, or null or 0 for none");
        string text = EffectiveText(s.Text);
        float size = EffectiveSize(s.Size);
        using var font = Fonts.CreateFont(s.Font, Weight, size);

        // editor.js:2135-2154, in JS doubles.
        var lines = text.Split('\n').Select(l => Shape(s.Font, size, l)).ToList();
        double lineHeight = size * 1.15;
        double totalW = lines.Max(l => (double)l.Width);
        double totalH = lines.Count * lineHeight;
        double pad = Pad(s.Style, s.StrokeWidth);
        int cw = (int)Math.Ceiling(totalW + pad * 2);
        int ch = (int)Math.Ceiling(totalH + pad * 2);
        int width = maxWidth is > 0 ? Math.Min(cw, maxWidth.Value) : cw;
        int height = maxHeight is > 0 ? Math.Min(ch, maxHeight.Value) : ch;
        double sc = Math.Min((double)width / cw, (double)height / ch);

        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create word art surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)sc, (float)sc);
        float middle = MiddleOffset(font.Typeface, size);
        double cx = cw / 2.0;
        double startY = pad + lineHeight / 2;
        for (int li = 0; li < lines.Count; li++)
            DrawLine(canvas, font, lines[li], s, cx, startY + li * lineHeight, middle, cw, ch);
        return (surface.Snapshot(), cw, ch);
    }

    /// <summary>One line's passes (editor.js:2166-2236).</summary>
    static void DrawLine(SKCanvas canvas, SKFont font, ShapedLine line, WordArtSettings s, double cx, double ty, float middle, int cw, int ch)
    {
        if (line.Glyphs.Length == 0) return;
        using var builder = new SKTextBlobBuilder();
        builder.AddHorizontalRun(line.Glyphs, font, line.Xs, 0);
        using var blob = builder.Build();

        // fillText/strokeText(line, x, y) with textAlign center and textBaseline middle: Blink takes x
        // and y as floats, moves left by half the width and down by the middle offset.
        void Text(double x, double y, SKPaint paint) => canvas.DrawText(blob, (float)x - line.Width / 2, (float)y + middle, paint);

        // editor.js:2171-2179: the gradient spans the whole canvas around each line's center.
        using var gradient = s.Style == WordArtStyle.Gradient ? Gradient(s, cx, ty, cw, ch) : null;

        if (s.Style == WordArtStyle.Shadow)
        {
            // editor.js:2182-2188: rgba(0,0,0,0.6) at +4,+4, no blur.
            using var shadow = FillPaint(new SKColor(0, 0, 0, 153));
            Text(cx + 4, ty + 4, shadow);
        }

        if (s.Style == WordArtStyle.Retro)
        {
            // editor.js:2190-2196: 8 copies, deepest first, even offsets in the stroke color.
            for (int i = 8; i > 0; i--)
            {
                using var copy = FillPaint(i % 2 == 0 ? s.Stroke : s.Grad);
                Text(cx + i, ty + i, copy);
            }
        }

        if (s.Style == WordArtStyle.Neon)
        {
            // editor.js:2198-2213: the fill twice with a glow of the fill color (blur 20, then 40),
            // then a white core at globalAlpha 0.8. Stroke settings are ignored.
            using var glow = FillPaint(s.Fill);
            DrawWithShadow(s.Fill, 20, glow, p => Text(cx, ty, p));
            DrawWithShadow(s.Fill, 40, glow, p => Text(cx, ty, p));
            using var core = new SKPaint { ColorF = new SKColorF(1, 1, 1, 0.8f), IsAntialias = true };
            Text(cx, ty, core);
        }
        else if (s.Style == WordArtStyle.Outline)
        {
            // editor.js:2214-2219: stroked in the fill color, strokeW || 3, round joins.
            using var outline = StrokePaint(s.Fill, OutlineWidth(s.StrokeWidth));
            Text(cx, ty, outline);
        }
        else
        {
            // editor.js:2220-2235: fill, then stroke and fill again when strokeW > 0.
            using var fill = FillPaint(s.Fill);
            if (gradient is not null)
            {
                fill.Shader = gradient;
                fill.IsDither = true; // Chromium dithers canvas gradients; measured against the goldens.
            }
            Text(cx, ty, fill);
            if (s.StrokeWidth > 0)
            {
                using var stroke = StrokePaint(s.Stroke, s.StrokeWidth);
                Text(cx, ty, stroke);
                Text(cx, ty, fill);
            }
        }

        // A draw with shadowColor and shadowBlur set (Blink's shadow draw looper): the shadow first,
        // the same draw recolored src-in to the shadow color under a normal blur of sigma blur/2 that
        // ignores the canvas transform, then the draw itself.
        static void DrawWithShadow(SKColor color, float blur, SKPaint paint, Action<SKPaint> draw)
        {
            using var shadow = paint.Clone();
            using var recolor = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
            using var blurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur / 2, false);
            shadow.ColorFilter = recolor;
            shadow.MaskFilter = blurFilter;
            draw(shadow);
            draw(paint);
        }
    }

    static SKShader Gradient(WordArtSettings s, double cx, double ty, int cw, int ch)
    {
        double angle = s.Angle * Math.PI / 180;
        double gx = Math.Cos(angle) * cw / 2, gy = Math.Sin(angle) * ch / 2;
        return SKShader.CreateLinearGradient(new SKPoint((float)(cx - gx), (float)(ty - gy)), new SKPoint((float)(cx + gx), (float)(ty + gy)),
            new[] { s.Fill, s.Grad }, null, SKShaderTileMode.Clamp);
    }

    static SKPaint FillPaint(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>strokeText state: lineJoin round (editor.js:2218, :2230), canvas-default butt cap and miter limit 10.</summary>
    static SKPaint StrokePaint(SKColor color, float width) => new()
    {
        Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
        StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Butt, StrokeMiter = 10,
    };

    /// <summary>The layer name (editor.js:2276): a sparkle, the first 20 UTF-16 units of the text, the font family.</summary>
    public static string LayerName(string text, string font)
    {
        var t = EffectiveText(text);
        return "\u2728 " + (t.Length > 20 ? t[..20] : t) + " [" + font + "]";
    }

    /// <summary>
    /// Add to canvas (editor.js:2257-2291): the full-size render as a new layer, scaled to 85% of its
    /// fit in the front slot and centered there (editor.js:2272-2279). Template.Slot makes this work in
    /// Pants mode, where 4.1 throws (ledger mode-pants-crash-hardcoded-keys). The image is stored
    /// un-premultiplied, the form 4.1's toDataURL and img decode leave it in; every other field is a
    /// makeLayer default.
    /// </summary>
    public static Layer CreateLayer(WordArtSettings s, TemplateMode mode, int id)
    {
        var (rendered, _, _) = Render(s);
        using (rendered)
        {
            var info = new SKImageInfo(rendered.Width, rendered.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bmp = new SKBitmap(info);
            if (!rendered.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
                throw new InvalidOperationException("cannot read word art pixels");
            var front = Template.Slot(mode, BodyPart.Body, Face.Front);
            double scale = Math.Min((double)front.W / rendered.Width, (double)front.H / rendered.Height) * 0.85;
            return new Layer
            {
                Id = id,
                Name = LayerName(s.Text, s.Font),
                Image = SKImage.FromPixelCopy(bmp.PeekPixels()),
                X = front.X + (front.W - rendered.Width * scale) / 2,
                Y = front.Y + (front.H - rendered.Height * scale) / 2,
                Scale = scale,
            };
        }
    }
}
```

- [ ] **Step 9: Run the WordArt tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.WordArtTests"`
Expected: `Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 465, Failed: 0, Skipped: 22` (421 + 44); Parity.Tests `Passed: 31, Failed: 0`.

If a layout test fails, compare with the cited line. In order of likelihood:

- **A width off by 0.384 on Dancing Script only.** Shaping lost the variation: `Shape` must `SetVariations` with wght 700 before `Shape`, or the GPOS kern is read at the default instance.
- **A middle offset off by 1/64.** The LayoutUnit rounding: `MathF.Round(v * 64, MidpointRounding.AwayFromZero) / 64` on both halves, then halve the difference.
- **A canvas size off by one.** The layout ran in `float`: `totalW`, `pad`, `lineHeight` and the `ceil` are doubles, as in JS.
- **Placement off by a fraction.** `CreateLayer`'s scale and center are doubles from the image size; `Layer.X`/`Y`/`Scale` are doubles.

- [ ] **Step 10: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/WordArt.cs src/RBX15.Engine.Tests/WordArtTests.cs
git commit -m "feat(engine): WordArt renders the six 4.1 styles with HarfBuzz-shaped text and adds the layer"
```

- [ ] **Step 11: Write the failing fallback tests (ruling R3)**

Ruling R3: a character the vendored family lacks draws in a system face, as Chromium draws it (4.1's font string ends in `, sans-serif`, `editor.js:2131`), never as the family's `.notdef` glyph. Text the vendored face covers must shape exactly as Step 8 shapes it.

In `src/RBX15.Engine.Tests/WordArtTests.cs`, insert these two facts directly after `Lines_are_shaped_to_chromiums_measureText_width` (after its `Assert.Equal(width, WordArt.Shape(font, size, text).Width, 3);` line):

```csharp
    [Fact]
    public void Plain_text_shapes_as_one_run_in_the_vendored_face()
    {
        var line = WordArt.Shape("Dancing Script", 64, "RIZZ");
        var run = Assert.Single(line.Runs);
        Assert.True(run.Primary);
        Assert.Same(Fonts.Get("Dancing Script", WordArt.Weight), run.Typeface);
        Assert.Equal(4, run.Glyphs.Length);
    }

    [Fact]
    public void A_character_the_family_lacks_is_shaped_in_a_system_fallback_face()
    {
        // 4.1's font string ends in ", sans-serif" (editor.js:2131), so Chromium draws a character the
        // family lacks in a system face, never the family's .notdef glyph (id 0).
        var a = WordArt.Shape("Anton", 64, "A");
        var line = WordArt.Shape("Anton", 64, "A\U0001F525");
        Assert.Equal(2, line.Runs.Count);
        Assert.True(line.Runs[0].Primary);
        Assert.Equal("Anton", line.Runs[0].Typeface.FamilyName);
        var fire = line.Runs[1];
        Assert.False(fire.Primary);
        Assert.NotEqual("Anton", fire.Typeface.FamilyName);
        using (var fallback = new SKFont(fire.Typeface))
            Assert.True(fallback.ContainsGlyph(0x1F525));
        Assert.All(line.Runs, r => Assert.DoesNotContain((ushort)0, r.Glyphs));
        Assert.Equal(a.Width, fire.Xs[0], 3); // the fallback run continues the pen
        Assert.True(line.Width > a.Width);
    }
```

- [ ] **Step 12: Run them and see them fail**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in `RBX15.Engine.Tests` with `error CS1061: 'WordArt.ShapedLine' does not contain a definition for 'Runs'` at 6 sites (`6 Error(s)`).

- [ ] **Step 13: Split each line into runs by font coverage**

Make five exact replacements in `src/RBX15.Engine/WordArt.cs` (Edit tool). A cluster whose first character the vendored face covers stays in it; any other cluster takes the face `SKFontManager.Default.MatchCharacter` picks for the family at weight 900, and each run is shaped with HarfBuzz from that face's bytes. `SKTypeface.ContainsGlyph` is obsolete in 4.153.1, so coverage goes through `SKFont.ContainsGlyph`.

1. The usings at the top of the file. Replace:

   ```csharp
   using SkiaSharp;
   using HB = HarfBuzzSharp;
   ```

   with:

   ```csharp
   using System.Collections.Concurrent;
   using System.Globalization;
   using System.Text;
   using SkiaSharp;
   using HB = HarfBuzzSharp;
   ```

2. The `ShapedLine` record. Replace:

   ```csharp
       /// <summary>One shaped line: glyph ids, each glyph's x from the line's left edge, and the advance width.</summary>
       internal sealed record ShapedLine(ushort[] Glyphs, float[] Xs, float Width);
   ```

   with:

   ```csharp
       /// <summary>
       /// One run of a shaped line, all in one face: the vendored face (<c>Primary</c>) or the system face
       /// a character the family lacks falls back to. Glyph ids, and each glyph's x from the line's left edge.
       /// </summary>
       internal sealed record ShapedRun(SKTypeface Typeface, bool Primary, ushort[] Glyphs, float[] Xs);

       /// <summary>One shaped line: its runs in text order, and the advance width.</summary>
       internal sealed record ShapedLine(IReadOnlyList<ShapedRun> Runs, float Width);

       /// <summary>A system face a missing character falls back to, with the bytes HarfBuzz shapes from.</summary>
       internal sealed record FallbackFace(SKTypeface Typeface, byte[] Bytes, int Index);

       static readonly ConcurrentDictionary<(string Family, int Codepoint), FallbackFace?> FallbackByCharacter = new();
       static readonly ConcurrentDictionary<string, FallbackFace> FallbackFaces = new(StringComparer.Ordinal);

       /// <summary>The style 4.1 asks for (editor.js:2131): weight 900, normal width, upright.</summary>
       static readonly SKFontStyle RequestStyle = new(SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
   ```

3. `Shape`. Replace:

   ```csharp
       /// <summary>
       /// Shapes one line with HarfBuzz at the font size in 16.16 fixed point and the request's wght, as
       /// Blink's HarfBuzzShaper does; advances and offsets come back as floats summed left to right.
       /// </summary>
       internal static ShapedLine Shape(string family, float size, string line)
       {
           using var blob = HB.Blob.FromStream(new MemoryStream(Fonts.FileBytes(family, Weight), writable: false));
           using var face = new HB.Face(blob, 0);
           using var font = new HB.Font(face);
           int scale = (int)(size * 65536);
           font.SetScale(scale, scale);
           if (Fonts.AxisWeight(family, Weight) is { } wght)
               font.SetVariations(new[] { new HB.Variation { Tag = Fonts.WghtTag, Value = wght } });
           using var buffer = new HB.Buffer();
           buffer.AddUtf16(line);
           buffer.GuessSegmentProperties();
           font.Shape(buffer);
           var infos = buffer.GetGlyphInfoSpan();
           var positions = buffer.GetGlyphPositionSpan();
           var glyphs = new ushort[infos.Length];
           var xs = new float[infos.Length];
           float pen = 0;
           for (int i = 0; i < infos.Length; i++)
           {
               glyphs[i] = (ushort)infos[i].Codepoint;
               xs[i] = pen + positions[i].XOffset / 65536f;
               pen += positions[i].XAdvance / 65536f;
           }
           return new ShapedLine(glyphs, xs, pen);
       }
   ```

   with:

   ```csharp
       /// <summary>
       /// Shapes one line as Blink's HarfBuzzShaper does. The line splits into runs by font coverage, one
       /// grapheme cluster at a time: a cluster whose first character the vendored face covers stays in it,
       /// and any other falls back to the system face SKFontManager.MatchCharacter picks for the family at
       /// weight 900, as Chromium falls back (4.1's font string ends in ", sans-serif", editor.js:2131).
       /// Each run is shaped with HarfBuzz at the font size in 16.16 fixed point, the vendored run at the
       /// request's wght; advances and offsets come back as floats summed left to right. Text the vendored
       /// face covers is one run, shaped exactly as one HarfBuzz call over the whole line.
       /// </summary>
       internal static ShapedLine Shape(string family, float size, string line)
       {
           var primary = Fonts.Get(family, Weight);
           var runs = new List<ShapedRun>();
           float pen = 0;
           foreach (var (start, length, fallback) in Segment(primary, family, line))
           {
               var text = line.Substring(start, length);
               var (glyphs, xs, advance) = fallback is null
                   ? ShapeRun(Fonts.FileBytes(family, Weight), 0, Fonts.AxisWeight(family, Weight), size, text, pen)
                   : ShapeRun(fallback.Bytes, fallback.Index, null, size, text, pen);
               runs.Add(new ShapedRun(fallback?.Typeface ?? primary, fallback is null, glyphs, xs));
               pen += advance;
           }
           return new ShapedLine(runs, pen);
       }

       /// <summary>Consecutive grapheme clusters that draw in the same face: start, length, and the fallback face (null for the vendored face).</summary>
       static IEnumerable<(int Start, int Length, FallbackFace? Fallback)> Segment(SKTypeface primary, string family, string line)
       {
           int runStart = 0;
           FallbackFace? runFace = null;
           for (int i = 0; i < line.Length;)
           {
               int length = StringInfo.GetNextTextElementLength(line, i);
               Rune.DecodeFromUtf16(line.AsSpan(i), out var rune, out _);
               var face = Covers(primary, rune.Value) ? null : Fallback(family, rune.Value);
               if (i > runStart && !ReferenceEquals(face, runFace))
               {
                   yield return (runStart, i - runStart, runFace);
                   runStart = i;
               }
               runFace = face;
               i += length;
           }
           if (line.Length > runStart) yield return (runStart, line.Length - runStart, runFace);
       }

       /// <summary>
       /// The system face for a character the vendored family lacks, or null when no installed face has it
       /// (the cluster then stays in the vendored face). Cached per family and character, with one
       /// <see cref="FallbackFace"/> per distinct face, so neighbouring fallback characters share a run.
       /// </summary>
       static FallbackFace? Fallback(string family, int codepoint) => FallbackByCharacter.GetOrAdd((family, codepoint), key =>
       {
           var typeface = SKFontManager.Default.MatchCharacter(key.Family, RequestStyle, null, key.Codepoint);
           if (typeface is null) return null;
           if (!Covers(typeface, key.Codepoint))
           {
               typeface.Dispose();
               return null;
           }
           var id = $"{typeface.FamilyName}|{typeface.FontWeight}|{typeface.FontWidth}|{typeface.FontSlant}";
           var face = FallbackFaces.GetOrAdd(id, _ =>
           {
               using var stream = typeface.OpenStream(out int index) ?? throw new InvalidDataException($"cannot read {typeface.FamilyName}");
               using var data = SKData.Create(stream);
               return new FallbackFace(typeface, data.ToArray(), index);
           });
           if (!ReferenceEquals(face.Typeface, typeface)) typeface.Dispose();
           return face;
       });

       /// <summary>Whether the face has a glyph for the character (SKTypeface.ContainsGlyph is obsolete in 4.153.1).</summary>
       static bool Covers(SKTypeface typeface, int codepoint)
       {
           using var font = new SKFont(typeface);
           return font.ContainsGlyph(codepoint);
       }

       /// <summary>One run through HarfBuzz; its glyph x positions start at <paramref name="pen"/>.</summary>
       static (ushort[] Glyphs, float[] Xs, float Advance) ShapeRun(byte[] fontBytes, int index, float? wght, float size, string text, float pen)
       {
           using var blob = HB.Blob.FromStream(new MemoryStream(fontBytes, writable: false));
           using var face = new HB.Face(blob, index);
           using var font = new HB.Font(face);
           int scale = (int)(size * 65536);
           font.SetScale(scale, scale);
           if (wght is { } w)
               font.SetVariations(new[] { new HB.Variation { Tag = Fonts.WghtTag, Value = w } });
           using var buffer = new HB.Buffer();
           buffer.AddUtf16(text);
           buffer.GuessSegmentProperties();
           font.Shape(buffer);
           var infos = buffer.GetGlyphInfoSpan();
           var positions = buffer.GetGlyphPositionSpan();
           var glyphs = new ushort[infos.Length];
           var xs = new float[infos.Length];
           float x = pen;
           for (int i = 0; i < infos.Length; i++)
           {
               glyphs[i] = (ushort)infos[i].Codepoint;
               xs[i] = x + positions[i].XOffset / 65536f;
               x += positions[i].XAdvance / 65536f;
           }
           return (glyphs, xs, x - pen);
       }
   ```

4. The start of `DrawLine`. Replace:

   ```csharp
           if (line.Glyphs.Length == 0) return;
           using var builder = new SKTextBlobBuilder();
           builder.AddHorizontalRun(line.Glyphs, font, line.Xs, 0);
           using var blob = builder.Build();
   ```

   with:

   ```csharp
           if (line.Runs.Count == 0) return;
           using var builder = new SKTextBlobBuilder();
           foreach (var run in line.Runs)
           {
               if (run.Primary)
               {
                   builder.AddHorizontalRun(run.Glyphs, font, run.Xs, 0);
                   continue;
               }
               using var fallbackFont = FallbackFont(run.Typeface, font.Size);
               builder.AddHorizontalRun(run.Glyphs, fallbackFont, run.Xs, 0);
           }
           using var blob = builder.Build();
   ```

5. The line above `FillPaint`. Replace:

   ```csharp
       static SKPaint FillPaint(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
   ```

   with:

   ```csharp
       /// <summary>
       /// The SkFont for a fallback run: the canvas text settings of <see cref="Fonts.CreateFont"/>, plus
       /// synthetic bold when the system face is lighter than <see cref="Fonts.BoldThreshold"/>, because the
       /// request is 900 (Blink applies the same rule to fallback faces).
       /// </summary>
       static SKFont FallbackFont(SKTypeface typeface, float size) => new(typeface, size)
       {
           Embolden = typeface.FontWeight < Fonts.BoldThreshold,
           Edging = SKFontEdging.Antialias,
           Subpixel = true,
           Hinting = SKFontHinting.None,
           LinearMetrics = true,
       };

       static SKPaint FillPaint(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
   ```

- [ ] **Step 14: Run the WordArt tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.WordArtTests"`
Expected: `Passed!  - Failed:     0, Passed:    46, Skipped:     0, Total:    46` for `RBX15.Engine.Tests.dll`. The 44 tests of Step 9 still pass, so the layout of text the vendored face covers is unchanged.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 467, Failed: 0, Skipped: 22` (465 + 2); Parity.Tests `Passed: 31, Failed: 0`.

If `A_character_the_family_lacks_is_shaped_in_a_system_fallback_face` reports one run, `Covers` is reading the wrong face or `Segment` never splits. If the fallback run holds glyph 0, the face `MatchCharacter` returned lacks the character, and `Fallback` must return null for it. If `Plain_text_shapes_as_one_run_in_the_vendored_face` fails, `Segment` split text the vendored face covers.

- [ ] **Step 15: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/WordArt.cs src/RBX15.Engine.Tests/WordArtTests.cs
git commit -m "feat(engine): WordArt draws characters the family lacks in a system face, as Chromium does"
```

- [ ] **Step 16: Write the failing loader tests**

Create `src/RBX15.Parity.Tests/SceneLoaderWordArtTests.cs`:

```csharp
using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderWordArtTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static SceneLoader.Scene Load(string json) => SceneLoader.Parse(JsonDocument.Parse(json).RootElement, Root);

    [Fact]
    public void An_op_with_no_keys_uses_the_4_1_control_defaults()
    {
        // editor.html:229-262 and waStyle 'solid' (editor.js:2108): RIZZ, Anton, 64, stroke 3.
        var s = Load("""{ "name": "x", "actions": [ { "op": "wordart" } ] }""");
        var layer = Assert.Single(s.Document.Layers);
        Assert.Equal(1, layer.Id);
        Assert.Equal("\u2728 RIZZ [Anton]", layer.Name);
        Assert.Equal((118, 94), (layer.Image.Width, layer.Image.Height));
    }

    [Fact]
    public void Word_art_lands_above_the_scene_layers_with_the_next_id()
    {
        var s = Load("""
            { "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ],
              "actions": [ { "op": "wordart", "text": "NO\nCAP", "font": "Orbitron", "size": 72 } ] }
            """);
        Assert.Equal(new[] { 1, 2 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal("\u2728 NO\nCAP [Orbitron]", s.Document.Layers[1].Name);
        Assert.Equal((197, 186), (s.Document.Layers[1].Image.Width, s.Document.Layers[1].Image.Height));
    }

    [Fact]
    public void Unknown_style_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => Load("""{ "name": "x", "actions": [ { "op": "wordart", "style": "glitter" } ] }"""));

    [Fact]
    public void Every_frozen_wordart_scene_parses()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests", "goldens", "app", "scenes.json")));
        var scenes = doc.RootElement.EnumerateArray()
            .Where(e => e.GetProperty("name").GetString()!.StartsWith("wordart-", StringComparison.Ordinal))
            .Select(e => SceneLoader.Parse(e, Root))
            .ToList();
        Assert.Equal(22, scenes.Count);
        Assert.All(scenes, s => Assert.Single(s.Document.Layers));
    }
}
```

- [ ] **Step 17: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderWordArtTests"`
Expected: `Failed!  - Failed:     4, Passed:     0, Skipped:     0, Total:     4`. Three fail with `System.NotSupportedException : scene ...: ops the engine does not run yet: wordart`, and `Unknown_style_is_rejected` with `Expected: typeof(System.IO.InvalidDataException)` / `Actual: typeof(System.NotSupportedException)`.

- [ ] **Step 18: Enable the op (SupportedOps, handler arm, PendingOps and the skip row in one change)**

The contract's enabling rule: one change adds `wordart` to `SupportedOps` with its handler, removes it from `PendingOps`, and deletes its skip row. The script edits by pattern from the state Task 2 left (shown in Task 2 Step 8), and it stops with an `AssertionError` if an anchor is missing. If that happens, read the two files and report; do not hand-edit around it.

```bash
mkdir -p .superpowers/m2b
cat > .superpowers/m2b/enable-wordart.py <<'PYEOF'
import re

def edit(path, fn):
    # Keep the file's own line endings (core.autocrlf=true leaves CRLF in the working tree).
    nl = "\r\n" if b"\r\n" in open(path, "rb").read() else "\n"
    src = open(path, encoding="utf-8").read()
    out = fn(src)
    open(path, "w", encoding="utf-8", newline=nl).write(out)
    print("edited", path)

def loader(s):
    # 1. "wordart" joins SupportedOps, at the end (after whatever Tasks 1 and 2 added).
    m = re.search(r'(static readonly HashSet<string> SupportedOps = new\(StringComparer\.Ordinal\) \{ )([^}]*)( \};)', s)
    assert m and '"wordart"' not in m.group(2), "SupportedOps not found, or wordart already there"
    ops = [o.strip() for o in m.group(2).split(",")] + ['"wordart"']
    s = s[:m.start()] + m.group(1) + ", ".join(ops) + m.group(3) + s[m.end():]
    # 2. The handler arm, right after the erase arm.
    arm = '                    "erase" => ApplyErase(document, action, eraseStroking),\n'
    assert s.count(arm) == 1, "erase arm not found exactly once"
    s = s.replace(arm, arm + '                    "wordart" => ApplyWordArt(document, action),\n')
    # 3. ApplyWordArt, just above the erase op's doc comment.
    anchor = '    /// <summary>\n    /// Scene v2 erase op'
    assert s.count(anchor) == 1, "erase doc comment not found exactly once"
    s = s.replace(anchor, APPLY_WORDART + anchor)
    return s

APPLY_WORDART = '''    /// <summary>
    /// Scene v2 wordart op: <c>{ "op": "wordart", "text", "font", "style", "size", "fill", "stroke",
    /// "grad", "strokeWidth", "angle" }</c>, every key optional. app-harness.js wordart() sets only
    /// the controls a scene names, so an omitted key keeps the control's 4.1 default
    /// (editor.html:229-262; waStyle 'solid', editor.js:2108), then clicks Add to canvas
    /// (editor.js:2257-2291). The layer goes on top with the next id (nextId++, editor.js:2275).
    /// </summary>
    public static Document ApplyWordArt(Document doc, JsonElement a)
    {
        string Str(string key, string fallback) => a.TryGetProperty(key, out var v) ? v.GetString()! : fallback;
        float Num(string key, float fallback) => a.TryGetProperty(key, out var v) ? v.GetSingle() : fallback;
        var style = Str("style", "solid") switch
        {
            "solid" => WordArtStyle.Solid,
            "gradient" => WordArtStyle.Gradient,
            "neon" => WordArtStyle.Neon,
            "shadow" => WordArtStyle.Shadow,
            "outline" => WordArtStyle.Outline,
            "retro" => WordArtStyle.Retro,
            var other => throw new InvalidDataException($"unknown word art style '{other}'"),
        };
        var settings = new WordArtSettings(Str("text", "RIZZ"), Str("font", "Anton"), style, Num("size", 64),
            CssColor.ToSk(Str("fill", "#17d4fa")), CssColor.ToSk(Str("stroke", "#000000")), CssColor.ToSk(Str("grad", "#f22f89")),
            Num("strokeWidth", 3), Num("angle", 0));
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        return doc with { Layers = doc.Layers.Add(WordArt.CreateLayer(settings, doc.Mode, id)) };
    }

'''

def gate(s):
    const = '    const string WordArtSkip = "op \'wordart\' is not run by the engine yet (Engine WordArt, M2b)";\n'
    row = '    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]\n'
    assert s.count(const) == 1 and s.count(row) == 1, "wordart skip constant or row not found exactly once"
    s = s.replace(const, "").replace(row, "")
    m = re.search(r'static readonly string\[\] PendingOps = \{ ([^}]*) \};', s)
    assert m, "PendingOps not found"
    ops = [o.strip() for o in m.group(1).split(",") if o.strip()]
    assert '"wordart"' in ops, "wordart is not pending"
    ops.remove('"wordart"')
    inner = " " + ", ".join(ops) + " " if ops else " "
    return s[:m.start()] + 'static readonly string[] PendingOps = {' + inner + '};' + s[m.end():]

edit("src/RBX15.Parity/SceneLoader.cs", loader)
edit("src/RBX15.Engine.Tests/GoldenAppTests.cs", gate)
PYEOF
python .superpowers/m2b/enable-wordart.py
git diff --stat
```

Expected:

- `edited src/RBX15.Parity/SceneLoader.cs` and `edited src/RBX15.Engine.Tests/GoldenAppTests.cs`.
- `git diff --stat` lists exactly those two files (`SceneLoaderWordArtTests.cs` is new and untracked).
- `SupportedOps` reads `{ "shape", "erase", "detail", "project", "wordart" }`. The switch arms run erase, wordart, detail, project; the order has no effect on behavior. `PendingOps` reads `{ }`, which compiles, and the accounting fact holds with no skip rows.

The two files then read in full as follows. The change stays uncommitted until Step 23 commits it with the Word Art rule. They are shown with LF, and the working tree keeps CRLF.

`src/RBX15.Parity/SceneLoader.cs` after Task 3:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// <para>
/// Scene format v2 adds an ordered <c>actions</c> array, run after the scene's own layers exist.
/// The loader runs only the ops the engine implements (SupportedOps). A scene holding any other op
/// refuses to parse, so no test ever compares a half-built document against a golden.
/// </para>
/// <para>
/// The public API strokes the eraser once, as the editor does. The internal overloads take an
/// <see cref="EraseStroking"/> so the golden gate can replay 4.1's per-move stroking.
/// </para>
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    /// <summary>Ops the engine runs. Each engine task that implements an op adds it here.</summary>
    static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape", "erase", "detail", "project", "wordart" };

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    /// <summary>The scene's action ops the engine does not run yet, each once, in first-use order.</summary>
    public static IReadOnlyList<string> UnsupportedOps(JsonElement scene)
    {
        var ops = new List<string>();
        if (scene.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
            {
                var op = action.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                if (!SupportedOps.Contains(op) && !ops.Contains(op)) ops.Add(op);
            }
        return ops;
    }

    public static Scene Parse(JsonElement e, string repoRoot) => Parse(e, repoRoot, EraseStroking.Once);

    internal static Scene Parse(JsonElement e, string repoRoot, EraseStroking eraseStroking)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

        var name = Str(e, "name") ?? throw new InvalidDataException("scene without a name");
        var unsupported = UnsupportedOps(e);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"scene {name}: ops the engine does not run yet: {string.Join(", ", unsupported)}");

        var fills = ImmutableList.CreateBuilder<(string, Fill)>();
        if (e.TryGetProperty("regionColors", out var rc))
            foreach (var p in rc.EnumerateObject())
                fills.Add((p.Name, p.Value.ValueKind == JsonValueKind.String
                    ? Fill.Solid(p.Value.GetString()!)
                    : new Fill(Str(p.Value, "type") ?? "solid", Str(p.Value, "c1"), Str(p.Value, "c2"), Num(p.Value, "angle"), Num(p.Value, "scale"))));

        var layers = ImmutableList.CreateBuilder<Layer>();
        if (e.TryGetProperty("layers", out var ls))
        {
            int i = 0;
            foreach (var l in ls.EnumerateArray())
            {
                i++;
                layers.Add(new Layer
                {
                    Id = i, Name = "L" + i, Image = Image(Str(l, "image")!),
                    Visible = Bool(l, "visible") ?? true, Opacity = Num(l, "opacity") ?? 1,
                    X = Num(l, "x") ?? 0, Y = Num(l, "y") ?? 0, Scale = Num(l, "scale") ?? 1, Rotation = Num(l, "rotation") ?? 0,
                    FlipH = Bool(l, "flipH") ?? false, FlipV = Bool(l, "flipV") ?? false,
                    Brightness = Num(l, "brightness") ?? 100, Contrast = Num(l, "contrast") ?? 100,
                    Saturate = Num(l, "saturate") ?? 100, HueRotate = Num(l, "hueRotate") ?? 0,
                    RShift = (int)(Num(l, "rShift") ?? 0), GShift = (int)(Num(l, "gShift") ?? 0), BShift = (int)(Num(l, "bShift") ?? 0),
                });
            }
        }

        var bgImage = Str(e, "bgImage");
        var document = new Document
        {
            Mode = Str(e, "mode") == "Pants" ? TemplateMode.Pants : TemplateMode.Shirt,
            BgColor = Str(e, "bgColor") ?? "#192e44",
            BgOpacity = Num(e, "bgOpacity") ?? 1,
            BgImage = bgImage is null ? null : Image(bgImage),
            RegionFills = fills.ToImmutable(),
            Layers = layers.ToImmutable(),
        };

        if (e.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
                document = action.GetProperty("op").GetString() switch
                {
                    "shape" => ApplyShape(document, action),
                    "erase" => ApplyErase(document, action, eraseStroking),
                    "wordart" => ApplyWordArt(document, action),
                    "detail" => ApplyDetail(document, action),
                    "project" => ApplyProject(action, repoRoot),
                    var op => throw new InvalidDataException($"scene {name}: op '{op}' has no handler"),
                };

        return new Scene(name, document);
    }

    /// <summary>
    /// A shape gesture from <c>from</c> to <c>to</c>, committed as 4.1 commits it: a new drawing layer
    /// on top, with the next id after every layer already there (editor.js:1285, :528-540).
    /// tool, from, to, color and width are required; fill defaults to false (drawFill, editor.js:92).
    /// </summary>
    static Document ApplyShape(Document doc, JsonElement a)
    {
        var tool = a.GetProperty("tool").GetString() switch
        {
            "line" => ShapeTool.Line,
            "rect" => ShapeTool.Rect,
            "ellipse" => ShapeTool.Ellipse,
            var t => throw new InvalidDataException($"unknown shape tool '{t}'"),
        };
        SKPoint Pt(string key)
        {
            var p = a.GetProperty(key);
            return new SKPoint(p[0].GetSingle(), p[1].GetSingle());
        }
        bool fill = a.TryGetProperty("fill", out var f) && f.GetBoolean();
        var stroke = new Stroke(tool, new[] { Pt("from"), Pt("to") },
            CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), fill);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Tools.CommitDrawing(stroke, id, Tools.DrawingName(tool));
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

    /// <summary>
    /// Scene v2 project op: <c>{ "op": "project", "file": "&lt;repo-relative .r15proj&gt;" }</c>. 4.1
    /// (app-harness.js project) feeds the file to #projectFileInput, whose change handler runs
    /// loadProject (editor.js:2684-2687, 2593-2680), and waits until every image layer and the
    /// background have loaded. The loaded project replaces the document, so the result is the file's
    /// document alone. A file that drops a layer is refused: the harness only froze projects whose
    /// every layer loaded, so a drop would compare a different document against the golden.
    /// </summary>
    public static Document ApplyProject(JsonElement action, string repoRoot)
    {
        var file = action.GetProperty("file").GetString() ?? throw new InvalidDataException("project: file is null");
        var load = ProjectIO.Read(File.ReadAllText(Path.Combine(repoRoot, file)));
        if (!load.DroppedLayers.IsEmpty)
            throw new InvalidDataException($"project {file}: layers that did not load: {string.Join(", ", load.DroppedLayers)}");
        return load.Document;
    }

    /// <summary>
    /// Scene v2 detail op: <c>{ "op": "detail", "type": id, "color": "#rrggbb", "width": n, "stitch": bool }</c>.
    /// app-harness.js detail() sets #detailColor, #detailWidth and #detailDash (stitch defaults to
    /// false) and clicks the button (editor.js:2077-2103), so createDetailLayer reads those values
    /// (editor.js:1840-1842) and pushes the layer on top with the next id (editor.js:1858-1859).
    /// The document's mode is passed through, so a Pants scene draws on the pants faces (approved
    /// divergence details-pants-mode-behavior; the harness refuses Pants because 4.1 throws).
    /// </summary>
    public static Document ApplyDetail(Document doc, JsonElement a)
    {
        DetailType type;
        try { type = Details.FromId(a.GetProperty("type").GetString()!); }
        catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        bool stitch = a.TryGetProperty("stitch", out var st) && st.GetBoolean();
        var style = new DetailStyle(CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), stitch);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Details.Create(type, doc.Mode, style, id);
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

    /// <summary>
    /// Scene v2 wordart op: <c>{ "op": "wordart", "text", "font", "style", "size", "fill", "stroke",
    /// "grad", "strokeWidth", "angle" }</c>, every key optional. app-harness.js wordart() sets only
    /// the controls a scene names, so an omitted key keeps the control's 4.1 default
    /// (editor.html:229-262; waStyle 'solid', editor.js:2108), then clicks Add to canvas
    /// (editor.js:2257-2291). The layer goes on top with the next id (nextId++, editor.js:2275).
    /// </summary>
    public static Document ApplyWordArt(Document doc, JsonElement a)
    {
        string Str(string key, string fallback) => a.TryGetProperty(key, out var v) ? v.GetString()! : fallback;
        float Num(string key, float fallback) => a.TryGetProperty(key, out var v) ? v.GetSingle() : fallback;
        var style = Str("style", "solid") switch
        {
            "solid" => WordArtStyle.Solid,
            "gradient" => WordArtStyle.Gradient,
            "neon" => WordArtStyle.Neon,
            "shadow" => WordArtStyle.Shadow,
            "outline" => WordArtStyle.Outline,
            "retro" => WordArtStyle.Retro,
            var other => throw new InvalidDataException($"unknown word art style '{other}'"),
        };
        var settings = new WordArtSettings(Str("text", "RIZZ"), Str("font", "Anton"), style, Num("size", 64),
            CssColor.ToSk(Str("fill", "#17d4fa")), CssColor.ToSk(Str("stroke", "#000000")), CssColor.ToSk(Str("grad", "#f22f89")),
            Num("strokeWidth", 3), Num("angle", 0));
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        return doc with { Layers = doc.Layers.Add(WordArt.CreateLayer(settings, doc.Mode, id)) };
    }

    /// <summary>
    /// Scene v2 erase op: <c>{ "op": "erase", "layer": i, "points": [[x,y],...], "size": n }</c>, stroked
    /// once like the editor. 4.1 (app-render.js) selects layer i, picks the eraser, presses at
    /// points[0], moves through the rest and releases (editor.js:1120-1146, 1196-1203, 1248-1283).
    /// Drawings append above the scene's layers (editor.js:534 layers.push), so i indexes the
    /// document's layers directly.
    /// </summary>
    public static Document ApplyErase(Document document, JsonElement action) => ApplyErase(document, action, EraseStroking.Once);

    internal static Document ApplyErase(Document document, JsonElement action, EraseStroking stroking)
    {
        int index = action.GetProperty("layer").GetInt32();
        if (index < 0 || index >= document.Layers.Count)
            throw new InvalidDataException($"erase: the scene has no layer {index}");
        var points = action.GetProperty("points").EnumerateArray()
            .Select(p => new SKPoint((float)p[0].GetDouble(), (float)p[1].GetDouble()))
            .ToList();
        float size = (float)action.GetProperty("size").GetDouble();
        var erased = Eraser.Apply(document.Layers[index], points, size, stroking);
        return document with { Layers = document.Layers.SetItem(index, erased) };
    }
}
```

`src/RBX15.Engine.Tests/GoldenAppTests.cs` after Step 18 (Step 23 routes the Word Art scenes):

```csharp
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2b enabled detail, wordart and project one task at a time; PendingOps lists what is still skipped).
/// <para>
/// Erase actions run through the internal EraseStroking.Replay41, which re-strokes the eraser path
/// after every mousemove exactly as app-render.js drove 4.1 (mousedown at points[0], one mousemove
/// per later point, mouseup with no stroke), so every erase scene meets the unchanged rule.
/// Production strokes once (approved divergence draw-pen-incremental-stroke); it is held to the
/// goldens directly on the two-point drags, where 4.1 strokes once too.
/// </para>
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    static readonly string[] PendingOps = { };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    static JsonElement Scene(string name) => Raw.Value.Single(e => NameOf(e) == name);

    /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
    public static TheoryData<string> ScenesBlockedBy(string op)
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            var unsupported = SceneLoader.UnsupportedOps(e);
            if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
        }
        return data;
    }

    /// <summary>Runnable scenes with at least one erase, every erase a two-point drag (one stroke in 4.1).</summary>
    public static TheoryData<string> TwoPointEraseScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            if (SceneLoader.UnsupportedOps(e).Count != 0 || !e.TryGetProperty("actions", out var actions)) continue;
            var erases = actions.EnumerateArray().Where(a => a.GetProperty("op").GetString() == "erase").ToList();
            if (erases.Count > 0 && erases.All(a => a.GetProperty("points").GetArrayLength() == 2)) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root, EraseStroking.Replay41);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Theory]
    [MemberData(nameof(TwoPointEraseScenes))]
    public void Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".once.png"));
    }

    [Fact]
    public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
    {
        var unclassified = Raw.Value
            .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
            .ToList();
        Assert.Empty(unclassified);
    }

    [Fact]
    public void Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps()
    {
        var rows = typeof(GoldenAppTests).GetMethod(nameof(Engine_export_matches_the_frozen_4_1_app_golden))!
            .GetCustomAttributes(typeof(MemberDataAttribute), false).Cast<MemberDataAttribute>()
            .Select(a => (Op: (string)a.Parameters[0], a.Skip))
            .ToList();

        var skipOps = rows.Where(r => r.Skip != null).Select(r => r.Op).ToList();
        Assert.Equal(PendingOps.OrderBy(o => o), skipOps.OrderBy(o => o));
        Assert.Single(rows, r => r.Skip == null && r.Op == "");

        var accounted = rows.Sum(r => ((System.Collections.IEnumerable)ScenesBlockedBy(r.Op)).Cast<object>().Count());
        Assert.Equal(Raw.Value.Count, accounted);
        Assert.Equal(82, Raw.Value.Count);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
```

- [ ] **Step 19: Run the loader tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderWordArtTests|FullyQualifiedName~SceneLoaderTests"`
Expected: `Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14` (10 `SceneLoaderTests`, 4 `SceneLoaderWordArtTests`).

- [ ] **Step 20: Run the Word Art goldens under the unchanged rule (the contract's procedure)**

Run: `mkdir -p .superpowers/logs && dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" > .superpowers/logs/app-golden-wordart.log 2>&1; tail -3 .superpowers/logs/app-golden-wordart.log`
Expected: `Failed!  - Failed:    22, Passed:    66, Skipped:     0, Total:    88`. The accounting fact passes.

Then record each scene's numbers with the Parity CLI (heatmaps go to `.superpowers/m2b-measure/wordart-heat/`):

```bash
mkdir -p .superpowers/m2b-measure/wordart-heat .superpowers/logs
python - <<'EOF'
import json
s = json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))
w = [x for x in s if any(a["op"] == "wordart" for a in x.get("actions", []))]
open(".superpowers/m2b-measure/wordart-scenes.json", "w", encoding="utf-8", newline="\n").write(json.dumps(w))
print(len(w), "wordart scenes")
EOF
dotnet run --project src/RBX15.Parity -c Release -- render .superpowers/m2b-measure/wordart-scenes.json .superpowers/m2b-measure/wordart > /dev/null
for f in .superpowers/m2b-measure/wordart/*.png; do
  n=$(basename "$f" .png); printf '%s ' "$n"
  dotnet run --project src/RBX15.Parity -c Release --no-build -- diff "tests/goldens/app/$n.png" "$f" --heatmap ".superpowers/m2b-measure/wordart-heat/$n.png"
done | tee .superpowers/logs/wordart-finding.txt
```

Expected: `22 wordart scenes`, then exactly these 22 lines (measured by the prototype):

```text
wordart-gradient-135-poppins same-size=True max-delta=30 differing=1092 interior=0 edge-over-tolerance=798 pass=False
wordart-gradient-anton same-size=True max-delta=42 differing=2034 interior=0 edge-over-tolerance=1154 pass=False
wordart-gradient-dancing same-size=True max-delta=39 differing=1556 interior=0 edge-over-tolerance=867 pass=False
wordart-gradient-poppins same-size=True max-delta=37 differing=1091 interior=0 edge-over-tolerance=789 pass=False
wordart-multiline-orbitron same-size=True max-delta=45 differing=1610 interior=0 edge-over-tolerance=1168 pass=False
wordart-neon-anton same-size=True max-delta=61 differing=2393 interior=0 edge-over-tolerance=459 pass=False
wordart-neon-dancing same-size=True max-delta=56 differing=1684 interior=0 edge-over-tolerance=335 pass=False
wordart-neon-poppins same-size=True max-delta=38 differing=1839 interior=0 edge-over-tolerance=241 pass=False
wordart-outline-anton same-size=True max-delta=48 differing=2797 interior=7 edge-over-tolerance=1585 pass=False
wordart-outline-dancing same-size=True max-delta=47 differing=1625 interior=0 edge-over-tolerance=907 pass=False
wordart-outline-poppins same-size=True max-delta=44 differing=1611 interior=0 edge-over-tolerance=944 pass=False
wordart-outline-stroke-0-dancing same-size=True max-delta=47 differing=1625 interior=0 edge-over-tolerance=907 pass=False
wordart-retro-anton same-size=True max-delta=63 differing=2365 interior=0 edge-over-tolerance=1696 pass=False
wordart-retro-dancing same-size=True max-delta=76 differing=2484 interior=0 edge-over-tolerance=1680 pass=False
wordart-retro-poppins same-size=True max-delta=79 differing=1419 interior=0 edge-over-tolerance=1037 pass=False
wordart-shadow-anton same-size=True max-delta=67 differing=1729 interior=0 edge-over-tolerance=972 pass=False
wordart-shadow-dancing same-size=True max-delta=76 differing=1620 interior=0 edge-over-tolerance=833 pass=False
wordart-shadow-poppins same-size=True max-delta=39 differing=1169 interior=0 edge-over-tolerance=548 pass=False
wordart-solid-anton same-size=True max-delta=49 differing=2049 interior=0 edge-over-tolerance=1210 pass=False
wordart-solid-dancing same-size=True max-delta=56 differing=1594 interior=0 edge-over-tolerance=893 pass=False
wordart-solid-poppins same-size=True max-delta=32 differing=1087 interior=0 edge-over-tolerance=807 pass=False
wordart-stroke-12-anton same-size=True max-delta=77 differing=1194 interior=0 edge-over-tolerance=736 pass=False
```

Now apply the contract's Word Art procedure, as ruling R2 settled it, to what you got:

- **Your lines equal the 22 above.** On 21 scenes the differences are glyph-edge only. On `wordart-outline-anton`, 7 one-level interior pixels sit at `x=314, y=147..153`: the golden has `(22, 212, 250)` and the engine has `(23, 212, 250)`. Ruling R2 classified them as glyph rasterization, inside the Word Art rule's 1-level interior bound. Go to Step 21.
- **Every scene passes.** This is not expected. Go to Step 21 anyway: every Word Art scene uses the Word Art rule (a scene that meets the unchanged rule meets it too), and the swap guard still applies.
- **Any other interior difference, a size mismatch, or numbers that differ from the lines above.** That is an engine bug (fix-the-engine rule: never edit a golden, `ImageDiff` or `GoldenRule`). Use superpowers:systematic-debugging from `.superpowers/m2b-measure/wordart-heat/<scene>.png`. In order of likelihood:
  - **The whole word is offset, or the heatmap shows two outlines.** Check the middle offset, the center (`x - width/2` in float, after converting x to float) and the shaping width. Re-run Step 9.
  - **The neon glow is too tight or too wide** (a ring of differences 10 to 40 px from the glyphs). The blur sigma must be blur/2 with `respectCTM: false`.
  - **Banding differences across a gradient's interior.** The gradient paint lost `IsDither`, or another paint gained it.
  - **Retro or shadow copies are misplaced.** The offsets add to `cx`/`ty` before the half-width shift, and even `i` takes the stroke color.
  - **The outline is too thick or too thin everywhere.** Check `OutlineWidth` (0 means 3) and the round join.
  - **Synthetic bold is missing or doubled** (Anton strokes thinner or thicker throughout). `CreateFont` must set `Embolden` from `IsSyntheticBold`, and `Get` must not return a bold-simulated face.

  If none of these explains it, stop and report the scene, the heatmap and the line.

- [ ] **Step 21: Capture 4.1's own Word Art bitmaps once, and freeze them**

Ruling R2's guard needs, for each of the 22 Word Art scenes, the bitmap 4.1's own `renderWordArt` drew in the real `editor.html`. This step captures them once into `tests/goldens/app/wordart-ref/`, with the reproducibility check M2a Task 3 used for the app goldens (render twice, compare hashes, never freeze a nondeterministic image). From the commit at the end of this step, those files are frozen: never re-run the tool into that folder.

First add the tool. It is the drafters' scratch measurement tool (the source of every layout number in `WordArtTests`), folded into one file with repo-relative paths and flags. Its 22 PNGs and its widths were re-run and are byte-identical to the drafters' outputs. Create `tools/parity-ref/wordart-ref.js`:

```js
// M2b Task 3 froze this tool's output once in tests/goldens/app/wordart-ref, which WordArtSwapTests
// reads; never re-run it into that folder. Renders each Word Art scene's text bitmap with 4.1's own
// renderWordArt (editor.js:2120-2240) in the real editor.html: Electron on CPU raster, Google Fonts
// answered from assets/fonts through fonts.js, the controls set as app-harness.js wordart() sets
// them. Each PNG is the bitmap Add to canvas decodes (editor.js:2259-2269), so swapping it into the
// engine's layer isolates the text bitmap from placement and compositing. metrics.json records
// canvas size, measureText widths and the middle-baseline offset per scene.
//   env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/wordart-ref.js \
//     --out <dir> [--scenes tests/goldens/app/scenes.json] [--only name1,name2]
'use strict';
const { app, BrowserWindow, protocol } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const fonts = require('./fonts.js');

const ROOT = path.resolve(__dirname, '..', '..');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const OUT = path.resolve(arg('--out') || path.join(ROOT, '.superpowers', 'm2b-measure', 'wordart-ref'));
const SCENES = path.resolve(arg('--scenes') || path.join(ROOT, 'tests', 'goldens', 'app', 'scenes.json'));
const ONLY = arg('--only') ? new Set(arg('--only').split(',')) : null;

// Runs in the page. Sets only the controls the op names, selects the font, clicks the style,
// waits for the exact face renderWordArt asks for, then renders at full size (cw x ch).
function page() {
  window.__wa = async function (op) {
    const set = (id, v, ev = 'input') => { const el = document.getElementById(id); el.value = String(v); el.dispatchEvent(new Event(ev, { bubbles: true })); };
    for (const [k, id] of [['text', 'waText'], ['fill', 'waColor'], ['stroke', 'waStroke'], ['grad', 'waColor2'], ['size', 'waSize'], ['strokeWidth', 'waStrokeW'], ['angle', 'waAngle']]) if (op[k] !== undefined) set(id, op[k]);
    set('waFont', op.font, 'change');
    document.querySelector('.wordart-style-btn[data-style="' + op.style + '"]').click();
    const spec = '900 ' + (+document.getElementById('waSize').value || 64) + 'px "' + op.font + '"';
    await document.fonts.load(spec); await document.fonts.ready;
    const c = document.createElement('canvas'); c.width = 1024; c.height = 512;
    const r1 = renderWordArt(c, 0, 0); c.width = r1.cw; c.height = r1.ch; renderWordArt(c, 0, 0);
    // measureText with renderWordArt's own font string (editor.js:2131-2132) and text (:2121).
    const t = document.createElement('canvas').getContext('2d'); t.font = spec + ', sans-serif';
    const lines = (document.getElementById('waText').value || 'TEXT').split('\n');
    const m = lines.map((l) => { const mm = t.measureText(l); return { w: mm.width, chars: [...l].map((ch) => t.measureText(ch).width), abl: mm.actualBoundingBoxLeft, abr: mm.actualBoundingBoxRight, aba: mm.actualBoundingBoxAscent, abd: mm.actualBoundingBoxDescent, fba: mm.fontBoundingBoxAscent, fbd: mm.fontBoundingBoxDescent }; });
    t.textBaseline = 'middle'; const mid = t.measureText('R');
    return { cw: r1.cw, ch: r1.ch, m, midAlphabetic: mid.alphabeticBaseline, url: c.toDataURL('image/png') };
  };
}

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');
app.whenReady().then(async () => {
  try {
    for (const scheme of ['https', 'http']) {
      protocol.handle(scheme, (req) => {
        const r = fonts.respond(req.url);
        return r ? new Response(r.body, { status: 200, headers: r.headers }) : new Response('not vendored', { status: 404 });
      });
    }
    const scenes = JSON.parse(fs.readFileSync(SCENES, 'utf8'))
      .filter((s) => (s.actions || []).some((a) => a.op === 'wordart'))
      .filter((s) => !ONLY || ONLY.has(s.name));
    fs.mkdirSync(OUT, { recursive: true });
    const win = new BrowserWindow({ show: false, width: 1200, height: 800, webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false } });
    const results = {};
    for (const sc of scenes) {
      await win.loadFile(path.join(ROOT, 'editor.html'));
      await win.webContents.executeJavaScript(`(${page.toString()})(); 0;`);
      const op = sc.actions.find((a) => a.op === 'wordart');
      const r = await win.webContents.executeJavaScript(`__wa(${JSON.stringify(op)}).then((v) => ({ ok: true, v }), (e) => ({ ok: false, e: String((e && e.stack) || e) }))`);
      if (!r.ok) throw new Error(sc.name + ': ' + r.e);
      fs.writeFileSync(path.join(OUT, sc.name + '.png'), Buffer.from(r.v.url.split(',')[1], 'base64'));
      delete r.v.url;
      results[sc.name] = r.v;
      console.log('rendered ' + sc.name);
    }
    fs.writeFileSync(path.join(OUT, 'metrics.json'), JSON.stringify(results, null, 1) + '\n');
    console.log('ok ' + Object.keys(results).length);
    app.exit(0);
  } catch (e) {
    console.error(e.stack);
    app.exit(1);
  }
});
```

Check that the 4.1 sources are the ones the app goldens were captured from:

```bash
git hash-object editor.js editor.html
python -c "import json; d=json.load(open('tests/goldens/app/index.json')); print(d['editorJsBlob']); print(d['editorHtmlBlob'])"
```

Expected: `2790c41e418cd5bfea82cf1ae4d71626a7b54e61` and `401a5d5f33c4accbb452cb080c9c544a0f79a6ce`, twice, in that order. If they differ, the Electron app changed (a global-constraint violation): stop and report it.

Render all 22 scenes twice into the git-ignored `.superpowers/`:

```bash
for run in a b; do
  env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/wordart-ref.js --out .superpowers/m2b-measure/wordart-ref-$run 2>&1 | grep -E "^ok|Error"
done
```

Expected: `ok 22`, twice.

Compare the runs, then copy run `a` into the frozen folder with a provenance index. Save this as `.superpowers/m2b/freeze-wordart-ref.py` and run it from the repo root:

```python
import hashlib, json, pathlib, shutil, subprocess
a = pathlib.Path(".superpowers/m2b-measure/wordart-ref-a")
b = pathlib.Path(".superpowers/m2b-measure/wordart-ref-b")
out = pathlib.Path("tests/goldens/app/wordart-ref")
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
names = sorted(p.stem for p in a.glob("*.png"))
assert names == sorted(p.stem for p in b.glob("*.png")) and len(names) == 22, "each run must render the 22 wordart scenes"
bad = [n for n in names if sha(a / f"{n}.png") != sha(b / f"{n}.png")]
assert not bad, f"MISMATCH {bad}: 4.1's Word Art bitmaps are not deterministic; never freeze them"
assert json.loads((a / "metrics.json").read_text()) == json.loads((b / "metrics.json").read_text()), "metrics differ between runs"
print(f"reproducible: {len(names)}")
digest = hashlib.sha256("".join(f"{n} {sha(a / f'{n}.png')}\n" for n in names).encode()).hexdigest()
print("digest", digest)
assert not out.exists(), f"{out} already exists: the fixtures are frozen, never recaptured"
out.mkdir()
for n in names:
    shutil.copyfile(a / f"{n}.png", out / f"{n}.png")
shutil.copyfile(a / "metrics.json", out / "metrics.json")
golden = json.load(open("tests/goldens/app/index.json", encoding="utf-8"))
blob = lambda f: subprocess.run(["git", "hash-object", f], capture_output=True, text=True, check=True).stdout.strip()
index = {
    "tool": "tools/parity-ref/wordart-ref.js",
    "electron": json.load(open("node_modules/electron/package.json", encoding="utf-8"))["version"],
    "editorJsBlob": blob("editor.js"),
    "editorHtmlBlob": blob("editor.html"),
    "fontsManifestSha256": sha(pathlib.Path("assets/fonts/README.md")),
    "scenes": [{"name": n, "sha256": sha(out / f"{n}.png")} for n in names],
}
for k in ("electron", "editorJsBlob", "editorHtmlBlob", "fontsManifestSha256"):
    assert index[k] == golden[k], f"{k} differs from the app goldens' index: {index[k]} vs {golden[k]}"
(out / "index.json").write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8", newline="\n")
print(f"froze {len(names)} bitmaps, metrics.json and index.json in {out.as_posix()}")
```

```bash
python .superpowers/m2b/freeze-wordart-ref.py
ls tests/goldens/app/wordart-ref | wc -l
python -c "import json; m=json.load(open('tests/goldens/app/wordart-ref/metrics.json')); d=m['wordart-solid-dancing']; print(len(m), d['cw'], d['ch'], round(d['m'][0]['w'], 3))"
```

Expected:

- `reproducible: 22`.
- `digest 80625aa3c60a83534119f901528e1dbfc76da2b270192a556c9195071d3a3cee`. That is the hash of the 22 `name sha256` lines, and it equals the drafters' and the reconciler's runs, so these are the bitmaps every number in this task was measured against. A different digest with `reproducible: 22` means the rendering environment changed (Electron, the fonts or `editor.js`): stop and report the digest; do not commit.
- `froze 22 bitmaps, metrics.json and index.json in tests/goldens/app/wordart-ref`, then `24`.
- `22 179 94 158.272`. The value 158.272 is the shaped Dancing Script width that `Lines_are_shaped_to_chromiums_measureText_width` pins.

Commit the tool and the frozen bitmaps on their own. The enable change stays uncommitted in the working tree for Step 23. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add tools/parity-ref/wordart-ref.js tests/goldens/app/wordart-ref
git commit -m "test(goldens): freeze 4.1's own Word Art bitmaps for the swap guard (wordart-ref.js)"
git status --short
```

Expected: `git status --short` lists exactly `M src/RBX15.Parity/SceneLoader.cs`, `M src/RBX15.Engine.Tests/GoldenAppTests.cs` and `?? src/RBX15.Parity.Tests/SceneLoaderWordArtTests.cs`.

- [ ] **Step 22: Write the failing Word Art rule tests and the swap guard**

Create `src/RBX15.Engine.Tests/GoldenRuleWordArtTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The Word Art rule (GoldenRule.CompareWordArt, ruling R2): interior pixels within 1 level on every
/// channel, edge pixels unbounded, sizes equal. Synthetic 5x5 bitmaps: every border pixel is an edge,
/// and the center (2, 2) of a flat bitmap is interior.
/// </summary>
public class GoldenRuleWordArtTests
{
    static readonly SKColor Flat = new(23, 212, 250, 255);

    static SKBitmap Bitmap(int width = 5, int height = 5)
    {
        var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bmp.SetPixel(x, y, Flat);
        return bmp;
    }

    [Fact]
    public void An_interior_pixel_one_level_off_passes()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap();
        candidate.SetPixel(2, 2, new SKColor(22, 212, 250, 255));
        var result = GoldenRule.CompareWordArt(golden, candidate);
        Assert.Equal(new GoldenRule.WordArtResult(true, 1, 0, 0), result);
        Assert.True(result.Pass);
    }

    [Fact]
    public void An_interior_pixel_two_levels_off_fails_and_writes_a_heatmap()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap();
        candidate.SetPixel(2, 2, new SKColor(23, 212, 250, 253));
        Assert.Equal(new GoldenRule.WordArtResult(true, 2, 1, 0), GoldenRule.CompareWordArt(golden, candidate));
        var heatmap = Path.Combine(Path.GetTempPath(), "rbx15-wordart-rule-" + Guid.NewGuid().ToString("N"), "heat.png");
        try
        {
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => GoldenRule.AssertWordArtBitmaps("synthetic", golden, candidate, heatmap));
            Assert.True(File.Exists(heatmap));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(heatmap)!, true);
        }
    }

    [Fact]
    public void Edge_pixels_are_not_bounded()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap();
        candidate.SetPixel(0, 0, new SKColor(255, 0, 0, 255));
        var result = GoldenRule.CompareWordArt(golden, candidate);
        Assert.Equal(new GoldenRule.WordArtResult(true, 0, 0, 250), result);
        Assert.True(result.Pass);
    }

    [Fact]
    public void A_size_mismatch_fails()
    {
        using var golden = Bitmap();
        using var candidate = Bitmap(5, 4);
        Assert.False(GoldenRule.CompareWordArt(golden, candidate).Pass);
    }
}
```

Create `src/RBX15.Engine.Tests/WordArtSwapTests.cs`. It replaces the reconcile's throwaway classifier with a permanent test: the classifier's `swap` line, made exact for all 22 scenes.

```csharp
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The permanent guard under the Word Art rule (ruling R2). tests/goldens/app/wordart-ref holds, for
/// every wordart scene, the bitmap 4.1's own renderWordArt drew (tools/parity-ref/wordart-ref.js,
/// captured once in M2b and frozen). Swapping it in for the engine's Word Art bitmap must meet the
/// golden exactly under the unchanged GoldenRule, with no pixel differing: canvas size, the 85% fit,
/// centering, the un-premultiplied round trip and the downscaled draw are all the engine's own. The
/// only freedom the Word Art rule grants is inside the glyph bitmap.
/// </summary>
public class WordArtSwapTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");
    static readonly string RefDir = Path.Combine(Dir, "wordart-ref");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray()
            .Where(e => e.TryGetProperty("actions", out var a) && a.EnumerateArray().Any(x => x.GetProperty("op").GetString() == "wordart"))
            .Select(e => e.Clone())
            .ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    public static TheoryData<string> WordArtScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value) data.Add(NameOf(e));
        return data;
    }

    [Fact]
    public void Every_wordart_scene_has_exactly_one_frozen_4_1_bitmap()
    {
        Assert.Equal(22, Raw.Value.Count);
        var expected = Raw.Value.Select(e => NameOf(e) + ".png").Order(StringComparer.Ordinal).ToList();
        var actual = Directory.EnumerateFiles(RefDir, "*.png").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WordArtScenes))]
    public void The_4_1_word_art_bitmap_in_the_engine_document_meets_the_golden_exactly(string name)
    {
        var scene = SceneLoader.Parse(Raw.Value.Single(e => NameOf(e) == name), Root);
        var layers = scene.Document.Layers;
        var engine = layers[^1];
        var reference = Images.Decode(File.ReadAllBytes(Path.Combine(RefDir, name + ".png")));
        Assert.Equal((reference.Width, reference.Height), (engine.Image.Width, engine.Image.Height));

        var swapped = scene.Document with { Layers = layers.SetItem(layers.Count - 1, engine with { Image = reference }) };
        var goldenPath = Path.Combine(Dir, name + ".png");
        GoldenRule.AssertMatches(name, goldenPath, swapped, Path.Combine(Root, "diff-heatmaps", "app", name + ".swap.png"));

        using var golden = ImageDiff.Load(goldenPath);
        using var rendered = Renderer.Export(swapped);
        using var candidate = ImageDiff.FromImage(rendered);
        var exact = ImageDiff.Compare(golden, candidate);
        Assert.True(exact.DifferingPixels == 0, $"{name}: {exact}");
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails with `8 Error(s)`, all in `GoldenRuleWordArtTests.cs`: `error CS0117: 'GoldenRule' does not contain a definition for 'CompareWordArt'` (4 sites), the same for `'AssertWordArtBitmaps'` (1), and `error CS0426: The type name 'WordArtResult' does not exist in the type 'GoldenRule'` (3). `WordArtSwapTests.cs` compiles: it uses only the unchanged `GoldenRule.AssertMatches`.

- [ ] **Step 23: Add the Word Art rule, route the Word Art scenes, and record the divergence**

1. Replace `src/RBX15.Engine.Tests/GoldenRule.cs` with the file below. `AssertMatches` and everything above it are unchanged; the Word Art rule is added at the end of the class. `ImageDiff` does not change, so the rule restates its delta and interior test.

   ```csharp
   using RBX15.Parity;
   using SkiaSharp;

   namespace RBX15.Engine.Tests;

   /// <summary>
   /// The golden rule (spec section 6) as both golden gates apply it: zero interior differences and
   /// edge deltas of 3 or less. Scenes with an RGB shift (approved divergence rgb-shift-compute) are
   /// held to the full rule on pixels whose golden alpha is 255, plus an alpha delta of at most 1
   /// everywhere. A failing scene writes a heatmap (a CI artifact) before the assert.
   /// </summary>
   static class GoldenRule
   {
       static SKBitmap OpaqueOnly(SKBitmap source, SKBitmap golden)
       {
           var copy = source.Copy();
           for (int y = 0; y < golden.Height; y++)
               for (int x = 0; x < golden.Width; x++)
                   if (golden.GetPixel(x, y).Alpha < 255) copy.SetPixel(x, y, SKColors.Transparent);
           return copy;
       }

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

       /// <summary>
       /// What the Word Art rule measured: whether the sizes match, the largest delta on interior pixels,
       /// how many interior pixels differ by more than 1, and the largest delta on edge pixels (reported,
       /// never bounded).
       /// </summary>
       internal sealed record WordArtResult(bool SameSize, int InteriorMaxDelta, int InteriorOverOne, int EdgeMaxDelta)
       {
           public bool Pass => SameSize && InteriorOverOne == 0;
       }

       /// <summary>
       /// The Word Art rule (ruling R2, 2026-10-14; divergence wordart-glyph-rasterization). Both images
       /// have the same size, and every interior pixel (its golden 3x3 neighborhood one flat color, as
       /// ImageDiff defines interior) differs by at most 1 level on every channel. Edge pixels are not
       /// bounded: Chromium rasterizes Word Art glyphs through DirectWrite and SkiaSharp does not, so the
       /// glyph edges differ. Layout and placement are held exactly by WordArtSwapTests, which swaps 4.1's
       /// own Word Art bitmap into the engine's document under the unchanged <see cref="AssertMatches"/>.
       /// Both bitmaps must be un-premultiplied RGBA, as ImageDiff.Load and ImageDiff.FromImage return them.
       /// </summary>
       internal static WordArtResult CompareWordArt(SKBitmap golden, SKBitmap candidate)
       {
           foreach (var b in new[] { golden, candidate })
               if (b.ColorType != SKColorType.Rgba8888 || b.AlphaType != SKAlphaType.Unpremul)
                   throw new ArgumentException("the Word Art rule compares un-premultiplied RGBA bitmaps");
           if (golden.Width != candidate.Width || golden.Height != candidate.Height)
               return new WordArtResult(false, 255, -1, 255);

           // ImageDiff's delta and interior test, restated here because ImageDiff never changes.
           static int Delta(SKColor a, SKColor b) =>
               a.Alpha == 0 && b.Alpha == 0 ? 0
               : Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)),
                          Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));
           bool IsInterior(int x, int y)
           {
               if (x == 0 || y == 0 || x == golden.Width - 1 || y == golden.Height - 1) return false;
               var center = golden.GetPixel(x, y);
               for (int dy = -1; dy <= 1; dy++)
                   for (int dx = -1; dx <= 1; dx++)
                       if (Delta(golden.GetPixel(x + dx, y + dy), center) != 0) return false;
               return true;
           }

           int interiorMax = 0, interiorOverOne = 0, edgeMax = 0;
           for (int y = 0; y < golden.Height; y++)
               for (int x = 0; x < golden.Width; x++)
               {
                   int d = Delta(golden.GetPixel(x, y), candidate.GetPixel(x, y));
                   if (d == 0) continue;
                   if (IsInterior(x, y))
                   {
                       interiorMax = Math.Max(interiorMax, d);
                       if (d > 1) interiorOverOne++;
                   }
                   else edgeMax = Math.Max(edgeMax, d);
               }
           return new WordArtResult(true, interiorMax, interiorOverOne, edgeMax);
       }

       /// <summary>
       /// Asserts the Word Art rule on two bitmaps. A same-size failure writes a heatmap first, as
       /// <see cref="AssertMatches"/> does; a size mismatch has no per-pixel heatmap and fails on the size.
       /// </summary>
       internal static void AssertWordArtBitmaps(string name, SKBitmap golden, SKBitmap candidate, string heatmapPath)
       {
           var result = CompareWordArt(golden, candidate);
           if (!result.Pass && result.SameSize)
           {
               Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
               using var heat = ImageDiff.Heatmap(golden, candidate);
               using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
               File.WriteAllBytes(heatmapPath, data.ToArray());
           }
           Assert.True(result.Pass, $"{name}: {result}");
       }

       /// <summary>The Word Art golden gate: the engine export of a scene with a wordart action, under <see cref="CompareWordArt"/>.</summary>
       public static void AssertWordArtMatches(string name, string goldenPath, Document document, string heatmapPath)
       {
           using var golden = ImageDiff.Load(goldenPath);
           using var rendered = Renderer.Export(document);
           using var candidate = ImageDiff.FromImage(rendered);
           AssertWordArtBitmaps(name, golden, candidate, heatmapPath);
       }
   }
   ```

2. Replace `src/RBX15.Engine.Tests/GoldenAppTests.cs` (the Step 18 state) with the file below. Scenes whose actions include `wordart` go through `AssertWordArtMatches`; every other scene keeps `AssertMatches`. `Production_stroke_once_matches_the_golden_on_two_point_erase_scenes` and the accounting facts are unchanged.

   ```csharp
   using System.Text.Json;
   using RBX15.Parity;

   namespace RBX15.Engine.Tests;

   /// <summary>
   /// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
   /// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
   /// engine does not run yet is skipped with a reason that names the op; the task that implements
   /// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
   /// (M2b enabled detail, wordart and project one task at a time; PendingOps lists what is still skipped).
   /// <para>
   /// Erase actions run through the internal EraseStroking.Replay41, which re-strokes the eraser path
   /// after every mousemove exactly as app-render.js drove 4.1 (mousedown at points[0], one mousemove
   /// per later point, mouseup with no stroke), so every erase scene meets the unchanged rule.
   /// Production strokes once (approved divergence draw-pen-incremental-stroke); it is held to the
   /// goldens directly on the two-point drags, where 4.1 strokes once too.
   /// </para>
   /// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
   /// </summary>
   public class GoldenAppTests
   {
       static readonly string[] PendingOps = { };

       static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
       static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

       static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
       {
           using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
           return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
       });

       static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

       static JsonElement Scene(string name) => Raw.Value.Single(e => NameOf(e) == name);

       /// <summary>Scenes with a wordart action meet the Word Art rule (ruling R2, GoldenRule.AssertWordArtMatches); every other scene the unchanged rule.</summary>
       static bool HasWordArt(JsonElement e) =>
           e.TryGetProperty("actions", out var actions) && actions.EnumerateArray().Any(a => a.GetProperty("op").GetString() == "wordart");

       /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
       public static TheoryData<string> ScenesBlockedBy(string op)
       {
           var data = new TheoryData<string>();
           foreach (var e in Raw.Value)
           {
               var unsupported = SceneLoader.UnsupportedOps(e);
               if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
           }
           return data;
       }

       /// <summary>Runnable scenes with at least one erase, every erase a two-point drag (one stroke in 4.1).</summary>
       public static TheoryData<string> TwoPointEraseScenes()
       {
           var data = new TheoryData<string>();
           foreach (var e in Raw.Value)
           {
               if (SceneLoader.UnsupportedOps(e).Count != 0 || !e.TryGetProperty("actions", out var actions)) continue;
               var erases = actions.EnumerateArray().Where(a => a.GetProperty("op").GetString() == "erase").ToList();
               if (erases.Count > 0 && erases.All(a => a.GetProperty("points").GetArrayLength() == 2)) data.Add(NameOf(e));
           }
           return data;
       }

       [Theory]
       [MemberData(nameof(ScenesBlockedBy), "")]
       public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
       {
           var scene = SceneLoader.Parse(Scene(name), Root, EraseStroking.Replay41);
           var golden = Path.Combine(Dir, name + ".png");
           var heatmap = Path.Combine(Root, "diff-heatmaps", "app", name + ".png");
           if (HasWordArt(Scene(name)))
               GoldenRule.AssertWordArtMatches(name, golden, scene.Document, heatmap);
           else
               GoldenRule.AssertMatches(name, golden, scene.Document, heatmap);
       }

       [Theory]
       [MemberData(nameof(TwoPointEraseScenes))]
       public void Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)
       {
           var scene = SceneLoader.Parse(Scene(name), Root);
           GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".once.png"));
       }

       [Fact]
       public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
       {
           var unclassified = Raw.Value
               .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
               .ToList();
           Assert.Empty(unclassified);
       }

       [Fact]
       public void Every_scene_is_run_or_skipped_exactly_once_and_skip_rows_match_PendingOps()
       {
           var rows = typeof(GoldenAppTests).GetMethod(nameof(Engine_export_matches_the_frozen_4_1_app_golden))!
               .GetCustomAttributes(typeof(MemberDataAttribute), false).Cast<MemberDataAttribute>()
               .Select(a => (Op: (string)a.Parameters[0], a.Skip))
               .ToList();

           var skipOps = rows.Where(r => r.Skip != null).Select(r => r.Op).ToList();
           Assert.Equal(PendingOps.OrderBy(o => o), skipOps.OrderBy(o => o));
           Assert.Single(rows, r => r.Skip == null && r.Op == "");

           var accounted = rows.Sum(r => ((System.Collections.IEnumerable)ScenesBlockedBy(r.Op)).Cast<object>().Count());
           Assert.Equal(Raw.Value.Count, accounted);
           Assert.Equal(82, Raw.Value.Count);
       }

       [Fact]
       public void Every_app_scene_has_a_golden()
       {
           var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
           Assert.Empty(missing);
       }
   }
   ```

3. Record the divergence (ruling R2) as a new ledger row, `open` until Task 4 closes it, then render and check:

   ```bash
   python - <<'PYEOF'
   import json
   path = "docs/port/parity-ledger.json"
   data = json.load(open(path, encoding="utf-8"))
   ids = [r["id"] for r in data["rows"]]
   assert "wordart-glyph-rasterization" not in ids, "the row is already there"
   row = {
       "id": "wordart-glyph-rasterization",
       "area": "wordart",
       "name": "Word Art glyph rasterization (antialiased glyph edges)",
       "source": "gap:m2b-golden",
       "behavior": "renderWordArt draws its text with fillText and strokeText (editor.js:2166-2236) in the font '900 <size>px \"<family>\", sans-serif' (editor.js:2131-2132). Chromium on Windows rasterizes those glyphs through DirectWrite with ClearType-style coverage: 87 to 184 distinct coverage levels in a fill-only render, and horizontal spreading at stems.",
       "codeRefs": ["editor.js:2131-2132", "editor.js:2166-2236"],
       "quirks": "Measured 2026-10-14 on the 22 frozen Word Art goldens: under the unchanged golden rule every scene fails on glyph edges only (largest edge delta 79). Interior differences are 0 on 21 scenes, and 7 pixels of one level on wordart-outline-anton (x=314, y=147..153). Swapping 4.1's own Word Art bitmap into the engine document matches all 22 goldens exactly.",
       "portTarget": "WordArt draws HarfBuzz-shaped text blobs with the SkFont Chromium's canvas uses (Fonts.CreateFont). SkiaSharp 4.153.1 exposes no font-manager choice, text gamma or contrast, so its glyph coverage differs from DirectWrite's. Exact parity would need DirectWrite glyph-run analysis through COM interop, which M2b does not take.",
       "risk": "low",
       "parityCheck": "GoldenAppTests holds the 22 Word Art scenes to GoldenRule.AssertWordArtMatches: same size, every interior pixel within 1 level per channel, edge pixels unbounded. WordArtSwapTests swaps 4.1's own Word Art bitmap (tests/goldens/app/wordart-ref) into the engine document and requires an exact match under the unchanged GoldenRule.",
       "owner": "Engine.WordArt",
       "check": "golden",
       "disposition": "fix",
       "divergence": "4.1: Chromium rasterizes Word Art glyphs through DirectWrite with ClearType-style coverage. v5: SkiaSharp's rasterizer smooths glyph edges differently; interiors match within 1 level, layout and placement match exactly (pinned by the 4.1-bitmap swap test).",
       "status": "open",
   }
   assert list(row) == list(data["rows"][0]), "field order differs from the ledger's rows"
   data["rows"].insert(ids.index("wordart-style-retro") + 1, row)
   open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
   print("added wordart-glyph-rasterization after wordart-style-retro")
   PYEOF
   python tools/ledger/ledger.py render
   python tools/ledger/ledger.py check
   git diff --stat -- docs/port
   ```

   Expected: `added wordart-glyph-rasterization after wordart-style-retro`, `rendered docs/port/parity-ledger.md and docs/port/divergences.md`, `408 rows, 0 problem(s)`, and `3 files changed, 24 insertions(+), 2 deletions(-)`: 20 JSON lines for the row, one new row plus the counts line in `parity-ledger.md`, and one new row plus the `Fixed in v5 (185)` heading in `divergences.md`.

4. Run everything:

   ```bash
   dotnet build src/RBX15.slnx -c Release -warnaserror
   dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~GoldenRuleWordArtTests|FullyQualifiedName~WordArtSwapTests|FullyQualifiedName~GoldenAppTests" 2>&1 | grep -E "(Passed|Failed)!"
   dotnet test src/RBX15.slnx -c Release > .superpowers/logs/m2b-wordart-ruled.log 2>&1; grep -E "(Passed|Failed)!" .superpowers/logs/m2b-wordart-ruled.log
   ```

   Expected:

   - `0 Warning(s)`, `0 Error(s)`.
   - The filtered run: `Passed!  - Failed:     0, Passed:   115, Skipped:     0, Total:   115` for `RBX15.Engine.Tests.dll` (4 rule cases, 23 swap cases, 88 `GoldenAppTests` cases).
   - The full run: Engine.Tests `Passed: 516, Failed: 0, Skipped: 0` (467 + 22 Word Art scenes + 4 + 23); Parity.Tests `Passed: 35, Failed: 0` (31 + 4).

   If a Word Art scene fails `AssertWordArtMatches`, the message names its interior maximum: an interior pixel more than 1 level off is an engine bug, so debug it with Step 20's list. If a swap case fails, placement, the 85% fit, the un-premultiplied round trip or the downscaled draw is wrong; its heatmap is `diff-heatmaps/app/<scene>.swap.png`. Never change the rule, `ImageDiff`, the goldens or `tests/goldens/app/wordart-ref`. If the test host crashes, see the crash note in this task's risks: diagnose it, do not rerun past it.

5. Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

   ```bash
   git add src/RBX15.Parity/SceneLoader.cs src/RBX15.Parity.Tests/SceneLoaderWordArtTests.cs src/RBX15.Engine.Tests/GoldenAppTests.cs src/RBX15.Engine.Tests/GoldenRule.cs src/RBX15.Engine.Tests/GoldenRuleWordArtTests.cs src/RBX15.Engine.Tests/WordArtSwapTests.cs docs/port
   git commit -m "test(engine): every wordart golden met under the Word Art rule, layout held exact by 4.1's bitmap; the wordart op runs"
   git status --short
   ```

   Expected: `git status --short` is empty.

- [ ] **Step 24: Hand the ledger evidence to Task 4**

Don't run `ledger.py done` here. Task 4 closes rows in one batch and decides closure; several rows below stay open there because the App or UI owns part of their text.

**Evidence this task supplies (Task 4 decides closure):**

| Row | Evidence |
| --- | --- |
| `wordart-render-measure` | `Lines_are_shaped_to_chromiums_measureText_width`, `A_character_the_family_lacks_is_shaped_in_a_system_fallback_face`, `Plain_text_shapes_as_one_run_in_the_vendored_face`, `Canvas_size_is_4_1s_cw_by_ch`, `Pad_is_the_largest_effect_allowance`, `A_trailing_newline_adds_an_empty_line`, `The_preview_clamps_the_canvas_and_scales_the_content` |
| `wordart-add-to-canvas` | `Add_to_canvas_fits_85_percent_of_the_front_slot_and_centers` (Shirt and Pants), `Word_art_lands_above_the_scene_layers_with_the_next_id` |
| `wordart-edge-cases` | `Empty_text_renders_TEXT`, `Size_falls_back_to_64_and_clamps_to_8_through_300`, `Outline_with_stroke_width_0_draws_at_3`, `Solid_with_stroke_width_0_draws_no_stroke`, `Neon_ignores_the_stroke_settings`, `The_angle_only_moves_the_gradient` |
| `wordart-layer-name-emoji` | `Layer_names_follow_4_1` (20 UTF-16 units, newline kept, empty text) |
| `font-wordart-runtime-google` (golden, fix) | the 22 Word Art goldens under the Word Art rule, plus `Every_word_art_family_resolves_at_900`, `The_build_output_carries_a_byte_identical_copy_of_assets_fonts`, `An_unknown_family_throws` |
| `wordart-glyph-rasterization` (golden, fix; added in Step 23) | the 22 Word Art goldens under `GoldenRule.AssertWordArtMatches`; `WordArtSwapTests` (22 exact swaps and the fixture-set fact); `GoldenRuleWordArtTests` (the rule's bound) |
| `wordart-font-list` (faces only) | `A_900_request_is_synthetic_bold_only_on_faces_below_600`, `Variable_files_draw_at_the_declared_weight`, `Synthetic_bold_keeps_the_advance` |
| `wordart-style-solid` | the Solid goldens under the Word Art rule, plus `Solid_with_stroke_width_0_draws_no_stroke` |
| `wordart-style-gradient`, `-neon`, `-shadow`, `-outline`, `-retro` | the 22 Word Art goldens under the Word Art rule, and their swap cases |

---

### Task 4: Carry-forwards, ledger close and final verification

**Files:**

- Modify: `src/RBX15.Engine/Renderer.cs` (the decode-cache row of the Chromium-compat index, and the `DecodeCacheSize` doc comment)
- Modify: `src/RBX15.Engine/Images.cs` (the `DecodeCacheScaled` and `SkiaMipLevel` doc comments)
- Test: `src/RBX15.Engine.Tests/RendererTests.cs` (one characterization fact that pins the `SkiaMipLevel` bound)
- Modify: `docs/port/parity-ledger.json` (ruling R4's amendment of `project-load-field-coercion`, then `ledger.py done`), and the generated `docs/port/parity-ledger.md` and `docs/port/divergences.md`

**Interfaces:**

- Consumes:
  - `Renderer.DecodeCacheSize`, `Images.DecodeCacheScaled`, `Images.SkiaMipLevel` (M1, unchanged in behavior here; `RBX15.Engine.csproj` already has `InternalsVisibleTo RBX15.Engine.Tests`).
  - `ProjectIO.Read`, `ProjectIO.Write`, `ProjectLoad`, `ProjectFormatException`, `History.Clear` (Task 1).
  - `DetailType`, `Details.FromId`, `DetailStyle`, `Details.Create` (Task 2).
  - `WordArtStyle`, `WordArtSettings`, `WordArt.Render`, `WordArt.CreateLayer`, `Fonts.Get` (Task 3).
  - `SceneLoader.SupportedOps` holding `detail`, `wordart` and `project`, and `GoldenAppTests.PendingOps` holding no op (Tasks 1-3), so all 82 app scenes run.
  - The Word Art rule from Task 3 Step 23 (ruling R2): `GoldenRule.AssertWordArtMatches` on the 22 Word Art scenes, `WordArtSwapTests` on the frozen `tests/goldens/app/wordart-ref`, and the ledger row `wordart-glyph-rasterization` (open).
  - `python tools/ledger/ledger.py done <id...>` and `check` (M1 Task 7).
- Produces:
  - Index and doc-comment wording that tells cc decode-cache levels from Skia mip levels, names the `ScalePixels` Linear + MipmapNearest dependency, and states the measured bound on the `SkiaMipLevel` rule, pinned by `Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n`.
  - `project-load-field-coercion` amended to a `fix` row (ruling R4).
  - The ledger with the rows M2b proves set to `done` (26 if every gate holds: 55 to 81 done), and the rows left open, with reasons, in the commit body.
  - A clean, verified branch. Push, PR and dashboard steps belong to the controller and are not part of this task.

**The Word Art state.** Task 4 starts only after Task 3 has committed Step 23, with every app scene running. Then `src/RBX15.Engine.Tests/GoldenRule.cs` carries the Word Art rule of ruling R2 (`WordArtResult`, `CompareWordArt`, `AssertWordArtBitmaps`, `AssertWordArtMatches`) and nothing else new, the 22 Word Art scenes pass under it, the 23 `WordArtSwapTests` cases pass under the unchanged rule, and the ledger has 408 rows (407 plus `wordart-glyph-rasterization`). If Step 23 is not committed, do not start this task: report to the controller.

**The rule for closing** (as in M2a). The ledger is closed conservatively, and only here. A row closes only if Tasks 1-3 prove its whole behavior text (or, for a `fix` row, its whole v5 divergence sentence) in a unit test or a golden that exists on this branch. A half owned by the UI (`App.*`), by the caller's one-step-per-gesture semantics (M3/M4, per the History contract), or a clause that conflicts with an approved divergence keeps the row open.

- [ ] **Step 1: Record the baseline Tasks 1-3 left**

Run from the repo root:

```bash
mkdir -p .superpowers/logs
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet test src/RBX15.slnx -c Release > .superpowers/logs/m2b-t4-baseline.log 2>&1
grep -E "(Passed|Failed)!" .superpowers/logs/m2b-t4-baseline.log
git diff --stat "$BASE" -- src/RBX15.Engine.Tests/GoldenRule.cs
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done')"
```

Expected:

- `base db6b1cd` (the `main` tip M2b branched from).
- Two summary lines, both `Failed: 0` and `Skipped: 0`: Engine.Tests `Passed: 516` (Task 3 Step 14's 467, the 22 Word Art scenes, 4 rule cases and 23 swap cases) and Parity.Tests `Passed: 35` (31 + 4).
- The `GoldenRule.cs` diff shows only the Word Art rule added at the end of the class.
- `408 rows; 55 done`. Tasks 1-3 close no rows.

If either summary shows `Failed` above 0 or `Skipped` above 0, stop and report it to the controller: Tasks 1-3 are not finished.

- [ ] **Step 2: Write the characterization fact for the `SkiaMipLevel` bound**

M2a's final review asked the `SkiaMipLevel` comment to state where the fitted geometric-mean rule and the largest-axis rule (the other rule both goldens fit) agree. Measured on 2026-10-14, they do **not** agree on every ceil-halved cc target: they agree whenever the source is at least 2^n pixels on both axes at cc level n (both axis scales then lie in [2^-n, 2^(1-n)), so both rules give n at an exact halving and n-1 otherwise), and they differ only where a short axis has been clamped to 1 pixel. Exhaustive runs of the real `DecodeCacheSize` found 0 differences over every background pre-scale (sources 1..4096 on each axis, stretched to 585x559, 8,725,440 pre-scales) and 30,538 differences among 651,309 distinct uniform-scale layer targets (sources 1..300 on each axis), every one with a 1-pixel short side (for example 3x6 to 1x1: geometric mean 2, largest axis 1). The fact below pins the bound, not the stronger claim.

Add this fact to `src/RBX15.Engine.Tests/RendererTests.cs`, directly after `Decode_cache_prescales_only_at_mip_level_one_or_more` (after its closing `}` and before the `/// <summary>` of `Rotated_layer_composites_in_float_not_lowp`):

```csharp
    /// <summary>
    /// Pins the bounded uncertainty in Images.SkiaMipLevel. Two goldens fit the geometric-mean rule, and
    /// the largest-axis rule fits them too. On a cc level-n target whose source is at least 2^n on both
    /// axes, both axis scales lie in [2^-n, 2^(1-n)), so the two rules pick the same level. They part only
    /// where a short axis has been clamped to 1 pixel, which no golden exercises.
    /// </summary>
    [Fact]
    public void Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n()
    {
        static int LargestAxis(int sw, int sh, int w, int h)
        {
            double s = Math.Max((double)w / sw, (double)h / sh);
            return s >= 1 ? 0 : (int)Math.Floor(Math.Log2(1 / s));
        }
        int checkedTargets = 0;
        for (int sw = 1; sw <= 128; sw++)
            for (int sh = 1; sh <= 128; sh++)
                for (int n = 1; (1 << n) <= Math.Min(sw, sh); n++)
                {
                    int w = (sw + (1 << n) - 1) >> n, h = (sh + (1 << n) - 1) >> n;   // cc's ceil-halved target
                    Assert.True(Images.SkiaMipLevel(sw, sh, w, h) == LargestAxis(sw, sh, w, h), $"{sw}x{sh} at cc level {n}");
                    checkedTargets++;
                }
        Assert.Equal(72799, checkedTargets);
        Assert.Equal(1, Images.SkiaMipLevel(198, 125, 50, 32));   // gap-layer-mip2: cc level 2, Skia mip level 1
        Assert.Equal(0, Images.SkiaMipLevel(198, 125, 99, 63));    // full-scene: cc level 1, Skia mip level 0
        Assert.Equal(1, Images.SkiaMipLevel(2160, 2160, 1080, 1080)); // gap-bg-image-prescale: cc level 1, Skia mip level 1
        Assert.Equal(2, Images.SkiaMipLevel(3, 6, 1, 1));          // outside the bound: largest-axis would pick 1
        Assert.Equal(1, LargestAxis(3, 6, 1, 1));
    }
```

The three golden sizes come from the frozen scenes: `packs/word_art/word_626_anton.png` is 198x125, drawn at scale 0.5 in `full-scene` (target 99x63, cc level 1) and at 0.2 in `gap-layer-mip2` (target 40x25, cc level 2 to 50x32); `tests/goldens/fixtures/bg-large-2160.png` is 2160x2160, stretched to 585x559 in `gap-bg-image-prescale` (cc level 1 to 1080x1080).

- [ ] **Step 3: Run it, then watch it bite**

This is a characterization test of shipped code, so it passes on the first run. Prove it can fail by swapping in SkiaSharp's own smaller-axis rule for one run.

```bash
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~Skia_mip_level_matches_the_largest_axis_rule" 2>&1 | grep -E "(Passed|Failed)!"
sed -i 's|double scale = Math.Sqrt((double)width / srcWidth \* ((double)height / srcHeight));|double scale = Math.Min((double)width / srcWidth, (double)height / srcHeight);|' src/RBX15.Engine/Images.cs
grep -n "double scale" src/RBX15.Engine/Images.cs
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~Skia_mip_level_matches_the_largest_axis_rule" 2>&1 | grep -E "at cc level|(Passed|Failed)!"
git checkout -- src/RBX15.Engine/Images.cs
git diff --stat -- src/RBX15.Engine/Images.cs
```

Expected:

- First run: `Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1` for `RBX15.Engine.Tests.dll`.
- The `grep` shows line 69 with `Math.Min(...)`.
- Mutated run: `2x3 at cc level 1` and `Failed!  - Failed:     1, Passed:     0`.
- After `git checkout`, the `git diff --stat` prints nothing. Images.cs has no other edits yet, so the checkout restores it exactly. If it printed anything, restore the `Math.Sqrt` line by hand before going on.

- [ ] **Step 4: Fix the decode-cache wording**

Make four exact replacements (Edit tool; the files are CRLF in the working tree, which the Edit tool keeps).

In `src/RBX15.Engine/Renderer.cs`, the index row (lines 23-24). Replace:

```csharp
/// <item><term>cc decode cache: pre-scale at mip level 1 or more (target at or below 0.5), medium quality through Skia's mip chain at the geometric-mean level</term>
///   <description><c>Renderer.DecodeCacheSize</c>, <c>Images.DecodeCacheScaled</c>, <c>Images.SkiaMipLevel</c>; full-scene (scaled-down word art), gap-layer-mip2 (mip level 2), gap-bg-image-prescale (background pre-scale)</description></item>
```

with:

```csharp
/// <item><term>cc decode cache: at cc level 1 or more (target at or below 0.5) pre-scale to the source ceil-divided by 2^level, at medium quality through Skia's mip chain at the geometric-mean Skia mip level. cc levels and Skia mip levels are counted separately. The chain is built with SKImage.ScalePixels at Linear + MipmapNearest, which relies on SkiaSharp resolving a floor-halving to Skia mip level 1 and copying it unscaled</term>
///   <description><c>Renderer.DecodeCacheSize</c>, <c>Images.DecodeCacheScaled</c>, <c>Images.SkiaMipLevel</c>; full-scene (word art 198x125 at 0.5: cc level 1 to 99x63, Skia mip level 0), gap-layer-mip2 (word art 198x125 at 0.2: cc level 2 to 50x32, Skia mip level 1; the name counts cc levels), gap-bg-image-prescale (2160x2160 to 585x559: cc level 1 to 1080x1080, Skia mip level 1) (unit tests Decode_cache_prescales_only_at_mip_level_one_or_more, Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n)</description></item>
```

In `src/RBX15.Engine/Renderer.cs`, the `DecodeCacheSize` summary. Replace:

```csharp
    /// cc's software decode cache (CacheKey::FromDrawImage with MipMapUtil): the target is the source
    /// scaled and rounded; the mip level is how many ceil-halvings of the source stay at or above
    /// the target on both axes. Level 0 draws the original (null); level n pre-scales to the source
    /// ceil-divided by 2^n.
```

with:

```csharp
    /// cc's software decode cache (CacheKey::FromDrawImage with MipMapUtil): the target is the source
    /// scaled and rounded; the cc level is how many ceil-halvings of the source stay at or above
    /// the target on both axes. cc level 0 draws the original (null); cc level n pre-scales to the
    /// source ceil-divided by 2^n. The Skia mip level that pre-scale samples is a separate count
    /// (<see cref="Images.SkiaMipLevel"/>).
```

In `src/RBX15.Engine/Images.cs`, the `DecodeCacheScaled` summary. Replace:

```csharp
    /// drawn downscaled to a mip level of 1 or more (cc SoftwareImageDecodeCacheUtils::CacheKey::FromDrawImage):
```

with:

```csharp
    /// drawn downscaled to a cc level of 1 or more (cc SoftwareImageDecodeCacheUtils::CacheKey::FromDrawImage):
```

In `src/RBX15.Engine/Images.cs`, the `SkiaMipLevel` summary. Replace:

```csharp
    /// <summary>
    /// The mip level Chromium's Skia samples for a medium-quality draw at this size: log2 of the
    /// inverse geometric-mean scale, rounded down (Chromium builds Skia with
    /// SK_SUPPORT_LEGACY_ANISOTROPIC_MIPMAP_SCALE; SkiaSharp's Skia takes the smaller axis scale,
    /// which mips the full-scene word art at exactly 0.5 where 4.1 does not). 0 means no mipmap.
    /// Fitted: gap-layer-mip2 (198x125 to 50x32, level 1) and full-scene (198x125 to 99x63, level 0).
    /// </summary>
```

with:

```csharp
    /// <summary>
    /// The Skia mip level (not a cc decode-cache level) Chromium's Skia samples for a medium-quality
    /// draw at this size: log2 of the inverse geometric-mean scale, rounded down (Chromium builds Skia
    /// with SK_SUPPORT_LEGACY_ANISOTROPIC_MIPMAP_SCALE; SkiaSharp's Skia takes the smaller axis scale,
    /// which mips the full-scene word art at exactly 0.5 where 4.1 does not). 0 means no mipmap.
    /// Fitted: gap-layer-mip2 (198x125 to 50x32: cc level 2, Skia mip level 1) and full-scene
    /// (198x125 to 99x63: cc level 1, Skia mip level 0).
    /// <para>
    /// Bounded uncertainty: the largest-axis rule fits both goldens too. The two rules agree on every
    /// cc level-n target whose source is at least 2^n on both axes, because both axis scales then lie in
    /// [2^-n, 2^(1-n)). Measured on 2026-10-14, they also agree on every background pre-scale (sources up
    /// to 4096x4096 stretched to 585x559) and on every uniform-scale layer target whose shorter side is 2
    /// or more (sources up to 300x300). They differ only where a short axis is clamped to 1 pixel (3x6 to
    /// 1x1: geometric mean 2, largest axis 1), which no golden exercises. Pinned by
    /// Skia_mip_level_matches_the_largest_axis_rule_wherever_both_axes_reach_2_to_the_n.
    /// </para>
    /// </summary>
```

Build and run the renderer tests:

```bash
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RendererTests" 2>&1 | grep -E "(Passed|Failed)!"
git diff --stat
```

Expected:

- `0 Warning(s)` and `0 Error(s)`. The `<see cref="Images.SkiaMipLevel"/>` resolves (same assembly, internal is fine).
- `Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20` (main's 19 `RendererTests` cases plus the new fact; Tasks 1-3 add none here).
- `git diff --stat` lists exactly `src/RBX15.Engine.Tests/RendererTests.cs`, `src/RBX15.Engine/Images.cs` and `src/RBX15.Engine/Renderer.cs`. Only comments changed in the two engine files.

- [ ] **Step 5: Commit the carry-forward**

Add your own model's `Co-Authored-By` trailer as the last line (Global Constraints) before running it.

```bash
git add src/RBX15.Engine/Renderer.cs src/RBX15.Engine/Images.cs src/RBX15.Engine.Tests/RendererTests.cs
git commit -F - <<'EOF'
docs(engine): tell cc levels from Skia mip levels in the decode-cache notes

The Chromium-compat index row and the DecodeCacheSize, DecodeCacheScaled
and SkiaMipLevel comments now name which count they mean: gap-layer-mip2
is cc level 2 (pre-scaled to 50x32) sampled at Skia mip level 1. The row
also names the ScalePixels Linear + MipmapNearest dependency of the mip
chain, a place a SkiaSharp upgrade can break goldens.

SkiaMipLevel states its bounded uncertainty: the largest-axis rule fits
both goldens too, and the two rules agree on every cc level-n target whose
source reaches 2^n on both axes. Measured: 0 differences over every
background pre-scale (sources up to 4096x4096), and 30,538 of 651,309
uniform-scale layer targets (sources up to 300x300) differ, all with a
1-pixel short side. A characterization fact pins the bound.
EOF
git log --oneline -1
```

Expected: one commit with the subject `docs(engine): tell cc levels from Skia mip levels in the decode-cache notes`.

- [ ] **Step 6: Confirm the evidence for each ledger row before closing it**

Candidate rows (26). The golden scenes are exact names in `tests/goldens/app/scenes.json`. The unit pins are behaviors Tasks 1-3 must have written; find each one's test name in the listing below.

| Row | Check | Behavior that must be pinned | Golden scenes | Unit pin (owning task) | 4.1 source |
| --- | --- | --- | --- | --- | --- |
| `detail-collar-v` | golden | V path (cx-30, top), (cx, top+28), (cx+30, top). | `detail-collar-v`, `detail-collar-v-stitch` | none | `editor.js:1868-1877` |
| `detail-collar-crew` | golden | Front U of two quads plus the back collar quad, one layer. | `detail-collar-crew`, `detail-collar-crew-stitch` | none | `editor.js:1878-1894` |
| `detail-collar-scoop` | golden | One quad, front only. | `detail-collar-scoop`, `detail-collar-scoop-stitch` | none | `editor.js:1895-1903` |
| `detail-collar-henley` | golden | Crew, placket line, 3 buttons of radius = line width, undashed fill. | `detail-collar-henley`, `detail-collar-henley-stitch`, `detail-collar-henley-w8` | none | `editor.js:1904-1922` |
| `detail-cuffs` | golden | Left, right and both cuffs, 8 px above each arm face's bottom. | `detail-cuff-left`, `detail-cuff-left-stitch`, `detail-cuff-right`, `detail-cuff-right-stitch`, `detail-cuffs-both`, `detail-cuffs-both-stitch` | none | `editor.js:1923-1947` |
| `detail-hem` | golden | Four lines 4 px above the torso bottoms. | `detail-hem`, `detail-hem-stitch` | none | `editor.js:1948-1965` |
| `detail-seams` | golden | Center seam, and the four side seams. | `detail-seam-center`, `detail-seam-center-stitch`, `detail-seam-sides`, `detail-seam-sides-stitch`, `detail-seam-sides-w1-stitch` | none | `editor.js:1966-1995` |
| `detail-pocket` | golden | Open-top rectangle at the subpixel origin x=310.36. | `detail-pocket`, `detail-pocket-stitch` | none | `editor.js:1996-2008` |
| `detail-placket` | golden | Two lines and 7 buttons of radius max(1.5, 0.8w), widths 1, 2 and 8. | `detail-placket`, `detail-placket-stitch`, `detail-placket-w1`, `detail-placket-w8-stitch` | none | `editor.js:2009-2030` |
| `detail-shoulder-stripe` | golden | Three horizontal lines. | `detail-shoulder-stripe`, `detail-shoulder-stripe-stitch` | none | `editor.js:2031-2048` |
| `detail-arm-bands` | golden | Eight lines at y = r.y + r.h*0.35. | `detail-arm-bands`, `detail-arm-bands-stitch` | none | `editor.js:2049-2059` |
| `detail-yoke` | golden | One line on the back at 0.22 height. | `detail-yoke`, `detail-yoke-stitch` | none | `editor.js:2060-2068` |
| `detail-full-set` | golden | Crew, hem, both cuffs and side seams in ONE layer named `Full Detail Set`. | `detail-full-detail`, `detail-full-detail-stitch`, `detail-full-detail-w8-stitch` | The layer name for each of the 16 types, `Full Detail Set` included (Task 2) | `editor.js:2069-2075`, `:2083-2098` |
| `details-pants-mode-behavior` | unit (fix) | v5: every generator resolves through `Template.Slot` and runs in Pants. | none (no Pants goldens) | Each of the 16 `DetailType`s in Pants returns a layer without throwing (Task 2) | `editor.js:1868-2075` |
| `details-mode-independence-and-selection` | unit | No selection needed; the result is a normal image layer at absolute template position; repeated creation stacks identical duplicates. | none | Creating the same detail twice gives two layers with equal position, size and pixels and distinct ids (Task 2, `Two_clicks_stack_two_identical_layers`) | `editor.js:1834-1866` |
| `wordart-style-solid` | golden | Fill, then for stroke > 0 a round-join stroke and a refill; stroke 0 is fill only. | `wordart-solid-anton`, `wordart-solid-poppins`, `wordart-solid-dancing`, `wordart-stroke-12-anton`, `wordart-multiline-orbitron` | Solid with `StrokeWidth` 0 draws no stroke (Task 3, `Solid_with_stroke_width_0_draws_no_stroke`) | `editor.js:2219-2231` |
| `wordart-style-gradient` | golden | Per-line linear gradient over (cos, sin) times (cw/2, ch/2), Fill to Grad. | `wordart-gradient-anton`, `wordart-gradient-poppins`, `wordart-gradient-dancing`, `wordart-gradient-135-poppins` | none | `editor.js:2171-2179` |
| `wordart-style-neon` | golden | Two shadow passes (blur 20, 40), white core at 0.8; stroke ignored; pad 30. | `wordart-neon-anton`, `wordart-neon-poppins`, `wordart-neon-dancing` | none | `editor.js:2145`, `:2198-2213` |
| `wordart-style-shadow` | golden | Black 60% copy at (+4, +4), then the solid path; pad 15. | `wordart-shadow-anton`, `wordart-shadow-poppins`, `wordart-shadow-dancing` | none | `editor.js:2145`, `:2182-2187` |
| `wordart-style-outline` | golden | Stroke only in the Fill color, width `strokeW \|\| 3`. | `wordart-outline-anton`, `wordart-outline-poppins`, `wordart-outline-dancing`, `wordart-outline-stroke-0-dancing` | none | `editor.js:2214-2218` |
| `wordart-style-retro` | golden | Copies i = 8..1 at (+i, +i), even i Stroke color, odd i Grad color; pad 12. | `wordart-retro-anton`, `wordart-retro-poppins`, `wordart-retro-dancing` | none | `editor.js:2145`, `:2190-2195` |
| `font-wordart-runtime-google` | golden (fix) | v5: all 15 Word Art families are bundled. | the 22 Word Art scenes (every `wordart-*` scene above) | `Fonts.Get` resolves each of the 15 families of `editor.html:11` at weight 900 from `assets/fonts`, never a system face standing in for the family (Task 3); the bundle itself is `VendoredFontsTests` (M2a) | `editor.html:11`, `editor.js:2131` |
| `wordart-glyph-rasterization` | golden (fix) | v5: glyph edges differ from DirectWrite's; interiors match within 1 level, layout and placement exactly. | the 22 Word Art scenes, under `GoldenRule.AssertWordArtMatches` | The rule's bound (`GoldenRuleWordArtTests`, Task 3) and the exact 4.1-bitmap swap on all 22 scenes plus the fixture-set fact (`WordArtSwapTests`, Task 3) | `editor.js:2131-2132`, `:2166-2236` |
| `layer-fields-schema` | unit | All 18 layer fields persist; missing fields default as `makeLayer`. | `project-shirt-rich`, `project-pants-legacy`, `project-shirt-drawn` | A layer record holding only `id` and `imgSrc` reads with the 4.1 defaults, and `Read(Write(doc))` reproduces every field (Task 1) | `editor.js:134-142`, `:2635-2652` |
| `project-version-compat` | unit (fix) | v5: reads every 4.1 file (version '4.0' or absent, legacy color-string fills) and writes version '5.0'. | `project-shirt-rich`, `project-pants-legacy`, `project-shirt-drawn` | `Write` emits `"version": "5.0"`; `Read` accepts version `4.0` and a file with no version (Task 1) | `editor.js:2550-2592`, `:2593-2680` |
| `project-load-field-coercion` | unit (fix, ruling R4) | The coercions within each field's type, and v5: an unknown mode refuses the whole file; nothing is applied. | none | An unknown mode (`pants`, `Robot`, a number) throws `ProjectFormatException` naming it (Task 1, `A_structural_problem_throws_naming_it`); a refusal applies nothing (`A_problem_in_the_last_layer_refuses_the_file_even_after_valid_layers`); the coercions (`Layer_fields_coerce_like_loadProject`, `Top_level_falsy_and_null_fields_take_4_1s_defaults`) | `editor.js:2598-2621`, `:2635-2653`, `:1695-1709` |

List the unit tests Tasks 1-3 wrote:

```bash
grep -n "public void\|public async Task" src/RBX15.Engine.Tests/*ProjectIO*.cs src/RBX15.Engine.Tests/*Detail*.cs src/RBX15.Engine.Tests/*WordArt*.cs src/RBX15.Engine.Tests/*Font*.cs
```

Expected: the method names of every M2b test file (and `VendoredFontsTests`). For each row with a unit pin, find the test that pins it and write its name next to the row. Write down each row whose pin has no test.

Check that every cited golden scene exists and passed. Save this script as `.superpowers/logs/m2b-evidence.py` (git-ignored), then run the app gate at normal verbosity and feed it the log:

```python
import json, re, sys
log = open(sys.argv[1], encoding="utf-8", errors="replace").read()
passed = set(re.findall(r'^\s+Passed RBX15\.Engine\.Tests\.GoldenAppTests\.Engine_export_matches_the_frozen_4_1_app_golden\(name: "([^"]+)"\)', log, re.M))
skipped = set(re.findall(r'^\s+Skipped RBX15\.Engine\.Tests\.GoldenAppTests\.Engine_export_matches_the_frozen_4_1_app_golden\(name: "([^"]+)"\)', log, re.M))
names = {s["name"] for s in json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))}
def both(*types):
    return [f"detail-{t}{v}" for t in types for v in ("", "-stitch")]
def styles(style):
    return [f"wordart-{style}-{f}" for f in ("anton", "poppins", "dancing")]
rows = {
    "detail-collar-v": both("collar-v"),
    "detail-collar-crew": both("collar-crew"),
    "detail-collar-scoop": both("collar-scoop"),
    "detail-collar-henley": both("collar-henley") + ["detail-collar-henley-w8"],
    "detail-cuffs": both("cuff-left", "cuff-right", "cuffs-both"),
    "detail-hem": both("hem"),
    "detail-seams": both("seam-center", "seam-sides") + ["detail-seam-sides-w1-stitch"],
    "detail-pocket": both("pocket"),
    "detail-placket": both("placket") + ["detail-placket-w1", "detail-placket-w8-stitch"],
    "detail-shoulder-stripe": both("shoulder-stripe"),
    "detail-arm-bands": both("arm-bands"),
    "detail-yoke": both("yoke"),
    "detail-full-set": both("full-detail") + ["detail-full-detail-w8-stitch"],
    "wordart-style-solid": styles("solid") + ["wordart-stroke-12-anton", "wordart-multiline-orbitron"],
    "wordart-style-gradient": styles("gradient") + ["wordart-gradient-135-poppins"],
    "wordart-style-neon": styles("neon"),
    "wordart-style-shadow": styles("shadow"),
    "wordart-style-outline": styles("outline") + ["wordart-outline-stroke-0-dancing"],
    "wordart-style-retro": styles("retro"),
    "layer-fields-schema": ["project-shirt-rich", "project-pants-legacy", "project-shirt-drawn"],
    "project-version-compat": ["project-shirt-rich", "project-pants-legacy", "project-shirt-drawn"],
}
rows["font-wordart-runtime-google"] = sorted({s for k, v in rows.items() if k.startswith("wordart-style-") for s in v})
rows["wordart-glyph-rasterization"] = rows["font-wordart-runtime-google"]
bad = 0
for row, scenes in rows.items():
    unknown = [s for s in scenes if s not in names]
    failing = [s for s in scenes if s in names and s not in passed]
    ok = not unknown and not failing
    bad += not ok
    print(("proven   " if ok else "OPEN     ") + row + ("" if ok else f"  unknown={unknown} not-passed={failing}"))
covered = {s for ss in rows.values() for s in ss}
print(f"{len(covered)} distinct scenes cited; app gate: {len(passed)} scene rows passed, {len(skipped)} skipped")
sys.exit(1 if bad else 0)
```

```bash
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" --logger "console;verbosity=normal" > .superpowers/logs/m2b-app-golden.log 2>&1
python .superpowers/logs/m2b-evidence.py .superpowers/logs/m2b-app-golden.log
```

Expected: `proven` for each of the 23 rows, then `62 distinct scenes cited; app gate: 82 scene rows passed, 0 skipped`, and exit 0. On `main` the same script prints `OPEN` for every row with `20 scene rows passed, 62 skipped` (the drafters verified 21 rows on 2026-10-14; `font-wordart-runtime-google` and `wordart-glyph-rasterization` add no new scene), so an `OPEN` row here means its scenes did not run green. `project-load-field-coercion` has no golden scene, so the script does not list it; its pins are the unit tests above.

Then apply the gate:

- **An `OPEN` row, or a unit pin with no test:** take that row out of the Step 7 command and add it, with the reason, to the "left open" list in the Step 9 commit body. Report it to the controller. Do not write the missing test here; closing the gap belongs to the task that owns the module.
- **The Word Art rows** (`wordart-style-*`, `font-wordart-runtime-google`, `wordart-glyph-rasterization`) close under the Word Art rule, because ruling R2 made that rule plus the exact swap guard the approved proof and recorded the divergence. A failing `WordArtSwapTests` case keeps all of them open.

These rows stay open deliberately. Copy the list into the Step 9 commit body:

| Rows | Owner | Why open |
| --- | --- | --- |
| `project-save-format` | Engine.ProjectIO | The Save button, its tooltip and the download are App behavior, and "Electron opens a native file" is not tested in M2b. |
| `project-load-flow` | Engine.ProjectIO | The file picker, the input reset, the bgColor and bgOpacity control resync and the region-button repaint are App behavior. |
| `project-load-layers`, `file-project-format`, `project-format-r15proj`, `project-load-untrusted-src`, `project-load-error-path-bugs` | Engine.ProjectIO | Each divergence promises a visible warning naming dropped layers. The engine returns `ProjectLoad.DroppedLayers`; the warning and the top-layer selection are App behavior. |
| `project-load-no-imgsrc-hang`, `project-load-history-interplay` | Engine.ProjectIO | Their divergence clears history on open. `History.Clear` is proven (Task 1), but the Open command that calls it is the caller's (M3/M4). |
| `project-template-name-not-saved` | Engine.ProjectIO | The template-name field's state across Save and Open is App behavior. |
| `details-layer-creation-pipeline` | Engine.Details | saveUndo, pushing on top, selecting the layer and switching to Select are App behavior. |
| `details-buttons` | Engine.Details | The button grouping is UI, and "one real undo step per action" is the caller's (M3/M4). |
| `wordart-font-list` | Engine.WordArt | The dropdown labels are UI, and 11 of the 15 fonts have no golden (the scenes use Anton, Poppins, Dancing Script and Orbitron). |
| `wordart-font-loading-race` | Engine.WordArt | The first-frame preview is App.Ribbon.Text behavior. |
| `wordart-render-measure` | Engine.WordArt | Its `+waSize \|\| 64` clause is superseded by the 8-300 clamp recorded on `wordart-state-defaults` (App.Ribbon.Text), and the 200x80 preview path is the preview's. |
| `wordart-add-to-canvas` | Engine.WordArt | saveUndo, selection and the tool reset are App behavior; `WordArt.CreateLayer` is the engine half. |
| `wordart-edge-cases` | Engine.WordArt | Style-button exclusivity, font-select validity and no persistence of settings are UI. |
| `fonts-google-runtime` | Engine.WordArt | "Bundled with the app and zero network requests" is proven by the App build output and a network check, not by the engine. |
| `text-styles` | Engine.WordArt | The 3x2 style-button grid is UI. |
| `wordart-layer-name-emoji` | Engine.WordArt | Showing the name in the layer list with a color emoji font is App.Layers behavior. |

M2b closes no row outside the three owners. `mode-pants-crash-hardcoded-keys` and `bug-pants-mode-region-keys` (Engine.Template) also cover addLayer, Fit Front, Center and Sleeve Pop, which M2b does not port, and `text-add-to-canvas` (App.Ribbon.Text) is a UI row.

- [ ] **Step 7: Close the proven rows**

Print the state first:

```bash
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); ids='detail-collar-v detail-collar-crew detail-collar-scoop detail-collar-henley detail-cuffs detail-hem detail-seams detail-pocket detail-placket detail-shoulder-stripe detail-arm-bands detail-yoke detail-full-set details-pants-mode-behavior details-mode-independence-and-selection wordart-style-solid wordart-style-gradient wordart-style-neon wordart-style-shadow wordart-style-outline wordart-style-retro wordart-glyph-rasterization font-wordart-runtime-google layer-fields-schema project-version-compat project-load-field-coercion'.split(); s={r['id']:r['status'] for r in d['rows']}; print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', [i+'='+s[i] for i in ids])"
```

Expected: `408 rows; 55 done;` followed by the 26 ids, each `=open`.

Apply ruling R4 to `project-load-field-coercion` first: its disposition becomes `fix`, with the divergence the ruling words (the 4.1 half checked against `editor.js:2599` and `:1695-1709`). Then run the `done` command below, minus any id Step 6 removed. If Step 6 removed `project-load-field-coercion`, still run the amendment, so its divergence is recorded:

```bash
python - <<'PYEOF'
import json
path = "docs/port/parity-ledger.json"
data = json.load(open(path, encoding="utf-8"))
row = next(r for r in data["rows"] if r["id"] == "project-load-field-coercion")
assert (row["disposition"], row["divergence"], row["status"]) == ("keep", "", "open"), "the row was already amended"
row["disposition"] = "fix"
row["divergence"] = ("4.1: an unknown mode string is applied without validation (editor.js loadProject): "
                     "switchMode loads the Shirt regions while currentMode holds the bad string. "
                     "v5: an unknown mode refuses the whole file; nothing is applied. "
                     "Duplicate layer ids (which 4.1 kept) are renumbered: the first holder keeps the id, "
                     "later holders take fresh ids above every id in the file.")
open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
print("amended project-load-field-coercion: fix")
PYEOF
python tools/ledger/ledger.py done \
  detail-collar-v detail-collar-crew detail-collar-scoop detail-collar-henley detail-cuffs \
  detail-hem detail-seams detail-pocket detail-placket detail-shoulder-stripe detail-arm-bands \
  detail-yoke detail-full-set details-pants-mode-behavior details-mode-independence-and-selection \
  wordart-style-solid wordart-style-gradient wordart-style-neon wordart-style-shadow \
  wordart-style-outline wordart-style-retro wordart-glyph-rasterization font-wordart-runtime-google \
  layer-fields-schema project-version-compat project-load-field-coercion
python tools/ledger/ledger.py check
grep -c '"status": "done"' docs/port/parity-ledger.json
git diff --stat -- docs/port
```

Expected:

- `amended project-load-field-coercion: fix`.
- `rendered docs/port/parity-ledger.md and docs/port/divergences.md`.
- `408 rows, 0 problem(s)`.
- `81`: the 55 from M1 and M2a plus the 26 ids closed. If Step 6 removed ids, 81 minus their number.
- The `git diff --stat` shows `divergences.md | 3`, `parity-ledger.json | 56`, `parity-ledger.md | 54` and `3 files changed, 57 insertions(+), 56 deletions(-)` (measured on a scratch copy of `main`'s ledger with Task 3 Step 23's row added, 2026-10-14). The JSON carries one `status` line per id plus the coercion row's `disposition` and `divergence`; `parity-ledger.md` carries its counts line and the 26 rows; `divergences.md` gains the coercion row and its `Fixed in v5 (186)` heading. Closing a row never touches its divergence. If Step 6 removed ids, each removed id takes one line off each side of the JSON and of `parity-ledger.md`. A larger JSON diff means the file was reformatted: stop, `git checkout -- docs/port`, and report it.

- [ ] **Step 8: Final verification**

Run, in order, from the repo root:

```bash
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror
dotnet test src/RBX15.slnx -c Release > .superpowers/logs/m2b-final.log 2>&1; grep -E "(Passed|Failed)!" .superpowers/logs/m2b-final.log
python -m unittest discover -s tools/ledger -p "test_*.py"
python tools/ledger/ledger.py check
node --test "tools/parity-ref/*.test.js"
npm test
git diff --stat "$BASE" -- main.js editor.js editor.html styles.css lib packs package.json package-lock.json "tests/*.test.js" windows
git diff --stat --diff-filter=MDRT "$BASE" -- tests/goldens assets/fonts
git diff --name-only --diff-filter=A "$BASE" -- tests/goldens assets/fonts | sed 's|/[^/]*$||' | sort | uniq -c
git log --format=%s "$BASE"..HEAD -- tests/goldens assets/fonts
git status --short
```

Expected:

- `base db6b1cd`.
- Build: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: `RBX15.Parity.Tests.dll` shows `Failed: 0, Passed: 35, Skipped: 0` (Task 4 adds no Parity test). `RBX15.Engine.Tests.dll` shows `Failed: 0, Passed: 517, Skipped: 0`: Step 1's 516 plus the Step 2 fact. The 22 Word Art scenes pass under the Word Art rule, the 23 swap cases pass under the unchanged rule, and `GoldenRule.cs` holds the Word Art rule and nothing else new. No app scene is skipped, so no skip reason names `detail`, `wordart` or `project`.
- Ledger unit tests: `Ran 8 tests`, `OK`. `ledger.py check` prints `408 rows, 0 problem(s)`.
- `node --test`: `# tests 14`, `# pass 14`, `# fail 0` (main's count, measured 2026-10-14). `npm test`: `# tests 31`, `# pass 31`, `# fail 0`.
- The Electron `git diff --stat` prints nothing: Electron is untouched.
- The `--diff-filter=MDRT` diff prints nothing: no existing golden, scene file or font changed in M2b. The added files are exactly `24 tests/goldens/app/wordart-ref`, and the log prints exactly one subject, `test(goldens): freeze 4.1's own Word Art bitmaps for the swap guard (wordart-ref.js)` (ruling R2). Anything else means a frozen file moved: stop and show `git log --stat "$BASE"..HEAD -- tests/goldens assets/fonts` to the controller.
- `git status --short` lists exactly three lines, each starting with `M` in the second column (modified, unstaged): `docs/port/divergences.md`, `docs/port/parity-ledger.json` and `docs/port/parity-ledger.md`. Logs live in the git-ignored `.superpowers/logs/`, and `diff-heatmaps/` is ignored, so neither appears.

- [ ] **Step 9: Commit the ledger**

Write the body from Step 6's outcome. The list of rows left open is the fixed table above, plus any row Step 6 removed and its reason. Add your own model's `Co-Authored-By` trailer as the last line before running it.

```bash
git add docs/port/parity-ledger.json docs/port/parity-ledger.md docs/port/divergences.md
git commit -F - <<'EOF'
chore(ledger): close the rows M2b proves (ProjectIO, Details, Word Art)

Closed by the app goldens (GoldenAppTests) and the M2b unit tests:
detail-collar-v, detail-collar-crew, detail-collar-scoop,
detail-collar-henley, detail-cuffs, detail-hem, detail-seams,
detail-pocket, detail-placket, detail-shoulder-stripe, detail-arm-bands,
detail-yoke, detail-full-set, details-pants-mode-behavior,
details-mode-independence-and-selection, wordart-style-solid,
wordart-style-gradient, wordart-style-neon, wordart-style-shadow,
wordart-style-outline, wordart-style-retro, wordart-glyph-rasterization,
font-wordart-runtime-google, layer-fields-schema, project-version-compat,
project-load-field-coercion (now a fix row: an unknown mode refuses the
whole file, ruling R4).

Left open on purpose: project save and open halves owned by the App
(project-save-format, project-load-flow, project-template-name-not-saved);
the visible dropped-layer warning (project-load-layers,
file-project-format, project-format-r15proj, project-load-untrusted-src,
project-load-error-path-bugs); history cleared by the Open command
(project-load-no-imgsrc-hang, project-load-history-interplay); detail
undo, selection and button halves
(details-layer-creation-pipeline, details-buttons); Word Art UI halves
(wordart-font-list, wordart-font-loading-race, wordart-add-to-canvas,
wordart-edge-cases, text-styles, wordart-layer-name-emoji);
wordart-render-measure (size clause superseded by the 8-300 clamp on
wordart-state-defaults); fonts-google-runtime (app bundle and network).

The app gate runs all 82 scenes, none skipped: the 22 Word Art scenes
under the Word Art rule (ruling R2), with 4.1's own Word Art bitmap
swapped in exact under the unchanged rule, and the other 60 under the
unchanged rule. Electron, the existing goldens and assets/fonts are
untouched; tests/goldens/app/wordart-ref was added once and is frozen.
EOF
git log --oneline -2
git status --short
```

Expected: two commits from this task, `chore(ledger): close the rows M2b proves (ProjectIO, Details, Word Art)` on top of `docs(engine): tell cc levels from Skia mip levels in the decode-cache notes`, and an empty `git status --short`. If Step 6 removed any id, delete it from the "Closed" paragraph and add it, with its reason, to the "Left open" paragraph before committing. Do not push. The controller handles push, the PR and the dashboard after final review.
