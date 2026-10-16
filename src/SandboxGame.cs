using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public sealed class SandboxGame : Game
{
    public SelfTest? SelfTest { get; set; }

    /// <summary>Optional forced UI state for screenshots (see README "Screenshot mode").</summary>
    public string? ShotState { get; set; }
    private float _perfSum, _perfMax; private int _perfN;

    public Scene World { get; private set; } = null!;
    public PropManager Props { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public PhysGun PhysGun { get; private set; } = null!;
    public ToolGun ToolGun { get; private set; } = null!;
    public WeaponSystem Weapons { get; } = new();
    public SoundSystem Sounds { get; } = new();
    public VehicleSystem Vehicles { get; private set; } = null!;
    public Settings Settings { get; private set; } = new();
    public Menus Menus { get; } = new();
    public bool AimingAtProp { get; private set; }
    public bool InVehicle => Vehicles.InVehicle;
    public Shader Shader { get; private set; } = null!;
    public DirectionalLight Sun { get; private set; } = null!;

    private readonly Hud _hud = new();
    private readonly IconRenderer _icons = new();
    private SpawnMenu _spawnMenu = null!;
    public ContextMenu Context { get; } = new();
    private bool _pointerOverContextUi;
    private Prop? _lastHighlight;
    private readonly Queue<Action> _pending = new();
    private int _frames, _shotToasts;

    /// <summary>True when a menu (spawn menu, main/pause/settings) owns the mouse.</summary>
    public bool MenuOpen => Menus.Blocking || _spawnMenu.Open || Context.Open;

    public void Enqueue(Action a) => _pending.Enqueue(a);
    public void Notify(string? message, ToastKind? kind = null) => _hud.Notify(message, kind);

    public override void Initialize()
    {
        Settings = Application.IsHeadless ? new Settings() : Settings.Load();
        World = new Scene();
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
        Player = new Player(World, new Float3(0f, 0.1f, -10f), 0f);
        if (Environment.GetEnvironmentVariable("SANDBOX_SOLIDSKY") != "1") Player.Camera.ClearFlags = CameraClearFlags.Skybox;
        PhysGun = new PhysGun(Props);
        ToolGun = new ToolGun(Props);
        Vehicles = new VehicleSystem(this);
        _spawnMenu = new SpawnMenu(_icons);
        Scene.Load(World);
        Sounds.Init(World, Player.CameraObject);
        Ui.ClickSound = () => Sounds.Play("sfx_ui_click", 0.35f);
        _hum = Sounds.Loop("sfx_physgun_hum", Player.CameraObject, 0f);

        Fx = new Fx(this);
        Weapons.Add(new HandsWeapon());
        Weapons.Add(new CrowbarWeapon());
        Weapons.Add(new PhysGunWeapon());
        Weapons.Add(new SpawnGunWeapon());
        Weapons.Add(new PistolWeapon());
        Weapons.Add(new SmgWeapon());
        Weapons.Add(new ShotgunWeapon());
        Weapons.Add(new RocketLauncherWeapon());
        Weapons.Add(new GrenadeWeapon());
        foreach (Weapon wpn in Weapons.All)
        {
            Weapon w0 = wpn;
            SpawnCatalog.Register(new SpawnEntry(w0.Id, w0.Name, MenuTab.Weapons, w0.Slot switch { 1 => "Melee", 2 => "Sandbox", 5 => "Explosives", _ => "Guns" },
                w0.Icon, null, (g, _, _) => { g.Weapons.Select(g, w0); return w0.Name; }));
        }
        Weapons.Add(new ToolGunWeapon());
        Weapons.Select(this, Weapons.Get<PhysGunWeapon>());

        Props.Spawn(PropKind.Cube, new Float3(-1f, 1f, -2f), 10f, recordUndo: false);
        Props.Spawn(PropKind.Cube, new Float3(0.2f, 2.5f, -2f), 35f, recordUndo: false);
        Props.Spawn(PropKind.Sphere, new Float3(1.5f, 1f, -2.5f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Barrel, new Float3(3f, 1f, -1f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Plank, new Float3(-3f, 0.5f, 0f), 30f, recordUndo: false);

        ApplySettings();
        if (Application.IsHeadless || SelfTest is not null) Menus.Go(Screen.Playing);
        Console.WriteLine($"Sandbox ready: map built, {Props.Props.Count} starter props.");
        SelfTest?.Begin(this);
        ApplyShotState();
    }

    public void ApplySettings()
    {
        ApplySettingsLive();
        ShadowQuality q = Settings.Graphics == GraphicsQuality.Low ? ShadowQuality.Hard : ShadowQuality.Soft;
        Sun.ShadowQuality = q;
        Graphics.Apply(Player.Camera, Settings.Graphics);
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
            case "props": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Spawnlists; break;
            case "tools": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Tools; SelectTool(ToolMode.Balloon); break;
            case "entities": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Entities; break;
            case "vehicles": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Vehicles; break;
            case "weapons": Menus.Go(Screen.Playing); _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Weapons; break;
            case "pause": Menus.Go(Screen.Paused); break;
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
            default:
                if (ShotState is not null) { Menus.Go(Screen.Playing); ShotHooks?.Invoke(this, ShotState); }
                break;
        }
    }

    /// <summary>Feature hooks for extra screenshot states (e.g. toolgun, weapons, vehicle, context).</summary>
    public static event Action<SandboxGame, string>? ShotHooks;

    public override void BeginRender()
    {
        if (!Application.IsHeadless && _frames == 2) _icons.RenderAll(Shader);
    }

    public override void EndUpdate()
    {
        _frames++;
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
        while (_pending.Count > 0) _pending.Dequeue()();
        Time.TimeScale = Menus.Screen is Screen.Paused or Screen.Settings && Menus.Blocking && ShotState is null ? 0f : 1f;

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

        if (ShotState is null) HandleMenus();
        else if (ShotState == "context") { Context.Open = true; UpdateHighlight(Context.Selected); }
        else ShotTick();
        bool gameplay = !MenuOpen;
        bool input = gameplay && ShotState is null;

        if (gameplay) { if (!Input.CursorLocked) Input.LockCursor(); }
        else if (Input.CursorLocked) Input.UnlockCursor();

        Vehicles.Tick(Time.DeltaTime, input);
        if (!InVehicle) Player.Update(Time.DeltaTime, input);
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

        if (Input.GetKeyDown(KeyCode.F3)) { Settings.ShowFps = !Settings.ShowFps; Settings.Save(); }
        if (Input.GetKeyDown(KeyCode.E) && Carry.Held is null && Vehicles.TryUse()) return;
        if (InVehicle) { Carry.Drop(); return; }
        if (Input.GetKeyDown(KeyCode.E)) Notify(Carry.Use(this));
        Carry.Update(this);
        bool carrying = Carry.Held is not null;
        if (carrying && Input.GetMouseButtonDown(0)) Carry.Throw(this);
        if (Input.GetKeyDown(KeyCode.V)) Player.ToggleNoclip();
        if (Input.GetKeyDown(KeyCode.Z)) Undo();
        for (int k = 1; k <= 9; k++)
            if (Input.GetKeyDown(KeyCode.Number0 + k)) Weapons.SelectSlot(this, k);

        float wheel = Input.MouseWheelDelta;
        if (wheel != 0f && !Weapons.Current.CapturesWheel) { Weapons.Cycle(this, wheel > 0 ? -1 : 1); wheel = 0f; }

        if (carrying) return; // hands are busy
        Weapons.Tick(this, new WeaponInput(Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0),
            Input.GetMouseButtonDown(1), Input.GetMouseButton(1), Input.GetKeyDown(KeyCode.R), wheel, Input.GetKeyDown(KeyCode.F)), dt);
    }

    private void ShotTick()
    {
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
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_spawnMenu.Open) _spawnMenu.Open = false;
            else if (Menus.Screen != Screen.MainMenu) Menus.Back(this);
        }
        bool held = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Tab);
        _spawnMenu.Open = !Menus.Blocking && !InVehicle && (held || _spawnMenu.SearchFocused);
        Context.Open = !Menus.Blocking && !_spawnMenu.Open && !InVehicle && Input.GetKey(KeyCode.C);
        Context.Update(this, Input.GetMouseButtonDown(0), _pointerOverContextUi);
    }

    private AudioSource? _hum;
    private float _stepTimer;

    /// <summary>Footsteps while walking and the physgun hum while holding something.</summary>
    private void AmbientAudio(float dt)
    {
        if (!InVehicle && Player.Speed > 0.5f && Player.IsGrounded)
        {
            _stepTimer -= dt * Player.Speed / 4.5f;
            if (_stepTimer <= 0f)
            {
                _stepTimer = 0.45f;
                Sounds.Play("sfx_footstep", 0.25f, null, 0.9f + Random.Shared.NextSingle() * 0.2f);
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
        if (Props.Props.Count >= MaxProps) return $"Prop limit reached ({MaxProps}). Clean up with Undo or Clear map.";
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
        PhysGun.Release(keepVelocity: false);
        ToolGun.CancelPending();
        Vehicles.ClearAll();
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
        _hud.Draw(paper, w, h, this, dt, chrome);
        _pointerOverContextUi = Context.PointerOverPanel(paper, w, h);
        Context.Draw(paper, w, h, dt, this);
        _spawnMenu.Draw(paper, w, h, dt, this);
        Menus.Draw(paper, w, h, dt, this);
    }
}
