using Jitter2.Dynamics;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>More GMod tools: No-collide, Elastic, Slider, Hinge, Hoverball, Emitter, Stacker. Numpad keys are available for every key-bound tool.</summary>
public sealed partial class ToolGun
{
    public float ElasticStrength { get; set; } = 60f;
    public float HoverStrength { get; set; } = 1f;
    public float EmitterRate { get; set; } = 8f;
    public int StackCount { get; set; } = 3;
    public bool StackWeld { get; set; } = true;
    public KeyCode GadgetKey { get; set; } = KeyCode.T;
    public KeyCode GadgetKey2 { get; set; } = KeyCode.G;
    public KeyCode ButtonKey { get; set; } = KeyCode.T;

    /// <summary>A tool setting edited with the key-picker widget (value = KeyCode).</summary>
    public static ToolSetting KeyPick(string name, Func<KeyCode> get, Action<KeyCode> set, string help)
        => new(name, 0f, 0f, () => (float)(int)get(), v => set((KeyCode)(int)v), v => KeyName((KeyCode)(int)v), help, true);

    /// <summary>Keys offered by key-bound tools: the letter row plus the numpad, as in GMod.</summary>
    public static readonly KeyCode[] BindKeys =
    [
        KeyCode.T, KeyCode.G, KeyCode.Y, KeyCode.H, KeyCode.U, KeyCode.J,
        KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
        KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
    ];

    public static string KeyName(KeyCode k) => Binds.KeyName(k);

    public static readonly ToolInfo[] MoreTools =
    [
        new(ToolMode.NoCollide, "No-collide", "Constraints", "Stops two props colliding with each other.", "Click two props", "Cancel the first prop"),
        new(ToolMode.Elastic, "Elastic", "Constraints", "A springy band between two points that pulls them together.", "Click a point, then a second point", "Cancel the first point"),
        new(ToolMode.Slider, "Slider", "Constraints", "Lets a prop slide along a line relative to another (like a piston rail).", "Click the sliding prop, then the rail prop", "Cancel the first prop"),
        new(ToolMode.Hinge, "Hinge", "Constraints", "A free hinge around the surface normal you click: doors, flaps, see-saws.", "Click the hinge point, then the prop to hinge to", "Cancel the first point"),
        new(ToolMode.Hoverball, "Hoverball", "Construction", "Holds a prop at a height. Hold its keys to raise or lower it.", "Attach a hoverball", null),
        new(ToolMode.Emitter, "Emitter", "Construction", "Emits sparks while its key is held (or toggled by a button).", "Attach an emitter", null),
        new(ToolMode.Stacker, "Stacker", "Construction", "Stacks copies of a prop on top of each other, optionally welded.", "Stack copies on the side you click", null),
    ];

    private bool IsMoreMode => Mode is ToolMode.NoCollide or ToolMode.Elastic or ToolMode.Slider or ToolMode.Hinge or ToolMode.Hoverball or ToolMode.Emitter or ToolMode.Stacker;

    private IEnumerable<ToolSetting> MoreSettings(ToolMode mode)
    {
        ToolSetting KeySetting(string help) => KeyPick(mode == ToolMode.Hoverball ? "Up key" : "Key", () => GadgetKey, k => GadgetKey = k, help);
        switch (mode)
        {
            case ToolMode.Elastic:
                yield return new ToolSetting("Strength", 10f, 300f, () => ElasticStrength, v => ElasticStrength = v, v => $"{v:0}", "How hard the band pulls (per kg of the lighter prop).");
                break;
            case ToolMode.Hoverball:
                yield return new ToolSetting("Strength", 0.2f, 3f, () => HoverStrength, v => HoverStrength = v, v => $"{v:0.0}×", "How firmly it holds height.");
                yield return KeySetting("Hold this key to raise.");
                yield return KeyPick("Down key", () => GadgetKey2, k => GadgetKey2 = k, "Hold this key to lower.");
                break;
            case ToolMode.Emitter:
                yield return new ToolSetting("Rate", 1f, 30f, () => EmitterRate, v => EmitterRate = v, v => $"{v:0}/s", "Bursts per second.");
                yield return KeySetting("Hold this key to emit.");
                break;
            case ToolMode.Stacker:
                yield return new ToolSetting("Count", 1f, 10f, () => StackCount, v => StackCount = (int)MathF.Round(v), v => $"{(int)MathF.Round(v)}", "How many copies to stack.");
                yield return new ToolSetting("Weld", 0f, 1f, () => StackWeld ? 1f : 0f, v => StackWeld = v > 0.5f, v => v > 0.5f ? "On" : "Off", "Weld each copy to the one below.");
                break;
        }
    }

