using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public enum ActiveTool { PhysGun, ToolGun }

public sealed class SandboxGame : Game
{
    public SelfTest? SelfTest { get; set; }

    /// <summary>Optional forced UI state for screenshots: props, tools, toolgun, pause.</summary>
    public string? ShotState { get; set; }

    public Scene World { get; private set; } = null!;
    public PropManager Props { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public PhysGun PhysGun { get; private set; } = null!;
    public ToolGun ToolGun { get; private set; } = null!;
    public ActiveTool Tool { get; set; } = ActiveTool.PhysGun;
    public bool AimingAtProp { get; private set; }

    private readonly Hud _hud = new();
    private readonly IconRenderer _icons = new();
    private readonly PauseMenu _pause = new();
    private SpawnMenu _spawnMenu = null!;
    private readonly Queue<Action> _pending = new();
    private Shader _shader = null!;
    private int _frames, _shotToasts;

    public bool MenuOpen => _pause.Open || _spawnMenu.Open;

    /// <summary>Run an action at the next update (UI callbacks use this so scene edits happen outside the GUI pass).</summary>
    public void Enqueue(Action a) => _pending.Enqueue(a);

    public override void Initialize()
    {
        World = new Scene();
        _shader = Shader.LoadDefault(DefaultShader.Standard);
        MapBuilder.Build(World, _shader);
        Props = new PropManager(World, _shader);
        Player = new Player(World, new Float3(0f, 0.1f, -10f), 0f);
        PhysGun = new PhysGun(Props);
        ToolGun = new ToolGun(Props);
        _spawnMenu = new SpawnMenu(_icons);
        Scene.Load(World);

        // A few starter props so there's something to play with right away.
        Props.Spawn(PropKind.Cube, new Float3(-1f, 1f, -2f), 10f, recordUndo: false);
        Props.Spawn(PropKind.Cube, new Float3(0.2f, 2.5f, -2f), 35f, recordUndo: false);
        Props.Spawn(PropKind.Sphere, new Float3(1.5f, 1f, -2.5f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Barrel, new Float3(3f, 1f, -1f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Plank, new Float3(-3f, 0.5f, 0f), 30f, recordUndo: false);

        Console.WriteLine($"Sandbox ready: map built, {Props.Props.Count} starter props.");
        SelfTest?.Begin(this);
        ApplyShotState();
    }

    private void ApplyShotState()
    {
        switch (ShotState)
        {
            case "props": _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Spawnlists; break;
            case "tools": _spawnMenu.Open = true; _spawnMenu.Tab = MenuTab.Tools; SelectTool(ActiveTool.ToolGun, ToolMode.Balloon); break;
            case "toolgun":
                SelectTool(ActiveTool.ToolGun, ToolMode.Weld);
                Player.SetLook(0f, 14f);
                break;
            case "pause": _pause.Open = true; Time.TimeScale = 0f; break;
        }
    }

    public override void BeginRender()
    {
        // Thumbnails need a live GL context, so render them once on the second frame.
        if (!Application.IsHeadless && _frames == 2) _icons.RenderAll(_shader);
    }

    public override void EndUpdate()
    {
        _frames++;
        float dt = Time.UnscaledDeltaTime;
        _hud.Tick(dt);
        while (_pending.Count > 0) _pending.Dequeue()();

        if (SelfTest is not null)
        {
            Player.Update((float)Time.DeltaTime, allowInput: false);
            SelfTest.Step(this);
            return;
        }

        if (ShotState is null) HandleMenus();
        else if (ShotState == "toolgun" && _shotToasts < 3 && Time.UnscaledTotalTime > 7.5f + _shotToasts * 0.4f)
        {
            _hud.Notify(_shotToasts switch { 0 => "Spawned Cube #6", 1 => "Welded Cube #1 to Cube #2", _ => "Undone: Sphere #3" });
            _shotToasts++;
        }
        bool gameplay = !MenuOpen;

        if (gameplay) { if (!Input.CursorLocked) Input.LockCursor(); }
        else if (Input.CursorLocked) Input.UnlockCursor();

        Player.Update((float)Time.DeltaTime, gameplay && ShotState is null);
        AimingAtProp = Player.Trace(80f, out RaycastHit aim) && Props.FromBody(aim.Rigidbody) is not null;
        if (!gameplay)
        {
            PhysGun.Release(keepVelocity: false);
            return;
        }
        if (ShotState is not null) return;

        if (Input.GetKeyDown(KeyCode.V)) Player.ToggleNoclip();
        if (Input.GetKeyDown(KeyCode.Number1)) SelectTool(ActiveTool.PhysGun, null);
        if (Input.GetKeyDown(KeyCode.Number2)) SelectTool(ActiveTool.ToolGun, ToolMode.Weld);
        if (Input.GetKeyDown(KeyCode.Number3)) SelectTool(ActiveTool.ToolGun, ToolMode.Remover);
        if (Input.GetKeyDown(KeyCode.Number4)) SelectTool(ActiveTool.ToolGun, ToolMode.Balloon);
        if (Input.GetKeyDown(KeyCode.Z)) Undo();

        if (Tool == ActiveTool.PhysGun)
        {
            _hud.Notify(PhysGun.Update(Player, Input.GetMouseButtonDown(0), Input.GetMouseButton(0),
                Input.GetMouseButtonDown(1), Input.MouseWheelDelta));
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) _hud.Notify(ToolGun.Fire(Player));
            if (Input.GetMouseButtonDown(1) && ToolGun.PendingWeld is not null)
            {
                ToolGun.CancelPending();
                _hud.Notify("Weld selection cleared", ToastKind.Info);
            }
        }
    }

    private void HandleMenus()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_spawnMenu.Open) _spawnMenu.Open = false;
            else
            {
                _pause.Open = !_pause.Open;
                Time.TimeScale = _pause.Open ? 0f : 1f;
            }
        }

