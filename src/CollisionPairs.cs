using Prowl.Runtime;

namespace Sandbox;

/// <summary>
/// Wraps the engine's ignore-collision pairs. The engine keeps them in a static set holding strong references
/// and never prunes destroyed bodies, so every ragdoll/no-collide/ball-socket leaked its whole GameObject
/// (found by the 300 s soak: managed heap 24 → 190 MB with a flat object count). We track pairs per body and
/// release them before a body is removed.
/// </summary>
public static class CollisionPairs
{
    private static readonly Dictionary<Rigidbody3D, HashSet<Rigidbody3D>> _pairs = new(ReferenceEqualityComparer.Instance);

    public static void Ignore(Rigidbody3D a, Rigidbody3D b)
    {
        if (a is null || b is null || ReferenceEquals(a, b)) return;
        PhysicsWorld.IgnoreCollisionBetween(a, b);
        Link(a, b); Link(b, a);
    }

    public static void Enable(Rigidbody3D a, Rigidbody3D b)
    {
        if (a is null || b is null) return;
        PhysicsWorld.EnableCollisionBetween(a, b);
        Unlink(a, b); Unlink(b, a);
    }

    /// <summary>Drop every pair involving this body. Call while the body is still valid (before scene removal).</summary>
    public static void Release(Rigidbody3D? body)
    {
        if (body is null || !_pairs.Remove(body, out var others)) return;
        foreach (Rigidbody3D o in others)
        {
            try { PhysicsWorld.EnableCollisionBetween(body, o); } catch { }
            Unlink(o, body);
        }
    }

    public static int TrackedBodies => _pairs.Count;

    /// <summary>Engine-side pair count (read-only reflection, for the soak and self-test).</summary>
    public static int EnginePairs
    {
        get
        {
            var f = typeof(PhysicsWorld).Assembly.GetType("Prowl.Runtime.LayerFilter")?.GetField("_ignore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object? set = f?.GetValue(null);
            return set?.GetType().GetProperty("Count")?.GetValue(set) is int n ? n : -1;
        }
    }

    private static void Link(Rigidbody3D a, Rigidbody3D b)
    {
        if (!_pairs.TryGetValue(a, out var s)) _pairs[a] = s = new(ReferenceEqualityComparer.Instance);
        s.Add(b);
    }

    private static void Unlink(Rigidbody3D a, Rigidbody3D b)
    {
        if (_pairs.TryGetValue(a, out var s) && s.Remove(b) && s.Count == 0) _pairs.Remove(a);
    }
}
