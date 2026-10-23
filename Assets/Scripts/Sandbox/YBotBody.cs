#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// The local player's body as the Mixamo Y Bot: locomotion clips blended by speed and direction (idle / walk / run,
/// strafes, backwards = forward clips reversed, turn-in-place, crouch idle / crouch walk, jump and fall), plus a
/// procedural seated pose (IK legs to the pedals, hands on the steering wheel) in vehicles.
/// First person: the head is shrunk away (no camera clipping) and, on foot, the arms too (the view model shows them);
/// the body sits a little behind the camera so looking down shows the legs and feet.
/// </summary>
public sealed class YBotBody
{
    private readonly SandboxGame _g;
    private readonly YRig _rig;
    private Float3 _lastFeet;
    private float _lastYaw, _phase, _speed, _fwd, _side, _turn, _air, _crouch, _runBlend, _land;
    private bool _wasGrounded = true, _first = true;
    private float _airPeak;
    private readonly Dictionary<string, float> _weights = [], _targets = [];
    private readonly Float3 _knee, _elbow;

    // First person (Unreal "true first person" style): the visible body is a masked copy (hips and legs on foot, plus
    // the arms in a vehicle), while full-body shadow-only copies keep the complete shadow.
    // World model: the full body (head included) every other camera sees; it also casts the player's shadow.
    private readonly List<SkinnedMeshRenderer> _world = [];
    // View model body: the owner's first-person copy (no head/neck/arms on foot; legs + arms seated), owner camera only.
    private readonly List<(SkinnedMeshRenderer R, Mesh? Foot, Mesh? Seated)> _view = [];
    private int _mode = -1;
    private static bool IsLeg(string b) => b == "Hips" || b.Contains("UpLeg") || b.EndsWith("Leg") || b.Contains("Foot") || b.Contains("Toe");
    /// <summary>First person on foot: everything but the head, neck and arms (the view model arms stand in for them).</summary>
    private static bool IsTorsoAndLegs(string b) => !(b.Contains("Head") || b.Contains("Neck") || b.Contains("Arm") || b.Contains("Hand") || b.Contains("Eye"));
    private float _pitch;
    private static bool IsLegOrArm(string b) => IsLeg(b) || b.Contains("ForeArm") || b.Contains("Hand") || b.EndsWith("Arm");

    private YBotBody(SandboxGame g, YRig rig)
    {
        _g = g; _rig = rig;
        foreach (SkinnedMeshRenderer r in rig.Renderers.ToList())
        {
            if (r.SharedMesh is not { } full) continue;
            _world.Add(r);
            Mesh? foot = YRig.Masked(r, "torso-strict", IsTorsoAndLegs, strict: true), seated = YRig.Masked(r, "seated", IsLegOrArm);
            SkinnedMeshRenderer v = rig.AddRenderer(r, foot ?? full, r.GameObject.Name + " (view)", ShadowCasting.Off);
            v.GameObject.LayerIndex = LocalView.ViewLayer;
            v.Enabled = false;
            _view.Add((v, foot, seated));
        }
        _knee = rig.Hinge("LeftLeg", "CrouchIdle", 1f, Float3.UnitX);
        _elbow = rig.Hinge("RightForeArm", "Run", 0.2f, Float3.UnitX);
    }

    public static YBotBody? TryCreate(SandboxGame g) => YRig.Create(g.World, "PlayerBody") is { } rig ? new YBotBody(g, rig) : null;
    public bool Valid => _rig.Valid;
    public void Destroy() => _rig.Destroy();

    /// <summary>-1 hidden, 0 third person (world model only), 1 first person on foot, 2 first person seated (world model for other cameras + owner-only view body).</summary>
    private void SetMode(int mode)
    {
        if (mode == _mode) return;
        _mode = mode;
        bool fp = mode >= 1 && LocalView.IsLocal(_g.Player);
        foreach (SkinnedMeshRenderer r in _world)
        {
            r.Enabled = mode >= 0;
            r.ShadowCasting = ShadowCasting.On;
            // In first person the owner's camera skips the world model (other cameras still see it, head and all).
            r.GameObject.LayerIndex = fp ? LocalView.WorldLayer : 0;
        }
        foreach (var (v, foot, seated) in _view)
        {
            v.Enabled = fp;
            if (fp && (mode == 2 ? seated : foot) is { } m) v.SharedMesh = m;
        }
    }

