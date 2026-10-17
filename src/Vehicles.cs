using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using TextAlignment = Prowl.PaperUI.TextAlignment;

namespace Sandbox;

/// <summary>
/// A dune buggy built the same way as Prowl's CarPhysicsDemo: one chassis Rigidbody3D with four child
/// WheelColliders (raycast suspension), rear-wheel drive through an automatic gearbox.
/// </summary>
public sealed class Buggy
{
    public GameObject Go { get; }
    public Rigidbody3D Body { get; }
    public Prop Prop { get; }
    public List<WheelCollider> Front { get; } = [];
    public List<WheelCollider> Rear { get; } = [];
    public string Name => Go.Name;

    public float MaxSteerDeg = 34f, TopSpeedKmh = 78f, MaxEngineTorque = 95f, IdleRpm = 900f, MaxRpm = 6500f, FinalDrive = 3.9f, BrakeTorque = 1600f;
    public float[] Gears = [3.4f, 2.1f, 1.45f, 1.1f, 0.88f];
    public float ReverseRatio = 3.4f, ShiftUp = 6000f, ShiftDown = 2600f;
    public int Gear { get; private set; } = 1;
    private float _steer;
    public float Rpm { get; private set; }
    public float SpeedKmh => Float3.Dot(Body.LinearVelocity, Go.Transform.Forward) * 3.6f;
    public static readonly Float3 SeatOffset = new(0f, 0.55f, -0.25f);
    private static int _counter;

