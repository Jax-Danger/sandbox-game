using Prowl.Runtime;

namespace Sandbox;

/// <summary>Marks a prefab as a spawnable prop and carries its editable gameplay data (stage 4/6).
/// PropManager still builds props in code; prefabs baked from it carry this so the editor shows what they are.</summary>
public sealed class PropDefinition : Component
{
    [Tooltip("Which prop this prefab is (spawn menu entry).")] public PropKind Kind = PropKind.Cube;
    /// <summary>Surface material name (Wood, Metal, Plastic, ...), see SurfaceMaterial.</summary>
    [Tooltip("Surface material: Wood, Metal, Plastic, ... (sounds, buoyancy, impacts).")] public string Surface = "Plastic";
    /// <summary>Distance from the pivot to the bottom face, used when spawning on surfaces.</summary>
    [Range(0f, 5f), Tooltip("Pivot to bottom face distance, used when spawning on surfaces.")] public float HalfHeight = 0.5f;
}
