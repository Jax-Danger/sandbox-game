#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// An NPC / ragdoll drawn as the Y Bot (same skeleton and clips as the player). While its brain walks it, the body plays
/// idle / walk / run blended by the pelvis speed; once it goes limp, dies, or is a plain ragdoll, the bones follow the
/// physics limbs (pelvis → Hips, torso → Spine, head, upper/lower arms and legs), so physgun, ragdoll and save behaviour are
/// unchanged: the physics limbs (and the save format) stay exactly as before, only their meshes are hidden.
/// Far NPCs update less often to keep CPU cost down.
/// </summary>
public sealed class YBotNpc : Component
{
    private Ragdoll? _rd;
    private YRig? _rig;
    private DummyBrain? _brain;
    private SandboxGame? _g;
    private float _phase, _speed, _run;
    private int _frame;
    private static int _seed;
    // Limb index in Ragdoll.Defs → bone short name.
    private static readonly (int Limb, string Bone)[] Map =
    [
        (0, "Hips"), (1, "Spine"), (2, "Head"), (3, "LeftArm"), (4, "LeftForeArm"), (5, "RightArm"), (6, "RightForeArm"),
        (7, "LeftUpLeg"), (8, "LeftLeg"), (9, "RightUpLeg"), (10, "RightLeg"),
    ];
    private readonly Quaternion[] _rotOff = new Quaternion[Map.Length];
    private Float3 _hipsOff;

    /// <summary>Gives a freshly spawned ragdoll the Y Bot look. No-op (code-built look stays) when the assets are unavailable.</summary>
    public static void Attach(SandboxGame g, Ragdoll rd, Float3 feet, float yawDegrees, Color tint)
    {
        if (!YBot.Available || rd.Limbs.Count < 11) return;
        YRig? rig = YRig.Create(g.World, $"{rd.Name} YBot");
        if (rig is null) return;
        var go = new GameObject($"{rd.Name} Body");
        g.World.Add(go);
        YBotNpc npc = go.AddComponent<YBotNpc>();
        npc._rd = rd; npc._rig = rig; npc._g = g; npc._frame = _seed++;
        foreach (SkinnedMeshRenderer r in rig.Renderers) if (r.GameObject.Name.Contains("Surface")) r.MainColor = tint;
        // Bind: the rest-standing idle pose over the spawn pose of the limbs (yaw only, arms hanging).
        rig.Root.Transform.Position = feet;
        rig.Root.Transform.Rotation = Quaternion.AxisAngle(Float3.UnitY, yawDegrees * MathF.PI / 180f);
        rig.BeginPose();
        rig.Add(YBot.Clips.GetValueOrDefault("Idle"), 0f, 1f);
        rig.ApplyPose();
        for (int i = 0; i < Map.Length; i++)
        {
            Transform limb = rd.Limbs[Map[i].Limb].Go.Transform;
            Transform? bone = rig.Bone(Map[i].Bone);
            npc._rotOff[i] = bone is null ? Quaternion.Identity : Quaternion.Inverse(limb.Rotation) * bone.Rotation;
            if (i == 0 && bone is not null) npc._hipsOff = limb.InverseTransformPoint(bone.Position);
        }
        // Hide the code-built limb meshes and their sculpted details; hats, badges and carried tools stay on the limbs.
        foreach (Prop l in rd.Limbs)
        {
            if (l.Go.GetComponent<MeshRenderer>() is { } mr) mr.Enabled = false;
            foreach (GameObject c in l.Go.Children)
                if (c.Name == "detail" && c.GetComponent<MeshRenderer>() is { } dr) dr.Enabled = false;
        }
        rd.YBot = npc;
    }

    public override void LateUpdate()
    {
        if (_rd is null || _rig is null) return;
        if (!_rd.Alive || !_rig.Valid || _rd.Limbs.Count < 11 || !_rd.Limbs[0].Alive)
        {
            _rig?.Destroy(); _rig = null;
            GameObject.Destroy();
            return;
        }
        _brain ??= _rd.Pelvis.Go.GetComponent<DummyBrain>();
        bool animated = _brain is { Limp: false } && _brain.Team != NpcTeam.Dummy && _rd.Limbs.All(l => l.Alive && !l.Frozen);
        // Throttle: full rate within 30 m, every 3rd frame to 80 m, every 8th beyond.
        float dist = _g is null ? 0f : Float3.Length(_rd.Pelvis.Go.Transform.Position - _g.Player.EyePosition);
        int every = dist < 30f ? 1 : dist < 80f ? 3 : 8;
        float dt = Time.DeltaTime * every;
        if (++_frame % every != 0) return;
        if (animated) Animate(dt); else FollowLimbs();
    }

    private void Animate(float dt)
    {
        Transform pelvis = _rd!.Pelvis.Go.Transform;
        Float3 v = _rd.Pelvis.Body.LinearVelocity;
        float speed = Float3.Length(new Float3(v.X, 0f, v.Z));
        float k = Math.Clamp(dt * 8f, 0f, 1f);
        _speed += (speed - _speed) * k;
        _run += (Math.Clamp((_speed - 2.2f) / 1.5f, 0f, 1f) - _run) * k;
        float move = Math.Clamp(_speed / 0.6f, 0f, 1f);
        float stride = 1.5f + 1.8f * _run;
        _phase += dt * Math.Clamp(_speed / stride, 0f, 2f);
        _phase -= MathF.Floor(_phase);
        YRig rig = _rig!;
        rig.BeginPose();
        rig.Add(YBot.Clips.GetValueOrDefault("Idle"), (float)Time.UnscaledTotalTime + _frame * 0.37f, 1f - move);
        if (YBot.Clips.GetValueOrDefault("Walk") is { } walk) rig.Add(walk, _phase * walk.Duration, move * (1f - _run));
        if (YBot.Clips.GetValueOrDefault("Run") is { } run) rig.Add(run, _phase * run.Duration, move * _run);
        rig.ApplyPose();
        Float3 f = pelvis.Forward;
        float yaw = MathF.Atan2(f.X, f.Z);
        rig.Root.Transform.Rotation = Quaternion.AxisAngle(Float3.UnitY, yaw);
        // The brain floats the pelvis 1 m over the ground.
        rig.Root.Transform.Position = pelvis.Position - new Float3(0f, 1.0f, 0f);
    }

    private void FollowLimbs()
    {
        YRig rig = _rig!;
        for (int i = 0; i < Map.Length; i++)
        {
            Transform limb = _rd!.Limbs[Map[i].Limb].Go.Transform;
            if (rig.Bone(Map[i].Bone) is not { } bone) continue;
            if (i == 0) bone.Position = limb.TransformPoint(_hipsOff);
            bone.Rotation = limb.Rotation * _rotOff[i];
        }
    }

    public override void OnDisable() { _rig?.Destroy(); _rig = null; }
}
