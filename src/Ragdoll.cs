using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// A physics ragdoll: ten rigid-body limbs joined with ball sockets plus cone limits
/// (Prowl's BallSocketConstraint + ConeLimitConstraint, the same constraints the joint samples use).
/// Every limb is registered as a <see cref="Prop"/>, so the physgun, toolgun, weapons and explosions work per limb.
/// </summary>
public sealed class Ragdoll
{
    public List<Prop> Limbs { get; } = [];
    public string Name { get; private set; } = "Ragdoll";
    public bool IsDummy { get; set; }
    public NpcTeam Team { get; set; } = NpcTeam.Dummy;
    public Prop Pelvis => Limbs[0];
    private static int _counter;

    private sealed record LimbDef(string Name, Float3 Center, Float3 Size, bool Sphere, int Parent, Float3 Joint, float ConeDeg, float Mass);

    // Built for a 1.8 m figure standing with its feet at y = 0, facing +Z. Parent index -1 = root.
    private static readonly LimbDef[] Defs =
    [
        new("Pelvis",     new(0f, 1.00f, 0f),      new(0.34f, 0.20f, 0.20f), false, -1, default, 0f, 8f),
        new("Torso",      new(0f, 1.37f, 0f),      new(0.40f, 0.50f, 0.24f), false, 0, new(0f, 1.10f, 0f), 30f, 14f),
        new("Head",       new(0f, 1.77f, 0f),      new(0.26f, 0.26f, 0.26f), true,  1, new(0f, 1.63f, 0f), 45f, 4f),
        new("Upper arm L", new(-0.28f, 1.44f, 0f), new(0.11f, 0.32f, 0.11f), false, 1, new(-0.28f, 1.60f, 0f), 85f, 2f),
        new("Lower arm L", new(-0.28f, 1.12f, 0f), new(0.10f, 0.30f, 0.10f), false, 3, new(-0.28f, 1.28f, 0f), 70f, 1.5f),
        new("Upper arm R", new(0.28f, 1.44f, 0f),  new(0.11f, 0.32f, 0.11f), false, 1, new(0.28f, 1.60f, 0f), 85f, 2f),
        new("Lower arm R", new(0.28f, 1.12f, 0f),  new(0.10f, 0.30f, 0.10f), false, 5, new(0.28f, 1.28f, 0f), 70f, 1.5f),
        new("Upper leg L", new(-0.10f, 0.68f, 0f), new(0.14f, 0.42f, 0.14f), false, 0, new(-0.10f, 0.90f, 0f), 60f, 6f),
        new("Lower leg L", new(-0.10f, 0.24f, 0f), new(0.13f, 0.44f, 0.13f), false, 7, new(-0.10f, 0.46f, 0f), 75f, 4f),
        new("Upper leg R", new(0.10f, 0.68f, 0f),  new(0.14f, 0.42f, 0.14f), false, 0, new(0.10f, 0.90f, 0f), 60f, 6f),
        new("Lower leg R", new(0.10f, 0.24f, 0f),  new(0.13f, 0.44f, 0.13f), false, 9, new(0.10f, 0.46f, 0f), 75f, 4f),
    ];

