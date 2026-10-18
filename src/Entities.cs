using Prowl.Runtime.Resources;
using Prowl.Runtime;

namespace Sandbox;

/// <summary>Explosive barrel: blows up when shot enough, hit by an explosion, or slammed hard.</summary>
public sealed class ExplosiveBarrel : MonoBehaviour
{
    public SandboxGame? Game;
    public float Health = 20f;
    private float _fuse = -1f;
    public bool Exploded { get; private set; }

    public void Trigger(float delay) { if (_fuse < 0f && !Exploded) _fuse = delay; }
    public void Damage(float amount) { Health -= amount; if (Health <= 0f) Trigger(0.05f); }

    public override void Update()
    {
        if (_fuse < 0f || Exploded || Game is null) return;
        _fuse -= Time.DeltaTime;
        if (_fuse > 0f) return;
        Exploded = true;
        Prop? p = Game.Props.FromBody(GameObject.GetComponent<Rigidbody3D>());
        var at = GameObject.Transform.Position;
        if (p is not null) Game.Props.Remove(p);
        Game.Fx.Explode(at, 7f, 70f);
    }
}

/// <summary>Virtual keys held down by toggled buttons, so buttons can drive thrusters and wheels.</summary>
public static class KeyState
{
    public static readonly HashSet<KeyCode> Toggled = [];
    public static bool Held(KeyCode k) => Input.GetKey(k) || Toggled.Contains(k);
}

/// <summary>Press with E (or shoot it) to toggle its key on/off, like GMod's button bound to a numpad key.</summary>
public sealed class SandboxButton : MonoBehaviour
{
    public KeyCode Key = KeyCode.T;
    public bool On { get; private set; }
    public Prowl.Runtime.Resources.Material? Cap;

    public void Press(SandboxGame g)
    {
        On = !On;
        if (On) KeyState.Toggled.Add(Key); else KeyState.Toggled.Remove(Key);
        Cap?.SetColor("_MainColor", On ? new Prowl.Vector.Color(0.3f, 0.9f, 0.4f, 1f) : new Prowl.Vector.Color(0.9f, 0.25f, 0.2f, 1f));
        g.Sounds.Play("sfx_button", 0.6f, GameObject.Transform.Position);
        g.Notify($"Button {(On ? "on" : "off")} ({Key})");
    }

    public override void OnDisable() { if (On) KeyState.Toggled.Remove(Key); }

    public void Rebind(KeyCode k)
    {
        if (On) { KeyState.Toggled.Remove(Key); KeyState.Toggled.Add(k); }
        Key = k;
    }
}

/// <summary>
/// Keeps an NPC dummy ragdoll standing (springs pelvis height and keeps torso/pelvis upright) until it is hit
/// hard enough, then it goes limp like GMod's ragdoll-on-death.
/// </summary>
public enum NpcTeam { Dummy, Friendly, Hostile, Citizen, Police }

/// <summary>
/// Keeps an NPC ragdoll upright and, for walkers, steers it: friendly/citizen NPCs wander, hostiles chase and hit the
/// player, police chase a wanted player. Any hard hit (bullet, blast, physgun) knocks it limp.
/// </summary>
public sealed class DummyBrain : MonoBehaviour
{
    public Ragdoll? Doll;
    public float StandHeight;
    public NpcTeam Team = NpcTeam.Dummy;
    public bool Limp { get; private set; }
    public float Health = Combat.NpcMaxHealth;
    public static SandboxGame? Game;
    private Prowl.Vector.Float3 _wander;
    private float _wanderTimer, _attackCd, _heading;
    private bool _facing;
    public string Mood { get; private set; } = "idle";

    public void GoLimp() => Limp = true;

