#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SVG="$PROJECT_ROOT/Ready4Balfolk.UI/Assets/icon.svg"
OUT="$PROJECT_ROOT/Ready4Balfolk.UI/Assets"
HASH_FILE="$OUT/.icon-hash"

SIZES=(16 24 32 48 64 128 256 512 1024)
ICO_SIZES=(16 24 32 48 256)

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
  echo "ERROR: ImageMagick not found."
  echo "  Install it with:"
  echo "    Linux:  sudo apt install imagemagick"
  echo "    macOS:  brew install imagemagick"
  echo "  Or install a portable copy:"
  echo "    bash scripts/install-portable-imagemagick.sh"
  exit 1
fi

echo "Generating icons (magick)..."

# --- Generate PNGs ---
for size in "${SIZES[@]}"; do
  out_file="$OUT/icon-${size}.png"
  magick -background none -density 1200 "$SVG" -resize "${size}x${size}" "$out_file"
  echo "  ${size}x${size}"
done

# --- Generate ICO ---
ICO_INPUTS=()
for size in "${ICO_SIZES[@]}"; do
  ICO_INPUTS+=("$OUT/icon-${size}.png")
done

magick "${ICO_INPUTS[@]}" "$OUT/icon.ico"
echo "  icon.ico"

# --- Generate ICNS (macOS only) ---
if command -v iconutil &>/dev/null; then
  ICONSET_DIR="$OUT/AppIcon.iconset"
  mkdir -p "$ICONSET_DIR"

  cp "$OUT/icon-16.png"   "$ICONSET_DIR/icon_16x16.png"
  cp "$OUT/icon-32.png"   "$ICONSET_DIR/icon_16x16@2x.png"
  cp "$OUT/icon-32.png"   "$ICONSET_DIR/icon_32x32.png"
  cp "$OUT/icon-64.png"   "$ICONSET_DIR/icon_32x32@2x.png"
  cp "$OUT/icon-128.png"  "$ICONSET_DIR/icon_128x128.png"
  cp "$OUT/icon-256.png"  "$ICONSET_DIR/icon_128x128@2x.png"
  cp "$OUT/icon-256.png"  "$ICONSET_DIR/icon_256x256.png"
  cp "$OUT/icon-512.png"  "$ICONSET_DIR/icon_256x256@2x.png"
  cp "$OUT/icon-512.png"  "$ICONSET_DIR/icon_512x512.png"
  cp "$OUT/icon-1024.png" "$ICONSET_DIR/icon_512x512@2x.png"

  iconutil -c icns "$ICONSET_DIR" -o "$OUT/AppIcon.icns"
  rm -rf "$ICONSET_DIR"
  echo "  AppIcon.icns"
else
  echo "  (skipping .icns — iconutil not available, macOS only)"
fi

# --- Save hash ---
echo "$CURRENT_HASH" > "$HASH_FILE"
echo "Done!"
