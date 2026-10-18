using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum Screen { MainMenu, Playing, Paused, Settings, Worlds, CreateWorld, Loading, Jobs }

/// <summary>Main menu, pause menu and settings screen: full-screen frosted overlays with springy buttons.</summary>
public sealed partial class Menus
{
    private readonly Spring _fade = new(0f);
    private Screen _settingsReturn = Screen.MainMenu;
    private int _settingsTab;
    public int SettingsTab { get => _settingsTab; set => _settingsTab = value; }

    public Screen Screen { get; private set; } = Screen.MainMenu;
    public bool Blocking => Screen != Screen.Playing;

    public void Go(Screen s)
    {
        if (s == Screen.Settings && Screen != Screen.Settings) _settingsReturn = Screen;
        Screen = s;
    }

    public void Back(SandboxGame g)
    {
        switch (Screen)
        {
            case Screen.Settings: g.Settings.Save(); Screen = _settingsReturn; break;
            case Screen.Paused: Screen = Screen.Playing; break;
            case Screen.Playing: Screen = Screen.Paused; break;
            case Screen.Jobs: Screen = Screen.Playing; break;
            case Screen.Worlds when _modal is not null: _modal = null; break;
            case Screen.Worlds: Screen = Screen.MainMenu; break;
            case Screen.CreateWorld: Screen = Screen.Worlds; break;
        }
    }

    public void Draw(Paper paper, float w, float h, float dt, SandboxGame g)
    {
        float t = Math.Clamp(_fade.Step(Blocking ? 1f : 0f, dt, 200f, 26f), 0f, 1.05f);
        if (t <= 0.01f && !Blocking) return;
        Ui.Alpha = Math.Clamp(t, 0f, 1f);

        if (Screen == Screen.Loading) { DrawLoading(paper, w, h, g); Ui.Alpha = 1f; return; }
        bool main = Screen is Screen.MainMenu or Screen.Worlds or Screen.CreateWorld || (Screen == Screen.Settings && _settingsReturn == Screen.MainMenu);
        // The main menu keeps the live, slowly orbiting scene visible on the right; sub-screens blur it more.
        bool live = Screen == Screen.MainMenu;
        paper.Box("mn_backdrop").PositionType(PositionType.SelfDirected).Position(0, 0).Size(w, h)
            .BackdropBlur(Theme.S(live ? 0 : main ? 14 : 10) * Ui.Alpha)
            .BackgroundLinearGradient(0, 0, 1, 0.3f, Theme.WithAlpha(Color.FromArgb(live ? 215 : main ? 225 : 210, 8, 10, 15), Ui.Alpha),
                Theme.WithAlpha(Color.FromArgb(live ? 10 : main ? 90 : 120, 8, 10, 15), Ui.Alpha))
            .OnClick(0, (_, _) => { }).StopEventPropagation();

        switch (Screen)
        {
            case Screen.MainMenu: DrawMain(paper, w, h, g, false); break;
            case Screen.Paused: DrawMain(paper, w, h, g, true); break;
            case Screen.Settings: DrawSettings(paper, w, h, g); break;
            case Screen.Worlds: DrawWorlds(paper, w, h, g); break;
            case Screen.CreateWorld: DrawCreate(paper, w, h, g); break;
            case Screen.Jobs: DrawJobs(paper, w, h, g); break;
        }
        Ui.Alpha = 1f;
    }

