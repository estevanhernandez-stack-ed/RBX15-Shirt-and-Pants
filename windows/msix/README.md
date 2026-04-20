# MSIX packaging

Microsoft Store Partner Center submission artifacts and build scripts for ItsjustEste's RBX Classic Shirt and Pants Maker.

## What's here

| File | Purpose |
|---|---|
| [`Package.appxmanifest.template`](Package.appxmanifest.template) | MSIX manifest with `{{VERSION}}` and `{{PUBLISHER_CN}}` placeholders |
| [`make-msix-images.py`](make-msix-images.py) | Generates `Images/*.png` at all MSIX-required sizes |
| [`make-msix.ps1`](make-msix.ps1) | PowerShell build script — packages the MSIX from `dist/win-unpacked/` |
| [`Images/`](Images/) | Generated tiles for the manifest (StoreLogo, Square44/71/150/310, Wide310x150, Splash) |

The `Images/` PNGs are regenerated whenever you re-run `make-msix-images.py`. They're committed so the MSIX can be packed without Python, but they should be refreshed any time the brand palette or wordmark changes.

## Build

### 1. One-time Partner Center reservation

Before the MSIX can be submitted:

1. Reserve the app name `ItsjustEste's RBX Classic Shirt and Pants Maker` in [Microsoft Partner Center](https://partner.microsoft.com/dashboard).
2. Partner Center issues a **Publisher CN** that looks like `CN=ABCDEFGH-1234-5678-ABCD-EF1234567890`.
3. Pass that CN into the build script (see below). Keep it out of source control — it's tied to your Partner Center account.

### 2. Build the MSIX

```powershell
# From the repo root. Replace the CN with your Partner Center value.
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 `
  -Build `
  -PublisherCN "CN=YOUR_PARTNER_CENTER_CN"
```

Or set an env var once per terminal session:

```powershell
$env:RBX_MAKER_PUBLISHER_CN = "CN=YOUR_PARTNER_CENTER_CN"
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 -Build
```

The `-Build` flag runs `npm run build` to produce `dist/win-unpacked/` before packaging. Drop `-Build` if you've already run it.

Output: `dist/RBX-Maker-v{version}.msix`.

### 3. Upload to Partner Center

Partner Center's submission flow accepts **unsigned** MSIX packages and applies the Store's own signature during ingestion. **Do not sign the MSIX for Store submission.** The `-SelfSign` flag in the script is purely for sideload-testing on your own machine.

### Local sideload test (optional)

```powershell
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 `
  -SelfSign `
  -PublisherCN "CN=YOUR_PARTNER_CENTER_CN"
```

The first run creates a dev cert in `Cert:\CurrentUser\My` and signs the MSIX. To *install* the signed MSIX locally, you'll need to trust the dev cert once — follow the instructions the script prints.

## Requirements

- **Windows SDK** for `MakeAppx.exe` and `SignTool.exe`. Install via Visual Studio or the [standalone SDK installer](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/).
- **Node.js 18+** and the repo's `npm install` done (for the `electron-builder` step).
- **Python 3.8+** and **Pillow** (for regenerating tile images, optional after first commit).

## Manifest identity notes

```xml
<Identity
  Name="626LabsLLC.RBX15ShirtAndPantsMaker"
  Publisher="{{PUBLISHER_CN}}"
  Version="{{VERSION}}"
  ProcessorArchitecture="x64" />
```

- **Name** is fixed at `626LabsLLC.RBX15ShirtAndPantsMaker` — matches the Partner Center reservation. Do not change without re-reserving.
- **Publisher CN** is injected at build time. Never commit the real value to the repo.
- **Version** is injected from `package.json`, expanded to MSIX's 4-part `A.B.C.D` format by appending `.0` to the SemVer.
- **runFullTrust** capability is declared because Electron can't run inside the MSIX sandbox cleanly (child processes, user-chosen folders via asset library). Partner Center will ask for a justification on submission — the one-liner is: *"Electron's multi-process architecture and the app's user-initiated folder picker require unsandboxed file access."*

## Brand consistency

The MSIX tiles are generated from the same brand tokens as `docs/store-assets/` — solid brand-navy base, cyan + magenta wordmark gradient, tracked "CLASSIC R15" eyebrow. If the palette changes in [colors_and_type.css](../../styles.css), rerun `make-msix-images.py` to regenerate.
