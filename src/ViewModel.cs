using System.Text.Json;
using System.Text.Json.Serialization;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

// ───────────────────────────── Pose / clip data ─────────────────────────────

/// <summary>Offset of one bone from its rest pose: position (metres, bone-local) and rotation (Euler degrees).</summary>
public struct BonePose
{
    public Float3 Pos;
    public Float3 Rot;
    public BonePose(Float3 pos, Float3 rot) { Pos = pos; Rot = rot; }
    public static BonePose Lerp(BonePose a, BonePose b, float t) => new(a.Pos + (b.Pos - a.Pos) * t, a.Rot + (b.Rot - a.Rot) * t);
    public static BonePose operator +(BonePose a, BonePose b) => new(a.Pos + b.Pos, a.Rot + b.Rot);
    public static BonePose operator *(BonePose a, float w) => new(a.Pos * w, a.Rot * w);
}

public enum Ease { Linear, In, Out, InOut, Back }

/// <summary>One keyframe: time in seconds, easing used to reach it from the previous key, and per-bone offsets.</summary>
public sealed class Keyframe
{
    public float T { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public Ease Ease { get; set; } = Ease.InOut;
    public Dictionary<string, BoneKey> Bones { get; set; } = [];
}

/// <summary>JSON-friendly bone key: "pos": [x,y,z], "rot": [x,y,z] (degrees).</summary>
public sealed class BoneKey
{
    public float[] Pos { get; set; } = [0, 0, 0];
    public float[] Rot { get; set; } = [0, 0, 0];
    public BonePose ToPose() => new(new Float3(Pos[0], Pos[1], Pos[2]), new Float3(Rot[0], Rot[1], Rot[2]));
}

/// <summary>A keyframed animation clip. Bones not mentioned in a key are at rest (zero offset) for that key.</summary>
public sealed class VmClip
{
    public float Length { get; set; } = 1f;
    public bool Loop { get; set; }
    /// <summary>Holds the last pose instead of blending back to rest (chained clips like shell-by-shell reloads).</summary>
    public bool Hold { get; set; }
    public List<Keyframe> Keys { get; set; } = [];

    public static float Apply(Ease e, float t) => e switch
    {
        Ease.Linear => t,
        Ease.In => t * t,
        Ease.Out => 1f - (1f - t) * (1f - t),
        Ease.Back => 1f + 2.70158f * MathF.Pow(t - 1f, 3f) + 1.70158f * MathF.Pow(t - 1f, 2f),
        _ => t * t * (3f - 2f * t),
    };

