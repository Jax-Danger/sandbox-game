using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum PauseAction { None, Resume, ClearMap, Quit }

/// <summary>Escape menu styled like a game main menu: blurred, dimmed backdrop, big title, left-aligned buttons.</summary>
public sealed class PauseMenu
{
    private float _openness;
    public bool Open { get; set; }
    public bool Visible => _openness > 0.01f;
    public PauseAction Requested { get; set; }

    public void Draw(Paper paper, float w, float h, float dt, SandboxGame game)
    {
        _openness = Math.Clamp(_openness + (Open ? 1f : -1f) * dt * Theme.OpenSpeed, 0f, 1f);
        if (_openness <= 0.01f) return;
        float t = Theme.Ease(_openness);
        Ui.Alpha = t;

        // Full-screen blur + dim, with a darker gradient on the left behind the menu column.
        paper.Box("pm_backdrop").PositionType(PositionType.SelfDirected).Position(0, 0).Size(w, h)
            .BackdropBlur(Theme.S(10) * t)
            .BackgroundLinearGradient(0, 0, 1, 0, Theme.WithAlpha(Color.FromArgb(235, 8, 10, 14), t), Theme.WithAlpha(Color.FromArgb(120, 8, 10, 14), t))
            .OnClick(0, (_, _) => { }).StopEventPropagation();

        float x = Theme.S(96) - (1f - t) * Theme.S(30);
        float y = h * 0.28f;
        Ui.Text(paper, "pm_kicker", x, y - Theme.S(30), w, Theme.S(20), "PAUSED", Theme.Caption + 1, Theme.Accent, Theme.SemiBold);
        Ui.Text(paper, "pm_title", x, y, w, Theme.S(70), "Sandbox", Theme.Display, Theme.Text, Theme.Bold);
        Ui.Text(paper, "pm_sub", x + Theme.S(3), y + Theme.S(70), w, Theme.S(24),
            $"{game.Props.Props.Count} props on the map · physics paused", Theme.Label, Theme.TextMuted);

        float by = y + Theme.S(130), bw = Theme.S(300), bh = Theme.S(50), gap = Theme.S(Theme.Md);
        MenuButton(paper, "pm_resume", x, by, bw, bh, "Resume", "Esc", () => Requested = PauseAction.Resume, Theme.Accent);
        MenuButton(paper, "pm_clear", x, by + (bh + gap), bw, bh, "Clear map", null, () => Requested = PauseAction.ClearMap, Theme.Warning);
        MenuButton(paper, "pm_quit", x, by + 2 * (bh + gap), bw, bh, "Quit to desktop", null, () => Requested = PauseAction.Quit, Theme.Danger);

        Ui.Text(paper, "pm_footer", x, h - Theme.S(56), w, Theme.S(20),
            "Built on the Prowl engine · Jitter Physics 2", Theme.Caption, Theme.TextFaint);
        Ui.Alpha = 1f;
    }

    private static void MenuButton(Paper paper, string id, float x, float y, float w, float h, string text, string? key, Action onClick, Color accent)
    {
        float a = Ui.Alpha;
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .Rounded(Theme.S(Theme.RadiusSm))
            .BackgroundColor(Theme.WithAlpha(Color.FromArgb(150, 34, 38, 46), a))
            .BorderColor(Theme.WithAlpha(Theme.Border, a)).BorderWidth(1f)
            .Hovered.BackgroundColor(Theme.WithAlpha(Color.FromArgb(235, accent.R / 3 + 20, accent.G / 3 + 20, accent.B / 3 + 24), a))
                .BorderColor(Theme.WithAlpha(accent, a)).End()
            .Transition(GuiProp.BackgroundColor, 0.12f).Transition(GuiProp.BorderColor, 0.12f)
            .OnClick(0, (_, _) => onClick());
        bool hover = paper.IsPointerOverRect(x, y, w, h);
        Ui.Rect(paper, id + "_bar", x + Theme.S(8), y + Theme.S(14), Theme.S(3), h - Theme.S(28), hover ? accent : Theme.Border, Theme.S(2));
        Ui.Text(paper, id + "_lbl", x + Theme.S(Theme.Lg + 8) + (hover ? Theme.S(4) : 0f), y, w, h, text, Theme.Title, Theme.Text, Theme.SemiBold);
        if (key is not null)
        {
            Ui.KeyCap(paper, id + "_k", x + w - Theme.S(52), y + (h - Theme.S(20)) / 2f, key);
        }
    }
}
