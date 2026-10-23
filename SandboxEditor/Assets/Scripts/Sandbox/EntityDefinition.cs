using Prowl.Runtime;

namespace Sandbox;

/// <summary>Stage 4b: tags a baked entity prefab (vehicle, NPC, ragdoll, weapon) with what the game calls it.</summary>
public sealed class EntityDefinition : Component
{
    /// <summary>Vehicle, Npc, Ragdoll or Weapon.</summary>
    [Tooltip("Vehicle, Npc, Ragdoll or Weapon.")] public string Category = "";
    /// <summary>Game id: vehicle kind (buggy, jeep, airboat), NPC team, or weapon id.</summary>
    [Tooltip("Game id: buggy/jeep/airboat, NPC team, or weapon id.")] public string Id = "";
}

/// <summary>Put next to SandboxHost: entity prefabs (Assets/Prefabs/{Vehicles,Npcs,Ragdolls,Weapons}) the game may use
/// instead of building them in code. Today weapon world models (crates, holstered pistols) come from here.</summary>
public sealed class EntityLibrary : Component
{
    [Tooltip("Entity prefabs registered at play start by their EntityDefinition (Category, Id).")] public List<Prowl.Runtime.Resources.PrefabAsset> Entities = [];

    /// <summary>Prefabs by (Category, Id), filled by SandboxHost from the EntityDefinition on each prefab.</summary>
    public static readonly Dictionary<(string Category, string Id), Prowl.Runtime.Resources.PrefabAsset> Registry = [];
    /// <summary>Off while EntityBaker builds the prefabs from code.</summary>
    public static bool RegistryEnabled = true;
    public static Prowl.Runtime.Resources.PrefabAsset? Find(string category, string id) => RegistryEnabled && Registry.TryGetValue((category, id), out var p) ? p : null;

    /// <summary>Logic, physics and save data stay code-built (save format unchanged); the look comes from the prefab.
    /// Walks the code-built hierarchy and the prefab's in parallel (by name and sibling order) and copies each renderer's
    /// mesh and the local transform, plus the material where <paramref name="materials"/> allows (vehicle paint and NPC
    /// clothes stay per-instance). Returns how many renderers took the prefab's look; 0 = no prefab / headless.</summary>
    public static int ApplyLook(Prowl.Runtime.Resources.Scene scene, IReadOnlyList<GameObject> codeRoots, string category, string id, Func<GameObject, bool>? materials = null)
    {
        if (Application.IsHeadless || Find(category, id) is not { } prefab || !scene.IsValid()) return 0;
        // Instantiated into a private, never-simulated scene so wheel colliders/bodies of the prefab stay inert.
        if (!_lookScene.IsValid()) _lookScene = new Prowl.Runtime.Resources.Scene();
        GameObject inst = GameObject.Instantiate(prefab, _lookScene!, new Prowl.Vector.Float3(0f, -500f, 0f), Prowl.Vector.Quaternion.Identity);
        int n = 0;
        try
        {
            IReadOnlyList<GameObject> srcRoots = codeRoots.Count == 1 ? [inst] : inst.Children.ToList();
            for (int i = 0; i < codeRoots.Count && i < srcRoots.Count; i++) n += Copy(srcRoots[i], codeRoots[i], true, materials);
        }
        finally { inst.Destroy(); }
        return n;
    }

    private static Prowl.Runtime.Resources.Scene? _lookScene;
    /// <summary>Called by MapBuilder.ReleaseSession on Stop.</summary>
    public static void ReleaseLookScene() { if (_lookScene.IsValid()) _lookScene!.Dispose(); _lookScene = null; }

    private static int Copy(GameObject src, GameObject dst, bool root, Func<GameObject, bool>? materials)
    {
        int n = 0;
        if (!root) { dst.Transform.LocalPosition = src.Transform.LocalPosition; dst.Transform.LocalRotation = src.Transform.LocalRotation; dst.Transform.LocalScale = src.Transform.LocalScale; }
        if (src.GetComponent<MeshRenderer>() is { } sr && dst.GetComponent<MeshRenderer>() is { } dr && sr.Mesh is { } m)
        {
            dr.Mesh = m;
            if (materials?.Invoke(dst) == true && sr.Material is { } mat) dr.Material = mat;
            n++;
        }
        var used = new Dictionary<string, int>();
        foreach (GameObject dc in dst.Children.ToList())
        {
            int k = used.GetValueOrDefault(dc.Name); used[dc.Name] = k + 1;
            GameObject? sc = src.Children.Where(c => c.Name == dc.Name).Skip(k).FirstOrDefault();
            if (sc is not null) n += Copy(sc, dc, false, materials);
        }
        return n;
    }
}