    private string? ApplyMore(Prop prop)
    {
        if (!IsMoreMode) return null;
        Float3 point = HitPointOn(prop), normal = HitNormalOn(prop);
        switch (Mode)
        {
            case ToolMode.NoCollide or ToolMode.Elastic or ToolMode.Slider or ToolMode.Hinge:
                if (PendingLink is null)
                {
                    _linkFirst = prop;
                    _linkLocal = prop.Go.Transform.InverseTransformPoint(point);
                    _linkNormal = normal;
                    return $"{InfoFor(Mode).Name}: now click the second prop";
                }
                if (ReferenceEquals(_linkFirst, prop)) return "Pick a different prop";
                {
                    Prop a = _linkFirst!;
                    _linkFirst = null;
                    Float3 lb = prop.Go.Transform.InverseTransformPoint(point);
                    return Mode switch
                    {
                        ToolMode.NoCollide => NoCollide(a, prop),
                        ToolMode.Elastic => Elastic(a, _linkLocal, prop, lb, ElasticStrength),
                        ToolMode.Slider => Slider(a, prop, Float3.Normalize(prop.Go.Transform.Position - a.Go.Transform.Position + new Float3(0f, 1e-4f, 0f))),
                        _ => Hinge(a, _linkLocal, a.Go.Transform.InverseTransformDirection(_linkNormal), prop),
                    };
                }
            case ToolMode.Hoverball:
            {
                KeyCode up = GadgetKey, down = GadgetKey2;
                Hoverball h = Hoverball.Attach(props, prop, point, normal, HoverStrength, up, down);
                props.PushUndo(new ActionUndo($"Undone: hoverball on {prop.Go.Name}", () => h.Detach()));
                return $"Hoverball on {prop.Go.Name} ({KeyName(up)} up / {KeyName(down)} down)";
            }
            case ToolMode.Emitter:
            {
                KeyCode k = GadgetKey;
                Emitter e = Emitter.Attach(props, prop, point, normal, EmitterRate, k);
                props.PushUndo(new ActionUndo($"Undone: emitter on {prop.Go.Name}", () => e.Detach()));
                return $"Emitter on {prop.Go.Name} (hold {KeyName(k)})";
            }
            case ToolMode.Stacker:
                return Stack(prop, normal, StackCount, StackWeld);
        }
        return null;
    }

    private Float3 _linkNormal = Float3.UnitY;

    public string NoCollide(Prop a, Prop b, bool recordUndo = true)
    {
        CollisionPairs.Ignore(a.Body, b.Body);
        NoCollideLink link = a.Go.AddComponent<NoCollideLink>();
        link.Other = b;
        if (recordUndo) props.PushUndo(new ActionUndo("Undone: no-collide", () => { if (a.Alive) a.Go.RemoveComponent(link); if (a.Alive && b.Alive) CollisionPairs.Enable(a.Body, b.Body); }));
        return $"{a.Go.Name} no longer collides with {b.Go.Name}";
    }

    public string Elastic(Prop a, Float3 localA, Prop b, Float3 localB, float strength, float? rest = null, bool recordUndo = true)
    {
        ElasticBand e = a.Go.AddComponent<ElasticBand>();
        e.Other = b; e.LocalA = localA; e.LocalB = localB; e.Strength = strength;
        e.Rest = rest ?? Float3.Length(b.Go.Transform.TransformPoint(localB) - a.Go.Transform.TransformPoint(localA)) * 0.5f;
        RopeVisual v = a.Go.AddComponent<RopeVisual>();
        v.Init(localA, b, localB);
        e.Visual = v;
        if (recordUndo) props.PushUndo(new ActionUndo("Undone: elastic", () => { if (a.Alive) { a.Go.RemoveComponent(e); v.Destroy(); } }));
        return $"Elastic from {a.Go.Name} to {b.Go.Name}";
    }

