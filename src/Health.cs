using Prowl.PaperUI;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>Player health and armour, fall damage, death and respawn, with a minimal HUD chip.</summary>
public sealed class HealthSystem
{
    public float Hp { get; private set; } = 100f;
    public float Armor { get; private set; }
    public const float MaxHp = 100f, MaxArmor = 100f;
    public bool FallDamage { get; set; } = true;
    /// <summary>God mode (sandbox default): damage still flashes but never kills.</summary>
    public bool Dead => _deadTimer > 0f;
    public float DeadTimer => _deadTimer;
    private float _deadTimer, _flash, _regenDelay;
    public int Deaths { get; private set; }

    public void Set(float hp, float armor) { Hp = Math.Clamp(hp <= 0 ? MaxHp : hp, 1f, MaxHp); Armor = Math.Clamp(armor, 0f, MaxArmor); _deadTimer = 0f; }
    public void AddArmor(float a) => Armor = Math.Clamp(Armor + a, 0f, MaxArmor);
    public void Heal(float a) => Hp = Math.Clamp(Hp + a, 0f, MaxHp);

    public void Damage(SandboxGame g, float amount, string cause)
    {
        if (Dead || amount <= 0f || g.Player.Noclip) return;
        float absorbed = MathF.Min(Armor, amount * 0.6f);
        Armor -= absorbed;
        Hp -= amount - absorbed;
        _flash = MathF.Min(1f, _flash + amount / 40f);
        _regenDelay = 6f;
        g.Player.Punch(MathF.Min(6f, amount * 0.15f));
        g.Sounds.Play("sfx_hurt", 0.6f);
        if (Hp <= 0f)
        {
            Hp = 0f;
            _deadTimer = 3f;
            Deaths++;
            g.Notify($"You died ({cause})", ToastKind.Danger);
            g.Roleplay.OnDeath();
        }
    }

    public void Tick(SandboxGame g, float dt)
    {
        _flash = MathF.Max(0f, _flash - dt * 1.5f);
        float impact = g.Player.LandingImpact;
        g.Player.LandingImpact = 0f;
        if (FallDamage && impact > 13f && !g.InVehicle) Damage(g, (impact - 13f) * 7f, "fell");
        if (_deadTimer > 0f)
        {
            _deadTimer -= dt;
            if (_deadTimer <= 0f)
            {
                g.Player.Teleport(MapBuilder.NextSpawn());
                Hp = MaxHp; Armor = 0f; _flash = 0f;
                g.Notify("Respawned");
            }
            return;
        }
        // Slow regen to 100 after a few seconds without damage (sandbox-friendly).
        if (_regenDelay > 0f) _regenDelay -= dt; else if (Hp < MaxHp) Hp = MathF.Min(MaxHp, Hp + dt * 4f);
    }

    /// <summary>Minimal chip: heart + HP, shield + armour (only when > 0). Red vignette on damage / death.</summary>
    public float DrawHud(Paper paper, float x, float y, float h)
    {
        float hpW = Theme.S(70);
        Color hpC = Hp > 50 ? Theme.Text : Hp > 25 ? Theme.Warning : Theme.Danger;
        Ui.Glass(paper, "hp_chip", x, y, hpW, h, h / 2f, shadow: false).IsNotInteractable();
        Ui.Icon(paper, "hp_ic", x + Theme.S(9), y + (h - Theme.S(14)) / 2f, Theme.S(14), Icon.Heart, Theme.Danger);
        Ui.Text(paper, "hp_t", x + Theme.S(28), y, hpW, h, $"{MathF.Ceiling(Hp):0}", Theme.Caption, hpC, Theme.Bold);
        float used = hpW + Theme.S(6);
        if (Armor > 0.5f)
        {
            Ui.Glass(paper, "ar_chip", x + used, y, hpW, h, h / 2f, shadow: false).IsNotInteractable();
            Ui.Icon(paper, "ar_ic", x + used + Theme.S(9), y + (h - Theme.S(14)) / 2f, Theme.S(14), Icon.Shield, Theme.Accent);
            Ui.Text(paper, "ar_t", x + used + Theme.S(28), y, hpW, h, $"{MathF.Ceiling(Armor):0}", Theme.Caption, Theme.Text, Theme.Bold);
            used += hpW + Theme.S(6);
        }
        return used;
    }

    public void DrawOverlay(Paper paper, float w, float h)
    {
        float a = Dead ? 0.55f : _flash * 0.35f + (Hp < 25f ? 0.12f + 0.06f * MathF.Sin(Environment.TickCount64 / 180f) : 0f);
        if (a > 0.01f)
            paper.Box("hp_vignette").PositionType(PositionType.SelfDirected).Position(0, 0).Size(w, h).IsNotInteractable()
                .BackgroundRadialGradient(w * 0.5f, h * 0.5f, MathF.Min(w, h) * 0.25f, MathF.Max(w, h) * 0.75f, Color.FromArgb(0, 0, 0, 0), Color.FromArgb((int)(255 * Math.Clamp(a, 0f, 1f)), 150, 10, 10));
        if (Dead)
        {
            Ui.Text(paper, "dead_t", 0, h * 0.42f, w, Theme.S(48), "You died", Theme.Heading + 6, Theme.Text, Theme.Bold, TextAlignment.MiddleCenter);
            Ui.Text(paper, "dead_s", 0, h * 0.42f + Theme.S(46), w, Theme.S(20), $"Respawning in {MathF.Ceiling(_deadTimer):0}…", Theme.Label, Theme.TextMuted, null, TextAlignment.MiddleCenter);
        }
    }
}
