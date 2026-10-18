using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// The local player's visible body, built from the sculpted ragdoll limb meshes and posed procedurally with
/// two-bone IK: walk/crouch legs on foot, and a seated driving pose with hands on the steering wheel in vehicles.
/// In first person the head, torso and arms are hidden from the camera so you see your legs and feet when looking
/// down (and the wheel and your hands in a vehicle).
/// </summary>
public sealed class PlayerAvatar
{
    private sealed class Seg
    {
        public GameObject Go = null!;
        public List<MeshRenderer> R = [];
    }

    private readonly SandboxGame _g;
    private GameObject? _root;
    private Seg _pelvis = null!, _torso = null!, _head = null!;
    private readonly Seg[] _upperArm = new Seg[2], _lowerArm = new Seg[2], _upperLeg = new Seg[2], _lowerLeg = new Seg[2];
    private float _phase, _move;
    private Float3 _lastFeet;
    private bool _built;
    public static readonly Color Shirt = new(0.22f, 0.36f, 0.62f, 1f), Pants = new(0.16f, 0.17f, 0.2f, 1f), Skin = new(0.86f, 0.68f, 0.54f, 1f);

    private const float UpperLeg = 0.44f, LowerLeg = 0.44f, UpperArm = 0.32f, LowerArm = 0.30f;

    public PlayerAvatar(SandboxGame g) { _g = g; }
    /// <summary>Screenshot overrides: fake walking speed / crouch amount (&lt;0 = off).</summary>
    public float DebugSpeed = -1f, DebugCrouch = -1f;
    public Float3 DebugOffset; public float DebugYaw;

    private void Build()
    {
        _built = true;
        _root = new GameObject("PlayerAvatar");
        _pelvis = Make("Pelvis", Float3.Zero);
        _torso = Make("Torso", new Float3(0f, 0.27f, 0f));
        _head = Make("Head", new Float3(0f, 0.14f, 0f));
        for (int i = 0; i < 2; i++)
        {
            string s = i == 0 ? "L" : "R";
            _upperArm[i] = Make($"Upper arm {s}", new Float3(0f, -0.16f, 0f));
            _lowerArm[i] = Make($"Lower arm {s}", new Float3(0f, -0.16f, 0f));
            _upperLeg[i] = Make($"Upper leg {s}", new Float3(0f, -0.22f, 0f));
            _lowerLeg[i] = Make($"Lower leg {s}", new Float3(0f, -0.22f, 0f));
        }
        _g.World.Add(_root);
    }

    private Seg Make(string limb, Float3 meshOffset)
    {
        var seg = new Seg { Go = new GameObject(limb) };
        seg.Go.SetParent(_root!, false);
        var (main, det) = Humans.Limb(limb);
        Color mc = limb.StartsWith("Lower arm") || limb == "Head" ? Skin : limb.StartsWith("Torso") || limb.Contains("arm") ? Shirt : Pants;
        Add(seg, main, meshOffset, mc);
        foreach (var d in det)
        {
            Color c = d.Tint switch
            {
                Humans.Tint.Skin => Skin, Humans.Tint.Shoe => new Color(0.16f, 0.11f, 0.08f, 1f), Humans.Tint.Hair => new Color(0.2f, 0.13f, 0.08f, 1f),
                Humans.Tint.Eye or Humans.Tint.Belt => new Color(0.06f, 0.06f, 0.06f, 1f), Humans.Tint.Pants => new Color(Pants.R * 0.8f, Pants.G * 0.8f, Pants.B * 0.8f, 1f),
                Humans.Tint.Cuff => new Color(Shirt.R * 0.75f, Shirt.G * 0.75f, Shirt.B * 0.75f, 1f), _ => Shirt,
            };
            Add(seg, d.Mesh, meshOffset + d.Offset, c);
        }
        return seg;
    }

    private void Add(Seg seg, Mesh mesh, Float3 at, Color c)
    {
        var go = new GameObject("m");
        go.SetParent(seg.Go, false);
        go.Transform.LocalPosition = at;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.Mesh = mesh;
        var m = Mats.New(_g.Shader); m.SetColor("_MainColor", c); r.Material = m;
        seg.R.Add(r);
    }

