using System.Text.Json;
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Saves and loads the whole sandbox as JSON: props (colour, material, mass, frozen, entity type), welds, balloons,
/// ropes, thrusters, wheels, lights, ragdolls / NPC dummies and buggies.
/// </summary>
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
        public string? Surface { get; set; }
        public float Mass { get; set; }
        public string? Entity { get; set; }
        public string? Key { get; set; }
        public float[] Velocity { get; set; } = [];
        public float[] AngularVelocity { get; set; } = [];
    }

    public sealed class LinkData { public string Type { get; set; } = ""; public int A { get; set; } public int B { get; set; } public float[] LocalA { get; set; } = []; public float[] LocalB { get; set; } = []; public float[] Axis { get; set; } = []; public float Value { get; set; } public float Value2 { get; set; } }
    public sealed class GadgetData { public string Type { get; set; } = ""; public int Prop { get; set; } public float[] Point { get; set; } = []; public float[] Normal { get; set; } = []; public float Value { get; set; } public float Target { get; set; } public string Key { get; set; } = "T"; public string Key2 { get; set; } = "G"; }
    public sealed class RopeData { public int A { get; set; } public int B { get; set; } public float[] LocalA { get; set; } = []; public float[] LocalB { get; set; } = []; public float Length { get; set; } }
    public sealed class ThrusterData { public int Prop { get; set; } public float[] Point { get; set; } = []; public float[] Normal { get; set; } = []; public float Force { get; set; } public string Key { get; set; } = "T"; }
    public sealed class WheelData { public int Prop { get; set; } public float[] Point { get; set; } = []; public float[] Normal { get; set; } = []; public float Speed { get; set; } public string Fwd { get; set; } = "T"; public string Back { get; set; } = "G"; }
    public sealed class LightData { public int Prop { get; set; } public float[] Point { get; set; } = []; public float Range { get; set; } public float[] Color { get; set; } = []; }
    public sealed class RagdollData { public float[] Feet { get; set; } = []; public float Yaw { get; set; } public bool Dummy { get; set; } public string Team { get; set; } = "Dummy"; }
    public sealed class BuggyData { public float[] Position { get; set; } = []; public float Yaw { get; set; } public float[] Rotation { get; set; } = []; public float[] Velocity { get; set; } = []; public string Kind { get; set; } = "buggy"; }

    public sealed class SaveData
    {
        public int Version { get; set; } = 3;
        public List<PropData> Props { get; set; } = [];
        public List<int[]> Welds { get; set; } = [];
        public List<RopeData> Ropes { get; set; } = [];
        public List<LinkData> Links { get; set; } = [];
        public List<GadgetData> Gadgets { get; set; } = [];
        public List<int[]> BallSockets { get; set; } = [];
        public List<ThrusterData> Thrusters { get; set; } = [];
        public List<WheelData> Wheels { get; set; } = [];
        public List<LightData> Lights { get; set; } = [];
        public List<RagdollData> Ragdolls { get; set; } = [];
        public List<BuggyData> Buggies { get; set; } = [];
    }

    public static string DefaultPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProwlSandbox", "saves", "quicksave.json");

    private static float[] F(Float3 v) => [v.X, v.Y, v.Z];
    private static Float3 V(float[] a) => a.Length >= 3 ? new Float3(a[0], a[1], a[2]) : Float3.Zero;

    public static SaveData Capture(SandboxGame g)
    {
        var data = new SaveData();
        List<Prop> list = g.Props.Props.Where(p => p.Kind != PropKind.Limb).ToList();
        int Idx(Rigidbody3D? b) => b is null ? -1 : list.FindIndex(p => ReferenceEquals(p.Body, b));

        foreach (Prop p in list)
        {
            Quaternion rot = p.Go.Transform.Rotation;
            Color c = p.BaseColor;
            Balloon[] balloons = p.Go.GetComponents<Balloon>().ToArray();
            data.Props.Add(new PropData
            {
                Kind = p.Kind.ToString(), Position = F(p.Go.Transform.Position), Rotation = [rot.X, rot.Y, rot.Z, rot.W],
                Color = [c.R, c.G, c.B, c.A], Frozen = p.Frozen, Balloons = balloons.Length,
                BalloonForce = balloons.Length > 0 ? balloons[0].Lift : 1.45f, Surface = p.Surface, Mass = p.Body.Mass,
                Entity = p.Entity, Key = p.Go.GetComponent<SandboxButton>()?.Key.ToString() ?? p.EntityData,
                Velocity = p.Frozen ? [] : F(p.Body.LinearVelocity), AngularVelocity = p.Frozen ? [] : F(p.Body.AngularVelocity),
            });
        }
        for (int i = 0; i < list.Count; i++)
        {
            Prop p = list[i];
            foreach (FixedAngleConstraint fa in p.Go.GetComponents<FixedAngleConstraint>())
                if (Idx(fa.ConnectedBody) is >= 0 and var j) data.Welds.Add([i, j]);
            bool welded = p.Go.GetComponents<FixedAngleConstraint>().Any();
            if (!welded)
                foreach (BallSocketConstraint bs in p.Go.GetComponents<BallSocketConstraint>())
                    if (Idx(bs.ConnectedBody) is >= 0 and var j) data.BallSockets.Add([i, j]);
            foreach (RopeVisual rv in p.Go.GetComponents<RopeVisual>())
            {
                int j = rv.Other is null ? -1 : list.IndexOf(rv.Other);
                DistanceLimitConstraint? dl = p.Go.GetComponents<DistanceLimitConstraint>().FirstOrDefault(d => ReferenceEquals(d.ConnectedBody, rv.Other?.Body));
                if (j >= 0 && dl is not null) data.Ropes.Add(new RopeData { A = i, B = j, LocalA = F(rv.LocalA), LocalB = F(rv.LocalB), Length = dl.MaxDistance });
            }
            foreach (Thruster t in p.Go.GetComponents<Thruster>())
            {
                Float3 n = -p.Go.Transform.TransformDirection(t.LocalDir);
                Float3 pt = t.Visual is not null && t.Visual.IsValid() ? t.Visual.Transform.Position - n * 0.1f : p.Go.Transform.Position;
                data.Thrusters.Add(new ThrusterData { Prop = i, Point = F(p.Go.Transform.InverseTransformPoint(pt)), Normal = F(t.LocalDir * -1f), Force = t.Multiple, Key = t.Key.ToString() });
            }
            foreach (NoCollideLink nc in p.Go.GetComponents<NoCollideLink>())
                if (nc.Other is not null && list.IndexOf(nc.Other) is >= 0 and var j) data.Links.Add(new LinkData { Type = "nocollide", A = i, B = j });
            foreach (ElasticBand eb in p.Go.GetComponents<ElasticBand>())
                if (eb.Other is not null && list.IndexOf(eb.Other) is >= 0 and var j) data.Links.Add(new LinkData { Type = "elastic", A = i, B = j, LocalA = F(eb.LocalA), LocalB = F(eb.LocalB), Value = eb.Strength, Value2 = eb.Rest });
            foreach (PrismaticJoint pj in p.Go.GetComponents<PrismaticJoint>())
                if (Idx(pj.ConnectedBody) is >= 0 and var j) data.Links.Add(new LinkData { Type = "slider", A = i, B = j, Axis = F(p.Go.Transform.TransformDirection(pj.Axis)) });
            foreach (HingeJoint hj in p.Go.GetComponents<HingeJoint>())
                if (Idx(hj.ConnectedBody) is >= 0 and var j) data.Links.Add(new LinkData { Type = "hinge", A = i, B = j, LocalA = F(hj.Anchor), Axis = F(hj.Axis) });
            foreach (Hoverball hb in p.Go.GetComponents<Hoverball>())
                data.Gadgets.Add(new GadgetData { Type = "hoverball", Prop = i, Point = F(hb.LocalPoint), Normal = [0, 1, 0], Value = hb.Strength, Target = hb.Target, Key = hb.Up.ToString(), Key2 = hb.Down.ToString() });
            foreach (Emitter em in p.Go.GetComponents<Emitter>())
                data.Gadgets.Add(new GadgetData { Type = "emitter", Prop = i, Point = F(em.LocalPoint), Normal = F(em.LocalNormal), Value = em.Rate, Key = em.Key.ToString() });
            foreach (PointLight l in p.Go.GetComponentsInChildren<PointLight>())
                data.Lights.Add(new LightData { Prop = i, Point = F(p.Go.Transform.InverseTransformPoint(l.Transform.Position)), Range = l.Range, Color = [l.Color.R, l.Color.G, l.Color.B, 1f] });
        }
        foreach (Prop w in g.Props.Props.Where(p => p.Kind == PropKind.Limb))
            if (w.Go.GetComponent<WheelMotor>() is { Joint: { } h } m && Idx(h.ConnectedBody) is >= 0 and var pi)
            {
                Prop parent = list[pi];
                Float3 axle = w.Go.Transform.Up;
                Float3 pt = w.Go.Transform.Position - axle * (0.18f / 2f + 0.02f);
                data.Wheels.Add(new WheelData
                {
                    Prop = pi, Point = F(parent.Go.Transform.InverseTransformPoint(pt)), Normal = F(parent.Go.Transform.InverseTransformDirection(axle)),
                    Speed = m.Speed, Fwd = m.Fwd.ToString(), Back = m.Back.ToString(),
                });
            }
        foreach (Ragdoll rd in Ragdoll.All.Where(r => r.Alive))
        {
            Float3 pp = rd.Pelvis.Go.Transform.Position;
            data.Ragdolls.Add(new RagdollData { Feet = [pp.X, pp.Y - 1f, pp.Z], Yaw = rd.Pelvis.Go.Transform.LocalEulerAngles.Y, Dummy = rd.IsDummy, Team = rd.Team.ToString() });
        }
        foreach (Buggy b in g.Vehicles.All)
        {
            Quaternion q = b.Go.Transform.Rotation;
            data.Buggies.Add(new BuggyData { Position = F(b.Go.Transform.Position), Yaw = b.Go.Transform.LocalEulerAngles.Y, Rotation = [q.X, q.Y, q.Z, q.W], Velocity = F(b.Body.LinearVelocity), Kind = b.Kind });
        }
        return data;
    }

    public static int Save(SandboxGame g, string path)
    {
        SaveData data = Capture(g);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        return data.Props.Count;
    }

    public static int Load(SandboxGame g, string path)
    {
        SaveData data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path)) ?? new SaveData();
        g.ClearMap(notify: false);
        foreach (int _ in LoadSteps(g, data)) { }
        return _lastSpawned;
    }

    private static int _lastSpawned;

    /// <summary>Number of work items <see cref="LoadSteps"/> yields (for progress bars).</summary>
    public static int StepCount(SaveData d) => d.Props.Count + d.Welds.Count + d.BallSockets.Count + d.Ropes.Count + d.Links.Count + d.Gadgets.Count + d.Thrusters.Count
        + d.Wheels.Count + d.Lights.Count + d.Ragdolls.Count + d.Buggies.Count;

    /// <summary>Rebuilds the world one item at a time (the loading screen runs these in time-budgeted batches).</summary>
    public static IEnumerable<int> LoadSteps(SandboxGame g, SaveData data)
    {
        var spawned = new List<Prop?>();
        int step = 0;
        var velocities = new List<(Prop P, Float3 V, Float3 W)>();
        foreach (PropData d in data.Props)
        {
            if (!Enum.TryParse(d.Kind, out PropKind kind) || kind == PropKind.Limb) { spawned.Add(null); yield return ++step; continue; }
            Prop p = g.Props.Spawn(kind, V(d.Position), 0f, recordUndo: false);
            p.Go.Transform.Rotation = new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]);
            if (d.Entity is not null) EntityCatalog.Decorate(g, p, d.Entity, d.Key);
            if (d.Color.Length == 4) p.SetColor(new Color(d.Color[0], d.Color[1], d.Color[2], d.Color[3]));
            if (d.Surface is not null) p.SetSurface(SurfaceMaterial.Get(d.Surface));
            if (d.Mass > 0f) p.SetMass(d.Mass);
            if (d.Entity == "ent_ball") { p.Body.Restitution = 0.95f; p.Body.Friction = 0.3f; }
            if (d.Frozen) p.SetFrozen(true);
            else if (d.Velocity.Length == 3) velocities.Add((p, V(d.Velocity), V(d.AngularVelocity)));
            for (int b = 0; b < d.Balloons; b++) Balloon.Attach(g.Props, p, d.BalloonForce);
            spawned.Add(p);
            yield return ++step;
        }
        Prop? At(int i) => i >= 0 && i < spawned.Count ? spawned[i] : null;
        foreach (int[] w in data.Welds)
        {
            if (w.Length == 2 && At(w[0]) is { } a && At(w[1]) is { } b) g.ToolGun.Weld(a, b, recordUndo: false);
            yield return ++step;
        }
        foreach (int[] w in data.BallSockets)
        {
            if (w.Length == 2 && At(w[0]) is { } a && At(w[1]) is { } b)
            {
                BallSocketConstraint s = a.Go.AddComponent<BallSocketConstraint>();
                s.ConnectedBody = b.Body;
                PhysicsWorld.IgnoreCollisionBetween(a.Body, b.Body);
            }
            yield return ++step;
        }
        foreach (RopeData r in data.Ropes)
        {
            if (At(r.A) is { } a && At(r.B) is { } b) g.ToolGun.Rope(a, V(r.LocalA), b, V(r.LocalB), r.Length, recordUndo: false);
            yield return ++step;
        }
        foreach (LinkData l in data.Links)
        {
            if (At(l.A) is { } a && At(l.B) is { } b)
                switch (l.Type)
                {
                    case "nocollide": g.ToolGun.NoCollide(a, b, recordUndo: false); break;
                    case "elastic": g.ToolGun.Elastic(a, V(l.LocalA), b, V(l.LocalB), l.Value, l.Value2, recordUndo: false); break;
                    case "slider": g.ToolGun.Slider(a, b, V(l.Axis), recordUndo: false); break;
                    case "hinge": g.ToolGun.Hinge(a, V(l.LocalA), V(l.Axis), b, recordUndo: false); break;
                }
            yield return ++step;
        }
        foreach (GadgetData gd in data.Gadgets)
        {
            if (At(gd.Prop) is { } p)
            {
                Float3 pt = p.Go.Transform.TransformPoint(V(gd.Point)), n = p.Go.Transform.TransformDirection(V(gd.Normal));
                KeyCode k1 = Enum.TryParse(gd.Key, out KeyCode a1) ? a1 : KeyCode.T, k2 = Enum.TryParse(gd.Key2, out KeyCode a2) ? a2 : KeyCode.G;
                if (gd.Type == "hoverball") Hoverball.Attach(g.Props, p, pt, n, gd.Value, k1, k2).Target = gd.Target;
                else if (gd.Type == "emitter") Emitter.Attach(g.Props, p, pt, n, gd.Value, k1);
            }
            yield return ++step;
        }
        foreach (ThrusterData t in data.Thrusters)
        {
            if (At(t.Prop) is { } p)
                Thruster.Attach(g.Props, p, p.Go.Transform.TransformPoint(V(t.Point)), p.Go.Transform.TransformDirection(V(t.Normal)), t.Force,
                    Enum.TryParse(t.Key, out KeyCode k) ? k : KeyCode.T);
            yield return ++step;
        }
        foreach (WheelData w in data.Wheels)
        {
            if (At(w.Prop) is { } p)
                g.ToolGun.AttachWheel(p, p.Go.Transform.TransformPoint(V(w.Point)), Float3.Normalize(p.Go.Transform.TransformDirection(V(w.Normal))), w.Speed,
                    (Enum.TryParse(w.Fwd, out KeyCode f) ? f : KeyCode.T, Enum.TryParse(w.Back, out KeyCode bk) ? bk : KeyCode.G), recordUndo: false);
            yield return ++step;
        }
        foreach (LightData l in data.Lights)
        {
            if (At(l.Prop) is { } p)
                g.ToolGun.AttachLight(p, p.Go.Transform.TransformPoint(V(l.Point)), l.Range, l.Color.Length >= 3 ? new Color(l.Color[0], l.Color[1], l.Color[2], 1f) : new Color(1f, 1f, 1f, 1f));
            yield return ++step;
        }
        foreach (RagdollData r in data.Ragdolls)
        {
            Float3 feet = V(r.Feet);
            feet = new Float3(feet.X, MathF.Max(feet.Y, MapBuilder.Current == "flatgrass" ? 0.05f : feet.Y), feet.Z);
            if (r.Dummy) EntityCatalog.SpawnDummy(g, feet, r.Yaw, recordUndo: false, team: Enum.TryParse(r.Team, out NpcTeam tm) ? tm : NpcTeam.Dummy); else Ragdoll.Spawn(g, feet, r.Yaw, recordUndo: false);
            yield return ++step;
        }
        foreach (BuggyData b in data.Buggies)
        {
            Buggy car = g.Vehicles.SpawnBuggy(V(b.Position) + new Float3(0f, b.Rotation.Length == 4 ? 0f : 0.3f, 0f), b.Yaw, recordUndo: false, kind: b.Kind ?? "buggy");
            if (b.Rotation.Length == 4) car.Go.Transform.Rotation = new Quaternion(b.Rotation[0], b.Rotation[1], b.Rotation[2], b.Rotation[3]);
            if (b.Velocity.Length == 3) car.Body.LinearVelocity = V(b.Velocity);
            yield return ++step;
        }
        // Velocities last, once every constraint exists, so contraptions keep moving exactly as they were.
        foreach (var (p, v, w) in velocities)
            if (p.Alive && !p.Frozen) { p.Body.LinearVelocity = v; p.Body.AngularVelocity = w; }
        _lastSpawned = spawned.Count(p => p is not null);
    }
}
