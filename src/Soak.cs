using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Scripted soak / fuzz run (dotnet Sandbox.dll --soak [seconds] [seed]). Every frame it does a random gameplay action:
/// spawns every catalog entry, uses every tool and weapon, welds/ropes/dupes, saves and reloads (diffing world state),
/// creates/enters/exits worlds (unicode names), switches modes, opens menus, changes settings, dies, drives vehicles,
/// imports a marker map. It watches for exceptions, NaNs, physics explosions and memory growth, and prints a report.
/// </summary>
public sealed class Soak(float seconds, int seed)
{
    private readonly Random _r = new(seed);
    private readonly Dictionary<string, int> _errors = [];
    private readonly List<string> _diffs = [];
    private readonly List<(double T, long Managed, long Rss, int Objects, string Top)> _mem = [];
    private SandboxGame? _g;
    private readonly Dictionary<string, int> _actions = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _frame, _nanHits, _explosions, _maxProps;
    private bool _reported;
    private WorldEntry? _world;
    public int ExitCode { get; private set; } = 1;

    public void Begin(SandboxGame g)
    {
        _g = g;
        string root = Path.Combine(Path.GetTempPath(), $"soak saves ü {Environment.ProcessId}");
        Worlds.Root = root;
        Settings.FilePath = Path.Combine(root, "settings test.json");
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (e.Exception is OperationCanceledException or FileNotFoundException or DirectoryNotFoundException) return;
            string key = $"first-chance {e.Exception.GetType().Name}: {e.Exception.Message}";
            string st = string.Join(" <- ", (Environment.StackTrace.Split('\n').Where(l => l.Contains(" Sandbox.") && !l.Contains("Soak.")).Take(4).Select(l => l.Trim().Replace("at Sandbox.", ""))));
            Note($"{key} [{Regex.Replace(st, @" in /.*?:line", ":")}]");
        };
        Console.WriteLine($"SOAK start: {seconds:0}s seed {seed}, saves '{root}'");
    }

    private void Note(string what)
    {
        lock (_errors) { _errors[what] = _errors.GetValueOrDefault(what) + 1; }
    }

    private Float3 RandPos(float y = 2f) => new((float)(_r.NextDouble() * 60 - 30), y + (float)_r.NextDouble() * 3f, (float)(_r.NextDouble() * 60 - 30));

    private void Aim(SandboxGame g, Float3 target)
    {
        Float3 d = target - g.Player.EyePosition;
        if (Float3.Length(d) < 1e-3f) return;
        d = Float3.Normalize(d);
        g.Player.SetLook(MathF.Atan2(d.X, d.Z) * 180f / MathF.PI, -MathF.Asin(Math.Clamp(d.Y, -1f, 1f)) * 180f / MathF.PI);
    }

    private Prop? RandProp(SandboxGame g) => g.Props.Props.Count == 0 ? null : g.Props.Props[_r.Next(g.Props.Props.Count)];

    public void Step(SandboxGame g)
    {
        _frame++;
        if (_reported) return;
        if (_clock.Elapsed.TotalSeconds > seconds) { Report(g); return; }
        if (_frame % 300 == 1) SampleMemory();
        if (_frame % 20 == 0) CheckPhysics(g);
        _maxProps = Math.Max(_maxProps, g.Props.Props.Count);
        if (g.Menus.Screen == Screen.Loading) return;

        (string Name, Action<SandboxGame> Do, int Weight)[] actions =
        [
            ("spawn", Spawn, 30), ("tool", UseTool, 20), ("weapon", UseWeapon, 15), ("link", Link, 8), ("dupe", Dupe, 3),
            ("saveload", SaveLoad, 2), ("menu", Menu, 6), ("settings", ChangeSettings, 3), ("die", Die, 1), ("vehicle", Vehicle, 4),
            ("clear", Clear, 1), ("undo", G => G.Undo(), 4), ("world", WorldCycle, 1), ("custommap", CustomMap, 1), ("gravity", Gravity, 2),
            ("ragdoll", Ragdolls, 2), ("context", ContextEdit, 3),
        ];
        int total = actions.Sum(a => a.Weight), pick = _r.Next(total);
        foreach (var a in actions)
        {
            if ((pick -= a.Weight) >= 0) continue;
            _actions[a.Name] = _actions.GetValueOrDefault(a.Name) + 1;
            try { a.Do(g); }
            catch (Exception e)
            {
                string st = e.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("Sandbox."))?.Trim() ?? "";
                Note($"{a.Name}: {e.GetType().Name}: {e.Message} {st}");
            }
            break;
        }
        if (g.Props.Props.Count > SandboxGame.MaxProps - 20) g.ClearMap(notify: false);
    }

    private void Spawn(SandboxGame g)
    {
        var entries = SpawnCatalog.Entries.Where(e => e.Tab != MenuTab.Weapons).ToList();
        SpawnEntry e = entries[_r.Next(entries.Count)];
        g.Player.Teleport(RandPos(0.2f) with { Y = 0.2f });
        Aim(g, g.Player.FeetPosition + new Float3((float)_r.NextDouble() * 4 - 2, 0f, 4f));
        g.Spawn(e);
    }

    private void UseTool(SandboxGame g)
    {
        Prop? p = RandProp(g);
        if (p is null) return;
        var tools = ToolGun.AllTools.ToList();
        ToolInfo t = tools[_r.Next(tools.Count)];
        g.SelectTool(t.Mode);
        foreach (ToolSetting s in g.ToolGun.SettingsFor(t.Mode))
            if (!s.IsKey) s.Set(s.Min + (float)_r.NextDouble() * (s.Max - s.Min));
        Float3 at = p.Go.Transform.Position;
        g.Player.Teleport(at + new Float3(0f, 0f, -3f) with { Y = MathF.Max(0.2f, at.Y - 1.5f) });
        Aim(g, at);
        g.Notify(g.ToolGun.Fire(g.Player));
        if (_r.Next(3) == 0) { Prop? q = RandProp(g); if (q is not null) { Aim(g, q.Go.Transform.Position); g.Notify(g.ToolGun.Fire(g.Player)); } }
        if (_r.Next(4) == 0) g.Notify(g.ToolGun.Secondary(g.Player));
    }

    private void UseWeapon(SandboxGame g)
    {
        var all = g.Weapons.All.Where(w => w.Available(g)).ToList();
        Weapon w = all[_r.Next(all.Count)];
        g.Weapons.Select(g, w);
        if (RandProp(g) is { } p) Aim(g, p.Go.Transform.Position);
        for (int i = 0; i < 3; i++)
        {
            bool down = _r.Next(2) == 0, sec = _r.Next(4) == 0;
            g.Weapons.Tick(g, new WeaponInput(down, down, !down, sec, sec, _r.Next(8) == 0, (float)(_r.NextDouble() - 0.5), _r.Next(10) == 0), 1f / 60f);
        }
    }

    private void Link(SandboxGame g)
    {
        Prop? a = RandProp(g), b = RandProp(g);
        if (a is null || b is null || a == b || a.Kind == PropKind.Limb || b.Kind == PropKind.Limb) return;
        switch (_r.Next(4))
        {
            case 0: g.ToolGun.Weld(a, b); break;
            case 1: g.ToolGun.Rope(a, Float3.Zero, b, Float3.Zero); break;
            case 2: g.ToolGun.Elastic(a, Float3.Zero, b, Float3.Zero, 50f); break;
            default: g.ToolGun.NoCollide(a, b); break;
        }
    }

    private void Dupe(SandboxGame g)
    {
        Prop? p = RandProp(g);
        if (p is null || p.Kind == PropKind.Limb) return;
        var d = Dupes.Copy(g, p);
        Dupes.Paste(g, d, RandPos(1f), (float)_r.NextDouble() * 360f);
        if (_r.Next(3) == 0) { Dupes.Save($"Soak dupé {_r.Next(100)} / ✓", d); _ = Dupes.List(); }
    }

    private static readonly Regex Num = new(@"-?\d+(\.\d+)?(E[-+]?\d+)?", RegexOptions.Compiled);

    /// <summary>Structural diff of two saves: identical JSON skeleton, numbers equal within <paramref name="tol"/>. Null = same.</summary>
    public static string? Diff(MapSave.SaveData x, MapSave.SaveData y, double tol = 0.06)
    {
        string a = JsonSerializer.Serialize(x), b = JsonSerializer.Serialize(y);
        var na = Num.Matches(a); var nb = Num.Matches(b);
        string sa = Num.Replace(a, "#"), sb = Num.Replace(b, "#");
        if (sa != sb)
        {
            int i = 0; while (i < Math.Min(sa.Length, sb.Length) && sa[i] == sb[i]) i++;
            int s0 = Math.Max(0, i - 100);
            return $"shape differs: …{sa.Substring(s0, Math.Min(200, sa.Length - s0))}… vs …{sb.Substring(s0, Math.Min(200, sb.Length - s0))}…";
        }
        for (int i = 0; i < na.Count; i++)
        {
            double va = double.Parse(na[i].Value, System.Globalization.CultureInfo.InvariantCulture), vb = double.Parse(nb[i].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(va - vb) > tol)
            {
                int at = na[i].Index, s0 = Math.Max(0, at - 120);
                return $"value {va} -> {vb} near …{a.Substring(s0, Math.Min(160, a.Length - s0))}…";
            }
        }
        return null;
    }

    private void SaveLoad(SandboxGame g)
    {
        string path = Path.Combine(Worlds.Root, "quick sävé.json");
        MapSave.SaveData before = MapSave.Capture(g);
        MapSave.Save(g, path);
        MapSave.Load(g, path);
        MapSave.SaveData after = MapSave.Capture(g);
        // Ragdolls are re-stood on the ground at load (feet lifted a few cm), so their height is not compared.
        foreach (var r in before.Ragdolls.Concat(after.Ragdolls)) if (r.Feet.Length > 1) r.Feet[1] = 0f;
        if (Diff(before, after) is { } d) _diffs.Add($"props {before.Props.Count}->{after.Props.Count}: {d}");
    }

    private void Menu(SandboxGame g)
    {
        Screen[] screens = [Screen.Playing, Screen.Paused, Screen.Settings, Screen.Playing];
        g.Menus.Go(screens[_r.Next(screens.Length)]);
        if (_r.Next(2) == 0) g.Menus.SettingsTab = _r.Next(4);
        g.Menus.Go(Screen.Playing);
    }

    private void ChangeSettings(SandboxGame g)
    {
        Settings s = g.Settings;
        s.FieldOfView = 60 + _r.Next(50); s.MouseSensitivity = 0.2f + (float)_r.NextDouble() * 3f; s.UiScale = 0.8f + (float)_r.NextDouble() * 0.6f;
        s.TimeScale = 0.25f + (float)_r.NextDouble() * 1.5f;
        if (_r.Next(3) == 0) Binds.Set("use", (KeyCode)Enum.GetValues<KeyCode>().GetValue(_r.Next(Enum.GetValues<KeyCode>().Length))!);
        g.ApplySettingsLive();
        s.Save();
        Settings back = Settings.Load();
        if (MathF.Abs(back.FieldOfView - s.Clamped().FieldOfView) > 0.01f) Note($"settings round trip FOV {s.FieldOfView} -> {back.FieldOfView}");
        s.TimeScale = 1f;
    }

    private void Die(SandboxGame g) { if (g.Player.Noclip) g.Player.ToggleNoclip(); g.Health.Damage(g, 500f, "soak"); }

    private void Vehicle(SandboxGame g)
    {
        if (g.InVehicle) { g.Vehicles.Exit(); return; }
        if (g.Vehicles.All.Count == 0) { g.Vehicles.SpawnBuggy(RandPos(1f), 0f, kind: new[] { "buggy", "jeep", "airboat" }[_r.Next(3)]); return; }
        Buggy b = g.Vehicles.All[_r.Next(g.Vehicles.All.Count)];
        g.Vehicles.Enter(b);
        for (int i = 0; i < 5; i++) b.Drive((float)_r.NextDouble() * 2 - 1, _r.Next(2) == 0, _r.Next(4) == 0, _r.Next(6) == 0);
        if (_r.Next(5) == 0) b.Flip();
    }

    private void Clear(SandboxGame g) => g.ClearMap(notify: false);
    private void Gravity(SandboxGame g) => g.SetGravity(_r.Next(4) == 0 ? 0f : (float)_r.NextDouble() * 25f);

    private void Ragdolls(SandboxGame g)
    {
        if (_r.Next(2) == 0) Ragdoll.Spawn(g, RandPos(0.5f), 0f);
        else EntityCatalog.SpawnDummy(g, RandPos(0f) with { Y = 0f }, 0f, team: (NpcTeam)_r.Next(3));
    }

    private void ContextEdit(SandboxGame g)
    {
        Prop? p = RandProp(g);
        if (p is null) return;
        g.Context.Selected = p;
        switch (_r.Next(4))
        {
            case 0: p.SetMass(0.1f + (float)_r.NextDouble() * 500f); break;
            case 1: p.SetSurface(SurfaceMaterial.All[_r.Next(SurfaceMaterial.All.Length)]); break;
            case 2: p.SetFrozen(!p.Frozen); break;
            default: g.Props.Remove(p); break;
        }
    }

    private int _worldStage;
    private void WorldCycle(SandboxGame g)
    {
        if (g.Session is null)
        {
            string[] names = ["Mÿ Wörld 🚀 test", "  spaced   name  ", "CON", "日本語の世界", "a/b\\c:d*e?f", new string('x', 90), "..", "Mÿ Wörld 🚀 test"];
            string mode = _r.Next(2) == 0 ? "Roleplay" : "Sandbox";
            string map = new[] { "flatgrass", "hills", "town" }[_r.Next(3)];
            _world = Worlds.Create(new SessionMeta { Name = names[_worldStage++ % names.Length], Mode = mode, Map = map });
            g.EnterSession(_world);
        }
        else
        {
            g.SaveSession(sync: _r.Next(2) == 0);
            if (_r.Next(2) == 0) { g.ExitSession(); if (_world is not null && _r.Next(3) == 0) { var w = Worlds.Rename(_world, _world.Meta.Name); if (w.Folder != _world.Folder) Note($"rename to same name moved folder: {Path.GetFileName(_world.Folder)} -> {Path.GetFileName(w.Folder)}"); } }
        }
    }

    private void CustomMap(SandboxGame g)
    {
        if (g.Session is not null) return;
        Directory.CreateDirectory(Worlds.Root);
        string path = Path.Combine(Worlds.Root, "märker map.gltf");
        File.WriteAllText(path, SelfTest.MarkerGltf());
        string? err = MapBuilder.Load(g.World, g.Shader, "custom", path);
        if (err is not null) Note("custom map: " + err);
    }

    private void CheckPhysics(SandboxGame g)
    {
        foreach (Prop p in g.Props.Props)
        {
            if (!p.Alive) { Note("dead prop still listed"); continue; }
            Float3 pos = p.Go.Transform.Position, v = p.Body.LinearVelocity;
            if (float.IsNaN(pos.X + pos.Y + pos.Z) || float.IsNaN(v.X + v.Y + v.Z)) { _nanHits++; Note($"NaN on {p.Kind} {p.Go.Name}"); g.Props.Remove(p); return; }
            if (Float3.Length(v) > 300f) { _explosions++; Note($"physics explosion: {p.Kind} at {Float3.Length(v):0} m/s"); }
            if (pos.Y < -500f) Note($"prop fell out of the world: {p.Kind}");
        }
        foreach (Buggy b in g.Vehicles.All)
            if (float.IsNaN(b.Go.Transform.Position.X)) { _nanHits++; Note("NaN vehicle"); }
        if (float.IsNaN(g.Player.FeetPosition.X)) { _nanHits++; Note("NaN player"); g.Player.Teleport(MapBuilder.SpawnPoint); }
    }

    private void SampleMemory()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var objs = _g?.World.AllObjects.ToList() ?? [];
        string top = string.Join(", ", objs.GroupBy(o => Regex.Replace(o.Name, @"[\d.]+", "#")).OrderByDescending(x => x.Count()).Take(4).Select(x => $"{x.Key}:{x.Count()}"));
        _mem.Add((_clock.Elapsed.TotalSeconds, GC.GetTotalMemory(true), Process.GetCurrentProcess().WorkingSet64, objs.Count, top));
    }

    private void Report(SandboxGame g)
    {
        _reported = true;
        if (g.Session is not null) g.ExitSession();
        g.ClearMap(notify: false);
        SampleMemory();
        Console.WriteLine($"SOAK done: {_frame} frames, {_actions.Values.Sum()} actions ({string.Join(", ", _actions.OrderByDescending(a => a.Value).Select(a => $"{a.Key} {a.Value}"))}), max props {_maxProps}");
        Console.WriteLine($"SOAK NaN {_nanHits}, explosions {_explosions}, save/load mismatches {_diffs.Count}");
        foreach (string d in _diffs.Take(5)) Console.WriteLine("SOAK diff: " + d);
        foreach (var m in _mem) Console.WriteLine($"SOAK mem t={m.T:0}s managed {m.Managed / 1048576.0:0.0} MB rss {m.Rss / 1048576.0:0} MB objects {m.Objects} [{m.Top}]");
        foreach (string c in BigCollections(g)) Console.WriteLine("SOAK collection " + c);
        foreach (var e in _errors.OrderByDescending(e => e.Value)) Console.WriteLine($"SOAK error x{e.Value}: {e.Key}");
        ExitCode = _errors.Count == 0 && _diffs.Count == 0 && _nanHits == 0 ? 0 : 1;
        Console.WriteLine(ExitCode == 0 ? "SOAK clean." : "SOAK found problems.");
    }

    /// <summary>Leak finder: every static collection in the game assembly, and every collection held by the game
    /// object and its direct subsystems, with more than 64 items after a full clear.</summary>
    private static IEnumerable<string> BigCollections(SandboxGame g)
    {
        const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        int Count(object? o) => o is System.Collections.ICollection c ? c.Count : -1;
        foreach (Type t in typeof(SandboxGame).Assembly.GetTypes().Where(t => !t.IsGenericTypeDefinition))
            foreach (var f in t.GetFields(all | System.Reflection.BindingFlags.Static))
            {
                object? v; try { v = f.GetValue(null); } catch { continue; }
                if (Count(v) > 64) yield return $"static {t.Name}.{f.Name} = {Count(v)}";
            }
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<(object O, string Path, int Depth)>();
        queue.Enqueue((g, "game", 0));
        while (queue.Count > 0)
        {
            var (o, path, depth) = queue.Dequeue();
            if (!seen.Add(o)) continue;
            foreach (var f in o.GetType().GetFields(all | System.Reflection.BindingFlags.Instance))
            {
                object? v; try { v = f.GetValue(o); } catch { continue; }
                if (v is null || v is string || f.FieldType.IsValueType) continue;
                if (Count(v) > 64) yield return $"{path}.{f.Name} = {Count(v)}";
                else if (depth < 2 && v.GetType().Namespace == "Sandbox") queue.Enqueue((v, $"{path}.{f.Name}", depth + 1));
            }
        }
    }
}
