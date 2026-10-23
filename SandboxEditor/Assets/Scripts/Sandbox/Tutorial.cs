using Prowl.PaperUI;
using Prowl.Runtime;

namespace Sandbox;

/// <summary>
/// "Getting started" quest for new players in Sandbox worlds: five short steps shown as one small chip at the
/// top left. Each step completes from what you actually do (spawn, grab, freeze, weld, drive), with a tick and a
/// sound; progress is saved in settings.json. Console: tutorial reset|skip. Hidden with hints off or in photo mode.
/// </summary>
public sealed class Tutorial
{
    public sealed record Step(string Title, string How, Func<SandboxGame, bool> Done);

    public static readonly Step[] Steps =
    [
        new("Spawn a prop", $"hold {{spawnmenu}} and click one", g => g.Achievements.Has("first_spawn") || g.Props.Props.Count > 0),
        new("Pick it up", "physgun: hold LMB on a prop", g => g.PhysGun.Held is not null),
        new("Freeze something", "press RMB while holding it", g => g.Props.Props.Any(p => p.Frozen)),
        new("Weld two props", "tool gun → Weld, click both", g => g.Achievements.Has("welder")),
        new("Go for a drive", "spawn menu → Vehicles, {use} to get in", g => g.InVehicle || g.Achievements.Has("road_trip")),
    ];

    private float _flash, _show;
    public int Index(SandboxGame g) => Math.Clamp(g.Settings.TutorialStep, 0, Steps.Length);
    public bool Finished(SandboxGame g) => Index(g) >= Steps.Length;
    public bool Visible(SandboxGame g) => !Finished(g) && g.Settings.ShowHints && g.Session is not null && !g.IsRoleplay && !g.PhotoMode;

    public void Tick(SandboxGame g, float dt)
    {
        _flash = MathF.Max(0f, _flash - dt);
        _show = Math.Clamp(_show + (Visible(g) && !g.MenuOpen ? dt : -dt) * 4f, 0f, 1f);
        if (Finished(g) || g.IsRoleplay || g.Session is null && g.SelfTest is null) return;
        bool done; try { done = Steps[Index(g)].Done(g); } catch { done = false; }
        if (!done) return;
        g.Settings.TutorialStep = Index(g) + 1;
        _flash = 0.8f;
        g.Sounds?.Play("sfx_ui_click", 0.6f);
        g.Notify(Finished(g) ? "Tutorial complete. Have fun!" : $"✓ {Steps[Index(g) - 1].Title}", ToastKind.Success);
        if (!Application.IsHeadless) g.Settings.Save();
    }

    public void Reset(SandboxGame g) { g.Settings.TutorialStep = 0; }
    public void Skip(SandboxGame g) { g.Settings.TutorialStep = Steps.Length; }

    public void Draw(SandboxGame g, Paper paper, float w, float h)
    {
        if (_show <= 0.01f || Finished(g)) return;
        int i = Index(g);
        Step s = Steps[i];
        string title = $"{s.Title}", how = Hints.Format(s.How);
        float fs = Theme.S(Theme.Caption);
        float tw = MathF.Max((float)paper.MeasureText(title, Theme.S(Theme.Body), Theme.SemiBold, 0f).X, (float)paper.MeasureText(how, fs, Theme.Regular, 0f).X);
        float cw = tw + Theme.S(64), ch = Theme.S(46), x = Theme.S(16), y = Theme.S(16) - (1f - _show) * Theme.S(10);
        float old = Ui.Alpha; Ui.Alpha = _show;
        Ui.Glass(paper, "tut_bg", x, y, cw, ch, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        // Progress ring stand-in: "2/5" badge.
        Ui.Rect(paper, "tut_badge", x + Theme.S(10), y + Theme.S(11), Theme.S(28), Theme.S(24), Theme.WithAlpha(_flash > 0f ? Theme.Success : Theme.Accent, 0.9f), Theme.S(12));
        Ui.Text(paper, "tut_n", x + Theme.S(10), y + Theme.S(11), Theme.S(28), Theme.S(24), $"{i + 1}/{Steps.Length}", Theme.Caption, Theme.Text, Theme.SemiBold, TextAlignment.MiddleCenter);
        Ui.Text(paper, "tut_t", x + Theme.S(46), y + Theme.S(4), tw + Theme.S(8), Theme.S(22), title, Theme.Body, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "tut_h", x + Theme.S(46), y + Theme.S(22), tw + Theme.S(8), Theme.S(20), how, Theme.Caption, Theme.TextMuted);
        Ui.Alpha = old;
    }
}
