# Notes for certification: reviewer letter (v5.0.0.0)

> Paste the block between the `---` markers into Partner Center, your app,
> **Submission options**, **Notes for certification** (about 2000 characters; the block is
> under that and front-loaded).
>
> Framing: RBX15 is a fully local, offline desktop creative tool. No account, no data
> collection, no network requests. The letter states the disclosure surface up front, then
> justifies the single declared capability (`runFullTrust`). The public "What's new in this
> version" is a **separate** field, filled from
> [`listing-copy-5.0.0.md`](./listing-copy-5.0.0.md). Both get filled every release.

---

```text
Hello reviewer,

Thank you for your time on RBX15 Classic Shirt and Pants Maker v5.0.0.0.

WHAT IT IS
A Windows editor for Roblox Classic (R15) shirt and pants templates, the 585x559 PNG
panels creators upload to Roblox. Users compose layers, paint region fills, draw, add
word art and export a PNG. Version 5 is a native WPF app on .NET 10, self-contained,
packaged as MSIX. It replaces the Electron build (4.1) under the same package identity.

NO ACCOUNT, NO SETUP
No login, no configuration. It does not connect to Roblox. It only produces a PNG that
the user uploads to Roblox themselves.

NETWORK: NONE
The app makes no network requests: no update check, telemetry, analytics or ads, and no
internetClient capability. All fonts are bundled.

DATA: ALL LOCAL
It reads and writes only files the user chooses (projects, PNG exports, images, asset
folders), plus its own settings, autosave copy and error log in its local app data. The
error log holds no document content. Nothing is sent anywhere.

CAPABILITY: runFullTrust ONLY
Justification: a Win32/.NET desktop app cannot run in the AppContainer; it reads and
writes only user-chosen files, plus its own settings, autosave and error log in its
local app data. The package also registers the .r15proj file type.

FONTS
15 Word Art fonts (Anton, Bangers, Bebas Neue, Bungee Shade, Creepster, Dancing
Script, Great Vibes, Lato, Orbitron, Pacifico, Poppins, Press Start 2P, Righteous,
Rubik Mono One, Permanent Marker) and 3 UI fonts (Inter, JetBrains Mono, Space
Grotesk). 17 are under the SIL OFL 1.1; Permanent Marker is Apache 2.0. Licenses ship
in the package.

TRADEMARK
"Roblox" is a trademark of Roblox Corporation, used nominatively to describe the
template file format. RBX15 Classic Shirt and Pants Maker is an independent
third-party tool, not affiliated with or endorsed by Roblox Corporation. Source is
MIT at https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants.

Estevan Hernandez
626Labs LLC
```

---

## Pre-submission sanity check (v5.0.0.0)

- [ ] `<Version>` in `RBX15.App.csproj` is `5.0.0`; `scripts/build-msix.ps1` renders `5.0.0.0` and the output is `dist/RBX15-Maker-v5.0.0.0.msix`
- [ ] Identity `Name` = `626LabsLLC.RBX15ClassicShirtandPantsMaker`, `Publisher` = `CN=177BCE59-0966-4975-9962-10E36652141F`, `PublisherDisplayName` = `626Labs LLC` (NO space in 626Labs; the spaced form fails Partner Center validation). `build-msix.ps1` asserts these on every build
- [ ] ONLY `runFullTrust` declared (asserted by `build-msix.ps1`); the justification above, and in `windows/msix/README.md` under "Store submission", pasted into the capability's justification field
- [ ] The MSIX is unsigned (never a `-SelfSign` build); the Store signs on ingestion
- [ ] All tile images present at the sizes the manifest references (the v4.0.0 rejection was a tile filename mismatch, see CHANGELOG 4.0.1). `build-msix.ps1` checks each pixel size
- [ ] `TargetDeviceFamily MinVersion` = `10.0.17763.0` (Windows 10 1809)
- [ ] This letter's block pasted into **Notes for certification**
- [ ] Public **What's new in this version** filled from [`listing-copy-5.0.0.md`](./listing-copy-5.0.0.md)
- [ ] Store art in `docs/store-assets/` passes `StoreAssetsTests` (each PNG has the pixel size its README table states)

## Source

Format mirrors the estate store-docs convention (Sanduhr `docs/store/`, ROROROblox `docs/store/`) and the 4.1 letter, [`reviewer-letter-4.1.0.0.md`](./reviewer-letter-4.1.0.0.md). Public listing copy: [`listing-copy-5.0.0.md`](./listing-copy-5.0.0.md). Store ID `9MV9G4XFJ8S0`.
