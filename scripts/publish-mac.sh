#!/usr/bin/env bash
# Publish a self-contained BrimDeck.app. Run on any OS with the .NET 10 SDK.
# Usage: scripts/publish-mac.sh [osx-arm64|osx-x64]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
rid="${1:-osx-arm64}"
version="$(grep -m1 '<Version>' "$root/src/BrimDeck.Mac/BrimDeck.Mac.csproj" | sed -E 's/.*<Version>([^<]+)<\/Version>.*/\1/')"
stage="$root/artifacts/mac/$rid"
app="$root/artifacts/mac/BrimDeck-$rid.app"
rm -rf "$stage" "$app"
dotnet publish "$root/src/BrimDeck.Mac/BrimDeck.Mac.csproj" -c Release -r "$rid" --self-contained true -o "$stage"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
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
echo "Wrote $app"