    /// <summary>Samples the offset of <paramref name="bone"/> at time <paramref name="time"/>.</summary>
    public BonePose Sample(string bone, float time)
    {
        if (Keys.Count == 0) return default;
        if (Loop && Length > 0f) time %= Length;
        BonePose At(Keyframe k) => k.Bones.TryGetValue(bone, out BoneKey? b) ? b.ToPose() : default;
        if (time <= Keys[0].T) return At(Keys[0]);
        for (int i = 1; i < Keys.Count; i++)
        {
            if (time > Keys[i].T) continue;
            Keyframe a = Keys[i - 1], b = Keys[i];
            float u = b.T > a.T ? (time - a.T) / (b.T - a.T) : 1f;
            return BonePose.Lerp(At(a), At(b), Apply(b.Ease, u));
        }
        return Loop ? BonePose.Lerp(At(Keys[^1]), At(Keys[0]), Apply(Keys[0].Ease, Math.Clamp((time - Keys[^1].T) / MathF.Max(0.001f, Length - Keys[^1].T), 0f, 1f))) : At(Keys[^1]);
    }
}

/// <summary>Fluent builder for code-defined clips.</summary>
public sealed class ClipBuilder
{
    private readonly VmClip _c = new();
    public ClipBuilder(float length, bool loop = false) { _c.Length = length; _c.Loop = loop; }
    public ClipBuilder Key(float t, Ease ease = Ease.InOut, params (string bone, Float3 pos, Float3 rot)[] bones)
    {
        var k = new Keyframe { T = t, Ease = ease };
        foreach (var (bone, pos, rot) in bones)
            k.Bones[bone] = new BoneKey { Pos = [pos.X, pos.Y, pos.Z], Rot = [rot.X, rot.Y, rot.Z] };
        _c.Keys.Add(k);
        return this;
    }
    public VmClip Build() => _c;
}

// ───────────────────────────── Rig definition ─────────────────────────────

/// <summary>Surface finish of a primitive part (roughness / metallic for the PBR surface texture).</summary>
public enum Finish { Polymer, Metal, Fabric, Leather, Wood, Rubber, Paint, Gloss, Worn }

/// <summary>
/// A primitive mesh attached to a bone (used when no user model is installed). Shapes: 'b' rounded box, 'c' rounded
/// cylinder (along Y), 's' sphere, 'e' ellipsoid (Size = diameters), 't' tapered capsule along +Z
/// (Size = r0, r1, length). <paramref name="Q"/> overrides <paramref name="Rot"/> when set.
/// </summary>
public sealed record VmPart(string Bone, char Shape, Float3 Size, Float3 Offset, Color Color, Float3 Rot = default, bool Emissive = false,
    Finish Finish = Finish.Polymer, Quaternion? Q = null, Mesh? M = null);

/// <summary>Describes a first-person view model: bone rest poses, primitive parts, clips and handling feel.</summary>
public sealed class ViewModelDef
{
    public string Id = "";
    /// <summary>Bone name → (parent bone, rest local position, rest local Euler degrees). Order = creation order (parents first).</summary>
    public List<(string Bone, string Parent, Float3 Pos, Float3 Rot)> Bones = [];
    public List<VmPart> Parts = [];
    public Dictionary<string, VmClip> Clips = [];
    public float RecoilKick = 1f;
    /// <summary>Handling weight: heavier guns sway more slowly, lag further and bob less sharply.</summary>
    public float Weight = 1f;
    /// <summary>Right mouse aims down the "sight" bone (firearms only).</summary>
    public bool CanAim;
    /// <summary>Camera zoom while aiming (FOV is divided by this).</summary>
    public float AimZoom = 1.15f;
    /// <summary>When set, aiming shifts the rig by this (a shoulder raise) instead of centring the sight; the crosshair stays.
    /// Used by long launchers whose tube would otherwise run through the middle of the view.</summary>
    public Float3? AimShift;
    /// <summary>Extra vm_root pose blended in while aiming, on top of the sight-centring offset (drop/tilt the gun so the
    /// grip hand clears the sight line).</summary>
    public BonePose AimExtra;
    /// <summary>Extra vm_root offset while sprinting (gun lowered and angled).</summary>
    public BonePose SprintPose = new(new Float3(0.035f, -0.015f, -0.03f), new Float3(9f, -22f, 12f));

    /// <summary>Forearm "look-at" IK: the forearm bone sits at the wrist bone and points at a fixed elbow (vm_root space),
    /// so arms never swing into the camera when a hand travels (reloads, slide racks).</summary>
    public List<(string Forearm, string Wrist, Float3 Elbow, Float3 Up)> Ik = [];

