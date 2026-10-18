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
    private Prop? _carryCube, _heavy;
    private float _vmRestY;
    private Prop? _target, _barrel;
    private PistolWeapon? _pistol;
    private Buggy? _buggy, _jeep, _boat;
    private Float3 _jeepStart, _boatStart;
    private Prop? _ea, _eb, _hb, _woodP, _metalP;
    private Water.Volume? _pool;
    private Prop? _thrust, _xbarrel, _button, _ball;
    private Ragdoll? _dummy;
    private float _ballMaxY;
    private int _explBefore;
    private float _thrustY;
    private Float3 _buggyStart;
    private readonly List<string> _failures = [];
    private readonly Dictionary<string, Prop> _p = [];
    private float _restBarrelY, _frozenY;
    private bool _done;

    public int ExitCode { get => Soak?.ExitCode ?? _exit; private set => _exit = value; }
    private int _exit = 1;
    public Soak? Soak { get; init; }

    public void Begin(SandboxGame g)
    {
        if (Soak is not null) { Soak.Begin(g); return; }
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

    private float _jumpY0, _jumpPeak;
    private Prop? _cone;
    public void Step(SandboxGame g)
    {
        if (Soak is not null) { Soak.Step(g); return; }
        _frame++;
        if (_done) return;
        Prop cube = _p["Cube"], sphere = _p["Sphere"], barrel = _p["Barrel"], plank = _p["Plank"];

        switch (_frame)
        {
            case 40:
                _jumpY0 = g.Player.FeetPosition.Y; _jumpPeak = _jumpY0;
                g.Player.JumpRequested = true;
                break;
            case > 40 and < 100:
                _jumpPeak = MathF.Max(_jumpPeak, g.Player.FeetPosition.Y);
                if (_frame == 99) Check(_jumpPeak - _jumpY0 > 0.5f && g.Player.Jumps > 0, $"jump leaves the ground (rose {_jumpPeak - _jumpY0:0.00} m)");
                break;
            case 120:
                _ragdoll = Ragdoll.Spawn(g, new Float3(-10f, 0.3f, 6f), 0f, recordUndo: false);
                // Ballasted traffic cone tipped past a plain box's balance point (27°) must rock back upright.
                _cone = ModelProps.Spawn(g, ModelProps.All.First(d => d.Id == "cone"), new Float3(14f, 0.4f, 14f), 0f, recordUndo: false);
                _cone.Go.Transform.LocalEulerAngles = new Float3(32f, 0f, 0f);
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
                if (_cone is not null) { float up = Float3.Dot(_cone.Go.Transform.Up, Float3.UnitY); Check(up > 0.97f, $"tipped traffic cone rights itself (up {up:0.00})"); }
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
                Check(g.Props.Props.Count == before - 1 && !_p[PropManager.AllKinds[^1].ToString()].Alive, $"Z undoes the last spawn ({u3})");

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
                    int saved = MapSave.Save(g, path);
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
                    // Rebinding persists and Binds reads it; key-picker tool settings write KeyCodes.
                    var prevSrc = Binds.Source;
                    Binds.Source = back;
                    Check(Binds.Get("use") == Prowl.Runtime.KeyCode.E, "default keybind (use = E)");
                    Binds.Set("use", Prowl.Runtime.KeyCode.G);
                    Settings rb = Settings.Load();
                    Check(rb.Binds?["use"] == Prowl.Runtime.KeyCode.G, "rebound key saved to settings JSON");
                    Binds.Source = prevSrc;
                    var thr = g.ToolGun.SettingsFor(ToolMode.Thruster).First(t => t.IsKey);
                    thr.Set((int)Prowl.Runtime.KeyCode.Keypad5);
                    Check(g.ToolGun.ThrusterKey == Prowl.Runtime.KeyCode.Keypad5 && thr.Format(thr.Get()) == "Num 5", "key-picker setting binds any key (thruster -> Num 5)");
                    g.ToolGun.ThrusterKey = Prowl.Runtime.KeyCode.T;
                    Settings.FilePath = old;
                }

                g.ClearMap();
                Check(g.Props.Props.Count == 0, "clear map removed every prop");

                // Carry setup: a light cube in front of the player, picked up with E.
                g.Player.SetLook(0f, 0f);
                _carryCube = g.Props.Spawn(PropKind.Cube, g.Player.EyePosition + g.Player.Forward * 1.8f, 0f);
                _heavy = g.Props.Spawn(PropKind.BigCrate, g.Player.FeetPosition + g.Player.CameraObject.Transform.Right * 2.2f + new Float3(0f, 1.1f, 0f), 0f);
                _heavy.SetMass(100f);
                break;
            case 335:
                Aim(g, _carryCube!.Go.Transform.Position);
                g.Carry.Use(g);
                Check(ReferenceEquals(g.Carry.Held, _carryCube), "E picks up a light prop");
                break;
            case > 335 and < 390:
                g.Carry.Update(g);
                break;
            case 390:
                {
                    float d = Float3.Length(_carryCube!.Go.Transform.Position - (g.Player.EyePosition + g.Player.Forward * Carry.HoldDistance));
                    Check(d < 0.5f, $"carried prop follows the view (off by {d:0.00} m)");
                    g.Carry.Throw(g);
                    Check(g.Carry.Held is null && Float3.Length(_carryCube.Body.LinearVelocity) > 5f, "left click throws the carried prop");
                    Aim(g, _heavy!.Go.Transform.Position);
                    string? msg = g.Carry.Use(g);
                    Check(g.Carry.Held is null && msg?.Contains("heavy") == true, $"weight limit refuses heavy props ({msg})");

                    // Spawn gun fires the selected entry; right click cycles recent spawns.
                    g.Weapons.Select(g, g.Weapons.Get<SpawnGunWeapon>());
                    g.SpawnGunEntry = SpawnCatalog.Find("prop_barrel");
                    g.Player.SetLook(0f, 30f);
                    int n = g.Props.Props.Count;
                    g.Weapons.Tick(g, new WeaponInput(true, true, false, false, false, false, 0f), 0.016f);
                    Check(g.Props.Props.Count == n + 1 && g.Props.Props[^1].Kind == PropKind.Barrel, "spawn gun spawns the selected entry");
                    g.Weapons.Tick(g, new WeaponInput(false, false, false, true, true, false, 0f), 0.016f);
                    Check(g.SpawnGunEntry is not null, $"spawn gun right click cycles recent ({g.SpawnGunEntry?.Name})");
                    Check(g.Weapons.All[0] is HandsWeapon, "hands are weapon slot 1");

                    // View model: rig built with the standard bones, clips sample and blend.
                    ViewModel? vm = g.Weapons.Current.Vm;
                    Check(vm is not null && ViewModelDef.StandardBones.All(b => vm.Bones.ContainsKey(b)), "view model rig has the standard bones");
                    if (vm is not null)
                    {
                        Float3 rest = vm.Bones["weapon"].Transform.LocalPosition;
                        vm.Kick(1f);
                        for (int i = 0; i < 20; i++) vm.Update(0.15f, Float2.Zero, 0f); // ~7 FPS
                        float drift = Float3.Length(vm.Bones["weapon"].Transform.LocalPosition - rest);
                        Check(drift < 0.05f, $"view-model recoil stays stable at low FPS (drift {drift:0.000})");
                    }
                    VmClip reload = ViewModels.Create("smg").Clips["reload"];
                    BonePose mid = reload.Sample("hand_r", 0.5f), end = reload.Sample("hand_r", reload.Length);
                    Check(MathF.Abs(mid.Rot.Z) > 20f && MathF.Abs(end.Rot.Z) < 0.01f, $"reload clip tilts the weapon and returns to rest ({mid.Rot.Z:0})");
                    Check(vm!.Play("inspect") && vm.ClipPlaying, "inspect clip plays");
                    _vmRestY = vm.Bones["arm_r"].Transform.LocalPosition.Y;
                }
                break;
            case > 390 and < 420:
                break;
            case 420:
                {
                    ViewModel vm = g.Weapons.Current.Vm!;
                    float dy = MathF.Abs(vm.Bones["arm_r"].Transform.LocalPosition.Y - _vmRestY);
                    Check(vm.CurrentClip == "inspect" && dy > 0.005f, $"inspect animates the arm bone (moved {dy:0.000} m)");

                    // Weapons: pistol hitscan pushes a prop, uses ammo, reload refills.
                    g.Player.SetLook(0f, 0f);
                    _target = g.Props.Spawn(PropKind.Cube, g.Player.EyePosition + g.Player.Forward * 6f, 0f, recordUndo: false);
                    _target.Body.AffectedByGravity = false;
                    _pistol = g.Weapons.Get<PistolWeapon>();
                    g.Weapons.Select(g, _pistol);
                    Aim(g, _target.Go.Transform.Position);
                    g.Weapons.Tick(g, new WeaponInput(true, true, false, false, false, false, 0f), 0.016f);
                    Check(_pistol.Mag == 11 && Float3.Length(_target.Body.LinearVelocity) > 1f,
                        $"pistol shot uses ammo and pushes the prop (v {Float3.Length(_target.Body.LinearVelocity):0.0})");
                    _pistol.Mag = 3;
                    g.Weapons.Tick(g, new WeaponInput(false, false, false, false, false, true, 0f), 0.016f);
                    Check(_pistol.Reloading, "R starts a reload");
                }
                break;
            case > 420 and < 540:
                g.Weapons.Tick(g, new WeaponInput(false, false, false, false, false, false, 0f), 1f / 30f);
                if (_frame == 500)
                {
                    Check(!_pistol!.Reloading && _pistol.Mag == 12, $"reload refills the magazine ({_pistol.Mag})");
                    // Rocket at a cube on the floor; grenade dropped at our feet-ish.
                    g.ClearMap();
                    _barrel = g.Props.Spawn(PropKind.Cube, g.Player.FeetPosition + new Float3(0f, 0.5f, 8f), 0f, recordUndo: false);
                    Aim(g, _barrel.Go.Transform.Position);
                    var rpg = g.Weapons.Get<RocketLauncherWeapon>();
                    g.Weapons.Select(g, rpg);
                    g.Weapons.Tick(g, new WeaponInput(true, true, false, false, false, false, 0f), 0.016f);
                    Check(rpg.Mag == 0, "rocket launcher fires a rocket");
                }
                break;
            case 540:
                {
                    Check(g.Fx.Explosions >= 1 && Float3.Length(_barrel!.Body.LinearVelocity) > 2f,
                        $"rocket explodes and blasts the prop (explosions {g.Fx.Explosions}, v {Float3.Length(_barrel!.Body.LinearVelocity):0.0})");
                    var cb = g.Weapons.Get<CrowbarWeapon>();
                    g.Weapons.Select(g, cb);
                    Prop c = g.Props.Spawn(PropKind.Cube, g.Player.EyePosition + g.Player.Forward * 1.3f, 0f, recordUndo: false);
                    c.Body.AffectedByGravity = false;
                    Aim(g, c.Go.Transform.Position);
                    cb.Strike(g);
                    Check(cb.Hits == 1 && Float3.Length(c.Body.LinearVelocity) > 1f, "crowbar hit knocks a prop");
                    g.ClearMap();
                    _buggy = g.Vehicles.SpawnBuggy(g.Player.FeetPosition + new Float3(4f, 1.2f, 0f), 0f);
                    _buggyStart = _buggy.Go.Transform.Position;
                    _jeep = g.Vehicles.SpawnBuggy(g.Player.FeetPosition + new Float3(-6f, 1.4f, 0f), 0f, kind: "jeep");
                    _boat = g.Vehicles.SpawnBuggy(g.Player.FeetPosition + new Float3(-14f, 1.0f, 0f), 0f, kind: "airboat");
                    // Physics behaviour: elastic pulls, hoverball holds.
                    _ea = g.Props.Spawn(PropKind.Cube, new Float3(30f, 8f, -30f), 0f, recordUndo: false);
                    _eb = g.Props.Spawn(PropKind.Cube, new Float3(36f, 8f, -30f), 0f, recordUndo: false);
                    _ea.Body.AffectedByGravity = false; _eb.Body.AffectedByGravity = false;
                    g.ToolGun.Elastic(_ea, Float3.Zero, _eb, Float3.Zero, 3f, rest: 1f, recordUndo: false);
                    _hb = g.Props.Spawn(PropKind.Cube, new Float3(-30f, 3f, -30f), 0f, recordUndo: false);
                    Hoverball.Attach(g.Props, _hb, _hb.Go.Transform.Position, Float3.UnitY, 1f, KeyCode.Keypad8, KeyCode.Keypad2);
                    // Buoyancy: a test pool (surface y = 2) with a wooden crate and a metal cube dropped in.
                    _pool = Water.Add(new Float3(0f, -4f, -40f), new Float3(5f, 6f, 5f));
                    _woodP = g.Props.Spawn(PropKind.SmallCrate, new Float3(-1.5f, 3.5f, -40f), 0f, recordUndo: false);
                    _woodP.SetSurface(SurfaceMaterial.Get("Wood"));
                    _metalP = g.Props.Spawn(PropKind.Cube, new Float3(1.5f, 3.5f, -40f), 0f, recordUndo: false);
                    _metalP.SetSurface(SurfaceMaterial.Get("Metal"));
                    Check(g.Weapons.All.Select(w => w.Id).Distinct().Count() == g.Weapons.All.Count && g.Weapons.All.Count >= 10, $"weapon switcher has {g.Weapons.All.Count} weapons");
                }
                break;
            case 600:
                {
                    Buggy b = _buggy!;
                    Check(b.Front.Concat(b.Rear).Count(w => w.IsGrounded) >= 3, $"buggy rests on its wheels (y {b.Go.Transform.Position.Y:0.00})");
                    Aim(g, b.Go.Transform.Position);
                    Check(g.Vehicles.TryUse() && g.InVehicle, "E enters the buggy");
                    _buggyStart = b.Go.Transform.Position;
                }
                break;
            case > 600 and < 720:
                _buggy!.Drive(0f, true, false, false);
                g.Vehicles.Tick(Time.DeltaTime, false);
                _buggy.Drive(0f, true, false, false);
                if (_frame >= 620) { _jeep!.Drive(0f, true, false, false); _boat!.Drive(0f, true, false, false); }
                else { _jeepStart = _jeep!.Go.Transform.Position; _boatStart = _boat!.Go.Transform.Position; }
                break;
            case 720:
                {
                    Buggy b = _buggy!;
                    float moved = Float3.Length(b.Go.Transform.Position - _buggyStart);
                    Check(moved > 3f && b.SpeedKmh > 15f && b.SpeedKmh < 55f, $"throttle drives the buggy at a sane pace ({moved:0.0} m, {b.SpeedKmh:0} km/h, gear {b.Gear})");
                    Check(g.Vehicles.TryUse() && !g.InVehicle && Float3.Length(g.Player.FeetPosition - b.Go.Transform.Position) < 4f, "E exits beside the buggy");
                    float jm = Float3.Dot(_jeep!.Go.Transform.Position - _jeepStart, _jeep.Go.Transform.Forward), bm = Float3.Dot(_boat!.Go.Transform.Position - _boatStart, _boat.Go.Transform.Forward);
                    Check(jm > 2f && _jeep.Front.Concat(_jeep.Rear).Count(w => w.IsGrounded) >= 3, $"jeep drives on four wheels ({jm:0.0} m)");
                    Check(bm > 2f && _boat.Go.Transform.Up.Y > 0.5f /* it runs up onto the gentle ramp; just not capsized */, $"airboat fan pushes the hull forward upright ({bm:0.0} m, up {_boat.Go.Transform.Up.Y:0.00}, at {_boat.Go.Transform.Position})");
                    float ed = Float3.Length(_ea!.Go.Transform.Position - _eb!.Go.Transform.Position);
                    Check(ed < 4.5f, $"elastic pulls props together (6.0 → {ed:0.0} m)");
                    float wy = _woodP!.Go.Transform.Position.Y, my = _metalP!.Go.Transform.Position.Y;
                    Check(wy > 1.5f && wy < 2.6f && MathF.Abs(_woodP.Body.LinearVelocity.Y) < 1f, $"wood floats at the water surface (y {wy:0.00}, surface 2.00)");
                    Check(my < 1.0f, $"metal sinks to the bottom (y {my:0.00})");
                    Water.Volumes.Remove(_pool!);
                    float hy = _hb!.Go.Transform.Position.Y, ht = _hb.Go.GetComponent<Hoverball>()!.Target;
                    Check(MathF.Abs(hy - ht) < 0.6f, $"hoverball holds its height (y {hy:0.00}, target {ht:0.00})");
                    var data = MapSave.Capture(g);
                    Check(data.Buggies.Select(x => x.Kind).Distinct().Count() == 3, "vehicle kinds saved");
                }
                {
                    g.ClearMap();
                    // Extra tools.
                    ToolGun t = g.ToolGun;
                    Float3 basePos = new(20f, 0.6f, 20f);
                    Prop a = g.Props.Spawn(PropKind.Cube, basePos, 0f), bb = g.Props.Spawn(PropKind.Cube, basePos + new Float3(3f, 0f, 0f), 0f);
                    t.Mode = ToolMode.Rope;
                    t.SetHit(a, a.Go.Transform.Position, Float3.UnitY); t.Apply(a);
                    t.SetHit(bb, bb.Go.Transform.Position, Float3.UnitY); string? rope = t.Apply(bb);
                    Check(a.Go.GetComponent<DistanceLimitConstraint>().IsValid() && a.Go.GetComponent<RopeVisual>().IsValid(), $"rope tool links two props ({rope})");
                    t.Mode = ToolMode.BallSocket;
                    Prop c1 = g.Props.Spawn(PropKind.Sphere, basePos + new Float3(0f, 0f, 3f), 0f);
                    t.SetHit(c1, c1.Go.Transform.Position, Float3.UnitY); t.Apply(c1);
                    t.SetHit(bb, bb.Go.Transform.Position, Float3.UnitY); t.Apply(bb);
                    Check(c1.Go.GetComponent<BallSocketConstraint>().IsValid(), "ball socket tool pins props");
                    t.Mode = ToolMode.Colour; t.PaintHue = 0.33f;
                    Color col0 = a.BaseColor; t.Apply(a);
                    Check(MathF.Abs(a.BaseColor.G - col0.G) + MathF.Abs(a.BaseColor.R - col0.R) > 0.05f, "colour tool paints");
                    t.Mode = ToolMode.Material; t.MaterialIndex = 4; t.Apply(a);
                    Check(a.Surface == SurfaceMaterial.All[4].Name, $"material tool sets {a.Surface}");
                    t.Mode = ToolMode.Light; t.SetHit(bb, bb.Go.Transform.Position + new Float3(0f, 0.5f, 0f), Float3.UnitY); t.Apply(bb);
                    Check(bb.Go.GetComponentsInChildren<PointLight>().Any(), "light tool attaches a light");
                    t.Mode = ToolMode.Wheel; t.SetHit(bb, bb.Go.Transform.Position + new Float3(0.5f, 0f, 0f), Float3.UnitX);
                    int n0 = g.Props.Props.Count; t.Apply(bb);
                    Check(g.Props.Props.Count == n0 + 1 && g.Props.Props[^1].Go.GetComponent<HingeJoint>().IsValid(), "wheel tool adds a hinged wheel");
                    t.Mode = ToolMode.Thruster;
                    _thrust = g.Props.Spawn(PropKind.Cube, basePos + new Float3(-4f, 0f, 0f), 0f);
                    t.SetHit(_thrust, _thrust.Go.Transform.Position - new Float3(0f, 0.5f, 0f), -Float3.UnitY); t.Apply(_thrust);
                    _thrust.Go.GetComponent<Thruster>().ForceOn = true;
                    _thrustY = _thrust.Go.Transform.Position.Y;
                    // Duplicator: copy bb (via secondary needs aim) — use the API path: aim + Secondary + Fire.
                    t.Mode = ToolMode.Duplicator;
                    g.Player.Teleport(a.Go.Transform.Position + new Float3(0f, -0.6f, -4f));
                    Aim(g, a.Go.Transform.Position);
                    string? cp = t.Secondary(g.Player);
                    Check(t.HasCopy, $"duplicator copies ({cp})");
                }
                break;
            case 780:
                Check(_thrust!.Go.Transform.Position.Y > _thrustY + 1f, $"thruster lifts the prop ({_thrustY:0.0} -> {_thrust.Go.Transform.Position.Y:0.0})");
                // Entities.
                _dummy = EntityCatalog.SpawnDummy(g, new Float3(-20f, 0f, -20f), 0f);
                _xbarrel = EntityCatalog.SpawnBarrel(g, new Float3(-24f, 0.6f, -20f));
                _button = EntityCatalog.SpawnButton(g, new Float3(-26f, 0.3f, -24f), 0f);
                _ball = EntityCatalog.SpawnBall(g, new Float3(-45f, 4f, 40f));
                Prop lightEnt = EntityCatalog.SpawnLight(g, new Float3(-30f, 0.3f, -20f));
                Check(lightEnt.Go.GetComponentsInChildren<PointLight>().Any(), "light entity glows");
                Check(SpawnCatalog.Entries.Count(e => e.Tab == MenuTab.Entities) >= 6, "entities tab lists NPCs, barrel, ball, light, button");
                break;
            case 860:
                {
                    float py = _dummy!.Pelvis.Go.Transform.Position.Y;
                    Check(py > 0.75f && !_dummy.Pelvis.Go.GetComponent<DummyBrain>().Limp, $"NPC dummy stands (pelvis y {py:0.00})");
                    Fx.Push(_dummy.Limbs[1].Body, new Float3(0f, 0f, 60f), _dummy.Limbs[1].Go.Transform.Position);
                    var sb = _button!.Go.GetComponent<SandboxButton>();
                    sb.Press(g);
                    Check(sb.On && KeyState.Held(sb.Key), $"button toggles its key ({sb.Key})");
                    sb.Press(g);
                    _xbarrel!.Go.GetComponent<ExplosiveBarrel>().Damage(100f);
                    _ballMaxY = 0f;
                    _explBefore = g.Fx.Explosions;
                }
                break;
            case > 860 and < 960:
                if (_frame > 900) _ballMaxY = MathF.Max(_ballMaxY, _ball!.Go.Transform.Position.Y);
                break;
            case 960:
                Check(_dummy!.Pelvis.Go.GetComponent<DummyBrain>().Limp, "hitting the dummy makes it go limp");
                Check(g.Fx.Explosions > _explBefore && !_xbarrel!.Alive, "explosive barrel blows up when damaged");
                {
                    // Procedural audio: every placeholder renders to a valid WAV.
                    int ok = 0;
                    foreach (string id in SoundGen.Ids)
                    {
                        string? p = AssetRegistry.SoundPath(id);
                        if (p is not null && new FileInfo(p).Length > 1000 && File.ReadAllBytes(p).AsSpan(0, 4).SequenceEqual("RIFF"u8)) ok++;
                    }
                    Check(ok == SoundGen.Ids.Length, $"procedural WAVs generated ({ok}/{SoundGen.Ids.Length})");
                    g.Sounds.Play("sfx_explosion"); // must not throw without a device
                }
                {
                    // Full save/load round trip of the new object types.
                    g.ClearMap();
                    ToolGun t = g.ToolGun;
                    Prop a = g.Props.Spawn(PropKind.Cube, new Float3(30f, 0.6f, 30f), 0f), b = g.Props.Spawn(PropKind.Cube, new Float3(33f, 0.6f, 30f), 0f);
                    t.Rope(a, Float3.Zero, b, Float3.Zero);
                    Thruster.Attach(g.Props, a, a.Go.Transform.Position + new Float3(0f, 0.5f, 0f), Float3.UnitY, 2f, KeyCode.Y);
                    t.AttachWheel(b, b.Go.Transform.Position + new Float3(0.5f, 0f, 0f), Float3.UnitX);
                    t.AttachLight(b, b.Go.Transform.Position + new Float3(0f, 0.6f, 0f), 9f, new Color(1f, 0.5f, 0.2f, 1f));
                    a.SetSurface(SurfaceMaterial.Get("Metal")); a.SetMass(42f);
                    EntityCatalog.SpawnBarrel(g, new Float3(36f, 0.6f, 30f));
                    EntityCatalog.SpawnBall(g, new Float3(38f, 0.6f, 30f));
                    Prop btn = EntityCatalog.SpawnButton(g, new Float3(40f, 0.3f, 30f), 0f);
                    EntityCatalog.SpawnDummy(g, new Float3(30f, 0f, 36f), 0f);
                    g.Vehicles.SpawnBuggy(new Float3(36f, 1.2f, 36f), 90f);
                    string path = Path.Combine(Path.GetTempPath(), "sandbox-selftest-full.json");
                    MapSave.SaveData before2 = MapSave.Capture(g);
                    MapSave.Save(g, path);
                    MapSave.Load(g, path);
                    MapSave.SaveData after = MapSave.Capture(g);
                    string Sum(MapSave.SaveData d) => $"props {d.Props.Count}, ropes {d.Ropes.Count}, thrusters {d.Thrusters.Count}, wheels {d.Wheels.Count}, lights {d.Lights.Count}, " +
                        $"entities {string.Join("+", d.Props.Where(p => p.Entity != null).Select(p => p.Entity).OrderBy(x => x))}, dummies {d.Ragdolls.Count(r => r.Dummy)}, buggies {d.Buggies.Count}";
                    Check(Sum(before2) == Sum(after) && after.Ropes.Count == 1 && after.Thrusters.Count == 1 && after.Wheels.Count == 1 && after.Buggies.Count == 1,
                        $"full save/load round trip ({Sum(after)})");
                    Prop a2 = g.Props.Props.First(p => p.Kind == PropKind.Cube && p.Surface == "Metal");
                    Check(MathF.Abs(a2.Body.Mass - 42f) < 0.01f && a2.Go.GetComponent<Thruster>()?.Key == KeyCode.Y, "loaded prop keeps mass, material and thruster key");
                    Check(g.Props.Props.Any(p => p.Entity == "ent_barrel" && p.Go.GetComponent<ExplosiveBarrel>().IsValid()), "explosive barrel keeps its behaviour after load");
                    g.ClearMap();
                }
                {
                    // Performance: 200 props + 8 ragdolls, then time physics steps directly (Jitter sleeps resting bodies).
                    for (int i = 0; i < 200; i++)
                        g.Props.Spawn(PropManager.AllKinds[i % PropManager.AllKinds.Length], new Float3(30f + (i % 20) * 1.1f, 1f + (i / 20) * 1.3f, 30f), i * 17f, recordUndo: false);
                    for (int i = 0; i < 8; i++) Ragdoll.Spawn(g, new Float3(30f + i * 2.2f, 1.2f, 26f), 0f, recordUndo: false);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 120; i++) g.World.Physics.Update();
                    double settle = sw.Elapsed.TotalMilliseconds / 120.0;
                    for (int i = 0; i < 240; i++) g.World.Physics.Update();
                    sw.Restart();
                    for (int i = 0; i < 60; i++) g.World.Physics.Update();
                    double rest = sw.Elapsed.TotalMilliseconds / 60.0;
                    Check(settle < 16f, $"physics with {g.Props.Props.Count} props + 8 ragdolls: {settle:0.00} ms/step falling, {rest:0.00} ms/step at rest");
                    g.ClearMap();
                    Prop cam = CameraFeeds.Spawn(g, new Float3(0f, 1f, 5f), 0f);
                    string camPath = Path.Combine(Path.GetTempPath(), "sandbox-cam-test.json");
                    MapSave.Save(g, camPath); MapSave.Load(g, camPath);
                    Check(CameraFeeds.Active is { Entity: "ent_camera" } && CameraFeeds.All.Count == 1, "camera entity spawns and survives save/load");
                    while (g.Props.Props.Count < SandboxGame.MaxProps)
                        g.Props.Spawn(PropKind.SmallCrate, new Float3(60f, 1f + g.Props.Props.Count * 0.01f, 60f), 0f, recordUndo: false);
                    Check(g.Spawn(SpawnCatalog.Find("prop_cube")!).Contains("limit"), "prop limit blocks spawning past " + SandboxGame.MaxProps);
                    g.ClearMap();
                }
                Check(_ballMaxY > 0.9f, $"bouncy ball bounces (peak {_ballMaxY:0.00} m after landing)");
                WorldsRoundTrip(g);
                JumpArcTest(g);
                TownTest(g);
                RegressionTests(g);
                ConsoleTest(g);
                AchievementsTest(g);
                {
                    g.ClearMap(notify: false); g.Combat.Clear();
                    Ragdoll npc = EntityCatalog.SpawnDummy(g, new Float3(-30f, 0f, 80f), 0f, recordUndo: false, team: NpcTeam.Hostile);
                    DummyBrain brain = npc.Pelvis.Go.GetComponent<DummyBrain>()!;
                    Prop head = npc.Limbs.First(l => l.Go.Name.EndsWith(" Head")), torso = npc.Limbs[1];
                    float d1 = g.Combat.Hit(torso.Body, 24f, torso.Go.Transform.Position, "Pistol");
                    float d2 = g.Combat.Hit(head.Body, 24f, head.Go.Transform.Position, "Pistol");
                    bool hp = d1 == 24f && d2 == 48f && MathF.Abs(brain.Health - 28f) < 0.01f && !brain.Limp && g.Combat.NumberCount == 2;
                    int kills0 = g.Combat.Kills;
                    g.Combat.Hit(torso.Body, 40f, torso.Go.Transform.Position, "Pistol");
                    bool dead = brain.Limp && g.Combat.Kills == kills0 + 1 && g.Combat.FeedLines.Last().Contains("Criminal") && g.Combat.FeedLines.Last().Contains("Pistol");
                    bool noDouble = g.Combat.Hit(torso.Body, 40f, torso.Go.Transform.Position, "Pistol") == 0f && g.Combat.Kills == kills0 + 1;
                    Ragdoll npc2 = EntityCatalog.SpawnDummy(g, new Float3(-26f, 0f, 80f), 0f, recordUndo: false);
                    g.Combat.Explosion(npc2.Pelvis.Go.Transform.Position, 6f, 120f);
                    bool boom = npc2.Pelvis.Go.GetComponent<DummyBrain>()!.Limp && g.Combat.FeedLines.Last().Contains("Explosion");
                    bool nan = g.Combat.Hit(torso.Body, float.NaN, default, "x") == 0f && g.Combat.Hit(null, 50f, default, "x") == 0f;
                    g.Settings.DamageNumbers = false; int n0 = g.Combat.NumberCount;
                    Ragdoll npc3 = EntityCatalog.SpawnDummy(g, new Float3(-22f, 0f, 80f), 0f, recordUndo: false);
                    g.Combat.Hit(npc3.Limbs[1].Body, 10f, default, "SMG");
                    bool optional = g.Combat.NumberCount == n0; g.Settings.DamageNumbers = true;
                    for (int i = 0; i < 400; i++) g.Combat.Tick(1f / 60f);
                    Check(hp && dead && noDouble && boom && nan && optional && g.Combat.FeedLines.Count == 0,
                        $"NPC health: body/head damage, death → limp + kill feed, explosion damage, optional numbers (hp {brain.Health:0})");
                    g.ClearMap(notify: false);
                }
                {
                    g.ClearMap(notify: false);
                    g.Hints.Reset(g); g.Settings.ShowHints = true;
                    for (int i = 0; i < 120; i++) g.Hints.Tick(g, 1f / 30f);
                    string? first = g.Hints.Current;
                    for (int i = 0; i < 60 * 8; i++) g.Hints.Tick(g, 1f / 30f); // hint expires, gap, next
                    bool once = g.Settings.SeenHints.Count(x => x == "spawnmenu") == 1;
                    g.Settings.ShowHints = false; g.Hints.Dismiss();
                    for (int i = 0; i < 60 * 20; i++) g.Hints.Tick(g, 1f / 30f);
                    bool off = g.Hints.Current is null;
                    var js = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"SeenHints\":null}")!.Clamped();
                    Check(first is not null && first.Contains("spawn menu") && !first.Contains('{') && once && off && js.SeenHints is not null,
                        $"first-time hints: show once with live keybind names, can be disabled ('{first}')");
                    g.Hints.Reset(g); g.Settings.ShowHints = true;
                }
                {
                    float fogEnd0 = g.World.Fog.End, sun0 = g.Sun.Intensity;
                    bool ok = g.DevConsole.Execute(g, "weather storm") && g.Weather.Kind == WeatherKind.Storm;
                    for (int i = 0; i < 60 * 20; i++) g.Weather.Tick(g, 1f / 60f);
                    bool stormy = g.World.Fog.End < 100f && g.Weather.Bolts >= 1 && g.Weather.RainAmount > 0.9f && float.IsFinite(g.Sun.Intensity);
                    g.Weather.Set(WeatherKind.Clear, g);
                    for (int i = 0; i < 60 * 20; i++) g.Weather.Tick(g, 1f / 60f);
                    bool cleared = MathF.Abs(g.World.Fog.End - 284f) < 2f && g.Weather.RainAmount < 0.01f;
                    bool bad = !g.DevConsole.Execute(g, "weather tornado") && !g.DevConsole.Execute(g, "weather 99");
                    var meta = new SessionMeta(); meta.Modifiers.Weather = "Fog";
                    bool parse = WeatherSystem.Parse(meta.Modifiers.Weather) == WeatherKind.Fog && WeatherSystem.Parse("garbage") == WeatherKind.Clear && WeatherSystem.Parse(null) == WeatherKind.Clear;
                    Check(ok && stormy && cleared && bad && parse, $"weather: storm darkens/fogs/rains/lightning, clear restores, bad input rejected (fogEnd {fogEnd0:0}->{g.World.Fog.End:0})");
                }
                {
                    // View models: every rig builds with finite parts, firearms aim their sight onto the screen centre,
                    // springs survive a hitch, and clips/variants exist.
                    bool finite = true, aimOk = true, stable = true; string worst = "";
                    foreach (string id in new[] { "hands", "physgun", "toolgun", "pistol", "smg", "shotgun", "rpg", "grenade", "crowbar" })
                    {
                        ViewModelDef def = ViewModels.Create(id);
                        finite &= def.Parts.All(pt => float.IsFinite(pt.Size.X + pt.Size.Y + pt.Size.Z + pt.Offset.X + pt.Offset.Y + pt.Offset.Z)) && def.Parts.Count > 20;
                        var vm = new ViewModel(def, g.Player.CameraObject, g.Shader);
                        vm.SetVisible(true); vm.SetFov(75f, 75f);
                        for (int f = 0; f < 90; f++) vm.Update(1f / 60f, new VmInput(Float2.Zero, 0f, Aim: true));
                        if (def.CanAim && def.AimShift is null)
                        {
                            Float3 sp = g.Player.CameraObject.Transform.InverseTransformPoint(vm.Bones["sight"].Transform.Position);
                            if (MathF.Abs(sp.X) > 0.006f || MathF.Abs(sp.Y) > 0.006f || sp.Z < 0.05f) { aimOk = false; worst = $"{id} sight {sp.X:0.000},{sp.Y:0.000},{sp.Z:0.000}"; }
                        }
                        vm.Kick(5f); vm.Update(0.5f, new VmInput(new Float2(4000f, -4000f), 9f, Float2.UnitX, false, -30f, 20f, true, 1f));
                        for (int f = 0; f < 120; f++) vm.Update(1f / 60f, new VmInput(Float2.Zero, 0f));
                        Float3 rp0 = vm.Root.Transform.LocalPosition;
                        stable &= float.IsFinite(rp0.X + rp0.Y + rp0.Z) && Float3.Length(rp0) < 0.03f;
                        g.World.Remove(vm.Root);
                    }
                    var crow = ViewModels.Create("crowbar");
                    bool clipsOk = crow.Clips.ContainsKey("fire2") && ViewModels.Create("shotgun").Clips["reload_shell"].Hold && ViewModels.Create("grenade").Bones.Any(b => b.Bone == "pin");
                    Check(finite && aimOk && stable && clipsOk, $"view models: rigs build, ADS centres sights, springs settle, clip variants ({(worst == "" ? "ok" : worst)})");
                }
                {
                    var sg = g.Weapons.Get<ShotgunWeapon>();
                    g.Weapons.Select(g, sg);
                    sg.Mag = 2; sg.Reserve = 10;
                    sg.StartReload(g);
                    var none = new WeaponInput(false, false, false, false, false, false, 0f);
                    for (int f = 0; f < 70; f++) sg.Tick(g, none, 1f / 60f); // start + ~2 shells
                    int mid = sg.Mag;
                    sg.Tick(g, new WeaponInput(true, true, false, false, false, false, 0f), 1f / 60f); // fire interrupts
                    for (int f = 0; f < 120; f++) sg.Tick(g, none, 1f / 60f);
                    bool interrupted = !sg.Reloading && sg.Mag >= 3 && sg.Mag < 6 && sg.Mag + sg.Reserve == 12;
                    Check(mid >= 3 && interrupted, $"shotgun reloads shell by shell and firing interrupts it (mid {mid}, end {sg.Mag}/{sg.Reserve})");
                    g.Weapons.Select(g, g.Weapons.Get<PhysGunWeapon>());
                }
                {
                    // Prop protection: owners, world props, buddies, modes; owner survives a save round trip.
                    g.ClearMap(notify: false);
                    var pp = g.Props.Protection; string mode0 = pp.Mode;
                    var rp = g.Roleplay;
                    rp.Reset(new SessionMeta { Mode = "Roleplay" });
                    Prop mine = g.Props.Spawn(PropKind.Cube, new Float3(30f, 1f, 30f), 0f, recordUndo: false);
                    Prop world; using (g.Props.OwnedBy(PropProtection.World)) world = g.Props.Spawn(PropKind.Cube, new Float3(32f, 1f, 30f), 0f, recordUndo: false);
                    Prop theirs = g.Props.Spawn(PropKind.Cube, new Float3(34f, 1f, 30f), 0f, recordUndo: false); theirs.Owner = "alice";
                    pp.Mode = "rp";
                    bool rules = pp.CanTouch(mine, out _) && !pp.CanTouch(world, out string? w1) && w1!.Contains("map") && !pp.CanTouch(theirs, out _);
                    pp.AddBuddy("alice", PropProtection.LocalPlayer);
                    bool buddy = pp.CanTouch(theirs, out _) && !pp.CanTouch(mine, "alice", out _); // buddy lists are one-way
                    pp.RemoveBuddy("alice", PropProtection.LocalPlayer);
                    bool bob = !pp.CanTouch(mine, "bob", out _) && pp.AddBuddy(PropProtection.LocalPlayer, "bob") && pp.CanTouch(mine, "bob", out _) && !pp.AddBuddy(PropProtection.LocalPlayer, "bob");
                    pp.RemoveBuddy(PropProtection.LocalPlayer, "bob");
                    rp.Reset(null);
                    bool sandboxFree = pp.CanTouch(world, out _);
                    pp.Mode = "always"; bool always = !pp.CanTouch(world, out _); pp.Mode = "off"; bool off = pp.CanTouch(theirs, out _);
                    pp.Mode = mode0;
                    var data = MapSave.Capture(g);
                    bool saved = data.Props.Any(d => d.Owner == "alice") && data.Props.Any(d => d.Owner == "world") && data.Props.Count(d => d.Owner is null) >= 1;
                    Check(rules && buddy && bob && sandboxFree && always && off && saved, $"prop protection: owners, world props, buddies, modes, saved owners ({rules} {buddy} {bob} {sandboxFree} {always} {off} {saved})");
                    g.ClearMap(notify: false);
                }
                {
                    // Lottery (mayor) and car dealer.
                    var rp = g.Roleplay;
                    rp.Reset(new SessionMeta { Mode = "Roleplay", Modifiers = { StartingMoney = 5000 } });
                    string notMayor = rp.StartLottery();
                    rp.SetJob(RoleplaySystem.Jobs.First(j => j.Id == "mayor"));
                    var start = RoleplaySystem.Shop.First(b => b.Id == "rp_lottery"); var ticket = RoleplaySystem.Shop.First(b => b.Id == "rp_ticket");
                    string noLot = rp.Buy(ticket);
                    rp.Buy(start); rp.Buy(ticket); rp.Buy(ticket);
                    bool running = rp.LotteryRunning && rp.MyTickets == 2 && rp.LotteryPot == 300 && rp.Money == 5000 - 200 - 100;
                    rp.Tick(10f); int town = rp.TownTickets;
                    rp.LotteryDraw = n => 0; // first ticket = ours
                    int lotBefore = rp.Money, pot = rp.LotteryPot;
                    for (int k = 0; k < 10 && rp.LotteryRunning; k++) rp.Tick(RoleplaySystem.LotteryLength / 8f);
                    bool won = !rp.LotteryRunning && rp.Money >= lotBefore + pot && rp.LastLottery!.Contains("won");
                    rp.LotteryDraw = n => Random.Shared.Next(n);
                    var buggy = RoleplaySystem.Shop.First(b => b.Id == "car_buggy");
                    int full = rp.PriceOf(buggy);
                    rp.SetJob(RoleplaySystem.Jobs.First(j => j.Id == "cardealer"));
                    int cars0 = g.Vehicles.All.Count, m0 = rp.Money;
                    rp.Buy(buggy);
                    bool car = g.Vehicles.All.Count == cars0 + 1 && m0 - rp.Money == full * 6 / 10 && full == buggy.Price;
                    Check(notMayor.Contains("Mayor") && noLot.Contains("No lottery") && running && town >= 1 && won && car,
                        $"RP lottery (mayor) and car dealer (running {running}, town {town}, won {won}, car {car})");
                    foreach (var b in g.Vehicles.All.ToList()) g.Vehicles.Remove(b);
                    rp.Reset(null);
                }
                {
                    // Regression (soak seed 7): rope + elastic on the same pair must round-trip as 1 rope + 1 link.
                    g.ClearMap(notify: false);
                    Prop ra = g.Props.Spawn(PropKind.Cube, new Float3(10f, 1f, 10f), 0f, recordUndo: false), rb = g.Props.Spawn(PropKind.Cube, new Float3(12f, 1f, 10f), 0f, recordUndo: false);
                    g.ToolGun.Rope(ra, Float3.Zero, rb, Float3.Zero, 3f, recordUndo: false);
                    g.ToolGun.Elastic(ra, Float3.Zero, rb, Float3.Zero, 20f, recordUndo: false);
                    var d1 = MapSave.Capture(g);
                    Check(d1.Ropes.Count == 1 && d1.Links.Count(l => l.Type == "elastic") == 1, $"rope + elastic on one pair save as 1 rope + 1 elastic ({d1.Ropes.Count}, {d1.Links.Count})");
                    g.ClearMap(notify: false);
                }
                {
                    // Tutorial: steps complete from real actions, in order, and stop at the end.
                    g.ClearMap(notify: false);
                    var tut = g.Tutorial; tut.Reset(g);
                    tut.Tick(g, 0.1f); int s0 = tut.Index(g);
                    Prop tp = g.Props.Spawn(PropKind.Cube, new Float3(20f, 1f, 20f), 0f, recordUndo: false);
                    tut.Tick(g, 0.1f); int s1 = tut.Index(g);
                    tut.Tick(g, 0.1f); bool waits = tut.Index(g) == 1; // grabbing hasn't happened
                    g.Settings.TutorialStep = 2; tp.SetFrozen(true); tut.Tick(g, 0.1f); int s3 = tut.Index(g);
                    tut.Skip(g); tut.Tick(g, 0.1f);
                    Check((s0 == 0 || g.Achievements.Has("first_spawn")) && s1 >= 1 && waits && s3 == 3 && tut.Finished(g), $"tutorial steps advance from actions ({s0},{s1},{s3})");
                    g.ClearMap(notify: false);
                }
                {
                    // Kill plane: a ragdoll limb that tunnels out of the world takes its whole ragdoll with it.
                    g.ClearMap(notify: false);
                    var rd = Ragdoll.Spawn(g, new Float3(-40f, 1.2f, -40f), 0f, recordUndo: false);
                    rd.Limbs[3].Body.Transform.Position = new Float3(-40f, PhysicsGuard.KillY - 50f, -40f);
                    int fell0 = PhysicsGuard.FellOut;
                    PhysicsGuard.Clamp(g);
                    Check(PhysicsGuard.FellOut == fell0 + 1 && !Ragdoll.All.Contains(rd) && rd.Limbs.All(l => !l.Alive), "kill plane removes props that fall out of the world (whole ragdoll)");
                    g.ClearMap(notify: false);
                }
                {
                    var rp = g.Roleplay;
                    rp.Reset(new SessionMeta { Mode = "Roleplay", Modifiers = { StartingMoney = 500 } });
                    g.Health.Set(100f, 0f);
                    rp.TickHunger(RoleplaySystem.HungerSeconds / 2f);
                    bool half = MathF.Abs(rp.Hunger - 50f) < 0.5f && g.Health.Hp == 100f;
                    rp.TickHunger(RoleplaySystem.HungerSeconds / 2f + 0.01f); // empty; then 8 s of starving = 2 hits
                    for (int i = 0; i < 8; i++) rp.TickHunger(1f);
                    bool starving = rp.Starving && g.Health.Hp < 100f && g.Health.Hp >= 100f - 4 * RoleplaySystem.StarveDamage;
                    var burger = RoleplaySystem.Shop.First(b => b.Id == "rp_burger");
                    rp.Buy(burger);
                    bool ate = MathF.Abs(rp.Hunger - 45f) < 0.01f && rp.Money == 500 - burger.Price && !rp.Starving;
                    var save = new PlayerSave(); rp.Capture(save);
                    rp.Reset(new SessionMeta { Mode = "Roleplay" }); rp.Restore(save);
                    bool persisted = MathF.Abs(rp.Hunger - 45f) < 0.01f;
                    save.Hunger = float.NaN; rp.Restore(save);
                    Check(half && starving && ate && persisted && rp.Hunger == 100f, $"RP hunger: drains, starves, food restores, saves (hp {g.Health.Hp:0}, hunger {rp.Hunger:0})");
                    rp.Reset(null); g.Health.Set(100f, 0f);
                }
                {
                    g.ClearMap(notify: false);
                    var ents = SpawnCatalog.Entries.Where(e => e.Tab == MenuTab.Spawnlists).Take(15).ToArray();
                    foreach (var e in ents) g.Spawn(e);
                    g.Spawn(ents[3]);
                    var w = SpawnCatalog.Entries.FirstOrDefault(e => e.Tab == MenuTab.Weapons);
                    if (w is not null) g.Spawn(w);
                    Check(SpawnMenu.Recent.Count == SpawnMenu.RecentMax && SpawnMenu.Recent[0] == ents[3].Id && SpawnMenu.Recent.Distinct().Count() == SpawnMenu.Recent.Count
                        && (w is null || !SpawnMenu.Recent.Contains(w.Id)), "Recent spawnlist: newest first, deduped, capped, no weapons");
                    g.ClearMap(notify: false);
                    g.Weapons.Select(g, g.Weapons.All[0]);
                }
                MarkersTest(g);
                _done = true;
                ExitCode = _failures.Count == 0 ? 0 : 1;
                Console.WriteLine(_failures.Count == 0
                    ? "Sandbox self-test passed."
                    : $"Sandbox self-test FAILED ({_failures.Count}): {string.Join("; ", _failures)}");
                break;
        }
    }

    /// <summary>RP town: doors (open/buy/lock/pick), ATM bank, arrest → jail cell → jailbreak, and door state save.</summary>

    /// <summary>Jump arcs on Flatgrass and the town at 30 and 144 FPS: smooth rise/fall, no snaps, clean landing.</summary>
    private void JumpArcTest(SandboxGame g)
    {
        var peaks = new List<float>();
        foreach (string map in new[] { "flatgrass", "town" })
        {
            g.ClearMap(notify: false);
            MapBuilder.Load(g.World, g.Shader, map, null);
            foreach (int fps in new[] { 30, 144 })
            {
                float dt = 1f / fps;
                g.Player.Teleport(map == "town" ? new Float3(-32f, 0.05f, 12f) : new Float3(-4f, 0.05f, -20f));
                for (int i = 0; i < fps / 2; i++) { g.World.Physics.Update(); g.Player.Update(dt, false); }
                float y0 = g.Player.FeetPosition.Y, peak = y0, maxStep = 0f, prev = y0; int landedAt = -1, reversals = 0; float lastDy = 0f;
                g.Player.JumpRequested = true;
                for (int i = 0; i < fps * 2; i++)
                {
                    g.World.Physics.Update(); g.Player.Update(dt, false);
                    float y = g.Player.FeetPosition.Y, dy = y - prev; prev = y;
                    maxStep = MathF.Max(maxStep, MathF.Abs(dy));
                    if (dy * lastDy < 0f && MathF.Abs(dy) > 1e-4f) reversals++;
                    if (MathF.Abs(dy) > 1e-4f) lastDy = dy;
                    peak = MathF.Max(peak, y);
                    if (landedAt < 0 && i > 3 && g.Player.IsGrounded && y - y0 < 0.05f) landedAt = i;
                }
                float yEnd = g.Player.FeetPosition.Y;
                peaks.Add(peak - y0);
                // A 6.5 m/s jump can't move more than ~6.5*dt per frame; reversals: one at the apex only.
                Check(peak - y0 > 0.9f && peak - y0 < 1.3f && maxStep < 7f * dt + 0.02f && reversals <= 1 && landedAt > 0 && MathF.Abs(yEnd - y0) < 0.03f,
                    $"smooth jump on {map} at {fps} fps (peak {peak - y0:0.00} m, max step {maxStep:0.000}, reversals {reversals}, landed frame {landedAt}, end dy {yEnd - y0:0.000})");
            }
        }
        Check(peaks.Count == 4 && peaks.Max() - peaks.Min() < 0.05f, $"jump height is the same at 30 and 144 fps ({string.Join(" / ", peaks.Select(p => p.ToString("0.000")))} m)");
        g.ClearMap(notify: false);
        MapBuilder.Load(g.World, g.Shader, "flatgrass", null);
    }

    private void TownTest(SandboxGame g)
    {
        g.ClearMap(notify: false);
        MapBuilder.Load(g.World, g.Shader, "town", null);
        g.Roleplay.Reset(new SessionMeta { Mode = "Roleplay", Map = "town" });
        Check(Town.Active && Town.Doors.Count >= 8 && Town.Atm is not null && Town.Board is not null && Town.Waypoints.Count > 40,
            $"town map builds ({Town.Doors.Count} doors, {Town.Waypoints.Count} waypoints, ATM, laws board)");
        {
            // Stand on the RP grass (away from roads and buildings) for 4 s: height must stay put and no damage.
            Float3 spot = new(-32f, 0.05f, 12f);
            g.Player.Teleport(spot);
            float hp0 = g.Health.Hp, minY = 99f, maxY = -99f;
            for (int i = 0; i < 360; i++)
            {
                float dt = i % 3 == 0 ? 1f / 240f : i % 3 == 1 ? 1f / 25f : 1f / 144f; // uneven frame times
                if (i % 2 == 0) g.World.Physics.Update();
                g.Player.Update(dt, allowInput: false);
                g.Health.Tick(g, dt);
                if (Environment.GetEnvironmentVariable("GRASSDBG") == "1") Console.WriteLine($"grass {i} dt {dt:0.000} y {g.Player.FeetPosition.Y:0.000} vv {g.Player.VerticalVelocity:0.00} gr {g.Player.IsGrounded}");
                if (i > 30) { float y = g.Player.FeetPosition.Y; minY = MathF.Min(minY, y); maxY = MathF.Max(maxY, y); }
            }
            Check(maxY - minY < 0.05f && MathF.Abs(maxY) < 0.3f && g.Health.Hp >= hp0 && g.Player.IsGrounded,
                $"player stands still on the town grass (y {minY:0.000}..{maxY:0.000}, hp {hp0:0}->{g.Health.Hp:0})");
        }
        {
            // Sidewalk ramps are real slopes: a ray down the middle of the out-of-town ramp lands on the slope, not the grass.
            bool hitR = g.World.Physics.Raycast(new Float3(84.1f, 2f, 6.5f), new Float3(0f, -1f, 0f), 4f, out RaycastHit rh);
            bool hitP = g.World.Physics.Raycast(new Float3(16.2f, 2f, -6.5f), new Float3(0f, -1f, 0f), 4f, out RaycastHit ph);
            Check(hitR && MathF.Abs(rh.Point.Y - 0.08f) < 0.03f && hitP && MathF.Abs(ph.Point.Y - 0.1225f) < 0.03f,
                $"sidewalk ramps have sloped collision (out {(hitR ? rh.Point.Y : -1f):0.000}, plaza {(hitP ? ph.Point.Y : -1f):0.000})");
        }
        {
            // Zero/NaN normals on human details blacked out the whole HDR frame (v0.16.1).
            int bad = 0;
            foreach (string limb in new[] { "Head", "Torso", "Pelvis", "Upper Arm L", "Forearm R", "Thigh L", "Shin R" })
                foreach (Humans.Detail d in Humans.Limb(limb).Details)
                    bad += d.Mesh.Normals?.Count(n => !float.IsFinite(n.X + n.Y + n.Z) || Float3.Length(n) < 0.5f) ?? 0;
            Check(bad == 0, $"human detail meshes have valid normals ({bad} bad)");
            var badNames = new List<string>(); int scanned = 0;
            foreach (var go in g.World.AllObjects.ToList())
                if (go.GetComponent<MeshRenderer>() is { } mr && mr.Mesh.Res is { } me && me.Normals is { Length: > 0 } nn)
                {
                    scanned++;
                    if (nn.Any(n => !float.IsFinite(n.X + n.Y + n.Z) || Float3.Length(n) < 0.5f)) badNames.Add(go.Name);
                }
            Check(badNames.Count == 0, $"no world mesh has zero/NaN normals ({scanned} scanned; bad: {string.Join(", ", badNames.Distinct().Take(8))})");
        }
        Door house = Town.Find("door_house1")!;
        Float3 c = house.Transform.Position;
        bool Blocked() => g.World.Physics.Raycast(new Float3(c.X, 1.2f, c.Z - 2f), new Float3(0f, 0f, 1f), 4f, out RaycastHit h) && Town.DoorFrom(h) == house;
        bool any = g.World.Physics.Raycast(new Float3(c.X, 1.2f, c.Z - 2f), new Float3(0f, 0f, 1f), 4f, out RaycastHit dbg);
        Check(Blocked(), $"closed door blocks the doorway (door at {c.X:0.0},{c.Y:0.0},{c.Z:0.0}; hit {(any ? dbg.Transform?.GameObject.Name + " @" + dbg.Point.Z.ToString("0.00") : "nothing")})");
        string refused = g.Roleplay.UseDoor(house);
        Check(house.Locked && !house.Open && Blocked() && refused.Contains("for sale") && house.Status.Contains("F2"), $"unowned for-sale door is locked to everyone ({refused})");
        int m0 = g.Roleplay.Money;
        string buy = g.Roleplay.BuyOrSellDoor(house);
        Check(house.Owner == "you" && g.Roleplay.Money == m0 - house.Price, $"buy a house door ({buy})");
        g.Roleplay.UseDoor(house);
        bool hitPlane = g.World.Physics.Raycast(new Float3(c.X, 1.2f, c.Z - 2f), new Float3(0f, 0f, 1f), 4f, out RaycastHit oh) && MathF.Abs(oh.Point.Z - c.Z) < 0.3f;
        Check(house.Open && !Blocked() && !hitPlane, "owner opens the door with E (doorway physically clear)");
        g.Roleplay.UseDoor(house);
        Check(!house.Open && Blocked(), "E closes it again");
        g.Roleplay.ToggleLock(house);
        Check(house.Locked && g.Roleplay.UseDoor(house).StartsWith("Opened") && !house.Locked, "owner locks, then unlocks with E");
        {
            Ragdoll friend = EntityCatalog.SpawnDummy(g, g.Player.FeetPosition + new Float3(2f, 0f, 0f), 0f, false, NpcTeam.Citizen);
            string gave = g.Roleplay.GiveKey(house, false);
            bool has = house.SharedKeys.Contains(friend.Name);
            string back = g.Roleplay.GiveKey(house, true);
            Check(has && house.SharedKeys.Count == 0 && back.StartsWith("Took back"), $"owner gives a nearby NPC a door key and takes it back ({gave} / {back})");
        }
        Door store = Town.Find("door_generalstore")!;
        Check(g.Roleplay.BuyOrSellDoor(store).Contains("isn't for sale") && g.Roleplay.ToggleLock(store).Contains("keys"), "city doors can't be bought or locked");
        Door cell = Town.Find("door_cell")!;
        Check(cell.Locked && g.Roleplay.UseDoor(cell).Contains("locked"), "jail cell is locked to citizens");
        {
            string Kit() => string.Join(",", g.Weapons.All.Where(w => w.Available(g)).Select(w => w.Id.Replace("weapon_", "")).OrderBy(x => x));
            string citizen = Kit();
            g.Roleplay.SetJob(RoleplaySystem.Jobs.First(j => j.Id == "police"));
            string police = Kit();
            g.Roleplay.SetJob(RoleplaySystem.Jobs[0]);
            g.Weapons.Select(g, g.Weapons.Get<SmgWeapon>());
            bool blocked = g.Weapons.Current is not SmgWeapon;
            Prop ship = g.Roleplay.SpawnShipment(new Float3(-30f, 0.5f, 12f), "weapon_shotgun");
            g.Roleplay.Use(ship);
            bool bought = g.Weapons.Get<ShotgunWeapon>().Available(g) && g.Weapons.Current is ShotgunWeapon;
            Check(citizen == "hands,physgun,toolgun" && police == "baton,hands,physgun,pistol,toolgun" && blocked && bought,
                $"RP loadout: citizen [{citizen}], police [{police}], SMG blocked {blocked}, shipment shotgun {bought}");
            g.Roleplay.OwnedWeapons.Clear(); g.Weapons.Select(g, g.Weapons.Get<PhysGunWeapon>());
        }
        g.Roleplay.SetJob(RoleplaySystem.Jobs.First(j => j.Id == "police"));
        Check(g.Roleplay.CanUnlock(cell) && !g.Roleplay.CanUnlock(Town.Find("door_house2")!), "police hold keys to the station doors, not to for-sale houses");
        g.Roleplay.SetJob(RoleplaySystem.Jobs[0]);
        {
            Door h2 = Town.Find("door_house2")!;
            string pick = g.Roleplay.PickDoor(h2);
            Check(h2.Open && !h2.Locked, $"lockpick opens a for-sale door ({pick})");
            h2.SetOpen(false); h2.Locked = true; g.Roleplay.ClearWanted();
        }
        g.Roleplay.PickDoor(cell);
        Check(!cell.Locked && cell.Open && g.Roleplay.Wanted, "lockpicking the police cell unlocks it and makes you wanted");
        g.Roleplay.Arrest("Test arrest");
        Check(g.Roleplay.Jailed && Town.InCell(g.Player.FeetPosition) && cell.Locked && !cell.Open && !g.Roleplay.Wanted, "arrest puts you in the locked jail cell");
        g.Player.Teleport(Town.ReleasePoint);
        g.Roleplay.Tick(0.1f);
        Check(!g.Roleplay.Jailed && g.Roleplay.Wanted && g.Roleplay.WantedFor == "jailbreak", "leaving the cell early is a jailbreak (wanted)");
        g.Roleplay.Earn(500);
        int wallet = g.Roleplay.Money;
        g.Roleplay.Deposit(300); g.Roleplay.Withdraw(100);
        Check(g.Roleplay.Bank == 200 && g.Roleplay.Money == wallet - 200, "ATM deposit / withdraw");
        g.Roleplay.ToggleLock(house);
        var ps = new PlayerSave();
        g.Roleplay.Capture(ps);
        MapBuilder.Load(g.World, g.Shader, "town", null);
        g.Roleplay.Reset(new SessionMeta { Mode = "Roleplay", Map = "town" });
        g.Roleplay.Restore(ps);
        Check(Town.Find("door_house1") is { Owner: "you", Locked: true } && g.Roleplay.Bank == 200, "owned/locked doors and bank survive save/load");
        g.Roleplay.Reset(null);
        MapBuilder.Load(g.World, g.Shader, "flatgrass", null);
    }

    /// <summary>Worlds: create → play → autosave (atomic + .bak) → reload through the async loader; plus the roleplay economy.</summary>
    private void WorldsRoundTrip(SandboxGame g)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sandbox-selftest-{Environment.ProcessId}");
        Worlds.Root = root;
        try
        {
            var meta = new SessionMeta { Name = "Test/World:1", Mode = "Roleplay" };
            meta.Modifiers.Gravity = 5f; meta.Modifiers.StartingMoney = 1234; meta.Modifiers.Npcs = false;
            WorldEntry we = Worlds.Create(meta);
            Check(Directory.Exists(we.Folder) && File.Exists(Path.Combine(we.Folder, "session.json")) && !Path.GetFileName(we.Folder).Contains('/'),
                $"world folder created ({Path.GetFileName(we.Folder)})");

            void Load(WorldEntry w)
            {
                g.EnterSession(w);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (g.Loader is not null && sw.Elapsed.TotalSeconds < 15) { g.TickLoader(0.02f); Thread.Sleep(1); }
            }
            Load(we);
            Check(g.Session == we && g.Menus.Screen == Screen.Playing && MathF.Abs(g.World.Physics.Gravity.Y + 5f) < 0.01f && g.Roleplay.Money == 1234,
                $"new world loads with its modifiers (gravity {g.World.Physics.Gravity.Y:0.0}, money {g.Roleplay.Money})");

            Prop a = g.Props.Spawn(PropKind.Cube, new Float3(4f, 30f, 4f), 20f, recordUndo: false);
            a.Body.LinearVelocity = new Float3(1f, 2f, 3f);
            Prop fz = g.Props.Spawn(PropKind.Plank, new Float3(-4f, 3f, 4f), 45f, recordUndo: false);
            fz.SetFrozen(true);
            Prop chair = ModelProps.Spawn(g, ModelProps.All.First(d => d.Id == "chair"), new Float3(-8f, 1f, 8f), 0f, recordUndo: false);
            Check(chair.Go.GetComponentsInChildren<MeshRenderer>(false).Count() >= 6 && chair.Go.GetComponent<Seat>() is not null && MathF.Abs(chair.Body.Mass - 6f) < 0.01f,
                $"model prop builds its parts ({chair.Go.GetComponentsInChildren<MeshRenderer>(false).Count()} parts) and seat");
            Prop ta = g.Props.Spawn(PropKind.Cube, new Float3(10f, 0.6f, 10f), 0f, recordUndo: false), tb = g.Props.Spawn(PropKind.Cube, new Float3(12f, 0.6f, 10f), 0f, recordUndo: false);
            g.ToolGun.NoCollide(ta, tb);
            g.ToolGun.Elastic(ta, Float3.Zero, tb, Float3.Zero, 50f);
            g.ToolGun.Hinge(ta, new Float3(0.5f, 0f, 0f), Float3.UnitY, tb);
            Hoverball.Attach(g.Props, ta, ta.Go.Transform.Position, Float3.UnitY, 1f, KeyCode.Keypad8, KeyCode.Keypad2);
            Emitter.Attach(g.Props, tb, tb.Go.Transform.Position, Float3.UnitY, 5f, KeyCode.Keypad5);
            int before = g.Props.Props.Count;
            string st = g.ToolGun.Stack(chair, Float3.UnitY, 3, true);
            Check(g.Props.Props.Count == before + 3 && g.Props.Props.Skip(before).All(p => p.Entity == "mdl_chair"), $"stacker stacks 3 copies ({st})");
            {
                // Dupes: copy a welded 3-prop contraption, paste it, save it as a file, list it.
                Prop d1 = g.Props.Spawn(PropKind.Cube, new Float3(-40f, 0.6f, 40f), 0f, recordUndo: false), d2 = g.Props.Spawn(PropKind.Cube, new Float3(-39f, 0.6f, 40f), 0f, recordUndo: false), d3 = g.Props.Spawn(PropKind.Sphere, new Float3(-38f, 0.6f, 40f), 0f, recordUndo: false);
                g.ToolGun.Weld(d1, d2, recordUndo: false); g.ToolGun.Weld(d2, d3, recordUndo: false);
                MapSave.SaveData dupe = Dupes.Copy(g, d1);
                Check(dupe.Props.Count == 3 && dupe.Welds.Count >= 2, $"duplicator copies the whole contraption ({dupe.Props.Count} props, {dupe.Welds.Count} welds)");
                int n0 = g.Props.Props.Count;
                List<Prop> made = Dupes.Paste(g, dupe, new Float3(-40f, 0f, 30f), 90f);
                Check(made.Count == 3 && made.All(p => MathF.Abs(p.Go.Transform.Position.Z - 30f) < 1.6f) && made.Count(p => p.Go.GetComponent<FixedAngleConstraint>().IsValid()) >= 2,
                    "pasted dupe lands at the aim point, rotated, still welded");
                g.Props.Undo();
                Check(g.Props.Props.Count == n0, "one undo removes the whole pasted dupe");
                string path = Dupes.Save("Test Rig", dupe);
                Check(File.Exists(path) && Dupes.List().Any(x => x.File.Name == "Test Rig" && x.File.Props == 3) && !Worlds.List().Any(w => w.Folder.Contains("dupes")),
                    "dupe saved to file and listed in the dupes browser");
                var lists = new UserSpawnlists(Path.Combine(Worlds.Root, "spawnlists.json"));
                lists.Add("Builds", "prop_chair"); lists.Add("Builds", "prop_table"); lists.Add("Builds", "prop_chair");
                var re = new UserSpawnlists(lists.Path);
                Check(re.Lists.TryGetValue("Builds", out var ids) && ids.Count == 2, "user spawnlist persists without duplicates");
            }
            g.SitIn(chair);
            Check(ReferenceEquals(g.SeatedIn, chair), "E on a seat sits the player in it");
            g.StandUp();
            Check(g.SeatedIn is null && g.Player.FeetPosition.Y > -5f, "standing up puts the player back on the ground");
            g.Roleplay.SetJob(RoleplaySystem.Jobs.First(j => j.Id == "police"));
            g.Roleplay.Earn(100, null);
            g.SaveSession(sync: true);
            g.SaveSession(sync: true); // second write leaves a .bak behind
            Check(File.Exists(Path.Combine(we.Folder, "world.json")) && File.Exists(Path.Combine(we.Folder, "world.json.bak"))
                  && !Directory.EnumerateFiles(we.Folder, "*.tmp").Any(), "autosave writes atomically and keeps one backup");

            // Corrupt the main file: the loader must fall back to the backup.
            File.WriteAllText(Path.Combine(we.Folder, "world.json"), "{ not json");
            var reloaded = Worlds.List().First(x => x.Folder == we.Folder);
            g.ExitSession(Screen.Worlds);
            Check(g.Session is null && g.World.Physics.Gravity.Y < -9f, "exit restores the menu world");
            Load(reloaded);
            Prop? ra = g.Props.Props.FirstOrDefault(p => p.Kind == PropKind.Cube && p.Go.Transform.Position.Y > 20f);
            Prop? rf = g.Props.Props.FirstOrDefault(p => p.Kind == PropKind.Plank);
            Check(ra is not null && Float3.Length(ra.Body.LinearVelocity - new Float3(1f, 2f, 3f)) < 0.6f,
                $"reload restores exact velocity from backup ({(ra is null ? "missing" : $"{ra.Body.LinearVelocity.X:0.0},{ra.Body.LinearVelocity.Y:0.0},{ra.Body.LinearVelocity.Z:0.0}")})");
            Prop? rc = g.Props.Props.FirstOrDefault(p => p.Entity == "mdl_chair");
            int links = g.Props.Props.Sum(p => p.Go.GetComponents<NoCollideLink>().Count() + p.Go.GetComponents<ElasticBand>().Count() + p.Go.GetComponents<HingeJoint>().Count());
            int gadgets = g.Props.Props.Sum(p => p.Go.GetComponents<Hoverball>().Count() + p.Go.GetComponents<Emitter>().Count());
            Check(links >= 3 && gadgets == 2 && g.Props.Props.Any(p => p.Go.GetComponent<Hoverball>() is { Up: KeyCode.Keypad8 }),
                $"no-collide, elastic, hinge, hoverball (numpad keys) and emitter survive save/load ({links} links, {gadgets} gadgets)");
            Check(rc is not null && rc.Go.GetComponentsInChildren<MeshRenderer>(false).Count() >= 6 && rc.Go.GetComponent<Seat>() is not null, "model prop survives save/load with its parts");
            Check(rf is { Frozen: true } && MathF.Abs(rf.Go.Transform.Position.Y - 3f) < 0.05f, "frozen prop stays frozen in place after reload");
            Check(g.Roleplay.Job.Id == "police" && g.Roleplay.Money == 1334 && g.Weapons.All.Any(w => w is BatonWeapon && w.Available(g)),
                $"player job, money and job loadout restored ({g.Roleplay.Job.Id}, ${g.Roleplay.Money})");
            bool spent = g.Roleplay.TrySpend(5000, "test");
            Check(!spent && g.Roleplay.Money == 1334 && g.Roleplay.TrySpend(334, "test") && g.Roleplay.Money == 1000, "wallet refuses overspending and charges purchases");

            var dup = Worlds.Duplicate(reloaded);
            var ren = Worlds.Rename(dup, "Renamed copy");
            Check(Worlds.List().Count == 2 && ren.Meta.Name == "Renamed copy" && File.Exists(Path.Combine(ren.Folder, "world.json")), "duplicate + rename");
            g.ExitSession(Screen.Worlds);
            Worlds.Delete(ren);
            Check(Worlds.List().Count == 1, "delete removes the world");
            var steps = new SessionLoader(g, reloaded);
            int frames = 0; string last = "";
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!steps.Step(0.016f) && clock.Elapsed.TotalSeconds < 15) { Thread.Sleep(1); frames++; if (steps.Stage != last) { last = steps.Stage; Console.WriteLine($"  loader {clock.ElapsedMilliseconds} ms f{frames}: {last} {steps.Progress:0.00}"); } }
            Check(frames > 5 && steps.Error is null, $"loader spreads work over frames ({frames} frames)");
            g.ExitSession(Screen.Playing);
        }
        catch (Exception e) { Check(false, $"worlds round trip threw {e.GetType().Name}: {e.Message}"); }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>Writes a Blender-style glTF (a ground mesh + named empties), imports it as a custom map and checks every marker.</summary>
    private void MarkersTest(SandboxGame g)
    {
        string path = Path.Combine(Path.GetTempPath(), $"sandbox-markers-{Environment.ProcessId}.gltf");
        File.WriteAllText(path, MarkerGltf());
        string? err = MapBuilder.Load(g.World, g.Shader, "custom", path);
        Check(err is null, $"marker test map imports ({err ?? "ok"})");
        Check(MapBuilder.SpawnPoints.Count == 2 && Float3.Length(MapBuilder.SpawnPoint - new Float3(0f, 0.1f, 0f)) < 0.05f, $"spawn_* markers become spawn points ({MapBuilder.SpawnPoints.Count})");
        Check(Town.HasJail && Float3.Length(Town.CellCenter - new Float3(10f, 0f, -10f)) < 0.05f && MathF.Abs(Town.CellHalf.X - 3f) < 0.05f && MathF.Abs(Town.CellHalf.Z - 2f) < 0.05f,
            $"jail_cell marker sets the cell (centre {Town.CellCenter}, half {Town.CellHalf})");
        Check(Float3.Length(Town.ReleasePoint - new Float3(10f, 0.2f, -4f)) < 0.05f, "jail_release marker sets the release point");
        Check(Town.Atm is not null && Town.Board is not null && Town.Active, "atm and laws_board markers place working RP props");
        Door? shop = Town.Find("door_shop"), hq = Town.Find("door_police_hq"), cell = Town.Find("door_cell");
        Check(Town.Doors.Count == 3 && shop is { Price: 500, Owner: "" } && MathF.Abs(shop.Width - 1.5f) < 0.05f && hq is { Owner: "police" } && cell is { Locked: true, Owner: "police" },
            $"door_* markers make buyable / police / cell doors ({Town.Doors.Count})");
        Check(Town.Waypoints.Count == 2, "npc_path markers become NPC waypoints");
        Check(Water.Volumes.Count == 1 && MathF.Abs(Water.Volumes[0].Surface - 0.5f) < 0.05f, "water_* marker adds a buoyancy volume (surface = top of the box)");
        Check(MapBuilder.MarkerName("Spawn_B.001") == "spawn_b" && !MapBuilder.IsMarker("House"), "marker names ignore case and Blender .001 suffixes");
        MapBuilder.Load(g.World, g.Shader, "flatgrass", null);
        Check(!Town.Active && Water.Volumes.Count == 0 && MapBuilder.SpawnPoints.Count == 0, "loading another map clears marker data");
        try { File.Delete(path); } catch { }
    }

    public static string MarkerGltf()
    {
        // Ground quad, 40 x 40 m at y = 0. (The importer converts glTF's right-handed axes, so glTF +Z lands on Prowl -Z.)
        float[] pos = [-20f, 0f, -20f, 20f, 0f, -20f, 20f, 0f, 20f, -20f, 0f, 20f];
        ushort[] idx = [0, 2, 1, 0, 3, 2];
        var bytes = new List<byte>();
        foreach (float f in pos) bytes.AddRange(BitConverter.GetBytes(f));
        foreach (ushort u in idx) bytes.AddRange(BitConverter.GetBytes(u));
        string b64 = Convert.ToBase64String(bytes.ToArray());
        float s45 = MathF.Sin(MathF.PI / 4f), c45 = MathF.Cos(MathF.PI / 4f);
        string N(string name, float x, float y, float z, string extra = "") => $"{{\"name\":\"{name}\",\"translation\":[{x},{y},{z}]{extra}}}";
        string[] nodes =
        [
            "{\"name\":\"Ground\",\"mesh\":0}",
            N("spawn_a", 0, 0, 0), N("Spawn_B.001", 5, 0, 0),
            N("jail_cell", 10, 0, 10, ",\"scale\":[3,1,2]"), N("jail_release", 10, 0, 4),
            N("atm", -5, 0, 5, $",\"rotation\":[0,{s45},0,{c45}]"), N("laws_board", 0, 0, 12),
            N("door_shop_p500", -8, 0, -8, ",\"scale\":[1.5,2.6,1]"), N("door_police_hq", 8, 0, -8), N("door_cell", 10, 0, 7.5f),
            N("npc_path_1", -10, 0, 0), N("npc_path_2", 10, 0, 0),
            N("water_pond", 15, -1f, -15, ",\"scale\":[4,1.5,4]"),
        ];
        string list = string.Join(",", Enumerable.Range(0, nodes.Length));
        return "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[" + list + "]}],\"nodes\":[" + string.Join(",", nodes) + "]," +
            "\"meshes\":[{\"name\":\"Ground\",\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\",\"min\":[-20,0,-20],\"max\":[20,0,20]},{\"bufferView\":1,\"componentType\":5123,\"count\":6,\"type\":\"SCALAR\"}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":48},{\"buffer\":0,\"byteOffset\":48,\"byteLength\":12}]," +
            $"\"buffers\":[{{\"byteLength\":60,\"uri\":\"data:application/octet-stream;base64,{b64}\"}}]}}";
    }

    /// <summary>One check per bug the soak test (src/Soak.cs) found, so none of them can come back.</summary>
    /// <summary>Console commands drive the real systems; bad input reports an error instead of throwing; PNG writer round-trips.</summary>
    private void ConsoleTest(SandboxGame g)
    {
        g.ClearMap(notify: false);
        GameConsole c = g.DevConsole;
        bool n0 = g.Player.Noclip;
        Check(c.Execute(g, "noclip") && g.Player.Noclip != n0, "console: noclip toggles");
        if (g.Player.Noclip != n0) c.Execute(g, "/noclip");
        Check(c.Execute(g, "time 18") && MathF.Abs(g.DayNight.TimeOfDay - 0.75f) < 0.001f, "console: time 18 sets dusk");
        Check(c.Execute(g, "gravity 3.5") && MathF.Abs(g.World.Physics.Gravity.Y + 3.5f) < 1e-4f, "console: gravity sets world gravity");
        c.Execute(g, "gravity 9.81");
        Check(c.Execute(g, "spawn barrel 3") && g.Props.Props.Count(p => p.Kind == PropKind.Barrel) == 3, "console: spawn barrel 3");
        Check(c.Execute(g, "give shotgun") && g.Weapons.Current.Id == "weapon_shotgun", "console: give shotgun equips it");
        c.Execute(g, "give physgun");
        Check(c.Execute(g, "cleanup") && g.Props.Props.Count == 0, "console: cleanup removes props");
        int logN = c.Log.Count;
        bool bad = !c.Execute(g, "gravity banana") && !c.Execute(g, "tp 1 NaN 3") && !c.Execute(g, "/nosuchcmd") && !c.Execute(g, "spawn limb") && !c.Execute(g, "give laser");
        Check(bad && Player.IsFinite(g.Player.FeetPosition) && c.Log.Count >= logN + 5, "console: bad arguments/unknown commands print errors, never throw");
        c.Execute(g, "hello world");
        Check(c.Log[^1].text.EndsWith(": hello world"), "console: plain text is chat");
        for (int i = 0; i < 300; i++) c.Execute(g, "echo spam " + i);
        Check(c.Log.Count <= GameConsole.MaxLog, $"console: log capped at {GameConsole.MaxLog}");
        g.Settings.TimeScale = 1f;

        string dir = Path.Combine(Path.GetTempPath(), $"sandbox shots ü {Environment.ProcessId}");
        string png = Path.Combine(dir, "photo 1.png");
        byte[] px = new byte[4 * 3 * 4];
        px[0] = 255; px[3] = 255; // bottom-left red (GL bottom-up)
        bool ok = true;
        try { SandboxGame.WritePng(png, px, 4, 3); using var img = new ImageMagick.MagickImage(png); ok = img.Width == 4 && img.Height == 3; }
        catch (Exception e) { ok = false; Console.WriteLine(e); }
        Check(ok && File.Exists(png) && !File.Exists(png + ".tmp"), "screenshot PNG writer handles unicode/space paths");
        string next = SandboxGame.NextScreenshotPath(new DateTime(2026, 1, 2, 3, 4, 5));
        Check(next.EndsWith("sandbox_2026-01-02_03-04-05.png"), "screenshot names are timestamped");
        try { Directory.Delete(dir, true); } catch { }
    }

    /// <summary>Achievements unlock once from events and polled state, persist, reload, and survive a corrupt file.</summary>
    private void AchievementsTest(SandboxGame g)
    {
        string old = Achievements.FilePath;
        string dir = Path.Combine(Path.GetTempPath(), $"sandbox ach ü {Environment.ProcessId}");
        Achievements.FilePath = Path.Combine(dir, "achievements.json");
        try
        {
            var a = g.Achievements;
            a.Load();
            int fired = 0; void On(Achievement _) => fired++;
            a.OnUnlock += On;
            g.ClearMap(notify: false);
            g.Notify("Welded Cube #1 to Cube #2");
            g.DevConsole.Execute(g, "gravity 2");
            for (int i = 0; i < 26; i++) g.Props.Spawn(PropKind.Cube, new Float3(-30f + i % 6 * 1.2f, 0.6f + i / 6 * 1.2f, 70f), 0f, recordUndo: false);
            a.Tick(g, 1f);
            g.DevConsole.Execute(g, "gravity 9.81");
            bool got = a.Has("welder") && a.Has("hacker") && a.Has("mad_scientist") && a.Has("builder") && !a.Has("architect");
            Check(got && g.Hud.ActiveToasts.Any(t => t.StartsWith("Achievement unlocked")), $"achievements unlock from events + state ({string.Join(",", a.Unlocked)})");
            int before = fired;
            g.Notify("Welded again"); a.Tick(g, 1f);
            Check(fired == before, "achievements fire only once");
            a.OnUnlock -= On;
            int n = a.Unlocked.Count;
            a.Load();
            Check(a.Unlocked.Count == n && a.Has("welder"), "achievements persist and reload");
            File.WriteAllText(Achievements.FilePath, "{not json");
            bool ok = true; try { a.Load(); } catch { ok = false; }
            Check(ok && a.Unlocked.Count == 0, "corrupt achievements file loads as empty");
            g.ClearMap(notify: false);
        }
        finally { Achievements.FilePath = old; g.Achievements.Load(); try { Directory.Delete(dir, true); } catch { } }
    }

    private void RegressionTests(SandboxGame g)
    {
        g.ClearMap(notify: false);
        // 1. Round model props: the collider is configured detached, so the engine throws nothing.
        int thrown = 0;
        void Count(object? _, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) { if (e.Exception is ArgumentException) thrown++; }
        AppDomain.CurrentDomain.FirstChanceException += Count;
        ModelDef round = ModelProps.All.First(d => d.Round);
        Prop rp = ModelProps.Spawn(g, round, new Float3(-40f, 1f, 40f), 0f, recordUndo: false);
        ModelDef box = ModelProps.All.First(d => !d.Round);
        ModelProps.Spawn(g, box, new Float3(-42f, 1f, 40f), 0f, recordUndo: false);
        AppDomain.CurrentDomain.FirstChanceException -= Count;
        Check(thrown == 0 && rp.Go.GetComponent<CylinderCollider>() is { Enabled: true } && rp.Go.GetComponent<BoxCollider>() is null,
            $"model props build colliders without engine exceptions ({thrown} thrown)");

        // 2. NPC brain survives a removed or frozen limb (went NRE / static-body exception every physics step).
        Ragdoll npc = EntityCatalog.SpawnDummy(g, new Float3(-40f, 0f, 44f), 0f, recordUndo: false, team: NpcTeam.Friendly);
        g.Props.Remove(npc.Limbs[3]);
        DummyBrain brain = npc.Pelvis.Go.GetComponent<DummyBrain>();
        thrown = 0;
        AppDomain.CurrentDomain.FirstChanceException += Count;
        void Any(object? _, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) => thrown++;
        AppDomain.CurrentDomain.FirstChanceException += Any;
        brain.FixedUpdate();
        Ragdoll npc2 = EntityCatalog.SpawnDummy(g, new Float3(-36f, 0f, 44f), 0f, recordUndo: false, team: NpcTeam.Friendly);
        npc2.Limbs[0].SetFrozen(true);
        DummyBrain b2 = npc2.Pelvis.Go.GetComponent<DummyBrain>();
        b2.FixedUpdate();
        AppDomain.CurrentDomain.FirstChanceException -= Any;
        AppDomain.CurrentDomain.FirstChanceException -= Count;
        Check(thrown == 0 && brain.Limp && b2.Limp, $"NPC brain goes limp instead of throwing when a limb is removed or frozen ({thrown} thrown)");

        // 3. Rope line objects leave with their props (leaked one GameObject per rope per clear).
        g.ClearMap(notify: false);
        Prop r1 = g.Props.Spawn(PropKind.Cube, new Float3(-40f, 1f, 48f), 0f, recordUndo: false), r2 = g.Props.Spawn(PropKind.Cube, new Float3(-37f, 1f, 48f), 0f, recordUndo: false);
        g.ToolGun.Rope(r1, Float3.Zero, r2, Float3.Zero);
        int ropes() => g.World.AllObjects.Count(o => o.Name == "Rope");
        int during = ropes();
        g.ClearMap(notify: false);
        Check(during >= 1 && ropes() == 0, $"rope visuals are removed with their props ({during} -> {ropes()})");

        // 4. An unfrozen button stays unfrozen through save/load (buttons froze themselves on load).
        Prop btn = EntityCatalog.SpawnButton(g, new Float3(-40f, 0.3f, 52f), 0f);
        btn.SetFrozen(false);
        string bp = Path.Combine(Path.GetTempPath(), $"sandbox-btn-{Environment.ProcessId}.json");
        MapSave.Save(g, bp); MapSave.Load(g, bp);
        Check(g.Props.Props.Single(p => p.Entity == "ent_button") is { Frozen: false }, "unfrozen button stays unfrozen after save/load");
        try { File.Delete(bp); } catch { }

        // 5. Physics guard caps runaway speeds; frozen vehicles can't be flipped (threw on a static body).
        g.ClearMap(notify: false);
        Prop fast = g.Props.Spawn(PropKind.Cube, new Float3(-40f, 5f, 56f), 0f, recordUndo: false);
        fast.Body.LinearVelocity = new Float3(5000f, 0f, 0f); fast.Body.AngularVelocity = new Float3(0f, 900f, 0f);
        PhysicsGuard.Clamp(g);
        Check(Float3.Length(fast.Body.LinearVelocity) <= PhysicsGuard.MaxSpeed + 0.01f && Float3.Length(fast.Body.AngularVelocity) <= PhysicsGuard.MaxSpin + 0.01f, "physics guard caps runaway speed and spin");
        Buggy fb = g.Vehicles.SpawnBuggy(new Float3(-44f, 1f, 56f), 0f, recordUndo: false);
        fb.Body.MotionType = Jitter2.Dynamics.MotionType.Static;
        bool flipOk = true; try { fb.Flip(); } catch { flipOk = false; }
        Check(flipOk, "flipping a frozen vehicle does nothing instead of throwing");
        // 5c. Ragdoll no-collide pairs are released when it is removed (engine kept them forever -> GameObject leak).
        g.ClearMap(notify: false);
        int pairs0 = CollisionPairs.EnginePairs;
        Ragdoll.Spawn(g, new Float3(-40f, 1.2f, 64f), 0f, recordUndo: false);
        int pairsWith = CollisionPairs.EnginePairs;
        g.ClearMap(notify: false);
        Check(pairsWith > pairs0 && CollisionPairs.EnginePairs == pairs0 && CollisionPairs.TrackedBodies == 0, $"no-collide pairs released on removal ({pairs0}->{pairsWith}->{CollisionPairs.EnginePairs})");
        // 5d. Removing props frees their physics bodies (colliders re-attached to a static body and leaked before).
        g.ClearMap(notify: false);
        int bodies0 = g.World.Physics.World.RigidBodies.Count;
        for (int i = 0; i < 20; i++) g.Props.Spawn(PropKind.Cube, new Float3(-40f + i, 3f, 70f), 0f, recordUndo: false);
        Ragdoll.Spawn(g, new Float3(-40f, 1.2f, 74f), 0f, recordUndo: false);
        g.ClearMap(notify: false);
        Check(g.World.Physics.World.RigidBodies.Count == bodies0, $"removed props free their physics bodies ({bodies0} -> {g.World.Physics.World.RigidBodies.Count})");
        // 5a. A prop whose pose went NaN is removed before Jitter's next broadphase (threw on a worker thread in the 300 s soak).
        Prop nanProp = g.Props.Spawn(PropKind.Cube, new Float3(-40f, 5f, 60f), 0f, recordUndo: false);
        int removed0 = PhysicsGuard.Removed;
        nanProp.Body.MovePosition(new Float3(float.NaN, 0f, 0f));
        PhysicsGuard.Clamp(g);
        Check(PhysicsGuard.Removed == removed0 + 1 && !g.Props.Props.Contains(nanProp), "physics guard removes NaN-pose props");
        // 5b. A NaN player position (pushed by a degenerate contact) respawns instead of crashing the ground cast.
        Float3 savedFeet = g.Player.FeetPosition;
        g.Player.Teleport(new Float3(float.NaN, 1f, 0f));
        bool tpOk = Player.IsFinite(g.Player.FeetPosition);
        g.Player.RecoverFromNaN();
        bool nanOk = true; try { g.Player.Update(1f / 60f, false); } catch { nanOk = false; }
        Check(tpOk && nanOk && Player.IsFinite(g.Player.FeetPosition), "NaN teleport/position recovers to spawn without throwing");
        g.Player.Teleport(savedFeet);
        g.Vehicles.ClearAll();
        g.ClearMap(notify: false);

        // 6. World folders: same-name rename keeps the folder, Windows device names are avoided, unicode/spaces work,
        //    delete refuses sibling folders that merely share the prefix, concurrent saves never collide.
        string oldRoot = Worlds.Root;
        string root = Path.Combine(Path.GetTempPath(), $"sandbox wörlds 🚀 {Environment.ProcessId}");
        Worlds.Root = root;
        try
        {
            WorldEntry w = Worlds.Create(new SessionMeta { Name = "Mÿ Wörld" });
            WorldEntry same = Worlds.Rename(w, "Mÿ Wörld");
            Check(same.Folder == w.Folder && Directory.Exists(w.Folder), "renaming a world to its own name keeps its folder");
            WorldEntry con = Worlds.Create(new SessionMeta { Name = "con" });
            Check(!Path.GetFileName(con.Folder).Equals("con", StringComparison.OrdinalIgnoreCase), $"Windows device names are not used as folders ({Path.GetFileName(con.Folder)})");
            WorldEntry odd = Worlds.Create(new SessionMeta { Name = "a/b\\c:*?" + new string('x', 80) });
            Check(Directory.Exists(odd.Folder) && Path.GetFileName(odd.Folder).Length <= 48 && Worlds.List().Count == 3, "odd and over-long names make valid folders");
            string sibling = root + "2";
            Directory.CreateDirectory(sibling);
            bool refused = false;
            try { Worlds.Delete(new WorldEntry(sibling, new SessionMeta())); } catch (InvalidOperationException) { refused = true; }
            Check(refused && Directory.Exists(sibling), "delete refuses a sibling folder outside the saves root");
            try { Directory.Delete(sibling, true); } catch { }
            string target = Path.Combine(w.Folder, "world.json");
            var tasks = Enumerable.Range(0, 16).Select(i => Task.Run(() => Worlds.WriteAtomic(target, $"{{\"n\":{i}}}"))).ToArray();
            bool allOk = true; try { Task.WaitAll(tasks); } catch { allOk = false; }
            bool valid = Worlds.ReadJson<Dictionary<string, int>>(target) is { Count: 1 };
            Check(allOk && valid && !Directory.GetFiles(w.Folder, "*.tmp").Any(), "16 concurrent atomic saves never collide or leave temp files");
        }
        finally { Worlds.Root = oldRoot; try { Directory.Delete(root, true); } catch { } }

        // 7. The soak diff helper itself.
        Check(Soak.Diff(new MapSave.SaveData(), new MapSave.SaveData()) is null, "save diff of identical worlds is empty");
    }
}
