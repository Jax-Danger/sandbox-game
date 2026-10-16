using Jitter2.Dynamics;
using Prowl.Runtime;
using Prowl.Vector;
using Color = Prowl.Vector.Color;

namespace Sandbox;

/// <summary>
/// Scripted headless check (dotnet run -- --headless). Exercises spawning, falling, physgun
/// grab/hold/freeze/unfreeze, weld, balloon, remover, undo, clear, and the walking player.
/// </summary>
public sealed class SelfTest
{
    private int _frame;
    private Ragdoll? _ragdoll;
    private readonly List<string> _failures = [];
    private readonly Dictionary<string, Prop> _p = [];
    private float _restBarrelY, _frozenY;
    private bool _done;

    public int ExitCode { get; private set; } = 1;

    public void Begin(SandboxGame g)
    {
        g.Props.ClearAll();
        float x = -5f;
        foreach (PropKind k in PropManager.AllKinds)
        {
            _p[k.ToString()] = g.Props.Spawn(k, new Float3(x, 3f, 6f), 0f);
            x += 2.5f;
        }
    }

    private void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) _failures.Add(what);
    }

    private static float Y(Prop p) => p.Go.Transform.Position.Y;

    private void Aim(SandboxGame g, Float3 target)
    {
        Float3 dir = Float3.Normalize(target - g.Player.EyePosition);
        float yaw = MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI;
        float pitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f)) * 180f / MathF.PI;
        g.Player.SetLook(yaw, -pitch);
        if (Float3.Dot(g.Player.Forward, dir) < 0.99f) g.Player.SetLook(yaw, pitch);
        if (Float3.Dot(g.Player.Forward, dir) < 0.99f) g.Player.SetLook(-yaw, -pitch);
        if (Float3.Dot(g.Player.Forward, dir) < 0.99f) g.Player.SetLook(-yaw, pitch);
    }

    public void Step(SandboxGame g)
    {
        _frame++;
        if (_done) return;
        Prop cube = _p["Cube"], sphere = _p["Sphere"], barrel = _p["Barrel"], plank = _p["Plank"];

        switch (_frame)
        {
            case 120:
                _ragdoll = Ragdoll.Spawn(g, new Float3(-10f, 0.3f, 6f), 0f, recordUndo: false);
                Console.WriteLine("Self-test: props settled");
                foreach ((string name, Prop p) in _p)
                    Check(Y(p) < 2.5f && Y(p) > -0.5f, $"{name} fell and rests on the floor (y {Y(p):0.00})");
                Check(g.Player.IsGrounded && MathF.Abs(g.Player.FeetPosition.Y) < 0.3f,
                    $"player stands on the floor (feet y {g.Player.FeetPosition.Y:0.00})");
                _restBarrelY = Y(barrel);

                // Physgun: aim at the cube and grab it.
                g.SelectPhysGun();
                Aim(g, cube.Go.Transform.Position);
                Check(Float3.Dot(g.Player.Forward, Float3.Normalize(cube.Go.Transform.Position - g.Player.EyePosition)) > 0.99f, "camera aims at the cube");
                g.PhysGun.Update(g.Player, primaryDown: true, primaryHeld: true, secondaryDown: false, wheel: 0f);
                Check(ReferenceEquals(g.PhysGun.Held, cube), "physgun grabbed the cube under the crosshair");
                // Look up a little so the held cube should lift.
                g.Player.SetLook(g.Player.Yaw, 0f);
                break;

            case > 120 and < 200:
                g.PhysGun.Update(g.Player, false, true, false, _frame == 150 ? -1f : 0f);
                break;

            case 200:
            {
                Float3 eye = g.Player.EyePosition;
                float dist = Float3.Length(cube.Go.Transform.Position - eye);
                Check(Y(cube) > 1.0f, $"held cube is lifted off the floor (y {Y(cube):0.00}, {dist:0.0} m from eye)");
                // Freeze with right click while holding.
                g.PhysGun.Update(g.Player, false, true, secondaryDown: true, 0f);
                Check(cube.Frozen && cube.Body.MotionType == MotionType.Static && g.PhysGun.Held is null, "right click froze the held cube and let go");
                _frozenY = Y(cube);
                break;
            }

            case 260:
                Check(MathF.Abs(Y(cube) - _frozenY) < 0.05f, $"frozen cube stays in the air (y {Y(cube):0.00})");
                Aim(g, cube.Go.Transform.Position);
                g.PhysGun.Update(g.Player, true, true, false, 0f);
                Check(!cube.Frozen && ReferenceEquals(g.PhysGun.Held, cube), "clicking the frozen cube unfroze and grabbed it");
                g.PhysGun.Update(g.Player, false, false, false, 0f); // release
                Check(g.PhysGun.Held is null, "releasing left mouse drops the cube");

                // Toolgun: weld sphere to plank, balloon the barrel.
                g.SelectTool(ToolMode.Weld);
                g.ToolGun.Apply(sphere);
                g.ToolGun.Apply(plank);
                Check(sphere.Go.GetComponent<FixedAngleConstraint>().IsValid() && sphere.Go.GetComponent<BallSocketConstraint>().IsValid(),
                    "weld added fixed-angle + ball-socket constraints");
                g.ToolGun.Mode = ToolMode.Balloon;
                g.ToolGun.Apply(barrel);
                break;

            case 330:
                {
                    Ragdoll rd = _ragdoll!;
                    float neck = Float3.Length(rd.Limbs[2].Go.Transform.Position - rd.Limbs[1].Go.Transform.Position);
                    float knee = Float3.Length(rd.Limbs[8].Go.Transform.Position - rd.Limbs[7].Go.Transform.Position);
                    Check(rd.Limbs.Count == 11 && rd.Limbs.All(l => g.Props.FromBody(l.Body) == l), "ragdoll has 11 limbs registered as props");
                    Check(rd.Pelvis.Go.Transform.Position.Y < 0.9f && rd.Limbs.All(l => l.Go.Transform.Position.Y > -0.2f),
                        $"ragdoll collapsed onto the floor (pelvis y {rd.Pelvis.Go.Transform.Position.Y:0.00})");
                    Check(neck < 0.55f && knee < 0.6f, $"ragdoll joints held together (neck {neck:0.00}, knee {knee:0.00})");
                    rd.Remove(g.Props);
                }
                Check(Y(cube) < 2f, $"released cube fell back down (y {Y(cube):0.00})");
                Check(Y(barrel) > _restBarrelY + 1f, $"balloon lifts the barrel (y {_restBarrelY:0.00} -> {Y(barrel):0.00})");
                {
                    float d = Float3.Length(sphere.Go.Transform.Position - plank.Go.Transform.Position);
                    Console.WriteLine($"  (welded sphere-plank distance {d:0.00})");
                }

                // Undo order: balloon, then weld, then the last spawn (ramp).
                int before = g.Props.Props.Count;
                string? u1 = g.Props.Undo();
                Check(u1?.Contains("balloon") == true && !barrel.Go.GetComponent<Balloon>().IsValid(), $"Z undoes the balloon ({u1})");
                string? u2 = g.Props.Undo();
                Check(u2?.Contains("weld") == true && !sphere.Go.GetComponent<FixedAngleConstraint>().IsValid(), $"Z undoes the weld ({u2})");
                string? u3 = g.Props.Undo();
                Check(g.Props.Props.Count == before - 1 && !_p["Ramp"].Alive, $"Z undoes the last spawn ({u3})");

                g.ToolGun.Mode = ToolMode.Remover;
                g.ToolGun.Apply(plank);
                Check(!plank.Alive && g.Props.Props.Count == before - 2, "remover deleted the plank");

                // Spawn at the crosshair: look at the floor in front.
                g.Player.SetLook(0f, 0f);
                Aim(g, g.Player.FeetPosition + g.Player.Forward * 4f);
                Prop spawned = g.SpawnAtCrosshair(PropKind.BigCrate);
                Check(spawned.Alive && Y(spawned) > 0.5f && Y(spawned) < 3f, $"spawn at crosshair placed a crate on the floor (y {Y(spawned):0.00})");

                // Save/load round trip (props, welds, frozen state, balloons).
                {
                    Prop a2 = g.Props.Spawn(PropKind.Cube, new Float3(8f, 0.6f, 0f), 0f);
                    Prop b2 = g.Props.Spawn(PropKind.Cube, new Float3(9.2f, 0.6f, 0f), 0f);
                    g.ToolGun.Weld(a2, b2);
                    b2.SetFrozen(true);
                    a2.SetColor(new Color(0.1f, 0.9f, 0.2f, 1f));
                    string path = Path.Combine(Path.GetTempPath(), "sandbox-selftest-save.json");
                    int saved = MapSave.Save(g.Props, path);
                    g.ClearMap();
                    int loaded = MapSave.Load(g, path);
                    int welds = g.Props.Props.Sum(p => p.Go.GetComponents<FixedAngleConstraint>().Count());
                    int frozen = g.Props.Props.Count(p => p.Frozen);
                    Check(saved == loaded && loaded >= 3 && welds == 1 && frozen == 1, $"save/load restores props, weld and frozen state ({saved} saved, {loaded} loaded, {welds} weld, {frozen} frozen)");
                    Check(g.Props.Props.Any(p => MathF.Abs(p.BaseColor.G - 0.9f) < 0.01f), "save/load restores colours");
                }


                // Context menu edits.
                {
                    Prop c = g.Props.Spawn(PropKind.Cube, new Float3(12f, 0.6f, 0f), 0f);
                    c.SetSurface(SurfaceMaterial.Get("Rubber"));
                    c.SetMass(25f);
                    c.SetColor(ContextMenu.Palette[4]);
                    Check(c.Surface == "Rubber" && MathF.Abs(c.Body.Restitution - 0.75f) < 0.01f && MathF.Abs(c.Body.Mass - 25f) < 0.01f,
                        "context menu edits material (friction/bounce), mass and colour");
                }

                // Settings JSON round trip.
                {
                    string old = Settings.FilePath;
                    Settings.FilePath = Path.Combine(Path.GetTempPath(), "sandbox-selftest-settings.json");
                    var st = new Settings { MouseSensitivity = 2.5f, FieldOfView = 95f, Graphics = GraphicsQuality.Medium, ShowFps = true };
                    st.Save();
                    Settings back = Settings.Load();
                    Check(back.MouseSensitivity == 2.5f && back.FieldOfView == 95f && back.Graphics == GraphicsQuality.Medium && back.ShowFps,
                        "settings save and load from JSON");
                    Settings.FilePath = old;
                }

                g.ClearMap();
                Check(g.Props.Props.Count == 0, "clear map removed every prop");

                _done = true;
                ExitCode = _failures.Count == 0 ? 0 : 1;
                Console.WriteLine(_failures.Count == 0
                    ? "Sandbox self-test passed."
                    : $"Sandbox self-test FAILED ({_failures.Count}): {string.Join("; ", _failures)}");
                break;
        }
    }
}
