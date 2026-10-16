using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum ToastKind { Info, Success, Undo, Danger }

/// <summary>
/// GMod-style HUD: crosshair, bottom-left status, bottom-right weapon panel with slot strip,
/// top-left toolgun info box, and toast notifications that slide/fade in and out.
/// </summary>
public sealed class Hud
{
    private sealed class Toast(string text, ToastKind kind)
    {
        public string Text { get; } = text;
        public ToastKind Kind { get; } = kind;
        public float Age;
        public float Y = float.NaN; // animated stack position
    }

    private readonly List<Toast> _toasts = [];
    private float _time;
    private int _toastId;

    public IReadOnlyList<string> ActiveToasts => _toasts.Select(t => t.Text).ToList();

    public void Notify(string? message, ToastKind? kind = null)
    {
        if (string.IsNullOrEmpty(message)) return;
        kind ??= message.StartsWith("Undone") ? ToastKind.Undo
            : message.StartsWith("Removed") || message.StartsWith("Nothing") || message.StartsWith("That's") ? ToastKind.Danger
            : message.StartsWith("Welded") || message.StartsWith("Spawned") || message.StartsWith("Balloon") ? ToastKind.Success
            : ToastKind.Info;
        _toasts.Add(new Toast(message, kind.Value));
        if (_toasts.Count > 5) _toasts.RemoveAt(0);
        _toastId++;
        Console.WriteLine($"[hud] {message}");
    }

    public void Tick(float dt)
    {
        _time += dt;
        foreach (Toast t in _toasts) t.Age += dt;
        _toasts.RemoveAll(t => t.Age > Theme.ToastLife);
    }

    /// <param name="chrome">False while a full menu is open: only toasts are drawn.</param>
    public void Draw(Paper paper, float w, float h, SandboxGame game, float dt, bool chrome = true)
    {
        if (!chrome)
        {
            DrawToasts(paper, w - Theme.S(Theme.Xl), h - Theme.S(Theme.Xl), dt);
            return;
        }
        Ui.Alpha = 1f;
        float m = Theme.S(Theme.Xl);
        DrawCrosshair(paper, w / 2f, h / 2f, game);
        DrawStatus(paper, m, h - m, game);
        float weaponTop = DrawWeapon(paper, w - m, h - m, game);
        if (game.Tool == ActiveTool.ToolGun) DrawToolInfo(paper, m, m, game);
        DrawToasts(paper, w - m, weaponTop - Theme.S(Theme.Md), dt);
        DrawOnboarding(paper, w, h - m - Theme.S(40));
    }

    private static void DrawCrosshair(Paper paper, float cx, float cy, SandboxGame game)
    {
        bool target = game.AimingAtProp;
        Color c = target ? Theme.Accent : Color.FromArgb(235, 255, 255, 255);
        Color outline = Color.FromArgb(140, 0, 0, 0);
        float gap = Theme.S(target ? 7 : 5), len = Theme.S(6), th = MathF.Max(2f, Theme.S(2));
        void Bar(string id, float x, float y, float bw, float bh)
        {
            Ui.Rect(paper, id + "_o", x - 1, y - 1, bw + 2, bh + 2, outline, 1f);
            Ui.Rect(paper, id, x, y, bw, bh, c, 1f);
        }
        Bar("xh_l", cx - gap - len, cy - th / 2f, len, th);
        Bar("xh_r", cx + gap, cy - th / 2f, len, th);
        Bar("xh_t", cx - th / 2f, cy - gap - len, th, len);
        Bar("xh_b", cx - th / 2f, cy + gap, th, len);
        Bar("xh_d", cx - th / 2f, cy - th / 2f, th, th);
    }

