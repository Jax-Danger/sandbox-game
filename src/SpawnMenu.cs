using Prowl.PaperUI;
using Prowl.PaperUI.LayoutEngine;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum MenuTab { Spawnlists, Entities, Tools, Utilities }

/// <summary>
/// Spawn menu modelled on Garry's Mod: a large centred translucent panel, a tab strip on top,
/// a category tree on the left, and a scrollable grid of prop thumbnails on the right. The Tools tab
/// lists toolgun modes with a settings ("context") panel; Utilities holds map actions.
/// Shown while Q or Tab is held (or while its search box has focus).
/// </summary>
public sealed class SpawnMenu(IconRenderer icons)
{
    private static readonly (string Name, PropKind[] Kinds)[] Categories =
    [
        ("All Props", PropManager.AllKinds),
        ("Construction", [PropKind.Plank, PropKind.Ramp, PropKind.BigCrate]),
        ("Shapes", [PropKind.Cube, PropKind.Sphere, PropKind.Barrel]),
        ("Containers", [PropKind.Barrel, PropKind.BigCrate]),
    ];

    private float _openness;
    private int _category;
    private float _scroll;
    private string _search = "";

    public bool Open { get; set; }
    public bool Visible => _openness > 0.01f;
    public MenuTab Tab { get; set; } = MenuTab.Spawnlists;
    public bool SearchFocused { get; private set; }

    public void Draw(Paper paper, float w, float h, float dt, SandboxGame game)
    {
        _openness = Math.Clamp(_openness + (Open ? 1f : -1f) * dt * Theme.OpenSpeed, 0f, 1f);
        if (_openness <= 0.01f) { SearchFocused = false; return; }
        float t = Theme.Ease(_openness);
        Ui.Alpha = t;

        float pw = MathF.Min(w * 0.9f, Theme.S(1180)), ph = MathF.Min(h * 0.82f, Theme.S(680));
        float x0 = (w - pw) / 2f, y0 = (h - ph) / 2f + (1f - t) * Theme.S(24);
        float pad = Theme.S(Theme.Lg);

        Ui.Panel(paper, "sm_panel", x0, y0, pw, ph, blur: true).IsNotInteractable();

        // ---- Tab strip ----
        float tabH = Theme.S(38), tabY = y0 + pad;
        float tx = x0 + pad;
        foreach (MenuTab tab in Enum.GetValues<MenuTab>())
        {
            string label = tab.ToString();
            float tw = Theme.S(28 + 9f * label.Length);
            MenuTab captured = tab;
            Ui.Button(paper, $"sm_tab_{tab}", tx, tabY, tw, tabH, label, () => { Tab = captured; _scroll = 0f; },
                selected: Tab == tab, size: Theme.Label);
            tx += tw + Theme.S(Theme.Sm);
        }
        Ui.Rect(paper, "sm_tabline", x0 + pad, tabY + tabH + Theme.S(Theme.Sm), pw - 2 * pad, 1f, Theme.Border);

        float bodyY = tabY + tabH + Theme.S(Theme.Lg + 2);
        float bodyH = y0 + ph - pad - bodyY;
        float sideW = Theme.S(220);
        float mainX = x0 + pad + sideW + pad, mainW = x0 + pw - pad - mainX;

        SearchFocused = false;
        switch (Tab)
        {
            case MenuTab.Spawnlists:
                DrawSearch(paper, x0 + pw - pad - Theme.S(260), tabY + Theme.S(3), Theme.S(260), tabH - Theme.S(6));
                DrawCategoryTree(paper, x0 + pad, bodyY, sideW, bodyH);
                DrawPropGrid(paper, mainX, bodyY, mainW, bodyH, game);
                break;
            case MenuTab.Tools:
                DrawToolList(paper, x0 + pad, bodyY, sideW, bodyH, game);
                DrawToolSettings(paper, mainX, bodyY, mainW, bodyH, game);
                break;
            case MenuTab.Utilities:
                DrawUtilities(paper, x0 + pad, bodyY, pw - 2 * pad, bodyH, game);
                break;
            case MenuTab.Entities:
                DrawEmpty(paper, x0 + pad, bodyY, pw - 2 * pad, bodyH, "No entities yet",
                    "Entities (lights, thrusters, NPCs) will live here in a future update. Use the Spawnlists tab for props.");
                break;
        }
        Ui.Alpha = 1f;
    }

