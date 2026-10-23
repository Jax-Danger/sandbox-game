// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Rope, ball socket, thruster, wheel, colour, material, light and duplicator tools.</summary>
public sealed partial class ToolGun
{
    private RaycastHit _lastHit;
    private Prop? _linkFirst;
    private Float3 _linkLocal;

    public float RopeSlack { get; set; } = 1.1f;
    public float ThrusterForce { get; set; } = 1.5f;
    public KeyCode ThrusterKey { get; set; } = KeyCode.T;
    public float WheelSpeed { get; set; } = 8f;
    public KeyCode WheelFwd { get; set; } = KeyCode.T;
    public KeyCode WheelBack { get; set; } = KeyCode.G;
    public float PaintHue { get; set; } = 0.58f;
    public int MaterialIndex { get; set; } = 3;
    public float LightRange { get; set; } = 8f;
    public float LightHue { get; set; } = 0.1f;

    public static KeyCode[] ThrusterKeys => BindKeys;
    public static readonly (KeyCode Fwd, KeyCode Back)[] WheelKeyPairs = [(KeyCode.T, KeyCode.G), (KeyCode.Y, KeyCode.H), (KeyCode.U, KeyCode.J), (KeyCode.Keypad8, KeyCode.Keypad2), (KeyCode.Keypad4, KeyCode.Keypad6), (KeyCode.Keypad7, KeyCode.Keypad1), (KeyCode.Keypad9, KeyCode.Keypad3)];

    /// <summary>The Duplicator's clipboard: a whole contraption, stored relative to its bottom-centre.</summary>
    public MapSave.SaveData? Dupe { get; set; }
    public string DupeName { get; set; } = "";
    public bool HasCopy => Dupe is not null;
    public Prop? PendingLink => _linkFirst is { Alive: true } ? _linkFirst : null;

    public static Color HueColor(float h, float s = 0.7f, float v = 0.95f)
    {
        h = (h % 1f + 1f) % 1f * 6f;
        int i = (int)h; float f = h - i;
        float p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        return i switch { 0 => new(v, t, p, 1), 1 => new(q, v, p, 1), 2 => new(p, v, t, 1), 3 => new(p, q, v, 1), 4 => new(t, p, v, 1), _ => new(v, p, q, 1) };
    }

    private IEnumerable<ToolSetting> ExtraSettings(ToolMode mode)
    {
        switch (mode)
        {
            case ToolMode.Rope:
                yield return new ToolSetting("Slack", 1f, 2f, () => RopeSlack, v => RopeSlack = v, v => $"{v:0.00}×", "Rope length as a multiple of the distance between the two points.");
                break;
            case ToolMode.Thruster:
                yield return new ToolSetting("Force", 0.2f, 5f, () => ThrusterForce, v => ThrusterForce = v, v => $"{v:0.0}× weight", "Thrust as a multiple of the prop's weight.");
                yield return KeyPick("Key", () => ThrusterKey, k => ThrusterKey = k, "Hold this key to fire the thruster.");
                break;
            case ToolMode.Wheel:
                yield return new ToolSetting("Speed", 1f, 25f, () => WheelSpeed, v => WheelSpeed = v, v => $"{v:0} rad/s", "Motor target speed.");
                yield return KeyPick("Forward key", () => WheelFwd, k => WheelFwd = k, "Hold to spin forward.");
                yield return KeyPick("Reverse key", () => WheelBack, k => WheelBack = k, "Hold to spin backward.");
                break;
            case ToolMode.Colour:
                yield return new ToolSetting("Hue", 0f, 1f, () => PaintHue, v => PaintHue = v, v => $"{v * 360f:0}°", "Paint colour.");
                break;
            case ToolMode.Material:
                yield return new ToolSetting("Material", 0f, SurfaceMaterial.All.Length - 1, () => MaterialIndex, v => MaterialIndex = (int)MathF.Round(v),
                    v => SurfaceMaterial.All[(int)MathF.Round(v)].Name, "Surface to apply.");
                break;
            case ToolMode.Light:
                yield return new ToolSetting("Range", 2f, 20f, () => LightRange, v => LightRange = v, v => $"{v:0} m", "How far the light reaches.");
                yield return new ToolSetting("Hue", 0f, 1f, () => LightHue, v => LightHue = v, v => $"{v * 360f:0}°", "Light colour.");
                break;
        }
    }

