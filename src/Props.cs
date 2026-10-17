using Jitter2.Dynamics;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Limb = a ragdoll body part (not spawnable on its own).</summary>
public enum PropKind { Cube, Sphere, Plank, Barrel, BigCrate, Ramp, SmallCrate, Pole, Panel, BigBall, Limb }

/// <summary>A spawned, physics-driven prop.</summary>
public sealed class Prop
{
    public required GameObject Go { get; init; }
    public required Rigidbody3D Body { get; init; }
    public required PropKind Kind { get; init; }
    public required Material Material { get; init; }
    public required Color BaseColor { get; set; }
    /// <summary>Distance from center to the bottom face (used when spawning on surfaces).</summary>
    public float HalfHeight { get; init; }
    public bool Frozen { get; private set; }
    public string Surface { get; private set; } = "Plastic";
    /// <summary>Entity type id for special props (ent_barrel, ent_ball, ent_light, ent_button); null for plain props.</summary>
    public string? Entity { get; set; }
    /// <summary>Free-form entity state saved with the map (e.g. money amount, printer cash).</summary>
    public string? EntityData { get; set; }
    private float _highlight;

    public void SetSurface(SurfaceMaterial m)
    {
        Surface = m.Name;
        m.ApplyVisual(Material);
        Body.Friction = m.Friction;
        Body.Restitution = m.Restitution;
    }

    private void ApplyTint()
    {
        Color c = Frozen ? new Color(0.35f, 0.75f, 1f, 1f) : BaseColor;
        float k = Math.Clamp(_highlight, 0f, 1f) * 0.5f;
        Material.SetColor("_MainColor", new Color(c.R + (0.75f - c.R) * k, c.G + (0.88f - c.G) * k, c.B + (1f - c.B) * k, 1f));
    }

    public void SetMass(float mass)
    {
        Body.Mass = Math.Clamp(mass, 0.1f, 500f);
        Body.SetActive(true);
    }

    /// <summary>Soft emissive glow used for hover/selection highlight (0 = off).</summary>
    public void SetHighlight(float amount)
    {
        if (MathF.Abs(amount - _highlight) < 0.01f) return;
        _highlight = amount;
        ApplyTint();
    }
    public bool Alive => Go.IsValid() && Go.Scene != null;

    public void SetColor(Color c)
    {
        BaseColor = c;
        ApplyTint();
    }

    public void SetFrozen(bool frozen)
    {
        if (frozen == Frozen && frozen == (Body.MotionType == MotionType.Static)) return;
        Frozen = frozen;
        ApplyTint();
        if (frozen)
        {
            Body.LinearVelocity = Float3.Zero;
            Body.AngularVelocity = Float3.Zero;
            Body.MotionType = MotionType.Static;

        }
        else
        {
            Body.MotionType = MotionType.Dynamic;
            Body.SetActive(true);

        }
    }
}

public interface IUndoable
{
    string Label { get; }
    void Undo();
}

/// <summary>Owns every spawned prop, the welds/balloons attached to them, and the undo stack.</summary>
public sealed class PropManager(Scene scene, Shader shader)
{
    private readonly List<Prop> _props = [];
    private readonly Stack<IUndoable> _undo = new();
    private int _counter;

    public Scene Scene { get; } = scene;
    public Shader Shader { get; } = shader;
    public IReadOnlyList<Prop> Props => _props;
    public int UndoCount => _undo.Count;

    /// <summary>Smoothed placeholder meshes (bevelled boxes, rounded cylinders, high-res spheres), cached per kind.
    /// The asset manifest can still replace a kind's mesh with a real model.</summary>
    public static Mesh MeshFor(PropKind kind, Float3? size = null)
    {
        Float3 sz = size ?? Float3.One;
        float minDim = MathF.Min(sz.X, MathF.Min(sz.Y, sz.Z));
        return kind switch
        {
            PropKind.Cube => Meshes.RoundedBox(Float3.One, 0.06f),
            PropKind.Sphere => Meshes.Sphere(0.5f),
            PropKind.BigBall => Meshes.Sphere(1f),
            PropKind.Barrel => Meshes.Barrel(0.4f, 1.1f),
            PropKind.Pole => Meshes.RoundedCylinder(0.08f, 3f, 0.02f, 16),
            _ => Meshes.RoundedBox(sz, MathF.Min(0.05f, minDim * 0.3f)),
        };
    }

    public static Float3 SizeOf(PropKind kind) => kind switch
    {
        PropKind.Plank => new Float3(3f, 0.15f, 0.6f),
        PropKind.BigCrate => new Float3(2f, 2f, 2f),
        PropKind.Ramp => new Float3(2f, 0.2f, 3.5f),
        PropKind.SmallCrate => new Float3(0.6f, 0.6f, 0.6f),
        PropKind.Panel => new Float3(2f, 2f, 0.1f),
        _ => Float3.One,
    };

    /// <summary>Builds a kind's mesh ahead of time so the first spawn doesn't hitch (used by the loading screen).</summary>
    public static void WarmMesh(PropKind kind) { try { _ = MeshFor(kind, SizeOf(kind)); } catch { } }

