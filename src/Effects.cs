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
        m = new Material(g.Shader);
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
        GameObject go = Prim("rocket", Mesh.CreateCube(new Float3(0.08f, 0.08f, 0.35f)), Mat("rocket", new Color(0.35f, 0.38f, 0.30f, 1f)), from);
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
        GameObject go = Prim("grenade", Mesh.CreateSphere(0.09f, 10, 12), Mat("nade", new Color(0.25f, 0.32f, 0.20f, 1f)), from);
        go.AddComponent<SphereCollider>().Radius = 0.09f;
        Rigidbody3D rb = go.AddComponent<Rigidbody3D>();
        rb.Mass = 0.5f;
        rb.LinearVelocity = velocity;
        rb.Restitution = 0.4f;
        Add(go, 2.5f, tick: (t, _) => { if (t.Life <= 0.0001f) Explode(t.Go.Transform.Position + new Float3(0f, 0.2f, 0f)); });
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
