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
        Props = new PropManager(World, Shader);
        Player = new Player(World, new Float3(0f, 0.1f, -10f), 0f);
        PhysGun = new PhysGun(Props);
        ToolGun = new ToolGun(Props);
        Vehicles = new VehicleSystem(this);
        _spawnMenu = new SpawnMenu(_icons);
        Scene.Load(World);
        Sounds.Init(World, Player.CameraObject);

        Weapons.Add(new PhysGunWeapon());
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
            case "pause": Menus.Go(Screen.Paused); break;
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
        _hud.Tick(dt);
        while (_pending.Count > 0) _pending.Dequeue()();
        Time.TimeScale = Menus.Screen is Screen.Paused or Screen.Settings && Menus.Blocking && ShotState is null ? 0f : 1f;

        if (SelfTest is not null)
        {
            Player.Update(Time.DeltaTime, allowInput: false);
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
        if (Input.GetKeyDown(KeyCode.E) && Vehicles.TryUse()) return;
        if (InVehicle) return;
        if (Input.GetKeyDown(KeyCode.V)) Player.ToggleNoclip();
        if (Input.GetKeyDown(KeyCode.Z)) Undo();
        for (int k = 1; k <= 9; k++)
            if (Input.GetKeyDown(KeyCode.Number0 + k)) Weapons.SelectSlot(this, k);

        float wheel = Input.MouseWheelDelta;
        if (wheel != 0f && !Weapons.Current.CapturesWheel) { Weapons.Cycle(this, wheel > 0 ? -1 : 1); wheel = 0f; }

        Weapons.Tick(this, new WeaponInput(Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0),
            Input.GetMouseButtonDown(1), Input.GetMouseButton(1), Input.GetKeyDown(KeyCode.R), wheel), dt);
    }

    private void ShotTick()
    {
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
    public string Spawn(SpawnEntry entry)
    {
        Float3 pos;
        if (Player.Trace(40f, out RaycastHit hit))
            pos = hit.Point + hit.Normal * (entry.HalfHeight + 0.1f) + new Float3(0f, 0.1f, 0f);
        else
            pos = Player.EyePosition + Player.Forward * 5f;
        string name = entry.Spawn(this, pos, Player.Yaw);
        LastSpawned = entry;
        Notify($"Spawned {name}");
        Sounds.Play("sfx_spawn", 0.6f, pos);
        return name;
    }

    public SpawnEntry? LastSpawned { get; set; }

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

    public void ClearMap()
    {
        PhysGun.Release(keepVelocity: false);
        ToolGun.CancelPending();
        Props.ClearAll();
        Notify("Map cleared", ToastKind.Danger);
    }

    public void SaveMap()
    {
        try { Notify($"Saved {MapSave.Save(Props, MapSave.DefaultPath)} props"); }
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
