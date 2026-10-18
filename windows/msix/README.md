# MSIX packaging

Microsoft Store Partner Center submission artifacts and build scripts for RBX15 Classic Shirt and Pants Maker.

- **v5 (native WPF, .NET 10):** `scripts/build-msix.ps1` and `Package.v5.appxmanifest.template`. This is what ships from `v5.*` tags.
- **4.x (Electron, legacy):** `make-msix.ps1` and `Package.appxmanifest.template`, documented in the second half of this file. Kept until the cutover PR deletes the Electron app; only `v4.*` tags build it.

## v5 packaging

### What's here (v5)

| File | Purpose |
|---|---|
| [`../../scripts/build-msix.ps1`](../../scripts/build-msix.ps1) | One command: publish, render manifest, pack, scan, optional sign, zip |
| [`Package.v5.appxmanifest.template`](Package.v5.appxmanifest.template) | v5 manifest. `{{VERSION}}` and `{{EXECUTABLE}}` are the only placeholders |
| [`make-app-icon.py`](make-app-icon.py) | Regenerates `src/RBX15.App/app.ico` (exe, window and taskbar icon) from `Images/Square310x310Logo.png` |
| [`Images/`](Images/) | Tiles shared by 4.x and v5 (committed; the manifest references them) |
| [`../RELEASE_NOTES_v5.md`](../RELEASE_NOTES_v5.md) | Release body for `v5.*` tags. A placeholder until `/cut-release` fills it; the release workflow refuses the placeholder |

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

3. If the Store copy of RBX15 is installed, uninstall it first. Same identity with a different signer does not install over it.
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
- `v5-release.yml`: a `v5.MINOR.PATCH` tag checks the notes are filled, the ledger has no open row, builds with warnings as errors, runs every test, runs `build-msix.ps1 -Version <tag>.0` (fails if the tag and the csproj `<Version>` disagree), and creates the GitHub release with the MSIX and the zip attached and `windows/RELEASE_NOTES_v5.md` as the body.
- `windows-release.yml` and `windows-ci.yml` are the 4.x Electron workflows; the release one is fenced to `v4.*` tags.

---

## Legacy 4.1 (Electron) packaging

### What's here (4.1)

| File | Purpose |
|---|---|
| [`Package.appxmanifest.template`](Package.appxmanifest.template) | MSIX manifest with real Partner Center identity, `{{VERSION}}` placeholder |
| [`make-msix-images.py`](make-msix-images.py) | Generates `Images/*.png` at all MSIX-required sizes |
| [`make-msix.ps1`](make-msix.ps1) | PowerShell build script — packages the MSIX from `dist/win-unpacked/` |
| [`Images/`](Images/) | Generated tiles for the manifest (StoreLogo, Square44/71/150/310, Wide310x150, Splash) |

The `Images/` PNGs are regenerated whenever you re-run `make-msix-images.py`. They're committed so the MSIX can be packed without Python, but they should be refreshed any time the brand palette or wordmark changes.

### Partner Center identity (reserved; unchanged in v5)

| Field | Value |
|---|---|
| App name (reserved) | RBX15 Classic Shirt and Pants Maker |
| Package Name | `626LabsLLC.RBX15ClassicShirtandPantsMaker` |
| Publisher CN | `CN=177BCE59-0966-4975-9962-10E36652141F` |
| Publisher Display Name | `626Labs LLC` |
| Store ID | `9MV9G4XFJ8S0` |
| Store URL | <https://apps.microsoft.com/detail/9MV9G4XFJ8S0> (live after listing approval) |

All three identity fields (Name, Publisher CN, PublisherDisplayName) are hardcoded in [`Package.appxmanifest.template`](Package.appxmanifest.template). The Publisher CN is **not a secret** — it's baked into every signed MSIX for every app on the 626 Labs LLC Partner Center account (Sanduhr, RBX15, future products). Committing it is fine.

### Build (4.1)

#### Build the MSIX

```powershell
# From the repo root.
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 -Build
```

The `-Build` flag runs `npm run build` to produce `dist/win-unpacked/` before packaging. Drop `-Build` if you've already run it.

Output: `dist/RBX15-Maker-v{version}.msix`.

#### Upload to Partner Center

Partner Center's submission flow accepts **unsigned** MSIX packages and applies the Store's own signature during ingestion. **Do not sign the MSIX for Store submission.** The `-SelfSign` flag is purely for sideload-testing on your own machine.

#### Local sideload test (optional)

```powershell
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 -SelfSign
```

The first run creates a dev cert in `Cert:\CurrentUser\My` and signs the MSIX. To *install* the signed MSIX locally, you'll need to trust the dev cert once — follow the instructions the script prints.

### Requirements (4.1)

- **Windows SDK** for `MakeAppx.exe` and `SignTool.exe`. Install via Visual Studio or the [standalone SDK installer](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/).
- **Node.js 18+** and the repo's `npm install` done (for the `electron-builder` step).
- **Python 3.8+** and **Pillow** (for regenerating tile images, optional after first commit).

### Manifest identity notes

```xml
<Identity
  Name="626LabsLLC.RBX15ClassicShirtandPantsMaker"
  Publisher="CN=177BCE59-0966-4975-9962-10E36652141F"
  Version="{{VERSION}}"
  ProcessorArchitecture="x64" />
```

- **Name** case matters — Partner Center assigned the exact casing `626LabsLLC.RBX15ClassicShirtandPantsMaker`. Note the lowercase `and` and `Pants` mid-string. Do not re-case.
- **Publisher** is the 626 Labs LLC account CN, shared with Sanduhr and any future apps on the same account.
- **Version** is injected from `package.json`, expanded to MSIX's 4-part `A.B.C.D` format by appending `.0` to the SemVer.
- **runFullTrust** capability is declared because Electron can't run inside the MSIX sandbox cleanly (child processes, user-chosen folders via asset library). Partner Center will ask for a justification on submission — the one-liner is: *"Electron's multi-process architecture and the app's user-initiated folder picker require unsandboxed file access."*

### Brand consistency

The MSIX tiles are generated from the same brand tokens as `docs/store-assets/` — solid brand-navy base, cyan + magenta wordmark gradient, tracked "CLASSIC R15" eyebrow. If the palette changes in [styles.css](../../styles.css), rerun `make-msix-images.py` to regenerate.