        switch (_pause.Requested)
        {
            case PauseAction.Resume:
                _pause.Open = false;
                Time.TimeScale = 1f;
                break;
            case PauseAction.ClearMap:
                ClearMap();
                _pause.Open = false;
                Time.TimeScale = 1f;
                break;
            case PauseAction.Quit:
                Quit();
                break;
        }
        _pause.Requested = PauseAction.None;

        bool held = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Tab);
        // Stay open while typing in the search box so letters like Q don't close it.
        _spawnMenu.Open = !_pause.Open && (held || _spawnMenu.SearchFocused);
    }

    public void SelectTool(ActiveTool tool, ToolMode? mode)
    {
        PhysGun.Release(keepVelocity: false);
        ToolGun.CancelPending();
        Tool = tool;
        if (mode is { } m) ToolGun.Mode = m;
    }

    public Prop SpawnAtCrosshair(PropKind kind)
    {
        Float3 pos;
        float half = PropManager.HalfHeightOf(kind);
        if (Player.Trace(40f, out RaycastHit hit))
            pos = hit.Point + hit.Normal * (half + 0.1f) + new Float3(0f, 0.1f, 0f);
        else
            pos = Player.EyePosition + Player.Forward * 5f;
        Prop p = Props.Spawn(kind, pos, Player.Yaw);
        _hud.Notify($"Spawned {p.Go.Name}");
        return p;
    }

    public void Undo() => _hud.Notify(Props.Undo() ?? "Nothing to undo");

    public void ClearMap()
    {
        PhysGun.Release(keepVelocity: false);
        ToolGun.CancelPending();
        Props.ClearAll();
        _hud.Notify("Map cleared", ToastKind.Danger);
    }

    public override void OnGui(Scene? scene, Paper paper)
    {
        base.OnGui(scene, paper);
        if (Application.IsHeadless) return;
        (float w, float h) = Ui.ScreenSize();
        Theme.UpdateScale(w, h);
        float dt = Time.UnscaledDeltaTime;

        _hud.Draw(paper, w, h, this, dt, chrome: !_spawnMenu.Visible && !_pause.Visible);
        _spawnMenu.Draw(paper, w, h, dt, this);
        _pause.Draw(paper, w, h, dt, this);
    }
}