    public override void FixedUpdate()
    {
        if (Limp || Doll is null || !Doll.Alive) return;
        // A limb removed (remover / undo) or frozen (physgun) ends the brain: it can't steer a broken or static body.
        if (Doll.Limbs.Count < 2 || Doll.Limbs.Any(l => !l.Alive || l.Frozen)) { Limp = true; return; }
        foreach (Prop l in Doll.Limbs)
            if (Prowl.Vector.Float3.Length(l.Body.LinearVelocity) > (Team == NpcTeam.Dummy ? 3.5f : 5.5f)) { Limp = true; Game?.Roleplay.OnNpcDown(this); return; }
        Rigidbody3D pelvis = Doll.Limbs[0].Body, torso = Doll.Limbs[1].Body;
        Prowl.Vector.Float3 p = pelvis.GameObject.Transform.Position;
        Prowl.Vector.Float3 move = Steer(p, Time.FixedDeltaTime);
        float speed = Prowl.Vector.Float3.Length(move);
        if (speed > 0.05f) _heading = MathF.Atan2(move.X, move.Z);
        foreach (Rigidbody3D b in new[] { pelvis, torso })
        {
            Prowl.Vector.Float3 up = b.GameObject.Transform.Up;
            Prowl.Vector.Float3 axis = Prowl.Vector.Float3.Cross(up, Prowl.Vector.Float3.UnitY);
            Prowl.Vector.Float3 fwd = b.GameObject.Transform.Forward;
            float yawErr = speed > 0.05f || _facing ? Wrap(_heading - MathF.Atan2(fwd.X, fwd.Z)) : 0f;
            b.AngularVelocity = axis * 12f + new Prowl.Vector.Float3(0f, yawErr * 4f, 0f) + b.AngularVelocity * 0.2f;
        }
        float ground = Team == NpcTeam.Dummy ? StandHeight - 1f : MapBuilder.GroundHeight(p.X, p.Z, StandHeight - 1f);
        Prowl.Vector.Float3 v = pelvis.LinearVelocity;
        float bob = speed > 0.05f ? MathF.Sin((float)Time.UnscaledTotalTime * 9f) * 0.03f : 0f;
        pelvis.LinearVelocity = new Prowl.Vector.Float3(v.X * 0.8f + move.X * 0.2f, (ground + 1f + bob - p.Y) * 10f, v.Z * 0.8f + move.Z * 0.2f);
        // Legs swing while walking (cheap procedural gait).
        if (speed > 0.05f && Doll.Limbs.Count > 9)
        {
            float ph = (float)Time.UnscaledTotalTime * 9f;
            Prowl.Vector.Float3 side = pelvis.GameObject.Transform.Right;
            Doll.Limbs[7].Body.AngularVelocity = side * MathF.Sin(ph) * 6f;
            Doll.Limbs[9].Body.AngularVelocity = side * -MathF.Sin(ph) * 6f;
        }
        foreach (Prop l in Doll.Limbs) l.Body.SetActive(true);
    }

    private static float Wrap(float a) { while (a > MathF.PI) a -= MathF.Tau; while (a < -MathF.PI) a += MathF.Tau; return a; }

    /// <summary>Desired horizontal velocity (m/s).</summary>
    private Prowl.Vector.Float3 Steer(Prowl.Vector.Float3 p, float dt)
    {
        _facing = false;
        if (Team == NpcTeam.Dummy || Game is null) return Prowl.Vector.Float3.Zero;
        Prowl.Vector.Float3 player = Game.Player.FeetPosition;
        Prowl.Vector.Float3 toPlayer = new(player.X - p.X, 0f, player.Z - p.Z);
        float dist = Prowl.Vector.Float3.Length(toPlayer);
        // Citizens back away from drawn guns and wanted players; everyone turns to look at you up close.
        bool armed = Game.Weapons.Current is Firearm;
        if (Team == NpcTeam.Citizen && dist < 9f && dist > 0.01f && (armed || Game.Roleplay.Wanted))
        {
            Mood = "flee";
            return -toPlayer / dist * 2.8f;
        }
        bool jailedHere = Game.Roleplay.Jailed;
        bool chase = Team == NpcTeam.Hostile || (Team == NpcTeam.Police && Game.Roleplay.Wanted && !jailedHere);
        Mood = chase && dist < 40f ? "chase" : "wander";
        _attackCd -= dt;
        if (chase && !Game.Health.Dead && dist < 40f)
        {
            if (dist < 1.3f)
            {
                if (_attackCd <= 0f)
                {
                    _attackCd = 1f;
                    if (Team == NpcTeam.Police) Game.Roleplay.Arrest("You were arrested by the police");
                    else Game.Health.Damage(Game, 12f, "NPC attack");
                }
                return Prowl.Vector.Float3.Zero;
            }
            return toPlayer / dist * (Team == NpcTeam.Police ? 3.2f : 2.6f);
        }
        if (dist < 2.6f && Team is NpcTeam.Citizen or NpcTeam.Police or NpcTeam.Friendly)
        {
            _facing = true; Mood = "greet";
            _heading = MathF.Atan2(toPlayer.X, toPlayer.Z);
            return Prowl.Vector.Float3.Zero;
        }
        _wanderTimer -= dt;
        Prowl.Vector.Float3 flat = new(p.X, 0f, p.Z);
        if (Town.Active && Town.Waypoints.Count > 0 && (_wanderTimer <= 0f || Prowl.Vector.Float3.Length(_wander - flat) < 1f))
        {
            // Walk the sidewalks: pick one of the nearer waypoints, pause now and then.
            _wanderTimer = 6f + Random.Shared.NextSingle() * 8f;
            var near = Town.Waypoints.OrderBy(w => Prowl.Vector.Float3.Length(w - flat)).Skip(1).Take(4).ToArray();
            _wander = Random.Shared.NextSingle() < 0.2f ? flat : near[Random.Shared.Next(near.Length)];
        }
        if (_wanderTimer <= 0f || Prowl.Vector.Float3.Length(_wander - flat) < 1f)
        {
            _wanderTimer = 4f + Random.Shared.NextSingle() * 6f;
            float a = Random.Shared.NextSingle() * MathF.Tau, r = 3f + Random.Shared.NextSingle() * 10f;
            _wander = new Prowl.Vector.Float3(Math.Clamp(p.X + MathF.Cos(a) * r, -50f, 50f), 0f, Math.Clamp(p.Z + MathF.Sin(a) * r, -50f, 50f));
            if (Random.Shared.NextSingle() < 0.3f) _wander = flat; // stand around sometimes
        }
        Prowl.Vector.Float3 d = _wander - flat;
        float l = Prowl.Vector.Float3.Length(d);
        return l < 0.5f ? Prowl.Vector.Float3.Zero : d / l * (Team == NpcTeam.Police ? 1.6f : 1.3f);
    }
}

