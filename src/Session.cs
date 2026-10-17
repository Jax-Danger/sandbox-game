using System.Diagnostics;
using System.Text.Json;
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Asynchronous session loader. File IO + JSON parsing run on a background task; the main thread only does
/// engine work (mesh/texture uploads, spawning) in batches capped at <see cref="BudgetMs"/> per frame,
/// so the loading screen stays responsive and the progress bar reflects real work.
/// </summary>
public sealed class SessionLoader
{
    public const double BudgetMs = 6.0;
    public WorldEntry World { get; }
    public string Stage { get; private set; } = "Loading assets...";
    public float Progress { get; private set; }
    public bool Done { get; private set; }
    public string? Error { get; private set; }
    public float Elapsed { get; private set; }

    private readonly SandboxGame _g;
    private readonly IEnumerator<bool> _work;
    private Task<(MapSave.SaveData World, PlayerSave? Player)>? _read;

    public SessionLoader(SandboxGame g, WorldEntry world)
    {
        _g = g;
        World = world;
        _work = Run().GetEnumerator();
    }

    public void Hold(float dt) => Elapsed += dt;

    /// <summary>Runs work until the frame budget is used. Returns true when finished.</summary>
    public bool Step(float dt)
    {
        Elapsed += dt;
        if (Done) return true;
        var sw = Stopwatch.StartNew();
        try
        {
            while (sw.Elapsed.TotalMilliseconds < BudgetMs)
            {
                if (!_work.MoveNext()) { Done = true; Progress = 1f; break; }
                if (!_work.Current) break; // true = keep going this frame, false = wait for next frame
            }
        }
        catch (Exception e)
        {
            Error = e.GetBaseException().Message;
            Log.Crash(e, "session load");
            Done = true;
        }
        return Done;
    }

    private IEnumerable<bool> Run()
    {
        SessionMeta meta = World.Meta;
        // 1. Assets: read + parse JSON off the main thread.
        Stage = "Loading assets...";
        string folder = World.Folder;
        _read = Task.Run(() =>
        {
            var w = Worlds.ReadJson<MapSave.SaveData>(Path.Combine(folder, "world.json")) ?? new MapSave.SaveData();
            var p = Worlds.ReadJson<PlayerSave>(Path.Combine(folder, "player.json"));
            return (w, p);
        });
        while (!_read.IsCompleted) { Progress = 0.02f + 0.06f * MathF.Min(1f, Elapsed / 0.5f); yield return false; }
        var (world, player) = _read.Result;
        Progress = 0.08f;

        // 2. Models: build (and upload) every placeholder mesh the save uses, one per step.
        Stage = "Loading models...";
        var kinds = world.Props.Select(p => Enum.TryParse(p.Kind, out PropKind k) ? k : PropKind.Cube).Distinct().ToList();
        for (int i = 0; i < kinds.Count; i++)
        {
            PropManager.WarmMesh(kinds[i]);
            Progress = 0.08f + 0.10f * (i + 1) / MathF.Max(1, kinds.Count);
            yield return false;
        }

        // 3. Shaders/materials: generate + upload the procedural textures (one GPU upload per frame).
        Stage = "Loading shaders...";
        var warm = ProcTex.WarmList();
        for (int i = 0; i < warm.Count; i++)
        {
            warm[i]();
            Progress = 0.18f + 0.10f * (i + 1) / MathF.Max(1, warm.Count);
            yield return false;
        }

        // 4. Physics world: map geometry + colliders, gravity.
        Stage = "Building physics...";
        Progress = 0.30f;
        yield return false;
        _g.ClearMap(notify: false);
        string? customFile = meta.Map == "custom" && meta.CustomMap is not null ? Path.Combine(folder, meta.CustomMap) : null;
        string? mapError = MapBuilder.Load(_g.World, _g.Shader, meta.Map, customFile);
        if (mapError is not null) _g.Notify($"Custom map: {mapError}", ToastKind.Danger);
        _g.ApplyModifiers(meta);
        Progress = 0.38f;
        yield return false;

        // 5. Entities, in time-budgeted batches.
        int total = MapSave.StepCount(world), done = 0;
        Stage = total == 0 ? "Spawning entities..." : $"Spawning entities... (0/{total})";
        foreach (int n in MapSave.LoadSteps(_g, world))
        {
            done = n;
            Stage = $"Spawning entities... ({done}/{total})";
            Progress = 0.38f + 0.55f * done / MathF.Max(1, total);
            yield return true;
        }
        if (meta.Modifiers.Npcs && meta.Mode == "Roleplay" && world.Props.Count == 0 && world.Ragdolls.Count == 0) _g.Roleplay.SpawnTownNpcs();

        // 6. Player.
        Stage = "Restoring player...";
        Progress = 0.95f;
        yield return false;
        _g.RestorePlayer(player, meta);
        Progress = 1f;
        // Let one frame render the finished world before the fade.
        yield return false;
    }
}

