// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// First-person player. Walk mode uses Prowl's CharacterController with our own gravity and jump.
/// Noclip (V) flies the camera freely, like the slice-1 fly camera.
/// </summary>
public sealed class Player
{
    public const float EyeHeight = 1.65f;
    /// <summary>Movement tuning (m/s); GameTuning overrides these from the editor.</summary>
    public static float WalkSpeed = 5f, RunSpeed = 9f, JumpSpeed = 6.5f, CrouchSpeed = 2.3f;
    /// <summary>Player gravity; scaled with the world's gravity modifier (20 at Earth gravity feels right for the controller).</summary>
    public static float Gravity { get; set; } = 20f;
    public bool Crouching { get; private set; }
    private float _crouch;
    /// <summary>Current eye height above the feet (lowers smoothly while crouching).</summary>
    public float CurrentEyeHeight => EyeHeight - 0.6f * _crouch;
    private const float FlySpeed = 10f;
    public float LookSensitivity { get; set; } = 0.12f;

    private readonly GameObject _body;
    private readonly CharacterController _controller;
    private float _yaw, _pitch, _verticalVelocity;

    public GameObject CameraObject { get; }
    public Camera Camera { get; }
    public bool Noclip { get; private set; }
    public bool IsGrounded => _controller.IsGrounded;
    /// <summary>Horizontal move speed this frame (0 in the air / noclip); drives view-model bob.</summary>
    public float Speed { get; private set; }
    public Float3 FeetPosition => _body.Transform.Position;
    public Float3 EyePosition => CameraObject.Transform.Position;
    public Float3 Forward => CameraObject.Transform.Forward;
    public float Yaw => _yaw;
    public float Pitch => _pitch;
    /// <summary>Downward speed (m/s) of the last landing; consumed (reset to 0) by the health system.</summary>
    public float LandingImpact { get; set; }
    /// <summary>Landing speed for the view model's dip (separate from <see cref="LandingImpact"/>, which Health consumes).</summary>
    public float LandingKick { get; set; }
    public Float2 MoveInput { get; private set; }
    public bool Running { get; private set; }
    public float CrouchBlend => _crouch;
    public float VerticalVelocity => _verticalVelocity;
    private bool _wasGrounded = true;
    private float _jumpBuffer, _coyote, _jumpLock;
    private bool _justJumped;
    /// <summary>Programmatic jump (self-test); consumed on the next grounded update.</summary>
    public bool JumpRequested;
    public int Jumps { get; private set; }

    public Player(Scene scene, Float3 spawnFeet, float yaw)
    {
        _body = new GameObject("Player");
        _body.Transform.Position = spawnFeet;
        _controller = _body.AddComponent<CharacterController>();
        _controller.Radius = 0.4f;
        _controller.Height = 1.8f;
        scene.Add(_body);

        CameraObject = new GameObject("Main Camera") { Tag = "Main Camera" };
        Camera = CameraObject.AddComponent<Camera>();
        Camera.Background = CameraBackground.Color;
        Camera.BackgroundColor = new Color(0.53f, 0.70f, 0.86f, 1f);
        Camera.FieldOfView = 70f;
        Camera.NearClipPlane = 0.02f; // view models sit ~0.15 m from the eye
        Camera.NearClipPlane = 0.05f;
        Camera.FarClipPlane = 400f;
        scene.Add(CameraObject);

        _yaw = yaw;
        _pitch = 10f;
        ApplyCamera();
    }

    public void ToggleNoclip()
    {
        Noclip = !Noclip;
        _verticalVelocity = 0f;
        if (!Noclip)
        {
            // Drop back into walk mode where the camera is.
            _body.Transform.Position = CameraObject.Transform.Position - new Float3(0f, EyeHeight, 0f);
        }
    }

    /// <summary>Riding a seat: only mouse look; the controller is parked out of the way so it can't shove the contraption.</summary>
    public void UpdateSeated(Float3 eye, bool allowInput)
    {
        if (allowInput)
        {
            Float2 delta = Input.MouseDelta;
            _yaw += delta.X * LookSensitivity;
            _pitch = Math.Clamp(_pitch + delta.Y * LookSensitivity, -89f, 89f);
        }
        Speed = 0f;
        _verticalVelocity = 0f;
        ApplyCamera();
        CameraObject.Transform.Position = eye;
    }

