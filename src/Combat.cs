using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Vector;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// NPC health/damage: bullets, melee and explosions damage NPC ragdolls (head ×2). At 0 HP the NPC goes limp
/// and a kill-feed entry appears. Optional floating damage numbers (Settings → Game).
/// </summary>
public sealed class Combat(SandboxGame g)
{
    public const float NpcMaxHealth = 100f, HeadshotMultiplier = 2f;
    private sealed record Number(Float3 At, string Text, Color Color) { public float T; }
    private sealed record Feed(string Killer, string Victim, string Weapon, bool Headshot) { public float T; }
    private readonly List<Number> _numbers = [];
    private readonly List<Feed> _feed = [];
    public int Kills { get; private set; }
    public IReadOnlyList<string> FeedLines => _feed.Select(f => $"{f.Killer} [{f.Weapon}{(f.Headshot ? " · headshot" : "")}] {f.Victim}").ToList();
    public int NumberCount => _numbers.Count;

    public static string NpcName(NpcTeam t) => t switch
    {
        NpcTeam.Hostile => "Criminal", NpcTeam.Citizen => "Citizen", NpcTeam.Police => "Police officer",
        NpcTeam.Friendly => "Friendly NPC", _ => "Dummy",
    };

    /// <summary>Resolves a hit body to an NPC brain (if it's a living NPC limb).</summary>
    public DummyBrain? NpcFrom(Rigidbody3D? body, out bool head)
    {
        head = false;
        if (g.Props.FromBody(body) is not { } p) return null;
        Ragdoll? rd = Ragdoll.All.FirstOrDefault(r => r.Limbs.Contains(p));
        if (rd?.Pelvis.Go.GetComponent<DummyBrain>() is not { Limp: false } brain) return null;
        head = p.Go.Name.EndsWith(" Head");
        return brain;
    }

    /// <summary>Damage whatever was hit. Returns damage dealt to an NPC (0 if it wasn't one).</summary>
    public float Hit(Rigidbody3D? body, float damage, Float3 at, string weapon)
    {
        if (damage <= 0f || !float.IsFinite(damage)) return 0f;
        if (NpcFrom(body, out bool head) is not { } brain) return 0f;
        return DamageNpc(brain, head ? damage * HeadshotMultiplier : damage, at, weapon, head);
    }

    public float DamageNpc(DummyBrain brain, float damage, Float3 at, string weapon, bool head = false)
    {
        if (brain.Limp || brain.Health <= 0f) return 0f;
        float dealt = MathF.Min(brain.Health, damage);
        brain.Health -= damage;
        if (g.Settings.DamageNumbers)
            _numbers.Add(new Number(at, $"{MathF.Round(damage):0}", head ? Theme.Warning : Theme.Text));
        if (_numbers.Count > 32) _numbers.RemoveAt(0);
        if (brain.Health <= 0f)
        {
            brain.Health = 0f;
            brain.GoLimp();
            Kills++;
            _feed.Add(new Feed("You", NpcName(brain.Team), weapon, head));
            if (_feed.Count > 5) _feed.RemoveAt(0);
            g.Roleplay.OnNpcDown(brain);
            g.Achievements.Event("first_blood");
        }
        return dealt;
    }

    /// <summary>Radial explosion damage, once per NPC (closest limb).</summary>
    public void Explosion(Float3 at, float radius, float maxDamage)
    {
        foreach (Ragdoll rd in Ragdoll.All.ToArray())
        {
            if (rd.Pelvis.Go.GetComponent<DummyBrain>() is not { Limp: false } brain) continue;
            float best = float.MaxValue;
            foreach (Prop l in rd.Limbs) if (l.Alive) best = MathF.Min(best, Float3.Length(l.Go.Transform.Position - at));
            if (best > radius) continue;
            DamageNpc(brain, maxDamage * (1f - best / radius), rd.Pelvis.Go.Transform.Position + new Float3(0f, 0.6f, 0f), "Explosion");
        }
    }

    public void Tick(float dt)
    {
        foreach (Number n in _numbers) n.T += dt;
        _numbers.RemoveAll(n => n.T > 1.1f);
        foreach (Feed f in _feed) f.T += dt;
        _feed.RemoveAll(f => f.T > 6f);
    }

    public void Clear() { _numbers.Clear(); _feed.Clear(); }

    /// <summary>World → screen using the player camera (vertical FOV).</summary>
    public static bool Project(SandboxGame g, Float3 p, float w, float h, out float sx, out float sy)
    {
        sx = sy = 0f;
        Transform cam = g.Player.CameraObject.Transform;
        Float3 d = p - cam.Position;
        float z = Float3.Dot(d, cam.Forward);
        if (z < 0.2f) return false;
        float f = h / 2f / MathF.Tan(g.Player.Camera.FieldOfView * MathF.PI / 360f);
        sx = w / 2f + Float3.Dot(d, cam.Right) / z * f;
        sy = h / 2f - Float3.Dot(d, cam.Up) / z * f;
        return sx > -50f && sx < w + 50f && sy > -50f && sy < h + 50f;
    }

    public void Draw(Paper paper, float w, float h)
    {
        int i = 0;
        foreach (Number n in _numbers)
        {
            if (!Project(g, n.At + new Float3(0f, n.T * 0.6f, 0f), w, h, out float sx, out float sy)) { i++; continue; }
            float a = Math.Clamp((1.1f - n.T) * 3f, 0f, 1f);
            float old = Ui.Alpha; Ui.Alpha = a;
            Ui.Text(paper, "dmg_" + i++, sx - Theme.S(30), sy - Theme.S(10), Theme.S(60), Theme.S(20), n.Text, Theme.Label, n.Color, Theme.Bold, TextAlignment.MiddleCenter);
            Ui.Alpha = old;
        }
        float y = Theme.S(56), m = Theme.S(20);
        int k = 0;
        foreach (Feed f in _feed)
        {
            string text = $"{f.Killer}  ›  {f.Victim}";
            string tag = f.Headshot ? $"{f.Weapon} · HS" : f.Weapon;
            float tw = (float)paper.MeasureText(text, Theme.S(Theme.Caption), Theme.SemiBold, 0f).X;
            float gw = (float)paper.MeasureText(tag, Theme.S(Theme.Micro), Theme.SemiBold, 0f).X;
            float cw = tw + gw + Theme.S(34), ch = Theme.S(24), x = w - m - cw;
            float a = Math.Clamp((6f - f.T) * 2f, 0f, 1f);
            float old = Ui.Alpha; Ui.Alpha = a;
            Ui.Glass(paper, "kf_bg" + k, x, y, cw, ch, ch / 2f, shadow: false).IsNotInteractable();
            Ui.Text(paper, "kf_t" + k, x + Theme.S(12), y, tw + Theme.S(4), ch, text, Theme.Caption, Theme.Text, Theme.SemiBold);
            Ui.Text(paper, "kf_w" + k, x + Theme.S(20) + tw, y, gw + Theme.S(4), ch, tag, Theme.Micro, f.Headshot ? Theme.Warning : Theme.TextFaint, Theme.SemiBold);
            Ui.Alpha = old;
            y += ch + Theme.S(4); k++;
        }
    }
}
