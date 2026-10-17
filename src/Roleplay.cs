using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public sealed record Job(string Id, string Name, System.Drawing.Color Color, int Salary, int Max, string Description, string[] Loadout, string Perk);

public sealed record Buyable(string Id, string Name, Icon Icon, int Price, string Description, string? RequiresJob = null);

/// <summary>
/// DarkRP-style roleplay layer for singleplayer: wallet, jobs with salaries/limits/loadouts, laws, dropped money,
/// money printers, shipments, arrests/wanted status and NPC citizens/police. Only active in Roleplay sessions.
/// </summary>
public sealed class RoleplaySystem(SandboxGame g)
{
    public static readonly Job[] Jobs =
    [
        new("citizen", "Citizen", System.Drawing.Color.FromArgb(255, 92, 214, 140), 45, 0, "An ordinary citizen. Build a home, start a business, keep your head down.", [], "Default job"),
        new("police", "Police Officer", System.Drawing.Color.FromArgb(255, 77, 125, 255), 65, 4, "Keep the peace. Arrest wanted criminals with the baton.", ["weapon_baton", "weapon_pistol"], "Baton arrests, pistol"),
        new("mayor", "Mayor", System.Drawing.Color.FromArgb(255, 200, 70, 70), 95, 1, "Run the city. Write the laws everyone sees on the board.", [], "Sets the laws"),
        new("thief", "Thief", System.Drawing.Color.FromArgb(255, 150, 150, 160), 35, 3, "Pick locks on frozen props and printers. Becomes wanted when caught.", ["weapon_lockpick"], "Lockpick"),
        new("medic", "Medic", System.Drawing.Color.FromArgb(255, 240, 120, 170), 60, 2, "Heal people. Carries a medkit and can buy armour cheaper.", ["weapon_medkit"], "Medkit, cheap armour"),
        new("gundealer", "Gun Dealer", System.Drawing.Color.FromArgb(255, 250, 160, 60), 50, 2, "Order weapon shipments and sell them on.", [], "Buys shipments"),
        new("gangster", "Gangster", System.Drawing.Color.FromArgb(255, 120, 120, 120), 40, 4, "Run with the gang. Starts with a pistol; printers pay you more.", ["weapon_pistol"], "Pistol, +25% printer cash"),
        new("hobo", "Hobo", System.Drawing.Color.FromArgb(255, 140, 100, 60), 10, 5, "Live on the street. Small salary, but you never pay for props.", [], "Free props"),
    ];

    public static readonly Buyable[] Shop =
    [
        new("rp_printer", "Money Printer", Icon.Money, 1000, "Prints $25 every 20 s. Press E on it to collect."),
        new("rp_armor", "Armour vest", Icon.Shield, 250, "+50 armour."),
        new("rp_medkit", "Health kit", Icon.Heart, 120, "Restores 50 health."),
        new("rp_ammo", "Ammo box", Icon.Crate, 60, "Refills reserve ammo for every gun."),
        new("ship_pistol", "Pistol shipment", Icon.Pistol, 600, "Crate with a pistol (press E to equip).", "gundealer"),
        new("ship_smg", "SMG shipment", Icon.Smg, 1500, "Crate with an SMG.", "gundealer"),
        new("ship_shotgun", "Shotgun shipment", Icon.Shotgun, 1800, "Crate with a shotgun.", "gundealer"),
    ];

    public const float PayInterval = 60f;
    public const int PropCost = 20;
    public bool Active { get; private set; }
    public int Money { get; private set; }
    public Job Job { get; private set; } = Jobs[0];
    public List<string> Laws { get; } = [];
    public float WantedTimer { get; private set; }
    public bool Wanted => Active && WantedTimer > 0f;
    public float PayTimer { get; private set; } = PayInterval;
    public float JailTimer { get; private set; }
    private SessionMeta? _meta;

    public void Reset(SessionMeta? meta)
    {
        _meta = meta;
        Active = meta?.Mode == "Roleplay";
        Money = meta?.Modifiers.StartingMoney ?? 0;
        Job = Jobs[0];
        Laws.Clear();
        if (Active) Laws.AddRange(["Do not steal.", "No weapons in public.", "Printers are illegal."]);
        WantedTimer = 0f; JailTimer = 0f; PayTimer = PayInterval;
    }

