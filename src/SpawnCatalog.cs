using Prowl.Vector;

namespace Sandbox;

/// <summary>One thing the spawn menu can create.</summary>
public sealed record SpawnEntry(string Id, string Name, MenuTab Tab, string Category, Icon Icon, PropKind? Kind,
    Func<SandboxGame, Float3, float, string> Spawn, float HalfHeight = 0.5f);

/// <summary>Everything spawnable, grouped by tab and category. Later features register more entries here.</summary>
public static class SpawnCatalog
{
    private static readonly List<SpawnEntry> _entries = [];
    public static IReadOnlyList<SpawnEntry> Entries => _entries;

    static SpawnCatalog()
    {
        foreach (PropKind k in PropManager.AllKinds)
        {
            PropKind kind = k;
            string cat = k switch
            {
                PropKind.Plank or PropKind.Ramp => "Construction",
                PropKind.Barrel or PropKind.BigCrate => "Containers",
                _ => "Shapes",
            };
            Register(new SpawnEntry($"prop_{k.ToString().ToLowerInvariant()}", PropManager.DisplayName(k), MenuTab.Spawnlists, cat,
                Icons.For(k), kind, (g, pos, yaw) => g.Props.Spawn(kind, pos, yaw).Go.Name, PropManager.HalfHeightOf(k)));
        }
        Ragdoll.Register();
        VehicleSystem.Register();
    }

    public static void Register(SpawnEntry e)
    {
        _entries.RemoveAll(x => x.Id == e.Id);
        _entries.Add(e);
    }

    public static SpawnEntry? Find(string id) => _entries.FirstOrDefault(e => e.Id == id);

    public static IEnumerable<string> Categories(MenuTab tab) => _entries.Where(e => e.Tab == tab).Select(e => e.Category).Distinct();
}
