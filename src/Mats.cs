using Prowl.Runtime;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>Material factory: every material starts with a white albedo so untextured parts show their colour, not the engine's checker.</summary>
public static class Mats
{
    private static Texture2D? _white;
    public static Material New(Shader shader)
    {
        var m = new Material(shader);
        if (!Application.IsHeadless)
            try { m.SetTexture("_MainTex", _white ??= Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
        return m;
    }
}
