using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public enum ActiveTool { PhysGun, ToolGun }

public sealed class SandboxGame : Game
{
    public SelfTest? SelfTest { get; set; }

    public Scene World { get; private set; } = null!;
    public PropManager Props { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public PhysGun PhysGun { get; private set; } = null!;
    public ToolGun ToolGun { get; private set; } = null!;
    public ActiveTool Tool { get; set; } = ActiveTool.PhysGun;

    private readonly Hud _hud = new();
    private readonly SpawnMenu _spawnMenu = new();
    private readonly PauseMenu _pause = new();

    public override void Initialize()
    {
        World = new Scene();
        Shader shader = Shader.LoadDefault(DefaultShader.Standard);
        MapBuilder.Build(World, shader);
        Props = new PropManager(World, shader);
        Player = new Player(World, new Float3(0f, 0.1f, -10f), 0f);
        PhysGun = new PhysGun(Props);
        ToolGun = new ToolGun(Props);
        Scene.Load(World);

        // A few starter props so there's something to play with right away.
        Props.Spawn(PropKind.Cube, new Float3(-1f, 1f, -2f), 10f, recordUndo: false);
        Props.Spawn(PropKind.Cube, new Float3(0.2f, 2.5f, -2f), 35f, recordUndo: false);
        Props.Spawn(PropKind.Sphere, new Float3(1.5f, 1f, -2.5f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Barrel, new Float3(3f, 1f, -1f), 0f, recordUndo: false);
        Props.Spawn(PropKind.Plank, new Float3(-3f, 0.5f, 0f), 30f, recordUndo: false);

        Console.WriteLine($"Sandbox ready: map built, {Props.Props.Count} starter props.");
        SelfTest?.Begin(this);
    }

    public override void EndUpdate()
    {
        float dt = (float)Time.DeltaTime;
        _hud.Tick(dt);

        if (SelfTest is not null)
        {
            Player.Update(dt, allowInput: false);
            SelfTest.Step(this);
            return;
        }

        HandleMenus();
        bool gameplay = !_pause.Open && !_spawnMenu.Open;

        if (gameplay) { if (!Input.CursorLocked) Input.LockCursor(); }
        else if (Input.CursorLocked) Input.UnlockCursor();

        Player.Update(dt, gameplay);
        if (!gameplay)
        {
            PhysGun.Release(keepVelocity: false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.V)) Player.ToggleNoclip();
        if (Input.GetKeyDown(KeyCode.Number1)) SelectTool(ActiveTool.PhysGun, null);
        if (Input.GetKeyDown(KeyCode.Number2)) SelectTool(ActiveTool.ToolGun, ToolMode.Weld);
        if (Input.GetKeyDown(KeyCode.Number3)) SelectTool(ActiveTool.ToolGun, ToolMode.Remover);
        if (Input.GetKeyDown(KeyCode.Number4)) SelectTool(ActiveTool.ToolGun, ToolMode.Balloon);
        if (Input.GetKeyDown(KeyCode.Z)) Undo();

        if (Tool == ActiveTool.PhysGun)
        {
            _hud.Flash(PhysGun.Update(Player, Input.GetMouseButtonDown(0), Input.GetMouseButton(0),
                Input.GetMouseButtonDown(1), Input.MouseWheelDelta));
        }
        else if (Input.GetMouseButtonDown(0))
        {
            _hud.Flash(ToolGun.Fire(Player));
        }
    }

    private void HandleMenus()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _pause.Open = !_pause.Open;
            Time.TimeScale = _pause.Open ? 0f : 1f;
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

        _spawnMenu.Open = !_pause.Open && (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Tab));
        if (_spawnMenu.Requested is { } kind)
        {
            SpawnAtCrosshair(kind);
            _spawnMenu.Requested = null;
        }
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
        _hud.Flash($"Spawned {p.Go.Name}");
        return p;
    }

    public void Undo() => _hud.Flash(Props.Undo() ?? "Nothing to undo");

    public void ClearMap()
    {
        PhysGun.Release(keepVelocity: false);
        ToolGun.CancelPending();
        Props.ClearAll();
        _hud.Flash("Map cleared");
    }

    public override void OnGui(Scene? scene, Paper paper)
    {
        base.OnGui(scene, paper);
        if (Application.IsHeadless) return;
        (float w, float h) = Ui.ScreenSize();

        string toolLine, modeLine;
        if (Tool == ActiveTool.PhysGun)
        {
            toolLine = "Physgun";
            modeLine = PhysGun.Held is { } held
                ? $"Holding {held.Go.Name} — wheel push/pull, RMB freeze, release to drop"
                : "LMB grab a prop · grabbing a frozen (blue) prop unfreezes it";
        }
        else
        {
            toolLine = $"Toolgun — {ToolGun.Mode}";
            modeLine = ToolGun.PendingWeld is { } first
                ? $"Weld: {first.Go.Name} selected, click a second prop"
                : ToolGun.Describe(ToolGun.Mode);
        }

        _hud.Draw(paper, w, h, toolLine, modeLine, Props.Props.Count, Player.Noclip);
        _spawnMenu.Draw(paper, w, h);
        _pause.Draw(paper, w, h);
    }
}
