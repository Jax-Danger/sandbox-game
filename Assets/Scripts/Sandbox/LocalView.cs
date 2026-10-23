using Prowl.Runtime;

namespace Sandbox;

/// <summary>
/// Garry's Mod style split between what only the owning player sees and what the world sees, kept in one place so a
/// later client/server split only has to change <see cref="IsLocal"/>.
/// <list type="bullet">
/// <item><b>World model</b> (<see cref="WorldLayer"/>): the full player body with its head and normal animation. Every camera
/// sees it (camera props, icon renders, future mirrors and other clients) except the owner's own first-person camera.</item>
/// <item><b>View model</b> (<see cref="ViewLayer"/>): the first-person weapon, the arms-only Y Bot and the headless
/// first-person body. Only the owner's camera draws it; no other camera, and it casts no shadows.</item>
/// </list>
/// No networking: there is one local player, so <see cref="IsLocal"/> is always true today.
/// </summary>
public static class LocalView
{
    /// <summary>Layer for owner-only first-person renderers.</summary>
    public const int ViewLayer = 30;
    /// <summary>Layer for the owner's world model while the owner is in first person (hidden from the owner's camera only).</summary>
    public const int WorldLayer = 29; // not 31: Prowl reserves that bit for gizmos and strips it from every camera

    /// <summary>Whether a player object belongs to this client. Single player: always.</summary>
    public static bool IsLocal(Player? _) => true;

    public static uint Bit(int layer) => 1u << layer;

    /// <summary>The owner's first-person camera: sees the view model, not its own world model.</summary>
    public static void SetupOwnerCamera(Camera cam) => cam.CullingMask = LayerMask.FromMask(cam.CullingMask.Mask & ~Bit(WorldLayer) | Bit(ViewLayer));

    /// <summary>Any other camera (camera props, icon renders): sees world models, never a view model.</summary>
    public static void SetupWorldCamera(Camera cam) => cam.CullingMask = LayerMask.FromMask(cam.CullingMask.Mask & ~Bit(ViewLayer) | Bit(WorldLayer));

    /// <summary>Puts a whole hierarchy on a layer.</summary>
    public static void SetLayer(GameObject go, int layer)
    {
        go.LayerIndex = layer;
        foreach (GameObject c in go.Children) SetLayer(c, layer);
    }
}