public static class EntityCatalog
{
    public static void Register()
    {
        SpawnCatalog.Register(new SpawnEntry("npc_dummy", "NPC Dummy", MenuTab.Entities, "NPCs", Icon.Eye, null, (g, pos, yaw) => SpawnDummy(g, pos, yaw).Name, 0f));
        SpawnCatalog.Register(new SpawnEntry("npc_friendly", "Friendly Walker", MenuTab.Entities, "NPCs", Icon.Users, null, (g, pos, yaw) => SpawnDummy(g, pos, yaw, team: NpcTeam.Friendly).Name, 0f));
        SpawnCatalog.Register(new SpawnEntry("npc_hostile", "Hostile Walker", MenuTab.Entities, "NPCs", Icon.Bolt, null, (g, pos, yaw) => SpawnDummy(g, pos, yaw, team: NpcTeam.Hostile).Name, 0f));
        SpawnCatalog.Register(new SpawnEntry("ent_barrel", "Explosive Barrel", MenuTab.Entities, "Fun", Icon.Barrel, null, (g, pos, yaw) => SpawnBarrel(g, pos).Go.Name, 0.55f));
        SpawnCatalog.Register(new SpawnEntry("ent_ball", "Bouncy Ball", MenuTab.Entities, "Fun", Icon.Sphere, null, (g, pos, yaw) => SpawnBall(g, pos).Go.Name, 0.5f));
        SpawnCatalog.Register(new SpawnEntry("ent_camera", "Camera", MenuTab.Entities, "Utility", Icon.Camera, null, (g, pos, yaw) => CameraFeeds.Spawn(g, pos, yaw).Go.Name, 0.3f));
        SpawnCatalog.Register(new SpawnEntry("ent_light", "Light", MenuTab.Entities, "Lighting", Icon.Power, null, (g, pos, yaw) => SpawnLight(g, pos).Go.Name, 0.3f));
        SpawnCatalog.Register(new SpawnEntry("ent_button", "Button", MenuTab.Entities, "Interactive", Icon.Play, null, (g, pos, yaw) => SpawnButton(g, pos, yaw).Go.Name, 0.3f));
    }

    public static Ragdoll SpawnDummy(SandboxGame g, Prowl.Vector.Float3 feet, float yaw, bool recordUndo = true, NpcTeam team = NpcTeam.Dummy)
    {
        DummyBrain.Game = g;
        Ragdoll rd = Ragdoll.Spawn(g, feet + new Prowl.Vector.Float3(0f, 0.05f, 0f), yaw, recordUndo, team);
        rd.IsDummy = true;
        DummyBrain brain = rd.Pelvis.Go.AddComponent<DummyBrain>();
        brain.Doll = rd;
        brain.Team = team;
        brain.StandHeight = feet.Y + 1.0f;
        rd.Team = team;
        return rd;
    }

