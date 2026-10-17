using Prowl.Runtime;
using Prowl.PaperUI;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum MenuTab { Spawnlists, Entities, Vehicles, Weapons, Tools, Dupes, Utilities }

/// <summary>
/// Spawn menu: Garry's Mod structure (tabs, category tree, icon grid, tools + context panel)
/// with s&box styling (frosted glass, large radii, springy hover, vector icons). Shown while Q/Tab is held.
/// </summary>
public sealed class SpawnMenu(IconRenderer icons)
{
    private float _openness;
    private string _category = "All";
    public const string RecentCategory = "Recent";
    public const int RecentMax = 12;
    private static readonly List<string> _recent = [];
    /// <summary>Most-recently spawned entries (this session, newest first), shown as the "Recent" spawnlist.</summary>
    public static IReadOnlyList<string> Recent => _recent;
    public static void PushRecent(string id)
    {
        _recent.Remove(id);
        _recent.Insert(0, id);
        if (_recent.Count > RecentMax) _recent.RemoveRange(RecentMax, _recent.Count - RecentMax);
    }
    private float _scroll;
    private string _search = "";
    private static float _toolScroll;
    private UserSpawnlists? _user;
    public UserSpawnlists User => _user ??= new UserSpawnlists(System.IO.Path.Combine(Worlds.Root, "spawnlists.json"));
    private string _pinTarget = "My Spawnlist";
    private const string UserPrefix = "user:";
    public void ShowUserList(string name) { _category = UserPrefix + name; _pinTarget = name; }
    private readonly Spring _open = new(0f);

    public bool Open { get; set; }
    public bool Visible => _openness > 0.01f;
    public MenuTab Tab { get; set; } = MenuTab.Spawnlists;
    public bool SearchFocused { get; private set; }

