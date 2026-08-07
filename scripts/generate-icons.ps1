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

# Every PNG is downsampled from one master render. Keep this at or above the
# largest size above, or that size gets upscaled from a smaller raster.
$Master = 4096

# --- Hash check ---
$CurrentHash = (Get-FileHash -Algorithm SHA256 $Svg).Hash.ToLower()

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

# Fall back to convert if it is ImageMagick (v6 uses convert instead of magick)
if (-not $magick) {
    $convert = Get-Command 'convert' -ErrorAction SilentlyContinue
    if ($convert -and (& $convert.Source -version 2>&1 | Select-String 'ImageMagick')) {
        Set-Alias -Name magick -Value $convert.Source -Scope Script
        $magick = $convert
    }
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

# --- Render the master ---
# ImageMagick turns -density into pixels using the SVG's own units, and the ratio
# has not been the same in every version (72 vs 96 units per inch). So probe at a
# known density and scale from the size that comes back, rather than assuming one.
$probeDensity = 96
$baseWidth = & magick -density $probeDensity $Svg -format '%w' 'info:'
if ($LASTEXITCODE -ne 0) { throw 'magick failed to probe the SVG' }

$baseWidth = [int]$baseWidth
if ($baseWidth -le 0) { throw "Could not read the intrinsic size of $Svg" }

$density = [math]::Round($Master * $probeDensity / $baseWidth)

$masterPng = Join-Path ([IO.Path]::GetTempPath()) ("icon-master-$([guid]::NewGuid()).png")
try {
    & magick -background none -density $density $Svg `
        -resize "${Master}x${Master}" -depth 8 "PNG:$masterPng"
    if ($LASTEXITCODE -ne 0) { throw 'magick failed for the master render' }
    Write-Host "  master ${Master}x${Master}"

    # --- Generate PNGs ---
    # -depth 8 because 16 bits per channel quadruples these files for no visible gain,
    # and -strip drops the timestamp chunk so a rerun on an unchanged SVG is byte-identical.
    foreach ($size in $Sizes) {
        $outFile = Join-Path $Out "icon-${size}.png"
        & magick "PNG:$masterPng" -resize "${size}x${size}" -depth 8 -strip $outFile
        if ($LASTEXITCODE -ne 0) { throw "magick failed for size ${size}" }
        Write-Host "  ${size}x${size}"
    }
}
finally {
    Remove-Item $masterPng -ErrorAction SilentlyContinue
}

# --- Generate ICO ---
$icoInputs = $IcoSizes | ForEach-Object { Join-Path $Out "icon-${_}.png" }
& magick @icoInputs -strip (Join-Path $Out 'icon.ico')
if ($LASTEXITCODE -ne 0) { throw 'magick failed for icon.ico' }
Write-Host '  icon.ico'

# --- Save hash ---
$CurrentHash | Out-File -FilePath $HashFile -NoNewline -Encoding utf8
Write-Host 'Done!'