    /// <summary>Turns an existing prop into the given entity type (used by spawning and map loading).</summary>
    public static void Decorate(SandboxGame g, Prop p, string entity, string? key = null)
    {
        switch (entity)
        {
            case "ent_barrel": MakeBarrel(g, p); break;
            case { } m when m.StartsWith("mdl_"): if (ModelProps.Find(m) is { } def) ModelProps.Apply(g, p, def); break;
            case "ent_ball": MakeBall(p, keepColor: true, g); break;
            case "ent_light": p.Entity = "ent_light"; p.Go.Name = p.Go.Name.Replace("Small Crate", "Light"); LightModel(g, p); break;
            case "ent_camera": CameraFeeds.Make(g, p); break;
            case "rp_money" or "rp_printer" or "rp_shipment": g.Roleplay.Decorate(p, entity, key); break;
            case "ent_button": MakeButton(g, p, Enum.TryParse(key, out KeyCode k) ? k : KeyCode.T); break;
        }
    }

    public static Prop SpawnBarrel(SandboxGame g, Prowl.Vector.Float3 pos) => MakeBarrel(g, g.Props.Spawn(PropKind.Barrel, pos, 0f));

    private static Prop MakeBarrel(SandboxGame g, Prop p)
    {
        p.Entity = "ent_barrel";
        p.SetColor(new Prowl.Vector.Color(0.80f, 0.16f, 0.12f, 1f));
        p.Go.Name = p.Go.Name.Replace("Barrel", "Explosive Barrel");
        p.Go.AddComponent<ExplosiveBarrel>().Game = g;
        p.Go.AddComponent<ImpactFuse>().Game = g;
        if (!Application.IsHeadless && p.Go.Transform.Find("HazardLabel") is null)
        {
            // Hazard labels on two sides between the upper rib and hoop: yellow band, black border stripes, flame mark.
            var yel = Mats.New(g.Shader); yel.SetColor("_MainColor", new Prowl.Vector.Color(0.96f, 0.78f, 0.12f, 1f));
            var blk = Mats.New(g.Shader); blk.SetColor("_MainColor", new Prowl.Vector.Color(0.06f, 0.06f, 0.07f, 1f));
            const float r = 0.381f;
            Prowl.Vector.Float3 V(float a, float y, float dr) => new(MathF.Sin(a) * (r + dr), y, MathF.Cos(a) * (r + dr));
            Mesh flame = Meshes.Faces(
                [V(-0.16f, 0.16f, 0.004f), V(0.16f, 0.16f, 0.004f), V(0.13f, 0.215f, 0.004f), V(0.04f, 0.315f, 0.004f), V(0.03f, 0.25f, 0.004f), V(-0.03f, 0.3f, 0.004f), V(-0.13f, 0.215f, 0.004f)]);
            foreach (float yaw in new[] { 0f, 180f })
            {
                var root = new GameObject("HazardLabel"); root.SetParent(p.Go, false);
                root.Transform.LocalEulerAngles = new Prowl.Vector.Float3(0f, yaw, 0f);
                void Add(Mesh m, Material mt) { var c = new GameObject("lbl"); c.SetParent(root, false); MeshRenderer mr = c.AddComponent<MeshRenderer>(); mr.Mesh = m; mr.Material = mt; }
                Add(Meshes.CylPatch(r + 0.001f, 0.55f, 0.125f, 0.345f), yel);
                Add(Meshes.CylPatch(r + 0.002f, 0.55f, 0.125f, 0.14f), blk);
                Add(Meshes.CylPatch(r + 0.002f, 0.55f, 0.33f, 0.345f), blk);
                Add(flame, blk);
            }
        }
        return p;
    }

    public static Prop SpawnBall(SandboxGame g, Prowl.Vector.Float3 pos) => MakeBall(g.Props.Spawn(PropKind.Sphere, pos, 0f), keepColor: false, g);