    public static readonly PropKind[] AllKinds = Enum.GetValues<PropKind>().Where(k => k != PropKind.Limb).ToArray();

    public static string DisplayName(PropKind k) => k switch
    {
        PropKind.Limb => "Limb",
        PropKind.Cube => "Cube",
        PropKind.Sphere => "Sphere",
        PropKind.Plank => "Plank",
        PropKind.Barrel => "Barrel",
        PropKind.BigCrate => "Big Crate",
        PropKind.Ramp => "Ramp",
        PropKind.SmallCrate => "Small Crate",
        PropKind.Pole => "Pole",
        PropKind.Panel => "Panel",
        PropKind.BigBall => "Big Ball",
        _ => k.ToString(),
    };

    /// <summary>Half of the prop's height, so a spawn can rest on the surface it targets.</summary>
    public static float HalfHeightOf(PropKind k) => k switch
    {
        PropKind.Limb => 0.3f,
        PropKind.Cube => 0.5f,
        PropKind.Sphere => 0.5f,
        PropKind.Plank => 0.075f,
        PropKind.Barrel => 0.55f,
        PropKind.BigCrate => 1f,
        PropKind.Ramp => 0.6f,
        PropKind.SmallCrate => 0.3f,
        PropKind.Pole => 1.5f,
        PropKind.Panel => 1f,
        PropKind.BigBall => 1f,
        _ => 0.5f,
    };

    public Prop Spawn(PropKind kind, Float3 position, float yawDegrees, bool recordUndo = true)
    {
        _counter++;
        GameObject go = new($"{DisplayName(kind)} #{_counter}");
        go.Transform.Position = position;
        go.Transform.LocalEulerAngles = new Float3(kind == PropKind.Ramp ? -20f : 0f, yawDegrees, 0f);

        Color color = ColorFor(kind, _counter);
        var material = Mats.New(Shader);
        material.SetColor("_MainColor", color);
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Material = material;

        Rigidbody3D body = go.AddComponent<Rigidbody3D>();
        switch (kind)
        {
            case PropKind.Cube:
                renderer.Mesh = MeshFor(PropKind.Cube);
                go.AddComponent<BoxCollider>().Size = Float3.One;
                body.Mass = 1f;
                break;
            case PropKind.Sphere:
                renderer.Mesh = MeshFor(PropKind.Sphere);
                go.AddComponent<SphereCollider>().Radius = 0.5f;
                body.Mass = 1f;
                break;
            case PropKind.Plank:
            {
                var size = new Float3(3f, 0.15f, 0.6f);
                renderer.Mesh = MeshFor(kind, size);
                go.AddComponent<BoxCollider>().Size = size;
                body.Mass = 1.5f;
                break;
            }
            case PropKind.Barrel:
            {
                renderer.Mesh = MeshFor(PropKind.Barrel);
                CylinderCollider c = go.AddComponent<CylinderCollider>();
                c.Radius = 0.4f;
                c.Height = 1.1f;
                body.Mass = 2f;
                break;
            }
            case PropKind.BigCrate:
            {
                var size = new Float3(2f, 2f, 2f);
                renderer.Mesh = MeshFor(kind, size);
                go.AddComponent<BoxCollider>().Size = size;
                body.Mass = 6f;
                break;
            }
            case PropKind.SmallCrate:
            {
                var size = new Float3(0.6f, 0.6f, 0.6f);
                renderer.Mesh = MeshFor(kind, size);
                go.AddComponent<BoxCollider>().Size = size;
                body.Mass = 1.2f;
                break;
            }
            case PropKind.Pole:
            {
                renderer.Mesh = MeshFor(PropKind.Pole);
                CylinderCollider c = go.AddComponent<CylinderCollider>();
                c.Radius = 0.08f;
                c.Height = 3f;
                body.Mass = 2f;
                break;
            }
            case PropKind.Panel:
            {
                var size = new Float3(2f, 2f, 0.1f);
                renderer.Mesh = MeshFor(kind, size);
                go.AddComponent<BoxCollider>().Size = size;
                body.Mass = 3f;
                break;
            }
            case PropKind.BigBall:
                renderer.Mesh = MeshFor(PropKind.BigBall);
                go.AddComponent<SphereCollider>().Radius = 1f;
                body.Mass = 5f;
                break;
            case PropKind.Ramp:
            {
                // No wedge primitive in Prowl; a ramp is a wide, tilted slab.
                var size = new Float3(2f, 0.2f, 3.5f);
                renderer.Mesh = MeshFor(kind, size);
                go.AddComponent<BoxCollider>().Size = size;
                body.Mass = 4f;
                break;
            }
        }

        Scene.Add(go);
        var prop0 = new Prop
        {
            Go = go, Body = body, Kind = kind, Material = material, BaseColor = color,
            HalfHeight = HalfHeightOf(kind),
        };
        prop0.SetSurface(SurfaceMaterial.Get(DefaultSurface(kind)));
        var prop = prop0;
        _props.Add(prop);
        if (recordUndo)
            _undo.Push(new SpawnUndo(this, prop));
        return prop;
    }

