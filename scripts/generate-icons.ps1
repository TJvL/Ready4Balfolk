#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptDir
$Svg = Join-Path $ProjectRoot 'Ready4Balfolk.UI' 'Assets' 'icon.svg'
$Out = Join-Path $ProjectRoot 'Ready4Balfolk.UI' 'Assets'
$HashFile = Join-Path $Out '.icon-hash'

$Sizes = @(16, 24, 32, 48, 64, 128, 256, 512, 1024)
$IcoSizes = @(16, 24, 32, 48, 256)

# --- Hash check ---
$CurrentHash = (Get-FileHash -Algorithm SHA256 $Svg).Hash

$AllExist = (Test-Path (Join-Path $Out 'icon.ico'))
if ($AllExist) {
    foreach ($size in $Sizes) {
        if (-not (Test-Path (Join-Path $Out "icon-${size}.png"))) {
            $AllExist = $false
            break
        }
    }
}

if ($AllExist -and (Test-Path $HashFile)) {
    $StoredHash = (Get-Content $HashFile -Raw).Trim()
    if ($CurrentHash -eq $StoredHash) {
        Write-Host 'Icons up to date (SVG unchanged).'
        exit 0
    }
}

# --- Tool detection ---
$magick = Get-Command 'magick' -ErrorAction SilentlyContinue
$portableMagick = Join-Path $ScriptDir 'imagemagick' 'magick.exe'

if (-not $magick -and (Test-Path $portableMagick)) {
    $env:PATH = (Split-Path $portableMagick) + [IO.Path]::PathSeparator + $env:PATH
    $magick = Get-Command 'magick' -ErrorAction SilentlyContinue
}

if (-not $magick) {
    Write-Host 'ERROR: ImageMagick not found.'
    Write-Host '  Install it with:'
    Write-Host '    winget install ImageMagick.ImageMagick'
    Write-Host '  Or install a portable copy:'
    Write-Host '    pwsh scripts/install-portable-imagemagick.ps1'
    exit 1
}

Write-Host 'Generating icons (magick)...'

# --- Generate PNGs ---
foreach ($size in $Sizes) {
    $outFile = Join-Path $Out "icon-${size}.png"
    & magick -background none -density 1200 $Svg -resize "${size}x${size}" $outFile
    if ($LASTEXITCODE -ne 0) { throw "magick failed for size ${size}" }
    Write-Host "  ${size}x${size}"
}

# --- Generate ICO ---
$icoInputs = $IcoSizes | ForEach-Object { Join-Path $Out "icon-${_}.png" }
& magick @icoInputs (Join-Path $Out 'icon.ico')
if ($LASTEXITCODE -ne 0) { throw 'magick failed for icon.ico' }
Write-Host '  icon.ico'

# --- Save hash ---
$CurrentHash | Out-File -FilePath $HashFile -NoNewline -Encoding utf8
Write-Host 'Done!'
