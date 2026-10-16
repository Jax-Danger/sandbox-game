using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

public enum ToolMode { Weld, Remover, Balloon }

/// <summary>Display info for a tool, shown in the spawn menu and the HUD tool info box.</summary>
public sealed record ToolInfo(ToolMode Mode, string Name, string Category, string Description, string Left, string? Right);

/// <summary>Toolgun: left click applies the current mode to the prop under the crosshair.</summary>
public sealed class ToolGun(PropManager props)
{
    private Prop? _weldFirst;

    public ToolMode Mode { get; set; } = ToolMode.Weld;

    /// <summary>Weld strength 0..1. 1 is rigid; lower values make the joint softer and springier.</summary>
    public float WeldStrength { get; set; } = 1f;

    /// <summary>Balloon lift as a multiple of the prop's weight (1 = hovers, above 1 = rises).</summary>
    public float BalloonForce { get; set; } = 1.45f;

    public static readonly ToolInfo[] Tools =
    [
        new(ToolMode.Weld, "Weld", "Constraints", "Joins two props together so they move as one.",
            "Select a prop, then a second to weld them", "Cancel the current selection"),
        new(ToolMode.Balloon, "Balloon", "Construction", "Ties a balloon to a prop that pulls it upwards.",
            "Tie a balloon to a prop", null),
        new(ToolMode.Remover, "Remover", "Construction", "Deletes a prop and any welds attached to it.",
            "Remove the prop you're looking at", null),
    ];

    public static ToolInfo InfoFor(ToolMode m) => Tools.First(t => t.Mode == m);
    public Prop? PendingWeld => _weldFirst is { Alive: true } ? _weldFirst : null;

    public static string Describe(ToolMode m) => m switch
    {
        ToolMode.Weld => "Weld: click two props to join them",
        ToolMode.Remover => "Remover: click a prop to delete it",
        ToolMode.Balloon => "Balloon: click a prop to tie a balloon to it",
        _ => m.ToString(),
    };

    public void CancelPending() => _weldFirst = null;

    public string? Fire(Player player)
    {
        if (!player.Trace(120f, out RaycastHit hit)) return "Nothing there";
        Prop? prop = props.FromBody(hit.Rigidbody);
        if (prop is null) return "That's the map, not a prop";
        return Apply(prop);
    }

    public string? Apply(Prop prop)
    {
        switch (Mode)
        {
            case ToolMode.Remover:
                if (ReferenceEquals(_weldFirst, prop)) _weldFirst = null;
                props.Remove(prop);
                return $"Removed {prop.Go.Name}";

            case ToolMode.Balloon:
            {
                Balloon balloon = Balloon.Attach(props, prop, BalloonForce);
                props.PushUndo(new BalloonUndo(prop, balloon));
                return $"Balloon tied to {prop.Go.Name}";
            }

            case ToolMode.Weld:
            default:
                if (PendingWeld is null)
                {
                    _weldFirst = prop;
                    return $"Weld: selected {prop.Go.Name}, click a second prop";
                }
                if (ReferenceEquals(_weldFirst, prop))
                    return "Pick a different prop to weld to";
                Prop a = _weldFirst!;
                _weldFirst = null;
                Weld(a, prop);
                return $"Welded {a.Go.Name} to {prop.Go.Name}";
        }
    }

    public void Weld(Prop a, Prop b)
    {
        // A ball socket at A's center keeps the relative position; a fixed-angle constraint locks rotation.
        BallSocketConstraint socket = a.Go.AddComponent<BallSocketConstraint>();
        socket.Anchor = Float3.Zero;
        float softness = (1f - Math.Clamp(WeldStrength, 0f, 1f)) * 0.05f;
        socket.Softness = softness;
        socket.ConnectedBody = b.Body;
        FixedAngleConstraint angle = a.Go.AddComponent<FixedAngleConstraint>();
        angle.ConnectedBody = b.Body;
        angle.Softness = softness;
        PhysicsWorld.IgnoreCollisionBetween(a.Body, b.Body);
        a.Body.SetActive(true);
        b.Body.SetActive(true);
        props.PushUndo(new WeldUndo(a, b, socket, angle));
    }

    private sealed class WeldUndo(Prop a, Prop b, BallSocketConstraint socket, FixedAngleConstraint angle) : IUndoable
    {
        public string Label => $"Undone: weld {a.Go.Name} + {b.Go.Name}";
        public void Undo()
        {
            if (!a.Alive) return;
            if (socket.IsValid()) a.Go.RemoveComponent(socket);
            if (angle.IsValid()) a.Go.RemoveComponent(angle);
            if (b.Alive) PhysicsWorld.EnableCollisionBetween(a.Body, b.Body);
            a.Body.SetActive(true);
        }
    }

    private sealed class BalloonUndo(Prop prop, Balloon balloon) : IUndoable
    {
        public string Label => $"Undone: balloon on {prop.Go.Name}";
        public void Undo()
        {
            if (balloon.IsValid()) balloon.Detach();
        }
    }
}

/// <summary>Lifts its prop with a steady upward force. Shows a red sphere above the prop.</summary>
public sealed class Balloon : MonoBehaviour
{
    private const float Gravity = 9.81f;
    private float _lift = 1.45f; // multiple of the prop's weight

    private Prop _prop = null!;
    private GameObject? _visual;

    public static Balloon Attach(PropManager props, Prop prop, float liftMultiple)
    {
        Balloon b = prop.Go.AddComponent<Balloon>();
        b._prop = prop;
        b._lift = liftMultiple;

        GameObject visual = new("Balloon");
        MeshRenderer r = visual.AddComponent<MeshRenderer>();
        r.Mesh = Prowl.Runtime.Resources.Mesh.CreateSphere(0.35f, 12, 16);
        var m = new Prowl.Runtime.Resources.Material(props.Shader);
        m.SetColor("_MainColor", new Color(0.95f, 0.15f, 0.2f, 1f));
        r.Material = m;
        props.Scene.Add(visual);
        visual.Transform.Position = prop.Go.Transform.Position + new Float3(0f, prop.HalfHeight + 1.2f, 0f);
        b._visual = visual;
        prop.Body.SetActive(true);
        return b;
    }

    public override void FixedUpdate()
    {
        if (_prop is null || !_prop.Alive) return;
        if (!_prop.Frozen)
            _prop.Body.AddForce(new Float3(0f, _prop.Body.Mass * Gravity * _lift, 0f));
    }

    public override void Update()
    {
        if (_visual is null || _prop is null) return;
        if (!_prop.Alive) { Detach(); return; }
        // The balloon floats above the prop (kept upright, not parented, so it doesn't spin with it).
        _visual.Transform.Position = _prop.Go.Transform.Position + new Float3(0f, _prop.HalfHeight + 1.2f, 0f);
    }

    public override void OnDisable()
    {
        if (_visual is not null && _visual.Scene is { } s) s.Remove(_visual);
        _visual = null;
    }

    public void Detach()
    {
        if (_visual is not null && _visual.Scene is { } s) s.Remove(_visual);
        _visual = null;
        if (GameObject.IsValid()) GameObject.RemoveComponent(this);
    }
}