    public Prop? FromBody(Rigidbody3D? body)
    {
        if (body is null || !body.IsValid()) return null;
        foreach (Prop p in _props)
            if (ReferenceEquals(p.Body, body)) return p;
        return null;
    }

    public void Remove(Prop prop)
    {
        if (!_props.Remove(prop)) return;
        CollisionPairs.Release(prop.Body);
        // Break welds on other props that point at this one, so no constraint references a dead body.
        foreach (Prop other in _props)
            foreach (PhysicsConstraint c in other.Go.GetComponents<PhysicsConstraint>().ToArray())
                if (ReferenceEquals(c.ConnectedBody, prop.Body))
                    other.Go.RemoveComponent(c);
        if (prop.Go.Scene != null)
            SafeRemove(Scene, prop.Go);
    }

    /// <summary>
    /// Removes a physics GameObject without leaking. The engine's Rigidbody3D.OnDisable re-enables child colliders so
    /// they attach to a standalone static body, which is never removed: every deleted prop left an invisible static
    /// collider in the Jitter world (soak: 23k bodies after 90 s). Disabling the colliders first detaches them for good.
    /// </summary>
    public static void SafeRemove(Prowl.Runtime.Resources.Scene scene, GameObject go)
    {
        try { foreach (Collider c in go.GetComponentsInChildren<Collider>()) if (c.Enabled) c.Enabled = false; } catch { }
        scene.Remove(go);
    }

    public void ClearAll()
    {
        foreach (Prop p in _props.ToArray())
            Remove(p);
        Ragdoll.All.Clear();
        _undo.Clear();
        PhysicsSweep.Run(Scene);
    }

    /// <summary>Registers an externally built body (e.g. a ragdoll limb) as a prop so tools and the physgun work on it.</summary>
    public Prop Adopt(GameObject go, Rigidbody3D body, Material material, Color color, PropKind kind, float halfHeight)
    {
        var p = new Prop { Go = go, Body = body, Kind = kind, Material = material, BaseColor = color, HalfHeight = halfHeight };
        _props.Add(p);
        return p;
    }

    public bool Contains(Prop p) => _props.Contains(p);

    public void PushUndo(IUndoable action) => _undo.Push(action);

    /// <summary>Undo the newest action that still applies. Returns its label, or null if nothing to undo.</summary>
    public string? Undo()
    {
        while (_undo.Count > 0)
        {
            IUndoable action = _undo.Pop();
            if (action is SpawnUndo s && !_props.Contains(s.Prop)) continue; // already removed
            action.Undo();
            return action.Label;
        }
        return null;
    }

    /// <summary>Representative colour for icons.</summary>
    public static Color IconColor(PropKind kind) => ColorFor(kind, kind == PropKind.Sphere ? 5 : 4);

    public static System.Drawing.Color IconColorDrawing(PropKind kind)
    {
        Color c = IconColor(kind);
        return System.Drawing.Color.FromArgb(255, (int)(c.R * 255), (int)(c.G * 255), (int)(c.B * 255));
    }

    public static string DefaultSurface(PropKind kind) => kind switch
        {
            PropKind.Plank or PropKind.BigCrate or PropKind.Ramp or PropKind.SmallCrate => "Wood",
            PropKind.Pole => "Metal",
            PropKind.BigBall => "Rubber",
            PropKind.Barrel => "Metal",
            _ => "Plastic",
        };

    private static Color ColorFor(PropKind kind, int index)
    {
        Color[] palette =
        [
            new(0.85f, 0.28f, 0.22f, 1f), new(0.93f, 0.62f, 0.18f, 1f), new(0.95f, 0.84f, 0.25f, 1f),
            new(0.30f, 0.70f, 0.35f, 1f), new(0.25f, 0.48f, 0.86f, 1f), new(0.72f, 0.32f, 0.78f, 1f),
        ];
        return kind switch
        {
            PropKind.Plank => new Color(0.62f, 0.45f, 0.28f, 1f),
            PropKind.BigCrate => new Color(0.55f, 0.40f, 0.22f, 1f),
            PropKind.Barrel => new Color(0.30f, 0.45f, 0.30f, 1f),
            PropKind.Ramp => new Color(0.6f, 0.6f, 0.65f, 1f),
            PropKind.SmallCrate => new Color(0.66f, 0.50f, 0.30f, 1f),
            PropKind.Pole => new Color(0.75f, 0.75f, 0.78f, 1f),
            PropKind.Panel => new Color(0.82f, 0.84f, 0.88f, 1f),
            _ => palette[index % palette.Length],
        };
    }

    private sealed class SpawnUndo(PropManager owner, Prop prop) : IUndoable
    {
        public Prop Prop { get; } = prop;
        public string Label => $"Undone: {Prop.Go.Name}";
        public void Undo() => owner.Remove(Prop);
    }
}
