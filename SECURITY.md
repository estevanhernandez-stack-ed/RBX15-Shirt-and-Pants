# Security Policy

## The app never phones home

Before anything else: **this app makes zero network requests.** No telemetry, no analytics, no crash reporter, no "anonymous diagnostics" toggle, no auto-update check. 626 Labs has no server for this app, no pipeline, and no way to receive data from it even if we wanted one.

Everything runs locally:

- **Images** load from your filesystem or from an asset-library folder you point at.
- **Projects** (`.r15proj`) save where you tell them to.
- **Templates** (`.png`) export to your chosen location.
- **Fonts** are bundled with the app, so nothing is fetched at launch or later.
- **Local files** the app keeps (settings, a crash-recovery copy of unsaved work, an error log) stay in `%LOCALAPPDATA%\RBX15` and are never sent anywhere. [docs/PRIVACY.md](docs/PRIVACY.md) lists them.

There is no account, no sign-in, no cloud sync, no API keys.

## Reporting a vulnerability

If you find a security issue, please **do not** open a public GitHub issue.

**Preferred channel: GitHub private vulnerability reporting:**
[github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/security/advisories/new](https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/security/advisories/new)

This creates a private advisory visible only to repository maintainers.

**What to include:**

- A description of the issue and why you think it's a security problem
- Reproduction steps or a proof-of-concept if you have one
- The affected build / version (the version in the app header or the Apps list, or a commit hash if built from source)
- Your OS and version

**What to expect back:**

- Initial acknowledgement within **5 business days**
- A fix or an explanation (with timeline) within **30 days** for confirmed issues
- Credit in the release notes for the fix, if you want it (anonymous is fine too)

## In scope

Security reports against:

- The WPF host: the window, the dialogs and the command surface.
- Decoding of untrusted images (SkiaSharp), from a file, a drop, an asset-library folder or the clipboard. Every route enforces the same caps: 100 MiB encoded, 16384 pixels on a side and 50 megapixels.
- The `.r15proj` loader. It reads a file of at most 512 MiB, validates the whole file before it changes anything (a bad file changes nothing), and accepts embedded images only as `data:` URLs, each held to the image caps above.
- The clipboard paste path (Ctrl+V): a PNG, an image file list or a bitmap.
- The autosave and recovery files in `%LOCALAPPDATA%\RBX15\autosave` and the launch path, where the app opens a file passed on its command line.
- The error log in `%LOCALAPPDATA%\RBX15\logs`, which must never hold document content.
- The MSIX package and its manifest. The only capability is `runFullTrust`.
- Path-traversal and unsafe-deserialization routes from any of the above.

## Out of scope

- **Roblox itself.** Report those to Roblox Corporation via their HackerOne program or security contact.
- The integrity or licensing of user-supplied art: the app is a tool, not a licensing gatekeeper.
- Issues in your operating system or file manager.
- Issues in third-party dependencies without a demonstrated exploitation path against this app specifically.
- Social-engineering attacks that require the user to load a maliciously-crafted `.r15proj` file from an untrusted source. **Don't open project files from strangers.** The format is JSON with base64-embedded image data. The loader validates it and caps sizes, but the same rule applies as with any arbitrary file.

## Why this policy is short

The app has a small surface. No network, no accounts, no cloud, no plugins. Most security policies say a lot because they have a lot to defend. We have a canvas, an image decoder, and a JSON save format.

---

Built by [626 Labs LLC](https://626labs.dev).
