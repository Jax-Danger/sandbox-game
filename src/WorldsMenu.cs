using System.Diagnostics;
using Prowl.PaperUI;
using Prowl.Runtime.Resources;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>Singleplayer → Sessions list, Create New Session, the loading screen and the roleplay F4 menu.</summary>
public sealed partial class Menus
{
    private List<WorldEntry>? _worlds;
    private int _selected;
    private float _worldScroll;
    private string? _modal;            // "rename" | "delete"
    private string _renameText = "";
    private SessionMeta _draft = new();
    private string _customPath = "";
    private string? _createError;
    private bool _browsing;
    private int _jobsTab;
    private readonly string[] _lawEdit = new string[6];

    public void RefreshWorlds() { _worlds = Worlds.List(); _selected = Math.Clamp(_selected, 0, Math.Max(0, _worlds.Count - 1)); }

    private static string Ago(DateTime t)
    {
        TimeSpan d = DateTime.Now - t;
        string rel = d.TotalMinutes < 1 ? "just now" : d.TotalHours < 1 ? $"{(int)d.TotalMinutes} min ago" : d.TotalDays < 1 ? $"{(int)d.TotalHours} h ago" : $"{(int)d.TotalDays} d ago";
        return $"{t:ddd d MMM yyyy, HH:mm} ({rel})";
    }

    private static string MapName(SessionMeta m) => m.Map == "custom" ? $"Custom ({m.CustomMap})" : MapBuilder.Maps.FirstOrDefault(x => x.Id == m.Map).Name ?? m.Map;

    // ---------------------------------------------------------------- Worlds list