    public void Restore(PlayerSave p)
    {
        if (!Active) return;
        Money = p.Money;
        Job = Jobs.FirstOrDefault(j => j.Id == p.Job) ?? Jobs[0];
        if (p.Laws.Count > 0 || Job.Id == "mayor") { Laws.Clear(); Laws.AddRange(p.Laws); }
    }

    public void Capture(PlayerSave p)
    {
        if (!Active) return;
        p.Money = Money; p.Job = Job.Id; p.Laws = Laws.ToList();
    }

    public void Earn(int amount, string? why = null)
    {
        Money += amount;
        if (why is not null) g.Notify($"+${amount:N0} {why}", ToastKind.Success);
        g.Sounds.Play("sfx_coin", 0.5f);
    }

    public bool TrySpend(int amount, string what)
    {
        if (!Active || amount <= 0) return true;
        if (Money < amount) { g.Notify($"Can't afford {what} (${amount:N0})", ToastKind.Danger); return false; }
        Money -= amount;
        return true;
    }

    /// <summary>Roleplay spawn gate: sandbox props cost money unless the modifier is off or the player is a hobo.</summary>
    public bool ChargeForProp()
    {
        if (!Active || _meta is null || !_meta.Modifiers.PaidSpawning || Job.Id == "hobo") return true;
        return TrySpend(PropCost, "that prop");
    }

    public int CountInJob(Job j) => j.Id == Job.Id ? 1 : 0;

    public string? SetJob(Job j)
    {
        if (!Active) return null;
        if (j.Id == Job.Id) return $"You are already a {j.Name}";
        if (Wanted) return "You can't change jobs while wanted";
        Job = j;
        g.Weapons.RefreshAvailability(g);
        if (j.Loadout.FirstOrDefault() is { } first && g.Weapons.All.FirstOrDefault(w => w.Id == first) is { } wpn) g.Weapons.Select(g, wpn);
        if (j.Id == "mayor" && Laws.Count == 0) Laws.Add("Respect the mayor.");
        return $"You are now a {j.Name}";
    }

    public string Buy(Buyable b)
    {
        if (!Active) return "Only in Roleplay";
        if (b.RequiresJob is { } rj && Job.Id != rj) return $"Only a {Jobs.First(j => j.Id == rj).Name} can buy that";
        int price = b.Id == "rp_armor" && Job.Id == "medic" ? b.Price / 2 : b.Price;
        if (!TrySpend(price, b.Name)) return "";
        Float3 pos = g.Player.Trace(4f, out RaycastHit hit) ? hit.Point + hit.Normal * 0.4f : g.Player.EyePosition + g.Player.Forward * 2f;
        switch (b.Id)
        {
            case "rp_printer": SpawnPrinter(pos); break;
            case "rp_armor": g.Health.AddArmor(50f); break;
            case "rp_medkit": g.Health.Heal(50f); break;
            case "rp_ammo": foreach (Firearm f in g.Weapons.All.OfType<Firearm>()) f.Refill(); break;
            default:
                if (b.Id.StartsWith("ship_")) SpawnShipment(pos, "weapon_" + b.Id[5..]);
                break;
        }
        g.Sounds.Play("sfx_coin", 0.6f);
        return $"Bought {b.Name} for ${price:N0}";
    }

    public void DropMoney(int amount)
    {
        if (!Active || amount <= 0 || Money < amount) { g.Notify("Not enough money", ToastKind.Danger); return; }
        Money -= amount;
        SpawnMoney(g.Player.EyePosition + g.Player.Forward * 1.2f, amount);
        g.Notify($"Dropped ${amount:N0}");
    }

    public Prop SpawnMoney(Float3 pos, int amount)
    {
        Prop p = g.Props.Spawn(PropKind.Panel, pos, Random.Shared.NextSingle() * 360f, recordUndo: false);
        Decorate(p, "rp_money", amount.ToString());
        return p;
    }

    public Prop SpawnPrinter(Float3 pos)
    {
        Prop p = g.Props.Spawn(PropKind.SmallCrate, pos, g.Player.Yaw, recordUndo: false);
        Decorate(p, "rp_printer", "0");
        return p;
    }

    public Prop SpawnShipment(Float3 pos, string weaponId)
    {
        Prop p = g.Props.Spawn(PropKind.SmallCrate, pos, g.Player.Yaw, recordUndo: false);
        Decorate(p, "rp_shipment", weaponId);
        return p;
    }