    public Buggy(SandboxGame g, Float3 pos, float yaw)
    {
        Go = new GameObject($"Buggy #{++_counter}");
        Go.Transform.Position = pos;
        Go.Transform.LocalEulerAngles = new Float3(0f, yaw, 0f);
        var size = new Float3(1.5f, 0.35f, 2.8f);
        var paint = new Color(0.80f, 0.42f, 0.10f, 1f);
        var mat = Mats.New(g.Shader);
        try { mat.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        mat.SetColor("_MainColor", paint);
        MeshRenderer r = Go.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.RoundedBox(size, 0.08f);
        r.Material = mat;
        Body = Go.AddComponent<Rigidbody3D>();
        Body.Mass = 450f;
        Body.AngularDamping = 0.05f;
        Body.LinearDamping = 0.0001f;
        Body.AngularDamping = 0.05f;
        Go.AddComponent<BoxCollider>().Size = size;
        g.World.Add(Go);

        // Visual-only details (no colliders): seat, roll cage, bumper.
        var dark = new Color(0.12f, 0.12f, 0.13f, 1f);
        Detail(g, "Seat", Meshes.RoundedBox(new Float3(0.6f, 0.5f, 0.15f), 0.025f, 2), new Float3(0f, 0.4f, -0.55f), dark);
        Detail(g, "SeatBase", Meshes.RoundedBox(new Float3(0.6f, 0.12f, 0.55f), 0.025f, 2), new Float3(0f, 0.22f, -0.3f), dark);
        foreach (float x in new[] { -0.65f, 0.65f })
        {
            Detail(g, "CageA", Meshes.RoundedBox(new Float3(0.06f, 1.0f, 0.06f), 0.025f, 2), new Float3(x, 0.65f, -0.85f), dark);
            Detail(g, "CageB", Meshes.RoundedBox(new Float3(0.06f, 0.06f, 1.2f), 0.025f, 2), new Float3(x, 1.15f, -0.3f), dark);
            Detail(g, "CageC", Meshes.RoundedBox(new Float3(0.06f, 0.95f, 0.06f), 0.025f, 2), new Float3(x, 0.65f, 0.3f), dark, new Float3(-25f, 0f, 0f));
        }
        Detail(g, "CageTop", Meshes.RoundedBox(new Float3(1.36f, 0.06f, 0.06f), 0.025f, 2), new Float3(0f, 1.15f, -0.85f), dark);
        Detail(g, "Bumper", Meshes.RoundedBox(new Float3(1.6f, 0.15f, 0.15f), 0.025f, 2), new Float3(0f, 0.0f, 1.45f), dark);

        foreach (var (name, lp, front) in new[]
        {
            ("FL", new Float3(-0.85f, -0.15f, 1.05f), true), ("FR", new Float3(0.85f, -0.15f, 1.05f), true),
            ("RL", new Float3(-0.85f, -0.15f, -1.05f), false), ("RR", new Float3(0.85f, -0.15f, -1.05f), false),
        })
        {
            var wheel = new GameObject($"Wheel {name}");
            wheel.Transform.Parent = Go.Transform;
            wheel.Transform.LocalPosition = lp;
            var visual = new GameObject("Visual");
            visual.Transform.Parent = wheel.Transform;
            var mesh = new GameObject("VisualMesh");
            mesh.Transform.Parent = visual.Transform;
            mesh.Transform.LocalEulerAngles = new Float3(0f, 0f, 90f);
            MeshRenderer wr = mesh.AddComponent<MeshRenderer>();
            wr.Mesh = Meshes.Tyre(0.42f, 0.32f);
            var wm = Mats.New(g.Shader);
        try { wm.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
            wm.SetColor("_MainColor", dark);
            wr.Material = wm;
            WheelCollider wc = wheel.AddComponent<WheelCollider>();
            wc.Radius = 0.42f;
            wc.Width = 0.32f;
            wc.SuspensionDistance = 0.3f;
            wc.SuspensionFrequency = 2.2f;
            wc.SuspensionDampingRatio = 0.7f;
            wc.ForwardFriction = 2.5f;
            wc.SidewaysFriction = 1.6f;
            wc.visualTransform = visual.Transform;
            g.World.Add(wheel);
            (front ? Front : Rear).Add(wc);
        }

        Prop = g.Props.Adopt(Go, Body, mat, paint, PropKind.Limb, 0.5f);
    }

    private void Detail(SandboxGame g, string name, Mesh mesh, Float3 lp, Color c, Float3 rot = default)
    {
        var d = new GameObject(name);
        d.Transform.Parent = Go.Transform;
        d.Transform.LocalPosition = lp;
        d.Transform.LocalEulerAngles = rot;
        MeshRenderer r = d.AddComponent<MeshRenderer>();
        r.Mesh = mesh;
        var m = Mats.New(g.Shader);
        try { m.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        m.SetColor("_MainColor", c);
        r.Material = m;
        g.World.Add(d);
    }

    /// <summary>Same drivetrain as CarPhysicsDemo's CarController, but fed explicit input.</summary>
    public void Drive(float steer, bool accel, bool decel, bool handbrake)
    {
        if (!Go.IsValid()) return;
        float fwd = Float3.Dot(Body.LinearVelocity, Go.Transform.Forward);
        // Smooth steering that tightens at speed (less twitchy, GMod-jeep-like).
        float speedK = Math.Clamp(MathF.Abs(fwd) * 3.6f / TopSpeedKmh, 0f, 1f);
        _steer += (steer - _steer) * 0.15f;
        float steerRad = _steer * MaxSteerDeg * (1f - 0.55f * speedK) * MathF.PI / 180f;
        foreach (WheelCollider w in Front) w.SteerAngle = steerRad;
        // Handbrake lets the rear step out a bit for drifts.
        foreach (WheelCollider w in Rear) w.SidewaysFriction = handbrake ? 0.8f : 1.6f;
        float throttle = 0f;
        bool braking = false;
        if (accel) { if (Gear < 1) Gear = 1; throttle = 1f; }
        else if (decel)
        {
            if (fwd > 0.5f) braking = true;
            else { if (Gear >= 0) Gear = -1; throttle = 1f; }
        }
        else if (Gear < 0 && MathF.Abs(fwd) < 0.5f) Gear = 1;

        float av = Rear.Count > 0 ? Rear.Average(w => w.AngularVelocity) : 0f;
        float ratio = GearRatio(Gear) * FinalDrive;
        Rpm = RpmFor(av, ratio);
        if (Gear >= 1)
        {
            if (Rpm > ShiftUp && Gear < Gears.Length) Gear++;
            else if (Rpm < ShiftDown && Gear > 1) Gear--;
            ratio = GearRatio(Gear) * FinalDrive;
            Rpm = RpmFor(av, ratio);
        }
        float t = Math.Clamp(Rpm / MaxRpm, 0f, 1f);
        // Rev limiter / top speed governor.
        if (Gear > 0 && fwd * 3.6f > TopSpeedKmh) throttle = 0f;
        if (Gear < 0 && -fwd * 3.6f > 25f) throttle = 0f;
        float torque = MaxEngineTorque * Math.Clamp(1f - 1.25f * (t - 0.6f) * (t - 0.6f), 0.2f, 1f) * throttle;
        float perWheel = Rear.Count > 0 ? torque * ratio * 0.9f / Rear.Count : 0f;
        foreach (WheelCollider w in Front) { w.MotorTorque = 0f; w.BrakeTorque = braking ? BrakeTorque : 0f; }
        foreach (WheelCollider w in Rear) { w.MotorTorque = perWheel; w.BrakeTorque = (braking ? BrakeTorque : 0f) + (handbrake ? BrakeTorque : 0f); }
        if (accel || decel) Body.SetActive(true);
    }

    private float GearRatio(int gear) => gear == 0 ? 0f : gear < 0 ? -ReverseRatio : Gears[gear - 1];
    private float RpmFor(float wheelAv, float ratio) => Math.Clamp(MathF.Abs(wheelAv * 60f / (2f * MathF.PI) * ratio), IdleRpm, MaxRpm);

    /// <summary>Gently rights a flipped buggy (R while driving).</summary>
    public void Flip()
    {
        Go.Transform.Position += new Float3(0f, 1.2f, 0f);
        Go.Transform.LocalEulerAngles = new Float3(0f, Go.Transform.LocalEulerAngles.Y, 0f);
        Body.AngularVelocity = Float3.Zero;
        Body.LinearVelocity = Float3.Zero;
    }
}

/// <summary>Owns buggies: E to enter/exit, chase camera with mouse orbit, minimal speedometer chip.</summary>
public sealed class VehicleSystem(SandboxGame game)
{
    public List<Buggy> All { get; } = [];
    public Buggy? Driving { get; private set; }
    public bool InVehicle => Driving is not null;
    private float _orbitYaw, _orbitPitch = 12f;
    private AudioSource? _engine;

    public static void Register()
    {
        SpawnCatalog.Register(new SpawnEntry("vehicle_buggy", "Buggy", MenuTab.Vehicles, "Cars", Icon.Car, null,
            (g, pos, yaw) => g.Vehicles.SpawnBuggy(pos + new Float3(0f, 0.8f, 0f), yaw).Name, 0.5f));
    }

    public Buggy SpawnBuggy(Float3 pos, float yaw, bool recordUndo = true)
    {
        var b = new Buggy(game, pos, yaw);
        All.Add(b);
        if (recordUndo) game.Props.PushUndo(new BuggyUndo(this, b));
        return b;
    }

    public void Remove(Buggy b)
    {
        if (ReferenceEquals(Driving, b)) Exit();
        All.Remove(b);
        game.Props.Remove(b.Prop);
    }

    public void ClearAll() { foreach (Buggy b in All.ToArray()) Remove(b); }

    private sealed class BuggyUndo(VehicleSystem vs, Buggy b) : IUndoable
    {
        public string Label => $"Undone: {b.Name}";
        public void Undo() => vs.Remove(b);
    }

    /// <summary>E: exit if driving, else enter the buggy you're looking at (within 3.5 m).</summary>
    public bool TryUse()
    {
        if (Driving is not null) { Exit(); return true; }
        if (!game.Player.Trace(3.5f, out RaycastHit hit)) return false;
        Buggy? b = All.FirstOrDefault(x => ReferenceEquals(x.Body, hit.Rigidbody));
        if (b is null) return false;
        Enter(b);
        return true;
    }

    public void Enter(Buggy b)
    {
        game.PhysGun.Release(keepVelocity: false);
        game.Carry.Drop();
        Driving = b;
        _orbitYaw = 0f;
        _orbitPitch = 12f;
        game.Sounds.Play("sfx_door", 0.6f);
        _engine = game.Sounds.Loop("sfx_engine", b.Go);
        game.Notify($"Driving {b.Name} · E to exit");
    }

    public void Exit()
    {
        if (Driving is not { } b) return;
        b.Drive(0f, false, false, true);
        Float3 side = b.Go.Transform.Right * -1.8f;
        game.Player.Teleport(b.Go.Transform.Position + side + new Float3(0f, 0.2f, 0f));
        game.Player.SetLook(b.Go.Transform.LocalEulerAngles.Y, 0f);
        if (_engine is not null) { try { _engine.Stop(); } catch { } _engine = null; }
        Driving = null;
        game.Sounds.Play("sfx_door", 0.6f);
    }

    public void Tick(float dt, bool allowInput)
    {
        foreach (Buggy b in All.ToArray()) if (!b.Go.IsValid() || b.Go.Scene == null) { if (ReferenceEquals(Driving, b)) Driving = null; All.Remove(b); }
        if (Driving is not { } car) return;
        float steer = 0f;
        bool w = false, s = false, space = false;
        if (allowInput)
        {
            if (Input.GetKey(KeyCode.A)) steer -= 1f;
            if (Input.GetKey(KeyCode.D)) steer += 1f;
            w = Input.GetKey(KeyCode.W);
            s = Input.GetKey(KeyCode.S);
            space = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.R)) car.Flip();
            Float2 md = Input.MouseDelta * 0.12f * game.Settings.MouseSensitivity;
            _orbitYaw += md.X;
            _orbitPitch = Math.Clamp(_orbitPitch + md.Y, -10f, 60f);
        }
        car.Drive(steer, w, s, space);
        if (_engine is not null) { try { _engine.Pitch = 0.6f + car.Rpm / car.MaxRpm * 1.4f; } catch { } }

        // Keep the (invisible) player body parked above the car so it doesn't block anything.
        game.Player.Teleport(car.Go.Transform.Position + new Float3(0f, 40f, 0f), moveCamera: false);

        // Chase camera: orbit around the car, smoothed.
        float yaw = (car.Go.Transform.LocalEulerAngles.Y + _orbitYaw) * MathF.PI / 180f;
        float pitch = _orbitPitch * MathF.PI / 180f;
        Float3 back = new(-MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
        Float3 focus = car.Go.Transform.Position + new Float3(0f, 1.0f, 0f);
        Float3 want = focus + back * 6.5f;
        Transform cam = game.Player.CameraObject.Transform;
        cam.Position = cam.Position + (want - cam.Position) * Math.Clamp(dt * 8f, 0f, 1f);
        cam.Forward = Float3.Normalize(focus - cam.Position);
    }

    public void DrawHud(Paper paper, float w, float h, float m)
    {
        if (Driving is not { } car) return;
        string speed = $"{MathF.Abs(car.SpeedKmh):0}";
        string gear = car.Gear < 0 ? "R" : car.Gear.ToString();
        float ch = Theme.S(34), cw = Theme.S(150);
        float x = w - m - cw, y = h - m - ch;
        Ui.Glass(paper, "veh_chip", x, y, cw, ch, ch / 2f, shadow: false).IsNotInteractable();
        Ui.Icon(paper, "veh_ic", x + Theme.S(10), y + (ch - Theme.S(14)) / 2f, Theme.S(14), Icon.Car, Theme.TextMuted);
        Ui.Text(paper, "veh_spd", x + Theme.S(30), y, Theme.S(56), ch, speed, Theme.Title, Theme.Text, Theme.Bold, TextAlignment.MiddleRight);
        Ui.Text(paper, "veh_unit", x + Theme.S(90), y, Theme.S(40), ch, "km/h", Theme.Micro, Theme.TextMuted, Theme.SemiBold);
        Ui.Text(paper, "veh_gear", x + cw - Theme.S(26), y, Theme.S(16), ch, gear, Theme.Caption, Theme.Accent, Theme.Bold, TextAlignment.MiddleCenter);
        // RPM bar: a hairline under the chip.
        float k = Math.Clamp((car.Rpm - car.IdleRpm) / (car.MaxRpm - car.IdleRpm), 0f, 1f);
        Ui.Rect(paper, "veh_rpm_bg", x + Theme.S(14), y + ch - Theme.S(4), cw - Theme.S(28), Theme.S(2), Theme.WithAlpha(Theme.Border, 1f), Theme.S(1));
        Ui.Rect(paper, "veh_rpm", x + Theme.S(14), y + ch - Theme.S(4), (cw - Theme.S(28)) * k, Theme.S(2), k > 0.9f ? Theme.Warning : Theme.Accent, Theme.S(1));
    }
}
