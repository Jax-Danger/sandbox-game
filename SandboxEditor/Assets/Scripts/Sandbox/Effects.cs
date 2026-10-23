// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using Jitter2.Dynamics;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Short-lived combat effects and projectiles: muzzle flashes, tracers, spark bursts, bullet decals,
/// rockets, grenades and explosions. Everything is pooled/capped and ticked from the game update.
/// </summary>
public sealed class Fx(SandboxGame g)
{
    private sealed class Timed { public GameObject Go = null!; public float Life, Max; public Float3 Vel; public bool Gravity; public Action<Timed, float>? Tick; }
    private readonly List<Timed> _live = [];
    private readonly Queue<GameObject> _decals = new();
    private const int MaxDecals = 64;
    private readonly Dictionary<string, Material> _mats = [];

    private Material Mat(string key, Color c, float emission = 0f)
    {
        if (_mats.TryGetValue(key, out Material? m)) return m;
        m = Mats.New(g.Shader);
        m.SetColor("_MainColor", c);
        try { m.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        if (emission > 0f)
        {
            try { m.SetTexture("_EmissionTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
            m.SetFloat("_EmissionIntensity", emission);
        }
        return _mats[key] = m;
    }

    private GameObject Prim(string name, Mesh mesh, Material mat, Float3 pos)
    {
        var go = new GameObject(name);
        go.Transform.Position = pos;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.Mesh = mesh;
        r.Material = mat;
        g.World.Add(go);
        return go;
    }

    private void Add(GameObject go, float life, Float3 vel = default, bool gravity = false, Action<Timed, float>? tick = null)
        => _live.Add(new Timed { Go = go, Life = life, Max = life, Vel = vel, Gravity = gravity, Tick = tick });

    public int LiveCount => _live.Count;
    public int Explosions { get; private set; }

    public void MuzzleFlash(Float3 at, float size = 1f)
    {
        GameObject go = Prim("muzzle_flash", Mesh.CreateSphere(0.05f * size, 8, 10), Mat("flash", new Color(1f, 0.8f, 0.4f, 1f), 6f), at);
        Add(go, 0.05f);
    }

    /// <summary>Thin emissive box stretched between two points (mesh-based so it works without the Line shader).</summary>
    public static void Stretch(GameObject go, Float3 from, Float3 to)
    {
        Float3 d = to - from;
        float len = Float3.Length(d);
        go.Transform.Position = from + d * 0.5f;
        if (len > 1e-4f) go.Transform.Forward = d / len;
        go.Transform.LocalScale = new Float3(1f, 1f, MathF.Max(0.001f, len));
    }

    public void Tracer(Float3 from, Float3 to)
    {
        GameObject go = Prim("tracer", UnitBeam(0.012f), Mat("tracer", new Color(1f, 0.85f, 0.5f, 1f), 5f), from);
        Stretch(go, from, to);
        Add(go, 0.05f);
    }

    private static readonly Dictionary<float, Mesh> _beams = [];
    /// <summary>A 1 m long (Z) beam mesh of the given thickness; scale Z to set length.</summary>
    private static Mesh? _rocketMesh, _nadeMesh;
    public static Mesh UnitBeam(float thickness) => _beams.TryGetValue(thickness, out Mesh? m) ? m : _beams[thickness] = Mesh.CreateCube(new Float3(thickness, thickness, 1f));

    public void Sparks(Float3 at, Float3 normal, int count = 6)
    {
        var rng = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            Float3 dir = Float3.Normalize(normal + new Float3(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * 1.6f);
            GameObject go = Prim("spark", Mesh.CreateCube(new Float3(0.02f, 0.02f, 0.02f)), Mat("spark", new Color(1f, 0.75f, 0.3f, 1f), 4f), at);
            Add(go, 0.25f + rng.NextSingle() * 0.2f, dir * (2f + rng.NextSingle() * 3f), gravity: true);
        }
    }

    public void Decal(Float3 at, Float3 normal, Rigidbody3D? onBody)
    {
        GameObject go = Prim("decal", Mesh.CreateCube(new Float3(0.07f, 0.07f, 0.004f)), Mat("decal", new Color(0.05f, 0.05f, 0.05f, 1f)), at + normal * 0.003f);
        go.Transform.Forward = normal;
        if (onBody is not null && onBody.MotionType != MotionType.Static) go.SetParent(onBody.GameObject, true);
        _decals.Enqueue(go);
        while (_decals.Count > MaxDecals)
        {
            GameObject old = _decals.Dequeue();
            if (old.IsValid() && old.Scene != null) old.Scene.Remove(old);
        }
    }

    /// <summary>Hitscan: impulse + sparks + decal on what was hit. Returns the hit point or null.</summary>
    public Float3? Bullet(Float3 origin, Float3 dir, Float3 muzzle, float impulse, float range = 200f)
    {
        if (!g.World.Physics.Raycast(origin, dir, range, out RaycastHit hit))
        {
            Tracer(muzzle, origin + dir * 60f);
            return null;
        }
        Tracer(muzzle, hit.Point);
        Sparks(hit.Point, hit.Normal, 4);
        Decal(hit.Point, hit.Normal, hit.Rigidbody);
        Push(hit.Rigidbody, dir * impulse, hit.Point);
        g.Combat.Hit(hit.Rigidbody, impulse * 4f, hit.Point, g.Weapons.Current?.Name ?? "Gun");
        if (hit.Rigidbody?.GameObject is { } hgo && hgo.GetComponent<SandboxButton>() is { } btn) btn.Press(g);
        hit.Rigidbody?.GameObject.GetComponent<ExplosiveBarrel>()?.Damage(impulse * 1.5f);
        g.Sounds.Play("sfx_impact", 0.35f, hit.Point, 0.9f + Random.Shared.NextSingle() * 0.2f);
        return hit.Point;
    }

    /// <summary>Applies an impulse (kg·m/s) to a dynamic body, with a little spin from the off-centre hit.</summary>
    public static void Push(Rigidbody3D? body, Float3 impulse, Float3 at)
    {
        if (body is null || body.MotionType != MotionType.Dynamic) return;
        float m = MathF.Max(0.1f, body.Mass);
        body.LinearVelocity += impulse / m;
        Float3 r = at - body.GameObject.Transform.Position;
        body.AngularVelocity += Float3.Cross(r, impulse) / (m * 0.4f);
        body.SetActive(true);
    }

    public void Explode(Float3 at, float radius = 6f, float force = 60f)
    {
        Explosions++;
        GameObject ball = Prim("explosion", Mesh.CreateSphere(0.5f, 12, 16), Mat("boom", new Color(1f, 0.55f, 0.15f, 1f), 8f), at);
        Add(ball, 0.35f, tick: (t, _) =>
        {
            float k = 1f - t.Life / t.Max;
            float s = 0.5f + k * radius * 0.5f;
            t.Go.Transform.LocalScale = new Float3(s, s, s);
        });
        Sparks(at, Float3.UnitY, 14);
        g.Sounds.Play("sfx_explosion", 1f, at);
        g.Combat.Explosion(at, radius, 120f);
        foreach (Prop p in g.Props.Props.ToArray())
        {
            if (!p.Alive || p.Frozen) continue;
            Float3 d = p.Go.Transform.Position - at;
            float dist = Float3.Length(d);
            if (dist > radius) continue;
            Float3 dir = dist > 0.01f ? d / dist : Float3.UnitY;
            dir = Float3.Normalize(dir + new Float3(0f, 0.5f, 0f));
            float falloff = 1f - dist / radius;
            Push(p.Body, dir * force * falloff * MathF.Max(1f, MathF.Sqrt(p.Body.Mass)), p.Go.Transform.Position);
            p.Go.GetComponent<ExplosiveBarrel>()?.Trigger(0.15f + dist * 0.03f);
        }
        if (Float3.Length(g.Player.EyePosition - at) < radius * 0.5f) g.Player.Shake(0.4f);
    }

    public void Rocket(Float3 from, Float3 dir)
    {
        GameObject go = Prim("rocket", _rocketMesh ??= Meshes.LatheZ([(0.0001f, -0.2f), (0.03f, -0.2f), (0.032f, -0.12f), (0.04f, -0.08f), (0.04f, 0.06f), (0.045f, 0.09f), (0.042f, 0.13f), (0.025f, 0.18f), (0.008f, 0.2f), (0.0001f, 0.205f)]), Mat("rocket", new Color(0.35f, 0.38f, 0.30f, 1f)), from);
        go.Transform.Forward = dir;
        Add(go, 6f, dir * 28f, tick: (t, dt) =>
        {
            Float3 pos = t.Go.Transform.Position;
            float step = Float3.Length(t.Vel) * dt;
            if (g.World.Physics.Raycast(pos, Float3.Normalize(t.Vel), step + 0.2f, out RaycastHit hit))
            {
                Explode(hit.Point + hit.Normal * 0.2f);
                t.Life = 0f;
            }
            else if (Random.Shared.NextSingle() < 0.5f)
                Sparks(pos, -Float3.Normalize(t.Vel), 1);
        });
    }

    public void Grenade(Float3 from, Float3 velocity)
    {
        GameObject go = Prim("grenade", _nadeMesh ??= Meshes.Lathe("fxnade", [(0.0001f, -0.075f), (0.04f, -0.072f), (0.06f, -0.05f), (0.066f, -0.01f), (0.062f, 0.03f), (0.045f, 0.06f), (0.024f, 0.07f), (0.024f, 0.095f), (0.0001f, 0.1f)], 20), Mat("nade", new Color(0.25f, 0.32f, 0.20f, 1f)), from);
        go.AddComponent<SphereCollider>().Radius = 0.09f;
        Rigidbody3D rb = go.AddBody();
        rb.Mass = 0.5f;
        rb.LinearVelocity = velocity;
        rb.Restitution = 0.4f;
        Add(go, 2.5f, tick: (t, _) => { if (t.Life <= 0.0001f) Explode(t.Go.Transform.Position + new Float3(0f, 0.2f, 0f)); });
    }

    /// <summary>Blue sparkle + expanding shell where something spawns.</summary>
    public void SpawnPuff(Float3 at)
    {
        GameObject shell = Prim("spawn_fx", Mesh.CreateSphere(0.5f, 12, 16), Mat("spawnfx", new Color(0.35f, 0.65f, 1f, 1f), 3f), at);
        Add(shell, 0.18f, tick: (t, _) =>
        {
            float k = 1f - t.Life / t.Max, s = 0.3f + k * 1.4f * (1f - k * 0.5f);
            t.Go.Transform.LocalScale = new Float3(s, s * 0.15f, s);
        });
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * MathF.PI * 2f;
            GameObject sp = Prim("spark", Mesh.CreateCube(new Float3(0.03f, 0.03f, 0.03f)), Mat("spawnfx", new Color(0.35f, 0.65f, 1f, 1f), 3f), at);
            Add(sp, 0.35f, new Float3(MathF.Cos(a) * 2.5f, 1.5f, MathF.Sin(a) * 2.5f), gravity: true);
        }
    }

    private GameObject? _beam, _beamTip;
    /// <summary>Physgun beam from the muzzle to the grab point, gently pulsing. Pass null to hide.</summary>
    public void Beam(Float3 from, Float3? to)
    {
        if (to is null)
        {
            if (_beam is not null) _beam.Enabled = false;
            if (_beamTip is not null) _beamTip.Enabled = false;
            return;
        }
        Material m = Mat("beam", new Color(0.35f, 0.75f, 1f, 1f), 6f);
        _beam ??= Prim("physgun_beam", UnitBeam(0.02f), m, from);
        _beamTip ??= Prim("physgun_tip", Mesh.CreateSphere(0.06f, 10, 12), m, from);
        _beam.Enabled = _beamTip.Enabled = true;
        Stretch(_beam, from, to.Value);
        float pulse = 1f + 0.35f * MathF.Sin(Time.UnscaledTotalTime * 18f);
        Float3 sc = _beam.Transform.LocalScale;
        _beam.Transform.LocalScale = new Float3(pulse, pulse, sc.Z);
        _beamTip.Transform.Position = to.Value;
        _beamTip.Transform.LocalScale = new Float3(pulse, pulse, pulse);
    }

    public void Tick(float dt)
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            Timed t = _live[i];
            if (!t.Go.IsValid()) { _live.RemoveAt(i); continue; }
            t.Life -= dt;
            if (t.Vel != Float3.Zero)
            {
                if (t.Gravity) t.Vel += new Float3(0f, -9.81f, 0f) * dt;
                t.Go.Transform.Position += t.Vel * dt;
            }
            if (t.Life <= 0f) t.Life = 0f;
            t.Tick?.Invoke(t, dt);
            if (t.Life <= 0f)
            {
                if (t.Go.Scene != null) t.Go.Scene.Remove(t.Go);
                _live.RemoveAt(i);
            }
        }
    }

    public void Clear()
    {
        foreach (Timed t in _live) if (t.Go.IsValid() && t.Go.Scene != null) t.Go.Scene.Remove(t.Go);
        _live.Clear();
        while (_decals.Count > 0) { GameObject d = _decals.Dequeue(); if (d.IsValid() && d.Scene != null) d.Scene.Remove(d); }
    }
}
