// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.Runtime;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>Material factory: every material starts with a white albedo so untextured parts show their colour, not the engine's checker.</summary>
public static class Mats
{
    private static Texture2D? _white;
    /// <summary>Sets a material's texture tiling the engine-1.0 way: stores _Tiling (for reference) and scales the renderer's mesh UVs.</summary>
    public static void Tile(MeshRenderer r, Material m, Prowl.Vector.Float2 tiling)
    {
        m.SetVector("_Tiling", tiling);
        if (r.Mesh is { } mesh) r.Mesh = Meshes.ScaledUv(mesh, tiling);
    }

    public static Material New(Shader shader)
    {
        var m = new Material(shader);
        if (!Application.IsHeadless)
            try
            {
                m.SetTexture("_MainTex", _white ??= Texture2D.LoadDefault(DefaultTexture.White));
                // Explicit matte surface (R = occlusion where 0 = lit, G = roughness, B = metallic).
                m.SetTexture("_SurfaceTex", ProcTex.Surface(0.75f, 0f));
            }
            catch { }
        return m;
    }
}
