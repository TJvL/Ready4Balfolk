#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SVG="$PROJECT_ROOT/Ready4Balfolk.UI/Assets/icon.svg"
OUT="$PROJECT_ROOT/Ready4Balfolk.UI/Assets"
HASH_FILE="$OUT/.icon-hash"

SIZES=(16 24 32 48 64 128 256 512 1024)
ICO_SIZES=(16 24 32 48 256)

# Every PNG is downsampled from one master render. Keep this at or above the
# largest size above, or that size gets upscaled from a smaller raster.
MASTER=4096

# --- Hash helper ---
compute_hash() {
  if command -v sha256sum &>/dev/null; then
    sha256sum "$1" | cut -d' ' -f1
  elif command -v shasum &>/dev/null; then
    shasum -a 256 "$1" | cut -d' ' -f1
  else
    openssl dgst -sha256 "$1" | awk '{print $NF}'
  fi
}

# --- Hash check ---
CURRENT_HASH=$(compute_hash "$SVG")

ALL_EXIST=true
[[ -f "$OUT/icon.ico" ]] || ALL_EXIST=false
if [[ "$ALL_EXIST" == "true" ]]; then
  for size in "${SIZES[@]}"; do
    [[ -f "$OUT/icon-${size}.png" ]] || { ALL_EXIST=false; break; }
  done
fi

if [[ "$ALL_EXIST" == "true" ]] && [[ -f "$HASH_FILE" ]]; then
  STORED_HASH=$(cat "$HASH_FILE")
  if [[ "$CURRENT_HASH" == "$STORED_HASH" ]]; then
    echo "Icons up to date (SVG unchanged)."
    exit 0
  fi
fi

# --- Tool detection ---
PORTABLE_MAGICK="$SCRIPT_DIR/imagemagick/magick"
if ! command -v magick &>/dev/null && [[ -x "$PORTABLE_MAGICK" ]]; then
  export PATH="$(dirname "$PORTABLE_MAGICK"):$PATH"
fi

if ! command -v magick &>/dev/null; then
  # Fall back to convert if it is ImageMagick (v6 uses convert instead of magick)
  if command -v convert &>/dev/null && convert -version 2>&1 | grep -q "ImageMagick"; then
    magick() { convert "$@"; }
  else
    echo "ERROR: ImageMagick not found."
    echo "  Install it with:"
    echo "    Linux:  sudo apt install imagemagick"
    echo "    macOS:  brew install imagemagick"
    echo "  Or install a portable copy:"
    echo "    bash scripts/install-portable-imagemagick.sh"
    exit 1
  fi
fi

echo "Generating icons (magick)..."

# --- Render the master ---
# ImageMagick turns -density into pixels using the SVG's own units, and the ratio
# has not been the same in every version (72 vs 96 units per inch). So probe at a
# known density and scale from the size that comes back, rather than assuming one.
PROBE_DENSITY=96
BASE_WIDTH=$(magick -density "$PROBE_DENSITY" "$SVG" -format "%w" info:)

if [[ ! "$BASE_WIDTH" =~ ^[0-9]+$ ]] || [[ "$BASE_WIDTH" -eq 0 ]]; then
  echo "ERROR: could not read the intrinsic size of $SVG (got '$BASE_WIDTH')."
  exit 1
fi

DENSITY=$(awk -v m="$MASTER" -v p="$PROBE_DENSITY" -v w="$BASE_WIDTH" \
  'BEGIN { printf "%d", (m * p / w) + 0.5 }')

MASTER_PNG=$(mktemp)
trap 'rm -f "$MASTER_PNG"' EXIT

magick -background none -density "$DENSITY" "$SVG" \
  -resize "${MASTER}x${MASTER}" -depth 8 "PNG:$MASTER_PNG"
echo "  master ${MASTER}x${MASTER}"

# --- Generate PNGs ---
# -depth 8 because 16 bits per channel quadruples these files for no visible gain,
# and -strip drops the timestamp chunk so a rerun on an unchanged SVG is byte-identical.
for size in "${SIZES[@]}"; do
  out_file="$OUT/icon-${size}.png"
  magick "PNG:$MASTER_PNG" -resize "${size}x${size}" -depth 8 -strip "$out_file"
  echo "  ${size}x${size}"
done

# --- Generate ICO ---
ICO_INPUTS=()
for size in "${ICO_SIZES[@]}"; do
  ICO_INPUTS+=("$OUT/icon-${size}.png")
done

# -type TrueColorAlpha because the small PNGs above have few enough colours that
# ImageMagick stores them as palette images, and the ICO coder then writes 8bpp
# palette frames whose transparency is a 1-bit mask. That throws away the
# antialiased edges, so Windows renders the small sizes with hard jagged edges.
magick "${ICO_INPUTS[@]}" -type TrueColorAlpha -strip "$OUT/icon.ico"
echo "  icon.ico"

# --- Save hash ---
echo "$CURRENT_HASH" > "$HASH_FILE"
echo "Done!"
