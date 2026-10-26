# RBX15 v5 M5 and M6: autosave and recovery, recents and the jump list, launch with a file, paste, the error log, then the package, accessibility, UI automation and the Store 5.0 docs. Implementation plan

Drafted 2026-10-25 by a planning agent for the controller to review. Sources: origin/main at 35a5935 (M4b merged, PR #55), the ledger (`docs/port/parity-ledger.json`, 429 rows, 366 done), the spec (`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`, §3 "Upgrades" lines 129-136, §6 Tier 3 lines 226-232, §7 lines 234-270), the M4b plan and its two Cart runs (`~/.cwt/rbx15-m4b/docs/cart-runs/2026-10-18-m4b-items-1-8` and `-9-16`), `docs/port/m4b-manual.md`, and the memory notes `v5-m3-carry-forwards.md` and `v51-avatar-preview-run.md`.

> **For /take:** each `- [ ]` item below is one Cart item: one worker, one branch, about 1-2 h. Run the items one at a time, in order. Items 1-6 all edit `App.xaml.cs`, `Composition.cs`, `TestShell.cs`, `MainWindow.xaml.cs` or `ShellTests.cs`, so they run strictly in sequence. Items 7, 8 and 9 touch none of those files and may run beside the M5 chain. Items 12-15 are docs only, each in its own files, and may run in parallel with each other once item 6 has merged (they describe the finished features).

**M5 is the spec's upgrade row; M6 is everything up to Store-ready.** M5 builds the four §3 upgrades (autosave and recovery, recent projects with the jump list, the `.r15proj` launch, paste as layer) and the local error log. M6 proves the package, makes the accessibility pass, adds the thin UI automation suite, rewrites the docs, and writes the Store 5.0 copy and reviewer letter as files. **Done when:** every open ledger row that is not Retired is closed under the honesty rule, or left open only behind a named MAN check or a named "Este / release and after certification" step, and the seven new upgrade rows exist and are closed. The stop line is Store-ready: nothing in the Run list submits, signs, tags or pushes.

Already done, so not in scope:

- The MSIX and portable zip build with identity checks and the packaged bridge scan (`scripts/build-msix.ps1`, PR #52); CI (`dotnet-ci.yml`, `v5-package.yml`) and the release workflow (`v5-release.yml`, `v5.*` tags).
- The Store upgrade path: Este's Store-signed 4.1 updated in place to the self-signed 5.0 on 2026-10-25 (PR #56, open, closes `parity-store-update-path`). Item 17 only checks that row reads done after #56 merges.
- The `.r15proj` FileTypeAssociation in `Package.v5.appxmanifest.template` (registered and asserted by build-msix; the app does not yet open the launched file, which is item 4).
- Window placement memory (`WindowPlacement`, `PlacementGuard`, `JsonSettingsStore`), the 640x480 minimum (`ShellLayout.MinWidth/MinHeight`), the Save / Don't save / Cancel close prompt (M4a R13), and the `*` title marker.
- Two launches give two windows (`shell-single-instance`, keep, closed by the M4b smoke).
- The full suite passes at 100% and at a real 125% (MAN-8, 2026-10-25), so the display preflight is retired.
- The app icon, the header mark, the "Background" tab label (2d07fea).

---

## Global constraints

The M4a and M4b Global Constraints apply as written. The main ones:

- **The engine never references WPF.** `RBX15.Engine` stays free of `System.Windows`, `PresentationCore` and `WindowsBase`.
- **Goldens are frozen.** No file under `tests/goldens/` changes. A golden failure is a product bug, never a tolerance change.
- **No global synthetic input.** `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` prints exactly one line, in `src/RBX15.App.Tests/BridgeExclusionTests.cs`.
- **Implementers never launch the app.** Workers build and test only. Launching the exe, the bridge smoke, PrintWindow captures and packaged installs are the controller's (item 16) or Este's (the MAN list).
- **StaThread view tests.** Anything that builds WPF objects runs through `StaThread.Run`, in the `MainWindow` collection when it shows a window.
- **One composition root.** New services are constructed in `Composition.Build` at the `// M3a:` anchors, mirrored in `TestShell`; `App.xaml.cs` only gathers launch facts (paths, arguments) and hands them in.
- **The ledger honesty rule.** A row closes only on evidence that exercises the behavior its parityCheck names. A narrowing says what is not proven and is marked "Narrowed in M5" or "Narrowed in M6".
- **Items 1-16 close no ledger rows.** Each ends its commit body with `Ledger evidence: <row-id> <- <TestClass.Method>, ...` for its rows (new rows by the ids in the scope table). Item 17 does every ledger edit: it adds the seven new rows, closes rows from the trailers, and renders. `parity-ledger.json` never conflicts between branches.
- **Conventional commits**, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Verify, every item:** `dotnet build src/RBX15.slnx -c Release -warnaserror`, then `dotnet test src/RBX15.slnx -c Release --no-build` (0 failed, the Parity count unchanged at 35), then the synthetic-input grep. Each item reports its Engine and App counts.

M5 and M6 add these:

- **Tests never touch the user's machine state.** No test reads or writes `%LOCALAPPDATA%\RBX15`, the system clipboard, the process's real taskbar jump list, or starts a process. Every such surface sits behind a seam (`AppStorage` with a temp root, `IClipboardSource`, `IJumpListSink`, `IAppLauncher`) with a fake in `src/RBX15.App.Tests/Fakes/`. A worker that needs the real surface to prove something writes a MAN row for item 16 instead.
- **Wall-clock ceilings assert only under `RBX15_PERF=1`** (the M4b D20 rule). Without it, perf tests report their numbers and assert correctness only. Autosave and recovery tests use a manual tick source, never `Thread.Sleep` or real 60 s timers.
- **Every new modal string joins the dialog inventory.** `DialogInventoryTests.Every_v5_dialog_shows_an_inventory_string` stays green; new texts live in `DialogText`.
- **Upgrades never change the document format.** Autosave files are ordinary `ProjectIO.Write` output; recents and window state stay in `settings.json`.
- **Nothing written locally ever holds document content, except the autosave file itself.** The error log, `settings.json` and the autosave metadata hold paths, times and exception data only.

## Decisions for Este before the run

Each has a recommended answer. The run applies the recommendation unless Este says otherwise.

| # | Question | Recommended | If declined |
|---|---|---|---|
| A1 | Where do autosave files, the error log and recents live? | One root for everything, `App.SettingsDirectory` (`%LOCALAPPDATA%\RBX15`), as `settings.json` already does: `autosave\`, `logs\` beside `settings.json`. In the MSIX this is redirected into the package's local data and removed on uninstall (MAN-16 confirms where it lands). The portable zip writes the real folder and PRIVACY says so. One code path for both builds, no WinRT dependency. | `ApplicationData.Current.LocalFolder` when packaged: needs a `net10.0-windows10.0.17763.0` TFM and a second path for the zip. |
| A2 | What autosaves, and where does a titled document's autosave go? | Every dirty document autosaves, untitled or not, to `autosave\<session-id>.r15proj` plus a `<session-id>.json` sidecar (owner pid, process start time, original path or null, autosave time, app version). Never next to, and never over, the user's own file. A Save, an Open or a clean close deletes the pair. | Untitled documents only; titled ones rely on the close prompt. |
| R1 | The recovery prompt. | A themed dialog at launch, before any other prompt: "RBX15 didn't close properly. Restore your unsaved work on <name>, autosaved <time>?" with Restore and Discard. Closing it (Esc or X) keeps the file for the next launch. Restore opens it dirty (title `*`), with `CurrentFile` set to the original path when that file still exists, so Ctrl+S writes the original; untitled restores stay untitled. Discard deletes the pair. | Restore only, no Discard (files pile up), or an automatic restore with no prompt. |
| R2 | Several orphaned autosaves (two windows crashed together). | One prompt per launch, newest first. The launching process claims the orphan by renaming its pair to its own session id before it asks, so two launches never offer the same file. The rest wait for later launches. Orphans are never deleted silently. | A list dialog that restores several at once into new windows. |
| R3 | Launched with a file while an orphan is waiting. | The recovery prompt still shows first. Restore puts the recovered work in this window and opens the launched file in a new window through `IAppLauncher` (a self-launch with the path). Discard or Esc opens the launched file here. | The launched file wins and the orphan waits for a plain launch. Risk: a user who always double-clicks never sees it. |
| S1 | "Opening a second project starts a new window" (spec §3). | One process per window, as today: every launch (Explorer, the jump list, a second double-click) is its own process and window. No pipe or mutex between instances. `shell-single-instance` already pins two windows for two launches. Ctrl+O inside a window keeps replacing that window's document behind the save prompt. | A single process that hosts several windows over an IPC handoff. More surface, more failure modes, same visible result. |
| H1 | Where do recents show, and does the jump list carry tasks? | The File group gets a "Recent" drop-down button beside Open: the last 10, newest first, file name with the full path as tooltip, missing files pruned when the list is read, and a final "Clear recent list" entry. The jump list shows the same 10 under a custom "Recent" category, with no tasks. Recents are added on a successful Save, Save As or Open (including a launch with a file), never on a restore. | No in-app list (jump list only), or a jump list with a "New window" task as well. |
| J1 | How the jump list is published. | WPF's `System.Windows.Shell.JumpList` with one `JumpTask` per recent (this exe, the quoted path as the argument), behind an `IJumpListSink` seam. A JumpTask works in both the MSIX and the zip; a JumpPath needs the file type registered to the caller, which the zip never has. MAN-11 checks the packaged taskbar. If it fails there, item 5's follow-up switches the packaged build to WinRT `Windows.UI.StartScreen.JumpList` (a TFM change, so it holds for Este). | WinRT JumpList from the start, packaged only; the zip gets no jump list. |
| P1 | Ctrl+V with a layer selected. | Paste always adds a new layer on top, selected, with the Select tool on, as Add image does, in one undo step. It never replaces the selected layer's image. A focused text field keeps Ctrl+V for text (the existing `TextFieldShortcutCommand`). No Paste button (4.1 had none); README's Controls table and the Undo-style tooltip list carry it. | Paste replaces the selected layer's image. |
| P2 | Paste sources and the layer name. | In order: the clipboard's `PNG` format (keeps alpha); else a file list with image files (Explorer copy), added in order as one step exactly like a drop, each named by its file; else a bitmap (`CF_DIBV5`, then `CF_DIB`/Bitmap). A bitmap or PNG layer takes the default layer name, as an import with no name does. Every source goes through the 100 MiB / 16384 / 50 MP caps. Nothing usable: the status strip says "Nothing to paste. Copy an image first." A clipboard held by another app is retried 3 times over about 100 ms, then named on the status strip. | PNG and bitmap only; Explorer copies paste nothing. |
| E1 | The error log. | `logs\error.log` under A1's root, rolled to `error.1.log` at 256 KB (so at most about 512 KB). One entry per unhandled exception: UTC time, app version, OS version, exception type, HResult, message and stack, inner exceptions included. The user profile path in any message is rewritten to `%USERPROFILE%`. No document content, no layer names, never sent. No "Open log folder" button; the crash dialog and SMOKE_TEST name the path. | A button in the Settings group, or a larger log. |
| E2 | What an unhandled exception does. | Log it, write an emergency autosave if the document is dirty, show one themed dialog ("Something went wrong and RBX15 has to close. Your work was autosaved and will be offered next time you open the app. Details: <log path>"), then end the process. Continuing after an unknown failure risks saving a corrupt document over a good one. Background-thread and unobserved-task exceptions are logged the same way; an unobserved task exception is logged only and does not close the app (it never reached the UI). | Log and continue (mark the dispatcher exception handled). |
| U1 | The spec's "thin UI automation suite" (launch, add image, region paint, Word Art, undo, save and reopen, export). | In-process, as xUnit: `UiFlowTests` builds the real `MainWindow` through `Composition.Build` with fake dialogs, shows it on the STA thread and drives it only through UI Automation peers and patterns (Invoke, Value, Toggle, SelectionItem) plus the window's KeyBindings. It runs on every PR in CI, needs no app launch and no desktop input. The controller's out-of-process bridge smoke (item 16) covers the real exe and packaged build, as M4a and M4b did. | FlaUI out of process: a new dependency, and it launches the app, which workers may not do. |
| X1 | Accessibility pass scope. | Automated: a UIA sweep over every tab, the left panel and every dialog asserts that each keyboard-focusable or invokable element has a non-empty Name and the FocusRing focus visual, and that Tab reaches the region tiles, swatches, layer rows and the drop zone. Manual: a Narrator spot check (MAN-14). Out of scope, written down as known limitations: high-contrast themes and announcing canvas edits to screen readers. | Add high-contrast support (a theme pass, likely a session). |
| C1 | `render-pipeline-canvas-reset` is open "not proven": p95 12.6-13.3 ms on 20 rotated, translucent layers, 31.7-32.5 ms with color filters too. | Item 7 caches the static composite (background, region fills, every layer) as one image keyed on the `Document` instance while a pen, line, shape or eraser stroke is live, and draws only the live preview, mask, grid and handles over it. If the filtered document's p95 is then under 16.7 ms, the row closes. If not, item 7 holds with the numbers. | Narrow the budget to the lighter document now and close the row with no code change. |
| D1 | When README, CONTRIBUTING, SMOKE_TEST, TEST_PLAN, the landing page and the product card are rewritten. The spec puts them in the cutover PR, but the Docs rows must close before the v5 release gate passes, and PRIVACY is the Store's privacy URL. | Now, in M6 items 12-15, written for v5 while the Electron files still sit in the repo (a short "Legacy 4.1 source" note where needed). The cutover PR only removes the legacy notes and the Electron files. | At cutover; the Docs rows then stay open and the release gate needs a narrowing to tag. |
| D2 | `claim-fullscreen-canvas`: the listing and README promise a fullscreen canvas that does not exist. | Reword: "a large canvas that fills the window, built for the exact 585x559 template grid." No F11 feature this late. | Add F11 (WindowStyle None plus Maximized) and a matching row. |
| D3 | Screenshots. The look changed since 4.1 (the 626Labs mark, the Background tab, the 3D preview). | Item 16 captures v5 screenshots of the controller's Release build with PrintWindow into `docs/screenshots/5.0/` (Home, Background, Details, Text, a finished shirt); README and the landing page use them. Store screenshots stay Este's call in the after-certification list. | Keep the 4.1 screenshots in README and the landing page. |
| L1 | Rows whose check needs Partner Center, certification, a fork tag or an 1809 VM (`msix-identity`'s validation clause, `msix-target-os`'s VM launch, `ci-windows-release`'s tagged run, `msix-capabilities`' certification). | Narrow each to what is proven locally (build-msix's assertions, workflow tests, the MAN list), marked "Narrowed in M6", and move the remainder to the "Este / release and after certification" list with the row id. | The rows stay open, and the release gate cannot pass until certification, which comes after the tag. |

---

## 1. Scope table: every in-scope row and its item

38 open non-Retired rows on main: 10 App, 8 Package, 20 Docs. `parity-store-update-path` is handled by PR #56, which leaves 37 for this plan, plus 7 new rows. None is unassigned.

| Item | Rows |
|---|---|
| 1 | `upgrade-error-log` (new) |
| 2 | `upgrade-autosave-recovery` (new; the autosave half), `shell-no-persistence` (the autosave clause) |
| 3 | `upgrade-autosave-recovery` (the recovery half), `shell-no-unsaved-changes-guard`, `shell-lifecycle-quit` |
| 4 | `upgrade-file-association-launch` (new) |
| 5 | `upgrade-recent-projects` (new), `upgrade-jump-list` (new), `shell-no-persistence` (the recents and jump list clauses) |
| 6 | `upgrade-paste-as-layer` (new) |
| 7 | `render-pipeline-canvas-reset` |
| 8 | `asset-objecturl-lifecycle` |
| 9 | `msix-identity`, `msix-application-entry`, `msix-target-os`, `msix-arch-self-contained`, `ci-windows-ci`, `ci-windows-release`, `packs-resolve-dir` (the package half), `msix-capabilities` (the manifest and network-scan half) |
| 10 | `parity-ui-automation-suite` (new), `shell-window-size`, `shell-window-frame` |
| 11 | `a11y-focus-rings`, `docs-known-limitations` (the accessibility clause) |
| 12 | `docs-privacy-claims`, `docs-security-md`, `claim-network-surface-inconsistent`, `shell-userdata-footprint` |
| 13 | `docs-readme-claims`, `docs-dev-workflow-claims`, `tests-count-claims`, `legacy-docs-superpowers`, `docs-feedback-md`, `claim-undo-everything`, `claim-fullscreen-canvas`, `wordart-claim-wrap-any-size`, `docs-index-landing`, `docs-manifest-626labs` |
| 14 | `tests-manual-smoke`, `tests-windows-test-plan`, `claim-region-click-color-picker` |
| 15 | `docs-store-listing`, `docs-reviewer-letter`, `docs-store-assets`, `msix-capabilities` (the reviewer-letter half), `docs-known-limitations` (the release-notes clause) |
| 16 | The MAN list and smoke evidence for: `upgrade-autosave-recovery`, `upgrade-jump-list`, `upgrade-file-association-launch`, `upgrade-paste-as-layer`, `a11y-focus-rings`, `msix-arch-self-contained`, `msix-application-entry`, `packs-resolve-dir`, `msix-capabilities`, `shell-window-frame`, `shell-userdata-footprint`, `asset-objecturl-lifecycle` |
| 17 | Adds the 7 new rows and closes all of the above from the evidence. |

Rows that cut across items close at item 17 only when every contributing item has merged:

- `shell-no-persistence`: items 2 and 5, plus item 12's PRIVACY text (its check is `doc`).
- `upgrade-autosave-recovery`: items 2 and 3, plus MAN-10.
- `msix-capabilities`: items 9 and 15, plus MAN-17 (or L1's narrowing).
- `docs-known-limitations`: items 11 and 15.
- `shell-userdata-footprint`: item 12, plus MAN-16.

### Excluded

| Rows | Why |
|---|---|
| 25 Retired | Close at cutover, after certification (spec §7). |
| `parity-store-update-path` | PR #56 closes it (Este's in-place upgrade test, 2026-10-25). |

### New rows (item 17 adds them; workers cite these ids in their trailers)

| Id | Owner | Check | Disposition | What it promises |
|---|---|---|---|---|
| `upgrade-error-log` | App.Shell | unit | new | Unhandled exceptions only, rolled at 256 KB, no content, never sent (spec §3; E1, E2). |
| `upgrade-autosave-recovery` | App.Storage | ui-auto | new | 60 s atomic autosave while dirty; a clean exit deletes it; a crash offers restore at the next launch (spec §3; A1, A2, R1-R3). |
| `upgrade-recent-projects` | App.Storage | unit | new | The last 10, missing files pruned, in the File group (H1). |
| `upgrade-jump-list` | App.Shell | manual | new | The same 10 on the taskbar jump list (J1). |
| `upgrade-file-association-launch` | App.Shell | manual | new | A launch with a `.r15proj` opens it; a second project starts a new window (S1). |
| `upgrade-paste-as-layer` | App.Clipboard | unit | new | Ctrl+V: PNG first, keeps alpha, centered on the front slot, one undo step (P1, P2). |
| `parity-ui-automation-suite` | Parity | ui-auto | new | Spec §6 Tier 3: launch, add image, region paint, Word Art, undo, save and reopen, export (U1). |

---

## 2. Run list

### M5

- [ ] **1. The local error log**
  Rows: `upgrade-error-log`.
  Spec ref: §3 "Upgrades" (the error log replaces DevTools as the smoke diagnostic); E1, E2.
  What to build (App):
  - `src/RBX15.App/Services/AppStorage.cs`: `sealed record AppStorage(string Root)` with `SettingsDirectory` (= Root), `AutosaveDirectory` (Root\autosave) and `LogDirectory` (Root\logs). `App.xaml.cs` builds it from `App.SettingsDirectory`; the bridged launch from its temp folder. Item 2 passes it to Composition.
  - `src/RBX15.App/Services/ErrorLog.cs`: `ErrorLog(string directory, TimeProvider clock, long rollBytes = 256 * 1024)` with `Write(Exception e, string source)`. Appends one entry (E1's fields), rolls `error.log` to `error.1.log` when the next write would pass `rollBytes`, rewrites the user profile prefix to `%USERPROFILE%`, and never throws (an IO failure is swallowed).
  - `src/RBX15.App/Services/CrashHandler.cs`: pure wiring helpers so the policy is testable without crashing the test host: `HandleDispatcher(Exception)`, `HandleDomain(Exception)`, `HandleUnobservedTask(Exception)`. Each logs; the first two also call an injected `Action emergencyAutosave` (a no-op until item 2) and an injected `Action<string> showFatal`, then an injected `Action exit`. `App.xaml.cs` subscribes `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` to them first thing in `OnStartup`.
  - `DialogText.FatalError(string logPath)` with E2's text, added to the dialog inventory.
  Done when:
  - A thrown exception with an inner exception writes one entry with both types, HResults and stacks, and the version and OS lines.
  - A message holding the profile path is written with `%USERPROFILE%` and no user name.
  - Writes past 256 KB leave `error.log` small and one `error.1.log`; a third roll replaces `error.1.log` (never more than two files).
  - A read-only or missing directory makes `Write` return without throwing.
  - The dispatcher and domain handlers log, call emergency autosave, show the fatal text and exit, in that order (call-order fake); the unobserved-task handler logs only.
  - A source scan test finds no `HttpClient`, `WebClient`, `Socket` or `WebRequest` in `src/RBX15.App`.
  Verify: the standard three.
  Stop class: go.
  Risk: `DispatcherUnhandledException` during a modal dialog's nested loop. Mark it handled before showing the fatal dialog so it is not raised twice.

- [ ] **2. Autosave every 60 s while there are unsaved changes**
  Rows: `upgrade-autosave-recovery` (the autosave half), `shell-no-persistence` (the autosave clause).
  Spec ref: §3 "Upgrades", Autosave + recovery; A1, A2. Carry-forward (M2b): cache PNG data URLs per SKImage.
  What to build:
  - Engine (`ProjectIO.cs`): `PngDataUrl` caches its string per `SKImage` in a `ConditionalWeakTable`, so a 60 s autosave of an unchanged 20-layer document re-encodes nothing. `ProjectIO.Write` output stays byte-identical (the existing round-trip and 4.1 sample tests prove it) and stays safe to call off the UI thread for an immutable `Document`.
  - App: `src/RBX15.App/Services/AutosaveService.cs`: `AutosaveService(EditorSession, AppStorage, ITickSource, TimeProvider, string sessionId, Func<int> pid, Func<DateTime> processStart)`.
    - Every tick (60 s): if `IsDirty`, not `InGesture`, and the document instance differs from the last autosaved one, snapshot `Document`, `CurrentFile` and the time on the UI thread, then `ProjectIO.Write` and the atomic write (temp file in the same folder, then `File.Move(overwrite: true)`) on the thread pool. One write in flight at a time; a tick during a write is skipped.
    - The sidecar `<session-id>.json` (A2's fields) is written the same way, after the project file.
    - `Delete()` removes the pair. Called on `SessionChange.File` when the session reads clean (a Save or an Open), and by the window on a real close (after the prompt, beside `SaveWindowPlacement`).
    - `WriteNow()` for item 1's emergency autosave: synchronous, best effort, never throws.
    - Failures go to the status strip once per failure streak ("Couldn't autosave: <reason>"), never a dialog.
  - `ITickSource` (`DispatcherTimerTickSource` in the app, `ManualTickSource` fake).
  - Composition: `Build` takes `AppStorage`; `MainViewModel.Autosave` is a required member; `App.xaml.cs` passes `CrashHandler` the `WriteNow` delegate. `MainWindow.OnClosing` calls `Autosave.Delete()` only when the close is not cancelled.
  Done when:
  - A dirty document autosaves on the first tick; a second tick with no change writes nothing; a clean document never writes.
  - A tick inside an open gesture writes nothing, and the next tick after it ends does.
  - The written file reads back through `ProjectIO.Read` to a document `ContentEquals` the session's, and the sidecar names the pid, start time and original path.
  - A write killed between the temp file and the move (a fault-injection seam) leaves the previous autosave intact and no partial target.
  - Save, Open and a real close delete the pair; a cancelled close keeps it.
  - `WriteNow` with a dirty document writes synchronously.
  - Encoding the same image twice through `ProjectIO.Write` encodes once (counter seam), and the frozen ProjectIO round-trip tests pass unchanged.
  - No test writes outside its temp `AppStorage`.
  Verify: the standard three, plus `dotnet test src/RBX15.Engine.Tests -c Release --no-build --filter "FullyQualifiedName~ProjectIO"`.
  Stop class: go.
  Risk: the snapshot is the immutable `Document`, but `SKImage` encoding from a pool thread while the UI thread draws the same image must stay read-only. Pin it with a test that encodes on a pool thread while the UI thread renders.
  Depends on: 1.

- [ ] **3. Crash recovery: the restore prompt at launch**
  Rows: `upgrade-autosave-recovery` (the recovery half), `shell-no-unsaved-changes-guard`, `shell-lifecycle-quit`.
  Spec ref: §3 "Upgrades" ("the next launch after a crash offers to restore"); R1, R2, R3.
  What to build:
  - `src/RBX15.App/Services/RecoveryService.cs`: `FindOrphans()` lists sidecars whose owner pid is not running, or is running with a different start time (an `IProcessProbe` seam), newest first; an unreadable sidecar is skipped and logged, never deleted. `TryClaim(orphan, sessionId)` renames the pair to the caller's session id and returns false if another process won the rename.
  - `EditorSession.Recover(ProjectLoad load, string? originalPath)`: like `Open` (raises Changing, abandons gestures, clears history, releases outgoing images) but leaves the session dirty until the next `MarkSaved`, and sets `CurrentFile` to `originalPath` only when that file exists.
  - `IDialogService.AskRestore(string documentName, DateTimeOffset autosavedAt)` returning `RestoreChoice { Restore, Discard, Later }`, themed, with `DialogText.RestorePrompt`; the fake records calls.
  - `IAppLauncher` (`Process.Start` of `Environment.ProcessPath` with a quoted path in the app; a recording fake) for R3.
  - Startup order in `App.OnStartup` (wiring through a testable `StartupFlow` class, not inline): build the shell, show the window, then the recovery prompt (one orphan, R2), then item 4's launch file.
  Done when:
  - With a killed owner's pair present, the prompt shows once with the file name (or "Untitled") and the time; Restore gives a dirty session whose document `ContentEquals` the autosaved one, empty history, the title's `*`, and `CurrentFile` equal to the original path when it exists, else null.
  - Ctrl+S after a titled restore writes the original path, and the pair is gone.
  - Discard deletes the pair; Later keeps it and the next `FindOrphans` lists it again.
  - A pair owned by a live process (same pid and start time) is never offered.
  - Two `TryClaim` calls on the same orphan: exactly one wins.
  - A corrupt autosave file gives `DialogText.LoadFailed` and keeps the pair for the user (never deleted on a failed read).
  - `shell-lifecycle-quit`: a dirty close with Don't save exits with the pair deleted; a clean close shows no prompt (the existing R13 tests, cited).
  Verify: the standard three.
  Stop class: go.
  Risk: a crash during startup itself would offer its own still-claimed file next time. The claim renames to the new session id, so the next launch sees a dead owner and offers it again, which is the intended loop; pin it with a test.
  Depends on: 2.

- [ ] **4. Launch with a `.r15proj`, and a second project in a new window**
  Rows: `upgrade-file-association-launch`.
  Spec ref: §3 "Upgrades", File association; §7 manifest; S1, R3.
  What to build:
  - `src/RBX15.App/Services/LaunchArguments.cs`: `Parse(IReadOnlyList<string> args)` returns the first argument that is a fully qualified path to an existing file (any extension; `ProjectIO` refuses what is not a project), ignoring `--test-input-bridge` and other `--` flags. Quoted paths with spaces and Unicode survive.
  - `StartupFlow` (item 3) opens it through `ProjectFileService.OpenAsync(path)` after the recovery step. A failure is the existing `LoadFailed` alert over an empty document. With R3's Restore, the file goes to `IAppLauncher` instead.
  - No pipe, mutex or single-instance code (S1). Ctrl+O behavior is unchanged.
  - The manifest is unchanged. Add a build-msix assertion only if item 9 has not already (coordinate: item 9 owns build-msix).
  Done when:
  - `Parse` table: no args, a flag only, a missing file, a relative path, a quoted path with spaces, a Unicode path, a path plus the bridge flag.
  - A startup with a valid project path ends with that document open, clean, `CurrentFile` set and the folder remembered.
  - A startup with a corrupt file shows `LoadFailed` and leaves a new document.
  - With an orphan and a launch path: Restore keeps the recovered work here and asks `IAppLauncher` to open the path; Discard opens the path here.
  - The bridged launch still ignores everything but its flag.
  Verify: the standard three.
  Stop class: go.
  Risk: the packaged FileTypeAssociation passes the path as an argument to a full-trust app; that is checked on the real install at MAN-12, not assumed.
  Depends on: 3.

- [ ] **5. Recent projects and the taskbar jump list**
  Rows: `upgrade-recent-projects`, `upgrade-jump-list`, `shell-no-persistence` (the recents and jump list clauses).
  Spec ref: §3 "Upgrades", Recent projects; H1, J1. Carry-forward: `JsonSettingsStore` keeps sibling keys on a failed read (already fixed in M4a; recents must use the same `Update` path).
  What to build:
  - `ISettingsStore.LoadRecents()` and `SaveRecents(IReadOnlyList<string>)` on key `recentProjects` (a JSON array of full paths) in `JsonSettingsStore` and the fake.
  - `src/RBX15.App/Services/RecentProjects.cs`: `Add(path)` (most recent first, case-insensitive full-path dedupe, capped at 10), `Remove(path)`, `Clear()`, `Items` (pruned of files that no longer exist, on a pool thread so a dead network share never blocks the UI), and a `Changed` event. Fed by `ProjectFileService` on a successful Save, Save As or Open.
  - `src/RBX15.App/Services/JumpListPublisher.cs` over `IJumpListSink`: on `Changed`, publishes one `JumpTask` per item in a custom "Recent" category (Title = file name, Description = full path, Arguments = the quoted path, ApplicationPath = this exe, IconResourcePath = this exe). The app's sink wraps `System.Windows.Shell.JumpList` on `Application.Current`; `JumpItemsRejected` and `JumpItemsRemovedByUser` are handled (a removed item leaves the recents too).
  - File group: a "Recent" drop-down button beside Open (`FileGroupViewModel.Recent`, items with `OpenRecentCommand`, plus "Clear recent list"). Opening a recent goes through `ProjectFileService.OpenAsync(path)` with its save prompt; a missing file shows the status line "<name> was moved or deleted." and leaves the list.
  - Automation names on the button and each item; the FocusRing style.
  Done when:
  - Add 12 distinct paths gives the newest 10 in order; re-adding the 3rd moves it to the top with no duplicate; a path differing only in case is one entry.
  - A deleted file is pruned from `Items` and from the next save.
  - `settings.json` keeps `window`, `lastProjectFolder` and `lastAssetFolder` across a recents write, and a recents write after a failed read is skipped (the M4a rule).
  - Save, Save As and Open each add; a cancelled or failed one does not; a restore does not.
  - The publisher hands the sink 10 tasks with the exact arguments and category; Clear hands it an empty list; a user-removed item leaves the recents.
  - The drop-down opens a recent through the save prompt, and a missing one gives the status line.
  - `ShellTests` and `HomeRibbonViewTests` pins for the File group are updated, and the ribbon measure test still passes (Home already scrolls at 1200, so only report the new width).
  Verify: the standard three.
  Stop class: go.
  Risk: two windows (S1) write recents at nearly the same time. `JsonSettingsStore` does a read-modify-write per update, so one window's add can be lost in a race of a few milliseconds. Accept it, and say so in the item report; each window re-reads before it writes.
  Depends on: 4 (a jump-list click is a launch with a file).

- [ ] **6. Paste as layer (Ctrl+V)**
  Rows: `upgrade-paste-as-layer`.
  Spec ref: §3 "Upgrades", Paste as layer; §4 "Keyboard" (New: Ctrl+V); P1, P2. Pointers: `kbd-shortcuts`, `keys-tool-shortcuts` and `kbd-ctrl-combo-tool-toggles` say "Ctrl+V's paste moves to M5's paste row".
  What to build:
  - `src/RBX15.App/Services/IClipboardSource.cs`: `ClipboardContent Read()` returning one of `Png(byte[])`, `Files(IReadOnlyList<string>)`, `Bitmap(byte[] pngBytes)` or `None`, in P2's order. The app's `WpfClipboardSource` reads `Clipboard.GetDataObject()`: the "PNG" format as a stream; `FileDropList`; then `CF_DIBV5` or `Clipboard.GetImage()` encoded to PNG with `PngBitmapEncoder` (alpha kept when the format carries it). It retries `COMException` (CLIPBRD_E_CANT_OPEN) 3 times over about 100 ms, then reports the failure.
  - `src/RBX15.App/Services/PasteService.cs`: `Paste()` over `ImportService`: PNG and bitmap bytes go through the size cap and `Images.Decode`, then one `ApplyWithSelection` with `LayerOps.AddImage(s, image, id, name: null)` (front-slot fit, as Add image); files go through `ImportService.Import(paths)`. Raises `LayersAdded` (the Select tool). Nothing usable gives P2's status line and no step.
  - `MainWindow`: `AddShortcut(main.Paste.PasteCommand, Key.V, ModifierKeys.Control)`, so a focused text field keeps Ctrl+V.
  - Update `ShellTests.The_window_binds_exactly_the_seven_chords_and_not_ctrl_v` to eight chords, renamed `The_window_binds_exactly_the_eight_chords` (the ledger cites the old name; item 17 rewrites those parityChecks).
  Done when:
  - A PNG with a 50% alpha pixel pastes as one layer whose pixel keeps that alpha, fit in the front slot exactly as Add image places it, selected, Select tool on, one undo step that one Ctrl+Z removes.
  - PNG wins over a bitmap on the same data object; a file list of two PNGs and a `.txt` adds two layers in order in one step and names the skipped file.
  - An empty clipboard and a text-only clipboard each give the status line and no step.
  - A clipboard that throws CLIPBRD_E_CANT_OPEN 3 times gives a status line; once then success pastes.
  - An oversized image (over 16384 per side) is refused through the caps with a status line.
  - Ctrl+V with the Word Art text box focused does not paste a layer; Keys OFF does not block Ctrl+V; Ctrl+V still toggles no tool (`ShortcutRouterTests`, unchanged).
  - Works in Pants mode (front slot from `Template.Slot`).
  Verify: the standard three.
  Stop class: go.
  Risk: a browser's copied image often carries only `CF_DIB` (premultiplied or with a garbage alpha channel). MAN-13 checks Edge and the Snipping Tool; a DIB whose alpha channel is all zero is treated as opaque.
  Depends on: 5 (shares `MainWindow.xaml.cs`, `Composition.cs` and `ShellTests`).

- [ ] **7. The canvas frame budget: cache the static composite during a stroke**
  Rows: `render-pipeline-canvas-reset`.
  Spec ref: ledger parityCheck ("closes when the static-layer composite is cached during a stroke, or Este narrows the budget"); C1. M4b run 2, D12.
  What to build:
  - In the render path the canvas host uses (`ViewRenderer` and `Renderer.RenderView` in the engine; `TemplateCanvasHost` in the app): while a live preview stroke is active, cache background, region fills and every layer as one `SKImage` keyed on the `Document` instance, the mode and the view size and DPI. Draw the live preview, the mask, the grid, the selection overlay and the cursor over it each frame. Any document, mode, size or DPI change invalidates the cache; stroke end releases it.
  - The composite order must stay exactly as today (the mask and overlays above the preview), so the cache holds only what sits below the preview.
  Done when:
  - Frames drawn with the cache are byte-identical to frames drawn without it, for a pen, a line, a rectangle and an eraser stroke over the 20-layer document (a test that renders both ways).
  - `FrameBudgetTests.A_pen_stroke_over_twenty_layers_reports_its_frame_times` reports mean and p95 for both documents (rotated and translucent; with color filters too). Under `RBX15_PERF=1` it asserts p95 under 16.7 ms for both.
  - A stroke allocates the cache once (counter seam), and an undo or mode change mid-stroke rebuilds it.
  - The existing `FrameBudgetTests` facts and the render goldens pass unchanged.
  Verify: the standard three, plus `$env:RBX15_PERF=1; dotnet test src/RBX15.App.Tests -c Release --no-build --filter "FullyQualifiedName~FrameBudgetTests"` (report the numbers, run twice).
  Stop class: go, but **hold** with the measured numbers if the filtered document's p95 is still over 16.7 ms; that is C1's "If declined" call for Este.
  Risk: the eraser's live preview cuts into the selected layer, so for the eraser the selected layer cannot sit in the cache. Cache the layers below it and draw the selected layer and those above it live.

### M6

- [ ] **8. The asset library at scale**
  Rows: `asset-objecturl-lifecycle`.
  Spec ref: §3 "Asset library" (thumbnails decode lazily at thumbnail size); M4a ruling (500 images and a 20,000-file folder).
  What to build (App tests only, unless the measurement exposes a defect):
  - `AssetLibraryScaleTests`: generates 500 PNGs of 1024x1024 into a temp folder (deterministic, deleted after) and a 20,000-file folder (19,500 non-images plus the 500), loads each through `AssetLibraryViewModel` and the real `AssetThumbnailLoader` in a shown `AssetLibraryPanel`.
  - Reports: folder scan time, realized containers after load and after scrolling to the end, decoded thumbnail pixel size, and the process's private bytes before and after.
  Done when:
  - All 500 thumbnails resolve (functional parity), each decoded at thumbnail size, never at 1024x1024.
  - Realized containers stay under a bound proportional to the viewport (virtualization holds), not 500.
  - The 20,000-file scan lists exactly the 500 images and keeps the UI thread free while it scans (a dispatcher responsiveness probe).
  - Numbers reported; under `RBX15_PERF=1` the private-bytes growth stays under 300 MB and the scan under 5 s (provisional; recorded in the item report).
  Verify: the standard three.
  Stop class: go. A defect the test exposes is fixed test-first and named in the commit; a design change (for example a new cache) holds.
  Risk: generating 500 large PNGs costs suite time. Generate them once per test class in a fixture, and skip the 20,000-file case unless `RBX15_PERF=1` (report it in the item).

- [ ] **9. Package and CI gates**
  Rows: `msix-identity`, `msix-application-entry`, `msix-target-os`, `msix-arch-self-contained`, `ci-windows-ci`, `ci-windows-release`, `packs-resolve-dir` (the package half), `msix-capabilities` (the manifest and network-scan half).
  Spec ref: §7 "Package"; L1. Carry-forward (M2b): verify the MSIX keeps assets and fonts.
  What to build:
  - `scripts/build-msix.ps1`, after unpack: assert the package holds `packs\drips` with 156 PNGs and `packs\word_art` with 25, the 18 font families from `assets/fonts` (one folder each, each with its OFL license), `libSkiaSharp.dll` and `libHarfBuzzSharp.dll` (HarfBuzzSharp is an Engine dependency); assert `MinVersion` is `10.0.17763.0`; print the MSIX size, the zip size and the installed (unpacked) size. Each failure throws with a named reason.
  - `scripts/Test-BuildMsix.ps1` (no packing, no signing): renders the manifest template and runs the assertion functions against it, plus negative cases (a wrong version, a second capability, a missing `.r15proj`, a missing pack). Factor the assertions into `scripts/msix-contract.psm1` so the build and the test share them.
  - `tools/ci/test_workflows.py` (unittest, text parsing, no PyYAML): `dotnet-ci.yml` runs on PRs touching `src/**`, `packs/**`, `tests/goldens/**` and `tools/**`, and not on docs-only paths; `v5-release.yml` triggers only on `v5.*` tags, checks the tag against the csproj version, refuses the placeholder notes, runs the ledger gate and `dotnet test` before `build-msix.ps1`; `windows-release.yml` is fenced to `v4.*`. Add a step to `dotnet-ci.yml` that runs `python -m unittest discover -s tools/ci`.
  - No signing, no certificate, no Partner Center call, no tag.
  Done when:
  - `Test-BuildMsix.ps1` passes and each negative case fails with its reason.
  - `python -m unittest discover -s tools/ci -v` passes, and breaking each asserted line in a scratch copy makes it fail.
  - The item report lists the MSIX, zip and installed sizes from one local `build-msix.ps1` run without `-SelfSign` (the default is unsigned). If the Windows SDK is missing on the worker's machine, it reports the sizes from `dotnet publish` and leaves the full build to the controller (item 16).
  Verify: the standard three, plus `pwsh scripts/Test-BuildMsix.ps1` and `python -m unittest discover -s tools/ci -v`.
  Stop class: go. **Stop** if anything needs a certificate, signing, the Store or a pushed tag.
  Risk: `build-msix.ps1` is a release path. Keep every new assertion additive, and keep the bridge scan step exactly as it is.

- [ ] **10. The UI flow suite and the window rows**
  Rows: `parity-ui-automation-suite`, `shell-window-size`, `shell-window-frame`.
  Spec ref: §6 Tier 3 ("a thin suite covering launch, add image, region paint, Word Art, undo, save and reopen, and export"); §4 "Window"; U1.
  What to build (App tests only):
  - `src/RBX15.App.Tests/UiaKit.cs`: finds elements by AutomationId or Name through `UIElementAutomationPeer.CreatePeerForElement` and the peer tree, and drives Invoke, Value, Toggle, SelectionItem and ExpandCollapse. No mouse, no keyboard device, no `SendInput`.
  - `src/RBX15.App.Tests/UiFlowTests.cs` over a shown `MainWindow` built by `Composition.Build` with fake dialogs (temp files), one fact per spec flow:
    - Launch: the title, four tabs, Home active, Undo and Redo disabled, a clean document.
    - Add image: Invoke "Add image" with the fake picking a PNG; one layer, selected, in the front slot.
    - Region paint: with nothing selected, Invoke the Torso Front tile; its fill equals the current Fill Style; no dialog.
    - Word Art: set the text through ValuePattern, Invoke "Add to canvas"; one layer named "✨ <text> [Anton]".
    - Undo: Invoke Undo three times; the document returns to new.
    - Save and reopen: Invoke Save (fake path), then New state, then Open the same path; `ContentEquals` the saved document; the title has no `*`.
    - Export: Invoke Download; the fake path holds a 585x559 PNG byte-equal to `Renderer.ExportPng` of the document.
    - Paste and Recent: Ctrl+V through the window's KeyBinding with a fake clipboard; the Recent drop-down lists the saved file.
  - Window rows: with no saved placement, the window opens at 1200x800 DIP centered in the primary work area (`PlacementGuard`, cited, plus a shown-window fact through the real DPI); resizing to 640x480 lays out with no exception and no negative column; below the minimum the window refuses; the caption attributes (`CaptionThemeTests`, cited).
  Done when the suite passes in the normal run (no env var), takes under 60 s, and leaves no window open.
  Verify: the standard three.
  Stop class: go.
  Risk: the export and Save flows go through real file writes; keep them in a per-test temp folder. A flow that needs a real dialog stays on the fakes; the native dialogs are the controller's smoke.
  Depends on: 6.

- [ ] **11. The accessibility pass**
  Rows: `a11y-focus-rings`, `docs-known-limitations` (the accessibility clause).
  Spec ref: §4 "Accessibility"; X1.
  What to build:
  - `AccessibilitySweepTests` (with `UiaKit`): for each of the four tabs (with a layer selected, so Placement shows), the left panel, the themed dialog, the color picker and the recovery prompt: every element whose peer is keyboard-focusable or supports Invoke, Toggle, Value, RangeValue or SelectionItem has a non-empty Name, and every focusable `Control` resolves the `FocusRing` focus visual style.
  - A Tab-order fact: tabbing from the first ribbon tab cycles through the active pane, the asset browser, the drop zone, the layer rows, the Selected layer panel and the canvas, and reaches at least one region tile, one Background swatch and one layer row.
  - Fix what the sweep finds: missing names and missing focus styles only, each fix in the same commit. A control that cannot be made focusable without a look change holds for Este.
  - Note the X1 limitations in a comment block on the test class for item 15's release notes.
  Done when the sweep and Tab-order facts pass and the item report lists every fix.
  Verify: the standard three.
  Stop class: go, **hold** on a look change.
  Depends on: 10 (`UiaKit`).

- [ ] **12. PRIVACY, SECURITY and the network claims**
  Rows: `docs-privacy-claims`, `docs-security-md`, `claim-network-surface-inconsistent`, `shell-userdata-footprint`.
  Spec ref: §3 "Fonts and network" and "Upgrades" (PRIVACY discloses recents, autosave, window position and the error log); D1.
  What to build (docs only):
  - `docs/PRIVACY.md`: new "Last updated"; the data table gains settings.json (window position, last folders, the 10 recent paths), the autosave folder (a copy of unsaved work, deleted on save or clean close), and the error log (no content, rolled at 256 KB, never sent), with their folder (A1) for the Store build and the zip, and that uninstall removes them in the Store build and that zip users delete `%LOCALAPPDATA%\RBX15` by hand. Network section: "The app makes no network requests." Remove the Electron cache and Google Fonts text. Keep every other claim, the trademark section and the third-party list.
  - `SECURITY.md`: "never phones home" stays true; scope becomes the WPF host, SkiaSharp decoding of untrusted images (the 100 MiB, 16384 and 50 MP caps), the `.r15proj` loader (all-or-nothing, `data:` URLs only, the 512 MiB file cap), the clipboard paste path, the autosave and recovery files, and the MSIX. Remove the Electron, IPC and NSIS items.
  - Every number in the two files is checked against the code constants and cited in the commit body (`Images.MaxEncodedBytes`, `ProjectFileService.MaxProjectBytes`, `ErrorLog` roll size, the recents cap).
  Done when both files say nothing about Electron, Google Fonts or "once per install"; every stored item the app writes is listed; and `markdownlint` (the repo config) reports nothing new.
  Verify: the standard three (unchanged counts), plus `npx markdownlint-cli@0.42.0 docs/PRIVACY.md SECURITY.md` if Node is available; else report it as not run.
  Stop class: go.
  Depends on: 1, 2, 5.

- [ ] **13. README, CONTRIBUTING, the landing page and the product card**
  Rows: `docs-readme-claims`, `docs-dev-workflow-claims`, `tests-count-claims`, `legacy-docs-superpowers`, `docs-feedback-md`, `claim-undo-everything`, `claim-fullscreen-canvas`, `wordart-claim-wrap-any-size`, `docs-index-landing`, `docs-manifest-626labs`.
  Spec ref: §7 "Store 5.0" and "Cutover"; D1, D2, D3.
  What to build (docs only):
  - `README.md`: install from the Store (link `https://apps.microsoft.com/detail/9MV9G4XFJ8S0`) or the portable zip; "quit the app before updating"; build from source with the .NET 10 SDK (`dotnet build`, `dotnet test`, `dotnet run --project src/RBX15.App`, `scripts/build-msix.ps1`); the Controls table with redo (Ctrl+Y, Ctrl+Shift+Z), Ctrl+O, Ctrl+S, Ctrl+Shift+S and Ctrl+V; "undo covers every edit"; Word Art "explicit line breaks only, placed at 85% of the front"; D2's wording; the upgrades (autosave, recents, jump list, open from Explorer, paste). No hand-written test counts: point at the CI summary.
  - `CONTRIBUTING.md`: the .NET solution layout (Engine, App, Parity, tests), the ledger and the goldens rules, and the add-a-detail, add-a-style and add-a-preset recipes in C#. The `docs/superpowers` 4.0 specs are named as history.
  - `feedback.md`: each of the four fixed findings names its v5 regression test.
  - `docs/index.html`: v5.0, the Store link, no Google Fonts `<link>` (bundle or system fonts), no "Electron 41", no "Setup.exe", no "pending", Word Art and fullscreen copy corrected, screenshots per D3 (from `docs/screenshots/5.0/` once item 16 lands them; until then keep the 4.1 images and leave a `<!-- D3 -->` marker for item 16).
  - `.626labs/manifest.json`: version 5.0.0, `tech.stack` `["wpf", "dotnet", "skiasharp"]`, `source_language` csharp, build `dotnet/msix`, no "electron" tag, description "native Windows desktop editor", distribution Store MSIX plus the portable zip; ids and Store ids kept. Valid JSON.
  Done when a grep of the five files finds no "Electron", "npm", "Node.js", "Setup.exe", "fonts.googleapis" or "pending" outside a marked "Legacy 4.1" note; the manifest parses; and every Controls row names a binding that `ShellTests` or `ShortcutRouterTests` pins.
  Verify: the standard three (unchanged counts), plus `python -c "import json;json.load(open('.626labs/manifest.json'))"`.
  Stop class: go.
  Depends on: 6.

- [ ] **14. SMOKE_TEST and TEST_PLAN rewritten for v5**
  Rows: `tests-manual-smoke`, `tests-windows-test-plan`, `claim-region-click-color-picker`.
  Spec ref: §6 "Manual: docs/SMOKE_TEST.md rewritten for v5"; the upgrades.
  What to build (docs only):
  - `docs/SMOKE_TEST.md`: Part A keeps the four revival regressions in v5 terms; Part B covers launch, layers, adjustments, Word Art's six styles, Details, Placement and Presets, Save, Open and Export, Mode, plus the upgrades (autosave and recovery by ending the task, recents and the jump list, a double-clicked project, paste). Each step names what the automated suite already proves so the person skips nothing and repeats nothing. The diagnostic is the error log path (E1), not DevTools. Region clicks paint the current Fill Style; no picker opens.
  - `windows/TEST_PLAN.md`: section 1 is the MSIX install (Store or sideload) and the zip; section 2 the v5 first launch (1200x800 centered, the 626Labs mark, "V5.0"); section 13 the real region behavior; section 14 "zero network connections"; section 15 uninstall, including the local data folder; the MSIX submission checks updated (identity strings, runFullTrust only, tile images, MinVersion).
  Done when neither file mentions Electron, NSIS, DevTools, Ctrl+Shift+I or a color picker on a region click, and each upgrade has a step.
  Verify: the standard three (unchanged counts).
  Stop class: go.
  Depends on: 6.

- [ ] **15. Store 5.0 copy, the reviewer letter and the release-notes draft**
  Rows: `docs-store-listing`, `docs-reviewer-letter`, `docs-store-assets`, `msix-capabilities` (the reviewer-letter half), `docs-known-limitations` (the release-notes clause).
  Spec ref: §7 "Store 5.0" (What's new; the "native" claim now accurate; the reviewer letter with the runFullTrust justification; "quit before updating").
  What to build (files only; nothing is submitted):
  - `docs/store/listing-copy-5.0.0.md`, in the 4.1.0 file's shape: description, features and keywords kept where still true; "native" now accurate; D2's canvas wording; "undo everything" now true; a What's new (native app, offline fonts, autosave and recovery, recents and jump list, open from Explorer, paste, accessibility, quit before updating); the Roblox trademark disclaimer verbatim. Nothing claimed that the build does not do.
  - `docs/store/reviewer-letter-5.0.0.0.md`: native WPF on .NET 10, self-contained; no network; runFullTrust justification ("a Win32/.NET desktop app cannot run in the AppContainer; it reads and writes only user-chosen files, plus its own settings, autosave and error log in its local app data"); the 15 bundled Word Art fonts and 3 UI fonts under the OFL, by name; no account, no telemetry; the pre-submission checklist (5.0.0.0, identity strings, runFullTrust only, tile images, MinVersion 10.0.17763.0).
  - `windows/RELEASE_NOTES_v5.md`: keep the placeholder marker (only `/cut-release` removes it), and write the user-facing sections below it: What changed, the upgrade note, and Known issues (X1's limitations, the unsigned zip and SmartScreen). The commit bullets stay for `/cut-release`.
  - `docs/store-assets`: a test or script check that each listed PNG has its stated pixel size (the row's check), reused from `make-store-assets.py` constants where it has them.
  Done when every claim in the two Store files maps to a closed ledger row or a test (the item report lists the mapping); the release-notes marker is still present; and the asset size check passes.
  Verify: the standard three, plus the asset size check.
  Stop class: go. Writing the files is go; submitting them is the "Este / release and after certification" list.
  Depends on: 6, 11.

- [ ] **16. Smoke, screenshots and the manual list (controller)**
  Rows: evidence for the rows listed under item 16 in the scope table.
  What to build (scratch in `.superpowers/m5m6-smoke/`, plus `docs/port/m5m6-manual.md` and `docs/screenshots/5.0/`):
  - Extend the M4b bridge driver (`brg`) against a `-p:EnableTestInputBridge=true` Release build:
    - autosave: edit, wait one 60 s tick, confirm the pair exists; close with Don't save, confirm it is gone.
    - recovery: edit, wait a tick, end the process from the driver (`Stop-Process`), relaunch, answer the prompt through UIA, confirm the restored layers and the `*` title.
    - a launch with a project path, and a second launch with another path: two windows, two documents.
    - recents: save two files, confirm the drop-down lists both through UIA.
    - error log: the bridged build's test-only "throw" command if the bridge has one; otherwise skip and leave it to the unit tests.
    - Cold start: zero sockets (the M4b rule), first-frame time.
  - Build the MSIX and zip with `scripts/build-msix.ps1` (no `-SelfSign`, no install) and record the sizes.
  - D3: PrintWindow captures of the Release build at 1200x800 into `docs/screenshots/5.0/` (Home, Background, Details, Text, a finished shirt), and swap item 13's `<!-- D3 -->` marker in `docs/index.html` and README.
  - Write `docs/port/m5m6-manual.md` in `m4b-manual.md`'s format, MAN-10 onward:
    - MAN-10, crash recovery: edit, wait 70 s, end the task in Task Manager, relaunch, Restore; Ctrl+S writes the original. Rows `upgrade-autosave-recovery`.
    - MAN-11, the jump list on the packaged install: right-click the taskbar icon, the Recent category lists the last saved files; clicking one opens it in a new window. Row `upgrade-jump-list` (J1).
    - MAN-12, Explorer: double-click a `.r15proj` with the app closed, then with it open (a second window); the file icon and "Open with" entry. Row `upgrade-file-association-launch`.
    - MAN-13, paste: an image with transparency copied from Edge, a Snipping Tool capture, and a PNG copied in Explorer. Row `upgrade-paste-as-layer`.
    - MAN-14, Narrator: start Narrator, Tab through Home and the left panel, the tab names, buttons, sliders and layer rows read sensibly. Rows `a11y-focus-rings`, `docs-known-limitations`.
    - MAN-15, Windows Sandbox: unzip the portable zip into Sandbox (no .NET installed), launch, add an image, export. Row `msix-arch-self-contained`.
    - MAN-16, the packaged install: the Start tile reads "RBX15", the splash shows, Drips lists 156 and Word Art 25, and after an edit and a tick the autosave and log folders exist under the package's local data, not the real `%LOCALAPPDATA%\RBX15`. Rows `msix-application-entry`, `packs-resolve-dir`, `shell-userdata-footprint`.
    - MAN-17, the Windows App Certification Kit against the built MSIX (admin). Row `msix-capabilities` (or L1).
    - MAN-18, the caption: drag-resize to the 640x480 minimum, the snap layouts flyout on the maximize button, and the caption color. Row `shell-window-frame`.
  Done when the smoke log shows every bridge item passing or triaged as in M4a's D8 and D9 (driver fixes only; product defects written up, not fixed in the smoke), the screenshots and the manual list exist, and the package built.
  Verify: the driver refuses any display scale but 100%; the synthetic-input grep is unchanged; `git status --short` shows only the manual list, the screenshots and the two marker swaps outside `.superpowers/`.
  Stop class: go for the automated run. **Stop** before any install, signing or certificate step; those are MAN-16 and Este's. MAN results arrive asynchronously, and item 17 closes only what they pass.

- [ ] **17. Ledger close and final verification**
  Rows: all in the scope table, plus the 7 new rows.
  What to build: `.superpowers/logs/m5m6-ledger.py` in M4b's form. It:
  - adds the 7 new rows (scope table fields; `behavior` and `portTarget` from the spec; `source` "spec §3" or "spec §6"; `parityCheck` from the evidence);
  - gates each row on its items' evidence trailers, the smoke log and the MAN results;
  - rewrites the parityChecks that cite `ShellTests.The_window_binds_exactly_the_seven_chords_and_not_ctrl_v` to item 6's new name, and points the three "M5's paste row" narrowings at `upgrade-paste-as-layer`;
  - applies L1's narrowings ("Narrowed in M6") and records each moved clause in the "Este / release and after certification" list below by row id;
  - appends divergence text where a decision changed a promise (S1 on `shell-no-persistence`'s neighbors if needed, D2 on `claim-fullscreen-canvas`);
  - confirms `parity-store-update-path` reads done after PR #56, and reports it if #56 has not merged;
  - closes everything proven, leaves MAN-gated rows open with their MAN id, then runs `python tools/ledger/ledger.py render` and `check`.
  Done when `ledger.py check` reports 0 problems, the only open rows are the 25 Retired plus any MAN-gated rows with their MAN id, and the final build and test are green.
  Verify: the standard three, plus `python tools/ledger/ledger.py check` and `git status --short` (only `docs/port/parity-ledger.json`, `parity-ledger.md` and `divergences.md`).
  Stop class: go.

Dependencies: 1 → 2 → 3 → 4 → 5 → 6 (one chain; every link edits `App.xaml.cs`, `Composition.cs`, `TestShell.cs`, `MainWindow.xaml.cs` or `ShellTests.cs`). 7, 8 and 9 have no dependency and may run any time. 10 after 6; 11 after 10. 12 after 1, 2 and 5; 13 and 14 after 6; 15 after 6 and 11; 12-15 may run in parallel. 16 after 1-15. 17 last.

---

## 3. Contracts

**AppStorage** (`src/RBX15.App/Services/AppStorage.cs`, item 1): `sealed record AppStorage(string Root) { SettingsDirectory; AutosaveDirectory; LogDirectory; }`. `App.xaml.cs` makes one from `App.SettingsDirectory`; the bridged launch from `%TEMP%\rbx15-bridge-<pid>`; tests from a temp folder.

**Composition** (`src/RBX15.App/Composition.cs`): both `Build` overloads gain `AppStorage storage`, and the injectable overload gains `IClipboardSource clipboard`, `IJumpListSink jumpList`, `ITickSource autosaveTicks`, `IAppLauncher launcher` and `IProcessProbe processes` (defaults only in the app overload). New lines go above `// M3a: construction lines above this line.`, in this order:

```text
var recents = new RecentProjects(settings);
var jumpListPublisher = new JumpListPublisher(recents, jumpList);
var autosave = new AutosaveService(session, storage, autosaveTicks, TimeProvider.System, sessionId, ...);
var recovery = new RecoveryService(storage, processes);
var paste = new PasteService(session, import, clipboard, status);
paste.LayersAdded += (_, _) => tools.Set(Tool.Select);
```

`ProjectFileService` gains `RecentProjects` (it adds on success). `TestShell.Main` mirrors every line with fakes.

**MainViewModel** gains required members `Autosave`, `Recovery`, `Recents` and `Paste`. `FileGroupViewModel` gains `Recent` (items and commands).

**EditorSession** gains `Recover(ProjectLoad load, string? originalPath)` (dirty until `MarkSaved`).

**Services:**

- `ErrorLog(string directory, TimeProvider clock, long rollBytes)`, `Write(Exception, string source)`
- `CrashHandler { HandleDispatcher, HandleDomain, HandleUnobservedTask }` with injected log, emergency autosave, fatal dialog and exit
- `AutosaveService { Tick(), WriteNow(), Delete(), event Failed }`
- `RecoveryService { FindOrphans(), TryClaim(orphan, sessionId), Load(orphan), Delete(orphan) }`
- `LaunchArguments.Parse(IReadOnlyList<string>)`
- `StartupFlow` (recovery, then the launch path, then R3)
- `RecentProjects { Items, Add, Remove, Clear, event Changed }`, `JumpListPublisher`
- `PasteService { PasteCommand, Paste(), event LayersAdded }`
- Seams: `ITickSource`, `IProcessProbe`, `IAppLauncher`, `IJumpListSink`, `IClipboardSource`, each with an app implementation and a fake in `src/RBX15.App.Tests/Fakes/`.

**ISettingsStore** gains `LoadRecents()` and `SaveRecents(IReadOnlyList<string>)`, key `recentProjects`, through the existing `Update` path.

**IDialogService** gains `AskRestore(string documentName, DateTimeOffset autosavedAt)` → `RestoreChoice` and `ShowFatal(string message)`. `DialogText` gains `RestorePrompt`, `FatalError` and the paste and recents status lines; the dialog inventory test lists them.

**Engine:** `ProjectIO.PngDataUrl` caches per `SKImage` (item 2). Item 7's composite cache lives in `ViewRenderer` (engine, no WPF).

**Window** (`MainWindow.xaml.cs`): eight chords (Ctrl+V added through `AddShortcut`); `OnClosing` deletes the autosave pair on a real close.

**Existing tests whose pinned structure changes:**

- `ShellTests.The_window_binds_exactly_the_seven_chords_and_not_ctrl_v` (eight chords, renamed)
- the File group facts in `FileGroupTests`, `HomeRibbonViewTests` and `ShellTests` (the Recent button)
- `RibbonMeasureTests` (report Home's new width; Home already scrolls)
- `DialogInventoryTests` (the new strings)
- `JsonSettingsStoreTests` (the new key keeps its siblings)
- every `Composition.Build` caller (`TestShell`, `ShellWiringTests`)

---

## 4. Estimate and risks

**Estimate:** 17 items, about 24-32 worker-hours. Items 2, 3, 5, 10 and 13 are L; 4, 6, 7, 8, 9, 11, 12, 14, 15 and 16 are M; 1 and 17 are S to M. As sessions:

- Rulings: Este answers the table above (18 rows; most are confirm-the-recommendation).
- 2 unattended Cart runs: items 1-9 (the M5 chain plus the three independent items), then 10-15.
- 1 session for the smoke, the MAN list and the ledger close (16-17), with the usual triage pass.

Total: 4 sessions, about M4b's size. Then the "Este / release and after certification" list, which is mostly waiting on Microsoft.

**Biggest risks, highest first:**

1. **Recovery correctness (items 2-4).** A recovery feature that loses work or offers the wrong file is worse than none. The traps: two processes (S1) claiming the same orphan, a crash during startup, a restore that reads clean (so the close prompt never asks), and a titled restore that overwrites the user's file before they choose to save. Each has a named test; MAN-10 is the real crash.
2. **The packaged jump list and file launch (items 4-5).** Whether a WPF `JumpTask` and a FileTypeAssociation launch behave the same inside the MSIX as in the zip is checked only on the real install (MAN-11, MAN-12). J1's fallback is a TFM change, which holds for Este.
3. **The canvas budget (item 7).** Caching below the live preview must stay byte-identical, and the eraser cuts into the selected layer, so the cache splits around it. If the filtered document still misses 16.7 ms, C1 goes back to Este.
4. **Clipboard formats (item 6).** Browsers and the Snipping Tool put different formats on the clipboard; DIB alpha is unreliable. The unit tests use synthetic data objects; MAN-13 is the real check.
5. **Docs drift (items 12-15).** Four docs items written in parallel can disagree on a number or a claim. Each cites code constants, and item 15 maps every Store claim to a row.
6. **Release gate ordering.** `v5-release.yml` refuses any open row, and on main it counts the 25 Retired rows too. PR #54 (skip Retired) must merge before the tag, and L1's narrowings must be in, or the tag fails.

### Este / release and after certification

Not buildable by the run. Each item names what it needs.

| Step | Needs | Rows it finishes |
|---|---|---|
| Merge PR #54 (the release gate skips Retired rows) and PR #56 (the upgrade test) | Este's review | `parity-store-update-path`; unblocks the tag |
| Run the MAN list (MAN-10 to MAN-18) | Este at the machine, the dev-signed 5.0 installed (MAN-11, 12, 16, 17), Windows Sandbox enabled (MAN-15), admin (MAN-17) | per the MAN list |
| Restore Este's machine after the packaged MAN checks (`Test-Upgrade.ps1 -Cleanup`, uninstall, reinstall from the Store) | Este | none |
| Store screenshots: reuse `docs/screenshots/5.0/` or reshoot | Este's call (D3) | `docs-store-listing` (screenshot clause) |
| `/cut-release` for v5.0.0, then push tag `v5.0.0` | Este's go; it publishes a GitHub release | `ci-windows-release` (the tagged-run clause, L1) |
| `/store-api rbx15` with `listing-copy-5.0.0.md`, the What's new and the reviewer letter; Este's Packages and Notes pages in Partner Center; `/store-commit rbx15` | Este's Partner Center session and the release MSIX | `msix-identity` (validation clause, L1), `msix-capabilities` (certification clause, L1) |
| A GitHub note pointing NSIS users to the Store or the zip | Este's go | none (`dist-github-nsis-channel` is Retired) |
| After certification: the cutover PR (delete the Electron app, `package.json`, the Node tests and `tools/parity-ref`; keep the goldens; trim the "Legacy 4.1" notes; rewrite the repo keystone; close the 25 Retired rows) | Certification | the 25 Retired rows |

### Critical files for implementation

- src/RBX15.App/App.xaml.cs (with the new src/RBX15.App/Services/StartupFlow.cs)
- src/RBX15.App/Composition.cs (with src/RBX15.App.Tests/TestShell.cs)
- src/RBX15.App/EditorSession.cs
- src/RBX15.App/Services/ProjectFileService.cs and JsonSettingsStore.cs
- src/RBX15.App/MainWindow.xaml.cs
- src/RBX15.Engine/ProjectIO.cs and ViewRenderer.cs
- scripts/build-msix.ps1
- docs/port/parity-ledger.json
