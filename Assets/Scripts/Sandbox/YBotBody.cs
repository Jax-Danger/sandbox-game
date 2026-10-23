#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
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
    private readonly Float3 _knee, _elbow;

    private YBotBody(SandboxGame g, YRig rig)
    {
        _g = g; _rig = rig;
        _knee = rig.Hinge("LeftLeg", "CrouchIdle", 1f, Float3.UnitX);
        _elbow = rig.Hinge("RightForeArm", "Run", 0.2f, Float3.UnitX);
    }

    public static YBotBody? TryCreate(SandboxGame g) => YRig.Create(g.World, "PlayerBody") is { } rig ? new YBotBody(g, rig) : null;
    public bool Valid => _rig.Valid;
    public void Destroy() => _rig.Destroy();

    private YClip? C(string n) => YBot.Clips.GetValueOrDefault(n);

    public void Tick(float dt, bool hidden, PlayerAvatar dbg)
    {
        _rig.SetVisible(!hidden);
        if (hidden) { _first = true; return; }
        dt = Math.Clamp(dt, 1e-4f, 0.1f);
        Buggy? car = _g.Vehicles.Driving;
        bool firstPerson = car is null || _g.Vehicles.FirstPerson;
        _rig.Hide("Head", firstPerson);
        bool arms = !firstPerson || car is not null;
        _rig.Hide("LeftShoulder", !arms);
        _rig.Hide("RightShoulder", !arms);
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
        float k = Math.Clamp(dt * 10f, 0f, 1f);
        float speed = Float3.Length(flat);
        _speed += (speed - _speed) * k;
        float vf = Float3.Dot(flat, fwd), vs = Float3.Dot(flat, right);
        _fwd += ((speed > 0.1f ? vf / speed : 1f) - _fwd) * k;
        _side += ((speed > 0.1f ? vs / speed : 0f) - _side) * k;
        bool grounded = p.IsGrounded || p.Noclip || dbg.DebugSpeed >= 0f;
        float crouch = dbg.DebugCrouch >= 0f ? dbg.DebugCrouch : p.CrouchBlend;
        _crouch += (crouch - _crouch) * k;
        _air = grounded ? 0f : _air + dt;
        if (grounded && !_wasGrounded && p.LandingImpact > 3f) _land = 1f;
        _wasGrounded = grounded;
        _land = MathF.Max(0f, _land - dt * 2.5f);
        _turn += ((speed < 0.3f && grounded ? Math.Clamp(yawRate / 120f, -1f, 1f) : 0f) - _turn) * Math.Clamp(dt * 6f, 0f, 1f);

        // Gait: one shared normalized phase so walk, run and strafes stay foot-synced while they blend.
        float run = Math.Clamp((_speed - Player.WalkSpeed * 0.55f) / MathF.Max(0.5f, Player.RunSpeed * 0.75f - Player.WalkSpeed * 0.55f), 0f, 1f);
        _runBlend += (run - _runBlend) * k;
        float move = Math.Clamp(_speed / 1.2f, 0f, 1f);
        float stride = 1.6f + (3.4f - 1.6f) * _runBlend; // metres per gait cycle (walk / run clip)
        if (_crouch > 0.5f) stride = 1.1f;
        float rate = Math.Clamp(_speed / stride, 0f, 2.2f);
        if (move > 0.02f) _phase += dt * rate * (_fwd < -0.3f ? -1f : 1f);
        _phase -= MathF.Floor(_phase);

        _rig.BeginPose();
        float air = grounded ? 0f : Math.Clamp(_air / 0.12f, 0f, 1f);
        float ground = 1f - air;
        float stand = ground * (1f - _crouch), crouchW = ground * _crouch;
        float side = MathF.Abs(_side), back = MathF.Max(0f, -_fwd);
        float fwdW = 1f - side;
        // Standing locomotion.
        _rig.Add(C("Idle"), (float)Time.UnscaledTotalTime, stand * (1f - move) * (1f - MathF.Abs(_turn)));
        if (_turn > 0.05f) _rig.Add(C("TurnRight"), (float)Time.UnscaledTotalTime, stand * (1f - move) * _turn);
        if (_turn < -0.05f) _rig.Add(C("TurnLeft"), (float)Time.UnscaledTotalTime, stand * (1f - move) * -_turn);
        float mw = stand * move;
        Gait(C("Walk"), mw * fwdW * (1f - _runBlend));
        Gait(C("Run"), mw * fwdW * _runBlend);
        Gait(C(_side < 0f ? "StrafeWalkLeft" : "StrafeWalkRight"), mw * side * (1f - _runBlend) * (1f - back));
        Gait(C(_side < 0f ? "StrafeLeft" : "StrafeRight"), mw * side * _runBlend * (1f - back));
        // Crouch.
        _rig.Add(C("CrouchIdle") ?? C("CrouchWalk"), (float)Time.UnscaledTotalTime, crouchW * (1f - move));
        Gait(C("CrouchWalk"), crouchW * move);
        // Air: the jump clip's airborne section while rising, then the falling loop.
        if (air > 0f)
        {
            bool rising = p.VerticalVelocity > 0.5f;
            YClip? jump = C("Jump"), fall = C("FallIdle");
            if (rising || fall is null) _rig.Add(jump, 0.55f + MathF.Min(_air, 0.5f) * 0.7f, air, loop: false);
            else _rig.Add(fall, _air, air);
        }
        if (_land > 0f && C("FallLand") is { } land) _rig.Add(land, 0.45f + (1f - _land) * 0.4f, _land * 0.6f, loop: false);
        _rig.ApplyPose();

        // Place the body: a little behind the camera in first person (legs and feet visible when looking down, the
        // chest never in front of the lens); further back in a crouch, where the eye drops far below the model's head.
        float back0 = firstPerson ? 0.13f + 0.32f * _crouch : 0f;
        if (dbg.DebugOffset != Float3.Zero) back0 = 0f;
        Transform t = _rig.Root.Transform;
        t.Position = feet - fwd * back0 + dbg.DebugOffset;
        t.Rotation = Quaternion.LookRotation(fwd, Float3.UnitY);
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
