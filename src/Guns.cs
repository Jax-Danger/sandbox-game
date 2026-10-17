using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Base for ammo weapons: magazine + reserve, fire rate, semi/auto, reload with the view model's reload clip,
/// recoil (view punch + view-model kick), muzzle flash at the view model's muzzle bone.
/// </summary>
public abstract class Firearm : Weapon
{
    public abstract int MagSize { get; }
    public abstract float FireInterval { get; }
    public virtual bool Automatic => false;
    public virtual float ReloadTime => 1.4f;
    public virtual float Punch => 1.2f;
    public virtual string FireSound => $"sfx_{Id.Replace("weapon_", "")}";
    public int Mag { get; set; }
    public int Reserve { get; set; }
    public bool Reloading => _reload > 0f;
    private float _cooldown, _reload;

    protected Firearm() { }
    public void Init() { Mag = MagSize; Reserve = MagSize * 4; }
    public void Refill() { Mag = MagSize; Reserve = MagSize * 6; }
    public override string? AmmoText => Reloading ? "Reloading" : $"{Mag} / {Reserve}";
    public override void Holster(SandboxGame g) => _reload = 0f;

    public Float3 MuzzleOf(SandboxGame g) => Vm?.MuzzlePosition ?? g.Player.EyePosition + g.Player.Forward * 0.5f;

    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        _cooldown -= dt;
        if (_reload > 0f)
        {
            _reload -= dt;
            if (_reload <= 0f)
            {
                int take = Math.Min(MagSize - Mag, Reserve);
                Mag += take;
                Reserve -= take;
            }
            return;
        }
        if ((i.Reload || (Mag == 0 && i.PrimaryDown)) && Mag < MagSize && Reserve > 0) { StartReload(g); return; }
        bool trigger = Automatic ? i.PrimaryHeld : i.PrimaryDown;
        if (!trigger || _cooldown > 0f) return;
        if (Mag <= 0) { g.Sounds.Play("sfx_dry", 0.5f); _cooldown = 0.25f; return; }
        Mag--;
        _cooldown = FireInterval;
        Float3 muzzle = MuzzleOf(g);
        Shoot(g, muzzle);
        g.Fx.MuzzleFlash(muzzle, this is ShotgunWeapon or RocketLauncherWeapon ? 1.8f : 1f);
        g.Sounds.Play(FireSound, 0.8f, null, 0.95f + Random.Shared.NextSingle() * 0.1f);
        g.Player.Punch(Punch);
        Vm?.Play("fire");
        Vm?.Kick(1f);
        ShotsFired++;
    }

    public int ShotsFired { get; private set; }

    public void StartReload(SandboxGame g)
    {
        _reload = ReloadTime;
        Vm?.Play("reload");
        g.Sounds.Play("sfx_reload", 0.6f);
    }

    protected abstract void Shoot(SandboxGame g, Float3 muzzle);

    protected static Float3 Spread(Float3 dir, float degrees)
    {
        if (degrees <= 0f) return dir;
        float r = degrees * MathF.PI / 180f;
        var rng = Random.Shared;
        Float3 j = new((rng.NextSingle() - 0.5f) * 2f, (rng.NextSingle() - 0.5f) * 2f, (rng.NextSingle() - 0.5f) * 2f);
        return Float3.Normalize(dir + j * MathF.Tan(r));
    }
}

public sealed class PistolWeapon : Firearm
{
    public PistolWeapon() => Init();
    public override string Id => "weapon_pistol";
    public override string Name => "Pistol";
    public override int Slot => 3;
    public override Icon Icon => Icon.Pistol;
    public override string? ViewModelId => "pistol";
    public override int MagSize => 12;
    public override float FireInterval => 0.12f;
    protected override void Shoot(SandboxGame g, Float3 muzzle) => g.Fx.Bullet(g.Player.EyePosition, Spread(g.Player.Forward, 0.6f), muzzle, 6f);
}