    private static void DrawStatus(Paper paper, float x, float bottom, SandboxGame game)
    {
        float pw = Theme.S(260), ph = Theme.S(84), y = bottom - ph, pad = Theme.S(Theme.Lg);
        Ui.Panel(paper, "hud_status", x, y, pw, ph, Theme.Hud).IsNotInteractable();
        Ui.Text(paper, "hud_props_c", x + pad, y + Theme.S(12), pw, Theme.S(16), "PROPS", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        Ui.Text(paper, "hud_props_v", x + pad, y + Theme.S(28), Theme.S(110), Theme.S(44), game.Props.Props.Count.ToString(),
            40f, Theme.Text, Theme.Bold);
        float mx = x + Theme.S(130);
        Ui.Rect(paper, "hud_status_sep", mx - Theme.S(Theme.Md), y + Theme.S(14), 1f, ph - Theme.S(28), Theme.Border);
        Ui.Text(paper, "hud_mode_c", mx, y + Theme.S(12), Theme.S(120), Theme.S(16), "MOVEMENT", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        Ui.Text(paper, "hud_mode_v", mx, y + Theme.S(32), Theme.S(120), Theme.S(28), game.Player.Noclip ? "Noclip" : "Walking",
            Theme.Title, game.Player.Noclip ? Theme.Warning : Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "hud_mode_k", mx, y + Theme.S(58), Theme.S(120), Theme.S(16), "V to toggle", Theme.Caption, Theme.TextFaint);
    }

    /// <summary>Returns the top y of the weapon panel so toasts can stack above it.</summary>
    private static float DrawWeapon(Paper paper, float right, float bottom, SandboxGame game)
    {
        float pw = Theme.S(320), ph = Theme.S(84), x = right - pw, y = bottom - ph, pad = Theme.S(Theme.Lg);
        bool tool = game.Tool == ActiveTool.ToolGun;
        string caption = tool ? "TOOL GUN" : "PHYSICS GUN";
        string name = tool ? ToolGun.InfoFor(game.ToolGun.Mode).Name : game.PhysGun.Held is { } held ? "Holding" : "Ready";
        string detail = tool
            ? (game.ToolGun.PendingWeld is { } first ? $"Weld from {first.Go.Name}" : ToolGun.InfoFor(game.ToolGun.Mode).Category)
            : game.PhysGun.Held is { } h2 ? h2.Go.Name : "Grab · freeze · throw";

        Ui.Panel(paper, "hud_weapon", x, y, pw, ph, Theme.Hud).IsNotInteractable();
        Ui.Rect(paper, "hud_weapon_bar", x, y + Theme.S(14), Theme.S(3), ph - Theme.S(28), Theme.Accent, Theme.S(2));
        Ui.Text(paper, "hud_weapon_c", x + pad, y + Theme.S(12), pw, Theme.S(16), caption, Theme.Caption, Theme.Accent, Theme.SemiBold);
        Ui.Text(paper, "hud_weapon_n", x + pad, y + Theme.S(28), pw - 2 * pad, Theme.S(30), name, 24f, Theme.Text, Theme.Bold);
        Ui.Text(paper, "hud_weapon_d", x + pad, y + Theme.S(58), pw - 2 * pad, Theme.S(16), detail, Theme.Caption, Theme.TextMuted);

        // Slot strip above the panel: 1 Physgun · 2 Weld · 3 Remover · 4 Balloon.
        (string Key, string Label, bool Active)[] slots =
        [
            ("1", "Physgun", !tool),
            ("2", "Weld", tool && game.ToolGun.Mode == ToolMode.Weld),
            ("3", "Remover", tool && game.ToolGun.Mode == ToolMode.Remover),
            ("4", "Balloon", tool && game.ToolGun.Mode == ToolMode.Balloon),
        ];
        float sw = (pw - 3 * Theme.S(6)) / 4f, sh = Theme.S(30), sy = y - sh - Theme.S(Theme.Sm);
        for (int i = 0; i < slots.Length; i++)
        {
            float sx = x + i * (sw + Theme.S(6));
            var (key, label, active) = slots[i];
            Ui.Panel(paper, $"hud_slot_{i}", sx, sy, sw, sh, active ? Theme.AccentSoft : Theme.Hud, Theme.S(Theme.RadiusSm), shadow: false)
                .IsNotInteractable().BorderColor(active ? Theme.Accent : Theme.Border);
            Ui.Text(paper, $"hud_slot_k{i}", sx + Theme.S(8), sy, Theme.S(14), sh, key, Theme.Caption, active ? Color.White : Theme.TextFaint, Theme.Bold);
            Ui.Text(paper, $"hud_slot_l{i}", sx + Theme.S(20), sy, sw - Theme.S(22), sh, label, Theme.Caption,
                active ? Color.White : Theme.TextMuted, active ? Theme.SemiBold : Theme.Regular);
        }
        return sy;
    }

    private static void DrawToolInfo(Paper paper, float x, float y, SandboxGame game)
    {
        ToolInfo info = ToolGun.InfoFor(game.ToolGun.Mode);
        float pw = Theme.S(380), pad = Theme.S(Theme.Lg);
        float ph = Theme.S(info.Right is null ? 136 : 162);
        Ui.Panel(paper, "hud_tool", x, y, pw, ph, Theme.Hud).IsNotInteractable();
        Ui.Rect(paper, "hud_tool_acc", x, y, pw, Theme.S(3), Theme.Accent, Theme.S(2));
        float cy = y + pad;
        Ui.Text(paper, "hud_tool_n", x + pad, cy, pw, Theme.S(30), info.Name, 26f, Theme.Text, Theme.Bold);
        cy += Theme.S(34);
        Ui.Text(paper, "hud_tool_d", x + pad, cy, pw - 2 * pad, Theme.S(20), info.Description, Theme.Body, Theme.TextMuted);
        cy += Theme.S(32);
        float kx = x + pad;
        kx += Ui.KeyCap(paper, "hud_tool_lmb", kx, cy, "LMB") + Theme.S(Theme.Sm);
        Ui.Text(paper, "hud_tool_lt", kx, cy, pw, Theme.S(20), info.Left, Theme.Body, Theme.Text);
        if (info.Right is { } right)
        {
            cy += Theme.S(26);
            kx = x + pad;
            kx += Ui.KeyCap(paper, "hud_tool_rmb", kx, cy, "RMB") + Theme.S(Theme.Sm);
            Ui.Text(paper, "hud_tool_rt", kx, cy, pw, Theme.S(20), right, Theme.Body, Theme.Text);
        }
    }

    private void DrawToasts(Paper paper, float right, float bottom, float dt)
    {
        float tw = Theme.S(320), th = Theme.S(40), gap = Theme.S(Theme.Sm);
        float targetY = bottom;
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            Toast t = _toasts[i];
            targetY -= th;
            if (float.IsNaN(t.Y)) t.Y = targetY;
            t.Y += (targetY - t.Y) * Math.Clamp(dt * 14f, 0f, 1f); // smooth restacking

            float fadeIn = Theme.Ease(t.Age / 0.2f);
            float fadeOut = Theme.Ease((Theme.ToastLife - t.Age) / Theme.ToastFade);
            float a = MathF.Min(fadeIn, fadeOut);
            float slide = (1f - fadeIn) * Theme.S(40);
            Ui.Alpha = a;
            float x = right - tw + slide;
            Color accent = t.Kind switch
            {
                ToastKind.Success => Theme.Success,
                ToastKind.Undo => Theme.Warning,
                ToastKind.Danger => Theme.Danger,
                _ => Theme.Accent,
            };
            string id = $"toast_{t.GetHashCode()}";
            Ui.Panel(paper, id, x, t.Y, tw, th, Theme.Hud, Theme.S(Theme.RadiusSm)).IsNotInteractable();
            Ui.Rect(paper, id + "_dot", x + Theme.S(14), t.Y + th / 2f - Theme.S(4), Theme.S(8), Theme.S(8), accent, Theme.S(4));
            Ui.Text(paper, id + "_t", x + Theme.S(32), t.Y, tw - Theme.S(40), th, t.Text, Theme.Body, Theme.Text, Theme.SemiBold);
            targetY -= gap;
        }
        Ui.Alpha = 1f;
    }

    private void DrawOnboarding(Paper paper, float w, float top)
    {
        const float show = 14f;
        if (_time > show) return;
        float a = MathF.Min(Theme.Ease(_time / 0.6f), Theme.Ease((show - _time) / 1f));
        Ui.Alpha = a;
        float pw = Theme.S(520), ph = Theme.S(40), x = (w - pw) / 2f;
        Ui.Panel(paper, "hud_onb", x, top, pw, ph, Theme.Hud, ph / 2f).IsNotInteractable();
        float kx = x + Theme.S(Theme.Lg), ky = top + (ph - Theme.S(20)) / 2f;
        void Pair(string id, string key, string label)
        {
            kx += Ui.KeyCap(paper, id, kx, ky, key) + Theme.S(6);
            Ui.Text(paper, id + "_t", kx, top, Theme.S(140), ph, label, Theme.Body, Theme.TextMuted);
            kx += Theme.S(9) + Theme.S(7.2f) * label.Length;
        }
        Pair("onb_q", "Q", "Spawn menu");
        Pair("onb_1", "1-4", "Weapons");
        Pair("onb_z", "Z", "Undo");
        Pair("onb_esc", "Esc", "Menu");
        Ui.Alpha = 1f;
    }
}