    // ---------------------------------------------------------------- Spawnlists

    private void DrawSearch(Paper paper, float x, float y, float w, float h)
    {
        Ui.Panel(paper, "sm_search_bg", x, y, w, h, Theme.PanelSunken, h / 2f, shadow: false).IsNotInteractable();
        float inset = Theme.S(Theme.Lg);
        paper.Box("sm_search").PositionType(PositionType.SelfDirected).Position(x + inset, y).Size(w - 2 * inset, h)
            .FontSize(Theme.S(Theme.Body))
            .TextField(_search, Theme.Regular, v => { _search = v; _scroll = 0f; },
                Theme.ToVector(Theme.Text), "Search props…", Theme.ToVector(Theme.TextFaint));
        SearchFocused = paper.WantsCaptureKeyboard;
    }

    private void DrawCategoryTree(Paper paper, float x, float y, float w, float h)
    {
        Ui.Panel(paper, "sm_tree", x, y, w, h, Theme.PanelSunken, shadow: false).IsNotInteractable();
        float ix = x + Theme.S(Theme.Sm), iy = y + Theme.S(Theme.Sm);
        Ui.Text(paper, "sm_tree_hdr", ix + Theme.S(4), iy, w, Theme.S(24), "YOUR SPAWNLISTS", Theme.Caption, Theme.TextFaint, Theme.SemiBold);
        iy += Theme.S(28);
        for (int i = 0; i < Categories.Length; i++)
        {
            int captured = i;
            bool child = i > 0;
            float indent = child ? Theme.S(14) : 0f;
            string glyph = child ? "▸  " : "▾  ";
            Ui.Button(paper, $"sm_cat_{i}", ix + indent, iy, w - 2 * Theme.S(Theme.Sm) - indent, Theme.S(30),
                glyph + Categories[i].Name, () => { _category = captured; _scroll = 0f; },
                selected: _category == i, size: Theme.Body, align: TextAlignment.MiddleLeft,
                baseColor: Color.FromArgb(0, 0, 0, 0));
            iy += Theme.S(34);
        }
    }

