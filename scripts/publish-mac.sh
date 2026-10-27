#!/usr/bin/env bash
# Publish a self-contained BrimDeck.app and a zip of it.
# Usage: scripts/publish-mac.sh [osx-arm64|osx-x64]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
rid="${1:-osx-arm64}"
case "$rid" in
  osx-arm64) arch=arm64 ;;
  osx-x64) arch=x64 ;;
  *) echo "Use osx-arm64 or osx-x64" >&2; exit 1 ;;
esac
version="$(grep -m1 '<Version>' "$root/src/BrimDeck.Mac/BrimDeck.Mac.csproj" | sed -E 's/.*<Version>([^<]+)<\/Version>.*/\1/')"
stage="$root/artifacts/mac/$rid"
pack="$root/artifacts/mac/pack-$rid"
app="$pack/BrimDeck.app"
zip="$root/artifacts/releases/BrimDeck-${version}-macos-${arch}.zip"
rm -rf "$stage" "$pack"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$root/artifacts/releases"
dotnet publish "$root/src/BrimDeck.Mac/BrimDeck.Mac.csproj" -c Release -r "$rid" --self-contained true -p:Version="$version" -o "$stage"
cp -R "$stage"/. "$app/Contents/MacOS/"
cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>BrimDeck</string>
  <key>CFBundleDisplayName</key><string>BrimDeck</string>
  <key>CFBundleIdentifier</key><string>app.brimdeck.mac</string>
  <key>CFBundleVersion</key><string>${version}</string>
  <key>CFBundleShortVersionString</key><string>${version}</string>
  <key>CFBundleExecutable</key><string>BrimDeck</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>icon.icns</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
EOF
rm -f "$zip"
( cd "$pack" && zip -r -y -q "$zip" BrimDeck.app )
echo "Wrote $app"
echo "Wrote $zip"
