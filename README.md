# Sandbox

A Garry's Mod-style physics sandbox with a DarkRP-style Roleplay mode, built in C# on the [Prowl](https://github.com/ProwlEngine/Prowl) engine (Jitter Physics 2, Paper UI).

## Features

- **Physgun**, **tool gun**, **spawn gun**, and **hands** pickup
- **Ragdolls** and wandering **NPCs** (citizens, police, hostiles)
- **Vehicles**: buggy, jeep, airboat
- **Weapons** with animated first-person view models (aim, reload, inspect)
- **Sandbox** and **Roleplay** modes — jobs, money, F4 city menu, buyable/lockable doors, laws board
- **Sessions** with thumbnails, autosave, and modifiers
- **Custom map** import (glTF/FBX/OBJ) and remappable **keybinds**

## Screenshots (v1.0)

| | |
|---|---|
| ![Main menu (v1.0)](docs/screenshots/01-main.jpg) | ![Sessions (v1.0)](docs/screenshots/02-sessions.jpg) |
| ![Spawn menu (v1.0)](docs/screenshots/03-spawn.jpg) | ![Physgun (v1.0)](docs/screenshots/04-physgun.jpg) |
| ![SMG aiming (v1.0)](docs/screenshots/05-aim.jpg) | ![Driving (v1.0)](docs/screenshots/06-drive.jpg) |
| ![Roleplay town (v1.0)](docs/screenshots/07-town.jpg) | ![F4 jobs menu (v1.0)](docs/screenshots/08-jobs.jpg) |
| ![RP shop (v1.0)](docs/screenshots/09-shop.jpg) | ![NPCs & ragdolls (v1.0)](docs/screenshots/10-npcs.jpg) |
| ![Town at night (v1.0)](docs/screenshots/11-night.jpg) | ![Settings / keybinds (v1.0)](docs/screenshots/12-keybinds.jpg) |

## Project layout

This repo **is the Prowl editor project** (`SandboxEditor.prowl`): open the repo folder in the Prowl editor.

| Path | What |
|---|---|
| `Assets/` | Scenes, prefabs, baked meshes/materials/textures, game scripts (`Assets/Scripts/Sandbox`) |
| `ProjectSettings/`, `Packages/` | Prowl project settings |
| `GameData/` | Loose runtime files the game reads (fonts, sky shader, manifest, view model clips); copied to `assets/` next to a packaged build |
| `scripts/` | `bake-map.sh` (bake Hills/Town, which are git-ignored), `package-linux.sh`, `selftest.sh` (headless self-test), `setup-fedora.sh` |
| `docs/` | Development notes, custom maps |

Needs the .NET 10 SDK (`global.json`) and a Prowl editor build (pinned to Prowl `2510d8fc`). After cloning, bake the large maps once with the editor open: `scripts/bake-map.sh hills && scripts/bake-map.sh town`.

More in **[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)**. Custom map empties: **[docs/MAPS.md](docs/MAPS.md)**.
