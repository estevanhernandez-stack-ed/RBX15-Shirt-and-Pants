# MSIX packaging

Microsoft Store Partner Center submission artifacts and build scripts for RBX15 Classic Shirt and Pants Maker.

The package is built by `scripts/build-msix.ps1` from `Package.v5.appxmanifest.template`, and ships from `v5.*` tags. The 4.x Electron packaging (`make-msix.ps1`, `Package.appxmanifest.template`) was removed at the cutover and lives at the `v4.1.0` tag.

## v5 packaging

### What's here (v5)

| File | Purpose |
|---|---|
| [`../../scripts/build-msix.ps1`](../../scripts/build-msix.ps1) | One command: publish, render manifest, pack, scan, optional sign, zip |
| [`Package.v5.appxmanifest.template`](Package.v5.appxmanifest.template) | v5 manifest. `{{VERSION}}` and `{{EXECUTABLE}}` are the only placeholders |
| [`make-app-icon.py`](make-app-icon.py) | Regenerates `src/RBX15.App/app.ico` (exe, window and taskbar icon) from `Images/Square310x310Logo.png` |
| [`Images/`](Images/) | The tiles the manifest references (committed). [`make-msix-images.py`](make-msix-images.py) regenerates them |
| [`../RELEASE_NOTES_v5.md`](../RELEASE_NOTES_v5.md) | Release body for `v5.*` tags, filled by `/cut-release` before the tag. The release workflow refuses a placeholder |

### Build

```powershell
# From anywhere. Works in PowerShell 7 and Windows PowerShell 5.1.
pwsh -File scripts/build-msix.ps1 -Version 5.0.0.0
```

Outputs in `dist/`:

- `RBX15-Maker-v5.0.0.0.msix`: unsigned, the Store submission artifact.
- `RBX15-Maker-v5.0.0.0-portable-win-x64.zip`: unsigned, the same publish folder under a single top-level folder. Extract and run `RBX15.App.exe`.

Parameters: `-Version` (A.B.C.0, must equal the `<Version>` in `RBX15.App.csproj`; defaults to it), `-OutDir`, `-SelfSign`, `-SkipPublish` (reuse the staging publish folder). Needs the .NET SDK from `global.json` and the Windows SDK (`makeappx.exe`, `signtool.exe`; the newest `Windows Kits\10\bin\<ver>\x64` is used).

What it enforces, failing the build otherwise:

1. `EnableTestInputBridge` is not set in the environment. (`dotnet publish -p:EnableTestInputBridge=true` is refused by the csproj as well.)
2. The rendered manifest has the exact 4.1 identity (Name, Publisher, PublisherDisplayName), the exact 4.1 Application Id `RBXMaker`, `runFullTrust` as the only capability, and the `.r15proj` association.
3. Every image the manifest references exists in the package at its required pixel size.
4. `makeappx pack` runs with validation (the 4.x script used `/nv`).
5. The finished MSIX is unpacked and `BridgeExclusionTests.The_packaged_app_has_no_bridge` runs against the packaged `RBX15.App.dll`; a test-bridge type or string fails the build, and a scan that did not run exactly one passing test fails it too.

Publish settings: `-r win-x64 --self-contained`, ReadyToRun on (startup), trimming and single-file off (WPF and the SkiaSharp native libraries break under both), no symbols, English satellite resources only. About 69 MB packed.

Windows floor: the manifest keeps `MinVersion 10.0.17763.0` (Windows 10 1809), the 4.x floor. .NET 10 supports Windows 10 1607 and later, but 1607 and 1809 only in Enterprise/LTSC servicing (supported-OS table, dotnet/core release notes 10.0). Raising the floor would hide the app from those devices in the Store.

### Store submission

Upload `dist/RBX15-Maker-v5.0.0.0.msix` (unsigned) to Partner Center; the Store signs on ingestion. Never submit a `-SelfSign` build. The `runFullTrust` justification for the reviewer letter: *"WPF/.NET desktop application. It cannot run in the AppContainer; it reads and writes only files the user chooses. It makes no network calls."*

