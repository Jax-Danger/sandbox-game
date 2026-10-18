using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Thumbnail subjects for spawn-menu tiles that aren't props: weapons (their view-model parts, scaled up)
/// and entities (small multi-part placeholder models). Rendered by <see cref="IconRenderer"/> like model props.
/// </summary>
public static class IconSubjects
{
    private static readonly Color Steel = new(0.62f, 0.64f, 0.68f, 1f), Red = new(0.82f, 0.16f, 0.12f, 1f), Grip = new(0.10f, 0.10f, 0.12f, 1f);

    /// <summary>Hand-made subjects for weapons whose view model reads poorly as a thumbnail.</summary>
    private static Part[]? Custom(string id) => id switch
    {
        "weapon_hands" => HandIcon(),
        "weapon_crowbar" =>
        [
            M(Meshes.Sweep([new Meshes.Sec(new(-0.1f, -0.75f, 0f), 0.045f, 0.045f), new Meshes.Sec(new(0f, 0.55f, 0f), 0.045f, 0.045f), new Meshes.Sec(new(-0.05f, 0.72f, 0f), 0.043f, 0.043f),
                new Meshes.Sec(new(-0.2f, 0.78f, 0f), 0.04f, 0.04f), new Meshes.Sec(new(-0.3f, 0.7f, 0f), 0.03f, 0.018f, 3f)], Float3.UnitZ, 16, 0.6f), Red, new(0f, 0f, -20f)),
            M(Meshes.Sweep([new Meshes.Sec(new(-0.1f, -0.75f, 0f), 0.044f, 0.044f), new Meshes.Sec(new(-0.04f, -0.82f, 0f), 0.04f, 0.035f), new Meshes.Sec(new(0.08f, -0.86f, 0f), 0.028f, 0.014f, 3f)], Float3.UnitZ, 14, 0.6f), Steel, new(0f, 0f, -20f)),
        ],
        "weapon_baton" =>
        [
            M(Meshes.Lathe("icbaton", [(0.0001f, -0.7f), (0.075f, -0.7f), (0.08f, -0.66f), (0.075f, -0.2f), (0.06f, -0.18f), (0.05f, 0.65f), (0.055f, 0.7f), (0.0001f, 0.72f)], 28), Grip, new(0f, 0f, 35f)),
            M(Meshes.Sweep([new Meshes.Sec(new(0.06f, -0.3f, 0f), 0.04f, 0.04f), new Meshes.Sec(new(0.2f, -0.32f, 0f), 0.04f, 0.04f)], Float3.UnitY, 14, 0.7f), new Color(0.22f, 0.22f, 0.26f, 1f), new(0f, 0f, 35f)),
        ],
        "weapon_lockpick" =>
        [
            M(Meshes.Sweep([new Meshes.Sec(new(0f, -0.6f, 0f), 0.06f, 0.035f, 3f), new Meshes.Sec(new(0f, -0.35f, 0f), 0.08f, 0.04f, 3f), new Meshes.Sec(new(0f, -0.12f, 0f), 0.07f, 0.035f, 3f)], Float3.UnitX, 18, 0.7f), new Color(0.35f, 0.24f, 0.16f, 1f)),
            M(Meshes.Sweep([new Meshes.Sec(new(0f, -0.12f, 0f), 0.014f, 0.006f, 3f), new Meshes.Sec(new(0f, 0.5f, 0f), 0.012f, 0.005f, 3f), new Meshes.Sec(new(0.05f, 0.58f, 0f), 0.011f, 0.005f, 3f)], Float3.UnitZ, 10, 0.6f), Steel),
            M(Meshes.Sweep([new Meshes.Sec(new(0.09f, -0.12f, 0.02f), 0.012f, 0.005f, 3f), new Meshes.Sec(new(0.13f, 0.35f, 0.02f), 0.011f, 0.005f, 3f), new Meshes.Sec(new(0.08f, 0.4f, 0.02f), 0.01f, 0.005f, 3f)], Float3.UnitZ, 10, 0.6f), Steel),
        ],
        "weapon_medkit" =>
        [
            new('b', new(0.80f, 0.56f, 0.30f), Float3.Zero, new Color(0.94f, 0.94f, 0.92f, 1f)),
            new('b', new(0.36f, 0.10f, 0.02f), new(0f, 0f, 0.16f), Red), new('b', new(0.10f, 0.36f, 0.02f), new(0f, 0f, 0.16f), Red),
            M(VehicleModels.BentPipe(0.025f, 0.05f, Float3.UnitZ, new(-0.16f, 0.26f, 0f), new(-0.14f, 0.38f, 0f), new(0.14f, 0.38f, 0f), new(0.16f, 0.26f, 0f)), Grip),
        ],
        _ => null,
    };