    private static Prop MakeBall(Prop p, bool keepColor, SandboxGame? g = null)
    {
        p.Entity = "ent_ball";
        if (!keepColor) p.SetColor(ToolGun.HueColor(Random.Shared.NextSingle()));
        p.SetSurface(SurfaceMaterial.Get("Rubber"));
        p.Body.Restitution = 0.95f;
        p.Body.Friction = 0.3f;
        p.Body.Mass = 0.5f;
        p.Go.Name = p.Go.Name.Replace("Sphere", "Bouncy Ball");
        if (g is null || Application.IsHeadless || p.Go.Transform.Find("BallBand") is not null) return p;
        // Smooth high-res shell, a yellow moulding seam band and a little inflation valve (matches the icon).
        if (p.Go.GetComponent<MeshRenderer>() is { } sr) sr.Mesh = Meshes.Sphere(0.5f);
        void Part(string name, Mesh mesh, Prowl.Vector.Color c, Prowl.Vector.Float3 euler)
        {
            var o = new GameObject(name); o.SetParent(p.Go, false);
            o.Transform.LocalEulerAngles = euler;
            var r = o.AddComponent<MeshRenderer>(); r.Mesh = mesh;
            var m = Mats.New(g.Shader); m.SetColor("_MainColor", c); r.Material = m;
        }
        Part("BallBand", Meshes.Lathe("entballband", [(0.494f, -0.06f), (0.504f, -0.055f), (0.506f, 0f), (0.504f, 0.055f), (0.494f, 0.06f)], 64), new Prowl.Vector.Color(0.98f, 0.86f, 0.3f, 1f), new(20f, 0f, -25f));
        Part("BallValve", Meshes.Lathe("entballvalve", [(0.0001f, 0.508f), (0.035f, 0.505f), (0.042f, 0.497f)], 20), new Prowl.Vector.Color(0.2f, 0.2f, 0.22f, 1f), new(-35f, 0f, 30f));
        return p;
    }

    /// <summary>Work-lamp look on the light crate: lathed glass globe (prop colour) over a dark cage base.</summary>
    private static void LightModel(SandboxGame g, Prop p)
    {
        if (p.Go.GetComponent<MeshRenderer>() is { } r)
            r.Mesh = Meshes.Lathe("entlightglobe", [(0.0001f, -0.12f), (0.2f, -0.12f), (0.26f, 0f), (0.25f, 0.12f), (0.18f, 0.24f), (0.08f, 0.29f), (0.0001f, 0.3f)], 36);
        var b = new GameObject("LightBase");
        b.SetParent(p.Go, false);
        MeshRenderer br = b.AddComponent<MeshRenderer>();
        br.Mesh = Meshes.Lathe("entlightbase", [(0.0001f, -0.3f), (0.27f, -0.3f), (0.29f, -0.27f), (0.27f, -0.2f), (0.22f, -0.16f), (0.21f, -0.11f), (0.0001f, -0.11f)], 36);
        var m = Mats.New(g.Shader); m.SetColor("_MainColor", new Prowl.Vector.Color(0.12f, 0.12f, 0.13f, 1f)); br.Material = m;
        if (Application.IsHeadless || p.Go.Transform.Find("LightCage") is not null) return;
        // Frosted globe (plain white, not the crate's wood texture) inside a steel wire cage with a screw collar.
        try { p.Go.GetComponent<MeshRenderer>()?.Material.Res?.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        var wire = Mats.New(g.Shader); wire.SetColor("_MainColor", new Prowl.Vector.Color(0.2f, 0.2f, 0.22f, 1f));
        var cage = new GameObject("LightCage"); cage.SetParent(p.Go, false);
        var meshes = new List<Mesh>();
        (float R, float Y)[] prof = [(0.215f, -0.11f), (0.278f, 0f), (0.268f, 0.12f), (0.198f, 0.245f), (0.1f, 0.305f), (0.02f, 0.322f)];
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.PI / 3f;
            meshes.Add(VehicleModels.BentPipe(0.007f, 0.04f, Prowl.Vector.Float3.UnitY, prof.Select(q => new Prowl.Vector.Float3(MathF.Cos(a) * q.R, q.Y, MathF.Sin(a) * q.R)).ToArray()));
        }
        foreach (var (rr, yy) in new[] { (0.28f, 0.0f), (0.25f, 0.16f) })
            meshes.Add(Meshes.Lathe($"cagering{rr}", [(rr - 0.007f, yy - 0.007f), (rr + 0.007f, yy - 0.007f), (rr + 0.007f, yy + 0.007f), (rr - 0.007f, yy + 0.007f), (rr - 0.007f, yy - 0.007f)], 36));
        MeshRenderer cr = cage.AddComponent<MeshRenderer>(); cr.Mesh = Meshes.Merge(meshes.ToArray()); cr.Material = wire;
        var collar = new GameObject("LightCollar"); collar.SetParent(p.Go, false);
        MeshRenderer co = collar.AddComponent<MeshRenderer>();
        var cl = new List<(float, float)> { (0.0001f, -0.13f) };
        for (int i = 0; i <= 6; i++) cl.Add((0.205f + (i % 2) * 0.008f, -0.13f + i * 0.007f));
        cl.Add((0.0001f, -0.086f));
        co.Mesh = Meshes.Lathe("lightcollar", cl.ToArray(), 36);
        var brass = Mats.New(g.Shader); brass.SetColor("_MainColor", new Prowl.Vector.Color(0.62f, 0.5f, 0.3f, 1f)); co.Material = brass;
    }