    /// <param name="allowInput">False while a menu is open: the player stands still but gravity still applies.</param>
    public void Update(float dt, bool allowInput)
    {
        if (allowInput)
        {
            Float2 delta = Input.MouseDelta;
            _yaw += delta.X * LookSensitivity;
            _pitch = Math.Clamp(_pitch + delta.Y * LookSensitivity, -89f, 89f);
        }

        Float2 move = Float2.Zero;
        bool run = false;
        if (allowInput)
        {
            if (Binds.Held("forward")) move += Float2.UnitY;
            if (Binds.Held("back")) move -= Float2.UnitY;
            if (Binds.Held("right")) move += Float2.UnitX;
            if (Binds.Held("left")) move -= Float2.UnitX;
            run = Input.IsShiftPressed;
        }
        MoveInput = move; Running = run && move.Y > 0.1f;
        Crouching = allowInput && !Noclip && Input.IsCtrlPressed;
        _crouch += ((Crouching ? 1f : 0f) - _crouch) * MathF.Min(1f, dt * 12f);

        if (Noclip)
        {
            Speed = 0f;
            ApplyCamera();
            float speed = FlySpeed * (run ? 3f : 1f) * dt;
            Float3 pos = CameraObject.Transform.Position;
            pos += CameraObject.Transform.Forward * move.Y * speed;
            pos += CameraObject.Transform.Right * move.X * speed;
            if (allowInput && Binds.Held("jump")) pos += Float3.UnitY * speed;
            if (allowInput && Input.IsCtrlPressed) pos -= Float3.UnitY * speed;
            CameraObject.Transform.Position = pos;
            _body.Transform.Position = pos - new Float3(0f, EyeHeight, 0f);
            return;
        }

        ApplyCamera();
        Float3 f = CameraObject.Transform.Forward, r = CameraObject.Transform.Right;
        Float3 flatForward = Flat(f), flatRight = Flat(r);
        Float3 wish = flatForward * move.Y + flatRight * move.X;
        float len = Float3.Length(wish);
        if (len > 1e-4f) wish /= len;
        wish *= Crouching ? CrouchSpeed : run ? RunSpeed : WalkSpeed;
        Speed = _controller.IsGrounded ? Float3.Length(wish) : 0f;

        bool groundedNow = _controller.IsGrounded;
        if (groundedNow && !_wasGrounded && _verticalVelocity < -2f) { LandingImpact = -_verticalVelocity; LandingKick = -_verticalVelocity; }
        _wasGrounded = groundedNow;
        // Jump buffer + coyote time; and once airborne-upward, ignore the 0.1 m ground probe so the first frames of a
        // jump (still within probe range of the floor, especially at high frame rates) don't zero the velocity again.
        if (allowInput && Binds.Down("jump")) _jumpBuffer = 0.15f;
        _jumpBuffer -= dt; _coyote = groundedNow ? 0.1f : _coyote - dt; _jumpLock -= dt;
        if ((_jumpBuffer > 0f || JumpRequested) && _coyote > 0f && _jumpLock <= 0f)
        {
            _verticalVelocity = JumpSpeed - Gravity * dt; _jumpBuffer = 0f; _coyote = 0f; _jumpLock = 0.25f; JumpRequested = false; Jumps++; _justJumped = true;
        }
        else if (_controller.IsGrounded && _jumpLock <= 0f)
        {
            _verticalVelocity = -1f; // small stick-to-ground push
        }
        else
        {
            _justJumped = false;
            _verticalVelocity -= Gravity * dt;
        }

        if (!float.IsFinite(_verticalVelocity) || !float.IsFinite(Gravity)) _verticalVelocity = 0f;
        // While grounded the controller's ground snap keeps us on slopes; pushing down into the floor every frame only
        // sinks the capsule past its skin width (which then trips the engine's bad depenetration below).
        float vy = _controller.IsGrounded && _verticalVelocity < 0f && _jumpLock <= 0f ? 0f
            : _verticalVelocity + Gravity * dt * 0.5f; // midpoint (jump frame included): same peak at any FPS
        var motion = new Float3(wish.X, vy, wish.Z) * dt;
        if (!IsFinite(_body.Transform.Position)) RecoverFromNaN();
        else if (IsFinite(motion))
        {
            // Jitter's MPR/EPA can return a NaN contact normal for degenerate overlaps (seen in the 300 s soak
            // after welding two limbs of one ragdoll); the engine's Math.Sign then throws. Skip that frame's move.
            Float3 before = _body.Transform.Position;
            try { _controller.Move(motion); }
            catch (ArithmeticException) { MoveFaults++; }
            // The engine's sweep reports a bogus negative hit fraction when the capsule already overlaps the floor
            // slightly (uneven frame times can sink it past the skin width), which "resolves" by launching the
            // player tens of metres up — on the big town grass slab this repeated until fall damage killed you.
            // Reject any upward jump the move couldn't have produced and re-seat the feet on the floor below.
            Float3 after = _body.Transform.Position;
            if (IsFinite(after) && after.Y - before.Y > MathF.Max(motion.Y, 0f) + _controller.StepSize + 0.1f)
            {
                Float3 back = new(before.X + motion.X, before.Y, before.Z + motion.Z);
                if (_body.Scene?.Physics.Raycast(back + new Float3(0f, 0.6f, 0f), -Float3.UnitY, 1.2f, out RaycastHit gh) == true && gh.Point.Y <= back.Y + 0.6f)
                    back.Y = gh.Point.Y + 0.05f;
                _body.Transform.Position = back;
                LaunchesRejected++;
            }
            if (!IsFinite(_body.Transform.Position)) RecoverFromNaN();
        }

        // Fell out of the map: respawn at the center.
        if (_body.Transform.Position.Y < -30f)
        {
            _body.Transform.Position = MapBuilder.SpawnPoint + new Float3(0f, 1f, 0f);
            _verticalVelocity = 0f;
            _wasGrounded = false;
        }

        ApplyCamera();
        CameraObject.Transform.Position = _body.Transform.Position + new Float3(0f, CurrentEyeHeight, 0f);
    }

