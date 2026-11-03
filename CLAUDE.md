# RBX15 Classic Shirt and Pants Maker

> **Persona:** inherits The Architect from the global and estate keystones. Nothing here restates them.

A native WPF (.NET 10) editor for Roblox Classic shirt and pants templates, shipped through the Microsoft Store as 5.x (Store ID `9MV9G4XFJ8S0`) and as a portable zip. The architecture, the build and the contribution rules are in [CONTRIBUTING.md](CONTRIBUTING.md). This file holds what that doesn't.

## Gotchas

- **Never change `tests/goldens/`.** The images are frozen 4.1 output, and `source-4.1/` holds four 4.1 source files kept byte for byte from `v4.1.0` that tests read through `RepoRoot.Source41`. A failing golden is an engine bug. `.gitattributes` marks the folder `-text` so the line numbers tests cite never shift.
- **4.1 lives only at the `v4.1.0` tag.** The cutover (2026-11-03) deleted the Electron app. Comments citing `editor.js:<line>` mean `git show v4.1.0:editor.js`.
- **The ledger is JSON-first.** Edit `docs/port/parity-ledger.json`, then `python tools/ledger/ledger.py render`. Never hand-edit the `.md` views. All 437 rows are done, and `v5-release.yml` refuses a tag with any open row.
- **A `v5.*` tag creates a draft release, on purpose.** Publish it only after the Store serves the new version: check `displaycatalog` for the version in `PackageFullName`, then retitle the draft `<tag> — Live on the Microsoft Store`. The 6deux6 Discord feed announces published releases. `tools/ci/test_workflows.py` pins `draft: true` and allows only one tag workflow.
- **Store listings are written in the store-listing-console** (`apps/rbx15`), not here. A submission with new screenshots or a trailer goes through Partner Center by hand: the API can't upload media, and editing the listing page on an API-written submission kills the API's record.
- **Partner Center counts a line break as two characters.** The Notes for certification limit (2000) refused a 1975-character letter. Desktop screenshots must be at least 1366x768. The app's default 1200x800 window is too small, so capture at 1920x1080.
- **A packaged install writes to the real `%LOCALAPPDATA%\RBX15` if that folder already exists** (from dev builds), instead of the package's `LocalCache`. Test storage behavior on a clean profile (Windows Sandbox), never on a dev machine.
- **No synthetic desktop input, anywhere.** `SendInput` and friends appear only in `BridgeExclusionTests.cs`, and the test input bridge compiles only with `-p:EnableTestInputBridge=true`. Screenshots and smoke runs drive the app through UI Automation patterns and `PrintWindow`.

## Conventions

- Conventional commits with a scope: `feat(app)`, `fix(engine)`, `chore(ledger)`, `docs(store)`. A commit that proves ledger rows ends with `Ledger evidence: <row> <- <Test.Name>, ...`.
- PRs merge with merge commits, not squash.
- Release notes for a tag live in `windows/RELEASE_NOTES_v5.md` and are filled by `/cut-release` before the tag is pushed.
