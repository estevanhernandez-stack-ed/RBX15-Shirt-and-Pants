<#
.SYNOPSIS
  Builds the v5 (native WPF / .NET 10) MSIX and the unsigned portable zip.

.DESCRIPTION
  1. Refuses to run if the EnableTestInputBridge environment variable is set.
  2. dotnet publish src/RBX15.App, Release, win-x64, self-contained, into a staging folder.
  3. Renders windows/msix/Package.v5.appxmanifest.template, asserts the Store identity and capabilities
     against the contract below, checks every manifest-referenced image exists at its required pixel size.
  4. makeappx pack (with validation; /nv is NOT used) -> RBX15-Maker-v<Version>.msix
  5. makeappx unpack of the finished package, then runs BridgeExclusionTests.The_packaged_app_has_no_bridge
     against the PACKAGED RBX15.App.dll (fails the build if a test-bridge type or string is present).
  6. -SelfSign: signs the MSIX with a local dev certificate whose subject equals the manifest Publisher.
  7. Zips the same publish folder -> RBX15-Maker-v<Version>-portable-win-x64.zip (unsigned).

  Compatible with Windows PowerShell 5.1 and PowerShell 7. Run from anywhere; paths resolve from the repo root.

.PARAMETER Version
  4-part MSIX version, A.B.C.0 (the Store reserves the 4th part). Defaults to the csproj <Version> plus ".0".
  The first three parts must equal the csproj <Version>; a mismatch fails the build.

.PARAMETER OutDir
  Output folder (relative paths resolve from the repo root). Default: dist.

.PARAMETER SelfSign
  Sideload testing only. Signs with a dev cert (subject = manifest Publisher) kept as a .pfx under
  %LOCALAPPDATA%\RBX15\msix-dev (password DPAPI-protected). Nothing is installed into any certificate store and
  nothing is trusted: to install the signed package you must trust the exported .cer yourself, as admin.
  Never use this output for Store submission; the Store package stays unsigned.

.PARAMETER SkipPublish
  Reuse the staging publish folder from a previous run (<OutDir>/_stage/RBX15-Maker-v<Version>-portable-win-x64).

.EXAMPLE
  pwsh -File scripts/build-msix.ps1 -Version 5.0.0.0
  powershell -ExecutionPolicy Bypass -File scripts/build-msix.ps1 -Version 5.0.0.0 -SelfSign
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutDir = "dist",
    [switch]$SelfSign,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

# ---- The Store identity contract. Kept EXACTLY as 4.1 (windows/msix/Package.appxmanifest.template). -------------
$Contract = @{
    IdentityName         = "626LabsLLC.RBX15ClassicShirtandPantsMaker"
    Publisher            = "CN=177BCE59-0966-4975-9962-10E36652141F"
    PublisherDisplayName = "626Labs LLC"
    ApplicationId        = "RBXMaker"
}

function Step($text) { Write-Host "-> $text" -ForegroundColor Cyan }

# ---- 1. Guard: the test input bridge never ships. -----------------------------------------------------------------
if (-not [string]::IsNullOrEmpty($env:EnableTestInputBridge)) {
    throw "EnableTestInputBridge is set in the environment ('$($env:EnableTestInputBridge)'). A packaged RBX15 never carries the test input bridge; clear it and re-run."
}

# ---- Version ------------------------------------------------------------------------------------------------------
$csproj = Join-Path $RepoRoot "src/RBX15.App/RBX15.App.csproj"
$csprojVersion = ([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($csprojVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Unexpected <Version> '$csprojVersion' in RBX15.App.csproj. Expected MAJOR.MINOR.PATCH."
}
if (-not $Version) { $Version = "$csprojVersion.0" }
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.0$') {
    throw "Version '$Version' is invalid. MSIX needs A.B.C.0 (the Store reserves the 4th part for itself)."
}
if ($Version -ne "$csprojVersion.0") {
    throw "Version '$Version' does not match RBX15.App.csproj <Version> '$csprojVersion'. Bump the csproj first so the exe, the header and the package agree."
}
Step "Version: $Version"

if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $OutDir = Join-Path $RepoRoot $OutDir }
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force $OutDir | Out-Null

