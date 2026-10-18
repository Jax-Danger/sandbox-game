using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum ToastKind { Info, Success, Undo, Danger }

/// <summary>
/// Minimal s&box-style HUD: tiny crosshair, small low-opacity corner chips, and a weapon
/// switcher + tool card that only appear for a moment after switching. Notifications are small pills.
/// </summary>
public sealed class Hud
{
    private sealed class Toast(string text, ToastKind kind)
    {
        public string Text { get; } = text;
        public ToastKind Kind { get; } = kind;
        public float Age;
        public float Y = float.NaN;
        public int Id = Interlocked.Increment(ref _next);
        private static int _next;
    }

    private const float SwitchShow = 1.8f, SwitchFade = 0.4f;
    private readonly List<Toast> _toasts = [];
    private float _fpsAccum, _fps;
    private int _fpsFrames;

    public IReadOnlyList<string> ActiveToasts => _toasts.Select(t => t.Text).ToList();

    public void Notify(string? message, ToastKind? kind = null)
    {
        if (string.IsNullOrEmpty(message)) return;
        kind ??= message.StartsWith("Undone") ? ToastKind.Undo
            : message.StartsWith("Removed") || message.StartsWith("Nothing") || message.StartsWith("That's") || message.StartsWith("Too heavy") ? ToastKind.Danger
            : message.StartsWith("Welded") || message.StartsWith("Spawned") || message.StartsWith("Balloon") || message.StartsWith("Saved") || message.StartsWith("Loaded") ? ToastKind.Success
            : ToastKind.Info;
        _toasts.Add(new Toast(message, kind.Value));
        if (_toasts.Count > 4) _toasts.RemoveAt(0);
        Console.WriteLine($"[hud] {message}");
    }

    public void Tick(float dt)
    {
        foreach (Toast t in _toasts) t.Age += dt;
        _toasts.RemoveAll(t => t.Age > Theme.ToastLife);
        _fpsAccum += dt; _fpsFrames++;
        if (_fpsAccum >= 0.5f) { _fps = _fpsFrames / _fpsAccum; _fpsAccum = 0; _fpsFrames = 0; }
    }

    /// <param name="chrome">False while a full menu is open: only notifications are drawn.</param>
    public void Draw(Paper paper, float w, float h, SandboxGame game, float dt, bool chrome = true)
    {
        Ui.Alpha = 1f;
        float m = Theme.S(20);
        if (chrome)
        {
            DrawLawsBoard(paper, w, h, game);
            if (!game.InVehicle) DrawCrosshair(paper, w / 2f, h / 2f, game);
            if (!game.InVehicle) DrawInteract(paper, w / 2f, h / 2f, game);
            DrawCorners(paper, w, h, m, game);
            DrawSwitcher(paper, w, m, game);
            game.Vehicles.DrawHud(paper, w, h, m);
        }
        if (game.Settings.ShowFps)
            Ui.Text(paper, "hud_fps", w - m - Theme.S(80), Theme.S(8), Theme.S(80), Theme.S(16), $"{_fps:0} FPS",
                Theme.Micro, Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleRight);
        DrawToasts(paper, w / 2f, h - m - Theme.S(chrome ? 44 : 0), dt);
    }

