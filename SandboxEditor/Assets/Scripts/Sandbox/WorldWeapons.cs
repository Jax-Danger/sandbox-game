// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Third-person / world models of weapons, built from the same sculpted parts as the view models (weapon bones only,
/// no hands) at real-world scale. Used on shipment crates so you can see what's inside, and anywhere a gun sits in the world.
/// </summary>
public static class WorldWeapons
{
    private static readonly Dictionary<string, ModelDef?> _defs = new();

    /// <summary>IconSubjects builds weapons 4x up for thumbnails; world models are real size.</summary>
    private const float Scale = 0.25f;

    public static ModelDef? Def(SandboxGame g, string weaponId)
    {
        if (_defs.TryGetValue(weaponId, out ModelDef? d)) return d;
        string? vm = g.Weapons.All.FirstOrDefault(w => w.Id == weaponId)?.ViewModelId;
        try { d = IconSubjects.Weapon(weaponId, vm); } catch { d = null; }
        return _defs[weaponId] = d;
    }

    /// <summary>Builds the model as a child of <paramref name="parent"/>; returns null if the weapon has no model.</summary>
    public static GameObject? Attach(SandboxGame g, GameObject parent, string weaponId, Float3 localPos, Float3 localEuler)
    {
        if (Application.IsHeadless) return null;
        if (EntityLibrary.Find("Weapon", weaponId) is { } prefab && parent.Scene.IsValid())
        {
            // Baked prefab (editable in the editor): its root holds the "WorldWeapon" model built below.
            GameObject inst = GameObject.Instantiate(prefab, parent.Scene, Float3.Zero, Quaternion.Identity);
            GameObject? model = inst.Children.FirstOrDefault(c => c.Name == "WorldWeapon");
            if (model.IsValid())
            {
                model!.SetParent(parent, false);
                model.Transform.LocalPosition = localPos;
                model.Transform.LocalEulerAngles = localEuler;
                model.Transform.LocalScale = new Float3(Scale) / parent.Transform.LocalScale;
                inst.Destroy();
                return model;
            }
            inst.Destroy();
        }
        if (Def(g, weaponId) is not { } def) return null;
        var go = new GameObject("WorldWeapon");
        go.Transform.Parent = parent.Transform;
        go.Transform.LocalPosition = localPos;
        go.Transform.LocalEulerAngles = localEuler;
        go.Transform.LocalScale = new Float3(Scale) / parent.Transform.LocalScale;
        ModelProps.BuildParts(go, def, g.Shader, Mats.New(g.Shader));
        return go;
    }

    /// <summary>Real-world size of the model's bounds.</summary>
    public static Float3 Size(SandboxGame g, string weaponId) => Def(g, weaponId) is { } d ? d.Collider * Scale : Float3.Zero;
    /// <summary>Measures the gun's real mesh bounds in crate space, centres it on the lid, sits it on top and shrinks long ones (RPG) to fit.</summary>
    public static void FitOnLid(GameObject crate, GameObject gun, Float3 cs)
    {
        Float3 lo = new(1e9f), hi = new(-1e9f);
        foreach (var mr in gun.GetComponentsInChildren<MeshRenderer>())
            if (mr.Mesh?.Vertices is { } vs)
                for (int i = 0; i < vs.Length; i++)
                {
                    Float3 q = crate.Transform.InverseTransformPoint(mr.GameObject.Transform.TransformPoint(vs[i]));
                    lo = new Float3(MathF.Min(lo.X, q.X), MathF.Min(lo.Y, q.Y), MathF.Min(lo.Z, q.Z)); hi = new Float3(MathF.Max(hi.X, q.X), MathF.Max(hi.Y, q.Y), MathF.Max(hi.Z, q.Z));
                }
        if (hi.X < lo.X) return;
        if (crate.GetComponent<MeshRenderer>()?.Mesh?.Vertices is { Length: > 0 } cv)
        {   // the crate's own mesh extent in its local space (props may be scaled)
            Float3 a = new(1e9f), b = new(-1e9f);
            foreach (var q in cv) { a = new Float3(MathF.Min(a.X, q.X), MathF.Min(a.Y, q.Y), MathF.Min(a.Z, q.Z)); b = new Float3(MathF.Max(b.X, q.X), MathF.Max(b.Y, q.Y), MathF.Max(b.Z, q.Z)); }
            cs = b - a;
        }
        Float3 o = gun.Transform.LocalPosition, c = (lo + hi) * 0.5f;
        // Diagonal lie: the long axis spans both X and Z, so fit each against the lid with a small margin.
        float f = MathF.Min(1f, MathF.Min(cs.X * 0.82f / MathF.Max(1e-3f, hi.X - lo.X), cs.Z * 0.82f / MathF.Max(1e-3f, hi.Z - lo.Z)));
        Float3 cn = o + (c - o) * f; float bottom = o.Y + (lo.Y - o.Y) * f;
        gun.Transform.LocalScale *= f;
        gun.Transform.LocalPosition = new Float3(o.X - cn.X, o.Y + (cs.Y / 2f + 0.004f - bottom), o.Z - cn.Z);
    }

}
