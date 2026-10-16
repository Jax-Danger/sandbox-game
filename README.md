# Sandbox

A small Garry's Mod-style physics sandbox in C# on the [Prowl](https://github.com/ProwlEngine/Prowl) runtime (v1.0-preview, MIT). Physics is Jitter Physics 2, which ships inside Prowl. UI uses Prowl's immediate-mode UI (Paper).

`Prowl.Runtime` is not on NuGet, so this repo vendors it as a git submodule at `external/Prowl`, pinned to the `v1.0-preview` tag (`50d287009738cd3ea7b6350221ae58f639a4aa39`).

![HUD](docs/sandbox-hud.png)
![Spawn menu](docs/sandbox-spawn-menu.png)

## Run on Windows

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Clone the repo **with its submodule**:

   ```bat
   git clone --recurse-submodules https://github.com/Jax-Danger/sandbox-game.git
   cd sandbox-game
   ```

   Already cloned without it? Run `git submodule update --init --recursive`.

3. From the repo root:

   ```bat
   dotnet run
   ```

The first build downloads Prowl's NuGet dependencies, so it needs internet and takes a minute or two.

## What you see

A 1280×720 window titled **Sandbox**. You stand on a large 120 × 120 m floor with walls around the edge, three ramps, a raised platform, a free-standing wall and a staircase. A few starter props (cubes, a sphere, a barrel, a plank) sit in front of you. The props use Prowl's default grid texture.

The HUD shows a crosshair, the current tool and mode (top left), the prop count (top right), a control hint line (bottom) and short messages such as "Welded …" or "Undone …".

## Controls

| Input | Action |
| --- | --- |
| Mouse | Look (the cursor is locked while playing) |
| W A S D | Walk |
| Shift | Run (in noclip: fly faster) |
| Space | Jump (in noclip: fly up) |
| Ctrl | Fly down (noclip only) |
| V | Toggle noclip (fly through everything) |
| Hold Q or Tab | Spawn menu: click Cube, Sphere, Plank, Barrel, Big Crate or Ramp to spawn it where the crosshair points |
| 1 | Physgun |
| 2 | Toolgun: Weld |
| 3 | Toolgun: Remover |
| 4 | Toolgun: Balloon |
| Z | Undo the last spawn, weld or balloon |
| Esc | Pause menu: Resume, Clear map, Quit |

**Physgun (1):** hold left mouse on a prop to pick it up. The mouse wheel pushes it away or pulls it in. Let go to drop or throw it (it keeps its velocity). Right-click while holding to **freeze** it in place (it turns blue). Grabbing a frozen prop unfreezes it.

**Toolgun (2–4):** left click applies the mode:
- **Weld:** click one prop, then a second, and they're locked together.
- **Remover:** click a prop to delete it.
- **Balloon:** click a prop to tie a red balloon to it. Each balloon pulls up with about 1.4× the prop's weight, so it floats away. Freeze it or weld it to something heavy.

## Adding your own models and sounds

Everything visual or audible has a placeholder (primitive meshes, procedurally generated WAVs), so you can swap assets in one at a time without touching code. Assets live in `assets/` and get copied next to the exe when you build.

| What | Drop it here | Id examples |
|---|---|---|
| Prop / entity / vehicle model | `assets/models/<id>.glb` (or `.gltf`, `.fbx`, `.obj`) | `prop_cube`, `prop_barrel`, `vehicle_buggy` |
| View model (first-person arms + weapon) | `assets/models/vm_<weapon>.glb` | `vm_pistol`, `vm_smg`, `vm_shotgun`, `vm_rpg`, `vm_grenade`, `vm_crowbar`, `vm_physgun`, `vm_toolgun`, `vm_spawngun`, `vm_hands` |
| Sound | `assets/sounds/<id>.wav` | `sfx_pistol`, `sfx_impact`, `sfx_spawn`, `sfx_ui_click` |
| View model animation clips | `assets/viewmodels/<weapon>.clips.json` | `smg.clips.json` |