    /// <summary>Small s&box-style prompt under the crosshair for doors and the ATM.</summary>
    private static void DrawInteract(Paper paper, float cx, float cy, SandboxGame game)
    {
        var (door, atm, _) = game.WorldAim();
        if (door is null && !atm) return;
        RoleplaySystem rp = game.Roleplay;
        string title = door?.Title ?? "ATM", sub = door?.Status ?? "Deposit and withdraw";
        var keys = new List<(string Key, string What)>();
        if (door is not null)
        {
            keys.Add(("E", door.Locked && !rp.CanUnlock(door) ? "Knock" : door.Open ? "Close" : "Open"));
            if (rp.Active && door.Owner == "" && door.Price > 0) keys.Add(("F2", $"Buy ${door.Price:N0}"));
            if (rp.Active && door.Owner == "you") { keys.Add(("L", door.Locked ? "Unlock" : "Lock")); keys.Add(("F2", "Sell")); }
            else if (rp.CanUnlock(door) && door.Owner == "police") keys.Add(("L", door.Locked ? "Unlock" : "Lock"));
        }
        else keys.Add(("E", "Use ATM"));
        float y = cy + Theme.S(26);
        float tw = (float)paper.MeasureText(title, Theme.S(Theme.Body), Theme.SemiBold, 0f).X;
        Ui.Text(paper, "ia_t", cx - tw / 2f - Theme.S(20), y, tw + Theme.S(40), Theme.S(20), title, Theme.Body, Theme.Text, Theme.SemiBold, TextAlignment.MiddleCenter);
        y += Theme.S(19);
        if (sub.Length > 0)
        {
            Ui.Text(paper, "ia_s", cx - Theme.S(150), y, Theme.S(300), Theme.S(16), sub, Theme.Caption, door?.Locked == true ? Theme.Warning : Theme.TextMuted, Theme.Regular, TextAlignment.MiddleCenter);
            y += Theme.S(22);
        }
        float kh = Theme.S(22), gap = Theme.S(10);
        float[] widths = keys.Select(k => Theme.S(30) + (float)paper.MeasureText(k.What, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X).ToArray();
        float total = widths.Sum() + gap * (keys.Count - 1), x = cx - total / 2f;
        for (int i = 0; i < keys.Count; i++)
        {
            float kw = Theme.S(10) + (float)paper.MeasureText(keys[i].Key, Theme.S(Theme.Micro), Theme.Bold, 0f).X;
            Ui.Glass(paper, $"ia_k{i}", x, y, kw + Theme.S(4), kh, Theme.S(5), shadow: false).IsNotInteractable();
            Ui.Text(paper, $"ia_kt{i}", x, y, kw + Theme.S(4), kh, keys[i].Key, Theme.Micro, Theme.Text, Theme.Bold, TextAlignment.MiddleCenter);
            Ui.Text(paper, $"ia_kw{i}", x + kw + Theme.S(10), y, widths[i], kh, keys[i].What, Theme.Caption, Theme.TextMuted, Theme.SemiBold);
            x += widths[i] + gap;
        }
    }

    /// <summary>Projects a world point onto the screen (null when behind the camera).</summary>
    public static Prowl.Vector.Float2? Project(SandboxGame game, Prowl.Vector.Float3 world, float w, float h)
    {
        var cam = game.Player.CameraObject.Transform;
        Prowl.Vector.Float3 l = cam.InverseTransformDirection(world - cam.Position);
        if (l.Z < 0.2f) return null;
        float f = 1f / MathF.Tan(game.Player.Camera.FieldOfView * MathF.PI / 360f);
        return new Prowl.Vector.Float2(w / 2f + l.X / l.Z * f * h / 2f, h / 2f - l.Y / l.Z * f * h / 2f);
    }

    /// <summary>The town laws board, drawn as a world panel over its 3D frame (like s&box WorldPanels).</summary>
    private static void DrawLawsBoard(Paper paper, float w, float h, SandboxGame game)
    {
        if (Town.Board is not { } board || game.InVehicle) return;
        Prowl.Vector.Float3 c = board.Transform.Position;
        Prowl.Vector.Float3 eye = game.Player.EyePosition;
        float dist = Prowl.Vector.Float3.Length(c - eye);
        if (dist > 30f || eye.Z > c.Z - 0.1f) return;
        if (Project(game, c + new Prowl.Vector.Float3(-1.9f, 1.0f, -0.05f), w, h) is not { } tl || Project(game, c + new Prowl.Vector.Float3(1.9f, -1.0f, -0.05f), w, h) is not { } br) return;
        float bw = br.X - tl.X, bh = br.Y - tl.Y;
        if (bw < 40f || bh < 20f) return;
        float k = MathF.Max(bh / Theme.S(220), 0.7f);   // text scales with the board on screen
        float a = Math.Clamp((30f - dist) / 6f, 0f, 1f);
        float old = Ui.Alpha; Ui.Alpha = a;
        RoleplaySystem rp = game.Roleplay;
        var laws = rp.Laws.Count > 0 ? rp.Laws : ["No laws yet.", "The mayor writes laws in the F4 menu."];
        Ui.Text(paper, "lb_kick", tl.X + 14f * k, tl.Y + 10f * k, bw, 18f * k, "CITY OF PROWL", MathF.Max(7f, 11f * k), Theme.WithAlpha(Theme.Warning, 0.95f), Theme.Bold);
        Ui.Text(paper, "lb_title", tl.X + 14f * k, tl.Y + 26f * k, bw, 34f * k, "Laws", MathF.Max(9f, 28f * k), Theme.Text, Theme.Bold);
        float ly = tl.Y + 70f * k;
        for (int i = 0; i < laws.Count && i < 7; i++)
        {
            Ui.Text(paper, $"lb_n{i}", tl.X + 14f * k, ly, 24f * k, 20f * k, $"{i + 1}", MathF.Max(7f, 14f * k), Theme.Accent, Theme.Bold);
            Ui.Text(paper, $"lb_l{i}", tl.X + 38f * k, ly, bw - 50f * k, 20f * k, laws[i], MathF.Max(7f, 14f * k), Theme.Text, Theme.Regular);
            ly += 22f * k;
        }
        Ui.Text(paper, "lb_foot", tl.X + 14f * k, br.Y - 24f * k, bw - 28f * k, 16f * k, rp.Active ? $"Mayor: {(rp.Job.Id == "mayor" ? "you" : "vacant")}" : "Roleplay laws", MathF.Max(6f, 10f * k), Theme.TextFaint, Theme.SemiBold);
        Ui.Alpha = old;
    }

    private static void DrawCrosshair(Paper paper, float cx, float cy, SandboxGame game)
    {
        if (game.Weapons.Current?.Vm is { AimBlend: > 0.55f, Def.AimShift: null }) return; // the sights take over while aiming
        float t = Ui.SpringTo("xh", game.AimingAtProp ? 1f : 0f);
        Color c = Theme.WithAlpha(Color.White, 0.9f);
        float d = Theme.S(3 + 1.5f * t);
        Ui.Rect(paper, "xh_d", cx - d / 2f, cy - d / 2f, d, d, Theme.WithAlpha(Color.Black, 0.35f), d / 2f);
        float d2 = d - 1.5f;
        Ui.Rect(paper, "xh_c", cx - d2 / 2f, cy - d2 / 2f, d2, d2, c, d2 / 2f);
        if (t > 0.02f)
        {
            // Thin ring when aiming at something you can interact with.
            float r = Theme.S(9);
            paper.Box("xh_ring").PositionType(PositionType.SelfDirected).Position(cx - r, cy - r).Size(2 * r, 2 * r).IsNotInteractable()
                .Rounded(r).BorderColor(Theme.WithAlpha(Theme.Accent, t * 0.8f)).BorderWidth(1.5f).Scale(0.6f + 0.4f * t).TransformOrigin(0.5f, 0.5f);
        }
    }

    private static void DrawCorners(Paper paper, float w, float h, float m, SandboxGame game)
    {
        // Bottom-left: prop count (+ noclip chip only when active).
        float ch = Theme.S(26);
        // Health/armor sits in the bottom-left corner; the prop chip moves up above it.
        float hpW = game.Health.DrawHud(paper, m, h - m - Theme.S(34), Theme.S(34));
        h -= Theme.S(34) + Theme.S(8);
        if (game.IsRoleplay) DrawRoleplay(paper, m, game);
        string left = game.IsRoleplay ? $"{game.Props.Props.Count(p => p.Kind != PropKind.Limb)}/{game.PropLimit} props" : $"{game.Props.Props.Count(p => p.Kind != PropKind.Limb)} props";
        float lw = (float)paper.MeasureText(left, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X + Theme.S(36);
        Ui.Glass(paper, "hud_props", m, h - m - ch, lw, ch, ch / 2f, shadow: false).IsNotInteractable();
        Ui.Icon(paper, "hud_props_ic", m + Theme.S(9), h - m - ch + Theme.S(6), Theme.S(14), Icon.Cube, Theme.TextMuted);
        Ui.Text(paper, "hud_props_t", m + Theme.S(28), h - m - ch, lw, ch, left, Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        if (game.Player.Noclip)
        {
            float nw = Theme.S(64);
            Ui.Glass(paper, "hud_noclip", m + lw + Theme.S(6), h - m - ch, nw, ch, ch / 2f, shadow: false).IsNotInteractable();
            Ui.Text(paper, "hud_noclip_t", m + lw + Theme.S(6), h - m - ch, nw, ch, "NOCLIP", Theme.Micro, Theme.Warning, Theme.Bold, TextAlignment.MiddleCenter);
        }

        // Bottom-right: weapon icon + name (+ ammo), one small chip. Replaced by the speedometer while driving.
        h += Theme.S(34) + Theme.S(8);
        if (game.InVehicle) return;
        Weapon wpn = game.Weapons.Current;
        string label = game.Weapons.IsToolGun ? ToolGun.InfoFor(game.ToolGun.Mode).Name : wpn.Name;
        string? ammo = wpn.AmmoText;
        float tw = (float)paper.MeasureText(label, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X;
        float aw = ammo is null ? 0f : (float)paper.MeasureText(ammo, Theme.S(Theme.Title), Theme.Bold, 0f).X + Theme.S(14);
        float rw = tw + aw + Theme.S(40);
        float rh = ammo is null ? ch : Theme.S(34);
        float rx = w - m - rw, ry = h - m - rh;
        Ui.Glass(paper, "hud_wpn", rx, ry, rw, rh, rh / 2f, shadow: false).IsNotInteractable();
        Ui.Icon(paper, "hud_wpn_ic", rx + Theme.S(10), ry + (rh - Theme.S(14)) / 2f, Theme.S(14),
            game.Weapons.IsToolGun ? Icons.For(game.ToolGun.Mode) : wpn.Icon, Theme.TextMuted);
        Ui.Text(paper, "hud_wpn_t", rx + Theme.S(30), ry, tw + 4, rh, label, Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        if (ammo is not null)
            Ui.Text(paper, "hud_ammo", rx + Theme.S(30) + tw + Theme.S(10), ry, aw, rh, ammo, Theme.Title, Theme.Text, Theme.Bold);

        // Tiny weld-in-progress hint (only state that needs persistent feedback).
        if (game.Weapons.IsToolGun && game.ToolGun.PendingWeld is { } first)
            Ui.Text(paper, "hud_weldhint", 0, h / 2f + Theme.S(18), w, Theme.S(18), $"Weld: pick a second prop (from {first.Go.Name})",
                Theme.Caption, Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
    }

    /// <summary>Roleplay chip (top-left): wallet, job colour/name, salary countdown, wanted badge; laws board under it.</summary>
    private static void DrawRoleplay(Paper paper, float m, SandboxGame game)
    {
        RoleplaySystem rp = game.Roleplay;
        float y = m, ch = Theme.S(48);
        string money = $"${rp.Money:N0}";
        float mw = (float)paper.MeasureText(money, Theme.S(Theme.Title), Theme.Bold, 0f).X;
        float jw = (float)paper.MeasureText(rp.Job.Name, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X;
        float w = MathF.Max(mw + Theme.S(44), jw + Theme.S(120)) + Theme.S(16);
        float pulse = Ui.SpringTo("rp_money_pulse", 0f, 0f);
        Ui.Glass(paper, "rp_chip", m, y, w, ch, Theme.S(12), shadow: false).IsNotInteractable();
        Ui.Rect(paper, "rp_jobbar", m + Theme.S(8), y + Theme.S(8), Theme.S(4), ch - Theme.S(16), rp.Job.Color, Theme.S(2));
        Ui.Icon(paper, "rp_money_ic", m + Theme.S(20), y + Theme.S(6), Theme.S(16), Icon.Money, Theme.Success);
        Ui.Text(paper, "rp_money", m + Theme.S(42), y + Theme.S(2), w, Theme.S(24), money, Theme.Title + pulse, Theme.Text, Theme.Bold);
        Ui.Text(paper, "rp_job", m + Theme.S(20), y + Theme.S(26), w, Theme.S(18), rp.Job.Name, Theme.Caption, rp.Job.Color, Theme.SemiBold);
        Ui.Text(paper, "rp_pay", m, y + Theme.S(26), w - Theme.S(10), Theme.S(18), $"+${rp.Job.Salary} in {rp.PayTimer:0}s", Theme.Micro, Theme.TextFaint, Theme.SemiBold, TextAlignment.MiddleRight);
        {
            float bx = m + Theme.S(20), bw = w - Theme.S(30), by = y + ch - Theme.S(6), f = rp.Hunger / 100f;
            Color hc = f <= 0f ? Theme.Danger : f < 0.25f ? Theme.Warning : Theme.WithAlpha(Theme.Success, 0.75f);
            Ui.Rect(paper, "rp_hunger_bg", bx, by, bw, Theme.S(2), Theme.Border, Theme.S(1));
            Ui.Rect(paper, "rp_hunger", bx, by, MathF.Max(Theme.S(2), bw * f), Theme.S(2), hc, Theme.S(1));
        }
        y += ch + Theme.S(6);
        if (rp.Starving)
        {
            Ui.Glass(paper, "rp_starving", m, y, w, Theme.S(24), Theme.S(12), shadow: false).IsNotInteractable();
            Ui.Text(paper, "rp_starving_t", m, y, w, Theme.S(24), "STARVING · F4 for food", Theme.Micro, Theme.Danger, Theme.Bold, TextAlignment.MiddleCenter);
            y += Theme.S(30);
        }
        if (rp.WantedTimer > 0f)
        {
            Ui.Glass(paper, "rp_wanted", m, y, w, Theme.S(24), Theme.S(12), shadow: false).IsNotInteractable();
            Ui.Text(paper, "rp_wanted_t", m, y, w, Theme.S(24), $"WANTED · {rp.WantedTimer:0}s", Theme.Micro, Theme.Danger, Theme.Bold, TextAlignment.MiddleCenter);
            y += Theme.S(30);
        }
        if (rp.JailTimer > 0f)
        {
            Ui.Glass(paper, "rp_jail", m, y, w, Theme.S(24), Theme.S(12), shadow: false).IsNotInteractable();
            Ui.Text(paper, "rp_jail_t", m, y, w, Theme.S(24), $"ARRESTED · {rp.JailTimer:0}s", Theme.Micro, Theme.Warning, Theme.Bold, TextAlignment.MiddleCenter);
            y += Theme.S(30);
        }
        if (rp.Laws.Count > 0 && !Town.Active)
        {
            float lh = Theme.S(18), bw = Theme.S(260), bh = Theme.S(28) + rp.Laws.Count * lh;
            Ui.Glass(paper, "rp_laws", m, y, bw, bh, Theme.S(10), shadow: false).IsNotInteractable();
            Ui.Icon(paper, "rp_laws_ic", m + Theme.S(10), y + Theme.S(7), Theme.S(12), Icon.Gavel, Theme.TextMuted);
            Ui.Text(paper, "rp_laws_t", m + Theme.S(28), y + Theme.S(2), bw, Theme.S(22), "LAWS", Theme.Micro, Theme.TextMuted, Theme.Bold);
            for (int i = 0; i < rp.Laws.Count; i++)
                Ui.Text(paper, $"rp_law_{i}", m + Theme.S(12), y + Theme.S(24) + i * lh, bw - Theme.S(20), lh, $"{i + 1}. {rp.Laws[i]}", Theme.Caption, Theme.TextMuted);
        }
    }

    /// <summary>Weapon slots and tool card, shown briefly after switching, then faded out.</summary>
    private static void DrawSwitcher(Paper paper, float w, float m, SandboxGame game)
    {
        float t = game.Weapons.TimeSinceSwitch;
        if (t > SwitchShow + SwitchFade) return;
        float a = MathF.Min(Theme.Ease(t / 0.12f), Theme.Ease((SwitchShow + SwitchFade - t) / SwitchFade));
        Ui.Alpha = a;
        IReadOnlyList<Weapon> all = game.Weapons.All.Where(x => x.Available(game) || ReferenceEquals(x, game.Weapons.Current)).ToList();
        int[] slots = all.Select(x => x.Slot).Distinct().OrderBy(x => x).ToArray();
        float sw = Theme.S(46), gap = Theme.S(6), total = slots.Length * sw + (slots.Length - 1) * gap;
        float x0 = (w - total) / 2f, y0 = m;
        foreach (int slot in slots)
        {
            Weapon[] inSlot = all.Where(x => x.Slot == slot).ToArray();
            Weapon shown = inSlot.Contains(game.Weapons.Current) ? game.Weapons.Current : inSlot[0];
            bool active = ReferenceEquals(shown, game.Weapons.Current);
            float k = Ui.SpringTo($"sw_{slot}", active ? 1f : 0f);
            float hh = sw + k * Theme.S(4);
            Ui.Glass(paper, $"sw_{slot}", x0, y0, sw, hh, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable()
                .BorderColor(Theme.WithAlpha(active ? Theme.Accent : Theme.Border, a));
            Ui.Icon(paper, $"sw_{slot}_ic", x0 + (sw - Theme.S(20)) / 2f, y0 + Theme.S(9), Theme.S(20), shown.Icon, active ? Color.White : Theme.TextFaint);
            Ui.Text(paper, $"sw_{slot}_n", x0, y0 + sw - Theme.S(16), sw, Theme.S(14), slot.ToString(), Theme.Micro, active ? Theme.Accent : Theme.TextFaint,
                Theme.Bold, TextAlignment.MiddleCenter);
            x0 += sw + gap;
        }
        string name = game.Weapons.IsToolGun ? $"Tool Gun · {ToolGun.InfoFor(game.ToolGun.Mode).Name}" : game.Weapons.Current.Name;
        Ui.Text(paper, "sw_name", 0, y0 + sw + Theme.S(8), w, Theme.S(18), name, Theme.Caption, Theme.Text, Theme.SemiBold, TextAlignment.MiddleCenter);
        if (game.Weapons.IsToolGun)
        {
            ToolInfo info = ToolGun.InfoFor(game.ToolGun.Mode);
            string hint = info.Right is null ? $"LMB  {info.Left}" : $"LMB  {info.Left}   ·   RMB  {info.Right}";
            Ui.Text(paper, "sw_hint", 0, y0 + sw + Theme.S(26), w, Theme.S(16), hint, Theme.Micro, Theme.TextMuted, align: TextAlignment.MiddleCenter);
        }
        Ui.Alpha = 1f;
    }

    private void DrawToasts(Paper paper, float cx, float bottom, float dt)
    {
        float th = Theme.S(26), gap = Theme.S(6);
        float targetY = bottom;
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            Toast t = _toasts[i];
            targetY -= th;
            if (float.IsNaN(t.Y)) t.Y = targetY + Theme.S(10);
            t.Y += (targetY - t.Y) * Math.Clamp(dt * 16f, 0f, 1f);
            float a = MathF.Min(Theme.Ease(t.Age / 0.18f), Theme.Ease((Theme.ToastLife - t.Age) / Theme.ToastFade));
            Ui.Alpha = a;
            Color accent = t.Kind switch
            {
                ToastKind.Success => Theme.Success, ToastKind.Undo => Theme.Warning, ToastKind.Danger => Theme.Danger, _ => Theme.Accent,
            };
            float tw = (float)paper.MeasureText(t.Text, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X + Theme.S(34);
            float x = cx - tw / 2f;
            string id = $"toast_{t.Id}";
            Ui.Glass(paper, id, x, t.Y, tw, th, th / 2f, shadow: false).IsNotInteractable();
            Ui.Rect(paper, id + "_dot", x + Theme.S(11), t.Y + th / 2f - Theme.S(3), Theme.S(6), Theme.S(6), accent, Theme.S(3));
            Ui.Text(paper, id + "_t", x + Theme.S(23), t.Y, tw, th, t.Text, Theme.Caption, Theme.Text, Theme.SemiBold);
            targetY -= gap;
        }
        Ui.Alpha = 1f;
    }
}
