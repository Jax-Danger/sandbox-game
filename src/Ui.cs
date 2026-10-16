using Prowl.PaperUI;
using Prowl.PaperUI.LayoutEngine;
using Prowl.Runtime;
using Prowl.Scribe;
using TextAlignment = Prowl.PaperUI.TextAlignment;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// Widgets over Prowl's immediate-mode UI (Paper). Every call takes absolute logical
/// coordinates (already multiplied by <see cref="Theme.Scale"/>) and honours <see cref="Alpha"/>,
/// the fade multiplier of whichever menu is drawing.
/// </summary>
public static class Ui
{
    /// <summary>Global opacity multiplier for the menu currently drawing (used by open/close fades).</summary>
    public static float Alpha { get; set; } = 1f;

    private static Color A(Color c) => Theme.WithAlpha(c, Alpha);

    public static (float W, float H) ScreenSize()
    {
        var fb = Window.InternalWindow.FramebufferSize;
        float cs = Math.Max(0.01f, Window.ContentScale);
        return (fb.X / cs, fb.Y / cs);
    }

    public static ElementBuilder Panel(Paper paper, string id, float x, float y, float w, float h,
        Color? color = null, float? radius = null, bool shadow = true, bool blur = false)
    {
        ElementBuilder b = paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .BackgroundColor(A(color ?? Theme.Panel)).Rounded(radius ?? Theme.S(Theme.Radius))
            .BorderColor(A(Theme.Border)).BorderWidth(1f);
        if (shadow) b.BoxShadow(0, Theme.S(6), Theme.S(24), 0, A(Theme.Shadow));
        if (blur) b.BackdropBlur(Theme.S(12));
        return b;
    }

    public static void Rect(Paper paper, string id, float x, float y, float w, float h, Color color, float rounded = 0f)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .BackgroundColor(A(color)).Rounded(rounded).IsNotInteractable();
    }

    public static void Text(Paper paper, string id, float x, float y, float w, float h, string text, float size,
        Color color, FontFile? font = null, TextAlignment align = TextAlignment.MiddleLeft)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).IsNotInteractable()
            .Text(text, font ?? Theme.Regular).TextColor(A(color)).FontSize(Theme.S(size)).Alignment(align);
    }

    /// <summary>Multi-line wrapped text.</summary>
    public static void Paragraph(Paper paper, string id, float x, float y, float w, float h, string text, float size, Color color)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).IsNotInteractable()
            .Text(text, Theme.Regular).TextColor(A(color)).FontSize(Theme.S(size)).Alignment(TextAlignment.Left)
            .Wrap(TextWrapMode.Wrap);
    }

    /// <summary>A button with a hover state, optional selected (accent) state, and smooth colour transition.</summary>
    public static void Button(Paper paper, string id, float x, float y, float w, float h, string text, Action onClick,
        bool selected = false, bool enabled = true, float size = Theme.Label, TextAlignment align = TextAlignment.MiddleCenter,
        Color? baseColor = null, float? radius = null)
    {
        Color bg = selected ? Theme.AccentSoft : baseColor ?? Theme.PanelRaised;
        Color fg = enabled ? (selected ? Color.White : Theme.Text) : Theme.TextFaint;
        bool inset = align == TextAlignment.MiddleLeft;
        ElementBuilder b = paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .Rounded(radius ?? Theme.S(Theme.RadiusSm))
            .BackgroundColor(A(bg))
            .BorderColor(A(selected ? Theme.Accent : Color.FromArgb(0, 0, 0, 0))).BorderWidth(selected ? 1.5f : 0f)
            .Transition(GuiProp.BackgroundColor, 0.12f);
        if (!inset)
            b.Text(text, selected ? Theme.SemiBold : Theme.Regular).TextColor(A(fg)).FontSize(Theme.S(size)).Alignment(align);
        if (enabled)
        {
            b.Hovered.BackgroundColor(A(selected ? Theme.AccentSoft : Theme.Hover)).End();
            b.OnClick(0, (_, _) => onClick());
        }
        // Paper draws a box's own text over the full rect and ignores padding, so inset labels are separate.
        if (inset)
            Text(paper, id + "_lbl", x + Theme.S(Theme.Md), y, w - Theme.S(Theme.Md), h, text, size, fg,
                selected ? Theme.SemiBold : Theme.Regular);
    }

    /// <summary>Horizontal slider. <paramref name="set"/> is called while the user drags it.</summary>
    public static void Slider(Paper paper, string id, float x, float y, float w, float value, float min, float max, Action<float> set)
    {
        float h = Theme.S(6), knob = Theme.S(16);
        float t = Math.Clamp((value - min) / (max - min), 0f, 1f);
        float hitH = Theme.S(24);
        paper.Box(id + "_hit").PositionType(PositionType.SelfDirected).Position(x, y).Size(w, hitH)
            .BackgroundColor(Color.FromArgb(0, 0, 0, 0))
            .OnHeld(0, (_, _) =>
            {
                float px = (float)paper.PointerPos.X;
                set(min + Math.Clamp((px - x) / w, 0f, 1f) * (max - min));
            });
        Rect(paper, id + "_track", x, y + (hitH - h) / 2f, w, h, Theme.Hover, h / 2f);
        Rect(paper, id + "_fill", x, y + (hitH - h) / 2f, MathF.Max(h, w * t), h, Theme.Accent, h / 2f);
        Rect(paper, id + "_knob", x + w * t - knob / 2f, y + (hitH - knob) / 2f, knob, knob, Color.White, knob / 2f);
    }

    /// <summary>Keyboard key cap, e.g. [LMB].</summary>
    public static float KeyCap(Paper paper, string id, float x, float y, string key)
    {
        float h = Theme.S(20);
        float w = MathF.Max(h, Theme.S(10 + 7.5f * key.Length));
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).IsNotInteractable()
            .BackgroundColor(A(Theme.PanelRaised)).BorderColor(A(Theme.Border)).BorderWidth(1f).Rounded(Theme.S(4))
            .Text(key, Theme.SemiBold).TextColor(A(Theme.Text)).FontSize(Theme.S(Theme.Caption)).Alignment(TextAlignment.MiddleCenter);
        return w;
    }
}
