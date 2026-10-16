using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

public enum ToolMode { Weld, Remover, Balloon }

/// <summary>Toolgun: left click applies the current mode to the prop under the crosshair.</summary>
public sealed class ToolGun(PropManager props)
{
    private Prop? _weldFirst;

    public ToolMode Mode { get; set; } = ToolMode.Weld;
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
                Balloon balloon = Balloon.Attach(props, prop);
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
        socket.ConnectedBody = b.Body;
        FixedAngleConstraint angle = a.Go.AddComponent<FixedAngleConstraint>();
        angle.ConnectedBody = b.Body;
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
    public const float LiftPerBalloon = 14f; // newtons per kg of the prop's mass. Gravity is ~9.8.

    private Prop _prop = null!;
    private GameObject? _visual;

    public static Balloon Attach(PropManager props, Prop prop)
    {
        Balloon b = prop.Go.AddComponent<Balloon>();
        b._prop = prop;

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
            _prop.Body.AddForce(new Float3(0f, _prop.Body.Mass * LiftPerBalloon, 0f));
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