public sealed partial class SandboxGame
{
    public WorldEntry? Session { get; private set; }
    public SessionLoader? Loader { get; private set; }
    public SessionMeta? Meta => Session?.Meta;
    public bool IsRoleplay => Meta?.Mode == "Roleplay";
    public int PropLimit => Meta?.Modifiers.PropLimit ?? MaxProps;

    public const float AutosaveInterval = 5f;
    private float _autosaveTimer, _iconTimer;
    private Task? _saveTask;
    private readonly Stopwatch _playClock = new();

    public void EnterSession(WorldEntry w)
    {
        if (Session is not null) SaveSession(sync: true, icon: true);
        Session = w;
        Loader = new SessionLoader(this, w);
        Menus.Go(Screen.Loading);
        Sounds.Play("sfx_ui_open", 0.5f);
    }

    /// <summary>Called every frame while a loader is active.</summary>
    public void TickLoader(float dt)
    {
        if (Loader is null) return;
        if (Loader.Progress >= HoldLoaderAt) { Loader.Hold(dt); return; }
        if (Loader.Step(dt) && Loader.Elapsed > 0.6f)
        {
            if (Loader.Error is { } err) { Notify($"Load failed: {err}", ToastKind.Danger); Session = null; Loader = null; Menus.Go(Screen.Worlds); return; }
            Loader = null;
            Session!.Meta.LastPlayed = DateTime.Now;
            Worlds.WriteMeta(Session.Folder, Session.Meta);
            _autosaveTimer = 0f; _iconTimer = 1.5f;
            _playClock.Restart();
            Menus.Go(Screen.Playing);
            Notify($"Welcome to {Session.Meta.Name}");
            if (ShotState == "jobs") Menus.Go(Screen.Jobs);
            switch (ShotState)
            {
                case "town": Player.Teleport(new Float3(-3f, 0.2f, 2.5f)); Player.SetLook(8f, 2f); break;
                case "towndoor": Roleplay.Earn(0); Player.Teleport(new Float3(-26.3f, 0.2f, 18.6f)); Player.SetLook(0f, 8f); break;
                case "townair": if (!Player.Noclip) Player.ToggleNoclip(); Player.Teleport(new Float3(-30f, 34f, -52f)); Player.SetLook(28f, 34f); break;
                case "jail": Roleplay.Arrest("Arrested for the screenshot"); Player.SetLook(200f, 6f); break;
                case "bank": Roleplay.AtAtm = true; Menus.OpenBank(); break;
                case "lake":
                    Vehicles.SpawnBuggy(new Float3(42f, -1f, 30f), 200f, kind: "airboat");
                    for (int i = 0; i < 5; i++)
                    {
                        Prop p = Props.Spawn(i % 2 == 0 ? PropKind.SmallCrate : PropKind.Plank, new Float3(48f + i * 2.2f, -0.6f, 34f + (i % 3) * 2f), i * 30f, recordUndo: false);
                        p.SetSurface(SurfaceMaterial.Get("Wood"));
                    }
                    Player.Teleport(new Float3(28f, MapBuilder.HillsHeight(28f, 14f) + 0.3f, 14f)); Player.SetLook(45f, 12f); break;
            }
        }
    }

    public void ApplyModifiers(SessionMeta meta)
    {
        World.Physics.Gravity = new Float3(0f, -meta.Modifiers.Gravity, 0f);
        Player.Gravity = 20f * meta.Modifiers.Gravity / 9.81f;
        Weather.Set(WeatherSystem.Parse(meta.Modifiers.Weather), this, instant: true);
        Health.FallDamage = meta.Modifiers.FallDamage;
        DayNight.Enabled = meta.Modifiers.DayNight;
        Roleplay.Reset(meta);
    }

    public void RestorePlayer(PlayerSave? p, SessionMeta meta)
    {
        if (p is { Feet.Length: 3 })
        {
            Player.Teleport(new Float3(p.Feet[0], p.Feet[1], p.Feet[2]));
            Player.SetLook(p.Yaw, p.Pitch);
            if (p.Noclip != Player.Noclip) Player.ToggleNoclip();
            if (p.Weapon is { } id && Weapons.All.FirstOrDefault(x => x.Id == id) is { } wpn) Weapons.Select(this, wpn);
            Health.Set(p.Health, p.Armor);
            Roleplay.Restore(p);
        }
        else
        {
            Player.Teleport(MapBuilder.SpawnPoint);
            Player.SetLook(0f, 8f);
            Health.Set(100f, 0f);
            if (Player.Noclip) Player.ToggleNoclip();
        }
    }

