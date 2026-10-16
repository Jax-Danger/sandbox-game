using Prowl.PaperUI;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>Spawn menu, shown while Q or Tab is held. Click a tile to spawn that prop at the crosshair.</summary>
public sealed class SpawnMenu
{
    public bool Open { get; set; }
    public PropKind? Requested { get; set; }

    public void Draw(Paper paper, float w, float h)
    {
        if (!Open) return;
        const int cols = 3;
        const float tile = 150f, gap = 14f;
        int rows = (PropManager.AllKinds.Length + cols - 1) / cols;
        float panelW = cols * tile + (cols + 1) * gap;
        float panelH = 56f + rows * (tile * 0.6f) + (rows + 1) * gap;
        float x0 = (w - panelW) / 2f, y0 = (h - panelH) / 2f;

        Ui.Rect(paper, "spawn_bg", x0, y0, panelW, panelH, Color.FromArgb(210, 18, 20, 26), 12f);
        Ui.Label(paper, "spawn_title", x0 + gap, y0 + 8f, panelW - 2 * gap, 40f, "Spawn menu  (click a prop)", 22f, Color.White);

        for (int i = 0; i < PropManager.AllKinds.Length; i++)
        {
            PropKind kind = PropManager.AllKinds[i];
            int c = i % cols, r = i / cols;
            float x = x0 + gap + c * (tile + gap);
            float y = y0 + 56f + gap + r * (tile * 0.6f + gap);
            Ui.Button(paper, $"spawn_{kind}", x, y, tile, tile * 0.6f, PropManager.DisplayName(kind), () => Requested = kind);
        }
    }
}
