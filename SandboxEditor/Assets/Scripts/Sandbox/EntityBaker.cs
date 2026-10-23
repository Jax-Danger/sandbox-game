#if PROWL_EDITOR
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Stage 4b (editor only, run while the editor is PLAYING): spawns every vehicle, NPC team, the plain ragdoll and every
/// weapon's world model with the game's own code, turns their procedural meshes/materials into assets (deduped by
/// content) and saves each as a prefab under Assets/Prefabs/{Vehicles,Npcs,Ragdolls,Weapons}.
/// Prefabs cannot be created during play, so the hierarchies are serialized here and written by <see cref="FinishPrefabs"/>.
/// CLI: play --action Start; eval "return Sandbox.EntityBaker.BakeAll();"; play --action Stop; eval "return Sandbox.EntityBaker.FinishPrefabs();".
/// </summary>
public static class EntityBaker
{
    public static string BakeAll(bool overwrite = true)
    {
        var g = SandboxHost.Current ?? throw new InvalidOperationException("Start play mode first.");
        Pending.Clear();
        EntityLibrary.Enabled = false; // bake from the code-built models, never from earlier prefabs
        try { return BakeAllCore(g, overwrite); } finally { EntityLibrary.Enabled = true; }
    }

    private static string BakeAllCore(SandboxGame g, bool overwrite)
    {
        var baker = new AssetBaking();
        var log = new List<string>();
        Float3 at = new(0f, 200f, 0f); // out of sight, above the map
        void Try(string name, Action a) { try { a(); } catch (Exception e) { log.Add($"{name}: {e.GetBaseException().Message}"); } }

        foreach (string kind in new[] { "buggy", "jeep", "airboat" })
            Try(kind, () =>
            {
                Buggy b = g.Vehicles.SpawnBuggy(at, 0f, recordUndo: false, kind: kind);
                b.Body.Enabled = false;
                Save(baker, b.Go, "Vehicles", Buggy.Title(kind), "Vehicle", kind, overwrite, log);
                g.Vehicles.Remove(b);
            });

        foreach (NpcTeam team in Enum.GetValues<NpcTeam>())
            Try(team.ToString(), () =>
            {
                Ragdoll rd = Ragdoll.Spawn(g, at, 0f, recordUndo: false, team: team);
                var root = new GameObject(team == NpcTeam.Dummy ? "Ragdoll" : $"Npc {team}");
                g.World.Add(root);
                root.Transform.Position = at;
                foreach (Prop l in rd.Limbs) { l.Body.Enabled = false; l.Go.SetParent(root, true); }
                Save(baker, root, team == NpcTeam.Dummy ? "Ragdolls" : "Npcs", root.Name, team == NpcTeam.Dummy ? "Ragdoll" : "Npc", team.ToString(), overwrite, log);
                Ragdoll.All.Remove(rd);
                root.Destroy();
            });

        foreach (var w in g.Weapons.All)
            Try(w.Id, () =>
            {
                var root = new GameObject(w.Id);
                g.World.Add(root);
                root.Transform.Position = at;
                if (WorldWeapons.Attach(g, root, w.Id, Float3.Zero, Float3.Zero) is null) { root.Destroy(); return; }
                Save(baker, root, "Weapons", w.Id, "Weapon", w.Id, overwrite, log);
                root.Destroy();
            });

        return $"entity prefabs {baker.Prefabs}, meshes {baker.Meshes}, materials {baker.Materials}, failed {log.Count}" + (log.Count > 0 ? "\n" + string.Join("\n", log) : "");
    }

    private static void Save(AssetBaking baker, GameObject root, string folder, string name, string category, string id, bool overwrite, List<string> log)
    {
        string clean = AssetBaking.Clean(name);
        Float3 pos = root.Transform.Position;
        root.Transform.Position = Float3.Zero;
        var def = root.GetComponent<EntityDefinition>() ?? root.AddComponent<EntityDefinition>();
        def.Category = category; def.Id = id;
        root.Name = name;
        baker.BakeRenderers(root, $"Meshes/Entities/{clean}", "Materials/Entities");
        Pending.Add(($"Prefabs/{folder}/{clean}.prefab", Prowl.Echo.Serializer.Serialize(typeof(GameObject), root)));
        baker.Prefabs++;
        root.Transform.Position = pos;
    }

