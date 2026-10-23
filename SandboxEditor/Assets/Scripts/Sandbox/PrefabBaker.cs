#if PROWL_EDITOR
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Stage 4 (editor only): builds every spawnable prop kind exactly as PropManager.Spawn does in game, then saves it
/// as Assets/Prefabs/Props/&lt;Kind&gt;.prefab with its mesh (Assets/Meshes/Props) and material (Assets/Materials/Props)
/// as real assets. CLI: <c>eval "return Sandbox.PrefabBaker.BakeProps();"</c>. Existing prefabs are kept unless overwrite.
/// </summary>
public static class PrefabBaker
{
    public static string BakeProps(bool overwrite = false)
    {
        var db = Prowl.Editor.EditorAssetBackend.Instance ?? throw new InvalidOperationException("No editor asset database.");
        string assets = Prowl.Editor.Projects.Project.Current!.AssetsPath;
        var log = new List<string>();
        int ok = 0, skipped = 0;
        var scene = new Scene();
        try
        {
            var pm = new PropManager(scene, Shader.LoadDefault(DefaultShader.Lit));
            foreach (PropKind kind in PropManager.AllKinds)
            {
                string prefab = $"Prefabs/Props/{kind}.prefab";
                if (!overwrite && File.Exists(Path.Combine(assets, prefab))) { skipped++; continue; }
                try
                {
                    Prop prop = pm.Spawn(kind, Float3.Zero, 0f, recordUndo: false);
                    GameObject go = prop.Go;
                    go.Name = kind.ToString();
                    go.Transform.Position = Float3.Zero;
                    var r = go.GetComponent<MeshRenderer>()!;

                    var mesh = Clone(r.Mesh!); mesh.Name = kind.ToString();
                    db.CreateAsset(mesh, $"Meshes/Props/{kind}.mesh");
                    r.Mesh = mesh;
                    var mat = Clone(prop.Material); mat.Name = kind.ToString();
                    db.CreateAsset(mat, $"Materials/Props/{kind}.mat");
                    r.Material = mat;

                    var def = go.AddComponent<PropDefinition>();
                    def.Kind = kind; def.Surface = prop.Surface; def.HalfHeight = prop.HalfHeight;

                    if (Prowl.Editor.Prefabs.PrefabUtility.SaveAsPrefabAsset(go, prefab, overwrite)) ok++;
                    else log.Add($"{kind}: SaveAsPrefabAsset returned false");
                }
                catch (Exception e) { log.Add($"{kind}: {e.GetBaseException().Message}"); }
            }
        }
        finally { scene.Dispose(); }
        return $"prefabs {ok}, skipped {skipped} (exist), failed {log.Count}" + (log.Count > 0 ? "\n" + string.Join("\n", log) : "");
    }

    private static T Clone<T>(T asset) where T : class =>
        Prowl.Echo.Serializer.Deserialize<T>(Prowl.Echo.Serializer.Serialize(typeof(object), asset))!;
}
#endif
