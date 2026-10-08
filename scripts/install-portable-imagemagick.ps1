#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$InstallDir = Join-Path $ScriptDir 'imagemagick'

# Pinned by version and checksum, like the BASS natives in Directory.Build.targets, so that every
# contributor renders the icons with the same ImageMagick and a tampered or truncated download is
# refused rather than run. The archive is the portable Windows build ImageMagick attaches to its
# GitHub release; the SHA-256 is the digest GitHub lists for that asset. To move to a newer release,
# change both values together, and keep install-portable-imagemagick.sh on the same version.
$Version = '7.1.2-32'
$AssetName = "ImageMagick-$Version-portable-Q16-HDRI-x64.7z"
$Sha256 = 'bac9155acac3147c460082282e01646596c054c613c77e6616f7e3ad0ed38e54'
$Url = "https://github.com/ImageMagick/ImageMagick/releases/download/$Version/$AssetName"

# --- Check for 7z ---
$sevenZip = Get-Command '7z' -ErrorAction SilentlyContinue
if (-not $sevenZip) {
    $commonPaths = @(
        'C:\Program Files\7-Zip\7z.exe',
        'C:\Program Files (x86)\7-Zip\7z.exe'
    )
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $sevenZip = Get-Command $path
            break
        }
    }
}

if (-not $sevenZip) {
    Write-Host 'ERROR: 7-Zip is required to extract the portable archive.'
    Write-Host '  Install 7-Zip first:'
    Write-Host '    winget install 7zip.7zip'
    Write-Host ''
    Write-Host '  Or install ImageMagick globally instead:'
    Write-Host '    winget install ImageMagick.ImageMagick'
    exit 1
}

# --- Download, verify and extract ---
Write-Host "Downloading $AssetName..."
$archivePath = Join-Path ([IO.Path]::GetTempPath()) $AssetName

Invoke-WebRequest -Uri $Url -OutFile $archivePath

$actual = (Get-FileHash -Algorithm SHA256 $archivePath).Hash.ToLower()
if ($actual -ne $Sha256) {
    Remove-Item $archivePath
    Write-Host "ERROR: $AssetName does not match the SHA-256 pinned in this script."
    Write-Host "  Expected: $Sha256"
    Write-Host "  Got:      $actual"
    Write-Host 'The download was corrupted or tampered with; nothing was installed.'
    exit 1
}

Write-Host "Extracting to $InstallDir..."
if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
New-Item -ItemType Directory -Path $InstallDir | Out-Null

& $sevenZip.Source x $archivePath "-o$InstallDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw '7z extraction failed' }

Remove-Item $archivePath

$magickPath = Join-Path $InstallDir 'magick.exe'
if (-not (Test-Path $magickPath)) {
    Write-Host 'ERROR: magick.exe not found after extraction.'
    exit 1
}

Write-Host "Installed to: $magickPath"
& $magickPath --version | Select-Object -First 1
Write-Host 'Done!'
