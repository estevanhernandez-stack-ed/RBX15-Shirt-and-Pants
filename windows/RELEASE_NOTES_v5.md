<!-- RELEASE-NOTES-PLACEHOLDER: this file is a draft. /cut-release replaces it with real commit-bullet notes before a
     v5.* tag is pushed, and removes this marker when it does. v5-release.yml refuses to publish while this marker is present.
     The user-facing sections below are written; the commit bullets since v4.1.0 are not. Not marketing copy. -->

# RBX15 Classic Shirt and Pants Maker 5.0.0 (draft)

RBX15 5.0 is a rebuild as a native Windows app (WPF on .NET 10) in place of the Electron app. Your `.r15proj` projects open as they are, and exports match what the canvas shows.

## What changed

- **Native and smaller.** The Store package is about 69 MB, down from 141 MB. The app carries its own .NET runtime.
- **Works offline.** The app makes no network requests. The 15 Word Art fonts and 3 interface fonts are bundled, so the app looks the same without a connection. 17 of the 18 families are under the SIL Open Font License 1.1 and Permanent Marker is under Apache 2.0.
- **3D preview.** A panel beside the canvas shows your template on a Classic or R15 avatar and follows every edit. Drag to turn it, double-click to face front. Switch it off with the 3D preview box.
- **Autosave and recovery.** While a document has unsaved changes, the app copies it to its own data folder every 60 seconds and never writes over your file. After a crash, the next launch offers to restore it.
- **Recent projects and the jump list.** A Recent button beside Open lists your last 10 projects, and the taskbar jump list shows the same 10.
- **Open from Explorer.** Double-click a `.r15proj` file to open it. Each launch is its own window.
- **Paste.** Ctrl+V adds the clipboard image as a new layer on top, as one undo step.
- **Undo covers every edit,** with redo on Ctrl+Y and Ctrl+Shift+Z.
- **Error log.** An unexpected error is written to `%LOCALAPPDATA%\RBX15\logs\error.log`. It holds no document content and is never sent anywhere.
- **Accessibility pass.** Keyboard focus shows a cyan ring on every control, and controls have names for Narrator.
- **A large canvas that fills the window,** built for the exact 585x559 template grid. The 4.1 wording promised a fullscreen canvas, which was never a feature.

## Upgrade note

**Quit RBX15 before updating.** Save your work and close the app first. For the Store version, the update installs over the existing app under the same identity. For the zip, extract the new version into a fresh folder rather than over the old one, because Windows locks the files of a running app. The NSIS installer (4.x) is retired: use the Store or the zip.

## Known issues

- High-contrast themes are not supported. The app draws its own dark palette whatever Windows theme is set.
- Screen readers read the controls, but edits made on the canvas (moving, painting, filling) are not announced.
- The portable zip is not code-signed, so Windows SmartScreen may ask you to confirm the first launch.
