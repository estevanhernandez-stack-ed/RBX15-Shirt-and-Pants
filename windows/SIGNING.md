# Code signing

What each way of shipping RBX15 needs signed. v5 ships two artifacts, both built by `scripts/build-msix.ps1`: the Store MSIX and the portable zip. The 4.x NSIS installer is retired, and its electron-builder signing notes live at the `v4.1.0` tag.

## TL;DR

| Distribution path | Signed? | Who signs? |
| --- | --- | --- |
| **Microsoft Store (MSIX)** | Yes, by the Store | Partner Center signs on ingestion. We upload it unsigned |
| **Portable zip (GitHub Releases)** | No | Nobody yet. SmartScreen may ask on first launch |
| **Sideload testing (local MSIX)** | Self-signed | `scripts/build-msix.ps1 -SelfSign` |

## Microsoft Store (MSIX)

The preferred path, and no certificate work on our end.

1. Build an unsigned MSIX: `pwsh -File scripts/build-msix.ps1` (or let the `v5.*` tag's release run build it).
2. Upload it to Partner Center. Never upload a `-SelfSign` build.
3. Ingestion signs it with Microsoft's certificate, so Store installs show no prompt.

Full flow: [`msix/README.md`](msix/README.md).

## Portable zip

The zip's `RBX15.App.exe` is unsigned. On first launch Windows SmartScreen may show "Windows protected your PC", with "Run anyway" behind "More info". README and the release notes say so. Signing it would need a code-signing certificate with reputation (an OV or EV certificate, or Azure Trusted Signing) and a `signtool sign` step in `build-msix.ps1` before the zip is made. That isn't planned: the Store is the main channel, and the zip is for people who prefer it.

## Self-signed (sideload testing only)

For installing a local MSIX before Partner Center. Never ship these to users.

```powershell
# The certificate subject is always the manifest Publisher; there is no -PublisherCN parameter.
pwsh -File scripts/build-msix.ps1 -SelfSign
```

The first run creates a dev certificate as a `.pfx` under `%LOCALAPPDATA%\RBX15\msix-dev` (password protected with DPAPI) and exports the public `rbx15-dev.cer` next to it. Nothing is added to any certificate store and nothing is trusted automatically. To install the signed MSIX on a test machine, trust that `.cer` yourself as administrator. The steps are in [`msix/README.md`](msix/README.md).
