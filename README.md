# Sandbox

A small Garry's Mod-style physics sandbox in C# on the [Prowl](https://github.com/ProwlEngine/Prowl) runtime (v1.0-preview, MIT). Physics is Jitter Physics 2, which ships inside Prowl. UI uses Prowl's immediate-mode UI (Paper).

`Prowl.Runtime` is not on NuGet, so this repo vendors it as a git submodule at `external/Prowl`, pinned to the `v1.0-preview` tag (`50d287009738cd3ea7b6350221ae58f639a4aa39`).

| | |
|---|---|
| ![Console](docs/console.png) | ![First-time hint](docs/hints.png) |
| ![Rain](docs/weather-rain.png) | ![Fog](docs/weather-fog.png) |
| ![Kill feed and damage numbers](docs/combat.png) | ![RP shop: cars and lottery](docs/rp-shop.png) |
| ![View models](docs/vm-r2-after.png) | ![Aiming down sights](docs/vm-r2-aim.png) |
| ![RP town](docs/r7-townair.png) | ![Laws board](docs/r7-town.png) |
| ![Buy a door](docs/r7-towndoor.png) | ![Jail cell](docs/r7-jail.png) |
| ![Vehicles](docs/r7-garage.png) | ![Model props](docs/r6-models.png) |
| ![Dupes](docs/r7-dupes.png) | ![User spawnlist](docs/r7-myspawn.png) |
| ![Tools](docs/r7-tools-scroll.png) | ![ATM bank](docs/r7-bank.png) |
| ![Main menu](docs/r5-main.png) | ![Sessions](docs/r5-worlds.png) |
| ![Create session](docs/r5-create.png) | ![Loading](docs/r5-loading.png) |
| ![Roleplay HUD](docs/r5-rp.png) | ![F4 jobs menu](docs/r5-jobs.png) |
| ![Rolling Hills](docs/r5-hills.png) | ![Dusk](docs/r5-dusk.png) |
| ![Utilities](docs/r5-utilities.png) | ![Video settings](docs/r5-video.png) |
| ![Physgun](docs/v4-physgun.png) | ![Night + flashlight](docs/r5-night.png) |
| ![SMG](docs/v4-smg.png) | ![Buggy](docs/v4-drive.png) |
| ![Spawn menu](docs/v4-props.png) | ![Tools](docs/v4-tools.png) |
| ![Entities](docs/v4-entities.png) | ![Context menu](docs/v3-context.png) |
| ![Settings](docs/v3-settings.png) | ![Pause](docs/v3-pause.png) |

## Camera

Spawn **Entities → Utility → Camera** and aim it with the physgun. The newest camera streams to a picture-in-picture monitor in the top right, rendered to a RenderTexture every other frame. Cameras are saved with the map.

## Download & run