    private void DrawMain(Paper paper, float w, float h, SandboxGame g, bool paused)
    {
        float slide = (1f - Ui.Alpha) * Theme.S(30);
        float x = Theme.S(96) - slide, y = h * 0.26f;
        Ui.Text(paper, "mn_kicker", x, y - Theme.S(26), w, Theme.S(18), paused ? "PAUSED" : "PROWL ENGINE · PHYSICS SANDBOX",
            Theme.Micro, Theme.Accent, Theme.Bold);
        float logo = Theme.S(64);
        DrawLogo(paper, x, y + Theme.S(6), logo, "mn_logo", (float)Prowl.Runtime.Time.UnscaledTotalTime);
        Ui.Text(paper, "mn_title", x + logo + Theme.S(16), y, w, Theme.S(76), "Sandbox", Theme.Display, Theme.Text, Theme.Bold);
        Ui.Text(paper, "mn_sub", x, y + Theme.S(76), w, Theme.S(22),
            paused ? $"{g.Session?.Meta.Name ?? "Session"} · {g.Props.Props.Count} props · physics paused" : "Build, break and mess around.",
            Theme.Label, Theme.TextMuted);

        float by = y + Theme.S(132), bw = Theme.S(280), bh = Theme.S(46), gap = Theme.S(10);
        (string Label, Icon Icon, Action Click)[] items = paused
            ?
            [
                ("Resume", Icon.Play, () => g.Enqueue(() => Go(Screen.Playing))),
                ("Save now", Icon.Save, () => g.Enqueue(() => { g.SaveSession(true, true); g.Notify("Session saved"); })),
                ("Clear map", Icon.Trash, () => g.Enqueue(() => { g.ClearMap(); Go(Screen.Playing); })),
                ("Settings", Icon.Gear, () => Go(Screen.Settings)),
                ("Save & exit to Sessions", Icon.Back, () => g.Enqueue(() => { g.ExitSession(Screen.Worlds); RefreshWorlds(); })),
            ]
            :
            [
                ("Singleplayer", Icon.Play, () => { RefreshWorlds(); Go(Screen.Worlds); }),
                ("Settings", Icon.Gear, () => Go(Screen.Settings)),
                ("Quit", Icon.Power, () => g.Enqueue(g.QuitGame)),
            ];
        for (int i = 0; i < items.Length; i++)
        {
            // Staggered entrance.
            float st = Theme.Ease((Ui.Alpha - i * 0.06f) / 0.7f);
            var (label, icon, click) = items[i];
            float a = Ui.Alpha;
            Ui.Alpha = a * st;
            Ui.Button(paper, $"mn_btn_{label}", x - (1 - st) * Theme.S(20), by + i * (bh + gap), bw, bh, label, click,
                size: Theme.Label, align: TextAlignment.MiddleLeft, icon: icon, selected: i == 0, radius: Theme.S(Theme.RadiusSm + 2));
            Ui.Alpha = a;
        }

        Ui.Text(paper, "mn_footer", x, h - Theme.S(48), w, Theme.S(18),
            paused ? "Esc to resume" : "Prowl 1.0-preview · Jitter Physics 2 · placeholder art", Theme.Caption, Theme.TextFaint);
    }