# ---- Windows SDK tools --------------------------------------------------------------------------------------------
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$SdkBin = Get-ChildItem $sdkRoot -Filter "10.*" -Directory -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName "x64" } |
    Where-Object { Test-Path (Join-Path $_ "makeappx.exe") } |
    Select-Object -First 1
if (-not $SdkBin) { throw "Windows SDK not found under $sdkRoot (need makeappx.exe and signtool.exe)." }
$MakeAppx = Join-Path $SdkBin "makeappx.exe"
$SignTool = Join-Path $SdkBin "signtool.exe"
Step "SDK: $SdkBin"

# ---- 2. Publish ---------------------------------------------------------------------------------------------------
$BaseName   = "RBX15-Maker-v$Version"
$ZipName    = "$BaseName-portable-win-x64"
$Stage      = Join-Path $OutDir "_stage"
$PublishDir = Join-Path $Stage $ZipName          # also the zip's top-level folder name
$MsixStage  = Join-Path $Stage "msix"
$Artifacts  = Join-Path $Stage "_build"

if ($SkipPublish) {
    if (-not (Test-Path (Join-Path $PublishDir "RBX15.App.exe"))) { throw "-SkipPublish: no publish folder at $PublishDir. Run once without it." }
    Step "Reusing publish folder $PublishDir"
} else {
    Remove-Item -Recurse -Force $Stage -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $PublishDir | Out-Null
    Step "dotnet publish (Release, win-x64, self-contained, ReadyToRun, no trim, no single-file)"
    # Choices: self-contained so a clean machine needs no .NET; ReadyToRun for startup time (verified by launching the
    # published exe); NOT trimmed and NOT single-file because WPF and the SkiaSharp native libraries break under both;
    # no symbols in the shipped payload; English-only satellite resources (the app and the manifest are en-US).
    & dotnet publish $csproj -c Release -r win-x64 --self-contained true `
        -o $PublishDir --artifacts-path $Artifacts `
        -p:PublishReadyToRun=true -p:PublishTrimmed=false -p:PublishSingleFile=false `
        -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=en `
        --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
    # DebugType=none covers our assemblies; the SkiaSharp and HarfBuzz native packages still ship ~119 MB of .pdb files.
    Get-ChildItem $PublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
}

$ExeName = "RBX15.App.exe"
if (-not (Test-Path (Join-Path $PublishDir $ExeName))) { throw "$ExeName missing from the publish output." }

# ---- 3. Stage and render the manifest -----------------------------------------------------------------------------
Step "Staging the MSIX payload"
Remove-Item -Recurse -Force $MsixStage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $MsixStage | Out-Null
Copy-Item (Join-Path $PublishDir "*") $MsixStage -Recurse -Force
$ImagesSrc = Join-Path $RepoRoot "windows/msix/Images"
Copy-Item $ImagesSrc $MsixStage -Recurse -Force

$template = Get-Content (Join-Path $RepoRoot "windows/msix/Package.v5.appxmanifest.template") -Raw
$manifestText = $template.Replace("{{VERSION}}", $Version).Replace("{{EXECUTABLE}}", $ExeName)
$manifestPath = Join-Path $MsixStage "AppxManifest.xml"
[System.IO.File]::WriteAllText($manifestPath, $manifestText, (New-Object System.Text.UTF8Encoding($false)))

