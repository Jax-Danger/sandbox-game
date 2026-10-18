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
            case "ent_ball": MakeBall(p, keepColor: true); break;
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

    public static Prop SpawnBall(SandboxGame g, Prowl.Vector.Float3 pos) => MakeBall(g.Props.Spawn(PropKind.Sphere, pos, 0f), keepColor: false);

    private static Prop MakeBall(Prop p, bool keepColor)
    {
        p.Entity = "ent_ball";
        if (!keepColor) p.SetColor(ToolGun.HueColor(Random.Shared.NextSingle()));
        p.SetSurface(SurfaceMaterial.Get("Rubber"));
        p.Body.Restitution = 0.95f;
        p.Body.Friction = 0.3f;
        p.Body.Mass = 0.5f;
        p.Go.Name = p.Go.Name.Replace("Sphere", "Bouncy Ball");
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
        p.SetColor(new Prowl.Vector.Color(0.25f, 0.27f, 0.30f, 1f));
        var cap = new GameObject("ButtonCap");
        cap.SetParent(p.Go, false);
        cap.Transform.LocalPosition = new Prowl.Vector.Float3(0f, 0.3f, 0f);
        if (p.Go.GetComponent<MeshRenderer>() is { } housing) housing.Mesh = Meshes.RoundedBox(new Prowl.Vector.Float3(0.6f, 0.6f, 0.6f), 0.12f, 4);
        var bezel = new GameObject("ButtonBezel");
        bezel.SetParent(p.Go, false);
        bezel.Transform.LocalPosition = new Prowl.Vector.Float3(0f, 0.3f, 0f);
        MeshRenderer bz = bezel.AddComponent<MeshRenderer>();
        bz.Mesh = Meshes.Lathe("btnbezel", [(0.17f, -0.01f), (0.24f, -0.01f), (0.245f, 0.01f), (0.23f, 0.035f), (0.2f, 0.04f), (0.185f, 0.02f)], 40);
        var bzm = Mats.New(g.Shader); bzm.SetColor("_MainColor", new Prowl.Vector.Color(0.75f, 0.76f, 0.78f, 1f)); bz.Material = bzm;
        MeshRenderer r = cap.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.Lathe("btncap", [(0.0001f, 0f), (0.175f, 0f), (0.18f, 0.03f), (0.17f, 0.06f), (0.12f, 0.085f), (0.0001f, 0.09f)], 40);
        var m = new Prowl.Runtime.Resources.Material(g.Shader);
        m.SetColor("_MainColor", new Prowl.Vector.Color(0.9f, 0.25f, 0.2f, 1f));
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