    private static void Show(Seg s, bool on) { foreach (var r in s.R) if (r.Enabled != on) r.Enabled = on; }

    /// <summary>Bone frame whose local -Y points along <paramref name="dir"/> (limb meshes hang down -Y), front toward <paramref name="fwd"/>.</summary>
    private static Quaternion Along(Float3 dir, Float3 fwd)
    {
        Float3 up = -Float3.Normalize(dir);
        Float3 f = fwd - up * Float3.Dot(fwd, up);
        if (Float3.Length(f) < 1e-4f) f = Float3.Cross(up, Float3.UnitX);
        return Quaternion.LookRotation(Float3.Normalize(f), up);
    }

    /// <summary>Two-bone IK: returns the middle joint for root a reaching b, bending toward pole.</summary>
    private static Float3 Knee(Float3 a, Float3 b, float l1, float l2, Float3 pole)
    {
        Float3 ab = b - a; float d = Math.Clamp(Float3.Length(ab), 0.01f, l1 + l2 - 0.002f);
        Float3 dir = Float3.Normalize(ab);
        float x = (l1 * l1 + d * d - l2 * l2) / (2f * d), h = MathF.Sqrt(MathF.Max(0f, l1 * l1 - x * x));
        Float3 p = pole - dir * Float3.Dot(pole, dir);
        p = Float3.Length(p) < 1e-4f ? Float3.UnitY : Float3.Normalize(p);
        return a + dir * x + p * h;
    }

    private static void Place(Seg s, Float3 at, Quaternion q) { s.Go.Transform.Position = at; s.Go.Transform.Rotation = q; }

    private void Limb(Seg up, Seg lo, Float3 root, Float3 target, float l1, float l2, Float3 pole, Float3 fwd)
    {
        Float3 k = Knee(root, target, l1, l2, pole);
        Float3 end = k + Float3.Normalize(target - k) * l2;
        Place(up, root, Along(k - root, fwd));
        Place(lo, k, Along(end - k, fwd));
    }

    /// <summary>Call once per frame after the player / vehicles have moved.</summary>
    public void Tick(float dt, bool hidden)
    {
        if (_built && (_root is null || !_root.IsValid() || _root.Scene == null)) _built = false;
        if (!_built) { if (hidden) return; Build(); }
        bool visible = !hidden;
        Buggy? car = _g.Vehicles.Driving;
        bool firstPerson = car is null || _g.Vehicles.FirstPerson;
        // First person: hide head/torso (camera sits inside them); on foot the view model shows the arms instead.
        Show(_head, visible && !firstPerson);
        Show(_torso, visible && !firstPerson);
        for (int i = 0; i < 2; i++)
        {
            bool arms = visible && (!firstPerson || car is not null);
            Show(_upperArm[i], arms); Show(_lowerArm[i], arms);
            Show(_upperLeg[i], visible && !(firstPerson && car is null)); Show(_lowerLeg[i], visible);
        }
        Show(_pelvis, visible && !(firstPerson && car is null));
        if (!visible) return;
        if (car is not null) PoseSeated(car); else PoseStanding(dt);
    }

