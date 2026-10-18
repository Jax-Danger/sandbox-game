using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Screenshot-only showcase scenes ("--shot gal_&lt;set&gt;"): lines up every model of a category, frozen, in front of
/// the player with the HUD and view model hidden, so model polish can be reviewed in one image.
/// Sets: props (model props), basic (prop kinds + entities), vehicles, npcs, weapons (spawn-menu weapon models).
/// </summary>
public static class Gallery
{
    public static bool Active { get; private set; }

    public static void Show(SandboxGame g, string set)
    {
        Active = true;
        g.PhotoMode = true;
        var spots = new List<Float3>();
        Prop Freeze(Prop p) => p; // frozen props are tinted blue, so gallery props just settle
        switch (set.Split('_')[0])
        {
            case "props":
            {
                int page = set.Contains('_') && int.TryParse(set.Split('_')[1], out int pg) ? pg : 0;
                var defs = ModelProps.All.Skip(page * 15).Take(15).ToArray();
                for (int i = 0; i < defs.Length; i++)
                {
                    int col = i % 5, row = i / 5;
                    Float3 at = new(-4.4f + col * 2.2f, defs[i].HalfHeight + 0.02f, 3.2f + row * 2.6f);
                    Freeze(ModelProps.Spawn(g, defs[i], at, 200f, false));
                }
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 22f);
                break;
            }
            case "drive":
            {
                // Scripted drive test: straight, hard right, hard left, let go. Logs to /tmp/drivetest-<kind>.log.
                string kind = set.Contains("jeep") ? "jeep" : "buggy";
                Buggy car = g.Vehicles.SpawnBuggy(new Float3(-40f, 1.2f, -40f), 0f, false, kind);
                g.Vehicles.Enter(car);
                var go = new GameObject("DriveTest");
                go.AddComponent<DriveTest>().Init(g, car, kind);
                g.World.Add(go);
                break;
            }
            case "tools":
            {
                // Tool-gun attachments on frozen cubes: wheel, thruster, balloon, hoverball, light.
                ToolMode[] modes = [ToolMode.Wheel, ToolMode.Thruster, ToolMode.Balloon, ToolMode.Hoverball, ToolMode.Light];
                for (int i = 0; i < modes.Length; i++)
                {
                    Float3 at = new(-3.2f + i * 1.6f, 0.9f, 4f);
                    Prop c = g.Props.Spawn(PropKind.Cube, at, 0f, false);
                    c.Body.MotionType = Jitter2.Dynamics.MotionType.Static;
                    Float3 n = modes[i] == ToolMode.Wheel || modes[i] == ToolMode.Thruster ? -Float3.UnitX : Float3.UnitY;
                    if (modes[i] == ToolMode.Thruster) n = -Float3.UnitZ;
                    g.ToolGun.SetHit(c, at + n * 0.5f, n);
                    ToolMode old = g.ToolGun.Mode; g.ToolGun.Mode = modes[i]; g.ToolGun.Apply(c); g.ToolGun.Mode = old;
                }
                g.Player.Teleport(new Float3(0f, 0.05f, -0.5f));
                g.Player.SetLook(0f, 12f);
                break;
            }
            case "basic":
            {
                var kinds = PropManager.AllKinds;
                for (int i = 0; i < kinds.Length; i++)
                {
                    int col = i % 6, row = i / 6;
                    Float3 at = new(-6f + col * 2.4f, PropManager.HalfHeightOf(kinds[i]) + 0.4f, 6.5f + row * 3.6f);
                    Freeze(g.Props.Spawn(kinds[i], at, 20f, false));
                }
                Freeze(EntityCatalog.SpawnBarrel(g, new Float3(-4f, 0.6f, 4.5f)));
                Freeze(EntityCatalog.SpawnBall(g, new Float3(-2.4f, 0.6f, 4.5f)));
                Freeze(EntityCatalog.SpawnLight(g, new Float3(-0.8f, 0.5f, 4.5f)));
                Freeze(EntityCatalog.SpawnButton(g, new Float3(0.8f, 0.2f, 4.5f), 180f));
                Freeze(CameraFeeds.Spawn(g, new Float3(2.4f, 0.6f, 4.5f), 180f, recordUndo: false));
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 22f);
                break;
            }
            case "vehicles":
            {
                g.Vehicles.SpawnBuggy(new Float3(-3.4f, 1.0f, 4.2f), 215f, false);
                g.Vehicles.SpawnBuggy(new Float3(0f, 1.2f, 4.8f), 215f, false, "jeep");
                g.Vehicles.SpawnBuggy(new Float3(3.6f, 0.9f, 4.2f), 215f, false, "airboat");
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 18f);
                break;
            }
            case "npcs":
            case "face":
            {
                NpcTeam[] teams = [NpcTeam.Dummy, NpcTeam.Friendly, NpcTeam.Hostile];
                for (int i = 0; i < 3; i++)
                {
                    Ragdoll rd = EntityCatalog.SpawnDummy(g, new Float3(-1.5f + i * 1.2f, 0.05f, 2.2f), 180f, false, teams[i]);
                    foreach (Prop l in rd.Limbs) { if (l.Go.GetComponent<DummyBrain>() is { } b) b.Enabled = false; l.Body.MotionType = Jitter2.Dynamics.MotionType.Static; }
                }
                Ragdoll.Spawn(g, new Float3(2.4f, 0.6f, 2.6f), 140f, false);
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 10f);
                if (set.StartsWith("face")) { g.Player.Teleport(new Float3(-0.3f, 0.05f, 1.45f)); g.Player.SetLook(0f, 4f); }
                break;
            }
        }
    }

    /// <summary>Keeps the view model hidden while a gallery is on screen.</summary>
    public static void Hide(ViewModel? vm) { if (Active) vm?.SetVisible(false); }
}