    private void DrawPropGrid(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        (string catName, PropKind[] kinds) = Categories[_category];
        PropKind[] shown = kinds
            .Where(k => _search.Length == 0 || PropManager.DisplayName(k).Contains(_search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Ui.Text(paper, "sm_grid_title", x, y, w, Theme.S(26), catName, Theme.Title, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "sm_grid_count", x, y, w, Theme.S(26), $"{shown.Length} item{(shown.Length == 1 ? "" : "s")}",
            Theme.Body, Theme.TextMuted, align: TextAlignment.MiddleRight);
        float gy = y + Theme.S(36), gh = h - Theme.S(36);

        if (shown.Length == 0)
        {
            DrawEmpty(paper, x, gy, w, gh, "No props match", $"Nothing called \"{_search}\". Try another search.");
            return;
        }

        float tile = Theme.S(124), gap = Theme.S(Theme.Md);
        int cols = Math.Max(1, (int)((w + gap) / (tile + gap)));
        int rows = (shown.Length + cols - 1) / cols;
        float contentH = rows * (tile + gap);
        float maxScroll = MathF.Max(0f, contentH - gh);
        _scroll = Math.Clamp(_scroll, 0f, maxScroll);

        PropKind? hovered = null;
        using (paper.Box("sm_grid").PositionType(PositionType.SelfDirected).Position(x, gy).Size(w, gh).Clip()
                   .OnScroll(e => _scroll = Math.Clamp(_scroll - (float)e.Delta * Theme.S(60), 0f, maxScroll))
                   .Enter())
        {
            for (int i = 0; i < shown.Length; i++)
            {
                PropKind kind = shown[i];
                float lx = (i % cols) * (tile + gap), ly = (i / cols) * (tile + gap) - _scroll;
                bool hover = paper.IsPointerOverRect(x + lx, gy + ly, tile, tile) && paper.IsPointerOverRect(x, gy, w, gh);
                if (hover) hovered = kind;
                DrawTile(paper, kind, lx, ly, tile, hover, game);
            }
        }

        if (maxScroll > 0f)
        {
            float barH = gh * gh / contentH;
            float barY = gy + (gh - barH) * (_scroll / maxScroll);
            Ui.Rect(paper, "sm_scrollbar", x + w - Theme.S(4), barY, Theme.S(4), barH, Theme.TextFaint, Theme.S(2));
        }

        if (hovered is { } hk) DrawTooltip(paper, hk);
    }

    private void DrawTile(Paper paper, PropKind kind, float x, float y, float size, bool hover, SandboxGame game)
    {
        PropKind captured = kind;
        var rt = icons.Get(kind);
        float a = Ui.Alpha;
        paper.Box($"sm_tile_{kind}").PositionType(PositionType.SelfDirected).Position(x, y).Size(size, size)
            .Rounded(Theme.S(Theme.Radius))
            .BackgroundRadialGradient(0.5f, 0.42f, 0f, size * 0.75f,
                Theme.WithAlpha(hover ? Color.FromArgb(255, 70, 80, 98) : Color.FromArgb(255, 52, 58, 70), a),
                Theme.WithAlpha(Color.FromArgb(255, 30, 33, 40), a))
            .BorderColor(Theme.WithAlpha(hover ? Theme.Accent : Theme.Border, a)).BorderWidth(hover ? 2f : 1f)
            .Scale(hover ? 1.04f : 1f).TransformOrigin(0.5f, 0.5f)
            .Transition(GuiProp.ScaleX, 0.1f).Transition(GuiProp.ScaleY, 0.1f)
            .OnClick(0, (_, _) => game.Enqueue(() => game.SpawnAtCrosshair(captured)))
            .OnPostLayout((handle, rect) => paper.Draw(ref handle, (canvas, r) =>
            {
                float rx = (float)r.Min.X, ry = (float)r.Min.Y, rw = (float)r.Size.X, rh = (float)r.Size.Y;
                float m = rw * 0.08f;
                if (rt?.MainTexture is { } tex)
                {
                    // Render textures are bottom-up; flip Y.
                    canvas.SetBrushTexture(tex);
                    canvas.SetBrushTextureTransform(
                        Prowl.Vector.Spatial.Transform2D.CreateTranslation(rx + m, ry + rh - m - rh * 0.14f) *
                        Prowl.Vector.Spatial.Transform2D.CreateScale(rw - 2 * m, -(rh - 2 * m - rh * 0.14f)));
                    canvas.RectFilled(rx + m, ry + m, rw - 2 * m, rh - 2 * m - rh * 0.14f, new Color32(255, 255, 255, (byte)(255 * a)));
                    canvas.ClearBrushTexture();
                }
                else
                {
                    Color c = PropManager.IconColorDrawing(captured);
                    canvas.RoundedRectFilled(rx + rw * 0.3f, ry + rh * 0.22f, rw * 0.4f, rh * 0.4f, rw * 0.06f,
                        new Color32(c.R, c.G, c.B, (byte)(255 * a)));
                }
            }));

        // Name strip along the bottom of the tile.
        Ui.Text(paper, $"sm_tile_lbl_{kind}", x, y + size * 0.8f, size, size * 0.18f, PropManager.DisplayName(kind),
            Theme.Caption, hover ? Color.White : Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
    }

    private static void DrawTooltip(Paper paper, PropKind kind)
    {
        float px = (float)paper.PointerPos.X + Theme.S(16), py = (float)paper.PointerPos.Y + Theme.S(18);
        float tw = Theme.S(200), th = Theme.S(54);
        Ui.Panel(paper, "sm_tip", px, py, tw, th, Theme.PanelRaised, Theme.S(Theme.RadiusSm)).IsNotInteractable().Layer(Layer.Overlay);
        Ui.Text(paper, "sm_tip_name", px + Theme.S(Theme.Md), py + Theme.S(6), tw, Theme.S(22),
            PropManager.DisplayName(kind), Theme.Body, Theme.Text, Theme.SemiBold);
        Ui.Text(paper, "sm_tip_hint", px + Theme.S(Theme.Md), py + Theme.S(27), tw, Theme.S(20),
            "Click to spawn at crosshair", Theme.Caption, Theme.TextMuted);
    }

    // ---------------------------------------------------------------- Tools

    private static void DrawToolList(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        Ui.Panel(paper, "sm_tools", x, y, w, h, Theme.PanelSunken, shadow: false).IsNotInteractable();
        float ix = x + Theme.S(Theme.Sm), iy = y + Theme.S(Theme.Sm);
        foreach (IGrouping<string, ToolInfo> group in ToolGun.Tools.GroupBy(t => t.Category))
        {
            Ui.Text(paper, $"sm_tcat_{group.Key}", ix + Theme.S(4), iy, w, Theme.S(24), group.Key.ToUpperInvariant(),
                Theme.Caption, Theme.TextFaint, Theme.SemiBold);
            iy += Theme.S(28);
            foreach (ToolInfo tool in group)
            {
                ToolInfo captured = tool;
                bool active = game.Tool == ActiveTool.ToolGun && game.ToolGun.Mode == tool.Mode;
                Ui.Button(paper, $"sm_tool_{tool.Mode}", ix + Theme.S(14), iy, w - Theme.S(14 + 2 * Theme.Sm), Theme.S(30),
                    tool.Name, () => game.SelectTool(ActiveTool.ToolGun, captured.Mode), selected: active,
                    size: Theme.Body, align: TextAlignment.MiddleLeft, baseColor: Color.FromArgb(0, 0, 0, 0));
                iy += Theme.S(34);
            }
            iy += Theme.S(6);
        }
    }

    private static void DrawToolSettings(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        ToolInfo info = ToolGun.InfoFor(game.ToolGun.Mode);
        bool equipped = game.Tool == ActiveTool.ToolGun;
        Ui.Panel(paper, "sm_cp", x, y, w, h, Theme.PanelSunken, shadow: false).IsNotInteractable();
        float pad = Theme.S(Theme.Xl), cx = x + pad, cw = w - 2 * pad, cy = y + pad;

        Ui.Text(paper, "sm_cp_cat", cx, cy, cw, Theme.S(18), info.Category.ToUpperInvariant(), Theme.Caption, Theme.Accent, Theme.SemiBold);
        cy += Theme.S(20);
        Ui.Text(paper, "sm_cp_name", cx, cy, cw, Theme.S(36), info.Name, Theme.Heading, Theme.Text, Theme.Bold);
        cy += Theme.S(42);
        Ui.Paragraph(paper, "sm_cp_desc", cx, cy, cw, Theme.S(40), info.Description, Theme.Body, Theme.TextMuted);
        cy += Theme.S(34);

        // Controls.
        float kx = cx;
        kx += Ui.KeyCap(paper, "sm_cp_lmb", kx, cy, "LMB") + Theme.S(Theme.Sm);
        Ui.Text(paper, "sm_cp_lmbt", kx, cy, cw, Theme.S(20), info.Left, Theme.Body, Theme.Text);
        cy += Theme.S(28);
        if (info.Right is { } right)
        {
            kx = cx;
            kx += Ui.KeyCap(paper, "sm_cp_rmb", kx, cy, "RMB") + Theme.S(Theme.Sm);
            Ui.Text(paper, "sm_cp_rmbt", kx, cy, cw, Theme.S(20), right, Theme.Body, Theme.Text);
            cy += Theme.S(28);
        }

        cy += Theme.S(Theme.Md);
        Ui.Rect(paper, "sm_cp_sep", cx, cy, cw, 1f, Theme.Border);
        cy += Theme.S(Theme.Lg);
        Ui.Text(paper, "sm_cp_set", cx, cy, cw, Theme.S(22), "Settings", Theme.Label, Theme.Text, Theme.SemiBold);
        cy += Theme.S(32);

        float sliderW = MathF.Min(cw, Theme.S(420));
        switch (info.Mode)
        {
            case ToolMode.Weld:
                SettingRow(paper, "weld", cx, ref cy, sliderW, "Weld strength",
                    game.ToolGun.WeldStrength >= 0.995f ? "Rigid" : $"{game.ToolGun.WeldStrength * 100f:0}%",
                    game.ToolGun.WeldStrength, 0f, 1f, v => game.ToolGun.WeldStrength = v,
                    "Lower values give a springy joint that flexes under load.");
                break;
            case ToolMode.Balloon:
                SettingRow(paper, "balloon", cx, ref cy, sliderW, "Balloon force", $"{game.ToolGun.BalloonForce:0.00}× weight",
                    game.ToolGun.BalloonForce, 0.5f, 3f, v => game.ToolGun.BalloonForce = v,
                    "Above 1.00× the prop rises; below it, the balloon only lightens the prop.");
                break;
            default:
                Ui.Text(paper, "sm_cp_none", cx, cy, cw, Theme.S(20), "This tool has no settings.", Theme.Body, Theme.TextMuted);
                cy += Theme.S(28);
                break;
        }

        float bw = Theme.S(180), bh = Theme.S(40);
        Ui.Button(paper, "sm_cp_equip", cx, y + h - pad - bh, bw, bh, equipped ? "✓  Equipped" : "Equip tool gun",
            () => game.SelectTool(ActiveTool.ToolGun, info.Mode), selected: equipped);
    }

    private static void SettingRow(Paper paper, string id, float x, ref float y, float w, string label, string value,
        float current, float min, float max, Action<float> set, string help)
    {
        Ui.Text(paper, $"sm_s_{id}_l", x, y, w, Theme.S(20), label, Theme.Body, Theme.Text);
        Ui.Text(paper, $"sm_s_{id}_v", x, y, w, Theme.S(20), value, Theme.Body, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
        y += Theme.S(24);
        Ui.Slider(paper, $"sm_s_{id}", x, y, w, current, min, max, set);
        y += Theme.S(30);
        Ui.Paragraph(paper, $"sm_s_{id}_h", x, y, w, Theme.S(36), help, Theme.Caption, Theme.TextFaint);
        y += Theme.S(40);
    }

    // ---------------------------------------------------------------- Utilities / empty

    private static void DrawUtilities(Paper paper, float x, float y, float w, float h, SandboxGame game)
    {
        Ui.Panel(paper, "sm_util", x, y, w, h, Theme.PanelSunken, shadow: false).IsNotInteractable();
        float pad = Theme.S(Theme.Xl), cx = x + pad, cy = y + pad;
        Ui.Text(paper, "sm_util_t", cx, cy, w, Theme.S(32), "Map", Theme.Title, Theme.Text, Theme.Bold);
        cy += Theme.S(36);
        Ui.Text(paper, "sm_util_c", cx, cy, w, Theme.S(20), $"{game.Props.Props.Count} props spawned · {game.Props.UndoCount} undo steps",
            Theme.Body, Theme.TextMuted);
        cy += Theme.S(36);
        float bw = Theme.S(200), bh = Theme.S(40);
        Ui.Button(paper, "sm_util_undo", cx, cy, bw, bh, "Undo last action", () => game.Enqueue(game.Undo));
        Ui.Button(paper, "sm_util_clear", cx + bw + Theme.S(Theme.Md), cy, bw, bh, "Clear all props", () => game.Enqueue(game.ClearMap),
            baseColor: Color.FromArgb(255, 90, 36, 40));
        cy += bh + Theme.S(Theme.Xl);
        Ui.Text(paper, "sm_util_t2", cx, cy, w, Theme.S(32), "Player", Theme.Title, Theme.Text, Theme.Bold);
        cy += Theme.S(36);
        Ui.Button(paper, "sm_util_noclip", cx, cy, bw, bh, game.Player.Noclip ? "Disable noclip" : "Enable noclip",
            () => game.Player.ToggleNoclip(), selected: game.Player.Noclip);
    }

    private static void DrawEmpty(Paper paper, float x, float y, float w, float h, string title, string body)
    {
        Ui.Panel(paper, $"sm_empty_{title}", x, y, w, h, Theme.PanelSunken, shadow: false).IsNotInteractable();
        Ui.Text(paper, $"sm_empty_t_{title}", x, y + h / 2f - Theme.S(30), w, Theme.S(30), title, Theme.Title, Theme.TextMuted,
            Theme.SemiBold, TextAlignment.MiddleCenter);
        Ui.Text(paper, $"sm_empty_b_{title}", x, y + h / 2f + Theme.S(4), w, Theme.S(22), body, Theme.Body, Theme.TextFaint,
            align: TextAlignment.MiddleCenter);
    }
}