    private string? ExtraSecondary(Player player)
    {
        switch (Mode)
        {
            case ToolMode.Rope or ToolMode.BallSocket or ToolMode.NoCollide or ToolMode.Elastic or ToolMode.Slider or ToolMode.Hinge when PendingLink is not null:
                _linkFirst = null;
                return "Selection cleared";
            case ToolMode.Duplicator:
                if (player.Trace(120f, out RaycastHit hit) && props.FromBody(hit.Rigidbody) is { Kind: not PropKind.Limb } p)
                {
                    if (!props.Protection.CanTouch(p, out string? deny)) return deny;
                    Dupe = Dupes.Copy(Game!, p);
                    DupeName = Dupe.Props.Count > 1 ? $"{p.Go.Name} contraption" : p.Go.Name;
                    return Dupe.Props.Count > 1 ? $"Copied {Dupe.Props.Count} props" : $"Copied {p.Go.Name}";
                }
                return "Look at a prop to copy it";
            case ToolMode.Colour:
                if (player.Trace(120f, out RaycastHit h2) && props.FromBody(h2.Rigidbody) is { } cp)
                {
                    if (!props.Protection.CanTouch(cp, out string? deny2)) return deny2;
                    PaintHue = HueOf(cp.BaseColor);
                    return $"Picked colour from {cp.Go.Name}";
                }
                return null;
        }
        return null;
    }

    private static float HueOf(Color c)
    {
        float max = MathF.Max(c.R, MathF.Max(c.G, c.B)), min = MathF.Min(c.R, MathF.Min(c.G, c.B)), d = max - min;
        if (d < 1e-4f) return 0f;
        float h = max == c.R ? (c.G - c.B) / d % 6f : max == c.G ? (c.B - c.R) / d + 2f : (c.R - c.G) / d + 4f;
        return (h / 6f + 1f) % 1f;
    }

    private Float3 HitPointOn(Prop p) => _lastHit.Rigidbody is not null && ReferenceEquals(_lastHit.Rigidbody, p.Body) ? _lastHit.Point : p.Go.Transform.Position;
    private Float3 HitNormalOn(Prop p) => _lastHit.Rigidbody is not null && ReferenceEquals(_lastHit.Rigidbody, p.Body) ? _lastHit.Normal : Float3.UnitY;

    /// <summary>Lets tests aim a tool at a specific point without a raycast.</summary>
    public void SetHit(Prop p, Float3 point, Float3 normal) => _lastHit = new RaycastHit { Rigidbody = p.Body, Point = point, Normal = normal };

