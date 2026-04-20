# Build RBX15-Maker.msix from the electron-builder win-unpacked output.
#
# Assumes `npm run build` already produced `dist/win-unpacked/`. If you
# haven't, this script will run it for you via the -Build switch.
#
# Identity values (Name, Publisher CN, PublisherDisplayName) are hardcoded
# in Package.appxmanifest.template per the Partner Center reservation for
# the 626 Labs LLC account.
#
# Emits `dist/RBX15-Maker-v{version}.msix`.
#
# Flags:
#   -Build       Run `npm run build` first to produce the unpacked output.
#   -SelfSign    Also self-sign the MSIX with a local cert matching the
#                manifest's Publisher CN. Required to sideload-install on
#                your own machine for testing. NOT used for Store submission
#                -- leave the MSIX unsigned and let Store ingestion sign it.
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1
#   powershell -ExecutionPolicy Bypass -File windows/msix/make-msix.ps1 -Build -SelfSign

[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$SelfSign
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $RepoRoot

# -- Version from package.json, 4-part (MSIX requires A.B.C.D) --
$pkg = Get-Content "package.json" -Raw | ConvertFrom-Json
$baseVersion = $pkg.version
if ($baseVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Unexpected package.json version '$baseVersion'. Expected MAJOR.MINOR.PATCH."
}
$Version = "$baseVersion.0"
Write-Host "-> MSIX version: $Version" -ForegroundColor Cyan

# Publisher CN is hardcoded in the manifest template (not a secret - it's
# baked into every signed MSIX anyway). No parameter injection needed.
$PublisherCN = "CN=177BCE59-0966-4975-9962-10E36652141F"

# -- Optional build step --
if ($Build) {
    Write-Host "-> npm run build..." -ForegroundColor Cyan
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
}

$Unpacked = Join-Path $RepoRoot "dist/win-unpacked"
if (-not (Test-Path $Unpacked)) {
    throw "dist/win-unpacked/ not found -- re-run with -Build, or run 'npm run build' first."
}

# -- Locate Windows SDK's MakeAppx + SignTool --
$SdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter "10.*" -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName "x64" } |
    Where-Object { Test-Path (Join-Path $_ "MakeAppx.exe") } |
    Select-Object -First 1

if (-not $SdkBin) {
    throw "Windows SDK not found. Install from https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/ (need MakeAppx.exe + SignTool.exe)"
}
$MakeAppx = Join-Path $SdkBin "MakeAppx.exe"
$SignTool = Join-Path $SdkBin "SignTool.exe"
Write-Host "-> SDK: $SdkBin" -ForegroundColor Cyan

# -- Ensure MSIX images exist --
$MsixDir = Join-Path $RepoRoot "windows/msix"
$ImagesDir = Join-Path $MsixDir "Images"
if (-not (Test-Path (Join-Path $ImagesDir "StoreLogo.png"))) {
    Write-Host "-> Generating MSIX images..." -ForegroundColor Cyan
    Push-Location $MsixDir
    try { python make-msix-images.py } finally { Pop-Location }
}

# -- Stage the pack tree --
$Stage = Join-Path $MsixDir "_stage"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $Stage
New-Item -ItemType Directory -Force $Stage | Out-Null

Write-Host "-> Staging files..." -ForegroundColor Cyan
Copy-Item "$Unpacked/*" $Stage -Recurse -Force
Copy-Item $ImagesDir $Stage -Recurse -Force

# Inject version into manifest template (Publisher CN is already hardcoded)
$manifest = Get-Content (Join-Path $MsixDir "Package.appxmanifest.template") -Raw
$manifest = $manifest -replace '\{\{VERSION\}\}', $Version
Set-Content (Join-Path $Stage "AppxManifest.xml") -Value $manifest -Encoding UTF8

# -- Pack --
$OutDir = Join-Path $RepoRoot "dist"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$MsixPath = Join-Path $OutDir "RBX15-Maker-v$Version.msix"
Remove-Item $MsixPath -ErrorAction SilentlyContinue

Write-Host "-> Packing MSIX..." -ForegroundColor Cyan
& $MakeAppx pack /d $Stage /p $MsixPath /nv /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx pack failed (exit $LASTEXITCODE)"
}

# -- Optional local self-sign --
if ($SelfSign) {
    Write-Host "-> Self-signing for local sideload test..." -ForegroundColor Yellow
    $Cert = Get-ChildItem -Path Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $PublisherCN } |
        Select-Object -First 1
    if (-not $Cert) {
        Write-Host "   No matching cert found; creating one..." -ForegroundColor Yellow
        $Cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $PublisherCN `
            -KeyUsage DigitalSignature `
            -FriendlyName "RBX15 Maker MSIX Dev Cert" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        Write-Host "   Created cert thumbprint: $($Cert.Thumbprint)"
        Write-Host "   NOTE: To sideload-install the signed MSIX, trust this cert once:"
        Write-Host "     Export-Certificate -Cert Cert:\CurrentUser\My\$($Cert.Thumbprint) -FilePath rbx15-maker-dev.cer"
        Write-Host "     Then import rbx15-maker-dev.cer into 'Trusted People' in certlm.msc"
    }
    & $SignTool sign /fd SHA256 /a /sha1 $Cert.Thumbprint $MsixPath
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool sign failed (exit $LASTEXITCODE)"
    }
}

Write-Host ""
Write-Host "-> MSIX: $MsixPath" -ForegroundColor Green
Write-Host "   Size: $([math]::Round((Get-Item $MsixPath).Length / 1MB, 2)) MB"
if (-not $SelfSign) {
    Write-Host "   (Unsigned - Store ingestion signs it on upload.)" -ForegroundColor Gray
}
