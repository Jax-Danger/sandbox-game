# Sandbox

A small physics sandbox on the [Prowl](https://github.com/ProwlEngine/Prowl) runtime (v1.0-preview, MIT). This is the first slice: a window with a ground plane, a directional light, a fly camera, and a pile of dynamic cubes plus a sphere that fall and collide. Physics is Jitter Physics 2, which ships inside Prowl.

`Prowl.Runtime` is not published on NuGet. This repo vendors it as a git submodule at `external/Prowl`, pinned to the `v1.0-preview` tag (`50d287009738cd3ea7b6350221ae58f639a4aa39`).

## Run on Windows

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Clone the repo and its submodule:

   ```bat
   git clone --recurse-submodules https://github.com/Jax-Danger/sandbox-game.git
   cd sandbox-game
   ```

   If you already cloned without submodules:

   ```bat
   git submodule update --init --recursive
   ```

3. From the repo root:

   ```bat
   dotnet run
   ```

The first build restores Prowl's NuGet dependencies (Jitter2, Silk.NET, Magick.NET, Prowl.Clay, and others), so it needs a network connection. Later runs do not.

## What you should see

A 1280×720 window titled **Sandbox**:

- A large ground slab.
- A directional light with soft shadows.
- Four colored cubes and a purple sphere, spawned in a loose stack. They fall, hit the ground, and knock into each other.
- A camera aimed at the pile.

Controls:

| Input | Action |
| --- | --- |
| W A S D | Move |
| Q / E | Down / up |
| Hold right mouse | Look |
| Shift | Move faster |

Close the window to quit.

## Headless smoke test

`dotnet run -- --headless` runs the same scene for about 2.5 seconds without opening a window, then prints each body's height. The process exits 0 when the bodies have fallen onto the ground. This does not exercise rendering.

## Requirements that can fail the window

- A GPU and driver that support OpenGL 4.1 (Prowl's window requests a 4.1 core context).
- The native libraries under `external/Prowl/Libraries` (copied next to the build automatically). Do not delete the submodule.