    /// <summary>Edit mode: lays every entity prefab out in the open scene (vehicles, then NPCs/ragdoll, then weapons).</summary>
    public static string Showcase()
    {
        var db = Prowl.Editor.EditorAssetBackend.Instance!;
        Scene s = Scene.Current;
        string assets = Prowl.Editor.Projects.Project.Current!.AssetsPath;
        int n = 0;
        foreach (var (dir, z, step, y) in new[] { ("Vehicles", 6f, 5f, 0.6f), ("Npcs", 0f, 1.6f, 0f), ("Ragdolls", 0f, 1.6f, 0f), ("Weapons", -3f, 1.1f, 1f) })
        {
            float x = dir == "Ragdolls" ? 2.4f : -6f;
            foreach (string f in Directory.GetFiles(Path.Combine(assets, "Prefabs", dir), "*.prefab").Order())
            {
                var p = AssetDatabase.Load<PrefabAsset>(db.PathToGuid($"Prefabs/{dir}/{Path.GetFileName(f)}"));
                if (p is null) continue;
                GameObject.Instantiate(p, s, new Float3(x, y, z), Quaternion.Identity);
                x += step; n++;
            }
        }
        Prowl.Editor.GUI.SceneView.EditorSceneManager.MarkDirty();
        return $"placed {n}";
    }

    private static readonly List<(string Path, Prowl.Echo.EchoObject Data)> Pending = [];

    /// <summary>Edit mode: writes the prefabs BakeAll captured during play.</summary>
    public static string FinishPrefabs(bool overwrite = true)
    {
        if (Application.IsPlaying) return "stop play mode first";
        var scene = new Scene();
        int ok = 0; var log = new List<string>();
        try
        {
            foreach (var (path, data) in Pending)
                try
                {
                    var go = Prowl.Echo.Serializer.Deserialize<GameObject>(data)!;
                    scene.Add(go);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Prowl.Editor.Projects.Project.Current!.AssetsPath, path))!);
                    if (Prowl.Editor.Prefabs.PrefabUtility.SaveAsPrefabAsset(go, path, overwrite)) ok++; else log.Add($"{path}: not saved");
                }
                catch (Exception e) { log.Add($"{path}: {e.GetBaseException().Message}"); }
        }
        finally { scene.Dispose(); }
        Pending.Clear();
        return $"prefabs written {ok}, failed {log.Count}" + (log.Count > 0 ? "\n" + string.Join("\n", log) : "");
    }
}

/// <summary>Turns runtime (procedural) meshes and materials under a hierarchy into assets, deduped by content.</summary>
public sealed class AssetBaking
{
    private readonly Dictionary<string, Mesh> _meshByHash = [];
    private readonly Dictionary<string, Material> _matByHash = [];
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
    public int Meshes => _meshByHash.Count;
    public int Materials => _matByHash.Count;
    public int Prefabs;

