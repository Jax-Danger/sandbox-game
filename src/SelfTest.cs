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
    private Buggy? _buggy;
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
                    BonePose mid = reload.Sample("weapon", 0.5f), end = reload.Sample("weapon", reload.Length);
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
                break;
            case 720:
                {
                    Buggy b = _buggy!;
                    float moved = Float3.Length(b.Go.Transform.Position - _buggyStart);
                    Check(moved > 3f && b.SpeedKmh > 15f && b.SpeedKmh < 55f, $"throttle drives the buggy at a sane pace ({moved:0.0} m, {b.SpeedKmh:0} km/h, gear {b.Gear})");
                    Check(g.Vehicles.TryUse() && !g.InVehicle && Float3.Length(g.Player.FeetPosition - b.Go.Transform.Position) < 4f, "E exits beside the buggy");
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
                _done = true;
                ExitCode = _failures.Count == 0 ? 0 : 1;
                Console.WriteLine(_failures.Count == 0
                    ? "Sandbox self-test passed."
                    : $"Sandbox self-test FAILED ({_failures.Count}): {string.Join("; ", _failures)}");
                break;
        }
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
}
