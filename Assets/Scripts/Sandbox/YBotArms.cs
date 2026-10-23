#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// First-person arms, set up like a Unity FPS arms rig: a separate Y Bot whose mesh is masked to the arms and hands
/// (shoulders down; every other triangle is dropped), parented to the view model root, so the view model's FOV scale,
/// sway, bob, recoil and per-weapon clips move it exactly like the weapon. The arms and fingers take their pose from the
/// Mixamo "rifle aiming idle" clip (real gripping fingers), then two-bone IK places each wrist on the weapon's grip
/// (hand IK targets = the view model's wrist bones) and turns the hand to the grip axis. A free left hand uses the plain
/// idle pose. No shadows, no animation state: one clip sample per frame.
/// </summary>
public sealed class YBotArms
{
    private readonly YRig _rig;
    private readonly ViewModelDef _def;
    private readonly Float3 _elbow;
    private readonly Quaternion _gripToHandR, _gripToHandL;
    private readonly int[] _armChain;
    /// <summary>Shoulder midpoint in view-model space: just behind and below the eye.</summary>
    public static Float3 ShoulderMid = new(0f, -0.24f, -0.03f);

    private static bool IsArm(string b) => b.Contains("Arm") || b.Contains("Hand") || b.Contains("Shoulder");

    private YBotArms(YRig rig, GameObject vmRoot, ViewModelDef def)
    {
        _rig = rig; _def = def;
        _elbow = rig.Hinge("RightForeArm", "Run", 0.2f, Float3.UnitX);
        rig.Root.SetParent(vmRoot, false);
        rig.Root.Transform.LocalRotation = Quaternion.Identity;
        // Arms-only mesh: swap the full body for a masked copy; the Alpha_Joints mesh is masked the same way.
        foreach (SkinnedMeshRenderer r in rig.Renderers)
        {
            if (YRig.Masked(r, "arms", IsArm) is { } m) r.SharedMesh = m;
            r.ShadowCasting = ShadowCasting.Off;
        }
        _gripToHandR = HandFrame("RightHand", def.GripR);
        _gripToHandL = HandFrame("LeftHand", def.GripL);
        var chain = new List<int>();
        for (int i = 0; i < rig.Skeleton.BoneCount; i++)
            if (rig.Skeleton.GetBoneID(i).DebugName is { } n && IsArm(n)) chain.Add(i);
        _armChain = [.. chain];
        _rig.ResetToBind();
        _curlR = Curls("Right"); _curlL = Curls("Left");
    }

    private static readonly string[] Fingers = ["Index", "Middle", "Ring", "Pinky"];
    private readonly (Transform Bone, Quaternion Bind, Float3 Axis, int Finger, int Joint)[] _curlR, _curlL;

    /// <summary>Finger joints with their bind rotation and curl axis (the hand's index → pinky line in joint space).</summary>
    private (Transform, Quaternion, Float3, int, int)[] Curls(string side)
    {
        var list = new List<(Transform, Quaternion, Float3, int, int)>();
        int idx = _rig.Index($"{side}HandIndex1"), pinky = _rig.Index($"{side}HandPinky1");
        if (idx < 0 || pinky < 0) return [];
        Float3 lateral = Float3.Normalize(_rig.BindModelPosition(pinky) - _rig.BindModelPosition(idx));
        for (int f = 0; f < Fingers.Length; f++)
            for (int j = 1; j <= 3; j++)
            {
                int i = _rig.Index($"{side}Hand{Fingers[f]}{j}");
                if (i < 0 || _rig.Bone(i) is not { } b) continue;
                Float3 axis = Quaternion.Inverse(_rig.BindModelRotation(i)) * lateral;
                list.Add((b, b.LocalRotation, axis, f, j));
            }
        return [.. list];
    }

    /// <summary>Wraps the fingers around a grip: open grips (fore-ends, big handles) curl less; the trigger finger stays straighter.</summary>
    private void Curl((Transform Bone, Quaternion Bind, Float3 Axis, int Finger, int Joint)[] joints, VmGrip g, float sign, bool trigger, float? relaxed = null)
    {
        float baseDeg = relaxed ?? (g.Open ? 42f : 68f);
        foreach (var (b, bind, axis, f, j) in joints)
        {
            float deg = baseDeg * (j == 1 ? 0.8f : j == 2 ? 1.15f : 0.9f) + f * 4f;
            if (trigger && f == 0) deg = j == 1 ? 20f : 35f;
            b.LocalRotation = bind * Quaternion.AxisAngle(axis, sign * CurlSign * deg * MathF.PI / 180f);
        }
    }

    /// <summary>Direction of a finger curl about the index → pinky axis (+1 or -1); tuned visually.</summary>
    public static float CurlSign = -1f;