    public static readonly string[] StandardBones = ["vm_root", "arm_r", "hand_r", "weapon", "muzzle", "arm_l", "hand_l"];
}

/// <summary>Per-frame inputs that drive the procedural layers.</summary>
public readonly record struct VmInput(Float2 Mouse, float Speed, Float2 Move = default, bool Grounded = true, float VerticalVelocity = 0f,
    float Landing = 0f, bool Sprint = false, float Crouch = 0f, bool Aim = false, float Step = -1f);

// ───────────────────────────── Runtime rig + animator ─────────────────────────────

/// <summary>
/// First-person view model: a bone hierarchy under the camera with primitive meshes (or a user model) on the bones,
/// driven by layered animation: looping idle, a blended one-shot clip (draw/holster/reload/inspect/fire), then
/// procedural layers (all springs, sub-stepped so they are frame-rate independent): weight-based mouse sway with
/// lag, strafe tilt, walk/sprint bob, jump lift and landing dip, sprint and crouch poses, aim-down-sights, a recoil
/// spring with a little random yaw/roll, and an occasional idle fidget.
/// </summary>
public sealed class ViewModel
{
    /// <summary>The close, brightly lit view model makes saturated paints glow; pull saturation and value down in proportion to chroma.</summary>
    static Color VmTone(Color c)
    {
        float mx = MathF.Max(c.R, MathF.Max(c.G, c.B)), mn = MathF.Min(c.R, MathF.Min(c.G, c.B));
        float sat = mx > 1e-4f ? (mx - mn) / mx : 0f;
        float l = c.R * 0.2126f + c.G * 0.7152f + c.B * 0.0722f;
        float ks = 1f - 0.22f * sat, kv = 1f - 0.2f * sat;
        return new Color((l + (c.R - l) * ks) * kv, (l + (c.G - l) * ks) * kv, (l + (c.B - l) * ks) * kv, c.A);
    }

    public ViewModelDef Def { get; }
    public GameObject Root { get; }
    public Dictionary<string, GameObject> Bones { get; } = [];
    private readonly Dictionary<string, (Float3 Pos, Float3 Rot)> _rest = [];
    public bool UsingUserModel { get; }
    /// <summary>Primitive parts are authored at roughly real-world size and shrunk to sit nicely on screen.</summary>
    public const float PartScale = 1f;

