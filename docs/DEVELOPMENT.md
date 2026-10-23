# Development

Build from source, asset workflow, view models, headless tests, and code layout. For players, see the [README](../README.md).

## Opening the project

1. Install the .NET SDK from `global.json` (10.0.402; `scripts/setup-fedora.sh` does everything on Fedora).
2. Build Prowl at commit `2510d8fc` (`dotnet build Prowl.Editor/Prowl.Editor.csproj` and `Prowl.Cli/Prowl.Cli.csproj`). Newer Prowl main does not compile the scripts yet and migrates the project format.
3. Open this repo folder in the Prowl editor (`Prowl.Editor --project <repo>`). Press Play on `Scenes/MainMenu.scene`.
4. Hills and Town scenes are git-ignored (large): with the editor open run `scripts/bake-map.sh hills` and `scripts/bake-map.sh town`.

Linux player build: `scripts/package-linux.sh [out-dir]` (editor open).

## Headless self-test

```bash
scripts/selftest.sh                 # builds scripts/selftest against ~/Projects/Prowl (PROWL_DIR=... to override)
scripts/selftest.sh --soak 300 6    # soak/fuzz
```

Runs ~25 seconds without a window and prints PASS/FAIL; exits 0 when everything passes.

## Custom maps

Build in Blender and mark spawns, jail, ATMs, doors, NPC paths and water with named empties. See **[MAPS.md](MAPS.md)**.

## Adding models and sounds

Loose game files live in `GameData/` (copied to `assets/` next to a packaged build). Drop `GameData/models/<id>.glb` (or gltf/fbx/obj), `GameData/sounds/<id>.wav`, and optional `GameData/viewmodels/<weapon>.clips.json`. Map alternate filenames in `GameData/manifest.json`. Missing files fall back to placeholders.

### View models

Bone hierarchy (camera space, +Z forward, +Y up, metres): `vm_root` → `arm_r`/`hand_r`/`weapon`/`muzzle`, plus `arm_l`/`hand_l`, optional `sight`/`mag`/`slide`/`pump`/`charge`/`rocket`/`pin`. Clips: `idle`, `draw`, `holster`, `fire`, `reload`, `inspect`, plus variants. See the old in-repo notes in git history for a full clip JSON example if needed; the game plays `fire` on shot, `reload` on R, `inspect` on F, and `draw`/`holster` on switch.

## Code layout

| File | Purpose |
| --- | --- |
| `scripts/selftest/Program.cs` | Headless self-test entry (`--headless`, `--soak`) |
| `Assets/Scripts/Sandbox/SandboxHost.cs` | Editor/player entry component on `/Sandbox` |
| `Assets/Scripts/Sandbox/SandboxGame.cs` | Loop, input, menus, shot states |
| `Assets/Scripts/Sandbox/MapBuilder.cs`, `Assets/Scripts/Sandbox/Town.cs` | Maps (Flatgrass, Hills, RP Town, custom) |
| `Assets/Scripts/Sandbox/ModelProps.cs`, `Assets/Scripts/Sandbox/IconRenderer.cs` | Model props and spawn-menu thumbs |
| `Assets/Scripts/Sandbox/ToolsMore.cs`, `Assets/Scripts/Sandbox/Dupes.cs`, `Assets/Scripts/Sandbox/Vehicles.cs` | Tools, dupes, vehicles |
| `Assets/Scripts/Sandbox/Worlds.cs`, `Assets/Scripts/Sandbox/Session.cs`, `Assets/Scripts/Sandbox/WorldsMenu.cs` | Sessions, autosave, UI screens |
| `Assets/Scripts/Sandbox/Roleplay.cs` | Jobs, economy, printers, shipments |
| `Assets/Scripts/Sandbox/Player.cs`, `Assets/Scripts/Sandbox/Props.cs`, `Assets/Scripts/Sandbox/PhysGun.cs` | Movement, props, physgun |
| `Assets/Scripts/Sandbox/SelfTest.cs` | Headless checks |

## Requirements

- Vulkan-capable GPU/driver (Prowl 1.0 renders with Vulkan).
