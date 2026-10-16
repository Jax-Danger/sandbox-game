using Prowl.PaperUI;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum MenuTab { Spawnlists, Entities, Vehicles, Weapons, Tools, Utilities }

/// <summary>
/// Spawn menu: Garry's Mod structure (tabs, category tree, icon grid, tools + context panel)
/// with s&box styling (frosted glass, large radii, springy hover, vector icons). Shown while Q/Tab is held.
/// </summary>
public sealed class SpawnMenu(IconRenderer icons)
{
    private float _openness;
    private string _category = "All";
    private float _scroll;
    private string _search = "";
    private readonly Spring _open = new(0f);

    public bool Open { get; set; }
    public bool Visible => _openness > 0.01f;
    public MenuTab Tab { get; set; } = MenuTab.Spawnlists;
    public bool SearchFocused { get; private set; }

    private static Icon TabIcon(MenuTab t) => t switch
    {
        MenuTab.Spawnlists => Icon.Grid,
        MenuTab.Entities => Icon.Bolt,
        MenuTab.Vehicles => Icon.Car,
        MenuTab.Weapons => Icon.Pistol,
        MenuTab.Tools => Icon.Wrench,
        _ => Icon.Sliders,
    };

    public void Draw(Paper paper, float w, float h, float dt, SandboxGame game)
    {
        _openness = Math.Clamp(_openness + (Open ? 1f : -1f) * dt * Theme.OpenSpeed, 0f, 1f);
        float spring = _open.Step(Open ? 1f : 0f, dt, 260f, 22f);
        if (_openness <= 0.01f) { SearchFocused = false; return; }
        Ui.Alpha = Theme.Ease(_openness);

        float pw = MathF.Min(w * 0.9f, Theme.S(1160)), ph = MathF.Min(h * 0.84f, Theme.S(680));
        float x0 = (w - pw) / 2f, y0 = (h - ph) / 2f + (1f - spring) * Theme.S(30);
        float pad = Theme.S(Theme.Lg);

        Ui.Glass(paper, "sm_panel", x0, y0, pw, ph, strong: true).IsNotInteractable().Scale(0.96f + 0.04f * spring).TransformOrigin(0.5f, 0.5f);

        // ---- Tab strip: pill buttons with icons ----
        float tabH = Theme.S(36), tabY = y0 + pad, tx = x0 + pad;
        foreach (MenuTab tab in Enum.GetValues<MenuTab>())
        {
            string label = tab.ToString();
            float tw = Theme.S(48) + (float)paper.MeasureText(label, Theme.S(Theme.Body), Theme.SemiBold, 0f).X;
            MenuTab captured = tab;
            Ui.Button(paper, $"sm_tab_{tab}", tx, tabY, tw, tabH, label, () => { Tab = captured; _scroll = 0f; _category = "All"; },
                selected: Tab == tab, size: Theme.Body, icon: TabIcon(tab), ghost: Tab != tab, radius: tabH / 2f);
            tx += tw + Theme.S(Theme.Xs);
        }

        float bodyY = tabY + tabH + pad, bodyH = y0 + ph - pad - bodyY;
        float sideW = Theme.S(210);
        float mainX = x0 + pad + sideW + pad, mainW = x0 + pw - pad - mainX;

        SearchFocused = false;
        switch (Tab)
        {
            case MenuTab.Tools:
                DrawToolList(paper, x0 + pad, bodyY, sideW, bodyH, game);
                DrawToolSettings(paper, mainX, bodyY, mainW, bodyH, game);
                break;
            case MenuTab.Utilities:
                DrawUtilities(paper, x0 + pad, bodyY, pw - 2 * pad, bodyH, game);
                break;
            default:
                if (Tab == MenuTab.Spawnlists)
                    DrawSearch(paper, x0 + pw - pad - Theme.S(240), tabY, Theme.S(240), tabH);
                DrawCategoryTree(paper, x0 + pad, bodyY, sideW, bodyH);
                DrawGrid(paper, mainX, bodyY, mainW, bodyH, game);
                break;
        }
        Ui.Alpha = 1f;
    }

    // ---------------------------------------------------------------- grid tabs

