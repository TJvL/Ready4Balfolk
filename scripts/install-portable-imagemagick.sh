#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_DIR="$SCRIPT_DIR/imagemagick"

# Pinned by version and checksum, like the BASS natives in Directory.Build.targets, so that every
# contributor renders the icons with the same ImageMagick and a tampered or truncated download is
# refused rather than run. The AppImage is the Linux build ImageMagick attaches to its GitHub
# release; the SHA-256 is the digest GitHub lists for that asset. To move to a newer release, change
# both values together, and keep install-portable-imagemagick.ps1 on the same version.
VERSION="7.1.2-32"
ASSET="ImageMagick-$VERSION-gcc-x86_64.AppImage"
SHA256="d456cab221b5fc1c396768a026d0a33ee8665f7119c7fea7151b960c69058b21"
URL="https://github.com/ImageMagick/ImageMagick/releases/download/$VERSION/$ASSET"

if [[ "$(uname -s)" == "Darwin" ]]; then
  echo "macOS does not have a portable ImageMagick binary."
  echo "Install it with:"
  echo "  brew install imagemagick"
  exit 1
fi

if [[ "$(uname -m)" != "x86_64" ]]; then
  echo "ImageMagick publishes its portable Linux build for x86_64 only."
  echo "Install it from your distribution instead, for example:"
  echo "  sudo apt install imagemagick"
  exit 1
fi

echo "Downloading portable ImageMagick $VERSION for Linux..."

mkdir -p "$INSTALL_DIR"
DOWNLOAD="$INSTALL_DIR/$ASSET.download"
trap 'rm -f "$DOWNLOAD"' EXIT
curl -fSL -o "$DOWNLOAD" "$URL"

ACTUAL=$(sha256sum "$DOWNLOAD" | cut -d' ' -f1)
if [[ "$ACTUAL" != "$SHA256" ]]; then
  echo "ERROR: $ASSET does not match the SHA-256 pinned in this script."
  echo "  Expected: $SHA256"
  echo "  Got:      $ACTUAL"
  echo "The download was corrupted or tampered with; nothing was installed."
  exit 1
fi

chmod +x "$DOWNLOAD"
mv "$DOWNLOAD" "$INSTALL_DIR/magick"

echo "Installed to: $INSTALL_DIR/magick"
"$INSTALL_DIR/magick" --version | head -1
echo "Done!"