    public static Prop SpawnLight(SandboxGame g, Prowl.Vector.Float3 pos)
    {
        Prop p = g.Props.Spawn(PropKind.SmallCrate, pos, 0f);
        p.Entity = "ent_light";
        p.Go.Name = p.Go.Name.Replace("Small Crate", "Light");
        p.SetColor(new Prowl.Vector.Color(1f, 0.92f, 0.7f, 1f));
        LightModel(g, p);
        g.ToolGun.SetHit(p, pos + new Prowl.Vector.Float3(0f, 0.3f, 0f), Prowl.Vector.Float3.UnitY);
        ToolMode old = g.ToolGun.Mode;
        g.ToolGun.Mode = ToolMode.Light;
        g.ToolGun.Apply(p);
        g.ToolGun.Mode = old;
        return p;
    }

    public static Prop SpawnButton(SandboxGame g, Prowl.Vector.Float3 pos, float yaw)
        => MakeButton(g, g.Props.Spawn(PropKind.SmallCrate, pos, yaw), g.ToolGun.ButtonKey);

    private static Prop MakeButton(SandboxGame g, Prop p, KeyCode key)
    {
        p.Entity = "ent_button";
        p.Go.Name = p.Go.Name.Replace("Small Crate", "Button");
        p.SetColor(new Prowl.Vector.Color(0.42f, 0.44f, 0.47f, 1f));
        var cap = new GameObject("ButtonCap");
        cap.SetParent(p.Go, false);
        cap.Transform.LocalPosition = new Prowl.Vector.Float3(0f, 0.3f, 0f);
        if (p.Go.GetComponent<MeshRenderer>() is { } housing)
        {
            housing.Mesh = Meshes.RoundedBox(new Prowl.Vector.Float3(0.6f, 0.56f, 0.6f), 0.05f, 4);
            p.SetSurface(SurfaceMaterial.Get("Metal"));
            if (!Application.IsHeadless) try { housing.Material.Res?.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        }
        if (!Application.IsHeadless && p.Go.Transform.Find("ButtonPlate") is null)
        {
            // Industrial push-station: yellow top plate with screws, black/yellow hazard collar, "PUSH" plate and a foot plinth.
            var yel = Mats.New(g.Shader); yel.SetColor("_MainColor", new Prowl.Vector.Color(0.95f, 0.74f, 0.1f, 1f));
            var blk = Mats.New(g.Shader); blk.SetColor("_MainColor", new Prowl.Vector.Color(0.07f, 0.07f, 0.08f, 1f));
            var stl = Mats.New(g.Shader); stl.SetColor("_MainColor", new Prowl.Vector.Color(0.7f, 0.71f, 0.73f, 1f));
            var wht = Mats.New(g.Shader); wht.SetColor("_MainColor", new Prowl.Vector.Color(0.95f, 0.95f, 0.94f, 1f));
            void Part(string n, Mesh mesh, Prowl.Vector.Float3 at, Material mt, Prowl.Vector.Float3 eul = default)
            {
                var c = new GameObject(n); c.SetParent(p.Go, false); c.Transform.LocalPosition = at; c.Transform.LocalEulerAngles = eul;
                MeshRenderer mr = c.AddComponent<MeshRenderer>(); mr.Mesh = mesh; mr.Material = mt;
            }
            Part("ButtonPlate", Meshes.RoundedBox(new Prowl.Vector.Float3(0.56f, 0.03f, 0.56f), 0.01f, 3), new(0f, 0.285f, 0f), yel);
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                Part("Screw", Meshes.Lathe("btnscrew", [(0.0001f, 0f), (0.018f, 0f), (0.018f, 0.006f), (0.012f, 0.01f), (0.0001f, 0.011f)], 12), new(sx * 0.23f, 0.3f, sz * 0.23f), stl);
            // Hazard collar: alternating black/yellow chevron blocks around the top edge.
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 5; k++)
                {
                    float off = -0.24f + k * 0.12f;
                    Prowl.Vector.Float3 at = i switch { 0 => new(off, 0.245f, 0.302f), 1 => new(off, 0.245f, -0.302f), 2 => new(0.302f, 0.245f, off), _ => new(-0.302f, 0.245f, off) };
                    Part("Hazard", Meshes.RoundedBox(i < 2 ? new Prowl.Vector.Float3(0.12f, 0.05f, 0.006f) : new Prowl.Vector.Float3(0.006f, 0.05f, 0.12f), 0.002f, 2), at, k % 2 == 0 ? blk : yel);
                }
            // Plates front and back; the glyph mesh reads from -Z, so the front copy turns round.
            foreach (float sz in new[] { 1f, -1f })
            {
                Part("PushPlate", Meshes.RoundedBox(new Prowl.Vector.Float3(0.26f, 0.09f, 0.008f), 0.004f, 2), new(0f, 0.05f, 0.3f * sz), blk);
                Part("PushText", Glyphs.Text("PUSH", 0.06f, 0f, 0.003f), new(0f, 0.05f, 0.3075f * sz), wht, new(0f, sz > 0f ? 180f : 0f, 0f));
            }
            Part("Plinth", Meshes.RoundedBox(new Prowl.Vector.Float3(0.64f, 0.04f, 0.64f), 0.01f, 3), new(0f, -0.28f, 0f), blk);
        }
        var bezel = new GameObject("ButtonBezel");
        bezel.SetParent(p.Go, false);
        bezel.Transform.LocalPosition = new Prowl.Vector.Float3(0f, 0.3f, 0f);
        MeshRenderer bz = bezel.AddComponent<MeshRenderer>();
        bz.Mesh = Meshes.Lathe("btnbezel", [(0.17f, -0.01f), (0.24f, -0.01f), (0.245f, 0.01f), (0.23f, 0.035f), (0.2f, 0.04f), (0.185f, 0.02f)], 40);
        var bzm = Mats.New(g.Shader); bzm.SetColor("_MainColor", new Prowl.Vector.Color(0.75f, 0.76f, 0.78f, 1f)); bz.Material = bzm;
        MeshRenderer r = cap.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.Lathe("btncap2", [(0.0001f, 0f), (0.175f, 0f), (0.18f, 0.02f), (0.178f, 0.04f), (0.165f, 0.062f), (0.14f, 0.077f), (0.105f, 0.086f), (0.06f, 0.0905f), (0.0001f, 0.092f)], 48);
        var m = new Prowl.Runtime.Resources.Material(g.Shader);
        m.SetColor("_MainColor", new Prowl.Vector.Color(0.9f, 0.25f, 0.2f, 1f));
        // Plain white albedo: the default texture's pattern showed through the glossy dome as dark specks.
        if (!Application.IsHeadless) try { m.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        r.Material = m;
        SandboxButton b = p.Go.AddComponent<SandboxButton>();
        b.Cap = m;
        b.Key = key;
        p.SetFrozen(true);
        return p;
    }
}

/// <summary>Explodes a barrel when it hits something hard (big sudden velocity change).</summary>
public sealed class ImpactFuse : MonoBehaviour
{
    public SandboxGame? Game;
    private Prowl.Vector.Float3 _lastV;
    private float _age;
    public override void FixedUpdate()
    {
        Rigidbody3D? b = GetComponent<Rigidbody3D>();
        if (b is null) return;
        _age += Time.FixedDeltaTime;
        Prowl.Vector.Float3 v = b.LinearVelocity;
        if (_age > 0.5f && Prowl.Vector.Float3.Length(v - _lastV) > 9f) GetComponent<ExplosiveBarrel>()?.Trigger(0.05f);
        _lastV = v;
    }
}
