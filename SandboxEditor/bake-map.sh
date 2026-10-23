#!/bin/sh
# Bakes a game map into an editor scene (stage 5). The editor must have this project open.
# usage: ./bake-map.sh <mapid: flatgrass|hills|town> <SceneName>   (PROWL_CLI = path to Prowl.Cli, default ~/Projects/Prowl/Build/Cli/Prowl.Cli)
set -e
PROJ=$(cd "$(dirname "$0")" && pwd)
C="${PROWL_CLI:-$HOME/Projects/Prowl/Build/Cli/Prowl.Cli}"; P="--project $PROJ"
rm -rf "$PROJ/Assets/Meshes/Maps/$1" "$PROJ/Assets/Materials/Maps/$1" "$PROJ/Assets/Scenes/Maps/$2.scene"*
$C command asset Refresh $P >/dev/null 2>&1 || true
$C command scene $P --action New --force >/dev/null
R=$($C command tree $P | python3 -c "import json,sys;print(','.join(x['path'] for x in json.load(sys.stdin)))")
[ -n "$R" ] && $C command go $P --action Delete --target "$R" >/dev/null
$C command eval $P "return Sandbox.SceneBaker.BakeMap(\"$1\");" 2>&1 | grep -v Importing | tail -3
$C command scene $P --action SaveAs --path "Scenes/Maps/$2.scene" | grep -E "Saved"
