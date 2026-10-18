using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Per-frame weapon input, already gated by menus.</summary>
public readonly record struct WeaponInput(bool PrimaryDown, bool PrimaryHeld, bool PrimaryUp, bool SecondaryDown, bool SecondaryHeld,
    bool Reload, float Wheel, bool Inspect = false);

/// <summary>Base class for everything in the weapon switcher (hands, physgun, toolgun, guns).</summary>
public abstract class Weapon
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract int Slot { get; }   // 1-based slot column, like GMod/HL2
    public abstract Icon Icon { get; }
    /// <summary>True while this weapon wants the mouse wheel (e.g. physgun pushing a prop).</summary>
    public virtual bool CapturesWheel => false;
    public virtual string? AmmoText => null;
    /// <summary>View model definition id (see ViewModels.Create / assets/viewmodels). Null = no view model.</summary>
    public virtual string? ViewModelId => null;
    /// <summary>The live view model for this weapon (created by WeaponSystem on first equip).</summary>
    public ViewModel? Vm { get; set; }
    /// <summary>False hides the weapon from the switcher (e.g. job-only roleplay tools).</summary>
    /// <summary>Usable right now (Roleplay limits you to the build tools, your job loadout and bought weapons).</summary>
    public virtual bool Available(SandboxGame g) => g.Roleplay?.Allows(this) ?? true;
    public virtual void Equip(SandboxGame g) { }
    public virtual void Holster(SandboxGame g) { }
    public abstract void Tick(SandboxGame g, WeaponInput input, float dt);
}

public sealed class PhysGunWeapon : Weapon
{
    public override string Id => "weapon_physgun";
    public override string Name => "Physics Gun";
    public override int Slot => 2;
    public override Icon Icon => Icon.Magnet;
    public override string? ViewModelId => "physgun";
    public override bool CapturesWheel => _g?.PhysGun.Held is not null;
    private SandboxGame? _g;

    public override void Equip(SandboxGame g) => _g = g;
    public override void Holster(SandboxGame g) { g.PhysGun.Release(keepVelocity: false); g.Fx.Beam(default, null); }
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        bool had = g.PhysGun.Held is not null;
        g.Notify(g.PhysGun.Update(g.Player, i.PrimaryDown, i.PrimaryHeld, i.SecondaryDown, i.Wheel));
        bool has = g.PhysGun.Held is not null;
        if (has != had) { Vm?.Play(has ? "grab" : "release"); Vm?.Kick(has ? 0.4f : 0.8f); }
        g.Fx.Beam(Vm?.MuzzlePosition ?? g.Player.EyePosition, g.PhysGun.GrabPoint);
    }
}

public sealed class ToolGunWeapon : Weapon
{
    public override string Id => "weapon_toolgun";
    public override string Name => "Tool Gun";
    public override int Slot => 2;
    public override Icon Icon => Icon.Wrench;
    public override string? ViewModelId => "toolgun";
    public override void Holster(SandboxGame g) => g.ToolGun.CancelPending();
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        if (i.PrimaryDown)
        {
            g.Notify(g.ToolGun.Fire(g.Player));
            g.Sounds.Play("sfx_tool", 0.5f);
            Vm?.Play("fire");
            Vm?.Kick(0.3f);
        }
        if (i.SecondaryDown) g.Notify(g.ToolGun.Secondary(g.Player));
    }
}

/// <summary>Owns the weapon list, the current weapon and switching (number keys / mouse wheel).</summary>
public sealed class WeaponSystem
{
    private readonly List<Weapon> _all = [];
    public IReadOnlyList<Weapon> All => _all;
    public Weapon Current { get; private set; } = null!;
    public float TimeSinceSwitch { get; private set; } = 99f;
    public bool IsToolGun => Current is ToolGunWeapon;

    public void Add(Weapon w)
    {
        _all.Add(w);
        List<Weapon> ordered = _all.OrderBy(x => x.Slot).ToList(); // OrderBy is stable: insertion order within a slot
        _all.Clear();
        _all.AddRange(ordered);
    }

    public T Get<T>() where T : Weapon => _all.OfType<T>().First();

    private const float HolsterTime = 0.14f;
    private ViewModel? _holsterVm;
    private float _holsterLeft;

