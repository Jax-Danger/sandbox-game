// Code-built game objects are owned by the game world; ?./?? on them follows the legacy files' convention.
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Spatial;
using MotionClip = Prowl.Motion.AnimationClipBase;
using MotionPose = Prowl.Motion.Pose;
using MotionSkeleton = Prowl.Motion.Skeleton;
using MotionMapping = Prowl.Motion.SkeletonMapping;

namespace Sandbox;

/// <summary>One Mixamo clip resolved against the Y Bot skeleton.</summary>
public sealed class YClip
{
    public required string Name;
    public required MotionClip Clip;
    public required MotionMapping Map;
    public float Duration => Clip.Duration;
    /// <summary>Cancels the clip's average hips yaw (Mixamo crouch/strafe takes face up to 80° off +Z).</summary>
    public Quaternion YawFix = Quaternion.Identity;
}

/// <summary>
/// The Mixamo Y Bot ("FBX for Unity") and its clips, loaded by path from Assets/Resources/Player (so no editor-assigned
/// references are needed and it works in packaged builds). Null when the assets are not available (headless self-test,
/// missing files): callers fall back to the code-built bodies.
/// </summary>
public static class YBot
{
    public const string ModelPath = "Player/YBot", AnimFolder = "Player/Animations";
    public static readonly string[] ClipNames =
    [
        "Idle", "Walk", "Run", "Jump", "CrouchIdle", "CrouchWalk", "FallIdle", "FallLand",
        "StrafeLeft", "StrafeRight", "StrafeWalkLeft", "StrafeWalkRight", "TurnLeft", "TurnRight", "TurnLeft90", "TurnRight90",
    ];

    private static bool _tried;
    public static PrefabAsset? Model { get; private set; }
    public static MotionSkeleton? Skeleton { get; private set; }
    public static Dictionary<string, YClip> Clips { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Why loading failed (for the log / self-test), or null.</summary>
    public static string? Error { get; private set; }
    /// <summary>SANDBOX_YBOT=0 forces the code-built bodies.</summary>
    public static bool Enabled = Environment.GetEnvironmentVariable("SANDBOX_YBOT") != "0";

    public static bool Available => EnsureLoaded();

    public static bool EnsureLoaded()
    {
        if (_tried) return Model is not null && Skeleton is not null && Clips.ContainsKey("Idle");
        _tried = true;
        if (!Enabled || Application.IsHeadless) { Error = "disabled"; return false; }
        try
        {
            Model = AssetDatabase.FindResource<PrefabAsset>(ModelPath);
            var avatar = AssetDatabase.FindAllResources<Prowl.Runtime.Avatar>(ModelPath).FirstOrDefault();
            if (avatar is not null) { avatar.Load(); Skeleton = avatar.Skeleton; }
            if (Model is null || Skeleton is null) { Error = $"model '{ModelPath}' not found"; Console.WriteLine($"[ybot] {Error}"); return false; }
            foreach (string name in ClipNames)
            {
                AnimationClip? asset = AssetDatabase.FindAllResources<AnimationClip>($"{AnimFolder}/{name}").FirstOrDefault();
                if (asset is null) continue;
                AddClip(name, asset);
            }
            Console.WriteLine($"[ybot] model {Skeleton.BoneCount} bones, {Clips.Count}/{ClipNames.Length} clips");
            if (!Clips.ContainsKey("Idle")) { Error = "no Idle clip"; return false; }
            return true;
        }
        catch (Exception e) { Error = e.Message; Console.WriteLine($"[ybot] load failed: {e.Message}"); Model = null; return false; }
    }

    private static void AddClip(string name, AnimationClip asset)
    {
        asset.Avatar?.Load();
        MotionClip? clip = asset.GetClip(null);
        if (clip is null) { Console.WriteLine($"[ybot] clip {name} did not decode"); return; }
        var yc = new YClip { Name = name, Clip = clip, Map = clip.GetMappingTo(Skeleton!) };
        if (!name.StartsWith("Turn")) yc.YawFix = MeanYawFix(yc);
        Clips[name] = yc;
    }

    /// <summary>Uses already-imported assets (tests / tools that run without the asset database).</summary>
    public static void Install(PrefabAsset? model, MotionSkeleton skeleton, IEnumerable<(string Name, AnimationClip Clip)> clips)
    {
        _tried = true; Error = null;
        Model = model; Skeleton = skeleton; Clips.Clear();
        foreach (var (name, clip) in clips) AddClip(name, clip);
    }

    private static Quaternion MeanYawFix(YClip c)
    {
        var pose = new MotionPose(Skeleton!);
        int hips = YRig.FindBone(Skeleton!, "Hips");
        if (hips < 0) return Quaternion.Identity;
        Float3 sum = Float3.Zero;
        for (int i = 0; i < 8; i++)
        {
            c.Clip.GetPose(c.Duration * i / 8f, pose, c.Map);
            Float3 f = pose.GetTransform(hips).rotation * Float3.UnitZ;
            sum += new Float3(f.X, 0f, f.Z);
        }
        if (Float3.Length(sum) < 1e-3f) return Quaternion.Identity;
        float yaw = MathF.Atan2(sum.X, sum.Z);
        return MathF.Abs(yaw) < 0.03f ? Quaternion.Identity : Quaternion.AxisAngle(Float3.UnitY, -yaw);
    }

    /// <summary>Called when a play session ends so a hot-reloaded or re-imported model is picked up next time.</summary>
    public static void Release() { _tried = false; Model = null; Skeleton = null; Clips.Clear(); }
}

/// <summary>
/// One Y Bot instance in the game world: its bone Transforms by skeleton index, a cheap weighted clip blender
/// (sampled straight from the Motion clips, no Animator/graph: a handful of 66-bone samples per frame), and
/// helpers for procedural overrides (two-bone IK, bone aiming, shrinking bones to hide body parts).
/// Skinning itself runs on the GPU (SkinnedMeshRenderer pushes 52 bone matrices only when the pose changed).
/// </summary>
public sealed class YRig
{
    public GameObject Root { get; }
    public MotionSkeleton Skeleton { get; }
    public List<SkinnedMeshRenderer> Renderers { get; } = [];
    private readonly Transform?[] _bones;
    private readonly Transform3D[] _rest; // bind-pose locals
    private readonly MotionPose _sample;
    private readonly Transform3D[] _acc;
    private float _accWeight;
    private readonly int _hips;
    private readonly Dictionary<string, int> _byShort = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Bones currently shrunk to zero (index → true).</summary>
    private readonly bool[] _hidden;

