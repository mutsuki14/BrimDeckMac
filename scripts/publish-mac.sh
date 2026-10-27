#!/usr/bin/env bash
# Build a self-contained BrimDeck.app and a compressed DMG.
# DMG creation uses hdiutil, so this script runs on macOS.
# Usage: scripts/publish-mac.sh [osx-arm64|osx-x64]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
rid="${1:-osx-arm64}"
case "$rid" in
  osx-arm64) arch=arm64 ;;
  osx-x64) arch=x64 ;;
  *) echo "Use osx-arm64 or osx-x64" >&2; exit 1 ;;
esac
if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "A DMG is created with hdiutil, which is part of macOS." >&2
  exit 1
fi
version="$(grep -m1 '<Version>' "$root/src/BrimDeck.Mac/BrimDeck.Mac.csproj" | sed -E 's/.*<Version>([^<]+)<\/Version>.*/\1/')"
stage="$root/artifacts/mac/$rid"
pack="$root/artifacts/mac/pack-$rid"
app="$pack/BrimDeck.app"
dmg_root="$pack/dmg"
dmg="$root/artifacts/releases/BrimDeck-${version}-macos-${arch}.dmg"
rm -rf "$stage" "$pack"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$dmg_root" "$root/artifacts/releases"
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
cp -R "$app" "$dmg_root/BrimDeck.app"
ln -s /Applications "$dmg_root/Applications"
rm -f "$dmg"
hdiutil create -volname "BrimDeck" -srcfolder "$dmg_root" -ov -format UDZO "$dmg"
echo "Wrote $app"
echo "Wrote $dmg"
