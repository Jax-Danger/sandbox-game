using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// GMod-style context menu: hold C to free the cursor, click a prop in the world to edit it
/// (freeze, colour, material, mass, delete). Also shows the current tool's quick settings.
/// </summary>
public sealed class ContextMenu
{
    private float _openness;
    public bool Open { get; set; }
    public bool Visible => _openness > 0.01f;
    public Prop? Selected { get; set; }
    public Prop? Hovered { get; private set; }

    public static readonly Prowl.Vector.Color[] Palette =
    [
        new(0.92f, 0.30f, 0.26f, 1f), new(0.96f, 0.62f, 0.20f, 1f), new(0.97f, 0.86f, 0.28f, 1f), new(0.36f, 0.78f, 0.40f, 1f),
        new(0.26f, 0.62f, 0.95f, 1f), new(0.56f, 0.40f, 0.92f, 1f), new(0.93f, 0.45f, 0.72f, 1f), new(0.95f, 0.95f, 0.95f, 1f),
        new(0.45f, 0.47f, 0.50f, 1f), new(0.12f, 0.13f, 0.15f, 1f),
    ];

    /// <summary>Picks the prop under the mouse cursor. Called from the game update while open.</summary>
    public void Update(SandboxGame g, bool clicked, bool pointerOverUi)
    {
        if (!Open) { Hovered = null; return; }
        if (Selected is { Alive: false }) Selected = null;
        Hovered = null;
        if (pointerOverUi) return;
        Float2 mouse = (Float2)Input.MousePosition;
        var size = new Float2(Window.Size.X, Window.Size.Y);
        Ray ray = g.Player.Camera.ScreenPointToRay(mouse, size);
        if (g.World.Physics.Raycast(ray.Origin, ray.Direction, 150f, out RaycastHit hit))
            Hovered = g.Props.FromBody(hit.Rigidbody);
        if (clicked)
        {
            Selected = Hovered;
            if (Selected is not null) g.Sounds.Play("sfx_ui_click", 0.4f);
        }
    }

    public bool PointerOverPanel(Paper paper, float w, float h) =>
        _openness > 0.5f && (paper.IsPointerOverRect(PanelX(w), PanelY(h), PanelW, PanelH(h)) || paper.IsPointerOverRect(w / 2f - Theme.S(200), Theme.S(14), Theme.S(400), Theme.S(40)));

    private static float PanelW => Theme.S(300);
    private static float PanelX(float w) => w - PanelW - Theme.S(20);
    private static float PanelY(float h) => Theme.S(20);
    private static float PanelH(float h) => h - Theme.S(40);