Grab the zip for your platform from [Releases](https://github.com/Jax-Danger/sandbox-game/releases). The builds are self-contained, so no .NET install is needed.

| Platform | Zip | Run |
|---|---|---|
| Windows 10/11 | `Sandbox-<ver>-win-x64.zip` | Unzip and run `Sandbox-win-x64\Sandbox.exe`. If SmartScreen warns, choose *More info → Run anyway*. |
| Linux x64 | `Sandbox-<ver>-linux-x64.zip` | `unzip …; chmod +x Sandbox-linux-x64/Sandbox; ./Sandbox-linux-x64/Sandbox`. Needs OpenGL 4.1+ drivers. |
| macOS Apple Silicon | `Sandbox-<ver>-osx-arm64.zip` | Unzip and open `Sandbox.app`. |
| macOS Intel | `Sandbox-<ver>-osx-x64.zip` | Unzip and open `Sandbox.app`. |

The macOS app is unsigned, so Gatekeeper blocks the first launch. Right-click → **Open**, or run `xattr -dr com.apple.quarantine Sandbox.app`. Prowl requests an OpenGL **4.1 core** forward-compatible context, which is the highest version macOS supports, so it works there.

### Building releases yourself

```bash
./build-all.sh 0.1.0                # all four: win-x64, linux-x64, osx-x64, osx-arm64 → dist/
./build-all.sh 0.1.0 linux-x64      # just one
```
```powershell
./build-all.ps1 -Version 0.1.0
```
Build macOS on a Mac when you can, because `build-all.sh` ad-hoc codesigns the `.app`, which Apple Silicon requires. CI (`.github/workflows/build.yml`) builds all four on every push, runs the headless self-test and an xvfb smoke run on Linux, and uploads them as artifacts. Pushing a `v*` tag publishes a GitHub Release with the zips.

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

## Sessions (Singleplayer)

**Main menu → Singleplayer** opens the Sessions list, which works like Minecraft's world list. Each row shows a thumbnail, name, when you last played it, the mode, the map, the prop count and how long you've played. The buttons are **Play Selected** (or double-click a row), **Create New**, **Edit** (rename), **Duplicate**, **Delete** (asks you to confirm) and **Open Folder**.

**Create New Session** asks for:
- a name
- a mode: **Sandbox** or **Roleplay**
- a map: **Flatgrass**, **Rolling Hills** (terrain plus a pond), or **Custom map**. A custom map is a `.glb/.gltf/.fbx/.obj/.vrm` file you pick with *Browse…* (uses zenity/kdialog on Linux) or paste a path to. The file is copied into the session folder and gets static mesh colliders.
- modifiers: gravity, prop limit, fall damage, day/night cycle and NPCs. Roleplay sessions also have starting money and paid prop spawning.

Each session is a folder under `%AppData%/ProwlSandbox/saves/<name>/` (on Linux, `~/.config/ProwlSandbox/saves`):

| File | Contents |
| --- | --- |
| `session.json` | name, created, last played, mode, map, modifiers, seed, format and game version |
| `world.json` | props with exact transforms, velocities and frozen state; colours and materials; welds, ropes, ball-sockets, thrusters, wheels; lights, buttons, entities; ragdolls and NPC teams; buggies; roleplay money printers and shipments |
| `player.json` | position, look, noclip, weapon, health and armor; roleplay money, job and laws |
| `icon.png` | the thumbnail, captured from your view |

The game autosaves every 5 seconds on a background thread. Each write goes to a temp file and is renamed into place, and the previous file is kept as `.bak`; if a file is corrupt, loading falls back to the `.bak`. The game also saves on **Save & exit to Sessions**, on Quit and when you close the window.

**Loading** a session goes through a loading screen with real progress. It runs these stages: *Loading assets*, *Loading models*, *Loading shaders*, *Building physics*, *Spawning entities (n/N)*, *Restoring player*. File reading happens on a background task. Main-thread work runs in batches of 6 ms per frame, so the window keeps responding.

## Roleplay mode (DarkRP-style)

The HUD adds a wallet/job chip, a salary countdown, a WANTED or ARRESTED badge when that applies, and the laws board. **F4** opens the City menu:
- **Jobs:** Citizen, Police Officer (baton arrests, pistol), Mayor (writes the laws), Thief (lockpick), Medic (medkit, cheaper armour), Gun Dealer (weapon shipments), Gangster (pistol, +25 % printer cash) and Hobo (free props). Each job has its own colour, salary, description and player limit.
- **Shop:** money printer, armour, medkit, ammo and shipments. Shipments can only be bought as Gun Dealer.
- **Laws:** edited by the Mayor.
- **Money:** drop cash; at an ATM, deposit and withdraw from your bank account (bank money is safe when you die).

**RP Town map** (the default for Roleplay sessions): four avenues with sidewalks and crosswalks around a spawn plaza (fountain, benches, the laws board and an ATM), a police station with a jail cell, a general store, three houses, an apartment and a garage.
- **Doors:** E opens/closes. Doors marked *for sale* can be bought with **F2** (F2 again sells for half price); **L** locks/unlocks doors you own. Police can lock the station doors. Thieves can pick any locked door with the lockpick (police doors always make you wanted).
- **Arrests:** police NPCs chase you while you're WANTED and arrest you on contact: a $200 fine and 30 s in the locked jail cell. Leaving early counts as a jailbreak (wanted again).
- **Wanted:** assaulting citizens/police nearby, lockpicking, jailbreaks, and carrying a gun near police while the laws ban weapons (they warn you first).
- **Laws board:** the mayor's laws show on the board in the plaza as a world panel.
- **NPCs:** citizens walk the sidewalks between waypoints, stop and look at you up close, and back away from drawn guns or wanted players; police patrol faster.

Press **E** to pick up money, collect from a printer or open a shipment. Printers print $25 every 20 s. Dying drops 10 % of your wallet. Town NPC citizens and police wander the map, and police chase you while you're wanted. With paid spawning on, props cost $20 each.

### Custom maps for Roleplay

Build the map in Blender and mark spawns, the jail, ATMs, doors, NPC paths and water with named empties
(`spawn_*`, `jail_cell`, `jail_release`, `atm`, `laws_board`, `door_*`, `npc_path_*`, `water_*`). For the full naming
reference, see **[docs/MAPS.md](docs/MAPS.md)**.

## What you see

A 1280×720 window titled **Sandbox**. You stand on a large 120 × 120 m floor with walls around the edge, three ramps, a raised platform, a free-standing wall and a staircase. A few starter props (cubes, a sphere, a barrel, a plank) sit in front of you. Untextured materials get a white albedo, so props show their own colour.

The HUD is deliberately minimal (s&box-style): a small dot crosshair that gets a ring when you aim at a prop, a prop-count chip bottom-left, a weapon/ammo chip bottom-right (a speedometer while driving), and small notification pills. The weapon switcher strip shows for about two seconds after you switch, then fades out.

## New in v0.6.0

- **View models**: gloved hands with posed fingers, sleeved forearms, detailed guns; spring sway, footstep-synced bob, landing dip, sprint/crouch poses, aim-down-sights (right mouse), view-model FOV, per-weapon reloads (shotgun shell by shell, interruptible), inspects (F), melee swing variants, grenade pin pull. See "View models" below.
- **Prop protection**: every prop has an owner. In Roleplay (or always, with `pp always`) you can only physgun, tool or remove your own props, or those of players who added you as a buddy. Map/town entities belong to `world`. Console: `buddy add|remove|list <name>`, `pp off|rp|always`, `pp_owner [name]`. Owners are saved with the map.
- **NPC health and combat**: NPCs take bullet (headshots ×2), crowbar and explosion damage; kill feed (top right) and optional damage numbers (Settings → Game).
- **RP lottery**: the Mayor starts a 90 s lottery from the shop; anyone buys $50 tickets, townsfolk join in, the Mayor keeps 10%.
- **Car Dealer** job and buyable **Buggy** / **Jeep** in the shop (dealers pay 60%).
- **Getting started**: a five-step tutorial chip for new players in Sandbox sessions (spawn, grab, freeze, weld, drive). Steps complete from what you do; `tutorial reset|skip` in the console. ![](docs/tutorial.png)
- **Save fix**: a rope and an elastic band on the same two props no longer turn into an extra rope on every save/load (found by the soak, seed 7).
- **Kill plane**: anything that falls through the world is removed (whole ragdoll), fixing the soak's "Limb fell out of the world".

## New in v0.5.1

- **Door keys** (Roleplay): press **K** on your door to give the nearest NPC a key, **Shift+K** to take keys back. Full history in [CHANGELOG.md](CHANGELOG.md).

## New in v0.5.0

- **Console** (`` ` ``): `help`, `noclip`, `give <weapon>`, `time <0-24>`, `gravity <m/s²>`, `timescale`, `cleanup`, `spawn <prop> [n]`, `tp x y z`, `pos`, `heal`, `kill`, `weather <preset>`, `achievements`, `hints reset`, `photo`, `screenshot`. Anything that isn't a command is chat. ↑/↓ for history, mouse wheel or PgUp/PgDn to scroll, Esc to close.
- **Photo mode** (F1) hides the HUD. **F12** saves a PNG of the world (no HUD) to `<AppData>/ProwlSandbox/screenshots/`.
- **Weather**: Clear, Overcast, Fog, Rain, Storm (with lightning). Pick it in Spawn menu → Utilities or with the console. It blends smoothly and is saved per session.
- **Achievements**: 14, two of them Roleplay-only. They unlock with a toast and are saved to `achievements.json` next to `settings.json`.
- **Recent spawnlist**: the last 12 things you spawned, at the top of the spawn menu's category list.
- **RP hunger**: hunger drains over about 8 minutes, then you take starvation damage. Buy a Burger or Soda from the F4 shop. A thin bar in the RP chip shows hunger, and it's saved with the session.
- **First-time hints**: one small hint at a time, each shown once. Turn them off in Settings → Game, or replay them with `hints reset`.
- **Stability**: a soak/fuzz harness (`--headless --soak <secs> <seed>`) drove fixes for NaN physics crashes, a ragdoll/no-collide memory leak, save/load mismatches, autosave races and unicode or reserved session names.

## Controls

| Input | Action |
| --- | --- |
| Mouse | Look (cursor locked while playing) |
| W A S D / Shift / Space | Walk / run / jump |
| ` | Console / chat |
| F1 / F12 | Photo mode (hide HUD) / save screenshot |
| Ctrl | Crouch (fly down in noclip) |
| F | Flashlight (inspects the weapon when you hold the tool gun) |
| Hold B | Zoom |
| F4 | Roleplay City menu (jobs, shop, laws, money) |
| V | Toggle noclip |
| Hold Q or Tab | Spawn menu: Spawnlists, Entities, Vehicles, Weapons, Tools, Dupes, Utilities |
| Hold C | Context menu: click a prop to edit freeze, colour, material, mass or delete it. Also has the current tool's settings |
| 1 … 5, mouse wheel | Weapon slots (press a number again to cycle within the slot): **1** Hands, Crowbar · **2** Physgun, Tool gun, Spawn gun · **3** Pistol, SMG · **4** Shotgun · **5** Rocket launcher, Grenades |
| Left / right mouse | Primary / secondary fire |
| R | Reload (in a vehicle: flip the buggy upright) |
| E | Pick up / drop a light prop (left click throws it) · press a button · enter / exit a vehicle |
| T G Y H U J, numpad 0–9 | Keys for thrusters, wheels, hoverballs, emitters and buttons |
| F2 / L | RP town: buy or sell the door you look at / lock or unlock it |
| Z | Undo |
| F3 | FPS counter |
| Esc | Pause menu: Resume, Save now, Clear map, Settings, Save & exit to Sessions |

**Hands (1):** E picks up anything up to 15 kg and carries it in front of you. Left click throws it; E again drops it. With nothing held, left click gives a light shove.

**Physgun:** hold left mouse on a prop (or a single ragdoll limb) to grab it. A pulsing beam connects you to it. The wheel pushes or pulls. Right-click freezes it. Let go to throw.

**Tool gun:** pick a tool in the spawn menu's Tools tab. Its settings appear there and in the C menu.
- Constraints: **Weld**, **Rope** (two points, slack setting), **Ball socket** (pivot), **No-collide**, **Elastic** (damped spring), **Slider** (prismatic, ±4 m), **Hinge** (around the surface normal).
- Construction: **Balloon**, **Remover**, **Thruster** (hold its key), **Wheel** (motorised, forward/back keys), **Duplicator** (right click copies the whole contraption, left click pastes it facing you; one undo removes it), **Hoverball** (holds height; its key raises, the next key lowers), **Emitter** (sparks while its key is held), **Stacker** (copies along an axis, optionally welded).
- Render: **Colour** (right click picks a colour up from a prop), **Material** (plastic, wood, metal, rubber, ice, concrete; changes friction/bounce), **Light**.

**Spawn gun:** left click spawns whatever you last picked in the spawn menu, where you aim. Right click cycles your recent spawns.

**Weapons:** hitscan guns push props and ragdolls, leave decals and sparks, and set off explosive barrels. Rockets and grenades explode with a physics blast. Each gun has a magazine plus reserve ammo; R reloads.

**Entities tab:** Ragdoll; NPC Dummy (stands until you hit it, then goes limp); Explosive Barrel (shoot it, blast it, or slam it); Bouncy Ball; Light; Button (press E or shoot it to toggle its key, which drives thrusters and wheels bound to that key).

**Spawnlists:** 33 model props with rendered thumbnails (furniture, construction, signs, wheels, vehicle parts) next to the basic shapes. Hover any tile and press **+** to add it to your own spawnlist (*My Spawnlists*, saved as `saves/spawnlists.json`). Chairs, stools, benches and the seat are **seats**: E sits (weld one into a contraption and ride it), E or Space stands.

**Dupes tab:** the Duplicator's clipboard; name and save it as a file (`saves/dupes/*.dupe.json`), then load any saved dupe back onto the Duplicator.

**Vehicles tab:** Buggy, Jeep (four-wheel drive) and Airboat (fan-driven hull on skids). Look at it and press E to drive: W/S throttle and brake, A/D steer, Space handbrake, mouse orbits the chase camera, R flips it upright, E gets out.

**Health:** health and armor show bottom-left. Falls faster than about 13 m/s hurt (you can turn this off per session or in Utilities). When you die you respawn after 3 s.

**Utilities tab:** sky presets (Morning, Noon, Golden hour, Dusk, Night), a day/night cycle toggle, gravity and time-scale sliders, noclip, fall damage, heal and save.

**Settings → Video:**
- graphics quality; Medium is the default (FXAA, bloom, tonemapping)
- window mode: windowed, borderless or fullscreen
- resolution
- frame-rate cap
- VSync, which applies after a restart

**Logs and crashes:** the game writes a log to `<app data>/ProwlSandbox/logs/latest.log` and keeps the previous run's log as `previous.log`. An unhandled error writes `crash-<time>.txt` and shows a dialog. If the window can't get an OpenGL 4.1 context, the dialog explains that and says what to update.

**Legacy quicksave:** Utilities → Save map (shown when no session is loaded) writes `quicksave.json`. Everything is saved: props (colour, material, mass, frozen), welds, balloons, ropes, ball-sockets, thrusters, wheels, lights, buttons (with their keys), explosive barrels, bouncy balls, NPC dummies, ragdolls, cameras and buggies. Procedural textures are on by default; set `SANDBOX_PROCTEX=0` to use flat colours instead.

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
└── arm_l          left forearm (free left hand: hands, toolgun, grenade, crowbar)
    └── hand_l     left hand; on two-handed guns it is parented to weapon (or pump) instead
optional under weapon: sight (ADS lines this up with the screen centre), mag, slide, pump, charge, rocket, pin
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

Extra clips the game plays when they exist: `fire2`, `fire3`... (alternating variants, e.g. the crowbar's backhand), `reload_start` / `reload_shell` / `reload_end` (shell-by-shell; set `"hold": true` so a clip keeps its last pose until the next one), and `grab` / `release` on the physgun.

The `ease` value is how the pose gets *to* that key: `Linear`, `In`, `Out`, `InOut` or `Back` (overshoot). A bone missing from a key is at rest for that key. One-shot clips crossfade in and out over 0.08 s, on top of the looping `idle`. Procedural bob, sway and recoil are added on top of that. The game plays `fire` on each shot, `reload` on R, `inspect` on F, and `draw`/`holster` when you switch weapons.

### View models: rigged glb/fbx models and what Prowl supports

Prowl's importer (glTF/GLB, FBX, OBJ) brings in skinned meshes (`SkinnedMeshRenderer`, bones are ordinary transforms), blend shapes and baked animation clips (`AnimationClip`, per-bone position/rotation/scale curves). It has a simple one-clip `Animation` player, but no animation blending, state machine or IK. The sandbox works within that:

- Any node of your model named like a rig bone above is driven by our pose and clip system, and so is any node named in a `clips.json` key (e.g. `finger_index_02`). The skinned mesh deforms with those bones.
- Embedded clips play by name: a model clip called `reload` (or `Armature|reload` from Blender) replaces the built-in `reload` and drives the skeleton directly; `idle` loops when nothing else plays. The procedural layers (sway, bob, recoil, ADS, sprint) still move `vm_root` on top, so imported clips get the same feel.
- Embedded clips don't blend with each other (Prowl samples one pose at a time); our own JSON clips do.

### View models: feel

All procedural layers are sub-stepped springs, so they behave the same at any frame rate: weight-based mouse sway with lag (heavier guns swing slower and further), strafe tilt, walk bob locked to the footstep sounds, jump lift and a landing dip, a lowered/angled sprint pose, a crouch tuck, aim-down-sights on the right mouse button (pistol, SMG, shotgun, RPG; slight zoom, crosshair hides), recoil with a little random yaw/roll, and an idle fidget after standing still. Interrupted clips crossfade instead of snapping. **View model FOV** (Settings → Video) draws the arms at their own FOV, independent of the world FOV.

The built-in view models are generated from swept and lathed meshes with smooth normals: every finger is one smooth tube that wraps the grip it holds, palms follow the grip surface, thumbs close from the other side and trigger fingers run along the frame. Forearms are separate bones that point at a fixed elbow below the screen, so a hand that travels during a reload never drags the sleeve into the camera. The RPG aims with a shoulder raise instead of centring its side optic, so the tube stays out of the middle of the view.

| Before (v0.6 primitives) | After (swept/lathed rebuild) | Aiming |
|---|---|---|
| ![](docs/vm-r2-before.png) | ![](docs/vm-r2-after.png) | ![](docs/vm-r2-aim.png) |

Frame sequences (10 fps) of the pistol and shotgun reloads: [pistol](docs/vm-seq-pistol-reload.png), [shotgun](docs/vm-seq-shotgun-reload.png).

## Headless self-test

```bat
dotnet run -- --headless
```

This runs the game for about 25 seconds without a window. It scripts spawning, physgun, tools (including elastic pull and hoverball hold), vehicles (buggy, jeep, airboat), weapons, entities, dupes, spawnlists, sessions save/load, the roleplay economy and the RP town (doors, ATM, arrest/jail/jailbreak), prints PASS or FAIL for each, and exits 0 when everything passes. It doesn't test rendering or real mouse/keyboard input.

Soak/fuzz test: `dotnet run -- --headless --soak 300 6` runs random actions for 300 s with seed 6. It checks for exceptions, NaNs, physics explosions, save/load round-trip mismatches and memory growth, prints a report, and exits non-zero if it finds problems.

## Code layout

| File | Purpose |
| --- | --- |
| `src/Program.cs` | Entry point (`--headless` runs the self-test) |
| `src/SandboxGame.cs` | Game loop, input routing, tool selection, menus |
| `src/MapBuilder.cs`, `src/Town.cs` | Maps (Flatgrass, Rolling Hills, RP Town, custom model import), doors/ATM/laws board |
| `src/ModelProps.cs`, `src/IconRenderer.cs` | Placeholder model props (behind the asset manifest) and spawn-menu thumbnails |
| `src/ToolsMore.cs`, `src/Dupes.cs`, `src/Vehicles.cs` | Extra tools, contraption dupes + user spawnlists, buggy/jeep/airboat |
| `src/Worlds.cs`, `src/Session.cs`, `src/WorldsMenu.cs` | Session folders (same `saves/` layout and `world.json` file as before, so old saves load), atomic saves, async loader, Sessions/Create/Loading/F4 screens |
| `src/Roleplay.cs` | Jobs, economy, printers, shipments, baton/lockpick/medkit |
| `src/Health.cs`, `src/DayNight.cs` | Health/armor/fall damage/respawn; sky presets and day/night |
| `src/Meshes.cs` | Procedural bevelled boxes, rounded cylinders, tyres, capsules (placeholders the asset manifest can replace) |
| `src/Log.cs` | Log file, crash reports, native error dialogs |
| `src/Player.cs` | First-person CharacterController with gravity, jumping and noclip |
| `src/Props.cs` | Prop definitions, spawning, removal, undo stack |
| `src/PhysGun.cs` | Grab, hold, push/pull, throw, freeze |
| `src/ToolGun.cs` | Weld, remover and balloon (`Balloon` component) |
| `src/SpawnMenu.cs`, `src/PauseMenu.cs`, `src/Hud.cs`, `src/Ui.cs` | Paper UI |
| `src/SelfTest.cs` | Headless scripted checks |

## Requirements that can stop the window opening

- A GPU and driver with OpenGL 4.1 (Prowl requests a 4.1 core context).
- The native libraries under `external/Prowl/Libraries`, which are copied next to the build automatically. Don't delete the submodule.