/// <summary>Drives a car through a fixed input script and logs speed, yaw rate, roll and slip angle.</summary>
public sealed class DriveTest : MonoBehaviour
{
    private SandboxGame _g = null!; private Buggy _car = null!; private float _t, _next; private string _path = "";
    private float _maxRoll, _maxSlip; private readonly System.Text.StringBuilder _log = new();
    public void Init(SandboxGame g, Buggy car, string kind) { _g = g; _car = car; _path = $"/tmp/drivetest-{kind}.log"; }
    public override void Update()
    {
        if (_car is null || !_car.Go.IsValid()) return;
        _t += Time.DeltaTime;
        float steer = _t < 4f ? 0f : _t < 8f ? 1f : _t < 11f ? -1f : 0f;
        bool thr = _t > 1f && _t < 13f;
        _g.Vehicles.ScriptInput = (steer, thr, false, false);
        if (_t >= _next)
        {
            _next += 0.25f;
            Float3 v = _car.Body.LinearVelocity, f = _car.Go.Transform.Forward, r = _car.Go.Transform.Right;
            float spd = Float3.Length(new Float3(v.X, 0f, v.Z));
            float slip = spd > 2f ? MathF.Atan2(Float3.Dot(v, r), MathF.Max(0.1f, Float3.Dot(v, f))) * 180f / MathF.PI : 0f;
            float roll = MathF.Asin(Math.Clamp(Float3.Dot(r, Float3.UnitY), -1f, 1f)) * 180f / MathF.PI;
            float yaw = Float3.Dot(_car.Body.AngularVelocity, _car.Go.Transform.Up);
            if (_t > 1.5f) { _maxRoll = MathF.Max(_maxRoll, MathF.Abs(roll)); _maxSlip = MathF.Max(_maxSlip, MathF.Abs(slip)); }
            _log.AppendLine($"t={_t:0.00} steerIn={steer:0} steer={_car.SteerInput:0.00} kmh={_car.SpeedKmh:0.0} yaw={yaw:0.00} roll={roll:0.0} slip={slip:0.0} up={_car.Go.Transform.Up.Y:0.00}");
            try { System.IO.File.WriteAllText(_path, _log + $"MAX roll={_maxRoll:0.0} slip={_maxSlip:0.0}\n"); } catch { }
        }
    }
}
