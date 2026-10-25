# The MSIX contract assertions, shared by build-msix.ps1 (the real build) and Test-BuildMsix.ps1 (no packing, no
# signing). Every failure throws with a named reason. Compatible with Windows PowerShell 5.1 and PowerShell 7.

# The Store identity contract. Kept EXACTLY as 4.1 (windows/msix/Package.appxmanifest.template).
function Get-MsixContract {
    return @{
        IdentityName         = "626LabsLLC.RBX15ClassicShirtandPantsMaker"
        Publisher            = "CN=177BCE59-0966-4975-9962-10E36652141F"
        PublisherDisplayName = "626Labs LLC"
        ApplicationId        = "RBXMaker"
        MinVersion           = "10.0.17763.0"
        DripsCount           = 156
        WordArtCount         = 25
        FontFamilyCount      = 18
        NativeLibraries      = @("libSkiaSharp.dll", "libHarfBuzzSharp.dll")
    }
}

function Assert-Equal($what, $actual, $expected) {
    if ($actual -cne $expected) { throw "Manifest $what is '$actual', expected '$expected'." }
}

# Renders the manifest template text (the two placeholders build-msix.ps1 fills).
function Expand-ManifestTemplate([string]$TemplateText, [string]$Version, [string]$Executable) {
    return $TemplateText.Replace("{{VERSION}}", $Version).Replace("{{EXECUTABLE}}", $Executable)
}

# Asserts a rendered manifest against the contract: identity, application entry, MinVersion, capabilities, .r15proj.
function Assert-ManifestContract([string]$ManifestText, [string]$Version, [string]$Executable, [hashtable]$Contract = (Get-MsixContract)) {
    [xml]$m = $ManifestText
    $ns = New-Object System.Xml.XmlNamespaceManager($m.NameTable)
    $ns.AddNamespace("p", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
    $ns.AddNamespace("rescap", "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities")
    $id = $m.SelectSingleNode("/p:Package/p:Identity", $ns)
    Assert-Equal "Identity Name" $id.Name $Contract.IdentityName
    Assert-Equal "Identity Publisher" $id.Publisher $Contract.Publisher
    Assert-Equal "Identity Version" $id.Version $Version
    Assert-Equal "PublisherDisplayName" $m.SelectSingleNode("/p:Package/p:Properties/p:PublisherDisplayName", $ns).InnerText $Contract.PublisherDisplayName
    $app = $m.SelectSingleNode("/p:Package/p:Applications/p:Application", $ns)
    Assert-Equal "Application Id" $app.Id $Contract.ApplicationId
    Assert-Equal "Application Executable" $app.Executable $Executable
    Assert-Equal "Application EntryPoint" $app.EntryPoint "Windows.FullTrustApplication"
    $target = $m.SelectSingleNode("/p:Package/p:Dependencies/p:TargetDeviceFamily", $ns)
    if (-not $target) { throw "Manifest has no TargetDeviceFamily." }
    Assert-Equal "TargetDeviceFamily Name" $target.Name "Windows.Desktop"
    Assert-Equal "TargetDeviceFamily MinVersion" $target.MinVersion $Contract.MinVersion
    $caps = @($m.SelectNodes("/p:Package/p:Capabilities/*", $ns))
    if ($caps.Count -ne 1 -or $caps[0].Name -ne "runFullTrust") {
        throw "Manifest capabilities must be exactly runFullTrust; found: $(($caps | ForEach-Object { $_.Name }) -join ', ')."
    }
    $ext = $m.SelectSingleNode("//uap:FileType", $ns)
    if (-not $ext -or $ext.InnerText -ne ".r15proj") { throw "Manifest is missing the .r15proj file type association." }
}

# Asserts a package root (the unpacked MSIX, or the staging folder before packing) holds what the app needs at run
# time: both built-in packs, every font family with its license, and the two native libraries.
function Assert-PackageContents([string]$Root, [hashtable]$Contract = (Get-MsixContract)) {
    foreach ($pack in @(@("drips", $Contract.DripsCount), @("word_art", $Contract.WordArtCount))) {
        $dir = Join-Path $Root "packs\$($pack[0])"
        if (-not (Test-Path $dir)) { throw "Package is missing packs\$($pack[0])." }
        $n = @(Get-ChildItem $dir -Filter "*.png" -File).Count
        if ($n -ne $pack[1]) { throw "Package packs\$($pack[0]) holds $n PNGs, expected $($pack[1])." }
    }
    $fontRoot = Join-Path $Root "assets\fonts"
    if (-not (Test-Path $fontRoot)) { throw "Package is missing assets\fonts." }
    $families = @(Get-ChildItem $fontRoot -Directory)
    if ($families.Count -ne $Contract.FontFamilyCount) {
        throw "Package assets\fonts holds $($families.Count) font families, expected $($Contract.FontFamilyCount)."
    }
    foreach ($f in $families) {
        $files = @(Get-ChildItem $f.FullName -File)
        if (-not ($files | Where-Object { $_.Extension -in ".ttf", ".otf" })) { throw "Font family '$($f.Name)' has no .ttf or .otf file." }
        if (-not ($files | Where-Object { $_.Name -like "OFL*.txt" -or $_.Name -like "LICENSE*" })) { throw "Font family '$($f.Name)' has no license file (OFL.txt or LICENSE.txt)." }
    }
    foreach ($lib in $Contract.NativeLibraries) {
        if (-not (Test-Path (Join-Path $Root $lib))) { throw "Package is missing the native library $lib." }
    }
}

function Get-FolderSizeBytes([string]$Path) {
    $sum = (Get-ChildItem $Path -Recurse -File | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $sum) { return 0 }
    return [long]$sum
}

Export-ModuleMember -Function Get-MsixContract, Expand-ManifestTemplate, Assert-ManifestContract, Assert-PackageContents, Get-FolderSizeBytes