    /// <summary>Applies the look/behaviour for RP entities (also used by map loading).</summary>
    public void Decorate(Prop p, string entity, string? data)
    {
        p.Entity = entity;
        p.EntityData = data;
        switch (entity)
        {
            case "rp_money":
                p.Go.Name = $"${data}";
                p.Go.Transform.LocalScale = new Float3(0.35f, 0.05f, 0.7f) / new Float3(1f, 1f, 1f);
                p.SetColor(new Color(0.35f, 0.70f, 0.38f, 1f));
                p.SetMass(0.2f);
                break;
            case "rp_printer":
                p.Go.Name = "Money Printer";
                p.SetColor(new Color(0.16f, 0.18f, 0.20f, 1f));
                p.SetSurface(SurfaceMaterial.Get("Metal"));
                if (p.Go.GetComponent<MoneyPrinter>() is null) { var mp = p.Go.AddComponent<MoneyPrinter>(); mp.Prop = p; mp.Rp = this; }
                break;
            case "rp_shipment":
                p.Go.Name = $"Shipment: {g.Weapons.All.FirstOrDefault(w => w.Id == data)?.Name ?? "weapons"}";
                p.SetColor(new Color(0.45f, 0.36f, 0.22f, 1f));
                break;
        }
    }

    /// <summary>E on an RP entity. Returns a message, or null if it wasn't one.</summary>
    public string? Use(Prop p)
    {
        if (!Active) return null;
        switch (p.Entity)
        {
            case "rp_money":
                int amount = int.TryParse(p.EntityData, out int a) ? a : 0;
                g.Props.Remove(p);
                Earn(amount);
                return $"Picked up ${amount:N0}";
            case "rp_printer":
                int cash = int.TryParse(p.EntityData, out int c) ? c : 0;
                if (cash <= 0) return "The printer is empty";
                p.EntityData = "0";
                Earn(cash, "from the printer");
                return null;
            case "rp_shipment":
                if (g.Weapons.All.FirstOrDefault(w => w.Id == p.EntityData) is { } wpn)
                {
                    g.Weapons.Select(g, wpn);
                    if (wpn is Firearm f) f.Refill();
                    g.Props.Remove(p);
                    return $"Took the {wpn.Name}";
                }
                return "Empty crate";
        }
        return null;
    }

    public void Arrest(string why)
    {
        if (!Active) return;
        WantedTimer = 0f;
        JailTimer = 10f;
        int fine = Math.Min(Money, 200);
        Money -= fine;
        g.Player.Teleport(MapBuilder.SpawnPoint + new Float3(0f, 0f, -3f));
        g.Notify($"{why}. Fined ${fine:N0}; jailed for 10 s.", ToastKind.Danger);
    }

    public void MakeWanted(string why)
    {
        if (!Active) return;
        WantedTimer = 45f;
        g.Notify($"You are wanted: {why}", ToastKind.Danger);
    }

    public void OnDeath()
    {
        if (!Active) return;
        int lost = Money / 10;
        if (lost > 0) { Money -= lost; SpawnMoney(g.Player.FeetPosition + new Float3(0f, 0.5f, 0f), lost); }
        WantedTimer = 0f;
    }

    public void OnNpcDown(DummyBrain npc)
    {
        if (!Active) return;
        if (npc.Team == NpcTeam.Hostile && Job.Id == "police") Earn(150, "for stopping a criminal");
        else if (npc.Team is NpcTeam.Citizen or NpcTeam.Police && Job.Id != "police") MakeWanted("assault");
    }

    public void SpawnTownNpcs()
    {
        for (int i = 0; i < 6; i++)
        {
            float a = i * 1.05f, r = 8f + i * 2.5f;
            float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
            EntityCatalog.SpawnDummy(g, new Float3(x, MapBuilder.GroundHeight(x, z, 0f), z), i * 60f, recordUndo: false, team: i < 4 ? NpcTeam.Citizen : NpcTeam.Police);
        }
    }

    public void Tick(float dt)
    {
        if (!Active) return;
        if (WantedTimer > 0f) WantedTimer -= dt;
        if (JailTimer > 0f) JailTimer -= dt;
        PayTimer -= dt;
        if (PayTimer <= 0f)
        {
            PayTimer = PayInterval;
            if (JailTimer > 0f) g.Notify("No salary while in jail");
            else Earn(Job.Salary, $"salary ({Job.Name})");
        }
    }

    public int PrinterBonus(int baseAmount) => Job.Id == "gangster" ? baseAmount * 5 / 4 : baseAmount;
}

