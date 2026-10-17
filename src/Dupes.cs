using System.Text.Json;
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Contraption copies ("dupes"): the Duplicator copies everything constrained to the prop you aim at,
/// pastes it where you aim, and the Dupes tab saves/loads them as files under saves/dupes.
/// </summary>
public static class Dupes
{
    public sealed class DupeFile
    {
        public string Name { get; set; } = "";
        public DateTime Created { get; set; } = DateTime.Now;
        public int Props { get; set; }
        public MapSave.SaveData Data { get; set; } = new();
    }

    public static string Dir => Path.Combine(Worlds.Root, "dupes");

    /// <summary>Every prop linked to <paramref name="root"/> by a constraint, rope, elastic or no-collide.</summary>
    public static List<Prop> Contraption(SandboxGame g, Prop root)
    {
        var all = g.Props.Props.ToList();
        var edges = new Dictionary<Prop, HashSet<Prop>>();
        void Link(Prop a, Prop? b) { if (b is null || ReferenceEquals(a, b)) return; (edges.TryGetValue(a, out var s) ? s : edges[a] = []).Add(b); (edges.TryGetValue(b, out var t) ? t : edges[b] = []).Add(a); }
        Prop? Of(Rigidbody3D? b) => b is null ? null : all.FirstOrDefault(p => ReferenceEquals(p.Body, b));
        foreach (Prop p in all)
        {
            foreach (PhysicsConstraint c in p.Go.GetComponents<PhysicsConstraint>()) Link(p, Of(c.ConnectedBody));
            foreach (NoCollideLink n in p.Go.GetComponents<NoCollideLink>()) Link(p, n.Other);
            foreach (ElasticBand e in p.Go.GetComponents<ElasticBand>()) Link(p, e.Other);
            foreach (RopeVisual r in p.Go.GetComponents<RopeVisual>()) Link(p, r.Other);
        }
        var seen = new HashSet<Prop> { root };
        var q = new Queue<Prop>([root]);
        while (q.Count > 0)
            if (edges.TryGetValue(q.Dequeue(), out var n))
                foreach (Prop x in n) if (seen.Add(x)) q.Enqueue(x);
        return all.Where(seen.Contains).ToList();
    }

    public static MapSave.SaveData Copy(SandboxGame g, Prop root)
    {
        MapSave.SaveData d = MapSave.Capture(g, Contraption(g, root));
        // Store relative to the contraption's bottom-centre so pastes land on the aim point.
        Float3 min = new(float.MaxValue), max = new(float.MinValue);
        foreach (var p in d.Props) { Float3 v = new(p.Position[0], p.Position[1], p.Position[2]); min = new Float3(MathF.Min(min.X, v.X), MathF.Min(min.Y, v.Y), MathF.Min(min.Z, v.Z)); max = new Float3(MathF.Max(max.X, v.X), MathF.Max(max.Y, v.Y), MathF.Max(max.Z, v.Z)); }
        float bottom = Contraption(g, root).Min(p => p.Go.Transform.Position.Y - PropManager.HalfHeightOf(p.Kind));
        return Moved(d, new Float3((min.X + max.X) / 2f, bottom, (min.Z + max.Z) / 2f), Float3.Zero, 0f);
    }

    /// <summary>A copy of <paramref name="d"/> translated from <paramref name="from"/> to <paramref name="to"/> and spun by yaw.</summary>
    public static MapSave.SaveData Moved(MapSave.SaveData d, Float3 from, Float3 to, float yawDeg)
    {
        var c = JsonSerializer.Deserialize<MapSave.SaveData>(JsonSerializer.Serialize(d))!;
        Quaternion r = Quaternion.FromEuler(new Float3(0f, yawDeg * MathF.PI / 180f, 0f));
        foreach (var p in c.Props)
        {
            Float3 v = new Float3(p.Position[0], p.Position[1], p.Position[2]) - from;
            Float3 w = to + r * v;
            p.Position = [w.X, w.Y, w.Z];
            if (p.Rotation.Length == 4)
            {
                Quaternion q = r * new Quaternion(p.Rotation[0], p.Rotation[1], p.Rotation[2], p.Rotation[3]);
                p.Rotation = [q.X, q.Y, q.Z, q.W];
            }
            p.Velocity = []; p.AngularVelocity = [];
        }
        foreach (var l in c.Links.Where(l => l.Type == "slider" && l.Axis.Length == 3))
        {
            Float3 a = r * new Float3(l.Axis[0], l.Axis[1], l.Axis[2]);
            l.Axis = [a.X, a.Y, a.Z];
        }
        foreach (var gd in c.Gadgets.Where(x => x.Type == "hoverball")) gd.Target += to.Y - from.Y;
        return c;
    }

    /// <summary>Spawns a dupe at <paramref name="at"/>; returns every new prop (one undo removes them all).</summary>
    public static List<Prop> Paste(SandboxGame g, MapSave.SaveData dupe, Float3 at, float yawDeg, bool recordUndo = true)
    {
        var before = g.Props.Props.ToHashSet();
        foreach (int _ in MapSave.LoadSteps(g, Moved(dupe, Float3.Zero, at + new Float3(0f, 0.05f, 0f), yawDeg))) { }
        List<Prop> made = g.Props.Props.Where(p => !before.Contains(p)).ToList();
        if (recordUndo && made.Count > 0)
            g.Props.PushUndo(new ActionUndo($"Undone: dupe ({made.Count} props)", () => { foreach (Prop p in made) g.Props.Remove(p); }));
        return made;
    }

    public static string Save(string name, MapSave.SaveData d)
    {
        Directory.CreateDirectory(Dir);
        string safe = string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        if (safe.Length == 0) safe = "dupe";
        string path = Path.Combine(Dir, safe + ".dupe.json");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(Dir, $"{safe}_{i}.dupe.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new DupeFile { Name = name, Props = d.Props.Count, Data = d }));
        return path;
    }

    public static List<(string Path, DupeFile File)> List()
    {
        var list = new List<(string, DupeFile)>();
        if (!Directory.Exists(Dir)) return list;
        foreach (string f in Directory.GetFiles(Dir, "*.dupe.json"))
            try { if (JsonSerializer.Deserialize<DupeFile>(File.ReadAllText(f)) is { } d) list.Add((f, d)); } catch { }
        return list.OrderByDescending(x => x.Item2.Created).ToList();
    }
}

/// <summary>Player-made spawnlists (name → spawn entry ids), saved as JSON next to the worlds.</summary>
public sealed class UserSpawnlists
{
    public string Path { get; }
    public Dictionary<string, List<string>> Lists { get; private set; } = [];

    public UserSpawnlists(string path)
    {
        Path = path;
        try { if (File.Exists(path)) Lists = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path)) ?? []; } catch { Lists = []; }
    }

    public string Create()
    {
        string n = Lists.Count == 0 ? "My Spawnlist" : $"Spawnlist {Lists.Count + 1}";
        while (Lists.ContainsKey(n)) n += "+";
        Lists[n] = [];
        Save();
        return n;
    }

    public void Add(string list, string id)
    {
        if (!Lists.TryGetValue(list, out var l)) Lists[list] = l = [];
        if (!l.Contains(id)) l.Add(id);
        Save();
    }

    public void Remove(string list, string id) { if (Lists.TryGetValue(list, out var l) && l.Remove(id)) Save(); }

    private void Save()
    {
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!); File.WriteAllText(Path, JsonSerializer.Serialize(Lists)); } catch { }
    }
}