    /// <summary>
    /// Rotation from the view model hand bone's frame to the Y Bot hand's frame: Y Bot fingers (+Y) point from the wrist
    /// toward the grip, and its index → pinky direction runs down the grip axis.
    /// </summary>
    private Quaternion HandFrame(string hand, VmGrip g)
    {
        int h = _rig.Index(hand), idx = _rig.Index(hand.Replace("Hand", "HandIndex1")), pinky = _rig.Index(hand.Replace("Hand", "HandPinky1"));
        if (h < 0 || idx < 0 || pinky < 0) return Quaternion.Identity;
        Quaternion hm = _rig.BindModelRotation(h);
        Float3 lateralLocal = Quaternion.Inverse(hm) * (_rig.BindModelPosition(pinky) - _rig.BindModelPosition(idx));
        Float3 axis = Float3.Normalize(g.Axis);
        Float3 fingers = -g.Wrist - axis * Float3.Dot(-g.Wrist, axis);
        if (Float3.Length(fingers) < 1e-4f) fingers = Float3.UnitZ;
        Quaternion vm = YRig.AimRotation(fingers, Float3.UnitZ, hand.StartsWith("Left") ? axis : -axis); // mirrored for the left hand      // vm-hand space frame: +Y fingers, +Z index→pinky
        Quaternion yb = YRig.AimRotation(Float3.UnitY, Float3.UnitZ, lateralLocal); // the same frame in Y Bot hand-local space
        return vm * Quaternion.Inverse(yb);
    }

    public static YBotArms? TryCreate(GameObject vmRoot, ViewModelDef def)
    {
        if (vmRoot.Scene is not { } scene) return null;
        return YRig.Create(scene, "VmArms", castShadows: false) is { } rig ? new YBotArms(rig, vmRoot, def) : null;
    }

    public void SetVisible(bool on) => _rig.SetVisible(on);
    public void Destroy() => _rig.Destroy();

    /// <summary>Call after the view model has posed its bones this frame. Bones are looked up by the view model's names.</summary>
    public void Pose(ViewModel vm, float time)
    {
        if (!_rig.Valid) return;
        _rig.ResetToBind();
        // Base pose: the rifle aim (both hands gripping); a free left arm hangs in the idle pose instead.
        _rig.BeginPose();
        _rig.Add(YBot.Clips.GetValueOrDefault("RifleAimIdle") ?? YBot.Clips.GetValueOrDefault("Idle"), time, 1f);
        _rig.ApplyPose();
        // Place the rig so its shoulders sit at ShoulderMid (rig root has no rotation inside the view model root).
        Transform? ls = _rig.Bone("LeftArm"), rs = _rig.Bone("RightArm");
        if (ls is null || rs is null) return;
        _rig.Root.Transform.LocalPosition = Float3.Zero;
        Float3 mid = (_rig.RootTR(ls).Pos + _rig.RootTR(rs).Pos) * 0.5f;
        _rig.Root.Transform.LocalPosition = ShoulderMid - mid;
        Float3 off = _rig.Root.Transform.LocalPosition;
        Reach(vm, "Right", "hand_r", _def.GripR, _gripToHandR, 1f, off);
        Curl(_curlR, _def.GripR, 1f, _def.GripR.Trigger);
        if (!_def.FreeLeft) { Reach(vm, "Left", "hand_l", _def.GripL, _gripToHandL, -1f, off); Curl(_curlL, _def.GripL, -1f, false); }
        else { Reach(vm, "Left", "hand_l", _def.GripL, _gripToHandL, -1f, off, keepClipHand: true); Curl(_curlL, _def.GripL, -1f, false, relaxed: 30f); }
        // Only the arms are drawn (masked mesh), but collapse the head too so nothing of it can ever show.
        if (_rig.Bone("Head") is { } head) head.LocalScale = new Float3(1e-4f);
    }

    private void Reach(ViewModel vm, string side, string vmHand, VmGrip grip, Quaternion gripToHand, float s, Float3 off, bool keepClipHand = false)
    {
        if (!vm.Bones.TryGetValue(vmHand, out GameObject? hb) || _rig.Bone($"{side}Hand") is not { } hand) return;
        // The view model hand's pose in vm_root space, from its local chain (vm_root carries the FOV scale).
        var (hp, hr) = VmSpace(vm, hb);
        Float3 wrist = hp + hr * grip.Wrist - off;
        Quaternion fingersFromClip = hand.LocalRotation;
        _rig.SolveTwoBoneRoot($"{side}Arm", $"{side}ForeArm", $"{side}Hand", wrist, new Float3(0.6f * s, -1f, -0.4f), _elbow);
        if (!keepClipHand) _rig.SetRootRotation(hand, hr * gripToHand);
        else hand.LocalRotation = fingersFromClip;
    }

    private static (Float3, Quaternion) VmSpace(ViewModel vm, GameObject bone)
    {
        Float3 p = Float3.Zero; Quaternion r = Quaternion.Identity;
        var chain = new List<Transform>();
        for (Transform? t = bone.Transform; t is not null && !ReferenceEquals(t, vm.Root.Transform); t = t.Parent) chain.Add(t);
        for (int i = chain.Count - 1; i >= 0; i--) { p += r * chain[i].LocalPosition; r *= chain[i].LocalRotation; }
        return (p, r);
    }
}
