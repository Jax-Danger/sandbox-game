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

    public string Kind { get; }
    public bool IsAirboat => Kind == "airboat";
    private Transform? _fan;
    public static string Title(string kind) => kind switch { "jeep" => "Jeep", "airboat" => "Airboat", _ => "Buggy" };

    public Buggy(SandboxGame g, Float3 pos, float yaw, string kind = "buggy")
    {
        Kind = kind;
        Go = new GameObject($"{Title(kind)} #{++_counter}");
        Go.Transform.Position = pos;
        Go.Transform.LocalEulerAngles = new Float3(0f, yaw, 0f);
        bool jeep = kind == "jeep", boat = kind == "airboat";
        var size = jeep ? new Float3(1.8f, 0.55f, 3.4f) : boat ? new Float3(1.9f, 0.3f, 3.6f) : new Float3(1.5f, 0.35f, 2.8f);
        var paint = jeep ? new Color(0.33f, 0.38f, 0.22f, 1f) : boat ? new Color(0.20f, 0.36f, 0.52f, 1f) : new Color(0.80f, 0.42f, 0.10f, 1f);
        var mat = Mats.New(g.Shader);
        try { mat.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        mat.SetColor("_MainColor", paint);
        MeshRenderer r = Go.AddComponent<MeshRenderer>();
        r.Mesh = jeep ? VehicleModels.JeepBody() : boat ? VehicleModels.Hull() : VehicleModels.BuggyBody();
        r.Material = mat;
        Body = Go.AddComponent<Rigidbody3D>();
        Body.Mass = jeep ? 750f : boat ? 380f : 450f;
        if (jeep) { MaxEngineTorque = 170f; TopSpeedKmh = 92f; MaxSteerDeg = 30f; }
        Body.AngularDamping = 0.05f;
        Body.LinearDamping = 0.0001f;
        Body.AngularDamping = 0.05f;
        Go.AddComponent<BoxCollider>().Size = size;
        g.World.Add(Go);

        var dark = new Color(0.12f, 0.12f, 0.13f, 1f);
        if (jeep) JeepDetails(g, paint, dark);
        else if (boat) BoatDetails(g, dark);
        else {
        // Visual-only details (no colliders): bucket seat, bent-tube roll cage, tube bumper.
        Detail(g, "Seat", VehicleModels.Seat(0.27f), new Float3(0f, 0.22f, -0.4f), dark);
        var cage = new Color(0.16f, 0.16f, 0.17f, 1f);
        foreach (float x in new[] { -0.62f, 0.62f })
            Detail(g, "Cage", VehicleModels.BentPipe(0.032f, 0.18f, Float3.UnitX, new Float3(x, 0.15f, -0.95f), new Float3(x, 1.15f, -0.85f), new Float3(x * 0.9f, 1.15f, 0.0f), new Float3(x, 0.15f, 0.55f)), new Float3(0f, 0f, 0f), cage);
        Detail(g, "CageTop", VehicleModels.BentPipe(0.032f, 0.1f, Float3.UnitY, new Float3(-0.62f, 1.15f, -0.85f), new Float3(0.62f, 1.15f, -0.85f)), Float3.Zero, cage);
        Detail(g, "CageX", VehicleModels.BentPipe(0.03f, 0.1f, Float3.UnitY, new Float3(-0.56f, 1.15f, 0.0f), new Float3(0.56f, 1.15f, 0.0f)), Float3.Zero, cage);
        Detail(g, "Bumper", VehicleModels.BentPipe(0.05f, 0.15f, Float3.UnitY, new Float3(-0.7f, 0.1f, 1.25f), new Float3(-0.55f, 0.02f, 1.52f), new Float3(0.55f, 0.02f, 1.52f), new Float3(0.7f, 0.1f, 1.25f)), Float3.Zero, cage);
        foreach (float x in new[] { -0.2f, 0.2f })
            Detail(g, "Light", VehicleModels.Lamp(0.07f), new Float3(x, 0.16f, 1.38f), new Color(0.95f, 0.92f, 0.75f, 1f));
        }
        float wx = jeep ? 0.95f : boat ? 0.7f : 0.85f, wz = jeep ? 1.25f : boat ? 1.2f : 1.05f, wy = jeep ? -0.2f : boat ? -0.1f : -0.15f, rad = jeep ? 0.5f : boat ? 0.25f : 0.42f;

        foreach (var (name, lp, front) in new[]
        {
            ("FL", new Float3(-wx, wy, wz), true), ("FR", new Float3(wx, wy, wz), true),
            ("RL", new Float3(-wx, wy, -wz), false), ("RR", new Float3(wx, wy, -wz), false),
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
            wr.Mesh = Meshes.Tyre(rad, jeep ? 0.38f : 0.32f);
            if (boat) wr.Enabled = false; // skids: the hull slides on hidden rollers
            var wm = Mats.New(g.Shader);
        try { wm.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
            wm.SetColor("_MainColor", dark);
            wr.Material = wm;
            WheelCollider wc = wheel.AddComponent<WheelCollider>();
            wc.Radius = rad;
            wc.Width = jeep ? 0.38f : 0.32f;
            wc.SuspensionDistance = 0.3f;
            wc.SuspensionFrequency = 2.2f;
            wc.SuspensionDampingRatio = 0.7f;
            wc.ForwardFriction = boat ? 0.04f : 2.5f;
            wc.SidewaysFriction = boat ? 0.35f : 1.6f;
            _visuals[wc] = visual.Transform; // synced by SyncWheels (exactly the physics steer frame)
            g.World.Add(wheel);
            (front ? Front : Rear).Add(wc);
        }

        Prop = g.Props.Adopt(Go, Body, mat, paint, PropKind.Limb, 0.5f);
    }

    private void JeepDetails(SandboxGame g, Color paint, Color dark)
    {
        Detail(g, "Hood", VehicleModels.JeepHood(), Float3.Zero, paint);
        foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -1.25f, 1.25f })
            Detail(g, "Fender", VehicleModels.Fender(x, z, 0.5f), Float3.Zero, paint * 0.85f);
        Detail(g, "Grille", VehicleModels.Grille(), Float3.Zero, dark);
        foreach (float x in new[] { -0.6f, 0.6f })
            Detail(g, "Light", VehicleModels.Lamp(0.12f), new Float3(x, 0.4f, 1.72f), new Color(0.95f, 0.92f, 0.75f, 1f));
        Detail(g, "Windshield", VehicleModels.Windshield(), new Float3(0f, 0.85f, 0.5f), new Color(0.55f, 0.68f, 0.78f, 1f), new Float3(-12f, 0f, 0f));
        Detail(g, "WindFrame", VehicleModels.BentPipe(0.03f, 0.08f, Float3.UnitZ, new Float3(-0.8f, -0.32f, 0f), new Float3(-0.8f, 0.32f, 0f), new Float3(0.8f, 0.32f, 0f), new Float3(0.8f, -0.32f, 0f)), new Float3(0f, 0.85f, 0.5f), dark, new Float3(-12f, 0f, 0f));
        foreach (float x in new[] { -0.4f, 0.4f })
            Detail(g, "Seat", VehicleModels.Seat(0.24f), new Float3(x, 0.36f, -0.42f), dark);
        Detail(g, "RollBar", VehicleModels.BentPipe(0.04f, 0.2f, Float3.UnitZ, new Float3(-0.8f, 0.3f, -0.9f), new Float3(-0.8f, 1.3f, -0.9f), new Float3(0.8f, 1.3f, -0.9f), new Float3(0.8f, 0.3f, -0.9f)), Float3.Zero, dark);
        var spare = Detail(g, "Spare", Meshes.Tyre(0.42f, 0.25f), new Float3(0f, 0.55f, -1.82f), dark, new Float3(90f, 0f, 0f));
        Detail(g, "Bumper", VehicleModels.BentPipe(0.07f, 0.12f, Float3.UnitY, new Float3(-0.95f, 0.05f, 1.72f), new Float3(-0.9f, 0.05f, 1.8f), new Float3(0.9f, 0.05f, 1.8f), new Float3(0.95f, 0.05f, 1.72f)), Float3.Zero, dark);
    }

    private void BoatDetails(SandboxGame g, Color dark)
    {
        var white = new Color(0.86f, 0.87f, 0.88f, 1f);
        foreach (float x in new[] { -0.9f, 0.9f })
            Detail(g, "Rail", VehicleModels.BentPipe(0.04f, 0.2f, Float3.UnitY, new Float3(x * 0.98f, 0.2f, -1.78f), new Float3(x, 0.215f, -1.4f), new Float3(x, 0.215f, 1.1f), new Float3(x * 0.88f, 0.27f, 1.7f), new Float3(x * 0.62f, 0.33f, 2.06f)), Float3.Zero, white);
        Detail(g, "Seat", VehicleModels.Seat(0.3f), new Float3(0f, 0.25f, -0.35f), dark);
        Detail(g, "Mast", VehicleModels.BentPipe(0.04f, 0.1f, Float3.UnitX, new Float3(-0.45f, 0.18f, -1.2f), new Float3(0f, 1.25f, -1.25f), new Float3(0.45f, 0.18f, -1.2f)), Float3.Zero, dark);
        Detail(g, "Cage", VehicleModels.Ring(0.85f, 0.035f), new Float3(0f, 1.25f, -1.45f), new Color(0.3f, 0.3f, 0.32f, 1f));
        Detail(g, "Hub", VehicleModels.Hub(0.16f), new Float3(0f, 1.25f, -1.38f), dark);
        var fan = new GameObject("Fan");
        fan.Transform.Parent = Go.Transform;
        fan.Transform.LocalPosition = new Float3(0f, 1.25f, -1.4f);
        g.World.Add(fan);
        _fan = fan.Transform;
        foreach (float a in new[] { 0f, 120f, 240f })
        {
            var blade = new GameObject("Blade");
            blade.Transform.Parent = fan.Transform;
            blade.Transform.LocalEulerAngles = new Float3(0f, 0f, a);
            blade.Transform.LocalPosition = Float3.Zero;
            var child = new GameObject("BladeMesh");
            child.Transform.Parent = blade.Transform;
            child.Transform.LocalPosition = Float3.Zero;
            MeshRenderer r = child.AddComponent<MeshRenderer>();
            r.Mesh = VehicleModels.Blade();
            var m = Mats.New(g.Shader); m.SetColor("_MainColor", new Color(0.7f, 0.55f, 0.3f, 1f)); r.Material = m;
            g.World.Add(blade); g.World.Add(child);
        }
        Detail(g, "Rudder", Meshes.Sweep([new Meshes.Sec(new Float3(0f, -0.4f, 0f), 0.025f, 0.2f, 2.5f), new Meshes.Sec(new Float3(0f, 0.4f, -0.03f), 0.02f, 0.16f, 2.5f)], Float3.UnitX, 14, 0.5f), new Float3(0f, 1.2f, -1.75f), dark);
    }

    public float Thrust { get; private set; }
    private readonly Dictionary<WheelCollider, Transform> _visuals = new();
    /// <summary>Steering-wheel input in -1..1 after smoothing (drives the steering-wheel mesh and the driver's arms).</summary>
    public float SteerInput => _steer;

    /// <summary>Places each wheel visual at the suspension-resolved centre, steered about the mount's up axis with the
    /// same convention the physics uses, and spun about the axle. Done in game code so the visuals can't drift or
    /// steer the wrong way relative to the contact frame.</summary>
    public void SyncWheels()
    {
        foreach (var (wc, vis) in _visuals)
        {
            if (!wc.GameObject.IsValid()) continue;
            Transform mount = wc.Transform;
            Float3 up = Float3.Normalize(mount.Up);
            var m = Jitter2.LinearMath.JMatrix.CreateRotationMatrix(new Jitter2.LinearMath.JVector(up.X, up.Y, up.Z), wc.SteerAngle);
            var f = Jitter2.LinearMath.JVector.Transform(new Jitter2.LinearMath.JVector(mount.Forward.X, mount.Forward.Y, mount.Forward.Z), m);
            Float3 fwd = Float3.Normalize(new Float3(f.X, f.Y, f.Z));
            vis.Position = wc.GetWheelCenter();
            vis.Rotation = Quaternion.LookRotation(fwd, up) * Quaternion.AxisAngle(Float3.UnitX, wc.WheelRotation);
        }
    }

    private GameObject Detail(SandboxGame g, string name, Mesh mesh, Float3 lp, Color c, Float3 rot = default)
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
        return d;
    }

    /// <summary>Same drivetrain as CarPhysicsDemo's CarController, but fed explicit input.</summary>
    public void Drive(float steer, bool accel, bool decel, bool handbrake)
    {
        if (!Go.IsValid()) return;
        if (IsAirboat) { DriveBoat(steer, accel, decel, handbrake); return; }
        float fwd = Float3.Dot(Body.LinearVelocity, Go.Transform.Forward);
        float dt = Math.Clamp(Time.DeltaTime, 0.0005f, 0.05f);
        // Speed-sensitive steering: eases in, returns to centre faster than it turns in, and the lock shrinks with speed.
        float speedK = Math.Clamp(MathF.Abs(fwd) * 3.6f / TopSpeedKmh, 0f, 1f);
        float rate = (MathF.Abs(steer) < 0.01f || MathF.Sign(steer) != MathF.Sign(_steer)) ? 6f : 2.6f;
        _steer += Math.Clamp(steer - _steer, -rate * dt, rate * dt);
        float steerRad = _steer * MaxSteerDeg * (1f - 0.68f * speedK) * MathF.PI / 180f;
        foreach (WheelCollider w in Front) w.SteerAngle = steerRad;
        foreach (WheelCollider w in Rear) w.SidewaysFriction = handbrake ? 1.0f : 2.2f;
        Stabilise(fwd, steerRad, handbrake, dt);
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
        bool awd = false; // rear-wheel drive for every car: predictable, no four-wheel-drive twitch
        // Traction control: back off torque while the driven wheels spin much faster than the ground speed.
        if (Rear.Count > 0 && throttle > 0f)
        {
            float slip = MathF.Abs(av) * Rear[0].Radius - MathF.Abs(fwd);
            if (slip > 2.5f) perWheel *= Math.Clamp(1f - (slip - 2.5f) / 5f, 0.25f, 1f);
        }
        foreach (WheelCollider w in Front) { w.MotorTorque = awd ? perWheel : 0f; w.BrakeTorque = braking ? BrakeTorque : 0f; }
        foreach (WheelCollider w in Rear) { w.MotorTorque = perWheel; w.BrakeTorque = (braking ? BrakeTorque : 0f) + (handbrake ? BrakeTorque : 0f); }
        if (accel || decel) Body.SetActive(true);
    }

    /// <summary>Arcade assists, applied as small per-frame velocity corrections (frame-rate independent):
    /// anti-roll + a virtual low centre of mass, yaw-rate tracking so the car follows the wheels (no spinouts),
    /// lateral grip assist and a little speed-squared downforce.</summary>
    private void Stabilise(float fwd, float steerRad, bool handbrake, float dt)
    {
        bool grounded = Front.Concat(Rear).Count(w => w.IsGrounded) >= 3;
        Float3 up = Go.Transform.Up, right = Go.Transform.Right, forward = Go.Transform.Forward;
        Float3 av = Body.AngularVelocity, v = Body.LinearVelocity;
        if (grounded)
        {
            // Roll and pitch: damp the rate and pull back toward the ground-plane (acts like anti-roll bars + low COM).
            float roll = Float3.Dot(av, forward), pitch = Float3.Dot(av, right);
            float rollAngle = MathF.Asin(Math.Clamp(Float3.Dot(right, Float3.UnitY), -1f, 1f));
            float pitchAngle = MathF.Asin(Math.Clamp(Float3.Dot(forward, Float3.UnitY), -1f, 1f));
            av -= forward * (roll * Math.Clamp(6f * dt, 0f, 1f) - rollAngle * 9f * dt);
            av -= right * (pitch * Math.Clamp(3f * dt, 0f, 1f) + pitchAngle * 3f * dt);
            // Yaw tracking: steer toward the yaw rate the front wheels ask for (bicycle model), never beyond it.
            float wheelbase = 2f * MathF.Abs(Front.Count > 0 ? Front[0].Transform.LocalPosition.Z : 1.1f);
            float wantYaw = fwd * MathF.Tan(steerRad) / MathF.Max(wheelbase, 0.5f);
            float yaw = Float3.Dot(av, up);
            float k = handbrake ? 1.5f : 7f;
            av += up * ((wantYaw - yaw) * Math.Clamp(k * dt, 0f, 1f));
            // Lateral grip assist: bleed sideways slide.
            float lat = Float3.Dot(v, right);
            v -= right * (lat * Math.Clamp((handbrake ? 0.6f : 3.5f) * dt, 0f, 1f));
            // Downforce.
            v -= up * (0.0025f * fwd * fwd * dt * 9.81f);
            Body.AngularVelocity = av;
            Body.LinearVelocity = v;
        }
    }

    /// <summary>Airboat: a big fan pushes the hull; steering yaws it. Wheels are frictionless skids.</summary>
    private void DriveBoat(float steer, bool accel, bool decel, bool brake)
    {
        float fwd = Float3.Dot(Body.LinearVelocity, Go.Transform.Forward);
        float want = accel ? 1f : decel ? -0.5f : 0f;
        Thrust += (want - Thrust) * 0.08f;
        if (fwd * 3.6f > 85f && Thrust > 0f) Thrust = 0f;
        Body.AddForce(Go.Transform.Forward * (Thrust * 5200f));
        float yawRate = Float3.Dot(Body.AngularVelocity, Go.Transform.Up);
        Body.AddTorque(Go.Transform.Up * ((steer * 1.6f - yawRate) * 900f));
        // Bleed sideways slide so it carves instead of drifting forever.
        Float3 side = Go.Transform.Right * Float3.Dot(Body.LinearVelocity, Go.Transform.Right);
        Body.AddForce(side * -(brake ? 700f : 260f) + Go.Transform.Forward * (brake ? -fwd * 400f : 0f));
        foreach (WheelCollider w in Front.Concat(Rear)) { w.MotorTorque = 0f; w.BrakeTorque = 0f; w.SteerAngle = 0f; }
        Rpm = IdleRpm + MathF.Abs(Thrust) * (MaxRpm - IdleRpm);
        Gear = Thrust < -0.05f ? -1 : 1;
        if (_fan is not null) _fan.LocalEulerAngles = new Float3(0f, 0f, _fan.LocalEulerAngles.Z + 6f + 40f * MathF.Abs(Thrust));
        if (accel || decel) Body.SetActive(true);
    }

    private float GearRatio(int gear) => gear == 0 ? 0f : gear < 0 ? -ReverseRatio : Gears[gear - 1];
    private float RpmFor(float wheelAv, float ratio) => Math.Clamp(MathF.Abs(wheelAv * 60f / (2f * MathF.PI) * ratio), IdleRpm, MaxRpm);

    /// <summary>Gently rights a flipped buggy (R while driving).</summary>
    public void Flip()
    {
        if (Body.MotionType != Jitter2.Dynamics.MotionType.Dynamic) return; // frozen with the physgun
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
    /// <summary>Scripted driving input (drive tests / screenshots); overrides the keyboard when set.</summary>
    public (float Steer, bool Throttle, bool Brake, bool Handbrake)? ScriptInput;
    private float _orbitYaw, _orbitPitch = 12f;
    private AudioSource? _engine;

    public static void Register()
    {
        SpawnCatalog.Register(new SpawnEntry("vehicle_buggy", "Buggy", MenuTab.Vehicles, "Cars", Icon.Car, null,
            (g, pos, yaw) => g.Vehicles.SpawnBuggy(pos + new Float3(0f, 0.8f, 0f), yaw).Name, 0.5f));
        SpawnCatalog.Register(new SpawnEntry("vehicle_jeep", "Jeep", MenuTab.Vehicles, "Cars", Icon.Car, null,
            (g, pos, yaw) => g.Vehicles.SpawnBuggy(pos + new Float3(0f, 1.0f, 0f), yaw, kind: "jeep").Name, 0.5f));
        SpawnCatalog.Register(new SpawnEntry("vehicle_airboat", "Airboat", MenuTab.Vehicles, "Boats", Icon.Car, null,
            (g, pos, yaw) => g.Vehicles.SpawnBuggy(pos + new Float3(0f, 0.7f, 0f), yaw, kind: "airboat").Name, 0.5f));
    }

    public Buggy SpawnBuggy(Float3 pos, float yaw, bool recordUndo = true, string kind = "buggy")
    {
        var b = new Buggy(game, pos, yaw, kind);
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
        foreach (Buggy b in All) b.SyncWheels();
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
        if (ScriptInput is { } si) { steer = si.Steer; w = si.Throttle; s = si.Brake; space = si.Handbrake; }
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
