using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public sealed partial class SandboxGame : Game
{
    public SelfTest? SelfTest { get; set; }

    /// <summary>Optional forced UI state for screenshots (see README "Screenshot mode").</summary>
    public string? ShotState { get; set; }
    /// <summary>Screenshot-mode view-model pose override (aim, sprint, crouch, reload, fire).</summary>
    public string? ShotPose { get; private set; }
    private bool _vmShotDone;
    private float _perfSum, _perfMax; private int _perfN;

    public Scene World { get; private set; } = null!;
    public PropManager Props { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public PhysGun PhysGun { get; private set; } = null!;
    public ToolGun ToolGun { get; private set; } = null!;
    public WeaponSystem Weapons { get; } = new();
    public SoundSystem Sounds { get; } = new();
    public VehicleSystem Vehicles { get; private set; } = null!;
    public PlayerAvatar Avatar { get; private set; } = null!;
    public Settings Settings { get; private set; } = new();
    public Menus Menus { get; } = new();
    public HealthSystem Health { get; } = new();
    public DayNightCycle DayNight { get; } = new();
    public WeatherSystem Weather { get; } = new();
    public Hints Hints { get; } = new();
    public Tutorial Tutorial { get; } = new();
    public Combat Combat => _combat ??= new Combat(this);
    private Combat? _combat;
    public RoleplaySystem Roleplay { get; private set; } = null!;
    public bool Flashlight { get; private set; }
    /// <summary>True while the main menu's live scene is showing (no session): hides the view model, orbits the camera.</summary>
    public bool MenuBackdrop { get; private set; }
    private SpotLight? _flashlight;
    private float _zoom, _frameStart;
    public bool AimingAtProp { get; private set; }
    public bool InVehicle => Vehicles.InVehicle;
    public Shader Shader { get; private set; } = null!;
    public DirectionalLight Sun { get; private set; } = null!;

    private readonly Hud _hud = new();
    public Hud Hud => _hud;
    private readonly IconRenderer _icons = new();
    private SpawnMenu _spawnMenu = null!;
    public ContextMenu Context { get; } = new();
    private bool _pointerOverContextUi;
    private Prop? _lastHighlight;
    /// <summary>Main-thread work queue; thread-safe so background tasks (autosave, loading) can post to it.</summary>
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pending = new();
    private int _frames, _shotToasts;

    /// <summary>True when a menu (spawn menu, main/pause/settings) owns the mouse.</summary>
    public bool MenuOpen => Menus.Blocking || _spawnMenu.Open || Context.Open || DevConsole.Open;
    public GameConsole DevConsole { get; } = new();
    /// <summary>Photo mode (F1): hides every HUD element so the world can be screenshotted clean.</summary>
    public bool PhotoMode { get; set; }

    public void Enqueue(Action a) => _pending.Enqueue(a);
    public void Notify(string? message, ToastKind? kind = null) { _hud.Notify(message, kind); if (message is not null) Achievements.Observe(message); }
    public Achievements Achievements { get; } = new();

    public override void Initialize()
    {
        Settings = Application.IsHeadless ? new Settings() : Settings.Load();
        if (!Application.IsHeadless) Window.StateChanged += st => _minimized = st == Silk.NET.Windowing.WindowState.Minimized;
        Achievements.Load();
        Achievements.OnUnlock += a => { _hud.Notify($"Achievement unlocked · {a.Name}", ToastKind.Success); Sounds?.Play("sfx_ui_click", 0.7f); };
        World = new Scene();
        World.Physics.UseMultithreading = false; // see EndUpdate: only big worlds use Jitter's worker pool
        Shader = Shader.LoadDefault(DefaultShader.Standard);
        Sun = MapBuilder.Build(World, Shader);
        // Atmosphere: procedural sky, soft linear distance fog matched to the horizon, hemisphere ambient.
        World.Skybox.Mode = Scene.SkyboxMode.Procedural;
        World.Fog.Mode = Scene.FogParams.FogMode.Linear;
        World.Fog.Color = new Color(0.72f, 0.80f, 0.88f, 1f);
        World.Fog.Start = 45f;
        World.Fog.End = 220f;
        World.Ambient.Mode = Scene.AmbientLightParams.AmbientMode.Hemisphere;
        World.Ambient.SkyColor = new Float4(0.55f, 0.65f, 0.80f, 1f);
        World.Ambient.GroundColor = new Float4(0.30f, 0.28f, 0.26f, 1f);
        World.Ambient.Strength = 0.9f;
        Props = new PropManager(World, Shader);
        Props.Protection.RoleplayActive = () => Roleplay?.Active == true;
        { var wgo = new GameObject("WaterSimulator"); wgo.AddComponent<WaterSimulator>().Game = this; World.Add(wgo); }
        Roleplay = new RoleplaySystem(this);
        DummyBrain.Game = this;
        Player = new Player(World, new Float3(0f, 0.1f, -10f), 0f);
        if (Environment.GetEnvironmentVariable("SANDBOX_SOLIDSKY") != "1") Player.Camera.ClearFlags = CameraClearFlags.Skybox;
        PhysGun = new PhysGun(Props);
        ToolGun = new ToolGun(Props);
        Vehicles = new VehicleSystem(this);
        Avatar = new PlayerAvatar(this);
        _spawnMenu = new SpawnMenu(_icons);
        Scene.Load(World);
        Sounds.Init(World, Player.CameraObject);
        Ui.ClickSound = () => Sounds.Play("sfx_ui_click", 0.35f);
        _hum = Sounds.Loop("sfx_physgun_hum", Player.CameraObject, 0f);

        Fx = new Fx(this);
        Emitter.Fx = Fx;
        ToolGun.Game = this;
        Weapons.Add(new HandsWeapon());
        Weapons.Add(new CrowbarWeapon());
        Weapons.Add(new PhysGunWeapon());
        Weapons.Add(new SpawnGunWeapon());
        Weapons.Add(new PistolWeapon());
        Weapons.Add(new SmgWeapon());
        Weapons.Add(new ShotgunWeapon());
        Weapons.Add(new RocketLauncherWeapon());
        Weapons.Add(new GrenadeWeapon());
        Weapons.Add(new BatonWeapon());
        Weapons.Add(new LockpickWeapon());
        Weapons.Add(new MedkitWeapon());
        foreach (Weapon wpn in Weapons.All)
        {
            Weapon w0 = wpn;
            SpawnCatalog.Register(new SpawnEntry(w0.Id, w0.Name, MenuTab.Weapons, w0.Slot switch { 1 => "Melee", 2 => "Sandbox", 5 => "Explosives", _ => "Guns" },
                w0.Icon, null, (g, _, _) => { g.Weapons.Select(g, w0); return w0.Name; }));
        }
        Weapons.Add(new ToolGunWeapon());
        Weapons.Select(this, Weapons.Get<PhysGunWeapon>());

        SpawnMenuBackdrop();
        if (!Application.IsHeadless)
            Window.Closing += () => SaveOnExit();

        ApplySettings();
        if (Application.IsHeadless || SelfTest is not null) Menus.Go(Screen.Playing);
        Console.WriteLine($"Sandbox ready: map built, {Props.Props.Count} starter props.");
        SelfTest?.Begin(this);
        ApplyShotState();
    }

    /// <summary>The live scene behind the main menu (and the starter props for shot/self-test states).</summary>
    public void SpawnMenuBackdrop()
    {
        Props.Spawn(PropKind.Cube, new Float3(-1f, 1f, -2f), 10f, recordUndo: false);
        Props.Spawn(PropKind.Cube, new Float3(0.2f, 2.5f, -2f), 35f, recordUndo: false);
        Props.Spawn(PropKind.Sphere, new Float3(1.5f, 1f, -2.5f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Barrel, new Float3(3f, 1f, -1f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Plank, new Float3(-3f, 0.5f, 0f), 30f, recordUndo: false);
        Player.Teleport(new Float3(0f, 0.1f, -10f));
    }

    /// <summary>Slow orbit around the starter props while no session is running.</summary>
    private void OrbitMenuCamera()
    {
        float t = 0.6f + (float)Time.UnscaledTotalTime * 0.13f;
        Float3 c = new(0f, 1.2f, -1.5f);
        Float3 eye = c + new Float3(MathF.Sin(t) * 9f, 2.6f + MathF.Sin(t * 0.7f) * 0.5f, -MathF.Cos(t) * 9f);
        Player.Teleport(eye - new Float3(0f, Player.EyeHeight, 0f));
        Float3 d = Float3.Normalize(c - eye);
        Player.SetLook(MathF.Atan2(d.X, d.Z) * 180f / MathF.PI, -MathF.Asin(d.Y) * 180f / MathF.PI);
    }

    public void ApplySettings()
    {
        Binds.Source = Settings;
        Props.Protection.Mode = Settings.PropProtection;
        Props.Protection.Load(Settings.Buddies);
        ApplySettingsLive();
        GraphicsQuality gq = Settings.LowPower ? GraphicsQuality.Low : Settings.Graphics;
        ShadowQuality q = gq == GraphicsQuality.Low ? ShadowQuality.Hard : ShadowQuality.Soft;
        Sun.ShadowQuality = q;
        Graphics.Apply(Player.Camera, gq);
    }

    /// <summary>Cheap settings applied every frame while the settings screen is open.</summary>
    public void ApplySettingsLive()
    {
        Player.LookSensitivity = 0.12f * Settings.MouseSensitivity;
        Player.Camera.FieldOfView = Settings.FieldOfView;
        Theme.UserScale = Settings.UiScale;
        Sounds.Volume = Settings.MasterVolume;
    }

    public void ResetSettings()
    {
        Settings = new Settings();
        ApplySettings();
    }

    private void ApplyShotState()
    {
        switch (ShotState)
        {
            case "main": Menus.Go(Screen.MainMenu); break;
            case "settings": Menus.Go(Screen.Settings); break;
            case "night": Menus.Go(Screen.Playing); DayNight.TimeOfDay = 0.95f; DayNight.Apply(this); ToggleFlashlight(); break;
            case "garage":
                Menus.Go(Screen.Playing);
                ClearMap(false);
                Vehicles.SpawnBuggy(new Float3(-4.5f, 1.0f, 0f), 210f, false, "buggy");
                Vehicles.SpawnBuggy(new Float3(0f, 1.2f, 1f), 200f, false, "jeep");
                Vehicles.SpawnBuggy(new Float3(4.8f, 0.9f, 0f), 160f, false, "airboat");
                Player.Teleport(new Float3(0f, 0.1f, -8f));
                Player.SetLook(0f, 14f);
                break;
            case "models":
                Menus.Go(Screen.Playing);
                ClearMap(false);
                {
                    string[] ids = ["sofa", "coffee_table", "tv", "lamp", "bookshelf", "plant", "chair", "table", "barrier", "cone", "stop_sign", "street_lamp", "wheel_tractor", "metal_crate", "fence", "bench"];
                    for (int i = 0; i < ids.Length; i++)
                    {
                        ModelDef d = ModelProps.All.First(x => x.Id == ids[i]);
                        float row = i / 8, col = i % 8;
                        ModelProps.Spawn(this, d, new Float3(-7f + col * 2f, d.HalfHeight + 0.05f, -2f + row * 3f), 180f, recordUndo: false);
                    }
                    Player.Teleport(new Float3(0f, 0.1f, -9f));
                    Player.SetLook(0f, 12f);
                }
                break;
            case "tools2": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Tools; SelectTool(ToolMode.Hoverball); break;
            case "dupes":
                Menus.Go(Screen.Playing); ClearMap(false);
                {
                    Prop a = Props.Spawn(PropKind.Cube, new Float3(0f, 0.6f, 4f), 0f, recordUndo: false), b = Props.Spawn(PropKind.Plank, new Float3(0f, 1.3f, 4f), 0f, recordUndo: false);
                    ToolGun.Weld(a, b, recordUndo: false);
                    ToolGun.Dupe = Dupes.Copy(this, a); ToolGun.DupeName = "Cube tower";
                    Dupes.Save("Hover platform", ToolGun.Dupe); Dupes.Save("Catapult", ToolGun.Dupe); Dupes.Save("Cube tower", ToolGun.Dupe);
                }
                _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Dupes; break;
            case "myspawn":
                Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Spawnlists;
                _spawnMenu.User.Add("Living room", "prop_sofa"); _spawnMenu.User.Add("Living room", "prop_tv"); _spawnMenu.User.Add("Living room", "prop_lamp"); _spawnMenu.User.Add("Living room", "prop_plant");
                _spawnMenu.ShowUserList("Living room"); break;
            case "utilities": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Utilities; break;
            case "dusk": Menus.Go(Screen.Playing); DayNight.TimeOfDay = 0.755f; DayNight.Apply(this); break;
            case "props": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Spawnlists; break;
            case "keybinds": Menus.Go(Screen.Settings); Menus.SettingsTab = 3; KeyCapture.Begin("st_kbk_use", _ => { }); break;
            case "thruster": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Tools; SelectTool(ToolMode.Wheel); break;
            case "tools": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Tools; SelectTool(ToolMode.Balloon); break;
            case "entities": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Entities; break;
            case "vehicles": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Vehicles; break;
            case "weapons": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Weapons; break;
            case "pause": Menus.Go(Screen.Paused); break;
            case "worlds": SeedDemoWorlds(); Menus.RefreshWorlds(); Menus.Go(Screen.Worlds); break;
            case "create": Menus.RefreshWorlds(); Menus.Go(Screen.Worlds); Menus.Go(Screen.CreateWorld); break;
            case "loading": HoldLoaderAt = 0.62f; EnterSession(SeedDemoWorlds()[0]); break;
            case "tutorial": Settings.TutorialStep = 1; EnterSession(SeedDemoWorlds().First(x => x.Meta.Mode != "Roleplay")); break;
            case "rp" or "jobs" or "shop" or "town" or "towndoor" or "townair" or "jail" or "bank": EnterSession(SeedDemoWorlds().First(x => x.Meta.Mode == "Roleplay")); break;
            case "hills" or "lake": EnterSession(SeedDemoWorlds().First(x => x.Meta.Map == "hills")); break;
            case "smg":
                Menus.Go(Screen.Playing);
                Weapons.Select(this, Weapons.Get<SmgWeapon>());
                break;
            case "drive":
                Menus.Go(Screen.Playing);
                Vehicles.Enter(Vehicles.SpawnBuggy(Player.FeetPosition + new Float3(0f, 1.2f, 3f), 0f));
                break;
            case "physgun":
                Menus.Go(Screen.Playing);
                SelectPhysGun();
                break;
            case "camera":
                Menus.Go(Screen.Playing);
                {
                    Float3 at = new(3.4f, 0.6f, 2.2f);
                    Prop camProp = CameraFeeds.Spawn(this, at, 0f, recordUndo: false);
                    camProp.SetFrozen(true);
                    camProp.Go.Transform.Rotation = Quaternion.LookRotation(Float3.Normalize(new Float3(0.2f, 1.0f, -2.2f) - at), Float3.UnitY);
                }
                break;
            case "perf":
                Menus.Go(Screen.Playing);
                int perfN = int.TryParse(Environment.GetEnvironmentVariable("SANDBOX_PERF_PROPS"), out var pn) ? pn : 200;
                for (int i = 0; i < perfN; i++)
                    Props.Spawn(PropManager.AllKinds[i % PropManager.AllKinds.Length],
                        new Float3(-10f + (i % 20) * 1.1f, 2f + (i / 20) * 1.3f, 6f + (i % 7) * 0.4f), i * 17f, recordUndo: false);
                for (int i = 0; i < (perfN > 0 ? 8 : 0); i++) Ragdoll.Spawn(this, new Float3(-8f + i * 2.2f, 1.2f, 2f), i * 40f, recordUndo: false);
                break;
            case "ragdoll":
                Menus.Go(Screen.Playing);
                Ragdoll.Spawn(this, new Float3(-0.5f, 1.2f, -3.5f), 160f);
                break;
            case "context":
                Menus.Go(Screen.Playing);
                Context.Open = true;
                Context.Selected = Props.Props[1];
                break;
            case { } gal when gal.StartsWith("gal_"):
                Menus.Go(Screen.Playing);
                Gallery.Show(this, gal[4..]);
                break;
            default:
                if (ShotState is not null) { Menus.Go(Screen.Playing); ShotHooks?.Invoke(this, ShotState); }
                break;
        }
    }

    /// <summary>Feature hooks for extra screenshot states (e.g. toolgun, weapons, vehicle, context).</summary>
    public static event Action<SandboxGame, string>? ShotHooks;

    public override void BeginRender()
    {
        if (_shotRequested && !Application.IsHeadless) { _shotRequested = false; CaptureScreenshot(); }
        if (!Application.IsHeadless && _frames == 2) _icons.RenderAll(Shader, Weapons.All.Select(w => (w.Id, w.ViewModelId)));
    }

    public override void EndUpdate()
    {
        _frames++;
        PerfLog();
        // Jitter's worker pool wakes every thread twice per step; for a sandbox-sized world one thread is cheaper.
        bool mt = Props.Props.Count > 300;
        if (World.Physics.UseMultithreading != mt) World.Physics.UseMultithreading = mt;
        KeyCapture.Poll();
        float dt = Time.UnscaledDeltaTime;
        if (ShotState == "perf")
        {
            _perfSum += dt; _perfMax = MathF.Max(_perfMax, dt); _perfN++;
            if (_perfSum > 2f)
            {
                Console.WriteLine($"PERF t={Time.UnscaledTotalTime:0.0}s props={Props.Props.Count} frame avg {_perfSum / _perfN * 1000f:0.0} ms max {_perfMax * 1000f:0.0} ms");
                _perfSum = 0; _perfMax = 0; _perfN = 0;
            }
        }
        _hud.Tick(dt);
        CameraFeeds.Tick(this);
        while (_pending.TryDequeue(out Action? act)) act();
        MenuBackdrop = Session is null && SelfTest is null && !Application.IsHeadless && Menus.Screen != Screen.Playing;
        bool menuWorld = Session is null && Menus.Screen is Screen.MainMenu or Screen.Worlds or Screen.CreateWorld or Screen.Settings && SelfTest is null && !Application.IsHeadless;
        Time.TimeScale = Menus.Screen == Screen.Loading ? 0f
            : (Menus.Screen is Screen.Paused || (Menus.Screen == Screen.Settings && Session is not null)) && ShotState is null ? 0f
            : Settings.TimeScale;
        if (Menus.Screen == Screen.Loading) { TickLoader(dt); FrameCap(); return; }
        if (Session is not null && SelfTest is null) TickAutosave(dt);
        if (menuWorld) OrbitMenuCamera();
        Health.Tick(this, Time.DeltaTime);
        Achievements.Tick(this, dt);
        if (DayNight.Enabled) DayNight.Tick(this, Time.DeltaTime);
        Weather.Tick(this, dt);
        Combat.Tick(Time.DeltaTime);
        if (Menus.Screen == Screen.Playing && (ShotState is null or "hints")) Hints.Tick(this, dt);
        if (ShotState is null or "tutorial") Tutorial.Tick(this, dt);
        if (IsRoleplay) Roleplay.Tick(Time.DeltaTime);

        if (SelfTest is not null)
        {
            Player.Update(Time.DeltaTime, allowInput: false);
            Weapons.Animate(this, Time.DeltaTime, Float2.Zero);
            Fx.Tick(Time.DeltaTime);
        AmbientAudio(Time.DeltaTime);
            Vehicles.Tick(Time.DeltaTime, allowInput: false);
            SelfTest.Step(this);
            return;
        }

        FrameCap();
        if (ShotState is null) { DevConsole.Tick(this); Binds.Typing = DevConsole.Open; HandleMenus(); }
        else if (ShotState == "context") { Context.Open = true; UpdateHighlight(Context.Selected); }
        else ShotTick();
        bool gameplay = !MenuOpen;
        bool input = gameplay && ShotState is null;

        if (gameplay) { if (!Input.CursorLocked) Input.LockCursor(); }
        else if (Input.CursorLocked) Input.UnlockCursor();

        Vehicles.Tick(Time.DeltaTime, input);
        if (SeatedIn is { Alive: true } seat)
        {
            Float3 up = seat.Go.Transform.Up;
            Player.UpdateSeated(seat.Go.Transform.Position + up * 0.75f, input);
            if (input && (Binds.Down("use") || Binds.Down("jump"))) { StandUp(); return; }
        }
        else if (SeatedIn is not null) StandUp();
        else if (!InVehicle && !menuWorld) Player.Update(Time.DeltaTime, input && !Health.Dead);
        Avatar.Tick(Time.DeltaTime, hidden: menuWorld || (Gallery.Active && !Gallery.ShowAvatar) || Health.Dead || SeatedIn is not null);
        UpdateFlashlightZoom(dt, input);
        Weapons.Aiming = input && !Health.Dead && Weapons.Current is Firearm && Input.GetMouseButton(1);
        Weapons.Animate(this, Time.DeltaTime, input ? Input.MouseDelta : Float2.Zero);
        Fx.Tick(Time.DeltaTime);
        Prop? aimed = !InVehicle && Player.Trace(80f, out RaycastHit aim) ? Props.FromBody(aim.Rigidbody) : null;
        AimingAtProp = aimed is not null;
        UpdateHighlight(Context.Open ? Context.Hovered ?? Context.Selected : Weapons.Current is PhysGunWeapon or ToolGunWeapon ? aimed : null);
        if (!gameplay)
        {
            PhysGun.Release(keepVelocity: false);
            return;
        }
        if (!input) return;

        if (Binds.Down("fps")) { Settings.ShowFps = !Settings.ShowFps; Settings.Save(); }
        if (Binds.Down("use") && Carry.Held is null && Vehicles.TryUse()) return;
        if (!InVehicle && SeatedIn is null && UseWorld()) return;
        if (InVehicle) { Carry.Drop(); return; }
        if (Health.Dead) return;
        if (Binds.Down("flashlight") && Weapons.Current is not ToolGunWeapon) ToggleFlashlight();
        if (SeatedIn is not null) { Weapons.Tick(this, new WeaponInput(Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0), Input.GetMouseButtonDown(1), Input.GetMouseButton(1), Binds.Down("reload"), 0f, false), dt); return; }
        if (Binds.Down("use") && aimed?.Go.GetComponent<Seat>() is not null && Carry.Held is null
            && Float3.Length(aimed.Go.Transform.Position - Player.EyePosition) < 3.5f) { SitIn(aimed); return; }
        if (Binds.Down("use") && IsRoleplay && aimed is not null && aimed.Entity?.StartsWith("rp_") == true
            && Float3.Length(aimed.Go.Transform.Position - Player.EyePosition) < 3.5f) { Notify(Roleplay.Use(aimed)); return; }
        if (Binds.Down("use")) Notify(Carry.Use(this));
        Carry.Update(this);
        bool carrying = Carry.Held is not null;
        if (carrying && Input.GetMouseButtonDown(0)) Carry.Throw(this);
        if (Binds.Down("noclip")) Player.ToggleNoclip();
        if (Binds.Down("undo")) Undo();
        for (int k = 1; k <= 9; k++)
            if (Input.GetKeyDown(KeyCode.Number0 + k)) Weapons.SelectSlot(this, k);

        float wheel = Input.MouseWheelDelta;
        if (wheel != 0f && !Weapons.Current.CapturesWheel) { Weapons.Cycle(this, wheel > 0 ? -1 : 1); wheel = 0f; }

        if (carrying) return; // hands are busy
        Weapons.Tick(this, new WeaponInput(Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0),
            Input.GetMouseButtonDown(1), Input.GetMouseButton(1), Binds.Down("reload"), wheel, Binds.Down("flashlight")), dt);
    }

    private void ShotTick()
    {
        if (ShotState is "rain" or "storm" or "fog" && Weather.Kind == WeatherKind.Clear) Weather.Set(WeatherSystem.Parse(ShotState), this, instant: true);
        if (ShotState == "combat" && _shotToasts == 0 && Time.UnscaledTotalTime > 4f)
        {
            _shotToasts = 1;
            Weapons.Select(this, Weapons.All.First(w => w.Id == "weapon_pistol"));
            var a = EntityCatalog.SpawnDummy(this, new Float3(-1.5f, 0f, -5f), 180f, recordUndo: false, team: NpcTeam.Hostile);
            EntityCatalog.SpawnDummy(this, new Float3(1.8f, 0f, -4f), 180f, recordUndo: false, team: NpcTeam.Citizen);
            var b = EntityCatalog.SpawnDummy(this, new Float3(4f, 0f, -6f), 180f, recordUndo: false, team: NpcTeam.Police);
            Combat.Hit(a.Limbs.First(l => l.Go.Name.EndsWith(" Head")).Body, 60f, a.Limbs[1].Go.Transform.Position + new Float3(0f, 0.6f, 0f), "Pistol");
            Combat.Hit(a.Limbs[1].Body, 60f, a.Limbs[1].Go.Transform.Position, "Pistol");
            Combat.Hit(b.Limbs[1].Body, 120f, b.Limbs[1].Go.Transform.Position, "Shotgun");
        }
        if (ShotState == "combat" && _shotToasts == 1 && Time.UnscaledTotalTime > 7.4f)
        {
            _shotToasts = 2;
            foreach (var r in Ragdoll.All.ToArray())
                if (r.Pelvis.Go.GetComponent<DummyBrain>() is { Limp: false, Team: NpcTeam.Citizen } c) Combat.Hit(r.Limbs[1].Body, 14f, r.Limbs[1].Go.Transform.Position + new Float3(0f, 0.3f, 0f), "SMG");
        }
        if (ShotState == "hints" && _frames < 5) Hints.Reset(this);
        if (ShotState == "console" && !DevConsole.Open && Time.UnscaledTotalTime > 4f)
        {
            DevConsole.Toggle(true);
            foreach (string l in new[] { "help", "spawn barrel 3", "time 18", "give laser", "nice build!" }) DevConsole.Execute(this, l);
            RequestScreenshot();
        }
        if (ShotState == "physgun" && Time.UnscaledTotalTime > 5f)
        {
            if (PhysGun.Held is null)
            {
                Prop target = Props.Props[1];
                Float3 d = Float3.Normalize(target.Go.Transform.Position - Player.EyePosition);
                Player.SetLook(MathF.Atan2(d.X, d.Z) * 180f / MathF.PI, -MathF.Asin(d.Y) * 180f / MathF.PI);
                Weapons.Tick(this, new WeaponInput(true, true, false, false, false, false, 0f), Time.DeltaTime);
            }
            else
            {
                Player.SetLook(-20f + MathF.Sin(Time.UnscaledTotalTime) * 5f, 15f);
                Weapons.Tick(this, new WeaponInput(false, true, false, false, false, false, 0f), Time.DeltaTime);
            }
        }
        // Screenshot mode "vm_<weapon>[_aim|_sprint|_reload|_fire]": holds one weapon still for view-model shots.
        if (ShotState is { } vs && vs.StartsWith("vm_") && Time.UnscaledTotalTime > 2f)
        {
            string[] sp = vs[3..].Split('_');
            if (Weapons.All.FirstOrDefault(w => w.Id == "weapon_" + sp[0]) is { } vw && !ReferenceEquals(Weapons.Current, vw)) Weapons.Select(this, vw);
            ShotPose = sp.Length > 1 ? sp[1] : null;
            // Motion checks: the clip fires once at 6 s (frame sequences are recorded from there).
            if (ShotPose is "reload" or "inspect" or "fire" or "grab" or "release" && !_vmShotDone && Time.UnscaledTotalTime > 6f && Weapons.Current.Vm is { } sv)
            {
                _vmShotDone = true;
                if (ShotPose == "reload" && Weapons.Current is Firearm rf) { rf.Mag = Math.Min(rf.Mag, 2); rf.StartReload(this); }
                else if (ShotPose == "fire" && Weapons.Current is Firearm ff) { ff.Mag = Math.Max(ff.Mag, 3); Weapons.Tick(this, new WeaponInput(true, true, false, false, false, false, 0f), Time.DeltaTime); }
                else { sv.Play(ShotPose == "fire" ? "fire" : ShotPose); if (ShotPose == "fire") sv.Kick(1f); }
            }
            if (ShotPose == "reload" && Weapons.Current is Firearm { Reloading: true } rr) rr.Tick(this, new WeaponInput(false, false, false, false, false, false, 0f), Time.DeltaTime);
        }
        if (ShotState == "drive" && Vehicles.Driving is { } car)
            car.Drive(Time.UnscaledTotalTime > 7f ? 0.6f : 0f, Time.UnscaledTotalTime < 8.5f, false, false);
        if (ShotState == "smg" && Time.UnscaledTotalTime > 6f)
        {
            var smg = Weapons.Get<SmgWeapon>();
            smg.Mag = 30;
            Weapons.Tick(this, new WeaponInput(false, true, false, false, false, false, 0f), Time.DeltaTime);
        }
        if (ShotState == "toolgun" && _shotToasts < 3 && Time.UnscaledTotalTime > 7.5f + _shotToasts * 0.4f)
        {
            Notify(_shotToasts switch { 0 => "Spawned Cube #6", 1 => "Welded Cube #1 to Cube #2", _ => "Undone: Sphere #3" });
            _shotToasts++;
        }
        if (ShotState == "toolgun" && Time.UnscaledTotalTime > 8.5f && Weapons.TimeSinceSwitch > 1f) Weapons.Select(this, Weapons.Get<PhysGunWeapon>()) ;
        if (ShotState == "toolgun" && Time.UnscaledTotalTime > 8.6f && !Weapons.IsToolGun) SelectTool(ToolMode.Weld);
    }

    private void HandleMenus()
    {
        if (IsRoleplay && Binds.Down("jobs") && Menus.Screen is Screen.Playing or Screen.Jobs)
            Menus.Go(Menus.Screen == Screen.Jobs ? Screen.Playing : Screen.Jobs);
        if (!DevConsole.Open && !DevConsole.JustClosed && Menus.Screen == Screen.Playing)
        {
            if (Input.GetKeyDown(KeyCode.F1)) { PhotoMode = !PhotoMode; if (PhotoMode) Notify("Photo mode · F1 to exit, F12 to save a screenshot"); }
            if (Input.GetKeyDown(KeyCode.F12)) RequestScreenshot();
        }
        if (DevConsole.Open || DevConsole.JustClosed) return;
        if ((Input.GetKeyDown(KeyCode.Escape) && !KeyCapture.Active))
        {
            if (_spawnMenu.Open) _spawnMenu.Open = false;
            else if (Menus.Screen != Screen.MainMenu) Menus.Back(this);
        }
        bool held = Binds.Held("spawnmenu") || (!Binds.Typing && Input.GetKey(KeyCode.Tab));
        _spawnMenu.Open = !Menus.Blocking && !InVehicle && (held || _spawnMenu.SearchFocused);
        Context.Open = !Menus.Blocking && !_spawnMenu.Open && !InVehicle && Binds.Held("context");
        Context.Update(this, Input.GetMouseButtonDown(0), _pointerOverContextUi);
    }

    private AudioSource? _hum;
    private float _stepTimer;
    private int _steps;
    /// <summary>Continuous footstep counter (integer = a footfall); the view model's walk bob locks to it.</summary>
    public float StepCycle => _steps + Math.Clamp(1f - _stepTimer / 0.45f, 0f, 1f);

    /// <summary>Footsteps while walking and the physgun hum while holding something.</summary>
    private void AmbientAudio(float dt)
    {
        if (!InVehicle && Player.Speed > 0.5f && Player.IsGrounded)
        {
            _stepTimer -= dt * Player.Speed / 4.5f;
            if (_stepTimer <= 0f)
            {
                _stepTimer = 0.45f; _steps++;
                Sounds.Play(FootstepSound(), Player.Crouching ? 0.12f : 0.25f, null, 0.9f + Random.Shared.NextSingle() * 0.2f);
            }
        }
        if (_hum is not null)
        {
            try
            {
                bool holding = PhysGun.Held is not null && !InVehicle;
                _hum.Volume += ((holding ? 0.35f : 0f) * Sounds.Volume - _hum.Volume) * MathF.Min(1f, dt * 10f);
                _hum.Pitch = 1f + (holding ? MathF.Min(0.4f, Float3.Length(PhysGun.Held!.Body.LinearVelocity) * 0.04f) : 0f);
            }
            catch { }
        }
    }

    /// <summary>Footstep by surface: the prop underfoot's material, grass on the hills, concrete otherwise.</summary>
    private string FootstepSound()
    {
        Float3 feet = Player.FeetPosition + new Float3(0f, 0.3f, 0f);
        if (World.Physics.Raycast(feet, -Float3.UnitY, 0.8f, out RaycastHit hit) && Props.FromBody(hit.Rigidbody) is { } p)
            return p.Surface switch
            {
                "Wood" => "sfx_footstep_wood",
                "Metal" => "sfx_footstep_metal",
                _ => "sfx_footstep",
            };
        return MapBuilder.Current == "hills" ? "sfx_footstep_grass" : "sfx_footstep";
    }

    private void UpdateHighlight(Prop? p)
    {
        if (!ReferenceEquals(p, _lastHighlight) && _lastHighlight is { Alive: true } old) old.SetHighlight(0f);
        _lastHighlight = p;
        p?.SetHighlight(Context.Open && ReferenceEquals(p, Context.Selected) ? 0.9f : 0.45f);
    }

    public void SelectPhysGun() => Weapons.Select(this, Weapons.Get<PhysGunWeapon>());

    public void SelectTool(ToolMode mode)
    {
        ToolGun.CancelPending();
        ToolGun.Mode = mode;
        if (!Weapons.IsToolGun) Weapons.Select(this, Weapons.Get<ToolGunWeapon>());
        else Weapons.Select(this, Weapons.Current); // no-op; keep current
        Sounds.Play("sfx_ui_click", 0.4f);
    }

    /// <summary>Spawn a catalog entry where the crosshair points.</summary>
    /// <summary>Soft cap on player-spawned props (sbox_maxprops style); Jitter already sleeps resting bodies.</summary>
    public const int MaxProps = 400;

    public string Spawn(SpawnEntry entry)
    {
        if (entry.Tab != MenuTab.Weapons && Props.Props.Count >= PropLimit) return $"Prop limit reached ({PropLimit}). Clean up with Undo or Clear map.";
        if (entry.Tab != MenuTab.Weapons && IsRoleplay && !Roleplay.ChargeForProp()) return $"You need ${RoleplaySystem.PropCost} to spawn that";
        if (entry.Tab != MenuTab.Weapons) SpawnMenu.PushRecent(entry.Id);
        Float3 pos;
        if (Player.Trace(40f, out RaycastHit hit))
            pos = hit.Point + hit.Normal * (entry.HalfHeight + 0.1f) + new Float3(0f, 0.1f, 0f);
        else
            pos = Player.EyePosition + Player.Forward * 5f;
        string name = entry.Spawn(this, pos, Player.Yaw);
        if (entry.Tab != MenuTab.Weapons) Fx.SpawnPuff(pos);
        LastSpawned = entry;
        if (entry.Tab != MenuTab.Weapons)
        {
            if (Weapons.Current is not SpawnGunWeapon) SpawnGunEntry = entry;
            RecentSpawns.Remove(entry);
            RecentSpawns.Insert(0, entry);
            if (RecentSpawns.Count > 8) RecentSpawns.RemoveAt(8);
        }
        if (entry.Tab != MenuTab.Weapons) Notify($"Spawned {name}");
        Sounds.Play("sfx_spawn", 0.6f, pos);
        return name;
    }

    public SpawnEntry? LastSpawned { get; set; }
    /// <summary>What the spawn gun fires: the last thing picked in the spawn menu.</summary>
    public SpawnEntry? SpawnGunEntry { get; set; }
    /// <summary>Most recent distinct spawn entries, newest first (spawn gun right click cycles these).</summary>
    public List<SpawnEntry> RecentSpawns { get; } = [];
    public Carry Carry { get; } = new();
    public Fx Fx { get; private set; } = null!;

    public Prop SpawnAtCrosshair(PropKind kind)
    {
        Spawn(SpawnCatalog.Find($"prop_{kind.ToString().ToLowerInvariant()}")!);
        return Props.Props[^1];
    }

    public void Undo()
    {
        Notify(Props.Undo() ?? "Nothing to undo");
        Sounds.Play("sfx_undo", 0.5f);
    }

    public void ClearMap(bool notify = true)
    {
        StandUp();
        PhysGun.Release(keepVelocity: false);
        ToolGun.CancelPending();
        Vehicles.ClearAll();
        CameraFeeds.Clear();
        Props.ClearAll();
        Fx.Clear();
        KeyState.Toggled.Clear();
        if (notify) Notify("Map cleared", ToastKind.Danger);
    }

    public void SaveMap()
    {
        try { Notify($"Saved {MapSave.Save(this, MapSave.DefaultPath)} props"); }
        catch (Exception e) { Notify($"Save failed: {e.Message}", ToastKind.Danger); }
    }

    public void LoadMap()
    {
        if (!File.Exists(MapSave.DefaultPath)) { Notify("Nothing saved yet", ToastKind.Danger); return; }
        try { Notify($"Loaded {MapSave.Load(this, MapSave.DefaultPath)} props"); }
        catch (Exception e) { Notify($"Load failed: {e.Message}", ToastKind.Danger); }
    }

    public void QuitGame()
    {
        SaveOnExit();
        Settings.Save();
        Quit();
    }

    public override void OnGui(Scene? scene, Paper paper)
    {
        base.OnGui(scene, paper);
        if (Application.IsHeadless) return;
        (float w, float h) = Ui.ScreenSize();
        Theme.UpdateScale(w, h);
        float dt = Time.UnscaledDeltaTime;
        Ui.Dt = dt;

        bool chrome = !_spawnMenu.Visible && !Menus.Blocking && !Context.Visible;
        if (PhotoMode && Menus.Screen == Screen.Playing) { _hud.Draw(paper, w, h, this, dt, chrome: false); return; }
        _hud.Draw(paper, w, h, this, dt, chrome);
        if (chrome) { Hints.Draw(paper, w, h); Combat.Draw(paper, w, h); Tutorial.Draw(this, paper, w, h); }
        if (chrome || Health.Dead) Health.DrawOverlay(paper, w, h);
        if (chrome) CameraFeeds.Draw(paper, w, h);
        _pointerOverContextUi = Context.PointerOverPanel(paper, w, h);
        Context.Draw(paper, w, h, dt, this);
        _spawnMenu.Draw(paper, w, h, dt, this);
        Menus.Draw(paper, w, h, dt, this);
        DevConsole.Draw(paper, w, h);
    }
}

public sealed partial class SandboxGame
{
    private readonly System.Diagnostics.Stopwatch _frameClock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Optional frame-rate cap (Settings → Video). Sleeps the remainder of the frame budget.</summary>
    private static readonly bool _perfLog = Environment.GetEnvironmentVariable("SANDBOX_PERF") == "1";
    private readonly System.Diagnostics.Stopwatch _perfClock = System.Diagnostics.Stopwatch.StartNew();
    private int _perfFrames; private double _perfBusy, _lastSleepMs; private long _perfAlloc; private TimeSpan _perfCpu;
    /// <summary>SANDBOX_PERF=1: every 5 s log fps, main-thread CPU ms/frame, allocations/frame, GCs and heap.</summary>
    private void PerfLog()
    {
        if (!_perfLog) return;
        _perfFrames++;
        _perfBusy += Time.UnscaledDeltaTime * 1000.0 - Prowl.Runtime.Graphics.LastFrameWaitMs - _lastSleepMs;
        double s = _perfClock.Elapsed.TotalSeconds;
        if (s < 5) return;
        long alloc = GC.GetTotalAllocatedBytes(); TimeSpan cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        Console.WriteLine($"[perf] {_perfFrames / s:0.0} fps, main busy {_perfBusy / _perfFrames:0.00} ms/frame, process cpu {(cpu - _perfCpu).TotalMilliseconds / s / 10:0}% , alloc {(alloc - _perfAlloc) / _perfFrames / 1024.0:0.0} KB/frame, gc {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}, heap {GC.GetTotalMemory(false) / 1048576} MB, live-after-gc {GC.GetGCMemoryInfo().HeapSizeBytes / 1048576} MB, rss {Environment.WorkingSet / 1048576} MB, threads {System.Diagnostics.Process.GetCurrentProcess().Threads.Count}, props {Props.Props.Count}, renderers {World.AllObjects.Count(o => o.EnabledInHierarchy && o.GetComponent<MeshRenderer>() is { Enabled: true })}, objects {World.AllObjects.Count()}, batched {MapBuilder.BatchedPieces}->{MapBuilder.BatchMeshes}");
        if (Environment.GetEnvironmentVariable("SANDBOX_PERF_DUMP") == "1")
            foreach (var gp in World.AllObjects.Where(o => o.EnabledInHierarchy && o.GetComponent<MeshRenderer>() is { Enabled: true })
                .GroupBy(o => { var r = o; while (r.Transform.Parent is { } pp) r = pp.GameObject; return r.Name.Split(' ')[0]; }).OrderByDescending(x => x.Count()).Take(15))
                Console.WriteLine($"  [perf] {gp.Count(),4} renderers under '{gp.Key}*'");
        _perfAlloc = alloc; _perfCpu = cpu; _perfFrames = 0; _perfBusy = 0; _perfClock.Restart();
    }

    private bool _minimized;
    /// <summary>Effective frame-rate limit: the setting, 60 in low-power mode, 30 (15) unfocused, 10 minimised.</summary>
    public int EffectiveFrameCap
    {
        get
        {
            int cap = Settings.FrameCap;
            if (Settings.LowPower) cap = cap <= 0 ? 60 : Math.Min(cap, 60);
            bool focused = true; try { focused = Window.IsFocused; } catch { }
            if (_minimized) return 10;
            if (!focused) return cap <= 0 ? (Settings.LowPower ? 15 : 30) : Math.Min(cap, Settings.LowPower ? 15 : 30);
            return cap;
        }
    }

    /// <summary>Frame limiter. Sleeps (nanosleep on Linux is accurate to well under a millisecond) instead of
    /// spin-waiting, so a capped frame costs no CPU while it waits.</summary>
    private void FrameCap()
    {
        int cap = Application.IsHeadless ? 0 : EffectiveFrameCap;
        if (cap <= 0) { _frameClock.Restart(); return; }
        double budget = 1000.0 / cap;
        double left = budget - _frameClock.Elapsed.TotalMilliseconds;
        _lastSleepMs = 0;
        if (left > 0.25) { Thread.Sleep(TimeSpan.FromMilliseconds(left - 0.15)); _lastSleepMs = left; }
        _frameClock.Restart();
    }

    /// <summary>World gravity (Utilities slider); the player controller follows, and the session modifier is updated.</summary>
    public void SetGravity(float g)
    {
        World.Physics.Gravity = new Float3(0f, -g, 0f);
        Player.Gravity = 20f * g / 9.81f;
        if (Meta is { } m) m.Modifiers.Gravity = g;
        foreach (Prop p in Props.Props) if (!p.Frozen) p.Body.SetActive(true); // wake sleepers so they feel the change
    }

    /// <summary>The seat prop the player is riding, if any.</summary>
    public Prop? SeatedIn { get; private set; }

    public void SitIn(Prop seat)
    {
        PhysGun.Release(keepVelocity: false);
        SeatedIn = seat;
        Player.Teleport(seat.Go.Transform.Position - new Float3(0f, 200f, 0f), moveCamera: false);
        Notify($"Sitting in {seat.Go.Name.Split(" #")[0]} · E to stand up");
        Sounds.Play("sfx_door", 0.4f, seat.Go.Transform.Position);
    }

    public void StandUp()
    {
        if (SeatedIn is not { } s) return;
        SeatedIn = null;
        Float3 at = s.Alive ? s.Go.Transform.Position + new Float3(0f, 0.2f, 0f) - Float3.Normalize(new Float3(Player.Forward.X, 0f, Player.Forward.Z) + new Float3(0.0001f, 0f, 0f)) * 0.9f : Player.EyePosition;
        Player.Teleport(new Float3(at.X, MathF.Max(at.Y, (s.Alive ? s.Go.Transform.Position.Y : at.Y) - 0.3f), at.Z));
    }

    public void ToggleFlashlight()
    {
        Flashlight = !Flashlight;
        if (_flashlight is null)
        {
            var go = new GameObject("Flashlight");
            _flashlight = go.AddComponent<SpotLight>();
            _flashlight.Color = new Color(1f, 0.95f, 0.85f, 1f);
            _flashlight.Intensity = 4.5f;
            _flashlight.Range = 30f;
            _flashlight.SpotAngle = 50f;
            _flashlight.CastShadows = false;
            World.Add(go);
        }
        _flashlight.Enabled = Flashlight;
        Sounds.Play("sfx_ui_click", 0.5f, null, Flashlight ? 1.2f : 0.9f);
    }

    /// <summary>Flashlight follows the eye; hold Zoom (B) to narrow the FOV smoothly.</summary>
    private void UpdateFlashlightZoom(float dt, bool input)
    {
        if (_flashlight is not null && Flashlight)
        {
            _flashlight.GameObject.Transform.Position = Player.EyePosition + Player.Forward * 0.75f;
            _flashlight.GameObject.Transform.Rotation = Player.CameraObject.Transform.Rotation;
        }
        bool zoom = input && Binds.Held("zoom");
        _zoom += ((zoom ? 1f : 0f) - _zoom) * MathF.Min(1f, dt * 12f);
        // Aim-down-sights narrows the FOV by the weapon's zoom and slows the mouse a touch.
        float ads = Weapons.Current?.Vm is { } avm ? avm.AimBlend : 0f;
        float adsZoom = 1f + ((Weapons.Current?.Vm?.Def.AimZoom ?? 1f) - 1f) * ads;
        Player.Camera.FieldOfView = (Settings.FieldOfView + (25f - Settings.FieldOfView) * _zoom) / adsZoom;
        Player.LookSensitivity = 0.12f * Settings.MouseSensitivity * (1f - 0.6f * _zoom) * (1f - 0.3f * ads);
    }
}

public sealed partial class SandboxGame
{
    /// <summary>Screenshot mode: freeze the loading screen once progress reaches this value.</summary>
    public float HoldLoaderAt { get; set; } = 2f;

    /// <summary>Screenshot mode: a few example worlds so the Worlds screen isn't empty.</summary>
    /// <summary>What the crosshair is on, for town interactions (doors, ATM, laws board), within reach.</summary>
    public (Door? Door, bool Atm, bool Board) WorldAim()
    {
        if (!Town.Active || !Player.Trace(3f, out RaycastHit hit) || Town.At(hit) is not { } go) return (null, false, false);
        return (go.GetComponent<Door>(), go.GetComponent<AtmMachine>() is not null, go.GetComponent<LawsBoard>() is not null);
    }

    /// <summary>E / F2 / L on town objects. Returns true when the key press was consumed.</summary>
    private bool UseWorld()
    {
        bool e = Binds.Down("use"), f2 = Input.GetKeyDown(KeyCode.F2), l = Input.GetKeyDown(KeyCode.L), k = Input.GetKeyDown(KeyCode.K);
        if (!e && !f2 && !l && !k) return false;
        var (door, atm, _) = WorldAim();
        if (door is not null)
        {
            string msg = e ? Roleplay.UseDoor(door) : f2 ? Roleplay.BuyOrSellDoor(door) : l ? Roleplay.ToggleLock(door)
                : Roleplay.GiveKey(door, Input.IsShiftPressed);
            if (msg.Length > 0) Notify(msg);
            return true;
        }
        if (atm && e)
        {
            if (!IsRoleplay) { Notify("The ATM only works in Roleplay"); return true; }
            Roleplay.AtAtm = true;
            Menus.OpenBank();
            return true;
        }
        return false;
    }

    private List<WorldEntry> SeedDemoWorlds()
    {
        var list = Worlds.List();
        if (list.Count > 0) return list;
        (string Name, string Mode, string Map)[] demos = [("Flatgrass build", "Sandbox", "flatgrass"), ("Rolling Hills", "Sandbox", "hills"), ("Downtown RP", "Roleplay", "town")];
        foreach (var (name, mode, map) in demos)
        {
            var meta = new SessionMeta { Name = name, Mode = mode, Map = map, PlaySeconds = name.Length * 300, PropCount = name.Length * 3, LastPlayed = DateTime.Now.AddHours(-name.Length) };
            meta.Modifiers.Npcs = mode == "Roleplay";
            Worlds.Create(meta);
        }
        return Worlds.List();
    }
}

public sealed partial class SandboxGame
{
    /// <summary>Window mode, resolution and VSync from Settings (VSync is only honoured at start-up by the engine).</summary>
    public override void InitializeWindow(string title, int width, int height)
    {
        Settings boot = Settings.Load();
        var state = boot.WindowMode == WindowMode.Fullscreen ? Silk.NET.Windowing.WindowState.Fullscreen : Silk.NET.Windowing.WindowState.Normal;
        Window.InitWindow(title, width, height, state, boot.VSync);
        if (boot.WindowMode == WindowMode.Borderless)
        {
            try { Window.InternalWindow.WindowBorder = Silk.NET.Windowing.WindowBorder.Hidden; Window.InternalWindow.WindowState = Silk.NET.Windowing.WindowState.Maximized; }
            catch (Exception e) { Log.Warn($"Borderless window unavailable: {e.Message}"); }
        }
    }

    /// <summary>Applies window mode + resolution while running (Settings → Video).</summary>
    public void ApplyWindowMode()
    {
        if (Application.IsHeadless) return;
        try
        {
            var w = Window.InternalWindow;
            switch (Settings.WindowMode)
            {
                case WindowMode.Fullscreen:
                    w.WindowBorder = Silk.NET.Windowing.WindowBorder.Resizable;
                    w.WindowState = Silk.NET.Windowing.WindowState.Fullscreen;
                    break;
                case WindowMode.Borderless:
                    w.WindowState = Silk.NET.Windowing.WindowState.Normal;
                    w.WindowBorder = Silk.NET.Windowing.WindowBorder.Hidden;
                    w.WindowState = Silk.NET.Windowing.WindowState.Maximized;
                    break;
                default:
                    w.WindowState = Silk.NET.Windowing.WindowState.Normal;
                    w.WindowBorder = Silk.NET.Windowing.WindowBorder.Resizable;
                    w.Size = new Silk.NET.Maths.Vector2D<int>(Settings.ResolutionWidth, Settings.ResolutionHeight);
                    break;
            }
        }
        catch (Exception e) { Log.Warn($"Window mode change failed: {e.Message}"); Notify("Couldn't change the window mode on this system", ToastKind.Danger); }
    }
}

public sealed partial class SandboxGame
{
    private bool _shotRequested;
    public static string ScreenshotDir => Path.Combine(Path.GetDirectoryName(Worlds.Root) ?? ".", "screenshots");
    public string? LastScreenshot { get; private set; }

    public void RequestScreenshot()
    {
        if (Application.IsHeadless) { Notify("Screenshots need a window", ToastKind.Danger); return; }
        _shotRequested = true;
    }

    /// <summary>Unique timestamped file name in <see cref="ScreenshotDir"/> (never overwrites).</summary>
    public static string NextScreenshotPath(DateTime now)
    {
        string baseName = $"sandbox_{now:yyyy-MM-dd_HH-mm-ss}";
        string path = Path.Combine(ScreenshotDir, baseName + ".png");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(ScreenshotDir, $"{baseName}_{i}.png");
        return path;
    }

    /// <summary>Writes bottom-up RGBA8 pixels (GL order) as a PNG.</summary>
    public static void WritePng(string path, byte[] rgba, int w, int h)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var img = new ImageMagick.MagickImage(rgba, new ImageMagick.PixelReadSettings((uint)w, (uint)h, ImageMagick.StorageType.Char, ImageMagick.PixelMapping.RGBA));
        img.Flip();
        img.Alpha(ImageMagick.AlphaOption.Off);
        string tmp = path + ".tmp";
        img.Write(tmp, ImageMagick.MagickFormat.Png);
        File.Move(tmp, path, true);
    }

    /// <summary>Renders the player camera (world only, no HUD) to an offscreen target and saves it.</summary>
    private void CaptureScreenshot()
    {
        try
        {
            var cam = Player.CameraObject.GetComponent<Camera>();
            if (cam is null) return;
            var fb = Window.InternalWindow.FramebufferSize;
            int w = Math.Max(16, fb.X), h = Math.Max(16, fb.Y);
            var rt = new RenderTexture(w, h, true, [TextureImageFormat.Color4b]);
            RenderTexture? old = cam.Target;
            cam.Target = rt;
            try { cam.UpdateRenderData(); (cam.Pipeline ?? Prowl.Runtime.Rendering.DefaultRenderPipeline.Default).Render(cam, new Prowl.Runtime.Rendering.RenderingData()); }
            finally { cam.Target = old; cam.UpdateRenderData(); }
            byte[] px = new byte[w * h * 4];
            rt.MainTexture.GetData(new Memory<byte>(px));
            rt.Dispose();
            string path = NextScreenshotPath(DateTime.Now);
            System.Threading.Tasks.Task.Run(() =>
            {
                try { WritePng(path, px, w, h); LastScreenshot = path; Enqueue(() => { Notify($"Saved screenshot {Path.GetFileName(path)}", ToastKind.Success); Sounds.Play("sfx_ui_click", 0.5f); }); }
                catch (Exception e) { Enqueue(() => Notify($"Screenshot failed: {e.Message}", ToastKind.Danger)); }
            });
        }
        catch (Exception e) { Notify($"Screenshot failed: {e.Message}", ToastKind.Danger); }
    }
}
