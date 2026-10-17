using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Water volumes (axis-aligned boxes with a flat surface) and buoyancy. Every physics step each prop and vehicle
/// inside a volume gets an upward force proportional to how deep it is submerged, scaled by how well its surface
/// material floats (wood floats high, metal and concrete sink), plus water drag so things settle instead of bobbing forever.
/// </summary>
public static class Water
{
    public sealed record Volume(Float3 Center, Float3 Half)
    {
        public float Surface => Center.Y + Half.Y;
        public bool ContainsXZ(Float3 p) => MathF.Abs(p.X - Center.X) <= Half.X && MathF.Abs(p.Z - Center.Z) <= Half.Z && p.Y <= Surface + 2f && p.Y >= Center.Y - Half.Y;
    }

    public static readonly List<Volume> Volumes = [];
    public static void Clear() => Volumes.Clear();
    public static Volume Add(Float3 center, Float3 half) { var v = new Volume(center, half); Volumes.Add(v); return v; }

    /// <summary>Buoyancy relative to weight when fully submerged (&gt;1 floats).</summary>
    public static float Floatiness(string surface) => surface switch
    {
        "Wood" => 2.2f, "Rubber" => 1.8f, "Plastic" => 1.6f, "Ice" => 1.4f, "Metal" => 0.55f, "Concrete" => 0.45f, _ => 1.4f,
    };

    public static Volume? At(Float3 p)
    {
        foreach (Volume v in Volumes) if (v.ContainsXZ(p)) return v;
        return null;
    }

    /// <summary>Surface height at a point, or null when not over water.</summary>
    public static float? SurfaceAt(Float3 p) => At(p)?.Surface;

    /// <summary>Applies buoyancy + drag to one body; returns the submerged fraction (0..1).</summary>
    public static float Apply(Rigidbody3D body, Float3 pos, float halfHeight, float floatiness, float dragScale = 1f)
    {
        if (Volumes.Count == 0 || !body.Enabled) return 0f;
        Volume? v = At(pos);
        if (v is null) return 0f;
        float h = MathF.Max(halfHeight, 0.05f);
        float sub = Math.Clamp((v.Surface - (pos.Y - h)) / (2f * h), 0f, 1f);
        if (sub <= 0f) return 0f;
        float g = 9.81f;
        body.SetActive(true);
        Float3 vel = body.LinearVelocity;
        body.AddForce(new Float3(0f, sub * floatiness * body.Mass * g, 0f));
        // Linear drag (stronger vertically to damp bobbing) and angular drag.
        body.AddForce(new Float3(-vel.X * 1.2f, -vel.Y * 3.0f, -vel.Z * 1.2f) * (body.Mass * sub * dragScale));
        body.AngularVelocity *= 1f - Math.Clamp(1.5f * sub * Time.FixedDeltaTime * dragScale, 0f, 0.5f);
        return sub;
    }
}

/// <summary>Ticks buoyancy for every prop and vehicle once per physics step.</summary>
public sealed class WaterSimulator : MonoBehaviour
{
    public SandboxGame? Game;
    private int _sweep;

    public override void FixedUpdate()
    {
        if (Game is null) return;
        PhysicsGuard.Clamp(Game);
        if (++_sweep % 120 == 0) PhysicsSweep.Run(Game.World);
        if (Water.Volumes.Count == 0) return;
        foreach (Prop p in Game.Props.Props)
        {
            if (p.Frozen) continue;
            Water.Apply(p.Body, p.Go.Transform.Position, p.HalfHeight, Water.Floatiness(p.Surface));
        }
        foreach (Buggy b in Game.Vehicles.All)
            // Hulls float; the airboat rides high and glides (low drag so the fan can push it).
            Water.Apply(b.Body, b.Go.Transform.Position, 0.45f, b.IsAirboat ? 3.2f : 1.3f, b.IsAirboat ? 0.35f : 1.5f);
    }
}