    private string? _oneShot;
    private float _oneShotTime, _oneShotWeight;
    // Cross-fade: the clip that was interrupted keeps playing underneath while it fades out (no snapping).
    private string? _prevShot;
    private float _prevTime, _prevWeight;
    private readonly Dictionary<string, int> _variant = [];
    /// <summary>Embedded clips of a user model (glb/fbx), by name; played instead of the built-in clip of the same name.</summary>
    private readonly Dictionary<string, AnimationClip> _embedded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> _nodes = [];
    public IReadOnlyCollection<string> EmbeddedClips => _embedded.Keys;
    private float _time;
    // Springs: sway (position xy + rotation), recoil (pitch/back), recoil yaw/roll, vertical (jump/land).
    private Float2 _sway, _swayVel;
    private float _recoil, _recoilVel, _kickYaw, _kickYawVel, _kickRoll, _kickRollVel;
    private float _lift, _liftVel;
    private float _bobPhase, _bobAmount, _strafe, _sprint, _crouch, _aim;
    private float _fidgetIn = 9f, _fidgetT = -1f;
    private readonly Random _rng = new();
    /// <summary>Camera-local vm_root offset that centres the sight (computed from the rest pose).</summary>
    public Float3 AimOffset { get; }
    /// <summary>0..1 aim-down-sights blend (for crosshair fade and camera zoom).</summary>
    public float AimBlend => _aim;
    public float SprintBlend => _sprint;
    public string? CurrentClip => _oneShot;
    public bool ClipPlaying => _oneShot is not null;
    /// <summary>Scales the rig in camera X/Y so it renders as if drawn at the view-model FOV (see <see cref="SetFov"/>).</summary>
    public float FovScale { get; private set; } = 1f;
    /// <summary>VM_DEBUG="x,y,z,rx,ry,rz" shifts the whole rig (for framing screenshots while authoring).</summary>
    private static readonly BonePose? DebugOffset = Environment.GetEnvironmentVariable("VM_DEBUG") is { } e && e.Split(',') is { Length: 6 } p
        ? new BonePose(new Float3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])), new Float3(float.Parse(p[3]), float.Parse(p[4]), float.Parse(p[5]))) : null;

    public ViewModel(ViewModelDef def, GameObject camera, Shader shader)
    {
        Def = def;
        LoadUserClips(def);
        Root = new GameObject($"vm_{def.Id}");
        Root.SetParent(camera, false);
        Root.Transform.LocalPosition = Float3.Zero;
        Root.Transform.LocalRotation = Quaternion.Identity;
        Bones["vm_root"] = Root;
        _rest["vm_root"] = (Float3.Zero, Float3.Zero);

        // A user model replaces the primitive parts; its nodes named like our bones become the bones.
        if (AssetRegistry.TryAttachModel($"vm_{def.Id}", Root, out GameObject? model) && model is not null)
        {
            UsingUserModel = true;
            foreach (GameObject n in model.GetChildrenDeep()) _nodes.TryAdd(n.Name, n);
            if (AssetRegistry.Animations.TryGetValue($"vm_{def.Id}", out var clips))
                foreach (AnimationClip ac in clips)
                {
                    // "Armature|reload" (Blender) → "reload".
                    string nm = ac.Name.Contains('|') ? ac.Name[(ac.Name.LastIndexOf('|') + 1)..] : ac.Name;
                    _embedded.TryAdd(nm.Trim(), ac);
                    if (!def.Clips.ContainsKey(nm)) def.Clips[nm] = new VmClip { Length = MathF.Max(0.05f, ac.Duration) };
                }
            foreach (GameObject n in model.GetChildrenDeep())
                if (!Bones.ContainsKey(n.Name) && def.Bones.Any(b => b.Bone == n.Name))
                {
                    Bones[n.Name] = n;
                    _rest[n.Name] = (n.Transform.LocalPosition, n.Transform.LocalEulerAngles);
                }
        }

        if (UsingUserModel)
            foreach (string bn in def.Clips.Values.SelectMany(c => c.Keys).SelectMany(k => k.Bones.Keys).Distinct())
                if (!Bones.ContainsKey(bn) && _nodes.TryGetValue(bn, out GameObject? node))
                { Bones[bn] = node; _rest[bn] = (node.Transform.LocalPosition, node.Transform.LocalEulerAngles); }

        foreach (var (bone, parent, pos, rot) in def.Bones)
        {
            if (Bones.ContainsKey(bone)) continue;
            var go = new GameObject(bone);
            go.SetParent(Bones.TryGetValue(parent, out GameObject? p) ? p : Root, false);
            // Bones below the arms are scaled with the primitive parts; arm roots keep their screen placement.
            Float3 p2 = parent == "vm_root" || UsingUserModel ? pos : pos * PartScale;
            go.Transform.LocalPosition = p2;
            go.Transform.LocalEulerAngles = rot;
            Bones[bone] = go;
            _rest[bone] = (p2, rot);
        }

        if (!UsingUserModel)
            foreach (VmPart part in def.Parts)
            {
                if (!Bones.TryGetValue(part.Bone, out GameObject? bone)) continue;
                var go = new GameObject($"{part.Bone}_mesh");
                go.SetParent(bone, false);
                go.Transform.LocalPosition = part.Offset * PartScale;
                if (part.Q is { } q) go.Transform.LocalRotation = q; else go.Transform.LocalEulerAngles = part.Rot;
                if (part.Shape == 'e') go.Transform.LocalScale = part.Size * PartScale;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                float ps = PartScale;
                r.Mesh = part.M ?? part.Shape switch
                {
                    's' => Meshes.Sphere(part.Size.X / 2f * ps),
                    'e' => Meshes.Sphere(0.5f),
                    't' => Meshes.Taper(part.Size.X * ps, part.Size.Y * ps, part.Size.Z * ps),
                    'c' => Meshes.RoundedCylinder(part.Size.X / 2f * ps, part.Size.Y * ps, MathF.Min(part.Size.X, part.Size.Y) * ps * 0.12f, 24),
                    _ => Meshes.RoundedBox(part.Size * ps, MathF.Min(part.Size.X, MathF.Min(part.Size.Y, part.Size.Z)) * ps * 0.18f, 2),
                };
                var m = Mats.New(shader);
                m.SetColor("_MainColor", part.Emissive ? part.Color : VmTone(part.Color));
                if (!Application.IsHeadless)
                    try
                    {
                        var (rough, metal) = part.Finish switch
                        {
                            Finish.Metal => (0.32f, 0.9f), Finish.Fabric => (0.95f, 0f), Finish.Leather => (0.55f, 0f),
                            Finish.Wood => (0.62f, 0f), Finish.Rubber => (0.85f, 0f), Finish.Paint => (0.45f, 0.3f), Finish.Worn => (0.5f, 0.35f), Finish.Gloss => (0.22f, 0.05f), _ => (0.5f, 0.05f),
                        };
                        m.SetTexture("_SurfaceTex", ProcTex.Surface(rough, metal));
                        if (part.Finish == Finish.Wood) m.SetTexture("_MainTex", ProcTex.WalnutGrain());
                        else if (part.Finish == Finish.Leather && part.M is not null) m.SetTexture("_MainTex", ProcTex.GloveKnit());
                        else if (part.Finish == Finish.Worn) m.SetTexture("_MainTex", ProcTex.WornPaint());
                    }
                    catch { }
                if (part.Emissive && !Application.IsHeadless)
                {
                    m.SetTexture("_EmissionTex", ProcTex.Solid((byte)(part.Color.R * 255), (byte)(part.Color.G * 255), (byte)(part.Color.B * 255)));
                    m.SetFloat("_EmissionIntensity", 1.2f);
                    if (part.Shape == 'L' && go.GetComponent<PointLight>() is null)
                    {
                        PointLight glow = go.AddComponent<PointLight>();
                        glow.Color = part.Color; glow.Range = 0.6f; glow.Intensity = 0.5f; glow.CastShadows = false;
                    }
                }
                r.Material = m;
            }

        // Aim offset: move vm_root so the sight sits on the screen centre line, slightly closer to the eye.
        if (def.AimShift is { } shift) AimOffset = shift;
        else if (def.CanAim && Bones.TryGetValue("sight", out GameObject? sight))
        {
            Float3 s = Root.Transform.InverseTransformPoint(sight.Transform.Position); // rest pose (bones were just placed)
            AimOffset = new Float3(-s.X, -s.Y, -MathF.Max(0f, s.Z - 0.16f) * 0.35f);
        }
        SetVisible(false);
    }

    /// <summary>Bone origin in vm_root space at the rest pose (walks the rest transforms; no scene update needed).</summary>
    public Float3 RestPointInRoot(string bone)
    {
        Float3 p = Float3.Zero;
        string? b = bone;
        while (b is not null && b != "vm_root")
        {
            var (bn, parent, _, _) = Def.Bones.First(x => x.Bone == b);
            var rest = _rest[bn];
            Quaternion q = Quaternion.FromEuler(rest.Rot * (MathF.PI / 180f));
            p = rest.Pos + Rotate(q, p);
            b = parent;
        }
        return p;
    }

    private static Float3 Rotate(Quaternion q, Float3 v)
    {
        Float3 u = new(q.X, q.Y, q.Z);
        Float3 t = Float3.Cross(u, v) * 2f;
        return v + t * q.W + Float3.Cross(u, t);
    }

    public void SetVisible(bool v) => Root.Enabled = v;

    /// <summary>Renders the rig as if at <paramref name="vmFov"/> while the camera uses <paramref name="cameraFov"/>.</summary>
    public void SetFov(float cameraFov, float vmFov)
    {
        float s = MathF.Tan(cameraFov * MathF.PI / 360f) / MathF.Tan(Math.Clamp(vmFov, 40f, 110f) * MathF.PI / 360f);
        FovScale = float.IsFinite(s) ? Math.Clamp(s, 0.4f, 2.5f) : 1f;
        Root.Transform.LocalScale = new Float3(FovScale, FovScale, 1f);
    }

    /// <summary>Plays a one-shot clip (blended in over ~0.08 s). Unknown clip names are ignored.</summary>
    public bool Play(string clip)
    {
        if (!Def.Clips.ContainsKey(clip)) return false;
        // Variants: "fire" alternates with "fire2", "fire3"... when they exist (melee swings, left/right).
        int n = 1; while (Def.Clips.ContainsKey($"{clip}{n + 1}")) n++;
        if (n > 1) { int v = _variant.GetValueOrDefault(clip); _variant[clip] = (v + 1) % n; if (v > 0) clip = $"{clip}{v + 1}"; }
        if (_oneShot is not null && _oneShotWeight > 0.01f && clip != "draw") { _prevShot = _oneShot; _prevTime = _oneShotTime; _prevWeight = _oneShotWeight; }
        else _prevShot = null;
        _oneShot = clip;
        _oneShotTime = 0f;
        // Draw starts fully posed (gun below the screen); a chained clip starts at full weight to continue the pose.
        if (clip is "draw" || _prevShot is not null && Def.Clips[_prevShot].Hold) { _oneShotWeight = 1f; _prevShot = null; }
        else if (_prevShot is not null) _oneShotWeight = 0f;
        _fidgetT = -1f;
        return true;
    }

    /// <summary>Recoil impulse: pitch/back spring plus a small random yaw and roll so shots don't look canned.</summary>
    public void Kick(float amount)
    {
        float k = amount * Def.RecoilKick;
        _recoilVel += k * 30f * (_aim > 0.5f ? 0.6f : 1f);
        _kickYawVel += (_rng.NextSingle() - 0.5f) * k * 160f;
        _kickRollVel += (_rng.NextSingle() - 0.35f) * k * 220f;
    }

    public void Update(float dt, Float2 mouseDelta, float moveSpeed) => Update(dt, new VmInput(mouseDelta, moveSpeed));

    public void Update(float dt, VmInput i)
    {
        if (!float.IsFinite(dt) || dt <= 0f) return;
        _time += dt;
        // One-shot clip time + weight blending.
        if (_oneShot is not null)
        {
            VmClip c = Def.Clips[_oneShot];
            _oneShotTime += dt;
            // Held clips keep their last pose for a moment (waiting for the next chained clip), then let go.
            bool ending = c.Hold ? _oneShotTime > c.Length + 0.35f : _oneShotTime > c.Length - 0.1f;
            _oneShotWeight = Math.Clamp(_oneShotWeight + (ending ? -dt / 0.1f : dt / 0.07f), 0f, 1f);
            if (_oneShotTime >= c.Length + (c.Hold ? 0.45f : 0f) && _oneShot != "holster") { _oneShot = null; _oneShotWeight = 0f; }
        }
        if (_prevShot is not null)
        {
            _prevTime += dt;
            _prevWeight -= dt / 0.12f;
            if (_prevWeight <= 0f) _prevShot = null;
        }

        float w = Math.Clamp(Def.Weight, 0.3f, 3f);
        bool busy = _oneShot is "reload" or "draw" or "holster" or "inspect";
        float a = Math.Clamp(dt * 12f, 0f, 1f);
        _aim += ((i.Aim && Def.CanAim && !busy && !i.Sprint ? 1f : 0f) - _aim) * Math.Clamp(dt * 14f / MathF.Sqrt(w), 0f, 1f);
        _sprint += ((i.Sprint && i.Speed > 6f && !i.Aim ? 1f : 0f) - _sprint) * Math.Clamp(dt * 8f, 0f, 1f);
        _crouch += (Math.Clamp(i.Crouch, 0f, 1f) - _crouch) * a;
        _strafe += (Math.Clamp(i.Move.X, -1f, 1f) * (1f - _aim * 0.7f) - _strafe) * Math.Clamp(dt * 6f, 0f, 1f);
        if (i.Landing > 2f) _liftVel -= Math.Min(i.Landing, 25f) * 0.045f;
        float liftTarget = i.Grounded ? 0f : Math.Clamp(-i.VerticalVelocity * 0.0035f, -0.02f, 0.03f);

        // Springs, sub-stepped (semi-implicit) so they stay stable at low frame rates.
        Float2 swayTarget = new Float2(-i.Mouse.X, i.Mouse.Y) * (0.0011f * w) * (1f - _aim * 0.75f);
        swayTarget = new Float2(Math.Clamp(swayTarget.X, -0.05f, 0.05f), Math.Clamp(swayTarget.Y, -0.05f, 0.05f));
        float ks = 140f / w, ds = 2f * MathF.Sqrt(ks) * 0.75f; // slightly under-damped: lag, then a soft settle
        int steps = Math.Clamp((int)MathF.Ceiling(dt * 240f), 1, 60);
        float h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            _swayVel += ((swayTarget - _sway) * ks - _swayVel * ds) * h;
            _sway += _swayVel * h;
            _recoilVel += (-_recoil * 220f - _recoilVel * 22f) * h;
            _recoil += _recoilVel * h;
            _kickYawVel += (-_kickYaw * 160f - _kickYawVel * 18f) * h;
            _kickYaw += _kickYawVel * h;
            _kickRollVel += (-_kickRoll * 140f - _kickRollVel * 16f) * h;
            _kickRoll += _kickRollVel * h;
            _liftVel += ((liftTarget - _lift) * 90f - _liftVel * 11f) * h;
            _lift += _liftVel * h;
        }
        if (!float.IsFinite(_recoil + _kickYaw + _kickRoll + _lift + _sway.X + _sway.Y))
        { _recoil = _recoilVel = _kickYaw = _kickYawVel = _kickRoll = _kickRollVel = _lift = _liftVel = 0f; _sway = _swayVel = Float2.Zero; }

        float moving = Math.Clamp(i.Speed / 6f, 0f, 1.5f);
        _bobAmount += (moving * (1f - _aim * 0.85f) - _bobAmount) * Math.Clamp(dt * 8f, 0f, 1f);
        // Walk bob locked to the footstep sounds: a dip on every footfall, a sideways sway over each pair.
        if (i.Step >= 0f) _bobPhase = i.Step * MathF.PI; else _bobPhase += dt * (5.5f + i.Speed * 0.85f) / MathF.Sqrt(w);

        // Idle fidget: every 10-18 s of standing still with nothing playing, a small weapon check.
        if (_fidgetT >= 0f) { _fidgetT += dt; if (_fidgetT > 1.6f) _fidgetT = -1f; }
        else if (_oneShot is null && moving < 0.05f && _aim < 0.05f) { _fidgetIn -= dt; if (_fidgetIn <= 0f) { _fidgetT = 0f; _fidgetIn = 10f + _rng.NextSingle() * 8f; } }
        else _fidgetIn = MathF.Max(_fidgetIn, 4f);
        float fidget = _fidgetT >= 0f ? MathF.Sin(MathF.Min(_fidgetT / 1.6f, 1f) * MathF.PI) : 0f;

        foreach (var (name, bone) in Bones)
        {
            if (!_rest.TryGetValue(name, out var rest)) continue;
            BonePose pose = default;
            if (Def.Clips.TryGetValue("idle", out VmClip? idle)) pose += idle.Sample(name, _time) * (1f - _aim * 0.8f);
            if (_prevShot is not null) pose += Def.Clips[_prevShot].Sample(name, MathF.Min(_prevTime, Def.Clips[_prevShot].Length)) * _prevWeight;
            if (_oneShot is not null) pose += Def.Clips[_oneShot].Sample(name, MathF.Min(_oneShotTime, Def.Clips[_oneShot].Length)) * _oneShotWeight;
            if (name == "vm_root")
            {
                float sharp = 1f / MathF.Sqrt(w);
                float bx = MathF.Sin(_bobPhase) * 0.011f * _bobAmount * (1f + _sprint * 0.8f);
                float by = -MathF.Abs(MathF.Cos(_bobPhase)) * 0.013f * _bobAmount * (1f + _sprint * 0.6f) * sharp;
                Float3 pos = new(bx + _sway.X + _strafe * -0.006f, by + _sway.Y + _lift - _crouch * 0.012f, -_recoil * 0.06f - _crouch * 0.01f);
                Float3 rot = new(-_recoil * 6f + _sway.Y * 120f + _lift * 120f, _sway.X * 70f + _kickYaw, bx * 80f + _strafe * -4f + _kickRoll + _crouch * -3f);
                pose += new BonePose(pos, rot);
                pose += Def.SprintPose * _sprint;
                pose += new BonePose(AimOffset, Float3.Zero) * _aim;
                pose += Def.AimExtra * _aim;
                if (DebugOffset is { } dbg) pose += dbg;
            }
            else if (name == "hand_r" && fidget > 0f) pose += new BonePose(new Float3(0f, 0.004f, 0f), new Float3(-6f, 4f, -28f)) * fidget;
            bone.Transform.LocalPosition = rest.Pos + pose.Pos;
            bone.Transform.LocalEulerAngles = rest.Rot + pose.Rot;
        }
        if (!UsingUserModel)
            foreach (var (fa, wr, elbow, up) in Def.Ik)
                if (Bones.TryGetValue(fa, out GameObject? fb) && Bones.TryGetValue(wr, out GameObject? wb))
                {
                    Float3 wl = Root.Transform.InverseTransformPoint(wb.Transform.Position);
                    Float3 dir = elbow - wl;
                    if (Float3.Length(dir) < 1e-4f) continue;
                    fb.Transform.LocalPosition = wl;
                    fb.Transform.LocalRotation = Quaternion.LookRotation(Float3.Normalize(dir), up);
                }
        // A user model's own clip (same name as the playing one-shot) drives its skeleton directly; the skinned
        // mesh follows its bone transforms. Procedural layers still move vm_root on top.
        if (_oneShot is not null && _embedded.TryGetValue(_oneShot, out AnimationClip? emb)) ApplyEmbedded(emb, _oneShotTime);
        else if (_oneShot is null && _embedded.TryGetValue("idle", out AnimationClip? embIdle)) ApplyEmbedded(embIdle, embIdle.Duration > 0f ? _time % embIdle.Duration : 0f);
    }

    private void ApplyEmbedded(AnimationClip clip, float seconds)
    {
        float t = MathF.Min(seconds, clip.Duration);
        foreach (var ab in clip.Bones)
        {
            if (!_nodes.TryGetValue(ab.BoneName, out GameObject? n)) continue;
            if (ab.PosX is not null) n.Transform.LocalPosition = ab.EvaluatePositionAt(t);
            if (ab.RotX is not null) n.Transform.LocalRotation = ab.EvaluateRotationAt(t);
        }
    }


    /// <summary>World position of the muzzle bone (for flashes and tracers).</summary>
    public Float3 MuzzlePosition => Bones.TryGetValue("muzzle", out GameObject? m) ? m.Transform.Position : Root.Transform.Position;

    /// <summary>Loads assets/viewmodels/&lt;id&gt;.clips.json if present: user clips override/extend the built-in ones.</summary>
    private static void LoadUserClips(ViewModelDef def)
    {
        try
        {
            string path = Path.Combine(AssetRegistry.Root, "viewmodels", $"{def.Id}.clips.json");
            if (!File.Exists(path)) return;
            var clips = JsonSerializer.Deserialize<Dictionary<string, VmClip>>(File.ReadAllText(path), JsonOpts);
            if (clips is null) return;
            foreach (var (k, v) in clips) def.Clips[k] = v;
        }
        catch (Exception e) { Console.WriteLine($"[viewmodel] bad clips file for {def.Id}: {e.Message}"); }
    }

    public static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
