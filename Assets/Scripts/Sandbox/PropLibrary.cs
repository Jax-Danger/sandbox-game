using Prowl.Runtime;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>
/// Stage 6: the prop prefabs the game spawns from, editable in the editor. Put it on the GameObject with SandboxHost
/// and fill Props with Assets/Prefabs/Props/*.prefab; each prefab's PropDefinition says which kind it is. Kinds with
/// no prefab here (and the standalone exe, which has no asset database) fall back to the code-built props.
/// </summary>
public sealed class PropLibrary : Component
{
    public List<PrefabAsset> Props = [];
}
