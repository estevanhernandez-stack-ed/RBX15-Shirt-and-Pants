# Code signing

Runbook for signing the Windows installer and the MSIX package. Covers three distribution paths — Microsoft Store, direct GitHub Releases download, and sideload testing — and what signing each one actually needs.

## TL;DR

| Distribution path | Signing required? | Who signs? |
|---|---|---|
| **Microsoft Store (MSIX)** | No cert on our end | Partner Center signs on ingestion |
| **GitHub Releases (NSIS `.exe`)** | Yes, eventually | We sign with a code-signing cert |
| **Sideload testing (local MSIX)** | Yes, self-signed | `make-msix.ps1 -SelfSign` |

Microsoft Store approval eliminates the SmartScreen prompt for Store installs. For direct downloads from GitHub Releases, SmartScreen will keep warning users until we sign the `.exe` with a real cert.

## Why SmartScreen warns

When a user double-clicks an unsigned `.exe` on Windows 10/11, SmartScreen checks:

1. Does the file have a valid Authenticode signature?
2. Does the signing certificate have reputation (downloads, installs, clean history)?

Our installer currently fails both — no signature, no reputation. SmartScreen shows the "Windows protected your PC" warning with a "Run anyway" link tucked behind "More info."

Paying users of the app will click through it. Casual users will not.

## Path 1 — Microsoft Store (MSIX)

This is the preferred distribution path. **No code signing work required on our end.**

1. Build an unsigned MSIX: `windows/msix/make-msix.ps1`
2. Upload to Partner Center
3. Partner Center's ingestion process signs the MSIX with Microsoft's cert
4. Installs from the Store show zero prompts to users

Full flow documented in [`msix/README.md`](msix/README.md).

## Path 2 — Azure Trusted Signing (recommended for NSIS)

[Azure Trusted Signing](https://learn.microsoft.com/en-us/azure/trusted-signing/) is Microsoft's managed code-signing service. Pros:

- No hardware token, no USB dongle to lose
- Certificate management handled by Azure — no manual renewal
- ~$10/month for individual developers (as of writing — verify current pricing)
- SmartScreen reputation accrues the same way as with traditional certs

Cons:

- Requires an Azure subscription + setup
- Only available to verified accounts (individual verification takes ~1 week)

### One-time setup

1. Create an Azure account and enable a subscription
2. Complete [identity verification](https://learn.microsoft.com/en-us/azure/trusted-signing/how-to-signing-account) for Trusted Signing
3. Create a Trusted Signing account in Azure Portal — region close to your CI
4. Create a **certificate profile** under that account
5. Create a service principal (App Registration) that the build will use
6. Grant the service principal "Trusted Signing Certificate Profile Signer" role on the profile

### Integration with electron-builder

Add the Azure sign helper as a dev dependency:

```bash
npm install --save-dev @azure/trusted-signing-client
```

Add a `sign.js` hook in the repo root:

```javascript
// sign.js — called by electron-builder for each artifact it produces.
// Requires AZURE_CLIENT_ID / AZURE_CLIENT_SECRET / AZURE_TENANT_ID env vars.
const { signAsync } = require("@azure/trusted-signing-client");

exports.default = async function (configuration) {
  await signAsync({
    endpoint: "https://eus.codesigning.azure.net",
    account: "rbx-maker-signing",
    profile: "rbx-maker-release",
    fileDigest: "SHA256",
    timestampRfc3161: "http://timestamp.acs.microsoft.com",
    files: [configuration.path],
  });
};
```

Wire it in `package.json` under `build.win`:

```json
"win": {
  "target": "nsis",
  "sign": "./sign.js",
  "signingHashAlgorithms": ["sha256"]
}
```

### CI secrets

In your GitHub Actions workflow, store the Azure credentials as repository secrets:

- `AZURE_CLIENT_ID`
- `AZURE_CLIENT_SECRET`
- `AZURE_TENANT_ID`

Set them as env vars on the \`npm run build\` step so the sign hook can authenticate. Never commit these to the repo.

## Path 3 — Traditional CA certificate

If Azure Trusted Signing isn't an option (e.g., you don't want Azure), you can buy a code-signing cert from a CA:

- **OV (Organization Validation) cert** — $200-300 / yr. SmartScreen reputation builds over weeks / months of installs before the warning goes away.
- **EV (Extended Validation) cert** — $300-500 / yr, issued on a hardware token (YubiKey or similar). SmartScreen warning disappears immediately on first install.

Recommended CAs: DigiCert, Sectigo, SSL.com.

### Integration

Import the cert's `.pfx` into the Windows cert store, then set electron-builder to find it by thumbprint:

```json
"win": {
  "target": "nsis",
  "certificateSubjectName": "626 Labs LLC",
  "signingHashAlgorithms": ["sha256"]
}
```

Or pass the `.pfx` file and password via env vars at build time:

```powershell
$env:CSC_LINK = "file:///absolute/path/to/cert.pfx"
$env:CSC_KEY_PASSWORD = "your-pfx-password"
npm run build
```

Electron-builder picks these up automatically and signs the NSIS installer + every inner `.exe`.

### CI with a .pfx file

Base64-encode the `.pfx` and store it as a GitHub secret, along with the password:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))
```

Decode and write to disk in the workflow:

```yaml
- name: Decode cert
  run: |
    echo "${{ secrets.CODE_SIGN_PFX_BASE64 }}" | base64 -d > cert.pfx
  shell: bash
- name: Build
  env:
    CSC_LINK: ${{ github.workspace }}/cert.pfx
    CSC_KEY_PASSWORD: ${{ secrets.CODE_SIGN_PFX_PASSWORD }}
  run: npm run build
- name: Cleanup
  if: always()
  run: rm -f cert.pfx
```

## Path 4 — Self-signed (sideload testing only)

For local MSIX testing before Partner Center submission. Never ship these to users.

```powershell
powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 `
  -SelfSign `
  -PublisherCN "CN=YOUR_PARTNER_CENTER_CN"
```

First run creates a dev cert in `Cert:\CurrentUser\My`. To actually install the signed MSIX locally, trust the dev cert once — the script prints the exact `Export-Certificate` + `certlm.msc` steps.

## Decision: what we're doing right now

**v4.0.0 ships unsigned.** Users clicking through SmartScreen is an acceptable one-time friction while the Microsoft Store submission is in review.

**v4.1.0 plan:** once the Store submission is live, evaluate Azure Trusted Signing for the GitHub Releases NSIS installer so the two distribution paths have parity. Added to the roadmap.
