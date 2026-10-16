using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// HL2/GMod-style "use" carry: E picks up a light prop and holds it in front of you,
/// left click throws it, E again drops it. Heavy props are refused (weight limit).
/// Works with any weapon out, like HL2's +use.
/// </summary>
public sealed class Carry
{
    public const float MaxMass = 15f;
    public const float Reach = 2.6f;
    public const float HoldDistance = 1.5f;
    public const float ThrowSpeed = 14f;
    public Prop? Held { get; private set; }
    private Quaternion _relRot;

    /// <summary>E pressed: pick up what you look at, or drop what you hold. Returns a toast message or null.</summary>
    public string? Use(SandboxGame g)
    {
        if (Held is not null) { Drop(); return null; }
        if (!g.Player.Trace(Reach, out RaycastHit hit)) return null;
        Prop? p = g.Props.FromBody(hit.Rigidbody);
        if (p is null) return null;
        if (p.Go.GetComponent<SandboxButton>() is { } button) { button.Press(g); return null; }
        if (p.Frozen) return $"{p.Go.Name} is frozen";
        if (p.Body.Mass > MaxMass) return $"Too heavy ({p.Body.Mass:0} kg)";
        if (ReferenceEquals(g.PhysGun.Held, p)) return null;
        Held = p;
        p.Body.AffectedByGravity = false;
        // Remember the prop's rotation relative to the view yaw so it turns with you.
        _relRot = Quaternion.Inverse(YawRot(g.Player)) * p.Go.Transform.Rotation;
        g.Sounds.Play("sfx_pickup", 0.5f, p.Go.Transform.Position);
        return null;
    }

    private static Quaternion YawRot(Player pl) => pl.CameraObject.Transform.Rotation;

    public void Drop()
    {
        if (Held is { Alive: true } p) p.Body.AffectedByGravity = true;
        Held = null;
    }

    public void Throw(SandboxGame g)
    {
        if (Held is not { Alive: true } p) { Held = null; return; }
        Drop();
        float speed = ThrowSpeed / MathF.Max(1f, MathF.Sqrt(p.Body.Mass / 2f));
        p.Body.LinearVelocity = g.Player.Forward * speed;
        p.Body.SetActive(true);
        g.Sounds.Play("sfx_throw", 0.6f, p.Go.Transform.Position);
    }

    public void Update(SandboxGame g)
    {
        if (Held is null) return;
        if (!Held.Alive || Held.Frozen) { Drop(); return; }
        Prop p = Held;
        Float3 target = g.Player.EyePosition + g.Player.Forward * HoldDistance;
        Float3 delta = target - p.Go.Transform.Position;
        if (Float3.Length(delta) > Reach + 1.5f) { Drop(); return; } // snagged on something
        Float3 v = delta * 12f;
        float sp = Float3.Length(v);
        if (sp > 20f) v *= 20f / sp;
        p.Body.LinearVelocity = v;
        // Steer rotation toward the remembered yaw-relative orientation.
        Quaternion want = YawRot(g.Player) * _relRot;
        Quaternion diff = want * Quaternion.Inverse(p.Go.Transform.Rotation);
        if (diff.W < 0f) diff = new Quaternion(-diff.X, -diff.Y, -diff.Z, -diff.W);
        Float3 axis = new(diff.X, diff.Y, diff.Z);
        p.Body.AngularVelocity = axis * 2f * 10f;
        p.Body.SetActive(true);
    }
}

/// <summary>Empty hands: no gun, left click throws a carried prop or gives a light shove.</summary>
public sealed class HandsWeapon : Weapon
{
    public override string Id => "weapon_hands";
    public override string Name => "Hands";
    public override int Slot => 1;
    public override Icon Icon => Icon.Eye;
    public override string? ViewModelId => "hands";
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        if (!i.PrimaryDown || g.Carry.Held is not null) return;
        if (g.Player.Trace(2.2f, out RaycastHit hit) && g.Props.FromBody(hit.Rigidbody) is { Frozen: false } p)
        {
            p.Body.LinearVelocity += g.Player.Forward * (4f / MathF.Max(1f, p.Body.Mass / 3f));
            p.Body.SetActive(true);
            g.Sounds.Play("sfx_impact_soft", 0.5f, hit.Point);
        }
    }
}

/// <summary>Spawn gun: left click spawns the selected spawn-menu entry where you aim; right click cycles recent spawns.</summary>
public sealed class SpawnGunWeapon : Weapon
{
    public override string Id => "weapon_spawngun";
    public override string Name => "Spawn Gun";
    public override int Slot => 2;
    public override Icon Icon => Icon.Cube;
    public override string? ViewModelId => "spawngun";
    public override string? AmmoText => _g?.SpawnGunEntry?.Name ?? "Cube";
    private SandboxGame? _g;
    public override void Equip(SandboxGame g) => _g = g;

    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        _g = g;
        if (i.PrimaryDown)
        {
            SpawnEntry e = g.SpawnGunEntry ?? SpawnCatalog.Find("prop_cube")!;
            g.Spawn(e);
            Vm?.Play("fire");
        }
        if (i.SecondaryDown && g.RecentSpawns.Count > 0)
        {
            int idx = g.SpawnGunEntry is null ? -1 : g.RecentSpawns.IndexOf(g.SpawnGunEntry);
            g.SpawnGunEntry = g.RecentSpawns[(idx + 1) % g.RecentSpawns.Count];
            g.Notify($"Spawn gun: {g.SpawnGunEntry.Name}");
            g.Sounds.Play("sfx_ui_click", 0.4f);
        }
    }
}
