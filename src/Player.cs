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
    private const float WalkSpeed = 5f, RunSpeed = 9f, JumpSpeed = 6f, Gravity = 20f;
    private const float FlySpeed = 10f, LookSensitivity = 0.12f;

    private readonly GameObject _body;
    private readonly CharacterController _controller;
    private float _yaw, _pitch, _verticalVelocity;

    public GameObject CameraObject { get; }
    public Camera Camera { get; }
    public bool Noclip { get; private set; }
    public bool IsGrounded => _controller.IsGrounded;
    public Float3 FeetPosition => _body.Transform.Position;
    public Float3 EyePosition => CameraObject.Transform.Position;
    public Float3 Forward => CameraObject.Transform.Forward;
    public float Yaw => _yaw;

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
        Camera.ClearFlags = CameraClearFlags.SolidColor;
        Camera.ClearColor = new Color(0.53f, 0.70f, 0.86f, 1f);
        Camera.FieldOfView = 70f;
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
            if (Input.GetKey(KeyCode.W)) move += Float2.UnitY;
            if (Input.GetKey(KeyCode.S)) move -= Float2.UnitY;
            if (Input.GetKey(KeyCode.D)) move += Float2.UnitX;
            if (Input.GetKey(KeyCode.A)) move -= Float2.UnitX;
            run = Input.IsShiftPressed;
        }

        if (Noclip)
        {
            ApplyCamera();
            float speed = FlySpeed * (run ? 3f : 1f) * dt;
            Float3 pos = CameraObject.Transform.Position;
            pos += CameraObject.Transform.Forward * move.Y * speed;
            pos += CameraObject.Transform.Right * move.X * speed;
            if (allowInput && Input.GetKey(KeyCode.Space)) pos += Float3.UnitY * speed;
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
        wish *= run ? RunSpeed : WalkSpeed;

        if (_controller.IsGrounded)
        {
            _verticalVelocity = -1f; // small stick-to-ground push
            if (allowInput && Input.GetKeyDown(KeyCode.Space))
                _verticalVelocity = JumpSpeed;
        }
        else
        {
            _verticalVelocity -= Gravity * dt;
        }

        _controller.Move(new Float3(wish.X, _verticalVelocity, wish.Z) * dt);

        // Fell out of the map: respawn at the center.
        if (_body.Transform.Position.Y < -30f)
        {
            _body.Transform.Position = new Float3(0f, 2f, -8f);
            _verticalVelocity = 0f;
        }

        ApplyCamera();
        CameraObject.Transform.Position = _body.Transform.Position + new Float3(0f, EyeHeight, 0f);
    }

    private static Float3 Flat(Float3 v)
    {
        var flat = new Float3(v.X, 0f, v.Z);
        float l = Float3.Length(flat);
        return l > 1e-4f ? flat / l : Float3.Zero;
    }

    private void ApplyCamera() => CameraObject.Transform.LocalEulerAngles = new Float3(_pitch, _yaw, 0f);

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