    private void DrawWorlds(Paper paper, float w, float h, SandboxGame g)
    {
        _worlds ??= Worlds.List();
        float mg = Theme.S(12), pw = w - 2 * mg, ph = h - 2 * mg;
        float x = mg, y = mg + (1f - Ui.Alpha) * Theme.S(20), pad = Theme.S(Theme.Lg);
        Ui.Glass(paper, "wl_panel", x, y, pw, ph, strong: true).IsNotInteractable();
        Ui.Text(paper, "wl_kick", x + pad, y + pad - Theme.S(4), pw, Theme.S(16), "SINGLEPLAYER", Theme.Micro, Theme.Accent, Theme.Bold);
        Ui.Text(paper, "wl_title", x + pad, y + pad + Theme.S(12), pw, Theme.S(34), "Sessions", Theme.Heading, Theme.Text, Theme.Bold);
        Ui.Text(paper, "wl_count", x + pad, y + pad + Theme.S(12), pw - 2 * pad, Theme.S(34), $"{_worlds.Count} session{(_worlds.Count == 1 ? "" : "s")} · autosaves every {SandboxGame.AutosaveInterval:0} s",
            Theme.Caption, Theme.TextFaint, null, TextAlignment.MiddleRight);

        float bh = Theme.S(30), footer = bh + Theme.S(18);
        float ly = y + pad + Theme.S(52), lh = ph - (ly - y) - footer - pad / 2f, lw = pw - 2 * pad;
        Ui.Panel(paper, "wl_list_bg", x + pad, ly, lw, lh, Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();

        float rowH = Theme.S(62), gap = Theme.S(5), inner = Theme.S(6);
        float contentH = _worlds.Count * (rowH + gap) + inner;
        float maxScroll = MathF.Max(0f, contentH - lh);
        _worldScroll = Math.Clamp(_worldScroll, 0f, maxScroll);
        if (_worlds.Count == 0)
        {
            Ui.Icon(paper, "wl_empty_ic", x + pad + lw / 2f - Theme.S(20), ly + lh / 2f - Theme.S(54), Theme.S(40), Icon.Globe, Theme.TextFaint);
            Ui.Text(paper, "wl_empty", x + pad, ly + lh / 2f - Theme.S(6), lw, Theme.S(24), "No sessions yet", Theme.Title, Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
            Ui.Text(paper, "wl_empty2", x + pad, ly + lh / 2f + Theme.S(20), lw, Theme.S(20), "Create one to start building. It saves itself as you play.", Theme.Body, Theme.TextFaint, null, TextAlignment.MiddleCenter);
        }
        using (paper.Box("wl_list").PositionType(PositionType.SelfDirected).Position(x + pad, ly).Size(lw, lh).Clip()
                   .OnScroll(e => _worldScroll = Math.Clamp(_worldScroll - (float)e.Delta * Theme.S(60), 0f, maxScroll)).Enter())
        {
            for (int i = 0; i < _worlds.Count; i++)
            {
                WorldEntry we = _worlds[i];
                float ry = inner + i * (rowH + gap) - _worldScroll;
                if (ry > lh || ry + rowH < 0) continue;
                DrawWorldRow(paper, g, we, i, inner, ry, lw - 2 * inner, rowH, x + pad, ly, lh);
            }
        }
        if (maxScroll > 0f)
        {
            float barH = lh * lh / contentH, barY = ly + (lh - barH) * (_worldScroll / maxScroll);
            Ui.Rect(paper, "wl_scrollbar", x + pad + lw - Theme.S(4), barY, Theme.S(3), barH, Theme.BorderStrong, Theme.S(2));
        }

        // Footer buttons.
        WorldEntry? sel = _worlds.Count > 0 ? _worlds[Math.Clamp(_selected, 0, _worlds.Count - 1)] : null;
        float fy = y + ph - pad - bh, fx = x + pad;
        bool has = sel is not null && _modal is null;
        Ui.Rect(paper, "wl_footer_rule", x + pad, fy - Theme.S(9), pw - 2 * pad, 1f, Theme.Border);
        Ui.Button(paper, "wl_play", fx, fy, Theme.S(136), bh, "Play Selected", () => { if (sel is not null) g.Enqueue(() => g.EnterSession(sel)); },
            selected: has, enabled: has, icon: Icon.Play, radius: bh / 2f);
        fx += Theme.S(142);
        Ui.Button(paper, "wl_new", fx, fy, Theme.S(116), bh, "Create New", () => { BeginCreate(); }, icon: Icon.Plus, radius: bh / 2f, enabled: _modal is null);
        fx += Theme.S(128);
        float sw = Theme.S(78);
        Ui.Button(paper, "wl_edit", fx, fy, sw, bh, "Edit", () => { _renameText = sel!.Meta.Name; _modal = "rename"; }, icon: Icon.Pencil, radius: bh / 2f, enabled: has, ghost: true);
        fx += sw + Theme.S(4);
        Ui.Button(paper, "wl_dup", fx, fy, sw + Theme.S(16), bh, "Duplicate", () => g.Enqueue(() =>
        {
            try { var d = Worlds.Duplicate(sel!); RefreshWorlds(); _selected = _worlds!.FindIndex(x => x.Folder == d.Folder); g.Notify($"Duplicated as {d.Meta.Name}"); }
            catch (Exception e) { g.Notify($"Couldn't duplicate: {e.Message}", ToastKind.Danger); }
        }),
            icon: Icon.Copy, radius: bh / 2f, enabled: has, ghost: true);
        fx += sw + Theme.S(20);
        Ui.Button(paper, "wl_del", fx, fy, sw, bh, "Delete", () => _modal = "delete", icon: Icon.Trash, radius: bh / 2f, enabled: has, ghost: true);
        fx += sw + Theme.S(4);
        Ui.Button(paper, "wl_folder", fx, fy, Theme.S(112), bh, "Open Folder", () => Worlds.OpenFolder(sel?.Folder ?? Worlds.Root), icon: Icon.Folder, radius: bh / 2f, ghost: true, enabled: _modal is null);
        Ui.Button(paper, "wl_back", x + pw - pad - Theme.S(84), fy, Theme.S(84), bh, "Back", () => Go(Screen.MainMenu), icon: Icon.Back, radius: bh / 2f, ghost: true, enabled: _modal is null);

        if (_modal is not null && sel is not null) DrawWorldModal(paper, w, h, g, sel);
    }

    private void DrawWorldRow(Paper paper, SandboxGame g, WorldEntry we, int i, float rx, float ry, float rw, float rh, float ox, float oy, float clipH)
    {
        bool selected = i == _selected;
        bool hover = _modal is null && paper.IsPointerOverRect(ox + rx, oy + ry, rw, rh) && paper.IsPointerOverRect(ox, oy, rw + rx * 2, clipH);
        float k = Ui.SpringTo($"wr_{we.Folder}", hover ? 1f : 0f);
        int capturedI = i;
        paper.Box($"wr_bg_{i}").PositionType(PositionType.SelfDirected).Position(rx, ry).Size(rw, rh)
            .Rounded(Theme.S(Theme.RadiusSm))
            .BackgroundColor(Theme.WithAlpha(selected ? Theme.AccentSoft : Color.FromArgb((int)(18 + 22 * k), 255, 255, 255), Ui.Alpha))
            .BorderColor(Theme.WithAlpha(selected ? Theme.Accent : Theme.Border, Ui.Alpha)).BorderWidth(1f)
            .OnClick(0, (_, _) => { if (_modal is not null) return; if (_selected == capturedI && _lastClick.Elapsed.TotalSeconds < 0.4) g.Enqueue(() => g.EnterSession(we)); _selected = capturedI; _lastClick.Restart(); Ui.ClickSound?.Invoke(); });

        // Thumbnail 16:9.
        float th = rh - Theme.S(10), tw = th * 16f / 9f, tx = rx + Theme.S(5), ty = ry + Theme.S(5);
        Texture2D? thumb = Worlds.Thumbnail(we);
        paper.Box($"wr_th_{i}").PositionType(PositionType.SelfDirected).Position(tx, ty).Size(tw, th).IsNotInteractable()
            .OnPostLayout((handle, rect) => paper.Draw(ref handle, (canvas, r) =>
            {
                float bx = (float)r.Min.X, by = (float)r.Min.Y, bw = (float)r.Size.X, bh = (float)r.Size.Y;
                if (thumb is not null)
                {
                    canvas.SetBrushTexture(thumb);
                    canvas.SetBrushTextureTransform(Prowl.Vector.Spatial.Transform2D.CreateTranslation(bx, by) * Prowl.Vector.Spatial.Transform2D.CreateScale(bw, bh));
                    canvas.RoundedRectFilled(bx, by, bw, bh, Theme.S(6), new Prowl.Vector.Color32(255, 255, 255, (byte)(255 * Ui.Alpha)));
                    canvas.ClearBrushTexture();
                }
                else canvas.RoundedRectFilled(bx, by, bw, bh, Theme.S(6), new Prowl.Vector.Color32(40, 46, 58, (byte)(220 * Ui.Alpha)));
            }));
        if (thumb is null) Ui.Icon(paper, $"wr_thi_{i}", tx + tw / 2f - Theme.S(12), ty + th / 2f - Theme.S(12), Theme.S(24), we.Meta.Mode == "Roleplay" ? Icon.Users : Icon.Globe, Theme.TextFaint);

        float cx = tx + tw + Theme.S(12), cw = rw - (cx - rx) - Theme.S(12);
        Ui.Text(paper, $"wr_n_{i}", cx, ry + Theme.S(7), cw, Theme.S(20), we.Meta.Name, Theme.Label, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, $"wr_d_{i}", cx, ry + Theme.S(27), cw, Theme.S(15), $"Last played {Ago(we.Meta.LastPlayed)}", Theme.Caption, Theme.TextMuted);
        Ui.Text(paper, $"wr_m_{i}", cx, ry + Theme.S(42), cw, Theme.S(15),
            $"{we.Meta.Mode} · {MapName(we.Meta)} · {we.Meta.PropCount} props · {TimeSpan.FromSeconds(we.Meta.PlaySeconds):h\\:mm} played", Theme.Caption, Theme.TextFaint);
        // Mode pill.
        string mode = we.Meta.Mode.ToUpperInvariant();
        float mw = (float)paper.MeasureText(mode, Theme.S(Theme.Micro), Theme.Bold, 0f).X + Theme.S(16);
        Color mc = we.Meta.Mode == "Roleplay" ? Theme.Warning : Theme.Accent;
        Ui.Panel(paper, $"wr_pill_{i}", rx + rw - mw - Theme.S(12), ry + (rh - Theme.S(18)) / 2f, mw, Theme.S(18), Color.FromArgb(40, mc.R, mc.G, mc.B), Theme.S(10), shadow: false).IsNotInteractable();
        Ui.Text(paper, $"wr_pillt_{i}", rx + rw - mw - Theme.S(12), ry + (rh - Theme.S(18)) / 2f, mw, Theme.S(18), mode, Theme.Micro, mc, Theme.Bold, TextAlignment.MiddleCenter);
    }

    private readonly Stopwatch _lastClick = Stopwatch.StartNew();

    private void DrawWorldModal(Paper paper, float w, float h, SandboxGame g, WorldEntry sel)
    {
        paper.Box("wm_dim").PositionType(PositionType.SelfDirected).Position(0, 0).Size(w, h).BackgroundColor(Color.FromArgb(120, 0, 0, 0))
            .OnClick(0, (_, _) => { }).StopEventPropagation();
        float mw = Theme.S(440), mh = Theme.S(_modal == "rename" ? 190 : 176), mx = (w - mw) / 2f, my = (h - mh) / 2f, pad = Theme.S(Theme.Lg);
        Ui.Glass(paper, "wm_panel", mx, my, mw, mh, strong: true).IsNotInteractable();
        float bh = Theme.S(38);
        if (_modal == "rename")
        {
            Ui.Text(paper, "wm_t", mx + pad, my + pad, mw, Theme.S(26), "Rename session", Theme.Title, Theme.Text, Theme.Bold);
            TextInput(paper, "wm_name", mx + pad, my + pad + Theme.S(40), mw - 2 * pad, Theme.S(40), _renameText, v => _renameText = v, "Session name");
            Ui.Button(paper, "wm_ok", mx + mw - pad - Theme.S(110), my + mh - pad - bh, Theme.S(110), bh, "Save", () => g.Enqueue(() =>
            {
                try { var r = Worlds.Rename(sel, _renameText); RefreshWorlds(); _selected = Math.Max(0, _worlds!.FindIndex(x => x.Folder == r.Folder)); }
                catch (Exception e) { g.Notify($"Couldn't rename: {e.Message}", ToastKind.Danger); }
                _modal = null;
            }), selected: true, radius: bh / 2f, enabled: _renameText.Trim().Length > 0);
        }
        else
        {
            Ui.Text(paper, "wm_t", mx + pad, my + pad, mw, Theme.S(26), "Delete session?", Theme.Title, Theme.Text, Theme.Bold);
            Ui.Paragraph(paper, "wm_p", mx + pad, my + pad + Theme.S(34), mw - 2 * pad, Theme.S(44), $"\"{sel.Meta.Name}\" and all of its saves will be permanently deleted. This can't be undone.", Theme.Body, Theme.TextMuted);
            Ui.Button(paper, "wm_ok", mx + mw - pad - Theme.S(120), my + mh - pad - bh, Theme.S(120), bh, "Delete", () => g.Enqueue(() =>
            {
                try { Worlds.Delete(sel); g.Notify($"Deleted {sel.Meta.Name}", ToastKind.Danger); } catch (Exception e) { g.Notify(e.Message, ToastKind.Danger); }
                RefreshWorlds(); _modal = null;
            }), radius: bh / 2f, baseColor: Color.FromArgb(200, 200, 60, 60), icon: Icon.Trash);
        }
        Ui.Button(paper, "wm_cancel", mx + mw - pad - Theme.S(232), my + mh - pad - bh, Theme.S(104), bh, "Cancel", () => _modal = null, ghost: true, radius: bh / 2f);
    }

    public static void TextInput(Paper paper, string id, float x, float y, float w, float h, string value, Action<string> set, string placeholder)
    {
        Ui.Panel(paper, id + "_bg", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        float lh = Theme.S(Theme.Body) * 1.3f;
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x + Theme.S(12), y + (h - lh) / 2f).Size(w - Theme.S(24), lh)
            .FontSize(Theme.S(Theme.Body))
            .TextField(value, Theme.Regular, set, Theme.ToVector(Theme.Text), placeholder, Theme.ToVector(Theme.TextFaint));
    }

    // ---------------------------------------------------------------- Create New Session

    private void BeginCreate()
    {
        int n = (_worlds?.Count ?? 0) + 1;
        _draft = new SessionMeta { Name = $"New Session {n}" };
        _customPath = ""; _createError = null;
        Go(Screen.CreateWorld);
    }

    private void DrawCreate(Paper paper, float w, float h, SandboxGame g)
    {
        float mg = Theme.S(12), pw = w - 2 * mg, ph = h - 2 * mg;
        float x = mg, y = mg + (1f - Ui.Alpha) * Theme.S(20), pad = Theme.S(Theme.Lg);
        Ui.Glass(paper, "cw_panel", x, y, pw, ph, strong: true).IsNotInteractable();
        Ui.Text(paper, "cw_kick", x + pad, y + pad - Theme.S(4), pw, Theme.S(16), "SINGLEPLAYER", Theme.Micro, Theme.Accent, Theme.Bold);
        Ui.Text(paper, "cw_title", x + pad, y + pad + Theme.S(12), pw, Theme.S(34), "Create New Session", Theme.Heading, Theme.Text, Theme.Bold);

        float colW = (pw - 2 * pad - Theme.S(28)) / 2f, lx = x + pad, rx = lx + colW + Theme.S(28);
        float cy = y + pad + Theme.S(54);
        SessionMeta d = _draft; WorldModifiers m = d.Modifiers;

        void Label(string id, float px, float py, string t) => Ui.Text(paper, id, px, py, colW, Theme.S(18), t, Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        Label("cw_l_name", lx, cy, "Session name");
        TextInput(paper, "cw_name", lx, cy + Theme.S(20), colW, Theme.S(32), d.Name, v => d.Name = v, "My session");
        cy += Theme.S(64);
        Label("cw_l_mode", lx, cy, "Game mode");
        Ui.Segmented(paper, "cw_mode", lx, cy + Theme.S(20), colW, ["Sandbox", "Roleplay"], d.Mode == "Roleplay" ? 1 : 0, v => d.Mode = v == 1 ? "Roleplay" : "Sandbox");
        Ui.Paragraph(paper, "cw_mode_d", lx, cy + Theme.S(58), colW, Theme.S(40), d.Mode == "Roleplay"
            ? "DarkRP-style: jobs, salaries, money, printers, laws and NPC citizens. Press F4 in game for jobs and the shop."
            : "Classic sandbox: spawn anything, build contraptions, no rules.", Theme.Caption, Theme.TextFaint);
        cy += Theme.S(96);
        Label("cw_l_map", lx, cy, "Map");
        int mapIdx = Math.Max(0, Array.FindIndex(MapBuilder.Maps, mm => mm.Id == d.Map));
        Ui.Segmented(paper, "cw_map", lx, cy + Theme.S(20), colW, MapBuilder.Maps.Select(mm => mm.Name).ToArray(), mapIdx, v => d.Map = MapBuilder.Maps[v].Id);
        Ui.Paragraph(paper, "cw_map_d", lx, cy + Theme.S(58), colW, Theme.S(22), MapBuilder.Maps[mapIdx].Description, Theme.Caption, Theme.TextFaint);
        cy += Theme.S(92);
        if (d.Map == "custom")
        {
            TextInput(paper, "cw_path", lx, cy, colW - Theme.S(108), Theme.S(40), _customPath, v => _customPath = v, "/path/to/map.glb");
            Ui.Button(paper, "cw_browse", lx + colW - Theme.S(100), cy, Theme.S(100), Theme.S(40), _browsing ? "…" : "Browse…", () => Browse(g), icon: Icon.Folder, enabled: !_browsing);
            bool okExt = MapBuilder.ImportExtensions.Contains(Path.GetExtension(_customPath).ToLowerInvariant());
            Ui.Text(paper, "cw_path_h", lx, cy + Theme.S(46), colW, Theme.S(18),
                _customPath.Length == 0 ? "glb, gltf, fbx, obj or vrm. It's copied into the session folder." : !File.Exists(_customPath) ? "File not found" : okExt ? "Ready to import (static mesh colliders are generated)" : "Unsupported file type",
                Theme.Caption, _customPath.Length > 0 && (!File.Exists(_customPath) || !okExt) ? Theme.Danger : Theme.TextFaint);
        }

        // Right column: modifiers.
        float ry = y + pad + Theme.S(54);
        Ui.Text(paper, "cw_mods", rx, ry, colW, Theme.S(18), "Modifiers", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        ry += Theme.S(20);
        float rowH = Theme.S(36), sx = rx + Theme.S(150), sw = colW - Theme.S(150) - Theme.S(56);
        void Row(string id, string label, string? value)
        {
            Ui.Panel(paper, id + "_bg", rx, ry, colW, rowH - Theme.S(6), Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
            Ui.Text(paper, id + "_l", rx + Theme.S(12), ry, Theme.S(140), rowH - Theme.S(6), label, Theme.Body, Theme.Text);
            if (value is not null) Ui.Text(paper, id + "_v", rx, ry, colW - Theme.S(12), rowH - Theme.S(6), value, Theme.Body, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
        }
        float sy() => ry + (rowH - Theme.S(6) - Theme.S(24)) / 2f;
        Row("cw_g", "Gravity", $"{m.Gravity:0.0}"); Ui.Slider(paper, "cw_g_s", sx, sy(), sw, m.Gravity, 1f, 25f, v => m.Gravity = MathF.Round(v * 10f) / 10f); ry += rowH;
        Row("cw_pl", "Prop limit", $"{m.PropLimit}"); Ui.Slider(paper, "cw_pl_s", sx, sy(), sw, m.PropLimit, 50f, 1000f, v => m.PropLimit = (int)(MathF.Round(v / 10f) * 10f)); ry += rowH;
        // Toggles in a tight two-column grid.
        var toggles = new List<(string Id, string Label, bool Val, Action<bool> Set)>
        {
            ("cw_fd", "Fall damage", m.FallDamage, v => m.FallDamage = v), ("cw_dn", "Day/night", m.DayNight, v => m.DayNight = v),
            ("cw_npc", "NPCs", m.Npcs, v => m.Npcs = v),
        };
        if (d.Mode == "Roleplay") toggles.Add(("cw_ps", "Props cost money", m.PaidSpawning, v => m.PaidSpawning = v));
        float half = (colW - Theme.S(6)) / 2f;
        for (int ti = 0; ti < toggles.Count; ti++)
        {
            var t = toggles[ti];
            float tx2 = rx + (ti % 2) * (half + Theme.S(6)), ty2 = ry + (ti / 2) * rowH;
            Ui.Panel(paper, t.Id + "_bg", tx2, ty2, half, rowH - Theme.S(6), Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
            Ui.Text(paper, t.Id + "_l", tx2 + Theme.S(10), ty2, half, rowH - Theme.S(6), t.Label, Theme.Body, Theme.Text);
            Ui.Toggle(paper, t.Id + "_t", tx2 + half - Theme.S(50), ty2 + (rowH - Theme.S(6) - Theme.S(22)) / 2f, t.Val, t.Set);
        }
        ry += ((toggles.Count + 1) / 2) * rowH;
        if (d.Mode == "Roleplay")
        {
            Row("cw_sm", "Starting money", $"${m.StartingMoney:N0}"); Ui.Slider(paper, "cw_sm_s", sx, sy(), sw - Theme.S(30), m.StartingMoney, 0f, 10000f, v => m.StartingMoney = (int)(MathF.Round(v / 50f) * 50f)); ry += rowH;
        }

        float bh = Theme.S(32), fy = y + ph - pad - bh;
        Ui.Rect(paper, "cw_footer_rule", x + pad, fy - Theme.S(10), pw - 2 * pad, 1f, Theme.Border);
        if (_createError is not null) Ui.Text(paper, "cw_err", x + pad, fy - Theme.S(28), pw - 2 * pad, Theme.S(20), _createError, Theme.Caption, Theme.Danger, Theme.SemiBold);
        Ui.Button(paper, "cw_create", x + pw - pad - Theme.S(150), fy, Theme.S(150), bh, "Create Session", () => g.Enqueue(() => Create(g)), selected: true, icon: Icon.Check, radius: bh / 2f);
        Ui.Button(paper, "cw_cancel", x + pw - pad - Theme.S(246), fy, Theme.S(90), bh, "Cancel", () => Go(Screen.Worlds), ghost: true, radius: bh / 2f);
    }

    private void Create(SandboxGame g)
    {
        _draft.Name = _draft.Name.Trim();
        if (_draft.Name.Length == 0) { _createError = "Give the session a name."; return; }
        string? src = null;
        if (_draft.Map == "custom")
        {
            src = _customPath.Trim().Trim('"');
            if (!File.Exists(src)) { _createError = "Pick a map file first (Browse… or paste a path)."; return; }
            if (!MapBuilder.ImportExtensions.Contains(Path.GetExtension(src).ToLowerInvariant())) { _createError = "Unsupported map file. Use glb, gltf, fbx, obj or vrm."; return; }
        }
        try
        {
            WorldEntry we = Worlds.Create(_draft, src);
            RefreshWorlds();
            g.EnterSession(we);
        }
        catch (Exception e) { _createError = $"Couldn't create the session: {e.Message}"; }
    }

    /// <summary>Native file picker on a background task (zenity/kdialog on Linux, osascript on macOS, PowerShell on Windows).</summary>
    private void Browse(SandboxGame g)
    {
        _browsing = true;
        Task.Run(() =>
        {
            string? path = null;
            try
            {
                (string Exe, string[] Args)[] tries = OperatingSystem.IsWindows()
                    ? [("powershell", ["-NoProfile", "-STA", "-Command", "Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.OpenFileDialog; $d.Filter = 'Models|*.glb;*.gltf;*.fbx;*.obj;*.vrm'; if ($d.ShowDialog() -eq 'OK') { $d.FileName }"])]
                    : OperatingSystem.IsMacOS()
                        ? [("osascript", ["-e", "POSIX path of (choose file with prompt \"Choose a map model\" of type {\"glb\",\"gltf\",\"fbx\",\"obj\",\"vrm\"})"])]
                        : [("zenity", ["--file-selection", "--title=Choose a map model", "--file-filter=Models | *.glb *.gltf *.fbx *.obj *.vrm"]), ("kdialog", ["--getopenfilename", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "*.glb *.gltf *.fbx *.obj *.vrm"])];
                foreach (var (exe, args) in tries)
                {
                    try
                    {
                        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, UseShellExecute = false };
                        foreach (string a in args) psi.ArgumentList.Add(a);
                        using Process? p = Process.Start(psi);
                        if (p is null) continue;
                        string outp = p.StandardOutput.ReadToEnd().Trim();
                        p.WaitForExit();
                        path = outp.Length > 0 ? outp : null;
                        break;
                    }
                    catch { /* try the next picker */ }
                }
            }
            finally
            {
                g.Enqueue(() =>
                {
                    _browsing = false;
                    if (path is not null) _customPath = path;
                    else if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS()) _createError ??= "No file picker found (install zenity) — paste the path instead.";
                });
            }
        });
    }

    // ---------------------------------------------------------------- Loading screen

    private static readonly string[] Tips =
    [
        "Hold Q to open the spawn menu. Drag anything you spawn with the physics gun.",
        "Right-click with the physics gun to freeze a prop in place.",
        "The session autosaves every few seconds. Your contraptions keep moving when you come back.",
        "Hold C on a prop to change its colour, material and mass.",
        "Press Z to undo your last spawn or weld.",
        "In Roleplay, press F4 to pick a job or buy a money printer.",
        "Press F for the flashlight and hold B to zoom.",
        "Weld wheels and thrusters to a plank to build a car.",
    ];

    private void DrawLoading(Paper paper, float w, float h, SandboxGame g)
    {
        SessionLoader? l = g.Loader;
        float progress = l?.Progress ?? 1f;
        float shown = Ui.SpringTo("ld_prog", progress, 0f);
        paper.Box("ld_bg").PositionType(PositionType.SelfDirected).Position(0, 0).Size(w, h)
            .BackgroundLinearGradient(0, 0, 0, 1, Color.FromArgb(255, 14, 18, 26), Color.FromArgb(255, 6, 8, 12))
            .OnClick(0, (_, _) => { }).StopEventPropagation();
        // Soft accent glow.
        paper.Box("ld_glow").PositionType(PositionType.SelfDirected).Position(w * 0.15f, h * 0.05f).Size(w * 0.7f, h * 0.7f).IsNotInteractable()
            .BackgroundRadialGradient(w * 0.35f, h * 0.35f, 0f, w * 0.35f, Color.FromArgb(46, 77, 163, 255), Color.FromArgb(0, 77, 163, 255));

        float cx = w / 2f, by = h * 0.62f, bw = MathF.Min(Theme.S(560), w * 0.7f);
        DrawLogo(paper, cx - Theme.S(32), h * 0.24f, Theme.S(64), "ld_logo", (float)(l?.Elapsed ?? 0f));
        string name = g.Session?.Meta.Name ?? "Session";
        Ui.Text(paper, "ld_name", 0, h * 0.24f + Theme.S(80), w, Theme.S(40), name, Theme.Heading + 6, Theme.Text, Theme.Bold, TextAlignment.MiddleCenter);
        if (g.Session is { } s)
            Ui.Text(paper, "ld_sub", 0, h * 0.24f + Theme.S(122), w, Theme.S(20), $"{s.Meta.Mode} · {MapName(s.Meta)} · {s.Meta.PropCount} props", Theme.Body, Theme.TextMuted, null, TextAlignment.MiddleCenter);

        string stage = l?.Stage ?? "Ready";
        Ui.Text(paper, "ld_stage", cx - bw / 2f, by - Theme.S(30), bw, Theme.S(20), stage, Theme.Body, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "ld_pct", cx - bw / 2f, by - Theme.S(30), bw, Theme.S(20), $"{shown * 100f:0}%", Theme.Body, Theme.Accent, Theme.Bold, TextAlignment.MiddleRight);
        float barH = Theme.S(6);
        Ui.Rect(paper, "ld_track", cx - bw / 2f, by, bw, barH, Color.FromArgb(40, 255, 255, 255), barH / 2f);
        float fw = MathF.Max(barH, bw * Math.Clamp(shown, 0f, 1f));
        paper.Box("ld_fill").PositionType(PositionType.SelfDirected).Position(cx - bw / 2f, by).Size(fw, barH).IsNotInteractable().Rounded(barH / 2f)
            .BackgroundLinearGradient(0, 0, 1, 0, Color.FromArgb(255, 77, 163, 255), Color.FromArgb(255, 140, 200, 255));
        // Shimmer sweeping across the filled part.
        float t = ((float)(l?.Elapsed ?? 0f) * 0.8f) % 1f;
        float sw = Theme.S(60), sx = cx - bw / 2f + (fw + sw) * t - sw;
        if (sx + sw > cx - bw / 2f && sx < cx - bw / 2f + fw)
            paper.Box("ld_shimmer").PositionType(PositionType.SelfDirected).Position(MathF.Max(cx - bw / 2f, sx), by).Size(MathF.Min(sw, cx - bw / 2f + fw - MathF.Max(cx - bw / 2f, sx)), barH).IsNotInteractable().Rounded(barH / 2f)
                .BackgroundLinearGradient(0, 0, 1, 0, Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255));

        int tip = (int)((l?.Elapsed ?? 0f) / 4f + (g.Session?.Meta.Seed ?? 0) % Tips.Length) % Tips.Length;
        Ui.Text(paper, "ld_tipk", 0, h - Theme.S(96), w, Theme.S(16), "TIP", Theme.Micro, Theme.Accent, Theme.Bold, TextAlignment.MiddleCenter);
        Ui.Text(paper, "ld_tip", 0, h - Theme.S(76), w, Theme.S(22), Tips[tip], Theme.Body, Theme.TextMuted, null, TextAlignment.MiddleCenter);
        if (l?.Error is { } err) Ui.Text(paper, "ld_err", 0, by + Theme.S(24), w, Theme.S(20), err, Theme.Body, Theme.Danger, Theme.SemiBold, TextAlignment.MiddleCenter);
    }

    /// <summary>The Sandbox logo mark: an accent rounded square with an isometric cube, optionally spinning slowly.</summary>
    public static void DrawLogo(Paper paper, float x, float y, float size, string id, float time = 0f)
    {
        paper.Box(id + "_bg").PositionType(PositionType.SelfDirected).Position(x, y).Size(size, size).IsNotInteractable()
            .Rounded(size * 0.26f)
            .BackgroundLinearGradient(0, 0, 1, 1, Theme.WithAlpha(Color.FromArgb(255, 95, 178, 255), Ui.Alpha), Theme.WithAlpha(Color.FromArgb(255, 48, 110, 230), Ui.Alpha))
            .BorderColor(Theme.WithAlpha(Color.FromArgb(90, 255, 255, 255), Ui.Alpha)).BorderWidth(1f);
        float k = 1f + 0.04f * MathF.Sin(time * 3f);
        float isz = size * 0.62f * k;
        Ui.Icon(paper, id + "_ic", x + (size - isz) / 2f, y + (size - isz) / 2f, isz, Icon.Cube, Color.White);
    }

    // ---------------------------------------------------------------- Roleplay F4 menu

    public void OpenBank() { _jobsTab = 3; Go(Screen.Jobs); }
    public void OpenShop() { _jobsTab = 1; Go(Screen.Jobs); }

    private string? _jobPick;

    private static Icon WeaponIcon(string id) => id switch
    {
        "weapon_baton" => Icon.Gavel, "weapon_lockpick" => Icon.Key, "weapon_medkit" => Icon.Heart,
        "weapon_smg" => Icon.Smg, "weapon_shotgun" => Icon.Shotgun, _ => Icon.Pistol,
    };

    private void DrawJobs(Paper paper, float w, float h, SandboxGame g)
    {
        if (Screen != Screen.Jobs) g.Roleplay.AtAtm = false;
        RoleplaySystem rp = g.Roleplay;
        // Same frame as the spawn menu: nearly full screen, glass, pill tab strip.
        float margin = Theme.S(12);
        float pw = w - 2 * margin, ph = h - 2 * margin;
        float x = margin, y = margin + (1f - Ui.Alpha) * Theme.S(24), pad = Theme.S(Theme.Sm + 2);
        Ui.Glass(paper, "jb_panel", x, y, pw, ph, strong: true).IsNotInteractable();

        string[] tabs = ["Jobs", "Shop", "Laws", "Money"];
        Icon[] tabIcons = [Icon.Users, Icon.Briefcase, Icon.Gavel, Icon.Money];
        float tabH = Theme.S(28), tx = x + pad, ty = y + pad;
        for (int i = 0; i < tabs.Length; i++)
        {
            int c = i;
            float tw = Theme.S(36) + (float)paper.MeasureText(tabs[i], Theme.S(Theme.Caption), Theme.SemiBold, 0f).X;
            Ui.Button(paper, $"jb_tab_{i}", tx, ty, tw, tabH, tabs[i], () => _jobsTab = c, selected: _jobsTab == i, ghost: _jobsTab != i,
                size: Theme.Caption, icon: tabIcons[i], radius: tabH / 2f);
            tx += tw + Theme.S(2);
        }
        // Status chips on the right of the strip: wallet, job, payday.
        float chx = x + pw - pad;
        void Chip(string id, string text, Color fg, Color? dot = null)
        {
            float cw0 = (float)paper.MeasureText(text, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X + Theme.S(dot is null ? 20 : 30);
            chx -= cw0;
            Ui.Panel(paper, id, chx, ty, cw0, tabH, Theme.PanelRaised, tabH / 2f, shadow: false).IsNotInteractable();
            if (dot is { } d) Ui.Rect(paper, id + "_d", chx + Theme.S(11), ty + tabH / 2f - Theme.S(4), Theme.S(8), Theme.S(8), d, Theme.S(4));
            Ui.Text(paper, id + "_t", chx + Theme.S(dot is null ? 10 : 24), ty, cw0, tabH, text, Theme.Caption, fg, Theme.SemiBold);
            chx -= Theme.S(4);
        }
        Chip("jb_c_pay", $"payday {rp.PayTimer:0}s", Theme.TextMuted);
        Chip("jb_c_job", rp.Job.Name, Theme.Text, rp.Job.Color);
        Chip("jb_c_cash", $"${rp.Money:N0}", Theme.Success);

        float cy = ty + tabH + pad, cw = pw - 2 * pad, chH = y + ph - pad - cy;
        switch (_jobsTab)
        {
            case 0:
            {
                Job pick = RoleplaySystem.Jobs.FirstOrDefault(j => j.Id == _jobPick) ?? rp.Job;
                float colW = Theme.S(260), gap = Theme.S(Theme.Sm);
                float gw = cw - colW - gap, gx = x + pad;
                float cardW0 = Theme.S(190), cgap = Theme.S(6);
                int cols = Math.Max(2, (int)((gw + cgap) / (cardW0 + cgap)));
                float cardW = (gw - (cols - 1) * cgap) / cols, cardH = Theme.S(124), band = Theme.S(46);
                for (int i = 0; i < RoleplaySystem.Jobs.Length; i++)
                {
                    Job j = RoleplaySystem.Jobs[i];
                    float jx = gx + (i % cols) * (cardW + cgap), jy = cy + (i / cols) * (cardH + cgap);
                    bool cur = j.Id == rp.Job.Id, sel = j.Id == pick.Id;
                    bool hover = paper.IsPointerOverRect(jx, jy, cardW, cardH);
                    Job cj = j;
                    paper.Box($"jb_card_{i}").PositionType(PositionType.SelfDirected).Position(jx, jy).Size(cardW, cardH).Rounded(Theme.S(Theme.RadiusSm))
                        .BackgroundColor(Theme.WithAlpha(hover ? Theme.Hover : Theme.PanelRaised, Ui.Alpha))
                        .BorderColor(Theme.WithAlpha(sel ? j.Color : hover ? Theme.BorderStrong : Theme.Border, Ui.Alpha)).BorderWidth(sel ? 1.5f : 1f)
                        .OnClick(0, (_, _) => { Ui.ClickSound?.Invoke(); _jobPick = cj.Id; });
                    // Colour band with the job's avatar.
                    Ui.Rect(paper, $"jb_band_{i}", jx + 1, jy + 1, cardW - 2, band, Color.FromArgb((int)(Ui.Alpha * 70), j.Color.R, j.Color.G, j.Color.B), Theme.S(Theme.RadiusSm) - 1);
                    Ui.Rect(paper, $"jb_av_{i}", jx + Theme.S(10), jy + Theme.S(9), band - Theme.S(18), band - Theme.S(18), j.Color, (band - Theme.S(18)) / 2f);
                    Ui.Icon(paper, $"jb_avi_{i}", jx + Theme.S(15), jy + Theme.S(14), band - Theme.S(28), Icon.Users, Color.White);
                    Ui.Text(paper, $"jb_n_{i}", jx + band + Theme.S(2), jy + Theme.S(6), cardW - band, Theme.S(20), j.Name, Theme.Label, Theme.Text, Theme.Bold);
                    Ui.Text(paper, $"jb_s_{i}", jx + band + Theme.S(2), jy + Theme.S(23), cardW - band, Theme.S(16), $"${j.Salary} / payday", Theme.Caption, Theme.Success, Theme.SemiBold);
                    if (cur) Ui.Text(paper, $"jb_cur_{i}", jx, jy + Theme.S(6), cardW - Theme.S(10), Theme.S(16), "CURRENT", Theme.Micro, j.Color, Theme.Bold, TextAlignment.MiddleRight);
                    Ui.Paragraph(paper, $"jb_d_{i}", jx + Theme.S(10), jy + band + Theme.S(6), cardW - Theme.S(20), Theme.S(30), j.Description, Theme.Micro, Theme.TextMuted);
                    // Slots: filled bar (∞ for unlimited) + loadout icons.
                    int taken = rp.CountInJob(j);
                    float sy = jy + cardH - Theme.S(18);
                    if (j.Max > 0)
                        for (int k = 0; k < j.Max; k++)
                            Ui.Rect(paper, $"jb_sl_{i}_{k}", jx + Theme.S(10) + k * Theme.S(12), sy + Theme.S(4), Theme.S(9), Theme.S(6), k < taken ? j.Color : Theme.Border, Theme.S(2));
                    else Ui.Text(paper, $"jb_inf_{i}", jx + Theme.S(10), sy, Theme.S(60), Theme.S(14), "unlimited", Theme.Micro, Theme.TextFaint, Theme.SemiBold);
                    for (int k = 0; k < j.Loadout.Length; k++)
                        Ui.Icon(paper, $"jb_lo_{i}_{k}", jx + cardW - Theme.S(22) - k * Theme.S(18), sy - Theme.S(1), Theme.S(14), WeaponIcon(j.Loadout[k]), Theme.TextMuted);
                }

                // Detail column.
                float dx = x + pw - pad - colW, dy = cy, dh = chH;
                Ui.Panel(paper, "jb_det", dx, dy, colW, dh, Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
                float ip = Theme.S(Theme.Md), ix = dx + ip, iw = colW - 2 * ip, iy = dy + ip;
                Ui.Rect(paper, "jb_det_band", dx + 1, dy + 1, colW - 2, Theme.S(64), Color.FromArgb((int)(Ui.Alpha * 80), pick.Color.R, pick.Color.G, pick.Color.B), Theme.S(Theme.RadiusSm) - 1);
                Ui.Rect(paper, "jb_det_av", ix, iy + Theme.S(4), Theme.S(40), Theme.S(40), pick.Color, Theme.S(20));
                Ui.Icon(paper, "jb_det_avi", ix + Theme.S(9), iy + Theme.S(13), Theme.S(22), Icon.Users, Color.White);
                Ui.Text(paper, "jb_det_k", ix + Theme.S(50), iy + Theme.S(2), iw, Theme.S(14), pick.Id == rp.Job.Id ? "YOUR JOB" : "JOB", Theme.Micro, Theme.Text, Theme.Bold);
                Ui.Text(paper, "jb_det_n", ix + Theme.S(50), iy + Theme.S(16), iw, Theme.S(26), pick.Name, Theme.Title, Theme.Text, Theme.Bold);
                iy += Theme.S(64) + Theme.S(4);
                (string L, string V)[] stats = [("Salary", $"${pick.Salary}"), ("Slots", pick.Max == 0 ? "∞" : $"{rp.CountInJob(pick)}/{pick.Max}"), ("Perk", pick.Perk)];
                foreach (var (l, v) in stats)
                {
                    Ui.Text(paper, $"jb_det_{l}", ix, iy, iw, Theme.S(20), l, Theme.Caption, Theme.TextMuted);
                    Ui.Text(paper, $"jb_det_{l}_v", ix, iy, iw, Theme.S(20), v, Theme.Caption, l == "Salary" ? Theme.Success : Theme.Text, Theme.SemiBold, TextAlignment.MiddleRight);
                    iy += Theme.S(21);
                }
                iy += Theme.S(4);
                Ui.Rect(paper, "jb_det_sep", ix, iy, iw, 1f, Theme.Border); iy += Theme.S(8);
                Ui.Paragraph(paper, "jb_det_d", ix, iy, iw, Theme.S(48), pick.Description, Theme.Caption, Theme.TextMuted);
                iy += Theme.S(52);
                Ui.Text(paper, "jb_det_lo", ix, iy, iw, Theme.S(16), "LOADOUT", Theme.Micro, Theme.TextFaint, Theme.Bold);
                iy += Theme.S(18);
                if (pick.Loadout.Length == 0) { Ui.Text(paper, "jb_det_lo0", ix, iy, iw, Theme.S(20), "Hands, physgun and tool gun", Theme.Caption, Theme.TextFaint); iy += Theme.S(24); }
                for (int k = 0; k < pick.Loadout.Length; k++)
                {
                    string wid = pick.Loadout[k];
                    string wn = g.Weapons.All.FirstOrDefault(wp => wp.Id == wid)?.Name ?? wid.Replace("weapon_", "");
                    Ui.Panel(paper, $"jb_det_w_{k}", ix, iy, iw, Theme.S(32), Theme.PanelRaised, Theme.S(Theme.RadiusXs), shadow: false).IsNotInteractable();
                    Ui.Icon(paper, $"jb_det_wi_{k}", ix + Theme.S(9), iy + Theme.S(8), Theme.S(16), WeaponIcon(wid), Theme.Text);
                    Ui.Text(paper, $"jb_det_wn_{k}", ix + Theme.S(34), iy, iw, Theme.S(32), wn, Theme.Caption, Theme.Text, Theme.SemiBold);
                    iy += Theme.S(36);
                }
                bool isCur = pick.Id == rp.Job.Id;
                float bh = Theme.S(30);
                Job pj = pick;
                Ui.Button(paper, "jb_become", ix, dy + dh - ip - bh, iw, bh, isCur ? "Current job" : $"Become {pick.Name}",
                    () => g.Enqueue(() => g.Notify(rp.SetJob(pj))), selected: !isCur, enabled: !isCur, icon: isCur ? Icon.Check : Icon.Briefcase, radius: bh / 2f, size: Theme.Caption);
                break;
            }
            case 1:
            {
                int cols = 3; float gap = Theme.S(10), cardW = (cw - 2 * gap) / cols, cardH = Theme.S(108);
                for (int i = 0; i < RoleplaySystem.Shop.Length; i++)
                {
                    Buyable b = RoleplaySystem.Shop[i];
                    float bx = x + pad + (i % cols) * (cardW + gap), by = cy + (i / cols) * (cardH + gap);
                    bool locked = b.RequiresJob is { } rj && rj != rp.Job.Id;
                    Ui.Panel(paper, $"sh_bg_{i}", bx, by, cardW, cardH, Theme.PanelRaised, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
                    Ui.Icon(paper, $"sh_ic_{i}", bx + Theme.S(14), by + Theme.S(14), Theme.S(22), b.Icon, locked ? Theme.TextFaint : Theme.Text);
                    Ui.Text(paper, $"sh_n_{i}", bx + Theme.S(46), by + Theme.S(12), cardW, Theme.S(24), b.Name, Theme.Label, locked ? Theme.TextFaint : Theme.Text, Theme.SemiBold);
                    Ui.Paragraph(paper, $"sh_d_{i}", bx + Theme.S(14), by + Theme.S(42), cardW - Theme.S(28), Theme.S(30), locked ? $"{RoleplaySystem.Jobs.First(j => j.Id == b.RequiresJob).Name} only. {b.Description}" : b.Description, Theme.Caption, Theme.TextMuted);
                    Buyable cb = b;
                    Ui.Button(paper, $"sh_buy_{i}", bx + cardW - Theme.S(104), by + cardH - Theme.S(36), Theme.S(92), Theme.S(28), $"${rp.PriceOf(b):N0}",
                        () => g.Enqueue(() => { string r = rp.Buy(cb); if (r.Length > 0) g.Notify(r); }), enabled: !locked, selected: !locked && rp.Money >= rp.PriceOf(b), size: Theme.Body, radius: Theme.S(14));
                }
                break;
            }
            case 2:
            {
                bool mayor = rp.Job.Id == "mayor";
                Ui.Text(paper, "lw_h", x + pad, cy, cw, Theme.S(20), mayor ? "You're the mayor: edit the laws everyone sees on the board." : "Only the Mayor can change the laws.", Theme.Body, Theme.TextMuted);
                for (int i = 0; i < 6; i++)
                {
                    float ly = cy + Theme.S(32) + i * Theme.S(48);
                    Ui.Text(paper, $"lw_n_{i}", x + pad, ly, Theme.S(28), Theme.S(40), $"{i + 1}.", Theme.Label, Theme.TextFaint, Theme.SemiBold);
                    string cur = i < rp.Laws.Count ? rp.Laws[i] : "";
                    if (mayor)
                    {
                        _lawEdit[i] ??= cur;
                        int ci = i;
                        TextInput(paper, $"lw_e_{i}", x + pad + Theme.S(32), ly, cw - Theme.S(32), Theme.S(40), _lawEdit[i], v => _lawEdit[ci] = v, "Add a law…");
                    }
                    else Ui.Text(paper, $"lw_t_{i}", x + pad + Theme.S(32), ly, cw, Theme.S(40), cur.Length > 0 ? cur : "—", Theme.Label, cur.Length > 0 ? Theme.Text : Theme.TextFaint);
                }
                if (mayor)
                    Ui.Button(paper, "lw_save", x + pw - pad - Theme.S(150), y + ph - pad - Theme.S(40), Theme.S(150), Theme.S(40), "Update laws", () =>
                    {
                        rp.Laws.Clear(); rp.Laws.AddRange(_lawEdit.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
                        g.Notify("The laws were updated");
                    }, selected: true, icon: Icon.Gavel, radius: Theme.S(20));
                break;
            }
            case 3:
            {
                Ui.Text(paper, "mn_h", x + pad, cy, cw, Theme.S(20), "Drop cash for someone (or yourself) to pick up with E.", Theme.Body, Theme.TextMuted);
                int[] amounts = [50, 100, 500, 1000, 5000];
                for (int i = 0; i < amounts.Length; i++)
                {
                    int a = amounts[i];
                    Ui.Button(paper, $"mn_drop_{i}", x + pad + i * Theme.S(130), cy + Theme.S(34), Theme.S(120), Theme.S(44), $"Drop ${a:N0}", () => g.Enqueue(() => rp.DropMoney(a)),
                        enabled: rp.Money >= a, icon: Icon.Money, radius: Theme.S(Theme.RadiusSm));
                }
                // Bank: only usable at an ATM.
                float by = cy + Theme.S(100);
                Ui.Panel(paper, "mn_bank", x + pad, by, cw, Theme.S(104), Theme.WithAlpha(System.Drawing.Color.FromArgb(26, 255, 255, 255), 1f), Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
                Ui.Icon(paper, "mn_bank_ic", x + pad + Theme.S(16), by + Theme.S(16), Theme.S(18), Icon.Briefcase, Theme.Accent);
                Ui.Text(paper, "mn_bank_t", x + pad + Theme.S(44), by + Theme.S(10), cw, Theme.S(28), $"Bank  ${rp.Bank:N0}", Theme.Title, Theme.Text, Theme.Bold);
                Ui.Text(paper, "mn_bank_s", x + pad + Theme.S(44), by + Theme.S(36), cw, Theme.S(18), rp.AtAtm ? "ATM connected · money in the bank is safe when you die" : "Find an ATM (plaza) to deposit or withdraw", Theme.Caption, Theme.TextMuted);
                (string L, Func<string> A)[] ops = [("Deposit $100", () => rp.Deposit(100)), ("Deposit all", () => rp.Deposit(rp.Money)), ("Withdraw $100", () => rp.Withdraw(100)), ("Withdraw $500", () => rp.Withdraw(500))];
                for (int i = 0; i < ops.Length; i++)
                {
                    var op = ops[i];
                    Ui.Button(paper, $"mn_bank_{i}", x + pad + Theme.S(44) + i * Theme.S(138), by + Theme.S(62), Theme.S(130), Theme.S(30), op.L, () => g.Enqueue(() => g.Notify(op.A())),
                        enabled: rp.AtAtm, size: Theme.Caption, radius: Theme.S(15), ghost: i >= 2);
                }
                Ui.Paragraph(paper, "mn_p", x + pad, cy + Theme.S(220), cw, Theme.S(80),
                    $"Salary: ${rp.Job.Salary} every {RoleplaySystem.PayInterval:0} s. Props cost ${RoleplaySystem.PropCost} each when paid spawning is on. Money printers print $25 every 20 s (gangsters get 25% more); press E on a printer to collect. Dying drops 10% of your wallet.",
                    Theme.Caption, Theme.TextFaint);
                break;
            }
        }
        if (_jobsTab != 0) Ui.Text(paper, "jb_hint", x + pad, y + ph - pad - Theme.S(16), pw, Theme.S(16), "F4 or Esc to close", Theme.Caption, Theme.TextFaint);
    }
}
