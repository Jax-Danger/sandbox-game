#!/usr/bin/env bash
# Builds self-contained releases for every platform into dist/ (one folder + one zip each).
# Usage: ./build-all.sh [version] [rid ...]     e.g. ./build-all.sh 0.1.0 linux-x64
set -euo pipefail
cd "$(dirname "$0")"
VERSION="${1:-0.1.0}"; shift || true
RIDS=("$@"); [ ${#RIDS[@]} -eq 0 ] && RIDS=(win-x64 linux-x64 osx-x64 osx-arm64)
rm -rf dist && mkdir -p dist
for rid in "${RIDS[@]}"; do
  echo "== $rid"
  out="dist/Sandbox-$rid"
  dotnet publish Sandbox.csproj -c Release -r "$rid" --self-contained true \
    -p:Version="$VERSION" -p:DebugType=none -o "$out"
  if [[ "$rid" == osx-* ]]; then
    # Wrap in a .app bundle; AppContext.BaseDirectory becomes Contents/MacOS so assets/ resolve.
    app="dist/Sandbox-$rid/Sandbox.app"; tmp="dist/.app-$rid"
    mv "$out" "$tmp"; mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    cp -R "$tmp"/. "$app/Contents/MacOS/"; rm -rf "$tmp"
    sed "s/__VERSION__/$VERSION/g" packaging/macos/Info.plist > "$app/Contents/Info.plist"
    chmod +x "$app/Contents/MacOS/Sandbox"
    # Apple Silicon refuses unsigned code; ad-hoc sign when building on a Mac.
    if command -v codesign >/dev/null; then codesign --force --deep --sign - "$app"; fi
  else
    [ -f "$out/Sandbox" ] && chmod +x "$out/Sandbox"
  fi
  if command -v zip >/dev/null; then (cd dist && zip -qry "Sandbox-$VERSION-$rid.zip" "Sandbox-$rid")
  else (cd dist && python3 -m zipfile -c "Sandbox-$VERSION-$rid.zip" "Sandbox-$rid"); fi
done
ls -la dist