    public string Slider(Prop a, Prop b, Float3 worldAxis, bool recordUndo = true)
    {
        PrismaticJoint j = a.Go.AddComponent<PrismaticJoint>();
        j.Anchor = Float3.Zero;
        j.Axis = a.Go.Transform.InverseTransformDirection(worldAxis);
        j.MinDistance = -4f;
        j.MaxDistance = 4f;
        j.ConnectedBody = b.Body;
        CollisionPairs.Ignore(a.Body, b.Body);
        a.Body.SetActive(true);
        if (recordUndo) props.PushUndo(new ActionUndo("Undone: slider", () => { if (a.Alive) a.Go.RemoveComponent(j); }));
        return $"{a.Go.Name} slides along {b.Go.Name}";
    }

    public string Hinge(Prop a, Float3 localA, Float3 localAxis, Prop b, bool recordUndo = true)
    {
        HingeJoint h = a.Go.AddComponent<HingeJoint>();
        h.Anchor = localA;
        h.Axis = Float3.Normalize(localAxis);
        h.ConnectedBody = b.Body;
        CollisionPairs.Ignore(a.Body, b.Body);
        a.Body.SetActive(true);
        if (recordUndo) props.PushUndo(new ActionUndo("Undone: hinge", () => { if (a.Alive) a.Go.RemoveComponent(h); }));
        return $"Hinged {a.Go.Name} to {b.Go.Name}";
    }

    public string Stack(Prop src, Float3 normal, int count, bool weld)
    {
        // Snap the stacking direction to the prop's nearest local axis so copies line up like GMod's stacker.
        Float3 ln = src.Go.Transform.InverseTransformDirection(normal);
        Float3 axis = MathF.Abs(ln.X) > MathF.Abs(ln.Y) && MathF.Abs(ln.X) > MathF.Abs(ln.Z) ? new(MathF.Sign(ln.X), 0, 0)
            : MathF.Abs(ln.Y) > MathF.Abs(ln.Z) ? new(0, MathF.Sign(ln.Y), 0) : new(0, 0, MathF.Sign(ln.Z));
        Float3 ext = Extent(src);
        float step = MathF.Abs(axis.X) * ext.X + MathF.Abs(axis.Y) * ext.Y + MathF.Abs(axis.Z) * ext.Z;
        Prop below = src;
        var made = new List<Prop>();
        for (int i = 1; i <= Math.Clamp(count, 1, 20); i++)
        {
            Float3 pos = src.Go.Transform.TransformPoint(axis * step * i);
            Prop c = props.Spawn(src.Kind, pos, 0f, recordUndo: false);
            c.Go.Transform.Rotation = src.Go.Transform.Rotation;
            c.SetColor(src.BaseColor);
            c.SetSurface(SurfaceMaterial.Get(src.Surface));
            if (src.Entity is { } ent && ent.StartsWith("mdl_")) EntityCatalog.Decorate(Game!, c, ent);
            if (src.Frozen) c.SetFrozen(true);
            if (weld) Weld(below, c, recordUndo: false);
            made.Add(c);
            below = c;
        }
        props.PushUndo(new ActionUndo($"Undone: stack of {made.Count}", () => { foreach (Prop p in made) if (p.Alive) props.Remove(p); }));
        return $"Stacked {made.Count} × {src.Go.Name.Split(" #")[0]}";
    }

    public static SandboxGame? Game { get; set; }

    private static Float3 Extent(Prop p)
    {
        if (ModelProps.Find(p.Entity) is { } d) return d.Collider;
        Float3 s = PropManager.SizeOf(p.Kind);
        return p.Kind switch { PropKind.Sphere => Float3.One, PropKind.BigBall => new(2, 2, 2), PropKind.Barrel => new(0.8f, 1.1f, 0.8f), PropKind.Pole => new(0.16f, 3f, 0.16f), _ => s };
    }
}

public sealed class NoCollideLink : MonoBehaviour { public Prop? Other; }

/// <summary>Spring band: pulls two anchor points together past its rest length (damped).</summary>
public sealed class ElasticBand : MonoBehaviour
{
    public Prop? Other;
    public Float3 LocalA, LocalB;
    public float Strength = 60f, Rest = 1f;
    public RopeVisual? Visual;

    public override void FixedUpdate()
    {
        Rigidbody3D? a = GetComponent<Rigidbody3D>();
        if (a is null || Other is not { Alive: true } o) return;
        Float3 pa = Transform.TransformPoint(LocalA), pb = o.Go.Transform.TransformPoint(LocalB);
        Float3 d = pb - pa;
        float len = Float3.Length(d);
        if (len < 1e-4f || len <= Rest) return;
        Float3 dir = d / len;
        float rel = Float3.Dot(o.Body.LinearVelocity - a.LinearVelocity, dir);
        float mass = MathF.Min(a.Mass, o.Body.Mass);
        float f = (Strength * (len - Rest) + 2f * MathF.Sqrt(Strength) * rel) * mass;
        a.AddForceAtPosition(dir * f, pa);
        o.Body.AddForceAtPosition(-dir * f, pb);
        a.SetActive(true); o.Body.SetActive(true);
    }
}

