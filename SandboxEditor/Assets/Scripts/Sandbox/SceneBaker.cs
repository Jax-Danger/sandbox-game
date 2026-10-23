#if PROWL_EDITOR
using Prowl.Runtime;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>
/// Stage 5 (editor only): builds a map with the game's MapBuilder into the editor's open scene and turns every
/// procedural mesh and material it used into assets under Assets/Meshes/Maps/&lt;map&gt; and Assets/Materials/Maps/&lt;map&gt;
/// (shared instances are baked once). Then save the scene with the CLI: scene --action SaveAs --path Scenes/Maps/&lt;Map&gt;.scene.
/// CLI: <c>scene --action New --force</c>, then <c>eval "return Sandbox.SceneBaker.BakeMap(\"flatgrass\");"</c>.
/// </summary>
public static class SceneBaker
{
    public static string BakeMap(string map)
    {
        var db = Prowl.Editor.EditorAssetBackend.Instance ?? throw new InvalidOperationException("No editor asset database.");
        Scene? scene = Scene.Current;
        if (!scene.IsValid()) throw new InvalidOperationException("No open scene.");
        Shader shader = Shader.LoadDefault(DefaultShader.Lit);
        int before = scene.AllObjects.Count();
        var sky = scene.Environment.SkyMaterial; // the editor's default sky is an asset; a runtime-made one would not save
        SandboxGame.ApplyAtmosphere(scene);
        if (sky is not null) scene.Environment.SkyMaterial = sky;
        MapBuilder.CreateSun(scene);
        // Keep objects individual (editable) instead of the game's runtime static batches, which also bloat the assets.
        string? prevBatch = Environment.GetEnvironmentVariable("SANDBOX_BATCH");
        Environment.SetEnvironmentVariable("SANDBOX_BATCH", "0");
        string? err;
        try { err = MapBuilder.Load(scene, shader, map, null); }
        finally { Environment.SetEnvironmentVariable("SANDBOX_BATCH", prevBatch); }
        if (err is not null) return "map error: " + err;

        MapBuilder.CaptureInfo(scene, map);
        var spawn = new GameObject("SpawnPoint");
        spawn.Transform.Position = MapBuilder.SpawnPoint;
        scene.Add(spawn);

        var meshes = new Dictionary<Mesh, Mesh>(ReferenceEqualityComparer.Instance);
        var mats = new Dictionary<Material, Material>(ReferenceEqualityComparer.Instance);
        // Identical procedural meshes/materials (built separately per object) are baked once, by content.
        var meshByHash = new Dictionary<string, Mesh>();
        var matByHash = new Dictionary<string, Material>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Unique(string folder, string name, string ext)
        {
            string clean = string.Concat((string.IsNullOrWhiteSpace(name) ? "Asset" : name).Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_'));
            string rel = $"{folder}/{clean}{ext}";
            for (int i = 2; !usedNames.Add(rel); i++) rel = $"{folder}/{clean}_{i}{ext}";
            return rel;
        }

        foreach (var r in scene.AllObjects.SelectMany(g => g.GetComponents<MeshRenderer>()).ToList())
        {
            if (r.Mesh is { } m && m.AssetID == Guid.Empty)
            {
                if (!meshes.TryGetValue(m, out var baked) && meshByHash.TryGetValue(Hash(m), out baked)) meshes[m] = baked;
                if (baked is null)
                {
                    string label = string.IsNullOrEmpty(m.Name) || m.Name.StartsWith("NewMesh") ? r.GameObject.Name : m.Name;
                    baked = Clone(m); baked.Name = label;
                    db.CreateAsset(baked, Unique($"Meshes/Maps/{map}", label, ".mesh"));
                    meshes[m] = baked; meshByHash[Hash(m)] = baked;
                }
                r.Mesh = baked;
            }
            if (r.Material is { } mat && mat.AssetID == Guid.Empty)
            {
                if (!mats.TryGetValue(mat, out var baked) && matByHash.TryGetValue(Hash(mat), out baked)) mats[mat] = baked;
                if (baked is null)
                {
                    baked = Clone(mat);
                    db.CreateAsset(baked, Unique($"Materials/Maps/{map}", r.GameObject.Name, ".mat"));
                    mats[mat] = baked; matByHash[Hash(mat)] = baked;
                }
                r.Material = baked;
            }
        }
        Prowl.Editor.GUI.SceneView.EditorSceneManager.MarkDirty();
        return $"{map}: {scene.AllObjects.Count() - before} objects, {meshByHash.Count} meshes, {matByHash.Count} materials baked ({meshes.Count}/{mats.Count} instances)";
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, string> _hashes = new();
    private static string Hash(object asset) => _hashes.GetValue(asset, a => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
        System.Text.Encoding.UTF8.GetBytes(Prowl.Echo.Serializer.Serialize(typeof(object), a).WriteToString()))));

    private static T Clone<T>(T asset) where T : class =>
        Prowl.Echo.Serializer.Deserialize<T>(Prowl.Echo.Serializer.Serialize(typeof(object), asset))!;
}
#endif