    private YClip? C(string n) => YBot.Clips.GetValueOrDefault(n);

    public void Tick(float dt, bool hidden, PlayerAvatar dbg)
    {
        if (hidden) { SetMode(-1); _first = true; return; }
        dt = Math.Clamp(dt, 1e-4f, 0.1f);
        Buggy? car = _g.Vehicles.Driving;
        bool firstPerson = (car is null || _g.Vehicles.FirstPerson) && dbg.DebugOffset == Float3.Zero;
        SetMode(!firstPerson ? 0 : car is null ? 1 : 2);
        if (car is not null) Seated(car); else Standing(dt, firstPerson, dbg);
    }

    private void Standing(float dt, bool firstPerson, PlayerAvatar dbg)
    {
        Player p = _g.Player;
        Float3 feet = p.FeetPosition;
        float yawDeg = p.Yaw + dbg.DebugYaw;
        float yaw = yawDeg * MathF.PI / 180f;
        Float3 fwd = new(MathF.Sin(yaw), 0f, MathF.Cos(yaw)), right = new(fwd.Z, 0f, -fwd.X);
        if (_first) { _lastFeet = feet; _lastYaw = yawDeg; _first = false; }
        Float3 vel = (feet - _lastFeet) / dt; _lastFeet = feet;
        if (Float3.Length(vel) > 25f) vel = Float3.Zero; // teleport
        float yawRate = Wrap(yawDeg - _lastYaw) / dt; _lastYaw = yawDeg;
        Float3 flat = new(vel.X, 0f, vel.Z);
        if (dbg.DebugSpeed >= 0f) flat = fwd * dbg.DebugSpeed;
        // Blend parameters are smoothed (critically-damped style exponential, ~0.12 s), and every clip weight then
        // eases toward its target over ~0.25 s, so state changes cross-fade instead of popping.
        float k = 1f - MathF.Exp(-dt / 0.12f);
        float speed = Float3.Length(flat);
        _speed += (speed - _speed) * k;
        float vf = Float3.Dot(flat, fwd), vs = Float3.Dot(flat, right);
        _fwd += ((speed > 0.3f ? vf / speed : 1f) - _fwd) * k;
        _side += ((speed > 0.3f ? vs / speed : 0f) - _side) * k;
        bool grounded = p.IsGrounded || p.Noclip || dbg.DebugSpeed >= 0f;
        float crouch = dbg.DebugCrouch >= 0f ? dbg.DebugCrouch : p.CrouchBlend;
        _crouch += (crouch - _crouch) * k;
        _air = grounded ? 0f : _air + dt;
        if (grounded && !_wasGrounded && _airPeak > 0.25f) _land = 1f;
        _airPeak = grounded ? 0f : _air;
        _wasGrounded = grounded;
        _land = MathF.Max(0f, _land - dt / 0.45f);
        float turnK = 1f - MathF.Exp(-dt / 0.2f);
        _turn += ((speed < 0.3f && grounded ? Math.Clamp(yawRate / 150f, -1f, 1f) : 0f) - _turn) * turnK;

        // Gait: one shared normalized phase so walk, run and strafes stay foot-synced while they blend.
        float run = Math.Clamp((_speed - Player.WalkSpeed * 0.55f) / MathF.Max(0.5f, Player.RunSpeed * 0.75f - Player.WalkSpeed * 0.55f), 0f, 1f);
        _runBlend += (run - _runBlend) * k;
        float move = Math.Clamp((_speed - 0.15f) / 1.2f, 0f, 1f);
        move = move * move * (3f - 2f * move);
        float stride = (1.6f + (3.4f - 1.6f) * _runBlend) * (1f - 0.35f * _crouch); // metres per gait cycle
        float rate = Math.Clamp(_speed / stride, 0f, 2.2f);
        _phase += dt * rate * (_fwd < -0.2f ? -1f : 1f);
        _phase -= MathF.Floor(_phase);

        bool rifle = _g.Weapons.Current is Firearm && C("RifleAimIdle") is not null;
        bool backClips = rifle && C("RifleWalkBack") is not null;
        float airT = grounded ? 0f : 1f;
        float stand = (1f - airT) * (1f - _crouch), crouchW = (1f - airT) * _crouch;
        float side = MathF.Abs(_side), back = Math.Clamp(-_fwd, 0f, 1f);
        float fwdW = 1f - side;
        var w = _targets; w.Clear();
        void T(string clip, float weight) { if (weight > 0f && C(clip) is not null) w[clip] = w.GetValueOrDefault(clip) + weight; }
        float idleW = stand * (1f - move);
        T(rifle ? "RifleAimIdle" : "Idle", idleW * (1f - MathF.Abs(_turn)));
        T(rifle ? "RifleTurnRight45" : "TurnRight", idleW * MathF.Max(0f, _turn));
        T(rifle ? "RifleTurnLeft" : "TurnLeft", idleW * MathF.Max(0f, -_turn));
        float mw = stand * move;
        float fb = mw * fwdW;
        if (backClips) { T("RifleWalkBack", fb * back * (1f - _runBlend)); T("RifleRunBack", fb * back * _runBlend); fb *= 1f - back; }
        T(rifle ? "RifleWalk" : "Walk", fb * (1f - _runBlend));
        T(rifle ? "RifleRun" : "Run", fb * _runBlend);
        string sl = rifle ? "RifleStrafeLeft" : "StrafeWalkLeft", sr = rifle ? "RifleStrafeRight" : "StrafeWalkRight";
        T(_side < 0f ? sl : sr, mw * side * (rifle ? 1f : 1f - _runBlend));
        if (!rifle) T(_side < 0f ? "StrafeLeft" : "StrafeRight", mw * side * _runBlend);
        T("CrouchIdle", crouchW * (1f - move));
        T("CrouchWalk", crouchW * move);
        bool rising = p.VerticalVelocity > 0.5f;
        T(rising || C("FallIdle") is null ? (rifle && C("RifleJump") is not null ? "RifleJump" : "Jump") : "FallIdle", airT);
        T("FallLand", _land * 0.7f);

        float fade = 1f - MathF.Exp(-dt / 0.08f); // ~0.25 s cross-fade
        foreach (string key in _weights.Keys.Union(w.Keys).ToList())
        {
            float cur = _weights.GetValueOrDefault(key), tgt = w.GetValueOrDefault(key);
            cur += (tgt - cur) * fade;
            if (cur < 0.002f && tgt == 0f) _weights.Remove(key); else _weights[key] = cur;
        }
        _rig.BeginPose();
        float now = (float)Time.UnscaledTotalTime;
        foreach (var (key, weight) in _weights)
        {
            YClip c = C(key)!;
            float time = key switch
            {
                "Jump" => 0.55f + MathF.Min(_air, 0.5f) * 0.7f,
                "RifleJump" => 0.35f + MathF.Min(_air, 0.5f) * 0.7f,
                "FallIdle" => _air,
                "FallLand" => 0.45f + (1f - _land) * 0.45f,
                "Idle" or "RifleAimIdle" or "CrouchIdle" => now,
                _ when key.StartsWith("Turn") || key.StartsWith("RifleTurn") => now,
                _ => _phase * c.Duration,
            };
            _rig.Add(c, time, weight, loop: key is not ("Jump" or "RifleJump" or "FallLand"));
        }
        _rig.ApplyPose();

        // Place the body. Third person: feet on the controller. First person: the camera sits at the head bone's eye
        // point (the body is shifted so the eye lands under the camera), then 6 cm further back so looking straight
        // down shows the thighs and feet rather than the inside of the waist.
        Transform t = _rig.Root.Transform;
        t.Rotation = Quaternion.LookRotation(fwd, Float3.UnitY);
        // Look pitch spread over the spine (Spine, Spine1, Spine2): bend forward looking down, lean back looking up,
        // smoothed so the torso follows the camera without snapping. Y Bot's right is +X, so +X rotation bends forward.
        _pitch += (Math.Clamp(p.Pitch, -89f, 89f) - _pitch) * (1f - MathF.Exp(-dt / 0.08f));
        float bend = (_pitch > 0f ? _pitch * 0.35f : _pitch * 0.3f) / 3f * MathF.PI / 180f;
        Quaternion q = Quaternion.AxisAngle(Float3.UnitX, bend);
        foreach (string sb in (string[])["Spine", "Spine1", "Spine2"])
            if (_rig.Bone(sb) is { } bone) _rig.SetRootRotation(bone, q * _rig.RootTR(bone).Rot);
        t.Position = feet + dbg.DebugOffset;
        if (firstPerson && _rig.Bone("Head") is { } head)
        {
            Float3 eye = head.Position + t.Rotation * new Float3(0f, 0.09f, 0.1f);
            Float3 cam = p.EyePosition;
            // Eye under the camera (the bent spine moves the head forward, so this pulls the hips back like a real
            // lean), then a little further back the more the player looks down so the chest doesn't fill the view.
            t.Position += new Float3(cam.X - eye.X, 0f, cam.Z - eye.Z) - fwd * (0.04f + 0.16f * Math.Clamp(_pitch / 89f, 0f, 1f));
            // Never let the head rise to the camera (jumping or falling while looking down, crouch transitions): drop
            // the body so the eye never sits above it. The view body has no head/neck triangles anyway.
            float above = eye.Y - cam.Y;
            if (above > 0f) t.Position -= new Float3(0f, above, 0f);
        }
    }

