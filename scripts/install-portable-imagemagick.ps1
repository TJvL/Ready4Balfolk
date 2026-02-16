#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$InstallDir = Join-Path $ScriptDir 'imagemagick'

# --- Find latest portable download URL via GitHub API ---
Write-Host 'Querying GitHub for latest ImageMagick release...'
$release = Invoke-RestMethod -Uri 'https://api.github.com/repos/ImageMagick/ImageMagick/releases/latest'
$asset = $release.assets | Where-Object { $_.name -match 'portable-Q16-HDRI-x64\.7z$' } | Select-Object -First 1

if (-not $asset) {
    Write-Host 'ERROR: Could not find portable Windows download in latest release.'
    Write-Host '  Install ImageMagick globally instead:'
    Write-Host '    winget install ImageMagick.ImageMagick'
    exit 1
}

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

# --- Download and extract ---
Write-Host "Downloading $($asset.name)..."
$archivePath = Join-Path $env:TEMP $asset.name

Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath

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
