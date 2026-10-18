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

        Color[] skins = [new(0.85f, 0.68f, 0.55f, 1f), new(0.62f, 0.45f, 0.34f, 1f), new(0.95f, 0.80f, 0.68f, 1f), new(0.42f, 0.30f, 0.22f, 1f)];
        var skin = skins[_counter % skins.Length];
        Color[] citizenShirts = [new(0.30f, 0.45f, 0.70f, 1f), new(0.75f, 0.35f, 0.30f, 1f), new(0.35f, 0.60f, 0.40f, 1f), new(0.85f, 0.75f, 0.40f, 1f), new(0.55f, 0.40f, 0.65f, 1f)];
        var shirt = team switch
        {
            NpcTeam.Police => new Color(0.12f, 0.20f, 0.42f, 1f),
            NpcTeam.Hostile => new Color(0.62f, 0.12f, 0.12f, 1f),
            NpcTeam.Friendly => new Color(0.25f, 0.62f, 0.45f, 1f),
            NpcTeam.Citizen => citizenShirts[_counter % citizenShirts.Length],
            _ => new Color(0.30f, 0.45f, 0.70f, 1f),
        };
        var pants = team == NpcTeam.Police ? new Color(0.08f, 0.10f, 0.16f, 1f) : new Color(0.22f, 0.24f, 0.28f, 1f);

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
            if (!Application.IsHeadless) { mat.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); mat.SetTexture("_SurfaceTex", ProcTex.Surface(0.75f, 0f)); }
            rd.Limbs.Add(p);
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
