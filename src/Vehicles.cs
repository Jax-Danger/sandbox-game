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
        var paint = jeep ? new Color(0.22f, 0.26f, 0.14f, 1f) : boat ? new Color(0.20f, 0.36f, 0.52f, 1f) : new Color(0.80f, 0.42f, 0.10f, 1f);
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
        {
            // Off-road spot lamps in black steel buckets on short stalks above the cowl (were half-sunk into the nose).
            Float3 la = new(x, 0.43f, 1.16f);
            HeadLamp(g, VehicleModels.Lamp(0.07f), 0.07f, la, new Float3(-8f, 0f, 0f));
            Detail(g, "LampBucket", Meshes.LatheZ([(0.0001f, -0.045f), (0.05f, -0.04f), (0.074f, -0.01f), (0.079f, 0.03f), (0.072f, 0.032f)], 24), la, cage, new Float3(-8f, 0f, 0f));
            Detail(g, "LampStalk", VehicleModels.Pipe(0.012f, Float3.UnitZ, la + new Float3(0f, -0.03f, -0.03f), new Float3(x, 0.33f, 1.12f)), Float3.Zero, cage);
        }
        // Dune-buggy body: raised fibreglass nose cowl with a scooped dash, rear wheel arches, side nerf bars,
        // rear engine with air filter and twin exhausts. (Was a flat plank with a cage on it.)
        Detail(g, "Nose", VehicleModels.BuggyNose(), Float3.Zero, paint);
        Detail(g, "Dash", VehicleModels.BuggyDash(), Float3.Zero, dark);
        // Twin racing stripes over the nose cowl, following its crown (top = centre y + half height of each Tub section).
        {
            (float z, float top)[] crown = [(0.45f, 0.36f), (0.8f, 0.4f), (1.1f, 0.35f), (1.32f, 0.25f), (1.44f, 0.16f)];
            foreach (float sx in new[] { -0.075f, 0.075f })
            {
                var ss = new List<Meshes.Sec>();
                for (int i = 0; i <= 16; i++)
                {
                    float z = 0.5f + 0.9f * i / 16f;
                    int j = 0; while (j < crown.Length - 2 && z > crown[j + 1].z) j++;
                    float t = Math.Clamp((z - crown[j].z) / (crown[j + 1].z - crown[j].z), 0f, 1f);
                    float y = crown[j].top + (crown[j + 1].top - crown[j].top) * t + 0.009f + 0.012f * MathF.Max(0f, z - 1.1f) / 0.34f;
                    ss.Add(new Meshes.Sec(new Float3(sx, y, z), 0.032f, 0.003f, 4f));
                }
                Detail(g, "Stripe", Meshes.Sweep(ss, Float3.UnitX, 10, 0.4f), Float3.Zero, new Color(0.06f, 0.06f, 0.065f, 1f));
            }
        }
        foreach (float x in new[] { -0.85f, 0.85f })
        {
            Detail(g, "Arch", VehicleModels.BuggyArch(x, -1.05f, 0.42f), Float3.Zero, paint * 0.9f);
            Detail(g, "Nerf", VehicleModels.BentPipe(0.035f, 0.12f, Float3.UnitY, new Float3(x * 0.8f, 0.12f, 0.55f), new Float3(x * 0.98f, 0.0f, 0.35f), new Float3(x * 0.98f, 0.0f, -0.55f), new Float3(x * 0.8f, 0.12f, -0.72f)), Float3.Zero, cage);
        }
        // Fibreglass panel detail: rubber edge trim where the cowl meets the cockpit, a moulded centre seam, chrome
        // fuel filler and twin hood pins on the hump, and a rivet row + pressed character line along each tub side.
        var rubber = new Color(0.05f, 0.05f, 0.055f, 1f); var chrome = new Color(0.78f, 0.79f, 0.81f, 1f);
        Detail(g, "CowlTrim", VehicleModels.BentPipe(0.018f, 0.12f, Float3.UnitY, new Float3(-0.6f, 0.2f, 0.27f), new Float3(-0.5f, 0.27f, 0.3f), new Float3(0.5f, 0.27f, 0.3f), new Float3(0.6f, 0.2f, 0.27f)), Float3.Zero, rubber);
        Detail(g, "NoseSeam", VehicleModels.Pipe(0.006f, Float3.UnitY, new Float3(0f, 0.355f, 0.45f), new Float3(0f, 0.398f, 0.8f), new Float3(0f, 0.348f, 1.1f), new Float3(0f, 0.25f, 1.32f)), Float3.Zero, paint * 0.82f);
        Detail(g, "FuelCap", Meshes.Lathe("bfuel", [(0.0001f, 0.022f), (0.045f, 0.022f), (0.05f, 0.012f), (0.05f, 0f), (0.0001f, 0f)], 24), new Float3(0.3f, 0.385f, 0.72f), chrome);
        foreach (float x in new[] { -0.38f, 0.38f })
        {
            Detail(g, "HoodPin", VehicleModels.Pipe(0.009f, Float3.UnitX, new Float3(x, 0.36f, 1.05f), new Float3(x, 0.4f, 1.05f)), Float3.Zero, chrome);
            Detail(g, "PinClip", VehicleModels.Ring(0.022f, 0.004f), new Float3(x, 0.395f, 1.05f), chrome, new Float3(90f, 0f, 0f));
        }
        foreach (float x in new[] { -1f, 1f })
        {
            Detail(g, "TubRivets", VehicleModels.Rivets(new Float3(x * 0.735f, 0.15f, -1.0f), new Float3(x * 0.735f, 0.15f, 0.3f), 11, 0.008f), Float3.Zero, chrome);
            Detail(g, "TubLine", VehicleModels.Pipe(0.009f, Float3.UnitY, new Float3(x * 0.742f, 0.02f, -1.05f), new Float3(x * 0.742f, 0.02f, 0.35f)), Float3.Zero, paint * 1.1f);
        }
        var metal = new Color(0.55f, 0.56f, 0.58f, 1f);
        Detail(g, "Engine", VehicleModels.BuggyEngine(), new Float3(0f, 0.3f, -1.15f), new Color(0.28f, 0.29f, 0.3f, 1f));
        Detail(g, "Filter", VehicleModels.Lamp(0.1f), new Float3(0.22f, 0.62f, -1.0f), metal, new Float3(-90f, 0f, 0f));
        foreach (float x in new[] { -0.22f, -0.12f })
            Detail(g, "Exhaust", VehicleModels.BentPipe(0.028f, 0.08f, Float3.UnitX, new Float3(x, 0.3f, -1.1f), new Float3(x, 0.32f, -1.38f), new Float3(x, 0.42f, -1.52f)), Float3.Zero, metal);
        Detail(g, "Plate", Meshes.RoundedBox(new Float3(0.34f, 0.12f, 0.015f), 0.01f), new Float3(0.15f, 0.14f, -1.42f), new Color(0.92f, 0.9f, 0.8f, 1f));
        foreach (float x in new[] { -0.42f, 0.42f })
            Detail(g, "Tail", VehicleModels.Lamp(0.045f), new Float3(x, 0.14f, -1.4f), new Color(0.8f, 0.08f, 0.06f, 1f), new Float3(0f, 180f, 0f));
        // Embossed registration characters (was a plain blue bar) with a thin blue border strip along the top.
        Detail(g, "PlateText", Glyphs.Text("SBX 017", 0.048f, -0.004f, 0f, mirror: false), new Float3(0.15f, 0.13f, -1.4275f), new Color(0.12f, 0.16f, 0.4f, 1f));
        Detail(g, "PlateBand", Meshes.RoundedBox(new Float3(0.3f, 0.012f, 0.003f), 0.001f), new Float3(0.15f, 0.183f, -1.428f), new Color(0.12f, 0.16f, 0.4f, 1f));
        }
        float wx = jeep ? 0.95f : boat ? 0.7f : 0.85f, wz = jeep ? 1.25f : boat ? 1.2f : 1.05f, wy = jeep ? -0.2f : boat ? 0.1f : -0.15f, rad = jeep ? 0.5f : boat ? 0.2f : 0.42f;

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
            if (!boat)
            {
                // Spoked steel rims on both faces of the tyre (plain black discs read as toy wheels).
                var rm = Mats.New(g.Shader); rm.SetColor("_MainColor", jeep ? new Color(0.55f, 0.56f, 0.5f, 1f) : new Color(0.72f, 0.74f, 0.77f, 1f));
                if (!Application.IsHeadless) try { rm.SetTexture("_SurfaceTex", ProcTex.Surface(0.35f, 0.85f)); } catch { }
                float tw = jeep ? 0.38f : 0.32f;
                foreach (float face in new[] { 1f, -1f })
                {
                    var rim = new GameObject("Rim"); rim.SetParent(mesh, false);
                    rim.Transform.LocalEulerAngles = face > 0f ? Float3.Zero : new Float3(180f, 0f, 0f);
                    rim.Transform.LocalScale = new Float3(1f, 1.42f, 1f);
                    MeshRenderer rr = rim.AddComponent<MeshRenderer>();
                    rr.Mesh = PropMeshes.Rim(rad * 0.56f, tw * 0.72f, jeep ? 6 : 5);
                    rr.Material = rm;
                }
            }
            WheelCollider wc = wheel.AddComponent<WheelCollider>();
            wc.Radius = rad;
            wc.Width = jeep ? 0.38f : 0.32f;
            wc.SuspensionDistance = boat ? 0.12f : 0.3f;
            wc.SuspensionFrequency = 2.2f;
            wc.SuspensionDampingRatio = 0.7f;
            wc.ForwardFriction = boat ? 0.04f : 2.5f;
            wc.SidewaysFriction = boat ? 0.35f : 1.6f;
            _visuals[wc] = visual.Transform; // synced by SyncWheels (exactly the physics steer frame)
            g.World.Add(wheel);
            (front ? Front : Rear).Add(wc);
        }

        if (!boat) BuildUnderbody(g, jeep, wx, wy, wz, jeep ? 0.38f : 0.32f);
        if (!boat) AddSteeringWheel(g);
        Prop = g.Props.Adopt(Go, Body, mat, paint, PropKind.Limb, 0.5f);
    }

    /// <summary>Headlamp: lens dome, chrome bezel ring, and fresnel rings moulded into the glass.</summary>
    private void HeadLamp(SandboxGame g, Mesh lamp, float r, Float3 at, Float3 eul)
    {
        Detail(g, "Light", lamp, at, new Color(0.74f, 0.73f, 0.64f, 1f), eul);
        Detail(g, "LampReflector", Meshes.LatheZ([(0.0001f, 0.049f), (r * 0.18f, 0.0495f), (r * 0.2f, 0.051f), (0.0001f, 0.052f)], 20), at, new Color(0.9f, 0.88f, 0.7f, 1f), eul);
        Detail(g, "LampBezel", Meshes.LatheZ([(r * 0.93f, 0.028f), (r * 1.08f, 0.02f), (r * 1.13f, 0.03f), (r * 1.08f, 0.043f), (r * 0.9f, 0.046f)], 28), at, new Color(0.78f, 0.79f, 0.81f, 1f), eul);
        foreach (float k in new[] { 0.35f, 0.62f })
            Detail(g, "LampFresnel", Meshes.LatheZ([(r * k, 0.0505f - k * k * 0.0045f), (r * k + 0.004f, 0.051f - k * k * 0.0045f), (r * k + 0.008f, 0.0505f - k * k * 0.0045f)], 28), at, new Color(0.5f, 0.49f, 0.42f, 1f), eul);
    }

    private void JeepDetails(SandboxGame g, Color paint, Color dark)
    {
        Detail(g, "Hood", VehicleModels.JeepHood(), Float3.Zero, paint);
        foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -1.25f, 1.25f })
            Detail(g, "Fender", VehicleModels.Fender(x, z, 0.5f), Float3.Zero, paint * 0.85f);
        Detail(g, "Grille", VehicleModels.Grille(), Float3.Zero, paint * 0.9f); // painted grille panel so the black slots read (was black-on-black)
        // Stencilled five-point star on the hood (centre pentagon + five point triangles).
        {
            const float sy = 0.546f, sz = 1.12f, ro = 0.19f, ri = 0.075f;
            Float3 P(float a, float r) => new(MathF.Sin(a) * r, sy, sz + MathF.Cos(a) * r);
            var polys = new List<Float3[]>();
            var pent = new Float3[5];
            for (int k = 0; k < 5; k++) pent[k] = P(MathF.PI * 2f * k / 5f + MathF.PI / 5f, ri);
            polys.Add(pent);
            for (int k = 0; k < 5; k++)
            {
                float a = MathF.PI * 2f * k / 5f;
                polys.Add([P(a - MathF.PI / 5f, ri), P(a, ro), P(a + MathF.PI / 5f, ri)]);
            }
            Detail(g, "HoodStar", Meshes.Faces(polys.ToArray()), Float3.Zero, new Color(0.6f, 0.6f, 0.55f, 1f));
        }
        foreach (float x in new[] { -0.6f, 0.6f })
            HeadLamp(g, VehicleModels.Lamp(0.12f), 0.12f, new Float3(x, 0.4f, 1.72f), Float3.Zero);
        Detail(g, "Windshield", VehicleModels.Windshield(), new Float3(0f, 0.85f, 0.5f), new Color(0.16f, 0.23f, 0.29f, 1f), new Float3(-12f, 0f, 0f));
        // Glass sheen streaks, split-screen centre post and two parked wiper arms with rubber blades.
        foreach (float sx in new[] { -0.42f, 0.36f })
            Detail(g, "Sheen", Meshes.Faces([new Float3(sx - 0.16f, -0.24f, 0.023f), new Float3(sx - 0.05f, -0.24f, 0.023f), new Float3(sx + 0.13f, 0.24f, 0.023f), new Float3(sx + 0.02f, 0.24f, 0.023f)],
                [new Float3(sx - 0.02f, -0.24f, 0.023f), new Float3(sx + 0.02f, -0.24f, 0.023f), new Float3(sx + 0.2f, 0.24f, 0.023f), new Float3(sx + 0.16f, 0.24f, 0.023f)]), new Float3(0f, 0.85f, 0.5f), new Color(0.42f, 0.5f, 0.56f, 1f), new Float3(-12f, 0f, 0f));
        Detail(g, "WindPost", Meshes.RoundedBox(new Float3(0.04f, 0.62f, 0.05f), 0.012f), new Float3(0f, 0.85f, 0.5f), dark, new Float3(-12f, 0f, 0f));
        foreach (float wx in new[] { -0.55f, 0.15f })
        {
            Detail(g, "WiperArm", VehicleModels.Pipe(0.008f, Float3.UnitY, new Float3(wx, 0.57f, 0.58f), new Float3(wx + 0.38f, 0.585f, 0.585f)), Float3.Zero, dark);
            Detail(g, "WiperBlade", Meshes.RoundedBox(new Float3(0.36f, 0.018f, 0.014f), 0.005f), new Float3(wx + 0.2f, 0.6f, 0.588f), new Color(0.04f, 0.04f, 0.045f, 1f), new Float3(-12f, 0f, 0f));
        }
        Detail(g, "WindFrame", VehicleModels.BentPipe(0.03f, 0.08f, Float3.UnitZ, new Float3(-0.8f, -0.32f, 0f), new Float3(-0.8f, 0.32f, 0f), new Float3(0.8f, 0.32f, 0f), new Float3(0.8f, -0.32f, 0f)), new Float3(0f, 0.85f, 0.5f), dark, new Float3(-12f, 0f, 0f));
        // Dashboard: a painted steel panel under the screen with a padded top roll, chrome-bezel speedometer and
        // tachometer (ticks + red needles) ahead of the driver, small fuel/temp gauges, switches and a glovebox.
        Detail(g, "DashPanel", Meshes.RoundedBox(new Float3(1.62f, 0.2f, 0.05f), 0.015f), new Float3(0f, 0.5f, 0.46f), paint * 0.92f, new Float3(-12f, 0f, 0f));
        Detail(g, "DashRoll", Meshes.RoundedBox(new Float3(1.6f, 0.05f, 0.1f), 0.024f), new Float3(0f, 0.61f, 0.45f), new Color(0.08f, 0.08f, 0.085f, 1f));
        var bezel = new Color(0.62f, 0.63f, 0.65f, 1f); var face = new Color(0.05f, 0.05f, 0.055f, 1f); var tick = new Color(0.85f, 0.85f, 0.8f, 1f); var needle = new Color(0.85f, 0.12f, 0.06f, 1f);
        void Gauge(float gx, float gy, float r, float needleDeg)
        {
            // Gauges sit on the -Z face of the dash panel (tilted with it); built facing -Z.
            float tz = 0.432f - (gy - 0.5f) * 0.21f;
            Detail(g, "GaugeBezel", Meshes.LatheZ([(r * 0.86f, 0f), (r, 0f), (r, -0.012f), (r * 0.9f, -0.016f), (r * 0.86f, -0.012f)]), new Float3(gx, gy, tz), bezel, new Float3(-12f, 0f, 0f));
            Detail(g, "GaugeFace", Meshes.LatheZ([(0.0001f, 0f), (r * 0.87f, 0f), (r * 0.87f, -0.004f), (0.0001f, -0.004f)]), new Float3(gx, gy, tz), face, new Float3(-12f, 0f, 0f));
            for (int i = 0; i <= 8; i++)
            {
                float ang = (-135f + i * 33.75f) * MathF.PI / 180f;
                Detail(g, "GaugeTick", Meshes.RoundedBox(new Float3(r * 0.04f, r * (i % 2 == 0 ? 0.2f : 0.12f), 0.002f), 0.0008f), new Float3(gx + MathF.Sin(ang) * r * 0.66f, gy + MathF.Cos(ang) * r * 0.66f * 0.98f, tz - 0.005f), tick, new Float3(-12f, 0f, -ang * 180f / MathF.PI));
            }
            Detail(g, "GaugeNeedle", Meshes.RoundedBox(new Float3(r * 0.05f, r * 0.62f, 0.002f), 0.001f), new Float3(gx + MathF.Sin(needleDeg * MathF.PI / 180f) * r * 0.28f, gy + MathF.Cos(needleDeg * MathF.PI / 180f) * r * 0.28f, tz - 0.008f), needle, new Float3(-12f, 0f, -needleDeg));
            Detail(g, "GaugeHub", Meshes.Sphere(r * 0.08f), new Float3(gx, gy, tz - 0.008f), bezel);
        }
        Gauge(-0.52f, 0.5f, 0.06f, -60f);
        Gauge(-0.29f, 0.5f, 0.06f, -100f);
        Gauge(-0.13f, 0.52f, 0.032f, 40f);
        Gauge(-0.13f, 0.46f, 0.032f, -20f);
        for (int i = 0; i < 3; i++)
        {
            float sx = 0.02f + i * 0.06f;
            Detail(g, "Switch", Meshes.LatheZ([(0.0001f, 0f), (0.012f, 0f), (0.012f, -0.008f), (0.0001f, -0.008f)]), new Float3(sx, 0.46f, 0.445f), bezel, new Float3(-12f, 0f, 0f));
            Detail(g, "Toggle", VehicleModels.Pipe(0.0035f, Float3.UnitX, new Float3(sx, 0.46f, 0.44f), new Float3(sx, 0.475f, 0.415f)), Float3.Zero, bezel);
        }
        Detail(g, "Glovebox", Meshes.RoundedBox(new Float3(0.42f, 0.13f, 0.02f), 0.012f), new Float3(0.45f, 0.49f, 0.43f), paint * 0.86f, new Float3(-12f, 0f, 0f));
        Detail(g, "GloveLatch", Meshes.RoundedBox(new Float3(0.06f, 0.018f, 0.02f), 0.006f), new Float3(0.45f, 0.53f, 0.418f), bezel, new Float3(-12f, 0f, 0f));
        Detail(g, "GrabBar", VehicleModels.BentPipe(0.014f, 0.05f, Float3.UnitY, new Float3(0.2f, 0.6f, 0.42f), new Float3(0.24f, 0.63f, 0.36f), new Float3(0.66f, 0.63f, 0.36f), new Float3(0.7f, 0.6f, 0.42f)), Float3.Zero, dark);
        foreach (float x in new[] { -0.4f, 0.4f })
            Detail(g, "Seat", VehicleModels.Seat(0.24f), new Float3(x, 0.36f, -0.42f), dark);
        Detail(g, "RollBar", VehicleModels.BentPipe(0.04f, 0.2f, Float3.UnitZ, new Float3(-0.8f, 0.3f, -0.9f), new Float3(-0.8f, 1.3f, -0.9f), new Float3(0.8f, 1.3f, -0.9f), new Float3(0.8f, 0.3f, -0.9f)), Float3.Zero, dark);
        // Stencilled vehicle number on the rear panel, below the spare (the tub back was bare).
        Detail(g, "Stencil", Glyphs.Text("20431-S", 0.055f, -0.003f, 0f), new Float3(0f, 0.06f, -1.712f), new Color(0.78f, 0.78f, 0.7f, 1f));
        var spare = Detail(g, "Spare", Meshes.Tyre(0.42f, 0.25f), new Float3(0f, 0.55f, -1.82f), dark, new Float3(90f, 0f, 0f));
        // Spare wheel rim (was an empty tyre): dished steel disc with a hub cap and five lug nuts, facing rearward.
        // Domed steel wheel (the old dished profile read as a pale hollow disc): flat outer disc, raised bead ring, a hub
        // shoulder bulging rearward and a hub cap, so the centre reads solid from behind.
        Detail(g, "SpareRim", Meshes.LatheZ([(0.0001f, -0.08f), (0.27f, -0.08f), (0.27f, 0.035f), (0.255f, 0.055f), (0.235f, 0.045f), (0.215f, 0.06f), (0.19f, 0.062f), (0.125f, 0.058f), (0.095f, 0.07f), (0.075f, 0.095f), (0.0001f, 0.1f)], 32), new Float3(0f, 0.55f, -1.86f), new Color(0.46f, 0.48f, 0.4f, 1f), new Float3(0f, 180f, 0f));
        Detail(g, "SpareCap", VehicleModels.Hub(0.1f), new Float3(0f, 0.55f, -1.94f), new Color(0.56f, 0.57f, 0.56f, 1f), new Float3(0f, 180f, 0f));
        for (int i = 0; i < 5; i++) { float a = i * MathF.Tau / 5f; Detail(g, "SpareLug", Meshes.Sphere(0.014f), new Float3(MathF.Cos(a) * 0.15f, 0.55f + MathF.Sin(a) * 0.15f, -1.92f), new Color(0.3f, 0.3f, 0.32f, 1f)); }
        // Classic seven-slot grille, side mirrors on stalks, tail lamps, hood latches and a jerrycan on the tailgate.
        for (int i = 0; i < 7; i++)
            Detail(g, "GrilleSlot", Meshes.RoundedBox(new Float3(0.07f, 0.3f, 0.02f), 0.03f), new Float3(-0.36f + i * 0.12f, 0.3f, 1.745f), new Color(0.03f, 0.03f, 0.03f, 1f));
        foreach (float x in new[] { -0.92f, 0.92f })
        {
            Detail(g, "MirrorArm", VehicleModels.Pipe(0.012f, Float3.UnitY, new Float3(x * 0.9f, 0.5f, 0.55f), new Float3(x * 1.05f, 0.75f, 0.5f)), Float3.Zero, dark);
            Detail(g, "Mirror", Meshes.Lathe("mirror", [(0.0001f, -0.015f), (0.07f, -0.015f), (0.075f, 0f), (0.07f, 0.015f), (0.0001f, 0.015f)], 20), new Float3(x * 1.06f, 0.8f, 0.5f), dark, new Float3(90f, 0f, 0f));
            Detail(g, "TailLamp", VehicleModels.Lamp(0.06f), new Float3(x * 0.85f, 0.25f, -1.7f), new Color(0.62f, 0.06f, 0.05f, 1f), new Float3(0f, 180f, 0f));
            // Black rubber bezel and chrome trim ring so the lamp reads as a flush lens, not a red ball stuck on.
            Detail(g, "TailBezel", Meshes.LatheZ([(0.058f, 0.012f), (0.074f, 0.008f), (0.078f, -0.004f), (0.075f, -0.02f), (0.058f, -0.02f)], 28), new Float3(x * 0.85f, 0.25f, -1.7f), new Color(0.05f, 0.05f, 0.055f, 1f), new Float3(0f, 180f, 0f));
            Detail(g, "TailTrim", Meshes.LatheZ([(0.06f, 0.016f), (0.064f, 0.016f), (0.064f, 0.008f), (0.06f, 0.008f)], 28), new Float3(x * 0.85f, 0.25f, -1.7f), new Color(0.7f, 0.71f, 0.73f, 1f), new Float3(0f, 180f, 0f));
            Detail(g, "Latch", Meshes.RoundedBox(new Float3(0.04f, 0.1f, 0.06f), 0.015f), new Float3(x * 0.84f, 0.48f, 1.2f), dark);
        }
        Detail(g, "Jerrycan", Meshes.RoundedBox(new Float3(0.16f, 0.45f, 0.32f), 0.04f), new Float3(0.62f, 0.55f, -1.82f), paint * 0.8f);
        // Jerrycan pressings (X ribs), triple handle; door cut-out trim, steps, hinge pegs, fuel filler.
        foreach (float sg in new[] { -1f, 1f })
            Detail(g, "CanRib", Meshes.RoundedBox(new Float3(0.02f, 0.5f, 0.02f), 0.008f), new Float3(0.62f, 0.55f, -1.98f), paint * 0.7f, new Float3(0f, 0f, sg * 30f));
        Detail(g, "CanHandle", VehicleModels.BentPipe(0.012f, 0.03f, Float3.UnitX, new Float3(0.62f, 0.775f, -1.74f), new Float3(0.62f, 0.83f, -1.76f), new Float3(0.62f, 0.83f, -1.88f), new Float3(0.62f, 0.775f, -1.9f)), Float3.Zero, paint * 0.7f);
        foreach (float x in new[] { -1f, 1f })
        {
            Detail(g, "DoorTrim", VehicleModels.BentPipe(0.02f, 0.12f, Float3.UnitX, new Float3(x * 0.905f, 0.28f, 0.2f), new Float3(x * 0.915f, 0.02f, 0.05f), new Float3(x * 0.915f, -0.02f, -0.75f), new Float3(x * 0.905f, 0.28f, -0.9f)), Float3.Zero, new Color(0.06f, 0.06f, 0.06f, 1f));
            Detail(g, "Step", Meshes.RoundedBox(new Float3(0.14f, 0.025f, 0.32f), 0.008f), new Float3(x * 0.98f, -0.24f, -0.35f), dark);
            foreach (float y in new[] { 0.36f, 0.12f })
                Detail(g, "Hinge", Meshes.RoundedBox(new Float3(0.03f, 0.06f, 0.1f), 0.01f), new Float3(x * 0.92f, y, 0.3f), dark);
        }
        Detail(g, "FuelCap", Meshes.Lathe("jfuel", [(0.0001f, 0.02f), (0.05f, 0.02f), (0.055f, 0f), (0.05f, -0.02f), (0.0001f, -0.02f)], 20), new Float3(-0.92f, 0.12f, -1.25f), new Color(0.6f, 0.6f, 0.62f, 1f), new Float3(0f, 0f, 90f));
        // Body panels: hood shut lines and a raised centre spine, hood-side louvres, a pressed character line along the
        // rear quarters, rivet rows on the tub rim, and a tailgate shut line with its chain stays.
        var seam = new Color(0.07f, 0.075f, 0.06f, 1f);
        foreach (float x in new[] { -1f, 1f })
        {
            Detail(g, "HoodSeam", VehicleModels.Pipe(0.0055f, Float3.UnitY, new Float3(x * 0.62f, 0.531f, 0.62f), new Float3(x * 0.62f, 0.529f, 1.62f)), Float3.Zero, seam);
            for (int i = 0; i < 4; i++)
                Detail(g, "Louvre", Meshes.RoundedBox(new Float3(0.02f, 0.035f, 0.11f), 0.008f), new Float3(x * 0.815f, 0.46f, 0.95f + i * 0.13f), seam, new Float3(0f, 0f, x * -18f));
            Detail(g, "Character", VehicleModels.Pipe(0.011f, Float3.UnitY, new Float3(x * 0.9f, 0.13f, -1.62f), new Float3(x * 0.905f, 0.13f, -0.98f)), Float3.Zero, paint * 1.08f);
            Detail(g, "Rivets", VehicleModels.Rivets(new Float3(x * 0.885f, 0.262f, -1.6f), new Float3(x * 0.885f, 0.262f, -0.98f), 7, 0.009f), Float3.Zero, paint * 0.95f);
            Detail(g, "Rivets", VehicleModels.Rivets(new Float3(x * 0.885f, 0.262f, 0.25f), new Float3(x * 0.885f, 0.262f, 0.45f), 3, 0.009f), Float3.Zero, paint * 0.95f);
            Detail(g, "TailStay", VehicleModels.Pipe(0.005f, Float3.UnitX, new Float3(x * 0.8f, 0.27f, -1.69f), new Float3(x * 0.84f, 0.12f, -1.72f)), Float3.Zero, new Color(0.45f, 0.46f, 0.48f, 1f));
        }
        Detail(g, "HoodSpine", Meshes.RoundedBox(new Float3(0.14f, 0.02f, 1.0f), 0.009f), new Float3(0f, 0.535f, 1.1f), paint * 1.05f);
        Detail(g, "TailSeam", VehicleModels.BentPipe(0.005f, 0.05f, Float3.UnitY, new Float3(-0.74f, -0.18f, -1.695f), new Float3(-0.74f, 0.2f, -1.695f), new Float3(0.74f, 0.2f, -1.695f), new Float3(0.74f, -0.18f, -1.695f)), Float3.Zero, seam);
        // Flat steel channel bumper in body paint (was a round black pipe) with angled end returns, carriage bolts and
        // a pair of tow shackles.
        // Bumper in dark satin chassis paint so it separates from the olive body (body paint blended into the grille/fenders).
        var bumperPaint = new Color(0.075f, 0.08f, 0.07f, 1f);
        Detail(g, "Bumper", Meshes.RoundedBox(new Float3(1.8f, 0.14f, 0.08f), 0.012f), new Float3(0f, 0.05f, 1.8f), bumperPaint);
        foreach (float x in new[] { -1f, 1f })
        {
            Detail(g, "BumperEnd", Meshes.RoundedBox(new Float3(0.16f, 0.14f, 0.08f), 0.012f), new Float3(x * 0.95f, 0.05f, 1.75f), bumperPaint, new Float3(0f, x * -35f, 0f));
            foreach (float bx in new[] { 0.62f, 0.78f })
                Detail(g, "BumperBolt", Meshes.LatheZ([(0.0001f, 0.012f), (0.013f, 0.009f), (0.016f, 0f), (0.0001f, 0f)], 12), new Float3(x * bx, 0.05f, 1.84f), new Color(0.32f, 0.33f, 0.31f, 1f));
            Detail(g, "Shackle", VehicleModels.BentPipe(0.012f, 0.04f, Float3.UnitX, new Float3(x * 0.42f - 0.04f, 0.0f, 1.84f), new Float3(x * 0.42f - 0.04f, -0.02f, 1.9f), new Float3(x * 0.42f + 0.04f, -0.02f, 1.9f), new Float3(x * 0.42f + 0.04f, 0.0f, 1.84f)), Float3.Zero, new Color(0.62f, 0.12f, 0.08f, 1f));
        }
    }

    private void BoatDetails(SandboxGame g, Color dark)
    {
        var white = new Color(0.66f, 0.67f, 0.68f, 1f); // 0.86 clipped to a glowing outline under full sun
        foreach (float x in new[] { -0.9f, 0.9f })
            Detail(g, "Rail", VehicleModels.BentPipe(0.04f, 0.2f, Float3.UnitY, new Float3(x * 0.98f, 0.2f, -1.78f), new Float3(x, 0.215f, -1.4f), new Float3(x, 0.215f, 1.1f), new Float3(x * 0.88f, 0.27f, 1.7f), new Float3(x * 0.62f, 0.33f, 2.06f)), Float3.Zero, white);
        // Deck: aluminium diamond treadplate over the hull top, a hinged bow hatch, mooring cleats and a dark rub strake
        // along the gunwale, so the deck reads as a working boat instead of a flat blue slab.
        var alu = new Color(0.42f, 0.44f, 0.46f, 1f);
        Detail(g, "Deck", VehicleModels.TreadPlate(1.62f, 2.7f, 0.176f), new Float3(0f, 0f, -0.25f), alu);
        Detail(g, "Hatch", Meshes.RoundedBox(new Float3(0.9f, 0.02f, 0.42f), 0.01f), new Float3(0f, 0.2f, 1.42f), alu * 0.92f, new Float3(-6f, 0f, 0f));
        Detail(g, "HatchSeam", Meshes.RoundedBox(new Float3(0.94f, 0.012f, 0.46f), 0.006f), new Float3(0f, 0.192f, 1.42f), new Color(0.12f, 0.13f, 0.14f, 1f), new Float3(-6f, 0f, 0f));
        foreach (float x in new[] { -0.3f, 0.3f })
            Detail(g, "Hinge", VehicleModels.Pipe(0.012f, Float3.UnitY, new Float3(x - 0.06f, 0f, 0f), new Float3(x + 0.06f, 0f, 0f)), new Float3(0f, 0.2f, 1.2f), new Color(0.4f, 0.41f, 0.43f, 1f));
        Detail(g, "Handle", VehicleModels.BentPipe(0.008f, 0.02f, Float3.UnitY, new Float3(-0.07f, 0f, 0f), new Float3(-0.06f, 0.025f, 0f), new Float3(0.06f, 0.025f, 0f), new Float3(0.07f, 0f, 0f)), new Float3(0f, 0.235f, 1.6f), new Color(0.4f, 0.41f, 0.43f, 1f));
        foreach (var (cx, cz) in new[] { (-0.72f, 1.05f), (0.72f, 1.05f), (-0.78f, -1.62f), (0.78f, -1.62f) })
            Detail(g, "Cleat", VehicleModels.Cleat(), new Float3(cx, 0.18f, cz), new Color(0.75f, 0.76f, 0.78f, 1f), new Float3(0f, 90f, 0f));
        foreach (float x in new[] { -1f, 1f })
            Detail(g, "Strake", VehicleModels.Pipe(0.022f, Float3.UnitY, new Float3(x * 0.95f, 0.1f, -1.78f), new Float3(x * 0.965f, 0.1f, -1.4f), new Float3(x * 0.965f, 0.1f, 1.1f), new Float3(x * 0.86f, 0.17f, 1.7f), new Float3(x * 0.6f, 0.26f, 2.1f)), Float3.Zero, new Color(0.1f, 0.11f, 0.12f, 1f));
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
            child.Transform.LocalEulerAngles = new Float3(0f, 24f, 0f); // blade pitch: flat paddles read edge-on as thin sticks
            MeshRenderer r = child.AddComponent<MeshRenderer>();
            r.Mesh = VehicleModels.Blade();
            var m = Mats.New(g.Shader); m.SetColor("_MainColor", new Color(0.7f, 0.55f, 0.3f, 1f)); r.Material = m;
            // Painted safety tip on each blade.
            var tip = new GameObject("BladeTip");
            tip.Transform.Parent = child.Transform;
            tip.Transform.LocalPosition = new Float3(0f, 0.63f, 0f);
            MeshRenderer tr = tip.AddComponent<MeshRenderer>();
            tr.Mesh = Meshes.RoundedBox(new Float3(0.122f, 0.1f, 0.019f), 0.008f);
            var tm = Mats.New(g.Shader); tm.SetColor("_MainColor", new Color(0.85f, 0.82f, 0.2f, 1f)); tr.Material = tm;
            g.World.Add(blade); g.World.Add(child); g.World.Add(tip);
        }
        // Spinner cone over the hub, pointing aft into the guard.
        Detail(g, "Spinner", Meshes.LatheZ([(0.0001f, 0.12f), (0.04f, 0.1f), (0.075f, 0.05f), (0.09f, 0f), (0.0001f, 0f)], 24), new Float3(0f, 1.25f, -1.42f), new Color(0.75f, 0.2f, 0.15f, 1f), new Float3(0f, 180f, 0f));
        // Aircraft engine on the mast base: finned cylinder bank, exhaust stacks, and guard spokes behind the fan.
        Detail(g, "Engine", VehicleModels.BuggyEngine(), new Float3(0f, 0.38f, -1.25f), new Color(0.3f, 0.31f, 0.33f, 1f));
        foreach (float x in new[] { -0.18f, 0.18f })
            Detail(g, "Stack", VehicleModels.BentPipe(0.03f, 0.06f, Float3.UnitX, new Float3(x, 0.5f, -1.2f), new Float3(x * 1.4f, 0.75f, -1.1f), new Float3(x * 1.4f, 0.95f, -1.12f)), Float3.Zero, new Color(0.5f, 0.5f, 0.52f, 1f));
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.Tau / 6f;
            Detail(g, "GuardSpoke", VehicleModels.Pipe(0.01f, Float3.UnitZ, new Float3(0f, 0f, 0f), new Float3(MathF.Cos(a) * 0.84f, MathF.Sin(a) * 0.84f, 0f)), new Float3(0f, 1.25f, -1.6f), new Color(0.3f, 0.3f, 0.32f, 1f));
        }
        // Fan guard: denser spokes plus two inner hoops, so it reads as a wire cage rather than a lone ring.
        for (int i = 0; i < 12; i++)
        {
            float a = (i + 0.5f) * MathF.Tau / 12f;
            Detail(g, "GuardSpoke", VehicleModels.Pipe(0.008f, Float3.UnitZ, new Float3(MathF.Cos(a) * 0.2f, MathF.Sin(a) * 0.2f, 0f), new Float3(MathF.Cos(a) * 0.84f, MathF.Sin(a) * 0.84f, 0f)), new Float3(0f, 1.25f, -1.6f), new Color(0.3f, 0.3f, 0.32f, 1f));
        }
        Detail(g, "Hoop", VehicleModels.Ring(0.58f, 0.012f), new Float3(0f, 1.25f, -1.6f), new Color(0.3f, 0.3f, 0.32f, 1f));
        Detail(g, "Hoop", VehicleModels.Ring(0.32f, 0.012f), new Float3(0f, 1.25f, -1.6f), new Color(0.3f, 0.3f, 0.32f, 1f));
        // Three linked rudder blades in the prop wash, with a tie bar.
        foreach (float x in new[] { -0.42f, 0f, 0.42f })
            Detail(g, "Rudder", Meshes.Sweep([new Meshes.Sec(new Float3(0f, -0.45f, 0.02f), 0.01f, 0.12f, 2.5f), new Meshes.Sec(new Float3(0f, 0f, 0f), 0.012f, 0.14f, 2.5f), new Meshes.Sec(new Float3(0f, 0.45f, -0.03f), 0.009f, 0.11f, 2.5f)], Float3.UnitX, 14, 0.5f), new Float3(x, 1.2f, -1.82f), dark);
        Detail(g, "TieBar", VehicleModels.Pipe(0.012f, Float3.UnitY, new Float3(-0.46f, 0f, 0f), new Float3(0.46f, 0f, 0f)), new Float3(0f, 0.8f, -1.66f), dark);
        // Rudder frame: top and bottom rails carry the blade pivots, braced forward to the guard ring and down to the
        // transom. (The blades used to hang in mid-air behind the cage.)
        foreach (float y in new[] { 1.68f, 0.72f })
        {
            Detail(g, "RudderRail", VehicleModels.Pipe(0.016f, Float3.UnitY, new Float3(-0.52f, y, -1.72f), new Float3(0.52f, y, -1.72f)), Float3.Zero, dark);
            float rx = MathF.Sqrt(0.84f * 0.84f - (y - 1.25f) * (y - 1.25f)) - 0.02f;
            foreach (float x in new[] { -1f, 1f })
                Detail(g, "RudderBrace", VehicleModels.Pipe(0.014f, Float3.UnitY, new Float3(x * 0.52f, y, -1.72f), new Float3(x * rx, y, -1.6f)), Float3.Zero, dark);
            foreach (float x in new[] { -0.42f, 0f, 0.42f })
                Detail(g, "RudderPivot", Meshes.Sphere(0.022f), new Float3(x, y, -1.72f), new Color(0.5f, 0.5f, 0.52f, 1f));
        }
        foreach (float x in new[] { -1f, 1f })
            Detail(g, "RudderPost", VehicleModels.Pipe(0.02f, Float3.UnitZ, new Float3(x * 0.52f, 0.72f, -1.72f), new Float3(x * 0.56f, 0.18f, -1.7f)), Float3.Zero, dark);
        // Hull: grey rub rail along the sheer, a chine strake, non-slip deck pad, bow cleat and navigation lights.
        var rub = new Color(0.22f, 0.23f, 0.25f, 1f);
        foreach (float x in new[] { -1f, 1f })
        {
            Detail(g, "RubRail", VehicleModels.BentPipe(0.03f, 0.25f, Float3.UnitY, new Float3(x * 0.9f, 0.13f, -1.8f), new Float3(x * 0.955f, 0.14f, -1.4f), new Float3(x * 0.955f, 0.14f, 1.1f), new Float3(x * 0.85f, 0.2f, 1.7f), new Float3(x * 0.6f, 0.3f, 2.1f), new Float3(x * 0.3f, 0.33f, 2.27f)), Float3.Zero, rub);
            Detail(g, "NavLight", Meshes.Sphere(0.035f), new Float3(x * 0.42f, 0.33f, 2.05f), x < 0 ? new Color(0.9f, 0.1f, 0.08f, 1f) : new Color(0.1f, 0.8f, 0.2f, 1f));
        }
        Detail(g, "DeckPad", Meshes.RoundedBox(new Float3(1.3f, 0.012f, 1.6f), 0.006f), new Float3(0f, 0.175f, 0.55f), new Color(0.32f, 0.34f, 0.36f, 1f));
        Detail(g, "Cleat", Meshes.Sweep([new Meshes.Sec(new Float3(-0.08f, 0f, 0f), 0.012f, 0.012f), new Meshes.Sec(new Float3(0f, 0.035f, 0f), 0.016f, 0.012f), new Meshes.Sec(new Float3(0.08f, 0f, 0f), 0.012f, 0.012f)], Float3.UnitZ, 10, 0.8f), new Float3(0f, 0.3f, 2.12f), new Color(0.7f, 0.71f, 0.72f, 1f));
    }

    public float Thrust { get; private set; }
    /// <summary>Driver's hip point in vehicle-local space (x right, y up, z forward).</summary>
    public Float3 DriverHips => Kind switch { "jeep" => new Float3(-0.4f, 0.48f, -0.5f), "airboat" => new Float3(0f, 0.4f, -0.45f), _ => new Float3(0f, 0.34f, -0.45f) };
    private Transform? _wheel;
    private Float3 WheelCentre => Kind switch { "jeep" => new Float3(-0.4f, 0.9f, 0.08f), "airboat" => new Float3(0f, 0.8f, 0.08f), _ => new Float3(0f, 0.74f, 0.08f) };
    private const float WheelTilt = 55f, WheelR = 0.19f;
    /// <summary>World position of the left (side -1) / right (+1) hand grip on the steering wheel, turning with it.</summary>
    public Float3 WheelGrip(float side)
    {
        Transform t = Go.Transform;
        float a = -_steer * 1.6f; // radians of wheel turn
        float tilt = WheelTilt * MathF.PI / 180f;
        // Wheel plane axes in car space: x = right, v = up tilted back toward the driver.
        Float3 vx = new(1f, 0f, 0f), vy = new(0f, MathF.Sin(tilt), MathF.Cos(tilt));
        Float3 radial = (vx * MathF.Cos(a) - vy * MathF.Sin(a)) * side;
        // Fist centre sits ~3 cm inside the rim (the curled fingers wrap it), so the top hand no longer floats above the ring.
        Float3 local = WheelCentre + radial * (WheelR - 0.03f);
        return t.Position + t.Right * local.X + t.Up * local.Y + t.Forward * local.Z;
    }
    private void AddSteeringWheel(SandboxGame g)
    {
        var col = new GameObject("SteeringColumn"); col.Transform.Parent = Go.Transform;
        col.Transform.LocalPosition = WheelCentre; col.Transform.LocalEulerAngles = new Float3(-WheelTilt, 0f, 0f);
        g.World.Add(col);
        var spin = new GameObject("SteeringWheel"); spin.Transform.Parent = col.Transform; spin.Transform.LocalPosition = Float3.Zero;
        g.World.Add(spin);
        _wheel = spin.Transform;
        void Part(Mesh m, Color c, Float3 rot)
        {
            var d = new GameObject("WheelPart"); d.Transform.Parent = spin.Transform; d.Transform.LocalPosition = Float3.Zero; d.Transform.LocalEulerAngles = rot;
            MeshRenderer r = d.AddComponent<MeshRenderer>(); r.Mesh = m; var mt = Mats.New(g.Shader); try { mt.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { } mt.SetColor("_MainColor", c); r.Material = mt;
            g.World.Add(d);
        }
        var dark = new Color(0.08f, 0.08f, 0.09f, 1f);
        Part(VehicleModels.Ring(WheelR, 0.016f), dark, new Float3(90f, 0f, 0f)); // ring in the column's XZ plane
        Part(Meshes.Lathe("whub", [(0.0001f, -0.03f), (0.045f, -0.03f), (0.05f, 0f), (0.035f, 0.025f), (0.0001f, 0.03f)], 20), new Color(0.2f, 0.2f, 0.22f, 1f), Float3.Zero);
        Part(VehicleModels.Pipe(0.011f, Float3.UnitY, new Float3(-WheelR, 0f, 0f), new Float3(WheelR, 0f, 0f)), dark, Float3.Zero);
        Part(VehicleModels.Pipe(0.011f, Float3.UnitY, new Float3(0f, 0f, 0f), new Float3(0f, 0f, -WheelR)), dark, Float3.Zero);
        Part(VehicleModels.Pipe(0.018f, Float3.UnitX, new Float3(0f, -0.03f, 0f), new Float3(0f, -0.38f, 0f)), dark, Float3.Zero);
    }
    private readonly Dictionary<WheelCollider, Transform> _visuals = new();
    /// <summary>Suspension parts that follow the wheels: (part, wheel or null, end-point function in world space).
    /// Struts are unit-length meshes along +Z, stretched between their two ends every frame.</summary>
    private readonly List<(Transform T, Func<(Float3 A, Float3 B)> Ends)> _struts = new();
    private readonly List<(Transform T, WheelCollider W, float Side)> _hubs = new();

    private static Mesh ShockMesh() => Meshes.Merge(
        Meshes.LatheZ([(0.0001f, 0f), (0.022f, 0f), (0.022f, 0.06f), (0.011f, 0.07f), (0.011f, 0.55f), (0.0001f, 0.55f)]),
        Meshes.LatheZ([(0.0001f, 0.42f), (0.034f, 0.42f), (0.036f, 0.45f), (0.036f, 0.97f), (0.024f, 1f), (0.0001f, 1f)]),
        Helix(0.052f, 0.0085f, 0.12f, 0.9f, 6.5f),
        Meshes.LatheZ([(0.0001f, 0.1f), (0.062f, 0.1f), (0.062f, 0.12f), (0.0001f, 0.12f)]),
        Meshes.LatheZ([(0.0001f, 0.9f), (0.062f, 0.9f), (0.062f, 0.92f), (0.0001f, 0.92f)]));

    private static Mesh Helix(float r, float wire, float z0, float z1, float turns)
    {
        var secs = new List<Meshes.Sec>();
        int n = (int)(turns * 14);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, a = t * turns * MathF.Tau;
            secs.Add(new Meshes.Sec(new Float3(MathF.Cos(a) * r, MathF.Sin(a) * r, z0 + (z1 - z0) * t), wire, wire, 2f, Float3.UnitZ));
        }
        return Meshes.Sweep(secs, Float3.UnitZ, 8, 0.9f);
    }

    /// <summary>What you see under the body: frame rails, skid plate, exhaust and fuel tank (static), plus coil-over
    /// shocks, control arms / a live axle and brake drums that follow each wheel's suspension travel.</summary>
    private void BuildUnderbody(SandboxGame g, bool jeep, float wx, float wy, float wz, float tw)
    {
        var steel = new Color(0.12f, 0.12f, 0.13f, 1f); var coil = jeep ? new Color(0.62f, 0.12f, 0.08f, 1f) : new Color(0.95f, 0.75f, 0.1f, 1f);
        var chrome = new Color(0.6f, 0.61f, 0.63f, 1f);
        float rail = jeep ? -0.27f : -0.2f;
        foreach (float x in new[] { -1f, 1f })
            Detail(g, "FrameRail", Meshes.RoundedBox(new Float3(0.08f, jeep ? 0.14f : 0.07f, wz * 2f + 0.7f), 0.012f), new Float3(x * (jeep ? 0.42f : 0.32f), rail, 0f), steel);
        for (int i = -1; i <= 1; i++)
            Detail(g, "CrossMember", Meshes.RoundedBox(new Float3(jeep ? 0.84f : 0.64f, 0.06f, 0.07f), 0.01f), new Float3(0f, rail, i * wz * 0.62f), steel);
        Detail(g, "SkidPlate", Meshes.RoundedBox(new Float3(jeep ? 0.7f : 0.5f, 0.02f, 0.6f), 0.008f), new Float3(0f, rail - 0.06f, wz * 0.55f), new Color(0.3f, 0.31f, 0.32f, 1f));
        Detail(g, "Exhaust", VehicleModels.BentPipe(0.028f, 0.2f, Float3.UnitX, new Float3(0.18f, rail - 0.02f, wz * 0.6f), new Float3(0.22f, rail - 0.06f, 0.2f), new Float3(0.22f, rail - 0.06f, -wz + 0.2f), new Float3(0.3f, rail - 0.03f, -wz - 0.42f)), Float3.Zero, steel);
        Detail(g, "Muffler", Meshes.LatheZ([(0.0001f, -0.2f), (0.07f, -0.2f), (0.08f, -0.17f), (0.08f, 0.17f), (0.07f, 0.2f), (0.0001f, 0.2f)]), new Float3(0.22f, rail - 0.07f, -wz * 0.35f), steel);
        Detail(g, "ExhaustTip", Meshes.LatheZ([(0.024f, 0f), (0.034f, 0f), (0.034f, 0.08f), (0.024f, 0.08f)]), new Float3(0.3f, rail - 0.03f, -wz - 0.5f), chrome);
        Detail(g, "FuelTank", Meshes.RoundedBox(new Float3(0.5f, 0.16f, 0.36f), 0.05f), new Float3(-0.12f, rail - 0.02f, -wz * 0.55f), steel);

        Mesh shock = ShockMesh();
        Mesh arm = Meshes.LatheZ([(0.0001f, 0f), (0.022f, 0f), (0.018f, 0.04f), (0.018f, 0.96f), (0.022f, 1f), (0.0001f, 1f)]);
        Mesh axle = Meshes.LatheZ([(0.0001f, 0f), (0.035f, 0f), (0.035f, 1f), (0.0001f, 1f)]);
        Mesh drum = Meshes.LatheZ([(0.0001f, 0f), (0.13f, 0f), (0.14f, 0.015f), (0.14f, 0.07f), (0.06f, 0.08f), (0.0001f, 0.08f)]);
        GameObject Part(string name, Mesh m, Color c)
        {
            var go = Detail(g, name, m, Float3.Zero, c);
            if (!Application.IsHeadless) try { go.GetComponent<MeshRenderer>().Material?.SetTexture("_SurfaceTex", ProcTex.Surface(0.4f, 0.7f)); } catch { }
            return go;
        }
        Float3 Body(Float3 p) => Go.Transform.TransformPoint(p);
        Float3 Inner(WheelCollider w, float side, float extra) => w.GetWheelCenter() - Go.Transform.Right * side * (tw / 2f + extra);
        foreach (WheelCollider w in Front.Concat(Rear))
        {
            float side = MathF.Sign(w.Transform.LocalPosition.X), z = w.Transform.LocalPosition.Z;
            var wc = w;
            _struts.Add((Part("Shock", shock, coil).Transform, () => (Inner(wc, side, 0.1f), Body(new Float3(side * (wx - (jeep ? 0.3f : 0.42f)), wy + (jeep ? 0.5f : 0.3f), z - (jeep ? 0.05f : 0.12f))))));
            var hub = Part("BrakeDrum", drum, steel);
            _hubs.Add((hub.Transform, wc, side));
            if (!jeep)
                foreach (float dz in new[] { -0.2f, 0.2f })
                {
                    float dzc = dz;
                    _struts.Add((Part("ControlArm", arm, steel).Transform, () => (Body(new Float3(side * 0.3f, wy - 0.02f, z + dzc)), Inner(wc, side, 0.06f) - Go.Transform.Up * 0.06f)));
                }
        }
        if (jeep)
        {
            // Live axles: a beam between the two wheel hubs of each axle, with a differential pumpkin and leaf springs.
            foreach (var pair in new[] { Front, Rear })
            {
                if (pair.Count < 2) continue;
                WheelCollider l = pair.First(w => w.Transform.LocalPosition.X < 0f), r = pair.First(w => w.Transform.LocalPosition.X > 0f);
                _struts.Add((Part("Axle", axle, steel).Transform, () => (Inner(l, -1f, 0.04f), Inner(r, 1f, 0.04f))));
                var diff = Part("Differential", Meshes.LatheZ([(0.0001f, -0.12f), (0.08f, -0.12f), (0.13f, -0.05f), (0.13f, 0.04f), (0.09f, 0.1f), (0.0001f, 0.11f)]), steel);
                _struts.Add((diff.Transform, () => { Float3 m = (Inner(l, -1f, 0.04f) + Inner(r, 1f, 0.04f)) / 2f; return (m - Go.Transform.Forward * 0.001f, m + Go.Transform.Forward * (pair == Front ? -1f : 1f) * 0.001f); }));
                foreach (var (w, sd) in new[] { (l, -1f), (r, 1f) })
                {
                    float z = w.Transform.LocalPosition.Z; var ww = w; float sdd = sd;
                    _struts.Add((Part("LeafSpring", Meshes.LatheZ([(0.0001f, 0f), (0.03f, 0f), (0.03f, 1f), (0.0001f, 1f)]), steel).Transform, () => (Body(new Float3(sdd * 0.42f, rail - 0.06f, z + 0.45f)), Inner(ww, sdd, 0.12f))));
                    _struts.Add((Part("LeafSpring", Meshes.LatheZ([(0.0001f, 0f), (0.03f, 0f), (0.03f, 1f), (0.0001f, 1f)]), steel).Transform, () => (Body(new Float3(sdd * 0.42f, rail - 0.06f, z - 0.45f)), Inner(ww, sdd, 0.12f))));
                }
            }
        }
    }

    private void SyncUnderbody()
    {
        Float3 up = Go.Transform.Up, fw = Go.Transform.Forward;
        foreach (var (t, ends) in _struts)
        {
            if (!t.GameObject.IsValid()) continue;
            var (a, b) = ends();
            Float3 d = b - a; float len = Float3.Length(d);
            if (len < 1e-5f) continue;
            Float3 dir = d / len;
            bool diffPart = len < 0.01f;
            t.Position = a;
            t.Rotation = Quaternion.LookRotation(dir, MathF.Abs(Float3.Dot(dir, up)) > 0.9f ? fw : up);
            t.LocalScale = diffPart ? Float3.One : new Float3(1f, 1f, len);
        }
        foreach (var (t, w, side) in _hubs)
        {
            if (!t.GameObject.IsValid() || !_visuals.TryGetValue(w, out Transform? vis)) continue;
            Float3 axleDir = Float3.Normalize(vis.Right) * side; // the visual spins about its X axis, so X stays the axle
            t.Position = w.GetWheelCenter() - axleDir * 0.17f;
            t.Rotation = Quaternion.LookRotation(axleDir, up);
        }
    }
    /// <summary>Steering-wheel input in -1..1 after smoothing (drives the steering-wheel mesh and the driver's arms).</summary>
    public float SteerInput => _steer;

    /// <summary>Places each wheel visual at the suspension-resolved centre, steered about the mount's up axis with the
    /// same convention the physics uses, and spun about the axle. Done in game code so the visuals can't drift or
    /// steer the wrong way relative to the contact frame.</summary>
    public void SyncWheels()
    {
        if (_wheel is not null) _wheel.LocalEulerAngles = new Float3(0f, _steer * 1.6f * 180f / MathF.PI, 0f);
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
        SyncUnderbody();
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
    /// <summary>V toggles a first-person driver's-eye camera (legs, hands and steering wheel visible).</summary>
    public bool FirstPerson;
    public void SetOrbit(float yaw, float pitch) { _orbitYaw = yaw; _orbitPitch = pitch; }
    private float _orbitYaw, _orbitPitch = 12f;
    public float OrbitDist = 6.5f;
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
        foreach (MeshRenderer gr in b.Go.GetComponentsInChildren<MeshRenderer>())
            if (gr.GameObject.Name is "Windshield" or "Sheen") gr.Enabled = true; // shown again once nobody looks from the seat
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
            if (Binds.Down("noclip")) { FirstPerson = !FirstPerson; _orbitYaw = 0f; _orbitPitch = FirstPerson ? 0f : 12f; }
            Float2 md = Input.MouseDelta * 0.12f * game.Settings.MouseSensitivity;
            _orbitYaw += md.X;
            _orbitPitch = FirstPerson ? Math.Clamp(_orbitPitch + md.Y, -60f, 60f) : Math.Clamp(_orbitPitch + md.Y, -10f, 60f);
            if (FirstPerson) _orbitYaw = Math.Clamp(_orbitYaw, -120f, 120f);
        }
        if (ScriptInput is { } si) { steer = si.Steer; w = si.Throttle; s = si.Brake; space = si.Handbrake; }
        car.Drive(steer, w, s, space);
        if (_engine is not null) { try { _engine.Pitch = 0.6f + car.Rpm / car.MaxRpm * 1.4f; } catch { } }

        // Keep the (invisible) player body parked above the car so it doesn't block anything.
        game.Player.Teleport(car.Go.Transform.Position + new Float3(0f, 40f, 0f), moveCamera: false);

        // The glass is opaque (no transparent pass), so from the driver's seat the windscreen pane blocked the whole view:
        // hide pane + sheen streaks in first person and keep the frame/wipers as the "glass" outline.
        foreach (MeshRenderer gr in car.Go.GetComponentsInChildren<MeshRenderer>())
            if (gr.GameObject.Name is "Windshield" or "Sheen") gr.Enabled = !FirstPerson;
        if (FirstPerson)
        {
            Transform ct = car.Go.Transform;
            Float3 hips = car.DriverHips;
            Float3 eye = ct.Position + ct.Right * hips.X + ct.Up * (hips.Y + 0.80f) + ct.Forward * (hips.Z + 0.02f);
            float fy = _orbitYaw * MathF.PI / 180f, fp = -_orbitPitch * MathF.PI / 180f;
            Float3 look = ct.Forward * (MathF.Cos(fy) * MathF.Cos(fp)) + ct.Right * (MathF.Sin(fy) * MathF.Cos(fp)) + ct.Up * MathF.Sin(fp);
            Transform fc = game.Player.CameraObject.Transform;
            fc.Position = eye;
            fc.Rotation = Quaternion.LookRotation(Float3.Normalize(look), ct.Up);
            return;
        }
        // Chase camera: orbit around the car, smoothed.
        float yaw = (car.Go.Transform.LocalEulerAngles.Y + _orbitYaw) * MathF.PI / 180f;
        float pitch = _orbitPitch * MathF.PI / 180f;
        Float3 back = new(-MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
        Float3 focus = car.Go.Transform.Position + new Float3(0f, 1.0f, 0f);
        Float3 want = focus + back * OrbitDist;
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