    private void Gait(YClip? c, float w)
    {
        if (c is null || w <= 0.001f) return;
        _rig.Add(c, _phase * c.Duration, w);
    }

    private void Seated(Buggy car)
    {
        Transform ct = car.Go.Transform;
        Float3 P(Float3 local) => ct.Position + ct.Right * local.X + ct.Up * local.Y + ct.Forward * local.Z;
        Float3 fwd = ct.Forward, up = ct.Up, right = ct.Right;
        _first = true;
        // Base: the idle pose (spine, head, fingers), then the seat overrides.
        _rig.BeginPose();
        _rig.Add(C("Idle"), (float)Time.UnscaledTotalTime, 1f);
        _rig.ApplyPose();
        Transform root = _rig.Root.Transform;
        root.Rotation = Quaternion.LookRotation(fwd, up);
        Transform? hips = _rig.Bone("Hips");
        if (hips is null) return;
        Float3 hipAt = P(car.DriverHips);
        // Root so the hips land on the seat point.
        root.Position = hipAt - (hips.Position - root.Position);
        // Slight recline of the torso.
        if (_rig.Bone("Spine") is { } spine) YRig.RotateWorld(spine, Quaternion.AxisAngle(right, -0.12f));
        // Legs: thighs forward along the seat, feet on the floor pan.
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            string side = i == 0 ? "Left" : "Right";
            Float3 foot = hipAt + right * (0.16f * s) + fwd * 0.62f - up * 0.30f;
            _rig.SolveTwoBone($"{side}UpLeg", $"{side}Leg", $"{side}Foot", foot, up + fwd * 0.6f, _knee);
            if (_rig.Bone($"{side}Foot") is { } f) f.Rotation = Quaternion.LookRotation(fwd, up) * Quaternion.AxisAngle(Float3.UnitX, 0.35f);
            // Hands on the steering wheel (the wheel's grip points turn with it), elbows down and out.
            _rig.SolveTwoBone($"{side}Arm", $"{side}ForeArm", $"{side}Hand", car.WheelGrip(s), -up + right * (s * 0.8f), _elbow);
        }
    }

    private static float Wrap(float a) { while (a > 180f) a -= 360f; while (a < -180f) a += 360f; return a; }
}