    public static bool IsFinite(Float3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>Non-finite position (e.g. pushed by a NaN body): respawn instead of crashing the shape cast.</summary>
    public void RecoverFromNaN()
    {
        _body.Transform.Position = MapBuilder.SpawnPoint + new Float3(0f, 1f, 0f);
        _verticalVelocity = 0f;
        _wasGrounded = false;
        NaNRecoveries++;
    }
    public int NaNRecoveries { get; private set; }
    public int MoveFaults { get; private set; }
    public int LaunchesRejected { get; private set; }

    public static Float3 Flat(Float3 v)
    {
        var flat = new Float3(v.X, 0f, v.Z);
        float l = Float3.Length(flat);
        return l > 1e-4f ? flat / l : Float3.Zero;
    }

    private float _shake, _punch;
    /// <summary>Camera shake (explosions), decays over ~0.5 s.</summary>
    public void Shake(float amount) => _shake = MathF.Max(_shake, amount);
    /// <summary>View punch: kicks the view up by degrees and springs back (weapon recoil).</summary>
    public void Punch(float degrees) => _punch += degrees;

    private void ApplyCamera()
    {
        float dt = MathF.Min(Time.DeltaTime, 0.05f);
        _punch -= _punch * MathF.Min(1f, dt * 10f);
        _shake = MathF.Max(0f, _shake - dt * 1.5f);
        float sx = (Random.Shared.NextSingle() - 0.5f) * _shake * 3f, sy = (Random.Shared.NextSingle() - 0.5f) * _shake * 3f;
        CameraObject.Transform.LocalEulerAngles = new Float3(_pitch - _punch + sx, _yaw + sy, 0f);
    }

    /// <summary>Moves the player (feet) without collision, e.g. when leaving a vehicle.</summary>
    public void Teleport(Float3 feet, bool moveCamera = true)
    {
        if (!IsFinite(feet)) feet = MapBuilder.SpawnPoint + new Float3(0f, 1f, 0f);
        _body.Transform.Position = feet;
        _verticalVelocity = 0f;
        if (moveCamera) CameraObject.Transform.Position = feet + new Float3(0f, EyeHeight, 0f);
    }

    /// <summary>For tests: aim the view.</summary>
    public void SetLook(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        ApplyCamera();
    }

    /// <summary>Ray from the eye through the crosshair.</summary>
    public bool Trace(float maxDistance, out RaycastHit hit)
    {
        Scene? scene = CameraObject.Scene;
        hit = default;
        if (scene is null) return false;
        return scene.Physics.Raycast(EyePosition, Forward, maxDistance, out hit);
    }
}
