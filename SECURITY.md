# Security Policy

## The app never phones home

Before anything else: **this app makes zero network requests.** No telemetry, no analytics, no crash reporter, no "anonymous diagnostics" toggle, no auto-update check. 626 Labs has no server for this app, no pipeline, and no way to receive data from it even if we wanted one.

Everything runs locally:

- **Images** load from your filesystem or from an asset-library folder you point at.
- **Projects** (`.r15proj`) save where you tell them to.
- **Templates** (`.png`) download to your chosen location.
- **Google Fonts** load once at first launch for word-art rendering — that is the only outbound request the app makes, to `fonts.googleapis.com` and `fonts.gstatic.com`. It happens in the Electron renderer, cached in Electron's HTTP cache, and stops on subsequent runs. No data from you goes out with that request.

There is no account, no sign-in, no cloud sync, no API keys.

## Reporting a vulnerability

If you find a security issue, please **do not** open a public GitHub issue.

**Preferred channel — GitHub private vulnerability reporting:**
[github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/security/advisories/new](https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/security/advisories/new)

This creates a private advisory visible only to repository maintainers.

**What to include:**

- A description of the issue and why you think it's a security problem
- Reproduction steps or a proof-of-concept if you have one
- The affected build / version (installer version from the Apps & features list, or a commit hash if built from source)
- Your OS and version

**What to expect back:**

- Initial acknowledgement within **5 business days**
- A fix or an explanation (with timeline) within **30 days** for confirmed issues
- Credit in the release notes for the fix, if you want it (anonymous is fine too)

## In scope

Security reports against:

- The Electron shell — `main.js`, window creation, IPC surface
- The editor renderer — `editor.html`, `styles.css`, `editor.js`
- File-handling paths — image loading, project file loading (`.r15proj` JSON), asset library folder enumeration
- The NSIS installer and uninstaller
- The upcoming MSIX package (once submitted)
- Any prototype-pollution, XSS, or path-traversal vector in the project-file loader

## Out of scope

- **Roblox itself.** Report those to Roblox Corporation via their HackerOne program or security contact.
- The integrity or licensing of user-supplied art — the app is a tool, not a licensing gatekeeper.
- Issues in your operating system or file manager.
- Issues in third-party dependencies without a demonstrated exploitation path against this app specifically.
- Social-engineering attacks that require the user to load a maliciously-crafted `.r15proj` file from an untrusted source. **Don't open project files from strangers.** The format is JSON with base64-embedded image data — we sanitize on load, but the same rule applies as with any arbitrary file.

## Why this policy is short

The app has a small surface. No network, no accounts, no cloud, no plugins. Most security policies say a lot because they have a lot to defend. We have a canvas, an NSIS installer, and a JSON save format.

---

Built by [626 Labs LLC](https://626labs.dev).