/// <summary>Holds its prop at a target height; up/down keys move the target.</summary>
public sealed class Hoverball : MonoBehaviour
{
    public KeyCode Up, Down;
    public float Strength = 1f, Target;
    public Float3 LocalPoint;
    private GameObject? _visual;

    public static Hoverball Attach(PropManager props, Prop p, Float3 point, Float3 normal, float strength, KeyCode up, KeyCode down)
    {
        Hoverball h = p.Go.AddComponent<Hoverball>();
        h.Up = up; h.Down = down; h.Strength = strength;
        h.LocalPoint = p.Go.Transform.InverseTransformPoint(point);
        h.Target = p.Go.Transform.Position.Y + 0.3f;
        var v = new GameObject("Hoverball");
        v.SetParent(p.Go, false);
        v.Transform.Position = point + normal * 0.12f;
        MeshRenderer r = v.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.Sphere(0.14f);
        var m = Mats.New(props.Shader);
        m.SetColor("_MainColor", new Color(0.35f, 0.75f, 1f, 1f));
        m.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 1.6f, 1f));
        r.Material = m;
        h._visual = v;
        return h;
    }

    public override void FixedUpdate()
    {
        Rigidbody3D? b = GetComponent<Rigidbody3D>();
        if (b is null || b.MotionType == MotionType.Static) return;
        float dt = Time.FixedDeltaTime;
        if (KeyState.Held(Up)) Target += 2.5f * dt;
        if (KeyState.Held(Down)) Target -= 2.5f * dt;
        float g = -(GameObject.Scene?.Physics.Gravity.Y ?? -9.81f);
        float err = Target - Transform.Position.Y, vy = b.LinearVelocity.Y;
        float k = 18f * Strength, c = 2f * MathF.Sqrt(k);
        b.AddForce(new Float3(-b.LinearVelocity.X * 0.6f, g + k * err - c * vy, -b.LinearVelocity.Z * 0.6f) * b.Mass);
        b.SetActive(true);
    }

    public void Detach()
    {
        if (_visual is not null && _visual.IsValid()) _visual.Scene?.Remove(_visual);
        if (GameObject.IsValid()) GameObject.RemoveComponent(this);
    }
}

/// <summary>Sprays sparks along its normal while its key is held.</summary>
public sealed class Emitter : MonoBehaviour
{
    public KeyCode Key;
    public float Rate = 8f;
    public Float3 LocalPoint, LocalNormal;
    public bool ForceOn;
    private float _t;
    private GameObject? _visual;
    public static Fx? Fx;

    public static Emitter Attach(PropManager props, Prop p, Float3 point, Float3 normal, float rate, KeyCode key)
    {
        Emitter e = p.Go.AddComponent<Emitter>();
        e.Key = key; e.Rate = rate;
        e.LocalPoint = p.Go.Transform.InverseTransformPoint(point);
        e.LocalNormal = p.Go.Transform.InverseTransformDirection(normal);
        var v = new GameObject("Emitter");
        v.SetParent(p.Go, false);
        v.Transform.Position = point + normal * 0.05f;
        v.Transform.Up = normal;
        MeshRenderer r = v.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.RoundedCylinder(0.09f, 0.1f, 0.02f, 20);
        var m = Mats.New(props.Shader);
        m.SetColor("_MainColor", new Color(0.95f, 0.55f, 0.15f, 1f));
        r.Material = m;
        e._visual = v;
        return e;
    }

    public bool Firing => ForceOn || KeyState.Held(Key);

    public override void Update()
    {
        if (!Firing || Fx is null) return;
        _t -= Time.DeltaTime;
        if (_t > 0f) return;
        _t = 1f / MathF.Max(0.5f, Rate);
        Fx.Sparks(Transform.TransformPoint(LocalPoint), Float3.Normalize(Transform.TransformDirection(LocalNormal)), 5);
    }

    public void Detach()
    {
        if (_visual is not null && _visual.IsValid()) _visual.Scene?.Remove(_visual);
        if (GameObject.IsValid()) GameObject.RemoveComponent(this);
    }
}
