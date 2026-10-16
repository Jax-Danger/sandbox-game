using Prowl.PaperUI;
using Prowl.PaperUI.LayoutEngine;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>Small helpers over Prowl's immediate-mode UI (Paper).</summary>
public static class Ui
{
    private static FontAsset? _font;
    public static Prowl.Scribe.FontFile Font => (_font ??= FontAsset.LoadDefault()).FontFile;

    public static (float W, float H) ScreenSize()
    {
        var fb = Window.InternalWindow.FramebufferSize;
        float cs = Math.Max(0.01f, Window.ContentScale);
        return (fb.X / cs, fb.Y / cs);
    }

    public static void Rect(Paper paper, string id, float x, float y, float w, float h, Color color, float rounded = 0f)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .BackgroundColor(color).Rounded(rounded).IsNotInteractable();
    }

    public static void Label(Paper paper, string id, float x, float y, float w, float h, string text, float size,
        Color color, TextAlignment align = TextAlignment.MiddleLeft, Color? background = null)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h)
            .BackgroundColor(background ?? Color.Transparent).Rounded(6).IsNotInteractable()
            .Text(text, Font).TextColor(color).FontSize(size).Alignment(align);
    }

    public static void Button(Paper paper, string id, float x, float y, float w, float h, string text, Action onClick)
    {
        paper.Box(id).PositionType(PositionType.SelfDirected).Position(x, y).Size(w, h).Rounded(8)
            .BackgroundColor(Color.FromArgb(230, 40, 44, 52))
            .Hovered.BackgroundColor(Color.FromArgb(240, 70, 110, 170)).End()
            .Text(text, Font).TextColor(Color.White).FontSize(18f).Alignment(TextAlignment.MiddleCenter)
            .OnClick(0, (_, _) => onClick());
    }
}
