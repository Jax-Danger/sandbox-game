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

/// <summary>A primitive mesh attached to a bone (used when no user model is installed).</summary>
public sealed record VmPart(string Bone, char Shape, Float3 Size, Float3 Offset, Color Color, Float3 Rot = default, bool Emissive = false);

/// <summary>Describes a first-person view model: bone rest poses, primitive parts and clips.</summary>
public sealed class ViewModelDef
{
    public string Id = "";
    /// <summary>Bone name → (parent bone, rest local position, rest local Euler degrees). Order = creation order (parents first).</summary>
    public List<(string Bone, string Parent, Float3 Pos, Float3 Rot)> Bones = [];
    public List<VmPart> Parts = [];
    public Dictionary<string, VmClip> Clips = [];
    public float RecoilKick = 1f;

    public static readonly string[] StandardBones = ["vm_root", "arm_r", "hand_r", "weapon", "muzzle", "arm_l", "hand_l"];
}

// ───────────────────────────── Runtime rig + animator ─────────────────────────────

/// <summary>
/// First-person view model: a bone hierarchy under the camera with primitive meshes (or a user model) on the bones,
/// driven by layered animation: looping idle, a blended one-shot clip (draw/holster/reload/inspect/fire),
/// then procedural walk bob, mouse sway and a recoil spring.
/// </summary>
public sealed class ViewModel
{
    public ViewModelDef Def { get; }
    public GameObject Root { get; }
    public Dictionary<string, GameObject> Bones { get; } = [];
    private readonly Dictionary<string, (Float3 Pos, Float3 Rot)> _rest = [];
    public bool UsingUserModel { get; }
    /// <summary>Primitive parts are authored at roughly real-world size and shrunk to sit nicely on screen.</summary>
    public const float PartScale = 0.6f;

    private string? _oneShot;
    private float _oneShotTime, _oneShotWeight;
    private float _time;
    private Float2 _sway;
    private float _recoil, _recoilVel;
    private float _bobPhase, _bobAmount;
    public string? CurrentClip => _oneShot;
    public bool ClipPlaying => _oneShot is not null;

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
            foreach (GameObject n in model.GetChildrenDeep())
                if (!Bones.ContainsKey(n.Name) && def.Bones.Any(b => b.Bone == n.Name))
                {
                    Bones[n.Name] = n;
                    _rest[n.Name] = (n.Transform.LocalPosition, n.Transform.LocalEulerAngles);
                }
        }

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
                go.Transform.LocalEulerAngles = part.Rot;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.Mesh = part.Shape switch
                {
                    's' => Mesh.CreateSphere(part.Size.X / 2f * PartScale, 10, 14),
                    'c' => Mesh.CreateCylinder(part.Size.X / 2f * PartScale, part.Size.Y * PartScale, 14),
                    _ => Mesh.CreateCube(part.Size * PartScale),
                };
                var m = new Material(shader);
                m.SetColor("_MainColor", part.Color);
                try { m.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
                if (part.Emissive) m.SetFloat("_EmissionIntensity", 2f);
                r.Material = m;
            }
        SetVisible(false);
    }

    public void SetVisible(bool v) => Root.Enabled = v;

    /// <summary>Plays a one-shot clip (blended in over ~0.08 s). Unknown clip names are ignored.</summary>
    public bool Play(string clip)
    {
        if (!Def.Clips.ContainsKey(clip)) return false;
        _oneShot = clip;
        _oneShotTime = 0f;
        if (clip is "draw") _oneShotWeight = 1f; // draw starts fully posed (gun below the screen)
        return true;
    }

    public void Kick(float amount) => _recoilVel += amount * Def.RecoilKick * 30f;

    public void Update(float dt, Float2 mouseDelta, float moveSpeed)
    {
        _time += dt;
        // One-shot clip time + weight blending.
        if (_oneShot is not null)
        {
            VmClip c = Def.Clips[_oneShot];
            _oneShotTime += dt;
            bool ending = _oneShotTime > c.Length - 0.08f;
            _oneShotWeight = Math.Clamp(_oneShotWeight + (ending ? -dt : dt) / 0.08f, 0f, 1f);
            if (_oneShotTime >= c.Length && _oneShot != "holster") { _oneShot = null; _oneShotWeight = 0f; }
        }

        // Procedural layers.
        _sway += (new Float2(-mouseDelta.X, mouseDelta.Y) * 0.0009f - _sway) * Math.Clamp(dt * 10f, 0f, 1f);
        _sway = new Float2(Math.Clamp(_sway.X, -0.04f, 0.04f), Math.Clamp(_sway.Y, -0.04f, 0.04f));
        float acc = -_recoil * 220f - _recoilVel * 22f;          // critically-damped-ish spring back to rest
        _recoilVel += acc * dt;
        _recoil += _recoilVel * dt;
        _bobAmount += (Math.Clamp(moveSpeed / 6f, 0f, 1.2f) - _bobAmount) * Math.Clamp(dt * 8f, 0f, 1f);
        _bobPhase += dt * (6f + moveSpeed * 0.9f);

        foreach (var (name, bone) in Bones)
        {
            if (!_rest.TryGetValue(name, out var rest)) continue;
            BonePose pose = default;
            if (Def.Clips.TryGetValue("idle", out VmClip? idle)) pose += idle.Sample(name, _time);
            if (_oneShot is not null) pose += Def.Clips[_oneShot].Sample(name, _oneShotTime) * _oneShotWeight;
            if (name == "vm_root")
            {
                float bx = MathF.Sin(_bobPhase) * 0.012f * _bobAmount, by = -MathF.Abs(MathF.Cos(_bobPhase)) * 0.014f * _bobAmount;
                pose += new BonePose(new Float3(bx + _sway.X, by + _sway.Y, -_recoil * 0.06f), new Float3(-_recoil * 6f, _sway.X * 60f, bx * 80f));
            }
            bone.Transform.LocalPosition = rest.Pos + pose.Pos;
            bone.Transform.LocalEulerAngles = rest.Rot + pose.Rot;
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
