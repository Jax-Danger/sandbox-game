using Prowl.Vector;

namespace Sandbox;

/// <summary>Built-in view model definitions: shared arm rig + primitive weapon meshes + shared clips.</summary>
public static class ViewModels
{
    private static readonly Color Sleeve = new(0.20f, 0.24f, 0.30f, 1f);
    private static readonly Color Glove = new(0.12f, 0.12f, 0.13f, 1f);
    private static readonly Color Gun = new(0.16f, 0.17f, 0.19f, 1f);
    private static readonly Color GunLight = new(0.32f, 0.34f, 0.37f, 1f);
    private static readonly Color Wood = new(0.48f, 0.30f, 0.16f, 1f);
    private static readonly Color Accent = new(0.30f, 0.64f, 1f, 1f);

    private static (string, Float3, Float3) B(string bone, float px = 0, float py = 0, float pz = 0, float rx = 0, float ry = 0, float rz = 0)
        => (bone, new Float3(px, py, pz), new Float3(rx, ry, rz));

    public static ViewModelDef Create(string id)
    {
        var d = new ViewModelDef { Id = id };
        // Standard rig (see README "View models"): vm_root → arm_r → hand_r → weapon → muzzle, vm_root → arm_l → hand_l.
        d.Bones.Add(("arm_r", "vm_root", new Float3(0.16f, -0.19f, 0.20f), new Float3(0f, -4f, 0f)));
        d.Bones.Add(("hand_r", "arm_r", new Float3(0f, 0.02f, 0.20f), Float3.Zero));
        d.Bones.Add(("weapon", "hand_r", new Float3(0f, 0.05f, 0.0f), Float3.Zero));
        d.Bones.Add(("muzzle", "weapon", new Float3(0f, 0.035f, 0.30f), Float3.Zero));
        d.Bones.Add(("arm_l", "vm_root", new Float3(0.02f, -0.24f, 0.20f), new Float3(0f, 22f, 0f)));
        d.Bones.Add(("hand_l", "arm_l", new Float3(0.02f, 0.04f, 0.20f), Float3.Zero));

        // Arms: sleeve forearm + glove.
        d.Parts.Add(new VmPart("arm_r", 'b', new Float3(0.075f, 0.075f, 0.30f), new Float3(0f, 0f, 0.04f), Sleeve));
        d.Parts.Add(new VmPart("hand_r", 'b', new Float3(0.07f, 0.06f, 0.09f), Float3.Zero, Glove));
        d.Parts.Add(new VmPart("arm_l", 'b', new Float3(0.075f, 0.075f, 0.30f), new Float3(0f, 0f, 0.04f), Sleeve));
        d.Parts.Add(new VmPart("hand_l", 'b', new Float3(0.07f, 0.06f, 0.09f), Float3.Zero, Glove));

        void W(char shape, float sx, float sy, float sz, float ox, float oy, float oz, Color c, float rx = 0f, bool glow = false)
            => d.Parts.Add(new VmPart("weapon", shape, new Float3(sx, sy, sz), new Float3(ox, oy, oz), c, new Float3(rx, 0f, 0f), glow));

        switch (id)
        {
            case "hands":
                d.Bones[0] = ("arm_r", "vm_root", new Float3(0.22f, -0.34f, 0.12f), new Float3(10f, -6f, 0f));
                d.Bones[4] = ("arm_l", "vm_root", new Float3(-0.22f, -0.34f, 0.12f), new Float3(10f, 6f, 0f));
                break;
            case "physgun":
                W('b', 0.07f, 0.09f, 0.36f, 0f, 0.04f, 0.12f, GunLight);
                W('c', 0.05f, 0.14f, 0f, 0f, 0.04f, 0.33f, Accent, 90f, true);
                W('b', 0.04f, 0.10f, 0.05f, 0f, -0.03f, 0.0f, Gun);
                break;
            case "toolgun":
                W('b', 0.06f, 0.08f, 0.24f, 0f, 0.04f, 0.08f, new Color(0.85f, 0.85f, 0.88f, 1f));
                W('b', 0.03f, 0.03f, 0.12f, 0f, 0.05f, 0.24f, Gun);
                W('b', 0.05f, 0.03f, 0.05f, 0f, 0.09f, 0.04f, Accent, 0f, true);
                break;
            case "spawngun":
                W('b', 0.08f, 0.08f, 0.22f, 0f, 0.04f, 0.08f, new Color(0.25f, 0.70f, 0.45f, 1f));
                W('s', 0.09f, 0f, 0f, 0f, 0.05f, 0.24f, Accent, 0f, true);
                break;
            case "pistol":
                W('b', 0.035f, 0.045f, 0.19f, 0f, 0.035f, 0.08f, Gun);
                W('b', 0.03f, 0.09f, 0.045f, 0f, -0.02f, 0.0f, GunLight, 12f);
                d.Bones[3] = ("muzzle", "weapon", new Float3(0f, 0.035f, 0.18f), Float3.Zero);
                d.RecoilKick = 1.0f;
                break;
            case "smg":
                W('b', 0.05f, 0.07f, 0.30f, 0f, 0.035f, 0.10f, Gun);
                W('b', 0.03f, 0.12f, 0.04f, 0f, -0.04f, 0.10f, GunLight);
                W('c', 0.025f, 0.10f, 0f, 0f, 0.04f, 0.29f, GunLight, 90f);
                d.Bones[3] = ("muzzle", "weapon", new Float3(0f, 0.04f, 0.34f), Float3.Zero);
                d.RecoilKick = 0.45f;
                break;
            case "shotgun":
                W('b', 0.05f, 0.06f, 0.30f, 0f, 0.03f, 0.04f, Wood);
                W('c', 0.035f, 0.42f, 0f, 0f, 0.06f, 0.30f, Gun, 90f);
                W('c', 0.045f, 0.16f, 0f, 0f, 0.025f, 0.30f, Wood, 90f);
                d.Bones[3] = ("muzzle", "weapon", new Float3(0f, 0.06f, 0.52f), Float3.Zero);
                d.Bones[5] = ("hand_l", "arm_l", new Float3(0.08f, 0.04f, 0.26f), Float3.Zero);
                d.RecoilKick = 2.2f;
                break;
            case "rpg":
                W('c', 0.12f, 0.80f, 0f, 0f, 0.07f, 0.12f, new Color(0.30f, 0.36f, 0.24f, 1f), 90f);
                W('b', 0.04f, 0.10f, 0.05f, 0f, -0.02f, 0.0f, Gun);
                d.Bones[3] = ("muzzle", "weapon", new Float3(0f, 0.07f, 0.52f), Float3.Zero);
                d.RecoilKick = 3f;
                break;
            case "grenade":
                W('s', 0.08f, 0f, 0f, 0f, 0.04f, 0.03f, new Color(0.25f, 0.32f, 0.20f, 1f));
                W('b', 0.02f, 0.03f, 0.02f, 0f, 0.09f, 0.03f, GunLight);
                break;
            case "crowbar":
                W('c', 0.03f, 0.62f, 0f, 0f, 0.0f, 0.22f, new Color(0.72f, 0.16f, 0.14f, 1f), 70f);
                W('b', 0.03f, 0.03f, 0.10f, 0f, 0.13f, 0.50f, GunLight, -40f);
                break;
        }

        // Shared clips (offsets from rest). Weapons can override any of them via assets/viewmodels/<id>.clips.json.
        d.Clips["idle"] = new ClipBuilder(3.2f, loop: true)
            .Key(0f, Ease.InOut, B("vm_root"))
            .Key(1.6f, Ease.InOut, B("vm_root", 0f, 0.004f, 0f, -0.6f), B("hand_l", 0f, 0.003f))
            .Build();
        d.Clips["draw"] = new ClipBuilder(0.45f)
            .Key(0f, Ease.Linear, B("vm_root", 0f, -0.22f, -0.05f, 35f, 0f, -10f))
            .Key(0.45f, Ease.Back, B("vm_root"))
            .Build();
        d.Clips["holster"] = new ClipBuilder(0.22f)
            .Key(0f, Ease.Linear, B("vm_root"))
            .Key(0.22f, Ease.In, B("vm_root", 0f, -0.25f, -0.05f, 35f))
            .Build();
        d.Clips["fire"] = id == "crowbar"
            ? new ClipBuilder(0.40f)
                .Key(0f, Ease.Linear, B("arm_r"))
                .Key(0.08f, Ease.Out, B("arm_r", 0.02f, 0.06f, -0.04f, -45f, 20f, 10f))
                .Key(0.18f, Ease.In, B("arm_r", -0.12f, -0.04f, 0.10f, 50f, -35f, -15f))
                .Key(0.40f, Ease.InOut, B("arm_r"))
                .Build()
            : new ClipBuilder(0.14f)
                .Key(0f, Ease.Linear, B("weapon"))
                .Key(0.03f, Ease.Out, B("weapon", 0f, 0.01f, -0.025f, -7f))
                .Key(0.14f, Ease.InOut, B("weapon"))
                .Build();
        d.Clips["reload"] = new ClipBuilder(id == "shotgun" ? 1.8f : 1.4f)
            .Key(0f, Ease.Linear, B("weapon"), B("hand_l"))
            .Key(0.30f, Ease.InOut, B("weapon", 0f, -0.03f, 0f, 8f, 0f, -35f), B("hand_l", -0.02f, -0.06f, -0.02f))
            .Key(0.55f, Ease.InOut, B("weapon", 0f, -0.03f, 0f, 8f, 0f, -35f), B("hand_l", 0f, -0.18f, -0.06f, 20f))
            .Key(0.95f, Ease.InOut, B("weapon", 0f, -0.03f, 0f, 8f, 0f, -35f), B("hand_l", 0.04f, -0.02f, 0.0f))
            .Key(1.10f, Ease.Out, B("weapon", 0f, -0.02f, 0f, 2f, 0f, -30f), B("hand_l", 0.04f, 0f, 0.01f))
            .Key(id == "shotgun" ? 1.8f : 1.4f, Ease.InOut, B("weapon"), B("hand_l"))
            .Build();
        d.Clips["inspect"] = new ClipBuilder(2.4f)
            .Key(0f, Ease.Linear, B("arm_r"))
            .Key(0.55f, Ease.InOut, B("arm_r", -0.08f, 0.04f, 0f, -10f, 35f, 30f))
            .Key(1.4f, Ease.InOut, B("arm_r", -0.08f, 0.05f, 0f, -14f, 40f, 34f))
            .Key(1.8f, Ease.InOut, B("arm_r", -0.04f, 0.02f, 0f, 10f, -15f, -20f))
            .Key(2.4f, Ease.InOut, B("arm_r"))
            .Build();
        return d;
    }
}
