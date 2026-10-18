using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Screenshot-only showcase scenes ("--shot gal_&lt;set&gt;"): lines up every model of a category, frozen, in front of
/// the player with the HUD and view model hidden, so model polish can be reviewed in one image.
/// Sets: props (model props), basic (prop kinds + entities), vehicles, npcs, weapons (spawn-menu weapon models).
/// </summary>
public static class Gallery
{
    public static bool Active { get; private set; }

    public static void Show(SandboxGame g, string set)
    {
        Active = true;
        g.PhotoMode = true;
        var spots = new List<Float3>();
        Prop Freeze(Prop p) => p; // frozen props are tinted blue, so gallery props just settle
        switch (set.Split('_')[0])
        {
            case "props":
            {
                int page = set.Contains('_') && int.TryParse(set.Split('_')[1], out int pg) ? pg : 0;
                var defs = ModelProps.All.Skip(page * 15).Take(15).ToArray();
                for (int i = 0; i < defs.Length; i++)
                {
                    int col = i % 5, row = i / 5;
                    Float3 at = new(-4.4f + col * 2.2f, defs[i].HalfHeight + 0.02f, 3.2f + row * 2.6f);
                    Freeze(ModelProps.Spawn(g, defs[i], at, 200f, false));
                }
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 22f);
                break;
            }
            case "basic":
            {
                var kinds = PropManager.AllKinds;
                for (int i = 0; i < kinds.Length; i++)
                {
                    int col = i % 6, row = i / 6;
                    Float3 at = new(-6f + col * 2.4f, PropManager.HalfHeightOf(kinds[i]) + 0.4f, 6.5f + row * 3.6f);
                    Freeze(g.Props.Spawn(kinds[i], at, 20f, false));
                }
                Freeze(EntityCatalog.SpawnBarrel(g, new Float3(-4f, 0.6f, 4.5f)));
                Freeze(EntityCatalog.SpawnBall(g, new Float3(-2.4f, 0.6f, 4.5f)));
                Freeze(EntityCatalog.SpawnLight(g, new Float3(-0.8f, 0.5f, 4.5f)));
                Freeze(EntityCatalog.SpawnButton(g, new Float3(0.8f, 0.2f, 4.5f), 180f));
                Freeze(CameraFeeds.Spawn(g, new Float3(2.4f, 0.6f, 4.5f), 180f, recordUndo: false));
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 22f);
                break;
            }
            case "vehicles":
            {
                g.Vehicles.SpawnBuggy(new Float3(-3.4f, 1.0f, 4.2f), 215f, false);
                g.Vehicles.SpawnBuggy(new Float3(0f, 1.2f, 4.8f), 215f, false, "jeep");
                g.Vehicles.SpawnBuggy(new Float3(3.6f, 0.9f, 4.2f), 215f, false, "airboat");
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 18f);
                break;
            }
            case "npcs":
            case "face":
            {
                NpcTeam[] teams = [NpcTeam.Dummy, NpcTeam.Friendly, NpcTeam.Hostile];
                for (int i = 0; i < 3; i++)
                {
                    Ragdoll rd = EntityCatalog.SpawnDummy(g, new Float3(-1.5f + i * 1.2f, 0.05f, 2.2f), 180f, false, teams[i]);
                    foreach (Prop l in rd.Limbs) { if (l.Go.GetComponent<DummyBrain>() is { } b) b.Enabled = false; l.Body.MotionType = Jitter2.Dynamics.MotionType.Static; }
                }
                Ragdoll.Spawn(g, new Float3(2.4f, 0.6f, 2.6f), 140f, false);
                g.Player.Teleport(new Float3(0f, 0.05f, -1.5f));
                g.Player.SetLook(0f, 10f);
                if (set.StartsWith("face")) { g.Player.Teleport(new Float3(-0.3f, 0.05f, 1.45f)); g.Player.SetLook(0f, 4f); }
                break;
            }
        }
    }

    /// <summary>Keeps the view model hidden while a gallery is on screen.</summary>
    public static void Hide(ViewModel? vm) { if (Active) vm?.SetVisible(false); }
}