    private string? ApplyExtra(Prop prop)
    {
        Float3 point = HitPointOn(prop), normal = HitNormalOn(prop);
        switch (Mode)
        {
            case ToolMode.Rope:
            case ToolMode.BallSocket:
                if (PendingLink is null)
                {
                    _linkFirst = prop;
                    _linkLocal = prop.Go.Transform.InverseTransformPoint(point);
                    return $"{InfoFor(Mode).Name}: now click the second point";
                }
                if (ReferenceEquals(_linkFirst, prop)) return "Pick a different prop";
                {
                    Prop a = _linkFirst!;
                    _linkFirst = null;
                    return Mode == ToolMode.Rope ? Rope(a, _linkLocal, prop, prop.Go.Transform.InverseTransformPoint(point))
                                                 : BallSocket(a, _linkLocal, prop);
                }

            case ToolMode.Thruster:
            {
                Thruster t = Thruster.Attach(props, prop, point, normal, ThrusterForce, ThrusterKey);
                props.PushUndo(new ActionUndo($"Undone: thruster on {prop.Go.Name}", () => t.Detach()));
                return $"Thruster on {prop.Go.Name} (hold {t.Key})";
            }

            case ToolMode.Wheel:
                return AttachWheel(prop, point, normal);

            case ToolMode.Colour:
            {
                Color old = prop.BaseColor, c = HueColor(PaintHue);
                prop.SetColor(c);
                props.PushUndo(new ActionUndo($"Undone: paint on {prop.Go.Name}", () => { if (prop.Alive) prop.SetColor(old); }));
                return $"Painted {prop.Go.Name}";
            }

            case ToolMode.Material:
            {
                string old = prop.Surface;
                SurfaceMaterial m = SurfaceMaterial.All[Math.Clamp(MaterialIndex, 0, SurfaceMaterial.All.Length - 1)];
                prop.SetSurface(m);
                props.PushUndo(new ActionUndo($"Undone: material on {prop.Go.Name}", () => { if (prop.Alive) prop.SetSurface(SurfaceMaterial.Get(old)); }));
                return $"{prop.Go.Name} is now {m.Name}";
            }

            case ToolMode.Light:
            {
                GameObject go = AttachLight(prop, point + normal * 0.08f, LightRange, HueColor(LightHue, 0.5f, 1f));
                props.PushUndo(new ActionUndo($"Undone: light on {prop.Go.Name}", () => { if (go.IsValid()) go.Scene?.Remove(go); }));
                return $"Light attached to {prop.Go.Name}";
            }
            case ToolMode.Duplicator:
                return null; // handled in Fire (paste) / Secondary (copy)
        }
        return null;
    }

