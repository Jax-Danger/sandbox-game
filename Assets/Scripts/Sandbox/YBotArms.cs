#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// First-person arms from a second Y Bot: everything but the arms is shrunk away (hips scaled to 1 %, shoulders scaled
/// back up), the shoulders are pinned in camera space just behind and below the eye, and two-bone IK reaches each arm to
/// the view model's wrist bones. The view model keeps all of its procedural motion (sway, bob, recoil, clips): the arms
/// simply follow its hands. The Y Bot hands are shrunk too: the sculpted grip-fitted gloves stay on the weapons until
/// Mixamo rifle/pistol hand poses are supplied. No clip sampling: about 6 bone writes per frame. No shadows.
/// </summary>
public sealed class YBotArms
{
    private readonly YRig _rig;
    private readonly GameObject _camera;
    private readonly Float3 _elbow;
    // Shoulder sockets in camera space (x right, y up, z forward).
    private static readonly Float3 ShoulderR = new(0.17f, -0.21f, -0.1f), ShoulderL = new(-0.17f, -0.21f, -0.1f);

    private YBotArms(YRig rig, GameObject camera)
    {
        _rig = rig; _camera = camera;
        _elbow = rig.Hinge("RightForeArm", "Run", 0.2f, Float3.UnitX);
        rig.Root.SetParent(camera, false);
        rig.BeginPose();
        rig.Add(YBot.Clips.GetValueOrDefault("Idle"), 0f, 1f);
        rig.ApplyPose();
        if (rig.Bone("Hips") is { } hips) hips.LocalScale = new Float3(0.01f);
        foreach (string s in new[] { "LeftShoulder", "RightShoulder" }) if (rig.Bone(s) is { } b) b.LocalScale = new Float3(100f);
        foreach (string s in new[] { "LeftHand", "RightHand" }) if (rig.Bone(s) is { } b) b.LocalScale = new Float3(0.0001f);
    }

    public static YBotArms? TryCreate(GameObject camera)
    {
        if (camera.Scene is not { } scene) return null;
        return YRig.Create(scene, "VmArms", castShadows: false) is { } rig ? new YBotArms(rig, camera) : null;
    }

    public void SetVisible(bool on) => _rig.SetVisible(on);
    public void Destroy() => _rig.Destroy();

    /// <summary>Call after the view model has posed its bones this frame.</summary>
    public void Pose(GameObject? wristR, GameObject? wristL)
    {
        if (!_rig.Valid) return;
        Transform cam = _camera.Transform;
        Float3 At(Float3 l) => cam.Position + cam.Right * l.X + cam.Up * l.Y + cam.Forward * l.Z;
        Reach("Right", At(ShoulderR), wristR, 1f, cam);
        Reach("Left", At(ShoulderL), wristL, -1f, cam);
    }

    private void Reach(string side, Float3 shoulder, GameObject? wrist, float s, Transform cam)
    {
        if (wrist is null || !wrist.IsValid() || _rig.Bone($"{side}Arm") is not { } arm) return;
        arm.Position = shoulder;
        // Elbows drop down, out and back from the hands so the forearms enter from the lower screen corners.
        _rig.SolveTwoBone($"{side}Arm", $"{side}ForeArm", $"{side}Hand", wrist.Transform.Position, -cam.Up + cam.Right * (0.6f * s) - cam.Forward * 0.3f, _elbow);
    }
}