# Assert the rendered manifest against the contract.
[xml]$m = Get-Content $manifestPath -Raw
$ns = New-Object System.Xml.XmlNamespaceManager($m.NameTable)
$ns.AddNamespace("p", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
$ns.AddNamespace("rescap", "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities")
function Assert-Equal($what, $actual, $expected) {
    if ($actual -cne $expected) { throw "Manifest $what is '$actual', expected '$expected'." }
}
$id = $m.SelectSingleNode("/p:Package/p:Identity", $ns)
Assert-Equal "Identity Name" $id.Name $Contract.IdentityName
Assert-Equal "Identity Publisher" $id.Publisher $Contract.Publisher
Assert-Equal "Identity Version" $id.Version $Version
Assert-Equal "PublisherDisplayName" $m.SelectSingleNode("/p:Package/p:Properties/p:PublisherDisplayName", $ns).InnerText $Contract.PublisherDisplayName
$app = $m.SelectSingleNode("/p:Package/p:Applications/p:Application", $ns)
Assert-Equal "Application Id" $app.Id $Contract.ApplicationId
Assert-Equal "Application Executable" $app.Executable $ExeName
Assert-Equal "Application EntryPoint" $app.EntryPoint "Windows.FullTrustApplication"
$caps = @($m.SelectNodes("/p:Package/p:Capabilities/*", $ns))
if ($caps.Count -ne 1 -or $caps[0].Name -ne "runFullTrust") {
    throw "Manifest capabilities must be exactly runFullTrust; found: $(($caps | ForEach-Object { $_.Name }) -join ', ')."
}
$ext = $m.SelectSingleNode("//uap:FileType", $ns)
if (-not $ext -or $ext.InnerText -ne ".r15proj") { throw "Manifest is missing the .r15proj file type association." }
Step "Manifest matches the identity contract (Name, Publisher, PublisherDisplayName, Application Id, runFullTrust only, .r15proj)"

# Every manifest-referenced image exists at its required pixel size (4.0.0 was rejected for a tile name mismatch).
function Get-PngSize($path) {
    $b = [System.IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 24 -or $b[1] -ne 0x50 -or $b[2] -ne 0x4E -or $b[3] -ne 0x47) { throw "$path is not a PNG." }
    $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
    $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
    return "${w}x${h}"
}
$expected = [ordered]@{
    "Images\StoreLogo.png"        = "50x50"
    "Images\Square44x44Logo.png"  = "44x44"
    "Images\Square71x71Logo.png"  = "71x71"
    "Images\Square150x150Logo.png" = "150x150"
    "Images\Square310x310Logo.png" = "310x310"
    "Images\Wide310x150Logo.png"  = "310x150"
    "Images\SplashScreen.png"     = "620x300"
}
$referenced = [regex]::Matches($manifestText, '(?:Logo|Image)(?:="|>)(Images\\[^"<]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($rel in $referenced) {
    $file = Join-Path $MsixStage $rel
    if (-not (Test-Path $file)) { throw "Manifest references $rel but the file is not in the package." }
    if ($expected.Contains($rel)) {
        $size = Get-PngSize $file
        if ($size -ne $expected[$rel]) { throw "$rel is $size, the manifest needs $($expected[$rel])." }
    }
}
Step "All $(@($referenced).Count) manifest-referenced images exist at the required sizes"

# ---- 4. Pack ------------------------------------------------------------------------------------------------------
$MsixPath = Join-Path $OutDir "$BaseName.msix"
Remove-Item $MsixPath -ErrorAction SilentlyContinue
Step "makeappx pack (with validation)"
& $MakeAppx pack /d $MsixStage /p $MsixPath /o | Where-Object { $_ -notmatch '^Processing ' }
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed (exit $LASTEXITCODE)" }

# ---- 5. Bridge-exclusion scan of the PACKAGED RBX15.App.dll -------------------------------------------------------
$Unpacked = Join-Path $Stage "unpacked"
Remove-Item -Recurse -Force $Unpacked -ErrorAction SilentlyContinue
& $MakeAppx unpack /p $MsixPath /d $Unpacked /o /nv | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx unpack failed (exit $LASTEXITCODE)" }
$PackagedDll = Join-Path $Unpacked "RBX15.App.dll"
if (-not (Test-Path $PackagedDll)) { throw "RBX15.App.dll is not in the unpacked MSIX." }

Step "Bridge-exclusion scan of the packaged RBX15.App.dll"
$trxDir = Join-Path $Stage "scan-results"
Remove-Item -Recurse -Force $trxDir -ErrorAction SilentlyContinue
$env:RBX15_PACKAGED_APP_DLL = $PackagedDll
try {
    & dotnet test (Join-Path $RepoRoot "src/RBX15.App.Tests/RBX15.App.Tests.csproj") -c Release `
        --artifacts-path (Join-Path $Stage "_build-test") `
        --filter "FullyQualifiedName~BridgeExclusionTests.The_packaged_app_has_no_bridge" `
        --logger "trx;LogFileName=bridge-scan.trx" --results-directory $trxDir --nologo -v minimal
    $testExit = $LASTEXITCODE
} finally {
    Remove-Item Env:\RBX15_PACKAGED_APP_DLL -ErrorAction SilentlyContinue
}
if ($testExit -ne 0) { throw "Bridge-exclusion scan FAILED (dotnet test exit $testExit): the packaged RBX15.App.dll carries test-bridge types or strings." }
# Zero matching tests also exits 0, so prove the test really ran and passed.
[xml]$trx = Get-Content (Join-Path $trxDir "bridge-scan.trx") -Raw
$ran = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.testName -like "*The_packaged_app_has_no_bridge" })
if ($ran.Count -ne 1 -or $ran[0].outcome -ne "Passed") { throw "Bridge-exclusion scan did not run exactly one passing test (found $($ran.Count))." }
Step "Packaged RBX15.App.dll holds no bridge type and no bridge string"

# ---- 6. Optional self-sign (sideload testing only) ----------------------------------------------------------------
if ($SelfSign) {
    Step "Self-signing for a local sideload test (dev certificate; nothing is installed or trusted)"
    $devDir = Join-Path $env:LOCALAPPDATA "RBX15\msix-dev"
    New-Item -ItemType Directory -Force $devDir | Out-Null
    $pfx = Join-Path $devDir "rbx15-dev.pfx"
    $pwdFile = Join-Path $devDir "rbx15-dev.pwd"
    $cerOut = Join-Path $devDir "rbx15-dev.cer"
    if (-not ((Test-Path $pfx) -and (Test-Path $pwdFile))) {
        Write-Host "   Creating the dev certificate (subject $($Contract.Publisher))" -ForegroundColor Yellow
        $bytes = New-Object byte[] 24
        [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
        $plain = [Convert]::ToBase64String($bytes)
        $rsa = [System.Security.Cryptography.RSA]::Create(2048)
        $subject = New-Object System.Security.Cryptography.X509Certificates.X500DistinguishedName($Contract.Publisher)
        $req = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
            $subject, $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $req.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($false, $false, 0, $true)))
        $req.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true)))
        $oids = New-Object System.Security.Cryptography.OidCollection
        [void]$oids.Add((New-Object System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.3")))
        $req.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($oids, $true)))
        $cert = $req.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(2))
        [System.IO.File]::WriteAllBytes($pfx, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $plain))
        [System.IO.File]::WriteAllBytes($cerOut, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        ConvertTo-SecureString $plain -AsPlainText -Force | ConvertFrom-SecureString | Set-Content $pwdFile
        $cert.Dispose(); $rsa.Dispose()
    }
    $secure = Get-Content $pwdFile | ConvertTo-SecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $pfxPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }

    & $SignTool sign /fd SHA256 /f $pfx /p $pfxPassword $MsixPath
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed (exit $LASTEXITCODE)" }
    $sig = Get-AuthenticodeSignature $MsixPath
    if (-not $sig.SignerCertificate -or $sig.SignerCertificate.Subject -ne $Contract.Publisher) {
        throw "Signed package's signer is '$($sig.SignerCertificate.Subject)', expected the manifest Publisher."
    }
    Write-Host "   Signed. Signer subject equals the manifest Publisher." -ForegroundColor Green
    Write-Host "   Public certificate (trust it yourself, as admin, only on a test machine): $cerOut" -ForegroundColor Yellow
    Write-Host "   See windows/msix/README.md, 'v5 sideload upgrade test'." -ForegroundColor Yellow
}

# ---- 7. Portable zip (unsigned) -----------------------------------------------------------------------------------
$ZipPath = Join-Path $OutDir "$ZipName.zip"
Remove-Item $ZipPath -ErrorAction SilentlyContinue
Step "Zipping the publish folder"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)

Write-Host ""
Write-Host "-> MSIX: $MsixPath ($([math]::Round((Get-Item $MsixPath).Length / 1MB, 2)) MB)" -ForegroundColor Green
if (-not $SelfSign) { Write-Host "   (Unsigned: Store ingestion signs it on upload.)" -ForegroundColor Gray }
Write-Host "-> ZIP:  $ZipPath ($([math]::Round((Get-Item $ZipPath).Length / 1MB, 2)) MB, unsigned)" -ForegroundColor Green
