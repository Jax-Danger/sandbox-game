using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Stage 5b: the gameplay data a baked map scene carries (written by SceneBaker, editable in the inspector).
/// When the game loads a baked map, <see cref="MapBuilder"/> reads this instead of re-running the code that built
/// the map: spawn points, NPC waypoints, the jail, water volumes, interactive pieces and night-lit lamps.
/// </summary>
public sealed class MapInfo : Component
{
    /// <summary>Map id (flatgrass, hills, town) this scene replaces.</summary>
    public string MapId = "flatgrass";
    public Float3 Spawn = new(0f, 0.1f, -10f);
    public List<Float3> Spawns = [];
    /// <summary>Sidewalk points NPCs walk between (town).</summary>
    public List<Float3> Waypoints = [];
    public Float3 CellCenter, CellHalf, ReleasePoint;
    /// <summary>Water volumes as center / half-extent pairs (same index).</summary>
    public List<Float3> WaterCenters = [], WaterHalves = [];
    /// <summary>Objects the player can use (doors, ATM, laws board) with their box sizes (same index).</summary>
    public List<GameObject> Interactive = [];
    public List<Float3> InteractiveSizes = [];
    /// <summary>Glass that glows after dusk; LampLights holds the matching point light or null (same index).</summary>
    public List<MeshRenderer> LampGlass = [];
    public List<Light> LampLights = [];
}

/// <summary>One baked map: the map id and its scene.</summary>
public sealed class MapEntry
{
    public string Id = "";
    public SceneAsset? Scene;
}

/// <summary>Put next to SandboxHost: the baked map scenes the game loads instead of building maps in code.
/// Maps with no (valid) scene here, and the headless self-test, fall back to the code-built map.</summary>
public sealed class MapLibrary : Component
{
    public List<MapEntry> Maps = [];
}
