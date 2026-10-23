# Making a map in Blender (map markers)

You can build any map you like in Blender. The game imports it as a **Custom map**. Its gameplay positions (where
players spawn, the jail, ATMs, doors and so on) come from **markers**: objects with special names that you place
in the scene. Every other mesh becomes solid static world geometry.

## Quick start

1. Model your map in Blender. Use 1 Blender unit = 1 metre, with the floor at roughly Z = 0.
2. Add markers: **Add → Empty → Plain Axes** (or Cube), then name them using the table below.
3. **File → Export → glTF 2.0** (`.glb` or `.gltf`). Keep the defaults, which include **+Y Up**. Turn on *Include → Custom
   Properties* if you want, but the game doesn't need them. FBX and OBJ also import, but OBJ has no empties.
4. In the game: **Worlds → Create New World → Map: Custom**, then pick your file. The import line in the log (`logs/`)
   lists what it found, for example `markers: 4 spawns, jail yes, 1 ATMs, board yes, 9 doors, 12 path points, 1 water`.

Markers are never drawn and never collide, even if you used a cube mesh for one. Names ignore case. Blender's
duplicate suffix (`.001`, `_001`) is ignored too, so you can copy `spawn` five times without renaming anything.

## Axes (Blender → game)

| Blender | In game |
|---|---|
| X | X (east) |
| Y | forward (+Z in game) |
| Z | up |

A marker's **front** is its local **+Y** axis, the green arrow in Blender. Rotating an empty around Z turns the ATM,
the laws board or a door in game the same way. Only the yaw (rotation around Z) is used.

## Marker reference

| Name | What it does | Position | Scale |
|---|---|---|---|
| `spawn`, `spawn_<anything>` | Player spawn point. Add several; respawns pick one at random. The first one is used for a new world. | Feet position on the floor | – |
| `jail_cell` | The jail cell. Arrested players are teleported here and held for the sentence. Leaving early is a jailbreak, which makes you wanted. | Centre of the cell floor | X / Y = **half** the cell's width / depth (default 2.4 m) |
| `jail_release` | Where players appear when their sentence ends. Optional: if it's missing, they appear 3 m in front of the cell. | Floor point | – |
| `atm`, `atm_<anything>` | Places an ATM. Press E on it to open the bank. | Floor point; the screen faces +Y | – |
| `laws_board` | Places the mayor's laws board, which shows the current laws. | Floor point at the board's centre; the face points +Y | – |
| `door_<id>` | A buyable door (F2 to buy, L to lock, E to open; thieves can pick the lock). Costs $250 by default. | **Hinge** corner, on the floor | X = door width (default 1.3 m), Z = height (default 2.45 m) |
| `door_<id>_p<price>` | A buyable door with a price, e.g. `door_house1_p400`. | as above | as above |
| `door_police_<id>` | Police property: can't be bought; police can lock it. | as above | as above |
| `door_city_<id>` | City property: public and can't be bought. | as above | as above |
| `door_cell` | The jail cell door. Owned by the police, and locked whenever someone is jailed. | as above | as above |
| `npc_path_<n>` | A point on the route NPC citizens walk, e.g. sidewalks. Place 10–30 of them around your streets. | Floor point | – |
| `water`, `water_<anything>` | A water volume. Props float according to their material (wood floats high, metal sinks) and the airboat rides on it. Model the visible water surface yourself. | **Centre** of the water box | X / Y = half width / depth, Z = half height. **Surface = top of the box.** |

Doors swing from the hinge along the marker's local **+X** axis. Put the empty at the bottom corner of the doorway
on the hinge side, with +X pointing across the opening. The door title shown in game comes from the id:
`door_corner_shop` becomes "Corner Shop".

A scale of exactly 1 means "use the default". For a door that really is 1 m wide, use a scale of 1.01.

## Roleplay

A custom map that has any RP marker (`jail_cell`, `atm`, `laws_board`, `door_*` or `npc_path_*`) switches on the town
features in Roleplay worlds: doors you can buy and lock, the ATM, the laws board, NPC walking routes, and the
jail and arrest flow. A map with no `jail_cell` still works in Roleplay; arrests then use a short timed sentence
at the spawn point. Spawns and water work in every mode. Buying doors, the bank and the jail are Roleplay features.

## Checklist

- [ ] At least one `spawn`.
- [ ] RP maps: `jail_cell` with walls you modelled around it, plus a `door_cell` in its doorway.
- [ ] The floor is solid under every spawn and marker (all non-marker meshes get colliders).
- [ ] Applied scale on your *meshes* (Ctrl+A → Scale). On markers, scale is the size, so don't apply it there.
- [ ] Exported with +Y Up.

This is checked by the headless self-test (`MarkersTest` in `Assets/Scripts/Sandbox/SelfTest.cs`), which writes a small glTF with
every marker type, imports it and checks each one.