If the file name doesn't match the id, map it in `assets/manifest.json`:

```json
{
  "models":     { "vm_pistol": "models/my_glock.fbx", "prop_barrel": "models/barrel_v2.glb" },
  "sounds":     { "sfx_pistol": "sounds/glock_shot.wav" },
  "modelScale": { "vm_pistol": 0.01 }
}
```

Missing or broken files are logged once and the placeholder is used, so the game never fails because of an asset.

### View models: bone names

Each view model is a small bone hierarchy parented to the camera (+Z forward, +Y up, metres):

```
vm_root            (camera-space root; walk bob, mouse sway and recoil are applied here)
├── arm_r          right forearm
│   └── hand_r     right hand
│       └── weapon the gun itself (fire/reload/inspect move this)
│           └── muzzle  muzzle point (flash and tracers start here)
└── arm_l          left forearm
    └── hand_l     left hand (reload grabs the mag with this)
```

For your own model, name the nodes exactly like this (any extra nodes are fine). When `vm_<weapon>` loads, those nodes become the animated bones and the primitive placeholder meshes are skipped. Bones you leave out are created as empty nodes. If you don't have arms, a model containing only `weapon` and `muzzle` works too.

### View models: animation clips

Clips are keyframed offsets from each bone's rest pose (`pos` in metres, `rot` in Euler degrees). Built-in clips are `idle` (loops), `draw`, `holster`, `fire`, `reload` and `inspect`. To override or add one, write `assets/viewmodels/<weapon>.clips.json`:

```json
{
  "reload": {
    "length": 1.2,
    "loop": false,
    "keys": [
      { "t": 0.0, "ease": "Linear", "bones": {} },
      { "t": 0.4, "ease": "InOut",  "bones": { "weapon": { "pos": [0, -0.03, 0], "rot": [8, 0, -35] },
                                                "hand_l": { "pos": [0, -0.18, -0.06], "rot": [20, 0, 0] } } },
      { "t": 1.2, "ease": "Back",   "bones": {} }
    ]
  }
}
```

The `ease` value is how the pose gets *to* that key: `Linear`, `In`, `Out`, `InOut` or `Back` (overshoot). A bone missing from a key is at rest for that key. One-shot clips crossfade in and out over 0.08 s, on top of the looping `idle`. Procedural bob, sway and recoil are added on top of that. The game plays `fire` on each shot, `reload` on R, `inspect` on F, and `draw`/`holster` when you switch weapons.

## Headless self-test

```bat
dotnet run -- --headless
```

This runs the game for about 6 seconds without a window. It scripts spawning, falling, physgun grab/hold/freeze/unfreeze/release, weld, balloon lift, undo, remover, spawn at the crosshair, clear map, and the player standing on the floor, prints PASS or FAIL for each, and exits 0 when everything passes. It doesn't test rendering or real mouse/keyboard input.

## Code layout

| File | Purpose |
| --- | --- |
| `src/Program.cs` | Entry point (`--headless` runs the self-test) |
| `src/SandboxGame.cs` | Game loop, input routing, tool selection, menus |
| `src/MapBuilder.cs` | Static test map and light |
| `src/Player.cs` | First-person CharacterController with gravity, jumping and noclip |
| `src/Props.cs` | Prop definitions, spawning, removal, undo stack |
| `src/PhysGun.cs` | Grab, hold, push/pull, throw, freeze |
| `src/ToolGun.cs` | Weld, remover and balloon (`Balloon` component) |
| `src/SpawnMenu.cs`, `src/PauseMenu.cs`, `src/Hud.cs`, `src/Ui.cs` | Paper UI |
| `src/SelfTest.cs` | Headless scripted checks |

## Requirements that can stop the window opening

- A GPU and driver with OpenGL 4.1 (Prowl requests a 4.1 core context).
- The native libraries under `external/Prowl/Libraries`, which are copied next to the build automatically. Don't delete the submodule.
