param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\Imvix Pro-v2.0.1-win-x64"),
    [string]$PackageName = "D787ABC4.ImvixPro",
    [string]$Publisher = "CN=FA0F6293-29B7-43FB-AB9B-49D0FB5F198C",
    [string]$PublisherDisplayName = "&#24050;&#36893;&#24773;&#27527;",
    [string]$DisplayName = "Imvix Pro",
    [string]$Description = "Professional desktop conversion tool with local AI, OCR, QR, and barcode support.",
    [string]$Version = "2.0.1.0",
    [string]$Architecture = "x64",
    [string]$MinVersion = "10.0.17763.0",
    [string]$MaxVersionTested = "10.0.26100.0",
    [string]$PackageFamilyName = "D787ABC4.ImvixPro_fsfgxngdrj64r",
    [switch]$KeepStage
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishDirPath = (Resolve-Path $PublishDir).Path
$logoPath = Join-Path $repoRoot "Assets\logo.png"

if (-not (Test-Path $publishDirPath -PathType Container)) {
    throw "Publish directory not found: $PublishDir"
}

if (-not (Test-Path $logoPath -PathType Leaf)) {
    throw "Base logo not found: $logoPath"
}

$makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$signToolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

foreach ($tool in @($makeAppxPath, $signToolPath)) {
    if (-not (Test-Path $tool -PathType Leaf)) {
        throw "Required tool not found: $tool"
    }
}

$packageHash = ($PackageFamilyName -split "_", 2)[1]
if ([string]::IsNullOrWhiteSpace($packageHash)) {
    throw "PackageFamilyName must include the publisher hash suffix."
}

$packageBaseName = "{0}_{1}_{2}__{3}" -f $PackageName, $Version, $Architecture, $packageHash
$workRoot = Join-Path $repoRoot "obj\msix-pack"
$stageRoot = Join-Path $workRoot $packageBaseName
$assetRoot = Join-Path $stageRoot "Assets"
$outputPackage = Join-Path $publishDirPath ($packageBaseName + ".msix")
$certFile = Join-Path $workRoot ($PackageName + "_SigningTemp.cer")
$pfxFile = Join-Path $workRoot ($PackageName + "_SigningTemp.pfx")

if (Test-Path $outputPackage) {
    Remove-Item $outputPackage -Force
}

if (Test-Path $stageRoot) {
    Remove-Item $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
Copy-Item -LiteralPath $publishDirPath -Destination $stageRoot -Recurse -Force
New-Item -ItemType Directory -Force -Path $assetRoot | Out-Null

Add-Type -AssemblyName System.Drawing

function New-PngAsset {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter(Mandatory = $true)][int]$Width,
        [Parameter(Mandatory = $true)][int]$Height
    )

    $image = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.DrawImage($image, 0, 0, $Width, $Height)
                $bitmap.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally {
                $graphics.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $image.Dispose()
    }
}

$assets = @(
    @{ Name = "StoreLogo.png"; Width = 50; Height = 50 },
    @{ Name = "Square44x44Logo.png"; Width = 44; Height = 44 },
    @{ Name = "Square71x71Logo.png"; Width = 71; Height = 71 },
    @{ Name = "Square70x70Logo.png"; Width = 70; Height = 70 },
    @{ Name = "Square150x150Logo.png"; Width = 150; Height = 150 },
    @{ Name = "Wide310x150Logo.png"; Width = 310; Height = 150 },
    @{ Name = "Square310x310Logo.png"; Width = 310; Height = 310 }
)

foreach ($asset in $assets) {
    New-PngAsset -SourcePath $logoPath -DestinationPath (Join-Path $assetRoot $asset.Name) -Width $asset.Width -Height $asset.Height
}

$visualElementsManifest = @'
<?xml version="1.0" encoding="utf-8"?>
<Application xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <VisualElements
    ShowNameOnSquare150x150Logo="on"
    Square150x150Logo="Assets\Square150x150Logo.png"
    Square70x70Logo="Assets\Square70x70Logo.png"
    ForegroundText="light"
    BackgroundColor="#111111" />
</Application>
'@

$visualElementsManifest | Set-Content -LiteralPath (Join-Path $stageRoot "Imvix Pro.VisualElementsManifest.xml") -Encoding utf8

$appxManifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity
    Name="$PackageName"
    Publisher="$Publisher"
    Version="$Version"
    ProcessorArchitecture="$Architecture" />
  <Properties>
    <DisplayName>$DisplayName</DisplayName>
    <PublisherDisplayName>$PublisherDisplayName</PublisherDisplayName>
    <Description>$Description</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily
      Name="Windows.Desktop"
      MinVersion="$MinVersion"
      MaxVersionTested="$MaxVersionTested" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application
      Id="App"
      Executable="Imvix Pro.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="$DisplayName"
        Description="$Description"
        BackgroundColor="#111111"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile
          Square71x71Logo="Assets\Square71x71Logo.png"
          Wide310x150Logo="Assets\Wide310x150Logo.png"
          Square310x310Logo="Assets\Square310x310Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

$appxManifest | Set-Content -LiteralPath (Join-Path $stageRoot "AppxManifest.xml") -Encoding utf8

& $makeAppxPath pack /o /d $stageRoot /p $outputPackage
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed."
}

$createdCert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Publisher `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears(3) `
    -TextExtension @("2.5.29.19={text}CA=FALSE")

$pfxPasswordPlain = [Guid]::NewGuid().ToString("N") + "!"
$pfxPassword = ConvertTo-SecureString -String $pfxPasswordPlain -AsPlainText -Force

Export-Certificate -Cert $createdCert -FilePath $certFile -Force | Out-Null
Export-PfxCertificate -Cert $createdCert -FilePath $pfxFile -Password $pfxPassword -Force | Out-Null
Remove-Item -LiteralPath ("Cert:\CurrentUser\My\" + $createdCert.Thumbprint) -Force

& $signToolPath sign /fd SHA256 /f $pfxFile /p $pfxPasswordPlain $outputPackage
if ($LASTEXITCODE -ne 0) {
    throw "signtool sign failed."
}

$packageInfo = Get-Item $outputPackage
$sourceFiles = Get-ChildItem -LiteralPath $publishDirPath -Recurse -Force -File |
    Where-Object { $_.FullName -ne $outputPackage }
$sourceSizeBytes = ($sourceFiles | Measure-Object Length -Sum).Sum

if (Test-Path $pfxFile) {
    Remove-Item $pfxFile -Force
}

if (-not $KeepStage -and (Test-Path $stageRoot)) {
    Remove-Item $stageRoot -Recurse -Force
}

[pscustomobject]@{
    Package = $packageInfo.FullName
    PackageSizeBytes = $packageInfo.Length
    PackageSizeGB = [math]::Round($packageInfo.Length / 1GB, 3)
    SourceFileCount = $sourceFiles.Count
    SourceBytes = $sourceSizeBytes
    SourceSizeGB = [math]::Round($sourceSizeBytes / 1GB, 3)
    Certificate = $certFile
} | Format-List
