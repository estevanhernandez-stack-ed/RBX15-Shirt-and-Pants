# Notes for certification — reviewer letter (v4.1.0.0)

> Paste the block between the `---` markers into Partner Center → your app →
> **Submission options** → **Notes for certification** (~2000-char field; front-loaded).
>
> Framing: RBX15 Maker is a fully local, offline desktop creative tool — no account, no
> data collection, one outbound request (word-art fonts). The letter states the disclosure
> surface plainly up front, then justifies the single declared capability (`runFullTrust`).
> Public "What's new in this version" is a **separate** field — fill it from
> [`listing-copy-4.1.0.md`](./listing-copy-4.1.0.md). Both get filled every release.

---

```text
Hello reviewer,

Thank you for your time on RBX15 Classic Shirt and Pants Maker v4.1.0.0.

WHAT IT IS
A native Windows desktop editor for Roblox Classic (R15) shirt and pants
templates — the 585x559 PNG panels creators upload to Roblox. Users compose
layers, paint region fills, draw, add word art, and export a PNG. It is an
Electron desktop app packaged as MSIX.

NO ACCOUNT, NO SETUP
There is no login and no configuration. Open the app and every feature is
available immediately. It does not connect to Roblox, does not sign in to any
Roblox account, and does not automate anything in Roblox — it only produces a
PNG file that the user uploads to Roblox themselves.

DATA — ALL LOCAL
The app reads and writes only files the user explicitly chooses: open/save a
.r15proj project, export a PNG, add an image, or point the asset browser at a
folder. Nothing is collected, stored off-device, or transmitted. No telemetry,
no analytics, no ads.

NETWORK — ONE REQUEST
The only outbound request loads the word-art display fonts from Google Fonts
(fonts.googleapis.com, fonts.gstatic.com) on launch; Windows caches them. No
user data is sent in that request, and nothing else contacts the network.

CAPABILITY — runFullTrust ONLY
The package declares only runFullTrust (rescap). Justification: (1) Electron's
main process spawns the renderer as a child process, which a sandboxed MSIX
process cannot do cleanly; (2) the asset-library browser reads a user-chosen
folder of images, and sandboxed file access would force a folder picker on
every load, breaking the workflow. No other capabilities are declared.

TRADEMARK
"Roblox" is a trademark of Roblox Corporation, used nominatively to describe
the template file format. RBX15 Classic Shirt and Pants Maker is an independent
third-party tool, not affiliated with or endorsed by Roblox Corporation. Source
is MIT at https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants.

If anything is unclear, please reach out and we will respond same-day.

Estevan Hernandez
626 Labs LLC
```

---

## Pre-submission sanity check (v4.1.0.0)

- [ ] `package.json` version = `4.1.0` → `make-msix.ps1` injects `4.1.0.0` (4th component `.0`)
- [ ] Identity `Name` = `626LabsLLC.RBX15ClassicShirtandPantsMaker`, `Publisher` = `CN=177BCE59-0966-4975-9962-10E36652141F`, `PublisherDisplayName` = `626Labs LLC` (NO space in 626Labs — the spaced form fails Partner Center validation)
- [ ] ONLY `runFullTrust` declared; the justification above pasted into the capability's justification field on submission
- [ ] `dist/RBX15-Maker-v4.1.0.0.msix` built off the `v4.1.0` tag, unsigned (Store ingestion signs it)
- [ ] All tile images present and named as the manifest references them (the v4.0.0 rejection was a tile filename mismatch — see CHANGELOG 4.0.1)
- [ ] `TargetDeviceFamily MinVersion` = `10.0.17763.0` (Windows 10 1809, the Electron 41 floor)
- [ ] This letter's block pasted into **Notes for certification**
- [ ] Public **What's new in this version** filled from [`listing-copy-4.1.0.md`](./listing-copy-4.1.0.md)

## Source

Format mirrors the estate store-docs convention (Sanduhr `docs/store/`, ROROROblox `docs/store/`). Public listing copy: [`listing-copy-4.1.0.md`](./listing-copy-4.1.0.md). Store ID `9MV9G4XFJ8S0`.
