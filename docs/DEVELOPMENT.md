# Development

Build from source, asset workflow, view models, headless tests, and code layout. For players, see the [README](../README.md).

## Building from source

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Clone **with submodules**:

```bash
git clone --recurse-submodules https://github.com/Jax-Danger/sandbox-game.git
cd sandbox-game
```

Already cloned without them? `git submodule update --init --recursive`.

3. From the repo root: `dotnet run`

`Prowl.Runtime` is not on NuGet; it is vendored as a git submodule at `external/Prowl`, pinned to the `v1.0-preview` tag.

### Building release zips

```bash
./build-all.sh 1.0.0                # win-x64, linux-x64, osx-x64, osx-arm64 → dist/
./build-all.sh 1.0.0 linux-x64      # just one
```

```powershell
./build-all.ps1 -Version 1.0.0
```

Build macOS on a Mac when you can (`build-all.sh` ad-hoc codesigns the `.app`). CI (`.github/workflows/build.yml`) builds all four on every push, runs the headless self-test and an xvfb smoke run on Linux, and uploads artifacts. Pushing a `v*` tag publishes a GitHub Release with the zips.

## Headless self-test

```bash
dotnet run -- --headless
```

Runs ~25 seconds without a window: spawn, physgun, tools, vehicles, weapons, entities, dupes, sessions, roleplay/town, prints PASS/FAIL, exits 0 when everything passes.

Soak/fuzz: `dotnet run -- --headless --soak 300 6`

## Screenshot mode

```bash
SANDBOX_RES=1920x1080 dotnet bin/Debug/net10.0/Sandbox.dll --shot <state>
```

Useful states include `main`, `worlds`, `props`, `physgun`, `drive`, `vm_<weapon>[_aim]`, `town`, `jobs`, `shop`, `keybinds`, `gal_npcs`, `townat_x_y_z_yaw_pitch[_time]`, `night`. Helpers under `/workspace/shot.sh` (and similar) grab the window via xvfb.

## Custom maps

Build in Blender and mark spawns, jail, ATMs, doors, NPC paths and water with named empties. See **[MAPS.md](MAPS.md)**.

## Adding models and sounds

Assets live in `assets/` and are copied next to the exe on build. Drop `assets/models/<id>.glb` (or gltf/fbx/obj), `assets/sounds/<id>.wav`, and optional `assets/viewmodels/<weapon>.clips.json`. Map alternate filenames in `assets/manifest.json`. Missing files fall back to placeholders.

### View models

Bone hierarchy (camera space, +Z forward, +Y up, metres): `vm_root` → `arm_r`/`hand_r`/`weapon`/`muzzle`, plus `arm_l`/`hand_l`, optional `sight`/`mag`/`slide`/`pump`/`charge`/`rocket`/`pin`. Clips: `idle`, `draw`, `holster`, `fire`, `reload`, `inspect`, plus variants. See the old in-repo notes in git history for a full clip JSON example if needed; the game plays `fire` on shot, `reload` on R, `inspect` on F, and `draw`/`holster` on switch.

## Code layout

| File | Purpose |
| --- | --- |
| `src/Program.cs` | Entry (`--headless` / `--shot`) |
| `src/SandboxGame.cs` | Loop, input, menus, shot states |
| `src/MapBuilder.cs`, `src/Town.cs` | Maps (Flatgrass, Hills, RP Town, custom) |
| `src/ModelProps.cs`, `src/IconRenderer.cs` | Model props and spawn-menu thumbs |
| `src/ToolsMore.cs`, `src/Dupes.cs`, `src/Vehicles.cs` | Tools, dupes, vehicles |
| `src/Worlds.cs`, `src/Session.cs`, `src/WorldsMenu.cs` | Sessions, autosave, UI screens |
| `src/Roleplay.cs` | Jobs, economy, printers, shipments |
| `src/Player.cs`, `src/Props.cs`, `src/PhysGun.cs` | Movement, props, physgun |
| `src/SelfTest.cs` | Headless checks |

## Requirements

- GPU/driver with OpenGL 4.1 (Prowl requests a 4.1 core context).
- Native libraries under `external/Prowl/Libraries` (copied next to the build). Do not delete the submodule.
