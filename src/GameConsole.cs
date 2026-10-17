using System.Globalization;
using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// Developer console / chat (` key). Lines starting with '/' or a known command run it; anything else is chat.
/// Commands are plain functions so the self-test can drive them headlessly via <see cref="Execute"/>.
/// </summary>
public sealed class GameConsole
{
    public bool Open { get; private set; }
    /// <summary>True on the frame the console closed, so Enter/Esc don't also hit the game.</summary>
    public bool JustClosed { get; private set; }
    private string _input = "";
    private readonly List<(string text, Color color)> _log = [];
    private readonly List<string> _history = [];
    private int _histIdx = -1, _focusFrames, _scroll;
    public IReadOnlyList<(string text, Color color)> Log => _log;
    public const int MaxLog = 200, Rows = 14;

    private sealed record Cmd(string Name, string Usage, string Help, Func<SandboxGame, string[], string?> Run);
    private readonly Dictionary<string, Cmd> _cmds = new(StringComparer.OrdinalIgnoreCase);

    public GameConsole()
    {
        Add("help", "help", "List commands", (g, a) => string.Join("\n", _cmds.Values.Select(c => $"{c.Usage}\t{c.Help}")));
        Add("clear", "clear", "Clear the console", (g, a) => { _log.Clear(); return null; });
        Add("noclip", "noclip", "Toggle flying", (g, a) => { g.Player.ToggleNoclip(); return g.Player.Noclip ? "Noclip on" : "Noclip off"; });
        Add("give", "give <weapon>", "Equip a weapon (pistol, smg, shotgun, rpg, ...)", (g, a) =>
        {
            if (a.Length == 0) return "Weapons: " + string.Join(", ", g.Weapons.All.Select(w => w.Id.Replace("weapon_", "")));
            string q = a[0].ToLowerInvariant();
            Weapon? w = g.Weapons.All.FirstOrDefault(x => x.Id == q || x.Id == "weapon_" + q || x.Name.Equals(a[0], StringComparison.OrdinalIgnoreCase));
            if (w is null) throw new ArgumentException($"Unknown weapon '{a[0]}'");
            g.Weapons.Select(g, w); return $"Equipped {w.Name}";
        });
        Add("time", "time <0-24>", "Set the time of day (hours)", (g, a) =>
        {
            float h = Clamp(Num(a, 0), 0f, 24f);
            g.DayNight.TimeOfDay = h / 24f % 1f; g.DayNight.Tick(g, 0f);
            return $"Time set to {(int)h:00}:{(int)(h % 1f * 60f):00}";
        });
        Add("gravity", "gravity <m/s²>", "World gravity (9.81 default, this session)", (g, a) =>
        {
            float v = Clamp(Num(a, 0), -50f, 50f);
            g.World.Physics.Gravity = new Float3(0f, -v, 0f); Player.Gravity = 20f * v / 9.81f;
            return $"Gravity {v:0.##} m/s²";
        });
        Add("timescale", "timescale <0.1-4>", "Game speed", (g, a) => { g.Settings.TimeScale = Clamp(Num(a, 0), 0.1f, 4f); return $"Time scale {g.Settings.TimeScale:0.##}x"; });
        Add("cleanup", "cleanup", "Remove every prop and vehicle", (g, a) => { int n = g.Props.Props.Count; g.ClearMap(notify: false); return $"Cleaned up {n} props"; });
        Add("spawn", "spawn <prop> [count]", "Spawn props in front of you", (g, a) =>
        {
            if (a.Length == 0) return "Props: " + string.Join(", ", PropManager.AllKinds.Select(k => k.ToString().ToLowerInvariant()));
            if (!Enum.TryParse(a[0], true, out PropKind kind) || kind == PropKind.Limb) throw new ArgumentException($"Unknown prop '{a[0]}'");
            int n = a.Length > 1 ? (int)Clamp(Num(a, 1), 1, 20) : 1;
            Float3 at = g.Player.EyePosition + g.Player.Forward * 3f;
            for (int i = 0; i < n; i++) g.Props.Spawn(kind, at + new Float3(0f, i * (PropManager.HalfHeightOf(kind) * 2f + 0.05f), 0f), g.Player.Yaw);
            return $"Spawned {n}× {PropManager.DisplayName(kind)}";
        });
        Add("tp", "tp <x> <y> <z>", "Teleport (feet position)", (g, a) =>
        {
            var p = new Float3(Num(a, 0), Num(a, 1), Num(a, 2));
            g.Player.Teleport(p); return $"Teleported to {p.X:0.#} {p.Y:0.#} {p.Z:0.#}";
        });
        Add("pos", "pos", "Print your position", (g, a) => { Float3 f = g.Player.FeetPosition; return $"{f.X:0.##} {f.Y:0.##} {f.Z:0.##} yaw {g.Player.Yaw:0}"; });
        Add("heal", "heal", "Restore health and armor", (g, a) => { g.Health.Heal(Health0); g.Health.AddArmor(100f); return "Healed"; });
        Add("kill", "kill", "Respawn yourself", (g, a) => { if (g.Player.Noclip) g.Player.ToggleNoclip(); g.Health.Damage(g, 10000f, "console"); return null; });
        Add("photo", "photo", "Toggle photo mode (hides the HUD, F1)", (g, a) => { g.PhotoMode = !g.PhotoMode; return g.PhotoMode ? "Photo mode on (F1 to exit)" : "Photo mode off"; });
        Add("screenshot", "screenshot", "Save a screenshot (F12)", (g, a) => { g.RequestScreenshot(); return "Screenshot queued"; });
        Add("achievements", "achievements", "List achievements", (g, a) =>
            $"{g.Achievements.Unlocked.Count}/{Achievements.All.Length} unlocked\n" + string.Join("\n", Achievements.All.Select(x => $"{(g.Achievements.Has(x.Id) ? "✓ " : "· ")}{x.Name}\t{x.Description}{(x.Roleplay ? " (RP)" : "")}")));
        Add("weather", "weather <preset>", "Clear, Overcast, Fog, Rain, Storm", (g, a) =>
        {
            if (a.Length == 0) return $"Weather: {g.Weather.Kind}. Presets: {string.Join(", ", WeatherSystem.Names)}";
            if (!Enum.TryParse(a[0], true, out WeatherKind k) || !Enum.IsDefined(k)) throw new ArgumentException($"Unknown weather '{a[0]}'");
            g.Weather.Set(k, g); return $"Weather → {k}";
        });
        Add("buddy", "buddy add|remove|list <name>", "Who may touch your props under prop protection", (g, a) =>
        {
            var pp = g.Props.Protection; string me = PropProtection.LocalPlayer;
            string sub = a.Length > 0 ? a[0].ToLowerInvariant() : "list";
            string name = a.Length > 1 ? string.Join(' ', a[1..]) : "";
            string r = sub switch
            {
                "add" when name.Length > 0 => pp.AddBuddy(me, name) ? $"{name} can now touch your props" : $"Couldn't add {name}",
                "remove" when name.Length > 0 => pp.RemoveBuddy(me, name) ? $"{name} removed" : $"{name} isn't a buddy",
                "list" => pp.BuddiesOf(me).Count == 0 ? "No buddies yet (buddy add <name>)" : "Buddies: " + string.Join(", ", pp.BuddiesOf(me)),
                _ => "Usage: buddy add|remove|list <name>",
            };
            g.Settings.Buddies = pp.BuddiesOf(me).ToList(); g.Settings.Save();
            return r;
        });
        Add("pp", "pp off|rp|always", "Prop protection mode (rp = Roleplay only)", (g, a) =>
        {
            if (a.Length == 0) return $"Prop protection: {g.Props.Protection.Mode}{(g.Props.Protection.Enforced ? " (active)" : "")}";
            string m = a[0].ToLowerInvariant();
            if (m is not ("off" or "rp" or "always")) return "Usage: pp off|rp|always";
            g.Props.Protection.Mode = g.Settings.PropProtection = m; g.Settings.Save();
            return $"Prop protection: {m}";
        });
        Add("pp_owner", "pp_owner [name]", "Show or set the owner of the prop you're looking at", (g, a) =>
        {
            if (!g.Player.Trace(80f, out var hit) || g.Props.FromBody(hit.Rigidbody) is not { } p) return "Look at a prop";
            if (a.Length == 0) return $"{p.Go.Name} is owned by {p.Owner ?? "nobody"}";
            if (!g.Props.Protection.CanTouch(p, out string? deny)) return deny ?? "Not yours";
            p.Owner = string.Join(' ', a)[..Math.Min(32, string.Join(' ', a).Length)];
            return $"{p.Go.Name} now belongs to {p.Owner}";
        });
        Add("hints", "hints reset", "Replay the first-time hints", (g, a) => { g.Hints.Reset(g); g.Settings.ShowHints = true; return "Hints will show again"; });
        Add("echo", "echo <text>", "Print text", (g, a) => string.Join(' ', a));
    }

