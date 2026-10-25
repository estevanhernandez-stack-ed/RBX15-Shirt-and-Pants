<#
.SYNOPSIS
  Tests the MSIX contract assertions in msix-contract.psm1 without packing or signing anything.

.DESCRIPTION
  Renders windows/msix/Package.v5.appxmanifest.template and runs the same assertion functions build-msix.ps1 runs,
  then proves each one fails with its named reason on a deliberately broken input. The package-contents cases run
  against a scratch copy of packs/ and assets/fonts under the system temp folder. Exits 1 if any case misbehaves.

.EXAMPLE
  pwsh scripts/Test-BuildMsix.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "msix-contract.psm1") -Force
$Contract = Get-MsixContract
$Version = "5.0.0.0"
$Exe = "RBX15.App.exe"
$template = Get-Content (Join-Path $RepoRoot "windows/msix/Package.v5.appxmanifest.template") -Raw
$good = Expand-ManifestTemplate $template $Version $Exe

$script:failures = 0
function Pass($name) { Write-Host "  ok    $name" -ForegroundColor Green }
function Fail($name, $why) { Write-Host "  FAIL  $name : $why" -ForegroundColor Red; $script:failures++ }

function Expect-Ok($name, [scriptblock]$body) {
    try { & $body; Pass $name } catch { Fail $name "threw: $($_.Exception.Message)" }
}
function Expect-Throw($name, $reasonPattern, [scriptblock]$body) {
    try { & $body } catch {
        if ($_.Exception.Message -match $reasonPattern) { Pass "$name -> $($_.Exception.Message)" }
        else { Fail $name "threw '$($_.Exception.Message)', expected /$reasonPattern/" }
        return
    }
    Fail $name "did not throw"
}

Write-Host "Manifest contract" -ForegroundColor Cyan
Expect-Ok "the rendered template passes" { Assert-ManifestContract $good $Version $Exe $Contract }
Expect-Throw "a wrong version" "Identity Version is '5\.0\.0\.0', expected '5\.1\.0\.0'" { Assert-ManifestContract $good "5.1.0.0" $Exe $Contract }
Expect-Throw "a wrong MinVersion" "MinVersion is '10\.0\.19041\.0', expected '10\.0\.17763\.0'" {
    Assert-ManifestContract $good.Replace('MinVersion="10.0.17763.0"', 'MinVersion="10.0.19041.0"') $Version $Exe $Contract }
Expect-Throw "a wrong identity name" "Identity Name" {
    Assert-ManifestContract $good.Replace('Name="626LabsLLC.RBX15ClassicShirtandPantsMaker"', 'Name="Other.Name"') $Version $Exe $Contract }
Expect-Throw "a second capability" "exactly runFullTrust; found: runFullTrust, internetClient" {
    Assert-ManifestContract $good.Replace('<rescap:Capability Name="runFullTrust" />', '<rescap:Capability Name="runFullTrust" /><Capability Name="internetClient" />') $Version $Exe $Contract }
Expect-Throw "no runFullTrust" "exactly runFullTrust" {
    Assert-ManifestContract $good.Replace('<rescap:Capability Name="runFullTrust" />', '') $Version $Exe $Contract }
Expect-Throw "a missing .r15proj" "missing the \.r15proj file type" {
    Assert-ManifestContract $good.Replace('<uap:FileType>.r15proj</uap:FileType>', '<uap:FileType>.png</uap:FileType>') $Version $Exe $Contract }
Expect-Throw "a wrong entry point" "Application EntryPoint" {
    Assert-ManifestContract $good.Replace('Windows.FullTrustApplication', 'Windows.PartialTrustApplication') $Version $Exe $Contract }
Expect-Throw "a wrong executable" "Application Executable" { Assert-ManifestContract $good $Version "Other.exe" $Contract }

Write-Host "Package contents" -ForegroundColor Cyan
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) "rbx15-test-buildmsix-$([guid]::NewGuid().ToString('N'))"
function New-Fixture {
    $root = Join-Path $scratch ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force (Join-Path $root "packs") | Out-Null
    New-Item -ItemType Directory -Force (Join-Path $root "assets") | Out-Null
    Copy-Item (Join-Path $RepoRoot "packs\*") (Join-Path $root "packs") -Recurse
    Copy-Item (Join-Path $RepoRoot "assets\fonts") (Join-Path $root "assets") -Recurse
    foreach ($lib in $Contract.NativeLibraries) { Set-Content (Join-Path $root $lib) "stub" }
    return $root
}
try {
    Expect-Ok "the repo's packs and fonts, with both native libraries, pass" { Assert-PackageContents (New-Fixture) $Contract }
    Expect-Throw "a missing drips pack" "missing packs.drips" {
        $r = New-Fixture; Remove-Item (Join-Path $r "packs\drips") -Recurse; Assert-PackageContents $r $Contract }
    Expect-Throw "one drip PNG short" "packs.drips holds 155 PNGs, expected 156" {
        $r = New-Fixture; Get-ChildItem (Join-Path $r "packs\drips") -Filter *.png | Select-Object -First 1 | Remove-Item; Assert-PackageContents $r $Contract }
    Expect-Throw "a missing word_art pack" "missing packs.word_art" {
        $r = New-Fixture; Remove-Item (Join-Path $r "packs\word_art") -Recurse; Assert-PackageContents $r $Contract }
    Expect-Throw "one word art PNG short" "packs.word_art holds 24 PNGs, expected 25" {
        $r = New-Fixture; Get-ChildItem (Join-Path $r "packs\word_art") -Filter *.png | Select-Object -First 1 | Remove-Item; Assert-PackageContents $r $Contract }
    Expect-Throw "a missing font family" "17 font families, expected 18" {
        $r = New-Fixture; Remove-Item (Join-Path $r "assets\fonts\anton") -Recurse; Assert-PackageContents $r $Contract }
    Expect-Throw "a font family with no license" "Font family 'anton' has no license" {
        $r = New-Fixture; Remove-Item (Join-Path $r "assets\fonts\anton\OFL.txt"); Assert-PackageContents $r $Contract }
    Expect-Throw "a font family with no font file" "Font family 'anton' has no \.ttf" {
        $r = New-Fixture; Remove-Item (Join-Path $r "assets\fonts\anton\Anton-Regular.ttf"); Assert-PackageContents $r $Contract }
    Expect-Throw "a missing libSkiaSharp.dll" "missing the native library libSkiaSharp\.dll" {
        $r = New-Fixture; Remove-Item (Join-Path $r "libSkiaSharp.dll"); Assert-PackageContents $r $Contract }
    Expect-Throw "a missing libHarfBuzzSharp.dll" "missing the native library libHarfBuzzSharp\.dll" {
        $r = New-Fixture; Remove-Item (Join-Path $r "libHarfBuzzSharp.dll"); Assert-PackageContents $r $Contract }
} finally {
    Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
}

Write-Host ""
if ($script:failures -gt 0) { Write-Host "$($script:failures) case(s) failed." -ForegroundColor Red; exit 1 }
Write-Host "All cases passed." -ForegroundColor Green