    private static Part M(Prowl.Runtime.Resources.Mesh m, Color c, Float3 rot = default) => new('m', new(0.4f, 0.4f, 0.1f), Float3.Zero, c, false, rot, m);

    /// <summary>Open gloved hand from the empty-hands view model, scaled up.</summary>
    private static Part[] HandIcon()
    {
        ModelDef? d = null;
        try
        {
            ViewModelDef vm = ViewModels.Create("hands");
            var src = vm.Parts.Where(p => p.Bone == "hand_r" && p.M is not null).ToList();
            if (src.Count > 0) return src.Select(p => new Part('m', new(0.4f, 0.4f, 0.1f), p.Offset * 6f, p.Color, false, p.Rot, p.M, 6f)).ToArray();
        }
        catch { }
        _ = d;
        return [new('s', new(0.5f, 0.5f, 0.5f), Float3.Zero, Skin)];
    }

    public static bool IsCustom(string id) => Custom(id) is not null;

    public static ModelDef? Weapon(string id, string? vmId)
    {
        if (Custom(id) is { } cp)
        {
            float r = cp.Max(p => p.M is { } pm && pm.Vertices is { } vs
                ? vs.Max(v => MathF.Max(MathF.Abs(v.X * p.K + p.Offset.X), MathF.Abs(v.Y * p.K + p.Offset.Y)))
                : MathF.Max(MathF.Abs(p.Offset.X), MathF.Abs(p.Offset.Y)) + MathF.Max(p.Size.X, p.Size.Y) / 2f);
            return new ModelDef("w_" + id, id, "Weapon", new Float3(r * 1.5f, r * 1.5f, 0.3f), 1f, "Metal", cp);
        }
        if (vmId is null) return null;
        ViewModelDef vm;
        try { vm = ViewModels.Create(vmId); } catch { return null; }
        // The weapon and its moving parts (slide, mag, pump, …) all rest at the weapon origin.
        string[] bones = ["weapon", "slide", "mag", "charge", "pump", "rocket", "pin"];
        var src = vm.Parts.Where(p => bones.Contains(p.Bone)).ToList();
        if (src.Count == 0) src = vm.Parts.Where(p => p.Bone == "hand_r").ToList();
        if (src.Count == 0) return null;
        const float k = 4f;
        Float3 min = new(1e9f), max = new(-1e9f);
        void Grow(Float3 a) { min = new Float3(MathF.Min(min.X, a.X), MathF.Min(min.Y, a.Y), MathF.Min(min.Z, a.Z)); max = new Float3(MathF.Max(max.X, a.X), MathF.Max(max.Y, a.Y), MathF.Max(max.Z, a.Z)); }
        var parts = new List<Part>();
        foreach (VmPart p in src)
        {
            if (p.M is { } mesh)
            {
                parts.Add(new Part('m', Float3.One, p.Offset * k, p.Color, false, p.Rot, mesh, k, p.Emissive));
                foreach (Float3 v in mesh.Vertices ?? []) Grow((v + p.Offset) * k);
            }
            else
            {
                char sh = p.Shape is 'c' or 's' ? p.Shape : 'b';
                parts.Add(new Part(sh, p.Size * k, p.Offset * k, p.Color, false, p.Rot, null, 1f, p.Emissive));
                Grow((p.Offset - p.Size / 2f) * k); Grow((p.Offset + p.Size / 2f) * k);
            }
        }
        Float3 c = (min + max) / 2f;
        return new ModelDef("w_" + id, id, "Weapon", max - min, 1f, "Metal", parts.Select(p => p with { Offset = p.Offset - c }).ToArray());
    }

    private static readonly Color Skin = new(0.86f, 0.68f, 0.54f, 1f), Dark = new(0.14f, 0.14f, 0.16f, 1f);

    private static readonly (string Name, Float3 C)[] Body =
    [
        ("Pelvis", new(0f, 1.00f, 0f)), ("Torso", new(0f, 1.37f, 0f)), ("Head", new(0f, 1.77f, 0f)),
        ("Upper arm L", new(-0.28f, 1.44f, 0f)), ("Lower arm L", new(-0.28f, 1.12f, 0f)), ("Upper arm R", new(0.28f, 1.44f, 0f)), ("Lower arm R", new(0.28f, 1.12f, 0f)),
        ("Upper leg L", new(-0.10f, 0.68f, 0f)), ("Lower leg L", new(-0.10f, 0.24f, 0f)), ("Upper leg R", new(0.10f, 0.68f, 0f)), ("Lower leg R", new(0.10f, 0.24f, 0f)),
    ];