    private const float Health0 = HealthSystem.MaxHp;

    private void Add(string name, string usage, string help, Func<SandboxGame, string[], string?> run) => _cmds[name] = new Cmd(name, usage, help, run);
    public IEnumerable<string> Commands => _cmds.Keys;

    private static float Clamp(float v, float lo, float hi) => Math.Clamp(v, lo, hi);
    private static float Num(string[] a, int i)
    {
        if (i >= a.Length) throw new ArgumentException("Missing number");
        if (!float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || !float.IsFinite(v))
            throw new ArgumentException($"'{a[i]}' is not a number");
        return v;
    }

    public void Print(string text, Color? color = null)
    {
        foreach (string line in text.Replace("\r", "").Split('\n')) _log.Add((line, color ?? Theme.TextMuted));
        if (_log.Count > MaxLog) _log.RemoveRange(0, _log.Count - MaxLog);
        _scroll = 0;
    }

    /// <summary>Runs one line. Returns false if it was an error. Unknown words without '/' are chat.</summary>
    public bool Execute(SandboxGame g, string line)
    {
        line = line.Trim();
        if (line.Length == 0) return true;
        if (_history.Count == 0 || _history[^1] != line) _history.Add(line);
        if (_history.Count > 50) _history.RemoveAt(0);
        _histIdx = -1;
        bool slash = line.StartsWith('/');
        string[] parts = line.TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;
        if (!_cmds.TryGetValue(parts[0], out Cmd? cmd))
        {
            if (slash) { Print($"Unknown command '{parts[0]}'. Type help.", Theme.Danger); return false; }
            Print($"{(g.IsRoleplay ? g.Roleplay.Job.Name : "You")}: {line}", Theme.Text);
            return true;
        }
        Print("> " + line, Theme.TextFaint);
        if (cmd.Name is not ("help" or "echo" or "clear" or "achievements")) g.Achievements.Event("hacker");
        try
        {
            string? r = cmd.Run(g, parts[1..]);
            if (r is not null) Print(r);
            return true;
        }
        catch (ArgumentException e) { Print($"{e.Message}. Usage: {cmd.Usage}", Theme.Danger); return false; }
        catch (Exception e) { Print($"{cmd.Name} failed: {e.Message}", Theme.Danger); return false; }
    }