    private float FpLead => _g.Vehicles.Driving is null && DebugOffset == Float3.Zero ? 0.14f : 0f;
    private void PoseStanding(float dt)
    {
        Player p = _g.Player;
        Float3 feet = p.FeetPosition;
        Float3 vel = (feet - _lastFeet) / MathF.Max(dt, 1e-4f); _lastFeet = feet;
        float speed = Float3.Length(new Float3(vel.X, 0f, vel.Z));
        if (speed > 20f) speed = 0f; // teleports
        if (DebugSpeed >= 0f) speed = DebugSpeed;
        _move += (Math.Clamp(speed / 4f, 0f, 1f) - _move) * Math.Clamp(dt * 8f, 0f, 1f);
        _phase += dt * (3f + speed * 1.6f) * (_move > 0.02f ? 1f : 0f);
        float yaw = (p.Yaw + DebugYaw) * MathF.PI / 180f;
        Float3 fwd = new(MathF.Sin(yaw), 0f, MathF.Cos(yaw)), right = new(fwd.Z, 0f, -fwd.X);
        float crouch = DebugCrouch >= 0f ? DebugCrouch : p.CrouchBlend;
        // Body sits a little behind the camera so looking down shows the legs and feet, not the inside of the chest.
        Float3 baseAt = feet - fwd * (0.07f + 0.1f * crouch) + DebugOffset;
        Quaternion rot = Quaternion.LookRotation(fwd, Float3.UnitY);
        Float3 hips = baseAt + new Float3(0f, 1.0f - 0.42f * crouch - 0.03f * _move * MathF.Abs(MathF.Cos(_phase)), 0f);
        Place(_pelvis, hips, rot);
        Float3 tj = hips + new Float3(0f, 0.10f, 0f);
        Quaternion lean = Quaternion.LookRotation(Float3.Normalize(fwd - Float3.UnitY * (0.15f + 0.5f * crouch)), Float3.Normalize(Float3.UnitY + fwd * (0.15f + 0.5f * crouch)));
        Place(_torso, tj, lean);
        Float3 tUp = Float3.Normalize(Float3.UnitY + fwd * (0.15f + 0.5f * crouch));
        Place(_head, tj + tUp * 0.53f, rot);
        float stride = 0.32f * _move;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f, ph = _phase + (i == 0 ? 0f : MathF.PI);
            Float3 hipJ = hips + right * (0.10f * side) - new Float3(0f, 0.10f, 0f);
            float lift = MathF.Max(0f, MathF.Cos(ph)) * 0.13f * _move;
            Float3 foot = baseAt + right * (0.11f * side) + fwd * (MathF.Sin(ph) * stride + 0.05f + 0.12f * crouch + FpLead) + new Float3(0f, 0.05f + lift, 0f);
            Limb(_upperLeg[i], _lowerLeg[i], hipJ, foot, UpperLeg, LowerLeg, fwd + Float3.UnitY * 0.2f, fwd);
            Float3 sh = tj + tUp * 0.50f + right * (0.28f * side);
            Float3 hand = sh + new Float3(0f, -0.6f, 0f) + right * (0.04f * side) - fwd * (MathF.Sin(ph) * stride * 0.6f - 0.05f);
            Limb(_upperArm[i], _lowerArm[i], sh, hand, UpperArm, LowerArm, -fwd + right * side * 0.3f, fwd);
        }
    }

    private void PoseSeated(Buggy car)
    {
        Transform t = car.Go.Transform;
        Float3 P(Float3 local) => t.Position + t.Right * local.X + t.Up * local.Y + t.Forward * local.Z;
        Float3 fwd = t.Forward, up = t.Up, right = t.Right;
        Quaternion rot = Quaternion.LookRotation(fwd, up);
        Float3 hips = P(car.DriverHips);
        Place(_pelvis, hips, rot);
        Float3 tj = hips + up * 0.10f;
        Float3 tUp = Float3.Normalize(up - fwd * 0.12f); // slight recline
        Place(_torso, tj, Quaternion.LookRotation(Float3.Normalize(fwd + up * 0.12f), tUp));
        Place(_head, tj + tUp * 0.53f, rot);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Float3 hipJ = hips + right * (0.10f * side) - up * 0.04f;
            Float3 foot = hips + right * (0.14f * side) + fwd * 0.62f - up * 0.34f;
            Limb(_upperLeg[i], _lowerLeg[i], hipJ, foot, UpperLeg, LowerLeg, up + fwd * 0.5f, fwd);
            Float3 sh = tj + tUp * 0.50f + right * (0.26f * side);
            Float3 hand = car.WheelGrip(side);
            Limb(_upperArm[i], _lowerArm[i], sh, hand, UpperArm, LowerArm, -up + right * side * 0.8f, fwd);
        }
    }
}