public sealed class SmgWeapon : Firearm
{
    public SmgWeapon() => Init();
    public override string Id => "weapon_smg";
    public override string Name => "SMG";
    public override int Slot => 3;
    public override Icon Icon => Icon.Smg;
    public override string? ViewModelId => "smg";
    public override int MagSize => 30;
    public override float FireInterval => 0.075f;
    public override bool Automatic => true;
    public override float Punch => 0.6f;
    protected override void Shoot(SandboxGame g, Float3 muzzle) => g.Fx.Bullet(g.Player.EyePosition, Spread(g.Player.Forward, 2.2f), muzzle, 3.5f);
}

public sealed class ShotgunWeapon : Firearm
{
    public ShotgunWeapon() => Init();
    public override string Id => "weapon_shotgun";
    public override string Name => "Shotgun";
    public override int Slot => 4;
    public override Icon Icon => Icon.Shotgun;
    public override string? ViewModelId => "shotgun";
    public override int MagSize => 6;
    public override float FireInterval => 0.85f;
    public override float ReloadTime => 1.8f;
    public override float Punch => 4f;
    protected override void Shoot(SandboxGame g, Float3 muzzle)
    {
        for (int k = 0; k < 8; k++) g.Fx.Bullet(g.Player.EyePosition, Spread(g.Player.Forward, 5f), muzzle, 3.5f, 60f);
    }
}

public sealed class RocketLauncherWeapon : Firearm
{
    public RocketLauncherWeapon() { Init(); Reserve = 6; }
    public override string Id => "weapon_rpg";
    public override string Name => "Rocket Launcher";
    public override int Slot => 5;
    public override Icon Icon => Icon.Bolt;
    public override string? ViewModelId => "rpg";
    public override int MagSize => 1;
    public override float FireInterval => 0.9f;
    public override float ReloadTime => 1.6f;
    public override float Punch => 3f;
    protected override void Shoot(SandboxGame g, Float3 muzzle) => g.Fx.Rocket(g.Player.EyePosition + g.Player.Forward * 0.6f, g.Player.Forward);
}

public sealed class GrenadeWeapon : Weapon
{
    public override string Id => "weapon_grenade";
    public override string Name => "Grenade";
    public override int Slot => 5;
    public override Icon Icon => Icon.Sphere;
    public override string? ViewModelId => "grenade";
    public int Count { get; set; } = 5;
    public override string? AmmoText => $"{Count}";
    private float _cooldown;
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        _cooldown -= dt;
        if (_cooldown > 0f || Count <= 0) return;
        bool lob = i.SecondaryDown;
        if (!i.PrimaryDown && !lob) return;
        Count--;
        _cooldown = 0.9f;
        Float3 v = g.Player.Forward * (lob ? 6f : 15f) + new Float3(0f, lob ? 3f : 2f, 0f);
        g.Fx.Grenade(g.Player.EyePosition + g.Player.Forward * 0.5f, v);
        g.Sounds.Play("sfx_throw", 0.6f);
        Vm?.Play("fire");
    }
}

public sealed class CrowbarWeapon : Weapon
{
    public override string Id => "weapon_crowbar";
    public override string Name => "Crowbar";
    public override int Slot => 1;
    public override Icon Icon => Icon.Wrench;
    public override string? ViewModelId => "crowbar";
    private float _cooldown, _pendingHit = -1f;
    public int Hits { get; private set; }
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        _cooldown -= dt;
        if (_pendingHit > 0f)
        {
            _pendingHit -= dt;
            if (_pendingHit <= 0f) Strike(g);
        }
        if (!i.PrimaryHeld || _cooldown > 0f) return;
        _cooldown = 0.42f;
        _pendingHit = 0.12f; // impact lands mid-swing
        Vm?.Play("fire");
        g.Sounds.Play("sfx_swing", 0.5f);
    }

    public void Strike(SandboxGame g)
    {
        _pendingHit = -1f;
        if (!g.Player.Trace(2.0f, out RaycastHit hit)) return;
        Hits++;
        Fx.Push(hit.Rigidbody, g.Player.Forward * 9f, hit.Point);
        g.Combat.Hit(hit.Rigidbody, 34f, hit.Point, "Crowbar");
        g.Fx.Sparks(hit.Point, hit.Normal, 3);
        g.Fx.Decal(hit.Point, hit.Normal, hit.Rigidbody);
        g.Sounds.Play("sfx_impact", 0.7f, hit.Point);
        g.Player.Punch(-1.5f);
    }
}