### v5 sideload upgrade test (manual, needs admin; ledger `parity-store-update-path`)

Proves a 4.1 install updates in place to 5.0.0.0 with the same identity, the pinned tile surviving. Nothing in the repo or the build script installs or trusts a certificate; these steps are yours to run, on a test machine or at least a test user.

1. Build the signed v5 package: `pwsh -File scripts/build-msix.ps1 -Version 5.0.0.0 -SelfSign`. Output `dist\RBX15-Maker-v5.0.0.0.msix`; public cert `%LOCALAPPDATA%\RBX15\msix-dev\rbx15-dev.cer`.
2. Build a signed 4.1 package from a separate worktree of the `v4.1.0` tag (never from your working copy):

   ```powershell
   git worktree add C:\temp\rbx15-v41 v4.1.0
   cd C:\temp\rbx15-v41
   npm install
   powershell -ExecutionPolicy Bypass -File windows\msix\make-msix.ps1 -Build -SelfSign   # dist\RBX15-Maker-v4.1.0.0.msix
   $c = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq 'CN=177BCE59-0966-4975-9962-10E36652141F' | Select-Object -First 1
   Export-Certificate -Cert $c -FilePath C:\temp\rbx15-41-dev.cer
   ```

3. If the Store copy of RBX15 is installed, you can skip the 4.1 package and update the Store copy directly: a Store-signed 4.1 takes the self-signed 5.0 as an in-place update once the dev certificate is trusted (proven 2026-10-25; ledger `parity-store-update-path`). Build the test-signed 4.1 only when no copy is installed.
4. In an **elevated** PowerShell, trust both public certs for sideloading (this is the step that needs you; you undo it in step 8):

   ```powershell
   Import-Certificate -FilePath "$env:LOCALAPPDATA\RBX15\msix-dev\rbx15-dev.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   Import-Certificate -FilePath C:\temp\rbx15-41-dev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

5. Install 4.1, pin its Start tile and taskbar icon, and open it: `Add-AppxPackage C:\temp\rbx15-v41\dist\RBX15-Maker-v4.1.0.0.msix`. Note `Get-AppxPackage 626LabsLLC.RBX15* | Select Name, Version, PackageFamilyName` (expect `626LabsLLC.RBX15ClassicShirtandPantsMaker_wz1chhb2h2v4a`).
6. Quit the app, then update in place: `Add-AppxPackage <repo>\dist\RBX15-Maker-v5.0.0.0.msix`. It must succeed as an upgrade, not as a second package.
7. Check: `Get-AppxPackage 626LabsLLC.RBX15*` shows exactly one package, Version `5.0.0.0`, the same PackageFamilyName; the pinned Start tile and taskbar icon still launch the new app (window title, brand icon); the Start tile label reads `RBX15`; right-click a `.r15proj` and RBX15 is offered under Open with (the app's open-on-launch handling is M5).
8. Clean up: `Get-AppxPackage 626LabsLLC.RBX15* | Remove-AppxPackage`; in the elevated shell remove both certs (`Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=177BCE59-0966-4975-9962-10E36652141F' | Remove-Item`); delete `%LOCALAPPDATA%\RBX15\msix-dev` and the 4.1 worktree.

### CI

- `v5-package.yml`: every PR and main push touching `src/`, `packs/`, `assets/`, `scripts/` or this folder runs `build-msix.ps1` and uploads the MSIX and zip as the `rbx15-v5-package` artifact.
- `v5-release.yml`: a `v5.MINOR.PATCH` tag checks the notes are filled, the ledger has no open row, builds with warnings as errors, runs every test, runs `build-msix.ps1 -Version <tag>.0` (fails if the tag and the csproj `<Version>` disagree), and creates a **draft** GitHub release with the MSIX and the zip attached and `windows/RELEASE_NOTES_v5.md` as the body. The draft is published once the Store serves the new version, titled `<tag> — Live on the Microsoft Store`.
- `v5-release.yml` is the only workflow that runs on a tag (`tools/ci/test_workflows.py`).
