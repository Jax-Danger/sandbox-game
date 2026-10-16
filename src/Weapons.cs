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
        g.Notify(g.PhysGun.Update(g.Player, i.PrimaryDown, i.PrimaryHeld, i.SecondaryDown, i.Wheel));
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

    public void Select(SandboxGame g, Weapon w)
    {
        if (ReferenceEquals(w, Current)) return;
        if (Current is not null)
        {
            Current.Holster(g);
            Current.Vm?.SetVisible(false);
        }
        Current = w;
        TimeSinceSwitch = 0f;
        w.Equip(g);
        if (w.ViewModelId is { } vmId)
        {
            try { w.Vm ??= new ViewModel(ViewModels.Create(vmId), g.Player.CameraObject, g.Shader); }
            catch (Exception e) { Console.WriteLine($"[viewmodel] {vmId}: {e.Message}"); }
            w.Vm?.SetVisible(true);
            w.Vm?.Play("draw");
        }
        g.Sounds.Play("sfx_weapon_switch", 0.5f);
    }

    public void SelectSlot(SandboxGame g, int slot)
    {
        Weapon[] inSlot = _all.Where(w => w.Slot == slot).ToArray();
        if (inSlot.Length == 0) return;
        int idx = Array.IndexOf(inSlot, Current);
        Select(g, inSlot[(idx + 1) % inSlot.Length]);
    }

    public void Cycle(SandboxGame g, int dir)
    {
        int i = _all.IndexOf(Current);
        Select(g, _all[((i + dir) % _all.Count + _all.Count) % _all.Count]);
    }

    public void Tick(SandboxGame g, WeaponInput input, float dt)
    {
        TimeSinceSwitch += dt;
        Current.Tick(g, input, dt);
        if (input.Inspect && Current.Vm is { ClipPlaying: false } ivm) ivm.Play("inspect");
    }

    /// <summary>Animates the current view model; hidden while driving.</summary>
    public void Animate(SandboxGame g, float dt, Float2 mouse)
    {
        if (Current?.Vm is not { } vm) return;
        vm.SetVisible(!g.InVehicle);
        vm.Update(dt, mouse, g.Player.Speed);
        if (Environment.GetEnvironmentVariable("VMDBG") == "1" && Time.FrameCount % 60 == 0) Console.WriteLine($"vm {vm.Def.Id} en={vm.Root.EnabledInHierarchy} scene={vm.Root.Scene != null} muzzle={vm.MuzzlePosition} eye={g.Player.EyePosition} fwd={g.Player.Forward} kids={vm.Root.GetChildrenDeep().Count()}");
    }
}
