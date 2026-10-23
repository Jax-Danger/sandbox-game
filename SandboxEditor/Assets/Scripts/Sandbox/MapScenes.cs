#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public static partial class MapBuilder
{
    /// <summary>Baked map scenes by map id (filled from MapLibrary by SandboxHost).</summary>
    public static readonly Dictionary<string, SceneAsset> BakedScenes = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>True when the current map came from a baked scene.</summary>
    public static bool FromScene { get; private set; }

    private static bool TryLoadBaked(Scene scene, string map)
    {
        FromScene = false;
        if (Application.IsHeadless || Environment.GetEnvironmentVariable("SANDBOX_MAPSCENES") == "0") return false;
        if (!BakedScenes.TryGetValue(map, out var asset) || !asset.IsValid()) return false;
        Scene tmp;
        try
        {
            using (var pre = AssetDatabase.Preload(asset)) pre.Wait();
            tmp = Scene.Instantiate(asset);
        }
        catch (Exception e) { Console.WriteLine($"[map] baked scene for '{map}' failed, building in code: {e.Message}"); return false; }
        MapInfo? info = null;
        foreach (GameObject root in tmp.RootObjects.ToList())
        {
            // The game has its own sun; the bake's light and spawn marker are editor helpers.
            if (root.Name is "Directional Light" or "SpawnPoint") continue;
            info ??= root.GetComponent<MapInfo>();
            Add(scene, root);
        }
        tmp.Dispose();
        FromScene = true;
        if (info is null) return true;
        SpawnPoint = info.Spawn;
        SpawnPoints.AddRange(info.Spawns);
        Town.Waypoints.AddRange(info.Waypoints);
        Town.CellCenter = info.CellCenter; Town.CellHalf = info.CellHalf; Town.ReleasePoint = info.ReleasePoint;
        for (int i = 0; i < Math.Min(info.WaterCenters.Count, info.WaterHalves.Count); i++) Water.Add(info.WaterCenters[i], info.WaterHalves[i]);
        for (int i = 0; i < Math.Min(info.Interactive.Count, info.InteractiveSizes.Count); i++)
        {
            GameObject go = info.Interactive[i];
            if (go.IsNotValid()) continue;
            Town.Interactive.Add((go, info.InteractiveSizes[i]));
            if (go.GetComponent<Door>() is { } d) { Town.Doors.Add(d); d.Pose(); }
            if (go.GetComponent<AtmMachine>() is not null) Town.Atm = go;
            if (go.GetComponent<LawsBoard>() is not null) Town.Board = go;
        }
        // Lamp glass shares a baked material asset; give each its own copy so the dusk glow never edits the asset.
        var copies = new Dictionary<Material, Material>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < info.LampGlass.Count; i++)
        {
            MeshRenderer r = info.LampGlass[i];
            if (r.IsNotValid() || r.Material is not { } m) continue;
            if (!copies.TryGetValue(m, out var copy))
                copies[m] = copy = Prowl.Echo.Serializer.Deserialize<Material>(Prowl.Echo.Serializer.Serialize(typeof(object), m))!;
            r.Material = copy;
            Light? l = i < info.LampLights.Count && info.LampLights[i].IsValid() ? info.LampLights[i] : null;
            Town.NightLamps.Add((copy, l));
        }
        return true;
    }

    /// <summary>Editor play stop: forgets the map statics that point into the stopped session's world.</summary>
    public static void ReleaseSession()
    {
        _objects.Clear(); _batch.Clear(); SpawnPoints.Clear();
        Town.Reset(); Water.Clear(); Ragdoll.All.Clear();
        BakedScenes.Clear();
    }

#if PROWL_EDITOR
    /// <summary>Editor bake: records the code-built map's gameplay data on a MapInfo GameObject.</summary>
    internal static MapInfo CaptureInfo(Scene scene, string map)
    {
        var go = new GameObject("MapInfo");
        var info = go.AddComponent<MapInfo>();
        info.MapId = map; info.Spawn = SpawnPoint; info.Spawns = [.. SpawnPoints]; info.Waypoints = [.. Town.Waypoints];
        info.CellCenter = Town.CellCenter; info.CellHalf = Town.CellHalf; info.ReleasePoint = Town.ReleasePoint;
        foreach (var v in Water.Volumes) { info.WaterCenters.Add(v.Center); info.WaterHalves.Add(v.Half); }
        foreach (var (g, size) in Town.Interactive) { info.Interactive.Add(g); info.InteractiveSizes.Add(size); }
        var byMat = scene.AllObjects.SelectMany(o => o.GetComponents<MeshRenderer>()).Where(r => r.Material is not null).ToList();
        foreach (var (glass, light) in Town.NightLamps)
            foreach (var r in byMat.Where(r => ReferenceEquals(r.Material, glass)))
            { info.LampGlass.Add(r); info.LampLights.Add(light!); }
        scene.Add(go);
        return info;
    }
#endif
}
