#if PROWL_EDITOR
using System.Reflection;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>
/// Stage 3 (editor only): bakes the procedural meshes (every public static parameterless Mesh factory on the
/// classes below) into Assets/Meshes/&lt;Class&gt;/&lt;Name&gt;.mesh so prefabs and scenes can reference real assets.
/// Run from the CLI: <c>Library/prowl eval "return Sandbox.MeshBaker.BakeAll();"</c>. Existing files are kept unless overwrite is true.
/// </summary>
public static class MeshBaker
{
    public static readonly Type[] Sources = [typeof(PropMeshes), typeof(VehicleModels), typeof(Meshes)];

    public static IEnumerable<(string Folder, string Name, MethodInfo Factory)> Factories() =>
        Sources.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(Mesh) && m.GetParameters().Length == 0 && !m.IsSpecialName)
            .Select(m => (t.Name, m.Name, m)));

    public static string BakeAll(bool overwrite = false)
    {
        var db = Prowl.Editor.EditorAssetBackend.Instance ?? throw new InvalidOperationException("No editor asset database.");
        var report = new List<string>();
        int ok = 0, skipped = 0;
        foreach (var (folder, name, factory) in Factories())
        {
            try
            {
                string rel = $"Meshes/{folder}/{name}.mesh";
                if (!overwrite && File.Exists(Path.Combine(Prowl.Editor.Projects.Project.Current!.RootPath, "Assets", rel))) { skipped++; continue; }
                // Bake a copy: the factories cache their meshes, and the asset database takes ownership of what it saves.
                var source = (Mesh)factory.Invoke(null, null)!;
                var copy = Prowl.Echo.Serializer.Deserialize<Mesh>(Prowl.Echo.Serializer.Serialize(typeof(object), source))!;
                copy.Name = name;
                db.CreateAsset(copy, rel);
                ok++;
            }
            catch (Exception e) { report.Add($"{folder}.{name}: {e.GetBaseException().Message}"); }
        }
        return $"baked {ok}, skipped {skipped} (exist), failed {report.Count}" + (report.Count > 0 ? "\n" + string.Join("\n", report) : "");
    }
}
#endif
