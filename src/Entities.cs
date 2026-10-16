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
}

/// <summary>
/// Keeps an NPC dummy ragdoll standing (springs pelvis height and keeps torso/pelvis upright) until it is hit
/// hard enough, then it goes limp like GMod's ragdoll-on-death.
/// </summary>
public sealed class DummyBrain : MonoBehaviour
{
    public Ragdoll? Doll;
    public float StandHeight;
    public bool Limp { get; private set; }

    public void GoLimp() => Limp = true;

    public override void FixedUpdate()
    {
        if (Limp || Doll is null || !Doll.Alive) return;
        // Any limb moving fast (shot, blasted, physgunned) knocks it over.
        foreach (Prop l in Doll.Limbs)
            if (Prowl.Vector.Float3.Length(l.Body.LinearVelocity) > 3.5f) { Limp = true; return; }
        Rigidbody3D pelvis = Doll.Limbs[0].Body, torso = Doll.Limbs[1].Body;
        foreach (Rigidbody3D b in new[] { pelvis, torso })
        {
            Prowl.Vector.Float3 up = b.GameObject.Transform.Up;
            Prowl.Vector.Float3 axis = Prowl.Vector.Float3.Cross(up, Prowl.Vector.Float3.UnitY);
            b.AngularVelocity = axis * 12f + b.AngularVelocity * 0.2f;
        }
        Prowl.Vector.Float3 p = pelvis.GameObject.Transform.Position;
        Prowl.Vector.Float3 v = pelvis.LinearVelocity;
        pelvis.LinearVelocity = new Prowl.Vector.Float3(v.X * 0.8f, (StandHeight - p.Y) * 10f, v.Z * 0.8f);
        foreach (Prop l in Doll.Limbs) l.Body.SetActive(true);
    }
}

public static class EntityCatalog
{
    public static void Register()
    {
        SpawnCatalog.Register(new SpawnEntry("npc_dummy", "NPC Dummy", MenuTab.Entities, "NPCs", Icon.Eye, null, (g, pos, yaw) => SpawnDummy(g, pos, yaw).Name, 0f));
        SpawnCatalog.Register(new SpawnEntry("ent_barrel", "Explosive Barrel", MenuTab.Entities, "Fun", Icon.Barrel, null, (g, pos, yaw) => SpawnBarrel(g, pos).Go.Name, 0.55f));
        SpawnCatalog.Register(new SpawnEntry("ent_ball", "Bouncy Ball", MenuTab.Entities, "Fun", Icon.Sphere, null, (g, pos, yaw) => SpawnBall(g, pos).Go.Name, 0.5f));
        SpawnCatalog.Register(new SpawnEntry("ent_light", "Light", MenuTab.Entities, "Lighting", Icon.Power, null, (g, pos, yaw) => SpawnLight(g, pos).Go.Name, 0.3f));
        SpawnCatalog.Register(new SpawnEntry("ent_button", "Button", MenuTab.Entities, "Interactive", Icon.Play, null, (g, pos, yaw) => SpawnButton(g, pos, yaw).Go.Name, 0.3f));
    }

    public static Ragdoll SpawnDummy(SandboxGame g, Prowl.Vector.Float3 feet, float yaw)
    {
        Ragdoll rd = Ragdoll.Spawn(g, feet + new Prowl.Vector.Float3(0f, 0.05f, 0f), yaw);
        DummyBrain brain = rd.Pelvis.Go.AddComponent<DummyBrain>();
        brain.Doll = rd;
        brain.StandHeight = feet.Y + 1.0f;
        return rd;
    }

    public static Prop SpawnBarrel(SandboxGame g, Prowl.Vector.Float3 pos)
    {
        Prop p = g.Props.Spawn(PropKind.Barrel, pos, 0f);
        p.SetColor(new Prowl.Vector.Color(0.80f, 0.16f, 0.12f, 1f));
        p.Go.Name = p.Go.Name.Replace("Barrel", "Explosive Barrel");
        p.Go.AddComponent<ExplosiveBarrel>().Game = g;
        p.Go.AddComponent<ImpactFuse>().Game = g;
        return p;
    }

    public static Prop SpawnBall(SandboxGame g, Prowl.Vector.Float3 pos)
    {
        Prop p = g.Props.Spawn(PropKind.Sphere, pos, 0f);
        p.SetColor(ToolGun.HueColor(Random.Shared.NextSingle()));
        p.SetSurface(SurfaceMaterial.Get("Rubber"));
        p.Body.Restitution = 0.95f;
        p.Body.Friction = 0.3f;
        p.Body.Mass = 0.5f;
        p.Go.Name = p.Go.Name.Replace("Sphere", "Bouncy Ball");
        return p;
    }

    public static Prop SpawnLight(SandboxGame g, Prowl.Vector.Float3 pos)
    {
        Prop p = g.Props.Spawn(PropKind.SmallCrate, pos, 0f);
        p.Go.Name = p.Go.Name.Replace("Small Crate", "Light");
        p.SetColor(new Prowl.Vector.Color(1f, 0.92f, 0.7f, 1f));
        g.ToolGun.SetHit(p, pos + new Prowl.Vector.Float3(0f, 0.3f, 0f), Prowl.Vector.Float3.UnitY);
        ToolMode old = g.ToolGun.Mode;
        g.ToolGun.Mode = ToolMode.Light;
        g.ToolGun.Apply(p);
        g.ToolGun.Mode = old;
        return p;
    }

    public static Prop SpawnButton(SandboxGame g, Prowl.Vector.Float3 pos, float yaw)
    {
        Prop p = g.Props.Spawn(PropKind.SmallCrate, pos, yaw);
        p.Go.Name = p.Go.Name.Replace("Small Crate", "Button");
        p.SetColor(new Prowl.Vector.Color(0.25f, 0.27f, 0.30f, 1f));
        var cap = new GameObject("ButtonCap");
        cap.SetParent(p.Go, false);
        cap.Transform.LocalPosition = new Prowl.Vector.Float3(0f, 0.32f, 0f);
        MeshRenderer r = cap.AddComponent<MeshRenderer>();
        r.Mesh = Mesh.CreateCylinder(0.18f, 0.08f, 16);
        var m = new Prowl.Runtime.Resources.Material(g.Shader);
        m.SetColor("_MainColor", new Prowl.Vector.Color(0.9f, 0.25f, 0.2f, 1f));
        r.Material = m;
        SandboxButton b = p.Go.AddComponent<SandboxButton>();
        b.Cap = m;
        b.Key = ToolGun.ThrusterKeys[Math.Clamp(g.ToolGun.ThrusterKey, 0, ToolGun.ThrusterKeys.Length - 1)];
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
