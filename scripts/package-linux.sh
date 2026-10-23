#!/bin/sh
# Builds the linux-x64 player through the running Prowl editor and adds the loose
# runtime files (fonts, sky shader) the game reads from <exe dir>/assets.
# Usage: scripts/package-linux.sh [out-dir]   (editor must be open on SandboxEditor)
set -e
REPO=$(cd "$(dirname "$0")/.." && pwd)
OUT=${1:-$HOME/Projects/SandboxBuild}
CLI=${PROWL_CLI:-$(find "$HOME/Projects/Prowl/Build/Cli" -maxdepth 3 -name Prowl.Cli -type f 2>/dev/null | head -1)}
rm -rf "$OUT/SandboxEditor"
"$CLI" --timeout 900 command build --project "${SANDBOX_PROJECT:-$HOME/Projects/SandboxEditor}" "$OUT"
rm -rf "$OUT/SandboxEditor/assets"
cp -r "$REPO/assets" "$OUT/SandboxEditor/assets"
echo "Packaged: $OUT/SandboxEditor/SandboxEditor"