    public void Select(SandboxGame g, Weapon w)
    {
        if (ReferenceEquals(w, Current)) return;
        if (Current is not null && !w.Available(g)) return;
        if (Current is not null)
        {
            Current.Holster(g);
            // Visual-only holster: the old view model dips away, then the new one draws.
            if (_holsterVm is not null && !ReferenceEquals(_holsterVm, Current.Vm)) _holsterVm.SetVisible(false);
            if (Current.Vm is { } old && old.Root.Enabled && !g.InVehicle && !Application.IsHeadless) { _holsterVm = old; _holsterLeft = HolsterTime; old.Play("holster"); }
            else Current.Vm?.SetVisible(false);
        }
        Current = w;
        TimeSinceSwitch = 0f;
        w.Equip(g);
        if (w.ViewModelId is { } vmId)
        {
            try { w.Vm ??= new ViewModel(ViewModels.Create(vmId), g.Player.CameraObject, g.Shader); }
            catch (Exception e) { Console.WriteLine($"[viewmodel] {vmId}: {e.Message}"); }
            if (_holsterVm is not null && !ReferenceEquals(_holsterVm, w.Vm)) w.Vm?.SetVisible(false);
            else { _holsterVm = null; w.Vm?.SetVisible(true); w.Vm?.Play("draw"); }
        }
        g.Sounds.Play("sfx_weapon_switch", 0.5f);
    }

    /// <summary>Drops back to the physgun if the current weapon stopped being available (job change).</summary>
    public void RefreshAvailability(SandboxGame g)
    {
        if (Current is not null && !Current.Available(g)) Select(g, _all.OfType<PhysGunWeapon>().First());
    }

    public void SelectSlot(SandboxGame g, int slot)
    {
        Weapon[] inSlot = _all.Where(w => w.Slot == slot && w.Available(g)).ToArray();
        if (inSlot.Length == 0) return;
        int idx = Array.IndexOf(inSlot, Current);
        Select(g, inSlot[(idx + 1) % inSlot.Length]);
    }

    public void Cycle(SandboxGame g, int dir)
    {
        var usable = _all.Where(w => w.Available(g) || ReferenceEquals(w, Current)).ToList();
        int i = usable.IndexOf(Current);
        Select(g, usable[((i + dir) % usable.Count + usable.Count) % usable.Count]);
    }

    public void Tick(SandboxGame g, WeaponInput input, float dt)
    {
        TimeSinceSwitch += dt;
        Current.Tick(g, input, dt);
        if (input.Inspect && Current.Vm is { ClipPlaying: false } ivm) ivm.Play("inspect");
    }

    /// <summary>Animates the current view model; hidden while driving.</summary>
    /// <summary>True while the right mouse button aims the current firearm (set by the game each frame).</summary>
    public bool Aiming { get; set; }

    private static VmInput VmIn(SandboxGame g, Float2 mouse, bool aim)
    {
        Player p = g.Player;
        float land = p.LandingKick; p.LandingKick = 0f;
        bool shot = g.ShotPose is not null;
        return new VmInput(mouse, shot ? (g.ShotPose == "sprint" ? 9f : 0f) : p.Speed, shot ? (g.ShotPose == "sprint" ? Float2.UnitY : Float2.Zero) : p.MoveInput,
            shot || p.IsGrounded, p.VerticalVelocity, land, shot ? g.ShotPose == "sprint" : p.Running,
            g.ShotPose == "crouch" ? 1f : p.CrouchBlend, shot ? g.ShotPose == "aim" : aim, shot ? (g.ShotPose == "sprint" ? Time.UnscaledTotalTime * 2.6f : -1f) : g.StepCycle);
    }

    public void Animate(SandboxGame g, float dt, Float2 mouse)
    {
        float camFov = g.Player.Camera.FieldOfView;
        if (_holsterVm is not null)
        {
            _holsterLeft -= MathF.Max(dt, 1f / 120f);
            _holsterVm.SetFov(camFov, g.Settings.ViewModelFov);
            _holsterVm.Update(dt, VmIn(g, mouse, false));
            if (_holsterLeft > 0f && !g.InVehicle) return;
            _holsterVm.SetVisible(false);
            _holsterVm = null;
            Current?.Vm?.Play("draw");
        }
        if (Current?.Vm is not { } vm) return;
        vm.SetVisible(!g.InVehicle && !g.MenuBackdrop && !Gallery.Active);
        vm.SetFov(camFov, g.Settings.ViewModelFov);
        vm.Update(dt, VmIn(g, mouse, Aiming && Current is Firearm));
        if (Environment.GetEnvironmentVariable("VMDBG") == "1" && Time.FrameCount % 60 == 0) Console.WriteLine($"vm {vm.Def.Id} en={vm.Root.EnabledInHierarchy} scene={vm.Root.Scene != null} muzzle={vm.MuzzlePosition} eye={g.Player.EyePosition} fwd={g.Player.Forward} kids={vm.Root.GetChildrenDeep().Count()}");
    }
}
