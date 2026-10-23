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
