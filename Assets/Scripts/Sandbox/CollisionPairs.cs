// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
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
        a.GameObject.Scene.Physics.IgnoreCollisionBetween(a, b);
        Link(a, b); Link(b, a);
    }

    public static void Enable(Rigidbody3D a, Rigidbody3D b)
    {
        if (a is null || b is null) return;
        a.GameObject.Scene.Physics.EnableCollisionBetween(a, b);
        Unlink(a, b); Unlink(b, a);
    }

    /// <summary>Drop every pair involving this body. Call while the body is still valid (before scene removal).</summary>
    public static void Release(Rigidbody3D? body)
    {
        if (body is null || !_pairs.Remove(body, out var others)) return;
        foreach (Rigidbody3D o in others)
        {
            try { body.GameObject.Scene.Physics.EnableCollisionBetween(body, o); } catch { }
            Unlink(o, body);
        }
    }

    public static int TrackedBodies => _pairs.Count;

    /// <summary>Engine-side pair count (read-only reflection, for the soak and self-test).</summary>
    public static int EnginePairs
    {
        get
        {
            // Engine 1.0: the ignore set lives on each scene's PhysicsWorld (_layerFilter._ignore).
            const System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            PhysicsWorld? world = Prowl.Runtime.Resources.Scene.Current?.Physics;
            object? filter = world is null ? null : typeof(PhysicsWorld).GetField("_layerFilter", NP)?.GetValue(world);
            object? set = filter?.GetType().GetField("_ignore", NP)?.GetValue(filter);
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

/// <summary>
/// The engine's PhysicsConstraint creates a temporary static Jitter body whenever it (re)builds without a connected
/// body (on AddComponent and on property setters before ConnectedBody is set) and never removes it. Every joint
/// leaked bodies (soak: 23k after 90 s, managed heap growing ~0.25 MB/s). This sweeps bodies that are provably
/// orphaned: untagged (not a Rigidbody3D or a layer static body), static, no shapes, no constraints, not NullBody.
/// </summary>
public static class PhysicsSweep
{
    public static int Swept { get; private set; }

    public static int Run(Prowl.Runtime.Resources.Scene scene)
    {
        var world = scene.Physics.World;
        if (world is null) return 0;
        List<Jitter2.Dynamics.RigidBody>? dead = null;
        foreach (var b in world.RigidBodies)
            if (b.Tag is null && !ReferenceEquals(b, world.NullBody) && b.MotionType == Jitter2.Dynamics.MotionType.Static
                && b.Shapes.Count == 0 && b.Constraints.Count == 0)
                (dead ??= []).Add(b);
        if (dead is null) return 0;
        foreach (var b in dead) world.Remove(b);
        Swept += dead.Count;
        return dead.Count;
    }
}