    public static Ragdoll Spawn(SandboxGame g, Float3 feet, float yawDegrees, bool recordUndo = true, NpcTeam team = NpcTeam.Dummy)
    {
        var rd = new Ragdoll { Name = $"Ragdoll #{++_counter}" };
        float yaw = yawDegrees * MathF.PI / 180f;
        float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
        Float3 Rot(Float3 v) => new(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);

        // Tones kept below ~0.72 albedo (sun-facing foreheads clipped at 0.84): the full sun (1.5) plus bloom turned the palest preset into a white mask.
        Color[] skins = [new(0.68f, 0.5f, 0.39f, 1f), new(0.54f, 0.38f, 0.28f, 1f), new(0.72f, 0.55f, 0.44f, 1f), new(0.38f, 0.26f, 0.19f, 1f)];
        var skin = skins[_counter % skins.Length];
        Color[] citizenShirts = [new(0.30f, 0.45f, 0.70f, 1f), new(0.75f, 0.35f, 0.30f, 1f), new(0.35f, 0.60f, 0.40f, 1f), new(0.85f, 0.75f, 0.40f, 1f), new(0.55f, 0.40f, 0.65f, 1f)];
        var shirt = team switch
        {
            NpcTeam.Police => new Color(0.15f, 0.21f, 0.36f, 1f),
            NpcTeam.Hostile => new Color(0.62f, 0.12f, 0.12f, 1f),
            NpcTeam.Friendly => new Color(0.25f, 0.62f, 0.45f, 1f),
            NpcTeam.Citizen => citizenShirts[_counter % citizenShirts.Length],
            _ => new Color(0.30f, 0.45f, 0.70f, 1f),
        };
        // Trousers vary per NPC (denim, charcoal, khaki, brown, faded denim) on a different cycle from shirts; police keep navy.
        Color[] trousers = [new(0.2f, 0.27f, 0.42f, 1f), new(0.22f, 0.24f, 0.28f, 1f), new(0.55f, 0.48f, 0.34f, 1f), new(0.3f, 0.22f, 0.16f, 1f), new(0.27f, 0.33f, 0.44f, 1f)];
        var pants = team == NpcTeam.Police ? new Color(0.08f, 0.10f, 0.16f, 1f) : trousers[(_counter * 3 + 1) % trousers.Length];

        for (int i = 0; i < Defs.Length; i++)
        {
            LimbDef d = Defs[i];
            var go = new GameObject($"{rd.Name} {d.Name}");
            go.Transform.Position = feet + Rot(d.Center);
            go.Transform.LocalEulerAngles = new Float3(0f, yawDegrees, 0f);
            Color col = d.Name == "Head" || d.Name.StartsWith("Lower arm") ? skin : d.Name == "Torso" || d.Name.StartsWith("Upper arm") ? shirt : pants;
            var mat = Mats.New(g.Shader);
            mat.SetColor("_MainColor", col);
            try { mat.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.Material = mat;
            Rigidbody3D body = go.AddComponent<Rigidbody3D>();
            if (d.Sphere)
            {
                r.Mesh = Meshes.Sphere(d.Size.X / 2f);
                go.AddComponent<SphereCollider>().Radius = d.Size.X / 2f;
            }
            else go.AddComponent<BoxCollider>().Size = d.Size; // colliders stay boxes (stable stacking)
            // Sculpted look: swept limbs with hands, shoes, face and hair riding on the limb (visual only).
            var (main, _) = Humans.Limb(d.Name);
            var details = Humans.MergedDetails(d.Name);
            r.Mesh = main;
            foreach (Humans.Detail det in details)
            {
                var dg = new GameObject("detail");
                dg.SetParent(go, false);
                dg.Transform.LocalPosition = det.Offset;
                MeshRenderer dr = dg.AddComponent<MeshRenderer>();
                dr.Mesh = det.Mesh;
                dr.Material = DetailMat(g, det.Tint, skin, shirt, pants);
            }
            body.Mass = d.Mass;
            g.World.Add(go);
            Prop p = g.Props.Adopt(go, body, mat, col, PropKind.Limb, d.Size.Y / 2f);
            p.SetSurface(SurfaceMaterial.Get("Rubber"));
            // Rubber's albedo/sheen made the torso read tealer than the sleeves (details use plain matte cloth):
            // keep rubber physics but give the limb the same matte cloth look as its detail parts.
            if (!Application.IsHeadless) { mat.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); mat.SetTexture("_SurfaceTex", ProcTex.Surface(0.95f, 0f)); }
            rd.Limbs.Add(p);
        }

        // Police peaked cap: navy crown flaring over a black band, glossy visor and a gold cap badge (scaled to the
        // skull's oval so it sits on the hair instead of floating as a round tin).
        if (team == NpcTeam.Police && rd.Limbs.FirstOrDefault(l => l.Go.Name.EndsWith("Head")) is { } head)
        {
            var cap = new GameObject("PoliceCap");
            cap.SetParent(head.Go, false);
            cap.Transform.LocalPosition = new Float3(0f, 0.0f, -0.004f);
            cap.Transform.LocalScale = new Float3(1f, 1f, 1.1f);
            var navy = Mats.New(g.Shader); navy.SetColor("_MainColor", new Color(0.07f, 0.09f, 0.17f, 1f));
            var black = Mats.New(g.Shader); black.SetColor("_MainColor", new Color(0.03f, 0.03f, 0.035f, 1f));
            var gold = Mats.New(g.Shader); gold.SetColor("_MainColor", new Color(0.78f, 0.6f, 0.2f, 1f));
            if (!Application.IsHeadless) try { black.SetTexture("_SurfaceTex", ProcTex.Surface(0.25f, 0.1f)); gold.SetTexture("_SurfaceTex", ProcTex.Surface(0.3f, 0.9f)); } catch { }
            void Part(string n, Prowl.Runtime.Resources.Mesh m, Material mt, Float3 p, Float3 e)
            {
                var o = new GameObject(n); o.SetParent(cap, false); o.Transform.LocalPosition = p; o.Transform.LocalEulerAngles = e;
                MeshRenderer mr = o.AddComponent<MeshRenderer>(); mr.Mesh = m; mr.Material = mt;
            }
            Part("CapBand", Meshes.Lathe("capband", [(0.1135f, 0.05f), (0.1175f, 0.054f), (0.1185f, 0.092f), (0.115f, 0.097f)], 32), black, Float3.Zero, Float3.Zero);
            Part("CapCrown", Meshes.Lathe("capcrown", [(0.114f, 0.094f), (0.12f, 0.11f), (0.134f, 0.15f), (0.136f, 0.16f), (0.128f, 0.167f), (0.0001f, 0.168f)], 32), navy, Float3.Zero, Float3.Zero);
            Part("CapVisor", Meshes.Faces([new Float3(-0.095f, 0f, -0.01f), new Float3(-0.065f, 0f, 0.06f), new Float3(0.065f, 0f, 0.06f), new Float3(0.095f, 0f, -0.01f)], [new Float3(0.095f, -0.004f, -0.01f), new Float3(0.065f, -0.004f, 0.06f), new Float3(-0.065f, -0.004f, 0.06f), new Float3(-0.095f, -0.004f, -0.01f)]), black, new Float3(0f, 0.056f, 0.112f), new Float3(16f, 0f, 0f));
            Part("CapBadge", Meshes.Lathe("capbadge", [(0.0001f, 0.006f), (0.012f, 0.004f), (0.014f, 0f), (0.0001f, 0f)], 12), gold, new Float3(0f, 0.12f, 0.124f), new Float3(80f, 0f, 0f));
        }
        // Police carry their kit on the duty belt: holstered pistol on the right hip, baton on the left.
        if (team == NpcTeam.Police && rd.Limbs.FirstOrDefault(l => l.Go.Name.EndsWith("Pelvis")) is { } hips)
        {
            WorldWeapons.Attach(g, hips.Go, "weapon_pistol", new Float3(0.175f, -0.06f, 0.01f), new Float3(95f, 0f, 0f));
            var pouch = new GameObject("Holster");
            pouch.SetParent(hips.Go, false);
            pouch.Transform.LocalPosition = new Float3(0.172f, -0.1f, 0.01f);
            MeshRenderer pr = pouch.AddComponent<MeshRenderer>();
            pr.Mesh = Meshes.RoundedBox(new Float3(0.04f, 0.13f, 0.05f), 0.012f);
            pr.Material = DetailMat(g, Humans.Tint.Belt, skin, shirt, pants);
            // Side-handle baton hanging from a belt ring: rubber grip, slimmer shaft, rounded tip, short side handle.
            var baton = new GameObject("Baton");
            baton.SetParent(hips.Go, false);
            baton.Transform.LocalPosition = new Float3(-0.175f, -0.2f, 0.02f);
            baton.Transform.LocalEulerAngles = new Float3(-8f, 0f, -6f);
            MeshRenderer br = baton.AddComponent<MeshRenderer>();
            br.Mesh = Meshes.Lathe("npcbaton", [(0.0001f, 0.2f), (0.017f, 0.2f), (0.018f, 0.19f), (0.017f, 0.07f), (0.0135f, 0.064f), (0.012f, -0.31f), (0.0135f, -0.32f), (0.0001f, -0.328f)], 18);
            br.Material = DetailMat(g, Humans.Tint.Belt, skin, shirt, pants);
            var side = new GameObject("BatonHandle");
            side.SetParent(baton, false);
            side.Transform.LocalPosition = new Float3(0f, 0.05f, 0.045f);
            side.Transform.LocalEulerAngles = new Float3(90f, 0f, 0f);
            MeshRenderer sr = side.AddComponent<MeshRenderer>();
            sr.Mesh = Meshes.RoundedCylinder(0.011f, 0.08f, 0.004f, 14);
            sr.Material = br.Material;
            var ring = new GameObject("BatonRing");
            ring.SetParent(hips.Go, false);
            ring.Transform.LocalPosition = new Float3(-0.175f, -0.06f, 0.025f);
            MeshRenderer rr = ring.AddComponent<MeshRenderer>();
            rr.Mesh = Meshes.RoundedBox(new Float3(0.03f, 0.05f, 0.03f), 0.008f);
            rr.Material = DetailMat(g, Humans.Tint.Cuff, skin, shirt, pants);
            if (rd.Limbs.FirstOrDefault(l => l.Go.Name.EndsWith("Torso")) is { } chest)
            {
                // Gold shield badge above the right-hand pocket and a silver name bar over the left one.
                var gold = Mats.New(g.Shader); gold.SetColor("_MainColor", new Color(0.86f, 0.68f, 0.26f, 1f));
                var silver = Mats.New(g.Shader); silver.SetColor("_MainColor", new Color(0.78f, 0.8f, 0.83f, 1f));
                void Bit(string n, Mesh m, Float3 at, Float3 eul, Material mt)
                {
                    var bg = new GameObject(n); bg.SetParent(chest.Go, false);
                    bg.Transform.LocalPosition = at; bg.Transform.LocalEulerAngles = eul;
                    MeshRenderer mr = bg.AddComponent<MeshRenderer>(); mr.Mesh = m; mr.Material = mt;
                }
                Bit("Badge", Meshes.Lathe("npcbadge", [(0.0001f, 0.004f), (0.017f, 0.004f), (0.022f, 0.002f), (0.022f, 0f), (0.0001f, 0f)], 7), new Float3(0.09f, 0.1f, 0.121f), new Float3(90f, 0f, 0f), gold);
                Bit("BadgeBoss", Meshes.Lathe("npcbadge2", [(0.0001f, 0.002f), (0.008f, 0.0018f), (0.009f, 0f), (0.0001f, 0f)], 16), new Float3(0.09f, 0.1f, 0.125f), new Float3(90f, 0f, 0f), silver);
                Bit("NameBar", Meshes.RoundedBox(new Float3(0.05f, 0.011f, 0.004f), 0.0015f), new Float3(-0.09f, 0.125f, 0.121f), Float3.Zero, silver);
            }
        }

        // Hostile walkers carry a crowbar in the right hand, hanging down along the forearm (hook forward).
        if (team == NpcTeam.Hostile && rd.Limbs.FirstOrDefault(l => l.Go.Name.EndsWith("Lower arm R")) is { } fore && !Application.IsHeadless)
        {
            var bar = new GameObject("Crowbar"); bar.SetParent(fore.Go, false);
            bar.Transform.LocalPosition = new Float3(0.0f, -0.2f, 0.035f);
            bar.Transform.LocalEulerAngles = new Float3(-12f, 0f, 0f);
            var red = Mats.New(g.Shader); red.SetColor("_MainColor", new Color(0.62f, 0.08f, 0.07f, 1f));
            var stl = Mats.New(g.Shader); stl.SetColor("_MainColor", new Color(0.5f, 0.51f, 0.53f, 1f));
            MeshRenderer br = bar.AddComponent<MeshRenderer>();
            // Shaft from the fist down to the hooked claw (hook curls forward, +Z), chisel end above the fist.
            br.Mesh = Meshes.Sweep([new Meshes.Sec(new Float3(0f, 0.08f, 0f), 0.011f, 0.011f, 2.4f), new Meshes.Sec(new Float3(0f, -0.5f, 0f), 0.011f, 0.011f, 2.4f),
                new Meshes.Sec(new Float3(0f, -0.55f, 0.01f), 0.0108f, 0.0105f, 2.6f), new Meshes.Sec(new Float3(0f, -0.575f, 0.04f), 0.0104f, 0.009f, 3f),
                new Meshes.Sec(new Float3(0f, -0.565f, 0.075f), 0.0098f, 0.006f, 3.4f), new Meshes.Sec(new Float3(0f, -0.54f, 0.09f), 0.0085f, 0.0025f, 4f)], Float3.UnitX, 14, 0.4f);
            br.Material = red;
            var tip = new GameObject("CrowbarChisel"); tip.SetParent(bar, false);
            MeshRenderer tr = tip.AddComponent<MeshRenderer>();
            tr.Mesh = Meshes.Sweep([new Meshes.Sec(new Float3(0f, 0.079f, 0f), 0.0112f, 0.0112f, 2.4f), new Meshes.Sec(new Float3(0f, 0.11f, -0.006f), 0.0115f, 0.007f, 3f), new Meshes.Sec(new Float3(0f, 0.135f, -0.016f), 0.012f, 0.0022f, 4f)], Float3.UnitX, 12, 0.3f);
            tr.Material = stl;
        }

        // Joints: socket on the child at the joint point, then a cone around the limb's long axis.
        for (int i = 0; i < Defs.Length; i++)
        {
            LimbDef d = Defs[i];
            if (d.Parent < 0) continue;
            Prop child = rd.Limbs[i], parent = rd.Limbs[d.Parent];
            BallSocketConstraint sock = child.Go.AddComponent<BallSocketConstraint>();
            sock.Anchor = d.Joint - d.Center; // limb rotation is yaw-only and anchors are local, so the unrotated offset is right
            sock.Softness = 0.002f;
            sock.ConnectedBody = parent.Body;
            ConeLimitConstraint cone = child.Go.AddComponent<ConeLimitConstraint>();
            cone.Axis = new Float3(0f, 1f, 0f);
            cone.MinAngle = 0f;
            cone.MaxAngle = d.ConeDeg;
            cone.Softness = 0.01f;
            cone.ConnectedBody = parent.Body;
        }
        for (int i = 0; i < rd.Limbs.Count; i++)
            for (int j = i + 1; j < rd.Limbs.Count; j++)
                CollisionPairs.Ignore(rd.Limbs[i].Body, rd.Limbs[j].Body);

        All.Add(rd);
        if (recordUndo) g.Props.PushUndo(new RagdollUndo(g.Props, rd));
        return rd;
    }