    private YRig(GameObject root, MotionSkeleton skeleton)
    {
        Root = root;
        Skeleton = skeleton;
        int n = skeleton.BoneCount;
        _bones = new Transform?[n];
        _rest = new Transform3D[n];
        _acc = new Transform3D[n];
        _hidden = new bool[n];
        _sample = new MotionPose(skeleton);
        var byName = new Dictionary<string, Transform>();
        foreach (GameObject g in root.GetChildrenDeep()) byName.TryAdd(g.Name, g.Transform);
        for (int i = 0; i < n; i++)
        {
            string? name = skeleton.GetBoneID(i).DebugName;
            if (name is null) continue;
            _byShort.TryAdd(Short(name), i);
            if (byName.TryGetValue(name, out Transform? t)) _bones[i] = t;
            _rest[i] = skeleton.ParentSpaceReferencePose[i];
        }
        _hips = Index("Hips");
        foreach (GameObject g in root.GetChildrenDeep())
            if (g.GetComponent<SkinnedMeshRenderer>() is { } smr) Renderers.Add(smr);
        // The import may add an Animator; the rig is posed here, so it must not fight us.
        foreach (GameObject g in root.GetChildrenDeep().Prepend(root))
            if (g.GetComponent<Animator>() is { } an) an.Enabled = false;
    }

    private static string Short(string name) => name.Contains(':') ? name[(name.LastIndexOf(':') + 1)..] : name;

