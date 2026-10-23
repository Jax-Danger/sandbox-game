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

/// <summary>v1.0 bodies kept Jitter's default damping (0.002 linear, 0.005 angular per step); engine 1.0 zeroes it
/// unless set, which left floating props bobbing and ragdolls/props spinning longer. Restores the v1.0 feel.</summary>
public static class BodyUtil
{
    public const float V10LinearDamping = 0.002f, V10AngularDamping = 0.005f;

    public static Rigidbody3D AddBody(this GameObject go)
    {
        Rigidbody3D b = go.AddComponent<Rigidbody3D>();
        b.LinearDamping = V10LinearDamping;
        b.AngularDamping = V10AngularDamping;
        return b;
    }
}
