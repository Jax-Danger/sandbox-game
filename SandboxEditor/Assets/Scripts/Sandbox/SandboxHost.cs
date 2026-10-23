using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>
/// Editor bootstrap (stage 2b): put this on a GameObject in the Main scene. On Play it creates the code-built
/// SandboxGame world (which becomes the current scene) and adds a driver that forwards the Game loop hooks
/// the standalone exe gets from Game.Run (EndUpdate, BeginRender, OnGui).
/// </summary>
public sealed class SandboxHost : Component
{
    public static SandboxGame? Current { get; private set; }

    internal static void ClearForStop() => Current = null;

    public override void Start()
    {
        if (!Application.IsPlaying || Current is not null) return;
        DayNightCycle.ResetStatics();
        MapBuilder.BakedScenes.Clear();
        if (GameObject.GetComponent<MapLibrary>() is { } maps)
            foreach (var e in maps.Maps)
                if (e is { Scene: { } sa } && sa.IsValid() && e.Id != "") MapBuilder.BakedScenes[e.Id] = sa;
        var game = new SandboxGame();
        Current = game;
        game.Initialize();
        if (GameObject.GetComponent<PropLibrary>() is { } lib)
            foreach (var prefab in lib.Props)
                if (prefab.IsValid() && GameObject.InstantiateDetached(prefab) is { } probe)
                {
                    // Read the kind off the prefab's PropDefinition (an unplaced instance, discarded right away).
                    if (probe.GetComponent<PropDefinition>() is { } def) game.Props.Prefabs[def.Kind] = prefab;
                    probe.Destroy();
                }
        var go = new GameObject("~SandboxDriver");
        go.AddComponent<SandboxDriver>().Game = game;
        game.World.Add(go);
    }
}

/// <summary>Lives in the SandboxGame world and drives it once per frame while the editor is playing.</summary>
public sealed class SandboxDriver : Component
{
    [Prowl.Echo.SerializeIgnore] public SandboxGame? Game;

    public override void LateUpdate()
    {
        if (Game is null) return;
        Game.EndUpdate();
        Game.BeginRender();
    }

    public override void OnGui(Paper paper) => Game?.OnGui(null, paper);

    public override void OnDisable() { if (SandboxHost.Current == Game) SandboxHost.ClearForStop(); }
}
