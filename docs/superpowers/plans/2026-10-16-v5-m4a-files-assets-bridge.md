# RBX15 v5 M4a: files, assets and the test input bridge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**M4a is the first half of the spec's M4 row** (spec §7: "File I/O, assets and packs, the Layout, Details and Text tabs, keyboard, dialogs and status strip"). M3b finished the shell, the select and draw tools and the properties panel, and held 23 ledger rows for a real mouse. M4a adds Save work, Save As and Open with a current file, dirty tracking against the last save, the unsaved-changes prompt (moved forward from M5 by Este on 2026-10-16), the window title, the three file chords, the asset library with the two built-in packs and Open folder, the rest of 4.1's dialog inventory, layers on the canvas as UI Automation elements, and a test-only in-process input bridge that finally drives the M3b mouse checks unattended. M4b adds the Layout, Details and Text tabs, the Placement group, the rest of the keyboard and the ribbon re-measure. **What does not work yet is hidden, never shown dead** (Este's standing ruling).

**Goal:** A WPF app in which a user can save the document to a `.r15proj` file that 4.1 opens, reopen it with history cleared, keep working against a current file with Ctrl+S, Ctrl+Shift+S and Ctrl+O, see the file name and an unsaved marker in the title, never lose unsaved work to Open or close without a Save / Don't save / Cancel prompt, browse the Drips and Word art packs or any folder of images in a virtualized thumbnail grid and add a thumbnail by click, right-click or drag onto the canvas as one undo step each, read every failure 4.1 only wrote to the console on the status strip, and reach and move canvas layers through UI Automation. A flag-gated bridge that no shipped build contains proves the M3b mouse rows, the file chords and the asset drags through real WPF routed input, and the ledger closes only what that evidence proves.

**Architecture:**

- **Files (Task 1).** `EditorSession` keeps the saved document as a marker, so `IsDirty` compares content and an undo back to the save reads clean (interfaces R2). `Open` raises `Changing` first, abandons any open gesture, clears history (spec §3), selects nothing, and returns the outgoing images, which `ProjectFileService` frees at `ApplicationIdle` (R11). `ProjectFileService` writes `ProjectIO.Write` atomically (a temp file in the same folder, then `File.Replace` or `File.Move`), reads and decodes off the UI thread with the window blocked (R4), refuses a file over 512 MiB before reading it (critic M1), alerts 4.1's `Failed to load project. <message>` with nothing changed, and asks Save / Don't save / Cancel only after a good read. `MainViewModel.Title` follows the Windows convention (R3). The window binds Ctrl+S, Ctrl+Shift+S and Ctrl+O through `TextFieldShortcutCommand`, and its close runs the same guard. `ProjectLoad.Title` stays dropped (R1).
- **Assets (Task 2).** `AssetScan` is 4.1's pack, folder, dedupe, grouping and caption logic as pure functions in the engine. `PackSource` reads the packs the build copies beside the app. `AssetLibraryViewModel` groups a set into rows of three for a virtualized `ListBox` (R5), and `AssetThumbnailLoader` decodes visible tiles only, newest first, through the engine caps. `AssetLibraryPanel` is 4.1's panel: tiles are Buttons (click-add, keyboard and UIA reachable), right-click adds at full size, and a thumbnail drag is in-app and capture-based (R6), so the stale-drag hijack cannot happen.
- **Dialogs and status (Task 3).** `DialogInventoryTests` pins each of 4.1's six dialog sites verbatim and every console-only failure to the status strip; `ThumbnailFailureReport` names a set's first unreadable preview once.
- **UI Automation and the bridge (Task 4).** Each visible layer is a `LayerAutomationPeer` (SelectionItem, Transform in template pixels, one undo step per call). `src/RBX15.App/Testing/**` compiles into the app only with `-p:EnableTestInputBridge=true` and starts only with `--test-input-bridge`: a current-user named pipe whose JSON commands raise WPF's own raw mouse reports and a synthetic keyboard device in-process (R7, R8), never a Windows input API. `BridgeExclusionTests` proves a Release build has no bridge and that publish and MSIX packaging refuse the flag.
- **Close-out (Task 5).** Seven `LedgerScenarioTests` replay M4a's scenarios through the real composition. A scratch driver (`brg`) runs the M3b hand checks and the files and assets checks through the bridge (controller-run), 4.1 opens a v5-saved project through its real Open path (P41), Este answers three manual checks, and the ledger closes only what the evidence proves.

**Tech Stack:** .NET 10 (`net10.0` engine, `net10.0-windows` App and App.Tests), C# with nullable and warnings as errors, WPF, CommunityToolkit.Mvvm `8.4.2` (unchanged), SkiaSharp `4.153.1` and HarfBuzzSharp `14.2.1.301` (engine, unchanged), `System.IO.Pipes` with `PipeSecurity` (the bridge, flagged builds and App.Tests only), `System.Reflection.Metadata` (the exclusion test), xUnit 2.9.3 with `xunit.runner.visualstudio` 3.1.4 and `Microsoft.NET.Test.Sdk` 17.14.1, Electron 41.1.0 (devDependency; the P41 cross-check only), `System.Windows.Automation` (the scratch smoke driver only), Python 3 stdlib (ledger scripts), Git Bash.

**Spec:** [`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`](../specs/2026-10-11-native-v5-port-design.md), sections 3 (project files, saving, export, import, asset library, the upgrades table), 4 (dialogs and status, the keyboard rules M4a needs, accessibility), 5, 6 (the UI automation tier) and the §7 M4 row. Ledger: [`docs/port/parity-ledger.json`](../../port/parity-ledger.json) and [`docs/port/divergences.md`](../../port/divergences.md). Planning contract: `.superpowers/m4-planning/contract.md`; interface sheet: `.superpowers/m4-planning/interfaces.md`; critic: `.superpowers/m4-planning/critic.md` (all git-ignored; the plan below is authoritative where they differ). Previous milestone: [M3b plan](2026-10-15-v5-m3b-select-draw-properties.md).

**Contract amendments for Este's OK** (2026-10-16). Each departs from the planning contract's wording in a way the contract does not rule. Each is applied below so nothing waits, and each names the exact change if Este declines.

- **CA1: Save As is a chord, not a button** (interfaces §2.1; Task 1). The contract lists Save As among the File group's commands, and in the same paragraph fixes the group to 4.1's four buttons, Save work, Open, Add image and Download template (editor.html:85-97). The two cannot both hold, so Save As is Ctrl+Shift+S with no button, and Save work on an untitled document runs Save As. **If declined:** Task 1 Step 11 adds a `<Button x:Name="SaveAsButton" Content="Save as" ToolTip="Save your workspace under a new name." Command="{Binding SaveAsCommand}" Padding="10,5" FontSize="11" Margin="0,0,3,0" Style="{DynamicResource SecondaryButton}" />` after `SaveButton` in `FileGroup.xaml`'s first row; `Task4ViewTests.The_file_group_shows_4_1s_four_buttons_in_order` expects five labels and five tooltips; Task 5's `home-file-group` NARROW text names the extra button.
- **CA2: Open also lists `.json`** (interfaces §2.2; Task 1). The contract says "filtered to .r15proj"; 4.1's input accepted `.json,.r15proj` (editor.html:108), so the Open dialog's first filter is `RBX15 project (*.r15proj)` and its second `JSON project (*.json)`, keeping 4.1's reach. **If declined:** `Win32FileDialogs.ProjectOpenFilter` becomes `"RBX15 project (*.r15proj)|*.r15proj"`, `ProjectFileServiceTests.The_dialog_filters_keep_4_1s_reach` expects that string, and Task 5's `project-load-flow` append drops its first clause.
- **CA3: the bridge sources also compile into App.Tests** (interfaces R9; Task 4). The contract says the bridge is "compiled only when the MSBuild property `EnableTestInputBridge=true`". The App assembly honors that exactly, and `BridgeExclusionTests` proves it; App.Tests links `src/RBX15.App/Testing/**` so the bridge's own tests run in every CI configuration with no flagged build. Nothing in App.Tests ships. **If declined:** remove the `<Compile Include="..\RBX15.App\Testing\**\*.cs" LinkBase="Bridge" />` item from `RBX15.App.Tests.csproj` and delete `InputBridgeTests.cs` and `BridgeSecurityTests.cs` (App.Tests 43 lower from Task 4 Step 8 on); the bridge is then proven only by Task 5's controller-run smoke, and Review Focus 1 loses its pipe and parsing pins.
- **CA4: Open refuses a project file over 512 MiB** (critic M1; Task 1 ruling T1-R14). The contract caps decoding; the per-image caps (100 MiB encoded, 50 MP) do not bound the file, so a hostile or corrupt file could be read whole into memory and crash the app with unsaved work on screen. The limit is new v5 surface, provisional, and shows as 4.1's alert prefix with `the file is <n> MB; the limit is 512 MB`. A total decoded-pixels budget across layers is an M5 or M6 carry-forward (it changes the engine). **If declined:** `ProjectFileService.ReadFile` drops its length check and `MaxProjectBytes`, `ProjectFileServiceTests.A_file_over_the_size_limit_alerts_and_changes_nothing` goes (App.Tests 1 lower from Task 1 Step 9 on), and Task 5's `project-load-untrusted-src` NARROW drops its size clause.
- **CA5: `asset-open-folder` is amended from keep to fix** (Task 5 ruling 3). Its check says "only 5 extensions listed"; the contract's "formats as import" lists six (interfaces R14). A keep row carries no divergence, so it gets one, as M3b amended `eraser-cursor`. The ledger honesty rule is unchanged. **If declined:** remove block 1 from `m4a-ledger.py` and `asset-open-folder` from `ROWS` and `NARROW`; the row stays `keep` and open, the Step 12 Expected lines read `amended 0` and one fewer `fix`, and the numstat lines are re-measured.

**Rulings that stand on the contract, not amendments.** After Open the top layer is selected, as 4.1 does (editor.js:2664; Este's ruling 2026-10-16 overrides the contract's "selection none"; `project-load-layers`' divergence text "nextId and selection always set" stays true, so Task 5 does not reword it). The title is `[*]<name> - RBX15 Classic Shirt and Pants Maker` (R3, the Notepad convention the contract allows). Open always reads off the UI thread (R4, the drafter's choice the contract asks for). `ProjectLoad.Title` stays dropped (R1, `project-template-name-not-saved` is keep).

**Interface sheet amendments** (`.superpowers/m4-planning/interfaces.md`; the plan below is authoritative):

- R6's drag cursor is `Cursors.Hand` over the canvas, since WPF has no `Cursors.Copy` (T2-R1).
- §3.6's panel seams are `HandleMove(Point inPanel, bool leftButtonDown)` and `HandleRelease(Point inRoot)` (critic L3).
- §3.8's pipe passes `PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance` with `CreatePipeSecurity()` and 128 KiB buffers; clients pass `CurrentUserOnly` (T4-R1, T4-R2).
- §3.8's `inspect` returns the shapes Task 5's driver reads: `target` `canvas` adds `cursor {kind, ring}`, `ringRadiusPx`, `hostCursor`, `overrideCursor`, `tool`, `brush`, per-layer `w`, `h` and `image`, and `fills`; `target` `layers` is new; a disabled-owner `pointer down` adds `ownerClick` and `handled` (critic C3).
- §3.3's fake members are Task 1's: `FakeFileDialogs.PickFolderResult` and `PickFolderDirs`. Task 2's Addition 10 (`FolderResult`, `FolderInitialDirectories`) is rejected (critic C1).
- Task 2's `AssetLibraryPanel` reads the left button through `PointerState` from Task 4 Step 5 on (critic C2).
- The folder failure text is Task 2's `Couldn't open <folder>: <reason>` (T2-R8); Task 3's Addition 6 (`Couldn't read ...`) is rejected (critic L2).
- §4's `OnClosing` order stays cancel-then-ask (T1-R16; critic L6 ruled, not applied).
- Every task's other "Additions requested" item is adopted as written, with the names aligned to one spelling.

## Global Constraints

The M3b plan's Global Constraints apply verbatim (copied below from [the M3b plan](2026-10-15-v5-m3b-select-draw-properties.md)), then M4a's additions. Where an M3b or earlier bullet names an artifact or rule M4a changes (the hidden Save work, Open and asset library, the synthetic-input grep, keys posted to the app's own windows, the M3b scratch folders and controller steps), the M4a bullets after it say how it applies here, and the M4a bullet wins.

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

**M4a additions** (planning contract, "The test input bridge", "Rulings already made" and "Plan rules"; critic M2 and M5):

- **The test input bridge never ships.** `src/RBX15.App/Testing/**` compiles into RBX15.App only with `-p:EnableTestInputBridge=true`, and it starts only when that build is launched with `--test-input-bridge`. Nobody passes the flag to a solution build, a Release build or a publish, and M6 packaging never sets it. A publish or an MSIX build with the flag fails by design (`RefuseTestInputBridgeOnPublish`), and so does a solution build with it (CS0436). `BridgeExclusionTests` is the proof and runs in every CI configuration.
- **The bridge sends nothing global.** No SendInput, mouse_event, keybd_event, SendKeys, SetCursorPos, ClipCursor, SetWindowsHookEx or BlockInput anywhere in `src/RBX15.App` (`BridgeExclusionTests.No_source_calls_a_global_input_api`). Input is raised in-process through WPF's InputManager. The one Win32 message the bridge sends is WM_SETCURSOR, to a window of its own process that a modal has disabled, which is what Windows itself sends on that click.
- **The pipe is the current user's only.** The pipe ACL is protected, owner and only Allow are the current user, and NETWORK is denied; `FirstPipeInstance` refuses a name a squatter created first. The pipe name is random per launch. The pipe serves one client, then closes for good. A malformed, over-long, invalid UTF-8 or duplicate-key line gets one error line and closes it.
- **Implementers still never launch the app.** Bridge tests drive an off-screen (-12000, -12000), never-activated MainWindow inside the test process. Only the controller launches the flagged app, for Task 5's smoke.
- **M6 packaging asserts the flag is absent.** The packaging pipeline asserts `EnableTestInputBridge` is unset in its environment (MSBuild imports environment variables as properties) and runs `BridgeExclusionTests`' scan on the packaged `RBX15.App.dll` (critic M5). Task 5 records this in the `test-input-bridge` row's portTarget.
- **What ships and what stays hidden.** Save work, Open, Save As's chord, the asset library and the unsaved-changes prompt ship in M4a. The Layout, Details and Text tabs, the Placement group, the presets, "Center on front torso" and "Keys: ON" stay absent (M4b). Ctrl+Z, Ctrl+S, Ctrl+Shift+S and Ctrl+O are the only keyboard shortcuts; the rest is M4b.
- **The synthetic-input grep.** From Task 4 on, `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` prints exactly one line, in `src/RBX15.App.Tests/BridgeExclusionTests.cs` (the test's own regex); before Task 4 it prints nothing. Any other line fails the task.
- **No posted keys.** M4a's drivers post no window messages; keys and pointer go through the bridge's pipe, and everything else is UI Automation patterns on PID-checked elements and PrintWindow of the app's own windows. The bridge's one WM_SETCURSOR to its own disabled HWND is interfaces R8's.
- **The unsaved-changes prompt is in M4a** (Este, 2026-10-16; moved forward from M5). Autosave, recovery, recents, the jump list, file association and paste stay M5.
- **Bridge-built windows run alone.** Test classes that show a `MainWindow` or install the bridge's input hooks run in `[Collection("MainWindow")]` or the non-parallel `[Collection("Bridge")]` (critic M7).
- **Task order is 1, 2, 3, 4, 5.** Each task edits files the previous ones created, at named anchors, and fails loudly if an anchor is missing. The controller's steps are Task 5 Steps 7, 8, 9 and 10 (the bridge build and probe, the smoke, the 4.1 cross-check and Este's manual list); a subagent stops there and reports.
- **Scratch.** Measurements and throwaway scripts go to `.superpowers/m4a/`, the smoke driver and its output to `.superpowers/m4a-smoke/`, ledger scripts and logs to `.superpowers/logs/`, the flagged build to `%TEMP%\rbx15-bridge-build` (all outside the tracked tree).

## Review Focus

The critic picked five input classes as the most likely to cause bugs. Each has a pinning test in its owning task.

- **The bridge can never ship, or be driven by anyone but the current user** (Task 4; contract "Security"; critic M5, M6). Expected: a plain Release build and the app under test contain no bridge type or string, and a flagged build does (the positive control); publish and an MSIX build refuse the flag; the pipe ACL is protected, current user only, NETWORK denied, with `FirstPipeInstance`; an over-long, non-JSON, invalid UTF-8 or duplicate-key line gets one error line and a disconnect. Pinned in Task 4 by `BridgeExclusionTests` (`The_app_under_test_has_no_bridge`, `A_release_build_has_no_bridge_and_a_flagged_build_does`, `Publishing_with_the_flag_is_refused`, `No_source_calls_a_global_input_api`) and `BridgeSecurityTests` (`The_pipe_acl_allows_only_the_current_user`, `The_pipe_serves_one_client_one_line_per_request_and_never_listens_again`, `A_bad_request_line_gets_one_error_and_a_disconnect` with its `dup` row, `Parsing_refuses_anything_unexpected` with its duplicate-key row).
- **Opening a bad, hostile or huge `.r15proj` while the document is dirty** (Task 1; interfaces R4, R11; critic M1). Expected: 4.1's alert text, no save prompt first, and the document, history, current file and dirty state unchanged; a file over 512 MiB is refused before it is read, never crashing; a read cancelled by closing the window is released, never applied; outgoing images are freed only after ApplicationIdle, and never when the new document shares them. Pinned in Task 1 by `ProjectFileServiceTests.A_file_that_cannot_open_alerts_4_1s_text_and_changes_nothing` (4 rows), `Not_json_and_a_missing_file_alert_too`, `A_file_over_the_size_limit_alerts_and_changes_nothing`, `A_cancelled_read_is_released_and_never_applied` and `The_default_release_frees_outgoing_images_at_application_idle`; in Task 5 by `LedgerScenarioTests.A_file_that_cannot_open_alerts_and_changes_nothing_and_layers_that_cannot_load_are_named` and bridge item F1.
- **Dirty state against the saved marker** (Task 1; interfaces R2, R13). Expected: undo back to the save reads clean, and an edit back to the saved values reads clean; a gesture's live change reads dirty at once, and the title stars and unstars; an Open mid-stroke or mid-burst discards the stroke and reads clean; closing dirty asks, and Save on an untitled document whose Save As is cancelled keeps the window open. Pinned in Task 1 by `EditorSessionTests.Undo_back_to_the_saved_state_reads_clean`, `An_edit_back_to_the_saved_values_reads_clean`, `Open_swaps_the_document_clears_history_and_selection_and_reads_clean`, `A_gesture_open_across_an_Open_is_abandoned_and_its_stale_dispose_does_nothing`, `WindowTitleTests`, `ShellTests.Closing_dirty_work_asks_save_dont_save_or_cancel` and `CanvasInteractionTests.An_open_mid_stroke_discards_it`; in Task 5 by bridge items F1 and F2.
- **An asset thumbnail drag that ends anywhere but the canvas, and drops at zoom 1 and 2** (Task 2; interfaces R6; critic C2, M8). Expected: Esc from any focus, a lost capture, a release on the list or the zoom pill, a disabled canvas, or a move with the button up each add nothing and leave no state; the next Explorer drop adds the Explorer file; a canvas drop centers on the drop point as one step; under the bridge the drag reads the button through `PointerState`. Pinned in Task 2 by `AssetLibraryPanelTests.A_drag_dropped_elsewhere_never_hijacks_the_next_os_file_drop`, `Every_cancel_ends_the_drag_with_nothing_added` (4 rows), `Esc_anywhere_in_the_window_cancels_a_drag`, `A_disabled_canvas_or_the_zoom_pill_takes_no_drop` and `A_drag_onto_the_canvas_adds_the_tile_centered_on_the_drop_point` (zoom 1 and 2); in Task 4 by `InputBridgeTests.An_asset_tile_drag_onto_the_canvas_adds_one_layer_in_one_step`; in Task 5 by bridge item A1 and manual item MAN-1.
- **The ledger close keeps to the honesty rule** (Task 5; critic M9, M10). Expected: each closed row's parityCheck and divergence say exactly what the evidence proves; the cursor rows are reworded to "the cursor the canvas sets"; the touchpad is injected deltas; `project-load-layers`' selection clause stays true (T1-R2, top layer selected); the Background-image and Background-picker halves are narrowed to M4b; MAN-gated rows stay open unless Este passed the item. Pinned in Task 5 by `m4a-evidence.py` (with `DialogInventoryTests` in the `diag-console-only-errors` gate), `m4a-ledger.py`'s asserted texts and `ledger.py check`.

The drafters pinned more edge cases: a locked save target, which leaves the old file byte-for-byte and no `*.tmp` (`ProjectFileServiceTests.A_failed_write_keeps_the_existing_file_and_stays_dirty`); a settings file that cannot be read, whose sibling keys survive the next save (`JsonSettingsStoreTests.A_save_after_a_failed_read_keeps_the_sibling_keys`); folder trees that stress 4.1's dedupe and grouping (`AssetScanTests.The_deepest_copy_of_a_name_wins`, `A_subfolder_named_all_does_not_make_a_second_all_group`, `Folder_groups_sort_as_javascripts_localeCompare`); a set switch while decodes are queued (`AssetLibraryViewModelTests.A_new_load_cancels_the_previous_sets_queued_thumbnails`); a UIA transform on a layer that is not selected or is hidden, or with NaN (`CanvasLayerAutomationTests.Only_the_selected_layer_can_be_transformed`, `A_hidden_layer_is_neither_an_element_nor_movable`); and a real modifier held while the bridge sends a chord (`InputBridgeTests.A_key_is_refused_while_a_physical_modifier_is_held`).

---

## File structure

```text
src/RBX15.Engine/
  History.cs                   + Snapshots (Task 1)
  Images.cs                    + Release (Task 1); + DecodeThumbnail (Task 2)
  Adjust.cs                    + internal TakeShifted (Task 1)
  AssetScan.cs                 AssetEntry, AssetGroup, AssetSet, AssetScan (Task 2)
  LayerOps.cs                  + AddFull (Task 2)
src/RBX15.Engine.Tests/
  HistoryTests.cs +2, ImagesCapsTests.cs +3 (Task 1)
  AssetScanTests.cs 38; ImagesCapsTests.cs +7, ImagesOrientationTests.cs +2, LayerOpsTests.cs +6 (Task 2)
src/RBX15.App/
  EditorSession.cs             SessionChange.File and Replaced, the saved marker, IsDirty, CommitPendingEdits, MarkSaved, Open (Task 1)
  Canvas/CanvasInteraction.cs  Replaced cancels a stroke (Task 1)
  Services/ProjectFileService.cs, DialogText.cs (Task 1); PackSource.cs, AssetThumbnailLoader.cs (Task 2)
  Services/IFileDialogs.cs, Win32FileDialogs.cs, ISettingsStore.cs, JsonSettingsStore.cs, IDialogService.cs,
    ThemedDialogService.cs (Task 1); ImportService.cs + ImportFull (Task 2)
  Converters/NotConverter.cs (Task 1); EqualsMultiConverter.cs (Task 2)
  Views/ThemedDialog.xaml(.cs), FileGroup.xaml (Task 1); AssetLibraryPanel.xaml(.cs) (Task 2, full; Task 4 one line);
    DropZone.xaml (Task 2); LayersPanel.xaml.cs, ColorPickerPopup.xaml.cs, CanvasView.xaml (Task 4)
  ViewModels/FileGroupViewModel.cs, MainViewModel.cs (Task 1; Assets Task 2); AssetLibraryViewModel.cs,
    AssetRowViewModel.cs, AssetTileViewModel.cs (Task 2); ThumbnailFailureReport.cs (Task 3); CanvasViewModel.cs (Task 4)
  Controls/AssetRowPanel.cs (Task 2); ICanvasLayerAutomation.cs, LayerAutomationPeer.cs, PointerState.cs,
    TemplateCanvasHostAutomationPeer.cs, TemplateCanvasHost.cs, SliderGestureTracker.cs (Task 4)
  Testing/                     BridgeJson.cs, BridgeKeyboardDevice.cs, RawMouse.cs, BridgeFileDialogs.cs,
                                 InputBridgeDispatcher.cs, TestInputBridge.cs; compiled only with EnableTestInputBridge (Task 4)
  MainWindow.xaml(.cs)         title, busy, the chords, the close guard (Task 1); the asset panel first (Task 2)
  Composition.cs, App.xaml.cs  ISettingsStore and ProjectFileService (Task 1); assets (Task 2); the failure report (Task 3);
                                 the two #if TEST_INPUT_BRIDGE hook sites (Task 4)
  Themes/Controls.xaml         the Not converter (Task 1)
  RBX15.App.csproj             the packs Content item (Task 2); the bridge flag, exclusion and publish refusal (Task 4)
src/RBX15.App.Tests/
  ProjectFileServiceTests.cs 31, WindowTitleTests.cs 6; EditorSessionTests.cs +8, CanvasInteractionTests.cs +1,
    JsonSettingsStoreTests.cs +9, ThemedDialogTests.cs +4, ShellTests.cs +8, FileGroupTests.cs +3,
    LedgerScenarioTests.cs +1; fakes, TestShell, HomeTestKit and four view tests edited with no count change (Task 1)
  PackSourceTests.cs 4, AssetThumbnailLoaderTests.cs 6, AssetLibraryViewModelTests.cs 25, AssetLibraryPanelTests.cs 19;
    ImportServiceTests.cs +1; ThemeKeyGuardTests.cs and LayerPropertiesPanelTests.cs edited with no count change (Task 2)
  DialogInventoryTests.cs 31 (Task 3)
  CanvasLayerAutomationTests.cs 11, InputBridgeTests.cs 16, BridgeSecurityTests.cs 27, BridgeExclusionTests.cs 4;
    CanvasAutomationPeerTests.cs +1; RBX15.App.Tests.csproj links Testing/** (Task 4)
  LedgerScenarioTests.cs       + 7 M4a scenarios (34 in all) (Task 5)
docs/port/                     m4a-manual.md (Task 5 Step 6); parity-ledger.json, parity-ledger.md, divergences.md:
                                 1 amended, 10 appended, 3 added, narrowed and closed per the evidence (Task 5 Step 12)
.superpowers/ (git-ignored)    m4a-smoke/bridge/ (the brg driver), m4a-smoke/out/ and p41/ (Task 5);
                                 logs/m4a-evidence.py, m4a-ledger.py and the run logs (Task 5)
```

**Test counts.** The baseline on `feat/v5-m4a-files-assets-bridge` (from main `3f3c5c5`, M3b merged) is Engine.Tests 880 passed and 0 skipped, App.Tests 644, Parity.Tests 35. Every run step below states the cumulative count; nothing is skipped and Parity.Tests stays 35 throughout:

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| `3f3c5c5` | 880 | 644 | |
| Task 1 Step 3 | 885 | 644 | HistoryTests +2, ImagesCapsTests +3 |
| Task 1 Step 5 | 885 | 653 | EditorSessionTests +8, CanvasInteractionTests +1 |
| Task 1 Step 7 | 885 | 666 | JsonSettingsStoreTests +9, ThemedDialogTests +4 |
| Task 1 Step 9 | 885 | 697 | ProjectFileServiceTests +31 |
| **Task 1 Step 11** | **885** | **715** | ShellTests +8, WindowTitleTests +6, FileGroupTests +3, LedgerScenarioTests +1 |
| Task 2 Step 3 | 923 | 715 | AssetScanTests +38 |
| Task 2 Step 5 | 938 | 715 | ImagesCapsTests +7, ImagesOrientationTests +2, LayerOpsTests +6 |
| Task 2 Step 8 | 938 | 726 | PackSourceTests +4, AssetThumbnailLoaderTests +6, ImportServiceTests +1 |
| Task 2 Step 11 | 938 | 751 | AssetLibraryViewModelTests +25 |
| **Task 2 Step 14** | **938** | **770** | AssetLibraryPanelTests +19 |
| Task 3 Step 3 | 938 | 799 passed, 2 failed | DialogInventoryTests +31, two failing until Step 4 |
| **Task 3 Step 4** | **938** | **801** | DialogInventoryTests +31 |
| Task 4 Step 3 | 938 | 813 | CanvasLayerAutomationTests +11, CanvasAutomationPeerTests +1 |
| Task 4 Step 5 | 938 | 813 | call sites only |
| Task 4 Step 8 | 938 | 856 | InputBridgeTests +16, BridgeSecurityTests +27 |
| **Task 4 Step 10** | **938** | **860** | BridgeExclusionTests +4 |
| **Task 5 Steps 3 and 13** | **938** | **867** | LedgerScenarioTests +7 |

The per-class counts are the drafters' measured counts, recounted per fact and theory row by the critic, plus the reconciler's additions for the critic's fixes: `ProjectFileServiceTests` +1 (Task 1, M1), `AssetLibraryPanelTests` +1 (Task 2, M8), `InputBridgeTests` +2 (Task 4, C2 and C3) and `BridgeSecurityTests` +2 (Task 4, M6). After the fixes the reconciler applied Tasks 1 to 5's code as this plan prints it, task by task, to a scratch copy outside the repo: each task's end built with 0 warnings under `-warnaserror`, and the full runs gave exactly 885 and 715, 938 and 770, 938 and 801, 938 and 860, and 938 and 867, all green. On a desktop whose display scale is not 100%, M3a's `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails before and after every task alike; every Expected line assumes a 100% display, as CI runs.

---

### Task 1: Save, Open and Save As, dirty tracking, the unsaved-changes prompt, the window title and the three file chords

**Files:**

- Modify (engine):
  - `src/RBX15.Engine/History.cs`: `Snapshots`.
  - `src/RBX15.Engine/Images.cs`: `Release` (and its private `ReleaseScaled`).
  - `src/RBX15.Engine/Adjust.cs`: the internal `TakeShifted` that `Release` uses (Addition 1).
- Modify (app), printed in full unless marked as an edit:
  - `src/RBX15.App/EditorSession.cs`: `SessionChange.File` and `Replaced`, the saved-state marker and `IsDirty` (R2), `CommitPendingEdits`, `MarkSaved`, `Open`, and the gesture generation.
  - `src/RBX15.App/Canvas/CanvasInteraction.cs` (edit): `Replaced` cancels a stroke (R11; Addition 6).
  - `src/RBX15.App/Services/IFileDialogs.cs`, `Win32FileDialogs.cs`: `OpenProject`, `SaveProject`, `PickFolder`, and the two filter constants (Addition 2).
  - `src/RBX15.App/Services/ISettingsStore.cs`, `JsonSettingsStore.cs`: `SettingsFolder`, `LoadFolder`, `SaveFolder`, and the skipped write after a failed read (§2.5).
  - `src/RBX15.App/Services/IDialogService.cs`, `ThemedDialogService.cs`: `SaveChoice`, `AskSaveChanges`.
  - `src/RBX15.App/Views/ThemedDialog.xaml`, `ThemedDialog.xaml.cs`: `ThemedDialogKind`, `DontSaveButton`, `Choice`.
  - `src/RBX15.App/ViewModels/FileGroupViewModel.cs`, `src/RBX15.App/Views/FileGroup.xaml`: Save work and Open shown; the three commands.
  - `src/RBX15.App/ViewModels/MainViewModel.cs`: `Title` (R3), `WindowTitle`, `IsBusy`, `Files`.
  - `src/RBX15.App/MainWindow.xaml` (edit): the title binding and the Viewport's `IsEnabled`.
  - `src/RBX15.App/MainWindow.xaml.cs`: `AddShortcut`, the four chords, the wait cursor, the close guard.
  - `src/RBX15.App/Composition.cs`, `src/RBX15.App/App.xaml.cs`: the `ISettingsStore` parameter and `ProjectFileService`.
  - `src/RBX15.App/Themes/Controls.xaml` (edit): the `Not` converter.
- Create (app): `src/RBX15.App/Services/ProjectFileService.cs`, `src/RBX15.App/Services/DialogText.cs`, `src/RBX15.App/Converters/NotConverter.cs`.
- Create (tests): `src/RBX15.App.Tests/ProjectFileServiceTests.cs` (31), `src/RBX15.App.Tests/WindowTitleTests.cs` (6).
- Modify (tests): `src/RBX15.Engine.Tests/HistoryTests.cs` (+2), `ImagesCapsTests.cs` (+3); `src/RBX15.App.Tests/EditorSessionTests.cs` (+8, one assertion changed per R2), `CanvasInteractionTests.cs` (+1), `JsonSettingsStoreTests.cs` (+9), `ThemedDialogTests.cs` (+4), `ShellTests.cs` (+8), `FileGroupTests.cs` (+3), `LedgerScenarioTests.cs` (+1, plus its two private fakes and two constructor calls), `Fakes/FakeFileDialogs.cs`, `Fakes/FakeSettingsStore.cs`, `Fakes/FakeDialogService.cs`, `TestShell.cs`, `HomeTestKit.cs` (Addition 8), and with no count change `Task4ViewTests.cs` (one fact rewritten), `HomeRibbonViewTests.cs` (one assertion), `ShellLookTests.cs` and `ShellWiringTests.cs` (constructor calls).
- Read only: 4.1's `editor.js`, `editor.html`, `styles.css`, `main.js` (untouched); `tests/goldens/projects/*.r15proj` (frozen fixtures, copied to a temp folder by the tests); `docs/port/parity-ledger.json`.
- Not touched: the ledger and `divergences.md` (Task 5, from the handoff at the end), `ProjectIO` (R1 keeps `Write`'s optional `title` for M5), `HomeRibbon.xaml` (the File group grows inside its own view).

**Interfaces:**

- Consumes:
  - **M2b/M3b engine:** `ProjectIO.Read(string) : ProjectLoad` (throws `ProjectFormatException`), `ProjectIO.Write(Document, DateTimeOffset, string? title = null) : string`, `ProjectLoad(Document Document, int NextId, ImmutableList<string> DroppedLayers)`, `ExportNaming.ProjectFileName(string?)`, `History.Clear()`, `Images.ForDrawing`, `Images.DecodeCacheScaled` (internal), `Adjust.ShiftRgb`, `Document.ContentEquals`, `LayerOps.AddImage`.
  - **M3b app:** `EditorSession` (`Apply`, `ApplyWithSelection`, `BeginGesture`, `Undo`, `Redo`, `Changing`, `Changed`, `TemplateTitle`, `NextId`), `CanvasInteraction.Cancel()` and its `Changing` handler (closes a wheel burst and a select drag), `TextFieldShortcutCommand(ICommand, Func<IInputElement?>)` and `.Inner`, `MainWindow.FocusedElement`, `ThemedDialog`, `CaptionTheme.Apply`, `StatusViewModel : IStatus`, `ImportService`, `HomeViewModel.File`/`.Edit`, `EditGroupViewModel.UndoCommand`, `Composition.Build`, CommunityToolkit.Mvvm `RelayCommand`/`AsyncRelayCommand` (8.4.2).
  - **Test helpers:** `StaThread.Run`, `StaThread.Pump`, `TestFiles`, `RepoRoot.Find` (RBX15.Parity), `HomeTestKit`, `FakeStatus`, `FakeColorPicker`, `Px` (engine tests).
- Produces (interfaces.md §3 names verbatim; "+" marks an addition listed at the end):
  - Engine: `History.Snapshots`, `Images.Release`, + `Adjust.TakeShifted` (internal).
  - `SessionChange.File`, `SessionChange.Replaced`; `EditorSession.CurrentFile`, `IsDirty` (R2), `CommitPendingEdits()`, `MarkSaved(string, Document)`, `Open(ProjectLoad, string) : IReadOnlyCollection<SKImage>`.
  - `IFileDialogs.OpenProject`, `SaveProject`, `PickFolder`; + `Win32FileDialogs.ProjectOpenFilter`, `ProjectSaveFilter`.
  - `SettingsFolder`, `ISettingsStore.LoadFolder`, `SaveFolder`; + `JsonSettingsStore.FolderKey` (internal).
  - `SaveChoice`, `IDialogService.AskSaveChanges`; `ThemedDialogKind`, `ThemedDialog(string, ThemedDialogKind)`, `ThemedDialog.Choice`, `DontSaveButton`.
  - `DialogText` (all six entries).
  - `ProjectFileService` (constructor, `IsBusy`, `Save`, `SaveAs`, `OpenAsync()`, `OpenAsync(string)`, `ConfirmDiscardChanges`, `CancelPendingOpen`, `DocumentName`); + `ReadFile` (internal) and + `MaxProjectBytes` (internal, T1-R14).
  - `FileGroupViewModel` constructor (+ `ProjectFileService projectFiles` last), `SaveCommand`, `SaveAsCommand`, `OpenCommand`.
  - `MainViewModel.Title`, `WindowTitle`, `IsBusy`, `Files`; `MainWindow.AddShortcut`; `Composition.Build(…, ISettingsStore)` (both overloads); `NotConverter` (key `Not`).
  - Named parts: `SaveButton`, `OpenButton` (FileGroup.xaml), `DontSaveButton` (ThemedDialog.xaml).
  - Fakes (+): `FakeFileDialogs.{OpenProjectResult, OpenProjectDirs, SaveProjectResult, SaveProjectCalls, PickFolderResult, PickFolderDirs}`, `FakeSettingsStore.{Folders, FolderSaves}`, `FakeDialogService.{SaveChoiceAnswer, SaveChanges}` (the last two are the sheet's), `HomeTestKit.FileGroup(...)`.

**What 4.1 does (cited).** Shipped 4.1, untouched:

- **Save work** (saveProject, editor.js:2550-2591): builds `version`, `app`, `savedAt`, `mode`, `bgColor`, `bgOpacity`, `bgImageSrc`, `regionColors`, `showMask`, `showGrid`, `layers[]` (:2551-2582), `JSON.stringify(project, null, 2)` (:2583), and downloads it through an anchor (:2584-2590). The name is `#exportTitle` or `untitled`, every character outside `[a-zA-Z0-9_-]` becoming `_`, plus `.r15proj` (:2586-2587). No current file, no last folder, no feedback after saving (ledger `io-download-save-dialog`). The template name is not saved (:2552-2582; `project-template-name-not-saved`, keep).
- **Open** (btnLoadProject, editor.js:2683; `#projectFileInput accept=".json,.r15proj"`, editor.html:108; the input is reset after each pick, :2686): loadProject reads with a FileReader (:2594-2595, asynchronous), then mutates mode, background, fills and checkboxes in place (:2599-2622) before the layers are even parsed, clears the layers and the selection (:2625-2626), decodes every image asynchronously (:2631-2673), and on the last successful decode sorts by file order, sets `nextId = max + 1` and selects the top layer (:2656-2666). Any thrown error alerts `'Failed to load project. ' + err.message` (:2675-2677). It neither pushes nor clears history (`project-load-history-interplay`).
- **Buttons** (editor.html:85-97): row 1 is `Save work` (title `Save your workspace. Reopen and keep editing.`, :88), `Open` (`Open a saved workspace.`, :89), `Add image` (:90), with `gap:3px` (:87); row 2 is `Download template` (:93). `.btn-sm` is padding 5px 10px, 11px (styles.css:407).
- **Keys** (editor.js:1775-1830): there is no Ctrl+S, Ctrl+Shift+S or Ctrl+O. Ctrl+O even toggled the ellipse tool, because the tool switch ignores Ctrl for every letter but `e` (:1786-1787); spec §4 fixes that and adds the three chords. Only text inputs trap keys (:1777).
- **The window** (main.js:5-9, editor.html:6): the title is the product name and never changes; there is no close handler or `beforeunload` (main.js:4-23), so closing discards work silently (`shell-no-unsaved-changes-guard`, `shell-unsaved-work-loss`).

**Ledger rows this task serves.** Dispositions are followed; Task 1 closes no row (Task 5 does, under the honesty rule).

- `project-save-format` (fix), `file-project-format` (fix), `project-format-r15proj` (fix): Steps 8-9 (ProjectIO's bytes written atomically, UTF-8 without a BOM, a stable round trip).
- `project-save-filename` (keep): Step 8's theory, `My Shirt v2!` gives `My_Shirt_v2_.r15proj` and empty gives `untitled.r15proj`.
- `project-load-flow` (fix), `project-load-layers` (fix), `project-load-error-path-bugs` (fix), `project-load-no-imgsrc-hang` (fix), `project-load-untrusted-src` (fix): Steps 8-9.
- `project-load-history-interplay` (fix): Steps 4-5 (`EditorSession.Open` clears history) and Step 8 (Ctrl+Z after Open does nothing).
- `project-template-name-not-saved` (keep): R1, pinned in Steps 4 and 8.
- `home-file-group` (fix): Steps 10-11.
- `io-download-save-dialog` (fix): Steps 6-9 (native dialogs, current file, status message).
- `shell-no-unsaved-changes-guard` (fix), `shell-unsaved-work-loss` (new): Steps 8-11.
- `shell-window-frame` (fix; the title part only) and `shell-window-title` (keep, done; unchanged at launch): Steps 10-11 (R3).
- `shell-no-persistence` (fix; the last-folder part): Steps 6-7.
- `undo-buttons-shortcuts` (fix; its "shortcuts do nothing while typing" check, for the three file chords): Steps 10-11. The tooltips stay M4b.

**Rulings** (each pinned by a named test):

- **T1-R1: R1 as written.** `ProjectFileService` writes `title: null`; `Open` leaves `TemplateTitle` alone. Pinned by `ProjectFileServiceTests.Save_writes_the_project_marks_the_session_saved_and_says_so` (no `title` key) and `Open_swaps_in_a_4_1_file_clears_history_and_reads_clean` (`"Hoodie"` survives).
- **T1-R2 (Este's ruling, 2026-10-16): after Open the top layer is selected, matching 4.1** (editor.js:2664; ledger `project-load-layers` "nextId and selection always set"). `EditorSession.Open` sets `SelectedLayerId = Document.Layers.IsEmpty ? null : Document.Layers[^1].Id;` (the top layer is the last in the list). An empty project selects nothing. Selecting is never an undo step, so history stays empty. Pinned by `EditorSessionTests.Open_swaps_the_document_clears_history_and_selection_and_reads_clean`, `ProjectFileServiceTests.Open_swaps_in_a_4_1_file_clears_history_and_reads_clean` and the Task 5 round-trip scenario. **Implementer:** any other assertion in this plan that expects `SelectedLayerId` null right after a successful Open of a project with layers expects the top layer's id instead.
- **T1-R3: IsDirty is R2's marker, and the saved marker's images count as outgoing.** `_saved` can hold images the 30-deep history has evicted, so `Open` collects them too (R11 lists the document, history and a gesture start; the marker is the fourth holder). Pinned by `EditorSessionTests.Open_returns_every_outgoing_image_except_those_the_new_document_holds`. The one changed assertion is `EditorSessionTests.A_gesture_is_one_step_recorded_when_it_closes` (`Assert.False(s.IsDirty)` inside the gesture becomes `Assert.True`), as R2 names.
- **T1-R4: `Images.Release` also frees the RGB-shift cache.** R11's sheet line names the premultiplied and decode-cache entries; `Adjust.ShiftRgb` keeps a third per-image cache, and its shifted copies have their own premultiplied entries. `Release` takes and releases them recursively through the internal `Adjust.TakeShifted` (Addition 1). Pinned by `ImagesCapsTests.Release_frees_the_image_and_every_derived_cache_entry`.
- **T1-R5: an Open mid stroke cancels the stroke.** R11 says Open's `Replaced` makes `CanvasInteraction` cancel a stroke or an erase preview; the sheet's Task 1 file list omits `CanvasInteraction.cs`, so the two-line handler and its test are listed as Addition 6. Changing (raised first by `Open`) already closes a wheel burst and a select drag. Pinned by `CanvasInteractionTests.An_open_mid_stroke_discards_it`.
- **T1-R6: what Open catches.** The read's `ProjectFormatException`, `IOException` (file not found, sharing violation), `UnauthorizedAccessException`, and the path errors `NotSupportedException` and `ArgumentException` become the alert. `OutOfMemoryException` is not caught: the decode caps bound every image, and T1-R14 refuses a file over 512 MiB before it is read. Pinned by `A_file_that_cannot_open_alerts_4_1s_text_and_changes_nothing` and `Not_json_and_a_missing_file_alert_too`.
- **T1-R7: Save also catches `InvalidOperationException`.** `ProjectIO.Write` throws it when PNG encoding fails (ProjectIO.cs `PngDataUrl`); the sheet lists IO and access errors only. A crash on Save loses the work it was saving, so the encode failure takes the same status-strip path. **Cost if wrong:** none in behavior that a test can reach (no fixture makes Skia's encoder fail).
- **T1-R8: `Save` and `SaveAs` do not check `IsBusy`; the commands do.** The close guard must be able to save while a cancelled read is still landing (MainWindow cancels the read, then asks). The three commands' `CanExecute` is `!IsBusy`, and `OpenAsync` refuses re-entry. Pinned by `FileGroupTests.The_file_commands_wait_while_an_open_reads` and `ProjectFileServiceTests.While_the_read_runs_the_service_is_busy_and_refuses_a_second_open`.
- **T1-R9: remembered folders are absolute.** `SaveFolder` ignores an empty or relative path; `LoadFolder` returns a stored path only when it is fully qualified and still exists, so a dialog never starts in the process's working directory. Pinned by `JsonSettingsStoreTests.A_missing_or_bad_folder_loads_as_unset` (6 rows) and `A_relative_or_empty_folder_is_not_saved`.
- **T1-R10: §2.5's skipped write is proven with an ACL, not a lock.** An exclusive lock also blocks the final move, so it cannot tell a skipped write from a failed one (measured: the lock-based test passed with the fix removed). The test denies `ReadData` to the current user on `settings.json` for the duration of the saves; reading fails, the move would succeed, and the file must be unchanged. With the fix removed it fails (measured). Pinned by `JsonSettingsStoreTests.A_save_after_a_failed_read_keeps_the_sibling_keys`.
- **T1-R11: the prompt's look.** Save is the existing `PrimaryButton` default, Don't save and Cancel are `SecondaryButton`, 8 DIP apart, in the Windows order. 4.1 has no such dialog, so the Don't save button copies Cancel's look; "new v5 surface; provisional; M4a look sign-off" (comment in ThemedDialog.xaml). No access keys: 4.1's dialogs have none and Tab, Enter and Esc reach every button. Pinned by `ThemedDialogTests.The_save_changes_prompt_has_save_dont_save_and_cancel`.
- **T1-R12: the default release captures the constructing thread's dispatcher.** `ProjectFileService` is built on the UI thread in `Composition.Build`, so `Dispatcher.CurrentDispatcher` there is the window's. Pinned by `ProjectFileServiceTests.The_default_release_frees_outgoing_images_at_application_idle` (the image is alive after Open and freed after an ApplicationIdle pump; a shared image is never freed).
- **T1-R13: the close test closes a window that was never shown.** WPF raises `Closing` and `Closed` for it (measured), so the guard is tested through `Window.Close()` with no seam and no desktop input.
- **T1-R14: Open refuses a file over 512 MiB before reading it** (critic M1; contract amendment CA4). `ProjectIO.Read` caps each image (100 MiB encoded, 50 MP) but not the layer count or the file, and `File.ReadAllText` reads any size into a UTF-16 string, so a hostile or corrupt `.r15proj` opened over dirty work could crash the app and lose the work the guard protects. `ReadFile` checks `FileInfo.Length` against `MaxProjectBytes` (512 MiB, new v5 surface, provisional) and throws `ProjectFormatException("the file is <n> MB; the limit is 512 MB")`, which becomes 4.1's alert prefix like every other refusal. A total decoded-pixels budget across layers is an M5 or M6 carry-forward (Task 5's left-open list): it changes the engine. Pinned by `ProjectFileServiceTests.A_file_over_the_size_limit_alerts_and_changes_nothing`, which reads through `ReadFile(path, maxBytes: 1 MiB)` so the test writes 1.5 MiB, not 512.
- **T1-R15: the atomic write replaces an existing file** (critic L7). `File.Move(tmp, path, overwrite: true)` drops the target's attributes, ACL and alternate streams; `File.Replace(tmp, path, null)` keeps them. `WriteTo` replaces when the target exists and moves when it does not; both fail with `IOException` or `UnauthorizedAccessException` on a locked or read-only target, which the existing catch turns into the status line. Pinned by `A_failed_write_keeps_the_existing_file_and_stays_dirty` (locked target) and `Save_work_with_a_current_file_writes_there_without_a_dialog` (replace).
- **T1-R16: the close cancels a pending read before it asks** (critic L6, ruled; not applied). The critic proposed asking first and cancelling the read only when the close proceeds, so a Cancel keeps the Open the user started. The read's continuation is posted to the dispatcher, so it would run inside the prompt's nested loop: a second prompt for the Open would stack on the close prompt and could swap the document under it. A close during a read therefore drops that read, as before; the only cost is that a close the user then cancels leaves the Open to be started again. `OnClosing` is unchanged from the draft.

**Measured while drafting:**

- **Setup.** Two scratch copies of `3f3c5c5` made with `git archive HEAD` under `%TEMP%` (outside the repo; the repo stayed clean), .NET SDK 10.0.302, this machine's display. No app launch, no dialog shown, no input of any kind.
- **Baseline:** Engine.Tests 880, App.Tests 644, Parity.Tests 35, all green.
- **The finished task:** `dotnet build src/RBX15.slnx -c Release -warnaserror` gave 0 warnings and 0 errors; `dotnet test` gave Engine.Tests 885, App.Tests 714 (715 with the reconciler's fact, below), Parity.Tests 35, all passing, in three consecutive full runs.
- **Each step boundary** was rebuilt from a clean copy with only that step's files applied, and gave exactly the counts in the table below (Steps 3, 5, 7, 9 and 11).
- **Filters:** Step 3's 41 (HistoryTests 23, ImagesCapsTests 18), Step 5's 78 (EditorSessionTests 39, CanvasInteractionTests 39), Step 7's 33 (JsonSettingsStoreTests 26, ThemedDialogTests 7), Step 9's 30 (31 with the reconciler's size-cap fact; run three times), Step 11's 97 (ShellTests 25, WindowTitleTests 6, FileGroupTests 13, Task4ViewTests 6, HomeRibbonViewTests 13, ShellLookTests 5, ShellWiringTests 2, LedgerScenarioTests 27).
- **Failing first, measured:** every "write the failing tests" step (2, 4, 6, 8, 10) was applied to a copy holding only the earlier steps, and its build failed with exactly the compiler errors its Expected line names. With `JsonSettingsStore`'s skip removed, `A_save_after_a_failed_read_keeps_the_sibling_keys` fails (`Expected: "{ "recent": ...`, `Actual: "{\r\n  "lastProjectFolder": ...`).
- **Findings that shaped the code:**
  - The ShellLookTests height bounds still hold with Save work and Open in row 1 (ribbon ≤ 155, area ≥ 531): the row gains width, not height.
  - `Window.Close()` on a never-shown window raises `Closing` and `Closed` (T1-R13).
  - A `Changing` handler that closes a gesture fires on the gesture's own `Apply` too; the canvas guards that with `_ownChange`, so the test that stands in for it subscribes after its own edit (`Save_closes_a_pending_gesture_before_it_reads_the_document`).
- `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` printed nothing on the finished copy.
- **Reconciler's changes** (critic M1 and L7), measured on a copy of the drafter's finished tree outside the repo: `ReadFile`'s size check, `MaxProjectBytes`, `File.Replace` and `A_file_over_the_size_limit_alerts_and_changes_nothing` built with 0 warnings; `ProjectFileServiceTests` passed 31 of 31, and the full run gave Engine.Tests 885, App.Tests 715, Parity.Tests 35, all green.

**Counts.** Parity.Tests stays 35 throughout.

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| Step 1 (baseline) | 880 | 644 | |
| Step 3 | 885 (880 + 5) | 644 | HistoryTests +2, ImagesCapsTests +3 |
| Step 5 | 885 | 653 (644 + 9) | EditorSessionTests +8, CanvasInteractionTests +1 |
| Step 7 | 885 | 666 (644 + 22) | JsonSettingsStoreTests +9, ThemedDialogTests +4 |
| Step 9 | 885 | 697 (644 + 53) | ProjectFileServiceTests +31 |
| **Step 11** | **885 (880 + 5)** | **715 (644 + 71)** | ShellTests +8, WindowTitleTests +6, FileGroupTests +3, LedgerScenarioTests +1; Task4ViewTests, HomeRibbonViewTests, ShellLookTests, ShellWiringTests ±0 |

- [ ] **Step 1: Baseline**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 880, Failed: 0, Skipped: 0`; Parity.Tests `Passed: 35, Failed: 0`; App.Tests `Passed: 644, Failed: 0`.

On a display scaled to 125%, `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` may fail before any change (M3a's layout-rounding assertion; see the M3b plan's count note). Report it as pre-existing; do not change it.

- [ ] **Step 2: Write the failing engine tests (History.Snapshots, Images.Release)**

Ledger: `project-load-flow` (R11, the outgoing images after Open). At the end of the class in `src/RBX15.Engine.Tests/HistoryTests.cs` (the old block is the file's last two lines):

Replace:

```csharp
    }
}
```

with:

```csharp
    }

    [Fact] // M4a R11: Open reads every recorded snapshot before Clear, undo stack then redo stack, oldest first
    public void Snapshots_lists_undo_then_redo_oldest_first_as_a_copy()
    {
        var h = new History();
        Assert.Empty(h.Snapshots);
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        h.Record(Snap("C"));
        h.Undo(Snap("D"));                   // undo [A, B], redo [D]
        h.Undo(Snap("C2"));                  // undo [A], redo [D, C2]
        var listed = h.Snapshots;
        Assert.Equal(new[] { "A", "D", "C2" }, listed.Select(Label));
        h.Clear();
        Assert.Equal(new[] { "A", "D", "C2" }, listed.Select(Label));   // a copy: Clear after reading does not empty it
        Assert.Empty(h.Snapshots);
    }

    [Fact] // M4a R11: Snapshots shares the recorded documents by reference, so Open can collect their images
    public void Snapshots_are_the_recorded_instances()
    {
        var h = new History();
        var a = Snap("A");
        h.Record(a);
        Assert.Same(a, Assert.Single(h.Snapshots));
    }
}
```

At the end of the class in `src/RBX15.Engine.Tests/ImagesCapsTests.cs` (the file's last two lines):

Replace:

```csharp
    }
}
```

with:

```csharp
    }

    // ---- Release (M4a interfaces R11: the outgoing document's images after Open) ----

    [Fact] // the image, its premultiplied drawing copy, its decode-cache pre-scale and its RGB shifts are all freed
    public void Release_frees_the_image_and_every_derived_cache_entry()
    {
        var image = Images.Decode(RealPng(64, 64));                 // un-premultiplied RGBA, so ForDrawing makes a copy
        var premul = Images.ForDrawing(image);
        Assert.NotSame(image, premul);
        var scaled = Images.DecodeCacheScaled(premul, 16, 16);
        var shifted = Adjust.ShiftRgb(image, 10, 0, 0);
        var shiftedPremul = Images.ForDrawing(shifted);

        Images.Release(image);

        Assert.All(new[] { image, premul, scaled, shifted, shiftedPremul }, i => Assert.Equal(IntPtr.Zero, i.Handle));
    }

    [Fact] // an image that is already premultiplied kN32 is its own drawing copy: it is disposed once, and a second Release is a no-op
    public void Release_is_idempotent_and_safe_when_the_image_is_its_own_drawing_copy()
    {
        SKImage image;
        using (var surface = SKSurface.Create(new SKImageInfo(8, 8, SKImageInfo.PlatformColorType, SKAlphaType.Premul)))
        {
            surface.Canvas.Clear(SKColors.Red);
            image = surface.Snapshot();
        }
        Assert.Same(image, Images.ForDrawing(image));
        Images.Release(image);
        Images.Release(image);
        Assert.Equal(IntPtr.Zero, image.Handle);
        Assert.Throws<ArgumentNullException>(() => Images.Release(null!));
    }

    [Fact] // releasing one image leaves another image's cache entries alone, and a new image of the same pixels draws
    public void Release_leaves_other_images_alone()
    {
        var a = Images.Decode(RealPng(8, 8));
        var b = Images.Decode(RealPng(8, 8));
        var bPremul = Images.ForDrawing(b);
        Images.Release(a);
        Assert.NotEqual(IntPtr.Zero, b.Handle);
        Assert.Same(bPremul, Images.ForDrawing(b));
        Assert.NotEqual(IntPtr.Zero, bPremul.Handle);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in RBX15.Engine.Tests with `CS0117: 'Images' does not contain a definition for 'Release'` and `CS1061: 'History' does not contain a definition for 'Snapshots'` (measured).

- [ ] **Step 3: History.Snapshots, Images.Release and Adjust.TakeShifted**

In `src/RBX15.Engine/History.cs`:

Replace:

```csharp
    /// <summary>
    /// Records the state from BEFORE a change: pushes it onto undo, evicts the oldest past
```

with:

```csharp
    /// <summary>
    /// Every recorded snapshot, the undo stack then the redo stack, each oldest first, as a copy. Opening a
    /// project reads it before <see cref="Clear"/> so the session can release the images only history still
    /// holds (M4a interfaces R11).
    /// </summary>
    public IEnumerable<EditorSnapshot> Snapshots => [.. _undo, .. _redo];

    /// <summary>
    /// Records the state from BEFORE a change: pushes it onto undo, evicts the oldest past
```

In `src/RBX15.Engine/Adjust.cs` (T1-R4):

Replace:

```csharp
    /// <summary>
    /// applyRGBShift on exact un-premultiplied pixels: add per channel, clamp 0..255, skip alpha 0,
```

with:

```csharp
    /// <summary>
    /// Removes and returns every cached shift of <paramref name="source"/>, for <see cref="Images.Release"/>,
    /// which disposes them. Empty when there are none.
    /// </summary>
    internal static IReadOnlyList<SKImage> TakeShifted(SKImage source)
    {
        if (!ShiftCache.TryGetValue(source, out var slots)) return [];
        ShiftCache.Remove(source);
        lock (slots)
        {
            var taken = slots.Values.ToList();
            slots.Clear();
            return taken;
        }
    }

    /// <summary>
    /// applyRGBShift on exact un-premultiplied pixels: add per channel, clamp 0..255, skip alpha 0,
```

In `src/RBX15.Engine/Images.cs`, before `ScalePixels`:

Replace:

```csharp

    static SKImage ScalePixels(SKImage source, int width, int height, SKSamplingOptions sampling)
```

with:

```csharp

    /// <summary>
    /// Frees a layer or background image the editor no longer references (M4a interfaces R11: the outgoing
    /// document after Open). Disposes the image's RGB-shifted copies (<see cref="Adjust.ShiftRgb"/>) with their
    /// own derived entries, its premultiplied drawing copy (<see cref="ForDrawing"/>) unless that copy is the
    /// image itself, the decode-cache pre-scale keyed by either, and then the image. Idempotent; the caller
    /// guarantees nothing draws the image afterwards. UI thread.
    /// </summary>
    public static void Release(SKImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        foreach (var shifted in Adjust.TakeShifted(image)) Release(shifted);
        if (Premultiplied.TryGetValue(image, out var premul))
        {
            Premultiplied.Remove(image);
            ReleaseScaled(premul);
            if (!ReferenceEquals(premul, image)) premul.Dispose();
        }
        ReleaseScaled(image);
        image.Dispose();
    }

    static void ReleaseScaled(SKImage key)
    {
        if (!Scaled.TryGetValue(key, out var entry)) return;
        Scaled.Remove(key);
        if (!ReferenceEquals(entry.Result, key)) entry.Result.Dispose();
    }

    static SKImage ScalePixels(SKImage source, int width, int height, SKSamplingOptions sampling)
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.HistoryTests|FullyQualifiedName~RBX15.Engine.Tests.ImagesCapsTests"`
Expected: `Passed!  - Failed:     0, Passed:    41, Skipped:     0, Total:    41` for `RBX15.Engine.Tests.dll` (HistoryTests 23, ImagesCapsTests 18).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 885, Failed: 0, Skipped: 0` (880 + 5); Parity.Tests `35`; App.Tests `644`.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/History.cs src/RBX15.Engine/Images.cs src/RBX15.Engine/Adjust.cs src/RBX15.Engine.Tests/HistoryTests.cs src/RBX15.Engine.Tests/ImagesCapsTests.cs
git commit -m "feat(engine): History.Snapshots and Images.Release for the images an Open leaves behind"
```

- [ ] **Step 4: Write the failing session tests (the saved marker, MarkSaved, CommitPendingEdits, Open)**

Ledger: `project-load-flow`, `project-load-history-interplay`, `project-template-name-not-saved`, `shell-no-unsaved-changes-guard`. In `src/RBX15.App.Tests/EditorSessionTests.cs`, change the one assertion R2 names:

Replace:

```csharp
            Assert.Equal(0, s.UndoCount);
            Assert.False(s.IsDirty);
            Assert.True(s.CanUndo);
```

with:

```csharp
            Assert.Equal(0, s.UndoCount);
            Assert.True(s.IsDirty);                 // M4a R2: IsDirty is the live content against the saved marker
            Assert.True(s.CanUndo);
```

and add, before `Redo_during_an_unchanged_gesture_still_redoes`:

Replace:

```csharp

    [Fact] // an unchanged gesture leaves redo alone
```

with:

```csharp

    // ---- M4a: the saved-state marker, MarkSaved, CommitPendingEdits and Open (interfaces R2, R11) ----

    static ProjectLoad Load(Document doc, int nextId) => new(doc, nextId, []);

    static SKImage Img(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    [Fact] // R2 (Este, 2026-10-16): undo back to the saved state reads clean; redo away from it reads dirty
    public void Undo_back_to_the_saved_state_reads_clean()
    {
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { BgColor = "#000001" }));
        Assert.True(s.IsDirty);
        Assert.True(s.Undo());
        Assert.False(s.IsDirty);                              // back at the starting document
        Assert.True(s.Redo());
        Assert.True(s.IsDirty);

        s.MarkSaved(@"C:\p\a.r15proj", s.Document);
        Assert.False(s.IsDirty);
        Assert.True(s.Apply(d => d with { BgColor = "#000002" }));
        Assert.True(s.IsDirty);
        Assert.True(s.Undo());
        Assert.False(s.IsDirty);                              // back at the save, though history still holds a step
        Assert.True(s.Undo());
        Assert.True(s.IsDirty);                               // before the save
    }

    [Fact] // R2: "differs from the last save": an edit made by hand back to the saved values reads clean
    public void An_edit_back_to_the_saved_values_reads_clean()
    {
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        Assert.True(s.Apply(d => d with { ShowGrid = false }));
        Assert.Equal(2, s.UndoCount);
        Assert.False(s.IsDirty);
    }

    [Fact] // MarkSaved sets the file and the marker and raises File alone; IsDirty flips with no Document change
    public void MarkSaved_sets_the_file_and_raises_only_File()
    {
        var s = new EditorSession();
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        var seen = Events(s);
        s.MarkSaved(@"C:\p\shirt.r15proj", s.Document);
        Assert.Equal(@"C:\p\shirt.r15proj", s.CurrentFile);
        Assert.False(s.IsDirty);
        Assert.Equal([SessionChange.File], seen);
        Assert.Equal(1, s.UndoCount);                         // saving is not an undo step and keeps history
        Assert.Throws<ArgumentException>(() => s.MarkSaved("", s.Document));
        Assert.Throws<ArgumentNullException>(() => s.MarkSaved(null!, s.Document));
        Assert.Throws<ArgumentNullException>(() => s.MarkSaved("x.r15proj", null!));
    }

    [Fact] // M3b carry-forward: Save raises Changing first, so the canvas closes a wheel burst or a select drag; nothing else happens
    public void CommitPendingEdits_raises_Changing_and_nothing_else()
    {
        var s = new EditorSession();
        int changing = 0;
        s.Changing += (_, _) => changing++;
        var seen = Events(s);
        s.CommitPendingEdits();
        Assert.Equal(1, changing);
        Assert.Empty(seen);
    }

    [Fact] // project-load-flow, project-load-history-interplay (fix): Open swaps everything at once, clears history, reads clean
    public void Open_swaps_the_document_clears_history_and_selection_and_reads_clean()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2));
        s.Select(2);
        s.TemplateTitle = "Hoodie";
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        Assert.True(s.Undo());
        Assert.Equal((0, 1), (s.UndoCount, s.RedoCount));
        var order = new List<string>();
        s.Changing += (_, _) => order.Add("Changing");
        s.Changed += (_, e) => order.Add(e.What.ToString());
        var loaded = WithLayers(new Document { Mode = TemplateMode.Pants }, 7);

        s.Open(Load(loaded, 8), @"C:\p\pants.r15proj");

        Assert.Same(loaded, s.Document);
        Assert.Equal(loaded.Layers[^1].Id, s.SelectedLayerId); // T1-R2: top layer selected, editor.js:2664
        Assert.Equal(8, s.NextId);
        Assert.Equal((0, 0), (s.UndoCount, s.RedoCount));
        Assert.False(s.CanUndo);
        Assert.Equal(@"C:\p\pants.r15proj", s.CurrentFile);
        Assert.False(s.IsDirty);
        Assert.Equal("Hoodie", s.TemplateTitle);              // R1: the template name is not part of the file
        Assert.Equal(new[] { "Changing", "Document, Selection, History, File, Replaced" }, order);
        Assert.Throws<ArgumentNullException>(() => s.Open(null!, "x"));
        Assert.Throws<ArgumentException>(() => s.Open(Load(loaded, 8), ""));
    }

    [Fact] // the id counter comes from the load, never below the loaded ids
    public void Open_takes_the_loads_next_id_but_never_below_its_layers()
    {
        var s = new EditorSession(WithLayers(new Document(), 1, 2, 3, 4, 5));
        s.Open(Load(WithLayers(new Document(), 2), 3), "a.r15proj");
        Assert.Equal(3, s.NextId);                            // lower than before: history is gone, so no id can come back
        s.Open(Load(WithLayers(new Document(), 9), 4), "b.r15proj");
        Assert.Equal(10, s.NextId);
    }

    [Fact] // R11: the outgoing images are the old document's, the saved marker's, history's and a gesture start's, minus the new document's
    public void Open_returns_every_outgoing_image_except_those_the_new_document_holds()
    {
        SKImage a = Img(SKColors.Red), b = Img(SKColors.Green), c = Img(SKColors.Blue), bg = Img(SKColors.White), kept = Img(SKColors.Black), fresh = Img(SKColors.Gray);
        var start = new Document { BgImage = bg, Layers = [new Layer { Id = 1, Image = a }, new Layer { Id = 2, Image = kept }] };
        var s = new EditorSession(start);                     // the saved marker holds a, kept and bg
        Assert.True(s.Apply(d => d with { BgImage = null, Layers = [new Layer { Id = 3, Image = b }, new Layer { Id = 2, Image = kept }] }));
        var gesture = s.BeginGesture();
        Assert.True(s.Apply(d => d with { Layers = d.Layers.Add(new Layer { Id = 4, Image = c }) }));   // c lives only in the live document

        var released = s.Open(Load(new Document { Layers = [new Layer { Id = 9, Image = kept }, new Layer { Id = 10, Image = fresh }] }, 11), "x.r15proj");

        Assert.Equal(new HashSet<SKImage>([a, b, c, bg]), new HashSet<SKImage>(released));
        Assert.Equal(4, released.Count);                      // each once, though a and kept sit in several places
        gesture.Dispose();
    }

    [Fact] // Open abandons an open gesture: its stale Dispose records nothing, and a new gesture works as usual
    public void A_gesture_open_across_an_Open_is_abandoned_and_its_stale_dispose_does_nothing()
    {
        var s = new EditorSession();
        var stale = s.BeginGesture();
        Assert.True(s.Apply(d => d with { BgOpacity = 0.5 }));
        s.Open(Load(new Document(), 1), "x.r15proj");
        Assert.False(s.InGesture);
        Assert.False(s.CanUndo);
        var seen = Events(s);
        stale.Dispose();
        Assert.Equal((0, false), (s.UndoCount, s.InGesture));
        Assert.Empty(seen);

        using (s.BeginGesture())
        {
            Assert.True(s.Apply(d => d with { BgOpacity = 0.25 }));
            stale.Dispose();                                  // still a no-op, even with a new gesture open
            Assert.True(s.InGesture);
        }
        Assert.Equal(1, s.UndoCount);
        Assert.True(s.IsDirty);
    }

    [Fact] // an unchanged gesture leaves redo alone
```

In `src/RBX15.App.Tests/CanvasInteractionTests.cs`, before `Capture_loss_mid_stroke_commits_it_at_the_last_point` (T1-R5):

Replace:

```csharp

    [Fact] // draw-release-outside-canvas fix (R10): a lost capture commits the stroke at the last point
```

with:

```csharp

    [Fact] // M4a R11: an Open mid stroke discards it, as a tool switch does; the release that follows adds nothing to the opened file
    public void An_open_mid_stroke_discards_it()
    {
        var rig = new Rig();
        rig.Tools.Set(Tool.Pen);
        rig.I.PointerDown(new Point(100, 100), 1);
        rig.I.PointerMove(new Point(150, 150), 1);
        rig.Session.Open(new ProjectLoad(new Document(), 1, []), "x.r15proj");
        Assert.Equal((CanvasGesture.None, (LivePreview?)null), (rig.I.Gesture, rig.I.Preview));
        rig.I.PointerUp(new Point(150, 150), 1);
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal((0, false), (rig.Session.UndoCount, rig.Session.IsDirty));
    }

    [Fact] // draw-release-outside-canvas fix (R10): a lost capture commits the stroke at the last point
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in RBX15.App.Tests with `CS1061` naming `MarkSaved`, `CommitPendingEdits` and `Open` on `EditorSession`, and `CS0117: 'SessionChange' does not contain a definition for 'File'` (measured).

- [ ] **Step 5: EditorSession's saved marker and Open; the canvas cancels on Replaced**

Replace `src/RBX15.App/EditorSession.cs` with:

```csharp
using RBX15.Engine;
using SkiaSharp;

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

    /// <summary>
    /// <see cref="EditorSession.CurrentFile"/> or the saved-state marker changed, so
    /// <see cref="EditorSession.IsDirty"/> may flip without a Document change (M4a).
    /// </summary>
    File = 16,

    /// <summary><see cref="EditorSession.Open"/> swapped the whole document; always raised with Document | Selection | History | File.</summary>
    Replaced = 32,
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
/// <para>
/// <b>Unsaved changes</b> (M4a interfaces R2; Este's ruling 2026-10-16; 4.1 had no dirty tracking at all,
/// ledger shell-no-unsaved-changes-guard): the session keeps the document last saved or opened (the starting
/// document before either) and <see cref="IsDirty"/> compares the live document's content with it. Documents are
/// immutable and Undo restores the recorded instance, so undoing back to the save reads clean, and an edit made by
/// hand back to the saved values reads clean too.
/// </para>
/// </summary>
public sealed class EditorSession
{
    readonly History _history;
    int _gestureDepth;
    EditorSnapshot? _gestureStart;
    int _generation;      // bumped by Open, which abandons open gestures: a stale scope's Dispose then does nothing
    Document _saved;      // the saved-state marker (R2)
    string _templateTitle = "";

    /// <param name="document">The starting document; null gives 4.1's startup state (editor.js:76-107, the <see cref="Engine.Document"/> defaults).</param>
    /// <param name="historyCapacity">Undo depth; 4.1 uses 30 (editor.js:86).</param>
    public EditorSession(Document? document = null, int historyCapacity = 30)
    {
        _history = new History(historyCapacity);
        Document = document ?? new Document();
        _saved = Document;
        NextId = MaxLayerId(Document) + 1;
    }

    public Document Document { get; private set; }

    public int? SelectedLayerId { get; private set; }

    /// <summary>The selected layer, or null (getSelected, editor.js:582).</summary>
    public Layer? SelectedLayer => SelectedLayerId is int id ? Document.Layers.Find(l => l.Id == id) : null;

    /// <summary>
    /// The next layer id. Starts at 1 (editor.js:499) and only grows: undo and redo never lower it, and
    /// adopting a document whose ids reach it raises it past them (editor.js:1465). <see cref="Open"/> sets it
    /// from the loaded file (editor.js:2663), with history cleared.
    /// </summary>
    public int NextId { get; private set; }

    /// <summary>The open project file: null until a Save or an Open. Set only by <see cref="Open"/> and <see cref="MarkSaved"/>.</summary>
    public string? CurrentFile { get; private set; }

    /// <summary>
    /// True when the document's content differs from the last save or open (R2): one
    /// <see cref="Engine.Document.ContentEquals"/> against the saved marker, O(layers + fills). Inside an open
    /// gesture it reflects the live content.
    /// </summary>
    public bool IsDirty => !_saved.ContentEquals(Document);

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

    /// <summary>
    /// Raised first by every change entry point (<see cref="Apply"/>, <see cref="ApplyWithSelection"/>,
    /// <see cref="BeginGesture"/>, <see cref="Undo"/>, <see cref="Redo"/>, <see cref="Open"/>,
    /// <see cref="CommitPendingEdits"/>), before it changes anything. The canvas closes an open wheel burst on it,
    /// so an unrelated edit is never folded into the burst's step (M3b R7).
    /// </summary>
    public event EventHandler? Changing;

    void RaiseChanging() => Changing?.Invoke(this, EventArgs.Empty);

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
        RaiseChanging();
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
        RaiseChanging();
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
    /// Raises <see cref="Changing"/> and nothing else, so the canvas closes an open wheel burst or select drag
    /// before a save reads the document (M3b carry-forward). Save calls it before it takes its snapshot.
    /// </summary>
    public void CommitPendingEdits() => RaiseChanging();

    /// <summary>
    /// Records a successful write of <paramref name="saved"/> to <paramref name="path"/>: the current file and the
    /// saved marker, so <see cref="IsDirty"/> reads false while the document's content equals it. Raises File.
    /// </summary>
    public void MarkSaved(string path, Document saved)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(saved);
        CurrentFile = path;
        _saved = saved;
        Raise(SessionChange.File);
    }

    /// <summary>
    /// The Open entry point (planning contract: the swap goes through an entry point that raises Changing).
    /// <list type="number">
    /// <item>Raises <see cref="Changing"/>, so the canvas closes a wheel burst and a select drag first.</item>
    /// <item>Abandons any still-open gesture: depth 0, no start, and a new generation, so a stale scope's Dispose
    ///   is a no-op.</item>
    /// <item>Collects the images held by the outgoing document, the saved marker, every history snapshot and an
    ///   open gesture's start (R11).</item>
    /// <item>Adopts the loaded document with no selection (planning contract) and the load's
    ///   <see cref="ProjectLoad.NextId"/> (editor.js:2663), clears history (spec §3; ledger
    ///   project-load-history-interplay, fix: 4.1 kept it, editor.js:2593-2679), and sets
    ///   <see cref="CurrentFile"/> and the saved marker, so the session reads clean.</item>
    /// </list>
    /// <see cref="TemplateTitle"/> is untouched (R1; ledger project-template-name-not-saved, keep, editor.js:1536-1537).
    /// Raises Document | Selection | History | File | Replaced once. Returns the collected images minus any the
    /// new document holds, for the caller to release once nothing draws them.
    /// </summary>
    public IReadOnlyCollection<SKImage> Open(ProjectLoad load, string path)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentException.ThrowIfNullOrEmpty(path);
        RaiseChanging();
        var outgoing = new HashSet<SKImage>(ReferenceEqualityComparer.Instance);
        void Collect(Document doc)
        {
            if (doc.BgImage is { } bg) outgoing.Add(bg);
            foreach (var layer in doc.Layers) outgoing.Add(layer.Image);
        }
        Collect(Document);
        Collect(_saved);
        foreach (var snapshot in _history.Snapshots) Collect(snapshot.Document);
        if (_gestureStart is { } start) Collect(start.Document);

        _gestureDepth = 0;
        _gestureStart = null;
        _generation++;
        _history.Clear();
        Document = load.Document;
        SelectedLayerId = Document.Layers.IsEmpty ? null : Document.Layers[^1].Id;   // T1-R2: editor.js:2664 selects the top layer
        NextId = Math.Max(load.NextId, MaxLayerId(Document) + 1);
        CurrentFile = path;
        _saved = Document;

        if (Document.BgImage is { } kept) outgoing.Remove(kept);
        foreach (var layer in Document.Layers) outgoing.Remove(layer.Image);
        Raise(SessionChange.Document | SessionChange.Selection | SessionChange.History | SessionChange.File | SessionChange.Replaced);
        return outgoing.ToArray();
    }

    /// <summary>
    /// Opens a gesture. Until the outermost scope is disposed, <see cref="Apply"/> and
    /// <see cref="ApplyWithSelection"/> adopt each change without recording. Disposing the outermost
    /// scope records the state from its opening once, iff the content changed (ledger
    /// fillstyle-undo-snapshot-timing: one slider drag is one step). Scopes nest by count, and disposing
    /// a scope twice does nothing. A scope opened before an <see cref="Open"/> does nothing when disposed.
    /// </summary>
    public IDisposable BeginGesture()
    {
        RaiseChanging();
        if (_gestureDepth++ == 0) _gestureStart = Snapshot;
        return new GestureScope(this, _generation);
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
        RaiseChanging();
        var what = RecordGesture();
        var current = Snapshot;
        var target = redo ? _history.Redo(current) : _history.Undo(current);
        if (target is not null)
        {
            Document = target.Document;
            SelectedLayerId = target.SelectedLayerId;
            BumpNextId(Document);
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
        return SessionChange.History;
    }

    void EndGesture(int generation)
    {
        if (generation != _generation || _gestureDepth == 0 || --_gestureDepth > 0) return;
        var what = RecordGesture();
        _gestureStart = null;
        if (what != SessionChange.None) Raise(what);
    }

    void BumpNextId(Document doc) => NextId = Math.Max(NextId, MaxLayerId(doc) + 1);

    static int MaxLayerId(Document doc) => doc.Layers.IsEmpty ? 0 : doc.Layers.Max(l => l.Id);

    static int? Resolve(Document doc, int? id) => id is int v && doc.Layers.Exists(l => l.Id == v) ? v : null;

    void Raise(SessionChange what) => Changed?.Invoke(this, new SessionChangedEventArgs(what));

    sealed class GestureScope(EditorSession session, int generation) : IDisposable
    {
        bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            session.EndGesture(generation);
        }
    }
}
```

In `src/RBX15.App/Canvas/CanvasInteraction.cs`, the constructor's `Changed` handler (T1-R5):

Replace:

```csharp
        {
            if (e.What.HasFlag(SessionChange.Selection)) EndWheelBurst();
```

with:

```csharp
        {
            // M4a R11: an Open swapped the document under a stroke or an erase; their points and preview belong to the old one.
            if (e.What.HasFlag(SessionChange.Replaced)) Cancel();
            if (e.What.HasFlag(SessionChange.Selection)) EndWheelBurst();
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.EditorSessionTests|FullyQualifiedName~RBX15.App.Tests.CanvasInteractionTests"`
Expected: `Passed!  - Failed:     0, Passed:    78, Skipped:     0, Total:    78` for `RBX15.App.Tests.dll` (EditorSessionTests 39, CanvasInteractionTests 39).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `885`; Parity.Tests `35`; App.Tests `Passed: 653, Failed: 0` (644 + 9). Every other `IsDirty` assertion in the suite holds unchanged under R2 (FileGroupTests, FillStyleTests, ImportServiceTests, SettingsGroupTests check a document that never changed).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/EditorSession.cs src/RBX15.App/Canvas/CanvasInteraction.cs src/RBX15.App.Tests/EditorSessionTests.cs src/RBX15.App.Tests/CanvasInteractionTests.cs
git commit -m "feat(app): a saved-state dirty marker, MarkSaved and the Open swap on EditorSession"
```

- [ ] **Step 6: Write the failing settings and dialog tests**

Ledger: `shell-no-persistence` (last folder), `io-download-save-dialog`, `shell-no-unsaved-changes-guard`. In `src/RBX15.App.Tests/JsonSettingsStoreTests.cs`, before `An_unusable_placement_is_not_saved`:

Replace:

```csharp

    [Fact] // a non-finite or non-positive placement is never written
```

with:

```csharp

    // ---- M4a: the last folders (spec §3 "Saving": last folder remembered) ----

    [Fact] // each folder has its own root key, round-trips, and leaves the window and the other folder alone
    public void Folders_round_trip_under_their_own_keys_beside_the_window()
    {
        var project = Directory.CreateDirectory(Path.Combine(_dir, "projects")).FullName;
        var assets = Directory.CreateDirectory(Path.Combine(_dir, "assets")).FullName;
        var store = new JsonSettingsStore(_dir);
        Assert.Null(store.LoadFolder(SettingsFolder.Project));
        store.SaveWindowPlacement(new WindowPlacement(5, 6, 700, 500, false));
        store.SaveFolder(SettingsFolder.Project, project);
        store.SaveFolder(SettingsFolder.Asset, assets);

        var reread = new JsonSettingsStore(_dir);
        Assert.Equal(project, reread.LoadFolder(SettingsFolder.Project));
        Assert.Equal(assets, reread.LoadFolder(SettingsFolder.Asset));
        Assert.Equal(new WindowPlacement(5, 6, 700, 500, false), reread.LoadWindowPlacement());
        var root = JsonNode.Parse(File.ReadAllText(FilePath))!;
        Assert.Equal(project, root["lastProjectFolder"]!.GetValue<string>());
        Assert.Equal(assets, root["lastAssetFolder"]!.GetValue<string>());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Theory] // a folder that is gone, relative, or not a string reads as unset, so the dialog opens where Windows chooses
    [InlineData("""{ "lastProjectFolder": "C:\\no\\such\\rbx15\\folder" }""")]
    [InlineData("""{ "lastProjectFolder": "relative\\folder" }""")]
    [InlineData("""{ "lastProjectFolder": 42 }""")]
    [InlineData("""{ "lastProjectFolder": null }""")]
    [InlineData("""{ "lastProjectFolder": "" }""")]
    [InlineData("{ truncated")]
    public void A_missing_or_bad_folder_loads_as_unset(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);
        Assert.Null(new JsonSettingsStore(_dir).LoadFolder(SettingsFolder.Project));
    }

    [Fact] // a relative or empty folder is never written
    public void A_relative_or_empty_folder_is_not_saved()
    {
        var store = new JsonSettingsStore(_dir);
        store.SaveFolder(SettingsFolder.Project, "");
        store.SaveFolder(SettingsFolder.Project, "relative");
        Assert.False(File.Exists(FilePath));
    }

    [Fact] // interfaces §2.5 (M3b carry-forward): a save after a read that failed on a locked file is skipped, never written over
    public void A_save_after_a_failed_read_keeps_the_sibling_keys()
    {
        Directory.CreateDirectory(_dir);
        string original = """{ "recent": ["a.r15proj"], "window": { "left": 1, "top": 2, "width": 300, "height": 400, "maximized": false } }""";
        File.WriteAllText(FilePath, original);
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "p")).FullName;
        var store = new JsonSettingsStore(_dir);
        // Deny reading the file to the current user while leaving delete and replace allowed: the read fails with
        // UnauthorizedAccessException, and the atomic move over the file would succeed if the store tried it.
        var file = new FileInfo(FilePath);
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var deny = new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.ReadData,
            System.Security.AccessControl.AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(deny);
        file.SetAccessControl(acl);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllText(FilePath));
            Assert.Null(store.LoadFolder(SettingsFolder.Project));
            store.SaveFolder(SettingsFolder.Project, folder);
            store.SaveWindowPlacement(new WindowPlacement(9, 9, 900, 900, true));
        }
        finally
        {
            acl = file.GetAccessControl();
            acl.RemoveAccessRule(deny);
            file.SetAccessControl(acl);
        }
        Assert.Equal(original, File.ReadAllText(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact] // a non-finite or non-positive placement is never written
```

In `src/RBX15.App.Tests/ThemedDialogTests.cs`, the usings:

Replace:

```csharp
using System.Windows.Controls;
using RBX15.App.Views;
```

with:

```csharp
using System.Windows.Controls;
using RBX15.App.Services;
using RBX15.App.Views;
```

and the alert fact, followed by three new ones:

Replace:

```csharp
        Assert.False(Find<Button>(d, "CancelButton").IsCancel);
        Assert.False(d.IsConfirm);
    });
```

with:

```csharp
        Assert.False(Find<Button>(d, "CancelButton").IsCancel);
        Assert.Equal(Visibility.Collapsed, Find<Button>(d, "DontSaveButton").Visibility);
        Assert.False(d.IsConfirm);
    });

    [Fact] // M4a R13: Save (default, Enter), Don't save, Cancel (Esc), in the Windows order, with the product title
    public void The_save_changes_prompt_has_save_dont_save_and_cancel() => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Do you want to save changes to Untitled?", ThemedDialogKind.SaveChanges);
        Assert.Equal("RBX15 Classic Shirt and Pants Maker", d.Title);
        Assert.Equal("Do you want to save changes to Untitled?", Find<TextBlock>(d, "MessageText").Text);
        var ok = Find<Button>(d, "OkButton");
        var dontSave = Find<Button>(d, "DontSaveButton");
        var cancel = Find<Button>(d, "CancelButton");
        Assert.Equal(("Save", true, false), (ok.Content, ok.IsDefault, ok.IsCancel));
        Assert.Equal(("Don't save", false, false, Visibility.Visible), (dontSave.Content, dontSave.IsDefault, dontSave.IsCancel, dontSave.Visibility));
        Assert.Equal(("Cancel", true, Visibility.Visible), (cancel.Content, cancel.IsCancel, cancel.Visibility));
        var row = (StackPanel)ok.Parent;
        Assert.Equal(new object[] { ok, dontSave, cancel }, row.Children.Cast<object>());
        Assert.False(d.IsConfirm);
        Assert.Equal(SaveChoice.Cancel, d.Choice);          // nothing pressed yet, as the caption's close button leaves it
    });

    [Theory] // R13: each button's choice, set before DialogResult closes the dialog; the test raises Click on a dialog it never shows
    [InlineData("OkButton", SaveChoice.Save)]
    [InlineData("DontSaveButton", SaveChoice.DontSave)]
    public void Each_button_reports_its_choice(string button, SaveChoice expected) => StaThread.Run(() =>
    {
        var d = new ThemedDialog("Do you want to save changes to a.r15proj?", ThemedDialogKind.SaveChanges);
        var b = Find<Button>(d, button);
        // A window that was never shown refuses DialogResult; Click reaches the handler, which sets the choice first.
        Assert.Throws<InvalidOperationException>(() => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, b)));
        Assert.Equal(expected, d.Choice);
    });

    [Fact] // the existing (message, bool) constructor is the Alert and Confirm kinds
    public void The_bool_constructor_maps_to_alert_and_confirm() => StaThread.Run(() =>
    {
        Assert.True(new ThemedDialog("a", confirm: true).IsConfirm);
        Assert.True(new ThemedDialog("a", ThemedDialogKind.Confirm).IsConfirm);
        Assert.Equal(Visibility.Collapsed, Find<Button>(new ThemedDialog("a", ThemedDialogKind.Confirm), "DontSaveButton").Visibility);
        Assert.Equal("OK", Find<Button>(new ThemedDialog("a", ThemedDialogKind.Confirm), "OkButton").Content);
    });
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in RBX15.App.Tests with `CS0246` and `CS0103` naming `SaveChoice` (measured; `SettingsFolder` and `ThemedDialogKind` are reported once it resolves).

- [ ] **Step 7: The project dialogs, the remembered folders, the save-changes prompt and 4.1's dialog text**

Replace `src/RBX15.App/Services/IFileDialogs.cs` with:

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

    /// <summary>
    /// The Open dialog (btnLoadProject and #projectFileInput, editor.js:2683, editor.html:108; ledger project-load-flow):
    /// single select, the filters of <see cref="Win32FileDialogs.ProjectOpenFilter"/> (interfaces §2.2), starting in
    /// <paramref name="initialDirectory"/> when it exists. Null on cancel.
    /// </summary>
    string? OpenProject(string? initialDirectory);

    /// <summary>
    /// The project Save dialog (saveProject's anchor download, editor.js:2584-2590; ledger project-save-filename, keep):
    /// FileName = <paramref name="suggestedFileName"/>, filter <see cref="Win32FileDialogs.ProjectSaveFilter"/>,
    /// DefaultExt .r15proj, AddExtension and OverwritePrompt, starting in <paramref name="initialDirectory"/> when it
    /// exists. Null on cancel.
    /// </summary>
    string? SaveProject(string suggestedFileName, string? initialDirectory);

    /// <summary>
    /// The native folder picker (Open folder, 4.1's webkitdirectory input, editor.js:2378-2381; ledger io-folder-picker):
    /// one folder, starting in <paramref name="initialDirectory"/> when it exists. Null on cancel. Task 2 calls it.
    /// </summary>
    string? PickFolder(string? initialDirectory);
}
```

Replace `src/RBX15.App/Services/Win32FileDialogs.cs` with (interfaces §2.2; `Microsoft.Win32.OpenFolderDialog` is in WPF since .NET 8):

```csharp
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace RBX15.App.Services;

/// <summary>
/// The WPF wrappers over the Windows common file dialogs (no WinForms). Replaces 4.1's hidden
/// <c>&lt;input type=file&gt;</c> elements (editor.html:108-109, :314) and the anchor-download Save As
/// (editor.js:1535-1539, :2584-2590; ledger io-download-save-dialog). A native dialog returns on every pick, so
/// re-picking the same file works (file-input-same-file-reselect).
/// </summary>
public sealed class Win32FileDialogs(Func<Window?> owner) : IFileDialogs
{
    /// <summary>
    /// The Open filter. 4.1's input accepted <c>.json,.r15proj</c> (editor.html:108); the contract names .r15proj, so it
    /// comes first and JSON keeps 4.1's reach as a second entry (interfaces §2.2).
    /// </summary>
    public const string ProjectOpenFilter = "RBX15 project (*.r15proj)|*.r15proj|JSON project (*.json)|*.json";

    /// <summary>The Save filter (ledger project-save-filename portTarget).</summary>
    public const string ProjectSaveFilter = "RBX15 project (*.r15proj)|*.r15proj";

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

    public string? OpenProject(string? initialDirectory)
    {
        var dialog = new OpenFileDialog { Filter = ProjectOpenFilter, Multiselect = false };
        StartIn(dialog, initialDirectory);
        return Show(dialog) ? dialog.FileName : null;
    }

    public string? SaveProject(string suggestedFileName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = ProjectSaveFilter,
            DefaultExt = ".r15proj",
            AddExtension = true,
            OverwritePrompt = true,
        };
        StartIn(dialog, initialDirectory);
        return Show(dialog) ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog { Multiselect = false };
        StartIn(dialog, initialDirectory);
        return Show(dialog) ? dialog.FolderName : null;
    }

    /// <summary>A remembered folder that has since gone is ignored, so the dialog opens where Windows chooses.</summary>
    static void StartIn(CommonItemDialog dialog, string? directory)
    {
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) dialog.InitialDirectory = directory;
    }

    bool Show(CommonDialog dialog) => (owner() is { } o ? dialog.ShowDialog(o) : dialog.ShowDialog()) == true;
}
```

Replace `src/RBX15.App/Services/ISettingsStore.cs` with:

```csharp
namespace RBX15.App.Services;

/// <summary>Which remembered folder (spec §3 "Saving": last folder remembered).</summary>
public enum SettingsFolder
{
    /// <summary>The Save and Open dialogs' folder (<c>lastProjectFolder</c>).</summary>
    Project,

    /// <summary>The asset library's Open folder picker (<c>lastAssetFolder</c>, Task 2).</summary>
    Asset,
}

/// <summary>The app's small local settings file (spec §3 "Upgrades": the saved window position; spec §3 "Saving": the last folder).</summary>
public interface ISettingsStore
{
    /// <summary>The saved placement, or null when there is none or the file is unreadable.</summary>
    WindowPlacement? LoadWindowPlacement();

    /// <summary>Saves the placement. Best effort: a failure to write never throws.</summary>
    void SaveWindowPlacement(WindowPlacement placement);

    /// <summary>The remembered folder, or null when it is unset, unreadable, or no longer a directory.</summary>
    string? LoadFolder(SettingsFolder which);

    /// <summary>Remembers <paramref name="folder"/>. Best effort and atomic, keeps sibling keys, never throws, and skips the write after a failed read (interfaces §2.5).</summary>
    void SaveFolder(SettingsFolder which, string folder);
}
```

Replace `src/RBX15.App/Services/JsonSettingsStore.cs` with (§2.5; T1-R9):

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RBX15.App.Services;

/// <summary>
/// <c>settings.json</c> in one directory (the app passes <c>%LOCALAPPDATA%\RBX15</c>, which MSIX
/// redirects into the package's local data, removed on uninstall). The file holds one object:
/// <c>{ "window": { "left", "top", "width", "height", "maximized" }, "lastProjectFolder": "...", "lastAssetFolder": "..." }</c>.
/// Each save rewrites only its own key and keeps every other key it read.
/// <para>
/// Writes are atomic: the whole file goes to <c>settings.json.tmp</c>, then
/// <see cref="File.Move(string, string, bool)"/> replaces <c>settings.json</c>, so a crash mid-write
/// leaves the previous file intact. Reads never throw: a missing, unreadable, malformed or
/// out-of-range file gives null and the window opens at its default.
/// </para>
/// <para>
/// A save after a read that failed on an existing file (an <see cref="IOException"/> such as a sharing violation, or
/// <see cref="UnauthorizedAccessException"/>) is skipped, so a transient lock never makes a save drop the sibling keys
/// it could not read (M4a interfaces §2.5, the M3b carry-forward). A malformed file is still replaced.
/// </para>
/// </summary>
public sealed class JsonSettingsStore(string directory) : ISettingsStore
{
    public const string FileName = "settings.json";
    const string WindowKey = "window";

    /// <summary>The root key of each remembered folder.</summary>
    internal static string FolderKey(SettingsFolder which) => which switch
    {
        SettingsFolder.Project => "lastProjectFolder",
        SettingsFolder.Asset => "lastAssetFolder",
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
    };

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
        Update(WindowKey, new JsonObject
        {
            ["left"] = placement.Left,
            ["top"] = placement.Top,
            ["width"] = placement.Width,
            ["height"] = placement.Height,
            ["maximized"] = placement.Maximized,
        });
    }

    public string? LoadFolder(SettingsFolder which)
    {
        string key = FolderKey(which);
        if (ReadRoot()?[key] is not JsonValue v || !v.TryGetValue(out string? folder)) return null;
        try
        {
            return Path.IsPathFullyQualified(folder) && Directory.Exists(folder) ? folder : null;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SaveFolder(SettingsFolder which, string folder)
    {
        string key = FolderKey(which);
        if (string.IsNullOrEmpty(folder) || !Path.IsPathFullyQualified(folder)) return;
        Update(key, JsonValue.Create(folder));
    }

    /// <summary>Finite coordinates and a positive finite size; anything else is a corrupt file.</summary>
    static bool IsUsable(WindowPlacement p) =>
        double.IsFinite(p.Left) && double.IsFinite(p.Top) &&
        double.IsFinite(p.Width) && double.IsFinite(p.Height) && p.Width > 0 && p.Height > 0;

    /// <summary>Rewrites <paramref name="key"/> and keeps every other key; skipped when the existing file could not be read.</summary>
    void Update(string key, JsonNode value)
    {
        if (!TryReadRoot(out var root)) return;
        root ??= new JsonObject();
        root[key] = value;
        var temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: losing a remembered setting must never block closing the app or a save.
            try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
        }
    }

    JsonObject? ReadRoot() => TryReadRoot(out var root) ? root : null;

    /// <summary>
    /// False only when an existing file could not be read (IO or access). A missing file reads as true and null; a
    /// malformed or non-object file as true and null, so the next save replaces it.
    /// </summary>
    bool TryReadRoot(out JsonObject? root)
    {
        root = null;
        try
        {
            if (!File.Exists(FilePath)) return true;
            root = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject;
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

Replace `src/RBX15.App/Services/IDialogService.cs` with:

```csharp
namespace RBX15.App.Services;

/// <summary>The unsaved-changes prompt's answer (M4a interfaces R13).</summary>
public enum SaveChoice
{
    Save,
    DontSave,
    Cancel,
}

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

    /// <summary>
    /// The unsaved-changes prompt (R13; new in v5: 4.1 had none, ledger shell-no-unsaved-changes-guard and
    /// shell-unsaved-work-loss): <see cref="DialogText.SaveChanges"/> with Save (Enter), Don't save, and Cancel (Esc and
    /// the caption's close button).
    /// </summary>
    SaveChoice AskSaveChanges(string documentName);
}
```

Create `src/RBX15.App/Services/DialogText.cs` (dialogs-inventory, io-alert-inventory):

```csharp
namespace RBX15.App.Services;

/// <summary>
/// The dialog and status strings 4.1 showed, verbatim (ledger dialogs-inventory, io-alert-inventory), and the
/// new unsaved-changes prompt. M4a Task 1 owns every entry; Tasks 2 and 3 consume them. The two strings 4.1 already
/// had a home for stay where they are: <c>ExportGuard.EmptyTemplateMessage</c> (editor.js:1506) and
/// <c>CanvasInteraction.NoLayerToErase</c> (editor.js:1270, shown on the status strip).
/// </summary>
public static class DialogText
{
    /// <summary>editor.js:2344, loadBuiltinPack with no pack folder (Task 2).</summary>
    public static string PackNotFound(string packName) => $"Built-in pack \"{packName}\" not found. Reinstall the app to restore it.";

    /// <summary>editor.js:2364, loadBuiltinPack with an empty pack folder (Task 2).</summary>
    public static string PackEmpty(string packName) => $"Built-in pack \"{packName}\" is empty.";

    /// <summary>editor.js:2385, Open folder with no images (Task 2; R14 keeps the text though BMP is accepted).</summary>
    public const string NoImagesInFolder = "No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files.";

    /// <summary>
    /// editor.js:2676, <c>alert('Failed to load project. ' + err.message)</c>. v5's message is the
    /// <c>ProjectFormatException</c>'s or the read error's (ledger project-load-flow portTarget: "message text will differ").
    /// </summary>
    public static string LoadFailed(string message) => "Failed to load project. " + message;

    /// <summary>The unsaved-changes prompt (R13): the Windows wording, since 4.1 had no prompt.</summary>
    public static string SaveChanges(string documentName) => $"Do you want to save changes to {documentName}?";

    /// <summary>The document name before the first save, in the prompt and the window title (R3).</summary>
    public const string Untitled = "Untitled";
}
```

Replace `src/RBX15.App/Views/ThemedDialog.xaml` with (T1-R11):

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
         provisional; M3b look sign-off. The buttons are .btn-sm (padding 5px 10px, 11px, styles.css:407).
         M4a (interfaces R13): the unsaved-changes prompt reuses it as Save (the default, Enter), Don't save and Cancel
         (Esc), the Windows order; 4.1 had no such prompt, so the Don't save button copies Cancel's look. -->
    <Border BorderBrush="{DynamicResource BorderAccentBrush}" BorderThickness="0,1,0,0" Padding="20,18,20,16">
        <StackPanel>
            <TextBlock x:Name="MessageText"
                       TextWrapping="Wrap"
                       FontSize="13"
                       Margin="0,0,0,18" />
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button x:Name="OkButton"
                        Content="OK"
                        IsDefault="True"
                        MinWidth="72"
                        Padding="10,5"
                        FontSize="11"
                        Style="{DynamicResource PrimaryButton}"
                        Click="OnOk" />
                <Button x:Name="DontSaveButton"
                        Content="Don't save"
                        Visibility="Collapsed"
                        MinWidth="72"
                        Padding="10,5"
                        FontSize="11"
                        Margin="8,0,0,0"
                        Style="{DynamicResource SecondaryButton}"
                        Click="OnDontSave" />
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

Replace `src/RBX15.App/Views/ThemedDialog.xaml.cs` with:

```csharp
using System.Windows;
using System.Windows.Input;
using RBX15.App.Services;

namespace RBX15.App.Views;

/// <summary>What a <see cref="ThemedDialog"/> asks.</summary>
public enum ThemedDialogKind
{
    /// <summary>A message and OK (4.1's alert).</summary>
    Alert,

    /// <summary>OK and Cancel (4.1's confirm).</summary>
    Confirm,

    /// <summary>Save, Don't save and Cancel: the unsaved-changes prompt (M4a interfaces R13).</summary>
    SaveChanges,
}

/// <summary>
/// The themed dialog: a message with OK, plus Cancel for a confirm, or Save, Don't save and Cancel for the
/// unsaved-changes prompt. Shown modally by <see cref="ThemedDialogService"/>. <see cref="Choice"/> is Save on
/// OK or Save, DontSave on Don't save, and Cancel on Cancel, Esc or the caption's close button.
/// </summary>
public partial class ThemedDialog : Window
{
    SaveChoice _choice = SaveChoice.Cancel;

    public ThemedDialog(string message, bool confirm)
        : this(message, confirm ? ThemedDialogKind.Confirm : ThemedDialogKind.Alert)
    {
    }

    public ThemedDialog(string message, ThemedDialogKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        InitializeComponent();
        CaptionTheme.Apply(this);
        Title = AppInfo.ProductName;
        MessageText.Text = message;
        IsConfirm = kind == ThemedDialogKind.Confirm;
        switch (kind)
        {
            case ThemedDialogKind.Alert:
                CancelButton.Visibility = Visibility.Collapsed;
                CancelButton.IsCancel = false;
                PreviewKeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) Close(); // an alert closes on Esc too
                };
                break;
            case ThemedDialogKind.SaveChanges:
                OkButton.Content = "Save";
                DontSaveButton.Visibility = Visibility.Visible;
                break;
        }
    }

    public bool IsConfirm { get; }

    /// <summary>The button that closed the dialog; Cancel until one did.</summary>
    public SaveChoice Choice => _choice;

    void OnOk(object sender, RoutedEventArgs e)
    {
        _choice = SaveChoice.Save;
        DialogResult = true;
    }

    void OnDontSave(object sender, RoutedEventArgs e)
    {
        _choice = SaveChoice.DontSave;
        DialogResult = false;
    }
}
```

Replace `src/RBX15.App/Services/ThemedDialogService.cs` with:

```csharp
using System.Windows;
using RBX15.App.Views;

namespace RBX15.App.Services;

/// <summary><see cref="IDialogService"/> over <see cref="ThemedDialog"/>, owned by the main window when it is showing.</summary>
public sealed class ThemedDialogService(Func<Window?> owner) : IDialogService
{
    public bool Confirm(string message) => Show(message, ThemedDialogKind.Confirm).Choice == SaveChoice.Save;

    public void Alert(string message) => Show(message, ThemedDialogKind.Alert);

    public SaveChoice AskSaveChanges(string documentName) =>
        Show(DialogText.SaveChanges(documentName), ThemedDialogKind.SaveChanges).Choice;

    ThemedDialog Show(string message, ThemedDialogKind kind)
    {
        var dialog = new ThemedDialog(message, kind);
        if (owner() is { IsVisible: true } o) dialog.Owner = o;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        return dialog;
    }
}
```

Replace `src/RBX15.App.Tests/Fakes/FakeFileDialogs.cs` with (Addition 7):

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Returns canned picks and records what each dialog was asked (suggested names and starting folders).</summary>
sealed class FakeFileDialogs : IFileDialogs
{
    public IReadOnlyList<string> OpenResult { get; set; } = [];
    public string? SaveResult { get; set; }
    public int OpenCalls { get; private set; }
    public List<string> SaveSuggestions { get; } = [];

    /// <summary>OpenProject's answer (null = cancel) and the folder each call started in.</summary>
    public string? OpenProjectResult { get; set; }
    public List<string?> OpenProjectDirs { get; } = [];

    /// <summary>SaveProject's answer (null = cancel) and each call's suggested name and starting folder.</summary>
    public string? SaveProjectResult { get; set; }
    public List<(string Suggested, string? Dir)> SaveProjectCalls { get; } = [];

    /// <summary>PickFolder's answer (null = cancel) and the folder each call started in.</summary>
    public string? PickFolderResult { get; set; }
    public List<string?> PickFolderDirs { get; } = [];

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

    public string? OpenProject(string? initialDirectory)
    {
        OpenProjectDirs.Add(initialDirectory);
        return OpenProjectResult;
    }

    public string? SaveProject(string suggestedFileName, string? initialDirectory)
    {
        SaveProjectCalls.Add((suggestedFileName, initialDirectory));
        return SaveProjectResult;
    }

    public string? PickFolder(string? initialDirectory)
    {
        PickFolderDirs.Add(initialDirectory);
        return PickFolderResult;
    }
}
```

Replace `src/RBX15.App.Tests/Fakes/FakeSettingsStore.cs` with:

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An in-memory <see cref="ISettingsStore"/> that records every save.</summary>
internal sealed class FakeSettingsStore(WindowPlacement? saved = null) : ISettingsStore
{
    public WindowPlacement? Saved { get; private set; } = saved;

    public List<WindowPlacement> Saves { get; } = [];

    /// <summary>The remembered folders; LoadFolder returns these as they are (the fake does not check the disk).</summary>
    public Dictionary<SettingsFolder, string> Folders { get; } = [];

    public List<(SettingsFolder Which, string Folder)> FolderSaves { get; } = [];

    public WindowPlacement? LoadWindowPlacement() => Saved;

    public void SaveWindowPlacement(WindowPlacement placement)
    {
        Saves.Add(placement);
        Saved = placement;
    }

    public string? LoadFolder(SettingsFolder which) => Folders.GetValueOrDefault(which);

    public void SaveFolder(SettingsFolder which, string folder)
    {
        FolderSaves.Add((which, folder));
        Folders[which] = folder;
    }
}
```

Replace `src/RBX15.App.Tests/Fakes/FakeDialogService.cs` with:

```csharp
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>Records every dialog; Confirm answers <see cref="ConfirmAnswer"/>, the unsaved-changes prompt <see cref="SaveChoiceAnswer"/>.</summary>
sealed class FakeDialogService : IDialogService
{
    public bool ConfirmAnswer { get; set; }
    public List<string> Confirms { get; } = [];
    public List<string> Alerts { get; } = [];

    /// <summary>The prompt's answer; Cancel by default, so a test that forgets to answer never discards work.</summary>
    public SaveChoice SaveChoiceAnswer { get; set; } = SaveChoice.Cancel;

    /// <summary>The document name each unsaved-changes prompt named.</summary>
    public List<string> SaveChanges { get; } = [];

    public bool Confirm(string message)
    {
        Confirms.Add(message);
        return ConfirmAnswer;
    }

    public void Alert(string message) => Alerts.Add(message);

    public SaveChoice AskSaveChanges(string documentName)
    {
        SaveChanges.Add(documentName);
        return SaveChoiceAnswer;
    }
}
```

`LedgerScenarioTests` has its own private fakes (Addition 9). In `src/RBX15.App.Tests/LedgerScenarioTests.cs`, its `Dialogs` class:

Replace:

```csharp
        public void Alert(string message) => Alerts.Add(message);
    }
```

with:

```csharp
        public void Alert(string message) => Alerts.Add(message);

        public SaveChoice SaveAnswer { get; set; } = SaveChoice.Cancel;

        public List<string> SavePrompts { get; } = [];

        public SaveChoice AskSaveChanges(string documentName)
        {
            SavePrompts.Add(documentName);
            return SaveAnswer;
        }
    }
```

and its `Files` class:

Replace:

```csharp
            return savePath;
        }
    }

```

with:

```csharp
            return savePath;
        }

        public string? ProjectPath { get; set; }

        public List<string> ProjectSuggested { get; } = [];

        public string? OpenProject(string? initialDirectory) => ProjectPath;

        public string? SaveProject(string suggestedFileName, string? initialDirectory)
        {
            ProjectSuggested.Add(suggestedFileName);
            return ProjectPath;
        }

        public string? PickFolder(string? initialDirectory) => null;
    }

```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.JsonSettingsStoreTests|FullyQualifiedName~RBX15.App.Tests.ThemedDialogTests"`
Expected: `Passed!  - Failed:     0, Passed:    33, Skipped:     0, Total:    33` for `RBX15.App.Tests.dll` (JsonSettingsStoreTests 26, ThemedDialogTests 7).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `885`; Parity.Tests `35`; App.Tests `Passed: 666, Failed: 0` (644 + 22).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Services/IFileDialogs.cs src/RBX15.App/Services/Win32FileDialogs.cs src/RBX15.App/Services/ISettingsStore.cs src/RBX15.App/Services/JsonSettingsStore.cs src/RBX15.App/Services/IDialogService.cs src/RBX15.App/Services/DialogText.cs src/RBX15.App/Services/ThemedDialogService.cs src/RBX15.App/Views/ThemedDialog.xaml src/RBX15.App/Views/ThemedDialog.xaml.cs src/RBX15.App.Tests/Fakes/FakeFileDialogs.cs src/RBX15.App.Tests/Fakes/FakeSettingsStore.cs src/RBX15.App.Tests/Fakes/FakeDialogService.cs src/RBX15.App.Tests/LedgerScenarioTests.cs src/RBX15.App.Tests/JsonSettingsStoreTests.cs src/RBX15.App.Tests/ThemedDialogTests.cs
git commit -m "feat(app): project dialogs, remembered folders, the save-changes prompt and 4.1's dialog text"
```

- [ ] **Step 8: Write the failing ProjectFileService tests**

Ledger: `project-save-format`, `project-save-filename`, `file-project-format`, `project-format-r15proj`, `project-load-flow`, `project-load-layers`, `project-load-error-path-bugs`, `project-load-no-imgsrc-hang`, `project-load-untrusted-src`, `project-load-history-interplay`, `project-template-name-not-saved`, `io-download-save-dialog`, `shell-no-unsaved-changes-guard`, `dialogs-inventory`, `io-alert-inventory`. Review Focus candidates 1 and 2. Create `src/RBX15.App.Tests/ProjectFileServiceTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Save work, Save As, Open and the unsaved-changes guard (spec §3 "Saving"; saveProject and loadProject,
/// editor.js:2550-2687; interfaces R1, R2, R4, R11-R13; ledger project-save-format, project-save-filename,
/// project-load-flow, project-load-layers, project-load-error-path-bugs, project-load-history-interplay,
/// project-load-no-imgsrc-hang, project-load-untrusted-src, io-download-save-dialog, shell-no-unsaved-changes-guard).
/// Real files in a temp folder; fake dialogs; no window.
/// </summary>
public sealed class ProjectFileServiceTests : IDisposable
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly DateTimeOffset Noon = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class Rig
    {
        public Rig(EditorSession? session = null, Func<string, CancellationToken, Task<ProjectLoad>>? read = null)
        {
            Session = session ?? new EditorSession();
            Service = new ProjectFileService(Session, Files, Dialogs, Status, Settings, new FixedClock(Noon), read, Released.Add);
        }

        public EditorSession Session { get; }
        public FakeFileDialogs Files { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public FakeStatus Status { get; } = new();
        public FakeSettingsStore Settings { get; } = new();
        public List<IReadOnlyCollection<SKImage>> Released { get; } = [];
        public ProjectFileService Service { get; }

        public IEnumerable<SKImage> AllReleased => Released.SelectMany(r => r);
    }

    static SKImage Img(SKColor color, int w = 4, int h = 4)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    static string Url(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.AsSpan());
    }

    /// <summary>A copy of a frozen 4.1 fixture (tests/goldens/projects) in the temp folder.</summary>
    string Fixture(string name)
    {
        string path = Path.Combine(_files.Dir, name + ".r15proj");
        File.Copy(Path.Combine(Root, "tests", "goldens", "projects", name + ".r15proj"), path);
        return path;
    }

    string Text(string name, string json) => _files.Write(name, Encoding.UTF8.GetBytes(json));

    /// <summary>A session with one layer added through Apply, so it is dirty and has one step.</summary>
    static EditorSession Dirty(out SKImage image)
    {
        var s = new EditorSession();
        var img = Img(SKColors.Red);
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, img, s.TakeId(), "art")));
        image = img;
        return s;
    }

    // ---- Save (saveProject, editor.js:2550-2591) ----

    [Theory] // project-save-filename (keep) parityCheck: 'My Shirt v2!' -> 'My_Shirt_v2_.r15proj'; empty -> 'untitled.r15proj' (editor.js:2586-2587)
    [InlineData("My Shirt v2!", "My_Shirt_v2_.r15proj")]
    [InlineData("", "untitled.r15proj")]
    public void Save_work_with_no_current_file_is_save_as_with_4_1s_name(string title, string suggested)
    {
        var rig = new Rig(Dirty(out _));
        rig.Session.TemplateTitle = title;
        string remembered = Directory.CreateDirectory(Path.Combine(_files.Dir, "remembered")).FullName;
        rig.Settings.Folders[SettingsFolder.Project] = remembered;
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, suggested);

        Assert.True(rig.Service.Save());

        Assert.Equal([(suggested, (string?)remembered)], rig.Files.SaveProjectCalls);
        Assert.True(File.Exists(rig.Files.SaveProjectResult));
    }

    [Fact] // project-save-format: ProjectIO.Write's bytes, UTF-8 without a BOM, no title (R1); then clean, current file, status, folder
    public void Save_writes_the_project_marks_the_session_saved_and_says_so()
    {
        var rig = new Rig(Dirty(out _));
        rig.Session.TemplateTitle = "Hoodie";
        string path = Path.Combine(_files.Dir, "hoodie.r15proj");
        rig.Files.SaveProjectResult = path;

        Assert.True(rig.Service.SaveAs());

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.UTF8.GetBytes(ProjectIO.Write(rig.Session.Document, Noon, title: null)), bytes);
        Assert.NotEqual(0xEF, bytes[0]);                                   // no BOM
        var json = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!.AsObject();
        Assert.Equal("5.0", json["version"]!.GetValue<string>());
        Assert.Equal("2026-10-16T12:00:00.000Z", json["savedAt"]!.GetValue<string>());
        Assert.False(json.ContainsKey("title"));                          // project-template-name-not-saved (keep)
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        Assert.Equal(1, rig.Session.UndoCount);                           // saving keeps history
        Assert.Equal("Saved hoodie.r15proj.", rig.Status.Current);        // io-download-save-dialog fix: feedback after saving
        Assert.Equal([(SettingsFolder.Project, _files.Dir)], rig.Settings.FolderSaves);
        Assert.Equal(new[] { "hoodie.r15proj" }, Directory.GetFiles(_files.Dir).Select(Path.GetFileName));   // no temp file left
    }

    [Fact] // spec §3: Ctrl+S saves to the current file with no dialog
    public void Save_work_with_a_current_file_writes_there_without_a_dialog()
    {
        var rig = new Rig(Dirty(out _));
        string path = Path.Combine(_files.Dir, "a.r15proj");
        rig.Files.SaveProjectResult = path;
        Assert.True(rig.Service.Save());
        Assert.True(rig.Session.Apply(d => d with { BgColor = "#123456" }));
        Assert.True(rig.Session.IsDirty);

        Assert.True(rig.Service.Save());

        Assert.Single(rig.Files.SaveProjectCalls);
        Assert.False(rig.Session.IsDirty);
        Assert.Equal("#123456", ProjectIO.Read(File.ReadAllText(path)).Document.BgColor);
    }

    [Fact] // Save As suggests the current file's name in its own folder
    public void Save_as_suggests_the_current_name_in_the_current_folder()
    {
        var rig = new Rig(Dirty(out _));
        string sub = Directory.CreateDirectory(Path.Combine(_files.Dir, "sub")).FullName;
        rig.Files.SaveProjectResult = Path.Combine(sub, "first.r15proj");
        Assert.True(rig.Service.Save());
        rig.Settings.Folders[SettingsFolder.Project] = _files.Dir;            // the current file's folder wins over the remembered one
        rig.Files.SaveProjectResult = Path.Combine(sub, "second.r15proj");

        Assert.True(rig.Service.SaveAs());

        Assert.Equal(("first.r15proj", (string?)sub), rig.Files.SaveProjectCalls[1]);
        Assert.Equal(Path.Combine(sub, "second.r15proj"), rig.Session.CurrentFile);
    }

    [Fact] // io-download-save-dialog parityCheck: cancel produces no file and no error
    public void Cancelling_save_as_writes_nothing_and_stays_dirty()
    {
        var rig = new Rig(Dirty(out _));
        Assert.False(rig.Service.Save());
        Assert.Single(rig.Files.SaveProjectCalls);
        Assert.Empty(Directory.GetFiles(_files.Dir));
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Status.Shown);
        Assert.Empty(rig.Settings.FolderSaves);
    }

    [Fact] // Review Focus 2: a locked target. The write is atomic: the old file survives, no temp is left, the error is named, still dirty
    public void A_failed_write_keeps_the_existing_file_and_stays_dirty()
    {
        var rig = new Rig(Dirty(out _));
        string path = _files.Write("locked.r15proj", [1, 2, 3]);
        rig.Files.SaveProjectResult = path;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(rig.Service.Save());
        Assert.StartsWith("Couldn't save locked.r15proj: ", rig.Status.Current);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_files.Dir));
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Settings.FolderSaves);
    }

    [Fact] // M3b carry-forward: Save raises Changing before its snapshot, so an open wheel burst closes and its change is saved as one step
    public void Save_closes_a_pending_gesture_before_it_reads_the_document()
    {
        var rig = new Rig();
        IDisposable? burst = rig.Session.BeginGesture();
        Assert.True(rig.Session.Apply(d => d with { BgOpacity = 0.5 }));
        Assert.Equal(0, rig.Session.UndoCount);
        rig.Session.Changing += (_, _) => { var b = burst; burst = null; b?.Dispose(); };   // what CanvasInteraction does for a burst
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, "w.r15proj");

        Assert.True(rig.Service.Save());

        Assert.False(rig.Session.InGesture);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(0.5, ProjectIO.Read(File.ReadAllText(rig.Files.SaveProjectResult)).Document.BgOpacity);
        Assert.False(rig.Session.IsDirty);
    }

    // ---- Open (loadProject, editor.js:2593-2680) ----

    [Fact] // project-load-flow, project-load-history-interplay (fix): a 4.1 file opens all at once, history cleared, clean, nothing selected
    public async Task Open_swaps_in_a_4_1_file_clears_history_and_reads_clean()
    {
        var rig = new Rig(Dirty(out var old));
        rig.Session.TemplateTitle = "Hoodie";
        string path = Fixture("proj-shirt-rich");
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        var busy = new List<bool>();
        rig.Service.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProjectFileService.IsBusy)) busy.Add(rig.Service.IsBusy); };

        Assert.True(await rig.Service.OpenAsync(path));

        var expected = ProjectIO.Read(File.ReadAllText(path));
        Assert.Equal(ProjectIO.Write(expected.Document, Noon), ProjectIO.Write(rig.Session.Document, Noon));
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, rig.Session.Document.Layers.Select(l => l.Name));
        Assert.Equal(expected.NextId, rig.Session.NextId);
        Assert.Equal(rig.Session.Document.Layers[^1].Id, rig.Session.SelectedLayerId);   // T1-R2
        Assert.Equal((0, 0), (rig.Session.UndoCount, rig.Session.RedoCount));
        Assert.False(rig.Session.Undo());                                 // project-load-history-interplay parityCheck: Ctrl+Z after Open does nothing
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        Assert.Equal("Hoodie", rig.Session.TemplateTitle);                // R1
        Assert.Equal(new[] { true, false }, busy);                        // R4: busy only while the read runs
        Assert.Equal((1, (string?)null), (rig.Status.Clears, rig.Status.Current));
        Assert.Equal([(SettingsFolder.Project, _files.Dir)], rig.Settings.FolderSaves);
        Assert.Equal(new[] { old }, rig.AllReleased);                     // R11: the outgoing document's image
    }

    [Fact] // project-load-no-imgsrc-hang (fix): records without imgSrc are named, and the document still swaps and refreshes
    public async Task A_file_whose_layers_all_lack_images_opens_empty_and_names_them()
    {
        var rig = new Rig();
        string path = Text("bare.r15proj", """{ "mode": "Pants", "layers": [ { "id": 1, "name": "A" }, { "id": 2, "name": "B" } ] }""");
        Assert.True(await rig.Service.OpenAsync(path));
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal(TemplateMode.Pants, rig.Session.Document.Mode);
        Assert.Equal("Opened bare.r15proj. Couldn't load: A, B.", rig.Status.Current);
    }

    [Fact] // project-load-error-path-bugs, project-load-layers (fix): a corrupt last image keeps the file order and ids, and is named
    public async Task Dropped_layers_are_named_on_the_status_strip_and_the_rest_keep_file_order()
    {
        var rig = new Rig();
        using var red = Img(SKColors.Red);
        string json = $$"""
            { "layers": [
              { "id": 5, "name": "Good", "imgSrc": "{{Url(red)}}" },
              { "id": 2, "name": "Also good", "imgSrc": "{{Url(red)}}" },
              { "id": 9, "name": "Broken", "imgSrc": "data:image/png;base64,AAAA" } ] }
            """;
        string path = Text("mixed.r15proj", json);
        Assert.True(await rig.Service.OpenAsync(path));
        Assert.Equal(new[] { 5, 2 }, rig.Session.Document.Layers.Select(l => l.Id));
        Assert.Equal(6, rig.Session.NextId);
        Assert.Equal("Opened mixed.r15proj. Couldn't load: Broken.", rig.Status.Current);
    }

    [Fact] // contract (R12): out-of-range stored values load raw, as 4.1 kept them; nothing is normalized
    public async Task Out_of_range_values_load_raw()
    {
        var rig = new Rig();
        using var red = Img(SKColors.Red);
        string path = Text("wild.r15proj", $$"""
            { "layers": [ { "id": 1, "imgSrc": "{{Url(red)}}", "rotation": 720, "scale": 900, "opacity": 3,
                            "brightness": 999, "contrast": -50, "saturate": 1000, "hueRotate": -720, "rShift": 400 } ] }
            """);
        Assert.True(await rig.Service.OpenAsync(path));
        var l = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal((720.0, 900.0, 3.0, 999.0, -50.0, 1000.0, -720.0, 400), (l.Rotation, l.Scale, l.Opacity, l.Brightness, l.Contrast, l.Saturate, l.HueRotate, l.RShift));
    }

    public static TheoryData<string, string> BadFiles => new()
    {
        { "[]", "Failed to load project. the file is a JSON array, not a project object" },
        { """{ "mode": "Hoodie" }""", "Failed to load project. unknown mode \"Hoodie\" (expected \"Shirt\" or \"Pants\")" },
        { """{ "bgImageSrc": "http://example.com/a.png" }""", "Failed to load project. bgImageSrc must be a data: URL, not \"http://example.com/a.png\"" },
        { """{ "layers": [ { "id": 0 } ] }""", "Failed to load project. layers[0].id must be a positive whole number" },
    };

    [Theory, MemberData(nameof(BadFiles))] // Review Focus 1; project-load-untrusted-src, project-load-flow: 4.1's alert text, all or nothing, no prompt
    public async Task A_file_that_cannot_open_alerts_4_1s_text_and_changes_nothing(string json, string alert)
    {
        var rig = new Rig(Dirty(out _));
        var before = rig.Session.Document;
        string path = Text("bad.r15proj", json);

        Assert.False(await rig.Service.OpenAsync(path));

        Assert.Equal(new[] { alert }, rig.Dialogs.Alerts);
        Assert.Empty(rig.Dialogs.SaveChanges);                            // R4: nobody is asked to save for a file that cannot open
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Null(rig.Session.CurrentFile);
        Assert.True(rig.Session.IsDirty);
        Assert.False(rig.Service.IsBusy);
        Assert.Empty(rig.Released);
        Assert.Empty(rig.Settings.FolderSaves);
    }

    [Fact] // Review Focus 1: text that is not JSON, and a file that is gone, alert with the reader's own message
    public async Task Not_json_and_a_missing_file_alert_too()
    {
        var rig = new Rig();
        Assert.False(await rig.Service.OpenAsync(Text("trunc.r15proj", "{ \"layers\": [")));
        Assert.StartsWith("Failed to load project. the file is not JSON: ", Assert.Single(rig.Dialogs.Alerts));
        string gone = Path.Combine(_files.Dir, "gone.r15proj");
        Assert.False(await rig.Service.OpenAsync(gone));
        Assert.Equal($"Failed to load project. Could not find file '{gone}'.", rig.Dialogs.Alerts[1]);
        Assert.Null(rig.Session.CurrentFile);
    }

    [Fact] // critic M1, T1-R14 (project-load-untrusted-src): a file over the size limit is refused before it is read, with 4.1's alert prefix, and nothing changes
    public async Task A_file_over_the_size_limit_alerts_and_changes_nothing()
    {
        Assert.Equal(512L * 1024 * 1024, ProjectFileService.MaxProjectBytes);
        var rig = new Rig(Dirty(out _), read: (p, c) => Task.Run(() => ProjectFileService.ReadFile(p, maxBytes: 1024 * 1024), c));
        var before = rig.Session.Document;
        string path = Text("big.r15proj", "{ \"layers\": [] }" + new string(' ', 1536 * 1024 - 16));   // 1.5 MiB of valid JSON

        Assert.False(await rig.Service.OpenAsync(path));

        Assert.Equal(new[] { "Failed to load project. the file is 1.5 MB; the limit is 1 MB" }, rig.Dialogs.Alerts);
        Assert.Empty(rig.Dialogs.SaveChanges);
        Assert.Same(before, rig.Session.Document);
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Released);
    }

    [Fact] // R4: the read is awaited off the caller, the service is busy until it lands, and a second open is refused meanwhile
    public async Task While_the_read_runs_the_service_is_busy_and_refuses_a_second_open()
    {
        var pending = new TaskCompletionSource<ProjectLoad>();
        var rig = new Rig(read: (_, _) => pending.Task);
        var open = rig.Service.OpenAsync("C:\\p\\slow.r15proj");
        Assert.False(open.IsCompleted);
        Assert.True(rig.Service.IsBusy);
        Assert.False(await rig.Service.OpenAsync("C:\\p\\other.r15proj"));
        rig.Files.OpenProjectResult = "C:\\p\\third.r15proj";
        Assert.False(await rig.Service.OpenAsync());
        Assert.Empty(rig.Files.OpenProjectDirs);                          // no dialog while busy

        pending.SetResult(new ProjectLoad(new Document { BgColor = "#010203" }, 1, []));
        Assert.True(await open);
        Assert.False(rig.Service.IsBusy);
        Assert.Equal("#010203", rig.Session.Document.BgColor);
    }

    [Fact] // R4/R11: closing the window cancels a pending read; its result is released, never applied
    public async Task A_cancelled_read_is_released_and_never_applied()
    {
        var pending = new TaskCompletionSource<ProjectLoad>();
        var rig = new Rig(read: (_, _) => pending.Task);
        var open = rig.Service.OpenAsync("C:\\p\\slow.r15proj");
        rig.Service.CancelPendingOpen();
        var img = Img(SKColors.Blue);
        pending.SetResult(new ProjectLoad(new Document { Layers = [new Layer { Id = 1, Image = img }] }, 2, []));

        Assert.False(await open);
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Equal(new[] { img }, rig.AllReleased);
        Assert.Empty(rig.Dialogs.Alerts);
        rig.Service.CancelPendingOpen();                                  // nothing pending: a no-op
    }

    [Fact] // R13: the guard asks only after a good read; Cancel keeps everything and releases what was loaded
    public async Task Cancel_in_the_guard_keeps_the_document_and_releases_the_load()
    {
        var rig = new Rig(Dirty(out _));
        var before = rig.Session.Document;
        string path = Fixture("proj-shirt-rich");

        Assert.False(await rig.Service.OpenAsync(path));

        Assert.Equal(new[] { "Untitled" }, rig.Dialogs.SaveChanges);
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(4, rig.AllReleased.Count());                         // the four decoded fixture layers
        Assert.DoesNotContain(rig.AllReleased, i => before.Layers.Any(l => ReferenceEquals(l.Image, i)));
    }

    [Fact] // R13: Save in the guard saves first; a cancelled Save As cancels the whole open
    public async Task Save_in_the_guard_saves_first_and_a_cancelled_save_as_cancels_the_open()
    {
        var rig = new Rig(Dirty(out _));
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.Save;
        string path = Fixture("proj-shirt-rich");

        Assert.False(await rig.Service.OpenAsync(path));                  // Save As cancelled
        Assert.Single(rig.Files.SaveProjectCalls);
        Assert.Null(rig.Session.CurrentFile);
        Assert.True(rig.Session.IsDirty);

        string saved = Path.Combine(_files.Dir, "work.r15proj");
        rig.Files.SaveProjectResult = saved;
        Assert.True(await rig.Service.OpenAsync(path));
        Assert.Single(ProjectIO.Read(File.ReadAllText(saved)).Document.Layers);   // the work was saved before the swap
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.Equal(new[] { "Untitled", "Untitled" }, rig.Dialogs.SaveChanges);
    }

    [Fact] // shell-no-unsaved-changes-guard: never on a pristine, just-saved, just-opened or undone-to-clean document
    public async Task The_guard_never_asks_for_a_clean_document()
    {
        var rig = new Rig();
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // pristine
        Assert.True(rig.Session.Apply(d => d with { ShowGrid = true }));
        Assert.True(rig.Session.Undo());
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // undone back to clean (R2)
        Assert.True(await rig.Service.OpenAsync(Fixture("proj-pants-legacy")));
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // just opened
        Assert.True(rig.Session.Apply(d => d with { ShowGrid = !d.ShowGrid }));
        Assert.True(rig.Service.Save());                                  // to the current file
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // just saved
        Assert.Empty(rig.Dialogs.SaveChanges);

        Assert.True(rig.Session.Apply(d => d with { ShowGrid = !d.ShowGrid }));
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        Assert.True(rig.Service.ConfirmDiscardChanges());
        Assert.Equal(new[] { "proj-pants-legacy.r15proj" }, rig.Dialogs.SaveChanges);   // named by its file
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        Assert.False(rig.Service.ConfirmDiscardChanges());
    }

    [Fact] // the Open dialog starts in the remembered folder; a cancel does nothing
    public async Task Open_with_no_pick_does_nothing()
    {
        var rig = new Rig(Dirty(out _));
        rig.Settings.Folders[SettingsFolder.Project] = _files.Dir;
        Assert.False(await rig.Service.OpenAsync());
        Assert.Equal(new[] { (string?)_files.Dir }, rig.Files.OpenProjectDirs);
        Assert.Empty(rig.Dialogs.SaveChanges);
        Assert.True(rig.Session.IsDirty);
    }

    [Fact] // file-project-format parityCheck: a round trip save -> load is stable
    public async Task A_round_trip_through_save_and_open_is_stable()
    {
        var rig = new Rig();
        Assert.True(await rig.Service.OpenAsync(Fixture("proj-shirt-rich")));
        string again = Path.Combine(_files.Dir, "again.r15proj");
        rig.Files.SaveProjectResult = again;
        Assert.True(rig.Service.SaveAs());
        string first = ProjectIO.Write(rig.Session.Document, Noon);

        Assert.True(await rig.Service.OpenAsync(again));

        Assert.Equal(first, ProjectIO.Write(rig.Session.Document, Noon));
        Assert.Equal(again, rig.Session.CurrentFile);
    }

    [Fact] // R11's default: the outgoing images are freed at ApplicationIdle, after the new document has rendered; kept ones are not
    public void The_default_release_frees_outgoing_images_at_application_idle() => StaThread.Run(() =>
    {
        var old = Img(SKColors.Red);
        var shared = Img(SKColors.Green);
        var session = new EditorSession(new Document { Layers = [new Layer { Id = 1, Image = old }, new Layer { Id = 2, Image = shared }] });
        var load = new ProjectLoad(new Document { Layers = [new Layer { Id = 1, Image = shared }] }, 2, []);
        var service = new ProjectFileService(session, new FakeFileDialogs(), new FakeDialogService(), new FakeStatus(), new FakeSettingsStore(),
            read: (_, _) => Task.FromResult(load));

        Assert.True(service.OpenAsync("C:\\p\\x.r15proj").GetAwaiter().GetResult());
        Assert.NotEqual(IntPtr.Zero, old.Handle);                         // not yet: the frame for the new document comes first
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        Assert.Equal(IntPtr.Zero, old.Handle);
        Assert.NotEqual(IntPtr.Zero, shared.Handle);
    });

    // ---- names and strings ----

    [Fact] // dialogs-inventory, io-alert-inventory: 4.1's alert strings verbatim (editor.js:2344, :2364, :2385, :2676)
    public void Dialog_text_is_4_1s_verbatim()
    {
        string js = File.ReadAllText(Path.Combine(Root, "editor.js"));
        Assert.Contains("alert('Built-in pack \"' + packName + '\" not found. Reinstall the app to restore it.');", js);
        Assert.Contains("alert('Built-in pack \"' + packName + '\" is empty.');", js);
        Assert.Contains("alert('" + DialogText.NoImagesInFolder + "');", js);
        Assert.Contains("alert('Failed to load project. ' + err.message);", js);
        Assert.Equal("Built-in pack \"drips\" not found. Reinstall the app to restore it.", DialogText.PackNotFound("drips"));
        Assert.Equal("Built-in pack \"word_art\" is empty.", DialogText.PackEmpty("word_art"));
        Assert.Equal("Failed to load project. boom", DialogText.LoadFailed("boom"));
        Assert.Equal("Do you want to save changes to a.r15proj?", DialogText.SaveChanges("a.r15proj"));
        Assert.Equal("Untitled", DialogText.Untitled);
    }

    [Fact] // interfaces §2.2: .r15proj first, JSON keeps 4.1's reach (accept=".json,.r15proj", editor.html:108); Save writes .r15proj only
    public void The_dialog_filters_keep_4_1s_reach()
    {
        Assert.Contains("<input type=\"file\" id=\"projectFileInput\" accept=\".json,.r15proj\" hidden>", File.ReadAllText(Path.Combine(Root, "editor.html")));
        Assert.Equal("RBX15 project (*.r15proj)|*.r15proj|JSON project (*.json)|*.json", Win32FileDialogs.ProjectOpenFilter);
        Assert.Equal("RBX15 project (*.r15proj)|*.r15proj", Win32FileDialogs.ProjectSaveFilter);
    }

    [Theory]
    [InlineData(null, "Untitled")]
    [InlineData("", "Untitled")]
    [InlineData("C:\\work\\my shirt.r15proj", "my shirt.r15proj")]
    public void Document_name_is_the_file_name_or_untitled(string? file, string expected) =>
        Assert.Equal(expected, ProjectFileService.DocumentName(file));
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails in RBX15.App.Tests with `CS0246: The type or namespace name 'ProjectFileService' could not be found` (measured).

- [ ] **Step 9: ProjectFileService**

Create `src/RBX15.App/Services/ProjectFileService.cs` (R1, R4, R11, R13; T1-R6 to T1-R8, T1-R12, T1-R14, T1-R15):

```csharp
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>
/// Save work, Save As and Open over the session (spec §3 "Saving": a current file, Ctrl+S, Ctrl+Shift+S, native
/// dialogs, last folder remembered), and the unsaved-changes guard (Este's ruling 2026-10-16; interfaces R13).
/// <list type="bullet">
/// <item><b>Save</b> (saveProject, editor.js:2550-2591): ProjectIO.Write with no title (R1), written atomically
///   (a temp file in the same folder, then a replace or move over the target), then the session is marked saved and the
///   status strip names the file. 4.1 wrote through an anchor download with no feedback (io-download-save-dialog).</item>
/// <item><b>Open</b> (loadProject, editor.js:2593-2680): the read, parse and decode run off the UI thread (R4),
///   all-or-nothing; a failure is 4.1's alert text with v5's message (editor.js:2676) and changes nothing; the guard
///   asks only after a good read; dropped layers are named on the status strip (project-load-error-path-bugs).</item>
/// </list>
/// UI thread, except the read.
/// </summary>
public sealed class ProjectFileService : ObservableObject
{
    readonly EditorSession _session;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ISettingsStore _settings;
    readonly TimeProvider _clock;
    readonly Func<string, CancellationToken, Task<ProjectLoad>> _read;
    readonly Action<IReadOnlyCollection<SKImage>> _release;
    CancellationTokenSource? _pending;
    bool _isBusy;

    /// <param name="clock">savedAt's clock; TimeProvider.System by default.</param>
    /// <param name="read">The off-thread read; by default <see cref="ReadFile"/> on the thread pool.</param>
    /// <param name="release">Frees images nothing references any more; by default <see cref="ReleaseOnIdle"/> (R11).</param>
    public ProjectFileService(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status, ISettingsStore settings,
        TimeProvider? clock = null,
        Func<string, CancellationToken, Task<ProjectLoad>>? read = null,
        Action<IReadOnlyCollection<SKImage>>? release = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(settings);
        (_session, _files, _dialogs, _status, _settings) = (session, files, dialogs, status, settings);
        _clock = clock ?? TimeProvider.System;
        _read = read ?? ((path, cancel) => Task.Run(() => ReadFile(path), cancel));
        var dispatcher = Dispatcher.CurrentDispatcher;
        _release = release ?? (images => ReleaseOnIdle(dispatcher, images));
    }

    /// <summary>True while <see cref="OpenAsync(string)"/> awaits its read (R4). The window blocks input and every file command waits.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>The name the prompt and the window title use: the file name, or <see cref="DialogText.Untitled"/>.</summary>
    public static string DocumentName(string? currentFile) =>
        string.IsNullOrEmpty(currentFile) ? DialogText.Untitled : Path.GetFileName(currentFile);

    /// <summary>
    /// The largest project file Open reads, in bytes (512 MiB). ProjectIO caps each image (100 MiB encoded, 50 MP) but
    /// not the file, so a hostile or corrupt file could otherwise be read whole into memory and crash the app with the
    /// user's unsaved work on screen (T1-R14). New v5 surface; provisional.
    /// </summary>
    internal const long MaxProjectBytes = 512L * 1024 * 1024;

    /// <summary>
    /// The default read: refuses a file over <paramref name="maxBytes"/> before reading it, then the whole file as
    /// UTF-8 and <see cref="ProjectIO.Read"/> (parse, validate and capped decode).
    /// </summary>
    internal static ProjectLoad ReadFile(string path, long maxBytes = MaxProjectBytes)
    {
        long length = new FileInfo(path).Length;
        if (length > maxBytes)
            throw new ProjectFormatException(string.Create(CultureInfo.InvariantCulture, $"the file is {length / 1048576.0:0.#} MB; the limit is {maxBytes / 1048576.0:0.#} MB"));
        return ProjectIO.Read(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>Save work (btnSaveProject, editor.js:2682): to the current file, or Save As when there is none. True iff written.</summary>
    public bool Save() => _session.CurrentFile is { } path ? WriteTo(path) : SaveAs();

    /// <summary>
    /// Save As (Ctrl+Shift+S; no button, interfaces §2.1). Suggests the current file's name, else
    /// <see cref="ExportNaming.ProjectFileName"/> of the template name (editor.js:2586-2587; project-save-filename, keep),
    /// starting in the current file's folder, else the remembered one. False on cancel or a failed write.
    /// </summary>
    public bool SaveAs()
    {
        string suggested = _session.CurrentFile is { } current ? Path.GetFileName(current) : ExportNaming.ProjectFileName(_session.TemplateTitle);
        var path = _files.SaveProject(suggested, InitialDirectory());
        return path is not null && WriteTo(path);
    }

    /// <summary>Open (btnLoadProject, editor.js:2683-2687): the Open dialog, then <see cref="OpenAsync(string)"/>. False on cancel, a failure or a declined guard.</summary>
    public async Task<bool> OpenAsync()
    {
        if (IsBusy) return false;
        var path = _files.OpenProject(InitialDirectory());
        return path is not null && await OpenAsync(path);
    }

    /// <summary>
    /// Opens <paramref name="path"/> (R4 order): the read off the UI thread; a failure alerts
    /// <see cref="DialogText.LoadFailed"/> and changes nothing; then the unsaved-changes guard, so nobody is asked to
    /// save for a file that cannot open; a declined guard releases the loaded images. On success the session swaps
    /// (<see cref="EditorSession.Open"/>), the outgoing images are released, the folder is remembered, and the status
    /// strip names the dropped layers or clears.
    /// </summary>
    public async Task<bool> OpenAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (IsBusy) return false;
        using var cancel = new CancellationTokenSource();
        _pending = cancel;
        IsBusy = true;
        ProjectLoad? load = null;
        Exception? failure = null;
        try
        {
            load = await _read(path, cancel.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
        }
        catch (Exception e) when (e is ProjectFormatException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            failure = e;
        }
        finally
        {
            _pending = null;
            IsBusy = false;
        }

        if (cancel.IsCancellationRequested)
        {
            if (load is not null) _release(ImagesOf(load.Document));
            return false;
        }
        if (failure is not null)
        {
            _dialogs.Alert(DialogText.LoadFailed(failure.Message));
            return false;
        }
        if (!ConfirmDiscardChanges())
        {
            _release(ImagesOf(load!.Document));
            return false;
        }
        _release(_session.Open(load!, path));
        RememberFolder(path);
        string name = Path.GetFileName(path);
        if (load!.DroppedLayers.IsEmpty) _status.Clear();
        else _status.Show($"Opened {name}. Couldn't load: {string.Join(", ", load.DroppedLayers)}.");
        return true;
    }

    /// <summary>
    /// The unsaved-changes guard (R13): clean goes on with no prompt; Save runs <see cref="Save"/> and goes on only if it
    /// wrote (a cancelled Save As or a failed write cancels the whole action); Don't save goes on; Cancel stops.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_session.IsDirty) return true;
        return _dialogs.AskSaveChanges(DocumentName(_session.CurrentFile)) switch
        {
            SaveChoice.Save => Save(),
            SaveChoice.DontSave => true,
            _ => false,
        };
    }

    /// <summary>Cancels a pending read; its result is released, not applied. The window calls it when it closes.</summary>
    public void CancelPendingOpen() => _pending?.Cancel();

    string? InitialDirectory() =>
        _session.CurrentFile is { } current ? Path.GetDirectoryName(current) : _settings.LoadFolder(SettingsFolder.Project);

    void RememberFolder(string path)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir) _settings.SaveFolder(SettingsFolder.Project, dir);
    }

    bool WriteTo(string path)
    {
        _session.CommitPendingEdits();
        var doc = _session.Document;
        string name = Path.GetFileName(path);
        string temp = path + "." + RandomNumberGenerator.GetHexString(8, lowercase: true) + ".tmp";
        try
        {
            string json = ProjectIO.Write(doc, _clock.GetUtcNow(), title: null);
            File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            // Replace keeps an existing target's attributes, ACL and alternate streams; Move only when there is none (T1-R15).
            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
            else File.Move(temp, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            try { File.Delete(temp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
            _status.Show($"Couldn't save {name}: {e.Message}");
            return false;
        }
        _session.MarkSaved(path, doc);
        RememberFolder(path);
        _status.Show($"Saved {name}.");
        return true;
    }

    static IReadOnlyCollection<SKImage> ImagesOf(Document doc)
    {
        var images = new List<SKImage>(doc.Layers.Count + 1);
        if (doc.BgImage is { } bg) images.Add(bg);
        images.AddRange(doc.Layers.Select(l => l.Image));
        return images;
    }

    /// <summary>
    /// R11: frees the images at ApplicationIdle, after the host has rendered the new document and every view-model has
    /// rebuilt, so nothing draws a disposed image.
    /// </summary>
    static void ReleaseOnIdle(Dispatcher dispatcher, IReadOnlyCollection<SKImage> images)
    {
        if (images.Count == 0) return;
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            foreach (var image in images) Images.Release(image);
        });
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run, three times: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ProjectFileServiceTests"`
Expected each time: `Passed!  - Failed:     0, Passed:    31, Skipped:     0, Total:    31` for `RBX15.App.Tests.dll`. The read, cancel and busy facts await real tasks; a flaky one is a defect in `OpenAsync`, never a reason to add a delay.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `885`; Parity.Tests `35`; App.Tests `Passed: 697, Failed: 0` (644 + 53).

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Services/ProjectFileService.cs src/RBX15.App.Tests/ProjectFileServiceTests.cs
git commit -m "feat(app): ProjectFileService, atomic Save and Save As, an off-thread Open and the unsaved-changes guard"
```

- [ ] **Step 10: Write the failing shell tests (the File group, the chords, the title, the close guard)**

Ledger: `home-file-group`, `undo-buttons-shortcuts` (the chords' text-field guard), `shell-window-frame` (title), `shell-window-title` (launch unchanged), `shell-no-unsaved-changes-guard`, `shell-unsaved-work-loss`, `project-load-history-interplay`. Review Focus candidates 2 and 3.

Create `src/RBX15.App.Tests/WindowTitleTests.cs` (R3):

```csharp
using System.Windows;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The window title (interfaces R3): 4.1's product name while untitled and clean (main.js:8, editor.html:6; ledger
/// shell-window-title, keep), otherwise the Windows convention "[*]name - product" (ledger shell-window-frame, title part).
/// </summary>
[Collection("MainWindow")] // builds a MainWindow; see ShellLookTests
public class WindowTitleTests
{
    const string Product = "RBX15 Classic Shirt and Pants Maker";

    [Theory]
    [InlineData(null, false, Product)]
    [InlineData(null, true, "*Untitled - " + Product)]
    [InlineData("C:\\work\\shirt.r15proj", false, "shirt.r15proj - " + Product)]
    [InlineData("C:\\work\\shirt.r15proj", true, "*shirt.r15proj - " + Product)]
    public void The_title_formula(string? file, bool dirty, string expected) =>
        Assert.Equal(expected, MainViewModel.WindowTitle(file, dirty));

    [Fact] // the title follows an edit, an undo back to clean, a save and an open; selection alone never raises it
    public void The_title_follows_the_document_and_the_file()
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var raised = new List<string>();
        main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.Title)) raised.Add(main.Title); };

        Assert.Equal(Product, main.Title);
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Assert.True(session.Apply(d => d with { BgColor = "#000001" }));   // still dirty: no second raise
        Assert.True(session.Undo());
        Assert.True(session.Undo());
        session.MarkSaved("C:\\work\\a.r15proj", session.Document);
        session.Select(null);
        session.Open(new ProjectLoad(new Document(), 1, []), "C:\\work\\b.r15proj");

        Assert.Equal(new[]
        {
            "*Untitled - " + Product,
            Product,
            "a.r15proj - " + Product,
            "b.r15proj - " + Product,
        }, raised);
    }

    [Fact] // the window's caption is bound to it
    public void The_window_shows_the_title() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var window = new MainWindow(TestShell.Main(session), new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        Assert.Equal(Product, window.Title);
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Assert.Equal("*Untitled - " + Product, window.Title);
    });
}
```

In `src/RBX15.App.Tests/ShellTests.cs`, before `Ribbon_tabs_are_grouped_per_window` (T1-R13):

Replace:

```csharp

    [Fact] // final fix wave 6: each window's ribbon tabs are their own group; one window's tab never unchecks another's
```

with:

```csharp

    // ---- M4a Task 1: the three file chords and the close guard ----

    public static TheoryData<Key, ModifierKeys, string> FileChords => new()
    {
        { Key.S, ModifierKeys.Control, nameof(FileGroupViewModel.SaveCommand) },
        { Key.S, ModifierKeys.Control | ModifierKeys.Shift, nameof(FileGroupViewModel.SaveAsCommand) },
        { Key.O, ModifierKeys.Control, nameof(FileGroupViewModel.OpenCommand) },
    };

    [Theory, MemberData(nameof(FileChords))] // spec §4 "New: Ctrl+O, Ctrl+S, Ctrl+Shift+S"; each binding: its gesture, its inner command, the text-field guard
    public void A_file_chord_runs_its_file_group_command_unless_a_text_field_has_focus(Key key, ModifierKeys modifiers, string command) => StaThread.Run(() =>
    {
        var main = TestShell.Main(new EditorSession());
        var window = new MainWindow(main, new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var binding = Assert.Single(window.InputBindings.OfType<KeyBinding>(), b => b.Key == key && b.Modifiers == modifiers);
        var guard = Assert.IsType<TextFieldShortcutCommand>(binding.Command);
        var expected = typeof(FileGroupViewModel).GetProperty(command)!.GetValue(main.Home.File);
        Assert.Same(expected, guard.Inner);

        window.FocusedElement = () => new TextBox();
        Assert.False(guard.CanExecute(null));                       // the key stays the text field's
        window.FocusedElement = () => null;
        Assert.True(guard.CanExecute(null));
    });

    [Fact] // the window has exactly the four chords: Ctrl+Z (M3b) and the three file chords; the rest of the keyboard is M4b
    public void The_window_binds_only_ctrl_z_and_the_three_file_chords() => StaThread.Run(() =>
    {
        var window = new MainWindow(TestShell.Main(new EditorSession()), new FakeSettingsStore(), [Primary]);
        StaThread.Pump();
        var gestures = window.InputBindings.OfType<KeyBinding>().Select(b => (b.Key, b.Modifiers)).ToArray();
        Assert.Equal(new[]
        {
            (Key.Z, ModifierKeys.Control),
            (Key.S, ModifierKeys.Control),
            (Key.S, ModifierKeys.Control | ModifierKeys.Shift),
            (Key.O, ModifierKeys.Control),
        }, gestures);
        Assert.Equal(4, window.InputBindings.Count);
        Assert.All(window.InputBindings.OfType<KeyBinding>(), b => Assert.IsType<TextFieldShortcutCommand>(b.Command));
    });

    /// <summary>A main window over fakes the test can reach, closed without ever being shown.</summary>
    static (MainWindow Window, EditorSession Session, FakeDialogService Dialogs, FakeFileDialogs Files, FakeSettingsStore Store) ClosableShell()
    {
        var session = new EditorSession();
        var dialogs = new FakeDialogService();
        var files = new FakeFileDialogs();
        var store = new FakeSettingsStore();
        var main = Composition.Build(session, files, dialogs, new FakeColorPicker(), store);
        var window = new MainWindow(main, store, [Primary]);
        StaThread.Pump();
        return (window, session, dialogs, files, store);
    }

    [Fact] // shell-no-unsaved-changes-guard parityCheck: closing with no edits shows no prompt and closes
    public void Closing_a_clean_document_shows_no_prompt() => StaThread.Run(() =>
    {
        var (window, _, dialogs, _, store) = ClosableShell();
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();
        Assert.True(closed);
        Assert.Empty(dialogs.SaveChanges);
        Assert.Single(store.Saves);                                  // the placement is saved on a real close
    });

    [Theory] // R13: closing dirty asks; Cancel, and Save with a cancelled Save As, keep the window open; Don't save closes
    [InlineData(SaveChoice.Cancel, false)]
    [InlineData(SaveChoice.DontSave, true)]
    [InlineData(SaveChoice.Save, false)]
    public void Closing_dirty_work_asks_save_dont_save_or_cancel(SaveChoice answer, bool closes) => StaThread.Run(() =>
    {
        var (window, session, dialogs, files, store) = ClosableShell();
        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        dialogs.SaveChoiceAnswer = answer;
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();
        Assert.Equal(new[] { "Untitled" }, dialogs.SaveChanges);
        Assert.Equal(closes, closed);
        Assert.Equal(closes ? 1 : 0, store.Saves.Count);             // no placement save when the close was cancelled
        Assert.Equal(answer == SaveChoice.Save ? 1 : 0, files.SaveProjectCalls.Count);
        if (!closes) window.Close();                                 // the guard asks again on the next attempt
        if (!closes) Assert.Equal(2, dialogs.SaveChanges.Count);
    });

    [Fact] // final fix wave 6: each window's ribbon tabs are their own group; one window's tab never unchecks another's
```

In `src/RBX15.App.Tests/FileGroupTests.cs`, the rig (M4a added the last constructor argument; Addition 8):

Replace:

```csharp
        var status = new FakeStatus();
        return new Rig(session, files, dialogs, status, new FileGroupViewModel(session, files, dialogs, status, new ImportService(session, status)));
    }
```

with:

```csharp
        var status = new FakeStatus();
        return new Rig(session, files, dialogs, status, HomeTestKit.FileGroup(session, files, dialogs, status));
    }
```

and before `Add_image_imports_the_picked_files`:

Replace:

```csharp

    [Fact] // project-load-image-layer: Add image imports what the picker returns, one step
```

with:

```csharp

    // ---- M4a Task 1: Save work, Save As and Open ----

    [Fact] // home-file-group: Save work runs Save As when untitled, then saves to the file; Save As always asks
    public void Save_work_and_save_as_go_through_the_project_service()
    {
        using var files = new TestFiles();
        var r = Make(new Document { BgImage = Img() });
        r.Files.SaveProjectResult = Path.Combine(files.Dir, "w.r15proj");
        r.Vm.SaveCommand.Execute(null);
        Assert.Equal(r.Files.SaveProjectResult, r.Session.CurrentFile);
        r.Vm.SaveCommand.Execute(null);
        Assert.Single(r.Files.SaveProjectCalls);                     // the second Save work wrote to the current file
        r.Vm.SaveAsCommand.Execute(null);
        Assert.Equal(2, r.Files.SaveProjectCalls.Count);
        Assert.Equal("Saved w.r15proj.", r.Status.Current);
    }

    [Fact] // home-file-group: Open runs the Open dialog, then opens the pick
    public async Task Open_opens_the_picked_project()
    {
        using var files = new TestFiles();
        var r = Make();
        string path = files.Write("p.r15proj", System.Text.Encoding.UTF8.GetBytes("""{ "mode": "Pants" }"""));
        r.Files.OpenProjectResult = path;
        await r.Vm.OpenCommand.ExecuteAsync(null);
        Assert.Equal(TemplateMode.Pants, r.Session.Document.Mode);
        Assert.Equal(path, r.Session.CurrentFile);
    }

    [Fact] // R4: while an Open reads, Save work, Save As and Open cannot run, and each says so through CanExecuteChanged
    public async Task The_file_commands_wait_while_an_open_reads()
    {
        var session = new EditorSession();
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var status = new FakeStatus();
        var pending = new TaskCompletionSource<ProjectLoad>();
        var service = new ProjectFileService(session, files, dialogs, status, new FakeSettingsStore(), read: (_, _) => pending.Task, release: _ => { });
        var vm = new FileGroupViewModel(session, files, dialogs, status, new ImportService(session, status), service);
        int changes = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => changes++;
        vm.SaveAsCommand.CanExecuteChanged += (_, _) => changes++;
        vm.OpenCommand.CanExecuteChanged += (_, _) => changes++;
        Assert.True(vm.SaveCommand.CanExecute(null) && vm.SaveAsCommand.CanExecute(null) && vm.OpenCommand.CanExecute(null));

        var open = service.OpenAsync("C:\\p\\slow.r15proj");
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.SaveAsCommand.CanExecute(null));
        Assert.False(vm.OpenCommand.CanExecute(null));
        Assert.Equal(3, changes);

        pending.SetResult(new ProjectLoad(new Document(), 1, []));
        Assert.True(await open);
        Assert.True(vm.SaveCommand.CanExecute(null) && vm.SaveAsCommand.CanExecute(null) && vm.OpenCommand.CanExecute(null));
        Assert.Equal(6, changes);
    }

    [Fact] // project-load-image-layer: Add image imports what the picker returns, one step
```

In `src/RBX15.App.Tests/Task4ViewTests.cs`, M3a's "Add image and Download template only" fact becomes 4.1's four buttons (count unchanged):

Replace:

```csharp

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
```

with:

```csharp

    [Fact] // home-file-group (M4a): 4.1's four buttons in its order, with its title= tooltips (editor.html:88-90, :93)
    public void The_file_group_shows_4_1s_four_buttons_in_order() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var status = new FakeStatus();
        var group = new FileGroup { DataContext = HomeTestKit.FileGroup(session, new FakeFileDialogs(), new FakeDialogService(), status) };
        Layout(group);
        var buttons = Descendants<Button>(group).ToArray();
        Assert.Equal(new[] { "Save work", "Open", "Add image", "Download template" }, buttons.Select(b => b.Content as string));
        Assert.Equal(new[]
        {
            "Save your workspace. Reopen and keep editing.",
            "Open a saved workspace.",
            "Add an image file as a layer.",
            "Export the finished 585x559 PNG. Ready to upload to Roblox.",
        }, buttons.Select(b => b.ToolTip as string));
        Assert.Equal(new[] { "SaveButton", "OpenButton" }, buttons.Take(2).Select(b => b.Name));
        var vm = (FileGroupViewModel)group.DataContext;
        Assert.Same(vm.SaveCommand, buttons[0].Command);
        Assert.Same(vm.OpenCommand, buttons[1].Command);
    });
```

In `src/RBX15.App.Tests/HomeRibbonViewTests.cs`, Save work and Open are no longer hidden (count unchanged):

Replace:

```csharp

    [Fact] // the mode button, settings and edit bindings; M3b shows Tools and Brush; Placement, Save work and Open stay hidden (M4)
    public void Mode_settings_and_edit_bind_and_hidden_groups_are_absent() => StaThread.Run(() =>
```

with:

```csharp

    [Fact] // the mode button, settings and edit bindings; M3b shows Tools and Brush; Placement and Keys stay hidden (M4b); Save work and Open show (M4a)
    public void Mode_settings_and_edit_bind_and_hidden_groups_are_absent() => StaThread.Run(() =>
```

Replace:

```csharp
        Assert.Contains(buttons, b => b.Content is "Draw");
        Assert.DoesNotContain(buttons, b => b.Content is "Fit Front" or "Save work" or "Open" or "Keys: ON");
        Assert.Contains("Set a style, then click a region to paint it. Right-click clears.", texts);
```

with:

```csharp
        Assert.Contains(buttons, b => b.Content is "Draw");
        Assert.DoesNotContain(buttons, b => b.Content is "Fit Front" or "Keys: ON");
        Assert.Contains(buttons, b => b.Content is "Save work");
        Assert.Contains(buttons, b => b.Content is "Open");
        Assert.Contains("Set a style, then click a region to paint it. Right-click clears.", texts);
```

In `src/RBX15.App.Tests/HomeTestKit.cs` (Addition 8):

Replace:

```csharp
            Brush = new BrushViewModel(picker ?? new FakeColorPicker()),
            File = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)),
        };
    }

```

with:

```csharp
            Brush = new BrushViewModel(picker ?? new FakeColorPicker()),
            File = FileGroup(session, new FakeFileDialogs(), new FakeDialogService(), status),
        };
    }

    /// <summary>A File group over fakes, with its own project-file service (M4a Task 1 added the last constructor argument).</summary>
    public static FileGroupViewModel FileGroup(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status) =>
        new(session, files, dialogs, status, new ImportService(session, status), new ProjectFileService(session, files, dialogs, status, new FakeSettingsStore()));

```

In `src/RBX15.App.Tests/ShellLookTests.cs` (count unchanged):

Replace:

```csharp
        var status = new FakeStatus();
        var file = new FileGroup { DataContext = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)) };
        StaThread.Pump();
```

with:

```csharp
        var status = new FakeStatus();
        var file = new FileGroup { DataContext = HomeTestKit.FileGroup(session, new FakeFileDialogs(), new FakeDialogService(), status) };
        StaThread.Pump();
```

In `src/RBX15.App.Tests/ShellWiringTests.cs` (the new `Build` signature; count unchanged):

Replace:

```csharp
        });
        var main = Composition.Build(session, () => null);
        void Click(double x, double y)
```

with:

```csharp
        });
        var main = Composition.Build(session, () => null, new Fakes.FakeSettingsStore());
        void Click(double x, double y)
```

Replace `src/RBX15.App.Tests/TestShell.cs` with (the CS0028 pragma stays):

```csharp
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The tests' one MainViewModel builder: the composition root of App (Composition.Build) with fakes in
/// place of the native services. A task that adds a required member to MainViewModel adds it in Composition only; a new native
/// service adds a parameter to the injectable overload and a fake here.
/// </summary>
internal static class TestShell
{
#pragma warning disable CS0028 // The name Main is the interface sheet's; the test SDK's generated Program.Main is the entry point.
    public static MainViewModel Main(EditorSession session)
#pragma warning restore CS0028
    {
        return Composition.Build(session, new FakeFileDialogs(), new FakeDialogService(), new FakeColorPicker(), new FakeSettingsStore());
    }
}
```

In `src/RBX15.App.Tests/LedgerScenarioTests.cs`, the two `FileGroupViewModel` constructions:

Replace:

```csharp
        var status = new Status();
        var file = new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status));

```

with:

```csharp
        var status = new Status();
        var file = new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status), new ProjectFileService(s, files, dialogs, status, new Fakes.FakeSettingsStore()));

```

Replace:

```csharp

        new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status)).DownloadTemplateCommand.Execute(null);

```

with:

```csharp

        new FileGroupViewModel(s, files, dialogs, status, new ImportService(s, status), new ProjectFileService(s, files, dialogs, status, new Fakes.FakeSettingsStore())).DownloadTemplateCommand.Execute(null);

```

and the M4a Task 1 scenario, before the private `Picker` class:

Replace:

```csharp

    sealed class Picker(params string?[] answers) : IColorPicker
```

with:

```csharp

    // ─── M4a Task 1 scenario: save, edit, undo to clean, open, and the prompt only for unsaved work ───

    [Fact]
    public async System.Threading.Tasks.Task Save_open_and_the_unsaved_prompt_through_the_real_composition()
    {
        // project-save-format, project-load-flow, project-load-history-interplay, shell-no-unsaved-changes-guard,
        // home-file-group (editor.js:2550-2687): Save work, an edit undone back to the save, then Open through the
        // File group's commands, with the canvas's Changing and Replaced wiring live.
        using var red = Solid(20, 20, Red);
        var s = new EditorSession();
        var dialogs = new Dialogs();
        var files = new Files(null);
        var main = Composition.Build(s, files, dialogs, new Picker(), new Fakes.FakeSettingsStore());
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, red, s.TakeId(), "art")));
        string first = Path.Combine(_dir, "first.r15proj");
        files.ProjectPath = first;

        main.Home.File.SaveCommand.Execute(null);                       // untitled: Save As with the template name's file name
        Assert.Equal("untitled.r15proj", Assert.Single(files.ProjectSuggested));
        Assert.Equal("first.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);

        main.Canvas.Interaction.Wheel(-1);                              // a wheel burst, still open: the title reads dirty at once (R2)
        Assert.Equal("*first.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        Assert.True(s.Undo());                                          // Undo closes the burst as one step and undoes it: back to the save
        Assert.False(s.IsDirty);
        Assert.True(main.Files.ConfirmDiscardChanges());                // clean: no prompt
        Assert.Empty(dialogs.SavePrompts);

        string second = WriteText("second.r15proj", """{ "mode": "Pants", "bgColor": "#112233" }""");
        Assert.True(s.Apply(d => d with { ShowGrid = true }));
        files.ProjectPath = second;
        dialogs.SaveAnswer = SaveChoice.DontSave;
        await main.Home.File.OpenCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "first.r15proj" }, dialogs.SavePrompts);
        Assert.Equal((TemplateMode.Pants, "#112233"), (s.Document.Mode, s.Document.BgColor));
        Assert.Empty(main.Layers.Rows);
        Assert.False(s.Undo());                                         // history cleared on open
        Assert.Equal("second.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        var reread = ProjectIO.Read(File.ReadAllText(first));
        Assert.Equal("art", Assert.Single(reread.Document.Layers).Name);   // first.r15proj kept the save, not the unsaved grid toggle
        Assert.False(reread.Document.ShowGrid);
    }

    sealed class Picker(params string?[] answers) : IColorPicker
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected (measured): the build fails in RBX15.App.Tests with `CS1061`/`CS0117` naming `SaveCommand`, `SaveAsCommand` and `OpenCommand` on `FileGroupViewModel`, `CS1729` (`FileGroupViewModel` has no 6-argument constructor), `CS1501` and `CS7036` (`Composition.Build` with 5 arguments, and with 3), `CS1061` naming `Files` and `CS0117` naming `WindowTitle` on `MainViewModel`.

- [ ] **Step 11: Save work and Open in the File group, the chords, the title, the busy window and the close guard**

Create `src/RBX15.App/Converters/NotConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>Boolean negation for bindings (M4a: the window's input is disabled while an Open reads, interfaces R4).</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class NotConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
```

In `src/RBX15.App/Themes/Controls.xaml`:

Replace:

```xml
  <conv:UpperCaseConverter x:Key="UpperCase" />

```

with:

```xml
  <conv:UpperCaseConverter x:Key="UpperCase" />
  <conv:NotConverter x:Key="Not" />

```

Replace `src/RBX15.App/ViewModels/FileGroupViewModel.cs` with (T1-R8):

```csharp
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's File group (editor.html:85-97; ledger home-file-group): Save work, Open and Add image, then
/// Download template. Save As has no button (interfaces §2.1); the window binds it to Ctrl+Shift+S.
/// </summary>
public sealed class FileGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ImportService _import;

    public FileGroupViewModel(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status, ImportService import,
        ProjectFileService projectFiles)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(projectFiles);
        (_session, _files, _dialogs, _status, _import) = (session, files, dialogs, status, import);
        AddImageCommand = new RelayCommand(AddImage);
        DownloadTemplateCommand = new RelayCommand(DownloadTemplate);
        SaveCommand = new RelayCommand(() => projectFiles.Save(), () => !projectFiles.IsBusy);
        SaveAsCommand = new RelayCommand(() => projectFiles.SaveAs(), () => !projectFiles.IsBusy);
        OpenCommand = new AsyncRelayCommand(projectFiles.OpenAsync, () => !projectFiles.IsBusy);
        projectFiles.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ProjectFileService.IsBusy)) return;
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsCommand.NotifyCanExecuteChanged();
            OpenCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>btnLoadImage (editor.js:2692-2708; project-load-image-layer): pick, then one undo step for all.</summary>
    public IRelayCommand AddImageCommand { get; }

    /// <summary>btnExport (exportPNG, editor.js:1500-1541; canvas-export-pipeline, export-png-guard, io-download-save-dialog).</summary>
    public IRelayCommand DownloadTemplateCommand { get; }

    /// <summary>btnSaveProject, "Save work" (editor.html:88, editor.js:2682): the current file, or Save As. Waits while an Open reads.</summary>
    public IRelayCommand SaveCommand { get; }

    /// <summary>Save As, Ctrl+Shift+S only (spec §3, interfaces §2.1). Waits while an Open reads.</summary>
    public IRelayCommand SaveAsCommand { get; }

    /// <summary>btnLoadProject, "Open" (editor.html:89, editor.js:2683). One run at a time; waits while an Open reads.</summary>
    public IAsyncRelayCommand OpenCommand { get; }

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
        // Atomic: write beside the target, then move over it, so a failed write never truncates an existing file.
        string tmp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, Renderer.ExportPng(doc));
            File.Move(tmp, path, overwrite: true);
            _status.Show($"Saved {name}.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(tmp); } catch (Exception) { /* best effort; the save error below is what matters */ }
            _status.Show($"Couldn't save {name}: {e.Message}");
        }
    }
}
```

Replace `src/RBX15.App/Views/FileGroup.xaml` with (editor.html:85-97; the 3 DIP right margins are the row's `gap:3px`, :87):

```xml
<UserControl x:Class="RBX15.App.Views.FileGroup"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- The Home tab's File group (editor.html:85-97; ledger home-file-group). DataContext:
         FileGroupViewModel. Row 1 is Save work, Open and Add image, row 2 Download template, in 4.1's order and labels
         with its title= tooltips (editor.html:88-90, :93). .ribbon-group-items.vert: column, stretch, gap 4
         (styles.css:207-208); each row is a flex row with gap 3 (editor.html:87, :92); .btn-sm is padding 5px 10px, 11px
         (styles.css:407). Save As has no button (interfaces §2.1): Ctrl+Shift+S. -->
    <Border Style="{DynamicResource RibbonGroup}">
        <DockPanel>
            <TextBlock DockPanel.Dock="Bottom" Text="FILE" Style="{DynamicResource RibbonGroupLabel}" />
            <StackPanel VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                    <Button x:Name="SaveButton"
                            Content="Save work"
                            ToolTip="Save your workspace. Reopen and keep editing."
                            Command="{Binding SaveCommand}"
                            Padding="10,5"
                            FontSize="11"
                            Margin="0,0,3,0"
                            Style="{DynamicResource SecondaryButton}" />
                    <Button x:Name="OpenButton"
                            Content="Open"
                            ToolTip="Open a saved workspace."
                            Command="{Binding OpenCommand}"
                            Padding="10,5"
                            FontSize="11"
                            Margin="0,0,3,0"
                            Style="{DynamicResource SecondaryButton}" />
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

Replace `src/RBX15.App/ViewModels/MainViewModel.cs` with (R3):

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The window's view-model: the shell text and one sub-view-model per region (Canvas, Home, Layers, Status,
/// LayerProperties), plus the project files (M4a Task 1).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    readonly ProjectFileService _files = null!;
    string _title;

    public MainViewModel(EditorSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _title = WindowTitle(session.CurrentFile, session.IsDirty);
        session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Document | SessionChange.File)) == 0) return;
            string next = WindowTitle(Session.CurrentFile, Session.IsDirty);
            if (next == _title) return;
            _title = next;
            OnPropertyChanged(nameof(Title));
        };
    }

    public EditorSession Session { get; }

    /// <summary>
    /// The window title (interfaces R3): 4.1's product name while the document is untitled and clean (main.js:8,
    /// editor.html:6; ledger shell-window-title, keep), otherwise the Windows convention
    /// "[*]&lt;name&gt; - RBX15 Classic Shirt and Pants Maker". Raised only when the text changes.
    /// </summary>
    public string Title => _title;

    /// <summary>
    /// R3's formula. <paramref name="currentFile"/> null and clean is the bare product name; otherwise the file name
    /// (or "Untitled"), prefixed with "*" when <paramref name="dirty"/>, then " - " and the product name. "*" rather than
    /// "•": Narrator reads it as "star", it is the classic Win32 marker, and it survives every font.
    /// </summary>
    public static string WindowTitle(string? currentFile, bool dirty) =>
        currentFile is null && !dirty
            ? AppInfo.ProductName
            : (dirty ? "*" : "") + ProjectFileService.DocumentName(currentFile) + " - " + AppInfo.ProductName;

    /// <summary>The header h1, verbatim (editor.html:16, <c>&amp;amp;</c> decoded; ledger shell-header).</summary>
    public string HeaderTitle => "RBX15 Classic Shirt & Pants Maker";

    /// <summary>
    /// The header byline. 4.1 hand-typed "ItsjustEste | v4.1" (editor.html:17); v5 builds the same shape
    /// from the assembly's major.minor (ledger shell-header-version-hardcoded, fix, whose parity check
    /// reads 'ItsjustEste | v&lt;major.minor&gt;'). The view upper-cases it (styles.css:153).
    /// </summary>
    public string HeaderByline => "ItsjustEste | v" + MajorMinor(AppInfo.Version);

    /// <summary>
    /// The footer (ledger shell-footer, fix): 4.1's "Developed at 626 Labs LLC" (editor.html:343) reads
    /// "Developed at 626Labs" in v5 (Este, 2026-10-14). The view upper-cases it (styles.css:686).
    /// </summary>
    public string FooterText => "Developed at 626Labs";

    static string MajorMinor(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? parts[0] + "." + parts[1] : version;
    }

    /// <summary>True while an Open reads its file (R4); forwards <see cref="ProjectFileService.IsBusy"/>.</summary>
    public bool IsBusy => _files.IsBusy;

    /// <summary>The canvas area (Task 2). Required, so the composition root cannot forget it.</summary>
    public required CanvasViewModel Canvas { get; init; }

    /// <summary>The Home tab (Task 3).</summary>
    public required HomeViewModel Home { get; init; }

    /// <summary>The drop zone and layers panel (Task 4).</summary>
    public required LayersViewModel Layers { get; init; }

    /// <summary>The status strip (Task 4).</summary>
    public required StatusViewModel Status { get; init; }

    /// <summary>The left panel's "Selected layer" section (M3b Task 3; editor.html:320-323).</summary>
    public required LayerPropertiesViewModel LayerProperties { get; init; }

    /// <summary>Save, Save As, Open and the unsaved-changes guard (M4a Task 1). The window's close runs the guard.</summary>
    public required ProjectFileService Files
    {
        get => _files;
        init
        {
            _files = value ?? throw new ArgumentNullException(nameof(value));
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProjectFileService.IsBusy)) OnPropertyChanged(nameof(IsBusy));
            };
        }
    }
}
```

In `src/RBX15.App/MainWindow.xaml`, the title:

Replace:

```xml
        x:Name="Root"
        Title="{x:Static app:AppInfo.ProductName}"
        Width="{x:Static app:ShellLayout.DefaultWidth}" Height="{x:Static app:ShellLayout.DefaultHeight}"
```

with:

```xml
        x:Name="Root"
        Title="{Binding Title}"
        Width="{x:Static app:ShellLayout.DefaultWidth}" Height="{x:Static app:ShellLayout.DefaultHeight}"
```

and the comment and the Viewport (R4):

Replace:

```xml

  <!-- Ctrl+Z, M3b's one shortcut, is added in MainWindow.xaml.cs behind TextFieldShortcutCommand. -->

  <Grid x:Name="Viewport">
    <Grid.RowDefinitions>
```

with:

```xml

  <!-- Ctrl+Z, Ctrl+S, Ctrl+Shift+S and Ctrl+O are added in MainWindow.xaml.cs (AddShortcut) behind TextFieldShortcutCommand.
       The title is MainViewModel.Title (M4a R3). While an Open reads its file the whole client takes no input (R4). -->

  <Grid x:Name="Viewport" IsEnabled="{Binding IsBusy, Converter={StaticResource Not}}">
    <Grid.RowDefinitions>
```

Replace `src/RBX15.App/MainWindow.xaml.cs` with:

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// The main window: the shell skeleton, window placement memory (ledger shell-window-size, fix), the window's
/// shortcuts, and the unsaved-changes guard on close (M4a R13). The placement is restored before the window is shown
/// and saved when it closes.
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
        CaptionTheme.Apply(this);
        ApplyPlacement(PlacementGuard.Resolve(settings.LoadWindowPlacement(), workAreas ?? MonitorWorkAreas.Get(),
                                              ShellLayout.DefaultWidth, ShellLayout.DefaultHeight));
        Viewport.SizeChanged += (_, e) => PanelColumn.Width = new GridLength(ShellLayout.PanelWidthFor(e.NewSize.Width));
        // The window's shortcuts (spec §4 "Keyboard"; M4a: the three file chords, the rest is M4b). A focused text field
        // keeps each key even when it has nothing to do with it (final fix wave 5); anywhere else it is the command.
        AddShortcut(viewModel.Home.Edit.UndoCommand, Key.Z, ModifierKeys.Control);
        AddShortcut(viewModel.Home.File.SaveCommand, Key.S, ModifierKeys.Control);
        AddShortcut(viewModel.Home.File.SaveAsCommand, Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddShortcut(viewModel.Home.File.OpenCommand, Key.O, ModifierKeys.Control);
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) _maximizedBeforeMinimize = WindowState == WindowState.Maximized; };
        // R4: a wait cursor while an Open reads; the Viewport's IsEnabled binding blocks the input.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBusy)) Mouse.OverrideCursor = viewModel.IsBusy ? Cursors.Wait : null;
        };
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>The element with keyboard focus, as the window's shortcuts see it. Tests replace it; nothing else does.</summary>
    internal Func<IInputElement?> FocusedElement { get; set; } = () => Keyboard.FocusedElement;

    /// <summary>
    /// Binds <paramref name="key"/> with <paramref name="modifiers"/> on the window to <paramref name="command"/> behind
    /// <see cref="TextFieldShortcutCommand"/>, so a focused text field keeps the key (spec §4: only text fields trap
    /// shortcuts). Returns the binding.
    /// </summary>
    internal KeyBinding AddShortcut(ICommand command, Key key, ModifierKeys modifiers)
    {
        ArgumentNullException.ThrowIfNull(command);
        var binding = new KeyBinding(new TextFieldShortcutCommand(command, () => FocusedElement()), key, modifiers);
        InputBindings.Add(binding);
        return binding;
    }

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

    /// <summary>
    /// Closing (R13): a pending Open's read is cancelled and released; dirty work asks Save / Don't save / Cancel, and
    /// Cancel, a cancelled Save As or a failed save keeps the window open. A clean document closes with no prompt
    /// (ledger shell-no-unsaved-changes-guard parityCheck). The placement is saved only when the window really closes.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            ViewModel.Files.CancelPendingOpen();
            if (!ViewModel.Files.ConfirmDiscardChanges()) e.Cancel = true;
        }
        if (!e.Cancel) _settings.SaveWindowPlacement(CapturePlacement());
    }
}
```

Replace `src/RBX15.App/Composition.cs` with:

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
    public static MainViewModel Build(EditorSession session, Func<Window?> owner, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Build(session, new Win32FileDialogs(owner), new ThemedDialogService(owner), new ThemedColorPicker(owner), settings);
    }

    /// <summary>
    /// The wiring, once: the native services come in, so the app passes the real ones and the tests'
    /// TestShell passes fakes. Every line that connects view-models lives here and nowhere else.
    /// </summary>
    public static MainViewModel Build(EditorSession session, IFileDialogs fileDialogs, IDialogService dialogs, IColorPicker colorPicker, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fileDialogs);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(colorPicker);
        ArgumentNullException.ThrowIfNull(settings);
        // M3b: the status strip, the tool and the brush come first; the canvas, the Home tab and the eraser share them.
        var status = new StatusViewModel();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(colorPicker);
        var canvas = new CanvasViewModel(session, tools, brush, status);
        // Task 4: import, export, layers and status (one ImportService serves Add image, the drop zone and the canvas).
        var import = new ImportService(session, status);
        // addLayer and selectLayer set the Select tool; a drawing commit keeps the drawing tool (editor.js:523, :577; R8).
        import.LayersAdded += (_, _) => tools.Set(Tool.Select);
        // M4a Task 1: Save, Save As, Open and the unsaved-changes guard over the session (spec §3).
        var projectFiles = new ProjectFileService(session, fileDialogs, dialogs, status, settings);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, dialogs, status, import, projectFiles);
        var layers = new LayersViewModel(session, import, fileDialogs);
        layers.LayerPicked += (_, _) => tools.Set(Tool.Select);
        // A canvas drop lands every file on the drop point (editor.js:961-986, drop-canvas).
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        // M3b Task 3: the "Selected layer" panel (editor.html:320-323).
        var layerProperties = new LayerPropertiesViewModel(session);
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
            LayerProperties = layerProperties,
            Files = projectFiles,
            // M3a: required members above this line.
        };
    }
}
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
        // The settings store comes first: the composition remembers the last project folder in it (M4a Task 1).
        var settings = new JsonSettingsStore(SettingsDirectory);
        var main = Composition.Build(session, () => Current?.MainWindow, settings);
        MainWindow = new MainWindow(main, settings);
        MainWindow.Show();
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.App.Tests.ShellTests|FullyQualifiedName~RBX15.App.Tests.WindowTitleTests|FullyQualifiedName~RBX15.App.Tests.FileGroupTests|FullyQualifiedName~RBX15.App.Tests.Task4ViewTests|FullyQualifiedName~RBX15.App.Tests.HomeRibbonViewTests|FullyQualifiedName~RBX15.App.Tests.ShellLookTests|FullyQualifiedName~RBX15.App.Tests.ShellWiringTests|FullyQualifiedName~RBX15.App.Tests.LedgerScenarioTests"`
Expected: `Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97` for `RBX15.App.Tests.dll` (ShellTests 25, WindowTitleTests 6, FileGroupTests 13, Task4ViewTests 6, HomeRibbonViewTests 13, ShellLookTests 5, ShellWiringTests 2, LedgerScenarioTests 27).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 885, Failed: 0, Skipped: 0` (880 + 5); Parity.Tests `Passed: 35, Failed: 0`; App.Tests `Passed: 715, Failed: 0` (644 + 71).

Run: `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor" src`
Expected: no output.

Commit. End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Converters/NotConverter.cs src/RBX15.App/Themes/Controls.xaml src/RBX15.App/ViewModels/FileGroupViewModel.cs src/RBX15.App/Views/FileGroup.xaml src/RBX15.App/ViewModels/MainViewModel.cs src/RBX15.App/MainWindow.xaml src/RBX15.App/MainWindow.xaml.cs src/RBX15.App/Composition.cs src/RBX15.App/App.xaml.cs src/RBX15.App.Tests/WindowTitleTests.cs src/RBX15.App.Tests/ShellTests.cs src/RBX15.App.Tests/FileGroupTests.cs src/RBX15.App.Tests/Task4ViewTests.cs src/RBX15.App.Tests/HomeRibbonViewTests.cs src/RBX15.App.Tests/HomeTestKit.cs src/RBX15.App.Tests/ShellLookTests.cs src/RBX15.App.Tests/ShellWiringTests.cs src/RBX15.App.Tests/TestShell.cs src/RBX15.App.Tests/LedgerScenarioTests.cs
git commit -m "feat(app): Save work and Open in the File group, the file chords, the window title and the close guard"
```

**Handoff to Task 5** (the ledger and divergences; Task 1 closes nothing):

1. **Evidence by row:** `project-save-format`, `file-project-format`, `project-format-r15proj`, `project-save-filename`, `project-load-flow`, `project-load-layers`, `project-load-error-path-bugs`, `project-load-no-imgsrc-hang`, `project-load-untrusted-src`, `project-load-history-interplay`, `project-template-name-not-saved`, `io-download-save-dialog` (its cancel and confirm halves for `.r15proj`), `shell-no-unsaved-changes-guard`, `shell-unsaved-work-loss`, `home-file-group`: the facts named in the steps above, plus the `brg files` smoke run (interfaces §3.9) for the live dialogs and chords. `shell-no-unsaved-changes-guard` and `shell-unsaved-work-loss` are `ui-auto`/manual checks; Step 10's close facts are unit evidence, and the smoke's UIA prompt run is the live one.
2. **Divergence rows** (contract "divergence rows for the unsaved prompt moved from M5"): the unsaved-changes prompt on Open and on close, landed in M4a ahead of M5's autosave; `shell-unsaved-work-loss` (disposition `new`) gets its sentence.
3. **`project-load-layers`' divergence** says "nextId and selection always set"; T1-R2 (Este's ruling) selects the top layer after Open, so the text stays true and Task 5 leaves it alone.
4. **`shell-window-title`** (keep, done): unchanged at launch, but the title now carries the file name and `*` once a file is saved or the document is edited (R3). Add a divergence sentence or a narrowed note.
5. **R12 note** on `project-load-layers`: out-of-range values load raw (pinned by `ProjectFileServiceTests.Out_of_range_values_load_raw`).
6. **§2.2 note** on `project-load-flow`: the Open dialog lists `.r15proj` first and `.json` second.
7. **`undo-buttons-shortcuts`**: the three file chords are behind the same text-field guard as Ctrl+Z (`ShellTests.A_file_chord_runs_its_file_group_command_unless_a_text_field_has_focus`); Ctrl+Y and Ctrl+Shift+Z remain M4b.
8. **`shell-no-persistence`**: settings.json gains `lastProjectFolder` (and Task 2's `lastAssetFolder`); M6's PRIVACY.md pass lists them.

**Additions requested by Task 1** (beyond `.superpowers/m4-planning/interfaces.md`; the sheet's names are unchanged):

1. `internal static IReadOnlyList<SKImage> Adjust.TakeShifted(SKImage source)` (engine): removes and returns an image's cached RGB shifts so `Images.Release` frees them (T1-R4).
2. `public const string Win32FileDialogs.ProjectOpenFilter` and `ProjectSaveFilter`: the §2.2 filter strings, pinned by a test.
3. `internal static string JsonSettingsStore.FolderKey(SettingsFolder)`: `lastProjectFolder` / `lastAssetFolder`.
4. `internal static ProjectLoad ProjectFileService.ReadFile(string path, long maxBytes = MaxProjectBytes)` and `internal const long MaxProjectBytes`: the default read's body (the size check of T1-R14, UTF-8 text, then `ProjectIO.Read`).
5. `ProjectFileService.Save` and `SaveAs` catch `InvalidOperationException` as well (T1-R7), and `OpenAsync(string)` catches `NotSupportedException` and `ArgumentException` (T1-R6).
6. `CanvasInteraction` (not in the sheet's Task 1 file list): its `Changed` handler calls `Cancel()` on `SessionChange.Replaced` (R11's own consequence; T1-R5), with `CanvasInteractionTests.An_open_mid_stroke_discards_it`.
7. Fake members: `FakeFileDialogs.{OpenProjectResult, OpenProjectDirs, SaveProjectResult, SaveProjectCalls, PickFolderResult, PickFolderDirs}` (Task 3's re-pick counters can read `OpenProjectDirs.Count` and `PickFolderDirs.Count`); `FakeSettingsStore.{Folders, FolderSaves}`.
8. `HomeTestKit.FileGroup(EditorSession, IFileDialogs, IDialogService, IStatus) : FileGroupViewModel`: one place that builds the File group with its `ProjectFileService`; `FileGroupTests`, `ShellLookTests`, `Task4ViewTests` and `HomeTestKit.Home` use it.
9. `LedgerScenarioTests`' private `Dialogs` (`SaveAnswer`, `SavePrompts`) and `Files` (`ProjectPath`, `ProjectSuggested`) implement the new interface members.
10. `EditorSession.Open` also collects the saved marker's images (T1-R3), and `NextId` is `Math.Max(load.NextId, max layer id + 1)`, a guard that equals `load.NextId` for every `ProjectIO.Read` result.
11. T1-R2 conflicts with `project-load-layers`' divergence text. Resolved: the contract wins, and Task 5's `m4a-ledger.py` rewords the divergence (critic M9).
12. Test files touched beyond the sheet's Task 1 row: `CanvasInteractionTests` (+1), `Task4ViewTests` (one fact rewritten), `HomeRibbonViewTests` (one assertion), `ShellLookTests` and `ShellWiringTests` (constructor and `Build` calls), `HomeTestKit`.
13. **Reconciler (critic M1, L7):** `ProjectFileService.MaxProjectBytes`, `ReadFile`'s `maxBytes` parameter and `ProjectFileServiceTests.A_file_over_the_size_limit_alerts_and_changes_nothing` (+1); `WriteTo` uses `File.Replace` over an existing target.

---

### Task 2: The asset library and packs (engine scan, dedupe and grouping; packs in the output; the view-model and panel; lazy thumbnails; click, right-click and drag add)

**Files:**

- Create (engine): `src/RBX15.Engine/AssetScan.cs` (`AssetEntry`, `AssetGroup`, `AssetSet`, `AssetScan`).
- Modify (engine): `src/RBX15.Engine/Images.cs` (+ `DecodeThumbnail`), `src/RBX15.Engine/LayerOps.cs` (+ `AddFull`).
- Create (engine tests): `src/RBX15.Engine.Tests/AssetScanTests.cs` (38).
- Modify (engine tests): `src/RBX15.Engine.Tests/ImagesCapsTests.cs` (+7), `src/RBX15.Engine.Tests/ImagesOrientationTests.cs` (+2), `src/RBX15.Engine.Tests/LayerOpsTests.cs` (+6).
- Create (app):
  - `src/RBX15.App/Services/PackSource.cs`, `src/RBX15.App/Services/AssetThumbnailLoader.cs`.
  - `src/RBX15.App/ViewModels/AssetLibraryViewModel.cs`, `AssetRowViewModel.cs`, `AssetTileViewModel.cs`.
  - `src/RBX15.App/Views/AssetLibraryPanel.xaml` and `AssetLibraryPanel.xaml.cs`, both printed in full.
  - `src/RBX15.App/Controls/AssetRowPanel.cs` (Addition 1), `src/RBX15.App/Converters/EqualsMultiConverter.cs` (Addition 2).
- Modify (app):
  - `src/RBX15.App/RBX15.App.csproj`: the packs `Content` item.
  - `src/RBX15.App/Services/ImportService.cs` (+ `ImportFull`).
  - `src/RBX15.App/ViewModels/MainViewModel.cs` (+ `Assets`), `src/RBX15.App/Composition.cs` (the `assets` line and member, at the two anchors).
  - `src/RBX15.App/MainWindow.xaml` (the panel first in `LeftPanelContent`), `src/RBX15.App/Views/DropZone.xaml` (its heading takes the non-first margin).
- Create (app tests): `src/RBX15.App.Tests/PackSourceTests.cs` (4), `AssetThumbnailLoaderTests.cs` (6, Addition 9), `AssetLibraryViewModelTests.cs` (25), `AssetLibraryPanelTests.cs` (19, one of them the reconciler's, critic M8).
- Modify (app tests): `ImportServiceTests.cs` (+1); `ThemeKeyGuardTests.cs` (view count 15 to 16) and `LayerPropertiesPanelTests.cs` (the left panel's order), with no count change.
- Read only: 4.1's `editor.js`, `editor.html`, `styles.css`, `main.js`, `package.json` and `packs/` (untouched); `docs/port/parity-ledger.json`.
- Not touched: the ledger and `divergences.md` (Task 5, from the handoff at the end); `Themes/*.xaml` (no new theme key: the panel's own styles live in its resources). Files Task 1 also edits (`Composition.cs`, `MainViewModel.cs`, `MainWindow.xaml`) change here only at the named anchors.

**Interfaces:**

- Consumes:
  - **Task 1:**
    - `IFileDialogs.PickFolder(string? initialDirectory) : string?`.
    - `SettingsFolder.Asset`, `ISettingsStore.LoadFolder(SettingsFolder)` and `SaveFolder(SettingsFolder, string)`.
    - `DialogText.PackNotFound(string)`, `DialogText.PackEmpty(string)`, `DialogText.NoImagesInFolder`.
    - `Composition.Build(EditorSession, IFileDialogs, IDialogService, IColorPicker, ISettingsStore settings)`, whose `settings` parameter the new line uses, and `TestShell.Main` passing a `FakeSettingsStore`.
    - In the fakes: `FakeSettingsStore` stores folders in memory through the interface (`SaveFolder` then `LoadFolder` returns it); `FakeFileDialogs.PickFolderResult` (`string?`, returned by `PickFolder`) and `FakeFileDialogs.PickFolderDirs` (`List<string?>`, each `PickFolder` argument), both printed in full by Task 1 Step 7 (critic C1).
  - **M3b:** `ImportService` (`Import`, `LayersAdded`, `FailureMessage`), `ImageImporter` (`Extensions`, `LayerName`, `Load`), `LayerOps.Place` (the front slot by mode, which makes Pants work through its slot), `CanvasCoordinates.ToTemplate`/`ToFrame`, `TemplateCanvasHost` (`Zoom`, `HandleDrop`), `CanvasViewModel.Zoom`, `MainWindow`, `LayersPanel`, the theme keys `PanelHeading`, `SecondaryButtonSm`, `ThemedScrollViewer`, `FocusRing`, `FontMono`, `FontBody`, `Border2Brush`, `BrandGradientBrush`, `UpperCase`.
  - Test helpers: `StaThread.Run`, `StaThread.Pump`, `TestShell.Main`, `TestFiles`, `FakeStatus`, `FakeDialogService` (`Alerts`), `RepoRoot.Find`, `Px` (engine).
- Produces (interfaces.md §3 names verbatim; "+" marks an addition listed at the end):
  - Engine: `AssetEntry`, `AssetGroup`, `AssetSet`, `AssetScan` (`AllKey`, `RootKey`, `Extensions`, `IsImageName`, `FromFolder`, `FromPack`, `Caption`, `CountText`); `Images.DecodeThumbnail(byte[], int)`; `LayerOps.AddFull(EditorSnapshot, SKImage, int, string)`.
  - App services: `ImportService.ImportFull(string)`; `PackSource` (`Drips`, `WordArt`, `DefaultDirectory`, `Files`); `AssetThumbnailLoader` (`PixelSide`, `LoadAsync`, `Dispose`; + the internal test constructor and `ToBitmapSource`).
  - View-models: `AssetLibraryViewModel` with every member of interfaces §3.5 (+ the constants `DefaultBrowserHeight`, `MinBrowserHeight`, `TilesPerRow`; + internal `Set`, `Enumerate`, `FolderFailedMessage`); `AssetRowViewModel`; `AssetTileViewModel` (+ internal constructor and `ThumbnailRequested`); `MainViewModel.Assets`.
  - View: `AssetLibraryPanel` with the §3.6 seams, where `HandleRelease` takes root-visual coordinates (+ clarification), plus + `IsDragging`, `IsResizing`, `HandleResizePress`, `HandleResizeEnd`, `CanvasAt`, `TileFrom`; + the named parts listed in Addition 6.
  - + `AssetRowPanel`, + `EqualsMultiConverter`.
  - The packs in every output that references the App, at `packs/<name>/<file>`.

**What 4.1 does (cited).** The shipped `editor.js`, `editor.html`, `styles.css`, `main.js` and `package.json`, untouched.

- **The panel** (editor.html:291-309): the first `h3` "Asset library"; label "Built-in packs" (10px #888, editor.html:293) over a `.row` of Drips and Word art (`btn btn-sm btn-secondary`, tooltips editor.html:295-296); label "Load folder" (margin-top 6px, editor.html:298) over Open folder (editor.html:300); `#assetBrowser` (max-height 180px, overflow-y auto, margin-top 6px, display none, editor.html:303) holding `#assetSubfolders` (flex-wrap, gap 3px, margin-bottom 6px, :304) and `#assetGrid` (3 columns, gap 4px, :305); `#assetCount` (10px #555, margin-top 4px, :307); `#assetResizeHandle` (title "Drag to resize", :308). `.controls label` is mono 10px uppercase, margin 8px 0 3px (styles.css:324-332); `.row > * { flex: 1 }` (styles.css:369-370); `.btn-sm` is padding 5px 10px, 11px (styles.css:407).
- **Packs** (editor.js:2325-2378): `resolvePackDir` looks beside editor.js, then in `process.resourcesPath/packs` (:2325-2333); electron-builder ships `packs/**/*.png` there (package.json:31-38). `readdirSync`, filter `/\.(png|jpg|jpeg|gif|webp)$/i`, every file read synchronously (:2347-2353), with the fake path `packs/<pack>/<name>` (:2357). Missing: alert (:2344); empty: alert (:2364). The groups are `(all)` and the pack (:2370), the subfolder buttons are cleared (:2372), and the count text `N images in built-in <pack> pack` (:2373) is overwritten by `showAssetFolder('(all)')` in the same tick (:2374). Node runs in the page because main.js:10 sets `nodeIntegration: true`.
- **Open folder** (editor.js:2316, :2380-2450): the input is cleared and clicked; the filter is the same regex (:2382); no images: alert, state unchanged (:2384-2387). Dedupe by `f.name` in a Map, a strictly deeper path replacing the kept one (:2390-2398). Group by the immediate parent, `(root)` at depth 2 or less (:2403-2416); `(all)` is all unique files (:2419). Buttons only past two keys (:2432), sorted `(all)`, `(root)`, then `localeCompare` (:2424-2430), labelled `All (n)` / `Root (n)` / `name (n)`, title = key, 9px with padding 2px 6px (:2435-2442). The label `U unique images (D duplicates removed)` (:2448) is overwritten by `showAssetFolder('(all)')` (:2449).
- **showAssetFolder** (editor.js:2452-2545): revokes the old blob URLs (:2456-2460); highlights the button whose title is the folder, #2d4a2d on #7fff7f (:2463-2466); for each file a square, 1px #333, radius 4, #111 thumb with `title = file.name` (:2470-2471), the image at `max-width/max-height 100%`, `object-fit: contain` (:2474), decoded from a blob URL (:2475), and a 7px #ccc caption on rgba(0,0,0,.7), one line with an ellipsis (:2479), showing the name minus the 5 image extensions, or the parent folder when the name starts like a UUID and the path has 3 or more parts (:2480-2484). The count text `N images in all folders` or `N images in <folder>` (:2544).
- **Adds:** a click reads the file and calls `addLayer(img, displayName)` (:2502-2510), with `displayName` the name minus its last extension (:2490); `addLayer` fits at 85% in `SHIRT_REGIONS.torso_front`, centered, or centered on a drop point, then pushes, selects and sets the Select tool (editor.js:500-526). A right-click prevents the menu (:2514) and adds `name (full)` at 0,0 with `max(TW / w, TH / h)` (:2519-2534). A drag stores `_assetDragFile` (:2494-2500); `canvas.ondrop` maps `(clientX - rect.left) / zoom` (:965-967) and, if the global is set, clears it and adds at the drop point (:970-983). Nothing else ever clears it (editor.js:101-102).
- **Resize** (editor.js:2713-2743; styles.css:649-673): mousedown prevents the default, `startH = browser.offsetHeight || 180` (:2724); every move sets `max-height = max(80, min(innerHeight * 0.7, startH + delta))` (:2732-2733); a document mouseup ends it (:2736-2742). The handle is 8px, margin 2px 0, ns-resize, with a 40x3 bar in border-2 that turns to the brand gradient on hover and while active.

**Ledger rows this task serves** (dispositions followed; Task 5 closes rows under the honesty rule): `packs-inventory-drips`, `packs-inventory-wordart`, `pack-contents`, `packs-resolve-dir`, `packs-load-builtin`, `pack-builtin-load` (keep), `pack-load-sync-and-mime` (fix), `asset-open-folder` (keep), `asset-dedupe-by-name` (keep), `asset-folder-grouping` (keep), `asset-count-label` and `asset-count-label-overwritten` (fix), `asset-grid-thumbnails` and `asset-objecturl-lifecycle` (fix), `asset-click-add` (keep), `asset-rightclick-full` (keep), `asset-drag-to-canvas` and `asset-drag-stale-reference` (fix), `asset-resizable-browser` (keep), `asset-thumb-uuid-label` (keep), `panel-asset-library` (fix), `panel-asset-resize-handle` (keep), `io-folder-picker` (fix), `io-image-formats` (fix, the R14 BMP note is Task 5's).

**Rulings** (each pinned by a named test):

- **T2-R1: Hand, not Copy, over the canvas during a drag** (amends interfaces R6). WPF's `Cursors` has no Copy member: the arrow-with-plus is OLE's drag feedback, which R6 rules out, and `Cursors.Copy` fails to compile with CS0117 (measured). Over a canvas host the panel shows `Cursors.Hand`, the tiles' own pointer; anywhere else `Cursors.No`. 4.1 prevented `dragover` on the whole document (editor.js:989), so the page accepted the drag everywhere and only the canvas acted on it. Provisional, M4b look sign-off. Pinned by `A_drag_onto_the_canvas_adds_the_tile_centered_on_the_drop_point` (Hand) and `A_drag_dropped_elsewhere_never_hijacks_the_next_os_file_drop` (No).
- **T2-R2: a tile is a Button.** Its click (mouse, Enter, Space, or UI Automation Invoke) is the click-add, so the library is keyboard and screen-reader operable where 4.1's divs were not. The panel's tunnelling handlers only arm and run the drag: a move past the system drag distance captures the mouse on the panel, which takes the press capture from the Button, so a drag never also clicks. Task 5 adds "thumbnails are keyboard operable" to `panel-asset-library`'s divergence. Pinned by `Invoking_a_tile_adds_it_fit_in_the_front_slot` and `A_drag_starts_past_the_distance_and_a_release_off_the_canvas_adds_nothing`.
- **T2-R3: rows are a custom square panel.** `AssetRowPanel` sizes three square cells from the width it is measured with. Measured first with a `UniformGrid` and `Height="{Binding ActualWidth}"` tiles: the rows measured 0 tall in the first pass, so the virtualizing list realized 45 of 52 rows (135 tiles) before the heights arrived. With `AssetRowPanel`, 3 rows are realized at 180 DIP. Pinned by `Tiles_are_square_three_to_a_row_and_only_visible_rows_are_realized_and_decoded`.
- **T2-R4: the grid's scrollbar takes its width,** as `overflow-y: auto` did with the thin scrollbar (styles.css:115-116): tiles are `(251 - 8 - 8) / 3 = 78.33` DIP in the 280 panel while the grid scrolls, 81 when it does not. Pinned by the same test (`ViewportWidth = 243`) and `A_short_grid_resizes_from_its_rendered_height` (a one-row grid renders 85: 81 plus the row gap).
- **T2-R5: provisional layout notes for the M4b look sign-off.** The subfolder buttons sit above the scrolling grid (4.1 scrolled them with it, editor.html:303-305); the last row carries its 4 DIP gap below it; the count label collapses while empty (an empty div has no height); the labels are upper-cased without CSS's .08em letter-spacing (the M3b `PanelHeading` observation). None has a pinning test; Task 5 lists them on the look page.
- **T2-R6: captions keep 4.1's regex verbatim.** R14 adds `.bmp` to the folder filter only; the caption strips `png|jpg|jpeg|gif|webp` (editor.js:2480), so `photo.bmp` is captioned `photo.bmp`, while its layer is named `photo` (editor.js:2490). Pinned by `Captions_use_the_parent_for_uuid_names` (the `photo.bmp` row). Task 5 can fold this into the R14 `io-image-formats` note.
- **T2-R7: a pack folder that cannot be listed is "not found".** `PackSource.Files` returns null for a missing folder and for one that throws on listing, so both show 4.1's reinstall alert (editor.js:2344). An installed pack that cannot be read is a broken install. Pinned by `A_missing_pack_alerts_and_changes_nothing` (missing) and `Files_lists_the_top_level_of_a_pack_or_null_when_it_is_missing`.
- **T2-R8: a folder that cannot be listed goes to the status strip** as `Couldn't open <folder>: <reason>`, following the shape of the M3b import line and Task 1's save line (interfaces §3.5 OpenFolderCommand; `diag-console-only-errors`). It is new text, since Chromium either listed a folder or showed nothing; Task 3 adds it to the inventory. Pinned by `A_folder_that_cannot_be_listed_goes_to_the_status_strip`.
- **T2-R9: a failed thumbnail leaves its tile empty with its caption** and sets `ThumbnailFailed`; 4.1 showed a broken image. The summary on the status strip, once per set, is Task 3's (interfaces §4). Pinned by `A_tile_decodes_on_first_read_and_reports_a_failure`.
- **T2-R10: the resize starts from the rendered height and reads its upper clamp at the press.** `startH` is the browser's `ActualHeight`, or 180 while it is hidden (`offsetHeight || 180`, editor.js:2724). `MaxBrowserHeight` is set to `0.7 x` the window content's height at each press (`innerHeight`, editor.js:2732), and the lower bound wins as `Math.max` does. The handle captures the mouse, so a release outside the window ends it (4.1 stayed stuck until the next mouseup: the `asset-resizable-browser` quirk). Pinned by `The_handle_resizes_the_browser_from_its_rendered_height_within_the_clamps`, `A_short_grid_resizes_from_its_rendered_height`, `In_the_window_the_resize_clamps_at_70_percent_of_the_client` and `The_lower_clamp_wins_in_a_short_window`.
- **T2-R11: a drop needs a hit inside an enabled `TemplateCanvasHost`.** The zoom pill, the area around the frame and the panels are no canvas, as `canvas.ondrop` fired only on the canvas element; a disabled host (Open running, interfaces R4) takes nothing. Pinned by `A_disabled_canvas_or_the_zoom_pill_takes_no_drop`.
- **T2-R12: enumeration follows the sheet's options.** `new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }` keeps .NET's default `AttributesToSkip = Hidden | System`, so hidden and system files are not listed. 4.1's Chromium folder input was not measured for hidden files; Task 5 records the difference only if the folder check shows one.
- **T2-R13: the asset library reuses `ImportService`,** so each add is exactly one undo step, a failure is named on the status strip, and `LayersAdded` sets the Select tool. Pants needs nothing new: `LayerOps.Place` already takes the front slot for the mode (`bug-pants-mode-region-keys`). Pinned by `A_click_adds_the_file_fit_in_the_front_slot_as_one_step` (Shirt and Pants rows) and `Library_adds_revert_the_tool_to_select`.
- **T2-R14: Esc reaches a running drag from anywhere in the window** (critic M8). The panel's own `OnPreviewKeyDown` sees Esc only while keyboard focus is inside the panel; a focus change mid-drag, or the bridge's key target when the app is not foreground, would let the next release drop the tile. When a drag starts, the panel adds a `PreviewKeyDown` handler to its window that calls `HandleKey`, and `Finish()` removes it. Pinned by `Esc_anywhere_in_the_window_cancels_a_drag`.
- **T2-R15: the thumbnail loader is not disposed at exit** (critic L12, ruled; not applied). `AssetThumbnailLoader.Dispose` only cancels queued requests; its worker is a thread-pool task, so process exit ends it, and an `IDisposable` chain through `AssetLibraryViewModel` and the window would add surface for no behavior. A new load already cancels the old set's queue (`A_new_load_cancels_the_previous_sets_queued_thumbnails`).

**Measured while drafting:**

- **Setup.**
  - A scratch copy of main `3f3c5c5` (`git archive HEAD` under `%TEMP%`, outside the repo; the repo stayed untouched), .NET SDK 10.0.302.
  - Stand-ins for Task 1, written from interfaces.md: `IFileDialogs.{OpenProject, SaveProject, PickFolder}` (with `OpenFolderDialog` in `Win32FileDialogs` and nulls in `LedgerScenarioTests.Files`); `SettingsFolder` and `ISettingsStore.{LoadFolder, SaveFolder}` (no-ops in `JsonSettingsStore`); an in-memory `FakeSettingsStore`; `FakeFileDialogs.FolderResult` and `FolderInitialDirectories` (renamed below to Task 1's `PickFolderResult` and `PickFolderDirs`, critic C1); `DialogText`'s three pack and folder entries; `Composition.Build(…, ISettingsStore settings)` with `App.xaml.cs`, `TestShell` and `ShellWiringTests` passing a store. The baseline with the stand-ins alone ran Engine 880, App 644, Parity 35, all green.
- **Build and tests.**
  - Every file below compiled under `-warnaserror` with 0 warnings.
  - Engine.Tests went to 933 (+53): AssetScanTests 38, ImagesCapsTests +7, ImagesOrientationTests +2, LayerOpsTests +6.
  - App.Tests went to 698 (+54): PackSourceTests 4, AssetThumbnailLoaderTests 6, AssetLibraryViewModelTests 25, AssetLibraryPanelTests 18, ImportServiceTests +1. Parity.Tests stayed 35.
  - Three full solution runs, all green.
  - The packs reached both `RBX15.App/bin/Release/net10.0-windows/packs` and the App.Tests output (156 and 25 files): a referenced project's `Content` items flow to the referencing output.
- **Failure before implementation, measured:** without `AssetScan.cs`, the engine tests fail to build with CS0246 (`AssetSet`). The other "fails to build" lines below name the error the missing member gives (CS0117, CS0103 or CS0246); those were not each re-measured.
- **4.1 numbers:** node 22 printed every literal from 4.1's own expressions:
  - addLayer's fit and drop placement (editor.js:502-511): 128x150 gives `248.57866666666666, 83.6, 0.7253333333333334`, and dropped at (300, 200) gives `253.57866666666666, 145.6`.
  - The right-click scale (editor.js:2525): `2.795`, `4.5703125`, `9.140625`, `4.203007518796992`, `585`, and `4.472` for 198x125.
  - `localeCompare` order for the group sort test.
  - `readdirSync` order of both packs (first and last names).
- **Findings that shaped the code:**
  - T2-R1: `Cursors.Copy` does not exist (CS0117).
- **Reconciler's changes** (critic C1 and M8), not in the drafter's measured runs: the fake names became Task 1's `PickFolderResult` and `PickFolderDirs`; the panel's window-level Esc handler (T2-R14) and `AssetLibraryPanelTests.Esc_anywhere_in_the_window_cancels_a_drag` (+1). See the plan's count note for where they were compiled.
  - T2-R3: zero-height first-pass rows defeat virtualization (45 rows realized).
  - In the standalone panel, the root visual is the panel itself, so the 70% clamp must come from the window, never from the root (a first draft clamped at `0.7 x 123.5 = 86.5` and failed `The_handle_resizes...`).
  - `.NET` `StringComparer.InvariantCulture` (ICU) ordered all 13 sample names exactly as node's `localeCompare`.
  - `BitmapSource.Create` plus `Freeze` on a thread-pool thread gives a bitmap the UI thread binds (the real-loader tests run the worker on the pool).

**Counts.** Cumulative from Task 1's end (Engine.Tests 885, App.Tests 715). The drafter measured the deltas on a copy without Task 1's tests; the reconciler added `AssetLibraryPanelTests.Esc_anywhere_in_the_window_cancels_a_drag` (+1, critic M8). Parity.Tests stays 35.

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| Task 1 (Step 1 baseline) | 885 | 715 | |
| Step 3 | 923 | 715 | AssetScanTests +38 |
| Step 5 | 938 | 715 | ImagesCapsTests +7, ImagesOrientationTests +2, LayerOpsTests +6 |
| Step 8 | 938 | 726 | PackSourceTests +4, AssetThumbnailLoaderTests +6, ImportServiceTests +1 |
| Step 11 | 938 | 751 | AssetLibraryViewModelTests +25 |
| **Step 14** | **938** | **770** | AssetLibraryPanelTests +19; ThemeKeyGuardTests and LayerPropertiesPanelTests ±0 |

- [ ] **Step 1: Baseline and the Task 1 surfaces this task builds on**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 885, Failed: 0, Skipped: 0`; App.Tests `Passed: 715, Failed: 0`; Parity.Tests `Passed: 35, Failed: 0`.

Run: `grep -n "PickFolder" src/RBX15.App/Services/IFileDialogs.cs src/RBX15.App.Tests/Fakes/FakeFileDialogs.cs`
Expected: the interface member, and in the fake `PickFolder`, `PickFolderResult` and `PickFolderDirs` (Task 1 Step 7's names).

Run: `grep -n "PackNotFound\|PackEmpty\|NoImagesInFolder" src/RBX15.App/Services/DialogText.cs`
Expected: three lines.

Run: `grep -n "ISettingsStore settings" src/RBX15.App/Composition.cs`
Expected: one line, the injectable `Build` overload.

If any check finds nothing, stop and report: those are Task 1's, and this task's code calls them.

- [ ] **Step 2: Write the failing engine tests for the scan, dedupe and grouping**

Ledger: `asset-dedupe-by-name`, `asset-folder-grouping`, `asset-count-label`, `asset-count-label-overwritten`, `asset-thumb-uuid-label`, `io-folder-picker`, `io-image-formats`, `packs-load-builtin`, `pack-builtin-load`, `pack-load-sync-and-mime`. Create `src/RBX15.Engine.Tests/AssetScanTests.cs`:

```csharp
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The asset library's pure logic (editor.js:2339-2450, :2452-2484, :2544). Fixture paths stand for a folder picked
/// as "root", so RelativePath segment 0 is "root", as Chromium's webkitRelativePath. Expected orders that depend on
/// JavaScript were printed by node from 4.1's own expressions.
/// </summary>
public class AssetScanTests
{
    static (string FullPath, string RelativePath) F(string relative) => (@"C:\pick\" + relative.Replace('/', '\\'), relative);

    static string[] Paths(AssetSet set) => set.Entries.Select(e => e.RelativePath).ToArray();

    [Fact] // R14: import's formats, so BMP is listed too (4.1: png|jpg|jpeg|gif|webp, editor.js:2382)
    public void The_listed_extensions_are_imports()
    {
        Assert.Equal([".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"], AssetScan.Extensions);
    }

    [Theory] // /\.(png|jpg|jpeg|gif|webp)$/i plus .bmp: case-insensitive, the last extension only
    [InlineData("a.png", true)]
    [InlineData("A.PNG", true)]
    [InlineData("b.Jpeg", true)]
    [InlineData("c.jpg", true)]
    [InlineData("d.webp", true)]
    [InlineData("e.gif", true)]
    [InlineData("f.bmp", true)]
    [InlineData(".png", true)]
    [InlineData("g.svg", false)]
    [InlineData("h.txt", false)]
    [InlineData("png", false)]
    [InlineData("i.png.txt", false)]
    [InlineData("README.md", false)]
    public void Image_names_are_filtered_by_extension(string name, bool expected) => Assert.Equal(expected, AssetScan.IsImageName(name));

    [Fact] // asset-dedupe-by-name parityCheck: four x.png at depths 2-5, only the deepest remains, and the label says so
    public void The_deepest_copy_of_a_name_wins()
    {
        var set = AssetScan.FromFolder([F("root/a/x.png"), F("root/a/sub/x.png"), F("root/b/x.png"), F("root/a/sub/deep/x.png")]);
        Assert.Equal(["root/a/sub/deep/x.png"], Paths(set));
        Assert.Equal(3, set.DuplicatesRemoved);
        Assert.Equal("1 unique images (3 duplicates removed)", set.Summary);     // editor.js:2448 (asset-count-label, fix)
        Assert.Equal(@"C:\pick\root\a\sub\deep\x.png", set.Entries[0].FullPath);
    }

    [Fact] // a tie keeps the first in sorted order; a deeper copy takes the first-seen name's position (a JS Map's set)
    public void A_tie_keeps_the_first_and_a_deeper_copy_keeps_the_names_position()
    {
        var set = AssetScan.FromFolder([F("root/z/a.png"), F("root/b.png"), F("root/a.png"), F("root/y/q.png"), F("root/c/q.png")]);
        // Sorted: root/a.png, root/b.png, root/c/q.png, root/y/q.png, root/z/a.png. a.png is first seen at depth 2 and
        // replaced by root/z/a.png (depth 3) in its slot; c/q and y/q tie at depth 3, so c/q stays.
        Assert.Equal(["root/z/a.png", "root/b.png", "root/c/q.png"], Paths(set));
        Assert.Equal(2, set.DuplicatesRemoved);
    }

    [Fact] // asset-dedupe-by-name quirk: the Map key is case-sensitive, so X.png and x.png are two images
    public void Names_differing_only_in_case_are_both_kept()
    {
        var set = AssetScan.FromFolder([F("root/A/X.png"), F("root/B/x.png")]);
        Assert.Equal(["root/A/X.png", "root/B/x.png"], Paths(set));
        Assert.Equal(0, set.DuplicatesRemoved);
    }

    [Fact] // io-folder-picker (fix): the input order never matters; non-images never count as duplicates
    public void The_result_is_the_same_for_any_input_order_and_non_images_are_dropped()
    {
        (string, string)[] files = [F("root/b/2.png"), F("root/notes.txt"), F("root/a/1.PNG"), F("root/b/logo.svg"), F("root/a/2.png")];
        var forward = AssetScan.FromFolder(files);
        var backward = AssetScan.FromFolder(files.Reverse());
        Assert.Equal(["root/a/1.PNG", "root/a/2.png"], Paths(forward));
        Assert.Equal(Paths(forward), Paths(backward));
        Assert.Equal(1, forward.DuplicatesRemoved);  // b/2.png ties a/2.png at depth 3; a/ sorts first
        Assert.Equal("2 unique images (1 duplicates removed)", forward.Summary);
    }

    [Fact] // a folder with no images: no entries (the caller shows the no-images alert, editor.js:2384-2386)
    public void A_folder_without_images_has_no_entries()
    {
        var set = AssetScan.FromFolder([F("root/readme.txt"), F("root/art.svg")]);
        Assert.Empty(set.Entries);
        Assert.Equal(0, set.DuplicatesRemoved);
    }

    [Fact] // asset-folder-grouping parityCheck: root files, three subfolders, two same-named leaves merged; order, counts, labels
    public void Groups_are_immediate_parents_in_4_1s_order_with_labels()
    {
        var set = AssetScan.FromFolder([
            F("root/top.png"),
            F("root/b/1.png"),
            F("root/x/Cool/2.png"),
            F("root/y/Cool/3.png"),
            F("root/A/4.png"),
            F("root/_x/5.png"),
        ]);
        Assert.Equal(["(all)", "(root)", "_x", "A", "b", "Cool"], set.Groups.Select(g => g.Key));
        Assert.Equal(["All (6)", "Root (1)", "_x (1)", "A (1)", "b (1)", "Cool (2)"], set.Groups.Select(g => g.Label));
        Assert.True(set.ShowGroupButtons);
        Assert.Equal(["root/x/Cool/2.png", "root/y/Cool/3.png"], set.Groups[5].Entries.Select(e => e.RelativePath));
        Assert.Equal(set.Entries, set.Groups[0].Entries);
        Assert.Equal("(root)", set.Entries.Single(e => e.FileName == "top.png").Group);
    }

    [Fact] // a.localeCompare(b) in node 22 (ICU): ["_x","10","9","a","A","b","cool","Cool","e","é","f","zeta","Zeta"]
    public void Folder_groups_sort_as_javascripts_localeCompare()
    {
        string[] names = ["b", "A", "a", "_x", "10", "9", "Zeta", "zeta", "é", "e", "f", "Cool", "cool"];
        var set = AssetScan.FromFolder(names.Select((n, i) => F($"root/{n}/{i}.png")));
        Assert.Equal(["(all)", "_x", "10", "9", "a", "A", "b", "cool", "Cool", "e", "é", "f", "zeta", "Zeta"], set.Groups.Select(g => g.Key));
    }

    [Theory] // buttons only with more than two groups (editor.js:2432): one subfolder alone, or root files alone, shows none
    [InlineData(new[] { "root/sub/1.png", "root/sub/2.png" }, false)]
    [InlineData(new[] { "root/1.png", "root/2.png" }, false)]
    [InlineData(new[] { "root/1.png", "root/sub/2.png" }, true)]
    [InlineData(new[] { "root/s/1.png", "root/t/2.png" }, true)]
    public void Group_buttons_show_only_past_two_groups(string[] paths, bool expected) =>
        Assert.Equal(expected, AssetScan.FromFolder(paths.Select(F)).ShowGroupButtons);

    [Fact] // a subfolder named "(all)" is overwritten by the all-files entry in 4.1's map (editor.js:2419)
    public void A_subfolder_named_all_does_not_make_a_second_all_group()
    {
        var set = AssetScan.FromFolder([F("root/(all)/1.png"), F("root/s/2.png")]);
        Assert.Equal(["(all)", "s"], set.Groups.Select(g => g.Key));
        Assert.Equal(2, set.Groups[0].Entries.Length);
    }

    [Fact] // packs-load-builtin (fix): the pack's own summary, sorted ordinal-ignore-case, no dedupe, no buttons
    public void A_pack_is_sorted_by_name_with_its_own_summary_and_no_buttons()
    {
        var set = AssetScan.FromPack("drips", [@"D:\p\drips\b.png", @"D:\p\drips\README.md", @"D:\p\drips\A.png", @"D:\p\drips\_c.png"]);
        Assert.Equal(["A.png", "b.png", "_c.png"], set.Entries.Select(e => e.FileName));   // OrdinalIgnoreCase: 'B' (0x42) < '_' (0x5F)
        Assert.Equal(["packs/drips/A.png", "packs/drips/b.png", "packs/drips/_c.png"], Paths(set));
        Assert.Equal(["(all)", "drips"], set.Groups.Select(g => g.Key));
        Assert.False(set.ShowGroupButtons);
        Assert.Equal(0, set.DuplicatesRemoved);
        Assert.Equal("3 images in built-in drips pack", set.Summary);   // editor.js:2373
        Assert.Equal("drips", set.Entries[0].Group);
    }

    [Fact] // pack-builtin-load: the shipped packs list 156 and 25 in readdir (NTFS) order, first and last as node printed
    public void The_shipped_packs_sort_as_4_1_listed_them()
    {
        string packs = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "packs");
        var drips = AssetScan.FromPack("drips", Directory.GetFiles(Path.Combine(packs, "drips")));
        var words = AssetScan.FromPack("word_art", Directory.GetFiles(Path.Combine(packs, "word_art")));
        Assert.Equal(156, drips.Entries.Length);
        Assert.Equal(25, words.Entries.Length);
        Assert.Equal(("drip_blood_front_flipped.png", "drip_blood_front_v1.png", "drip_white_wide_v2.png"),
            (drips.Entries[0].FileName, drips.Entries[1].FileName, drips.Entries[^1].FileName));
        Assert.Equal(("word_626_anton.png", "word_w_anton.png"), (words.Entries[0].FileName, words.Entries[^1].FileName));
        Assert.Equal("25 images in built-in word_art pack", words.Summary);   // the folder token, not "word art"
    }

    [Theory] // asset-thumb-uuid-label: the caption regex strips 5 extensions only; a UUID-looking name shows its parent
    [InlineData("x.png", "root/x.png", "x")]
    [InlineData("name.webp.PNG", "root/a/name.webp.PNG", "name.webp")]
    [InlineData("photo.bmp", "root/photo.bmp", "photo.bmp")]
    [InlineData("1a2b3c4d-1111-2222-3333-444455556666.png", "root/x/Cool/1a2b3c4d-1111-2222-3333-444455556666.png", "Cool")]
    [InlineData("1A2B3C4D-AAAA-rest.jpg", "root/Shots/1A2B3C4D-AAAA-rest.jpg", "Shots")]
    [InlineData("1a2b3c4d-1111-2222.png", "root/1a2b3c4d-1111-2222.png", "1a2b3c4d-1111-2222")]
    [InlineData("1a2b3c4d-111.png", "root/x/1a2b3c4d-111.png", "1a2b3c4d-111")]
    [InlineData("g1a2b3c4-1111.png", "root/x/g1a2b3c4-1111.png", "g1a2b3c4-1111")]
    public void Captions_use_the_parent_for_uuid_names(string name, string relative, string expected) =>
        Assert.Equal(expected, AssetScan.Caption(name, relative));

    [Fact] // the entry keeps both names: the caption may be the folder, the layer is always the file (asset-thumb-uuid-label quirk)
    public void An_entry_carries_the_file_name_layer_name_and_caption()
    {
        var e = AssetScan.FromFolder([F("root/x/Cool/1a2b3c4d-1111-z.v2.png")]).Entries.Single();
        Assert.Equal(("1a2b3c4d-1111-z.v2.png", "1a2b3c4d-1111-z.v2", "Cool", "Cool"), (e.FileName, e.LayerName, e.Caption, e.Group));
    }

    [Fact] // showAssetFolder's label (editor.js:2544)
    public void The_count_text_names_all_folders_or_the_group_key()
    {
        var set = AssetScan.FromFolder([F("root/1.png"), F("root/s/2.png"), F("root/s/3.png")]);
        Assert.Equal("3 images in all folders", AssetScan.CountText(set.Groups[0]));
        Assert.Equal("1 images in (root)", AssetScan.CountText(set.Groups[1]));
        Assert.Equal("2 images in s", AssetScan.CountText(set.Groups[2]));
    }
}
```

Run: `dotnet build src/RBX15.Engine.Tests/RBX15.Engine.Tests.csproj -c Release -warnaserror`
Expected: the build fails with `CS0246` naming `AssetSet` (measured).

- [ ] **Step 3: Implement `AssetScan`**

Create `src/RBX15.Engine/AssetScan.cs`:

```csharp
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RBX15.Engine;

/// <summary>One image the asset browser lists (4.1: a File with its webkitRelativePath, editor.js:2354-2360, :2381).</summary>
/// <param name="FullPath">The absolute path on disk.</param>
/// <param name="RelativePath">'/'-separated; segment 0 is the picked root's name, as webkitRelativePath; packs: "packs/&lt;name&gt;/&lt;file&gt;" (editor.js:2357).</param>
/// <param name="FileName">The file name with its extension (f.name).</param>
/// <param name="LayerName">The file name minus its last extension: the layer name an add gives (editor.js:2490).</param>
/// <param name="Caption">The thumbnail caption (editor.js:2478-2484, ledger asset-thumb-uuid-label).</param>
/// <param name="Group">The immediate parent folder's name, or <see cref="AssetScan.RootKey"/> (editor.js:2403-2413).</param>
public sealed record AssetEntry(string FullPath, string RelativePath, string FileName, string LayerName, string Caption, string Group);

/// <summary>One group button: its key (the button's title, editor.js:2442), its label and its entries in browser order.</summary>
public sealed record AssetGroup(string Key, string Label, ImmutableArray<AssetEntry> Entries);

/// <summary>A loaded pack or folder: what the browser shows right after the load.</summary>
/// <param name="Entries">The unique entries in 4.1's Map order: first-seen name position, deepest copy (editor.js:2390-2398).</param>
/// <param name="Groups"><see cref="AssetScan.AllKey"/> first, <see cref="AssetScan.RootKey"/> second, the rest by culture order (editor.js:2424-2430).</param>
/// <param name="ShowGroupButtons">More than two groups, for a folder (editor.js:2432); never for a pack (editor.js:2372).</param>
/// <param name="DuplicatesRemoved">Image files dropped by the name dedupe (editor.js:2448).</param>
/// <param name="Summary">The count label right after the load: the text 4.1 set and then overwrote in the same tick (ledger asset-count-label-overwritten, fix).</param>
public sealed record AssetSet(ImmutableArray<AssetEntry> Entries, ImmutableArray<AssetGroup> Groups, bool ShowGroupButtons, int DuplicatesRemoved, string Summary);

/// <summary>
/// The asset library's folder and pack logic (editor.js:2339-2450 and :2452-2484) as pure functions over paths:
/// no file IO, so every rule is tested on fixture lists. The App enumerates the disk and hands the paths here.
/// </summary>
public static class AssetScan
{
    /// <summary>The "(all)" group key (editor.js:2419).</summary>
    public const string AllKey = "(all)";

    /// <summary>The group of files directly inside the picked folder (editor.js:2408-2409).</summary>
    public const string RootKey = "(root)";

    /// <summary>
    /// The listed extensions, lowercase, matched case-insensitively. 4.1's folder and pack filter is
    /// /\.(png|jpg|jpeg|gif|webp)$/i (editor.js:2348, :2382); v5 lists exactly what import accepts, so BMP is
    /// added (interfaces R14; ledger io-image-formats). Equal to RBX15.App.Services.ImageImporter.Extensions (pinned by an App test).
    /// </summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    static readonly Regex CaptionExtension = new(@"\.(png|jpg|jpeg|gif|webp)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex UuidPrefix = new("^[0-9a-f]{8}-[0-9a-f]{4}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>True when the name ends in one of <see cref="Extensions"/>, any case (editor.js:2382).</summary>
    public static bool IsImageName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return Extensions.Any(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A picked folder (folderInput.onchange, editor.js:2380-2450). The files are first sorted by RelativePath,
    /// ordinal ignoring case: 4.1 took Chromium's enumeration order, v5 makes it deterministic (io-folder-picker, fix).
    /// Then the image filter; then the dedupe by file name, ordinal (a JS Map key), where a strictly deeper copy
    /// replaces the kept one in its first-seen position and a tie keeps the first (editor.js:2390-2398,
    /// asset-dedupe-by-name, keep); then the groups by immediate parent (asset-folder-grouping, keep).
    /// </summary>
    public static AssetSet FromFolder(IEnumerable<(string FullPath, string RelativePath)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var images = files
            .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(f => (f.FullPath, f.RelativePath, FileName: LastSegment(f.RelativePath)))
            .Where(f => IsImageName(f.FileName))
            .ToList();

        var order = new List<string>();
        var byName = new Dictionary<string, (string FullPath, string RelativePath, int Depth)>(StringComparer.Ordinal);
        foreach (var f in images)
        {
            int depth = f.RelativePath.Split('/').Length;
            if (!byName.TryGetValue(f.FileName, out var kept)) order.Add(f.FileName);
            else if (depth <= kept.Depth) continue;
            byName[f.FileName] = (f.FullPath, f.RelativePath, depth);
        }
        var unique = order.Select(n => Entry(byName[n].FullPath, byName[n].RelativePath)).ToImmutableArray();

        var groups = new List<AssetGroup>();
        // A subfolder named "(all)" is overwritten by the (all) entry in 4.1's map (editor.js:2419), so it has no group.
        foreach (var key in unique.Select(e => e.Group).Where(k => k != AllKey).Distinct(StringComparer.Ordinal)
                     .OrderBy(k => k == RootKey ? 0 : 1)
                     .ThenBy(k => k, StringComparer.InvariantCulture))
            groups.Add(Group(key, unique.Where(e => e.Group == key).ToImmutableArray()));
        groups.Insert(0, Group(AllKey, unique));

        int removed = images.Count - unique.Length;
        return new AssetSet(unique, [.. groups], groups.Count > 2, removed,
            Invariant($"{unique.Length} unique images ({removed} duplicates removed)"));
    }

    /// <summary>
    /// A built-in pack (loadBuiltInPack, editor.js:2339-2375): the image files sorted by name, ordinal ignoring
    /// case (spec §3; 4.1 took readdir order, which NTFS returns sorted the same way). No dedupe. The groups are
    /// "(all)" and the pack (editor.js:2370), with no buttons (editor.js:2372). RelativePath is 4.1's fake
    /// webkitRelativePath, "packs/&lt;pack&gt;/&lt;file&gt;" (editor.js:2357).
    /// </summary>
    public static AssetSet FromPack(string packName, IEnumerable<string> fullPaths)
    {
        ArgumentException.ThrowIfNullOrEmpty(packName);
        ArgumentNullException.ThrowIfNull(fullPaths);
        var entries = fullPaths
            .Select(p => (FullPath: p, FileName: Path.GetFileName(p)))
            .Where(f => IsImageName(f.FileName))
            .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(f => Entry(f.FullPath, "packs/" + packName + "/" + f.FileName))
            .ToImmutableArray();
        return new AssetSet(entries, [Group(AllKey, entries), Group(packName, entries)], false, 0,
            Invariant($"{entries.Length} images in built-in {packName} pack"));
    }

    /// <summary>
    /// The thumbnail caption (editor.js:2480-2484): the name minus /\.(png|jpg|jpeg|gif|webp)$/i, verbatim (so a
    /// .bmp keeps its extension), or the immediate parent folder's name when that starts like a UUID
    /// (/^[0-9a-f]{8}-[0-9a-f]{4}/i) and the path has 3 or more segments.
    /// </summary>
    public static string Caption(string fileName, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(relativePath);
        string raw = CaptionExtension.Replace(fileName, "", 1);
        var parts = relativePath.Split('/');
        return parts.Length >= 3 && UuidPrefix.IsMatch(raw) ? parts[^2] : raw;
    }

    /// <summary>The count label after a group switch (showAssetFolder, editor.js:2544).</summary>
    public static string CountText(AssetGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return Invariant($"{group.Entries.Length} images in {(group.Key == AllKey ? "all folders" : group.Key)}");
    }

    static AssetEntry Entry(string fullPath, string relativePath)
    {
        string fileName = LastSegment(relativePath);
        var parts = relativePath.Split('/');
        return new AssetEntry(fullPath, relativePath, fileName, LayerName(fileName), Caption(fileName, relativePath),
            parts.Length <= 2 ? RootKey : parts[^2]);
    }

    /// <summary>
    /// <c>file.name.replace(/\.[^.]+$/, '')</c> (editor.js:2490): "a.b.png" gives "a.b", ".png" gives "", "file." stays.
    /// The same rule as RBX15.App.Services.ImageImporter.LayerName, which the add itself uses.
    /// </summary>
    static string LayerName(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        return dot >= 0 && dot < fileName.Length - 1 ? fileName[..dot] : fileName;
    }

    static string LastSegment(string relativePath) => relativePath[(relativePath.LastIndexOf('/') + 1)..];

    /// <summary>The button label (editor.js:2437-2440).</summary>
    static AssetGroup Group(string key, ImmutableArray<AssetEntry> entries) =>
        new(key, Invariant($"{(key == AllKey ? "All" : key == RootKey ? "Root" : key)} ({entries.Length})"), entries);

    static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
```

Run: `dotnet test src/RBX15.Engine.Tests/RBX15.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~AssetScanTests"`
Expected: `Passed: 38, Failed: 0`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 923`; App.Tests `Passed: 715`; Parity.Tests `Passed: 35`.

- [ ] **Step 4: Write the failing tests for the thumbnail decode and the full-size add**

Ledger: `asset-grid-thumbnails`, `asset-objecturl-lifecycle` (the decode), `asset-rightclick-full` (the add).

In `src/RBX15.Engine.Tests/ImagesCapsTests.cs`, insert before the class's final closing brace (the file already has `PngHeader` and `JpegHeader`):

```csharp
    // ---- DecodeThumbnail (M4a Task 2; ledger asset-grid-thumbnails, asset-objecturl-lifecycle) ----

    static byte[] Encoded(int w, int h, SKEncodedImageFormat format)
    {
        using var img = Px.Solid(w, h, new SKColor(200, 30, 60));
        using var data = img.Encode(format, 100);
        return data.ToArray();
    }

    [Theory] // the longer side lands on maxSide; a smaller image is never upscaled; JPEG and WebP take the codec's scaled decode
    [InlineData(400, 100, "png", 160, 40)]
    [InlineData(100, 400, "png", 40, 160)]
    [InlineData(1600, 1200, "jpeg", 160, 120)]
    [InlineData(1000, 500, "webp", 160, 80)]
    [InlineData(50, 30, "png", 50, 30)]
    [InlineData(160, 160, "png", 160, 160)]
    public void A_thumbnail_fits_the_longer_side_and_never_upscales(int w, int h, string format, int tw, int th)
    {
        var f = format switch { "jpeg" => SKEncodedImageFormat.Jpeg, "webp" => SKEncodedImageFormat.Webp, _ => SKEncodedImageFormat.Png };
        using var thumb = Images.DecodeThumbnail(Encoded(w, h, f), 160);
        Assert.Equal((tw, th), (thumb.Width, thumb.Height));
        Assert.Equal((SKImageInfo.PlatformColorType, SKAlphaType.Premul), (thumb.ColorType, thumb.AlphaType));
        Px.Near(new SKColor(200, 30, 60), Px.At(thumb, tw / 2, th / 2), 3);
    }

    [Fact] // the same caps as Decode, from the header, before any pixel memory
    public void A_thumbnail_is_capped_exactly_as_decode()
    {
        var side = Assert.Throws<ImageTooLargeException>(() => Images.DecodeThumbnail(PngHeader(20000, 20000), 160));
        Assert.Equal("The image is 20000 x 20000 pixels; the limit is 16384 pixels on a side.", side.Message);
        var pixels = Assert.Throws<ImageTooLargeException>(() => Images.DecodeThumbnail(JpegHeader(8000, 8000), 160));
        Assert.Equal("The image is 8000 x 8000 pixels (64 megapixels); the limit is 50 megapixels.", pixels.Message);
        Assert.Throws<ImageTooLargeException>(() => Images.DecodeThumbnail(new byte[Images.MaxEncodedBytes + 1], 160));
        Assert.Throws<InvalidDataException>(() => Images.DecodeThumbnail([1, 2, 3, 4], 160));
        Assert.Throws<ArgumentOutOfRangeException>(() => Images.DecodeThumbnail(Encoded(2, 2, SKEncodedImageFormat.Png), 0));
    }
```

In `src/RBX15.Engine.Tests/ImagesOrientationTests.cs`, insert before the class's final closing brace (it reuses the file's `JpegWithOrientation`):

```csharp
    [Theory] // the thumbnail is oriented like the full decode (M4a Task 2): EXIF 6 turns 32x16 into 16x32, white top-right
    [InlineData(1, 16, 8, "TL")]
    [InlineData(6, 8, 16, "TR")]
    public void A_thumbnail_applies_the_exif_orientation(int orientation, int w, int h, string corner)
    {
        using var img = Images.DecodeThumbnail(JpegWithOrientation(orientation), 16);
        Assert.Equal((w, h), (img.Width, img.Height));
        var (x, y) = corner == "TL" ? (1, 1) : (w - 2, 1);
        var px = Px.At(img, x, y);
        Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200, $"{corner} should be white, got {px}");
        var far = Px.At(img, corner == "TL" ? w - 2 : 1, h - 2);
        Assert.True(far.Red < 55, $"the opposite corner should be black, got {far}");
    }
```

In `src/RBX15.Engine.Tests/LayerOpsTests.cs`, insert before the class's final closing brace (it reuses `Img`, `L`, `Doc` and `Ids`):

```csharp
    [Theory] // asset-rightclick-full (keep): max(585/w, 559/h), 4.1's doubles as node printed them (editor.js:2525); unclamped (R15)
    [InlineData(390, 200, 2.795)]
    [InlineData(128, 150, 4.5703125)]
    [InlineData(64, 130, 9.140625)]
    [InlineData(684, 133, 4.203007518796992)]
    [InlineData(1, 1, 585)]
    public void Add_full_covers_the_template_from_the_top_left(int w, int h, double scale)
    {
        var start = new EditorSnapshot(Doc(L(1)), 1);
        var s = LayerOps.AddFull(start, Img(w, h), 2, "drip");
        var l = s.Document.Layers[^1];
        Assert.Equal((2, "drip (full)", 0.0, 0.0, scale), (l.Id, l.Name, l.X, l.Y, l.Scale));
        Assert.Equal((true, 1.0, 0.0, false, false), (l.Visible, l.Opacity, l.Rotation, l.FlipH, l.FlipV));
        Assert.Equal((100.0, 100.0, 100.0, 0.0, 0, 0, 0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate, l.RShift, l.GShift, l.BShift));
        Assert.Equal(2, s.SelectedLayerId);                  // pushed on top and selected (editor.js:2530-2531)
        Assert.Equal([1, 2], Ids(s.Document));
        Assert.Throws<ArgumentException>(() => LayerOps.AddFull(s, Img(w, h), 2, "again"));   // ids are never reused
    }

    [Fact] // displayName + ' (full)' with an empty name is " (full)", as 4.1's concatenation
    public void Add_full_keeps_an_empty_base_name() =>
        Assert.Equal(" (full)", LayerOps.AddFull(new EditorSnapshot(Doc(), null), Img(4, 4), 1, "").Document.Layers[0].Name);
```

Run: `dotnet build src/RBX15.Engine.Tests/RBX15.Engine.Tests.csproj -c Release -warnaserror`
Expected: the build fails with `CS0117`: `Images` has no `DecodeThumbnail`, and `LayerOps` has no `AddFull`.

- [ ] **Step 5: Implement `Images.DecodeThumbnail` and `LayerOps.AddFull`**

In `src/RBX15.Engine/Images.cs`, insert directly above the `/// <summary>` of `static SKImage Orient(SKBitmap source, SKEncodedOrigin origin)` (it calls `Orient`, `CheckEncodedSize` and `CheckDimensions`):

```csharp
    /// <summary>
    /// An asset-browser thumbnail (spec §3 "Thumbnails decode lazily at thumbnail size"; ledger asset-grid-thumbnails and
    /// asset-objecturl-lifecycle, fix: 4.1 decoded every file at full size through a blob: URL, editor.js:2475). The same
    /// caps as <see cref="Decode"/>, in the same order: the payload length, then the codec header's dimensions, before
    /// any pixel memory. JPEG and WebP decode straight at a reduced size where the codec supports it
    /// (<see cref="SKCodec.GetScaledDimensions"/>); PNG, GIF and BMP decode full size. The EXIF orientation is applied,
    /// then one Linear + mipmap draw scales the longer side down to <paramref name="maxSide"/> pixels; a smaller image
    /// is never upscaled (an img with max-width/max-height 100% shows a small file at its own size, editor.js:2474).
    /// Returns a premultiplied platform-native (kN32) image the caller owns. Throws <see cref="ImageTooLargeException"/>
    /// and <see cref="InvalidDataException"/> exactly as <see cref="Decode"/>. Thread-safe: it touches no shared state.
    /// </summary>
    public static SKImage DecodeThumbnail(byte[] encoded, int maxSide)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSide, 1);
        CheckEncodedSize(encoded.LongLength);
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("not a supported image");
        CheckDimensions(codec.Info.Width, codec.Info.Height);
        float want = Math.Min(1f, (float)maxSide / Math.Max(codec.Info.Width, codec.Info.Height));
        var size = codec.GetScaledDimensions(want);
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException($"image decode failed: {result}");
        using var oriented = codec.EncodedOrigin == SKEncodedOrigin.TopLeft
            ? SKImage.FromPixelCopy(bmp.PeekPixels())
            : Orient(bmp, codec.EncodedOrigin);
        double s = Math.Min(1.0, (double)maxSide / Math.Max(oriented.Width, oriented.Height));
        int w = Math.Max(1, (int)Math.Round(oriented.Width * s)), h = Math.Max(1, (int)Math.Round(oriented.Height * s));
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKImageInfo.PlatformColorType, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("cannot create thumbnail surface");
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        surface.Canvas.DrawImage(oriented, SKRect.Create(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        return surface.Snapshot();
    }
```

In `src/RBX15.Engine/LayerOps.cs`, insert directly above the `/// <summary>` of `Remove` (the doc line starts `/// removeLayer (editor.js:562-568)`):

```csharp
    /// <summary>
    /// The asset browser's right-click add (thumb.oncontextmenu, editor.js:2513-2539; ledger asset-rightclick-full, keep):
    /// named <paramref name="baseName"/> + " (full)", at X = Y = 0, scaled to cover the template,
    /// max(585 / w, 559 / h) (editor.js:2525), with every other field at its default (editor.js:2523-2528); pushed on
    /// top and selected (editor.js:2530-2531). The scale is not clamped to 500%: 4.1 stored the raw cover scale and v5
    /// keeps stored values raw (interfaces R15, R12). An empty base name stays empty, as 4.1's concatenation.
    /// </summary>
    public static EditorSnapshot AddFull(EditorSnapshot s, SKImage image, int id, string baseName)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(baseName);
        RequireFreshId(s.Document, id);
        var layer = new Layer
        {
            Id = id,
            Image = image,
            Name = baseName + " (full)",
            X = 0,
            Y = 0,
            Scale = Math.Max((double)TemplateSize.Width / image.Width, (double)TemplateSize.Height / image.Height),
        };
        return new EditorSnapshot(s.Document with { Layers = s.Document.Layers.Add(layer) }, id);
    }
```

Run: `dotnet test src/RBX15.Engine.Tests/RBX15.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~ImagesCapsTests|FullyQualifiedName~ImagesOrientationTests|FullyQualifiedName~LayerOpsTests|FullyQualifiedName~AssetScanTests"`
Expected: `Failed: 0`; the four classes run 22, 10, 36 and 38 (measured on main plus this step).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 938`; App.Tests `Passed: 715`; Parity.Tests `Passed: 35`.

Run: `grep -rn --include=*.cs --include=*.csproj "System.Windows\|PresentationCore\|UseWPF" src/RBX15.Engine`
Expected: no output (the engine never references WPF; the include filters skip the build's pdb files, which match).

- [ ] **Step 6: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/AssetScan.cs src/RBX15.Engine/Images.cs src/RBX15.Engine/LayerOps.cs src/RBX15.Engine.Tests/AssetScanTests.cs src/RBX15.Engine.Tests/ImagesCapsTests.cs src/RBX15.Engine.Tests/ImagesOrientationTests.cs src/RBX15.Engine.Tests/LayerOpsTests.cs
git commit -m "feat(engine): asset scan, dedupe and grouping, thumbnail decode and the full-size add"
```

- [ ] **Step 7: Write the failing tests for the packs, the thumbnail loader and `ImportFull`**

Ledger: `pack-contents`, `packs-inventory-drips`, `packs-inventory-wordart`, `packs-resolve-dir` (PackSource); `asset-grid-thumbnails`, `asset-objecturl-lifecycle`, `pack-load-sync-and-mime` (the loader); `asset-rightclick-full` (ImportFull).

Create `src/RBX15.App.Tests/PackSourceTests.cs`:

```csharp
using System.Security.Cryptography;
using RBX15.App.Services;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The built-in packs ship in the build output byte for byte (ledger pack-contents, packs-inventory-drips,
/// packs-inventory-wordart) and resolve from one place (packs-resolve-dir; resolvePackDir, editor.js:2325-2333).
/// </summary>
public class PackSourceTests
{
    static readonly string RepoPacks = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "packs");

    static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [Theory] // pack-contents parityCheck: 156 and 25 files, each SHA-256 equal to the repo's; only the PNGs (package.json extraResources **/*.png)
    [InlineData(PackSource.Drips, 156)]
    [InlineData(PackSource.WordArt, 25)]
    public void The_output_carries_each_pack_byte_for_byte(string pack, int count)
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "packs"), PackSource.DefaultDirectory);
        var shipped = new PackSource(PackSource.DefaultDirectory).Files(pack)!;
        var repo = Directory.GetFiles(Path.Combine(RepoPacks, pack), "*.png");
        Assert.Equal(count, shipped.Count);
        Assert.Equal(count, repo.Length);
        Assert.Equal(
            repo.Select(p => (Path.GetFileName(p), Sha(p))).OrderBy(x => x.Item1, StringComparer.Ordinal),
            shipped.Select(p => (Path.GetFileName(p), Sha(p))).OrderBy(x => x.Item1, StringComparer.Ordinal));
        Assert.False(File.Exists(Path.Combine(PackSource.DefaultDirectory, "README.md")));
    }

    [Fact] // 4.1's folder names, which the count label shows (editor.js:2373, :2377-2378)
    public void The_pack_names_are_4_1s_folder_names()
    {
        Assert.Equal("drips", PackSource.Drips);
        Assert.Equal("word_art", PackSource.WordArt);
    }

    [Fact] // a missing pack is null (the not-found alert); a pack lists only its own top-level files, unfiltered
    public void Files_lists_the_top_level_of_a_pack_or_null_when_it_is_missing()
    {
        using var files = new TestFiles();
        Directory.CreateDirectory(Path.Combine(files.Dir, "p", "nested"));
        File.WriteAllBytes(Path.Combine(files.Dir, "p", "a.png"), [1]);
        File.WriteAllBytes(Path.Combine(files.Dir, "p", "notes.txt"), [1]);
        File.WriteAllBytes(Path.Combine(files.Dir, "p", "nested", "deep.png"), [1]);
        var source = new PackSource(files.Dir);
        Assert.Null(source.Files("missing"));
        Assert.Equal(["a.png", "notes.txt"], source.Files("p")!.Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }
}
```

Create `src/RBX15.App.Tests/AssetThumbnailLoaderTests.cs`:

```csharp
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Services;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Thumbnails decode on one background worker, newest first, at 160 px through the engine caps (interfaces R5;
/// ledger asset-grid-thumbnails, asset-objecturl-lifecycle, pack-load-sync-and-mime).
/// </summary>
public class AssetThumbnailLoaderTests
{
    [Fact] // newest first: rows scrolled to load before rows scrolled past
    public async Task The_newest_request_is_decoded_first()
    {
        using var gate = new ManualResetEventSlim();
        var order = new ConcurrentQueue<string>();
        using var loader = new AssetThumbnailLoader(p =>
        {
            order.Enqueue(p);
            if (p == "a") gate.Wait(TimeSpan.FromSeconds(10));
            return null;
        });
        var a = loader.LoadAsync("a", default);
        Assert.True(SpinWait.SpinUntil(() => !order.IsEmpty, TimeSpan.FromSeconds(10)));   // the worker is inside "a"
        var rest = new[] { "b", "c", "d" }.Select(p => loader.LoadAsync(p, default)).ToArray();
        gate.Set();
        await Task.WhenAll(rest.Prepend(a));
        Assert.Equal(["a", "d", "c", "b"], order);
    }

    [Fact] // a request cancelled while queued ends cancelled and is never decoded (a newer set replaced it)
    public async Task A_request_cancelled_while_queued_is_never_decoded()
    {
        using var gate = new ManualResetEventSlim();
        var decoded = new ConcurrentQueue<string>();
        using var loader = new AssetThumbnailLoader(p =>
        {
            decoded.Enqueue(p);
            if (p == "a") gate.Wait(TimeSpan.FromSeconds(10));
            return null;
        });
        using var cts = new CancellationTokenSource();
        var a = loader.LoadAsync("a", default);
        Assert.True(SpinWait.SpinUntil(() => !decoded.IsEmpty, TimeSpan.FromSeconds(10)));
        var b = loader.LoadAsync("b", cts.Token);
        cts.Cancel();
        gate.Set();
        await a;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);
        Assert.Equal(["a"], decoded);
        Assert.True(loader.LoadAsync("c", cts.Token).IsCanceled);   // already cancelled: never queued
    }

    [Fact] // the real decode, off the UI thread: frozen Pbgra32 at 96 DPI, the longer side at 160
    public async Task A_file_decodes_to_a_frozen_thumbnail_at_160()
    {
        using var files = new TestFiles();
        using var loader = new AssetThumbnailLoader();
        var thumb = await loader.LoadAsync(files.Image("wide.png", 400, 100, SKColors.Red), default);
        Assert.NotNull(thumb);
        Assert.True(thumb.IsFrozen);
        Assert.Equal((160, 40, 96.0, PixelFormats.Pbgra32), (thumb.PixelWidth, thumb.PixelHeight, thumb.DpiX, thumb.Format));
        var small = await loader.LoadAsync(files.Image("small.png", 50, 30, SKColors.Blue), default);
        Assert.Equal((50, 30), (small!.PixelWidth, small.PixelHeight));   // never upscaled: it shows at its own size
        var px = new byte[4];
        small.CopyPixels(new System.Windows.Int32Rect(25, 15, 1, 1), px, 4, 0);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px);   // BGRA: blue
    }

    [Fact] // over a cap, damaged or gone: null, and the worker goes on to the next request
    public async Task A_failed_decode_is_null_and_the_worker_goes_on()
    {
        using var files = new TestFiles();
        using var loader = new AssetThumbnailLoader();
        Assert.Null(await loader.LoadAsync(files.Write("bomb.png", TestFiles.PngHeader(30000, 30000)), default));
        Assert.Null(await loader.LoadAsync(files.Write("junk.png", [1, 2, 3, 4]), default));
        Assert.Null(await loader.LoadAsync(Path.Combine(files.Dir, "gone.png"), default));
        Assert.NotNull(await loader.LoadAsync(files.Image("fine.png", 8, 8, SKColors.Lime), default));
        using var throwing = new AssetThumbnailLoader(_ => throw new InvalidOperationException("skia"));
        Assert.Null(await throwing.LoadAsync("x", default));
        Assert.Null(await throwing.LoadAsync("y", default));
    }

    [Fact] // Dispose cancels what is queued; nothing more is accepted
    public async Task Dispose_cancels_queued_requests()
    {
        using var gate = new ManualResetEventSlim();
        var started = new ManualResetEventSlim();
        var loader = new AssetThumbnailLoader(p =>
        {
            started.Set();
            gate.Wait(TimeSpan.FromSeconds(10));
            return null;
        });
        var a = loader.LoadAsync("a", default);
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        var b = loader.LoadAsync("b", default);
        loader.Dispose();
        gate.Set();
        Assert.Null(await a);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);
        Assert.Throws<ObjectDisposedException>(() => { _ = loader.LoadAsync("c", default); });
        started.Dispose();
    }

    [Fact] // the bitmap a tile shows is a straight copy of the engine image, premultiplied BGRA
    public void To_bitmap_source_copies_the_pixels()
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(new SKColor(255, 0, 0, 128));
        using var image = surface.Snapshot();
        BitmapSource b = AssetThumbnailLoader.ToBitmapSource(image);
        var px = new byte[8];
        b.CopyPixels(px, 8, 0);
        Assert.Equal(new byte[] { 0, 0, 128, 128, 0, 0, 128, 128 }, px);
    }
}
```

In `src/RBX15.App.Tests/ImportServiceTests.cs`, insert before the class's final closing brace:

```csharp
    [Fact] // asset-rightclick-full: one file at full size, one step, on top and selected, LayersAdded raised; a failure is named
    public void Import_full_adds_one_cover_layer_as_one_step_or_names_the_failure()
    {
        using var files = new TestFiles();
        var session = new EditorSession();
        var status = new FakeStatus();
        var import = new ImportService(session, status);
        int raised = 0;
        import.LayersAdded += (_, _) => raised++;
        Assert.Equal(1, import.ImportFull(files.Image("wide.png", 390, 200, SKColors.Red)));
        var l = session.SelectedLayer!;
        Assert.Equal(("wide (full)", 0.0, 0.0, 2.795), (l.Name, l.X, l.Y, l.Scale));
        Assert.Equal((1, 1, 1), (session.UndoCount, raised, status.Clears));

        Assert.Equal(0, import.ImportFull(files.Write("logo.svg", [0x3C])));
        Assert.Equal("Couldn't add logo.svg: SVG images can't be added. Use PNG, JPEG, WebP, BMP or GIF.", status.Current);
        Assert.Equal((1, 1), (session.UndoCount, raised));
    }
```

Run: `dotnet build src/RBX15.App.Tests/RBX15.App.Tests.csproj -c Release -warnaserror`
Expected: the build fails with `CS0246` (`PackSource`, `AssetThumbnailLoader`) and `CS1061` (`ImportFull`).

- [ ] **Step 8: Ship the packs, and implement `PackSource`, `AssetThumbnailLoader` and `ImportFull`**

In `src/RBX15.App/RBX15.App.csproj`, insert directly above the comment that starts `<!-- The 3 UI families`:

```xml
  <!-- The built-in packs (ledger packs-inventory-drips, packs-inventory-wordart, pack-contents, packs-resolve-dir): the
       repo's packs/**/*.png, byte for byte, at packs/<name>/<file> in every output that references the App, as 4.1's
       extraResources filter **/*.png shipped them (package.json:31-38); README.md stays out. PackSource reads them. -->
  <ItemGroup>
    <Content Include="..\..\packs\**\*.png" LinkBase="packs" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

```

Create `src/RBX15.App/Services/PackSource.cs`:

```csharp
using System.IO;

namespace RBX15.App.Services;

/// <summary>
/// The two built-in packs on disk (resolvePackDir, editor.js:2325-2333; ledger packs-resolve-dir, keep). 4.1 looked
/// next to editor.js, then in resources/packs; v5 ships them in its own output at <c>packs/&lt;name&gt;</c> (the App
/// project's Content items, copied from the repo's packs/ folder), so there is one place to look, in development and
/// installed alike. The pack names are 4.1's folder names, which the count label shows (editor.js:2373).
/// </summary>
public sealed class PackSource(string packsDirectory)
{
    /// <summary>btnPackDrips (editor.js:2377).</summary>
    public const string Drips = "drips";

    /// <summary>btnPackWordArt (editor.js:2378).</summary>
    public const string WordArt = "word_art";

    /// <summary>The build output's packs folder.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "packs");

    readonly string _directory = packsDirectory ?? throw new ArgumentNullException(nameof(packsDirectory));

    /// <summary>
    /// Every file directly in <c>&lt;packsDirectory&gt;/&lt;packName&gt;</c>, unsorted and unfiltered (AssetScan.FromPack sorts
    /// and filters, as readdirSync then filter did, editor.js:2347-2348). Null when the folder is missing or cannot be
    /// listed: 4.1's not-found case, and a broken install either way.
    /// </summary>
    public IReadOnlyList<string>? Files(string packName)
    {
        ArgumentException.ThrowIfNullOrEmpty(packName);
        string dir = Path.Combine(_directory, packName);
        if (!Directory.Exists(dir)) return null;
        try
        {
            return Directory.GetFiles(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
```

Create `src/RBX15.App/Services/AssetThumbnailLoader.cs`:

```csharp
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>
/// Decodes asset-browser thumbnails off the UI thread (spec §3; ledger asset-grid-thumbnails, asset-objecturl-lifecycle,
/// pack-load-sync-and-mime, all fix: 4.1 read every pack file synchronously and decoded every image at full size,
/// editor.js:2347-2353, :2475). One background worker takes the newest request first, so the rows the user has just
/// scrolled to load before the ones scrolled past. Each file goes through the engine's decode caps
/// (<see cref="Images.CheckEncodedSize"/> on the file length before reading, then <see cref="Images.DecodeThumbnail"/>)
/// and comes back as a frozen Pbgra32 <see cref="BitmapSource"/> at 96 DPI, so a file smaller than
/// <see cref="PixelSide"/> shows at its own size in DIPs, as 4.1's img did at its own CSS size.
/// </summary>
public sealed class AssetThumbnailLoader : IDisposable
{
    /// <summary>The thumbnail's longer side in pixels: two device pixels per DIP of the widest tile (81 DIP in the 280 panel).</summary>
    public const int PixelSide = 160;

    readonly Func<string, BitmapSource?> _decode;
    readonly object _gate = new();
    readonly List<Request> _pending = [];
    bool _running;
    bool _disposed;

    public AssetThumbnailLoader() : this(Decode) { }

    /// <summary>Tests pass a decode that blocks or records, to observe the order and the cancellation.</summary>
    internal AssetThumbnailLoader(Func<string, BitmapSource?> decode) => _decode = decode ?? throw new ArgumentNullException(nameof(decode));

    /// <summary>
    /// Queues <paramref name="path"/>. The task gives the thumbnail, or null when the file is over a cap, cannot be
    /// decoded or cannot be read (or the decode fails in any other way: a thumbnail never stops the worker). A request
    /// cancelled before the worker reaches it ends cancelled and is never decoded.
    /// </summary>
    public Task<BitmapSource?> LoadAsync(string path, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (cancel.IsCancellationRequested) return Task.FromCanceled<BitmapSource?>(cancel);
        var request = new Request(path, cancel);
        bool start;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pending.Add(request);
            start = !_running;
            _running = true;
        }
        if (start) _ = Task.Run(Drain);
        return request.Done.Task;
    }

    void Drain()
    {
        while (true)
        {
            Request next;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _running = false;
                    return;
                }
                next = _pending[^1];
                _pending.RemoveAt(_pending.Count - 1);
            }
            if (next.Cancel.IsCancellationRequested)
            {
                next.Done.TrySetCanceled(next.Cancel);
                continue;
            }
            BitmapSource? result;
            try
            {
                result = _decode(next.Path);
            }
            catch (Exception)
            {
                result = null;   // a cap, a damaged file, a vanished file or a Skia failure: the tile shows no picture
            }
            next.Done.TrySetResult(result);
        }
    }

    /// <summary>Cancels every queued request; a decode already running finishes and is delivered.</summary>
    public void Dispose()
    {
        List<Request> dropped;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            dropped = [.. _pending];
            _pending.Clear();
        }
        foreach (var r in dropped) r.Done.TrySetCanceled();
    }

    static BitmapSource? Decode(string path)
    {
        Images.CheckEncodedSize(new FileInfo(path).Length);
        using var image = Images.DecodeThumbnail(File.ReadAllBytes(path), PixelSide);
        return ToBitmapSource(image);
    }

    /// <summary>A frozen Pbgra32 copy of <paramref name="image"/> at 96 DPI. Safe on any thread.</summary>
    internal static BitmapSource ToBitmapSource(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0)) throw new InvalidOperationException("cannot read thumbnail pixels");
        var bitmap = BitmapSource.Create(info.Width, info.Height, 96, 96, PixelFormats.Pbgra32, null, bmp.GetPixels(), bmp.RowBytes * info.Height, bmp.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }

    sealed record Request(string Path, CancellationToken Cancel)
    {
        public TaskCompletionSource<BitmapSource?> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
```

In `src/RBX15.App/Services/ImportService.cs`, insert directly above `/// <summary>One line naming every file that failed and why.</summary>`:

```csharp
    /// <summary>
    /// The asset browser's right-click add (thumb.oncontextmenu, editor.js:2512-2539; ledger asset-rightclick-full):
    /// the file at full template size through <see cref="LayerOps.AddFull"/>, as one undo step, on top and selected,
    /// then the Select tool (setTool('select'), editor.js:2532). A failure is named on the status strip, as
    /// <see cref="Import"/> names it. Returns the number of layers added, 0 or 1.
    /// </summary>
    public int ImportFull(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var result = ImageImporter.Load([path]);
        if (result.Images.Count > 0)
        {
            var image = result.Images[0];
            session.ApplyWithSelection(s => LayerOps.AddFull(s, image.Image, session.TakeId(), image.Name));
            status.Clear();
            LayersAdded?.Invoke(this, EventArgs.Empty);
        }
        else status.Show(FailureMessage(result.Failures));
        return result.Images.Count;
    }
```

Run: `dotnet test src/RBX15.App.Tests/RBX15.App.Tests.csproj -c Release --filter "FullyQualifiedName~PackSourceTests|FullyQualifiedName~AssetThumbnailLoaderTests|FullyQualifiedName~ImportServiceTests"`
Expected: `Passed: 21, Failed: 0` (4 + 6 + 11).

Run: `ls src/RBX15.App/bin/Release/net10.0-windows/packs/drips | wc -l; ls src/RBX15.App/bin/Release/net10.0-windows/packs/word_art | wc -l; ls src/RBX15.App/bin/Release/net10.0-windows/packs`
Expected: `156`, `25`, then `drips` and `word_art` only (no README.md).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 938`; App.Tests `Passed: 726`; Parity.Tests `Passed: 35`.

- [ ] **Step 9: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/RBX15.App.csproj src/RBX15.App/Services/PackSource.cs src/RBX15.App/Services/AssetThumbnailLoader.cs src/RBX15.App/Services/ImportService.cs src/RBX15.App.Tests/PackSourceTests.cs src/RBX15.App.Tests/AssetThumbnailLoaderTests.cs src/RBX15.App.Tests/ImportServiceTests.cs
git commit -m "feat(app): ship the built-in packs, decode thumbnails off the UI thread, add at full size"
```

- [ ] **Step 10: Write the failing view-model tests**

Ledger: `packs-load-builtin`, `pack-builtin-load`, `packs-resolve-dir`, `asset-open-folder`, `asset-dedupe-by-name`, `asset-folder-grouping`, `asset-count-label`, `asset-count-label-overwritten`, `io-folder-picker`, `asset-objecturl-lifecycle`, `asset-click-add`, `asset-rightclick-full`, `asset-drag-to-canvas`, `asset-resizable-browser`, `panel-asset-library`. Create `src/RBX15.App.Tests/AssetLibraryViewModelTests.cs`:

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using RBX15.App.Canvas;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The asset library's view-model (editor.js:2302-2545; ledger panel-asset-library and the asset-* and pack* rows).
/// Placement literals are 4.1's own doubles: node evaluated addLayer (editor.js:502-511) and the right-click scale
/// (editor.js:2525) for each size and printed them.
/// </summary>
public class AssetLibraryViewModelTests
{
    sealed record Rig(AssetLibraryViewModel Vm, EditorSession Session, FakeFileDialogs Files, FakeDialogService Dialogs,
        FakeStatus Status, FakeSettingsStore Settings);

    static Rig Make(string? packsDirectory = null, AssetThumbnailLoader? thumbnails = null, TemplateMode mode = TemplateMode.Shirt)
    {
        var session = new EditorSession(new Document { Mode = mode });
        var status = new FakeStatus();
        var files = new FakeFileDialogs();
        var dialogs = new FakeDialogService();
        var settings = new FakeSettingsStore();
        var vm = new AssetLibraryViewModel(new ImportService(session, status), files, dialogs, status, settings,
            new PackSource(packsDirectory ?? PackSource.DefaultDirectory), thumbnails ?? new AssetThumbnailLoader());
        return new Rig(vm, session, files, dialogs, status, settings);
    }

    /// <summary>Writes a PNG at a '/'-separated path under <paramref name="files"/>' folder, creating folders.</summary>
    static string Png(TestFiles files, string relative, int w = 8, int h = 8)
    {
        string path = Path.Combine(files.Dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Red);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    static string[] Names(AssetLibraryViewModel vm) => vm.Rows.SelectMany(r => r.Tiles).Select(t => t.Entry.FileName).ToArray();

    static void WaitUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("the thumbnail never arrived");
            StaThread.Pump();
            Thread.Sleep(5);
        }
    }

    [Fact] // packs-load-builtin, pack-builtin-load, pack-load-sync-and-mime: 156 sorted, the pack's own label, no buttons, 3 per row
    public void Drips_loads_156_sorted_with_its_own_count_and_no_group_buttons()
    {
        var r = Make();
        Assert.False(r.Vm.IsBrowserVisible);
        Assert.Equal("", r.Vm.CountText);
        r.Vm.LoadDripsCommand.Execute(null);
        Assert.True(r.Vm.IsBrowserVisible);
        Assert.Equal("156 images in built-in drips pack", r.Vm.CountText);
        Assert.Empty(r.Vm.GroupButtons);
        Assert.Equal(52, r.Vm.Rows.Count);
        Assert.All(r.Vm.Rows, row => Assert.Equal(3, row.Tiles.Count));
        var names = Names(r.Vm);
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
        Assert.Equal("drip_blood_front_flipped.png", names[0]);
        Assert.Equal(AssetScan.AllKey, r.Vm.CurrentGroup!.Key);
        Assert.DoesNotContain(r.Vm.Rows.SelectMany(x => x.Tiles), t => t.ThumbnailRequested);   // nothing decodes until a tile is shown
        Assert.Equal(0, r.Session.UndoCount);
    }

    [Fact] // packs-inventory-wordart: 25 items, the last row holds one; the folder token in the label (pack-builtin-load quirk)
    public void Word_art_loads_25_and_replaces_the_previous_set()
    {
        var r = Make();
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.LoadWordArtCommand.Execute(null);
        Assert.Equal("25 images in built-in word_art pack", r.Vm.CountText);
        Assert.Equal(9, r.Vm.Rows.Count);
        Assert.Single(r.Vm.Rows[^1].Tiles);
        Assert.Equal(25, Names(r.Vm).Length);
        Assert.All(Names(r.Vm), n => Assert.StartsWith("word_", n));
    }

    [Fact] // packs-resolve-dir: a missing pack alerts 4.1's text (editor.js:2344) and leaves the browser as it was
    public void A_missing_pack_alerts_and_changes_nothing()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        r.Vm.LoadDripsCommand.Execute(null);
        Assert.Equal(["Built-in pack \"drips\" not found. Reinstall the app to restore it."], r.Dialogs.Alerts);
        Assert.False(r.Vm.IsBrowserVisible);
        Assert.Empty(r.Vm.Rows);
    }

    [Fact] // a pack folder with no images alerts 4.1's text (editor.js:2364) and keeps what was shown
    public void An_empty_pack_alerts_and_keeps_the_shown_set()
    {
        using var files = new TestFiles();
        Png(files, "drips/one.png");
        Directory.CreateDirectory(Path.Combine(files.Dir, "word_art"));
        File.WriteAllText(Path.Combine(files.Dir, "word_art", "README.md"), "only bundle assets you own");
        var r = Make(files.Dir);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.LoadWordArtCommand.Execute(null);
        Assert.Equal(["Built-in pack \"word_art\" is empty."], r.Dialogs.Alerts);
        Assert.Equal(["one.png"], Names(r.Vm));
        Assert.Equal("1 images in built-in drips pack", r.Vm.CountText);
    }

    [Fact] // asset-open-folder, asset-dedupe-by-name, asset-folder-grouping, asset-count-label (fix), io-folder-picker
    public void Open_folder_lists_dedupes_groups_and_shows_the_duplicates_count()
    {
        using var files = new TestFiles();
        Png(files, "art/top.png");
        Png(files, "art/a/x.png");
        Png(files, "art/a/sub/x.png");
        Png(files, "art/b/y.PNG");
        Png(files, "art/b/z.webp");
        File.WriteAllText(Path.Combine(files.Dir, "art", "notes.txt"), "not an image");
        var r = Make();
        r.Settings.SaveFolder(SettingsFolder.Asset, @"C:\last");
        r.Files.PickFolderResult = Path.Combine(files.Dir, "art");
        r.Vm.OpenFolderCommand.Execute(null);

        Assert.Equal([@"C:\last"], r.Files.PickFolderDirs);                 // last folder remembered (spec §3)
        Assert.Equal("4 unique images (1 duplicates removed)", r.Vm.CountText);        // shown, not overwritten (editor.js:2448-2449)
        Assert.Equal(["All (4)", "Root (1)", "b (2)", "sub (1)"], r.Vm.GroupButtons.Select(g => g.Label));
        Assert.Equal(["x.png", "y.PNG", "z.webp", "top.png"], Names(r.Vm));   // sorted paths, x.png in its first-seen slot
        Assert.Equal("art/a/sub/x.png", r.Vm.Set!.Entries.Single(e => e.FileName == "x.png").RelativePath);   // the deepest copy
        Assert.Equal(Path.Combine(files.Dir, "art"), r.Settings.LoadFolder(SettingsFolder.Asset));
        Assert.Equal(AssetScan.AllKey, r.Vm.CurrentGroup!.Key);
    }

    [Fact] // a cancelled picker does nothing and remembers nothing
    public void A_cancelled_folder_pick_changes_nothing()
    {
        var r = Make();
        r.Vm.OpenFolderCommand.Execute(null);
        Assert.Equal([null], r.Files.PickFolderDirs);
        Assert.False(r.Vm.IsBrowserVisible);
        Assert.Null(r.Settings.LoadFolder(SettingsFolder.Asset));
        Assert.Empty(r.Dialogs.Alerts);
    }

    [Fact] // a folder with no images alerts 4.1's text (editor.js:2385) and the existing browser state is left unchanged
    public void A_folder_without_images_alerts_and_keeps_the_shown_set()
    {
        using var files = new TestFiles();
        Directory.CreateDirectory(Path.Combine(files.Dir, "empty"));
        File.WriteAllText(Path.Combine(files.Dir, "empty", "a.svg"), "<svg/>");
        var r = Make();
        r.Vm.LoadWordArtCommand.Execute(null);
        r.Files.PickFolderResult = Path.Combine(files.Dir, "empty");
        r.Vm.OpenFolderCommand.Execute(null);
        Assert.Equal(["No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files."], r.Dialogs.Alerts);
        Assert.Equal(25, Names(r.Vm).Length);
        Assert.Null(r.Settings.LoadFolder(SettingsFolder.Asset));
    }

    [Fact] // a folder that cannot be listed is named on the status strip (diag-console-only-errors)
    public void A_folder_that_cannot_be_listed_goes_to_the_status_strip()
    {
        using var files = new TestFiles();
        var r = Make();
        string gone = Path.Combine(files.Dir, "gone");
        r.Files.PickFolderResult = gone;
        r.Vm.OpenFolderCommand.Execute(null);
        Assert.NotNull(r.Status.Current);
        Assert.StartsWith("Couldn't open gone: ", r.Status.Current);
        Assert.Empty(r.Dialogs.Alerts);
        Assert.False(r.Vm.IsBrowserVisible);
    }

    [Fact] // showAssetFolder (editor.js:2452-2466, :2544): the switch keeps the tiles (asset-objecturl-lifecycle) and sets the label
    public void A_group_switch_reuses_the_tiles_and_sets_the_count_text()
    {
        using var files = new TestFiles();
        Png(files, "art/top.png");
        Png(files, "art/s/1.png");
        Png(files, "art/s/2.png");
        var r = Make();
        r.Files.PickFolderResult = Path.Combine(files.Dir, "art");
        r.Vm.OpenFolderCommand.Execute(null);
        var before = r.Vm.Rows.SelectMany(x => x.Tiles).ToDictionary(t => t.Entry.FileName);
        var s = r.Vm.GroupButtons.Single(g => g.Key == "s");
        r.Vm.ShowGroupCommand.Execute(s);
        Assert.Same(s, r.Vm.CurrentGroup);
        Assert.Equal("2 images in s", r.Vm.CountText);
        Assert.All(r.Vm.Rows.SelectMany(x => x.Tiles), t => Assert.Same(before[t.Entry.FileName], t));
        r.Vm.ShowGroupCommand.Execute(r.Vm.GroupButtons[0]);
        Assert.Equal("3 images in all folders", r.Vm.CountText);
        r.Vm.ShowGroupCommand.Execute(r.Vm.GroupButtons[1]);
        Assert.Equal("1 images in (root)", r.Vm.CountText);
    }

    [Theory] // asset-click-add parityCheck: drip_blue_front_v1 (128x150) fit at 85%, 4.1's doubles; Pants uses its slot (same geometry)
    [InlineData(TemplateMode.Shirt)]
    [InlineData(TemplateMode.Pants)]
    public void A_click_adds_the_file_fit_in_the_front_slot_as_one_step(TemplateMode mode)
    {
        using var files = new TestFiles();
        var r = Make(files.Dir, mode: mode);
        Png(files, "drips/drip_blue_front_v1.png", 128, 150);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.AddCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        var l = r.Session.SelectedLayer!;
        Assert.Equal(("drip_blue_front_v1", 248.57866666666666, 83.6, 0.7253333333333334), (l.Name, l.X, l.Y, l.Scale));
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Empty(r.Session.Document.Layers);
    }

    [Fact] // asset-rightclick-full parityCheck: a 390x200 drip, scale 2.795, at 0,0, "(full)", undo removes it
    public void A_right_click_adds_the_file_at_full_size_as_one_step()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        Png(files, "drips/drip_blue_wide_v1.png", 390, 200);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.AddFullCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        var l = r.Session.SelectedLayer!;
        Assert.Equal(("drip_blue_wide_v1 (full)", 0.0, 0.0, 2.795), (l.Name, l.X, l.Y, l.Scale));
        Assert.Equal(1, r.Session.UndoCount);
        Assert.True(r.Session.Undo());
        Assert.Empty(r.Session.Document.Layers);
    }

    [Fact] // asset-drag-to-canvas: centered on the drop point (editor.js:505-507), 4.1's doubles, one step
    public void A_canvas_drop_centers_the_file_on_the_drop_point()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        Png(files, "drips/drip_blue_front_v1.png", 128, 150);
        r.Vm.LoadDripsCommand.Execute(null);
        r.Vm.DropOnCanvas(r.Vm.Rows[0].Tiles[0], new Point(300, 200));
        var l = r.Session.SelectedLayer!;
        Assert.Equal((253.57866666666666, 145.6, 0.7253333333333334), (l.X, l.Y, l.Scale));
        Assert.Equal(1, r.Session.UndoCount);
    }

    [Fact] // a file removed after the listing: named on the status strip (diag-console-only-errors), no step
    public void A_file_gone_since_the_listing_is_named_on_the_status_strip()
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        string path = Png(files, "drips/gone.png");
        r.Vm.LoadDripsCommand.Execute(null);
        File.Delete(path);
        r.Vm.AddCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        r.Vm.AddFullCommand.Execute(r.Vm.Rows[0].Tiles[0]);
        Assert.Equal(["Couldn't add gone.png: The file couldn't be read.", "Couldn't add gone.png: The file couldn't be read."], r.Status.Shown);
        Assert.Equal(0, r.Session.UndoCount);
    }

    [Fact] // addLayer and the right-click add both set the Select tool (editor.js:523, :2532), through the real composition
    public void Library_adds_revert_the_tool_to_select()
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        main.Assets.LoadWordArtCommand.Execute(null);
        main.Home.Tools.Set(Tool.Pen);
        main.Assets.AddCommand.Execute(main.Assets.Rows[0].Tiles[0]);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        main.Home.Tools.Set(Tool.Eraser);
        main.Assets.AddFullCommand.Execute(main.Assets.Rows[0].Tiles[0]);
        Assert.Equal(Tool.Select, main.Home.Tools.Current);
        Assert.Equal(["word_626_anton", "word_626_anton (full)"], session.Document.Layers.Select(l => l.Name));
        Assert.Equal(2, session.UndoCount);
    }

    [Fact] // the folder filter is import's (R14), and the entry's layer name is the one import gives
    public void The_folder_filter_and_layer_names_match_the_importer()
    {
        Assert.Equal(ImageImporter.Extensions, AssetScan.Extensions);
        foreach (var name in new[] { "a.png", "a.b.png", ".png", "file.", "UPPER.JPEG" })
        {
            var entry = AssetScan.FromPack("p", [@"C:\p\" + name]).Entries.SingleOrDefault();
            if (entry is not null) Assert.Equal(ImageImporter.LayerName(name), entry.LayerName);
        }
    }

    [Theory] // the resize clamp: Math.max(80, Math.min(innerHeight * 0.7, startH + delta)) (editor.js:2732); non-finite ignored
    [InlineData(180, 180)]
    [InlineData(50, 80)]
    [InlineData(300, 300)]
    [InlineData(10000, 532.7)]
    [InlineData(double.NaN, 180)]
    [InlineData(double.PositiveInfinity, 180)]
    public void The_browser_height_clamps_to_80_and_70_percent(double set, double expected)
    {
        var r = Make();
        Assert.Equal(180, r.Vm.BrowserHeight);
        r.Vm.MaxBrowserHeight = 0.7 * 761;
        r.Vm.BrowserHeight = set;
        Assert.Equal(expected, r.Vm.BrowserHeight, 9);
    }

    [Fact] // the lower bound wins when 70% of a short window is under 80 (Math.max outside Math.min)
    public void The_lower_clamp_wins_in_a_short_window()
    {
        var r = Make();
        r.Vm.MaxBrowserHeight = 0.7 * 100;
        r.Vm.BrowserHeight = 300;
        Assert.Equal(80, r.Vm.BrowserHeight);
    }

    [Fact] // R5: the first read queues the decode, the property changes when it lands; a damaged file marks the tile failed
    public void A_tile_decodes_on_first_read_and_reports_a_failure() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var r = Make(files.Dir);
        Png(files, "drips/a_ok.png", 400, 100);
        Directory.CreateDirectory(Path.Combine(files.Dir, "drips"));
        File.WriteAllBytes(Path.Combine(files.Dir, "drips", "b_bad.png"), [1, 2, 3]);
        r.Vm.LoadDripsCommand.Execute(null);
        var ok = r.Vm.Rows[0].Tiles[0];
        var bad = r.Vm.Rows[0].Tiles[1];
        var changed = new List<string?>();
        ok.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(ok.ThumbnailRequested);
        Assert.Null(ok.Thumbnail);
        Assert.Null(bad.Thumbnail);
        WaitUntil(() => ok.Thumbnail is not null && bad.ThumbnailFailed);
        Assert.Equal([nameof(AssetTileViewModel.Thumbnail)], changed);
        Assert.Equal((160, 40), (ok.Thumbnail!.PixelWidth, ok.Thumbnail.PixelHeight));
        Assert.Null(bad.Thumbnail);
        Assert.False(ok.ThumbnailFailed);
    });

    [Fact] // a new load cancels the old set's queued decodes (asset-objecturl-lifecycle): an old tile never gets a picture
    public void A_new_load_cancels_the_previous_sets_queued_thumbnails() => StaThread.Run(() =>
    {
        using var gate = new ManualResetEventSlim();
        var decoded = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var loader = new AssetThumbnailLoader(p =>
        {
            decoded.Enqueue(Path.GetFileName(p));
            gate.Wait(TimeSpan.FromSeconds(10));
            var pixel = BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, new byte[4], 4);
            pixel.Freeze();
            return pixel;
        });
        var r = Make(thumbnails: loader);
        r.Vm.LoadDripsCommand.Execute(null);
        var first = r.Vm.Rows[0].Tiles[0];
        var second = r.Vm.Rows[0].Tiles[1];
        _ = first.Thumbnail;                                    // the worker takes it and blocks
        Assert.True(SpinWait.SpinUntil(() => !decoded.IsEmpty, TimeSpan.FromSeconds(10)));
        _ = second.Thumbnail;                                   // queued behind it
        r.Vm.LoadWordArtCommand.Execute(null);                  // a new set: the queued request is cancelled
        gate.Set();
        WaitUntil(() => first.Thumbnail is not null);           // a decode already running is delivered
        StaThread.Pump();
        Assert.Equal([first.Entry.FileName], decoded);
        Assert.Null(second.Thumbnail);
        Assert.False(second.ThumbnailFailed);
    });
}
```

Run: `dotnet build src/RBX15.App.Tests/RBX15.App.Tests.csproj -c Release -warnaserror`
Expected: the build fails with `CS0246` (`AssetLibraryViewModel`, `AssetTileViewModel`) and `CS1061` (`MainViewModel.Assets`).

- [ ] **Step 11: Implement the view-models and wire them**

Create `src/RBX15.App/ViewModels/AssetTileViewModel.cs`:

```csharp
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// One thumbnail in the asset browser (showAssetFolder's thumb, editor.js:2468-2541). The first read of
/// <see cref="Thumbnail"/> queues its decode: only realized rows bind it, so only tiles the user can see are ever
/// decoded (interfaces R5). Tiles belong to one loaded set and are reused across that set's group switches, so a
/// switch never decodes again (ledger asset-objecturl-lifecycle, fix: 4.1 revoked and re-created every blob URL).
/// </summary>
public sealed class AssetTileViewModel : ObservableObject
{
    readonly Func<string, Task<BitmapSource?>> _load;
    BitmapSource? _thumbnail;
    bool _requested;
    bool _failed;

    internal AssetTileViewModel(AssetEntry entry, Func<string, Task<BitmapSource?>> load)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _load = load ?? throw new ArgumentNullException(nameof(load));
    }

    public AssetEntry Entry { get; }

    /// <summary>The name overlay (editor.js:2478-2484).</summary>
    public string Caption => Entry.Caption;

    /// <summary>thumb.title = file.name (editor.js:2471).</summary>
    public string ToolTip => Entry.FileName;

    /// <summary>Null until the decode arrives; the first read queues it. PropertyChanged when it arrives.</summary>
    public BitmapSource? Thumbnail
    {
        get
        {
            if (!_requested)
            {
                _requested = true;
                Load();
            }
            return _thumbnail;
        }
    }

    /// <summary>True when the decode failed (over a cap, damaged, or gone): the tile stays empty with its caption.</summary>
    public bool ThumbnailFailed => _failed;

    /// <summary>True once <see cref="Thumbnail"/> was read: what the virtualization test counts.</summary>
    internal bool ThumbnailRequested => _requested;

    async void Load()
    {
        BitmapSource? result;
        try
        {
            result = await _load(Entry.FullPath);
        }
        catch (OperationCanceledException)
        {
            return;   // a newer pack or folder replaced this tile's set
        }
        if (result is null)
        {
            _failed = true;
            OnPropertyChanged(nameof(ThumbnailFailed));
            return;
        }
        _thumbnail = result;
        OnPropertyChanged(nameof(Thumbnail));
    }
}
```

Create `src/RBX15.App/ViewModels/AssetRowViewModel.cs`:

```csharp
namespace RBX15.App.ViewModels;

/// <summary>
/// One row of the asset grid: up to three tiles (grid-template-columns: repeat(3, 1fr), editor.html:305). The browser
/// virtualizes rows, not tiles, because WPF's virtualizing panel is a stack (interfaces R5).
/// </summary>
public sealed class AssetRowViewModel(IReadOnlyList<AssetTileViewModel> tiles)
{
    public IReadOnlyList<AssetTileViewModel> Tiles { get; } = tiles ?? throw new ArgumentNullException(nameof(tiles));
}
```

Create `src/RBX15.App/ViewModels/AssetLibraryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The left panel's asset library (editor.html:291-309; editor.js:2302-2545; ledger panel-asset-library). Load a
/// built-in pack or a folder, filter by subfolder, and add a thumbnail as a layer by click (85% in the front slot),
/// right-click (full template size) or a drag onto the canvas (at the drop point). Every add is one undo step through
/// <see cref="ImportService"/>, which also names a failure on the status strip and reverts the tool to Select.
/// Loading replaces whatever the browser showed (editor.js:2369, :2400). A failed load (a missing or empty pack, a
/// folder with no images, a cancelled picker) leaves it as it was (editor.js:2343-2346, :2363-2366, :2384-2387).
/// </summary>
public sealed class AssetLibraryViewModel : ObservableObject
{
    /// <summary>#assetBrowser's starting max-height, and the resize's start when it is hidden (editor.html:303, editor.js:2724).</summary>
    public const double DefaultBrowserHeight = 180;

    /// <summary>The resize's lower clamp (editor.js:2732).</summary>
    public const double MinBrowserHeight = 80;

    /// <summary>grid-template-columns: repeat(3, 1fr) (editor.html:305).</summary>
    public const int TilesPerRow = 3;

    readonly ImportService _import;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ISettingsStore _settings;
    readonly PackSource _packs;
    readonly AssetThumbnailLoader _thumbnails;
    Dictionary<string, AssetTileViewModel> _tiles = new(StringComparer.Ordinal);
    CancellationTokenSource _setCancel = new();
    bool _browserVisible;
    AssetGroup? _currentGroup;
    string _countText = "";
    double _browserHeight = DefaultBrowserHeight;
    double _maxBrowserHeight = double.PositiveInfinity;

    public AssetLibraryViewModel(ImportService import, IFileDialogs files, IDialogService dialogs, IStatus status,
        ISettingsStore settings, PackSource packs, AssetThumbnailLoader thumbnails)
    {
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _packs = packs ?? throw new ArgumentNullException(nameof(packs));
        _thumbnails = thumbnails ?? throw new ArgumentNullException(nameof(thumbnails));
        LoadDripsCommand = new RelayCommand(() => LoadPack(PackSource.Drips));
        LoadWordArtCommand = new RelayCommand(() => LoadPack(PackSource.WordArt));
        OpenFolderCommand = new RelayCommand(OpenFolder);
        ShowGroupCommand = new RelayCommand<AssetGroup>(g => { if (g is not null) ShowGroup(g); });
        AddCommand = new RelayCommand<AssetTileViewModel>(t => { if (t is not null) _import.Import([t.Entry.FullPath]); });
        AddFullCommand = new RelayCommand<AssetTileViewModel>(t => { if (t is not null) _import.ImportFull(t.Entry.FullPath); });
    }

    /// <summary>Drips (#btnPackDrips, editor.js:2377).</summary>
    public IRelayCommand LoadDripsCommand { get; }

    /// <summary>Word art (#btnPackWordArt, editor.js:2378).</summary>
    public IRelayCommand LoadWordArtCommand { get; }

    /// <summary>Open folder (#btnLoadFolder, editor.js:2316): the native folder picker, starting at the last asset folder.</summary>
    public IRelayCommand OpenFolderCommand { get; }

    /// <summary>A subfolder button (editor.js:2443).</summary>
    public IRelayCommand<AssetGroup> ShowGroupCommand { get; }

    /// <summary>Click a thumbnail: add it fit at 85% in the front slot (thumb.onclick, editor.js:2502-2510; asset-click-add).</summary>
    public IRelayCommand<AssetTileViewModel> AddCommand { get; }

    /// <summary>Right-click a thumbnail: add it at full template size (editor.js:2513-2539; asset-rightclick-full).</summary>
    public IRelayCommand<AssetTileViewModel> AddFullCommand { get; }

    /// <summary>
    /// A thumbnail dropped on the canvas: centered on <paramref name="templatePoint"/>, in template pixels
    /// (canvas.ondrop's asset branch, editor.js:965-983; asset-drag-to-canvas).
    /// </summary>
    public void DropOnCanvas(AssetTileViewModel tile, Point templatePoint)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _import.Import([tile.Entry.FullPath], (templatePoint.X, templatePoint.Y));
    }

    /// <summary>False until the first successful load (#assetBrowser display: none, editor.html:303; block at editor.js:2371, :2421).</summary>
    public bool IsBrowserVisible
    {
        get => _browserVisible;
        private set => SetProperty(ref _browserVisible, value);
    }

    /// <summary>The subfolder buttons: every group, only when a folder has more than two (editor.js:2432); none for a pack (editor.js:2372).</summary>
    public ObservableCollection<AssetGroup> GroupButtons { get; } = [];

    /// <summary>The group on show; its button is highlighted (editor.js:2463-2466).</summary>
    public AssetGroup? CurrentGroup
    {
        get => _currentGroup;
        private set => SetProperty(ref _currentGroup, value);
    }

    /// <summary>The current group's tiles, three to a row.</summary>
    public ObservableCollection<AssetRowViewModel> Rows { get; } = [];

    /// <summary>
    /// #assetCount (editor.html:307). Right after a load it is the load's summary, "N unique images (D duplicates
    /// removed)" or "N images in built-in &lt;pack&gt; pack", which 4.1 overwrote in the same tick (ledger
    /// asset-count-label-overwritten and asset-count-label, fix); after a group switch it is showAssetFolder's text (editor.js:2544).
    /// </summary>
    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    /// <summary>
    /// The browser's max-height (max-height: 180px, editor.html:303; resized at editor.js:2732-2733). Setting it clamps
    /// to [<see cref="MinBrowserHeight"/>, <see cref="MaxBrowserHeight"/>], the lower bound winning as Math.max does;
    /// a non-finite value is ignored. Not saved (asset-resizable-browser, keep).
    /// </summary>
    public double BrowserHeight
    {
        get => _browserHeight;
        set
        {
            if (!double.IsFinite(value)) return;
            SetProperty(ref _browserHeight, Math.Max(MinBrowserHeight, Math.Min(_maxBrowserHeight, value)));
        }
    }

    /// <summary>70% of the window's client height (window.innerHeight * 0.7, editor.js:2732); the view sets it as a resize starts.</summary>
    public double MaxBrowserHeight
    {
        get => _maxBrowserHeight;
        set => SetProperty(ref _maxBrowserHeight, value);
    }

    /// <summary>The loaded set, or null before the first load.</summary>
    internal AssetSet? Set { get; private set; }

    void LoadPack(string name)
    {
        var files = _packs.Files(name);
        if (files is null)
        {
            _dialogs.Alert(DialogText.PackNotFound(name));
            return;
        }
        var set = AssetScan.FromPack(name, files);
        if (set.Entries.IsEmpty)
        {
            _dialogs.Alert(DialogText.PackEmpty(name));
            return;
        }
        Show(set);
    }

    void OpenFolder()
    {
        string? root = _files.PickFolder(_settings.LoadFolder(SettingsFolder.Asset));
        if (root is null) return;
        List<(string FullPath, string RelativePath)> found;
        try
        {
            found = Enumerate(root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Show(FolderFailedMessage(root, e.Message));
            return;
        }
        var set = AssetScan.FromFolder(found);
        if (set.Entries.IsEmpty)
        {
            _dialogs.Alert(DialogText.NoImagesInFolder);
            return;
        }
        _settings.SaveFolder(SettingsFolder.Asset, root);
        Show(set);
    }

    /// <summary>The status line for a folder that cannot be listed (new in v5: Chromium either listed a folder or showed nothing).</summary>
    internal static string FolderFailedMessage(string root, string reason) => $"Couldn't open {new DirectoryInfo(root).Name}: {reason}";

    /// <summary>
    /// Every file under <paramref name="root"/>, recursively, skipping folders it cannot enter, with a
    /// webkitRelativePath: the picked folder's name, then the path below it with '/' separators (editor.js:2405-2406).
    /// </summary>
    internal static List<(string FullPath, string RelativePath)> Enumerate(string root)
    {
        string name = new DirectoryInfo(root).Name;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(root, "*", options)
            .Select(p => (p, name + "/" + Path.GetRelativePath(root, p).Replace('\\', '/')))
            .ToList();
    }

    void Show(AssetSet set)
    {
        _setCancel.Cancel();
        _setCancel.Dispose();
        _setCancel = new CancellationTokenSource();
        var token = _setCancel.Token;
        _tiles = set.Entries.ToDictionary(e => e.FullPath, e => new AssetTileViewModel(e, p => _thumbnails.LoadAsync(p, token)), StringComparer.Ordinal);
        Set = set;
        GroupButtons.Clear();
        if (set.ShowGroupButtons)
            foreach (var g in set.Groups) GroupButtons.Add(g);
        IsBrowserVisible = true;
        ShowGroup(set.Groups[0]);
        CountText = set.Summary;
    }

    void ShowGroup(AssetGroup group)
    {
        if (Set is null || !Set.Groups.Contains(group)) return;
        CurrentGroup = group;
        Rows.Clear();
        foreach (var chunk in group.Entries.Chunk(TilesPerRow))
            Rows.Add(new AssetRowViewModel(chunk.Select(e => _tiles[e.FullPath]).ToArray()));
        CountText = AssetScan.CountText(group);
    }
}
```

In `src/RBX15.App/ViewModels/MainViewModel.cs`, insert after the class's last member, Task 1's `Files` property (after its closing brace; it follows `public required LayerPropertiesViewModel LayerProperties { get; init; }`):

```csharp

    /// <summary>The left panel's asset library (M4a Task 2; editor.html:291-309).</summary>
    public required AssetLibraryViewModel Assets { get; init; }
```

In `src/RBX15.App/Composition.cs`, insert directly above the anchor line `        // M3a: construction lines above this line.`:

```csharp
        // M4a Task 2: the asset library (editor.html:291-309); its adds go through the one ImportService, so LayersAdded sets Select.
        var assets = new AssetLibraryViewModel(import, fileDialogs, dialogs, status, settings, new PackSource(PackSource.DefaultDirectory), new AssetThumbnailLoader());
```

and directly above the anchor line `            // M3a: required members above this line.`:

```csharp
            Assets = assets,
```

Run: `dotnet test src/RBX15.App.Tests/RBX15.App.Tests.csproj -c Release --filter "FullyQualifiedName~AssetLibraryViewModelTests"`
Expected: `Passed: 25, Failed: 0`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 938`; App.Tests `Passed: 751`; Parity.Tests `Passed: 35`.

- [ ] **Step 12: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/ViewModels/AssetTileViewModel.cs src/RBX15.App/ViewModels/AssetRowViewModel.cs src/RBX15.App/ViewModels/AssetLibraryViewModel.cs src/RBX15.App/ViewModels/MainViewModel.cs src/RBX15.App/Composition.cs src/RBX15.App.Tests/AssetLibraryViewModelTests.cs
git commit -m "feat(app): the asset library view-model: packs, folders, groups, adds, one undo step each"
```

- [ ] **Step 13: Write the failing panel tests, and update the view-count and panel-order tests**

Ledger: `panel-asset-library`, `asset-grid-thumbnails`, `asset-thumb-uuid-label`, `asset-folder-grouping`, `asset-click-add`, `asset-rightclick-full`, `asset-drag-to-canvas`, `asset-drag-stale-reference`, `asset-resizable-browser`, `panel-asset-resize-handle`. Create `src/RBX15.App.Tests/AssetLibraryPanelTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The asset library panel (editor.html:291-309; editor.js:2452-2545, :2713-2743; styles.css:649-673). WPF ignores
/// posted mouse input and the rules forbid synthetic desktop input, so the drag and the resize run through the
/// Handle* seams the overrides forward to, with points taken from the laid-out tree. Standalone panels are laid out
/// at 251 DIP, the 280 panel's content width (280 - 2 x 14 padding - the 1 px right border, styles.css:246-253); the
/// canvas drops run in the whole main window laid out off-screen at the 1184x761 client, as ShellLookTests does.
/// </summary>
[Collection("MainWindow")] // builds MainWindow, like ShellTests and ShellLookTests
public class AssetLibraryPanelTests
{
    const double PanelWidth = 251;

    static AssetLibraryViewModel Vm(EditorSession session, string? packs = null, FakeDialogService? dialogs = null) =>
        new(new ImportService(session, new FakeStatus()), new FakeFileDialogs(), dialogs ?? new FakeDialogService(), new FakeStatus(),
            new FakeSettingsStore(), new PackSource(packs ?? PackSource.DefaultDirectory), new AssetThumbnailLoader());

    static AssetLibraryPanel Panel(AssetLibraryViewModel vm)
    {
        var panel = new AssetLibraryPanel { DataContext = vm };
        Layout(panel);
        return panel;
    }

    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(PanelWidth, 2000));
        e.Arrange(new Rect(0, 0, PanelWidth, Math.Max(e.DesiredSize.Height, 1)));
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

    static List<Button> Tiles(DependencyObject root) => Descendants<Button>(root).Where(b => b.Name == "AssetTile").ToList();

    static Point Center(FrameworkElement e, Visual relativeTo) => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight / 2), (UIElement)relativeTo);

    [Fact] // panel-asset-library: 4.1's heading, labels, buttons and tooltips; the browser is hidden until a load, the handle is not
    public void The_panel_shows_4_1s_controls_and_the_browser_only_after_a_load() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        var panel = Panel(vm);
        string Text(string name) => ((TextBlock)panel.FindName(name)).Text;
        (string, string) Btn(string name) => (((Button)panel.FindName(name)).Content.ToString()!, ((Button)panel.FindName(name)).ToolTip.ToString()!);
        Assert.Equal(("Asset library", "BUILT-IN PACKS", "LOAD FOLDER"), (Text("AssetHeading"), Text("PacksLabel"), Text("FolderLabel")));
        Assert.Equal(("Drips", "Load the bundled blood / liquid drip pack."), Btn("DripsButton"));
        Assert.Equal(("Word art", "Load the bundled word art pack."), Btn("WordArtButton"));
        Assert.Equal(("Open folder", "Pick a folder of PNGs to browse."), Btn("OpenFolderButton"));
        Assert.Equal(Visibility.Collapsed, panel.AssetBrowser.Visibility);
        Assert.Equal(Visibility.Collapsed, panel.CountLabel.Visibility);
        Assert.Equal((Visibility.Visible, "Drag to resize"), (panel.ResizeHandle.Visibility, panel.ResizeHandle.ToolTip));   // shown even while hidden (quirk, keep)
        var drips = (Button)panel.FindName("DripsButton");
        var words = (Button)panel.FindName("WordArtButton");
        Assert.Equal(drips.ActualWidth, words.ActualWidth, 1);                 // .row > * { flex: 1 }
        Assert.Equal(PanelWidth, drips.ActualWidth + words.ActualWidth + 4, 1); // gap 4px

        vm.LoadDripsCommand.Execute(null);
        Layout(panel);
        Assert.Equal(Visibility.Visible, panel.AssetBrowser.Visibility);
        Assert.Equal("156 images in built-in drips pack", panel.CountLabel.Text);
        Assert.Equal(Visibility.Collapsed, panel.GroupButtonsPanel.Visibility);   // a pack has no subfolder buttons
        Assert.Equal(180, panel.AssetBrowser.ActualHeight, 1);                    // max-height 180px with 52 rows of content
    });

    [Fact] // asset-grid-thumbnails: 3 square columns with a 4 px gap, 1px #333 border, #111, a 7px #ccc caption; R5 virtualizes rows
    public void Tiles_are_square_three_to_a_row_and_only_visible_rows_are_realized_and_decoded() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        vm.LoadDripsCommand.Execute(null);
        var panel = Panel(vm);
        var tiles = Tiles(panel);
        var scroller = Descendants<ScrollViewer>(panel.AssetGrid).Single();
        Assert.Equal(PanelWidth - 8, scroller.ViewportWidth, 3);  // the 8 DIP thin scrollbar takes its width, as overflow-y: auto did
        double side = (PanelWidth - 8 - 2 * 4) / 3;              // 78.33 DIP
        Assert.All(tiles, t => Assert.Equal((side, side), (t.ActualWidth, t.ActualHeight)));
        var row = tiles.Take(3).Select(t => t.TranslatePoint(new Point(0, 0), panel).X).ToArray();
        Assert.Equal([0, side + 4, 2 * (side + 4)], row);
        var next = tiles[3].TranslatePoint(new Point(0, 0), panel).Y - tiles[0].TranslatePoint(new Point(0, 0), panel).Y;
        Assert.Equal(side + 4, next, 3);

        int realized = Enumerable.Range(0, vm.Rows.Count).Count(i => panel.AssetGrid.ItemContainerGenerator.ContainerFromIndex(i) is not null);
        Assert.InRange(realized, 3, 8);                          // the 180 DIP view's rows (3 measured), never all 52
        Assert.Equal(realized * 3, tiles.Count);
        var requested = vm.Rows.SelectMany(r => r.Tiles).Count(t => t.ThumbnailRequested);
        Assert.Equal(tiles.Count, requested);                    // only realized tiles ever asked for a decode

        var border = (Border)VisualTreeHelper.GetChild(tiles[0], 0);
        Assert.Equal(("#FF111111", "#FF333333", 1.0, 4.0), (((SolidColorBrush)border.Background).Color.ToString(),
            ((SolidColorBrush)border.BorderBrush).Color.ToString(), border.BorderThickness.Left, border.CornerRadius.TopLeft));
        var caption = Descendants<TextBlock>(tiles[0]).Single(t => t.Name == "CaptionText");
        Assert.Equal((7.0, "#FFCCCCCC", TextTrimming.CharacterEllipsis, "drip_blood_front_flipped"),
            (caption.FontSize, ((SolidColorBrush)caption.Foreground).Color.ToString(), caption.TextTrimming, caption.Text));
        Assert.Equal(("drip_blood_front_flipped.png", "drip_blood_front_flipped"), (tiles[0].ToolTip, AutomationProperties.GetName(tiles[0])));
    });

    [Fact] // asset-folder-grouping: 9px buttons labelled All/Root/name (n), title = key, the one on show #2d4a2d on #7fff7f
    public void Group_buttons_show_their_labels_and_highlight_the_group_on_show() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        foreach (var rel in new[] { "art/top.png", "art/s/1.png", "art/t/2.png" })
        {
            string path = Path.Combine(files.Dir, rel.Replace('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(files.Image("src.png", 4, 4, SKColors.Red), path);
        }
        var session = new EditorSession();
        var dialogs = new FakeFileDialogs { PickFolderResult = Path.Combine(files.Dir, "art") };
        var vm = new AssetLibraryViewModel(new ImportService(session, new FakeStatus()), dialogs, new FakeDialogService(), new FakeStatus(),
            new FakeSettingsStore(), new PackSource(files.Dir), new AssetThumbnailLoader());
        vm.OpenFolderCommand.Execute(null);
        var panel = Panel(vm);
        var buttons = Descendants<Button>(panel.GroupButtonsPanel).ToList();
        Assert.Equal(["All (3)", "Root (1)", "s (1)", "t (1)"], buttons.Select(b => (string)b.Content));
        Assert.Equal(["(all)", "(root)", "s", "t"], buttons.Select(b => (string)b.ToolTip));
        Assert.All(buttons, b => Assert.Equal((9.0, new Thickness(6, 2, 6, 2)), (b.FontSize, b.Padding)));
        string Look(Button b) => (b.Background is SolidColorBrush bg ? bg.Color.ToString() : "gradient") + "/" + ((SolidColorBrush)b.Foreground).Color;
        Assert.Equal("#FF2D4A2D/#FF7FFF7F", Look(buttons[0]));
        Assert.NotEqual("#FF2D4A2D/#FF7FFF7F", Look(buttons[2]));
        buttons[2].Command.Execute(buttons[2].CommandParameter);
        Layout(panel);
        Assert.Equal("#FF2D4A2D/#FF7FFF7F", Look(buttons[2]));
        Assert.NotEqual("#FF2D4A2D/#FF7FFF7F", Look(buttons[0]));
        Assert.Equal("1 images in s", panel.CountLabel.Text);
        Assert.Equal("1", AutomationProperties.GetName(Assert.Single(Tiles(panel))));
    });

    [Fact] // asset-click-add: a tile is a button; Invoke (a click, Enter, Space, UI Automation) adds it at 85%, one step
    public void Invoking_a_tile_adds_it_fit_in_the_front_slot() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var tile = Tiles(panel)[0];
        ((IInvokeProvider)new ButtonAutomationPeer(tile).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal(("word_626_anton", 0.5494949494949495), (l.Name, l.Scale));   // 198x125: min(128/198, 128/125) * 0.85, as node printed it
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // asset-rightclick-full: the right button's release adds at full size and is handled, so no context menu opens
    public void A_right_click_on_a_tile_adds_it_at_full_size_and_is_handled() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var tile = Tiles(panel)[0];
        var e = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonUpEvent };
        tile.RaiseEvent(e);
        Assert.True(e.Handled);
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal(("word_626_anton (full)", 0.0, 0.0, 4.472), (l.Name, l.X, l.Y, l.Scale));   // 198x125: max(585/198, 559/125), as node printed it
        Assert.Equal(1, session.UndoCount);
        Assert.False(panel.IsDragging);
    });

    [Fact] // R6: only a move past the system distance starts a drag; a release off the canvas adds nothing and clears the state
    public void A_drag_starts_past_the_distance_and_a_release_off_the_canvas_adds_nothing() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var tile = vm.Rows[0].Tiles[0];
        var at = Center(Tiles(panel)[0], panel);
        panel.HandleTilePress(tile, at);
        panel.HandleMove(new Point(at.X + SystemParameters.MinimumHorizontalDragDistance, at.Y), leftButtonDown: true);
        Assert.False(panel.IsDragging);                                       // within the distance: still a click
        panel.HandleMove(new Point(at.X + 20, at.Y + 20), leftButtonDown: true);
        Assert.True(panel.IsDragging);
        Assert.Equal(Cursors.No, panel.Cursor);                               // no canvas in this tree
        Assert.False(panel.HandleRelease(new Point(at.X + 20, at.Y + 20)));
        Assert.False(panel.IsDragging);
        Assert.Null(panel.ReadLocalValue(FrameworkElement.CursorProperty) as Cursor);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
    });

    [Theory] // R6: Esc, a lost capture, a move with the button up, or a new press each end a drag with nothing added
    [InlineData("escape")]
    [InlineData("capture")]
    [InlineData("buttonup")]
    [InlineData("press")]
    public void Every_cancel_ends_the_drag_with_nothing_added(string how) => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var at = Center(Tiles(panel)[0], panel);
        panel.HandleTilePress(vm.Rows[0].Tiles[0], at);
        panel.HandleMove(new Point(at.X + 30, at.Y), leftButtonDown: true);
        Assert.True(panel.IsDragging);
        switch (how)
        {
            case "escape": Assert.True(panel.HandleKey(Key.Escape)); break;
            case "capture": panel.HandleCaptureLost(); break;
            case "buttonup": panel.HandleMove(new Point(at.X + 40, at.Y), leftButtonDown: false); break;
            case "press": panel.HandleTilePress(null, at); break;
        }
        Assert.False(panel.IsDragging);
        Assert.False(panel.HandleRelease(at));
        Assert.False(panel.HandleKey(Key.Escape));                           // with no drag, Esc is left to others
        Assert.Empty(session.Document.Layers);
    });

    [Fact] // a press off every tile arms nothing (the handle, the buttons, the empty grid)
    public void A_press_off_the_tiles_never_drags() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        Assert.Null(AssetLibraryPanel.TileFrom((Button)panel.FindName("DripsButton")));
        Assert.Null(AssetLibraryPanel.TileFrom(panel.ResizeBar));
        var caption = Descendants<TextBlock>(Tiles(panel)[1]).Single(t => t.Name == "CaptionText");
        Assert.Same(vm.Rows[0].Tiles[1], AssetLibraryPanel.TileFrom(caption));   // a press on the caption is a press on the tile
        panel.HandleTilePress(null, new Point(5, 5));
        panel.HandleMove(new Point(100, 100), leftButtonDown: true);
        Assert.False(panel.IsDragging);
    });

    [Fact] // asset-resizable-browser, panel-asset-resize-handle: 1:1 from the rendered height, clamped to 80 and 70% of the client
    public void The_handle_resizes_the_browser_from_its_rendered_height_within_the_clamps() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        var panel = Panel(vm);
        panel.HandleResizePress();                       // hidden: offsetHeight is 0, so it starts from 180 (editor.js:2724)
        panel.HandleResize(20);
        Assert.Equal(200, vm.BrowserHeight);
        panel.HandleResizeEnd();
        Assert.False(panel.IsResizing);

        vm.LoadDripsCommand.Execute(null);
        vm.BrowserHeight = 180;
        Layout(panel);
        panel.HandleResizePress();
        panel.HandleResize(100);
        Layout(panel);
        Assert.Equal((280.0, 280.0), (vm.BrowserHeight, Math.Round(panel.AssetBrowser.ActualHeight, 3)));
        panel.HandleResize(-1000);
        Assert.Equal(80, vm.BrowserHeight);
        panel.HandleResize(-40);
        Assert.Equal(140, vm.BrowserHeight);              // from the press, not from the last move
        panel.HandleResizeEnd();
        panel.HandleResize(300);                          // no press: nothing
        Assert.Equal(140, vm.BrowserHeight);
    });

    [Fact] // with only a few thumbnails, max-height changes nothing visible: the drag starts from the rendered height (quirk, keep)
    public void A_short_grid_resizes_from_its_rendered_height() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        Directory.CreateDirectory(Path.Combine(files.Dir, "drips"));
        File.Copy(files.Image("one.png", 4, 4, SKColors.Red), Path.Combine(files.Dir, "drips", "one.png"));
        var vm = Vm(new EditorSession(), files.Dir);
        vm.LoadDripsCommand.Execute(null);
        var panel = Panel(vm);
        double rendered = panel.AssetBrowser.ActualHeight;
        Assert.Equal(85, rendered, 3);                    // one 81 DIP row (no scrollbar) and its 4 DIP row gap, under the 180 max
        panel.HandleResizePress();
        panel.HandleResize(10);
        Assert.Equal(rendered + 10, vm.BrowserHeight, 6);
    });

    // ---- the canvas drop, in the whole window ----

    sealed record Shell(MainViewModel Main, EditorSession Session, FrameworkElement Root, AssetLibraryPanel Panel, TemplateCanvasHost Host);

    static Shell InWindow()
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var w = new MainWindow(main, new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        main.Assets.LoadWordArtCommand.Execute(null);
        var root = (FrameworkElement)w.Content;
        Lay(root);
        return new Shell(main, session, root, Descendants<AssetLibraryPanel>(root).Single(), Descendants<TemplateCanvasHost>(root).Single());
    }

    static void Lay(FrameworkElement root)
    {
        StaThread.Pump();
        root.Measure(new Size(1184, 761));
        root.Arrange(new Rect(0, 0, 1184, 761));
        root.UpdateLayout();
    }

    static void StartDrag(Shell s)
    {
        var at = Center(Tiles(s.Panel)[0], s.Panel);
        s.Panel.HandleTilePress(s.Main.Assets.Rows[0].Tiles[0], at);
        s.Panel.HandleMove(new Point(at.X + 30, at.Y + 30), leftButtonDown: true);
        Assert.True(s.Panel.IsDragging);
    }

    [Theory] // asset-drag-to-canvas parityCheck: dropped at template (300, 200) at zoom 1 and 2, the layer centers there; one undo restores
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void A_drag_onto_the_canvas_adds_the_tile_centered_on_the_drop_point(double zoom) => StaThread.Run(() =>
    {
        var s = InWindow();
        s.Main.Canvas.Zoom = zoom;
        Lay(s.Root);
        var inHost = CanvasCoordinates.ToFrame(new Point(300, 200), s.Host.Zoom);
        var inRoot = s.Host.TranslatePoint(inHost, s.Root);
        StartDrag(s);
        s.Panel.HandleMove(s.Root.TranslatePoint(inRoot, s.Panel), leftButtonDown: true);
        Assert.Equal(Cursors.Hand, s.Panel.Cursor);                          // over the canvas (R6, amended)
        Assert.True(s.Panel.HandleRelease(inRoot));
        var l = Assert.Single(s.Session.Document.Layers);
        double scale = 0.5494949494949495;                                    // word_626_anton is 198x125
        Assert.Equal(("word_626_anton", 300.0, 200.0), (l.Name, Math.Round(l.X + 198 * scale / 2, 9), Math.Round(l.Y + 125 * scale / 2, 9)));
        Assert.Equal(1, s.Session.UndoCount);
        Assert.True(s.Session.Undo());
        Assert.Empty(s.Session.Document.Layers);
        Assert.False(s.Panel.IsDragging);
    });

    [Fact] // asset-drag-stale-reference parityCheck: drop a thumbnail on the layer list, then an OS file on the canvas: the OS file is added
    public void A_drag_dropped_elsewhere_never_hijacks_the_next_os_file_drop() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var s = InWindow();
        var layers = Descendants<LayersPanel>(s.Root).Single();
        var overLayers = layers.TranslatePoint(new Point(40, 30), s.Root);
        StartDrag(s);
        s.Panel.HandleMove(s.Root.TranslatePoint(overLayers, s.Panel), leftButtonDown: true);
        Assert.Equal(Cursors.No, s.Panel.Cursor);
        Assert.False(s.Panel.HandleRelease(overLayers));
        Assert.Empty(s.Session.Document.Layers);

        var data = new DataObject(DataFormats.FileDrop, new[] { files.Image("os.png", 20, 20, SKColors.Blue) });
        Assert.True(s.Host.HandleDrop(data, new Point(100, 100)));
        Assert.Equal(["os"], s.Session.Document.Layers.Select(l => l.Name));
    });

    [Fact] // a disabled canvas (Open running, interfaces R4) and the zoom pill over it take no drop
    public void A_disabled_canvas_or_the_zoom_pill_takes_no_drop() => StaThread.Run(() =>
    {
        var s = InWindow();
        var zoomIn = Descendants<Button>(s.Root).Single(b => b.Name == "ZoomInButton");
        StartDrag(s);
        Assert.False(s.Panel.HandleRelease(Center(zoomIn, s.Root)));
        s.Host.IsEnabled = false;
        StartDrag(s);
        var inRoot = s.Host.TranslatePoint(new Point(50, 50), s.Root);
        Assert.False(s.Panel.HandleRelease(inRoot));
        Assert.Empty(s.Session.Document.Layers);
    });

    /// <summary>A PresentationSource for key events raised in a window that was never shown (KeyEventArgs requires one).</summary>
    sealed class NoSource : PresentationSource
    {
        public override Visual RootVisual { get => null!; set { } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    [Fact] // critic M8 (T2-R14): Esc cancels a running drag from anywhere in the window, not only with keyboard focus in the panel
    public void Esc_anywhere_in_the_window_cancels_a_drag() => StaThread.Run(() =>
    {
        var s = InWindow();
        StartDrag(s);
        var esc = new KeyEventArgs(Keyboard.PrimaryDevice, new NoSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        s.Root.RaiseEvent(esc);                                              // the window's content: the route never passes through the panel
        Assert.True(esc.Handled);
        Assert.False(s.Panel.IsDragging);
        Assert.False(s.Panel.HandleRelease(s.Host.TranslatePoint(new Point(50, 50), s.Root)));
        Assert.Empty(s.Session.Document.Layers);
        var again = new KeyEventArgs(Keyboard.PrimaryDevice, new NoSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        s.Root.RaiseEvent(again);
        Assert.False(again.Handled);                                         // the handler left with the drag
    });

    [Fact] // the resize's upper clamp is 70% of the window's client height (window.innerHeight * 0.7, editor.js:2732)
    public void In_the_window_the_resize_clamps_at_70_percent_of_the_client() => StaThread.Run(() =>
    {
        var s = InWindow();
        s.Panel.HandleResizePress();
        s.Panel.HandleResize(5000);
        Assert.Equal(0.7 * 761, s.Main.Assets.BrowserHeight, 9);
        s.Panel.HandleResizeEnd();
    });
}
```

In `src/RBX15.App.Tests/ThemeKeyGuardTests.cs`, replace

```csharp
        Assert.Equal(15, views.Count); // the 10 M3a view files, ColorPickerPopup.xaml, MainWindow.xaml, ToolsGroup.xaml and BrushGroup.xaml (M3b Task 2), LayerPropertiesPanel.xaml (M3b Task 3)
```

with

```csharp
        Assert.Equal(16, views.Count); // the 10 M3a view files, ColorPickerPopup.xaml, MainWindow.xaml, ToolsGroup.xaml and BrushGroup.xaml (M3b Task 2), LayerPropertiesPanel.xaml (M3b Task 3), AssetLibraryPanel.xaml (M4a Task 2)
```

In `src/RBX15.App.Tests/LayerPropertiesPanelTests.cs`, in `The_main_window_puts_the_panel_after_the_layers`, replace the comment line

```csharp
    [Fact] // the panel sits in the left panel after the layers, as editor.html:311-323 orders them
```

with

```csharp
    [Fact] // the panel sits in the left panel after the layers, as editor.html:288-323 orders them (the asset library first, M4a Task 2)
```

and replace

```csharp
        Assert.Equal(new[] { typeof(DropZone), typeof(LayersPanel), typeof(LayerPropertiesPanel) }, children.Select(c => c.GetType()));
        Assert.Same(main.LayerProperties, children[2].DataContext);
```

with

```csharp
        Assert.Equal(new[] { typeof(AssetLibraryPanel), typeof(DropZone), typeof(LayersPanel), typeof(LayerPropertiesPanel) }, children.Select(c => c.GetType()));
        Assert.Same(main.Assets, children[0].DataContext);
        Assert.Same(main.LayerProperties, children[3].DataContext);
```

Run: `dotnet build src/RBX15.App.Tests/RBX15.App.Tests.csproj -c Release -warnaserror`
Expected: the build fails with `CS0246` naming `AssetLibraryPanel`.

- [ ] **Step 14: Implement the panel, the row panel and the converter, and put the panel first in the left panel**

Create `src/RBX15.App/Controls/AssetRowPanel.cs` (T2-R3):

```csharp
using System.Windows;
using System.Windows.Controls;

namespace RBX15.App.Controls;

/// <summary>
/// One row of the asset grid: three square cells with a 4 DIP gap, flush at both ends (grid-template-columns:
/// repeat(3, 1fr); gap 4px, editor.html:305; aspect-ratio 1, editor.js:2470). The cell side comes from the width the
/// row is measured with, so a row knows its height in its first measure: the virtualizing list sizes and realizes
/// rows from real heights, never from zero-height rows waiting for an ActualWidth binding.
/// </summary>
public sealed class AssetRowPanel : Panel
{
    public const int Columns = 3;

    public const double Gap = 4;

    /// <summary>The side of a cell in a row <paramref name="width"/> DIPs wide.</summary>
    public static double CellSide(double width) => double.IsFinite(width) ? Math.Max(0, (width - Gap * (Columns - 1)) / Columns) : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        double side = CellSide(availableSize.Width);
        foreach (UIElement child in InternalChildren) child.Measure(new Size(side, side));
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, InternalChildren.Count > 0 ? side : 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double side = CellSide(finalSize.Width);
        for (int i = 0; i < InternalChildren.Count; i++)
            InternalChildren[i].Arrange(new Rect(i % Columns * (side + Gap), i / Columns * (side + Gap), side, side));
        return finalSize;
    }
}
```

Create `src/RBX15.App/Converters/EqualsMultiConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>
/// True when the two bound values are equal (<see cref="object.Equals(object, object)"/>). The asset library's
/// subfolder buttons use it to highlight the button whose group is on show (btn.title === folder, editor.js:2464).
/// One-way only.
/// </summary>
public sealed class EqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is { Length: 2 } && Equals(values[0], values[1]);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("EqualsMultiConverter is one-way.");
}
```

Create `src/RBX15.App/Views/AssetLibraryPanel.xaml`, in full:

```xml
<UserControl x:Class="RBX15.App.Views.AssetLibraryPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:RBX15.App.ViewModels"
             xmlns:conv="clr-namespace:RBX15.App.Converters"
             xmlns:controls="clr-namespace:RBX15.App.Controls">
    <!-- The asset library (editor.html:291-309; editor.js:2302-2545, :2713-2743; styles.css:324-332, :369-370, :407,
         :649-673; ledger panel-asset-library, panel-asset-resize-handle). DataContext: AssetLibraryViewModel. Click a
         thumbnail (or press Enter or Space on it) to add it at 85% in the front slot; right-click to add it at full
         template size; drag it onto the canvas to add it at the drop point. The code-behind drives the drag and the
         resize handle. The panel has no bottom margin: 4.1's .controls margin-bottom 8px collapses into the next
         heading's 14px top margin. -->
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="Visible" />
        <conv:EqualsMultiConverter x:Key="IsCurrent" />
        <!-- .controls label (styles.css:324-332) with the inline font-size:10px;color:#888 (editor.html:293, :298):
             mono 10px uppercase, margin 8px 0 3px. The 8px top collapses with the heading's 8px bottom. WPF has no
             letter-spacing (.08em; the M3b PanelHeading observation). -->
        <Style x:Key="AssetLabel" TargetType="TextBlock">
            <Setter Property="FontFamily" Value="{DynamicResource FontMono}" />
            <Setter Property="FontSize" Value="10" />
            <Setter Property="Foreground" Value="#888888" />
            <Setter Property="Margin" Value="0,0,0,3" />
        </Style>
        <!-- A subfolder button: btn btn-sm btn-secondary, font-size:9px; padding:2px 6px (editor.js:2435-2436), gap 3px
             (editor.html:304). The button on show is #2d4a2d on #7fff7f (editor.js:2464-2465). -->
        <Style x:Key="GroupButton" TargetType="Button" BasedOn="{StaticResource SecondaryButtonSm}">
            <Setter Property="FontSize" Value="9" />
            <Setter Property="Padding" Value="6,2" />
            <Setter Property="Margin" Value="0,0,3,3" />
            <Style.Triggers>
                <DataTrigger Value="True">
                    <DataTrigger.Binding>
                        <MultiBinding Converter="{StaticResource IsCurrent}">
                            <Binding />
                            <Binding Path="DataContext.CurrentGroup" RelativeSource="{RelativeSource AncestorType=UserControl}" />
                        </MultiBinding>
                    </DataTrigger.Binding>
                    <Setter Property="Background" Value="#2D4A2D" />
                    <Setter Property="Foreground" Value="#7FFF7F" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
        <!-- The resize bar: 40x3, radius r-xs, border-2; the brand gradient on hover and while dragging (styles.css:661-673). -->
        <Style x:Key="ResizeBar" TargetType="Rectangle">
            <Setter Property="Fill" Value="{DynamicResource Border2Brush}" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=Border}}" Value="True">
                    <Setter Property="Fill" Value="{DynamicResource BrandGradientBrush}" />
                </DataTrigger>
                <DataTrigger Binding="{Binding IsMouseCaptured, RelativeSource={RelativeSource AncestorType=Border}}" Value="True">
                    <Setter Property="Fill" Value="{DynamicResource BrandGradientBrush}" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <StackPanel>
        <!-- The panel's first heading: .panel h3:first-child has no top margin (styles.css:263). -->
        <TextBlock x:Name="AssetHeading" Text="Asset library" Style="{DynamicResource PanelHeading}" Margin="0,0,0,8" />

        <TextBlock x:Name="PacksLabel" Style="{StaticResource AssetLabel}"
                   Text="{Binding Source='Built-in packs', Converter={StaticResource UpperCase}, Mode=OneTime}" />
        <!-- .row with gap:4px; .row > * { flex: 1 } (editor.html:294; styles.css:369-370): two equal halves. -->
        <UniformGrid Columns="2">
            <Button x:Name="DripsButton" Style="{StaticResource SecondaryButtonSm}" Margin="0,0,2,0" Content="Drips"
                    Command="{Binding LoadDripsCommand}" ToolTip="Load the bundled blood / liquid drip pack." />
            <Button x:Name="WordArtButton" Style="{StaticResource SecondaryButtonSm}" Margin="2,0,0,0" Content="Word art"
                    Command="{Binding LoadWordArtCommand}" ToolTip="Load the bundled word art pack." />
        </UniformGrid>

        <!-- The inline margin-top:6px (editor.html:298). -->
        <TextBlock x:Name="FolderLabel" Style="{StaticResource AssetLabel}" Margin="0,6,0,3"
                   Text="{Binding Source='Load folder', Converter={StaticResource UpperCase}, Mode=OneTime}" />
        <Button x:Name="OpenFolderButton" Style="{StaticResource SecondaryButtonSm}" Content="Open folder"
                Command="{Binding OpenFolderCommand}" ToolTip="Pick a folder of PNGs to browse." />

        <!-- #assetBrowser: max-height 180px, margin-top 6px, hidden until a load (editor.html:303). The subfolder
             buttons sit above the scrolling grid; 4.1 scrolled them with it (provisional, M4b look sign-off). -->
        <DockPanel x:Name="AssetBrowser" Margin="0,6,0,0" MaxHeight="{Binding BrowserHeight}"
                   Visibility="{Binding IsBrowserVisible, Converter={StaticResource Visible}}">
            <!-- #assetSubfolders: flex-wrap, gap 3px, margin-bottom 6px (editor.html:304): 3 from the buttons' own margin. -->
            <ItemsControl x:Name="GroupButtonsPanel" DockPanel.Dock="Top" Margin="0,0,0,3" Focusable="False"
                          ItemsSource="{Binding GroupButtons}">
                <ItemsControl.Style>
                    <Style TargetType="ItemsControl">
                        <Style.Triggers>
                            <Trigger Property="HasItems" Value="False">
                                <Setter Property="Visibility" Value="Collapsed" />
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </ItemsControl.Style>
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Button x:Name="GroupButton" Style="{StaticResource GroupButton}" Content="{Binding Label}"
                                ToolTip="{Binding Key}" AutomationProperties.Name="{Binding Label}"
                                Command="{Binding DataContext.ShowGroupCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                CommandParameter="{Binding}" />
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            <!-- #assetGrid: 3 columns, gap 4px (editor.html:305), virtualized by rows (interfaces R5). -->
            <ListBox x:Name="AssetGrid" ItemsSource="{Binding Rows}" Focusable="False"
                     Background="Transparent" BorderThickness="0" Padding="0"
                     VirtualizingPanel.IsVirtualizing="True" VirtualizingPanel.VirtualizationMode="Recycling"
                     VirtualizingPanel.ScrollUnit="Pixel"
                     ScrollViewer.HorizontalScrollBarVisibility="Disabled" ScrollViewer.VerticalScrollBarVisibility="Auto"
                     KeyboardNavigation.TabNavigation="Continue"
                     AutomationProperties.Name="Asset thumbnails">
                <ListBox.Template>
                    <ControlTemplate TargetType="ListBox">
                        <ScrollViewer x:Name="AssetScroller" Style="{DynamicResource ThemedScrollViewer}" Focusable="False"
                                      CanContentScroll="True"
                                      HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
                            <ItemsPresenter />
                        </ScrollViewer>
                    </ControlTemplate>
                </ListBox.Template>
                <ListBox.ItemsPanel>
                    <ItemsPanelTemplate>
                        <VirtualizingStackPanel />
                    </ItemsPanelTemplate>
                </ListBox.ItemsPanel>
                <ListBox.ItemContainerStyle>
                    <Style TargetType="ListBoxItem">
                        <Setter Property="Focusable" Value="False" />
                        <Setter Property="Padding" Value="0" />
                        <Setter Property="Margin" Value="0,0,0,4" />
                        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="ListBoxItem">
                                    <ContentPresenter />
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ListBox.ItemContainerStyle>
                <ListBox.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:AssetRowViewModel}">
                        <!-- Three square cells, gap 4, flush ends: the row's height is known in its first measure. -->
                        <ItemsControl ItemsSource="{Binding Tiles}" Focusable="False">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate>
                                    <controls:AssetRowPanel />
                                </ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                            <ItemsControl.ItemTemplate>
                                <DataTemplate DataType="{x:Type vm:AssetTileViewModel}">
                                    <!-- A thumb (editor.js:2470-2484): cursor pointer, 1px #333 border, radius 4, #111, square
                                         (aspect-ratio 1), the image centered and never upscaled (max-width/max-height 100%),
                                         title = file name, and a 7px #ccc caption on rgba(0,0,0,.7) at the bottom, one line
                                         with an ellipsis. A Button, so Enter, Space and UI Automation Invoke add it too. -->
                                    <Button x:Name="AssetTile" Cursor="Hand"
                                            FocusVisualStyle="{DynamicResource FocusRing}"
                                            ToolTip="{Binding ToolTip}" AutomationProperties.Name="{Binding Caption}"
                                            Command="{Binding DataContext.AddCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                            CommandParameter="{Binding}"
                                            MouseRightButtonUp="OnTileRightButtonUp">
                                        <Button.Template>
                                            <ControlTemplate TargetType="Button">
                                                <Border Background="#111111" BorderBrush="#333333" BorderThickness="1" CornerRadius="4">
                                                    <Grid ClipToBounds="True">
                                                        <Image x:Name="Thumb" Source="{Binding Thumbnail}" Stretch="Uniform" StretchDirection="DownOnly"
                                                               HorizontalAlignment="Center" VerticalAlignment="Center"
                                                               RenderOptions.BitmapScalingMode="HighQuality" />
                                                        <Border VerticalAlignment="Bottom" Background="#B3000000" Padding="2,1" CornerRadius="0,0,3,3">
                                                            <TextBlock x:Name="CaptionText" Text="{Binding Caption}" FontSize="7"
                                                                       FontFamily="{DynamicResource FontBody}" Foreground="#CCCCCC"
                                                                       TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                                                        </Border>
                                                    </Grid>
                                                </Border>
                                            </ControlTemplate>
                                        </Button.Template>
                                    </Button>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>

        <!-- #assetCount: 10px #555, margin-top 4px (editor.html:307). Collapsed while empty, as an empty div has no height. -->
        <TextBlock x:Name="CountLabel" Text="{Binding CountText}" FontSize="10" Foreground="#555555" Margin="0,4,0,0"
                   FontFamily="{DynamicResource FontBody}">
            <TextBlock.Style>
                <Style TargetType="TextBlock">
                    <Style.Triggers>
                        <Trigger Property="Text" Value="">
                            <Setter Property="Visibility" Value="Collapsed" />
                        </Trigger>
                    </Style.Triggers>
                </Style>
            </TextBlock.Style>
        </TextBlock>

        <!-- .asset-resize-handle: full width, 8px, margin 2px 0, ns-resize, title "Drag to resize", shown even while the
             browser is hidden (styles.css:649-660, editor.html:308; panel-asset-resize-handle, keep). -->
        <Border x:Name="ResizeHandle" Height="8" Margin="0,2" Background="Transparent" Cursor="SizeNS"
                ToolTip="Drag to resize" AutomationProperties.Name="Resize the asset browser"
                MouseLeftButtonDown="OnHandleDown" MouseMove="OnHandleMove" MouseLeftButtonUp="OnHandleUp"
                LostMouseCapture="OnHandleCaptureLost">
            <Rectangle x:Name="ResizeBar" Width="40" Height="3" RadiusX="1.5" RadiusY="1.5" Style="{StaticResource ResizeBar}" />
        </Border>
    </StackPanel>
</UserControl>
```

Create `src/RBX15.App/Views/AssetLibraryPanel.xaml.cs`, in full:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The asset library's mouse input (interfaces R6). A thumbnail drag is in-app and capture-based, not OLE DoDragDrop:
/// a left press on a tile arms it, a move past the system drag distance starts it and captures the mouse on this
/// panel, and the release hit-tests the window. A <see cref="TemplateCanvasHost"/> under the release point gets the
/// tile at that point (canvas.ondrop's asset branch, editor.js:962-983); anywhere else nothing is added. Esc, a lost
/// capture or a move with the button up ends it with nothing added. The drag lives only in this panel's fields and
/// every end clears them, so a cancelled drag can never hijack a later OS file drop (ledger asset-drag-stale-reference,
/// fix: 4.1's _assetDragFile, editor.js:101-102, :2498, was cleared only by a canvas drop). OS file drops keep the
/// canvas's own OLE path and never see this state. A click that never became a drag is the tile Button's own
/// click (AddCommand). Right-click adds at full size and is handled, so no context menu opens (editor.js:2514). The
/// resize handle is capture-based too: 4.1 lost a mouseup outside the window and stayed resizing (asset-resizable-browser quirk).
/// The overrides only forward to the internal Handle* seams, which tests call directly.
/// </summary>
public partial class AssetLibraryPanel : UserControl
{
    AssetTileViewModel? _pressed;    // the tile a left press armed, until it moves past the drag distance or releases
    Point _pressPoint;
    AssetTileViewModel? _dragging;   // the tile being dragged; the only drag state there is
    Window? _keyWindow;              // the window whose PreviewKeyDown hands Esc to a running drag (T2-R14)
    bool _resizing;
    double _resizeStart;              // browser.offsetHeight || 180 at the press (editor.js:2724)
    double _resizeStartY;

    public AssetLibraryPanel() => InitializeComponent();

    AssetLibraryViewModel? ViewModel => DataContext as AssetLibraryViewModel;

    /// <summary>True between a drag's start and its drop or cancel.</summary>
    internal bool IsDragging => _dragging is not null;

    /// <summary>True between a press on the resize handle and its release.</summary>
    internal bool IsResizing => _resizing;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        HandleTilePress(TileFrom(e.OriginalSource as DependencyObject), e.GetPosition(this));
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        HandleMove(e.GetPosition(this), LeftDown(e));
        if (_dragging is not null) e.Handled = true;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        bool wasDragging = _dragging is not null;
        HandleRelease(e.GetPosition(Root()));
        if (wasDragging) e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // LostMouseCapture bubbles: a tile Button losing its press capture when the drag captures this panel must not cancel it.
        if (ReferenceEquals(e.OriginalSource, this)) HandleCaptureLost();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (HandleKey(e.Key)) e.Handled = true;
    }

    /// <summary>The physical left button. Task 4 Step 5 replaces this read with PointerState.LeftButton(e) (interfaces 3.3; critic C2).</summary>
    static bool LeftDown(MouseEventArgs e) => e.LeftButton == MouseButtonState.Pressed;

    /// <summary>A left press. On a tile it arms a drag; anywhere else it disarms. A drag still running (its release was missed) is cancelled.</summary>
    internal void HandleTilePress(AssetTileViewModel? tile, Point inPanel)
    {
        if (_dragging is not null) Cancel();
        _pressed = tile;
        _pressPoint = inPanel;
    }

    /// <summary>
    /// A move. Armed and past SystemParameters.MinimumHorizontalDragDistance or MinimumVerticalDragDistance, it starts
    /// the drag and captures the mouse. Dragging, it shows Hand over the canvas and No elsewhere. A move with the button
    /// up ends a drag whose release was missed, as a cancel.
    /// </summary>
    internal void HandleMove(Point inPanel, bool leftButtonDown)
    {
        if (!leftButtonDown)
        {
            _pressed = null;
            if (_dragging is not null) Cancel();
            return;
        }
        if (_dragging is null)
        {
            if (_pressed is null) return;
            if (Math.Abs(inPanel.X - _pressPoint.X) <= SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(inPanel.Y - _pressPoint.Y) <= SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = _pressed;
            _pressed = null;
            CaptureMouse();
            // Esc must reach the drag wherever keyboard focus is, not only inside this panel (T2-R14).
            _keyWindow = Window.GetWindow(this);
            if (_keyWindow is not null) _keyWindow.PreviewKeyDown += OnWindowPreviewKeyDown;
        }
        // WPF has no stock Copy cursor (that one is OLE's drag feedback), so the canvas shows Hand, the tiles' own
        // pointer, and anywhere else shows No (interfaces R6, amended: provisional, M4b look sign-off).
        Cursor = CanvasAt(TranslatePoint(inPanel, Root())) is not null ? Cursors.Hand : Cursors.No;
    }

    /// <summary>
    /// The release, at a point in the root visual's coordinates (the window's client area in the app). A drag ends
    /// here: over a canvas host it adds the tile centered on that point in template pixels and returns true; anywhere
    /// else it adds nothing. Without a drag it only disarms, and the tile Button's click does the click-add.
    /// </summary>
    internal bool HandleRelease(Point inRoot)
    {
        _pressed = null;
        if (_dragging is not { } tile) return false;
        _dragging = null;
        Finish();
        if (CanvasAt(inRoot) is not var (host, inHost) || ViewModel is not { } vm) return false;
        vm.DropOnCanvas(tile, CanvasCoordinates.ToTemplate(inHost, host.Zoom));
        return true;
    }

    /// <summary>Capture lost mid-drag (Alt+Tab, a dialog): cancel, with nothing added.</summary>
    internal void HandleCaptureLost()
    {
        if (_dragging is not null) Cancel();
    }

    /// <summary>Esc cancels a running drag (4.1's dragend, which also ran on Esc, left the stale file behind). True when used.</summary>
    internal bool HandleKey(Key key)
    {
        if (key != Key.Escape || _dragging is null) return false;
        Cancel();
        return true;
    }

    /// <summary>
    /// The canvas host under a root point, and the point in the host's coordinates, or null. A hit on anything that is
    /// not inside a host (the zoom pill, the area around the frame, the panels) is no canvas, as canvas.ondrop fired
    /// only on the canvas element itself. A disabled host (Open running, interfaces R4) takes no drop.
    /// </summary>
    internal (TemplateCanvasHost Host, Point InHost)? CanvasAt(Point inRoot)
    {
        var root = Root();
        if (VisualTreeHelper.HitTest(root, inRoot) is not { VisualHit: DependencyObject hit }) return null;
        for (DependencyObject? d = hit; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is TemplateCanvasHost host)
                return host.IsEnabled ? (host, root.TranslatePoint(inRoot, host)) : null;
        return null;
    }

    /// <summary>The top of this panel's visual tree: the window in the app, the laid-out root in tests.</summary>
    UIElement Root()
    {
        DependencyObject node = this;
        while (VisualTreeHelper.GetParent(node) is { } parent) node = parent;
        return (UIElement)node;
    }

    void Cancel()
    {
        _dragging = null;
        _pressed = null;
        Finish();
    }

    void Finish()
    {
        ClearValue(CursorProperty);
        if (_keyWindow is not null)
        {
            _keyWindow.PreviewKeyDown -= OnWindowPreviewKeyDown;
            _keyWindow = null;
        }
        // _dragging is already null, so the LostMouseCapture this raises is ignored.
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>The window-level Esc while a drag runs (T2-R14); the panel's own override covers focus inside it.</summary>
    void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleKey(e.Key)) e.Handled = true;
    }

    /// <summary>The tile a visual belongs to, or null.</summary>
    internal static AssetTileViewModel? TileFrom(DependencyObject? source)
    {
        for (var d = source; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is Button { Name: "AssetTile", DataContext: AssetTileViewModel tile }) return tile;
            if (d is AssetLibraryPanel) return null;
        }
        return null;
    }

    void OnTileRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;   // contextmenu is prevented (editor.js:2514)
        if (sender is FrameworkElement { DataContext: AssetTileViewModel tile } && ViewModel is { } vm) vm.AddFullCommand.Execute(tile);
    }

    // ---- the resize handle (editor.js:2713-2743) ----

    /// <summary>
    /// A press on the handle: the start height is the browser's rendered height, or 180 while it is hidden
    /// (<c>browser.offsetHeight || 180</c>, editor.js:2724), and the upper clamp is 70% of the window's client height
    /// (editor.js:2732), read now from the window's content, as 4.1 read innerHeight on every move. Outside a window the
    /// view-model's clamp stays as it is.
    /// </summary>
    internal void HandleResizePress()
    {
        _resizing = true;
        _resizeStart = AssetBrowser.ActualHeight > 0 ? AssetBrowser.ActualHeight : AssetLibraryViewModel.DefaultBrowserHeight;
        if (ViewModel is { } vm && Window.GetWindow(this)?.Content is FrameworkElement { ActualHeight: > 0 } client)
            vm.MaxBrowserHeight = 0.7 * client.ActualHeight;
    }

    /// <summary>A drag of the handle: <paramref name="deltaY"/> DIPs from the press (positive is down); the view-model clamps.</summary>
    internal void HandleResize(double deltaY)
    {
        if (_resizing && ViewModel is { } vm) vm.BrowserHeight = _resizeStart + deltaY;
    }

    /// <summary>The handle's release, or its capture lost: the resize ends where it is.</summary>
    internal void HandleResizeEnd()
    {
        _resizing = false;
        if (ResizeHandle.IsMouseCaptured) ResizeHandle.ReleaseMouseCapture();
    }

    void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;   // preventDefault (editor.js:2721): no tile press, no text selection
        HandleResizePress();
        _resizeStartY = e.GetPosition(Root()).Y;
        ResizeHandle.CaptureMouse();
    }

    void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_resizing) HandleResize(e.GetPosition(Root()).Y - _resizeStartY);
    }

    void OnHandleUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        e.Handled = true;
        HandleResizeEnd();
    }

    void OnHandleCaptureLost(object sender, MouseEventArgs e)
    {
        if (_resizing) HandleResizeEnd();
    }
}
```

In `src/RBX15.App/MainWindow.xaml`, replace

```xml
            <views:DropZone DataContext="{Binding Layers}" />
```

with

```xml
            <views:AssetLibraryPanel DataContext="{Binding Assets}" />
            <views:DropZone DataContext="{Binding Layers}" />
```

In `src/RBX15.App/Views/DropZone.xaml`, replace

```xml
        <!-- The panel's first heading: .panel h3:first-child has no top margin (styles.css:263). -->
        <TextBlock Text="Drop art here" Style="{DynamicResource PanelHeading}" Margin="0,0,0,8" />
```

with

```xml
        <!-- .panel h3 (styles.css:254-263): the asset library is the panel's first section from M4a, so this heading takes
             PanelHeading's own 14px top margin, as 4.1's second h3 does (editor.html:311). -->
        <TextBlock Text="Drop art here" Style="{DynamicResource PanelHeading}" />
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.App.Tests/RBX15.App.Tests.csproj -c Release --filter "FullyQualifiedName~AssetLibraryPanelTests|FullyQualifiedName~ThemeKeyGuardTests|FullyQualifiedName~LayerPropertiesPanelTests"`
Expected: `Passed: 35, Failed: 0` (19 + 1 + 15).

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 938, Failed: 0, Skipped: 0`; App.Tests `Passed: 770, Failed: 0`; Parity.Tests `Passed: 35, Failed: 0`. Run it three times; every run is green (measured).

- [ ] **Step 15: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Controls/AssetRowPanel.cs src/RBX15.App/Converters/EqualsMultiConverter.cs src/RBX15.App/Views/AssetLibraryPanel.xaml src/RBX15.App/Views/AssetLibraryPanel.xaml.cs src/RBX15.App/MainWindow.xaml src/RBX15.App/Views/DropZone.xaml src/RBX15.App.Tests/AssetLibraryPanelTests.cs src/RBX15.App.Tests/ThemeKeyGuardTests.cs src/RBX15.App.Tests/LayerPropertiesPanelTests.cs
git commit -m "feat(app): the asset library panel: virtualized thumbnails, click, right-click and drag to canvas, resize"
```

- [ ] **Step 16: Task 2 checks**

Run: `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx" src`
Expected: no output.

Run: `grep -rn --include=*.cs "DoDragDrop(" src/RBX15.App`
Expected: no output (R6: the asset drag is in-app and capture-based; the doc comments that name OLE DoDragDrop have no call).

Run: `grep -c "AssetLibraryPanel" src/RBX15.App/MainWindow.xaml`
Expected: `1`.

Run: `git diff --stat 3f3c5c5 -- editor.js editor.html styles.css main.js package.json packs lib tools/parity-ref`
Expected: no output. The Electron app and the packs are untouched; the build copies the packs.

Run: `git status --short`
Expected: no output after Step 15.

**Handoff to Task 5** (evidence only; Task 5 owns the ledger, `divergences.md` and the manual list):

1. **`asset-count-label`:** its parityCheck says the label reads `<N> images in all folders` after a folder with duplicates, which contradicts its own fix. Reword it to the shown summary (`U unique images (D duplicates removed)`, then the showAssetFolder text after a switch). Evidence: `Open_folder_lists_dedupes_groups_and_shows_the_duplicates_count`, `A_group_switch_reuses_the_tiles_and_sets_the_count_text`. `asset-count-label-overwritten`: the same evidence plus `Drips_loads_156_sorted_with_its_own_count_and_no_group_buttons`.
2. **Packs:**
   - `packs-inventory-drips`, `packs-inventory-wordart` and `pack-contents`: `The_output_carries_each_pack_byte_for_byte` (156 and 25, SHA-256 equal, no README).
   - `packs-resolve-dir`: its MSIX half is M6's; the missing-folder alert is `A_missing_pack_alerts_and_changes_nothing`. Reword, or leave the installed-package half open.
   - `pack-builtin-load` and `packs-load-builtin`: the two captions, 156 and 25 in 4.1's readdir order (`The_shipped_packs_sort_as_4_1_listed_them`).
   - `pack-load-sync-and-mime`: listing a pack's 156 names stays synchronous (`Directory.GetFiles`); every read and decode is off the UI thread (`AssetThumbnailLoaderTests`). The divergence text should say "decoded without blocking the UI".
3. **Folder rows:** `asset-open-folder`, `asset-dedupe-by-name`, `asset-folder-grouping` and `io-folder-picker`: `AssetScanTests` plus the view-model folder tests. The picker itself (`OpenFolderDialog`) is Task 1's `Win32FileDialogs` and the bridge's `brg files` run.
4. **Thumbnails:** `asset-grid-thumbnails` and `asset-objecturl-lifecycle`: the virtualization, square and look facts in `AssetLibraryPanelTests`, and the decode tests. `asset-objecturl-lifecycle`'s parityCheck ("500-image folder memory and scroll") is a manual line (M-A below), or a reword to the realized-rows evidence.
5. **Adds:** `asset-click-add` (`A_click_adds_the_file_fit_in_the_front_slot_as_one_step`, both modes), `asset-rightclick-full` (`A_right_click_adds_the_file_at_full_size_as_one_step`, `A_right_click_on_a_tile_adds_it_at_full_size_and_is_handled`, `LayerOpsTests.Add_full_covers_the_template_from_the_top_left`), `asset-drag-to-canvas` (`A_drag_onto_the_canvas_adds_the_tile_centered_on_the_drop_point` at zoom 1 and 2, through the seams; the bridge `brg files` run is the live check) and `asset-drag-stale-reference` (`A_drag_dropped_elsewhere_never_hijacks_the_next_os_file_drop`; its portTarget's "typed DataObject payload" becomes "in-app capture drag" per R6).
6. **Resize:** `asset-resizable-browser` and `panel-asset-resize-handle`: the resize facts. The "cursor shows ns-resize; no stuck cursor" half is the bridge's `inspect` or manual.
7. **`asset-thumb-uuid-label`:** `Captions_use_the_parent_for_uuid_names`, `An_entry_carries_the_file_name_layer_name_and_caption`.
8. **Divergence wording to add:**
   - `panel-asset-library`: "thumbnails are buttons: Enter, Space and UI Automation add them" (T2-R2).
   - `asset-drag-to-canvas`: "the drag shows the Hand cursor over the canvas and No elsewhere, with no drag image" (T2-R1).
   - `io-image-formats`: a .bmp in a folder keeps its extension in the caption (T2-R6, with R14).
9. **Manual lines.** Automation cannot do these, or the bridge does not cover them:
   - **M-A:** Open a folder of about 500 photos and scroll the grid quickly. Thumbnails fill in from the rows on screen, and the app never stalls.
   - **M-B:** Tab into the grid and keep pressing Tab past the visible rows. Focus moves through the tiles and the grid scrolls with it (virtualized keyboard focus, not unit-tested).
   - **M-C:** Wheel over the grid, at its end, and over the panel around it. The grid scrolls first, then the left panel (nested scrolling; Chromium's chaining was not measured).
   - **M-D:** Drag a thumbnail onto the canvas at 100% and 200% zoom. It lands centered under the pointer.
   - **M-E:** Drag a thumbnail, then drop it on the layer list. Then drag a PNG from Explorer onto the canvas. The Explorer file is added. Explorer is out of the bridge's reach.
10. **Look page (M4b sign-off):** T2-R1, T2-R4 and T2-R5. Also the 78.33 against 81 DIP tile with the grid scrollbar, the thumbnail `BitmapScalingMode="HighQuality"`, and the caption's `CornerRadius` 0,0,3,3 that stands in for `overflow: hidden` with radius 4.

**Notes for the other tasks:**

- **Task 3:** add the T2-R8 status line (`AssetLibraryViewModel.FolderFailedMessage`) to the inventory. The thumbnail failure summary, once per set (interfaces §4), can read `AssetTileViewModel.ThumbnailFailed` (raised through `PropertyChanged`) from inside `AssetLibraryViewModel.Show`. The four alert sites this task calls are `DialogText.PackNotFound`, `PackEmpty` and `NoImagesInFolder`, at editor.js:2344, :2364 and :2385.
- **Task 4:** `AssetLibraryPanel.LeftDown(MouseEventArgs)` is the one left-button read; Task 4 Step 5 replaces its body with `PointerState.LeftButton(e) == MouseButtonState.Pressed`, and `InputBridgeTests.An_asset_tile_drag_onto_the_canvas_adds_one_layer_in_one_step` pins it (critic C2). The bridge's `{"element":"AssetTile",...}` matches the first realized tile (`x:Name="AssetTile"`). A UIA `Invoke` on a tile is a click-add. The resize handle captures on `ResizeHandle`, and the drag captures on the panel.
- **Task 5:** the smoke's `brg files` asset checks press on a tile and move by more than `SystemParameters.MinimumHorizontalDragDistance` before the canvas move. The release must land inside the canvas frame, not on the zoom pill (T2-R11).

**Additions requested by Task 2** (beyond `.superpowers/m4-planning/interfaces.md`; the sheet's names are unchanged):

1. **New, `RBX15.App.Controls.AssetRowPanel : Panel`** (T2-R3): `Columns = 3`, `Gap = 4`, `static double CellSide(double width)`. It is the row items panel.
2. **New, `RBX15.App.Converters.EqualsMultiConverter : IMultiValueConverter`**, defined in the panel's own resources (key `IsCurrent`) for the group-button highlight. `Controls.xaml` is unchanged.
3. **Amendment, R6's cursor:** `Cursors.Hand` over the canvas (T2-R1). `Cursors.Copy` does not exist in WPF.
4. **New, `AssetLibraryViewModel` members:** `const double DefaultBrowserHeight = 180`, `const double MinBrowserHeight = 80` and `const int TilesPerRow = 3`; `internal AssetSet? Set`; `internal static List<(string FullPath, string RelativePath)> Enumerate(string root)`; `internal static string FolderFailedMessage(string root, string reason)`.
5. **New, `AssetTileViewModel`:** `internal AssetTileViewModel(AssetEntry entry, Func<string, Task<BitmapSource?>> load)` and `internal bool ThumbnailRequested`. **`AssetThumbnailLoader`:** `internal AssetThumbnailLoader(Func<string, BitmapSource?> decode)` and `internal static BitmapSource ToBitmapSource(SKImage image)`.
6. **New, `AssetLibraryPanel`:**
   - Seams: `internal bool IsDragging`, `internal bool IsResizing`, `internal void HandleResizePress()`, `internal void HandleResizeEnd()`, `internal (TemplateCanvasHost Host, Point InHost)? CanvasAt(Point inRoot)` and `internal static AssetTileViewModel? TileFrom(DependencyObject? source)`.
   - Clarifications: `HandleResize(double deltaY)` is the total delta since `HandleResizePress`, as 4.1's `startH + (clientY - startY)`. `HandleRelease(Point inRoot)` takes root-visual coordinates (the window's client area in the app) and returns true iff it added.
   - Named parts: `AssetHeading`, `PacksLabel`, `DripsButton`, `WordArtButton`, `FolderLabel`, `OpenFolderButton`, `AssetBrowser` (DockPanel, `MaxHeight` = `BrowserHeight`), `GroupButtonsPanel`, `GroupButton` (template), `AssetGrid`, `AssetScroller` (template), `AssetTile` (template), `Thumb` and `CaptionText` (tile template), `CountLabel`, `ResizeHandle` and `ResizeBar`.
7. **Clarification, `PackSource.Files`:** null also when the pack folder exists but cannot be listed (T2-R7).
8. **Clarification, `AssetScan.IsImageName`:** a bare `.png` is an image name, as 4.1's regex matches it; its layer then takes `LayerOps.DefaultName`. **`FromFolder`:** a subfolder named `(all)` gets no group of its own, as 4.1's map overwrote it.
9. **New test class, `AssetThumbnailLoaderTests`** (6), not in interfaces §4's table. Also `ImagesOrientationTests` +2, beside `ImagesCapsTests`.
10. **Rejected (critic C1):** the drafter asked Task 1 for `FakeFileDialogs.FolderResult` and `FolderInitialDirectories`. Task 1 Step 7 prints the fake in full with `PickFolderResult` and `PickFolderDirs`, which this task now uses throughout.
11. **Reconciler (critic M8):** `AssetLibraryPanel` gains a private `_keyWindow` field and `OnWindowPreviewKeyDown`; `AssetLibraryPanelTests` gains `NoSource` and `Esc_anywhere_in_the_window_cancels_a_drag` (+1).

---

### Task 3: The dialog and status inventory, console-only failures on the status strip, and same-file re-pick

**Files:**

- Create (app): `src/RBX15.App/ViewModels/ThumbnailFailureReport.cs`: one status line per asset set whose previews fail (ruling T3-R3).
- Create (app tests): `src/RBX15.App.Tests/DialogInventoryTests.cs` (31 cases).
- Modify (app): `src/RBX15.App/Composition.cs`: one comment line and one call after Task 2's `assets` construction line (Step 4).
- Read only:
  - 4.1: `editor.js`, `editor.html`, `main.js`, untouched. `DialogInventoryTests` reads `editor.js`, `editor.html` and `main.js` at test time.
  - `docs/port/parity-ledger.json`.
  - Task 1's `src/RBX15.App/Services/DialogText.cs`, `ProjectFileService.cs`, `IDialogService.cs`, `IFileDialogs.cs`, `Win32FileDialogs.cs`; Task 2's `src/RBX15.App/ViewModels/AssetLibraryViewModel.cs`, `AssetTileViewModel.cs`, `AssetRowViewModel.cs`, `src/RBX15.App/Services/PackSource.cs`, `AssetThumbnailLoader.cs` (Step 1 checks they landed).
- Not touched:
  - the engine; every view and XAML file (this task adds no view); `Fakes/*` (the test class carries its own recording fakes, ruling T3-R6); `ImportService`, `ImageImporter`, `CanvasInteraction`, `FileGroupViewModel`, `LayersViewModel` (their failure routing already lands on the status strip, and the tests pin it as it is).
  - The ledger and `divergences.md` (Task 5, from the handoff at the end).

**Interfaces:**

- Consumes:
  - **Task 1** (interfaces.md §3.3, verbatim): `DialogText.{PackNotFound, PackEmpty, NoImagesInFolder, LoadFailed}`; `IDialogService.{Alert, Confirm, AskSaveChanges}` and `SaveChoice`; `IFileDialogs.{OpenImages, SavePng, OpenProject, SaveProject, PickFolder}`; `ProjectFileService.{OpenAsync(), IsBusy}`; `EditorSession.{IsDirty (R2), CurrentFile}`; `MainViewModel.Files`; `Composition.Build(EditorSession, IFileDialogs, IDialogService, IColorPicker, ISettingsStore)`; `FakeSettingsStore` with `LoadFolder`/`SaveFolder`; `Win32FileDialogs`.
  - **Task 2** (interfaces.md §3.3, §3.5, R5): `AssetLibraryViewModel.{LoadDripsCommand, LoadWordArtCommand, OpenFolderCommand, ShowGroupCommand, AddCommand, IsBrowserVisible, GroupButtons, Rows, CountText}`; `AssetRowViewModel.Tiles`; `AssetTileViewModel.{Entry, Thumbnail, ThumbnailFailed}`; `PackSource(string)`, `PackSource.Drips`, `PackSource.WordArt`; `AssetThumbnailLoader()`; `AssetScan.AllKey`; `MainViewModel.Assets`; the Composition line `var assets = new AssetLibraryViewModel(import, fileDialogs, dialogs, status, settings, new PackSource(PackSource.DefaultDirectory), new AssetThumbnailLoader());` (§3.10).
  - **M3b and earlier:** `CanvasInteraction.NoLayerToErase`, `ExportGuard.EmptyTemplateMessage`, `ImportService(EditorSession, IStatus)`, `ImageImporter`'s four reasons, `LayersViewModel.{BrowseCommand, DropFiles}`, `CanvasViewModel.DropHandler`, `CanvasDrop`, `FileGroupViewModel.AddImageCommand`, `StatusViewModel.Message`, `LayerOps.{AddImage, MoveTo}`, `ProjectIO.Write`, `StaThread.{Run, Pump}`, `FakeStatus`, `FakeColorPicker`, `RepoRoot.Find` (RBX15.Parity).
- Produces (the sheet's Task 3 row, plus "Additions requested by Task 3" at the end):
  - `internal sealed class ThumbnailFailureReport` (namespace `RBX15.App.ViewModels`): `public static ThumbnailFailureReport Attach(AssetLibraryViewModel library, IStatus status)` and `public static string Message(string fileName)` (+).
  - The Composition call `ThumbnailFailureReport.Attach(assets, status);` (+).
  - `DialogInventoryTests` (31 cases): each of 4.1's six sites mapped to its v5 text verbatim, with the editor.js line in the test data; the console-only sites mapped to their status text; same-file re-pick for `OpenImages`, `OpenProject` and `PickFolder` (the sheet's Task 3 row).

**What 4.1 does (cited).** The shipped `editor.js`, `editor.html` and `main.js`, 4.1, untouched.

- **The six blocking sites** (ledger `dialogs-inventory`, `io-alert-inventory`):
  - editor.js:1270 `alert('Select a layer to erase from, then try again.')`, after a whole eraser stroke with no selection.
  - editor.js:1506 `confirm('The template is empty — download a blank PNG anyway?')`, the empty export.
  - editor.js:2344 `alert('Built-in pack "' + packName + '" not found. Reinstall the app to restore it.')`, from `resolvePackDir` returning null (editor.js:2325-2333).
  - editor.js:2364 `alert('Built-in pack "' + packName + '" is empty.')`, after the `/\.(png|jpg|jpeg|gif|webp)$/i` filter (editor.js:2348).
  - editor.js:2385 `alert('No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files.')` (filter at editor.js:2381-2383).
  - editor.js:2676 `alert('Failed to load project. ' + err.message)`, any throw inside `loadProject`'s `onload` (editor.js:2596-2677).
  - `packName` is the raw key, `'drips'` or `'word_art'` (editor.js:2377-2378). `editor.html` and `main.js` hold no `alert(`, `confirm(`, `prompt(`, `showMessageBox` or `showErrorBox` (measured: `main.js` has no `console`, `dialog` or `catch` at all).
- **Console-only failures** (ledger `diag-console-only-errors`):
  - `handleFiles` (editor.js:998-1011): `console.log('Skipped non-image:', ...)` (:1000), `console.error('Image load failed:', ...)` (:1006), `console.error('FileReader failed:', ...)` (:1009). It serves the drop zone's click (editor.js:954, :958), its drop (:957) and the canvas drop (:985).
  - Add image (editor.js:2692-2708) and the asset thumbnail click (editor.js:2502-2510) set `img.onload` and no `onerror`: an undecodable file does nothing.
  - The asset grid sets `img.src = URL.createObjectURL(file)` with no `onerror` (editor.js:2473-2475): an unreadable file shows a broken image.
  - `loadProject`'s layer `img.onerror` only counts (editor.js:2669-2672): the layer vanishes silently.
  - The background image (editor.js:935-946, no `onerror` on its image at :941) and Word Art are the Layout and Text tabs: M4b.
- **Re-pick** (ledger `file-input-same-file-reselect`): `#fileInput` (the drop zone's click, editor.js:954, :958) and `#bgFileInput` (editor.js:935) never reset `.value`, so the same file picked again fires no `change`. `#projectFileInput` (editor.js:2686), `#imageLoadInput` (editor.js:2707) and `#folderInput` (editor.js:2316) reset it.

**Ledger rows this task serves.** Dispositions followed; Task 5 closes rows under the honesty rule.

- `dialogs-inventory` (fix): five of the six sites are themed dialogs titled with the product name, text verbatim; the sixth (editor.js:1270) is on the status strip by M3b's ruling (T3-R1). No other modal text exists.
- `io-alert-inventory` (fix): the six strings verbatim; a non-JSON and a truncated project both show the `Failed to load project.` prefix and leave the document untouched (its parityCheck, word for word).
- `diag-console-only-errors` (fix): every console-only failure in M4a's scope reaches the status strip; the background image and Word Art sites are M4b's (T3-R5).
- `file-input-same-file-reselect` (fix): re-picking the same file acts again, through the drop zone's click (the row's parityCheck), Add image, Open and Open folder.

**Rulings** (each pinned by a named test):

- **T3-R1: the eraser's message stays on the status strip.** Spec §4 says 4.1's six blocking calls "become themed dialogs". M3b ruled the eraser refuses at the press with a status message (`eraser-selection-semantics`, fix, closed in M3b; divergences.md:36), because a dialog after a press would block every stroke attempt. That ruling is not reopened ("Rulings already made"). The text stays verbatim (`CanvasInteraction.NoLayerToErase`, CanvasInteraction.cs:43) and its routing is already pinned by M3b's `LedgerScenarioTests.The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden`. Task 5 words `dialogs-inventory`'s divergence to match (handoff item 1).
  - Pinned by `Each_4_1_message_is_kept_verbatim(1270, "eraser")`.
  - Cost if wrong: route the refusal through `IDialogService.Alert` in `CanvasInteraction` and rewrite that M3b scenario's status assertion.
- **T3-R2: `Failed to load project. ` plus the raw reason, no case change.** `ProjectFormatException` messages start lower case and name JSON keys verbatim (`layers must be an array, not a string`, `bgImageSrc must be a data: URL, ...`; ProjectIO.cs:64-261). Capitalizing would misname a key (`Layers[2].id`). 4.1 appended `err.message` raw (editor.js:2676), and the ledger's portTarget says "define native messages". IO failures carry .NET's own sentence (`Could not find file '...'`).
  - Pinned by `Each_4_1_message_is_kept_verbatim(2676, ...)` and `A_project_that_cannot_be_read_alerts_with_the_4_1_prefix_asks_nothing_and_changes_nothing` (`the file is not JSON: ` and `Could not find file`).
  - Cost if wrong: one `char.ToUpperInvariant` in `DialogText.LoadFailed`.
- **T3-R3: an asset preview that cannot be shown is one status line per set.** The sheet leaves "the caller reports to status once per set" (§3.3, `AssetThumbnailLoader`) unowned by Task 2 and lists it under Task 3 (§4). It is a new class beside the library rather than an edit inside Task 2's view-model, so Task 3 edits no Task 2 file and its anchor is the sheet's own Composition line.
  - A set is the tiles one load made. R5 makes a group switch reuse them and a new load create them, so the report keys on tile identity: rows whose tiles were all unseen start a new set; rows appended to rows already showing, or a group switch, stay in it.
  - The line names the first file that failed: `Couldn't show a preview for <file>. Click its tile to see why.` Clicking imports the file, and the import names the exact reason (decode, size cap or read), which the loader's null result does not carry. New v5 wording; provisional; listed for the M4b look sign-off (handoff item 4).
  - A failure from a tile of an earlier set is ignored; its handlers are removed when the set changes.
  - Pinned by the two preview facts (Review Focus 2).
- **T3-R4: same-file re-pick is proven through the real composition, with picks scripted.** A native dialog returns on every pick (Win32FileDialogs.cs:6-10, M3a), so the 4.1 bug cannot occur as long as no picker instance outlives a call. `The_native_pickers_keep_no_dialog_between_calls` pins that structurally: no field of `Win32FileDialogs` is a `CommonDialog`. The background picker (`#bgFileInput`) is M4b's Layout tab.
- **T3-R5: `diag-console-only-errors` is covered for M4a's routes.** editor.js:1000-1009, :2473-2475, :2502-2510, :2669-2672 and :2700-2703 are pinned here. editor.js:935-946 (the background image; the ledger cites :940-944) is M4b's Layout tab. Task 5 narrows the row to M4a's routes or holds it for M4b; this task does not decide closure (handoff item 2).
- **T3-R6: the test class carries its own recording fakes** (`RecordingDialogs`, `ScriptedPicks`). Task 1 extends `FakeFileDialogs` and `FakeDialogService`, and the sheet does not fix those members' names, so a local queue-per-picker fake keeps Task 3 independent of them. `FakeStatus`, `FakeColorPicker` and `FakeSettingsStore` are used as they are.
- **T3-R7: "no other modal text" is checked on both sides.** The 4.1 side counts `alert(`/`confirm(`/`prompt(` in `editor.js` (exactly lines 1270, 1506, 2344, 2364, 2385, 2676) and finds none in `editor.html` or `main.js`. The v5 side scans `src/RBX15.App/**/*.cs`:
  - no `MessageBox.Show(`;
  - `new ThemedDialog(` only in `ThemedDialogService.cs`;
  - every `.Alert(` and `.Confirm(` call passes a `DialogText.*` member or `ExportGuard.EmptyTemplateMessage` directly as its argument;
  - every inventory entry has a call site.
  - `AskSaveChanges` takes a document name, not a message, so it is outside the argument rule (R13 composes the text inside the service).
  - Pinned by `Editor_js_has_exactly_the_six_dialog_sites_of_the_inventory` and `Every_v5_dialog_shows_an_inventory_string`.

**Measured while drafting:**

- **Setup.**
  - A scratch copy of `feat/v5-m4a-files-assets-bridge` at `3f3c5c5` (`git archive HEAD` into `%TEMP%\rbx15-m4a-t3-draft`, outside the repo; the repo stayed clean). .NET SDK 10.0.302.
  - Stand-ins for Tasks 1 and 2, written from interfaces.md alone (scratch only, never part of this plan):
    - Task 1: `DialogText` with the sheet's strings verbatim; the three `IFileDialogs` members (and `Win32FileDialogs` creating each dialog per call); `SettingsFolder` with `LoadFolder`/`SaveFolder`; `SaveChoice` with `AskSaveChanges`; `EditorSession` R2 (`IsDirty` against a saved document), `MarkSaved`, `Open`, `CommitPendingEdits`; `ProjectFileService` with the R4 order (read on `Task.Run`, alert on failure, then the guard, then the swap, then the dropped-layer status); `Composition.Build(..., ISettingsStore)`; `MainViewModel.Files`; the fakes' new members. `EditorSessionTests` line 346 flipped to `Assert.True`, as R2 says Task 1 does.
    - Task 2: `AssetScan` (dedupe, grouping, labels, summaries per §3.1), `PackSource`, an `AssetThumbnailLoader` that decodes on `Task.Run` and returns null on any failure, and `AssetLibraryViewModel`, `AssetRowViewModel` and `AssetTileViewModel` per §3.5 and R5. Tiles are made once per load and reused by a group switch; `Rows` is cleared and refilled one row at a time; the tile's `await` continues on the dispatcher and raises `PropertyChanged(nameof(ThumbnailFailed))`.
  - With the stand-ins alone: Engine.Tests 880, App.Tests 644, Parity.Tests 35, all green.
- **Build and tests with this task's code.**
  - `-warnaserror`: 0 warnings, 0 errors.
  - The filter `DialogInventoryTests` gave 31 passed.
  - The full suite three times in a row: Engine.Tests 880, App.Tests 675 (644 + 31), Parity.Tests 35, 0 failed and 0 skipped each time. On this desktop `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` passed (100% display).
  - `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` printed nothing.
- **Failure counts, measured by removing code:**
  - Step 2 (no `ThumbnailFailureReport.cs`): the build fails with two `CS0103: The name 'ThumbnailFailureReport' does not exist in the current context`, both at `DialogInventoryTests.cs(255,...)`.
  - Step 3 (the class, no Composition line): 2 failed, 29 passed. `A_set_with_unreadable_previews_...` fails with `Assert.Single() Failure: The collection was empty`; `A_new_set_reports_...` with `Assert.Equal() Failure: Values differ`.
  - Mutations of the class: dropping the once-per-set guard fails both preview facts; treating every rescan as a new set (so a group switch re-arms) fails `A_set_with_unreadable_previews_reports_once_and_a_group_switch_does_not_repeat_it` alone.
  - The first draft of `The_native_pickers_keep_no_dialog_between_calls` also required every field to be `readonly`. A primary constructor's captured parameter (`<owner>P`) is a mutable field, so that half was removed; the dialog-type half is the claim.
- **Not measured here:** Tasks 1 and 2's real code. Every member this task calls is the sheet's verbatim name, and Step 1 checks each one landed before Step 2 starts.

**Counts.** Cumulative from Task 2's end (Engine.Tests 938, App.Tests 770). Task 3 changes no engine file and adds App.Tests +31. Parity.Tests stays 35.

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| Task 2 (Step 1 baseline) | 938 | 770 | |
| Step 2 | build fails (CS0103 x2) | | the failing tests |
| Step 3 | 938 | 799 passed, 2 failed | `ThumbnailFailureReport` without its wiring |
| **Step 4** | **938** | **801** | DialogInventoryTests +31 |

- [ ] **Step 1: Baseline and the Task 1 and Task 2 preconditions**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 938`; Parity.Tests `Passed: 35`; App.Tests `Passed: 770`; `Failed: 0` everywhere.

On a display scaled to 125%, M3a's `CanvasViewTests.The_frame_scrolls_when_it_outgrows_the_area_and_centers_when_it_fits` fails before any Task 3 change. Run at 100% or report it as pre-existing; do not change it here.

Run each of these and check every one prints at least one line:

```bash
grep -n "PackNotFound\|PackEmpty\|NoImagesInFolder\|LoadFailed" src/RBX15.App/Services/DialogText.cs
grep -n "SaveChoice AskSaveChanges" src/RBX15.App/Services/IDialogService.cs
grep -n "OpenProject\|PickFolder" src/RBX15.App/Services/IFileDialogs.cs
grep -n "public async Task<bool> OpenAsync()\|public Task<bool> OpenAsync()" src/RBX15.App/Services/ProjectFileService.cs
grep -n "ThumbnailFailed" src/RBX15.App/ViewModels/AssetTileViewModel.cs
grep -n "new AssetThumbnailLoader());" src/RBX15.App/Composition.cs
grep -n "LoadFolder\|SaveFolder" src/RBX15.App.Tests/Fakes/FakeSettingsStore.cs
```

If any prints nothing, stop and report: that member is Task 1's or Task 2's and has not landed. The last `grep` on Composition.cs must print exactly one line; it is Step 4's anchor.

- [ ] **Step 2: Write the failing inventory tests**

Ledger: `dialogs-inventory`, `io-alert-inventory`, `diag-console-only-errors`, `file-input-same-file-reselect`. Create `src/RBX15.App.Tests/DialogInventoryTests.cs`:

```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;
using Point = System.Windows.Point;

namespace RBX15.App.Tests;

/// <summary>
/// The dialog and status inventory (ledger dialogs-inventory, io-alert-inventory, diag-console-only-errors,
/// file-input-same-file-reselect). 4.1 had five alert() sites and one confirm() (editor.js:1270, :1506, :2344,
/// :2364, :2385, :2676), and wrote every other failure to the developer console only. Each test names the 4.1
/// line it maps. The 4.1 strings are read from the untouched editor.js itself, so a typo on either side fails.
/// Dialogs and pickers are local recording fakes: no real dialog opens and no input is synthesized.
/// </summary>
public sealed class DialogInventoryTests : IDisposable
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string EditorJs = Path.Combine(Root, "editor.js");
    readonly string _dir = Directory.CreateTempSubdirectory("rbx15-m4a-t3-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ------------------------------------------------------------------ the six 4.1 sites, verbatim

    [Fact] // dialogs-inventory: "verify no other modal text appears" (the 4.1 side)
    public void Editor_js_has_exactly_the_six_dialog_sites_of_the_inventory()
    {
        var sites = File.ReadLines(EditorJs)
            .Select((text, i) => (text, line: i + 1))
            .Where(x => Regex.IsMatch(x.text, @"\b(alert|confirm|prompt)\("))
            .Select(x => x.line);
        Assert.Equal([1270, 1506, 2344, 2364, 2385, 2676], sites);
        foreach (var other in new[] { "editor.html", "main.js" })
        {
            string text = File.ReadAllText(Path.Combine(Root, other));
            Assert.DoesNotMatch(@"\b(alert|confirm|prompt)\(|showMessageBox|showErrorBox", text);
        }
    }

    [Theory] // dialogs-inventory, io-alert-inventory: "compare text exactly"
    [InlineData(1270, "eraser")]          // v5: the status strip (eraser-selection-semantics, closed in M3b)
    [InlineData(1506, "empty-export")]    // v5: the themed confirm (io-empty-export-confirm, closed in M3a)
    [InlineData(2344, "pack-not-found")]
    [InlineData(2364, "pack-empty")]
    [InlineData(2385, "no-images")]
    [InlineData(2676, "load-failed")]
    public void Each_4_1_message_is_kept_verbatim(int line, string site)
    {
        const string reason = "layers must be an array, not a string";
        var (bindings, v5) = site switch
        {
            "eraser" => (Bind(), CanvasInteraction.NoLayerToErase),
            "empty-export" => (Bind(), ExportGuard.EmptyTemplateMessage),
            "pack-not-found" => (Bind(("packName", PackSource.Drips)), DialogText.PackNotFound(PackSource.Drips)),
            "pack-empty" => (Bind(("packName", PackSource.WordArt)), DialogText.PackEmpty(PackSource.WordArt)),
            "no-images" => (Bind(), DialogText.NoImagesInFolder),
            "load-failed" => (Bind(("err.message", reason)), DialogText.LoadFailed(reason)),
            _ => throw new ArgumentOutOfRangeException(nameof(site)),
        };
        Assert.Equal(Evaluate41(line, bindings), v5);
    }

    [Fact] // dialogs-inventory: "verify no other modal text appears" (the v5 side)
    public void Every_v5_dialog_shows_an_inventory_string()
    {
        string app = Path.Combine(Root, "src", "RBX15.App");
        string sep = Path.DirectorySeparatorChar.ToString();
        var sources = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
            .ToList();
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            string text = File.ReadAllText(file);
            string name = Path.GetFileName(file);
            Assert.False(text.Contains("MessageBox.Show(", StringComparison.Ordinal), $"{name} opens a MessageBox");
            if (text.Contains("new ThemedDialog(", StringComparison.Ordinal)) Assert.Equal("ThemedDialogService.cs", name);
            foreach (Match m in Regex.Matches(text, @"\.(Alert|Confirm)\(\s*([A-Za-z_.]+)"))
            {
                string arg = m.Groups[2].Value;
                Assert.True(arg.StartsWith("DialogText.", StringComparison.Ordinal) || arg == "ExportGuard.EmptyTemplateMessage",
                    $"{name}: {m.Value} shows text outside the inventory");
                shown.Add(arg);
            }
        }
        Assert.Superset(new HashSet<string>
        {
            "ExportGuard.EmptyTemplateMessage", "DialogText.PackNotFound", "DialogText.PackEmpty",
            "DialogText.NoImagesInFolder", "DialogText.LoadFailed",
        }, shown);
    }

    // ------------------------------------------------------------------ the alerts, triggered

    [Fact] // io-alert-inventory, editor.js:2342-2346
    public void A_missing_built_in_pack_alerts_with_the_4_1_text() => StaThread.Run(() =>
    {
        var (library, dialogs, _) = Library(packsDirectory: _dir);
        library.LoadDripsCommand.Execute(null);
        Assert.Equal(["Built-in pack \"drips\" not found. Reinstall the app to restore it."], dialogs.Alerts);
        Assert.False(library.IsBrowserVisible);
    });

    [Fact] // io-alert-inventory, editor.js:2347-2366: the pack's folder holds no image
    public void An_empty_built_in_pack_alerts_with_the_4_1_text() => StaThread.Run(() =>
    {
        Directory.CreateDirectory(Path.Combine(_dir, PackSource.WordArt));
        File.WriteAllText(Path.Combine(_dir, PackSource.WordArt, "readme.txt"), "not an image");
        var (library, dialogs, _) = Library(packsDirectory: _dir);
        library.LoadWordArtCommand.Execute(null);
        Assert.Equal(["Built-in pack \"word_art\" is empty."], dialogs.Alerts);
        Assert.False(library.IsBrowserVisible);
    });

    [Fact] // io-alert-inventory, editor.js:2380-2387
    public void A_folder_with_no_images_alerts_with_the_4_1_text() => StaThread.Run(() =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(_dir, "notes")).FullName;
        File.WriteAllText(Path.Combine(folder, "todo.txt"), "x");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "sub", "logo.svg"), "<svg/>");
        var (library, dialogs, picks) = Library(packsDirectory: _dir);
        picks.Folders.Enqueue(folder);
        library.OpenFolderCommand.Execute(null);
        Assert.Equal(["No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files."], dialogs.Alerts);
        Assert.False(library.IsBrowserVisible);
    });

    [Fact] // diag-console-only-errors (new v5 path: the folder vanished between the pick and the read)
    public void A_folder_that_cannot_be_read_goes_to_the_status_strip_not_a_dialog() => StaThread.Run(() =>
    {
        var status = new FakeStatus();
        var (library, dialogs, picks) = Library(packsDirectory: _dir, status: status);
        picks.Folders.Enqueue(Path.Combine(_dir, "gone"));
        library.OpenFolderCommand.Execute(null);
        Assert.Empty(dialogs.Alerts);
        Assert.NotNull(status.Current);
        Assert.StartsWith("Couldn't open gone: ", status.Current);   // Task 2's FolderFailedMessage (T2-R8; critic L2)
        Assert.False(library.IsBrowserVisible);
    });

    [Theory] // io-alert-inventory parityCheck: a non-JSON file and a truncated JSON; editor.js:2675-2677
    [InlineData("not-json", "the file is not JSON: ")]
    [InlineData("truncated", "the file is not JSON: ")]
    [InlineData("missing", "Could not find file")]
    public void A_project_that_cannot_be_read_alerts_with_the_4_1_prefix_asks_nothing_and_changes_nothing(string kind, string reasonStart) => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, kind + ".r15proj");
        if (kind == "not-json") File.WriteAllText(path, "hello");
        if (kind == "truncated") File.WriteAllText(path, "{\"layers\":[");
        var (session, main, dialogs, picks) = Shell();
        using var red = Solid(SKColors.Red);
        Assert.True(session.ApplyWithSelection(s => LayerOps.AddImage(s, red, session.TakeId(), "Mine")));
        var before = session.Document;
        picks.Projects.Enqueue(path);

        Assert.False(Await(main.Files.OpenAsync()));

        string alert = Assert.Single(dialogs.Alerts);
        Assert.StartsWith("Failed to load project. " + reasonStart, alert);
        Assert.Empty(dialogs.SaveChanges);                   // a file that cannot open never asks to save (R4)
        Assert.Same(before, session.Document);
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.IsDirty);
        Assert.Null(session.CurrentFile);
        Assert.False(main.Files.IsBusy);
    });

    // ------------------------------------------------------------------ console-only failures, now on the status strip

    [Theory] // diag-console-only-errors: an image that does not decode, through every route that adds one
    [InlineData("add-image")]     // editor.js:2700-2703, no onerror
    [InlineData("zone-click")]    // editor.js:954, :958 -> handleFiles :1006 'Image load failed' (console)
    [InlineData("zone-drop")]     // editor.js:957 -> handleFiles :1006
    [InlineData("canvas-drop")]   // editor.js:985 -> handleFiles :1006
    [InlineData("asset-click")]   // editor.js:2502-2510, no onerror
    public void An_undecodable_image_is_named_on_the_status_strip_by_every_route(string route) => StaThread.Run(() =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(_dir, "art")).FullName;
        string bad = Path.Combine(folder, "broken.png");
        File.WriteAllBytes(bad, [1, 2, 3, 4, 5, 6, 7, 8]);
        var (session, main, dialogs, picks) = Shell();
        switch (route)
        {
            case "add-image": picks.Images.Enqueue([bad]); main.Home.File.AddImageCommand.Execute(null); break;
            case "zone-click": picks.Images.Enqueue([bad]); main.Layers.BrowseCommand.Execute(null); break;
            case "zone-drop": main.Layers.DropFiles([bad]); break;
            case "canvas-drop": main.Canvas.DropHandler!(new CanvasDrop([bad], new Point(100, 100))); break;
            case "asset-click":
                picks.Folders.Enqueue(folder);
                main.Assets.OpenFolderCommand.Execute(null);
                main.Assets.AddCommand.Execute(Assert.Single(main.Assets.Rows.SelectMany(r => r.Tiles)));
                break;
        }
        Assert.Equal("Couldn't add broken.png: The file isn't a readable image, or it is damaged.", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
    });

    [Theory] // diag-console-only-errors: handleFiles' three console lines (editor.js:1000, :1006, :1009)
    [InlineData("notes.txt", "Not a supported image type. Use PNG, JPEG, WebP, BMP or GIF.")]   // :1000 'Skipped non-image'
    [InlineData("broken.png", "The file isn't a readable image, or it is damaged.")]            // :1006 'Image load failed'
    [InlineData("locked.png", "The file couldn't be read.")]                                     // :1009 'FileReader failed'
    public void Each_console_only_import_failure_has_its_status_text(string fileName, string reason) => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, fileName == "locked.png" ? Encoded(SKColors.Red) : [1, 2, 3, 4]);
        var (session, main, dialogs, _) = Shell();
        using (fileName == "locked.png" ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None) : null)
            main.Layers.DropFiles([path]);
        Assert.Equal($"Couldn't add {fileName}: {reason}", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
        Assert.Empty(session.Document.Layers);
    });

    [Fact] // diag-console-only-errors, project-load-error-path-bugs: 4.1's layer img.onerror only counted (editor.js:2669-2672)
    public void A_project_layer_that_cannot_load_is_named_on_the_status_strip() => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, "half.r15proj");
        File.WriteAllText(path, "{\"layers\":[{\"id\":1,\"name\":\"Bad\",\"imgSrc\":\"data:image/png;base64,AAAA\"}]}");
        var (session, main, dialogs, picks) = Shell();
        picks.Projects.Enqueue(path);
        Assert.True(Await(main.Files.OpenAsync()));
        Assert.Equal("Opened half.r15proj. Couldn't load: Bad.", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(path, session.CurrentFile);
    });

    [Fact] // diag-console-only-errors: a preview that cannot be shown (editor.js:2473-2475, no onerror)
    public void A_set_with_unreadable_previews_reports_once_and_a_group_switch_does_not_repeat_it() => StaThread.Run(() =>
    {
        string folder = AssetFolder();
        var (_, main, _, picks) = Shell();
        var messages = Messages(main.Status);
        picks.Folders.Enqueue(folder);
        main.Assets.OpenFolderCommand.Execute(null);
        Assert.Equal(4, main.Assets.GroupButtons.Count);      // All, a, b, c: the subfolder buttons show
        LoadThumbnails(main.Assets);

        var report = Assert.Single(messages);
        Assert.Contains(report, new[] { ThumbnailFailureReport.Message("bad1.png"), ThumbnailFailureReport.Message("bad2.png") });

        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons.Single(g => g.Key == "b"));
        LoadThumbnails(main.Assets);
        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons.Single(g => g.Key == AssetScan.AllKey));
        LoadThumbnails(main.Assets);
        Assert.Single(messages);
    });

    [Fact] // diag-console-only-errors: a new load is a new set, so its failures report again
    public void A_new_set_reports_its_unreadable_previews_again() => StaThread.Run(() =>
    {
        string folder = AssetFolder();
        var (_, main, _, picks) = Shell();
        var messages = Messages(main.Status);
        picks.Folders.Enqueue(folder);
        picks.Folders.Enqueue(folder);
        main.Assets.OpenFolderCommand.Execute(null);
        LoadThumbnails(main.Assets);
        main.Assets.OpenFolderCommand.Execute(null);
        LoadThumbnails(main.Assets);
        Assert.Equal(2, messages.Count);
    });

    // ------------------------------------------------------------------ the same file, picked again

    [Theory] // file-input-same-file-reselect: 4.1's drop-zone input never reset its value (editor.js:958)
    [InlineData("add-image")]     // #imageLoadInput reset its value (editor.js:2707)
    [InlineData("zone-click")]    // #fileInput did not (editor.js:954, :958): the 4.1 bug
    [InlineData("open-project")]  // #projectFileInput reset its value (editor.js:2686)
    [InlineData("open-folder")]   // #folderInput reset its value (editor.js:2316)
    public void Picking_the_same_file_again_acts_again(string route) => StaThread.Run(() =>
    {
        var (session, main, dialogs, picks) = Shell();
        string image = Path.Combine(_dir, "star.png");
        File.WriteAllBytes(image, Encoded(SKColors.Red));
        switch (route)
        {
            case "add-image":
            case "zone-click":
                picks.Images.Enqueue([image]);
                picks.Images.Enqueue([image]);
                for (int i = 0; i < 2; i++)
                    (route == "add-image" ? main.Home.File.AddImageCommand : main.Layers.BrowseCommand).Execute(null);
                Assert.Equal(["star", "star"], session.Document.Layers.Select(l => l.Name));
                Assert.Equal(2, session.UndoCount);
                Assert.Equal(2, picks.ImageCalls);
                break;
            case "open-project":
                string project = Path.Combine(_dir, "shirt.r15proj");
                using (var red = Solid(SKColors.Red))
                {
                    var seed = new EditorSession();
                    seed.ApplyWithSelection(s => LayerOps.AddImage(s, red, seed.TakeId(), "Star"));
                    File.WriteAllText(project, ProjectIO.Write(seed.Document, DateTimeOffset.UnixEpoch));
                }
                picks.Projects.Enqueue(project);
                picks.Projects.Enqueue(project);
                Assert.True(Await(main.Files.OpenAsync()));
                double x = session.Document.Layers[0].X;
                Assert.True(session.Apply(d => LayerOps.MoveTo(d, d.Layers[0].Id, x + 40, 0)));
                dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
                Assert.True(Await(main.Files.OpenAsync()));
                Assert.Equal(["shirt.r15proj"], dialogs.SaveChanges);
                Assert.Equal(x, session.Document.Layers[0].X);       // the edit is gone: the file was read again
                Assert.False(session.IsDirty);
                Assert.Equal(0, session.UndoCount);
                Assert.Equal(project, session.CurrentFile);
                Assert.Equal(2, picks.ProjectCalls);
                break;
            case "open-folder":
                string folder = AssetFolder();
                picks.Folders.Enqueue(folder);
                picks.Folders.Enqueue(folder);
                main.Assets.OpenFolderCommand.Execute(null);
                var first = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
                main.Assets.OpenFolderCommand.Execute(null);
                var second = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
                Assert.Equal(first.Select(t => t.Entry.FileName), second.Select(t => t.Entry.FileName));
                Assert.DoesNotContain(second, t => first.Contains(t));   // a fresh set, not the old one kept
                Assert.Equal("4 unique images (0 duplicates removed)", main.Assets.CountText);
                Assert.Equal(2, picks.FolderCalls);
                break;
        }
        Assert.Empty(dialogs.Alerts);
    });

    [Fact] // file-input-same-file-reselect: no picker instance outlives a call, so nothing can remember a pick
    public void The_native_pickers_keep_no_dialog_between_calls()
    {
        var fields = typeof(Win32FileDialogs).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(fields, f => typeof(Microsoft.Win32.CommonDialog).IsAssignableFrom(f.FieldType));
    }

    // ------------------------------------------------------------------ helpers

    static Dictionary<string, string> Bind(params (string Name, string Value)[] pairs) => pairs.ToDictionary(p => p.Name, p => p.Value);

    /// <summary>
    /// The text 4.1 shows at editor.js:<paramref name="line"/>: the argument of its alert( or confirm(, a chain of
    /// single-quoted literals and identifiers joined by +, with each identifier taken from <paramref name="bindings"/>.
    /// </summary>
    static string Evaluate41(int line, IReadOnlyDictionary<string, string> bindings)
    {
        string src = File.ReadLines(EditorJs).ElementAt(line - 1);
        var call = Regex.Match(src, @"\b(alert|confirm)\(");
        Assert.True(call.Success, $"editor.js:{line} holds no alert( or confirm(");
        int i = call.Index + call.Length;
        var text = new StringBuilder();
        while (true)
        {
            while (src[i] == ' ') i++;
            if (src[i] == '\'')
            {
                for (i++; src[i] != '\''; i++)
                {
                    if (src[i] == '\\') i++;
                    text.Append(src[i]);
                }
                i++;
            }
            else
            {
                string id = Regex.Match(src[i..], @"^[A-Za-z_$][\w$.]*").Value;
                Assert.True(id.Length > 0, $"editor.js:{line}: unexpected '{src[i]}' at column {i + 1}");
                Assert.True(bindings.TryGetValue(id, out var value), $"editor.js:{line}: no binding for {id}");
                text.Append(value);
                i += id.Length;
            }
            while (src[i] == ' ') i++;
            if (src[i] == ')') return text.ToString();
            Assert.Equal('+', src[i]);
            i++;
        }
    }

    (EditorSession Session, MainViewModel Main, RecordingDialogs Dialogs, ScriptedPicks Picks) Shell()
    {
        var session = new EditorSession();
        var dialogs = new RecordingDialogs();
        var picks = new ScriptedPicks();
        var main = Composition.Build(session, picks, dialogs, new FakeColorPicker(), new FakeSettingsStore());
        return (session, main, dialogs, picks);
    }

    static (AssetLibraryViewModel Library, RecordingDialogs Dialogs, ScriptedPicks Picks) Library(string packsDirectory, FakeStatus? status = null)
    {
        status ??= new FakeStatus();
        var dialogs = new RecordingDialogs();
        var picks = new ScriptedPicks();
        var library = new AssetLibraryViewModel(new ImportService(new EditorSession(), status), picks, dialogs, status,
            new FakeSettingsStore(), new PackSource(packsDirectory), new AssetThumbnailLoader());
        return (library, dialogs, picks);
    }

    /// <summary>root/a/bad1.png, root/b/bad2.png, root/b/good.png, root/c/good2.png: two previews that cannot show.</summary>
    string AssetFolder()
    {
        string root = Path.Combine(_dir, "assets");
        foreach (var sub in new[] { "a", "b", "c" }) Directory.CreateDirectory(Path.Combine(root, sub));
        File.WriteAllBytes(Path.Combine(root, "a", "bad1.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(root, "b", "bad2.png"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(root, "b", "good.png"), Encoded(SKColors.Blue));
        File.WriteAllBytes(Path.Combine(root, "c", "good2.png"), Encoded(SKColors.Lime));
        return root;
    }

    /// <summary>Reads every shown tile's Thumbnail, as a realized row does (R5), and pumps until each has loaded or failed.</summary>
    static void LoadThumbnails(AssetLibraryViewModel library)
    {
        var tiles = library.Rows.SelectMany(r => r.Tiles).ToList();
        foreach (var tile in tiles) _ = tile.Thumbnail;
        PumpUntil(() => tiles.All(t => t.Thumbnail is not null || t.ThumbnailFailed));
    }

    static List<string> Messages(StatusViewModel status)
    {
        var shown = new List<string>();
        status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusViewModel.Message) && status.Message is { } m) shown.Add(m);
        };
        return shown;
    }

    /// <summary>Runs the STA dispatcher until <paramref name="task"/> completes: its continuations post to it.</summary>
    static T Await<T>(Task<T> task)
    {
        PumpUntil(() => task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    static void PumpUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.ElapsedMilliseconds > 10_000) throw new TimeoutException("the dispatcher work did not finish in 10 s");
            StaThread.Pump();
            Thread.Sleep(5);
        }
    }

    static SKImage Solid(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    static byte[] Encoded(SKColor color)
    {
        using var image = Solid(color);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Records every dialog; AskSaveChanges answers <see cref="SaveChoiceAnswer"/>.</summary>
    sealed class RecordingDialogs : IDialogService
    {
        public List<string> Alerts { get; } = [];
        public List<string> Confirms { get; } = [];
        public List<string> SaveChanges { get; } = [];
        public SaveChoice SaveChoiceAnswer { get; set; } = SaveChoice.Cancel;

        public void Alert(string message) => Alerts.Add(message);

        public bool Confirm(string message)
        {
            Confirms.Add(message);
            return false;
        }

        public SaveChoice AskSaveChanges(string documentName)
        {
            SaveChanges.Add(documentName);
            return SaveChoiceAnswer;
        }
    }

    /// <summary>Answers each picker from its own queue (an empty queue is a cancel) and counts the calls.</summary>
    sealed class ScriptedPicks : IFileDialogs
    {
        public Queue<IReadOnlyList<string>> Images { get; } = new();
        public Queue<string> Projects { get; } = new();
        public Queue<string> Folders { get; } = new();
        public int ImageCalls { get; private set; }
        public int ProjectCalls { get; private set; }
        public int FolderCalls { get; private set; }

        public IReadOnlyList<string> OpenImages()
        {
            ImageCalls++;
            return Images.TryDequeue(out var paths) ? paths : [];
        }

        public string? SavePng(string suggestedFileName) => null;

        public string? OpenProject(string? initialDirectory)
        {
            ProjectCalls++;
            return Projects.TryDequeue(out var path) ? path : null;
        }

        public string? SaveProject(string suggestedFileName, string? initialDirectory) => null;

        public string? PickFolder(string? initialDirectory)
        {
            FolderCalls++;
            return Folders.TryDequeue(out var path) ? path : null;
        }
    }
}
```

Notes on the code:

- `Evaluate41` reads the 4.1 line itself and joins its single-quoted literals and bound identifiers, so the comparison is against the shipped text, not a copy of it. The six lines are stable because `editor.js` is never edited (global constraint).
- `PumpUntil` runs the test STA dispatcher, which the tile's and `OpenAsync`'s continuations post to, so nothing blocks on a `Task` the dispatcher must finish. The 10 s ceiling is a hang guard; measured runs finish in milliseconds.
- `locked.png` is held open with `FileShare.None`, so `File.ReadAllBytes` throws `IOException` as a read failure would. It is a real PNG, so only the read fails.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails with two `error CS0103: The name 'ThumbnailFailureReport' does not exist in the current context`, both in `DialogInventoryTests.cs` at line 255. Any other error means a Task 1 or Task 2 member differs from interfaces.md: stop and report it.

- [ ] **Step 3: Add the preview failure report**

Ledger: `diag-console-only-errors` (T3-R3). Create `src/RBX15.App/ViewModels/ThumbnailFailureReport.cs`:

```csharp
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// Says on the status strip that an asset preview could not be shown (ledger diag-console-only-errors). 4.1 set
/// each thumbnail's <c>img.src</c> to a blob URL with no <c>onerror</c> (editor.js:2473-2475), so an unreadable or
/// oversized file showed a broken image and nothing else. v5 shows one line per loaded set, naming the first file
/// that failed, so a folder of broken files never floods the strip or the screen reader's live region.
/// <para>
/// A set is the tiles one pack or folder load made. A group switch reuses them (interfaces R5), so it never
/// reports again; a new load brings tiles never seen before, which starts a new set and re-arms the report.
/// A failure from a tile of an earlier set is ignored. UI thread only.
/// </para>
/// </summary>
internal sealed class ThumbnailFailureReport
{
    readonly IStatus _status;
    readonly HashSet<AssetTileViewModel> _set = new(ReferenceEqualityComparer.Instance);
    bool _reported;

    ThumbnailFailureReport(AssetLibraryViewModel library, IStatus status)
    {
        _status = status;
        library.Rows.CollectionChanged += OnRowsChanged;
        Rescan(library.Rows);
    }

    /// <summary>Starts reporting for <paramref name="library"/>. Composition calls it once, beside the library's construction.</summary>
    public static ThumbnailFailureReport Attach(AssetLibraryViewModel library, IStatus status)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(status);
        return new ThumbnailFailureReport(library, status);
    }

    /// <summary>The status line. Clicking the tile imports the file, and the import names the exact reason.</summary>
    public static string Message(string fileName) => $"Couldn't show a preview for {fileName}. Click its tile to see why.";

    void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var rows = (IList)sender!;
        // Rows appended to rows already showing belong to the set on screen; anything else is checked from scratch.
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } added && rows.Count > added.Count)
            Watch(Tiles(added), sameSet: true);
        else
            Rescan(rows);
    }

    void Rescan(IList rows)
    {
        var tiles = Tiles(rows);
        if (tiles.Count == 0) return;                    // a Clear before the next group or set fills the rows
        Watch(tiles, sameSet: tiles.Exists(_set.Contains));
    }

    static List<AssetTileViewModel> Tiles(IList rows) => rows.Cast<AssetRowViewModel>().SelectMany(r => r.Tiles).ToList();

    void Watch(List<AssetTileViewModel> tiles, bool sameSet)
    {
        if (!sameSet)
        {
            foreach (var old in _set) old.PropertyChanged -= OnTileChanged;
            _set.Clear();
            _reported = false;
        }
        foreach (var tile in tiles)
        {
            if (!_set.Add(tile)) continue;
            tile.PropertyChanged += OnTileChanged;
            if (tile.ThumbnailFailed) Report(tile);
        }
    }

    void OnTileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetTileViewModel.ThumbnailFailed) or null or ""
            && sender is AssetTileViewModel { ThumbnailFailed: true } tile && _set.Contains(tile))
            Report(tile);
    }

    void Report(AssetTileViewModel tile)
    {
        if (_reported) return;
        _reported = true;
        _status.Show(Message(tile.Entry.FileName));
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.DialogInventoryTests"`
Expected: `Failed!  - Failed:     2, Passed:    29, Skipped:     0, Total:    31` for `RBX15.App.Tests.dll`. The two failures are `A_set_with_unreadable_previews_reports_once_and_a_group_switch_does_not_repeat_it` (`Assert.Single() Failure: The collection was empty`) and `A_new_set_reports_its_unreadable_previews_again` (`Assert.Equal() Failure: Values differ`): nothing attaches the report yet.

If any other test fails, it pins behavior Tasks 1 and 2 own (a string, a routing, a re-pick). Report the failure with its message to the controller; do not edit the test.

- [ ] **Step 4: Attach the report in the composition root**

Ledger: `diag-console-only-errors`. In `src/RBX15.App/Composition.cs`, after the line

```csharp
        var assets = new AssetLibraryViewModel(import, fileDialogs, dialogs, status, settings, new PackSource(PackSource.DefaultDirectory), new AssetThumbnailLoader());
```

insert:

```csharp
        // M4a Task 3: one status line per asset set whose previews fail (diag-console-only-errors; editor.js:2473-2475).
        ThumbnailFailureReport.Attach(assets, status);
```

The anchor is the sheet's §3.10 line, which Step 1 found exactly once. If Task 2 wrapped or renamed it, insert the two lines directly after the statement that assigns `assets`, and say so in the report.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.DialogInventoryTests"`
Expected: `Passed!  - Failed:     0, Passed:    31, Skipped:     0, Total:    31` for `RBX15.App.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 938`; Parity.Tests `Passed: 35`; App.Tests `Passed: 801`; `Failed: 0` and `Skipped: 0` everywhere.

- [ ] **Step 5: Verify the task**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run the suite three times in a row: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected each time: Engine.Tests `Passed: 938`; App.Tests `Passed: 801`; Parity.Tests `Passed: 35`; `Failed: 0` and `Skipped: 0` everywhere. The preview facts depend on a background decode, so a failure in only one of the three runs is a flake to report, not to retry away.

Run: `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src`
Expected: no output.

Run: `git status --short`
Expected exactly:

```text
 M src/RBX15.App/Composition.cs
?? src/RBX15.App.Tests/DialogInventoryTests.cs
?? src/RBX15.App/ViewModels/ThumbnailFailureReport.cs
```

- [ ] **Step 6: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint: each commit ends with the trailer of the model that made it).

```bash
git add src/RBX15.App/ViewModels/ThumbnailFailureReport.cs src/RBX15.App/Composition.cs src/RBX15.App.Tests/DialogInventoryTests.cs
git commit -m "feat(app): pin the dialog and status inventory, report asset preview failures once per set"
```

Run: `git status --short`
Expected: empty.

**Handoff to Task 5** (the ledger close; this task closes no row):

1. `dialogs-inventory`: evidence is `Editor_js_has_exactly_the_six_dialog_sites_of_the_inventory`, `Each_4_1_message_is_kept_verbatim` (6 rows), `Every_v5_dialog_shows_an_inventory_string` and the triggered alerts (`A_missing_built_in_pack_...`, `An_empty_built_in_pack_...`, `A_folder_with_no_images_...`, `A_project_that_cannot_be_read_...`), plus M3a's `LedgerScenarioTests` export-confirm scenario and M3b's eraser scenario. The parityCheck says "trigger each of the 6 messages"; the triggers here are view-model level with recording dialogs, so the themed window itself is shown only by the bridge smoke (`brg files`). Divergence wording to add: "five of the six as themed dialogs titled with the product name; the eraser's message on the status strip at the press (eraser-selection-semantics); one new dialog, the unsaved-changes prompt (R13)".
2. `diag-console-only-errors`: proven for editor.js:1000-1009, :2473-2475, :2502-2510, :2669-2672 and :2700-2703 by `An_undecodable_image_is_named_on_the_status_strip_by_every_route` (5 rows), `Each_console_only_import_failure_has_its_status_text` (3 rows), `A_project_layer_that_cannot_load_is_named_on_the_status_strip` and the two preview facts. editor.js:935-946 (background image; the ledger cites :940-944) is M4b's: narrow the row to M4a's routes ("Narrowed in M4a") or hold it open for M4b. The row's check is `manual`; the drop of a renamed `.txt` its parityCheck names is the `zone-drop` route with `notes.txt`.
3. `file-input-same-file-reselect`: the parityCheck ("pick the same image twice via the drop-zone click and count the layers") is `Picking_the_same_file_again_acts_again("zone-click")`, through the real composition with the pick scripted. The background picker half of the row's behavior is M4b's.
4. New v5 status text for the sign-off list: `Couldn't show a preview for <file>. Click its tile to see why.` (T3-R3, provisional).
5. `io-alert-inventory`: its parityCheck is met word for word by `A_project_that_cannot_be_read_alerts_with_the_4_1_prefix_asks_nothing_and_changes_nothing` (`not-json`, `truncated`) plus the verbatim theory.

**Additions requested by Task 3** (not in interfaces.md; each is used by the code above):

1. `internal sealed class ThumbnailFailureReport` in `RBX15.App.ViewModels`: `public static ThumbnailFailureReport Attach(AssetLibraryViewModel library, IStatus status)` and `public static string Message(string fileName)`, returning `$"Couldn't show a preview for {fileName}. Click its tile to see why."` (T3-R3).
2. Composition (§3.10) gains, after Task 2's `assets` line: `ThumbnailFailureReport.Attach(assets, status);` with its comment.
3. **Requirements on Task 2's code** (no new member; the report depends on them, and the preview facts fail if they do not hold):
   - `AssetTileViewModel.ThumbnailFailed` raises `PropertyChanged(nameof(ThumbnailFailed))` on the UI thread when it becomes true.
   - Tiles are created once per pack or folder load and reused by a group switch (R5 says so; the report keys a set on tile identity).
   - `AssetLibraryViewModel.Rows` is changed in place (Clear and Add, or a Reset), never replaced, so its `CollectionChanged` reaches the report.
4. **Requirement on Tasks 1 and 2's call sites:** every `IDialogService.Alert` and `Confirm` call passes a `DialogText.*` member (or `ExportGuard.EmptyTemplateMessage`) directly as its argument, never a local variable (T3-R7; the sheet's own examples already do).
5. **Requirement on Task 1's `Win32FileDialogs`:** each picker creates its dialog per call; no dialog is kept in a field (T3-R4).
6. **Rejected (critic L2):** the drafter proposed `Couldn't read <folder>: <reason>`. Task 2's `AssetLibraryViewModel.FolderFailedMessage` already fixes the text as `Couldn't open <folder>: <reason>` (T2-R8), and `A_folder_that_cannot_be_read_goes_to_the_status_strip_not_a_dialog` now asserts `Assert.StartsWith("Couldn't open gone: ", status.Current)`. No count change.
7. **No change to `DialogText.LoadFailed`** (T3-R2): the sheet's `"Failed to load project. " + message` stands, raw.

---

### Task 4: Canvas UI Automation (layer children, SelectionItem, Transform) and the test input bridge

**Files:**

- Create (app, every build):
  - `src/RBX15.App/Controls/ICanvasLayerAutomation.cs`
  - `src/RBX15.App/Controls/LayerAutomationPeer.cs`
  - `src/RBX15.App/Controls/PointerState.cs`
- Create (app, compiled only with `EnableTestInputBridge=true`; also compiled into the test assembly), all in `src/RBX15.App/Testing/`:
  - `BridgeJson.cs`: holds `BridgePoint`, `BridgeCommand`, `BridgeResponse`, `IInputBridge` and `BridgeJson`.
  - `BridgeKeyboardDevice.cs`
  - `RawMouse.cs`
  - `BridgeFileDialogs.cs`
  - `InputBridgeDispatcher.cs`: also holds `BridgeException`.
  - `TestInputBridge.cs`
- Modify (app):
  - `src/RBX15.App/Controls/TemplateCanvasHostAutomationPeer.cs`, printed in full.
  - `src/RBX15.App/Controls/TemplateCanvasHost.cs`: the `LayerAutomation` DP, Document-change and selection notification, and `ScreenBounds`.
  - `src/RBX15.App/ViewModels/CanvasViewModel.cs`: implements `ICanvasLayerAutomation`.
  - `src/RBX15.App/Views/CanvasView.xaml`: one attribute, `LayerAutomation="{Binding}"`.
  - `src/RBX15.App/Views/LayersPanel.xaml.cs`, `src/RBX15.App/Views/ColorPickerPopup.xaml.cs`, `src/RBX15.App/Controls/SliderGestureTracker.cs` and Task 2's `src/RBX15.App/Views/AssetLibraryPanel.xaml.cs` (one line, critic C2): modifiers from `e.KeyboardDevice`, and the left button through `PointerState` (R7, R8).
  - `src/RBX15.App/App.xaml.cs`, printed in full: the two `#if TEST_INPUT_BRIDGE` hook sites.
  - `src/RBX15.App/RBX15.App.csproj`: the flag, the exclusion and the publish refusal.
  - `src/RBX15.App.Tests/RBX15.App.Tests.csproj`: the `Bridge` link of `Testing/**`.
- Create (tests): `src/RBX15.App.Tests/CanvasLayerAutomationTests.cs` (11), `InputBridgeTests.cs` (16), `BridgeSecurityTests.cs` (27), `BridgeExclusionTests.cs` (4).
- Modify (tests): `src/RBX15.App.Tests/CanvasAutomationPeerTests.cs` (+1).
- Read only:
  - 4.1: `editor.html`, `editor.js` and `main.js`.
  - `docs/port/parity-ledger.json`. This task closes no row; Task 5 does.
- Not touched:
  - The engine.
  - `Composition.cs`, `MainWindow.xaml(.cs)` and `TestShell.cs`: Task 1's shape is consumed as is.
  - The ledger and `divergences.md`.
  - `.superpowers/m4a-smoke/` (Task 5).

**Interfaces:**

- Consumes:
  - **Task 1** (interfaces §3.2, §3.3, §3.5, §3.6, §3.10):
    - `ProjectFileService.IsBusy` through `MainViewModel.Files`.
    - `MainWindow.AddShortcut`, which wraps a command in `TextFieldShortcutCommand(command, () => FocusedElement())`, and the window's Ctrl+S, Ctrl+Shift+S and Ctrl+O bindings to `Home.File.SaveCommand`, `SaveAsCommand` and `OpenCommand`.
    - `IFileDialogs.OpenProject(string?)`, `SaveProject(string, string?)` and `PickFolder(string?)`.
    - `Composition.Build(EditorSession, IFileDialogs, IDialogService, IColorPicker, ISettingsStore)` and `Composition.Build(EditorSession, Func<Window?>, ISettingsStore)`.
    - `EditorSession.MarkSaved(string, Document)`: InputBridgeTests' rig marks the session saved before Close, so Task 1's close prompt never opens.
    - `JsonSettingsStore(string)`, `ThemedDialogService(Func<Window?>)` and `ThemedColorPicker(Func<Window?>, …)`.
  - **Task 2:** `AssetLibraryPanel.LeftDown` (Step 5's one-line edit, critic C2), `MainViewModel.Assets.LoadWordArtCommand` and the tile's `x:Name="AssetTile"` (an `InputBridgeTests` fact), and `LayersPanel.IsDragging` with `LayerRowViewModel.IsDragSource` and `DropIndicator` (M3b; `inspect` `layers`). The smoke drives `AssetLibraryPanel` by its AutomationIds through `{"element":…}`.
  - **M3b:**
    - `CanvasInteraction`: `Gesture`, `Wheel` and `EndWheelBurst`.
    - `TemplateCanvasHost`: `HandleWheel`, `FlushWheelBurst`, `WheelBurstPending`, `RenderingHooked`, `DeviceScale`, `Zoom` and `Interaction`.
    - `CanvasCoordinates.ToFrame`/`ToTemplate`, `SelectionGeometry.Corner`/`Center`/`MaxScale` and `LayerOps.MoveTo`/`ScaleAboutCenter`/`SetRotation`/`ToggleVisibility`.
    - `ToolsViewModel.Set`.
    - `ThemedColorPicker.IsClickOnDisabledOwner`, internal and reached through IVT by one test.
    - `LayersPanel.LayerList`, `MainWindow.FocusedElement` and `MainWindow.ViewModel`.
    - Test helpers: `StaThread`, `TestShell.Main`, `HomeTestKit.SessionWith`/`LayerOf`, `FakeSettingsStore`, `FakeColorPicker`, `FakeStatus` and `RepoRoot.Find`.
- Produces (interfaces §3.3, §3.7, §3.8; "+" marks an addition listed at the end of this slice):
  - `PointerState`: `LeftButtonOverride`, `LeftButton(MouseEventArgs)` and `LeftButtonDown()`.
  - `ICanvasLayerAutomation`. `CanvasViewModel : ICanvasLayerAutomation`, and its `SelectedLayerId` raises PropertyChanged (+).
  - `TemplateCanvasHost.LayerAutomation` (DP) and `internal Rect ScreenBounds(IEnumerable<Point>)` (+).
  - `TemplateCanvasHostAutomationPeer : ISelectionProvider`, with the internal members `Host`, `LayerPeer(int)`, `Provider()`, `OnLayersChanged()` and `OnSelectionChanged()` (+).
  - `LayerAutomationPeer : AutomationPeer, ISelectionItemProvider, ITransformProvider`, with a public `LayerId` and an internal constructor (+).
  - `RBX15.App.Testing`: everything in §3.8, plus the following (+):
    - `BridgeException`;
    - `InputBridgeDispatcher.AppExceptionPrefix`;
    - the internal `InputBridgeDispatcher(MainWindow, BridgeFileDialogs?)`, `PhysicalModifiers`, `BusyReason()`, `Neutralize(string, bool)` and `FindElement(string)`;
    - `RawMouse.Inject(…, nint extraInformation = Marker)`, `RawMouse.Create`, `RawMouse.TryRead` and `RawMouse.PreviewInputReportEvent`;
    - `BridgeFileDialogs(TextWriter log)` and `BridgeFileDialogs.LogPrefix`;
    - the internal `TestInputBridge.Start(MainWindow, TextWriter, BridgeFileDialogs?)`, `Listen(IInputBridge)`, `Served`, `LiveSecurity()`, `MaxLineBytes` and `NewPipeName()`.
  - The csproj property `EnableTestInputBridge`, the constant `TEST_INPUT_BRIDGE` and the target `RefuseTestInputBridgeOnPublish` (+).
  - Test classes:
    - `CanvasLayerAutomationTests` (11);
    - `InputBridgeTests` (16: 14 facts plus a theory with 2 rows);
    - `BridgeSecurityTests` (27: 7 facts plus 3 theories with 4, 11 and 7 rows; the sheet names two facts);
    - `BridgeExclusionTests` (4; the sheet names two);
    - `CanvasAutomationPeerTests` (+1).

**What 4.1 does (cited).** These are the shipped `editor.html`, `editor.js` and `main.js`, 4.1, untouched.

- **The canvas.** `<canvas id="mainCanvas" width="585" height="559">` (editor.html:330) has no role, no name and no children. A layer exists only as pixels, so no assistive technology can find, select or move one (ledger `a11y-focus-rings`: "No tabindex, ARIA roles, aria-labels … exist anywhere").
- **What a layer pick does.** `selectLayer(id)` sets `selectedLayerId` and `setTool('select')` (editor.js:575-580). `addLayer` does the same (editor.js:522-523). The UIA Select uses the same pair.
- **The transforms the patterns stand for:**
  - Move: the drag sets `x = layerStart.x + (mx - dragStart.x)` (editor.js:1169-1171), and the X and Y fields set `sel.x = +value` (editor.js:825-826). The pattern sets the top-left in template pixels, which are the fields' units.
  - Resize: the corner drag scales uniformly about the center. `scale = max(0.01, startScale * ratio)`, then `x = c.x - w/2` and `y = c.y - h/2` (editor.js:1181-1190). v5 applies the one 1-500% clamp (`props-scale-clamp-inconsistency`, done).
  - Rotate: the drag sets an absolute angle (editor.js:1172-1180), and v5 normalizes it to -180..180 (`select-rotate-drag`, fix). UIA Rotate is relative by definition, so the pattern adds to the current angle.
  - Corners: `[[-w/2,-h/2],[w/2,-h/2],[w/2,h/2],[-w/2,h/2]]` rotated about the center (editor.js:1080-1088). The element's bounding box is the box around these four points.
- **Keys** (editor.js:1775-1830): every shortcut reads `e.ctrlKey`, `e.shiftKey` and `e.key` from the event itself. v5's handlers read modifiers from `e.KeyboardDevice`, the event's own device, for the same reason.
- **No test or automation hook.** `main.js:1-39` reads no command line and opens no channel. `nodeIntegration: true, contextIsolation: false` (main.js:9-11) meant anything that reached DevTools could script the page. The spec removes that path (§4: "Electron's hidden accelerators (Ctrl+R, Ctrl+Shift+I) are gone"). v5's only automation channel is this flag-gated bridge, absent from shipped builds.

**Ledger rows this task serves.** Task 4 closes no row (interfaces §5). It supplies the evidence Task 5 uses under the honesty rule.

- `a11y-focus-rings` (fix, open): layers become automation elements with names, and a keyboard or screen-reader user can select and move them. The row stays open for M6's accessibility pass.
- `undo-empty-steps` (fix): a pattern call that changes nothing records nothing.
- `select-move`, `canvas-select-gestures` and `select-rotate-drag` (fix): the pattern semantics match the gestures, one step each.
- Bridge evidence for Task 5's 23 held rows, with the R7/R8 call sites changed here:
  - `layers-reorder-drag` and `layers-reorder-drag-2` (LayersPanel reads `PointerState` and `e.KeyboardDevice.Modifiers`);
  - `fillstyle-color-pickers` (the picker reads `e.KeyboardDevice.Modifiers`; a disabled-owner click goes through WM_SETCURSOR);
  - `canvas-mouse-button-agnostic` (right and middle presses start nothing);
  - `select-move`, `draw-release-outside-canvas`, `wheel-scale-undo-burst` and `wheel-scale-layer` (drag, release outside and the burst through the bridge).
- `kbd-shortcuts` and `undo-buttons-shortcuts` (fix): the bridge proves the chord path. Ctrl+S runs its binding exactly once, and a focused text field blocks it.

**Rulings** (each pinned by a named test or a measured fact):

- **T4-R1: the server does not pass `PipeOptions.CurrentUserOnly`.** Interfaces §3.8 combines `CurrentUserOnly` with a custom `PipeSecurity`, and .NET 10 refuses that: `ArgumentException: 'pipeSecurity' must be null when 'options' contains 'PipeOptions.CurrentUserOnly'` (measured).
  - The server passes `PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance` with `CreatePipeSecurity()`. That ACL is stricter than CurrentUserOnly's own: protected, plus a NETWORK deny.
  - `FirstPipeInstance` makes Create fail if the name already exists, so a squatter cannot pre-create it.
  - Clients connect with `PipeOptions.CurrentUserOnly`, which checks that the server's owner is the current user.
  - Pinned by `BridgeSecurityTests.The_pipe_acl_allows_only_the_current_user` and `The_pipe_serves_one_client_one_line_per_request_and_never_listens_again`, which reads the live pipe's ACL.
- **T4-R2: the pipe buffers are 128 KiB each way, not 0.** With 0 and 0, a client that writes two requests before reading deadlocks (measured: the test hung until the host was killed).
  - The server blocks writing response 1 while the client blocks writing request 2.
  - Pinned by the same pipe test, which pipelines two requests.
- **T4-R3: an input that opens a modal answers at once.** `OnUi` subscribes to `ComponentDispatcher.EnterThreadModal` around the input. When a `ShowDialog` starts, the command answers `{"modal":true}`.
  - The next command runs inside the modal's nested loop. Without this, Ctrl+O with dirty edits (Task 1's prompt) or a picker would block the client until someone closed the dialog.
  - Pinned by `An_input_that_opens_a_modal_answers_at_once_and_a_click_on_the_owner_goes_through_set_cursor`.
- **T4-R4: the bridge's pointer does not click WPF buttons.** `ButtonBase` captures and sets `IsPressed` only when `e.ButtonState == Pressed`, and `MouseButtonEventArgs.ButtonState` reads `GetKeyState`, which in-process input cannot set. This is R8's known limit, now measured: a bridge down and up on `ZoomInButton` left zoom at 1, `captured: null`.
  - The smoke presses buttons, toggle buttons and repeat buttons with UIA Invoke or Toggle.
  - The canvas, the layer rows, the picker's square and strip, and a slider's Thumb all capture without reading ButtonState, so the bridge drives them.
  - Task 5 records the limit; no test pins a negative.
- **T4-R5: an Alt chord arrives as `Key.System` with `SystemKey`, as real input delivers it.** The bridge calls the internal `KeyEventArgs.MarkSystem` and fails loudly if it is missing.
  - Pinned by `Alt_up_on_a_focused_layer_row_moves_it_one_step`, which asserts `(Key.System, Key.Up, Alt)`.
- **T4-R6: an exception from app code is answered, not rethrown.** It comes back as `ok:false` with `APP-EXCEPTION <type>: <message>`.
  - A rethrow onto the dispatcher would kill the test STA thread for every later test.
  - In the smoke, the prefix fails the check loudly.
  - Bridge refusals use `BridgeException`.
- **T4-R7: a publish or an MSIX build with the flag is refused.** The `RefuseTestInputBridgeOnPublish` target errors before compiling when `_IsPublishing` is true (`dotnet publish`) or when `GenerateAppxPackageOnBuild` or `AppxPackage` is true or `WindowsPackageType` is `MSIX` (MSBuild packaging, M6's Store path, which does not set `_IsPublishing`; critic M5). Belt and braces for M6, whose packaging also asserts the flag is unset (Global Constraints).
  - Pinned by `BridgeExclusionTests.Publishing_with_the_flag_is_refused`, which also builds with `-p:WindowsPackageType=MSIX`. Measured: it failed (exit 0) before the target existed; the reconciler measured the MSIX build and a `GenerateAppxPackageOnBuild=true` build refusing with the target's text.
- **T4-R8: the key target.** It is `Keyboard.FocusedElement` when that lies in an app window. Otherwise it is the active app window's focused element; for the main window that is `MainWindow.FocusedElement()`, the seam `TextFieldShortcutCommand`'s guard reads. Otherwise it is the window itself.
  - So a test, and the smoke on a background window, put "focus" where the guard sees it.
  - Pinned by `Ctrl_S_with_a_text_field_focused_does_nothing` (target `XBox`, no binding runs) and the Alt+Up test.
- **T4-R9: the peers keep identity per layer id while the layer exists.** A hidden layer is not a child, and its peer comes back when the layer is shown.
  - Selection changes reach the canvas peer through `INotifyPropertyChanged` on the `LayerAutomation` object: `CanvasViewModel.SelectedLayerId` raises PropertyChanged. That needs no new member on the sheet's interface.
  - Pinned by `A_layers_element_keeps_its_identity_across_edits`.
- **T4-R10: pattern arguments are validated.** A non-finite position, a non-finite or non-positive size, or a non-finite angle throws `ArgumentException`, which UIA reports as E_INVALIDARG, and records nothing.
  - Resize uses min(w/W, h/H) under `ScaleAboutCenter`'s clamp.
  - Rotate adds to the current angle (UIA's relative semantics).
- **T4-R11: no ItemStatus text.** A spoken "x, y, scale" string would be a new user-facing string with no 4.1 source. Name, bounds and the Transform pattern's values already carry the information. It stays "".
- **T4-R12: when the client disconnects, the dispatcher is disposed** on the UI thread. The hardware neutralizer and the `PointerState` override go with it, so a smoke that ends early leaves a usable window.
- **T4-R13: wait-idle is defined by the app's own busy signals.** These are an open wheel burst, a frame waiting for `CompositionTarget.Rendering`, and `Files.IsBusy`, checked at `ApplicationIdle` priority every 10 ms. The timeout defaults to 5000 ms; the maximum is 60000.
  - Pinned deterministically by `Wait_idle_returns_once_the_wheel_burst_and_its_frame_are_done`, which checks `BusyReason()` inside one dispatcher turn. A wall-clock bound on the wait failed under a parallel run (measured: the 400 ms burst had already closed in the STA queue, so the wait was 7 ms), so the test asserts no timing.
- **T4-R14: a repeated key is a malformed line** (critic M6). `JsonObject` builds its dictionary on first use and throws `ArgumentException` on a duplicate key (measured on .NET 10), which escaped the server's `FormatException` catch, so the server closed with no error line and `Served` faulted. `Parse` turns it into `FormatException("a field is listed twice: ...")`, and `ServeAsync` also catches `ArgumentException`. Pinned by the `dup` rows of `A_bad_request_line_gets_one_error_and_a_disconnect` and `Parsing_refuses_anything_unexpected`.
- **T4-R15: `inspect` returns what the smoke reads** (critic C3; Task 5 Additions 1-3). `target` `canvas` gives `cursor` as `{kind, ring}`, `ringRadiusPx` (`CanvasCursors.RingRadius` for the eraser ring, else 0), `hostCursor` and `overrideCursor` by name (matched by reference over `Cross`, `SizeAll`, `Hand`, `SizeNWSE`, `SizeNS`, `Arrow`, `No` and `Wait`; `Custom` for the ring; null for none), `tool`, `brush`, per layer `w`, `h` and an `image` identity number (a `ConditionalWeakTable` counter), and `fills`. `target` `layers` gives `{dragging, rows: [{id, name, dragSource, drop}]}` top first. A `pointer down` on a disabled owner adds `ownerClick: true` and `handled` (the owner hook's nonzero WM_SETCURSOR result). Pinned by `A_canvas_move_drag_is_one_undo_step` (cursor and tool), `Inspect_layers_reports_a_row_drag_in_progress` and `An_input_that_opens_a_modal_answers_at_once_and_a_click_on_the_owner_goes_through_set_cursor` (`ownerClick`, `handled`).
- **T4-R16: bridge tests run alone** (critic M7). Every `StaThread.Run` body shares one UI thread, and while a rig lives `PointerState.LeftButtonOverride` and the hardware neutralizer are installed on it; the modal fact runs `ShowDialog` there. `InputBridgeTests` is in the non-parallel `[Collection("Bridge")]` and `CanvasLayerAutomationTests` in `[Collection("MainWindow")]`, as ShellTests and ShellLookTests are.
- **T4-R17: known limits, recorded for Task 5's `test-input-bridge` quirks.** The hardware keyboard is not neutralized: `BridgeKeyboardDevice` is a second keyboard device, and only the mouse is pinned (R8), so a key pressed by hand during a smoke run reaches the app (critic L11). The bridge's `CaptureMouse` is a real Win32 `SetCapture` on the app's own window, inert for the tests' never-activated off-screen windows (critic L14). The `file-dialog` command lets the pipe's client, the current user, choose any path that user can write through Save: accepted by design. The bridged launch deletes its `%TEMP%\rbx15-bridge-<pid>` settings folder on exit (critic L12).

**Measured while drafting:**

- **Setup.**
  - `git archive` of `main` 3f3c5c5 into `%TEMP%\rbx15-t4` (outside the repo; the repo was never built in or written, beyond this slice). .NET SDK 10.0.302.
  - Baseline there: Engine.Tests 880, App.Tests 644, Parity.Tests 35, all green, `-warnaserror` clean.
- **Stand-ins for Task 1**, written from interfaces §3 and kept minimal:
  - the three `IFileDialogs` members, returning null, in the interface, `Win32FileDialogs`, `FakeFileDialogs` and `LedgerScenarioTests.Files`;
  - `ProjectFileService` with only `IsBusy` (settable), and `MainViewModel.Files` (required), built in Composition;
  - `FileGroupViewModel.SaveCommand`, `SaveAsCommand` and `OpenCommand` as no-op commands;
  - `MainWindow.AddShortcut` with Ctrl+Z, Ctrl+S, Ctrl+Shift+S and Ctrl+O;
  - the two `Composition.Build` overloads with `ISettingsStore`, forwarding;
  - `EditorSession.MarkSaved`, setting `CurrentFile`.

  No Task 2 or Task 3 code was needed by the drafter's code. The reconciler's C2 edit and fact use Task 2's panel (below).
- **Build and tests.** Every file below compiled under `-warnaserror`, 0 warnings: the solution, and `RBX15.App.csproj` alone with `-p:EnableTestInputBridge=true --artifacts-path %TEMP%\rbx15-t4-flag`.
  - The full suite ran three times after the last change: Engine 880, Parity 35, App 699 (644 + 55), 0 failed each time.
  - The filters below gave 14, 39 and 4.
  - `BridgeExclusionTests` takes about 20 s: two App builds and a refused publish. The full App run went from 4 s to 22 s.
- **End-to-end UIA.** `Through_the_UIA_client_a_layer_is_found_selected_and_moved` used the real `System.Windows.Automation` client from the test thread against the off-screen window. It found "Template canvas", then `Layer_1`, then SelectionItem.Select and Transform.Move(25, 35), and the session had one step at (25, 35).
- **Failure first, measured by removing the implementation:**
  - Step 2: the build fails with CS0246 `LayerAutomationPeer`.
  - Step 7: CS0234 `RBX15.App.Testing`, and CS0246 for `InputBridgeDispatcher`, `BridgeCommand`, `BridgeResponse` and `IInputBridge`.
  - Step 10: 1 failed (`Publishing_with_the_flag_is_refused`: `Assert.NotEqual() Failure`, the exit code was 0), 3 passed.
  - Reverting Step 5's two LayersPanel call sites makes `InputBridgeTests.Alt_up_on_a_focused_layer_row_moves_it_one_step` and `A_layers_row_drag_reorders_in_one_step` fail (2 of 12 then).
  - Removing `<Compile Remove="Testing\**" />` breaks the solution build: CS0436 on every bridge type (BridgeException 46, TestInputBridge 30, …).
- **R7 and R8 re-measured through this code:**
  - A bare key raised on `BridgeKeyboardDevice` gives exactly one PreviewKeyDown, KeyDown, PreviewKeyUp and KeyUp, so WPF does not promote it as well.
  - A captured release at window (-40, -30) reached the canvas at that point.
  - An unmarked `AbsoluteMove` and `Button1Press` were pinned to the bridge point (no gesture, `Mouse.GetPosition` unchanged) until Dispose, after which the unmarked move went through.
  - The flagged exe is `<artifacts>\bin\RBX15.App\release\RBX15.App.exe`.
- **Reconciler's changes, measured on a copy of the drafter's tree** (critic C3, M5, M6, M7, L12): the patched `BridgeJson`, `TestInputBridge`, `InputBridgeDispatcher`, App.xaml.cs, csproj and tests built with 0 warnings, including the flagged App build; `InputBridgeTests` (15 there, without the asset-drag fact), `BridgeSecurityTests` (27) and `CanvasLayerAutomationTests` (11) passed 53 of 53, `BridgeExclusionTests` 4 of 4 with the MSIX build refused, and the full run gave Engine 880, App 702 (644 + 58), Parity 35. The C2 edit and `An_asset_tile_drag_onto_the_canvas_adds_one_layer_in_one_step` need Task 2's panel, so the reconciler also merged this task onto a scratch chain of the plan's Tasks 1-3 as printed: 0 warnings, `InputBridgeTests` and `BridgeSecurityTests` 43 of 43, the full run Engine 938, App 860, Parity 35, and both greps of Steps 5 and 12 printed exactly the one line their Expected names.
- **Not measured:** a launch of the flagged app with `--test-input-bridge`. Implementers never launch the app; this is the controller's `brg probe` in Task 5. The App.xaml.cs hook compiles in the flagged build, which `A_release_build_has_no_bridge_and_a_flagged_build_does` builds on every run.

**Counts.** Cumulative from Task 3's end (Engine.Tests 938, App.Tests 801). Task 4 adds no engine test. Parity.Tests stays 35. The reconciler added `InputBridgeTests` +2 (critic C2, C3) and `BridgeSecurityTests` +2 (critic M6).

| After | Engine.Tests | App.Tests | Delta |
| --- | --- | --- | --- |
| Step 1 (baseline) | 938 | 801 | |
| Step 3 | 938 | 813 | CanvasLayerAutomationTests +11, CanvasAutomationPeerTests +1 |
| Step 5 | 938 | 813 | call sites only |
| Step 8 | 938 | 856 | InputBridgeTests +16, BridgeSecurityTests +27 |
| **Step 10** | **938** | **860** | BridgeExclusionTests +4 |

- [ ] **Step 1: Baseline and Task 1's interfaces**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 938, Failed: 0`; Parity.Tests `Passed: 35, Failed: 0`; App.Tests `Passed: 801, Failed: 0`.

Run: `grep -n "public bool IsBusy\|internal KeyBinding AddShortcut\|string? PickFolder\|ISettingsStore settings)" src/RBX15.App/Services/ProjectFileService.cs src/RBX15.App/MainWindow.xaml.cs src/RBX15.App/Services/IFileDialogs.cs src/RBX15.App/Composition.cs`
Expected: at least one line from each of the four files. Task 4 consumes them. If any is missing, stop and report: Task 1 has not landed.

Run: `grep -n "public void MarkSaved" src/RBX15.App/EditorSession.cs`
Expected: one line.

- [ ] **Step 2: Write the failing canvas automation tests**

Ledger: `a11y-focus-rings`, `undo-empty-steps`, `select-move`, `select-rotate-drag`, `canvas-select-gestures`. Create `src/RBX15.App.Tests/CanvasLayerAutomationTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The canvas's layer automation (M4a contract "Canvas UI Automation"; spec §4 accessibility; ledger a11y-focus-rings,
/// undo-empty-steps). 4.1's canvas (editor.html:330) exposed no layers. Every test drives the peers through their
/// provider interfaces, as a UIA client does, with no input of any kind.
/// </summary>
[Collection("MainWindow")] // shows windows on the shared STA thread, like ShellTests and ShellLookTests (critic M7)
public class CanvasLayerAutomationTests
{
    /// <summary>The canvas view over a session (Select tool), its host and the host's peer.</summary>
    static (EditorSession Session, CanvasViewModel Vm, TemplateCanvasHost Host, TemplateCanvasHostAutomationPeer Peer) Canvas(params Layer[] layers) =>
        Canvas(new ToolsViewModel(), layers);

    static (EditorSession Session, CanvasViewModel Vm, TemplateCanvasHost Host, TemplateCanvasHostAutomationPeer Peer) Canvas(ToolsViewModel tools, params Layer[] layers)
    {
        var session = SessionWith(layers);
        var vm = new CanvasViewModel(session, tools, new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        var host = (TemplateCanvasHost)view.FindName("Host");
        var peer = (TemplateCanvasHostAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(host);
        return (session, vm, host, peer);
    }

    static LayerAutomationPeer Child(TemplateCanvasHostAutomationPeer peer, int id) =>
        Assert.Single(peer.GetChildren().OfType<LayerAutomationPeer>(), p => p.LayerId == id);

    static ITransformProvider Transform(LayerAutomationPeer p) => (ITransformProvider)p.GetPattern(PatternInterface.Transform)!;

    static ISelectionItemProvider Item(LayerAutomationPeer p) => (ISelectionItemProvider)p.GetPattern(PatternInterface.SelectionItem)!;

    static Layer L(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    [Fact] // the visible layers, top first, as named list items; hidden layers are not elements; the canvas stays an Image
    public void The_children_are_the_visible_layers_top_first() => StaThread.Run(() =>
    {
        var (_, _, _, peer) = Canvas(LayerOf(1) with { Name = "bottom" }, LayerOf(2) with { Name = "hidden", Visible = false }, LayerOf(3) with { Name = "top" });
        var children = peer.GetChildren().Cast<LayerAutomationPeer>().ToList();
        Assert.Equal(new[] { 3, 1 }, children.Select(c => c.LayerId));
        Assert.Equal(new[] { "top", "bottom" }, children.Select(c => c.GetName()));
        Assert.Equal(new[] { "Layer_3", "Layer_1" }, children.Select(c => c.GetAutomationId()));
        Assert.All(children, c =>
        {
            Assert.Equal(AutomationControlType.ListItem, c.GetAutomationControlType());
            Assert.Equal("CanvasLayer", c.GetClassName());
            Assert.False(c.IsKeyboardFocusable());
            Assert.Same(peer, c.GetParent());
        });
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
    });

    [Fact] // a client keeps the same element across an edit; a removed layer's element goes
    public void A_layers_element_keeps_its_identity_across_edits() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1), LayerOf(2));
        var before = Child(peer, 1);
        session.Apply(d => LayerOps.MoveTo(d, 1, 40, 50));
        Assert.Same(before, Child(peer, 1));
        session.Apply(d => d with { Layers = d.Layers.RemoveAll(l => l.Id == 2) });
        Assert.Equal(new[] { 1 }, peer.GetChildren().Cast<LayerAutomationPeer>().Select(c => c.LayerId));
    });

    [Fact] // SelectionItem: Select picks the layer and the Select tool (as a list pick); one layer at a time; Remove deselects
    public void Selecting_through_automation_picks_the_layer_and_the_select_tool() => StaThread.Run(() =>
    {
        var tools = new ToolsViewModel();
        var (session, _, _, peer) = Canvas(tools, LayerOf(1), LayerOf(2));
        tools.Set(Tool.Pen);
        var one = Item(Child(peer, 1));
        var two = Item(Child(peer, 2));
        var selection = (ISelectionProvider)peer.GetPattern(PatternInterface.Selection);
        Assert.Empty(selection.GetSelection());
        Assert.Equal((false, false), (selection.CanSelectMultiple, selection.IsSelectionRequired));

        one.Select();
        Assert.Equal(1, session.SelectedLayerId);
        Assert.Equal(Tool.Select, tools.Current);
        Assert.True(one.IsSelected);
        Assert.False(two.IsSelected);
        Assert.Single(selection.GetSelection());
        Assert.Equal(0, session.UndoCount);   // selection is never an undo step

        Assert.Throws<InvalidOperationException>(() => two.AddToSelection());
        one.AddToSelection();                 // already selected: allowed, nothing changes
        Assert.Equal(1, session.SelectedLayerId);
        two.RemoveFromSelection();            // not the selected one: nothing
        Assert.Equal(1, session.SelectedLayerId);
        one.RemoveFromSelection();
        Assert.Null(session.SelectedLayerId);
        Assert.Empty(selection.GetSelection());
    });

    [Fact] // only the selected visible layer transforms; any other call is refused and records nothing
    public void Only_the_selected_layer_can_be_transformed() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1), LayerOf(2));
        var t1 = Transform(Child(peer, 1));
        Assert.Equal((false, false, false), (t1.CanMove, t1.CanResize, t1.CanRotate));
        Assert.Throws<ElementNotEnabledException>(() => t1.Move(5, 5));
        Assert.Throws<ElementNotEnabledException>(() => t1.Resize(5, 5));
        Assert.Throws<ElementNotEnabledException>(() => t1.Rotate(5));
        session.Select(1);
        Assert.Equal((true, true, true), (t1.CanMove, t1.CanResize, t1.CanRotate));
        Assert.False(Transform(Child(peer, 2)).CanMove);
        Assert.Equal(0, session.UndoCount);
    });

    [Fact] // Move sets the top-left in template pixels as one step; the same position again is no step; junk is refused
    public void Move_is_template_pixels_and_one_undo_step() => StaThread.Run(() =>
    {
        var (session, _, host, peer) = Canvas(LayerOf(1));
        host.Zoom = 2;                         // template units, so zoom never enters
        session.Select(1);
        var t = Transform(Child(peer, 1));
        t.Move(10.5, -20);
        Assert.Equal((10.5, -20.0), (L(session, 1).X, L(session, 1).Y));
        Assert.Equal(1, session.UndoCount);
        t.Move(10.5, -20);
        Assert.Equal(1, session.UndoCount);
        Assert.Throws<ArgumentException>(() => t.Move(double.NaN, 0));
        Assert.Throws<ArgumentException>(() => t.Move(0, double.PositiveInfinity));
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.Undo());
        Assert.Equal((0.0, 0.0), (L(session, 1).X, L(session, 1).Y));
    });

    [Fact] // Resize fits the displayed size uniformly about the center (editor.js:1181-1190), clamped to 1-500%, one step
    public void Resize_fits_the_size_about_the_center_under_the_one_clamp() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1, 200, 100) with { X = 100, Y = 100 });   // center (200, 150)
        session.Select(1);
        var t = Transform(Child(peer, 1));
        t.Resize(100, 100);                    // min(100/200, 100/100) = 0.5
        var l = L(session, 1);
        Assert.Equal(0.5, l.Scale);
        Assert.Equal((150.0, 125.0), (l.X, l.Y));
        Assert.Equal(1, session.UndoCount);
        t.Resize(100, 100);
        Assert.Equal(1, session.UndoCount);
        t.Resize(100000, 100000);
        Assert.Equal(SelectionGeometry.MaxScale, L(session, 1).Scale);
        Assert.Equal(2, session.UndoCount);
        Assert.Throws<ArgumentException>(() => t.Resize(0, 10));
        Assert.Throws<ArgumentException>(() => t.Resize(10, double.NaN));
        Assert.Equal(2, session.UndoCount);
    });

    [Fact] // Rotate is relative and normalized to -180..180 (select-rotate-drag); a full turn changes nothing and records nothing
    public void Rotate_is_relative_normalized_and_one_step() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1) with { Rotation = 170 });
        session.Select(1);
        var t = Transform(Child(peer, 1));
        t.Rotate(20);
        Assert.Equal(-170, L(session, 1).Rotation, 9);
        Assert.Equal(1, session.UndoCount);
        t.Rotate(360);
        Assert.Equal(1, session.UndoCount);
        Assert.Throws<ArgumentException>(() => t.Rotate(double.NaN));
    });

    [Fact] // a pattern change is its own step, like any other edit during a wheel burst (M3b R7): the burst records first
    public void A_pattern_change_during_a_wheel_burst_is_its_own_step() => StaThread.Run(() =>
    {
        var (session, vm, _, peer) = Canvas(LayerOf(1));
        session.Select(1);
        vm.Interaction.Wheel(1);
        vm.Interaction.Wheel(1);
        var wheeled = L(session, 1);           // x1.05 twice about the center, adopted live
        Transform(Child(peer, 1)).Move(30, 40);
        vm.Interaction.EndWheelBurst();
        Assert.Equal(2, session.UndoCount);
        Assert.True(session.Undo());           // the move only
        Assert.Same(wheeled, L(session, 1));
        Assert.True(session.Undo());           // the burst
        Assert.Equal((1.0, 0.0, 0.0), (L(session, 1).Scale, L(session, 1).X, L(session, 1).Y));
    });

    [Fact] // a hidden selected layer is no element and cannot be moved
    public void A_hidden_layer_is_neither_an_element_nor_movable() => StaThread.Run(() =>
    {
        var (session, _, _, peer) = Canvas(LayerOf(1), LayerOf(2));
        var element = Child(peer, 1);
        session.Select(1);
        session.Apply(d => LayerOps.ToggleVisibility(d, 1));
        Assert.Equal(new[] { 2 }, peer.GetChildren().Cast<LayerAutomationPeer>().Select(c => c.LayerId));
        Assert.False(Transform(element).CanMove);
        Assert.Throws<ElementNotEnabledException>(() => Transform(element).Move(1, 1));
        Assert.Empty(((ISelectionProvider)peer.GetPattern(PatternInterface.Selection)).GetSelection());
    });

    [Fact] // BoundingRectangle is the screen box of the rotated corners at the current zoom; empty while not shown
    public void The_bounds_are_the_rotated_box_on_screen() => StaThread.Run(() =>
    {
        var (session, vm, host, peer) = Canvas(LayerOf(1, 200, 100) with { X = 10, Y = 20 });
        var element = Child(peer, 1);
        Assert.Equal(Rect.Empty, element.GetBoundingRectangle());
        Assert.True(element.IsOffscreen());
        var window = new Window
        {
            Content = new CanvasView { DataContext = vm },
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000,
            Width = 900, Height = 800, ShowActivated = false, ShowInTaskbar = false,
        };
        window.Show();
        try
        {
            StaThread.Pump();
            var shown = (TemplateCanvasHost)((CanvasView)window.Content).FindName("Host");
            var shownPeer = (TemplateCanvasHostAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(shown);
            var layer = Child(shownPeer, 1);
            var origin = shown.PointToScreen(new Point(0, 0));
            double scale = shown.DeviceScale;
            AssertRect(new Rect(origin.X + 10 * scale, origin.Y + 20 * scale, 200 * scale, 100 * scale), layer.GetBoundingRectangle());
            Assert.False(layer.IsOffscreen());
            vm.Zoom = 2;
            StaThread.Pump();
            origin = shown.PointToScreen(new Point(0, 0));
            AssertRect(new Rect(origin.X + 20 * scale, origin.Y + 40 * scale, 400 * scale, 200 * scale), layer.GetBoundingRectangle());
            vm.Zoom = 1;
            session.Apply(d => LayerOps.SetRotation(d, 1, 90));   // about the center (110, 70): 100 wide, 200 tall
            StaThread.Pump();
            origin = shown.PointToScreen(new Point(0, 0));
            AssertRect(new Rect(origin.X + 60 * scale, origin.Y - 30 * scale, 100 * scale, 200 * scale), layer.GetBoundingRectangle());
        }
        finally
        {
            window.Close();
        }
    });

    [Fact] // end to end through the real UI Automation client (another thread, as Narrator or a test driver): find, select, move
    public void Through_the_UIA_client_a_layer_is_found_selected_and_moved()
    {
        var (window, session) = StaThread.Run(() =>
        {
            var (s, vm, _, _) = Canvas(LayerOf(1) with { Name = "drip" });
            var w = new Window
            {
                Content = new CanvasView { DataContext = vm },
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -12000,
                Width = 900, Height = 800, ShowActivated = false, ShowInTaskbar = false,
            };
            w.Show();
            return (w, s);
        });
        try
        {
            var hwnd = StaThread.Run(() => new System.Windows.Interop.WindowInteropHelper(window).Handle);
            var root = AutomationElement.FromHandle(hwnd);
            var canvas = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Template canvas"));
            Assert.NotNull(canvas);
            var layer = canvas.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "Layer_1"));
            Assert.NotNull(layer);
            Assert.Equal("drip", layer.Current.Name);
            Assert.Equal(ControlType.ListItem, layer.Current.ControlType);
            ((SelectionItemPattern)layer.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            var transform = (TransformPattern)layer.GetCurrentPattern(TransformPattern.Pattern);
            Assert.True(transform.Current.CanMove);
            transform.Move(25, 35);
            StaThread.Run(() =>
            {
                Assert.Equal(1, session.SelectedLayerId);
                Assert.Equal((25.0, 35.0), (L(session, 1).X, L(session, 1).Y));
                Assert.Equal(1, session.UndoCount);
            });
        }
        finally
        {
            StaThread.Run(window.Close);
        }
    }

    static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }
}
```

In `src/RBX15.App.Tests/CanvasAutomationPeerTests.cs`, replace:

```csharp
        session.Select(null);
        Assert.Equal("", value.Value);
    });
}
```

with:

```csharp
        session.Select(null);
        Assert.Equal("", value.Value);
    });

    [Fact] // M4a: CanvasView binds the view model as the host's LayerAutomation, and the canvas is a single-select container
    public void The_canvas_is_the_selection_container_of_its_layers() => StaThread.Run(() =>
    {
        var session = SessionWith(LayerOf(1));
        var vm = new CanvasViewModel(session, new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus());
        var view = new CanvasView { DataContext = vm };
        StaThread.Pump();
        var host = (TemplateCanvasHost)view.FindName("Host");
        Assert.Same(vm, host.LayerAutomation);
        var selection = Assert.IsAssignableFrom<ISelectionProvider>(UIElementAutomationPeer.CreatePeerForElement(host).GetPattern(PatternInterface.Selection));
        Assert.Equal((false, false), (selection.CanSelectMultiple, selection.IsSelectionRequired));
        Assert.Empty(selection.GetSelection());
        session.Select(1);
        Assert.Single(selection.GetSelection());
    });
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails with `error CS0246: The type or namespace name 'LayerAutomationPeer' could not be found` (6 sites, measured).

- [ ] **Step 3: Implement the canvas automation**

Ledger: as Step 2. Create `src/RBX15.App/Controls/ICanvasLayerAutomation.cs`:

```csharp
namespace RBX15.App.Controls;

/// <summary>
/// What the canvas's layer automation peers act through (M4a contract "Canvas UI Automation"). The canvas view
/// model implements it over the session, so a layer selected, moved, resized or rotated through UI Automation goes
/// through the same session entry points as the mouse: one undo step per change, none for a no-op (spec §5;
/// ledger undo-empty-steps). Sizes and positions are template pixels (585x559), never screen pixels.
/// </summary>
public interface ICanvasLayerAutomation
{
    /// <summary>The selected layer's id, or null.</summary>
    int? SelectedLayerId { get; }

    /// <summary>Selects a layer (null deselects). A non-null id also sets the Select tool, as a pick in the layers list does.</summary>
    void SelectLayer(int? id);

    /// <summary>Sets the layer's top-left to (x, y) in template pixels. True iff the document changed (one undo step).</summary>
    bool MoveLayer(int id, double x, double y);

    /// <summary>
    /// Scales the layer about its center so its displayed size fits width x height template pixels:
    /// scale = min(width / image width, height / image height), under the one 1-500% clamp. True iff it changed.
    /// </summary>
    bool ResizeLayer(int id, double width, double height);

    /// <summary>Turns the layer by <paramref name="degrees"/> (relative, clockwise), normalized to -180..180. True iff it changed.</summary>
    bool RotateLayer(int id, double degrees);
}
```

Create `src/RBX15.App/Controls/LayerAutomationPeer.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using RBX15.Engine;

namespace RBX15.App.Controls;

/// <summary>
/// One visible layer on the canvas as a UI Automation element (M4a contract "Canvas UI Automation"; spec §4
/// accessibility). 4.1's &lt;canvas&gt; (editor.html:330) exposed no layers at all, so a screen reader or a
/// keyboard-only user could not reach them (ledger a11y-focus-rings). Here every visible layer is a child of the
/// canvas peer with a SelectionItem pattern, and the selected one can also be transformed.
/// <list type="bullet">
/// <item>Name is the layer name, AutomationId <c>Layer_&lt;id&gt;</c>, ControlType ListItem, ClassName CanvasLayer. It
///   takes no keyboard focus: the canvas has none either (M3b), and the layers list is the keyboard path.</item>
/// <item>BoundingRectangle is the screen box of the layer's four rotated corners (SelectionGeometry.Corner, the
///   corners the overlay draws, editor.js:1083-1088), so it follows zoom, scroll, rotation and the display scale.</item>
/// <item>Transform works in <b>template pixels</b>: Move(x, y) sets the layer's top-left, Resize(w, h) sets its
///   displayed size, Rotate(degrees) turns it relative to now. This deviates from UIA's screen coordinates on
///   purpose: the contract asks for template units, which are the X and Y the properties panel shows
///   (editor.js:825-826). Each call is one undo step through the session, the same as the matching gesture, and a
///   call that changes nothing records nothing. Only the selected, visible layer can be transformed; any other
///   call throws <see cref="ElementNotEnabledException"/>.</item>
/// </list>
/// The canvas peer creates and caches these by layer id, so a client holding one keeps it across edits.
/// </summary>
public sealed class LayerAutomationPeer : AutomationPeer, ISelectionItemProvider, ITransformProvider
{
    static readonly ResizeCorner[] CornerOrder = [ResizeCorner.TopLeft, ResizeCorner.TopRight, ResizeCorner.BottomRight, ResizeCorner.BottomLeft];

    readonly TemplateCanvasHostAutomationPeer _canvas;

    internal LayerAutomationPeer(TemplateCanvasHostAutomationPeer canvas, int layerId)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        LayerId = layerId;
    }

    /// <summary>The layer this element stands for.</summary>
    public int LayerId { get; }

    TemplateCanvasHost Host => _canvas.Host;

    ICanvasLayerAutomation? Automation => Host.LayerAutomation;

    Layer? Layer => Host.Document?.Layers.Find(l => l.Id == LayerId);

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.SelectionItem or PatternInterface.Transform ? this : null;

    protected override string GetNameCore() => Layer?.Name ?? "";

    protected override string GetAutomationIdCore() => "Layer_" + LayerId.ToString(CultureInfo.InvariantCulture);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => "CanvasLayer";

    protected override Rect GetBoundingRectangleCore() =>
        Layer is { } layer ? Host.ScreenBounds(CornerOrder.Select(c => SelectionGeometry.Corner(layer, c)).Select(p => new Point(p.X, p.Y))) : Rect.Empty;

    protected override Point GetClickablePointCore()
    {
        if (Layer is not { } layer) return new Point(double.NaN, double.NaN);
        var (x, y) = SelectionGeometry.Center(layer);
        return Host.ScreenBounds([new Point(x, y)]) is { IsEmpty: false } r ? r.TopLeft : new Point(double.NaN, double.NaN);
    }

    protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override void SetFocusCore() => throw new InvalidOperationException("A canvas layer takes no keyboard focus; the layers list does.");

    protected override bool IsEnabledCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override string GetAcceleratorKeyCore() => "";

    protected override string GetAccessKeyCore() => "";

    protected override string GetHelpTextCore() => "";

    protected override string GetItemStatusCore() => "";

    protected override string GetItemTypeCore() => "";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string GetLocalizedControlTypeCore() => "list item";

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    // ---- SelectionItem ----

    public bool IsSelected => Automation?.SelectedLayerId == LayerId;

    public IRawElementProviderSimple SelectionContainer => _canvas.Provider();

    /// <summary>Selects this layer and sets the Select tool, as a pick in the layers list does.</summary>
    public void Select()
    {
        var automation = Automation ?? throw new ElementNotEnabledException();
        if (Layer is not { Visible: true }) throw new ElementNotAvailableException();
        automation.SelectLayer(LayerId);
    }

    /// <summary>One layer is selected at a time: with nothing (or this layer) selected it selects, otherwise it throws.</summary>
    public void AddToSelection()
    {
        var automation = Automation ?? throw new ElementNotEnabledException();
        if (automation.SelectedLayerId is int other && other != LayerId)
            throw new InvalidOperationException("Only one layer can be selected at a time.");
        Select();
    }

    /// <summary>Deselects when this layer is the selected one; otherwise nothing changes.</summary>
    public void RemoveFromSelection()
    {
        if (IsSelected) Automation!.SelectLayer(null);
    }

    // ---- Transform (template pixels; one undo step per call that changes the layer) ----

    bool Editable => Automation is not null && IsSelected && Layer is { Visible: true };

    public bool CanMove => Editable;

    public bool CanResize => Editable;

    public bool CanRotate => Editable;

    public void Move(double x, double y)
    {
        var automation = RequireEditable();
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("The position must be finite.");
        automation.MoveLayer(LayerId, x, y);
    }

    public void Resize(double width, double height)
    {
        var automation = RequireEditable();
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentException("The size must be finite and positive.");
        automation.ResizeLayer(LayerId, width, height);
    }

    public void Rotate(double degrees)
    {
        var automation = RequireEditable();
        if (!double.IsFinite(degrees)) throw new ArgumentException("The angle must be finite.");
        automation.RotateLayer(LayerId, degrees);
    }

    ICanvasLayerAutomation RequireEditable() => Editable ? Automation! : throw new ElementNotEnabledException();
}
```

Replace the whole of `src/RBX15.App/Controls/TemplateCanvasHostAutomationPeer.cs` with:

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
/// <para>
/// M4a: its children are the visible layers, top first, as <see cref="LayerAutomationPeer"/>s cached by layer id
/// (a client keeps the same element across edits), and it is their Selection container: one layer at a time, none
/// required. A document change resets the children, which makes WPF raise StructureChanged to listening clients;
/// a selection change raises ElementSelected on the newly selected layer. ControlType stays Image (M3b sign-off).
/// </para>
/// </summary>
public sealed class TemplateCanvasHostAutomationPeer : FrameworkElementAutomationPeer, IValueProvider, ISelectionProvider
{
    readonly Dictionary<int, LayerAutomationPeer> _layers = [];
    int? _announced;

    public TemplateCanvasHostAutomationPeer(TemplateCanvasHost owner) : base(owner)
    {
        _announced = owner.LayerAutomation?.SelectedLayerId;
    }

    internal TemplateCanvasHost Host => (TemplateCanvasHost)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => nameof(TemplateCanvasHost);

    protected override bool IsKeyboardFocusableCore() => false;

    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Value or PatternInterface.Selection ? this : base.GetPattern(patternInterface);

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

    /// <summary>The visible layers, top first (the order the user sees them stacked), one cached peer per layer id.</summary>
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        if (Host.Document is not { } doc)
        {
            _layers.Clear();
            return null;
        }
        var ids = doc.Layers.Select(l => l.Id).ToHashSet();
        foreach (int gone in _layers.Keys.Where(id => !ids.Contains(id)).ToList()) _layers.Remove(gone);
        var children = new List<AutomationPeer>();
        for (int i = doc.Layers.Count - 1; i >= 0; i--)
        {
            var layer = doc.Layers[i];
            if (!layer.Visible) continue;
            if (!_layers.TryGetValue(layer.Id, out var peer)) _layers[layer.Id] = peer = new LayerAutomationPeer(this, layer.Id);
            children.Add(peer);
        }
        return children.Count == 0 ? null : children;
    }

    /// <summary>The child peer of a visible layer, or null. It enumerates the children first, so the peer knows its parent.</summary>
    internal LayerAutomationPeer? LayerPeer(int id)
    {
        GetChildren();
        return Host.Document?.Layers.Find(l => l.Id == id) is { Visible: true } && _layers.TryGetValue(id, out var peer) ? peer : null;
    }

    internal IRawElementProviderSimple Provider() => ProviderFromPeer(this);

    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => false;

    /// <summary>The selected layer when it is visible, else nothing.</summary>
    public IRawElementProviderSimple[] GetSelection() =>
        Host.LayerAutomation?.SelectedLayerId is int id && LayerPeer(id) is { } peer ? [ProviderFromPeer(peer)] : [];

    /// <summary>The document changed: re-read the children (WPF raises StructureChanged to listening clients).</summary>
    internal void OnLayersChanged()
    {
        ResetChildrenCache();
        OnSelectionChanged();
    }

    /// <summary>The selection may have changed: announce the newly selected layer once.</summary>
    internal void OnSelectionChanged()
    {
        int? now = Host.LayerAutomation?.SelectedLayerId;
        if (now == _announced) return;
        _announced = now;
        if (now is int id && ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected) && LayerPeer(id) is { } peer)
            peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }
}
```

In `src/RBX15.App/Controls/TemplateCanvasHost.cs`, make six replacements. Each anchor occurs once.

1. Replace:

```csharp
using System.Windows;
using System.Windows.Automation;
```

with:

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
```

2. Replace:

```csharp
/// <item>Its automation peer is an Image named "Template canvas" whose Value is <see cref="AutomationValue"/>.</item>
```

with:

```csharp
/// <item>Its automation peer is an Image named "Template canvas" whose Value is <see cref="AutomationValue"/>. M4a: the
///   peer's children are the visible layers (<see cref="LayerAutomationPeer"/>), which select and transform through
///   <see cref="LayerAutomation"/>.</item>
```

3. Replace:

```csharp
        nameof(Document), typeof(Document), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnFrameInputChanged));
```

with:

```csharp
        nameof(Document), typeof(Document), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnDocumentChanged));
```

4. Replace:

```csharp
    readonly DispatcherTimer _wheelTimer;
```

with:

```csharp
    public static readonly DependencyProperty LayerAutomationProperty = DependencyProperty.Register(
        nameof(LayerAutomation), typeof(ICanvasLayerAutomation), typeof(TemplateCanvasHost),
        new FrameworkPropertyMetadata(null, OnLayerAutomationChanged));

    readonly DispatcherTimer _wheelTimer;
```

5. Replace:

```csharp
    /// <summary>The bitmap on screen, rendered now if a frame is pending; null while there is no document. Read by tests.</summary>
```

with:

```csharp
    /// <summary>
    /// What the layer automation peers select and transform through (M4a); CanvasView binds the CanvasViewModel.
    /// Null leaves the layers readable but not selectable or movable through UI Automation.
    /// </summary>
    public ICanvasLayerAutomation? LayerAutomation
    {
        get => (ICanvasLayerAutomation?)GetValue(LayerAutomationProperty);
        set => SetValue(LayerAutomationProperty, value);
    }

    /// <summary>The bitmap on screen, rendered now if a frame is pending; null while there is no document. Read by tests.</summary>
```

6. Replace:

```csharp
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
```

with:

```csharp
    static void OnFrameInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TemplateCanvasHost)d).RequestFrame();

    static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        host.RequestFrame();
        host.CreatedPeer()?.OnLayersChanged();
    }

    static void OnLayerAutomationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= host.OnLayerAutomationPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged now) now.PropertyChanged += host.OnLayerAutomationPropertyChanged;
        host.CreatedPeer()?.OnSelectionChanged();
    }

    void OnLayerAutomationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ICanvasLayerAutomation.SelectedLayerId)) CreatedPeer()?.OnSelectionChanged();
    }

    /// <summary>The automation peer if a client has asked for it; null otherwise (no peer is made just to notify).</summary>
    TemplateCanvasHostAutomationPeer? CreatedPeer() => UIElementAutomationPeer.FromElement(this) as TemplateCanvasHostAutomationPeer;

    /// <summary>
    /// The screen rectangle, in device pixels, around template points: each goes through CanvasCoordinates.ToFrame at
    /// the current zoom, then PointToScreen. Rect.Empty while the control has no PresentationSource (not shown).
    /// </summary>
    internal Rect ScreenBounds(IEnumerable<Point> templatePoints)
    {
        if (PresentationSource.FromVisual(this) is null) return Rect.Empty;
        var bounds = Rect.Empty;
        foreach (var p in templatePoints) bounds.Union(PointToScreen(CanvasCoordinates.ToFrame(p, Zoom)));
        return bounds;
    }

    static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (TemplateCanvasHost)d;
        host.RequestFrame();
        host.UpdateCursor();
    }

    static void OnAutomationValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TemplateCanvasHost)d).CreatedPeer()?.RaiseValueChanged((string?)e.OldValue ?? "", (string?)e.NewValue ?? "");
```

In `src/RBX15.App/ViewModels/CanvasViewModel.cs`, make three replacements.

1. Replace:

```csharp
/// cursor, and <see cref="AutomationValue"/> is the selected layer's name for the canvas automation peer.
/// </para>
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject
{
```

with:

```csharp
/// cursor, and <see cref="AutomationValue"/> is the selected layer's name for the canvas automation peer.
/// </para>
/// <para>
/// M4a: it is the canvas's <see cref="ICanvasLayerAutomation"/>. The layer automation peers select and transform
/// through it, over the same session entry points the mouse uses, so each change is one undo step and a no-op is
/// none (spec §5); <see cref="SelectedLayerId"/> raises PropertyChanged on every selection change, which the canvas
/// peer turns into ElementSelected.
/// </para>
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject, ICanvasLayerAutomation
{
```

2. Replace:

```csharp
    string SelectedName() => _session.SelectedLayer?.Name ?? "";
```

with:

```csharp
    string SelectedName() => _session.SelectedLayer?.Name ?? "";

    /// <summary>The selected layer's id (<see cref="ICanvasLayerAutomation"/>); PropertyChanged on every selection change.</summary>
    public int? SelectedLayerId => _session.SelectedLayerId;

    /// <summary>Selects through the session; a layer pick also sets the Select tool, as a layers-list pick does (editor.js:575-577).</summary>
    public void SelectLayer(int? id)
    {
        _session.Select(id);
        if (id is not null && _session.SelectedLayerId == id) _tools.Set(Tool.Select);
    }

    /// <summary>The X and Y fields' edit (editor.js:825-826) through LayerOps.MoveTo: one step, none if unchanged.</summary>
    public bool MoveLayer(int id, double x, double y) => _session.Apply(d => LayerOps.MoveTo(d, id, x, y));

    /// <summary>A corner-handle resize to a size (editor.js:1181-1190): uniform, about the center, clamped to 1-500%.</summary>
    public bool ResizeLayer(int id, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "The size must be finite and positive.");
        if (_session.Document.Layers.Find(l => l.Id == id) is not { } layer) return false;
        double scale = Math.Min(width / layer.Image.Width, height / layer.Image.Height);
        return _session.Apply(d => LayerOps.ScaleAboutCenter(d, id, scale));
    }

    /// <summary>A rotate drag by a relative angle (editor.js:1172-1180), normalized to -180..180.</summary>
    public bool RotateLayer(int id, double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees), "The angle must be finite.");
        if (_session.Document.Layers.Find(l => l.Id == id) is not { } layer) return false;
        return _session.Apply(d => LayerOps.SetRotation(d, id, layer.Rotation + degrees));
    }
```

3. Replace:

```csharp
            Overlay = CurrentOverlay();
            AutomationValue = SelectedName();
        }
    }
```

with:

```csharp
            Overlay = CurrentOverlay();
            AutomationValue = SelectedName();
        }
        if (e.What.HasFlag(SessionChange.Selection)) OnPropertyChanged(nameof(SelectedLayerId));
    }
```

In `src/RBX15.App/Views/CanvasView.xaml`, replace:

```xml
                                                 AutomationValue="{Binding AutomationValue}" />
```

with:

```xml
                                                 AutomationValue="{Binding AutomationValue}"
                                                 LayerAutomation="{Binding}" />
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.CanvasLayerAutomationTests|FullyQualifiedName~RBX15.App.Tests.CanvasAutomationPeerTests"`
Expected: `Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14` for `RBX15.App.Tests.dll` (11 + 3).

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 938`; Parity.Tests `Passed: 35`; App.Tests `Passed: 813, Failed: 0`.

- [ ] **Step 4: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Controls/ICanvasLayerAutomation.cs src/RBX15.App/Controls/LayerAutomationPeer.cs src/RBX15.App/Controls/TemplateCanvasHostAutomationPeer.cs src/RBX15.App/Controls/TemplateCanvasHost.cs src/RBX15.App/ViewModels/CanvasViewModel.cs src/RBX15.App/Views/CanvasView.xaml src/RBX15.App.Tests/CanvasLayerAutomationTests.cs src/RBX15.App.Tests/CanvasAutomationPeerTests.cs
git commit -m "feat(app): canvas layers as UI Automation elements that select and transform in one undo step"
```

- [ ] **Step 5: The left-button seam and event-device modifiers**

Ledger: `layers-reorder-drag`, `layers-reorder-drag-2` and `fillstyle-color-pickers` (the evidence path R7 and R8 need). App key handlers read modifiers from `e.KeyboardDevice`, as 4.1's handlers read `e.ctrlKey` and `e.shiftKey` from the event (editor.js:1775-1830). App code reads the left button only through `PointerState`. Create `src/RBX15.App/Controls/PointerState.cs`:

```csharp
using System.Windows.Input;

namespace RBX15.App.Controls;

/// <summary>
/// The one place app code reads the physical left-button state (M4a interfaces R8). WPF's
/// <see cref="MouseEventArgs.LeftButton"/> and <see cref="Mouse.LeftButton"/> read the system key state
/// (GetKeyState), which input raised inside the process cannot set, so the test input bridge overrides it here
/// while it drives the app. With no override, both members return exactly what WPF reports. Real input never
/// changes this seam; only <c>RBX15.App.Testing.InputBridgeDispatcher</c> sets
/// <see cref="LeftButtonOverride"/>, and that type exists only in builds made with EnableTestInputBridge.
/// UI thread only.
/// </summary>
internal static class PointerState
{
    /// <summary>Set only by the test input bridge for its lifetime; null in every shipped build.</summary>
    internal static Func<MouseButtonState>? LeftButtonOverride;

    /// <summary>The left button's state for <paramref name="e"/>: the override, else <c>e.LeftButton</c>.</summary>
    public static MouseButtonState LeftButton(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return LeftButtonOverride?.Invoke() ?? e.LeftButton;
    }

    /// <summary>True while the left button is down: the override, else <c>Mouse.LeftButton</c>.</summary>
    public static bool LeftButtonDown() => (LeftButtonOverride?.Invoke() ?? Mouse.LeftButton) == MouseButtonState.Pressed;
}
```

In `src/RBX15.App/Views/LayersPanel.xaml.cs`, make three replacements:

1. `using RBX15.App.ViewModels;` with:

```csharp
using RBX15.App.Controls;
using RBX15.App.ViewModels;
```

2. `        HandleMove(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed);` with:

```csharp
        HandleMove(e.GetPosition(this), PointerState.LeftButton(e) == MouseButtonState.Pressed);
```

3. `        if (HandleKey(key, Keyboard.Modifiers, RowFrom(e.OriginalSource as DependencyObject, stopAtButtons: false))) e.Handled = true;` with:

```csharp
        if (HandleKey(key, e.KeyboardDevice.Modifiers, RowFrom(e.OriginalSource as DependencyObject, stopAtButtons: false))) e.Handled = true;
```

In `src/RBX15.App/Views/ColorPickerPopup.xaml.cs`, replace:

```csharp
        PreviewKeyDown += (_, e) => e.Handled = HandleKey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers, Keyboard.FocusedElement);
```

with:

```csharp
        PreviewKeyDown += (_, e) => e.Handled = HandleKey(e.Key == Key.System ? e.SystemKey : e.Key, e.KeyboardDevice.Modifiers, Keyboard.FocusedElement);
```

In `src/RBX15.App/Controls/SliderGestureTracker.cs`, replace:

```csharp
    /// <summary>The real mouse: <c>Mouse.LeftButton == Pressed</c>.</summary>
    public static bool RealLeftButtonDown() => Mouse.LeftButton == MouseButtonState.Pressed;
```

with:

```csharp
    /// <summary>The real mouse: <c>Mouse.LeftButton == Pressed</c>, read through <see cref="PointerState"/> (the bridge's seam, M4a R8).</summary>
    public static bool RealLeftButtonDown() => PointerState.LeftButtonDown();
```

In `src/RBX15.App/Views/AssetLibraryPanel.xaml.cs` (Task 2's panel; it already has `using RBX15.App.Controls;`), replace (critic C2):

```csharp
    /// <summary>The physical left button. Task 4 Step 5 replaces this read with PointerState.LeftButton(e) (interfaces 3.3; critic C2).</summary>
    static bool LeftDown(MouseEventArgs e) => e.LeftButton == MouseButtonState.Pressed;
```

with:

```csharp
    /// <summary>The left button, read through <see cref="PointerState"/> (the bridge's seam, M4a R8; critic C2).</summary>
    static bool LeftDown(MouseEventArgs e) => PointerState.LeftButton(e) == MouseButtonState.Pressed;
```

Under the bridge `e.LeftButton` reads `GetKeyState` and stays Released, so without this edit every bridge-driven thumbnail drag would end as a move with the button up and add nothing. Step 7's `An_asset_tile_drag_onto_the_canvas_adds_one_layer_in_one_step` pins it.

With no override set, both members return exactly what WPF reports, so no behavior changes. Step 8's `Alt_up_on_a_focused_layer_row_moves_it_one_step` and `A_layers_row_drag_reorders_in_one_step` pin the two LayersPanel sites; reverting them fails those two (measured).

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `grep -rn "Keyboard\.Modifiers\|Mouse\.LeftButton)\|\.LeftButton == MouseButtonState" src/RBX15.App --include=*.cs`
Expected: exactly one line, `src/RBX15.App/Controls/PointerState.cs:27:    public static bool LeftButtonDown() => (LeftButtonOverride?.Invoke() ?? Mouse.LeftButton) == MouseButtonState.Pressed;`. That is the seam itself, and no other app code reads the physical modifier or button state.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.LayersPanelDragTests|FullyQualifiedName~RBX15.App.Tests.SliderGestureTrackerTests|FullyQualifiedName~RBX15.App.Tests.ColorPickerPopupTests|FullyQualifiedName~RBX15.App.Tests.AssetLibraryPanelTests"`
Expected: `Failed:     0` for `RBX15.App.Tests.dll`, with the same Passed count as at Step 1. These call sites' existing tests are unchanged.

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: App.Tests `Passed: 813, Failed: 0`; Engine.Tests `938`; Parity.Tests `35`.

- [ ] **Step 6: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/Controls/PointerState.cs src/RBX15.App/Views/LayersPanel.xaml.cs src/RBX15.App/Views/ColorPickerPopup.xaml.cs src/RBX15.App/Controls/SliderGestureTracker.cs src/RBX15.App/Views/AssetLibraryPanel.xaml.cs
git commit -m "refactor(app): read modifiers from the key event and the left button through one seam"
```

- [ ] **Step 7: The build flag and the failing bridge tests**

Ledger: evidence for Task 5's held rows. Contract "Security". In `src/RBX15.App/RBX15.App.csproj`, replace:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.App.Tests" />
  </ItemGroup>
```

with:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.App.Tests" />
  </ItemGroup>

  <!-- M4a test input bridge (interfaces R9). Testing/** compiles into the app only with
       -p:EnableTestInputBridge=true, which also defines TEST_INPUT_BRIDGE for the two App.xaml.cs hook sites.
       Release, Store and portable builds never set it; BridgeExclusionTests proves a plain Release build carries
       no bridge type. Publishing with it set is refused outright. -->
  <PropertyGroup Condition="'$(EnableTestInputBridge)' == 'true'">
    <DefineConstants>$(DefineConstants);TEST_INPUT_BRIDGE</DefineConstants>
  </PropertyGroup>
  <ItemGroup Condition="'$(EnableTestInputBridge)' != 'true'">
    <Compile Remove="Testing\**" />
  </ItemGroup>
```

In `src/RBX15.App.Tests/RBX15.App.Tests.csproj`, replace:

```xml
  <ItemGroup>
    <ProjectReference Include="..\RBX15.App\RBX15.App.csproj" />
```

with:

```xml
  <!-- The bridge's sources (excluded from the flagless app) compile into this assembly, so its tests run in every
       configuration while the app under test stays bridge-free (M4a interfaces R9). -->
  <ItemGroup>
    <Compile Include="..\RBX15.App\Testing\**\*.cs" LinkBase="Bridge" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RBX15.App\RBX15.App.csproj" />
```

Create `src/RBX15.App.Tests/InputBridgeTests.cs` (interfaces §4's list, plus the right and middle buttons, the hardware neutralizer, the left-button seam, a row drag and the modal):

```csharp
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Testing;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>InputBridgeTests run with no other test class in parallel (critic M7).</summary>
[CollectionDefinition("Bridge", DisableParallelization = true)]
public sealed class BridgeCollection
{
}

/// <summary>
/// The test input bridge's dispatcher against a real MainWindow (TestShell's view model), shown off-screen at
/// (-12000, -12000) and never activated (M4a interfaces §4, R7, R8). Commands go through
/// <see cref="InputBridgeDispatcher.ExecuteAsync"/> from the test thread, as the pipe sends them, and run on the STA
/// thread's dispatcher. Nothing here sends input outside this process: the bridge raises WPF input in-process only.
/// While a rig lives, PointerState's override and the hardware neutralizer are installed on the shared STA thread, and
/// the modal fact runs a nested loop there, so this class runs alone (critic M7).
/// </summary>
[Collection("Bridge")]
public class InputBridgeTests
{
    sealed class Rig(MainWindow window, InputBridgeDispatcher bridge) : IDisposable
    {
        public MainWindow Window { get; } = window;
        public MainViewModel Main => Window.ViewModel;
        public EditorSession Session => Main.Session;
        public InputBridgeDispatcher Bridge { get; } = bridge;

        public BridgeResponse Send(string json) => Bridge.ExecuteAsync(BridgeJson.Parse(json)).GetAwaiter().GetResult();

        public JsonObject Ok(string json)
        {
            var r = Send(json);
            Assert.True(r.Ok, r.Error);
            return r.Result ?? [];
        }

        public void Dispose() => StaThread.Run(() =>
        {
            Bridge.Dispose();
            Session.MarkSaved("bridge-test.r15proj", Session.Document);   // closing never prompts (M4a Task 1)
            Window.Close();
        });
    }

    static Rig Open(params Layer[] layers) => StaThread.Run(() =>
    {
        var window = new MainWindow(TestShell.Main(SessionWith(layers)), new FakeSettingsStore(), [new Rect(-12000, -12000, 1920, 1040)])
        {
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        StaThread.Pump();
        return new Rig(window, new InputBridgeDispatcher(window));
    });

    static T First<T>(DependencyObject root) where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>([root]);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t) return t;
            for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        throw new InvalidOperationException("no " + typeof(T).Name);
    }

    static Layer L(EditorSession s, int id) => s.Document.Layers.Single(l => l.Id == id);

    sealed class CountingCommand(ICommand inner) : ICommand
    {
        public int Executed { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => inner.CanExecute(parameter);
        public void Execute(object? parameter) => Executed++;
    }

    [Fact] // H1-style move drag (select-move): press on the layer, move, release; one undo step; capture held mid-drag
    public void A_canvas_move_drag_is_one_undo_step()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"canvas":{"x":50,"y":50}}}""");
        var mid = rig.Ok("""{"id":2,"cmd":"inspect","target":"canvas"}""");
        Assert.Equal(("Move", true), (mid["gesture"]!.GetValue<string>(), mid["captured"]!.GetValue<bool>()));
        Assert.Equal(("Move", "Select"), (mid["cursor"]!["kind"]!.GetValue<string>(), mid["tool"]!.GetValue<string>()));   // the smoke's Inspect shape (critic C3)
        rig.Ok("""{"id":3,"cmd":"pointer","action":"move","at":{"canvas":{"x":65,"y":70}}}""");
        rig.Ok("""{"id":4,"cmd":"pointer","action":"move","at":{"canvas":{"x":80,"y":90}}}""");
        rig.Ok("""{"id":5,"cmd":"pointer","action":"up","at":{"canvas":{"x":80,"y":90}}}""");
        var end = rig.Ok("""{"id":6,"cmd":"inspect","target":"canvas"}""");
        Assert.Equal(("None", false, 1, 1), (end["gesture"]!.GetValue<string>(), end["captured"]!.GetValue<bool>(), end["undo"]!.GetValue<int>(), end["selected"]!.GetValue<int>()));
        StaThread.Run(() => Assert.Equal((30.0, 40.0), (L(rig.Session, 1).X, L(rig.Session, 1).Y)));
    }

    [Fact] // draw-release-outside-canvas / select-move: a release outside the window reaches the captured canvas
    public void A_release_outside_the_window_ends_the_drag_where_it_was_released()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"canvas":{"x":50,"y":50}}}""");
        rig.Ok("""{"id":2,"cmd":"pointer","action":"move","at":{"window":{"x":-40,"y":-30}}}""");
        rig.Ok("""{"id":3,"cmd":"pointer","action":"up","at":{"window":{"x":-40,"y":-30}}}""");
        StaThread.Run(() =>
        {
            var host = First<TemplateCanvasHost>(rig.Window);
            var released = CanvasCoordinates.ToTemplate(rig.Window.TranslatePoint(new Point(-40, -30), host), host.Zoom);
            Assert.Equal(released.X - 50, L(rig.Session, 1).X, 6);
            Assert.Equal(released.Y - 50, L(rig.Session, 1).Y, 6);
            Assert.Equal(CanvasGesture.None, host.Interaction!.Gesture);
            Assert.False(host.IsMouseCaptured);
            Assert.Equal(1, rig.Session.UndoCount);
        });
    }

    [Fact] // Ctrl+S reaches the window's Ctrl+S binding exactly once (the chord check R7 does instead of KeyGesture.Matches)
    public void Ctrl_S_runs_the_save_binding_exactly_once()
    {
        using var rig = Open();
        var counter = StaThread.Run(() =>
        {
            var binding = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.S && b.Modifiers == ModifierKeys.Control);
            var counting = new CountingCommand(binding.Command);
            binding.Command = counting;
            return counting;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"S","modifiers":["Ctrl"]}""");
        Assert.Equal(nameof(CountingCommand), result["binding"]!.GetValue<string>());
        Assert.Equal(1, counter.Executed);
        var shift = rig.Ok("""{"id":2,"cmd":"key","key":"S","modifiers":["Ctrl","Shift"]}""");   // a different chord: Save As's binding
        Assert.Equal(1, counter.Executed);
        Assert.NotNull(shift["binding"]);
    }

    [Theory] // canvas-mouse-button-agnostic (fix): only the left button starts a canvas gesture; right and middle do nothing
    [InlineData("right")]
    [InlineData("middle")]
    public void A_right_or_middle_press_starts_no_canvas_gesture(string button)
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("{\"id\":1,\"cmd\":\"pointer\",\"action\":\"down\",\"button\":\"" + button + "\",\"at\":{\"canvas\":{\"x\":50,\"y\":50}}}");
        rig.Ok("{\"id\":2,\"cmd\":\"pointer\",\"action\":\"move\",\"at\":{\"canvas\":{\"x\":90,\"y\":90}}}");
        var state = rig.Ok("{\"id\":3,\"cmd\":\"inspect\",\"target\":\"canvas\"}");
        rig.Ok("{\"id\":4,\"cmd\":\"pointer\",\"action\":\"up\",\"button\":\"" + button + "\",\"at\":{\"canvas\":{\"x\":90,\"y\":90}}}");
        Assert.Equal(("None", false), (state["gesture"]!.GetValue<string>(), state["captured"]!.GetValue<bool>()));
        StaThread.Run(() =>
        {
            Assert.Equal((0.0, 0.0), (L(rig.Session, 1).X, L(rig.Session, 1).Y));
            Assert.Null(rig.Session.SelectedLayerId);
            Assert.Equal(0, rig.Session.UndoCount);
        });
    }

    [Fact] // spec §4: text fields trap shortcuts; the bridge calls CanExecute, so TextFieldShortcutCommand's guard holds
    public void Ctrl_S_with_a_text_field_focused_does_nothing()
    {
        using var rig = Open(LayerOf(1));
        var counter = StaThread.Run(() =>
        {
            rig.Session.Select(1);
            StaThread.Pump();
            var field = (TextBox)First<LayerPropertiesPanel>(rig.Window).FindName("XBox");
            rig.Window.FocusedElement = () => field;
            var binding = rig.Window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.S && b.Modifiers == ModifierKeys.Control);
            var counting = new CountingCommand(binding.Command);
            binding.Command = counting;
            return counting;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"S","modifiers":["Ctrl"]}""");
        Assert.Equal("XBox", result["target"]!.GetValue<string>());
        Assert.Null(result["binding"]);
        Assert.Equal(0, counter.Executed);
    }

    [Fact] // layers-reorder-drag-2: Alt+Up reaches the focused row as Key.System with Alt in e.KeyboardDevice.Modifiers
    public void Alt_up_on_a_focused_layer_row_moves_it_one_step()
    {
        using var rig = Open(LayerOf(1), LayerOf(2), LayerOf(3));
        var seen = StaThread.Run(() =>
        {
            var panel = First<LayersPanel>(rig.Window);
            var row = rig.Main.Layers.Rows.Single(r => r.Id == 1);
            var item = (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(row);
            rig.Window.FocusedElement = () => item;
            var log = new List<(Key, Key, ModifierKeys)>();
            rig.Window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => log.Add((e.Key, e.SystemKey, e.KeyboardDevice.Modifiers))), true);
            return log;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"Up","modifiers":["Alt"]}""");
        Assert.True(result["previewHandled"]!.GetValue<bool>());
        StaThread.Run(() =>
        {
            Assert.Equal(new[] { (Key.System, Key.Up, ModifierKeys.Alt) }, seen);   // as real input delivers an Alt chord
            Assert.Equal(new[] { 2, 1, 3 }, rig.Session.Document.Layers.Select(l => l.Id));
            Assert.Equal(1, rig.Session.SelectedLayerId);
            Assert.Equal(1, rig.Session.UndoCount);
        });
    }

    [Fact] // a bare key: PreviewKeyDown, then KeyDown exactly once (WPF does not promote it for the bridge's device), then the ups
    public void A_bare_key_reaches_key_down_once_with_no_modifiers()
    {
        using var rig = Open();
        var seen = StaThread.Run(() =>
        {
            var log = new List<string>();
            rig.Window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => log.Add($"PreviewKeyDown {e.Key} {e.KeyboardDevice.Modifiers}")), true);
            rig.Window.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) => log.Add($"KeyDown {e.Key} {e.KeyboardDevice.Modifiers}")), true);
            rig.Window.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler((_, e) => log.Add($"PreviewKeyUp {e.Key}")), true);
            rig.Window.AddHandler(Keyboard.KeyUpEvent, new KeyEventHandler((_, e) => log.Add($"KeyUp {e.Key}")), true);
            return log;
        });
        var result = rig.Ok("""{"id":1,"cmd":"key","key":"F7"}""");
        Assert.False(result["previewHandled"]!.GetValue<bool>());
        StaThread.Run(() => Assert.Equal(new[] { "PreviewKeyDown F7 None", "KeyDown F7 None", "PreviewKeyUp F7", "KeyUp F7" }, seen));
    }

    [Fact] // R7 step 1: with a physical modifier held, a key command is refused, so a real Ctrl never mixes into a bridge chord
    public void A_key_is_refused_while_a_physical_modifier_is_held()
    {
        using var rig = Open();
        var seen = StaThread.Run(() =>
        {
            rig.Bridge.PhysicalModifiers = () => ModifierKeys.Control;
            var log = new List<Key>();
            rig.Window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => log.Add(e.Key)), true);
            return log;
        });
        var r = rig.Send("""{"id":7,"cmd":"key","key":"F7"}""");
        Assert.False(r.Ok);
        Assert.Equal(7, r.Id);
        Assert.StartsWith("physical modifier keys are held (Control)", r.Error);
        StaThread.Run(() => Assert.Empty(seen));
    }

    [Fact] // wheel-scale-undo-burst: wait-idle waits out an open wheel burst and the frame it leaves; then the burst is one step
    public void Wait_idle_returns_once_the_wheel_burst_and_its_frame_are_done()
    {
        using var rig = Open(LayerOf(1));
        StaThread.Run(() =>
        {
            rig.Session.Select(1);
            var host = First<TemplateCanvasHost>(rig.Window);
            host.HandleWheel(120);   // inside one dispatcher turn, so the 400 ms quiet period cannot elapse before the checks
            Assert.Equal("a wheel burst is open", rig.Bridge.BusyReason());
            host.FlushWheelBurst();
            Assert.Equal("a frame is waiting to render", rig.Bridge.BusyReason());
            host.HandleWheel(120);
        });
        rig.Ok("""{"id":1,"cmd":"wheel","delta":120,"at":{"canvas":{"x":50,"y":50}}}""");
        var idle = rig.Ok("""{"id":2,"cmd":"wait-idle","timeoutMs":3000}""");
        Assert.InRange(idle["ms"]!.GetValue<long>(), 0, 3000);
        StaThread.Run(() =>
        {
            Assert.Null(rig.Bridge.BusyReason());
            Assert.Equal(2, rig.Session.UndoCount);   // the seam's flushed burst, then the second burst (seam + bridge notch)
            Assert.Equal(Math.Pow(1.05, 3), L(rig.Session, 1).Scale, 9);
        });
        var r = rig.Send("""{"id":3,"cmd":"wait-idle","timeoutMs":60001}""");
        Assert.Equal((false, "\"timeoutMs\" must be 0..60000"), (r.Ok, r.Error));
    }

    [Fact] // R8: while the bridge lives, an unmarked (hardware) report cannot move or press; after Dispose it can again
    public void Hardware_mouse_reports_are_pinned_to_the_bridge_point_until_it_is_disposed()
    {
        using var rig = Open(LayerOf(1));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"move","at":{"canvas":{"x":100,"y":80}}}""");
        StaThread.Run(() =>
        {
            var host = First<TemplateCanvasHost>(rig.Window);
            var source = PresentationSource.FromVisual(host)!;
            var elsewhere = source.CompositionTarget.TransformToDevice.Transform(host.TranslatePoint(new Point(10, 10), rig.Window));
            RawMouse.Inject(source, "AbsoluteMove", elsewhere, 0, extraInformation: 0);
            RawMouse.Inject(source, "Button1Press", elsewhere, 0, extraInformation: 0);
            Assert.Equal(new Point(100, 80), RoundedTemplate(host));
            Assert.Equal(CanvasGesture.None, host.Interaction!.Gesture);
            Assert.Null(rig.Session.SelectedLayerId);

            rig.Bridge.Dispose();
            RawMouse.Inject(source, "AbsoluteMove", elsewhere, 0, extraInformation: 0);
            Assert.Equal(new Point(10, 10), RoundedTemplate(host));
        });
    }

    static Point RoundedTemplate(TemplateCanvasHost host)
    {
        var p = CanvasCoordinates.ToTemplate(Mouse.GetPosition(host), host.Zoom);
        return new Point(Math.Round(p.X), Math.Round(p.Y));
    }

    [Fact] // R8: app code reads the left button only through PointerState, which follows the bridge's presses; a dispose clears it
    public void The_left_button_seam_follows_the_bridge()
    {
        using var rig = Open();
        Assert.False(StaThread.Run(PointerState.LeftButtonDown));
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"window":{"x":5,"y":5}}}""");
        Assert.True(StaThread.Run(PointerState.LeftButtonDown));
        rig.Ok("""{"id":2,"cmd":"pointer","action":"up","at":{"window":{"x":5,"y":5}}}""");
        Assert.False(StaThread.Run(PointerState.LeftButtonDown));
        StaThread.Run(rig.Bridge.Dispose);
        Assert.Null(StaThread.Run(() => PointerState.LeftButtonOverride));
    }

    [Fact] // H7 (layers-reorder-drag): a row drag through the bridge, which LayersPanel sees through PointerState
    public void A_layers_row_drag_reorders_in_one_step()
    {
        using var rig = Open(LayerOf(1), LayerOf(2), LayerOf(3));
        var (from, to) = StaThread.Run(() =>
        {
            var panel = First<LayersPanel>(rig.Window);
            Point Centre(int id)
            {
                var item = (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(rig.Main.Layers.Rows.Single(r => r.Id == id));
                return item.TranslatePoint(new Point(item.ActualWidth / 3, item.ActualHeight * 0.25), rig.Window);   // upper half, off the buttons
            }
            return (Centre(1), Centre(3));
        });
        string At(Point p) => "{\"window\":{\"x\":" + BridgeJson.Invariant(p.X) + ",\"y\":" + BridgeJson.Invariant(p.Y) + "}}";
        rig.Ok($$"""{"id":1,"cmd":"pointer","action":"down","at":{{At(from)}}}""");
        rig.Ok($$"""{"id":2,"cmd":"pointer","action":"move","at":{{At(new Point(from.X, from.Y - 12))}}}""");
        rig.Ok($$"""{"id":3,"cmd":"pointer","action":"move","at":{{At(to)}}}""");
        rig.Ok($$"""{"id":4,"cmd":"pointer","action":"up","at":{{At(to)}}}""");
        StaThread.Run(() =>
        {
            Assert.Equal(new[] { 2, 3, 1 }, rig.Session.Document.Layers.Select(l => l.Id));   // the bottom row onto the top row's upper half
            Assert.Equal(1, rig.Session.UndoCount);
        });
    }

    [Fact] // asset-drag-to-canvas through the bridge (critic C2): the asset panel reads the left button through PointerState, so a tile drag lands
    public void An_asset_tile_drag_onto_the_canvas_adds_one_layer_in_one_step()
    {
        using var rig = Open();
        StaThread.Run(() =>
        {
            rig.Main.Assets.LoadWordArtCommand.Execute(null);
            StaThread.Pump();
        });
        rig.Ok("""{"id":1,"cmd":"pointer","action":"down","at":{"element":"AssetTile","x":20,"y":20}}""");
        rig.Ok("""{"id":2,"cmd":"pointer","action":"move","at":{"element":"AssetTile","x":32,"y":32}}""");   // past the system drag distance
        rig.Ok("""{"id":3,"cmd":"pointer","action":"move","at":{"canvas":{"x":300,"y":200}}}""");
        rig.Ok("""{"id":4,"cmd":"pointer","action":"up","at":{"canvas":{"x":300,"y":200}}}""");
        StaThread.Run(() =>
        {
            var l = Assert.Single(rig.Session.Document.Layers);
            Assert.Equal("word_626_anton", l.Name);                          // the first Word art tile (AssetLibraryPanelTests)
            Assert.Equal(1, rig.Session.UndoCount);
            Assert.False(First<AssetLibraryPanel>(rig.Window).IsDragging);
        });
    }

    [Fact] // H7's mid-drag read (critic C3): inspect "layers" reports the running drag, the marked source row and the target's drop line
    public void Inspect_layers_reports_a_row_drag_in_progress()
    {
        using var rig = Open(LayerOf(1), LayerOf(2), LayerOf(3));
        var (from, to) = StaThread.Run(() =>
        {
            var panel = First<LayersPanel>(rig.Window);
            Point UpperQuarter(int id)
            {
                var item = (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(rig.Main.Layers.Rows.Single(r => r.Id == id));
                return item.TranslatePoint(new Point(item.ActualWidth / 3, item.ActualHeight * 0.25), rig.Window);   // upper half, off the buttons
            }
            return (UpperQuarter(1), UpperQuarter(3));
        });
        string At(Point p) => "{\"window\":{\"x\":" + BridgeJson.Invariant(p.X) + ",\"y\":" + BridgeJson.Invariant(p.Y) + "}}";
        rig.Ok($$"""{"id":1,"cmd":"pointer","action":"down","at":{{At(from)}}}""");
        rig.Ok($$"""{"id":2,"cmd":"pointer","action":"move","at":{{At(new Point(from.X, from.Y - 12))}}}""");
        rig.Ok($$"""{"id":3,"cmd":"pointer","action":"move","at":{{At(to)}}}""");
        var mid = rig.Ok("""{"id":4,"cmd":"inspect","target":"layers"}""");
        rig.Ok($$"""{"id":5,"cmd":"pointer","action":"up","at":{{At(to)}}}""");
        var after = rig.Ok("""{"id":6,"cmd":"inspect","target":"layers"}""");

        Assert.True(mid["dragging"]!.GetValue<bool>());
        var rows = mid["rows"]!.AsArray().Select(n => (Id: n!["id"]!.GetValue<int>(), Source: n["dragSource"]!.GetValue<bool>(), Drop: n["drop"]!.GetValue<string>())).ToList();
        Assert.Equal(new[] { 3, 2, 1 }, rows.Select(r => r.Id));                 // top first, as the list shows them
        Assert.Equal((true, "None"), (rows[2].Source, rows[2].Drop));            // the dragged row (layer 1) is marked
        Assert.Equal((false, "Above"), (rows[0].Source, rows[0].Drop));          // the target row (layer 3) shows the line above
        Assert.False(after["dragging"]!.GetValue<bool>());
        Assert.All(after["rows"]!.AsArray(), n => Assert.Equal((false, "None"), (n!["dragSource"]!.GetValue<bool>(), n["drop"]!.GetValue<string>())));
    }

    [Fact] // a chord whose command opens a modal answers {"modal":true} at once; a click on the disabled owner sends HTERROR
    public void An_input_that_opens_a_modal_answers_at_once_and_a_click_on_the_owner_goes_through_set_cursor()
    {
        using var rig = Open();
        var (messages, dialog) = StaThread.Run(() =>
        {
            var log = new List<bool>();
            HwndSource.FromHwnd(new WindowInteropHelper(rig.Window).Handle)!.AddHook((IntPtr _, int msg, IntPtr _, IntPtr lParam, ref bool handled) =>
            {
                if (msg != 0x0020) return IntPtr.Zero;
                bool click = ThemedColorPicker.IsClickOnDisabledOwner(msg, lParam);
                log.Add(click);
                if (!click) return IntPtr.Zero;
                handled = true;            // as ThemedColorPicker.OwnerHook does, so Windows does not beep
                return new IntPtr(1);
            });
            var modal = new Window
            {
                Owner = rig.Window, ShowActivated = false, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -12000, Top = -11000, Width = 200, Height = 100,
            };
            rig.Window.InputBindings.Add(new KeyBinding(new RelayCommandStub(() => modal.ShowDialog()), Key.M, ModifierKeys.Control));
            return (log, modal);
        });
        var opened = rig.Ok("""{"id":1,"cmd":"key","key":"M","modifiers":["Ctrl"]}""");
        Assert.True(opened["modal"]!.GetValue<bool>());
        var click = rig.Ok("""{"id":2,"cmd":"pointer","action":"down","at":{"canvas":{"x":10,"y":10}}}""");
        Assert.True(click["disabledOwner"]!.GetValue<bool>());
        Assert.Equal((true, true), (click["ownerClick"]!.GetValue<bool>(), click["handled"]!.GetValue<bool>()));   // what the smoke's H10 reads (critic C3)
        StaThread.Run(() =>
        {
            Assert.Equal(new[] { true }, messages);
            Assert.Equal(CanvasGesture.None, First<TemplateCanvasHost>(rig.Window).Interaction!.Gesture);
            dialog.Close();
        });
    }

    sealed class RelayCommandStub(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }
}
```

Create `src/RBX15.App.Tests/BridgeSecurityTests.cs`:

```csharp
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RBX15.App.Testing;

namespace RBX15.App.Tests;

/// <summary>
/// The bridge's attack surface (M4a contract "Security"): who can open the pipe, what the launch flag matches, and
/// how strictly a request line is read. The pipe tests serve a fake <see cref="IInputBridge"/>, so no window and
/// no input is involved.
/// </summary>
public class BridgeSecurityTests
{
    sealed class EchoBridge : IInputBridge
    {
        public List<BridgeCommand> Seen { get; } = [];

        public Task<BridgeResponse> ExecuteAsync(BridgeCommand command)
        {
            Seen.Add(command);
            return Task.FromResult(new BridgeResponse(command.Id, true, null, new JsonObject { ["cmd"] = command.Cmd }));
        }
    }

    static SecurityIdentifier CurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User!;
    }

    static List<PipeAccessRule> Rules(PipeSecurity security) =>
        security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

    [Fact] // protected, owned by the current user, the only Allow is the current user, and NETWORK is denied
    public void The_pipe_acl_allows_only_the_current_user()
    {
        var security = TestInputBridge.CreatePipeSecurity();
        var user = CurrentUser();
        var network = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
        Assert.True(security.AreAccessRulesProtected);
        Assert.Equal(user, security.GetOwner(typeof(SecurityIdentifier)));
        var rules = Rules(security);
        var allow = Assert.Single(rules, r => r.AccessControlType == AccessControlType.Allow);
        Assert.Equal((user, PipeAccessRights.FullControl), ((SecurityIdentifier)allow.IdentityReference, allow.PipeAccessRights));
        var deny = Assert.Single(rules, r => r.AccessControlType == AccessControlType.Deny);
        Assert.Equal(network, deny.IdentityReference);
        Assert.All(rules, r => Assert.False(r.IsInherited));
    }

    [Fact] // the live pipe: only the current user is allowed; one request line, one response line; one client, then it closes for good
    public async Task The_pipe_serves_one_client_one_line_per_request_and_never_listens_again()
    {
        var fake = new EchoBridge();
        using var bridge = TestInputBridge.Listen(fake);
        Assert.Matches(new Regex("^rbx15-bridge-" + Environment.ProcessId + "-[0-9a-f]{32}$"), bridge.PipeName);
        var user = CurrentUser();
        Assert.All(Rules(bridge.LiveSecurity()).Where(r => r.AccessControlType == AccessControlType.Allow),
            r => Assert.Equal(user, r.IdentityReference));

        using (var client = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(5000);
            using var reader = new StreamReader(client, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n", AutoFlush = true };
            await writer.WriteLineAsync("""{"id":4,"cmd":"ping"}""");
            await writer.WriteLineAsync("""{"id":5,"cmd":"inspect","target":"canvas"}""");
            Assert.Equal("""{"id":4,"ok":true,"result":{"cmd":"ping"}}""", await reader.ReadLineAsync());
            Assert.Equal("""{"id":5,"ok":true,"result":{"cmd":"inspect"}}""", await reader.ReadLineAsync());
        }
        await bridge.Served.WaitAsync(TimeSpan.FromSeconds(5));
        using var second = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut);
        await Assert.ThrowsAsync<TimeoutException>(() => second.ConnectAsync(300));
        Assert.Equal(new[] { "ping", "inspect" }, fake.Seen.Select(c => c.Cmd));
    }

    [Theory] // an over-long line, a non-JSON line or invalid UTF-8 gets one error line with id 0, then the server disconnects
    [InlineData("long")]
    [InlineData("junk")]
    [InlineData("utf8")]
    [InlineData("dup")]
    public async Task A_bad_request_line_gets_one_error_and_a_disconnect(string kind)
    {
        var fake = new EchoBridge();
        using var bridge = TestInputBridge.Listen(fake);
        using var client = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        byte[] line = kind switch
        {
            "long" => Encoding.ASCII.GetBytes("{\"id\":1,\"cmd\":\"" + new string('a', TestInputBridge.MaxLineBytes) + "\"}\n"),
            "junk" => Encoding.ASCII.GetBytes("{not json\n"),
            "dup" => Encoding.ASCII.GetBytes("{\"id\":1,\"id\":2,\"cmd\":\"ping\"}\n"),   // critic M6: a repeated key
            _ => [0x7B, 0xC3, 0x28, 0x7D, 0x0A],
        };
        try
        {
            await client.WriteAsync(line);
        }
        catch (IOException)
        {
            // the server may close before the whole over-long line is written
        }
        using var reader = new StreamReader(client, new UTF8Encoding(false));
        var response = JsonNode.Parse((await reader.ReadLineAsync())!)!.AsObject();
        Assert.Equal((0, false), (response["id"]!.GetValue<int>(), response["ok"]!.GetValue<bool>()));
        Assert.Null(await reader.ReadLineAsync());
        Assert.Empty(fake.Seen);
    }

    [Fact] // the launch flag is matched exactly and case-sensitively
    public void Requested_needs_the_exact_flag()
    {
        Assert.True(TestInputBridge.Requested(["--test-input-bridge"]));
        Assert.True(TestInputBridge.Requested(["--other", "--test-input-bridge"]));
        Assert.False(TestInputBridge.Requested([]));
        Assert.False(TestInputBridge.Requested(["--TEST-INPUT-BRIDGE"]));
        Assert.False(TestInputBridge.Requested(["--test-input-bridge=1"]));
        Assert.False(TestInputBridge.Requested(["-test-input-bridge"]));
        Assert.False(TestInputBridge.Requested([" --test-input-bridge"]));
    }

    [Theory] // strict parsing: anything unexpected is refused with the reason, before it reaches the app
    [InlineData("""{"cmd":"ping"}""", "\"id\" is required")]
    [InlineData("""{"id":1}""", "\"cmd\" is required")]
    [InlineData("""{"id":1,"cmd":"ping","extra":1}""", "unknown field \"extra\"")]
    [InlineData("""{"id":"1","cmd":"ping"}""", "\"id\" must be a 32-bit integer")]
    [InlineData("""{"id":1,"cmd":"key","key":"S","modifiers":["Control"]}""", "unknown modifier \"Control\"")]
    [InlineData("""{"id":1,"cmd":"key","key":"S","modifiers":["Ctrl","Ctrl"]}""", "a modifier is listed twice")]
    [InlineData("""{"id":1,"cmd":"pointer","at":{"canvas":{"x":1}}}""", "\"at.canvas\" must be")]
    [InlineData("""{"id":1,"cmd":"pointer","at":{"canvas":{"x":1,"y":2},"window":{"x":1,"y":2}}}""", "\"at\" must be")]
    [InlineData("""{"id":1,"cmd":"pointer","at":{"element":"","x":1,"y":2}}""", "\"at.element\" must be a non-empty string")]
    [InlineData("""[1,2]""", "a request is one JSON object")]
    [InlineData("""{"id":1,"id":2,"cmd":"ping"}""", "a field is listed twice")]
    public void Parsing_refuses_anything_unexpected(string line, string reason)
    {
        var e = Assert.Throws<FormatException>(() => BridgeJson.Parse(line));
        Assert.StartsWith(reason, e.Message);
    }

    [Fact] // the three "at" forms, modifiers and a null answer
    public void Parsing_reads_every_form()
    {
        var c = BridgeJson.Parse("""{"id":3,"cmd":"pointer","action":"down","button":"right","at":{"canvas":{"x":-5.5,"y":600}}}""");
        Assert.Equal(new BridgePoint(null, -5.5, 600, Canvas: true, Window: false), c.At);
        Assert.Equal(("pointer", "down", "right"), (c.Cmd, c.Action, c.Button));
        Assert.Equal(new BridgePoint("SvSquare", 10, 20, false, false), BridgeJson.Parse("""{"id":1,"cmd":"pointer","at":{"element":"SvSquare","x":10,"y":20}}""").At);
        Assert.Equal(new BridgePoint(null, 900, -40, false, true), BridgeJson.Parse("""{"id":1,"cmd":"pointer","at":{"window":{"x":900,"y":-40}}}""").At);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, BridgeJson.Parse("""{"id":1,"cmd":"key","key":"S","modifiers":["Shift","Ctrl"]}""").Modifiers);
        var dialog = BridgeJson.Parse("""{"id":2,"cmd":"file-dialog","answer":null}""");
        Assert.Null(dialog.Answer);
        Assert.Equal("""{"id":2,"ok":false,"error":"no"}""", BridgeJson.Write(new BridgeResponse(2, false, "no", null)));
    }

    [Fact] // R10: no native dialog under the bridge; answers come from the queue, an empty queue cancels, every call is logged
    public void The_bridge_file_dialogs_answer_from_the_queue_and_log_every_call()
    {
        var log = new StringWriter();
        var dialogs = new BridgeFileDialogs(log);
        dialogs.Enqueue(@"C:\work\a.r15proj");
        dialogs.Enqueue(null);
        dialogs.Enqueue(@"C:\art\1.png|C:\art\2.png");
        Assert.Equal(@"C:\work\a.r15proj", dialogs.SaveProject("shirt.r15proj", null));
        Assert.Null(dialogs.OpenProject(@"C:\work"));
        Assert.Equal(new[] { @"C:\art\1.png", @"C:\art\2.png" }, dialogs.OpenImages());
        Assert.Null(dialogs.PickFolder(null));
        Assert.Equal(new[]
        {
            @"RBX15-BRIDGE-DIALOG SaveProject shirt.r15proj -> C:\work\a.r15proj",
            @"RBX15-BRIDGE-DIALOG OpenProject C:\work -> cancel",
            @"RBX15-BRIDGE-DIALOG OpenImages  -> C:\art\1.png|C:\art\2.png",
            @"RBX15-BRIDGE-DIALOG PickFolder  -> cancel (queue empty)",
        }, log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory] // R8's neutralizer: what an unmarked (hardware) report keeps
    [InlineData("AbsoluteMove", "AbsoluteMove")]
    [InlineData("Button1Press", "AbsoluteMove")]
    [InlineData("Activate, AbsoluteMove, Button1Press", "Activate, AbsoluteMove")]
    [InlineData("VerticalWheelRotate", "AbsoluteMove")]
    [InlineData("Activate", "Activate")]
    [InlineData("Deactivate", "Deactivate")]
    [InlineData("QueryCursor", "QueryCursor")]
    public void The_neutralizer_keeps_only_bookkeeping_plus_a_move(string actions, string kept) =>
        Assert.Equal(kept, InputBridgeDispatcher.Neutralize(actions));
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: the build fails with `error CS0234: The type or namespace name 'Testing' does not exist in the namespace 'RBX15.App'`, plus CS0246 for `InputBridgeDispatcher`, `BridgeCommand`, `BridgeResponse` and `IInputBridge` (measured).

- [ ] **Step 8: Implement the bridge**

Ledger: as Step 7. Every file below lives in `src/RBX15.App/Testing/`. The flagless app build removes them all, and the test assembly compiles them (R9).

Create `src/RBX15.App/Testing/BridgeJson.cs` (the records, `IInputBridge` and the strict wire format):

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;

namespace RBX15.App.Testing;

/// <summary>
/// Where a pointer command lands (M4a interfaces §3.8). Exactly one form is set:
/// <list type="bullet">
/// <item><see cref="Canvas"/>: (X, Y) in template pixels on the main window's canvas, outside 0..585 x 0..559 allowed;</item>
/// <item><see cref="Element"/>: (X, Y) in DIPs from the top-left of the element whose AutomationId (else Name) it is;</item>
/// <item><see cref="Window"/>: (X, Y) in main-window client DIPs; negative or past the size is outside the window.</item>
/// </list>
/// </summary>
public sealed record BridgePoint(string? Element, double X, double Y, bool Canvas, bool Window);

/// <summary>One request line. Fields a command does not use are left at their defaults.</summary>
public sealed record BridgeCommand(int Id, string Cmd, string? Action, string? Button, BridgePoint? At, int Delta,
    string? Key, ModifierKeys Modifiers, int TimeoutMs, string? Answer, string? Target);

/// <summary>One response line: Ok with an optional Result, or not Ok with an Error.</summary>
public sealed record BridgeResponse(int Id, bool Ok, string? Error, JsonObject? Result);

/// <summary>Executes one command on the app's UI thread.</summary>
public interface IInputBridge
{
    Task<BridgeResponse> ExecuteAsync(BridgeCommand command);
}

/// <summary>
/// The bridge's wire format: one JSON object per line (UTF-8 NDJSON). Parsing is strict, because the pipe is an input
/// surface: an unknown field, a wrong type, a missing id or cmd, or an unknown modifier is a <see cref="FormatException"/>
/// naming the problem, and the server answers it with an error line and disconnects.
/// </summary>
public static class BridgeJson
{
    static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "id", "cmd", "action", "button", "at", "delta", "key", "modifiers", "timeoutMs", "answer", "target",
    };

    /// <summary>Parses one request line. Throws <see cref="FormatException"/> with the reason.</summary>
    public static BridgeCommand Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(line, documentOptions: new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException e)
        {
            throw new FormatException("not JSON: " + e.Message);
        }
        if (root is not JsonObject o) throw new FormatException("a request is one JSON object");
        try
        {
            foreach (var (name, _) in o)
                if (!Fields.Contains(name)) throw new FormatException($"unknown field \"{name}\"");
        }
        catch (ArgumentException e)
        {
            // JsonObject builds its dictionary on first use and throws ArgumentException on a repeated key (measured,
            // .NET 10): a request that names a field twice is refused like any other malformed line (critic M6).
            throw new FormatException("a field is listed twice: " + e.Message);
        }
        int id = Int(o, "id") ?? throw new FormatException("\"id\" is required");
        string cmd = Str(o, "cmd") ?? throw new FormatException("\"cmd\" is required");
        return new BridgeCommand(id, cmd, Str(o, "action"), Str(o, "button"), Point(o["at"]), Int(o, "delta") ?? 0,
            Str(o, "key"), Modifiers(o["modifiers"]), Int(o, "timeoutMs") ?? 0, Str(o, "answer"), Str(o, "target"));
    }

    /// <summary>One response line, no indentation and no line break.</summary>
    public static string Write(BridgeResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var o = new JsonObject { ["id"] = response.Id, ["ok"] = response.Ok };
        if (response.Error is not null) o["error"] = response.Error;
        if (response.Result is not null) o["result"] = response.Result.DeepClone();
        return o.ToJsonString();
    }

    static string? Str(JsonObject o, string name) => o[name] switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out string? s) => s,
        _ => throw new FormatException($"\"{name}\" must be a string"),
    };

    static int? Int(JsonObject o, string name) => o[name] switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out int i) => i,
        _ => throw new FormatException($"\"{name}\" must be a 32-bit integer"),
    };

    static double Num(JsonObject o, string name) => o[name] switch
    {
        JsonValue v when v.TryGetValue(out double d) && double.IsFinite(d) => d,
        _ => throw new FormatException($"\"{name}\" must be a finite number"),
    };

    /// <summary>{"canvas":{"x","y"}}, {"window":{"x","y"}} or {"element":"id","x","y"}; nothing else.</summary>
    static BridgePoint? Point(JsonNode? node)
    {
        if (node is null) return null;
        if (node is not JsonObject at) throw new FormatException("\"at\" must be an object");
        string[] keys = at.Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        if (keys is ["canvas"] or ["window"])
        {
            if (at[keys[0]] is not JsonObject xy || xy.Count != 2 || !xy.ContainsKey("x") || !xy.ContainsKey("y"))
                throw new FormatException($"\"at.{keys[0]}\" must be {{\"x\":..,\"y\":..}}");
            return new BridgePoint(null, Num(xy, "x"), Num(xy, "y"), Canvas: keys[0] == "canvas", Window: keys[0] == "window");
        }
        if (keys is ["element", "x", "y"])
        {
            string element = Str(at, "element") is { Length: > 0 } e ? e : throw new FormatException("\"at.element\" must be a non-empty string");
            return new BridgePoint(element, Num(at, "x"), Num(at, "y"), Canvas: false, Window: false);
        }
        throw new FormatException("\"at\" must be {\"canvas\":{x,y}}, {\"window\":{x,y}} or {\"element\":id,\"x\":..,\"y\":..}");
    }

    /// <summary>An array of "Ctrl", "Shift" and "Alt", each at most once.</summary>
    static ModifierKeys Modifiers(JsonNode? node)
    {
        if (node is null) return ModifierKeys.None;
        if (node is not JsonArray list) throw new FormatException("\"modifiers\" must be an array");
        var result = ModifierKeys.None;
        foreach (var item in list)
        {
            var m = item is JsonValue v && v.TryGetValue(out string? s) ? s switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Shift" => ModifierKeys.Shift,
                "Alt" => ModifierKeys.Alt,
                _ => throw new FormatException($"unknown modifier \"{s}\" (Ctrl, Shift, Alt)"),
            } : throw new FormatException("\"modifiers\" holds strings");
            if ((result & m) != 0) throw new FormatException("a modifier is listed twice");
            result |= m;
        }
        return result;
    }

    internal static string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
```

Create `src/RBX15.App/Testing/BridgeKeyboardDevice.cs` (R7):

```csharp
using System.Windows.Input;

namespace RBX15.App.Testing;

/// <summary>
/// The bridge's own keyboard device (M4a interfaces R7). Key events built on it carry its <see cref="Modifiers"/>
/// in <c>e.KeyboardDevice.Modifiers</c>, which is where app key handlers read modifiers. It never touches the
/// system keyboard state, and it is never focused: the bridge sets each event's Source itself.
/// One instance per UI thread (the base constructor subscribes to that thread's InputManager for good).
/// </summary>
internal sealed class BridgeKeyboardDevice : KeyboardDevice
{
    ModifierKeys _modifiers;

    public BridgeKeyboardDevice() : base(InputManager.Current) { }

    /// <summary>The modifiers this device reports as held. KeyboardDevice.Modifiers reads them through GetKeyStatesFromSystem.</summary>
    public new ModifierKeys Modifiers
    {
        get => _modifiers;
        set => _modifiers = value;
    }

    protected override KeyStates GetKeyStatesFromSystem(Key key)
    {
        var held = key switch
        {
            Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
            Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
            Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
            _ => ModifierKeys.None,
        };
        return held != ModifierKeys.None && (_modifiers & held) != 0 ? KeyStates.Down : KeyStates.None;
    }
}
```

Create `src/RBX15.App/Testing/RawMouse.cs` (R8):

```csharp
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace RBX15.App.Testing;

/// <summary>
/// WPF's own raw mouse report, built by reflection and pushed through <see cref="InputManager.ProcessInput"/>
/// (M4a interfaces R8). This is the path WPF's HwndMouseInputProvider feeds from WM_MOUSE* messages, entered one
/// step later and in-process, so WPF does its own hit testing, MouseEnter and MouseLeave, the promotion to
/// MouseLeftButtonDown, capture routing and the wheel. Nothing leaves the process and no Windows input API is
/// called. Coordinates are device pixels in the source's client area. Every report this class builds carries
/// <see cref="InputBridgeDispatcher.Marker"/> in its extraInformation, which is how the bridge tells its own reports
/// from the hardware's. The internal types are looked up once, by name, and a missing one fails loudly.
/// </summary>
internal static class RawMouse
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Assembly Core = typeof(InputManager).Assembly;
    static readonly Type ReportType = Core.GetType("System.Windows.Input.RawMouseInputReport", throwOnError: true)!;
    static readonly Type ActionsType = Core.GetType("System.Windows.Input.RawMouseActions", throwOnError: true)!;
    static readonly Type ArgsType = Core.GetType("System.Windows.Input.InputReportEventArgs", throwOnError: true)!;
    static readonly Type InputReportType = Core.GetType("System.Windows.Input.InputReport", throwOnError: true)!;

    static readonly ConstructorInfo ReportCtor = ReportType.GetConstructor(Any, [typeof(InputMode), typeof(int), typeof(PresentationSource), ActionsType, typeof(int), typeof(int), typeof(int), typeof(IntPtr)])
        ?? throw new MissingMethodException("RawMouseInputReport(InputMode, int, PresentationSource, RawMouseActions, int, int, int, IntPtr)");
    static readonly ConstructorInfo ArgsCtor = ArgsType.GetConstructor(Any, [typeof(InputDevice), InputReportType])
        ?? throw new MissingMethodException("InputReportEventArgs(InputDevice, InputReport)");
    static readonly PropertyInfo ReportProperty = ArgsType.GetProperty("Report", Any) ?? throw new MissingMemberException("InputReportEventArgs.Report");
    static readonly PropertyInfo ActionsProperty = ReportType.GetProperty("Actions", Any) ?? throw new MissingMemberException("RawMouseInputReport.Actions");
    static readonly PropertyInfo ExtraProperty = ReportType.GetProperty("ExtraInformation", Any) ?? throw new MissingMemberException("RawMouseInputReport.ExtraInformation");
    static readonly PropertyInfo SourceProperty = InputReportType.GetProperty("InputSource", Any) ?? throw new MissingMemberException("InputReport.InputSource");

    /// <summary>InputManager.PreviewInputReportEvent, the routed event every raw report travels as.</summary>
    internal static readonly RoutedEvent PreviewInputReportEvent =
        (RoutedEvent)(typeof(InputManager).GetField("PreviewInputReportEvent", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new MissingFieldException("InputManager.PreviewInputReportEvent"));

    /// <summary>
    /// Pushes one report. <paramref name="actions"/> is RawMouseActions in Enum.Parse form ("AbsoluteMove",
    /// "Button1Press", "Activate, AbsoluteMove"); <paramref name="wheel"/> is the wheel delta for VerticalWheelRotate.
    /// </summary>
    public static void Inject(PresentationSource source, string actions, Point devicePixels, int wheel, nint extraInformation = InputBridgeDispatcher.Marker)
    {
        ArgumentNullException.ThrowIfNull(source);
        InputManager.Current.ProcessInput(Create(source, actions, devicePixels, wheel, extraInformation));
    }

    /// <summary>The report as input event args, ready for ProcessInput or a PushInput from a PreProcessInput handler.</summary>
    internal static InputEventArgs Create(PresentationSource source, string actions, Point devicePixels, int wheel, nint extraInformation = InputBridgeDispatcher.Marker)
    {
        object report = ReportCtor.Invoke([InputMode.Foreground, Environment.TickCount, source, Enum.Parse(ActionsType, actions),
            (int)Math.Round(devicePixels.X), (int)Math.Round(devicePixels.Y), wheel, (IntPtr)extraInformation]);
        var args = (InputEventArgs)ArgsCtor.Invoke([Mouse.PrimaryDevice, report]);
        args.RoutedEvent = PreviewInputReportEvent;
        return args;
    }

    /// <summary>Reads a raw mouse report: its actions (Enum.ToString form), extraInformation and source. False for anything else.</summary>
    internal static bool TryRead(InputEventArgs input, out string actions, out nint extraInformation, out PresentationSource? source)
    {
        actions = "";
        extraInformation = 0;
        source = null;
        if (input.RoutedEvent != PreviewInputReportEvent || !ArgsType.IsInstanceOfType(input)) return false;
        object? report = ReportProperty.GetValue(input);
        if (report is null || !ReportType.IsInstanceOfType(report)) return false;
        actions = ActionsProperty.GetValue(report)!.ToString()!;
        extraInformation = (IntPtr)ExtraProperty.GetValue(report)!;
        source = SourceProperty.GetValue(report) as PresentationSource;
        return true;
    }
}
```

Create `src/RBX15.App/Testing/BridgeFileDialogs.cs` (R10):

```csharp
using System.IO;
using RBX15.App.Services;

namespace RBX15.App.Testing;

/// <summary>
/// The file dialogs under the bridge (M4a interfaces R10): no native dialog ever opens. Each call takes the oldest
/// answer the <c>file-dialog</c> command queued; an empty queue or a null answer is a cancel. Every call is written
/// to the log (stdout in the app) as <c>RBX15-BRIDGE-DIALOG &lt;method&gt; &lt;arg&gt; -&gt; &lt;answer&gt;</c>, so the
/// smoke driver sees what the app asked for. OpenImages splits an answer on '|' (no Windows path holds one).
/// </summary>
internal sealed class BridgeFileDialogs(TextWriter log) : IFileDialogs
{
    public const string LogPrefix = "RBX15-BRIDGE-DIALOG ";

    readonly Queue<string?> _answers = new();
    readonly TextWriter _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <summary>Queues the next answer: a path, or null to cancel.</summary>
    public void Enqueue(string? answer) => _answers.Enqueue(answer);

    public IReadOnlyList<string> OpenImages() =>
        Next(nameof(OpenImages), "") is { } answer ? answer.Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

    public string? SavePng(string suggestedFileName) => Next(nameof(SavePng), suggestedFileName);

    public string? OpenProject(string? initialDirectory) => Next(nameof(OpenProject), initialDirectory ?? "");

    public string? SaveProject(string suggestedFileName, string? initialDirectory) => Next(nameof(SaveProject), suggestedFileName);

    public string? PickFolder(string? initialDirectory) => Next(nameof(PickFolder), initialDirectory ?? "");

    string? Next(string method, string arg)
    {
        bool queued = _answers.TryDequeue(out var answer);
        _log.WriteLine($"{LogPrefix}{method} {arg} -> {(queued ? answer ?? "cancel" : "cancel (queue empty)")}");
        _log.Flush();
        return queued ? answer : null;
    }
}
```

Create `src/RBX15.App/Testing/InputBridgeDispatcher.cs` (R7, R8, T4-R3 to T4-R6, T4-R8, T4-R13):

```csharp
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Views;
using SkiaSharp;

namespace RBX15.App.Testing;

/// <summary>
/// Runs bridge commands on the app's UI thread, into the app's own visual tree only (M4a contract "test input
/// bridge"; interfaces R7, R8). Tests drive it directly; <see cref="TestInputBridge"/> puts a pipe in front of it.
/// <list type="bullet">
/// <item><b>Pointer and wheel</b> go through WPF's raw mouse report (<see cref="RawMouse"/>), so WPF hit-tests,
///   promotes, captures and routes them as it does hardware input. Each command sends an Activate report when the
///   mouse device is not active on the target's window, a move-only report, then a button-only or wheel-only report.
///   The left button's state reaches app code through <see cref="PointerState"/>.</item>
/// <item><b>While it lives, hardware cannot act on the app.</b> A PreProcessInput handler cancels every raw mouse
///   report without <see cref="Marker"/> that moves, presses or turns the wheel, and replaces it with a move to the
///   bridge's last point (WPF's own Mouse.Synchronize reads the real cursor; this keeps it from moving the pointer).
///   It is a WPF event handler in this process, not a Windows hook.</item>
/// <item><b>Keys</b> are raised on a <see cref="BridgeKeyboardDevice"/>, so <c>e.KeyboardDevice.Modifiers</c> carries
///   the command's modifiers. A chord's KeyBindings are matched by the bridge itself, because WPF's KeyGesture.Matches
///   reads the physical modifier state (R7).</item>
/// <item><b>A click on a window disabled by a modal</b> does what Windows does: it sends WM_SETCURSOR with HTERROR and
///   the button-down message to that window's own HWND (ThemedColorPicker.OwnerHook commits on it). That is the only
///   message the bridge ever sends, and only to this process's windows.</item>
/// </list>
/// A command whose input opens a modal window answers <c>{"modal":true}</c> as soon as the modal opens; the next
/// command runs inside the modal's loop. An exception thrown by app code is answered as an error starting
/// <see cref="AppExceptionPrefix"/>, never swallowed silently.
/// </summary>
public sealed class InputBridgeDispatcher : IInputBridge, IDisposable
{
    /// <summary>The extraInformation every bridge mouse report carries: "RBX1".</summary>
    public const nint Marker = 0x52425831;

    /// <summary>The error prefix for an exception that app code threw while handling bridge input.</summary>
    public const string AppExceptionPrefix = "APP-EXCEPTION ";

    const int DefaultTimeoutMs = 5000, MaxTimeoutMs = 60000;
    const int WM_SETCURSOR = 0x0020, HTERROR = -2;

    [ThreadStatic] static BridgeKeyboardDevice? t_keyboard;
    static readonly MethodInfo? MarkSystem = typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly MainWindow _main;
    readonly Dispatcher _dispatcher;
    readonly BridgeFileDialogs? _fileDialogs;
    bool _leftDown;
    Point? _lastScreen;
    bool _disposed;

    /// <summary>Installs the hardware neutralizer and the left-button override. UI thread.</summary>
    public InputBridgeDispatcher(MainWindow main) : this(main, null) { }

    internal InputBridgeDispatcher(MainWindow main, BridgeFileDialogs? fileDialogs)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _dispatcher = main.Dispatcher;
        _dispatcher.VerifyAccess();
        _fileDialogs = fileDialogs;
        InputManager.Current.PreProcessInput += OnPreProcessInput;
        PointerState.LeftButtonOverride = () => _leftDown ? MouseButtonState.Pressed : MouseButtonState.Released;
    }

    /// <summary>The physical keyboard's modifiers. A key command refuses while any is held (R7). Tests replace it.</summary>
    internal Func<ModifierKeys> PhysicalModifiers { get; set; } = () => Keyboard.PrimaryDevice.Modifiers;

    /// <summary>Removes the neutralizer and the left-button override. UI thread; a second call does nothing.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _dispatcher.VerifyAccess();
        _disposed = true;
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        PointerState.LeftButtonOverride = null;
    }

    public async Task<BridgeResponse> ExecuteAsync(BridgeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            JsonObject? result = command.Cmd switch
            {
                "ping" => await OnUi(() => new JsonObject { ["pid"] = Environment.ProcessId, ["version"] = AppInfo.Version }),
                "pointer" => await OnUi(() => Pointer(command)),
                "wheel" => await OnUi(() => Wheel(command)),
                "key" => await OnUi(() => KeyCommand(command)),
                "wait-idle" => await WaitIdle(command.TimeoutMs),
                "file-dialog" => await OnUi(() => FileDialog(command)),
                "inspect" => await OnUi(() => Inspect(command)),
                _ => throw new BridgeException($"unknown cmd \"{command.Cmd}\""),
            };
            return new BridgeResponse(command.Id, true, null, result);
        }
        catch (BridgeException e)
        {
            return new BridgeResponse(command.Id, false, e.Message, null);
        }
        catch (Exception e)
        {
            return new BridgeResponse(command.Id, false, AppExceptionPrefix + e.GetType().Name + ": " + e.Message, null);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread. If the input it raises opens a modal window
    /// (ComponentDispatcher.EnterThreadModal), the task completes then with {"modal":true}, so the client is not
    /// blocked until the modal closes; later commands run inside the modal's loop.
    /// </summary>
    Task<JsonObject?> OnUi(Func<JsonObject?> action)
    {
        var done = new TaskCompletionSource<JsonObject?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var op = _dispatcher.InvokeAsync(() =>
        {
            EventHandler modal = (_, _) => done.TrySetResult(new JsonObject { ["modal"] = true });
            ComponentDispatcher.EnterThreadModal += modal;
            try
            {
                if (_disposed) throw new BridgeException("the bridge is closed");
                done.TrySetResult(action());
            }
            catch (Exception e)
            {
                done.TrySetException(e);
            }
            finally
            {
                ComponentDispatcher.EnterThreadModal -= modal;
            }
        });
        op.Aborted += (_, _) => done.TrySetException(new BridgeException("the app's dispatcher shut down"));
        return done.Task;
    }

    // ---- pointer and wheel (R8) ----

    readonly record struct Target(PresentationSource Source, Point Device, Point Screen, Window Window, bool OwnerDisabled);

    JsonObject Pointer(BridgeCommand c)
    {
        string action = c.Action is "down" or "move" or "up" ? c.Action : throw new BridgeException("\"action\" must be down, move or up");
        int button = (c.Button ?? "left") switch
        {
            "left" => 1,
            "right" => 2,
            "middle" => 3,
            var other => throw new BridgeException($"unknown button \"{other}\" (left, right, middle)"),
        };
        var target = Resolve(c.At);
        if (target.OwnerDisabled)
        {
            // Windows sends a disabled window no mouse input; a press reaches it only as WM_SETCURSOR + HTERROR.
            // ownerClick and handled are what the smoke's H10 reads (critic C3; Task 5 Addition 3).
            bool? handled = action == "down" ? ClickDisabledWindow(target.Window, button) : null;
            var described = Describe(target);
            if (handled is { } h)
            {
                described["ownerClick"] = true;
                described["handled"] = h;
            }
            return described;
        }
        EnsureActive(target);
        MoveTo(target);
        if (action == "down")
        {
            if (button == 1) _leftDown = true;
            RawMouse.Inject(target.Source, $"Button{button}Press", target.Device, 0);
        }
        else if (action == "up")
        {
            if (button == 1) _leftDown = false;
            RawMouse.Inject(target.Source, $"Button{button}Release", target.Device, 0);
        }
        return Describe(target);
    }

    JsonObject Wheel(BridgeCommand c)
    {
        if (c.Delta == 0) throw new BridgeException("\"delta\" must be nonzero (120 is one notch)");
        var target = Resolve(c.At);
        if (target.OwnerDisabled) return Describe(target);
        EnsureActive(target);
        MoveTo(target);
        RawMouse.Inject(target.Source, "VerticalWheelRotate", target.Device, c.Delta);
        return Describe(target);
    }

    void EnsureActive(Target target)
    {
        if (!ReferenceEquals(Mouse.PrimaryDevice.ActiveSource, target.Source)) RawMouse.Inject(target.Source, "Activate", target.Device, 0);
    }

    void MoveTo(Target target)
    {
        RawMouse.Inject(target.Source, "AbsoluteMove", target.Device, 0);
        _lastScreen = target.Screen;
    }

    Target Resolve(BridgePoint? at)
    {
        if (at is null) throw new BridgeException("\"at\" is required");
        Visual anchor;
        Point dip = new(at.X, at.Y);
        if (at.Canvas)
        {
            var host = Host() ?? throw new BridgeException("the main window has no canvas");
            anchor = host;
            dip = CanvasCoordinates.ToFrame(dip, host.Zoom);
        }
        else if (at.Window)
        {
            anchor = _main;
        }
        else
        {
            anchor = FindElement(at.Element!) ?? throw new BridgeException($"no element \"{at.Element}\" in the app's windows");
        }
        var source = PresentationSource.FromVisual(anchor) ?? throw new BridgeException("the target is not shown");
        var root = source.RootVisual ?? throw new BridgeException("the target's window has no root");
        Point rootDip = ReferenceEquals(anchor, root) ? dip : anchor.TransformToAncestor(root).Transform(dip);
        Point device = source.CompositionTarget.TransformToDevice.Transform(rootDip);
        var window = (anchor as DependencyObject is { } d ? Window.GetWindow(d) : null) ?? _main;
        bool disabled = !IsWindowEnabled(new WindowInteropHelper(window).Handle);
        return new Target(source, device, anchor.PointToScreen(dip), window, disabled);
    }

    /// <summary>Sends the owner its WM_SETCURSOR + HTERROR; true when a hook handled it (nonzero result, so Windows does not beep).</summary>
    static bool ClickDisabledWindow(Window window, int button)
    {
        int down = button switch { 1 => 0x0201, 2 => 0x0204, _ => 0x0207 };   // WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN
        nint hwnd = new WindowInteropHelper(window).Handle;
        return SendMessage(hwnd, WM_SETCURSOR, hwnd, (nint)((down << 16) | (HTERROR & 0xFFFF))) != 0;
    }

    JsonObject Describe(Target target) => new()
    {
        ["x"] = target.Device.X,
        ["y"] = target.Device.Y,
        ["over"] = Name(Mouse.DirectlyOver),
        ["captured"] = Name(Mouse.Captured),
        ["disabledOwner"] = target.OwnerDisabled,
    };

    /// <summary>
    /// The hardware neutralizer (R8): an unmarked raw mouse report that moves, presses or turns the wheel is cancelled
    /// and replaced by a marked move to the bridge's last point. Activate, Deactivate and WPF's own bookkeeping pass.
    /// </summary>
    void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (!RawMouse.TryRead(e.StagingItem.Input, out var actions, out var extra, out var source) || extra == Marker) return;
        var kept = Neutralize(actions);
        if (kept == actions) return;
        e.Cancel();
        if (source?.RootVisual is not { } root) return;
        if (_lastScreen is { } screen)
        {
            var device = source.CompositionTarget.TransformToDevice.Transform(root.PointFromScreen(screen));
            e.PushInput(RawMouse.Create(source, kept, device, 0), e.StagingItem);
        }
        else if (Neutralize(actions, withMove: false) is { Length: > 0 } rest)
        {
            e.PushInput(RawMouse.Create(source, rest, default, 0), e.StagingItem);
        }
    }

    /// <summary>
    /// The actions an unmarked report keeps: unchanged when it neither moves, presses nor turns the wheel; otherwise
    /// only its Activate, Deactivate, AttributesChanged, QueryCursor and CancelCapture parts, plus AbsoluteMove
    /// (to the bridge's last point) when <paramref name="withMove"/>.
    /// </summary>
    internal static string Neutralize(string actions, bool withMove = true)
    {
        var parts = actions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool acts = parts.Any(p => p is "AbsoluteMove" or "RelativeMove" or "VirtualDesktopMove" or "VerticalWheelRotate" or "HorizontalWheelRotate"
                                   || p.StartsWith("Button", StringComparison.Ordinal));
        if (!acts) return actions;
        var kept = parts.Where(p => p is "Activate" or "Deactivate" or "AttributesChanged" or "QueryCursor" or "CancelCapture");
        return string.Join(", ", withMove ? kept.Append("AbsoluteMove") : kept);
    }

    // ---- keys (R7) ----

    JsonObject KeyCommand(BridgeCommand c)
    {
        if (c.Key is null || !Enum.GetNames<Key>().Contains(c.Key, StringComparer.Ordinal))
            throw new BridgeException($"unknown key \"{c.Key}\" (a System.Windows.Input.Key name)");
        var key = Enum.Parse<Key>(c.Key);
        if (key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
            throw new BridgeException($"\"{c.Key}\" is not sent as a key; modifiers go in \"modifiers\"");
        string action = (c.Action ?? "press") is "press" or "down" or "up" ? c.Action ?? "press" : throw new BridgeException("\"action\" must be press, down or up");
        var physical = PhysicalModifiers();
        if (physical != ModifierKeys.None)
            throw new BridgeException($"physical modifier keys are held ({physical}); release them and retry");

        var target = KeyTarget();
        var source = PresentationSource.FromDependencyObject((DependencyObject)target) ?? PresentationSource.FromVisual(_main)
            ?? throw new BridgeException("the app has no shown window");
        var keyboard = t_keyboard ??= new BridgeKeyboardDevice();
        keyboard.Modifiers = c.Modifiers;
        var result = new JsonObject { ["target"] = Name(target) };
        try
        {
            if (action is "press" or "down")
            {
                var preview = Raise(keyboard, source, key, Keyboard.PreviewKeyDownEvent, target, c.Modifiers);
                result["previewHandled"] = preview.Handled;
                if (!preview.Handled)
                {
                    if (c.Modifiers == ModifierKeys.None) result["keyDownHandled"] = Raise(keyboard, source, key, Keyboard.KeyDownEvent, target, c.Modifiers).Handled;
                    else result["binding"] = RunKeyBinding(target, key, c.Modifiers);
                }
            }
            if (action is "press" or "up")
            {
                var preview = Raise(keyboard, source, key, Keyboard.PreviewKeyUpEvent, target, c.Modifiers);
                if (!preview.Handled && c.Modifiers == ModifierKeys.None) Raise(keyboard, source, key, Keyboard.KeyUpEvent, target, c.Modifiers);
            }
        }
        finally
        {
            keyboard.Modifiers = ModifierKeys.None;
        }
        return result;
    }

    static KeyEventArgs Raise(BridgeKeyboardDevice keyboard, PresentationSource source, Key key, RoutedEvent routedEvent, IInputElement target, ModifierKeys modifiers)
    {
        var args = new KeyEventArgs(keyboard, source, Environment.TickCount, key) { RoutedEvent = routedEvent, Source = target };
        // Real input delivers an Alt chord as Key.System with SystemKey = the key; handlers read it that way.
        if ((modifiers & ModifierKeys.Alt) != 0)
            (MarkSystem ?? throw new BridgeException("KeyEventArgs.MarkSystem is missing")).Invoke(args, null);
        InputManager.Current.ProcessInput(args);
        return args;
    }

    /// <summary>
    /// CommandManager's KeyDown binding match, done here because KeyGesture.Matches reads the physical modifiers (R7):
    /// from the target up through visual parents (logical when there is none) to the window, the first KeyBinding
    /// with this key and these exact modifiers whose command can execute runs, and the walk stops. Null if none ran.
    /// </summary>
    static string? RunKeyBinding(IInputElement target, Key key, ModifierKeys modifiers)
    {
        for (var d = target as DependencyObject; d is not null; d = Parent(d))
        {
            var bindings = d switch
            {
                UIElement u => u.InputBindings,
                ContentElement c => c.InputBindings,
                UIElement3D u3 => u3.InputBindings,
                _ => null,
            };
            var match = bindings?.OfType<KeyBinding>().FirstOrDefault(b => b.Gesture is KeyGesture g && g.Key == key && g.Modifiers == modifiers);
            if (match?.Command is not { } command) continue;
            var commandTarget = match.CommandTarget ?? d as IInputElement;
            if (command is RoutedCommand routed)
            {
                if (!routed.CanExecute(match.CommandParameter, commandTarget)) continue;
                routed.Execute(match.CommandParameter, commandTarget);
            }
            else
            {
                if (!command.CanExecute(match.CommandParameter)) continue;
                command.Execute(match.CommandParameter);
            }
            return command.GetType().Name;
        }
        return null;
    }

    static DependencyObject? Parent(DependencyObject d) =>
        (d is Visual or Visual3D ? VisualTreeHelper.GetParent(d) : null) ?? LogicalTreeHelper.GetParent(d);

    /// <summary>Keyboard focus when it is in an app window; else the active app window's (or the main window's) focused element; else that window.</summary>
    IInputElement KeyTarget()
    {
        if (Keyboard.FocusedElement is DependencyObject focused && Window.GetWindow(focused) is { } owner && AppWindows().Contains(owner))
            return (IInputElement)focused;
        var window = AppWindows().FirstOrDefault(w => w.IsActive) ?? _main;
        var element = ReferenceEquals(window, _main) ? _main.FocusedElement() : FocusManager.GetFocusedElement(window);
        return element ?? window;
    }

    // ---- wait-idle, file-dialog, inspect ----

    async Task<JsonObject?> WaitIdle(int timeoutMs)
    {
        if (timeoutMs < 0 || timeoutMs > MaxTimeoutMs) throw new BridgeException($"\"timeoutMs\" must be 0..{MaxTimeoutMs}");
        int limit = timeoutMs == 0 ? DefaultTimeoutMs : timeoutMs;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            string? busy = await _dispatcher.InvokeAsync(BusyReason, DispatcherPriority.ApplicationIdle);
            if (busy is null) return new JsonObject { ["ms"] = clock.ElapsedMilliseconds };
            if (clock.ElapsedMilliseconds >= limit) throw new BridgeException($"not idle after {limit} ms: {busy}");
            await Task.Delay(10);
        }
    }

    /// <summary>Why the app is not idle yet, or null: an open wheel burst, a frame waiting to render, a project read.</summary>
    internal string? BusyReason()
    {
        var host = Host();
        if (host is { WheelBurstPending: true }) return "a wheel burst is open";
        if (host is { RenderingHooked: true }) return "a frame is waiting to render";
        if (_main.ViewModel.Files.IsBusy) return "a project is being read";
        return null;
    }

    JsonObject FileDialog(BridgeCommand c)
    {
        var dialogs = _fileDialogs ?? throw new BridgeException("this launch has no bridge file dialogs");
        dialogs.Enqueue(c.Answer);
        return new JsonObject { ["queued"] = c.Answer };
    }

    /// <summary>Read-only state for the smoke's assertions (interfaces §2.7; Task 5 Additions 1 and 2): no input, no change.</summary>
    JsonObject Inspect(BridgeCommand c) => c.Target switch
    {
        "canvas" => InspectCanvas(),
        "layers" => InspectLayers(),
        _ => throw new BridgeException("\"target\" must be \"canvas\" or \"layers\""),
    };

    /// <summary>
    /// The canvas: the cursor the canvas sets (kind and ring, the ring's radius in device px, the host's WPF cursor and
    /// any override, by name), zoom, gesture, capture, history, selection, tool, brush, the layers bottom to top with
    /// their image size and image identity, and the region fills.
    /// </summary>
    JsonObject InspectCanvas()
    {
        var host = Host() ?? throw new BridgeException("the main window has no canvas");
        var session = _main.ViewModel.Session;
        var layers = new JsonArray();
        foreach (var l in session.Document.Layers)
            layers.Add(new JsonObject
            {
                ["id"] = l.Id, ["name"] = l.Name, ["x"] = l.X, ["y"] = l.Y, ["scale"] = l.Scale, ["rotation"] = l.Rotation, ["visible"] = l.Visible,
                ["w"] = l.Image.Width, ["h"] = l.Image.Height, ["image"] = ImageId(l.Image),
            });
        var fills = new JsonArray();
        foreach (var (region, fill) in session.Document.RegionFills)
            fills.Add(new JsonObject { ["region"] = region, ["type"] = fill.Type, ["c1"] = fill.C1, ["c2"] = fill.C2 });
        var cursor = host.CanvasCursor;
        return new JsonObject
        {
            ["cursor"] = new JsonObject { ["kind"] = cursor.Kind.ToString(), ["ring"] = cursor.RingDiameter },
            ["ringRadiusPx"] = cursor.Kind == CanvasCursorKind.EraserRing ? CanvasCursors.RingRadius(cursor.RingDiameter, host.Zoom, host.DeviceScale) : 0.0,
            ["hostCursor"] = CursorName(host.Cursor),
            ["overrideCursor"] = CursorName(Mouse.OverrideCursor),
            ["zoom"] = host.Zoom,
            ["gesture"] = host.Interaction?.Gesture.ToString(),
            ["captured"] = host.IsMouseCaptured,
            ["undo"] = session.UndoCount,
            ["redo"] = session.RedoCount,
            ["selected"] = session.SelectedLayerId,
            ["tool"] = _main.ViewModel.Home.Tools.Current.ToString(),
            ["brush"] = _main.ViewModel.Home.Brush.Size,
            ["layers"] = layers,
            ["fills"] = fills,
        };
    }

    /// <summary>The layers panel: whether a row drag runs, and each row top first with its drag mark and drop line.</summary>
    JsonObject InspectLayers()
    {
        var panel = Descendants(_main).OfType<LayersPanel>().FirstOrDefault() ?? throw new BridgeException("the main window has no layers panel");
        var rows = new JsonArray();
        foreach (var r in _main.ViewModel.Layers.Rows)
            rows.Add(new JsonObject { ["id"] = r.Id, ["name"] = r.Name, ["dragSource"] = r.IsDragSource, ["drop"] = r.DropIndicator.ToString() });
        return new JsonObject { ["dragging"] = panel.IsDragging, ["rows"] = rows };
    }

    /// <summary>The stock WPF cursors the app sets, matched by reference (the Cursors members return cached instances).</summary>
    static readonly (Cursor Cursor, string? Name)[] KnownCursors =
    [
        (Cursors.Cross, "Cross"), (Cursors.SizeAll, "SizeAll"), (Cursors.Hand, "Hand"), (Cursors.SizeNWSE, "SizeNWSE"),
        (Cursors.SizeNS, "SizeNS"), (Cursors.Arrow, "Arrow"), (Cursors.No, "No"), (Cursors.Wait, "Wait"),
    ];

    /// <summary>A stock cursor's name, "Custom" for any other (the eraser ring), null for none.</summary>
    static string? CursorName(Cursor? cursor) =>
        cursor is null ? null : KnownCursors.FirstOrDefault(k => ReferenceEquals(k.Cursor, cursor)).Name ?? "Custom";

    static readonly ConditionalWeakTable<SKImage, StrongBox<long>> ImageIds = new();
    static long s_nextImageId;

    /// <summary>A per-process identity number for an image instance: equal for the same instance, new for a replaced one.</summary>
    static long ImageId(SKImage image) => ImageIds.GetValue(image, _ => new StrongBox<long>(Interlocked.Increment(ref s_nextImageId))).Value;

    // ---- the app's own windows and elements ----

    IEnumerable<Window> AppWindows()
    {
        yield return _main;
        if (Application.Current is { } app)
            foreach (Window w in app.Windows)
                if (!ReferenceEquals(w, _main)) yield return w;
    }

    TemplateCanvasHost? Host() => Descendants(_main).OfType<TemplateCanvasHost>().FirstOrDefault();

    /// <summary>The first element in visual-tree order, over the app's windows, whose AutomationId (else Name) is <paramref name="id"/>.</summary>
    internal Visual? FindElement(string id)
    {
        foreach (var window in AppWindows())
            foreach (var d in Descendants(window))
                if (d is Visual v && (AutomationProperties.GetAutomationId(d) is { Length: > 0 } aid ? aid == id : (d as FrameworkElement)?.Name == id))
                    return v;
        return null;
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            yield return d;
            int n = d is Visual or Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = n - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
    }

    static string? Name(IInputElement? element) => element switch
    {
        null => null,
        DependencyObject d when AutomationProperties.GetAutomationId(d) is { Length: > 0 } id => id,
        FrameworkElement { Name.Length: > 0 } f => f.Name,
        _ => element.GetType().Name,
    };

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowEnabled(nint hWnd);

    [DllImport("user32.dll")]
    static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
}

/// <summary>A command the bridge refuses (bad arguments, no such element): answered as an error, never thrown to the app.</summary>
public sealed class BridgeException(string message) : Exception(message);
```

Create `src/RBX15.App/Testing/TestInputBridge.cs` (§3.8 "Pipe", T4-R1, T4-R2, T4-R12):

```csharp
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Threading;

namespace RBX15.App.Testing;

/// <summary>
/// The test input bridge's pipe (M4a contract "test input bridge"; interfaces §3.8). It exists only in a build made
/// with <c>-p:EnableTestInputBridge=true</c>, and only starts when that build is launched with <see cref="LaunchFlag"/>.
/// <list type="bullet">
/// <item>One named pipe per launch, <c>rbx15-bridge-&lt;pid&gt;-&lt;32 random hex&gt;</c>, printed once to stdout as
///   <see cref="PipeLinePrefix"/> + name. Its ACL (<see cref="CreatePipeSecurity"/>) is protected, owned by the
///   current user, allows only the current user and denies network logons.</item>
/// <item>One client for the process lifetime: when it disconnects, the server closes, never listens again, and the
///   dispatcher is disposed, so hardware input works again.</item>
/// <item>UTF-8 NDJSON: one request line, one response line. A line over <see cref="MaxLineBytes"/>, invalid UTF-8 or
///   an unparsable request gets one error line (id 0) and a disconnect.</item>
/// </list>
/// </summary>
public sealed class TestInputBridge : IDisposable
{
    public const string LaunchFlag = "--test-input-bridge";
    public const string PipeLinePrefix = "RBX15-BRIDGE-PIPE ";

    /// <summary>The longest request line, in bytes, before the newline.</summary>
    internal const int MaxLineBytes = 64 * 1024;

    /// <summary>The pipe's in and out buffer sizes: a pipelining client never deadlocks against a response the server is writing.</summary>
    const int PipeBufferBytes = 128 * 1024;

    static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    readonly NamedPipeServerStream _server;
    readonly IInputBridge _bridge;
    readonly IDisposable? _owned;
    readonly Dispatcher? _ownerDispatcher;
    readonly CancellationTokenSource _stop = new();

    TestInputBridge(string pipeName, IInputBridge bridge, IDisposable? owned, Dispatcher? ownerDispatcher)
    {
        PipeName = pipeName;
        _bridge = bridge;
        _owned = owned;
        _ownerDispatcher = ownerDispatcher;
        _server = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, PipeBufferBytes, PipeBufferBytes, CreatePipeSecurity());
        Served = Task.Run(ServeAsync);
    }

    public string PipeName { get; }

    /// <summary>Completes when the server has closed for good. Read by tests.</summary>
    internal Task Served { get; }

    /// <summary>True when <paramref name="args"/> holds <see cref="LaunchFlag"/> exactly (case-sensitive).</summary>
    public static bool Requested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(a => string.Equals(a, LaunchFlag, StringComparison.Ordinal));
    }

    /// <summary>Starts the bridge on <paramref name="main"/>'s UI thread and prints the pipe line to <paramref name="stdout"/>.</summary>
    public static TestInputBridge Start(MainWindow main, TextWriter stdout) => Start(main, stdout, null);

    internal static TestInputBridge Start(MainWindow main, TextWriter stdout, BridgeFileDialogs? fileDialogs)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(stdout);
        var dispatcher = new InputBridgeDispatcher(main, fileDialogs);
        var bridge = new TestInputBridge(NewPipeName(), dispatcher, dispatcher, main.Dispatcher);
        stdout.WriteLine(PipeLinePrefix + bridge.PipeName);
        stdout.Flush();
        return bridge;
    }

    /// <summary>A server over any <see cref="IInputBridge"/>; tests use it with a fake.</summary>
    internal static TestInputBridge Listen(IInputBridge bridge) => new(NewPipeName(), bridge, null, null);

    internal static string NewPipeName() =>
        $"rbx15-bridge-{Environment.ProcessId}-{RandomNumberGenerator.GetHexString(32, lowercase: true)}";

    /// <summary>Protected (no inherited entries); owner and only Allow: the current user, FullControl; Deny: NETWORK.</summary>
    internal static PipeSecurity CreatePipeSecurity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("the current user has no SID");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    /// <summary>The live pipe's security, as Windows reports it. Read by tests.</summary>
    internal PipeSecurity LiveSecurity() => _server.GetAccessControl();

    public void Dispose()
    {
        _stop.Cancel();
        _server.Dispose();
        if (_ownerDispatcher is null || _ownerDispatcher.CheckAccess()) _owned?.Dispose();
        else _ownerDispatcher.InvokeAsync(() => _owned?.Dispose());
    }

    async Task ServeAsync()
    {
        try
        {
            await _server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
            var buffer = new List<byte>();
            var chunk = new byte[4096];
            int scanned = 0;
            while (true)
            {
                int newline = buffer.IndexOf((byte)'\n', scanned);
                if (newline < 0)
                {
                    scanned = buffer.Count;
                    if (buffer.Count > MaxLineBytes)
                    {
                        await WriteAsync(new BridgeResponse(0, false, $"a request line is longer than {MaxLineBytes} bytes", null)).ConfigureAwait(false);
                        return;
                    }
                    int read = await _server.ReadAsync(chunk, _stop.Token).ConfigureAwait(false);
                    if (read == 0) return;
                    buffer.AddRange(chunk.AsSpan(0, read));
                    continue;
                }
                if (newline > MaxLineBytes)
                {
                    await WriteAsync(new BridgeResponse(0, false, $"a request line is longer than {MaxLineBytes} bytes", null)).ConfigureAwait(false);
                    return;
                }
                var bytes = buffer.GetRange(0, newline).ToArray();
                buffer.RemoveRange(0, newline + 1);
                scanned = 0;
                BridgeCommand command;
                try
                {
                    command = BridgeJson.Parse(Utf8.GetString(bytes).TrimEnd('\r'));
                }
                catch (Exception e) when (e is FormatException or DecoderFallbackException or ArgumentException)
                {
                    await WriteAsync(new BridgeResponse(0, false, e.Message, null)).ConfigureAwait(false);
                    return;
                }
                await WriteAsync(await _bridge.ExecuteAsync(command).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
        finally
        {
            _server.Dispose();
            _ownerDispatcher?.InvokeAsync(() => _owned?.Dispose());
        }
    }

    async Task WriteAsync(BridgeResponse response)
    {
        var line = Utf8.GetBytes(BridgeJson.Write(response) + "\n");
        await _server.WriteAsync(line, _stop.Token).ConfigureAwait(false);
        await _server.FlushAsync(_stop.Token).ConfigureAwait(false);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.InputBridgeTests|FullyQualifiedName~RBX15.App.Tests.BridgeSecurityTests"`
Expected: `Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43` for `RBX15.App.Tests.dll` (16 + 27).

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 938`; Parity.Tests `Passed: 35`; App.Tests `Passed: 856, Failed: 0`.

- [ ] **Step 9: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App/RBX15.App.csproj src/RBX15.App.Tests/RBX15.App.Tests.csproj src/RBX15.App/Testing src/RBX15.App.Tests/InputBridgeTests.cs src/RBX15.App.Tests/BridgeSecurityTests.cs
git commit -m "test(app): flag-gated in-process input bridge over a current-user pipe, never compiled into the app by default"
```

- [ ] **Step 10: The Release-exclusion proof, the publish refusal and the launch hook**

Contract "Security": "Prove the Release build contains no bridge type … Nothing is reachable without the build flag and the command-line flag together." Create `src/RBX15.App.Tests/BridgeExclusionTests.cs`:

```csharp
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The test input bridge must be impossible in a shipped build (M4a contract "Security"; interfaces R9, §2.4). A build
/// without <c>EnableTestInputBridge=true</c> holds no bridge type and no bridge string; a flagged build holds both
/// (the positive control, so a broken check cannot pass by finding nothing); publishing with the flag is refused; and
/// no source file calls a global input API.
/// </summary>
public class BridgeExclusionTests
{
    static readonly string[] BridgeTypes = ["TestInputBridge", "InputBridgeDispatcher", "BridgeKeyboardDevice", "RawMouse", "BridgeFileDialogs", "BridgeJson"];
    static readonly string[] BridgeStrings = ["--test-input-bridge", "RBX15-BRIDGE-PIPE"];

    /// <summary>Whether the assembly defines any bridge type (by namespace or by name) and any bridge user string.</summary>
    static (bool Types, bool Strings) Scan(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        bool types = md.TypeDefinitions.Select(md.GetTypeDefinition).Any(t =>
            md.GetString(t.Namespace) == "RBX15.App.Testing" || BridgeTypes.Contains(md.GetString(t.Name)));
        bool strings = false;
        for (var h = MetadataTokens.UserStringHandle(1); !h.IsNil; h = md.GetNextHandle(h))
            if (BridgeStrings.Any(md.GetUserString(h).Contains)) strings = true;
        return (types, strings);
    }

    [Fact] // the RBX15.App.dll these tests run against was built without the flag
    public void The_app_under_test_has_no_bridge() =>
        Assert.Equal((false, false), Scan(typeof(App).Assembly.Location));

    [Fact] // a plain Release build of the app has no bridge; the same build with the flag has it (positive control)
    public void A_release_build_has_no_bridge_and_a_flagged_build_does()
    {
        string root = RepoRoot.Find(AppContext.BaseDirectory);
        string temp = Path.Combine(Path.GetTempPath(), "rbx15-exclusion-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            string plain = Build(root, Path.Combine(temp, "a"));
            string flagged = Build(root, Path.Combine(temp, "b"), "-p:EnableTestInputBridge=true");
            Assert.Equal((false, false), Scan(plain));
            Assert.Equal((true, true), Scan(flagged));
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact] // the flag never reaches a published app: publish and MSIX packaging refuse it before compiling (critic M5)
    public void Publishing_with_the_flag_is_refused()
    {
        string root = RepoRoot.Find(AppContext.BaseDirectory);
        string temp = Path.Combine(Path.GetTempPath(), "rbx15-exclusion-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (code, output) = Dotnet(root, "publish", Path.Combine(root, "src", "RBX15.App", "RBX15.App.csproj"), "-c", "Release",
                "--artifacts-path", temp, "-p:EnableTestInputBridge=true", "-nologo", "-v", "q", "--disable-build-servers");
            Assert.NotEqual(0, code);
            Assert.Contains("a published RBX15 never carries the test input bridge", output);
            // M6's Store path packages through MSBuild, which does not set _IsPublishing; the MSIX property refuses as well.
            var (msix, msixOutput) = Dotnet(root, "build", Path.Combine(root, "src", "RBX15.App", "RBX15.App.csproj"), "-c", "Release",
                "--artifacts-path", Path.Combine(temp, "msix"), "-p:EnableTestInputBridge=true", "-p:WindowsPackageType=MSIX", "-nologo", "-v", "q", "--disable-build-servers");
            Assert.NotEqual(0, msix);
            Assert.Contains("a published RBX15 never carries the test input bridge", msixOutput);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact] // no global input anywhere: the app's sources name none of the Windows APIs that send input or hook it
    public void No_source_calls_a_global_input_api()
    {
        string root = RepoRoot.Find(AppContext.BaseDirectory);
        var forbidden = new Regex(@"\b(SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx|BlockInput)\b");
        var hits = Directory.EnumerateFiles(Path.Combine(root, "src", "RBX15.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => forbidden.IsMatch(x.line))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToList();
        Assert.Empty(hits);
    }

    static string Build(string root, string artifacts, params string[] extra)
    {
        var (code, output) = Dotnet(root, ["build", Path.Combine(root, "src", "RBX15.App", "RBX15.App.csproj"), "-c", "Release",
            "--artifacts-path", artifacts, "-nologo", "-v", "q", "--disable-build-servers", .. extra]);
        Assert.True(code == 0, output);
        return Directory.EnumerateFiles(Path.Combine(artifacts, "bin", "RBX15.App"), "RBX15.App.dll", SearchOption.AllDirectories).Single();
    }

    static (int Code, string Output) Dotnet(string root, params string[] args)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet " + string.Join(' ', args) + " ran past 5 minutes");
        }
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`, then `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.BridgeExclusionTests"`
Expected: `Failed:     1, Passed:     3, Total:     4`. `Publishing_with_the_flag_is_refused` fails with `Assert.NotEqual() Failure: Values are equal` (the publish succeeded), as measured.

In `src/RBX15.App/RBX15.App.csproj`, replace:

```xml
  <ItemGroup Condition="'$(EnableTestInputBridge)' != 'true'">
    <Compile Remove="Testing\**" />
  </ItemGroup>
```

with:

```xml
  <ItemGroup Condition="'$(EnableTestInputBridge)' != 'true'">
    <Compile Remove="Testing\**" />
  </ItemGroup>
  <!-- dotnet publish sets _IsPublishing; MSIX packaging (M6's Store path) sets the Appx/WindowsPackageType properties
       instead, so those refuse too (critic M5). -->
  <Target Name="RefuseTestInputBridgeOnPublish" BeforeTargets="BeforeBuild"
          Condition="'$(EnableTestInputBridge)' == 'true' and ('$(_IsPublishing)' == 'true' or '$(GenerateAppxPackageOnBuild)' == 'true' or '$(AppxPackage)' == 'true' or '$(WindowsPackageType)' == 'MSIX')">
    <Error Text="EnableTestInputBridge is for the smoke build only; a published RBX15 never carries the test input bridge." />
  </Target>
```

Replace the whole of `src/RBX15.App/App.xaml.cs` with the file below. Outside the two `#if TEST_INPUT_BRIDGE` blocks, `OnStartup` is Task 1's body per interfaces §3.10: settings first, then `Build(session, owner, settings)`, then `new MainWindow(main, settings)`. If Task 1's committed `OnStartup` differs from those three lines, keep Task 1's lines and add only the `using` block, the `#if` block at the top of `OnStartup` and `StartWithBridge`.

```csharp
using System.IO;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;
#if TEST_INPUT_BRIDGE
using RBX15.App.Testing;
#endif

namespace RBX15.App;

/// <summary>
/// Startup. 4.1's startup state is a fresh document (editor.js:2745-2746; ledger project-startup).
/// The view-models are built in one place, <see cref="Composition.Build"/>; later tasks add their
/// services and view-models there, never here.
/// <para>
/// M4a: the test input bridge's two hook sites are the <c>#if TEST_INPUT_BRIDGE</c> blocks below. They compile only
/// with <c>-p:EnableTestInputBridge=true</c> (interfaces R9), and the bridge starts only when that build is also
/// launched with <c>--test-input-bridge</c>.
/// </para>
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
#if TEST_INPUT_BRIDGE
        if (TestInputBridge.Requested(e.Args))
        {
            StartWithBridge(session);
            return;
        }
#endif
        var settings = new JsonSettingsStore(SettingsDirectory);
        var main = Composition.Build(session, () => Current?.MainWindow, settings);
        MainWindow = new MainWindow(main, settings);
        MainWindow.Show();
    }

#if TEST_INPUT_BRIDGE
    /// <summary>
    /// The bridged launch (interfaces R10): file dialogs answer from the bridge's queue, never natively; settings go to
    /// %TEMP%\rbx15-bridge-&lt;pid&gt;, so the user's settings.json is never read or written; themed dialogs and the
    /// color picker stay real app windows. The pipe line goes to stdout once the window is shown.
    /// </summary>
    void StartWithBridge(EditorSession session)
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "rbx15-bridge-" + Environment.ProcessId);
        Directory.CreateDirectory(settingsDirectory);
        var settings = new JsonSettingsStore(settingsDirectory);
        var files = new BridgeFileDialogs(Console.Out);
        Func<Window?> owner = () => Current?.MainWindow;
        var main = Composition.Build(session, files, new ThemedDialogService(owner), new ThemedColorPicker(owner), settings);
        var window = new MainWindow(main, settings);
        MainWindow = window;
        window.Show();
        var bridge = TestInputBridge.Start(window, Console.Out, files);
        Exit += (_, _) =>
        {
            bridge.Dispose();
            // The window saved its placement on Closing, before Exit; nothing reads this folder again (critic L12).
            try { Directory.Delete(settingsDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        };
    }
#endif
}
```

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet build src/RBX15.App/RBX15.App.csproj -c Release -p:EnableTestInputBridge=true -warnaserror --artifacts-path %TEMP%\rbx15-bridge-build` (Git Bash: `"$TEMP/rbx15-bridge-build"`)
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`, and `%TEMP%\rbx15-bridge-build\bin\RBX15.App\release\RBX15.App.exe` exists. This is interfaces R9's smoke build; Task 5's `brg build` runs the same command. Do not launch it: launching is the controller's `brg probe`.

Run: `dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~RBX15.App.Tests.BridgeExclusionTests"`
Expected: `Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4` for `RBX15.App.Tests.dll`, in about 20 s (two App builds and a refused publish).

Run: `dotnet test src/RBX15.slnx -c Release --no-build`
Expected: Engine.Tests `Passed: 938, Failed: 0`; Parity.Tests `Passed: 35`; App.Tests `Passed: 860, Failed: 0`.

- [ ] **Step 11: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.App.Tests/BridgeExclusionTests.cs src/RBX15.App/RBX15.App.csproj src/RBX15.App/App.xaml.cs
git commit -m "test(app): prove release builds carry no input bridge, refuse it on publish, and hook it behind both flags"
```

- [ ] **Step 12: Task 4 checks**

Run: `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx|BlockInput" src --include=*.cs`
Expected: one line, in `src/RBX15.App.Tests/BridgeExclusionTests.cs` (the test's own regex). No other file.

Run: `grep -rn "SendMessage" src/RBX15.App --include=*.cs`
Expected: two lines, both in `src/RBX15.App/Testing/InputBridgeDispatcher.cs`: the WM_SETCURSOR call in `ClickDisabledWindow` and its `DllImport`.

Run: `grep -rn "TEST_INPUT_BRIDGE" src`
Expected: lines in `src/RBX15.App/App.xaml.cs` (the `#if` sites and the doc) and `src/RBX15.App/RBX15.App.csproj`, and no other file.

Run: `git diff --stat 3f3c5c5 -- editor.js editor.html styles.css main.js lib packs tools/parity-ref`
Expected: no output. 4.1 is untouched.

Run: `git status --short`
Expected: no output after Step 11.

**Handoff to Task 5** (facts the smoke driver and the ledger close need; nothing here is edited by Task 4):

1. **Launch protocol** (`brg probe`):
   - Build with Step 10's command into `%TEMP%\rbx15-bridge-build`.
   - Start `%TEMP%\rbx15-bridge-build\bin\RBX15.App\release\RBX15.App.exe --test-input-bridge` with stdout redirected.
   - Read lines until one starts with `RBX15-BRIDGE-PIPE `, then connect `new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly)`.
   - Send one JSON object per line and read one response line each. Pipelining is safe (T4-R2), and the ids are the driver's.
   - stdout also carries `RBX15-BRIDGE-DIALOG <method> <arg> -> <answer>` for every file dialog. Settings go to `%TEMP%\rbx15-bridge-<pid>\settings.json`.
2. **Driving rules measured here:**
   - **Buttons:** WPF buttons, toggle buttons and repeat buttons are pressed by UIA Invoke or Toggle, never by `pointer` (T4-R4).
   - **Surfaces the bridge drives:** the canvas, the layer rows, the picker's `SvSquare` and `HueStrip`, slider thumbs and the asset tiles (Task 2's capture drag).
   - **Modals:** a command that opens a modal answers `{"modal":true}`; continue inside it. A `pointer down` on a window the modal disabled answers `disabledOwner: true` and commits the picker (H10).
   - **Text:** the bridge raises no TextInput, so typing into a field is UIA `ValuePattern.SetValue`.
   - **Key bindings:** chords run the window's KeyBindings with exact modifiers. Class-level command gestures, such as TextBox's own Ctrl+Z, are not reachable (R7).
   - **Idle:** `wait-idle` after every wheel and drag before `inspect`.
3. **Rows and the evidence each takes from the bridge** (Task 5 rules each under the honesty rule):
   - `select-move-hit-and-drag`, `select-move` and `canvas-select-gestures`: a down/move/up sequence on the canvas, then `inspect` for x and y and one undo step. `select-hover-and-end`: moves with no button, then `inspect.cursor`.
   - `canvas-mouse-button-agnostic`: right and middle presses give `gesture` None (this task's theory).
   - `draw-release-outside-canvas`: a release at `{"window":{…}}` outside the client.
   - `wheel-scale-layer`, `wheel-sign-only-and-midstroke` and `wheel-scale-undo-burst`: `wheel` deltas ±120 and a fractional 40, then `wait-idle`.
   - `eraser-click-without-move`: down and up at one point.
   - `props-live-refresh`: a drag while UIA reads the X field's Value.
   - `layers-reorder-drag`, `layers-reorder-drag-2` and `layer-reorder-dragging-class-timer`: a row drag through window points, and `key` Up with `["Alt"]`.
   - `fillstyle-color-pickers`: Invoke Color 1, drag `SvSquare` and `HueStrip`, click the canvas through the disabled owner.
   - The seven cursor rows (interfaces §2.7): `inspect.cursor` after a routed hover is the cursor the canvas sets, not the OS image.
   - `shell-external-drop-guard`: stays manual.
4. **Divergence rows for Task 5 to add or amend:**
   - The bridge itself: test-only, never shipped, with the `BridgeExclusionTests` evidence.
   - `a11y-focus-rings`: layers are UIA elements (ListItem, SelectionItem, Transform in template units). The row stays open for M6.
   - The Transform pattern's units are template pixels, not screen pixels (the contract's choice).
5. **Manual lines** for `docs/port/m4a-manual.md`:
   - **N1:** with Narrator on, Tab to nothing on the canvas. Use Caps+arrow scan mode to reach "Template canvas", then its layer items by name, and hear the selected layer announced on selection.
   - **N2:** in Accessibility Insights, select a layer item and run Transform Move(10, 10): the layer moves to (10, 10), and one Ctrl+Z restores it.

**Interface additions requested by Task 4** (each is `internal` unless marked, and is used only by Task 4 and its tests):

1. **§3.8 Pipe, change** (T4-R1, T4-R2): the server's options are `PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance`, not `CurrentUserOnly`, because .NET 10 throws on CurrentUserOnly plus a PipeSecurity (measured). The in and out buffers are 128 KiB, not 0, because 0 deadlocks a pipelining client (measured). Clients pass `PipeOptions.CurrentUserOnly`.
2. **§3.8, new members:**
   - Public: `BridgeException` and `InputBridgeDispatcher.AppExceptionPrefix`.
   - Internal on `InputBridgeDispatcher`: the constructor `(MainWindow, BridgeFileDialogs?)` and `PhysicalModifiers`, a `Func<ModifierKeys>` test seam. Also `BusyReason()` and `static Neutralize(string, bool withMove = true)`, both tested, and `FindElement(string)`.
   - `RawMouse`: `Inject(…, nint extraInformation = InputBridgeDispatcher.Marker)`, `Create(…)`, `TryRead(…)` and `PreviewInputReportEvent`.
   - `BridgeFileDialogs`: the constructor `(TextWriter log)` and `LogPrefix`.
   - Internal on `TestInputBridge`: `Start(MainWindow, TextWriter, BridgeFileDialogs?)`, which App.xaml.cs uses to hand over the queue the composition got, `Listen(IInputBridge)`, `Served`, `LiveSecurity()`, `MaxLineBytes` and `NewPipeName()`.
3. **§3.7, new members:**
   - `CanvasViewModel.SelectedLayerId` raises PropertyChanged on every selection change. The host listens through `INotifyPropertyChanged`; the interface is unchanged.
   - `TemplateCanvasHost.ScreenBounds(IEnumerable<Point>)`.
   - On `TemplateCanvasHostAutomationPeer`: `Host`, `LayerPeer(int)`, `Provider()`, `OnLayersChanged()` and `OnSelectionChanged()`.
   - `LayerAutomationPeer.LayerId` (public) and its internal constructor `(TemplateCanvasHostAutomationPeer, int)`.
4. **§3.8 Release exclusion, two more facts:** `BridgeExclusionTests.Publishing_with_the_flag_is_refused` (the new `RefuseTestInputBridgeOnPublish` target, T4-R7) and `No_source_calls_a_global_input_api`. Also, the scan rejects the bridge's type names in any namespace, not only `RBX15.App.Testing`.
5. **§4 test list, extra classes and facts:**
   - `CanvasLayerAutomationTests` (11; not in §4's table).
   - `BridgeSecurityTests` grows to 25: the live-pipe test, bad lines, strict parsing, the forms, the dialog queue and the neutralizer.
   - `InputBridgeTests` grows to 14: right and middle presses, the hardware neutralizer, the left-button seam, a row drag and the modal.
6. **For Task 1:**
   - `KeyBinding.Command` stays a settable DP on the bindings `AddShortcut` creates. `InputBridgeTests` swaps a counting command in to count Ctrl+S.
   - `MainWindow.FocusedElement` stays the seam the guard reads (T4-R8).
   - `EditorSession.MarkSaved` is used by the bridge rig before `Close`.
7. **For Task 5:** the handoff above. Record the T4-R4 button limit, the text-entry limit and T4-R17's limits in the `test-input-bridge` quirks (Task 5 does), and drive H10's Color 1 by UIA Invoke (§3.9 already says so).
8. **Reconciler (critic C2, C3, M5, M6, M7, L12):** `inspect` `canvas` and `layers` shapes and the disabled-owner `ownerClick`/`handled` (T4-R15); `BridgeJson.Parse`'s duplicate-key refusal (T4-R14); the MSIX conditions on `RefuseTestInputBridgeOnPublish` (T4-R7); `BridgeCollection` (`[CollectionDefinition("Bridge", DisableParallelization = true)]`) and the two collection attributes (T4-R16); the temp settings folder deleted on exit; `AssetLibraryPanel.LeftDown` through `PointerState`; `InputBridgeTests` +2 and `BridgeSecurityTests` +2.

---

### Task 5: Close-out: the bridge smoke, the 4.1 cross-check, the manual list, the ledger and final verification

**Files:**

- Create (committed):
  - `docs/port/m4a-manual.md`: the three checks that stay manual (Explorer drags, a second monitor, high DPI).
- Modify (committed):
  - `src/RBX15.App.Tests/LedgerScenarioTests.cs`: nine `using` lines, seven M4a scenario facts, the `Shell4a` and `Await` helpers and three nested recording fakes (`ProjectDialogs`, `PromptDialogs`, `MemorySettings`). App.Tests +7.
  - `docs/port/parity-ledger.json`: amend `asset-open-folder` to `fix`; append one sentence to ten divergence texts and correct `project-load-layers`' selection clause; add three rows; narrow the parityCheck of the rows being closed; close up to 67 rows (Step 12).
  - `docs/port/parity-ledger.md`, `docs/port/divergences.md` (generated by `tools/ledger/ledger.py`).
- Scratch only (git-ignored under `.superpowers/`):
  - `.superpowers/m4a-smoke/bridge/bridge.csproj` and `Program.cs`: the smoke driver `brg` (interfaces 3.9).
  - `.superpowers/m4a-smoke/out/`: `smoke-results.md`, `smoke-items.txt`, `files-results.md`, `files-items.txt`, the run logs, PNG captures and the saved projects the run writes.
  - `.superpowers/m4a-smoke/p41-scene.json` and `.superpowers/m4a-smoke/p41/`: the 4.1 cross-check.
  - `.superpowers/logs/m4a-evidence.py`, `m4a-ledger.py`, `m4a-manual.txt`, `m4a-p41.txt`, `m4a-proven-ids.txt` and the test logs.

**Interfaces:**

- Consumes (evidence and scripted use; Task 5 changes no production code):
  - Task 1: `EditorSession` (`IsDirty`, `CurrentFile`, `TemplateTitle`, `Undo`, `Redo`, `CanUndo`, `UndoCount`, `NextId`, `SelectedLayerId`); `SaveChoice`; `IDialogService.AskSaveChanges`; `IFileDialogs.{OpenProject, SaveProject, PickFolder}`; `SettingsFolder`, `ISettingsStore.{LoadFolder, SaveFolder}`; `ProjectFileService.{OpenAsync(string), ConfirmDiscardChanges}`; `FileGroupViewModel.SaveCommand`; `MainViewModel.{Title, Files, Session}`; `Composition.Build(EditorSession, IFileDialogs, IDialogService, IColorPicker, ISettingsStore)`; the ThemedDialog element names `MessageText`, `OkButton`, `DontSaveButton`, `CancelButton`; the R3 title; the status texts `Saved <file>.` and `Opened <file>. Couldn't load: <names>.`; test classes `ProjectFileServiceTests`, `WindowTitleTests`, `EditorSessionTests`, `FileGroupTests`, `ShellTests`, `ImagesCapsTests`.
  - Task 2: `AssetLibraryViewModel.{LoadDripsCommand, LoadWordArtCommand, OpenFolderCommand, ShowGroupCommand, AddCommand, AddFullCommand, DropOnCanvas, GroupButtons, Rows, CountText}`, `AssetRowViewModel.Tiles`, `AssetTileViewModel.{Entry, Caption, ToolTip}`, `AssetGroup.{Key, Label}`, `AssetEntry.{FileName, FullPath}`, `PackSource.{DefaultDirectory, Drips, WordArt, Files}`, `MainViewModel.Assets`; the panel's `AssetGrid`, `AssetTile` and the handle (Additions 6); packs in the build output; test classes `AssetScanTests` (Engine), `AssetLibraryViewModelTests`, `AssetLibraryPanelTests`, `PackSourceTests`, `ImportServiceTests`, `LayerOpsTests` (Engine).
  - Task 3: `DialogInventoryTests`; the status routing of failed imports.
  - Task 4: the bridge (interfaces 3.8: `EnableTestInputBridge`, `--test-input-bridge`, `RBX15-BRIDGE-PIPE <name>`, the commands, `BridgeFileDialogs`), `LayerAutomationPeer` through UI Automation, and Additions 1-5; test classes `BridgeExclusionTests`, `BridgeSecurityTests`, `InputBridgeTests`, `CanvasLayerAutomationTests`.
  - M2-M3b: `ProjectIO.{Write, Read}`, `Renderer.ExportPng`, `LayerOps.MoveTo`, `DocumentOps.PaintRegion`, `Fill.Solid`, `CanvasCoordinates.ToTemplate`, `CanvasDrop`, `CanvasViewModel.{DropHandler, Document}`, `ToolsViewModel.Set`, `StatusViewModel.Message`, `RepoRoot.Find` (RBX15.Parity), `TestFiles.Bmp2x1`, `StaThread.Run`; `LedgerScenarioTests`' helpers `Solid`, `Png`, `AddLayer`, `WritePng`, `WriteText`, the nested `Picker` and the colors `Red`, `Blue`, `Slate`; the UI Automation names of M3a-M3b (`Layers, top layer first`, `Template canvas`, `Scale percent`, `Rotation degrees`, `Brush size`, `Fill shapes`, `Template name`, `Torso Front`, `Color 1`, `Duplicate layer`, `Hide <name>`, the tool labels, `UndoButton`, `MessageText`, `SvSquare`, `HueStrip`, `Color picker`); M3b test classes for the 23 rows.
  - Tooling: `tools/parity-ref/app-render.js` (the `project` op, Electron 41.1.0), `python tools/ledger/ledger.py done | check`.
- Produces:
  - `LedgerScenarioTests` +7 facts; `docs/port/m4a-manual.md`; the smoke driver `brg` with `build`, `probe`, `smoke`, `files` and `compare`.
  - The ledger: 420 rows (417 + 3 new), `188 + C` done, where C is the number of ids Step 12 closes (61 when every automated gate holds and the manual items are not run; 67 when MAN-1 and MAN-3 also pass), `203` fix and `2` new.
  - The controller-run evidence under `.superpowers/`. Push, PR and dashboard steps belong to the controller.

**Rulings made in this task:**

1. **The 23 held M3b rows, ruled per row** (contract "Its results close the 23 held ledger rows under the honesty rule"):
   - 15 bridge-provable rows close on the bridge items named in their new parityCheck, each marked "Narrowed in M4a": the input is real WPF routed input injected in-process (R7, R8), not a physical device. `select-move-hit-and-drag`, `select-move`, `select-hover-and-end`, `canvas-select-gestures` (H1-H3), `canvas-mouse-button-agnostic`, `draw-release-outside-canvas` (H4), `wheel-scale-layer`, `wheel-sign-only-and-midstroke`, `wheel-scale-undo-burst` (H3, H4), `eraser-click-without-move` (H5), `props-live-refresh` (H2), `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer` (H7, H7b), `fillstyle-color-pickers` (H10).
   - The precision touchpad of `wheel-sign-only-and-midstroke` is reworded to injected wheel reports of a touchpad's size (five events of delta 8): the property under test is "by sign, not distance", which the injected deltas prove; the physical touchpad is not claimed.
   - Alt+Tab mid-drag (`layers-reorder-drag-2`, H7b) is not automatable (contract). Its cancel path is a lost capture, pinned by `LayersPanelDragTests`; the narrowed text says so instead of claiming it.
   - The seven cursor rows (`tools-cursor`, `cursor-eraser`, `eraser-cursor`, `cursor-default`, `canvas-cursor-states`, `select-hover-idle`, `render-restore-and-cursor`; sheet §2.7) are all **reworded and closed**: each check now reads "the cursor the canvas sets", which the bridge's `inspect` returns after routed hover and drag input, with the WPF cursor's name and the ring radius (Additions 1); `CanvasCursorsTests` already pin the ring bitmap. The OS cursor image is never observable, and no row's check is left claiming it.
   - `shell-external-drop-guard` stays manual (sheet §2.6): it closes only on MAN-1.
   - H6's two Ctrl+Z halves, which M3b could not run, now run through the bridge as part of H7 and H10 ("one Ctrl+Z restores"); the typing-pending Ctrl+Z is a TextBox's own binding and stays out of reach (R7 limit), and no open row needs it.
2. **The M4a rows close only on Tasks 1-3's test classes, the seven scenarios and the bridge items**, gated in Step 11. Narrowed or reworded where the check names a tool v5 does not use (Process Monitor, a same-scenario Electron JSON diff, a 50-image fixture), each marked "Narrowed in M4a" or "Reworded in M4a". Left open on purpose, with the milestone that closes them:
   - `shell-no-unsaved-changes-guard`: its divergence promises 60 s autosave and recovery (M5). Step 12 appends the M4a prompt to it; `shell-unsaved-work-loss` (new) closes on F1-F2 instead.
   - `shell-no-persistence` (M5: autosave, recents), `shell-window-frame` and `shell-window-size` (launch size, resize, MAN-2 defect checks; M6), `undo-buttons-shortcuts` (Ctrl+Y and Ctrl+Shift+Z, tooltips; M4b), `packs-resolve-dir` (the MSIX run; M6), `asset-grid-thumbnails` (a look comparison with 4.1; the M4b look sign-off), `asset-objecturl-lifecycle` (a measured 500-image memory and scroll run; M6), `io-image-formats` (the Background picker; M4b).
   - `panel-asset-library`, `asset-drag-stale-reference` and `drop-canvas` close only with MAN-1 (their checks or divergences include an OS file drop after a cancelled asset drag).
3. **`asset-open-folder` is amended from keep to fix.** Its check says "only 5 extensions listed", and v5 lists the six import formats (R14). A keep row carries no divergence, so it gets one, as M3b amended `eraser-cursor`.
4. **Three new rows:** `test-input-bridge` (`new`, owner Parity: the bridge and its Release-exclusion proof), `canvas-layer-automation` (`fix`: the contract's canvas UIA, an accessibility feature with a 4.1 counterpart that exposed nothing, editor.html:330), `window-title-dirty-marker` (`fix`: R3 changes the title after launch; `shell-window-title` stays closed because the launch title is unchanged).
5. **Ten divergence appends** record the M4a rulings where they change behavior: the prompt moved forward from M5 (`shell-no-unsaved-changes-guard`), R12 raw values (`project-load-layers`), R14 BMP and the .bmp caption (`io-image-formats`), sheet §2.2 and R4 (`project-load-flow`), R6 and the Hand and No cursors (`asset-drag-to-canvas`), R5 (`asset-grid-thumbnails`), the owner-click hook (`fillstyle-color-pickers`), the synchronous listing (`pack-load-sync-and-mime`), the eraser message and the one new modal (`dialogs-inventory`) and keyboard-operable thumbnails (`panel-asset-library`) (critic M10, L9). `project-load-layers`' "selection always set" stays as written: Este ruled that Open selects the top layer (T1-R2).
6. **4.1 reads a v5 file (the "P41" cross-check).** Three rows ask that 4.1 opens what v5 saves (`project-save-format`, `project-format-r15proj`, `home-file-group`). The controller loads the project the smoke saved (`smoke2.r15proj`) through 4.1's real Open path with `tools/parity-ref/app-render.js` (its `project` op, which fails on any 4.1 alert), and `brg compare` holds 4.1's export to v5's export of the same document under the golden rule. `brg compare` is a port of `RBX15.Parity.ImageDiff.Compare` (measured equal below), so no test project or Python image code is added.
7. **Manual items are numbered MAN-1 to MAN-3**, so they never read as milestones or as M3b's H items. MAN-2 (second monitor, the navy border) is a defect check only: `shell-window-size` and `shell-window-frame` stay open whatever it shows, as M3b ruled for H10.
8. **The driver is scratch and talks only through the bridge.** It is built from the m3b-look `hand` driver's safety rules with the posted keys removed: keys and pointer go through the pipe, everything else is UI Automation patterns on PID-checked elements, and PrintWindow of the app's own windows. It refuses any display scale but 100%. It needs no settings backup, because the bridge build keeps its settings in `%TEMP%\rbx15-bridge-<pid>` (R10). Its full rules head `Program.cs`.
9. **A driver error is a failed item.** An item passes only when every check in it passes and the driver did not stop early; a failed item keeps its rows open and goes to the owning task as a defect (Step 8's table).

**The state Task 5 starts from.** Tasks 1-4 are committed and every suite is green. Tasks 1-4 close no ledger rows: `docs/port` is unchanged from 3f3c5c5 (188 done). If any of that is not true, stop and report to the controller.

**Test counts.** Cumulative from Task 4's end (Engine.Tests 938, App.Tests 860). Task 5 adds Engine.Tests +0, Parity.Tests +0, App.Tests +7.

| After | Engine.Tests | Parity.Tests | App.Tests |
| --- | --- | --- | --- |
| `3f3c5c5` (main) | 880 | 35 | 644 |
| Task 4 (Step 1 baseline) | 938 | 35 | 860 |
| Task 5 Step 3 and Step 13 | 938 | 35 | 867 |

Every run has 0 failed and 0 skipped. The `node --test "tools/parity-ref/*.test.js"` count stays 14 and `npm test` 31 (M3b's measured counts; no M4a task adds a `.test.js`).

**Measured for this task (2026-10-16, on scratch copies under the session scratchpad; the repo was never built into or written outside `.superpowers/m4-planning`):**

- The driver (`bridge.csproj` and `Program.cs` below) builds with `dotnet build -c Release` on .NET SDK 10.0.302: `0 Warning(s)`, `0 Error(s)` (TreatWarningsAsErrors on). Run with no arguments it prints the usage line and exits 2. `brg compare` was run on two frozen app goldens: identical files print `pass 585x559 max 0 differing 0 interior 0 edgeOver 0`; `detail-arm-bands-stitch.png` against `detail-arm-bands.png` prints `FAIL 585x559 max 26 differing 768 interior 24 edgeOver 688`, exactly the `DiffResult` that `RBX15.Parity.ImageDiff.Compare` returns for the same pair (a scratch console over the copy's RBX15.Parity). A grep of `Program.cs` for `SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx|PostMessage|SendMessage|SetForegroundWindow` finds only the safety header's comment lines. The driver was **not** run against the app: `probe`, `smoke` and `files` launch it.
- `dotnet build src/RBX15.App/RBX15.App.csproj -c Release -p:EnableTestInputBridge=true --artifacts-path <scratch>` on a `git archive` copy of 3f3c5c5 (where the flag is still inert) puts the exe at `<artifacts>/bin/RBX15.App/release/RBX15.App.exe`, the path `brg` uses under `%TEMP%\rbx15-bridge-build`.
- The seven scenarios compile under `dotnet build src/RBX15.slnx -c Release -warnaserror` (xUnit analyzers included) on that copy with compile-only stand-ins of the sheet's Task 1 and Task 2 members (bodies throw; Composition and TestShell given the five-argument `Build`): `0 Warning(s)`, `0 Error(s)`. `--list-tests` counts 651 App.Tests cases (644 + 7) and 33 `LedgerScenarioTests` cases (26 + 7) on that copy; on the real branch, with Task 1's scenario, the class has 34 (critic M4). On the stand-ins the 26 existing facts pass and the 7 new ones fail with `NotImplementedException`; their pass is Step 3's check against the real Tasks 1-4.
- The engine half of three scenarios ran green on the copy in a scratch fact (not committed): `ProjectIO.Read` keeps rotation 720, scale 9, opacity 2.5, brightness 500, hueRotate -90 and x 5000 raw with nextId 5; a written three-layer project with an http `imgSrc` on B and none on C loads A only, drops `B, C` and gives nextId 2; a two-layer project without images drops `P, Q`; both bad files throw `ProjectFormatException`.
- `m4a-ledger.py` (below) ran on a fresh git repository holding 3f3c5c5's `docs/port` and `tools/ledger`:
  - with all 67 ids: `amended 1, appended 10, added 3, narrowed 44; 420 rows`, then `420 rows, 0 problem(s)`, `420 rows; 255 done; 203 fix; 2 new`, `## Fixed in v5 (203)`, `## Not carried into v5 (26)`, `## New in v5 (2)` at lines 5, 213 and 244, and numstat `179 120` JSON, `73 65` ledger md, `16 12` divergences md (re-measured by the reconciler with critic M9, M10 and L9's edits; the drafter's script gave `appended 7`, `narrowed 43`, `175 116` and `13 9`);
  - with the 61 ids that need no manual item: `narrowed 39`, `420 rows; 249 done`, numstat `168 109`, `67 59`, `16 12`;
  - `python -m unittest discover -s tools/ledger -p "test_*.py"`: 8 tests, OK.
- `m4a-evidence.py` (below) ran on a synthetic normal-verbosity log (one passing fact per cited class and every scenario name), all bridge items `pass`, P41 `pass` and the three manual items `not run`: `67 rows, 61 proven; 15 of 15 scenarios passed; ...`, with the six manual-gated rows OPEN and `m4a-proven-ids.txt` holding 61 ids in ledger order.
- Every 4.1 citation in this task was read at 3f3c5c5. Three differ from the sheet: the asset grid's `repeat(3,1fr)` is editor.html:305 (sheet R5: 304), the resize clamp `window.innerHeight * 0.7` is editor.js:2732 (sheet: 2731), and the canvas element is editor.html:330 (M3b cited the comment line 327). See Additions 12.
- **Reconciler's runs** (after the critic's fixes): the seven scenarios applied as Step 2 prints them onto a scratch chain of the plan's Tasks 1-4 built with 0 warnings and passed 34 of 34 `LedgerScenarioTests` twice; the full run gave Engine 938, App 867, Parity 35. `m4a-ledger.py` as printed below ran on fresh copies of `3f3c5c5`'s `docs/port` and `tools/ledger` with the 61 and the 67 ids and printed the Step 12 Expected lines.
- Not measured: anything that launches the app (`brg probe`, `smoke`, `files`) and the P41 Electron run (it needs a project the smoke saves).

**The rule for closing.** As in M3a and M3b: a row closes only when the evidence on this branch proves its whole parityCheck, or, for a `fix` row, also its whole v5 divergence sentence. Evidence means a unit test, a golden, a `LedgerScenarioTests` fact, a passed bridge item, P41, or a manual item Este passed. Ruling 2 lists what stays open.

- [ ] **Step 1: Record the baseline Tasks 1-4 left**

Run from the repo root:

```bash
mkdir -p .superpowers/logs
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test src/RBX15.slnx -c Release --no-build > .superpowers/logs/m4a-t5-baseline.log 2>&1
grep -E "(Passed|Failed)!" .superpowers/logs/m4a-t5-baseline.log
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
git diff --stat "$BASE" -- docs/port
grep -c "^    \[Fact\]" src/RBX15.App.Tests/LedgerScenarioTests.cs
grep -n "EnableTestInputBridge" src/RBX15.App/RBX15.App.csproj
ls src/RBX15.App/Testing
ls src/RBX15.App/bin/Release/net10.0-windows/packs/drips | wc -l
```

Expected:

- `base 3f3c5c5`.
- `0 Warning(s)` and `0 Error(s)`.
- Three summary lines, each `Failed: 0` and `Skipped: 0`: `RBX15.Engine.Tests.dll` `Passed: 938`, `RBX15.Parity.Tests.dll` `Passed: 35`, `RBX15.App.Tests.dll` `Passed: 860`.
- `417 rows; 188 done; 200 fix`.
- The `docs/port` diff prints nothing.
- `27`: the M3a and M3b scenarios and Task 1's M4a scenario (`Save_open_and_the_unsaved_prompt_through_the_real_composition`).
- The flag's lines from Task 4: the comment, the property group's condition, the compile exclusion's condition, the publish refusal's condition and its error text (critic L4).
- Task 4's bridge sources (`TestInputBridge.cs`, `InputBridgeDispatcher.cs` and the rest).
- `156`: Task 2 copies the packs into the build output.

If a summary shows `Failed` or `Skipped` above 0, or the ledger is not at 188 done, stop and report it to the controller: Tasks 1-4 are not finished.

- [ ] **Step 2: Write the M4a ledger scenarios**

Three edits to `src/RBX15.App.Tests/LedgerScenarioTests.cs`, each at a named anchor; the file keeps its own line endings. If an anchor is missing, stop and report it.

Edit 1, the usings. Replace

```csharp
using System.IO;
using System.Linq;
```

with

```csharp
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Threading;
```

and replace `using RBX15.Engine;` with

```csharp
using RBX15.Engine;
using RBX15.Parity;
```

Edit 2, the scenarios. Insert this block immediately before the line `    static (EditorSession Session, MainViewModel Main) Shell()` (after the M3b scenarios):

```csharp
    // ─── M4a close-out scenarios (Task 5). Each builds the real composition (Composition.Build) with recording fakes
    // for the native file dialogs, the themed dialogs and the settings file, and drives the File group, the project
    // service and the asset library through their public surface, the way the window and the panels do.

    [Fact]
    public void Save_as_writes_the_project_marks_it_clean_and_reopening_restores_it_with_history_cleared() => StaThread.Run(() =>
    {
        // project-save-format, project-save-filename, file-project-format, project-load-history-interplay,
        // project-template-name-not-saved, home-file-group, window-title-dirty-marker
        // (editor.js:2550-2591 saveProject, :2594-2690 loadProject; interfaces R1, R2, R3)
        using var art = Solid(40, 30, Slate);
        var (s, main, files, dialogs, settings) = Shell4a();
        string path = Path.Combine(_dir, "My_Shirt_v2_.r15proj");
        s.TemplateTitle = "My Shirt v2!";
        Assert.True(s.Apply(d => DocumentOps.PaintRegion(d, "torso_front", Fill.Solid("#ff0000"))));
        int id = AddLayer(s, art, 200, 100, "art");
        Assert.True(s.IsDirty);
        Assert.Equal("*Untitled - RBX15 Classic Shirt and Pants Maker", main.Title);

        files.Saves.Enqueue(path);
        main.Home.File.SaveCommand.Execute(null);                          // Save work with no current file is Save As
        Assert.Equal(new[] { "My_Shirt_v2_.r15proj" }, files.SuggestedProjects);   // ExportNaming.ProjectFileName
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));                   // temp file plus move: nothing left over
        using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("5.0", json.RootElement.GetProperty("version").GetString());
            Assert.False(json.RootElement.TryGetProperty("title", out _));   // R1: the template name is not saved
        }
        Assert.False(s.IsDirty);
        Assert.Equal(path, s.CurrentFile);
        Assert.Equal("My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        Assert.Equal("Saved My_Shirt_v2_.r15proj.", main.Status.Message);
        Assert.Equal(_dir, settings.Folders[SettingsFolder.Project]);     // the last folder is remembered
        byte[] saved = Renderer.ExportPng(s.Document);

        Assert.True(s.Apply(d => LayerOps.MoveTo(d, id, 10, 10)));
        Assert.True(s.IsDirty);
        Assert.Equal("*My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);
        Assert.True(s.Undo());                                             // R2: undoing back to the save reads clean
        Assert.False(s.IsDirty);
        Assert.Equal("My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);

        Assert.True(s.Apply(d => LayerOps.MoveTo(d, id, 10, 10)));
        dialogs.Choices.Enqueue(SaveChoice.DontSave);
        Assert.True(Await(main.Files.OpenAsync(path)));
        Assert.Equal(new[] { "My_Shirt_v2_.r15proj" }, dialogs.SaveChanges);   // the guard named the file
        Assert.Equal(saved, Renderer.ExportPng(s.Document));               // the reopened file exports as it was saved
        Assert.Equal(0, s.UndoCount);                                      // spec §3: history cleared on open
        Assert.False(s.CanUndo);
        Assert.Equal(s.Document.Layers[^1].Id, s.SelectedLayerId);         // T1-R2: top layer, editor.js:2664
        Assert.Equal(s.Document.Layers.Max(l => l.Id) + 1, s.NextId);
        Assert.Equal("My Shirt v2!", s.TemplateTitle);                     // R1: Open leaves the name box alone
        Assert.False(s.IsDirty);
        Assert.Equal(path, s.CurrentFile);
        Assert.Equal("My_Shirt_v2_.r15proj - RBX15 Classic Shirt and Pants Maker", main.Title);

        var (_, untitled, blank, _, _) = Shell4a();
        untitled.Home.File.SaveCommand.Execute(null);                      // the Save dialog is cancelled: nothing written
        Assert.Equal(new[] { "untitled.r15proj" }, blank.SuggestedProjects);
        Assert.Null(untitled.Session.CurrentFile);
    });

    [Fact]
    public void A_file_that_cannot_open_alerts_and_changes_nothing_and_layers_that_cannot_load_are_named() => StaThread.Run(() =>
    {
        // io-alert-inventory, project-load-flow, project-load-error-path-bugs, project-load-untrusted-src,
        // project-load-no-imgsrc-hang (editor.js:2632, :2663-2680; interfaces R4)
        using var art = Solid(30, 30, Slate);
        var (s, main, _, dialogs, _) = Shell4a();
        AddLayer(s, art, 50, 60, "kept");
        var before = s.Document;
        foreach (var text in new[] { "not json", "{\"version\": \"4.0\", \"layers\": [{\"id\": 1," })
        {
            string bad = WriteText($"bad{dialogs.Alerts.Count}.r15proj", text);
            Assert.False(Await(main.Files.OpenAsync(bad)));
            Assert.StartsWith("Failed to load project. ", dialogs.Alerts[^1]);   // editor.js:2676
            Assert.Same(before, s.Document);
            Assert.Equal(1, s.UndoCount);
            Assert.Null(s.CurrentFile);
        }
        Assert.Empty(dialogs.SaveChanges);                                  // R4: a file that cannot open never asks to save first

        using var a = Solid(20, 10, Red);
        using var b = Solid(20, 10, Blue);
        using var c = Solid(20, 10, Slate);
        var three = new Document
        {
            Layers = ImmutableList.Create(
                new Layer { Id = 1, Image = a, Name = "A", X = 10, Y = 10 },
                new Layer { Id = 2, Image = b, Name = "B", X = 20, Y = 20 },
                new Layer { Id = 3, Image = c, Name = "C", X = 30, Y = 30 }),
        };
        var node = JsonNode.Parse(ProjectIO.Write(three, DateTimeOffset.UnixEpoch))!;
        node["layers"]![1]!["imgSrc"] = "http://example.com/b.png";        // refused: not a data: URL
        node["layers"]![2]!.AsObject().Remove("imgSrc");                     // 4.1 skipped it silently (editor.js:2632)
        string mixed = WriteText("mixed.r15proj", node.ToJsonString());
        dialogs.Choices.Enqueue(SaveChoice.DontSave);
        Assert.True(Await(main.Files.OpenAsync(mixed)));
        Assert.Equal(new[] { "A" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal("Opened mixed.r15proj. Couldn't load: B, C.", main.Status.Message);
        Assert.Equal(2, s.NextId);
        Assert.Equal(0, s.UndoCount);

        var none = new JsonObject { ["layers"] = new JsonArray(new JsonObject { ["id"] = 1, ["name"] = "P" }, new JsonObject { ["id"] = 2, ["name"] = "Q" }) };
        string empty = WriteText("none.r15proj", none.ToJsonString());
        Assert.True(Await(main.Files.OpenAsync(empty)));                    // clean since the last open: no prompt
        Assert.Empty(s.Document.Layers);
        Assert.Same(s.Document, main.Canvas.Document);                      // the canvas took the opened document
        Assert.Equal("Opened none.r15proj. Couldn't load: P, Q.", main.Status.Message);
        Assert.False(s.Undo());                                              // Undo straight after Open does nothing
        Assert.Single(dialogs.SaveChanges);
    });

    [Fact]
    public void The_unsaved_changes_guard_asks_only_when_dirty_and_cancel_or_a_cancelled_save_keeps_everything() => StaThread.Run(() =>
    {
        // shell-unsaved-work-loss, shell-no-unsaved-changes-guard (interfaces R2, R13; Este 2026-10-16)
        using var art = Solid(30, 30, Slate);
        var (s, main, files, dialogs, _) = Shell4a();
        Assert.True(main.Files.ConfirmDiscardChanges());                    // nothing to lose: no prompt
        Assert.Empty(dialogs.SaveChanges);

        AddLayer(s, art, 50, 60, "art");
        dialogs.Choices.Enqueue(SaveChoice.Cancel);
        Assert.False(main.Files.ConfirmDiscardChanges());
        dialogs.Choices.Enqueue(SaveChoice.Save);                           // Save on an untitled document is Save As...
        Assert.False(main.Files.ConfirmDiscardChanges());                   // ...and a cancelled Save As cancels the action
        Assert.Equal(new[] { "untitled.r15proj" }, files.SuggestedProjects);
        dialogs.Choices.Enqueue(SaveChoice.DontSave);
        Assert.True(main.Files.ConfirmDiscardChanges());
        Assert.Equal(new[] { "Untitled", "Untitled", "Untitled" }, dialogs.SaveChanges);
        Assert.True(s.IsDirty);                                             // Don't save leaves the document to the caller
        Assert.Null(s.CurrentFile);

        string path = Path.Combine(_dir, "guard.r15proj");
        files.Saves.Enqueue(path);
        dialogs.Choices.Enqueue(SaveChoice.Save);
        Assert.True(main.Files.ConfirmDiscardChanges());
        Assert.True(File.Exists(path));
        Assert.False(s.IsDirty);
        Assert.True(s.Undo());                                               // R2: past the save reads dirty...
        Assert.True(s.IsDirty);
        Assert.True(s.Redo());                                               // ...and back to it reads clean
        Assert.False(s.IsDirty);
        Assert.True(main.Files.ConfirmDiscardChanges());
        Assert.Equal(4, dialogs.SaveChanges.Count);
    });

    [Fact]
    public void The_built_in_packs_ship_byte_identical_list_sorted_and_add_by_click_and_right_click_in_one_step_each() => StaThread.Run(() =>
    {
        // pack-contents, packs-inventory-drips, packs-inventory-wordart, pack-builtin-load, packs-load-builtin,
        // pack-load-sync-and-mime, asset-click-add, asset-rightclick-full (editor.js:2340-2378, :2491-2539)
        string repo = RepoRoot.Find(AppContext.BaseDirectory);
        var packs = new PackSource(PackSource.DefaultDirectory);
        foreach (var (name, count) in new[] { (PackSource.Drips, 156), (PackSource.WordArt, 25) })
        {
            var shipped = packs.Files(name)!.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
            var source = Directory.GetFiles(Path.Combine(repo, "packs", name), "*.png").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.Equal(count, shipped.Count);
            Assert.Equal(source.Select(Path.GetFileName), shipped.Select(Path.GetFileName));
            for (int i = 0; i < count; i++)
                Assert.Equal(SHA256.HashData(File.ReadAllBytes(source[i])), SHA256.HashData(File.ReadAllBytes(shipped[i])));
        }

        var (s, main, _, _, _) = Shell4a();
        main.Home.Tools.Set(Tool.Pen);
        main.Assets.LoadDripsCommand.Execute(null);
        Assert.Equal("156 images in built-in drips pack", main.Assets.CountText);   // editor.js:2373, no longer overwritten
        var tiles = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(156, tiles.Count);
        Assert.All(main.Assets.Rows.Take(main.Assets.Rows.Count - 1), r => Assert.Equal(3, r.Tiles.Count));   // editor.html:305
        var names = tiles.Select(t => t.Entry.FileName).ToList();
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
        Assert.Equal("drip_blood_front_flipped.png", names[0]);

        var front = tiles.Single(t => t.Entry.FileName == "drip_blue_front_v1.png");   // 128x150
        Assert.Equal(("drip_blue_front_v1", "drip_blue_front_v1.png"), (front.Caption, front.ToolTip));
        main.Assets.AddCommand.Execute(front);
        var l = s.Document.Layers[^1];
        double fit = Math.Min(128.0 / 128, 128.0 / 150) * 0.85;                       // 0.7253..., the front slot 231,74,128,128
        Assert.Equal("drip_blue_front_v1", l.Name);
        Assert.Equal(fit, l.Scale, 12);
        Assert.Equal(231 + (128 - 128 * fit) / 2, l.X, 9);
        Assert.Equal(74 + (128 - 150 * fit) / 2, l.Y, 9);
        Assert.Equal((l.Id, 1, Tool.Select), (s.SelectedLayerId!.Value, s.UndoCount, main.Home.Tools.Current));   // editor.js:523

        var wide = tiles.Single(t => t.Entry.FileName == "drip_blue_wide_v1.png");     // 390x200
        main.Assets.AddFullCommand.Execute(wide);
        var full = s.Document.Layers[^1];
        Assert.Equal("drip_blue_wide_v1 (full)", full.Name);
        Assert.Equal((0.0, 0.0), (full.X, full.Y));
        Assert.Equal(Math.Max(585.0 / 390, 559.0 / 200), full.Scale, 12);              // 2.795, unclamped (R15)
        Assert.Equal(2, s.UndoCount);
        Assert.True(s.Undo());
        Assert.DoesNotContain(s.Document.Layers, x => x.Id == full.Id);

        main.Assets.LoadWordArtCommand.Execute(null);
        Assert.Equal("25 images in built-in word_art pack", main.Assets.CountText);
        var words = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(25, words.Count);
        main.Assets.AddCommand.Execute(words[0]);
        Assert.Equal(Path.GetFileNameWithoutExtension(words[0].Entry.FileName), s.Document.Layers[^1].Name);
    });

    [Fact]
    public void A_folder_lists_the_import_formats_dedupes_by_name_keeping_the_deepest_and_shows_the_duplicates_count() => StaThread.Run(() =>
    {
        // asset-open-folder, asset-dedupe-by-name, asset-folder-grouping, asset-count-label, asset-count-label-overwritten,
        // asset-thumb-uuid-label, io-folder-picker (editor.js:2380-2470, :2478-2486, :2544; interfaces R14)
        const string uuid = "1a2b3c4d-1111-2222-3333-444444444444";
        string root = Path.Combine(_dir, "art");
        string Put(string rel, int w, int h, SKColor color)
        {
            string p = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            using var image = Solid(w, h, color);
            File.WriteAllBytes(p, Png(image));
            return p;
        }
        Put(@"a\x.png", 10, 10, Red);
        Put(@"a\sub\x.png", 10, 10, Red);
        Put(@"b\x.png", 10, 10, Red);
        string deepest = Put(@"a\sub\deep\x.png", 12, 7, Blue);
        Put("y.png", 10, 10, Slate);
        Put($@"Cool\{uuid}.png", 9, 9, Blue);
        File.WriteAllBytes(Path.Combine(root, "z.bmp"), TestFiles.Bmp2x1());
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not an image");
        File.WriteAllText(Path.Combine(root, "logo.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        var (s, main, files, dialogs, settings) = Shell4a();
        files.Folders.Enqueue(root);
        main.Assets.OpenFolderCommand.Execute(null);
        Assert.Equal("4 unique images (3 duplicates removed)", main.Assets.CountText);   // editor.js:2448, shown (4.1 overwrote it)
        Assert.Equal(new[] { "All (4)", "Root (2)", "Cool (1)", "deep (1)" }, main.Assets.GroupButtons.Select(g => g.Label));
        var tiles = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
        Assert.Equal(deepest, tiles.Single(t => t.Entry.FileName == "x.png").Entry.FullPath);
        Assert.Contains(tiles, t => t.Entry.FileName == "z.bmp");                        // R14: the import formats, .bmp included
        Assert.DoesNotContain(tiles, t => t.Entry.FileName is "notes.txt" or "logo.svg");
        Assert.Equal(root, settings.Folders[SettingsFolder.Asset]);

        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons.Single(g => g.Key == "Cool"));
        Assert.Equal("1 images in Cool", main.Assets.CountText);                          // editor.js:2544
        var tile = Assert.Single(main.Assets.Rows.SelectMany(r => r.Tiles));
        Assert.Equal("Cool", tile.Caption);                                                // a UUID name shows its folder
        main.Assets.AddCommand.Execute(tile);
        Assert.Equal(uuid, s.Document.Layers[^1].Name);
        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons[0]);
        Assert.Equal("4 images in all folders", main.Assets.CountText);

        string empty = Directory.CreateDirectory(Path.Combine(_dir, "empty")).FullName;
        files.Folders.Enqueue(empty);
        main.Assets.OpenFolderCommand.Execute(null);
        Assert.Equal("No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files.", dialogs.Alerts[^1]);   // editor.js:2385
    });

    [Fact]
    public void A_thumbnail_or_a_file_dropped_on_the_canvas_lands_centered_on_the_drop_point_at_every_zoom() => StaThread.Run(() =>
    {
        // asset-drag-to-canvas, drop-canvas (editor.js:961-986 the canvas drop; :500-526 addLayer's drop placement; interfaces R6)
        var (s, main, _, _, _) = Shell4a();
        main.Assets.LoadDripsCommand.Execute(null);
        var front = main.Assets.Rows.SelectMany(r => r.Tiles).Single(t => t.Entry.FileName == "drip_blue_front_v1.png");
        foreach (double zoom in new[] { 1.0, 2.0 })
        {
            main.Assets.DropOnCanvas(front, CanvasCoordinates.ToTemplate(new Point(300 * zoom, 200 * zoom), zoom));
            var l = s.Document.Layers[^1];
            Assert.Equal(300, l.X + 128 * l.Scale / 2, 9);
            Assert.Equal(200, l.Y + 150 * l.Scale / 2, 9);
        }
        Assert.Equal(2, s.UndoCount);

        string png = WritePng("dropped.png", 40, 20, Blue);
        foreach (double zoom in new[] { 0.5, 1.0, 2.0 })
            foreach (var (tx, ty) in new[] { (10.0, 10.0), (150.0, 300.0), (292.5, 279.5), (575.0, 549.0) })
            {
                main.Canvas.DropHandler!(new CanvasDrop([png], CanvasCoordinates.ToTemplate(new Point(tx * zoom, ty * zoom), zoom)));
                var l = s.Document.Layers[^1];
                Assert.Equal("dropped", l.Name);
                Assert.Equal(tx, l.X + 40 * l.Scale / 2, 9);
                Assert.Equal(ty, l.Y + 20 * l.Scale / 2, 9);
            }
        Assert.Equal(14, s.UndoCount);
    });

    [Fact]
    public void Stored_values_out_of_range_open_raw_as_4_1_opens_them() => StaThread.Run(() =>
    {
        // project-load-layers note (interfaces R12; editor.js:2632-2660 copies each stored field as it is)
        using var art = Solid(20, 10, Red);
        var one = new Document { Layers = ImmutableList.Create(new Layer { Id = 4, Image = art, Name = "raw" }) };
        var node = JsonNode.Parse(ProjectIO.Write(one, DateTimeOffset.UnixEpoch))!;
        var layer = node["layers"]![0]!;
        layer["rotation"] = 720;
        layer["scale"] = 9;
        layer["opacity"] = 2.5;
        layer["brightness"] = 500;
        layer["hueRotate"] = -90;
        layer["x"] = 5000;
        string path = WriteText("raw.r15proj", node.ToJsonString());
        var (s, main, _, _, _) = Shell4a();
        Assert.True(Await(main.Files.OpenAsync(path)));
        var l = Assert.Single(s.Document.Layers);
        Assert.Equal((720.0, 9.0, 2.5, 500.0, -90.0, 5000.0), (l.Rotation, l.Scale, l.Opacity, l.Brightness, l.HueRotate, l.X));
        Assert.Equal(5, s.NextId);
    });
```

Edit 3, the helpers and fakes. Insert this block immediately before the line `    sealed class Picker(params string?[] answers) : IColorPicker`:

```csharp
    /// <summary>The M4a scenarios' shell: the real composition with recording fakes for the dialogs and the settings file.</summary>
    static (EditorSession Session, MainViewModel Main, ProjectDialogs Files, PromptDialogs Dialogs, MemorySettings Settings) Shell4a()
    {
        var s = new EditorSession();
        var files = new ProjectDialogs();
        var dialogs = new PromptDialogs();
        var settings = new MemorySettings();
        return (s, Composition.Build(s, files, dialogs, new Picker(), settings), files, dialogs, settings);
    }

    /// <summary>Pumps the STA dispatcher until <paramref name="task"/> completes: Open reads on the pool and resumes here.</summary>
    static T Await<T>(Task<T> task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>File dialogs that answer from queues (an empty queue is a cancel) and record the Save dialog's suggestions.</summary>
    sealed class ProjectDialogs : IFileDialogs
    {
        public Queue<string?> Saves { get; } = new();
        public Queue<string?> Opens { get; } = new();
        public Queue<string?> Folders { get; } = new();
        public List<string> SuggestedProjects { get; } = [];

        public IReadOnlyList<string> OpenImages() => [];

        public string? SavePng(string suggestedFileName) => null;

        public string? OpenProject(string? initialDirectory) => Opens.Count > 0 ? Opens.Dequeue() : null;

        public string? SaveProject(string suggestedFileName, string? initialDirectory)
        {
            SuggestedProjects.Add(suggestedFileName);
            return Saves.Count > 0 ? Saves.Dequeue() : null;
        }

        public string? PickFolder(string? initialDirectory) => Folders.Count > 0 ? Folders.Dequeue() : null;
    }

    /// <summary>Themed dialogs that record every message; the save prompt answers from a queue (empty: Cancel).</summary>
    sealed class PromptDialogs : IDialogService
    {
        public Queue<SaveChoice> Choices { get; } = new();
        public List<string> SaveChanges { get; } = [];
        public List<string> Alerts { get; } = [];

        public bool Confirm(string message) => true;

        public void Alert(string message) => Alerts.Add(message);

        public SaveChoice AskSaveChanges(string documentName)
        {
            SaveChanges.Add(documentName);
            return Choices.Count > 0 ? Choices.Dequeue() : SaveChoice.Cancel;
        }
    }

    /// <summary>An in-memory settings file that keeps the remembered folders.</summary>
    sealed class MemorySettings : ISettingsStore
    {
        public Dictionary<SettingsFolder, string> Folders { get; } = [];

        public WindowPlacement? LoadWindowPlacement() => null;

        public void SaveWindowPlacement(WindowPlacement placement) { }

        public string? LoadFolder(SettingsFolder which) => Folders.GetValueOrDefault(which);

        public void SaveFolder(SettingsFolder which, string folder) => Folders[which] = folder;
    }
```

Why these shapes:

- The scenarios build the composition with their own three fakes instead of `TestShell.Main`, because they need answers the shared fakes do not carry (queued Save and folder answers, queued save-prompt choices, remembered folders). The fakes implement the interfaces exactly as the sheet's 3.3 lists them; a task that adds an interface member adds it to these three too (Additions 10).
- `Await` pushes a dispatcher frame because `ProjectFileService.OpenAsync` reads on the pool and resumes on the STA dispatcher (R4); blocking on `.Result` would deadlock.
- Every expected value is 4.1's: the front slot (231, 74, 128, 128) and the 0.85 fit are `LayerOps.Place`'s port of editor.js:500-526, the full-size scale is editor.js:2525, the counts are editor.js:2373, :2448 and :2544, the alert is editor.js:2385, the file name is editor.js:2587 through `ExportNaming.ProjectFileName`.

- [ ] **Step 3: Run the scenarios**

These facts characterize code Tasks 1-4 committed, so they pass on the first run. Run them alone three times (they write temp files and run Open on the pool), then the whole suite:

```bash
dotnet build src/RBX15.slnx -c Release -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
for i in 1 2 3; do dotnet test src/RBX15.slnx -c Release --no-build --filter "FullyQualifiedName~LedgerScenarioTests" 2>&1 | grep -E "(Passed|Failed)!"; done
dotnet test src/RBX15.slnx -c Release --no-build 2>&1 | grep -E "(Passed|Failed)!"
```

Expected:

- `0 Warning(s)` and `0 Error(s)` (measured on compile stand-ins).
- Three lines `Passed!  - Failed:     0, Passed:    34, Skipped:     0, Total:    34` for `RBX15.App.Tests.dll` (14 M3a, 12 M3b, 1 M4a Task 1, 7 M4a Task 5).
- The full run: Engine `938`, Parity `35`, App `867`, each `Failed: 0`, `Skipped: 0`.

If a fact fails, do not edit it and do not change production code in this task. Report the fact and its owner:

| Fact | Owner |
| --- | --- |
| `Save_as_writes_*`, `A_file_that_cannot_open_*`, `The_unsaved_changes_guard_*`, `Stored_values_out_of_range_*` | Task 1 (`ProjectFileService`, `EditorSession`, `MainViewModel.Title`), with M2b (`ProjectIO`) when a value is off |
| `The_built_in_packs_*`, `A_folder_lists_*`, `A_thumbnail_or_a_file_dropped_*` | Task 2 (`AssetScan`, `PackSource`, `AssetLibraryViewModel`, `ImportService.ImportFull`, the packs Content item), with M3a (`ImportService`, the canvas drop) |

- [ ] **Step 4: Commit the scenarios**

Add your own model's `Co-Authored-By` trailer as the last line before running it.

```bash
git add src/RBX15.App.Tests/LedgerScenarioTests.cs
git commit -F - <<'EOF'
test(app): replay the M4a ledger scenarios for files, the unsaved prompt, packs, folders and drops

Seven facts through the real composition with recording fakes for the
file dialogs, the themed dialogs and the settings file: Save work on an
untitled document runs Save As with the sanitized template name, writes
version 5.0 with no title and no temp file, marks the title clean and
remembers the folder, Undo back to the save reads clean, and reopening
with Don't save restores the export with an empty history; a file that
cannot open alerts with nothing changed and no prompt, and layers that
cannot load are named in the status strip; the unsaved-changes guard
asks only when dirty, and Cancel or a cancelled Save As keeps
everything; the built-in packs ship byte-identical, list sorted three
per row, and add by click and right-click in one step each; a folder
lists the import formats, keeps the deepest copy of a name and shows the
duplicates count; a thumbnail or a file dropped on the canvas lands
centered on the drop point at every zoom; stored values out of range
open raw.
EOF
git log --oneline -1
```

Expected: one commit with the subject `test(app): replay the M4a ledger scenarios for files, the unsaved prompt, packs, folders and drops`.

- [ ] **Step 5: Build the bridge smoke driver (scratch)**

The driver lives under `.superpowers/m4a-smoke/bridge/` (git-ignored), like the M3b look and hand drivers: it needs a live desktop and a flagged build, and spec §6's committed UI-automation tier is M6. The implementer builds it; only the controller runs it (Steps 7 and 8).

Create `.superpowers/m4a-smoke/bridge/bridge.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- M4a Task 5 smoke driver (scratch, git-ignored). Talks to the app only through the test input bridge's pipe,
       UI Automation patterns on the launched RBX15 process, and PrintWindow of that process's own windows. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <AssemblyName>brg</AssemblyName>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Create `.superpowers/m4a-smoke/bridge/Program.cs`:

```csharp
// M4a smoke driver "brg" (scratch, git-ignored: .superpowers/m4a-smoke/bridge/). Plan: M4a Task 5.
// It drives a Release build of RBX15.App compiled with -p:EnableTestInputBridge=true and launched with
// --test-input-bridge, and records evidence for the M3b hand checks the bridge can reproduce (H1-H5, H7, H7b and
// the H10 picker) and for the M4a file, asset, dialog and canvas-automation checks (F1-F4, A1, A2, U1).
//
// SAFETY RULES (planning contract "no synthetic global input"; the m3b-look hand driver's rules, extended):
//   1. It acts only on the RBX15 process it launched. The pipe name must carry that process id
//      (rbx15-bridge-<pid>-<random>), every UI Automation element is PID-checked before any pattern call, and a
//      mismatch aborts the run.
//   2. Its inputs are (a) JSON commands on the bridge's named pipe, which the app dispatches on its own UI thread
//      into its own visual tree, and (b) UI Automation patterns (Invoke, SelectionItem, RangeValue, Value, Toggle,
//      Scroll, Transform, Window, SetFocus) on PID-checked elements. It posts no window message and sends no key
//      or mouse input of any kind.
//   3. No SendInput, mouse_event, keybd_event, SendKeys, SetCursorPos, ClipCursor, hooks, SetForegroundWindow or
//      screen-coordinate actions. It never captures the screen: PrintWindow of the app's own HWNDs only. Its only
//      other Win32 calls are reads (window rectangles, DPI, the client origin, process ids, class names).
//   4. It never shows a native dialog: the bridge build answers every file dialog from the queue the `file-dialog`
//      command fills (an empty queue is a cancel) and keeps its settings in %TEMP%\rbx15-bridge-<pid>
//      (interfaces R10), so the user's settings.json is never read or written.
//   5. It writes only under the output folder it is given and under %TEMP%\rbx15-bridge-build (F4 parks the
//      build's own packs\drips folder and restores it in a finally block). It closes the app it launched,
//      answering "Don't save" to the app's own prompt, and kills only that process if it does not exit.
//   6. It refuses to run at a display scale other than 100% (high DPI is manual item MAN-3).
//
//   brg build                              build the bridge-enabled app into %TEMP%\rbx15-bridge-build (R9)
//   brg probe                              launch, read the pipe line, ping, one canvas move, inspect
//   brg smoke <out-dir> [items]            H1 H2 H3 H4 H5 H7 H7b H10 -> smoke-results.md, smoke-items.txt, PNGs
//   brg files <out-dir> [items]            F1 F2 F3 F4 A1 A2 U1      -> files-results.md, files-items.txt, PNGs
//   brg compare <ref.png> <candidate.png>  the golden rule (RBX15.Parity ImageDiff.Compare, edge tolerance 3)
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Condition = System.Windows.Automation.Condition;

sealed class BridgeException(string message) : Exception(message);

/// <summary>One launched app and its pipe. Every request is one JSON line; every response is one JSON line.</summary>
sealed class Bridge : IDisposable
{
    public const string PipePrefix = "RBX15-BRIDGE-PIPE ";
    public const string DialogPrefix = "RBX15-BRIDGE-DIALOG ";
    readonly List<string> _stdout = new();
    NamedPipeClientStream _pipe;
    StreamReader _in;
    StreamWriter _out;
    int _id;

    public Process Proc { get; private set; }
    public string PipeName { get; private set; }

    public IReadOnlyList<string> DialogLines
    {
        get { lock (_stdout) return _stdout.Where(l => l.StartsWith(DialogPrefix, StringComparison.Ordinal)).ToList(); }
    }

    public static Bridge Launch(string exe)
    {
        var b = new Bridge();
        var psi = new ProcessStartInfo(exe, "--test-input-bridge")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(exe),
        };
        b.Proc = Process.Start(psi) ?? throw new Exception("launch failed");
        b.Proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (b._stdout) b._stdout.Add(e.Data); };
        b.Proc.BeginOutputReadLine();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (b.PipeName == null)
        {
            lock (b._stdout)
            {
                var line = b._stdout.FirstOrDefault(l => l.StartsWith(PipePrefix, StringComparison.Ordinal));
                if (line != null) b.PipeName = line[PipePrefix.Length..].Trim();
            }
            if (b.PipeName != null) break;
            if (b.Proc.HasExited) throw new Exception("the app exited before printing its pipe name (is this a bridge build?)");
            if (DateTime.UtcNow > deadline) throw new Exception("no " + PipePrefix.Trim() + " line within 30 s");
            Thread.Sleep(100);
        }
        if (!b.PipeName.StartsWith($"rbx15-bridge-{b.Proc.Id}-", StringComparison.Ordinal))
            throw new Exception("REFUSED: the pipe name does not carry the launched process id: " + b.PipeName);
        b._pipe = new NamedPipeClientStream(".", b.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        b._pipe.Connect(10000);
        b._in = new StreamReader(b._pipe, new UTF8Encoding(false));
        b._out = new StreamWriter(b._pipe, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        return b;
    }

    /// <summary>Sends one command and returns its result object; a response with ok=false throws.</summary>
    public JsonObject Send(JsonObject command, int timeoutMs = 60000)
    {
        int id = ++_id;
        command["id"] = id;
        _out.WriteLine(command.ToJsonString());
        var read = _in.ReadLineAsync();
        if (!read.Wait(timeoutMs)) throw new BridgeException($"no response to {command.ToJsonString()} within {timeoutMs} ms");
        string line = read.Result ?? throw new BridgeException("the bridge closed the pipe");
        var response = JsonNode.Parse(line)!.AsObject();
        if ((int)response["id"]! != id) throw new BridgeException($"response id {response["id"]} for request {id}");
        if (!(bool)response["ok"]!) throw new BridgeException($"{command["cmd"]}: {response["error"]}");
        return response["result"] as JsonObject ?? new JsonObject();
    }

    public void Dispose()
    {
        try { _pipe?.Dispose(); } catch (IOException) { }
    }
}

sealed class LayerSt
{
    public int Id, W, H;
    public string Name;
    public double X, Y, Scale, Rotation;
    public bool Visible;
    public long Image;
    public double Cx => X + W * Scale / 2;
    public double Cy => Y + H * Scale / 2;
}

sealed class CanvasSt
{
    public string Cursor, HostCursor, OverrideCursor, Gesture, Tool;
    public double Ring, RingRadiusPx, Zoom;
    public bool Captured;
    public int Undo, Redo, Brush;
    public int? Selected;
    public List<LayerSt> Layers = new();
    public Dictionary<string, string> FillC1 = new();
    public LayerSt L(int id) => Layers.Single(l => l.Id == id);
}

sealed class RowSt
{
    public int Id;
    public string Name, Drop;
    public bool DragSource;
}

static class P
{
    const string Repo = @"C:\Users\estev\Projects\RBX15-Shirt-and-Pants";
    const string Product = "RBX15 Classic Shirt and Pants Maker";
    static readonly string BuildRoot = Path.Combine(Path.GetTempPath(), "rbx15-bridge-build");
    static readonly string Exe = Path.Combine(BuildRoot, "bin", "RBX15.App", "release", "RBX15.App.exe");
    const double S = 1;   // 100% display scale only (rule 6): screen px = DIPs

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
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    const uint PW_RENDERFULLCONTENT = 2;

    static Bridge B;
    static int Pid;
    static string Out, Prefix;
    static readonly StringBuilder Log = new();
    static readonly List<(string Item, string Check, string Result, string Detail)> Results = new();
    static readonly List<(string Item, bool Pass)> Items = new();

    static int Main(string[] a)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Console.OutputEncoding = Encoding.UTF8;
        string cmd = a.Length > 0 ? a[0] : "";
        try
        {
            switch (cmd)
            {
                case "build": return Build();
                case "probe": return Probe();
                case "compare" when a.Length == 3: return Compare(a[1], a[2]);
                case "smoke" when a.Length >= 2:
                    return RunItems(a, new (string, Action)[] { ("H1", H1), ("H2", H2), ("H3", H3), ("H4", H4), ("H5", H5), ("H7", H7), ("H7b", H7b), ("H10", H10) });
                case "files" when a.Length >= 2:
                    return RunItems(a, new (string, Action)[] { ("F1", F1), ("F2", F2), ("F3", F3), ("F4", F4), ("A1", A1), ("A2", A2), ("U1", U1) });
            }
        }
        catch (Exception e) { Say("ERROR " + e.GetType().Name + ": " + e.Message); return 1; }
        finally { StopApp(); }
        Console.WriteLine("usage: brg build | brg probe | brg smoke <out-dir> [items] | brg files <out-dir> [items] | brg compare <ref.png> <candidate.png>");
        return 2;
    }

    // ---------------------------------------------------------------- commands

    static int Build()
    {
        string project = Path.Combine(Repo, "src", "RBX15.App", "RBX15.App.csproj");
        var psi = new ProcessStartInfo("dotnet", $"build \"{project}\" -c Release -p:EnableTestInputBridge=true --artifacts-path \"{BuildRoot}\" -nologo -v q")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi) ?? throw new Exception("dotnet did not start");
        var err = p.StandardError.ReadToEndAsync();
        string o = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        foreach (var line in (o + "\n" + err.Result).Split('\n').Select(l => l.Trim()))
            if (line.Contains("Warning(s)") || line.Contains("Error(s)") || line.Contains(": error ") || line.Contains(": warning ")) Console.WriteLine(line);
        Console.WriteLine(File.Exists(Exe) ? "exe " + Exe : "MISSING " + Exe);
        return p.ExitCode == 0 && File.Exists(Exe) ? 0 : 1;
    }

    static int Probe()
    {
        RequireBuild();
        Launch();
        Move(Canvas(100, 100));
        var r = B.Send(new JsonObject { ["cmd"] = "inspect", ["target"] = "canvas" });
        Say("inspect " + r.ToJsonString());
        Say("probe ok");
        return 0;
    }

    static int RunItems(string[] a, (string Id, Action Body)[] items)
    {
        RequireBuild();
        Prefix = a[0];
        Out = Path.GetFullPath(a[1]);
        Directory.CreateDirectory(Out);
        var only = a.Length > 2 ? a[2].Split(',') : null;
        if (only != null && only.Except(items.Select(i => i.Id)).FirstOrDefault() is string unknown) throw new Exception("unknown item " + unknown);
        foreach (var (id, body) in items)
        {
            if (only != null && !only.Contains(id)) continue;
            int first = Results.Count;
            try { body(); }
            catch (Exception e) { Record(id, "driver error", false, e.GetType().Name + ": " + e.Message); }
            finally { StopApp(); }
            var mine = Results.Skip(first).ToList();
            Items.Add((id, mine.Count > 0 && mine.All(r => r.Result == "pass")));
        }
        var md = new StringBuilder();
        md.AppendLine($"# M4a bridge {Prefix} run").AppendLine();
        md.AppendLine($"Run {DateTime.Now:yyyy-MM-dd HH:mm}, `{Exe}` with `--test-input-bridge`, 100% display scale.").AppendLine();
        md.AppendLine("| Item | Check | Result | Detail |").AppendLine("| --- | --- | --- | --- |");
        foreach (var r in Results) md.AppendLine($"| {r.Item} | {r.Check} | {r.Result} | {r.Detail.Replace("|", "\\|")} |");
        File.WriteAllText(Path.Combine(Out, Prefix + "-results.md"), md.ToString());
        File.WriteAllText(Path.Combine(Out, Prefix + "-items.txt"), string.Concat(Items.Select(i => $"{i.Item}: {(i.Pass ? "pass" : "fail")}\n")));
        File.WriteAllText(Path.Combine(Out, Prefix + "-run.log"), Log.ToString());
        Say($"done: {Items.Count(i => i.Pass)} of {Items.Count} items pass; {Results.Count(r => r.Result == "pass")} checks pass, {Results.Count(r => r.Result != "pass")} fail");
        return Items.All(i => i.Pass) ? 0 : 1;
    }

    static void RequireBuild()
    {
        if (!File.Exists(Exe)) throw new Exception("no bridge build at " + Exe + "; run: brg build");
    }

    // ---------------------------------------------------------------- H1: move drag, capture, release outside, click, zoom 2, pick

    static void H1()
    {
        // select-move, select-move-hit-and-drag, select-hover-and-end, canvas-select-gestures (editor.js:1100-1113, :1169-1171, :1239-1246)
        Launch();
        AddImage("drip_red_wide_v1.png");
        var (iw, ih) = PngSize(Pack("drip_red_wide_v1.png"));
        var s = Inspect();
        var l0 = s.Layers.Single();
        int id = l0.Id, u0 = s.Undo;
        double x0 = l0.X, y0 = l0.Y, cx = l0.Cx, cy = l0.Cy;
        Record("H1", "the added drip is selected with the Select tool", s.Selected == id && s.Tool == "Select" && l0.W == iw && l0.H == ih, $"selected {s.Selected}, tool {s.Tool}, image {l0.W}x{l0.H}");

        Move(Canvas(cx, cy));
        var hv = Inspect();
        Record("H1", "hovering the body shows the move cursor", hv.Cursor == "Move" && hv.HostCursor == "SizeAll", $"{hv.Cursor} / {hv.HostCursor}");

        Down(Canvas(cx, cy));
        bool follows = true;
        var trail = new StringBuilder();
        foreach (var (px, py) in new[] { (cx + 10, cy + 6), (cx + 30, cy + 18), (cx + 50, cy + 30), (700.0, cy + 30), (760.0, cy - 40), (300.0, 250.0) })
        {
            Move(Canvas(px, py));
            var m = Inspect();
            var l = m.L(id);
            bool ok = Near(l.X, x0 + px - cx) && Near(l.Y, y0 + py - cy) && m.Captured && m.Gesture == "Move";
            follows &= ok;
            trail.Append($"({px:0.#},{py:0.#})->({l.X - x0:0.##},{l.Y - y0:0.##}){(ok ? "" : "!")} ");
        }
        Record("H1", "the layer follows the pointer with the mouse captured, also past the canvas edge (x 700 and 760 > 585) and back", follows, trail.ToString());

        var o = OutsideWindow();
        Up(Canvas(o.X, o.Y));
        var e = Inspect();
        var le = e.L(id);
        Shot("h1-released-outside");
        Record("H1", "released outside the window, it stays where released, and the drag is one undo step",
            Near(le.X, x0 + o.X - cx) && Near(le.Y, y0 + o.Y - cy) && e.Gesture == "None" && !e.Captured && e.Undo == u0 + 1,
            $"release at template ({o.X:0.#},{o.Y:0.#}), left of the window; layer at ({le.X:0.##},{le.Y:0.##}); undo {u0} -> {e.Undo}");

        Undo();
        var ub = Inspect();
        Record("H1", "one Undo puts it back where the drag started", Near(ub.L(id).X, x0) && Near(ub.L(id).Y, y0) && ub.Undo == u0, $"({ub.L(id).X:0.##},{ub.L(id).Y:0.##}), undo {ub.Undo}");

        Down(Canvas(cx, cy));
        Up(Canvas(cx, cy));
        var c = Inspect();
        Record("H1", "a click without moving changes nothing and records no step", Near(c.L(id).X, x0) && Near(c.L(id).Y, y0) && c.Undo == u0 && c.Selected == id, $"undo {c.Undo}");

        ZoomTo(2);
        Move(Canvas(cx, cy));
        var hz = Inspect();
        Down(Canvas(cx, cy));
        Move(Canvas(cx + 12.5, cy + 7.5));
        Up(Canvas(cx + 25, cy + 15));
        var z1 = Inspect();
        Record("H1", "at zoom 200% a 50x30 px drag moves the layer 25x15 template px, as one step",
            hz.Cursor == "Move" && z1.Zoom == 2 && Near(z1.L(id).X, x0 + 25) && Near(z1.L(id).Y, y0 + 15) && z1.Undo == u0 + 1,
            $"hover {hz.Cursor}; moved ({z1.L(id).X - x0:0.###},{z1.L(id).Y - y0:0.###}); undo {z1.Undo}");
        Down(Canvas(cx + 25, cy + 15));
        Up(Canvas(cx + 40, cy + 5));
        var z2 = Inspect();
        Record("H1", "at zoom 200% a (30,-20) px drag moves the layer (15,-10) template px",
            Near(z2.L(id).X, x0 + 40) && Near(z2.L(id).Y, y0 + 5) && z2.Undo == u0 + 2, $"moved ({z2.L(id).X - x0 - 25:0.###},{z2.L(id).Y - y0 - 15:0.###})");
        Undo();
        Undo();
        ZoomTo(1);

        AddImage("drip_blue_wide_v1.png");
        var s2 = Inspect();
        var top = s2.Layers.Last();
        var (tx, ty) = TransparentPoint(Pack("drip_blue_wide_v1.png"), top);
        Select(Row("drip_red_wide_v1"));
        Down(Canvas(tx, ty));
        Up(Canvas(tx, ty));
        var p1 = Inspect();
        Record("H1", "overlapping layers: a press on a fully transparent pixel inside the top layer's box selects the top layer (box pick)",
            p1.Selected == top.Id && p1.Undo == s2.Undo, $"press at ({tx:0.##},{ty:0.##}); selected {p1.Selected} (top {top.Id})");
        Invoke(Find("Hide drip_blue_wide_v1", "Button"));
        Down(Canvas(tx, ty));
        Up(Canvas(tx, ty));
        var p2 = Inspect();
        Record("H1", "a hidden layer is skipped: the same press selects the visible layer under it", p2.Selected == id, $"selected {p2.Selected} (lower {id})");
    }

    // ---------------------------------------------------------------- H2: hover cursors, corner resize, knob rotate, live panel

    static void H2()
    {
        // cursor-default, canvas-cursor-states, select-hover-idle, canvas-select-gestures, props-live-refresh
        // (editor.js:456-459, :1155-1167, :1172-1192)
        Launch();
        AddImage("drip_red_wide_v1.png");
        var s = Inspect();
        var l0 = s.Layers.Single();
        int id = l0.Id, u0 = s.Undo;
        double cx = l0.Cx, cy = l0.Cy, s0 = l0.Scale;
        var knob = Knob(l0, 1);
        var corners = Corners(l0);
        var hover = new List<(string Where, (double X, double Y) At, string Kind, string Wpf)>
        {
            ("knob", knob, "Grab", "Hand"),
            ("top-left corner", corners[0], "ResizeNwse", "SizeNWSE"),
            ("top-right corner", corners[1], "ResizeNwse", "SizeNWSE"),
            ("bottom-right corner", corners[2], "ResizeNwse", "SizeNWSE"),
            ("bottom-left corner", corners[3], "ResizeNwse", "SizeNWSE"),
            ("body", (cx, cy), "Move", "SizeAll"),
            ("empty canvas", (10, 540), "Crosshair", "Cross"),
        };
        bool allHover = true;
        var hd = new StringBuilder();
        foreach (var (where, at, kind, wpf) in hover)
        {
            Move(Canvas(at.X, at.Y));
            var h = Inspect();
            bool ok = h.Cursor == kind && h.HostCursor == wpf;
            allHover &= ok;
            hd.Append($"{where} {h.Cursor}/{h.HostCursor}{(ok ? "" : "!")}; ");
        }
        Record("H2", "idle hover: knob Grab (Hand), corners ResizeNwse (SizeNWSE), body Move (SizeAll), empty canvas Crosshair (Cross)", allHover, hd.ToString());

        var br = corners[2];
        double d0 = Dist((cx, cy), br);
        Down(Canvas(br.X, br.Y));
        bool grows = true;
        var gd = new StringBuilder();
        foreach (double f in new[] { 1.2, 1.4, 1.5 })
        {
            var p = (X: cx + (br.X - cx) * f, Y: cy + (br.Y - cy) * f);
            Move(Canvas(p.X, p.Y));
            var m = Inspect();
            var l = m.L(id);
            double want = s0 * Dist((cx, cy), p) / d0;
            double slider = Range("Scale percent");
            bool ok = Near(l.Scale, want, 1e-9) && Near(l.Cx, cx, 1e-6) && Near(l.Cy, cy, 1e-6) && m.Cursor == "ResizeNwse" && Near(slider, l.Scale * 100, 1e-6);
            grows &= ok;
            gd.Append($"x{f}: scale {l.Scale:0.#####} (want {want:0.#####}), slider {slider:0.###}, cursor {m.Cursor}{(ok ? "" : "!")}; ");
        }
        Up(Canvas(cx + (br.X - cx) * 1.5, cy + (br.Y - cy) * 1.5));
        var r1 = Inspect();
        Record("H2", "a corner drag grows the layer about its center, keeps the resize cursor, the Scale slider follows live, one step",
            grows && r1.Undo == u0 + 1, gd + $"undo {u0} -> {r1.Undo}");

        var l1 = r1.L(id);
        var k = Knob(l1, 1);
        double radius = Dist((cx, cy), k);
        Down(Canvas(k.X, k.Y));
        bool turns = true;
        var td = new StringBuilder();
        foreach (var (angle, want) in new[] { (20.0, 20.0), (44.0, 45.0), (120.0, 120.0), (182.0, -178.0) })
        {
            double rad = angle * Math.PI / 180;
            Move(Canvas(cx + radius * Math.Sin(rad), cy - radius * Math.Cos(rad)));
            var m = Inspect();
            double rot = m.L(id).Rotation, slider = Range("Rotation degrees");
            string label = TextStartingWith("ROTATION: ");
            string wantLabel = "ROTATION: " + Math.Round(want, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u00b0";
            bool ok = Near(rot, want, 1e-6) && m.Cursor == "Grabbing" && m.HostCursor == "Hand" && Near(slider, rot, 1e-6) && label == wantLabel;
            turns &= ok;
            td.Append($"pointer {angle}: rotation {rot:0.###}, slider {slider:0.###}, \"{label}\", cursor {m.Cursor}{(ok ? "" : "!")}; ");
        }
        Up(Canvas(cx + radius * Math.Sin(182 * Math.PI / 180), cy - radius * Math.Cos(182 * Math.PI / 180)));
        var r2 = Inspect();
        Record("H2", "the knob drag snaps at 45, wraps past 180 to -178, holds the Grabbing cursor, and the Rotation slider and label follow live in whole degrees; one step",
            turns && r2.Undo == u0 + 2, td + $"undo {r2.Undo}");

        Undo();
        var b1 = Inspect();
        Undo();
        var b2 = Inspect();
        Record("H2", "one Undo per drag", Near(b1.L(id).Rotation, 0) && Near(b1.L(id).Scale, l1.Scale) && Near(b2.L(id).Scale, s0) && b2.Undo == u0,
            $"after one Undo rotation {b1.L(id).Rotation}, after two scale {b2.L(id).Scale:0.#####} (start {s0:0.#####})");
    }

    // ---------------------------------------------------------------- H3: wheel scaling, bursts, the clamp, touchpad deltas

    static void H3()
    {
        // wheel-scale-layer, wheel-sign-only-and-midstroke, wheel-scale-undo-burst, canvas-select-gestures (editor.js:1305-1313)
        Launch();
        AddImage("drip_red_wide_v1.png");
        var s = Inspect();
        var l0 = s.Layers.Single();
        int id = l0.Id;
        double s0 = l0.Scale, cx = l0.Cx, cy = l0.Cy;
        var at = Canvas(cx, cy);

        Wheel(at, 120);
        var a = Inspect().L(id);
        Wheel(at, -120);
        var b = Inspect().L(id);
        Record("H3", "one notch up scales x1.05 about the center; one notch down returns",
            Rel(a.Scale, s0 * 1.05) && Near(a.Cx, cx, 1e-9) && Near(a.Cy, cy, 1e-9) && Rel(b.Scale, s0) && Near(b.Cx, cx, 1e-9),
            $"{s0:0.######} -> {a.Scale:0.######} -> {b.Scale:0.######}; center ({a.Cx:0.###},{a.Cy:0.###})");
        Idle();

        for (int i = 0; i < 10; i++) Wheel(at, -120);
        for (int i = 0; i < 10; i++) Wheel(at, 120);
        var c = Inspect().L(id);
        Record("H3", "ten notches down then ten up return to the start scale about the center (4.1 drifts to 0.9794)",
            Rel(c.Scale, s0, 1e-9) && Near(c.Cx, cx, 1e-9) && Near(c.Cy, cy, 1e-9), $"scale {c.Scale:0.############} (start {s0:0.############})");
        Idle();

        int u1 = Inspect().Undo;
        double before = Inspect().L(id).Scale;
        for (int i = 0; i < 200; i++) Wheel(at, 120);
        var d = Inspect();
        double pct = Range("Scale percent");
        string label = TextStartingWith("SCALE: ");
        Idle();
        int u2 = Inspect().Undo;
        Record("H3", "200 fast notches stop growing at 500% (the panel reads 500%), and the burst is one undo step",
            d.L(id).Scale == 5 && Near(pct, 500, 1e-9) && label == "SCALE: 500%" && u2 == u1 + 1, $"scale {d.L(id).Scale}, slider {pct}, \"{label}\", undo {u1} -> {u2}");
        Undo();
        var e = Inspect();
        Record("H3", "one Undo reverts the whole burst", Rel(e.L(id).Scale, before) && e.Undo == u1, $"scale {e.L(id).Scale:0.######}, undo {e.Undo}");

        double pre = e.L(id).Scale;
        for (int i = 0; i < 5; i++) Wheel(at, 8);
        var f = Inspect().L(id);
        Record("H3", "a precision touchpad's small deltas (five events of +8, 40 in all) step five times by sign, not by distance",
            Rel(f.Scale, pre * Math.Pow(1.05, 5)), $"{pre:0.######} -> {f.Scale:0.######} (x{f.Scale / pre:0.######}; x1.05^5 = {Math.Pow(1.05, 5):0.######})");
        Idle();
        Record("H3", "that burst is one undo step too", Inspect().Undo == u1 + 1, $"undo {Inspect().Undo}");
    }

    // ---------------------------------------------------------------- H4: draw tools, release outside, wheel mid-shape, other buttons

    static void H4()
    {
        // canvas-mouse-button-agnostic, draw-release-outside-canvas, wheel-sign-only-and-midstroke (editor.js:1120-1146, :1239-1302, :1314-1319)
        Launch();
        Tool("Draw");
        var s0 = Inspect();
        Down(Canvas(100, 300));
        Move(Canvas(120, 310));
        Move(Canvas(140, 320));
        Up(Canvas(160, 330));
        var a = Inspect();
        var pen = a.Layers.LastOrDefault();
        Record("H4", "a pen stroke becomes one selected layer named \"Pen drawing\" and the Draw tool stays",
            a.Layers.Count == s0.Layers.Count + 1 && pen?.Name == "Pen drawing" && a.Selected == pen.Id && a.Tool == "Pen" && a.Undo == s0.Undo + 1,
            $"{a.Layers.Count} layers, last \"{pen?.Name}\", selected {a.Selected}, tool {a.Tool}, undo {a.Undo}");

        Down(Canvas(200, 420));
        Up(Canvas(200, 420));
        var b = Inspect();
        var dot = b.Layers.Last();
        Record("H4", "a pen click draws a brush-size dot", b.Layers.Count == a.Layers.Count + 1 && dot.Name == "Pen drawing" && dot.W <= 2 * b.Brush + 4 && dot.H <= 2 * b.Brush + 4,
            $"dot {dot.W}x{dot.H} at brush {b.Brush}");

        Tool("Rect");
        var fill = (TogglePattern)Find("Fill shapes", "CheckBox").GetCurrentPattern(TogglePattern.Pattern);
        if (fill.Current.ToggleState != ToggleState.On) fill.Toggle();
        int brush0 = (int)Range("Brush size");
        var rA = Shape((250, 150), (310, 190), wheelNotches: 0);
        var rB = Shape((250, 150), (310, 190), wheelNotches: 3);
        int brush1 = (int)Range("Brush size");
        var rC = Shape((250, 150), (310, 190), wheelNotches: 0);
        Record("H4", "three wheel notches during a Rect drag widen the live shape: it commits at the new width, the same as a Rect drawn at that width",
            brush1 == brush0 + 3 && rA.Name == "Rect drawing" && rB.Name == "Rect drawing" && rB.W == rC.W && rB.H == rC.H && rB.W > rA.W,
            $"brush {brush0} -> {brush1}; A {rA.W}x{rA.H}, B (wheeled) {rB.W}x{rB.H}, C (at {brush1}) {rC.W}x{rC.H}");
        SetRange("Brush size", brush0);

        Tool("Ellipse");
        var el = Shape((330, 150), (390, 200), wheelNotches: 0);
        Record("H4", "an Ellipse stroke becomes \"Circle drawing\"", el.Name == "Circle drawing", el.Name);

        Tool("Line");
        var o = OutsideWindow();
        var c0 = Inspect();
        Down(Canvas(300, 300));
        Move(Canvas(350, 320));
        Up(Canvas(o.X, o.Y));
        var c1 = Inspect();
        Record("H4", "a Line released outside the window is committed as \"Line drawing\"",
            c1.Layers.Count == c0.Layers.Count + 1 && c1.Layers.Last().Name == "Line drawing" && c1.Gesture == "None" && !c1.Captured && c1.Undo == c0.Undo + 1,
            $"release at template ({o.X:0.#},{o.Y:0.#}); {c1.Layers.Count} layers, last \"{c1.Layers.Last().Name}\" {c1.Layers.Last().W}x{c1.Layers.Last().H}");
        Move(Canvas(320, 310));
        Move(Canvas(340, 330));
        var c2 = Inspect();
        Record("H4", "re-entering the canvas with the button up draws nothing (4.1 resumed a phantom stroke)",
            c2.Layers.Count == c1.Layers.Count && c2.Gesture == "None" && c2.Undo == c1.Undo, $"{c2.Layers.Count} layers, gesture {c2.Gesture}");

        Tool("Draw");
        var d0 = Inspect();
        foreach (var button in new[] { "right", "middle" })
        {
            Down(Canvas(100, 100), button);
            Move(Canvas(150, 150));
            Up(Canvas(200, 200), button);
        }
        var d1 = Inspect();
        Record("H4", "right and middle drags with Draw add nothing", d1.Layers.Count == d0.Layers.Count && d1.Undo == d0.Undo && d1.Gesture == "None",
            $"{d0.Layers.Count} -> {d1.Layers.Count} layers, undo {d0.Undo} -> {d1.Undo}");

        Tool("Select");
        var sel = Inspect();
        var last = sel.L(sel.Selected ?? throw new Exception("nothing selected"));
        Down(Canvas(last.Cx, last.Cy), "right");
        Move(Canvas(last.Cx + 40, last.Cy + 20));
        Up(Canvas(last.Cx + 40, last.Cy + 20), "right");
        var after = Inspect().L(last.Id);
        Record("H4", "a right drag on the selected layer with Select moves nothing", Near(after.X, last.X) && Near(after.Y, last.Y), $"({after.X - last.X:0.###},{after.Y - last.Y:0.###})");
    }

    /// <summary>Draws one shape with the current tool from a to b, rolling the wheel up mid-drag; returns the new layer.</summary>
    static LayerSt Shape((double X, double Y) a, (double X, double Y) b, int wheelNotches)
    {
        var before = Inspect();
        Down(Canvas(a.X, a.Y));
        var mid = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        Move(Canvas(mid.Item1, mid.Item2));
        for (int i = 0; i < wheelNotches; i++) Wheel(Canvas(mid.Item1, mid.Item2), 120);
        Up(Canvas(b.X, b.Y));
        var after = Inspect();
        if (after.Layers.Count != before.Layers.Count + 1) throw new Exception($"the shape added {after.Layers.Count - before.Layers.Count} layers");
        return after.Layers.Last();
    }

    // ---------------------------------------------------------------- H5: eraser ring, refusal, click, stroke

    static void H5()
    {
        // tools-cursor, cursor-eraser, eraser-cursor, render-restore-and-cursor, cursor-default, eraser-click-without-move
        // (editor.js:440-459, :1133-1144, :1265-1283)
        Launch();
        Tool("Eraser");
        Down(Canvas(300, 300));
        Up(Canvas(300, 300));
        var r = Inspect();
        string status = Status();
        Record("H5", "with nothing selected a press refuses in the status strip and changes nothing",
            status == "Select a layer to erase from, then try again." && r.Undo == 0 && r.Layers.Count == 0, $"\"{status}\", undo {r.Undo}");

        bool rings = true;
        var rd = new StringBuilder();
        foreach (double zoom in new[] { 1.0, 2.0, 3.0, 0.25 })
        {
            ZoomTo(zoom);
            foreach (int size in new[] { 4, 50 })
            {
                SetRange("Brush size", size);
                var m = Inspect();
                double want = Math.Min(Math.Max(4, size * zoom / 2) * S, 128 - Math.Ceiling(2 * S));
                bool ok = m.Cursor == "EraserRing" && m.Ring == size && Near(m.RingRadiusPx, want, 1e-9);
                rings &= ok;
                rd.Append($"zoom {zoom} size {size}: ring {m.Ring}, radius {m.RingRadiusPx} px (want {want}){(ok ? "" : "!")}; ");
            }
        }
        Record("H5", "the ring is the brush footprint, radius max(4, size x zoom / 2) DIPs at sizes 4 and 50 and zoom 25-300%, rebuilt as soon as size or zoom changes, with no hover",
            rings, rd.ToString());
        ZoomTo(1);
        SetRange("Brush size", 4);

        bool cross = true;
        var cd = new StringBuilder();
        int i = 0;
        foreach (var t in new[] { "Draw", "Line", "Rect", "Ellipse" })
        {
            Tool(t);
            Move(Canvas(100 + 10 * i++, 100));
            var m = Inspect();
            bool ok = m.Cursor == "Crosshair" && m.HostCursor == "Cross";
            cross &= ok;
            cd.Append($"{t} {m.Cursor}/{m.HostCursor}; ");
        }
        Record("H5", "Draw, Line, Rect and Ellipse hover with the crosshair", cross, cd.ToString());

        Tool("Select");
        AddImage("drip_red_wide_v1.png");
        AddImage("drip_blue_wide_v1.png");
        Select(Row("drip_red_wide_v1"));
        Tool("Eraser");
        var e0 = Inspect();
        var red = e0.Layers.First();
        var blue = e0.Layers.Last();
        Down(Canvas(red.Cx, red.Cy));
        Up(Canvas(red.Cx, red.Cy));
        var e1 = Inspect();
        Record("H5", "an eraser click without movement erases nothing and records no step", e1.L(red.Id).Image == red.Image && e1.Undo == e0.Undo, $"undo {e0.Undo} -> {e1.Undo}");
        Down(Canvas(red.Cx - 30, red.Cy));
        Move(Canvas(red.Cx, red.Cy + 5));
        Up(Canvas(red.Cx + 30, red.Cy));
        var e2 = Inspect();
        Record("H5", "a stroke erases only the selected (lower) layer, as one step",
            e2.L(red.Id).Image != red.Image && e2.L(blue.Id).Image == blue.Image && e2.Undo == e0.Undo + 1, $"red image {red.Image} -> {e2.L(red.Id).Image}, blue unchanged {e2.L(blue.Id).Image == blue.Image}");
        Undo();
        Record("H5", "one Undo restores the exact image", Inspect().L(red.Id).Image == red.Image, "");
    }

    // ---------------------------------------------------------------- H7: row drag, drop outside, Alt+Up, Ctrl+Z

    static void H7()
    {
        // layers-reorder-drag, layers-reorder-drag-2, layer-reorder-dragging-class-timer (editor.js:584-666, :738-741)
        Launch();
        foreach (var f in new[] { "drip_red_wide_v1.png", "drip_blue_wide_v1.png", "drip_gold_wide_v1.png", "drip_green_wide_v1.png" }) AddImage(f);
        const string red = "drip_red_wide_v1", blue = "drip_blue_wide_v1", gold = "drip_gold_wide_v1", green = "drip_green_wide_v1";
        int u0 = Inspect().Undo;
        Record("H7", "four layers list top first", RowNames().SequenceEqual(new[] { green, gold, blue, red }), string.Join(", ", RowNames()));

        Down(RowPoint(Row(red), 0.5));
        Move(RowPoint(Row(red), 0.5, -12));
        Move(RowPoint(Row(green), 0.25));
        var (dragging, rows) = InspectLayers();
        Shot("h7-mid-drag");
        bool marks = dragging && rows.Single(x => x.Name == red).DragSource && rows.Single(x => x.Name == green).Drop == "Above"
            && rows.Where(x => x.Name != green).All(x => x.Drop == "None") && rows.Where(x => x.Name != red).All(x => !x.DragSource);
        Record("H7", "mid-drag the source row is marked (dimmed) and the drop line shows above the top row", marks,
            string.Join("; ", rows.Select(x => $"{x.Name} source={x.DragSource} drop={x.Drop}")));
        Up(RowPoint(Row(green), 0.25));
        var (d1, rows1) = InspectLayers();
        Record("H7", "the drop on the top half of the top row makes it the top layer, as one step, and clears every mark",
            RowNames().SequenceEqual(new[] { red, green, gold, blue }) && Inspect().Undo == u0 + 1 && !d1 && rows1.All(x => !x.DragSource && x.Drop == "None"),
            string.Join(", ", RowNames()));

        var list = Find("id:LayerList", null).Current.BoundingRectangle;
        Down(RowPoint(Row(gold), 0.5));
        Move(RowPoint(Row(gold), 0.5, -12));
        var below = Elem("LayerList", 20, list.Height / S + 6);
        Move(below);
        Up(below);
        var (d2, rows2) = InspectLayers();
        Record("H7", "a drag released below the list, off every row, moves nothing, records nothing and leaves no mark",
            RowNames().SequenceEqual(new[] { red, green, gold, blue }) && Inspect().Undo == u0 + 1 && !d2 && rows2.All(x => !x.DragSource && x.Drop == "None"),
            string.Join(", ", RowNames()));

        var blueRow = Row(blue);
        blueRow.FocusOn();
        Key("Up", "Alt");
        Record("H7", "Alt+Up on the focused row moves it one place, as one step, and focus stays on it",
            RowNames().SequenceEqual(new[] { red, green, blue, gold }) && Inspect().Undo == u0 + 2 && Row(blue).Current.HasKeyboardFocus,
            string.Join(", ", RowNames()));

        Key("Z", "Ctrl");
        var z1 = RowNames();
        Key("Z", "Ctrl");
        var z2 = RowNames();
        Record("H7", "one Ctrl+Z (the window's binding, through the bridge) undoes each reorder",
            z1.SequenceEqual(new[] { red, green, gold, blue }) && z2.SequenceEqual(new[] { green, gold, blue, red }) && Inspect().Undo == u0,
            $"{string.Join(", ", z1)} | {string.Join(", ", z2)}");
    }

    // ---------------------------------------------------------------- H7b: Esc mid-drag, wheel the panel mid-drag

    static void H7b()
    {
        // layers-reorder-drag-2 (editor.js:584-666; M3b Task 4 M3, M4)
        Launch();
        AddImage("drip_red_wide_v1.png");
        AddImage("drip_blue_wide_v1.png");
        var start = RowNames();
        int u0 = Inspect().Undo;
        Down(RowPoint(Row("drip_blue_wide_v1"), 0.5));
        Move(RowPoint(Row("drip_red_wide_v1"), 0.75));
        bool wasDragging = InspectLayers().Dragging;
        Key("Escape");
        var (d, rows) = InspectLayers();
        Up(RowPoint(Row("drip_red_wide_v1"), 0.75));
        Record("H7b", "Esc mid-drag cancels: nothing moves, no row stays marked, and the release after it does nothing",
            wasDragging && !d && rows.All(x => !x.DragSource && x.Drop == "None") && RowNames().SequenceEqual(start) && Inspect().Undo == u0,
            $"dragging before Esc {wasDragging}, after {d}; {string.Join(", ", RowNames())}");

        for (int i = 0; i < 18; i++) Invoke(Find("Duplicate layer", "Button"));
        var all = InspectLayers().Rows;
        PanelScroll().SetScrollPercent(ScrollPattern.NoScroll, 0);
        Settle();
        int topId = all[0].Id, u1 = Inspect().Undo;
        var items = RowElements();
        Down(RowPoint(items[0], 0.5));
        Move(RowPoint(items[0], 0.5, 12));
        double scrolled = 0;
        for (int i = 0; i < 40 && scrolled < 99.5; i++)
        {
            Wheel(Elem("LayerList", 20, 20), -120);
            scrolled = PanelScroll().Current.VerticalScrollPercent;
        }
        var lastRow = RowElements().Last();
        Move(RowPoint(lastRow, 0.75));
        Up(RowPoint(lastRow, 0.75));
        var after = InspectLayers().Rows;
        Record("H7b", "with 20 layers, the left panel wheels down mid-drag and the top row drops below the last row, as one step",
            all.Count == 20 && scrolled > 0 && after.Last().Id == topId && Inspect().Undo == u1 + 1,
            $"{all.Count} rows; scrolled to {scrolled:0.#}%; bottom row id {after.Last().Id} (dragged {topId})");
        Key("Z", "Ctrl");
        Record("H7b", "one Ctrl+Z puts it back on top", InspectLayers().Rows[0].Id == topId && Inspect().Undo == u1, "");
    }

    // ---------------------------------------------------------------- H10: the themed picker, live, click outside, Ctrl+Z

    static void H10()
    {
        // fillstyle-color-pickers (editor.html Color 1 input; M3a themed picker ruling, 2026-10-14)
        Launch();
        Invoke(Find("Torso Front", "Button"));
        var s = Inspect();
        string f0 = s.FillC1.GetValueOrDefault("torso_front");
        int u0 = s.Undo;
        Invoke(Find("Color 1", "Button"), settle: false);
        var popup = WaitWindow(e => e.Current.Name == "Color picker", 10000);
        Down(Elem("SvSquare", 20, 20));
        Move(Elem("SvSquare", 180, 30));
        string fa = Inspect().FillC1["torso_front"];
        Move(Elem("SvSquare", 150, 120));
        string fb = Inspect().FillC1["torso_front"];
        Up(Elem("SvSquare", 150, 120));
        Record("H10", "dragging in the square repaints Front live", f0 != null && fa != f0 && fb != fa, $"{f0} -> {fa} -> {fb}");
        Down(Elem("HueStrip", 10, 7));
        Move(Elem("HueStrip", 120, 7));
        Up(Elem("HueStrip", 120, 7));
        string fc = Inspect().FillC1["torso_front"];
        Record("H10", "dragging along the hue strip repaints Front live", fc != fb, $"{fb} -> {fc}");
        ShotOf(new IntPtr(popup.Current.NativeWindowHandle), "h10-picker");

        // Windows tells a disabled owner about a press only (WM_SETCURSOR, HTERROR, WM_LBUTTONDOWN); the release
        // goes to the canvas once the popup has closed and is a stray release, which does nothing.
        var down = Ptr("down", Canvas(300, 300), "left");
        bool closed = WaitGone(() => AppWindows().Any(h => SafeName(h) == "Color picker"), 5000);
        Ptr("up", Canvas(300, 300), "left");
        Idle();
        var m = Inspect();
        bool owner = down["ownerClick"]?.GetValue<bool>() == true && down["handled"]?.GetValue<bool>() == true;
        Record("H10", "a click on the canvas through the disabled owner commits the pick as one step, handled so Windows does not beep",
            owner && closed && m.Undo == u0 + 1 && m.FillC1["torso_front"] == fc, $"pointer result {down.ToJsonString()}; popup closed {closed}; undo {u0} -> {m.Undo}; Front {m.FillC1["torso_front"]}");
        Key("Z", "Ctrl");
        var z = Inspect();
        Record("H10", "one Ctrl+Z reverts the pick", z.FillC1["torso_front"] == f0 && z.Undo == u0, $"Front {z.FillC1["torso_front"]}, undo {z.Undo}");
    }

    // ---------------------------------------------------------------- F1: save, Save As, export, open, the prompt, failures, chords, buttons

    static void F1()
    {
        // home-file-group, project-save-filename, io-download-save-dialog, project-load-history-interplay,
        // window-title-dirty-marker, io-alert-inventory (editor.js:2550-2591, :2594-2690; editor.html:85-97)
        string dir = Fresh("files");
        string p1 = Path.Combine(dir, "smoke1.r15proj"), p2 = Path.Combine(dir, "smoke2.r15proj"), png = Path.Combine(dir, "smoke2-v5.png");
        Launch();
        Record("F1", "at launch the title is the product name", Title() == Product, Title());
        SetValue("Template name", "My Shirt v2!");
        AddImage("drip_red_wide_v1.png");
        Record("F1", "an edit marks the title", Title() == "*Untitled - " + Product, Title());

        int dl = B.DialogLines.Count;
        Answer(p1);
        Key("S", "Ctrl");
        Idle();
        var lines = B.DialogLines.Skip(dl).ToList();
        string json = File.Exists(p1) ? File.ReadAllText(p1) : "";
        Record("F1", "Ctrl+S on an untitled document runs Save As with the sanitized template name, writes version 5.0 without a title and no temp file, and marks the title clean",
            lines.Count == 1 && lines[0].StartsWith(Bridge.DialogPrefix + "SaveProject My_Shirt_v2_.r15proj", StringComparison.Ordinal) && json.Contains("\"version\": \"5.0\"")
            && !json.Contains("\"title\"") && Directory.GetFiles(dir, "*.tmp").Length == 0 && Title() == "smoke1.r15proj - " + Product && Status() == "Saved smoke1.r15proj.",
            $"{string.Join(" | ", lines)}; title \"{Title()}\"; status \"{Status()}\"");

        var l0 = Inspect().Layers.Single();
        DragCanvas((l0.Cx, l0.Cy), (l0.Cx + 40, l0.Cy + 20));
        string t1 = Title();
        Key("Z", "Ctrl");
        string t2 = Title();
        Record("F1", "an edit marks the title, and Ctrl+Z back to the saved state reads clean", t1 == "*smoke1.r15proj - " + Product && t2 == "smoke1.r15proj - " + Product, $"{t1} | {t2}");

        DragCanvas((l0.Cx, l0.Cy), (l0.Cx + 40, l0.Cy + 20));
        dl = B.DialogLines.Count;
        Answer(p2);
        Key("S", "Ctrl", "Shift");
        Idle();
        lines = B.DialogLines.Skip(dl).ToList();
        Record("F1", "Ctrl+Shift+S saves under a new name, suggesting the current file's name",
            lines.Count == 1 && lines[0].StartsWith(Bridge.DialogPrefix + "SaveProject smoke1.r15proj", StringComparison.Ordinal) && File.Exists(p2) && Title() == "smoke2.r15proj - " + Product,
            $"{string.Join(" | ", lines)}; title \"{Title()}\"");

        dl = B.DialogLines.Count;
        Answer(png);
        Invoke(Find("Download template", "Button"));
        Idle();
        lines = B.DialogLines.Skip(dl).ToList();
        var (pw, ph) = File.Exists(png) ? PngSize(png) : (0, 0);
        Record("F1", "Download template suggests My_Shirt_v2__roblox.png and writes a 585x559 RGBA PNG (the v5 half of the 4.1 cross-check)",
            lines.Count == 1 && lines[0].StartsWith(Bridge.DialogPrefix + "SavePng My_Shirt_v2__roblox.png", StringComparison.Ordinal) && pw == 585 && ph == 559 && PngColorType(png) == 6,
            $"{string.Join(" | ", lines)}; {pw}x{ph}");
        int files = Directory.GetFiles(dir).Length;
        dl = B.DialogLines.Count;
        Invoke(Find("Download template", "Button"));
        Idle();
        Record("F1", "a cancelled Download writes nothing and shows no dialog",
            Directory.GetFiles(dir).Length == files && B.DialogLines.Count == dl + 1 && OtherWindows().Count == 0, $"{Directory.GetFiles(dir).Length} files");

        DragCanvas((l0.Cx + 40, l0.Cy + 20), (l0.Cx + 70, l0.Cy + 30));
        double dirtyX = Inspect().Layers.Single().X;
        Answer(p1);
        Key("O", "Ctrl");
        var prompt = Dialog("Do you want to save changes to smoke2.r15proj?");
        bool themed = prompt.Current.Name == Product && Find(prompt, "id:DontSaveButton").Current.Name == "Don't save" && Find(prompt, "id:OkButton").Current.Name == "Save";
        Press(prompt, "CancelButton");
        Idle();
        Record("F1", "Open with unsaved edits asks first (themed, product title, Save / Don't save / Cancel); Cancel changes nothing",
            themed && Near(Inspect().Layers.Single().X, dirtyX) && Title() == "*smoke2.r15proj - " + Product, $"title \"{Title()}\"");
        Answer(p1);
        Key("O", "Ctrl");
        Press(Dialog("Do you want to save changes to smoke2.r15proj?"), "DontSaveButton");
        Idle();
        var o = Inspect();
        var lo = o.Layers.Single();
        Record("F1", "Don't save opens the file: its layer where it was saved, an empty history, no selection, a clean title, the template name untouched",
            Near(lo.X, l0.X) && Near(lo.Y, l0.Y) && o.Undo == 0 && o.Redo == 0 && o.Selected == null && Title() == "smoke1.r15proj - " + Product
            && Value("Template name") == "My Shirt v2!" && !Find("id:UndoButton", null).Current.IsEnabled,
            $"layer ({lo.X:0.##},{lo.Y:0.##}) saved at ({l0.X:0.##},{l0.Y:0.##}); undo {o.Undo}; selected {o.Selected}; title \"{Title()}\"; name \"{Value("Template name")}\"");
        Key("Z", "Ctrl");
        Record("F1", "Ctrl+Z right after Open changes nothing", Near(Inspect().Layers.Single().X, lo.X) && Inspect().Undo == 0, "");

        DragCanvas((lo.Cx, lo.Cy), (lo.Cx + 20, lo.Cy + 10));
        double keepX = Inspect().Layers.Single().X;
        string bad = Path.Combine(dir, "bad.r15proj"), trunc = Path.Combine(dir, "trunc.r15proj");
        File.WriteAllText(bad, "not json");
        File.WriteAllText(trunc, "{\"version\": \"4.0\", \"layers\": [{\"id\": 1,");
        foreach (var f in new[] { bad, trunc })
        {
            Answer(f);
            Key("O", "Ctrl");
            var alert = Dialog("Failed to load project. ", 15000);
            string text = Find(alert, "id:MessageText").Current.Name;
            bool prompted = OtherWindows().Any(h => Text(h).StartsWith("Do you want to save", StringComparison.Ordinal));
            Press(alert, "OkButton");
            Idle();
            Record("F1", $"opening {Path.GetFileName(f)} alerts in the themed dialog and changes nothing, with no save prompt first",
                alert.Current.Name == Product && !prompted && Near(Inspect().Layers.Single().X, keepX) && Title() == "*smoke1.r15proj - " + Product, $"\"{text}\"");
        }

        var t = File.GetLastWriteTimeUtc(p1);
        Find("Template name", "Edit").FocusOn();
        dl = B.DialogLines.Count;
        Key("S", "Ctrl");
        Idle();
        Record("F1", "Ctrl+S while the template name has focus does nothing (the text-field guard)",
            B.DialogLines.Count == dl && File.GetLastWriteTimeUtc(p1) == t && Title() == "*smoke1.r15proj - " + Product, Title());
        Find("id:UndoButton", null).FocusOn();
        Key("S", "Ctrl");
        Idle();
        Record("F1", "Ctrl+S elsewhere saves to the current file without a dialog",
            B.DialogLines.Count == dl && File.GetLastWriteTimeUtc(p1) > t && Title() == "smoke1.r15proj - " + Product && Status() == "Saved smoke1.r15proj.", $"{Title()}; \"{Status()}\"");

        var lb = Inspect().Layers.Single();
        DragCanvas((lb.Cx, lb.Cy), (lb.Cx + 10, lb.Cy + 10));
        Invoke(Find("Save work", "Button"));
        Idle();
        Record("F1", "the Save work button saves to the current file", Title() == "smoke1.r15proj - " + Product, Title());
        dl = B.DialogLines.Count;
        Invoke(Find("Open", "Button"));
        Idle();
        Record("F1", "the Open button shows the project dialog, and a cancel changes nothing",
            B.DialogLines.Skip(dl).Any(l => l.StartsWith(Bridge.DialogPrefix + "OpenProject", StringComparison.Ordinal)) && Title() == "smoke1.r15proj - " + Product,
            string.Join(" | ", B.DialogLines.Skip(dl)));
        Shot("f1-end");
    }

    // ---------------------------------------------------------------- F2: the close prompt

    static void F2()
    {
        // shell-unsaved-work-loss, shell-no-unsaved-changes-guard (R13)
        string dir = Fresh("close");
        string p = Path.Combine(dir, "close.r15proj");
        Launch();
        AddImage("drip_red_wide_v1.png");
        Answer(p);
        Key("S", "Ctrl");
        Idle();
        var l = Inspect().Layers.Single();
        DragCanvas((l.Cx, l.Cy), (l.Cx + 30, l.Cy + 10));
        var closing = CloseAsync();
        Press(Dialog("Do you want to save changes to close.r15proj?"), "CancelButton");
        closing.Wait(10000);
        Thread.Sleep(800);
        Record("F2", "closing with edits asks; Cancel keeps the window and the edits", !B.Proc.HasExited && Title() == "*close.r15proj - " + Product, Title());
        var t = File.GetLastWriteTimeUtc(p);
        closing = CloseAsync();
        Press(Dialog("Do you want to save changes to close.r15proj?"), "OkButton");
        bool exited = B.Proc.WaitForExit(10000);
        Record("F2", "Save on close saves to the current file, then closes", exited && File.GetLastWriteTimeUtc(p) > t && B.Proc.ExitCode == 0, $"exited {exited}");
        Done();

        Launch();
        AddImage("drip_red_wide_v1.png");
        int dl = B.DialogLines.Count;
        closing = CloseAsync();
        Press(Dialog("Do you want to save changes to Untitled?"), "OkButton");
        closing.Wait(10000);
        Thread.Sleep(1000);
        Record("F2", "Save on an untitled document runs Save As, and cancelling it cancels the close",
            !B.Proc.HasExited && B.DialogLines.Skip(dl).Any(x => x.StartsWith(Bridge.DialogPrefix + "SaveProject", StringComparison.Ordinal)) && Title() == "*Untitled - " + Product,
            string.Join(" | ", B.DialogLines.Skip(dl)));
        int count = Directory.GetFiles(dir).Length;
        closing = CloseAsync();
        Press(Dialog("Do you want to save changes to Untitled?"), "DontSaveButton");
        exited = B.Proc.WaitForExit(10000);
        Record("F2", "Don't save closes without writing", exited && Directory.GetFiles(dir).Length == count, $"exited {exited}");
        Done();

        Launch();
        closing = CloseAsync();
        bool prompt = false;
        var until = DateTime.UtcNow.AddSeconds(8);
        while (!B.Proc.HasExited && DateTime.UtcNow < until)
        {
            prompt |= OtherWindows().Any(h => Text(h).StartsWith("Do you want to save", StringComparison.Ordinal));
            Thread.Sleep(200);
        }
        Record("F2", "a window with no edits closes at once, with no prompt", B.Proc.HasExited && !prompt, $"exited {B.Proc.HasExited}");
        Done();
    }

    // ---------------------------------------------------------------- F3: the template name is not saved

    static void F3()
    {
        // project-template-name-not-saved (keep; editor.js:2552-2582, :1536-1537; interfaces R1)
        string dir = Fresh("title");
        string p = Path.Combine(dir, "abc.r15proj");
        Launch();
        SetValue("Template name", "abc");
        AddImage("drip_red_wide_v1.png");
        Answer(p);
        Key("S", "Ctrl");
        Idle();
        StopApp();
        Launch();
        Answer(p);
        Key("O", "Ctrl");
        Idle();
        Record("F3", "a project saved with the template name \"abc\" reopens in a fresh window with the name box blank (4.1 never stored it)",
            Value("Template name") == "" && Inspect().Layers.Count == 1 && !File.ReadAllText(p).Contains("\"title\""), $"name \"{Value("Template name")}\"");
    }

    // ---------------------------------------------------------------- F4: the dialog inventory, live

    static void F4()
    {
        // dialogs-inventory, io-alert-inventory, packs-resolve-dir, diag-console-only-errors, file-input-same-file-reselect
        string dir = Fresh("dialogs");
        Launch();
        Invoke(Find("Download template", "Button"), settle: false);
        var c = Dialog("The template is empty");
        string ct = Find(c, "id:MessageText").Current.Name;
        Press(c, "CancelButton");
        Record("F4", "editor.js:1506 the empty-template confirm, verbatim, themed and titled with the product name",
            ct == "The template is empty \u2014 download a blank PNG anyway?" && c.Current.Name == Product, $"\"{ct}\"");

        Tool("Eraser");
        Down(Canvas(300, 300));
        Up(Canvas(300, 300));
        Record("F4", "editor.js:1270 the eraser refusal, in the status strip", Status() == "Select a layer to erase from, then try again.", $"\"{Status()}\"");
        Tool("Select");

        string empty = Path.Combine(dir, "empty-folder");
        Directory.CreateDirectory(empty);
        Answer(empty);
        Invoke(Find("Open folder", "Button"), settle: false);
        var n = Dialog("No images found");
        string nt = Find(n, "id:MessageText").Current.Name;
        Press(n, "OkButton");
        Record("F4", "editor.js:2385 no images in the folder, verbatim", nt == "No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files." && n.Current.Name == Product, $"\"{nt}\"");

        string packs = Path.Combine(Path.GetDirectoryName(Exe), "packs"), drips = Path.Combine(packs, "drips"), parked = drips + ".m4a-off";
        try
        {
            Directory.Move(drips, parked);
            Invoke(Find("Drips", "Button"), settle: false);
            var m = Dialog("Built-in pack");
            string mt = Find(m, "id:MessageText").Current.Name;
            Press(m, "OkButton");
            Record("F4", "editor.js:2344 a missing built-in pack, verbatim", mt == "Built-in pack \"drips\" not found. Reinstall the app to restore it." && m.Current.Name == Product, $"\"{mt}\"");
            Directory.CreateDirectory(drips);
            Invoke(Find("Drips", "Button"), settle: false);
            var e = Dialog("Built-in pack");
            string et = Find(e, "id:MessageText").Current.Name;
            Press(e, "OkButton");
            Record("F4", "editor.js:2364 an empty built-in pack, verbatim", et == "Built-in pack \"drips\" is empty." && e.Current.Name == Product, $"\"{et}\"");
        }
        finally
        {
            if (Directory.Exists(parked))
            {
                if (Directory.Exists(drips) && Directory.GetFileSystemEntries(drips).Length == 0) Directory.Delete(drips);
                Directory.Move(parked, drips);
            }
        }
        Record("F4", "the build's packs folder is restored", Directory.GetFiles(drips, "*.png").Length == 156, $"{Directory.GetFiles(drips, "*.png").Length} drips");

        string fake = Path.Combine(dir, "not-an-image.png");
        File.WriteAllText(fake, "hello");
        Answer(fake);
        Invoke(Find("Add image", "Button"));
        Idle();
        Record("F4", "a text file renamed .png fails through Add image with a status message, no layer and no dialog (4.1: console only)",
            Status().StartsWith("Couldn't add not-an-image.png: ", StringComparison.Ordinal) && Inspect().Layers.Count == 0 && OtherWindows().Count == 0, $"\"{Status()}\"");

        string red = Pack("drip_red_wide_v1.png");
        var zone = Find("Drop art here. Drag and drop images, or press to browse.", "Button");
        Answer(red);
        Invoke(zone);
        Idle();
        Answer(red);
        Invoke(zone);
        Idle();
        Record("F4", "picking the same file twice through the drop zone adds it twice", Inspect().Layers.Count == 2, $"{Inspect().Layers.Count} layers");
    }

    // ---------------------------------------------------------------- A1: packs, click, right-click, drag to canvas, resize

    static void A1()
    {
        // packs-load-builtin, pack-builtin-load, packs-inventory-*, asset-click-add, asset-rightclick-full,
        // asset-drag-to-canvas, asset-resizable-browser, panel-asset-resize-handle, panel-asset-library
        // (editor.js:2340-2378, :2494-2539, :2716-2742)
        Launch();
        Invoke(Find("Drips", "Button"));
        Idle();
        string drips = WaitText(@"^\d+ images in built-in drips pack$");
        var stems = Directory.GetFiles(Path.Combine(Repo, "packs", "drips"), "*.png").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var shown = Tiles();
        Shot("a1-drips");
        Record("A1", "Drips: \"156 images in built-in drips pack\"; the realized tiles are the first ones in ordinal-ignore-case order, three per row, and the grid is virtualized",
            drips == "156 images in built-in drips pack" && shown.Count >= 6 && shown.Count < 156 && shown.Select(x => x.Current.Name).SequenceEqual(stems.Take(shown.Count))
            && RowsOf(shown).Take(RowsOf(shown).Count - 1).All(r => r == 3),
            $"\"{drips}\"; {shown.Count} realized: {string.Join(", ", shown.Take(4).Select(x => x.Current.Name))}...");
        Invoke(Find("Word art", "Button"));
        Idle();
        string words = WaitText(@"^\d+ images in built-in word_art pack$");
        Record("A1", "Word art: \"25 images in built-in word_art pack\"", words == "25 images in built-in word_art pack", $"\"{words}\"");
        Invoke(Find("Drips", "Button"));
        Idle();

        var tile = Tiles().First();
        string stem = tile.Current.Name;
        var (w, h) = PngSize(Pack(stem + ".png"));
        int u0 = Inspect().Undo;
        Invoke(tile);   // a tile is a Button: the bridge pointer cannot click a ButtonBase (T4-R4), UIA Invoke is the click (T2-R2)
        Idle();
        var a = Inspect();
        var la = a.Layers.Last();
        double fit = Math.Min(128.0 / w, 128.0 / h) * 0.85;
        Record("A1", "a click (UIA Invoke, T4-R4) adds the tile as one layer fit at 85% in the front slot (231,74,128,128), selected",
            la.Name == stem && Near(la.Scale, fit, 1e-12) && Near(la.X, 231 + (128 - w * fit) / 2, 1e-9) && Near(la.Y, 74 + (128 - h * fit) / 2, 1e-9) && a.Selected == la.Id && a.Undo == u0 + 1,
            $"\"{la.Name}\" scale {la.Scale:0.######} (want {fit:0.######}) at ({la.X:0.###},{la.Y:0.###})");
        Down(AtCenter(tile, "AssetGrid"), "right");
        Up(AtCenter(tile, "AssetGrid"), "right");
        var b = Inspect();
        var lb = b.Layers.Last();
        Record("A1", "a right-click adds it at full template size, max(585/w, 559/h) at (0,0), named \"<name> (full)\", with no context menu",
            lb.Name == stem + " (full)" && lb.X == 0 && lb.Y == 0 && Near(lb.Scale, Math.Max(585.0 / w, 559.0 / h), 1e-12) && b.Undo == u0 + 2 && !AnyMenu(),
            $"\"{lb.Name}\" scale {lb.Scale:0.######} at ({lb.X},{lb.Y})");

        foreach (double zoom in new[] { 1.0, 2.0 })
        {
            ZoomTo(zoom);
            var before = Inspect();
            tile = Tiles().First();
            Down(AtCenter(tile, "AssetGrid"));
            Move(AtCenter(tile, "AssetGrid", 12));
            Move(Canvas(300, 200));
            Up(Canvas(300, 200));
            var after = Inspect();
            var ld = after.Layers.Last();
            Record("A1", $"a thumbnail dragged onto the canvas at zoom {zoom * 100}% lands centered on the drop point (300,200), as one step",
                after.Layers.Count == before.Layers.Count + 1 && Near(ld.Cx, 300, 1e-9) && Near(ld.Cy, 200, 1e-9) && after.Undo == before.Undo + 1,
                $"center ({ld.Cx:0.###},{ld.Cy:0.###})");
        }
        ZoomTo(1);
        var d0 = Inspect();
        Undo();
        var d1 = Inspect();
        Record("A1", "one Undo removes the dropped layer", d1.Layers.Count == d0.Layers.Count - 1 && d1.Undo == d0.Undo - 1, $"{d0.Layers.Count} -> {d1.Layers.Count} layers");
        var c0 = Inspect();
        tile = Tiles().First();
        Down(AtCenter(tile, "AssetGrid"));
        Move(AtCenter(tile, "AssetGrid", 12));
        Move(Elem("LayerList", 20, 10));
        Up(Elem("LayerList", 20, 10));
        Down(AtCenter(tile, "AssetGrid"));
        Move(AtCenter(tile, "AssetGrid", 12));
        Move(Canvas(300, 200));
        Key("Escape");
        Up(Canvas(300, 200));
        var c1 = Inspect();
        Record("A1", "a drag released over the layers list, or cancelled with Esc over the canvas, adds nothing", c1.Layers.Count == c0.Layers.Count && c1.Undo == c0.Undo,
            $"{c0.Layers.Count} -> {c1.Layers.Count} layers");

        var grid = Find("id:AssetGrid", null);
        double h0 = grid.Current.BoundingRectangle.Height / S;
        var handle = Find("id:ResizeHandle", null).Current.BoundingRectangle;
        var origin = ClientOrigin();
        double hx = (handle.X + handle.Width / 2 - origin.X) / S, hy = (handle.Y + handle.Height / 2 - origin.Y) / S;
        Down(Win(hx, hy));
        Move(Win(hx, hy + 50));
        double h50 = Find("id:AssetGrid", null).Current.BoundingRectangle.Height / S;
        Move(Win(hx, hy + 100));
        Up(Win(hx, hy + 100));
        double h100 = Find("id:AssetGrid", null).Current.BoundingRectangle.Height / S;
        Record("A1", "the browser height tracks the handle 1:1", Near(h50, h0 + 50, 1) && Near(h100, h0 + 100, 1), $"{h0:0.#} -> {h50:0.#} -> {h100:0.#}");
        Down(Win(hx, hy + 100));
        Move(Win(hx, hy - 1000));
        Up(Win(hx, hy - 1000));
        double hMin = Find("id:AssetGrid", null).Current.BoundingRectangle.Height / S;
        var handle2 = Find("id:ResizeHandle", null).Current.BoundingRectangle;
        double hy2 = (handle2.Y + handle2.Height / 2 - origin.Y) / S;
        Down(Win(hx, hy2));
        Move(Win(hx, hy2 + 2000));
        Up(Win(hx, hy2 + 2000));
        double hMax = Find("id:AssetGrid", null).Current.BoundingRectangle.Height / S;
        GetClientRect(MainHwnd(), out var client);
        double want = 0.7 * (client.B - client.T) / S;
        Record("A1", "it clamps at 80 DIPs and at 70% of the window's client height (window.innerHeight * 0.7)", Near(hMin, 80, 1) && Near(hMax, want, 1),
            $"min {hMin:0.#}, max {hMax:0.#} (want {want:0.#})");
        var hover = Inspect();
        Record("A1", "after the release no cursor is left overridden", hover.OverrideCursor == null, $"override {hover.OverrideCursor ?? "none"}");
    }

    // ---------------------------------------------------------------- A2: a folder

    static void A2()
    {
        // asset-open-folder, asset-dedupe-by-name, asset-folder-grouping, asset-count-label, asset-count-label-overwritten,
        // asset-thumb-uuid-label, io-folder-picker (editor.js:2380-2470, :2474-2486, :2544)
        string root = Path.Combine(Fresh("folder"), "art");
        const string uuid = "1a2b3c4d-1111-2222-3333-444444444444";
        void Copy(string pack, string rel)
        {
            string to = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(Pack(pack), to);
        }
        Copy("drip_red_wide_v1.png", @"a\x.png");
        Copy("drip_red_wide_v1.png", @"a\sub\x.png");
        Copy("drip_red_wide_v1.png", @"b\x.png");
        Copy("drip_blue_front_v1.png", @"a\sub\deep\x.png");
        Copy("drip_gold_wide_v1.png", "y.png");
        Copy("drip_green_wide_v1.png", $@"Cool\{uuid}.png");
        File.WriteAllBytes(Path.Combine(root, "z.bmp"), Bmp2x1());
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not an image");
        File.WriteAllText(Path.Combine(root, "logo.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        Launch();
        Answer(root);
        Invoke(Find("Open folder", "Button"));
        Idle();
        string count = WaitText(@"unique images");
        Record("A2", "four unique images (x.png deduped, .bmp listed, .txt and .svg skipped) with the duplicates count shown",
            count == "4 unique images (3 duplicates removed)", $"\"{count}\"");
        var labels = new[] { "All (4)", "Root (2)", "Cool (1)", "deep (1)" };
        var buttons = labels.Select(x => Find(x, "Button")).ToList();
        var order = buttons.Select((x, i) => (i, r: x.Current.BoundingRectangle)).OrderBy(x => Math.Round(x.r.Y)).ThenBy(x => x.r.X).Select(x => x.i).ToList();
        Shot("a2-folder");
        Record("A2", "subfolder buttons All, Root, then the folders in locale order", order.SequenceEqual(new[] { 0, 1, 2, 3 }), string.Join(", ", order.Select(i => labels[i])));

        Invoke(buttons[2]);
        Idle();
        string cool = WaitText(@"images in Cool$");
        var ct = Tiles().Single();
        Invoke(ct);   // UIA Invoke is the click (T2-R2, T4-R4)
        Idle();
        Record("A2", "Cool (1): \"1 images in Cool\", the UUID file captioned by its folder, added under its UUID name",
            cool == "1 images in Cool" && ct.Current.Name == "Cool" && Inspect().Layers.Last().Name == uuid, $"\"{cool}\"; caption \"{ct.Current.Name}\"; layer \"{Inspect().Layers.Last().Name}\"");
        Invoke(buttons[3]);
        Idle();
        var dt = Tiles().Single();
        Invoke(dt);   // UIA Invoke is the click (T2-R2, T4-R4)
        Idle();
        var deep = Inspect().Layers.Last();
        Record("A2", "deep (1): the kept x.png is the deepest copy (128x150, not the 390x200 shallower ones)", deep.Name == "x" && deep.W == 128 && deep.H == 150, $"{deep.Name} {deep.W}x{deep.H}");
        Invoke(buttons[0]);
        Idle();
        string all = WaitText(@"images in all folders$");
        Record("A2", "All (4): \"4 images in all folders\"", all == "4 images in all folders", $"\"{all}\"");
    }

    // ---------------------------------------------------------------- U1: the canvas's layer automation

    static void U1()
    {
        // canvas-layer-automation (contract "Canvas UI Automation"; interfaces 3.7)
        Launch();
        foreach (var f in new[] { "drip_red_wide_v1.png", "drip_blue_wide_v1.png", "drip_gold_wide_v1.png" }) AddImage(f);
        var s = Inspect();
        var canvas = Find("Template canvas", "Image");
        var kids = LayerPeers(canvas);
        Record("U1", "each visible layer is a canvas child, top first, with AutomationId Layer_<id>",
            kids.Select(k => k.Current.Name).SequenceEqual(new[] { "drip_gold_wide_v1", "drip_blue_wide_v1", "drip_red_wide_v1" })
            && kids.Select(k => k.Current.AutomationId).SequenceEqual(s.Layers.AsEnumerable().Reverse().Select(l => $"Layer_{l.Id}")),
            string.Join(", ", kids.Select(k => $"{k.Current.Name}/{k.Current.AutomationId}")));
        var blue = s.Layers[1];
        ((SelectionItemPattern)kids[1].GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Settle();
        var t = (TransformPattern)kids[1].GetCurrentPattern(TransformPattern.Pattern);
        var other = (TransformPattern)kids[0].GetCurrentPattern(TransformPattern.Pattern);
        Record("U1", "SelectionItem.Select selects the layer; only the selected layer can move", Inspect().Selected == blue.Id && t.Current.CanMove && !other.Current.CanMove,
            $"selected {Inspect().Selected}");
        int u0 = Inspect().Undo;
        t.Move(100, 120);
        Settle();
        var m = Inspect().L(blue.Id);
        int u1 = Inspect().Undo;
        t.Rotate(30);
        Settle();
        var r = Inspect().L(blue.Id);
        int u2 = Inspect().Undo;
        t.Resize(195, 100);
        Settle();
        var z = Inspect().L(blue.Id);
        int u3 = Inspect().Undo;
        t.Move(z.X, z.Y);
        Settle();
        int u4 = Inspect().Undo;
        Record("U1", "Move, Rotate and Resize through TransformPattern are template pixels and one undo step each; a no-op records none",
            m.X == 100 && m.Y == 120 && u1 == u0 + 1 && Near(r.Rotation, 30, 1e-9) && u2 == u0 + 2 && Near(z.Scale, 0.5, 1e-12) && u3 == u0 + 3 && u4 == u3,
            $"move ({m.X},{m.Y}), rotation {r.Rotation}, scale {z.Scale}, undo {u0} {u1} {u2} {u3} {u4}");
        Invoke(Find("Hide drip_gold_wide_v1", "Button"));
        var after = LayerPeers(Find("Template canvas", "Image"));
        Record("U1", "a hidden layer is not a child", after.Select(k => k.Current.Name).SequenceEqual(new[] { "drip_blue_wide_v1", "drip_red_wide_v1" }), string.Join(", ", after.Select(k => k.Current.Name)));
    }

    static List<AutomationElement> LayerPeers(AutomationElement canvas) =>
        canvas.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().Select(Guard).ToList();

    // ---------------------------------------------------------------- compare

    /// <summary>RBX15.Parity ImageDiff.Compare, ported: straight RGBA, two transparent pixels are equal, a pixel is
    /// interior when its reference 3x3 neighborhood is one color; interior differences must be zero and edge deltas at most 3.</summary>
    static int Compare(string reference, string candidate)
    {
        var (a, aw, ah) = Rgba(reference);
        var (b, bw, bh) = Rgba(candidate);
        if (aw != bw || ah != bh) { Console.WriteLine($"FAIL size {aw}x{ah} vs {bw}x{bh}"); return 1; }
        int Delta(byte[] p, int i, byte[] q, int j)
        {
            if (p[i + 3] == 0 && q[j + 3] == 0) return 0;
            return Math.Max(Math.Max(Math.Abs(p[i] - q[j]), Math.Abs(p[i + 1] - q[j + 1])), Math.Max(Math.Abs(p[i + 2] - q[j + 2]), Math.Abs(p[i + 3] - q[j + 3])));
        }
        bool Interior(int x, int y)
        {
            if (x == 0 || y == 0 || x == aw - 1 || y == ah - 1) return false;
            int c = (y * aw + x) * 4;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (Delta(a, ((y + dy) * aw + x + dx) * 4, a, c) != 0) return false;
            return true;
        }
        int max = 0, differing = 0, interior = 0, edgeOver = 0;
        for (int y = 0; y < ah; y++)
            for (int x = 0; x < aw; x++)
            {
                int i = (y * aw + x) * 4, d = Delta(a, i, b, i);
                if (d == 0) continue;
                differing++;
                max = Math.Max(max, d);
                if (Interior(x, y)) interior++;
                else if (d > 3) edgeOver++;
            }
        bool pass = interior == 0 && edgeOver == 0;
        Console.WriteLine($"{(pass ? "pass" : "FAIL")} {aw}x{ah} max {max} differing {differing} interior {interior} edgeOver {edgeOver}");
        return pass ? 0 : 1;
    }

    /// <summary>Straight (un-premultiplied) RGBA bytes; WPF decodes a PNG to Bgra32 without premultiplying.</summary>
    static (byte[] Px, int W, int H) Rgba(string path)
    {
        var frame = BitmapDecoder.Create(new Uri(Path.GetFullPath(path)), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad).Frames[0];
        var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        var px = new byte[w * h * 4];
        bgra.CopyPixels(px, w * 4, 0);
        for (int i = 0; i < px.Length; i += 4) (px[i], px[i + 2]) = (px[i + 2], px[i]);
        return (px, w, h);
    }

    // ---------------------------------------------------------------- bridge helpers

    static void Launch()
    {
        StopApp();
        B = Bridge.Launch(Exe);
        Pid = B.Proc.Id;
        var ping = B.Send(new JsonObject { ["cmd"] = "ping" });
        IntPtr main = MainHwnd();
        uint dpi = GetDpiForWindow(main);
        if (dpi != 96) throw new Exception($"REFUSED: display scale {dpi * 100 / 96}% (this driver runs at 100% only; high DPI is manual item MAN-3)");
        Say($"launched pid {Pid}, pipe {B.PipeName}, ping {ping.ToJsonString()}");
        Settle(1000);
    }

    /// <summary>Waits for an exited app's pipe and process to be released.</summary>
    static void Done()
    {
        B?.Dispose();
        B = null;
    }

    static void StopApp()
    {
        if (B == null) return;
        try
        {
            if (!B.Proc.HasExited)
            {
                var closing = CloseAsync();
                var until = DateTime.UtcNow.AddSeconds(8);
                while (!B.Proc.HasExited && DateTime.UtcNow < until)
                {
                    var prompt = OtherWindows().Select(h => (h, t: Text(h))).FirstOrDefault(x => x.t.StartsWith("Do you want to save", StringComparison.Ordinal));
                    if (prompt.h != IntPtr.Zero) Press(Guard(AutomationElement.FromHandle(prompt.h)), "DontSaveButton");
                    Thread.Sleep(300);
                }
                if (!B.Proc.HasExited) { B.Proc.Kill(); B.Proc.WaitForExit(5000); }
            }
        }
        catch (Exception e) { Say("stop: " + e.Message); }
        finally { Done(); }
    }

    static Task CloseAsync()
    {
        var w = (WindowPattern)MainElement().GetCurrentPattern(WindowPattern.Pattern);
        return Task.Run(() => { try { w.Close(); } catch (ElementNotAvailableException) { } catch (InvalidOperationException) { } });
    }

    static JsonObject Canvas(double x, double y) => new() { ["canvas"] = new JsonObject { ["x"] = x, ["y"] = y } };
    static JsonObject Elem(string id, double x, double y) => new() { ["element"] = id, ["x"] = x, ["y"] = y };
    static JsonObject Win(double x, double y) => new() { ["window"] = new JsonObject { ["x"] = x, ["y"] = y } };

    static JsonObject Ptr(string action, JsonObject at, string button = null)
    {
        var c = new JsonObject { ["cmd"] = "pointer", ["action"] = action, ["at"] = at };
        if (button != null) c["button"] = button;
        return B.Send(c);
    }

    static void Down(JsonObject at, string button = "left") => Ptr("down", at, button);
    static void Move(JsonObject at) => Ptr("move", at);
    static void Up(JsonObject at, string button = "left") => Ptr("up", at, button);
    static void Wheel(JsonObject at, int delta) => B.Send(new JsonObject { ["cmd"] = "wheel", ["delta"] = delta, ["at"] = at });

    static void Key(string key, params string[] modifiers)
    {
        var mods = new JsonArray();
        foreach (var m in modifiers) mods.Add(m);
        B.Send(new JsonObject { ["cmd"] = "key", ["key"] = key, ["modifiers"] = mods });
        Settle(300);
    }

    static void Idle(int timeoutMs = 10000) => B.Send(new JsonObject { ["cmd"] = "wait-idle", ["timeoutMs"] = timeoutMs }, timeoutMs + 5000);

    static void Answer(string path) => B.Send(new JsonObject { ["cmd"] = "file-dialog", ["answer"] = path });

    static void DragCanvas((double X, double Y) from, (double X, double Y) to)
    {
        Down(Canvas(from.X, from.Y));
        Move(Canvas((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        Up(Canvas(to.X, to.Y));
        Idle();
    }

    static CanvasSt Inspect()
    {
        var r = B.Send(new JsonObject { ["cmd"] = "inspect", ["target"] = "canvas" });
        var s = new CanvasSt
        {
            Cursor = (string)r["cursor"]!["kind"],
            Ring = (double)r["cursor"]!["ring"],
            RingRadiusPx = (double)r["ringRadiusPx"],
            HostCursor = (string)r["hostCursor"],
            OverrideCursor = (string)r["overrideCursor"],
            Zoom = (double)r["zoom"],
            Gesture = (string)r["gesture"],
            Captured = (bool)r["captured"],
            Undo = (int)r["undo"],
            Redo = (int)r["redo"],
            Selected = (int?)r["selected"],
            Tool = (string)r["tool"],
            Brush = (int)r["brush"],
        };
        foreach (var n in r["layers"]!.AsArray())
            s.Layers.Add(new LayerSt
            {
                Id = (int)n!["id"], Name = (string)n["name"], X = (double)n["x"], Y = (double)n["y"], Scale = (double)n["scale"],
                Rotation = (double)n["rotation"], Visible = (bool)n["visible"], W = (int)n["w"], H = (int)n["h"], Image = (long)n["image"],
            });
        foreach (var n in r["fills"]!.AsArray()) s.FillC1[(string)n!["region"]] = (string)n["c1"];
        return s;
    }

    static (bool Dragging, List<RowSt> Rows) InspectLayers()
    {
        var r = B.Send(new JsonObject { ["cmd"] = "inspect", ["target"] = "layers" });
        var rows = r["rows"]!.AsArray().Select(n => new RowSt { Id = (int)n!["id"], Name = (string)n["name"], DragSource = (bool)n["dragSource"], Drop = (string)n["drop"] }).ToList();
        return ((bool)r["dragging"], rows);
    }

    static void ZoomTo(double zoom)
    {
        for (int i = 0; i < 12; i++)
        {
            double z = Inspect().Zoom;
            if (z == zoom) return;
            Invoke(Find(z < zoom ? "Zoom in" : "Zoom out", "Button"));
        }
        throw new Exception("could not reach zoom " + zoom);
    }

    static readonly Dictionary<string, string> ToolEnum = new() { ["Select"] = "Select", ["Draw"] = "Pen", ["Line"] = "Line", ["Rect"] = "Rect", ["Ellipse"] = "Ellipse", ["Eraser"] = "Eraser" };

    /// <summary>Makes <paramref name="label"/> the current tool. Pressing the active tool would toggle back to Select, so it is pressed only when it is not current.</summary>
    static void Tool(string label)
    {
        if (Inspect().Tool == ToolEnum[label]) return;
        Invoke(Find(label, "Button"));
        if (Inspect().Tool != ToolEnum[label]) throw new Exception("tool did not switch to " + label);
    }

    static void AddImage(string packFile)
    {
        string stem = Path.GetFileNameWithoutExtension(packFile);
        int before = RowNames().Count(n => n == stem);
        Answer(Pack(packFile));
        Invoke(Find("Add image", "Button"));
        for (int i = 0; i < 50 && RowNames().Count(n => n == stem) == before; i++) Thread.Sleep(200);
        if (RowNames().Count(n => n == stem) == before) throw new Exception("the layer did not arrive: " + stem);
        Idle();
    }

    static string Pack(string file) => Path.Combine(Repo, "packs", "drips", file);

    static (double X, double Y) Knob(LayerSt l, double zoom)
    {
        // SelectionGeometry.RotateHandle (editor.js:387-401, ruling T1-1): h/2 + 30/zoom above the center in the layer's frame.
        double d = l.H * l.Scale / 2 + 30 / zoom, rad = l.Rotation * Math.PI / 180;
        return (l.Cx + Math.Sin(rad) * d, l.Cy - Math.Cos(rad) * d);
    }

    static (double X, double Y)[] Corners(LayerSt l)
    {
        // SelectionGeometry.Corner (editor.js:1083-1088), order TL, TR, BR, BL; rotation 0 here.
        double hw = l.W * l.Scale / 2, hh = l.H * l.Scale / 2;
        return new[] { (l.Cx - hw, l.Cy - hh), (l.Cx + hw, l.Cy - hh), (l.Cx + hw, l.Cy + hh), (l.Cx - hw, l.Cy + hh) };
    }

    /// <summary>A template point over a fully transparent pixel of the layer's image, at least 15 template px inside its box.</summary>
    static (double X, double Y) TransparentPoint(string png, LayerSt l)
    {
        var (px, w, h) = Rgba(png);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double tx = l.X + (x + 0.5) * l.Scale, ty = l.Y + (y + 0.5) * l.Scale;
                if (tx < l.X + 15 || tx > l.X + l.W * l.Scale - 15 || ty < l.Y + 15 || ty > l.Y + l.H * l.Scale - 15) continue;
                if (px[(y * w + x) * 4 + 3] == 0) return (tx, ty);
            }
        throw new Exception("no transparent pixel inside the box of " + png);
    }

    /// <summary>A template point 60 DIPs left of the main window, at the canvas's vertical middle. Valid at zoom 100% with the canvas unscrolled.</summary>
    static (double X, double Y) OutsideWindow()
    {
        var win = MainElement().Current.BoundingRectangle;
        var host = Find("Template canvas", "Image").Current.BoundingRectangle;
        double z = Inspect().Zoom;
        double sx = win.Left - 60 * S, sy = host.Top + host.Height / 2;
        return ((sx - host.Left) / S / z, (sy - host.Top) / S / z);
    }

    // ---------------------------------------------------------------- UI Automation helpers (PID-checked)

    static IntPtr MainHwnd()
    {
        for (int i = 0; i < 100; i++)
        {
            B.Proc.Refresh();
            if (B.Proc.MainWindowHandle != IntPtr.Zero) return B.Proc.MainWindowHandle;
            Thread.Sleep(100);
        }
        throw new Exception("no main window");
    }

    static uint PidOf(IntPtr h) { GetWindowThreadProcessId(h, out uint p); return p; }

    static List<IntPtr> AppWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => { if (PidOf(h) == Pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    static List<IntPtr> OtherWindows()
    {
        IntPtr main = MainHwnd();
        return AppWindows().Where(h => h != main).ToList();
    }

    static string SafeName(IntPtr h)
    {
        try { return Guard(AutomationElement.FromHandle(h)).Current.Name; } catch (ElementNotAvailableException) { return ""; }
    }

    /// <summary>The message of a themed dialog window, or "".</summary>
    static string Text(IntPtr h)
    {
        try
        {
            var e = Guard(AutomationElement.FromHandle(h)).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "MessageText"));
            return e?.Current.Name ?? "";
        }
        catch (ElementNotAvailableException) { return ""; }
    }

    static AutomationElement Guard(AutomationElement e)
    {
        if (e == null) throw new Exception("element not found");
        if (e.Current.ProcessId != Pid) throw new Exception("PID mismatch: refusing to touch an element of another process");
        return e;
    }

    static AutomationElement MainElement() => Guard(AutomationElement.FromHandle(MainHwnd()));

    static AutomationElement WaitWindow(Func<AutomationElement, bool> match, int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            foreach (var h in OtherWindows())
            {
                AutomationElement e;
                try { e = Guard(AutomationElement.FromHandle(h)); } catch (ElementNotAvailableException) { continue; }
                if (match(e)) return e;
            }
            Thread.Sleep(200);
        }
        throw new Exception("no matching window within " + ms + " ms");
    }

    /// <summary>The themed dialog whose message starts with <paramref name="prefix"/>.</summary>
    static AutomationElement Dialog(string prefix, int ms = 10000) =>
        WaitWindow(e => (e.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "MessageText"))?.Current.Name ?? "")
            .StartsWith(prefix, StringComparison.Ordinal), ms);

    static void Press(AutomationElement dialog, string automationId)
    {
        var button = Find(dialog, "id:" + automationId);
        ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var h = new IntPtr(dialog.Current.NativeWindowHandle);
        WaitGone(() => AppWindows().Contains(h), 8000);
        Settle(300);
    }

    static bool WaitGone(Func<bool> present, int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until) { if (!present()) return true; Thread.Sleep(150); }
        return !present();
    }

    static bool AnyMenu()
    {
        foreach (var h in AppWindows())
        {
            var top = Guard(AutomationElement.FromHandle(h));
            if (top.FindFirst(TreeScope.Subtree, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Menu),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem))) != null) return true;
        }
        return false;
    }

    static void Invoke(AutomationElement e, bool settle = true)
    {
        if (!e.Current.IsEnabled) throw new Exception($"\"{e.Current.Name}\" is disabled");
        ((InvokePattern)Guard(e).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        if (settle) { Settle(); Idle(); }
    }

    /// <summary>The Edit group's Undo button, by UI Automation Invoke.</summary>
    static void Undo() => Invoke(Find("id:UndoButton", null));

    static void Select(AutomationElement row)
    {
        ((SelectionItemPattern)Guard(row).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Settle();
    }

    static double Range(string slider) => ((RangeValuePattern)Find(slider, "Slider").GetCurrentPattern(RangeValuePattern.Pattern)).Current.Value;

    static void SetRange(string slider, double v)
    {
        ((RangeValuePattern)Find(slider, "Slider").GetCurrentPattern(RangeValuePattern.Pattern)).SetValue(v);
        Settle();
    }

    static string Value(string edit) => ((ValuePattern)Find(edit, "Edit").GetCurrentPattern(ValuePattern.Pattern)).Current.Value;

    static void SetValue(string edit, string v)
    {
        ((ValuePattern)Find(edit, "Edit").GetCurrentPattern(ValuePattern.Pattern)).SetValue(v);
        Settle();
    }

    static string Title() => MainElement().Current.Name;

    /// <summary>The status strip's text in the main window, or "" when it is hidden.</summary>
    static string Status()
    {
        var e = MainElement().FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "MessageText"));
        return e == null || e.Current.IsOffscreen ? "" : e.Current.Name;
    }

    static string TextStartingWith(string prefix)
    {
        foreach (AutomationElement e in MainElement().FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
            if (e.Current.Name.StartsWith(prefix, StringComparison.Ordinal)) return e.Current.Name;
        return "";
    }

    static string WaitText(string pattern)
    {
        var re = new Regex(pattern);
        for (int i = 0; i < 40; i++)
        {
            foreach (AutomationElement e in MainElement().FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
                if (re.IsMatch(e.Current.Name)) return e.Current.Name;
            Thread.Sleep(250);
        }
        return "(no text matching " + pattern + ")";
    }

    static List<string> RowNames() => RowElements().Select(e => e.Current.Name).ToList();

    static List<AutomationElement> RowElements()
    {
        var list = Find("Layers, top layer first", "List");
        return list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().Select(Guard).ToList();
    }

    static AutomationElement Row(string name) => RowElements().FirstOrDefault(e => e.Current.Name == name) ?? throw new Exception("row not found: " + name);

    /// <summary>A point on a row's body (24 DIPs in from its left edge, clear of its buttons), as an offset in LayerList.</summary>
    static JsonObject RowPoint(AutomationElement row, double fy, double dy = 0)
    {
        var r = row.Current.BoundingRectangle;
        var list = Find("id:LayerList", null).Current.BoundingRectangle;
        return Elem("LayerList", (r.Left + 24 - list.Left) / S, (r.Top + r.Height * fy - list.Top) / S + dy);
    }

    static JsonObject AtCenter(AutomationElement e, string refId, double dy = 0)
    {
        var r = e.Current.BoundingRectangle;
        var re = Find("id:" + refId, null).Current.BoundingRectangle;
        return Elem(refId, (r.Left + r.Width / 2 - re.Left) / S, (r.Top + r.Height / 2 - re.Top) / S + dy);
    }

    /// <summary>The realized asset tiles in reading order (AutomationId AssetTile, named by caption).</summary>
    static List<AutomationElement> Tiles()
    {
        var grid = Find("id:AssetGrid", null);
        return grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AssetTile")).Cast<AutomationElement>()
            .Select(Guard).Where(e => !e.Current.IsOffscreen)
            .OrderBy(e => Math.Round(e.Current.BoundingRectangle.Top)).ThenBy(e => e.Current.BoundingRectangle.Left).ToList();
    }

    /// <summary>The tile count of each visual row (tiles grouped by their top edge).</summary>
    static List<int> RowsOf(List<AutomationElement> tiles) =>
        tiles.GroupBy(t => Math.Round(t.Current.BoundingRectangle.Top)).Select(g => g.Count()).ToList();

    static ScrollPattern PanelScroll()
    {
        var walker = TreeWalker.ControlViewWalker;
        AutomationElement e = Find("id:LayerList", null);
        do e = walker.GetParent(e); while (e != null && !e.TryGetCurrentPattern(ScrollPattern.Pattern, out _));
        if (e == null) throw new Exception("the left panel has no ScrollPattern");
        return (ScrollPattern)Guard(e).GetCurrentPattern(ScrollPattern.Pattern);
    }

    static Point ClientOrigin()
    {
        var p = new POINT();
        ClientToScreen(MainHwnd(), ref p);
        return new Point(p.X, p.Y);
    }

    // spec: "Name", "id:AutomationId" or "cls:ClassName"; type is the ControlType's short name or null.
    static AutomationElement Find(string spec, string type)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            foreach (var h in AppWindows())
            {
                AutomationElement top;
                try { top = AutomationElement.FromHandle(h); } catch (ElementNotAvailableException) { continue; }
                if (top.Current.ProcessId != Pid) continue;
                var hit = Match(top, spec, type);
                if (hit != null) return Guard(hit);
            }
            Thread.Sleep(300);
        }
        throw new Exception($"not found: {spec}{(type == null ? "" : " (" + type + ")")}");
    }

    static AutomationElement Find(AutomationElement scope, string spec) => Guard(Match(scope, spec, null) ?? throw new Exception("not found in the dialog: " + spec));

    static AutomationElement Match(AutomationElement scope, string spec, string type)
    {
        foreach (AutomationElement e in scope.FindAll(TreeScope.Subtree, Condition.TrueCondition))
        {
            var c = e.Current;
            bool m = spec.StartsWith("id:", StringComparison.Ordinal) ? c.AutomationId == spec[3..]
                   : spec.StartsWith("cls:", StringComparison.Ordinal) ? c.ClassName == spec[4..]
                   : c.Name == spec;
            if (m && type != null) m = c.ControlType.ProgrammaticName == "ControlType." + type;
            if (m) return e;
        }
        return null;
    }

    // ---------------------------------------------------------------- pixels, files, records

    static BitmapSource Capture(IntPtr h)
    {
        if (PidOf(h) != Pid) throw new Exception("REFUSED: not the app's window");
        GetWindowRect(h, out var r);
        int w = r.R - r.L, hh = r.B - r.T;
        IntPtr wdc = GetDC(h);
        IntPtr mdc = CreateCompatibleDC(wdc);
        IntPtr bmp = CreateCompatibleBitmap(wdc, w, hh);
        try
        {
            var old = SelectObject(mdc, bmp);
            bool ok = PrintWindow(h, mdc, PW_RENDERFULLCONTENT);
            SelectObject(mdc, old);
            if (!ok) throw new Exception("PrintWindow failed");
            var src = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            return new FormatConvertedBitmap(src, PixelFormats.Bgr32, null, 0);
        }
        finally { DeleteObject(bmp); DeleteDC(mdc); ReleaseDC(h, wdc); }
    }

    static void Shot(string name) => ShotOf(MainHwnd(), name);

    static void ShotOf(IntPtr h, string name)
    {
        if (h == IntPtr.Zero || Out == null) return;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(Capture(h)));
        using var fs = File.Create(Path.Combine(Out, name + ".png"));
        enc.Save(fs);
    }

    static (int W, int H) PngSize(string path)
    {
        var b = File.ReadAllBytes(path);
        if (b.Length < 26 || b[1] != (byte)'P' || b[2] != (byte)'N' || b[3] != (byte)'G') throw new Exception("not a PNG: " + path);
        return (BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20)));
    }

    static int PngColorType(string path) => File.ReadAllBytes(path)[25];

    /// <summary>A 2x1 24-bit BMP, red then blue (TestFiles.Bmp2x1's bytes).</summary>
    static byte[] Bmp2x1()
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
        b[54] = 0; b[55] = 0; b[56] = 255;   // red (BGR)
        b[57] = 255; b[58] = 0; b[59] = 0;   // blue
        return b;
    }

    /// <summary>An empty folder under the output folder (its old contents removed).</summary>
    static string Fresh(string name)
    {
        string dir = Path.Combine(Out, name);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    static bool Near(double a, double b, double eps = 1e-6) => Math.Abs(a - b) <= eps;
    static bool Rel(double a, double b, double eps = 1e-12) => Math.Abs(a - b) <= eps * Math.Max(Math.Abs(a), Math.Abs(b));
    static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static void Settle(int ms = 400) => Thread.Sleep(ms);

    static void Say(string s) { Console.WriteLine(s); Log.AppendLine(s); }

    static void Record(string item, string check, bool ok, string detail)
    {
        Results.Add((item, check, ok ? "pass" : "FAIL", detail));
        Say($"[{item}] {(ok ? "pass" : "FAIL")}: {check} -- {detail}");
    }
}

static class Ext
{
    /// <summary>UIA SetFocus, then confirm the element holds keyboard focus (retries; throws if it never does).</summary>
    public static void FocusOn(this AutomationElement e)
    {
        for (int i = 0; i < 6; i++)
        {
            e.SetFocus();
            Thread.Sleep(300);
            if (e.Current.HasKeyboardFocus) return;
        }
        throw new Exception("could not focus \"" + e.Current.Name + "\"");
    }
}
```

What each item drives, and why each value (the H items are M3b's hand checks, M3b Task 5 Step 11; the 4.1 sources are cited in each method's first comment):

| Item | Drives | Rows it evidences |
| --- | --- | --- |
| H1 | Add a drip; hover the body; drag it past the canvas edge (template x 700 and 760) and back, release 60 DIPs left of the window; one Undo; a click; at zoom 200% a 50x30 px and a (30,-20) px drag; two overlapping drips: a press on a fully transparent pixel of the top one, then with it hidden | `select-move-hit-and-drag`, `select-move`, `select-hover-and-end`, `canvas-select-gestures` |
| H2 | Hover the knob, each corner, the body and empty canvas; a corner drag to 1.2, 1.4 and 1.5 times the distance; a knob drag to 20°, 44°, 120° and 182°; two Undos | `cursor-default`, `canvas-cursor-states`, `select-hover-idle`, `canvas-select-gestures`, `props-live-refresh` |
| H3 | One notch up and down; ten down and ten up; 200 fast notches; Undo; five deltas of +8 | `wheel-scale-layer`, `wheel-sign-only-and-midstroke`, `wheel-scale-undo-burst`, `canvas-select-gestures` |
| H4 | A pen stroke and a click; three Rects (plain, with three notches mid-drag, at the new width); an Ellipse; a Line released outside the window, then a move back with the button up; right and middle drags with Draw; a right drag with Select | `canvas-mouse-button-agnostic`, `draw-release-outside-canvas`, `wheel-sign-only-and-midstroke` |
| H5 | The eraser with nothing selected; the ring at sizes 4 and 50 and zoom 100%, 200%, 300% and 25%; the crosshair for the four draw tools; an eraser click and a stroke on the lower of two drips; Undo | `tools-cursor`, `cursor-eraser`, `eraser-cursor`, `render-restore-and-cursor`, `cursor-default`, `eraser-click-without-move` |
| H7 | Four drips; the bottom row onto the top row's top half (mid-drag state and a capture); a release below the list; Alt+Up on a focused row; two Ctrl+Z | `layers-reorder-drag`, `layers-reorder-drag-2`, `layer-reorder-dragging-class-timer` |
| H7b | Esc mid-drag; 20 layers by Duplicate; the top row dragged while the left panel wheels down, dropped on the last row's lower half; Ctrl+Z | `layers-reorder-drag`, `layers-reorder-drag-2` |
| H10 | Paint Front; open Color 1; drag in the square and along the hue strip; press on the canvas through the disabled owner; Ctrl+Z | `fillstyle-color-pickers` |
| F1 | The title at launch and after an edit; Ctrl+S untitled (Save As with the template name), an edit and Ctrl+Z back to clean; Ctrl+Shift+S; Download template, then a cancelled one; Ctrl+O with unsaved edits (Cancel, then Don't save); Ctrl+Z after Open; a non-JSON and a truncated file; Ctrl+S in the template name box and elsewhere; the Save work and Open buttons | `project-save-format`, `project-save-filename`, `project-load-flow`, `project-load-history-interplay`, `project-format-r15proj`, `home-file-group`, `io-download-save-dialog`, `shell-unsaved-work-loss`, `io-alert-inventory`, `dialogs-inventory`, `window-title-dirty-marker` |
| F2 | Close with edits (Cancel, then Save); untitled (Save with a cancelled Save As, then Don't save); a clean close | `shell-unsaved-work-loss` |
| F3 | Save with the template name "abc", relaunch, Open | `project-template-name-not-saved` |
| F4 | The six 4.1 messages live (editor.js:1506, :1270, :2385, :2344, :2364; :2676 in F1); a text file renamed .png through Add image; the same file twice through the drop zone | `dialogs-inventory`, `io-alert-inventory`, `diag-console-only-errors`, `file-input-same-file-reselect`, `asset-open-folder` |
| A1 | Drips and Word art counts and order; click and right-click a tile; drag a tile to (300,200) at zoom 1 and 2, Undo; a drag released over the layer list and one cancelled with Esc; the resize handle by 50, 100, -1000 and +2000 DIPs | `packs-*`, `pack-*`, `asset-click-add`, `asset-rightclick-full`, `asset-drag-to-canvas`, `asset-drag-stale-reference` (in-app half), `asset-resizable-browser`, `panel-asset-resize-handle`, `panel-asset-library` |
| A2 | A folder with duplicates at depths 1-3, a .bmp, a .txt, an .svg and a UUID-named file; the group buttons | `asset-open-folder`, `asset-folder-grouping`, `asset-count-label`, `asset-count-label-overwritten`, `asset-thumb-uuid-label`, `panel-asset-library` |
| U1 | The canvas's layer children; SelectionItem; TransformPattern Move, Rotate, Resize and a no-op Move; a hidden layer | `canvas-layer-automation` |

Build it, and check that it posts and sends nothing:

```bash
dotnet build .superpowers/m4a-smoke/bridge/bridge.csproj -c Release -nologo 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
grep -nE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx|PostMessage|SendMessage|SetForegroundWindow" .superpowers/m4a-smoke/bridge/Program.cs | grep -vE "^[0-9]+:\s*//"
.superpowers/m4a-smoke/bridge/bin/Release/net10.0-windows/brg.exe; echo "exit $?"
```

Expected: `0 Warning(s)`, `0 Error(s)`; the grep prints nothing (the names appear only in the safety header's comments); the usage line and `exit 2`. Do not run `probe`, `smoke` or `files`: they launch the app.

- [ ] **Step 6: Write and commit the manual list**

Create `docs/port/m4a-manual.md`:

```markdown
# M4a manual checks

M4a checks the canvas, the layers panel, files, the asset library and the dialogs through a test-only input bridge that drives RBX15's own window from inside (`test-input-bridge` in the [parity ledger](parity-ledger.md)). Three checks are out of its reach and stay with a person: a drag from another app, a second monitor and a display above 100%. Run the Release build, `dotnet run --project src/RBX15.App/RBX15.App.csproj -c Release --no-build`, and answer each item pass, fail or not run.

| # | Do | Expect | Rows |
| --- | --- | --- | --- |
| MAN-1 | Drag a PNG from File Explorer over the ribbon and over the layers list, then onto the drop zone, then onto the canvas. Then load the Drips pack, start dragging a thumbnail, release it over the layers list, and drag a PNG from Explorer onto the canvas. | Over the ribbon and the list: the no-drop cursor, and nothing happens. The drop zone highlights and the layer lands centered in the front slot. On the canvas it lands centered on the drop point. After the cancelled thumbnail drag, the Explorer file is the one added (its name in the list), not the drip. | `shell-external-drop-guard`, `asset-drag-stale-reference`, `drop-canvas`, `panel-asset-library` |
| MAN-2 | Move the window to a second monitor, close it, relaunch. Look at the 1 px window border. | The window reopens on the second monitor. The border reads navy (#223A54). | Defect checks only: `shell-window-size` and `shell-window-frame` stay open for their other clauses. |
| MAN-3 | Optional: on a display at 125% or 150%, set zoom to 100% and press Download template. | Template edges are crisp, the layout matches the 100% display in DIPs, and the PNG is 585x559. Answer `not run` if no such display is available. | `canvas-device-pixel-render`, `cross-window-dpi-rendering` |

Two further limits rest on unit tests, not on a person: Alt+Tab during a row drag ends it as a lost capture (`LayersPanelDragTests`), and the cursor image Windows draws is never observable to automation, so the cursor rows are checked as the cursor the canvas sets (`CanvasCursorsTests`).
```

Add your own model's `Co-Authored-By` trailer as the last line before running it.

```bash
git add docs/port/m4a-manual.md
git commit -F - <<'EOF'
docs(port): list the M4a checks that stay manual

The test input bridge drives RBX15's own window from inside, so three
checks stay with a person: drags from File Explorer (the drop guard and
the stale asset drag), a second monitor and the window border, and a
display above 100%. Each names the rows it closes or only defect-checks.
EOF
git log --oneline -1
```

Expected: one commit with the subject `docs(port): list the M4a checks that stay manual`.

- [ ] **Step 7: Build the bridge app and probe it (controller-run)**

From the repo root, at 100% display scale, with nothing held on the keyboard (the bridge refuses a chord while a physical modifier is down, R7):

```bash
B=.superpowers/m4a-smoke/bridge/bin/Release/net10.0-windows/brg.exe
$B build
$B probe
```

Expected:

- `build`: `0 Warning(s)`, `0 Error(s)` and `exe <TEMP>\rbx15-bridge-build\bin\RBX15.App\release\RBX15.App.exe`. The build goes to `%TEMP%` only (R9), so the repo's `bin` and `obj` hold no flagged assembly.
- `probe`: `launched pid <n>, pipe rbx15-bridge-<n>-<32 hex>, ping {...}`, one `inspect {...}` line whose `zoom` is 1, `tool` is `Select` and `layers` is `[]`, then `probe ok`. The app window opens and closes by itself.

A `REFUSED` line or an error stops here: report it to the controller as a Task 4 defect.

- [ ] **Step 8: Run the smoke (controller-run)**

The app opens and closes once per item. Leave the mouse and keyboard alone while it runs: the bridge rewrites real input to the app away while it drives (R8), but a held modifier makes a chord refuse.

```bash
B=.superpowers/m4a-smoke/bridge/bin/Release/net10.0-windows/brg.exe
$B smoke .superpowers/m4a-smoke/out
$B files .superpowers/m4a-smoke/out
cat .superpowers/m4a-smoke/out/smoke-items.txt .superpowers/m4a-smoke/out/files-items.txt
grep -c "| FAIL |" .superpowers/m4a-smoke/out/smoke-results.md .superpowers/m4a-smoke/out/files-results.md
ls .superpowers/m4a-smoke/out/*.png .superpowers/m4a-smoke/out/files
```

Expected:

- `done: 8 of 8 items pass; ...` and `done: 7 of 7 items pass; ...`.
- 15 lines, each `<item>: pass`: H1, H2, H3, H4, H5, H7, H7b, H10, then F1, F2, F3, F4, A1, A2, U1.
- Both FAIL counts `0`.
- `a1-drips.png`, `a2-folder.png`, `f1-end.png`, `h10-picker.png`, `h1-released-outside.png`, `h7-mid-drag.png`; in `files/`: `smoke1.r15proj`, `smoke2.r15proj`, `smoke2-v5.png`, `bad.r15proj`, `trunc.r15proj` and no `.tmp` file.

A failed item keeps its rows open (Step 11 sees it) and goes to the controller as a defect for its owner; re-run one item with `$B smoke .superpowers/m4a-smoke/out H3` after a fix.

| Item | Owner |
| --- | --- |
| H1-H5 | M3b Task 2 (`CanvasInteraction`, `TemplateCanvasHost`), or Task 4 when the bridge misroutes (`InputBridgeDispatcher`, `PointerState`) |
| H7, H7b | M3b Task 4 (`LayersPanel`), or Task 4 (`PointerState`, the R7 key path) |
| H10 | M3a/M3b (`ThemedColorPicker.OwnerHook`, `FillStyleViewModel`), or Task 4 (the R8 disabled-owner rule) |
| F1-F3 | Task 1; F4: Task 3 (Task 2 for the pack messages) |
| A1, A2 | Task 2 |
| U1 | Task 4 (`LayerAutomationPeer`, `ICanvasLayerAutomation`) |

- [ ] **Step 9: The 4.1 cross-check P41 (controller-run)**

4.1 opens the project v5 saved in F1, through its real Open path, and its export is held to v5's export of the same document (ruling 6):

```bash
mkdir -p .superpowers/m4a-smoke/p41
printf '[{"name": "m4a-v5-saved", "actions": [{"op": "project", "file": ".superpowers/m4a-smoke/out/files/smoke2.r15proj"}]}]\n' > .superpowers/m4a-smoke/p41-scene.json
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js \
  --scenes .superpowers/m4a-smoke/p41-scene.json --out .superpowers/m4a-smoke/p41
.superpowers/m4a-smoke/bridge/bin/Release/net10.0-windows/brg.exe compare \
  .superpowers/m4a-smoke/p41/m4a-v5-saved.png .superpowers/m4a-smoke/out/files/smoke2-v5.png | tee .superpowers/logs/m4a-p41.txt
git status --short tests tools
```

Expected: a `fonts ok:` line and `rendered m4a-v5-saved` (app-render.js fails the run on any 4.1 alert, so 4.1 opened the file); then one line starting `pass 585x559 ` with `interior 0 edgeOver 0`; `git status` prints nothing (the output stays under `.superpowers/`). A `FAIL` line or an app-render error leaves `project-save-format`, `project-format-r15proj` and `home-file-group` open; report it with the line.

- [ ] **Step 10: Este's manual list (controller-run, with Este)**

Show Este `docs/port/m4a-manual.md` and ask for pass, fail or not run on each item. Record the answers in `.superpowers/logs/m4a-manual.txt` (git-ignored) in exactly this form, then one summary line, which Step 14's commit body reads verbatim:

```text
MAN-1: pass
MAN-2: not run
MAN-3: not run
Manual checks: MAN-1 pass, MAN-2 and MAN-3 not run, by Este on 2026-10-17.
```

A failed item's line reads `MAN-1: fail` and the summary says what Este saw; the controller routes it as a defect (MAN-1: Task 2 or M3a's drop path; MAN-2: M3a's window placement; MAN-3: M3a's device-pixel view).

- [ ] **Step 11: Confirm the evidence for each row before closing it**

Save the gate as `.superpowers/logs/m4a-evidence.py`:

```python
"""M4a Task 5 evidence gate.
Usage: python m4a-evidence.py <normal-verbosity dotnet test log> <smoke-items.txt> <files-items.txt> <p41.txt> <m4a-manual.txt>
Prints `proven` or `OPEN` per candidate row, writes the proven ids to .superpowers/logs/m4a-proven-ids.txt (one line,
space-separated, ledger order), and exits 1 if any row is OPEN."""
import collections, json, re, sys

log = open(sys.argv[1], encoding="utf-8", errors="replace").read()
items_text = open(sys.argv[2], encoding="utf-8").read() + open(sys.argv[3], encoding="utf-8").read()
p41_text = open(sys.argv[4], encoding="utf-8").read()
manual_text = open(sys.argv[5], encoding="utf-8").read()
LINE = re.compile(r'^\s+(Passed|Failed|Skipped) RBX15\.(App|Engine|Parity)\.Tests\.([\w.]+)\.(\w+)(?:\((.*)\))? \[', re.M)
passed, failed = collections.Counter(), collections.Counter()
methods = set()
for status, proj, ns_class, method, _args in LINE.findall(log):
    cls = ns_class.rsplit(".", 1)[-1]
    if status == "Passed":
        passed[(proj, cls)] += 1
        methods.add((cls, method))
    elif status == "Failed":
        failed[(proj, cls)] += 1
items = dict(re.findall(r'^(H\d+b?|F\d|A\d|U\d): (pass|fail)$', items_text, re.M))
manual = dict(re.findall(r'^(MAN-\d): (pass|fail|not run)', manual_text, re.M))
p41 = re.match(r'^pass 585x559 ', p41_text) is not None

E, A = "Engine", "App"
SC = "LedgerScenarioTests"
S = {
    # M3b (already in the class)
    "select": "Select_resize_rotate_and_move_are_one_step_each_and_a_click_records_nothing",
    "wheel": "A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step",
    "eraser": "The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden",
    "switch": "A_tool_switch_mid_stroke_discards_it_and_a_lost_capture_commits_it",
    "brush": "The_wheel_steps_the_brush_in_draw_tools_and_widens_a_live_shape",
    "live": "The_panel_follows_a_canvas_drag_live_and_refreshes_after_a_region_fit",
    "reorder": "Dragging_a_row_reorders_in_one_step_a_stale_target_does_nothing_and_move_up_is_one_step",
    "color": "A_color_pick_retargets_matching_regions_in_one_step_and_a_cancel_changes_nothing",
    # M4a (Step 2)
    "save": "Save_as_writes_the_project_marks_it_clean_and_reopening_restores_it_with_history_cleared",
    "fail": "A_file_that_cannot_open_alerts_and_changes_nothing_and_layers_that_cannot_load_are_named",
    "guard": "The_unsaved_changes_guard_asks_only_when_dirty_and_cancel_or_a_cancelled_save_keeps_everything",
    "packs": "The_built_in_packs_ship_byte_identical_list_sorted_and_add_by_click_and_right_click_in_one_step_each",
    "folder": "A_folder_lists_the_import_formats_dedupes_by_name_keeping_the_deepest_and_shows_the_duplicates_count",
    "drop": "A_thumbnail_or_a_file_dropped_on_the_canvas_lands_centered_on_the_drop_point_at_every_zoom",
    "raw": "Stored_values_out_of_range_open_raw_as_4_1_opens_them",
}
SG, LO, LE, CI, TH, CC = (E, "SelectionGeometryTests"), (E, "LayerOpsTests"), (E, "LayerOpsEditTests"), (A, "CanvasInteractionTests"), (A, "TemplateCanvasHostTests"), (A, "CanvasCursorsTests")
PIO, GA, PFS, AVM, APT, AST, DIT = (E, "ProjectIOTests"), (E, "GoldenAppTests"), (A, "ProjectFileServiceTests"), (A, "AssetLibraryViewModelTests"), (A, "AssetLibraryPanelTests"), (E, "AssetScanTests"), (A, "DialogInventoryTests")
PS, IMP, CAPS = (A, "PackSourceTests"), (A, "ImportServiceTests"), (E, "ImagesCapsTests")
# row -> (test classes that must run green, scenario keys, bridge items, manual items, needs the 4.1 cross-check)
ROWS = {
    # The 23 M3b rows held for the real mouse.
    "select-move-hit-and-drag": ([LO, SG, TH], ["select"], ["H1"], [], False),
    "select-move": ([TH], ["select"], ["H1"], [], False),
    "select-hover-and-end": ([TH, CI], ["switch"], ["H1"], [], False),
    "canvas-select-gestures": ([CI, SG], ["select", "wheel"], ["H1", "H2", "H3"], [], False),
    "canvas-mouse-button-agnostic": ([TH], [], ["H4"], [], False),
    "draw-release-outside-canvas": ([CI, TH], ["switch"], ["H4"], [], False),
    "wheel-scale-layer": ([SG], ["wheel"], ["H3"], [], False),
    "wheel-sign-only-and-midstroke": ([CI, TH], ["brush"], ["H3", "H4"], [], False),
    "wheel-scale-undo-burst": ([CI, TH], ["wheel"], ["H3"], [], False),
    "eraser-click-without-move": ([(E, "EraserTests"), CI], ["eraser"], ["H5"], [], False),
    "props-live-refresh": ([(A, "LayerPropertiesViewModelTests")], ["live"], ["H2"], [], False),
    "layers-reorder-drag": ([LE, (A, "LayersReorderTests")], ["reorder"], ["H7", "H7b"], [], False),
    "layers-reorder-drag-2": ([(A, "LayersReorderTests"), (A, "LayersPanelDragTests"), (A, "ShellTests")], ["reorder"], ["H7", "H7b"], [], False),
    "layer-reorder-dragging-class-timer": ([(A, "LayersPanelDragTests")], ["reorder"], ["H7"], [], False),
    "fillstyle-color-pickers": ([(A, "ColorPickerPopupTests"), (A, "ColorPickerViewModelTests"), (A, "FillStyleTests")], ["color"], ["H10"], [], False),
    "tools-cursor": ([CC, TH], [], ["H5"], [], False),
    "cursor-eraser": ([CC], [], ["H5"], [], False),
    "eraser-cursor": ([CC], [], ["H5"], [], False),
    "cursor-default": ([CI, CC], [], ["H2", "H5"], [], False),
    "canvas-cursor-states": ([CI, CC], [], ["H2"], [], False),
    "select-hover-idle": ([CI], [], ["H2"], [], False),
    "render-restore-and-cursor": ([TH, CC], [], ["H5"], [], False),
    "shell-external-drop-guard": ([], [], [], ["MAN-1"], False),
    # M4a Task 1.
    "project-save-format": ([PIO, PFS], ["save"], ["F1"], [], True),
    "project-save-filename": ([PFS], ["save"], ["F1"], [], False),
    "project-load-flow": ([PIO, PFS, CAPS], ["fail"], ["F1"], [], False),
    "project-load-layers": ([PIO, CAPS], ["raw"], [], [], False),
    "project-load-error-path-bugs": ([PIO], ["fail"], [], [], False),
    "project-load-history-interplay": ([(A, "EditorSessionTests"), PFS], ["save"], ["F1"], [], False),
    "project-load-no-imgsrc-hang": ([PIO], ["fail"], [], [], False),
    "project-load-untrusted-src": ([PIO, GA], ["fail"], [], [], False),
    "project-format-r15proj": ([PIO, GA], ["save"], ["F1"], [], True),
    "file-project-format": ([PIO, GA], ["save"], [], [], False),
    "project-template-name-not-saved": ([PFS], ["save"], ["F3"], [], False),
    "home-file-group": ([(A, "FileGroupTests"), (E, "RendererTests")], ["save"], ["F1"], [], True),
    "io-download-save-dialog": ([(A, "FileGroupTests"), PFS, (E, "RendererTests")], ["save"], ["F1"], [], False),
    "shell-unsaved-work-loss": ([PFS, (A, "ShellTests")], ["guard"], ["F1", "F2"], [], False),
    # M4a Task 2.
    "packs-inventory-drips": ([PS], ["packs"], ["A1"], [], False),
    "packs-inventory-wordart": ([PS], ["packs"], ["A1"], [], False),
    "packs-load-builtin": ([AVM], ["packs"], ["A1"], [], False),
    "pack-builtin-load": ([AVM], ["packs"], ["A1"], [], False),
    "pack-contents": ([PS], ["packs"], [], [], False),
    "pack-load-sync-and-mime": ([AVM, PS], ["packs"], ["A1"], [], False),
    "asset-open-folder": ([AST, AVM], ["folder"], ["A2", "F4"], [], False),
    "asset-dedupe-by-name": ([AST], ["folder"], [], [], False),
    "asset-folder-grouping": ([AST], ["folder"], ["A2"], [], False),
    "asset-count-label": ([AVM], ["folder"], ["A2"], [], False),
    "asset-count-label-overwritten": ([AVM], ["folder"], ["A2"], [], False),
    "asset-click-add": ([AVM, IMP], ["packs"], ["A1"], [], False),
    "asset-rightclick-full": ([LO, IMP], ["packs"], ["A1"], [], False),
    "asset-drag-to-canvas": ([APT], ["drop"], ["A1"], [], False),
    "asset-resizable-browser": ([APT], [], ["A1"], [], False),
    "asset-thumb-uuid-label": ([AST], ["folder"], ["A2"], [], False),
    "panel-asset-resize-handle": ([APT], [], ["A1"], [], False),
    "io-folder-picker": ([AST], ["folder"], [], [], False),
    "panel-asset-library": ([AVM, APT], ["packs", "folder", "drop"], ["A1", "A2"], ["MAN-1"], False),
    "asset-drag-stale-reference": ([APT], [], ["A1"], ["MAN-1"], False),
    "drop-canvas": ([TH], ["drop"], [], ["MAN-1"], False),
    # M4a Task 3.
    "dialogs-inventory": ([DIT], [], ["F1", "F4"], [], False),
    "io-alert-inventory": ([DIT], ["fail"], ["F1", "F4"], [], False),
    "diag-console-only-errors": ([IMP, DIT], [], ["F4"], [], False),
    "file-input-same-file-reselect": ([DIT], [], ["F4"], [], False),
    # New rows (Step 5).
    "test-input-bridge": ([(A, "BridgeExclusionTests"), (A, "BridgeSecurityTests"), (A, "InputBridgeTests")], [], [], [], False),
    "canvas-layer-automation": ([(A, "CanvasLayerAutomationTests")], [], ["U1"], [], False),
    "window-title-dirty-marker": ([(A, "WindowTitleTests")], ["save"], ["F1"], [], False),
    # The display-scale rows, only when MAN-3 ran.
    "canvas-device-pixel-render": ([(E, "RendererViewTests")], [], [], ["MAN-3"], False),
    "cross-window-dpi-rendering": ([], [], [], ["MAN-3"], False),
}
assert len(ROWS) == 67, len(ROWS)
order = [r["id"] for r in json.load(open("docs/port/parity-ledger.json", encoding="utf-8"))["rows"]]
order += [i for i in ("test-input-bridge", "canvas-layer-automation", "window-title-dirty-marker") if i not in order]
proven = []
for row, (classes, scen, bridge, hands, needs_p41) in ROWS.items():
    why = [f"{p}.{c} not run green" for p, c in classes if passed[(p, c)] == 0 or failed[(p, c)] > 0]
    why += [f"{SC}.{S[k]} did not pass" for k in scen if (SC, S[k]) not in methods]
    why += [f"bridge {i}: {items.get(i, 'no result')}" for i in bridge if items.get(i) != "pass"]
    why += [f"manual {m}: {manual.get(m, 'no result')}" for m in hands if manual.get(m) != "pass"]
    if needs_p41 and not p41:
        why.append("the 4.1 cross-check (P41) did not pass")
    if not why:
        proven.append(row)
    print(("proven   " if not why else "OPEN     ") + row + ("" if not why else "  " + "; ".join(why)))
proven.sort(key=order.index)
open(".superpowers/logs/m4a-proven-ids.txt", "w", encoding="utf-8").write(" ".join(proven) + "\n")
print(f"{len(ROWS)} rows, {len(proven)} proven; {sum(1 for k in S if (SC, S[k]) in methods)} of {len(S)} scenarios passed; "
      f"bridge items passed {sum(v == 'pass' for v in items.values())} of {len(items)}; P41 {'pass' if p41 else 'not passed'}; "
      f"failures in any class: {sum(failed.values())}")
sys.exit(0 if len(proven) == len(ROWS) else 1)
```

Run the whole suite at normal verbosity and feed it, the two item files, P41 and the manual answers to the gate:

```bash
dotnet test src/RBX15.slnx -c Release --no-build --logger "console;verbosity=normal" > .superpowers/logs/m4a-evidence.log 2>&1
python .superpowers/logs/m4a-evidence.py .superpowers/logs/m4a-evidence.log .superpowers/m4a-smoke/out/smoke-items.txt \
  .superpowers/m4a-smoke/out/files-items.txt .superpowers/logs/m4a-p41.txt .superpowers/logs/m4a-manual.txt
wc -w .superpowers/logs/m4a-proven-ids.txt
```

Expected: `proven` for each of the 67 rows whose gates hold, `OPEN` with the reason for the rest, then `67 rows, P proven; 15 of 15 scenarios passed; bridge items passed 15 of 15; P41 pass; failures in any class: 0`. With every automated gate green, P is 61 when MAN-1 and MAN-3 were not run (`shell-external-drop-guard`, `panel-asset-library`, `asset-drag-stale-reference`, `drop-canvas`, `canvas-device-pixel-render` and `cross-window-dpi-rendering` print `OPEN ... manual MAN-n: not run`), 65 with MAN-1 passed, 67 with both. The `wc` count equals P.

The gate proves the right classes ran green; it cannot prove each class pins the exact behavior. List the tests Tasks 1-3 wrote:

```bash
grep -n "public void\|public async Task" src/RBX15.App.Tests/ProjectFileServiceTests.cs src/RBX15.App.Tests/WindowTitleTests.cs src/RBX15.Engine.Tests/AssetScanTests.cs src/RBX15.App.Tests/AssetLibraryViewModelTests.cs src/RBX15.App.Tests/AssetLibraryPanelTests.cs src/RBX15.App.Tests/PackSourceTests.cs src/RBX15.App.Tests/DialogInventoryTests.cs src/RBX15.App.Tests/CanvasLayerAutomationTests.cs
```

Write the test name next to each pin below, and note each pin with no test. `M1` to `M7` are Step 2's facts in file order (`M1` `Save_as_writes_*` to `M7` `Stored_values_out_of_range_*`).

| Row | Behavior that must be pinned | Evidence | 4.1 source |
| --- | --- | --- | --- |
| `project-save-filename`, `io-download-save-dialog` | suggestion `My_Shirt_v2_.r15proj` and `untitled.r15proj`; PNG `<title>_roblox.png`; a cancel writes nothing | M1, `ProjectFileServiceTests`, F1 | editor.js:2585-2587, :1536 |
| `project-load-flow`, `io-alert-inventory` | read before the guard; both bad files alert the prefix and change nothing | M2, `ProjectFileServiceTests`, F1 | editor.js:2594-2680 |
| `project-load-history-interplay` | Open clears history, selects the top layer, nextId from the load | M1, `ProjectFileServiceTests` | editor.js:2663 |
| `shell-unsaved-work-loss` | Cancel; Save as Save As whose cancel cancels; Don't save; clean closes without a prompt | M3, `ProjectFileServiceTests`, `ShellTests`, F2 | (new) |
| `window-title-dirty-marker` | the R3 formula, star while dirty, clean after Undo to the save | M1, `WindowTitleTests`, F1 | main.js:8, editor.html:6 |
| `asset-dedupe-by-name`, `asset-folder-grouping`, `io-folder-picker` | deepest copy; `(all)`, `(root)`, then locale order; no buttons with two groups or fewer | M5, `AssetScanTests` | editor.js:2389-2433 |
| `pack-contents`, `packs-inventory-*` | 156 and 25 files, SHA-256 equal to the repo | M4, `PackSourceTests` | editor.js:2323-2333 (resolvePackDir), :2340-2378 |
| `asset-click-add`, `asset-rightclick-full` | 0.7253... in the front slot; 2.795 at (0,0) named ` (full)` | M4, `AssetLibraryViewModelTests`, `LayerOpsTests` | editor.js:2502-2539 |
| `asset-drag-to-canvas`, `asset-drag-stale-reference`, `drop-canvas` | center on the drop point; nothing on Esc, capture loss or a release elsewhere | M6, `AssetLibraryPanelTests`, A1, MAN-1 | editor.js:961-986, :2494-2500 |
| `asset-resizable-browser`, `panel-asset-resize-handle` | 1:1, 80 and 0.7 x client height, SizeNS | `AssetLibraryPanelTests`, A1 | editor.js:2716-2742 |
| `dialogs-inventory`, `diag-console-only-errors`, `file-input-same-file-reselect` | six sites verbatim; failed import to the status strip; re-pick | `DialogInventoryTests`, `ImportServiceTests`, F4 | editor.js:1270, :1506, :2344, :2364, :2385, :2676 |
| `canvas-layer-automation`, `test-input-bridge` | children, SelectionItem, TransformPattern; Release exclusion, ACL, flag | `CanvasLayerAutomationTests`, `BridgeExclusionTests`, `BridgeSecurityTests`, `InputBridgeTests`, U1 | editor.html:330 |

Then apply the gate:

- **An `OPEN` row, a pin with no test, or a failed item for that row:** remove the id from `.superpowers/logs/m4a-proven-ids.txt`, and add it with the reason to the "Left open" list of Step 14's commit body. Report it to the controller. Do not write the missing test here; the gap belongs to the task that owns the module.
- **Everything else** closes in Step 12.

- [ ] **Step 12: Amend, append, add, narrow and close**

Save the ledger edit as `.superpowers/logs/m4a-ledger.py`. It refuses to run on a ledger that moved since planning:

```python
"""M4a Task 5: amend asset-open-folder to fix, append the M4a rulings to ten divergence texts, add three rows, and
narrow the parityCheck of the rows being closed. Usage: python m4a-ledger.py <parity-ledger.json> <id to close>...
Run once, before `ledger.py done` with the same ids. Fails loudly if any row moved since planning."""
import json, sys

path, closing = sys.argv[1], set(sys.argv[2:])
data = json.load(open(path, encoding="utf-8"))
rows = {r["id"]: r for r in data["rows"]}
assert len(data["rows"]) == 417, f"expected 417 rows, found {len(data['rows'])}"
assert sum(r["status"] == "done" for r in data["rows"]) == 188, "expected 188 done rows (M3b's close)"
NEW_IDS = {"test-input-bridge", "canvas-layer-automation", "window-title-dirty-marker"}
assert not (NEW_IDS & rows.keys()), "the M4a rows were already added"

# 1. asset-open-folder (keep) says "only 5 extensions listed"; v5 lists the six import formats (interfaces R14).
#    A keep row carries no divergence, so it is amended to fix, as M3b amended eraser-cursor.
r = rows["asset-open-folder"]
assert (r["disposition"], r["divergence"], r["status"]) == ("keep", "", "open"), "asset-open-folder was already amended"
r["disposition"] = "fix"
r["divergence"] = ("4.1: the folder picker lists .png, .jpg, .jpeg, .gif and .webp only (editor.js:2381). v5: it lists the "
                   "import formats, .bmp included, in a deterministic order (io-folder-picker); the no-images alert keeps "
                   "4.1's text (M4a, interfaces R14).")

# 2. The M4a rulings, one sentence appended to each fix row's divergence.
APPEND = {
    "shell-no-unsaved-changes-guard": "The prompt (Save, Don't save, Cancel, in the Windows wording) and the window title's * marker ship in M4a, ahead of M5's autosave and recovery (Este, 2026-10-16); the prompt also guards Open, and undoing back to the saved state reads clean.",
    "project-load-layers": "Stored rotation, scale, opacity and adjustment values open raw, in range or not, as 4.1 opens them; only the field a gesture or the panel edits is clamped (M4a, interfaces R12).",
    "io-image-formats": "The folder picker lists the import formats, .bmp included, while its no-images alert keeps 4.1's text naming PNG, JPG, GIF and WEBP, and a .bmp keeps its extension in the thumbnail caption, whose regex is 4.1's (M4a, interfaces R14, T2-R6).",
    "project-load-flow": "The Open dialog lists RBX15 project (*.r15proj) first and JSON project (*.json) second, keeping 4.1's .json reach (editor.html:108); the file is read and decoded off the UI thread with the window blocked, and a file that fails to read alerts before any save prompt (M4a, interfaces R4).",
    "asset-drag-to-canvas": "The drag is in-app with mouse capture, not an OLE drag: it starts past the system drag distance and ends with nothing added on Esc, a lost capture or a release off the canvas, and it shows the Hand cursor over the canvas and No elsewhere, with no drag image (M4a, interfaces R6, T2-R1).",
    "asset-grid-thumbnails": "The grid is virtualized by rows of three, and only realized rows decode, at 160 px on one background worker (M4a, interfaces R5).",
    "fillstyle-color-pickers": "A click on the main window while the picker is open commits it through the owner's WM_SETCURSOR hook, which marks the message handled so Windows does not beep (M3b; checked through the M4a bridge).",
    "pack-load-sync-and-mime": "The 156 names are listed synchronously; every read and decode runs off the UI thread (M4a, interfaces R5).",
    "dialogs-inventory": "The eraser's refusal (editor.js:1270) stays on the status strip at the press (eraser-selection-semantics, M3b); the only new modal is the unsaved-changes prompt (M4a, R13).",
    "panel-asset-library": "Thumbnails are buttons: Enter, Space and UI Automation add them (M4a, T2-R2).",
}
# 2b. project-load-layers' "nextId and selection always set" stays true (T1-R2, Este's ruling): no reword.

for rid, text in APPEND.items():
    r = rows[rid]
    assert (r["disposition"], r["status"]) == ("fix", "open") and r["divergence"].startswith("4.1:") and text not in r["divergence"], f"{rid} moved since planning"
    r["divergence"] = r["divergence"] + " " + text

# 3. New rows.
NEW = [
    {
        "id": "test-input-bridge",
        "area": "tests",
        "name": "Test-only input bridge (absent from Release and Store builds)",
        "source": "gap:m4a-bridge",
        "behavior": "4.1 has no in-process test input: its UI was checked by hand and through tools/parity-ref's Electron harness, which drives the page's own DOM events.",
        "codeRefs": ["tools/parity-ref/app-harness.js"],
        "quirks": "Limits (interfaces R7, R8): the OS cursor image is not observable; an OLE drag from another process, Alt+Tab and a second monitor are out of reach; a chord a control handles only on bubbling KeyDown (TextBox's own Ctrl+Z) is not reachable; the bridge pointer cannot click a WPF ButtonBase, whose ButtonState reads GetKeyState, so buttons are pressed by UI Automation Invoke (T4-R4); it raises no TextInput, so text is entered through UI Automation ValuePattern; the hardware keyboard is not neutralized, only the mouse is (T4-R17); its CaptureMouse is a real Win32 SetCapture on the app's own window; the file-dialog command lets the pipe's client, the current user, choose any path that user can write through Save (accepted by design).",
        "portTarget": "src/RBX15.App/Testing/** compiles into RBX15.App only with the MSBuild property EnableTestInputBridge=true (never passed to a solution build, a Release build or Store packaging) and runs only with --test-input-bridge: a named pipe restricted to the current user with a per-launch random name, JSON commands for pointer, wheel, key, wait-idle, file-dialog and inspect, each dispatched on the UI thread into the app's own visual tree; native file dialogs are answered from a queue and settings go to a temp folder, deleted on exit (interfaces R7-R10). A publish or an MSIX build with the flag fails (RefuseTestInputBridgeOnPublish), and M6's packaging pipeline asserts EnableTestInputBridge is unset in its environment and runs BridgeExclusionTests' scan on the packaged RBX15.App.dll (critic M5).",
        "risk": "medium",
        "parityCheck": "BridgeExclusionTests: the App assembly under test and a Release build of RBX15.App contain no RBX15.App.Testing type and neither the launch flag nor the pipe line, and a build with EnableTestInputBridge=true contains them (the positive control). BridgeSecurityTests: the pipe ACL allows only the current user (protected, network denied) and Requested needs the exact flag. InputBridgeTests: a canvas drag as one step, a release outside the window, Ctrl+S through its KeyBinding with the text-field guard, Alt+Up in the layer list, a bare key, the physical-modifier refusal and wait-idle. The controller ran brg probe, brg smoke and brg files against a flagged build (M4a Task 5).",
        "owner": "Parity",
        "check": "unit",
        "disposition": "new",
        "divergence": "",
        "status": "open",
    },
    {
        "id": "canvas-layer-automation",
        "area": "canvas",
        "name": "Each canvas layer is exposed to UI Automation and can be moved through it",
        "source": "gap:m4a-a11y",
        "behavior": "4.1's <canvas id=mainCanvas> exposes nothing (editor.html:330): a screen reader or keyboard user cannot reach a layer on it, and moving one takes a mouse drag (editor.js:1056-1320).",
        "codeRefs": ["editor.html:330", "editor.js:1056-1320"],
        "quirks": "Move and Resize take template pixels, not UI Automation's screen coordinates (contract: 'in template units'); Rotate is relative, as UI Automation defines it.",
        "portTarget": "TemplateCanvasHostAutomationPeer implements ISelectionProvider and has one LayerAutomationPeer per visible layer, top first, cached by id; LayerAutomationPeer is a ListItem with AutomationId Layer_<id>, ISelectionItemProvider and ITransformProvider over ICanvasLayerAutomation (LayerOps.MoveTo, ScaleAboutCenter, SetRotation), one undo step per call and none for a no-op (interfaces 3.7).",
        "risk": "low",
        "parityCheck": "CanvasLayerAutomationTests pin the children, SelectionItem and TransformPattern through the peers. Through UI Automation on the running app (bridge U1): each visible layer is a canvas child, top first, with AutomationId Layer_<id>; SelectionItem.Select selects it; only the selected layer can move; Move(100,120), Rotate(30) and Resize(195,100) set X/Y 100,120, rotation 30 and scale 0.5, one undo step each, and a no-op Move records none; a hidden layer is not a child.",
        "owner": "App.Canvas",
        "check": "unit",
        "disposition": "fix",
        "divergence": "4.1: layers on the canvas are invisible to assistive technology and move only by mouse. v5: each visible layer is a UI Automation child of the canvas (top first) that can be selected, and the selected layer takes TransformPattern Move, Resize and Rotate in template pixels, one undo step each (Este, 2026-10-16).",
        "status": "open",
    },
    {
        "id": "window-title-dirty-marker",
        "area": "shell",
        "name": "Window title names the project file and marks unsaved changes",
        "source": "gap:m4a-files",
        "behavior": "4.1's title is always the product name (main.js:8, editor.html:6); it never names the open project or shows unsaved work.",
        "codeRefs": ["main.js:8", "editor.html:6"],
        "quirks": "",
        "portTarget": "MainViewModel.Title = MainViewModel.WindowTitle(CurrentFile, IsDirty): the product name when clean and untitled, else [*]<file name or Untitled> - RBX15 Classic Shirt and Pants Maker (the Windows convention; interfaces R3). IsDirty compares the document with the last saved or opened one (R2).",
        "risk": "low",
        "parityCheck": "WindowTitleTests pin MainViewModel.WindowTitle. Through UI Automation (bridge F1): the title is the product name at launch, '*Untitled - RBX15 Classic Shirt and Pants Maker' after an edit, 'smoke1.r15proj - RBX15 Classic Shirt and Pants Maker' after Ctrl+S, starred again after an edit, and clean after Ctrl+Z back to the saved state (LedgerScenarioTests pin the same through the view model).",
        "owner": "App.Shell",
        "check": "unit",
        "disposition": "fix",
        "divergence": "4.1: the title is always the product name. v5: it is the product name at launch, then '[*]<file> - RBX15 Classic Shirt and Pants Maker', with * while the document differs from its last save or open and 'Untitled' before the first save (interfaces R3).",
        "status": "open",
    },
]
data["rows"].extend(NEW)
rows.update({r["id"]: r for r in NEW})

# 4. parityCheck narrowed or reworded to the evidence used (ledger honesty rule), applied only to rows being closed.
BR = ("Narrowed in M4a: the gesture is real WPF routed input injected in-process by the test-only input bridge "
      "(brg smoke; interfaces R7, R8) and read back through its inspect command and UI Automation; the bridge cannot show "
      "the OS cursor image or drive a physical device")
CUR = ("Reworded in M4a: the bridge reads the cursor the canvas sets (TemplateCanvasHost.CanvasCursor and its WPF cursor) "
       "after routed hover and drag input; the OS cursor image is not observable to automation, and CanvasCursorsTests pin "
       "the WPF cursors and the ring bitmap")
NARROW = {
    # The M3b rows held for the real mouse (23).
    "select-move-hit-and-drag": f"Overlapping layers: a press on a fully transparent pixel inside the top layer's box selects the top layer, and with that layer hidden the same press selects the layer under it (bridge H1; LayerOpsTests, SelectionGeometryTests, LedgerScenarioTests). A (30,-20) px drag at zoom 2 moves the layer (15,-10) template px, and every drag is one undo step (bridge H1). {BR} (H1).",
    "select-move": f"A 50x30 px drag at zoom 2 moves the layer 25x15 template px, as one undo step, and the layer keeps following past the canvas edge under mouse capture (bridge H1; LedgerScenarioTests, TemplateCanvasHostTests). {BR} (H1).",
    "select-hover-and-end": f"Reworded in M3b to the fixed behavior: a move drag follows the pointer past the canvas edge (template x 700 and 760) and back, and a release outside the window ends it where released, as one undo step (bridge H1); a lost capture ends it as a release at the last point (CanvasInteractionTests, TemplateCanvasHostTests). {BR} (H1).",
    "canvas-select-gestures": f"Pointer sequences through the bridge: a move drag at zoom 1 and 2, a corner drag whose scale ratio equals the distance ratio about a fixed center, a knob drag that snaps at 45 and wraps past 180 to -178, and wheel bursts up to the 500% clamp, each one undo step (bridge H1-H3); the same sequences through CanvasInteraction (LedgerScenarioTests, CanvasInteractionTests, SelectionGeometryTests). {BR} (H1-H3).",
    "canvas-mouse-button-agnostic": f"With Draw, right and middle drags on the canvas add no layer and no undo step, and a right drag on the selected layer with Select moves nothing (bridge H4; TemplateCanvasHostTests). {BR} (H4).",
    "draw-release-outside-canvas": f"Reworded to the fixed behavior: a Line drag released outside the window is committed as one layer, and moving back over the canvas with the button up draws nothing (bridge H4; CanvasInteractionTests, TemplateCanvasHostTests). {BR} (H4).",
    "wheel-scale-layer": f"Reworded to the fixed behavior: one notch up scales x1.05 about the center and one down returns; ten notches down then ten up return to the start scale with the center unchanged (4.1 ends near 0.9794 s); 200 fast notches stop at 500% (bridge H3; SelectionGeometryTests, LedgerScenarioTests). {BR} (H3).",
    "wheel-sign-only-and-midstroke": f"A mouse notch (delta 120) is one scale step, and five events of delta 8 are five steps, by sign not distance (bridge H3); three wheel notches during a Rect drag raise the brush by 3 and the Rect commits at the same size as one drawn at that width (bridge H4; CanvasInteractionTests, TemplateCanvasHostTests, LedgerScenarioTests). {BR}; the small deltas are injected wheel reports of a precision touchpad's size, not a physical touchpad (H3, H4).",
    "wheel-scale-undo-burst": f"LedgerScenarioTests.A_wheel_burst_scales_about_the_center_clamps_at_500_percent_and_is_one_step; CanvasInteractionTests and TemplateCanvasHostTests pin the closes (the 400 ms timer, a press, a tool change, a selection change, and an edit during the burst as its own step). Through the bridge (H3): 200 fast notches are one undo step and one Undo reverts the whole burst, and a burst of small deltas is one step. {BR} (H3).",
    "eraser-click-without-move": f"EraserTests.A_press_without_movement_erases_nothing; CanvasInteractionTests.Eraser_click_without_move_changes_nothing_and_records_no_step; LedgerScenarioTests.The_eraser_refuses_without_a_selection_and_erases_only_the_selected_layer_even_when_hidden. Through the bridge (H5): an eraser click leaves the layer's image and the undo count unchanged, and a stroke replaces only the selected layer's image, as one step. {BR} (H5).",
    "props-live-refresh": f"During a knob drag the Rotation slider equals the layer's rotation after every move, and during a corner drag the Scale slider equals its scale (bridge H2, read through UI Automation RangeValue); LayerPropertiesViewModelTests and LedgerScenarioTests pin the live X and Y and the panel after a fit. {BR} (H2).",
    "layers-reorder-drag": f"LayerOpsEditTests and LayersReorderTests check the reorder index math with 4 layers over every (from, to, position) against editor.js:621-655. Through the bridge (H7, H7b): the bottom of four rows dropped on the top row's top half becomes the top, and with 20 layers the top row dropped on the last row's lower half becomes the bottom, each one undo step. {BR} (H7, H7b).",
    "layers-reorder-drag-2": f"Through the bridge (H7, H7b): the bottom row dropped on the top half of the top row becomes the top while the drop line shows above that row; a drag released off every row, or cancelled with Esc, moves nothing and leaves no mark; with 20 layers the left panel wheels down mid-drag and the drop lands below the last row; Alt+Up moves a focused row one place; and one Ctrl+Z (the window's KeyBinding) undoes each. LayersReorderTests, LayersPanelDragTests, ShellTests and LedgerScenarioTests pin the same; a lost capture (Alt+Tab) cancelling the drag is pinned by LayersPanelDragTests only. {BR} (H7, H7b).",
    "layer-reorder-dragging-class-timer": "Mid-drag the bridge reads the source row's drag mark and the target row's drop line (above) through its inspect command, and a PrintWindow capture records them (.superpowers/m4a-smoke/out/h7-mid-drag.png); LayersPanelDragTests pin the look: the source row at opacity .4 and a 2 px #17d4fa line on the target's top or bottom edge (styles.css:310-312). Narrowed in M4a: the dim and the line are checked as state plus a capture, not by eye (H7).",
    "tools-cursor": f"Through the bridge (H5): with the Eraser the canvas cursor is the ring with radius max(4, size x zoom / 2) DIPs at sizes 4 and 50 and zoom 25%, 100%, 200% and 300%, rebuilt as soon as size or zoom changes; Draw, Line, Rect and Ellipse hover with the crosshair. CanvasCursorsTests: the ring bitmap at device scale and the 256-px Win32 cap. {CUR} (H5).",
    "cursor-eraser": f"Through the bridge (H5): ring radius max(4, size x zoom / 2) DIPs (diameter size x zoom) at sizes 4 and 50 and zoom 25-300%, hotspot centered (CanvasCursorsTests), capped by the Win32 cursor size (128 - ceil(2 x display scale) device px). Reworded in M3b: 4.1's 240 px clamp is the fixed behavior; size 100 and zoom 4 are outside v5's ranges. {CUR} (H5).",
    "eraser-cursor": f"Through the bridge (H5): the ring diameter equals size x zoom DIPs at zoom 0.25 and 3, with no 240 cap (amended to fix in M3b); CanvasCursorsTests. {CUR} (H5).",
    "cursor-default": f"Through the bridge (H2, H5): idle hover gives the crosshair (Cross) on empty canvas with Select and everywhere with Draw, Line, Rect and Ellipse, the ring with the Eraser, and Grab (Hand) on the knob, ResizeNwse (SizeNWSE) on each corner and Move (SizeAll) on the body. {CUR} (H2, H5).",
    "canvas-cursor-states": f"Through the bridge (H2): hovering the knob gives Grab (Hand), and the knob drag holds Grabbing (Hand) and the corner drag ResizeNwse (SizeNWSE) after every move until the release. {CUR} (H2).",
    "select-hover-idle": f"Through the bridge (H2): idle hover over the knob, each corner and the body gives Grab, ResizeNwse and Move, else the crosshair. {CUR} (H2).",
    "render-restore-and-cursor": f"Through the bridge (H5): with the Eraser active, a brush size change (UI Automation RangeValue) and a zoom change (the zoom buttons) each change the ring the canvas sets before any pointer move; TemplateCanvasHostTests and CanvasCursorsTests. {CUR} (H5).",
    "shell-external-drop-guard": "By hand in the M4a manual list (MAN-1): a PNG dragged from Explorer over the layers panel and the ribbon shows the no-drop cursor and does nothing; dropped on the drop zone it highlights the zone and lands centered in the front slot; dropped on the canvas it lands centered on the drop point. Narrowed in M4a: a hand check, because an OLE drag from another process is outside the bridge (interfaces R8).",
    # M4a Task 1.
    "project-save-format": "ProjectIOTests (M2b): Write keeps saveProject's field set and order for the three 4.1-saved fixtures in tests/goldens/projects and round-trips them to equal documents. LedgerScenarioTests: a save through the File group writes version 5.0 with no title and reopens to the same export. 4.1 reads a v5 file: tools/parity-ref/app-render.js loads a v5-saved project through 4.1's real Open path with no alert, and its export equals v5's export of the same file under the golden rule (M4a Task 5 P41, brg compare). Narrowed in M4a: the JSON is compared field by field with 4.1's own saves, not diffed against a same-scenario Electron save.",
    "project-load-flow": "ProjectIOTests (M2b) open the 4.1 fixtures (a v4 file and the legacy color-string pants file), '{}' and every structural refusal field by field; LedgerScenarioTests and the bridge (F1) show a non-JSON and a truncated file alerting 'Failed to load project. ...' with the document, history and title unchanged and no save prompt first; the read runs off the UI thread (ProjectFileServiceTests) and a file past the caps is refused by them (ImagesCapsTests). Narrowed in M4a: 'huge file' is the decode caps, not a timed load.",
    "project-load-layers": "ProjectIOTests (M2b): missing fields, opacity 0, scale 0, visible false, an undecodable image in the middle and in the last position, duplicate ids and nextId follow loadProject's rules; LedgerScenarioTests: stored values out of range open raw. Narrowed in M4a: the 50-large-image fixture is covered by the decode caps (ImagesCapsTests) and the off-thread read, not run.",
    "project-load-history-interplay": "Reworded in M4a to the fixed behavior: after an edit and an Open, the history is empty, Undo is disabled and Ctrl+Z changes nothing (LedgerScenarioTests, bridge F1); 4.1's Ctrl+Z back into the pre-load session is what this row fixes.",
    "project-load-no-imgsrc-hang": "ProjectIOTests.A_layer_without_an_image_is_dropped_and_named; LedgerScenarioTests: a project whose two layers lack imgSrc opens to an empty layer list, the canvas view model takes the opened document, the status strip names both layers, and Undo straight after Open does nothing (history cleared).",
    "project-load-untrusted-src": "ProjectIOTests (M2b): http and file image URLs and undecodable payloads are dropped and named, a background that is not a data: URL refuses the file, an unknown mode refuses it, and junk regionColors are skipped; LedgerScenarioTests: an http imgSrc layer is named in the status strip after Open; GoldenAppTests project-* scenes render the 4.1 fixtures against 4.1's goldens. Narrowed in M4a: no file or network read is possible because only data: URLs are decoded, from memory, and the app references no network API (Step 13's grep); a project file over 512 MiB is refused before it is read (ProjectFileServiceTests.A_file_over_the_size_limit_alerts_and_changes_nothing); no Process Monitor trace is taken.",
    "io-download-save-dialog": "Through the bridge (F1): with the title 'My Shirt v2!' Download template suggests My_Shirt_v2__roblox.png and writes a 585x559 RGBA PNG, a cancel writes nothing and shows nothing, and Save As suggests My_Shirt_v2_.r15proj; RendererTests pin 'My Shirt' to My_Shirt_roblox.png. Narrowed in M4a: the dialogs are the bridge's file-dialog queue, which logs each suggested name; the native dialogs are Win32FileDialogs (FileGroupTests, ProjectFileServiceTests).",
    "shell-unsaved-work-loss": "Through the bridge (F2): closing with edits shows the themed Save / Don't save / Cancel prompt; Cancel keeps the window and the edits, Save saves to the current file and closes, Save on an untitled document runs Save As and a cancelled Save As cancels the close, Don't save closes without writing, and a window with no edits closes at once; the same prompt guards Open (F1). LedgerScenarioTests and ShellTests pin the guard. Reworded in M4a to the new behavior (Este, 2026-10-16).",
    # M4a Task 2.
    "packs-inventory-drips": "LedgerScenarioTests: the build output's packs/drips holds 156 files with the repo's names, each SHA-256 equal to the source, and the library lists 156 tiles under those names in ordinal-ignore-case order; the bridge (A1) reads '156 images in built-in drips pack'. Narrowed in M4a: the browser is virtualized, so the 156 are counted in the view model and only the realized tiles are read through UI Automation.",
    "packs-load-builtin": "Click Drips: the view model holds 156 tiles in ordinal-ignore-case order, three per row, and the realized tiles are the first ones in that order (LedgerScenarioTests, bridge A1); Word art: 25; the count reads '156 images in built-in drips pack' and stays (the fix). Narrowed in M4a: thumbnails decode as rows scroll into view (asset-grid-thumbnails).",
    "asset-open-folder": "Reworded in M4a: a folder with .png, .bmp, .txt and .svg files lists the six import formats (.png, .jpg, .jpeg, .webp, .bmp, .gif; interfaces R14) and skips the rest; an empty folder shows 'No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files.' verbatim (LedgerScenarioTests, AssetScanTests, bridge A2 and F4).",
    "asset-count-label": "Reworded in M4a to the fixed behavior: after a folder with duplicates loads, the label reads '<U> unique images (<D> duplicates removed)' (editor.js:2448) and stays; a subfolder button sets '<N> images in <folder>' and All sets '<N> images in all folders' (editor.js:2544) (LedgerScenarioTests, bridge A2).",
    "asset-count-label-overwritten": "Through the bridge (A2): after a folder with duplicates loads, the label read through UI Automation is '4 unique images (3 duplicates removed)', with a PrintWindow capture (a2-folder.png); LedgerScenarioTests pin the same. Narrowed in M4a: read and captured, not compared by eye.",
    "asset-resizable-browser": "Through the bridge (A1): a handle drag of 50 and 100 DIPs grows the browser by the same amount, it clamps at 80 DIPs and at 70% of the window's client height, and no cursor stays overridden after the release; AssetLibraryPanelTests pin the handle's SizeNS cursor. Narrowed in M4a: the OS cursor image is not observable.",
    "asset-drag-stale-reference": "In-app drags carry no OS data: a thumbnail drag released over the layers list or cancelled with Esc adds nothing (bridge A1, AssetLibraryPanelTests), and by hand (MAN-1) a PNG dragged from Explorer onto the canvas right after a cancelled thumbnail drag adds the Explorer file. Narrowed in M4a: the in-app half through the bridge, the OS drop by hand.",
    "drop-canvas": "LedgerScenarioTests: a file dropped at four template positions at zoom 0.5, 1 and 2 lands with its center on the drop point; TemplateCanvasHostTests map the host point by zoom. The stale asset drag is gone: asset-drag-stale-reference (bridge A1, MAN-1).",
    # M4a Task 3.
    "dialogs-inventory": "DialogInventoryTests map each of 4.1's six sites to its themed dialog or status text verbatim; through the bridge (F1, F4) all six fire on the running app and read verbatim through UI Automation: the empty-template confirm (editor.js:1506), the eraser refusal in the status strip (:1270), no images in the folder (:2385), a missing and an empty built-in pack (:2344, :2364) and 'Failed to load project. ...' (:2676), each dialog titled with the product name. Narrowed in M4a: the only other modal text is the Save / Don't save / Cancel prompt, which 4.1 never had (shell-unsaved-work-loss).",
    "diag-console-only-errors": "Through the bridge (F4): a text file renamed .png and picked through Add image adds no layer, opens no dialog and names the file in the status strip ('Couldn't add not-an-image.png: ...'). Narrowed in M4a to the import, asset, project and preview routes (DialogInventoryTests: undecodable files through Add image, the drop zone, the canvas drop and an asset click; handleFiles' three console lines; a project layer that cannot load; preview failures once per set; bridge F4); the Background image route (editor.js:940-944) is the M4b Layout tab's.",
    "file-input-same-file-reselect": "DialogInventoryTests.Picking_the_same_file_again_acts_again (drop-zone click, Add image, Open, Open folder) and bridge F4. Narrowed in M4a: the Background picker is the M4b Layout tab's.",
    # The display-scale rows, closed only when MAN-3 ran.
    "canvas-device-pixel-render": "RendererViewTests (M3a): the frame is Renderer.ViewSize at DeviceScale 1.25, 1.5 and 2, and at DeviceScale 1 it equals the view goldens. By hand in the M4a manual list (MAN-3) at 125% or 150% display scale: template edges are crisp at zoom 1, and the export stays 585x559.",
    "cross-window-dpi-rendering": "By hand in the M4a manual list (MAN-3): at 100% and at 125% or 150% display scale the layout matches in DIPs and the export is 585x559. Narrowed in M4a: two display scales, not four.",
}
APPEND_CHECK = {
    "fillstyle-color-pickers": "Through the bridge (H10): dragging in the square and along the hue strip repaints a painted Front live, a click on the canvas through the disabled owner commits the pick as one step with the owner hook's WM_SETCURSOR handled (no beep), and one Ctrl+Z reverts it. Narrowed in M4a: Color 1 only through the bridge; Color 2, the hex field, Esc and Enter come from the M3a themed run.",
}
unknown = closing - rows.keys()
assert not unknown, f"unknown ids: {sorted(unknown)}"
for rid, text in NARROW.items():
    if rid not in closing:
        continue
    r = rows[rid]
    assert r["status"] == "open" and r["parityCheck"] != text, f"{rid} was already narrowed or closed"
    r["parityCheck"] = text
for rid, text in APPEND_CHECK.items():
    if rid not in closing:
        continue
    r = rows[rid]
    assert r["status"] == "open" and text not in r["parityCheck"], f"{rid} was already narrowed or closed"
    r["parityCheck"] = r["parityCheck"] + " " + text

open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
narrowed = len(closing & NARROW.keys()) + len(closing & APPEND_CHECK.keys())
print(f"amended 1, appended {len(APPEND)}, added {len(NEW)}, narrowed {narrowed}; {len(data['rows'])} rows")
```

Print the state first:

```bash
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix')"
```

Expected: `417 rows; 188 done; 200 fix`.

Then run the edit and the close with the proven ids. The edit narrows only the ids it is given; the amendment, the seven appends and the three new rows always apply:

```bash
IDS=$(cat .superpowers/logs/m4a-proven-ids.txt)
python .superpowers/logs/m4a-ledger.py docs/port/parity-ledger.json $IDS
python tools/ledger/ledger.py done $IDS
python tools/ledger/ledger.py check
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', sum(r['disposition']=='fix' for r in d['rows']), 'fix;', sum(r['disposition']=='new' for r in d['rows']), 'new')"
grep -n "^## " docs/port/divergences.md
git diff --numstat -- docs/port
```

Expected, measured on a scratch copy of 3f3c5c5's `docs/port` and `tools/ledger` with exactly this script:

- With the 61 ids that need no manual item: `amended 1, appended 10, added 3, narrowed 39; 420 rows`, `rendered docs/port/parity-ledger.md and docs/port/divergences.md`, `420 rows, 0 problem(s)`, `420 rows; 249 done; 203 fix; 2 new`; numstat `168	109	docs/port/parity-ledger.json`, `67	59	docs/port/parity-ledger.md`, `16	12	docs/port/divergences.md`. (Measured before Este's T1-R2 ruling removed the `project-load-layers` reword in block 2b: the json, md and divergences numstat may each drop by one changed line; every other expected line stands.)
- With all 67: `narrowed 44`, `420 rows; 255 done; 203 fix; 2 new`; numstat `179	120`, `73	65`, `16	12`.
- In every case `## Fixed in v5 (203)`, `## Not carried into v5 (26)`, `## New in v5 (2)` at lines 5, 213 and 244, and `188 + C` done with C the number of ids given.
- The JSON loses one `status` line per closed existing id (C - 3; the three new rows are appended), one `parityCheck` line per narrowed id, `asset-open-folder`'s `disposition` and `divergence` (2) and the ten appended `divergence` lines (10; `project-load-layers`' selection correction is on its appended line). It gains those back plus the three new rows (59 lines). Each id left out takes one line off each side of `parity-ledger.md` and one (two if narrowed) off each side of the JSON. A larger JSON diff means the file was reformatted: stop, run `git checkout -- docs/port`, and report it.

- [ ] **Step 13: Final verification**

Run, in order, from the repo root:

```bash
BASE=$(git merge-base HEAD main); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror
dotnet test src/RBX15.slnx -c Release > .superpowers/logs/m4a-final.log 2>&1; grep -E "(Passed|Failed)!" .superpowers/logs/m4a-final.log
python -m unittest discover -s tools/ledger -p "test_*.py"
python tools/ledger/ledger.py check
node --test "tools/parity-ref/*.test.js" 2>&1 | grep -E "^# (tests|pass|fail)"
npm test 2>&1 | grep -E "^# (tests|pass|fail)"
git diff --stat "$BASE" -- main.js editor.js editor.html styles.css lib packs package.json package-lock.json "tests/*.test.js" windows
git diff --stat "$BASE" -- tests/goldens assets/fonts
grep -rnE "UseWPF|PresentationCore|PresentationFramework|WindowsBase|System\.Windows" src/RBX15.Engine --include=*.cs --include=*.csproj --exclude-dir=bin --exclude-dir=obj
grep -rnE "HttpClient|WebRequest|WebClient|System\.Net\.Sockets|TcpClient|ClientWebSocket" src/RBX15.Engine src/RBX15.App --include=*.cs --exclude-dir=bin --exclude-dir=obj
grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos|ClipCursor|SetWindowsHookEx" src --include=*.cs --exclude-dir=bin --exclude-dir=obj
grep -rlE "SendMessage|PostMessage" src --include=*.cs --exclude-dir=bin --exclude-dir=obj
grep -rln "EnableTestInputBridge" src --include=*.csproj --include=*.props --include=*.targets --exclude-dir=bin --exclude-dir=obj
dotnet list src/RBX15.App/RBX15.App.csproj package
grep -rnE "626 ?Labs" src/RBX15.App --include=*.cs --include=*.xaml --exclude-dir=bin --exclude-dir=obj
git diff "$BASE" -U0 -- src tools | grep -inE "^\+.*(api[_-]?key|secret|password|bearer )"
git status --short
```

Expected:

- `base 3f3c5c5`.
- Build: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: three summary lines, each `Failed: 0` and `Skipped: 0`: `RBX15.Engine.Tests.dll` `Passed: 938`; `RBX15.Parity.Tests.dll` `Passed: 35`; `RBX15.App.Tests.dll` `Passed: 867`. `BridgeExclusionTests` is among them: the Release `RBX15.App.dll` holds no bridge type and no bridge string.
- Ledger unit tests: `Ran 8 tests`, `OK`. `ledger.py check`: `420 rows, 0 problem(s)`.
- `node --test`: `# tests 14`, `# pass 14`, `# fail 0`. `npm test`: `# tests 31`, `# pass 31`, `# fail 0`.
- The Electron diff prints nothing; the goldens and fonts diff prints nothing (M4a adds no golden).
- The WPF grep on the engine and the network grep print nothing. The synthetic-input grep prints one line, in `src/RBX15.App.Tests/BridgeExclusionTests.cs` (the test's own regex; critic M2).
- The `SendMessage|PostMessage` grep lists only files under `src/RBX15.App/Testing/` (the R8 owner-click rule) and `src/RBX15.App.Tests/` (the bridge's own tests, if they reference it).
- The `EnableTestInputBridge` grep lists `src/RBX15.App/RBX15.App.csproj` only: no solution-wide file sets the flag (R9).
- `dotnet list`: one top-level package, `CommunityToolkit.Mvvm`, Requested and Resolved `8.4.2`.
- The `626` grep prints exactly three lines, all in `src/RBX15.App/ViewModels/MainViewModel.cs`: the two doc-comment lines about the footer and `public string FooterText => "Developed at 626Labs";`. Any other hit is branding 4.1 does not show: report it.
- The secrets grep prints nothing.
- `git status --short` lists exactly three lines, each with `M` in the second column: `docs/port/divergences.md`, `docs/port/parity-ledger.json`, `docs/port/parity-ledger.md`. `.superpowers/` and `diff-heatmaps/` are git-ignored.

- [ ] **Step 14: Commit the ledger**

Write the body from Steps 8-12. The "Left open" list is the fixed table below plus every id Step 11 removed, with its reason. The `Manual checks:` line is the last line of `.superpowers/logs/m4a-manual.txt`. Add your own model's `Co-Authored-By` trailer as the last line before running it.

| Rows | Why open | Closes with |
| --- | --- | --- |
| `shell-no-unsaved-changes-guard`, `shell-no-persistence` | Their divergences promise autosave, recovery and recents. settings.json now also holds `lastProjectFolder` and `lastAssetFolder` (Task 1, Task 2), which M6's PRIVACY.md pass lists. | M5 (M6 for PRIVACY.md) |
| `shell-window-frame`, `shell-window-size` | Launch size, resize and caption clauses; MAN-2 is a defect check only. | M6 |
| `undo-buttons-shortcuts` | Ctrl+Y, Ctrl+Shift+Z and the tooltips. | M4b |
| `packs-resolve-dir` | The installed-MSIX run. | M6 |
| `asset-grid-thumbnails` | A look comparison with 4.1. | M4b look sign-off |
| `asset-objecturl-lifecycle` | A measured 500-image memory and scroll run, and a 20,000-file folder that Open folder enumerates with no stall over 2 s (critic L8; 4.1 enumerated synchronously too). | M6 |
| (carry-forward, no row) | `ProjectIO` has no total decoded-pixels budget across layers; M4a caps the file at 512 MiB (CA4) and each image as before (critic M1). | M5 or M6 |
| `io-image-formats` | The Background picker. | M4b |
| `shell-external-drop-guard`, `panel-asset-library`, `asset-drag-stale-reference`, `drop-canvas` (only if MAN-1 was not passed) | An OS file drop from Explorer. | MAN-1 |
| `canvas-device-pixel-render`, `cross-window-dpi-rendering` (only if MAN-3 was not passed) | A display above 100%. | MAN-3 |
| Every other open row in `.superpowers/m4-planning/open-rows.txt` | Outside M4a's scope; untouched. | M4b and later |

```bash
MAN=$(tail -n 1 .superpowers/logs/m4a-manual.txt)
git add docs/port/parity-ledger.json docs/port/parity-ledger.md docs/port/divergences.md
git commit -F - <<EOF
chore(ledger): close the rows M4a and the bridge smoke prove; add the M4a divergence rows

Closed by the M4a unit tests, the LedgerScenarioTests, the test input
bridge's smoke run (H1-H5, H7, H7b, H10; F1-F4, A1, A2, U1) and the 4.1
cross-check, in which 4.1 opened a v5-saved project through its real
Open path and its export met the golden rule against v5's:
the 23 M3b rows held for the real mouse (move, capture and release
outside the window, resize, rotate and its snap, the wheel and its
bursts, draw-tool releases and other buttons, the eraser click, the
live panel, row drags with Esc, the panel wheeled mid-drag, Alt+Up and
Ctrl+Z, the color picker's owner click, and the cursor the canvas sets
for every tool and handle), save and Save As, open and its failures,
the unsaved-changes prompt, the template name not being saved, the
built-in packs, the folder browser, click, right-click and drag adds,
the resizable browser, and the dialog inventory, verbatim and live.
Narrowed or reworded texts say "Narrowed in M4a" or "Reworded in M4a".

Amended to fix: asset-open-folder, whose check listed 4.1's five
extensions; the folder picker lists the import formats, .bmp included.

Appended: shell-no-unsaved-changes-guard (the prompt ships ahead of
M5's autosave), project-load-layers (stored values open raw; nothing is
selected after Open), io-image-formats (the folder alert keeps 4.1's
text), project-load-flow (the .json filter and the off-thread read),
asset-drag-to-canvas (an in-app drag), asset-grid-thumbnails
(virtualized rows), fillstyle-color-pickers (the owner-click hook),
pack-load-sync-and-mime (the synchronous listing), dialogs-inventory
(the eraser message and the one new modal) and panel-asset-library
(keyboard-operable thumbnails).

New: test-input-bridge (absent from Release and Store builds),
canvas-layer-automation (layers selectable and movable through UI
Automation) and window-title-dirty-marker (file name and unsaved
marker in the title).

Left open on purpose: autosave, recovery and recents (M5); the window
launch, resize and caption clauses, the MSIX pack path, a measured
500-image run and a 20,000-file folder (M6); redo shortcuts, the
Background picker and the asset grid's look comparison (M4b). Carried
forward: a total decoded-pixels budget for ProjectIO (M5 or M6) and the
two new settings keys for M6's PRIVACY.md.

$MAN
Electron, every golden and assets/fonts are untouched.
EOF
git log --oneline -4
git status --short
```

The heredoc is unquoted so `$MAN` expands; the body holds no other `$` or backtick. If Step 11 removed ids, delete their topics from the "Closed" paragraph and add each, with its reason, to "Left open" before committing; if MAN-1 or MAN-3 was not passed, add their rows to "Left open" too.

Expected: four commits from this task on top of Task 4's, newest first: `chore(ledger): close the rows M4a and the bridge smoke prove; add the M4a divergence rows`, `docs(port): list the M4a checks that stay manual`, `test(app): replay the M4a ledger scenarios for files, the unsaved prompt, packs, folders and drops`, then Task 4's last commit; and an empty `git status --short`. Do not push: push, the PR and the dashboard (log the bridge smoke results and the ledger close as decisions with project `6KpHItSNg9NYHkJg9thn`) belong to the controller.

**Additions requested by Task 5.**

1. **`inspect` with `target` `canvas` returns exactly these fields** (the sheet lists the first eight without their shapes; Task 4 produces them, T4-R15, critic C3):
   - `cursor`: `{"kind": <CanvasCursorKind name>, "ring": <CanvasCursor.RingDiameter>}`;
   - `ringRadiusPx`: `CanvasCursors.RingRadius(ring, zoom, deviceScale)` when the kind is `EraserRing`, else 0;
   - `hostCursor`: the name of the host's WPF cursor, matched by reference against `Cursors.Cross`, `SizeAll`, `Hand`, `SizeNWSE`, `SizeNS`, `Arrow`, `No` and `Wait` (WPF has no `Copy` cursor, T2-R1) (`"Cross"`, `"SizeAll"`, ...), `"Custom"` for the ring, `null` for none;
   - `overrideCursor`: the same name for `Mouse.OverrideCursor`, or `null`;
   - `zoom`, `gesture` (`CanvasGesture` name), `captured` (`TemplateCanvasHost.IsMouseCaptured`), `undo`, `redo`, `selected` (id or `null`);
   - `tool`: the `Tool` name (`Select`, `Pen`, `Line`, `Rect`, `Ellipse`, `Eraser`); `brush`: `BrushViewModel.Size`;
   - `layers`: bottom to top (document order), each `{id, name, x, y, scale, rotation, visible, w, h, image}`, where `w` and `h` are the image's pixel size and `image` is a per-process identity number of the `SKImage` instance (a `ConditionalWeakTable<SKImage, object>` counter): equal for the same instance, different for a replaced one;
   - `fills`: `[{region, type, c1, c2}]` from `Document.RegionFills`.
2. **`inspect` with `target` `layers`** (new): `{"dragging": LayersPanel.IsDragging, "rows": [{id, name, dragSource: LayerRowViewModel.IsDragSource, drop: DropIndicator name}]}`, rows top first as the list shows them.
3. **The `pointer` result on the disabled-owner path** (R8): `{"ownerClick": true, "handled": <whether the owner's WM_SETCURSOR returned nonzero>}`; `{}` on every other path.
4. **`BridgeFileDialogs` prints one stdout line per call, flushed,** in Task 4's format: `RBX15-BRIDGE-DIALOG <Method> <argument> -> <answer>`, where the argument is the suggested file name for `SaveProject` and `SavePng` and the initial directory (empty when none) for `OpenProject`, `PickFolder` and `OpenImages`, and the answer is the path, `cancel` or `cancel (queue empty)`. Every driver check reads these lines with `StartsWith` (critic L13).
5. **Wire names are camelCase as the sheet's table writes them:** requests `id`, `cmd`, `action`, `button` (optional, default left), `at`, `delta`, `key` (WPF `Key` names: `S`, `Z`, `O`, `Up`, `Escape`), `modifiers` (optional array), `timeoutMs`, `answer`, `target`; responses `id`, `ok`, `error`, `result`.
6. **AssetLibraryPanel names:** the ListBox `x:Name="AssetGrid"`; the resize handle `x:Name="ResizeHandle"` (new); each tile a control with an automation peer (a Button) whose AutomationId is `AssetTile` (Task 2's `x:Name="AssetTile"`: `FrameworkElementAutomationPeer` falls back to Name, so no extra attribute is needed; critic C5) and its caption as `AutomationProperties.Name`; the pack and folder buttons' content `Drips`, `Word art`, `Open folder`; each group button's content its label (`All (4)`).
7. **`MaxBrowserHeight` is 0.7 x the main window's client height** (`Viewport.ActualHeight`), as 4.1's `window.innerHeight * 0.7` (editor.js:2732); the sheet's "0.7 * window height" could be read as the outer height. `AssetGrid`'s height is `BrowserHeight`.
8. **Status texts use the file name, not the path:** `Saved {Path.GetFileName(path)}.` and `Opened {Path.GetFileName(path)}. Couldn't load: {names}.`
9. **ThemedDialog's elements keep their names in the SaveChanges kind:** `MessageText`, `OkButton` (content `Save`), `DontSaveButton` (content `Don't save`), `CancelButton`, and the window title is the product name; UI Automation reads them by those AutomationIds.
10. **Interface members and the scenario fakes:** Task 1 adds the three `IFileDialogs` members to `LedgerScenarioTests`' existing nested `Files` and `AskSaveChanges` to its `Dialogs`. Step 2's new nested `ProjectDialogs`, `PromptDialogs` and `MemorySettings` implement the sheet's 3.3 members; a task that adds a member to `IFileDialogs`, `IDialogService` or `ISettingsStore` adds it to all five.
11. **`PackSource.Files` reads the folder on every call** (no cache), as 4.1 resolves and reads it on every click (editor.js:2342, :2347), so a pack folder parked while the app runs gives the not-found and empty alerts (F4, `packs-resolve-dir`'s delete-the-folder clause).
12. **Citation corrections for the sheet:** R5's grid is editor.html:305 (not 304); the asset browser clamp is editor.js:2732 (not 2731); the canvas element is editor.html:330.
13. **Ledger rows by task (sheet section 5), Task 5:** add the `asset-open-folder` amendment, the three new rows and the two MAN-3 rows (`canvas-device-pixel-render`, `cross-window-dpi-rendering`, M3b's H9 rows) to the list; `shell-no-unsaved-changes-guard`, `shell-window-frame`, `shell-no-persistence`, `undo-buttons-shortcuts`, `packs-resolve-dir`, `asset-grid-thumbnails`, `asset-objecturl-lifecycle` and `io-image-formats` stay open (ruling 2).
