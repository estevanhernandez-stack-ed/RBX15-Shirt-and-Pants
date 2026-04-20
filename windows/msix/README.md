# MSIX packaging

Microsoft Store Partner Center submission artifacts and build scripts for RBX15 Classic Shirt and Pants Maker.

## What's here

| File | Purpose |
|---|---|
| [`Package.appxmanifest.template`](Package.appxmanifest.template) | MSIX manifest with real Partner Center identity, `{{VERSION}}` placeholder |
| [`make-msix-images.py`](make-msix-images.py) | Generates `Images/*.png` at all MSIX-required sizes |
| [`make-msix.ps1`](make-msix.ps1) | PowerShell build script — packages the MSIX from `dist/win-unpacked/` |
| [`Images/`](Images/) | Generated tiles for the manifest (StoreLogo, Square44/71/150/310, Wide310x150, Splash) |

The `Images/` PNGs are regenerated whenever you re-run `make-msix-images.py`. They're committed so the MSIX can be packed without Python, but they should be refreshed any time the brand palette or wordmark changes.

## Partner Center identity (reserved)

| Field | Value |
|---|---|
| App name (reserved) | RBX15 Classic Shirt and Pants Maker |
| Package Name | `626LabsLLC.RBX15ClassicShirtandPantsMaker` |
| Publisher CN | `CN=177BCE59-0966-4975-9962-10E36652141F` |
| Publisher Display Name | `626Labs LLC` |
| Store ID | `9MV9G4XFJ8S0` |
| Store URL | <https://apps.microsoft.com/detail/9MV9G4XFJ8S0> (live after listing approval) |

All three identity fields (Name, Publisher CN, PublisherDisplayName) are hardcoded in [`Package.appxmanifest.template`](Package.appxmanifest.template). The Publisher CN is **not a secret** — it's baked into every signed MSIX for every app on the 626 Labs LLC Partner Center account (Sanduhr, RBX15, future products). Committing it is fine.

## Build

### Build the MSIX

```powershell
# From the repo root.
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 -Build
```

The `-Build` flag runs `npm run build` to produce `dist/win-unpacked/` before packaging. Drop `-Build` if you've already run it.

Output: `dist/RBX15-Maker-v{version}.msix`.

### Upload to Partner Center

Partner Center's submission flow accepts **unsigned** MSIX packages and applies the Store's own signature during ingestion. **Do not sign the MSIX for Store submission.** The `-SelfSign` flag is purely for sideload-testing on your own machine.

### Local sideload test (optional)

```powershell
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 -SelfSign
```

The first run creates a dev cert in `Cert:\CurrentUser\My` and signs the MSIX. To *install* the signed MSIX locally, you'll need to trust the dev cert once — follow the instructions the script prints.

## Requirements

- **Windows SDK** for `MakeAppx.exe` and `SignTool.exe`. Install via Visual Studio or the [standalone SDK installer](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/).
- **Node.js 18+** and the repo's `npm install` done (for the `electron-builder` step).
- **Python 3.8+** and **Pillow** (for regenerating tile images, optional after first commit).

## Manifest identity notes

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

## Brand consistency

The MSIX tiles are generated from the same brand tokens as `docs/store-assets/` — solid brand-navy base, cyan + magenta wordmark gradient, tracked "CLASSIC R15" eyebrow. If the palette changes in [styles.css](../../styles.css), rerun `make-msix-images.py` to regenerate.
