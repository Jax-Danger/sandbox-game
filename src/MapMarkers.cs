using System.Text.RegularExpressions;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Data-driven map hooks. A custom map (glTF/FBX/OBJ exported from Blender) marks gameplay positions with named
/// empties (or throwaway meshes): spawn points, the jail cell and release point, ATMs, the laws board, buyable /
/// police / city doors, NPC sidewalk paths and water volumes. See docs/MAPS.md for the naming reference.
/// </summary>
public static partial class MapBuilder
{
    /// <summary>All spawn points for the current map (the first is <see cref="SpawnPoint"/>); respawns pick one at random.</summary>
    public static List<Float3> SpawnPoints { get; } = [];
    private static readonly Random _spawnRng = new();

    public static Float3 NextSpawn() => SpawnPoints.Count > 1 ? SpawnPoints[_spawnRng.Next(SpawnPoints.Count)] : SpawnPoint;

    public sealed record MarkerReport(int Spawns, bool Jail, int Atms, bool Board, int Doors, int Path, int Water)
    {
        public override string ToString() => $"{Spawns} spawns, jail {(Jail ? "yes" : "no")}, {Atms} ATMs, board {(Board ? "yes" : "no")}, {Doors} doors, {Path} path points, {Water} water";
    }

    /// <summary>Strips Blender's ".001" duplicate suffix (exporters may turn it into "_001") and lower-cases.</summary>
    public static string MarkerName(string name)
    {
        string n = name.Trim().ToLowerInvariant();
        n = Regex.Replace(n, @"[._]\d{3}$", "");
        return n;
    }

    public static bool IsMarker(string name)
    {
        string n = MarkerName(name);
        return n is "spawn" or "jail_cell" or "jail_release" or "atm" or "laws_board" or "water"
            || n.StartsWith("spawn_") || n.StartsWith("atm_") || n.StartsWith("door_") || n.StartsWith("npc_path") || n.StartsWith("water_");
    }

    private static Float3 WorldScale(GameObject go)
    {
        Transform t = go.Transform;
        Float3 o = t.TransformPoint(Float3.Zero);
        return new Float3(Float3.Length(t.TransformPoint(Float3.UnitX) - o), Float3.Length(t.TransformPoint(Float3.UnitY) - o), Float3.Length(t.TransformPoint(Float3.UnitZ) - o));
    }

    private static float WorldYaw(GameObject go)
    {
        Float3 f = go.Transform.Forward;
        return MathF.Atan2(f.X, f.Z) * 180f / MathF.PI;
    }

    /// <summary>Turns every marker object into gameplay data (and hides it). Safe to call with any object list.</summary>
    public static MarkerReport ApplyMarkers(Scene scene, Shader shader, IEnumerable<GameObject> objects)
    {
        int spawns = 0, atms = 0, doors = 0, path = 0, water = 0; bool jail = false, board = false, release = false;
        var list = objects.Where(o => IsMarker(o.Name)).ToList();
        foreach (GameObject go in list)
        {
            string n = MarkerName(go.Name);
            Float3 p = go.Transform.Position, s = WorldScale(go);
            float yaw = WorldYaw(go);
            // Markers are invisible and never collide.
            if (go.GetComponent<MeshRenderer>() is { } mr) mr.Enabled = false;
            if (go.GetComponent<MeshCollider>() is { } mc) mc.Enabled = false;

            if (n == "spawn" || n.StartsWith("spawn_"))
            {
                if (spawns == 0) { SpawnPoints.Clear(); SpawnPoint = p + new Float3(0f, 0.1f, 0f); }
                SpawnPoints.Add(p + new Float3(0f, 0.1f, 0f));
                spawns++;
            }
            else if (n == "jail_cell")
            {
                Town.CellCenter = p;
                Town.CellHalf = new Float3(Near1(s.X) ? 2.4f : s.X, 0f, Near1(s.Z) ? 2.4f : s.Z);
                jail = true;
            }
            else if (n == "jail_release") { Town.ReleasePoint = p + new Float3(0f, 0.2f, 0f); release = true; }
            else if (n == "atm" || n.StartsWith("atm_")) { PlaceAtm(scene, shader, p, yaw); atms++; }
            else if (n == "laws_board") { PlaceBoard(scene, shader, p, yaw); board = true; }
            else if (n.StartsWith("door_"))
            {
                // door_<id>, door_<id>_p<price>, door_police_<id>, door_city_<id>, door_cell
                string id = n;
                int price = 250;
                if (Regex.Match(id, @"_p(\d+)$") is { Success: true } m) { price = int.Parse(m.Groups[1].Value); id = id[..m.Index]; }
                string owner = id == "door_cell" || id.StartsWith("door_police") ? "police" : id.StartsWith("door_city") ? "city" : "";
                if (owner != "") price = 0;
                string title = id == "door_cell" ? "Jail Cell" : Title(id["door_".Length..].Replace("police_", "").Replace("city_", ""));
                float width = Near1(s.X) ? 1.3f : s.X, height = Near1(s.Y) ? 2.45f : s.Y;
                MakeDoor(scene, shader, id, title, p, yaw, width, height, new Color(0.42f, 0.28f, 0.18f, 1f), owner, price, locked: id == "door_cell");
                doors++;
            }
            else if (n.StartsWith("npc_path")) { Town.Waypoints.Add(new Float3(p.X, 0f, p.Z)); path++; }
            else if (n == "water" || n.StartsWith("water_"))
            {
                Float3 half = new(Near1(s.X) ? 5f : s.X, Near1(s.Y) ? 2f : s.Y, Near1(s.Z) ? 5f : s.Z);
                Water.Add(p, half);
                water++;
            }
        }
        if (jail && !release) Town.ReleasePoint = Town.CellCenter + new Float3(0f, 0.2f, -(Town.CellHalf.Z + 3f));
        if (jail || atms > 0 || board || doors > 0 || path > 0) Town.Markers = true;
        return new MarkerReport(spawns, jail, atms, board, doors, path, water);
    }