/// <summary>Prints money into its own internal tray (saved in Prop.EntityData) every 20 s.</summary>
public sealed class MoneyPrinter : MonoBehaviour
{
    public Prop? Prop;
    public RoleplaySystem? Rp;
    private float _t;
    public override void Update()
    {
        if (Prop is null || Rp is null || !Rp.Active) return;
        _t += Time.DeltaTime;
        if (_t < 20f) return;
        _t = 0f;
        int cash = int.TryParse(Prop.EntityData, out int c) ? c : 0;
        Prop.EntityData = Math.Min(cash + Rp.PrinterBonus(25), 2000).ToString();
    }
}

/// <summary>Police baton: hitting a wanted/hostile NPC knocks it out and (as police) pays a bounty.</summary>
public sealed class BatonWeapon : Weapon
{
    public override string Id => "weapon_baton";
    public override string Name => "Arrest Baton";
    public override int Slot => 1;
    public override Icon Icon => Icon.Gavel;
    public override string? ViewModelId => "crowbar";
    public override bool Available(SandboxGame g) => g.Roleplay.Active && g.Roleplay.Job.Id == "police";
    private float _cd;
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        _cd -= dt;
        if (!i.PrimaryDown || _cd > 0f) return;
        _cd = 0.6f;
        Vm?.Play("fire");
        g.Sounds.Play("sfx_swing", 0.5f);
        if (!g.Player.Trace(2.2f, out RaycastHit hit)) return;
        g.Sounds.Play("sfx_impact", 0.7f, hit.Point);
        Fx.Push(hit.Rigidbody, g.Player.Forward * 6f, hit.Point);
        if (g.Props.FromBody(hit.Rigidbody) is { } p && Ragdoll.All.FirstOrDefault(r => r.Limbs.Contains(p)) is { } rd
            && rd.Pelvis.Go.GetComponent<DummyBrain>() is { Limp: false } brain)
        {
            brain.GoLimp();
            g.Notify(brain.Team == NpcTeam.Hostile ? "Arrested a criminal" : "Knocked out a citizen", brain.Team == NpcTeam.Hostile ? ToastKind.Success : ToastKind.Danger);
            g.Roleplay.OnNpcDown(brain);
        }
    }
}

/// <summary>Thief lockpick: hold on a frozen prop or a money printer for 3 s to unlock/steal; risks becoming wanted.</summary>
public sealed class LockpickWeapon : Weapon
{
    public override string Id => "weapon_lockpick";
    public override string Name => "Lockpick";
    public override int Slot => 1;
    public override Icon Icon => Icon.Key;
    public override string? ViewModelId => "hands";
    public override bool Available(SandboxGame g) => g.Roleplay.Active && g.Roleplay.Job.Id == "thief";
    public float Progress { get; private set; }
    private Prop? _target;
    public override string? AmmoText => Progress > 0f ? $"{Progress * 100f:0}%" : null;
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        Prop? aimed = g.Player.Trace(2.5f, out RaycastHit hit) ? g.Props.FromBody(hit.Rigidbody) : null;
        bool valid = aimed is not null && (aimed.Frozen || aimed.Entity == "rp_printer");
        if (!i.PrimaryHeld || !valid || !ReferenceEquals(aimed, _target)) { Progress = 0f; _target = valid && i.PrimaryHeld ? aimed : null; return; }
        Progress += dt / 3f;
        if (Progress < 1f) return;
        Progress = 0f;
        if (_target!.Entity == "rp_printer") g.Notify(g.Roleplay.Use(_target) ?? "Stole the printer's cash");
        else { _target.SetFrozen(false); g.Notify("Lock picked"); }
        g.Sounds.Play("sfx_tool", 0.6f);
        if (Random.Shared.NextSingle() < 0.5f) g.Roleplay.MakeWanted("lockpicking");
        _target = null;
    }
}

/// <summary>Medic medkit: heal yourself (primary) over time.</summary>
public sealed class MedkitWeapon : Weapon
{
    public override string Id => "weapon_medkit";
    public override string Name => "Medkit";
    public override int Slot => 5;
    public override Icon Icon => Icon.Heart;
    public override string? ViewModelId => "grenade";
    public override bool Available(SandboxGame g) => g.Roleplay.Active && g.Roleplay.Job.Id == "medic";
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        if (i.PrimaryHeld && g.Health.Hp < HealthSystem.MaxHp) g.Health.Heal(dt * 20f);
    }
}
