#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_DIR="$SCRIPT_DIR/imagemagick"

if [[ "$(uname -s)" == "Darwin" ]]; then
  echo "macOS does not have a portable ImageMagick binary."
  echo "Install it with:"
  echo "  brew install imagemagick"
  exit 1
fi

echo "Downloading portable ImageMagick for Linux..."

mkdir -p "$INSTALL_DIR"
curl -fSL -o "$INSTALL_DIR/magick" "https://imagemagick.org/archive/binaries/magick"
chmod +x "$INSTALL_DIR/magick"

echo "Installed to: $INSTALL_DIR/magick"
"$INSTALL_DIR/magick" --version | head -1
echo "Done!"