    private static bool Near1(float v) => MathF.Abs(v - 1f) < 1e-3f;
    private static string Title(string id) => string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private static Float3 Rot(Float3 local, float yaw)
    {
        float r = yaw * MathF.PI / 180f;
        return new Float3(local.X * MathF.Cos(r) + local.Z * MathF.Sin(r), local.Y, -local.X * MathF.Sin(r) + local.Z * MathF.Cos(r));
    }

    /// <summary>ATM placeholder at a floor point; its screen faces the marker's forward (+Z) axis.</summary>
    public static GameObject PlaceAtm(Scene scene, Shader shader, Float3 p, float yaw)
    {
        Float3 e = new(0f, yaw, 0f);
        GameObject atm = Box(scene, shader, "ATM", p + new Float3(0f, 1f, 0f), new Float3(0.9f, 2.0f, 0.8f), e, new Color(0.24f, 0.30f, 0.40f, 1f));
        atm.AddComponent<AtmMachine>();
        Town.Interactive.Add((atm, new Float3(0.9f, 2.0f, 0.9f)));
        Town.Atm ??= atm;
        Box(scene, shader, "~ATM screen", p + Rot(new Float3(0f, 1.35f, 0.42f), yaw), new Float3(0.6f, 0.45f, 0.04f), e, new Color(0.35f, 0.85f, 0.65f, 1f), collide: false);
        Box(scene, shader, "~ATM sign", p + Rot(new Float3(0f, 1.86f, 0.42f), yaw), new Float3(0.7f, 0.2f, 0.04f), e, new Color(0.30f, 0.65f, 0.95f, 1f), collide: false);
        return atm;
    }

    /// <summary>Laws board placeholder standing on a floor point, face towards the marker's forward (+Z) axis.</summary>
    public static GameObject PlaceBoard(Scene scene, Shader shader, Float3 p, float yaw)
    {
        Float3 e = new(0f, yaw, 0f);
        Box(scene, shader, "Board posts", p + new Float3(0f, 1.1f, 0f), new Float3(4.4f, 2.2f, 0.18f), e, new Color(0.30f, 0.22f, 0.15f, 1f));
        GameObject board = Box(scene, shader, "~Laws board", p + Rot(new Float3(0f, 2.0f, 0.12f), yaw), new Float3(4f, 2.2f, 0.06f), e, new Color(0.10f, 0.13f, 0.17f, 1f));
        board.AddComponent<LawsBoard>();
        Town.Interactive.Add((board, new Float3(4f, 2.2f, 0.06f)));
        Town.Board = board;
        Box(scene, shader, "~Board header", p + Rot(new Float3(0f, 3.25f, 0.14f), yaw), new Float3(4f, 0.3f, 0.06f), e, new Color(0.78f, 0.22f, 0.22f, 1f), collide: false);
        return board;
    }
}
