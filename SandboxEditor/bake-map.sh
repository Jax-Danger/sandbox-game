#!/bin/sh
# Bakes a game map into an editor scene (stage 5). The editor must have this project open.
# usage: ./bake-map.sh <mapid: flatgrass|hills|town> <SceneName>   (PROWL_CLI = path to Prowl.Cli, default ~/Projects/Prowl/Build/Cli/Prowl.Cli)
# Afterwards Scenes/MainMenu.scene is open and its MapLibrary points at the new scene.
set -e
PROJ=$(cd "$(dirname "$0")" && pwd)
C="${PROWL_CLI:-$HOME/Projects/Prowl/Build/Cli/Prowl.Cli}"; P="--project $PROJ"
rm -rf "$PROJ/Assets/Meshes/Maps/$1" "$PROJ/Assets/Materials/Maps/$1" "$PROJ/Assets/Scenes/Maps/$2.scene"*
$C command asset Refresh $P >/dev/null 2>&1 || true
$C command scene $P --action New --force >/dev/null
R=$($C command tree $P | python3 -c "import json,sys;print(','.join(x['path'] for x in json.load(sys.stdin)))")
[ -n "$R" ] && $C command go $P --action Delete --target "$R" >/dev/null
$C command eval $P "return Sandbox.SceneBaker.BakeMap(\"$1\");" 2>&1 | grep -v Importing | tail -3
$C command scene $P --action SaveAs --path "Scenes/Maps/$2.scene" 2>&1 | grep -E "Saved" || true
# Point MainMenu's MapLibrary at the new scene (a rebake gets a new asset guid).
$C command scene $P --action Open --path Scenes/MainMenu.scene >/dev/null
$C command eval $P "var lib = Prowl.Runtime.Resources.Scene.Current.AllObjects.First(o => o.Name == \"Sandbox\").GetComponent<Sandbox.MapLibrary>()!; lib.Maps.RemoveAll(m => m.Id == \"$1\"); lib.Maps.Add(new Sandbox.MapEntry { Id = \"$1\", Scene = Prowl.Runtime.AssetDatabase.Load<Prowl.Runtime.Resources.SceneAsset>(Prowl.Editor.EditorAssetBackend.Instance!.PathToGuid(\"Scenes/Maps/$2.scene\")) }); Prowl.Editor.GUI.SceneView.EditorSceneManager.MarkDirty(); return \"MapLibrary: $1 -> \" + lib.Maps.Last().Scene?.AssetID;" | tail -2
$C command scene $P --action Save >/dev/null
