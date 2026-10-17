using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Scribe;
using Prowl.Vector;
using Color = System.Drawing.Color;
using TextAlignment = Prowl.PaperUI.TextAlignment;

namespace Sandbox;

/// <summary>Critically-damped-ish spring used for hover/press/open animations.</summary>
public sealed class Spring(float value)
{
    public float Value = value;
    public float Velocity;

    public float Step(float target, float dt, float stiffness = Theme.SpringStiffness, float damping = Theme.SpringDamping)
    {
        dt = MathF.Min(dt, 1f / 30f);
        float force = (target - Value) * stiffness - Velocity * damping;
        Velocity += force * dt;
        Value += Velocity * dt;
        return Value;
    }
}

/// <summary>
/// Widgets over Prowl's immediate-mode UI (Paper). Coordinates are absolute logical pixels
/// (already scaled by <see cref="Theme.Scale"/>). <see cref="Alpha"/> fades whatever menu is drawing.
/// </summary>
public static class Ui
{
    /// <summary>Played on every button/toggle/segment click (wired to the UI click sound).</summary>
    public static Action? ClickSound;

    public static float Alpha { get; set; } = 1f;
    public static float Dt { get; set; } = 1f / 60f;
    private static readonly Dictionary<string, Spring> Springs = [];

    private static Color A(Color c) => Theme.WithAlpha(c, Alpha);
    private static Color32 A32(Color c) => new(c.R, c.G, c.B, (byte)Math.Clamp(c.A * Alpha, 0, 255));

    public static float SpringTo(string id, float target, float initial = 0f)
    {
        if (!Springs.TryGetValue(id, out Spring? s)) Springs[id] = s = new Spring(initial);
        return s.Step(target, Dt);
    }

    public static (float W, float H) ScreenSize()
    {
        var fb = Window.InternalWindow.FramebufferSize;
        float cs = Math.Max(0.01f, Window.ContentScale);
        return (fb.X / cs, fb.Y / cs);
    }

    /// <summary>Frosted glass panel: backdrop blur, a subtle vertical gradient, hairline border, soft shadow.</summary>
    public static ElementBuilder Glass(Paper paper, string id, float x, float y, float w, float h, float? radius = null, bool shadow = true, bool strong = false)
    {
        ElementBuilder b = paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .Rounded(radius ?? Theme.S(Theme.Radius))
            .BackdropBlur(Theme.S(Theme.Blur))
            .BackgroundLinearGradient(0, 0, 0, 1, A(strong ? Theme.GlassTop : Theme.WithAlpha(Theme.GlassTop, 0.8f)), A(Theme.Glass))
            .BorderColor(A(Theme.Border)).BorderWidth(1f);
        if (shadow) b.BoxShadow(0, Theme.S(10), Theme.S(40), 0, A(Theme.Shadow));
        return b;
    }

    public static ElementBuilder Panel(Paper paper, string id, float x, float y, float w, float h,
        Color? color = null, float? radius = null, bool shadow = true)
    {
        ElementBuilder b = paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .BackgroundColor(A(color ?? Theme.Panel)).Rounded(radius ?? Theme.S(Theme.Radius))
            .BorderColor(A(Theme.Border)).BorderWidth(1f);
        if (shadow) b.BoxShadow(0, Theme.S(6), Theme.S(24), 0, A(Theme.Shadow));
        return b;
    }