    public static readonly List<Ragdoll> All = [];

    private static readonly Color[] HairColors = [new(0.12f, 0.08f, 0.05f, 1f), new(0.32f, 0.2f, 0.1f, 1f), new(0.06f, 0.06f, 0.06f, 1f), new(0.62f, 0.46f, 0.24f, 1f)];

    private static Material DetailMat(SandboxGame g, Humans.Tint t, Color skin, Color shirt, Color pants)
    {
        Color c = t switch
        {
            Humans.Tint.Skin => skin, Humans.Tint.Sclera => new Color(0.93f, 0.92f, 0.88f, 1f), Humans.Tint.Lip => new Color(skin.R * 0.8f, skin.G * 0.6f, skin.B * 0.58f, 1f),
            Humans.Tint.Shoe => new Color(0.16f, 0.11f, 0.08f, 1f),
            Humans.Tint.Hair => HairColors[_counter % HairColors.Length],
            Humans.Tint.Eye => new Color(0.05f, 0.05f, 0.06f, 1f),
            Humans.Tint.Belt => new Color(0.08f, 0.07f, 0.06f, 1f),
            Humans.Tint.Pants => new Color(pants.R * 0.8f, pants.G * 0.8f, pants.B * 0.8f, 1f),
            Humans.Tint.Cuff => new Color(shirt.R * 0.75f, shirt.G * 0.75f, shirt.B * 0.75f, 1f),
            _ => shirt,
        };
        var m = Mats.New(g.Shader);
        m.SetColor("_MainColor", c);
        return m;
    }

    public bool Alive => Limbs.Any(l => l.Alive);

    public void Remove(PropManager props)
    {
        foreach (Prop l in Limbs) props.Remove(l);
        All.Remove(this);
    }

    private sealed class RagdollUndo(PropManager props, Ragdoll rd) : IUndoable
    {
        public string Label => $"Undone: {rd.Name}";
        public void Undo() => rd.Remove(props);
    }

    public static void Register()
    {
        SpawnCatalog.Register(new SpawnEntry("npc_ragdoll", "Ragdoll", MenuTab.Entities, "NPCs", Icon.Eye, null,
            (g, pos, yaw) => Spawn(g, pos, yaw).Name, 0f));
    }
}