    private static Icon TabIcon(MenuTab t) => t switch
    {
        MenuTab.Spawnlists => Icon.Grid,
        MenuTab.Dupes => Icon.Copy,
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

        // GMod-style: nearly full screen with a small margin.
        float margin = Theme.S(12);
        float pw = w - 2 * margin, ph = h - 2 * margin;
        float x0 = margin, y0 = margin + (1f - spring) * Theme.S(24);
        float pad = Theme.S(Theme.Sm + 2);

        Ui.Glass(paper, "sm_panel", x0, y0, pw, ph, strong: true).IsNotInteractable().Scale(0.96f + 0.04f * spring).TransformOrigin(0.5f, 0.5f);

        // ---- Tab strip: pill buttons with icons ----
        float tabH = Theme.S(28), tabY = y0 + pad, tx = x0 + pad;
        foreach (MenuTab tab in Enum.GetValues<MenuTab>())
        {
            string label = tab.ToString();
            float tw = Theme.S(36) + (float)paper.MeasureText(label, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X;
            MenuTab captured = tab;
            Ui.Button(paper, $"sm_tab_{tab}", tx, tabY, tw, tabH, label, () => { Tab = captured; _scroll = 0f; _category = "All"; },
                selected: Tab == tab, size: Theme.Caption, icon: TabIcon(tab), ghost: Tab != tab, radius: tabH / 2f);
            tx += tw + Theme.S(2);
        }

        float bodyY = tabY + tabH + pad, bodyH = y0 + ph - pad - bodyY;
        float sideW = Theme.S(156), gap = Theme.S(Theme.Sm);
        // Right column (GMod's tool panel): tool list + the selected tool's settings, on every content tab.
        bool column = Tab is not (MenuTab.Utilities or MenuTab.Dupes);
        float colW = column ? Theme.S(250) : 0f;
        float colX = x0 + pw - pad - colW;
        float mainX = x0 + pad + sideW + gap, mainW = (column ? colX - gap : x0 + pw - pad) - mainX;
        if (column)
        {
            float listH = MathF.Floor(bodyH * 0.44f);
            DrawToolList(paper, colX, bodyY, colW, listH, game);
            DrawToolSettings(paper, colX, bodyY + listH + gap, colW, bodyH - listH - gap, game);
        }

        SearchFocused = false;
        switch (Tab)
        {
            case MenuTab.Tools:
                DrawToolGrid(paper, x0 + pad, bodyY, colX - gap - x0 - pad, bodyH, game);
                break;
            case MenuTab.Dupes:
                DrawDupes(paper, x0 + pad, bodyY, sideW, mainX, mainW, bodyH, game);
                break;
            case MenuTab.Utilities:
                DrawUtilities(paper, x0 + pad, bodyY, pw - 2 * pad, bodyH, game);
                break;
            default:
                if (Tab == MenuTab.Spawnlists)
                    DrawSearch(paper, x0 + pw - pad - Theme.S(220), tabY, Theme.S(220), tabH);
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
        float lh = Theme.S(Theme.Body) * 1.3f; // TextField draws from the top; size it to one line and centre it
        paper.Box("sm_search").PositionType(PositionType.SelfDirected).Position(x + inset, y + (h - lh) / 2f).Size(w - inset - Theme.S(14), lh)
            .FontSize(Theme.S(Theme.Body))
            .TextField(_search, Theme.Regular, v => { _search = v; _scroll = 0f; },
                Theme.ToVector(Theme.Text), "Search…", Theme.ToVector(Theme.TextFaint));
        SearchFocused = paper.WantsCaptureKeyboard;
    }

    private void DrawCategoryTree(Paper paper, float x, float y, float w, float h)
    {
        Ui.Panel(paper, "sm_tree", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float ix = x + Theme.S(6), iy = y + Theme.S(6);
        Ui.Text(paper, "sm_tree_hdr", ix + Theme.S(6), iy, w, Theme.S(20), Tab == MenuTab.Spawnlists ? "SPAWNLISTS" : Tab.ToString().ToUpperInvariant(),
            Theme.Micro, Theme.TextFaint, Theme.SemiBold);
        iy += Theme.S(22);
        string[] cats = Tab == MenuTab.Spawnlists && _recent.Count > 0
            ? ["All", RecentCategory, .. SpawnCatalog.Categories(Tab)]
            : ["All", .. SpawnCatalog.Categories(Tab)];
        foreach (string cat in cats)
        {
            string captured = cat;
            Ui.Button(paper, $"sm_cat_{Tab}_{cat}", ix, iy, w - 2 * Theme.S(6), Theme.S(26), cat,
                () => { _category = captured; _scroll = 0f; }, selected: _category == cat, size: Theme.Caption,
                align: TextAlignment.MiddleLeft, ghost: _category != cat, icon: cat == RecentCategory ? Icon.Undo : Icon.Folder);
            iy += Theme.S(28);
        }
        if (Tab != MenuTab.Spawnlists) return;
        iy += Theme.S(8);
        Ui.Text(paper, "sm_tree_user", ix + Theme.S(6), iy, w, Theme.S(20), "MY SPAWNLISTS", Theme.Micro, Theme.TextFaint, Theme.SemiBold);
        iy += Theme.S(22);
        foreach (string name in User.Lists.Keys)
        {
            string key = UserPrefix + name, captured = name;
            Ui.Button(paper, $"sm_ucat_{name}", ix, iy, w - 2 * Theme.S(6), Theme.S(26), name,
                () => { _category = UserPrefix + captured; _pinTarget = captured; _scroll = 0f; }, selected: _category == key, size: Theme.Caption,
                align: TextAlignment.MiddleLeft, ghost: _category != key, icon: Icon.Pencil);
            iy += Theme.S(28);
        }
        Ui.Button(paper, "sm_ucat_new", ix, iy, w - 2 * Theme.S(6), Theme.S(26), "New spawnlist",
            () => { string n = User.Create(); _category = UserPrefix + n; _pinTarget = n; }, size: Theme.Caption,
            align: TextAlignment.MiddleLeft, ghost: true, icon: Icon.Plus);
    }

    private void DrawGrid(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        bool userList = _category.StartsWith(UserPrefix);
        List<string>? ids = userList && User.Lists.TryGetValue(_category[UserPrefix.Length..], out var l) ? l
            : _category == RecentCategory && Tab == MenuTab.Spawnlists ? [.. _recent] : null;
        SpawnEntry[] shown = (ids is not null
                ? ids.Select(id => SpawnCatalog.Entries.FirstOrDefault(e => e.Id == id)).OfType<SpawnEntry>()
                : SpawnCatalog.Entries.Where(e => e.Tab == Tab && (_category == "All" || e.Category == _category)))
            .Where(e => _search.Length == 0 || e.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Ui.Text(paper, "sm_grid_title", x + Theme.S(2), y, w, Theme.S(22), _category == "All" ? $"All {Tab}" : userList ? _category[UserPrefix.Length..] : _category,
            Theme.Label, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "sm_grid_count", x, y, w - Theme.S(4), Theme.S(22), $"{shown.Length} item{(shown.Length == 1 ? "" : "s")}",
            Theme.Caption, Theme.TextFaint, align: TextAlignment.MiddleRight);
        float gy = y + Theme.S(28), gh = h - Theme.S(28);

        if (shown.Length == 0)
        {
            DrawEmpty(paper, x, gy, w, gh, _search.Length > 0 ? "No matches" : userList ? "Empty spawnlist" : "Nothing here yet",
                _search.Length > 0 ? $"Nothing called \"{_search}\"." : userList ? "Hover any item and press + to add it here." : "More content is coming in a future update.");
            return;
        }

        float tile = Theme.S(84), gap = Theme.S(6);
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
                if (hover && entry.Tab != MenuTab.Weapons)
                {
                    float bs = Theme.S(20);
                    SpawnEntry ce = entry;
                    string list = userList ? _category[UserPrefix.Length..] : _pinTarget;
                    Ui.Button(paper, $"sm_pin_{entry.Id}", lx + tile - bs - Theme.S(4), ly + Theme.S(4), bs, bs, "",
                        () => { if (userList) User.Remove(list, ce.Id); else { User.Add(list, ce.Id); game.Notify($"Added {ce.Name} to {list}"); } },
                        icon: userList ? Icon.Trash : Icon.Plus, radius: bs / 2f, size: Theme.Caption);
                }
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
        var rt = entry.Kind is { } kind ? icons.Get(kind) : icons.Get(entry.Id);
        float a = Ui.Alpha;
        SpawnEntry captured = entry;
        Icon icon = entry.Icon;
        paper.Box($"sm_tile_{entry.Id}").PositionType(PositionType.SelfDirected).Position(x, y).Size(size, size)
            .Rounded(Theme.S(Theme.RadiusSm))
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
        Ui.Text(paper, $"sm_tile_lbl_{entry.Id}", x + Theme.S(2), y + size * 0.79f, size - Theme.S(4), size * 0.17f, entry.Name,
            Theme.Micro, hover ? Color.White : Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
    }

    private static void DrawTooltip(Paper paper, SpawnEntry e)
    {
        float px = (float)paper.PointerPos.X + Theme.S(16), py = (float)paper.PointerPos.Y + Theme.S(18);
        float tw = Theme.S(190), th = Theme.S(50);
        Ui.Glass(paper, "sm_tip", px, py, tw, th, Theme.S(Theme.RadiusSm), strong: true).IsNotInteractable().Layer(Layer.Overlay);
        Ui.Text(paper, "sm_tip_name", px + Theme.S(Theme.Md), py + Theme.S(5), tw, Theme.S(22), e.Name, Theme.Body, Theme.Text, Theme.SemiBold, overlay: true);
        Ui.Text(paper, "sm_tip_hint", px + Theme.S(Theme.Md), py + Theme.S(25), tw, Theme.S(18), e.Tab == MenuTab.Weapons ? $"{e.Category} · click to equip" : $"{e.Category} · click to spawn",
            Theme.Micro, Theme.TextMuted, overlay: true);
    }

    // ---------------------------------------------------------------- tools

    private static void DrawToolList(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        Ui.Panel(paper, "sm_tools", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        var groups = ToolGun.AllTools.GroupBy(t => t.Category).ToArray();
        float contentH = Theme.S(6) * 2 + groups.Sum(gr => Theme.S(20) + gr.Count() * Theme.S(26) + Theme.S(4));
        float maxScroll = MathF.Max(0f, contentH - h);
        _toolScroll = Math.Clamp(_toolScroll, 0f, maxScroll);
        using var _box = paper.Box("sm_tools_clip").PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).Clip()
            .OnScroll(e => _toolScroll = Math.Clamp(_toolScroll - (float)e.Delta * Theme.S(60), 0f, maxScroll)).Enter();
        float ix = Theme.S(6), iy = Theme.S(6) - _toolScroll;
        if (maxScroll > 0f)
        {
            float barH = h * h / contentH, barY = (h - barH) * (_toolScroll / maxScroll);
            Ui.Rect(paper, "sm_tools_bar", w - Theme.S(5), barY + Theme.S(4), Theme.S(3), barH - Theme.S(8), Theme.BorderStrong, Theme.S(2));
        }
        foreach (IGrouping<string, ToolInfo> group in groups)
        {
            Ui.Text(paper, $"sm_tcat_{group.Key}", ix + Theme.S(6), iy, w, Theme.S(20), group.Key.ToUpperInvariant(),
                Theme.Micro, Theme.TextFaint, Theme.SemiBold);
            iy += Theme.S(20);
            foreach (ToolInfo tool in group)
            {
                ToolInfo captured = tool;
                bool active = game.Weapons.IsToolGun && game.ToolGun.Mode == tool.Mode;
                Ui.Button(paper, $"sm_tool_{tool.Mode}", ix, iy, w - 2 * Theme.S(6) - Theme.S(6), Theme.S(24),
                    tool.Name, () => game.SelectTool(captured.Mode), selected: active, ghost: !active,
                    size: Theme.Caption, align: TextAlignment.MiddleLeft, icon: Icons.For(tool.Mode));
                iy += Theme.S(26);
            }
            iy += Theme.S(4);
        }
    }

    private static void DrawToolSettings(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        ToolInfo info = ToolGun.InfoFor(game.ToolGun.Mode);
        bool equipped = game.Weapons.IsToolGun;
        Ui.Panel(paper, "sm_cp", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        float pad = Theme.S(Theme.Md), cx = x + pad, cw = w - 2 * pad, cy = y + pad;

        float isz = Theme.S(22);
        Ui.Rect(paper, "sm_cp_icbg", cx, cy, isz + Theme.S(10), isz + Theme.S(10), Theme.AccentSoft, Theme.S(Theme.RadiusXs + 1));
        Ui.Icon(paper, "sm_cp_ic", cx + Theme.S(5), cy + Theme.S(5), isz, Icons.For(info.Mode), Color.White);
        float tx = cx + isz + Theme.S(18);
        Ui.Text(paper, "sm_cp_cat", tx, cy - Theme.S(1), cw, Theme.S(14), info.Category.ToUpperInvariant(), Theme.Micro, Theme.Accent, Theme.SemiBold);
        Ui.Text(paper, "sm_cp_name", tx, cy + Theme.S(12), cw, Theme.S(22), info.Name, Theme.Label, Theme.Text, Theme.Bold);
        cy += isz + Theme.S(18);
        Ui.Paragraph(paper, "sm_cp_desc", cx, cy, cw, Theme.S(32), info.Description, Theme.Caption, Theme.TextMuted);
        cy += Theme.S(34);

        float kx = cx;
        kx += Ui.KeyCap(paper, "sm_cp_lmb", kx, cy, "LMB") + Theme.S(6);
        Ui.Text(paper, "sm_cp_lmbt", kx, cy - Theme.S(1), cw - (kx - cx), Theme.S(20), info.Left, Theme.Caption, Theme.Text);
        cy += Theme.S(24);
        if (info.Right is { } right)
        {
            kx = cx;
            kx += Ui.KeyCap(paper, "sm_cp_rmb", kx, cy, "RMB") + Theme.S(6);
            Ui.Text(paper, "sm_cp_rmbt", kx, cy - Theme.S(1), cw - (kx - cx), Theme.S(20), right, Theme.Caption, Theme.Text);
            cy += Theme.S(24);
        }
        cy += Theme.S(4);
        Ui.Rect(paper, "sm_cp_sep", cx, cy, cw, 1f, Theme.Border);
        cy += Theme.S(Theme.Sm);
        bool any = false;
        float bh = Theme.S(28), bottom = y + h - pad - bh - Theme.S(6);
        foreach (ToolSetting s in game.ToolGun.SettingsFor(info.Mode))
        {
            if (cy + Theme.S(40) > bottom) break;
            any = true;
            if (s.IsKey)
            {
                Ui.Text(paper, $"sm_s_{s.Name}_l", cx, cy + Theme.S(3), cw, Theme.S(18), s.Name, Theme.Caption, Theme.Text);
                ToolSetting ks = s;
                Ui.KeyPicker(paper, $"sm_s_{s.Name}_k", cx + cw - Theme.S(96), cy, Theme.S(96), Theme.S(24), (KeyCode)(int)s.Get(), k => ks.Set((int)k));
                cy += Theme.S(32);
                continue;
            }
            Ui.Text(paper, $"sm_s_{s.Name}_l", cx, cy, cw, Theme.S(18), s.Name, Theme.Caption, Theme.Text);
            Ui.Text(paper, $"sm_s_{s.Name}_v", cx, cy, cw, Theme.S(18), s.Format(s.Get()), Theme.Caption, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
            cy += Theme.S(19);
            Ui.Slider(paper, $"sm_s_{s.Name}", cx, cy, cw, s.Get(), s.Min, s.Max, s.Set);
            cy += Theme.S(26);
        }
        if (!any) Ui.Text(paper, "sm_cp_none", cx, cy, cw, Theme.S(18), "No settings.", Theme.Caption, Theme.TextFaint);

        Ui.Button(paper, "sm_cp_equip", cx, y + h - pad - bh, cw, bh, equipped ? "Equipped" : "Equip tool gun",
            () => game.SelectTool(info.Mode), selected: equipped, icon: equipped ? Icon.Check : Icon.Wrench, radius: bh / 2f, size: Theme.Caption);
    }

    /// <summary>Tools tab body: every tool as an icon tile, grouped by category (settings live in the right column).</summary>
    private static void DrawToolGrid(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        float tile = Theme.S(84), gap = Theme.S(6);
        int cols = Math.Max(1, (int)((w + gap) / (tile + gap)));
        float cy = y;
        foreach (var group in ToolGun.AllTools.GroupBy(t => t.Category))
        {
            Ui.Text(paper, $"sm_tg_h_{group.Key}", x + Theme.S(2), cy, w, Theme.S(22), group.Key, Theme.Label, Theme.Text, Theme.SemiBold);
            cy += Theme.S(26);
            int i = 0;
            foreach (ToolInfo t in group)
            {
                float tx = x + (i % cols) * (tile + gap), ty = cy + (i / cols) * (tile + gap);
                bool active = game.Weapons.IsToolGun && game.ToolGun.Mode == t.Mode;
                bool hover = paper.IsPointerOverRect(tx, ty, tile, tile);
                ToolInfo ct = t;
                paper.Box($"sm_tg_{t.Mode}").PositionType(PositionType.SelfDirected).Position(tx, ty).Size(tile, tile)
                    .Rounded(Theme.S(Theme.RadiusSm))
                    .BackgroundColor(Theme.WithAlpha(active ? Theme.AccentSoft : hover ? Theme.Hover : Theme.PanelRaised, Ui.Alpha))
                    .BorderColor(Theme.WithAlpha(active || hover ? Theme.Accent : Theme.Border, Ui.Alpha)).BorderWidth(active ? 1.5f : 1f)
                    .OnClick(0, (_, _) => game.SelectTool(ct.Mode));
                float isz = tile * 0.36f;
                Ui.Icon(paper, $"sm_tg_ic_{t.Mode}", tx + (tile - isz) / 2f, ty + tile * 0.22f, isz, Icons.For(t.Mode), active ? Color.White : Theme.Text);
                Ui.Text(paper, $"sm_tg_l_{t.Mode}", tx + Theme.S(2), ty + tile * 0.72f, tile - Theme.S(4), tile * 0.2f, t.Name, Theme.Micro, active || hover ? Color.White : Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
                i++;
            }
            cy += ((i + cols - 1) / cols) * (tile + gap) + Theme.S(8);
        }
    }

    // ---------------------------------------------------------------- utilities / empty

    private static void DrawUtilities(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        Ui.Panel(paper, "sm_util", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float pad = Theme.S(Theme.Xl + Theme.Md), cx = x + pad, cy = y + pad;
        // Content is capped to a readable width; each group sits on its own raised card.
        w = MathF.Min(w, Theme.S(980));
        float cardPad = Theme.S(Theme.Md);
        Ui.Panel(paper, "sm_util_card0", cx - cardPad, cy - cardPad, w - 2 * pad + 2 * cardPad, Theme.S(118) + cardPad, Theme.PanelRaised, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        float colW0 = (w - 2 * pad - Theme.S(32)) / 2f, cardTop = cy + Theme.S(118) + Theme.S(Theme.Xl) + cardPad;
        Ui.Panel(paper, "sm_util_card1", cx - cardPad, cardTop - cardPad, colW0 + 2 * cardPad, Theme.S(214), Theme.PanelRaised, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        Ui.Panel(paper, "sm_util_card2", cx + colW0 + Theme.S(32) - cardPad, cardTop - cardPad, colW0 + 2 * cardPad, Theme.S(214), Theme.PanelRaised, Theme.S(Theme.RadiusSm), shadow: false).IsNotInteractable();
        Section(paper, "sm_util_t", cx, cy, w - 2 * pad, "MAP");
        cy += Theme.S(28);
        Ui.Text(paper, "sm_util_c", cx, cy, w, Theme.S(20), $"{game.Props.Props.Count} props · {game.Props.UndoCount} undo steps",
            Theme.Body, Theme.TextMuted);
        cy += Theme.S(34);
        float bw = Theme.S(190), bh = Theme.S(38), gap = Theme.S(Theme.Md);
        Ui.Button(paper, "sm_util_undo", cx, cy, bw, bh, "Undo", () => game.Enqueue(game.Undo), icon: Icon.Undo, radius: bh / 2f);
        Ui.Button(paper, "sm_util_clear", cx + bw + gap, cy, bw, bh, "Clear props", () => game.Enqueue(() => game.ClearMap()), icon: Icon.Trash, radius: bh / 2f);
        Ui.Button(paper, "sm_util_save", cx + 2 * (bw + gap), cy, bw, bh, game.Session is null ? "Save map" : "Save world",
            () => game.Enqueue(() => { if (game.Session is null) game.SaveMap(); else { game.SaveSession(true, true); game.Notify("World saved"); } }), icon: Icon.Save, radius: bh / 2f);
        cy += bh + Theme.S(Theme.Xl) + cardPad * 2f;
        cy = cardTop;

        // World: sky presets, day/night, gravity and time scale.
        float colW = (w - 2 * pad - Theme.S(32)) / 2f, rx = cx + colW + Theme.S(32), ly = cy;
        Section(paper, "sm_util_tw", cx, cy, colW, "WORLD");
        cy += Theme.S(32);
        Ui.Text(paper, "sm_util_sky", cx, cy, w, Theme.S(18), "Sky", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        cy += Theme.S(22);
        int cur = Array.FindIndex(DayNightCycle.Presets, p => MathF.Abs(p.Time - game.DayNight.TimeOfDay) < 0.01f);
        Ui.Segmented(paper, "sm_util_skyseg", cx, cy, colW, DayNightCycle.Presets.Select(p => p.Name).ToArray(), cur,
            v => game.Enqueue(() => { game.DayNight.TimeOfDay = DayNightCycle.Presets[v].Time; game.DayNight.Apply(game); }));
        cy += Theme.S(48);
        float lw = Theme.S(150), sw = colW - lw - Theme.S(60);
        Ui.Text(paper, "sm_util_dn", cx, cy, w, Theme.S(22), "Day/night cycle", Theme.Body, Theme.Text);
        Ui.Toggle(paper, "sm_util_dnt", cx + lw, cy, game.DayNight.Enabled, v => game.DayNight.Enabled = v);
        cy += Theme.S(40);
        float grav = -game.World.Physics.Gravity.Y;
        Ui.Text(paper, "sm_util_g", cx, cy, w, Theme.S(22), "Gravity", Theme.Body, Theme.Text);
        Ui.Slider(paper, "sm_util_gs", cx + lw, cy - Theme.S(1), sw, grav, 0f, 30f, v => game.SetGravity(MathF.Round(v * 10f) / 10f));
        Ui.Text(paper, "sm_util_gv", cx + lw + sw + Theme.S(8), cy, Theme.S(60), Theme.S(22), $"{grav:0.0}", Theme.Body, Theme.Accent, Theme.SemiBold);
        cy += Theme.S(36);
        Ui.Text(paper, "sm_util_ts", cx, cy, w, Theme.S(22), "Time scale", Theme.Body, Theme.Text);
        Ui.Slider(paper, "sm_util_tss", cx + lw, cy - Theme.S(1), sw, game.Settings.TimeScale, 0.1f, 2f, v => game.Settings.TimeScale = MathF.Round(v * 20f) / 20f);
        Ui.Text(paper, "sm_util_tsv", cx + lw + sw + Theme.S(8), cy, Theme.S(60), Theme.S(22), $"{game.Settings.TimeScale:0.00}×", Theme.Body, Theme.Accent, Theme.SemiBold);
        cy += Theme.S(36);
        Ui.Text(paper, "sm_util_wx", cx, cy, w, Theme.S(18), "Weather", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
        cy += Theme.S(22);
        Ui.Segmented(paper, "sm_util_wxseg", cx, cy, colW, WeatherSystem.Names, (int)game.Weather.Kind,
            v => game.Enqueue(() => game.Weather.Set((WeatherKind)v, game)));

        // Player column.
        cy = ly;
        Section(paper, "sm_util_t2", rx, cy, colW, "PLAYER");
        cy += Theme.S(32);
        Ui.Text(paper, "sm_util_nc", rx, cy, w, Theme.S(22), "Noclip", Theme.Body, Theme.Text);
        Ui.Toggle(paper, "sm_util_nct", rx + lw, cy, game.Player.Noclip, _ => game.Player.ToggleNoclip());
        cy += Theme.S(36);
        Ui.Text(paper, "sm_util_fd", rx, cy, w, Theme.S(22), "Fall damage", Theme.Body, Theme.Text);
        Ui.Toggle(paper, "sm_util_fdt", rx + lw, cy, game.Health.FallDamage, v => { game.Health.FallDamage = v; if (game.Meta is { } m) m.Modifiers.FallDamage = v; });
        cy += Theme.S(36);
        Ui.Text(paper, "sm_util_fps", rx, cy, w, Theme.S(22), "FPS counter", Theme.Body, Theme.Text);
        Ui.Toggle(paper, "sm_util_fpst", rx + lw, cy, game.Settings.ShowFps, v => { game.Settings.ShowFps = v; game.Settings.Save(); });
        cy += Theme.S(36);
        Ui.Button(paper, "sm_util_heal", rx, cy, Theme.S(150), bh, "Heal", () => game.Enqueue(() => game.Health.Set(100f, game.Health.Armor)), icon: Icon.Heart, radius: bh / 2f);
    }

    // ---------------------------------------------------------------- dupes

    private string _dupeName = "";
    private List<(string Path, Dupes.DupeFile File)>? _dupes;
    private float _dupesAge = 99f;

    private void DrawDupes(Paper paper, float x, float y, float sideW, float mainX, float mainW, float h, SandboxGame game)
    {
        _dupesAge += 1f / 60f;
        if (_dupes is null || _dupesAge > 2f) { _dupes = Dupes.List(); _dupesAge = 0f; }
        ToolGun t = game.ToolGun;

        // Left: clipboard card.
        Ui.Panel(paper, "sm_dc", x, y, sideW, h, Theme.PanelSunken, Theme.S(Theme.RadiusSm + 2), shadow: false).IsNotInteractable();
        float ix = x + Theme.S(Theme.Md), iy = y + Theme.S(Theme.Md), iw = sideW - 2 * Theme.S(Theme.Md);
        Ui.Text(paper, "sm_dc_h", ix, iy, iw, Theme.S(18), "CLIPBOARD", Theme.Micro, Theme.TextFaint, Theme.SemiBold);
        iy += Theme.S(24);
        Ui.Icon(paper, "sm_dc_ic", ix, iy + Theme.S(2), Theme.S(18), Icon.Copy, t.HasCopy ? Theme.Accent : Theme.TextFaint);
        Ui.Text(paper, "sm_dc_n", ix + Theme.S(26), iy, iw - Theme.S(26), Theme.S(22), t.HasCopy ? t.DupeName : "Nothing copied", Theme.Body, Theme.Text, Theme.SemiBold);
        iy += Theme.S(22);
        Ui.Text(paper, "sm_dc_c", ix + Theme.S(26), iy, iw, Theme.S(18), t.HasCopy ? $"{t.Dupe!.Props.Count} props" : "Duplicator: right click a contraption", Theme.Caption, Theme.TextMuted);
        iy += Theme.S(34);
        float bh = Theme.S(34);
        Menus.TextInput(paper, "sm_dc_name", ix, iy, iw, bh, _dupeName, v => _dupeName = v, "Name this dupe…");
        iy += bh + Theme.S(Theme.Sm);
        Ui.Button(paper, "sm_dc_save", ix, iy, iw, bh, "Save dupe", () =>
        {
            if (t.Dupe is not { } d) { game.Notify("Copy something with the Duplicator first"); return; }
            string n = _dupeName.Trim().Length > 0 ? _dupeName.Trim() : t.DupeName;
            try { Dupes.Save(n, d); _dupeName = ""; _dupesAge = 99f; game.Notify($"Saved dupe \"{n}\""); }
            catch (Exception e) { game.Notify($"Couldn't save the dupe: {e.Message}", ToastKind.Danger); }
        }, enabled: t.HasCopy, icon: Icon.Save, radius: bh / 2f);
        iy += bh + Theme.S(Theme.Sm);
        Ui.Button(paper, "sm_dc_tool", ix, iy, iw, bh, "Equip Duplicator", () => game.SelectTool(ToolMode.Duplicator), ghost: true, icon: Icon.Wrench, radius: bh / 2f);

        // Right: saved dupes.
        Ui.Text(paper, "sm_dl_t", mainX + Theme.S(4), y, mainW, Theme.S(26), "Saved Dupes", Theme.Title, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "sm_dl_c", mainX, y, mainW - Theme.S(4), Theme.S(26), $"{_dupes.Count} saved", Theme.Caption, Theme.TextFaint, align: TextAlignment.MiddleRight);
        float gy = y + Theme.S(38), gh = h - Theme.S(38);
        if (_dupes.Count == 0)
        {
            DrawEmpty(paper, mainX, gy, mainW, gh, "No dupes yet", "Copy a contraption with the Duplicator, then save it here.");
            return;
        }
        float cardW = Theme.S(250), cardH = Theme.S(92), gap = Theme.S(Theme.Md);
        int cols = Math.Max(1, (int)((mainW + gap) / (cardW + gap)));
        cardW = (mainW - gap * (cols - 1)) / cols;
        for (int i = 0; i < _dupes.Count; i++)
        {
            var (path, f) = _dupes[i];
            float cx = mainX + (i % cols) * (cardW + gap), cy = gy + (i / cols) * (cardH + gap);
            if (cy + cardH > y + h) break;
            Ui.Panel(paper, $"sm_dd_{i}", cx, cy, cardW, cardH, Theme.WithAlpha(Color.FromArgb(30, 255, 255, 255), 1f), Theme.S(Theme.Radius - 2), shadow: false).IsNotInteractable();
            Ui.Icon(paper, $"sm_dd_ic_{i}", cx + Theme.S(14), cy + Theme.S(16), Theme.S(20), Icon.Cube, Theme.Accent);
            Ui.Text(paper, $"sm_dd_n_{i}", cx + Theme.S(44), cy + Theme.S(10), cardW - Theme.S(54), Theme.S(22), f.Name, Theme.Body, Theme.Text, Theme.SemiBold);
            Ui.Text(paper, $"sm_dd_m_{i}", cx + Theme.S(44), cy + Theme.S(30), cardW - Theme.S(54), Theme.S(18), $"{f.Props} props · {f.Created:MMM d, HH:mm}", Theme.Caption, Theme.TextMuted);
            float by = cy + cardH - Theme.S(36), bw = Theme.S(96);
            var cf = f; string cp = path;
            Ui.Button(paper, $"sm_dd_l_{i}", cx + Theme.S(14), by, bw, Theme.S(26), "Load", () =>
            {
                t.Dupe = cf.Data; t.DupeName = cf.Name; game.SelectTool(ToolMode.Duplicator); game.Notify($"\"{cf.Name}\" on the Duplicator · left click to paste");
            }, icon: Icon.Load, radius: Theme.S(13), size: Theme.Caption);
            Ui.Button(paper, $"sm_dd_d_{i}", cx + Theme.S(14) + bw + Theme.S(6), by, Theme.S(32), Theme.S(26), "", () =>
            {
                try { File.Delete(cp); } catch { }
                _dupesAge = 99f;
            }, ghost: true, icon: Icon.Trash, radius: Theme.S(13), size: Theme.Caption);
        }
    }

    /// <summary>s&box-style section header: small caps label with a hairline rule.</summary>
    private static void Section(Paper paper, string id, float x, float y, float w, string label)
    {
        Ui.Text(paper, id, x, y, w, Theme.S(18), label, Theme.Micro, Theme.TextFaint, Theme.Bold);
        float lw = (float)paper.MeasureText(label, Theme.S(Theme.Micro), Theme.Bold, 0f).X + Theme.S(10);
        Ui.Rect(paper, id + "_rule", x + lw, y + Theme.S(9), w - lw, 1f, Theme.Border, 0f);
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