    public void Toggle(bool open)
    {
        if (Open == open) return;
        Open = open;
        _focusFrames = open ? 3 : 0;
        if (!open) { JustClosed = true; _input = ""; }
    }

    /// <summary>Per-frame keyboard handling. Call before game input.</summary>
    public void Tick(SandboxGame g)
    {
        JustClosed = false;
        if (KeyCapture.Active) return;
        if (!Open)
        {
            if (Input.GetKeyDown(KeyCode.GraveAccent) && g.Menus.Screen == Screen.Playing) Toggle(true);
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.GraveAccent)) { Toggle(false); return; }
        if (Input.GetKeyDown(KeyCode.Enter) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string line = _input.Replace("`", "");
            _input = "";
            Execute(g, line);
            if (line.Length == 0) Toggle(false);
            _focusFrames = 2;
        }
        float wheel = Input.MouseWheelDelta;
        if (wheel != 0f) _scroll = Math.Clamp(_scroll + (wheel > 0 ? 3 : -3), 0, Math.Max(0, _log.Count - Rows));
        if (Input.GetKeyDown(KeyCode.PageUp)) _scroll = Math.Clamp(_scroll + Rows - 1, 0, Math.Max(0, _log.Count - Rows));
        if (Input.GetKeyDown(KeyCode.PageDown)) _scroll = Math.Max(0, _scroll - (Rows - 1));
        if (_history.Count > 0 && Input.GetKeyDown(KeyCode.Up))
        {
            _histIdx = _histIdx < 0 ? _history.Count - 1 : Math.Max(0, _histIdx - 1);
            _input = _history[_histIdx]; _focusFrames = 2;
        }
        if (_histIdx >= 0 && Input.GetKeyDown(KeyCode.Down))
        {
            _histIdx++;
            _input = _histIdx < _history.Count ? _history[_histIdx] : "";
            if (_histIdx >= _history.Count) _histIdx = -1;
            _focusFrames = 2;
        }
    }

    public void Draw(Paper paper, float w, float h)
    {
        if (!Open) return;
        float pad = Theme.S(12), pw = MathF.Min(w - Theme.S(32), Theme.S(620)), lineH = Theme.S(Theme.Body) * 1.45f;
        int rows = Rows;
        float ih = Theme.S(30), ph = pad * 3f + rows * lineH + ih;
        float x = Theme.S(20), y = h - ph - Theme.S(104);
        Ui.Glass(paper, "con_bg", x, y, pw, ph, Theme.S(Theme.Radius));
        Ui.Text(paper, "con_title", x + pad, y + Theme.S(6), pw, Theme.S(16), "CONSOLE  ·  help for commands  ·  wheel/PgUp scrolls  ·  Esc closes", Theme.Micro, Theme.TextFaint, Theme.SemiBold);
        int end = Math.Max(0, _log.Count - _scroll), start = Math.Max(0, end - rows);
        if (_scroll > 0) Ui.Text(paper, "con_more", x + pad, y + Theme.S(6), pw - pad * 2f, Theme.S(16), $"↓ {_scroll} newer · PgDn", Theme.Micro, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
        for (int i = start; i < end; i++)
        {
            (string text, Color color) = _log[i];
            float ly = y + pad + Theme.S(8) + (i - start) * lineH;
            int tab = text.IndexOf('\t');
            if (tab < 0) Ui.Text(paper, "con_l" + (i - start), x + pad, ly, pw - pad * 2f, lineH, text, Theme.Body, color);
            else
            {
                float col = Theme.S(170);
                Ui.Text(paper, "con_l" + (i - start), x + pad, ly, col, lineH, text[..tab], Theme.Body, Theme.Text, Theme.SemiBold);
                Ui.Text(paper, "con_h" + (i - start), x + pad + col, ly, pw - pad * 2f - col, lineH, text[(tab + 1)..], Theme.Body, color);
            }
        }
        float iy = y + ph - pad - ih;
        Ui.Panel(paper, "con_in_bg", x + pad, iy, pw - pad * 2f, ih, Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        var field = paper.Box("con_in").PositionType(PositionType.SelfDirected).Position(x + pad * 2f, iy + (ih - lineH) / 2f).Size(pw - pad * 4f, lineH)
            .FontSize(Theme.S(Theme.Body))
            .TextField(_input, Theme.Regular, v => _input = v.Replace("`", ""), Theme.ToVector(Theme.Text), "Type a command or chat…", Theme.ToVector(Theme.TextFaint));
        if (_focusFrames > 0) { _focusFrames--; paper.SetFocus(field._handle); }
    }
}