    public void Draw(Paper paper, float w, float h, float dt, SandboxGame g)
    {
        _openness = Math.Clamp(_openness + (Open ? 1f : -1f) * dt * Theme.OpenSpeed, 0f, 1f);
        if (_openness <= 0.01f) return;
        Ui.Alpha = Theme.Ease(_openness);

        // Hint pill at the top.
        string hint = Selected is null ? "Click a prop to edit it" : $"Editing {Selected.Go.Name}";
        float hw = (float)paper.MeasureText(hint, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X + Theme.S(28);
        Ui.Glass(paper, "cx_hint", (w - hw) / 2f, Theme.S(20), hw, Theme.S(28), Theme.S(14), shadow: false).IsNotInteractable();
        Ui.Text(paper, "cx_hint_t", (w - hw) / 2f, Theme.S(20), hw, Theme.S(28), hint, Theme.Caption, Theme.Text, Theme.SemiBold, TextAlignment.MiddleCenter);

        float x = PanelX(w) + (1f - Ui.Alpha) * Theme.S(30), y = PanelY(h), pw = PanelW, ph = PanelH(h), pad = Theme.S(Theme.Lg);
        Ui.Glass(paper, "cx_panel", x, y, pw, ph, strong: true).IsNotInteractable();
        float cx = x + pad, cw = pw - 2 * pad, cy = y + pad;

        if (Selected is { } p)
        {
            Ui.Text(paper, "cx_kind", cx, cy, cw, Theme.S(16), "PROP", Theme.Micro, Theme.Accent, Theme.Bold);
            cy += Theme.S(18);
            Ui.Text(paper, "cx_name", cx, cy, cw, Theme.S(28), p.Go.Name, Theme.Title, Theme.Text, Theme.Bold);
            cy += Theme.S(40);

            Ui.Text(paper, "cx_frz", cx, cy, cw, Theme.S(22), "Frozen", Theme.Body, Theme.Text);
            Ui.Toggle(paper, "cx_frz_t", cx + cw - Theme.S(40), cy, p.Frozen, v => g.Enqueue(() => p.SetFrozen(v)));
            cy += Theme.S(36);

            Ui.Text(paper, "cx_col", cx, cy, cw, Theme.S(18), "Colour", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
            cy += Theme.S(22);
            float sw = (cw - 4 * Theme.S(6)) / 5f;
            for (int i = 0; i < Palette.Length; i++)
            {
                var col = Palette[i];
                float sx = cx + (i % 5) * (sw + Theme.S(6)), sy = cy + (i / 5) * (Theme.S(26) + Theme.S(6));
                bool sel = MathF.Abs(p.BaseColor.R - col.R) + MathF.Abs(p.BaseColor.G - col.G) + MathF.Abs(p.BaseColor.B - col.B) < 0.02f;
                paper.Box($"cx_sw_{i}").PositionType(PositionType.SelfDirected).Position(sx, sy).Size(sw, Theme.S(26)).Rounded(Theme.S(Theme.RadiusXs))
                    .BackgroundColor(Theme.WithAlpha(Color.FromArgb(255, (int)(col.R * 255), (int)(col.G * 255), (int)(col.B * 255)), Ui.Alpha))
                    .BorderColor(Theme.WithAlpha(sel ? Color.White : Theme.Border, Ui.Alpha)).BorderWidth(sel ? 2f : 1f)
                    .OnClick(0, (_, _) => p.SetColor(col));
            }
            cy += 2 * (Theme.S(26) + Theme.S(6)) + Theme.S(10);

            Ui.Text(paper, "cx_mat", cx, cy, cw, Theme.S(18), "Material", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
            cy += Theme.S(22);
            float mw = (cw - 2 * Theme.S(6)) / 3f;
            for (int i = 0; i < SurfaceMaterial.All.Length; i++)
            {
                SurfaceMaterial m = SurfaceMaterial.All[i];
                Ui.Button(paper, $"cx_m_{m.Name}", cx + (i % 3) * (mw + Theme.S(6)), cy + (i / 3) * Theme.S(36), mw, Theme.S(30), m.Name,
                    () => g.Enqueue(() => p.SetSurface(m)), selected: p.Surface == m.Name, size: Theme.Caption, radius: Theme.S(Theme.RadiusXs));
            }
            cy += 2 * Theme.S(36) + Theme.S(10);

            Ui.Text(paper, "cx_mass", cx, cy, cw, Theme.S(20), "Mass", Theme.Caption, Theme.TextMuted, Theme.SemiBold);
            Ui.Text(paper, "cx_mass_v", cx, cy, cw, Theme.S(20), $"{p.Body.Mass:0.0} kg", Theme.Caption, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
            cy += Theme.S(22);
            Ui.Slider(paper, "cx_mass_s", cx, cy, cw, MathF.Log10(p.Body.Mass), -1f, 2.7f, v => p.SetMass(MathF.Pow(10f, v)));
            cy += Theme.S(40);

            if (p.Go.GetComponent<SandboxButton>() is { } sbtn)
            {
                Ui.Text(paper, "cx_btnkey", cx, cy + Theme.S(3), cw, Theme.S(18), "Button key", Theme.Caption, Theme.TextMuted);
                Ui.KeyPicker(paper, "cx_btnkey_k", cx + cw - Theme.S(96), cy, Theme.S(96), Theme.S(24), sbtn.Key, k => sbtn.Rebind(k));
                cy += Theme.S(34);
            }
            Ui.Button(paper, "cx_del", cx, cy, cw, Theme.S(36), "Delete prop", () => g.Enqueue(() => { g.Props.Remove(p); Selected = null; g.Notify($"Removed {p.Go.Name}"); }),
                icon: Icon.Trash, radius: Theme.S(18), baseColor: Color.FromArgb(200, 200, 60, 60));
            cy += Theme.S(52);
        }
        else
        {
            Ui.Icon(paper, "cx_empty_ic", cx + (cw - Theme.S(32)) / 2f, cy + Theme.S(30), Theme.S(32), Icon.Cube, Theme.TextFaint);
            Ui.Text(paper, "cx_empty", cx, cy + Theme.S(70), cw, Theme.S(20), "No prop selected", Theme.Body, Theme.TextMuted, Theme.SemiBold, TextAlignment.MiddleCenter);
            Ui.Text(paper, "cx_empty2", cx, cy + Theme.S(92), cw, Theme.S(18), "Click one in the world", Theme.Caption, Theme.TextFaint, align: TextAlignment.MiddleCenter);
            cy += Theme.S(140);
        }

        // Quick tool settings for the current tool.
        Ui.Rect(paper, "cx_sep", cx, cy, cw, 1f, Theme.Border);
        cy += Theme.S(14);
        ToolInfo info = ToolGun.InfoFor(g.ToolGun.Mode);
        Ui.Icon(paper, "cx_tool_ic", cx, cy, Theme.S(16), Icons.For(info.Mode), Theme.TextMuted);
        Ui.Text(paper, "cx_tool", cx + Theme.S(22), cy - Theme.S(2), cw, Theme.S(20), $"{info.Name} tool", Theme.Body, Theme.Text, Theme.SemiBold);
        cy += Theme.S(28);
        foreach (ToolSetting s in g.ToolGun.SettingsFor(info.Mode))
        {
            if (s.IsKey)
            {
                Ui.Text(paper, $"cx_ts_{s.Name}", cx, cy + Theme.S(3), cw, Theme.S(18), s.Name, Theme.Caption, Theme.TextMuted);
                ToolSetting ks = s;
                Ui.KeyPicker(paper, $"cx_ts_{s.Name}_k", cx + cw - Theme.S(96), cy, Theme.S(96), Theme.S(24), (Prowl.Runtime.KeyCode)(int)s.Get(), k => ks.Set((int)k));
                cy += Theme.S(34);
                continue;
            }
            Ui.Text(paper, $"cx_ts_{s.Name}", cx, cy, cw, Theme.S(18), s.Name, Theme.Caption, Theme.TextMuted);
            Ui.Text(paper, $"cx_ts_{s.Name}_v", cx, cy, cw, Theme.S(18), s.Format(s.Get()), Theme.Caption, Theme.Accent, Theme.SemiBold, TextAlignment.MiddleRight);
            cy += Theme.S(20);
            Ui.Slider(paper, $"cx_ts_{s.Name}_s", cx, cy, cw, s.Get(), s.Min, s.Max, s.Set);
            cy += Theme.S(32);
        }
        Ui.Alpha = 1f;
    }
}