/// <summary>
/// Safety net against physics explosions (extreme mass ratios, stacked constraints): caps linear speed at
/// <see cref="MaxSpeed"/> and spin at <see cref="MaxSpin"/>, and zeroes any NaN velocity before it spreads.
/// </summary>
public static class PhysicsGuard
{
    public const float MaxSpeed = 120f, MaxSpin = 60f;
    public static int Clamped { get; private set; }

    public static bool Fix(Rigidbody3D b)
    {
        if (b is null || b.GameObject is not { } go || go.Scene is null || !b.Enabled || b.MotionType != Jitter2.Dynamics.MotionType.Dynamic) return false;
        Float3 v = b.LinearVelocity, w = b.AngularVelocity;
        bool hit = false;
        if (float.IsNaN(v.X + v.Y + v.Z) || float.IsInfinity(v.X + v.Y + v.Z)) { b.LinearVelocity = Float3.Zero; hit = true; }
        else if (Float3.Length(v) > MaxSpeed) { b.LinearVelocity = v * (MaxSpeed / Float3.Length(v)); hit = true; }
        if (float.IsNaN(w.X + w.Y + w.Z) || float.IsInfinity(w.X + w.Y + w.Z)) { b.AngularVelocity = Float3.Zero; hit = true; }
        else if (Float3.Length(w) > MaxSpin) { b.AngularVelocity = w * (MaxSpin / Float3.Length(w)); hit = true; }
        if (hit) Clamped++;
        return hit;
    }

    public static int Removed { get; private set; }

    private static readonly System.Reflection.FieldInfo? BodyField =
        typeof(Rigidbody3D).GetField("_body", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    /// <summary>
    /// True when the solver left the body's pose NaN/huge. The Transform setter silently rejects NaN, so the
    /// Jitter body itself is checked (internal engine field; read-only).
    /// </summary>
    /// <summary>True when the component is still alive but its Jitter body is gone (removed this frame).</summary>
    public static bool Detached(Rigidbody3D rb)
    {
        if (rb.GameObject is not { Scene: not null } || BodyField?.GetValue(rb) is not Jitter2.Dynamics.RigidBody jb) return true;
        try { _ = jb.Position; return false; } catch (NullReferenceException) { return true; }
    }

    public static bool Corrupt(Rigidbody3D? rb)
    {
        if (rb is null || !rb.Enabled || rb.GameObject is not { Scene: not null }) return false;
        if (BodyField?.GetValue(rb) is not Jitter2.Dynamics.RigidBody jb) return false;
        try
        {
            var p = jb.Position; var q = jb.Orientation;
            return !float.IsFinite(p.X + p.Y + p.Z) || !float.IsFinite(q.X + q.Y + q.Z + q.W) || MathF.Abs(p.X) + MathF.Abs(p.Y) + MathF.Abs(p.Z) > 1e6f;
        }
        catch (NullReferenceException) { return false; } // body already detached from the Jitter world this frame
    }

    /// <summary>
    /// Runs after each physics step. Caps runaway speeds and removes any body whose pose went NaN/huge
    /// (degenerate constraint stacks can do this), because Jitter's next broadphase pass throws on NaN
    /// (Math.Sign in SupportMap) on a worker thread and kills the process.
    /// </summary>
    public static void Clamp(SandboxGame g)
    {
        List<Prop>? bad = null;
        foreach (Prop p in g.Props.Props)
        {
            if (!p.Alive) continue;
            if (Corrupt(p.Body)) (bad ??= []).Add(p);
            else Fix(p.Body);
        }
        if (bad is not null)
        {
            foreach (Prop p in bad) { try { g.Props.Remove(p); } catch { g.World.Remove(p.Go); } Removed++; }
            g.Notify($"Removed {bad.Count} unstable prop{(bad.Count == 1 ? "" : "s")}", ToastKind.Danger);
        }
        foreach (Buggy b in g.Vehicles.All.ToList())
        {
            if (!b.Go.IsValid()) continue;
            if (Corrupt(b.Body)) { try { g.Vehicles.Remove(b); } catch { g.World.Remove(b.Go); } Removed++; }
            else Fix(b.Body);
        }
    }
}