    public static int FindBone(MotionSkeleton s, string shortName)
    {
        for (int i = 0; i < s.BoneCount; i++)
            if (s.GetBoneID(i).DebugName is { } n && Short(n).Equals(shortName, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Instantiates the Y Bot into <paramref name="world"/>; null when the assets are unavailable.</summary>
    public static YRig? Create(Scene world, string name, bool castShadows = true)
    {
        if (!YBot.EnsureLoaded()) return null;
        try
        {
            GameObject? go = GameObject.InstantiateDetached(YBot.Model!);
            if (go is null) return null;
            go.Name = name;
            world.Add(go);
            var rig = new YRig(go, YBot.Skeleton!);
            if (rig._hips < 0 || rig._bones[rig._hips] is null || rig.Renderers.Count == 0) { go.Destroy(); return null; }
            foreach (SkinnedMeshRenderer r in rig.Renderers) r.ShadowCasting = castShadows ? ShadowCasting.On : ShadowCasting.Off;
            return rig;
        }
        catch (Exception e) { Console.WriteLine($"[ybot] instantiate failed: {e.Message}"); return null; }
    }

    /// <summary>Wraps an already-instantiated Y Bot hierarchy (tests / tools).</summary>
    public static YRig Wrap(GameObject root, MotionSkeleton skeleton) => new(root, skeleton);

    public bool Valid => Root.IsValid() && Root.Scene is not null;
    public int Index(string shortName) => _byShort.TryGetValue(shortName, out int i) ? i : -1;
    public Transform? Bone(int index) => (uint)index < (uint)_bones.Length ? _bones[index] : null;
    public Transform? Bone(string shortName) => Bone(Index(shortName));
    public Transform3D Rest(int index) => _rest[index];

    public void SetVisible(bool on) { foreach (SkinnedMeshRenderer r in Renderers) if (r.Enabled != on) r.Enabled = on; }

    // ── clip blending ──
    public void BeginPose() => _accWeight = 0f;

    /// <summary>Adds <paramref name="clip"/> at <paramref name="time"/> (wrapped when looping) with weight <paramref name="w"/>.</summary>
    public void Add(YClip? clip, float time, float w, bool loop = true)
    {
        if (clip is null || w <= 0.001f) return;
        float d = MathF.Max(clip.Duration, 1e-3f);
        time = loop ? ((time % d) + d) % d : Math.Clamp(time, 0f, d);
        clip.Clip.GetPose(time, _sample, clip.Map);
        float t = w / (_accWeight + w);
        int n = _acc.Length;
        for (int i = 0; i < n; i++)
        {
            Transform3D s = _sample.GetTransform(i);
            if (i == _hips) s.rotation = clip.YawFix * s.rotation;
            if (_accWeight <= 0f) _acc[i] = s;
            else
            {
                Transform3D a = _acc[i];
                a.position = a.position + (s.position - a.position) * t;
                a.rotation = Quaternion.Slerp(a.rotation, s.rotation, t);
                _acc[i] = a;
            }
        }
        _accWeight += w;
    }

    /// <summary>Writes the blended pose onto the bones. The hips keep their rest X/Z (no root motion), only the vertical bob.</summary>
    public void ApplyPose()
    {
        if (_accWeight <= 0f) return;
        for (int i = 0; i < _acc.Length; i++)
        {
            Transform? b = _bones[i];
            if (b is null || ReferenceEquals(b, Root.Transform)) continue;
            Transform3D p = _acc[i];
            Float3 pos = p.position;
            if (i == _hips) pos = new Float3(_rest[i].position.X, pos.Y, _rest[i].position.Z);
            else pos = _rest[i].position; // clips only rotate limbs; keeping bind lengths avoids per-clip scale drift
            b.LocalPosition = pos;
            b.LocalRotation = p.rotation;
            b.LocalScale = _hidden[i] ? new Float3(0.0001f) : Float3.One;
        }
    }

    /// <summary>Shrinks a bone (and so everything skinned to it and its children) to nothing, or restores it.</summary>
    public void Hide(string shortName, bool hidden)
    {
        int i = Index(shortName);
        if (i < 0) return;
        _hidden[i] = hidden;
        if (_bones[i] is { } b) b.LocalScale = hidden ? new Float3(0.0001f) : Float3.One;
    }

    // ── procedural helpers ──

    /// <summary>Rotates <paramref name="bone"/> (world) so its local +Y (Mixamo bones point along +Y to their child) runs
    /// along <paramref name="dir"/>, with the local axis <paramref name="bendLocal"/> mapped onto <paramref name="bendWorld"/>.</summary>
    public static void Aim(Transform bone, Float3 dir, Float3 bendLocal, Float3 bendWorld)
    {
        Float3 y = Float3.Normalize(dir);
        Float3 z = bendWorld - y * Float3.Dot(bendWorld, y);
        if (Float3.Length(z) < 1e-4f) z = MathF.Abs(y.Y) < 0.9f ? Float3.Cross(y, Float3.UnitY) : Float3.Cross(y, Float3.UnitX);
        z = Float3.Normalize(z);
        Float3 ly = Float3.UnitY;
        Float3 lz = bendLocal - ly * Float3.Dot(bendLocal, ly);
        if (Float3.Length(lz) < 1e-4f) lz = Float3.UnitZ;
        lz = Float3.Normalize(lz);
        Quaternion target = Quaternion.LookRotation(z, y), local = Quaternion.LookRotation(lz, ly);
        bone.Rotation = target * Quaternion.Inverse(local);
    }

    /// <summary>Two-bone IK: middle joint for a chain from <paramref name="a"/> reaching <paramref name="b"/>, bending toward <paramref name="pole"/>.</summary>
    public static Float3 Knee(Float3 a, Float3 b, float l1, float l2, Float3 pole)
    {
        Float3 ab = b - a; float d = Math.Clamp(Float3.Length(ab), 0.01f, l1 + l2 - 0.002f);
        Float3 dir = Float3.Length(ab) < 1e-5f ? -Float3.UnitY : Float3.Normalize(ab);
        float x = (l1 * l1 + d * d - l2 * l2) / (2f * d), h = MathF.Sqrt(MathF.Max(0f, l1 * l1 - x * x));
        Float3 p = pole - dir * Float3.Dot(pole, dir);
        p = Float3.Length(p) < 1e-4f ? Float3.UnitY : Float3.Normalize(p);
        return a + dir * x + p * h;
    }

    /// <summary>
    /// Solves upper/lower/end (e.g. Arm/ForeArm/Hand, UpLeg/Leg/Foot) so the end bone's origin reaches <paramref name="target"/>.
    /// The hinge axis (the joint's local axis perpendicular to the bend plane) is taken from <paramref name="hingeLocal"/>.
    /// </summary>
    public void SolveTwoBone(string upper, string lower, string end, Float3 target, Float3 pole, Float3 hingeLower)
    {
        int li = Index(lower), ei = Index(end);
        Transform? u = Bone(upper), l = Bone(lower), e = Bone(end);
        if (u is null || l is null || e is null || li < 0 || ei < 0) return;
        float l1 = Float3.Length(_rest[li].position), l2 = Float3.Length(_rest[ei].position);
        Float3 a = u.Position;
        Float3 k = Knee(a, target, l1, l2, pole);
        Float3 n = Float3.Cross(k - a, target - k);
        if (Float3.Length(n) < 1e-5f) n = Float3.Cross(target - a, pole);
        Aim(u, k - a, _rest[li].rotation * hingeLower, n);
        Aim(l, target - k, hingeLower, n);
    }

    /// <summary>
    /// The hinge axis of a joint in its own local frame, taken from a clip where it bends (knee in a crouch, elbow in a run):
    /// the axis of the rotation from the bind pose to that frame, signed so a positive bend turns the child bone toward the
    /// side <see cref="SolveTwoBone"/> expects.
    /// </summary>
    public Float3 Hinge(string lower, string clip, float time, Float3 fallback)
    {
        int i = Index(lower);
        if (i < 0 || !YBot.Clips.TryGetValue(clip, out YClip? c)) return fallback;
        c.Clip.GetPose(time, _sample, c.Map);
        Quaternion q = Quaternion.Inverse(_rest[i].rotation) * _sample.GetTransform(i).rotation;
        Float3 v = new(q.X, q.Y, q.Z);
        if (q.W < 0f) v = -v;
        return Float3.Length(v) < 0.05f ? fallback : Float3.Normalize(v);
    }

    /// <summary>World rotation helper that keeps the bone's current local rotation but adds a world-space rotation.</summary>
    public static void RotateWorld(Transform bone, Quaternion delta) => bone.Rotation = delta * bone.Rotation;

    public void Destroy() { if (Root.IsValid()) Root.Destroy(); }
}
