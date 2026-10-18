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
        new("cardealer", "Car Dealer", System.Drawing.Color.FromArgb(255, 70, 190, 200), 55, 2, "Sell cars. Buys buggies and jeeps at trade price and earns commission when citizens buy.", [], "Cars 40% off"),
        new("hobo", "Hobo", System.Drawing.Color.FromArgb(255, 140, 100, 60), 10, 5, "Live on the street. Small salary, but you never pay for props.", [], "Free props"),
    ];

    public static readonly Buyable[] Shop =
    [
        new("rp_printer", "Money Printer", Icon.Money, 1000, "Prints $25 every 20 s. Press E on it to collect."),
        new("rp_armor", "Armour vest", Icon.Shield, 250, "+50 armour."),
        new("rp_medkit", "Health kit", Icon.Heart, 120, "Restores 50 health."),
        new("rp_ammo", "Ammo box", Icon.Crate, 60, "Refills reserve ammo for every gun."),
        new("rp_burger", "Burger", Icon.Heart, 30, "+45 hunger. Hunger drains over ~8 minutes."),
        new("rp_soda", "Soda", Icon.Heart, 10, "+15 hunger."),
        new("car_buggy", "Buggy", Icon.Car, 900, "A light dune buggy, delivered in front of you."),
        new("car_jeep", "Jeep", Icon.Car, 1600, "Four-wheel drive. Sturdier and faster off-road."),
        new("rp_ticket", "Lottery ticket", Icon.Money, LotteryTicket, "One ticket in the mayor's lottery (only while one runs)."),
        new("rp_lottery", "Start a lottery", Icon.Gavel, 200, "Seeds the pot with $200. Runs 90 s; you keep 10% of the pot.", "mayor"),
        new("ship_pistol", "Pistol shipment", Icon.Pistol, 600, "Crate with a pistol (press E to equip).", "gundealer"),
        new("ship_smg", "SMG shipment", Icon.Smg, 1500, "Crate with an SMG.", "gundealer"),
        new("ship_shotgun", "Shotgun shipment", Icon.Shotgun, 1800, "Crate with a shotgun.", "gundealer"),
    ];

    public const float PayInterval = 60f;

    // ---------------------------------------------------------------- lottery (mayor)
    public const int LotteryTicket = 50;
    public const float LotteryLength = 90f;
    /// <summary>Seconds left in the running lottery (0 = none).</summary>
    public float LotteryTimer { get; private set; }
    public int LotteryPot { get; private set; }
    public int MyTickets { get; private set; }
    public int TownTickets { get; private set; }
    public bool LotteryRunning => LotteryTimer > 0f;
    private float _townBuy;
    /// <summary>Picks the winning ticket (index into all tickets); swap in a fixed sequence for tests.</summary>
    public Func<int, int> LotteryDraw { get; set; } = n => Random.Shared.Next(n);
    public string? LastLottery { get; private set; }

    public string StartLottery()
    {
        if (!Active) return "Only in Roleplay";
        if (Job.Id != "mayor") return "Only the Mayor can start a lottery";
        if (LotteryRunning) return "A lottery is already running";
        LotteryTimer = LotteryLength; LotteryPot = 200; MyTickets = 0; TownTickets = 0; _townBuy = 4f;
        return $"Lottery started: tickets ${LotteryTicket}, draw in {LotteryLength:0} s";
    }

    private void TickLottery(float dt)
    {
        if (!LotteryRunning) return;
        // Townsfolk buy tickets now and then (stand-ins for other players).
        _townBuy -= dt;
        if (_townBuy <= 0f) { _townBuy = 6f + Random.Shared.NextSingle() * 8f; TownTickets++; LotteryPot += LotteryTicket; }
        LotteryTimer -= dt;
        if (LotteryTimer <= 0f) DrawLottery();
    }

    public string DrawLottery()
    {
        LotteryTimer = 0f;
        int total = MyTickets + TownTickets;
        int cut = LotteryPot / 10, prize = LotteryPot - cut;
        bool mayor = Job.Id == "mayor";
        string msg;
        if (total == 0) { msg = "Lottery: nobody bought a ticket"; if (mayor) Earn(LotteryPot, "lottery refund"); }
        else if (LotteryDraw(total) < MyTickets) { Earn(prize + (mayor ? cut : 0), "lottery win"); msg = $"You won the lottery: ${prize:N0}!"; }
        else { if (mayor) Earn(cut, "lottery cut"); msg = $"Lottery: a townsperson won ${prize:N0}"; }
        LastLottery = msg;
        g.Notify(msg, ToastKind.Success);
        MyTickets = 0; TownTickets = 0; LotteryPot = 0;
        return msg;
    }

    /// <summary>Car Dealer pays trade price.</summary>
    public int PriceOf(Buyable b) => b.Id == "rp_armor" && Job.Id == "medic" ? b.Price / 2 : b.Id.StartsWith("car_") && Job.Id == "cardealer" ? b.Price * 6 / 10 : b.Price;
    /// <summary>Hunger 0-100 (100 = full). Drains fully in <see cref="HungerSeconds"/>; at 0 you starve (2 hp / 4 s).</summary>
    public float Hunger { get; private set; } = 100f;
    public const float HungerSeconds = 480f, StarveInterval = 4f, StarveDamage = 2f;
    private float _starve;
    public bool Starving => Active && Hunger <= 0f;
    public void Eat(float amount) { Hunger = Math.Clamp(Hunger + amount, 0f, 100f); _starve = StarveInterval; }
    /// <summary>Hunger tick, split out so tests can drive it with a large dt.</summary>
    public void TickHunger(float dt)
    {
        if (!Active || JailTimer > 0f || g.Health.Dead) return;
        float before = Hunger;
        Hunger = MathF.Max(0f, Hunger - dt * 100f / HungerSeconds);
        if (before > 25f && Hunger <= 25f) g.Notify("You're getting hungry · buy food from the F4 shop");
        if (Hunger > 0f || before > 0f) { if (Hunger > 0f) _starve = StarveInterval; return; } // starve time starts once empty
        _starve -= dt;
        if (_starve <= 0f) { _starve = StarveInterval; g.Health.Damage(g, StarveDamage, "starvation"); } // max one hit per tick
    }
    public const int PropCost = 20;
    public bool Active { get; private set; }
    public int Money { get; private set; }
    public Job Job { get; private set; } = Jobs[0];
    public List<string> Laws { get; } = [];
    public float WantedTimer { get; private set; }
    public bool Wanted => Active && WantedTimer > 0f;
    public void ClearWanted() => WantedTimer = 0f;
    /// <summary>Weapons bought from shipments this life (RP); the job loadout and the build tools are always allowed.</summary>
    public readonly HashSet<string> OwnedWeapons = [];
    public static readonly string[] BaseKit = ["weapon_physgun", "weapon_toolgun", "weapon_hands"];
    public bool Allows(Weapon w) => !Active || BaseKit.Contains(w.Id) || Job.Loadout.Contains(w.Id) || OwnedWeapons.Contains(w.Id);
    public float PayTimer { get; private set; } = PayInterval;
    public float JailTimer { get; private set; }
    public int Bank { get; private set; }
    public bool AtAtm { get; set; }
    public int Arrests { get; private set; }
    public bool Jailed => Active && JailTimer > 0f;
    private float _brandish;
    private SessionMeta? _meta;

    public void Reset(SessionMeta? meta)
    {
        _meta = meta;
        Active = meta?.Mode == "Roleplay";
        Money = meta?.Modifiers.StartingMoney ?? 0;
        Job = Jobs[0];
        Laws.Clear();
        if (Active) Laws.AddRange(["Do not steal.", "No weapons in public.", "Printers are illegal."]);
        WantedTimer = 0f; JailTimer = 0f; PayTimer = PayInterval; Bank = 0; Arrests = 0; AtAtm = false;
        Hunger = 100f; _starve = StarveInterval; OwnedWeapons.Clear();
        LotteryTimer = 0f; LotteryPot = 0; MyTickets = 0; TownTickets = 0; LastLottery = null;
    }

    public void Restore(PlayerSave p)
    {
        if (!Active) return;
        Money = p.Money;
        Job = Jobs.FirstOrDefault(j => j.Id == p.Job) ?? Jobs[0];
        if (p.Laws.Count > 0 || Job.Id == "mayor") { Laws.Clear(); Laws.AddRange(p.Laws); }
        Bank = p.Bank; JailTimer = p.JailTimer; WantedTimer = p.WantedTimer;
        Hunger = float.IsFinite(p.Hunger) ? Math.Clamp(p.Hunger, 0f, 100f) : 100f;
        foreach (Door d in Town.Doors)
        {
            if (p.OwnedDoors.Contains(d.Id)) d.Owner = "you";
            d.Locked = p.LockedDoors.Contains(d.Id) || (d.Locked && d.Owner is "police") || d.ForSale;
        }
        if (Jailed && Town.HasJail) g.Player.Teleport(Town.CellCenter + new Float3(0f, 0.2f, 0f));
    }

    public void Capture(PlayerSave p)
    {
        if (!Active) return;
        p.Money = Money; p.Job = Job.Id; p.Laws = Laws.ToList();
        p.Bank = Bank; p.JailTimer = JailTimer; p.WantedTimer = WantedTimer; p.Hunger = Hunger;
        p.OwnedDoors = Town.Doors.Where(d => d.Owner == "you").Select(d => d.Id).ToList();
        p.LockedDoors = Town.Doors.Where(d => d.Locked).Select(d => d.Id).ToList();
    }

    // ---------------------------------------------------------------- doors, ATM

    public bool CanUnlock(Door d) => d.Owner == "you" || d.SharedKeys.Contains("you") || (Active && d.Jobs.Contains(Job.Id))
        || (!Active && d.Owner != "police");

    /// <summary>E on a door: opens/closes it, or unlocks it if it's yours.</summary>
    public string UseDoor(Door d)
    {
        if (d.Locked || (Active && d.ForSale && !d.Open))
        {
            if (!CanUnlock(d))
            {
                g.Sounds.Play("sfx_impact", 0.35f, d.Transform.Position);
                return Active && d.ForSale ? $"{d.Title} is for sale · press {Binds.KeyName(Prowl.Runtime.KeyCode.F2)} to buy (${d.Price:N0})" : $"{d.Title} is locked";
            }
            d.Locked = false;
        }
        d.SetOpen(!d.Open);
        g.Sounds.Play("sfx_door", 0.5f, d.Transform.Position);
        return d.Open ? $"Opened {d.Title}" : $"Closed {d.Title}";
    }

    /// <summary>F2 on a door: buy it if it's for sale, sell it back (half price) if it's yours.</summary>
    public string BuyOrSellDoor(Door d)
    {
        if (!Active) return "Doors can be bought in Roleplay";
        if (d.Owner == "you")
        {
            int refund = d.Price / 2;
            d.Owner = ""; d.SharedKeys.Clear(); d.SetOpen(false); d.Locked = true;
            Earn(refund);
            return $"Sold {d.Title} for ${refund:N0}";
        }
        if (d.Owner != "" || d.Price <= 0) return $"{d.Title} isn't for sale";
        if (!TrySpend(d.Price, d.Title)) return "";
        d.Owner = "you";
        g.Sounds.Play("sfx_coin", 0.6f);
        return $"You own {d.Title} · L to lock";
    }

    /// <summary>K on your door: hand a key to the nearest living citizen/police/friendly NPC within 8 m. Shift+K takes all keys back.</summary>
    public string GiveKey(Door d, bool revoke)
    {
        if (!Active) return "Keys can be shared in Roleplay";
        if (d.Owner != "you") return "Only the owner can hand out keys";
        if (revoke)
        {
            int n = d.SharedKeys.Count; d.SharedKeys.Clear();
            return n == 0 ? $"Nobody else has a key to {d.Title}" : $"Took back {n} key{(n == 1 ? "" : "s")} to {d.Title}";
        }
        Ragdoll? best = null; float bd = 8f;
        foreach (Ragdoll r in Ragdoll.All)
        {
            if (r.Team is NpcTeam.Hostile or NpcTeam.Dummy || !r.Pelvis.Alive) continue;
            if (r.Pelvis.Go.GetComponent<DummyBrain>() is not { Limp: false }) continue;
            float dist = Prowl.Vector.Float3.Length(r.Pelvis.Go.Transform.Position - g.Player.FeetPosition);
            if (dist < bd) { bd = dist; best = r; }
        }
        if (best is null) return "Stand near someone (within 8 m) to give them a key";
        string who = $"the {Combat.NpcName(best.Team).ToLowerInvariant()} #{best.Name[(best.Name.LastIndexOf('#') + 1)..]}";
        if (!d.SharedKeys.Add(best.Name)) return $"{Cap(who)} already has a key to {d.Title}";
        g.Sounds.Play("sfx_coin", 0.4f);
        return $"Gave {who} a key to {d.Title}";
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public string ToggleLock(Door d)
    {
        if (!CanUnlock(d)) return "You don't have the keys";
        if (d.Open && !d.Locked) d.SetOpen(false);
        d.Locked = !d.Locked;
        g.Sounds.Play("sfx_tool", 0.4f, d.Transform.Position);
        return d.Locked ? $"Locked {d.Title}" : $"Unlocked {d.Title}";
    }

    /// <summary>A thief picked <paramref name="d"/>: it unlocks and swings open; picking police locks always gets you wanted.</summary>
    public string PickDoor(Door d)
    {
        d.Locked = false;
        d.SetOpen(true);
        if (d.Owner == "police" || Random.Shared.NextSingle() < 0.5f) MakeWanted($"breaking into {d.Title}");
        return $"Picked the lock on {d.Title}";
    }

    public string Deposit(int amount)
    {
        amount = Math.Min(amount, Money);
        if (amount <= 0) return "Nothing to deposit";
        Money -= amount; Bank += amount;
        g.Sounds.Play("sfx_coin", 0.5f);
        return $"Deposited ${amount:N0}";
    }

    public string Withdraw(int amount)
    {
        amount = Math.Min(amount, Bank);
        if (amount <= 0) return "Your account is empty";
        Bank -= amount; Money += amount;
        g.Sounds.Play("sfx_coin", 0.5f);
        return $"Withdrew ${amount:N0}";
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
        int price = PriceOf(b);
        if (b.Id == "rp_ticket" && !LotteryRunning) return "No lottery is running";
        if (b.Id == "rp_lottery") { if (Job.Id != "mayor" || LotteryRunning) return StartLottery(); }
        if (!TrySpend(price, b.Name)) return "";
        Float3 pos = g.Player.Trace(4f, out RaycastHit hit) ? hit.Point + hit.Normal * 0.4f : g.Player.EyePosition + g.Player.Forward * 2f;
        switch (b.Id)
        {
            case "rp_printer": SpawnPrinter(pos); break;
            case "rp_armor": g.Health.AddArmor(50f); break;
            case "rp_medkit": g.Health.Heal(50f); break;
            case "rp_ammo": foreach (Firearm f in g.Weapons.All.OfType<Firearm>()) f.Refill(); break;
            case "rp_burger": Eat(45f); break;
            case "rp_soda": Eat(15f); break;
            case "rp_ticket": MyTickets++; LotteryPot += price; return $"Lottery ticket bought ({MyTickets} held, pot ${LotteryPot:N0})";
            case "rp_lottery": return StartLottery();
            case "car_buggy" or "car_jeep":
            {
                Float3 at = g.Player.FeetPosition + Player.Flat(g.Player.Forward) * 4.5f + new Float3(0f, 1.2f, 0f);
                g.Vehicles.SpawnBuggy(at, g.Player.Yaw + 90f, recordUndo: false, kind: b.Id[4..]);
                break;
            }
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
                    OwnedWeapons.Add(wpn.Id);
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
        JailTimer = Town.HasJail ? 30f : 10f;
        Arrests++;
        int fine = Math.Min(Money, 200);
        Money -= fine;
        g.Player.Teleport(Town.HasJail ? Town.CellCenter + new Float3(0f, 0.2f, 0f) : MapBuilder.SpawnPoint + new Float3(0f, 0f, -3f));
        if (Town.Find("door_cell") is { } cell) { cell.SetOpen(false); cell.Locked = true; }
        g.Notify($"{why}. Fined ${fine:N0}; jailed for {JailTimer:0} s.", ToastKind.Danger);
    }

    public void MakeWanted(string why)
    {
        if (!Active) return;
        bool was = WantedTimer > 0f;
        WantedTimer = 45f;
        WantedFor = why;
        if (!was) g.Notify($"You are wanted: {why}", ToastKind.Danger);
    }

    public string WantedFor { get; private set; } = "";

    public void OnDeath()
    {
        if (!Active) return;
        int lost = Money / 10;
        if (lost > 0) { Money -= lost; SpawnMoney(g.Player.FeetPosition + new Float3(0f, 0.5f, 0f), lost); }
        WantedTimer = 0f;
        Hunger = MathF.Max(Hunger, 60f);
    }

    public void OnNpcDown(DummyBrain npc)
    {
        if (!Active) return;
        if (npc.Team == NpcTeam.Hostile && Job.Id == "police") Earn(150, "for stopping a criminal");
        else if (npc.Team is NpcTeam.Citizen or NpcTeam.Police && Job.Id != "police" && npc.Doll is { } d
                 && Float3.Length(d.Pelvis.Go.Transform.Position - g.Player.FeetPosition) < 8f) MakeWanted("assault");
    }

    public void SpawnTownNpcs()
    {
        using var _ = g.Props.OwnedBy(PropProtection.World);
        if (Town.Active && Town.Waypoints.Count > 0)
        {
            for (int i = 0; i < 10; i++)
            {
                Float3 w = Town.Waypoints[(i * 7) % Town.Waypoints.Count];
                EntityCatalog.SpawnDummy(g, w + new Float3(0f, 0.2f, 0f), i * 36f, recordUndo: false, team: i < 7 ? NpcTeam.Citizen : NpcTeam.Police);
            }
            return;
        }
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
        TickHunger(dt);
        TickLottery(dt);
        if (WantedTimer > 0f) WantedTimer -= dt;
        if (JailTimer > 0f)
        {
            JailTimer -= dt;
            if (Town.HasJail && !Town.InCell(g.Player.FeetPosition) && JailTimer > 0.5f) { JailTimer = 0f; MakeWanted("jailbreak"); }
            else if (JailTimer <= 0f)
            {
                JailTimer = 0f;
                if (Town.HasJail) g.Player.Teleport(Town.ReleasePoint);
                g.Notify("Released from jail. Behave.", ToastKind.Success);
            }
        }
        // Brandishing: police nearby see you holding a gun while it's against the law.
        bool armedLaw = Laws.Any(l => l.Contains("weapon", StringComparison.OrdinalIgnoreCase));
        bool seen = armedLaw && Job.Id != "police" && g.Weapons.Current is Firearm && !Wanted
            && Ragdoll.All.Any(r => r.Alive && r.Team == NpcTeam.Police && Float3.Length(r.Pelvis.Go.Transform.Position - g.Player.FeetPosition) < 12f);
        _brandish = seen ? _brandish + dt : 0f;
        if (_brandish > 0.01f && _brandish - dt <= 0.01f) g.Notify("Police: put that weapon away!", ToastKind.Danger);
        if (_brandish > 3f) { _brandish = 0f; MakeWanted("carrying a weapon in public"); }
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
    private Door? _door;
    public override string? AmmoText => Progress > 0f ? $"{Progress * 100f:0}%" : null;
    public override void Tick(SandboxGame g, WeaponInput i, float dt)
    {
        Prop? aimed = g.Player.Trace(2.5f, out RaycastHit hit) ? g.Props.FromBody(hit.Rigidbody) : null;
        Door? door = aimed is null && g.Player.Trace(2.5f, out RaycastHit dh) ? Town.DoorFrom(dh) : null;
        if (door is { Locked: true })
        {
            if (!i.PrimaryHeld || !ReferenceEquals(door, _door)) { Progress = 0f; _door = i.PrimaryHeld ? door : null; return; }
            Progress += dt / 4f;
            if (Progress >= 1f) { Progress = 0f; _door = null; g.Notify(g.Roleplay.PickDoor(door)); g.Sounds.Play("sfx_tool", 0.6f); }
            return;
        }
        _door = null;
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
