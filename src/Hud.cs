using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>Crosshair, current tool, prop count, control hints, and a short message line.</summary>
public sealed class Hud
{
    private string _message = "";
    private float _messageTime;

    public void Flash(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        _message = message;
        _messageTime = 3f;
        Console.WriteLine($"[hud] {message}");
    }

    public void Tick(float dt) => _messageTime = MathF.Max(0f, _messageTime - dt);

    public void Draw(Paper paper, float w, float h, string toolLine, string modeLine, int propCount, bool noclip)
    {
        // Crosshair
        float cx = w / 2f, cy = h / 2f;
        Ui.Rect(paper, "xh_h", cx - 9f, cy - 1f, 18f, 2f, Color.FromArgb(230, 255, 255, 255));
        Ui.Rect(paper, "xh_v", cx - 1f, cy - 9f, 2f, 18f, Color.FromArgb(230, 255, 255, 255));

        var panel = Color.FromArgb(150, 10, 12, 16);
        Ui.Label(paper, "hud_tool", 16f, 16f, 460f, 34f, "  " + toolLine, 20f, Color.White, background: panel);
        Ui.Label(paper, "hud_mode", 16f, 54f, 460f, 28f, "  " + modeLine, 15f, Color.FromArgb(255, 200, 210, 225), background: panel);
        Ui.Label(paper, "hud_count", w - 236f, 16f, 220f, 34f, $"Props: {propCount}{(noclip ? "   NOCLIP" : "")}  ", 20f,
            Color.White, TextAlignment.MiddleRight, panel);

        Ui.Label(paper, "hud_hint", 16f, h - 44f, w - 32f, 30f,
            "  WASD move · Space jump · V noclip · Q/Tab spawn · 1 Physgun · 2 Weld · 3 Remover · 4 Balloon · LMB use · RMB freeze · Wheel push/pull · Z undo · Esc menu",
            14f, Color.FromArgb(255, 220, 220, 220), background: panel);

        if (_messageTime > 0f)
        {
            int a = (int)(255 * Math.Clamp(_messageTime, 0f, 1f));
            Ui.Label(paper, "hud_msg", cx - 300f, cy + 40f, 600f, 32f, _message, 18f,
                Color.FromArgb(a, 255, 240, 160), TextAlignment.MiddleCenter, Color.FromArgb(a * 150 / 255, 10, 12, 16));
        }
    }
}