    public static string Clean(string name) => string.Concat((string.IsNullOrWhiteSpace(name) ? "Asset" : name).Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_'));

    private string Unique(string folder, string name, string ext)
    {
        string rel = $"{folder}/{Clean(name)}{ext}";
        for (int i = 2; !_used.Add(rel); i++) rel = $"{folder}/{Clean(name)}_{i}{ext}";
        return rel;
    }

    public void BakeRenderers(GameObject root, string meshFolder, string matFolder)
    {
        var db = Prowl.Editor.EditorAssetBackend.Instance ?? throw new InvalidOperationException("No editor asset database.");
        var all = new List<GameObject> { root };
        for (int i = 0; i < all.Count; i++) all.AddRange(all[i].Children);
        foreach (var r in all.SelectMany(go => go.GetComponents<MeshRenderer>()).ToList())
        {
            if (r.Mesh is { } m && m.AssetID == Guid.Empty)
            {
                string h = Hash(m);
                if (!_meshByHash.TryGetValue(h, out var baked))
                {
                    baked = Clone(m); baked.Name = string.IsNullOrEmpty(m.Name) || m.Name.StartsWith("NewMesh") ? r.GameObject.Name : m.Name;
                    db.CreateAsset(baked, Unique(meshFolder, baked.Name, ".mesh"));
                    _meshByHash[h] = baked;
                }
                r.Mesh = baked;
            }
            if (r.Material is { } mat && mat.AssetID == Guid.Empty)
            {
                string h = Hash(mat);
                if (!_matByHash.TryGetValue(h, out var baked))
                {
                    baked = Clone(mat);
                    BakeTextures(mat, baked);
                    db.CreateAsset(baked, Unique(matFolder, r.GameObject.Name, ".mat"));
                    _matByHash[h] = baked;
                }
                r.Material = baked;
            }
        }
    }

    private static readonly Dictionary<string, Texture2D> _texByKey = [];
    public static readonly string[] TextureSlots = ["_MainTex", "_SurfaceTex", "_EmissionTex"];

    /// <summary>Replaces the clone's inline procedural textures with PNG assets under Assets/Textures/Proc (one per ProcTex key,
    /// imported linear like the game's own upload). Returns how many slots now point at assets.</summary>
    public static int BakeTextures(Material original, Material clone)
    {
        int n = 0;
        foreach (string slot in TextureSlots)
            if (original.GetTexture(slot) is { } t && t.AssetID == Guid.Empty && ProcTex.TryGetSource(t, out var src) && TextureAsset(src) is { } asset)
            { clone.SetTexture(slot, asset); n++; }
        return n;
    }

    public static Texture2D? TextureAsset(ProcTex.Source src)
    {
        if (_texByKey.TryGetValue(src.Key, out var have) && have.IsValid()) return have;
        var db = Prowl.Editor.EditorAssetBackend.Instance!;
        string rel = $"Textures/Proc/{Clean(src.Key)}.png";
        string abs = Path.Combine(Prowl.Editor.Projects.Project.Current!.AssetsPath, rel);
        Guid guid = db.PathToGuid(rel);
        if (guid == Guid.Empty)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            bool alpha = false;
            for (int i = 3; i < src.Rgba.Length; i += 4) if (src.Rgba[i] != 255) { alpha = true; break; }
            PngWriter.Write(abs, src.Rgba, src.W, src.H, bottomUp: false, alpha);
            string importer = Prowl.Editor.EditorRegistries.GetImporterTypeName(".png");
            var imp = Prowl.Editor.EditorRegistries.CreateImporterByName(importer);
            var settings = imp?.DefaultSettings() ?? Prowl.Echo.EchoObject.NewCompound();
            settings["srgb"] = new Prowl.Echo.EchoObject(Environment.GetEnvironmentVariable("SANDBOX_SRGBTEX") == "1");
            Prowl.Editor.MetaFile.Write(Prowl.Editor.MetaFile.GetMetaPath(abs), Prowl.Editor.MetaFile.CreateNew(importer, imp?.Version ?? 1, settings));
            guid = db.ImportFile(rel);
        }
        var tex = AssetDatabase.Load<Texture2D>(guid);
        if (tex is not null) _texByKey[src.Key] = tex;
        return tex;
    }

    private static string Hash(object asset) => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
        System.Text.Encoding.UTF8.GetBytes(Prowl.Echo.Serializer.Serialize(typeof(object), asset).WriteToString())));

    private static T Clone<T>(T asset) where T : class =>
        Prowl.Echo.Serializer.Deserialize<T>(Prowl.Echo.Serializer.Serialize(typeof(object), asset))!;
}
#endif
