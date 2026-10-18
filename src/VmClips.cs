using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

public static partial class ViewModels
{
    // ───────────────────────────── Clips ─────────────────────────────
    // Offsets from rest. Ease names the curve used to reach a key from the previous one: Out = decelerate into the
    // pose, In = accelerate (strikes), Back = overshoot and settle. Most moves have a small anticipation key first.


    private static void BuildClips(ViewModelDef d, string id, bool supportHand)
    {
        bool has(string b) => d.Bones.Any(x => x.Bone == b);
        var clips = d.Clips;
        // Breathing idle: slow figure-eight on the rig; the support hand micro-adjusts its grip.
        clips["idle"] = new ClipBuilder(4.0f, loop: true)
            .Key(0f, Ease.InOut, B("vm_root"))
            .Key(1.3f, Ease.InOut, B("vm_root", 0.0015f, 0.004f, 0f, -0.7f, 0.2f, 0.3f), B("hand_l", 0f, 0.002f))
            .Key(2.7f, Ease.InOut, B("vm_root", -0.0015f, 0.002f, 0f, -0.3f, -0.2f, -0.3f), B("hand_l", 0f, 0.001f, 0.002f))
            .Build();
        // Draw: up from below with a roll, slight overshoot, settle. Holster: a quick dip away.
        clips["draw"] = new ClipBuilder(0.55f)
            .Key(0f, Ease.Linear, B("vm_root", 0.02f, -0.24f, -0.06f, 38f, 6f, -14f), B("hand_r", 0f, 0f, 0f, 0f, 0f, -20f))
            .Key(0.36f, Ease.Out, B("vm_root", 0f, 0.006f, 0f, -2.5f, 0f, 1f), B("hand_r", 0f, 0f, 0f, 0f, 0f, 3f))
            .Key(0.55f, Ease.InOut, B("vm_root"), B("hand_r"))
            .Build();
        clips["holster"] = new ClipBuilder(0.2f)
            .Key(0f, Ease.Linear, B("vm_root"))
            .Key(0.04f, Ease.Out, B("vm_root", 0f, 0.006f, 0f, -3f))
            .Key(0.2f, Ease.In, B("vm_root", 0.02f, -0.26f, -0.05f, 36f, 4f, 12f))
            .Build();

        switch (id)
        {
            case "crowbar":
                // Forehand: wind up right, fast diagonal cut down-left, follow through, settle with a little overshoot.
                clips["fire"] = new ClipBuilder(0.48f)
                    .Key(0f, Ease.Linear, B("arm_r"))
                    .Key(0.09f, Ease.Out, B("arm_r", 0.04f, 0.07f, -0.05f, -42f, 28f, 18f))
                    .Key(0.18f, Ease.In, B("arm_r", -0.13f, -0.05f, 0.10f, 52f, -38f, -22f))
                    .Key(0.26f, Ease.Out, B("arm_r", -0.17f, -0.08f, 0.06f, 62f, -46f, -26f))
                    .Key(0.48f, Ease.Back, B("arm_r"))
                    .Build();
                // Backhand: wind up across the body, cut right.
                clips["fire2"] = new ClipBuilder(0.48f)
                    .Key(0f, Ease.Linear, B("arm_r"))
                    .Key(0.09f, Ease.Out, B("arm_r", -0.10f, 0.05f, -0.03f, -30f, -30f, -35f))
                    .Key(0.18f, Ease.In, B("arm_r", 0.06f, -0.04f, 0.10f, 40f, 30f, 25f))
                    .Key(0.26f, Ease.Out, B("arm_r", 0.08f, -0.07f, 0.05f, 50f, 36f, 30f))
                    .Key(0.48f, Ease.Back, B("arm_r"))
                    .Build();
                clips["inspect"] = new ClipBuilder(2.2f)
                    .Key(0f, Ease.Linear, B("weapon"), B("arm_r"))
                    .Key(0.3f, Ease.Out, B("arm_r", -0.04f, 0.03f, 0f, -10f, 20f, 0f))
                    .Key(1.1f, Ease.InOut, B("arm_r", -0.04f, 0.03f, 0f, -10f, 20f, 0f), B("weapon", 0f, 0f, 0f, 0f, 0f, 360f))
                    .Key(1.5f, Ease.InOut, B("arm_r", -0.02f, 0.04f, 0f, -20f, 10f, 10f), B("weapon", 0f, 0f, 0f, 0f, 0f, 360f))
                    .Key(2.2f, Ease.Back, B("arm_r"), B("weapon", 0f, 0f, 0f, 0f, 0f, 360f))
                    .Build();
                break;
            case "grenade":
                // Left hand pulls the pin (pin flies off), arm winds back, throws, a fresh grenade comes up.
                clips["fire"] = new ClipBuilder(0.85f)
                    .Key(0f, Ease.Linear, B("arm_r"), B("arm_l"), B("pin"))
                    .Key(0.12f, Ease.Out, B("arm_l", 0.24f, 0.08f, 0.0f, -10f, -20f, -20f), B("arm_r", -0.01f, 0.01f))
                    .Key(0.2f, Ease.InOut, B("arm_l", 0.25f, 0.08f, 0.0f, -10f, -20f, -20f), B("arm_r", -0.01f, 0.01f))
                    .Key(0.3f, Ease.In, B("arm_l", 0.12f, 0.06f, -0.02f, -5f, -10f, -10f), B("pin", -0.13f, 0.02f, -0.01f, 0f, 0f, 60f), B("arm_r", -0.01f, 0.01f))
                    .Key(0.42f, Ease.Out, B("arm_l"), B("pin", -0.5f, -0.3f, 0f), B("arm_r", 0.05f, 0.05f, -0.04f, -45f, 10f, 12f))
                    .Key(0.52f, Ease.In, B("arm_l"), B("pin", -0.5f, -0.3f, 0f), B("arm_r", -0.05f, -0.05f, 0.17f, 52f, -16f, -10f))
                    .Key(0.62f, Ease.Linear, B("arm_r", 0f, -0.30f, 0.05f, 40f), B("pin", -0.5f, -0.3f, 0f))
                    .Key(0.85f, Ease.Out, B("arm_r"), B("pin"))
                    .Build();
                clips["inspect"] = new ClipBuilder(1.8f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.15f, Ease.Out, B("weapon", 0f, -0.01f, 0f))
                    .Key(0.55f, Ease.Out, B("weapon", 0f, 0.14f, 0.02f, 360f, 0f, 0f))
                    .Key(0.95f, Ease.In, B("weapon", 0f, 0f, 0f, 360f, 0f, 0f))
                    .Key(1.05f, Ease.Out, B("weapon", 0f, -0.012f, 0f, 360f, 0f, 0f))
                    .Key(1.8f, Ease.Back, B("weapon", 0f, 0f, 0f, 360f, 0f, 0f))
                    .Build();
                break;
            case "physgun":
                clips["fire"] = new ClipBuilder(0.2f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.04f, Ease.Out, B("weapon", 0f, 0.006f, -0.015f, -4f))
                    .Key(0.2f, Ease.InOut, B("weapon"))
                    .Build();
                // Grab: a push forward and a twist as the beam locks on. Release: a recoil flick that settles.
                clips["grab"] = new ClipBuilder(0.4f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.06f, Ease.Out, B("weapon", 0f, 0f, 0.02f, 2f, 0f, -10f))
                    .Key(0.18f, Ease.InOut, B("weapon", 0f, 0.004f, -0.018f, -3f, 0f, 6f))
                    .Key(0.4f, Ease.Back, B("weapon"))
                    .Build();
                clips["release"] = new ClipBuilder(0.45f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.05f, Ease.Out, B("weapon", 0f, 0.015f, -0.03f, -12f, 0f, 8f))
                    .Key(0.2f, Ease.InOut, B("weapon", 0f, -0.004f, 0.006f, 3f, 0f, -3f))
                    .Key(0.45f, Ease.InOut, B("weapon"))
                    .Build();
                clips["inspect"] = new ClipBuilder(2.6f)
                    .Key(0f, Ease.Linear, B("hand_r"))
                    .Key(0.6f, Ease.InOut, B("hand_r", -0.03f, 0.04f, 0f, -10f, 30f, -50f))
                    .Key(1.4f, Ease.InOut, B("hand_r", -0.03f, 0.05f, 0f, -14f, 34f, -54f))
                    .Key(1.9f, Ease.InOut, B("hand_r", -0.01f, 0.02f, 0f, 8f, -12f, 14f))
                    .Key(2.6f, Ease.InOut, B("hand_r"))
                    .Build();
                break;
            case "toolgun":
                clips["fire"] = new ClipBuilder(0.18f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.03f, Ease.Out, B("weapon", 0f, 0.006f, -0.015f, -5f))
                    .Key(0.18f, Ease.InOut, B("weapon"))
                    .Build();
                // Turn the rear screen to the face, tap it, turn back.
                clips["inspect"] = new ClipBuilder(2.4f)
                    .Key(0f, Ease.Linear, B("hand_r"))
                    .Key(0.5f, Ease.InOut, B("hand_r", -0.05f, 0.05f, -0.02f, 25f, -45f, 10f))
                    .Key(1.6f, Ease.InOut, B("hand_r", -0.05f, 0.055f, -0.02f, 28f, -48f, 12f))
                    .Key(2.4f, Ease.InOut, B("hand_r"))
                    .Build();
                break;
            case "hands":
                clips["inspect"] = new ClipBuilder(2.4f)
                    .Key(0f, Ease.Linear, B("arm_r"), B("arm_l"))
                    .Key(0.6f, Ease.InOut, B("arm_r", -0.04f, 0.05f, 0f, -20f, 0f, 70f), B("arm_l", 0.04f, 0.05f, 0f, -20f, 0f, -70f))
                    .Key(1.6f, Ease.InOut, B("arm_r", -0.04f, 0.055f, 0f, -22f, 0f, 75f), B("arm_l", 0.04f, 0.055f, 0f, -22f, 0f, -75f))
                    .Key(2.4f, Ease.InOut, B("arm_r"), B("arm_l"))
                    .Build();
                clips["fire"] = new ClipBuilder(0.3f)
                    .Key(0f, Ease.Linear, B("arm_r"))
                    .Key(0.08f, Ease.Out, B("arm_r", -0.03f, 0.03f, 0.08f, -10f))
                    .Key(0.3f, Ease.InOut, B("arm_r"))
                    .Build();
                break;
            default:
                // Gun shot: sharp kick (slide/bolt cycles), quick recovery. The recoil spring adds randomness on top.
                clips["fire"] = new ClipBuilder(id == "shotgun" ? 0.8f : 0.16f)
                    .Key(0f, Ease.Linear, B("weapon"), B("slide"), B("pump"))
                    .Key(0.025f, Ease.Out, B("weapon", 0f, 0.008f, -0.022f, -6f), B("slide", 0f, 0f, -0.03f))
                    .Key(0.08f, Ease.InOut, B("weapon", 0f, 0.002f, -0.006f, -1.5f), B("slide"))
                    .Key(0.16f, Ease.InOut, B("weapon"), B("slide"))
                    .Build();
                if (id == "shotgun")
                {
                    // Pump: anticipation, rack back, slam forward with a small settle.
                    var f = new ClipBuilder(0.8f)
                        .Key(0f, Ease.Linear, B("weapon"), B("pump"))
                        .Key(0.04f, Ease.Out, B("weapon", 0f, 0.014f, -0.035f, -10f))
                        .Key(0.24f, Ease.InOut, B("weapon", 0f, 0f, 0f, 0f, 0f, 3f), B("pump", 0f, 0f, 0.006f))
                        .Key(0.38f, Ease.In, B("weapon", 0f, 0f, 0f, 1f, 0f, 5f), B("pump", 0f, 0f, -0.08f))
                        .Key(0.5f, Ease.In, B("weapon", 0f, 0f, 0.004f, -1f, 0f, 3f), B("pump", 0f, 0f, 0.004f))
                        .Key(0.8f, Ease.InOut, B("weapon"), B("pump"));
                    clips["fire"] = f.Build();
                }
                break;
        }

        // Reloads.
        if (id == "shotgun")
        {
            // Shell by shell (interruptible): roll the port up → (shell: hand dips to the belt, pushes one in) × n → rack.
            Float3 port = V(0.0f, -0.005f, -0.18f);
            var roll = B("hand_r", 0f, 0.01f, 0f, -6f, 0f, 26f);
            clips["reload_start"] = new ClipBuilder(Firearm.ShellStart)
                .Key(0f, Ease.Linear, B("hand_r"), B("hand_l"))
                .Key(Firearm.ShellStart, Ease.InOut, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f))
                .Build();
            clips["reload_start"].Hold = true;
            clips["reload_shell"] = new ClipBuilder(Firearm.ShellTime)
                .Key(0f, Ease.Linear, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f))
                .Key(0.12f, Ease.Out, roll, B("hand_l", port.X - 0.02f, port.Y - 0.12f, port.Z - 0.02f, 25f))
                .Key(0.26f, Ease.InOut, roll, B("hand_l", port.X, port.Y - 0.01f, port.Z + 0.01f, -8f))
                .Key(0.32f, Ease.In, roll, B("hand_l", port.X, port.Y + 0.004f, port.Z + 0.03f, -12f))
                .Key(Firearm.ShellTime, Ease.Out, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f))
                .Build();
            clips["reload_shell"].Hold = true;
            clips["reload_end"] = new ClipBuilder(Firearm.ShellEnd + 0.15f)
                .Key(0f, Ease.Linear, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f), B("pump"))
                .Key(0.2f, Ease.InOut, B("hand_r"), B("hand_l"), B("pump"))
                .Key(0.34f, Ease.In, B("hand_r", 0f, 0f, 0f, 0f, 0f, 4f), B("hand_l"), B("pump", 0f, 0f, -0.08f))
                .Key(0.46f, Ease.In, B("hand_r"), B("hand_l"), B("pump", 0f, 0f, 0.004f))
                .Key(Firearm.ShellEnd + 0.15f, Ease.InOut, B("hand_r"), B("hand_l"), B("pump"))
                .Build();
            clips["reload"] = clips["reload_end"];
            clips["inspect"] = new ClipBuilder(2.6f)
                .Key(0f, Ease.Linear, B("hand_r"), B("pump"))
                .Key(0.6f, Ease.InOut, B("hand_r", -0.04f, 0.05f, 0f, -12f, 22f, -38f))
                .Key(1.2f, Ease.InOut, B("hand_r", -0.04f, 0.05f, 0f, -12f, 22f, -38f), B("pump", 0f, 0f, -0.035f))
                .Key(1.5f, Ease.InOut, B("hand_r", -0.04f, 0.05f, 0f, -12f, 22f, -38f), B("pump"))
                .Key(2.6f, Ease.InOut, B("hand_r"), B("pump"))
                .Build();
        }
        else if (has("mag") || id == "rpg")
        {
            // Mag change: tilt, support hand to the mag, pull it, fetch a fresh one, seat it with a bump;
            // pistol racks the slide, SMG pulls the charging handle; RPG slides a new warhead in from the front.
            string m = id == "rpg" ? "rocket" : "mag";
            Float3 toMag = id switch { "smg" => V(0f, -0.07f, -0.12f), "pistol" => V(0f, -0.02f, -0.01f), "rpg" => V(0f, 0.10f, 0.25f), _ => V(0f, -0.04f, -0.08f) };
            Float3 pull = id == "rpg" ? V(-0.05f, 0.06f, 0.20f) : V(-0.02f, -0.22f, -0.04f);
            float L = id == "pistol" ? 1.4f : id == "rpg" ? 2.0f : 1.5f;
            var tilt = B("hand_r", 0f, 0.006f, 0f, 6f, 0f, -24f);
            var c = new ClipBuilder(L)
                .Key(0f, Ease.Linear, B("hand_r"), B("hand_l"), B(m), B("weapon"), B("slide"), B("charge"))
                .Key(0.06f * L, Ease.Out, B("hand_r", 0f, -0.004f, 0f, -3f, 0f, 4f)) // anticipation
                .Key(0.20f * L, Ease.InOut, tilt, B("hand_l", toMag.X, toMag.Y, toMag.Z))
                .Key(0.30f * L, Ease.InOut, tilt, B("hand_l", toMag.X, toMag.Y - 0.01f, toMag.Z), B(m, 0f, -0.01f, 0f))
                .Key(0.44f * L, Ease.In, B("hand_r", 0f, 0.006f, 0f, 8f, 0f, -26f), B("hand_l", toMag.X + pull.X, toMag.Y + pull.Y, toMag.Z + pull.Z, 20f), B(m, pull.X, pull.Y, pull.Z, 20f))
                .Key(0.55f * L, Ease.InOut, B("hand_r", 0f, 0.006f, 0f, 8f, 0f, -26f), B("hand_l", toMag.X + pull.X, toMag.Y + pull.Y, toMag.Z + pull.Z, 20f), B(m, pull.X, pull.Y, pull.Z, 20f))
                .Key(0.70f * L, Ease.Out, tilt, B("hand_l", toMag.X, toMag.Y - 0.015f, toMag.Z), B(m, 0f, -0.015f, 0f))
                .Key(0.76f * L, Ease.In, B("hand_r", 0f, 0.014f, 0f, 4f, 0f, -22f), B("hand_l", toMag.X, toMag.Y + 0.004f, toMag.Z), B(m), B("weapon", 0f, 0.006f, 0f, -2f));
            if (id == "pistol")
                c.Key(0.85f * L, Ease.InOut, B("hand_r", 0f, 0.01f, 0f, 2f, 0f, -12f), B("hand_l", -0.012f, 0.072f, 0.004f, 0f, 0f, -18f), B("slide"))
                 .Key(0.90f * L, Ease.In, B("hand_r", 0f, 0.01f, 0f, 2f, 0f, -12f), B("hand_l", -0.012f, 0.072f, -0.028f, 0f, 0f, -18f), B("slide", 0f, 0f, -0.03f))
                 .Key(0.93f * L, Ease.Out, B("hand_r"), B("hand_l", -0.03f, 0.05f, -0.04f, 0f, 0f, -24f), B("slide"));
            if (id == "smg")
                c.Key(0.85f * L, Ease.InOut, B("hand_r", 0f, 0.01f, 0f, 0f, 0f, 10f), B("hand_l", 0.012f, 0.04f, -0.305f, 0f, 0f, 30f), B("charge"))
                 .Key(0.90f * L, Ease.In, B("hand_r", 0f, 0.01f, 0f, 0f, 0f, 10f), B("hand_l", 0.012f, 0.04f, -0.345f, 0f, 0f, 30f), B("charge", 0f, 0f, -0.04f))
                 .Key(0.93f * L, Ease.Out, B("hand_r"), B("hand_l", 0.012f, 0.03f, -0.32f, 0f, 0f, 20f), B("charge"));
            c.Key(L, Ease.InOut, B("hand_r"), B("hand_l"), B(m), B("weapon"), B("slide"), B("charge"));
            clips["reload"] = c.Build();
            clips["inspect"] = id switch
            {
                // Pistol: show the left side, then flip to the right with a press check.
                "pistol" => new ClipBuilder(2.6f)
                    .Key(0f, Ease.Linear, B("hand_r"), B("slide"), B("hand_l"))
                    .Key(0.5f, Ease.InOut, B("hand_r", -0.04f, 0.04f, 0f, -8f, 30f, -55f), B("hand_l", 0f, -0.1f, -0.05f))
                    .Key(1.2f, Ease.InOut, B("hand_r", -0.04f, 0.045f, 0f, -10f, 34f, -58f), B("hand_l", 0f, -0.1f, -0.05f))
                    .Key(1.6f, Ease.InOut, B("hand_r", -0.02f, 0.03f, 0f, 6f, -20f, 30f), B("hand_l", -0.012f, 0.072f, 0.0f, 0f, 0f, -18f))
                    .Key(1.85f, Ease.InOut, B("hand_r", -0.02f, 0.03f, 0f, 6f, -20f, 30f), B("hand_l", -0.012f, 0.072f, -0.012f, 0f, 0f, -18f), B("slide", 0f, 0f, -0.012f))
                    .Key(2.05f, Ease.InOut, B("hand_r", -0.02f, 0.03f, 0f, 6f, -20f, 30f), B("hand_l", -0.012f, 0.072f, 0.0f, 0f, 0f, -18f), B("slide"))
                    .Key(2.6f, Ease.InOut, B("hand_r"), B("slide"), B("hand_l"))
                    .Build(),
                // SMG: roll to look at the mag, tap it.
                "smg" => new ClipBuilder(2.6f)
                    .Key(0f, Ease.Linear, B("hand_r"), B("hand_l"))
                    .Key(0.6f, Ease.InOut, B("hand_r", -0.03f, 0.04f, 0f, -15f, 15f, 45f), B("hand_l", 0f, -0.07f, -0.12f))
                    .Key(1.3f, Ease.InOut, B("hand_r", -0.03f, 0.045f, 0f, -16f, 16f, 47f), B("hand_l", 0f, -0.07f, -0.12f))
                    .Key(1.42f, Ease.In, B("hand_r", -0.03f, 0.045f, 0f, -16f, 16f, 47f), B("hand_l", 0f, -0.06f, -0.12f))
                    .Key(1.55f, Ease.Out, B("hand_r", -0.03f, 0.05f, 0f, -16f, 16f, 47f), B("hand_l", 0f, -0.07f, -0.12f))
                    .Key(2.6f, Ease.InOut, B("hand_r"), B("hand_l"))
                    .Build(),
                _ => new ClipBuilder(2.4f)
                    .Key(0f, Ease.Linear, B("hand_r"))
                    .Key(0.6f, Ease.InOut, B("hand_r", -0.03f, 0.06f, -0.03f, -20f, 20f, -20f))
                    .Key(1.6f, Ease.InOut, B("hand_r", -0.03f, 0.065f, -0.03f, -22f, 22f, -22f))
                    .Key(2.4f, Ease.InOut, B("hand_r"))
                    .Build(),
            };
        }
        if (!clips.ContainsKey("inspect"))
            clips["inspect"] = new ClipBuilder(2.4f)
                .Key(0f, Ease.Linear, B("arm_r"))
                .Key(0.6f, Ease.InOut, B("arm_r", -0.07f, 0.04f, 0f, -8f, 32f, 34f))
                .Key(1.5f, Ease.InOut, B("arm_r", -0.07f, 0.05f, 0f, -12f, 38f, 38f))
                .Key(2.4f, Ease.InOut, B("arm_r"))
                .Build();
    }
}
