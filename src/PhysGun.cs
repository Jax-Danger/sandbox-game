using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Physgun: hold left mouse to grab the prop under the crosshair and carry it at a distance.
/// Scroll pushes/pulls. Releasing keeps the prop's velocity (so you can throw).
/// Right-click while holding freezes the prop in place. Grabbing a frozen prop unfreezes it.
/// </summary>
public sealed class PhysGun(PropManager props)
{
    private const float MaxReach = 80f, MinDistance = 1.5f, Spring = 14f, MaxSpeed = 45f;

    private Prop? _held;
    private float _distance;
    private Float3 _localGrabPoint;
    private bool _hadGravity;

    public Prop? Held => _held is { Alive: true } ? _held : null;

    /// <summary>Returns a short status string for the HUD, or null.</summary>
    public string? Update(Player player, bool primaryDown, bool primaryHeld, bool secondaryDown, float wheel)
    {
        if (_held is { Alive: false }) _held = null;

        if (_held is null && primaryDown)
            return TryGrab(player);

        if (_held is null) return null;

        if (secondaryDown)
        {
            Prop p = _held;
            Release(keepVelocity: false);
            p.SetFrozen(true);
            return $"Froze {p.Go.Name}";
        }

        if (!primaryHeld)
        {
            Release(keepVelocity: true);
            return null;
        }

        _distance = Math.Clamp(_distance + wheel * 1.5f, MinDistance, MaxReach);
        Hold(player);
        return null;
    }

    public string? TryGrab(Player player)
    {
        if (!player.Trace(MaxReach, out RaycastHit hit)) return null;
        Prop? prop = props.FromBody(hit.Rigidbody);
        if (prop is null) return null;

        string? msg = null;
        if (prop.Frozen)
        {
            prop.SetFrozen(false);
            msg = $"Unfroze {prop.Go.Name}";
        }

        _held = prop;
        _distance = Math.Max(MinDistance, hit.Distance);
        _localGrabPoint = prop.Go.Transform.InverseTransformPoint(hit.Point);
        _hadGravity = prop.Body.AffectedByGravity;
        prop.Body.AffectedByGravity = false;
        prop.Body.SetActive(true);
        return msg;
    }

    private void Hold(Player player)
    {
        Prop p = _held!;
        Float3 target = player.EyePosition + player.Forward * _distance;
        Float3 grabWorld = p.Go.Transform.TransformPoint(_localGrabPoint);
        Float3 velocity = (target - grabWorld) * Spring;
        float speed = Float3.Length(velocity);
        if (speed > MaxSpeed) velocity *= MaxSpeed / speed;
        p.Body.LinearVelocity = velocity;
        p.Body.AngularVelocity *= 0.85f;
        p.Body.SetActive(true);
    }

    public void Release(bool keepVelocity)
    {
        if (_held is { Alive: true } p)
        {
            p.Body.AffectedByGravity = _hadGravity;
            if (!keepVelocity)
            {
                p.Body.LinearVelocity = Float3.Zero;
                p.Body.AngularVelocity = Float3.Zero;
            }
        }
        _held = null;
    }
}
