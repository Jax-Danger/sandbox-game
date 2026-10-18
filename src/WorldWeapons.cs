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
        if (Application.IsHeadless || Def(g, weaponId) is not { } def) return null;
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
}