    public PlayerSave CapturePlayer()
    {
        Float3 f = Player.FeetPosition;
        var p = new PlayerSave
        {
            Feet = [f.X, f.Y, f.Z], Yaw = Player.Yaw, Pitch = Player.Pitch, Noclip = Player.Noclip, Weapon = Weapons.Current?.Id,
            Health = Health.Hp, Armor = Health.Armor,
        };
        Roleplay.Capture(p);
        return p;
    }

    /// <summary>Captures on the main thread, then serialises + writes atomically on a background task (unless sync).</summary>
    /// <summary>Last save failure (read-only folder, disk full); null after a good save.</summary>
    public string? LastSaveError { get; private set; }

    public void SaveSession(bool sync = false, bool icon = false)
    {
        if (Session is not { } s || Loader is not null) return;
        if (!sync && _saveTask is { IsCompleted: false }) return; // previous autosave still writing
        _saveTask?.Wait(2000);
        MapSave.SaveData world = MapSave.Capture(this);
        PlayerSave player = CapturePlayer();
        s.Meta.LastPlayed = DateTime.Now;
        s.Meta.PropCount = world.Props.Count;
        s.Meta.PlaySeconds += _playClock.Elapsed.TotalSeconds;
        _playClock.Restart();
        string folder = s.Folder;
        // Snapshot the metadata on the main thread: the live object keeps changing while a background save runs.
        string metaJson = JsonSerializer.Serialize(s.Meta, Worlds.Json);
        if (icon) Worlds.CaptureIcon(Player.Camera, folder);
        void Write()
        {
            Worlds.WriteAtomic(Path.Combine(folder, "world.json"), JsonSerializer.Serialize(world, Worlds.Json));
            Worlds.WriteAtomic(Path.Combine(folder, "player.json"), JsonSerializer.Serialize(player, Worlds.Json));
            Worlds.WriteAtomic(Path.Combine(folder, "session.json"), metaJson);
        }
        if (sync) { try { Write(); LastSaveError = null; } catch (Exception e) { LastSaveError = e.Message; Log.Warn($"Save failed: {e.Message}"); Notify($"Couldn't save the world: {e.Message}", ToastKind.Danger); } }
        else _saveTask = Task.Run(() => { try { Write(); LastSaveError = null; } catch (Exception e) { LastSaveError = e.Message; Log.Warn($"Autosave failed: {e.Message}"); Enqueue(() => Notify($"Autosave failed: {e.Message}", ToastKind.Danger)); } });
    }

    private void TickAutosave(float dt)
    {
        if (Session is null || Loader is not null || Menus.Screen is not (Screen.Playing or Screen.Paused)) return;
        _autosaveTimer += dt;
        _iconTimer -= dt;
        if (_autosaveTimer >= AutosaveInterval)
        {
            _autosaveTimer = 0f;
            bool icon = _iconTimer <= 0f && Menus.Screen == Screen.Playing && !_spawnMenu.Visible && !Context.Visible;
            if (icon) _iconTimer = 60f;
            SaveSession(icon: icon);
        }
    }

    /// <summary>Save and leave the session (back to the Worlds list).</summary>
    public void ExitSession(Screen to = Screen.Worlds)
    {
        if (Session is not null)
        {
            SaveSession(sync: true, icon: Menus.Screen != Screen.Playing ? false : true);
            Session = null;
        }
        Loader = null;
        ClearMap(notify: false);
        MapBuilder.Load(World, Shader, "flatgrass", null);
        World.Physics.Gravity = new Float3(0f, -9.81f, 0f);
        Player.Gravity = 20f;
        Weather.Set(WeatherKind.Clear, this, instant: true);
        Roleplay.Reset(null);
        DayNight.Enabled = false;
        SpawnMenuBackdrop();
        Menus.Go(to);
    }

    /// <summary>Called by Program after the window closes, and on quit.</summary>
    public void SaveOnExit()
    {
        try { if (Session is not null && Loader is null) SaveSession(sync: true); }
        catch (Exception e) { Log.Warn($"Exit save failed: {e.Message}"); }
        _saveTask?.Wait(3000);
    }
}
