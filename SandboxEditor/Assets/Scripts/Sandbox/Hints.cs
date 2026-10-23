using Prowl.PaperUI;
using Prowl.Runtime;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// First-time contextual hints: each shows once (remembered in settings.json), one at a time, as a small
/// chip above the hotbar. Toggle in Settings → Game; console "hints reset" replays them.
/// </summary>
public sealed class Hints
{
    public sealed record Hint(string Id, string Text, Func<SandboxGame, bool> When);

    public static readonly Hint[] All =
    [
        new("spawnmenu", $"Hold {{spawnmenu}} to open the spawn menu", g => g.Session is not null || g.SelfTest is not null),
        new("physgun", "Physics gun: hold LMB to grab · wheel pushes/pulls · RMB freezes", g => g.Weapons.Current is PhysGunWeapon && g.Props.Props.Count > 0),
        new("undo", "Press {undo} to undo your last spawn", g => g.Props.UndoCount > 0),
        new("context", "Hold {context} and click a prop to edit it", g => g.Props.Props.Count >= 3),
        new("toolgun", "Tool gun: pick a tool in the spawn menu → Tools, then click props", g => g.Weapons.IsToolGun),
        new("vehicle", "W/S drive · A/D steer · Space brakes · {use} gets out", g => g.InVehicle),
        new("console", "Press ` for the console · type help", g => g.Props.Props.Count >= 8),
        new("photo", "F1 hides the HUD (photo mode) · F12 saves a screenshot", g => g.Props.Props.Count >= 12),
        new("jobs", "Press {jobs} for jobs and the shop", g => g.IsRoleplay),
    ];

    private Hint? _current;
    private float _t, _cooldown = 4f;
    public const float Show = 7f, Gap = 10f;
    public string? Current => _current is null ? null : Format(_current.Text);
    public IReadOnlyList<string> Seen(SandboxGame g) => g.Settings.SeenHints;

    public static string Format(string text) =>
        text.Replace("{spawnmenu}", Binds.Name("spawnmenu")).Replace("{undo}", Binds.Name("undo")).Replace("{context}", Binds.Name("context"))
            .Replace("{use}", Binds.Name("use")).Replace("{jobs}", Binds.Name("jobs"));

    public void Tick(SandboxGame g, float dt)
    {
        if (!g.Settings.ShowHints || g.MenuOpen || g.PhotoMode) return;
        if (_current is not null)
        {
            _t += dt;
            if (_t >= Show) { _current = null; _cooldown = Gap; }
            return;
        }
        _cooldown -= dt;
        if (_cooldown > 0f) return;
        foreach (Hint h in All)
        {
            if (g.Settings.SeenHints.Contains(h.Id)) continue;
            bool when; try { when = h.When(g); } catch { when = false; }
            if (!when) continue;
            _current = h; _t = 0f;
            g.Settings.SeenHints.Add(h.Id);
            if (!Application.IsHeadless) g.Settings.Save();
            return;
        }
    }

    public void Reset(SandboxGame g) { g.Settings.SeenHints.Clear(); _current = null; _cooldown = 1f; }
    public void Dismiss() { _current = null; _cooldown = Gap; }

    public void Draw(Paper paper, float w, float h)
    {
        if (_current is null) return;
        float a = Math.Clamp(MathF.Min(_t * 4f, (Show - _t) * 2f), 0f, 1f);
        if (a <= 0f) return;
        string text = Format(_current.Text);
        float fs = Theme.S(Theme.Body);
        float tw = (float)paper.MeasureText(text, fs, Theme.SemiBold, 0f).X;
        float cw = tw + Theme.S(52), ch = Theme.S(30), x = (w - cw) / 2f, y = h - Theme.S(150) + (1f - a) * Theme.S(8);
        float old = Ui.Alpha; Ui.Alpha = a;
        Ui.Glass(paper, "hint_bg", x, y, cw, ch, ch / 2f, shadow: false).IsNotInteractable();
        Ui.Icon(paper, "hint_ic", x + Theme.S(12), y + (ch - Theme.S(14)) / 2f, Theme.S(14), Icon.Keyboard, Theme.Accent);
        Ui.Text(paper, "hint_t", x + Theme.S(34), y, tw + Theme.S(8), ch, text, Theme.Body, Theme.Text, Theme.SemiBold);
        Ui.Alpha = old;
    }
}
