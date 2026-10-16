using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

public enum PauseAction { None, Resume, ClearMap, Quit }

/// <summary>Escape menu: Resume, Clear map, Quit.</summary>
public sealed class PauseMenu
{
    public bool Open { get; set; }
    public PauseAction Requested { get; set; }

    public void Draw(Paper paper, float w, float h)
    {
        if (!Open) return;
        Ui.Rect(paper, "pause_dim", 0, 0, w, h, Color.FromArgb(140, 0, 0, 0));
        const float pw = 320f, ph = 300f;
        float x = (w - pw) / 2f, y = (h - ph) / 2f;
        Ui.Rect(paper, "pause_bg", x, y, pw, ph, Color.FromArgb(235, 18, 20, 26), 12f);
        Ui.Label(paper, "pause_title", x, y + 14f, pw, 44f, "Paused", 28f, Color.White, Prowl.PaperUI.TextAlignment.MiddleCenter);
        Ui.Button(paper, "pause_resume", x + 30f, y + 80f, pw - 60f, 52f, "Resume", () => Requested = PauseAction.Resume);
        Ui.Button(paper, "pause_clear", x + 30f, y + 148f, pw - 60f, 52f, "Clear map", () => Requested = PauseAction.ClearMap);
        Ui.Button(paper, "pause_quit", x + 30f, y + 216f, pw - 60f, 52f, "Quit", () => Requested = PauseAction.Quit);
    }
}
