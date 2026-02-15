#!/usr/bin/env bash
set -euo pipefail

# Creates a macOS .app bundle from dotnet publish output.
#
# Usage: create-app-bundle.sh <publish-dir> <version> <icns-path> <output-dir>
#   publish-dir  - directory containing dotnet publish output
#   version      - application version (e.g., 1.0.0)
#   icns-path    - path to .icns icon file
#   output-dir   - directory where .app bundle will be created

PUBLISH_DIR="${1:?Usage: create-app-bundle.sh <publish-dir> <version> <icns-path> <output-dir>}"
VERSION="${2:?Version required}"
ICNS_PATH="${3:?ICNS path required}"
OUTPUT_DIR="${4:?Output directory required}"

APP_NAME="Ready4Balfolk"
BUNDLE_ID="io.github.tjvl.Ready4Balfolk"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

# Clean previous bundle
rm -rf "$APP_BUNDLE"

# Create bundle structure
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy published binaries
cp -R "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/Ready4Balfolk.UI"

# Copy icon
cp "$ICNS_PATH" "$APP_BUNDLE/Contents/Resources/AppIcon.icns"

# Create Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>Ready4Balfolk.UI</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSMicrophoneUsageDescription</key>
    <string>Ready4Balfolk needs audio access for music playback.</string>
</dict>
</plist>
PLIST

echo "Created $APP_BUNDLE"
