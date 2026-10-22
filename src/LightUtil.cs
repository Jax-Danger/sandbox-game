using Prowl.Runtime;

namespace Sandbox;

/// <summary>Engine 1.0 merged the per-type light components into one <see cref="Light"/> with a <see cref="LightType"/>.</summary>
public static class LightUtil
{
    public static Light AddLight(this GameObject go, LightType type)
    {
        Light l = go.AddComponent<Light>();
        l.Type = type;
        return l;
    }
}