    private void DrawSettings(Paper paper, float w, float h, SandboxGame g)
    {
        Settings s = g.Settings;
        float pw = MathF.Min(w * 0.86f, Theme.S(760)), ph = MathF.Min(h * 0.84f, Theme.S(_settingsTab switch { 1 => 600, 3 => 470, _ => 400 }));
        float x = (w - pw) / 2f, y = (h - ph) / 2f + (1f - Ui.Alpha) * Theme.S(24), pad = Theme.S(Theme.Xl);
        Ui.Glass(paper, "st_panel", x, y, pw, ph, strong: true).IsNotInteractable();
        Ui.Text(paper, "st_title", x + pad, y + pad, pw, Theme.S(32), "Settings", Theme.Heading, Theme.Text, Theme.Bold);

        string[] tabs = ["Game", "Video", "Audio", "Controls"];
        float tx = x + pad, ty = y + pad + Theme.S(46);
        for (int i = 0; i < tabs.Length; i++)
        {
            int c = i;
            Icon[] tIcons = [Icon.Gear, Icon.Image, Icon.Play, Icon.Keyboard];
            float tw = Theme.S(38) + (float)paper.MeasureText(tabs[i], Theme.S(Theme.Caption), Theme.SemiBold, 0f).X;
            Ui.Button(paper, $"st_tab_{i}", tx, ty, tw, Theme.S(28), tabs[i], () => _settingsTab = c, selected: _settingsTab == i,
                ghost: _settingsTab != i, size: Theme.Caption, icon: tIcons[i], radius: Theme.S(14));
            tx += tw + Theme.S(2);
        }

        float cy = ty + Theme.S(56), lx = x + pad, cw = pw - 2 * pad, vx = lx + Theme.S(240), vw = cw - Theme.S(240);
        void Row(string label, string? value = null)
        {
            // Card per setting row (s&box style) so the panel reads as structured, not empty.
            Ui.Panel(paper, $"st_rc_{label}", lx - Theme.S(10), cy - Theme.S(5), cw + Theme.S(20), Theme.S(38), Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
            Ui.Text(paper, $"st_l_{label}", lx, cy, Theme.S(230), Theme.S(28), label, Theme.Body, Theme.Text);
            if (value is not null)
                Ui.Text(paper, $"st_v_{label}", vx, cy, vw, Theme.S(28), value, Theme.Body, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
        }
        float sw = vw - Theme.S(90);
        switch (_settingsTab)
        {
            case 0:
                Row("Mouse sensitivity", $"{s.MouseSensitivity:0.00}×");
                Ui.Slider(paper, "st_sens", vx, cy + Theme.S(2), sw, s.MouseSensitivity, 0.1f, 4f, v => s.MouseSensitivity = v); cy += Theme.S(44);
                Row("Field of view", $"{s.FieldOfView:0}°");
                Ui.Slider(paper, "st_fov", vx, cy + Theme.S(2), sw, s.FieldOfView, 60f, 110f, v => s.FieldOfView = MathF.Round(v)); cy += Theme.S(44);
                Row("View model FOV", $"{s.ViewModelFov:0}°");
                Ui.Slider(paper, "st_vmfov", vx, cy + Theme.S(2), sw, s.ViewModelFov, 50f, 90f, v => s.ViewModelFov = MathF.Round(v)); cy += Theme.S(44);
                Row("UI scale", $"{s.UiScale * 100:0}%");
                Ui.Slider(paper, "st_ui", vx, cy + Theme.S(2), sw, s.UiScale, 0.75f, 1.5f, v => s.UiScale = MathF.Round(v * 20f) / 20f); cy += Theme.S(44);
                Row("Time scale", $"{s.TimeScale:0.00}×");
                Ui.Slider(paper, "st_ts", vx, cy + Theme.S(2), sw, s.TimeScale, 0.1f, 2f, v => s.TimeScale = MathF.Round(v * 20f) / 20f); cy += Theme.S(44);
                Row("Tutorial hints");
                Ui.Toggle(paper, "st_hints", vx, cy + Theme.S(3), s.ShowHints, v => { s.ShowHints = v; if (v) g.Hints.Reset(g); }); cy += Theme.S(44);
                Row("Damage numbers");
                Ui.Toggle(paper, "st_dmgn", vx, cy + Theme.S(3), s.DamageNumbers, v => s.DamageNumbers = v); cy += Theme.S(44);
                break;
            case 1:
                Row("Graphics quality");
                Ui.Segmented(paper, "st_gfx", vx, cy, MathF.Min(vw, Theme.S(330)), ["Low", "Medium", "High"], (int)s.Graphics,
                    v => { s.Graphics = (GraphicsQuality)v; g.ApplySettings(); }); cy += Theme.S(48);
                Row("Window mode");
                Ui.Segmented(paper, "st_wm", vx, cy, MathF.Min(vw, Theme.S(330)), ["Windowed", "Borderless", "Fullscreen"], (int)s.WindowMode,
                    v => { s.WindowMode = (WindowMode)v; g.Enqueue(g.ApplyWindowMode); }); cy += Theme.S(48);
                Row("Resolution");
                {
                    int ri = Math.Max(0, Array.FindIndex(Settings.Resolutions, r => r.W == s.ResolutionWidth && r.H == s.ResolutionHeight));
                    Ui.Segmented(paper, "st_res", vx, cy, vw, Settings.Resolutions.Select(r => r.H >= 2160 ? "4K" : $"{r.H}p").ToArray(), ri,
                        v => { (s.ResolutionWidth, s.ResolutionHeight) = Settings.Resolutions[v]; g.Enqueue(g.ApplyWindowMode); });
                }
                cy += Theme.S(48);
                Row("Frame rate cap");
                {
                    int fi = Math.Max(0, Array.IndexOf(Settings.FrameCaps, s.FrameCap));
                    Ui.Segmented(paper, "st_cap", vx, cy, vw, Settings.FrameCaps.Select(f => f == 0 ? "Off" : f.ToString()).ToArray(), fi, v => s.FrameCap = Settings.FrameCaps[v]);
                }
                cy += Theme.S(48);
                Row("VSync", "applies after restart  ");
                Ui.Toggle(paper, "st_vsync", vx, cy + Theme.S(3), s.VSync, v => s.VSync = v); cy += Theme.S(44);
                Row("Low power mode", "60 FPS, low graphics  ");
                Ui.Toggle(paper, "st_lowpow", vx, cy + Theme.S(3), s.LowPower, v => { s.LowPower = v; g.ApplySettings(); }); cy += Theme.S(44);
                Row("Show FPS counter");
                Ui.Toggle(paper, "st_fps", vx, cy + Theme.S(3), s.ShowFps, v => s.ShowFps = v); cy += Theme.S(44);
                Ui.Paragraph(paper, "st_gfx_h", lx, cy, cw, Theme.S(40),
                    "Low: hard shadows, no post-processing. Medium: soft shadows, FXAA, bloom + tonemapping. High: adds ambient occlusion. Logs and crash reports: " + Log.Dir, Theme.Caption, Theme.TextFaint);
                break;
            case 2:
                Row("Master volume", $"{s.MasterVolume * 100:0}%");
                Ui.Slider(paper, "st_vol", vx, cy + Theme.S(2), sw, s.MasterVolume, 0f, 1f, v => s.MasterVolume = v); cy += Theme.S(44);
                Ui.Paragraph(paper, "st_vol_h", lx, cy, cw, Theme.S(40),
                    g.Sounds.Available ? "Audio device ready." : "No audio device was found; sounds are muted.", Theme.Caption, Theme.TextFaint);
                break;
            case 3:
                float colW = cw / 2f - Theme.S(8);
                for (int i = 0; i < Binds.Actions.Length; i++)
                {
                    var (id, label, _) = Binds.Actions[i];
                    float rx = lx + (i % 2) * (colW + Theme.S(16)), ry = cy + (i / 2) * Theme.S(30);
                    bool clash = Binds.Conflicts(id).Any();
                    Ui.Text(paper, $"st_kb_{i}", rx, ry, colW, Theme.S(24), clash ? "\u26a0 " + label : label, Theme.Body, clash ? Theme.Danger : Theme.TextMuted);
                    string bid = id;
                    Ui.KeyPicker(paper, $"st_kbk_{id}", rx + colW - Theme.S(92), ry, Theme.S(92), Theme.S(24), Binds.Get(id), k => Binds.Set(bid, k));
                }
                float fy = cy + ((Binds.Actions.Length + 1) / 2) * Theme.S(30) + Theme.S(6);
                var clashes = Binds.Actions.Where(a => Binds.Conflicts(a.Id).Any()).GroupBy(a => Binds.Get(a.Id))
                    .Select(gp => $"{Binds.KeyName(gp.Key)}: {string.Join(" + ", gp.Select(a => a.Label))}").ToList();
                Ui.Button(paper, "st_kb_reset", lx + cw - Theme.S(150), fy, Theme.S(150), Theme.S(26), "Reset keys", Binds.ResetAll, ghost: true, icon: Icon.Undo, radius: Theme.S(13));
                Ui.Text(paper, "st_kb_warn", lx, fy, cw - Theme.S(160), Theme.S(26),
                    clashes.Count == 0 ? "Click a key, press the new one (Esc cancels). Shift runs, Ctrl crouches." : "Conflict \u2014 " + string.Join("; ", clashes), Theme.Caption, clashes.Count == 0 ? Theme.TextFaint : Theme.Danger);
                break;
        }
        g.ApplySettingsLive();

        float bw = Theme.S(150), bh = Theme.S(40);
        Ui.Panel(paper, "st_sep", x + pad, y + ph - pad - bh - Theme.S(16), pw - 2 * pad, Theme.S(1), Theme.Hover, 0f, shadow: false).IsNotInteractable();
        Ui.Button(paper, "st_back", x + pw - pad - bw, y + ph - pad - bh, bw, bh, "Done", () => Back(g), selected: true, icon: Icon.Check, radius: bh / 2f);
        Ui.Button(paper, "st_reset", x + pad - Theme.S(14), y + ph - pad - bh, Theme.S(120), bh, "Reset", () => g.ResetSettings(), ghost: true, icon: Icon.Undo, radius: bh / 2f);
    }
}
