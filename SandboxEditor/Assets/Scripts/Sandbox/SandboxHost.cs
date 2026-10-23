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
        var game = new SandboxGame();
        Current = game;
        game.Initialize();
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
