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
        "weapon_hands" =>
        [
            new('b', new(0.50f, 0.56f, 0.22f), new(0f, 0f, 0f), Skin),
            new('b', new(0.11f, 0.34f, 0.12f), new(-0.19f, 0.43f, 0f), Skin), new('b', new(0.11f, 0.38f, 0.12f), new(-0.06f, 0.46f, 0f), Skin),
            new('b', new(0.11f, 0.36f, 0.12f), new(0.07f, 0.45f, 0f), Skin), new('b', new(0.11f, 0.30f, 0.12f), new(0.20f, 0.40f, 0f), Skin),
            new('b', new(0.12f, 0.30f, 0.12f), new(0.30f, 0.02f, 0f), Skin, false, new(0f, 0f, -40f)),
            new('b', new(0.52f, 0.22f, 0.26f), new(0f, -0.36f, 0f), new Color(0.20f, 0.30f, 0.45f, 1f)),
        ],
        "weapon_crowbar" =>
        [
            new('c', new(0.09f, 1.6f, 0.09f), new(0f, 0f, 0f), Red, false, new(0f, 0f, 40f)),
            new('b', new(0.09f, 0.30f, 0.09f), new(-0.62f, 0.72f, 0f), Red, false, new(0f, 0f, -30f)),
            new('b', new(0.09f, 0.22f, 0.09f), new(0.56f, -0.66f, 0f), Steel, false, new(0f, 0f, 75f)),
        ],
        "weapon_baton" =>
        [
            new('c', new(0.12f, 1.1f, 0.12f), new(0f, 0.2f, 0f), Grip, false, new(0f, 0f, 35f)),
            new('c', new(0.15f, 0.42f, 0.15f), new(0.42f, -0.4f, 0f), new Color(0.22f, 0.22f, 0.26f, 1f), false, new(0f, 0f, 35f)),
            new('c', new(0.30f, 0.05f, 0.30f), new(0.30f, -0.24f, 0f), Steel, false, new(0f, 0f, 35f)),
        ],
        "weapon_lockpick" =>
        [
            new('b', new(0.16f, 0.46f, 0.08f), new(0f, -0.35f, 0f), new Color(0.35f, 0.24f, 0.16f, 1f)),
            new('b', new(0.035f, 0.70f, 0.02f), new(0f, 0.22f, 0f), Steel),
            new('b', new(0.10f, 0.035f, 0.02f), new(0.04f, 0.56f, 0f), Steel),
            new('b', new(0.035f, 0.55f, 0.02f), new(0.09f, 0.12f, 0.02f), Steel, false, new(0f, 0f, -8f)),
        ],
        "weapon_medkit" =>
        [
            new('b', new(0.80f, 0.56f, 0.30f), Float3.Zero, new Color(0.94f, 0.94f, 0.92f, 1f)),
            new('b', new(0.36f, 0.10f, 0.02f), new(0f, 0f, 0.16f), Red), new('b', new(0.10f, 0.36f, 0.02f), new(0f, 0f, 0.16f), Red),
            new('b', new(0.34f, 0.06f, 0.06f), new(0f, 0.32f, 0f), Grip),
        ],
        _ => null,
    };

    public static bool IsCustom(string id) => Custom(id) is not null;

    public static ModelDef? Weapon(string id, string? vmId)
    {
        if (Custom(id) is { } cp)
        {
            float r = cp.Max(p => MathF.Max(MathF.Abs(p.Offset.X), MathF.Abs(p.Offset.Y)) + MathF.Max(p.Size.X, p.Size.Y) / 2f);
            return new ModelDef("w_" + id, id, "Weapon", new Float3(r * 1.5f, r * 1.5f, 0.3f), 1f, "Metal", cp);
        }
        if (vmId is null) return null;
        ViewModelDef vm;
        try { vm = ViewModels.Create(vmId); } catch { return null; }
        var src = vm.Parts.Where(p => p.Bone == "weapon").ToList();
        if (src.Count == 0) src = vm.Parts.Where(p => p.Bone == "hand_r").ToList();
        if (src.Count == 0) return null;
        const float k = 4f;
        Part[] parts = src.Select(p => new Part(p.Shape is 'c' or 's' ? p.Shape : 'b', p.Size * k, p.Offset * k, p.Color, false, p.Rot)).ToArray();
        Float3 min = new(1e9f), max = new(-1e9f);
        foreach (Part p in parts)
        {
            float r = MathF.Max(p.Size.X, MathF.Max(p.Size.Y, p.Size.Z)) / 2f;
            Float3 h = p.Rot == default ? p.Size / 2f : new Float3(r);
            min = new Float3(MathF.Min(min.X, p.Offset.X - h.X), MathF.Min(min.Y, p.Offset.Y - h.Y), MathF.Min(min.Z, p.Offset.Z - h.Z));
            max = new Float3(MathF.Max(max.X, p.Offset.X + h.X), MathF.Max(max.Y, p.Offset.Y + h.Y), MathF.Max(max.Z, p.Offset.Z + h.Z));
        }
        Float3 c = (min + max) / 2f;
        parts = parts.Select(p => p with { Offset = p.Offset - c }).ToArray();
        return new ModelDef("w_" + id, id, "Weapon", max - min, 1f, "Metal", parts);
    }

    private static readonly Color Skin = new(0.86f, 0.68f, 0.54f, 1f), Dark = new(0.14f, 0.14f, 0.16f, 1f);

    private static Part[] Person(Color shirt, Color pants) =>
    [
        new('b', new(0.36f, 0.22f, 0.22f), new(0f, 0.0f, 0f), pants),
        new('b', new(0.40f, 0.55f, 0.24f), new(0f, 0.40f, 0f), shirt),
        new('s', new(0.26f, 0.26f, 0.26f), new(0f, 0.83f, 0f), Skin),
        new('b', new(0.11f, 0.50f, 0.11f), new(-0.27f, 0.36f, 0f), shirt),
        new('b', new(0.11f, 0.50f, 0.11f), new(0.27f, 0.36f, 0f), shirt),
        new('b', new(0.14f, 0.60f, 0.15f), new(-0.10f, -0.42f, 0f), pants),
        new('b', new(0.14f, 0.60f, 0.15f), new(0.10f, -0.42f, 0f), pants),
    ];

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
