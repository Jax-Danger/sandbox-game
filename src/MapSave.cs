using System.Text.Json;
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Saves and loads spawned props, colours, frozen state, welds and balloons as JSON.</summary>
public static class MapSave
{
    public sealed class PropData
    {
        public string Kind { get; set; } = "";
        public float[] Position { get; set; } = [];
        public float[] Rotation { get; set; } = [];
        public float[] Color { get; set; } = [];
        public bool Frozen { get; set; }
        public int Balloons { get; set; }
        public float BalloonForce { get; set; } = 1.45f;
    }

    public sealed class SaveData
    {
    public List<float[]> Ragdolls { get; set; } = [];
        public int Version { get; set; } = 1;
        public List<PropData> Props { get; set; } = [];
        public List<int[]> Welds { get; set; } = [];
    }

    public static string DefaultPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProwlSandbox", "saves", "quicksave.json");

    public static int Save(PropManager props, string path)
    {
        var data = new SaveData();
        List<Prop> list = props.Props.Where(p => p.Kind != PropKind.Limb).ToList();
        foreach (Ragdoll rd in Ragdoll.All.Where(r => r.Alive))
        {
            Float3 pp = rd.Pelvis.Go.Transform.Position;
            data.Ragdolls.Add([pp.X, pp.Y - 1f, pp.Z]);
        }
        foreach (Prop p in list)
        {
            Float3 pos = p.Go.Transform.Position;
            Quaternion rot = p.Go.Transform.Rotation;
            Color c = p.BaseColor;
            Balloon[] balloons = p.Go.GetComponents<Balloon>().ToArray();
            data.Props.Add(new PropData
            {
                Kind = p.Kind.ToString(),
                Position = [pos.X, pos.Y, pos.Z],
                Rotation = [rot.X, rot.Y, rot.Z, rot.W],
                Color = [c.R, c.G, c.B, c.A],
                Frozen = p.Frozen,
                Balloons = balloons.Length,
                BalloonForce = balloons.Length > 0 ? balloons[0].Lift : 1.45f,
            });
        }
        for (int i = 0; i < list.Count; i++)
            foreach (FixedAngleConstraint fa in list[i].Go.GetComponents<FixedAngleConstraint>())
            {
                int j = list.FindIndex(p => ReferenceEquals(p.Body, fa.ConnectedBody));
                if (j >= 0) data.Welds.Add([i, j]);
            }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        return data.Props.Count;
    }

    public static int Load(SandboxGame g, string path)
    {
        SaveData data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path)) ?? new SaveData();
        g.Props.ClearAll();
        var spawned = new List<Prop>();
        foreach (PropData d in data.Props)
        {
            if (!Enum.TryParse(d.Kind, out PropKind kind)) continue;
            Prop p = g.Props.Spawn(kind, new Float3(d.Position[0], d.Position[1], d.Position[2]), 0f, recordUndo: false);
            p.Go.Transform.Rotation = new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]);
            if (d.Color.Length == 4) p.SetColor(new Color(d.Color[0], d.Color[1], d.Color[2], d.Color[3]));
            if (d.Frozen) p.SetFrozen(true);
            for (int b = 0; b < d.Balloons; b++) Balloon.Attach(g.Props, p, d.BalloonForce);
            spawned.Add(p);
        }
        foreach (int[] w in data.Welds)
            if (w.Length == 2 && w[0] < spawned.Count && w[1] < spawned.Count)
                g.ToolGun.Weld(spawned[w[0]], spawned[w[1]], recordUndo: false);
        foreach (float[] r in data.Ragdolls)
            if (r.Length == 3) Ragdoll.Spawn(g, new Float3(r[0], MathF.Max(r[1], 0.05f), r[2]), 0f, recordUndo: false);
        return spawned.Count;
    }
}