    public GameObject AttachLight(Prop prop, Float3 worldPos, float range, Color color)
    {
        var go = new GameObject("Light");
        go.SetParent(prop.Go, false);
        go.Transform.Position = worldPos;
        Light l = go.AddLight(LightType.Point);
        l.Range = range;
        l.Color = color;
        l.Intensity = 4f;
        l.CastShadows = false;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.Sphere(0.08f);
        var mat = Mats.New(props.Shader);
        mat.SetColor("_MainColor", color);
        try { mat.SetTexture("_EmissionTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        mat.SetFloat("_EmissionIntensity", 3f);
        r.Material = mat;
        return go;
    }

    public string Rope(Prop a, Float3 localA, Prop b, Float3 localB, float? length = null, bool recordUndo = true)
    {
        Float3 wa = a.Go.Transform.TransformPoint(localA), wb = b.Go.Transform.TransformPoint(localB);
        float len = length ?? MathF.Max(0.2f, Float3.Length(wb - wa) * RopeSlack);
        DistanceLimitConstraint c = a.Go.AddComponent<DistanceLimitConstraint>();
        c.Anchor = localA;
        c.ConnectedAnchor = localB;
        c.MinDistance = 0f;
        c.MaxDistance = len;
        c.TargetDistance = len;
        c.ConnectedBody = b.Body;
        RopeVisual v = a.Go.AddComponent<RopeVisual>();
        v.Init(localA, b, localB);
        a.Body.SetActive(true);
        b.Body.SetActive(true);
        if (recordUndo) props.PushUndo(new ActionUndo($"Undone: rope", () => { if (a.Alive) { a.Go.RemoveComponent(c); v.Destroy(); } }));
        return $"Roped {a.Go.Name} to {b.Go.Name} ({len:0.0} m)";
    }

    public string BallSocket(Prop a, Float3 localA, Prop b)
    {
        BallSocketConstraint s = a.Go.AddComponent<BallSocketConstraint>();
        s.Anchor = localA;
        s.ConnectedBody = b.Body;
        CollisionPairs.Ignore(a.Body, b.Body);
        a.Body.SetActive(true);
        props.PushUndo(new ActionUndo($"Undone: ball socket", () => { if (a.Alive) a.Go.RemoveComponent(s); if (a.Alive && b.Alive) CollisionPairs.Enable(a.Body, b.Body); }));
        return $"Ball-socketed {a.Go.Name} to {b.Go.Name}";
    }

    public string AttachWheel(Prop prop, Float3 point, Float3 normal, float? speed = null, (KeyCode, KeyCode)? keys = null, bool recordUndo = true)
    {
        const float radius = 0.4f, width = 0.18f;
        var go = new GameObject($"Wheel on {prop.Go.Name}");
        go.Transform.Position = point + normal * (width / 2f + 0.02f);
        // Cylinder's long axis is Y: point it along the surface normal (the wheel's axle).
        go.Transform.Up = normal;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.Tyre(radius, width);
        var hub = new GameObject("Rim");
        hub.SetParent(go, false);
        MeshRenderer hr = hub.AddComponent<MeshRenderer>();
        hr.Mesh = PropMeshes.Rim(radius * 0.55f, width + 0.02f, 5);
        var hm = Mats.New(props.Shader); hm.SetColor("_MainColor", new Color(0.6f, 0.62f, 0.65f, 1f)); hr.Material = hm;
        var mat = Mats.New(props.Shader);
        var c = new Color(0.12f, 0.12f, 0.13f, 1f);
        mat.SetColor("_MainColor", c);
        r.Material = mat;
        CylinderCollider col = go.AddComponent<CylinderCollider>();
        col.Radius = radius;
        col.Height = width;
        Rigidbody3D body = go.AddBody();
        body.Mass = 1f;
        body.Friction = 1f;
        prop.Go.Scene!.Add(go);
        Prop wheel = props.Adopt(go, body, mat, c, PropKind.Limb, radius);
        HingeJoint h = go.AddComponent<HingeJoint>();
        h.Anchor = Float3.Zero;
        h.Axis = Float3.UnitY;
        h.HasMotor = true;
        h.MotorMaxForce = 40f;
        h.MotorTargetVelocity = 0f;
        h.ConnectedBody = prop.Body;
        CollisionPairs.Ignore(body, prop.Body);
        var (fwd, back) = keys ?? (WheelFwd, WheelBack);
        WheelMotor m = go.AddComponent<WheelMotor>();
        m.Joint = h; m.Fwd = fwd; m.Back = back; m.Speed = speed ?? WheelSpeed;
        if (recordUndo) props.PushUndo(new ActionUndo($"Undone: wheel", () => props.Remove(wheel)));
        return $"Wheel on {prop.Go.Name} ({fwd} / {back})";
    }

    private string Paste(RaycastHit hit)
    {
        if (Dupe is not { } d) return "Right click a prop to copy it first";
        List<Prop> made = Dupes.Paste(Game!, d, hit.Point, Game!.Player.Yaw);
        return made.Count == 1 ? $"Pasted {made[0].Go.Name}" : $"Pasted {made.Count} props";
    }
}

public sealed class ActionUndo(string label, Action undo) : IUndoable
{
    public string Label => label;
    public void Undo() => undo();
}

/// <summary>Draws a rope between two anchors (on this object and on another prop).</summary>
public sealed class RopeVisual : Component
{
    private Float3 _localA, _localB;
    private Prop? _b;
    private GameObject? _lineGo;
    public Float3 LocalA => _localA;
    public Float3 LocalB => _localB;
    public Prop? Other => _b;
    private static Material? _mat;

    public void Init(Float3 localA, Prop b, Float3 localB)
    {
        _localA = localA; _b = b; _localB = localB;
        if (_mat is null)
        {
            _mat = Mats.New(Shader.LoadDefault(DefaultShader.Lit));
            _mat.SetColor("_MainColor", new Color(0.55f, 0.42f, 0.28f, 1f));
            try { _mat.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        }
        _lineGo = new GameObject("Rope");
        MeshRenderer r = _lineGo.AddComponent<MeshRenderer>();
        r.Mesh = Fx.UnitBeam(0.03f);
        r.Material = _mat;
        GameObject.Scene?.Add(_lineGo);
        Update();
    }

    public void Destroy()
    {
        RemoveLine();
        if (GameObject.IsValid()) GameObject.RemoveComponent(this);
    }

    private void RemoveLine()
    {
        if (_lineGo is not null && _lineGo.IsValid()) _lineGo.Scene?.Remove(_lineGo);
        _lineGo = null;
    }

    /// <summary>The owner prop left the scene (removed, cleared, map change): take the separate line object with it.</summary>
    public override void OnDisable() => RemoveLine();

    public override void Update()
    {
        if (_lineGo is null) return;
        if (_b is null || !_b.Alive || !GameObject.EnabledInHierarchy) { Destroy(); return; }
        Fx.Stretch(_lineGo, Transform.TransformPoint(_localA), _b.Go.Transform.TransformPoint(_localB));
    }
}

/// <summary>Key-bound thruster: pushes the prop opposite to the surface normal while its key is held.</summary>
public sealed class Thruster : Component
{
    public KeyCode Key;
    public float Multiple = 1.5f;
    public Float3 LocalDir;
    public bool ForceOn; // tests / always-on
    private Rigidbody3D? _body;
    private GameObject? _visual;
    public GameObject? Visual => _visual;

    public static Thruster Attach(PropManager props, Prop p, Float3 point, Float3 normal, float multiple, KeyCode key)
    {
        Thruster t = p.Go.AddComponent<Thruster>();
        t._body = p.Body;
        t.Key = key;
        t.Multiple = multiple;
        t.LocalDir = p.Go.Transform.InverseTransformDirection(-normal);
        var v = new GameObject("Thruster");
        v.SetParent(p.Go, false);
        v.Transform.Position = point + normal * 0.1f;
        v.Transform.Up = normal;
        MeshRenderer r = v.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.Lathe("thruster", [(0.0001f, -0.1f), (0.07f, -0.1f), (0.09f, -0.08f), (0.09f, 0.0f), (0.06f, 0.04f), (0.07f, 0.08f), (0.11f, 0.11f), (0.1f, 0.12f), (0.05f, 0.09f), (0.0001f, 0.08f)], 28);
        var m = Mats.New(props.Shader);
        m.SetColor("_MainColor", new Color(0.3f, 0.32f, 0.36f, 1f));
        r.Material = m;
        t._visual = v;
        return t;
    }

    public bool Firing => ForceOn || KeyState.Held(Key);

    public override void FixedUpdate()
    {
        if (_body is null || !Firing) return;
        Float3 dir = Transform.TransformDirection(LocalDir);
        _body.AddForce(dir * _body.Mass * 9.81f * Multiple);
        _body.SetActive(true);
    }

    public void Detach()
    {
        if (_visual is not null && _visual.IsValid()) _visual.Scene?.Remove(_visual);
        if (GameObject.IsValid()) GameObject.RemoveComponent(this);
    }
}

/// <summary>Drives a wheel's hinge motor from two keys.</summary>
public sealed class WheelMotor : Component
{
    public HingeJoint? Joint;
    public KeyCode Fwd, Back;
    public float Speed = 8f;
    public float ForceInput; // tests: -1..1 override
    public override void Update()
    {
        if (Joint is null || !Joint.IsValid()) return;
        float dir = ForceInput != 0f ? ForceInput : (KeyState.Held(Fwd) ? 1f : 0f) - (KeyState.Held(Back) ? 1f : 0f);
        Joint.MotorTargetVelocity = dir * Speed;
        if (dir != 0f) GetComponent<Rigidbody3D>()?.SetActive(true);
    }
}
