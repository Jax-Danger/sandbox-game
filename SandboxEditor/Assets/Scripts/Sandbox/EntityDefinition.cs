using Prowl.Runtime;

namespace Sandbox;

/// <summary>Stage 4b: tags a baked entity prefab (vehicle, NPC, ragdoll, weapon) with what the game calls it.</summary>
public sealed class EntityDefinition : Component
{
    /// <summary>Vehicle, Npc, Ragdoll or Weapon.</summary>
    public string Category = "";
    /// <summary>Game id: vehicle kind (buggy, jeep, airboat), NPC team, or weapon id.</summary>
    public string Id = "";
}

/// <summary>Put next to SandboxHost: entity prefabs (Assets/Prefabs/{Vehicles,Npcs,Ragdolls,Weapons}) the game may use
/// instead of building them in code. Today weapon world models (crates, holstered pistols) come from here.</summary>
public sealed class EntityLibrary : Component
{
    public List<Prowl.Runtime.Resources.PrefabAsset> Entities = [];

    /// <summary>Prefabs by (Category, Id), filled by SandboxHost from the EntityDefinition on each prefab.</summary>
    public static readonly Dictionary<(string Category, string Id), Prowl.Runtime.Resources.PrefabAsset> Registry = [];
    /// <summary>Off while EntityBaker builds the prefabs from code.</summary>
    public static bool Enabled = true;
    public static Prowl.Runtime.Resources.PrefabAsset? Find(string category, string id) => Enabled && Registry.TryGetValue((category, id), out var p) ? p : null;
}