    public static void Rect(Paper paper, string id, float x, float y, float w, float h, Color color, float rounded = 0f)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .BackgroundColor(A(color)).Rounded(rounded).IsNotInteractable();
    }

    public static void Text(Paper paper, string id, float x, float y, float w, float h, string text, float size,
        Color color, FontFile? font = null, TextAlignment align = TextAlignment.MiddleLeft, bool overlay = false)
    {
        var b = paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).IsNotInteractable()
            .Text(text, font ?? Theme.Regular).TextColor(A(color)).FontSize(Theme.S(size)).Alignment(align);
        if (overlay) b.Layer(Layer.Overlay);
    }

    public static void Paragraph(Paper paper, string id, float x, float y, float w, float h, string text, float size, Color color)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).IsNotInteractable()
            .Text(text, Theme.Regular).TextColor(A(color)).FontSize(Theme.S(size)).Alignment(TextAlignment.Left)
            .Wrap(TextWrapMode.Wrap);
    }

    public static void Icon(Paper paper, string id, float x, float y, float size, Icon icon, Color color)
    {
        Color32 c = A32(color);
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(size, size).IsNotInteractable()
            .OnPostLayout((handle, rect) => paper.Draw(ref handle, (canvas, r) =>
                Icons.Draw(canvas, icon, (float)r.Min.X, (float)r.Min.Y, (float)r.Size.X, c)));
    }

    /// <summary>
    /// Button with optional icon, hover/press spring (scale + lift), selected accent state and focus ring.
    /// </summary>
    public static void Button(Paper paper, string id, float x, float y, float w, float h, string text, Action onClick,
        bool selected = false, bool enabled = true, float size = Theme.Label, TextAlignment align = TextAlignment.MiddleCenter,
        Color? baseColor = null, float? radius = null, Icon? icon = null, bool ghost = false)
    {
        bool hover = enabled && paper.IsPointerOverRect(x, y, w, h);
        bool press = hover && paper.IsPointerDown(PaperMouseBtn.Left);
        float k = SpringTo(id + "#s", press ? 0.965f : hover ? 1.025f : 1f, 1f);

        Color bg = selected ? Theme.AccentSoft : baseColor ?? (ghost ? Color.FromArgb(0, 0, 0, 0) : Theme.PanelRaised);
        if (hover && !selected) bg = press ? Theme.Press : Theme.Hover;
        Color fg = enabled ? (selected ? Color.White : Theme.Text) : Theme.TextFaint;

        ElementBuilder b = paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .Rounded(radius ?? Theme.S(Theme.RadiusSm))
            .BackgroundColor(A(bg))
            .BorderColor(A(selected ? Theme.Accent : hover ? Theme.BorderStrong : ghost ? Color.FromArgb(0, 0, 0, 0) : Theme.Border))
            .BorderWidth(1f)
            .Scale(k).TransformOrigin(0.5f, 0.5f)
            .Focused.BorderColor(A(Theme.Accent)).BorderWidth(2f).End();
        if (enabled) b.OnClick(0, (_, _) => { ClickSound?.Invoke(); onClick(); });

        // Content: [icon] text. Paper ignores padding for a box's own text, so labels are separate boxes.
        float isz = Theme.S(size + 4);
        float pad = Theme.S(Theme.Md);
        bool center = align == TextAlignment.MiddleCenter;
        float textW = paper.MeasureText(text, Theme.S(size), selected ? Theme.SemiBold : Theme.Regular, 0f).X;
        float contentW = (icon is null ? 0f : isz + Theme.S(Theme.Sm)) + (float)textW;
        float cx = center ? x + (w - contentW) / 2f : x + pad;
        float dx = (k - 1f) * w * 0.5f * (center ? 0f : -1f);
        if (icon is { } ic)
        {
            Icon(paper, id + "_ic", cx + dx, y + (h - isz) / 2f, isz, ic, selected ? Color.White : enabled ? Theme.TextMuted : Theme.TextFaint);
            cx += isz + Theme.S(Theme.Sm);
        }
        if (text.Length > 0)
            Text(paper, id + "_lbl", cx + dx, y, w, h, text, size, fg, selected ? Theme.SemiBold : Theme.Regular);
    }

    /// <summary>Horizontal slider. <paramref name="set"/> is called while the user drags it.</summary>
    public static void Slider(Paper paper, string id, float x, float y, float w, float value, float min, float max, Action<float> set)
    {
        float h = Theme.S(4), hitH = Theme.S(24);
        float t = Math.Clamp((value - min) / (max - min), 0f, 1f);
        bool hover = paper.IsPointerOverRect(x - Theme.S(8), y, w + Theme.S(16), hitH);
        float knob = Theme.S(14) * SpringTo(id + "#k", hover ? 1.2f : 1f, 1f);
        paper.Box(id + "_hit").PositionType(PositionType.SelfDirected).Position(x - Theme.S(8), y).Size(w + Theme.S(16), hitH)
            .BackgroundColor(Color.FromArgb(0, 0, 0, 0))
            .OnHeld(0, (_, _) => set(min + Math.Clamp(((float)paper.PointerPos.X - x) / w, 0f, 1f) * (max - min)));
        Rect(paper, id + "_track", x, y + (hitH - h) / 2f, w, h, Theme.Hover, h / 2f);
        Rect(paper, id + "_fill", x, y + (hitH - h) / 2f, MathF.Max(h, w * t), h, Theme.Accent, h / 2f);
        paper.Box(id + "_knob").PositionType(PositionType.SelfDirected)
            .Position(x + w * t - knob / 2f, y + (hitH - knob) / 2f).Size(knob, knob).IsNotInteractable()
            .BackgroundColor(A(Color.White)).Rounded(knob / 2f).BoxShadow(0, Theme.S(2), Theme.S(6), 0, A(Theme.Shadow));
    }

    /// <summary>iOS/s&box style switch.</summary>
    public static void Toggle(Paper paper, string id, float x, float y, bool value, Action<bool> set)
    {
        float w = Theme.S(40), h = Theme.S(22), knob = h - Theme.S(4);
        float t = SpringTo(id + "#t", value ? 1f : 0f, value ? 1f : 0f);
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).Rounded(h / 2f)
            .BackgroundColor(A(value ? Theme.Accent : Theme.Hover)).BorderColor(A(Theme.Border)).BorderWidth(1f)
            .OnClick(0, (_, _) => { ClickSound?.Invoke(); set(!value); });
        Rect(paper, id + "_k", x + Theme.S(2) + t * (w - knob - Theme.S(4)), y + Theme.S(2), knob, knob, Color.White, knob / 2f);
    }

    /// <summary>Segmented control (e.g. Low / Medium / High).</summary>
    public static void Segmented(Paper paper, string id, float x, float y, float w, string[] options, int selected, Action<int> set)
    {
        float h = Theme.S(30), seg = w / options.Length;
        Rect(paper, id + "_bg", x, y, w, h, Theme.PanelSunken, Theme.S(Theme.RadiusXs + 2));
        float sx = SpringTo(id + "#x", x + Math.Max(0, selected) * seg, x + Math.Max(0, selected) * seg);
        if (selected >= 0) Rect(paper, id + "_sel", sx + Theme.S(2), y + Theme.S(2), seg - Theme.S(4), h - Theme.S(4), Theme.AccentSoft, Theme.S(Theme.RadiusXs));
        for (int i = 0; i < options.Length; i++)
        {
            int captured = i;
            paper.Box($"{id}_{i}").PositionType(PositionType.SelfDirected).Position(x + i * seg, y).Size(seg, h)
                .BackgroundColor(Color.FromArgb(0, 0, 0, 0)).OnClick(0, (_, _) => { ClickSound?.Invoke(); set(captured); });
            Text(paper, $"{id}_{i}_t", x + i * seg, y, seg, h, options[i], Theme.Body, i == selected ? Color.White : Theme.TextMuted,
                i == selected ? Theme.SemiBold : Theme.Regular, TextAlignment.MiddleCenter);
        }
    }

    /// <summary>Keyboard key cap, e.g. [LMB]. Returns its width.</summary>
    public static float KeyCap(Paper paper, string id, float x, float y, string key, float size = Theme.Micro)
    {
        float h = Theme.S(size + 7);
        float w = MathF.Max(h, (float)paper.MeasureText(key, Theme.S(size), Theme.SemiBold, 0f).X + Theme.S(10));
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).IsNotInteractable()
            .BackgroundColor(A(Theme.PanelRaised)).BorderColor(A(Theme.Border)).BorderWidth(1f).Rounded(Theme.S(4))
            .Text(key, Theme.SemiBold).TextColor(A(Theme.Text)).FontSize(Theme.S(size)).Alignment(TextAlignment.MiddleCenter);
        return w;
    }
}