    /// <summary>The sculpted ragdoll body (same meshes as in game), centred for a thumbnail.</summary>
    private static Part[] Person(Color shirt, Color pants)
    {
        var parts = new List<Part>();
        Color Tc(Humans.Tint t) => t switch
        {
            Humans.Tint.Skin => Skin, Humans.Tint.Shoe => new Color(0.16f, 0.11f, 0.08f, 1f), Humans.Tint.Hair => new Color(0.18f, 0.12f, 0.08f, 1f),
            Humans.Tint.Eye or Humans.Tint.Belt => new Color(0.06f, 0.06f, 0.06f, 1f), Humans.Tint.Pants => new Color(pants.R * 0.8f, pants.G * 0.8f, pants.B * 0.8f, 1f),
            Humans.Tint.Cuff => new Color(shirt.R * 0.75f, shirt.G * 0.75f, shirt.B * 0.75f, 1f), _ => shirt,
        };
        foreach (var (name, c) in Body)
        {
            var (main, det) = Humans.Limb(name);
            Float3 o = c - new Float3(0f, 0.95f, 0f);
            Color mc = name.StartsWith("Torso") || name.Contains("arm") ? shirt : name == "Head" ? Skin : pants;
            if (name.StartsWith("Lower arm")) mc = Skin;
            parts.Add(new Part('m', Float3.One, o, mc, false, default, main));
            foreach (var d in det) parts.Add(new Part('m', Float3.One, o + d.Offset, Tc(d.Tint), false, default, d.Mesh));
        }
        return parts.ToArray();
    }

    public static ModelDef? Entity(string id) => id switch
    {
        "npc_dummy" => M(id, new(0.8f, 1.9f, 0.4f), Person(new Color(0.82f, 0.78f, 0.70f, 1f), new Color(0.60f, 0.56f, 0.50f, 1f))),
        "npc_ragdoll" => M(id, new(0.8f, 1.9f, 0.4f), Person(new Color(0.85f, 0.55f, 0.30f, 1f), new Color(0.30f, 0.32f, 0.40f, 1f))),
        "npc_friendly" => M(id, new(0.8f, 1.9f, 0.4f), Person(new Color(0.30f, 0.70f, 0.45f, 1f), new Color(0.22f, 0.26f, 0.34f, 1f))),
        "npc_hostile" => M(id, new(0.8f, 1.9f, 0.4f), Person(new Color(0.78f, 0.22f, 0.20f, 1f), new Color(0.15f, 0.15f, 0.17f, 1f))),
        "ent_barrel" => M(id, new(0.7f, 1.0f, 0.7f),
        [
            new('c', new(0.66f, 1.0f, 0.66f), Float3.Zero, new Color(0.80f, 0.16f, 0.12f, 1f)),
            new('c', new(0.69f, 0.06f, 0.69f), new(0f, 0.3f, 0f), new Color(0.95f, 0.80f, 0.20f, 1f)),
            new('c', new(0.69f, 0.06f, 0.69f), new(0f, -0.3f, 0f), new Color(0.95f, 0.80f, 0.20f, 1f)),
        ]),
        "ent_ball" => M(id, new(0.8f, 0.8f, 0.8f), [new('s', new(0.8f, 0.8f, 0.8f), Float3.Zero, new Color(0.95f, 0.30f, 0.55f, 1f))]),
        "ent_camera" => M(id, new(0.6f, 0.5f, 0.7f),
        [
            new('b', new(0.36f, 0.28f, 0.50f), new(0f, 0.08f, 0f), Dark),
            new('c', new(0.20f, 0.14f, 0.20f), new(0f, 0.08f, 0.31f), new Color(0.25f, 0.55f, 1f, 1f), false, new(90f, 0f, 0f)),
            new('b', new(0.06f, 0.24f, 0.06f), new(0f, -0.16f, 0f), Dark),
            new('b', new(0.30f, 0.04f, 0.30f), new(0f, -0.28f, 0f), Dark),
        ]),
        "ent_light" => M(id, new(0.5f, 0.8f, 0.5f),
        [
            new('s', new(0.40f, 0.40f, 0.40f), new(0f, 0.12f, 0f), new Color(1f, 0.92f, 0.62f, 1f)),
            new('c', new(0.20f, 0.20f, 0.20f), new(0f, -0.16f, 0f), new Color(0.55f, 0.56f, 0.58f, 1f)),
        ]),
        "ent_button" => M(id, new(0.6f, 0.4f, 0.6f),
        [
            new('b', new(0.50f, 0.16f, 0.50f), new(0f, -0.06f, 0f), new Color(0.25f, 0.26f, 0.28f, 1f)),
            new('c', new(0.30f, 0.14f, 0.30f), new(0f, 0.08f, 0f), new Color(0.90f, 0.20f, 0.18f, 1f)),
        ]),
        _ => null,
    };

    private static ModelDef M(string id, Float3 size, Part[] parts) => new("e_" + id, id, "Entity", size, 1f, "Plastic", parts);
}