    private void DrawSearch(Paper paper, float x, float y, float w, float h)
    {
        Ui.Panel(paper, "sm_search_bg", x, y, w, h, Theme.PanelSunken, h / 2f, shadow: false).IsNotInteractable();
        float isz = Theme.S(16);
        Ui.Icon(paper, "sm_search_ic", x + Theme.S(12), y + (h - isz) / 2f, isz, Icon.Search, Theme.TextFaint);
        float inset = Theme.S(36);
        paper.Box("sm_search").PositionType(PositionType.SelfDirected).Position(x + inset, y).Size(w - inset - Theme.S(14), h)
            .FontSize(Theme.S(Theme.Body))
            .TextField(_search, Theme.Regular, v => { _search = v; _scroll = 0f; },
                Theme.ToVector(Theme.Text), "Search…", Theme.ToVector(Theme.TextFaint));
        SearchFocused = paper.WantsCaptureKeyboard;
    }

    private void DrawCategoryTree(Paper paper, float x, float y, float w, float h)
    {
        Ui.Panel(paper, "sm_tree", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float ix = x + Theme.S(Theme.Sm), iy = y + Theme.S(Theme.Sm);
        Ui.Text(paper, "sm_tree_hdr", ix + Theme.S(6), iy, w, Theme.S(24), Tab == MenuTab.Spawnlists ? "SPAWNLISTS" : Tab.ToString().ToUpperInvariant(),
            Theme.Micro, Theme.TextFaint, Theme.SemiBold);
        iy += Theme.S(28);
        string[] cats = ["All", .. SpawnCatalog.Categories(Tab)];
        foreach (string cat in cats)
        {
            string captured = cat;
            Ui.Button(paper, $"sm_cat_{Tab}_{cat}", ix, iy, w - 2 * Theme.S(Theme.Sm), Theme.S(32), cat,
                () => { _category = captured; _scroll = 0f; }, selected: _category == cat, size: Theme.Body,
                align: TextAlignment.MiddleLeft, ghost: _category != cat, icon: cat == "All" ? Icon.Folder : Icon.Folder);
            iy += Theme.S(36);
        }
    }

    private void DrawGrid(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        SpawnEntry[] shown = SpawnCatalog.Entries
            .Where(e => e.Tab == Tab && (_category == "All" || e.Category == _category))
            .Where(e => _search.Length == 0 || e.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Ui.Text(paper, "sm_grid_title", x + Theme.S(4), y, w, Theme.S(26), _category == "All" ? $"All {Tab}" : _category,
            Theme.Title, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "sm_grid_count", x, y, w - Theme.S(4), Theme.S(26), $"{shown.Length} item{(shown.Length == 1 ? "" : "s")}",
            Theme.Caption, Theme.TextFaint, align: TextAlignment.MiddleRight);
        float gy = y + Theme.S(38), gh = h - Theme.S(38);

        if (shown.Length == 0)
        {
            DrawEmpty(paper, x, gy, w, gh, _search.Length > 0 ? "No matches" : "Nothing here yet",
                _search.Length > 0 ? $"Nothing called \"{_search}\"." : "More content is coming in a future update.");
            return;
        }

        float tile = Theme.S(118), gap = Theme.S(Theme.Md);
        int cols = Math.Max(1, (int)((w + gap) / (tile + gap)));
        int rows = (shown.Length + cols - 1) / cols;
        float contentH = rows * (tile + gap);
        float maxScroll = MathF.Max(0f, contentH - gh);
        _scroll = Math.Clamp(_scroll, 0f, maxScroll);

        SpawnEntry? hovered = null;
        using (paper.Box("sm_grid").PositionType(PositionType.SelfDirected).Position(x, gy).Size(w, gh).Clip()
                   .OnScroll(e => _scroll = Math.Clamp(_scroll - (float)e.Delta * Theme.S(60), 0f, maxScroll))
                   .Enter())
        {
            for (int i = 0; i < shown.Length; i++)
            {
                SpawnEntry entry = shown[i];
                float lx = (i % cols) * (tile + gap), ly = (i / cols) * (tile + gap) - _scroll;
                bool hover = paper.IsPointerOverRect(x + lx, gy + ly, tile, tile) && paper.IsPointerOverRect(x, gy, w, gh);
                if (hover) hovered = entry;
                DrawTile(paper, entry, lx, ly, tile, hover, game);
            }
        }

        if (maxScroll > 0f)
        {
            float barH = gh * gh / contentH, barY = gy + (gh - barH) * (_scroll / maxScroll);
            Ui.Rect(paper, "sm_scrollbar", x + w - Theme.S(3), barY, Theme.S(3), barH, Theme.BorderStrong, Theme.S(2));
        }
        if (hovered is { } hk) DrawTooltip(paper, hk);
    }

    private void DrawTile(Paper paper, SpawnEntry entry, float x, float y, float size, bool hover, SandboxGame game)
    {
        bool press = hover && paper.IsPointerDown(PaperMouseBtn.Left);
        float k = Ui.SpringTo($"tile_{entry.Id}", press ? 0.94f : hover ? 1.05f : 1f, 1f);
        var rt = entry.Kind is { } kind ? icons.Get(kind) : null;
        float a = Ui.Alpha;
        SpawnEntry captured = entry;
        Icon icon = entry.Icon;
        paper.Box($"sm_tile_{entry.Id}").PositionType(PositionType.SelfDirected).Position(x, y).Size(size, size)
            .Rounded(Theme.S(Theme.Radius - 2))
            .BackgroundLinearGradient(0, 0, 0, 1,
                Theme.WithAlpha(hover ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(36, 255, 255, 255), a),
                Theme.WithAlpha(Color.FromArgb(14, 255, 255, 255), a))
            .BorderColor(Theme.WithAlpha(hover ? Theme.Accent : Theme.Border, a)).BorderWidth(hover ? 1.5f : 1f)
            .BoxShadow(0, Theme.S(hover ? 8 : 2), Theme.S(hover ? 20 : 6), 0, Theme.WithAlpha(Theme.Shadow, a * (hover ? 1f : 0.5f)))
            .Scale(k).TransformOrigin(0.5f, 0.5f)
            .OnClick(0, (_, _) => game.Enqueue(() => game.Spawn(captured)))
            .OnPostLayout((handle, rect) => paper.Draw(ref handle, (canvas, r) =>
            {
                float rx = (float)r.Min.X, ry = (float)r.Min.Y, rw = (float)r.Size.X, rh = (float)r.Size.Y;
                float m = rw * 0.1f, ih = rh * 0.68f;
                if (rt?.MainTexture is { } tex)
                {
                    canvas.SetBrushTexture(tex);
                    canvas.SetBrushTextureTransform(
                        Prowl.Vector.Spatial.Transform2D.CreateTranslation(rx + m, ry + m + ih) *
                        Prowl.Vector.Spatial.Transform2D.CreateScale(rw - 2 * m, -ih));
                    canvas.RectFilled(rx + m, ry + m, rw - 2 * m, ih, new Color32(255, 255, 255, (byte)(255 * a)));
                    canvas.ClearBrushTexture();
                }
                else
                {
                    float isz = rw * 0.42f;
                    Icons.Draw(canvas, icon, rx + (rw - isz) / 2f, ry + rh * 0.38f - isz / 2f, isz,
                        new Color32(Theme.Text.R, Theme.Text.G, Theme.Text.B, (byte)(220 * a)));
                }
            }));
        Ui.Text(paper, $"sm_tile_lbl_{entry.Id}", x, y + size * 0.78f, size, size * 0.18f, entry.Name,
            Theme.Caption, hover ? Color.White : Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
    }

    private static void DrawTooltip(Paper paper, SpawnEntry e)
    {
        float px = (float)paper.PointerPos.X + Theme.S(16), py = (float)paper.PointerPos.Y + Theme.S(18);
        float tw = Theme.S(190), th = Theme.S(50);
        Ui.Glass(paper, "sm_tip", px, py, tw, th, Theme.S(Theme.RadiusSm), strong: true).IsNotInteractable().Layer(Layer.Overlay);
        Ui.Text(paper, "sm_tip_name", px + Theme.S(Theme.Md), py + Theme.S(5), tw, Theme.S(22), e.Name, Theme.Body, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "sm_tip_hint", px + Theme.S(Theme.Md), py + Theme.S(25), tw, Theme.S(18), $"{e.Category} · click to spawn",
            Theme.Micro, Theme.TextMuted);
    }

    // ---------------------------------------------------------------- tools

    private static void DrawToolList(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        Ui.Panel(paper, "sm_tools", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float ix = x + Theme.S(Theme.Sm), iy = y + Theme.S(Theme.Sm);
        foreach (IGrouping<string, ToolInfo> group in ToolGun.Tools.GroupBy(t => t.Category))
        {
            Ui.Text(paper, $"sm_tcat_{group.Key}", ix + Theme.S(6), iy, w, Theme.S(24), group.Key.ToUpperInvariant(),
                Theme.Micro, Theme.TextFaint, Theme.SemiBold);
            iy += Theme.S(26);
            foreach (ToolInfo tool in group)
            {
                ToolInfo captured = tool;
                bool active = game.Weapons.IsToolGun && game.ToolGun.Mode == tool.Mode;
                Ui.Button(paper, $"sm_tool_{tool.Mode}", ix, iy, w - 2 * Theme.S(Theme.Sm), Theme.S(32),
                    tool.Name, () => game.SelectTool(captured.Mode), selected: active, ghost: !active,
                    size: Theme.Body, align: TextAlignment.MiddleLeft, icon: Icons.For(tool.Mode));
                iy += Theme.S(36);
            }
            iy += Theme.S(6);
        }
    }

    private static void DrawToolSettings(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        ToolInfo info = ToolGun.InfoFor(game.ToolGun.Mode);
        bool equipped = game.Weapons.IsToolGun;
        Ui.Panel(paper, "sm_cp", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float pad = Theme.S(Theme.Xl), cx = x + pad, cw = w - 2 * pad, cy = y + pad;

        float isz = Theme.S(40);
        Ui.Rect(paper, "sm_cp_icbg", cx, cy, isz + Theme.S(16), isz + Theme.S(16), Theme.AccentSoft, Theme.S(Theme.RadiusSm + 2));
        Ui.Icon(paper, "sm_cp_ic", cx + Theme.S(8), cy + Theme.S(8), isz, Icons.For(info.Mode), Color.White);
        float tx = cx + isz + Theme.S(32);
        Ui.Text(paper, "sm_cp_cat", tx, cy + Theme.S(4), cw, Theme.S(18), info.Category.ToUpperInvariant(), Theme.Micro, Theme.Accent, Theme.SemiBold);
        Ui.Text(paper, "sm_cp_name", tx, cy + Theme.S(20), cw, Theme.S(34), info.Name, Theme.Heading, Theme.Text, Theme.Bold);
        cy += isz + Theme.S(32);
        Ui.Paragraph(paper, "sm_cp_desc", cx, cy, cw, Theme.S(40), info.Description, Theme.Body, Theme.TextMuted);
        cy += Theme.S(32);

        float kx = cx;
        kx += Ui.KeyCap(paper, "sm_cp_lmb", kx, cy, "LMB") + Theme.S(Theme.Sm);
        Ui.Text(paper, "sm_cp_lmbt", kx, cy - Theme.S(1), cw, Theme.S(20), info.Left, Theme.Body, Theme.Text);
        cy += Theme.S(28);
        if (info.Right is { } right)
        {
            kx = cx;
            kx += Ui.KeyCap(paper, "sm_cp_rmb", kx, cy, "RMB") + Theme.S(Theme.Sm);
            Ui.Text(paper, "sm_cp_rmbt", kx, cy - Theme.S(1), cw, Theme.S(20), right, Theme.Body, Theme.Text);
            cy += Theme.S(28);
        }

        cy += Theme.S(Theme.Md);
        Ui.Rect(paper, "sm_cp_sep", cx, cy, cw, 1f, Theme.Border);
        cy += Theme.S(Theme.Lg);
        float sliderW = MathF.Min(cw, Theme.S(420));
        bool any = false;
        foreach (ToolSetting s in game.ToolGun.SettingsFor(info.Mode))
        {
            any = true;
            Ui.Text(paper, $"sm_s_{s.Name}_l", cx, cy, sliderW, Theme.S(20), s.Name, Theme.Body, Theme.Text);
            Ui.Text(paper, $"sm_s_{s.Name}_v", cx, cy, sliderW, Theme.S(20), s.Format(s.Get()), Theme.Body, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
            cy += Theme.S(24);
            Ui.Slider(paper, $"sm_s_{s.Name}", cx, cy, sliderW, s.Get(), s.Min, s.Max, s.Set);
            cy += Theme.S(28);
            Ui.Paragraph(paper, $"sm_s_{s.Name}_h", cx, cy, sliderW, Theme.S(34), s.Help, Theme.Caption, Theme.TextFaint);
            cy += Theme.S(40);
        }
        if (!any) Ui.Text(paper, "sm_cp_none", cx, cy, cw, Theme.S(20), "This tool has no settings.", Theme.Body, Theme.TextFaint);

        float bw = Theme.S(170), bh = Theme.S(38);
        Ui.Button(paper, "sm_cp_equip", cx, y + h - pad - bh, bw, bh, equipped ? "Equipped" : "Equip",
            () => game.SelectTool(info.Mode), selected: equipped, icon: equipped ? Icon.Check : Icon.Wrench, radius: bh / 2f);
    }

    // ---------------------------------------------------------------- utilities / empty

    private static void DrawUtilities(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        Ui.Panel(paper, "sm_util", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float pad = Theme.S(Theme.Xl), cx = x + pad, cy = y + pad;
        Ui.Text(paper, "sm_util_t", cx, cy, w, Theme.S(28), "Map", Theme.Title, Theme.Text, Theme.Bold);
        cy += Theme.S(30);
        Ui.Text(paper, "sm_util_c", cx, cy, w, Theme.S(20), $"{game.Props.Props.Count} props · {game.Props.UndoCount} undo steps",
            Theme.Body, Theme.TextMuted);
        cy += Theme.S(34);
        float bw = Theme.S(190), bh = Theme.S(38), gap = Theme.S(Theme.Md);
        Ui.Button(paper, "sm_util_undo", cx, cy, bw, bh, "Undo", () => game.Enqueue(game.Undo), icon: Icon.Undo, radius: bh / 2f);
        Ui.Button(paper, "sm_util_clear", cx + bw + gap, cy, bw, bh, "Clear props", () => game.Enqueue(game.ClearMap), icon: Icon.Trash, radius: bh / 2f);
        Ui.Button(paper, "sm_util_save", cx + 2 * (bw + gap), cy, bw, bh, "Save map", () => game.Enqueue(game.SaveMap), icon: Icon.Save, radius: bh / 2f);
        Ui.Button(paper, "sm_util_load", cx + 3 * (bw + gap), cy, bw, bh, "Load map", () => game.Enqueue(game.LoadMap), icon: Icon.Load, radius: bh / 2f);
        cy += bh + Theme.S(Theme.Xl);
        Ui.Text(paper, "sm_util_t2", cx, cy, w, Theme.S(28), "Player", Theme.Title, Theme.Text, Theme.Bold);
        cy += Theme.S(34);
        Ui.Text(paper, "sm_util_nc", cx, cy, w, Theme.S(22), "Noclip", Theme.Body, Theme.Text);
        Ui.Toggle(paper, "sm_util_nct", cx + Theme.S(150), cy, game.Player.Noclip, _ => game.Player.ToggleNoclip());
        cy += Theme.S(34);
        Ui.Text(paper, "sm_util_fps", cx, cy, w, Theme.S(22), "FPS counter", Theme.Body, Theme.Text);
        Ui.Toggle(paper, "sm_util_fpst", cx + Theme.S(150), cy, game.Settings.ShowFps, v => { game.Settings.ShowFps = v; game.Settings.Save(); });
    }

    private static void DrawEmpty(Paper paper, float x, float y, float w, float h, string title, string body)
    {
        float isz = Theme.S(40);
        Ui.Icon(paper, $"sm_empty_ic_{title}", x + (w - isz) / 2f, y + h / 2f - Theme.S(74), isz, Icon.Folder, Theme.TextFaint);
        Ui.Text(paper, $"sm_empty_t_{title}", x, y + h / 2f - Theme.S(26), w, Theme.S(28), title, Theme.Title, Theme.TextMuted,
            Theme.SemiBold, TextAlignment.MiddleCenter);
        Ui.Text(paper, $"sm_empty_b_{title}", x, y + h / 2f + Theme.S(4), w, Theme.S(22), body, Theme.Body, Theme.TextFaint,
            align: TextAlignment.MiddleCenter);
    }
}
